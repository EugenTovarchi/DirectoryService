using System.Net;
using System.Text;
using AuthService.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace AuthService.IntegrationTests.Features;

/// <summary>
/// Подтверждает удаление custom пользовательской выдачи access/refresh JWT.
/// </summary>
public sealed class LegacyAuthEndpointsTests : AuthServiceBaseTests
{
    public LegacyAuthEndpointsTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/api/auth/refresh")]
    [InlineData("/api/auth/logout")]
    public async Task LegacyAuthEndpoint_Should_Return_NotFound(string requestUri)
    {
        // Arrange
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

        // Act
        using HttpResponseMessage response = await AppHttpClient.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
