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
            // A real-looking catalogue search (LIKE over 4 columns). The app wraps the input
            // in %...%' — so an injected ' leaves the trailing %' dangling (comment it with --).
            //   q = ' OR 1=1 --                                   -> every product
            //   q = ' UNION SELECT NULL, name, NULL, NULL FROM sys.tables --   -> enumerate tables
            //   q = ' UNION SELECT NULL, Secret, NULL, NULL FROM Flags --      -> exfiltrate
            var sql = "SELECT Sku, Name, Category, Price FROM Products WHERE Name LIKE '%" + q + "%'";

            var results = new List<object>();
            using var conn = new SqlConnection(cs);
            conn.Open();
            using var cmd = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new
                {
                    sku = reader.GetValue(0)?.ToString(),
                    name = reader.GetValue(1)?.ToString(),
                    category = reader.GetValue(2)?.ToString(),
                    price = reader.GetValue(3) is decimal d ? d : (decimal?)null
                });
            }
            return Results.Ok(results);
        });
    }
}
