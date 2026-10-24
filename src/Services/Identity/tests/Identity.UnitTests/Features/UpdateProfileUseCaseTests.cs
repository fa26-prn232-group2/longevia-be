using Identity.Application.Common.Events;
using Identity.Application.Features.Users;
using Identity.Domain.Constants;
using Identity.Domain.Entities;
using Identity.UnitTests.Common;
using Longevia.Shared.Errors;

namespace Identity.UnitTests.Features;

public sealed class UpdateProfileUseCaseTests
{
    private readonly FakeCurrentUser _currentUser = new();
    private readonly InMemoryUserRepository _userRepository = new();
    private readonly FakeEventPublisher _eventPublisher = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly UpdateProfileUseCase _sut;

    public UpdateProfileUseCaseTests()
    {
        _sut = new UpdateProfileUseCase(
            _currentUser,
            _userRepository,
            _eventPublisher,
            _unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateProfile_WhenProfileDoesNotExist()
    {
        var user = User.Create(
            Guid.NewGuid(),
            "profile.user@example.com",
            "hashed",
            "Profile User",
            DateTimeOffset.UtcNow,
            "hash",
            DateTimeOffset.UtcNow.AddHours(24));
        await _userRepository.AddAsync(user);

        _currentUser.UserId = user.Id;

        var request = new UpdateProfileRequest(
            new DateOnly(1998, 3, 15),
            BiologicalSex.Male,
            180,
            ActivityLevel.VeryActive,
            DietaryPreference.Keto,
            ["Seafood"]);

        var result = await _sut.ExecuteAsync(request);

        Assert.NotNull(result);
        Assert.Equal(180, result.HeightCm);
        Assert.Equal(BiologicalSex.Male, result.BiologicalSex);
        Assert.Equal(DietaryPreference.Keto, result.DietaryPreference);
        Assert.Contains("Seafood", result.Allergies);

        Assert.NotNull(user.Profile);
        Assert.Equal(1, _unitOfWork.SaveChangesCalls);
        Assert.Single(_eventPublisher.PublishedEvents);
        Assert.IsType<UserProfileUpdatedIntegrationEvent>(_eventPublisher.PublishedEvents[0]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated()
    {
        _currentUser.UserId = null;

        var request = new UpdateProfileRequest(
            new DateOnly(1998, 3, 15),
            BiologicalSex.Male,
            180,
            ActivityLevel.VeryActive,
            DietaryPreference.Keto,
            []);

        var ex = await Assert.ThrowsAsync<AppException>(() => _sut.ExecuteAsync(request));

        Assert.Equal(401, ex.Status);
        Assert.Equal("identity.unauthorized", ex.Code);
    }
}
