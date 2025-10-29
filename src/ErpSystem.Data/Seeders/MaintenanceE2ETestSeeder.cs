using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds all master data required for end-to-end maintenance workflow testing
/// From Job Card creation → Approval → Work Order → Execution → Quality Check → Completion
/// </summary>
public class MaintenanceE2ETestSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MaintenanceE2ETestSeeder> _logger;
    
    // Master data IDs for easy reference
    private Guid _defaultTenantId;
    private Guid _vehicleCategoryId;
    private Guid _equipmentCategoryId;
    private Guid _preventiveMaintenanceTypeId;
    private Guid _correctiveMaintenanceTypeId;
    private Guid _emergencyMaintenanceTypeId;
    private Guid _vehicleAssetTypeId;
    private Guid _equipmentAssetTypeId;
    private Guid _highPriorityId;
    private Guid _mediumPriorityId;
    private Guid _lowPriorityId;
    private Guid _preventiveWorkOrderTypeId;
    private Guid _correctiveWorkOrderTypeId;
    
    // User/Employee IDs
    private Guid _fleetManagerId;
    private Guid _maintenanceSupervisorId;
    private Guid _maintenanceManagerId;
    private Guid _seniorTechnicianId;
    private Guid _juniorTechnicianId;
    private Guid _qualityInspectorId;
    private Guid _serviceAdvisorId;
    
    // Asset IDs
    private Guid _deliveryTruckId;
    private Guid _forkliftId;
    private Guid _generatorId;
    
    // Inventory Part IDs
    private Guid _engineOilPartId;
    private Guid _oilFilterPartId;
    private Guid _airFilterPartId;
    private Guid _brakeFluidPartId;
    private Guid _sparkPlugPartId;

    public MaintenanceE2ETestSeeder(ApplicationDbContext context, ILogger<MaintenanceE2ETestSeeder> logger)
    {
        _context = context;
        _logger = logger;
        InitializeIds();
    }

    private void InitializeIds()
    {
        // Use predictable GUIDs for easy reference
        _defaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        
        // Categories
        _vehicleCategoryId = Guid.Parse("CAT00000-0000-0000-0000-000000000001");
        _equipmentCategoryId = Guid.Parse("CAT00000-0000-0000-0000-000000000002");
        
        // Maintenance Types
        _preventiveMaintenanceTypeId = Guid.Parse("MT000000-0000-0000-0000-000000000001");
        _correctiveMaintenanceTypeId = Guid.Parse("MT000000-0000-0000-0000-000000000002");
        _emergencyMaintenanceTypeId = Guid.Parse("MT000000-0000-0000-0000-000000000003");
        
        // Asset Types
        _vehicleAssetTypeId = Guid.Parse("AT000000-0000-0000-0000-000000000001");
        _equipmentAssetTypeId = Guid.Parse("AT000000-0000-0000-0000-000000000002");
        
        // Priority Levels
        _highPriorityId = Guid.Parse("PR000000-0000-0000-0000-000000000001");
        _mediumPriorityId = Guid.Parse("PR000000-0000-0000-0000-000000000002");
        _lowPriorityId = Guid.Parse("PR000000-0000-0000-0000-000000000003");
        
        // Work Order Types
        _preventiveWorkOrderTypeId = Guid.Parse("WOT00000-0000-0000-0000-000000000001");
        _correctiveWorkOrderTypeId = Guid.Parse("WOT00000-0000-0000-0000-000000000002");
        
        // Employees (will be created or linked to existing)
        _fleetManagerId = Guid.Parse("EMP00000-0000-0000-0000-000000000001");
        _maintenanceSupervisorId = Guid.Parse("EMP00000-0000-0000-0000-000000000002");
        _maintenanceManagerId = Guid.Parse("EMP00000-0000-0000-0000-000000000003");
        _seniorTechnicianId = Guid.Parse("EMP00000-0000-0000-0000-000000000004");
        _juniorTechnicianId = Guid.Parse("EMP00000-0000-0000-0000-000000000005");
        _qualityInspectorId = Guid.Parse("EMP00000-0000-0000-0000-000000000006");
        _serviceAdvisorId = Guid.Parse("EMP00000-0000-0000-0000-000000000007");
        
        // Assets
        _deliveryTruckId = Guid.Parse("AST00000-0000-0000-0000-000000000001");
        _forkliftId = Guid.Parse("AST00000-0000-0000-0000-000000000002");
        _generatorId = Guid.Parse("AST00000-0000-0000-0000-000000000003");
        
        // Inventory Parts
        _engineOilPartId = Guid.Parse("PART0000-0000-0000-0000-000000000001");
        _oilFilterPartId = Guid.Parse("PART0000-0000-0000-0000-000000000002");
        _airFilterPartId = Guid.Parse("PART0000-0000-0000-0000-000000000003");
        _brakeFluidPartId = Guid.Parse("PART0000-0000-0000-0000-000000000004");
        _sparkPlugPartId = Guid.Parse("PART0000-0000-0000-0000-000000000005");
    }

    public async Task SeedAsync()
    {
        try
        {
            _logger.LogInformation("Starting E2E Maintenance Test Data Seeding...");
            
            var baseDate = DateTime.UtcNow;

            // 1. HR Employees (Technicians, Inspectors, Managers)
            await SeedHREmployeesAsync(baseDate);
            await _context.SaveChangesAsync();
            
            // 2. Inventory Parts
            await SeedInventoryPartsAsync(baseDate);
            await _context.SaveChangesAsync();
            
            // 3. Asset Categories
            await SeedAssetCategoriesAsync(baseDate);
            await _context.SaveChangesAsync();
            
            // 4. Asset Types
            await SeedAssetTypesAsync(baseDate);
            await _context.SaveChangesAsync();
            
            // 5. Maintenance Assets
            await SeedMaintenanceAssetsAsync(baseDate);
            await _context.SaveChangesAsync();
            
            // 6. Maintenance Types
            await SeedMaintenanceTypesAsync(baseDate);
            await _context.SaveChangesAsync();
            
            // 7. Priority Levels
            await SeedPriorityLevelsAsync(baseDate);
            await _context.SaveChangesAsync();
            
            // 8. Work Order Types
            await SeedWorkOrderTypesAsync(baseDate);
            await _context.SaveChangesAsync();
            
            // 9. Task Templates
            await SeedTaskTemplatesAsync(baseDate);
            await _context.SaveChangesAsync();
            
            // 10. Quality Checklists
            await SeedQualityChecklistsAsync(baseDate);
            await _context.SaveChangesAsync();
            
            // 11. Inspection Templates
            await SeedInspectionTemplatesAsync(baseDate);
            await _context.SaveChangesAsync();
            
            // 12. Technician Records (link employees to maintenance)
            await SeedTechniciansAsync(baseDate);
            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ E2E Maintenance Test Data Seeding Completed Successfully!");
            LogSeedingSummary();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error occurred during E2E maintenance test data seeding");
            throw;
        }
    }

    private async Task SeedHREmployeesAsync(DateTime baseDate)
    {
        _logger.LogInformation("Seeding HR Employees...");

        // First, ensure we have departments and positions
        var maintenanceDeptId = Guid.Parse("DEPT0000-0000-0000-0000-000000000001");
        var fleetDeptId = Guid.Parse("DEPT0000-0000-0000-0000-000000000002");
        var qcDeptId = Guid.Parse("DEPT0000-0000-0000-0000-000000000003");
        
        var managerPosId = Guid.Parse("POS00000-0000-0000-0000-000000000001");
        var supervisorPosId = Guid.Parse("POS00000-0000-0000-0000-000000000002");
        var technicianPosId = Guid.Parse("POS00000-0000-0000-0000-000000000003");
        var inspectorPosId = Guid.Parse("POS00000-0000-0000-0000-000000000004");

        // Seed Departments
        if (!await _context.Departments.AnyAsync(d => d.Id == maintenanceDeptId))
        {
            await _context.Departments.AddAsync(new Department
            {
                Id = maintenanceDeptId,
                Name = "Maintenance",
                Code = "MAINT",
                DepartmentType = DepartmentType.Maintenance,
                IsActive = true,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }
        
        if (!await _context.Departments.AnyAsync(d => d.Id == fleetDeptId))
        {
            await _context.Departments.AddAsync(new Department
            {
                Id = fleetDeptId,
                Name = "Fleet Operations",
                Code = "FLEET",
                DepartmentType = DepartmentType.Operations,
                IsActive = true,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }
        
        if (!await _context.Departments.AnyAsync(d => d.Id == qcDeptId))
        {
            await _context.Departments.AddAsync(new Department
            {
                Id = qcDeptId,
                Name = "Quality Control",
                Code = "QC",
                DepartmentType = DepartmentType.QualityAssurance,
                IsActive = true,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }
        
        await _context.SaveChangesAsync();

        // Seed Positions
        if (!await _context.EmployeePositions.AnyAsync(p => p.Id == managerPosId))
        {
            await _context.EmployeePositions.AddAsync(new EmployeePosition
            {
                Id = managerPosId,
                Title = "Manager",
                Code = "MGR",
                DepartmentId = maintenanceDeptId,
                Level = 5,
                IsActive = true,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }
        
        if (!await _context.EmployeePositions.AnyAsync(p => p.Id == supervisorPosId))
        {
            await _context.EmployeePositions.AddAsync(new EmployeePosition
            {
                Id = supervisorPosId,
                Title = "Supervisor",
                Code = "SUP",
                DepartmentId = maintenanceDeptId,
                Level = 4,
                IsActive = true,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }
        
        if (!await _context.EmployeePositions.AnyAsync(p => p.Id == technicianPosId))
        {
            await _context.EmployeePositions.AddAsync(new EmployeePosition
            {
                Id = technicianPosId,
                Title = "Technician",
                Code = "TECH",
                DepartmentId = maintenanceDeptId,
                Level = 2,
                IsActive = true,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }
        
        if (!await _context.EmployeePositions.AnyAsync(p => p.Id == inspectorPosId))
        {
            await _context.EmployeePositions.AddAsync(new EmployeePosition
            {
                Id = inspectorPosId,
                Title = "Inspector",
                Code = "INSP",
                DepartmentId = qcDeptId,
                Level = 3,
                IsActive = true,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }
        
        await _context.SaveChangesAsync();

        var employees = new[]
        {
            new { Id = _fleetManagerId, Number = "EMP001", FirstName = "John", LastName = "Fleet", Email = "john.fleet@company.com", DeptId = fleetDeptId, PosId = managerPosId },
            new { Id = _maintenanceSupervisorId, Number = "EMP002", FirstName = "Sarah", LastName = "Supervisor", Email = "sarah.supervisor@company.com", DeptId = maintenanceDeptId, PosId = supervisorPosId },
            new { Id = _maintenanceManagerId, Number = "EMP003", FirstName = "Michael", LastName = "Manager", Email = "michael.manager@company.com", DeptId = maintenanceDeptId, PosId = managerPosId },
            new { Id = _seniorTechnicianId, Number = "EMP004", FirstName = "David", LastName = "Senior", Email = "david.senior@company.com", DeptId = maintenanceDeptId, PosId = technicianPosId },
            new { Id = _juniorTechnicianId, Number = "EMP005", FirstName = "Emily", LastName = "Junior", Email = "emily.junior@company.com", DeptId = maintenanceDeptId, PosId = technicianPosId },
            new { Id = _qualityInspectorId, Number = "EMP006", FirstName = "Robert", LastName = "Inspector", Email = "robert.inspector@company.com", DeptId = qcDeptId, PosId = inspectorPosId },
            new { Id = _serviceAdvisorId, Number = "EMP007", FirstName = "Linda", LastName = "Advisor", Email = "linda.advisor@company.com", DeptId = maintenanceDeptId, PosId = technicianPosId }
        };

        foreach (var emp in employees)
        {
            if (!await _context.Employees.AnyAsync(e => e.Id == emp.Id))
            {
                await _context.Employees.AddAsync(new Employee
                {
                    Id = emp.Id,
                    EmployeeNumber = emp.Number,
                    FirstName = emp.FirstName,
                    LastName = emp.LastName,
                    EmailAddress = emp.Email,
                    MobileNumber = $"+123456700{emp.Number.Substring(emp.Number.Length - 1)}",
                    DepartmentId = emp.DeptId,
                    PositionId = emp.PosId,
                    DateEmployed = DateOnly.FromDateTime(baseDate.AddYears(-5)),
                    StaffStatus = StaffStatus.Active,
                    IsActive = true,
                    TenantId = _defaultTenantId,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                });
            }
        }

        _logger.LogInformation($"✓ Seeded {employees.Length} HR employees");
    }

    private async Task SeedInventoryPartsAsync(DateTime baseDate)
    {
        _logger.LogInformation("Seeding Inventory Parts...");

        // First create inventory categories
        var lubricantsCategoryId = Guid.Parse("INVC0000-0000-0000-0000-000000000001");
        var filtersCategoryId = Guid.Parse("INVC0000-0000-0000-0000-000000000002");
        var fluidsCategoryId = Guid.Parse("INVC0000-0000-0000-0000-000000000003");
        var ignitionCategoryId = Guid.Parse("INVC0000-0000-0000-0000-000000000004");

        if (!await _context.InventoryCategories.AnyAsync(c => c.Id == lubricantsCategoryId))
        {
            await _context.InventoryCategories.AddAsync(new InventoryCategory
            {
                Id = lubricantsCategoryId,
                Name = "Lubricants",
                Code = "LUB",
                IsActive = true,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }
        
        if (!await _context.InventoryCategories.AnyAsync(c => c.Id == filtersCategoryId))
        {
            await _context.InventoryCategories.AddAsync(new InventoryCategory
            {
                Id = filtersCategoryId,
                Name = "Filters",
                Code = "FILT",
                IsActive = true,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }
        
        if (!await _context.InventoryCategories.AnyAsync(c => c.Id == fluidsCategoryId))
        {
            await _context.InventoryCategories.AddAsync(new InventoryCategory
            {
                Id = fluidsCategoryId,
                Name = "Fluids",
                Code = "FLD",
                IsActive = true,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }
        
        if (!await _context.InventoryCategories.AnyAsync(c => c.Id == ignitionCategoryId))
        {
            await _context.InventoryCategories.AddAsync(new InventoryCategory
            {
                Id = ignitionCategoryId,
                Name = "Ignition",
                Code = "IGN",
                IsActive = true,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }
        
        await _context.SaveChangesAsync();

        var parts = new[]
        {
            new { Id = _engineOilPartId, Code = "OIL-5W30-5L", Name = "Engine Oil 5W-30 (5 Liters)", CategoryId = lubricantsCategoryId, Unit = "L", Price = 45.00m, Qty = 100m },
            new { Id = _oilFilterPartId, Code = "FILTER-OIL-001", Name = "Oil Filter - Heavy Duty", CategoryId = filtersCategoryId, Unit = "EA", Price = 25.00m, Qty = 50m },
            new { Id = _airFilterPartId, Code = "FILTER-AIR-001", Name = "Air Filter - Heavy Duty", CategoryId = filtersCategoryId, Unit = "EA", Price = 35.00m, Qty = 40m },
            new { Id = _brakeFluidPartId, Code = "FLUID-BRAKE-DOT4", Name = "Brake Fluid DOT 4 (1 Liter)", CategoryId = fluidsCategoryId, Unit = "L", Price = 15.00m, Qty = 60m },
            new { Id = _sparkPlugPartId, Code = "SPARK-PLUG-001", Name = "Spark Plug - Iridium", CategoryId = ignitionCategoryId, Unit = "EA", Price = 12.00m, Qty = 80m }
        };

        foreach (var part in parts)
        {
            if (!await _context.InventoryItems.AnyAsync(i => i.Id == part.Id))
            {
                await _context.InventoryItems.AddAsync(new InventoryItem
                {
                    Id = part.Id,
                    ItemCode = part.Code,
                    Name = part.Name,
                    CategoryId = part.CategoryId,
                    UnitOfMeasure = part.Unit,
                    SalePrice = part.Price,
                    StandardCost = part.Price * 0.7m,
                    CurrentStock = part.Qty,
                    AvailableStock = part.Qty,
                    ReorderLevel = part.Qty / 5,
                    Status = ItemStatus.Active,
                    TenantId = _defaultTenantId,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                });
            }
        }

        _logger.LogInformation($"✓ Seeded {parts.Length} inventory parts");
    }

    private async Task SeedAssetCategoriesAsync(DateTime baseDate)
    {
        _logger.LogInformation("Seeding Asset Categories...");

        var categories = new[]
        {
            new { Id = _vehicleCategoryId, Name = "Vehicles", Code = "VEH", Type = "Vehicle" },
            new { Id = _equipmentCategoryId, Name = "Heavy Equipment", Code = "HEQ", Type = "Equipment" }
        };

        foreach (var cat in categories)
        {
            if (!await _context.MaintenanceAssetCategories.AnyAsync(c => c.Id == cat.Id))
            {
                await _context.MaintenanceAssetCategories.AddAsync(new MaintenanceAssetCategory
                {
                    Id = cat.Id,
                    Name = cat.Name,
                    Code = cat.Code,
                    AssetType = cat.Type,
                    IsActive = true,
                    TenantId = _defaultTenantId,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                });
            }
        }

        _logger.LogInformation($"✓ Seeded {categories.Length} asset categories");
    }

    private async Task SeedAssetTypesAsync(DateTime baseDate)
    {
        _logger.LogInformation("Seeding Asset Types...");

        var types = new[]
        {
            new { Id = _vehicleAssetTypeId, Name = "Delivery Truck", Code = "TRK" },
            new { Id = _equipmentAssetTypeId, Name = "Forklift", Code = "FRK" }
        };

        foreach (var type in types)
        {
            if (!await _context.AssetTypes.AnyAsync(a => a.Id == type.Id))
            {
                await _context.AssetTypes.AddAsync(new AssetType
                {
                    Id = type.Id,
                    Name = type.Name,
                    Code = type.Code,
                    RequiresMileageTracking = true,
                    RequiresPreventiveMaintenance = true,
                    DefaultMaintenanceIntervalDays = 90,
                    IsActive = true,
                    TenantId = _defaultTenantId,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                });
            }
        }

        _logger.LogInformation($"✓ Seeded {types.Length} asset types");
    }

    private async Task SeedMaintenanceAssetsAsync(DateTime baseDate)
    {
        _logger.LogInformation("Seeding Maintenance Assets...");

        if (!await _context.MaintenanceAssets.AnyAsync(a => a.Id == _deliveryTruckId))
        {
            await _context.MaintenanceAssets.AddAsync(new MaintenanceAsset
            {
                Id = _deliveryTruckId,
                AssetNumber = "TRK-001",
                Name = "Delivery Truck - Isuzu NPR 75",
                AssetCategoryId = _vehicleCategoryId,
                SerialNumber = "ISU-NPR-2020-001",
                Manufacturer = "Isuzu",
                Model = "NPR 75",
                PurchaseDate = baseDate.AddYears(-4),
                PurchasePrice = 45000.00m,
                CurrentValue = 30000.00m,
                Status = AssetStatus.Active,
                Criticality = AssetCriticality.High,
                Location = "Fleet Parking - Bay 3",
                Mileage = 85000,
                LastMileageUpdate = baseDate.AddDays(-1),
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        _logger.LogInformation("✓ Seeded maintenance assets");
    }

    private async Task SeedMaintenanceTypesAsync(DateTime baseDate)
    {
        _logger.LogInformation("Seeding Maintenance Types...");

        if (!await _context.MaintenanceTypes.AnyAsync(m => m.Id == _preventiveMaintenanceTypeId))
        {
            await _context.MaintenanceTypes.AddAsync(new MaintenanceType
            {
                Id = _preventiveMaintenanceTypeId,
                Name = "Preventive Maintenance",
                Code = "PM",
                Category = "Preventive",
                MaintenanceClass = "Routine",
                Location = "Internal",
                DefaultPriority = 3,
                EstimatedHours = 4,
                EstimatedCost = 500.00m,
                RequiresApproval = true,
                IsActive = true,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        _logger.LogInformation("✓ Seeded maintenance types");
    }

    private async Task SeedPriorityLevelsAsync(DateTime baseDate)
    {
        _logger.LogInformation("Seeding Priority Levels...");

        var priorities = new[]
        {
            new { Id = _highPriorityId, Name = "High", Level = 1, Color = "#FF0000", Response = 4 },
            new { Id = _mediumPriorityId, Name = "Medium", Level = 2, Color = "#FFA500", Response = 24 },
            new { Id = _lowPriorityId, Name = "Low", Level = 3, Color = "#00FF00", Response = 72 }
        };

        foreach (var priority in priorities)
        {
            if (!await _context.PriorityLevels.AnyAsync(p => p.Id == priority.Id))
            {
                await _context.PriorityLevels.AddAsync(new PriorityLevel
                {
                    Id = priority.Id,
                    Name = priority.Name,
                    Level = priority.Level,
                    Color = priority.Color,
                    ResponseTimeHours = priority.Response,
                    IsActive = true,
                    TenantId = _defaultTenantId,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                });
            }
        }

        _logger.LogInformation($"✓ Seeded {priorities.Length} priority levels");
    }

    private async Task SeedWorkOrderTypesAsync(DateTime baseDate)
    {
        _logger.LogInformation("Seeding Work Order Types...");

        if (!await _context.WorkOrderTypes.AnyAsync(w => w.Id == _preventiveWorkOrderTypeId))
        {
            await _context.WorkOrderTypes.AddAsync(new WorkOrderType
            {
                Id = _preventiveWorkOrderTypeId,
                Name = "Preventive Maintenance",
                Code = "PM-WO",
                DefaultPriority = 3,
                RequiresApproval = true,
                IsActive = true,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        _logger.LogInformation("✓ Seeded work order types");
    }

    private async Task SeedTaskTemplatesAsync(DateTime baseDate)
    {
        _logger.LogInformation("Seeding Task Templates...");
        
        var templates = new[]
        {
            new { Name = "Engine Oil Change", Seq = 1, Duration = 0.5 },
            new { Name = "Oil Filter Replacement", Seq = 2, Duration = 0.25 },
            new { Name = "Air Filter Inspection", Seq = 3, Duration = 0.33 },
            new { Name = "Brake System Inspection", Seq = 4, Duration = 1.0 },
            new { Name = "Tire Rotation", Seq = 5, Duration = 0.75 },
            new { Name = "General Visual Inspection", Seq = 6, Duration = 0.5 }
        };

        foreach (var tmpl in templates)
        {
            var exists = await _context.MaintenanceTaskTemplates.AnyAsync(t => 
                t.TaskName == tmpl.Name && t.MaintenanceTypeId == _preventiveMaintenanceTypeId);
                
            if (!exists)
            {
                await _context.MaintenanceTaskTemplates.AddAsync(new MaintenanceTaskTemplate
                {
                    Id = Guid.NewGuid(),
                    TaskName = tmpl.Name,
                    MaintenanceTypeId = _preventiveMaintenanceTypeId,
                    Sequence = tmpl.Seq,
                    EstimatedHours = tmpl.Duration,
                    IsRequired = true,
                    IsActive = true,
                    TenantId = _defaultTenantId,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                });
            }
        }

        _logger.LogInformation($"✓ Seeded {templates.Length} task templates");
    }

    private async Task SeedQualityChecklistsAsync(DateTime baseDate)
    {
        _logger.LogInformation("Seeding Quality Checklists...");

        var checklistId = Guid.NewGuid();
        if (!await _context.QualityControlChecklists.AnyAsync(q => q.Name == "PM Quality Checklist"))
        {
            await _context.QualityControlChecklists.AddAsync(new QualityControlChecklist
            {
                Id = checklistId,
                Name = "PM Quality Checklist",
                WorkOrderType = "Preventive Maintenance",
                AssetCategory = "Vehicle",
                IsMandatory = true,
                IsActive = true,
                MinimumPassingScore = 85,
                ChecklistItems = "[]",
                TenantId = _defaultTenantId,
                CreatedDate = baseDate,
                CreatedById = _qualityInspectorId
            });
        }

        _logger.LogInformation("✓ Seeded quality checklists");
    }

    private async Task SeedInspectionTemplatesAsync(DateTime baseDate)
    {
        _logger.LogInformation("Seeding Inspection Templates...");

        var templateId = Guid.NewGuid();
        if (!await _context.InspectionChecklistTemplates.AnyAsync(t => t.Name == "Vehicle PM Inspection"))
        {
            await _context.InspectionChecklistTemplates.AddAsync(new InspectionChecklistTemplate
            {
                Id = templateId,
                Name = "Vehicle PM Inspection",
                Category = "Quality",
                IsActive = true,
                IsDefault = true,
                Version = 1,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        _logger.LogInformation("✓ Seeded inspection templates");
    }

    private async Task SeedTechniciansAsync(DateTime baseDate)
    {
        _logger.LogInformation("Seeding Technician records...");

        var technicians = new[]
        {
            new { 
                EmployeeId = _seniorTechnicianId, 
                Number = "EMP004",
                FirstName = "David", 
                LastName = "Senior",
                Email = "david.senior@company.com",
                Phone = "+1234567004",
                Dept = "Maintenance",
                Position = "Senior Technician",
                Specialization = "Diesel Mechanics", 
                CertLevel = "Level 3",
                ExpLevel = "Senior"
            },
            new { 
                EmployeeId = _juniorTechnicianId, 
                Number = "EMP005",
                FirstName = "Emily", 
                LastName = "Junior",
                Email = "emily.junior@company.com",
                Phone = "+1234567005",
                Dept = "Maintenance",
                Position = "Junior Technician",
                Specialization = "General Mechanics", 
                CertLevel = "Level 1",
                ExpLevel = "Junior"
            }
        };

        foreach (var tech in technicians)
        {
            if (!await _context.Technicians.AnyAsync(t => t.EmployeeId == tech.EmployeeId))
            {
                await _context.Technicians.AddAsync(new Technician
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = tech.EmployeeId,
                    EmployeeNumber = tech.Number,
                    FirstName = tech.FirstName,
                    LastName = tech.LastName,
                    Email = tech.Email,
                    Phone = tech.Phone,
                    Department = tech.Dept,
                    Position = tech.Position,
                    Specialization = tech.Specialization,
                    CertificationLevel = tech.CertLevel,
                    ExperienceLevel = tech.ExpLevel,
                    HireDate = baseDate.AddYears(-5),
                    IsActive = true,
                    CurrentWorkload = 0,
                    MaxWorkload = 100,
                    TenantId = _defaultTenantId,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                });
            }
        }

        _logger.LogInformation($"✓ Seeded {technicians.Length} technician records");
    }

    private void LogSeedingSummary()
    {
        _logger.LogInformation("\n" +
            "╔══════════════════════════════════════════════════════╗\n" +
            "║     E2E MAINTENANCE TEST DATA SEEDING SUMMARY       ║\n" +
            "╠══════════════════════════════════════════════════════╣\n" +
            "║ ✓ 7 HR Employees                                    ║\n" +
            "║ ✓ 5 Inventory Parts                                 ║\n" +
            "║ ✓ 2 Asset Categories                                ║\n" +
            "║ ✓ 2 Asset Types                                     ║\n" +
            "║ ✓ 1 Maintenance Asset (Truck)                       ║\n" +
            "║ ✓ 1 Maintenance Type (Preventive)                   ║\n" +
            "║ ✓ 3 Priority Levels                                 ║\n" +
            "║ ✓ 1 Work Order Type                                 ║\n" +
            "║ ✓ 6 Task Templates                                  ║\n" +
            "║ ✓ 1 Quality Checklist                               ║\n" +
            "║ ✓ 1 Inspection Template                             ║\n" +
            "║ ✓ 2 Technician Records                              ║\n" +
            "╠══════════════════════════════════════════════════════╣\n" +
            "║ 🎯 READY FOR E2E WORKFLOW TESTING!                  ║\n" +
            "╚══════════════════════════════════════════════════════╝");
    }
}
