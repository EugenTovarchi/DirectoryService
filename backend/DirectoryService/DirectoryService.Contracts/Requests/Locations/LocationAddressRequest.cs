namespace DirectoryService.Contracts.Requests.Locations;

public record LocationAddressRequest(
    string Country,
    string City,
    string Street,
    string House,
    int? Flat = null);
