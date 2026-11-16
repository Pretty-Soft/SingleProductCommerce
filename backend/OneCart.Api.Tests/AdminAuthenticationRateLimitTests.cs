using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OneCart.Api.Admin;
using Xunit;

namespace OneCart.Api.Tests;

public sealed class AdminAuthenticationRateLimitTests
{
    [Fact]
    public async Task Anonymous_admin_sign_in_routes_use_the_authentication_rate_limit()
    {
        var builder = WebApplication.CreateBuilder();
        await using var app = builder.Build();
        app.MapPost("/api/v1/admin/login", () => Results.Ok())
            .RequireAdminAuthenticationRateLimit();
        app.MapPost("/api/v1/admin/two-factor/login", () => Results.Ok())
            .RequireAdminAuthenticationRateLimit();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToDictionary(endpoint => endpoint.RoutePattern.RawText!);

        Assert.Equal("AdminAuthentication", endpoints["/api/v1/admin/login"]
            .Metadata.GetMetadata<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()?.PolicyName);
        Assert.Equal("AdminAuthentication", endpoints["/api/v1/admin/two-factor/login"]
            .Metadata.GetMetadata<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()?.PolicyName);
    }
}
