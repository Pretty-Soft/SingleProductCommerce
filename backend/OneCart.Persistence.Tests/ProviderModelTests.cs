using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using OneCart.Api.Catalog;
using OneCart.Api.Data;
using Xunit;

namespace OneCart.Persistence.Tests;

public sealed class ProviderModelTests
{
    [Fact]
    public void PostgreSql_remains_the_default_provider()
    {
        Assert.Equal(StoreDatabaseProvider.PostgreSql, StoreDatabaseProvider.GetProviderName(null));
    }

    [Fact]
    public void SqlServer_uses_its_own_filtered_index_and_check_constraint_syntax()
    {
        using var context = CreateContext(StoreDatabaseProvider.SqlServer);
        var product = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(StoreProduct))!;
        var publishedIndex = product.GetIndexes().Single(index =>
            index.Properties.Any(property => property.Name == nameof(StoreProduct.IsPublished)));

        Assert.Equal("[IsPublished] = 1", publishedIndex.GetFilter());
        Assert.Contains(product.GetCheckConstraints(), constraint =>
            constraint.Sql == "LEN([Name]) > 0");
    }

    [Fact]
    public void PostgreSql_uses_its_own_filtered_index_and_check_constraint_syntax()
    {
        using var context = CreateContext(StoreDatabaseProvider.PostgreSql);
        var product = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(StoreProduct))!;
        var publishedIndex = product.GetIndexes().Single(index =>
            index.Properties.Any(property => property.Name == nameof(StoreProduct.IsPublished)));

        Assert.Equal("\"IsPublished\" = TRUE", publishedIndex.GetFilter());
        Assert.Contains(product.GetCheckConstraints(), constraint =>
            constraint.Sql == "length(\"Name\") > 0");
    }

    private static StoreDbContext CreateContext(string provider)
    {
        var options = new DbContextOptionsBuilder<StoreDbContext>();
        StoreDatabaseProvider.Configure(options, provider, "local-test-connection");
        return new StoreDbContext(options.Options);
    }
}
