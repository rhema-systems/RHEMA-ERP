using ErpSystem.Core.Entities;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api;

public static class UserSeeder
{
    public static async Task SeedTestUsersAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            logger.LogInformation("Starting manual test user seeding...");

            // Get default tenant
            var defaultTenant = await context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT");
            if (defaultTenant == null)
            {
                logger.LogError("Default tenant not found");
                return;
            }

            // Create admin user
            await CreateUserIfNotExistsAsync(userManager, logger, "admin", "admin@default.com", "Admin123!", 
                "System", "Administrator", defaultTenant.Id, Constants.Roles.SuperAdmin);

            // Create manager user  
            await CreateUserIfNotExistsAsync(userManager, logger, "manager", "manager@default.com", "Manager123!",
                "John", "Manager", defaultTenant.Id, Constants.Roles.Manager);

            // Create employee user
            await CreateUserIfNotExistsAsync(userManager, logger, "employee", "employee@default.com", "Employee123!",
                "Jane", "Employee", defaultTenant.Id, Constants.Roles.Employee);

            logger.LogInformation("Test user seeding completed!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during test user seeding");
            throw;
        }
    }

    private static async Task CreateUserIfNotExistsAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        string username, 
        string email, 
        string password,
        string firstName, 
        string lastName, 
        Guid tenantId, 
        string roleName)
    {
        var existingUser = await userManager.FindByNameAsync(username);
        if (existingUser != null)
        {
            logger.LogInformation("User {Username} already exists, skipping", username);
            return;
        }

        var user = new ApplicationUser
        {
            UserName = username,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            TenantId = tenantId,
            AuthenticationProvider = AuthenticationProvider.Local,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "System",
            PhoneNumberConfirmed = false
        };

        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            // Add user to role
            var roleResult = await userManager.AddToRoleAsync(user, roleName);
            if (roleResult.Succeeded)
            {
                logger.LogInformation("✅ Created test user: {Username} ({Email}) with role {Role}", 
                    username, email, roleName);
            }
            else
            {
                logger.LogError("❌ Failed to add role {Role} to user {Username}: {Errors}",
                    roleName, username, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            logger.LogError("❌ Failed to create test user {Username}: {Errors}",
                username, string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}