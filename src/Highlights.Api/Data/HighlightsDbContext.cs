using Highlights.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Highlights.Api.Data;

// This DbContext is the EF "gateway" to our Postgres highlights database.
// Any queries/updates to highlights will go through here.
public class HighlightsDbContext : DbContext
{
    public HighlightsDbContext(DbContextOptions<HighlightsDbContext> options)
        : base(options)
    {
    }

    // This is the main entry point for querying highlights:
    // db.Highlights.Where(...), db.Highlights.Add(...), etc.
    public DbSet<Highlight> Highlights => Set<Highlight>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var highlight = modelBuilder.Entity<Highlight>();

        // Keep table/column names snake_case to feel at home in Postgres.
        highlight.ToTable("highlights");

        highlight.HasKey(h => h.Id);

        highlight.Property(h => h.Id)
            .HasColumnName("id");

        highlight.Property(h => h.MatchId)
            .HasColumnName("match_id")
            .IsRequired();

        highlight.Property(h => h.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        highlight.Property(h => h.EventType)
            .HasColumnName("event_type")
            .IsRequired()
            .HasMaxLength(64);

        highlight.Property(h => h.Team)
            .HasColumnName("team")
            .IsRequired()
            .HasMaxLength(32);

        highlight.Property(h => h.Player)
            .HasColumnName("player")
            .IsRequired()
            .HasMaxLength(128);

        highlight.Property(h => h.Description)
            .HasColumnName("description")
            .IsRequired()
            .HasMaxLength(1024);

        highlight.Property(h => h.Title)
            .HasColumnName("title")
            .HasMaxLength(256);

        highlight.Property(h => h.Summary)
            .HasColumnName("summary")
            .HasMaxLength(2048);

        highlight.Property(h => h.ThumbnailUrl)
            .HasColumnName("thumbnail_url")
            .HasMaxLength(1024);

        highlight.Property(h => h.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        highlight.Property(h => h.UpdatedAt)
            .HasColumnName("updated_at");

        // A few handy indexes so queries later don’t have to suffer.
        highlight.HasIndex(h => h.MatchId);
        highlight.HasIndex(h => new { h.MatchId, h.EventType });
        highlight.HasIndex(h => h.OccurredAt);
    }
}
