namespace Db.Repositories.StoredProcedures;

public interface IFeedbackProcedures
{
    Task<Guid> CreateFeedbackAsync(string name, string email, string type, string message, int? rating, Guid? userId, string? userAgent, string? ip, CancellationToken ct = default);
}
