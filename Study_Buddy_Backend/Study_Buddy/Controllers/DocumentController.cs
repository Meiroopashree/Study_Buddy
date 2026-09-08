using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Interfaces;
using StudyBuddy.Models;
using System.Text;
using UglyToad.PdfPig;
using Document = StudyBuddy.Models.Document;

namespace StudyBuddy.Controllers
{
    [ApiController]
    [Route("api/documents")]
    public class DocumentController : ControllerBase
    {
        private readonly StudyBuddyContext _db;
        private readonly IMistralService _aiService;

        public DocumentController(StudyBuddyContext db, IMistralService aiService)
        {
            _db = db;
            _aiService = aiService;
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
            var query = _db.Documents.AsQueryable();
            if (userId.HasValue)
                query = query.Where(d => d.UserId == userId.Value);
            var docs = await query
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => new { d.Id, d.Title, d.UploadedAt })
                .ToListAsync();
            return Ok(docs);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var doc = await _db.Documents.FindAsync(id);
            if (doc == null) return NotFound();
            var userId = GetUserId();
            if (userId.HasValue && doc.UserId != userId.Value) return Forbid();
            return Ok(doc);
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file provided.");

            var isPdf = file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
            var isDocx = file.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase);
            string content;

            if (isPdf)
            {
                try
                {
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    ms.Position = 0;

                    var sb = new StringBuilder();
                    using (var document = PdfDocument.Open(ms))
                    {
                        foreach (var page in document.GetPages())
                        {
                            sb.AppendLine(page.Text);
                        }
                    }
                    content = sb.ToString().Trim();
                }
                catch (Exception)
                {
                    return BadRequest("Could not read this PDF. It may be password-protected or corrupted.");
                }
            }
            else if (isDocx)
            {
                try
                {
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    ms.Position = 0;

                    var sb = new StringBuilder();
                    using (var wordDoc = WordprocessingDocument.Open(ms, false))
                    {
                        var body = wordDoc.MainDocumentPart?.Document?.Body;
                        if (body != null)
                        {
                            foreach (var para in body.Elements<Paragraph>())
                            {
                                sb.AppendLine(para.InnerText);
                            }
                        }
                        else
                        {
                            sb.AppendLine("(No body content found in this DOCX.)");
                        }
                    }
                    content = sb.ToString().Trim();
                }
                catch (Exception)
                {
                    return BadRequest("Could not read this DOCX file. It may be corrupted.");
                }
            }
            else
            {
                using var reader = new StreamReader(file.OpenReadStream());
                content = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(content))
                return BadRequest("No readable text found in this file.");

            var doc = new Document
            {
                Title = Path.GetFileNameWithoutExtension(file.FileName),
                Content = content,
                UserId = GetUserId()
            };

            _db.Documents.Add(doc);
            await _db.SaveChangesAsync();
            await LogActivity("upload");

            return Ok(new { doc.Id, doc.Title });
        }

        [HttpPost("ocr")]
        public async Task<IActionResult> Ocr(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file provided.");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var mime = ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/png"
            };

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var dataUri = $"data:{mime};base64,{Convert.ToBase64String(ms.ToArray())}";

            string transcribed;
            try
            {
                transcribed = await _aiService.TranscribeImage(dataUri);
            }
            catch (Exception)
            {
                return StatusCode(502, "OCR failed. The AI service could not read this image.");
            }

            if (string.IsNullOrWhiteSpace(transcribed))
                return BadRequest("No text detected in this image.");

            var doc = new Document
            {
                Title = Path.GetFileNameWithoutExtension(file.FileName),
                Content = transcribed,
                UserId = GetUserId()
            };

            _db.Documents.Add(doc);
            await _db.SaveChangesAsync();
            await LogActivity("upload");

            return Ok(new { doc.Id, doc.Title });
        }

        [HttpPost("upload-text")]
        public async Task<IActionResult> UploadText([FromBody] UploadTextRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest("Content is required.");

            var doc = new Document
            {
                Title = request.Title ?? "Untitled",
                Content = request.Content,
                UserId = GetUserId()
            };

            _db.Documents.Add(doc);
            await _db.SaveChangesAsync();
            await LogActivity("upload");

            return Ok(new { doc.Id, doc.Title });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var doc = await _db.Documents.FindAsync(id);
            if (doc == null) return NotFound();
            var userId = GetUserId();
            if (userId.HasValue && doc.UserId != userId.Value) return Forbid();
            _db.Documents.Remove(doc);
            await _db.SaveChangesAsync();
            return Ok();
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

    public class UploadTextRequest
    {
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
    }
}
