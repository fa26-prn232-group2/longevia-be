using System.Globalization;
using Diet.Application.Abstractions;
using Grpc.Core;
using Longevia.Identity.Contracts.V1;
using Longevia.Shared.Errors;

namespace Diet.Infrastructure.Grpc;

public sealed class IdentityProfileProvider(IdentityService.IdentityServiceClient client,
    TimeProvider time) : IProfileProvider
{
    public async Task<UserProfileSnapshot> GetAsync(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User ID is required.", nameof(userId));
        ct.ThrowIfCancellationRequested();
        GetUserProfileResponse reply;
        try
        {
            reply = await client.GetUserProfileAsync(new() { UserId = userId.ToString("D") },
                deadline: time.GetUtcNow().AddSeconds(2).UtcDateTime, cancellationToken: ct).ResponseAsync;
        }
        catch (RpcException) when (ct.IsCancellationRequested) { throw new OperationCanceledException(ct); }
        catch (RpcException ex)
        {
            throw ex.StatusCode switch
            {
                StatusCode.NotFound => AppException.Unprocessable("Bạn chưa có hồ sơ thể trạng.", "diet.profile_not_found"),
                StatusCode.DeadlineExceeded => new AppException("Lấy hồ sơ quá thời gian chờ.", 504,
                    "Dịch vụ phụ thuộc quá hạn.", "diet.profile_timeout"),
                StatusCode.Unavailable => AppException.ServiceUnavailable("Chưa thể lấy hồ sơ.", "diet.profile_unavailable"),
                _ => InvalidResponse(),
            };
        }
        if (!DateOnly.TryParseExact(reply.BirthDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var birthDate) || reply.BiologicalSex is not ("Male" or "Female") ||
            reply.HeightCmX100 is < 5000 or > 25000 ||
            reply.ActivityLevel is not ("Sedentary" or "LightlyActive" or "ModeratelyActive" or "VeryActive") ||
            (reply.HasDietaryPreference && reply.DietaryPreference is not ("None" or "Keto" or "Vegan" or "Mediterranean" or "LowCarb")) ||
            reply.Allergies.Any(string.IsNullOrWhiteSpace)) throw InvalidResponse();
        var profile = new UserProfileSnapshot(birthDate, reply.BiologicalSex == "Male", reply.HeightCmX100 / 100m,
            reply.ActivityLevel, reply.HasDietaryPreference ? reply.DietaryPreference : null,
            reply.HasDietaryPreference ? reply.Allergies.ToArray() : null);
        if (profile.AgeOn(DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime)) is < 1 or > 120)
            throw AppException.Unprocessable("Hồ sơ chưa phù hợp để tính nhu cầu năng lượng.", "diet.profile_not_supported");
        return profile;
    }
    private static AppException InvalidResponse() => new("Dữ liệu hồ sơ chưa thể sử dụng.", 502,
        "Lỗi dịch vụ phụ thuộc.", "diet.profile_integration_failed");
}
