using System;

namespace Highlights.Api.Messaging;

// This DTO is our view of the event that Events.Api publishes into Kafka.
// It should mirror the JSON shape on the topic `match-events`.
public class MatchEventMessage
{
    // This ties the event back to the match.
    public Guid MatchId { get; set; }

    // When in the match clock this happened.
    public DateTimeOffset OccurredAt { get; set; }

    // Example: "goal", "foul", etc. We'll care about "goal" for now.
    public string EventType { get; set; } = string.Empty;

    // Which team this event belongs to (e.g. "home" / "away").
    public string Team { get; set; } = string.Empty;

    // Who did the thing (usually the scorer).
    public string Player { get; set; } = string.Empty;

    // Human-friendly description of what happened.
    public string Description { get; set; } = string.Empty;
}
