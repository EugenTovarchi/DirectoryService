using DirectoryService.Application.Commands.Departments.Create;
using DirectoryService.Core.Abstractions;
using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DirectoryService.IntegrationTests.Departments.Create;

public class CreateDepartmentTestWebFactory : DirectoryTestWebFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddScoped<ICommandHandler<Guid, CreateDepartmentCommand>, CreateDepartmentHandler>();
            services.AddScoped<IValidator<CreateDepartmentCommand>, CreateDepartmentValidator>();
        });
    }
}
