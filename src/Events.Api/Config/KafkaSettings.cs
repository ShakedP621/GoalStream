using System;

namespace Events.Api.Config
{
    // Simple POCO that maps to the "Kafka" section in appsettings.*.json
    public sealed class KafkaSettings
    {
        public const string SectionName = "Kafka";

        // This flag lets us flip between the stub and the real Kafka publisher.
        public bool Enabled { get; init; } = true;

        // Comma-separated list of brokers, e.g. "localhost:9092" or "kafka:9092"
        public string BootstrapServers { get; init; } = string.Empty;

        // Topic where match events will be published.
        public string MatchEventsTopic { get; init; } = "match-events";
    }
}
