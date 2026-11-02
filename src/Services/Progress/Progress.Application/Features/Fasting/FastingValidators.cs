using FluentValidation;
using Progress.Domain.Constants;

namespace Progress.Application.Features.Fasting;

public sealed class StartFastingRequestValidator : AbstractValidator<StartFastingRequest>
{
    public StartFastingRequestValidator(TimeProvider time)
    {
        RuleFor(x => x.Protocol)
            .NotEmpty().WithMessage("Giao thức nhịn ăn không được để trống.")
            .Must(p => FastingProtocol.All.Contains(p))
            .WithMessage(x => $"Giao thức nhịn ăn '{x.Protocol}' không hợp lệ.");

        RuleFor(x => x.StartedAt)
            .NotEmpty().WithMessage("Thời điểm bắt đầu không được để trống.")
            .Must(startedAt => startedAt <= time.GetUtcNow().AddMinutes(1))
            .WithMessage("Thời điểm bắt đầu không được ở tương lai.");

        RuleFor(x => x.TargetHours)
            .GreaterThan(0).WithMessage("Mục tiêu số giờ nhịn ăn phải lớn hơn 0.");

        When(x => FastingProtocol.TryGetPresetTargetHours(x.Protocol, out var preset), () =>
        {
            RuleFor(x => x.TargetHours)
                .Must((req, target) =>
                {
                    FastingProtocol.TryGetPresetTargetHours(req.Protocol, out var preset);
                    return target == preset;
                })
                .WithMessage((req, _) =>
                {
                    FastingProtocol.TryGetPresetTargetHours(req.Protocol, out var preset);
                    return $"Giao thức {req.Protocol} yêu cầu targetHours là {preset}.";
                });
        });

        When(x => x.Protocol.Equals(FastingProtocol.Custom, StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.TargetHours)
                .InclusiveBetween(1, 24)
                .WithMessage("Giao thức Custom yêu cầu targetHours từ 1 đến 24 giờ.");
        });
    }
}

public sealed class EndFastingRequestValidator : AbstractValidator<EndFastingRequest>
{
    public EndFastingRequestValidator(TimeProvider time)
    {
        RuleFor(x => x.SessionId)
            .NotEmpty().WithMessage("SessionId không được để trống.");

        RuleFor(x => x.EndedAt)
            .NotEmpty().WithMessage("Thời điểm kết thúc không được để trống.")
            .Must(endedAt => endedAt <= time.GetUtcNow().AddMinutes(1))
            .WithMessage("Thời điểm kết thúc không được ở tương lai.");
    }
}
