using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Db.Repositories.StoredProcedures;

public class FeedbackProcedures(EventPlatformDbContext context) : IFeedbackProcedures
{
    public async Task<Guid> CreateFeedbackAsync(string name, string email, string type, string message, int? rating, Guid? userId, string? userAgent, string? ip, CancellationToken ct = default)
    {
        var result = await context.Database
            .SqlQueryRaw<Guid>(
                "SELECT sp_create_feedback(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7) AS \"Value\"",
                new NpgsqlParameter("p0", name),
                new NpgsqlParameter("p1", email),
                new NpgsqlParameter("p2", type),
                new NpgsqlParameter("p3", message),
                new NpgsqlParameter("p4", NpgsqlDbType.Integer) { Value = (object?)rating ?? DBNull.Value },
                new NpgsqlParameter("p5", NpgsqlDbType.Uuid) { Value = (object?)userId ?? DBNull.Value },
                new NpgsqlParameter("p6", NpgsqlDbType.Text) { Value = (object?)userAgent ?? DBNull.Value },
                new NpgsqlParameter("p7", NpgsqlDbType.Text) { Value = (object?)ip ?? DBNull.Value })
            .FirstAsync(ct);

        return result;
    }
}
