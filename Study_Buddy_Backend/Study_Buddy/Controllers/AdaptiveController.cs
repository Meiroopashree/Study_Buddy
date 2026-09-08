using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.DTOs;
using StudyBuddy.Models;
using System.Text.Json;

namespace StudyBuddy.Controllers
{
    [ApiController]
    [Route("api/adaptive")]
    public class AdaptiveController : ControllerBase
    {
        private readonly StudyBuddyContext _db;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public AdaptiveController(StudyBuddyContext db)
        {
            _db = db;
        }

        private int? GetUserId()
        {
            var auth = HttpContext.Request.Headers["authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(auth)) return null;
            var user = _db.Users.FirstOrDefault(u => u.Token == auth);
            return user?.Id;
        }

        private IQueryable<QuizResult> UserQuizQuery()
        {
            var query = _db.QuizResults.AsQueryable();
            var userId = GetUserId();
            if (userId.HasValue)
                query = query.Where(q => q.UserId == userId.Value);
            return query;
        }

        [HttpGet("insights")]
        public async Task<IActionResult> GetInsights()
        {
            var results = await UserQuizQuery().ToListAsync();
            if (results.Count == 0)
                return Ok(new { summary = EmptySummary(), topics = new List<object>(), hasData = false });

            var topics = await BuildTopicInsights(results);

            int totalQuestions = results.Sum(q => q.TotalQuestions);
            int totalCorrect = results.Sum(q => q.Score);
            int mistakes = 0;
            foreach (var r in results)
            {
                var answers = SafeDeserialize<UserAnswer>(r.AnswersJson);
                mistakes += answers.Count(a => a != null && !a.IsCorrect);
            }

            var summary = new
            {
                quizzesTaken = results.Count,
                accuracy = totalQuestions > 0 ? Math.Round((double)totalCorrect / totalQuestions * 100, 1) : 0,
                avgTimePerQuestion = totalQuestions > 0 ? Math.Round((double)results.Sum(q => q.TimeSpentSeconds) / totalQuestions, 1) : 0,
                weakTopics = topics.Count(t => t.Classification == "weak"),
                strongTopics = topics.Count(t => t.Classification == "strong"),
                mistakes,
                hasData = true
            };

            return Ok(new { summary, topics, hasData = true });
        }

        [HttpGet("plan")]
        public async Task<IActionResult> GetPlan()
        {
            var results = await UserQuizQuery().ToListAsync();
            if (results.Count == 0)
                return Ok(new { date = DateTime.Now.ToString("yyyy-MM-dd"), revisionTopics = new List<object>(), recommendedQuiz = (object?)null, hasData = false });

            var topics = await BuildTopicInsights(results);

            var due = topics
                .Where(t => t.Classification is "weak" or "needs-work")
                .OrderBy(t => t.Confidence)
                .ThenByDescending(t => t.DaysSinceLast)
                .Take(3)
                .ToList();

            if (due.Count == 0)
                due = topics.OrderBy(t => t.Confidence).Take(2).ToList();

            var recommended = topics
                .Where(t => t.Classification is "weak" or "needs-work")
                .OrderBy(t => t.Confidence)
                .ThenByDescending(t => t.HasQuestions)
                .FirstOrDefault();

            int mistakes = 0;
            foreach (var r in results)
            {
                var answers = SafeDeserialize<UserAnswer>(r.AnswersJson);
                mistakes += answers.Count(a => a != null && !a.IsCorrect);
            }

            return Ok(new
            {
                date = DateTime.Now.ToString("yyyy-MM-dd"),
                revisionTopics = due.Select(t => (object)new
                {
                    t.Topic,
                    t.TopicId,
                    t.Classification,
                    t.Confidence,
                    t.Accuracy,
                    t.Attempts,
                    t.DaysSinceLast,
                    t.HasQuestions
                }),
                recommendedQuiz = recommended == null ? null : new
                {
                    recommended.Topic,
                    recommended.TopicId,
                    recommended.Classification,
                    recommended.Accuracy,
                    recommended.Confidence,
                    recommended.Attempts,
                    recommended.DaysSinceLast,
                    recommended.HasQuestions,
                    mistakeCount = recommended.MistakeCount
                },
                mistakes,
                hasData = true
            });
        }

        [HttpGet("mistakes")]
        public async Task<IActionResult> GetMistakes()
        {
            var results = await UserQuizQuery()
                .OrderByDescending(q => q.CompletedAt)
                .Take(50)
                .ToListAsync();

            var subjectLookup = await BuildSubjectLookup();

            var mistakes = new List<object>();
            var seen = new HashSet<string>();

            foreach (var r in results)
            {
                var answers = SafeDeserialize<UserAnswer>(r.AnswersJson);
                var questions = SafeDeserialize<Question>(r.QuestionsJson);
                var questionLookup = questions
                    .GroupBy(q => q.QuestionText)
                    .ToDictionary(g => g.Key, g => g.First());

                foreach (var a in answers)
                {
                    if (a == null || a.IsCorrect || string.IsNullOrWhiteSpace(a.Question)) continue;
                    var key = a.Question.Trim().ToLowerInvariant();
                    if (!seen.Add(key)) continue;
                    var full = questionLookup.TryGetValue(a.Question, out var q) ? q : null;
                    mistakes.Add(new
                    {
                        subject = ResolveSubject(r.Topic, subjectLookup),
                        topic = r.Topic,
                        quizId = r.Id,
                        completedAt = r.CompletedAt,
                        questionText = a.Question,
                        type = full != null ? QuestionMapper.NormalizeType(full.Type) : "mcq",
                        options = full?.Options ?? new List<string>(),
                        correctAnswers = full?.CorrectAnswers ?? new List<string>(),
                        assertion = full?.Assertion,
                        reason = full?.Reason,
                        rightOptions = full?.RightOptions ?? new List<string>(),
                        passage = full?.Passage,
                        yourAnswer = a.YourAnswer,
                        correctAnswer = a.CorrectAnswer,
                        explanation = full?.Explanation
                    });
                    if (mistakes.Count >= 60) break;
                }
                if (mistakes.Count >= 60) break;
            }

            return Ok(new { count = mistakes.Count, mistakes });
        }

        [HttpGet("quiz")]
        public async Task<IActionResult> GetAdaptiveQuiz([FromQuery] string? topic = null, [FromQuery] int count = 10)
        {
            count = Math.Clamp(count, 1, 40);
            var results = await UserQuizQuery().ToListAsync();
            if (results.Count == 0)
                return BadRequest(new { message = "Complete a quiz first so we can personalise questions for you." });

            var topics = await BuildTopicInsights(results);
            string? target = string.IsNullOrWhiteSpace(topic)
                ? topics.Where(t => t.Classification is "weak" or "needs-work" && (t.HasQuestions || t.MistakeCount > 0))
                        .OrderBy(t => t.Confidence)
                        .FirstOrDefault()?.Topic
                : topic.Trim();

            if (target == null)
                target = topics.FirstOrDefault()?.Topic;
            if (target == null)
                return BadRequest(new { message = "No quiz history found." });

            var pool = new List<object>();
            var seen = new HashSet<string>();

            // 1) Questions the user got wrong on this topic
            foreach (var r in results.Where(r => string.Equals(r.Topic, target, StringComparison.OrdinalIgnoreCase)))
            {
                var answers = SafeDeserialize<UserAnswer>(r.AnswersJson);
                var questions = SafeDeserialize<Question>(r.QuestionsJson);
                var lookup = questions.GroupBy(q => q.QuestionText).ToDictionary(g => g.Key, g => g.First());
                foreach (var a in answers.Where(a => a != null && !a.IsCorrect))
                {
                    if (string.IsNullOrWhiteSpace(a.Question)) continue;
                    if (!seen.Add(a.Question.Trim().ToLowerInvariant())) continue;
                    var full = lookup.TryGetValue(a.Question, out var q) ? q : null;
                    var type = full != null ? QuestionMapper.NormalizeType(full.Type) : "mcq";
                    pool.Add(new
                    {
                        id = r.Id,
                        type,
                        questionText = a.Question,
                        options = full?.Options ?? new List<string>(),
                        answer = a.CorrectAnswer,
                        correctAnswers = full?.CorrectAnswers ?? new List<string>(),
                        assertion = full?.Assertion,
                        reason = full?.Reason,
                        rightOptions = full?.RightOptions ?? new List<string>(),
                        passage = full?.Passage,
                        explanation = full?.Explanation
                    });
                }
            }

            // 2) Stored questions for the topic (from the Learn engine) to pad
            var allTopics = await _db.Topics.ToListAsync();
            var matchedTopic = allTopics.FirstOrDefault(t =>
                t.Title.Equals(target, StringComparison.OrdinalIgnoreCase));
            if (matchedTopic != null)
            {
                var stored = await _db.QuizQuestions
                    .Where(q => q.TopicId == matchedTopic.Id)
                    .OrderBy(q => q.Id)
                    .ToListAsync();
                foreach (var q in stored)
                {
                    if (!seen.Add(q.QuestionText.Trim().ToLowerInvariant())) continue;
                    List<string>? options = null;
                    try { options = JsonSerializer.Deserialize<List<string>>(q.OptionsJson); } catch { }
                    List<string>? correctAnswers = null;
                    try { correctAnswers = JsonSerializer.Deserialize<List<string>>(q.CorrectAnswersJson); } catch { }
                    List<string>? rightOptions = null;
                    try { rightOptions = JsonSerializer.Deserialize<List<string>>(q.RightOptionsJson); } catch { }
                    pool.Add(new
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
                    });
                }
            }

            if (pool.Count == 0)
                return BadRequest(new { message = $"No questions available for '{target}' yet. Take a quiz on this topic first." });

            var selected = pool.OrderBy(_ => Guid.NewGuid()).Take(count).ToList();
            return Ok(new
            {
                topic = target,
                available = pool.Count,
                questions = selected
            });
        }

