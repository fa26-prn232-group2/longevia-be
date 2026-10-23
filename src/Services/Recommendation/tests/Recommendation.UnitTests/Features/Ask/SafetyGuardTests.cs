using Recommendation.Application.Features.Ask;

namespace Recommendation.UnitTests.Features.Ask;

/// <summary>
/// Hồi quy SafetyGuard (FR-032/FR-033): chặn cụm từ chỉ định y tế (thuốc, liều,
/// chẩn đoán, thay thế điều trị) nhưng không chặn câu hỏi lối sống có chữ "thuốc"
/// và không chặn câu hỏi chế độ ăn cho người mắc bệnh.
/// </summary>
public sealed class SafetyGuardTests
{
    private readonly SafetyGuard _guard = new();

    [Theory]
    [InlineData("Uống thuốc gì để giảm đường huyết?")]
    [InlineData("Liều thuốc này bao nhiêu?")]
    [InlineData("Chẩn đoán bệnh cho tôi")]
    [InlineData("lieu thuoc nay bao nhieu")]
    [InlineData("Tôi nên tự ý bỏ thuốc không?")]
    [InlineData("Thay thế điều trị bằng phương pháp nào?")]
    public void RefusesMedicalInstructionPhrases(string question)
    {
        var refusal = _guard.FindRefusal(question);

        Assert.False(string.IsNullOrWhiteSpace(refusal));
        Assert.Contains("bác sĩ", refusal!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Hút thuốc lá có hại gì?")]
    [InlineData("Bỏ thuốc lá có giúp sống lâu không?")]
    [InlineData("Thuốc trừ sâu có ảnh hưởng sức khỏe không?")]
    [InlineData("Thuốc phiện gây hại gì cho sức khỏe?")]
    [InlineData("Người tiểu đường nên ăn gì?")]
    [InlineData("hut thuoc la co hai gi")]
    public void AllowsLifestyleQuestionsMentioningThuoc(string question)
    {
        Assert.Null(_guard.FindRefusal(question));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void DoesNotRefuseBlankQuestion(string? question)
    {
        Assert.Null(_guard.FindRefusal(question));
    }
}
