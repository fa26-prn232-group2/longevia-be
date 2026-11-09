using Grpc.Core;
using Longevia.Shared.Auth;

namespace Identity.UnitTests.Security;

public sealed class ServiceTokenInterceptorTests
{
    [Fact]
    public async Task ServerInterceptor_AcceptsMatchingToken()
    {
        var interceptor = new ServiceTokenServerInterceptor("secret");
        var context = new FakeServerCallContext(new Metadata { { "x-service-token", "secret" } });
        var result = await interceptor.UnaryServerHandler("request", context,
            (request, _) => Task.FromResult(request + "-ok"));
        Assert.Equal("request-ok", result);
    }

    [Fact]
    public async Task ServerInterceptor_RejectsMissingToken()
    {
        var interceptor = new ServiceTokenServerInterceptor("secret");
        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            interceptor.UnaryServerHandler("request", new FakeServerCallContext(new Metadata()),
                (request, _) => Task.FromResult(request)));
        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
    }

    [Fact]
    public async Task ServerInterceptor_RejectsWrongToken()
    {
        var interceptor = new ServiceTokenServerInterceptor("secret");
        var context = new FakeServerCallContext(new Metadata { { "x-service-token", "wrong" } });
        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            interceptor.UnaryServerHandler("request", context,
                (request, _) => Task.FromResult(request)));
        Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode);
    }

    private sealed class FakeServerCallContext(Metadata headers) : ServerCallContext
    {
        protected override string MethodCore => "test";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "peer";
        protected override DateTime DeadlineCore => DateTime.UtcNow.AddSeconds(5);
        protected override Metadata RequestHeadersCore => headers;
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore { get; } = new();
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore => null!;
        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
            throw new NotSupportedException();
    }
}
