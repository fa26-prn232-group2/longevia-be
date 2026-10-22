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
