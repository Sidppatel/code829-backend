using Contracts.DTOs.Organizations;
using FluentValidation;

namespace Api.Validators;

public class StartStripeOnboardingRequestValidator : AbstractValidator<StartStripeOnboardingRequest>
{
    public StartStripeOnboardingRequestValidator()
    {
        RuleFor(x => x.BusinessType)
            .NotEmpty().WithMessage("BusinessType is required")
            .Must(t => t is "individual" or "company")
            .WithMessage("BusinessType must be 'individual' or 'company'");

        When(x => !string.IsNullOrWhiteSpace(x.ProductDescription), () =>
        {
            RuleFor(x => x.ProductDescription!)
                .Length(10, 500)
                .WithMessage("ProductDescription must be 10-500 characters");
        });

        When(x => !string.IsNullOrWhiteSpace(x.Mcc), () =>
        {
            RuleFor(x => x.Mcc!)
                .Matches("^[0-9]{4}$")
                .WithMessage("Mcc must be a 4-digit Merchant Category Code");
        });

        When(x => !string.IsNullOrWhiteSpace(x.LegalName), () =>
        {
            RuleFor(x => x.LegalName!)
                .MaximumLength(200)
                .WithMessage("LegalName must be 200 characters or fewer");
        });
    }
}
