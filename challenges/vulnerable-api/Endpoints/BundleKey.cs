namespace VulnApi.Endpoints;

// CHALLENGE 12 — API key in the JS bundle.
// The admin dashboard ships an API key inside its client-side JavaScript. Anything sent to
// the browser is public: open the bundle, read the key, and use it against a privileged
// endpoint it should never have been able to reach.
//
// !!! INTENTIONALLY VULNERABLE — teaching target. Secure guidance in BundleKey.patched.cs.txt !!!
public static class BundleKeyEndpoint
{
    // Write the "built" dashboard with the admin key baked into app.js (models a Vite build
    // that embedded an env var). Served at /dashboard/.
    public static void EnsureFrontend(IWebHostEnvironment env, string adminKey)
    {
        var dir = Path.Combine(env.ContentRootPath, "wwwroot", "dashboard");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "index.html"),
            "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Acme Admin</title>" +
            "<style>body{font:15px system-ui;max-width:640px;margin:40px auto;padding:0 16px}" +
            "h1{font-size:1.2rem}li{padding:4px 0}</style></head><body>" +
            "<h1>Acme Inventory Dashboard</h1><ul id=\"inv\">loading…</ul>" +
            "<script src=\"app.js\"></script></body></html>");
        File.WriteAllText(Path.Combine(dir, "app.js"),
            "// Acme admin dashboard. Uses the platform API key to load data.\n" +
            $"const API_KEY = \"{adminKey}\";\n" +
            "async function load(){\n" +
            "  const r = await fetch('/api/inventory', { headers: { 'X-Api-Key': API_KEY } });\n" +
            "  const items = await r.json();\n" +
            "  document.getElementById('inv').innerHTML =\n" +
            "    items.map(i => `<li>${i.sku} — ${i.name} (${i.stock} in stock)</li>`).join('');\n" +
            "}\nload();\n");
    }

    public static void MapBundleKey(this WebApplication app, string adminKey, string flag)
    {
        // Normal endpoint the dashboard calls — needs the key.
        app.MapGet("/api/inventory", (HttpContext ctx, Data.VulnDbContext db) =>
        {
            if (ctx.Request.Headers["X-Api-Key"] != adminKey) return Results.Unauthorized();
            return Results.Ok(db.Products.Select(p => new { p.Sku, p.Name, p.Stock }).ToList());
        });

        // Privileged endpoint the *same* key can reach — it should have been server-side only.
        app.MapGet("/api/admin/export", (HttpContext ctx) =>
        {
            if (ctx.Request.Headers["X-Api-Key"] != adminKey) return Results.Unauthorized();
            return Results.Ok(new { export = "full customer + billing export", flag });
        });
    }
}
