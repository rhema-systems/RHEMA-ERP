using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;

[assembly: UserSecretsId("10483e62-e5b2-4652-8963-50f9500d3d5d")]

namespace ErpSystem.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        var apiDirectory = FindApiDirectory(Directory.GetCurrentDirectory());
        var config = new ConfigurationBuilder()
            .SetBasePath(apiDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets(
                typeof(DesignTimeDbContextFactory).Assembly,
                optional: true)
            .AddEnvironmentVariables()
            .Build();
        var connectionString = config.GetConnectionString("DefaultConnection");

        // Fallback connection string if none found
        connectionString ??= "Server=(localdb)\\MSSQLLocalDB;Database=ErpSystem;Trusted_Connection=True;TrustServerCertificate=True";

        optionsBuilder.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
        });

        optionsBuilder.ConfigureWarnings(warnings =>
            warnings
                .Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)
                .Ignore(CoreEventId.MappedEntityTypeIgnoredWarning)
                .Ignore(CoreEventId.MappedNavigationIgnoredWarning));

        // Create context without tenant filter for migrations
        return new ApplicationDbContext(optionsBuilder.Options);
    }

    private static string FindApiDirectory(string startDirectory)
    {
        for (var directory = new DirectoryInfo(startDirectory);
             directory != null;
             directory = directory.Parent)
        {
            var candidates = new[]
            {
                Path.Combine(directory.FullName, "src", "ErpSystem.Api"),
                Path.Combine(directory.FullName, "ErpSystem.Api")
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(Path.Combine(candidate, "appsettings.json")))
                    return candidate;
            }
        }

        throw new InvalidOperationException(
            "Unable to locate src/ErpSystem.Api/appsettings.json for design-time database configuration.");
    }
}
