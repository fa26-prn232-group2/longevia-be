using Diet.Infrastructure.Grpc;
using Grpc.Core;
using Longevia.Identity.Contracts.V1;
using Longevia.Shared.Errors;

namespace Diet.IntegrationTests;

public sealed class ProfileClientTests
{
    private static GetUserProfileResponse Valid() => new()
    {
        BirthDate = "1990-04-23",
        BiologicalSex = "Female",
        HeightCmX100 = 17525,
        ActivityLevel = "VeryActive",
        DietaryPreference = "Vegan",
        Allergies = { "Peanuts" }
    };

    [Fact]
    public async Task Profile_MapsCanonicalValuesAndMissingMetadataDistinctly()
    {
        var reply = Valid();
        var invoker = new ReplyInvoker(reply);
        var provider = new IdentityProfileProvider(new(invoker), TimeProvider.System);
        var result = await provider.GetAsync(Guid.NewGuid(), default);
        Assert.Equal(175.25m, result.HeightCm);
        Assert.False(result.IsMale);
        Assert.Equal(1.725m, result.ActivityFactor);
        Assert.Equal("Vegan", result.DietaryPreference);
        Assert.Equal(new[] { "Peanuts" }, result.Allergies);
        Assert.InRange((invoker.Deadline!.Value - DateTime.UtcNow).TotalSeconds, 0, 2);
        reply.ClearDietaryPreference();
        var legacy = await provider.GetAsync(Guid.NewGuid(), default);
        Assert.Null(legacy.DietaryPreference);
        Assert.Null(legacy.Allergies);
    }

    [Theory]
    [InlineData(StatusCode.NotFound, "diet.profile_not_found", 422)]
    [InlineData(StatusCode.Unavailable, "diet.profile_unavailable", 503)]
    [InlineData(StatusCode.DeadlineExceeded, "diet.profile_timeout", 504)]
    public async Task Failure_IsExplicit(StatusCode status, string code, int http)
    {
        var provider = new IdentityProfileProvider(new(new ReplyInvoker(Valid(), status)), TimeProvider.System);
        var error = await Assert.ThrowsAsync<AppException>(() => provider.GetAsync(Guid.NewGuid(), default));
        Assert.Equal(code, error.Code);
        Assert.Equal(http, error.Status);
    }

    [Fact]
    public async Task InvalidProfile_IsRejected()
    {
        var reply = Valid();
        reply.ActivityLevel = "unknown";
        var provider = new IdentityProfileProvider(new(new ReplyInvoker(reply)), TimeProvider.System);
        Assert.Equal("diet.profile_integration_failed", (await Assert.ThrowsAsync<AppException>(() =>
            provider.GetAsync(Guid.NewGuid(), default))).Code);
    }

    private sealed class ReplyInvoker(GetUserProfileResponse reply, StatusCode? error = null) : CallInvoker
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
