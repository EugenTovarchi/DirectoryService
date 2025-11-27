namespace DirectoryService.Contracts.Requests.Locations;

public  record CreateLocationRequest(string LocationName, string TimeZone, LocationAddressRequest LocationAddress);

