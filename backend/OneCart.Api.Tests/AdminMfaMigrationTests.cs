using Microsoft.EntityFrameworkCore;
using OneCart.Api.Data;
using Xunit;

namespace OneCart.Api.Tests;

public sealed class AdminMfaMigrationTests
{
    [Theory]
    [InlineData(StoreDatabaseProvider.PostgreSql, "20261115190000_AdminMfaPolicy")]
    [InlineData(StoreDatabaseProvider.SqlServer, "20261115191000_AdminMfaPolicy")]
    public void ProviderMigrationAssemblyDiscoversAdminMfaPolicy(string provider, string migrationId)
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>();
        StoreDatabaseProvider.Configure(options, provider, "unused migration discovery connection");
        using var database = new StoreDbContext(options.Options);

        Assert.Contains(migrationId, database.Database.GetMigrations());
    }

    [Theory]
    [InlineData(StoreDatabaseProvider.PostgreSql, "20261115192000_ProductTranslations")]
    [InlineData(StoreDatabaseProvider.SqlServer, "20261115192010_ProductTranslations")]
    public void ProviderMigrationAssemblyDiscoversProductTranslations(string provider, string migrationId)
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>();
        StoreDatabaseProvider.Configure(options, provider, "unused migration discovery connection");
        using var database = new StoreDbContext(options.Options);

        Assert.Contains(migrationId, database.Database.GetMigrations());
    }

    [Theory]
    [InlineData(StoreDatabaseProvider.PostgreSql, "20261115193000_StorefrontTheme")]
    [InlineData(StoreDatabaseProvider.SqlServer, "20261115193010_StorefrontTheme")]
    public void ProviderMigrationAssemblyDiscoversStorefrontTheme(string provider, string migrationId)
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>();
        StoreDatabaseProvider.Configure(options, provider, "unused migration discovery connection");
        using var database = new StoreDbContext(options.Options);

        Assert.Contains(migrationId, database.Database.GetMigrations());
    }
}
