using ErpSystem.Core.Entities.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

public class ToolManagementSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ToolManagementSeeder> _logger;

    public ToolManagementSeeder(ApplicationDbContext context, ILogger<ToolManagementSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            _logger.LogInformation("Starting Tool Management data seeding...");

            // Get first tenant (or use a specific tenant ID if you know it)
            var tenant = await _context.Tenants.FirstOrDefaultAsync();
            if (tenant == null)
            {
                _logger.LogWarning("No tenants found. Please create a tenant first.");
                return;
            }

            var tenantId = tenant.Id;
            _logger.LogInformation($"Using tenant: {tenant.Name} ({tenantId})");

            // Check if tools already exist
            var existingTools = await _context.MaintenanceTools.AnyAsync();
            if (existingTools)
            {
                _logger.LogInformation("Tools already exist. Skipping seeding.");
                return;
            }

            // Create sample tools
            var tools = new List<MaintenanceTool>
            {
                new MaintenanceTool
                {
                    Id = Guid.NewGuid(),
                    ToolCode = "TL-001",
                    Name = "Digital Torque Wrench",
                    Description = "High-precision digital torque wrench, 0-100 Nm range",
                    Category = "Hand Tools",
                    Manufacturer = "Snap-On",
                    Model = "TECH3FR100",
                    SerialNumber = "SN-2024-001",
                    Status = "Available",
                    CurrentLocation = "Tool Room A - Bay 1",
                    HomeLocation = "Tool Room A - Bay 1",
                    PurchasePrice = 450.00m,
                    CurrentValue = 400.00m,
                    DailyRentalRate = 25.00m,
                    TotalUsageDays = 45,
                    LastUsedDate = DateTime.UtcNow.AddDays(-7),
                    RequiresCertification = false,
                    RequiresTraining = true,
                    SafetyNotes = "Always use within specified torque range. Calibrate annually.",
                    IsActive = true,
                    TenantId = tenantId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                },
                new MaintenanceTool
                {
                    Id = Guid.NewGuid(),
                    ToolCode = "TL-002",
                    Name = "Hydraulic Jack 5-Ton",
                    Description = "Heavy-duty hydraulic floor jack, 5-ton capacity",
                    Category = "Lifting Equipment",
                    Manufacturer = "Milwaukee",
                    Model = "HJ-5000",
                    SerialNumber = "SN-2024-002",
                    Status = "Available",
                    CurrentLocation = "Workshop - Section B",
                    HomeLocation = "Workshop - Section B",
                    PurchasePrice = 350.00m,
                    CurrentValue = 320.00m,
                    DailyRentalRate = 15.00m,
                    TotalUsageDays = 120,
                    LastUsedDate = DateTime.UtcNow.AddDays(-3),
                    RequiresCertification = true,
                    RequiresTraining = true,
                    SafetyNotes = "Inspect before use. Do not exceed weight capacity. Use jack stands for support.",
                    IsActive = true,
                    TenantId = tenantId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                },
                new MaintenanceTool
                {
                    Id = Guid.NewGuid(),
                    ToolCode = "TL-003",
                    Name = "Industrial Drill Press",
                    Description = "Variable speed industrial drill press with laser guide",
                    Category = "Power Tools",
                    Manufacturer = "DeWalt",
                    Model = "DWE1622K",
                    SerialNumber = "SN-2024-003",
                    Status = "Available",
                    CurrentLocation = "Machine Shop - Station 3",
                    HomeLocation = "Machine Shop - Station 3",
                    PurchasePrice = 1200.00m,
                    CurrentValue = 1100.00m,
                    DailyRentalRate = 40.00m,
                    TotalUsageDays = 230,
                    LastUsedDate = DateTime.UtcNow.AddDays(-1),
                    RequiresCertification = true,
                    RequiresTraining = true,
                    SafetyNotes = "Safety glasses mandatory. Secure workpiece before operation. Emergency stop accessible.",
                    IsActive = true,
                    TenantId = tenantId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                },
                new MaintenanceTool
                {
                    Id = Guid.NewGuid(),
                    ToolCode = "TL-004",
                    Name = "Multimeter Digital",
                    Description = "Professional digital multimeter with auto-ranging",
                    Category = "Diagnostic Equipment",
                    Manufacturer = "Fluke",
                    Model = "87V",
                    SerialNumber = "SN-2024-004",
                    Status = "Available",
                    CurrentLocation = "Electrical Workshop",
                    HomeLocation = "Electrical Workshop",
                    PurchasePrice = 400.00m,
                    CurrentValue = 380.00m,
                    DailyRentalRate = 20.00m,
                    TotalUsageDays = 180,
                    LastUsedDate = DateTime.UtcNow.AddDays(-5),
                    RequiresCertification = false,
                    RequiresTraining = true,
                    SafetyNotes = "Check leads before use. Do not measure voltages exceeding rating.",
                    LastCalibrationDate = DateTime.UtcNow.AddMonths(-6),
                    NextCalibrationDate = DateTime.UtcNow.AddMonths(6),
                    IsActive = true,
                    TenantId = tenantId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                },
                new MaintenanceTool
                {
                    Id = Guid.NewGuid(),
                    ToolCode = "TL-005",
                    Name = "Welding Machine MIG",
                    Description = "MIG welding machine with digital display, 200A output",
                    Category = "Welding Equipment",
                    Manufacturer = "Lincoln Electric",
                    Model = "PowerMIG 210",
                    SerialNumber = "SN-2024-005",
                    Status = "InUse",
                    CurrentLocation = "Fabrication Shop",
                    HomeLocation = "Fabrication Shop",
                    PurchasePrice = 2500.00m,
                    CurrentValue = 2300.00m,
                    DailyRentalRate = 75.00m,
                    TotalUsageDays = 350,
                    LastUsedDate = DateTime.UtcNow,
                    RequiresCertification = true,
                    RequiresTraining = true,
                    SafetyNotes = "Welding shield required. Adequate ventilation mandatory. Fire extinguisher nearby.",
                    IsActive = true,
                    TenantId = tenantId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                },
                new MaintenanceTool
                {
                    Id = Guid.NewGuid(),
                    ToolCode = "TL-006",
                    Name = "Angle Grinder 9-inch",
                    Description = "Heavy-duty angle grinder with variable speed control",
                    Category = "Power Tools",
                    Manufacturer = "Makita",
                    Model = "GA9040S",
                    SerialNumber = "SN-2024-006",
                    Status = "Available",
                    CurrentLocation = "Tool Room B - Shelf 2",
                    HomeLocation = "Tool Room B - Shelf 2",
                    PurchasePrice = 280.00m,
                    CurrentValue = 250.00m,
                    DailyRentalRate = 18.00m,
                    TotalUsageDays = 95,
                    LastUsedDate = DateTime.UtcNow.AddDays(-10),
                    RequiresCertification = false,
                    RequiresTraining = true,
                    SafetyNotes = "Safety glasses and gloves required. Check disc before use.",
                    IsActive = true,
                    TenantId = tenantId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                },
                new MaintenanceTool
                {
                    Id = Guid.NewGuid(),
                    ToolCode = "TL-007",
                    Name = "Thermal Imaging Camera",
                    Description = "Professional thermal camera for electrical inspections",
                    Category = "Diagnostic Equipment",
                    Manufacturer = "FLIR",
                    Model = "E60",
                    SerialNumber = "SN-2024-007",
                    Status = "Available",
                    CurrentLocation = "Inspection Equipment Room",
                    HomeLocation = "Inspection Equipment Room",
                    PurchasePrice = 3500.00m,
                    CurrentValue = 3300.00m,
                    DailyRentalRate = 100.00m,
                    TotalUsageDays = 55,
                    LastUsedDate = DateTime.UtcNow.AddDays(-14),
                    RequiresCertification = true,
                    RequiresTraining = true,
                    SafetyNotes = "Handle with care. Store in protective case when not in use.",
                    LastCalibrationDate = DateTime.UtcNow.AddMonths(-3),
                    NextCalibrationDate = DateTime.UtcNow.AddMonths(9),
                    IsActive = true,
                    TenantId = tenantId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                },
                new MaintenanceTool
                {
                    Id = Guid.NewGuid(),
                    ToolCode = "TL-008",
                    Name = "Pipe Threading Machine",
                    Description = "Portable pipe threading machine for 1/2\" to 2\" pipes",
                    Category = "Specialized Tools",
                    Manufacturer = "RIDGID",
                    Model = "300-T2",
                    SerialNumber = "SN-2024-008",
                    Status = "Maintenance",
                    CurrentLocation = "Repair Shop",
                    HomeLocation = "Plumbing Workshop",
                    PurchasePrice = 1800.00m,
                    CurrentValue = 1600.00m,
                    DailyRentalRate = 60.00m,
                    TotalUsageDays = 140,
                    LastUsedDate = DateTime.UtcNow.AddDays(-20),
                    RequiresCertification = true,
                    RequiresTraining = true,
                    SafetyNotes = "Machine guards must be in place. Keep hands clear of threading dies.",
                    LastMaintenanceDate = DateTime.UtcNow.AddDays(-2),
                    NextMaintenanceDate = DateTime.UtcNow.AddMonths(3),
                    IsActive = true,
                    TenantId = tenantId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                },
                new MaintenanceTool
                {
                    Id = Guid.NewGuid(),
                    ToolCode = "TL-009",
                    Name = "Air Compressor 30-Gallon",
                    Description = "Stationary air compressor, 30-gallon tank, 150 PSI max",
                    Category = "Power Equipment",
                    Manufacturer = "Ingersoll Rand",
                    Model = "SS3F2-GM",
                    SerialNumber = "SN-2024-009",
                    Status = "Available",
                    CurrentLocation = "Air Tool Station",
                    HomeLocation = "Air Tool Station",
                    PurchasePrice = 950.00m,
                    CurrentValue = 850.00m,
                    DailyRentalRate = 35.00m,
                    TotalUsageDays = 280,
                    LastUsedDate = DateTime.UtcNow.AddDays(-2),
                    RequiresCertification = false,
                    RequiresTraining = true,
                    SafetyNotes = "Drain condensate regularly. Do not exceed pressure rating.",
                    LastMaintenanceDate = DateTime.UtcNow.AddMonths(-1),
                    NextMaintenanceDate = DateTime.UtcNow.AddMonths(2),
                    IsActive = true,
                    TenantId = tenantId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                },
                new MaintenanceTool
                {
                    Id = Guid.NewGuid(),
                    ToolCode = "TL-010",
                    Name = "Precision Level Digital",
                    Description = "Digital precision level with 0.1-degree accuracy",
                    Category = "Measuring Tools",
                    Manufacturer = "Bosch",
                    Model = "DNM 120 L",
                    SerialNumber = "SN-2024-010",
                    Status = "Available",
                    CurrentLocation = "Calibration Lab",
                    HomeLocation = "Calibration Lab",
                    PurchasePrice = 320.00m,
                    CurrentValue = 300.00m,
                    DailyRentalRate = 12.00m,
                    TotalUsageDays = 65,
                    LastUsedDate = DateTime.UtcNow.AddDays(-8),
                    RequiresCertification = false,
                    RequiresTraining = false,
                    SafetyNotes = "Handle carefully to maintain calibration accuracy.",
                    LastCalibrationDate = DateTime.UtcNow.AddMonths(-4),
                    NextCalibrationDate = DateTime.UtcNow.AddMonths(8),
                    IsActive = true,
                    TenantId = tenantId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                }
            };

            await _context.MaintenanceTools.AddRangeAsync(tools);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Successfully seeded {tools.Count} tools");

            // Display summary
            var statusGroups = tools.GroupBy(t => t.Status).OrderBy(g => g.Key);
            _logger.LogInformation("Tool Status Summary:");
            foreach (var group in statusGroups)
            {
                _logger.LogInformation($"  {group.Key}: {group.Count()} tools");
            }

            _logger.LogInformation("Tool Management seeding completed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding Tool Management data");
            throw;
        }
    }
}
