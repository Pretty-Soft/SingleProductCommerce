namespace OneCart.Api.Data;

public static class StoreConnectionStringConfiguration
{
    public static string GetRequired(IConfiguration configuration)
    {
        var configuredFile = configuration["ConnectionStrings:StoreFile"];
        if (!string.IsNullOrWhiteSpace(configuredFile))
        {
            var path = Path.GetFullPath(configuredFile);
            if (!File.Exists(path))
                throw new InvalidOperationException("The configured ConnectionStrings:StoreFile could not be read.");

            var fileConnectionString = File.ReadAllText(path).Trim();
            if (!string.IsNullOrWhiteSpace(fileConnectionString))
                return fileConnectionString;

            throw new InvalidOperationException("The configured ConnectionStrings:StoreFile is empty.");
        }

        return configuration.GetConnectionString("Store")
            ?? throw new InvalidOperationException(
                "Configure ConnectionStrings:Store or ConnectionStrings:StoreFile for the selected Database:Provider.");
    }
}
