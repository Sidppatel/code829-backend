using Contracts.DTOs.Purchases;
using FluentValidation;

namespace Api.Validators;

public class CreatePurchaseRequestValidator : AbstractValidator<CreatePurchaseRequest>
{
    public CreatePurchaseRequestValidator()
    {
        RuleFor(x => x.EventId).NotEmpty().WithMessage("Event ID is required");
        RuleFor(x => x)
            .Must(x => x.TableId.HasValue || x.TableIds is { Count: > 0 } || x.SeatsReserved.HasValue)
            .WithMessage("Either TableId/TableIds or SeatsReserved is required");
        RuleFor(x => x.SeatsReserved)
            .GreaterThan(0).When(x => x.SeatsReserved.HasValue)
            .WithMessage("Seats reserved must be greater than 0");
    }
}
