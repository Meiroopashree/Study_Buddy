using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.DTOs;
using StudyBuddy.Interfaces;
using StudyBuddy.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StudyBuddy.Controllers
{
    [ApiController]
    [Route("api/learning")]
    public class LearningController : ControllerBase
    {
        private readonly StudyBuddyContext _db;
        private readonly IMistralService _aiService;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;

        public LearningController(StudyBuddyContext db, IMistralService aiService, IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _db = db;
            _aiService = aiService;
            _config = config;
            _httpClientFactory = httpClientFactory;
        }

        private int? GetUserId()
        {
            var auth = HttpContext.Request.Headers["authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(auth)) return null;
            var user = _db.Users.FirstOrDefault(u => u.Token == auth);
            return user?.Id;
        }

        private async Task<TopicContent?> GetEffectiveTopicContent(int topicId, int? userId)
        {
            if (userId.HasValue)
            {
                var mine = await _db.TopicContents
                    .FirstOrDefaultAsync(c => c.TopicId == topicId && c.UserId == userId);
                if (mine != null) return mine;
            }

            return await _db.TopicContents
                .FirstOrDefaultAsync(c => c.TopicId == topicId && c.UserId == null);
        }

        private async Task<string> GetEffectiveChapterSummary(int chapterId, int? userId)
        {
            if (userId.HasValue)
            {
                var mine = await _db.ChapterContents
                    .FirstOrDefaultAsync(c => c.ChapterId == chapterId && c.UserId == userId);
                if (mine != null) return mine.Summary;
            }

            var global = await _db.ChapterContents
                .FirstOrDefaultAsync(c => c.ChapterId == chapterId && c.UserId == null);
            return global?.Summary ?? "";
        }

        // ==================== Syllabus structure ====================

        [HttpGet("tree")]
        public async Task<IActionResult> GetTree()
        {
            var subjects = await _db.Subjects
                .Include(s => s.Chapters)
                .ThenInclude(c => c.Topics)
                .OrderBy(s => s.Name)
                .ToListAsync();

            var tree = subjects
                .GroupBy(s => string.IsNullOrWhiteSpace(s.Exam) ? "General" : s.Exam)
                .Select(g => new
                {
                    Exam = g.Key,
                    Subjects = g.Select(s => new
                    {
                        s.Id,
                        s.Name,
                        s.Description,
                        Chapters = s.Chapters.OrderBy(c => c.Title).Select(c => new
                        {
                            c.Id,
                            c.Title,
                            c.Summary,
                            Topics = c.Topics.OrderBy(t => t.Title).Select(t => new
                            {
                                t.Id,
                                t.Title,
                                t.Description
                            })
                        })
                    })
                });

            return Ok(tree);
        }

        [HttpPost("subjects")]
        public async Task<IActionResult> AddSubject([FromBody] SubjectRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Subject name is required.");

            var subject = new Subject
            {
                Exam = string.IsNullOrWhiteSpace(request.Exam) ? "General" : request.Exam,
                Name = request.Name.Trim(),
                Description = request.Description ?? ""
            };
            _db.Subjects.Add(subject);
            await _db.SaveChangesAsync();
            return Ok(new { subject.Id, subject.Exam, subject.Name, subject.Description });
        }

        [HttpPost("chapters")]
        public async Task<IActionResult> AddChapter([FromBody] ChapterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest("Chapter title is required.");
            if (request.SubjectId <= 0)
                return BadRequest("SubjectId is required.");

            var chapter = new Chapter
            {
                SubjectId = request.SubjectId,
                Title = request.Title.Trim()
            };
            _db.Chapters.Add(chapter);
            await _db.SaveChangesAsync();
            return Ok(new { chapter.Id, chapter.SubjectId, chapter.Title });
        }

        [HttpPost("topics")]
        public async Task<IActionResult> AddTopic([FromBody] TopicRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest("Topic title is required.");
            if (request.ChapterId <= 0)
                return BadRequest("ChapterId is required.");

            var topic = new Topic
            {
                ChapterId = request.ChapterId,
                Title = request.Title.Trim(),
                Description = request.Description ?? "",
                UpdatedAt = DateTime.UtcNow
            };
            _db.Topics.Add(topic);
            await _db.SaveChangesAsync();
            return Ok(new { topic.Id, topic.ChapterId, topic.Title, topic.Description });
        }

        // ==================== AI generation ====================

        [HttpGet("topics/{id}")]
        public async Task<IActionResult> GetTopic(int id)
        {
            var topic = await _db.Topics.Include(t => t.Chapter).FirstOrDefaultAsync(t => t.Id == id);
            if (topic == null) return NotFound();

            var userId = GetUserId();
            var content = await GetEffectiveTopicContent(id, userId);

            var summary = await GetEffectiveChapterSummary(topic.ChapterId, userId);
            if (string.IsNullOrEmpty(summary))
                summary = topic.Chapter.Summary;

            return Ok(new
            {
                topic.Id,
                topic.Title,
                topic.Description,
                LessonContent = !string.IsNullOrWhiteSpace(content?.LessonContent) ? content.LessonContent : topic.LessonContent,
                NotesContent = !string.IsNullOrWhiteSpace(content?.NotesContent) ? content.NotesContent : topic.NotesContent,
                RevisionContent = !string.IsNullOrWhiteSpace(content?.RevisionContent) ? content.RevisionContent : topic.RevisionContent,
                FormulaSheet = !string.IsNullOrWhiteSpace(content?.FormulaSheet) ? content.FormulaSheet : topic.FormulaSheet,
                ConceptMap = !string.IsNullOrWhiteSpace(content?.ConceptMap) ? content.ConceptMap : topic.ConceptMap,
                VideoId = topic.VideoId,
                UpdatedAt = content?.UpdatedAt ?? topic.UpdatedAt,
                Chapter = new { topic.Chapter.Id, topic.Chapter.Title, Summary = summary }
            });
        }

        [HttpGet("topics/{id}/video")]
        public async Task<IActionResult> GetTopicVideo(int id)
        {
            var topic = await _db.Topics
                .Include(t => t.Chapter)
                .ThenInclude(c => c.Subject)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (topic == null) return NotFound();

            string searchQuery = $"{topic.Title} {topic.Chapter?.Subject?.Name}".Trim();
            string searchUrl = "https://www.youtube.com/results?search_query=" +
                Uri.EscapeDataString(searchQuery);

            if (!string.IsNullOrWhiteSpace(topic.VideoId))
                return Ok(new { topic.Title, videoId = topic.VideoId, embedUrl = VideoEmbedUrl(topic.VideoId), searchUrl, source = "cached" });

            var apiKey = _config["YouTube:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                var videoId = await SearchYouTubeVideoAsync(apiKey, searchQuery);
                if (!string.IsNullOrWhiteSpace(videoId))
                {
                    topic.VideoId = videoId;
                    topic.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                    return Ok(new { topic.Title, videoId, embedUrl = VideoEmbedUrl(videoId), searchUrl, source = "youtube" });
                }
            }

            return Ok(new { topic.Title, videoId = (string?)null, embedUrl = (string?)null, searchUrl, hasKey = !string.IsNullOrWhiteSpace(apiKey) });
        }

        private static string VideoEmbedUrl(string videoId) => $"https://www.youtube-nocookie.com/embed/{videoId}";

        private async Task<string?> SearchYouTubeVideoAsync(string apiKey, string query)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = "https://www.googleapis.com/youtube/v3/search"
                    + $"?part=snippet&type=video&maxResults=1&safeSearch=none"
                    + $"&q={Uri.EscapeDataString(query)}&key={Uri.EscapeDataString(apiKey)}";
                using var res = await client.GetAsync(url);
                if (!res.IsSuccessStatusCode) return null;

                using var json = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
                if (json.RootElement.TryGetProperty("items", out var items) && items.GetArrayLength() > 0
                    && items[0].TryGetProperty("id", out var idEl)
                    && idEl.TryGetProperty("videoId", out var videoIdEl))
                {
                    return videoIdEl.GetString();
                }
            }
            catch { /* fall through to search link */ }
            return null;
        }

        [HttpPost("topics/{id}/generate-content")]
        public async Task<IActionResult> GenerateTopicContent(int id)
        {
            var topic = await _db.Topics
                .Include(t => t.Chapter)
                .ThenInclude(c => c.Subject)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (topic == null) return NotFound();

            var userId = GetUserId();
            if (userId == null)
                return Unauthorized(new { message = "Please log in to generate topic content." });

            var exam = topic.Chapter?.Subject?.Exam;
            string examContext = ExamGuidance.ExamContextInstruction(exam);

            var sections = new List<(string Key, string SectionName, string Instructions)>
            {
                ("lesson", "lesson", @"A DETAILED lesson in markdown starting with a ## heading. It must be comprehensive (at least 700 words) and include ALL of the following when relevant:
- an overview that defines the topic in clear, simple terms
- every core concept and principle explained step by step
- all key definitions, laws, properties and relationships
- at least two fully worked example problems with numbered solution steps and final answers
- common student misunderstandings and how to avoid them
- exam-focused tips: how questions on this are usually asked, quick-solution tricks, and what to look out for in MCQ traps"),
                ("notes", "notes", @"DETAILED bullet-point study notes in markdown (start with a ## heading). Cover every concept from the topic, list each key formula alongside a one-line explanation, and include unit/convention reminders. Use 20-30+ detailed bullets."),
                ("revision", "revision sheet", @"A quick revision sheet in markdown (start with a ## heading): the 8-12 most important points, every formula that can be directly asked, a ""Common mistakes"" sub-section, and a list of 5 self-test questions (questions only, no answers)."),
                ("formula_sheet", "formula sheet", @"ALL formulas, equations and definitions required for the topic in markdown (start with a ## heading). Give each formula a name, present it in \( \) LaTeX, and add one line on when/how to use it."),
                ("concept_map", "concept map", @"An indented tree in markdown (start with a ## heading, use nested '-' bullets) showing how the topic's concepts connect, including branches for prerequisites, subtopics, and derived results.")
            };

            var generated = new Dictionary<string, string>();
            foreach (var s in sections)
            {
                string prompt = BuildTopicContentSectionPrompt(topic, examContext, s.SectionName, s.Instructions);
                string raw = await _aiService.SendRawPrompt(prompt);
                if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("Error:"))
                {
                    raw = await _aiService.SendRawPrompt(prompt + "\n\nIMPORTANT: Return ONLY the " + s.SectionName + " in Markdown. No JSON, no code fences, no preamble. Start directly with the ## heading.");
                }

                var text = StripCodeFences(raw ?? "").Trim();
                if (string.IsNullOrEmpty(text) || text.StartsWith("Error:"))
                    return StatusCode(502, "AI could not generate the " + s.SectionName + ". Please try again.");

                generated[s.Key] = text;
            }

            var content = await _db.TopicContents
                .FirstOrDefaultAsync(c => c.TopicId == id && c.UserId == userId);
            if (content == null)
            {
                content = new TopicContent
                {
                    TopicId = id,
                    UserId = userId
                };
                _db.TopicContents.Add(content);
            }

            content.LessonContent = NormalizeGeneratedNewlines(generated["lesson"]);
            content.NotesContent = NormalizeGeneratedNewlines(generated["notes"]);
            content.RevisionContent = NormalizeGeneratedNewlines(generated["revision"]);
            content.FormulaSheet = NormalizeGeneratedNewlines(generated["formula_sheet"]);
            content.ConceptMap = NormalizeGeneratedNewlines(generated["concept_map"]);
            content.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                topic.Id,
                LessonContent = content.LessonContent,
                NotesContent = content.NotesContent,
                RevisionContent = content.RevisionContent,
                FormulaSheet = content.FormulaSheet,
                ConceptMap = content.ConceptMap,
                UpdatedAt = content.UpdatedAt
            });
        }

        private static string BuildTopicContentSectionPrompt(Topic topic, string examContext, string sectionName, string instructions)
        {
            return $@"
You are an expert teacher helping a student prepare thoroughly for an exam. Create the {sectionName} as part of COMPLETE, exam-ready study material for the topic ""{topic.Title}"".

{examContext}

Your task: {instructions}

MATH FORMATTING (REQUIRED - follow exactly):
- Write ALL mathematics using LaTeX delimiters: \[...\] for display equations and \(...\) for inline math.
- NEVER output bare \begin{{...}} or \end{{...}} outside \[...\] delimiters.
- NEVER use $ or $$ delimiters anywhere.
- Do not put spaces right after the opening delimiter or right before the closing delimiter (write \(x+y\), not \( x+y \)).
- Put every display equation on its own line(s), with a blank line before and after the \[...\] block.
- Inside display math, use \begin{{bmatrix}}, \begin{{matrix}}, \begin{{cases}}, etc. normally. Do NOT wrap a matrix/cases environment in an extra \begin{{aligned}}.
- Use \; for multiplication and \cdot for dot products.

Context:
Chapter: {topic.Chapter?.Title ?? ""}
Topic description: {topic.Description}

Return ONLY the {sectionName} in Markdown. Do NOT wrap it in JSON, code fences, or any commentary. Start directly with the ## heading.
";
        }

        [HttpPost("chapters/{id}/generate-summary")]
        public async Task<IActionResult> GenerateChapterSummary(int id)
        {
            var chapter = await _db.Chapters
                .Include(c => c.Topics)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (chapter == null) return NotFound();

            var userId = GetUserId();
            if (userId == null)
                return Unauthorized(new { message = "Please log in to generate chapter summary." });

            var topicNames = string.Join(", ", chapter.Topics.Select(t => t.Title));
            string prompt = $@"
Write a concise chapter summary for ""{chapter.Title}"". Topics covered: {topicNames}.
Return ONLY the summary text (markdown, start with a ## heading, ~150-200 words). No JSON.
";

            var raw = await _aiService.SendRawPrompt(prompt);
            var summary = StripCodeFences(raw).Trim();

            if (string.IsNullOrWhiteSpace(summary) || summary.StartsWith("Error:"))
                return StatusCode(502, "AI could not generate the summary.");

            var content = await _db.ChapterContents
                .FirstOrDefaultAsync(c => c.ChapterId == id && c.UserId == userId);
            if (content == null)
            {
                content = new ChapterContent
                {
                    ChapterId = id,
                    UserId = userId
                };
                _db.ChapterContents.Add(content);
            }

            content.Summary = summary;
            content.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { chapter.Id, chapter.Title, Summary = content.Summary });
        }

        [HttpPost("generate-syllabus")]
        public async Task<IActionResult> GenerateSyllabus([FromBody] SyllabusRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Exam))
                return BadRequest("Exam name is required.");

            var subjectNames = request.Subjects != null && request.Subjects.Count > 0
                ? string.Join(", ", request.Subjects)
                : "Physics, Chemistry, Mathematics";

            string prompt = $@"
