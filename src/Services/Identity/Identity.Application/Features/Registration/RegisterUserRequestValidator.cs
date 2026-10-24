using FluentValidation;
using Identity.Application.Features.Password;

namespace Identity.Application.Features.Registration;

public sealed class RegisterUserRequestValidator : AbstractValidator<RegisterUserRequest>
{
    public RegisterUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không đúng định dạng.")
            .MaximumLength(255).WithMessage("Email tối đa 255 ký tự.");

        PasswordRules.Add(RuleFor(x => x.Password));

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .Length(2, 100).WithMessage("Họ và tên phải có độ dài từ 2 đến 100 ký tự.");
    }
}
