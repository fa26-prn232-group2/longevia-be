using FluentValidation;

namespace Identity.Application.Features.Users;

public sealed class ConfirmEmailChangeRequestValidator : AbstractValidator<ConfirmEmailChangeRequest>
{
    public ConfirmEmailChangeRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Thiếu mã xác nhận đổi email.")
            .Matches("^[A-Fa-f0-9]{64}$").WithMessage("Mã xác nhận đổi email không hợp lệ.");
    }
}
