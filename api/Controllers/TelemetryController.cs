using Api.Services;
using Contracts.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Api.Controllers;

[Asp.Versioning.ApiVersion("1.0")]
[ApiController]
[Route("v{version:apiVersion}/telemetry")]
[AllowAnonymous]
public class TelemetryController(IDbLoggingService loggingService) : ControllerBase
{
    public record ClientLogPayload(
        string Level,
        string Message,
        string? StackTrace,
        string? Url,
        string? UserAgent,
        int? StatusCode
    );

    public record ClientVisitPayload(
        string Path,
        string? Referrer,
        string? ScreenResolution,
        string? Portal
    );

    [HttpPost("log")]
    public IActionResult SubmitClientLog([FromBody] ClientLogPayload payload)
    {
        // Ignore telemetry logging failures to prevent infinite loops
        if (payload.Message.Contains("/telemetry/log") || payload.Message.Contains("/telemetry/visit"))
            return Ok();

        var severity = payload.Level switch
        {
            "Warning" or "warn" => LogSeverity.Warning.ToString(),
            "Error" or "error" or "uncaught" or "rejection" => LogSeverity.Error.ToString(),
            "Critical" => LogSeverity.Critical.ToString(),
            _ => LogSeverity.Error.ToString()
        };

        var (browser, os) = ParseUserAgent(payload.UserAgent ?? Request.Headers.UserAgent.ToString());

        var metadata = new JsonObject
        {
            ["severity"] = severity,
            ["message"] = payload.Message,
            ["exception_type"] = payload.Level == "rejection" ? "UnhandledPromiseRejection" : "ClientError",
            ["stack_trace"] = payload.StackTrace,
            ["request_path"] = payload.Url,
            ["browser"] = browser,
            ["os"] = os,
            ["userAgent"] = payload.UserAgent ?? Request.Headers.UserAgent.ToString()
        };

        if (payload.StatusCode.HasValue)
        {
            metadata["status_code"] = payload.StatusCode.Value;
        }

        Guid? userId = null;
        var subClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(subClaim, out var parsedUser)) userId = parsedUser;

        loggingService.Enqueue(new LogQueueEntry
        {
            EventType = "Exception",
            ActorType = AuditActorType.Developer,
            ActorId = userId,
            Action = "ClientConsoleLog",
            MetadataJson = metadata.ToJsonString(),
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
            CorrelationId = Guid.TryParse(HttpContext.TraceIdentifier, out var correlationGuid) ? correlationGuid : null
        });

        return Ok();
    }

    [HttpPost("visit")]
    public IActionResult SubmitClientVisit([FromBody] ClientVisitPayload payload)
    {
        var userAgent = Request.Headers.UserAgent.ToString();
        var (browser, os) = ParseUserAgent(userAgent);

        var metadata = new JsonObject
        {
            ["userAgent"] = userAgent,
            ["referrer"] = payload.Referrer,
            ["screenResolution"] = payload.ScreenResolution,
            ["portal"] = payload.Portal ?? "public",
            ["browser"] = browser,
            ["os"] = os
        };

        Guid? actorId = null;
        var actorType = AuditActorType.User; // Default to standard regular user / anonymous visitor

        var subClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(subClaim, out var parsedId))
        {
            actorId = parsedId;
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
            if (roleClaim == "Admin" || roleClaim == "Staff" || roleClaim == "Developer")
            {
                // Align with business user RLS structures
                if (Enum.TryParse<AuditActorType>(roleClaim, out var parsedActorType))
                {
                    actorType = parsedActorType;
                }
                else
                {
                    actorType = AuditActorType.Admin;
                }
            }
        }

        loggingService.Enqueue(new LogQueueEntry
        {
            EventType = "PageView",
            ActorType = actorType,
            ActorId = actorId,
            Action = payload.Path, // store visited path in action column
            MetadataJson = metadata.ToJsonString(),
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString(),
            CorrelationId = Guid.TryParse(HttpContext.TraceIdentifier, out var correlationGuid) ? correlationGuid : null
        });

        return Ok();
    }

    private static (string Browser, string Os) ParseUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return ("Unknown", "Unknown");

        var ua = userAgent.ToLowerInvariant();
        
        // Browser Detection
        var browser = "Unknown";
        if (ua.Contains("edg/")) browser = "Edge";
        else if (ua.Contains("chrome") || ua.Contains("crios")) browser = "Chrome";
        else if (ua.Contains("firefox") || ua.Contains("fxios")) browser = "Firefox";
        else if (ua.Contains("safari") && !ua.Contains("chrome") && !ua.Contains("android")) browser = "Safari";
        else if (ua.Contains("opera") || ua.Contains("opr/")) browser = "Opera";
        else if (ua.Contains("msie") || ua.Contains("trident/")) browser = "IE";

        // OS Detection
        var os = "Unknown";
        if (ua.Contains("windows")) os = "Windows";
        else if (ua.Contains("macintosh") || ua.Contains("mac os x")) os = "macOS";
        else if (ua.Contains("iphone") || ua.Contains("ipad")) os = "iOS";
        else if (ua.Contains("android")) os = "Android";
        else if (ua.Contains("linux")) os = "Linux";

        return (browser, os);
    }
}
