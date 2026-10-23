using FluentValidation;
using Identity.Domain.Constants;

namespace Identity.Application.Features.Users;

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.BirthDate)
            .NotEmpty().WithMessage("Ngày sinh không được để trống.")
            .Must(BeAtLeast13YearsOld).WithMessage("Người dùng phải từ đủ 13 tuổi trở lên.");

        RuleFor(x => x.BiologicalSex)
            .NotEmpty().WithMessage("Giới tính không được để trống.")
            .Must(sex => BiologicalSex.All.Contains(sex))
            .WithMessage("Giới tính phải là 'Male' hoặc 'Female'.");

        RuleFor(x => x.HeightCm)
            .InclusiveBetween(50, 250)
            .WithMessage("Chiều cao phải từ 50cm đến 250cm.");

        RuleFor(x => x.ActivityLevel)
            .NotEmpty().WithMessage("Mức độ vận động không được để trống.")
            .Must(lvl => ActivityLevel.All.Contains(lvl))
            .WithMessage("Mức độ vận động không hợp lệ.");

        RuleFor(x => x.DietaryPreference)
            .NotEmpty().WithMessage("Chế độ ăn uống không được để trống.")
            .Must(pref => DietaryPreference.All.Contains(pref))
            .WithMessage("Chế độ ăn uống không hợp lệ.");
    }

    private static bool BeAtLeast13YearsOld(DateOnly birthDate)
    {
        var minDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-13));
        return birthDate <= minDate;
    }
}
