namespace Progress.IntegrationTests;

public sealed class FoundationTimeProvider : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(2026, 10, 25, 0, 0, 0, TimeSpan.Zero);
}
