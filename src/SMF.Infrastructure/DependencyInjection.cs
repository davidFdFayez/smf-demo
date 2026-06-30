using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Communication.Broadcasts;
using SMF.Infrastructure.Certificates;
using SMF.Infrastructure.Communication;
using SMF.Infrastructure.Compliance;
using SMF.Infrastructure.Communication.Chatbot;
using SMF.Infrastructure.Communication.Email;
using SMF.Infrastructure.Communication.Push;
using SMF.Infrastructure.Communication.Sms;
using SMF.Infrastructure.DigitalId;
using SMF.Infrastructure.Invoicing;
using SMF.Infrastructure.MultiTenancy;
using SMF.Infrastructure.Outbox;
using SMF.Infrastructure.Payments;
using SMF.Infrastructure.Persistence;
using SMF.Infrastructure.Persistence.Repositories;
using SMF.Infrastructure.Scoring;
using SMF.Infrastructure.Services;

namespace SMF.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                // Dev/test fallback — lets the solution run end-to-end without a DB.
                options.UseInMemoryDatabase("smf-dev");
            }
            else
            {
                options.UseSqlServer(connectionString, sql =>
                    sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
            }
        });

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<IMemberRepository, MemberRepository>();
        services.AddScoped<IMatchRepository, MatchRepository>();
        services.AddScoped<IMatchAuthorizationService, EfMatchAuthorizationService>();
        services.AddScoped<IScoringEventStore, EfScoringEventStore>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IBillingSequenceRepository, BillingSequenceRepository>();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        // Payment gateway — options + active provider. The Provider option
        // selects between the in-memory Mada sandbox and the real Tap
        // Payments adapter at runtime; both implementations stay registered
        // so integration tests can target either.
        services
            .AddOptions<PaymentGatewayOptions>()
            .Bind(configuration.GetSection(PaymentGatewayOptions.SectionName));

        services.AddSingleton<MadaPaymentGatewayService>();
        services.AddHttpClient(TapPaymentGatewayService.HttpClientName, (sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<PaymentGatewayOptions>>().CurrentValue;
            if (!string.IsNullOrWhiteSpace(opts.TapApiBaseUrl))
                client.BaseAddress = new Uri(opts.TapApiBaseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(20);
        });
        services.AddSingleton<TapPaymentGatewayService>();
        services.AddSingleton<IPaymentGatewayService>(sp =>
        {
            var providerName = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<PaymentGatewayOptions>>()
                .CurrentValue.Provider?.Trim();

            return string.Equals(providerName, "Tap", StringComparison.OrdinalIgnoreCase)
                ? sp.GetRequiredService<TapPaymentGatewayService>()
                : sp.GetRequiredService<MadaPaymentGatewayService>();
        });
        services.AddSingleton<WebhookSignatureVerifier>();

        // Transactional outbox — durable fan-out for domain events such as
        // PaymentSuccessfulEvent. Scoped writer + processor share the
        // request's DbContext; singleton hosted service drives the poll loop.
        services
            .AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection(OutboxOptions.SectionName));

        services.AddScoped<IOutbox, EfOutbox>();
        services.AddScoped<IOutboxProcessor, OutboxProcessor>();
        services.AddHostedService<OutboxHostedService>();

        // Digital ID token service — HMAC-SHA256 issuer/verifier for the
        // offline QR accreditation. Singleton because it's stateless and
        // lets IOptionsMonitor pick up secret rotations at runtime.
        services
            .AddOptions<DigitalIdOptions>()
            .Bind(configuration.GetSection(DigitalIdOptions.SectionName));

        services.AddSingleton<IDigitalIdTokenService, HmacDigitalIdTokenService>();

        // ── Communication & engagement ─────────────────────────────────
        // Channel-specific senders (email, SMS, push) are picked at runtime
        // by the Provider switch in each section. Defaults are dev-safe
        // logging stubs so a fresh checkout doesn't accidentally email
        // real users from a developer's laptop.
        services
            .AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName));
        services
            .AddOptions<SmsOptions>()
            .Bind(configuration.GetSection(SmsOptions.SectionName));
        services
            .AddOptions<PushOptions>()
            .Bind(configuration.GetSection(PushOptions.SectionName));
        services
            .AddOptions<ChatbotOptions>()
            .Bind(configuration.GetSection(ChatbotOptions.SectionName));

        // Email: HTTP clients + sender selection.
        services.AddHttpClient(SendGridEmailSender.HttpClientName, (sp, http) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<EmailOptions>>().CurrentValue;
            if (!string.IsNullOrWhiteSpace(opts.SendGrid.ApiBaseUrl))
                http.BaseAddress = new Uri(opts.SendGrid.ApiBaseUrl, UriKind.Absolute);
            http.Timeout = TimeSpan.FromSeconds(20);
        });
        services.AddSingleton<LoggingEmailSender>();
        services.AddSingleton<SmtpEmailSender>();
        services.AddSingleton<SendGridEmailSender>();
        services.AddSingleton<IEmailSender>(sp =>
        {
            var name = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<EmailOptions>>()
                .CurrentValue.Provider?.Trim();
            return name switch
            {
                var x when string.Equals(x, "Smtp",     StringComparison.OrdinalIgnoreCase) => sp.GetRequiredService<SmtpEmailSender>(),
                var x when string.Equals(x, "SendGrid", StringComparison.OrdinalIgnoreCase) => sp.GetRequiredService<SendGridEmailSender>(),
                _ => (IEmailSender)sp.GetRequiredService<LoggingEmailSender>()
            };
        });

        // SMS: HTTP clients + sender selection.
        services.AddHttpClient(UnifonicSmsSender.HttpClientName, (sp, http) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<SmsOptions>>().CurrentValue;
            if (!string.IsNullOrWhiteSpace(opts.Unifonic.ApiBaseUrl))
                http.BaseAddress = new Uri(opts.Unifonic.ApiBaseUrl, UriKind.Absolute);
            http.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddHttpClient(TwilioSmsSender.HttpClientName, (sp, http) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<SmsOptions>>().CurrentValue;
            if (!string.IsNullOrWhiteSpace(opts.Twilio.ApiBaseUrl))
                http.BaseAddress = new Uri(opts.Twilio.ApiBaseUrl, UriKind.Absolute);
            http.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddSingleton<LoggingSmsSender>();
        services.AddSingleton<UnifonicSmsSender>();
        services.AddSingleton<TwilioSmsSender>();
        services.AddSingleton<ISmsSender>(sp =>
        {
            var name = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<SmsOptions>>()
                .CurrentValue.Provider?.Trim();
            return name switch
            {
                var x when string.Equals(x, "Unifonic", StringComparison.OrdinalIgnoreCase) => sp.GetRequiredService<UnifonicSmsSender>(),
                var x when string.Equals(x, "Twilio",   StringComparison.OrdinalIgnoreCase) => sp.GetRequiredService<TwilioSmsSender>(),
                _ => (ISmsSender)sp.GetRequiredService<LoggingSmsSender>()
            };
        });

        // Push: FCM HTTP v1 with Google OAuth2 service-account JWT.
        services.AddHttpClient(FcmPushSender.HttpClientName, http =>
        {
            http.BaseAddress = new Uri("https://fcm.googleapis.com", UriKind.Absolute);
            http.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddHttpClient(FcmAccessTokenProvider.HttpClientName, http =>
        {
            http.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddSingleton<FcmAccessTokenProvider>();
        services.AddSingleton<LoggingPushSender>();
        services.AddSingleton<FcmPushSender>();
        services.AddSingleton<IPushSender>(sp =>
        {
            var name = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<PushOptions>>()
                .CurrentValue.Provider?.Trim();
            return string.Equals(name, "Fcm", StringComparison.OrdinalIgnoreCase)
                ? sp.GetRequiredService<FcmPushSender>()
                : sp.GetRequiredService<LoggingPushSender>();
        });

        // Chatbot: OpenAI streaming with a no-op stub fallback when no key.
        services.AddHttpClient(OpenAiChatbotService.HttpClientName, (sp, http) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<ChatbotOptions>>().CurrentValue;
            if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
                http.BaseAddress = new Uri(opts.BaseUrl, UriKind.Absolute);
            // OpenAI streaming responses can run for many seconds.
            http.Timeout = TimeSpan.FromMinutes(2);
        });
        services.AddSingleton<NoOpChatbotService>();
        services.AddSingleton<OpenAiChatbotService>();
        services.AddSingleton<IChatbotService>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<ChatbotOptions>>().CurrentValue;
            return string.IsNullOrWhiteSpace(opts.ApiKey)
                ? sp.GetRequiredService<NoOpChatbotService>()
                : sp.GetRequiredService<OpenAiChatbotService>();
        });

        // Dispatcher: implements both the new INotificationDispatcher (used by
        // the broadcast pipeline) and the legacy INotificationService (used by
        // existing handlers like ActivateMember). A single class keeps the
        // audit-trail / fan-out logic in one place.
        services.AddScoped<BroadcastAudienceResolver>();
        services.AddSingleton<NotificationDispatcher>();
        services.AddSingleton<INotificationDispatcher>(sp => sp.GetRequiredService<NotificationDispatcher>());
        services.AddSingleton<INotificationService>(sp => sp.GetRequiredService<NotificationDispatcher>());

        services
            .AddOptions<CertificateRenderingOptions>()
            .Bind(configuration.GetSection(CertificateRenderingOptions.SectionName));

        // Renderer selection: PDF (QuestPDF) by default, with the HTML
        // fallback always registered for environments that opt out of the
        // native dependency. The active implementation is the *last* one
        // resolved by ICertificateRenderer.
        services.AddSingleton<HtmlCertificateRenderer>();
        services.AddSingleton<PdfCertificateRenderer>();
        services.AddSingleton<ICertificateVerifyUrlBuilder, CertificateVerifyUrlBuilder>();
        services.AddSingleton<ICertificateRenderer>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<CertificateRenderingOptions>>().CurrentValue;
            if (!opts.UsePdfRenderer) return sp.GetRequiredService<HtmlCertificateRenderer>();

            // Setting the licence is idempotent; QuestPdfInvoiceRenderer may
            // also have set it. Both default to Community — bump in Program.cs
            // after purchasing a Professional licence.
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
            return sp.GetRequiredService<PdfCertificateRenderer>();
        });

        // Invoicing settings (issuer block + VAT rate) and renderers.
        // HtmlInvoiceRenderer is always registered as a fallback; QuestPDF is
        // gated by configuration so it can be turned off for environments
        // that don't want the native dependency.
        services
            .AddOptions<InvoicingOptions>()
            .Bind(configuration.GetSection(InvoicingOptions.SectionName));

        services.AddSingleton<IInvoicingSettings, InvoicingSettingsAdapter>();
        services.AddSingleton<IInvoiceRenderer, HtmlInvoiceRenderer>();

        var invoicingOpts = configuration
            .GetSection(InvoicingOptions.SectionName)
            .Get<InvoicingOptions>() ?? new InvoicingOptions();
        if (invoicingOpts.EnableQuestPdfRenderer)
        {
            // Default to Community licence; bump to Professional in
            // Program.cs after you've purchased one. Setting it here keeps
            // QuestPDF from logging a noisy warning at first render.
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
            services.AddSingleton<IInvoiceRenderer, QuestPdfInvoiceRenderer>();
        }
        services.AddSingleton<IInvoiceRendererResolver, InvoiceRendererResolver>();

        // ── Compliance & governance ────────────────────────────────────
        // HMAC consent signer + pluggable file storage. Storage provider is
        // selected at runtime so we can swap in S3/Azure Blob later without
        // touching feature handlers.
        services
            .AddOptions<ComplianceOptions>()
            .Bind(configuration.GetSection(ComplianceOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.SigningSecret) && o.SigningSecret.Length >= 32,
                "Compliance:SigningSecret must be at least 32 characters.")
            .ValidateOnStart();

        services.AddSingleton<IConsentSigner, HmacConsentSigner>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        // ── Multi-tenant white-label context ──────────────────────────
        // Scoped so each HTTP request gets a fresh, mutable context that
        // the API tenant-resolution middleware populates before any
        // handler runs. Single-tenant callers see IsResolved=false.
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddMemoryCache();

        return services;
    }
}
