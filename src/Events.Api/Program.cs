using Events.Api.Contracts;
using Events.Api.Publishing;
using Microsoft.AspNetCore.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// This hooks up our current stub publisher.
// Later we’ll swap this to a real Kafka-backed implementation
// without touching the /events endpoint.
builder.Services.AddSingleton<IMatchEventPublisher, LoggingMatchEventPublisher>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Note: HTTPS redirection is disabled for now to keep local + docker usage simple.
// app.UseHttpsRedirection();

app.MapGet("/health", () =>
{
    // Simple health check so we know the service is alive and roughly what time it is.
    return Results.Ok(new
    {
        Service = "Events.Api",
        Status = "Healthy",
        UtcNow = DateTime.UtcNow
    });
});

// This is the main entry point for real-time match events.
app.MapPost("/events", async (
    MatchEventRequest request,
    IMatchEventPublisher publisher,
    CancellationToken cancellationToken) =>
{
    var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

    if (request.MatchId == Guid.Empty)
    {
        errors["matchId"] = new[] { "MatchId must be a non-empty GUID." };
    }

    if (request.OccurredAt == default)
    {
        errors["occurredAt"] = new[] { "OccurredAt must be a valid date/time." };
    }

    if (string.IsNullOrWhiteSpace(request.EventType))
    {
        errors["eventType"] = new[] { "EventType is required." };
    }

    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    await publisher.PublishAsync(request, cancellationToken);

    return Results.Accepted($"/events/{request.MatchId}", new
    {
        message = "Match event accepted for processing.",
        matchId = request.MatchId,
        eventType = request.EventType
    });
});


app.Run();
