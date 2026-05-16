using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Api.Exceptions;
using Api.Services;
using Contracts.DTOs;
using Contracts.Enums;
using Microsoft.Extensions.Hosting;
using Sentry;
using Serilog;

namespace Api.Middleware;

public class ErrorHandlingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAuditLogService auditLog)
    {
        try
        {
            await next(context);
        }
        catch (MalwareDetectedException ex)
        {
            var correlationId = context.TraceIdentifier;
            Log.Warning("Malware detected on {Method} {Path}: {Threat}", context.Request.Method, context.Request.Path, ex.Threat);
            context.Response.StatusCode = 422;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://datatracker.ietf.org/doc/html/rfc4918#section-11.2",
                title = "File rejected",
                status = 422,
                detail = "File rejected",
                correlationId
            });
            return;
        }
        catch (OperationCanceledException)
        {
            Log.Information("Request was cancelled on {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        catch (Exception ex)
        {
            var correlationId = context.TraceIdentifier;

            Log.Error(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);

            SentrySdk.CaptureException(ex);

            try
            {
                var metadata = new JsonObject
                {
                    ["severity"] = LogSeverity.Error.ToString(),
                    ["message"] = ex.Message,
                    ["exception_type"] = ex.GetType().FullName,
                    ["stack_trace"] = ex.StackTrace,
                    ["request_path"] = context.Request.Path.ToString(),
                    ["request_method"] = context.Request.Method,
                    ["status_code"] = 500,
                };

                Guid? actorId = null;
                var actorClaim = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (Guid.TryParse(actorClaim, out var parsedActorId)) actorId = parsedActorId;

                Guid? correlationGuid = null;
                if (Guid.TryParse(correlationId, out var parsedCorrelation)) correlationGuid = parsedCorrelation;

                await auditLog.LogAsync(
                    eventType: "Exception",
                    actorType: AuditActorType.System,
                    actorId: actorId,
                    subjectType: null,
                    subjectId: null,
                    action: ex.GetType().Name,
                    metadataJson: metadata.ToJsonString(),
                    ip: context.Connection.RemoteIpAddress?.ToString(),
                    correlationId: correlationGuid);
            }
            catch (Exception logEx)
            {
                Log.Error(logEx, "Failed to write exception audit log");
            }

            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
#if DEBUG
            var innerMsg = ex.InnerException?.Message;
            await context.Response.WriteAsJsonAsync(new
            {
                statusCode = 500,
                message = ex.Message,
                innerMessage = innerMsg,
                exceptionType = ex.GetType().FullName,
                correlationId
            });
#else
            var error = new ApiError(500, "An internal error occurred", CorrelationId: correlationId);
            await context.Response.WriteAsJsonAsync(error);
#endif
        }
    }
}
