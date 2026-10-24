namespace Progress.Application.Abstractions;

public interface IBodyProfileProvider
{
    Task<decimal?> GetUserHeightCmAsync(Guid userId, CancellationToken ct);
}
