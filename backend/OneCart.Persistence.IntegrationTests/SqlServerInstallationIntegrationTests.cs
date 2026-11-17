using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using OneCart.Api.Admin;
using OneCart.Api.Data;
using Xunit;

namespace OneCart.Persistence.IntegrationTests;

public sealed class SqlServerInstallationIntegrationTests
{
    [Fact]
    public async Task Wizard_migrates_a_fresh_database_and_creates_the_only_system_admin()
    {
        var configured = Environment.GetEnvironmentVariable("ONECART_SQLSERVER_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(configured), "Set ONECART_SQLSERVER_TEST_CONNECTION to a disposable SQL Server login with database-creation permission.");
        var source = new SqlConnectionStringBuilder(configured);
        var name = "OneCart_Wizard_" + Guid.NewGuid().ToString("N");
        var folder = Path.Combine(Path.GetTempPath(), "onecart-wizard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var connection = new SqlConnectionStringBuilder(configured) { InitialCatalog = name, Pooling = false };
        var databaseOptions = new DbContextOptionsBuilder<StoreDbContext>();
        StoreDatabaseProvider.Configure(databaseOptions, StoreDatabaseProvider.SqlServer, connection.ConnectionString);
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
                new DatabaseSetupRequest("SqlServer", source.DataSource, null, name, source.UserID, source.Password, true, false),
                "owner@example.test", "OneCart-Setup1!A", "OneCart-Setup1!A", "es", "EUR");
            Assert.True(FirstRunInstallationService.TryBuildDatabase(request.Database, out var selected, out var connectionError), connectionError);
            var maintenance = new SqlConnectionStringBuilder(selected!.ConnectionString) { InitialCatalog = "master" };
            try
            {
                await using var check = new SqlConnection(maintenance.ConnectionString);
                await check.OpenAsync();
            }
            catch (SqlException exception)
            {
                Assert.Fail($"Wizard SQL connection failed with provider error {exception.Number}: {exception.Message}");
            }
            var installed = await wizard.InstallAsync(request, CancellationToken.None);
            Assert.True(installed.Succeeded, installed.Error);
            Assert.True((await wizard.InstallAsync(request, CancellationToken.None)).AlreadyComplete);

            var savedFile = File.ReadAllText(Path.Combine(folder, "App_Data", "installation.json"));
            Assert.DoesNotContain(source.Password, savedFile);
            await using var verification = new StoreDbContext(databaseOptions.Options);
            var settings = await verification.AdminSystemSettings.SingleAsync();
            Assert.Equal("es", settings.DefaultLanguageCode);
            Assert.Equal("EUR", settings.CurrencyCode);
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
