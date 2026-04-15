using FluentValidation;
using PainFinder.Shared.Contracts;

namespace PainFinder.Application.Validators;

public class SearchRunRequestValidator : AbstractValidator<SearchRunRequest>
{
    public SearchRunRequestValidator()
    {
        RuleFor(x => x.Keyword)
            .NotEmpty().WithMessage("Keyword is required.")
            .MaximumLength(200).WithMessage("Keyword must not exceed 200 characters.");

        RuleFor(x => x.Sources)
            .NotEmpty().WithMessage("At least one source is required.");

        RuleFor(x => x.DateTo)
            .GreaterThan(x => x.DateFrom)
            .When(x => x.DateFrom.HasValue && x.DateTo.HasValue)
            .WithMessage("DateTo must be after DateFrom.");
    }
}
