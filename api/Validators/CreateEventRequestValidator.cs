using Contracts.DTOs.Events;
using FluentValidation;

namespace Api.Validators;

public class CreateEventRequestValidator : AbstractValidator<CreateEventRequest>
{
    public CreateEventRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Title is required");
        RuleFor(x => x.StartDate).GreaterThan(DateTime.UtcNow).WithMessage("Start date must be in the future");
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).WithMessage("End date must be after start date");
        RuleFor(x => x.VenueId).NotEmpty().WithMessage("Venue ID is required");
        RuleFor(x => x.PlatformFeePercent)
            .GreaterThanOrEqualTo(0).When(x => x.PlatformFeePercent.HasValue)
            .WithMessage("Platform fee must be non-negative");
    }
}
