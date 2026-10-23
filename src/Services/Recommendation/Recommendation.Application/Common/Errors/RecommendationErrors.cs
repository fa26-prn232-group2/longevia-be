using Longevia.Shared.Errors;

namespace Recommendation.Application.Common.Errors;

/// <summary>
/// Factory lỗi nghiệp vụ của Recommendation. Mã lỗi ổn định dạng
/// <c>recommendation.*</c>, không đổi sau khi đã phát hành.
/// </summary>
public static class RecommendationErrors
{
    /// <summary>Danh mục ứng viên không còn món nào an toàn sau khi lọc.</summary>
    public static AppException NoCandidateFood() =>
        AppException.Unprocessable("Không còn món nào an toàn trong danh mục ứng viên.")
            .WithCode("recommendation.no_candidate_food");

    /// <summary>Kho tri thức không truy cập được.</summary>
    public static AppException KnowledgeUnavailable() =>
        AppException.ServiceUnavailable("Kho tri thức tạm thời không khả dụng.")
            .WithCode("recommendation.knowledge_unavailable");

    /// <summary>Provider AI (embedding/chat) không khả dụng.</summary>
    public static AppException ProviderUnavailable() =>
        AppException.ServiceUnavailable("Dịch vụ AI tạm thời không khả dụng.")
            .WithCode("recommendation.provider_unavailable");

    /// <summary>Câu hỏi rỗng hoặc vượt giới hạn độ dài.</summary>
    public static AppException InvalidQuestion() =>
        AppException.BadRequest("Câu hỏi không hợp lệ.")
            .WithCode("recommendation.invalid_question");
}
