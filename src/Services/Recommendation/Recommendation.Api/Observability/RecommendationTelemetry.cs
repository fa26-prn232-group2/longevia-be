using System.Diagnostics;
using System.Diagnostics.Metrics;
using Grpc.Core;

namespace Recommendation.Api.Observability;

/// <summary>
/// Metric và trace của service Recommendation.
/// Phát qua System.Diagnostics.Metrics - API chuẩn mà OpenTelemetry thu thập.
/// Metric chỉ mang thông tin kỹ thuật (method, status, thời gian); không mang
/// dữ liệu sức khỏe, token hay API key.
/// </summary>
public static class RecommendationTelemetry
{
    public const string MeterName = "Longevia.Recommendation";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    private static readonly Counter<long> RpcCalls = Meter.CreateCounter<long>(
        "recommendation.rpc.calls",
        description: "Số lượt gọi gRPC theo method và status.");

    private static readonly Histogram<double> RpcDuration = Meter.CreateHistogram<double>(
        "recommendation.rpc.duration",
        unit: "ms",
        description: "Thời gian xử lý một lượt gọi gRPC.");

    private static readonly Counter<long> RetrievedChunks = Meter.CreateCounter<long>(
        "recommendation.chunks.retrieved",
        description: "Số đoạn tri thức được truy xuất.");

    public static ActivitySource ActivitySource { get; } = new(MeterName);

    public static void RecordRpc(string method, StatusCode statusCode, double durationMs)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("rpc.method", method),
            new("rpc.status", statusCode.ToString()),
        };

        RpcCalls.Add(1, tags);
        RpcDuration.Record(durationMs, tags);
    }

    public static void RecordRetrievedChunks(int count) => RetrievedChunks.Add(count);
}
