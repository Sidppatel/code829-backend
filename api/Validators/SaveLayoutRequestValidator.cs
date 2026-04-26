using Contracts.DTOs.Layout;
using FluentValidation;

namespace Api.Validators;

public class SaveLayoutRequestValidator : AbstractValidator<SaveLayoutRequest>
{
    public SaveLayoutRequestValidator()
    {
        RuleFor(x => x.Tables).NotNull();
        RuleForEach(x => x.Tables).SetValidator(new SaveLayoutTableRequestValidator());
    }
}

public class SaveLayoutTableRequestValidator : AbstractValidator<SaveLayoutTableRequest>
{
    public SaveLayoutTableRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty();
        RuleFor(x => x.GridRow).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GridCol).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RowSpan).GreaterThanOrEqualTo(1).WithMessage("RowSpan must be ≥ 1");
        RuleFor(x => x.ColSpan).GreaterThanOrEqualTo(1).WithMessage("ColSpan must be ≥ 1");
        RuleFor(x => x.EventTableId).NotEmpty();
    }
}

public class AddTableRequestValidator : AbstractValidator<AddTableRequest>
{
    public AddTableRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty();
        RuleFor(x => x.GridRow).GreaterThanOrEqualTo(0);
        RuleFor(x => x.GridCol).GreaterThanOrEqualTo(0);
        RuleFor(x => x.RowSpan).GreaterThanOrEqualTo(1);
        RuleFor(x => x.ColSpan).GreaterThanOrEqualTo(1);
        RuleFor(x => x.EventTableId).NotEmpty();
    }
}

public class UpdateTableRequestValidator : AbstractValidator<UpdateTableRequest>
{
    public UpdateTableRequestValidator()
    {
        RuleFor(x => x.RowSpan).GreaterThanOrEqualTo(1).When(x => x.RowSpan.HasValue);
        RuleFor(x => x.ColSpan).GreaterThanOrEqualTo(1).When(x => x.ColSpan.HasValue);
        RuleFor(x => x.GridRow).GreaterThanOrEqualTo(0).When(x => x.GridRow.HasValue);
        RuleFor(x => x.GridCol).GreaterThanOrEqualTo(0).When(x => x.GridCol.HasValue);
    }
}

public class CreateTableTemplateRequestValidator : AbstractValidator<CreateTableTemplateRequest>
{
    public CreateTableTemplateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.DefaultCapacity).GreaterThan(0);
        RuleFor(x => x.DefaultShape).NotEmpty();
        RuleFor(x => x.DefaultRowSpan).GreaterThanOrEqualTo(1);
        RuleFor(x => x.DefaultColSpan).GreaterThanOrEqualTo(1);
    }
}
