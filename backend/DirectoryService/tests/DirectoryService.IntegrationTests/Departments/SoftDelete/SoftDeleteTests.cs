using DirectoryService.Application.Commands.Departments.SoftDelete;
using DirectoryService.Contracts.ValueObjects;
using DirectoryService.Contracts.ValueObjects.Ids;
using DirectoryService.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DirectoryService.IntegrationTests.Departments.SoftDelete;

public class SoftDeleteTests : DirectoryBaseTests
{
    public SoftDeleteTests(DirectoryTestWebFactory factory)
        : base(factory) { }

    [Fact]
    public async Task SoftDelete_root_department_should_mark_deleted_and_update_descendant_paths()
    {
        // Arrange
        var parentId = await CreateRootTestDepartment("Sales department", "sales");
        var childId = await CreateChildTestDepartment(parentId, "Hr department", "human");
        var grandChildId = await CreateChildTestDepartment(childId, "Ops department", "ops");

        // Act
        var result = await ExecuteHandler((sut) =>
        {
            var command = new SoftDeleteCommand(parentId);

            return sut.Handle(command, CancellationToken.None);
        });

        // Assert
        await ExecuteInDb(async dbContext =>
        {
            result.IsSuccess.Should().BeTrue();

            var parent = await dbContext.Departments
                .IgnoreQueryFilters()
                .FirstAsync(d => d.Id == DepartmentId.Create(parentId), CancellationToken.None);
            parent.IsDeleted.Should().BeTrue();
            parent.Path.Value.Should().Be("deleted_sales");

            var child = await dbContext.Departments
                .FirstAsync(d => d.Id == DepartmentId.Create(childId), CancellationToken.None);
            child.Path.Value.Should().Be("deleted_sales.human");
            child.Depth.Should().Be(1);

            var grandChild = await dbContext.Departments
                .FirstAsync(d => d.Id == DepartmentId.Create(grandChildId), CancellationToken.None);
            grandChild.Path.Value.Should().Be("deleted_sales.human.ops");
            grandChild.Depth.Should().Be(2);
        });
    }

    [Fact]
    public async Task SoftDelete_nested_department_should_mark_deleted_segment_and_update_descendant_paths()
    {
        // Arrange
        var rootId = await CreateRootTestDepartment("Company department", "company");
        var parentId = await CreateChildTestDepartment(rootId, "Sales department", "sales");
        var childId = await CreateChildTestDepartment(parentId, "Hr department", "human");

        // Act
        var result = await ExecuteHandler((sut) =>
        {
            var command = new SoftDeleteCommand(parentId);

            return sut.Handle(command, CancellationToken.None);
        });

        // Assert
        await ExecuteInDb(async dbContext =>
        {
            result.IsSuccess.Should().BeTrue();

            var parent = await dbContext.Departments
                .IgnoreQueryFilters()
                .FirstAsync(d => d.Id == DepartmentId.Create(parentId), CancellationToken.None);
            parent.IsDeleted.Should().BeTrue();
            parent.Path.Value.Should().Be("company.deleted_sales");
            parent.Depth.Should().Be(1);

            var child = await dbContext.Departments
                .FirstAsync(d => d.Id == DepartmentId.Create(childId), CancellationToken.None);
            child.Path.Value.Should().Be("company.deleted_sales.human");
            child.Depth.Should().Be(2);
        });
    }

    private async Task<Guid> CreateRootTestDepartment(string name, string depIdentifier)
    {
        return await ExecuteInDb(async dbContext =>
        {
            var departmentName = Name.Create(name);
            var identifier = Identifier.Create(depIdentifier);
            var department = Department.CreateRoot(departmentName.Value, identifier.Value).Value;

            await dbContext.AddAsync(department, CancellationToken.None);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            return department.Id.Value;
        });
    }

    private async Task<Guid> CreateChildTestDepartment(Guid parentId, string name, string depIdentifier)
    {
        return await ExecuteInDb(async dbContext =>
        {
            var departmentName = Name.Create(name);
            var identifier = Identifier.Create(depIdentifier);
            var parent = await dbContext.Departments.FirstAsync(
                d => d.Id == DepartmentId.Create(parentId),
                CancellationToken.None);

            var child = Department.CreateChild(departmentName.Value, identifier.Value, parent).Value;

            await dbContext.AddAsync(child, CancellationToken.None);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            return child.Id.Value;
        });
    }

    private async Task<T> ExecuteHandler<T>(Func<SoftDeleteHandler, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();

        var sut = scope.ServiceProvider.GetRequiredService<SoftDeleteHandler>();

        return await action(sut);
    }
}
