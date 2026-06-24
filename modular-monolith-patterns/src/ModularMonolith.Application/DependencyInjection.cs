using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Application.Common.Behaviors;
using ModularMonolith.Application.Common.Events;
using ModularMonolith.Application.Modules.Catalog;
using ModularMonolith.Application.Modules.Jobs;

namespace ModularMonolith.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);

            // Order matters: behaviors run in registration order.
            // Logging (outer) -> Validation -> Transaction (inner) -> handler.
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(TransactionBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        // Mapperly mappers are stateless source-generated classes — register one per module.
        services.AddSingleton<JobMapper>();
        services.AddSingleton<CrewRoleMapper>();

        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        return services;
    }
}
