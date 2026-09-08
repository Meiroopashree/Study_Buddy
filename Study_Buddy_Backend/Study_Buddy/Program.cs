using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Interfaces;
using StudyBuddy.Middleware;
using StudyBuddy.Repositories;
using StudyBuddy.Services;

var builder = WebApplication.CreateBuilder(args);

// =============================
// Database: SQL Server (local dev) or PostgreSQL (Render via DATABASE_URL)
// =============================
var databaseUrl = builder.Configuration["DATABASE_URL"];
var usePostgres = !string.IsNullOrWhiteSpace(databaseUrl);

if (usePostgres)
{
    builder.Services.AddDbContext<StudyBuddyContext>(options =>
        options.UseNpgsql(ParsePostgresUrl(databaseUrl!, builder.Configuration["PGSSL_MODE"])));
}
else
{
    builder.Services.AddDbContext<StudyBuddyContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
}

builder.Services.AddScoped<IMistralService, MistralService>();
builder.Services.AddScoped<IChatRepository, ChatRepository>();

builder.Services.AddControllers();
builder.Services.AddHttpClient();

// =============================
// CORS for React (Vercel origin in production, localhost:3000 in dev)
// =============================
var corsOrigin = builder.Configuration["CORS_ORIGIN"];
var frontendOrigin = !string.IsNullOrWhiteSpace(corsOrigin)
    ? corsOrigin.Trim().TrimEnd('/')
    : !builder.Environment.IsProduction()
        ? (builder.Configuration["FrontendUrl"] ?? "").Trim().TrimEnd('/')
        : "";

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (string.IsNullOrWhiteSpace(frontendOrigin))
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        else
            policy.WithOrigins(frontendOrigin).AllowAnyHeader().AllowAnyMethod();
    });
});

// =============================
// Swagger
// =============================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// =============================
// Bind the port Render provides (PORT env var)
// =============================
var port = builder.Configuration["PORT"];
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

if (!app.Environment.IsProduction() || builder.Configuration["SWAGGER"] == "true")
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseRateLimiting();
app.UseStaticFiles();
if (!app.Environment.IsProduction())
    app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// =============================
// On Render/PostgreSQL: ensure schema exists and seed built-in syllabi.
// =============================
if (usePostgres)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<StudyBuddyContext>();
    StartupSeeder.Initialize(db);
}

app.Run();

static string ParsePostgresUrl(string url, string? sslMode)
{
    var uri = new Uri(url);
    var userInfo = uri.UserInfo?.Split(':', 2);
    var username = userInfo?[0] ?? "";
    var password = userInfo?.Length > 1 ? userInfo[1] : "";
    var ssl = string.IsNullOrWhiteSpace(sslMode) ? "Require" : sslMode;
    return $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};"
        + $"Username={username};Password={password};SSL Mode={ssl};Trust Server Certificate=true;";
}