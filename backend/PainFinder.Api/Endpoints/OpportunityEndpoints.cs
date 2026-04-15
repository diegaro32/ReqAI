using PainFinder.Application.Services;

namespace PainFinder.Api.Endpoints;

public static class OpportunityEndpoints
{
    public static RouteGroupBuilder MapOpportunityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/opportunities").WithTags("Opportunities");

        group.MapGet("/", async (IOpportunityQueryService service, CancellationToken cancellationToken) =>
        {
            var opportunities = await service.GetAllOpportunitiesAsync(cancellationToken);
            return Results.Ok(opportunities);
        });

        return group;
    }
}
