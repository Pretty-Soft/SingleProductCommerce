using OneCart.Api.Admin;

namespace OneCart.Api.Orders;

public static class AdminCustomerEndpoints
{
    public static IEndpointRouteBuilder MapAdminCustomerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/admin/customers", async (
            string? search,
            OrderManagementService orders,
            CancellationToken cancellationToken,
            int page = 1,
            int pageSize = 25) =>
                Results.Ok(await orders.SearchCustomerContactsAsync(search, page, pageSize, cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.OrderAccess))
            .AddEndpointFilter(AdminEndpoints.RequireChangedPasswordAsync);

        return endpoints;
    }
}
