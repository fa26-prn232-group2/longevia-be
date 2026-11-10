using Grpc.Core;
using Longevia.Identity.Contracts.V1;
using Microsoft.Extensions.Options;
using Progress.Application.Abstractions;

namespace Progress.Infrastructure.Adapters.Grpc;

public sealed class UserProfileProvider(IdentityService.IdentityServiceClient client,
    IOptions<IdentityGrpcOptions> options) : IUserProfileProvider
{
    public async Task<UserProfileResult> GetAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            var reply = await client.GetUserProfileAsync(new GetUserProfileRequest { UserId = userId.ToString("D") },
                deadline: DateTime.UtcNow.AddMilliseconds(options.Value.DeadlineMilliseconds),
                cancellationToken: ct).ResponseAsync;
            if (!DateOnly.TryParseExact(reply.BirthDate, "yyyy-MM-dd", out var birthDate))
                throw new InvalidOperationException("Identity birth_date không hợp lệ.");
            var today = Progress.Domain.UtcCalendar.Day(DateTimeOffset.UtcNow);
            var age = today.Year - birthDate.Year - (today < birthDate.AddYears(today.Year - birthDate.Year) ? 1 : 0);
            var male = string.Equals(reply.BiologicalSex, "Male", StringComparison.OrdinalIgnoreCase);
            return new(age, male, Math.Round(reply.HeightCmX100 / 100m, 2), reply.ActivityLevel,
                reply.DietaryPreference ?? "None", reply.Allergies.ToArray(),
                reply.HealthLimitations.ToArray());
        }
        catch (RpcException ex)
        {
            throw new InvalidOperationException($"Identity profile call failed: {ex.StatusCode}", ex);
        }
    }
}

public sealed class UserContactProvider(IdentityService.IdentityServiceClient client,
    IOptions<IdentityGrpcOptions> options) : IUserContactProvider
{
    public async Task<UserContactResult> GetAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            var reply = await client.GetUserContactAsync(new GetUserContactRequest { UserId = userId.ToString("D") },
                deadline: DateTime.UtcNow.AddMilliseconds(options.Value.DeadlineMilliseconds),
                cancellationToken: ct).ResponseAsync;
            return new(reply.Email, reply.FullName, reply.Status, reply.ReceiveWeeklyDigest);
        }
        catch (RpcException ex)
        {
            throw new InvalidOperationException($"Identity contact call failed: {ex.StatusCode}", ex);
        }
    }
}
