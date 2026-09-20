using System.Security.Cryptography;
using System.Text;

namespace VulnApi.Data;

// Deterministic seeder. All data is FAKE, and reproducible (explicit rows, no randomness) so
// a reset always restores exactly this. Scored flags are read from environment variables
// (FLAG_C*), never hardcoded — see .env.example / BUILD.md §6.
public static class Seeder
{
    // `challenge` selects which flag(s) get planted, so each challenge's OWN database holds
    // exactly one extractable secret (per-challenge isolation, docs/plan/C-challenges.md).
    // "c1" -> the C1 flag in the Flags table; "c2" -> the C2 flag in the admin's invoice.
    public static void Seed(VulnDbContext db, string challenge = "c1")
    {
        if (db.Users.Any()) return;   // already seeded

        // --- Users: predictable emails, all normal users share the password "hunter2" so
        // players can log in. The admin's password is a random value players do NOT know, so
        // they can't just log in as admin — challenges 5 (over-post) and 8 (forge token) are
        // the intended ways to get admin. ---
        const string userPassword = "hunter2";
        var users = new List<User>
        {
            new() { Email = "user1@corp.local", DisplayName = "Alex Rivera",  PasswordHash = Md5(userPassword), IsAdmin = false },
            new() { Email = "user2@corp.local", DisplayName = "Sam Okafor",   PasswordHash = Md5(userPassword), IsAdmin = false },
            new() { Email = "user3@corp.local", DisplayName = "Priya Nair",   PasswordHash = Md5(userPassword), IsAdmin = false },
            new() { Email = "user4@corp.local", DisplayName = "Jordan Blake", PasswordHash = Md5(userPassword), IsAdmin = false },
            new() { Email = "user5@corp.local", DisplayName = "Wei Zhang",    PasswordHash = Md5(userPassword), IsAdmin = false },
        };
        var admin = new User
        {
            Email = "admin@corp.local", DisplayName = "Admin",
            PasswordHash = Md5(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")),  // unknown to players
            IsAdmin = true
        };
        users.Add(admin);
        db.Users.AddRange(users);
        db.SaveChanges();   // assigns Ids

        // --- Products (SQLi search target) — realistic catalogue rows ---
        var products = new List<Product>
        {
            new() { Sku = "WID-0001", Name = "Widget",          Category = "Fasteners",  Price = 9.99m,   Stock = 120,  Description = "Standard 8mm zinc-plated widget." },
            new() { Sku = "WID-0002", Name = "Widget Pro",      Category = "Fasteners",  Price = 14.99m,  Stock = 60,   Description = "Hardened widget, higher shear rating." },
            new() { Sku = "GAD-0100", Name = "Gadget",          Category = "Assemblies", Price = 19.50m,  Stock = 80,   Description = "Multi-purpose gadget, boxed." },
            new() { Sku = "SPR-0210", Name = "Sprocket 12T",    Category = "Drivetrain", Price = 4.25m,   Stock = 500,  Description = "12-tooth steel sprocket." },
            new() { Sku = "SPR-0220", Name = "Sprocket 16T",    Category = "Drivetrain", Price = 5.10m,   Stock = 320,  Description = "16-tooth steel sprocket." },
            new() { Sku = "COG-0050", Name = "Cog",             Category = "Drivetrain", Price = 2.10m,   Stock = 1000, Description = "Generic replacement cog." },
            new() { Sku = "FLX-9000", Name = "Flux Capacitor",  Category = "Specialty",  Price = 999.00m, Stock = 3,    Description = "Handle with care. Requires 1.21 GW." },
            new() { Sku = "BRK-0303", Name = "Bracket",         Category = "Mounting",   Price = 6.75m,   Stock = 240,  Description = "L-bracket, powder-coated." },
            new() { Sku = "GRM-0007", Name = "Grommet",         Category = "Seals",      Price = 1.15m,   Stock = 1500, Description = "Rubber grommet, 10mm." },
            new() { Sku = "WSH-0011", Name = "Washer Pack",     Category = "Fasteners",  Price = 3.40m,   Stock = 880,  Description = "Assorted washers, 100pc." },
        };
        db.Products.AddRange(products);
        db.SaveChanges();

        // --- Invoices ---
        // Challenge 2 (IDOR): the admin's invoice holds the C2 flag in its Notes. It's only
        // planted when this DB is the C2 instance, so it can't leak into another challenge.
        var normal = users.Where(u => !u.IsAdmin).ToList();
        var adminNotes = challenge == "c2"
            ? $"CONFIDENTIAL — do not share: {Env("FLAG_C2_IDOR", "flag{missing_env_c2}")}"
            : "admin: quarterly summary";
        var invoices = new List<Invoice>
        {
            new() { OwnerId = normal[0].Id, Amount = 120.00m, Notes = "Q1 order" },
            new() { OwnerId = normal[1].Id, Amount = 45.50m,  Notes = "replacement parts" },
            new() { OwnerId = admin.Id,     Amount = 8800.00m, Notes = adminNotes },
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

        // --- Flags table: only the SQL-injection instances store a secret here ---
        // Each SQLi challenge uses its OWN database, so its UNION can't reach another's flag.
        if (challenge == "c1")
            db.Flags.Add(new Flag { Name = "search", Secret = Env("FLAG_C1_SQLI", "flag{missing_env_c1}") });
        if (challenge == "c4")
            db.Flags.Add(new Flag { Name = "orders_ref", Secret = Env("FLAG_C4_FROMSQLRAW", "flag{missing_env_c4}") });

        db.SaveChanges();
    }

    public static void Reseed(VulnDbContext db, string challenge = "c1")
    {
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        Seed(db, challenge);
    }

    private static string Env(string key, string fallback) =>
        Environment.GetEnvironmentVariable(key) is { Length: > 0 } v ? v : fallback;

    // Simple (deliberately weak) password hashing so login can verify credentials. Real apps
    // should use a slow salted hash (bcrypt/argon2); MD5 is fine for a throwaway lab account.
    public static string Md5(string s) =>
        "md5:" + Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
}
