using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Helpers;
using Db.Repositories.StoredProcedures;
using Serilog;

namespace Api.Services;

public class ResendEmailService(ISecretsProvider secrets, ISettingsService settings, ILogProcedures logProc) : IEmailService
{
    private static readonly HttpClient Http = new();

    public async Task SendAsync(string recipient, string subject, string body)
    {
        var apiKey = secrets.ResendApiKey;
        var fromAddress = await settings.GetOrDefaultAsync("email_from_address", "noreply@code829.com") ?? "noreply@code829.com";

        var isHtml = body.TrimStart().StartsWith('<');
        var payload = isHtml
            ? JsonSerializer.Serialize(new { from = fromAddress, to = new[] { recipient }, subject, html = body })
            : JsonSerializer.Serialize(new { from = fromAddress, to = new[] { recipient }, subject, text = body });

        var response = await RetryHelper.WithRetryAsync(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
            return await Http.SendAsync(request);
        }, context: "Resend email");
        var responseBody = await response.Content.ReadAsStringAsync();

        var status = response.IsSuccessStatusCode ? "sent" : "failed";

        await logProc.CreateEmailLogAsync(recipient, subject, body, status);

        if (!response.IsSuccessStatusCode)
        {
            Log.Error("[Resend] Failed to send email to {Recipient}: {Status} {Body}",
                recipient, response.StatusCode, responseBody);
            throw new InvalidOperationException($"Resend email failed: {response.StatusCode}");
        }

        Log.Information("[Resend] Sent to {Recipient}: {Subject}", recipient, subject);
    }
}
