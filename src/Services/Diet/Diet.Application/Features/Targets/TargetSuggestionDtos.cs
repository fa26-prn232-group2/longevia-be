using Diet.Application.Abstractions;

namespace Diet.Application.Features.Targets;

/// <summary>Kết quả gợi ý mục tiêu (chưa ghi DB — người dùng phải xác nhận lưu).</summary>
public sealed record SuggestedTargetsResponse(
    int TargetCalories,
    decimal TargetProteinGrams,
    decimal TargetCarbsGrams,
    decimal TargetFatGrams,
    string Rationale,
    string Source,
    string SafetyNote);

public sealed record SuggestTargetsRequest(string? Description, string? Goal);

public sealed record TargetSuggestionInput(
    UserProfileSnapshot Profile,
    decimal WeightKg,
    string Goal,
    string Description,
    int CurrentCalories);
