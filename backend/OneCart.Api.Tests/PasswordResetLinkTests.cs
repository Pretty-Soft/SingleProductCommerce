using Microsoft.Extensions.Configuration;
using OneCart.Api.Admin;
using Xunit;

namespace OneCart.Api.Tests;

public sealed class PasswordResetLinkTests
{
    [Theory]
    [InlineData("https://shop.example.test/admin/reset-password", false, true)]
    [InlineData("https://other.example.test/admin/reset-password", false, false)]
    [InlineData("https://shop.example.test/admin/reset-password?token=old", false, false)]
    [InlineData("http://127.0.0.1:5175/admin/reset-password", false, false)]
    [InlineData("http://127.0.0.1:5175/admin/reset-password", true, true)]
    public void Accepts_only_the_configured_storefront_reset_page(string url, bool development, bool expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:PasswordResetUrl"] = url,
            ["Cors:AllowedOrigins:0"] = "https://shop.example.test"
        }).Build();
        Assert.Equal(expected, PasswordResetLink.GetDestination(configuration, development) is not null);
    }

    [Fact]
    public void Uses_allowlisted_storefront_when_no_reset_url_is_configured()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = "https://shop.example.test"
        }).Build();
        Assert.Equal("https://shop.example.test/admin/reset-password",
            PasswordResetLink.GetDestination(configuration, false, "https://shop.example.test")?.AbsoluteUri);
        Assert.Null(PasswordResetLink.GetDestination(configuration, false, "https://untrusted.example.test"));
    }

    [Fact]
    public void Keeps_email_and_token_out_of_http_request_url()
    {
        var link = PasswordResetLink.Create(new Uri("https://shop.example.test/admin/reset-password"), "owner+test@example.test", "test-token");
        Assert.Empty(link.Query);
        Assert.Equal("https://shop.example.test/admin/reset-password", link.GetLeftPart(UriPartial.Path));
        var values = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(link.Fragment[1..]);
        Assert.Equal("owner+test@example.test", values["email"].ToString());
        Assert.Equal("dGVzdC10b2tlbg", values["token"].ToString());
    }
}
