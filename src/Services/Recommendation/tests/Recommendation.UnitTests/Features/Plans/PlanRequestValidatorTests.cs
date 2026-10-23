using Longevia.Shared.Errors;
using Recommendation.Application.Features.MealPlans;
using Recommendation.Application.Features.Plans;
using Recommendation.Application.Features.WorkoutPlans;
using Recommendation.Domain.Models;

namespace Recommendation.UnitTests.Features.Plans;

/// <summary>
/// T013 — FR-038..FR-046: validator kiểm tra profile, target, ngày/bữa, candidate,
/// consumed_date, workout params và locale trước khi use case chạy pipeline.
/// </summary>
public sealed class PlanRequestValidatorTests
{
    private readonly PlanRequestValidator _validator = new();

    [Fact]
    public void AcceptsValidMealPlanRequest()
    {
        _validator.Validate(MealRequest());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(120)]
    public void AcceptsProfileAgeBoundaries(int age)
    {
        _validator.Validate(MealRequest() with { Profile = ValidProfile() with { Age = age } });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(121)]
    public void RejectsProfileAgeOutsideOneTo120(int age)
    {
        AssertInvalid(
            () => _validator.Validate(MealRequest() with { Profile = ValidProfile() with { Age = age } }),
            "recommendation.invalid_profile");
    }

    [Theory]
    [InlineData("MALE", true)]
    [InlineData("FEMALE", true)]
    [InlineData("SEX_UNSPECIFIED", false)]
    [InlineData("", false)]
    public void ProfileSexMustBeMaleOrFemale(string sex, bool accepted)
    {
        var request = MealRequest() with { Profile = ValidProfile() with { Sex = sex } };

        if (accepted)
        {
            _validator.Validate(request);
        }
        else
        {
            AssertInvalid(() => _validator.Validate(request), "recommendation.invalid_profile");
        }
    }

    [Fact]
    public void RejectsNonPositiveHeightOrWeight()
    {
        AssertInvalid(
            () => _validator.Validate(MealRequest() with { Profile = ValidProfile() with { HeightCm = 0 } }),
            "recommendation.invalid_profile");
        AssertInvalid(
            () => _validator.Validate(MealRequest() with { Profile = ValidProfile() with { WeightKg = -1 } }),
            "recommendation.invalid_profile");
    }

    [Theory]
    [InlineData("Sedentary", true)]
    [InlineData("LightlyActive", true)]
    [InlineData("ModeratelyActive", true)]
    [InlineData("VeryActive", true)]
    [InlineData("Rowing", false)]
    [InlineData("", false)]
    public void ProfileActivityLevelMustBelongToDeclaredSet(string activityLevel, bool accepted)
    {
        var request = MealRequest() with
        {
            Profile = ValidProfile() with { ActivityLevel = activityLevel },
        };

        if (accepted)
        {
            _validator.Validate(request);
        }
        else
        {
            AssertInvalid(() => _validator.Validate(request), "recommendation.invalid_profile");
        }
    }

    [Theory]
    [InlineData("None", true)]
    [InlineData("Keto", true)]
    [InlineData("Vegan", true)]
    [InlineData("Mediterranean", true)]
    [InlineData("LowCarb", true)]
    [InlineData("Paleo", false)]
    [InlineData("", false)]
    public void ProfileDietaryPreferenceMustBelongToDeclaredSet(string preference, bool accepted)
    {
        var request = MealRequest() with
        {
            Profile = ValidProfile() with { DietaryPreference = preference },
        };

        if (accepted)
        {
            _validator.Validate(request);
        }
        else
        {
            AssertInvalid(() => _validator.Validate(request), "recommendation.invalid_profile");
        }
    }

    [Fact]
    public void MissingTargetCaloriesReturnsMissingTargets()
    {
        AssertInvalid(
            () => _validator.Validate(MealRequest() with { TargetCalories = 0 }),
            "recommendation.missing_targets");
    }

    [Fact]
    public void AllZeroMacrosReturnsMissingTargets()
    {
        AssertInvalid(
            () => _validator.Validate(MealRequest() with
            {
                TargetProteinGrams = 0,
                TargetCarbsGrams = 0,
                TargetFatGrams = 0,
            }),
            "recommendation.missing_targets");
    }

    [Fact]
    public void NegativeTargetCaloriesReturnsInvalidTargets()
    {
        AssertInvalid(
            () => _validator.Validate(MealRequest() with { TargetCalories = -1 }),
            "recommendation.invalid_targets");
    }

    [Fact]
    public void NegativeMacroReturnsInvalidTargets()
    {
        AssertInvalid(
            () => _validator.Validate(MealRequest() with { TargetProteinGrams = -1 }),
            "recommendation.invalid_targets");
        AssertInvalid(
            () => _validator.Validate(MealRequest() with { TargetCarbsGrams = -1 }),
            "recommendation.invalid_targets");
        AssertInvalid(
            () => _validator.Validate(MealRequest() with { TargetFatGrams = -1 }),
            "recommendation.invalid_targets");
    }

