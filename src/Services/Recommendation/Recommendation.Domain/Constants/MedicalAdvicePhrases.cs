namespace Recommendation.Domain.Constants;

/// <summary>
/// Cụm từ chỉ định y tế mà <c>SafetyGuard</c> phải chặn (FR-033). Mỗi phần tử đã
/// chuẩn hoá bỏ dấu, chữ thường, không dấu câu và được so khớp nguyên cụm theo
/// ranh giới từ — nhờ vậy câu hỏi lối sống có chữ "thuốc" (hút thuốc lá, bỏ thuốc
/// lá, thuốc trừ sâu, thuốc phiện) không bị chặn oan.
/// </summary>
public static class MedicalAdvicePhrases
{
    /// <summary>
    /// Danh sách cụm từ bị chặn: thuốc/liều/đơn thuốc, chẩn đoán bệnh và thay thế
    /// điều trị. Tra cứu tuyến tính vì danh sách nhỏ và cố định.
    /// </summary>
    public static readonly IReadOnlyList<string> Blocked =
    [
        // Thuốc, liều thuốc, đơn thuốc và hành vi tự ý điều trị.
        "lieu thuoc",
        "uong thuoc",
        "dung thuoc",
        "thuoc gi",
        "thuoc nao",
        "ke don",
        "don thuoc",
        "thuoc dieu tri",
        "thuoc chua benh",
        "tu y bo thuoc",
        "ngung thuoc",
        "thay the thuoc",
        "thay the dieu tri",
        "thay cho dieu tri",
        "ngung dieu tri",
        "bo dieu tri",
        // Chẩn đoán bệnh.
        "chan doan",
        "chuan doan benh",
        "co benh gi",
        "toi bi benh gi",
        // Can thiệp y tế / hứa hẹn chữa khỏi.
        "chua benh",
        "chua khoi benh",
        "tu chua",
        "phau thuat",
        "xet nghiem",
    ];
}
