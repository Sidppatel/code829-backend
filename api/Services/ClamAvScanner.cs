using nClam;
using Serilog;

namespace Api.Services;

public class ClamAvScanner : IMalwareScanner
{
    private readonly string _host;
    private readonly int _port;

    public ClamAvScanner(IConfiguration config)
    {
        _host = config["CLAMAV_HOST"] ?? throw new InvalidOperationException("CLAMAV_HOST required");
        _port = int.TryParse(config["CLAMAV_PORT"], out var p) ? p : 3310;
    }

    public async Task<ScanResult> ScanAsync(Stream content, CancellationToken ct = default)
    {
        var client = new ClamClient(_host, _port);
        try
        {
            var result = await client.SendAndScanFileAsync(content, ct);
            return result.Result switch
            {
                ClamScanResults.Clean => new ScanResult(true, null),
                ClamScanResults.VirusDetected => new ScanResult(false, result.InfectedFiles?.FirstOrDefault()?.VirusName ?? "unknown"),
                ClamScanResults.Error => throw new InvalidOperationException($"ClamAV scan error: {result.RawResult}"),
                _ => throw new InvalidOperationException($"Unexpected ClamAV result: {result.Result}")
            };
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            Log.Error(ex, "[ClamAV] scan transport failure against {Host}:{Port}", _host, _port);
            throw;
        }
    }
}

public class NoopMalwareScanner : IMalwareScanner
{
    public Task<ScanResult> ScanAsync(Stream content, CancellationToken ct = default)
        => Task.FromResult(new ScanResult(true, null));
}
