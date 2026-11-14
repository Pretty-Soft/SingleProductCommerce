using Microsoft.AspNetCore.Identity;

namespace OneCart.Api.Admin;

public static class AdminRoles
{
    public const string SystemAdmin = "SystemAdmin";
    public const string CatalogManager = "CatalogManager";
    public const string OrderManager = "OrderManager";
    public const string StoreManager = "StoreManager";

    public static readonly IReadOnlyList<string> BuiltIn =
    [
        SystemAdmin,
        CatalogManager,
        OrderManager,
        StoreManager
    ];
}

public static class AdminRoleSeeder
{
    public static async Task EnsureSeededAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var roleName in AdminRoles.BuiltIn)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                if (!result.Succeeded)
                {
                    var errors = string.Join("; ", result.Errors.Select(error => error.Description));
                    throw new InvalidOperationException($"Could not create the '{roleName}' role: {errors}");
                }
            }
        }
    }
}
