using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace OneCart.Api.Admin;

public static class PasswordResetLink
{
    public static Uri? GetDestination(IConfiguration configuration, bool development, string? requestOrigin = null)
    {
        var configured = configuration["Email:PasswordResetUrl"];
        var origins = CorsOriginConfiguration.GetAllowedOrigins(configuration, development);
        if (string.IsNullOrWhiteSpace(configured)
            && origins.Contains(requestOrigin, StringComparer.OrdinalIgnoreCase))
            configured = requestOrigin + "/admin/reset-password";

        if (!Uri.TryCreate(configured, UriKind.Absolute, out var url)
            || url.AbsolutePath != "/admin/reset-password"
            || url.UserInfo.Length > 0 || url.Query.Length > 0 || url.Fragment.Length > 0)
            return null;

        if (url.Scheme != Uri.UriSchemeHttps && !(development && url.IsLoopback && url.Scheme == Uri.UriSchemeHttp))
            return null;

        return CorsOriginConfiguration.GetAllowedOrigins(configuration, development)
            .Contains(url.GetLeftPart(UriPartial.Authority), StringComparer.OrdinalIgnoreCase) ? url : null;
    }

    public static Uri Create(Uri destination, string email, string token)
    {
        var values = QueryHelpers.AddQueryString("", new Dictionary<string, string?>
        {
            ["email"] = email,
            ["token"] = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))
        });
        return new UriBuilder(destination) { Fragment = values.TrimStart('?') }.Uri;
    }
}
