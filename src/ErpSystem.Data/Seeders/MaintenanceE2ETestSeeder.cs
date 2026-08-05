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
    private Guid _diagnosticScannerPartId;

    // Workflow test IDs
    private Guid _maintenanceTeamId;
    private Guid _jobCardId;
    private Guid _workOrderId;
    private Guid _maintenanceScheduleId;
    private Guid _scheduleHistoryId;
    private Guid _scheduleNotificationHistoryId;
    private Guid _maintenanceToolId;
    private Guid _workOrderToolId;
    private Guid _workOrderLaborId;
    private Guid _maintenanceExpenseId;
    private Guid _maintenanceStaffScheduleId;
    private Guid _inspectionTemplateId;
    private Guid _assetInspectionId;
    private Guid _inspectionDocumentId;
    private Guid _qualityChecklistId;
    private Guid _qualityCheckId;
    private Guid _qualitySignOffId;
    private Guid _assetDowntimeId;
    private Guid _assetUsageId;
    private Guid _notificationTemplateId;
    private Guid _notificationId;
    private Guid _technicalSkillId;
    private Guid _technicianSkillId;
    private Guid _safetyProtocolId;
    private Guid _safetyComplianceId;
    private Guid _preInspectionTemplateId;
    private Guid _admissionConditionRecordId;
    private Guid _dischargeConditionRecordId;
    private Guid _assetAdmissionId;
    private Guid _assetDischargeId;
    private Guid _assetMaintenanceDowntimeId;
    private Guid _maintenanceAttachmentId;
    private Guid _maintenanceAttachmentAccessId;
    private Guid _toolCheckoutId;
    private Guid _protocolAdherenceId;
    private Guid _protocolTrainingId;
    private Guid _safetyAuditId;
    private Guid _protocolAuditDetailId;
    private Guid _technicianCertificationId;
    private Guid _jobCardCertificateId;

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
        _vehicleCategoryId = Guid.Parse("00000001-0000-0000-0000-000000000001");
        _equipmentCategoryId = Guid.Parse("00000001-0000-0000-0000-000000000002");

        // Maintenance Types
        _preventiveMaintenanceTypeId = Guid.Parse("00000002-0000-0000-0000-000000000001");
        _correctiveMaintenanceTypeId = Guid.Parse("00000002-0000-0000-0000-000000000002");
        _emergencyMaintenanceTypeId = Guid.Parse("00000002-0000-0000-0000-000000000003");

        // Asset Types
        _vehicleAssetTypeId = Guid.Parse("00000003-0000-0000-0000-000000000001");
        _equipmentAssetTypeId = Guid.Parse("00000003-0000-0000-0000-000000000002");

        // Priority Levels
        _highPriorityId = Guid.Parse("00000004-0000-0000-0000-000000000001");
        _mediumPriorityId = Guid.Parse("00000004-0000-0000-0000-000000000002");
        _lowPriorityId = Guid.Parse("00000004-0000-0000-0000-000000000003");

        // Work Order Types
        _preventiveWorkOrderTypeId = Guid.Parse("00000005-0000-0000-0000-000000000001");
        _correctiveWorkOrderTypeId = Guid.Parse("00000005-0000-0000-0000-000000000002");

        // Employees (will be created or linked to existing)
        _fleetManagerId = Guid.Parse("00000006-0000-0000-0000-000000000001");
        _maintenanceSupervisorId = Guid.Parse("00000006-0000-0000-0000-000000000002");
        _maintenanceManagerId = Guid.Parse("00000006-0000-0000-0000-000000000003");
        _seniorTechnicianId = Guid.Parse("00000006-0000-0000-0000-000000000004");
        _juniorTechnicianId = Guid.Parse("00000006-0000-0000-0000-000000000005");
        _qualityInspectorId = Guid.Parse("00000006-0000-0000-0000-000000000006");
        _serviceAdvisorId = Guid.Parse("00000006-0000-0000-0000-000000000007");

        // Assets
        _deliveryTruckId = Guid.Parse("00000007-0000-0000-0000-000000000001");
        _forkliftId = Guid.Parse("00000007-0000-0000-0000-000000000002");
        _generatorId = Guid.Parse("00000007-0000-0000-0000-000000000003");

        // Inventory Parts
        _engineOilPartId = Guid.Parse("00000008-0000-0000-0000-000000000001");
        _oilFilterPartId = Guid.Parse("00000008-0000-0000-0000-000000000002");
        _airFilterPartId = Guid.Parse("00000008-0000-0000-0000-000000000003");
        _brakeFluidPartId = Guid.Parse("00000008-0000-0000-0000-000000000004");
        _sparkPlugPartId = Guid.Parse("00000008-0000-0000-0000-000000000005");
        _diagnosticScannerPartId = Guid.Parse("00000008-0000-0000-0000-000000000006");

        // Workflow test records
        _maintenanceTeamId = Guid.Parse("0000000C-0000-0000-0000-000000000001");
        _jobCardId = Guid.Parse("0000000C-0000-0000-0000-000000000002");
        _workOrderId = Guid.Parse("0000000C-0000-0000-0000-000000000003");
        _maintenanceScheduleId = Guid.Parse("0000000C-0000-0000-0000-000000000004");
        _scheduleHistoryId = Guid.Parse("0000000C-0000-0000-0000-000000000005");
        _scheduleNotificationHistoryId = Guid.Parse("0000000C-0000-0000-0000-000000000006");
        _maintenanceToolId = Guid.Parse("0000000C-0000-0000-0000-000000000007");
        _workOrderToolId = Guid.Parse("0000000C-0000-0000-0000-000000000008");
        _workOrderLaborId = Guid.Parse("0000000C-0000-0000-0000-000000000009");
        _maintenanceExpenseId = Guid.Parse("0000000C-0000-0000-0000-00000000000A");
        _maintenanceStaffScheduleId = Guid.Parse("0000000C-0000-0000-0000-00000000000B");
        _inspectionTemplateId = Guid.Parse("0000000C-0000-0000-0000-00000000000C");
        _assetInspectionId = Guid.Parse("0000000C-0000-0000-0000-00000000000D");
        _inspectionDocumentId = Guid.Parse("0000000C-0000-0000-0000-00000000000E");
        _qualityChecklistId = Guid.Parse("0000000C-0000-0000-0000-00000000000F");
        _qualityCheckId = Guid.Parse("0000000C-0000-0000-0000-000000000010");
        _qualitySignOffId = Guid.Parse("0000000C-0000-0000-0000-000000000011");
        _assetDowntimeId = Guid.Parse("0000000C-0000-0000-0000-000000000012");
        _assetUsageId = Guid.Parse("0000000C-0000-0000-0000-000000000013");
        _notificationTemplateId = Guid.Parse("0000000C-0000-0000-0000-000000000014");
        _notificationId = Guid.Parse("0000000C-0000-0000-0000-000000000015");
        _technicalSkillId = Guid.Parse("0000000C-0000-0000-0000-000000000016");
        _technicianSkillId = Guid.Parse("0000000C-0000-0000-0000-000000000017");
        _safetyProtocolId = Guid.Parse("0000000C-0000-0000-0000-000000000018");
        _safetyComplianceId = Guid.Parse("0000000C-0000-0000-0000-000000000019");
        _preInspectionTemplateId = Guid.Parse("0000000C-0000-0000-0000-00000000001A");
        _admissionConditionRecordId = Guid.Parse("0000000C-0000-0000-0000-00000000001B");
        _dischargeConditionRecordId = Guid.Parse("0000000C-0000-0000-0000-00000000001C");
        _assetAdmissionId = Guid.Parse("0000000C-0000-0000-0000-00000000001D");
        _assetDischargeId = Guid.Parse("0000000C-0000-0000-0000-00000000001E");
        _assetMaintenanceDowntimeId = Guid.Parse("0000000C-0000-0000-0000-00000000001F");
        _maintenanceAttachmentId = Guid.Parse("0000000C-0000-0000-0000-000000000020");
        _maintenanceAttachmentAccessId = Guid.Parse("0000000C-0000-0000-0000-000000000021");
        _toolCheckoutId = Guid.Parse("0000000C-0000-0000-0000-000000000022");
        _protocolAdherenceId = Guid.Parse("0000000C-0000-0000-0000-000000000023");
        _protocolTrainingId = Guid.Parse("0000000C-0000-0000-0000-000000000024");
        _safetyAuditId = Guid.Parse("0000000C-0000-0000-0000-000000000025");
        _protocolAuditDetailId = Guid.Parse("0000000C-0000-0000-0000-000000000026");
        _technicianCertificationId = Guid.Parse("0000000C-0000-0000-0000-000000000027");
        _jobCardCertificateId = Guid.Parse("0000000C-0000-0000-0000-000000000028");
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

            // 13. End-to-end workflow records
            await SeedMaintenanceWorkflowAsync(baseDate);
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
        var maintenanceDeptId = Guid.Parse("00000009-0000-0000-0000-000000000001");
        var fleetDeptId = Guid.Parse("00000009-0000-0000-0000-000000000002");
        var qcDeptId = Guid.Parse("00000009-0000-0000-0000-000000000003");

        var managerPosId = Guid.Parse("0000000A-0000-0000-0000-000000000001");
        var supervisorPosId = Guid.Parse("0000000A-0000-0000-0000-000000000002");
        var technicianPosId = Guid.Parse("0000000A-0000-0000-0000-000000000003");
        var inspectorPosId = Guid.Parse("0000000A-0000-0000-0000-000000000004");

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

        // [HR-MODULE-PORT] Positions require OrganizationUnitId + OrganizationLevelId; resolve a default
        // org unit/level for the tenant so the position inserts below satisfy their FKs.
        var (orgUnitId, orgLevelId) = await SeederOrgDefaults.EnsureDefaultUnitAsync(_context, _defaultTenantId);

        // Seed Positions
        if (!await _context.EmployeePositions.AnyAsync(p => p.Id == managerPosId))
        {
            await _context.EmployeePositions.AddAsync(new EmployeePosition
            {
                Id = managerPosId,
                Title = "Manager",
                Code = "MGR",
                // [HR-MODULE-PORT] Positions anchor to the org structure via the REQUIRED
                // OrganizationUnitId + OrganizationLevelId (DepartmentId was removed).
                OrganizationUnitId = orgUnitId,
                OrganizationLevelId = orgLevelId,
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
                // [HR-MODULE-PORT] Positions anchor to the org structure via the REQUIRED
                // OrganizationUnitId + OrganizationLevelId (DepartmentId was removed).
                OrganizationUnitId = orgUnitId,
                OrganizationLevelId = orgLevelId,
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
                // [HR-MODULE-PORT] Positions anchor to the org structure via the REQUIRED
                // OrganizationUnitId + OrganizationLevelId (DepartmentId was removed).
                OrganizationUnitId = orgUnitId,
                OrganizationLevelId = orgLevelId,
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
                // [HR-MODULE-PORT] Positions anchor to the org structure via the REQUIRED
                // OrganizationUnitId + OrganizationLevelId (DepartmentId was removed).
                OrganizationUnitId = orgUnitId,
                OrganizationLevelId = orgLevelId,
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
        var lubricantsCategoryId = Guid.Parse("0000000B-0000-0000-0000-000000000001");
        var filtersCategoryId = Guid.Parse("0000000B-0000-0000-0000-000000000002");
        var fluidsCategoryId = Guid.Parse("0000000B-0000-0000-0000-000000000003");
        var ignitionCategoryId = Guid.Parse("0000000B-0000-0000-0000-000000000004");
        var toolsCategoryId = Guid.Parse("0000000B-0000-0000-0000-000000000005");

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

        if (!await _context.InventoryCategories.AnyAsync(c => c.Id == toolsCategoryId))
        {
            await _context.InventoryCategories.AddAsync(new InventoryCategory
            {
                Id = toolsCategoryId,
                Name = "Maintenance Tools",
                Code = "TOOLS",
                DefaultUnitOfMeasure = "EA",
                IsActive = true,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        await _context.SaveChangesAsync();

        var parts = new[]
        {
            new { Id = _engineOilPartId, Code = "OIL-5W30-5L", Name = "Engine Oil 5W-30 (5 Liters)", CategoryId = lubricantsCategoryId, Unit = "L", Price = 45.00m, Qty = 100m, Type = ItemType.StockItem },
            new { Id = _oilFilterPartId, Code = "FILTER-OIL-001", Name = "Oil Filter - Heavy Duty", CategoryId = filtersCategoryId, Unit = "EA", Price = 25.00m, Qty = 50m, Type = ItemType.StockItem },
            new { Id = _airFilterPartId, Code = "FILTER-AIR-001", Name = "Air Filter - Heavy Duty", CategoryId = filtersCategoryId, Unit = "EA", Price = 35.00m, Qty = 40m, Type = ItemType.StockItem },
            new { Id = _brakeFluidPartId, Code = "FLUID-BRAKE-DOT4", Name = "Brake Fluid DOT 4 (1 Liter)", CategoryId = fluidsCategoryId, Unit = "L", Price = 15.00m, Qty = 60m, Type = ItemType.StockItem },
            new { Id = _sparkPlugPartId, Code = "SPARK-PLUG-001", Name = "Spark Plug - Iridium", CategoryId = ignitionCategoryId, Unit = "EA", Price = 12.00m, Qty = 80m, Type = ItemType.StockItem },
            new { Id = _diagnosticScannerPartId, Code = "TOOL-SCAN-001", Name = "Heavy Duty Diagnostic Scanner", CategoryId = toolsCategoryId, Unit = "EA", Price = 850.00m, Qty = 2m, Type = ItemType.FixedAsset }
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
                    ItemType = part.Type,
                    Status = ItemStatus.Active,
                    TenantId = _defaultTenantId,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                });
            }
        }

        _logger.LogInformation($"✓ Seeded {parts.Length} inventory parts/tools");
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

        var assets = new[]
        {
            new
            {
                Id = _deliveryTruckId,
                AssetNumber = "TRK-001",
                Name = "Delivery Truck - Isuzu NPR 75",
                CategoryId = _vehicleCategoryId,
                Description = "Primary delivery fleet truck used for maintenance E2E testing",
                SerialNumber = "ISU-NPR-2020-001",
                Manufacturer = "Isuzu",
                Model = "NPR 75",
                Location = "Fleet Parking - Bay 3",
                Status = AssetStatus.Active,
                Criticality = AssetCriticality.High,
                PurchasePrice = 45000.00m,
                CurrentValue = 30000.00m,
                Mileage = 85000d,
                OperatingHours = 3200d,
                LicensePlate = "E2E-TRK-001",
                Vin = "ISUNPR75000000001",
                IsFleet = true
            },
            new
            {
                Id = _forkliftId,
                AssetNumber = "FL-001",
                Name = "Forklift - Toyota 8FG25",
                CategoryId = _equipmentCategoryId,
                Description = "Warehouse forklift for maintenance workflow testing",
                SerialNumber = "TOY-8FG25-2021-001",
                Manufacturer = "Toyota",
                Model = "8FG25",
                Location = "Warehouse A - Loading Dock",
                Status = AssetStatus.Active,
                Criticality = AssetCriticality.Medium,
                PurchasePrice = 35000.00m,
                CurrentValue = 24000.00m,
                Mileage = 0d,
                OperatingHours = 1420d,
                LicensePlate = "",
                Vin = "",
                IsFleet = false
            },
            new
            {
                Id = _generatorId,
                AssetNumber = "GEN-001",
                Name = "Backup Generator - Caterpillar C15",
                CategoryId = _equipmentCategoryId,
                Description = "Backup generator used for inspection and downtime testing",
                SerialNumber = "CAT-C15-2021-089",
                Manufacturer = "Caterpillar",
                Model = "C15",
                Location = "Power House",
                Status = AssetStatus.Active,
                Criticality = AssetCriticality.Critical,
                PurchasePrice = 85000.00m,
                CurrentValue = 72500.00m,
                Mileage = 0d,
                OperatingHours = 4580d,
                LicensePlate = "",
                Vin = "",
                IsFleet = false
            }
        };

        foreach (var asset in assets)
        {
            if (!await _context.MaintenanceAssets.AnyAsync(a => a.Id == asset.Id))
            {
                await _context.MaintenanceAssets.AddAsync(new MaintenanceAsset
                {
                    Id = asset.Id,
                    AssetNumber = asset.AssetNumber,
                    Name = asset.Name,
                    AssetCategoryId = asset.CategoryId,
                    Description = asset.Description,
                    SerialNumber = asset.SerialNumber,
                    Manufacturer = asset.Manufacturer,
                    Model = asset.Model,
                    PurchaseDate = baseDate.AddYears(-4),
                    PurchasePrice = asset.PurchasePrice,
                    CurrentValue = asset.CurrentValue,
                    Status = asset.Status,
                    Criticality = asset.Criticality,
                    Location = asset.Location,
                    Mileage = asset.Mileage,
                    OperatingHours = asset.OperatingHours,
                    LastMileageUpdate = asset.Mileage > 0 ? baseDate.AddDays(-1) : null,
                    LastOperatingHoursUpdate = baseDate.AddDays(-1),
                    LicensePlate = string.IsNullOrWhiteSpace(asset.LicensePlate) ? null : asset.LicensePlate,
                    VIN = string.IsNullOrWhiteSpace(asset.Vin) ? null : asset.Vin,
                    FuelType = asset.IsFleet ? "Diesel" : null,
                    IsFleetAsset = asset.IsFleet,
                    LastServiceDate = baseDate.AddMonths(-3),
                    NextServiceDue = baseDate.AddDays(30),
                    TenantId = _defaultTenantId,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                });
            }
        }

        _logger.LogInformation($"✓ Seeded {assets.Length} maintenance assets");
    }

    private async Task SeedMaintenanceTypesAsync(DateTime baseDate)
    {
        _logger.LogInformation("Seeding Maintenance Types...");

        var maintenanceTypes = new[]
        {
            new { Id = _preventiveMaintenanceTypeId, Name = "Preventive Maintenance", Code = "PM", Category = "Preventive", Class = "Routine", Priority = 3, Hours = 4d, Cost = 500.00m, RequiresApproval = true, RequiresQuality = true, Color = "#16A34A" },
            new { Id = _correctiveMaintenanceTypeId, Name = "Corrective Maintenance", Code = "CM", Category = "Corrective", Class = "Repair", Priority = 2, Hours = 6d, Cost = 900.00m, RequiresApproval = true, RequiresQuality = true, Color = "#2563EB" },
            new { Id = _emergencyMaintenanceTypeId, Name = "Emergency Maintenance", Code = "EM", Category = "Emergency", Class = "Emergency", Priority = 1, Hours = 8d, Cost = 1500.00m, RequiresApproval = true, RequiresQuality = true, Color = "#DC2626" }
        };

        foreach (var type in maintenanceTypes)
        {
            if (!await _context.MaintenanceTypes.AnyAsync(m => m.Id == type.Id))
            {
                await _context.MaintenanceTypes.AddAsync(new MaintenanceType
                {
                    Id = type.Id,
                    Name = type.Name,
                    Code = type.Code,
                    Category = type.Category,
                    MaintenanceClass = type.Class,
                    Location = "Internal",
                    DefaultPriority = type.Priority,
                    EstimatedHours = type.Hours,
                    EstimatedCost = type.Cost,
                    FixedAmount = type.Cost,
                    RequiresApproval = type.RequiresApproval,
                    RequiresQualityCheck = type.RequiresQuality,
                    RequiresDocumentation = true,
                    RequiresSafetyPermit = type.Priority == 1,
                    IsActive = true,
                    Color = type.Color,
                    TenantId = _defaultTenantId,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                });
            }
        }

        _logger.LogInformation($"✓ Seeded {maintenanceTypes.Length} maintenance types");
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

        var workOrderTypes = new[]
        {
            new { Id = _preventiveWorkOrderTypeId, Name = "Preventive Maintenance", Code = "PM-WO", Priority = 3, Color = "#16A34A" },
            new { Id = _correctiveWorkOrderTypeId, Name = "Corrective Repair", Code = "CM-WO", Priority = 2, Color = "#2563EB" }
        };

        foreach (var type in workOrderTypes)
        {
            if (!await _context.WorkOrderTypes.AnyAsync(w => w.Id == type.Id))
            {
                await _context.WorkOrderTypes.AddAsync(new WorkOrderType
                {
                    Id = type.Id,
                    Name = type.Name,
                    Code = type.Code,
                    DefaultPriority = type.Priority,
                    RequiresApproval = true,
                    Color = type.Color,
                    IsActive = true,
                    TenantId = _defaultTenantId,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                });
            }
        }

        _logger.LogInformation($"✓ Seeded {workOrderTypes.Length} work order types");
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

        if (!await _context.QualityControlChecklists.AnyAsync(q => q.Name == "PM Quality Checklist"))
        {
            await _context.QualityControlChecklists.AddAsync(new QualityControlChecklist
            {
                Id = _qualityChecklistId,
                Name = "PM Quality Checklist",
                Description = "Standard final quality checks for seeded preventive and corrective work orders",
                WorkOrderType = "Preventive Maintenance",
                WorkOrderTypeId = _preventiveWorkOrderTypeId,
                AssetCategory = "Vehicle",
                AssetCategoryId = _vehicleCategoryId,
                MaintenanceType = "Preventive Maintenance",
                MaintenanceTypeId = _preventiveMaintenanceTypeId,
                IsMandatory = true,
                IsActive = true,
                MinimumPassingScore = 85,
                ChecklistItems = "[{\"item\":\"All work order tasks completed\",\"required\":true},{\"item\":\"Parts and labor recorded\",\"required\":true},{\"item\":\"Road test completed\",\"required\":true},{\"item\":\"Safety checks passed\",\"required\":true}]",
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

        if (!await _context.InspectionTemplates.AnyAsync(t => t.Id == _inspectionTemplateId))
        {
            await _context.InspectionTemplates.AddAsync(new InspectionTemplate
            {
                Id = _inspectionTemplateId,
                Name = "Vehicle PM Final Inspection",
                Code = "VEH-PM-FINAL",
                Description = "Final inspection template used by the maintenance E2E seed workflow",
                Category = "Vehicle",
                Frequency = "AdHoc",
                InspectionType = "Maintenance",
                EstimatedDuration = 45,
                RequiresSignature = true,
                AllowPhotos = true,
                Version = "1.0",
                Priority = "High",
                AssetTypes = "[\"Vehicle\"]",
                InspectorRoles = "[\"QualityInspector\",\"Supervisor\"]",
                ChecklistItems = "[{\"label\":\"Engine bay clean and leak free\",\"type\":\"boolean\"},{\"label\":\"Brakes tested\",\"type\":\"boolean\"},{\"label\":\"Road test completed\",\"type\":\"boolean\"}]",
                IsActive = true,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        if (!await _context.InspectionChecklistTemplates.AnyAsync(t => t.Name == "Vehicle PM Inspection"))
        {
            await _context.InspectionChecklistTemplates.AddAsync(new InspectionChecklistTemplate
            {
                Id = Guid.NewGuid(),
                Name = "Vehicle PM Inspection",
                Description = "Reusable checklist template for vehicle preventive maintenance",
                Category = "Quality",
                IsActive = true,
                IsDefault = true,
                Version = 1,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        var checklistTemplate = await _context.InspectionChecklistTemplates
            .FirstOrDefaultAsync(t => t.Name == "Vehicle PM Inspection");

        if (checklistTemplate != null)
        {
            var items = new[]
            {
                new { Text = "Confirm all required tasks are completed", Sort = 1, Category = "Completion" },
                new { Text = "Verify no active leaks or warning lights remain", Sort = 2, Category = "Quality" },
                new { Text = "Confirm photos/documents are attached", Sort = 3, Category = "Documentation" }
            };

            foreach (var item in items)
            {
                if (!await _context.InspectionChecklistItems.AnyAsync(i =>
                    i.TemplateId == checklistTemplate.Id && i.ItemText == item.Text))
                {
                    await _context.InspectionChecklistItems.AddAsync(new InspectionChecklistItem
                    {
                        Id = Guid.NewGuid(),
                        TemplateId = checklistTemplate.Id,
                        ItemText = item.Text,
                        Category = item.Category,
                        ItemType = "Boolean",
                        IsRequired = true,
                        SortOrder = item.Sort,
                        TenantId = _defaultTenantId,
                        CreatedAt = baseDate,
                        CreatedBy = "System"
                    });
                }
            }
        }

        _logger.LogInformation("✓ Seeded inspection templates and checklist items");
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

    private async Task SeedMaintenanceWorkflowAsync(DateTime baseDate)
    {
        _logger.LogInformation("Seeding maintenance E2E workflow records...");

        var tenantUserId = await ResolveTenantUserIdAsync();

        await SeedAssetTypeConfigurationAsync(baseDate);
        await SeedTeamSkillsAndSafetyMastersAsync(baseDate);
        await SeedMaintenanceToolsAsync(baseDate);
        await SeedPreInspectionConfigurationAsync(baseDate);
        await SeedJobCardAndWorkOrderAsync(baseDate, tenantUserId);

        if (tenantUserId.HasValue)
        {
            await SeedAdmissionRecordsAsync(baseDate, tenantUserId.Value);
        }
        else
        {
            _logger.LogWarning("No application user found for tenant {TenantId}; skipped admission/discharge records that require a login user.", _defaultTenantId);
        }

        await SeedWorkOrderExecutionRecordsAsync(baseDate, tenantUserId);
        await SeedScheduleUsageAndDowntimeAsync(baseDate);
        await SeedInspectionAndQualityRecordsAsync(baseDate);
        await SeedSafetyAndNotificationRecordsAsync(baseDate, tenantUserId);

        if (tenantUserId.HasValue)
        {
            await SeedDischargeAndCompletionRecordsAsync(baseDate, tenantUserId.Value);
        }

        _logger.LogInformation("✓ Seeded maintenance E2E workflow records");
    }

    private async Task<Guid?> ResolveTenantUserIdAsync()
    {
        var tenantUserId = await _context.Users
            .AsNoTracking()
            .Where(u => u.TenantId == _defaultTenantId)
            .OrderBy(u => u.CreatedAt)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync();

        if (tenantUserId.HasValue)
        {
            return tenantUserId;
        }

        return await _context.Users
            .AsNoTracking()
            .OrderBy(u => u.CreatedAt)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync();
    }

    private async Task SeedAssetTypeConfigurationAsync(DateTime baseDate)
    {
        var vehicleFields = new[]
        {
            new { Field = "odometerReading", Display = "Odometer Reading", Type = "Number", Order = 1, Unit = "miles" },
            new { Field = "licensePlate", Display = "License Plate", Type = "Text", Order = 2, Unit = "" },
            new { Field = "fuelType", Display = "Fuel Type", Type = "Dropdown", Order = 3, Unit = "[\"Diesel\",\"Petrol\",\"Hybrid\",\"Electric\"]" }
        };

        foreach (var field in vehicleFields)
        {
            if (!await _context.AssetTypeFields.AnyAsync(f => f.AssetTypeId == _vehicleAssetTypeId && f.FieldName == field.Field))
            {
                await _context.AssetTypeFields.AddAsync(new AssetTypeField
                {
                    Id = Guid.NewGuid(),
                    AssetTypeId = _vehicleAssetTypeId,
                    FieldName = field.Field,
                    DisplayName = field.Display,
                    FieldType = field.Type,
                    IsRequired = field.Order < 3,
                    DisplayOrder = field.Order,
                    Options = field.Type == "Dropdown" ? field.Unit : null,
                    HelpText = field.Type == "Number" ? $"Enter value in {field.Unit}." : null,
                    IsActive = true,
                    TenantId = _defaultTenantId,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                });
            }
        }

        var assetTaskTemplates = new[]
        {
            new { Task = "Record vehicle admission readings", Sequence = 1, Hours = 0.25, AssetId = _deliveryTruckId },
            new { Task = "Road test after service", Sequence = 7, Hours = 0.50, AssetId = _deliveryTruckId }
        };

        foreach (var template in assetTaskTemplates)
        {
            if (!await _context.AssetTaskTemplates.AnyAsync(t =>
                t.AssetId == template.AssetId &&
                t.MaintenanceTypeId == _preventiveMaintenanceTypeId &&
                t.TaskName == template.Task))
            {
                await _context.AssetTaskTemplates.AddAsync(new AssetTaskTemplate
                {
                    Id = Guid.NewGuid(),
                    AssetId = template.AssetId,
                    MaintenanceTypeId = _preventiveMaintenanceTypeId,
                    TaskName = template.Task,
                    Sequence = template.Sequence,
                    EstimatedHours = template.Hours,
                    IsRequired = true,
                    AssignedTechnicianId = _seniorTechnicianId,
                    Instructions = "Seeded E2E task template",
                    SafetyRequirements = "Use PPE and standard workshop procedures.",
                    RequiredTools = "[\"Diagnostic scanner\"]",
                    RequiredParts = "[]",
                    IsActive = true,
                    TenantId = _defaultTenantId,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                });
            }
        }

        if (!await _context.AssetTypeTaskTemplates.AnyAsync(t =>
            t.AssetTypeId == _vehicleAssetTypeId &&
            t.MaintenanceTypeId == _preventiveMaintenanceTypeId &&
            t.TaskName == "Vehicle service checklist review"))
        {
            await _context.AssetTypeTaskTemplates.AddAsync(new AssetTypeTaskTemplate
            {
                Id = Guid.NewGuid(),
                AssetTypeId = _vehicleAssetTypeId,
                MaintenanceTypeId = _preventiveMaintenanceTypeId,
                TaskName = "Vehicle service checklist review",
                Description = "Generic vehicle service review task",
                Sequence = 99,
                EstimatedHours = 0.25,
                IsRequired = true,
                AssignedTechnicianId = _seniorTechnicianId,
                Instructions = "Confirm all vehicle PM tasks are recorded before quality check.",
                SafetyRequirements = "Vehicle must be secured before inspection.",
                RequiredTools = "[\"Diagnostic scanner\"]",
                RequiredParts = "[]",
                IsActive = true,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }
    }

    private async Task SeedTeamSkillsAndSafetyMastersAsync(DateTime baseDate)
    {
        if (!await _context.TechnicianTeams.AnyAsync(t => t.Id == _maintenanceTeamId))
        {
            await _context.TechnicianTeams.AddAsync(new TechnicianTeam
            {
                Id = _maintenanceTeamId,
                Name = "E2E Vehicle Maintenance Team",
                Description = "Seeded team for end-to-end maintenance testing",
                TeamLeaderId = _seniorTechnicianId,
                Status = "Active",
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        foreach (var member in new[]
        {
            new { TechnicianId = _seniorTechnicianId, Role = "Leader" },
            new { TechnicianId = _juniorTechnicianId, Role = "Member" }
        })
        {
            if (!await _context.TechnicianTeamMembers.AnyAsync(m => m.TeamId == _maintenanceTeamId && m.TechnicianId == member.TechnicianId))
            {
                await _context.TechnicianTeamMembers.AddAsync(new TechnicianTeamMember
                {
                    Id = Guid.NewGuid(),
                    TeamId = _maintenanceTeamId,
                    TechnicianId = member.TechnicianId,
                    Role = member.Role,
                    JoinedDate = baseDate.AddMonths(-6),
                    IsActive = true,
                    TenantId = _defaultTenantId,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                });
            }
        }

        if (!await _context.TechnicianSkills.AnyAsync(s => s.Id == _technicianSkillId))
        {
            await _context.TechnicianSkills.AddAsync(new TechnicianSkill
            {
                Id = _technicianSkillId,
                Name = "Diesel Vehicle Maintenance",
                Description = "Diesel engine preventive and corrective maintenance",
                Category = "Mechanical",
                IsActive = true,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        if (!await _context.UserTechnicianSkills.AnyAsync(s => s.EmployeeId == _seniorTechnicianId && s.SkillId == _technicianSkillId))
        {
            await _context.UserTechnicianSkills.AddAsync(new UserTechnicianSkill
            {
                Id = Guid.NewGuid(),
                EmployeeId = _seniorTechnicianId,
                SkillId = _technicianSkillId,
                ProficiencyLevel = 4,
                CertificationDate = baseDate.AddYears(-2),
                CertificationExpiry = baseDate.AddYears(1),
                CertifyingBody = "Maintenance Academy",
                CertificationNumber = "E2E-DIESEL-001",
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        if (!await _context.TechnicalSkills.AnyAsync(s => s.Id == _technicalSkillId))
        {
            await _context.TechnicalSkills.AddAsync(new TechnicalSkill
            {
                Id = _technicalSkillId,
                Name = "Brake and Lubrication Service",
                Code = "BRK-LUB",
                Description = "Brake inspection, lubrication service, and PM documentation",
                Category = "Vehicle",
                SkillLevel = "Advanced",
                Complexity = "Medium",
                RiskLevel = "Medium",
                ToolsRequired = "[\"PPE\",\"Wheel chocks\",\"Diagnostic scanner\"]",
                SafetyRequirements = "Lock vehicle, apply wheel chocks, and wear PPE.",
                CompetencyAreas = "[\"Brakes\",\"Lubrication\",\"Inspection\"]",
                RelatedMaintenanceTypes = "[\"PM\",\"CM\"]",
                IsActive = true,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        if (!await _context.TechnicianSkillAssignments.AnyAsync(s => s.TechnicianId == _seniorTechnicianId && s.SkillId == _technicalSkillId))
        {
            await _context.TechnicianSkillAssignments.AddAsync(new TechnicianSkillAssignment
            {
                Id = Guid.NewGuid(),
                TechnicianId = _seniorTechnicianId,
                SkillId = _technicalSkillId,
                ProficiencyLevel = 4,
                ProficiencyDescription = "Advanced",
                AcquiredDate = baseDate.AddYears(-2),
                ExpirationDate = baseDate.AddYears(1),
                IsVerified = true,
                VerifiedBy = "System",
                LastAssessmentDate = baseDate.AddMonths(-1),
                Notes = "Seeded skill assignment for maintenance E2E testing",
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        if (!await _context.TechnicianCertifications.AnyAsync(c => c.Id == _technicianCertificationId))
        {
            await _context.TechnicianCertifications.AddAsync(new TechnicianCertification
            {
                Id = _technicianCertificationId,
                TechnicianId = _seniorTechnicianId,
                CertificationName = "Heavy Vehicle PM Certification",
                CertificationNumber = "HVPM-E2E-001",
                IssuingOrganization = "Maintenance Academy",
                IssueDate = baseDate.AddYears(-1),
                ExpirationDate = baseDate.AddYears(1),
                Status = "Active",
                CertificationLevel = "Level 3",
                Category = "Technical",
                Description = "Seeded certification for E2E maintenance testing",
                Requirements = "Annual renewal",
                RenewalRequirements = "Refresher assessment",
                Cost = 250m,
                IsMandatory = true,
                IsVerified = true,
                VerifiedBy = "System",
                VerificationDate = baseDate.AddMonths(-6),
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        if (!await _context.SafetyProtocols.AnyAsync(p => p.Id == _safetyProtocolId))
        {
            await _context.SafetyProtocols.AddAsync(new SafetyProtocol
            {
                Id = _safetyProtocolId,
                Name = "Vehicle Lockout and PPE",
                Code = "SAFE-VEH-LOTO",
                Description = "Seeded vehicle safety procedure for maintenance workflow testing",
                Category = "PPE",
                Severity = "High",
                RegulatoryStandard = "Internal Safety Standard",
                Procedures = "Secure vehicle, apply wheel chocks, isolate ignition, and wear PPE before work.",
                RequiredEquipment = "[\"Gloves\",\"Safety glasses\",\"Wheel chocks\"]",
                RequiredTraining = "[\"Workshop safety\"]",
                RequiredCertifications = "[\"HVPM-E2E-001\"]",
                EmergencyProcedures = "Stop work and notify supervisor.",
                PreventiveMeasures = "Complete pre-work safety checklist.",
                ApplicableMaintenanceTypes = "[\"PM\",\"CM\"]",
                ApplicableAssetTypes = "[\"Vehicle\"]",
                IsMandatory = true,
                IsActive = true,
                ReviewDate = baseDate.AddMonths(-1),
                NextReviewDate = baseDate.AddMonths(11),
                ReviewFrequencyMonths = 12,
                MinimumTrainingLevel = "Basic",
                EffectiveDate = baseDate.AddYears(-1),
                ReviewedBy = "System",
                IsRegulatory = false,
                ComplianceCheckpoints = "[\"PPE worn\",\"Vehicle secured\",\"Tools inspected\"]",
                ApprovalStatus = "Approved",
                ApprovedBy = "System",
                ApprovalDate = baseDate.AddMonths(-1),
                Version = "1.0",
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }
    }

    private async Task SeedMaintenanceToolsAsync(DateTime baseDate)
    {
        if (!await _context.MaintenanceTools.AnyAsync(t => t.Id == _maintenanceToolId))
        {
            await _context.MaintenanceTools.AddAsync(new MaintenanceTool
            {
                Id = _maintenanceToolId,
                ToolCode = "TOOL-SCAN-001",
                Name = "Heavy Duty Diagnostic Scanner",
                Description = "Seeded maintenance tool record; work-order allocations use the linked inventory fixed-asset item.",
                Category = "Diagnostic",
                Manufacturer = "Autel",
                Model = "HD-E2E",
                SerialNumber = "SCAN-E2E-001",
                Status = "Available",
                CurrentLocation = "Workshop Tool Room",
                HomeLocation = "Workshop Tool Room",
                LastMaintenanceDate = baseDate.AddMonths(-2),
                NextMaintenanceDate = baseDate.AddMonths(10),
                LastCalibrationDate = baseDate.AddMonths(-2),
                NextCalibrationDate = baseDate.AddMonths(10),
                PurchasePrice = 850m,
                CurrentValue = 700m,
                DailyRentalRate = 25m,
                RequiresCertification = true,
                RequiresTraining = true,
                SafetyNotes = "Use by trained technicians only.",
                DocumentPaths = "[\"/seeded/maintenance/tools/diagnostic-scanner-manual.pdf\"]",
                IsActive = true,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }
    }

    private async Task SeedPreInspectionConfigurationAsync(DateTime baseDate)
    {
        if (!await _context.PreInspectionChecklistTemplates.AnyAsync(t => t.Id == _preInspectionTemplateId))
        {
            await _context.PreInspectionChecklistTemplates.AddAsync(new PreInspectionChecklistTemplate
            {
                Id = _preInspectionTemplateId,
                Name = "Vehicle Admission and Discharge Checklist",
                Description = "Seeded condition checklist for maintenance admission/discharge testing",
                Category = "Vehicle",
                AssetCategoryId = _vehicleCategoryId,
                IsActive = true,
                IsDefault = true,
                SortOrder = 1,
                Version = 1,
                VersionNotes = "E2E seed",
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        var items = new[]
        {
            new { Name = "Fuel level", Type = "Numeric", Sort = 1, Unit = "%", Options = "" },
            new { Name = "Exterior body condition", Type = "Choice", Sort = 2, Unit = "", Options = "[\"Excellent\",\"Good\",\"Fair\",\"Poor\"]" },
            new { Name = "Spare tire present", Type = "Boolean", Sort = 3, Unit = "", Options = "" }
        };

        foreach (var item in items)
        {
            if (!await _context.PreInspectionChecklistItems.AnyAsync(i => i.TemplateId == _preInspectionTemplateId && i.ItemName == item.Name))
            {
                await _context.PreInspectionChecklistItems.AddAsync(new PreInspectionChecklistItem
                {
                    Id = Guid.NewGuid(),
                    TemplateId = _preInspectionTemplateId,
                    ItemName = item.Name,
                    Category = "Vehicle",
                    ItemType = item.Type,
                    IsRequired = true,
                    SortOrder = item.Sort,
                    ChoiceOptions = string.IsNullOrWhiteSpace(item.Options) ? null : item.Options,
                    Unit = string.IsNullOrWhiteSpace(item.Unit) ? null : item.Unit,
                    RequiresPhoto = item.Sort == 2,
                    AllowRepairReplacement = item.Sort == 2,
                    DefaultRepairReplacementAction = "None",
                    TenantId = _defaultTenantId,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                });
            }
        }
    }

    private async Task SeedJobCardAndWorkOrderAsync(DateTime baseDate, Guid? tenantUserId)
    {
        if (!await _context.JobCards.AnyAsync(j => j.Id == _jobCardId))
        {
            await _context.JobCards.AddAsync(new JobCard
            {
                Id = _jobCardId,
                JobCardNumber = "E2E-JC-0001",
                AssetId = _deliveryTruckId,
                MaintenanceTypeId = _preventiveMaintenanceTypeId,
                PriorityLevelId = _mediumPriorityId,
                Title = "E2E Preventive Maintenance - Delivery Truck TRK-001",
                Description = "Seeded job card for full maintenance workflow testing",
                ProblemDescription = "Routine service due: oil, filters, brake check, road test, and final quality sign-off.",
                MaintenanceLocation = "Internal",
                RequestedById = _fleetManagerId,
                RequestedDate = baseDate.AddDays(-7),
                RequiredCompletionDate = baseDate.AddDays(1),
                EstimatedHours = 4.5,
                EstimatedCost = 620m,
                PreferredTechnicianId = _seniorTechnicianId,
                RequiresSpecialTools = true,
                RequiresShutdown = false,
                RequiresSafetyPermit = false,
                SpecialInstructions = "Use seeded inventory parts and final quality checklist.",
                SafetyRequirements = "Follow SAFE-VEH-LOTO.",
                JobCardStatus = "Approved",
                ApprovalStatus = "Approved",
                SubmittedById = _fleetManagerId,
                SubmittedDate = baseDate.AddDays(-7),
                ApprovedById = _maintenanceSupervisorId,
                ApprovedDate = baseDate.AddDays(-6),
                ApprovalComments = "Approved for E2E maintenance testing.",
                PlannedStartDate = baseDate.AddDays(-5),
                PlannedEndDate = baseDate.AddDays(-1),
                AssignedTechnicianId = _seniorTechnicianId,
                AssignedTeamId = _maintenanceTeamId,
                AssetConditionOnAdmission = "Good",
                MileageReadingOnAdmission = 85000m,
                HoursReadingOnAdmission = 3200m,
                FuelLevelOnAdmission = 60m,
                AdmissionNotes = "Seeded admission notes for E2E workflow.",
                BayOrStation = "Bay 3",
                CompletedDate = null,
                CompletionNotes = null,
                AssetConditionOnCompletion = null,
                MileageReadingOnCompletion = null,
                HoursReadingOnCompletion = null,
                FuelLevelOnCompletion = null,
                WorkCompletedSummary = null,
                RemainingIssues = null,
                QualityCheckPassed = false,
                QualityCheckedById = null,
                QualityCheckDate = null,
                QualityCheckNotes = null,
                CertificateGenerated = false,
                CertificateGeneratedDate = null,
                CustomerAcceptance = false,
                AcceptedById = null,
                AcceptedDate = null,
                AcceptanceNotes = null,
                RequiresFollowUp = true,
                FollowUpDate = baseDate.AddMonths(3),
                FollowUpInstructions = "Run next preventive maintenance cycle.",
                WarrantyDays = 90,
                WarrantyExpiration = baseDate.AddDays(90),
                WarrantyTerms = "Seeded warranty on maintenance workmanship.",
                AttachmentPaths = "[\"/seeded/maintenance/jobcards/e2e-jc-0001.pdf\"]",
                CustomFieldValues = "{\"source\":\"MaintenanceE2ETestSeeder\"}",
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddDays(-7),
                CreatedBy = "System"
            });

            await _context.SaveChangesAsync();
        }

        if (!await _context.WorkOrders.AnyAsync(w => w.Id == _workOrderId))
        {
            await _context.WorkOrders.AddAsync(new WorkOrder
            {
                Id = _workOrderId,
                WorkOrderNumber = "E2E-WO-0001",
                Title = "E2E Preventive Maintenance - Delivery Truck TRK-001",
                Description = "Seeded work order generated from E2E-JC-0001.",
                JobCardId = _jobCardId,
                AssetId = _deliveryTruckId,
                WorkOrderTypeId = _preventiveWorkOrderTypeId,
                MaintenanceTypeId = _preventiveMaintenanceTypeId,
                PriorityLevelId = _mediumPriorityId,
                Status = "InProgress",
                MaintenanceLocation = "Internal",
                AssignedTechnicianId = _seniorTechnicianId,
                AssignedTeamId = _maintenanceTeamId,
                RequestedStartDate = baseDate.AddDays(-5),
                RequestedCompletionDate = baseDate.AddDays(-1),
                ActualStartDate = baseDate.AddDays(-5),
                ActualCompletionDate = null,
                EstimatedCost = 620m,
                ActualCost = 0m,
                BillingType = "Repairs",
                EstimatedHours = 4.5,
                ActualHours = 0,
                RequestedById = tenantUserId,
                ApprovedById = tenantUserId,
                ApprovedAt = baseDate.AddDays(-6),
                SupervisorId = tenantUserId,
                CompletedById = null,
                QualityCheckedById = null,
                CompletionNotes = null,
                SafetyRequirements = "Follow SAFE-VEH-LOTO.",
                RequiresPermit = false,
                RequiresLockout = true,
                IsRecurring = false,
                CustomFields = "{\"seed\":\"maintenance-e2e\"}",
                CustomFieldValues = "{\"odometerReading\":85012,\"licensePlate\":\"E2E-TRK-001\"}",
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddDays(-6),
                CreatedBy = "System"
            });

            await _context.SaveChangesAsync();
        }

        var jobCard = await _context.JobCards.FirstAsync(j => j.Id == _jobCardId);
        if (jobCard.GeneratedWorkOrderId != _workOrderId)
        {
            jobCard.GeneratedWorkOrderId = _workOrderId;
            jobCard.WorkOrderGeneratedAt = baseDate.AddDays(-6);
        }

        var approvalSteps = new[]
        {
            new { Step = 1, Name = "Supervisor Review", ApproverId = _maintenanceSupervisorId, Comments = "Approved by seeded supervisor." },
            new { Step = 2, Name = "Maintenance Manager Approval", ApproverId = _maintenanceManagerId, Comments = "Approved for execution." }
        };

        foreach (var step in approvalSteps)
        {
            if (!await _context.JobCardApprovalSteps.AnyAsync(s => s.JobCardId == _jobCardId && s.StepOrder == step.Step))
            {
                await _context.JobCardApprovalSteps.AddAsync(new JobCardApprovalStep
                {
                    Id = Guid.NewGuid(),
                    JobCardId = _jobCardId,
                    StepOrder = step.Step,
                    StepName = step.Name,
                    ApproverId = step.ApproverId,
                    Status = "Approved",
                    ActionDate = baseDate.AddDays(-6),
                    Comments = step.Comments,
                    IsRequired = true,
                    TenantId = _defaultTenantId,
                    CreatedAt = baseDate.AddDays(-6),
                    CreatedBy = "System"
                });
            }
        }

        if (!await _context.JobCardComments.AnyAsync(c => c.JobCardId == _jobCardId && c.CommentType == "Approval"))
        {
            await _context.JobCardComments.AddAsync(new JobCardComment
            {
                Id = Guid.NewGuid(),
                JobCardId = _jobCardId,
                CommentById = _maintenanceSupervisorId,
                Comment = "Seeded job card approved and converted to work order.",
                CommentType = "Approval",
                IsInternal = true,
                CommentDate = baseDate.AddDays(-6),
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddDays(-6),
                CreatedBy = "System"
            });
        }

        if (!await _context.JobCardDocuments.AnyAsync(d => d.JobCardId == _jobCardId && d.FileName == "e2e-job-card-request.pdf"))
        {
            await _context.JobCardDocuments.AddAsync(new JobCardDocument
            {
                Id = Guid.NewGuid(),
                JobCardId = _jobCardId,
                FileName = "e2e-job-card-request.pdf",
                FilePath = "/seeded/maintenance/jobcards/e2e-job-card-request.pdf",
                ContentType = "application/pdf",
                FileSize = 24576,
                DocumentType = "Report",
                Description = "Seeded job card request document",
                UploadedById = _fleetManagerId,
                UploadedDate = baseDate.AddDays(-7),
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddDays(-7),
                CreatedBy = "System"
            });
        }

        if (!await _context.JobCardCertificates.AnyAsync(c => c.Id == _jobCardCertificateId))
        {
            await _context.JobCardCertificates.AddAsync(new JobCardCertificate
            {
                Id = _jobCardCertificateId,
                JobCardId = _jobCardId,
                CertificateNumber = "E2E-CERT-JC-0001",
                AssetId = _deliveryTruckId,
                CertificateType = "Maintenance Completion",
                IssuedDate = baseDate,
                ValidUntil = baseDate.AddMonths(3),
                IssuedById = _qualityInspectorId,
                FilePath = "/seeded/maintenance/certificates/e2e-cert-jc-0001.pdf",
                FileFormat = "PDF",
                Description = "Seeded job card completion certificate",
                CertificateData = "{\"workOrder\":\"E2E-WO-0001\",\"result\":\"Pass\"}",
                IsActive = true,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }
    }

    private async Task SeedWorkOrderExecutionRecordsAsync(DateTime baseDate, Guid? tenantUserId)
    {
        var templates = await _context.MaintenanceTaskTemplates
            .AsNoTracking()
            .Where(t => t.MaintenanceTypeId == _preventiveMaintenanceTypeId)
            .OrderBy(t => t.Sequence)
            .ToListAsync();

        foreach (var template in templates)
        {
            if (!await _context.WorkOrderTasks.AnyAsync(t => t.WorkOrderId == _workOrderId && t.TaskName == template.TaskName))
            {
                await _context.WorkOrderTasks.AddAsync(new WorkOrderTask
                {
                    Id = Guid.NewGuid(),
                    WorkOrderId = _workOrderId,
                    TaskName = template.TaskName,
                    Description = template.Description,
                    Sequence = template.Sequence,
                    Status = "Completed",
                    EstimatedHours = template.EstimatedHours,
                    ActualHours = template.EstimatedHours,
                    AssignedTechnicianId = _seniorTechnicianId,
                    StartedAt = baseDate.AddDays(-5).AddHours(template.Sequence),
                    CompletedAt = baseDate.AddDays(-5).AddHours(template.Sequence).AddMinutes(30),
                    CompletionNotes = "Seeded task completed.",
                    IsRequired = template.IsRequired,
                    TenantId = _defaultTenantId,
                    CreatedAt = baseDate.AddDays(-5),
                    CreatedBy = "System"
                });
            }
        }

        var parts = new[]
        {
            new { ItemId = _engineOilPartId, Required = 5m, Used = 5m },
            new { ItemId = _oilFilterPartId, Required = 1m, Used = 1m },
            new { ItemId = _airFilterPartId, Required = 1m, Used = 1m }
        };

        foreach (var part in parts)
        {
            if (!await _context.WorkOrderParts.AnyAsync(p => p.WorkOrderId == _workOrderId && p.InventoryItemId == part.ItemId))
            {
                var item = await _context.InventoryItems.AsNoTracking().FirstAsync(i => i.Id == part.ItemId);
                var unitCost = item.StandardCost > 0 ? item.StandardCost : item.SalePrice;
                await _context.WorkOrderParts.AddAsync(new WorkOrderPart
                {
                    Id = Guid.NewGuid(),
                    WorkOrderId = _workOrderId,
                    InventoryItemId = item.Id,
                    ItemCode = item.ItemCode,
                    ItemName = item.Name,
                    Description = item.Description,
                    QuantityRequired = part.Required,
                    QuantityAllocated = part.Required,
                    QuantityUsed = part.Used,
                    UnitCost = unitCost,
                    TotalCost = unitCost * part.Used,
                    Status = "Used",
                    AllocatedAt = baseDate.AddDays(-5),
                    PickedAt = baseDate.AddDays(-5).AddHours(1),
                    UsedAt = baseDate.AddDays(-5).AddHours(2),
                    Notes = "Seeded part usage for E2E work order.",
                    TenantId = _defaultTenantId,
                    CreatedAt = baseDate.AddDays(-5),
                    CreatedBy = "System"
                });
            }
        }

        if (!await _context.WorkOrderLabor.AnyAsync(l => l.Id == _workOrderLaborId))
        {
            await _context.WorkOrderLabor.AddAsync(new WorkOrderLabor
            {
                Id = _workOrderLaborId,
                WorkOrderId = _workOrderId,
                TechnicianId = _seniorTechnicianId,
                StartTime = baseDate.AddDays(-5).AddHours(8),
                EndTime = baseDate.AddDays(-5).AddHours(12.25),
                Hours = 4.25,
                HourlyRate = 45m,
                TotalCost = 191.25m,
                Notes = "Seeded labor line for E2E work order.",
                LaborType = "Regular",
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddDays(-5),
                CreatedBy = "System"
            });
        }

        if (!await _context.WorkOrderComments.AnyAsync(c => c.WorkOrderId == _workOrderId && c.CommentType == "Resolution"))
        {
            await _context.WorkOrderComments.AddAsync(new WorkOrderComment
            {
                Id = Guid.NewGuid(),
                WorkOrderId = _workOrderId,
                EmployeeId = _seniorTechnicianId,
                Comment = "Seeded work order completed; ready for quality sign-off.",
                CommentType = "Resolution",
                IsInternal = false,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddDays(-1),
                CreatedBy = "System"
            });
        }

        if (!await _context.WorkOrderDocuments.AnyAsync(d => d.WorkOrderId == _workOrderId && d.FileName == "e2e-work-order-report.pdf"))
        {
            await _context.WorkOrderDocuments.AddAsync(new WorkOrderDocument
            {
                Id = Guid.NewGuid(),
                WorkOrderId = _workOrderId,
                FileName = "e2e-work-order-report.pdf",
                FilePath = "/seeded/maintenance/workorders/e2e-work-order-report.pdf",
                FileType = "application/pdf",
                FileSize = 32768,
                Description = "Seeded work order completion report",
                DocumentType = "Report",
                UploadedById = _seniorTechnicianId,
                UploadedAt = baseDate.AddDays(-1),
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddDays(-1),
                CreatedBy = "System"
            });
        }

        if (!await _context.ToolCheckouts.AnyAsync(c => c.Id == _toolCheckoutId))
        {
            await _context.ToolCheckouts.AddAsync(new ToolCheckout
            {
                Id = _toolCheckoutId,
                ToolId = _diagnosticScannerPartId,
                CheckedOutById = _seniorTechnicianId,
                WorkOrderId = _workOrderId,
                JobCardId = _jobCardId,
                CheckoutDate = baseDate.AddDays(-5),
                ExpectedReturnDate = baseDate.AddDays(-4),
                ActualReturnDate = baseDate.AddDays(-4),
                CheckedInById = _seniorTechnicianId,
                Status = "Returned",
                CheckoutNotes = "Seeded diagnostic scanner checkout.",
                ReturnNotes = "Returned in good condition.",
                ConditionOnCheckout = "Good",
                ConditionOnReturn = "Good",
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddDays(-5),
                CreatedBy = "System"
            });
        }

        if (!await _context.WorkOrderTools.AnyAsync(t => t.Id == _workOrderToolId))
        {
            await _context.WorkOrderTools.AddAsync(new WorkOrderTool
            {
                Id = _workOrderToolId,
                WorkOrderId = _workOrderId,
                ToolId = _diagnosticScannerPartId,
                IsRequired = true,
                IsAllocated = true,
                AllocationDate = baseDate.AddDays(-5),
                CheckoutId = _toolCheckoutId,
                Notes = "Diagnostic scanner used for seeded E2E work order.",
                IsExcludedFromBilling = true,
                BillingExclusionReason = "Company-owned tool",
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddDays(-5),
                CreatedBy = "System"
            });
        }

        if (!await _context.MaintenanceStaffSchedules.AnyAsync(s => s.Id == _maintenanceStaffScheduleId))
        {
            await _context.MaintenanceStaffSchedules.AddAsync(new MaintenanceStaffSchedule
            {
                Id = _maintenanceStaffScheduleId,
                TechnicianId = _seniorTechnicianId,
                StartDateTime = baseDate.AddDays(-5).AddHours(8),
                EndDateTime = baseDate.AddDays(-5).AddHours(13),
                ScheduleType = "WorkOrder",
                Status = "Completed",
                WorkOrderId = _workOrderId,
                JobCardId = _jobCardId,
                TeamId = _maintenanceTeamId,
                WorkLocation = "Workshop Bay 3",
                Notes = "Seeded staff schedule for E2E work order.",
                ActualStartTime = baseDate.AddDays(-5).AddHours(8),
                ActualEndTime = baseDate.AddDays(-5).AddHours(12.25),
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddDays(-5),
                CreatedBy = "System"
            });
        }

        if (!await _context.MaintenanceExpenses.AnyAsync(e => e.Id == _maintenanceExpenseId))
        {
            await _context.MaintenanceExpenses.AddAsync(new MaintenanceExpense
            {
                Id = _maintenanceExpenseId,
                WorkOrderId = _workOrderId,
                ScheduleId = _maintenanceStaffScheduleId,
                TechnicianId = _seniorTechnicianId,
                ExpenseType = "Fuel",
                Description = "Fuel used during seeded road test",
                Amount = 35m,
                ExpenseDate = baseDate.AddDays(-5),
                MileageDriven = 12m,
                MileageRate = 0.65m,
                VendorName = "Internal Fuel Store",
                ReferenceNumber = "E2E-EXP-0001",
                Status = "Approved",
                ApprovedById = _maintenanceSupervisorId,
                ApprovedDate = baseDate.AddDays(-4),
                ApprovalNotes = "Seeded expense approved.",
                IsReimbursable = false,
                VehicleId = _deliveryTruckId,
                Location = "Workshop Bay 3",
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddDays(-5),
                CreatedBy = "System"
            });
        }

        if (!await _context.MaintenanceAttachments.AnyAsync(a => a.Id == _maintenanceAttachmentId))
        {
            await _context.MaintenanceAttachments.AddAsync(new MaintenanceAttachment
            {
                Id = _maintenanceAttachmentId,
                FileName = "e2e-after-service-photo.jpg",
                FilePath = "/seeded/maintenance/workorders/e2e-after-service-photo.jpg",
                ContentType = "image/jpeg",
                FileSizeBytes = 102400,
                Description = "Seeded after-service photo attachment",
                AttachmentType = AttachmentType.Photo,
                EntityType = AttachmentEntityType.WorkOrder,
                EntityId = _workOrderId,
                UploadedDate = baseDate.AddDays(-1),
                UploadedByUserId = _seniorTechnicianId,
                IsMainImage = true,
                ImageWidth = 1280,
                ImageHeight = 720,
                ThumbnailPath = "/seeded/maintenance/workorders/e2e-after-service-photo-thumb.jpg",
                LocationDescription = "Workshop Bay 3",
                CreatedAt = baseDate.AddDays(-1),
                CreatedBy = "System"
            });
        }

        if (!await _context.MaintenanceAttachmentAccessLogs.AnyAsync(a => a.Id == _maintenanceAttachmentAccessId))
        {
            await _context.MaintenanceAttachmentAccessLogs.AddAsync(new MaintenanceAttachmentAccess
            {
                Id = _maintenanceAttachmentAccessId,
                AttachmentId = _maintenanceAttachmentId,
                AccessedByUserId = _maintenanceSupervisorId,
                AccessedDate = baseDate,
                AccessType = AttachmentAccessType.View,
                UserAgent = "MaintenanceE2ESeeder",
                IpAddress = "127.0.0.1",
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }
    }

    private async Task SeedScheduleUsageAndDowntimeAsync(DateTime baseDate)
    {
        if (!await _context.MaintenanceSchedules.AnyAsync(s => s.Id == _maintenanceScheduleId))
        {
            await _context.MaintenanceSchedules.AddAsync(new MaintenanceSchedule
            {
                Id = _maintenanceScheduleId,
                Name = "E2E Truck Quarterly PM",
                Code = "E2E-PM-TRK",
                Description = "Seeded recurring PM schedule for E2E testing",
                AssetId = _deliveryTruckId,
                MaintenanceTypeId = _preventiveMaintenanceTypeId,
                Frequency = "Quarterly",
                FrequencyValue = 3,
                FrequencyUnit = "Months",
                PrimaryTriggerType = "Combined",
                SecondaryTriggerType = "Usage",
                TriggerLogic = "OR",
                MileageTrigger = 5000m,
                UsageUnit = "miles",
                LastUsageValue = 85012m,
                StartDate = baseDate.AddMonths(-6),
                NextDueDate = baseDate.AddMonths(3),
                LastCompletedDate = baseDate.AddDays(-1),
                Priority = "Medium",
                EstimatedHours = 4.50m,
                EstimatedCost = 620m,
                Instructions = "Perform quarterly truck preventive maintenance.",
                SafetyNotes = "Follow vehicle lockout and PPE protocol.",
                RequiredSkills = "[\"Brake and Lubrication Service\"]",
                RequiredTools = "[\"TOOL-SCAN-001\"]",
                RequiredParts = "[\"OIL-5W30-5L\",\"FILTER-OIL-001\",\"FILTER-AIR-001\"]",
                AutoGenerateWorkOrders = true,
                AdvanceNotificationDays = 7,
                NotificationRecipients = "maintenance@example.com",
                IsActive = true,
                ScheduleType = "Preventive",
                LastGeneratedDate = baseDate.AddDays(-7),
                LastProcessedDate = baseDate,
                LastReminderSentDate = baseDate.AddDays(-7),
                LastUsageCheckDate = baseDate,
                DefaultTechnicianId = _seniorTechnicianId,
                DefaultTeamId = _maintenanceTeamId,
                PriorityLevelId = _mediumPriorityId,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddMonths(-6),
                CreatedBy = "System"
            });
        }

        var workOrder = await _context.WorkOrders.FirstAsync(w => w.Id == _workOrderId);
        if (workOrder.MaintenanceScheduleId != _maintenanceScheduleId)
        {
            workOrder.MaintenanceScheduleId = _maintenanceScheduleId;
            workOrder.IsRecurring = true;
        }

        if (!await _context.MaintenanceScheduleHistories.AnyAsync(h => h.Id == _scheduleHistoryId))
        {
            await _context.MaintenanceScheduleHistories.AddAsync(new MaintenanceScheduleHistory
            {
                Id = _scheduleHistoryId,
                ScheduleId = _maintenanceScheduleId,
                ChangeType = "Created",
                NewValues = "{\"code\":\"E2E-PM-TRK\",\"frequency\":\"Quarterly\"}",
                ChangeReason = "Seeded E2E schedule",
                ChangedById = _maintenanceSupervisorId,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddMonths(-6),
                CreatedBy = "System"
            });
        }

        if (!await _context.MaintenanceScheduleNotificationHistories.AnyAsync(h => h.Id == _scheduleNotificationHistoryId))
        {
            await _context.MaintenanceScheduleNotificationHistories.AddAsync(new MaintenanceScheduleNotificationHistory
            {
                Id = _scheduleNotificationHistoryId,
                ScheduleId = _maintenanceScheduleId,
                NotificationType = "Reminder",
                Status = "Sent",
                ScheduledFor = baseDate.AddDays(-7),
                SentAt = baseDate.AddDays(-7),
                Recipients = "[\"maintenance@example.com\"]",
                Subject = "E2E Truck Quarterly PM due",
                Message = "Seeded schedule reminder for E2E maintenance testing.",
                RetryCount = 0,
                AdditionalData = "{\"schedule\":\"E2E-PM-TRK\"}",
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddDays(-7),
                CreatedBy = "System"
            });
        }

        if (!await _context.AssetUsageTrackings.AnyAsync(u => u.Id == _assetUsageId))
        {
            await _context.AssetUsageTrackings.AddAsync(new AssetUsageTracking
            {
                Id = _assetUsageId,
                AssetId = _deliveryTruckId,
                RecordedAt = baseDate,
                Mileage = 85012m,
                MileageUnit = "miles",
                OperatingHours = 3204m,
                FuelConsumed = 12m,
                FuelUnit = "liters",
                DataSource = "Manual",
                AdditionalMetrics = "{\"route\":\"E2E road test\"}",
                Notes = "Seeded post-service usage reading.",
                RecordedById = _seniorTechnicianId,
                TriggeredMaintenance = false,
                IsValidated = true,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        if (!await _context.AssetDowntimes.AnyAsync(d => d.Id == _assetDowntimeId))
        {
            await _context.AssetDowntimes.AddAsync(new AssetDowntime
            {
                Id = _assetDowntimeId,
                AssetId = _deliveryTruckId,
                WorkOrderId = _workOrderId,
                StartTime = baseDate.AddDays(-5).AddHours(8),
                EndTime = baseDate.AddDays(-5).AddHours(12.25),
                DowntimeHours = 4.25,
                Reason = "Maintenance",
                Description = "Seeded truck downtime during E2E preventive maintenance.",
                EstimatedCostImpact = 250m,
                Status = "Resolved",
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddDays(-5),
                CreatedBy = "System"
            });
        }
    }

    private async Task SeedInspectionAndQualityRecordsAsync(DateTime baseDate)
    {
        if (!await _context.AssetInspections.AnyAsync(i => i.Id == _assetInspectionId))
        {
            await _context.AssetInspections.AddAsync(new AssetInspection
            {
                Id = _assetInspectionId,
                AssetId = _deliveryTruckId,
                InspectionTemplateId = _inspectionTemplateId,
                InspectorId = _qualityInspectorId,
                InspectionDate = baseDate,
                Status = "Completed",
                OverallResult = "Pass",
                InspectionData = "{\"engineBay\":\"Pass\",\"brakes\":\"Pass\",\"roadTest\":\"Pass\"}",
                Notes = "Seeded final asset inspection passed.",
                RecommendedActions = "Continue quarterly PM schedule.",
                NextInspectionDue = baseDate.AddMonths(3),
                IsRegulatoryRequired = false,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        if (!await _context.InspectionDocuments.AnyAsync(d => d.Id == _inspectionDocumentId))
        {
            await _context.InspectionDocuments.AddAsync(new InspectionDocument
            {
                Id = _inspectionDocumentId,
                InspectionId = _assetInspectionId,
                FileName = "e2e-final-inspection.pdf",
                FilePath = "/seeded/maintenance/inspections/e2e-final-inspection.pdf",
                FileType = "application/pdf",
                FileSize = 18432,
                DocumentType = "Report",
                Description = "Seeded final inspection report",
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        if (!await _context.InspectionApprovals.AnyAsync(a => a.InspectionId == _assetInspectionId && a.ApprovalLevel == 1))
        {
            await _context.InspectionApprovals.AddAsync(new InspectionApproval
            {
                Id = Guid.NewGuid(),
                InspectionId = _assetInspectionId,
                ApprovalLevel = 1,
                ApproverId = _maintenanceSupervisorId,
                ApproverRole = "Maintenance Supervisor",
                Status = "Approved",
                RequestedDate = baseDate,
                DueDate = baseDate.AddDays(1),
                ApprovedDate = baseDate,
                Comments = "Seeded inspection approval passed.",
                Priority = 2,
                TenantId = _defaultTenantId
            });
        }

        var checklist = await _context.QualityControlChecklists.FirstOrDefaultAsync(c => c.Name == "PM Quality Checklist");
        if (checklist != null && !await _context.WorkOrderQualityChecks.AnyAsync(q => q.Id == _qualityCheckId))
        {
            await _context.WorkOrderQualityChecks.AddAsync(new WorkOrderQualityCheck
            {
                Id = _qualityCheckId,
                WorkOrderId = _workOrderId,
                ChecklistId = checklist.Id,
                InspectorId = _qualityInspectorId,
                InspectionDate = baseDate,
                OverallResult = "Pass",
                Score = 96,
                CheckResults = "[{\"item\":\"All work order tasks completed\",\"result\":\"Pass\"},{\"item\":\"Road test completed\",\"result\":\"Pass\"}]",
                Notes = "Seeded quality check passed.",
                RequiresFollowUp = false,
                AttachmentPaths = "[\"/seeded/maintenance/quality/e2e-quality-check.pdf\"]",
                TenantId = _defaultTenantId
            });
        }

        if (!await _context.WorkOrderQualitySignOffs.AnyAsync(s => s.Id == _qualitySignOffId))
        {
            await _context.WorkOrderQualitySignOffs.AddAsync(new WorkOrderQualitySignOff
            {
                Id = _qualitySignOffId,
                WorkOrderId = _workOrderId,
                SignOffLevel = 1,
                SignOffRole = "QualityInspector",
                SignOffById = _qualityInspectorId,
                Status = "Approved",
                SignOffDate = baseDate,
                Comments = "Seeded quality sign-off approved.",
                QualityRating = 5,
                SafetyCompliant = true,
                WorkmanshipSatisfactory = true,
                MaterialsAcceptable = true,
                TestingComplete = true,
                DocumentationComplete = true,
                IsRequired = true,
                SortOrder = 1,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        foreach (var item in new[]
        {
            new { Text = "Safety compliance confirmed", Category = "Safety", Sort = 1 },
            new { Text = "Materials and parts acceptable", Category = "Materials", Sort = 2 },
            new { Text = "Documentation complete", Category = "Documentation", Sort = 3 }
        })
        {
            if (!await _context.QualitySignOffChecklists.AnyAsync(c => c.SignOffId == _qualitySignOffId && c.CheckItem == item.Text))
            {
                await _context.QualitySignOffChecklists.AddAsync(new QualitySignOffChecklist
                {
                    Id = Guid.NewGuid(),
                    SignOffId = _qualitySignOffId,
                    CheckItem = item.Text,
                    Category = item.Category,
                    CheckType = "Boolean",
                    IsRequired = true,
                    SortOrder = item.Sort,
                    BooleanResult = true,
                    Notes = "Seeded sign-off checklist result.",
                    CheckedDate = baseDate,
                    CheckedById = _qualityInspectorId,
                    TenantId = _defaultTenantId,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                });
            }
        }
    }

    private async Task SeedSafetyAndNotificationRecordsAsync(DateTime baseDate, Guid? tenantUserId)
    {
        if (!await _context.SafetyComplianceRecords.AnyAsync(c => c.Id == _safetyComplianceId))
        {
            await _context.SafetyComplianceRecords.AddAsync(new SafetyComplianceRecord
            {
                Id = _safetyComplianceId,
                SafetyProtocolId = _safetyProtocolId,
                TechnicianId = _seniorTechnicianId,
                WorkOrderId = _workOrderId,
                ComplianceDate = baseDate.AddDays(-5),
                ComplianceStatus = "Compliant",
                Notes = "Seeded safety compliance check passed.",
                IsCompliant = true,
                ChecklistItems = "[{\"item\":\"PPE worn\",\"result\":true},{\"item\":\"Vehicle secured\",\"result\":true}]",
                Violations = "[]",
                InspectorId = _qualityInspectorId,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddDays(-5),
                CreatedBy = "System"
            });
        }

        if (!await _context.ProtocolAdherences.AnyAsync(a => a.Id == _protocolAdherenceId))
        {
            await _context.ProtocolAdherences.AddAsync(new ProtocolAdherence
            {
                Id = _protocolAdherenceId,
                ProtocolId = _safetyProtocolId,
                WorkOrderId = _workOrderId,
                TechnicianId = _seniorTechnicianId,
                AdherenceDate = baseDate.AddDays(-5),
                WasFollowed = true,
                ComplianceScore = 100,
                AdherenceLevel = "Full",
                Notes = "Seeded protocol adherence record.",
                VerifiedBy = "System",
                VerificationDate = baseDate.AddDays(-5),
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddDays(-5),
                CreatedBy = "System"
            });
        }

        if (!await _context.ProtocolTrainings.AnyAsync(t => t.Id == _protocolTrainingId))
        {
            await _context.ProtocolTrainings.AddAsync(new ProtocolTraining
            {
                Id = _protocolTrainingId,
                ProtocolId = _safetyProtocolId,
                TechnicianId = _seniorTechnicianId,
                TrainingDate = baseDate.AddMonths(-3),
                TrainingMethod = "On-the-job",
                TrainerName = "Maintenance Supervisor",
                TrainingHours = 2.5m,
                TestScore = 95,
                Completed = true,
                CompletionStatus = "Completed",
                CertificationIssued = true,
                ExpirationDate = baseDate.AddMonths(9),
                Notes = "Seeded safety protocol training.",
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddMonths(-3),
                CreatedBy = "System"
            });
        }

        if (!await _context.SafetyAudits.AnyAsync(a => a.Id == _safetyAuditId))
        {
            await _context.SafetyAudits.AddAsync(new SafetyAudit
            {
                Id = _safetyAuditId,
                AuditName = "E2E Maintenance Safety Audit",
                AuditDate = baseDate,
                LeadAuditor = "Quality Inspector",
                AuditTeam = "[\"Maintenance Supervisor\",\"Quality Inspector\"]",
                AuditScope = "Seeded E2E work order safety controls",
                OverallScore = 98,
                Findings = "Seeded safety audit passed.",
                Recommendations = "Continue enforcing PPE and lockout checks.",
                AuditStatus = "Completed",
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        if (!await _context.ProtocolAuditDetails.AnyAsync(d => d.Id == _protocolAuditDetailId))
        {
            await _context.ProtocolAuditDetails.AddAsync(new ProtocolAuditDetail
            {
                Id = _protocolAuditDetailId,
                AuditId = _safetyAuditId,
                ProtocolId = _safetyProtocolId,
                ComplianceScore = 98,
                Passed = true,
                ProtocolFindings = "Seeded protocol audit passed.",
                ProtocolRecommendations = "No corrective action required.",
                Priority = "Low",
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        if (!await _context.MaintenanceNotificationTemplates.AnyAsync(t => t.Id == _notificationTemplateId))
        {
            await _context.MaintenanceNotificationTemplates.AddAsync(new MaintenanceNotificationTemplate
            {
                Id = _notificationTemplateId,
                Name = "E2E Work Order Assigned",
                NotificationType = "WorkOrderAssigned",
                Description = "Seeded maintenance notification template",
                TitleTemplate = "Work order {WorkOrderNumber} assigned",
                MessageTemplate = "Seeded work order {WorkOrderNumber} has been assigned for testing.",
                DefaultPriority = "Normal",
                LeadTimeMinutes = 0,
                IsActive = true,
                DefaultRoles = "Maintenance",
                DeliveryMethods = "InApp,Email",
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        if (tenantUserId.HasValue && !await _context.MaintenanceNotifications.AnyAsync(n => n.Id == _notificationId))
        {
            await _context.MaintenanceNotifications.AddAsync(new MaintenanceNotification
            {
                Id = _notificationId,
                NotificationType = "WorkOrderAssigned",
                EntityType = "WorkOrder",
                EntityId = _workOrderId,
                RecipientId = tenantUserId.Value,
                RecipientRole = "Maintenance",
                Title = "Work order E2E-WO-0001 assigned",
                Message = "Seeded work order E2E-WO-0001 is available for end-to-end maintenance testing.",
                Priority = "Normal",
                Status = "Sent",
                ScheduledFor = baseDate.AddDays(-6),
                SentAt = baseDate.AddDays(-6),
                AttemptCount = 1,
                AdditionalData = "{\"workOrderNumber\":\"E2E-WO-0001\"}",
                ActionUrl = "/maintenance/work-orders/E2E-WO-0001",
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddDays(-6),
                CreatedBy = "System"
            });
        }

        if (!await _context.MaintenanceEscalationRules.AnyAsync(r => r.Name == "E2E Overdue Work Order Escalation"))
        {
            await _context.MaintenanceEscalationRules.AddAsync(new MaintenanceEscalationRule
            {
                Id = Guid.NewGuid(),
                Name = "E2E Overdue Work Order Escalation",
                EntityType = "WorkOrder",
                TriggerCondition = "{\"status\":\"Overdue\"}",
                HoursOverdue = 24,
                TriggerPriority = "High",
                EscalationRole = "Maintenance Manager",
                IsActive = true,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }
    }

    private async Task SeedAdmissionRecordsAsync(DateTime baseDate, Guid tenantUserId)
    {
        if (!await _context.AssetAdmissions.AnyAsync(a => a.Id == _assetAdmissionId))
        {
            await _context.AssetAdmissions.AddAsync(new AssetAdmission
            {
                Id = _assetAdmissionId,
                AdmissionNumber = "E2E-ADM-0001",
                AssetId = _deliveryTruckId,
                JobCardId = _jobCardId,
                WorkOrderId = _workOrderId,
                AdmissionDate = baseDate.AddDays(-5),
                AdmittedById = tenantUserId,
                AdmissionType = "Scheduled",
                AssetConditionOnAdmission = "Good",
                AdmissionNotes = "Seeded vehicle admission for E2E workflow.",
                ObservedProblems = "Routine PM due.",
                MileageReading = 85000m,
                HoursReading = 3200m,
                FuelLevel = 60m,
                AdmissionChecklist = "{\"fuelLevel\":60,\"bodyCondition\":\"Good\",\"spareTire\":true}",
                PhotoPaths = "[\"/seeded/maintenance/admissions/e2e-admission.jpg\"]",
                AdmissionLocation = "Workshop",
                BayOrStation = "Bay 3",
                EstimatedCompletionDate = baseDate.AddDays(-1),
                EstimatedDischargeDate = baseDate.AddDays(-1),
                Status = "Active",
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddDays(-5),
                CreatedBy = "System"
            });

            await _context.SaveChangesAsync();
        }

        var templateItems = await _context.PreInspectionChecklistItems
            .AsNoTracking()
            .Where(i => i.TemplateId == _preInspectionTemplateId)
            .OrderBy(i => i.SortOrder)
            .ToListAsync();

        if (!await _context.AssetConditionRecords.AnyAsync(r => r.Id == _admissionConditionRecordId))
        {
            await _context.AssetConditionRecords.AddAsync(new AssetConditionRecord
            {
                Id = _admissionConditionRecordId,
                InspectionNumber = "E2E-COND-ADM-0001",
                AssetId = _deliveryTruckId,
                TemplateId = _preInspectionTemplateId,
                InspectorId = tenantUserId,
                InspectionDate = baseDate.AddDays(-5),
                InspectionType = "Admission",
                Status = "Completed",
                GeneralNotes = "Seeded admission condition inspection.",
                AdmissionId = _assetAdmissionId,
                PhotoPaths = "[\"/seeded/maintenance/conditions/e2e-admission.jpg\"]",
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddDays(-5),
                CreatedBy = "System"
            });
        }

        foreach (var item in templateItems)
        {
            if (!await _context.AssetConditionItemResults.AnyAsync(r =>
                r.ConditionRecordId == _admissionConditionRecordId && r.ChecklistItemId == item.Id))
            {
                await _context.AssetConditionItemResults.AddAsync(new AssetConditionItemResult
                {
                    Id = Guid.NewGuid(),
                    ConditionRecordId = _admissionConditionRecordId,
                    ChecklistItemId = item.Id,
                    IsPresent = item.ItemType == "Boolean" ? true : null,
                    NumericValue = item.ItemType == "Numeric" ? 60m : null,
                    SelectedOption = item.ItemType == "Choice" ? "Good" : null,
                    Comment = "Seeded admission condition item.",
                    InspectedAt = baseDate.AddDays(-5),
                    RepairReplacementAction = "None",
                    TaskCreated = false,
                    TenantId = _defaultTenantId,
                    CreatedAt = baseDate.AddDays(-5),
                    CreatedBy = "System"
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedDischargeAndCompletionRecordsAsync(DateTime baseDate, Guid tenantUserId)
    {
        if (!await _context.AssetAdmissions.AnyAsync(a => a.Id == _assetAdmissionId))
        {
            await _context.AssetAdmissions.AddAsync(new AssetAdmission
            {
                Id = _assetAdmissionId,
                AdmissionNumber = "E2E-ADM-0001",
                AssetId = _deliveryTruckId,
                JobCardId = _jobCardId,
                WorkOrderId = _workOrderId,
                AdmissionDate = baseDate.AddDays(-5),
                AdmittedById = tenantUserId,
                AdmissionType = "Scheduled",
                AssetConditionOnAdmission = "Good",
                AdmissionNotes = "Seeded vehicle admission for E2E workflow.",
                ObservedProblems = "Routine PM due.",
                MileageReading = 85000m,
                HoursReading = 3200m,
                FuelLevel = 60m,
                AdmissionChecklist = "{\"fuelLevel\":60,\"bodyCondition\":\"Good\",\"spareTire\":true}",
                PhotoPaths = "[\"/seeded/maintenance/admissions/e2e-admission.jpg\"]",
                AdmissionLocation = "Workshop",
                BayOrStation = "Bay 3",
                EstimatedCompletionDate = baseDate.AddDays(-1),
                EstimatedDischargeDate = baseDate.AddDays(-1),
                Status = "Active",
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddDays(-5),
                CreatedBy = "System"
            });

            await _context.SaveChangesAsync();
        }

        if (!await _context.AssetDischarges.AnyAsync(d => d.Id == _assetDischargeId))
        {
            await _context.AssetDischarges.AddAsync(new AssetDischarge
            {
                Id = _assetDischargeId,
                DischargeNumber = "E2E-DIS-0001",
                AdmissionId = _assetAdmissionId,
                AssetId = _deliveryTruckId,
                JobCardId = _jobCardId,
                WorkOrderId = _workOrderId,
                DischargeDate = baseDate.AddDays(-1),
                DischargedById = tenantUserId,
                AssetConditionOnDischarge = "Excellent",
                DischargeNotes = "Seeded vehicle discharge after completed E2E PM.",
                WorkCompleted = "Oil service, filters, brakes, scanner diagnostics, and road test completed.",
                RemainingIssues = "None",
                MileageReading = 85012m,
                HoursReading = 3204m,
                FuelLevel = 55m,
                QualityCheckPassed = true,
                QualityCheckedById = tenantUserId,
                QualityCheckDate = baseDate,
                QualityCheckNotes = "Seeded discharge quality check passed.",
                DischargeChecklist = "{\"roadTest\":\"Pass\",\"documentation\":\"Complete\"}",
                PhotoPaths = "[\"/seeded/maintenance/discharges/e2e-discharge.jpg\"]",
                CertificateGenerated = true,
                CertificateGeneratedDate = baseDate,
                CertificatePath = "/seeded/maintenance/certificates/e2e-maintenance-completion.pdf",
                CustomerAcceptance = true,
                AcceptedById = tenantUserId,
                AcceptedDate = baseDate,
                AcceptanceNotes = "Seeded acceptance completed.",
                RequiresFollowUp = true,
                FollowUpDate = baseDate.AddMonths(3),
                FollowUpInstructions = "Run next quarterly PM cycle.",
                WarrantyDays = 90,
                WarrantyExpiration = baseDate.AddDays(90),
                WarrantyTerms = "Seeded warranty for E2E maintenance.",
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddDays(-1),
                CreatedBy = "System"
            });

            await _context.SaveChangesAsync();
        }

        var admission = await _context.AssetAdmissions.FirstAsync(a => a.Id == _assetAdmissionId);
        if (admission.DischargeId != _assetDischargeId)
        {
            admission.DischargeId = _assetDischargeId;
        }
        admission.Status = "Completed";

        if (!await _context.AssetMaintenanceDowntimes.AnyAsync(d => d.Id == _assetMaintenanceDowntimeId))
        {
            await _context.AssetMaintenanceDowntimes.AddAsync(new AssetMaintenanceDowntime
            {
                Id = _assetMaintenanceDowntimeId,
                AssetId = _deliveryTruckId,
                AdmissionId = _assetAdmissionId,
                DischargeId = _assetDischargeId,
                JobCardId = _jobCardId,
                WorkOrderId = _workOrderId,
                DowntimeStart = baseDate.AddDays(-5).AddHours(8),
                DowntimeEnd = baseDate.AddDays(-1).AddHours(15),
                DowntimeMinutes = 6180,
                DowntimeType = "Maintenance",
                Priority = "Medium",
                EstimatedCostImpact = 250m,
                ActualCostImpact = 200m,
                Status = "Completed",
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddDays(-5),
                CreatedBy = "System"
            });
        }

        var templateItems = await _context.PreInspectionChecklistItems
            .AsNoTracking()
            .Where(i => i.TemplateId == _preInspectionTemplateId)
            .OrderBy(i => i.SortOrder)
            .ToListAsync();

        if (!await _context.AssetConditionRecords.AnyAsync(r => r.Id == _admissionConditionRecordId))
        {
            await _context.AssetConditionRecords.AddAsync(new AssetConditionRecord
            {
                Id = _admissionConditionRecordId,
                InspectionNumber = "E2E-COND-ADM-0001",
                AssetId = _deliveryTruckId,
                TemplateId = _preInspectionTemplateId,
                InspectorId = tenantUserId,
                InspectionDate = baseDate.AddDays(-5),
                InspectionType = "Admission",
                Status = "Completed",
                GeneralNotes = "Seeded admission condition inspection.",
                AdmissionId = _assetAdmissionId,
                PhotoPaths = "[\"/seeded/maintenance/conditions/e2e-admission.jpg\"]",
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddDays(-5),
                CreatedBy = "System"
            });
        }

        if (!await _context.AssetConditionRecords.AnyAsync(r => r.Id == _dischargeConditionRecordId))
        {
            await _context.AssetConditionRecords.AddAsync(new AssetConditionRecord
            {
                Id = _dischargeConditionRecordId,
                InspectionNumber = "E2E-COND-DIS-0001",
                AssetId = _deliveryTruckId,
                TemplateId = _preInspectionTemplateId,
                InspectorId = tenantUserId,
                InspectionDate = baseDate.AddDays(-1),
                InspectionType = "Discharge",
                Status = "Completed",
                GeneralNotes = "Seeded discharge condition inspection.",
                DischargeId = _assetDischargeId,
                PhotoPaths = "[\"/seeded/maintenance/conditions/e2e-discharge.jpg\"]",
                TenantId = _defaultTenantId,
                CreatedAt = baseDate.AddDays(-1),
                CreatedBy = "System"
            });
        }

        foreach (var item in templateItems)
        {
            if (!await _context.AssetConditionItemResults.AnyAsync(r =>
                r.ConditionRecordId == _admissionConditionRecordId && r.ChecklistItemId == item.Id))
            {
                await _context.AssetConditionItemResults.AddAsync(new AssetConditionItemResult
                {
                    Id = Guid.NewGuid(),
                    ConditionRecordId = _admissionConditionRecordId,
                    ChecklistItemId = item.Id,
                    IsPresent = item.ItemType == "Boolean" ? true : null,
                    NumericValue = item.ItemType == "Numeric" ? 60m : null,
                    SelectedOption = item.ItemType == "Choice" ? "Good" : null,
                    Comment = "Seeded admission condition item.",
                    InspectedAt = baseDate.AddDays(-5),
                    RepairReplacementAction = "None",
                    TaskCreated = false,
                    TenantId = _defaultTenantId,
                    CreatedAt = baseDate.AddDays(-5),
                    CreatedBy = "System"
                });
            }

            if (!await _context.AssetConditionItemResults.AnyAsync(r =>
                r.ConditionRecordId == _dischargeConditionRecordId && r.ChecklistItemId == item.Id))
            {
                await _context.AssetConditionItemResults.AddAsync(new AssetConditionItemResult
                {
                    Id = Guid.NewGuid(),
                    ConditionRecordId = _dischargeConditionRecordId,
                    ChecklistItemId = item.Id,
                    IsPresent = item.ItemType == "Boolean" ? true : null,
                    NumericValue = item.ItemType == "Numeric" ? 55m : null,
                    SelectedOption = item.ItemType == "Choice" ? "Excellent" : null,
                    Comment = "Seeded discharge condition item.",
                    InspectedAt = baseDate.AddDays(-1),
                    RepairReplacementAction = "None",
                    TaskCreated = false,
                    TenantId = _defaultTenantId,
                    CreatedAt = baseDate.AddDays(-1),
                    CreatedBy = "System"
                });
            }
        }

        if (!await _context.Set<MaintenanceCertificate>().AnyAsync(c => c.CertificateNumber == "E2E-CERT-MAINT-0001"))
        {
            await _context.Set<MaintenanceCertificate>().AddAsync(new MaintenanceCertificate
            {
                Id = Guid.Parse("0000000C-0000-0000-0000-000000000029"),
                CertificateNumber = "E2E-CERT-MAINT-0001",
                DischargeId = _assetDischargeId,
                AssetId = _deliveryTruckId,
                CertificateType = "Maintenance Completion",
                IssuedDate = baseDate,
                ValidUntil = baseDate.AddMonths(3),
                IssuedById = tenantUserId,
                FilePath = "/seeded/maintenance/certificates/e2e-maintenance-completion.pdf",
                FileFormat = "PDF",
                Description = "Seeded maintenance completion certificate",
                CertificateData = "{\"jobCard\":\"E2E-JC-0001\",\"workOrder\":\"E2E-WO-0001\"}",
                IsActive = true,
                TenantId = _defaultTenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        await CompleteSeededJobCardAndWorkOrderAsync(baseDate, tenantUserId);
    }

    private async Task CompleteSeededJobCardAndWorkOrderAsync(DateTime baseDate, Guid tenantUserId)
    {
        var jobCard = await _context.JobCards.FirstOrDefaultAsync(j => j.Id == _jobCardId);
        if (jobCard != null)
        {
            jobCard.JobCardStatus = "Closed";
            jobCard.CompletedDate = baseDate.AddDays(-1);
            jobCard.CompletionNotes = "Seeded PM workflow completed successfully.";
            jobCard.AssetConditionOnCompletion = "Excellent";
            jobCard.MileageReadingOnCompletion = 85012m;
            jobCard.HoursReadingOnCompletion = 3204m;
            jobCard.FuelLevelOnCompletion = 55m;
            jobCard.WorkCompletedSummary = "Oil service, filters, brake inspection, diagnostic scan, and road test completed.";
            jobCard.RemainingIssues = "None";
            jobCard.QualityCheckPassed = true;
            jobCard.QualityCheckedById = _qualityInspectorId;
            jobCard.QualityCheckDate = baseDate;
            jobCard.QualityCheckNotes = "Seeded quality check passed.";
            jobCard.CertificateGenerated = true;
            jobCard.CertificateGeneratedDate = baseDate;
            jobCard.CustomerAcceptance = true;
            jobCard.AcceptedById = _fleetManagerId;
            jobCard.AcceptedDate = baseDate;
            jobCard.AcceptanceNotes = "Accepted for E2E testing.";
        }

        var workOrder = await _context.WorkOrders.FirstOrDefaultAsync(w => w.Id == _workOrderId);
        if (workOrder != null)
        {
            workOrder.Status = "Completed";
            workOrder.ActualCompletionDate = baseDate.AddDays(-1);
            workOrder.ActualCost = 545m;
            workOrder.ActualHours = 4.25;
            workOrder.CompletedById = tenantUserId;
            workOrder.QualityCheckedById = tenantUserId;
            workOrder.CompletionNotes = "Seeded E2E work order completed and quality checked.";
        }
    }

    private void LogSeedingSummary()
    {
        _logger.LogInformation("\n" +
            "╔══════════════════════════════════════════════════════╗\n" +
            "║     E2E MAINTENANCE TEST DATA SEEDING SUMMARY       ║\n" +
            "╠══════════════════════════════════════════════════════╣\n" +
            "║ ✓ 7 HR Employees                                    ║\n" +
            "║ ✓ 6 Inventory Parts/Tools                           ║\n" +
            "║ ✓ 2 Asset Categories                                ║\n" +
            "║ ✓ 2 Asset Types                                     ║\n" +
            "║ ✓ 3 Maintenance Assets                              ║\n" +
            "║ ✓ 3 Maintenance Types                               ║\n" +
            "║ ✓ 3 Priority Levels                                 ║\n" +
            "║ ✓ 2 Work Order Types                                ║\n" +
            "║ ✓ 6 Task Templates                                  ║\n" +
            "║ ✓ 1 Quality Checklist                               ║\n" +
            "║ ✓ 1 Inspection Template                             ║\n" +
            "║ ✓ 2 Technician Records                              ║\n" +
            "║ ✓ E2E Job Card + Work Order Workflow                ║\n" +
            "║ ✓ Schedules, Parts, Labor, Tools, QC, Safety        ║\n" +
            "║ ✓ Admission, Discharge, Attachments, Notifications  ║\n" +
            "╠══════════════════════════════════════════════════════╣\n" +
            "║ 🎯 READY FOR E2E WORKFLOW TESTING!                  ║\n" +
            "╚══════════════════════════════════════════════════════╝");
    }
}
