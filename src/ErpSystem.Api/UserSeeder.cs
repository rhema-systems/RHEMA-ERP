using ErpSystem.Web.Services;

namespace ErpSystem.Api;

public static class UserSeeder
{
    public static async Task SeedTestUsersAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            logger.LogInformation("Starting manual test user seeding...");

            var seedingService = scope.ServiceProvider.GetRequiredService<IDatabaseSeedingService>();
            await seedingService.SeedTestUsersAsync();

            logger.LogInformation("Manual test user seeding completed!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during test user seeding");
            throw;
        }
    }
}
