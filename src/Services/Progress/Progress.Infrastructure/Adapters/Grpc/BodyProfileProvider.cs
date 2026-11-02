using Grpc.Core;
using Longevia.Identity.Contracts.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Progress.Application.Abstractions;

namespace Progress.Infrastructure.Adapters.Grpc;

public sealed class BodyProfileProvider(
    IdentityService.IdentityServiceClient client,
    IOptions<IdentityGrpcOptions> options,
    ILogger<BodyProfileProvider> logger) : IBodyProfileProvider
{
    public async Task<ProfileHeightResult> GetUserHeightCmAsync(Guid userId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var reply = await client.GetUserHeightAsync(new GetUserHeightRequest { UserId = userId.ToString("D") },
                deadline: DateTime.UtcNow.AddMilliseconds(options.Value.DeadlineMilliseconds),
                cancellationToken: ct).ResponseAsync;
            if (!reply.HasHeightCmX100)
                return new(ProfileHeightStatus.HeightNotRecorded, GrpcStatus: nameof(StatusCode.OK));
            var height = reply.HeightCmX100 / 100m;
            if (height is < 50m or > 250m || decimal.Round(height, 2) != height)
            {
                logger.LogWarning("Identity height contract validation failed after gRPC {Status}.", StatusCode.OK);
                return new(ProfileHeightStatus.IntegrationFailure, GrpcStatus: nameof(StatusCode.OK));
            }
            return new(ProfileHeightStatus.Success, height, nameof(StatusCode.OK));
        }
        catch (RpcException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
        catch (RpcException ex)
        {
            logger.LogWarning("Identity height call failed with gRPC {Status}.", ex.StatusCode);
            var status = ex.StatusCode switch
            {
                StatusCode.NotFound => ProfileHeightStatus.ProfileNotFound,
                StatusCode.Unavailable or StatusCode.DeadlineExceeded => ProfileHeightStatus.ServiceUnavailable,
                _ => ProfileHeightStatus.IntegrationFailure
            };
            return new(status, GrpcStatus: ex.StatusCode.ToString());
        }
    }
}
