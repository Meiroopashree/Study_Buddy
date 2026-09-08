using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Interfaces;
using StudyBuddy.Models;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using Document = StudyBuddy.Models.Document;

namespace StudyBuddy.Controllers
{
    [ApiController]
    [Route("api/papers")]
    public class PaperController : ControllerBase
    {
        private readonly StudyBuddyContext _db;
        private readonly IMistralService _aiService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<PaperController> _logger;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public PaperController(StudyBuddyContext db, IMistralService aiService, IServiceScopeFactory scopeFactory, ILogger<PaperController> logger)
        {
            _db = db;
            _aiService = aiService;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        private int? GetUserId()
        {
            var auth = HttpContext.Request.Headers["authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(auth)) return null;
            var user = _db.Users.FirstOrDefault(u => u.Token == auth);
            return user?.Id;
        }

        private static string ExtractTextFromPdf(Stream stream)
        {
            var sb = new StringBuilder();
            using var document = PdfDocument.Open(stream);
            foreach (var page in document.GetPages())
            {
                sb.AppendLine(page.Text);
            }
            return sb.ToString().Trim();
        }

        private static string ExtractTextFromDocx(Stream stream)
        {
            var sb = new StringBuilder();
            using var wordDoc = WordprocessingDocument.Open(stream, false);
            var body = wordDoc.MainDocumentPart?.Document?.Body;
            if (body != null)
            {
                foreach (var para in body.Elements<Paragraph>())
                {
                    sb.AppendLine(para.InnerText);
                }
            }
            return sb.ToString().Trim();
        }

        private async Task<string> ExtractText(IFormFile file)
        {
            if (file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    ms.Position = 0;
                    return ExtractTextFromPdf(ms);
                }
                catch (Exception)
                {
                    throw new Exception("Could not read this PDF. It may be password-protected or corrupted.");
                }
            }

            if (file.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    ms.Position = 0;
                    return ExtractTextFromDocx(ms);
                }
                catch (Exception)
                {
                    throw new Exception("Could not read this DOCX file. It may be corrupted.");
                }
            }

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".bmp")
            {
                var mime = ext switch
                {
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    ".bmp" => "image/bmp",
                    _ => "image/png"
                };
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var dataUri = $"data:{mime};base64,{Convert.ToBase64String(ms.ToArray())}";
                try
                {
                    var transcribed = await _aiService.TranscribeImage(dataUri);
                    if (!string.IsNullOrWhiteSpace(transcribed)) return transcribed.Trim();
                    throw new Exception("No text detected in this image.");
                }
                catch (Exception ex) when (ex.Message != "No text detected in this image.")
                {
                    throw new Exception("OCR failed. The AI service could not read this image.");
                }
            }

            using var reader = new StreamReader(file.OpenReadStream());
            return (await reader.ReadToEndAsync()).Trim();
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(
            IFormFile file,
            [FromForm] string? exam = null,
            [FromForm] int? year = null)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided." });

            string content;
            try
            {
                content = await ExtractText(file);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }

            if (string.IsNullOrWhiteSpace(content))
                return BadRequest(new { error = "No readable text found in this file." });

            if (content.Trim().Length < 150)
                return BadRequest(new { error = "This file has almost no selectable text (it looks like a scanned/image-only PDF). Upload screenshots of the pages as .png/.jpg images instead, or use a PDF with selectable text." });

            var paper = new QuestionPaper
            {
                Title = Path.GetFileNameWithoutExtension(file.FileName),
                UserId = GetUserId(),
                Status = "parsing",
                Exam = string.IsNullOrWhiteSpace(exam) ? null : exam.Trim(),
                Year = year,
                CreatedAt = DateTime.UtcNow
            };
            _db.QuestionPapers.Add(paper);
            await _db.SaveChangesAsync();

            var paperId = paper.Id;
            _ = Task.Run(() => ProcessPaperAsync(paperId, content, paper.Title));

            return Ok(new { paper.Id, paper.Title, paper.Status });
        }

        private async Task ProcessPaperAsync(int paperId, string content, string title)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StudyBuddyContext>();
            var ai = scope.ServiceProvider.GetRequiredService<IMistralService>();

