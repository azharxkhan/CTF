using Microsoft.EntityFrameworkCore;
using VulnApi.Data;

namespace VulnApi.Endpoints;

// CHALLENGE 5 — Mass assignment / over-posting.
// The update endpoint binds the request body straight onto the User entity, which has an
// IsAdmin property. A user can POST "isAdmin": true and grant themselves admin.
//
// !!! INTENTIONALLY VULNERABLE — teaching target. Secure version in Profile.patched.cs.txt !!!
public static class ProfileEndpoint
{
    public static void MapProfile(this WebApplication app, string flag)
    {
        // View my profile.
        app.MapGet("/api/profile", async (HttpContext ctx, VulnDbContext db) =>
        {
            var uid = Auth.CurrentUserId(ctx);
            if (uid is null) return Results.Unauthorized();
            var u = await db.Users.FindAsync(uid);
            return u is null ? Results.NotFound()
                : Results.Ok(new { u.Id, u.Email, u.DisplayName, u.IsAdmin });
        });

        // Update my profile.
        // VULNERABLE: every field on the incoming body is copied onto the entity — including
        // IsAdmin, which the UI never exposes. Over-posting "isAdmin": true makes you admin.
        app.MapPut("/api/profile", async (User update, HttpContext ctx, VulnDbContext db) =>
        {
            var uid = Auth.CurrentUserId(ctx);
            if (uid is null) return Results.Unauthorized();
            var u = await db.Users.FindAsync(uid);
            if (u is null) return Results.NotFound();

            u.DisplayName = update.DisplayName;
            u.Email = update.Email;
            u.IsAdmin = update.IsAdmin;    // <-- the bug: trusting a client-supplied field
            await db.SaveChangesAsync();
            return Results.Ok(new { u.Id, u.Email, u.DisplayName, u.IsAdmin });
        });

        // Admin-only panel — reveals the flag to an admin.
        app.MapGet("/api/admin/panel", async (HttpContext ctx, VulnDbContext db) =>
        {
            var uid = Auth.CurrentUserId(ctx);
            if (uid is null) return Results.Unauthorized();
            var u = await db.Users.FindAsync(uid);
            if (u is null || !u.IsAdmin) return Results.StatusCode(403);  // no auth scheme registered; Forbid() would 500
            return Results.Ok(new { message = "Welcome, admin.", flag });
        });
    }
}
