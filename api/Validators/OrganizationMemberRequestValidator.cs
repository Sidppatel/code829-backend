using Contracts.DTOs.Organizations;
using FluentValidation;

namespace Api.Validators;

public class OrganizationMemberRequestValidator : AbstractValidator<OrganizationMemberRequest>
{
    public OrganizationMemberRequestValidator()
    {
        RuleFor(x => x.BusinessUserId)
            .NotEmpty().WithMessage("BusinessUserId is required");
    }
}
