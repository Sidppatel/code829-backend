using System.Text.Json;
using Contracts.DTOs;
using Contracts.Enums;
using Db.Entities;
using Db.Repositories;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Api.Middleware;

/// <summary>
/// Global exception handler. Catches unhandled exceptions, logs them to developer_logs,
/// and returns a structured ApiError response.
/// </summary>
public class ErrorHandlingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ILogRepository logRepository)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var correlationId = context.TraceIdentifier;

            Log.Error(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);

            try
            {
                await logRepository.AddDeveloperLogAsync(new DeveloperLog
                {
                    Id = Guid.NewGuid(),
                    Severity = LogSeverity.Error,
                    Message = ex.Message,
                    ExceptionType = ex.GetType().FullName,
                    StackTrace = ex.StackTrace,
                    RequestPath = context.Request.Path,
                    RequestMethod = context.Request.Method,
                    StatusCode = 500,
                    IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                    CorrelationId = correlationId
                });
            }
            catch (Exception logEx)
            {
                Log.Error(logEx, "Failed to write developer log");
            }

            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            var env = context.RequestServices.GetService<IWebHostEnvironment>();
            if (env?.IsDevelopment() == true)
            {
                var innerMsg = ex.InnerException?.Message;
                await context.Response.WriteAsJsonAsync(new
                {
                    statusCode = 500,
                    message = ex.Message,
                    innerMessage = innerMsg,
                    exceptionType = ex.GetType().FullName,
                    correlationId
                });
            }
            else
            {
                var error = new ApiError(500, "An internal error occurred", CorrelationId: correlationId);
                await context.Response.WriteAsJsonAsync(error);
            }
        }
    }
}
