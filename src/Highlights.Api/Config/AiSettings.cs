namespace Highlights.Api.Config;

// Simple settings bag to control which enricher we use and how to call the "real" one.
public class AiSettings
{
    // When true (default), we stick to StubHighlightEnricher.
    // When false, we try to use RealLlmHighlightEnricher instead.
    public bool UseStubEnricher { get; set; } = true;

    // Where the real enrichment service lives (e.g. a small HTTP service that talks to an LLM).
    public string? EnrichmentEndpoint { get; set; }

    // Optional API key that we send to the enrichment endpoint (e.g. via header).
    public string? ApiKey { get; set; }
}
