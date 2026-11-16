using Diet.Domain;

namespace Diet.UnitTests;

public sealed class TextNormalizerTests
{
    [Theory]
    [InlineData("Cà chua", "ca chua")]
    [InlineData("Đậu phộng rang", "dau phong rang")]
    [InlineData("  Bánh   mì  ", "banh mi")]
    [InlineData("SỮA TƯƠI", "sua tuoi")]
    [InlineData("Chuối", "chuoi")]
    [InlineData("CHUỐI", "chuoi")]
    [InlineData("đậu", "dau")]
    [InlineData("Yến mạch cán", "yen mach can")]
    [InlineData("chuoi", "chuoi")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_RemovesVietnameseDiacritics(string? input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("Chuối", "chuoi")]
    [InlineData("Đậu phộng rang", "dau phong rang")]
    [InlineData("Yến mạch cán", "yen mach can")]
    public void Normalize_HandlesDecomposedInput(string composed, string expected)
    {
        var decomposed = composed.Normalize(System.Text.NormalizationForm.FormD);
        Assert.Equal(expected, TextNormalizer.Normalize(decomposed));
    }

    [Fact]
    public void Normalize_DoesNotDependOnStringNormalize()
    {
        // Runtime chiseled (invariant globalization) không tách được dấu bằng string.Normalize.
        // Quét mọi chữ cái tiếng Việt có dấu phải ra ASCII mà không cần ICU.
        const string accented = "àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ";
        Assert.All(accented, c => Assert.True(
            TextNormalizer.Normalize(c.ToString()).All(char.IsAsciiLetterLower),
            $"'{c}' chưa được bỏ dấu"));
        Assert.All(accented.ToUpperInvariant(), c => Assert.True(
            TextNormalizer.Normalize(c.ToString()).All(char.IsAsciiLetterLower),
            $"'{c}' (hoa) chưa được bỏ dấu"));
    }
}