For the exam ""{request.Exam}"", design a study syllabus.
Subjects: {subjectNames}

Return ONLY a valid JSON array (no markdown, no code fences). Each element:
{{
  ""subject"": ""Subject name"",
  ""chapters"": [
    {{
      ""title"": ""Chapter title"",
      ""topics"": [
        {{ ""title"": ""Topic title"", ""description"": ""1-line description"" }}
      ]
    }}
  ]
}}

Include 3-5 chapters per subject and 3-6 topics per chapter. Use escape sequence \"" for any double quotes inside strings.
";

            var raw = await _aiService.SendRawPrompt(prompt);
            var json = ExtractJson(raw);

            if (json == null)
                return StatusCode(502, "AI could not generate the syllabus. Please try again.");

            List<SyllabusSubjectDto>? subjects;
            try
            {
                subjects = JsonSerializer.Deserialize<List<SyllabusSubjectDto>>(json, JsonOptions);
            }
            catch
            {
                return StatusCode(502, "AI returned malformed syllabus. Please try again.");
            }

            if (subjects == null || subjects.Count == 0)
                return StatusCode(502, "AI returned an empty syllabus.");

            foreach (var s in subjects)
            {
                if (string.IsNullOrWhiteSpace(s.Subject)) continue;
                var subject = new Subject { Exam = request.Exam, Name = s.Subject.Trim() };
                _db.Subjects.Add(subject);
                await _db.SaveChangesAsync();

                foreach (var ch in s.Chapters ?? new List<SyllabusChapterDto>())
                {
                    if (string.IsNullOrWhiteSpace(ch.Title)) continue;
                    var chapter = new Chapter { SubjectId = subject.Id, Title = ch.Title.Trim() };
                    _db.Chapters.Add(chapter);
                    await _db.SaveChangesAsync();

                    foreach (var tp in ch.Topics ?? new List<SyllabusTopicDto>())
                    {
                        if (string.IsNullOrWhiteSpace(tp.Title)) continue;
                        _db.Topics.Add(new Topic
                        {
                            ChapterId = chapter.Id,
                            Title = tp.Title.Trim(),
                            Description = tp.Description ?? "",
                            UpdatedAt = DateTime.UtcNow
                        });
                    }
                    await _db.SaveChangesAsync();
                }
            }

            return Ok(new { Count = subjects.Count });
        }

        [HttpPost("seed-syllabus")]
        public async Task<IActionResult> SeedSyllabus([FromBody] SeedSyllabusRequest? request)
        {
            var exam = string.IsNullOrWhiteSpace(request?.Exam) ? "JEE" : request.Exam.Trim();

            var existingSubjects = await _db.Subjects
                .Include(s => s.Chapters)
                .ThenInclude(c => c.Topics)
                .Where(s => s.Exam == exam)
                .ToListAsync();

            foreach (var s in existingSubjects)
            {
                foreach (var c in s.Chapters)
                {
                    _db.Topics.RemoveRange(c.Topics);
                }
                _db.Chapters.RemoveRange(s.Chapters);
                _db.Subjects.Remove(s);
            }
            await _db.SaveChangesAsync();

            var seedDir = Path.Combine(AppContext.BaseDirectory, "SeedData");
            if (!Directory.Exists(seedDir))
                return StatusCode(500, $"SeedData directory not found at {seedDir}");

            var files = Directory.GetFiles(seedDir, "*.json");
            if (files.Length == 0)
                return StatusCode(500, "No seed syllabus files found.");

            var insertedSubjects = 0;
            var insertedChapters = 0;
            var insertedTopics = 0;

            foreach (var file in files)
            {
                List<SyllabusSubjectDto>? subjects = null;
                try
                {
                    var text = await System.IO.File.ReadAllTextAsync(file);
                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        // New-format seed file with its own exam marker.
                        var decoded = JsonSerializer.Deserialize<SeedFileDto>(text, JsonOptions);
                        if (decoded != null && decoded.Subjects != null && decoded.Subjects.Count > 0)
                        {
                            var fileExam = string.IsNullOrWhiteSpace(decoded.Exam) ? exam : decoded.Exam.Trim();
                            if (string.Equals(fileExam, exam, StringComparison.OrdinalIgnoreCase))
                                subjects = decoded.Subjects;
                        }
                    }
                    else if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        // Legacy-format seed file (plain array of subjects).
                        if (string.Equals(exam, "JEE", StringComparison.OrdinalIgnoreCase))
                            subjects = JsonSerializer.Deserialize<List<SyllabusSubjectDto>>(text, JsonOptions);
                    }
                }
                catch
                {
                    return StatusCode(500, $"Could not parse seed file {Path.GetFileName(file)}.");
                }

                if (subjects == null || subjects.Count == 0) continue;

                var subjectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in subjects)
                {
                    if (string.IsNullOrWhiteSpace(s.Subject)) continue;
                    if (!subjectNames.Add(s.Subject.Trim())) continue;
                    var subject = new Subject { Exam = exam, Name = s.Subject.Trim() };
                    _db.Subjects.Add(subject);
                    await _db.SaveChangesAsync();
                    insertedSubjects++;

                    foreach (var ch in s.Chapters ?? new List<SyllabusChapterDto>())
                    {
                        if (string.IsNullOrWhiteSpace(ch.Title)) continue;
                        var chapter = new Chapter { SubjectId = subject.Id, Title = ch.Title.Trim() };
                        _db.Chapters.Add(chapter);
                        await _db.SaveChangesAsync();
                        insertedChapters++;

                        foreach (var tp in ch.Topics ?? new List<SyllabusTopicDto>())
                        {
                            if (string.IsNullOrWhiteSpace(tp.Title)) continue;
                            _db.Topics.Add(new Topic
                            {
                                ChapterId = chapter.Id,
                                Title = tp.Title.Trim(),
                                Description = tp.Description ?? "",
                                UpdatedAt = DateTime.UtcNow
                            });
                            insertedTopics++;
                        }
                        await _db.SaveChangesAsync();
                    }
                }
            }

            return Ok(new { Exam = exam, Subjects = insertedSubjects, Chapters = insertedChapters, Topics = insertedTopics });
        }

        // ==================== User-created exams ====================

        /// <summary>List all user-created exams.</summary>
        [HttpGet("exams")]
        public async Task<IActionResult> GetExams()
        {
            var exams = await _db.Exams.OrderByDescending(x => x.CreatedAt).ToListAsync();
            var result = new List<object>();
            foreach (var x in exams)
            {
                var subjects = await _db.Subjects.Where(s => s.Exam == x.Name).ToListAsync();
                result.Add(new
                {
                    x.Id,
                    x.Name,
                    x.Year,
                    x.Description,
                    subjectCount = subjects.Count
                });
            }
            return Ok(result);
        }

        /// <summary>
        /// Create a new exam and AI-generate its full syllabus (subjects → chapters → topics)
        /// matching the exam's syllabus for the given year.
        /// </summary>
        [HttpPost("exams")]
        public async Task<IActionResult> CreateExam([FromBody] CreateExamRequest request)
        {
            var name = request.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return BadRequest("Exam name is required.");

            var year = request.Year > 0 ? request.Year : DateTime.Now.Year;

            // Remove any prior version of this exam's subjects so regeneration stays clean.
            var staleSubjects = await _db.Subjects.Include(s => s.Chapters).ThenInclude(c => c.Topics)
                .Where(s => s.Exam == name).ToListAsync();
            foreach (var s in staleSubjects)
            {
                foreach (var c in s.Chapters)
                    _db.Topics.RemoveRange(c.Topics);
                _db.Chapters.RemoveRange(s.Chapters);
                _db.Subjects.Remove(s);
            }
            await _db.SaveChangesAsync();

            var subjectNames = request.Subjects != null && request.Subjects.Count > 0
                ? string.Join(", ", request.Subjects)
                : "Physics, Chemistry, Mathematics";

            string prompt = $@"
You are a syllabus expert. Create the complete, exam-accurate study syllabus for the admission exam ""{name}"" for the year {year}.
Base the syllabus on the officially published syllabus for {name} {year}.

Subjects: {subjectNames}

Return ONLY a valid JSON array (no markdown, no code fences). Each element:
{{
  ""subject"": ""Subject name"",
  ""chapters"": [
    {{
      ""title"": ""Chapter title"",
      ""topics"": [
        {{ ""title"": ""Topic title"", ""description"": ""1-line description"" }}
      ]
    }}
  ]
}}

Rule: Include every chapter and topic that appears in the official {name} syllabus for {year}, organised by subject under the school/board chapters used by the exam. Cover the full breadth (do not skip topics). Aim for 6-25 chapters per subject and 3-8 topics per chapter. Use escape sequence \"" for any double quotes inside strings.
";

            var raw = await _aiService.SendRawPrompt(prompt);
            var json = ExtractJson(raw);
            if (json == null)
                return StatusCode(502, "AI could not generate the syllabus. Please try again.");

            List<SyllabusSubjectDto>? subjects;
            try
            {
                subjects = JsonSerializer.Deserialize<List<SyllabusSubjectDto>>(json, JsonOptions);
            }
            catch
            {
                return StatusCode(502, "AI returned malformed syllabus. Please try again.");
            }

            if (subjects == null || subjects.Count == 0)
                return StatusCode(502, "AI returned an empty syllabus.");

            // Register the exam.
            var examEntity = new Exam { Name = name, Year = year, Description = request.Description ?? "" };
            _db.Exams.Add(examEntity);
            await _db.SaveChangesAsync();

            int chapters = 0, topics = 0;
            foreach (var s in subjects)
            {
                if (string.IsNullOrWhiteSpace(s.Subject)) continue;
                var subject = new Subject { Exam = name, Name = s.Subject.Trim() };
                _db.Subjects.Add(subject);
                await _db.SaveChangesAsync();
                foreach (var ch in s.Chapters ?? new List<SyllabusChapterDto>())
                {
                    if (string.IsNullOrWhiteSpace(ch.Title)) continue;
                    var chapter = new Chapter { SubjectId = subject.Id, Title = ch.Title.Trim() };
                    _db.Chapters.Add(chapter);
                    await _db.SaveChangesAsync();
                    chapters++;
                    foreach (var tp in ch.Topics ?? new List<SyllabusTopicDto>())
                    {
                        if (string.IsNullOrWhiteSpace(tp.Title)) continue;
                        _db.Topics.Add(new Topic { ChapterId = chapter.Id, Title = tp.Title.Trim(), Description = tp.Description ?? "", UpdatedAt = DateTime.UtcNow });
                        topics++;
                    }
                    await _db.SaveChangesAsync();
                }
            }

            return Ok(new
            {
                exam = new { examEntity.Id, examEntity.Name, examEntity.Year, examEntity.Description },
                subjects = subjects.Count,
                chapters,
                topics
            });
        }

        /// <summary>Delete an exam and all of its syllabus subjects/chapters/topics.</summary>
        [HttpDelete("exams/{id}")]
        public async Task<IActionResult> DeleteExam(int id)
        {
            var examEntity = await _db.Exams.FirstOrDefaultAsync(x => x.Id == id);
            if (examEntity == null) return NotFound();

            var subjects = await _db.Subjects.Include(s => s.Chapters).ThenInclude(c => c.Topics)
                .Where(s => s.Exam == examEntity.Name).ToListAsync();
            foreach (var s in subjects)
            {
                foreach (var c in s.Chapters)
                    _db.Topics.RemoveRange(c.Topics);
                _db.Chapters.RemoveRange(s.Chapters);
                _db.Subjects.Remove(s);
            }
            await _db.SaveChangesAsync();

            _db.Exams.Remove(examEntity);
            await _db.SaveChangesAsync();

            return Ok(new { deleted = true, name = examEntity.Name });
        }

        /// <summary>
        /// Delete an exam by name and all of its syllabus subjects/chapters/topics.
        /// Works for both seed-provided exams (JEE Main/Advanced) and user-created exams.
        /// </summary>
        [HttpDelete("exams/by-name/{name}")]
        public async Task<IActionResult> DeleteExamByName(string name)
        {
            var examName = name?.Trim();
            if (string.IsNullOrWhiteSpace(examName)) return BadRequest("Exam name is required.");

            var subjects = await _db.Subjects.Include(s => s.Chapters).ThenInclude(c => c.Topics)
                .Where(s => s.Exam == examName).ToListAsync();
            foreach (var s in subjects)
            {
                foreach (var c in s.Chapters)
                    _db.Topics.RemoveRange(c.Topics);
                _db.Chapters.RemoveRange(s.Chapters);
                _db.Subjects.Remove(s);
            }

            var examEntity = await _db.Exams.FirstOrDefaultAsync(x => x.Name == examName);
            if (examEntity != null)
                _db.Exams.Remove(examEntity);

            await _db.SaveChangesAsync();

            return Ok(new { deleted = true, name = examName });
        }

        // ==================== Topic progress (mastery) ====================

        private const string ProgressNotStarted = "not_started";
        private const string ProgressInProgress = "in_progress";
        private const string ProgressMastered = "mastered";
        private const string ProgressRevising = "revising";

        private static readonly string[] ProgressStatuses = { ProgressNotStarted, ProgressInProgress, ProgressMastered, ProgressRevising };

        /// <summary>Return the current user's mastery status for every topic.</summary>
        [HttpGet("progress")]
        public async Task<IActionResult> GetProgress()
        {
            var userId = GetUserId();
            var rows = await _db.TopicProgresses
                .Where(p => p.UserId == userId)
                .ToListAsync();
            var map = rows.ToDictionary(p => p.TopicId, p => p.Status);
            return Ok(map);
        }

        /// <summary>Set the mastery status for a topic (not_started/in_progress/mastered/revising).</summary>
        [HttpPut("progress/{topicId:int}")]
        public async Task<IActionResult> SetProgress(int topicId, [FromBody] SetProgressRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Status))
                return BadRequest("Status is required.");
            var status = request.Status.Trim().ToLowerInvariant();
            if (!ProgressStatuses.Contains(status))
                return BadRequest($"Status must be one of: {string.Join(", ", ProgressStatuses)}.");

            var topic = await _db.Topics.FindAsync(topicId);
            if (topic == null) return NotFound("Topic not found.");

            var userId = GetUserId();
            var progress = await _db.TopicProgresses
                .FirstOrDefaultAsync(p => p.TopicId == topicId && p.UserId == userId);
            if (progress == null)
            {
                progress = new TopicProgress { TopicId = topicId, UserId = userId, Status = status };
                _db.TopicProgresses.Add(progress);
            }
            else
            {
                progress.Status = status;
            }
            await _db.SaveChangesAsync();

            return Ok(new { status = progress.Status, updatedAt = progress.UpdatedAt });
        }

        /// <summary>
        /// Syllabus tree enriched with per-topic progress so the UI can show
        /// status dots and progress bars without a second round-trip.
        /// </summary>
        [HttpGet("tree/progress")]
        public async Task<IActionResult> GetTreeWithProgress()
        {
            var userId = GetUserId();
            var subjects = await _db.Subjects
                .Include(s => s.Chapters)
                .ThenInclude(c => c.Topics)
                .OrderBy(s => s.Name)
                .ToListAsync();

            var progressMap = await _db.TopicProgresses
                .Where(p => p.UserId == userId)
                .ToDictionaryAsync(p => p.TopicId, p => p.Status);

            var tree = subjects
                .GroupBy(s => string.IsNullOrWhiteSpace(s.Exam) ? "General" : s.Exam)
                .Select(g => new
                {
                    exam = g.Key,
                    subjects = g.Select(s => new
                    {
                        s.Id,
                        s.Name,
                        chapters = s.Chapters.Select(c => new
                        {
                            c.Id,
                            c.Title,
                            topics = c.Topics.Select(t => new
                            {
                                t.Id,
                                t.Title,
                                status = progressMap.TryGetValue(t.Id, out var st) ? st : ProgressNotStarted
                            })
                        })
                    })
                });

            return Ok(tree);
        }

        // ==================== Review schedule (spaced repetition) ====================

        /// <summary>Get all topics due for review for the current user.</summary>
        [HttpGet("review/due")]
        public async Task<IActionResult> GetDueReviews()
        {
            var userId = GetUserId();
            var now = DateTime.UtcNow;
            var schedules = await _db.ReviewSchedules
                .Include(r => r.Topic)
                .ThenInclude(t => t.Chapter)
                .ThenInclude(c => c.Subject)
                .Where(r => r.UserId == userId && r.DueDate <= now)
                .OrderBy(r => r.DueDate)
                .ToListAsync();

            var result = schedules.Select(r => new
            {
                r.Id,
                r.TopicId,
                TopicTitle = r.Topic.Title,
                ChapterTitle = r.Topic.Chapter.Title,
                SubjectName = r.Topic.Chapter.Subject.Name,
                Exam = r.Topic.Chapter.Subject.Exam,
                r.DueDate,
                r.IntervalDays,
                r.ReviewCount,
                r.LastReviewDate,
                DaysOverdue = (now - r.DueDate).Days
            });

            return Ok(result);
        }

        /// <summary>Get the full review schedule for the current user.</summary>
        [HttpGet("review/schedule")]
        public async Task<IActionResult> GetReviewSchedule()
        {
            var userId = GetUserId();
            var schedules = await _db.ReviewSchedules
                .Include(r => r.Topic)
                .ThenInclude(t => t.Chapter)
                .ThenInclude(c => c.Subject)
                .Where(r => r.UserId == userId)
                .OrderBy(r => r.DueDate)
                .ToListAsync();

            var result = schedules.Select(r => new
            {
                r.Id,
                r.TopicId,
                TopicTitle = r.Topic.Title,
                ChapterTitle = r.Topic.Chapter.Title,
                Exam = r.Topic.Chapter.Subject.Exam,
                r.DueDate,
                r.IntervalDays,
                r.EaseFactor,
                r.ReviewCount,
                r.LastReviewDate,
                r.CreatedAt,
                IsDue = r.DueDate <= DateTime.UtcNow
            });

            return Ok(result);
        }

        /// <summary>Add a topic to the review schedule.</summary>
        [HttpPost("review/{topicId:int}")]
        public async Task<IActionResult> AddToReviewSchedule(int topicId)
        {
            var topic = await _db.Topics.FindAsync(topicId);
            if (topic == null) return NotFound("Topic not found.");

            var userId = GetUserId();
            var existing = await _db.ReviewSchedules
                .FirstOrDefaultAsync(r => r.TopicId == topicId && r.UserId == userId);
            if (existing != null)
                return Ok(new { id = existing.Id, message = "Already in review schedule" });

            var schedule = new ReviewSchedule
            {
                TopicId = topicId,
                UserId = userId,
                DueDate = DateTime.UtcNow.AddDays(1),
                IntervalDays = 1,
                EaseFactor = 2.5,
                ReviewCount = 0,
                CreatedAt = DateTime.UtcNow
            };
            _db.ReviewSchedules.Add(schedule);
            await _db.SaveChangesAsync();

            return Ok(new { id = schedule.Id, dueDate = schedule.DueDate });
        }

        /// <summary>
        /// Mark a topic as reviewed. Uses a simplified SM-2 algorithm
        /// to compute the next review date based on quality (1-5).
        /// quality: 1=blackout, 2=incorrect, 3=correct with difficulty, 4=correct, 5=perfect
        /// </summary>
        [HttpPut("review/{topicId:int}/complete")]
        public async Task<IActionResult> CompleteReview(int topicId, [FromBody] CompleteReviewRequest? request = null)
        {
            var userId = GetUserId();
            var schedule = await _db.ReviewSchedules
                .FirstOrDefaultAsync(r => r.TopicId == topicId && r.UserId == userId);
            if (schedule == null) return NotFound("Topic not in review schedule.");

            var quality = Math.Clamp(request?.Quality ?? 4, 1, 5);

            // SM-2 algorithm (simplified)
            if (quality >= 3)
            {
                if (schedule.ReviewCount == 0)
                    schedule.IntervalDays = 1;
                else if (schedule.ReviewCount == 1)
                    schedule.IntervalDays = 6;
                else
                    schedule.IntervalDays = (int)Math.Round(schedule.IntervalDays * schedule.EaseFactor);

                schedule.EaseFactor = Math.Max(1.3,
                    schedule.EaseFactor + (0.1 - (5 - quality) * (0.08 + (5 - quality) * 0.02)));
            }
            else
            {
                schedule.ReviewCount = 0;
                schedule.IntervalDays = 1;
            }

            schedule.ReviewCount++;
            schedule.LastReviewDate = DateTime.UtcNow;
            schedule.DueDate = DateTime.UtcNow.AddDays(schedule.IntervalDays);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                schedule.DueDate,
                schedule.IntervalDays,
                schedule.EaseFactor,
                schedule.ReviewCount,
                nextReview = schedule.DueDate.ToString("yyyy-MM-dd HH:mm")
            });
        }

        /// <summary>Remove a topic from the review schedule.</summary>
        [HttpDelete("review/{topicId:int}")]
        public async Task<IActionResult> RemoveFromReviewSchedule(int topicId)
        {
            var userId = GetUserId();
            var schedule = await _db.ReviewSchedules
                .FirstOrDefaultAsync(r => r.TopicId == topicId && r.UserId == userId);
            if (schedule == null) return NotFound();
            _db.ReviewSchedules.Remove(schedule);
            await _db.SaveChangesAsync();
            return Ok(new { removed = true });
        }

        // ==================== Topic quizzes & flashcards ====================

        [HttpGet("topics/{id}/quiz")]
        public async Task<IActionResult> GetTopicQuiz(int id, [FromQuery] int count = 10, [FromQuery] string difficulty = "all")
        {
            var topic = await _db.Topics
                .Include(t => t.Chapter)
                .ThenInclude(c => c.Subject)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (topic == null) return NotFound();

            var exam = topic.Chapter?.Subject?.Exam;

            count = Math.Clamp(count, 3, 30);
            difficulty = string.IsNullOrWhiteSpace(difficulty) ? "all" : difficulty.ToLowerInvariant();
            var userId = GetUserId();

            var existingQuery = _db.QuizQuestions.Where(q => q.TopicId == id && (q.UserId == userId || q.UserId == null));
            if (difficulty != "all")
                existingQuery = existingQuery.Where(q => q.Difficulty == difficulty);
            var existing = await existingQuery.OrderBy(q => q.Id).ToListAsync();

            if (existing.Count >= count)
            {
                return Ok(new
                {
                    topicId = id,
                    topicTitle = topic.Title,
                    questions = DedupeQuestions(existing).Take(count).Select(MapQuestion)
                });
            }

            var effectiveContent = await GetEffectiveTopicContent(id, userId);
            var lesson = !string.IsNullOrWhiteSpace(effectiveContent?.LessonContent) ? effectiveContent.LessonContent : topic.LessonContent;
            if (lesson.Length > 6000) lesson = lesson.Substring(0, 6000);
            var formula = !string.IsNullOrWhiteSpace(effectiveContent?.FormulaSheet) ? effectiveContent.FormulaSheet : topic.FormulaSheet;
            if (formula.Length > 4000) formula = formula.Substring(0, 4000);

            string prompt = $@"
You are creating a comprehensive quiz so a student can fully master the topic ""{topic.Title}"" ({topic.Description}).

LESSON CONTENT (every distinct concept, formula, definition and problem-solving technique used in the lesson is listed here):
{lesson}

FORMULA SHEET:
{formula}

Steps:
1. Read the LESSON CONTENT and list every distinct concept, formula, definition, and technique it contains.
2. Generate at least {count} questions so that EVERY concept is covered by one or more questions.
3. Generate all questions at medium or hard difficulty only. Never produce easy/direct-recall questions. Every question must require the student to apply at least one concept, do meaningful calculation, work through multiple steps, combine ideas, or reason through a tricky edge case. Medium questions require applying a formula or concept; hard questions require multi-step derivation or a tricky edge case. The questions must be challenging enough that the student has to think and work out the answer.
4. Use fresh numbers/scenarios so no two questions test the exact same thing.
5. Use a mix of question types matching the {exam ?? "selected exam"} pattern:
{ExamGuidance.TypeMixInstruction(exam)}
{ExamGuidance.SectionFormatInstruction(exam, topic.Chapter?.Subject?.Name, topic.Chapter?.Title)}

Rules:
- Every question must be unique. Never duplicate a question, never reuse the same numbers, and never repeat an option pattern.
- Use \(...\) for inline math and \[...\] for display math.
- Return ONLY a valid JSON array (no markdown, no code fences). Each element:
{{
  ""type"": ""one of the allowed types listed above"",
  ""question"": ""question text"",
  ""options"": [""a"", ""b"", ""c"", ""d""] (empty array for numerical),
  ""correct_answer"": ""exact option text for mcq/truefalse; comma-separated correct options for multi; the numeric value for numerical; the letter A/B/C/D for assertion; the mapping like 'A-2, B-3, C-1, D-4' for matching"",
  ""correct_answers"": [""only for multi, the exact correct option texts""],
  ""assertion"": ""only for assertion, the assertion statement"",
  ""reason"": ""only for assertion, the reason statement"",
  ""right_options"": [""only for matching, the List-II items""],
  ""passage"": ""only for passage questions, the comprehension paragraph text"",
  ""explanation"": ""brief 1-2 sentence explanation"",
  ""difficulty"": ""medium"" or ""hard""
}}
- For assertion questions, ""options"" must be exactly: [""Both A and R are true and R is the correct explanation of A"", ""Both A and R are true but R is NOT the correct explanation of A"", ""A is true but R is false"", ""A is false but R is true""] and ""question"" should be ""Assertion and Reason given below.""
- Escape LaTeX backslashes as double backslashes.
";

            var existingTexts = (await _db.QuizQuestions
                .Where(q => q.UserId == userId || q.UserId == null)
                .Select(q => q.QuestionText).ToListAsync())
                .Select(NormalizeQuestionText)
                .ToHashSet();

            var seen = new HashSet<string>(existingTexts);

            const int maxAttempts = 5;
            var added = new List<QuizQuestion>();
            for (int attempt = 0; attempt < maxAttempts && added.Count < count; attempt++)
            {
                var want = count - added.Count;
                var attemptPrompt = $"{prompt}\n\nYou still need to supply {want} more NEW, non-duplicate questions to reach a total of {count}. Generate up to {Math.Min(want * 2, want + 5)} questions. Do not repeat any question already generated or stored.";

                var raw = await _aiService.SendRawPrompt(attemptPrompt);
                var json = ExtractJson(raw);
                List<RawQuestion>? qs = null;
                if (json != null)
                {
                    try
                    {
                        qs = JsonSerializer.Deserialize<List<RawQuestion>>(json, JsonOptions);
                    }
                    catch
                    {
                        qs = null;
                    }
                }

                if (qs == null || qs.Count == 0) continue;

                foreach (var q in qs)
                {
                    if (string.IsNullOrWhiteSpace(q.question)) continue;
                    if (!QuestionMapper.HasValidFormat(q)) continue;
                    if (!ExamGuidance.SectionAllowsType(exam, topic.Chapter?.Subject?.Name, topic.Chapter?.Title, QuestionMapper.NormalizeType(q.type))) continue;
                    if (ExamGuidance.IsQuantitativeComparisonQuestion(q.question)
                        && !ExamGuidance.AllowsQuantitativeComparison(exam, topic.Chapter?.Subject?.Name, topic.Chapter?.Title)) continue;
                    if (!seen.Add(NormalizeQuestionText(q.question))) continue;

                    var saved = new QuizQuestion
                    {
                        TopicId = id,
                        UserId = userId,
                        Type = QuestionMapper.NormalizeType(q.type),
                        QuestionText = q.question.Trim(),
                        OptionsJson = JsonSerializer.Serialize(q.options ?? new List<string>()),
                        CorrectAnswer = q.correct_answer ?? "",
                        CorrectAnswersJson = QuestionMapper.AnswersJson(q, QuestionMapper.NormalizeType(q.type)),
                        Assertion = q.assertion,
                        Reason = q.reason,
                        RightOptionsJson = QuestionMapper.RightOptionsJson(q, QuestionMapper.NormalizeType(q.type)),
                        Passage = q.passage,
                        Explanation = q.explanation ?? "",
                        Difficulty = NormalizeDifficulty(q.difficulty)
                    };
                    _db.QuizQuestions.Add(saved);
                    added.Add(saved);
                    if (added.Count >= count) break;
                }
            }

            if (added.Count > 0)
            {
                await _db.SaveChangesAsync();
                await LogActivity("quiz");
            }

            var final = await _db.QuizQuestions.Where(x => x.TopicId == id && (x.UserId == userId || x.UserId == null)).OrderBy(x => x.Id).ToListAsync();
            var deduped = DedupeQuestions(final).Take(count).ToList();
            if (deduped.Count == 0)
                return StatusCode(502, "AI could not generate the quiz. Please try again.");

            return Ok(new
            {
                topicId = id,
                topicTitle = topic.Title,
                questions = deduped.Select(MapQuestion)
            });
        }

        [HttpGet("topics/{id}/flashcards")]
        public async Task<IActionResult> GetFlashcards(int id, [FromQuery] int count = 100)
        {
            var topic = await _db.Topics.FirstOrDefaultAsync(t => t.Id == id);
            if (topic == null) return NotFound();

            var questions = await _db.QuizQuestions
                .Where(q => q.TopicId == id && (q.UserId == GetUserId() || q.UserId == null))
                .OrderBy(q => q.Id)
                .Take(Math.Clamp(count, 1, 200))
                .ToListAsync();

            var cards = questions.Select(q =>
            {
                List<string>? options = null;
                try { options = JsonSerializer.Deserialize<List<string>>(q.OptionsJson); } catch { }
                var front = q.QuestionText;
                if (!string.IsNullOrWhiteSpace(q.Passage))
                    front = $"**Passage:**\n{q.Passage}\n\n---\n\n" + front;
                if (options != null && options.Count > 0)
                    front += "\n\n" + string.Join("\n", options.Select((o, i) => $"(**{(char)('A' + i)}**) {o}"));
                return new
                {
                    front,
                    back = $"{q.CorrectAnswer}\n\n{q.Explanation}"
                };
            }).ToList();

            return Ok(new { topicId = id, topicTitle = topic.Title, cards });
        }

        // ==================== Chapter-level quiz & review ====================

        [HttpGet("topics/{id}/review")]
        public async Task<IActionResult> GetTopicReview(int id)
        {
            var topic = await _db.Topics.FirstOrDefaultAsync(t => t.Id == id);
            if (topic == null) return NotFound();

            var questions = await _db.QuizQuestions
                .Where(q => q.TopicId == id && (q.UserId == GetUserId() || q.UserId == null))
                .OrderBy(q => q.Id)
                .ToListAsync();

            var topicTitles = new Dictionary<int, string> { [id] = topic.Title };

            return Ok(new
            {
                topicId = id,
                topicTitle = topic.Title,
                questionCount = questions.Count,
                questions = DedupeQuestions(questions).Select(q => MapQuestion(q, topicTitles))
            });
        }

        [HttpGet("chapters/{id}/review")]
        public async Task<IActionResult> GetChapterReview(int id)
        {
            var chapter = await _db.Chapters.Include(c => c.Topics).FirstOrDefaultAsync(c => c.Id == id);
            if (chapter == null) return NotFound();

            var topicIds = chapter.Topics.Select(t => t.Id).ToList();
            var questions = await _db.QuizQuestions
                .Where(q => topicIds.Contains(q.TopicId) && (q.UserId == GetUserId() || q.UserId == null))
                .OrderBy(q => q.Id)
                .ToListAsync();

            var topicTitles = chapter.Topics.ToDictionary(t => t.Id, t => t.Title);

            return Ok(new
            {
                chapterId = id,
                chapterTitle = chapter.Title,
                topics = topicIds.Count,
                questionCount = questions.Count,
                questions = DedupeQuestions(questions).Select(q => MapQuestion(q, topicTitles))
            });
        }

        [HttpGet("chapters/{id}/quiz")]
        public async Task<IActionResult> GetChapterQuiz(int id, [FromQuery] int count = 20, [FromQuery] string difficulty = "all")
        {
            var chapter = await _db.Chapters
                .Include(c => c.Topics)
                .Include(c => c.Subject)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (chapter == null) return NotFound();
            if (chapter.Topics.Count == 0) return BadRequest("This chapter has no topics yet.");

            var exam = chapter.Subject?.Exam;
            var topicIds = chapter.Topics.Select(t => t.Id).ToList();
            count = Math.Clamp(count, 5, 60);
            difficulty = string.IsNullOrWhiteSpace(difficulty) ? "all" : difficulty.ToLowerInvariant();
            var userId = GetUserId();

            var existingQuery = _db.QuizQuestions.Where(q => topicIds.Contains(q.TopicId) && (q.UserId == userId || q.UserId == null));
            if (difficulty != "all")
                existingQuery = existingQuery.Where(q => q.Difficulty == difficulty);
            var existing = await existingQuery.OrderBy(q => q.Id).ToListAsync();

            if (existing.Count >= count)
            {
                return Ok(new
                {
                    chapterId = id,
                    chapterTitle = chapter.Title,
                    topics = topicIds.Count,
                    questions = DedupeQuestions(existing).Take(count).Select(MapQuestion)
                });
            }

            var contentParts = new List<string>();
            foreach (var t in chapter.Topics)
            {
                var tc = await GetEffectiveTopicContent(t.Id, userId);
                var lesson = !string.IsNullOrWhiteSpace(tc?.LessonContent) ? tc.LessonContent : t.LessonContent;
                if (lesson.Length > 2000) lesson = lesson.Substring(0, 2000);
                var formula = !string.IsNullOrWhiteSpace(tc?.FormulaSheet) ? tc.FormulaSheet : t.FormulaSheet;
                if (formula.Length > 1200) formula = formula.Substring(0, 1200);
                contentParts.Add($"### Topic: {t.Title} ({(string.IsNullOrWhiteSpace(t.Description) ? "no description" : t.Description)})\nLESSON:\n{lesson}\nFORMULA:\n{formula}");
            }

            string prompt = $@"
You are creating a comprehensive quiz for the CHAPTER ""{chapter.Title}"", which contains the following topics that the student has finished studying:
{string.Join(", ", chapter.Topics.Select(t => t.Title))}

CONTENT FOR EVERY TOPIC IN THIS CHAPTER (lessons and formula sheets are below):
{string.Join("\n\n", contentParts)}

Steps:
1. Read the content for EVERY topic in the chapter.
2. Generate at least {count} questions so that EVERY topic AND every distinct concept, formula, definition and technique in the chapter is covered.
3. Generate all questions at medium or hard difficulty only. Never produce easy/direct-recall questions. Every question must require the student to apply at least one concept, do meaningful calculation, work through multiple steps, combine ideas, or reason through a tricky edge case. Medium questions require applying a formula or concept; hard questions require multi-step derivation or a tricky edge case. The questions must be challenging enough that the student has to think and work out the answer.
4. Use fresh numbers/scenarios so no two questions test the exact same thing.
5. Use a mix of question types matching the {exam ?? "selected exam"} pattern:
{ExamGuidance.TypeMixInstruction(exam)}
{ExamGuidance.SectionFormatInstruction(exam, chapter.Subject?.Name, chapter.Title)}

Rules:
- Every question must be unique. Never duplicate a question, never reuse the same numbers, and never repeat an option pattern.
- Use \(...\) for inline math and \[...\] for display math.
- Return ONLY a valid JSON array (no markdown, no code fences). Each element:
{{
  ""type"": ""one of the allowed types listed above"",
  ""question"": ""question text"",
  ""options"": [""a"", ""b"", ""c"", ""d""] (empty array for numerical),
  ""correct_answer"": ""exact option text for mcq/truefalse; comma-separated correct options for multi; the numeric value for numerical; the letter A/B/C/D for assertion; the mapping like 'A-2, B-3, C-1, D-4' for matching"",
  ""correct_answers"": [""only for multi, the exact correct option texts""],
  ""assertion"": ""only for assertion, the assertion statement"",
  ""reason"": ""only for assertion, the reason statement"",
  ""right_options"": [""only for matching, the List-II items""],
  ""passage"": ""only for passage questions, the comprehension paragraph text"",
  ""explanation"": ""brief 1-2 sentence explanation"",
  ""difficulty"": ""medium"" or ""hard"",
  ""topic"": ""the exact title of the chapter topic this question belongs to""
}}
- For assertion questions, ""options"" must be exactly: [""Both A and R are true and R is the correct explanation of A"", ""Both A and R are true but R is NOT the correct explanation of A"", ""A is true but R is false"", ""A is false but R is true""] and ""question"" should be ""Assertion and Reason given below.""
- Escape LaTeX backslashes as double backslashes.
";

            var existingTexts = (await _db.QuizQuestions
                .Where(q => q.UserId == userId || q.UserId == null)
                .Select(q => q.QuestionText).ToListAsync())
                .Select(NormalizeQuestionText)
                .ToHashSet();

            var seen = new HashSet<string>(existingTexts);

            const int maxAttempts = 5;
            var added = new List<QuizQuestion>();
            for (int attempt = 0; attempt < maxAttempts && added.Count < count; attempt++)
            {
                var want = count - added.Count;
                var attemptPrompt = $"{prompt}\n\nYou still need to supply {want} more NEW, non-duplicate questions to reach a total of {count}. Generate up to {Math.Min(want * 2, want + 5)} questions. Do not repeat any question already generated or stored.";

                var raw = await _aiService.SendRawPrompt(attemptPrompt);
                var json = ExtractJson(raw);
                List<RawQuestion>? qs = null;
                if (json != null)
                {
                    try
                    {
                        qs = JsonSerializer.Deserialize<List<RawQuestion>>(json, JsonOptions);
                    }
                    catch
                    {
                        qs = null;
                    }
                }

                if (qs == null || qs.Count == 0) continue;

                foreach (var q in qs)
                {
                    if (string.IsNullOrWhiteSpace(q.question)) continue;
                    if (!QuestionMapper.HasValidFormat(q)) continue;
                    if (!ExamGuidance.SectionAllowsType(exam, chapter.Subject?.Name, chapter.Title, QuestionMapper.NormalizeType(q.type))) continue;
                    if (ExamGuidance.IsQuantitativeComparisonQuestion(q.question)
                        && !ExamGuidance.AllowsQuantitativeComparison(exam, chapter.Subject?.Name, chapter.Title)) continue;
                    if (!seen.Add(NormalizeQuestionText(q.question))) continue;

                    var topic = MatchTopic(chapter, q.topic) ?? chapter.Topics.FirstOrDefault();
                    if (topic == null) continue;

                    var saved = new QuizQuestion
                    {
                        TopicId = topic.Id,
                        UserId = userId,
                        Type = QuestionMapper.NormalizeType(q.type),
                        QuestionText = q.question.Trim(),
                        OptionsJson = JsonSerializer.Serialize(q.options ?? new List<string>()),
                        CorrectAnswer = q.correct_answer ?? "",
                        CorrectAnswersJson = QuestionMapper.AnswersJson(q, QuestionMapper.NormalizeType(q.type)),
                        Assertion = q.assertion,
                        Reason = q.reason,
                        RightOptionsJson = QuestionMapper.RightOptionsJson(q, QuestionMapper.NormalizeType(q.type)),
                        Passage = q.passage,
                        Explanation = q.explanation ?? "",
                        Difficulty = NormalizeDifficulty(q.difficulty)
                    };
                    _db.QuizQuestions.Add(saved);
                    added.Add(saved);
                    if (added.Count >= count) break;
                }
            }

            if (added.Count > 0)
            {
                await _db.SaveChangesAsync();
                await LogActivity("quiz");
            }

            var final = await _db.QuizQuestions.Where(x => topicIds.Contains(x.TopicId) && (x.UserId == userId || x.UserId == null)).OrderBy(x => x.Id).ToListAsync();
            var deduped = DedupeQuestions(final).Take(count).ToList();
            if (deduped.Count == 0)
                return StatusCode(502, "AI could not generate the chapter quiz. Please try again.");

            return Ok(new
            {
                chapterId = id,
                chapterTitle = chapter.Title,
                topics = topicIds.Count,
                questions = deduped.Select(MapQuestion)
            });
        }

        private static Topic? MatchTopic(Chapter chapter, string? topicTitle)
        {
            if (string.IsNullOrWhiteSpace(topicTitle)) return null;
            var t = topicTitle.Trim();
            return chapter.Topics.FirstOrDefault(tp => string.Equals(tp.Title, t, StringComparison.OrdinalIgnoreCase))
                ?? chapter.Topics.FirstOrDefault(tp =>
                    tp.Title.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.IndexOf(tp.Title, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static object MapQuestion(QuizQuestion q)
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

        private static object MapQuestion(QuizQuestion q, IReadOnlyDictionary<int, string> topicTitles)
        {
            List<string>? options = null;
            try { options = JsonSerializer.Deserialize<List<string>>(q.OptionsJson); } catch { }
            List<string>? correctAnswers = null;
            try { correctAnswers = JsonSerializer.Deserialize<List<string>>(q.CorrectAnswersJson); } catch { }
            List<string>? rightOptions = null;
            try { rightOptions = JsonSerializer.Deserialize<List<string>>(q.RightOptionsJson); } catch { }
            var topic = topicTitles != null && topicTitles.TryGetValue(q.TopicId, out var t) ? t : null;
            return new
            {
                q.Id,
                type = QuestionMapper.NormalizeType(q.Type),
                questionText = q.QuestionText,
                options = options ?? new List<string>(),
                answer = q.CorrectAnswer,
                correctAnswers = correctAnswers ?? new List<string>(),
                assertion = q.Assertion,
                reason = q.Reason,
                rightOptions = rightOptions ?? new List<string>(),
                passage = q.Passage,
                explanation = q.Explanation,
                topic
            };
        }

        private static string NormalizeQuestionText(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var ch in s.ToLowerInvariant())
                if (!char.IsWhiteSpace(ch))
                    sb.Append(ch);
            return sb.ToString();
        }

        private static string NormalizeDifficulty(string? d)
        {
            var v = (d ?? "").Trim().ToLowerInvariant();
            return v is "medium" or "hard" ? v : "medium";
        }

        private static List<QuizQuestion> DedupeQuestions(IEnumerable<QuizQuestion> questions)
        {
            var seen = new HashSet<string>();
            var result = new List<QuizQuestion>();
            foreach (var q in questions)
            {
                if (q == null) continue;
                if (seen.Add(NormalizeQuestionText(q.QuestionText)))
                    result.Add(q);
            }
            return result;
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

        // ==================== Helpers ====================

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

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

            // The model often emits literal newlines and lone LaTeX backslashes
            // (e.g. \frac, \text) inside JSON string values, which are invalid.
            // Sanitize: double lone backslashes, escape raw newlines, keep \\ pairs.
            var sanitized = SanitizeJsonContent(json);
            if (TryParseJson(sanitized)) return sanitized;

            return null;
        }

        private static string SanitizeJsonContent(string json)
        {
            var sb = new System.Text.StringBuilder(json.Length + 16);
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
                        // The model often emits unescaped double quotes inside string
                        // values (e.g. the arrangement "ABC"). Only treat a quote as the
                        // structural closing quote when it is followed (ignoring whitespace)
                        // by a JSON structural character; otherwise escape it as content.
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

        private static string NormalizeGeneratedNewlines(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Replace("\\n", "\n");
        }
    }

    // ==================== DTOs ====================

    public class SubjectRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Exam { get; set; }
        public string? Description { get; set; }
    }

    public class ChapterRequest
    {
        public int SubjectId { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    public class TopicRequest
    {
        public int ChapterId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class SyllabusRequest
    {
        public string Exam { get; set; } = string.Empty;
        public List<string>? Subjects { get; set; }
    }

    public class SeedSyllabusRequest
    {
        public string? Exam { get; set; }
    }

    public class CreateExamRequest
    {
        public string Name { get; set; } = string.Empty;
        public int Year { get; set; }
        public string? Description { get; set; }
        public List<string>? Subjects { get; set; }
    }

    public class SetProgressRequest
    {
        public string? Status { get; set; }
    }

    public class CompleteReviewRequest
    {
        public int Quality { get; set; } = 4;
    }

    public class SyllabusSubjectDto
    {
        public string Subject { get; set; } = string.Empty;
        public List<SyllabusChapterDto>? Chapters { get; set; }
    }

    public class SeedFileDto
    {
        public string? Exam { get; set; }
        public List<SyllabusSubjectDto>? Subjects { get; set; }
    }

    public class SyllabusChapterDto
    {
        public string Title { get; set; } = string.Empty;
        public List<SyllabusTopicDto>? Topics { get; set; }
    }

    public class SyllabusTopicDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class ContentDto
    {
        public string? Lesson { get; set; }
        public string? Notes { get; set; }
        public string? Revision { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("formula_sheet")]
        public string? FormulaSheet { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("concept_map")]
        public string? ConceptMap { get; set; }
    }
}
