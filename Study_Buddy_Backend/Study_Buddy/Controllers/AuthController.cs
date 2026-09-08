using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.DTOs;
using StudyBuddy.Models;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StudyBuddy.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly StudyBuddyContext _db;
        private readonly IConfiguration _config;
        private readonly HttpClient _http;

        public AuthController(StudyBuddyContext db, IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _db = db;
            _config = config;
            _http = httpClientFactory.CreateClient();
        }

        [HttpPost("signup")]
        public async Task<IActionResult> Signup([FromBody] SignupRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Email and password are required.");

            if (await _db.Users.AnyAsync(u => u.Email == request.Email))
                return Conflict("Email already registered.");

            var user = new User
            {
                Username = request.Username ?? request.Email.Split('@')[0],
                Email = request.Email,
                PasswordHash = HashPassword(request.Password),
                Token = Guid.NewGuid().ToString("N")
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                user.Id,
                user.Username,
                user.Email,
                user.Token,
                user.Provider
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Email and password are required.");

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null || user.PasswordHash != HashPassword(request.Password))
                return Unauthorized("Invalid email or password.");

            if (string.IsNullOrEmpty(user.Token))
                user.Token = Guid.NewGuid().ToString("N");
            await _db.SaveChangesAsync();

            return Ok(new
            {
                user.Id,
                user.Username,
                user.Email,
                user.Token,
                user.Provider
            });
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile([FromHeader] string authorization)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Token == authorization);
            if (user == null) return Unauthorized();

            var quizCount = await _db.QuizResults.CountAsync(q => q.UserId == user.Id);
            var totalScore = await _db.QuizResults.Where(q => q.UserId == user.Id).SumAsync(q => q.Score);
            var totalQuestions = await _db.QuizResults.Where(q => q.UserId == user.Id).SumAsync(q => q.TotalQuestions);
            var totalQuizTime = await _db.QuizResults.Where(q => q.UserId == user.Id).SumAsync(q => q.TimeSpentSeconds);
            var docCount = await _db.Documents.CountAsync(d => d.UserId == user.Id);
            var noteCount = await _db.Notes.CountAsync(n => n.UserId == user.Id);

            return Ok(new
            {
                user.Id,
                user.Username,
                user.Email,
                user.Provider,
                user.CreatedAt,
                Stats = new
                {
                    QuizzesTaken = quizCount,
                    TotalScore = totalScore,
                    TotalQuestions = totalQuestions,
                    TotalQuizTimeSec = totalQuizTime,
                    Documents = docCount,
                    Notes = noteCount,
                    Accuracy = totalQuestions > 0 ? Math.Round((double)totalScore / totalQuestions * 100, 1) : 0
                }
            });
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromHeader] string authorization, [FromBody] UpdateProfileRequest request)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Token == authorization);
            if (user == null) return Unauthorized();

            if (!string.IsNullOrWhiteSpace(request.Username))
                user.Username = request.Username.Trim();

            await _db.SaveChangesAsync();
            return Ok(new { user.Id, user.Username, user.Email, user.Provider });
        }

        [HttpGet("google")]
        public IActionResult GoogleLogin()
        {
            var clientId = _config["Google:ClientId"];
            if (string.IsNullOrWhiteSpace(clientId))
                return BadRequest("Google login is not configured. Set Google:ClientId and Google:ClientSecret in appsettings.json.");

            var redirectUri = GetGoogleRedirectUri();
            var state = Guid.NewGuid().ToString("N");
            Response.Cookies.Append("oauth_state", state, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax });

            var url = "https://accounts.google.com/o/oauth2/v2/auth"
                + $"?client_id={Uri.EscapeDataString(clientId)}"
                + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
                + "&response_type=code"
                + "&scope=openid%20email%20profile"
                + $"&state={state}";

            return Redirect(url);
        }

        [HttpGet("google/callback")]
        public async Task<IActionResult> GoogleCallback([FromQuery] string code, [FromQuery] string state)
        {
            var expectedState = Request.Cookies["oauth_state"];
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(expectedState) || state != expectedState)
                return BadRequest("Invalid Google login request.");

            var clientId = _config["Google:ClientId"];
            var clientSecret = _config["Google:ClientSecret"];
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                return BadRequest("Google login is not configured.");

            var redirectUri = GetGoogleRedirectUri();
            var tokenContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            });

            var tokenResponse = await _http.PostAsync("https://oauth2.googleapis.com/token", tokenContent);
            if (!tokenResponse.IsSuccessStatusCode)
                return BadRequest("Could not exchange Google authorization code.");

            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            var tokenDoc = JsonDocument.Parse(tokenJson);
            var accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString();

            var userReq = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
            userReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var userResponse = await _http.SendAsync(userReq);
            if (!userResponse.IsSuccessStatusCode)
                return BadRequest("Could not fetch Google profile.");

            var userJson = await userResponse.Content.ReadAsStringAsync();
            using var userDoc = JsonDocument.Parse(userJson);
            var root = userDoc.RootElement;
            var googleId = root.GetProperty("id").GetString();
            var email = root.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : "";
            var name = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;

            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Google account has no email address.");

            var user = await _db.Users.FirstOrDefaultAsync(u => u.GoogleId == googleId);
            if (user == null)
            {
                user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user != null)
                {
                    user.GoogleId = googleId;
                    user.Provider = "google";
                }
                else
                {
                    user = new User
                    {
                        Username = name ?? email.Split('@')[0],
                        Email = email,
                        GoogleId = googleId,
                        Provider = "google",
                        PasswordHash = "",
                        Token = Guid.NewGuid().ToString("N")
                    };
                    _db.Users.Add(user);
                }
            }

            if (string.IsNullOrEmpty(user.Token))
                user.Token = Guid.NewGuid().ToString("N");
            await _db.SaveChangesAsync();

            var frontendUrl = _config["FrontendUrl"] ?? "http://localhost:3000";
            var redirect = $"{frontendUrl}/auth?token={user.Token}&username={Uri.EscapeDataString(user.Username)}&email={Uri.EscapeDataString(user.Email)}";
            return Redirect(redirect);
        }

        private string GetGoogleRedirectUri()
        {
            return $"{Request.Scheme}://{Request.Host}/api/auth/google/callback";
        }

        private static string HashPassword(string password)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes).ToLower();
        }
    }
}
