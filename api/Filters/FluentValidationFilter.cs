using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Api.Filters;

/// <summary>
/// Replacement for the deprecated FluentValidation.AspNetCore auto-validation. For each action
/// argument, resolves IValidator&lt;T&gt; from DI, runs validation, and short-circuits with the
/// same 400 response shape used by InvalidModelStateResponseFactory when validation fails.
/// </summary>
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
