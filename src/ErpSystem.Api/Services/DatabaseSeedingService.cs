using ErpSystem.Core.Entities;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Web.Services
{
    public interface IDatabaseSeedingService
    {
        Task SeedAsync();
        Task SeedBasicDataAsync();
        Task SeedTestUsersAsync();
        Task<bool> HasSeedDataAsync();
    }

    public class DatabaseSeedingService : IDatabaseSeedingService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ILogger<DatabaseSeedingService> _logger;
        private readonly IWebHostEnvironment _environment;

        public DatabaseSeedingService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            ILogger<DatabaseSeedingService> logger,
            IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
            _environment = environment;
        }

        public async Task SeedAsync()
        {
            try
            {
                _logger.LogInformation("Starting database seeding...");

                // Ensure database is created and migrated
                await _context.Database.MigrateAsync();

                // Check if we already have seed data
                var hasData = await HasSeedDataAsync();
                if (!hasData)
                {
                    // Seed basic data
                    await SeedBasicDataAsync();
                }
                else
                {
                    _logger.LogInformation("Basic data already exists, skipping basic seeding");
                }

                // Always seed/update test users in development to ensure correct passwords
                if (_environment.IsDevelopment())
                {
                    _logger.LogInformation("Ensuring test users have correct passwords...");
                    await SeedTestUsersAsync();
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Database seeding completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during database seeding");
                throw;
            }
        }

        public async Task SeedBasicDataAsync()
        {
            _logger.LogInformation("Seeding basic data...");

            // Seed roles
            await SeedRolesAsync();

            // Seed default tenant
            await SeedDefaultTenantAsync();
            
            // Seed default tenant modules
            await SeedDefaultTenantModulesAsync();

            _logger.LogInformation("Basic data seeding completed");
        }

        public async Task SeedTestUsersAsync()
        {
            _logger.LogInformation("Seeding test users...");

            var defaultTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT");
            if (defaultTenant == null)
            {
                _logger.LogError("Default tenant not found for test user seeding");
                return;
            }

            // Create admin user
            await CreateTestUserAsync("admin", "admin@default.com", "Admin123!", 
                "System", "Administrator", defaultTenant.Id, Constants.Roles.SuperAdmin);

            // Create manager user
            await CreateTestUserAsync("manager", "manager@default.com", "Manager123!",
                "John", "Manager", defaultTenant.Id, Constants.Roles.Manager);

            // Create employee user
            await CreateTestUserAsync("employee", "employee@default.com", "Employee123!",
                "Jane", "Employee", defaultTenant.Id, Constants.Roles.Employee);

            _logger.LogInformation("Test users seeding completed");
        }

        public async Task<bool> HasSeedDataAsync()
        {
            var hasRoles = await _roleManager.Roles.AnyAsync();
            var hasTenants = await _context.Tenants.AnyAsync();
            return hasRoles && hasTenants;
        }

        private async Task SeedRolesAsync()
        {
            var roles = new[]
            {
                new { Name = Constants.Roles.SuperAdmin, Description = "System Super Administrator with full access" },
                new { Name = Constants.Roles.TenantAdmin, Description = "Tenant Administrator with tenant-wide access" },
                new { Name = Constants.Roles.Manager, Description = "Manager with departmental access" },
                new { Name = Constants.Roles.Employee, Description = "Standard employee with limited access" },
                new { Name = "Finance User", Description = "User with access to finance module" },
                new { Name = "HR User", Description = "User with access to HR module" },
                new { Name = "Sales User", Description = "User with access to sales module" },
                new { Name = "Inventory User", Description = "User with access to inventory module" },
                new { Name = "Procurement User", Description = "User with access to procurement module" },
                new { Name = "Marketing User", Description = "User with access to marketing module" }
            };

            foreach (var roleInfo in roles)
            {
                var existingRole = await _roleManager.FindByNameAsync(roleInfo.Name);
                if (existingRole == null)
                {
                    var role = new ApplicationRole(roleInfo.Name)
                    {
                        Description = roleInfo.Description,
                        IsSystemRole = roleInfo.Name.Contains("Admin") || roleInfo.Name.Contains("Manager"),
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    };

                    var result = await _roleManager.CreateAsync(role);
                    if (result.Succeeded)
                    {
                        _logger.LogDebug("Created role: {RoleName}", roleInfo.Name);
                    }
                    else
                    {
                        _logger.LogError("Failed to create role {RoleName}: {Errors}", 
                            roleInfo.Name, string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }
            }
        }

        private async Task SeedDefaultTenantAsync()
        {
            var existingTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT");
            if (existingTenant == null)
            {
                var tenant = new Tenant
                {
                    Name = "Default Company",
                    Code = "DEFAULT",
                    Description = "Default tenant for system operations",
                    Status = TenantStatus.Active,
                    ContactEmail = "admin@default.com",
                    ContactPhone = "+1-555-0100",
                    Address = "123 Default Street, Default City, DC 12345",
                    SubscriptionStartDate = DateTime.UtcNow,
                    SubscriptionEndDate = DateTime.UtcNow.AddYears(1),
                    LdapEnabled = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.Tenants.Add(tenant);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Created default tenant: {TenantName}", tenant.Name);
            }
        }

        private async Task SeedDefaultTenantModulesAsync()
        {
            var defaultTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT");
            if (defaultTenant == null)
            {
                _logger.LogError("Default tenant not found for module seeding");
                return;
            }

            var modules = new[]
            {
                new { ModuleName = "Financial", Description = "Financial reports and analytics" },
                new { ModuleName = "Sales", Description = "Sales performance and CRM reports" },
                new { ModuleName = "Human Resources", Description = "HR and employee reports" },
                new { ModuleName = "Inventory", Description = "Stock and inventory reports" },
                new { ModuleName = "Operations", Description = "Operational efficiency reports" },
                new { ModuleName = "Procurement", Description = "Purchasing and supplier reports" },
                new { ModuleName = "Marketing", Description = "Marketing campaigns and analytics" }
            };

            foreach (var moduleInfo in modules)
            {
                var existingModule = await _context.TenantModules
                    .FirstOrDefaultAsync(tm => tm.TenantId == defaultTenant.Id && tm.ModuleName == moduleInfo.ModuleName);
                    
                if (existingModule == null)
                {
                    var module = new TenantModule
                    {
                        TenantId = defaultTenant.Id,
                        ModuleName = moduleInfo.ModuleName,
                        Description = moduleInfo.Description,
                        Status = ModuleStatus.Enabled,
                        EnabledDate = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "System"
                    };

                    _context.TenantModules.Add(module);
                    _logger.LogDebug("Created tenant module: {ModuleName} for tenant {TenantName}", moduleInfo.ModuleName, defaultTenant.Name);
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task CreateTestUserAsync(string username, string email, string password,
            string firstName, string lastName, Guid tenantId, string roleName)
        {
            var existingUser = await _userManager.FindByNameAsync(username);
            if (existingUser != null)
            {
                // Ensure tenant, profile fields, and active status are correct
                var needsUpdate = false;
                if (existingUser.TenantId != tenantId) { existingUser.TenantId = tenantId; needsUpdate = true; }
                if (existingUser.Email != email) { existingUser.Email = email; needsUpdate = true; }
                if (existingUser.FirstName != firstName) { existingUser.FirstName = firstName; needsUpdate = true; }
                if (existingUser.LastName != lastName) { existingUser.LastName = lastName; needsUpdate = true; }
                if (!existingUser.EmailConfirmed) { existingUser.EmailConfirmed = true; needsUpdate = true; }
                if (!existingUser.IsActive) { existingUser.IsActive = true; needsUpdate = true; }

                if (needsUpdate)
                {
                    await _userManager.UpdateAsync(existingUser);
                }

                // Ensure role assignment
                var inRole = await _userManager.IsInRoleAsync(existingUser, roleName);
                if (!inRole)
                {
                    var addRoleResult = await _userManager.AddToRoleAsync(existingUser, roleName);
                    if (!addRoleResult.Succeeded)
                    {
                        _logger.LogError("Failed to ensure role {Role} for user {Username}: {Errors}", roleName, username,
                            string.Join(", ", addRoleResult.Errors.Select(e => e.Description)));
                    }
                }

                // Reset password to the expected strong password to align with docs/login page
                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(existingUser);
                var resetResult = await _userManager.ResetPasswordAsync(existingUser, resetToken, password);
                if (resetResult.Succeeded)
                {
                    // Clear lockout just in case
                    await _userManager.SetLockoutEndDateAsync(existingUser, null);
                    await _userManager.ResetAccessFailedCountAsync(existingUser);
                    _logger.LogInformation("Updated existing user {Username} and reset password.", username);
                }
                else
                {
                    _logger.LogError("Failed to reset password for {Username}: {Errors}", username,
                        string.Join(", ", resetResult.Errors.Select(e => e.Description)));
                }

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

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                // Add user to role
                var roleResult = await _userManager.AddToRoleAsync(user, roleName);
                if (roleResult.Succeeded)
                {
                    _logger.LogInformation("Created test user: {Username} with role {Role}", username, roleName);
                }
                else
                {
                    _logger.LogError("Failed to add role {Role} to user {Username}: {Errors}",
                        roleName, username, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                _logger.LogError("Failed to create test user {Username}: {Errors}",
                    username, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    // Extension methods for easy registration
    public static class DatabaseSeedingServiceExtensions
    {
        public static IServiceCollection AddDatabaseSeeding(this IServiceCollection services)
        {
            services.AddScoped<IDatabaseSeedingService, DatabaseSeedingService>();
            return services;
        }

        public static async Task SeedDatabaseAsync(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var seedingService = scope.ServiceProvider.GetRequiredService<IDatabaseSeedingService>();
            await seedingService.SeedAsync();
        }
    }
}