using FluentValidation;

namespace Identity.Application.Features.Password;

internal static class PasswordRules
{
    public static void Add<T>(IRuleBuilderInitial<T, string> rule)
    {
        rule.NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(8).WithMessage("Mật khẩu phải có tối thiểu 8 ký tự.")
            .Matches("[A-Z]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ cái in hoa.")
            .Matches("[a-z]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ cái in thường.")
            .Matches("[0-9]").WithMessage("Mật khẩu phải chứa ít nhất 1 chữ số.")
            .Matches(@"[\W_]").WithMessage("Mật khẩu phải chứa ít nhất 1 ký tự đặc biệt.");
    }
}
