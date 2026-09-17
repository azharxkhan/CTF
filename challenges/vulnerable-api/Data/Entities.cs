namespace VulnApi.Data;

// Entities mirror challenges/sql-playground/schema.sql. Keep the two in sync.
// NOTE: these are the *vulnerable app's* entities. Some carry properties that challenges
// deliberately abuse (e.g. User.IsAdmin for mass assignment, challenge 5).

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";   // fake hashes, never real credentials
    public bool IsAdmin { get; set; }                 // challenge 5 target (over-posting)
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

public class Invoice
{
    public int Id { get; set; }
    public int OwnerId { get; set; }                  // challenge 2 (IDOR) — ownership check missing
    public decimal Amount { get; set; }
    public string? Notes { get; set; }                // an admin invoice's Notes holds the C2 flag
}

public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ProductId { get; set; }
    public int Qty { get; set; }
    public string Ref { get; set; } = "";             // challenge 4 (FromSqlRaw interpolation) target
}

public class Comment
{
    public int Id { get; set; }
    public int AuthorId { get; set; }
    public string Body { get; set; } = "";            // challenge 6 (stored XSS) — rendered raw in the web app
}

// Holds the scored flags reachable via SQL (challenges 1, 4, 10). Values come from
// environment variables at seed time — never hardcoded, never baked into the image.
public class Flag
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Secret { get; set; } = "";
}
