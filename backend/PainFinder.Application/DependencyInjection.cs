using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PainFinder.Application.Services;

namespace PainFinder.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ISearchRunService, SearchRunService>();
        services.AddScoped<IRadarMonitorService, RadarMonitorService>();
        services.AddScoped<IDashboardService, DashboardService>();

        services.AddValidatorsFromAssemblyContaining<Validators.SearchRunRequestValidator>();

        return services;
    }
}
