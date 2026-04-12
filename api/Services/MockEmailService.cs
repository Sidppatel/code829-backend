using Db.Repositories.StoredProcedures;
using Serilog;

namespace Api.Services;

public class MockEmailService(ILogProcedures logProc) : IEmailService
{
    public async Task SendAsync(string recipient, string subject, string body)
    {
        Log.Information("[MockEmail] To: {Recipient} | Subject: {Subject}", recipient, subject);
        await logProc.CreateEmailLogAsync(recipient, subject, body, "sent");
    }
}
