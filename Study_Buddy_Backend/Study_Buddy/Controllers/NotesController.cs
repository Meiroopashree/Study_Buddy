using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Models;

namespace StudyBuddy.Controllers
{
    [ApiController]
    [Route("api/notes")]
    public class NotesController : ControllerBase
    {
        private readonly StudyBuddyContext _db;

        public NotesController(StudyBuddyContext db)
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
            var query = _db.Notes.AsQueryable();
            if (userId.HasValue)
                query = query.Where(n => n.UserId == userId.Value);

            var notes = await query
                .OrderByDescending(n => n.UpdatedAt)
                .Select(n => new { n.Id, n.Title, n.Content, n.CreatedAt, n.UpdatedAt })
                .ToListAsync();
            return Ok(notes);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var note = await _db.Notes.FindAsync(id);
            if (note == null) return NotFound();
            var userId = GetUserId();
            if (userId.HasValue && note.UserId != userId.Value) return Forbid();
            return Ok(note);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] NoteRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Content) && string.IsNullOrWhiteSpace(request.Title))
                return BadRequest("Note content is required.");

            var note = new Note
            {
                Title = request.Title ?? "Untitled",
                Content = request.Content ?? "",
                UserId = GetUserId()
            };

            _db.Notes.Add(note);
            await _db.SaveChangesAsync();

            return Ok(new { note.Id, note.Title, note.Content, note.CreatedAt, note.UpdatedAt });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] NoteRequest request)
        {
            var note = await _db.Notes.FindAsync(id);
            if (note == null) return NotFound();
            var userId = GetUserId();
            if (userId.HasValue && note.UserId != userId.Value) return Forbid();

            if (!string.IsNullOrWhiteSpace(request.Title))
                note.Title = request.Title;
            if (request.Content != null)
                note.Content = request.Content;
            note.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(new { note.Id, note.Title, note.Content, note.CreatedAt, note.UpdatedAt });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var note = await _db.Notes.FindAsync(id);
            if (note == null) return NotFound();
            var userId = GetUserId();
            if (userId.HasValue && note.UserId != userId.Value) return Forbid();
            _db.Notes.Remove(note);
            await _db.SaveChangesAsync();
            return Ok();
        }
    }

    public class NoteRequest
    {
        public string? Title { get; set; }
        public string? Content { get; set; }
    }
}
