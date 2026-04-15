using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PainFinder.Api.BackgroundServices;
using PainFinder.Api.Endpoints;
using PainFinder.Api.Middleware;
using PainFinder.Application;
using PainFinder.Application.Services;
using PainFinder.Infrastructure;
using PainFinder.Infrastructure.Persistence;
using PainFinder.Web.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services));

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

    var geminiApiKey = builder.Configuration["Gemini:ApiKey"];
    if (string.IsNullOrEmpty(geminiApiKey))
        throw new InvalidOperationException("Gemini API key not found. Set 'Gemini:ApiKey' via user secrets: dotnet user-secrets set \"Gemini:ApiKey\" \"your-key\"");

    var stackOverflowApiKey = builder.Configuration["StackOverflow:ApiKey"] ?? string.Empty;

    var jwtKey = builder.Configuration["Jwt:Key"];
    if (string.IsNullOrEmpty(jwtKey))
        throw new InvalidOperationException("JWT key not found. Set 'Jwt:Key' via user secrets: dotnet user-secrets set \"Jwt:Key\" \"your-key\"");

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(connectionString, geminiApiKey, stackOverflowApiKey);

    // Lemon Squeezy
    builder.Services.Configure<LemonSqueezySettings>(builder.Configuration.GetSection("LemonSqueezy"));

    // Blazor Server
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();
    builder.Services.AddScoped<AuthService>();
    builder.Services.AddScoped<IErrorBoundaryLogger, GlobalErrorHandler>();

    // Detailed circuit errors in development
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddServerSideBlazor().AddCircuitOptions(options =>
        {
            options.DetailedErrors = true;
        });
    }

    // JWT Authentication (for external API consumers)
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "PainFinder",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "PainFinder",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
    builder.Services.AddAuthorization();

    // Background services
    builder.Services.AddHostedService<ScraperBackgroundService>();
    builder.Services.AddHostedService<RadarScanBackgroundService>();

    // Health checks
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<PainFinderDbContext>("database");

    // Rate limiting
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Auth endpoints: 10 requests per minute per IP
        options.AddPolicy("auth", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1)
                }));

        // Search endpoints: 5 requests per minute per user
        options.AddPolicy("search", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1)
                }));

        // General API: 30 requests per minute per IP
        options.AddPolicy("api", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1)
                }));
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApi();

    var app = builder.Build();

    // Apply migrations on startup (creates DB if it doesn't exist)
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<PainFinderDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapDiagnosticsEndpoints();
    }

    app.UseSerilogRequestLogging();
    app.UseMiddleware<GlobalExceptionMiddleware>();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();
    app.UseAntiforgery();

    // API endpoints
    app.MapAuthEndpoints().RequireRateLimiting("auth");
    app.MapSearchEndpoints().RequireAuthorization().RequireRateLimiting("search");
    app.MapRadarEndpoints().RequireAuthorization().RequireRateLimiting("api");
    app.MapOpportunityEndpoints().RequireAuthorization().RequireRateLimiting("api");
    app.MapDashboardEndpoints().RequireAuthorization().RequireRateLimiting("api");
    app.MapSubscriptionEndpoints().RequireAuthorization().RequireRateLimiting("api");
    app.MapPaymentWebhookEndpoints();

    // Health check
    app.MapHealthChecks("/health");

    // Blazor Server UI
    app.MapRazorComponents<PainFinder.Api.Components.App>()
        .AddInteractiveServerRenderMode()
        .AddAdditionalAssemblies(typeof(PainFinder.Web.Pages.Dashboard).Assembly);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
