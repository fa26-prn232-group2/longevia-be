namespace Diet.Application.Abstractions;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);

    /// <summary>
    /// Chạy <paramref name="action"/> trong một transaction, giữ advisory lock theo
    /// <paramref name="lockKey"/> để tuần tự hoá các thao tác ghi cùng phạm vi
    /// (ví dụ thay thế bữa ăn của cùng user/ngày/loại bữa).
    /// </summary>
    Task ExecuteInTransactionAsync(string lockKey, Func<CancellationToken, Task> action,
        CancellationToken ct);
}
