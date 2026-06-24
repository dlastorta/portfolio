using ModularMonolith.Application;
using ModularMonolith.Infrastructure;
using ModularMonolith.Infrastructure.Persistence;
using ModularMonolith.WebApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Demo convenience: create and seed the database at startup.
// A real deployment applies migrations out-of-process (see the README's migration-runner note).
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await ApplicationDbContextSeed.SeedAsync(dbContext);
}

app.UseSwagger();
app.UseSwaggerUI();

app.MapJobsEndpoints();
app.MapCatalogEndpoints();

app.Run();

// Exposed so integration tests can use WebApplicationFactory<Program> if desired.
public partial class Program
{
}