            try
            {
                _logger.LogInformation("Processing paper {PaperId} '{Title}'. Extracted text length: {Len}. Preview: {Preview}",
                    paperId, title, content.Length,
                    content.Length > 300 ? content.Substring(0, 300).Replace("\n", " ") : content.Replace("\n", " "));

                var chunks = SplitIntoChunks(content, 12);

                var paper = await db.QuestionPapers.FindAsync(paperId);
                if (paper == null) return;
                paper.ChunksTotal = chunks.Count;
                await db.SaveChangesAsync();

                _logger.LogInformation("Paper {PaperId}: split into {ChunkCount} chunks.", paperId, chunks.Count);

                var all = new List<PaperQuestion>();
                var failures = new List<string>();
                for (var i = 0; i < chunks.Count; i++)
                {
                    var chunkQuestions = 0;
                    var raw = "";
                    for (var attempt = 0; attempt < 3; attempt++)
                    {
                        var prompt = BuildExtractionPrompt(title, chunks[i]);
                        raw = await ai.SendRawPrompt(prompt);
                        var json = ExtractJson(raw);
                        chunkQuestions = 0;
                        if (json != null)
                        {
                            try
                            {
                                var qs = JsonSerializer.Deserialize<List<PaperQuestionDto>>(json, JsonOptions);
                                if (qs != null)
                                {
                                    foreach (var q in qs)
                                    {
                                        if (string.IsNullOrWhiteSpace(q.question)) continue;
                                        var type = QuestionMapper.NormalizeType(q.type);
                                        if (!IsValidPaperQuestion(q, type)) continue;
                                        chunkQuestions++;
                                        all.Add(new PaperQuestion
                                        {
                                            QuestionNumber = q.question_number ?? 0,
                                            Section = q.section ?? "",
                                            Type = type,
                                            QuestionText = q.question.Trim(),
                                            OptionsJson = JsonSerializer.Serialize(q.options ?? new List<string>()),
                                            CorrectAnswer = q.correct_answer ?? "",
                                            CorrectAnswersJson = QuestionMapper.AnswersJson(q.correct_answers, type),
                                            Assertion = q.assertion,
                                            Reason = q.reason,
                                            RightOptionsJson = QuestionMapper.RightOptionsJson(q.right_options, type),
                                            Passage = q.passage,
                                            Explanation = q.explanation ?? ""
                                        });
                                    }
                                }
                            }
                            catch { }
                        }

                        if (chunkQuestions > 0) break;
                        if (attempt < 2) await Task.Delay(3000);
                    }

                    if (chunkQuestions == 0)
                    {
                        var snippet = string.IsNullOrWhiteSpace(raw) ? "empty AI response"
                            : raw.StartsWith("Error:") ? raw
                            : raw.Length > 250 ? raw.Substring(0, 250) : raw;
                        failures.Add($"chunk {i + 1}: {snippet}");
                        _logger.LogWarning("Paper {PaperId} chunk {Chunk}/{Total} yielded no questions. {Snippet}",
                            paperId, i + 1, chunks.Count, snippet);
                    }
                    else
                    {
                        _logger.LogInformation("Paper {PaperId} chunk {Chunk}/{Total}: {Count} questions.", paperId, i + 1, chunks.Count, chunkQuestions);
                    }

                    paper = await db.QuestionPapers.FindAsync(paperId);
                    if (paper == null) return;
                    paper.ChunksDone = i + 1;
                    await db.SaveChangesAsync();
                }

                var grouped = all
                    .GroupBy(q => q.QuestionNumber)
                    .Select(g => g.First())
                    .ToList();

                foreach (var q in grouped)
                {
                    q.PaperId = paperId;
                    db.PaperQuestions.Add(q);
                }

                paper = await db.QuestionPapers.FindAsync(paperId);
                if (paper == null) return;
                paper.TotalQuestions = grouped.Count;
                paper.Status = grouped.Count > 0 ? "ready" : "failed";
                if (failures.Count > 0)
                {
                    paper.ErrorMessage = string.Join(" | ", failures.Take(3));
                    _logger.LogWarning("Paper {PaperId} finished with {Failures}/{Chunks} chunks failing. Details: {Details}",
                        paperId, failures.Count, chunks.Count, paper.ErrorMessage);
                }
                if (grouped.Count == 0)
                {
                    paper.Status = "failed";
                    if (string.IsNullOrWhiteSpace(paper.ErrorMessage))
                        paper.ErrorMessage = "No questions could be extracted from this paper.";
                    paper.ErrorMessage += " If this PDF is a scanned image (no selectable text), upload a screenshot (.png/.jpg) of the pages instead.";
                }
                await db.SaveChangesAsync();

                db.StudyActivities.Add(new StudyActivity
                {
                    Type = "paper",
                    UserId = paper.UserId,
                    Timestamp = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Paper {PaperId} processing failed.", paperId);
                var paper = await db.QuestionPapers.FindAsync(paperId);
                if (paper != null)
                {
                    paper.Status = "failed";
                    paper.ErrorMessage = ex.Message;
                    await db.SaveChangesAsync();
                }
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var userId = GetUserId();
            var query = _db.QuestionPapers.AsQueryable();
            if (userId.HasValue)
                query = query.Where(p => p.UserId == userId.Value);

            var papers = await query
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.Status,
                    p.IsPublic,
                    p.Exam,
                    p.Year,
                    p.TotalQuestions,
                    p.ChunksTotal,
                    p.ChunksDone,
                    p.ErrorMessage,
                    p.CreatedAt
                })
                .ToListAsync();
            return Ok(papers);
        }

