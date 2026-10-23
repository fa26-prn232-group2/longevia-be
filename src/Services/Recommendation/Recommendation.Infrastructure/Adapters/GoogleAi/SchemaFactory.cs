using System.Text.Json.Nodes;
using Recommendation.Domain.Models;

namespace Recommendation.Infrastructure.Adapters.GoogleAi;

/// <summary>
/// Sinh JSON schema (JsonNode) cho từng loại phản hồi có cấu trúc. Dùng
/// <c>ResponseJsonSchema</c> thay cho <c>ResponseSchema</c> để giữ thứ tự property
/// cố định (D-12).
/// </summary>
public static class SchemaFactory
{
    /// <summary>Trả JSON schema tương ứng với loại phản hồi.</summary>
    public static JsonNode Create(StructuredResponseKind kind) => kind switch
    {
        StructuredResponseKind.GroundedAnswer => GroundedAnswerSchema(),
        StructuredResponseKind.MealSelection => MealSelectionSchema(),
        StructuredResponseKind.NutrientGapAnalysis => NutrientGapAnalysisSchema(),
        StructuredResponseKind.MealPlanSelection => MealPlanSelectionSchema(),
        StructuredResponseKind.WorkoutPlanSelection => WorkoutPlanSelectionSchema(),
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind),
            kind,
            "Loại phản hồi có cấu trúc chưa được hỗ trợ."),
    };

    private static JsonObject GroundedAnswerSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["answer"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Câu trả lời tiếng Việt, chỉ dựa trên CONTEXT.",
            },
            ["citations"] = new JsonObject
            {
                ["type"] = "array",
                ["description"] = "Các trích dẫn chống đỡ câu trả lời.",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["chunk_index"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Chỉ số đoạn trong CONTEXT (bắt đầu từ 0).",
                        },
                        ["quote"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Trích nguyên văn từ đoạn tương ứng.",
                        },
                    },
                    ["required"] = new JsonArray("chunk_index", "quote"),
                },
            },
        },
        ["required"] = new JsonArray("answer", "citations"),
    };

    private static JsonObject MealSelectionSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["selections"] = new JsonObject
            {
                ["type"] = "array",
                ["description"] = "Các món được chọn trong danh sách ứng viên (tối đa 5).",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["food_index"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Chỉ số ứng viên trong danh sách đã cung cấp (bắt đầu từ 0).",
                        },
                        ["rationale"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Lý do chọn món bằng tiếng Việt, không nêu số liệu tự tính.",
                        },
                        ["citations"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["description"] = "Các trích dẫn chống đỡ lý do chọn (có thể rỗng).",
                            ["items"] = new JsonObject
                            {
                                ["type"] = "object",
                                ["properties"] = new JsonObject
                                {
                                    ["chunk_index"] = new JsonObject
                                    {
                                        ["type"] = "integer",
                                        ["description"] = "Chỉ số đoạn trong CONTEXT (bắt đầu từ 0).",
                                    },
                                    ["quote"] = new JsonObject
                                    {
                                        ["type"] = "string",
                                        ["description"] = "Trích nguyên văn từ đoạn tương ứng.",
                                    },
                                },
                                ["required"] = new JsonArray("chunk_index", "quote"),
                            },
                        },
                    },
                    ["required"] = new JsonArray("food_index", "rationale", "citations"),
                },
            },
        },
        ["required"] = new JsonArray("selections"),
    };

    // Kế hoạch nhiều ngày: mô hình chỉ chọn chỉ số món trong danh mục ứng viên
    // và trả trích dẫn; mọi rationale/số liệu do code dựng từ request + candidate.
    private static JsonObject MealPlanSelectionSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["days"] = new JsonObject
            {
                ["type"] = "array",
                ["description"] = "Các ngày của kế hoạch, đúng ngày và đúng thứ tự đã cho trong prompt.",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["date"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Ngày ISO YYYY-MM-DD đã cho trong prompt.",
                        },
                        ["meals"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["description"] = "Các bữa của ngày; chỉ chọn món trong danh mục ứng viên.",
                            ["items"] = new JsonObject
                            {
                                ["type"] = "object",
                                ["properties"] = new JsonObject
                                {
                                    ["meal_type"] = new JsonObject
                                    {
                                        ["type"] = "string",
                                        ["description"] = "BREAKFAST | LUNCH | DINNER | SNACK theo bữa đã cho.",
                                    },
                                    ["food_indices"] = new JsonObject
                                    {
                                        ["type"] = "array",
                                        ["description"] = "Chỉ số món trong danh mục ứng viên (bắt đầu từ 0), tối đa 5 và không trùng.",
                                        ["items"] = new JsonObject { ["type"] = "integer" },
                                    },
                                    ["citations"] = CitationArraySchema(),
                                },
                                ["required"] = new JsonArray("meal_type", "food_indices", "citations"),
                            },
                        },
                    },
                    ["required"] = new JsonArray("date", "meals"),
                },
            },
        },
        ["required"] = new JsonArray("days"),
    };

    // Kế hoạch tập luyện: skeleton đã chốt ngày/buổi, mô hình chỉ chọn một
    // hoạt động trong catalog đóng và trả trích dẫn; không viết thời lượng,
    // cường độ hay bài tập mới.
    private static JsonObject WorkoutPlanSelectionSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["days"] = new JsonObject
            {
                ["type"] = "array",
                ["description"] = "Các ngày có buổi tập trong skeleton; không trả ngày nghỉ.",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["date"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Ngày ISO YYYY-MM-DD của buổi tập đã cho trong skeleton.",
                        },
                        ["activity_index"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Chỉ số hoạt động trong catalog đã cho (bắt đầu từ 0).",
                        },
                        ["citations"] = CitationArraySchema(),
                    },
                    ["required"] = new JsonArray("date", "activity_index", "citations"),
                },
            },
        },
        ["required"] = new JsonArray("days"),
    };

    private static JsonObject CitationArraySchema() => new()
    {
        ["type"] = "array",
        ["description"] = "Các trích dẫn chống đỡ lựa chọn (có thể rỗng).",
        ["items"] = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["chunk_index"] = new JsonObject
                {
                    ["type"] = "integer",
                    ["description"] = "Chỉ số đoạn trong CONTEXT (bắt đầu từ 0).",
                },
                ["quote"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Trích nguyên văn từ đoạn tương ứng.",
                },
            },
            ["required"] = new JsonArray("chunk_index", "quote"),
        },
    };

    private static JsonObject NutrientGapAnalysisSchema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["analysis_summary"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Giải thích ngắn gọn bằng tiếng Việt, chỉ dựa trên CONTEXT và số liệu đã cho.",
            },
            ["suggested_foods"] = new JsonObject
            {
                ["type"] = "array",
                ["description"] = "Tên thực phẩm xuất hiện nguyên văn trong CONTEXT (có thể rỗng).",
                ["items"] = new JsonObject
                {
                    ["type"] = "string",
                },
            },
            ["citations"] = new JsonObject
            {
                ["type"] = "array",
                ["description"] = "Các trích dẫn chống đỡ phần giải thích (có thể rỗng).",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["chunk_index"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Chỉ số đoạn trong CONTEXT (bắt đầu từ 0).",
                        },
                        ["quote"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Trích nguyên văn từ đoạn tương ứng.",
                        },
                    },
                    ["required"] = new JsonArray("chunk_index", "quote"),
                },
            },
        },
        ["required"] = new JsonArray("analysis_summary", "suggested_foods", "citations"),
    };
}
