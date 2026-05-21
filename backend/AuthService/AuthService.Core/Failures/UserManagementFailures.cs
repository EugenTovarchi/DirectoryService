using SharedService.SharedKernel;

namespace AuthService.Core.Failures;

public static class UserManagementFailures
{
    public static Failure InvalidCompanyContext()
    {
        return Error.Validation(
            "company.context.is.invalid",
            "User cannot invite users to another company").ToFailure();
    }

    public static Failure InvalidCompanyContextForList()
    {
        return Error.Validation(
            "company.context.is.invalid",
            "User list requires a valid company context").ToFailure();
    }

    public static Failure InvalidRole()
    {
        return Error.Validation(
            "role.is.invalid",
            "Role is invalid").ToFailure();
    }

    public static Failure UserCreationFailed()
    {
        return Error.Validation(
            "user.creation.failed",
            "User creation failed",
            "user").ToFailure();
    }

    public static Failure RoleAssignmentFailed()
    {
        return Error.Validation(
            "role.assignment.failed",
            "Role assignment failed",
            "role").ToFailure();
    }

    public static Failure SelfDeactivationIsInvalid()
    {
        return Error.Validation(
            "self.deactivation.is.invalid",
            "User cannot deactivate themselves").ToFailure();
    }

    public static Failure UserStatusChangeFailed()
    {
        return Error.Validation(
            "user.status.change.failed",
            "User status change failed",
            "isActive").ToFailure();
    }
}
