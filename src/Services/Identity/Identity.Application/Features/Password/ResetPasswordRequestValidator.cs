using FluentValidation;

namespace Identity.Application.Features.Password;

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithMessage("Mã đặt lại mật khẩu không được để trống.");
        PasswordRules.Add(RuleFor(x => x.NewPassword));
    }
}
