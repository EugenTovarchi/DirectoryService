using AuthService.Domain.Identity;
using AuthService.Infrastructure.Postgres.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Postgres;

public class AuthServiceDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public AuthServiceDbContext(DbContextOptions<AuthServiceDbContext> options)
        : base(options)
    {
    }

    public static AuthServiceDbContext Create(string connectionString)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AuthServiceDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.EnableSensitiveDataLogging();

        return new AuthServiceDbContext(optionsBuilder.Options);
    }

    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserInviteToken> UserInviteTokens => Set<UserInviteToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<AuthAuditEvent> AuthAuditEvents => Set<AuthAuditEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureIdentityTableNames(builder);

        builder.ApplyConfiguration(new ApplicationUserConfiguration());
        builder.ApplyConfiguration(new ApplicationRoleConfiguration());
        builder.ApplyConfiguration(new PermissionConfiguration());
        builder.ApplyConfiguration(new RolePermissionConfiguration());
        builder.ApplyConfiguration(new RefreshTokenConfiguration());
        builder.ApplyConfiguration(new UserInviteTokenConfiguration());
        builder.ApplyConfiguration(new PasswordResetTokenConfiguration());
        builder.ApplyConfiguration(new AuthAuditEventConfiguration());
    }

    private static void ConfigureIdentityTableNames(ModelBuilder builder)
    {
        builder.Entity<IdentityUserClaim<Guid>>(claim =>
        {
            claim.ToTable("identity_user_claims");
            claim.Property(value => value.Id).HasColumnName("id");
            claim.Property(value => value.UserId).HasColumnName("user_id");
            claim.Property(value => value.ClaimType).HasColumnName("claim_type");
            claim.Property(value => value.ClaimValue).HasColumnName("claim_value");
        });

        builder.Entity<IdentityUserLogin<Guid>>(login =>
        {
            login.ToTable("identity_user_logins");
            login.Property(value => value.LoginProvider).HasColumnName("login_provider");
            login.Property(value => value.ProviderKey).HasColumnName("provider_key");
            login.Property(value => value.ProviderDisplayName).HasColumnName("provider_display_name");
            login.Property(value => value.UserId).HasColumnName("user_id");
        });

        builder.Entity<IdentityUserToken<Guid>>(token =>
        {
            token.ToTable("identity_user_tokens");
            token.Property(value => value.UserId).HasColumnName("user_id");
            token.Property(value => value.LoginProvider).HasColumnName("login_provider");
            token.Property(value => value.Name).HasColumnName("name");
            token.Property(value => value.Value).HasColumnName("value");
        });

        builder.Entity<IdentityRoleClaim<Guid>>(claim =>
        {
            claim.ToTable("identity_role_claims");
            claim.Property(value => value.Id).HasColumnName("id");
            claim.Property(value => value.RoleId).HasColumnName("role_id");
            claim.Property(value => value.ClaimType).HasColumnName("claim_type");
            claim.Property(value => value.ClaimValue).HasColumnName("claim_value");
        });

        builder.Entity<IdentityUserRole<Guid>>(userRole =>
        {
            userRole.ToTable("identity_user_roles");
            userRole.Property(value => value.UserId).HasColumnName("user_id");
            userRole.Property(value => value.RoleId).HasColumnName("role_id");
        });
    }
}
