namespace Recommendation.Domain.Constants;

/// <summary>
/// Bảng nhu cầu dinh dưỡng tham chiếu theo nhóm tuổi và giới cho người trưởng
/// thành (FR-008/FR-009). Tuổi và giới của hồ sơ chọn đúng nhóm nhu cầu; toàn
/// bộ phép tính khoảng thiếu chạy bằng code, không giao cho mô hình.
/// </summary>
/// <remarks>
/// Nguồn giá trị (làm tròn, cho người trưởng thành khoẻ mạnh, không mang thai
/// hoặc cho con bú): Viện Hàn lâm Quốc gia Hoa Kỳ — Dietary Reference Intakes
/// (1997–2005) cho vitamin/khoáng chất, protein và chất xơ; WHO/FAO (2003) cho
/// năng lượng và omega-3 (ALA). Năng lượng dùng mức hoạt động trung bình; chất
/// béo lấy 30% năng lượng theo AMDR (9 kcal/g). Đây là hằng số nghiệp vụ để
/// tính khoảng thiếu, không phải tư vấn y tế.
/// <para>
/// Bốn nhóm tuổi: 19–30, 31–50, 51–70, trên 70. Tuổi ≤ 0 hoặc dưới 19 dùng
/// nhóm 19–30 (dịch vụ chỉ có dữ liệu người trưởng thành). Giới không xác định
/// dùng giá trị cao hơn giữa nam và nữ để không bỏ sót thiếu hụt.
/// </para>
/// </remarks>
public static class MicronutrientRda
{
    /// <summary>Giá trị giới tính nam theo contract (<c>BiologicalSex.MALE</c>).</summary>
    public const string Male = "MALE";

    /// <summary>Giá trị giới tính nữ theo contract (<c>BiologicalSex.FEMALE</c>).</summary>
    public const string Female = "FEMALE";

    private const int Band19To30 = 0;
    private const int Band31To50 = 1;
    private const int Band51To70 = 2;
    private const int BandOver70 = 3;

    private static readonly IReadOnlyList<Definition> Definitions =
    [
        new Definition(
            "Energy", "Năng lượng", "kcal",
            [2500, 2400, 2300, 2100],
            [2000, 1900, 1800, 1700]),
        new Definition(
            "Protein", "Protein", "g",
            [56, 56, 56, 56],
            [46, 46, 46, 46]),
        new Definition(
            "Carbohydrate", "Carbohydrate", "g",
            [130, 130, 130, 130],
            [130, 130, 130, 130]),
        new Definition(
            "Fat", "Chất béo", "g",
            [83, 80, 77, 70],
            [67, 63, 60, 57]),
        new Definition(
            "Vitamin A", "Vitamin A", "µg",
            [900, 900, 900, 900],
            [700, 700, 700, 700]),
        new Definition(
            "Vitamin C", "Vitamin C", "mg",
            [90, 90, 90, 90],
            [75, 75, 75, 75]),
        new Definition(
            "Vitamin D", "Vitamin D", "µg",
            [15, 15, 15, 20],
            [15, 15, 15, 20]),
        new Definition(
            "Vitamin E", "Vitamin E", "mg",
            [15, 15, 15, 15],
            [15, 15, 15, 15]),
        new Definition(
            "Vitamin K", "Vitamin K", "µg",
            [120, 120, 120, 120],
            [90, 90, 90, 90]),
        new Definition(
            "Vitamin B1", "Vitamin B1 (thiamine)", "mg",
            [1.2, 1.2, 1.2, 1.2],
            [1.1, 1.1, 1.1, 1.1]),
        new Definition(
            "Vitamin B2", "Vitamin B2 (riboflavin)", "mg",
            [1.3, 1.3, 1.3, 1.3],
            [1.1, 1.1, 1.1, 1.1]),
        new Definition(
            "Vitamin B3", "Vitamin B3 (niacin)", "mg",
            [16, 16, 16, 16],
            [14, 14, 14, 14]),
        new Definition(
            "Vitamin B5", "Vitamin B5 (pantothenic acid)", "mg",
            [5, 5, 5, 5],
            [5, 5, 5, 5]),
        new Definition(
            "Vitamin B6", "Vitamin B6", "mg",
            [1.3, 1.3, 1.7, 1.7],
            [1.3, 1.3, 1.5, 1.5]),
        new Definition(
            "Vitamin B9", "Vitamin B9 (folate)", "µg",
            [400, 400, 400, 400],
            [400, 400, 400, 400]),
        new Definition(
            "Vitamin B12", "Vitamin B12", "µg",
            [2.4, 2.4, 2.4, 2.4],
            [2.4, 2.4, 2.4, 2.4]),
        new Definition(
            "Calcium", "Canxi", "mg",
            [1000, 1000, 1000, 1200],
            [1000, 1000, 1200, 1200]),
        new Definition(
            "Iron", "Sắt", "mg",
            [8, 8, 8, 8],
            [18, 18, 8, 8]),
        new Definition(
            "Magnesium", "Magiê", "mg",
            [400, 420, 420, 420],
            [310, 320, 320, 320]),
        new Definition(
            "Zinc", "Kẽm", "mg",
            [11, 11, 11, 11],
            [8, 8, 8, 8]),
        new Definition(
            "Iodine", "Iot", "µg",
            [150, 150, 150, 150],
            [150, 150, 150, 150]),
        new Definition(
            "Selenium", "Selen", "µg",
            [55, 55, 55, 55],
            [55, 55, 55, 55]),
        new Definition(
            "Omega-3", "Omega-3 (ALA)", "g",
            [1.6, 1.6, 1.6, 1.6],
            [1.1, 1.1, 1.1, 1.1]),
        new Definition(
            "Fiber", "Chất xơ", "g",
            [38, 38, 30, 30],
            [25, 25, 21, 21]),
    ];

