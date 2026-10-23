using System.Globalization;
using System.Text.RegularExpressions;
using Recommendation.Domain.Constants;
using Recommendation.Domain.Models;

namespace Recommendation.Application.Features.WorkoutPlans;

/// <summary>
/// Dựng danh mục hoạt động đóng cho một lời gọi (D-09/FR-027/FR-029/FR-033/
/// FR-036): chỉ giữ hoạt động có alias trong chunk chương 5, lọc theo
/// preference/thiết bị/limitations, chỉ giữ cường độ khi chunk có căn cứ và seed
/// từ <see cref="WorkoutActivities"/> khi corpus rỗng.
/// </summary>
public sealed partial class WorkoutActivityCatalogBuilder(WorkoutLimitationRules limitationRules)
{
    private const int MaxExcerptWords = 40;
    private const int AliasLookAheadWords = 4;
    private const int ExcerptLeadingWords = 3;

    /// <summary>Dựng catalog và warning cho lời gọi workout plan.</summary>
    public WorkoutActivityCatalogBuildResult Build(
        GenerateWorkoutPlanRequest request,
        IReadOnlyList<RetrievedChunk> chunks)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(chunks);

        var warnings = new List<PlanWarning>();
        var chapterChunks = chunks
            .Where(chunk => chunk.Chunk.ChapterNumber == WorkoutActivities.SourceChapterNumber)
            .ToList();
        var corpusLoaded = chapterChunks.Count > 0;

        var limitations = limitationRules.Apply(request.Limitations);
        foreach (var unsupported in limitations.UnsupportedLimitations)
        {
            warnings.Add(new PlanWarning
            {
                Code = PlanWarningCodes.UnsupportedLimitation,
                Message = string.Create(
                    CultureInfo.InvariantCulture,
                    $"Giới hạn '{unsupported}' không thuộc bảng ánh xạ đóng nên đã bị bỏ qua."),
            });
        }

        var preferences = NormalizeValues(request.ActivityPreference);
        var equipment = NormalizeSet(request.EquipmentAvailable);

        // Pool nguồn: chương 5 khi corpus có dữ liệu, toàn bộ danh mục đóng ở chế
        // độ degraded (FR-033) — nhưng degraded chỉ dùng khi caller có preference.
        var candidatePool = corpusLoaded
            ? WorkoutActivities.Definitions
                .Where(definition => MatchesCorpus(definition, chapterChunks))
                .ToList()
            : WorkoutActivities.Definitions.ToList();

        IReadOnlyList<WorkoutActivityDefinition> candidates;
        var preferenceWarningAdded = false;

        if (preferences.Count > 0)
        {
            var unmatched = preferences
                .Where(preference => !candidatePool.Any(
                    definition => WorkoutActivities.MatchesAlias(definition, preference)))
                .ToList();

            if (unmatched.Count > 0)
            {
                preferenceWarningAdded = true;
                warnings.Add(UnsupportedPreferenceWarning(unmatched));
            }

            candidates = candidatePool
                .Where(definition => preferences.Any(
                    preference => WorkoutActivities.MatchesAlias(definition, preference)))
                .ToList();
        }
        else
        {
            // Không có preference ở degraded mode thì không có căn cứ nào để chọn
            // hoạt động (D-09.7): trả ngày nghỉ thay vì bịa bài tập.
            candidates = corpusLoaded ? candidatePool : [];
        }

        var equipmentDrops = candidates
            .Where(definition => !WorkoutActivities.SatisfiesEquipment(definition, equipment))
            .ToList();

        if (equipmentDrops.Count > 0)
        {
            candidates = candidates.Except(equipmentDrops).ToList();

            if (preferences.Count > 0 && !preferenceWarningAdded)
            {
                preferenceWarningAdded = true;
                warnings.Add(UnsupportedPreferenceWarning(
                    equipmentDrops.Select(definition => definition.Key).ToList()));
            }
        }

        candidates = candidates
            .Where(definition => !limitations.ExcludedActivityKeys.Contains(definition.Key))
            .ToList();

        var activities = candidates
            .Select(definition => BuildActivity(definition, chapterChunks, corpusLoaded))
            .ToList();

        if (!corpusLoaded && activities.Count > 0)
        {
            warnings.Add(new PlanWarning
            {
                Code = PlanWarningCodes.ActivitySourceUnverified,
                Message = "Kho tri thức chưa có chương 5 nên hoạt động chỉ lấy từ dữ liệu caller gửi, không có căn cứ sách.",
            });
        }

