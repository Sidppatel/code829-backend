namespace Api.Services;

public interface IAlertService
{
    Task RaiseAsync(
    string code,
    string message,
    IDictionary<string, string>? context = null,
    CancellationToken ct = default);
}
