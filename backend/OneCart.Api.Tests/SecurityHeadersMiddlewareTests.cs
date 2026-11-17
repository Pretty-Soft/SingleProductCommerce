using Microsoft.AspNetCore.Http;
using OneCart.Api;
using Xunit;

namespace OneCart.Api.Tests;

public sealed class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task AddsBrowserSecurityHeadersBeforePassingRequestAlong()
    {
        var context = new DefaultHttpContext();
        var nextWasCalled = false;
        var middleware = new SecurityHeadersMiddleware(_ =>
        {
            nextWasCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        Assert.True(nextWasCalled);
        Assert.Equal("nosniff", context.Response.Headers.XContentTypeOptions);
        Assert.Equal("DENY", context.Response.Headers.XFrameOptions);
        Assert.Equal("strict-origin-when-cross-origin", context.Response.Headers["Referrer-Policy"]);
        Assert.Equal("camera=(), geolocation=(), microphone=()", context.Response.Headers["Permissions-Policy"]);
    }
}