    /// <summary>Tên chuẩn (theo contract) của mọi dưỡng chất trong bảng.</summary>
    public static IReadOnlyList<string> Nutrients { get; } =
        Definitions.Select(definition => definition.Nutrient).ToList();

    /// <summary>
    /// Tra nhu cầu của một dưỡng chất theo tuổi và giới. Ném
    /// <see cref="ArgumentOutOfRangeException"/> khi tên không có trong bảng.
    /// </summary>
    public static Reference Get(string nutrient, int age, string? sex)
    {
        if (!TryGet(nutrient, age, sex, out var reference))
        {
            throw new ArgumentOutOfRangeException(
                nameof(nutrient),
                nutrient,
                "Dưỡng chất không có trong bảng nhu cầu tham chiếu.");
        }

        return reference!;
    }

    /// <summary>Tra nhu cầu; trả <c>false</c> khi tên dưỡng chất không có trong bảng.</summary>
    public static bool TryGet(string nutrient, int age, string? sex, out Reference? reference)
    {
        reference = null;

        var definition = Definitions.FirstOrDefault(
            item => string.Equals(item.Nutrient, nutrient, StringComparison.OrdinalIgnoreCase));
        if (definition is null)
        {
            return false;
        }

        var band = BandFor(age);
        reference = new Reference
        {
            Nutrient = definition.Nutrient,
            Label = definition.Label,
            Unit = definition.Unit,
            Target = TargetFor(definition, band, sex),
        };
        return true;
    }

    private static int BandFor(int age) => age switch
    {
        <= 0 => Band19To30,
        <= 30 => Band19To30,
        <= 50 => Band31To50,
        <= 70 => Band51To70,
        _ => BandOver70,
    };

    private static double TargetFor(Definition definition, int band, string? sex)
    {
        if (string.Equals(sex, Female, StringComparison.OrdinalIgnoreCase))
        {
            return definition.Female[band];
        }

        if (string.Equals(sex, Male, StringComparison.OrdinalIgnoreCase))
        {
            return definition.Male[band];
        }

        return Math.Max(definition.Male[band], definition.Female[band]);
    }

    /// <summary>Một dòng của bảng: tên, nhãn tiếng Việt, đơn vị và giá trị theo 4 nhóm tuổi.</summary>
    private sealed record Definition(
        string Nutrient,
        string Label,
        string Unit,
        double[] Male,
        double[] Female);

    /// <summary>Nhu cầu tham chiếu đã chọn theo tuổi và giới của một hồ sơ.</summary>
    public sealed record Reference
    {
        /// <summary>Tên chuẩn (theo contract), ví dụ <c>Fiber</c>.</summary>
        public required string Nutrient { get; init; }

        /// <summary>Nhãn tiếng Việt để hiển thị trong phần diễn giải.</summary>
        public required string Label { get; init; }

        /// <summary>Đơn vị đo: kcal | g | mg | µg.</summary>
        public required string Unit { get; init; }

        /// <summary>Nhu cầu hằng ngày theo nhóm tuổi và giới.</summary>
        public required double Target { get; init; }
    }
}
