using FluentValidation;

namespace Identity.Application.Features.Password;

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Mật khẩu hiện tại không được để trống.");
        PasswordRules.Add(RuleFor(x => x.NewPassword));
    }
}
