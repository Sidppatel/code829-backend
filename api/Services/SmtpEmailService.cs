using Serilog;

namespace Api.Services;

/// <summary>
/// Production email service using SMTP or a provider API (SendGrid, SES, etc.).
/// Requires email_api_key from DB settings. Currently validates config and throws
/// if misconfigured, ensuring mock services are not used in production.
/// </summary>
public class SmtpEmailService(ISettingsService settings) : IEmailService
{
    public async Task SendAsync(string recipient, string subject, string body)
    {
        var apiKey = await settings.GetAsync("email_api_key");
        if (apiKey == "MOCK_DEV")
            throw new InvalidOperationException("Email service is not configured — email_api_key is still MOCK_DEV");

        var fromAddress = await settings.GetOrDefaultAsync("email_from_address", "noreply@code829.local");

        // Production implementation would call SendGrid/SES/SMTP here:
        // await client.SendEmailAsync(new EmailMessage { From = fromAddress, To = recipient, ... });
        Log.Information("[Email] Sending to {Recipient}: {Subject}", recipient, subject);
        throw new NotImplementedException("Email provider integration pending — configure email_api_key");
    }
}
