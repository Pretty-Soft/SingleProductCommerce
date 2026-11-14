using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using OneCart.Api.Admin;
using Xunit;

namespace OneCart.Api.Tests;

public sealed class ApiCookieEventsTests
{
    [Fact]
    public async Task Login_challenge_returns_401_without_redirecting()
    {
        var httpContext = new DefaultHttpContext();
        var redirectContext = CreateRedirectContext(httpContext, "/Account/Login");

        await ApiCookieEvents.RedirectToUnauthorized(redirectContext);

        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        Assert.False(httpContext.Response.Headers.ContainsKey("Location"));
    }

    [Fact]
    public async Task Access_denied_returns_403_without_redirecting()
    {
        var httpContext = new DefaultHttpContext();
        var redirectContext = CreateRedirectContext(httpContext, "/Account/AccessDenied");

        await ApiCookieEvents.RedirectToForbidden(redirectContext);

        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.False(httpContext.Response.Headers.ContainsKey("Location"));
    }

    private static RedirectContext<CookieAuthenticationOptions> CreateRedirectContext(
        HttpContext httpContext,
        string redirectUri)
    {
        var scheme = new AuthenticationScheme(
            "Identity.Application",
            "Identity.Application",
            typeof(CookieAuthenticationHandler));
        return new RedirectContext<CookieAuthenticationOptions>(
            httpContext,
            scheme,
            new CookieAuthenticationOptions(),
            new AuthenticationProperties(),
            redirectUri);
    }
}
