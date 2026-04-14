using Contracts.DTOs.Events;
using FluentValidation;

namespace Api.Validators;

public class CreateEventRequestValidator : AbstractValidator<CreateEventRequest>
{
    public CreateEventRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Title is required");
        RuleFor(x => x.LayoutMode).NotEmpty().WithMessage("Layout mode is required")
            .Must(m => m is "Grid" or "Open").WithMessage("Layout mode must be 'Grid' or 'Open'");
        RuleFor(x => x.StartDate).GreaterThan(DateTime.UtcNow).WithMessage("Start date must be in the future");
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).WithMessage("End date must be after start date");
        RuleFor(x => x.VenueId).NotEmpty().WithMessage("Venue ID is required");
        RuleFor(x => x.PricePerPersonCents)
            .GreaterThanOrEqualTo(0).When(x => x.PricePerPersonCents.HasValue)
            .WithMessage("Price per person must be non-negative");
        RuleFor(x => x.MaxCapacity)
            .GreaterThan(0).When(x => x.MaxCapacity.HasValue)
            .WithMessage("Max capacity must be greater than 0");
    }
}
