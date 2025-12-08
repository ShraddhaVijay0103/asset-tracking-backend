using Microsoft.EntityFrameworkCore;
using AssetTracking.Rfid.Infrastructure.Persistence;
using AssetTracking.Rfid.Domain.Entities;

namespace AssetTracking.Rfid.Api.Middleware;

public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public AuditLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext ctx, AppDbContext db)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var user = ctx.User?.Identity?.Name ?? "anonymous";
        var path = ctx.Request.Path.ToString();

        await _next(ctx);

        if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            var module = "api";
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 2)
            {
                module = segments[1];
            }

            db.AuditLogs.Add(new AuditLog
            {
                AuditLogId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                UserName = user,
                Action = $"{ctx.Request.Method} {path}",
                Module = module,
                IpAddress = ip
            });

            await db.SaveChangesAsync();
        }
    }
}
