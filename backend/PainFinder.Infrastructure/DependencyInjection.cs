using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PainFinder.Application.Services;
using PainFinder.Domain.Entities;
using PainFinder.Domain.Interfaces;
using PainFinder.Infrastructure.AI;
using PainFinder.Infrastructure.Connectors;
using PainFinder.Infrastructure.Persistence;
using PainFinder.Infrastructure.Services;

namespace PainFinder.Infrastructure;

public static class DependencyInjection
{
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString, string geminiApiKey, string stackOverflowApiKey)
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

        // --- HttpClient registrations ---

        // API connectors (custom User-Agent)
        services.AddHttpClient<RedditConnector>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PainFinder/1.0 (.NET)");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient<HackerNewsConnector>(client =>
            client.Timeout = TimeSpan.FromSeconds(30));
        services.AddSingleton(new StackOverflowConnectorOptions { ApiKey = stackOverflowApiKey });
        services.AddHttpClient<StackOverflowConnector>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PainFinder/1.0 (.NET)");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            });
        services.AddHttpClient<AppStoreConnector>(client =>
            client.Timeout = TimeSpan.FromSeconds(30));

        // Web scraping connectors (browser-like headers)
        ConfigureScraperClient<G2Connector>(services);
        ConfigureScraperClient<CapterraConnector>(services);
        ConfigureScraperClient<TrustpilotConnector>(services);
        ConfigureScraperClient<GooglePlayConnector>(services);
        ConfigureScraperClient<AmazonReviewsConnector>(services);

        // Search engine proxy connectors (lighter headers)
        ConfigureScraperClient<IndieHackersConnector>(services);

        // Search engine proxy connectors
        ConfigureScraperClient<FacebookConnector>(services);
        ConfigureScraperClient<InstagramConnector>(services);

        // Placeholder connectors (need API keys)
        services.AddHttpClient<ProductHuntConnector>();
        services.AddHttpClient<TwitterConnector>();
        services.AddHttpClient<LinkedInConnector>();
        services.AddHttpClient<YouTubeConnector>();

        // Discussion communities
        services.AddScoped<ISourceConnector, RedditConnector>();
        services.AddScoped<ISourceConnector, HackerNewsConnector>();
        services.AddScoped<ISourceConnector, StackOverflowConnector>();
        services.AddScoped<ISourceConnector, IndieHackersConnector>();
        services.AddScoped<ISourceConnector, ProductHuntConnector>();

        // Product reviews
        services.AddScoped<ISourceConnector, G2Connector>();
        services.AddScoped<ISourceConnector, CapterraConnector>();
        services.AddScoped<ISourceConnector, TrustpilotConnector>();
        services.AddScoped<ISourceConnector, AppStoreConnector>();
        services.AddScoped<ISourceConnector, GooglePlayConnector>();
        services.AddScoped<ISourceConnector, AmazonReviewsConnector>();

        // Social media
        services.AddScoped<ISourceConnector, TwitterConnector>();
        services.AddScoped<ISourceConnector, LinkedInConnector>();
        services.AddScoped<ISourceConnector, YouTubeConnector>();
        services.AddScoped<ISourceConnector, FacebookConnector>();
        services.AddScoped<ISourceConnector, InstagramConnector>();

        // Developer communities
        services.AddHttpClient<GitHubConnector>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PainFinder/1.0 (.NET)");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<ISourceConnector, GitHubConnector>();

        // --- Gemini AI clients (two models) ---
        services.AddHttpClient("Gemini", client => client.Timeout = TimeSpan.FromSeconds(60));
        services.AddHttpClient("Gemini-Thinking", client => client.Timeout = TimeSpan.FromSeconds(120));
        services.AddHttpClient("Gemini-Expansion", client => client.Timeout = TimeSpan.FromSeconds(120));

        // Flash — for keyword expansion & subreddit suggestions (requires better reasoning)
        services.AddKeyedSingleton<IChatClient>("gemini-flash", (sp, _) =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("Gemini");
            return new GeminiChatClient(httpClient, geminiApiKey, "gemini-2.5-flash");
        });

        // Flash with light thinking — for keyword expansion (structured multi-category output)
        services.AddKeyedSingleton<IChatClient>("gemini-flash-expansion", (sp, _) =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("Gemini-Expansion");
            return new GeminiChatClient(httpClient, geminiApiKey, "gemini-2.5-flash", thinkingBudget: 512);
        });

        // Flash-Lite — for pain detection (high volume, classification task)
        services.AddKeyedSingleton<IChatClient>("gemini-flash-lite", (sp, _) =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("Gemini");
            return new GeminiChatClient(httpClient, geminiApiKey, "gemini-2.5-flash-lite");
        });

        // Flash with thinking — only for MVP generation (strategic reasoning, on-demand)
        services.AddKeyedSingleton<IChatClient>("gemini-flash-thinking", (sp, _) =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("Gemini-Thinking");
            return new GeminiChatClient(httpClient, geminiApiKey, "gemini-2.5-flash", thinkingBudget: 1024);
        });

        // Also register a default (Flash-Lite) for any non-keyed injection
        services.AddSingleton<IChatClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("Gemini");
            return new GeminiChatClient(httpClient, geminiApiKey, "gemini-2.5-flash-lite");
        });

        services.AddSingleton<IKeywordExpansionService>(sp =>
            new AiKeywordExpansionService(
                sp.GetRequiredKeyedService<IChatClient>("gemini-flash-expansion"),
                sp.GetRequiredService<ILogger<AiKeywordExpansionService>>()));

        services.AddSingleton<ISubredditSuggestionService>(sp =>
            new AiSubredditSuggestionService(
                sp.GetRequiredKeyedService<IChatClient>("gemini-flash"),
                sp.GetRequiredService<ILogger<AiSubredditSuggestionService>>()));

        // Analysis services — Pain detection uses Flash-Lite (high volume, cheap)
        services.AddScoped<IPainDetectionService>(sp =>
            new AiPainDetectionService(
                sp.GetRequiredKeyedService<IChatClient>("gemini-flash-lite"),
                sp.GetRequiredService<ILogger<AiPainDetectionService>>()));

        // Clustering + Opportunities — single Gemini Flash call produces both (scoped = same instance)
        services.AddScoped<AiInsightGenerationService>(sp =>
            new AiInsightGenerationService(
                sp.GetRequiredKeyedService<IChatClient>("gemini-flash"),
                sp.GetRequiredService<ILogger<AiInsightGenerationService>>()));
        services.AddScoped<IPainClusteringService>(sp => sp.GetRequiredService<AiInsightGenerationService>());
        services.AddScoped<IOpportunityGenerationService>(sp => sp.GetRequiredService<AiInsightGenerationService>());

        // MVP plan generation — uses Flash with thinking=1024 for strategic reasoning quality
        services.AddScoped<IMvpGenerationService>(sp =>
            new AiMvpGenerationService(
                sp.GetRequiredKeyedService<IChatClient>("gemini-flash-thinking"),
                sp.GetRequiredService<PainFinderDbContext>(),
                sp.GetRequiredService<ILogger<AiMvpGenerationService>>()));

        // Deep opportunity analysis — uses Flash with thinking=1024 for strategic depth
        services.AddScoped<IDeepOpportunityService>(sp =>
            new AiDeepOpportunityService(
                sp.GetRequiredKeyedService<IChatClient>("gemini-flash-thinking"),
                sp.GetRequiredService<PainFinderDbContext>(),
                sp.GetRequiredService<ILogger<AiDeepOpportunityService>>()));

        // Auth service
        services.AddScoped<IAuthService, AuthService>();

        // Subscription service
        services.AddScoped<ISubscriptionService, SubscriptionService>();

        // Opportunity query service
        services.AddScoped<IOpportunityQueryService, OpportunityQueryServiceImpl>();

        // Lemon Squeezy
        services.AddHttpClient("LemonSqueezy", client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddScoped<IPaymentService, LemonSqueezyService>();

        // Error logging
        services.AddScoped<IErrorLogService, ErrorLogService>();

        return services;
    }

    private static void ConfigureScraperClient<T>(IServiceCollection services) where T : class
    {
        services.AddHttpClient<T>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
    }
}
