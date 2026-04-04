using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Serilog;

namespace Api.Services;

/// <summary>
/// Email service using SMTP via MailKit (Gmail, Outlook, etc.).
/// Required settings: smtp_host, smtp_port, smtp_username, smtp_password.
/// Optional: email_from_address (defaults to smtp_username).
///
/// Port guide:
///   587 → STARTTLS (upgrade plaintext to TLS after EHLO)
///   465 → Implicit SSL/TLS (encrypted from the start)
/// </summary>
public class SmtpEmailService(ISettingsService settings) : IEmailService
{
    public async Task SendAsync(string recipient, string subject, string body)
    {
        var host = await settings.GetAsync("smtp_host");
        var portStr = await settings.GetOrDefaultAsync("smtp_port", "587") ?? "587";
        var username = await settings.GetAsync("smtp_username");
        var password = await settings.GetAsync("smtp_password");
        var fromAddress = await settings.GetOrDefaultAsync("email_from_address") ?? username;

        if (!int.TryParse(portStr, out var port))
            port = 587;

        // Port 465 = implicit SSL, port 587 = STARTTLS, others = auto-detect
        var secureSocket = port switch
        {
            465 => SecureSocketOptions.SslOnConnect,
            587 => SecureSocketOptions.StartTls,
            _ => SecureSocketOptions.Auto,
        };

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(fromAddress));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = subject;

        var isHtml = body.TrimStart().StartsWith('<');
        message.Body = new TextPart(isHtml ? "html" : "plain") { Text = body };

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, secureSocket);
            await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            Log.Information("[SMTP] Sent to {Recipient}: {Subject}", recipient, subject);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[SMTP] Failed to send email to {Recipient}: {Message}", recipient, ex.Message);
            throw new InvalidOperationException($"SMTP email failed: {ex.Message}", ex);
        }
    }
}
