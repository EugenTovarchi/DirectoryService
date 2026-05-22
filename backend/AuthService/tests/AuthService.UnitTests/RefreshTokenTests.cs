using AuthService.Domain.Identity;
using CSharpFunctionalExtensions;
using FluentAssertions;
using SharedService.SharedKernel;

namespace AuthService.UnitTests;

public sealed class RefreshTokenTests
{
    [Fact]
    public void Create_With_Valid_Data_Should_Create_Refresh_Token()
    {
        Guid userId = Guid.NewGuid();
        string tokenHash = new('a', RefreshToken.TOKEN_HASH_LENGTH);
        DateTime expiresAt = DateTime.UtcNow.AddDays(30);

        Result<RefreshToken, Error> result = RefreshToken.Create(
            userId,
            tokenHash,
            expiresAt,
            "127.0.0.1",
            "UnitTests/1.0");

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.UserId.Should().Be(userId);
        result.Value.TokenHash.Should().Be(tokenHash);
        result.Value.ExpiresAt.Should().Be(expiresAt);
        result.Value.CreatedByIp.Should().Be("127.0.0.1");
        result.Value.UserAgent.Should().Be("UnitTests/1.0");
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_With_Empty_UserId_Should_Return_Failure()
    {
        Result<RefreshToken, Error> result = RefreshToken.Create(
            Guid.Empty,
            new string('a', RefreshToken.TOKEN_HASH_LENGTH),
            DateTime.UtcNow.AddDays(30),
            null,
            null);

        result.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("short")]
    public void Create_With_Invalid_TokenHash_Should_Return_Failure(string tokenHash)
    {
        Result<RefreshToken, Error> result = RefreshToken.Create(
            Guid.NewGuid(),
            tokenHash,
            DateTime.UtcNow.AddDays(30),
            null,
            null);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_With_Expired_ExpiresAt_Should_Return_Failure()
    {
        Result<RefreshToken, Error> result = RefreshToken.Create(
            Guid.NewGuid(),
            new string('a', RefreshToken.TOKEN_HASH_LENGTH),
            DateTime.UtcNow.AddDays(-1),
            null,
            null);

        result.IsFailure.Should().BeTrue();
    }
}
