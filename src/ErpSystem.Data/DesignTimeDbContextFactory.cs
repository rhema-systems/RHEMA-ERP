using System;
using System.IO;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace ErpSystem.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        // Try to load configuration from the API project's appsettings.json
        var basePath = Directory.GetCurrentDirectory();
        // Assume running in ErpSystem.Data; navigate to Api appsettings
        var apiAppSettings = Path.GetFullPath(Path.Combine(basePath, "..", "ErpSystem.Api", "appsettings.json"));

        string? connectionString = null;
        if (File.Exists(apiAppSettings))
        {
            var apiDevSettings = Path.ChangeExtension(apiAppSettings.Replace(".json", ".Development.json"), null) + ".json";
            var config = new ConfigurationBuilder()
                .SetBasePath(Path.GetDirectoryName(apiAppSettings)!)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddUserSecrets("10483e62-e5b2-4652-8963-50f9500d3d5d") // Use the API project's user secrets ID
                .AddEnvironmentVariables()
                .Build();
            connectionString = config.GetConnectionString("DefaultConnection");
        }

        // Fallback connection string if none found
        connectionString ??= "Server=(localdb)\\MSSQLLocalDB;Database=ErpSystem;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        optionsBuilder.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
        });

        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));

        // Create context without tenant filter for migrations
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
