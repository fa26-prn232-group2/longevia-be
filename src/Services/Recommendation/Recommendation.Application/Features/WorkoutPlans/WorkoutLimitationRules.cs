using Recommendation.Domain.Constants;

namespace Recommendation.Application.Features.WorkoutPlans;

/// <summary>
/// Bảng ánh xạ <b>đóng</b> giữa limitation caller khai và hoạt động bị loại
/// (FR-031). Token không khớp bảng bị bỏ qua và báo <c>unsupported_limitation</c>;
/// hệ thống không suy diễn chấn thương/bệnh lý ngoài dữ liệu request.
/// </summary>
public sealed class WorkoutLimitationRules
{
    /// <summary>Rule duy nhất hiện có: đau/tổn thương khớp → loại hoạt động tác động mạnh.</summary>
    public const string JointPainRule = "joint_pain";

    private static readonly IReadOnlyDictionary<string, WorkoutLimitationRule> Rules =
        new Dictionary<string, WorkoutLimitationRule>(StringComparer.Ordinal)
        {
            [JointPainRule] = new(
                [
                    "đau khớp",
                    "đau khớp gối",
                    "đau đầu gối",
                    "đau khớp háng",
                    "đau khớp hông",
                    "chấn thương khớp",
                    "tổn thương khớp",
                    "joint pain",
                    "joint injury",
                    "knee pain",
                    "knee injury",
                    "hip pain",
                ],
                ["running", "dancing", "strength", "gardening"]),
        };

    /// <summary>Hoạt động bị loại bởi một rule; rỗng khi rule không tồn tại.</summary>
    public IReadOnlyList<string> ActivityKeysFor(string ruleKey) =>
        Rules.TryGetValue(ruleKey, out var rule) ? rule.ExcludedActivityKeys : [];

    /// <summary>
    /// Áp bảng ánh xạ đóng cho danh sách limitation; trả hoạt động bị loại, token
    /// không khớp và token đã áp dụng.
    /// </summary>
    public WorkoutLimitationRuleResult Apply(IReadOnlyList<string>? limitations)
    {
        if (limitations is null || limitations.Count == 0)
        {
            return new WorkoutLimitationRuleResult
            {
                ExcludedActivityKeys = new HashSet<string>(StringComparer.Ordinal),
                UnsupportedLimitations = [],
                AppliedLimitations = [],
            };
        }

        var excluded = new HashSet<string>(StringComparer.Ordinal);
        var unsupported = new List<string>();
        var applied = new List<string>();

        foreach (var limitation in limitations)
        {
            var normalized = WorkoutActivities.Normalize(limitation);
            if (normalized.Length == 0)
            {
                continue;
            }

            var matched = false;
            foreach (var rule in Rules.Values)
            {
                if (!rule.Aliases.Any(
                    alias => string.Equals(
                        WorkoutActivities.NormalizeForMatch(alias),
                        WorkoutActivities.NormalizeForMatch(normalized),
                        StringComparison.Ordinal)))
                {
                    continue;
                }

                excluded.UnionWith(rule.ExcludedActivityKeys);
                applied.Add(normalized);
                matched = true;
                break;
            }

            if (!matched)
            {
                unsupported.Add(limitation);
            }
        }

        return new WorkoutLimitationRuleResult
        {
            ExcludedActivityKeys = excluded,
            UnsupportedLimitations = unsupported,
            AppliedLimitations = applied,
        };
    }

    private sealed record WorkoutLimitationRule(
        IReadOnlyList<string> Aliases,
        IReadOnlyList<string> ExcludedActivityKeys);
}

/// <summary>Kết quả áp bảng limitation đóng cho một lời gọi.</summary>
public sealed record WorkoutLimitationRuleResult
{
    /// <summary>Hoạt động bị loại bởi các limitation đã khớp.</summary>
    public required IReadOnlySet<string> ExcludedActivityKeys { get; init; }

    /// <summary>Limitation không khớp bảng (giữ nguyên giá trị caller gửi).</summary>
    public required IReadOnlyList<string> UnsupportedLimitations { get; init; }

    /// <summary>Limitation đã khớp, đã chuẩn hoá.</summary>
    public required IReadOnlyList<string> AppliedLimitations { get; init; }
}
