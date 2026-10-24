using Identity.Application.Features.Users;
using Identity.Domain.Constants;
using Identity.Domain.Entities;
using Identity.UnitTests.Common;
using Longevia.Shared.Errors;
using Longevia.Shared.Events;

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
            180.5m,
            ActivityLevel.VeryActive,
            DietaryPreference.Keto,
            [" Seafood ", "Seafood"]);

        var result = await _sut.ExecuteAsync(request);

        Assert.NotNull(result);
        Assert.Equal(180.5m, result.HeightCm);
        Assert.Equal(BiologicalSex.Male, result.BiologicalSex);
        Assert.Equal(DietaryPreference.Keto, result.DietaryPreference);
        Assert.Equal(["Seafood"], result.Allergies);

        Assert.NotNull(user.Profile);
        Assert.Equal(1, _unitOfWork.SaveChangesCalls);
        Assert.Single(_eventPublisher.PublishedEvents);
        var publishedEvent = Assert.IsType<UserProfileUpdatedEvent>(_eventPublisher.PublishedEvents[0]);
        Assert.Equal(user.Id, publishedEvent.UserId);
        Assert.NotEqual(default, publishedEvent.OccurredOn);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotAccessOrPublishAnotherUsersProfile()
    {
        var anotherUser = User.Create(
            Guid.NewGuid(), "another.user@example.com", "hashed", "Another User",
            DateTimeOffset.UtcNow, "hash", DateTimeOffset.UtcNow.AddHours(24));
        await _userRepository.AddAsync(anotherUser);
        _currentUser.UserId = Guid.NewGuid();

        var request = new UpdateProfileRequest(
            new DateOnly(1998, 3, 15), BiologicalSex.Male, 180.5m,
            ActivityLevel.VeryActive, DietaryPreference.Keto, []);

        var ex = await Assert.ThrowsAsync<AppException>(() => _sut.ExecuteAsync(request));

        Assert.Equal(404, ex.Status);
        Assert.Empty(_eventPublisher.PublishedEvents);
        Assert.Equal(0, _unitOfWork.SaveChangesCalls);
        Assert.Null(anotherUser.Profile);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowUnauthorized_WhenUserIsNotAuthenticated()
    {
        _currentUser.UserId = null;

        var request = new UpdateProfileRequest(
            new DateOnly(1998, 3, 15),
            BiologicalSex.Male,
            180.5m,
            ActivityLevel.VeryActive,
            DietaryPreference.Keto,
            []);

        var ex = await Assert.ThrowsAsync<AppException>(() => _sut.ExecuteAsync(request));

        Assert.Equal(401, ex.Status);
        Assert.Equal("identity.unauthorized", ex.Code);
    }
}
