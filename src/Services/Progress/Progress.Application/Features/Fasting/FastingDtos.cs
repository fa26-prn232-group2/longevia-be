using System.Text.Json.Serialization;
using Progress.Domain.Entities;

namespace Progress.Application.Features.Fasting;

public sealed record StartFastingRequest(
    string Protocol,
    DateTimeOffset StartedAt,
    int TargetHours);

public sealed record EndFastingRequest(
    Guid SessionId,
    DateTimeOffset EndedAt);

public sealed record FastingSessionResponse(
    Guid SessionId,
    string Protocol,
    DateTimeOffset StartedAt,
    DateTimeOffset TargetEndedAt,
    string Status);

public sealed record EndFastingResponse(
    Guid SessionId,
    double ActualHours,
    int TargetHours,
    bool IsGoalReached,
    string Status);

public sealed record CurrentFastingResponse(
    Guid SessionId,
    string Protocol,
    DateTimeOffset StartedAt,
    DateTimeOffset TargetEndedAt,
    int TargetHours,
    double ElapsedHours,
    double RemainingHours,
    string Status);

public sealed record FastingHistoryItemResponse(
    Guid SessionId,
    string Protocol,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    int TargetHours,
    double ActualHours,
    bool IsGoalReached,
    string Status);

public sealed record FastingHistoryPageResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<FastingHistoryItemResponse> Items);
