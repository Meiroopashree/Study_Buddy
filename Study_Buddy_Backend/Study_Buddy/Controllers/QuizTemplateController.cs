using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Models;

namespace StudyBuddy.Controllers
{
    [ApiController]
    [Route("api/quiz-templates")]
    public class QuizTemplateController : ControllerBase
    {
        private readonly StudyBuddyContext _db;

        public QuizTemplateController(StudyBuddyContext db)
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
            var query = _db.QuizTemplates.AsQueryable();
            if (userId.HasValue)
                query = query.Where(t => t.UserId == userId.Value);

            var templates = await query
                .OrderByDescending(t => t.UpdatedAt)
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.ScopeType,
                    t.Exam,
                    t.Subject,
                    t.Chapter,
                    t.Topic,
                    t.QuestionCount,
                    t.Difficulty,
                    t.CreatedAt,
                    t.UpdatedAt
                })
                .ToListAsync();
            return Ok(templates);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] QuizTemplateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Template name is required.");

            var now = DateTime.UtcNow;
            var template = new QuizTemplate
            {
                Name = request.Name.Trim(),
                ScopeType = request.ScopeType,
                Exam = request.Exam,
                Subject = request.Subject,
                Chapter = request.Chapter,
                Topic = request.Topic,
                QuestionCount = request.QuestionCount > 0 ? request.QuestionCount : 10,
                Difficulty = request.Difficulty,
                UserId = GetUserId(),
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.QuizTemplates.Add(template);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                template.Id,
                template.Name,
                template.ScopeType,
                template.Exam,
                template.Subject,
                template.Chapter,
                template.Topic,
                template.QuestionCount,
                template.Difficulty,
                template.CreatedAt,
                template.UpdatedAt
            });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] QuizTemplateRequest request)
        {
            var template = await _db.QuizTemplates.FindAsync(id);
            if (template == null) return NotFound();
            var userId = GetUserId();
            if (userId.HasValue && template.UserId != userId.Value) return Forbid();

            if (!string.IsNullOrWhiteSpace(request.Name))
                template.Name = request.Name.Trim();
            if (request.ScopeType != null) template.ScopeType = request.ScopeType;
            if (request.Exam != null) template.Exam = request.Exam;
            if (request.Subject != null) template.Subject = request.Subject;
            if (request.Chapter != null) template.Chapter = request.Chapter;
            if (request.Topic != null) template.Topic = request.Topic;
            if (request.QuestionCount > 0) template.QuestionCount = request.QuestionCount;
            if (request.Difficulty != null) template.Difficulty = request.Difficulty;
            template.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(new
            {
                template.Id,
                template.Name,
                template.ScopeType,
                template.Exam,
                template.Subject,
                template.Chapter,
                template.Topic,
                template.QuestionCount,
                template.Difficulty,
                template.CreatedAt,
                template.UpdatedAt
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var template = await _db.QuizTemplates.FindAsync(id);
            if (template == null) return NotFound();
            var userId = GetUserId();
            if (userId.HasValue && template.UserId != userId.Value) return Forbid();
            _db.QuizTemplates.Remove(template);
            await _db.SaveChangesAsync();
            return Ok();
        }
    }

    public class QuizTemplateRequest
    {
        public string? Name { get; set; }
        public string? ScopeType { get; set; }
        public string? Exam { get; set; }
        public string? Subject { get; set; }
        public string? Chapter { get; set; }
        public string? Topic { get; set; }
        public int QuestionCount { get; set; }
        public string? Difficulty { get; set; }
    }
}
