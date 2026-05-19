using System.Text.Json.Serialization;
using CSharpFunctionalExtensions;
using SharedService.SharedKernel;

namespace DirectoryService.Contracts.ValueObjects;

public record Address
{
    public const int COUNTRY_MAX_LENGTH = 100;
    public const int CITY_MAX_LENGTH = 100;
    public const int STREET_MAX_LENGTH = 200;
    public const int HOUSE_MAX_LENGTH = 20;

    public string Country { get; }
    public string City { get; }
    public string Street { get; }
    public string House { get; }
    public int? Flat { get; }

    [JsonConstructor]
    private Address(string country, string city, string street, string house, int? flat = null)
    {
        Country = country.Trim();
        City = city.Trim();
        Street = street.Trim();
        House = house.Trim();
        Flat = flat;
    }

    // Для EF Core
    private Address()
    {
        Country = string.Empty;
        City = string.Empty;
        Street = string.Empty;
        House = string.Empty;
    }

    public static Result<Address, Error> Create(
        string country,
        string city,
        string street,
        string house)
    {
        if (string.IsNullOrWhiteSpace(country))
            return Errors.General.ValueIsEmptyOrWhiteSpace("country");

        if (string.IsNullOrWhiteSpace(city))
            return Errors.General.ValueIsEmptyOrWhiteSpace("city");

        if (string.IsNullOrWhiteSpace(street))
            return Errors.General.ValueIsEmptyOrWhiteSpace("street");

        if (string.IsNullOrWhiteSpace(house))
            return Errors.General.ValueIsEmptyOrWhiteSpace("house");

        if (country.Length > COUNTRY_MAX_LENGTH)
            return Errors.General.ValueIsRequired("country");

        if (city.Length > CITY_MAX_LENGTH)
            return Errors.General.ValueIsRequired("city");

        if (street.Length > STREET_MAX_LENGTH)
            return Errors.General.ValueIsRequired("street");

        if (house.Length > HOUSE_MAX_LENGTH)
            return Errors.General.ValueIsRequired("house");

        return new Address(
            country.Trim(),
            city.Trim(),
            street.Trim(),
            house);
    }

    public static Result<Address, Error> CreateWithFlat(string country, string city, string street, string house, int flat)
    {
        var addressResult = Create(country, city, street, house);
        if (addressResult.IsFailure)
            return addressResult;

        if (flat <= 0)
            return Errors.General.ValueMustBePositive("flat");

        return new Address(country.Trim(), city.Trim(), street.Trim(), house.Trim(), flat);
    }
}