        [HttpGet("drill")]
        public async Task<IActionResult> GetDrill([FromQuery] int count = 10, [FromQuery] int top = 3)
        {
            count = Math.Clamp(count, 1, 40);
            top = Math.Clamp(top, 1, 10);
            var results = await UserQuizQuery().ToListAsync();
            if (results.Count == 0)
                return BadRequest(new { message = "Complete a quiz first so we can drill your weak topics." });

            var topics = await BuildTopicInsights(results);
            var weakTopics = topics
                .Where(t => t.Classification is "weak" or "needs-work" && (t.HasQuestions || t.MistakeCount > 0))
                .OrderBy(t => t.Confidence)
                .ThenByDescending(t => t.MistakeCount)
                .Take(top)
                .ToList();

            if (weakTopics.Count == 0)
                weakTopics = topics
                    .Where(t => t.HasQuestions || t.MistakeCount > 0)
                    .OrderBy(t => t.Confidence)
                    .Take(top)
                    .ToList();
            if (weakTopics.Count == 0)
                return BadRequest(new { message = "No quiz history found. Take a quiz first." });

            var pool = new List<object>();
            var seen = new HashSet<string>();
            var allTopics = await _db.Topics.ToListAsync();
            var allStored = await _db.QuizQuestions.ToListAsync();

            foreach (var insight in weakTopics)
            {
                var target = insight.Topic;
                foreach (var r in results.Where(r => string.Equals(r.Topic, target, StringComparison.OrdinalIgnoreCase)))
                {
                    var answers = SafeDeserialize<UserAnswer>(r.AnswersJson);
                    var questions = SafeDeserialize<Question>(r.QuestionsJson);
                    var lookup = questions.GroupBy(q => q.QuestionText).ToDictionary(g => g.Key, g => g.First());
                    foreach (var a in answers.Where(a => a != null && !a.IsCorrect))
                    {
                        if (string.IsNullOrWhiteSpace(a.Question)) continue;
                        if (!seen.Add(a.Question.Trim().ToLowerInvariant())) continue;
                        var full = lookup.TryGetValue(a.Question, out var q) ? q : null;
                        var type = full != null ? QuestionMapper.NormalizeType(full.Type) : "mcq";
                        pool.Add(new
                        {
                            id = r.Id,
                            type,
                            questionText = a.Question,
                            options = full?.Options ?? new List<string>(),
                            answer = a.CorrectAnswer,
                            correctAnswers = full?.CorrectAnswers ?? new List<string>(),
                            assertion = full?.Assertion,
                            reason = full?.Reason,
                            rightOptions = full?.RightOptions ?? new List<string>(),
                            passage = full?.Passage,
                            explanation = full?.Explanation,
                            topic = target
                        });
                    }
                }

                var matchedTopic = allTopics.FirstOrDefault(t =>
                    t.Title.Equals(target, StringComparison.OrdinalIgnoreCase));
                if (matchedTopic != null)
                {
                    foreach (var q in allStored.Where(q => q.TopicId == matchedTopic.Id))
                    {
                        if (!seen.Add(q.QuestionText.Trim().ToLowerInvariant())) continue;
                        List<string>? options = null;
                        try { options = JsonSerializer.Deserialize<List<string>>(q.OptionsJson); } catch { }
                        List<string>? correctAnswers = null;
                        try { correctAnswers = JsonSerializer.Deserialize<List<string>>(q.CorrectAnswersJson); } catch { }
                        List<string>? rightOptions = null;
                        try { rightOptions = JsonSerializer.Deserialize<List<string>>(q.RightOptionsJson); } catch { }
                        pool.Add(new
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
                            topic = target
                        });
                    }
                }
            }

