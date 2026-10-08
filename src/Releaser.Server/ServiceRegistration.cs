using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Releaser.Server.Features.Admin;
using Releaser.Server.Features.Audit;
using Releaser.Server.Features.Feed;
using Releaser.Server.Features.Releases;
using Releaser.Server.Infrastructure.Auth;
using Releaser.Server.Infrastructure.Caching;
using Releaser.Server.Infrastructure.Http;
using Releaser.Server.Infrastructure.Manifests;
using Releaser.Server.Infrastructure.Persistence;
using Serilog;

namespace Releaser.Server;

internal static class ServiceRegistration
{
    public static WebApplicationBuilder AddReleaser(this WebApplicationBuilder builder)
    {
        builder.Services.AddSerilog((services, logger) => logger.ReadFrom.Configuration(builder.Configuration).ReadFrom.Services(services));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.ConfigureHttpJsonOptions(json =>
        {
            json.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            json.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        });
        // Stop each rule chain at its first failure, so e.g. NotEmpty() on a missing list prevents later Must(...) rules
        // from dereferencing null (which would surface as a 500 instead of a 400).
        ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;
        builder.Services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);
        builder.Services.AddOpenApi();
        builder.AddOptions();
        builder.AddPersistence();
        builder.AddCaching();
        builder.AddAdminAuthentication();
        builder.AddManifestFetching();
        builder.AddTelemetry();
        builder.Services.AddReleaserRateLimiting(builder.Configuration.GetSection(RateLimitOptions.Section).Get<RateLimitOptions>() ?? new RateLimitOptions());
        builder.Services.AddScoped<IAuditLog, AuditLog>();
        builder.Services.AddScoped<ReleaseRegistration>();
        builder.Services.AddScoped<ReleaseObliteration>();
        builder.Services.AddSingleton<ContextTokenValidator>();
        builder.Services.AddSingleton<FeedManifestRenderer>();
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            // Only honoured when Platform:TrustForwardedHeaders is true (behind your own reverse proxy).
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });
        builder.Services.AddHealthChecks().AddDbContextCheck<ReleaserDbContext>("postgres", tags: ["ready"]);
        return builder;
    }

    private static void AddOptions(this WebApplicationBuilder builder)
    {
        Bind<ResolutionOptions>(builder, ResolutionOptions.Section);
        Bind<ManifestOptions>(builder, ManifestOptions.Section);
        Bind<ContextTokenOptions>(builder, ContextTokenOptions.Section);
        Bind<RateLimitOptions>(builder, RateLimitOptions.Section);
        Bind<BootstrapOptions>(builder, BootstrapOptions.Section);
        Bind<DatabaseOptions>(builder, DatabaseOptions.Section);
        Bind<PlatformOptions>(builder, PlatformOptions.Section);
    }

    private static void Bind<TOptions>(WebApplicationBuilder builder, string section) where TOptions : class =>
        builder.Services.AddOptions<TOptions>().Bind(builder.Configuration.GetSection(section)).ValidateDataAnnotations().ValidateOnStart();

    private static void AddPersistence(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("Releaser")
            ?? throw new InvalidOperationException("ConnectionStrings:Releaser is required.");
        builder.Services.AddDbContext<ReleaserDbContext>(options => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention());
        builder.Services.AddScoped<FeedSnapshotLoader>();
        var dataProtection = builder.Services.AddDataProtection().SetApplicationName("releaser");
        if (!BuildContext.IsGeneratingOpenApi)
        {
            dataProtection.PersistKeysToDbContext<ReleaserDbContext>();
        }
    }

    private static void AddCaching(this WebApplicationBuilder builder)
    {
        builder.Services.AddMemoryCache();
        builder.Services.AddHybridCache(options =>
        {
            options.MaximumPayloadBytes = 16 * 1024 * 1024;
            options.DefaultEntryOptions = new() { Expiration = TimeSpan.FromHours(1), LocalCacheExpiration = TimeSpan.FromMinutes(10) };
        });
        var redis = builder.Configuration["Redis:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(redis))
        {
            // Optional L2 (ADR 0008). Keys are versioned, so Redis never holds data that could be stale.
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redis;
                options.InstanceName = "releaser:";
            });
        }
        builder.Services.AddSingleton<FeedSnapshotCache>();
        builder.Services.AddSingleton<IConfigVersionListener>(services => services.GetRequiredService<FeedSnapshotCache>());
    }

    private static void AddAdminAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        builder.Services
            .AddIdentityCore<AdminUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ReleaserDbContext>()
            .AddSignInManager();
        builder.Services.ConfigureApplicationCookie(cookie =>
        {
            cookie.Cookie.Name = "releaser.auth";
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SameSite = SameSiteMode.Strict;
            cookie.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            cookie.ExpireTimeSpan = TimeSpan.FromHours(8);
            cookie.SlidingExpiration = true;
            cookie.Events.OnRedirectToLogin = context => Reject(context.Response, StatusCodes.Status401Unauthorized);
            cookie.Events.OnRedirectToAccessDenied = context => Reject(context.Response, StatusCodes.Status403Forbidden);
        });
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(AdminRoles.CanView, policy => policy.RequireRole(AdminRoles.All))
            .AddPolicy(AdminRoles.CanManageReleases, policy => policy.RequireRole(AdminRoles.Admin, AdminRoles.ReleaseManager))
            .AddPolicy(AdminRoles.CanManageUsers, policy => policy.RequireRole(AdminRoles.Admin))
            .AddPolicy(AdminRoles.CanObliterateReleases, policy => policy.RequireRole(AdminRoles.Admin));
    }

    private static Task Reject(HttpResponse response, int statusCode)
    {
        response.StatusCode = statusCode;
        return Task.CompletedTask;
    }

    private static void AddManifestFetching(this WebApplicationBuilder builder) =>
        builder.Services.AddHttpClient<IManifestFetcher, HttpManifestFetcher>(client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Releaser-ManifestFetcher/0.1");
                client.Timeout = Timeout.InfiniteTimeSpan; // enforced per request by the fetcher
            })
            .ConfigurePrimaryHttpMessageHandler(services => HttpManifestFetcher.CreateHandler(services.GetRequiredService<IOptions<ManifestOptions>>().Value));

    private static void AddTelemetry(this WebApplicationBuilder builder)
    {
        var endpoint = builder.Configuration["Telemetry:OtlpEndpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return;
        }
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("releaser"))
            .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation().AddOtlpExporter(o => o.Endpoint = new Uri(endpoint)))
            .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation().AddOtlpExporter(o => o.Endpoint = new Uri(endpoint)));
    }
}
