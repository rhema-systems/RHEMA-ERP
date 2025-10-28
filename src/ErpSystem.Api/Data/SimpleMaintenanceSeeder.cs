using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Data;

/// <summary>
/// Simplified maintenance workflow seeder for testing
/// </summary>
public static class SimpleMaintenanceSeeder
{
    public static async Task SeedMaintenanceWorkflowAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            logger.LogInformation("Starting simple maintenance workflow seeding...");

            // Get tenant
            var tenant = await context.Tenants.FirstOrDefaultAsync();
            if (tenant == null)
            {
                logger.LogWarning("No tenant found, skipping seeding");
                return;
            }

            var tenantId = tenant.Id;

            // Seed HR data
            await SeedEmployeesAsync(context, tenantId, logger);

            // Seed Maintenance configuration
            await SeedMaintenanceConfigAsync(context, tenantId, logger);

            // Seed Asset categories and assets
            await SeedAssetsAsync(context, tenantId, logger);

            // Seed Sample work orders
            await SeedSampleWorkflowAsync(context, tenantId, logger);

            await context.SaveChangesAsync();
            logger.LogInformation("Simple maintenance workflow seeding completed successfully!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeding maintenance workflow data");
            throw;
        }
    }

    private static async Task SeedEmployeesAsync(ApplicationDbContext context, Guid tenantId, ILogger logger)
    {
        if (await context.Employees.AnyAsync(e => e.TenantId == tenantId))
        {
            logger.LogInformation("Employees already exist, skipping...");
            return;
        }

        logger.LogInformation("Seeding employees...");

        // Create departments first
        var maintenanceDept = new Department
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Maintenance Department",
            Code = "MAINT",
            DepartmentType = DepartmentType.Maintenance,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Departments.Add(maintenanceDept);
        await context.SaveChangesAsync();

        // Create positions
        var techPosition = new EmployeePosition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = "Maintenance Technician",
            Code = "TECH",
            DepartmentId = maintenanceDept.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.EmployeePositions.Add(techPosition);
        await context.SaveChangesAsync();

        // Create employees
        var technician = new Employee
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EmployeeNumber = "EMP001",
            FirstName = "John",
            LastName = "Smith",
            EmailAddress = "john.smith@rhema.com",
            DepartmentId = maintenanceDept.Id,
            PositionId = techPosition.Id,
            StaffStatus = StaffStatus.Active,
            IsActive = true,
            DateEmployed = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)),
            CreatedAt = DateTime.UtcNow
        };

        var inspector = new Employee
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EmployeeNumber = "EMP002",
            FirstName = "Emily",
            LastName = "Davis",
            EmailAddress = "emily.davis@rhema.com",
            DepartmentId = maintenanceDept.Id,
            PositionId = techPosition.Id,
            StaffStatus = StaffStatus.Active,
            IsActive = true,
            DateEmployed = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-3)),
            CreatedAt = DateTime.UtcNow
        };

        context.Employees.AddRange(technician, inspector);
        await context.SaveChangesAsync();

        logger.LogInformation($"Seeded {context.Employees.Count(e => e.TenantId == tenantId)} employees");
    }

    private static async Task SeedMaintenanceConfigAsync(ApplicationDbContext context, Guid tenantId, ILogger logger)
    {
        if (await context.MaintenanceTypes.AnyAsync(m => m.TenantId == tenantId))
        {
            logger.LogInformation("Maintenance config already exists, skipping...");
            return;
        }

        logger.LogInformation("Seeding maintenance configuration...");

        // Maintenance Types
        var preventive = new MaintenanceType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Preventive Maintenance",
            Code = "PM",
            Description = "Scheduled preventive maintenance",
            Category = "Scheduled",
            RequiresApproval = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var corrective = new MaintenanceType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Corrective Maintenance",
            Code = "CM",
            Description = "Repair of failed equipment",
            Category = "Reactive",
            RequiresApproval = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.MaintenanceTypes.AddRange(preventive, corrective);

        // Priority Levels
        var high = new PriorityLevel
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "High",
            Level = 1,
            Color = "#ea580c",
            ResponseTimeHours = 4,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var medium = new PriorityLevel
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Medium",
            Level = 2,
            Color = "#fbbf24",
            ResponseTimeHours = 24,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.PriorityLevels.AddRange(high, medium);

        // Work Order Types
        var emergency = new WorkOrderType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Emergency Repair",
            Code = "EMER",
            Description = "Emergency repairs",
            Color = "#dc2626",
            RequiresApproval = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.WorkOrderTypes.Add(emergency);

        await context.SaveChangesAsync();
        logger.LogInformation("Maintenance configuration seeded successfully");
    }

    private static async Task SeedAssetsAsync(ApplicationDbContext context, Guid tenantId, ILogger logger)
    {
        if (await context.MaintenanceAssets.AnyAsync(a => a.TenantId == tenantId))
        {
            logger.LogInformation("Assets already exist, skipping...");
            return;
        }

        logger.LogInformation("Seeding assets...");

        // Asset Category
        var vehicleCategory = new MaintenanceAssetCategory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Fleet Vehicles",
            Code = "FLEET",
            Description = "Company vehicles",
            AssetType = "Vehicle",
            MaintenanceScheduleType = "multi",
            MaintenanceType = "Distance",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.MaintenanceAssetCategories.Add(vehicleCategory);
        await context.SaveChangesAsync();

        var employee = await context.Employees.FirstAsync(e => e.TenantId == tenantId);

        // Vehicle Asset
        var deliveryVan = new MaintenanceAsset
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Delivery Van - Unit 1",
            AssetNumber = "VEH-001",
            Description = "Mercedes Sprinter delivery van",
            AssetCategoryId = vehicleCategory.Id,
            Manufacturer = "Mercedes-Benz",
            Model = "Sprinter 316 CDI",
            SerialNumber = "WDB9066331N123456",
            EmployeeId = employee.Id,
            PurchaseDate = DateTime.UtcNow.AddYears(-2),
            PurchasePrice = 45000m,
            CurrentValue = 35000m,
            Status = AssetStatus.Active,
            Criticality = AssetCriticality.Medium,
            Mileage = 45000,
            LastMileageUpdate = DateTime.UtcNow.AddDays(-5),
            LicensePlate = "ABC-123",
            VIN = "WDB9066331N123456",
            LastServiceDate = DateTime.UtcNow.AddMonths(-2),
            NextServiceDue = DateTime.UtcNow.AddDays(14),
            CreatedAt = DateTime.UtcNow
        };

        context.MaintenanceAssets.Add(deliveryVan);
        await context.SaveChangesAsync();

        logger.LogInformation("Assets seeded successfully");
    }

    private static async Task SeedSampleWorkflowAsync(ApplicationDbContext context, Guid tenantId, ILogger logger)
    {
        // Skip if job cards already exist
        if (await context.JobCards.AnyAsync(j => j.TenantId == tenantId))
        {
            logger.LogInformation("Sample workflow already exists, skipping...");
            return;
        }

        logger.LogInformation("Seeding sample workflow...");

        var vehicle = await context.MaintenanceAssets
            .FirstOrDefaultAsync(a => a.TenantId == tenantId);
        var technician = await context.Employees
            .FirstOrDefaultAsync(e => e.TenantId == tenantId);
        var maintenanceType = await context.MaintenanceTypes
            .FirstOrDefaultAsync(m => m.TenantId == tenantId);
        var priorityLevel = await context.PriorityLevels
            .FirstOrDefaultAsync(p => p.TenantId == tenantId);
        var workOrderType = await context.WorkOrderTypes
            .FirstOrDefaultAsync(w => w.TenantId == tenantId);

        // Check if all required entities exist
        if (vehicle == null || technician == null || maintenanceType == null || priorityLevel == null || workOrderType == null)
        {
            logger.LogWarning("Missing required entities for workflow seeding, skipping...");
            return;
        }

        // Create Job Card
        var jobCard = new JobCard
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JobCardNumber = "JC-2025-001",
            AssetId = vehicle.Id,
            MaintenanceTypeId = maintenanceType.Id,
            PriorityLevelId = priorityLevel.Id,
            Title = "Engine Diagnosis and Repair",
            Description = "Vehicle making unusual noise from engine",
            ProblemDescription = "Vehicle making unusual noise from engine, loss of power",
            RequestedById = technician.Id,
            RequestedDate = DateTime.UtcNow.AddDays(-2),
            RequiredCompletionDate = DateTime.UtcNow.AddDays(3),
            EstimatedHours = 8,
            EstimatedCost = 1500m,
            AssignedTechnicianId = technician.Id,
            JobCardStatus = "Approved",
            ApprovalStatus = "Approved",
            ApprovedDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };

        context.JobCards.Add(jobCard);

        // Create Work Order
        var workOrder = new WorkOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkOrderNumber = "WO-2025-001",
            Title = "Engine Diagnosis and Repair",
            Description = "Diagnose and repair engine noise and power loss issue",
            AssetId = vehicle.Id,
            WorkOrderTypeId = workOrderType.Id,
            MaintenanceTypeId = maintenanceType.Id,
            PriorityLevelId = priorityLevel.Id,
            AssignedTechnicianId = technician.Id,
            Status = "InProgress",
            RequestedStartDate = DateTime.UtcNow.AddDays(-1),
            ActualStartDate = DateTime.UtcNow.AddDays(-1),
            RequestedCompletionDate = DateTime.UtcNow.AddDays(2),
            EstimatedHours = 8,
            EstimatedCost = 1500m,
            CreatedById = technician.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };

        context.WorkOrders.Add(workOrder);
        await context.SaveChangesAsync();

        logger.LogInformation($"Sample workflow seeded: Job Card {jobCard.JobCardNumber}, Work Order {workOrder.WorkOrderNumber}");
    }
}
