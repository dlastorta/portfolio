using MediatR;
using ModularMonolith.Application.Modules.Catalog.Queries.GetAllCrewRoles;

namespace ModularMonolith.WebApi.Endpoints;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/crew-roles", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetAllCrewRolesQuery(), ct)).ToHttpResult())
            .WithTags("Catalog");

        return app;
    }
}
