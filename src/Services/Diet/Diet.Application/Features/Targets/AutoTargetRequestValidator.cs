using FluentValidation;

namespace Diet.Application.Features.Targets;

public sealed class AutoTargetRequestValidator : AbstractValidator<AutoTargetRequest>
{
    private static readonly HashSet<string> Goals = new(StringComparer.OrdinalIgnoreCase)
    {
        "LoseWeight",
        "Maintain",
        "BuildMuscle",
        "Longevity",
    };

    public AutoTargetRequestValidator()
    {
        RuleFor(x => x.Goal)
            .Must(goal => string.IsNullOrWhiteSpace(goal) || Goals.Contains(goal.Trim()))
            .WithMessage("Mục tiêu phải là LoseWeight, Maintain, BuildMuscle hoặc Longevity.");
    }
}
