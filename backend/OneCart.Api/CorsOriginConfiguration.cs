using Microsoft.Extensions.Configuration;

namespace OneCart.Api;

public static class CorsOriginConfiguration
{
    private static readonly string[] DevelopmentFrontendOrigins =
    [
        "http://localhost:5173",
        "http://127.0.0.1:5173",
        "http://localhost:5174",
        "http://127.0.0.1:5174"
    ];

    public static string[] GetAllowedOrigins(IConfiguration configuration, bool isDevelopment)
    {
        var configuredOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (!isDevelopment)
        {
            return configuredOrigins;
        }

        return configuredOrigins
            .Concat(DevelopmentFrontendOrigins)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
