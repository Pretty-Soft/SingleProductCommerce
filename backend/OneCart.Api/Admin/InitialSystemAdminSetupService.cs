using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OneCart.Api.Data;

namespace OneCart.Api.Admin;

public sealed class InitialSystemAdminSetupService(
    StoreDbContext database,
    UserManager<AdminUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager)
{
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await roleManager.RoleExistsAsync(AdminRoles.SystemAdmin))
            return true;

        return (await userManager.GetUsersInRoleAsync(AdminRoles.SystemAdmin)).Count == 0;
    }

    public async Task<InitialSystemAdminSetupResult> CreateAsync(
        string email,
        string password,
        CancellationToken cancellationToken,
        string? defaultLanguageCode = null,
        string? currencyCode = null)
    {
        await EnsureRolesAsync();

        await using var transaction = await database.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        if ((await userManager.GetUsersInRoleAsync(AdminRoles.SystemAdmin)).Count > 0)
            return InitialSystemAdminSetupResult.SetupAlreadyComplete;

        if (await userManager.FindByEmailAsync(email) is not null)
            return InitialSystemAdminSetupResult.Invalid(["An administrator account already uses this email address."]);

        var user = new AdminUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            MustChangePassword = false
        };

        var create = await userManager.CreateAsync(user, password);
        if (!create.Succeeded)
            return InitialSystemAdminSetupResult.Invalid(create.Errors.Select(error => error.Description).ToArray());

        var addRole = await userManager.AddToRoleAsync(user, AdminRoles.SystemAdmin);
        if (!addRole.Succeeded)
            return InitialSystemAdminSetupResult.Invalid(addRole.Errors.Select(error => error.Description).ToArray());

        if (defaultLanguageCode is not null || currencyCode is not null)
        {
            var setting = await database.AdminSystemSettings.SingleOrDefaultAsync(cancellationToken);
            if (setting is null)
            {
                setting = new AdminSystemSetting { Id = 1 };
                database.AdminSystemSettings.Add(setting);
            }
            if (defaultLanguageCode is not null) setting.DefaultLanguageCode = defaultLanguageCode;
            if (currencyCode is not null) setting.CurrencyCode = currencyCode;
        }

        database.AdminAuditEvents.Add(new AdminAuditEvent
        {
            Id = Guid.NewGuid(),
            ActorUserId = user.Id,
            ActorEmail = email,
            Action = "system_admin.bootstrap",
            TargetId = user.Id,
            Target = email,
            Details = "First SystemAdmin account created through the HTTPS setup wizard.",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return InitialSystemAdminSetupResult.Created;
    }

    private async Task EnsureRolesAsync()
    {
        foreach (var roleName in AdminRoles.BuiltIn)
        {
            if (await roleManager.RoleExistsAsync(roleName))
                continue;

            var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            if (!result.Succeeded && !await roleManager.RoleExistsAsync(roleName))
            {
                var errors = string.Join("; ", result.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Could not initialize the '{roleName}' role: {errors}");
            }
        }
    }
}

public sealed record InitialSystemAdminSetupResult(bool Succeeded, bool AlreadyComplete, IReadOnlyList<string> Errors)
{
    public static InitialSystemAdminSetupResult Created { get; } = new(true, false, []);
    public static InitialSystemAdminSetupResult SetupAlreadyComplete { get; } = new(false, true, []);
    public static InitialSystemAdminSetupResult Invalid(IReadOnlyList<string> errors) => new(false, false, errors);
}
