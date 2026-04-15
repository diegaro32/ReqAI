using PainFinder.Application.Services;

namespace PainFinder.Api.Endpoints;

public static class DashboardEndpoints
{
    public static RouteGroupBuilder MapDashboardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/dashboard").WithTags("Dashboard");

        group.MapGet("/", async (IDashboardService service, CancellationToken cancellationToken) =>
        {
            var dashboard = await service.GetDashboardAsync(cancellationToken);
            return Results.Ok(dashboard);
        });

        return group;
    }
}
