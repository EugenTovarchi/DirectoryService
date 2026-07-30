using FileService.Web.Configurations;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace FileService.UnitTests;

public sealed class ResourceServiceAuthenticationConfigurationTests
{
    [Fact]
    public async Task Production_ShouldRejectSymmetricSigningKey()
    {
        // Arrange
        WebApplicationBuilder builder = CreateBuilder(
            Environments.Production,
            metadataAddress: null,
            signingKey: "file-service-production-signing-key");
        builder.Services.AddResourceServiceAuthentication(
            builder.Configuration,
            builder.Environment);
        await using WebApplication app = builder.Build();

        // Act
        Func<Task> act = () => app.StartAsync();

        // Assert
        await act.Should()
            .ThrowAsync<OptionsValidationException>()
            .WithMessage("*only Testing may use a SigningKey*");
    }

    [Fact]
    public async Task Testing_ShouldAllowSymmetricSigningKey()
    {
        // Arrange
        WebApplicationBuilder builder = CreateBuilder(
            "Testing",
            metadataAddress: null,
            signingKey: "file-service-integration-test-signing-key");
        builder.Services.AddResourceServiceAuthentication(
            builder.Configuration,
            builder.Environment);
        await using WebApplication app = builder.Build();

        // Act
        Func<Task> act = () => app.StartAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Production_ShouldAllowHttpsMetadataWithoutSigningKey()
    {
        // Arrange
        WebApplicationBuilder builder = CreateBuilder(
            Environments.Production,
            metadataAddress: "https://auth-service.example/.well-known/openid-configuration",
            signingKey: null);
        builder.Services.AddResourceServiceAuthentication(
            builder.Configuration,
            builder.Environment);
        await using WebApplication app = builder.Build();

        // Act
        Func<Task> act = () => app.StartAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    private static WebApplicationBuilder CreateBuilder(
        string environmentName,
        string? metadataAddress,
        string? signingKey)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = environmentName
            });
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Jwt:Issuer"] = "https://auth-service.tests/",
            ["Jwt:Audience"] = "file-service",
            ["Jwt:MetadataAddress"] = metadataAddress,
            ["Jwt:SigningKey"] = signingKey
        };
        builder.Configuration.AddInMemoryCollection(settings);

        return builder;
    }
}
