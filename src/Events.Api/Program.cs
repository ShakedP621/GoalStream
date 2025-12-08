var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// This pair wires up Swagger / OpenAPI support
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

// Keep this if you want to use controllers later
app.MapControllers();

// Simple health endpoint so we can quickly see the service is alive.
// Nothing fancy yet – just "I'm here" plus a timestamp.
app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        status = "ok",
        service = "events-api",
        timeUtc = DateTime.UtcNow
    });
});

app.Run();
