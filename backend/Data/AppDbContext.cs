using Microsoft.EntityFrameworkCore;
using CryptoTracker.Models;

namespace CryptoTracker.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<Holding> Holdings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasConversion(g => g.ToString(), s => Guid.Parse(s));
            entity.Property(e => e.Username).IsRequired().HasMaxLength(32);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasConversion(g => g.ToString(), s => Guid.Parse(s));

            entity.Property(e => e.UserId)
                .HasConversion(g => g.ToString(), s => Guid.Parse(s));

            entity.Property(e => e.Symbol)
                .IsRequired()
                .HasMaxLength(50);
            entity.Property(e => e.Type)
                .IsRequired()
                .HasMaxLength(20);
            entity.Property(e => e.BaseCurrency)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.Quantity)
                .HasPrecision(28, 18);
            entity.Property(e => e.PriceAtTransaction)
                .HasPrecision(28, 18);
            entity.Property(e => e.Fee)
                .HasPrecision(28, 18);
            entity.Property(e => e.Notes)
                .HasMaxLength(200);

            entity.HasIndex(e => e.UserId);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Holding>(entity =>
        {
            entity.Property(e => e.UserId)
                .HasConversion(g => g.ToString(), s => Guid.Parse(s));

            entity.HasKey(e => new { e.UserId, e.Symbol });
            entity.Property(e => e.Symbol).HasMaxLength(50);
            entity.Property(e => e.CurrentQuantity).HasPrecision(28, 18);
            entity.Property(e => e.Source).HasMaxLength(50);
            entity.Property(e => e.SellTargetUsd).HasPrecision(28, 18);
            entity.Property(e => e.BuyZoneUsd).HasPrecision(28, 18);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
