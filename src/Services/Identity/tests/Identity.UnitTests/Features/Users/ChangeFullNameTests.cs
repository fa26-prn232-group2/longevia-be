using Identity.Application.Features.Users;
using Identity.Domain.Entities;
using Identity.UnitTests.Common;

namespace Identity.UnitTests.Features.Users;

public sealed class ChangeFullNameTests
{
    private readonly InMemoryUserRepository _users = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FakeCurrentUser _currentUser = new();

    private ChangeFullNameUseCase CreateSut() => new(_currentUser, _users, _uow);

    [Fact]
    public async Task ChangesNameAndTrimsWhitespace()
    {
        var user = CreateUser("Old Name");
        await _users.AddAsync(user);
        _currentUser.UserId = user.Id;

        var response = await CreateSut().ExecuteAsync(new ChangeFullNameRequest("  New Name  "));

        Assert.Equal("New Name", response.FullName);
        Assert.Equal(1, _uow.SaveChangesCalls);
        Assert.Equal("New Name", (await _users.GetByIdAsync(user.Id))!.FullName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RejectsBlankNameAtDomainLevel(string name)
    {
        var user = CreateUser("Old Name");
        await _users.AddAsync(user);
        _currentUser.UserId = user.Id;

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            CreateSut().ExecuteAsync(new ChangeFullNameRequest(name)));
        Assert.Equal(0, _uow.SaveChangesCalls);
    }

    [Fact]
    public async Task RejectsNameLongerThanOneHundredCharacters()
    {
        var user = CreateUser("Old Name");
        await _users.AddAsync(user);
        _currentUser.UserId = user.Id;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            CreateSut().ExecuteAsync(new ChangeFullNameRequest(new string('x', 101))));
        Assert.Equal(0, _uow.SaveChangesCalls);
    }

    private static User CreateUser(string fullName) =>
        User.Create(Guid.NewGuid(), $"{Guid.NewGuid():N}@example.com", "hash", fullName,
            DateTimeOffset.UtcNow, "verification-hash", DateTimeOffset.UtcNow.AddHours(24));
}
