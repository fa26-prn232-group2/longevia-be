using Identity.Application.Abstractions;
using Identity.Application.Features.Registration;

namespace Identity.IntegrationTests;

/// <summary>In-memory thay Redis trong integration tests, tránh phụ thuộc container Redis.</summary>
public sealed class InMemoryPendingRegistrationStore : IPendingRegistrationStore
{
    private readonly Dictionary<Guid, PendingRegistration> _records = [];
    private readonly object _gate = new();

    public IReadOnlyCollection<PendingRegistration> Records
    {
        get
        {
            lock (_gate)
            {
                return _records.Values.ToArray();
            }
        }
    }

    public Task SaveAsync(PendingRegistration registration, CancellationToken ct = default)
    {
        lock (_gate)
        {
            foreach (var existing in _records.Values
                         .Where(record => record.Email == registration.Email && record.Id != registration.Id)
                         .ToList())
            {
                _records.Remove(existing.Id);
            }

            _records[registration.Id] = registration;
        }

        return Task.CompletedTask;
    }

    public Task<PendingRegistration?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_records.Values.FirstOrDefault(record =>
                record.Email.Equals(email, StringComparison.OrdinalIgnoreCase)
                && record.ExpiresAt > DateTimeOffset.UtcNow));
        }
    }

    public Task<PendingRegistration?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_records.Values.FirstOrDefault(record =>
                record.VerificationTokenHash == tokenHash
                && record.ExpiresAt > DateTimeOffset.UtcNow));
        }
    }

    public Task DeleteAsync(PendingRegistration registration, CancellationToken ct = default)
    {
        lock (_gate)
        {
            _records.Remove(registration.Id);
        }

        return Task.CompletedTask;
    }
}
