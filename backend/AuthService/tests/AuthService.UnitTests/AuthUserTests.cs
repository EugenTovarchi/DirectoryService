using AuthService.Domain.Users;
using FluentAssertions;

namespace AuthService.UnitTests;

public class AuthUserTests
{
    [Fact]
    public void Create_With_Valid_Data_Should_Create_User()
    {
        var email = Email.Create("USER@example.com").Value;
        var username = Username.Create("test-user").Value;
        var passwordHash = PasswordHash.Create("12345678901234567890").Value;

        var result = AuthUser.Create(email, username, passwordHash);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.Email.Value.Should().Be("user@example.com");
        result.Value.Username.Value.Should().Be("test-user");
        result.Value.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.Value.UpdatedAt.Should().Be(result.Value.CreatedAt);
    }
}
