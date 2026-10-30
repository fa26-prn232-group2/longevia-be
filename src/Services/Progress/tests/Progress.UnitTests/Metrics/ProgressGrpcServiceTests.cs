using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Longevia.Progress.Contracts.V1;
using Microsoft.Extensions.Logging.Abstractions;
using Progress.Api.Grpc;
using Progress.Application.Features.Metrics;

namespace Progress.UnitTests.Metrics;

public sealed class ProgressGrpcServiceTests
{
    [Fact]
    public async Task GetLatestWeight_EmptySourceMetricId_ThrowsInternal()
    {
        var stub = new StubWeightQueryService
        {
            Handler = (_, _, _) => Task.FromResult(new LatestWeightResult(true, 7000, DateTimeOffset.UtcNow, Guid.Empty))
        };
        var service = new ProgressGrpcService(stub, NullLogger<ProgressGrpcService>.Instance);
        var ex = await Assert.ThrowsAsync<RpcException>(() => service.GetLatestWeight(
            new GetLatestWeightRequest { UserId = Guid.NewGuid().ToString() }, new TestServerCallContext()));
        Assert.Equal(StatusCode.Internal, ex.StatusCode);
    }

    [Fact]
    public async Task GetLatestWeight_RpcFault_PreservesTransportStatus()
    {
        var fault = new RpcException(new Status(StatusCode.Unavailable, "Provider unavailable"));
        var stub = new StubWeightQueryService { Handler = (_, _, _) => throw fault };
        var service = new ProgressGrpcService(stub, NullLogger<ProgressGrpcService>.Instance);
        var ex = await Assert.ThrowsAsync<RpcException>(() => service.GetLatestWeight(
            new GetLatestWeightRequest { UserId = Guid.NewGuid().ToString() }, new TestServerCallContext()));
        Assert.Same(fault, ex);
    }

    [Theory]
    [InlineData(null, true, true)]
    [InlineData(7000, false, true)]
    [InlineData(7000, true, false)]
    [InlineData(0, true, true)]
    [InlineData(-1, true, true)]
    public async Task GetLatestWeight_InconsistentFoundResult_ThrowsSanitizedInternal(int? weight, bool hasTime, bool hasId)
    {
        var stub = new StubWeightQueryService
        {
            Handler = (_, _, _) => Task.FromResult(new LatestWeightResult(true, weight,
                hasTime ? DateTimeOffset.UtcNow : null, hasId ? Guid.NewGuid() : null))
        };
        var service = new ProgressGrpcService(stub, NullLogger<ProgressGrpcService>.Instance);
        var ex = await Assert.ThrowsAsync<RpcException>(() => service.GetLatestWeight(
            new GetLatestWeightRequest { UserId = Guid.NewGuid().ToString() }, new TestServerCallContext()));
        Assert.Equal(StatusCode.Internal, ex.StatusCode);
        Assert.Equal("An internal error occurred while processing the request.", ex.Status.Detail);
    }

    [Theory]
    [InlineData(-62135596801L, 0)]
    [InlineData(253402300800L, 0)]
    [InlineData(0L, -1)]
    [InlineData(0L, 1000000000)]
    public async Task GetLatestWeight_MalformedTimestamp_ThrowsSafeInvalidArgument(long seconds, int nanos)
    {
        var stub = new StubWeightQueryService { Handler = (_, _, _) => throw new Exception("Must not query") };
        var service = new ProgressGrpcService(stub, NullLogger<ProgressGrpcService>.Instance);
        var ex = await Assert.ThrowsAsync<RpcException>(() => service.GetLatestWeight(
            new GetLatestWeightRequest { UserId = Guid.NewGuid().ToString(), AsOfUtc = new Timestamp { Seconds = seconds, Nanos = nanos } },
            new TestServerCallContext()));
        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
        Assert.Equal("as_of_utc must be a valid protobuf timestamp.", ex.Status.Detail);
    }

    [Fact]
    public async Task GetLatestWeight_ProviderArgumentFault_IsSanitizedInternal()
    {
        var stub = new StubWeightQueryService { Handler = (_, _, _) => throw new ArgumentException("secret") };
        var service = new ProgressGrpcService(stub, NullLogger<ProgressGrpcService>.Instance);
        var ex = await Assert.ThrowsAsync<RpcException>(() => service.GetLatestWeight(
            new GetLatestWeightRequest { UserId = Guid.NewGuid().ToString() }, new TestServerCallContext()));
        Assert.Equal(StatusCode.Internal, ex.StatusCode);
        Assert.DoesNotContain("secret", ex.Status.Detail);
    }

    private sealed class StubWeightQueryService : IWeightQueryService
    {
        public Func<Guid, DateTimeOffset?, CancellationToken, Task<LatestWeightResult>>? Handler { get; set; }

        public Task<LatestWeightResult> GetLatestWeightAsync(Guid userId, DateTimeOffset? asOfUtc, CancellationToken ct) =>
            Handler != null ? Handler(userId, asOfUtc, ct) : Task.FromResult(new LatestWeightResult(false));
    }

    private sealed class TestServerCallContext(CancellationToken ct = default) : ServerCallContext
    {
        private readonly Metadata _requestHeaders = [];
        private readonly Metadata _responseTrailers = [];

