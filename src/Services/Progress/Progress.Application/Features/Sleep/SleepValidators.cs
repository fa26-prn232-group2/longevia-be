using FluentValidation;
using Progress.Domain.Constants;

namespace Progress.Application.Features.Sleep;

public sealed class LogSleepRequestValidator : AbstractValidator<LogSleepRequest>
{
    public LogSleepRequestValidator(TimeProvider time)
    {
        RuleFor(x => x.SleptAt)
            .NotEmpty().WithMessage("Thời điểm đi ngủ không được để trống.")
            .Must(sleptAt => sleptAt <= time.GetUtcNow().AddMinutes(1))
            .WithMessage("Thời điểm đi ngủ không được ở tương lai.");

        RuleFor(x => x.WokeAt)
            .NotEmpty().WithMessage("Thời điểm thức dậy không được để trống.")
            .Must(wokeAt => wokeAt <= time.GetUtcNow().AddMinutes(1))
            .WithMessage("Thời điểm thức dậy không được ở tương lai.")
            .GreaterThan(x => x.SleptAt)
            .WithMessage("Thời điểm thức dậy phải sau thời điểm đi ngủ.");

        RuleFor(x => x)
            .Must(x => (x.WokeAt - x.SleptAt).TotalHours <= 24)
            .WithMessage("Thời gian ngủ không được vượt quá 24 giờ.");

        RuleFor(x => x.SleepQuality)
            .NotEmpty().WithMessage("Chất lượng giấc ngủ không được để trống.")
            .Must(q => SleepQuality.All.Contains(q))
            .WithMessage(x => $"Chất lượng giấc ngủ '{x.SleepQuality}' không hợp lệ.");
    }
}
