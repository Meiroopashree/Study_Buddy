using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Models;

namespace StudyBuddy.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    public class DashboardController : ControllerBase
    {
        private readonly StudyBuddyContext _db;

        public DashboardController(StudyBuddyContext db)
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

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var userId = GetUserId();

            var quizQuery = _db.QuizResults.AsQueryable();
            var docQuery = _db.Documents.AsQueryable();
            var noteQuery = _db.Notes.AsQueryable();
            var activityQuery = _db.StudyActivities.AsQueryable();
            if (userId.HasValue)
            {
                quizQuery = quizQuery.Where(q => q.UserId == userId.Value);
                docQuery = docQuery.Where(d => d.UserId == userId.Value);
                noteQuery = noteQuery.Where(n => n.UserId == userId.Value);
                activityQuery = activityQuery.Where(a => a.UserId == userId.Value);
            }

            var quizzes = await quizQuery.ToListAsync();
            var recentUploads = await docQuery.OrderByDescending(d => d.UploadedAt).Take(5)
                .Select(d => new { d.Id, d.Title, d.UploadedAt }).ToListAsync();
            var recentQuizzes = quizzes.OrderByDescending(q => q.CompletedAt).Take(5)
                .Select(q => new
                {
                    q.Id, q.Topic, q.Difficulty, q.Score, q.TotalQuestions, q.CompletedAt,
                    Accuracy = q.TotalQuestions > 0 ? Math.Round((double)q.Score / q.TotalQuestions * 100, 1) : 0
                }).ToList();
            var recentNotes = await noteQuery.OrderByDescending(n => n.UpdatedAt).Take(5)
                .Select(n => new { n.Id, n.Title, n.Content, n.UpdatedAt }).ToListAsync();

            var allActivities = await activityQuery.OrderBy(a => a.Timestamp).ToListAsync();
            var today = DateTime.UtcNow.Date;
            var todayActivities = allActivities.Where(a => a.Timestamp.Date == today).ToList();
            var todayQuizSec = quizzes.Where(q => q.CompletedAt.Date == today).Sum(q => q.TimeSpentSeconds);
            var totalQuizSec = quizzes.Sum(q => q.TimeSpentSeconds);

            long studyTimeToday = SumActiveTime(todayActivities.Select(a => a.Timestamp)) + todayQuizSec;
            long totalStudyTime = SumActiveTime(allActivities.Select(a => a.Timestamp)) + totalQuizSec;

            int totalCorrect = quizzes.Sum(q => q.Score);
            int totalQuestions = quizzes.Sum(q => q.TotalQuestions);
            double accuracy = totalQuestions > 0 ? Math.Round((double)totalCorrect / totalQuestions * 100, 1) : 0;
            double avgScore = quizzes.Count > 0 ? Math.Round(quizzes.Average(q => q.Score), 1) : 0;
            int bestScore = quizzes.Count > 0 ? quizzes.Max(q => q.Score) : 0;
            double avgQuizTime = quizzes.Count > 0 ? Math.Round(quizzes.Average(q => q.TimeSpentSeconds)) : 0;

            var profile = userId.HasValue ? await _db.Users.FindAsync(userId.Value) : null;
            var messagesCount = await _db.ChatHistory.CountAsync();

            return Ok(new
            {
                Profile = profile == null ? null : new { profile.Id, profile.Username, profile.Email, profile.Provider, profile.CreatedAt },
                RecentUploads = recentUploads,
                RecentQuizzes = recentQuizzes,
                RecentNotes = recentNotes,
                Counts = new
                {
                    Quizzes = quizzes.Count,
                    Documents = await docQuery.CountAsync(),
                    Notes = await noteQuery.CountAsync(),
                    Messages = messagesCount
                },
                Analytics = new
                {
                    Accuracy = accuracy,
                    AvgScore = avgScore,
                    BestScore = bestScore,
                    StudyTimeTodaySec = studyTimeToday,
                    TotalStudyTimeSec = totalStudyTime,
                    AvgQuizTimeSec = avgQuizTime,
                    QuizzesTaken = quizzes.Count
                }
            });
        }

        [HttpGet("exam-stats")]
        public async Task<IActionResult> GetExamStats()
        {
            var userId = GetUserId();

            var subjects = await _db.Subjects
                .Include(s => s.Chapters)
                .ThenInclude(c => c.Topics)
                .ToListAsync();

            var progressMap = await _db.TopicProgresses
                .Where(p => p.UserId == userId)
                .ToDictionaryAsync(p => p.TopicId, p => p.Status);

            var examGroups = subjects
                .Where(s => !string.IsNullOrWhiteSpace(s.Exam))
                .GroupBy(s => s.Exam)
                .Select(g =>
                {
                    var allTopics = g.SelectMany(s => s.Chapters).SelectMany(c => c.Topics).ToList();
                    var totalTopics = allTopics.Count;
                    var mastered = allTopics.Count(t => progressMap.TryGetValue(t.Id, out var st) && st == "mastered");
                    var inProgress = allTopics.Count(t => progressMap.TryGetValue(t.Id, out var st) && st == "in_progress");
                    var revising = allTopics.Count(t => progressMap.TryGetValue(t.Id, out var st) && st == "revising");
                    var done = mastered + inProgress + revising;
                    var pct = totalTopics > 0 ? Math.Round((double)done / totalTopics * 100) : 0;

                    return new
                    {
                        exam = g.Key,
                        totalTopics,
                        mastered,
                        inProgress,
                        revising,
                        notStarted = totalTopics - done,
                        completedTopics = done,
                        pct
                    };
                })
                .OrderByDescending(e => e.pct)
                .ToList();

            return Ok(examGroups);
        }

        private static long SumActiveTime(IEnumerable<DateTime> times, int maxGapSeconds = 45 * 60)
        {
            var sorted = times.OrderBy(t => t).ToList();
            long total = 0;
            for (int i = 1; i < sorted.Count; i++)
            {
                var gap = (sorted[i] - sorted[i - 1]).TotalSeconds;
                if (gap >= 0 && gap <= maxGapSeconds)
                    total += (long)gap;
            }
            return total;
        }
    }
}