        protected override string MethodCore => "/longevia.progress.v1.ProgressService/GetLatestWeight";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "127.0.0.1";
        protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(1);
        protected override Metadata RequestHeadersCore => _requestHeaders;
        protected override CancellationToken CancellationTokenCore => ct;
        protected override Metadata ResponseTrailersCore => _responseTrailers;
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore => new(null, new Dictionary<string, List<AuthProperty>>());

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) => throw new NotImplementedException();
        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task GetLatestWeight_InvalidUserId_ThrowsInvalidArgument(string userId)
    {
        var stub = new StubWeightQueryService();
        var service = new ProgressGrpcService(stub, NullLogger<ProgressGrpcService>.Instance);
        var context = new TestServerCallContext();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetLatestWeight(new GetLatestWeightRequest { UserId = userId }, context));

        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public async Task GetLatestWeight_ValidRequest_NoRecord_ReturnsNoRecordFound()
    {
        var stub = new StubWeightQueryService
        {
            Handler = (_, _, _) => Task.FromResult(new LatestWeightResult(Found: false))
        };
        var service = new ProgressGrpcService(stub, NullLogger<ProgressGrpcService>.Instance);
        var context = new TestServerCallContext();

        var response = await service.GetLatestWeight(new GetLatestWeightRequest { UserId = Guid.NewGuid().ToString() }, context);

        Assert.Equal(GetLatestWeightResponse.ResultOneofCase.NoRecordFound, response.ResultCase);
        Assert.True(response.NoRecordFound);
    }

    [Fact]
    public async Task GetLatestWeight_ValidRequest_FoundRecord_ReturnsWeightRecord()
    {
        var userId = Guid.NewGuid();
        var metricId = Guid.NewGuid();
        var measuredAt = new DateTimeOffset(2026, 10, 30, 8, 30, 0, TimeSpan.Zero);

        var stub = new StubWeightQueryService
        {
            Handler = (uid, _, _) => Task.FromResult(new LatestWeightResult(
                Found: true,
                WeightKgX100: 7050,
                MeasuredAt: measuredAt,
                SourceMetricId: metricId))
        };
        var service = new ProgressGrpcService(stub, NullLogger<ProgressGrpcService>.Instance);
        var context = new TestServerCallContext();

        var response = await service.GetLatestWeight(new GetLatestWeightRequest
        {
            UserId = userId.ToString(),
            AsOfUtc = Timestamp.FromDateTimeOffset(measuredAt)
        }, context);

        Assert.Equal(GetLatestWeightResponse.ResultOneofCase.Record, response.ResultCase);
        Assert.NotNull(response.Record);
        Assert.Equal(7050, response.Record.WeightKgX100);
        Assert.Equal(metricId.ToString(), response.Record.SourceMetricId);
        Assert.Equal(measuredAt, response.Record.MeasuredAt.ToDateTimeOffset());
    }

    [Fact]
    public async Task GetLatestWeight_DatabaseUnavailable_ThrowsUnavailable()
    {
        var stub = new StubWeightQueryService
        {
            Handler = (_, _, _) => throw new Npgsql.NpgsqlException("Database offline", new System.Net.Sockets.SocketException())
        };
        var service = new ProgressGrpcService(stub, NullLogger<ProgressGrpcService>.Instance);
        var context = new TestServerCallContext();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetLatestWeight(new GetLatestWeightRequest { UserId = Guid.NewGuid().ToString() }, context));

        Assert.Equal(StatusCode.Unavailable, ex.StatusCode);
    }

    [Fact]
    public async Task GetLatestWeight_ClientCancellation_ThrowsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var stub = new StubWeightQueryService
        {
            Handler = (_, _, ct) => throw new OperationCanceledException(ct)
        };
        var service = new ProgressGrpcService(stub, NullLogger<ProgressGrpcService>.Instance);
        var context = new TestServerCallContext(cts.Token);

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetLatestWeight(new GetLatestWeightRequest { UserId = Guid.NewGuid().ToString() }, context));

        Assert.Equal(StatusCode.Cancelled, ex.StatusCode);
    }

    [Fact]
    public async Task GetLatestWeight_DeadlineExceeded_ThrowsDeadlineExceeded()
    {
        var stub = new StubWeightQueryService
        {
            Handler = (_, _, _) => throw new OperationCanceledException() // token not cancelled
        };
        var service = new ProgressGrpcService(stub, NullLogger<ProgressGrpcService>.Instance);
        var context = new TestServerCallContext(CancellationToken.None);

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetLatestWeight(new GetLatestWeightRequest { UserId = Guid.NewGuid().ToString() }, context));

        Assert.Equal(StatusCode.DeadlineExceeded, ex.StatusCode);
    }

    [Fact]
    public async Task GetLatestWeight_UnexpectedFault_ThrowsInternalWithSanitizedMessage()
    {
        var stub = new StubWeightQueryService
        {
            Handler = (_, _, _) => throw new InvalidOperationException("Sensitive DB ConnectionString details")
        };
        var service = new ProgressGrpcService(stub, NullLogger<ProgressGrpcService>.Instance);
        var context = new TestServerCallContext();

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            service.GetLatestWeight(new GetLatestWeightRequest { UserId = Guid.NewGuid().ToString() }, context));

        Assert.Equal(StatusCode.Internal, ex.StatusCode);
        Assert.DoesNotContain("Sensitive", ex.Status.Detail);
        Assert.Equal("An internal error occurred while processing the request.", ex.Status.Detail);
    }
}
