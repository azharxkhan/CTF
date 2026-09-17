using Microsoft.EntityFrameworkCore;
using VulnApi.Data;
using VulnApi.Endpoints;

// Minimal host for now: wires the DbContext and seeds the database. Challenge endpoints
// (1,2,4,5,7,8,9,10) will be added under Endpoints/ as they're built.

var builder = WebApplication.CreateBuilder(args);

var conn = builder.Configuration.GetConnectionString("Default")
           ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
           ?? "Server=vuln-db;Database=Vuln;User Id=sa;Password=Change-Me-Strong-1;TrustServerCertificate=true";

builder.Services.AddDbContext<VulnDbContext>(o => o.UseSqlServer(conn));

var app = builder.Build();

// `dotnet run -- --reseed` drops, recreates, and reseeds, then exits. Used by the hourly
// reset / operator restore (docs/operator/command-reference.md).
if (args.Contains("--reseed"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VulnDbContext>();
    Seeder.Reseed(db);
    Console.WriteLine("Reseeded.");
    return;
}

// On normal startup: ensure the schema exists and seed if empty.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VulnDbContext>();
    db.Database.EnsureCreated();
    Seeder.Seed(db);
}

// Serve the browser search bar (wwwroot/index.html) so players can inject in the browser,
// with curl / sqlmap as the harder alternatives against the same /api/search endpoint.
app.UseDefaultFiles();
app.UseStaticFiles();

// Challenge endpoints
app.MapSearch();   // Challenge 1 — SQL injection

app.Run();
