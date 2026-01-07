using AutoFixture;
using DirectoryService.Application.Commands.Departments.UpdateDepartmentLocations;
using DirectoryService.Contracts.Requests.Departments;

namespace DirectoryService.IntegrationTests;

public static class FixtureExtensions
{
    public static UpdateDepartmentLocationsCommand SeedValidDepartmentWithExistLocations(
         this IFixture fixture,
         Guid departmentId,
         int numberOfLocations)
    {
        var locationIds = fixture.CreateMany<Guid>(numberOfLocations).ToList();
        var request = new UpdateDepartmentLocationsRequest(locationIds);

        return new UpdateDepartmentLocationsCommand(departmentId, request);
    }
}
