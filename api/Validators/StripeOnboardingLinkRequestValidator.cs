using Contracts.DTOs.Organizations;
using FluentValidation;

namespace Api.Validators;

public class StripeOnboardingLinkRequestValidator : AbstractValidator<StripeOnboardingLinkRequest>
{
    public StripeOnboardingLinkRequestValidator()
    {
        RuleFor(x => x.Scope)
            .NotEmpty().WithMessage("Scope is required")
            .Must(s => s is "identity" or "bank")
            .WithMessage("Scope must be 'identity' or 'bank'");
    }
}
