using System.Text.Json;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Longevia.Shared.Errors;

namespace Recommendation.Api.Middlewares;

/// <summary>
/// Chuyển lỗi nghiệp vụ sang gRPC <see cref="Status"/> kèm ErrorDetails chứa mã lỗi ổn định.
/// Theo <c>docs/system-design/design-pattern.md</c> §III và Constitution §II.
/// </summary>
public sealed class GrpcExceptionInterceptor(ILogger<GrpcExceptionInterceptor> logger) : Interceptor
{
    private const string CodeMetadataKey = "code";
    private const string TitleMetadataKey = "title";
    private const string FieldsMetadataKey = "fields";

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context).ConfigureAwait(false);
        }
        catch (AppException ex)
        {
            throw ToRpcException(ex);
        }
        catch (RpcException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, "Yêu cầu đã bị hủy."));
        }
        catch (Exception ex)
        {
            // Chi tiết kỹ thuật chỉ nằm trong log; client chỉ nhận thông điệp chung.
            logger.LogError(ex, "Lỗi không mong đợi khi xử lý gRPC method {Method}", context.Method);
            throw new RpcException(new Status(StatusCode.Internal, "Lỗi hệ thống."));
        }
    }

    private static RpcException ToRpcException(AppException ex)
    {
        var trailers = new Metadata
        {
            { CodeMetadataKey, ex.Code },
            { TitleMetadataKey, ex.Title },
        };

        if (ex.Errors is { Count: > 0 })
        {
            trailers.Add(FieldsMetadataKey, JsonSerializer.Serialize(ex.Errors));
        }

        return new RpcException(new Status(MapStatusCode(ex.Status), ex.Detail), trailers);
    }

    private static StatusCode MapStatusCode(int httpStatus) => httpStatus switch
    {
        400 => StatusCode.InvalidArgument,
        401 => StatusCode.Unauthenticated,
        403 => StatusCode.PermissionDenied,
        404 => StatusCode.NotFound,
        409 => StatusCode.FailedPrecondition,
        422 => StatusCode.FailedPrecondition,
        503 => StatusCode.Unavailable,
        _ => StatusCode.Internal,
    };
}
