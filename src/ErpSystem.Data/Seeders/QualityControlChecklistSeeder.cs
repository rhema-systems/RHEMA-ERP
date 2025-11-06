using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

public class ChecklistTemplate
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string WorkOrderType { get; set; } = string.Empty;
    public string? AssetCategory { get; set; }
    public string MaintenanceType { get; set; } = string.Empty;
    public string Items { get; set; } = string.Empty;
    public int MinPassingScore { get; set; }
}

public class QualityControlChecklistSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<QualityControlChecklistSeeder> _logger;

    public QualityControlChecklistSeeder(ApplicationDbContext context, ILogger<QualityControlChecklistSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(Guid tenantId)
    {
        _logger.LogInformation("Starting comprehensive QC checklist seeding for tenant {TenantId}", tenantId);

        // Load asset categories from MaintenanceAssetCategories table
        var assetCategories = await _context.MaintenanceAssetCategories
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .Select(c => c.Name)
            .ToListAsync();

        if (!assetCategories.Any())
        {
            _logger.LogWarning("No active asset categories found for tenant {TenantId}. Using default categories.", tenantId);
            assetCategories = new List<string> { "Power Systems", "HVAC", "Equipment", "Vehicles", "Electrical", "Building Systems" };
        }

        _logger.LogInformation("Found {Count} asset categories: {Categories}", assetCategories.Count, string.Join(", ", assetCategories));

        // Build checklist templates dynamically based on available categories
        var checklistTemplates = new List<ChecklistTemplate>();

        // Create category-specific checklists for common categories
        var categoryChecklistMap = new Dictionary<string, ChecklistTemplate[]>()
        {
            ["Power Systems"] = new[]
            {
                new ChecklistTemplate {
                    Name = "Power Systems - Preventive Maintenance QC",
                Description = "Quality checklist for preventive maintenance on power systems including generators and UPS",
                WorkOrderType = "Preventive Maintenance",
                AssetCategory = "Power Systems",
                MaintenanceType = "Preventive",
                Items = @"[
                    {""item"":""Generator voltage output within specification"",""type"":""Measurement"",""required"":true},
                    {""item"":""Frequency stable at 50/60 Hz"",""type"":""Measurement"",""required"":true},
                    {""item"":""Fuel system leak-free"",""type"":""Visual"",""required"":true},
                    {""item"":""Oil levels checked and adequate"",""type"":""Visual"",""required"":true},
                    {""item"":""Battery connections clean and tight"",""type"":""Visual"",""required"":true},
                    {""item"":""Load test performed successfully"",""type"":""Test"",""required"":true},
                    {""item"":""All safety systems functional"",""type"":""Test"",""required"":true},
                    {""item"":""Work area cleaned"",""type"":""Visual"",""required"":true}
                ]",
                    MinPassingScore = 85
                },
                new ChecklistTemplate {
                    Name = "Power Systems - Corrective Maintenance QC",
                Description = "Quality checklist for corrective maintenance on power systems",
                WorkOrderType = "Corrective Maintenance",
                AssetCategory = "Power Systems",
                MaintenanceType = "Corrective",
                Items = @"[
                    {""item"":""Root cause identified and documented"",""type"":""Documentation"",""required"":true},
                    {""item"":""Defective components replaced"",""type"":""Visual"",""required"":true},
                    {""item"":""System tested under load"",""type"":""Test"",""required"":true},
                    {""item"":""Performance within specifications"",""type"":""Measurement"",""required"":true},
                    {""item"":""All safety interlocks functional"",""type"":""Test"",""required"":true},
                    {""item"":""Documentation completed"",""type"":""Documentation"",""required"":true}
                ]",
                    MinPassingScore = 90
                }
            },
            ["HVAC"] = new[]
            {
                new ChecklistTemplate {
                Name = "HVAC - Preventive Maintenance QC",
                Description = "Quality checklist for HVAC preventive maintenance",
                WorkOrderType = "Preventive Maintenance",
                AssetCategory = "HVAC",
                MaintenanceType = "Preventive",
                Items = @"[
                    {""item"":""Air filters replaced or cleaned"",""type"":""Visual"",""required"":true},
                    {""item"":""Refrigerant levels within specification"",""type"":""Measurement"",""required"":true},
                    {""item"":""Electrical connections secure"",""type"":""Visual"",""required"":true},
                    {""item"":""Moving parts lubricated"",""type"":""Visual"",""required"":true},
                    {""item"":""No unusual noises or vibrations"",""type"":""Visual"",""required"":true},
                    {""item"":""Temperature differential within spec"",""type"":""Measurement"",""required"":true},
                    {""item"":""Condensate drain clear"",""type"":""Visual"",""required"":true},
                    {""item"":""Work area cleaned"",""type"":""Visual"",""required"":true}
                ]",
                    MinPassingScore = 80
                }
            },
            ["Equipment"] = new[]
            {
                new ChecklistTemplate {
                Name = "Equipment - Preventive Maintenance QC",
                Description = "General quality checklist for equipment preventive maintenance",
                WorkOrderType = "Preventive Maintenance",
                AssetCategory = "Equipment",
                MaintenanceType = "Preventive",
                Items = @"[
                    {""item"":""Equipment cleaned and inspected"",""type"":""Visual"",""required"":true},
                    {""item"":""All guards and covers in place"",""type"":""Visual"",""required"":true},
                    {""item"":""Lubrication performed per schedule"",""type"":""Visual"",""required"":true},
                    {""item"":""Belts and chains inspected"",""type"":""Visual"",""required"":true},
                    {""item"":""Electrical connections checked"",""type"":""Visual"",""required"":true},
                    {""item"":""Equipment operates smoothly"",""type"":""Test"",""required"":true},
                    {""item"":""Safety features tested"",""type"":""Test"",""required"":true}
                ]",
                    MinPassingScore = 80
                }
            },
            ["Vehicles"] = new[]
            {
                new ChecklistTemplate {
                Name = "Vehicles - Preventive Maintenance QC",
                Description = "Quality checklist for vehicle preventive maintenance",
                WorkOrderType = "Preventive Maintenance",
                AssetCategory = "Vehicles",
                MaintenanceType = "Preventive",
                Items = @"[
                    {""item"":""Oil and filter changed"",""type"":""Visual"",""required"":true},
                    {""item"":""Tire pressure checked and adjusted"",""type"":""Measurement"",""required"":true},
                    {""item"":""Brake system inspected"",""type"":""Visual"",""required"":true},
                    {""item"":""Lights and signals functional"",""type"":""Test"",""required"":true},
                    {""item"":""Fluid levels checked"",""type"":""Visual"",""required"":true},
                    {""item"":""Battery tested"",""type"":""Test"",""required"":true},
                    {""item"":""Test drive completed"",""type"":""Test"",""required"":true}
                ]",
                    MinPassingScore = 85
                }
            },
            ["Electrical"] = new[]
            {
                new ChecklistTemplate {
                Name = "Electrical - Preventive Maintenance QC",
                Description = "Quality checklist for electrical systems preventive maintenance",
                WorkOrderType = "Preventive Maintenance",
                AssetCategory = "Electrical",
                MaintenanceType = "Preventive",
                Items = @"[
                    {""item"":""All connections tight and secure"",""type"":""Visual"",""required"":true},
                    {""item"":""No signs of overheating"",""type"":""Visual"",""required"":true},
                    {""item"":""Insulation resistance tested"",""type"":""Test"",""required"":true},
                    {""item"":""Grounding verified"",""type"":""Test"",""required"":true},
                    {""item"":""Circuit breakers tested"",""type"":""Test"",""required"":true},
                    {""item"":""Panel labeling correct"",""type"":""Visual"",""required"":true},
                    {""item"":""Adequate clearance maintained"",""type"":""Visual"",""required"":true}
                ]",
                    MinPassingScore = 90
                }
            },
            ["Building Systems"] = new[]
            {
                new ChecklistTemplate {
                Name = "Building Systems - Preventive Maintenance QC",
                Description = "Quality checklist for building systems preventive maintenance",
                WorkOrderType = "Preventive Maintenance",
                AssetCategory = "Building Systems",
                MaintenanceType = "Preventive",
                Items = @"[
                    {""item"":""All systems inspected visually"",""type"":""Visual"",""required"":true},
                    {""item"":""Scheduled maintenance tasks completed"",""type"":""Visual"",""required"":true},
                    {""item"":""Safety devices tested"",""type"":""Test"",""required"":true},
                    {""item"":""Fire safety systems checked"",""type"":""Test"",""required"":true},
                    {""item"":""Emergency lighting functional"",""type"":""Test"",""required"":true},
                    {""item"":""Documentation updated"",""type"":""Documentation"",""required"":true}
                ]",
                    MinPassingScore = 85
                }
            }
        };

        // Add category-specific checklists for categories that exist in the database
        foreach (var category in assetCategories)
        {
            if (categoryChecklistMap.ContainsKey(category))
            {
                checklistTemplates.AddRange(categoryChecklistMap[category]);
            }
        }

        // Add generic/fallback checklists (no specific asset category)
        checklistTemplates.AddRange(new[]
        {
            // Generic Preventive Maintenance (fallback)
            new ChecklistTemplate {
                Name = "General - Preventive Maintenance QC",
                Description = "Generic quality checklist for preventive maintenance work orders",
                WorkOrderType = "Preventive Maintenance",
                AssetCategory = (string?)null, // Applies to all categories if no specific match
                MaintenanceType = "Preventive",
                Items = @"[
                    {""item"":""All scheduled tasks completed"",""type"":""Visual"",""required"":true},
                    {""item"":""Equipment cleaned"",""type"":""Visual"",""required"":true},
                    {""item"":""Lubrication performed where needed"",""type"":""Visual"",""required"":true},
                    {""item"":""Functional test passed"",""type"":""Test"",""required"":true},
                    {""item"":""Safety check completed"",""type"":""Test"",""required"":true},
                    {""item"":""Work area clean and safe"",""type"":""Visual"",""required"":true},
                    {""item"":""Documentation complete"",""type"":""Documentation"",""required"":true}
                ]",
                MinPassingScore = 80
            },

            // Generic Corrective Maintenance (fallback)
            new ChecklistTemplate {
                Name = "General - Corrective Maintenance QC",
                Description = "Generic quality checklist for corrective maintenance work orders",
                WorkOrderType = "Corrective Maintenance",
                AssetCategory = (string?)null,
                MaintenanceType = "Corrective",
                Items = @"[
                    {""item"":""Problem identified and documented"",""type"":""Documentation"",""required"":true},
                    {""item"":""Repairs completed successfully"",""type"":""Visual"",""required"":true},
                    {""item"":""Equipment tested and operational"",""type"":""Test"",""required"":true},
                    {""item"":""Safety systems verified"",""type"":""Test"",""required"":true},
                    {""item"":""Work area cleaned"",""type"":""Visual"",""required"":true},
                    {""item"":""Documentation completed"",""type"":""Documentation"",""required"":true}
                ]",
                MinPassingScore = 85
            },

            // Emergency Maintenance
            new ChecklistTemplate {
                Name = "Emergency Maintenance QC",
                Description = "Quality checklist for emergency maintenance work orders",
                WorkOrderType = "Emergency",
                AssetCategory = (string?)null,
                MaintenanceType = "Emergency",
                Items = @"[
                    {""item"":""Emergency resolved"",""type"":""Visual"",""required"":true},
                    {""item"":""Equipment safe to operate"",""type"":""Test"",""required"":true},
                    {""item"":""Safety hazards eliminated"",""type"":""Visual"",""required"":true},
                    {""item"":""Temporary repairs stable"",""type"":""Test"",""required"":true},
                    {""item"":""Follow-up requirements documented"",""type"":""Documentation"",""required"":true}
                ]",
                MinPassingScore = 90
            },

            // Inspection Work Orders
            new ChecklistTemplate {
                Name = "Inspection QC",
                Description = "Quality checklist for inspection work orders",
                WorkOrderType = "Inspection",
                AssetCategory = (string?)null,
                MaintenanceType = "Inspection",
                Items = @"[
                    {""item"":""All inspection points checked"",""type"":""Visual"",""required"":true},
                    {""item"":""Measurements recorded accurately"",""type"":""Measurement"",""required"":true},
                    {""item"":""Deficiencies documented"",""type"":""Documentation"",""required"":true},
                    {""item"":""Photos taken where required"",""type"":""Documentation"",""required"":true},
                    {""item"":""Inspection report completed"",""type"":""Documentation"",""required"":true}
                ]",
                MinPassingScore = 95
            }
        });

        _logger.LogInformation("Created {Count} checklist templates based on available asset categories", checklistTemplates.Count);

        foreach (var checklist in checklistTemplates)
        {
            var exists = await _context.QualityControlChecklists
                .AnyAsync(q => q.TenantId == tenantId && 
                              q.Name == checklist.Name);
            
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
                    MinimumPassingScore = checklist.MinPassingScore,
                    Version = 1,
                    CreatedDate = DateTime.UtcNow,
                    CreatedById = Guid.Empty // System user
                };

                _context.QualityControlChecklists.Add(qcChecklist);
                _logger.LogInformation("Seeded QC checklist: {Name} for {AssetCategory}/{WorkOrderType}/{MaintenanceType}", 
                    checklist.Name, 
                    checklist.AssetCategory ?? "All", 
                    checklist.WorkOrderType, 
                    checklist.MaintenanceType);
            }
        }

        var savedCount = await _context.SaveChangesAsync();
        _logger.LogInformation("QC checklist seeding completed. {Count} new checklists added.", savedCount);
    }
}
