using System;
using System.Threading;
using System.Threading.Tasks;
using Highlights.Api.Entities;

namespace Highlights.Api.Services.Enrichment;

// This is the "fake" enricher we can rely on in dev and tests.
// It just builds a predictable title + summary from the existing highlight fields.
public class StubHighlightEnricher : IHighlightEnricher
{
    public Task<HighlightEnrichmentResult> EnrichAsync(
        Highlight highlight,
        CancellationToken cancellationToken = default)
    {
        // Very small bit of defensive coding.
        var eventType = (highlight.EventType ?? string.Empty).Trim();
        var team = (highlight.Team ?? string.Empty).Trim();
        var player = (highlight.Player ?? string.Empty).Trim();
        var description = (highlight.Description ?? string.Empty).Trim();

        // Normalize the team label into something a person would expect to see.
        var teamLabel = team.ToLowerInvariant() switch
        {
            "home" => "Home",
            "away" => "Away",
            "" => "Unknown side",
            _ => team, // If it's something custom (like "Barcelona"), just echo it.
        };

        // Title: short and punchy, no magic, just stitched together.
        var title = $"{teamLabel} {eventType.ToUpperInvariant()} by {player}".Trim();

        // Summary: one friendly sentence that includes when, who, and what.
        // Keeping it deterministic and boring on purpose so tests are easy.
        var occurredAtLocal = highlight.OccurredAt.ToLocalTime();
        var timePart = occurredAtLocal.ToString("yyyy-MM-dd HH:mm");

        var summary =
            $"{player} recorded a {eventType.ToLowerInvariant()} for the {teamLabel.ToLowerInvariant()} side " +
            $"on {timePart}. Source says: \"{description}\"";

        var result = new HighlightEnrichmentResult
        {
            Success = true,
            Title = title,
            Summary = summary,
            ThumbnailUrl = null, 
            FailureReason = null
        };

        // No async work here, so we just wrap it up in a completed task.
        return Task.FromResult(result);
    }
}
