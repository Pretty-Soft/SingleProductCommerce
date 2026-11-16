using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneCart.Api.Admin;
using OneCart.Api.Data;
using Xunit;

namespace OneCart.Api.Tests;

public sealed class InitialSystemAdminSetupTests
{
    [Fact]
    public async Task Creates_only_one_initial_system_admin_and_audit_record()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<StoreDbContext>(options => options.UseSqlite(connection));
        services.AddIdentityCore<AdminUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
        })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<StoreDbContext>()
            .AddDefaultTokenProviders();
        services.AddScoped<InitialSystemAdminSetupService>();

        await using var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
            await database.Database.EnsureCreatedAsync();
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var setup = scope.ServiceProvider.GetRequiredService<InitialSystemAdminSetupService>();
            Assert.True(await setup.IsAvailableAsync(CancellationToken.None));
            var created = await setup.CreateAsync("owner@example.com", "OneCart-Setup1!A", CancellationToken.None);

            Assert.True(created.Succeeded);
            Assert.False(created.AlreadyComplete);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var setup = scope.ServiceProvider.GetRequiredService<InitialSystemAdminSetupService>();
            var result = await setup.CreateAsync("another@example.com", "OneCart-Setup1!A", CancellationToken.None);

            Assert.True(result.AlreadyComplete);
            Assert.False(await setup.IsAvailableAsync(CancellationToken.None));
            var database = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
            Assert.Equal(1, await database.AdminAuditEvents.CountAsync());
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AdminUser>>();
            Assert.Single(await userManager.GetUsersInRoleAsync(AdminRoles.SystemAdmin));
        }
    }
}
