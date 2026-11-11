using Diet.Domain;

namespace Diet.UnitTests;

public sealed class TextNormalizerTests
{
    [Theory]
    [InlineData("Cà chua", "ca chua")]
    [InlineData("Đậu phộng rang", "dau phong rang")]
    [InlineData("  Bánh   mì  ", "banh mi")]
    [InlineData("SỮA TƯƠI", "sua tuoi")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_RemovesVietnameseDiacritics(string? input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.Normalize(input));
    }
}
