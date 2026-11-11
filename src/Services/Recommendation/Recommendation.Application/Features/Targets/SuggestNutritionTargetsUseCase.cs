using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Recommendation.Application.Abstractions;
using Recommendation.Application.Common.Errors;
using Recommendation.Application.Features.Ask;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.Targets;

public sealed record SuggestNutritionTargetsRequest(
    UserProfile Profile,
    string Goal,
    string Description,
    int CurrentCalories,
    string Locale = "vi-VN");

public sealed record SuggestNutritionTargetsResult(
    int Calories,
    double ProteinGrams,
    double CarbsGrams,
    double FatGrams,
    string Rationale,
    string Source,
    string SafetyNote);

/// <summary>
/// Gợi ý mục tiêu dinh dưỡng: calo luôn do code tính từ BMR/TDEE (Mifflin-St Jeor)
/// và mục tiêu cân nặng; AI chỉ tinh chỉnh protein/fat trong khoảng an toàn và
/// viết giải thích. Mọi giá trị ngoài khoảng bị thay bằng công thức để tránh
/// khuyến nghị nguy hiểm.
/// </summary>
public sealed class SuggestNutritionTargetsUseCase(PromptBuilder promptBuilder, IChatModel chatModel)
{
    private static readonly HashSet<string> Goals = new(StringComparer.Ordinal)
    {
        "LOSE_WEIGHT", "MAINTAIN", "BUILD_MUSCLE", "LONGEVITY",
    };

    private const string SafetyNote =
        "Đây là gợi ý tham khảo, không thay thế tư vấn y khoa. Người dưới 18 tuổi, đang mang thai, "
        + "cho con bú hoặc có bệnh nền nên hỏi bác sĩ trước khi áp dụng.";

    public async Task<SuggestNutritionTargetsResult> ExecuteAsync(
        SuggestNutritionTargetsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = request.Profile;
        ValidateProfile(profile);

        var goal = string.IsNullOrWhiteSpace(request.Goal) ? "MAINTAIN" : request.Goal.Trim().ToUpperInvariant();
        if (!Goals.Contains(goal))
        {
            throw RecommendationErrors.InvalidGoal();
        }

        var formula = ComputeFormula(profile, goal);
        var description = (request.Description ?? string.Empty).Trim();
        if (description.Length > 500)
        {
            description = description[..500];
        }

        var prompt = promptBuilder.BuildNutritionTargetSuggestion(profile, goal, description,
            formula.Calories);
        var completion = await chatModel.CompleteAsync(prompt, cancellationToken).ConfigureAwait(false);

        if (completion is not { Abstained: false, Payload: not null })
        {
            return BuildResult(formula, "formula", BuildFormulaRationale(profile, goal, formula),
                "AI tạm thời không phản hồi nên mục tiêu dưới đây được tính bằng công thức chuẩn.");
        }

        var ai = TryReadAiSuggestion(completion.Payload, profile, formula);
        return ai is null
            ? BuildResult(formula, "formula", BuildFormulaRationale(profile, goal, formula),
                "AI trả về chỉ số ngoài khoảng an toàn nên hệ thống dùng công thức chuẩn thay thế.")
            : BuildResult(ai.Value.Formula, "ai", ai.Value.Rationale,
                "Chỉ số đã được kiểm tra nằm trong khoảng an toàn.");
    }

    private static void ValidateProfile(UserProfile profile)
    {
        if (profile.Age is < 14 or > 100
            || profile.HeightCm is < 120 or > 230
            || profile.WeightKg is < 30 or > 300
            || profile.Sex is not ("MALE" or "FEMALE" or "OTHER"))
        {
            throw RecommendationErrors.InvalidProfile();
        }
    }

