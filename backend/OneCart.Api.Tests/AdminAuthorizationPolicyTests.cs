using OneCart.Api.Admin;
using Xunit;

namespace OneCart.Api.Tests;

public sealed class AdminAuthorizationPolicyTests
{
    [Fact]
    public void Catalog_access_is_limited_to_catalog_managers_and_system_admins()
    {
        Assert.Equal(new[] { AdminRoles.SystemAdmin, AdminRoles.CatalogManager }, AdminRoles.CatalogAccess);
    }

    [Fact]
    public void Order_access_is_limited_to_order_managers_and_system_admins()
    {
        Assert.Equal(new[] { AdminRoles.SystemAdmin, AdminRoles.OrderManager }, AdminRoles.OrderAccess);
    }

    [Fact]
    public void Store_access_is_limited_to_store_managers_and_system_admins()
    {
        Assert.Equal(new[] { AdminRoles.SystemAdmin, AdminRoles.StoreManager }, AdminRoles.StoreAccess);
    }

    [Fact]
    public void Media_access_is_limited_to_catalog_store_and_system_admins()
    {
        Assert.Equal(new[] { AdminRoles.SystemAdmin, AdminRoles.CatalogManager, AdminRoles.StoreManager }, AdminRoles.MediaAccess);
    }
}
