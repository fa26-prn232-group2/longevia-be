using System.Globalization;

namespace Recommendation.Infrastructure.Adapters.Knowledge;

/// <summary>
/// Dựng biểu thức vector cho câu SQL truy xuất: chuỗi literal <c>[v1,v2,...]</c>
/// và biểu thức cast sang kiểu <c>vector</c>. Vector truy vấn được L2-normalize
/// trước khi dựng literal (D-13).
/// </summary>
public static class VectorQueryBuilder
{
    /// <summary>Chuẩn hoá L2 vector truy vấn; trả nguyên bản nếu norm bằng 0.</summary>
    public static float[] Normalize(IReadOnlyList<float> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var result = new float[values.Count];
        double sumOfSquares = 0;

        for (var index = 0; index < values.Count; index++)
        {
            sumOfSquares += values[index] * values[index];
        }

        var norm = Math.Sqrt(sumOfSquares);
        if (norm <= double.Epsilon)
        {
            return result;
        }

        for (var index = 0; index < values.Count; index++)
        {
            result[index] = (float)(values[index] / norm);
        }

        return result;
    }

    /// <summary>Dựng literal <c>[0.1,0.2,...]</c> từ vector đã L2-normalize.</summary>
    public static string ToSqlLiteral(IReadOnlyList<float> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var normalized = Normalize(values);
        var parts = new string[normalized.Length];

        for (var index = 0; index < normalized.Length; index++)
        {
            parts[index] = normalized[index].ToString("R", CultureInfo.InvariantCulture);
        }

        return $"[{string.Join(',', parts)}]";
    }

    /// <summary>Biểu thức cast tham số text sang <c>vector</c> (thay cho <c>$1::vector</c>).</summary>
    public static string CastExpression(string parameterName) =>
        $"CAST({parameterName} AS vector)";
}
