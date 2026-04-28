namespace Db.Repositories.StoredProcedures;

public interface ISettingsProcedures
{
    Task UpsertSettingAsync(string key, string encryptedValue, string? description = null, CancellationToken ct = default);
}
