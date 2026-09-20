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

// Which challenge's data this instance holds (per-challenge DB isolation). Each challenge
// runs against its own database (Vuln_C1, Vuln_C2, …) so a flag can't leak across them.
var challenge = Environment.GetEnvironmentVariable("CTF_CHALLENGE") ?? "c1";

// Challenge 7 (path traversal) is file-based: lay down report files + a secret flag file.
if (challenge == "c7")
{
    var c7flag = Environment.GetEnvironmentVariable("FLAG_C7_PATHTRAVERSAL") ?? "flag{missing_env_c7}";
    ReportsEndpoint.EnsureData(app.Environment, c7flag);
}

// `dotnet run -- --reseed` drops, recreates, and reseeds, then exits. Used by the hourly
// reset / operator restore (docs/operator/command-reference.md).
if (args.Contains("--reseed"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VulnDbContext>();
    Seeder.Reseed(db, challenge);
    Console.WriteLine($"Reseeded ({challenge}).");
    return;
}

// On normal startup: ensure the schema exists and seed if empty.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<VulnDbContext>();
    db.Database.EnsureCreated();
    Seeder.Seed(db, challenge);
}

// Static challenge UIs (wwwroot): the catalogue search (index.html) and the invoices app.
app.UseDefaultFiles();
app.UseStaticFiles();

// Challenge endpoints
app.MapSearch();     // Challenge 1 — SQL injection
app.MapAuth();       // shared login (identity for authz challenges)
app.MapInvoices();   // Challenge 2 — IDOR
app.MapReports();    // Challenge 7 — path traversal
app.MapComments(Environment.GetEnvironmentVariable("FLAG_C6_XSS") ?? "flag{missing_env_c6}");  // Challenge 6 — stored XSS
app.MapOrders();     // Challenge 4 — FromSqlRaw interpolation
app.MapProfile(Environment.GetEnvironmentVariable("FLAG_C5_MASSASSIGN") ?? "flag{missing_env_c5}");  // Challenge 5 — mass assignment

app.Run();
