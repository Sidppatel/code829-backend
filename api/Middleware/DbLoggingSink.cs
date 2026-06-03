using Api.Services;
using Contracts.Enums;
using Serilog.Core;
using Serilog.Events;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Api.Middleware;

public class DbLoggingSink(IDbLoggingService loggingService) : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        // 1. Prevent recursion and infinite logging loops
        string sourceContext = string.Empty;
        if (logEvent.Properties.TryGetValue("SourceContext", out var sourceValue) && sourceValue is ScalarValue scalar)
        {
            sourceContext = scalar.Value?.ToString() ?? string.Empty;
            if (sourceContext.Contains("Microsoft.EntityFrameworkCore") ||
                sourceContext.Contains("Npgsql") ||
                sourceContext.Contains("DbLoggingWorker") ||
                sourceContext.Contains("DbLoggingService") ||
                sourceContext.Contains("DbLoggingSink") ||
                sourceContext.Contains("System.Net.Http"))
            {
                return; // Ignore EF Core, database, http, and logging service logs
            }
        }

        // 2. Extract correlation ID if available
        Guid? correlationGuid = null;
        if (logEvent.Properties.TryGetValue("CorrelationId", out var corrVal) && corrVal is ScalarValue corrScalar)
        {
            var corrStr = corrScalar.Value?.ToString();
            if (Guid.TryParse(corrStr, out var parsedCorr))
            {
                correlationGuid = parsedCorr;
            }
        }

        // 3. Extract user ID if enriched in LogContext
        Guid? userId = null;
        if (logEvent.Properties.TryGetValue("UserId", out var userVal) && userVal is ScalarValue userScalar)
        {
            var userStr = userScalar.Value?.ToString();
            if (Guid.TryParse(userStr, out var parsedUser))
            {
                userId = parsedUser;
            }
        }

        // 4. Format log severity
        var severity = logEvent.Level switch
        {
            LogEventLevel.Warning => LogSeverity.Warning.ToString(),
            LogEventLevel.Error => LogSeverity.Error.ToString(),
            LogEventLevel.Fatal => LogSeverity.Critical.ToString(),
            _ => LogSeverity.Warning.ToString()
        };

        // 5. Build Metadata JSON payload
        var message = logEvent.RenderMessage();
        var metadata = new JsonObject
        {
            ["severity"] = severity,
            ["message"] = message,
            ["source_context"] = sourceContext
        };

        if (logEvent.Exception != null)
        {
            metadata["exception_type"] = logEvent.Exception.GetType().FullName;
            metadata["stack_trace"] = logEvent.Exception.StackTrace;
        }

        // Extract extra log properties (except SourceContext, CorrelationId, UserId)
        var extraProps = new JsonObject();
        foreach (var prop in logEvent.Properties)
        {
            if (prop.Key == "SourceContext" || prop.Key == "CorrelationId" || prop.Key == "UserId") continue;
            
            if (prop.Value is ScalarValue sv)
            {
                extraProps[prop.Key] = JsonValue.Create(sv.Value);
            }
            else
            {
                extraProps[prop.Key] = prop.Value.ToString();
            }
        }
        if (extraProps.Count > 0)
        {
            metadata["properties"] = extraProps;
        }

        var entry = new LogQueueEntry
        {
            EventType = logEvent.Exception != null ? "Exception" : "BackendLog",
            ActorType = AuditActorType.Developer,
            ActorId = userId,
            Action = logEvent.Exception != null ? logEvent.Exception.GetType().Name : "BackendLog",
            MetadataJson = metadata.ToJsonString(),
            CorrelationId = correlationGuid
        };

        loggingService.Enqueue(entry);
    }
}
