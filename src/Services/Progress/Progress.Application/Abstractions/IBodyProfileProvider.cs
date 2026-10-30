namespace Progress.Application.Abstractions;

public enum ProfileHeightStatus
{
    Success,
    ProfileNotFound,
    HeightNotRecorded,
    ServiceUnavailable,
    IntegrationFailure
}

public sealed record ProfileHeightResult(ProfileHeightStatus Status, decimal? HeightCm = null, string? GrpcStatus = null);

public interface IBodyProfileProvider
{
    Task<ProfileHeightResult> GetUserHeightCmAsync(Guid userId, CancellationToken ct);
}
