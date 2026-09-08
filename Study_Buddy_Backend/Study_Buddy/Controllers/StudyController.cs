using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.DTOs;
using StudyBuddy.Interfaces;
using StudyBuddy.Models;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StudyBuddy.Controllers
{
    [ApiController]
    [Route("api/study")]
    public class StudyController : ControllerBase
    {
        private readonly IMistralService _aiService;
        private readonly IChatRepository _chatRepo;
        private readonly StudyBuddyContext _db;

        public StudyController(IMistralService aiService, IChatRepository chatRepo, StudyBuddyContext db)
        {
            _aiService = aiService;
            _chatRepo = chatRepo;
            _db = db;
        }

        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] AskRequest request)
        {
            if (request == null || (string.IsNullOrWhiteSpace(request.Question) && string.IsNullOrWhiteSpace(request.ImageUrl)))
                return BadRequest("Question or image is required.");

            var history = request.History?.Select(h => new ChatMessage
            {
                Question = h.Role == "user" ? h.Content : "",
                Response = h.Role == "assistant" ? h.Content : ""
            }).ToList();

            var docContext = await GetDocumentContext(request.DocumentIds);

            var response = await _aiService.AskAI(request.Question, history, docContext, request.ImageUrl, request.Mode);

            if (string.IsNullOrWhiteSpace(response))
                response = "Sorry, I couldn't generate an answer. Please try again.";

            await _chatRepo.SaveAsync(new ChatMessage
            {
                Question = request.Question,
                Response = response
            });
            await LogActivity("chat");

            return Ok(new { answer = response });
        }

        private async Task<string?> GetDocumentContext(List<int>? documentIds)
        {
            if (documentIds == null || documentIds.Count == 0) return null;
            var docs = await _db.Documents.Where(d => documentIds.Contains(d.Id)).ToListAsync();
            if (docs.Count == 0) return null;
            return string.Join("\n\n---\n\n", docs.Select(d => $"Title: {d.Title}\n{d.Content}"));
        }

        [HttpPost("ask/stream")]
        public async Task AskStream([FromBody] AskRequest request)
        {
            if (request == null || (string.IsNullOrWhiteSpace(request.Question) && string.IsNullOrWhiteSpace(request.ImageUrl)))
            {
                HttpContext.Response.StatusCode = 400;
                await HttpContext.Response.WriteAsync("Question or image is required.");
                return;
            }

            var history = request.History?.Select(h => new ChatMessage
            {
                Question = h.Role == "user" ? h.Content : "",
                Response = h.Role == "assistant" ? h.Content : ""
            }).ToList();

            var docContext = await GetDocumentContext(request.DocumentIds);

            HttpContext.Response.ContentType = "text/event-stream";
            HttpContext.Response.Headers.CacheControl = "no-cache";
            HttpContext.Response.Headers.Connection = "keep-alive";

            using var stream = await _aiService.AskAIStream(request.Question, history, docContext, request.ImageUrl, request.Mode);
            using var reader = new StreamReader(stream);

            var fullResponse = new StringBuilder();
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line)) continue;
                if (line == "data: [DONE]") break;
                if (!line.StartsWith("data: ")) continue;

                var json = line[6..];
                using var doc = JsonDocument.Parse(json);
                var choices = doc.RootElement.GetProperty("choices");
                if (choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0) continue;
                var delta = choices[0].GetProperty("delta");
                if (!delta.TryGetProperty("content", out var contentProp)) continue;

                var chunk = contentProp.GetString() ?? "";
                fullResponse.Append(chunk);

                await HttpContext.Response.WriteAsync($"data: {JsonSerializer.Serialize(new { chunk })}\n\n");
                await HttpContext.Response.Body.FlushAsync();
            }

            var complete = fullResponse.ToString();
            await _chatRepo.SaveAsync(new ChatMessage
            {
                Question = request.Question,
                Response = complete
            });
            await LogActivity("chat");

            await HttpContext.Response.WriteAsync($"data: {JsonSerializer.Serialize(new { done = true })}\n\n");
            await HttpContext.Response.Body.FlushAsync();
        }

        // Generate a quiz for a topic
        [HttpPost("quiz")]
        public async Task<IActionResult> GenerateQuiz([FromBody] QuizRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Topic))
                return BadRequest("Topic is required.");

            var questionCount = Math.Clamp(request.QuestionCount, 3, 20);

            var difficulty = (request.Difficulty ?? "medium").Trim().ToLowerInvariant();
            if (difficulty is not "medium" and not "hard")
                difficulty = "medium";

            string prompt = $@"
