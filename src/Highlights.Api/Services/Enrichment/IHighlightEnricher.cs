using System.Threading;
using System.Threading.Tasks;
using Highlights.Api.Entities;

namespace Highlights.Api.Services.Enrichment;

// The worker depends on this abstraction and doesn't care if it's a stub or a real LLM.
public interface IHighlightEnricher
{
    // Given a "raw" highlight, try to come up with a nicer title/summary/etc.
    Task<HighlightEnrichmentResult> EnrichAsync(
        Highlight highlight,
        CancellationToken cancellationToken = default);
}
