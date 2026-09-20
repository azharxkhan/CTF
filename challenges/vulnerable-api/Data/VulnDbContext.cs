using Microsoft.EntityFrameworkCore;

namespace VulnApi.Data;

public class VulnDbContext : DbContext
{
    public VulnDbContext(DbContextOptions<VulnDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Allocation> Allocations => Set<Allocation>();
    public DbSet<Flag> Flags => Set<Flag>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Product>().Property(p => p.Price).HasPrecision(10, 2);
        b.Entity<Invoice>().Property(i => i.Amount).HasPrecision(10, 2);
    }
}
