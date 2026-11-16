using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OneCart.Api.Admin;
using OneCart.Api.Catalog;
using OneCart.Api.Data;
using OneCart.Api.Orders;
using Xunit;

namespace OneCart.Persistence.IntegrationTests;

public sealed class PostgreSqlFullBackupRestoreIntegrationTests
{
    private static readonly byte[] PngFixture = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0];

    [Fact]
    public async Task PostgreSql_database_media_and_data_protection_keys_restore_together()
    {
        var configuredConnection = Environment.GetEnvironmentVariable("ONECART_POSTGRES_TEST_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(configuredConnection),
            "Set ONECART_POSTGRES_TEST_CONNECTION to a PostgreSQL login allowed to create and drop databases.");

        var baseConnection = new NpgsqlConnectionStringBuilder(configuredConnection)
        {
            Database = "postgres",
            Pooling = false
        };
        var sourceDatabaseName = "onecart_backup_" + Guid.NewGuid().ToString("N");
        var restoredDatabaseName = "onecart_restore_" + Guid.NewGuid().ToString("N");
        var temporaryRoot = Path.Combine(Path.GetTempPath(), "onecart-recovery-" + Guid.NewGuid().ToString("N"));
        var backupRoot = Path.Combine(temporaryRoot, "backup");
        var sourceMedia = Path.Combine(temporaryRoot, "source-media");
        var restoredMedia = Path.Combine(temporaryRoot, "restored-media");
        var sourceKeys = Path.Combine(temporaryRoot, "source-keys");
        var restoredKeys = Path.Combine(temporaryRoot, "restored-keys");
        var dumpPath = Path.Combine(backupRoot, "onecart.dump");
        var mediaArchive = Path.Combine(backupRoot, "product-media.tar");
        var keysArchive = Path.Combine(backupRoot, "data-protection-keys.tar");

        Directory.CreateDirectory(backupRoot);
        await CreateDatabaseAsync(baseConnection, sourceDatabaseName);
        try
        {
            var sourceConnection = new NpgsqlConnectionStringBuilder(baseConnection.ConnectionString) { Database = sourceDatabaseName };
            var sourceOptions = CreateOptions(sourceConnection.ConnectionString);
            Guid imageId;
            string protectedRecoveryMarker;
            string recoveryAdminPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(36));
            string expectedProductName = "Recovery rehearsal product";

            await using (var source = new StoreDbContext(sourceOptions))
            {
                await source.Database.MigrateAsync();
                var productRequest = new AdminProductRequest(
                    expectedProductName,
                    "recovery-rehearsal-product",
                    "Product used to verify a paired database and filesystem recovery.",
                    true,
                    [new AdminProductVariantRequest(null, "RECOVERY-ONE", "Standard", 125m, "BDT", 4, true)],
                    [] ) { CurrencyCode = "BDT" };
                var saved = await new AdminCatalogService(source).SaveProductAsync(
                    productRequest,
                    Guid.NewGuid(),
                    "backup-test@onecart.local",
                    CancellationToken.None);
                Assert.Null(saved.Errors);
                var variant = Assert.Single(saved.Product!.Variants);

                var keyDirectory = new DirectoryInfo(sourceKeys);
                var dataProtection = DataProtectionProvider.Create(keyDirectory);
                var recoveryProtector = dataProtection.CreateProtector("OneCart.BackupRestore.RecoveryCheck.v1");
                protectedRecoveryMarker = recoveryProtector.Protect("database-media-keys-restored");

                var cartProtector = dataProtection.CreateProtector("OneCart.CartQuote.v1");
                var quoteService = new CartQuoteService(source, cartProtector);
                var quote = await quoteService.CreateAsync(new CartQuoteRequest(variant.Id, []), CancellationToken.None);
                Assert.NotNull(quote.Quote);
                var placed = await new CheckoutService(source, quoteService).PlaceOrderAsync(
                    new OrderPlacementRequest(
                        "Recovery Customer",
                        "+8801712345678",
                        "Recovery rehearsal delivery address",
                        [new OrderLineRequest(quote.Quote!.Token, 1)]),
                    CancellationToken.None);
                Assert.NotNull(placed.Order);
                Assert.Equal("Pending", placed.Order!.Status);

                var admin = new AdminUser
                {
                    Id = Guid.NewGuid(),
                    UserName = "recovery-admin@onecart.local",
                    NormalizedUserName = "RECOVERY-ADMIN@ONECART.LOCAL",
                    Email = "recovery-admin@onecart.local",
                    NormalizedEmail = "RECOVERY-ADMIN@ONECART.LOCAL",
                    EmailConfirmed = true,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    ConcurrencyStamp = Guid.NewGuid().ToString("N"),
                    LockoutEnabled = true
                };
                admin.PasswordHash = new PasswordHasher<AdminUser>().HashPassword(admin, recoveryAdminPassword);
                var systemAdminRole = new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = AdminRoles.SystemAdmin,
                    NormalizedName = AdminRoles.SystemAdmin.ToUpperInvariant(),
                    ConcurrencyStamp = Guid.NewGuid().ToString("N")
                };
                source.Users.Add(admin);
                source.Roles.Add(systemAdminRole);
                source.UserRoles.Add(new IdentityUserRole<Guid> { UserId = admin.Id, RoleId = systemAdminRole.Id });
                await source.SaveChangesAsync();

                var media = new ProductMediaService(source, new LocalProductImageStorage(sourceMedia));
                var upload = await media.UploadAsync(
                    new MemoryStream(PngFixture),
                    PngFixture.Length,
                    "product",
                    "Recovery rehearsal product image",
                    admin.Id,
                    admin.Email!,
                    CancellationToken.None);
                Assert.Null(upload.Error);
                imageId = Assert.IsType<ProductImageResponse>(upload.Image).Id;
            }

            await using (var connection = new NpgsqlConnection(sourceConnectionString(sourceDatabaseName)))
            {
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand("SELECT COUNT(*) FROM store_orders", connection);
                Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
            }

            RunPostgreSqlTool("pg_dump", baseConnection, ["--format=custom", $"--file={dumpPath}", $"--dbname={sourceDatabaseName}"]);
            TarFile.CreateFromDirectory(sourceMedia, mediaArchive, includeBaseDirectory: false);
            TarFile.CreateFromDirectory(sourceKeys, keysArchive, includeBaseDirectory: false);

            await CreateDatabaseAsync(baseConnection, restoredDatabaseName);
            try
            {
                RunPostgreSqlTool("pg_restore", baseConnection,
                    ["--no-owner", "--no-privileges", "--exit-on-error", $"--dbname={restoredDatabaseName}", dumpPath]);
                Directory.CreateDirectory(restoredMedia);
                Directory.CreateDirectory(restoredKeys);
                TarFile.ExtractToDirectory(mediaArchive, restoredMedia, overwriteFiles: false);
                TarFile.ExtractToDirectory(keysArchive, restoredKeys, overwriteFiles: false);

                var restoredConnection = new NpgsqlConnectionStringBuilder(baseConnection.ConnectionString) { Database = restoredDatabaseName };
                var restoredOptions = CreateOptions(restoredConnection.ConnectionString);
                await using var restored = new StoreDbContext(restoredOptions);
                var product = await new StorefrontCatalogService(restored).GetPublishedProductAsync(CancellationToken.None);
                Assert.NotNull(product);
                Assert.Equal(expectedProductName, product.Name);
                var restoredVariantId = Assert.Single(product.Variants).Id;
                Assert.Equal(3, await restored.ProductVariants.AsNoTracking()
                    .Where(variant => variant.Id == restoredVariantId)
                    .Select(variant => variant.StockQuantity)
                    .SingleAsync());
                Assert.Single(product.Images);

                var order = await restored.StoreOrders.AsNoTracking().SingleAsync();
                Assert.Equal("Pending", order.Status);
                Assert.Equal("Recovery Customer", order.CustomerName);
                Assert.Single(await restored.Users.AsNoTracking().ToListAsync());
                var restoredAdmin = await restored.Users.AsNoTracking().SingleAsync();
                Assert.Equal(PasswordVerificationResult.Success,
                    new PasswordHasher<AdminUser>().VerifyHashedPassword(restoredAdmin, restoredAdmin.PasswordHash!, recoveryAdminPassword));
                Assert.Contains(AdminRoles.SystemAdmin, await restored.UserRoles.AsNoTracking()
                    .Join(restored.Roles.AsNoTracking(), userRole => userRole.RoleId, role => role.Id, (_, role) => role.Name!)
                    .ToListAsync());

                var restoredImage = await new ProductMediaService(restored, new LocalProductImageStorage(restoredMedia))
                    .OpenPublishedImageAsync(imageId, CancellationToken.None);
                Assert.NotNull(restoredImage);
                await using (restoredImage!.Content)
                await using (var imageCopy = new MemoryStream())
                {
                    await restoredImage.Content.CopyToAsync(imageCopy);
                    Assert.Equal(PngFixture, imageCopy.ToArray());
                }

                var restoredProvider = DataProtectionProvider.Create(new DirectoryInfo(restoredKeys));
                Assert.Equal("database-media-keys-restored",
                    restoredProvider.CreateProtector("OneCart.BackupRestore.RecoveryCheck.v1").Unprotect(protectedRecoveryMarker));
            }
            finally
            {
                NpgsqlConnection.ClearAllPools();
                await DropDatabaseAsync(baseConnection, restoredDatabaseName);
            }
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await DropDatabaseAsync(baseConnection, sourceDatabaseName);
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    private static string sourceConnectionString(string databaseName) =>
        new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ONECART_POSTGRES_TEST_CONNECTION"))
        {
            Database = databaseName,
            Pooling = false
        }.ConnectionString;

    private static DbContextOptions<StoreDbContext> CreateOptions(string connectionString)
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>();
        StoreDatabaseProvider.Configure(options, StoreDatabaseProvider.PostgreSql, connectionString);
        return options.Options;
    }

    private static async Task CreateDatabaseAsync(NpgsqlConnectionStringBuilder baseConnection, string databaseName)
    {
        await using var connection = new NpgsqlConnection(baseConnection.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(NpgsqlConnectionStringBuilder baseConnection, string databaseName)
    {
        await using var connection = new NpgsqlConnection(baseConnection.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static void RunPostgreSqlTool(string tool, NpgsqlConnectionStringBuilder connection, IReadOnlyList<string> arguments)
    {
        var toolsDirectory = Environment.GetEnvironmentVariable("ONECART_PG_TOOLS_DIRECTORY");
        var executable = string.IsNullOrWhiteSpace(toolsDirectory)
            ? tool
            : Path.Combine(toolsDirectory, OperatingSystem.IsWindows() ? tool + ".exe" : tool);
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        startInfo.Environment["PGHOST"] = connection.Host;
        startInfo.Environment["PGPORT"] = connection.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        startInfo.Environment["PGUSER"] = connection.Username;
        startInfo.Environment["PGPASSWORD"] = connection.Password;
        startInfo.Environment["PGAPPNAME"] = "OneCart backup restore integration test";
        if (!string.IsNullOrWhiteSpace(connection.SslMode.ToString()))
            startInfo.Environment["PGSSLMODE"] = connection.SslMode.ToString().ToLowerInvariant();

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start PostgreSQL tool '{tool}'.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"PostgreSQL tool '{tool}' failed with exit code {process.ExitCode}. {standardOutput} {standardError}");
    }
}
