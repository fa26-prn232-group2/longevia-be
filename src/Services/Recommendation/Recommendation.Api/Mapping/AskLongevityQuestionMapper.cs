using Recommendation.Application.Features.Ask;
using Recommendation.Domain.Models;
using Contracts = Longevia.Recommendation.Contracts.V1;

namespace Recommendation.Api.Mapping;

/// <summary>
/// Map giữa message gRPC của contract và DTO nội bộ của Application cho RPC hỏi
/// sách. Contract type chỉ tồn tại ở tầng Api nên Application không tham chiếu
/// generated code.
/// </summary>
internal static class AskLongevityQuestionMapper
{
    /// <summary>Chuyển request gRPC sang DTO nội bộ.</summary>
    internal static AskLongevityQuestionRequest ToApplicationRequest(
        Contracts.AskLongevityQuestionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new AskLongevityQuestionRequest
        {
            UserId = request.UserId,
            Question = request.Question,
            Profile = ToProfile(request.Profile),
            ChapterFilter = request.ChapterFilter,
        };
    }

    /// <summary>Chuyển kết quả use case sang message phản hồi gRPC.</summary>
    internal static Contracts.AskLongevityQuestionResponse ToProtoResponse(
        AskLongevityQuestionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var response = new Contracts.AskLongevityQuestionResponse
        {
            Answer = result.Answer,
            Abstained = result.Abstained,
            AbstainReason = result.AbstainReason ?? string.Empty,
            Disclaimer = new Contracts.Disclaimer { Text = result.Disclaimer },
        };

        response.Citations.AddRange(result.Citations.Select(ToProtoCitation));

        return response;
    }

    private static UserProfile ToProfile(Contracts.UserProfile? profile)
    {
        if (profile is null)
        {
            return new UserProfile();
        }

        return new UserProfile
        {
            Age = profile.Age,
            Sex = profile.Sex.ToString(),
            HeightCm = profile.HeightCm,
            WeightKg = profile.WeightKg,
            ActivityLevel = profile.ActivityLevel,
            DietaryPreference = profile.DietaryPreference,
            Allergies = profile.Allergies.ToList(),
        };
    }

    private static Contracts.Citation ToProtoCitation(Citation citation) => new()
    {
        ChapterNumber = citation.ChapterNumber,
        ChapterTitle = citation.ChapterTitle,
        BookPage = citation.BookPage,
        PdfPage = citation.PdfPage,
        Quote = citation.Quote,
        Verified = citation.Verified,
        SourceFile = citation.SourceFile,
    };
}
