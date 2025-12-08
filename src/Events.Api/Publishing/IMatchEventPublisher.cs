using Events.Api.Contracts;

namespace Events.Api.Publishing;

// This is the "port" the rest of the app talks to.
// Today it's just a stub, tomorrow it can be a real Kafka producer
// without the /events endpoint even noticing.
public interface IMatchEventPublisher
{
    // Fire-and-forget style publish. We return a Task in case we later
    // want to do I/O (like talking to Kafka) without changing the signature.
    Task PublishAsync(MatchEventRequest matchEvent, CancellationToken cancellationToken = default);
}
