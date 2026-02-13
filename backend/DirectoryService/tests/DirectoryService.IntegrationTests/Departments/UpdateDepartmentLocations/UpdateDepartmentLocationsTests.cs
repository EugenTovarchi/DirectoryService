using DirectoryService.Application.Commands.Departments.UpdateDepartmentLocations;
using DirectoryService.Contracts.Requests.Departments;
using DirectoryService.Contracts.ValueObjects;
using DirectoryService.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedService.Core.Abstractions;
using SharedService.SharedKernel;
using TimeZone = DirectoryService.Contracts.ValueObjects.TimeZone;

namespace DirectoryService.IntegrationTests.Departments.UpdateDepartmentLocations;

public class UpdateDepartmentLocationsTests : DirectoryBaseTests
{
    public UpdateDepartmentLocationsTests(DirectoryTestWebFactory factory)
        : base(factory) { }

    [Fact]
    public async Task Update_DepartmentLocation_should_succeed()
    {
        // Arrange
        var locationId = await CreateLocation("location1");
        var locationId2 = await CreateLocation("location2");
        List<Guid> locations = [locationId, locationId2];

        var departmentId = await CreateTestDepartment();
        var request = new UpdateDepartmentLocationsRequest(locations);
        var command = new UpdateDepartmentLocationsCommand(departmentId, request );

        // Act
        var result = await ExecuteHandler(async (sut) =>
        {
            return await sut.Handle(command, CancellationToken.None);
        });

        // Assert
        await ExecuteInDb(async dbContext =>
        {
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeEmpty();

            var department = await dbContext.Departments
                .Include(d => d.DepartmentLocations)
                .FirstAsync(d => d.Id == result.Value, CancellationToken.None);

            department.DepartmentLocations.Should().HaveCount(2);
            department.DepartmentLocations
                .Select(dl => dl.LocationId.Value)
                .Should()
                .BeEquivalentTo(locations);
        });
    }

    [Fact]
    public async Task Update_DepartmentLocation_should_failed()
    {
        // Arrange
        List<Guid> locations = [];

        var departmentId = await CreateTestDepartment();
        var request = new UpdateDepartmentLocationsRequest(locations);
        var command = new UpdateDepartmentLocationsCommand(departmentId, request );

        // Act
        var result = await ExecuteHandler(async (sut) =>
        {
            return await sut.Handle(command, CancellationToken.None);
        });

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Update_DepartmentLocation_Should_Handle_Duplicate_LocationIds()
    {
        // Arrange
        var location1 = await CreateLocation("location1");

        List<Guid> duplicateLocations = [location1, location1, location1];

        var departmentId = await CreateTestDepartment();
        var request = new UpdateDepartmentLocationsRequest(duplicateLocations);
        var command = new UpdateDepartmentLocationsCommand(departmentId, request);

        // Act
        var result = await ExecuteHandler(async (sut) =>
        {
            return await sut.Handle(command, CancellationToken.None);
        });

        // Assert
        await ExecuteInDb(async dbContext =>
        {
            result.IsSuccess.Should().BeTrue();

            var department = await dbContext.Departments
                .Include(d => d.DepartmentLocations)
                .FirstAsync(d => d.Id == departmentId, CancellationToken.None);

            department.DepartmentLocations.Should().HaveCount(1);
            department.DepartmentLocations.First().LocationId.Value.Should().Be(location1);
        });
    }

    [Fact]
    public async Task Update_DepartmentLocation_With_NonExistent_Department_Should_Fail()
    {
        // Arrange
        var location1 = await CreateLocation("location1");
        var location2 = await CreateLocation("location2");
        List<Guid> locations = [location1, location2];

        var nonExistentDepartmentId = Guid.NewGuid();
        var request = new UpdateDepartmentLocationsRequest(locations);
        var command = new UpdateDepartmentLocationsCommand(nonExistentDepartmentId, request);

        // Act
        var result = await ExecuteHandler(async (sut) =>
        {
            return await sut.Handle(command, CancellationToken.None);
        });

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeEquivalentTo(Errors.General.NotFoundEntity("departmentId").ToFailure());
    }

    [Fact]
    public async Task Update_DepartmentLocation_With_Duplicate_Locations_Should_Remove_Duplicates()
    {
        // Arrange
        var locationId = await CreateLocation("location1");
        var locationId2 = await CreateLocation("location2");

        List<Guid> locationsWithDuplicates = [locationId, locationId2, locationId];

        var departmentId = await CreateTestDepartment();
        var request = new UpdateDepartmentLocationsRequest(locationsWithDuplicates);
        var command = new UpdateDepartmentLocationsCommand(departmentId, request);

        // Act
        var result = await ExecuteHandler(async (sut) =>
        {
            return await sut.Handle(command, CancellationToken.None);
        });

        // Assert
        await ExecuteInDb(async dbContext =>
        {
            result.IsSuccess.Should().BeTrue();

            var department = await dbContext.Departments
                .Include(d => d.DepartmentLocations)
                .FirstAsync(d => d.Id == departmentId, CancellationToken.None);

            department.DepartmentLocations.Should().HaveCount(2);
            department.DepartmentLocations
                .Select(dl => dl.LocationId.Value)
                .Should()
                .OnlyHaveUniqueItems();
        });
    }

    private async Task<Guid> CreateLocation(string name)
    {
        return await ExecuteInDb(async dbContext =>
        {
            var address = Address.CreateWithFlat("RF", "moscow", "testStreet", "12", 3).Value;
            var location = Location.Create(
                Name.Create(name).Value,
                TimeZone.Create("Europe/Moscow").Value,
                address);

            await dbContext.Locations.AddAsync(location.Value, CancellationToken.None);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            return location.Value.Id.Value;
        });
    }

    private async Task<Guid> CreateTestDepartment()
    {
        return await ExecuteInDb(async dbContext =>
        {
            var departmentName = Name.Create("Отдел продаж");
            var identifier = Identifier.Create("sales");
            var department = Department.CreateRoot(departmentName.Value, identifier.Value).Value;

            await dbContext.Departments.AddAsync(department, CancellationToken.None);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            return department.Id.Value;
        });
    }

    private async Task<T> ExecuteHandler<T>(Func<ICommandHandler<Guid, UpdateDepartmentLocationsCommand>, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<Guid, UpdateDepartmentLocationsCommand>>();
        return await action(handler);
    }
}
