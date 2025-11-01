using ErpSystem.Core.Entities.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds maintenance configuration data (WorkOrderTypes, PriorityLevels, MaintenanceTypes)
/// </summary>
public class MaintenanceConfigurationSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MaintenanceConfigurationSeeder> _logger;

    public MaintenanceConfigurationSeeder(ApplicationDbContext context, ILogger<MaintenanceConfigurationSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            _logger.LogInformation("Starting maintenance configuration seeding...");

            // Get default tenant
            var defaultTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT");
            if (defaultTenant == null)
            {
                _logger.LogError("Default tenant not found. Cannot seed maintenance configuration.");
                return;
            }

            // Seed work order types
            await SeedWorkOrderTypesAsync(defaultTenant.Id);

            // Seed priority levels
            await SeedPriorityLevelsAsync(defaultTenant.Id);

            // Seed maintenance types
            await SeedMaintenanceTypesAsync(defaultTenant.Id);

            // Seed asset categories
            await SeedAssetCategoriesAsync(defaultTenant.Id);

            // Seed asset types
            await SeedAssetTypesAsync(defaultTenant.Id);

            await _context.SaveChangesAsync();
            _logger.LogInformation("Maintenance configuration seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during maintenance configuration seeding");
            throw;
        }
    }

    private async Task SeedWorkOrderTypesAsync(Guid tenantId)
    {
        _logger.LogInformation("Seeding work order types...");

        var workOrderTypes = new[]
        {
            new { Code = "PM", Name = "Preventive Maintenance", Description = "Scheduled preventive maintenance work orders", Color = "#3b82f6", Icon = "calendar" },
            new { Code = "CM", Name = "Corrective Maintenance", Description = "Repair and corrective maintenance work orders", Color = "#f59e0b", Icon = "wrench" },
            new { Code = "EM", Name = "Emergency", Description = "Emergency repair work orders", Color = "#ef4444", Icon = "alert" },
            new { Code = "INSP", Name = "Inspection", Description = "Asset inspection work orders", Color = "#8b5cf6", Icon = "search" },
            new { Code = "INST", Name = "Installation", Description = "New equipment installation work orders", Color = "#10b981", Icon = "plus" },
            new { Code = "UPGR", Name = "Upgrade", Description = "Equipment upgrade work orders", Color = "#06b6d4", Icon = "arrow-up" },
            new { Code = "CAL", Name = "Calibration", Description = "Equipment calibration work orders", Color = "#ec4899", Icon = "settings" }
        };

        foreach (var wot in workOrderTypes)
        {
            var exists = await _context.WorkOrderTypes.AnyAsync(w => w.TenantId == tenantId && w.Code == wot.Code);
            if (!exists)
            {
                var workOrderType = new WorkOrderType
                {
                    TenantId = tenantId,
                    Code = wot.Code,
                    Name = wot.Name,
                    Description = wot.Description,
                    Color = wot.Color,
                    Icon = wot.Icon,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.WorkOrderTypes.Add(workOrderType);
                _logger.LogInformation("Seeded work order type: {Name}", wot.Name);
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedPriorityLevelsAsync(Guid tenantId)
    {
        _logger.LogInformation("Seeding priority levels...");

        var priorityLevels = new[]
        {
            new { Name = "Low", Level = 1, Description = "Low priority maintenance", Color = "#22c55e", ResponseTimeHours = 168 }, // 7 days
            new { Name = "Medium", Level = 2, Description = "Medium priority maintenance", Color = "#3b82f6", ResponseTimeHours = 48 }, // 2 days
            new { Name = "High", Level = 3, Description = "High priority maintenance", Color = "#f59e0b", ResponseTimeHours = 12 }, // 12 hours
            new { Name = "Critical", Level = 4, Description = "Critical emergency maintenance", Color = "#ef4444", ResponseTimeHours = 2 } // 2 hours
        };

        foreach (var pl in priorityLevels)
        {
            var exists = await _context.PriorityLevels.AnyAsync(p => p.TenantId == tenantId && p.Name == pl.Name);
            if (!exists)
            {
                var priorityLevel = new PriorityLevel
                {
                    TenantId = tenantId,
                    Name = pl.Name,
                    Level = pl.Level,
                    Description = pl.Description,
                    Color = pl.Color,
                    ResponseTimeHours = pl.ResponseTimeHours,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.PriorityLevels.Add(priorityLevel);
                _logger.LogInformation("Seeded priority level: {Name}", pl.Name);
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedMaintenanceTypesAsync(Guid tenantId)
    {
        _logger.LogInformation("Seeding maintenance types...");

        var maintenanceTypes = new[]
        {
            new { Code = "PREV", Name = "Preventive", Description = "Scheduled preventive maintenance to prevent failures", Color = "#3b82f6" },
            new { Code = "PRED", Name = "Predictive", Description = "Condition-based maintenance based on monitoring", Color = "#8b5cf6" },
            new { Code = "CORR", Name = "Corrective", Description = "Repair of failed or failing equipment", Color = "#f59e0b" },
            new { Code = "EMER", Name = "Emergency", Description = "Urgent repairs for critical failures", Color = "#ef4444" },
            new { Code = "COND", Name = "Condition-Based", Description = "Maintenance triggered by equipment condition", Color = "#06b6d4" },
            new { Code = "RUN", Name = "Run-to-Failure", Description = "Allow equipment to run until failure", Color = "#64748b" }
        };

        foreach (var mt in maintenanceTypes)
        {
            var exists = await _context.MaintenanceTypes.AnyAsync(m => m.TenantId == tenantId && m.Code == mt.Code);
            if (!exists)
            {
                var maintenanceType = new MaintenanceType
                {
                    TenantId = tenantId,
                    Code = mt.Code,
                    Name = mt.Name,
                    Description = mt.Description,
                    Color = mt.Color,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.MaintenanceTypes.Add(maintenanceType);
                _logger.LogInformation("Seeded maintenance type: {Name}", mt.Name);
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedAssetCategoriesAsync(Guid tenantId)
    {
        _logger.LogInformation("Seeding asset categories...");

        var categories = new[]
        {
            new { Code = "HVAC", Name = "HVAC Systems", Description = "Heating, ventilation, and air conditioning systems" },
            new { Code = "ELEC", Name = "Electrical Systems", Description = "Electrical equipment and systems" },
            new { Code = "PLUMB", Name = "Plumbing Systems", Description = "Plumbing and water systems" },
            new { Code = "MECH", Name = "Mechanical Equipment", Description = "General mechanical equipment" },
            new { Code = "TRANS", Name = "Transportation", Description = "Elevators, conveyors, vehicles" },
            new { Code = "POWER", Name = "Power Systems", Description = "Generators, UPS, power distribution" },
            new { Code = "SAFETY", Name = "Safety Systems", Description = "Fire, security, emergency systems" },
            new { Code = "IT", Name = "IT Equipment", Description = "Computer and network equipment" },
            new { Code = "BUILD", Name = "Building Infrastructure", Description = "Structural and building systems" },
            new { Code = "PROD", Name = "Production Equipment", Description = "Manufacturing and production machinery" }
        };

        foreach (var cat in categories)
        {
            var exists = await _context.MaintenanceAssetCategories.AnyAsync(c => c.TenantId == tenantId && c.Code == cat.Code);
            if (!exists)
            {
                var category = new MaintenanceAssetCategory
                {
                    TenantId = tenantId,
                    Code = cat.Code,
                    Name = cat.Name,
                    Description = cat.Description,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.MaintenanceAssetCategories.Add(category);
                _logger.LogInformation("Seeded asset category: {Name}", cat.Name);
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedAssetTypesAsync(Guid tenantId)
    {
        _logger.LogInformation("Seeding asset types...");

        // Get categories first
        var hvacCategory = await _context.MaintenanceAssetCategories
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Code == "HVAC");
        var elecCategory = await _context.MaintenanceAssetCategories
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Code == "ELEC");
        var powerCategory = await _context.MaintenanceAssetCategories
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Code == "POWER");
        var safetyCategory = await _context.MaintenanceAssetCategories
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Code == "SAFETY");
        var transCategory = await _context.MaintenanceAssetCategories
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Code == "TRANS");

        if (hvacCategory == null) return;

        var assetTypes = new[]
        {
            new { Code = "EQUIPMENT", Name = "Equipment", Description = "General equipment and machinery" },
            new { Code = "VEHICLE", Name = "Vehicle", Description = "Vehicles and mobile equipment" },
            new { Code = "BUILDING", Name = "Building", Description = "Building structures and facilities" },
            new { Code = "INFRASTRUCTURE", Name = "Infrastructure", Description = "Infrastructure and utilities" },
            new { Code = "SYSTEM", Name = "System", Description = "Complex systems (HVAC, electrical, etc.)" },
            new { Code = "TOOL", Name = "Tool", Description = "Tools and small equipment" }
        };

        foreach (var at in assetTypes)
        {
            var exists = await _context.AssetTypes.AnyAsync(a => a.TenantId == tenantId && a.Code == at.Code);
            if (!exists)
            {
                var assetType = new AssetType
                {
                    TenantId = tenantId,
                    Code = at.Code,
                    Name = at.Name,
                    Description = at.Description,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.AssetTypes.Add(assetType);
                _logger.LogInformation("Seeded asset type: {Name}", at.Name);
            }
        }

        await _context.SaveChangesAsync();
    }
}
