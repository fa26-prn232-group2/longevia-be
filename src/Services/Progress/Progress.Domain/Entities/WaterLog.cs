namespace Progress.Domain.Entities;

public sealed class WaterLog
{
    private WaterLog() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public int AmountMl { get; private set; }
    public DateOnly Date { get; private set; }
    public DateTimeOffset LoggedAt { get; private set; }

    public static WaterLog Create(Guid userId, int amountMl, DateOnly date, DateTimeOffset loggedAt)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId không được để trống.", nameof(userId));
        }

        if (amountMl < 1 || amountMl > 5000)
        {
            throw new ArgumentOutOfRangeException(nameof(amountMl), "Lượng nước phải từ 1 đến 5000 ml.");
        }

        return new WaterLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AmountMl = amountMl,
            Date = date,
            LoggedAt = loggedAt
        };
    }
}
