using Microsoft.Data.SqlClient;

namespace VulnApi.Endpoints;

// CHALLENGE 10 — Blind (time-based) SQL injection.
// The endpoint is injectable but returns the SAME response no matter what — no data comes
// back. You extract the secret one character at a time by making the DB pause (WAITFOR
// DELAY) when a guess is right, and reading the answer from the response time.
//
// !!! INTENTIONALLY VULNERABLE — teaching target. Secure version in Status.patched.cs.txt !!!
public static class StatusEndpoint
{
    public static void MapStatus(this WebApplication app)
    {
        // Order status check. Returns a generic 200 whether or not the ref exists — no data
        // leaks in the response, only in how long it takes.
        app.MapGet("/api/status", (string? @ref, IConfiguration cfg) =>
        {
            var r = @ref ?? "";
            var cs = cfg.GetConnectionString("Default")
                     ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")!;

            // VULNERABLE: concatenated into SQL and executed as a batch, so an injected
            //   '; IF (<condition>) WAITFOR DELAY '0:0:3' --
            // makes the response slow exactly when <condition> is true. Loop over positions
            // and characters of (SELECT TOP 1 Secret FROM Flags) to rebuild the flag.
            var sql = "SELECT Id FROM Orders WHERE Ref = '" + r + "'";
            using var conn = new SqlConnection(cs);
            conn.Open();
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 30 };
            cmd.ExecuteNonQuery();

            return Results.Ok(new { status = "checked" });   // identical every time (blind)
        });
    }
}
