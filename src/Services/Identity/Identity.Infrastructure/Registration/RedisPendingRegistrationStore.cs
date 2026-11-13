using System.Text.Json;
using Identity.Application.Abstractions;
using Identity.Application.Features.Registration;
using StackExchange.Redis;

namespace Identity.Infrastructure.Registration;

/// <summary>
/// Redis-backed pending registration store. Mỗi email có một bản ghi; đăng ký
/// lại sẽ xoá token cũ và ghi đè record/token mới (TTL 1 giờ).
/// </summary>
public sealed class RedisPendingRegistrationStore(IConnectionMultiplexer redis) : IPendingRegistrationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task SaveAsync(PendingRegistration registration, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var ttl = registration.ExpiresAt - DateTimeOffset.UtcNow;
        if (ttl <= TimeSpan.Zero)
        {
            await DeleteAsync(registration, ct);
            return;
        }

        var oldIdValue = await _db.StringGetAsync(EmailKey(registration.Email));
        if (oldIdValue.HasValue && Guid.TryParse(oldIdValue.ToString(), out var oldId))
        {
            var oldRecord = await ReadRecordAsync(oldId);
            if (oldRecord is not null)
            {
                await _db.KeyDeleteAsync(TokenKey(oldRecord.VerificationTokenHash));
            }

            if (oldId != registration.Id)
            {
                await _db.KeyDeleteAsync(RecordKey(oldId));
            }
        }

        var json = JsonSerializer.Serialize(registration, JsonOptions);
        var idValue = registration.Id.ToString("N");
        await _db.StringSetAsync(RecordKey(registration.Id), json, ttl);
        await _db.StringSetAsync(EmailKey(registration.Email), idValue, ttl);
        await _db.StringSetAsync(TokenKey(registration.VerificationTokenHash), idValue, ttl);
    }

    public async Task<PendingRegistration?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var idValue = await _db.StringGetAsync(EmailKey(email));
        if (!idValue.HasValue || !Guid.TryParse(idValue.ToString(), out var id))
        {
            return null;
        }

        var record = await ReadRecordAsync(id);
        if (record is null)
        {
            await _db.KeyDeleteAsync(EmailKey(email));
            return null;
        }

        if (record.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            await DeleteAsync(record, ct);
            return null;
        }

        return record;
    }

    public async Task<PendingRegistration?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var idValue = await _db.StringGetAsync(TokenKey(tokenHash));
        if (!idValue.HasValue || !Guid.TryParse(idValue.ToString(), out var id))
        {
            return null;
        }

        var record = await ReadRecordAsync(id);
        if (record is null
            || !string.Equals(record.VerificationTokenHash, tokenHash, StringComparison.Ordinal)
            || record.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            if (record is not null)
            {
                await DeleteAsync(record, ct);
            }

            return null;
        }

        return record;
    }

    public async Task DeleteAsync(PendingRegistration registration, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        await _db.KeyDeleteAsync(RecordKey(registration.Id));
        await _db.KeyDeleteAsync(EmailKey(registration.Email));
        await _db.KeyDeleteAsync(TokenKey(registration.VerificationTokenHash));
    }

    private async Task<PendingRegistration?> ReadRecordAsync(Guid id)
    {
        var json = await _db.StringGetAsync(RecordKey(id));
        return json.HasValue
            ? JsonSerializer.Deserialize<PendingRegistration>(json.ToString(), JsonOptions)
            : null;
    }

    private static string RecordKey(Guid id) => $"identity:pending:{id:N}";
    private static string EmailKey(string email) => $"identity:pending:email:{email}";
    private static string TokenKey(string tokenHash) => $"identity:pending:token:{tokenHash}";
}
