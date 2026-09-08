using Microsoft.AspNetCore.Mvc;

namespace StudyBuddy.Controllers
{
    [ApiController]
    [Route("api/upload")]
    public class UploadController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public UploadController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpPost("image")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided." });

            try
            {
                var uploadsDir = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "uploads");
                Directory.CreateDirectory(uploadsDir);

                var ext = Path.GetExtension(file.FileName);
                if (string.IsNullOrEmpty(ext)) ext = ".jpg";
                var fileName = $"{Guid.NewGuid():N}{ext}";
                var filePath = Path.Combine(uploadsDir, fileName);

                // Read file bytes first
                await using var memStream = new MemoryStream();
                await file.CopyToAsync(memStream);
                var bytes = memStream.ToArray();

                // Save to disk
                await System.IO.File.WriteAllBytesAsync(filePath, bytes);

                var fileUrl = $"{Request.Scheme}://{Request.Host}/uploads/{fileName}";

                // Create base64 data URI for Pixtral vision model (requires https or data URI)
                var base64 = Convert.ToBase64String(bytes);
                var mimeType = ext.ToLower() switch
                {
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    ".bmp" => "image/bmp",
                    _ => "image/png"
                };
                var dataUri = $"data:{mimeType};base64,{base64}";

                return Ok(new { url = dataUri, fileUrl, fileName });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Failed to save file: {ex.Message}" });
            }
        }
    }
}
