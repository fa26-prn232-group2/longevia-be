using FluentValidation;

namespace Progress.Application.Features.Metrics;

public sealed class LogBodyMetricRequestValidator : AbstractValidator<LogBodyMetricRequest>
{
    public LogBodyMetricRequestValidator(TimeProvider time)
    {
        RuleFor(x => x.LoggedAt)
            .NotEmpty().WithMessage("Thời điểm ghi nhận không được để trống.")
            .Must(loggedAt => loggedAt <= time.GetUtcNow().AddMinutes(1))
            .WithMessage("Thời điểm ghi nhận không được ở tương lai.");

        RuleFor(x => x.WeightKg)
            .InclusiveBetween(25.0m, 300.0m)
            .WithMessage("Cân nặng phải từ 25.0 đến 300.0 kg.");

        When(x => x.BodyFatPercent.HasValue, () =>
        {
            RuleFor(x => x.BodyFatPercent!.Value)
                .GreaterThan(0m).WithMessage("Tỷ lệ mỡ phải lớn hơn 0.")
                .LessThanOrEqualTo(100m).WithMessage("Tỷ lệ mỡ không được vượt quá 100%.");
        });

        When(x => x.WaistCircumferenceCm.HasValue, () =>
        {
            RuleFor(x => x.WaistCircumferenceCm!.Value)
                .GreaterThan(0m).WithMessage("Vòng eo phải lớn hơn 0.")
                .LessThanOrEqualTo(300m).WithMessage("Vòng eo không được vượt quá 300 cm.");
        });

        When(x => x.SystolicBloodPressure.HasValue || x.DiastolicBloodPressure.HasValue, () =>
        {
            RuleFor(x => x.SystolicBloodPressure)
                .NotNull().WithMessage("Huyết áp tâm thu và tâm trương phải được ghi nhận cùng lúc.")
                .InclusiveBetween(50, 250).WithMessage("Huyết áp tâm thu phải từ 50 đến 250 mmHg.");

            RuleFor(x => x.DiastolicBloodPressure)
                .NotNull().WithMessage("Huyết áp tâm thu và tâm trương phải được ghi nhận cùng lúc.")
                .InclusiveBetween(30, 150).WithMessage("Huyết áp tâm trương phải từ 30 đến 150 mmHg.");

            RuleFor(x => x)
                .Must(x => !x.SystolicBloodPressure.HasValue || !x.DiastolicBloodPressure.HasValue || x.SystolicBloodPressure > x.DiastolicBloodPressure)
                .WithMessage("Huyết áp tâm thu phải lớn hơn huyết áp tâm trương.");
        });

        When(x => x.RestingHeartRate.HasValue, () =>
        {
            RuleFor(x => x.RestingHeartRate!.Value)
                .InclusiveBetween(25, 250).WithMessage("Nhịp tim lúc nghỉ phải từ 25 đến 250 bpm.");
        });
    }
}
