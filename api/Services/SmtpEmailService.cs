using System.Net;
using System.Net.Mail;
using Serilog;

namespace Api.Services;

/// <summary>
/// Email service using SMTP (Gmail, Outlook, etc.).
/// Required settings: smtp_host, smtp_port, smtp_username, smtp_password.
/// Optional: email_from_address (defaults to smtp_username).
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

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(username, password),
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 15_000,
        };

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress),
            Subject = subject,
            Body = body,
            IsBodyHtml = body.TrimStart().StartsWith('<'),
        };
        message.To.Add(recipient);

        try
        {
            await client.SendMailAsync(message);
            Log.Information("[SMTP] Sent to {Recipient}: {Subject}", recipient, subject);
        }
        catch (SmtpException ex)
        {
            Log.Error(ex, "[SMTP] Failed to send email to {Recipient}: {Message}", recipient, ex.Message);
            throw new InvalidOperationException($"SMTP email failed: {ex.Message}", ex);
        }
    }
}
