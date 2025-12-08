using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Highlights.Api.Configuration;
using Highlights.Api.Data;
using Highlights.Api.Entities;
using Highlights.Api.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Highlights.Api.Consumers
{
    // This service sits quietly in the background,
    // listening to Kafka for new match events and turning GOAL events into PENDING_AI highlights.
    public class KafkaMatchEventsConsumer : BackgroundService
    {
        private readonly ILogger<KafkaMatchEventsConsumer> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly KafkaSettings _kafkaSettings;

        // We'll use this once and reuse it for all deserialization calls.
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public KafkaMatchEventsConsumer(
            ILogger<KafkaMatchEventsConsumer> logger,
            IServiceScopeFactory scopeFactory,
            IOptions<KafkaSettings> kafkaOptions)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _kafkaSettings = kafkaOptions.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // First priority: explicit env var (e.g. set in docker-compose).
            var bootstrapServersEnv = Environment.GetEnvironmentVariable("Kafka__BootstrapServers");

            // Second priority: whatever was bound into KafkaSettings from appsettings.
            var configuredBootstrapServers = _kafkaSettings.BootstrapServers;

            var bootstrapServers =
                !string.IsNullOrWhiteSpace(bootstrapServersEnv)
                    ? bootstrapServersEnv
                    : (!string.IsNullOrWhiteSpace(configuredBootstrapServers)
                        ? configuredBootstrapServers
                        : "localhost:9092");

            // Basic consumer configuration.
            var config = new ConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = _kafkaSettings.ConsumerGroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = true
            };

            using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();

            consumer.Subscribe(_kafkaSettings.MatchEventsTopic);

            _logger.LogInformation(
                "KafkaMatchEventsConsumer started. Listening to topic {Topic} with group {GroupId} on {BootstrapServers}.",
                _kafkaSettings.MatchEventsTopic,
                _kafkaSettings.ConsumerGroupId,
                bootstrapServers);


            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        // This will block until a message arrives or cancellation is requested.
                        var consumeResult = consumer.Consume(stoppingToken);

                        if (consumeResult?.Message == null || string.IsNullOrWhiteSpace(consumeResult.Message.Value))
                        {
                            // Nothing useful to do here, just loop again.
                            continue;
                        }

                        _logger.LogDebug(
                            "Consumed message at {TopicPartitionOffset}: {Value}",
                            consumeResult.TopicPartitionOffset,
                            consumeResult.Message.Value);

                        MatchEventMessage? matchEvent;

                        try
                        {
                            matchEvent = JsonSerializer.Deserialize<MatchEventMessage>(
                                consumeResult.Message.Value,
                                _jsonOptions);
                        }
                        catch (JsonException jsonEx)
                        {
                            // If deserialization blows up, we log and move on instead of crashing the whole service.
                            _logger.LogWarning(
                                jsonEx,
                                "Failed to deserialize match event message. Raw message: {RawMessage}",
                                consumeResult.Message.Value);
                            continue;
                        }

                        if (matchEvent is null)
                        {
                            _logger.LogWarning(
                                "Deserialized match event message is null. Raw message: {RawMessage}",
                                consumeResult.Message.Value);
                            continue;
                        }

                        // For now we only care about GOAL events.
                        if (!string.Equals(matchEvent.EventType, "goal", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogDebug(
                                "Skipping non-goal event type {EventType} for match {MatchId}.",
                                matchEvent.EventType,
                                matchEvent.MatchId);
                            continue;
                        }

                        await HandleGoalEventAsync(matchEvent, stoppingToken);
                    }
                    catch (ConsumeException ex)
                    {
                        // This is usually a transient Kafka issue; we log and keep going.
                        _logger.LogError(ex, "Error while consuming from Kafka.");
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        // Normal shutdown path, no drama needed.
                        break;
                    }
                    catch (Exception ex)
                    {
                        // Catch-all so a single bad message doesn't kill the whole service.
                        _logger.LogError(ex, "Unexpected error in KafkaMatchEventsConsumer loop.");
                        // Tiny backoff so we don't go crazy in a tight failure loop.
                        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    }
                }
            }
            finally
            {
                // Close will commit offsets and leave the group cleanly.
                consumer.Close();
                _logger.LogInformation("KafkaMatchEventsConsumer is shutting down.");
            }
        }

        private async Task HandleGoalEventAsync(
            MatchEventMessage matchEvent,
            CancellationToken cancellationToken)
        {
            // We create a scope because DbContext is scoped and this background service is effectively singleton.
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<HighlightsDbContext>();

            var nowUtc = DateTimeOffset.UtcNow;

            var highlight = new Highlight
            {
                Id = Guid.NewGuid(),
                MatchId = matchEvent.MatchId,
                OccurredAt = matchEvent.OccurredAt,
                EventType = matchEvent.EventType,
                Team = matchEvent.Team,
                Player = matchEvent.Player,
                Description = matchEvent.Description,
                Status = "PENDING_AI",
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };

            _logger.LogInformation(
                "Creating PENDING_AI highlight for match {MatchId} at {OccurredAt} for player {Player}.",
                matchEvent.MatchId,
                matchEvent.OccurredAt,
                matchEvent.Player);

            await dbContext.Highlights.AddAsync(highlight, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
