using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PainFinder.Application.Services;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;
using PainFinder.Infrastructure.AI;
using PainFinder.Infrastructure.Persistence;
using PainFinder.Infrastructure.Services;

namespace PainFinder.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString, string geminiApiKey)
    {
        services.AddDbContext<PainFinderDbContext>(options =>
            options.UseSqlServer(connectionString));

        // ASP.NET Core Identity
        services.AddIdentityCore<AppUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 6;
            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<PainFinderDbContext>();

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Gemini AI client para generación de requerimientos
        services.AddHttpClient("Gemini", client => client.Timeout = TimeSpan.FromSeconds(120));
        services.AddKeyedSingleton<IChatClient>("gemini-flash", (sp, _) =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("Gemini");
            return new GeminiChatClient(httpClient, geminiApiKey, "gemini-2.5-flash");
        });

        // Auth
        services.AddScoped<IAuthService, AuthService>();

        // Suscripción y pagos
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddHttpClient("LemonSqueezy", client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddScoped<IPaymentService, LemonSqueezyService>();

        // Error logging
        services.AddScoped<IErrorLogService, ErrorLogService>();

        // ReqAI
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IRequirementsService>(sp =>
            new AiRequirementsService(
                sp.GetRequiredKeyedService<IChatClient>("gemini-flash"),
                sp.GetRequiredService<PainFinderDbContext>(),
                sp.GetRequiredService<ISubscriptionService>(),
                sp.GetRequiredService<ILogger<AiRequirementsService>>()));

        return services;
    }
}

