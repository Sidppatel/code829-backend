using System.Net;
using System.Net.Mail;
using Serilog;

namespace Api.Services;

/// <summary>
/// Production email service using SMTP. Reads SMTP settings from ISettingsService.
/// Required settings: smtp_host, smtp_port, email_api_key (password), email_from_address.
/// </summary>
public class SmtpEmailService(ISettingsService settings) : IEmailService
{
    public async Task SendAsync(string recipient, string subject, string body)
    {
        var host = await settings.GetOrDefaultAsync("smtp_host", "");
        var portStr = await settings.GetOrDefaultAsync("smtp_port", "587");
        var password = await settings.GetAsync("email_api_key");
        var fromAddress = await settings.GetOrDefaultAsync("email_from_address", "noreply@code829.local") ?? "noreply@code829.local";

        if (string.IsNullOrEmpty(host) || password == "MOCK_DEV")
            throw new InvalidOperationException("Email service is not configured — set smtp_host and email_api_key in settings");

        var port = int.Parse(portStr ?? "587");

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(fromAddress, password),
            EnableSsl = true
        };

        var message = new MailMessage(fromAddress, recipient, subject, body);

        await client.SendMailAsync(message);
        Log.Information("[Email] Sent to {Recipient}: {Subject}", recipient, subject);
    }
}
