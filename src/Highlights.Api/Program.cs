using Highlights.Api.Config;
using Highlights.Api.Configuration;
using Highlights.Api.Consumers;
using Highlights.Api.Data;
using Highlights.Api.Services.Enrichment;
using Highlights.Api.Dtos;
using Highlights.Api.Services.Cache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using System.Linq;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Tell EF how to talk to Postgres for highlights.
builder.Services.AddDbContext<HighlightsDbContext>(options =>
{
    // We’ll define this connection string name in appsettings in the next step.
    var connectionString = builder.Configuration.GetConnectionString("HighlightsDatabase")
        ?? throw new InvalidOperationException(
            "Connection string 'HighlightsDatabase' not found. " +
            "Make sure it's set in appsettings or environment variables.");

    options.UseNpgsql(connectionString);
});


// Add services to the container.
builder.Services.AddControllers();

// Swagger / OpenAPI support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Wire up KafkaSettings so we can inject strongly-typed config into our consumer later.
builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection("Kafka"));
// Spin up the Kafka consumer as a background worker so it can quietly listen for match events.
builder.Services.AddHostedService<KafkaMatchEventsConsumer>();

// Bind Redis settings from configuration so we can keep the connection string in appsettings.
builder.Services.Configure<RedisSettings>(builder.Configuration.GetSection("Redis"));

// Register a single ConnectionMultiplexer to be shared by the whole app.
// StackExchange.Redis is designed to use one instance per app, not per request.
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var redisOptions = sp.GetRequiredService<IOptions<RedisSettings>>();
    var settings = redisOptions.Value;

    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("RedisStartup");

    logger.LogInformation("Connecting to Redis at {ConnectionString}", settings.ConnectionString);

    // This call creates the multiplexer which will handle reconnects under the hood.
    var mux = ConnectionMultiplexer.Connect(settings.ConnectionString);

    logger.LogInformation("Redis connection multiplexer created.");

    return mux;
});
// Register a highlight-specific cache that happens to be backed by Redis.
// Endpoints won't use this yet.
builder.Services.AddSingleton<IHighlightCache, RedisHighlightCache>();

// Enrichment pipeline:
// - We always register both implementations.
// - IHighlightEnricher itself is a small factory that chooses based on AiSettings.
builder.Services.AddScoped<StubHighlightEnricher>();
builder.Services.AddHttpClient<RealLlmHighlightEnricher>();

builder.Services.AddScoped<IHighlightEnricher>(sp =>
{
    var options = sp.GetRequiredService<IOptions<AiSettings>>();
    var settings = options.Value;

    if (settings.UseStubEnricher)
    {
        // Easy, predictable behavior for dev/test environments.
        return sp.GetRequiredService<StubHighlightEnricher>();
    }

    // In "real" environments, we lean on the external enrichment service.
    return sp.GetRequiredService<RealLlmHighlightEnricher>();
});

// This runs in the background and keeps chewing through PENDING_AI highlights.
builder.Services.AddHostedService<HighlightEnrichmentWorker>();
;
// Bind AI-related settings from configuration.
builder.Services.Configure<AiSettings>(builder.Configuration.GetSection("Ai"));

var app = builder.Build();


// On startup, make sure the database exists and migrations are applied.
// This keeps local/dev setups smooth – no manual "dotnet ef database update" required.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HighlightsDbContext>();

    // This will create the database (if it doesn't exist) and apply any pending migrations.
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.MapGet("/highlights/{id:guid}", async (Guid id, HighlightsDbContext db) =>
{
    // We only need to read here, so no tracking – keeps EF nice and lean.
    var highlight = await db.Highlights
        .AsNoTracking()
        .FirstOrDefaultAsync(h => h.Id == id);

    if (highlight is null)
    {
        // 404 with a tiny, friendly payload so callers know what went wrong.
        return Results.NotFound(new
        {
            message = "Highlight not found.",
            highlightId = id
        });
    }

    // Map the EF entity into the public DTO shape.
    var dto = highlight.ToDto();
    return Results.Ok(dto);
})
.WithName("GetHighlightById")
.WithSummary("Get a single highlight by its id.")
.WithDescription("Looks up a single highlight row by its Guid id and returns a clean HighlightDto payload.")
.Produces<HighlightDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);


// List highlights with optional filters + simple paging.
app.MapGet("/highlights", async (
    Guid? matchId,
    string? status,
    int? page,
    int? pageSize,
    HighlightsDbContext db) =>
{
    // Start with a read-only query so EF doesn't bother tracking.
    var query = db.Highlights
        .AsNoTracking()
        .AsQueryable();

    // If caller passes matchId, narrow results to that match.
    if (matchId.HasValue)
    {
        query = query.Where(h => h.MatchId == matchId.Value);
    }

    // If caller passes a status, filter by it (e.g. PENDING_AI, READY, FAILED_AI).
    if (!string.IsNullOrWhiteSpace(status))
    {
        var normalizedStatus = status.Trim();
        query = query.Where(h => h.Status == normalizedStatus);
    }

    // Basic paging guards so callers can’t accidentally ask for a million rows.
    const int DefaultPage = 1;
    const int DefaultPageSize = 50;
    const int MaxPageSize = 100;

    var safePage = !page.HasValue || page < 1
        ? DefaultPage
        : page.Value;

    var safePageSize =
        !pageSize.HasValue || pageSize <= 0
            ? DefaultPageSize
            : pageSize > MaxPageSize
                ? MaxPageSize
                : pageSize.Value;

    var skip = (safePage - 1) * safePageSize;

    // Sort newest-first so feeds feel natural.
    var entities = await query
        .OrderByDescending(h => h.OccurredAt)
        .Skip(skip)
        .Take(safePageSize)
        .ToListAsync();

    // Map entities into the public DTO shape.
    var dtos = entities
        .Select(h => h.ToDto())
        .ToList();

    return Results.Ok(dtos);
})
.WithName("ListHighlights")
.WithSummary("List highlights with optional filters.")
.WithDescription(
    "Returns a list of highlights filtered by optional matchId and status. " +
    "Use page and pageSize for basic paging (defaults: page=1, pageSize=50, max pageSize=100).")
.Produces<List<HighlightDto>>(StatusCodes.Status200OK);

// Do a tiny Redis check at startup just to prove we can talk to the cache.
// This doesn't change any HTTP behavior, it only writes to logs.
using (var scope = app.Services.CreateScope())
{
    var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("RedisStartup");
    var redis = scope.ServiceProvider.GetService<IConnectionMultiplexer>();

    if (redis is not null)
    {
        try
        {
            var db = redis.GetDatabase();
            var key = "health:highlights-api";
            var value = $"ok:{DateTimeOffset.UtcNow:O}";

            // Using the sync API here is fine – this is a one-off startup check.
            db.StringSet(key, value, TimeSpan.FromMinutes(1));
            var roundtrip = db.StringGet(key);

            logger.LogInformation(
                "Redis startup check succeeded. Stored {Key} = {Value}",
                key,
                roundtrip.ToString()
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Redis startup check failed.");
        }
    }
    else
    {
        logger.LogWarning("Redis startup check skipped – IConnectionMultiplexer not available.");
    }
}


app.Run();