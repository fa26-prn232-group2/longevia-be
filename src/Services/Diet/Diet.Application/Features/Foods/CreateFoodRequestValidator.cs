using FluentValidation;

namespace Diet.Application.Features.Foods;

public sealed class CreateFoodRequestValidator : AbstractValidator<CreateFoodRequest>
{
    public CreateFoodRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Tên món bắt buộc.")
            .MaximumLength(200).WithMessage("Tên món tối đa 200 ký tự.");
        RuleFor(x => x.Category).NotEmpty().WithMessage("Nhóm món bắt buộc.")
            .MaximumLength(50).WithMessage("Nhóm món tối đa 50 ký tự.");
        RuleFor(x => x.ServingUnit).NotEmpty().WithMessage("Đơn vị bắt buộc.")
            .MaximumLength(30).WithMessage("Đơn vị tối đa 30 ký tự.");
        RuleFor(x => x.ServingSize).Must(x => x > 0 && ValidAmount(x))
            .WithMessage("Khẩu phần phải dương, tối đa 1000000 và 3 chữ số thập phân.");
        RuleFor(x => x.Calories).Must(ValidAmount).WithMessage(NutrientError);
        RuleFor(x => x.ProteinGrams).Must(ValidAmount).WithMessage(NutrientError);
        RuleFor(x => x.CarbsGrams).Must(ValidAmount).WithMessage(NutrientError);
        RuleFor(x => x.FatGrams).Must(ValidAmount).WithMessage(NutrientError);
        RuleFor(x => x.FiberGrams).Must(ValidAmount).WithMessage(NutrientError);
        RuleFor(x => x.Allergens).Must(tags => tags is null || tags.Count <= 32)
            .WithMessage("Tối đa 32 nhãn dị ứng.");
        RuleForEach(x => x.Allergens!).NotEmpty().MaximumLength(50)
            .When(x => x.Allergens is not null);
    }

    private const string NutrientError = "Dinh dưỡng phải từ 0 đến 1000000, tối đa 3 chữ số thập phân.";
    private static bool ValidAmount(decimal amount) => amount is >= 0 and <= 1000000 && decimal.Round(amount, 3) == amount;
}
