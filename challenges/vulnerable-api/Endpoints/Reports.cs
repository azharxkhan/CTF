namespace VulnApi.Endpoints;

// CHALLENGE 7 — Path traversal.
//
// !!! INTENTIONALLY VULNERABLE — teaching target. Secure version in Reports.patched.cs.txt !!!
public static class ReportsEndpoint
{
    private static string ReportsDir(IWebHostEnvironment env) =>
        Path.Combine(env.ContentRootPath, "challenge-data", "reports");

    // Lay down realistic report files, and a secret file OUTSIDE the reports folder that
    // traversal can reach. Flag content comes from the environment, never hardcoded.
    public static void EnsureData(IWebHostEnvironment env, string flag)
    {
        var baseDir = Path.Combine(env.ContentRootPath, "challenge-data");
        var reports = Path.Combine(baseDir, "reports");
        Directory.CreateDirectory(reports);
        File.WriteAllText(Path.Combine(reports, "2025-q1-summary.txt"),
            "Acme Parts — Q1 2025 sales summary\nRevenue: $482,100\nTop SKU: WID-0001\n");
        File.WriteAllText(Path.Combine(reports, "2025-q2-summary.txt"),
            "Acme Parts — Q2 2025 sales summary\nRevenue: $511,940\nTop SKU: SPR-0210\n");
        File.WriteAllText(Path.Combine(reports, "inventory-snapshot.txt"),
            "SKU,Qty\nWID-0001,120\nSPR-0210,500\nFLX-9000,3\n");
        // Sensitive file the download was never meant to serve — one level up from reports/.
        File.WriteAllText(Path.Combine(baseDir, "flag.txt"), flag + "\n");
    }

    public static void MapReports(this WebApplication app)
    {
        var env = app.Environment;

        // List the available reports (realistic file picker).
        app.MapGet("/api/reports", () =>
        {
            var dir = ReportsDir(env);
            var files = Directory.Exists(dir)
                ? Directory.GetFiles(dir).Select(Path.GetFileName).ToArray()
                : Array.Empty<string>();
            return Results.Ok(files);
        });

        // Download a report by filename.
        // VULNERABLE: the user-supplied name is combined onto the reports path with no
        // validation, so "../flag.txt" (or an absolute path) escapes the reports folder.
        app.MapGet("/api/reports/download", (string file) =>
        {
            var path = Path.Combine(ReportsDir(env), file);
            if (!File.Exists(path)) return Results.NotFound();
            return Results.File(File.ReadAllBytes(path), "application/octet-stream",
                Path.GetFileName(path));
        });
    }
}
