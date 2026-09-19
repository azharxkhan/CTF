using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using VulnApi.Data;

namespace VulnApi.Endpoints;

// Minimal session auth so challenges have a "current user". This is NOT the vulnerability
// under test (challenge 2 is about missing authorization, not authentication) — it's just
// enough of a login to have an identity. Tokens are opaque and map to a user id in memory.
public static class Auth
{
    private static readonly ConcurrentDictionary<string, int> Tokens = new();

    public static void MapAuth(this WebApplication app)
    {
        // POST /api/login { "email": "...", "password": "..." }
        // Simplified: any password is accepted for a known email (labs don't test the login).
        app.MapPost("/api/login", async (LoginDto dto, VulnDbContext db) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user is null) return Results.Unauthorized();
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            Tokens[token] = user.Id;
            return Results.Ok(new { token, userId = user.Id, displayName = user.DisplayName });
        });
    }

    // Resolve the caller's user id from the Authorization: Bearer <token> header.
    public static int? CurrentUserId(HttpContext ctx)
    {
        var h = ctx.Request.Headers.Authorization.ToString();
        const string p = "Bearer ";
        if (!h.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return null;
        return Tokens.TryGetValue(h[p.Length..].Trim(), out var id) ? id : null;
    }
}

public record LoginDto(string Email, string Password);
