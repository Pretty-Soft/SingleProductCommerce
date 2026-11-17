using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using MySql.Data.MySqlClient;
using OneCart.Api.Admin;
using OneCart.Api.Catalog;
using OneCart.Api.Data;
using OneCart.Api.Orders;
using Xunit;

namespace OneCart.Persistence.IntegrationTests;

public sealed class MySqlInstallationIntegrationTests
{
    [Fact]
    public async Task Wizard_creates_a_fresh_database_and_persists_store_defaults()
    {
        var configured = Environment.GetEnvironmentVariable("ONECART_MYSQL_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(configured), "Set ONECART_MYSQL_TEST_CONNECTION to a MySQL login with database-creation permission.");
        var source = new MySqlConnectionStringBuilder(configured);
        var name = "onecart_wizard_" + Guid.NewGuid().ToString("N");
        var folder = Path.Combine(Path.GetTempPath(), "onecart-wizard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var databaseConnection = new MySqlConnectionStringBuilder(configured) { Database = name, Pooling = false };
        var databaseOptions = new DbContextOptionsBuilder<StoreDbContext>();
        StoreDatabaseProvider.Configure(databaseOptions, StoreDatabaseProvider.MySql, databaseConnection.ConnectionString);
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
                new DatabaseSetupRequest("MySql", source.Server, (int)source.Port, name, source.UserID, source.Password, EncryptConnection: source.SslMode != MySqlSslMode.Disabled),
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

            var owner = await verification.Users.SingleAsync();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AdminUser>>();
            Assert.True(await users.CheckPasswordAsync(owner, request.Password));
            var product = new AdminProductRequest("MySQL test product", "mysql-test-product", "A disposable test product", true,
                [new AdminProductVariantRequest(null, "MYSQL-TEST", "Standard", 19.95m, "BDT", 7, true)])
            {
                CurrencyCode = "BDT"
            };
            var save = await new AdminCatalogService(verification).SaveProductAsync(product, owner.Id, owner.Email!, CancellationToken.None);
            Assert.Null(save.Errors);
            var published = await new StorefrontCatalogService(verification).GetPublishedProductAsync(CancellationToken.None);
            var variant = Assert.Single(Assert.IsType<StorefrontProductResponse>(published).Variants);
            var quotes = new CartQuoteService(verification, new EphemeralDataProtectionProvider().CreateProtector("OneCart.CartQuote.v1"));
            var quote = await quotes.CreateAsync(new CartQuoteRequest(variant.Id, []), CancellationToken.None);
            Assert.NotNull(quote.Quote);
            var placed = await new CheckoutService(verification, quotes).PlaceOrderAsync(
                new OrderPlacementRequest("Test Shopper", "5550100", "Test Address", [new OrderLineRequest(quote.Quote.Token, 2)]), CancellationToken.None);
            Assert.NotNull(placed.Order);
            Assert.Equal(5, await verification.ProductVariants.AsNoTracking().Select(item => item.StockQuantity).SingleAsync());
            var order = await verification.StoreOrders.SingleAsync();
            var cancelled = await new OrderManagementService(verification).UpdateStatusAsync(order.Id, "Cancelled", owner.Id, owner.Email!, CancellationToken.None);
            Assert.Equal("Cancelled", cancelled.Order?.Status);
            Assert.Equal(7, await verification.ProductVariants.AsNoTracking().Select(item => item.StockQuantity).SingleAsync());
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
