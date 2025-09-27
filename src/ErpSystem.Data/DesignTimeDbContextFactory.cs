using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
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
            var config = new ConfigurationBuilder()
                .AddJsonFile(apiAppSettings, optional: false)
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

        // Create context without tenant filter for migrations
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}