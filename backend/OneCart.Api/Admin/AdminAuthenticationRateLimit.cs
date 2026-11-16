using Microsoft.AspNetCore.Builder;

namespace OneCart.Api.Admin;

public static class AdminAuthenticationRateLimit
{
    public const string PolicyName = "AdminAuthentication";

    public static RouteHandlerBuilder RequireAdminAuthenticationRateLimit(this RouteHandlerBuilder endpoint) =>
        endpoint.RequireRateLimiting(PolicyName);
}
