using Contracts.DTOs.Organizations;
using FluentValidation;

namespace Api.Validators;

public class OrganizationUpdateRequestValidator : AbstractValidator<OrganizationUpdateRequest>
{
    public OrganizationUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name cannot be empty")
            .MaximumLength(256).WithMessage("Name cannot exceed 256 characters")
            .When(x => x.Name is not null);

        RuleFor(x => x.LegalName)
            .MaximumLength(256).WithMessage("Legal name cannot exceed 256 characters")
            .When(x => !string.IsNullOrEmpty(x.LegalName));

        RuleFor(x => x.CountryCode)
            .Length(2).WithMessage("CountryCode must be a 2-letter ISO code")
            .Matches(@"^[A-Z]{2}$").WithMessage("CountryCode must be uppercase letters")
            .When(x => !string.IsNullOrEmpty(x.CountryCode));

        RuleFor(x => x)
            .Must(x => x.Name is not null || x.LegalName is not null || x.CountryCode is not null)
            .WithMessage("At least one field (name, legalName, countryCode) must be provided");
    }
}
