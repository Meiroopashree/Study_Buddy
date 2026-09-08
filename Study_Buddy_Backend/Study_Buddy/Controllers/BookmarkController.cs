using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Models;

namespace StudyBuddy.Controllers
{
    [ApiController]
    [Route("api/bookmarks")]
    public class BookmarkController : ControllerBase
    {
        private readonly StudyBuddyContext _db;

        public BookmarkController(StudyBuddyContext db)
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

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var userId = GetUserId();
            var query = _db.Bookmarks
                .Include(b => b.Topic)
                .ThenInclude(t => t.Chapter)
                .ThenInclude(c => c.Subject)
                .AsQueryable();
            if (userId.HasValue)
                query = query.Where(b => b.UserId == userId.Value);

            var bookmarks = await query
                .OrderByDescending(b => b.CreatedAt)
                .Select(b => new
                {
                    b.Id,
                    b.TopicId,
                    b.CreatedAt,
                    TopicTitle = b.Topic.Title,
                    ChapterTitle = b.Topic.Chapter.Title,
                    SubjectName = b.Topic.Chapter.Subject.Name,
                    Exam = b.Topic.Chapter.Subject.Exam
                })
                .ToListAsync();
            return Ok(bookmarks);
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] BookmarkRequest request)
        {
            if (request.TopicId <= 0)
                return BadRequest("TopicId is required.");

            var topic = await _db.Topics.FindAsync(request.TopicId);
            if (topic == null) return NotFound("Topic not found.");

            var userId = GetUserId();
            var existing = await _db.Bookmarks
                .FirstOrDefaultAsync(b => b.TopicId == request.TopicId && b.UserId == userId);
            if (existing != null)
                return Ok(new { existing.Id, existing.TopicId, Duplicate = true });

            var bookmark = new Bookmark
            {
                TopicId = request.TopicId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            _db.Bookmarks.Add(bookmark);
            await _db.SaveChangesAsync();
            return Ok(new { bookmark.Id, bookmark.TopicId, Duplicate = false });
        }

        [HttpDelete("{topicId}")]
        public async Task<IActionResult> Remove(int topicId)
        {
            var userId = GetUserId();
            var bookmark = await _db.Bookmarks
                .FirstOrDefaultAsync(b => b.TopicId == topicId && b.UserId == userId);
            if (bookmark == null) return NotFound();
            _db.Bookmarks.Remove(bookmark);
            await _db.SaveChangesAsync();
            return Ok();
        }
    }

    public class BookmarkRequest
    {
        public int TopicId { get; set; }
    }
}
