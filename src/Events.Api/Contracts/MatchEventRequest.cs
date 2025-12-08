namespace Events.Api.Contracts;

// This is the shape of the JSON payload clients will POST to /events.
public sealed class MatchEventRequest
{
    // Who/what this event belongs to (match, game, fixture, etc.)
    public Guid MatchId { get; set; }

    // When the event actually happened in the real world.
    public DateTimeOffset OccurredAt { get; set; }

    // A short, machine-friendly type like "goal", "foul", "yellow_card", etc.
    public string EventType { get; set; } = string.Empty;

    // Optional team identifier (e.g. "home", "away", or a team slug/code).
    public string? Team { get; set; }

    // Optional player name or identifier.
    public string? Player { get; set; }

    // Free-text description.
    public string? Description { get; set; }
}
