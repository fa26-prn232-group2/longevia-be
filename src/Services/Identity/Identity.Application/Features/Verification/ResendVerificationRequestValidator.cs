using FluentValidation;

namespace Identity.Application.Features.Verification;

public sealed class ResendVerificationRequestValidator : AbstractValidator<ResendVerificationRequest>
{
    public ResendVerificationRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage("Vui lòng nhập email.")
            .EmailAddress().WithMessage("Email chưa đúng định dạng.")
            .MaximumLength(320).WithMessage("Email quá dài.");
    }
}
