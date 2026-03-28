using Db;
using Db.Entities;
using Serilog;

namespace Api.Services;

/// <summary>
/// Development email service that logs emails to console and stores them in the email_log table.
/// No real emails are sent.
/// </summary>
public class MockEmailService(EventPlatformDbContext context) : IEmailService
{
    public async Task SendAsync(string recipient, string subject, string body)
    {
        Log.Information("[MockEmail] To: {Recipient} | Subject: {Subject}", recipient, subject);

        context.EmailLogs.Add(new EmailLog
        {
            Id = Guid.NewGuid(),
            Recipient = recipient,
            Subject = subject,
            Body = body,
            Status = "sent"
        });
        await context.SaveChangesAsync();
    }
}
