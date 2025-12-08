using Microsoft.EntityFrameworkCore;
using Highlights.Api.Data;

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

// Simple sanity-check endpoint: returns all highlights from the database.
// For now we don't do filtering/paging; this is just to prove wiring is correct.
app.MapGet("/highlights", async (HighlightsDbContext db) =>
{
    // Grab everything and sort newest-first so it feels a bit nicer.
    var highlights = await db.Highlights
        .OrderByDescending(h => h.OccurredAt)
        .ToListAsync();

    return Results.Ok(highlights);
});


app.Run();
