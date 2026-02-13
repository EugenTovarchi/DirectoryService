using DirectoryService.Application.Commands.Departments.Create;
using DirectoryService.Contracts.Requests.Departments;
using DirectoryService.Contracts.ValueObjects;
using DirectoryService.Contracts.ValueObjects.Ids;
using DirectoryService.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimeZone = DirectoryService.Contracts.ValueObjects.TimeZone;

namespace DirectoryService.IntegrationTests.Departments.Create;

public class CreateDepartmentTests : DirectoryBaseTests
{
    public CreateDepartmentTests(DirectoryTestWebFactory factory)
        : base(factory) { }

    [Fact]
    public async Task CreateDepartment_with_valid_data_should_succeed()
    {
        // Arrange
        var locationId = await CreateLocation("location1");
        var request = new CreateDepartmentRequest("testName", "testIdentifier", [locationId.Value], null);
        var command = new CreateDepartmentCommand(request);

        // Act
        var result = await ExecuteHandler((sut) =>
        {
            return sut.Handle(command, CancellationToken.None);
        });

        // Assert
        await ExecuteInDb(async dbContext =>
        {
            var department = await dbContext.Departments.FirstAsync(d => d.Id == result.Value, CancellationToken.None);

            Assert.NotEqual(Guid.Empty, result.Value);
            Assert.Equal(result.Value, department.Id.Value);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeEmpty();
        });
    }

    [Fact]
    public async Task CreateDepartment_with_few_locations_should_succeed()
    {
        // Arrange
        var locationId1 = await CreateLocation("location1");
        var locationId2 = await CreateLocation("location2");

        List<Guid> locations = [locationId1, locationId2];
        List<LocationId> locationsForCheck = [locationId1, locationId2];

        var request = new CreateDepartmentRequest("testName", "testIdentifier", locations, null);
        var command = new CreateDepartmentCommand(request);

        // Act
        var result = await ExecuteHandler((sut) =>
        {
            return sut.Handle(command, CancellationToken.None);
        });

        // Assert
        await ExecuteInDb(async dbContext =>
        {
            var department = await dbContext.Departments
                .Include(d => d.DepartmentLocations)
                .FirstAsync(d => d.Id == result.Value, CancellationToken.None);

            department.DepartmentLocations.Should().HaveCount(2);

            Assert.NotEqual(Guid.Empty, result.Value);
            Assert.Equal(result.Value, department.Id.Value);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeEmpty();
        });
    }

    [Fact]
    public async Task CreateDepartment_with_invalid_data_should_failed()
    {
        // Arrange
        var locationId = await CreateLocation("location1");
        var request = new CreateDepartmentRequest(string.Empty, "testIdentifier", [locationId.Value], null);
        var command = new CreateDepartmentCommand(request);

        // Act
        var result = await ExecuteHandler((sut) =>
        {
            return sut.Handle(command, CancellationToken.None);
        });

        // Assert
        await ExecuteInDb(async dbContext =>
        {
            result.IsSuccess.Should().BeFalse();
            Assert.True(result.IsFailure);
            Assert.NotEmpty(result.Error);

            bool departments = await dbContext.Departments.AnyAsync();
            departments.Should().BeFalse();
        });
    }

    private async Task<LocationId> CreateLocation(string name)
    {
        return await ExecuteInDb(async dbContext =>
        {
            var address = Address.CreateWithFlat("RF", "Moscow", "testStreet", "12", 3).Value;
            var location = Location.Create(
               Name.Create(name).Value,
               TimeZone.Create("test/Moscow").Value,
               address);

            dbContext.Locations.Add(location.Value);
            await dbContext.SaveChangesAsync();

            return location.Value.Id;
        });
    }

    private async Task<T> ExecuteHandler<T>(Func<CreateDepartmentHandler, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();

        var sut = scope.ServiceProvider.GetRequiredService<CreateDepartmentHandler>();

        return await action(sut);
    }
}
