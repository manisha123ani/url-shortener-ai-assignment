using Microsoft.EntityFrameworkCore;
using UrlShortener.Api.Models;

namespace UrlShortener.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ShortUrl> ShortUrls => Set<ShortUrl>();
    public DbSet<ClickEvent> ClickEvents => Set<ClickEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShortUrl>(e =>
        {
            e.HasIndex(x => x.ShortCode).IsUnique();
            e.HasIndex(x => x.IdempotencyKey);
            e.Property(x => x.ShortCode).HasMaxLength(16).IsRequired();
            e.Property(x => x.OriginalUrl).HasMaxLength(2048).IsRequired();
        });

        modelBuilder.Entity<ClickEvent>(e =>
        {
            e.HasIndex(x => new { x.ShortUrlId, x.ClickedAtUtc });
            e.HasOne(x => x.ShortUrl)
             .WithMany(s => s.ClickEvents)
             .HasForeignKey(x => x.ShortUrlId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
