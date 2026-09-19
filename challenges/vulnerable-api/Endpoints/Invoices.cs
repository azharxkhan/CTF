using Microsoft.EntityFrameworkCore;
using VulnApi.Data;

namespace VulnApi.Endpoints;

// CHALLENGE 2 — IDOR (Insecure Direct Object Reference).
//
// !!! INTENTIONALLY VULNERABLE — teaching target. Secure version in Invoices.patched.cs.txt !!!
public static class InvoicesEndpoint
{
    public static void MapInvoices(this WebApplication app)
    {
        // Your own invoices — realistic "My invoices" list. Correctly scoped to the caller.
        app.MapGet("/api/invoices", async (HttpContext ctx, VulnDbContext db) =>
        {
            var uid = Auth.CurrentUserId(ctx);
            if (uid is null) return Results.Unauthorized();
            var mine = await db.Invoices.Where(i => i.OwnerId == uid)
                .Select(i => new { i.Id, i.OwnerId, i.Amount, i.Notes })
                .ToListAsync();
            return Results.Ok(mine);
        });

        // Single invoice by id.
        // VULNERABLE: the caller is authenticated, but there is NO check that the invoice
        // actually belongs to them. Change the {id} and you read anyone's invoice.
        app.MapGet("/api/invoices/{id:int}", async (int id, HttpContext ctx, VulnDbContext db) =>
        {
            var uid = Auth.CurrentUserId(ctx);
            if (uid is null) return Results.Unauthorized();

            var invoice = await db.Invoices.FindAsync(id);
            if (invoice is null) return Results.NotFound();

            // <-- MISSING: if (invoice.OwnerId != uid) return Results.Forbid();
            return Results.Ok(new { invoice.Id, invoice.OwnerId, invoice.Amount, invoice.Notes });
        });
    }
}
