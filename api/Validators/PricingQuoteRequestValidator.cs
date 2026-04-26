using Contracts.DTOs.Purchases;
using FluentValidation;

namespace Api.Validators;

public class PricingQuoteRequestValidator : AbstractValidator<PricingQuoteRequest>
{
    public PricingQuoteRequestValidator()
    {
        RuleFor(x => x.EventId).NotEmpty();

        RuleFor(x => x).Must(x =>
            (x.TableIds is { Count: > 0 } && !x.SeatCount.HasValue)
            || (x.SeatCount is int s && s > 0 && (x.TableIds is null || x.TableIds.Count == 0)))
            .WithMessage("Provide exactly one of TableIds (table mode) or SeatCount + EventTicketTypeId (open mode)");

        RuleFor(x => x.SeatCount).GreaterThan(0).When(x => x.SeatCount.HasValue);
    }
}