    [Fact]
    public void NegativeFiberReturnsInvalidTargets()
    {
        AssertInvalid(
            () => _validator.Validate(MealRequest() with { TargetFiberGrams = -1 }),
            "recommendation.invalid_targets");
    }

    [Fact]
    public void WeekStartMustBeMonday()
    {
        AssertInvalid(
            () => _validator.Validate(MealRequest() with { WeekStartDate = "2026-10-27" }),
            "recommendation.invalid_week_start");
    }

    [Theory]
    [InlineData("")]
    [InlineData("2026-1-5")]
    [InlineData("26/10/2026")]
    [InlineData("not-a-date")]
    public void WeekStartMustUseIsoFormat(string weekStartDate)
    {
        AssertInvalid(
            () => _validator.Validate(MealRequest() with { WeekStartDate = weekStartDate }),
            "recommendation.invalid_week_start");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public void MealPlanDaysMustBeBetweenOneAndSeven(int days)
    {
        AssertInvalid(
            () => _validator.Validate(MealRequest() with { Days = days }),
            "recommendation.invalid_days");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void MealsPerDayMustBeBetweenOneAndFour(int mealsPerDay)
    {
        AssertInvalid(
            () => _validator.Validate(MealRequest() with { MealsPerDay = mealsPerDay }),
            "recommendation.invalid_meals_per_day");
    }

    [Fact]
    public void EmptyCandidateFoodsReturnsNoCandidateFood()
    {
        AssertInvalid(
            () => _validator.Validate(MealRequest() with { CandidateFoods = [] }),
            "recommendation.no_candidate_food");
    }

    [Fact]
    public void CandidateWithoutFoodIdReturnsInvalidCandidateFood()
    {
        AssertInvalid(
            () => _validator.Validate(MealRequest() with
            {
                CandidateFoods = [Food() with { FoodId = " " }],
            }),
            "recommendation.invalid_candidate_food");
    }

    [Fact]
    public void CandidateWithoutNameReturnsInvalidCandidateFood()
    {
        AssertInvalid(
            () => _validator.Validate(MealRequest() with
            {
                CandidateFoods = [Food() with { Name = string.Empty }],
            }),
            "recommendation.invalid_candidate_food");
    }

    [Theory]
    [InlineData(-1, 10, 20, 5, 3)]
    [InlineData(100, -1, 20, 5, 3)]
    [InlineData(100, 10, -1, 5, 3)]
    [InlineData(100, 10, 20, -1, 3)]
    [InlineData(100, 10, 20, 5, -1)]
    public void CandidateWithNegativeNutritionReturnsInvalidCandidateFood(
        int calories,
        double protein,
        double carbs,
        double fat,
        double fiber)
    {
        var candidate = Food() with
        {
            Calories = calories,
            ProteinGrams = protein,
            CarbsGrams = carbs,
            FatGrams = fat,
            FiberGrams = fiber,
        };

        AssertInvalid(
            () => _validator.Validate(MealRequest() with { CandidateFoods = [candidate] }),
            "recommendation.invalid_candidate_food");
    }

    [Fact]
    public void MoreThan120CandidatesIsNotAValidationError()
    {
        var candidates = Enumerable.Range(0, 121).Select(index => Food($"food-{index}")).ToList();

        _validator.Validate(MealRequest() with { CandidateFoods = candidates });
    }

    [Fact]
    public void NegativeConsumedCaloriesReturnsInvalidConsumedCalories()
    {
        AssertInvalid(
            () => _validator.Validate(MealRequest() with
            {
                ConsumedCalories = -1,
                ConsumedDate = "2026-10-26",
            }),
            "recommendation.invalid_consumed_calories");
    }

    [Fact]
    public void ConsumedCaloriesWithoutDateReturnsMissingConsumedDate()
    {
        AssertInvalid(
            () => _validator.Validate(MealRequest() with { ConsumedCalories = 500, ConsumedDate = null }),
            "recommendation.missing_consumed_date");
    }

    [Theory]
    [InlineData("2026-13-01")]
    [InlineData("26/10/2026")]
    [InlineData("hôm qua")]
    public void InvalidConsumedDateReturnsInvalidConsumedDate(string consumedDate)
    {
        AssertInvalid(
            () => _validator.Validate(MealRequest() with
            {
                ConsumedCalories = 500,
                ConsumedDate = consumedDate,
            }),
            "recommendation.invalid_consumed_date");
    }

    [Fact]
    public void ConsumedDateOutsidePlanIsAllowed()
    {
        _validator.Validate(MealRequest() with
        {
            ConsumedCalories = 2500,
            ConsumedDate = "2026-11-15",
        });
    }

    [Fact]
    public void UnsupportedLocaleFallsBackToVietnamese()
    {
        _validator.Validate(MealRequest() with { Locale = "en-US" });

        Assert.Equal("vi-VN", PlanRequestValidator.ResolveLocale("en-US"));
        Assert.Equal("vi-VN", PlanRequestValidator.ResolveLocale(string.Empty));
        Assert.True(PlanRequestValidator.IsSupportedLocale("vi-VN"));
        Assert.False(PlanRequestValidator.IsSupportedLocale("en-US"));
    }

    [Fact]
    public void AcceptsValidWorkoutPlanRequest()
    {
        _validator.Validate(WorkoutRequest());
    }

    [Theory]
    [InlineData("")]
    [InlineData("GOAL_UNSPECIFIED")]
    [InlineData("unknown")]
    public void GoalMustBeKnown(string goal)
    {
        AssertInvalid(
            () => _validator.Validate(WorkoutRequest() with { Goal = goal }),
            "recommendation.invalid_goal");
    }

    [Theory]
    [InlineData(7, 3)]
    [InlineData(3, 3)]
    [InlineData(2, 2)]
    [InlineData(1, 1)]
    public void ZeroSessionsResolvesToMinOfThreeAndDays(int days, int expected)
    {
        var validator = new PlanRequestValidator();

        Assert.Equal(expected, validator.ResolveSessionsPerWeek(0, days));
    }

    [Fact]
    public void SessionsGreaterThanDaysIsRejected()
    {
        AssertInvalid(
            () => _validator.Validate(WorkoutRequest() with { Days = 2, SessionsPerWeek = 3 }),
            "recommendation.invalid_workout_params");
    }

    [Theory]
    [InlineData(8)]
    [InlineData(-1)]
    public void SessionsOutOfRangeIsRejected(int sessions)
    {
        AssertInvalid(
            () => _validator.Validate(WorkoutRequest() with { SessionsPerWeek = sessions }),
            "recommendation.invalid_workout_params");
    }

    [Fact]
    public void ZeroMinutesResolvesToThirty()
    {
        var validator = new PlanRequestValidator();

        Assert.Equal(30, validator.ResolveMinutesPerSession(0));
        Assert.Equal(45, validator.ResolveMinutesPerSession(45));
    }

    [Theory]
    [InlineData(9)]
    [InlineData(121)]
    [InlineData(-1)]
    public void MinutesOutsideTenTo120IsRejected(int minutes)
    {
        AssertInvalid(
            () => _validator.Validate(WorkoutRequest() with { MinutesPerSession = minutes }),
            "recommendation.invalid_workout_params");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public void WorkoutDaysMustBeBetweenOneAndSeven(int days)
    {
        AssertInvalid(
            () => _validator.Validate(WorkoutRequest() with { Days = days }),
            "recommendation.invalid_days");
    }

    [Fact]
    public void WorkoutWeekStartMustBeMonday()
    {
        AssertInvalid(
            () => _validator.Validate(WorkoutRequest() with { WeekStartDate = "2026-10-28" }),
            "recommendation.invalid_week_start");
    }

    [Fact]
    public void WorkoutProfileIsValidatedToo()
    {
        AssertInvalid(
            () => _validator.Validate(WorkoutRequest() with { Profile = ValidProfile() with { Age = 0 } }),
            "recommendation.invalid_profile");
    }

    private static AppException AssertInvalid(Action action, string expectedCode)
    {
        var exception = Assert.Throws<AppException>(action);
        Assert.Equal(expectedCode, exception.Code);
        return exception;
    }

    private static GenerateMealPlanRequest MealRequest() => new()
    {
        Profile = ValidProfile(),
        TargetCalories = 2000,
        TargetProteinGrams = 120,
        TargetCarbsGrams = 220,
        TargetFatGrams = 60,
        TargetFiberGrams = 30,
        WeekStartDate = "2026-10-26",
        Days = 7,
        MealsPerDay = 4,
        CandidateFoods = [Food("food-1"), Food("food-2")],
        Locale = "vi-VN",
    };

    private static GenerateWorkoutPlanRequest WorkoutRequest() => new()
    {
        Profile = ValidProfile(),
        Goal = "LONGEVITY",
        WeekStartDate = "2026-10-26",
        Days = 7,
        SessionsPerWeek = 0,
        MinutesPerSession = 0,
        ActivityPreference = ["walking"],
        EquipmentAvailable = [],
        Limitations = [],
        Locale = "vi-VN",
    };

    private static UserProfile ValidProfile() => new()
    {
        Age = 30,
        Sex = "MALE",
        HeightCm = 175,
        WeightKg = 70,
        ActivityLevel = "ModeratelyActive",
        DietaryPreference = "None",
        Allergies = [],
    };

    private static CandidateFood Food(string foodId = "food-1") => new()
    {
        FoodId = foodId,
        Name = "Cơm gạo lứt",
        ServingSize = "1",
        ServingUnit = "chén",
        Calories = 200,
        ProteinGrams = 5,
        CarbsGrams = 40,
        FatGrams = 2,
        FiberGrams = 3,
        Allergens = [],
    };
}
