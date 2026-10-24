using Progress.Application.Abstractions;

namespace Progress.Infrastructure.Adapters.Grpc;

public sealed class BodyProfileProvider : IBodyProfileProvider
{
    public Task<ProfileHeightResult> GetUserHeightCmAsync(Guid userId, CancellationToken ct)
    {
        // Integration note: Identity canonical gRPC height RPC is pending in contracts/identity/v1/identity.proto from Dev 1 (Nguyên).
        // Default adapter returns ServiceUnavailable to distinguish external dependency unavailability from missing height.
        return Task.FromResult(new ProfileHeightResult(ProfileHeightStatus.ServiceUnavailable));
    }
}
