namespace Highlights.Api.Services.Enrichment;

// This is what an enricher gives back to the worker after it thinks about a highlight.
public class HighlightEnrichmentResult
{
    // True when the enricher feels it produced something usable.
    public bool Success { get; set; }

    // Short, punchy title.
    public string? Title { get; set; }

    // One- or two-sentence explanation of what happened.
    public string? Summary { get; set; }

    // Optional: where a thumbnail might live (we can keep this null for now).
    public string? ThumbnailUrl { get; set; }

    // If something goes wrong, this is a friendly-ish reason for logs, not users.
    public string? FailureReason { get; set; }
}
