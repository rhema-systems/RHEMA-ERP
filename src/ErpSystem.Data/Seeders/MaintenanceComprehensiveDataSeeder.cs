using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds comprehensive maintenance data including inventory items, assets, task templates, checklists
/// </summary>
public class MaintenanceComprehensiveDataSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MaintenanceComprehensiveDataSeeder> _logger;

    public MaintenanceComprehensiveDataSeeder(ApplicationDbContext context, ILogger<MaintenanceComprehensiveDataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            _logger.LogInformation("Starting comprehensive maintenance data seeding...");

            // Get default tenant
            var defaultTenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT");
            if (defaultTenant == null)
            {
                _logger.LogError("Default tenant not found. Cannot seed comprehensive maintenance data.");
                return;
            }

            // Seed in order of dependencies
            await SeedInventoryCategoriesAsync(defaultTenant.Id);
            await SeedInventoryItemsAsync(defaultTenant.Id);
            await SeedMaintenanceAssetsAsync(defaultTenant.Id);
            await SeedMaintenanceTaskTemplatesAsync(defaultTenant.Id);
            await SeedAssetTaskTemplatesAsync(defaultTenant.Id);
            await SeedAssetTypeTaskTemplatesAsync(defaultTenant.Id);
            await SeedQualityControlChecklistsAsync(defaultTenant.Id);
            await SeedInspectionTemplatesAsync(defaultTenant.Id);

            await _context.SaveChangesAsync();
            _logger.LogInformation("Comprehensive maintenance data seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during comprehensive maintenance data seeding");
            throw;
        }
    }

    private async Task SeedInventoryCategoriesAsync(Guid tenantId)
    {
        _logger.LogInformation("Seeding inventory categories...");

        var categories = new[]
        {
            new { Code = "ELEC-PARTS", Name = "Electrical Parts", Description = "Electrical components and parts" },
            new { Code = "MECH-PARTS", Name = "Mechanical Parts", Description = "Mechanical components and parts" },
            new { Code = "HVAC-PARTS", Name = "HVAC Parts", Description = "Heating, ventilation, and air conditioning parts" },
            new { Code = "PLUMB-PARTS", Name = "Plumbing Parts", Description = "Plumbing components and parts" },
            new { Code = "TOOLS", Name = "Tools", Description = "Maintenance and repair tools" },
            new { Code = "SUPPLIES", Name = "Supplies", Description = "General maintenance supplies" },
            new { Code = "LUBRICANTS", Name = "Lubricants", Description = "Oils, greases, and lubricants" },
            new { Code = "FILTERS", Name = "Filters", Description = "Air, oil, and fuel filters" }
        };

        foreach (var cat in categories)
        {
            var exists = await _context.InventoryCategories.AnyAsync(c => c.TenantId == tenantId && c.Code == cat.Code);
            if (!exists)
            {
                var category = new InventoryCategory
                {
                    TenantId = tenantId,
                    Code = cat.Code,
                    Name = cat.Name,
                    Description = cat.Description,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.InventoryCategories.Add(category);
                _logger.LogInformation("Seeded inventory category: {Name}", cat.Name);
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedInventoryItemsAsync(Guid tenantId)
    {
        _logger.LogInformation("Seeding inventory items...");

        // Get categories
        var elecCategory = await _context.InventoryCategories.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Code == "ELEC-PARTS");
        var mechCategory = await _context.InventoryCategories.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Code == "MECH-PARTS");
        var hvacCategory = await _context.InventoryCategories.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Code == "HVAC-PARTS");
        var lubCategory = await _context.InventoryCategories.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Code == "LUBRICANTS");
        var filterCategory = await _context.InventoryCategories.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Code == "FILTERS");

        if (elecCategory == null)
        {
            return;
        }

        var items = new[]
        {
            new { Code = "FUSE-20A", Name = "20A Fuse", Description = "20 Ampere fuse", CategoryId = elecCategory.Id, UOM = "EA", Cost = 5.50m, Price = 8.50m, Stock = 50m, Min = 10m, Reorder = 20m },
            new { Code = "RELAY-12V", Name = "12V Relay", Description = "12 Volt relay switch", CategoryId = elecCategory.Id, UOM = "EA", Cost = 12.00m, Price = 18.00m, Stock = 30m, Min = 5m, Reorder = 15m },
            new { Code = "BEARING-6205", Name = "Ball Bearing 6205", Description = "Standard ball bearing 6205", CategoryId = mechCategory?.Id ?? elecCategory.Id, UOM = "EA", Cost = 15.50m, Price = 25.00m, Stock = 40m, Min = 8m, Reorder = 20m },
            new { Code = "BELT-A52", Name = "V-Belt A52", Description = "V-belt type A, 52 inches", CategoryId = mechCategory?.Id ?? elecCategory.Id, UOM = "EA", Cost = 22.00m, Price = 35.00m, Stock = 15m, Min = 3m, Reorder = 10m },
            new { Code = "FILTER-AIR-001", Name = "Air Filter Standard", Description = "Standard HVAC air filter", CategoryId = filterCategory?.Id ?? elecCategory.Id, UOM = "EA", Cost = 18.00m, Price = 28.00m, Stock = 25m, Min = 5m, Reorder = 15m },
            new { Code = "OIL-15W40", Name = "Engine Oil 15W-40", Description = "15W-40 engine oil", CategoryId = lubCategory?.Id ?? elecCategory.Id, UOM = "L", Cost = 8.50m, Price = 14.00m, Stock = 200m, Min = 50m, Reorder = 100m },
            new { Code = "GREASE-MULTI", Name = "Multi-Purpose Grease", Description = "General purpose grease", CategoryId = lubCategory?.Id ?? elecCategory.Id, UOM = "KG", Cost = 12.00m, Price = 20.00m, Stock = 80m, Min = 20m, Reorder = 50m },
            new { Code = "COOLANT-001", Name = "HVAC Refrigerant R410A", Description = "R410A refrigerant", CategoryId = hvacCategory?.Id ?? elecCategory.Id, UOM = "KG", Cost = 45.00m, Price = 75.00m, Stock = 30m, Min = 10m, Reorder = 20m }
        };

        foreach (var item in items)
        {
            var exists = await _context.InventoryItems.AnyAsync(i => i.TenantId == tenantId && i.ItemCode == item.Code);
            if (!exists)
            {
                var inventoryItem = new InventoryItem
                {
                    TenantId = tenantId,
                    ItemCode = item.Code,
                    Name = item.Name,
                    Description = item.Description,
                    CategoryId = item.CategoryId,
                    UnitOfMeasure = item.UOM,
                    StandardCost = item.Cost,
                    AverageCost = item.Cost,
                    LastPurchaseCost = item.Cost,
                    SalePrice = item.Price,
                    CurrentStock = item.Stock,
                    AvailableStock = item.Stock,
                    MinimumLevel = item.Min,
                    ReorderLevel = item.Reorder,
                    ReorderQuantity = item.Reorder * 2,
                    ItemType = ItemType.StockItem,
                    Status = ItemStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.InventoryItems.Add(inventoryItem);
                _logger.LogInformation("Seeded inventory item: {Name}", item.Name);
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedMaintenanceAssetsAsync(Guid tenantId)
    {
        _logger.LogInformation("Seeding maintenance assets...");

        // Get asset categories
        var hvacCategory = await _context.MaintenanceAssetCategories.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Code == "HVAC");
        var powerCategory = await _context.MaintenanceAssetCategories.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Code == "POWER");
        var elecCategory = await _context.MaintenanceAssetCategories.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Code == "ELEC");

        if (hvacCategory == null)
        {
            return;
        }

        // Get maintenance technician (if exists)
        var techEmployee = await _context.Employees
            .Where(e => e.TenantId == tenantId && e.IsDeleted == false && e.Department != null && e.Department.Name.Contains("Maintenance"))
            .FirstOrDefaultAsync();

        var assets = new[]
        {
            new {
                Number = "HVAC-001", Name = "Main HVAC Unit - Building A", Description = "Primary HVAC system for Building A",
                CategoryId = hvacCategory.Id, Manufacturer = "Carrier", Model = "50TCQ-480", SerialNo = "CAR-2023-001",
                Location = "Building A - Roof", Status = AssetStatus.Active, Criticality = AssetCriticality.High
            },
            new {
                Number = "HVAC-002", Name = "Chiller Unit 1", Description = "Central chiller for cooling system",
                CategoryId = hvacCategory.Id, Manufacturer = "Trane", Model = "CGAM100", SerialNo = "TRA-2022-045",
                Location = "Mechanical Room 1", Status = AssetStatus.Active, Criticality = AssetCriticality.Critical
            },
            new {
                Number = "GEN-001", Name = "Backup Generator", Description = "Emergency diesel generator",
                CategoryId = powerCategory?.Id ?? hvacCategory.Id, Manufacturer = "Caterpillar", Model = "C18", SerialNo = "CAT-2021-789",
                Location = "Generator Room", Status = AssetStatus.Active, Criticality = AssetCriticality.Critical
            },
            new {
                Number = "ELEC-001", Name = "Main Electrical Panel", Description = "Primary distribution panel",
                CategoryId = elecCategory?.Id ?? hvacCategory.Id, Manufacturer = "Schneider Electric", Model = "NF630", SerialNo = "SCH-2020-123",
                Location = "Electrical Room", Status = AssetStatus.Active, Criticality = AssetCriticality.Critical
            },
            new {
                Number = "HVAC-003", Name = "Air Handler Unit - Floor 2", Description = "Air handling unit for second floor",
                CategoryId = hvacCategory.Id, Manufacturer = "York", Model = "YCAV0090", SerialNo = "YRK-2023-567",
                Location = "Building A - Floor 2", Status = AssetStatus.Active, Criticality = AssetCriticality.Medium
            }
        };

        foreach (var asset in assets)
        {
            var exists = await _context.MaintenanceAssets.AnyAsync(a => a.TenantId == tenantId && a.AssetNumber == asset.Number);
            if (!exists)
            {
                var maintenanceAsset = new MaintenanceAsset
                {
                    TenantId = tenantId,
                    AssetNumber = asset.Number,
                    Name = asset.Name,
                    Description = asset.Description,
                    AssetCategoryId = asset.CategoryId,
                    Manufacturer = asset.Manufacturer,
                    Model = asset.Model,
                    SerialNumber = asset.SerialNo,
                    Location = asset.Location,
                    Status = asset.Status,
                    Criticality = asset.Criticality,
                    EmployeeId = techEmployee?.Id,
                    PurchaseDate = DateTime.UtcNow.AddYears(-2),
                    PurchasePrice = 50000m,
                    CurrentValue = 40000m,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.MaintenanceAssets.Add(maintenanceAsset);
                _logger.LogInformation("Seeded maintenance asset: {Name}", asset.Name);
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedMaintenanceTaskTemplatesAsync(Guid tenantId)
    {
        _logger.LogInformation("Seeding maintenance task templates...");

        // Get maintenance types
        var preventiveMT = await _context.MaintenanceTypes.FirstOrDefaultAsync(mt => mt.TenantId == tenantId && mt.Code == "PREV");
        var correctiveMT = await _context.MaintenanceTypes.FirstOrDefaultAsync(mt => mt.TenantId == tenantId && mt.Code == "CORR");
        var emergencyMT = await _context.MaintenanceTypes.FirstOrDefaultAsync(mt => mt.TenantId == tenantId && mt.Code == "EMER");

        if (preventiveMT == null)
        {
            return;
        }

        var templates = new[]
        {
            new {
                MaintenanceTypeId = preventiveMT.Id, TaskName = "Inspect HVAC Filters", Description = "Check and replace air filters if needed",
                Sequence = 1, Hours = 0.5, Instructions = "1. Turn off unit\n2. Remove filter panel\n3. Inspect filter condition\n4. Replace if dirty\n5. Reinstall panel",
                Safety = "Use PPE, ensure power is off", Tools = "Screwdriver, replacement filters", Parts = "Air filters"
            },
            new {
                MaintenanceTypeId = preventiveMT.Id, TaskName = "Check Refrigerant Levels", Description = "Verify refrigerant pressure and levels",
                Sequence = 2, Hours = 1.0, Instructions = "1. Connect gauges\n2. Check pressure readings\n3. Compare to spec\n4. Add refrigerant if needed\n5. Check for leaks",
                Safety = "Certified technician required, use safety goggles", Tools = "Pressure gauges, leak detector", Parts = "Refrigerant R410A"
            },
            new {
                MaintenanceTypeId = preventiveMT.Id, TaskName = "Lubricate Moving Parts", Description = "Lubricate bearings and moving components",
                Sequence = 3, Hours = 0.75, Instructions = "1. Identify lubrication points\n2. Clean old grease\n3. Apply fresh lubricant\n4. Check for smooth operation",
                Safety = "Ensure equipment is off and locked out", Tools = "Grease gun", Parts = "Multi-purpose grease"
            },
            new {
                MaintenanceTypeId = correctiveMT?.Id ?? preventiveMT.Id, TaskName = "Replace Faulty Belt", Description = "Remove and replace worn or broken belt",
                Sequence = 1, Hours = 1.5, Instructions = "1. Turn off power\n2. Release tension\n3. Remove old belt\n4. Install new belt\n5. Adjust tension\n6. Test operation",
                Safety = "Lockout/tagout required", Tools = "Belt tension gauge, wrenches", Parts = "V-Belt"
            },
            new {
                MaintenanceTypeId = correctiveMT?.Id ?? preventiveMT.Id, TaskName = "Replace Defective Relay", Description = "Diagnose and replace failed relay",
                Sequence = 1, Hours = 1.0, Instructions = "1. Identify faulty relay\n2. Turn off power\n3. Remove old relay\n4. Install replacement\n5. Test circuit\n6. Verify operation",
                Safety = "Qualified electrician required", Tools = "Multimeter, screwdrivers", Parts = "12V Relay"
            },
            new {
                MaintenanceTypeId = emergencyMT?.Id ?? preventiveMT.Id, TaskName = "Emergency Generator Start", Description = "Respond to power outage and start backup generator",
                Sequence = 1, Hours = 2.0, Instructions = "1. Assess power outage\n2. Check generator fuel\n3. Start generator\n4. Monitor voltage/frequency\n5. Switch loads\n6. Document incident",
                Safety = "Follow emergency procedures, use communication equipment", Tools = "Multimeter, flashlight", Parts = "Diesel fuel"
            }
        };

        foreach (var template in templates)
        {
            var exists = await _context.MaintenanceTaskTemplates.AnyAsync(t =>
                t.TenantId == tenantId && t.MaintenanceTypeId == template.MaintenanceTypeId && t.TaskName == template.TaskName);

            if (!exists)
            {
                var taskTemplate = new MaintenanceTaskTemplate
                {
                    TenantId = tenantId,
                    MaintenanceTypeId = template.MaintenanceTypeId,
                    TaskName = template.TaskName,
                    Description = template.Description,
                    Sequence = template.Sequence,
                    EstimatedHours = template.Hours,
                    Instructions = template.Instructions,
                    SafetyRequirements = template.Safety,
                    IsRequired = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.MaintenanceTaskTemplates.Add(taskTemplate);
                _logger.LogInformation("Seeded task template: {Name}", template.TaskName);
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedAssetTaskTemplatesAsync(Guid tenantId)
    {
        _logger.LogInformation("Seeding asset task templates...");

        // Get assets
        var hvacAsset = await _context.MaintenanceAssets.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.AssetNumber == "HVAC-001");
        var chillerAsset = await _context.MaintenanceAssets.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.AssetNumber == "HVAC-002");
        var generatorAsset = await _context.MaintenanceAssets.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.AssetNumber == "GEN-001");

        // Get maintenance types
        var preventiveMT = await _context.MaintenanceTypes.FirstOrDefaultAsync(mt => mt.TenantId == tenantId && mt.Code == "PREV");
        var correctiveMT = await _context.MaintenanceTypes.FirstOrDefaultAsync(mt => mt.TenantId == tenantId && mt.Code == "CORR");

        if (hvacAsset == null || preventiveMT == null)
        {
            return;
        }

        var assetTemplates = new[]
        {
            new {
                AssetId = hvacAsset.Id, MaintenanceTypeId = preventiveMT.Id,
                TaskName = "Inspect HVAC-001 Air Filters", Description = "Specific inspection for main HVAC unit filters",
                Sequence = 1, Hours = 0.5, Instructions = "Check main unit filters on Building A roof",
                Safety = "Use fall protection when accessing roof unit", Tools = "Ladder, Screwdriver", Parts = "20x25 air filters"
            },
            new {
                AssetId = hvacAsset.Id, MaintenanceTypeId = preventiveMT.Id,
                TaskName = "Test HVAC-001 Thermostat Calibration", Description = "Verify thermostat accuracy",
                Sequence = 2, Hours = 0.75, Instructions = "Use calibrated thermometer to verify thermostat readings",
                Safety = "None specific", Tools = "Calibrated thermometer", Parts = ""
            },
            new {
                AssetId = chillerAsset?.Id ?? hvacAsset.Id, MaintenanceTypeId = preventiveMT.Id,
                TaskName = "Check Chiller Refrigerant Levels", Description = "Inspect chiller system refrigerant",
                Sequence = 1, Hours = 1.0, Instructions = "Connect gauges and check pressure levels",
                Safety = "EPA certified technician required", Tools = "Pressure gauges", Parts = "R410A refrigerant (if needed)"
            },
            new {
                AssetId = generatorAsset?.Id ?? hvacAsset.Id, MaintenanceTypeId = preventiveMT.Id,
                TaskName = "Generator Load Bank Test", Description = "Perform monthly load test on generator",
                Sequence = 1, Hours = 1.5, Instructions = "Connect load bank and run at 50%, 75%, 100% load",
                Safety = "Ensure adequate ventilation", Tools = "Load bank, Multimeter", Parts = "Diesel fuel"
            }
        };

        foreach (var template in assetTemplates)
        {
            var exists = await _context.AssetTaskTemplates.AnyAsync(t =>
                t.TenantId == tenantId && t.AssetId == template.AssetId &&
                t.MaintenanceTypeId == template.MaintenanceTypeId && t.TaskName == template.TaskName);

            if (!exists)
            {
                var assetTaskTemplate = new AssetTaskTemplate
                {
                    TenantId = tenantId,
                    AssetId = template.AssetId,
                    MaintenanceTypeId = template.MaintenanceTypeId,
                    TaskName = template.TaskName,
                    Description = template.Description,
                    Sequence = template.Sequence,
                    EstimatedHours = template.Hours,
                    Instructions = template.Instructions,
                    SafetyRequirements = template.Safety,
                    RequiredTools = template.Tools,
                    RequiredParts = template.Parts,
                    IsRequired = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.AssetTaskTemplates.Add(assetTaskTemplate);
                _logger.LogInformation("Seeded asset task template: {Name}", template.TaskName);
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedAssetTypeTaskTemplatesAsync(Guid tenantId)
    {
        _logger.LogInformation("Seeding asset type task templates...");

        // Get asset types
        var equipmentType = await _context.AssetTypes.FirstOrDefaultAsync(at => at.TenantId == tenantId && at.Code == "EQUIPMENT");
        var systemType = await _context.AssetTypes.FirstOrDefaultAsync(at => at.TenantId == tenantId && at.Code == "SYSTEM");

        // Get maintenance types
        var preventiveMT = await _context.MaintenanceTypes.FirstOrDefaultAsync(mt => mt.TenantId == tenantId && mt.Code == "PREV");
        var inspectionMT = await _context.MaintenanceTypes.FirstOrDefaultAsync(mt => mt.TenantId == tenantId && mt.Code == "PRED");

        if (equipmentType == null || preventiveMT == null)
        {
            return;
        }

        var assetTypeTemplates = new[]
        {
            new {
                AssetTypeId = equipmentType.Id, MaintenanceTypeId = preventiveMT.Id,
                TaskName = "Inspect Equipment Safety Guards", Description = "Check all safety guards and covers",
                Sequence = 1, Hours = 0.5, Instructions = "Verify all guards are in place and secure",
                Safety = "Lockout/tagout before inspection", Tools = "Visual inspection", Parts = ""
            },
            new {
                AssetTypeId = equipmentType.Id, MaintenanceTypeId = preventiveMT.Id,
                TaskName = "Lubricate Equipment Moving Parts", Description = "Apply lubrication to bearings and joints",
                Sequence = 2, Hours = 0.75, Instructions = "Follow equipment lubrication chart",
                Safety = "Equipment must be off", Tools = "Grease gun", Parts = "Multi-purpose grease"
            },
            new {
                AssetTypeId = systemType?.Id ?? equipmentType.Id, MaintenanceTypeId = preventiveMT.Id,
                TaskName = "System Performance Check", Description = "Verify system operating parameters",
                Sequence = 1, Hours = 1.0, Instructions = "Check temperature, pressure, flow rates",
                Safety = "Use appropriate PPE", Tools = "Multimeter, Thermometer", Parts = ""
            },
            new {
                AssetTypeId = systemType?.Id ?? equipmentType.Id, MaintenanceTypeId = inspectionMT?.Id ?? preventiveMT.Id,
                TaskName = "System Vibration Analysis", Description = "Perform vibration monitoring",
                Sequence = 1, Hours = 1.5, Instructions = "Use vibration analyzer on key points",
                Safety = "None specific", Tools = "Vibration analyzer", Parts = ""
            }
        };

        foreach (var template in assetTypeTemplates)
        {
            var exists = await _context.AssetTypeTaskTemplates.AnyAsync(t =>
                t.TenantId == tenantId && t.AssetTypeId == template.AssetTypeId &&
                t.MaintenanceTypeId == template.MaintenanceTypeId && t.TaskName == template.TaskName);

            if (!exists)
            {
                var assetTypeTaskTemplate = new AssetTypeTaskTemplate
                {
                    TenantId = tenantId,
                    AssetTypeId = template.AssetTypeId,
                    MaintenanceTypeId = template.MaintenanceTypeId,
                    TaskName = template.TaskName,
                    Description = template.Description,
                    Sequence = template.Sequence,
                    EstimatedHours = template.Hours,
                    Instructions = template.Instructions,
                    SafetyRequirements = template.Safety,
                    RequiredTools = template.Tools,
                    RequiredParts = template.Parts,
                    IsRequired = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.AssetTypeTaskTemplates.Add(assetTypeTaskTemplate);
                _logger.LogInformation("Seeded asset type task template: {Name}", template.TaskName);
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedQualityControlChecklistsAsync(Guid tenantId)
    {
        _logger.LogInformation("Seeding quality control checklists...");

        var checklists = new[]
        {
            new {
                Name = "HVAC Preventive Maintenance QC",
                Description = "Quality checklist for HVAC preventive maintenance work orders",
                WorkOrderType = "PM",
                AssetCategory = "HVAC",
                MaintenanceType = "PREV",
                Items = @"[
                    {""item"":""All filters replaced or cleaned"",""type"":""Boolean"",""required"":true},
                    {""item"":""Refrigerant levels checked and within spec"",""type"":""Boolean"",""required"":true},
                    {""item"":""All electrical connections tight and secure"",""type"":""Boolean"",""required"":true},
                    {""item"":""Moving parts lubricated"",""type"":""Boolean"",""required"":true},
                    {""item"":""No unusual noises or vibrations"",""type"":""Boolean"",""required"":true},
                    {""item"":""Temperature differential within spec"",""type"":""Measurement"",""required"":true},
                    {""item"":""Work area cleaned and organized"",""type"":""Boolean"",""required"":true}
                ]"
            },
            new {
                Name = "Emergency Generator Inspection QC",
                Description = "Quality checklist for generator testing and maintenance",
                WorkOrderType = "INSP",
                AssetCategory = "POWER",
                MaintenanceType = "PREV",
                Items = @"[
                    {""item"":""Generator starts within 10 seconds"",""type"":""Boolean"",""required"":true},
                    {""item"":""Voltage output stable and within range"",""type"":""Measurement"",""required"":true},
                    {""item"":""Frequency within 59.5-60.5 Hz"",""type"":""Measurement"",""required"":true},
                    {""item"":""No fuel leaks detected"",""type"":""Boolean"",""required"":true},
                    {""item"":""Oil level checked and topped up"",""type"":""Boolean"",""required"":true},
                    {""item"":""Battery voltage within spec"",""type"":""Measurement"",""required"":true},
                    {""item"":""Coolant level adequate"",""type"":""Boolean"",""required"":true},
                    {""item"":""Load test successful"",""type"":""Boolean"",""required"":true}
                ]"
            },
            new {
                Name = "General Corrective Maintenance QC",
                Description = "Quality checklist for corrective maintenance work",
                WorkOrderType = "CM",
                AssetCategory = (string?)null,
                MaintenanceType = "CORR",
                Items = @"[
                    {""item"":""Root cause identified and documented"",""type"":""Boolean"",""required"":true},
                    {""item"":""Failed parts removed and replaced"",""type"":""Boolean"",""required"":true},
                    {""item"":""Equipment tested and operational"",""type"":""Boolean"",""required"":true},
                    {""item"":""Safety systems verified functional"",""type"":""Boolean"",""required"":true},
                    {""item"":""All tools and parts accounted for"",""type"":""Boolean"",""required"":true},
                    {""item"":""Work area safe and clean"",""type"":""Boolean"",""required"":true}
                ]"
            }
        };

        foreach (var checklist in checklists)
        {
            var exists = await _context.QualityControlChecklists.AnyAsync(q => q.TenantId == tenantId && q.Name == checklist.Name);
            if (!exists)
            {
                var qcChecklist = new QualityControlChecklist
                {
                    TenantId = tenantId,
                    Name = checklist.Name,
                    Description = checklist.Description,
                    WorkOrderType = checklist.WorkOrderType,
                    AssetCategory = checklist.AssetCategory,
                    MaintenanceType = checklist.MaintenanceType,
                    ChecklistItems = checklist.Items,
                    IsMandatory = true,
                    IsActive = true,
                    MinimumPassingScore = 80,
                    Version = 1,
                    CreatedDate = DateTime.UtcNow,
                    CreatedById = Guid.Empty // System user
                };

                _context.QualityControlChecklists.Add(qcChecklist);
                _logger.LogInformation("Seeded quality control checklist: {Name}", checklist.Name);
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedInspectionTemplatesAsync(Guid tenantId)
    {
        _logger.LogInformation("Seeding inspection templates...");

        var templates = new[]
        {
            new {
                Name = "HVAC System Safety Inspection",
                Description = "Comprehensive safety inspection for HVAC systems",
                Category = "HVAC",
                Type = "Safety",
                Items = @"[
                    {""item"":""Electrical connections secure and properly insulated"",""category"":""Electrical"",""critical"":true},
                    {""item"":""No refrigerant leaks detected"",""category"":""Safety"",""critical"":true},
                    {""item"":""Emergency shutoff accessible and functional"",""category"":""Safety"",""critical"":true},
                    {""item"":""Ventilation adequate and unobstructed"",""category"":""Safety"",""critical"":false},
                    {""item"":""Guards and covers in place"",""category"":""Safety"",""critical"":true},
                    {""item"":""No abnormal noise or vibration"",""category"":""Performance"",""critical"":false},
                    {""item"":""Condensate drain clear and functioning"",""category"":""Maintenance"",""critical"":false}
                ]"
            },
            new {
                Name = "Generator Regulatory Inspection",
                Description = "Regulatory compliance inspection for emergency generators",
                Category = "Power Systems",
                Type = "Regulatory",
                Items = @"[
                    {""item"":""Fuel storage meets local fire code"",""category"":""Compliance"",""critical"":true},
                    {""item"":""Emissions within regulatory limits"",""category"":""Compliance"",""critical"":true},
                    {""item"":""Signage and labeling compliant"",""category"":""Compliance"",""critical"":false},
                    {""item"":""Monthly load test records maintained"",""category"":""Documentation"",""critical"":true},
                    {""item"":""Fire suppression system functional"",""category"":""Safety"",""critical"":true},
                    {""item"":""Spill containment adequate"",""category"":""Compliance"",""critical"":true}
                ]"
            },
            new {
                Name = "Electrical Panel Maintenance Inspection",
                Description = "Routine inspection for electrical distribution panels",
                Category = "Electrical",
                Type = "Maintenance",
                Items = @"[
                    {""item"":""No signs of overheating or discoloration"",""category"":""Safety"",""critical"":true},
                    {""item"":""All breakers properly labeled"",""category"":""Documentation"",""critical"":false},
                    {""item"":""Panel door seals intact"",""category"":""Maintenance"",""critical"":false},
                    {""item"":""No unauthorized modifications"",""category"":""Compliance"",""critical"":true},
                    {""item"":""Grounding connections tight and secure"",""category"":""Safety"",""critical"":true},
                    {""item"":""Adequate clearance maintained"",""category"":""Safety"",""critical"":true}
                ]"
            },
            new {
                Name = "Pre-Service Quality Inspection",
                Description = "Quality inspection before putting equipment back in service",
                Category = "Quality",
                Type = "Quality",
                Items = @"[
                    {""item"":""All repairs completed per work order"",""category"":""Quality"",""critical"":true},
                    {""item"":""Equipment tested and operational"",""category"":""Performance"",""critical"":true},
                    {""item"":""No fluid leaks present"",""category"":""Quality"",""critical"":true},
                    {""item"":""All fasteners torqued to specification"",""category"":""Quality"",""critical"":false},
                    {""item"":""Lockout/tagout devices removed"",""category"":""Safety"",""critical"":true},
                    {""item"":""Work area clean and free of hazards"",""category"":""Safety"",""critical"":true},
                    {""item"":""Documentation completed"",""category"":""Documentation"",""critical"":true}
                ]"
            }
        };

        foreach (var template in templates)
        {
            var exists = await _context.InspectionTemplates.AnyAsync(t => t.TenantId == tenantId && t.Name == template.Name);
            if (!exists)
            {
                var inspectionTemplate = new InspectionTemplate
                {
                    TenantId = tenantId,
                    Name = template.Name,
                    Description = template.Description,
                    Category = template.Category,
                    InspectionType = template.Type,
                    ChecklistItems = template.Items,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "System"
                };

                _context.InspectionTemplates.Add(inspectionTemplate);
                _logger.LogInformation("Seeded inspection template: {Name}", template.Name);
            }
        }

        await _context.SaveChangesAsync();
    }
}