        [HttpGet("browse")]
        public async Task<IActionResult> Browse([FromQuery] string? exam = null, [FromQuery] int? year = null)
        {
            var query = _db.QuestionPapers
                .Where(p => p.IsPublic && p.Status == "ready")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(exam))
                query = query.Where(p => p.Exam != null && p.Exam.ToLower() == exam.Trim().ToLower());
            if (year.HasValue)
                query = query.Where(p => p.Year == year.Value);

            var papers = await query
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.Exam,
                    p.Year,
                    p.TotalQuestions,
                    p.CreatedAt
                })
                .ToListAsync();
            return Ok(papers);
        }

        [HttpGet("browse/filters")]
        public async Task<IActionResult> BrowseFilters()
        {
            var papers = await _db.QuestionPapers.Where(p => p.IsPublic && p.Status == "ready").ToListAsync();
            var exams = papers
                .Where(p => !string.IsNullOrWhiteSpace(p.Exam))
                .Select(p => p.Exam!)
                .Distinct()
                .OrderBy(e => e)
                .ToList();
            var years = papers
                .Where(p => p.Year.HasValue)
                .Select(p => p.Year!.Value)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();
            return Ok(new { exams, years });
        }

        [HttpPut("{id}/visibility")]
        public async Task<IActionResult> SetVisibility(int id, [FromBody] VisibilityRequest request)
        {
            var paper = await _db.QuestionPapers.FindAsync(id);
            if (paper == null) return NotFound();
            var userId = GetUserId();
            if (userId.HasValue && paper.UserId != userId.Value) return Forbid();

            paper.IsPublic = request.IsPublic;
            await _db.SaveChangesAsync();
            return Ok(new { paper.Id, paper.IsPublic });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var paper = await _db.QuestionPapers.FindAsync(id);
            if (paper == null) return NotFound();
            var userId = GetUserId();
            if (userId.HasValue && paper.UserId != userId.Value && !paper.IsPublic) return Forbid();
            return Ok(new
            {
                paper.Id,
                paper.Title,
                paper.Status,
                paper.IsPublic,
                paper.Exam,
                paper.Year,
                paper.TotalQuestions,
                paper.ChunksTotal,
                paper.ChunksDone,
                paper.ErrorMessage,
                paper.CreatedAt
            });
        }

        [HttpGet("{id}/quiz")]
        public async Task<IActionResult> GetQuiz(int id, [FromQuery] int count = 0)
        {
            var paper = await _db.QuestionPapers
                .Include(p => p.Questions)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (paper == null) return NotFound();

            var userId = GetUserId();
            if (userId.HasValue && paper.UserId != userId.Value && !paper.IsPublic) return Forbid();

            if (paper.Status != "ready")
                return BadRequest(new { message = "This paper is still being processed.", status = paper.Status });

            var ordered = paper.Questions
                .OrderBy(q => q.QuestionNumber)
                .ThenBy(q => q.Id)
                .ToList();

            IEnumerable<PaperQuestion> selected;
            var mode = "full";
            if (count > 0 && count < ordered.Count)
            {
                selected = ordered.OrderBy(_ => Guid.NewGuid()).Take(count);
                mode = "selection";
            }
            else
            {
                selected = ordered;
            }

            return Ok(new
            {
                paperId = id,
                paperTitle = paper.Title,
                totalQuestions = ordered.Count,
                mode,
                questions = selected.Select(MapPaperQuestion)
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var paper = await _db.QuestionPapers.FindAsync(id);
            if (paper == null) return NotFound();
            var userId = GetUserId();
            if (userId.HasValue && paper.UserId != userId.Value) return Forbid();
            _db.QuestionPapers.Remove(paper);
            await _db.SaveChangesAsync();
            return Ok();
        }

        private static object MapPaperQuestion(PaperQuestion q)
        {
            List<string>? options = null;
            try { options = JsonSerializer.Deserialize<List<string>>(q.OptionsJson); } catch { }
            List<string>? correctAnswers = null;
            try { correctAnswers = JsonSerializer.Deserialize<List<string>>(q.CorrectAnswersJson); } catch { }
            List<string>? rightOptions = null;
            try { rightOptions = JsonSerializer.Deserialize<List<string>>(q.RightOptionsJson); } catch { }
            return new
            {
                q.Id,
                type = QuestionMapper.NormalizeType(q.Type),
                number = q.QuestionNumber,
                section = q.Section,
                questionText = q.QuestionText,
                options = options ?? new List<string>(),
                answer = q.CorrectAnswer,
                correctAnswers = correctAnswers ?? new List<string>(),
                assertion = q.Assertion,
                reason = q.Reason,
                rightOptions = rightOptions ?? new List<string>(),
                passage = q.Passage,
                explanation = q.Explanation
            };
        }

        private static string BuildExtractionPrompt(string title, string chunk)
        {
            return $@"
You are an expert exam-parsing assistant. Convert the raw text of a previous-year exam question paper into structured questions.

PAPER TITLE: {title}

PAPER TEXT (one section of the paper):
{chunk}

For EVERY numbered question found, return one JSON object with exactly these fields:
- ""question_number"": the original question number (integer)
- ""section"": the section/subject it belongs to (e.g. ""Physics"", ""Chemistry"", ""Mathematics"", ""Part A"") if identifiable, otherwise """"
- ""type"": ""mcq"" (single correct, 4 options), ""multi"" (one or more correct, 4 options), ""numerical"" (integer/numerical answer, no options), ""assertion"" (Assertion-Reason), ""passage"" (comprehension based on a paragraph, with the paragraph in the ""passage"" field), ""matching"" (match List-I to List-II), or ""truefalse"" (mark True/False)
- ""question"": the full question text (keep maths in \(...\) or \[...\] LaTeX)
- ""options"": array of exactly 4 answer choices (a, b, c, d); empty array for numerical
- ""correct_answer"": the exact correct option text (mcq/truefalse), comma-separated correct option texts (multi), the numeric value (numerical), the letter A/B/C/D (assertion), or the mapping like ""A-2, B-3, C-1, D-4"" (matching)
- ""correct_answers"": an array of exact correct option texts; ONLY for multi questions
- ""assertion"": the assertion statement; ONLY for assertion questions
- ""reason"": the reason statement; ONLY for assertion questions
- ""passage"": the full comprehension paragraph; ONLY for passage/comprehension questions
- ""right_options"": the List-II items; ONLY for matching questions
- ""explanation"": a brief 1-2 sentence solution or justification

Rules:
- If the paper text contains an answer key, use it. Otherwise determine the correct answer yourself.
- Preserve the ORIGINAL question type. If a question is integer/numerical-answer type, keep it as ""numerical"" with no options; do NOT convert it into an MCQ.
- For assertion questions, ""options"" must be exactly: [""Both A and R are true and R is the correct explanation of A"", ""Both A and R are true but R is NOT the correct explanation of A"", ""A is true but R is false"", ""A is false but R is true""] and ""correct_answer"" is the letter A, B, C or D.
- Do NOT invent questions that are not present.
- Return ONLY a valid JSON array (no markdown, no code fences, no comments). If the text contains no complete questions, return [].
";
        }

        private static List<string> SplitIntoChunks(string text, int maxQuestions)
        {
            var chunks = new List<string>();
            var buf = new StringBuilder();
            var qCount = 0;

            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;

                var isQuestionStart = Regex.IsMatch(line,
                    @"^(?:(?:Q|Question)\s*[\.\:\-]?\s*)?\d{1,3}\s*[\.\)]");

                if (buf.Length > 0 && isQuestionStart && (qCount >= maxQuestions || buf.Length + line.Length + 1 > 6000))
                {
                    chunks.Add(buf.ToString());
                    buf.Clear();
                    qCount = 0;
                }
                else if (buf.Length > 6000)
                {
                    chunks.Add(buf.ToString());
                    buf.Clear();
                    qCount = 0;
                }

                if (isQuestionStart) qCount++;
                buf.Append(line).Append('\n');
            }

            if (buf.Length > 0) chunks.Add(buf.ToString());

            var result = new List<string>();
            foreach (var chunk in chunks)
            {
                if (chunk.Length <= 6000)
                {
                    result.Add(chunk.Trim());
                    continue;
                }
                for (var i = 0; i < chunk.Length; i += 6000)
                {
                    result.Add(chunk.Substring(i, Math.Min(6000, chunk.Length - i)).Trim());
                }
            }

            return result.Where(c => c.Length > 0).ToList();
        }

        private static string? ExtractJson(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (raw.StartsWith("Error:")) return null;

            int start = raw.IndexOf('{');
            int arrayStart = raw.IndexOf('[');
            if (arrayStart >= 0 && (start < 0 || arrayStart < start))
                start = arrayStart;

            if (start < 0) return null;

            var end = start == arrayStart
                ? raw.LastIndexOf(']')
                : raw.LastIndexOf('}');

            if (end < 0 || end <= start) return null;

            var json = raw.Substring(start, end - start + 1);
            json = StripCodeFences(json);

            if (TryParseJson(json)) return json;

            var sanitized = SanitizeJsonContent(json);
            if (TryParseJson(sanitized)) return sanitized;

            return null;
        }

        private static string SanitizeJsonContent(string json)
        {
            var sb = new StringBuilder(json.Length + 16);
            bool inString = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (!inString)
                {
                    if (c == '"') inString = true;
                    sb.Append(c);
                    continue;
                }

                switch (c)
                {
                    case '"':
                        int q = i + 1;
                        while (q < json.Length && char.IsWhiteSpace(json[q])) q++;
                        if (q >= json.Length || json[q] == ',' || json[q] == '}' || json[q] == ']' || json[q] == ':')
                        {
                            inString = false;
                            sb.Append('"');
                        }
                        else
                        {
                            sb.Append("\\\"");
                        }
                        break;
                    case '\\':
                        if (i + 1 < json.Length && json[i + 1] == '\\')
                        {
                            sb.Append("\\\\");
                            i++;
                        }
                        else
                        {
                            sb.Append("\\\\");
                        }
                        break;
                    case '\r':
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    default:
                        if (c < 0x20)
                            sb.Append($"\\u{(int)c:x4}");
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private static bool TryParseJson(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.ValueKind is JsonValueKind.Array or JsonValueKind.Object;
            }
            catch
            {
                return false;
            }
        }

        private static string StripCodeFences(string raw)
        {
            return Regex.Replace(raw, @"```(?:[a-zA-Z]*)\s*([\s\S]*?)```", "$1").Trim();
        }

        private static bool IsValidPaperQuestion(PaperQuestionDto q, string type)
        {
            if (q == null || string.IsNullOrWhiteSpace(q.question)) return false;
            if (string.IsNullOrWhiteSpace(q.correct_answer)) return false;

            switch (type)
            {
                case QuestionMapper.TypeMulti:
                    return (q.options?.Count ?? 0) >= 2 && QuestionMapper.CorrectAnswers(new RawQuestion { correct_answers = q.correct_answers, correct_answer = q.correct_answer }, type).Count >= 1;
                case QuestionMapper.TypeMatching:
                    return (q.options?.Count ?? 0) >= 2 && (q.right_options?.Count ?? 0) >= 2;
                case QuestionMapper.TypeAssertion:
                    return !string.IsNullOrWhiteSpace(q.assertion) && !string.IsNullOrWhiteSpace(q.reason);
                case QuestionMapper.TypePassage:
                    return !string.IsNullOrWhiteSpace(q.passage) && (q.options?.Count ?? 0) >= 2;
                case QuestionMapper.TypeNumerical:
                    return System.Text.RegularExpressions.Regex.IsMatch(q.correct_answer.Trim(), @"^[-+]?[0-9]*\.?[0-9]+$");
                default: // mcq / truefalse
                    return (q.options?.Count ?? 0) >= 2;
            }
        }
    }

    public class PaperQuestionDto
    {
        public int? question_number { get; set; }
        public string? section { get; set; }
        public string? type { get; set; }
        public string? question { get; set; }
        public List<string>? options { get; set; }
        public string? correct_answer { get; set; }
        public List<string>? correct_answers { get; set; }
        public string? assertion { get; set; }
        public string? reason { get; set; }
        public List<string>? right_options { get; set; }
        public string? passage { get; set; }
        public string? explanation { get; set; }
    }

    public class VisibilityRequest
    {
        public bool IsPublic { get; set; }
    }
}
