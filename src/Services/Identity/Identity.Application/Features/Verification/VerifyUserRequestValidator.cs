using FluentValidation;

namespace Identity.Application.Features.Verification;

public sealed class VerifyUserRequestValidator : AbstractValidator<VerifyUserRequest>
{
    public VerifyUserRequestValidator()
    {
        RuleFor(request => request.Token)
            .NotEmpty().WithMessage("Token xác minh không được để trống.")
            .Matches("^[A-Fa-f0-9]{64}$").WithMessage("Token xác minh không hợp lệ.");
    }
}
