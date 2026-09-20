using Microsoft.EntityFrameworkCore;
using VulnApi.Data;

namespace VulnApi.Endpoints;

// CHALLENGE 4 — SQL injection hidden inside EF Core's FromSqlRaw.
// This looks safe because it's EF Core, but FromSqlRaw($"...") interpolates the string in C#
// BEFORE EF sees it, so it is NOT parameterized — same bug as challenge 1, better disguised.
//
// !!! INTENTIONALLY VULNERABLE — teaching target. Secure version in Orders.patched.cs.txt !!!
public static class OrdersEndpoint
{
    public static void MapOrders(this WebApplication app)
    {
        // Look up orders by their reference code.
        app.MapGet("/api/orders/lookup", (string? @ref, VulnDbContext db) =>
        {
            var r = @ref ?? "";

            // VULNERABLE: FromSqlRaw with an INTERPOLATED string does not parameterize.
            //   ref = ' OR 1=1 --                                  -> all orders
            //   ref = ' UNION SELECT 1,1,1,1,Secret FROM Flags --  -> flag in the ref field
            var orders = db.Orders
                .FromSqlRaw($"SELECT * FROM Orders WHERE Ref = '{r}'")
                .ToList();

            return Results.Ok(orders.Select(o => new
            {
                o.Id, o.UserId, o.ProductId, o.Qty, o.Ref
            }));
        });
    }
}
