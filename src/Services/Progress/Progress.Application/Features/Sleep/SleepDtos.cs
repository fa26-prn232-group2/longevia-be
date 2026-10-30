namespace Progress.Application.Features.Sleep;

public sealed record LogSleepRequest(
    DateTimeOffset SleptAt,
    DateTimeOffset WokeAt,
    string SleepQuality);

public sealed record SleepResponse(
    Guid Id,
    DateTimeOffset SleptAt,
    DateTimeOffset WokeAt,
    string SleepQuality,
    double DurationHours,
    DateOnly Date);
