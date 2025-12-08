using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Highlights.Api.Config;
using Highlights.Api.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Highlights.Api.Services.Enrichment;

// This is the "real" enricher that talks to an external HTTP endpoint.
// That endpoint can do whatever it wants (call an LLM, rules engine, etc.)
// as long as it returns a JSON payload that looks like HighlightEnrichmentResult.
public class RealLlmHighlightEnricher : IHighlightEnricher
{
    private readonly HttpClient _httpClient;
    private readonly AiSettings _settings;
    private readonly ILogger<RealLlmHighlightEnricher> _logger;

    public RealLlmHighlightEnricher(
        HttpClient httpClient,
        IOptions<AiSettings> settings,
        ILogger<RealLlmHighlightEnricher> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<HighlightEnrichmentResult> EnrichAsync(
        Highlight highlight,
        CancellationToken cancellationToken = default)
    {
        // Sanity checks so we fail gracefully if someone flips the switch
        // without actually wiring the external service.
        if (string.IsNullOrWhiteSpace(_settings.EnrichmentEndpoint))
        {
            _logger.LogWarning(
                "EnrichmentEndpoint is not configured. Cannot use RealLlmHighlightEnricher.");

            return new HighlightEnrichmentResult
            {
                Success = false,
                FailureReason = "Enrichment endpoint is not configured."
            };
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                _settings.EnrichmentEndpoint);

            // If an API key is configured, send it as a simple header.
            // You can change the header name to whatever your service expects.
            if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                request.Headers.Add("X-Api-Key", _settings.ApiKey);
            }

            // Ship only what the external service actually needs.
            var payload = new
            {
                highlight.Id,
                highlight.MatchId,
                highlight.OccurredAt,
                highlight.EventType,
                highlight.Team,
                highlight.Player,
                highlight.Description
            };

            request.Content = JsonContent.Create(payload);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Real enrichment call failed with HTTP {StatusCode}. Body: {Body}",
                    (int)response.StatusCode,
                    body);

                return new HighlightEnrichmentResult
                {
                    Success = false,
                    FailureReason = $"Enrichment endpoint returned {(int)response.StatusCode}."
                };
            }

            // The external service is expected to send back JSON that matches HighlightEnrichmentResult.
            var result = await response.Content.ReadFromJsonAsync<HighlightEnrichmentResult>(
                cancellationToken: cancellationToken);

            if (result is null)
            {
                _logger.LogWarning(
                    "Real enrichment endpoint returned an empty or invalid body.");

                return new HighlightEnrichmentResult
                {
                    Success = false,
                    FailureReason = "Enrichment endpoint returned invalid JSON."
                };
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Host is shutting down or request was canceled. Just bubble up a friendly failure.
            return new HighlightEnrichmentResult
            {
                Success = false,
                FailureReason = "Enrichment request was canceled."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while calling the real enrichment endpoint.");

            return new HighlightEnrichmentResult
            {
                Success = false,
                FailureReason = "Unexpected error during real enrichment call."
            };
        }
    }
}
