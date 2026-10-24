using FluentValidation;

namespace Diet.Application.Features.Targets;

public sealed class UpdateTargetRequestValidator : AbstractValidator<UpdateTargetRequest>
{
    public UpdateTargetRequestValidator()
    {
        RuleFor(x => x.TargetCalories).Must(x => x > 0 && ValidAmount(x))
            .WithMessage("Mục tiêu calo phải dương, tối đa 1000000 và 3 chữ số thập phân.");
        RuleFor(x => x.TargetProteinGrams).Must(ValidAmount).WithMessage(MacroError);
        RuleFor(x => x.TargetCarbsGrams).Must(ValidAmount).WithMessage(MacroError);
        RuleFor(x => x.TargetFatGrams).Must(ValidAmount).WithMessage(MacroError);
        RuleFor(x => x).Must(x => x.TargetCalories > 0 &&
            Math.Abs(x.TargetProteinGrams * 4 + x.TargetCarbsGrams * 4 +
                x.TargetFatGrams * 9 - x.TargetCalories) <= x.TargetCalories * 0.05m)
            .When(x => x.TargetCalories is > 0 and <= 1000000 &&
                x.TargetProteinGrams is >= 0 and <= 1000000 &&
                x.TargetCarbsGrams is >= 0 and <= 1000000 &&
                x.TargetFatGrams is >= 0 and <= 1000000)
            .WithMessage("Năng lượng từ macro không được lệch quá 5% so với mục tiêu calo.");
    }

    private const string MacroError = "Macro phải từ 0 đến 1000000, tối đa 3 chữ số thập phân.";
    private static bool ValidAmount(decimal amount) => amount is >= 0 and <= 1000000 && decimal.Round(amount, 3) == amount;
}
