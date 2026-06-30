using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SMF.Api;
using SMF.Api.Auth;
using SMF.Api.Endpoints;
using SMF.Api.Hubs;
using SMF.Api.Middleware;
using SMF.Application;
using SMF.Infrastructure;
using SMF.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

const string WebCorsPolicy = "web-ui";

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var cloudDemo = builder.Environment.IsEnvironment("CloudDemo");
if (string.IsNullOrWhiteSpace(connectionString)
    && !builder.Environment.IsEnvironment("Testing")
    && !cloudDemo)
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is not configured. " +
        "Set it in appsettings.json or via the ConnectionStrings__DefaultConnection environment variable.");
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// JWT authentication — the hub requires an authenticated connection.
builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey),
        "Jwt:SigningKey must be configured.")
    .ValidateOnStart();

builder.Services.AddSingleton<JwtTokenService>();

// Real-time bracket fan-out (PDF Phase 3 → bracket engine). Hub is registered
// so MediatR handlers in the Application project can push via the interface
// without any direct SignalR reference.
builder.Services.AddScoped<SMF.Application.Common.Interfaces.ITournamentBroadcaster,
    SignalRTournamentBroadcaster>();

// Compliance: URL builder needs HttpContext to resolve host fallback.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<
    SMF.Application.Features.Compliance.ParentalConsent.IComplianceUrlBuilder,
    SMF.Api.Compliance.HttpComplianceUrlBuilder>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                  ?? new JwtOptions();

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            // Map the sub claim onto HttpContext.User.Identity.Name / hub's UserIdentifier.
            NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier
        };

        // Browsers can't attach Authorization headers to WebSocket upgrade
        // requests, so SignalR conventionally passes the JWT via ?access_token=...
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken)
                    && (path.StartsWithSegments(MatchScoringHub.Path)
                        || path.StartsWithSegments(TournamentHub.Path)))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// SignalR for the real-time scoring hub.
builder.Services
    .AddSignalR(options =>
    {
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        options.KeepAliveInterval = TimeSpan.FromSeconds(10);
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    })
    .AddMessagePackProtocol();

builder.Services.Configure<JsonOptions>(opts =>
{
    opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    opts.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(WebCorsPolicy, policy =>
    {
        if (builder.Environment.IsEnvironment("CloudDemo"))
        {
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
            return;
        }

        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                      ?? new[] { "http://localhost:5173" };

        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// MVC controllers — used by PaymentCallbackController (webhook). The rest of
// the surface stays on Minimal APIs.
builder.Services
    .AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (db.Database.IsInMemory())
    {
        logger.LogInformation(
            "Using InMemory database (no connection string configured — testing/dev fallback).");
        db.Database.EnsureCreated();
    }
    else
    {
        var target = db.Database.GetDbConnection();
        logger.LogInformation(
            "Using relational database '{Database}' on '{DataSource}' (provider: {Provider}).",
            target.Database, target.DataSource, db.Database.ProviderName);

        db.Database.Migrate();
    }

    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("CloudDemo"))
    {
        await DevSeeder.SeedAsync(db, logger);
    }
}

app.UseCors(WebCorsPolicy);
app.UseMiddleware<ExceptionHandlingMiddleware>();

// White-label tenant resolver — must run before authentication so handlers
// and rendering pipelines can read ITenantContext for branding decisions.
// See SMF.Api/Middleware/TenantResolutionMiddleware.cs for resolution order.
app.UseMiddleware<SMF.Api.Middleware.TenantResolutionMiddleware>();

// Governance documents (and any future uploads) are served as static files
// from the configured local-storage root. Cloud adapters bypass this since
// they hand out signed URLs pointing at the bucket directly.
{
    var complianceOpts = app.Services
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<SMF.Infrastructure.Compliance.ComplianceOptions>>()
        .Value;
    var storageRoot = complianceOpts.Storage.LocalRoot;
    var resolved = Path.IsPathRooted(storageRoot)
        ? storageRoot
        : Path.Combine(app.Environment.ContentRootPath, storageRoot);

    // The directory may already exist (mounted volume) or live on a
    // read-only filesystem in some environments; either case is fine —
    // we only need it to exist before PhysicalFileProvider opens it.
    try
    {
        Directory.CreateDirectory(resolved);
    }
    catch (UnauthorizedAccessException)
    {
        app.Logger.LogWarning(
            "Compliance upload directory '{Path}' is not writable for the current user; " +
            "uploads will fail until the volume permissions are corrected.",
            resolved);
    }

    if (Directory.Exists(resolved))
    {
        app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(resolved),
            RequestPath  = complianceOpts.Storage.PublicUrlPrefix,
            ServeUnknownFileTypes = false
        });
    }
}

app.UseAuthentication();
app.UseAuthorization();

app.MapMembersEndpoints();
app.MapMatchesEndpoints();
app.MapAuthEndpoints();
app.MapPaymentsEndpoints();
app.MapAdmissionsEndpoints();
app.MapClubsEndpoints();
app.MapEventsEndpoints();
app.MapTournamentsEndpoints();
app.MapCertificatesEndpoints();
app.MapNotificationsEndpoints();
app.MapAdminEndpoints();
app.MapNewsEndpoints();
app.MapSafeguardingEndpoints();
app.MapRankingsEndpoints();
app.MapExportEndpoints();
app.MapMembersMedicalEndpoints();
// Advanced features (PDF §9).
app.MapELearningEndpoints();
app.MapClubMicrositeEndpoints();
app.MapTenantsEndpoints();
app.MapAnalyticsEndpoints();
app.MapLiveStreamEndpoints();
// Financial system + e-commerce store.
app.MapStoreEndpoints();
app.MapInvoiceEndpoints();
app.MapCommunicationEndpoints();
app.MapComplianceEndpoints();
app.MapControllers();
app.MapHub<MatchScoringHub>(MatchScoringHub.Path);
app.MapHub<TournamentHub>(TournamentHub.Path);

app.Run();

/// <summary>
/// Exposed as a partial class so integration tests can reference
/// <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program { }
