using Grpc.Core;
using Identity.Domain.Repositories;
using Longevia.Identity.Contracts.V1;

namespace Identity.Api.Endpoints;

/// <summary>Exposes the minimal Identity profile data needed by other services.</summary>
public sealed class IdentityGrpcService(IUserRepository userRepository)
    : IdentityService.IdentityServiceBase
{
    public override async Task<GetUserHeightResponse> GetUserHeight(
        GetUserHeightRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId) || userId == Guid.Empty)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "user_id must be a non-empty GUID."));
        }

        var user = await userRepository
            .GetByIdWithProfileAsync(userId, context.CancellationToken)
            .ConfigureAwait(false);

        if (user?.Profile is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "User profile was not found."));
        }

        var heightCm = user.Profile.HeightCm;
        if (heightCm is < 50m or > 250m || decimal.Round(heightCm, 2) != heightCm)
        {
            throw new RpcException(new Status(StatusCode.Internal, "Stored profile height is invalid."));
        }

        var heightCmX100 = decimal.ToInt32(heightCm * 100m);
        return new GetUserHeightResponse { HeightCmX100 = heightCmX100 };
    }
}
