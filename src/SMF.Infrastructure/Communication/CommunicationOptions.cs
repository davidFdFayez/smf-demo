namespace SMF.Infrastructure.Communication;

/// <summary>
/// Email channel options. <see cref="Provider"/> selects the active backend
/// (Smtp, SendGrid, Logging). Per-provider sub-blocks let the same
/// <c>appsettings.json</c> describe both Mailtrap-style local SMTP and a
/// production SendGrid setup without needing to edit DI.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>One of: <c>Smtp</c>, <c>SendGrid</c>, <c>Logging</c>. Default is
    /// <c>Logging</c> so a fresh checkout never silently emails real users.</summary>
    public string Provider { get; set; } = "Logging";

    public string FromAddress { get; set; } = "no-reply@smf.local";
    public string FromName { get; set; } = "Saudi MuayThai Federation";

    public SmtpEmailOptions Smtp { get; set; } = new();
    public SendGridOptions SendGrid { get; set; } = new();
}

public sealed class SmtpEmailOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;            // Mailtrap dev default; production uses 587/465
    public bool UseStartTls { get; set; }            // 587 with STARTTLS
    public bool UseSslOnConnect { get; set; }        // 465 implicit TLS
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public sealed class SendGridOptions
{
    public string ApiKey { get; set; } = string.Empty;
    /// <summary>HTTPS endpoint of the SendGrid mail-send API. Hard-coded to v3
    /// in the sender; this property is here for configurability against
    /// regional / EU subdomains.</summary>
    public string ApiBaseUrl { get; set; } = "https://api.sendgrid.com";
}

/// <summary>SMS channel options. Provider switch mirrors the email layout.</summary>
public sealed class SmsOptions
{
    public const string SectionName = "Sms";
    public string Provider { get; set; } = "Logging";
    public string DefaultSenderId { get; set; } = "SMF";

    public UnifonicOptions Unifonic { get; set; } = new();
    public TwilioOptions Twilio { get; set; } = new();
}

public sealed class UnifonicOptions
{
    /// <summary>Unifonic AppSid (the integration's API key).</summary>
    public string AppSid { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://el.cloud.unifonic.com";
}

public sealed class TwilioOptions
{
    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string FromPhoneE164 { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://api.twilio.com";
}

/// <summary>FCM HTTP v1 options. <see cref="ServiceAccountJson"/> is the raw
/// JSON of a Google Cloud service account with the
/// <c>roles/firebasecloudmessaging.admin</c> role. When empty the no-op
/// stub registers and pushes are recorded but not actually sent — avoids
/// surprise calls from dev environments.</summary>
public sealed class PushOptions
{
    public const string SectionName = "Push";
    public string Provider { get; set; } = "Logging";
    public string? ServiceAccountJson { get; set; }
    /// <summary>Path to the service-account JSON file. Used as a fallback when
    /// <see cref="ServiceAccountJson"/> isn't supplied directly (handy for
    /// docker-mounted secrets).</summary>
    public string? ServiceAccountJsonPath { get; set; }
    public string ProjectId { get; set; } = string.Empty;
}

/// <summary>AI chatbot options. The default points at OpenAI; swap
/// <see cref="BaseUrl"/> for an Azure OpenAI deployment URL to get
/// MENA data-residency without changing code.</summary>
public sealed class ChatbotOptions
{
    public const string SectionName = "Chatbot";
    public string Provider { get; set; } = "OpenAI";
    public string BaseUrl { get; set; } = "https://api.openai.com";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o-mini";
    public double Temperature { get; set; } = 0.2;
    public int MaxTokens { get; set; } = 600;

    public string SystemPrompt { get; set; } =
        """
        You are the Saudi MuayThai Federation (SMF) assistant. Answer questions
        about membership registration, events, tournaments, scoring rules,
        coaching/refereeing licences, the federation store and safeguarding —
        in the same language the user wrote in (Arabic or English). When you
        do not know the answer, say so honestly and suggest contacting
        info@smf.sa. Keep replies concise and friendly. Never invent prices,
        event dates, or contact information.
        """;
}
