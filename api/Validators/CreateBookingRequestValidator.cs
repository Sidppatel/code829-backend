using Contracts.DTOs.Bookings;
using FluentValidation;

namespace Api.Validators;

public class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingRequestValidator()
    {
        RuleFor(x => x.EventId).NotEmpty().WithMessage("Event ID is required");
        RuleFor(x => x.Items).NotEmpty().WithMessage("At least one item is required");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.TicketTypeId).NotEmpty().WithMessage("Ticket type ID is required");
        });
    }
}
