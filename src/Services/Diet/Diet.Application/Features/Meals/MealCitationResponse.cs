using Diet.Application.Abstractions;

namespace Diet.Application.Features.Meals;

public sealed record MealCitationResponse(int ChapterNumber, string ChapterTitle, int BookPage,
    int PdfPage, string Quote, bool Verified, string SourceFile)
{
    internal static MealCitationResponse From(MealCitation value) => new(value.ChapterNumber,
        value.ChapterTitle, value.BookPage, value.PdfPage, value.Quote, value.Verified, value.SourceFile);
}