            if (pool.Count == 0)
                return BadRequest(new { message = "No drill questions available yet. Complete more quizzes to build your weak-topic drill." });

            var selected = pool.OrderBy(_ => Guid.NewGuid()).Take(count).ToList();
            return Ok(new
            {
                mode = "drill",
                topics = weakTopics.Select(t => new { t.Topic, t.Classification, t.Confidence }),
                available = pool.Count,
                questions = selected
            });
        }

        private async Task<List<TopicInsight>> BuildTopicInsights(List<QuizResult> results)
        {
            var allTopics = await _db.Topics.ToListAsync();
            var topicTitles = allTopics
                .GroupBy(t => t.Title.ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First());

            var grouped = results.GroupBy(r => r.Topic.Trim());
            var insights = new List<TopicInsight>();

            foreach (var g in grouped)
            {
                var title = g.Key;
                if (string.IsNullOrWhiteSpace(title)) continue;

                int totalQ = g.Sum(r => r.TotalQuestions);
                int totalCorrect = g.Sum(r => r.Score);
                double accuracy = totalQ > 0 ? (double)totalCorrect / totalQ * 100 : 0;
                double avgTime = totalQ > 0 ? (double)g.Sum(r => r.TimeSpentSeconds) / totalQ : 0;
                var last = g.Max(r => r.CompletedAt);
                int daysSince = Math.Max(0, (int)(DateTime.UtcNow - last).TotalDays);
                int attempts = g.Count();

                var matched = topicTitles.TryGetValue(title.ToLowerInvariant(), out var t) ? t : null;
                int? topicId = matched?.Id;
                bool hasQuestions = matched != null && await _db.QuizQuestions.AnyAsync(q => q.TopicId == matched.Id);
                int mistakeCount = 0;
                foreach (var r in g)
                {
                    var answers = SafeDeserialize<UserAnswer>(r.AnswersJson);
                    mistakeCount += answers.Count(a => a != null && !a.IsCorrect);
                }

                var classification = accuracy < 50 ? "weak" : accuracy < 75 ? "needs-work" : "strong";

                var confidence = ComputeConfidence(accuracy, attempts, daysSince);

                insights.Add(new TopicInsight
                {
                    Topic = title,
                    TopicId = topicId,
                    Accuracy = Math.Round(accuracy, 1),
                    AvgTimePerQuestion = Math.Round(avgTime, 1),
                    Attempts = attempts,
                    DaysSinceLast = daysSince,
                    HasQuestions = hasQuestions,
                    MistakeCount = mistakeCount,
                    Classification = classification,
                    Confidence = confidence
                });
            }

            return insights
                .OrderByDescending(i => i.Attempts)
                .ThenByDescending(i => i.Accuracy)
                .ToList();
        }

