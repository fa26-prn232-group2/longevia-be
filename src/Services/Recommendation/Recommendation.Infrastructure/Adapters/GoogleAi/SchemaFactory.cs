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
}
