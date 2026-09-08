using Microsoft.EntityFrameworkCore;
using StudyBuddy.Controllers;
using StudyBuddy.Data;
using StudyBuddy.Models;
using System.Text.Json;

namespace StudyBuddy.Services
{
    /// <summary>
    /// Creates the database schema (PostgreSQL) and seeds the built-in exam syllabi
    /// from SeedData/*.json on startup. An exam is only inserted when it has no
    /// subjects yet, so restarts stay idempotent. Set SEED_RESET=true to force a
    /// delete-and-reinsert of every seeded exam.
    /// </summary>
    public static class StartupSeeder
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public static void Initialize(StudyBuddyContext db)
        {
            db.Database.EnsureCreated();

            var seedDir = Path.Combine(AppContext.BaseDirectory, "SeedData");
            if (!Directory.Exists(seedDir)) return;

            var forceReset = string.Equals(
                Environment.GetEnvironmentVariable("SEED_RESET") ?? "",
                "true",
                StringComparison.OrdinalIgnoreCase);

            var byExam = new Dictionary<string, Dictionary<string, SyllabusSubjectDto>>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in Directory.GetFiles(seedDir, "*.json"))
            {
                List<SyllabusSubjectDto>? subjects = null;
                string? exam = null;

                try
                {
                    var raw = File.ReadAllText(file);
                    using var doc = JsonDocument.Parse(raw);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        exam = "JEE";
                        subjects = JsonSerializer.Deserialize<List<SyllabusSubjectDto>>(raw, JsonOptions);
                    }
                    else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        var decoded = JsonSerializer.Deserialize<SeedFileDto>(raw, JsonOptions);
                        if (decoded != null && !string.IsNullOrWhiteSpace(decoded.Exam) && decoded.Subjects != null)
                        {
                            exam = decoded.Exam.Trim();
                            subjects = decoded.Subjects;
                        }
                    }
                }
                catch
                {
                    continue;
                }

                if (exam == null || subjects == null) continue;

                if (!byExam.TryGetValue(exam, out var map))
                {
                    map = new Dictionary<string, SyllabusSubjectDto>(StringComparer.OrdinalIgnoreCase);
                    byExam[exam] = map;
                }

                foreach (var s in subjects)
                {
                    if (string.IsNullOrWhiteSpace(s.Subject)) continue;
                    map[s.Subject.Trim()] = s;
                }
            }

            foreach (var entry in byExam)
            {
                if (!forceReset && db.Subjects.Any(s => s.Exam == entry.Key)) continue;

                var existing = db.Subjects
                    .Include(s => s.Chapters)
                    .ThenInclude(c => c.Topics)
                    .Where(s => s.Exam == entry.Key)
                    .ToList();

                foreach (var s in existing)
                {
                    foreach (var c in s.Chapters)
                    {
                        db.Topics.RemoveRange(c.Topics);
                    }
                    db.Chapters.RemoveRange(s.Chapters);
                    db.Subjects.Remove(s);
                }
                db.SaveChanges();

                foreach (var s in entry.Value.Values)
                {
                    var subject = new Subject { Exam = entry.Key, Name = s.Subject.Trim() };
                    db.Subjects.Add(subject);
                    db.SaveChanges();

                    foreach (var ch in s.Chapters ?? new List<SyllabusChapterDto>())
                    {
                        if (string.IsNullOrWhiteSpace(ch.Title)) continue;
                        var chapter = new Chapter { SubjectId = subject.Id, Title = ch.Title.Trim() };
                        db.Chapters.Add(chapter);
                        db.SaveChanges();

                        foreach (var tp in ch.Topics ?? new List<SyllabusTopicDto>())
                        {
                            if (string.IsNullOrWhiteSpace(tp.Title)) continue;
                            db.Topics.Add(new Topic
                            {
                                ChapterId = chapter.Id,
                                Title = tp.Title.Trim(),
                                Description = tp.Description ?? "",
                                UpdatedAt = DateTime.UtcNow
                            });
                        }
                        db.SaveChanges();
                    }
                }
            }
        }
    }
}