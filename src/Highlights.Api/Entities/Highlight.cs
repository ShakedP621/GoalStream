namespace Highlights.Api.Entities;

// This is the core "highlight" row that will live in Postgres.
// We can evolve it later, but this should cover the basics.
public class Highlight
{
    // Database identity for this highlight – Guid primary key.
    public Guid Id { get; set; }

    // Which match this belongs to (coming from events-api messages).
    public Guid MatchId { get; set; }

    // When the event actually happened in the match timeline.
    public DateTimeOffset OccurredAt { get; set; }

    // Raw event info we get from the events stream.
    public string EventType { get; set; } = default!; // e.g. "goal", "red_card"
    public string Team { get; set; } = default!;      // e.g. "home" / "away"
    public string Player { get; set; } = default!;    // simple player name for now

    public string Description { get; set; } = default!; // free-form description from the event

    // Simple lifecycle status for this highlight. We'll start with "PENDING_AI"
    // and let the enrichment worker move it to more interesting states later.
    public string Status { get; set; } = HighlightStatus.PendingAi;

    // Fields we expect the enrichment worker to fill or polish later.
    public string? Title { get; set; }        // e.g. "Last-minute screamer"
    public string? Summary { get; set; }      // short human-friendly explanation
    public string? ThumbnailUrl { get; set; } // where a preview image might live

    // Basic bookkeeping.
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
