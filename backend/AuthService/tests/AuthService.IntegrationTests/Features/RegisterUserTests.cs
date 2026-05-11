using System.Net;
using System.Net.Http.Json;
using AuthService.Contracts.Requests;
using AuthService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AuthService.IntegrationTests.Features;

public class RegisterUserTests : AuthServiceBaseTests
{
    public RegisterUserTests(AuthServiceTestWebFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task RegisterUser_With_Valid_Data_Should_Create_User()
    {
        var request = new RegisterUserRequest(
            "user@example.com",
            "test-user",
            "12345678901234567890");

        var response = await AppHttpClient.PostAsJsonAsync("/auth/users", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var userCount = await ExecuteInDb(dbContext => dbContext.AuthUsers.CountAsync());
        userCount.Should().Be(1);
    }

    [Fact]
    public async Task RegisterUser_With_Invalid_Email_Should_Return_BadRequest()
    {
        var request = new RegisterUserRequest(
            "invalid-email",
            "test-user",
            "12345678901234567890");

        var response = await AppHttpClient.PostAsJsonAsync("/auth/users", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
