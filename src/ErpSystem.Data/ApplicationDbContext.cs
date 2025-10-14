using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Data.Configuration;

namespace ErpSystem.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid, 
    Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>, ApplicationUserRole, 
    Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>,
    Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>, 
    Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>
{
    private readonly Guid? _tenantId;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, Guid? tenantId) : base(options)
    {
        _tenantId = tenantId;
    }

    // Core entities
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantModule> TenantModules { get; set; }
    public DbSet<UserTenant> UserTenants { get; set; }
    
    // Settings entities
    public DbSet<EmailSettings> EmailSettings { get; set; }
    public DbSet<EmailTemplate> EmailTemplates { get; set; }
    public DbSet<SystemSettings> SystemSettings { get; set; }
    public DbSet<Security> Securities { get; set; }
    
    // Logging entities
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<SecurityLog> SecurityLogs { get; set; }
    
    // Security entities
    public DbSet<SecurityAlert> SecurityAlerts { get; set; }
    public DbSet<SecurityMetrics> SecurityMetricsSet { get; set; }
    public DbSet<ThreatDetection> ThreatDetections { get; set; }
    public DbSet<ThreatIndicator> ThreatIndicators { get; set; }
    public DbSet<SecurityPolicy> SecurityPolicies { get; set; }
    public DbSet<SecurityPolicyViolation> SecurityPolicyViolations { get; set; }
    
    // Token management entities
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<BlacklistedToken> BlacklistedTokens { get; set; }
    public DbSet<UserSession> UserSessions { get; set; }
    
    // Permission entities
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    
    // Report entities
    public DbSet<Report> Reports { get; set; }
    public DbSet<ReportSchedule> ReportSchedules { get; set; }
    public DbSet<ReportTemplate> ReportTemplates { get; set; }
    public DbSet<ReportExecution> ReportExecutions { get; set; }
    public DbSet<UserReportFavorite> UserReportFavorites { get; set; }
    public DbSet<ReportExport> ReportExports { get; set; }
    public DbSet<ReportRoleAssignment> ReportRoleAssignments { get; set; }
    
    // Report data source entities
    public DbSet<DataSource> ReportDataSources { get; set; }
    public DbSet<DataSourceUsageLog> ReportDataSourceUsageLogs { get; set; }
    
    // Maintenance Management entities
    public DbSet<MaintenanceAsset> MaintenanceAssets { get; set; }
    public DbSet<MaintenanceAssetCategory> MaintenanceAssetCategories { get; set; }
    public DbSet<AssetType> AssetTypes { get; set; }
    public DbSet<AssetTypeField> AssetTypeFields { get; set; }
    public DbSet<WorkOrder> WorkOrders { get; set; }
    public DbSet<WorkOrderType> WorkOrderTypes { get; set; }
    public DbSet<MaintenanceType> MaintenanceTypes { get; set; }
    public DbSet<PriorityLevel> PriorityLevels { get; set; }
    public DbSet<WorkOrderTask> WorkOrderTasks { get; set; }
    public DbSet<WorkOrderPart> WorkOrderParts { get; set; }
    public DbSet<WorkOrderLabor> WorkOrderLabor { get; set; }
    public DbSet<WorkOrderDocument> WorkOrderDocuments { get; set; }
    public DbSet<WorkOrderComment> WorkOrderComments { get; set; }
    public DbSet<MaintenanceSchedule> MaintenanceSchedules { get; set; }
    public DbSet<InspectionTemplate> InspectionTemplates { get; set; }
    public DbSet<AssetInspection> AssetInspections { get; set; }
    public DbSet<InspectionDocument> InspectionDocuments { get; set; }
    public DbSet<TechnicianTeam> TechnicianTeams { get; set; }
    public DbSet<TechnicianTeamMember> TechnicianTeamMembers { get; set; }
    public DbSet<TechnicianSkill> TechnicianSkills { get; set; }
    public DbSet<UserTechnicianSkill> UserTechnicianSkills { get; set; }
    public DbSet<AssetDowntime> AssetDowntimes { get; set; }
    public DbSet<MaintenanceAttachment> MaintenanceAttachments { get; set; }
    
    // Technician Scheduling entities
    public DbSet<TechnicianSchedule> TechnicianSchedules { get; set; }
    public DbSet<TechnicianAvailability> TechnicianAvailabilities { get; set; }
    public DbSet<TechnicianShift> TechnicianShifts { get; set; }
    
    // New Maintenance Management entities
    public DbSet<TechnicalSkill> TechnicalSkills { get; set; }
    public DbSet<TechnicianSkillAssignment> TechnicianSkillAssignments { get; set; }
    public DbSet<SafetyProtocol> SafetyProtocols { get; set; }
    public DbSet<SafetyComplianceRecord> SafetyComplianceRecords { get; set; }
    public DbSet<Technician> Technicians { get; set; }
    
    // Additional Maintenance entities
    public DbSet<TechnicianCertification> TechnicianCertifications { get; set; }
    public DbSet<ProtocolAdherence> ProtocolAdherences { get; set; }
    public DbSet<ProtocolViolation> ProtocolViolations { get; set; }
    public DbSet<ProtocolTraining> ProtocolTrainings { get; set; }
    public DbSet<SafetyAudit> SafetyAudits { get; set; }
    public DbSet<ProtocolAuditDetail> ProtocolAuditDetails { get; set; }
    
    // HR entities
    public DbSet<Employee> Employees { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<Section> Sections { get; set; }
    public DbSet<EmployeePosition> EmployeePositions { get; set; }
    public DbSet<Country> Countries { get; set; }
    public DbSet<Shift> Shifts { get; set; }
    public DbSet<WorkStation> WorkStations { get; set; }
    public DbSet<EmployeeEmergencyContact> EmployeeEmergencyContacts { get; set; }
    public DbSet<EmployeeDependent> EmployeeDependents { get; set; }
    public DbSet<EmployeeQualification> EmployeeQualifications { get; set; }
    public DbSet<EmployeeIdentificationCard> EmployeeIdentificationCards { get; set; }
    public DbSet<EmployeeWorkHistory> EmployeeWorkHistories { get; set; }
    public DbSet<EmployeeContractDetail> EmployeeContractDetails { get; set; }
    public DbSet<EmployeeSkill> EmployeeSkills { get; set; }
    public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
    public DbSet<EmployeeBiometric> EmployeeBiometrics { get; set; }
    public DbSet<ShiftAssignment> ShiftAssignments { get; set; }
    public DbSet<EmployeeShiftPreference> EmployeeShiftPreferences { get; set; }
    
    // Inventory entities
    public DbSet<InventoryItem> InventoryItems { get; set; }
    public DbSet<InventoryCategory> InventoryCategories { get; set; }
    public DbSet<StockMovement> StockMovements { get; set; }
    public DbSet<StockAdjustment> StockAdjustments { get; set; }
    public DbSet<StockAdjustmentItem> StockAdjustmentItems { get; set; }
    public DbSet<Warehouse> Warehouses { get; set; }
    public DbSet<WarehouseLocation> WarehouseLocations { get; set; }
    public DbSet<InventoryLocation> InventoryLocations { get; set; }
    public DbSet<InventoryAllocation> InventoryAllocations { get; set; }
    
    // Procurement entities
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<SupplierContact> SupplierContacts { get; set; }
    public DbSet<SupplierItemCatalog> SupplierItemCatalogs { get; set; }
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
    public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }
    public DbSet<PurchaseOrderReceipt> PurchaseOrderReceipts { get; set; }
    public DbSet<PurchaseOrderReceiptItem> PurchaseOrderReceiptItems { get; set; }
    public DbSet<PurchaseRequisition> PurchaseRequisitions { get; set; }
    public DbSet<PurchaseRequisitionItem> PurchaseRequisitionItems { get; set; }
    
    // Workflow Engine entities
    public DbSet<WorkflowDefinition> WorkflowDefinitions { get; set; }
    public DbSet<WorkflowStep> WorkflowSteps { get; set; }
    public DbSet<WorkflowTransition> WorkflowTransitions { get; set; }
    public DbSet<WorkflowInstance> WorkflowInstances { get; set; }
    public DbSet<WorkflowStepInstance> WorkflowStepInstances { get; set; }
    public DbSet<WorkflowActivityLog> WorkflowActivityLogs { get; set; }
    public DbSet<WorkflowApproval> WorkflowApprovals { get; set; }
    public DbSet<WorkflowEntityType> WorkflowEntityTypes { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply entity configurations
        builder.ApplyConfiguration(new ApplicationUserConfiguration());
        builder.ApplyConfiguration(new TenantConfiguration());
        builder.ApplyConfiguration(new UserTenantConfiguration());
        builder.ApplyConfiguration(new AssetTypeConfiguration());
        builder.ApplyConfiguration(new AssetTypeFieldConfiguration());

        // Configure Identity tables with custom names
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.UserName).IsUnique();
            
            // Primary tenant relationship
            entity.HasOne(u => u.Tenant)
                .WithMany()
                .HasForeignKey(u => u.TenantId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            // Configure relationship to UserTenants
            entity.HasMany(u => u.UserTenants)
                .WithOne(ut => ut.User)
                .HasForeignKey(ut => ut.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            
            // Ignore computed helper properties
            entity.Ignore(u => u.FullName);
            entity.Ignore(u => u.DefaultTenant);
            entity.Ignore(u => u.AccessibleTenants);
            entity.Ignore(u => u.ActiveTenantRelationships);
            entity.Ignore(u => u.SuspendedTenantRelationships);
            entity.Ignore(u => u.AllTenantRelationships);
        });
        builder.Entity<ApplicationUserRole>(entity =>
        {
            entity.ToTable("UserRoles");
            entity.HasOne(ur => ur.User).WithMany(u => u.UserRoles).HasForeignKey(ur => ur.UserId);
            entity.HasOne(ur => ur.Role).WithMany(r => r.UserRoles).HasForeignKey(ur => ur.RoleId);
        });
        
        // Configure UserTenant junction entity
        builder.Entity<UserTenant>(entity =>
        {
            entity.ToTable("UserTenants");
            
            // Configure properties
            entity.Property(ut => ut.AccessLevel).HasConversion<int>();
            entity.Property(ut => ut.GrantedAt).IsRequired();
        });

        // Configure Tenant entity
        builder.Entity<Tenant>(entity =>
        {
            // Configure relationship to UserTenants
            entity.HasMany(t => t.UserTenants)
                .WithOne(ut => ut.Tenant)
                .HasForeignKey(ut => ut.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            
            // Ignore computed helper properties that shouldn't be treated as navigation properties
            entity.Ignore(t => t.Users);
            entity.Ignore(t => t.ActiveUserCount);
        });

        // Configure TenantModule entity
        builder.Entity<TenantModule>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ModuleName }).IsUnique();
        });
        
        // Configure EmailSettings entity
        builder.Entity<EmailSettings>(entity =>
        {
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId);
            entity.HasIndex(e => e.TenantId); // One email setting per tenant
        });
        
        // Configure EmailTemplate entity
        builder.Entity<EmailTemplate>(entity =>
        {
            entity.HasOne(et => et.Tenant).WithMany().HasForeignKey(et => et.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(et => new { et.TenantId, et.Name }).IsUnique();
            entity.HasIndex(et => et.Module);
            entity.HasIndex(et => et.Category);
        });
        
        
        // Configure SystemSettings entity
        builder.Entity<SystemSettings>(entity =>
        {
            entity.HasOne(s => s.Tenant).WithMany().HasForeignKey(s => s.TenantId);
            entity.HasIndex(s => new { s.TenantId, s.Key }).IsUnique();
        });
        
        // Configure AuditLog entity
        builder.Entity<AuditLog>(entity =>
        {
            entity.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(a => a.Tenant).WithMany().HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(a => a.Timestamp);
            entity.HasIndex(a => a.UserId);
            entity.HasIndex(a => a.Resource);
        });
        
        // Configure SecurityLog entity
        builder.Entity<SecurityLog>(entity =>
        {
            entity.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(s => s.Tenant).WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(s => s.Timestamp);
            entity.HasIndex(s => s.IpAddress);
            entity.HasIndex(s => s.Action);
        });
        
        // Configure Security entity
        builder.Entity<Security>(entity =>
        {
            entity.HasOne(s => s.Tenant).WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(s => s.TenantId);
        });
        
        // Configure SecurityAlert entity
        builder.Entity<SecurityAlert>(entity =>
        {
            entity.HasOne(sa => sa.Tenant).WithMany().HasForeignKey(sa => sa.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(sa => sa.AffectedUserEntity).WithMany().HasForeignKey(sa => sa.AffectedUserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(sa => sa.TenantId);
            entity.HasIndex(sa => sa.Timestamp);
            entity.HasIndex(sa => sa.Type);
            entity.HasIndex(sa => sa.Category);
            entity.HasIndex(sa => sa.Dismissed);
        });
        
        // Configure SecurityMetrics entity
        builder.Entity<SecurityMetrics>(entity =>
        {
            entity.HasOne(sm => sm.Tenant).WithMany().HasForeignKey(sm => sm.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(sm => new { sm.TenantId, sm.MetricDate }).IsUnique();
            entity.HasIndex(sm => sm.MetricDate);
        });
        
        // Configure ThreatDetection entity
        builder.Entity<ThreatDetection>(entity =>
        {
            entity.HasOne(td => td.Tenant).WithMany().HasForeignKey(td => td.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(td => td.AffectedUser).WithMany().HasForeignKey(td => td.AffectedUserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(td => td.TenantId);
            entity.HasIndex(td => td.DetectedAt);
            entity.HasIndex(td => td.ThreatType);
            entity.HasIndex(td => td.Status);
            entity.HasIndex(td => td.Severity);
            entity.HasIndex(td => td.IpAddress);
        });
        
        // Configure ThreatIndicator entity
        builder.Entity<ThreatIndicator>(entity =>
        {
            entity.HasOne(ti => ti.ThreatDetection).WithMany(td => td.Indicators).HasForeignKey(ti => ti.ThreatDetectionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(ti => ti.Tenant).WithMany().HasForeignKey(ti => ti.TenantId).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(ti => ti.ThreatDetectionId);
            entity.HasIndex(ti => ti.IndicatorType);
            entity.HasIndex(ti => ti.Value);
            entity.HasIndex(ti => ti.IsActive);
        });
        
        // Configure SecurityPolicy entity
        builder.Entity<SecurityPolicy>(entity =>
        {
            entity.HasOne(sp => sp.Tenant).WithMany().HasForeignKey(sp => sp.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(sp => sp.TenantId);
            entity.HasIndex(sp => sp.Name);
            entity.HasIndex(sp => sp.Type);
            entity.HasIndex(sp => sp.IsActive);
            entity.HasIndex(sp => sp.Priority);
        });
        
        // Configure SecurityPolicyViolation entity
        builder.Entity<SecurityPolicyViolation>(entity =>
        {
            entity.HasOne(spv => spv.SecurityPolicy).WithMany(sp => sp.Violations).HasForeignKey(spv => spv.SecurityPolicyId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(spv => spv.User).WithMany().HasForeignKey(spv => spv.UserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(spv => spv.Tenant).WithMany().HasForeignKey(spv => spv.TenantId).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(spv => spv.SecurityPolicyId);
            entity.HasIndex(spv => spv.UserId);
            entity.HasIndex(spv => spv.DetectedAt);
            entity.HasIndex(spv => spv.ViolationType);
            entity.HasIndex(spv => spv.Severity);
            entity.HasIndex(spv => spv.IsResolved);
        });
        
        // Configure Permission entity
        builder.Entity<Permission>(entity =>
        {
            entity.ToTable("Permissions");
            entity.HasIndex(p => p.Name).IsUnique();
            entity.HasIndex(p => p.Category);
        });
        
        // Configure RolePermission junction entity
        builder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("RolePermissions");
            entity.HasKey(rp => new { rp.RoleId, rp.PermissionId });
            
            entity.HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // Configure RefreshToken entity
        builder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasOne(rt => rt.User)
                .WithMany()
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(rt => rt.Tenant)
                .WithMany()
                .HasForeignKey(rt => rt.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(rt => rt.TokenHash).IsUnique();
            entity.HasIndex(rt => rt.UserId);
            entity.HasIndex(rt => rt.ExpiresAt);
        });
        
        // Configure BlacklistedToken entity
        builder.Entity<BlacklistedToken>(entity =>
        {
            entity.ToTable("BlacklistedTokens");
            entity.HasOne(bt => bt.User)
                .WithMany()
                .HasForeignKey(bt => bt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasIndex(bt => bt.Jti).IsUnique();
            entity.HasIndex(bt => bt.UserId);
            entity.HasIndex(bt => bt.ExpiresAt);
        });
        
        // Configure Report entity
        builder.Entity<Report>(entity =>
        {
            entity.ToTable("Reports");
            entity.HasOne(r => r.Tenant).WithMany().HasForeignKey(r => r.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(r => r.Module).WithMany(m => m.Reports).HasForeignKey(r => r.ModuleId).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(r => r.TenantId);
            entity.HasIndex(r => r.ModuleId);
            entity.HasIndex(r => r.Type);
            entity.HasIndex(r => r.Status);
            entity.HasIndex(r => r.IsScheduled);
            entity.HasIndex(r => r.LastRun);
        });
        
        // Configure ReportSchedule entity
        builder.Entity<ReportSchedule>(entity =>
        {
            entity.ToTable("ReportSchedules");
            entity.HasOne(s => s.Report).WithMany(r => r.Schedules).HasForeignKey(s => s.ReportId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(s => s.Tenant).WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(s => s.ReportId);
            entity.HasIndex(s => s.IsActive);
            entity.HasIndex(s => s.NextExecutionDate);
            entity.HasIndex(s => s.Frequency);
        });
        
        // Configure ReportTemplate entity
        builder.Entity<ReportTemplate>(entity =>
        {
            entity.ToTable("ReportTemplates");
            entity.HasOne(t => t.Tenant).WithMany().HasForeignKey(t => t.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(t => t.TenantId);
            entity.HasIndex(t => t.Category);
            entity.HasIndex(t => t.Type);
            entity.HasIndex(t => t.UsageCount);
        });
        
        // Configure ReportExecution entity
        builder.Entity<ReportExecution>(entity =>
        {
            entity.ToTable("ReportExecutions");
            entity.HasOne(e => e.Report).WithMany(r => r.Executions).HasForeignKey(e => e.ReportId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(e => e.ReportId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ExecutedAt);
            entity.HasIndex(e => e.Status);
        });
        
        // Configure UserReportFavorite entity
        builder.Entity<UserReportFavorite>(entity =>
        {
            entity.ToTable("UserReportFavorites");
            entity.HasKey(f => new { f.ReportId, f.UserId }); // Composite key
            entity.HasOne(f => f.Report).WithMany(r => r.Favorites).HasForeignKey(f => f.ReportId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(f => f.User).WithMany().HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(f => f.Tenant).WithMany().HasForeignKey(f => f.TenantId).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(f => f.UserId);
            entity.HasIndex(f => f.FavoritedAt);
        });
        
        // Configure ReportExport entity
        builder.Entity<ReportExport>(entity =>
        {
            entity.ToTable("ReportExports");
            entity.HasOne(e => e.Report).WithMany().HasForeignKey(e => e.ReportId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(e => e.ReportId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ExportedAt);
            entity.HasIndex(e => e.Format);
        });
        
        // Configure ReportRoleAssignment entity
        builder.Entity<ReportRoleAssignment>(entity =>
        {
            entity.ToTable("ReportRoleAssignments");
            entity.HasOne(rra => rra.Report).WithMany(r => r.RoleAssignments).HasForeignKey(rra => rra.ReportId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(rra => rra.Role).WithMany().HasForeignKey(rra => rra.RoleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(rra => rra.Tenant).WithMany().HasForeignKey(rra => rra.TenantId).OnDelete(DeleteBehavior.NoAction);
            entity.HasIndex(rra => new { rra.ReportId, rra.RoleId }).IsUnique();
            entity.HasIndex(rra => rra.ReportId);
            entity.HasIndex(rra => rra.RoleId);
            entity.HasIndex(rra => rra.AssignedAt);
        });
        
        // Configure DataSource entity
        builder.Entity<DataSource>(entity =>
        {
            entity.ToTable("ReportDataSources");
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.LastUsed);
            entity.HasIndex(e => e.CreatedAt);
            
            // Configure relationships
            entity.HasOne(d => d.CreatedByUser)
                .WithMany()
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
                
            entity.HasMany(d => d.UsageLogs)
                .WithOne(ul => ul.DataSource)
                .HasForeignKey(ul => ul.DataSourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // Configure DataSourceUsageLog entity
        builder.Entity<DataSourceUsageLog>(entity =>
        {
            entity.ToTable("ReportDataSourceUsageLogs");
            entity.HasIndex(e => e.DataSourceId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.AccessedAt);
            entity.HasIndex(e => e.OperationType);
            entity.HasIndex(e => e.Success);
        });
        
        // Configure decimal precision globally
        ConfigureDecimalPrecision(builder);
        
        // Configure all tenant relationships to avoid cascade conflicts
        ConfigureGlobalTenantRelationships(builder);
        
        // Configure HR Management entities
        ConfigureHREntities(builder);
        
        // Configure Maintenance Management entities
        ConfigureMaintenanceEntities(builder);
        
        // Configure Inventory Management entities
        ConfigureInventoryEntities(builder);
        
        // Configure Workflow Engine entities
        ConfigureWorkflowEntities(builder);

        // Apply global query filters for soft delete and multitenancy
        ApplyGlobalFilters(builder);

        // Seed initial data
        SeedData(builder);
    }
    
    private void ConfigureWorkflowEntities(ModelBuilder builder)
    {
        // Configure WorkflowEntityType relationships
        builder.Entity<WorkflowEntityType>(entity =>
        {
            entity.HasIndex(et => et.Name);
            entity.HasIndex(et => et.IsActive);
            entity.HasIndex(et => new { et.TenantId, et.Name }).IsUnique();
            
            entity.HasMany(et => et.WorkflowDefinitions)
                .WithOne(wd => wd.EntityType)
                .HasForeignKey(wd => wd.EntityTypeId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // WorkflowInstances navigation property will be configured in WorkflowInstance entity
        });
        
        // Configure WorkflowDefinition relationships
        builder.Entity<WorkflowDefinition>(entity =>
        {
            entity.HasIndex(wd => wd.EntityTypeId);
            entity.HasIndex(wd => wd.Name);
            entity.HasIndex(wd => wd.IsActive);
            entity.HasIndex(wd => new { wd.TenantId, wd.Name }).IsUnique();
            
            entity.HasMany(wd => wd.Steps)
                .WithOne(ws => ws.WorkflowDefinition)
                .HasForeignKey(ws => ws.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasMany(wd => wd.Instances)
                .WithOne(wi => wi.WorkflowDefinition)
                .HasForeignKey(wi => wi.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Restrict); // Avoid cascade conflicts
        });
        
        // Configure WorkflowStep relationships
        builder.Entity<WorkflowStep>(entity =>
        {
            entity.HasIndex(ws => ws.WorkflowDefinitionId);
            entity.HasIndex(ws => ws.Name);
            entity.HasIndex(ws => ws.Order);
            entity.HasIndex(ws => ws.StepType);
            entity.HasIndex(ws => ws.IsStartStep);
            entity.HasIndex(ws => ws.IsEndStep);
            
            entity.HasMany(ws => ws.StepInstances)
                .WithOne(si => si.WorkflowStep)
                .HasForeignKey(si => si.WorkflowStepId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure WorkflowTransition self-referencing relationships
        builder.Entity<WorkflowTransition>(entity =>
        {
            entity.HasIndex(wt => wt.WorkflowDefinitionId);
            entity.HasIndex(wt => wt.FromStepId);
            entity.HasIndex(wt => wt.ToStepId);
            entity.HasIndex(wt => wt.Priority);
            entity.HasIndex(wt => wt.IsDefault);
            entity.HasIndex(wt => new { wt.FromStepId, wt.ToStepId });
            
            // FromStep (OutgoingTransitions)
            entity.HasOne(t => t.FromStep)
                .WithMany(s => s.OutgoingTransitions)
                .HasForeignKey(t => t.FromStepId)
                .OnDelete(DeleteBehavior.Restrict);

            // ToStep (IncomingTransitions)
            entity.HasOne(t => t.ToStep)
                .WithMany(s => s.IncomingTransitions)
                .HasForeignKey(t => t.ToStepId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure WorkflowInstance relationships
        builder.Entity<WorkflowInstance>(entity =>
        {
            entity.HasIndex(wi => wi.WorkflowDefinitionId);
            entity.HasIndex(wi => wi.EntityTypeId);
            entity.HasIndex(wi => wi.EntityId);
            entity.HasIndex(wi => wi.Status);
            entity.HasIndex(wi => wi.StartedById);
            entity.HasIndex(wi => wi.StartedDate);
            // WorkflowInstance doesn't have DueDate property
            entity.HasIndex(wi => wi.Priority);
            entity.HasIndex(wi => new { wi.EntityTypeId, wi.EntityId });
            
            entity.HasOne(wi => wi.CurrentStep)
                .WithMany()
                .HasForeignKey(wi => wi.CurrentStepId)
                .OnDelete(DeleteBehavior.NoAction); // Avoid cycles
                
            entity.HasOne(wi => wi.InitiatedBy)
                .WithMany()
                .HasForeignKey(wi => wi.InitiatedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // WorkflowStepInstance configuration
        builder.Entity<WorkflowStepInstance>(entity =>
        {
            entity.HasIndex(si => si.WorkflowInstanceId);
            entity.HasIndex(si => si.WorkflowStepId);
            entity.HasIndex(si => si.Status);
            entity.HasIndex(si => si.AssignedToId);
            entity.HasIndex(si => si.StartedDate);
            entity.HasIndex(si => si.DueDate);
            
            entity.HasOne(si => si.WorkflowInstance)
                .WithMany(wi => wi.StepInstances)
                .HasForeignKey(si => si.WorkflowInstanceId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(si => si.AssignedTo)
                .WithMany()
                .HasForeignKey(si => si.AssignedToId)
                .OnDelete(DeleteBehavior.NoAction);
                
            entity.HasMany(si => si.Approvals)
                .WithOne(wa => wa.StepInstance)
                .HasForeignKey(wa => wa.StepInstanceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // Configure WorkflowApproval relationships
        builder.Entity<WorkflowApproval>(entity =>
        {
            entity.HasIndex(wa => wa.StepInstanceId);
            entity.HasIndex(wa => wa.ApproverId);
            entity.HasIndex(wa => wa.Status);
            entity.HasIndex(wa => wa.RequestedDate);
            entity.HasIndex(wa => wa.DueDate);
            
            entity.HasOne(wa => wa.Approver)
                .WithMany()
                .HasForeignKey(wa => wa.ApproverId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        
        // Configure WorkflowActivityLog relationships
        builder.Entity<WorkflowActivityLog>(entity =>
        {
            entity.HasIndex(wal => wal.WorkflowInstanceId);
            entity.HasIndex(wal => wal.StepInstanceId);
            entity.HasIndex(wal => wal.ActivityType);
            entity.HasIndex(wal => wal.ActivityDate);
            entity.HasIndex(wal => wal.PerformedById);
            
            entity.HasOne(wal => wal.WorkflowInstance)
                .WithMany(wi => wi.ActivityLogs)
                .HasForeignKey(wal => wal.WorkflowInstanceId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(wal => wal.StepInstance)
                .WithMany()
                .HasForeignKey(wal => wal.StepInstanceId)
                .OnDelete(DeleteBehavior.NoAction); // Optional relationship
                
            entity.HasOne(wal => wal.PerformedBy)
                .WithMany()
                .HasForeignKey(wal => wal.PerformedById)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private void ConfigureMaintenanceEntities(ModelBuilder builder)
    {
        // Configure MaintenanceAsset entity
        builder.Entity<MaintenanceAsset>(entity =>
        {
            entity.HasIndex(a => a.AssetNumber);
            entity.HasIndex(a => a.AssetCategoryId);
            entity.HasIndex(a => a.Status);
            entity.HasIndex(a => a.Criticality);
            entity.HasIndex(a => a.ParentAssetId);
            entity.HasIndex(a => a.SerialNumber);
            
            entity.HasOne(a => a.AssetCategory)
                .WithMany(c => c.Assets)
                .HasForeignKey(a => a.AssetCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasOne(a => a.ParentAsset)
                .WithMany(pa => pa.ChildAssets)
                .HasForeignKey(a => a.ParentAssetId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        
        // Configure MaintenanceAssetCategory entity
        builder.Entity<MaintenanceAssetCategory>(entity =>
        {
            entity.HasIndex(c => c.Code);
            entity.HasIndex(c => c.IsActive);
            entity.HasIndex(c => c.ParentCategoryId);
            
            // Configure parent-child relationship
            entity.HasOne(c => c.ParentCategory)
                .WithMany(pc => pc.ChildCategories)
                .HasForeignKey(c => c.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        
        // Configure WorkOrder entity
        builder.Entity<WorkOrder>(entity =>
        {
            entity.HasIndex(wo => wo.WorkOrderNumber).IsUnique();
            entity.HasIndex(wo => wo.AssetId);
            entity.HasIndex(wo => wo.Status);
            entity.HasIndex(wo => wo.AssignedTechnicianId);
            entity.HasIndex(wo => wo.AssignedTeamId);
            entity.HasIndex(wo => wo.RequestedStartDate);
            entity.HasIndex(wo => wo.RequestedCompletionDate);
            entity.HasIndex(wo => wo.ParentWorkOrderId);
            entity.HasIndex(wo => wo.MaintenanceScheduleId);
            
            entity.HasOne(wo => wo.Asset)
                .WithMany(a => a.WorkOrders)
                .HasForeignKey(wo => wo.AssetId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasOne(wo => wo.WorkOrderType)
                .WithMany(wot => wot.WorkOrders)
                .HasForeignKey(wo => wo.WorkOrderTypeId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasOne(wo => wo.MaintenanceType)
                .WithMany(mt => mt.WorkOrders)
                .HasForeignKey(wo => wo.MaintenanceTypeId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasOne(wo => wo.PriorityLevel)
                .WithMany(pl => pl.WorkOrders)
                .HasForeignKey(wo => wo.PriorityLevelId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasOne(wo => wo.AssignedTechnician)
                .WithMany()
                .HasForeignKey(wo => wo.AssignedTechnicianId)
                .OnDelete(DeleteBehavior.NoAction);
                
            entity.HasOne(wo => wo.AssignedTeam)
                .WithMany(t => t.WorkOrders)
                .HasForeignKey(wo => wo.AssignedTeamId)
                .OnDelete(DeleteBehavior.NoAction);
                
            entity.HasOne(wo => wo.RequestedBy)
                .WithMany()
                .HasForeignKey(wo => wo.RequestedById)
                .OnDelete(DeleteBehavior.NoAction);
                
            entity.HasOne(wo => wo.ApprovedBy)
                .WithMany()
                .HasForeignKey(wo => wo.ApprovedById)
                .OnDelete(DeleteBehavior.NoAction);
                
            entity.HasOne(wo => wo.ParentWorkOrder)
                .WithMany(pwo => pwo.ChildWorkOrders)
                .HasForeignKey(wo => wo.ParentWorkOrderId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasOne(wo => wo.MaintenanceSchedule)
                .WithMany()
                .HasForeignKey(wo => wo.MaintenanceScheduleId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        
        // Configure WorkOrderType entity
        builder.Entity<WorkOrderType>(entity =>
        {
            entity.HasIndex(wot => wot.Code);
            entity.HasIndex(wot => wot.IsActive);
        });
        
        // Configure MaintenanceType entity
        builder.Entity<MaintenanceType>(entity =>
        {
            entity.HasIndex(mt => mt.Code);
            entity.HasIndex(mt => mt.Category);
            entity.HasIndex(mt => mt.IsActive);
        });
        
        // Configure PriorityLevel entity
        builder.Entity<PriorityLevel>(entity =>
        {
            entity.HasIndex(pl => pl.Level).IsUnique();
            entity.HasIndex(pl => pl.IsActive);
        });
        
        // Configure WorkOrderTask entity
        builder.Entity<WorkOrderTask>(entity =>
        {
            entity.HasIndex(wot => wot.WorkOrderId);
            entity.HasIndex(wot => wot.AssignedTechnicianId);
            entity.HasIndex(wot => wot.Status);
            entity.HasIndex(wot => wot.Sequence);
            
            entity.HasOne(wot => wot.WorkOrder)
                .WithMany(wo => wo.Tasks)
                .HasForeignKey(wot => wot.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(wot => wot.AssignedTechnician)
                .WithMany()
                .HasForeignKey(wot => wot.AssignedTechnicianId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        
        // Configure WorkOrderPart entity
        builder.Entity<WorkOrderPart>(entity =>
        {
            entity.HasIndex(wop => wop.WorkOrderId);
            entity.HasIndex(wop => wop.ItemCode);
            entity.HasIndex(wop => wop.Status);
            entity.HasIndex(wop => wop.InventoryItemId);
            
            entity.HasOne(wop => wop.WorkOrder)
                .WithMany(wo => wo.Parts)
                .HasForeignKey(wop => wop.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // Configure WorkOrderLabor entity
        builder.Entity<WorkOrderLabor>(entity =>
        {
            entity.HasIndex(wol => wol.WorkOrderId);
            entity.HasIndex(wol => wol.TechnicianId);
            entity.HasIndex(wol => wol.StartTime);
            entity.HasIndex(wol => wol.LaborType);
            
            entity.HasOne(wol => wol.WorkOrder)
                .WithMany(wo => wo.Labor)
                .HasForeignKey(wol => wol.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(wol => wol.Technician)
                .WithMany()
                .HasForeignKey(wol => wol.TechnicianId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        
        // Configure WorkOrderDocument entity
        builder.Entity<WorkOrderDocument>(entity =>
        {
            entity.HasIndex(wod => wod.WorkOrderId);
            entity.HasIndex(wod => wod.DocumentType);
            entity.HasIndex(wod => wod.UploadedById);
            entity.HasIndex(wod => wod.UploadedAt);
            
            entity.HasOne(wod => wod.WorkOrder)
                .WithMany(wo => wo.Documents)
                .HasForeignKey(wod => wod.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(wod => wod.UploadedBy)
                .WithMany()
                .HasForeignKey(wod => wod.UploadedById)
                .OnDelete(DeleteBehavior.NoAction);
        });
        
        // Configure WorkOrderComment entity
        builder.Entity<WorkOrderComment>(entity =>
        {
            entity.HasIndex(woc => woc.WorkOrderId);
            entity.HasIndex(woc => woc.EmployeeId);
            entity.HasIndex(woc => woc.CreatedAt);
            entity.HasIndex(woc => woc.CommentType);
            
            entity.HasOne(woc => woc.WorkOrder)
                .WithMany(wo => wo.Comments)
                .HasForeignKey(woc => woc.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(woc => woc.User)
                .WithMany()
                .HasForeignKey(woc => woc.EmployeeId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        
        // Configure MaintenanceSchedule entity
        builder.Entity<MaintenanceSchedule>(entity =>
        {
            entity.HasIndex(ms => ms.AssetId);
            entity.HasIndex(ms => ms.MaintenanceTypeId);
            entity.HasIndex(ms => ms.ScheduleType);
            entity.HasIndex(ms => ms.IsActive);
            entity.HasIndex(ms => ms.NextDueDate);
            entity.HasIndex(ms => ms.LastGeneratedDate);
            
            entity.HasOne(ms => ms.Asset)
                .WithMany(a => a.MaintenanceSchedules)
                .HasForeignKey(ms => ms.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(ms => ms.MaintenanceType)
                .WithMany()
                .HasForeignKey(ms => ms.MaintenanceTypeId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasOne(ms => ms.DefaultTechnician)
                .WithMany()
                .HasForeignKey(ms => ms.DefaultTechnicianId)
                .OnDelete(DeleteBehavior.NoAction);
                
            entity.HasOne(ms => ms.DefaultTeam)
                .WithMany()
                .HasForeignKey(ms => ms.DefaultTeamId)
                .OnDelete(DeleteBehavior.NoAction);
                
            entity.HasOne(ms => ms.PriorityLevel)
                .WithMany()
                .HasForeignKey(ms => ms.PriorityLevelId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        
        // Configure InspectionTemplate entity
        builder.Entity<InspectionTemplate>(entity =>
        {
            entity.HasIndex(it => it.Category);
            entity.HasIndex(it => it.InspectionType);
            entity.HasIndex(it => it.IsActive);
        });
        
        // Configure AssetInspection entity
        builder.Entity<AssetInspection>(entity =>
        {
            entity.HasIndex(ai => ai.AssetId);
            entity.HasIndex(ai => ai.InspectionTemplateId);
            entity.HasIndex(ai => ai.InspectorId);
            entity.HasIndex(ai => ai.InspectionDate);
            entity.HasIndex(ai => ai.Status);
            entity.HasIndex(ai => ai.OverallResult);
            entity.HasIndex(ai => ai.NextInspectionDue);
            entity.HasIndex(ai => ai.IsRegulatoryRequired);
            
            entity.HasOne(ai => ai.Asset)
                .WithMany(a => a.Inspections)
                .HasForeignKey(ai => ai.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(ai => ai.InspectionTemplate)
                .WithMany(it => it.Inspections)
                .HasForeignKey(ai => ai.InspectionTemplateId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasOne(ai => ai.Inspector)
                .WithMany()
                .HasForeignKey(ai => ai.InspectorId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        
        // Configure InspectionDocument entity
        builder.Entity<InspectionDocument>(entity =>
        {
            entity.HasIndex(id => id.InspectionId);
            entity.HasIndex(id => id.DocumentType);
            
            entity.HasOne(id => id.Inspection)
                .WithMany(ai => ai.Documents)
                .HasForeignKey(id => id.InspectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // Configure TechnicianTeam entity
        builder.Entity<TechnicianTeam>(entity =>
        {
            entity.HasIndex(tt => tt.TeamLeaderId);
            entity.HasIndex(tt => tt.Status);
            
            entity.HasOne(tt => tt.TeamLeader)
                .WithMany()
                .HasForeignKey(tt => tt.TeamLeaderId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        
        // Configure TechnicianTeamMember entity
        builder.Entity<TechnicianTeamMember>(entity =>
        {
            entity.HasIndex(ttm => ttm.TeamId);
            entity.HasIndex(ttm => ttm.TechnicianId);
            entity.HasIndex(ttm => ttm.IsActive);
            entity.HasIndex(ttm => new { ttm.TeamId, ttm.TechnicianId });
            
            entity.HasOne(ttm => ttm.Team)
                .WithMany(tt => tt.Members)
                .HasForeignKey(ttm => ttm.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(ttm => ttm.Technician)
                .WithMany()
                .HasForeignKey(ttm => ttm.TechnicianId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        
        // Configure TechnicianSkill entity
        builder.Entity<TechnicianSkill>(entity =>
        {
            entity.HasIndex(ts => ts.Category);
            entity.HasIndex(ts => ts.IsActive);
        });
        
        // Configure UserTechnicianSkill entity
        builder.Entity<UserTechnicianSkill>(entity =>
        {
            entity.HasIndex(uts => uts.EmployeeId);
            entity.HasIndex(uts => uts.SkillId);
            entity.HasIndex(uts => uts.ProficiencyLevel);
            entity.HasIndex(uts => uts.CertificationExpiry);
            entity.HasIndex(uts => new { uts.EmployeeId, uts.SkillId }).IsUnique();
            
            entity.HasOne(uts => uts.Employee)
                .WithMany()
                .HasForeignKey(uts => uts.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(uts => uts.Skill)
                .WithMany(ts => ts.UserSkills)
                .HasForeignKey(uts => uts.SkillId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // Configure AssetDowntime entity
        builder.Entity<AssetDowntime>(entity =>
        {
            entity.HasIndex(ad => ad.AssetId);
            entity.HasIndex(ad => ad.WorkOrderId);
            entity.HasIndex(ad => ad.Status);
            entity.HasIndex(ad => ad.StartTime);
            entity.HasIndex(ad => ad.Reason);
            
            entity.HasOne(ad => ad.Asset)
                .WithMany(a => a.Downtimes)
                .HasForeignKey(ad => ad.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(ad => ad.WorkOrder)
                .WithMany()
                .HasForeignKey(ad => ad.WorkOrderId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        
        // Configure AssetType entity
        builder.Entity<AssetType>(entity =>
        {
            entity.HasIndex(at => at.Code).IsUnique();
            entity.HasIndex(at => at.IsActive);
        });
        
        // Configure AssetTypeField entity
        builder.Entity<AssetTypeField>(entity =>
        {
            entity.HasIndex(atf => atf.AssetTypeId);
            entity.HasIndex(atf => atf.FieldName);
            entity.HasIndex(atf => atf.DisplayOrder);
            entity.HasIndex(atf => atf.IsActive);
            
            entity.HasOne(atf => atf.AssetType)
                .WithMany(at => at.CustomFields)
                .HasForeignKey(atf => atf.AssetTypeId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // Configure TechnicianSchedule entity
        builder.Entity<TechnicianSchedule>(entity =>
        {
            entity.HasIndex(ts => ts.TechnicianId);
            entity.HasIndex(ts => ts.WorkOrderId);
            entity.HasIndex(ts => ts.ShiftId);
            entity.HasIndex(ts => ts.StartDate);
            entity.HasIndex(ts => ts.Status);
            entity.HasIndex(ts => ts.ScheduleType);
            
            entity.HasOne(ts => ts.Technician)
                .WithMany()
                .HasForeignKey(ts => ts.TechnicianId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(ts => ts.WorkOrder)
                .WithMany()
                .HasForeignKey(ts => ts.WorkOrderId)
                .OnDelete(DeleteBehavior.NoAction);
                
            entity.HasOne(ts => ts.Shift)
                .WithMany(s => s.Schedules)
                .HasForeignKey(ts => ts.ShiftId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        
        // Configure TechnicianAvailability entity
        builder.Entity<TechnicianAvailability>(entity =>
        {
            entity.HasIndex(ta => ta.TechnicianId);
            entity.HasIndex(ta => ta.StartDate);
            entity.HasIndex(ta => ta.AvailabilityType);
            entity.HasIndex(ta => ta.Reason);
            
            entity.HasOne(ta => ta.Technician)
                .WithMany()
                .HasForeignKey(ta => ta.TechnicianId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // Configure TechnicianShift entity
        builder.Entity<TechnicianShift>(entity =>
        {
            entity.HasIndex(ts => ts.IsActive);
            entity.HasIndex(ts => ts.StartTime);
        });
        
        // Configure new maintenance entities
        
        // Configure TechnicalSkill entity
        builder.Entity<TechnicalSkill>(entity =>
        {
            entity.HasIndex(ts => ts.Code).IsUnique();
            entity.HasIndex(ts => ts.Name);
            entity.HasIndex(ts => ts.Category);
            entity.HasIndex(ts => ts.SkillLevel);
            entity.HasIndex(ts => ts.Complexity);
            entity.HasIndex(ts => ts.RiskLevel);
            entity.HasIndex(ts => ts.IsActive);
            entity.HasIndex(ts => ts.IsFromHRModule);
            entity.HasIndex(ts => ts.LastSyncDate);
        });
        
        // Configure TechnicianSkillAssignment entity
        builder.Entity<TechnicianSkillAssignment>(entity =>
        {
            entity.HasIndex(tsa => tsa.TechnicianId);
            entity.HasIndex(tsa => tsa.SkillId);
            entity.HasIndex(tsa => tsa.ProficiencyLevel);
            entity.HasIndex(tsa => tsa.IsVerified);
            entity.HasIndex(tsa => tsa.ExpirationDate);
            entity.HasIndex(tsa => tsa.LastAssessmentDate);
            entity.HasIndex(tsa => new { tsa.TechnicianId, tsa.SkillId }).IsUnique();
            
            entity.HasOne(tsa => tsa.Technician)
                .WithMany()
                .HasForeignKey(tsa => tsa.TechnicianId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(tsa => tsa.Skill)
                .WithMany(ts => ts.TechnicianAssignments)
                .HasForeignKey(tsa => tsa.SkillId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        
        // Configure SafetyProtocol entity
        builder.Entity<SafetyProtocol>(entity =>
        {
            entity.HasIndex(sp => sp.Code).IsUnique();
            entity.HasIndex(sp => sp.Name);
            entity.HasIndex(sp => sp.Category);
            entity.HasIndex(sp => sp.Severity);
            entity.HasIndex(sp => sp.IsActive);
            entity.HasIndex(sp => sp.IsMandatory);
            entity.HasIndex(sp => sp.EffectiveDate);
            entity.HasIndex(sp => sp.NextReviewDate);
        });
        
        // Configure SafetyComplianceRecord entity
        builder.Entity<SafetyComplianceRecord>(entity =>
        {
            entity.HasIndex(scr => scr.SafetyProtocolId);
            entity.HasIndex(scr => scr.TechnicianId);
            entity.HasIndex(scr => scr.WorkOrderId);
            entity.HasIndex(scr => scr.ComplianceDate);
            entity.HasIndex(scr => scr.ComplianceStatus);
            entity.HasIndex(scr => scr.InspectorId);
            
            entity.HasOne(scr => scr.SafetyProtocol)
                .WithMany()
                .HasForeignKey(scr => scr.SafetyProtocolId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(scr => scr.Technician)
                .WithMany()
                .HasForeignKey(scr => scr.TechnicianId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasOne(scr => scr.WorkOrder)
                .WithMany()
                .HasForeignKey(scr => scr.WorkOrderId)
                .OnDelete(DeleteBehavior.NoAction);
                
            entity.HasOne(scr => scr.Inspector)
                .WithMany()
                .HasForeignKey(scr => scr.InspectorId)
                .OnDelete(DeleteBehavior.NoAction);
                
            entity.HasOne(scr => scr.VerifiedBy)
                .WithMany()
                .HasForeignKey(scr => scr.VerifiedById)
                .OnDelete(DeleteBehavior.NoAction);
        });
        
        // Configure Technician entity (now using Employee entity directly)
        // Technician-specific configurations are handled in the Employee entity configuration
    }
    
    private void ConfigureHREntities(ModelBuilder builder)
    {
        // Configure Employee entity
        builder.Entity<Employee>(entity =>
        {
            entity.HasIndex(e => e.EmployeeNumber).IsUnique();
            entity.HasIndex(e => e.CorporateEmployeeID);
            entity.HasIndex(e => e.EmailAddress).IsUnique();
            entity.HasIndex(e => e.DepartmentId);
            entity.HasIndex(e => e.PositionId);
            entity.HasIndex(e => e.StaffStatus);
            entity.HasIndex(e => e.ManagerId);
            entity.HasIndex(e => e.DateEmployed);
            entity.HasIndex(e => e.IsActive);
            
            // Self-referencing relationship for Manager/DirectReports
            entity.HasOne(e => e.Manager)
                .WithMany(m => m.DirectReports)
                .HasForeignKey(e => e.ManagerId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascading deletes
            
            // Department relationship
            entity.HasOne(e => e.Department)
                .WithMany(d => d.Employees)
                .HasForeignKey(e => e.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // Position relationship
            entity.HasOne(e => e.Position)
                .WithMany(p => p.Employees)
                .HasForeignKey(e => e.PositionId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // Section relationship
            entity.HasOne(e => e.Section)
                .WithMany(s => s.Employees)
                .HasForeignKey(e => e.SectionId)
                .OnDelete(DeleteBehavior.NoAction);
                
            // Country relationship
            entity.HasOne(e => e.Country)
                .WithMany(c => c.Employees)
                .HasForeignKey(e => e.CountryId)
                .OnDelete(DeleteBehavior.NoAction);
                
            // Shift relationship
            entity.HasOne(e => e.Shift)
                .WithMany(s => s.Employees)
                .HasForeignKey(e => e.ShiftId)
                .OnDelete(DeleteBehavior.NoAction);
                
            // Station relationship
            entity.HasOne(e => e.Station)
                .WithMany(ws => ws.Employees)
                .HasForeignKey(e => e.StationId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        
        // Configure Department entity
        builder.Entity<Department>(entity =>
        {
            entity.HasIndex(d => d.Code).IsUnique();
            entity.HasIndex(d => d.Name);
            entity.HasIndex(d => d.DepartmentType);
            entity.HasIndex(d => d.ParentDepartmentId);
            entity.HasIndex(d => d.DepartmentHeadId);
            entity.HasIndex(d => d.IsActive);
            
            // Self-referencing relationship for parent/child departments
            entity.HasOne(d => d.ParentDepartment)
                .WithMany(pd => pd.SubDepartments)
                .HasForeignKey(d => d.ParentDepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // Department head relationship
            entity.HasOne(d => d.DepartmentHead)
                .WithMany()
                .HasForeignKey(d => d.DepartmentHeadId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        
        // Configure Section entity
        builder.Entity<Section>(entity =>
        {
            entity.HasIndex(s => s.Code);
            entity.HasIndex(s => s.DepartmentId);
            entity.HasIndex(s => s.SectionHeadId);
            entity.HasIndex(s => s.IsActive);
            
            // Department relationship
            entity.HasOne(s => s.Department)
                .WithMany(d => d.Sections)
                .HasForeignKey(s => s.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // Section head relationship
            entity.HasOne(s => s.SectionHead)
                .WithMany()
                .HasForeignKey(s => s.SectionHeadId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private void ApplyGlobalFilters(ModelBuilder builder)
    {
        // Apply soft delete filter to all entities that inherit from BaseEntity
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var type = entityType.ClrType;
            if (typeof(BaseEntity).IsAssignableFrom(type))
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(SetSoftDeleteFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(type);
                method.Invoke(null, new object[] { builder, entityType });
            }

            // Apply tenant filter to all entities that inherit from TenantEntity
            if (typeof(TenantEntity).IsAssignableFrom(type) && _tenantId.HasValue)
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(SetTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(type);
                method.Invoke(null, new object[] { builder, entityType, _tenantId.Value });
            }
        }
    }

    private static void SetSoftDeleteFilter<TEntity>(ModelBuilder builder, Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType)
        where TEntity : BaseEntity
    {
        builder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }

    private static void SetTenantFilter<TEntity>(ModelBuilder builder, Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType, Guid tenantId)
        where TEntity : TenantEntity
    {
        builder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == tenantId);
    }

    private void SeedData(ModelBuilder builder)
    {
        // Seed default tenant
        var defaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        builder.Entity<Tenant>().HasData(
            new Tenant
            {
                Id = defaultTenantId,
                Name = "Default Tenant",
                Code = "DEFAULT",
                Description = "Default system tenant",
                Status = Shared.TenantStatus.Active,
                CreatedAt = DateTime.UtcNow
            }
        );

        // Seed default roles
        var superAdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var tenantAdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var managerRoleId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var employeeRoleId = Guid.Parse("00000000-0000-0000-0000-000000000004");

        builder.Entity<ApplicationRole>().HasData(
            new ApplicationRole { Id = superAdminRoleId, Name = Shared.Constants.Roles.SuperAdmin, NormalizedName = Shared.Constants.Roles.SuperAdmin.ToUpper(), IsSystemRole = true },
            new ApplicationRole { Id = tenantAdminRoleId, Name = Shared.Constants.Roles.TenantAdmin, NormalizedName = Shared.Constants.Roles.TenantAdmin.ToUpper(), IsSystemRole = true },
            new ApplicationRole { Id = managerRoleId, Name = Shared.Constants.Roles.Manager, NormalizedName = Shared.Constants.Roles.Manager.ToUpper(), IsSystemRole = true },
            new ApplicationRole { Id = employeeRoleId, Name = Shared.Constants.Roles.Employee, NormalizedName = Shared.Constants.Roles.Employee.ToUpper(), IsSystemRole = true }
        );

        // Seed default modules for default tenant
        var moduleIds = new List<Guid>
        {
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
        };

        var modules = new[]
        {
            Shared.Constants.Modules.Finance,
            Shared.Constants.Modules.HR,
            Shared.Constants.Modules.Sales,
            Shared.Constants.Modules.Procurement,
            Shared.Constants.Modules.Inventory,
            Shared.Constants.Modules.Marketing,
            Shared.Constants.Modules.WorkflowEngine
        };

        for (int i = 0; i < modules.Length; i++)
        {
            builder.Entity<TenantModule>().HasData(
                new TenantModule
                {
                    Id = moduleIds[i],
                    TenantId = defaultTenantId,
                    ModuleName = modules[i],
                    Status = Shared.ModuleStatus.Enabled,
                    EnabledDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                }
            );
        }
        
        // Seed permissions
        SeedPermissions(builder);
    }
    
    private void SeedPermissions(ModelBuilder builder)
    {
        var permissions = new List<Permission>();
        var permissionId = 1;
        
        // User Management permissions
        var userPermissions = new[]
        {
            ("users.read", "View Users", "View user accounts and details"),
            ("users.create", "Create Users", "Create new user accounts"),
            ("users.update", "Update Users", "Edit existing user accounts"),
            ("users.delete", "Delete Users", "Delete user accounts")
        };
        
        foreach (var (name, displayName, description) in userPermissions)
        {
            permissions.Add(new Permission
            {
                Id = Guid.Parse($"00000000-0000-0000-0000-{permissionId:000000000000}"),
                Name = name,
                DisplayName = displayName,
                Description = description,
                Category = "User Management",
                IsSystemPermission = true,
                CreatedAt = DateTime.UtcNow
            });
            permissionId++;
        }
        
        // Role Management permissions
        var rolePermissions = new[]
        {
            ("roles.read", "View Roles", "View role definitions"),
            ("roles.create", "Create Roles", "Create new roles"),
            ("roles.update", "Update Roles", "Edit existing roles"),
            ("roles.delete", "Delete Roles", "Delete roles")
        };
        
        foreach (var (name, displayName, description) in rolePermissions)
        {
            permissions.Add(new Permission
            {
                Id = Guid.Parse($"00000000-0000-0000-0000-{permissionId:000000000000}"),
                Name = name,
                DisplayName = displayName,
                Description = description,
                Category = "Role Management",
                IsSystemPermission = true,
                CreatedAt = DateTime.UtcNow
            });
            permissionId++;
        }
        
        // Dashboard & Reports permissions
        var dashboardPermissions = new[]
        {
            ("dashboard.read", "View Dashboard", "Access main dashboard"),
            ("reports.read", "View Reports", "Access reporting features"),
            ("reports.create", "Create Reports", "Generate custom reports"),
            ("analytics.read", "View Analytics", "Access analytics data")
        };
        
        foreach (var (name, displayName, description) in dashboardPermissions)
        {
            permissions.Add(new Permission
            {
                Id = Guid.Parse($"00000000-0000-0000-0000-{permissionId:000000000000}"),
                Name = name,
                DisplayName = displayName,
                Description = description,
                Category = "Dashboard & Reports",
                IsSystemPermission = true,
                CreatedAt = DateTime.UtcNow
            });
            permissionId++;
        }
        
        // System Administration permissions
        var adminPermissions = new[]
        {
            ("admin.read", "View Admin", "Access admin interface"),
            ("settings.read", "View Settings", "View system settings"),
            ("settings.update", "Update Settings", "Modify system settings"),
            ("audit.read", "View Audit Logs", "Access audit trail")
        };
        
        foreach (var (name, displayName, description) in adminPermissions)
        {
            permissions.Add(new Permission
            {
                Id = Guid.Parse($"00000000-0000-0000-0000-{permissionId:000000000000}"),
                Name = name,
                DisplayName = displayName,
                Description = description,
                Category = "System Administration",
                IsSystemPermission = true,
                CreatedAt = DateTime.UtcNow
            });
            permissionId++;
        }
        
        builder.Entity<Permission>().HasData(permissions.ToArray());
        
        // Seed role-permission relationships
        SeedRolePermissions(builder, permissions);
    }
    
    private void SeedRolePermissions(ModelBuilder builder, List<Permission> permissions)
    {
        var rolePermissions = new List<RolePermission>();
        
        // SuperAdmin gets all permissions
        var superAdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        foreach (var permission in permissions)
        {
            rolePermissions.Add(new RolePermission
            {
                RoleId = superAdminRoleId,
                PermissionId = permission.Id,
                GrantedAt = DateTime.UtcNow,
                GrantedBy = "System"
            });
        }
        
        // TenantAdmin gets most permissions except user management of SuperAdmin
        var tenantAdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var tenantAdminPermissions = permissions.Where(p => 
            p.Category != "System Administration" || 
            (p.Category == "System Administration" && p.Name != "admin.read")).ToList();
            
        foreach (var permission in tenantAdminPermissions)
        {
            rolePermissions.Add(new RolePermission
            {
                RoleId = tenantAdminRoleId,
                PermissionId = permission.Id,
                GrantedAt = DateTime.UtcNow,
                GrantedBy = "System"
            });
        }
        
        // Manager gets read permissions and basic management
        var managerRoleId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var managerPermissions = permissions.Where(p => 
            p.Name.EndsWith(".read") || 
            p.Name == "users.update" || 
            p.Name == "reports.create").ToList();
            
        foreach (var permission in managerPermissions)
        {
            rolePermissions.Add(new RolePermission
            {
                RoleId = managerRoleId,
                PermissionId = permission.Id,
                GrantedAt = DateTime.UtcNow,
                GrantedBy = "System"
            });
        }
        
        // Employee gets basic read permissions
        var employeeRoleId = Guid.Parse("00000000-0000-0000-0000-000000000004");
        var employeePermissions = permissions.Where(p => 
            p.Name == "dashboard.read" || 
            p.Name == "reports.read" || 
            p.Name == "users.read").ToList();
            
        foreach (var permission in employeePermissions)
        {
            rolePermissions.Add(new RolePermission
            {
                RoleId = employeeRoleId,
                PermissionId = permission.Id,
                GrantedAt = DateTime.UtcNow,
                GrantedBy = "System"
            });
        }
        
        builder.Entity<RolePermission>().HasData(rolePermissions.ToArray());
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditableEntities();
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        UpdateAuditableEntities();
        return base.SaveChanges();
    }

    private void UpdateAuditableEntities()
    {
        var entries = ChangeTracker.Entries<BaseEntity>();

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = DateTime.UtcNow;
                    break;
            }
        }
    }
    
    private void ConfigureDecimalPrecision(ModelBuilder builder)
    {
        // Configure decimal precision for all decimal properties to avoid SQL Server warnings
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                {
                    // Use precision 18,4 for most decimal fields, 18,2 for currency
                    if (property.Name.Contains("Cost") || property.Name.Contains("Price") || 
                        property.Name.Contains("Amount") || property.Name.Contains("Total") ||
                        property.Name.Contains("Salary"))
                    {
                        property.SetColumnType("decimal(18,2)");
                    }
                    else
                    {
                        property.SetColumnType("decimal(18,4)");
                    }
                }
            }
        }
    }
    
    private void ConfigureGlobalTenantRelationships(ModelBuilder builder)
    {
        // Configure all TenantEntity relationships to use Restrict instead of Cascade
        // to avoid multiple cascade paths in SQL Server
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(TenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                // Find all foreign keys that reference Tenant
                var tenantForeignKeys = entityType.GetForeignKeys()
                    .Where(fk => fk.PrincipalEntityType.ClrType == typeof(Tenant))
                    .ToList();
                    
                foreach (var fk in tenantForeignKeys)
                {
                    // Update the delete behavior to Restrict
                    fk.DeleteBehavior = DeleteBehavior.Restrict;
                }
                
                // If no foreign key exists, create one
                if (!tenantForeignKeys.Any())
                {
                    var tenantIdProperty = entityType.FindProperty("TenantId");
                    if (tenantIdProperty != null)
                    {
                        builder.Entity(entityType.ClrType)
                            .HasOne(typeof(Tenant))
                            .WithMany()
                            .HasForeignKey("TenantId")
                            .OnDelete(DeleteBehavior.Restrict);
                    }
                }
            }
        }
    }
    
    
    private void ConfigureInventoryEntities(ModelBuilder builder)
    {
        // InventoryItem entity
        builder.Entity<InventoryItem>(entity =>
        {
            entity.HasIndex(i => i.ItemCode).IsUnique();
            entity.HasIndex(i => i.CategoryId);
            entity.HasIndex(i => i.Status);
            entity.HasIndex(i => i.ABCClass);
            entity.HasIndex(i => i.IsSerialTracked);
            entity.HasIndex(i => i.IsLotTracked);
            
            entity.HasOne(i => i.Category)
                .WithMany(c => c.Items)
                .HasForeignKey(i => i.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        
        // InventoryCategory entity
        builder.Entity<InventoryCategory>(entity =>
        {
            entity.HasIndex(c => c.Code);
            entity.HasIndex(c => c.ParentCategoryId);
            entity.HasIndex(c => c.IsActive);
            
            entity.HasOne(c => c.ParentCategory)
                .WithMany(pc => pc.SubCategories)
                .HasForeignKey(c => c.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        
        // Warehouse entity
        builder.Entity<Warehouse>(entity =>
        {
            entity.HasIndex(w => w.Code).IsUnique();
            entity.HasIndex(w => w.IsActive);
            entity.HasIndex(w => w.WarehouseType);
        });
        
        // WarehouseLocation entity
        builder.Entity<WarehouseLocation>(entity =>
        {
            entity.HasIndex(wl => wl.LocationCode);
            entity.HasIndex(wl => wl.WarehouseId);
            entity.HasIndex(wl => wl.ParentLocationId);
            entity.HasIndex(wl => wl.IsActive);
            
            // Warehouse relationship with Restrict to avoid cascade conflicts
            entity.HasOne(wl => wl.Warehouse)
                .WithMany(w => w.Locations)
                .HasForeignKey(wl => wl.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // Self-referencing relationship for parent/child locations
            entity.HasOne(wl => wl.ParentLocation)
                .WithMany(pl => pl.ChildLocations)
                .HasForeignKey(wl => wl.ParentLocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        
        // StockMovement entity
        builder.Entity<StockMovement>(entity =>
        {
            entity.HasIndex(sm => sm.InventoryItemId);
            entity.HasIndex(sm => sm.LocationId);
            entity.HasIndex(sm => sm.MovementType);
            entity.HasIndex(sm => sm.MovementDate);
            entity.HasIndex(sm => sm.ReferenceType);
            entity.HasIndex(sm => sm.ReferenceNumber);
            
            entity.HasOne(sm => sm.InventoryItem)
                .WithMany(ii => ii.StockMovements)
                .HasForeignKey(sm => sm.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasOne(sm => sm.Location)
                .WithMany()
                .HasForeignKey(sm => sm.LocationId)
                .OnDelete(DeleteBehavior.SetNull);
                
            entity.HasOne(sm => sm.ProcessedBy)
                .WithMany()
                .HasForeignKey(sm => sm.ProcessedById)
                .OnDelete(DeleteBehavior.NoAction);
                
            entity.HasOne(sm => sm.ApprovedBy)
                .WithMany()
                .HasForeignKey(sm => sm.ApprovedById)
                .OnDelete(DeleteBehavior.NoAction);
        });
        
        // PurchaseOrder entity
        builder.Entity<PurchaseOrder>(entity =>
        {
            entity.HasIndex(po => po.OrderNumber).IsUnique();
            entity.HasIndex(po => po.Status);
            entity.HasIndex(po => po.OrderDate);
            entity.HasIndex(po => po.RequestedById);
            
            entity.HasOne(po => po.RequestedBy)
                .WithMany()
                .HasForeignKey(po => po.RequestedById)
                .OnDelete(DeleteBehavior.NoAction);
        });
        
        // PurchaseOrderItem entity
        builder.Entity<PurchaseOrderItem>(entity =>
        {
            entity.HasIndex(poi => poi.PurchaseOrderId);
            entity.HasIndex(poi => poi.InventoryItemId);
            
            entity.HasOne(poi => poi.PurchaseOrder)
                .WithMany(po => po.Items)
                .HasForeignKey(poi => poi.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Note: InventoryItem navigation is not configured due to cross-module dependencies
            // entity.HasOne(poi => poi.InventoryItem)
            //     .WithMany(ii => ii.PurchaseOrderItems)
            //     .HasForeignKey(poi => poi.InventoryItemId)
            //     .OnDelete(DeleteBehavior.Restrict);
        });
        
        // PurchaseOrderReceipt entity
        builder.Entity<PurchaseOrderReceipt>(entity =>
        {
            entity.HasIndex(por => por.PurchaseOrderId);
            entity.HasIndex(por => por.ReceiptNumber).IsUnique();
            entity.HasIndex(por => por.ReceiptDate);
            entity.HasIndex(por => por.Status);
            entity.HasIndex(por => por.ReceivedById);
            entity.HasIndex(por => por.InspectedById);
            
            entity.HasOne(por => por.PurchaseOrder)
                .WithMany(po => po.Receipts)
                .HasForeignKey(por => por.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Restrict); // Changed from Cascade to avoid conflicts
                
            entity.HasOne(por => por.ReceivedBy)
                .WithMany()
                .HasForeignKey(por => por.ReceivedById)
                .OnDelete(DeleteBehavior.NoAction);
                
            entity.HasOne(por => por.InspectedBy)
                .WithMany()
                .HasForeignKey(por => por.InspectedById)
                .OnDelete(DeleteBehavior.NoAction);
        });
        
        // PurchaseOrderReceiptItem entity
        builder.Entity<PurchaseOrderReceiptItem>(entity =>
        {
            entity.HasIndex(pori => pori.ReceiptId);
            entity.HasIndex(pori => pori.PurchaseOrderItemId);
            entity.HasIndex(pori => pori.LocationId);
            
            entity.HasOne(pori => pori.Receipt)
                .WithMany(por => por.Items)
                .HasForeignKey(pori => pori.ReceiptId)
                .OnDelete(DeleteBehavior.Restrict); // Changed from Cascade to avoid conflicts
                
            entity.HasOne(pori => pori.PurchaseOrderItem)
                .WithMany()
                .HasForeignKey(pori => pori.PurchaseOrderItemId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // Note: Location navigation is not configured due to cross-module dependencies
            // entity.HasOne(pori => pori.Location)
            //     .WithMany()
            //     .HasForeignKey(pori => pori.LocationId)
            //     .OnDelete(DeleteBehavior.SetNull);
        });
        
        // StockAdjustment entity
        builder.Entity<StockAdjustment>(entity =>
        {
            entity.HasIndex(sa => sa.AdjustmentNumber).IsUnique();
            entity.HasIndex(sa => sa.AdjustmentDate);
            entity.HasIndex(sa => sa.Status);
            entity.HasIndex(sa => sa.ReasonCode);
            entity.HasIndex(sa => sa.ApprovedById);
            
            entity.HasOne(sa => sa.ApprovedBy)
                .WithMany()
                .HasForeignKey(sa => sa.ApprovedById)
                .OnDelete(DeleteBehavior.NoAction);
        });
        
        // StockAdjustmentItem entity
        builder.Entity<StockAdjustmentItem>(entity =>
        {
            entity.HasIndex(sai => sai.AdjustmentId);
            entity.HasIndex(sai => sai.InventoryItemId);
            entity.HasIndex(sai => sai.LocationId);
            
            entity.HasOne(sai => sai.Adjustment)
                .WithMany(sa => sa.Items)
                .HasForeignKey(sai => sai.AdjustmentId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(sai => sai.InventoryItem)
                .WithMany()
                .HasForeignKey(sai => sai.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasOne(sai => sai.Location)
                .WithMany()
                .HasForeignKey(sai => sai.LocationId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}