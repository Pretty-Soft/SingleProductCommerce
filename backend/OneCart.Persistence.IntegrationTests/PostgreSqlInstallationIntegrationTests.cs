using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Npgsql;
using OneCart.Api.Admin;
using OneCart.Api.Data;
using Xunit;

namespace OneCart.Persistence.IntegrationTests;

public sealed class PostgreSqlInstallationIntegrationTests
{
    [Fact]
    public async Task Wizard_creates_a_fresh_database_and_persists_store_defaults()
    {
        var configured = Environment.GetEnvironmentVariable("ONECART_POSTGRES_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(configured), "Set ONECART_POSTGRES_TEST_CONNECTION to a PostgreSQL login with database-creation permission.");
        var source = new NpgsqlConnectionStringBuilder(configured);
        var name = "onecart_wizard_" + Guid.NewGuid().ToString("N");
        var folder = Path.Combine(Path.GetTempPath(), "onecart-wizard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var databaseConnection = new NpgsqlConnectionStringBuilder(configured) { Database = name, Pooling = false };
        var databaseOptions = new DbContextOptionsBuilder<StoreDbContext>();
        StoreDatabaseProvider.Configure(databaseOptions, StoreDatabaseProvider.PostgreSql, databaseConnection.ConnectionString);
        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build());
            services.AddSingleton<IWebHostEnvironment>(new TestEnvironment(folder));
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddSingleton<InstallationConfigurationStore>();
            services.AddScoped<FirstRunInstallationService>();
            services.AddScoped<InitialSystemAdminSetupService>();
            services.AddDbContext<StoreDbContext>((provider, options) =>
            {
                var selected = provider.GetRequiredService<InstallationConfigurationStore>().Current!;
                StoreDatabaseProvider.Configure(options, selected.Provider, selected.ConnectionString);
            });
            services.AddIdentityCore<AdminUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
            }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<StoreDbContext>().AddDefaultTokenProviders();

            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var wizard = scope.ServiceProvider.GetRequiredService<FirstRunInstallationService>();
            var request = new CompleteInstallationRequest(
                new DatabaseSetupRequest("PostgreSql", source.Host!, source.Port, name, source.Username!, source.Password!),
                "owner@example.test", "OneCart-Setup1!A", "OneCart-Setup1!A", "bn", "BDT");
            var installed = await wizard.InstallAsync(request, CancellationToken.None);
            Assert.True(installed.Succeeded, installed.Error);
            Assert.True((await wizard.InstallAsync(request, CancellationToken.None)).AlreadyComplete);

            var savedFile = File.ReadAllText(Path.Combine(folder, "App_Data", "installation.json"));
            Assert.DoesNotContain(source.Password!, savedFile);
            await using var verification = new StoreDbContext(databaseOptions.Options);
            var settings = await verification.AdminSystemSettings.SingleAsync();
            Assert.Equal("bn", settings.DefaultLanguageCode);
            Assert.Equal("BDT", settings.CurrencyCode);
            Assert.Single(await verification.Users.ToListAsync());
            Assert.Single(await verification.AdminAuditEvents.Where(item => item.Action == "system_admin.bootstrap").ToListAsync());
        }
        finally
        {
            await using var cleanup = new StoreDbContext(databaseOptions.Options);
            try { await cleanup.Database.EnsureDeletedAsync(); }
            catch { /* Keep the original test failure if database cleanup is unavailable. */ }
            if (Directory.Exists(folder) && Path.GetFullPath(folder).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
                Directory.Delete(folder, recursive: true);
        }
    }

    private sealed class TestEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "OneCart.Persistence.IntegrationTests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(root);
        public string WebRootPath { get; set; } = root;
        public IFileProvider WebRootFileProvider { get; set; } = new PhysicalFileProvider(root);
    }
}
