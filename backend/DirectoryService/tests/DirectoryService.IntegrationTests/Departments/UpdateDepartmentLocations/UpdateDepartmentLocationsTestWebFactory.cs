using DirectoryService.Application.Commands.Departments.UpdateDepartmentLocations;
using DirectoryService.Core.Abstractions;
using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DirectoryService.IntegrationTests.Departments.UpdateDepartmentLocations;

public class UpdateDepartmentLocationsTestWebFactory : DirectoryTestWebFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddScoped<ICommandHandler<Guid, UpdateDepartmentLocationsCommand>, UpdateDepartmentLocationsHandler>();
            services.AddScoped<IValidator<UpdateDepartmentLocationsCommand>, UpdateDepartmentLocationsValidator>();
        });
    }
}

