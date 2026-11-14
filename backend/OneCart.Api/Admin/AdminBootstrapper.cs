using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OneCart.Api.Data;

namespace OneCart.Api.Admin;

public static class AdminBootstrapper
{
    public static async Task<int> CreateFirstSystemAdminAsync(
        IServiceProvider services,
        string email,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
        await database.Database.MigrateAsync(cancellationToken);
        await AdminRoleSeeder.EnsureSeededAsync(scope.ServiceProvider);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AdminUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        if ((await userManager.GetUsersInRoleAsync(AdminRoles.SystemAdmin)).Count > 0)
        {
            Console.Error.WriteLine("A SystemAdmin already exists. Bootstrap is one-time and has been disabled.");
            return 1;
        }

        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            Console.Error.WriteLine("That email already belongs to an account. No changes were made.");
            return 1;
        }

        if (!await roleManager.RoleExistsAsync(AdminRoles.SystemAdmin))
        {
            Console.Error.WriteLine("The SystemAdmin role could not be initialized.");
            return 1;
        }

        var password = ReadSecret("Create a password (minimum 12 characters): ");
        var confirmation = ReadSecret("Confirm password: ");
        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Passwords did not match. No account was created.");
            return 1;
        }

        var user = new AdminUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            MustChangePassword = false
        };

        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            PrintErrors(createResult);
            return 1;
        }

        var roleResult = await userManager.AddToRoleAsync(user, AdminRoles.SystemAdmin);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            PrintErrors(roleResult);
            return 1;
        }

        database.AdminAuditEvents.Add(new AdminAuditEvent
        {
            Id = Guid.NewGuid(),
            ActorUserId = user.Id,
            ActorEmail = email,
            Action = "system_admin.bootstrap",
            TargetId = user.Id,
            Target = email,
            Details = "First SystemAdmin account created by the local bootstrap command.",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await database.SaveChangesAsync(cancellationToken);

        Console.WriteLine($"Created the first SystemAdmin account for {email}.");
        Console.WriteLine("Sign in through the admin panel using the password you just entered.");
        return 0;
    }

    private static string ReadSecret(string prompt)
    {
        Console.Write(prompt);
        var characters = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return new string(characters.ToArray());
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (characters.Count > 0)
                {
                    characters.RemoveAt(characters.Count - 1);
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                characters.Add(key.KeyChar);
            }
        }
    }

    private static void PrintErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            Console.Error.WriteLine(error.Description);
        }
    }
}