    private static Formula ComputeFormula(UserProfile profile, string goal)
    {
        var sexAdjustment = profile.Sex switch
        {
            "MALE" => 5,
            "FEMALE" => -161,
            _ => -78, // OTHER/không tiết lộ: trung tính giữa nam và nữ.
        };
        var bmr = (10 * profile.WeightKg) + (6.25 * profile.HeightCm) - (5 * profile.Age)
            + sexAdjustment;

        var factor = profile.ActivityLevel switch
        {
            "LightlyActive" => 1.375,
            "ModeratelyActive" => 1.55,
            "VeryActive" => 1.725,
            _ => 1.2,
        };

        var tdee = bmr * factor;
        var adjustment = goal switch
        {
            "LOSE_WEIGHT" => -0.15,
            "BUILD_MUSCLE" => 0.10,
            _ => 0.0,
        };

        var calories = (int)Clamp(RoundToTen(tdee * (1 + adjustment)), 1200, 4000);
        var proteinPerKg = goal is "LOSE_WEIGHT" or "BUILD_MUSCLE" ? 1.8 : 1.4;
        var protein = Clamp(profile.WeightKg * proteinPerKg, profile.WeightKg * 1.0, profile.WeightKg * 2.4);
        var fat = Clamp(calories * 0.25 / 9, calories * 0.20 / 9, calories * 0.35 / 9);
        var carbs = Math.Max(0, (calories - (protein * 4) - (fat * 9)) / 4);

        return new Formula(calories, Round(protein), Round(carbs), Round(fat), tdee);
    }

    private static (Formula Formula, string Rationale)? TryReadAiSuggestion(
        JsonObject payload,
        UserProfile profile,
        Formula formula)
    {
        if (!TryReadNumber(payload["protein_grams"], out var protein)
            || !TryReadNumber(payload["fat_grams"], out var fat)
            || payload["rationale"] is not JsonValue rationaleValue
            || !rationaleValue.TryGetValue<string>(out var rationale)
            || string.IsNullOrWhiteSpace(rationale))
        {
            return null;
        }

        var minProtein = profile.WeightKg * 1.0;
        var maxProtein = profile.WeightKg * 2.4;
        var minFat = formula.Calories * 0.20 / 9;
        var maxFat = formula.Calories * 0.35 / 9;

        if (protein < minProtein || protein > maxProtein || fat < minFat || fat > maxFat)
        {
            return null;
        }

        var proteinGrams = Round(protein);
        var fatGrams = Round(fat);
        var carbs = Math.Max(0, (formula.Calories - (proteinGrams * 4) - (fatGrams * 9)) / 4);

        return (formula with { ProteinGrams = proteinGrams, CarbsGrams = Round(carbs), FatGrams = fatGrams },
            rationale.Trim());
    }

    private static bool TryReadNumber(JsonNode? node, out double value)
    {
        value = 0;
        return node is JsonValue jsonValue && jsonValue.TryGetValue<double>(out value)
            && !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static string BuildFormulaRationale(UserProfile profile, string goal, Formula formula)
    {
        var goalText = goal switch
        {
            "LOSE_WEIGHT" => "giảm cân an toàn (giảm khoảng 15% so với TDEE)",
            "BUILD_MUSCLE" => "tăng cơ (cộng khoảng 10% so với TDEE)",
            "LONGEVITY" => "duy trì cân nặng và sống khỏe",
            _ => "duy trì cân nặng hiện tại",
        };

        var builder = new StringBuilder();
        builder.AppendLine("### Cách tính");
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"- TDEE ước tính **{Math.Round(formula.Tdee)} kcal** từ hồ sơ (tuổi {profile.Age}, {profile.WeightKg:0.#} kg, {profile.HeightCm:0.#} cm, vận động {profile.ActivityLevel})."));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"- Mục tiêu {goalText}: **{formula.Calories} kcal/ngày**."));
        builder.AppendLine(string.Create(CultureInfo.InvariantCulture,
            $"- Protein **{formula.ProteinGrams} g**, chất béo **{formula.FatGrams} g**, phần còn lại là carb **{formula.CarbsGrams} g**."));
        return builder.ToString().Trim();
    }

    private static SuggestNutritionTargetsResult BuildResult(Formula formula, string source,
        string rationale, string note) =>
        new(formula.Calories, formula.ProteinGrams, formula.CarbsGrams, formula.FatGrams,
            rationale, source, $"{SafetyNote} {note}");

    private static double Round(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

    private static int RoundToTen(double value) =>
        (int)(Math.Round(value / 10, MidpointRounding.AwayFromZero) * 10);

    private static double Clamp(double value, double min, double max) => Math.Min(max, Math.Max(min, value));

    private readonly record struct Formula(
        int Calories,
        double ProteinGrams,
        double CarbsGrams,
        double FatGrams,
        double Tdee);
}
