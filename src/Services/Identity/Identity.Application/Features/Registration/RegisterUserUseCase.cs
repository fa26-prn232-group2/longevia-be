using System.Security.Cryptography;
using Identity.Application.Abstractions;
using Identity.Application.Common.Dtos;
using Identity.Application.Common.Errors;
using Identity.Application.Common.Mappers;
using Identity.Domain.Entities;
using Identity.Domain.Repositories;
using Longevia.Shared.Events;

namespace Identity.Application.Features.Registration;

public sealed class RegisterUserUseCase(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IEventPublisher eventPublisher,
    IUnitOfWork unitOfWork)
{
    public async Task<UserResponse> ExecuteAsync(RegisterUserRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var exists = await userRepository.ExistsByEmailAsync(normalizedEmail, ct);
        if (exists)
        {
            throw UserErrors.EmailTaken(request.Email);
        }

        var passwordHash = passwordHasher.Hash(request.Password);
        var verificationToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var now = DateTimeOffset.UtcNow;

        var user = User.Create(
            Guid.NewGuid(),
            normalizedEmail,
            passwordHash,
            request.FullName,
            now,
            verificationToken);

        await userRepository.AddAsync(user, ct);

        await eventPublisher.PublishAsync(
            new UserRegisteredEvent(
                user.Id,
                user.Email,
                user.FullName,
                verificationToken,
                now),
            ct);

        await unitOfWork.SaveChangesAsync(ct);

        return user.ToResponse();
    }
}
