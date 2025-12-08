using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Events.Api.Config;
using Events.Api.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Events.Api.Publishing
{
    // This guy is responsible for taking our match events and pushing them into Kafka.
    public sealed class KafkaMatchEventPublisher : IMatchEventPublisher, IDisposable
    {
        private readonly KafkaSettings _settings;
        private readonly ILogger<KafkaMatchEventPublisher> _logger;
        private readonly IProducer<string, string> _producer;
        private readonly JsonSerializerOptions _serializerOptions;

        public KafkaMatchEventPublisher(
            IOptions<KafkaSettings> kafkaOptions,
            JsonSerializerOptions serializerOptions,
            ILogger<KafkaMatchEventPublisher> logger)
        {
            _settings = kafkaOptions?.Value ?? throw new ArgumentNullException(nameof(kafkaOptions));
            _serializerOptions = serializerOptions ?? throw new ArgumentNullException(nameof(serializerOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (string.IsNullOrWhiteSpace(_settings.BootstrapServers))
            {
                throw new InvalidOperationException(
                    "Kafka BootstrapServers is not configured. Set Kafka:BootstrapServers in appsettings or env vars.");
            }

            // Producer instances are thread-safe and meant to be long-lived,
            // so we spin up one here and reuse it for all events.
            var config = new ProducerConfig
            {
                BootstrapServers = _settings.BootstrapServers
            };

            _producer = new ProducerBuilder<string, string>(config).Build();
        }

        public async Task PublishAsync(
            MatchEventRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!_settings.Enabled)
            {
                // If Kafka is disabled, we just log and bail out.
                _logger.LogWarning(
                    "Kafka publishing is disabled via configuration. Skipping publish for match {MatchId}.",
                    request.MatchId);
                return;
            }

            // Serialize using the same JSON settings as the HTTP API so everything stays consistent.
            var payload = JsonSerializer.Serialize(request, _serializerOptions);

            var message = new Message<string, string>
            {
                // Using MatchId as the key helps Kafka keep events for the same match together.
                Key = request.MatchId.ToString(),
                Value = payload
            };

            try
            {
                var result = await _producer.ProduceAsync(
                    _settings.MatchEventsTopic,
                    message,
                    cancellationToken);

                _logger.LogInformation(
                    "Published match event to Kafka. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}, MatchId: {MatchId}, EventType: {EventType}",
                    result.Topic,
                    result.Partition.Value,
                    result.Offset.Value,
                    request.MatchId,
                    request.EventType);
            }
            catch (ProduceException<string, string> ex)
            {
                // We still log the underlying Kafka problem.
                _logger.LogError(
                    ex,
                    "Failed to publish match event to Kafka topic {Topic} for match {MatchId}.",
                    _settings.MatchEventsTopic,
                    request.MatchId);

                // Wrap it in a domain-specific exception so the API layer
                // can turn it into a clean ProblemDetails response.
                throw new MatchEventPublishException(
                    request.MatchId,
                    _settings.MatchEventsTopic,
                    "Failed to publish match event to Kafka.",
                    ex);
            }
        }

        public void Dispose()
        {
            try
            {
                // Try to flush any in-flight messages before shutting down.
                _producer.Flush(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                // Not the end of the world if flush fails during shutdown, but it's nice to know about it.
                _logger.LogWarning(ex, "Error while flushing Kafka producer during dispose.");
            }

            _producer.Dispose();
        }
    }
}
