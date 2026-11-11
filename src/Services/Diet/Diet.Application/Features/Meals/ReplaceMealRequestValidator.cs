using FluentValidation;

namespace Diet.Application.Features.Meals;

public sealed class ReplaceMealRequestValidator : AbstractValidator<ReplaceMealRequest>
{
    public ReplaceMealRequestValidator()
    {
        RuleFor(x => x.Items).NotNull().WithMessage("Danh sách món không được để trống.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("Bữa ăn cần ít nhất một món.");
        RuleFor(x => x.Items.Count).LessThanOrEqualTo(100).When(x => x.Items is not null)
            .WithMessage("Một bữa ăn tối đa 100 món.");
        RuleForEach(x => x.Items).NotNull().WithMessage("Món ăn không được để trống.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.FoodId).NotEmpty().WithMessage("Mã món ăn không hợp lệ.");
            item.RuleFor(x => x.Quantity)
                .Must(x => x is > 0 and <= 1000000 && decimal.Round(x, 3) == x)
                .WithMessage("Số lượng phải dương, tối đa 1000000 và 3 chữ số thập phân.");
        });
    }
}
