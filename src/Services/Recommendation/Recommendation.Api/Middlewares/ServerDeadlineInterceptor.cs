using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Recommendation.Api.Deadlines;
using Recommendation.Api.Observability;

namespace Recommendation.Api.Middlewares;

/// <summary>
/// Giới hạn thời gian xử lý phía server cho mỗi lời gọi gRPC.
/// Ngân sách lấy theo method từ <see cref="RpcDeadlinePolicy"/>: hai RPC plan
/// dùng <c>RECOMMENDATION__PLAN_DEADLINE_SECONDS</c> (mặc định 90s), 5 RPC cũ
/// dùng <c>RECOMMENDATION__DEADLINE_SECONDS</c> (mặc định 30s) và phải lớn hơn
/// độ trễ sinh văn bản thực tế của provider.
/// Token hết hạn được truyền xuống use case qua <c>HttpContext.RequestAborted</c>.
/// </summary>
public sealed class ServerDeadlineInterceptor(
    ILogger<ServerDeadlineInterceptor> logger,
    RpcDeadlinePolicy deadlinePolicy) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var clientToken = context.CancellationToken;
        var deadline = deadlinePolicy.Resolve(context.Method);

        using var deadlineCts = new CancellationTokenSource(deadline);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(clientToken, deadlineCts.Token);

        // Handler gRPC lấy CancellationToken từ call context; thay bằng token
        // liên kết để use case nhận được đúng ngân sách deadline của server.
        context.GetHttpContext().RequestAborted = linkedCts.Token;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var handler = continuation(request, context);
            var finished = await Task.WhenAny(handler, Task.Delay(deadline, clientToken)).ConfigureAwait(false);

            if (!ReferenceEquals(finished, handler))
            {
                // Handler có thể ném lỗi sau khi ta đã trả lời; quan sát để không bị
                // unobserved task exception.
                _ = handler.ContinueWith(static t => _ = t.Exception, TaskScheduler.Default);

                if (clientToken.IsCancellationRequested)
                {
                    throw new RpcException(new Status(StatusCode.Cancelled, "Yêu cầu đã bị hủy."));
                }

                logger.LogWarning("gRPC method {Method} vượt quá deadline {DeadlineSeconds}s", context.Method, deadline.TotalSeconds);
                throw new RpcException(new Status(StatusCode.DeadlineExceeded, "Yêu cầu vượt quá thời gian xử lý cho phép."));
            }

            var response = await handler.ConfigureAwait(false);
            RecommendationTelemetry.RecordRpc(context.Method, StatusCode.OK, stopwatch.Elapsed.TotalMilliseconds);
            return response;
        }
        catch (RpcException ex)
        {
            RecommendationTelemetry.RecordRpc(context.Method, ex.StatusCode, stopwatch.Elapsed.TotalMilliseconds);
            throw;
        }
    }
}
