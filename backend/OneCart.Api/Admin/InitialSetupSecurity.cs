using System.Security.Cryptography;
using System.Text;

namespace OneCart.Api.Admin;

public static class InitialSetupSecurity
{
    public static bool HasValidToken(IConfiguration configuration, string? suppliedToken)
    {
        if (string.IsNullOrWhiteSpace(suppliedToken) || suppliedToken.Length > 512)
            return false;

        var configuredToken = ReadConfiguredToken(configuration);
        if (configuredToken is null || Encoding.UTF8.GetByteCount(configuredToken) < 32)
            return false;

        var expected = Encoding.UTF8.GetBytes(configuredToken);
        var supplied = Encoding.UTF8.GetBytes(suppliedToken);
        return expected.Length == supplied.Length && CryptographicOperations.FixedTimeEquals(expected, supplied);
    }

    public static bool IsAllowedOrigin(HttpRequest request, IConfiguration configuration, bool isDevelopment)
    {
        var origin = request.Headers.Origin.ToString();
        if (string.IsNullOrWhiteSpace(origin))
            return false;

        return CorsOriginConfiguration.GetAllowedOrigins(configuration, isDevelopment)
            .Contains(origin, StringComparer.OrdinalIgnoreCase);
    }

    private static string? ReadConfiguredToken(IConfiguration configuration)
    {
        var tokenFile = configuration["Setup:BootstrapTokenFile"];
        if (!string.IsNullOrWhiteSpace(tokenFile))
        {
            var fullPath = Path.GetFullPath(tokenFile);
            return File.Exists(fullPath) ? File.ReadAllText(fullPath).Trim() : null;
        }

        return configuration["Setup:BootstrapToken"];
    }
}
