using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SMF.Infrastructure.Persistence;

namespace SMF.IntegrationTests;

/// <summary>
/// Hosts the real SMF.Api pipeline in-memory with a fresh, isolated
/// EF Core InMemory database per factory instance.
/// </summary>
public sealed class SmfWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"smf-tests-{Guid.NewGuid():N}";

    /// <summary>Known JWT signing key used for the Testing environment.</summary>
    public const string TestingSigningKey =
        "testing-only-signing-key-not-for-any-production-use-0123456789abcd";

    /// <summary>Known HMAC secret the payment webhook tests use to sign bodies.</summary>
    public const string TestingWebhookSecret =
        "testing-webhook-secret-0123456789abcdef-not-for-production";

    /// <summary>Known HMAC secret the Digital ID tests use to sign tokens.</summary>
    public const string TestingDigitalIdSecret =
        "testing-digital-id-secret-0123456789abcdef-not-for-production";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "smf-api",
                ["Jwt:Audience"] = "smf-clients",
                ["Jwt:SigningKey"] = TestingSigningKey,
                ["Jwt:TokenLifetimeMinutes"] = "60",
                ["Payments:WebhookSigningSecret"] = TestingWebhookSecret,
                ["Payments:WebhookSignatureHeader"] = "X-Smf-Signature",
                ["DigitalId:SigningSecret"] = TestingDigitalIdSecret,
                ["DigitalId:Lifetime"] = "00:00:10",
                ["DigitalId:ClockSkew"] = "00:00:02",
                // Integration tests drive IOutboxProcessor by hand so the
                // background poller doesn't race assertions or randomise
                // dispatch timing across runs.
                ["Outbox:RunHostedService"] = "false",
                ["Outbox:PollInterval"] = "00:00:00.050"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Replace whichever DbContext the production Program.cs wired up
            // (SQL Server or InMemory) with a dedicated isolated InMemory database.
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}
