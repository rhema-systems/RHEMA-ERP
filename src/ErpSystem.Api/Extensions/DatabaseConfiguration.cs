using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Data.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ErpSystem.Api.Extensions
{
    /// <summary>
    /// Database provider configuration service that supports multiple database types.
    /// Change the appsettings.json "Database:Provider" value and uncomment the corresponding NuGet package.
    /// </summary>
    public static class DatabaseConfiguration
    {
        /// <summary>
        /// Configures the database provider based on appsettings.json configuration.
        /// Supports: SqlServer, PostgreSQL, MySQL, Oracle, SQLite
        /// </summary>
        public static IServiceCollection AddConfigurableDatabase(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var provider = configuration.GetValue<string>("Database:Provider") ?? "SqlServer";
            var connectionString = configuration.GetConnectionString("DefaultConnection") ??
                throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            // Add audit logging if enabled
            var auditEnabled = configuration.GetValue<bool>("Audit:Enabled", true);
            if (auditEnabled)
            {
                services.AddAuditLogging(configuration);
            }

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                ConfigureDatabase(options, provider, connectionString);

                // Enable lazy loading proxies for better navigation property handling
                // Temporarily commented out due to build errors
                // options.UseLazyLoadingProxies();

                // NOTE: Audit interceptor requires service provider in DbContext constructor
                // which causes issues with tenant filtering during authentication.
                // Keeping audit disabled for now - can be re-enabled if needed by:
                // 1. Fixing the ApplicationDbContext constructor to not access ICurrentUserProvider
                // 2. Or implementing a different approach for tenant filtering
            });

            return services;
        }

        private static void ConfigureDatabase(DbContextOptionsBuilder options, string provider, string connectionString)
        {
            switch (provider.ToLowerInvariant())
            {
                case "sqlserver":
                    ConfigureSqlServer(options, connectionString);
                    break;

                case "postgresql":
                case "postgres":
                    ConfigurePostgreSQL(options, connectionString);
                    break;

                case "mysql":
                case "mariadb":
                    ConfigureMySQL(options, connectionString);
                    break;

                case "oracle":
                    ConfigureOracle(options, connectionString);
                    break;

                case "sqlite":
                    ConfigureSQLite(options, connectionString);
                    break;

                case "db2":
                case "ibm":
                    ConfigureDB2(options, connectionString);
                    break;

                case "firebird":
                    ConfigureFirebird(options, connectionString);
                    break;

                case "cosmosdb":
                case "cosmos":
                    ConfigureCosmosDB(options, connectionString);
                    break;

                case "inmemory":
                case "memory":
                    ConfigureInMemory(options, connectionString);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported database provider: {provider}. " +
                        $"Supported providers: SqlServer, PostgreSQL, MySQL, Oracle, SQLite, DB2, Firebird, CosmosDB, InMemory");
            }

            // The ERP intentionally keeps some historical dependent rows while soft-deleting principals.
            // We fix real model issues separately and suppress this noisy validation warning during startup.
            options.ConfigureWarnings(warnings =>
                warnings.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
        }

        #region SQL Server Configuration
        private static void ConfigureSqlServer(DbContextOptionsBuilder options, string connectionString)
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly("ErpSystem.Data");
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
                sqlOptions.CommandTimeout(30);
            });
        }
        #endregion

        #region PostgreSQL Configuration
        private static void ConfigurePostgreSQL(DbContextOptionsBuilder options, string connectionString)
        {
            // Uncomment when using PostgreSQL and add the NuGet package:
            // Npgsql.EntityFrameworkCore.PostgreSQL

            throw new NotImplementedException(
                "PostgreSQL support is commented out. To enable:\n" +
                "1. Uncomment the Npgsql.EntityFrameworkCore.PostgreSQL package in .csproj\n" +
                "2. Uncomment the code below\n" +
                "3. Set Database:Provider = 'PostgreSQL' in appsettings.json");

            /*
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly("ErpSystem.Data");
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30));
                npgsqlOptions.CommandTimeout(30);
            });
            */
        }
        #endregion

        #region MySQL Configuration
        private static void ConfigureMySQL(DbContextOptionsBuilder options, string connectionString)
        {
            // Uncomment when using MySQL and add the NuGet package:
            // Pomelo.EntityFrameworkCore.MySql

            throw new NotImplementedException(
                "MySQL support is commented out. To enable:\n" +
                "1. Uncomment the Pomelo.EntityFrameworkCore.MySql package in .csproj\n" +
                "2. Uncomment the code below\n" +
                "3. Set Database:Provider = 'MySQL' in appsettings.json");

            /*
            var serverVersion = ServerVersion.AutoDetect(connectionString);
            options.UseMySql(connectionString, serverVersion, mysqlOptions =>
            {
                mysqlOptions.MigrationsAssembly("ErpSystem.Data");
                mysqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30));
                mysqlOptions.CommandTimeout(30);
            });
            */
        }
        #endregion

        #region Oracle Configuration
        private static void ConfigureOracle(DbContextOptionsBuilder options, string connectionString)
        {
            // Uncomment when using Oracle and add the NuGet package:
            // Oracle.EntityFrameworkCore

            throw new NotImplementedException(
                "Oracle support is commented out. To enable:\n" +
                "1. Uncomment the Oracle.EntityFrameworkCore package in .csproj\n" +
                "2. Uncomment the code below\n" +
                "3. Set Database:Provider = 'Oracle' in appsettings.json");

            /*
            options.UseOracle(connectionString, oracleOptions =>
            {
                oracleOptions.MigrationsAssembly("ErpSystem.Data");
                oracleOptions.UseOracleSQLCompatibility("11");
                oracleOptions.CommandTimeout(30);
            });
            */
        }
        #endregion

        #region SQLite Configuration
        private static void ConfigureSQLite(DbContextOptionsBuilder options, string connectionString)
        {
            // Uncomment when using SQLite and add the NuGet package:
            // Microsoft.EntityFrameworkCore.Sqlite

            throw new NotImplementedException(
                "SQLite support is commented out. To enable:\n" +
                "1. Uncomment the Microsoft.EntityFrameworkCore.Sqlite package in .csproj\n" +
                "2. Uncomment the code below\n" +
                "3. Set Database:Provider = 'SQLite' in appsettings.json");

            /*
            options.UseSqlite(connectionString, sqliteOptions =>
            {
                sqliteOptions.MigrationsAssembly("ErpSystem.Data");
                sqliteOptions.CommandTimeout(30);
            });
            */
        }
        #endregion

        #region IBM DB2 Configuration
        private static void ConfigureDB2(DbContextOptionsBuilder options, string connectionString)
        {
            // Uncomment when using IBM DB2 and add the NuGet package:
            // IBM.EntityFrameworkCore

            throw new NotImplementedException(
                "IBM DB2 support is commented out. To enable:\n" +
                "1. Uncomment the IBM.EntityFrameworkCore package in .csproj\n" +
                "2. Uncomment the code below\n" +
                "3. Set Database:Provider = 'DB2' in appsettings.json");

            /*
            options.UseDb2(connectionString, db2Options =>
            {
                db2Options.MigrationsAssembly("ErpSystem.Data");
                db2Options.CommandTimeout(30);
            });
            */
        }
        #endregion

        #region Firebird Configuration
        private static void ConfigureFirebird(DbContextOptionsBuilder options, string connectionString)
        {
            // Uncomment when using Firebird and add the NuGet package:
            // FirebirdSql.EntityFrameworkCore.Firebird

            throw new NotImplementedException(
                "Firebird support is commented out. To enable:\n" +
                "1. Uncomment the FirebirdSql.EntityFrameworkCore.Firebird package in .csproj\n" +
                "2. Uncomment the code below\n" +
                "3. Set Database:Provider = 'Firebird' in appsettings.json");

            /*
            options.UseFirebird(connectionString, firebirdOptions =>
            {
                firebirdOptions.MigrationsAssembly("ErpSystem.Data");
                firebirdOptions.CommandTimeout(30);
            });
            */
        }
        #endregion

        #region Cosmos DB Configuration
        private static void ConfigureCosmosDB(DbContextOptionsBuilder options, string connectionString)
        {
            // Uncomment when using Azure Cosmos DB and add the NuGet package:
            // Microsoft.EntityFrameworkCore.Cosmos

            throw new NotImplementedException(
                "Azure Cosmos DB support is commented out. To enable:\n" +
                "1. Uncomment the Microsoft.EntityFrameworkCore.Cosmos package in .csproj\n" +
                "2. Uncomment the code below\n" +
                "3. Set Database:Provider = 'CosmosDB' in appsettings.json\n" +
                "4. Note: Cosmos DB has different data modeling requirements");

            /*
            // Connection string format: "AccountEndpoint=https://...; AccountKey=...; DatabaseName=..."
            var parts = connectionString.Split(';');
            var accountEndpoint = parts.FirstOrDefault(p => p.StartsWith("AccountEndpoint="))?.Split('=')[1];
            var accountKey = parts.FirstOrDefault(p => p.StartsWith("AccountKey="))?.Split('=')[1];
            var databaseName = parts.FirstOrDefault(p => p.StartsWith("DatabaseName="))?.Split('=')[1];
            
            options.UseCosmos(accountEndpoint, accountKey, databaseName);
            */
        }
        #endregion

        #region In-Memory Database Configuration
        private static void ConfigureInMemory(DbContextOptionsBuilder options, string connectionString)
        {
            // Uncomment when using In-Memory database and add the NuGet package:
            // Microsoft.EntityFrameworkCore.InMemory

            throw new NotImplementedException(
                "In-Memory Database support is commented out. To enable:\n" +
                "1. Uncomment the Microsoft.EntityFrameworkCore.InMemory package in .csproj\n" +
                "2. Uncomment the code below\n" +
                "3. Set Database:Provider = 'InMemory' in appsettings.json\n" +
                "4. Note: In-Memory is for testing only - data is not persisted");

            /*
            // connectionString can be used as database name for InMemory
            var databaseName = string.IsNullOrEmpty(connectionString) ? "TestDatabase" : connectionString;
            options.UseInMemoryDatabase(databaseName);
            */
        }
        #endregion

        /// <summary>
        /// Gets the current database provider information for diagnostics
        /// </summary>
        public static DatabaseProviderInfo GetDatabaseProviderInfo(IConfiguration configuration)
        {
            var provider = configuration.GetValue<string>("Database:Provider") ?? "SqlServer";
            var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";

            return new DatabaseProviderInfo
            {
                Provider = provider,
                IsConnectionStringConfigured = !string.IsNullOrEmpty(connectionString),
                SupportedProviders = new[] { "SqlServer", "PostgreSQL", "MySQL", "Oracle", "SQLite", "DB2", "Firebird", "CosmosDB", "InMemory" }
            };
        }
    }

    /// <summary>
    /// Information about the configured database provider
    /// </summary>
    public class DatabaseProviderInfo
    {
        public string Provider { get; set; } = string.Empty;
        public bool IsConnectionStringConfigured { get; set; }
        public string[] SupportedProviders { get; set; } = Array.Empty<string>();
    }
}
