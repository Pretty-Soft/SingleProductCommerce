using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using OneCart.Api.Data;

namespace OneCart.Api.Admin;

public sealed record InstallationDatabase(string Provider, string ConnectionString);

public sealed class InstallationConfigurationStore
{
    private readonly IConfiguration configuration;
    private readonly IDataProtector protector;
    private readonly string filePath;
    private InstallationDatabase? installed;
    private volatile bool installing;

    public InstallationConfigurationStore(IConfiguration configuration, IWebHostEnvironment environment,
        IDataProtectionProvider dataProtection)
    {
        this.configuration = configuration;
        protector = dataProtection.CreateProtector("OneCart.Installation.Database.v1");
        var configuredPath = configuration["Setup:InstallationFile"];
        filePath = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "installation.json")
            : configuredPath);
        if (!HasOperatorConfiguration && File.Exists(filePath))
        {
            var protectedValue = File.ReadAllText(filePath);
            installed = JsonSerializer.Deserialize<InstallationDatabase>(protector.Unprotect(protectedValue))
                ?? throw new InvalidOperationException("The protected installation configuration is invalid.");
        }
    }

    public InstallationDatabase? Current
    {
        get
        {
            if (configuration["ConnectionStrings:StoreFile"] is { Length: > 0 }
                || configuration.GetConnectionString("Store") is { Length: > 0 })
                return new InstallationDatabase(
                    StoreDatabaseProvider.GetProviderName(configuration["Database:Provider"]),
                    StoreConnectionStringConfiguration.GetRequired(configuration));
            return installed;
        }
    }

    public bool HasOperatorConfiguration => configuration["ConnectionStrings:StoreFile"] is { Length: > 0 }
        || configuration.GetConnectionString("Store") is { Length: > 0 };

    public bool IsReady => !installing && Current is not null;
    public void SetInstalling(bool value) => installing = value;

    public void Save(InstallationDatabase database)
    {
        if (Current is not null)
            throw new InvalidOperationException("Database installation is already configured.");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var temporaryPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, protector.Protect(JsonSerializer.Serialize(database)));
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(temporaryPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Move(temporaryPath, filePath);
            installed = database;
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public void RemoveIncompleteInstallation()
    {
        if (HasOperatorConfiguration) return;
        installed = null;
        if (File.Exists(filePath)) File.Delete(filePath);
    }
}
