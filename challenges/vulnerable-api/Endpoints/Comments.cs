using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using VulnApi.Data;

namespace VulnApi.Endpoints;

// CHALLENGE 6 — Stored XSS.
// The React board renders comment bodies with dangerouslySetInnerHTML (the real vuln, in the
// frontend). These endpoints store/serve comments verbatim, simulate an admin whose browser
// holds a sensitive value, and provide an attacker "collector" the player reads.
//
// !!! INTENTIONALLY VULNERABLE — teaching target. Secure frontend in vulnerable-web. !!!
public static class CommentsEndpoint
{
    // Attacker's collection box (what the exfil payload posts to; the player reads the log).
    private static readonly ConcurrentQueue<string> Collected = new();

    public static void MapComments(this WebApplication app, string flag)
    {
        // Post a comment (anonymous board). Body is stored verbatim — no sanitisation.
        app.MapPost("/api/comments", async (CommentDto dto, VulnDbContext db) =>
        {
            var c = new Comment { AuthorId = 0, Body = dto.Body ?? "" };
            db.Comments.Add(c);
            await db.SaveChangesAsync();
            return Results.Ok(new { c.Id });
        });

        // List comments (bodies returned raw; the frontend is what renders them unsafely).
        app.MapGet("/api/comments", async (VulnDbContext db) =>
            Results.Ok(await db.Comments.OrderByDescending(c => c.Id)
                .Select(c => new { c.Id, c.Body }).ToListAsync()));

        // The "admin" bot hits this first: it establishes a session whose browser holds a
        // sensitive value in a NON-HttpOnly cookie (so JS — i.e. an XSS payload — can read it).
        app.MapGet("/api/admin/session", (HttpContext ctx) =>
        {
            ctx.Response.Cookies.Append("acme_admin_note", flag, new CookieOptions
            {
                HttpOnly = false,   // the flaw the XSS abuses: script-readable
                SameSite = SameSiteMode.Lax
            });
            return Results.Text("Admin session established. Reviewing new comments…");
        });

        // Attacker collector: the exfil payload calls this; the player reads /collect/log.
        app.MapGet("/collect", (string? d) =>
        {
            if (!string.IsNullOrEmpty(d)) Collected.Enqueue($"{DateTime.UtcNow:HH:mm:ss}  {d}");
            return Results.Text("ok");
        });
        app.MapGet("/collect/log", () => Results.Ok(Collected.ToArray()));
    }
}

public record CommentDto(string? Body);