        return new WorkoutActivityCatalogBuildResult
        {
            Catalog = new WorkoutActivityCatalog { Activities = activities },
            Warnings = warnings,
            CorpusLoaded = corpusLoaded,
            AppliedLimitations = limitations.AppliedLimitations,
        };
    }

    private static WorkoutActivity BuildActivity(
        WorkoutActivityDefinition definition,
        IReadOnlyList<RetrievedChunk> chapterChunks,
        bool corpusLoaded)
    {
        if (!corpusLoaded)
        {
            return new WorkoutActivity
            {
                Key = definition.Key,
                DisplayName = definition.DisplayName,
                EquipmentUsed = definition.EquipmentUsed,
            };
        }

        var supportsIntensity = definition.IntensityEvidence.Length > 0
            && chapterChunks.Any(
                chunk => WorkoutActivities.ContainsPhrase(chunk.Chunk.Content, definition.IntensityEvidence));

        return new WorkoutActivity
        {
            Key = definition.Key,
            DisplayName = definition.DisplayName,
            EquipmentUsed = definition.EquipmentUsed,
            Intensity = supportsIntensity ? definition.Intensity : string.Empty,
            Grounding = BuildGrounding(definition, chapterChunks),
        };
    }

    private static bool MatchesCorpus(
        WorkoutActivityDefinition definition,
        IReadOnlyList<RetrievedChunk> chapterChunks) =>
        chapterChunks.Any(chunk => definition.Aliases.Any(
            alias => WorkoutActivities.ContainsPhrase(chunk.Chunk.Content, alias)));

    private static Citation? BuildGrounding(
        WorkoutActivityDefinition definition,
        IReadOnlyList<RetrievedChunk> chapterChunks)
    {
        foreach (var retrieved in chapterChunks)
        {
            var chunk = retrieved.Chunk;
            if (!definition.Aliases.Any(alias => WorkoutActivities.ContainsPhrase(chunk.Content, alias)))
            {
                continue;
            }

            return new Citation
            {
                ChapterNumber = chunk.ChapterNumber,
                ChapterTitle = chunk.ChapterTitle ?? string.Empty,
                BookPage = chunk.BookPageFrom,
                PdfPage = chunk.PdfPageFrom,
                Quote = BuildExcerpt(chunk.Content, definition),
                Verified = chunk.Verified,
                SourceFile = chunk.SourceFile ?? string.Empty,
            };
        }

        return null;
    }

    // Trích một câu (tối đa 40 từ) chứa alias của hoạt động. Chuỗi trả về là tập
    // con liên tiếp của nội dung chunk nên verify quote luôn khớp.
    private static string BuildExcerpt(string content, WorkoutActivityDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        foreach (var sentence in SentenceSeparator().Split(content))
        {
            var trimmed = sentence.Trim();
            if (trimmed.Length == 0
                || !definition.Aliases.Any(alias => WorkoutActivities.ContainsPhrase(trimmed, alias)))
            {
                continue;
            }

            return LimitWords(trimmed, definition);
        }

        return LimitWords(content.Trim(), definition);
    }

    private static string LimitWords(string text, WorkoutActivityDefinition definition)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= MaxExcerptWords)
        {
            return string.Join(' ', words);
        }

        var aliasIndex = 0;
        for (var index = 0; index < words.Length; index++)
        {
            var window = string.Join(
                ' ',
                words.Skip(index).Take(Math.Min(AliasLookAheadWords, words.Length - index)));

            if (definition.Aliases.Any(alias => WorkoutActivities.ContainsPhrase(window, alias)))
            {
                aliasIndex = index;
                break;
            }
        }

        var start = Math.Max(0, aliasIndex - ExcerptLeadingWords);
        return string.Join(' ', words.Skip(start).Take(MaxExcerptWords));
    }

    private static List<string> NormalizeValues(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return [];
        }

        var normalized = new List<string>(values.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var value in values)
        {
            var token = WorkoutActivities.Normalize(value);
            if (token.Length > 0 && seen.Add(token))
            {
                normalized.Add(token);
            }
        }

        return normalized;
    }

    private static HashSet<string> NormalizeSet(IReadOnlyList<string>? values)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (values is null)
        {
            return set;
        }

        foreach (var value in values)
        {
            var token = WorkoutActivities.NormalizeForMatch(value);
            if (token.Length > 0)
            {
                set.Add(token);
            }
        }

        return set;
    }

    private static PlanWarning UnsupportedPreferenceWarning(IReadOnlyList<string> values) => new()
    {
        Code = PlanWarningCodes.UnsupportedActivityPreference,
        Message = string.Create(
            CultureInfo.InvariantCulture,
            $"Hoạt động ưu tiên '{string.Join(", ", values)}' không có trong chương 5 hoặc không khả thi với thiết bị đã khai nên đã bị bỏ."),
    };

    [GeneratedRegex(@"[.!?\n]+", RegexOptions.CultureInvariant)]
    private static partial Regex SentenceSeparator();
}

/// <summary>Kết quả dựng catalog: danh mục, warning và trạng thái corpus.</summary>
public sealed record WorkoutActivityCatalogBuildResult
{
    /// <summary>Catalog đã lọc, sẵn sàng cho skeleton và prompt.</summary>
    public required WorkoutActivityCatalog Catalog { get; init; }

    /// <summary>Warning của bước lọc preference/thiết bị/limitations.</summary>
    public required IReadOnlyList<PlanWarning> Warnings { get; init; }

    /// <summary><c>true</c> khi truy xuất được ít nhất một chunk chương 5.</summary>
    public bool CorpusLoaded { get; init; }

    /// <summary>Limitation đã khớp bảng ánh xạ đóng (đã chuẩn hoá).</summary>
    public IReadOnlyList<string> AppliedLimitations { get; init; } = [];
}
