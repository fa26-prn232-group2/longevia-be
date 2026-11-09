using Diet.Application.Features.Targets;

namespace Diet.Application.Abstractions;

public interface ITargetSuggestionProvider
{
    Task<TargetSuggestionResult> SuggestAsync(TargetSuggestionInput input, DateTimeOffset deadline,
        CancellationToken ct);
}

public sealed record TargetSuggestionResult(
    int Calories,
    decimal ProteinGrams,
    decimal CarbsGrams,
    decimal FatGrams,
    string Rationale,
    string Source,
    string SafetyNote);
