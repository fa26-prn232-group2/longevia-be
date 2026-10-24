using Longevia.Shared.Auth;
using Progress.Application.Abstractions;
using Progress.Application.Common;
using Progress.Domain.Constants;
using Progress.Domain.Entities;

namespace Progress.Application.Features.Fasting;

public sealed class FastingUseCases(
    IFastingRepository fastingRepo,
    IUnitOfWork uow,
    ICurrentUser currentUser,
    TimeProvider time)
{
    public async Task<FastingSessionResponse> StartAsync(StartFastingRequest request, CancellationToken ct)
    {
        var userId = currentUser.RequiredId();

        var hasActive = await fastingRepo.HasActiveSessionAsync(userId, ct);
        if (hasActive)
        {
            throw ProgressErrors.ActiveFastingSessionExists();
        }

        var session = FastingSession.Start(userId, request.Protocol, request.TargetHours, request.StartedAt);
        fastingRepo.Add(session);

        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (IsUniqueConflict(ex))
        {
            throw ProgressErrors.ActiveFastingSessionExists();
        }

        return new FastingSessionResponse(
            session.Id,
            session.Protocol,
            session.StartedAt,
            session.TargetEndedAt,
            session.Status.ToString());
    }

    public async Task<EndFastingResponse> EndAsync(EndFastingRequest request, CancellationToken ct)
    {
        var userId = currentUser.RequiredId();

        var session = await fastingRepo.EndSessionWithLockAsync(request.SessionId, userId, request.EndedAt, ct);
        if (session == null)
        {
            throw ProgressErrors.FastingSessionNotFound();
        }

        return new EndFastingResponse(
            session.Id,
            session.ActualHours,
            session.TargetHours,
            session.IsGoalReached,
            session.Status.ToString());
    }

    public async Task<CurrentFastingResponse?> GetCurrentAsync(CancellationToken ct)
    {
        var userId = currentUser.RequiredId();

        var session = await fastingRepo.GetActiveSessionAsync(userId, ct);
        if (session == null)
        {
            return null;
        }

        var now = time.GetUtcNow();
        var elapsedHours = Math.Max(0, Math.Round((now - session.StartedAt).TotalHours, 2));
        var remainingHours = Math.Max(0, Math.Round((session.TargetEndedAt - now).TotalHours, 2));

        return new CurrentFastingResponse(
            session.Id,
            session.Protocol,
            session.StartedAt,
            session.TargetEndedAt,
            session.TargetHours,
            elapsedHours,
            remainingHours,
            session.Status.ToString());
    }

    private static bool IsUniqueConflict(Exception ex)
    {
        // Kiểm tra message hoặc inner exception liên quan unique violation
        var current = ex;
        while (current != null)
        {
            if (current.Message.Contains("23505") || current.Message.Contains("unique", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            current = current.InnerException;
        }
        return false;
    }
}
