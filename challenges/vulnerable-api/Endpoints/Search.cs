using Microsoft.Data.SqlClient;

namespace VulnApi.Endpoints;

// CHALLENGE 1 — SQL injection via string concatenation.
// This is the BLUNT one (raw ADO.NET, obvious concatenation), distinct from challenge 4
// which hides the same bug inside EF Core's FromSqlRaw.
//
// !!! INTENTIONALLY VULNERABLE — this is a teaching target, never a pattern to copy. !!!
// The secure version lives in Search.patched.cs.txt (the reviewer's answer key).
public static class SearchEndpoint
{
    public static void MapSearch(this WebApplication app)
    {
        app.MapGet("/api/search", (string? q, IConfiguration cfg) =>
        {
            q ??= "";
            var cs = cfg.GetConnectionString("Default")
                     ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")!;

            // VULNERABLE: the caller's input is concatenated straight into the SQL text.
            //   q = ' OR 1=1 --                        -> returns every product
            //   q = ' UNION SELECT Id, Secret FROM Flags --   -> leaks the Flags table
            var sql = "SELECT Id, Name FROM Products WHERE Name = '" + q + "'";

            var results = new List<object>();
            using var conn = new SqlConnection(cs);
            conn.Open();
            using var cmd = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new { id = reader.GetValue(0), name = reader.GetValue(1)?.ToString() });
            }
            return Results.Ok(results);
        });
    }
}
