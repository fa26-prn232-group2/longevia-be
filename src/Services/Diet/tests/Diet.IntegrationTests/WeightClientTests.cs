using Diet.Infrastructure.Grpc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Longevia.Progress.Contracts.V1;
using Longevia.Shared.Errors;

namespace Diet.IntegrationTests;

public sealed class WeightClientTests
{
    [Fact]
    public async Task RecordPreservesDecimalAndProvenance()
    {
        var id = Guid.NewGuid();
        var measured = DateTimeOffset.UtcNow.AddDays(-1);
        var invoker = new ReplyInvoker(new()
        {
            Record = new()
            {
                WeightKgX100 = 7050,
                MeasuredAt = Timestamp.FromDateTimeOffset(measured),
                SourceMetricId = id.ToString()
            }
        });
        var provider = new ProgressWeightProvider(new(invoker), TimeProvider.System);
        var result = await provider.GetLatestAsync(Guid.NewGuid(), DateTimeOffset.UtcNow, default);
        Assert.NotNull(result);
        Assert.Equal(70.50m, result.WeightKg);
        Assert.Equal(id, result.SourceMetricId);
        Assert.Equal(measured, result.MeasuredAt);
        Assert.NotNull(invoker.Deadline);
        Assert.InRange((invoker.Deadline.Value - DateTime.UtcNow).TotalSeconds, 0, 2);
    }

    [Fact]
    public async Task OnlyExplicitTrueMeansMissing()
    {
        foreach (var missing in new[] { true, false })
        {
            var provider = new ProgressWeightProvider(new(new ReplyInvoker(new() { NoRecordFound = missing })), TimeProvider.System);
            if (missing) Assert.Null(await provider.GetLatestAsync(Guid.NewGuid(), DateTimeOffset.UtcNow, default));
            else Assert.Equal("diet.weight_integration_failed", (await Assert.ThrowsAsync<AppException>(() =>
                provider.GetLatestAsync(Guid.NewGuid(), DateTimeOffset.UtcNow, default))).Code);
        }
    }

    [Theory]
    [InlineData(StatusCode.DeadlineExceeded, "diet.weight_timeout", 504)]
    [InlineData(StatusCode.Unavailable, "diet.weight_unavailable", 503)]
    public async Task DependencyFailuresAreNotMissing(StatusCode status, string code, int http)
    {
        var provider = new ProgressWeightProvider(new(new ReplyInvoker(new(), status)), TimeProvider.System);
        var error = await Assert.ThrowsAsync<AppException>(() => provider.GetLatestAsync(Guid.NewGuid(), DateTimeOffset.UtcNow, default));
        Assert.Equal(code, error.Code);
        Assert.Equal(http, error.Status);
    }

    private sealed class ReplyInvoker(GetLatestWeightResponse reply, StatusCode? error = null) : CallInvoker
    {
        public DateTime? Deadline { get; private set; }
        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method,
            string? host, CallOptions options, TRequest request)
        {
            Deadline = options.Deadline;
            return new(error.HasValue ? Task.FromException<TResponse>(new RpcException(new Status(error.Value, "private detail"))) : Task.FromResult((TResponse)(object)reply), Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess, () => new Metadata(), () => { });
        }
        public override TResponse BlockingUnaryCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();
        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) => throw new NotSupportedException();
        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(Method<TRequest, TResponse> method, string? host, CallOptions options) => throw new NotSupportedException();
    }
}
