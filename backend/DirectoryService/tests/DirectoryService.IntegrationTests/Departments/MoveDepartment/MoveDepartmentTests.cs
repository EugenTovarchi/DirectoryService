using DirectoryService.Application.Commands.Departments.MoveDepartment;
using DirectoryService.Contracts.Requests.Departments;
using DirectoryService.Domain.Entities;
using DirectoryService.SharedKernel.ValueObjects;
using DirectoryService.SharedKernel.ValueObjects.Ids;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DirectoryService.IntegrationTests.Departments.MoveDepartment;

public class MoveDepartmentTests : DirectoryBaseTests
{
    public MoveDepartmentTests(DirectoryTestWebFactory factory) : base(factory) { }

    [Fact]
    public async Task Move_parent_with_children_to_new_parent_should_update_all_children()
    {
        // Arrange
        var rootId = await CreateRootTestDepartment("Root department", "root");
        var parentId = await CreateRootTestDepartment("Parent department", "parent");
        var childId = await CreateChildTestDepartment(parentId, "Child department", "child");
        var grandChildId = await CreateChildTestDepartment(childId, "GrandChild department", "grandchild");

        // Act
        var result = await ExecuteHandler((_sut) =>
        {
            var request = new MoveDepartmentRequest(rootId);
            var command = new MoveDepartmentCommand(parentId, request);

           return  _sut.Handle(command, CancellationToken.None);
        });

        // Assert
        await ExecuteInDb(async dbContext =>
        {
            result.IsSuccess.Should().BeTrue();

            var parent = await dbContext.Departments.FirstAsync(p => p.Id == parentId, CancellationToken.None);
            parent.Path.Value.Should().Be("root.parent");
            parent.Depth.Should().Be(1);

            var child = await dbContext.Departments.FirstAsync(ch => ch.Id == childId, CancellationToken.None);
            child.Path.Value.Should().Be("root.parent.child");
            child.Depth.Should().Be(2);

            var grandChild = await dbContext.Departments.FirstAsync(gc => gc.Id == grandChildId, CancellationToken.None);
            grandChild.Path.Value.Should().Be("root.parent.child.grandchild");
            grandChild.Depth.Should().Be(3);
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

            var parent = await dbContext.Departments.FirstAsync(d => d.Id == DepartmentId.Create(parentId), CancellationToken.None);

            var child = Department.CreateChild(departmentName.Value, identifier.Value, parent).Value;

            await dbContext.AddAsync(child, CancellationToken.None);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            return child.Id.Value;
        });
           
    }

    private async Task<T> ExecuteHandler<T>(Func<MoveDepartmentHandler, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();

        var _sut = scope.ServiceProvider.GetRequiredService<MoveDepartmentHandler>();

        return await action(_sut);
    }
}
