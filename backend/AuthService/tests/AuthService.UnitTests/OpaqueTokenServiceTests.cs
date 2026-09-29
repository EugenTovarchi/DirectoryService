using AuthService.Core.Abstractions;
using AuthService.Core.Services;
using FluentAssertions;

namespace AuthService.UnitTests;

public sealed class OpaqueTokenServiceTests
{
    [Fact]
    public void CreateToken_Should_Create_Unique_UrlSafe_Tokens_With_Matching_Hashes()
    {
        // Arrange
        var service = new OpaqueTokenService();

        // Act
        OpaqueToken firstToken = service.CreateToken();
        OpaqueToken secondToken = service.CreateToken();

        // Assert
        firstToken.RawToken.Should().MatchRegex("^[0-9a-f]{128}$");
        firstToken.TokenHash.Should().MatchRegex("^[0-9a-f]{64}$");
        firstToken.TokenHash.Should().Be(service.HashToken(firstToken.RawToken));
        secondToken.RawToken.Should().NotBe(firstToken.RawToken);
        secondToken.TokenHash.Should().NotBe(firstToken.TokenHash);
    }

    [Fact]
    public void HashToken_Should_Be_Deterministic_And_Input_Sensitive()
    {
        // Arrange
        var service = new OpaqueTokenService();

        // Act
        string firstHash = service.HashToken("invite-token");
        string repeatedHash = service.HashToken("invite-token");
        string differentHash = service.HashToken("different-invite-token");

        // Assert
        repeatedHash.Should().Be(firstHash);
        differentHash.Should().NotBe(firstHash);
    }
}
