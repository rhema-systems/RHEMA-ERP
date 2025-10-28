using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

public class MaintenanceDataSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MaintenanceDataSeeder> _logger;

    public MaintenanceDataSeeder(ApplicationDbContext context, ILogger<MaintenanceDataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            _logger.LogInformation("Starting maintenance data seeding...");

            var defaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var baseDate = new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc);

            // Get existing Employee IDs from the database
            var employees = await _context.Employees
                .Where(e => e.TenantId == defaultTenantId)
                .Take(4)
                .Select(e => e.Id)
                .ToListAsync();

            if (employees.Count < 4)
            {
                _logger.LogWarning($"Not enough employees found in the database. Found {employees.Count}, need at least 4. Skipping Job Card seeding.");
                
                // Seed only base data without Job Cards
                await SeedAssetCategoriesAsync(defaultTenantId, baseDate);
                await _context.SaveChangesAsync();
                await SeedMaintenanceAssetsAsync(defaultTenantId, baseDate);
                await _context.SaveChangesAsync();
                await SeedMaintenanceTypesAsync(defaultTenantId, baseDate);
                await _context.SaveChangesAsync();
                await SeedPriorityLevelsAsync(defaultTenantId, baseDate);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Maintenance data seeding completed (Job Cards skipped due to insufficient employees)!");
                return;
            }

            var maintenanceManagerId = employees[0];
            var technicianId = employees[1];
            var qualityCheckerId = employees[2];
            var supervisorId = employees[3];
            
            _logger.LogInformation($"Using employees: Manager={maintenanceManagerId}, Technician={technicianId}, QC={qualityCheckerId}, Supervisor={supervisorId}");

            // 1. Seed Asset Categories
            await SeedAssetCategoriesAsync(defaultTenantId, baseDate);
            await _context.SaveChangesAsync();

            // 2. Seed Maintenance Assets
            await SeedMaintenanceAssetsAsync(defaultTenantId, baseDate);
            await _context.SaveChangesAsync();

            // 3. Seed Maintenance Types
            await SeedMaintenanceTypesAsync(defaultTenantId, baseDate);
            await _context.SaveChangesAsync();

            // 4. Seed Priority Levels
            await SeedPriorityLevelsAsync(defaultTenantId, baseDate);
            await _context.SaveChangesAsync();

            // 4.5. Seed Asset Types
            await SeedAssetTypesAsync(defaultTenantId, baseDate);
            await _context.SaveChangesAsync();

            // 5. Sync Employees to Technicians table (for technician page)
            await SyncEmployeesToTechniciansAsync(defaultTenantId, baseDate, employees);
            await _context.SaveChangesAsync();

            // 6. Seed Task Templates
            await SeedTaskTemplatesAsync(defaultTenantId, baseDate);
            await _context.SaveChangesAsync();

            // 7. Seed Job Cards
            await SeedJobCardsAsync(defaultTenantId, baseDate, maintenanceManagerId, technicianId, qualityCheckerId, supervisorId);
            await _context.SaveChangesAsync();

            // 8. Seed Job Card Certificates
            await SeedJobCardCertificatesAsync(defaultTenantId, baseDate, supervisorId);
            await _context.SaveChangesAsync();

            // 9. Seed Job Card Comments
            await SeedJobCardCommentsAsync(defaultTenantId, baseDate, maintenanceManagerId, technicianId, qualityCheckerId, supervisorId);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Maintenance data seeding completed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while seeding maintenance data");
            throw;
        }
    }

    private async Task SeedAssetCategoriesAsync(Guid tenantId, DateTime baseDate)
    {
        var vehicleCategoryId = Guid.Parse("aaaa1111-1111-1111-1111-111111111111");
        var equipmentCategoryId = Guid.Parse("aaaa2222-2222-2222-2222-222222222222");

        if (!await _context.MaintenanceAssetCategories.AnyAsync(c => c.Id == vehicleCategoryId))
        {
            await _context.MaintenanceAssetCategories.AddAsync(new MaintenanceAssetCategory
            {
                Id = vehicleCategoryId,
                Name = "Vehicles",
                Code = "VEH",
                Description = "Motor vehicles and transportation equipment",
                AssetType = "Vehicle",
                IsActive = true,
                TenantId = tenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        if (!await _context.MaintenanceAssetCategories.AnyAsync(c => c.Id == equipmentCategoryId))
        {
            await _context.MaintenanceAssetCategories.AddAsync(new MaintenanceAssetCategory
            {
                Id = equipmentCategoryId,
                Name = "Heavy Equipment",
                Code = "HEQ",
                Description = "Heavy machinery and equipment",
                AssetType = "Equipment",
                IsActive = true,
                TenantId = tenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        _logger.LogInformation("Asset categories seeded");
    }

    private async Task SeedMaintenanceAssetsAsync(Guid tenantId, DateTime baseDate)
    {
        var vehicleCategoryId = Guid.Parse("aaaa1111-1111-1111-1111-111111111111");
        var equipmentCategoryId = Guid.Parse("aaaa2222-2222-2222-2222-222222222222");

        var forkliftId = Guid.Parse("bbbb1111-1111-1111-1111-111111111111");
        var truckId = Guid.Parse("bbbb2222-2222-2222-2222-222222222222");
        var generatorId = Guid.Parse("bbbb3333-3333-3333-3333-333333333333");

        if (!await _context.MaintenanceAssets.AnyAsync(a => a.Id == forkliftId))
        {
            await _context.MaintenanceAssets.AddAsync(new MaintenanceAsset
            {
                Id = forkliftId,
                AssetNumber = "FL-001",
                Name = "Forklift Toyota 8FG25",
                AssetCategoryId = equipmentCategoryId,
                Description = "3-ton capacity forklift",
                SerialNumber = "TOY-8FG25-2020-001",
                Manufacturer = "Toyota",
                Model = "8FG25",
                PurchaseDate = new DateTime(2020, 6, 15),
                PurchasePrice = 35000.00m,
                CurrentValue = 28000.00m,
                Status = AssetStatus.Active,
                Criticality = AssetCriticality.High,
                Location = "Warehouse A",
                TenantId = tenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        if (!await _context.MaintenanceAssets.AnyAsync(a => a.Id == truckId))
        {
            await _context.MaintenanceAssets.AddAsync(new MaintenanceAsset
            {
                Id = truckId,
                AssetNumber = "TRK-001",
                Name = "Delivery Truck Isuzu NPR",
                AssetCategoryId = vehicleCategoryId,
                Description = "5-ton delivery truck",
                SerialNumber = "ISU-NPR-2019-045",
                Manufacturer = "Isuzu",
                Model = "NPR 75",
                PurchaseDate = new DateTime(2019, 3, 10),
                PurchasePrice = 45000.00m,
                CurrentValue = 32000.00m,
                Status = AssetStatus.Active,
                Criticality = AssetCriticality.High,
                Location = "Fleet Parking",
                TenantId = tenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        if (!await _context.MaintenanceAssets.AnyAsync(a => a.Id == generatorId))
        {
            await _context.MaintenanceAssets.AddAsync(new MaintenanceAsset
            {
                Id = generatorId,
                AssetNumber = "GEN-001",
                Name = "Backup Generator Caterpillar",
                AssetCategoryId = equipmentCategoryId,
                Description = "500 KVA backup generator",
                SerialNumber = "CAT-C15-2021-089",
                Manufacturer = "Caterpillar",
                Model = "C15",
                PurchaseDate = new DateTime(2021, 8, 20),
                PurchasePrice = 85000.00m,
                CurrentValue = 75000.00m,
                Status = AssetStatus.Active,
                Criticality = AssetCriticality.Critical,
                Location = "Power House",
                TenantId = tenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        _logger.LogInformation("Maintenance assets seeded");
    }

    private async Task SeedMaintenanceTypesAsync(Guid tenantId, DateTime baseDate)
    {
        var preventiveId = Guid.Parse("cccc1111-1111-1111-1111-111111111111");
        var correctiveId = Guid.Parse("cccc2222-2222-2222-2222-222222222222");
        var inspectionId = Guid.Parse("cccc3333-3333-3333-3333-333333333333");

        if (!await _context.MaintenanceTypes.AnyAsync(m => m.Id == preventiveId))
        {
            await _context.MaintenanceTypes.AddAsync(new MaintenanceType
            {
                Id = preventiveId,
                Name = "Preventive Maintenance",
                Code = "PM",
                Description = "Scheduled preventive maintenance",
                Category = "Scheduled",
                MaintenanceClass = "Preventive",
                IsTimeBased = true,
                RequiresApproval = false,
                RequiresQualityCheck = true,
                RequiresCertification = true,
                EstimatedHours = 4,
                EstimatedCost = 500.00m,
                DefaultPriority = 2,
                Color = "#4CAF50",
                IsActive = true,
                TenantId = tenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        if (!await _context.MaintenanceTypes.AnyAsync(m => m.Id == correctiveId))
        {
            await _context.MaintenanceTypes.AddAsync(new MaintenanceType
            {
                Id = correctiveId,
                Name = "Corrective Maintenance",
                Code = "CM",
                Description = "Repair and corrective maintenance",
                Category = "Emergency",
                MaintenanceClass = "Corrective",
                IsTimeBased = false,
                RequiresApproval = true,
                RequiresQualityCheck = true,
                RequiresCertification = false,
                EstimatedHours = 6,
                EstimatedCost = 800.00m,
                DefaultPriority = 1,
                Color = "#FF9800",
                IsActive = true,
                TenantId = tenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        if (!await _context.MaintenanceTypes.AnyAsync(m => m.Id == inspectionId))
        {
            await _context.MaintenanceTypes.AddAsync(new MaintenanceType
            {
                Id = inspectionId,
                Name = "Safety Inspection",
                Code = "SI",
                Description = "Regular safety inspection",
                Category = "Inspection",
                MaintenanceClass = "Routine",
                IsTimeBased = true,
                RequiresApproval = false,
                RequiresQualityCheck = true,
                RequiresCertification = true,
                EstimatedHours = 2,
                EstimatedCost = 200.00m,
                DefaultPriority = 3,
                Color = "#2196F3",
                IsActive = true,
                TenantId = tenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        _logger.LogInformation("Maintenance types seeded");
    }

    private async Task SeedPriorityLevelsAsync(Guid tenantId, DateTime baseDate)
    {
        var criticalPriorityId = Guid.Parse("dddd1111-1111-1111-1111-111111111111");
        var highPriorityId = Guid.Parse("dddd2222-2222-2222-2222-222222222222");
        var mediumPriorityId = Guid.Parse("dddd3333-3333-3333-3333-333333333333");

        // Check by Level (unique index) instead of ID
        if (!await _context.PriorityLevels.AnyAsync(p => p.Level == 1 && p.TenantId == tenantId))
        {
            await _context.PriorityLevels.AddAsync(new PriorityLevel
            {
                Id = criticalPriorityId,
                Name = "Critical",
                Level = 1,
                Description = "Critical priority - immediate action required",
                Color = "#F44336",
                ResponseTimeHours = 2,
                IsActive = true,
                TenantId = tenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        if (!await _context.PriorityLevels.AnyAsync(p => p.Level == 2 && p.TenantId == tenantId))
        {
            await _context.PriorityLevels.AddAsync(new PriorityLevel
            {
                Id = highPriorityId,
                Name = "High",
                Level = 2,
                Description = "High priority - action required within 24 hours",
                Color = "#FF9800",
                ResponseTimeHours = 24,
                IsActive = true,
                TenantId = tenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        if (!await _context.PriorityLevels.AnyAsync(p => p.Level == 3 && p.TenantId == tenantId))
        {
            await _context.PriorityLevels.AddAsync(new PriorityLevel
            {
                Id = mediumPriorityId,
                Name = "Medium",
                Level = 3,
                Description = "Medium priority - action required within 72 hours",
                Color = "#2196F3",
                ResponseTimeHours = 72,
                IsActive = true,
                TenantId = tenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        _logger.LogInformation("Priority levels seeded");
    }

    private async Task SeedJobCardsAsync(Guid tenantId, DateTime baseDate, Guid maintenanceManagerId, Guid technicianId, Guid qualityCheckerId, Guid supervisorId)
    {
        var forkliftId = Guid.Parse("bbbb1111-1111-1111-1111-111111111111");
        var truckId = Guid.Parse("bbbb2222-2222-2222-2222-222222222222");
        var generatorId = Guid.Parse("bbbb3333-3333-3333-3333-333333333333");

        var preventiveId = Guid.Parse("cccc1111-1111-1111-1111-111111111111");
        var correctiveId = Guid.Parse("cccc2222-2222-2222-2222-222222222222");
        var inspectionId = Guid.Parse("cccc3333-3333-3333-3333-333333333333");

        var criticalPriorityId = Guid.Parse("dddd1111-1111-1111-1111-111111111111");
        var highPriorityId = Guid.Parse("dddd2222-2222-2222-2222-222222222222");
        var mediumPriorityId = Guid.Parse("dddd3333-3333-3333-3333-333333333333");

        var jobCard1Id = Guid.Parse("eeee1111-1111-1111-1111-111111111111");

        // Check by JobCardNumber (unique index) instead of ID
        if (!await _context.JobCards.AnyAsync(j => j.JobCardNumber == "JC-2025-0001"))
        {
            await _context.JobCards.AddAsync(new JobCard
            {
                Id = jobCard1Id,
                JobCardNumber = "JC-2025-0001",
                AssetId = forkliftId,
                MaintenanceTypeId = preventiveId,
                PriorityLevelId = highPriorityId,
                Title = "3-Month Preventive Maintenance - Forklift FL-001",
                Description = "Scheduled 3-month preventive maintenance service",
                ProblemDescription = "Routine maintenance due. Oil change, filter replacement, general inspection required.",
                MaintenanceLocation = "Internal",
                RequestedById = maintenanceManagerId,
                RequestedDate = baseDate.AddDays(-30),
                RequiredCompletionDate = baseDate.AddDays(-15),
                EstimatedHours = 4.0,
                EstimatedCost = 500.00m,
                JobCardStatus = "Closed",
                ApprovalStatus = "Approved",
                ApprovedById = supervisorId,
                ApprovedDate = baseDate.AddDays(-28),
                AssetConditionOnAdmission = "Good",
                MileageReadingOnAdmission = 5420.5m,
                HoursReadingOnAdmission = 1250.0m,
                FuelLevelOnAdmission = 45.0m,
                AdmissionNotes = "Asset received in good working condition. Minor oil leak noted.",
                BayOrStation = "Bay 3",
                CompletedDate = baseDate.AddDays(-8),
                CompletionNotes = "All maintenance tasks completed successfully. Oil changed, filters replaced, brakes serviced.",
                AssetConditionOnCompletion = "Excellent",
                MileageReadingOnCompletion = 5425.0m,
                HoursReadingOnCompletion = 1252.5m,
                FuelLevelOnCompletion = 95.0m,
                WorkCompletedSummary = "Engine oil changed, oil filter replaced, air filter cleaned, brake system serviced, hydraulic fluid topped up, general safety inspection completed.",
                RemainingIssues = "None",
                WarrantyDays = 90,
                WarrantyTerms = "90-day warranty on parts and labor",
                WarrantyExpiration = baseDate.AddDays(-8).AddDays(90),
                RequiresFollowUp = true,
                FollowUpDate = baseDate.AddDays(52),
                FollowUpInstructions = "Schedule next preventive maintenance in 3 months",
                QualityCheckPassed = true,
                QualityCheckedById = qualityCheckerId,
                QualityCheckDate = baseDate.AddDays(-6),
                QualityCheckNotes = "All work meets quality standards. Asset tested and performing excellently. No defects found.",
                CustomerAcceptance = true,
                AcceptedById = maintenanceManagerId,
                AcceptedDate = baseDate.AddDays(-4),
                AcceptanceNotes = "Asset accepted. Forklift is running smoothly. Very satisfied with the maintenance work.",
                CertificateGenerated = true,
                CertificateGeneratedDate = baseDate.AddDays(-3),
                TenantId = tenantId,
                CreatedAt = baseDate.AddDays(-30),
                CreatedById = maintenanceManagerId,
                CreatedBy = "System"
            });
        }

        _logger.LogInformation("Job cards seeded");
    }

    private async Task SeedJobCardCertificatesAsync(Guid tenantId, DateTime baseDate, Guid supervisorId)
    {
        var forkliftId = Guid.Parse("bbbb1111-1111-1111-1111-111111111111");
        var certificate1Id = Guid.Parse("ffff1111-1111-1111-1111-111111111111");

        // Get the actual JobCard ID from the database
        var jobCard = await _context.JobCards
            .Where(j => j.JobCardNumber == "JC-2025-0001")
            .Select(j => new { j.Id })
            .FirstOrDefaultAsync();

        if (jobCard == null)
        {
            _logger.LogWarning("JobCard JC-2025-0001 not found. Skipping certificate seeding.");
            return;
        }

        if (!await _context.JobCardCertificates.AnyAsync(c => c.CertificateNumber == "CERT-2025-0001"))
        {
            await _context.JobCardCertificates.AddAsync(new JobCardCertificate
            {
                Id = certificate1Id,
                JobCardId = jobCard.Id,
                AssetId = forkliftId,
                CertificateNumber = "CERT-2025-0001",
                CertificateType = "Preventive Maintenance Completion",
                IssuedDate = baseDate.AddDays(-3),
                ValidUntil = baseDate.AddDays(-3).AddMonths(3),
                IssuedById = supervisorId,
                Description = "This certificate confirms that preventive maintenance was completed in accordance with manufacturer specifications and industry standards. All safety checks passed.",
                CertificateData = "{\"inspectorName\":\"John Smith\",\"inspectorLicense\":\"MECH-12345\",\"complianceStandards\":[\"ISO 9001\",\"OEM Standards\"]}",
                FileFormat = "PDF",
                IsActive = true,
                TenantId = tenantId,
                CreatedAt = baseDate.AddDays(-3),
                CreatedBy = "System"
            });
        }

        _logger.LogInformation("Job card certificates seeded");
    }

    private async Task SeedJobCardCommentsAsync(Guid tenantId, DateTime baseDate, Guid maintenanceManagerId, Guid technicianId, Guid qualityCheckerId, Guid supervisorId)
    {
        // Get the actual JobCard ID from the database
        var jobCard = await _context.JobCards
            .Where(j => j.JobCardNumber == "JC-2025-0001")
            .Select(j => new { j.Id })
            .FirstOrDefaultAsync();

        if (jobCard == null)
        {
            _logger.LogWarning("JobCard JC-2025-0001 not found. Skipping comments seeding.");
            return;
        }

        var commentIds = new[]
        {
            Guid.Parse("99991111-0000-0000-0000-000000000001"),
            Guid.Parse("99991111-0000-0000-0000-000000000002"),
            Guid.Parse("99991111-0000-0000-0000-000000000003"),
            Guid.Parse("99991111-0000-0000-0000-000000000004"),
            Guid.Parse("99991111-0000-0000-0000-000000000005")
        };

        if (!await _context.JobCardComments.AnyAsync(c => c.JobCardId == jobCard.Id && c.Comment.Contains("Job card created")))
        {
            await _context.JobCardComments.AddAsync(new JobCardComment
            {
                Id = commentIds[0],
                JobCardId = jobCard.Id,
                CommentById = maintenanceManagerId,
                Comment = "Job card created for scheduled preventive maintenance.",
                CommentType = "General",
                IsInternal = false,
                CommentDate = baseDate.AddDays(-30),
                TenantId = tenantId,
                CreatedAt = baseDate.AddDays(-30),
                CreatedBy = "System"
            });
        }

        _logger.LogInformation("Priority levels seeded");
    }

    private async Task SeedAssetTypesAsync(Guid tenantId, DateTime baseDate)
    {
        var equipmentTypeId = Guid.Parse("dddd1111-1111-1111-1111-111111111111");
        var vehicleTypeId = Guid.Parse("dddd2222-1111-1111-1111-111111111111");

        if (!await _context.AssetTypes.AnyAsync(at => at.Id == equipmentTypeId))
        {
            await _context.AssetTypes.AddAsync(new AssetType
            {
                Id = equipmentTypeId,
                Name = "Equipment",
                Code = "EQP",
                Description = "Heavy equipment and machinery",
                Color = "#FF9800",
                Icon = "engineering",
                IsActive = true,
                RequiresLocation = true,
                RequiresOperatingHours = true,
                RequiresMileageTracking = false,
                RequiresLicensing = false,
                RequiresInspections = true,
                SupportsHierarchy = false,
                RequiresSpecializedFields = true,
                DefaultMaintenanceIntervalDays = 90,
                RequiresPreventiveMaintenance = true,
                RequiresConditionMonitoring = true,
                RequiresSafetyChecks = true,
                RequiresLockoutTagout = true,
                RequiresPermits = false,
                DefaultWorkOrderPriority = 2,
                DefaultEstimatedHours = 4.0,
                DefaultWorkInstructions = "Follow manufacturer's maintenance guidelines and safety protocols.",
                TenantId = tenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        if (!await _context.AssetTypes.AnyAsync(at => at.Id == vehicleTypeId))
        {
            await _context.AssetTypes.AddAsync(new AssetType
            {
                Id = vehicleTypeId,
                Name = "Vehicle",
                Code = "VEH",
                Description = "Motor vehicles and transportation equipment",
                Color = "#2196F3",
                Icon = "directions_car",
                IsActive = true,
                RequiresLocation = true,
                RequiresOperatingHours = false,
                RequiresMileageTracking = true,
                RequiresLicensing = true,
                RequiresInspections = true,
                SupportsHierarchy = false,
                RequiresSpecializedFields = true,
                DefaultMaintenanceIntervalDays = 180,
                RequiresPreventiveMaintenance = true,
                RequiresConditionMonitoring = false,
                RequiresSafetyChecks = true,
                RequiresLockoutTagout = false,
                RequiresPermits = false,
                DefaultWorkOrderPriority = 3,
                DefaultEstimatedHours = 3.0,
                DefaultWorkInstructions = "Perform standard vehicle maintenance according to service schedule.",
                TenantId = tenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        _logger.LogInformation("Asset types seeded");
    }

    private async Task SyncEmployeesToTechniciansAsync(Guid tenantId, DateTime baseDate, List<Guid> employeeIds)
    {
        foreach (var employeeId in employeeIds)
        {
            // Check if technician already exists for this employee
            if (await _context.Technicians.AnyAsync(t => t.EmployeeId == employeeId))
            {
                continue;
            }

            // Get employee details
            var employee = await _context.Employees
                .Where(e => e.Id == employeeId)
                .Select(e => new { e.FirstName, e.LastName, e.EmailAddress, e.MobileNumber, e.EmployeeNumber, e.DepartmentId })
                .FirstOrDefaultAsync();

            if (employee == null) continue;

            // Get department name
            var department = await _context.Departments
                .Where(d => d.Id == employee.DepartmentId)
                .Select(d => d.Name)
                .FirstOrDefaultAsync();

            await _context.Technicians.AddAsync(new Technician
            {
                Id = Guid.NewGuid(),
                EmployeeId = employeeId,
                EmployeeNumber = employee.EmployeeNumber ?? "",
                FirstName = employee.FirstName,
                LastName = employee.LastName,
                Email = employee.EmailAddress,
                Phone = employee.MobileNumber,
                Department = department ?? "Maintenance",
                Position = "Maintenance Technician",
                Specialization = "General Maintenance",
                CertificationLevel = "Level 2",
                ExperienceLevel = "Intermediate",
                HireDate = baseDate.AddYears(-2),
                IsActive = true,
                CurrentWorkload = 0,
                MaxWorkload = 100,
                AverageRating = 0,
                CompletedWorkOrders = 0,
                TenantId = tenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        _logger.LogInformation("Technicians seeded");
    }

    private async Task SeedTaskTemplatesAsync(Guid tenantId, DateTime baseDate)
    {
        var preventiveId = Guid.Parse("cccc1111-1111-1111-1111-111111111111");
        var correctiveId = Guid.Parse("cccc2222-2222-2222-2222-222222222222");
        var inspectionId = Guid.Parse("cccc3333-3333-3333-3333-333333333333");

        // Maintenance Task Templates for Preventive Maintenance
        var preventiveTaskIds = new[]
        {
            Guid.Parse("aaaa1111-1111-1111-1111-111111111111"),
            Guid.Parse("aaaa1111-1111-1111-1111-111111111112"),
            Guid.Parse("aaaa1111-1111-1111-1111-111111111113")
        };

        if (!await _context.MaintenanceTaskTemplates.AnyAsync(t => t.Id == preventiveTaskIds[0]))
        {
            await _context.MaintenanceTaskTemplates.AddRangeAsync(
                new MaintenanceTaskTemplate
                {
                    Id = preventiveTaskIds[0],
                    MaintenanceTypeId = preventiveId,
                    TaskName = "Engine Oil Change",
                    Description = "Replace engine oil and oil filter",
                    Sequence = 1,
                    EstimatedHours = 1.0,
                    IsRequired = true,
                    IsActive = true,
                    TenantId = tenantId,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                },
                new MaintenanceTaskTemplate
                {
                    Id = preventiveTaskIds[1],
                    MaintenanceTypeId = preventiveId,
                    TaskName = "Filter Replacement",
                    Description = "Replace air filter and fuel filter",
                    Sequence = 2,
                    EstimatedHours = 0.5,
                    IsRequired = true,
                    IsActive = true,
                    TenantId = tenantId,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                },
                new MaintenanceTaskTemplate
                {
                    Id = preventiveTaskIds[2],
                    MaintenanceTypeId = preventiveId,
                    TaskName = "General Safety Inspection",
                    Description = "Inspect all safety systems and controls",
                    Sequence = 3,
                    EstimatedHours = 0.75,
                    IsRequired = true,
                    IsActive = true,
                    TenantId = tenantId,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                }
            );
        }

        // Maintenance Task Templates for Corrective Maintenance
        var correctiveTaskIds = new[]
        {
            Guid.Parse("bbbb2222-1111-1111-1111-111111111111"),
            Guid.Parse("bbbb2222-1111-1111-1111-111111111112")
        };

        if (!await _context.MaintenanceTaskTemplates.AnyAsync(t => t.Id == correctiveTaskIds[0]))
        {
            await _context.MaintenanceTaskTemplates.AddRangeAsync(
                new MaintenanceTaskTemplate
                {
                    Id = correctiveTaskIds[0],
                    MaintenanceTypeId = correctiveId,
                    TaskName = "Diagnose Issue",
                    Description = "Identify root cause of the problem",
                    Sequence = 1,
                    EstimatedHours = 1.0,
                    IsRequired = true,
                    IsActive = true,
                    TenantId = tenantId,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                },
                new MaintenanceTaskTemplate
                {
                    Id = correctiveTaskIds[1],
                    MaintenanceTypeId = correctiveId,
                    TaskName = "Repair/Replace Component",
                    Description = "Repair or replace the faulty component",
                    Sequence = 2,
                    EstimatedHours = 2.0,
                    IsRequired = true,
                    IsActive = true,
                    TenantId = tenantId,
                    CreatedAt = baseDate,
                    CreatedBy = "System"
                }
            );
        }

        // Asset Type Task Templates for Equipment
        var equipmentTypeId = Guid.Parse("dddd1111-1111-1111-1111-111111111111");
        var vehicleTypeId = Guid.Parse("dddd2222-1111-1111-1111-111111111111");
        
        var equipmentTaskId = Guid.Parse("eeee1111-1111-1111-1111-111111111111");
        var vehicleTaskId = Guid.Parse("eeee2222-1111-1111-1111-111111111111");

        if (!await _context.AssetTypeTaskTemplates.AnyAsync(t => t.Id == equipmentTaskId))
        {
            await _context.AssetTypeTaskTemplates.AddAsync(new AssetTypeTaskTemplate
            {
                Id = equipmentTaskId,
                AssetTypeId = equipmentTypeId,
                MaintenanceTypeId = preventiveId,
                TaskName = "Hydraulic System Check",
                Description = "Check hydraulic fluid levels and pressure",
                Sequence = 1,
                EstimatedHours = 0.5,
                IsRequired = true,
                IsActive = true,
                TenantId = tenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        if (!await _context.AssetTypeTaskTemplates.AnyAsync(t => t.Id == vehicleTaskId))
        {
            await _context.AssetTypeTaskTemplates.AddAsync(new AssetTypeTaskTemplate
            {
                Id = vehicleTaskId,
                AssetTypeId = vehicleTypeId,
                MaintenanceTypeId = preventiveId,
                TaskName = "Tire Pressure and Condition Check",
                Description = "Inspect tire pressure and tread condition",
                Sequence = 1,
                EstimatedHours = 0.25,
                IsRequired = true,
                IsActive = true,
                TenantId = tenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        // Asset Task Templates for specific assets
        var forkliftId = Guid.Parse("bbbb1111-1111-1111-1111-111111111111");
        var forkliftTaskId = Guid.Parse("cccc1111-1111-1111-1111-111111111111");

        if (!await _context.AssetTaskTemplates.AnyAsync(t => t.Id == forkliftTaskId))
        {
            await _context.AssetTaskTemplates.AddAsync(new AssetTaskTemplate
            {
                Id = forkliftTaskId,
                AssetId = forkliftId,
                MaintenanceTypeId = preventiveId,
                TaskName = "Forklift Mast Inspection",
                Description = "Inspect mast chains and rollers for wear",
                Sequence = 1,
                EstimatedHours = 0.75,
                IsRequired = true,
                IsActive = true,
                TenantId = tenantId,
                CreatedAt = baseDate,
                CreatedBy = "System"
            });
        }

        _logger.LogInformation("Task templates seeded");
    }
}
