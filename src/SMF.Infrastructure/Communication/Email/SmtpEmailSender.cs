using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using SMF.Application.Common.Interfaces;

namespace SMF.Infrastructure.Communication.Email;

/// <summary>
/// SMTP transport via MailKit. Compatible with Mailtrap (dev), Office 365,
/// Gmail (with App Passwords), Postfix and any other RFC-5321 server. Picks
/// up host/port + auth from <see cref="EmailOptions.Smtp"/>.
/// </summary>
internal sealed class SmtpEmailSender : IEmailSender
{
    private readonly IOptionsMonitor<EmailOptions> _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptionsMonitor<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<EmailDeliveryResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        var smtp = opts.Smtp;

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(opts.FromName, opts.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName ?? "", message.ToAddress));
        mime.Subject = message.Subject;

        var bodyBuilder = new BodyBuilder();
        if (message.IsHtml) bodyBuilder.HtmlBody = message.Body;
        else                bodyBuilder.TextBody = message.Body;
        mime.Body = bodyBuilder.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            // STARTTLS on 587 (the modern recommendation), implicit TLS on 465,
            // bare 25 only when the operator has explicitly opted out of both.
            var secure = smtp.UseSslOnConnect ? SecureSocketOptions.SslOnConnect
                       : smtp.UseStartTls     ? SecureSocketOptions.StartTls
                       : SecureSocketOptions.Auto;

            await client.ConnectAsync(smtp.Host, smtp.Port, secure, cancellationToken);

            if (!string.IsNullOrWhiteSpace(smtp.Username))
                await client.AuthenticateAsync(smtp.Username, smtp.Password ?? "", cancellationToken);

            var providerId = await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            return new EmailDeliveryResult(true, providerId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP delivery failed to {Recipient}", message.ToAddress);
            return new EmailDeliveryResult(false, null, ex.Message);
        }
    }
}
