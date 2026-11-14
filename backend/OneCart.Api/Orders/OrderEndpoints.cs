using Microsoft.AspNetCore.Identity;
using OneCart.Api.Admin;

namespace OneCart.Api.Orders;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/store/cart/quote", async (
            CartQuoteRequest request,
            CartQuoteService quotes,
            CancellationToken cancellationToken) =>
        {
            var result = await quotes.CreateAsync(request, cancellationToken);
            return result.Error is not null
                ? Results.Problem(result.Error, statusCode: result.StatusCode)
                : Results.Ok(result.Quote);
        }).AllowAnonymous();

        endpoints.MapPost("/api/v1/store/orders", async (
            OrderPlacementRequest request,
            CheckoutService checkout,
            CancellationToken cancellationToken) =>
        {
            var result = await checkout.PlaceOrderAsync(request, cancellationToken);
            if (result.Errors is not null) return Results.ValidationProblem(result.Errors);
            if (result.Error is not null) return Results.Problem(result.Error, statusCode: result.StatusCode);
            return Results.Json(result.Order, statusCode: result.StatusCode);
        }).AllowAnonymous();

        var admin = endpoints.MapGroup("/api/v1/admin/orders");
        admin.MapGet("", async (OrderManagementService orders, CancellationToken cancellationToken) =>
                Results.Ok(await orders.GetRecentOrdersAsync(cancellationToken)))
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.SystemAdmin))
            .AddEndpointFilter(AdminEndpoints.RequireChangedPasswordAsync);

        admin.MapGet("/{orderId:guid}", async (Guid orderId, OrderManagementService orders, CancellationToken cancellationToken) =>
                await orders.GetOrderAsync(orderId, cancellationToken) is { } order ? Results.Ok(order) : Results.NotFound())
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.SystemAdmin))
            .AddEndpointFilter(AdminEndpoints.RequireChangedPasswordAsync);

        admin.MapPut("/{orderId:guid}/status", async (
            Guid orderId,
            UpdateOrderStatusRequest request,
            OrderManagementService orders,
            UserManager<AdminUser> userManager,
            HttpContext context) =>
        {
            if (!Guid.TryParse(userManager.GetUserId(context.User), out var actorId)) return Results.Unauthorized();
            var result = await orders.UpdateStatusAsync(
                orderId,
                request.Status,
                actorId,
                userManager.GetUserName(context.User) ?? string.Empty,
                context.RequestAborted);
            if (result.IsNotFound) return Results.NotFound();
            return result.Error is not null ? Results.BadRequest(new { error = result.Error }) : Results.Ok(result.Order);
        })
            .RequireAuthorization(policy => policy.RequireRole(AdminRoles.SystemAdmin))
            .AddEndpointFilter(AdminEndpoints.ValidateAntiforgeryAsync)
            .AddEndpointFilter(AdminEndpoints.RequireChangedPasswordAsync);

        return endpoints;
    }
}

public sealed record UpdateOrderStatusRequest(string Status);
