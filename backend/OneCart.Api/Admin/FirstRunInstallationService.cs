using System.ComponentModel.DataAnnotations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using Npgsql;
using OneCart.Api.Data;

namespace OneCart.Api.Admin;

public sealed record DatabaseSetupRequest(
    string Provider, string Host, int? Port, string DatabaseName,
    string UserName, string Password, bool TrustServerCertificate = false,
    bool EncryptConnection = true);

public sealed record CompleteInstallationRequest(
    DatabaseSetupRequest Database, string Email, string Password,
    string ConfirmPassword, string DefaultLanguageCode, string CurrencyCode);

public sealed record InstallationResult(bool Succeeded, bool AlreadyComplete, string? Error)
{
    public static InstallationResult Complete { get; } = new(true, false, null);
    public static InstallationResult Existing { get; } = new(false, true, "Installation is already complete.");
    public static InstallationResult Invalid(string error) => new(false, false, error);
}

public sealed class FirstRunInstallationService(
    InstallationConfigurationStore configurationStore,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    IWebHostEnvironment environment)
{
    private static readonly SemaphoreSlim InstallationLock = new(1, 1);

    public static bool TryBuildDatabase(DatabaseSetupRequest request, out InstallationDatabase? database, out string? error)
    {
        database = null;
        error = null;
        if (request is null || request.Host is null || request.DatabaseName is null
            || request.UserName is null || request.Password is null
            || request.Host.Trim().Length is < 1 or > 255
            || request.DatabaseName.Trim().Length is < 1 or > 128
            || request.UserName.Trim().Length is < 1 or > 128
            || request.Password.Length is < 1 or > 512
            || request.Port is < 1 or > 65535
            || new[] { request.Host, request.DatabaseName, request.UserName, request.Password }
                .Any(value => value.Any(char.IsControl)))
        {
            error = "Enter a database host, name, user, and password within the allowed lengths.";
            return false;
        }

        if (string.Equals(request.Provider, StoreDatabaseProvider.PostgreSql, StringComparison.OrdinalIgnoreCase))
        {
            var connection = new NpgsqlConnectionStringBuilder
            {
                Host = request.Host.Trim(), Port = request.Port ?? 5432,
                Database = request.DatabaseName.Trim(), Username = request.UserName.Trim(),
                Password = request.Password, Pooling = false, Timeout = 10
            };
            database = new InstallationDatabase(StoreDatabaseProvider.PostgreSql, connection.ConnectionString);
            return true;
        }
        if (string.Equals(request.Provider, StoreDatabaseProvider.SqlServer, StringComparison.OrdinalIgnoreCase))
        {
            var host = request.Host.Trim();
            var connection = new SqlConnectionStringBuilder
            {
                DataSource = request.Port.HasValue ? $"{host},{request.Port.Value}" : host,
                InitialCatalog = request.DatabaseName.Trim(), UserID = request.UserName.Trim(),
                Password = request.Password, IntegratedSecurity = false, Encrypt = request.EncryptConnection,
                TrustServerCertificate = request.TrustServerCertificate,
                Pooling = false, ConnectTimeout = 10
            };
            database = new InstallationDatabase(StoreDatabaseProvider.SqlServer, connection.ConnectionString);
            return true;
        }
        if (string.Equals(request.Provider, StoreDatabaseProvider.MySql, StringComparison.OrdinalIgnoreCase))
        {
            var host = request.Host.Trim();
            var local = host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || (System.Net.IPAddress.TryParse(host, out var address) && System.Net.IPAddress.IsLoopback(address));
            if (!request.EncryptConnection && !local)
            {
                error = "MySQL requires TLS for connections to a remote database server.";
                return false;
            }
            var connection = new MySqlConnectionStringBuilder
            {
                Server = host, Port = (uint)(request.Port ?? 3306),
                Database = request.DatabaseName.Trim(), UserID = request.UserName.Trim(),
                Password = request.Password, Pooling = false, ConnectionTimeout = 10,
                SslMode = request.EncryptConnection ? MySqlSslMode.VerifyFull : MySqlSslMode.Disabled,
                AllowPublicKeyRetrieval = !request.EncryptConnection
            };
            database = new InstallationDatabase(StoreDatabaseProvider.MySql, connection.ConnectionString);
            return true;
        }
        error = "Choose PostgreSQL, SQL Server, or MySQL.";
        return false;
    }

    public async Task<bool> TestConnectionAsync(InstallationDatabase database, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() && database.Provider == StoreDatabaseProvider.MySql
            && new MySqlConnectionStringBuilder(database.ConnectionString).SslMode != MySqlSslMode.VerifyFull)
            return false;

        if (!environment.IsDevelopment() && database.Provider == StoreDatabaseProvider.SqlServer)
        {
            var sql = new SqlConnectionStringBuilder(database.ConnectionString);
            if (sql.TrustServerCertificate || sql.Encrypt == SqlConnectionEncryptOption.Optional) return false;
        }
        if (await CanConnectAsync(database, cancellationToken)) return true;
        var maintenanceConnection = database.Provider switch
        {
            StoreDatabaseProvider.PostgreSql => new NpgsqlConnectionStringBuilder(database.ConnectionString) { Database = "postgres" }.ConnectionString,
            StoreDatabaseProvider.MySql => new MySqlConnectionStringBuilder(database.ConnectionString) { Database = "" }.ConnectionString,
            _ => new SqlConnectionStringBuilder(database.ConnectionString) { InitialCatalog = "master" }.ConnectionString
        };
        return await CanConnectAsync(database with { ConnectionString = maintenanceConnection }, cancellationToken);
    }

    private static async Task<bool> CanConnectAsync(InstallationDatabase database, CancellationToken cancellationToken)
    {
        if (database.Provider == StoreDatabaseProvider.MySql)
        {
            await using var connection = new MySqlConnection(database.ConnectionString);
            try
            {
                await connection.OpenAsync(cancellationToken);
                return true;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return false;
            }
        }

        var options = new DbContextOptionsBuilder<StoreDbContext>();
        StoreDatabaseProvider.Configure(options, database.Provider, database.ConnectionString);
        await using var context = new StoreDbContext(options.Options);
        try { return await context.Database.CanConnectAsync(cancellationToken); }
        catch (Exception exception) when (exception is not OperationCanceledException) { return false; }
    }

    public async Task<InstallationResult> InstallAsync(
        CompleteInstallationRequest request, CancellationToken cancellationToken)
    {
        if (!TryBuildDatabase(request.Database, out var selectedDatabase, out var validationError))
            return InstallationResult.Invalid(validationError!);
        if (!environment.IsDevelopment() && string.IsNullOrWhiteSpace(configuration["DataProtection:KeyDirectory"]))
            return InstallationResult.Invalid("Configure a persistent Data Protection key directory on the host before installation.");
        var email = request.Email?.Trim();
        if (email is null || email.Length is < 1 or > 254
            || !new EmailAddressAttribute().IsValid(email))
            return InstallationResult.Invalid("Enter a valid SystemAdmin email address.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > 256
            || request.Password != request.ConfirmPassword)
            return InstallationResult.Invalid("Enter matching administrator passwords no longer than 256 characters.");
        var language = request.DefaultLanguageCode?.Trim().ToLowerInvariant();
        if (language is not ("en" or "bn" or "es"))
            return InstallationResult.Invalid("Choose English, Bangla, or Spanish.");
        var currency = request.CurrencyCode?.Trim().ToUpperInvariant();
        if (currency is null || currency.Length != 3 || currency.Any(character => character is < 'A' or > 'Z'))
            return InstallationResult.Invalid("Choose a three-letter store currency.");

        await InstallationLock.WaitAsync(cancellationToken);
        try
        {
            if (configurationStore.Current is not null) return InstallationResult.Existing;
            if (!await TestConnectionAsync(selectedDatabase!, cancellationToken))
                return InstallationResult.Invalid("Could not connect to the database server. Check its credentials, network access, and certificate settings.");

            var options = new DbContextOptionsBuilder<StoreDbContext>();
            StoreDatabaseProvider.Configure(options, selectedDatabase!.Provider, selectedDatabase.ConnectionString);
            await using (var context = new StoreDbContext(options.Options))
            {
                var existing = await context.Database.CanConnectAsync(cancellationToken)
                    && (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).Any();
                if (existing && await context.Users.AnyAsync(cancellationToken))
                    return InstallationResult.Invalid("This database already contains administrator accounts. Use its existing installation.");
                await context.Database.MigrateAsync(cancellationToken);
                if (await context.Users.AnyAsync(cancellationToken))
                    return InstallationResult.Invalid("This database already contains administrator accounts. Use its existing installation.");
            }

            configurationStore.SetInstalling(true);
            configurationStore.Save(selectedDatabase);
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var setup = scope.ServiceProvider.GetRequiredService<InitialSystemAdminSetupService>();
                var result = await setup.CreateAsync(email, request.Password, cancellationToken, language, currency);
                if (!result.Succeeded)
                {
                    configurationStore.RemoveIncompleteInstallation();
                    return InstallationResult.Invalid(string.Join(" ", result.Errors));
                }
                return InstallationResult.Complete;
            }
            catch
            {
                configurationStore.RemoveIncompleteInstallation();
                throw;
            }
        }
        finally
        {
            configurationStore.SetInstalling(false);
            InstallationLock.Release();
        }
    }
}
