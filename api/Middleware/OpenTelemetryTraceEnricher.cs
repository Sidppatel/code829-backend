using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace Api.Middleware;

public sealed class OpenTelemetryTraceEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory pf)
    {
        var activity = Activity.Current;
        if (activity is null) return;
        logEvent.AddPropertyIfAbsent(pf.CreateProperty("TraceId", activity.TraceId.ToString()));
        logEvent.AddPropertyIfAbsent(pf.CreateProperty("SpanId", activity.SpanId.ToString()));
    }
}