        private static int ComputeConfidence(double accuracy, int attempts, int daysSince)
        {
            double acc = Math.Clamp(accuracy, 0, 100) * 0.6;
            double attemptScore = Math.Min(attempts, 5) / 5.0 * 20;
            double recency = daysSince <= 1 ? 1 : Math.Max(0, 1 - (daysSince - 1) / 14.0);
            double score = acc + attemptScore + recency * 20;
            return (int)Math.Round(Math.Clamp(score, 0, 100));
        }

        private static object EmptySummary() => new
        {
            quizzesTaken = 0,
            accuracy = 0,
            avgTimePerQuestion = 0,
            weakTopics = 0,
            strongTopics = 0,
            mistakes = 0,
            hasData = false
        };

        private static List<T> SafeDeserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<T>();
            try { return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new List<T>(); }
            catch { return new List<T>(); }
        }

        private async Task<SubjectLookup> BuildSubjectLookup()
        {
            var lookup = new SubjectLookup();
            var subjects = await _db.Subjects
                .Include(s => s.Chapters)
                    .ThenInclude(c => c.Topics)
                .ToListAsync();
            foreach (var s in subjects)
            {
                var subjectName = ToTitleCase(s.Name.Trim());
                lookup.SubjectNames.Add(subjectName.ToLowerInvariant());
                foreach (var c in s.Chapters)
                {
                    var chapterKey = c.Title.Trim().ToLowerInvariant();
                    lookup.ChapterToSubject[chapterKey] = subjectName;
                    foreach (var t in c.Topics)
                        lookup.TopicToSubject[t.Title.Trim().ToLowerInvariant()] = subjectName;
                }
            }
            return lookup;
        }

        private static string ToTitleCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Uncategorized";
            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }

        private static string ResolveSubject(string? topicTitle, SubjectLookup lookup)
        {
            if (string.IsNullOrWhiteSpace(topicTitle)) return "Uncategorized";
            var normalized = topicTitle.Trim().ToLowerInvariant();

            if (lookup.TopicToSubject.TryGetValue(normalized, out var subject))
                return subject;
            if (lookup.ChapterToSubject.TryGetValue(normalized, out subject))
                return subject;

            foreach (var name in lookup.SubjectNames)
            {
                if (normalized.Contains(name)) return name;
            }

            foreach (var kv in lookup.ChapterToSubject)
            {
                if (normalized.Contains(kv.Key)) return kv.Value;
            }
            foreach (var kv in lookup.TopicToSubject)
            {
                if (normalized.Contains(kv.Key)) return kv.Value;
            }

            foreach (var name in lookup.SubjectNames)
            {
                var stem = name.Length >= 4 ? name.Substring(0, 4) : name;
                if (normalized.Contains(stem)) return name;
            }

            return "Uncategorized";
        }

        private class SubjectLookup
        {
            public HashSet<string> SubjectNames { get; } = new HashSet<string>();
            public Dictionary<string, string> ChapterToSubject { get; } = new Dictionary<string, string>();
            public Dictionary<string, string> TopicToSubject { get; } = new Dictionary<string, string>();
        }

        private class TopicInsight
        {
            public string Topic { get; set; } = string.Empty;
            public int? TopicId { get; set; }
            public double Accuracy { get; set; }
            public double AvgTimePerQuestion { get; set; }
            public int Attempts { get; set; }
            public int DaysSinceLast { get; set; }
            public bool HasQuestions { get; set; }
            public int MistakeCount { get; set; }
            public string Classification { get; set; } = "weak";
            public int Confidence { get; set; }
        }
    }
}
