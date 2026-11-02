using Progress.Infrastructure.Adapters.Grpc;

namespace Progress.UnitTests;

public sealed class IdentityGrpcOptionsTests
{
    [Theory]
    [InlineData(null, 2000, false)]
    [InlineData("", 2000, false)]
    [InlineData("ftp://identity:50051", 2000, false)]
    [InlineData("http://secret:password@identity:50051", 2000, false)]
    [InlineData("http://identity:50051/path?secret=value", 2000, false)]
    [InlineData("http://identity:50051", 0, false)]
    [InlineData("http://identity:50051", 10001, false)]
    [InlineData("http://identity:50051", 2000, true)]
    [InlineData("https://identity:50051", 10000, true)]
    public void Options_ValidateEndpointAndBoundedDeadline(string? address, int milliseconds, bool valid) =>
        Assert.Equal(valid, new IdentityGrpcOptions { Address = address, DeadlineMilliseconds = milliseconds }.IsValid());
}
