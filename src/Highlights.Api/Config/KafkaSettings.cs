namespace Highlights.Api.Configuration;

// These are the knobs we can tweak for the Kafka consumer.
// We'll bind them from appsettings.json / environment variables.
public class KafkaSettings
{
    public string BootstrapServers { get; set; } = string.Empty;

    // This is the topic we listen to for match events.
    public string MatchEventsTopic { get; set; } = "match-events";

    // This is the consumer group id, so Kafka can track our offsets.
    public string ConsumerGroupId { get; set; } = "highlights-api-consumer";
}
