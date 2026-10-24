using Progress.Application.Abstractions;

namespace Progress.Infrastructure.Adapters.Grpc;

public sealed class BodyProfileProvider : IBodyProfileProvider
{
    public Task<decimal?> GetUserHeightCmAsync(Guid userId, CancellationToken ct)
    {
        // Integration note: Identity canonical gRPC height RPC is pending in contracts/identity/v1/identity.proto.
        // Default adapter returns null (no height recorded yet). In integration tests, a mock or stub can be injected.
        return Task.FromResult<decimal?>(null);
    }
}
