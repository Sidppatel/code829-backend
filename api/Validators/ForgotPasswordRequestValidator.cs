using Contracts.DTOs.Auth;
using FluentValidation;

namespace Api.Validators;

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .MaximumLength(254).WithMessage("Email must be 254 characters or fewer")
            .EmailAddress().WithMessage("Invalid email format");
    }
}
