using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using VulnApi.Data;

namespace VulnApi.Endpoints;

// CHALLENGES 8 & 9 — JWT.
//   C8: tokens are HS256-signed with a short, guessable secret. Crack it offline, forge an
//       admin token. (Validation is otherwise correct — the key is the flaw.)
//   C9: the signing key is strong, but ValidateIssuer/Audience/Lifetime are turned OFF, so a
//       token minted for a DIFFERENT audience (the "partner" endpoint) is wrongly accepted.
//
// Red herring (do NOT implement): alg:none. ASP.NET Core's bearer middleware rejects it by
// default; a repo comment mentions it only to test whether players know it doesn't apply here.
//
// !!! INTENTIONALLY VULNERABLE — teaching target. Secure config in Jwt.patched.cs.txt !!!
public static class JwtChallenge
{
    public const string Issuer = "acme-api";
    public const string Audience = "acme-clients";

    // C8: a weak, guessable secret. .NET's IdentityModel requires HS256 keys to be >= 256
    // bits (32 bytes) — so it's a long but low-entropy passphrase (present in the challenge
    // wordlist), which jwt_tool cracks offline. That length requirement is itself the lesson:
    // "secret123" wouldn't even run here; the weakness is low entropy, not short length.
    private static string WeakKey() =>
        Environment.GetEnvironmentVariable("JWT_WEAK_KEY") ?? "weak-development-jwt-signing-key-please1";
    private static string StrongKey() =>
        Environment.GetEnvironmentVariable("JWT_STRONG_KEY")
        ?? "b8f2c1a9d4e7f0362b5a8c1d9e4f7a0362b5a8c1d9e4f7a0362b5a8c1d9e4f7a0";           // C9: 256-bit, secret

    public static void AddAuth(WebApplicationBuilder builder, string challenge)
    {
        var key = challenge == "c8" ? WeakKey() : StrongKey();
        var full = challenge == "c8";   // C8 validates everything; C9 turns checks OFF (the bug)

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    ValidateIssuer = full,
                    ValidIssuer = Issuer,
                    ValidateAudience = full,
                    ValidAudience = Audience,
                    ValidateLifetime = full,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    RoleClaimType = "role",
                };
            });
        builder.Services.AddAuthorization();
    }

    private static string Issue(string key, string issuer, string audience, int userId, string role, TimeSpan life)
    {
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience,
            claims: new[] { new Claim("sub", userId.ToString()), new Claim("role", role) },
            expires: DateTime.UtcNow.Add(life), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static void MapJwt(this WebApplication app, string challenge, string flag)
    {
        var key = challenge == "c8" ? WeakKey() : StrongKey();

        // Normal login: verifies the password and issues a token with the caller's real role.
        app.MapPost("/api/token/login", async (LoginDto dto, VulnDbContext db) =>
        {
            var u = await db.Users.FirstOrDefaultAsync(x => x.Email == dto.Email);
            if (u is null || !Auth.CheckPassword(u, dto.Password)) return Results.Unauthorized();
            var role = u.IsAdmin ? "admin" : "user";
            return Results.Ok(new { token = Issue(key, Issuer, Audience, u.Id, role, TimeSpan.FromMinutes(30)) });
        });

        // C9 only: a "partner" endpoint that mints an ADMIN token for a DIFFERENT issuer and
        // audience — intended for the HR system, NOT this API. The main API should reject it,
        // but with issuer/audience validation off it doesn't.
        if (challenge == "c9")
        {
            app.MapGet("/api/partner/token", () => Results.Ok(new
            {
                token = Issue(key, "legacy-hr", "hr-system", 999, "admin", TimeSpan.FromMinutes(30)),
                note = "For the HR integration only. Not valid for the main API."
            }));
        }

        // Admin-only data — returns the flag to a valid token with role=admin.
        app.MapGet("/api/admin/data", (HttpContext ctx) =>
        {
            if (ctx.User?.Identity?.IsAuthenticated != true) return Results.Unauthorized();
            var role = ctx.User.FindFirst("role")?.Value ?? ctx.User.FindFirst(ClaimTypes.Role)?.Value;
            if (role != "admin") return Results.StatusCode(403);
            return Results.Ok(new { flag });
        }).RequireAuthorization();
    }
}
