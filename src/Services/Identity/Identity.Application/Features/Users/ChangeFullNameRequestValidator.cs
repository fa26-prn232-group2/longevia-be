using FluentValidation;

namespace Identity.Application.Features.Users;

public sealed class ChangeFullNameRequestValidator : AbstractValidator<ChangeFullNameRequest>
{
    public ChangeFullNameRequestValidator()
    {
        RuleFor(request => request.FullName)
            .NotEmpty().WithMessage("Vui lòng nhập họ và tên.")
            .Must(name => name.Trim().Length is >= 1 and <= 100)
            .WithMessage("Họ và tên phải từ 1 đến 100 ký tự.");
    }
}
