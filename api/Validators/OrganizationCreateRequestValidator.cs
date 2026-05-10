using Contracts.DTOs.Organizations;
using FluentValidation;

namespace Api.Validators;

public class OrganizationCreateRequestValidator : AbstractValidator<OrganizationCreateRequest>
{
    public OrganizationCreateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(256).WithMessage("Name cannot exceed 256 characters");

        RuleFor(x => x.LegalName)
            .MaximumLength(256).WithMessage("Legal name cannot exceed 256 characters")
            .When(x => !string.IsNullOrEmpty(x.LegalName));

        RuleFor(x => x.CountryCode)
            .Length(2).WithMessage("CountryCode must be a 2-letter ISO code")
            .Matches(@"^[A-Z]{2}$").WithMessage("CountryCode must be uppercase letters")
            .When(x => !string.IsNullOrEmpty(x.CountryCode));
    }
}
