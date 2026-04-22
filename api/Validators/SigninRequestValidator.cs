using Contracts.DTOs.Auth;
using FluentValidation;

namespace Api.Validators;

public class SigninRequestValidator : AbstractValidator<SigninRequest>
{
    public SigninRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .MaximumLength(254).WithMessage("Email must be 254 characters or fewer")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MaximumLength(128).WithMessage("Password must be 128 characters or fewer");
    }
}
