using CSharpFunctionalExtensions;
using DirectoryService.Contracts.ValueObjects;
using DirectoryService.Contracts.ValueObjects.Ids;
using SharedService.SharedKernel;
using TimeZone = DirectoryService.Contracts.ValueObjects.TimeZone;

namespace DirectoryService.Domain.Entities;

public sealed class Location : SoftDeletableEntity<LocationId>
{
    private Location(LocationId id)
        : base(id) { }
    private Location(
        LocationId locationId,
        Name name,
        TimeZone timeZone,
        Address address)
        : base(locationId)
    {
        Name = name;
        TimeZone = timeZone;
        Address = address;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Name Name { get; private set; } = null!;
    public Address Address { get; private set; } = null!;
    public TimeZone TimeZone { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private readonly List<DepartmentLocation> _departmentLocations = [];
    public IReadOnlyCollection<DepartmentLocation> DepartmentLocations => _departmentLocations.ToList();

    public override void Delete()
    {
        base.Delete();
        UpdatedAt = DateTime.UtcNow;
    }

    public override void Restore()
    {
        base.Restore();
        UpdatedAt = DateTime.UtcNow;
    }

    public Result<Location, Error> UpdateName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return Errors.General.ValueIsEmptyOrWhiteSpace("location name");

        if (newName.Length > 100)
            return Errors.General.ValueIsInvalid("newName");

        Name = Name.Create(newName).Value;
        return this;
    }

    public static Result<Location, Error> Create(
        Name name,
        TimeZone timeZone,
        Address address)
    {
        if (name is null)
            return Errors.General.ValueIsInvalid("name");

        if (timeZone is null)
            return Errors.General.ValueIsInvalid("timeZone");

        if (address is null)
            return Errors.General.ValueIsInvalid("address");

        var locationId = LocationId.NewLocationId();
        var location = new Location(locationId, name, timeZone, address);

        return location;
    }

    internal void AddDepartmentLocation(DepartmentLocation departmentLocation)
    {
        if (departmentLocation != null && !_departmentLocations.Contains(departmentLocation))
        {
            _departmentLocations.Add(departmentLocation);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    internal void RemoveDepartmentLocation(DepartmentLocation departmentLocation)
    {
        if(departmentLocation != null && _departmentLocations.Contains(departmentLocation))
        {
            _departmentLocations.Remove(departmentLocation);
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
