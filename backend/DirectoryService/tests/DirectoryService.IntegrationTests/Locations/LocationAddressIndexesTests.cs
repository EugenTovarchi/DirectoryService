using DirectoryService.Contracts.ValueObjects;
using DirectoryService.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TimeZone = DirectoryService.Contracts.ValueObjects.TimeZone;

namespace DirectoryService.IntegrationTests.Locations;

public class LocationAddressIndexesTests : DirectoryBaseTests
{
    public LocationAddressIndexesTests(DirectoryTestWebFactory factory)
        : base(factory) { }

    [Fact]
    public async Task CreateLocations_With_Same_Address_Should_Succeed()
    {
        // Arrange
        var firstAddress = Address.CreateWithFlat("RF", "Moscow", "Lenina", "12", 3).Value;
        var secondAddress = Address.CreateWithFlat("RF", "Moscow", "Lenina", "12", 3).Value;
        var firstLocation = Location.Create(
            Name.Create("Sales office").Value,
            TimeZone.Create("Europe/Moscow").Value,
            firstAddress).Value;
        var secondLocation = Location.Create(
            Name.Create("Legal office").Value,
            TimeZone.Create("Europe/Moscow").Value,
            secondAddress).Value;

        // Act
        await ExecuteInDb(async dbContext =>
        {
            dbContext.Locations.AddRange(firstLocation, secondLocation);
            await dbContext.SaveChangesAsync(CancellationToken.None);
        });

        // Assert
        await ExecuteInDb(async dbContext =>
        {
            var locationsCount = await dbContext.Locations.CountAsync(CancellationToken.None);
            locationsCount.Should().Be(2);
        });
    }
}
