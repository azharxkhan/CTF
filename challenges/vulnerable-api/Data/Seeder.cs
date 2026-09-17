using Bogus;

namespace VulnApi.Data;

// Deterministic seeder. All data is FAKE (Bogus with a fixed seed). Scored flags are read
// from environment variables (FLAG_C*), never hardcoded — see .env.example / BUILD.md §6.
public static class Seeder
{
    private const int SeedValue = 1337;   // fixed -> a reset reproduces the same data every time

    public static void Seed(VulnDbContext db)
    {
        if (db.Users.Any()) return;   // already seeded
        Randomizer.Seed = new Random(SeedValue);

        // --- Users: fake volume + one known admin (Id will be assigned by the DB) ---
        var userFaker = new Faker<User>()
            .UseSeed(SeedValue)
            .RuleFor(u => u.Email, f => f.Internet.Email(provider: "corp.local"))
            .RuleFor(u => u.DisplayName, f => f.Name.FullName())
            .RuleFor(u => u.PasswordHash, f => "md5:" + f.Random.Hexadecimal(32, prefix: ""))
            .RuleFor(u => u.IsAdmin, _ => false);

        var users = userFaker.Generate(8);
        var admin = new User
        {
            Email = "admin@corp.local",
            DisplayName = "Admin",
            PasswordHash = "md5:21232f297a57a5a743894a0e4a801fc3",
            IsAdmin = true
        };
        users.Add(admin);
        db.Users.AddRange(users);
        db.SaveChanges();   // assigns Ids

        // --- Products (SQLi search target) ---
        var products = new List<Product>
        {
            new() { Name = "Widget",         Price = 9.99m,   Stock = 120 },
            new() { Name = "Gadget",         Price = 19.50m,  Stock = 80 },
            new() { Name = "Sprocket",       Price = 4.25m,   Stock = 500 },
            new() { Name = "Cog",            Price = 2.10m,   Stock = 1000 },
            new() { Name = "Flux Capacitor", Price = 999.00m, Stock = 3 },
            new() { Name = "Bracket",        Price = 6.75m,   Stock = 240 },
            new() { Name = "Grommet",        Price = 1.15m,   Stock = 1500 },
            new() { Name = "Widget Pro",     Price = 14.99m,  Stock = 60 },
        };
        db.Products.AddRange(products);
        db.SaveChanges();

        // --- Invoices (benign notes) ---
        // NOTE: the challenge-2 (IDOR) flag is deliberately NOT seeded here. Because
        // challenge-1's SQL injection can `UNION SELECT ... FROM Invoices`, any flag in this
        // database is extractable by C1 and would leak across challenges. See the
        // per-challenge-database isolation decision in docs/plan/C-challenges.md. C2 gets its
        // own isolated database when it's built.
        var normal = users.Where(u => !u.IsAdmin).ToList();
        var invoices = new List<Invoice>
        {
            new() { OwnerId = normal[0].Id, Amount = 120.00m, Notes = "Q1 order" },
            new() { OwnerId = normal[1].Id, Amount = 45.50m,  Notes = "replacement parts" },
            new() { OwnerId = admin.Id,     Amount = 8800.00m, Notes = "admin: quarterly summary" },
            new() { OwnerId = normal[2].Id, Amount = 15.00m,  Notes = "sample" },
            new() { OwnerId = normal[0].Id, Amount = 210.75m, Notes = "bulk widgets" },
        };
        db.Invoices.AddRange(invoices);

        // --- Orders (challenge 4 FromSqlRaw target on Ref) ---
        db.Orders.AddRange(
            new Order { UserId = normal[0].Id, ProductId = products[0].Id, Qty = 10, Ref = "ORD-1001" },
            new Order { UserId = normal[1].Id, ProductId = products[1].Id, Qty = 2,  Ref = "ORD-1002" },
            new Order { UserId = normal[2].Id, ProductId = products[2].Id, Qty = 50, Ref = "ORD-1003" });

        // --- Comments (challenge 6 stored XSS; seeded benign) ---
        db.Comments.AddRange(
            new Comment { AuthorId = normal[0].Id, Body = "Great product, fast shipping." },
            new Comment { AuthorId = normal[1].Id, Body = "Does this come in blue?" });

        // --- Flags reachable via SQL injection — ONLY challenge 1 lives in this database ---
        // Challenges 4 and 10 (also SQLi) must use their OWN databases, or C1's UNION would
        // dump their flags too. This DB therefore contains exactly one extractable secret.
        db.Flags.Add(
            new Flag { Name = "search", Secret = Env("FLAG_C1_SQLI", "flag{missing_env_c1}") });

        db.SaveChanges();
    }

    public static void Reseed(VulnDbContext db)
    {
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        Seed(db);
    }

    private static string Env(string key, string fallback) =>
        Environment.GetEnvironmentVariable(key) is { Length: > 0 } v ? v : fallback;
}
