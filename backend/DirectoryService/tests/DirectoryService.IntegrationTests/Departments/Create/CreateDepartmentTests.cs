using AutoFixture;
using DirectoryService.Application.Commands.Departments.Create;
using DirectoryService.Application.Database;
using DirectoryService.Contracts.Requests.Departments;
using DirectoryService.Core.Abstractions;
using DirectoryService.Domain.Entities;
using DirectoryService.Infrastructure.Postgres.DbContexts;
using DirectoryService.SharedKernel.ValueObjects;
using DirectoryService.SharedKernel.ValueObjects.Ids;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DirectoryService.IntegrationTests.Departments.Create;

public class CreateDepartmentTests : IClassFixture<CreateDepartmentTestWebFactory>, IAsyncLifetime
{
    private readonly CreateDepartmentTestWebFactory _factory;
    private readonly IServiceScope _scope;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly DirectoryServiceDbContext _dbContext;
    private readonly ICommandHandler<Guid, CreateDepartmentCommand> _sut;
    private readonly IFixture _fixture;

    public CreateDepartmentTests(CreateDepartmentTestWebFactory factory)
    {
        _factory = factory;
        _scope = _factory.Services.CreateScope();
        _departmentRepository = _scope.ServiceProvider.GetRequiredService<IDepartmentRepository>();
        _locationRepository = _scope.ServiceProvider.GetRequiredService<ILocationRepository>();
        _dbContext = _scope.ServiceProvider.GetRequiredService<DirectoryServiceDbContext>();

        _sut = _scope.ServiceProvider.GetRequiredService<ICommandHandler<Guid, CreateDepartmentCommand>>();
        _fixture = new Fixture();
    }

    [Fact]
    public async Task CreateDepartment_with_valid_data_should_succeed()
    {
        //Arrange
        var locationId = await CreateLocation("location1");
        var request = new CreateDepartmentRequest("testName", "testIdentifier", [locationId.Value], null);
        var command = new CreateDepartmentCommand(request);

        //Act
        var result = await _sut.Handle(command, CancellationToken.None);

        //Assert
        var department = await _departmentRepository.GetById(result.Value, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Value);
        Assert.Equal(result.Value, department.Value.Id.Value);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateDepartment_with_few_locations_should_succeed()
    {
        //Arrange
        var locationId1 = await CreateLocation("location1");
        var locationId2 = await CreateLocation("location2");

        List<Guid> locations = [locationId1, locationId2];
        List<LocationId> locationsForCheck = [locationId1, locationId2];

        var request = new CreateDepartmentRequest("testName", "testIdentifier", locations, null);
        var command = new CreateDepartmentCommand(request);

        //Act
        var result = await _sut.Handle(command, CancellationToken.None);

        //Assert
        var department = await _departmentRepository.GetById(result.Value, CancellationToken.None);
        var departmentLocationsExist = await _locationRepository.AllLocationsExistAsync(locationsForCheck, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Value);
        Assert.Equal(result.Value, department.Value.Id.Value);
        departmentLocationsExist.IsSuccess.Should().BeTrue();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateDepartment_with_invalid_data_should_false()
    {
        //Arrange
        var locationId = await CreateLocation("location1");
        var request = new CreateDepartmentRequest("", "testIdentifier", [locationId.Value], null);
        var command = new CreateDepartmentCommand(request);

        //Act
        var result = await _sut.Handle(command, CancellationToken.None);

        //Assert
        result.IsSuccess.Should().BeFalse();
        Assert.True(result.IsFailure);
        Assert.NotEmpty(result.Error);

        var departments = await _dbContext.Departments.AnyAsync();
        departments.Should().BeFalse();
    }

    private async Task<LocationId> CreateLocation(string name)
    {

        var address = Address.CreateWithFlat("RF", "moscov", "testStreet", "12", 3).Value;
        var location = Location.Create(
           Name.Create(name).Value,
           SharedKernel.ValueObjects.TimeZone.Create("test/Moscov").Value,
           address);

        _dbContext.Locations.Add(location.Value);
        await _dbContext.SaveChangesAsync();

        return location.Value.Id;
    }

    public async Task DisposeAsync()
    {
        await _factory.ResetDatabaseAsync();
        _scope.Dispose();
    }

    public Task InitializeAsync() => Task.CompletedTask;
}
