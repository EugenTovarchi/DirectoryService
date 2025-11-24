namespace DirectoryService.Contracts.Requests.Locations;

public record LocationAddressRequest(
    string City,
    string Street,
    int House,
    int? Flat = null);
