using Diet.Application.Abstractions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Longevia.Progress.Contracts.V1;
using Longevia.Shared.Errors;

namespace Diet.Infrastructure.Grpc;

public sealed class ProgressWeightProvider(ProgressService.ProgressServiceClient client,
    TimeProvider time) : IWeightProvider
{
    public async Task<WeightSnapshot?> GetLatestAsync(Guid userId, DateTimeOffset asOf, CancellationToken ct)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User ID is required.", nameof(userId));
        ct.ThrowIfCancellationRequested();
        var now = time.GetUtcNow();
        var cutoff = (asOf < now ? asOf : now).ToUniversalTime();
        try
        {
            var reply = await client.GetLatestWeightAsync(new GetLatestWeightRequest
            {
                UserId = userId.ToString("D"),
                AsOfUtc = Timestamp.FromDateTimeOffset(cutoff),
            }, deadline: now.AddSeconds(2).UtcDateTime, cancellationToken: ct).ResponseAsync;
            if (reply.ResultCase == GetLatestWeightResponse.ResultOneofCase.NoRecordFound && reply.NoRecordFound)
                return null;
            if (reply.ResultCase != GetLatestWeightResponse.ResultOneofCase.Record ||
                reply.Record.WeightKgX100 <= 0 || reply.Record.MeasuredAt is null ||
                !Guid.TryParse(reply.Record.SourceMetricId, out var sourceId) || sourceId == Guid.Empty)
                throw InvalidResponse();
            DateTimeOffset measuredAt;
            try { measuredAt = reply.Record.MeasuredAt.ToDateTimeOffset(); }
            catch (InvalidOperationException) { throw InvalidResponse(); }
            if (measuredAt > cutoff) throw InvalidResponse();
            return new(reply.Record.WeightKgX100 / 100m, measuredAt, sourceId, cutoff);
        }
        catch (RpcException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
        catch (RpcException ex)
        {
            throw ex.StatusCode switch
            {
                StatusCode.DeadlineExceeded => new AppException("Lấy cân nặng quá thời gian chờ.",
                    504, "Dịch vụ phụ thuộc quá hạn.", "diet.weight_timeout"),
                StatusCode.Unavailable => AppException.ServiceUnavailable(
                    "Không thể lấy cân nặng lúc này.", "diet.weight_unavailable"),
                _ => InvalidResponse(),
            };
        }
    }

    private static AppException InvalidResponse() => new("Dữ liệu cân nặng chưa thể sử dụng.",
        502, "Lỗi dịch vụ phụ thuộc.", "diet.weight_integration_failed");
}
