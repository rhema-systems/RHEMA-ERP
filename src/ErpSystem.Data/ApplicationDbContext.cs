using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Pricing;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Data.Configuration;
using ErpSystem.Data.Configuration.Maintenance;
using ErpSystem.Data.Configuration.Pricing;
using ErpSystem.Data.Configuration.Procurement;

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

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IServiceProvider serviceProvider) : base(options)
    {
        // Try to get tenant ID from ICurrentUserProvider
        var currentUserProvider = serviceProvider?.GetService(typeof(ErpSystem.Core.Interfaces.ICurrentUserProvider))
            as ErpSystem.Core.Interfaces.ICurrentUserProvider;

        if (currentUserProvider != null)
        {
            try
            {
                var tid = currentUserProvider.TenantId;
                if (tid != Guid.Empty)
                {
                    _tenantId = tid;
                }
            }
            catch
            {
                // Ignore errors during tenant resolution
            }
        }
    }

    protected ApplicationDbContext(DbContextOptions options, IServiceProvider serviceProvider) : base(options)
    {
        // Try to get tenant ID from ICurrentUserProvider
        var currentUserProvider = serviceProvider?.GetService(typeof(ErpSystem.Core.Interfaces.ICurrentUserProvider))
            as ErpSystem.Core.Interfaces.ICurrentUserProvider;

        if (currentUserProvider != null)
        {
            try
            {
                var tid = currentUserProvider.TenantId;
                if (tid != Guid.Empty)
                {
                    _tenantId = tid;
                }
            }
            catch
            {
                // Ignore errors during tenant resolution
            }
        }
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
    public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

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

    // Task Template entities
    public DbSet<AssetTaskTemplate> AssetTaskTemplates { get; set; }
    public DbSet<AssetTypeTaskTemplate> AssetTypeTaskTemplates { get; set; }
    public DbSet<MaintenanceTaskTemplate> MaintenanceTaskTemplates { get; set; }

    public DbSet<MaintenanceSchedule> MaintenanceSchedules { get; set; }
    public DbSet<MaintenanceScheduleHistory> MaintenanceScheduleHistories { get; set; }
    public DbSet<MaintenanceScheduleNotificationHistory> MaintenanceScheduleNotificationHistories { get; set; }
    public DbSet<AssetUsageTracking> AssetUsageTrackings { get; set; }
    public DbSet<InspectionTemplate> InspectionTemplates { get; set; }
    public DbSet<AssetInspection> AssetInspections { get; set; }
    public DbSet<InspectionDocument> InspectionDocuments { get; set; }
    public DbSet<TechnicianTeam> TechnicianTeams { get; set; }
    public DbSet<TechnicianTeamMember> TechnicianTeamMembers { get; set; }
    public DbSet<TechnicianSkill> TechnicianSkills { get; set; }
    public DbSet<UserTechnicianSkill> UserTechnicianSkills { get; set; }
    public DbSet<AssetDowntime> AssetDowntimes { get; set; }
    public DbSet<MaintenanceAttachment> MaintenanceAttachments { get; set; }

    public DbSet<AssetAdmission> AssetAdmissions { get; set; }
    public DbSet<AssetDischarge> AssetDischarges { get; set; }
    public DbSet<AssetMaintenanceDowntime> AssetMaintenanceDowntimes { get; set; }

    // Fixed Assets entities
    public DbSet<FixedAssetCategory> FixedAssetCategories { get; set; }
    public DbSet<FixedAsset> FixedAssets { get; set; }
    public DbSet<AssetDepreciationSchedule> AssetDepreciationSchedules { get; set; }
    public DbSet<AssetTransaction> AssetTransactions { get; set; }
    public DbSet<AssetDisposal> AssetDisposals { get; set; }
    public DbSet<AssetTransfer> AssetTransfers { get; set; }
    public DbSet<AssetVerificationSession> AssetVerificationSessions { get; set; }
    public DbSet<AssetVerificationItem> AssetVerificationItems { get; set; }

    // Quality Control entities
    public DbSet<QualityControlChecklist> QualityControlChecklists { get; set; }
    public DbSet<WorkOrderQualityCheck> WorkOrderQualityChecks { get; set; }
    public DbSet<WorkOrderQualitySignOff> WorkOrderQualitySignOffs { get; set; }
    public DbSet<QualitySignOffChecklist> QualitySignOffChecklists { get; set; }
    public DbSet<WorkOrderRework> WorkOrderReworks { get; set; }
    public DbSet<WorkOrderReworkTask> WorkOrderReworkTasks { get; set; }
    public DbSet<InspectionApproval> InspectionApprovals { get; set; }
    public DbSet<WorkOrderRejection> WorkOrderRejections { get; set; }
    public DbSet<RejectionFollowUp> RejectionFollowUps { get; set; }
    public DbSet<InspectionChecklistTemplate> InspectionChecklistTemplates { get; set; }
    public DbSet<InspectionChecklistItem> InspectionChecklistItems { get; set; }
    public DbSet<QualityMetrics> QualityMetrics { get; set; }

    // Technician Scheduling entities
    public DbSet<TechnicianSchedule> TechnicianSchedules { get; set; }
    public DbSet<TechnicianAvailability> TechnicianAvailabilities { get; set; }
    public DbSet<TechnicianShift> TechnicianShifts { get; set; }

    // Job Card Management entities
    public DbSet<JobCard> JobCards { get; set; }
    public DbSet<JobCardComment> JobCardComments { get; set; }
    public DbSet<JobCardDocument> JobCardDocuments { get; set; }
    public DbSet<JobCardApprovalStep> JobCardApprovalSteps { get; set; }
    public DbSet<JobCardCertificate> JobCardCertificates { get; set; }

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

    // Tool Management entities
    public DbSet<MaintenanceTool> MaintenanceTools { get; set; }
    public DbSet<ToolCheckout> ToolCheckouts { get; set; }
    public DbSet<WorkOrderTool> WorkOrderTools { get; set; }

    // Resource Management entities
    public DbSet<MaintenanceStaffSchedule> MaintenanceStaffSchedules { get; set; }
    public DbSet<MaintenanceExpense> MaintenanceExpenses { get; set; }

    // Notification entities
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<MaintenanceNotification> MaintenanceNotifications { get; set; }
    public DbSet<MaintenanceNotificationTemplate> MaintenanceNotificationTemplates { get; set; }
    public DbSet<MaintenanceEscalationRule> MaintenanceEscalationRules { get; set; }
    public DbSet<NotificationTopic> NotificationTopics { get; set; }
    public DbSet<NotificationTopicRecipient> NotificationTopicRecipients { get; set; }

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
    public DbSet<Skill> Skills { get; set; }
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
    public DbSet<WarehouseQuantity> WarehouseQuantities { get; set; }
    public DbSet<InventoryAllocation> InventoryAllocations { get; set; }
    public DbSet<InventoryBalance> InventoryBalances { get; set; }
    public DbSet<InventoryLayer> InventoryLayers { get; set; }
    public DbSet<InventoryMovement> InventoryMovements { get; set; }


    // Procurement entities
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<SupplierContact> SupplierContacts { get; set; }
    public DbSet<SupplierItemCatalog> SupplierItemCatalogs { get; set; }
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
    public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }
    public DbSet<PurchaseOrderReceipt> PurchaseOrderReceipts { get; set; }
    public DbSet<PurchaseOrderReceiptItem> PurchaseOrderReceiptItems { get; set; }
    public DbSet<PurchaseRequisition> PurchaseRequisitions { get; set; }

    // Tender entities
    public DbSet<Tender> Tenders { get; set; }
    public DbSet<TenderItem> TenderItems { get; set; }
    public DbSet<TenderLot> TenderLots { get; set; }
    public DbSet<TenderBid> TenderBids { get; set; }
    public DbSet<TenderAward> TenderAwards { get; set; }
    public DbSet<TenderViewLog> TenderViewLogs { get; set; }
    public DbSet<TenderFee> TenderFees { get; set; }
    public DbSet<TenderPayment> TenderPayments { get; set; }

    // Email Campaign entities
    public DbSet<EmailCampaign> EmailCampaigns { get; set; }
    public DbSet<EmailCampaignRecipient> EmailCampaignRecipients { get; set; }

    // System Logs
    public DbSet<SystemExceptionLog> SystemExceptionLogs { get; set; }

    // Inventory Transfer
    public DbSet<InventoryTransfer> InventoryTransfers { get; set; }


    // Business Partner Management (Unified Supplier/Contractor)
    public DbSet<BusinessPartner> BusinessPartners { get; set; }
    public DbSet<PartnerCategory> PartnerCategories { get; set; }
    public DbSet<BusinessPartnerCategory> BusinessPartnerCategories { get; set; }
    public DbSet<ContractorSpecialization> ContractorSpecializations { get; set; }
    public DbSet<BusinessPartnerSpecialization> BusinessPartnerSpecializations { get; set; }
    public DbSet<LicenseType> LicenseTypes { get; set; }
    public DbSet<BusinessPartnerLicense> BusinessPartnerLicenses { get; set; }
    public DbSet<BusinessPartnerContact> BusinessPartnerContacts { get; set; }
    public DbSet<BusinessPartnerDocument> BusinessPartnerDocuments { get; set; }
    public DbSet<BusinessPartnerFinancial> BusinessPartnerFinancials { get; set; }
    public DbSet<BusinessPartnerRegistration> BusinessPartnerRegistrations { get; set; }
    public DbSet<BusinessPartnerRegistrationDocument> BusinessPartnerRegistrationDocuments { get; set; }
    public DbSet<BusinessPartnerRegistrationStatusHistory> BusinessPartnerRegistrationStatusHistories { get; set; }
    public DbSet<PurchaseRequisitionItem> PurchaseRequisitionItems { get; set; }

    // RFQ Management (separate from formal tenders)
    public DbSet<RequestForQuotation> RequestForQuotations { get; set; }
    public DbSet<RequestForQuotationItem> RequestForQuotationItems { get; set; }
    public DbSet<RequestForQuotationInvitation> RequestForQuotationInvitations { get; set; }
    public DbSet<RequestForQuotationQuote> RequestForQuotationQuotes { get; set; }
    public DbSet<RequestForQuotationQuoteItem> RequestForQuotationQuoteItems { get; set; }
    public DbSet<RequestForQuotationAwardLine> RequestForQuotationAwardLines { get; set; }

    // Business Partner User Management
    public DbSet<BusinessPartnerUser> BusinessPartnerUsers { get; set; }
    public DbSet<TenderAssignment> TenderAssignments { get; set; }

    // Award Verification
    public DbSet<AwardVerificationChecklistTemplate> AwardVerificationChecklistTemplates { get; set; }
    public DbSet<AwardVerificationChecklistItem> AwardVerificationChecklistItems { get; set; }
    public DbSet<TenderAwardVerification> TenderAwardVerifications { get; set; }
    public DbSet<TenderAwardVerificationBidder> TenderAwardVerificationBidders { get; set; }
    public DbSet<TenderAwardVerificationItemResult> TenderAwardVerificationItemResults { get; set; }
    public DbSet<TenderAwardVerificationItemDocument> TenderAwardVerificationItemDocuments { get; set; }

    // Performance Bonds
    public DbSet<PerformanceBondRequest> PerformanceBondRequests { get; set; }

    // Tender Negotiations
    public DbSet<TenderNegotiation> TenderNegotiations { get; set; }
    public DbSet<TenderNegotiationItem> TenderNegotiationItems { get; set; }

    // Contract Management
    public DbSet<Contract> Contracts { get; set; }
    public DbSet<ContractMilestone> ContractMilestones { get; set; }
    public DbSet<ContractAmendment> ContractAmendments { get; set; }
    public DbSet<ContractDocument> ContractDocuments { get; set; }

    // Procurement Planning
    public DbSet<ProcurementPlan> ProcurementPlans { get; set; }
    public DbSet<ProcurementPlanItem> ProcurementPlanItems { get; set; }
    public DbSet<ProcurementPlanItemSupplier> ProcurementPlanItemSuppliers { get; set; }
    public DbSet<ProcurementBudget> ProcurementBudgets { get; set; }
    public DbSet<ProcurementBudgetAllocation> ProcurementBudgetAllocations { get; set; }
    public DbSet<ProcurementBudgetRevision> ProcurementBudgetRevisions { get; set; }
    public DbSet<ProcurementSchedule> ProcurementSchedules { get; set; }
    public DbSet<MarketAnalysis> MarketAnalyses { get; set; }
    public DbSet<PriceHistory> PriceHistories { get; set; }
    public DbSet<SupplierConsolidation> SupplierConsolidations { get; set; }
    public DbSet<EmergencyProcurementPlan> EmergencyProcurementPlans { get; set; }
    public DbSet<EmergencyProcurementItem> EmergencyProcurementItems { get; set; }
    public DbSet<EmergencySupplier> EmergencySuppliers { get; set; }

    // Procurement Settings
    public DbSet<ProcurementSettings> ProcurementSettings { get; set; }

    // Maintenance Settings
    public DbSet<MaintenanceSettings> MaintenanceSettings { get; set; }

    // Distributed locks (global, non-tenant scoped)
    public DbSet<DistributedLock> DistributedLocks { get; set; }

    // Fleet Management (Maintenance)
    public DbSet<FleetTrip> FleetTrips { get; set; }
    public DbSet<FleetComplianceItem> FleetComplianceItems { get; set; }
    public DbSet<FleetFuelTransaction> FleetFuelTransactions { get; set; }
    public DbSet<FleetVehicleAssignment> FleetVehicleAssignments { get; set; }
    public DbSet<FleetTripInspection> FleetTripInspections { get; set; }
    public DbSet<FleetDefect> FleetDefects { get; set; }
    public DbSet<FleetIncident> FleetIncidents { get; set; }
    public DbSet<FleetTyre> FleetTyres { get; set; }
    public DbSet<FleetTyreEvent> FleetTyreEvents { get; set; }
    public DbSet<FleetBattery> FleetBatteries { get; set; }
    public DbSet<FleetBatteryEvent> FleetBatteryEvents { get; set; }
    public DbSet<FleetExternalRepair> FleetExternalRepairs { get; set; }
    public DbSet<FleetCostEntry> FleetCostEntries { get; set; }

    // Performance Tracking
    public DbSet<SupplierPerformanceMetric> SupplierPerformanceMetrics { get; set; }
    public DbSet<QualityIncident> QualityIncidents { get; set; }
    public DbSet<PerformanceReview> PerformanceReviews { get; set; }

    // Blacklist Appeals
    public DbSet<BlacklistAppeal> BlacklistAppeals { get; set; }
    public DbSet<BlacklistHistory> BlacklistHistories { get; set; }

    // Enquiry, Helpdesk & Complaints (EHC)
    public DbSet<EhcTicket> EhcTickets { get; set; }
    public DbSet<EhcTicketCategory> EhcTicketCategories { get; set; }
    public DbSet<EhcTicketMessage> EhcTicketMessages { get; set; }
    public DbSet<EhcTicketAttachment> EhcTicketAttachments { get; set; }
    public DbSet<EhcTicketStatusHistory> EhcTicketStatusHistories { get; set; }
    public DbSet<EhcSlaTemplate> EhcSlaTemplates { get; set; }
    public DbSet<EhcWorkflowRoutingRule> EhcWorkflowRoutingRules { get; set; }
    public DbSet<EhcTicketAuditEvent> EhcTicketAuditEvents { get; set; }
    // Workflow Engine entities
    public DbSet<WorkflowDefinition> WorkflowDefinitions { get; set; }
    public DbSet<WorkflowStep> WorkflowSteps { get; set; }
    public DbSet<WorkflowTransition> WorkflowTransitions { get; set; }
    public DbSet<WorkflowInstance> WorkflowInstances { get; set; }
    public DbSet<WorkflowStepInstance> WorkflowStepInstances { get; set; }
    public DbSet<WorkflowActivityLog> WorkflowActivityLogs { get; set; }
    public DbSet<WorkflowApproval> WorkflowApprovals { get; set; }
    public DbSet<WorkflowEntityType> WorkflowEntityTypes { get; set; }

    //Finance - GL Entities
    public DbSet<Account> Accounts { get; set; }
    public DbSet<AccountSegmentValue> AccountSegmentValues { get; set; }
    public DbSet<AccountSegmentStructure> AccountSegmentStructures { get; set; }
    public DbSet<SegmentLookupValue> SegmentLookupValues { get; set; }
    public DbSet<JournalEntry> JournalEntries { get; set; }
    public DbSet<FiscalYear> FiscalYears { get; set; }
    public DbSet<FiscalPeriod> FiscalPeriods { get; set; }
    public DbSet<ModuleDefinition> ModuleDefinitions { get; set; }
    public DbSet<PeriodModuleLock> PeriodModuleLocks { get; set; }
    public DbSet<TransactionDocumentModuleMapping> TransactionDocumentModuleMappings { get; set; }
    public DbSet<ExchangeRate> ExchangeRates { get; set; }
    public DbSet<Currency> Currencies { get; set; }
    public DbSet<AccountBalance> AccountBalances { get; set; }
    public DbSet<BudgetScenario> BudgetScenarios { get; set; }
    public DbSet<BudgetReturn> BudgetReturns { get; set; }
    public DbSet<BudgetEntry> BudgetEntries { get; set; }

    // Tax entities
    public DbSet<Tax> Taxes { get; set; }
    public DbSet<TaxGroup> TaxGroups { get; set; }
    public DbSet<TaxGroupComponent> TaxGroupComponents { get; set; }
    public DbSet<TaxRateHistory> TaxRateHistory { get; set; }
    public DbSet<TaxThreshold> TaxThresholds { get; set; }
    public DbSet<TaxCalculation> TaxCalculations { get; set; }
    public DbSet<AccountCurrencyLink> AccountCurrencyLinks { get; set; }
    public DbSet<AccountTransaction> AccountTransactions { get; set; }
    public DbSet<FinanceSettings> FinanceSettings { get; set; }
    public DbSet<BankAccount> BankAccounts { get; set; }
    public DbSet<CashTransaction> CashTransactions { get; set; }

    // AR entities
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<InvoiceLineItem> InvoiceLineItems { get; set; }
    public DbSet<CustomerPayment> CustomerPayments { get; set; }
    public DbSet<PaymentAllocation> PaymentAllocations { get; set; }
    public DbSet<Payment> Payments { get; set; }

    // AP entities
    public DbSet<VendorInvoice> VendorInvoices { get; set; }
    public DbSet<VendorInvoiceLineItem> VendorInvoiceLineItems { get; set; }
    public DbSet<VendorPayment> VendorPayments { get; set; }
    public DbSet<VendorPaymentAllocation> VendorPaymentAllocations { get; set; }
    public DbSet<PaymentBatch> PaymentBatches { get; set; }
    public DbSet<PaymentBatchItem> PaymentBatchItems { get; set; }

    // Tax entities
    public DbSet<TaxType> TaxTypes { get; set; }
    public DbSet<TaxRate> TaxRates { get; set; }
    public DbSet<TaxRule> TaxRules { get; set; }



    // Unit Accounts entities
    public DbSet<UnitType> UnitTypes { get; set; }
    public DbSet<UnitAccount> UnitAccounts { get; set; }
    public DbSet<UnitJournalEntry> UnitJournalEntries { get; set; }
    public DbSet<UnitJournalEntryLine> UnitJournalEntryLines { get; set; }
    public DbSet<UnitAccountBalance> UnitAccountBalances { get; set; }
    public DbSet<RatioDefinition> RatioDefinitions { get; set; }
    public DbSet<UnitAccountBudget> UnitAccountBudgets { get; set; }
    public DbSet<AllocationRule> AllocationRules { get; set; }
    public DbSet<AllocationTarget> AllocationTargets { get; set; }


    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply entity configurations
        builder.ApplyConfiguration(new ApplicationUserConfiguration());
        builder.ApplyConfiguration(new TenantConfiguration());
        builder.ApplyConfiguration(new UserTenantConfiguration());
        builder.ApplyConfiguration(new AssetTypeConfiguration());
        builder.ApplyConfiguration(new AssetTypeFieldConfiguration());

        // Apply Unit Accounts configurations
        builder.ApplyConfiguration(new UnitTypeConfiguration());
        builder.ApplyConfiguration(new UnitAccountConfiguration());
        builder.ApplyConfiguration(new UnitJournalEntryConfiguration());
        builder.ApplyConfiguration(new UnitJournalEntryLineConfiguration());
        builder.ApplyConfiguration(new UnitAccountBalanceConfiguration());
        builder.ApplyConfiguration(new RatioDefinitionConfiguration());
        builder.ApplyConfiguration(new UnitAccountBudgetConfiguration());
        builder.ApplyConfiguration(new AllocationRuleConfiguration());
        builder.ApplyConfiguration(new AllocationTargetConfiguration());

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

        // Configure Enquiry, Helpdesk & Complaints (EHC) entities
        ConfigureEhcEntities(builder);

        // Configure Business Partner entities
        ConfigureBusinessPartnerEntities(builder);

        // Configure Finance entities
        ConfigureFinanceEntities(builder);
        ConfigureTaxEntities(builder);

        // Configure TaxRule entity
        builder.Entity<TaxRule>(entity =>
        {
            entity.HasOne(tr => tr.TaxGroup).WithMany().HasForeignKey(tr => tr.TaxGroupId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(tr => tr.TenantId);
            entity.HasIndex(tr => tr.ProductCategoryId);
            entity.HasIndex(tr => tr.IsActive);
            entity.HasIndex(tr => tr.Priority);
        });

        // Configure Fixed Assets entities
        ConfigureFixedAssetsEntities(builder);

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
                .WithMany(e => e.AssignedWorkOrders)
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

            entity.HasOne(wo => wo.Supervisor)
                .WithMany()
                .HasForeignKey(wo => wo.SupervisorId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(wo => wo.CompletedBy)
                .WithMany()
                .HasForeignKey(wo => wo.CompletedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(wo => wo.QualityCheckedBy)
                .WithMany()
                .HasForeignKey(wo => wo.QualityCheckedById)
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
            // TechnicianId now references ApplicationUser (Users table) instead of Employee
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
            entity.HasIndex(at => new { at.TenantId, at.Code }).IsUnique();
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

            // TechnicianId now references ApplicationUser (Users table) instead of Employee
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

            // TechnicianId now references ApplicationUser (Users table) instead of Employee
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

        // Configure JobCard entity
        builder.Entity<JobCard>(entity =>
        {
            entity.ToTable("JobCard"); // Explicitly set table name to singular
            entity.HasIndex(jc => jc.JobCardNumber).IsUnique();
            entity.HasIndex(jc => jc.AssetId);
            entity.HasIndex(jc => jc.MaintenanceTypeId);
            entity.HasIndex(jc => jc.PriorityLevelId);
            entity.HasIndex(jc => jc.RequestedById);
            entity.HasIndex(jc => jc.JobCardStatus);
            entity.HasIndex(jc => jc.ApprovalStatus);
            entity.HasIndex(jc => jc.RequestedDate);
            entity.HasIndex(jc => jc.RequiredCompletionDate);

            entity.HasOne(jc => jc.Asset)
                .WithMany()
                .HasForeignKey(jc => jc.AssetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(jc => jc.MaintenanceType)
                .WithMany(mt => mt.JobCards)
                .HasForeignKey(jc => jc.MaintenanceTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(jc => jc.PriorityLevel)
                .WithMany()
                .HasForeignKey(jc => jc.PriorityLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(jc => jc.RequestedBy)
                .WithMany()
                .HasForeignKey(jc => jc.RequestedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(jc => jc.PreferredTechnician)
                .WithMany()
                .HasForeignKey(jc => jc.PreferredTechnicianId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(jc => jc.AssignedTechnician)
                .WithMany()
                .HasForeignKey(jc => jc.AssignedTechnicianId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(jc => jc.GeneratedWorkOrder)
                .WithMany()
                .HasForeignKey(jc => jc.GeneratedWorkOrderId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure JobCardComment entity
        builder.Entity<JobCardComment>(entity =>
        {
            entity.ToTable("JobCardComment"); // Explicitly set table name to singular
            entity.HasIndex(jcc => jcc.JobCardId);
            entity.HasIndex(jcc => jcc.CommentById);
            entity.HasIndex(jcc => jcc.CommentDate);
            entity.HasIndex(jcc => jcc.CommentType);

            entity.HasOne(jcc => jcc.JobCard)
                .WithMany(jc => jc.Comments)
                .HasForeignKey(jcc => jcc.JobCardId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(jcc => jcc.CommentBy)
                .WithMany()
                .HasForeignKey(jcc => jcc.CommentById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure JobCardDocument entity
        builder.Entity<JobCardDocument>(entity =>
        {
            entity.ToTable("JobCardDocument"); // Explicitly set table name to singular
            entity.HasIndex(jcd => jcd.JobCardId);
            entity.HasIndex(jcd => jcd.DocumentType);
            entity.HasIndex(jcd => jcd.UploadedById);
            entity.HasIndex(jcd => jcd.UploadedDate);

            entity.HasOne(jcd => jcd.JobCard)
                .WithMany(jc => jc.Documents)
                .HasForeignKey(jcd => jcd.JobCardId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(jcd => jcd.UploadedBy)
                .WithMany()
                .HasForeignKey(jcd => jcd.UploadedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure JobCardApprovalStep entity
        builder.Entity<JobCardApprovalStep>(entity =>
        {
            entity.ToTable("JobCardApprovalStep"); // Explicitly set table name to singular
            entity.HasIndex(jcas => jcas.JobCardId);
            entity.HasIndex(jcas => jcas.ApproverId);
            entity.HasIndex(jcas => jcas.StepOrder);
            entity.HasIndex(jcas => jcas.Status);

            entity.HasOne(jcas => jcas.JobCard)
                .WithMany(jc => jc.ApprovalSteps)
                .HasForeignKey(jcas => jcas.JobCardId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(jcas => jcas.Approver)
                .WithMany()
                .HasForeignKey(jcas => jcas.ApproverId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure Quality Control entities
        ConfigureQualityControlEntities(builder);

        // Configure Finance entities (Budgeting)
        ConfigureBudgetEntities(builder);

        // Configure Technician entity (now using Employee entity directly)
        // Technician-specific configurations are handled in the Employee entity configuration
    }

    private void ConfigureBudgetEntities(ModelBuilder builder)
    {
        // BudgetScenario
        builder.Entity<BudgetScenario>(entity =>
        {
            entity.HasOne(e => e.FiscalYear)
                .WithMany()
                .HasForeignKey(e => e.FiscalYearId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting FY if it has budgets
        });

        // BudgetReturn
        builder.Entity<BudgetReturn>(entity =>
        {
             entity.HasOne(e => e.BudgetScenario)
                .WithMany(s => s.BudgetReturns)
                .HasForeignKey(e => e.BudgetScenarioId)
                .OnDelete(DeleteBehavior.Cascade); // Deleting Scenario deletes Returns

             entity.HasOne(e => e.SegmentValue)
                .WithMany()
                .HasForeignKey(e => e.SegmentValueId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // BudgetEntry
        builder.Entity<BudgetEntry>(entity =>
        {
            entity.HasOne(e => e.BudgetReturn)
                .WithMany(r => r.BudgetEntries)
                .HasForeignKey(e => e.BudgetReturnId)
                .OnDelete(DeleteBehavior.Cascade); // Deleting Return deletes Entries

            entity.HasOne(e => e.Account)
                .WithMany()
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cycle/multiple cascade paths

            entity.HasOne(e => e.FiscalPeriod)
                .WithMany()
                .HasForeignKey(e => e.FiscalPeriodId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cycle/multiple cascade paths
        });
    }

    private void ConfigureQualityControlEntities(ModelBuilder builder)
    {
        // Configure QualityControlChecklist entity
        builder.Entity<QualityControlChecklist>(entity =>
        {
            entity.HasIndex(qcc => qcc.Name);
            entity.HasIndex(qcc => qcc.WorkOrderType);
            entity.HasIndex(qcc => qcc.AssetCategory);
            entity.HasIndex(qcc => qcc.MaintenanceType);
            entity.HasIndex(qcc => qcc.IsActive);
            entity.HasIndex(qcc => qcc.IsMandatory);
            entity.HasIndex(qcc => qcc.Version);

            entity.HasMany(qcc => qcc.QualityChecks)
                .WithOne(wqc => wqc.Checklist)
                .HasForeignKey(wqc => wqc.ChecklistId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure WorkOrderQualityCheck entity
        builder.Entity<WorkOrderQualityCheck>(entity =>
        {
            entity.HasIndex(wqc => wqc.WorkOrderId);
            entity.HasIndex(wqc => wqc.ChecklistId);
            entity.HasIndex(wqc => wqc.InspectorId);
            entity.HasIndex(wqc => wqc.InspectionDate);
            entity.HasIndex(wqc => wqc.OverallResult);
            entity.HasIndex(wqc => wqc.Score);
            entity.HasIndex(wqc => wqc.RequiresFollowUp);

            entity.HasMany(wqc => wqc.RelatedRework)
                .WithOne()
                .HasForeignKey("WorkOrderQualityCheckId")
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure WorkOrderQualitySignOff entity
        builder.Entity<WorkOrderQualitySignOff>(entity =>
        {
            entity.HasIndex(wqso => wqso.WorkOrderId);
            entity.HasIndex(wqso => wqso.SignOffLevel);
            entity.HasIndex(wqso => wqso.SignOffRole);
            entity.HasIndex(wqso => wqso.SignOffById);
            entity.HasIndex(wqso => wqso.Status);
            entity.HasIndex(wqso => wqso.SignOffDate);
            entity.HasIndex(wqso => wqso.IsRequired);
            entity.HasIndex(wqso => wqso.SortOrder);

            entity.HasOne(wqso => wqso.WorkOrder)
                .WithMany()
                .HasForeignKey(wqso => wqso.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(wqso => wqso.SignOffBy)
                .WithMany()
                .HasForeignKey(wqso => wqso.SignOffById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(wqso => wqso.DelegatedTo)
                .WithMany()
                .HasForeignKey(wqso => wqso.DelegatedToId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(wqso => wqso.ChecklistItems)
                .WithOne(qscl => qscl.SignOff)
                .HasForeignKey(qscl => qscl.SignOffId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure QualitySignOffChecklist entity
        builder.Entity<QualitySignOffChecklist>(entity =>
        {
            entity.HasIndex(qscl => qscl.SignOffId);
            entity.HasIndex(qscl => qscl.Category);
            entity.HasIndex(qscl => qscl.CheckType);
            entity.HasIndex(qscl => qscl.IsRequired);
            entity.HasIndex(qscl => qscl.SortOrder);
            entity.HasIndex(qscl => qscl.CheckedDate);
            entity.HasIndex(qscl => qscl.CheckedById);

            entity.HasOne(qscl => qscl.CheckedBy)
                .WithMany()
                .HasForeignKey(qscl => qscl.CheckedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure WorkOrderRework entity
        builder.Entity<WorkOrderRework>(entity =>
        {
            entity.HasIndex(wor => wor.WorkOrderId);
            entity.HasIndex(wor => wor.InspectorId);
            entity.HasIndex(wor => wor.ReworkReason);
            entity.HasIndex(wor => wor.Severity);
            entity.HasIndex(wor => wor.Status);
            entity.HasIndex(wor => wor.AssignedTechnicianId);
            entity.HasIndex(wor => wor.IdentifiedDate);
            entity.HasIndex(wor => wor.TargetCompletionDate);

            entity.HasOne(wor => wor.WorkOrder)
                .WithMany()
                .HasForeignKey(wor => wor.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(wor => wor.ReworkTasks)
                .WithOne(wort => wort.WorkOrderRework)
                .HasForeignKey(wort => wort.WorkOrderReworkId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure WorkOrderReworkTask entity
        builder.Entity<WorkOrderReworkTask>(entity =>
        {
            entity.HasIndex(wort => wort.WorkOrderReworkId);
            entity.HasIndex(wort => wort.Sequence);
            entity.HasIndex(wort => wort.Status);
            entity.HasIndex(wort => wort.CompletedDate);
            entity.HasIndex(wort => wort.CompletedById);
        });

        // Configure InspectionApproval entity
        builder.Entity<InspectionApproval>(entity =>
        {
            entity.HasIndex(ia => ia.InspectionId);
            entity.HasIndex(ia => ia.ApprovalLevel);
            entity.HasIndex(ia => ia.ApproverId);
            entity.HasIndex(ia => ia.Status);
            entity.HasIndex(ia => ia.RequestedDate);
            entity.HasIndex(ia => ia.DueDate);
            entity.HasIndex(ia => ia.ApprovedDate);
            entity.HasIndex(ia => ia.Priority);
            entity.HasIndex(ia => ia.DelegatedToId);

            entity.HasOne(ia => ia.Inspection)
                .WithMany()
                .HasForeignKey(ia => ia.InspectionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ia => ia.Approver)
                .WithMany()
                .HasForeignKey(ia => ia.ApproverId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(ia => ia.DelegatedTo)
                .WithMany()
                .HasForeignKey(ia => ia.DelegatedToId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure AssetAdmission entity
        builder.Entity<AssetAdmission>(entity =>
        {
            entity.HasIndex(a => a.AdmissionNumber).IsUnique();
            entity.HasIndex(a => a.AssetId);
            entity.HasIndex(a => a.Status);
            entity.HasIndex(a => a.AdmissionDate);
            entity.HasIndex(a => a.AdmissionType);

            entity.HasOne(a => a.Asset)
                .WithMany()
                .HasForeignKey(a => a.AssetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.WorkOrder)
                .WithMany()
                .HasForeignKey(a => a.WorkOrderId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(a => a.AdmittedBy)
                .WithMany()
                .HasForeignKey(a => a.AdmittedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure AssetDischarge entity
        builder.Entity<AssetDischarge>(entity =>
        {
            entity.HasIndex(d => d.DischargeNumber).IsUnique();
            entity.HasIndex(d => d.AdmissionId);
            entity.HasIndex(d => d.AssetId);
            entity.HasIndex(d => d.DischargeDate);
            entity.HasIndex(d => d.CustomerAcceptance);

            entity.HasOne(d => d.Admission)
                .WithOne(a => a.Discharge)
                .HasForeignKey<AssetDischarge>(d => d.AdmissionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Asset)
                .WithMany()
                .HasForeignKey(d => d.AssetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.WorkOrder)
                .WithMany()
                .HasForeignKey(d => d.WorkOrderId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(d => d.DischargedBy)
                .WithMany()
                .HasForeignKey(d => d.DischargedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(d => d.QualityCheckedBy)
                .WithMany()
                .HasForeignKey(d => d.QualityCheckedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(d => d.AcceptedBy)
                .WithMany()
                .HasForeignKey(d => d.AcceptedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure AssetMaintenanceDowntime entity
        builder.Entity<AssetMaintenanceDowntime>(entity =>
        {
            entity.HasIndex(d => d.AssetId);
            entity.HasIndex(d => d.AdmissionId);
            entity.HasIndex(d => d.DischargeId);
            entity.HasIndex(d => d.WorkOrderId);
            entity.HasIndex(d => d.DowntimeStart);
            entity.HasIndex(d => d.Status);

            entity.HasOne(d => d.Asset)
                .WithMany()
                .HasForeignKey(d => d.AssetId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(d => d.Admission)
                .WithMany()
                .HasForeignKey(d => d.AdmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Discharge)
                .WithMany()
                .HasForeignKey(d => d.DischargeId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(d => d.WorkOrder)
                .WithMany()
                .HasForeignKey(d => d.WorkOrderId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure MaintenanceCertificate entity
        builder.Entity<MaintenanceCertificate>(entity =>
        {
            entity.HasIndex(c => c.CertificateNumber).IsUnique();
            entity.HasIndex(c => c.DischargeId);
            entity.HasIndex(c => c.AssetId);
            entity.HasIndex(c => c.IssuedDate);

            entity.HasOne(c => c.Discharge)
                .WithMany()
                .HasForeignKey(c => c.DischargeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(c => c.Asset)
                .WithMany()
                .HasForeignKey(c => c.AssetId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(c => c.IssuedBy)
                .WithMany()
                .HasForeignKey(c => c.IssuedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure WorkOrderRejection entity
        builder.Entity<WorkOrderRejection>(entity =>
        {
            entity.HasIndex(wor => wor.WorkOrderId);
            entity.HasIndex(wor => wor.RejectedById);
            entity.HasIndex(wor => wor.RejectedDate);
            entity.HasIndex(wor => wor.RejectionType);
            entity.HasIndex(wor => wor.Severity);
            entity.HasIndex(wor => wor.Status);
            entity.HasIndex(wor => wor.ReworkAssignedToId);
            entity.HasIndex(wor => wor.RequiresReinspection);
            entity.HasIndex(wor => wor.IsEscalated);

            entity.HasOne(wor => wor.WorkOrder)
                .WithMany()
                .HasForeignKey(wor => wor.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(wor => wor.RejectedBy)
                .WithMany()
                .HasForeignKey(wor => wor.RejectedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(wor => wor.ReworkAssignedTo)
                .WithMany()
                .HasForeignKey(wor => wor.ReworkAssignedToId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(wor => wor.ResolvedBy)
                .WithMany()
                .HasForeignKey(wor => wor.ResolvedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(wor => wor.ReinspectedBy)
                .WithMany()
                .HasForeignKey(wor => wor.ReinspectedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(wor => wor.EscalatedTo)
                .WithMany()
                .HasForeignKey(wor => wor.EscalatedToId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(wor => wor.FollowUps)
                .WithOne(rfu => rfu.Rejection)
                .HasForeignKey(rfu => rfu.RejectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure RejectionFollowUp entity
        builder.Entity<RejectionFollowUp>(entity =>
        {
            entity.HasIndex(rfu => rfu.RejectionId);
            entity.HasIndex(rfu => rfu.AssignedToId);
            entity.HasIndex(rfu => rfu.DueDate);
            entity.HasIndex(rfu => rfu.Status);
            entity.HasIndex(rfu => rfu.CompletedDate);
            entity.HasIndex(rfu => rfu.CompletedById);
            entity.HasIndex(rfu => rfu.Priority);

            entity.HasOne(rfu => rfu.AssignedTo)
                .WithMany()
                .HasForeignKey(rfu => rfu.AssignedToId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(rfu => rfu.CompletedBy)
                .WithMany()
                .HasForeignKey(rfu => rfu.CompletedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure InspectionChecklistTemplate entity
        builder.Entity<InspectionChecklistTemplate>(entity =>
        {
            entity.HasIndex(ict => ict.Name);
            entity.HasIndex(ict => ict.Category);
            entity.HasIndex(ict => ict.IsActive);
            entity.HasIndex(ict => ict.IsDefault);
            entity.HasIndex(ict => ict.SortOrder);
            entity.HasIndex(ict => ict.Version);

            entity.HasMany(ict => ict.ChecklistItems)
                .WithOne(ici => ici.Template)
                .HasForeignKey(ici => ici.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure InspectionChecklistItem entity
        builder.Entity<InspectionChecklistItem>(entity =>
        {
            entity.HasIndex(ici => ici.TemplateId);
            entity.HasIndex(ici => ici.Category);
            entity.HasIndex(ici => ici.ItemType);
            entity.HasIndex(ici => ici.IsRequired);
            entity.HasIndex(ici => ici.IsCritical);
            entity.HasIndex(ici => ici.SortOrder);
            entity.HasIndex(ici => ici.RequiresPhoto);
        });

        // Configure QualityMetrics entity
        builder.Entity<QualityMetrics>(entity =>
        {
            entity.HasIndex(qm => qm.MetricsDate);
            entity.HasIndex(qm => qm.TechnicianId);
            entity.HasIndex(qm => qm.TeamId);
            entity.HasIndex(qm => qm.AssetCategory);
            entity.HasIndex(qm => qm.CalculatedDate);
        });
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

    private void ConfigureBusinessPartnerEntities(ModelBuilder builder)
    {
        // Configure BusinessPartner entity
        builder.Entity<BusinessPartner>(entity =>
        {
            entity.HasIndex(bp => bp.PartnerCode).IsUnique();
            entity.HasIndex(bp => bp.PartnerName);
            entity.HasIndex(bp => bp.PartnerType);
            entity.HasIndex(bp => bp.RegistrationStatus);
            entity.HasIndex(bp => bp.IsActive);
            entity.HasIndex(bp => bp.IsBlacklisted);
            entity.HasIndex(bp => bp.IsPreferred);
            entity.HasIndex(bp => bp.PrimaryEmail);

            entity.HasOne(bp => bp.ApprovedBy)
                .WithMany()
                .HasForeignKey(bp => bp.ApprovedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure PartnerCategory entity
        builder.Entity<PartnerCategory>(entity =>
        {
            entity.HasIndex(pc => pc.CategoryCode).IsUnique();
            entity.HasIndex(pc => pc.CategoryName);
            entity.HasIndex(pc => pc.CategoryType);
            entity.HasIndex(pc => pc.IsActive);

            // Self-referencing relationship for hierarchical categories
            entity.HasOne(pc => pc.ParentCategory)
                .WithMany(p => p.SubCategories)
                .HasForeignKey(pc => pc.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure BusinessPartnerCategory junction entity
        builder.Entity<BusinessPartnerCategory>(entity =>
        {
            entity.HasKey(bpc => bpc.Id);
            entity.HasIndex(bpc => new { bpc.BusinessPartnerId, bpc.CategoryId }).IsUnique();

            entity.HasOne(bpc => bpc.BusinessPartner)
                .WithMany(bp => bp.Categories)
                .HasForeignKey(bpc => bpc.BusinessPartnerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(bpc => bpc.Category)
                .WithMany(c => c.BusinessPartnerCategories)
                .HasForeignKey(bpc => bpc.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure ContractorSpecialization entity
        builder.Entity<ContractorSpecialization>(entity =>
        {
            entity.HasIndex(cs => cs.SpecializationCode).IsUnique();
            entity.HasIndex(cs => cs.SpecializationName);
            entity.HasIndex(cs => cs.IsActive);
        });

        // Configure BusinessPartnerSpecialization junction entity
        builder.Entity<BusinessPartnerSpecialization>(entity =>
        {
            entity.HasKey(bps => bps.Id);
            entity.HasIndex(bps => new { bps.BusinessPartnerId, bps.SpecializationId }).IsUnique();

            entity.HasOne(bps => bps.BusinessPartner)
                .WithMany(bp => bp.Specializations)
                .HasForeignKey(bps => bps.BusinessPartnerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(bps => bps.Specialization)
                .WithMany(s => s.BusinessPartnerSpecializations)
                .HasForeignKey(bps => bps.SpecializationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure LicenseType entity
        builder.Entity<LicenseType>(entity =>
        {
            entity.HasIndex(lt => lt.LicenseCode).IsUnique();
            entity.HasIndex(lt => lt.LicenseName);
            entity.HasIndex(lt => lt.IsActive);
        });

        // Configure BusinessPartnerLicense entity
        builder.Entity<BusinessPartnerLicense>(entity =>
        {
            entity.HasIndex(bpl => bpl.LicenseNumber);
            entity.HasIndex(bpl => bpl.BusinessPartnerId);
            entity.HasIndex(bpl => bpl.ExpiryDate);
            entity.HasIndex(bpl => bpl.Status);

            entity.HasOne(bpl => bpl.BusinessPartner)
                .WithMany(bp => bp.Licenses)
                .HasForeignKey(bpl => bpl.BusinessPartnerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(bpl => bpl.LicenseType)
                .WithMany(lt => lt.BusinessPartnerLicenses)
                .HasForeignKey(bpl => bpl.LicenseTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure BusinessPartnerContact entity
        builder.Entity<BusinessPartnerContact>(entity =>
        {
            entity.HasIndex(bpc => bpc.BusinessPartnerId);
            entity.HasIndex(bpc => bpc.Email);

            entity.HasOne(bpc => bpc.BusinessPartner)
                .WithMany(bp => bp.Contacts)
                .HasForeignKey(bpc => bpc.BusinessPartnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure BusinessPartnerDocument entity
        builder.Entity<BusinessPartnerDocument>(entity =>
        {
            entity.HasIndex(bpd => bpd.BusinessPartnerId);
            entity.HasIndex(bpd => bpd.DocumentType);
            entity.HasIndex(bpd => bpd.IsVerified);
            entity.HasIndex(bpd => bpd.ExpiryDate);

            entity.HasOne(bpd => bpd.BusinessPartner)
                .WithMany(bp => bp.Documents)
                .HasForeignKey(bpd => bpd.BusinessPartnerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(bpd => bpd.VerifiedBy)
                .WithMany()
                .HasForeignKey(bpd => bpd.VerifiedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(bpd => bpd.UploadedBy)
                .WithMany()
                .HasForeignKey(bpd => bpd.UploadedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure BusinessPartnerFinancial entity
        builder.Entity<BusinessPartnerFinancial>(entity =>
        {
            entity.HasIndex(bpf => bpf.BusinessPartnerId);
            entity.HasIndex(bpf => bpf.FiscalYear);
            entity.HasIndex(bpf => new { bpf.BusinessPartnerId, bpf.FiscalYear }).IsUnique();

            entity.HasOne(bpf => bpf.BusinessPartner)
                .WithMany(bp => bp.Financials)
                .HasForeignKey(bpf => bpf.BusinessPartnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure BusinessPartnerRegistration entity
        builder.Entity<BusinessPartnerRegistration>(entity =>
        {
            entity.HasIndex(bpr => bpr.RegistrationNumber).IsUnique();
            entity.HasIndex(bpr => bpr.ApplicantEmail);
            entity.HasIndex(bpr => bpr.Status);
            entity.HasIndex(bpr => bpr.SubmittedDate);

            entity.HasOne(bpr => bpr.ReviewedBy)
                .WithMany()
                .HasForeignKey(bpr => bpr.ReviewedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(bpr => bpr.ApprovedBy)
                .WithMany()
                .HasForeignKey(bpr => bpr.ApprovedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(bpr => bpr.BusinessPartner)
                .WithMany()
                .HasForeignKey(bpr => bpr.BusinessPartnerId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure BusinessPartnerRegistrationDocument entity
        builder.Entity<BusinessPartnerRegistrationDocument>(entity =>
        {
            entity.HasIndex(bprd => bprd.RegistrationId);
            entity.HasIndex(bprd => bprd.DocumentType);
            entity.HasIndex(bprd => bprd.IsVerified);

            entity.HasOne(bprd => bprd.Registration)
                .WithMany(r => r.Documents)
                .HasForeignKey(bprd => bprd.RegistrationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(bprd => bprd.VerifiedBy)
                .WithMany()
                .HasForeignKey(bprd => bprd.VerifiedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure BusinessPartnerRegistrationStatusHistory entity
        builder.Entity<BusinessPartnerRegistrationStatusHistory>(entity =>
        {
            entity.HasKey(bprsh => bprsh.Id);
            entity.HasIndex(bprsh => bprsh.RegistrationId);
            entity.HasIndex(bprsh => bprsh.ChangedAt);

            entity.HasOne(bprsh => bprsh.Registration)
                .WithMany(r => r.StatusHistory)
                .HasForeignKey(bprsh => bprsh.RegistrationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(bprsh => bprsh.ChangedBy)
                .WithMany()
                .HasForeignKey(bprsh => bprsh.ChangedById)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    protected virtual void ApplyGlobalFilters(ModelBuilder builder)
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
            if (typeof(TenantEntity).IsAssignableFrom(type))
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(SetTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(type);
                method.Invoke(this, new object[] { builder, entityType });
            }
        }
    }

    private static void ConfigureEhcEntities(ModelBuilder builder)
    {
        builder.Entity<EhcTicketCategory>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Name });

            entity.HasOne(x => x.ParentCategory)
                .WithMany(x => x.Subcategories)
                .HasForeignKey(x => x.ParentCategoryId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<EhcSlaTemplate>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.IsActive });
            entity.HasIndex(x => new { x.TenantId, x.TicketType, x.Priority });
        });

        builder.Entity<EhcWorkflowRoutingRule>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.IsActive });
            entity.HasIndex(x => new { x.TenantId, x.Priority });
            entity.HasIndex(x => new { x.TenantId, x.WorkflowName });

            entity.HasOne(x => x.Category)
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Subcategory)
                .WithMany()
                .HasForeignKey(x => x.SubcategoryId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.AssignedDepartment)
                .WithMany()
                .HasForeignKey(x => x.AssignedDepartmentId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<EhcTicket>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.TicketNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.HasIndex(x => new { x.TenantId, x.RequesterUserId });
            entity.HasIndex(x => new { x.TenantId, x.AssignedToUserId });
            entity.HasIndex(x => new { x.TenantId, x.AssignedDepartmentId });

            entity.HasOne(x => x.Category)
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Subcategory)
                .WithMany()
                .HasForeignKey(x => x.SubcategoryId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.AssignedDepartment)
                .WithMany()
                .HasForeignKey(x => x.AssignedDepartmentId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(x => x.Messages)
                .WithOne(m => m.Ticket)
                .HasForeignKey(m => m.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Attachments)
                .WithOne(a => a.Ticket)
                .HasForeignKey(a => a.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.StatusHistory)
                .WithOne(h => h.Ticket)
                .HasForeignKey(h => h.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.AuditEvents)
                .WithOne(a => a.Ticket)
                .HasForeignKey(a => a.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EhcTicketMessage>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.TicketId, x.IsInternal });
        });

        builder.Entity<EhcTicketAttachment>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.TicketId });
            entity.HasIndex(x => new { x.TenantId, x.MessageId });
        });

        builder.Entity<EhcTicketStatusHistory>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.TicketId });
        });

        builder.Entity<EhcTicketAuditEvent>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.TicketId });
            entity.HasIndex(x => new { x.TenantId, x.EventType });
            entity.HasIndex(x => new { x.TenantId, x.IsInternal });
        });
    }

    private static void SetSoftDeleteFilter<TEntity>(ModelBuilder builder, Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType)
        where TEntity : BaseEntity
    {
        builder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }

    private void SetTenantFilter<TEntity>(ModelBuilder builder, Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType)
        where TEntity : TenantEntity
    {
        // Use a simpler expression that EF Core can translate reliably
        // This avoids the closure issue by using the field directly in a way EF understands
        builder.Entity<TEntity>().HasQueryFilter(e => _tenantId == null || e.TenantId == _tenantId);
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
                CreatedAt = new DateTime(2025, 11, 27, 0, 0, 0, DateTimeKind.Utc)
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
        // Use deterministic GUIDs so migrations are stable across scaffolds
        var moduleIds = new List<Guid>
        {
            Guid.Parse("00000000-0000-0000-0002-000000000001"), // Finance
            Guid.Parse("00000000-0000-0000-0002-000000000002"), // HR
            Guid.Parse("00000000-0000-0000-0002-000000000003"), // Sales
            Guid.Parse("00000000-0000-0000-0002-000000000004"), // Procurement
            Guid.Parse("00000000-0000-0000-0002-000000000005"), // Inventory
            Guid.Parse("00000000-0000-0000-0002-000000000006"), // Marketing
            Guid.Parse("00000000-0000-0000-0002-000000000007")  // WorkflowEngine
        };

        var seedDate = new DateTime(2025, 11, 27, 0, 0, 0, DateTimeKind.Utc);

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
                    EnabledDate = seedDate,
                    CreatedAt = seedDate
                }
            );
        }

        // Seed common Unit Types
        builder.Entity<UnitType>().HasData(
            new UnitType
            {
                Id = Guid.Parse("00000000-0000-0000-1001-000000000001"),
                TenantId = defaultTenantId,
                Code = "EMP",
                Name = "Employees",
                Description = "Headcount/Full-Time Equivalents (FTE)",
                DecimalPlaces = 0,
                IsActive = true,
                CreatedAt = new DateTime(2025, 11, 27, 0, 0, 0, DateTimeKind.Utc)
            },
            new UnitType
            {
                Id = Guid.Parse("00000000-0000-0000-1001-000000000002"),
                TenantId = defaultTenantId,
                Code = "SQFT",
                Name = "Square Feet",
                Description = "Floor space area measurement",
                DecimalPlaces = 2,
                IsActive = true,
                CreatedAt = new DateTime(2025, 11, 27, 0, 0, 0, DateTimeKind.Utc)
            },
            new UnitType
            {
                Id = Guid.Parse("00000000-0000-0000-1001-000000000003"),
                TenantId = defaultTenantId,
                Code = "HRS",
                Name = "Hours",
                Description = "Time measurement in hours",
                DecimalPlaces = 2,
                IsActive = true,
                CreatedAt = new DateTime(2025, 11, 27, 0, 0, 0, DateTimeKind.Utc)
            },
            new UnitType
            {
                Id = Guid.Parse("00000000-0000-0000-1001-000000000004"),
                TenantId = defaultTenantId,
                Code = "UNITS",
                Name = "Units",
                Description = "Generic unit count (production, sales, etc.)",
                DecimalPlaces = 0,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new UnitType
            {
                Id = Guid.Parse("00000000-0000-0000-1001-000000000005"),
                TenantId = defaultTenantId,
                Code = "PCT",
                Name = "Percentage",
                Description = "Percentage values (0-100)",
                DecimalPlaces = 2,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new UnitType
            {
                Id = Guid.Parse("00000000-0000-0000-1001-000000000006"),
                TenantId = defaultTenantId,
                Code = "KWH",
                Name = "Kilowatt Hours",
                Description = "Energy consumption measurement",
                DecimalPlaces = 2,
                IsActive = true,
                CreatedAt = new DateTime(2025, 11, 27, 0, 0, 0, DateTimeKind.Utc)
            }
        );

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
                    // Soft delete: convert to modified and set IsDeleted flag
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
        // UnitOfMeasure and Conversions
        builder.Entity<UnitOfMeasureConversion>(entity =>
        {
            entity.HasOne(c => c.FromUnit)
                .WithMany(u => u.ConversionsFrom)
                .HasForeignKey(c => c.FromUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.ToUnit)
                .WithMany(u => u.ConversionsTo)
                .HasForeignKey(c => c.ToUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });

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

    private void SeedMaintenanceData(ModelBuilder builder, Guid tenantId)
    {
        var now = DateTime.UtcNow;
        var baseDate = new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        // Seed Employees (Maintenance Team)
        var maintenanceManagerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var technicianId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var qualityCheckerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var supervisorId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        // Note: Employees would typically be seeded in HR module, but adding minimal data here for maintenance workflow

        // Seed Asset Categories
        var vehicleCategoryId = Guid.Parse("aaaa1111-1111-1111-1111-111111111111");
        var equipmentCategoryId = Guid.Parse("aaaa2222-2222-2222-2222-222222222222");

        builder.Entity<MaintenanceAssetCategory>().HasData(
            new MaintenanceAssetCategory
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
            },
            new MaintenanceAssetCategory
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
            }
        );

        // Seed Maintenance Assets
        var forkliftId = Guid.Parse("bbbb1111-1111-1111-1111-111111111111");
        var truckId = Guid.Parse("bbbb2222-2222-2222-2222-222222222222");
        var generatorId = Guid.Parse("bbbb3333-3333-3333-3333-333333333333");

        builder.Entity<MaintenanceAsset>().HasData(
            new MaintenanceAsset
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
            },
            new MaintenanceAsset
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
            },
            new MaintenanceAsset
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
            }
        );

        // Seed Maintenance Types
        var preventiveId = Guid.Parse("cccc1111-1111-1111-1111-111111111111");
        var correctiveId = Guid.Parse("cccc2222-2222-2222-2222-222222222222");
        var inspectionId = Guid.Parse("cccc3333-3333-3333-3333-333333333333");

        builder.Entity<MaintenanceType>().HasData(
            new MaintenanceType
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
            },
            new MaintenanceType
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
            },
            new MaintenanceType
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
            }
        );

        // Seed Priority Levels
        var criticalPriorityId = Guid.Parse("dddd1111-1111-1111-1111-111111111111");
        var highPriorityId = Guid.Parse("dddd2222-2222-2222-2222-222222222222");
        var mediumPriorityId = Guid.Parse("dddd3333-3333-3333-3333-333333333333");

        builder.Entity<PriorityLevel>().HasData(
            new PriorityLevel
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
            },
            new PriorityLevel
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
            },
            new PriorityLevel
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
            }
        );

        // Seed Job Cards with Complete Workflow

        // Job Card 1: Completed workflow with certificate (CLOSED)
        var jobCard1Id = Guid.Parse("eeee1111-1111-1111-1111-111111111111");
        builder.Entity<JobCard>().HasData(
            new JobCard
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
                // Admission details
                AssetConditionOnAdmission = "Good",
                MileageReadingOnAdmission = 5420.5m,
                HoursReadingOnAdmission = 1250.0m,
                FuelLevelOnAdmission = 45.0m,
                AdmissionNotes = "Asset received in good working condition. Minor oil leak noted.",
                BayOrStation = "Bay 3",
                // Completion details
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
                // Quality Check
                QualityCheckPassed = true,
                QualityCheckedById = qualityCheckerId,
                QualityCheckDate = baseDate.AddDays(-6),
                QualityCheckNotes = "All work meets quality standards. Asset tested and performing excellently. No defects found.",
                // Acceptance
                CustomerAcceptance = true,
                AcceptedById = maintenanceManagerId,
                AcceptedDate = baseDate.AddDays(-4),
                AcceptanceNotes = "Asset accepted. Forklift is running smoothly. Very satisfied with the maintenance work.",
                // Certificate
                CertificateGenerated = true,
                CertificateGeneratedDate = baseDate.AddDays(-3),
                TenantId = tenantId,
                CreatedAt = baseDate.AddDays(-30),
                CreatedById = maintenanceManagerId,
                CreatedBy = "System"
            }
        );

        // Job Card 2: Accepted but no certificate yet
        var jobCard2Id = Guid.Parse("eeee2222-2222-2222-2222-222222222222");
        builder.Entity<JobCard>().HasData(
            new JobCard
            {
                Id = jobCard2Id,
                JobCardNumber = "JC-2025-0002",
                AssetId = truckId,
                MaintenanceTypeId = correctiveId,
                PriorityLevelId = criticalPriorityId,
                Title = "Brake System Repair - Truck TRK-001",
                Description = "Emergency brake system repair",
                ProblemDescription = "Brake pedal feels spongy. Possible air in brake lines or worn brake pads.",
                MaintenanceLocation = "Internal",
                RequestedById = maintenanceManagerId,
                RequestedDate = baseDate.AddDays(-20),
                RequiredCompletionDate = baseDate.AddDays(-18),
                EstimatedHours = 6.0,
                EstimatedCost = 1200.00m,
                JobCardStatus = "Accepted",
                ApprovalStatus = "Approved",
                ApprovedById = supervisorId,
                ApprovedDate = baseDate.AddDays(-19),
                // Admission
                AssetConditionOnAdmission = "Fair",
                MileageReadingOnAdmission = 85420.0m,
                FuelLevelOnAdmission = 60.0m,
                AdmissionNotes = "Truck admitted with brake system issues. Safety concern - high priority.",
                BayOrStation = "Bay 1",
                // Completion
                CompletedDate = baseDate.AddDays(-12),
                CompletionNotes = "Brake system completely overhauled. All brake pads replaced, brake fluid flushed and replaced, brake lines inspected.",
                AssetConditionOnCompletion = "Good",
                MileageReadingOnCompletion = 85425.0m,
                FuelLevelOnCompletion = 55.0m,
                WorkCompletedSummary = "Front and rear brake pads replaced, brake rotors resurfaced, brake fluid completely flushed, brake lines pressure tested, handbrake adjusted.",
                WarrantyDays = 180,
                WarrantyTerms = "180-day warranty on brake system parts and labor",
                WarrantyExpiration = baseDate.AddDays(-12).AddDays(180),
                // Quality Check
                QualityCheckPassed = true,
                QualityCheckedById = qualityCheckerId,
                QualityCheckDate = baseDate.AddDays(-10),
                QualityCheckNotes = "Brake system tested thoroughly. Stopping distance improved significantly. All safety checks passed.",
                // Acceptance
                CustomerAcceptance = true,
                AcceptedById = maintenanceManagerId,
                AcceptedDate = baseDate.AddDays(-8),
                AcceptanceNotes = "Brakes are working perfectly now. Test drive completed successfully.",
                TenantId = tenantId,
                CreatedAt = baseDate.AddDays(-20),
                CreatedById = maintenanceManagerId,
                CreatedBy = "System"
            }
        );

        // Job Card 3: Quality checked, awaiting acceptance
        var jobCard3Id = Guid.Parse("eeee3333-3333-3333-3333-333333333333");
        builder.Entity<JobCard>().HasData(
            new JobCard
            {
                Id = jobCard3Id,
                JobCardNumber = "JC-2025-0003",
                AssetId = generatorId,
                MaintenanceTypeId = inspectionId,
                PriorityLevelId = mediumPriorityId,
                Title = "Annual Safety Inspection - Generator GEN-001",
                Description = "Mandatory annual safety inspection",
                ProblemDescription = "Annual safety inspection due for compliance with safety regulations.",
                MaintenanceLocation = "Internal",
                RequestedById = maintenanceManagerId,
                RequestedDate = baseDate.AddDays(-15),
                RequiredCompletionDate = baseDate.AddDays(-5),
                EstimatedHours = 3.0,
                EstimatedCost = 350.00m,
                JobCardStatus = "Quality Checked",
                ApprovalStatus = "Approved",
                ApprovedById = supervisorId,
                ApprovedDate = baseDate.AddDays(-14),
                // Admission
                AssetConditionOnAdmission = "Good",
                HoursReadingOnAdmission = 4580.0m,
                FuelLevelOnAdmission = 75.0m,
                AdmissionNotes = "Generator admitted for annual safety inspection. Last inspection was 12 months ago.",
                BayOrStation = "Power House",
                // Completion
                CompletedDate = baseDate.AddDays(-7),
                CompletionNotes = "Comprehensive safety inspection completed. All systems checked and tested.",
                AssetConditionOnCompletion = "Good",
                HoursReadingOnCompletion = 4585.0m,
                FuelLevelOnCompletion = 70.0m,
                WorkCompletedSummary = "Electrical system tested, cooling system inspected, fuel system checked, emissions tested, safety features verified, load testing completed.",
                // Quality Check
                QualityCheckPassed = true,
                QualityCheckedById = qualityCheckerId,
                QualityCheckDate = baseDate.AddDays(-5),
                QualityCheckNotes = "Generator passed all safety inspection criteria. Ready for certification.",
                TenantId = tenantId,
                CreatedAt = baseDate.AddDays(-15),
                CreatedById = maintenanceManagerId,
                CreatedBy = "System"
            }
        );

        // Job Card 4: Just completed, awaiting quality check
        var jobCard4Id = Guid.Parse("eeee4444-4444-4444-4444-444444444444");
        builder.Entity<JobCard>().HasData(
            new JobCard
            {
                Id = jobCard4Id,
                JobCardNumber = "JC-2025-0004",
                AssetId = forkliftId,
                MaintenanceTypeId = correctiveId,
                PriorityLevelId = highPriorityId,
                Title = "Hydraulic System Repair - Forklift FL-001",
                Description = "Hydraulic cylinder leak repair",
                ProblemDescription = "Hydraulic fluid leaking from lift cylinder. Reduced lifting capacity observed.",
                MaintenanceLocation = "Internal",
                RequestedById = maintenanceManagerId,
                RequestedDate = baseDate.AddDays(-10),
                RequiredCompletionDate = baseDate.AddDays(-2),
                EstimatedHours = 5.0,
                EstimatedCost = 850.00m,
                JobCardStatus = "Completed",
                ApprovalStatus = "Approved",
                ApprovedById = supervisorId,
                ApprovedDate = baseDate.AddDays(-9),
                // Admission
                AssetConditionOnAdmission = "Fair",
                HoursReadingOnAdmission = 1260.0m,
                FuelLevelOnAdmission = 40.0m,
                AdmissionNotes = "Forklift showing hydraulic leak. Lifting performance degraded.",
                BayOrStation = "Bay 2",
                // Completion
                CompletedDate = baseDate.AddDays(-2),
                CompletionNotes = "Hydraulic cylinder seals replaced. System pressure tested and leak resolved.",
                AssetConditionOnCompletion = "Good",
                HoursReadingOnCompletion = 1262.0m,
                FuelLevelOnCompletion = 38.0m,
                WorkCompletedSummary = "Hydraulic cylinder disassembled, seals and O-rings replaced, cylinder reassembled, hydraulic system flushed, pressure tested to specification.",
                WarrantyDays = 90,
                WarrantyTerms = "90-day warranty on hydraulic repairs",
                WarrantyExpiration = baseDate.AddDays(-2).AddDays(90),
                TenantId = tenantId,
                CreatedAt = baseDate.AddDays(-10),
                CreatedById = maintenanceManagerId,
                CreatedBy = "System"
            }
        );

        // Job Card 5: In progress (no completion yet)
        var jobCard5Id = Guid.Parse("eeee5555-5555-5555-5555-555555555555");
        builder.Entity<JobCard>().HasData(
            new JobCard
            {
                Id = jobCard5Id,
                JobCardNumber = "JC-2025-0005",
                AssetId = truckId,
                MaintenanceTypeId = preventiveId,
                PriorityLevelId = mediumPriorityId,
                Title = "6-Month Service - Truck TRK-001",
                Description = "Scheduled 6-month preventive maintenance",
                ProblemDescription = "Routine 6-month service due. Engine oil change, filters, and general inspection.",
                MaintenanceLocation = "Internal",
                RequestedById = maintenanceManagerId,
                RequestedDate = baseDate.AddDays(-5),
                RequiredCompletionDate = baseDate.AddDays(5),
                EstimatedHours = 4.0,
                EstimatedCost = 600.00m,
                JobCardStatus = "Approved",
                ApprovalStatus = "Approved",
                ApprovedById = supervisorId,
                ApprovedDate = baseDate.AddDays(-4),
                AssignedTechnicianId = technicianId,
                PlannedStartDate = baseDate.AddDays(-3),
                PlannedEndDate = baseDate.AddDays(2),
                // Admission only
                AssetConditionOnAdmission = "Good",
                MileageReadingOnAdmission = 86250.0m,
                FuelLevelOnAdmission = 50.0m,
                AdmissionNotes = "Truck admitted for routine 6-month service. Currently in Bay 4.",
                BayOrStation = "Bay 4",
                TenantId = tenantId,
                CreatedAt = baseDate.AddDays(-5),
                CreatedById = maintenanceManagerId,
                CreatedBy = "System"
            }
        );

        // Seed Certificate for Job Card 1 (completed workflow)
        var certificate1Id = Guid.Parse("ffff1111-1111-1111-1111-111111111111");
        builder.Entity<JobCardCertificate>().HasData(
            new JobCardCertificate
            {
                Id = certificate1Id,
                JobCardId = jobCard1Id,
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
            }
        );

        // Seed Comments for Job Cards (Audit Trail)
        builder.Entity<JobCardComment>().HasData(
            // Job Card 1 Comments
            new JobCardComment
            {
                Id = Guid.Parse("99991111-0000-0000-0000-000000000001"),
                JobCardId = jobCard1Id,
                CommentById = maintenanceManagerId,
                Comment = "Job card created for scheduled preventive maintenance.",
                CommentType = "General",
                IsInternal = false,
                CommentDate = baseDate.AddDays(-30),
                TenantId = tenantId,
                CreatedAt = baseDate.AddDays(-30),
                CreatedBy = "System"
            },
            new JobCardComment
            {
                Id = Guid.Parse("99991111-0000-0000-0000-000000000002"),
                JobCardId = jobCard1Id,
                CommentById = technicianId,
                Comment = $"Job card completed by {technicianId}. All maintenance tasks completed successfully. Oil changed, filters replaced, brakes serviced.",
                CommentType = "Completion",
                IsInternal = false,
                CommentDate = baseDate.AddDays(-8),
                TenantId = tenantId,
                CreatedAt = baseDate.AddDays(-8),
                CreatedBy = "System"
            },
            new JobCardComment
            {
                Id = Guid.Parse("99991111-0000-0000-0000-000000000003"),
                JobCardId = jobCard1Id,
                CommentById = qualityCheckerId,
                Comment = $"Quality check passed by employee {qualityCheckerId}. All work meets quality standards. Asset tested and performing excellently. No defects found.",
                CommentType = "QualityCheck",
                IsInternal = false,
                CommentDate = baseDate.AddDays(-6),
                TenantId = tenantId,
                CreatedAt = baseDate.AddDays(-6),
                CreatedBy = "System"
            },
            new JobCardComment
            {
                Id = Guid.Parse("99991111-0000-0000-0000-000000000004"),
                JobCardId = jobCard1Id,
                CommentById = maintenanceManagerId,
                Comment = $"Job card accepted by employee {maintenanceManagerId}. Asset accepted. Forklift is running smoothly. Very satisfied with the maintenance work.",
                CommentType = "Acceptance",
                IsInternal = false,
                CommentDate = baseDate.AddDays(-4),
                TenantId = tenantId,
                CreatedAt = baseDate.AddDays(-4),
                CreatedBy = "System"
            },
            new JobCardComment
            {
                Id = Guid.Parse("99991111-0000-0000-0000-000000000005"),
                JobCardId = jobCard1Id,
                CommentById = supervisorId,
                Comment = "Certificate CERT-2025-0001 generated for job card. Type: Preventive Maintenance Completion",
                CommentType = "Certificate",
                IsInternal = false,
                CommentDate = baseDate.AddDays(-3),
                TenantId = tenantId,
                CreatedAt = baseDate.AddDays(-3),
                CreatedBy = "System"
            },
            // Job Card 2 Comments
            new JobCardComment
            {
                Id = Guid.Parse("99992222-0000-0000-0000-000000000001"),
                JobCardId = jobCard2Id,
                CommentById = maintenanceManagerId,
                Comment = "Emergency brake system repair requested. High priority due to safety concerns.",
                CommentType = "General",
                IsInternal = false,
                CommentDate = baseDate.AddDays(-20),
                TenantId = tenantId,
                CreatedAt = baseDate.AddDays(-20),
                CreatedBy = "System"
            },
            new JobCardComment
            {
                Id = Guid.Parse("99992222-0000-0000-0000-000000000002"),
                JobCardId = jobCard2Id,
                CommentById = technicianId,
                Comment = $"Job card completed by {technicianId}. Brake system completely overhauled. All brake pads replaced, brake fluid flushed and replaced, brake lines inspected.",
                CommentType = "Completion",
                IsInternal = false,
                CommentDate = baseDate.AddDays(-12),
                TenantId = tenantId,
                CreatedAt = baseDate.AddDays(-12),
                CreatedBy = "System"
            },
            new JobCardComment
            {
                Id = Guid.Parse("99992222-0000-0000-0000-000000000003"),
                JobCardId = jobCard2Id,
                CommentById = qualityCheckerId,
                Comment = $"Quality check passed by employee {qualityCheckerId}. Brake system tested thoroughly. Stopping distance improved significantly. All safety checks passed.",
                CommentType = "QualityCheck",
                IsInternal = false,
                CommentDate = baseDate.AddDays(-10),
                TenantId = tenantId,
                CreatedAt = baseDate.AddDays(-10),
                CreatedBy = "System"
            },
            new JobCardComment
            {
                Id = Guid.Parse("99992222-0000-0000-0000-000000000004"),
                JobCardId = jobCard2Id,
                CommentById = maintenanceManagerId,
                Comment = $"Job card accepted by employee {maintenanceManagerId}. Brakes are working perfectly now. Test drive completed successfully.",
                CommentType = "Acceptance",
                IsInternal = false,
                CommentDate = baseDate.AddDays(-8),
                TenantId = tenantId,
                CreatedAt = baseDate.AddDays(-8),
                CreatedBy = "System"
            }
        );

    }

    private void ConfigureFinanceEntities(ModelBuilder builder)
    {
        // Configure BankAccount entity
        builder.Entity<BankAccount>(entity =>
        {
            entity.HasMany(b => b.Transactions)
                .WithOne(t => t.BankAccount)
                .HasForeignKey(t => t.BankAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(b => b.Statements)
                .WithOne(s => s.BankAccount)
                .HasForeignKey(s => s.BankAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(b => b.Reconciliations)
                .WithOne(r => r.BankAccount)
                .HasForeignKey(r => r.BankAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure CashTransaction entity
        builder.Entity<CashTransaction>(entity =>
        {
            entity.HasOne(t => t.ToBankAccount)
                .WithMany()
                .HasForeignKey(t => t.ToBankAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.PaymentMethod)
                .WithMany()
                .HasForeignKey(t => t.PaymentMethodId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(t => t.Cheque)
                .WithOne(c => c.CashTransaction)
                .HasForeignKey<CashTransaction>(t => t.ChequeId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(t => t.Reconciliation)
                .WithMany(r => r.ReconciledTransactions)
                .HasForeignKey(t => t.ReconciliationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure ReconciliationMatch entity
        builder.Entity<ReconciliationMatch>(entity =>
        {
            entity.HasOne(rm => rm.BankStatementLine)
                .WithOne(bsl => bsl.ReconciliationMatch)
                .HasForeignKey<ReconciliationMatch>(rm => rm.BankStatementLineId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure ModuleDefinition entity
        builder.Entity<ModuleDefinition>(entity =>
        {
            entity.HasIndex(m => m.ModuleCode).IsUnique();
            entity.Property(m => m.ModuleCode).IsRequired().HasMaxLength(50);
            entity.Property(m => m.ModuleName).IsRequired().HasMaxLength(100);
            entity.HasIndex(m => m.IsActive);
            entity.HasIndex(m => m.SortOrder);
        });

        // Configure PeriodModuleLock entity
        builder.Entity<PeriodModuleLock>(entity =>
        {
            // Ensure one lock entry per module per period
            entity.HasIndex(x => new { x.FiscalPeriodId, x.ModuleDefinitionId }).IsUnique();

            entity.HasOne(x => x.FiscalPeriod)
                .WithMany(p => p.PeriodModuleLocks)
                .HasForeignKey(x => x.FiscalPeriodId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.ModuleDefinition)
                .WithMany()
                .HasForeignKey(x => x.ModuleDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure TransactionDocumentModuleMapping entity
        builder.Entity<TransactionDocumentModuleMapping>(entity =>
        {
            entity.HasIndex(x => x.DocumentType).IsUnique();
            
            entity.HasOne(x => x.ModuleDefinition)
                .WithMany()
                .HasForeignKey(x => x.ModuleDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private void ConfigureTaxEntities(ModelBuilder builder)
    {
        // Tax
        builder.Entity<Tax>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            
            entity.Property(e => e.Rate).HasColumnType("decimal(18,4)");
            entity.Property(e => e.ThresholdAmount).HasColumnType("decimal(18,2)");
        });

        // TaxGroup
        builder.Entity<TaxGroup>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
        });

        // TaxGroupComponent
        builder.Entity<TaxGroupComponent>(entity =>
        {
            entity.HasOne(d => d.TaxGroup)
                .WithMany(p => p.Components)
                .HasForeignKey(d => d.TaxGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Tax)
                .WithMany(p => p.GroupComponents)
                .HasForeignKey(d => d.TaxId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // TaxRateHistory
        builder.Entity<TaxRateHistory>(entity =>
        {
            entity.HasOne(d => d.Tax)
                .WithMany(p => p.RateHistory)
                .HasForeignKey(d => d.TaxId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Rate).HasColumnType("decimal(18,4)");
        });
        
        // TaxThreshold
        builder.Entity<TaxThreshold>(entity =>
        {
             entity.HasOne(d => d.Tax)
                .WithMany(p => p.Thresholds)
                .HasForeignKey(d => d.TaxId)
                .OnDelete(DeleteBehavior.Cascade);

             entity.Property(e => e.ThresholdAmount).HasColumnType("decimal(18,2)");
        });

        // TaxCalculation
        builder.Entity<TaxCalculation>(entity =>
        {
             entity.HasOne(d => d.Tax)
                .WithMany(p => p.TaxCalculations)
                .HasForeignKey(d => d.TaxId)
                .OnDelete(DeleteBehavior.Restrict);

             entity.HasOne(d => d.TaxGroup)
                .WithMany()
                .HasForeignKey(d => d.TaxGroupId)
                .OnDelete(DeleteBehavior.SetNull);

             entity.Property(e => e.TaxableAmount).HasColumnType("decimal(18,2)");
             entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
             entity.Property(e => e.TaxRate).HasColumnType("decimal(18,4)");
        });

        // Configure AccountTransaction to fix cascade path issues
        builder.Entity<AccountTransaction>(entity =>
        {
            entity.HasOne(at => at.Account)
                .WithMany()
                .HasForeignKey(at => at.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(at => at.JournalEntry)
                .WithMany()
                .HasForeignKey(at => at.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(at => at.FiscalPeriod)
                .WithMany()
                .HasForeignKey(at => at.FiscalPeriodId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(at => at.ReversalTransaction)
                .WithOne()
                .HasForeignKey<AccountTransaction>(at => at.ReversalTransactionId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(at => at.OriginalTransaction)
                .WithOne()
                .HasForeignKey<AccountTransaction>(at => at.OriginalTransactionId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure PaymentAllocation relationships to prevent cascade cycles
        builder.Entity<PaymentAllocation>(entity =>
        {
            // Invoice relationship - NO ACTION (Invoice->Tenant is CASCADE)
            entity.HasOne(pa => pa.Invoice)
                .WithMany(i => i.PaymentAllocations)
                .HasForeignKey(pa => pa.InvoiceId)
                .OnDelete(DeleteBehavior.NoAction);

            // CustomerPayment relationship - NO ACTION (safer)
            entity.HasOne(pa => pa.CustomerPayment)
                .WithMany(cp => cp.Allocations)
                .HasForeignKey(pa => pa.CustomerPaymentId)
                .OnDelete(DeleteBehavior.NoAction);

            // Tenant relationship - NO ACTION
            entity.HasOne(pa => pa.Tenant)
                .WithMany()
                .HasForeignKey(pa => pa.TenantId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Global fix: Change cascade delete to NO ACTION for AR entities' Tenant FKs
        // This prevents SQL Server's "multiple cascade paths" error

        // Configure VendorPaymentAllocation relationships to prevent cascade cycles
        builder.Entity<VendorPaymentAllocation>(entity =>
        {
            // VendorInvoice relationship - NO ACTION
            entity.HasOne(vpa => vpa.VendorInvoice)
                .WithMany()
                .HasForeignKey(vpa => vpa.VendorInvoiceId)
                .OnDelete(DeleteBehavior.NoAction);

            // VendorPayment relationship - NO ACTION
            entity.HasOne(vpa => vpa.VendorPayment)
                .WithMany(vp => vp.Allocations)
                .HasForeignKey(vpa => vpa.VendorPaymentId)
                .OnDelete(DeleteBehavior.NoAction);

            // Tenant relationship - NO ACTION
            entity.HasOne(vpa => vpa.Tenant)
                .WithMany()
                .HasForeignKey(vpa => vpa.TenantId)
                .OnDelete(DeleteBehavior.NoAction);
        });
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (tableName == "Invoices" || tableName == "InvoiceLineItems" || 
                tableName == "CustomerPayments" || tableName == "PaymentAllocations")
            {
                var tenantFk = entityType.GetForeignKeys()
                    .FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(Tenant) &&
                                         fk.Properties.Any(p => p.Name == "TenantId"));
                
                if (tenantFk != null)
                {
                    tenantFk.DeleteBehavior = DeleteBehavior.NoAction;
                }
            }
        }
    }

    private void ConfigureFixedAssetsEntities(ModelBuilder builder)
    {
        // FixedAssetCategory
        builder.Entity<FixedAssetCategory>(entity =>
        {
            entity.HasIndex(c => c.Name);
            entity.HasIndex(c => c.Code);
            entity.HasIndex(c => c.TenantId);

            // GL Account Relationships (Restrict Delete to prevent breaking financial integrity)
            entity.HasOne(c => c.AssetAccount)
                .WithMany()
                .HasForeignKey(c => c.AssetAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.AccumulatedDepreciationAccount)
                .WithMany()
                .HasForeignKey(c => c.AccumulatedDepreciationAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.DepreciationExpenseAccount)
                .WithMany()
                .HasForeignKey(c => c.DepreciationExpenseAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(c => c.GainOnDisposalAccount)
                .WithMany()
                .HasForeignKey(c => c.GainOnDisposalAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.LossOnDisposalAccount)
                .WithMany()
                .HasForeignKey(c => c.LossOnDisposalAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // FixedAsset
        builder.Entity<FixedAsset>(entity =>
        {
            entity.HasIndex(a => a.AssetCode).IsUnique(); // Asset Code must be unique per tenant? 
            // Note: If Asset Code should be unique per tenant, we'd need a composite index {TenantId, AssetCode}
            // Usually Asset Tags are unique organization-wide or per tenant. Assuming per tenant for now.
            entity.HasIndex(a => new { a.TenantId, a.AssetCode }).IsUnique();

            entity.HasIndex(a => a.FixedAssetCategoryId);
            entity.HasIndex(a => a.Status);
            entity.HasIndex(a => a.MaintenanceAssetId);

            entity.HasOne(a => a.Category)
                .WithMany(c => c.Assets)
                .HasForeignKey(a => a.FixedAssetCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.MaintenanceAsset)
                .WithMany() // No navigation back from MaintenanceAsset to FixedAsset required for now
                .HasForeignKey(a => a.MaintenanceAssetId)
                .OnDelete(DeleteBehavior.SetNull); // If physical asset is deleted, keep financial record? Or Restrict?
                // SetNull allows keeping the financial record even if the maintenance record is purged.
        });

        // AssetDepreciationSchedule
        builder.Entity<AssetDepreciationSchedule>(entity =>
        {
            entity.HasIndex(ds => new { ds.FixedAssetId, ds.FiscalPeriodId }).IsUnique();
            entity.HasIndex(ds => ds.IsPosted);

            entity.HasOne(ds => ds.FixedAsset)
                .WithMany(a => a.DepreciationSchedules)
                .HasForeignKey(ds => ds.FixedAssetId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ds => ds.FiscalPeriod)
                .WithMany()
                .HasForeignKey(ds => ds.FiscalPeriodId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ds => ds.JournalEntry)
                .WithMany()
                .HasForeignKey(ds => ds.JournalEntryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // AssetTransaction
        builder.Entity<AssetTransaction>(entity =>
        {
            entity.HasIndex(t => t.FixedAssetId);
            entity.HasIndex(t => t.TransactionDate);
            entity.HasIndex(t => t.TransactionType);

            entity.HasOne(t => t.FixedAsset)
                .WithMany(a => a.Transactions)
                .HasForeignKey(t => t.FixedAssetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AssetDisposal
        builder.Entity<AssetDisposal>(entity =>
        {
            entity.HasIndex(d => d.FixedAssetId);
            entity.HasIndex(d => d.DisposalDate);
            entity.HasIndex(d => d.Status);

            entity.HasOne(d => d.FixedAsset)
                .WithMany()
                .HasForeignKey(d => d.FixedAssetId)
                .OnDelete(DeleteBehavior.Restrict);
                
            entity.HasOne(d => d.RequestedBy)
                .WithMany()
                .HasForeignKey(d => d.RequestedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ApprovedBy)
                .WithMany()
                .HasForeignKey(d => d.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.JournalEntry)
                .WithMany()
                .HasForeignKey(d => d.JournalEntryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // AssetTransfer
        builder.Entity<AssetTransfer>(entity =>
        {
            entity.HasIndex(t => t.FixedAssetId);
            entity.HasIndex(t => t.TransferDate);
            entity.HasIndex(t => t.Status);

            entity.HasOne(t => t.FixedAsset)
                .WithMany() // Or add a collection in FixedAsset if needed
                .HasForeignKey(t => t.FixedAssetId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(t => t.FromCustodian)
                .WithMany()
                .HasForeignKey(t => t.FromCustodianId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.ToCustodian)
                .WithMany()
                .HasForeignKey(t => t.ToCustodianId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.RequestedBy)
                .WithMany()
                .HasForeignKey(t => t.RequestedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.ApprovedBy)
                .WithMany()
                .HasForeignKey(t => t.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // AssetVerificationSession
        builder.Entity<AssetVerificationSession>(entity =>
        {
            entity.HasIndex(s => s.Status);
            entity.HasIndex(s => s.ScheduledDate);

            entity.HasOne(s => s.VerifiedBy)
                .WithMany()
                .HasForeignKey(s => s.VerifiedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // AssetVerificationItem
        builder.Entity<AssetVerificationItem>(entity =>
        {
            entity.HasIndex(i => i.SessionId);
            entity.HasIndex(i => i.FixedAssetId);

            entity.HasOne(i => i.Session)
                .WithMany(s => s.Items)
                .HasForeignKey(i => i.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(i => i.FixedAsset)
                .WithMany()
                .HasForeignKey(i => i.FixedAssetId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
