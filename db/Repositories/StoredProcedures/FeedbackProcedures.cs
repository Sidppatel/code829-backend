using Microsoft.EntityFrameworkCore;

namespace Db.Repositories.StoredProcedures;

public class FeedbackProcedures(EventPlatformDbContext context) : IFeedbackProcedures
{
    public async Task<Guid> CreateFeedbackAsync(string name, string email, string type, string message, int? rating, Guid? userId, string? userAgent, string? ip, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_feedback(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7) AS \"Value\"",
                name, email, type, message,
                (object?)rating ?? DBNull.Value, (object?)userId ?? DBNull.Value,
                (object?)userAgent ?? DBNull.Value, (object?)ip ?? DBNull.Value)
            .FirstAsync(ct);

        return result;
    }
}
