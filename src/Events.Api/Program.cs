using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;
using Events.Api.Contracts;
using Events.Api.Publishing;
using Events.Api.Config;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure one JSON style for both HTTP and Kafka so things stay consistent.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Use classic web-style camelCase JSON.
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

    // Skip nulls so payloads stay a bit cleaner.
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// Expose those same options as a singleton so Kafka publisher can reuse them.
builder.Services.AddSingleton(sp =>
{
    var httpJsonOptions = sp.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>();
    return httpJsonOptions.Value.SerializerOptions;
});

// Bind the "Kafka" section from configuration into KafkaSettings so we can inject it later.
builder.Services.AddOptions<KafkaSettings>()
    .Bind(builder.Configuration.GetSection(KafkaSettings.SectionName))
    // If Kafka is enabled, we require BootstrapServers.
    .Validate(
        settings => !settings.Enabled || !string.IsNullOrWhiteSpace(settings.BootstrapServers),
        "Kafka:BootstrapServers must be set when Kafka:Enabled is true.")
    // If Kafka is enabled, we also require a topic name.
    .Validate(
        settings => !settings.Enabled || !string.IsNullOrWhiteSpace(settings.MatchEventsTopic),
        "Kafka:MatchEventsTopic must be set when Kafka:Enabled is true.")
    // Run validation during startup so we fail fast instead of on first request.
    .ValidateOnStart();


// Register the concrete publishers so we can swap between them at runtime.
builder.Services.AddSingleton<LoggingMatchEventPublisher>();
builder.Services.AddSingleton<KafkaMatchEventPublisher>();

// Expose a single IMatchEventPublisher, choosing implementation by config.
// If Kafka:Enabled = true -> KafkaMatchEventPublisher
// If Kafka:Enabled = false -> LoggingMatchEventPublisher (stub)
builder.Services.AddSingleton<IMatchEventPublisher>(sp =>
{
    var kafkaOptions = sp.GetRequiredService<IOptions<KafkaSettings>>().Value;
    var logger = sp.GetRequiredService<ILoggerFactory>()
                   .CreateLogger("MatchEventPublisherFactory");

    if (kafkaOptions.Enabled)
    {
        logger.LogInformation("Kafka is enabled. Using KafkaMatchEventPublisher.");
        return sp.GetRequiredService<KafkaMatchEventPublisher>();
    }

    logger.LogWarning("Kafka is disabled. Falling back to LoggingMatchEventPublisher stub.");
    return sp.GetRequiredService<LoggingMatchEventPublisher>();
});

var app = builder.Build();

// We'll reuse the same JSON settings everywhere, including when we serialize ProblemDetails.
var jsonOptions = app.Services.GetRequiredService<JsonSerializerOptions>();

// Global error handler so callers always get a clean ProblemDetails response instead of a raw 500.
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
        if (exceptionFeature is null)
        {
            return;
        }

        var exception = exceptionFeature.Error;

        ProblemDetails problem;

        if (exception is MatchEventPublishException publishEx)
        {
            // This is our "Kafka publish failed" path.
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

            problem = new ProblemDetails
            {
                Type = "https://httpstatuses.io/503",
                Title = "Match event publishing failed.",
                Status = StatusCodes.Status503ServiceUnavailable,
                Detail = "We couldn't publish this match event to the streaming backend. Please try again in a bit."
            };

            // Extra context so callers (and we) can correlate things.
            problem.Extensions["matchId"] = publishEx.MatchId;
            problem.Extensions["topic"] = publishEx.Topic;
        }
        else
        {
            // Anything else is treated as an unexpected server error.
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            problem = new ProblemDetails
            {
                Type = "https://httpstatuses.io/500",
                Title = "An unexpected error occurred.",
                Status = StatusCodes.Status500InternalServerError,
                Detail = app.Environment.IsDevelopment()
                    ? exception.Message
                    : "Something went wrong while processing your request."
            };
        }

        context.Response.ContentType = "application/problem+json";

        var json = JsonSerializer.Serialize(problem, jsonOptions);
        await context.Response.WriteAsync(json);
    });
});

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
