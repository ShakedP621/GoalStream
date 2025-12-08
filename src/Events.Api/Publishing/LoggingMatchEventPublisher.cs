using Events.Api.Contracts;
using Microsoft.Extensions.Logging;

namespace Events.Api.Publishing;

// This is a temporary "fake" publisher.
// All it does is log the event, which is handy for testing
// and keeps our HTTP layer totally unaware of Kafka details.
public sealed class LoggingMatchEventPublisher : IMatchEventPublisher
{
    private readonly ILogger<LoggingMatchEventPublisher> _logger;

    public LoggingMatchEventPublisher(ILogger<LoggingMatchEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(MatchEventRequest matchEvent, CancellationToken cancellationToken = default)
    {
        // In a real implementation we'd serialize and send to Kafka here.
        // For now, we just log the payload so we can see something actually happened.
        _logger.LogInformation("Stub publisher received match event {@MatchEvent}", matchEvent);

        return Task.CompletedTask;
    }
}
