using Microsoft.EntityFrameworkCore;

namespace OneCart.Api.Data;

public static class StoreDatabaseProvider
{
    public const string PostgreSql = "PostgreSql";
    public const string SqlServer = "SqlServer";
    public const string MySql = "MySql";
    public const string PostgreSqlMigrationsAssembly = "OneCart.Api";
    public const string SqlServerMigrationsAssembly = "OneCart.SqlServer.Migrations";
    public const string MySqlMigrationsAssembly = "OneCart.MySql.Migrations";

    public static string GetProviderName(string? configuredProviderName) =>
        configuredProviderName?.Trim() is { Length: > 0 } configured
            ? configured
            : PostgreSql;

    public static void Configure(DbContextOptionsBuilder options, string providerName, string connectionString)
    {
        if (string.Equals(providerName, PostgreSql, StringComparison.OrdinalIgnoreCase)
            || string.Equals(providerName, "PostgreSQL", StringComparison.OrdinalIgnoreCase)
            || string.Equals(providerName, "Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(PostgreSqlMigrationsAssembly));
            return;
        }

        if (string.Equals(providerName, SqlServer, StringComparison.OrdinalIgnoreCase))
        {
            options.UseSqlServer(connectionString, sqlServer =>
                sqlServer.MigrationsAssembly(SqlServerMigrationsAssembly));
            return;
        }

        if (string.Equals(providerName, MySql, StringComparison.OrdinalIgnoreCase))
        {
            options.UseMySQL(connectionString, mysql => mysql.MigrationsAssembly(MySqlMigrationsAssembly));
            return;
        }

        throw new InvalidOperationException(
            $"Unsupported Database:Provider '{providerName}'. Choose '{PostgreSql}', '{SqlServer}', or '{MySql}'.");
    }
}