You are a {request.Exam ?? "JEE"} quiz generator AI. Generate exactly {questionCount} questions. All questions must be unique, non-duplicate, and relevant to the topic.

{ExamGuidance.TypeMixInstruction(request.Exam)}
{ExamGuidance.SectionFormatInstruction(request.Exam, "", request.Topic)}

Each object in the array must have these exact fields:
- ""id"" (number)
- ""type"" (one of the allowed types listed above)
- ""question"" (string)
- ""options"" (array of 4 strings; empty array for numerical)
- ""correct_answer"" (string; for mcq/truefalse the exact correct option text, for multi the comma-separated correct option texts, for numerical the numeric value, for assertion the letter A/B/C/D, for matching the mapping like ""A-2, B-3, C-1, D-4"")
- ""correct_answers"" (array of strings; ONLY for multi, list the exact correct option texts; omit for other types)
- ""assertion"" (string; ONLY for assertion, the assertion statement; omit otherwise)
- ""reason"" (string; ONLY for assertion, the reason statement; omit otherwise)
- ""right_options"" (array of 4 strings; ONLY for matching, the List-II items; omit otherwise)
- ""passage"" (string; ONLY for passage questions, the comprehension paragraph text; omit otherwise)
- ""explanation"" (string)

For assertion-reason questions, ""options"" must be exactly these four choices:
[""Both A and R are true and R is the correct explanation of A"", ""Both A and R are true but R is NOT the correct explanation of A"", ""A is true but R is false"", ""A is false but R is true""]
and ""correct_answer"" is A, B, C, or D (the letter). ""question"" should be ""Assertion and Reason given below.""

IMPORTANT: Keep explanations brief (1 to 2 sentences). If a question or explanation involves mathematical expressions, use LaTeX notation but ALWAYS escape backslashes as double backslashes. For example write ""What is the value of \\(x\\) in \\(2x + 3 = 7\\)?"" NOT \(x\).

Example:
[
  {{
    ""id"": 1,
    ""type"": ""mcq"",
    ""question"": ""What is the chemical symbol for water?"",
    ""options"": [""H2O"", ""CO2"", ""NaCl"", ""O2""],
    ""correct_answer"": ""H2O"",
    ""correct_answers"": [],
    ""explanation"": ""Water is composed of two hydrogen atoms and one oxygen atom.""
  }},
  {{
    ""id"": 2,
    ""type"": ""numerical"",
    ""question"": ""A ball is dropped from rest. Taking g = 10 m/s^2, find the distance (in metres) it falls in the 2nd second."",
    ""options"": [],
    ""correct_answer"": ""15"",
    ""correct_answers"": [],
    ""explanation"": ""Distance in nth second is u + g(2n-1)/2 = 0 + 10*(2*2-1)/2 = 15 m.""
  }}
]

Topic: '{request.Topic}'
Difficulty: '{difficulty}'

DIFFICULTY RULES: All generated questions must be at the {difficulty} difficulty level or harder. Never produce easy/direct-recall questions. Every question must require the student to apply at least one concept, do meaningful calculation, work through multiple steps, combine ideas, or reason through a tricky edge case. Questions at medium difficulty require applying a formula or concept; questions at hard difficulty require multi-step derivation or problem solving. The questions must be challenging enough that the student has to think and work out the answer.

