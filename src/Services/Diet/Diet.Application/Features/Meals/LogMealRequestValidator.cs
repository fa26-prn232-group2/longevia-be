using Diet.Domain;
using FluentValidation;

namespace Diet.Application.Features.Meals;

public sealed class LogMealRequestValidator : AbstractValidator<LogMealRequest>
{
    public LogMealRequestValidator()
    {
        RuleFor(x => x.Date).NotEqual(default(DateOnly)).WithMessage("Ngày ăn không hợp lệ.");
        RuleFor(x => x.MealType).Must(x => MealTypes.All.Contains(x))
            .WithMessage("Bữa ăn phải là Breakfast, Lunch, Dinner hoặc Snack.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Bữa ăn cần ít nhất một món.");
        RuleFor(x => x.Items.Count).LessThanOrEqualTo(100).When(x => x.Items is not null)
            .WithMessage("Một lần ghi bữa ăn tối đa 100 món.");
        RuleForEach(x => x.Items).NotNull().WithMessage("Món ăn không được để trống.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.FoodId).NotEmpty().WithMessage("Mã món ăn không hợp lệ.");
            item.RuleFor(x => x.Quantity).Must(x => x is > 0 and <= 1000000 && decimal.Round(x, 3) == x)
                .WithMessage("Số lượng phải dương, tối đa 1000000 và 3 chữ số thập phân.");
        });
    }
}
