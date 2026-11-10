using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Longevia.Shared.Auth;

/// <summary>Gửi service token qua metadata cho gRPC nội bộ.</summary>
public sealed class ServiceTokenInterceptor(string token) : Interceptor
{
    private const string Header = "x-service-token";

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request, ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var headers = context.Options.Headers ?? new Metadata();
        headers.Add(Header, token);
        var options = context.Options.WithHeaders(headers);
        return continuation(request, new ClientInterceptorContext<TRequest, TResponse>(
            context.Method, context.Host, options));
    }
}

/// <summary>Kiểm tra service token trên server gRPC nội bộ.</summary>
public sealed class ServiceTokenServerInterceptor(string token) : Interceptor
{
    private const string Header = "x-service-token";

    public override Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request,
        ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var provided = context.RequestHeaders.GetValue(Header);
        if (!string.Equals(provided, token, StringComparison.Ordinal))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Service token không hợp lệ."));
        return continuation(request, context);
    }
}
