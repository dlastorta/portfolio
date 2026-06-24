using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Domain.Abstractions;
using ModularMonolith.Domain.Common;
using ModularMonolith.Infrastructure.Persistence;
using ModularMonolith.Infrastructure.Persistence.Repositories;
using ModularMonolith.Infrastructure.Time;

namespace ModularMonolith.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' was not found. Set ConnectionStrings:Default to a SQL Server connection string.");

        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));

        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<ICrewRoleRepository, CrewRoleRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
