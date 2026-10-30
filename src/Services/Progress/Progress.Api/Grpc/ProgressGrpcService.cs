using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Longevia.Progress.Contracts.V1;
using Progress.Application.Features.Metrics;

namespace Progress.Api.Grpc;

public sealed class ProgressGrpcService(
    IWeightQueryService weightQueryService,
    ILogger<ProgressGrpcService> logger)
    : ProgressService.ProgressServiceBase
{
    public override async Task<GetLatestWeightResponse> GetLatestWeight(
        GetLatestWeightRequest request,
        ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.UserId) ||
            !Guid.TryParse(request.UserId, out var userId) ||
            userId == Guid.Empty)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "user_id must be a valid, non-empty GUID."));
        }

        DateTimeOffset? requestedAsOf = request.AsOfUtc != null
            ? request.AsOfUtc.ToDateTimeOffset()
            : null;

        try
        {
            var result = await weightQueryService.GetLatestWeightAsync(userId, requestedAsOf, context.CancellationToken);
            if (!result.Found || !result.WeightKgX100.HasValue || !result.MeasuredAt.HasValue || !result.SourceMetricId.HasValue)
            {
                return new GetLatestWeightResponse { NoRecordFound = true };
            }

            return new GetLatestWeightResponse
            {
                Record = new WeightRecord
                {
                    WeightKgX100 = result.WeightKgX100.Value,
                    MeasuredAt = Timestamp.FromDateTimeOffset(result.MeasuredAt.Value),
                    SourceMetricId = result.SourceMetricId.Value.ToString()
                }
            };
        }
        catch (ArgumentException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, "Request was cancelled by client."));
        }
        catch (OperationCanceledException)
        {
            throw new RpcException(new Status(StatusCode.DeadlineExceeded, "The deadline was exceeded."));
        }
        catch (Exception ex) when (IsDatabaseUnavailable(ex))
        {
            logger.LogWarning(ex, "Database unavailable while querying latest weight for user {UserId}", userId);
            throw new RpcException(new Status(StatusCode.Unavailable, "Database service is temporarily unavailable."));
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while querying latest weight for user {UserId}", userId);
            throw new RpcException(new Status(StatusCode.Internal, "An internal error occurred while processing the request."));
        }
    }

    private static bool IsDatabaseUnavailable(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is Npgsql.NpgsqlException or System.Net.Sockets.SocketException or TimeoutException)
            {
                return true;
            }
        }
        return false;
    }
}
