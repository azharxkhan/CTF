using Microsoft.EntityFrameworkCore;
using VulnApi.Data;

namespace VulnApi.Endpoints;

// CHALLENGE 11 — Race condition (TOCTOU).
// A limited item can be allocated up to Limit times. The endpoint reads the current count,
// checks it, then (after a gap) writes — with no locking or transaction. Fire many requests
// at once and they all read the same old count before any of them writes, so you allocate
// past the limit (double-spend).
//
// Per-player container is recommended in production (one player's exploit corrupts shared
// state) — see docs/plan. Reset by re-seeding (dotnet VulnApi.dll --reseed).
//
// !!! INTENTIONALLY VULNERABLE — teaching target. Secure version in Allocate.patched.cs.txt !!!
public static class AllocateEndpoint
{
    private const int Limit = 3;   // only 3 of the limited item exist

    public static void MapAllocate(this WebApplication app, string flag)
    {
        // Try to allocate one unit of the limited item.
        app.MapPost("/api/allocate", async (VulnDbContext db) =>
        {
            // 1) READ how many are already taken.
            var taken = await db.Allocations.CountAsync();

            // 2) CHECK — looks fine if we're under the limit.
            if (taken >= Limit)
                return Results.Ok(new { ok = false, allocated = taken, message = "Sold out." });

            // ...gap between check and write (no lock/transaction) — the race window.
            await Task.Delay(75);

            // 3) WRITE — record the allocation. Concurrent callers all get here.
            db.Allocations.Add(new Allocation { Ref = Guid.NewGuid().ToString("N")[..8] });
            await db.SaveChangesAsync();

            var total = await db.Allocations.CountAsync();
            // Over-allocated past the limit? That's the double-spend — reveal the flag.
            return Results.Ok(new
            {
                ok = true,
                allocated = total,
                oversold = total > Limit,
                flag = total > Limit ? flag : null
            });
        });

        // Current state (and the flag if the item was oversold).
        app.MapGet("/api/allocate/status", async (VulnDbContext db) =>
        {
            var total = await db.Allocations.CountAsync();
            return Results.Ok(new
            {
                limit = Limit,
                allocated = total,
                oversold = total > Limit,
                flag = total > Limit ? flag : null
            });
        });
    }
}
