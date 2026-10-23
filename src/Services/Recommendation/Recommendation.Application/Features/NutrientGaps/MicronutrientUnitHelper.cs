namespace Recommendation.Application.Features.NutrientGaps;

/// <summary>
/// Quy đổi đơn vị vi chất về đơn vị của bảng nhu cầu tham chiếu.
/// </summary>
/// <remarks>
/// Chỉ đổi trong họ khối lượng (g, mg, mcg/µg) vì đó là phép đổi tuyến tính an
/// toàn. IU phụ thuộc từng vi chất (vitamin A, D, E có hệ số khác nhau) nên
/// không đổi được nếu chỉ biết tên và đơn vị — bên gọi dùng IU sẽ bị bỏ qua thay
/// vì suy đoán.
/// </remarks>
internal static class MicronutrientUnitHelper
{
    private static readonly IReadOnlyDictionary<string, double> MassUnitToMilligrams =
        new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["g"] = 1_000d,
            ["mg"] = 1d,
            ["mcg"] = 0.001d,
            ["µg"] = 0.001d,
            ["μg"] = 0.001d,
            ["ug"] = 0.001d,
        };

    /// <summary>
    /// Quy đổi <paramref name="amount"/> từ <paramref name="fromUnit"/> sang
    /// <paramref name="toUnit"/>; trả <c>false</c> khi thiếu đơn vị hoặc không
    /// cùng họ khối lượng.
    /// </summary>
    internal static bool TryConvert(double amount, string? fromUnit, string? toUnit, out double converted)
    {
        converted = 0;

        var from = Normalize(fromUnit);
        var to = Normalize(toUnit);
        if (from.Length == 0 || to.Length == 0)
        {
            return false;
        }

        if (string.Equals(from, to, StringComparison.Ordinal))
        {
            converted = amount;
            return true;
        }

        if (!MassUnitToMilligrams.TryGetValue(from, out var fromFactor)
            || !MassUnitToMilligrams.TryGetValue(to, out var toFactor))
        {
            return false;
        }

        converted = amount * fromFactor / toFactor;
        return true;
    }

    private static string Normalize(string? unit) =>
        (unit ?? string.Empty).Trim().ToLowerInvariant();
}
