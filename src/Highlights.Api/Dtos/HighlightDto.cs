using System;
using Highlights.Api.Entities;

namespace Highlights.Api.Dtos;

// This is the "public face" of a highlight that the outside world sees.
// It mirrors the entity for now, but keeps us free to evolve the DB separately.
public class HighlightDto
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }

    public string EventType { get; set; } = default!;
    public string Team { get; set; } = default!;
    public string Player { get; set; } = default!;
    public string Description { get; set; } = default!;

    // Simple lifecycle status for this highlight (e.g. PENDING_AI, READY, FAILED_AI).
    public string Status { get; set; } = default!;

    // These are the "nice to have" AI polish fields.
    public string? Title { get; set; }
    public string? Summary { get; set; }
    public string? ThumbnailUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

// Tiny helper so mapping logic stays in one cozy place.
public static class HighlightMappings
{
    // Maps the EF entity to the DTO we actually expose on the wire.
    public static HighlightDto ToDto(this Highlight entity) =>
        new()
        {
            Id = entity.Id,
            MatchId = entity.MatchId,
            OccurredAt = entity.OccurredAt,
            EventType = entity.EventType,
            Team = entity.Team,
            Player = entity.Player,
            Description = entity.Description,
            Status = entity.Status,
            Title = entity.Title,
            Summary = entity.Summary,
            ThumbnailUrl = entity.ThumbnailUrl,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
}