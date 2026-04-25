namespace Api.Services;

public interface IAlertService
{
    /// <summary>
    /// Raise an operational alert. In dev / no-op mode this logs at Error severity.
    /// Production replacements will fan out to Sentry / Resend / PagerDuty.
    /// </summary>
    /// <param name="code">Stable machine-readable code, e.g. "PAYMENT_AMOUNT_MISMATCH".</param>
    /// <param name="message">Human-readable summary.</param>
    /// <param name="context">Optional key/value bag (purchase id, payment intent id, etc.).</param>
    Task RaiseAsync(
        string code,
        string message,
        IDictionary<string, string>? context = null,
        CancellationToken ct = default);
}
