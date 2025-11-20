using CSharpFunctionalExtensions;

namespace DirectoryService.SharedKernel.ValueObjects;

public record Address
{
    private const int MAX_LENGTH = 50;
    public string City { get; }
    public string Street { get; }
    public int House { get; }
    public int? Flat { get; }


    private Address(string city, string street, int house, int? flat = null)
    {
        City = city.Trim();
        Street = street.Trim();
        House = house;
        Flat = flat;
    }

    public static Result<Address, Error> Create(
        string city,
        string street,
        int house)
    {
        if (string.IsNullOrWhiteSpace(city))
            return Errors.General.ValueIsEmptyOrWhiteSpace("city");

        if (city.Length > MAX_LENGTH)
            return Errors.General.ValueIsRequired("city");

        if (string.IsNullOrWhiteSpace(street))
            return Errors.General.ValueIsEmptyOrWhiteSpace("street");

        if (street.Length > MAX_LENGTH)
            return Errors.General.ValueIsRequired("street");

        if (house <= 0)
            return Errors.General.ValueMustBePositive("house");

        return new Address(
            city.Trim(),
            street.Trim(),
            house);
    }
    public static Result<Address, Error> CreateWithFlat(string city, string street, int house, int flat)
    {
        var addressResult = Create(city, street, house);
        if (addressResult.IsFailure)
            return addressResult;

        if (flat <= 0)
            return Errors.General.ValueMustBePositive("flat");

        return new Address(city.Trim(), street.Trim(), house, flat);
    }

    // Для EF Core
    private Address()
    {
        City = string.Empty;
        Street = string.Empty;
    }
}

