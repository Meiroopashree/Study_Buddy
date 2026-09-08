using System.Collections.Concurrent;

namespace StudyBuddy.Middleware
{
    public class RateLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ConcurrentDictionary<string, RateLimitEntry> _clients = new();
        private readonly int _maxRequests;
        private readonly TimeSpan _window = TimeSpan.FromMinutes(1);

        public RateLimitMiddleware(RequestDelegate next, IConfiguration config)
        {
            _next = next;
            _maxRequests = config.GetValue("RateLimiting:MaxRequests", 200);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var now = DateTime.UtcNow;

            var entry = _clients.GetOrAdd(key, new RateLimitEntry { Count = 0, WindowStart = now });

            lock (entry)
            {
                if (now - entry.WindowStart > _window)
                {
                    entry.Count = 1;
                    entry.WindowStart = now;
                }
                else
                {
                    entry.Count++;
                }

                if (entry.Count > _maxRequests)
                {
                    context.Response.StatusCode = 429;
                    context.Response.Headers["Retry-After"] = _window.TotalSeconds.ToString();
                    return;
                }
            }

            await _next(context);
        }

        private class RateLimitEntry
        {
            public int Count { get; set; }
            public DateTime WindowStart { get; set; }
        }
    }

    public static class RateLimitExtensions
    {
        public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
        {
            return app.UseMiddleware<RateLimitMiddleware>();
        }
    }
}
