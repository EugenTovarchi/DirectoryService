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

    public static Failure InvalidInviteToken()
    {
        return Error.Validation(
            "invite.token.is.invalid",
            "Invite token is invalid").ToFailure();
    }

    public static Failure UserCreationFailed()
    {
        return Error.Validation(
            "user.creation.failed",
            "User creation failed",
            "user").ToFailure();
    }

    public static Failure PasswordAssignmentFailed()
    {
        return Error.Validation(
            "password.assignment.failed",
            "Password assignment failed",
            "password").ToFailure();
    }

    public static Failure InvalidPasswordResetToken()
    {
        return Error.Validation(
            "password.reset.token.is.invalid",
            "Password reset token is invalid").ToFailure();
    }

    public static Failure PasswordResetTokenCreationFailed()
    {
        return Error.Validation(
            "password.reset.token.creation.failed",
            "Password reset token creation failed").ToFailure();
    }

    public static Failure PasswordResetFailed()
    {
        return Error.Validation(
            "password.reset.failed",
            "Password reset failed",
            "password").ToFailure();
    }

    public static Failure RoleAssignmentFailed()
    {
        return Error.Validation(
            "role.assignment.failed",
            "Role assignment failed",
            "role").ToFailure();
    }

    public static Failure InviteTokenCreationFailed()
    {
        return Error.Validation(
            "invite.token.creation.failed",
            "Invite token creation failed").ToFailure();
    }

    public static Failure InviteResendIsInvalid()
    {
        return Error.Validation(
            "invite.resend.is.invalid",
            "Invite can be resent only for inactive users without password").ToFailure();
    }

    public static Failure RoleChangeFailed()
    {
        return Error.Validation(
            "role.change.failed",
            "Role change failed",
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

    public static Failure SelfRoleChangeIsInvalid()
    {
        return Error.Validation(
            "self.role.change.is.invalid",
            "User cannot change their own role").ToFailure();
    }

    public static Failure SystemAdminRoleAssignmentIsInvalid()
    {
        return Error.Validation(
            "system.admin.role.assignment.is.invalid",
            "Only system administrators can assign the SystemAdmin role",
            "role").ToFailure();
    }

    public static Failure SelfSessionRevocationIsInvalid()
    {
        return Error.Validation(
            "self.session.revocation.is.invalid",
            "User cannot revoke their own sessions through the admin endpoint").ToFailure();
    }

    public static Failure SelfSessionReadIsInvalid()
    {
        return Error.Validation(
            "self.session.read.is.invalid",
            "User cannot read their own sessions through the admin endpoint").ToFailure();
    }
}
