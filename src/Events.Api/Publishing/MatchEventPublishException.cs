using System;

namespace Events.Api.Publishing
{
    // This exception is our way of saying:
    // "Hey, we tried to publish to Kafka and it went badly."
    public sealed class MatchEventPublishException : Exception
    {
        public Guid MatchId { get; }
        public string Topic { get; }

        public MatchEventPublishException(
            Guid matchId,
            string topic,
            string message,
            Exception innerException)
            : base(message, innerException)
        {
            MatchId = matchId;
            Topic = topic;
        }
    }
}
