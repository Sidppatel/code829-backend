using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Serilog;

namespace Api.Filters;

public class FluentValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var services = context.HttpContext.RequestServices;
        Dictionary<string, string[]>? errors = null;

        foreach (var (_, value) in context.ActionArguments)
        {
            if (value is null) continue;
            var validatorType = typeof(IValidator<>).MakeGenericType(value.GetType());
            if (services.GetService(validatorType) is not IValidator validator) continue;

            var validationContext = new ValidationContext<object>(value);
            var result = await validator.ValidateAsync(validationContext);
            if (result.IsValid) continue;

            errors ??= new();
            foreach (var error in result.Errors)
            {
                errors[error.PropertyName] = errors.TryGetValue(error.PropertyName, out var existing)
                    ? [.. existing, error.ErrorMessage]
                    : [error.ErrorMessage];
            }
        }

        if (errors is not null)
        {

            var path = context.HttpContext.Request.Path.ToString();
            var method = context.HttpContext.Request.Method;
            var summary = string.Join("; ",
                errors.Select(kv => $"{kv.Key}: {string.Join(", ", kv.Value)}"));
            Log.Warning(
                "[Validation] 400 on {Method} {Path} — {Summary}",
                method, path, summary);

            context.Result = new BadRequestObjectResult(new
            {
                statusCode = 400,
                message = "Validation failed",
                errors,
                correlationId = context.HttpContext.TraceIdentifier,
            });
            return;
        }

        await next();
    }
}
