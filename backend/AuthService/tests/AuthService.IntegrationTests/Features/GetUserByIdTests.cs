using System.Net;
using System.Net.Http.Json;
using AuthService.Contracts.Requests;
using AuthService.Contracts.Responses;
using AuthService.IntegrationTests.Infrastructure;
using FluentAssertions;
using SharedService.SharedKernel;

namespace AuthService.IntegrationTests.Features;

public class GetUserByIdTests : AuthServiceBaseTests
{
    public GetUserByIdTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task GetUserById_With_Existing_User_Should_Return_User()
    {
        var request = new RegisterUserRequest(
            "user@example.com",
            "test-user",
            "12345678901234567890");

        var createResponse = await AppHttpClient.PostAsJsonAsync("/auth/users", request);
        var envelope = await createResponse.Content.ReadFromJsonAsync<Envelope<Guid>>();

        var response = await AppHttpClient.GetAsync($"/auth/users/{envelope!.Result}");
        var userEnvelope = await response.Content.ReadFromJsonAsync<Envelope<AuthUserResponse>>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        userEnvelope!.Result!.Email.Should().Be("user@example.com");
        userEnvelope.Result.Username.Should().Be("test-user");
    }

    [Fact]
    public async Task GetUserById_With_Missing_User_Should_Return_NotFound()
    {
        var response = await AppHttpClient.GetAsync($"/auth/users/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
