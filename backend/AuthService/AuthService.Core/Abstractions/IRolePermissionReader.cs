namespace AuthService.Core.Abstractions;

public interface IRolePermissionReader
{
    Task<IReadOnlyCollection<string>> GetPermissionCodesAsync(
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken);
}
