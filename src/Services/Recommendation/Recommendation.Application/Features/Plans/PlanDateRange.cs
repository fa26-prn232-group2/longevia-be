using System.Globalization;

namespace Recommendation.Application.Features.Plans;

/// <summary>
/// Sinh danh sách ngày ISO <c>YYYY-MM-DD</c> liên tiếp bắt đầu từ
/// <c>week_start_date</c> (data-model §2). Dùng chung cho cả hai RPC plan.
/// </summary>
public sealed class PlanDateRange
{
    private const string IsoDateFormat = "yyyy-MM-dd";

    /// <summary>
    /// Trả <paramref name="days"/> ngày liên tiếp; ném <see cref="ArgumentException"/>
    /// khi ngày bắt đầu sai định dạng và <see cref="ArgumentOutOfRangeException"/>
    /// khi số ngày không dương. Validator đã chặn các trường hợp này trước khi
    /// use case gọi.
    /// </summary>
    public IReadOnlyList<string> Build(string weekStartDate, int days)
    {
        if (!PlanRequestValidator.TryParseIsoDate(weekStartDate, out var start))
        {
            throw new ArgumentException(
                "Ngày bắt đầu tuần phải theo định dạng ISO YYYY-MM-DD.",
                nameof(weekStartDate));
        }

        if (days < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(days),
                days,
                "Số ngày của kế hoạch phải lớn hơn 0.");
        }

        var dates = new List<string>(days);
        for (var offset = 0; offset < days; offset++)
        {
            dates.Add(start.AddDays(offset).ToString(IsoDateFormat, CultureInfo.InvariantCulture));
        }

        return dates;
    }
}
