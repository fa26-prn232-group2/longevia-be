using FluentValidation;

namespace Progress.Application.Features.Water;

public sealed class LogWaterRequestValidator : AbstractValidator<LogWaterRequest>
{
    public LogWaterRequestValidator()
    {
        RuleFor(x => x.AmountMl)
            .InclusiveBetween(1, 5000)
            .WithMessage("Lượng nước phải từ 1 đến 5000 ml.");
    }
}
