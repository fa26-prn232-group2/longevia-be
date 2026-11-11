using FluentValidation;

namespace Identity.Application.Features.Users;

public sealed class RequestEmailChangeRequestValidator : AbstractValidator<RequestEmailChangeRequest>
{
    public RequestEmailChangeRequestValidator()
    {
        RuleFor(x => x.NewEmail)
            .NotEmpty().WithMessage("Vui lòng nhập email mới.")
            .EmailAddress().WithMessage("Email mới chưa đúng định dạng.")
            .MaximumLength(320).WithMessage("Email quá dài.");

        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Vui lòng nhập mật khẩu hiện tại để xác nhận.");
    }
}
