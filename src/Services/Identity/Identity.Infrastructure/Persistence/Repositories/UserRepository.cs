using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(IdentityDbContext dbContext) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> GetByIdWithProfileAsync(Guid id, CancellationToken ct = default) =>
        await dbContext.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        await dbContext.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default) =>
        await dbContext.Users.AnyAsync(u => u.Email == email, ct);

    public async Task<bool> TryVerifyTokenAsync(
        string tokenHash,
        DateTimeOffset verifiedAt,
        CancellationToken ct = default) =>
        await dbContext.Users
            .Where(u => u.VerificationTokenHash == tokenHash
                && u.Status == Identity.Domain.Constants.UserStatus.PendingVerification
                && u.VerificationTokenExpiresAt > verifiedAt
                && u.VerificationTokenConsumedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.Status, Identity.Domain.Constants.UserStatus.Active)
                .SetProperty(u => u.VerificationTokenConsumedAt, (DateTimeOffset?)verifiedAt)
                .SetProperty(u => u.UpdatedAt, (DateTimeOffset?)verifiedAt), ct) == 1;

    public async Task AddAsync(User user, CancellationToken ct = default) =>
        await dbContext.Users.AddAsync(user, ct);

    public void Update(User user) =>
        dbContext.Users.Update(user);

    public void AddProfile(UserProfile profile) =>
        dbContext.UserProfiles.Add(profile);
}
