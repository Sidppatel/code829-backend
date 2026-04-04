using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Db;
using Db.Entities;
using Serilog;

namespace Api.Services;

/// <summary>
/// Email service using Resend HTTP API.
/// Required settings: resend_api_key, email_from_address.
/// </summary>
public class ResendEmailService(ISettingsService settings, EventPlatformDbContext context) : IEmailService
{
    private static readonly HttpClient Http = new();

    public async Task SendAsync(string recipient, string subject, string body)
    {
        var apiKey = await settings.GetAsync("resend_api_key");
        var fromAddress = await settings.GetOrDefaultAsync("email_from_address", "noreply@code829.com") ?? "noreply@code829.com";

        var isHtml = body.TrimStart().StartsWith('<');
        var payload = isHtml
            ? JsonSerializer.Serialize(new { from = fromAddress, to = new[] { recipient }, subject, html = body })
            : JsonSerializer.Serialize(new { from = fromAddress, to = new[] { recipient }, subject, text = body });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await Http.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        var status = response.IsSuccessStatusCode ? "sent" : "failed";

        // Log to database
        context.EmailLogs.Add(new EmailLog
        {
            Id = Guid.NewGuid(),
            Recipient = recipient,
            Subject = subject,
            Body = body,
            Status = status
        });
        await context.SaveChangesAsync();

        if (!response.IsSuccessStatusCode)
        {
            Log.Error("[Resend] Failed to send email to {Recipient}: {Status} {Body}",
                recipient, response.StatusCode, responseBody);
            throw new InvalidOperationException($"Resend email failed: {response.StatusCode}");
        }

        Log.Information("[Resend] Sent to {Recipient}: {Subject}", recipient, subject);
    }
}