Return ONLY the valid JSON array. No extra text, no markdown formatting, no code blocks.
";

            const int maxAttempts = 5;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var aiResponse = await _aiService.SendRawPrompt(prompt);

                try
                {
                    var quizRaw = ParseQuizJson(aiResponse);

                    if (quizRaw == null || quizRaw.Count == 0)
                        throw new Exception("Empty quiz");

                    // Drop malformed / hallucinated questions that don't match their declared type.
                    var valid = quizRaw.Where(QuestionMapper.HasValidFormat).ToList();
                    if (valid.Count == 0)
                        throw new Exception("No valid questions");

                    var quiz = new QuizResponse
                    {
                        Topic = request.Topic,
                        Questions = valid.Select(QuestionMapper.ToQuestion).ToList()
                    };

                    return Ok(quiz);
                }
                catch (Exception)
                {
                    if (attempt == maxAttempts - 1)
                        break;
                }
            }

            return Ok(new QuizResponse
            {
                Questions = new List<Question>
                {
                    new Question
                    {
                        Type = "mcq",
                        QuestionText = "AI could not generate a quiz. Please try again with a simpler topic.",
                        Answer = "",
                        Explanation = ""
                    }
                }
            });
        }

        private static List<RawQuestion>? ParseQuizJson(string aiResponse)
        {
            if (string.IsNullOrWhiteSpace(aiResponse)) return null;

            // Extract the JSON array (AI may wrap it in a markdown code block).
            int arrayStart = aiResponse.IndexOf('[');
            if (arrayStart < 0) return null;

            string jsonString = aiResponse.Substring(arrayStart);
            int lastClose = jsonString.LastIndexOf(']');

            if (lastClose >= 0)
            {
                jsonString = jsonString.Substring(0, lastClose + 1);
            }
            else
            {
                // Response was truncated mid-array: keep the last complete object.
                int lastObj = jsonString.LastIndexOf('}');
                if (lastObj < 0) return null;
                jsonString = jsonString.Substring(0, lastObj + 1) + "]";
            }

            // The AI emits LaTeX backslashes inconsistently (some single like \(x\),
            // some already escaped like \\(x\\)). A single backslash such as \( is an
            // invalid JSON escape, so normalize every LONE backslash into a double.
            // Already-doubled backslashes are left untouched and decode back to the
            // correct single backslash the frontend's KaTeX expects.
            jsonString = Regex.Replace(jsonString, @"(?<![\\])\\(?![\\])", @"\\\\");

            return JsonSerializer.Deserialize<List<RawQuestion>>(jsonString);
        }

        [HttpPost("quiz/save")]
        public async Task<IActionResult> SaveQuizResult([FromBody] SaveQuizRequest request)
        {
            var result = new QuizResult
            {
                Topic = request.Topic,
                Difficulty = request.Difficulty,
                Score = request.Score,
                TotalQuestions = request.TotalQuestions,
                TimeSpentSeconds = request.TimeSpentSeconds,
                AnswersJson = JsonSerializer.Serialize(request.Answers ?? new List<UserAnswer>()),
                QuestionsJson = JsonSerializer.Serialize(request.Questions ?? new List<Question>()),
                UserId = GetUserId()
            };

            _db.QuizResults.Add(result);
            await _db.SaveChangesAsync();
            await LogActivity("quiz");

            return Ok(new { id = result.Id });
        }

        [HttpGet("quiz/history")]
        public async Task<IActionResult> GetQuizHistory()
        {
            var userId = GetUserId();
            var query = _db.QuizResults.AsQueryable();
            if (userId.HasValue)
                query = query.Where(q => q.UserId == userId.Value);

            var results = await query
                .OrderByDescending(q => q.CompletedAt)
                .Select(q => new
                {
                    q.Id,
                    q.Topic,
                    q.Difficulty,
                    q.Score,
                    q.TotalQuestions,
                    q.CompletedAt
                })
                .ToListAsync();

            return Ok(results);
        }

        [HttpGet("quiz/history/{id}")]
        public async Task<IActionResult> GetQuizDetail(int id)
        {
            var result = await _db.QuizResults.FindAsync(id);
            if (result == null) return NotFound();

            var userId = GetUserId();
            if (userId.HasValue && result.UserId != userId.Value) return Forbid();

            var answers = SafeDeserialize<UserAnswer>(result.AnswersJson);
            var questions = SafeDeserialize<Question>(result.QuestionsJson);

            return Ok(new
            {
                result.Id,
                result.Topic,
                result.Difficulty,
                result.Score,
                result.TotalQuestions,
                result.CompletedAt,
                Answers = answers,
                Questions = questions
            });
        }

        private static List<T> SafeDeserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<T>();
            try { return JsonSerializer.Deserialize<List<T>>(json) ?? new List<T>(); }
            catch { return new List<T>(); }
        }

        private int? GetUserId()
        {
            var auth = HttpContext.Request.Headers["authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(auth)) return null;
            var user = _db.Users.FirstOrDefault(u => u.Token == auth);
            return user?.Id;
        }

        private async Task LogActivity(string type)
        {
            _db.StudyActivities.Add(new StudyActivity
            {
                Type = type,
                UserId = GetUserId(),
                Timestamp = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
    }
}