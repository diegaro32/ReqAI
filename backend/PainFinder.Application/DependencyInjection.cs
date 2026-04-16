using Microsoft.Extensions.DependencyInjection;

namespace PainFinder.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // IProjectService e IRequirementsService se registran en Infrastructure DI
        // porque sus implementaciones dependen de IChatClient y PainFinderDbContext.
        return services;
    }
}
