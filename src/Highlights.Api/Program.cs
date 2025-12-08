var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Swagger / OpenAPI support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

// Simple health endpoint for the highlights service.
// Later we'll expand this once we have DB / Kafka wired up.
app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "ok",
        service = "highlights-api",
        timeUtc = DateTime.UtcNow
    });
});

app.Run();
