using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Highlights.Api.Entities;
using Highlights.Api.Services.Enrichment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// NOTE: If your DbContext lives in a different namespace (e.g. Highlights.Api.Persistence),
// just tweak this using:
using Highlights.Api.Data;

namespace Highlights.Api.Services.Enrichment;

// This background worker loops in the background, looks for PENDING_AI highlights,
// runs them through the IHighlightEnricher, and updates their status + fields.
public class HighlightEnrichmentWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HighlightEnrichmentWorker> _logger;

    // Keeping these as simple constants for now; we can move them to config later if needed.
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int MaxBatchSize = 25;

    public HighlightEnrichmentWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<HighlightEnrichmentWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Highlight enrichment worker is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Host is shutting down.
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while running highlight enrichment loop.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Host is stopping; just exit the loop.
                break;
            }
        }

        _logger.LogInformation("Highlight enrichment worker is stopping.");
    }

    // This is the "one pass" of the worker: grab some PENDING_AI rows, enrich them, save.
    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        // Resolve things that are scoped (like DbContext) from the scope, not from the root.
        var db = scope.ServiceProvider.GetRequiredService<HighlightsDbContext>();
        var enricher = scope.ServiceProvider.GetRequiredService<IHighlightEnricher>();

        // Grab a small batch of PENDING_AI highlights in a stable order.
        var pending = await db.Highlights
            .Where(h => h.Status == HighlightStatus.PendingAi)
            .OrderBy(h => h.CreatedAt)
            .Take(MaxBatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            _logger.LogDebug("No PENDING_AI highlights found in this cycle.");
            return;
        }

        _logger.LogInformation("Found {Count} PENDING_AI highlights to process.", pending.Count);

        foreach (var highlight in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await enricher.EnrichAsync(highlight, cancellationToken);

            if (result.Success)
            {
                highlight.Status = HighlightStatus.Ready;

                // Only overwrite if the enricher actually gave us something.
                if (!string.IsNullOrWhiteSpace(result.Title))
                {
                    highlight.Title = result.Title;
                }

                if (!string.IsNullOrWhiteSpace(result.Summary))
                {
                    highlight.Summary = result.Summary;
                }

                if (!string.IsNullOrWhiteSpace(result.ThumbnailUrl))
                {
                    highlight.ThumbnailUrl = result.ThumbnailUrl;
                }

                highlight.UpdatedAt = DateTimeOffset.UtcNow;

                _logger.LogInformation(
                    "Successfully enriched highlight {HighlightId} with title '{Title}'.",
                    highlight.Id,
                    highlight.Title);
            }
            else
            {
                // If enrichment failed, we park it in FAILED_AI so it doesn’t get stuck forever.
                highlight.Status = HighlightStatus.FailedAi;
                highlight.UpdatedAt = DateTimeOffset.UtcNow;

                _logger.LogWarning(
                    "Failed to enrich highlight {HighlightId}. Reason: {Reason}",
                    highlight.Id,
                    result.FailureReason ?? "no reason provided");
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
