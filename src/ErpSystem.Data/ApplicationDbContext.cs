using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.HR.Payroll;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Numbering;
using ErpSystem.Core.Entities.Pricing;
using ErpSystem.Core.Entities.Procedures;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Data.Configuration;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Data.Configuration.Maintenance;
using ErpSystem.Data.Configuration.Pricing;
using ErpSystem.Data.Configuration.Procurement;
using ErpSystem.Data.Configuration.Projects;
using ErpSystem.Data.Configuration.Sales;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FinancePaymentMethod = ErpSystem.Core.Entities.Finance.PaymentMethod;

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

    /// <summary>
    /// Protected constructor for derived contexts (e.g. ReportingDbContext) that use their own typed options.
    /// </summary>
    protected ApplicationDbContext(DbContextOptions options) : base(options)
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, Guid? tenantId) : base(options)
    {
        _tenantId = tenantId;
    }

    // TEMPORARILY DISABLED - This constructor was causing hangs during login
    // because it tries to access ICurrentUserProvider before authentication is complete
    // public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IServiceProvider serviceProvider) : base(options)
    // {
    //     // Try to get tenant ID from ICurrentUserProvider
    //     var currentUserProvider = serviceProvider?.GetService(typeof(ErpSystem.Core.Interfaces.ICurrentUserProvider))
    //         as ErpSystem.Core.Interfaces.ICurrentUserProvider;
    //
    //     if (currentUserProvider != null)
    //     {
    //         try
    //         {
    //             var tid = currentUserProvider.TenantId;
    //             if (tid != Guid.Empty)
    //             {
    //                 _tenantId = tid;
    //             }
    //         }
    //         catch
    //         {
    //             // Tenant ID not available - this is fine for seeding and anonymous requests
    //             _tenantId = null;
    //         }
    //     }
    // }

    // Core entities
    public DbSet<Tenant> Tenants { get; set; }

    // Finance entities
    public DbSet<ExchangeRate> ExchangeRates { get; set; }
    public DbSet<FiscalYear> FiscalYears { get; set; }
    public DbSet<FiscalPeriod> FiscalPeriods { get; set; }
    public DbSet<AccountSegmentStructure> AccountSegmentStructures { get; set; }
    public DbSet<SegmentLookupValue> SegmentLookupValues { get; set; }
    public DbSet<Account> Accounts { get; set; }
    public DbSet<AccountingBook> AccountingBooks { get; set; }
    public DbSet<AccountAccountingBook> AccountAccountingBooks { get; set; }
    public DbSet<AccountSegmentValue> AccountSegmentValues { get; set; }
    public DbSet<FinanceSettings> FinanceSettings { get; set; }
    public DbSet<DocumentSequenceDefinition> DocumentSequenceDefinitions { get; set; }
    public DbSet<DocumentNumberReservation> DocumentNumberReservations { get; set; }
    public DbSet<Tax> Taxes { get; set; }
    public DbSet<TaxGroup> TaxGroups { get; set; }
    public DbSet<TaxGroupComponent> TaxGroupComponents { get; set; }
    public DbSet<ModuleDefinition> ModuleDefinitions { get; set; }
    public DbSet<PeriodModuleLock> PeriodModuleLocks { get; set; }
    public DbSet<TransactionDocumentModuleMapping> TransactionDocumentModuleMappings { get; set; }
    public DbSet<FixedAssetCategory> FixedAssetCategories { get; set; }
    public DbSet<FixedAsset> FixedAssets { get; set; }
    public DbSet<FixedAssetBookValue> FixedAssetBookValues { get; set; }
    public DbSet<AssetDisposal> AssetDisposals { get; set; }
    public DbSet<AssetTransfer> AssetTransfers { get; set; }
    public DbSet<AssetTransaction> AssetTransactions { get; set; }
    public DbSet<AssetValuation> AssetValuations { get; set; }
    public DbSet<AssetDepreciationSchedule> AssetDepreciationSchedules { get; set; }
    public DbSet<FixedAssetDepreciationRun> FixedAssetDepreciationRuns { get; set; }
    public DbSet<AssetVerificationSession> AssetVerificationSessions { get; set; }
    public DbSet<AssetVerificationItem> AssetVerificationItems { get; set; }

    // Capital Projects (AUC) entities
    public DbSet<CapitalProject> CapitalProjects { get; set; }
    public DbSet<ProjectCostLine> ProjectCostLines { get; set; }
    public DbSet<ProjectSettlementRule> ProjectSettlementRules { get; set; }

    // Lease Accounting (IFRS 16) entities
    public DbSet<LeaseContract> LeaseContracts { get; set; }
    public DbSet<LeaseScheduleLine> LeaseScheduleLines { get; set; }
    public DbSet<JournalEntry> JournalEntries { get; set; }
    public DbSet<RecurringJournalTemplate> RecurringJournalTemplates { get; set; }
    public DbSet<RecurringJournalTemplateLine> RecurringJournalTemplateLines { get; set; }
    public DbSet<RecurringJournalOccurrence> RecurringJournalOccurrences { get; set; }
    public DbSet<FinancePostingEvent> FinancePostingEvents { get; set; }
    public DbSet<AccountTransaction> AccountTransactions { get; set; }
    public DbSet<FxRealizedSettlement> FxRealizedSettlements { get; set; }
    public DbSet<FxRevaluationBatch> FxRevaluationBatches { get; set; }
    public DbSet<FxRevaluationLine> FxRevaluationLines { get; set; }
    public DbSet<SubledgerSettlementBalance> SubledgerSettlementBalances { get; set; }
    public DbSet<SubledgerSettlementApplication> SubledgerSettlementApplications { get; set; }
    public DbSet<SubledgerUnappliedSettlementBalance> SubledgerUnappliedSettlementBalances { get; set; }
    public DbSet<OpeningBalanceBatch> OpeningBalanceBatches { get; set; }
    public DbSet<OpeningBalanceLine> OpeningBalanceLines { get; set; }
    public DbSet<AccountCurrencyLink> AccountCurrencyLinks { get; set; }
    public DbSet<BudgetScenario> BudgetScenarios { get; set; }
    public DbSet<BudgetEntry> BudgetEntries { get; set; }
    public DbSet<BudgetReturn> BudgetReturns { get; set; }
    public DbSet<BankAccount> BankAccounts { get; set; }
    public DbSet<FinancePaymentMethod> PaymentMethods { get; set; }
    public DbSet<TaxRule> TaxRules { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<VendorInvoice> VendorInvoices { get; set; }
    public DbSet<SubledgerAdjustmentJournal> SubledgerAdjustmentJournals { get; set; }
    public DbSet<SupplierReturn> SupplierReturns { get; set; }
    public DbSet<SupplierReturnLineItem> SupplierReturnLineItems { get; set; }
    public DbSet<SupplierDebitNote> SupplierDebitNotes { get; set; }
    public DbSet<SupplierDebitNoteLineItem> SupplierDebitNoteLineItems { get; set; }
    public DbSet<FinancePurchaseOrder> FinancePurchaseOrders { get; set; }
    public DbSet<FinancePurchaseOrderItem> FinancePurchaseOrderItems { get; set; }
    public DbSet<FinancePurchaseOrderReceipt> FinancePurchaseOrderReceipts { get; set; }
    public DbSet<FinancePurchaseOrderReceiptItem> FinancePurchaseOrderReceiptItems { get; set; }

    // Finance unit-accounting/statistical ledger entities
    public DbSet<UnitType> UnitTypes { get; set; }
    public DbSet<UnitAccount> UnitAccounts { get; set; }
    public DbSet<UnitJournalEntry> UnitJournalEntries { get; set; }
    public DbSet<UnitJournalEntryLine> UnitJournalEntryLines { get; set; }
    public DbSet<UnitAccountBalance> UnitAccountBalances { get; set; }
    public DbSet<UnitAccountBudget> UnitAccountBudgets { get; set; }
    public DbSet<RatioDefinition> RatioDefinitions { get; set; }
    public DbSet<AllocationRule> AllocationRules { get; set; }
    public DbSet<AllocationTarget> AllocationTargets { get; set; }

    // Sales Order Management entities
    public DbSet<SalesOrder> SalesOrders { get; set; }
    public DbSet<SalesOrderLine> SalesOrderLines { get; set; }
    public DbSet<SalesOrderStatusHistory> SalesOrderStatusHistories { get; set; }
    public DbSet<DeliveryNote> DeliveryNotes { get; set; }
    public DbSet<DeliveryNoteLine> DeliveryNoteLines { get; set; }

    // Sales Agreement Management entities
    public DbSet<SalesAgreement> SalesAgreements { get; set; }
    public DbSet<SalesAgreementLine> SalesAgreementLines { get; set; }
    public DbSet<SalesAgreementMilestone> SalesAgreementMilestones { get; set; }
    public DbSet<SalesAgreementRenewal> SalesAgreementRenewals { get; set; }
    public DbSet<SalesAgreementDocument> SalesAgreementDocuments { get; set; }
    public DbSet<SalesSaleableSource> SalesSaleableSources { get; set; }
    public DbSet<SalesAllocation> SalesAllocations { get; set; }
    public DbSet<SalesAllocationHistory> SalesAllocationHistories { get; set; }

    // CRM entities
    public DbSet<Lead> Leads { get; set; }
    public DbSet<Opportunity> Opportunities { get; set; }
    public DbSet<Quote> Quotes { get; set; }
    public DbSet<QuoteLineItem> QuoteLineItems { get; set; }
    public DbSet<Activity> CrmActivities { get; set; }
    public DbSet<Campaign> Campaigns { get; set; }
    public DbSet<CampaignMember> CampaignMembers { get; set; }
    public DbSet<Product> SalesProducts { get; set; }

    // Competitor Intelligence entities
    public DbSet<Competitor> Competitors { get; set; }
    public DbSet<CompetitorDeal> CompetitorDeals { get; set; }

    // Return Order / Credit Note / Refund entities
    public DbSet<ReturnOrder> ReturnOrders { get; set; }
    public DbSet<ReturnOrderLine> ReturnOrderLines { get; set; }
    public DbSet<CreditNote> CreditNotes { get; set; }
    public DbSet<CreditNoteLine> CreditNoteLines { get; set; }
    public DbSet<Refund> Refunds { get; set; }

    // Collections & Debt Management entities
    public DbSet<CollectionActivity> CollectionActivities { get; set; }
    public DbSet<PaymentPlan> PaymentPlans { get; set; }
    public DbSet<PaymentPlanInstallment> PaymentPlanInstallments { get; set; }

    public DbSet<TenantModule> TenantModules { get; set; }
    public DbSet<UserTenant> UserTenants { get; set; }

    // Settings entities
    public DbSet<EmailSettings> EmailSettings { get; set; }
    public DbSet<EmailTemplate> EmailTemplates { get; set; }
    public DbSet<SystemSettings> SystemSettings { get; set; }
    public DbSet<Security> Securities { get; set; }

    // File upload governance (per-tenant, per-category)
    public DbSet<FileUploadPolicy> FileUploadPolicies { get; set; }
    public DbSet<FileUploadRecord> FileUploadRecords { get; set; }

    // Central Document Management entities
    public DbSet<CentralDocumentRecord> CentralDocumentRecords { get; set; }
    public DbSet<CentralDocumentVersion> CentralDocumentVersions { get; set; }
    public DbSet<CentralDocumentMetadataTemplate> CentralDocumentMetadataTemplates { get; set; }
    public DbSet<CentralDocumentMetadataValue> CentralDocumentMetadataValues { get; set; }
    public DbSet<CentralDocumentGenerationTemplate> CentralDocumentGenerationTemplates { get; set; }
    public DbSet<CentralDocumentAnnotationReview> CentralDocumentAnnotationReviews { get; set; }
    public DbSet<CentralDocumentAccessRule> CentralDocumentAccessRules { get; set; }
    public DbSet<CentralDocumentRetentionPolicy> CentralDocumentRetentionPolicies { get; set; }

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
    public DbSet<MaintenanceAssetMovement> MaintenanceAssetMovements { get; set; }
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
    public DbSet<MaintenanceAttachmentAccess> MaintenanceAttachmentAccessLogs { get; set; }

    public DbSet<AssetAdmission> AssetAdmissions { get; set; }
    public DbSet<AssetDischarge> AssetDischarges { get; set; }
    public DbSet<AssetMaintenanceDowntime> AssetMaintenanceDowntimes { get; set; }

    // Asset Condition Inspection entities (Pre-Inspection at admission / Post-Inspection at discharge)
    public DbSet<PreInspectionChecklistTemplate> PreInspectionChecklistTemplates { get; set; }
    public DbSet<PreInspectionChecklistItem> PreInspectionChecklistItems { get; set; }
    public DbSet<AssetConditionRecord> AssetConditionRecords { get; set; }
    public DbSet<AssetConditionItemResult> AssetConditionItemResults { get; set; }

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
    public DbSet<SystemExceptionLog> SystemExceptionLogs { get; set; }
    public DbSet<NotificationTopic> NotificationTopics { get; set; }
    public DbSet<NotificationTopicRecipient> NotificationTopicRecipients { get; set; }
    public DbSet<EmailCampaign> EmailCampaigns { get; set; }
    public DbSet<EmailCampaignRecipient> EmailCampaignRecipients { get; set; }
    public DbSet<MaintenanceNotification> MaintenanceNotifications { get; set; }
    public DbSet<MaintenanceNotificationTemplate> MaintenanceNotificationTemplates { get; set; }
    public DbSet<MaintenanceEscalationRule> MaintenanceEscalationRules { get; set; }
    public DbSet<SmsSettings> SmsSettings { get; set; }
    public DbSet<DataRetentionPolicy> DataRetentionPolicies { get; set; }
    public DbSet<DataRetentionJobRun> DataRetentionJobRuns { get; set; }

    #region HR Entities

    #region Company Setup
    
    public DbSet<Division> Divisions { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<Section> Sections { get; set; }
    public DbSet<Unit> Units { get; set; }
    public DbSet<EmployeePosition> EmployeePositions { get; set; }
    public DbSet<BenefitPolicy> BenefitPolicies { get; set; }
    public DbSet<BenefitPolicyRelation> BenefitPolicyRelations { get; set; }
    public DbSet<EmployeePositionBenefit> EmployeePositionBenefits { get; set; }
    public DbSet<Skill> Skills { get; set; }
    public DbSet<PositionSkillRequirement> PositionSkillRequirements { get; set; }
    public DbSet<StaffLevel> StaffLevels { get; set; }
    public DbSet<Shift> Shifts { get; set; }
    public DbSet<WorkStation> WorkStations { get; set; }
    public DbSet<EmployeeContractType> EmployeeContractTypes { get; set; }

    public DbSet<OrganizationStructure> OrganizationStructures { get; set; }
    public DbSet<OrganizationLevel> OrganizationLevels { get; set; }
    public DbSet<OrganizationUnit> OrganizationUnits { get; set; }
    public DbSet<OrganizationUnitHistory> OrganizationUnitHistories { get; set; }
    public DbSet<OrganizationChartNode> OrganizationChartNodes { get; set; }

    public DbSet<LocationStructure> LocationStructures { get; set; }
    public DbSet<LocationLevel> LocationLevels { get; set; }
    public DbSet<Location> Locations { get; set; }
    public DbSet<LocationContact> LocationContacts { get; set; }

    public DbSet<SalaryGrade> SalaryGrades { get; set; }
    public DbSet<SalaryLevel> SalaryLevels { get; set; }
    public DbSet<SalaryNotch> SalaryNotches { get; set; }

    #endregion

    #region Employee Management
    
    public DbSet<Employee> Employees { get; set; }
    public DbSet<EmployeeContact> EmployeeContacts { get; set; }
    public DbSet<EmployeeEmergencyContact> EmployeeEmergencyContacts { get; set; }
    public DbSet<EmployeeDependent> EmployeeDependents { get; set; }
    public DbSet<EmployeeDependentBenefit> EmployeeDependentBenefits { get; set; }
    public DbSet<EmployeeQualification> EmployeeQualifications { get; set; }
    public DbSet<EmployeeIdentificationCard> EmployeeIdentificationCards { get; set; }
    public DbSet<IdentificationType> IdentificationTypes { get; set; }
    public DbSet<EmployeeWorkHistory> EmployeeWorkHistories { get; set; }
    public DbSet<EmployeeContractDetail> EmployeeContractDetails { get; set; }
    public DbSet<ExpatriateAssignment> ExpatriateAssignments { get; set; }
    public DbSet<EmployeePositionHistory> EmployeePositionHistories { get; set; }
    public DbSet<EmployeeSalaryAssignment> EmployeeSalaryAssignments { get; set; }
    public DbSet<EmployeeReferee> EmployeeReferees { get; set; }
    public DbSet<EmployeeGuarantor> EmployeeGuarantors { get; set; }
    public DbSet<EmployeeSkill> EmployeeSkills { get; set; }
    public DbSet<EmployeeBiometric> EmployeeBiometrics { get; set; }
    public DbSet<EmployeeShiftPreference> EmployeeShiftPreferences { get; set; }
    public DbSet<EmployeeBank> EmployeeBanks { get; set; }
    public DbSet<EmployeeBankBranch> EmployeeBankBranches { get; set; }
    public DbSet<EmployeeBankDetail> EmployeeBankDetails { get; set; }

    #endregion Employee Management

    #region HR Payroll

    public DbSet<PayrollBudgetAnalysisRow> PayrollBudgetAnalysisRows { get; set; }
    public DbSet<PayrollParameterSet> PayrollParameterSets { get; set; }
    public DbSet<PayrollComponent> PayrollComponents { get; set; }
    public DbSet<PayrollComponentRule> PayrollComponentRules { get; set; }
    public DbSet<PayrollTaxBand> PayrollTaxBands { get; set; }
    public DbSet<PayrollTaxRelief> PayrollTaxReliefs { get; set; }
    public DbSet<PayrollPensionScheme> PayrollPensionSchemes { get; set; }
    public DbSet<PayrollOvertimePolicy> PayrollOvertimePolicies { get; set; }
    public DbSet<PayrollLoanPolicy> PayrollLoanPolicies { get; set; }
    public DbSet<PayrollBonusPolicy> PayrollBonusPolicies { get; set; }
    public DbSet<PayrollBonusRule> PayrollBonusRules { get; set; }
    public DbSet<PayrollBonusException> PayrollBonusExceptions { get; set; }
    public DbSet<PayrollBackpayPolicy> PayrollBackpayPolicies { get; set; }
    public DbSet<PayrollBackpayRule> PayrollBackpayRules { get; set; }
    public DbSet<PayrollBackpayException> PayrollBackpayExceptions { get; set; }
    public DbSet<PayrollJournalMapping> PayrollJournalMappings { get; set; }
    public DbSet<PayrollCodeType> PayrollCodeTypes { get; set; }
    public DbSet<PayrollCodeValue> PayrollCodeValues { get; set; }
    public DbSet<PayrollHoliday> PayrollHolidays { get; set; }
    public DbSet<PayrollNonWorkingDay> PayrollNonWorkingDays { get; set; }
    public DbSet<PayrollExchangeRate> PayrollExchangeRates { get; set; }
    public DbSet<PayrollBankBranch> PayrollBankBranches { get; set; }
    public DbSet<PayrollLeaveSetup> PayrollLeaveSetups { get; set; }
    public DbSet<PayrollLeaveSetupDetail> PayrollLeaveSetupDetails { get; set; }
    public DbSet<PayrollOvertimeRange> PayrollOvertimeRanges { get; set; }
    public DbSet<PayrollLegacyMenuUser> PayrollLegacyMenuUsers { get; set; }
    public DbSet<PayrollLegacyMenuSecurity> PayrollLegacyMenuSecurity { get; set; }
    public DbSet<PayrollCompanyProfile> PayrollCompanyProfiles { get; set; }
    public DbSet<PayrollBusinessUnit> PayrollBusinessUnits { get; set; }
    public DbSet<PayrollCompanyBanker> PayrollCompanyBankers { get; set; }
    public DbSet<PayrollGrade> PayrollGrades { get; set; }
    public DbSet<PayrollGradeNotch> PayrollGradeNotches { get; set; }
    public DbSet<PayrollEmployeeProfile> PayrollEmployeeProfiles { get; set; }
    public DbSet<PayrollSalaryBasis> PayrollSalaryBases { get; set; }
    public DbSet<PayrollPaymentMethod> PayrollPaymentMethods { get; set; }
    public DbSet<PayrollEmployeeComponent> PayrollEmployeeComponents { get; set; }
    public DbSet<PayrollLoan> PayrollLoans { get; set; }
    public DbSet<PayrollLoanSchedule> PayrollLoanSchedules { get; set; }
    public DbSet<PayrollSalaryAdvance> PayrollSalaryAdvances { get; set; }
    public DbSet<PayrollEmployeeTaxRelief> PayrollEmployeeTaxReliefs { get; set; }
    public DbSet<PayrollPromotionArrearsEntry> PayrollPromotionArrears { get; set; }
    public DbSet<PayrollTimesheetSummary> PayrollTimesheetSummaries { get; set; }
    public DbSet<PayrollContributionOpeningBalance> PayrollContributionOpeningBalances { get; set; }
    public DbSet<PayrollContributionTransaction> PayrollContributionTransactions { get; set; }
    public DbSet<PayrollImportBatch> PayrollImportBatches { get; set; }
    public DbSet<PayrollImportRow> PayrollImportRows { get; set; }
    public DbSet<PayrollRun> PayrollRuns { get; set; }
    public DbSet<PayrollRunEmployee> PayrollRunEmployees { get; set; }
    public DbSet<PayrollTransaction> PayrollTransactions { get; set; }
    public DbSet<PayrollJournalLine> PayrollJournalLines { get; set; }
    public DbSet<PayrollReportSnapshot> PayrollReportSnapshots { get; set; }
    public DbSet<PayrollPayslipSnapshot> PayrollPayslipSnapshots { get; set; }

    #endregion HR Payroll

    // Staff Attendance
    public DbSet<ShiftAssignment> ShiftAssignments { get; set; }
    public DbSet<AttendanceRecord> AttendanceRecords { get; set; }

    #region HR - Miscellaneous

    public DbSet<Country> Countries { get; set; }
    public DbSet<Qualification> Qualifications { get; set; }
    public DbSet<ExternalAssociate> ExternalAssociates { get; set; }

    #endregion

    // Leave Management
    public DbSet<LeaveType> LeaveTypes { get; set; }
    public DbSet<LeaveSubType> LeaveSubTypes { get; set; }
    public DbSet<LeaveCategoryAllocation> LeaveCategoryAllocations { get; set; }
    public DbSet<LeaveBalance> LeaveBalances { get; set; }
    public DbSet<LeavePlan> LeavePlans { get; set; }
    public DbSet<LeaveRequest> LeaveRequests { get; set; }
    public DbSet<PublicHoliday> PublicHolidays { get; set; }

    // Performance Management
    public DbSet<AppraisalGradeDefinition> AppraisalGradeDefinitions { get; set; }
    public DbSet<KpiDefinition> KpiDefinitions { get; set; }
    public DbSet<AppraisalCriteria> AppraisalCriterias { get; set; }
    public DbSet<PositionCriteriaMapping> PositionCriteriaMappings { get; set; }
    public DbSet<MappingGradeRange> MappingGradeRanges { get; set; }
    public DbSet<EmployeeKpiTarget> EmployeeKpiTargets { get; set; }
    public DbSet<PerformanceAppraisal> PerformanceAppraisals { get; set; }
    public DbSet<EvaluatorEvaluation> EvaluatorEvaluations { get; set; }
    public DbSet<CriterionScore> CriterionScores { get; set; }
    public DbSet<KpiEvaluationRecord> KpiEvaluationRecords { get; set; }
    public DbSet<AppraisalEmployeeResponse> AppraisalEmployeeResponses { get; set; }
    public DbSet<AppraisalAttachment> AppraisalAttachments { get; set; }
    public DbSet<PerformanceImprovementPlan> PerformanceImprovementPlans { get; set; }
    public DbSet<PipReviewMeeting> PipReviewMeetings { get; set; }

    #endregion HR Entities

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

    // Enhanced Inventory entities
    public DbSet<UnitOfMeasure> UnitsOfMeasure { get; set; }
    public DbSet<UnitOfMeasureConversion> UnitOfMeasureConversions { get; set; }
    public DbSet<ItemUnitOfMeasure> ItemUnitsOfMeasure { get; set; }
    public DbSet<ItemSupplier> ItemSuppliers { get; set; }
    public DbSet<GoodsReceiptNote> GoodsReceiptNotes { get; set; }
    public DbSet<GoodsReceiptNoteItem> GoodsReceiptNoteItems { get; set; }
    public DbSet<InventoryTransfer> InventoryTransfers { get; set; }
    public DbSet<InventoryTransferItem> InventoryTransferItems { get; set; }
    public DbSet<PhysicalCount> PhysicalCounts { get; set; }
    public DbSet<PhysicalCountItem> PhysicalCountItems { get; set; }
    public DbSet<InventoryCostLayer> InventoryCostLayers { get; set; }
    public DbSet<LandedCost> LandedCosts { get; set; }
    public DbSet<LandedCostItem> LandedCostItems { get; set; }
    public DbSet<LandedCostAllocation> LandedCostAllocations { get; set; }
    public DbSet<PurchaseReturn> PurchaseReturns { get; set; }
    public DbSet<PurchaseReturnItem> PurchaseReturnItems { get; set; }
    public DbSet<InventoryRequisition> InventoryRequisitions { get; set; }
    public DbSet<InventoryRequisitionItem> InventoryRequisitionItems { get; set; }

    // Inventory Valuation entities
    public DbSet<InventoryMovement> InventoryMovements { get; set; }
    public DbSet<InventoryLayer> InventoryLayers { get; set; }
    public DbSet<InventoryBalance> InventoryBalances { get; set; }

    // GP-Style Inventory entities (TODO: Create entity classes when implementing GP-style inventory)
    // public DbSet<ItemClass> ItemClasses { get; set; }
    // public DbSet<PriceGroup> PriceGroups { get; set; }
    public DbSet<UnitOfMeasureSchedule> UnitOfMeasureSchedules { get; set; }
    public DbSet<UnitOfMeasureScheduleDetail> UnitOfMeasureScheduleDetails { get; set; }
    // public DbSet<SuggestedSalesItem> SuggestedSalesItems { get; set; }

    // Price List entities
    public DbSet<PriceList> PriceLists { get; set; }
    public DbSet<PriceListLine> PriceListLines { get; set; }
    public DbSet<CustomerGroup> CustomerGroups { get; set; }
    public DbSet<SupplierGroup> SupplierGroups { get; set; }
    public DbSet<PriceListChangeHistory> PriceListChangeHistories { get; set; }

    // Procurement entities
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<SupplierContact> SupplierContacts { get; set; }
    public DbSet<SupplierItemCatalog> SupplierItemCatalogs { get; set; }
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
    public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }
    public DbSet<PurchaseOrderReceipt> PurchaseOrderReceipts { get; set; }
    public DbSet<PurchaseOrderReceiptItem> PurchaseOrderReceiptItems { get; set; }
    public DbSet<PurchaseOrderLandedCostPlan> PurchaseOrderLandedCostPlans { get; set; }
    public DbSet<PurchaseOrderLandedCostPlanItem> PurchaseOrderLandedCostPlanItems { get; set; }
    public DbSet<PurchaseRequisition> PurchaseRequisitions { get; set; }
    public DbSet<ConsignmentSettlement> ConsignmentSettlements { get; set; }

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

    // Tender Management
    public DbSet<Tender> Tenders { get; set; }
    public DbSet<TenderLot> TenderLots { get; set; }
    public DbSet<TenderItem> TenderItems { get; set; }
    public DbSet<TenderDocument> TenderDocuments { get; set; }
    public DbSet<TenderInvitation> TenderInvitations { get; set; }
    public DbSet<TenderBid> TenderBids { get; set; }
    public DbSet<TenderBidLot> TenderBidLots { get; set; }
    public DbSet<TenderBidItem> TenderBidItems { get; set; }
    public DbSet<TenderBidDocument> TenderBidDocuments { get; set; }
    public DbSet<TenderFee> TenderFees { get; set; }
    public DbSet<TenderPayment> TenderPayments { get; set; }
    public DbSet<TenderEvaluator> TenderEvaluators { get; set; }
    public DbSet<TenderEvaluation> TenderEvaluations { get; set; }
    public DbSet<TenderInterview> TenderInterviews { get; set; }
    public DbSet<TenderClarification> TenderClarifications { get; set; }
    public DbSet<TenderRevision> TenderRevisions { get; set; }
    public DbSet<TenderAward> TenderAwards { get; set; }
    public DbSet<ProcurementTenderControl> ProcurementTenderControls { get; set; }
    public DbSet<ProcurementTenderDocumentIssue> ProcurementTenderDocumentIssues { get; set; }
    public DbSet<ProcurementTenderSubmissionReceipt> ProcurementTenderSubmissionReceipts { get; set; }
    public DbSet<ProcurementExceptionalSourcingControl> ProcurementExceptionalSourcingControls { get; set; }
    public DbSet<ProcurementPrequalificationExercise> ProcurementPrequalificationExercises { get; set; }
    public DbSet<ProcurementPrequalificationCriterion> ProcurementPrequalificationCriteria { get; set; }
    public DbSet<ProcurementPrequalificationApplication> ProcurementPrequalificationApplications { get; set; }
    public DbSet<ProcurementPrequalificationScore> ProcurementPrequalificationScores { get; set; }
    public DbSet<ProcurementQualifiedListEntry> ProcurementQualifiedListEntries { get; set; }
    public DbSet<TenderTemplate> TenderTemplates { get; set; }
    public DbSet<TenderViewLog> TenderViewLogs { get; set; }
    public DbSet<EvaluationCriterion> EvaluationCriteria { get; set; }
    public DbSet<EvaluationTemplate> EvaluationTemplates { get; set; }
    public DbSet<EvaluationTemplateCriterion> EvaluationTemplateCriteria { get; set; }
    public DbSet<TenderDocumentType> TenderDocumentTypes { get; set; }
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
    public DbSet<ProcurementRfqReceipt> ProcurementRfqReceipts { get; set; }
    public DbSet<ProcurementRfqOpeningRegister> ProcurementRfqOpeningRegisters { get; set; }
    public DbSet<ProcurementRfqOpeningParticipant> ProcurementRfqOpeningParticipants { get; set; }
    public DbSet<ProcurementRfqOpeningEntry> ProcurementRfqOpeningEntries { get; set; }
    public DbSet<ProcurementRfqEvaluation> ProcurementRfqEvaluations { get; set; }
    public DbSet<ProcurementRfqEvaluationLine> ProcurementRfqEvaluationLines { get; set; }

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
    public DbSet<ProcurementAppSubmission> ProcurementAppSubmissions { get; set; }
    public DbSet<ProcurementSpecificationTemplate> ProcurementSpecificationTemplates { get; set; }
    public DbSet<ProcurementPlanItemSupplier> ProcurementPlanItemSuppliers { get; set; }
    public DbSet<ProcurementBudget> ProcurementBudgets { get; set; }
    public DbSet<ProcurementBudgetAllocation> ProcurementBudgetAllocations { get; set; }
    public DbSet<ProcurementBudgetRevision> ProcurementBudgetRevisions { get; set; }
    public DbSet<ProcurementBudgetCommitment> ProcurementBudgetCommitments { get; set; }
    public DbSet<ProcurementRequisitionAuthorityRoute> ProcurementRequisitionAuthorityRoutes { get; set; }
    public DbSet<ProcurementRequisitionAuthorityRouteStep> ProcurementRequisitionAuthorityRouteSteps { get; set; }
    public DbSet<ProcurementRequisitionSourcingRelease> ProcurementRequisitionSourcingReleases { get; set; }
    public DbSet<ProcurementSourcingCase> ProcurementSourcingCases { get; set; }
    public DbSet<ProcurementSourcingCaseLot> ProcurementSourcingCaseLots { get; set; }
    public DbSet<ProcurementSourcingCaseLotItem> ProcurementSourcingCaseLotItems { get; set; }
    public DbSet<ProcurementSourcingCaseSourceRequest> ProcurementSourcingCaseSourceRequests { get; set; }
    public DbSet<ProcurementSchedule> ProcurementSchedules { get; set; }
    public DbSet<ProcurementCalendarProfile> ProcurementCalendarProfiles { get; set; }
    public DbSet<ProcurementCalendarRule> ProcurementCalendarRules { get; set; }
    public DbSet<ProcurementCalendarOccurrence> ProcurementCalendarOccurrences { get; set; }
    public DbSet<ProcurementCalendarRun> ProcurementCalendarRuns { get; set; }
    public DbSet<MarketAnalysis> MarketAnalyses { get; set; }
    public DbSet<PriceHistory> PriceHistories { get; set; }
    public DbSet<SupplierConsolidation> SupplierConsolidations { get; set; }
    public DbSet<EmergencyProcurementPlan> EmergencyProcurementPlans { get; set; }
    public DbSet<EmergencyProcurementItem> EmergencyProcurementItems { get; set; }
    public DbSet<EmergencySupplier> EmergencySuppliers { get; set; }

    // Procurement Settings
    public DbSet<ProcurementSettings> ProcurementSettings { get; set; }
    public DbSet<ProcurementConfigurationProfile> ProcurementConfigurationProfiles { get; set; }
    public DbSet<ProcurementConfigurationDecision> ProcurementConfigurationDecisions { get; set; }
    public DbSet<ProcurementConfigurationEvidenceLink> ProcurementConfigurationEvidenceLinks { get; set; }
    public DbSet<ProcurementConfigurationRevision> ProcurementConfigurationRevisions { get; set; }
    public DbSet<ProcurementPolicySet> ProcurementPolicySets { get; set; }
    public DbSet<ProcurementPolicyCategoryRule> ProcurementPolicyCategoryRules { get; set; }
    public DbSet<ProcurementPolicyMethodRule> ProcurementPolicyMethodRules { get; set; }
    public DbSet<ProcurementPolicyThresholdRule> ProcurementPolicyThresholdRules { get; set; }
    public DbSet<ProcurementPolicyAuthorityRule> ProcurementPolicyAuthorityRules { get; set; }
    public DbSet<ProcurementPolicyEvidenceRule> ProcurementPolicyEvidenceRules { get; set; }
    public DbSet<ProcurementPolicyExceptionRule> ProcurementPolicyExceptionRules { get; set; }
    public DbSet<ProcurementPolicySodRule> ProcurementPolicySodRules { get; set; }
    public DbSet<ProcurementPolicyRevision> ProcurementPolicyRevisions { get; set; }
    public DbSet<ProcurementResponsibilityAssignment> ProcurementResponsibilityAssignments { get; set; }
    public DbSet<ProcurementResponsibilityWarehouse> ProcurementResponsibilityWarehouses { get; set; }
    public DbSet<ProcurementCommittee> ProcurementCommittees { get; set; }
    public DbSet<ProcurementCommitteeMember> ProcurementCommitteeMembers { get; set; }
    public DbSet<ProcurementControlEvent> ProcurementControlEvents { get; set; }
    public DbSet<ProcurementControlEventEvidenceLink> ProcurementControlEventEvidenceLinks { get; set; }
    public DbSet<ProcurementMasterDataControlPolicy> ProcurementMasterDataControlPolicies { get; set; }
    public DbSet<ProcurementMasterDataChangeRequest> ProcurementMasterDataChangeRequests { get; set; }
    public DbSet<ProcurementMasterDataChangeEvidenceLink> ProcurementMasterDataChangeEvidenceLinks { get; set; }

    // Maintenance Settings
    public DbSet<MaintenanceSettings> MaintenanceSettings { get; set; }

    // Distributed locks (global, non-tenant scoped)
    public DbSet<DistributedLock> DistributedLocks { get; set; }

    // Fleet Management (Maintenance)
    public DbSet<FleetTrip> FleetTrips { get; set; }
    public DbSet<FleetTripDestination> FleetTripDestinations { get; set; }
    public DbSet<FleetComplianceItem> FleetComplianceItems { get; set; }
    public DbSet<FleetComplianceTemplate> FleetComplianceTemplates { get; set; }
    public DbSet<FleetComplianceTemplateItem> FleetComplianceTemplateItems { get; set; }
    public DbSet<FleetVehicleComplianceTemplate> FleetVehicleComplianceTemplates { get; set; }
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
    public DbSet<EhcRootCauseCode> EhcRootCauseCodes { get; set; }
    public DbSet<EhcCannedResponse> EhcCannedResponses { get; set; }
    public DbSet<EhcTicketMessage> EhcTicketMessages { get; set; }
    public DbSet<EhcTicketAttachment> EhcTicketAttachments { get; set; }
    public DbSet<EhcTicketStatusHistory> EhcTicketStatusHistories { get; set; }
    public DbSet<EhcSlaTemplate> EhcSlaTemplates { get; set; }
    public DbSet<EhcWorkflowRoutingRule> EhcWorkflowRoutingRules { get; set; }
    public DbSet<EhcTicketAuditEvent> EhcTicketAuditEvents { get; set; }
    public DbSet<EhcTicketFeedback> EhcTicketFeedbacks { get; set; }
    public DbSet<EhcTicketWatcher> EhcTicketWatchers { get; set; }
    public DbSet<EhcTicketLink> EhcTicketLinks { get; set; }
    public DbSet<EhcProblem> EhcProblems { get; set; }
    public DbSet<EhcProblemTicketLink> EhcProblemTicketLinks { get; set; }
    public DbSet<EhcCapaTask> EhcCapaTasks { get; set; }
    public DbSet<EhcProblemAuditEvent> EhcProblemAuditEvents { get; set; }
    public DbSet<EhcAgentReplyProfile> EhcAgentReplyProfiles { get; set; }
    public DbSet<EhcKnowledgeBaseCategory> EhcKnowledgeBaseCategories { get; set; }
    public DbSet<EhcKnowledgeBaseArticle> EhcKnowledgeBaseArticles { get; set; }
    public DbSet<EhcFaqCategory> EhcFaqCategories { get; set; }
    public DbSet<EhcFaqItem> EhcFaqItems { get; set; }
    public DbSet<EhcTicketPriorityLevel> EhcTicketPriorityLevels { get; set; }
    public DbSet<EhcEscalationPolicy> EhcEscalationPolicies { get; set; }
    public DbSet<EhcEscalationPolicyLevel> EhcEscalationPolicyLevels { get; set; }
    public DbSet<EhcEscalationExecution> EhcEscalationExecutions { get; set; }
    public DbSet<EhcLegalHold> EhcLegalHolds { get; set; }
    public DbSet<EhcRetentionCategoryException> EhcRetentionCategoryExceptions { get; set; }
    public DbSet<EhcComplianceAuditExport> EhcComplianceAuditExports { get; set; }
    public DbSet<EhcServiceRequestType> EhcServiceRequestTypes { get; set; }
    public DbSet<EhcServiceRequest> EhcServiceRequests { get; set; }
    public DbSet<EhcServiceRequestAttachment> EhcServiceRequestAttachments { get; set; }
    public DbSet<EhcServiceRequestAuditEvent> EhcServiceRequestAuditEvents { get; set; }
    public DbSet<EhcInboundEmailChannel> EhcInboundEmailChannels { get; set; }
    public DbSet<EhcInboundEmailMessage> EhcInboundEmailMessages { get; set; }
    public DbSet<EhcInboundEmailWebhookQueueItem> EhcInboundEmailWebhookQueueItems { get; set; }
    public DbSet<EhcInboundMessagingChannel> EhcInboundMessagingChannels { get; set; }
    public DbSet<EhcInboundMessagingMessage> EhcInboundMessagingMessages { get; set; }

    // Estate/DMS integration: Estate acquisition, property assets, and procedure cases are modeled here for shared workflow/DMS links.
    public DbSet<LandAcquisition> LandAcquisitions { get; set; }
    public DbSet<LandAcquisitionDocument> LandAcquisitionDocuments { get; set; }
    public DbSet<LandPhysicalAssessment> LandPhysicalAssessments { get; set; }
    public DbSet<CadastralSurvey> CadastralSurveys { get; set; }
    public DbSet<OwnershipHistory> OwnershipHistories { get; set; }
    public DbSet<NegotiationOffer> NegotiationOffers { get; set; }
    public DbSet<LandAgreement> LandAgreements { get; set; }
    public DbSet<LandInstrument> LandInstruments { get; set; }
    public DbSet<StatutoryConsent> StatutoryConsents { get; set; }
    public DbSet<StampDutyAssessment> StampDutyAssessments { get; set; }
    public DbSet<StampDutyPayment> StampDutyPayments { get; set; }
    public DbSet<LandRegistration> LandRegistrations { get; set; }
    public DbSet<LandAsset> LandAssets { get; set; }
    public DbSet<EstateManagedAsset> EstateManagedAssets { get; set; }
    public DbSet<EstateManagedAssetDocument> EstateManagedAssetDocuments { get; set; }
    public DbSet<LandAcquisitionNote> LandAcquisitionNotes { get; set; }
    public DbSet<LandAcquisitionChecklistResponse> LandAcquisitionChecklistResponses { get; set; }

    // Shared procedure case workspaces
    public DbSet<ProcedureCase> ProcedureCases { get; set; }
    public DbSet<ProcedureCaseField> ProcedureCaseFields { get; set; }
    public DbSet<ProcedureCaseChecklistItem> ProcedureCaseChecklistItems { get; set; }
    public DbSet<ProcedureCaseDocument> ProcedureCaseDocuments { get; set; }
    public DbSet<ProcedureCaseActivity> ProcedureCaseActivities { get; set; }

    // Workflow Engine entities
    public DbSet<WorkflowDefinition> WorkflowDefinitions { get; set; }
    public DbSet<WorkflowStep> WorkflowSteps { get; set; }
    public DbSet<WorkflowTransition> WorkflowTransitions { get; set; }
    public DbSet<WorkflowInstance> WorkflowInstances { get; set; }
    public DbSet<WorkflowStepInstance> WorkflowStepInstances { get; set; }
    public DbSet<WorkflowActivityLog> WorkflowActivityLogs { get; set; }
    public DbSet<WorkflowApproval> WorkflowApprovals { get; set; }
    public DbSet<WorkflowApprovalPolicySet> WorkflowApprovalPolicySets { get; set; }
    public DbSet<WorkflowDelegation> WorkflowDelegations { get; set; }
    public DbSet<WorkflowWorkingCalendar> WorkflowWorkingCalendars { get; set; }
    public DbSet<WorkflowCorrectionRequest> WorkflowCorrectionRequests { get; set; }
    public DbSet<WorkflowEscalationExecution> WorkflowEscalationExecutions { get; set; }
    public DbSet<WorkflowEvidencePolicy> WorkflowEvidencePolicies { get; set; }
    public DbSet<WorkflowEvidenceDocument> WorkflowEvidenceDocuments { get; set; }
    public DbSet<WorkflowSignatureEvidence> WorkflowSignatureEvidence { get; set; }
    public DbSet<WorkflowIntegrationExecution> WorkflowIntegrationExecutions { get; set; }
    public DbSet<WorkflowOfflineAction> WorkflowOfflineActions { get; set; }
    public DbSet<WorkflowEntityType> WorkflowEntityTypes { get; set; }

    // Finance - Common entities
    public DbSet<PaymentTerm> PaymentTerms { get; set; }
    public DbSet<Currency> Currencies { get; set; }

    // Project management entities
    public DbSet<Project> Projects { get; set; }
    public DbSet<ProjectType> ProjectTypes { get; set; }
    public DbSet<ProjectPriority> ProjectPriorities { get; set; }
    public DbSet<ProjectTemplate> ProjectTemplates { get; set; }
    public DbSet<ProjectPhaseTemplate> ProjectPhaseTemplates { get; set; }
    public DbSet<ProjectStageGateRule> ProjectStageGateRules { get; set; }
    public DbSet<ProjectPortfolio> ProjectPortfolios { get; set; }
    public DbSet<ProjectProgram> ProjectPrograms { get; set; }
    public DbSet<ProjectManagementSettings> ProjectManagementSettings { get; set; }
    public DbSet<ProjectCatalogEntry> ProjectCatalogEntries { get; set; }
    public DbSet<ProjectDevelopmentProfile> ProjectDevelopmentProfiles { get; set; }
    public DbSet<ProjectPhase> ProjectPhases { get; set; }
    public DbSet<ProjectPackage> ProjectPackages { get; set; }
    public DbSet<ProjectBoqItem> ProjectBoqItems { get; set; }
    public DbSet<ProjectApprovalRegisterItem> ProjectApprovalRegisterItems { get; set; }
    public DbSet<ProjectDrawing> ProjectDrawings { get; set; }
    public DbSet<ProjectSubmittal> ProjectSubmittals { get; set; }
    public DbSet<ProjectRfi> ProjectRfis { get; set; }
    public DbSet<ProjectSiteInstruction> ProjectSiteInstructions { get; set; }
    public DbSet<ProjectVariationOrder> ProjectVariationOrders { get; set; }
    public DbSet<ProjectInterimValuation> ProjectInterimValuations { get; set; }
    public DbSet<ProjectInterimValuationPackageCompletion> ProjectInterimValuationPackageCompletions { get; set; }
    public DbSet<ProjectPaymentCertificate> ProjectPaymentCertificates { get; set; }
    public DbSet<ProjectExtensionOfTime> ProjectExtensionOfTimeRequests { get; set; }
    public DbSet<ProjectFinalAccount> ProjectFinalAccounts { get; set; }
    public DbSet<ProjectBuilding> ProjectBuildings { get; set; }
    public DbSet<ProjectFloor> ProjectFloors { get; set; }
    public DbSet<ProjectUnitReleaseBatch> ProjectUnitReleaseBatches { get; set; }
    public DbSet<ProjectUnitHandoverBatch> ProjectUnitHandoverBatches { get; set; }
    public DbSet<ProjectUnitTypeTemplate> ProjectUnitTypeTemplates { get; set; }
    public DbSet<ProjectUnitTypeTemplateAmenity> ProjectUnitTypeTemplateAmenities { get; set; }
    public DbSet<ProjectUnit> ProjectUnits { get; set; }
    public DbSet<ProjectUnitAmenity> ProjectUnitAmenities { get; set; }
    public DbSet<ProjectCustomerVariation> ProjectCustomerVariations { get; set; }
    public DbSet<ProjectCommissioningItem> ProjectCommissioningItems { get; set; }
    public DbSet<ProjectHandoverItem> ProjectHandoverItems { get; set; }
    public DbSet<ProjectSnagItem> ProjectSnagItems { get; set; }
    public DbSet<ProjectDefectLiabilityCase> ProjectDefectLiabilityCases { get; set; }
    public DbSet<ProjectInitiationVersion> ProjectInitiationVersions { get; set; }
    public DbSet<ProjectMember> ProjectMembers { get; set; }
    public DbSet<ProjectWorkItem> ProjectWorkItems { get; set; }
    public DbSet<ProjectMilestone> ProjectMilestones { get; set; }
    public DbSet<ProjectMilestonePhase> ProjectMilestonePhases { get; set; }
    public DbSet<ProjectResourceAllocation> ProjectResourceAllocations { get; set; }
    public DbSet<ProjectRisk> ProjectRisks { get; set; }
    public DbSet<ProjectIssue> ProjectIssues { get; set; }
    public DbSet<ProjectQualityCheckpoint> ProjectQualityCheckpoints { get; set; }
    public DbSet<ProjectNonConformance> ProjectNonConformances { get; set; }
    public DbSet<ProjectChangeRequest> ProjectChangeRequests { get; set; }
    public DbSet<ProjectBillingSchedule> ProjectBillingSchedules { get; set; }
    public DbSet<ProjectInvoiceRequest> ProjectInvoiceRequests { get; set; }
    public DbSet<ProjectDeliverable> ProjectDeliverables { get; set; }
    public DbSet<ProjectDeliverableExternalReview> ProjectDeliverableExternalReviews { get; set; }
    public DbSet<ProjectTaskDependency> ProjectTaskDependencies { get; set; }
    public DbSet<ProjectInterdependency> ProjectInterdependencies { get; set; }
    public DbSet<ProjectBaseline> ProjectBaselines { get; set; }
    public DbSet<ProjectTimesheetEntry> ProjectTimesheetEntries { get; set; }
    public DbSet<ProjectExpense> ProjectExpenses { get; set; }
    public DbSet<ProjectMaterialCostEntry> ProjectMaterialCostEntries { get; set; }
    public DbSet<ProjectRevenueRecognition> ProjectRevenueRecognitions { get; set; }
    public DbSet<ProjectBudgetRevision> ProjectBudgetRevisions { get; set; }
    public DbSet<ProjectForecastVersion> ProjectForecastVersions { get; set; }
    public DbSet<ProjectAssetLink> ProjectAssetLinks { get; set; }
    public DbSet<ProjectExternalAccessPolicy> ProjectExternalAccessPolicies { get; set; }
    public DbSet<ProjectDecision> ProjectDecisions { get; set; }
    public DbSet<ProjectMeetingMinute> ProjectMeetingMinutes { get; set; }
    public DbSet<ProjectActionItem> ProjectActionItems { get; set; }
    public DbSet<ProjectLessonLearned> ProjectLessonsLearned { get; set; }
    public DbSet<ProjectDocument> ProjectDocuments { get; set; }
    public DbSet<ProjectComment> ProjectComments { get; set; }
    public DbSet<ProjectClosure> ProjectClosures { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply entity configurations
        builder.ApplyConfiguration(new ApplicationUserConfiguration());
        builder.ApplyConfiguration(new TenantConfiguration());
        builder.ApplyConfiguration(new UserTenantConfiguration());
        builder.Entity<WorkflowEscalationExecution>()
            .HasIndex(item => new { item.ApprovalId, item.RuleIndex })
            .IsUnique();
        builder.Entity<WorkflowEvidenceDocument>().HasIndex(item => new { item.TenantId, item.AttachmentId }).IsUnique();
        builder.Entity<WorkflowSignatureEvidence>().HasIndex(item => item.ApprovalId).IsUnique();
        builder.Entity<WorkflowIntegrationExecution>().HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
        builder.Entity<WorkflowOfflineAction>().HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
        ConfigureCentralDocumentManagementEntities(builder);
        ConfigureLandAcquisitionEntities(builder);
        ConfigureProcedureCaseEntities(builder);
        // Preserve the workflow-delegation indexes introduced by the prior manual migration.
        builder.Entity<WorkflowDelegation>().HasIndex(item => item.WorkflowDefinitionId);
        builder.Entity<WorkflowDelegation>().HasIndex(item => item.WorkflowStepId);
        ConfigureProcurementConfiguration(builder);
        ConfigureProcurementPolicy(builder);
        ConfigureProcurementRequisitionAuthorityRoutes(builder);
        ConfigureProcurementRequisitionSourcingReleases(builder);
        builder.ApplyConfiguration(new ProcurementSourcingCaseConfiguration());
        builder.ApplyConfiguration(new ProcurementSourcingCaseLotConfiguration());
        builder.ApplyConfiguration(new ProcurementSourcingCaseLotItemConfiguration());
        builder.ApplyConfiguration(new ProcurementSourcingCaseSourceRequestConfiguration());
        ConfigureProcurementAccessControl(builder);
        ConfigureProcurementControlEvents(builder);
        ConfigureProcurementMasterDataChanges(builder);
        builder.ApplyConfiguration(new AssetTypeConfiguration());
        builder.ApplyConfiguration(new AssetTypeFieldConfiguration());

        // Asset Condition Inspection configurations
        builder.ApplyConfiguration(new PreInspectionChecklistTemplateConfiguration());
        builder.ApplyConfiguration(new PreInspectionChecklistItemConfiguration());
        builder.ApplyConfiguration(new AssetConditionRecordConfiguration());
        builder.ApplyConfiguration(new AssetConditionItemResultConfiguration());

        // Fleet configurations
        builder.ApplyConfiguration(new FleetTripConfiguration());
        builder.ApplyConfiguration(new FleetComplianceItemConfiguration());
        builder.ApplyConfiguration(new FleetComplianceTemplateConfiguration());
        builder.ApplyConfiguration(new FleetComplianceTemplateItemConfiguration());
        builder.ApplyConfiguration(new FleetVehicleComplianceTemplateConfiguration());
        builder.ApplyConfiguration(new FleetFuelTransactionConfiguration());
        builder.ApplyConfiguration(new FleetVehicleAssignmentConfiguration());
        builder.ApplyConfiguration(new FleetTripInspectionConfiguration());
        builder.ApplyConfiguration(new FleetDefectConfiguration());
        builder.ApplyConfiguration(new FleetIncidentConfiguration());
        builder.ApplyConfiguration(new FleetTyreConfiguration());
        builder.ApplyConfiguration(new FleetTyreEventConfiguration());
        builder.ApplyConfiguration(new FleetBatteryConfiguration());
        builder.ApplyConfiguration(new FleetBatteryEventConfiguration());
        builder.ApplyConfiguration(new FleetExternalRepairConfiguration());
        builder.ApplyConfiguration(new FleetCostEntryConfiguration());

        // Tender configurations
        builder.ApplyConfiguration(new TenderConfiguration());
        builder.ApplyConfiguration(new TenderLotConfiguration());
        builder.ApplyConfiguration(new TenderBidConfiguration());
        builder.ApplyConfiguration(new TenderBidLotConfiguration());
        builder.ApplyConfiguration(new TenderBidItemConfiguration());
        builder.ApplyConfiguration(new TenderNegotiationConfiguration());
        builder.ApplyConfiguration(new TenderNegotiationItemConfiguration());
        builder.ApplyConfiguration(new TenderAwardConfiguration());
        builder.ApplyConfiguration(new EvaluationTemplateConfiguration());
        builder.ApplyConfiguration(new EvaluationTemplateCriterionConfiguration());
        builder.ApplyConfiguration(new ProcurementTenderControlConfiguration());
        builder.ApplyConfiguration(new ProcurementTenderDocumentIssueConfiguration());
        builder.ApplyConfiguration(new ProcurementTenderSubmissionReceiptConfiguration());
        builder.ApplyConfiguration(new ProcurementExceptionalSourcingControlConfiguration());
        builder.ApplyConfiguration(new ProcurementPrequalificationExerciseConfiguration());
        builder.ApplyConfiguration(new ProcurementPrequalificationCriterionConfiguration());
        builder.ApplyConfiguration(new ProcurementPrequalificationApplicationConfiguration());
        builder.ApplyConfiguration(new ProcurementPrequalificationScoreConfiguration());
        builder.ApplyConfiguration(new ProcurementQualifiedListEntryConfiguration());

        // RFQ configurations (separate from formal tenders)
        builder.ApplyConfiguration(new RequestForQuotationConfiguration());
        builder.ApplyConfiguration(new RequestForQuotationItemConfiguration());
        builder.ApplyConfiguration(new RequestForQuotationInvitationConfiguration());
        builder.ApplyConfiguration(new RequestForQuotationQuoteConfiguration());
        builder.ApplyConfiguration(new RequestForQuotationQuoteItemConfiguration());
        builder.ApplyConfiguration(new RequestForQuotationAwardLineConfiguration());
        builder.ApplyConfiguration(new ProcurementRfqReceiptConfiguration());
        builder.ApplyConfiguration(new ProcurementRfqOpeningRegisterConfiguration());
        builder.ApplyConfiguration(new ProcurementRfqOpeningParticipantConfiguration());
        builder.ApplyConfiguration(new ProcurementRfqOpeningEntryConfiguration());
        builder.ApplyConfiguration(new ProcurementRfqEvaluationConfiguration());
        builder.ApplyConfiguration(new ProcurementRfqEvaluationLineConfiguration());
        builder.ApplyConfiguration(new EmailCampaignConfiguration());
        builder.ApplyConfiguration(new EmailCampaignRecipientConfiguration());
        builder.ApplyConfiguration(new SystemExceptionLogConfiguration());
        builder.ApplyConfiguration(new DistributedLockConfiguration());
        builder.ApplyConfiguration(new NotificationTopicConfiguration());
        builder.ApplyConfiguration(new NotificationTopicRecipientConfiguration());

        // â”€â”€â”€ CRM Entity FK Configurations (prevent cascade cycles) â”€â”€â”€
        // Finance unit-accounting/statistical ledger configurations.
        builder.ApplyConfiguration(new UnitTypeConfiguration());
        builder.ApplyConfiguration(new UnitAccountConfiguration());
        builder.ApplyConfiguration(new UnitJournalEntryConfiguration());
        builder.ApplyConfiguration(new UnitJournalEntryLineConfiguration());
        builder.ApplyConfiguration(new UnitAccountBalanceConfiguration());
        builder.ApplyConfiguration(new UnitAccountBudgetConfiguration());
        builder.ApplyConfiguration(new RatioDefinitionConfiguration());
        builder.ApplyConfiguration(new AllocationRuleConfiguration());
        builder.ApplyConfiguration(new AllocationTargetConfiguration());

        builder.Entity<DocumentSequenceDefinition>(entity =>
        {
            entity.ToTable("DocumentSequenceDefinitions");
            entity.HasIndex(e => new { e.TenantId, e.Module, e.DocumentType, e.Name }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.Module, e.DocumentType, e.IsActive, e.IsDefault });
            entity.Property(e => e.Module).HasMaxLength(50).IsRequired();
            entity.Property(e => e.DocumentType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Format).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ResetPolicy).HasMaxLength(20).IsRequired();
            entity.HasMany(e => e.Reservations)
                .WithOne(e => e.DocumentSequenceDefinition)
                .HasForeignKey(e => e.DocumentSequenceDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DocumentNumberReservation>(entity =>
        {
            entity.ToTable("DocumentNumberReservations");
            entity.HasIndex(e => new { e.TenantId, e.Module, e.DocumentType, e.DocumentNumber }).IsUnique();
            entity.HasIndex(e => e.DocumentSequenceDefinitionId);
            entity.Property(e => e.Module).HasMaxLength(50).IsRequired();
            entity.Property(e => e.DocumentType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.DocumentNumber).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PeriodKey).HasMaxLength(20);
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
        });

        // ─── CRM Entity FK Configurations (prevent cascade cycles) ───

        builder.Entity<Lead>(entity =>
        {
            entity.ToTable("Leads");
            entity.HasOne(e => e.AssignedTo)
                .WithMany()
                .HasForeignKey(e => e.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ConvertedCustomer)
                .WithMany()
                .HasForeignKey(e => e.ConvertedCustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Opportunity>(entity =>
        {
            entity.ToTable("Opportunities");
            entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Lead)
                .WithMany(l => l.Opportunities)
                .HasForeignKey(e => e.LeadId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AssignedTo)
                .WithMany()
                .HasForeignKey(e => e.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Quote>(entity =>
        {
            entity.ToTable("Quotes");
            entity.HasOne(e => e.Opportunity)
                .WithMany(o => o.Quotes)
                .HasForeignKey(e => e.OpportunityId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ConvertedInvoice)
                .WithMany()
                .HasForeignKey(e => e.ConvertedInvoiceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<QuoteLineItem>(entity =>
        {
            entity.ToTable("QuoteLineItems");
            entity.HasOne(e => e.Quote)
                .WithMany(q => q.LineItems)
                .HasForeignKey(e => e.QuoteId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            // Ignore computed property
            entity.Ignore(e => e.LineTotal);
        });

        builder.Entity<Invoice>(entity =>
        {
            entity.ToTable("Invoices");
            entity.Property(e => e.InvoiceNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.CustomerName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.CustomerAddress).HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.Reference).HasMaxLength(100);
            entity.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(e => e.ExchangeRate).HasColumnType("decimal(18,4)");
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.PaidAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.CreditedAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.BaseCurrencyAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.EarlyPaymentDiscountPercentage).HasColumnType("decimal(18,4)");
            entity.Property(e => e.EarlyPaymentDiscountAmount).HasColumnType("decimal(18,2)");
            entity.Ignore(e => e.CustomerId);
            entity.Ignore(e => e.BalanceAmount);
            entity.HasOne(e => e.BusinessPartner)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.TaxGroup)
                .WithMany()
                .HasForeignKey(e => e.TaxGroupId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PaymentTerm)
                .WithMany()
                .HasForeignKey(e => e.PaymentTermId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CustomerPayment>(entity =>
        {
            entity.ToTable("CustomerPayment");
            entity.Ignore(e => e.Customer);
            entity.HasOne<BusinessPartner>()
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.BankAccount)
                .WithMany()
                .HasForeignKey(e => e.BankAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ConfiguredPaymentMethod)
                .WithMany()
                .HasForeignKey(e => e.PaymentMethodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.WithholdingTax)
                .WithMany()
                .HasForeignKey(e => e.WithholdingTaxId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.WithholdingTaxAccount)
                .WithMany()
                .HasForeignKey(e => e.WithholdingTaxAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.VatWithholdingTax)
                .WithMany()
                .HasForeignKey(e => e.VatWithholdingTaxId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.VatWithholdingAccount)
                .WithMany()
                .HasForeignKey(e => e.VatWithholdingAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(e => e.WithholdingTaxAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.VatWithholdingAmount).HasColumnType("decimal(18,2)");
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TenantId, e.PaymentMethodId });
        });

        builder.Entity<PaymentAllocation>(entity =>
        {
            // Advance applications are their own posted reclassification, not an edit to the receipt.
            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(e => e.ApplicationJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinancePostingEvent>()
                .WithMany()
                .HasForeignKey(e => e.ApplicationPostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TenantId, e.ApplicationPostingEventId });
        });

        builder.Entity<InvoiceLineItem>(entity =>
        {
            entity.ToTable("InvoiceLineItem");
            entity.Property(e => e.Description).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Quantity).HasColumnType("decimal(18,4)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
            entity.Property(e => e.UnitCost).HasColumnType("decimal(18,2)");
            entity.Property(e => e.CostTotal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TaxRate).HasColumnType("decimal(18,4)");
            entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TaxTreatment).HasDefaultValue(TaxTreatment.Standard);
            entity.Property(e => e.DiscountPercentage).HasColumnType("decimal(18,4)");
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");
            entity.Ignore(e => e.LineTotal);
            entity.HasOne(e => e.Invoice)
                .WithMany(i => i.LineItems)
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.GLAccount)
                .WithMany()
                .HasForeignKey(e => e.GLAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.InventoryItem)
                .WithMany()
                .HasForeignKey(e => e.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Warehouse)
                .WithMany()
                .HasForeignKey(e => e.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Location)
                .WithMany()
                .HasForeignKey(e => e.LocationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.TaxGroup)
                .WithMany()
                .HasForeignKey(e => e.TaxGroupId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Customer>(entity =>
        {
            entity.Ignore(e => e.Invoices);
            entity.Ignore(e => e.Payments);
        });

        builder.Entity<Activity>(entity =>
        {
            entity.ToTable("CrmActivities");
            entity.HasOne(e => e.AssignedTo)
                .WithMany()
                .HasForeignKey(e => e.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Lead)
                .WithMany(l => l.Activities)
                .HasForeignKey(e => e.LeadId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Opportunity)
                .WithMany(o => o.Activities)
                .HasForeignKey(e => e.OpportunityId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Campaign>(entity =>
        {
            entity.ToTable("Campaigns");
            entity.Ignore(e => e.ResponseRate);
            entity.HasOne(e => e.Manager)
                .WithMany()
                .HasForeignKey(e => e.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CampaignMember>(entity =>
        {
            entity.ToTable("CampaignMembers");
            entity.HasOne(e => e.Campaign)
                .WithMany(c => c.CampaignMembers)
                .HasForeignKey(e => e.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Lead)
                .WithMany()
                .HasForeignKey(e => e.LeadId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Product>(entity =>
        {
            entity.ToTable("SalesProducts");
            entity.Ignore(e => e.Margin);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SalesOrder>(entity =>
        {
            entity.ToTable("SalesOrders");
            entity.HasOne(e => e.BusinessPartner)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Quote)
                .WithMany()
                .HasForeignKey(e => e.QuoteId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Opportunity)
                .WithMany()
                .HasForeignKey(e => e.OpportunityId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.SalesRep)
                .WithMany()
                .HasForeignKey(e => e.SalesRepId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PaymentTerm)
                .WithMany()
                .HasForeignKey(e => e.PaymentTermId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Warehouse)
                .WithMany()
                .HasForeignKey(e => e.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Invoice)
                .WithMany()
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SalesOrderLine>(entity =>
        {
            entity.ToTable("SalesOrderLines");
            entity.HasOne(e => e.SalesOrder)
                .WithMany(e => e.Lines)
                .HasForeignKey(e => e.SalesOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.InventoryItem)
                .WithMany()
                .HasForeignKey(e => e.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Warehouse)
                .WithMany()
                .HasForeignKey(e => e.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Location)
                .WithMany()
                .HasForeignKey(e => e.LocationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.GLAccount)
                .WithMany()
                .HasForeignKey(e => e.GLAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DeliveryNote>(entity =>
        {
            entity.ToTable("DeliveryNotes");
            entity.HasOne(e => e.SalesOrder)
                .WithMany(e => e.DeliveryNotes)
                .HasForeignKey(e => e.SalesOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.BusinessPartner)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Warehouse)
                .WithMany()
                .HasForeignKey(e => e.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PackedBy)
                .WithMany()
                .HasForeignKey(e => e.PackedById)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ShippedBy)
                .WithMany()
                .HasForeignKey(e => e.ShippedById)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReceivedBy)
                .WithMany()
                .HasForeignKey(e => e.ReceivedById)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<DeliveryNoteLine>(entity =>
        {
            entity.ToTable("DeliveryNoteLines");
            entity.HasOne(e => e.DeliveryNote)
                .WithMany(e => e.Lines)
                .HasForeignKey(e => e.DeliveryNoteId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.SalesOrderLine)
                .WithMany()
                .HasForeignKey(e => e.SalesOrderLineId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.InventoryItem)
                .WithMany()
                .HasForeignKey(e => e.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Warehouse)
                .WithMany()
                .HasForeignKey(e => e.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Location)
                .WithMany()
                .HasForeignKey(e => e.LocationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // â”€â”€â”€ Return Order / Credit Note / Refund FK Configurations â”€â”€â”€

        builder.Entity<ReturnOrder>(entity =>
        {
            entity.ToTable("ReturnOrders");
            entity.HasOne(e => e.SalesOrder)
                .WithMany()
                .HasForeignKey(e => e.SalesOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.DeliveryNote)
                .WithMany()
                .HasForeignKey(e => e.DeliveryNoteId)
                .OnDelete(DeleteBehavior.Restrict);
            // Return/credit/refund documents feed Finance AR, so their counterparty is the canonical
            // tenant-scoped BusinessPartner rather than the legacy CRM/Sales Customer record.
            entity.HasOne(e => e.BusinessPartner)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.InspectedBy)
                .WithMany()
                .HasForeignKey(e => e.InspectedById)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CreditNote)
                .WithMany()
                .HasForeignKey(e => e.CreditNoteId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Refund)
                .WithMany()
                .HasForeignKey(e => e.RefundId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReturnOrderLine>(entity =>
        {
            entity.ToTable("ReturnOrderLines");
            entity.HasOne(e => e.ReturnOrder)
                .WithMany(r => r.Lines)
                .HasForeignKey(e => e.ReturnOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.SalesOrderLine)
                .WithMany()
                .HasForeignKey(e => e.SalesOrderLineId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(e => e.LineTotal);
        });

        builder.Entity<CreditNote>(entity =>
        {
            entity.ToTable("CreditNotes");
            entity.HasOne(e => e.BusinessPartner)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReturnOrder)
                .WithMany()
                .HasForeignKey(e => e.ReturnOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.OriginalInvoice)
                .WithMany()
                .HasForeignKey(e => e.OriginalInvoiceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(e => e.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(e => e.ReversalJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinancePostingEvent>()
                .WithMany()
                .HasForeignKey(e => e.ReversalPostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TenantId, e.JournalEntryId });
            entity.HasIndex(e => new { e.TenantId, e.ReversalJournalEntryId });
            entity.HasIndex(e => new { e.TenantId, e.ReversalPostingEventId });
        });

        builder.Entity<CreditNoteLine>(entity =>
        {
            entity.ToTable("CreditNoteLines");
            entity.HasOne(e => e.CreditNote)
                .WithMany(c => c.Lines)
                .HasForeignKey(e => e.CreditNoteId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(e => e.LineTotal);
        });

        builder.Entity<Refund>(entity =>
        {
            entity.ToTable("Refunds");
            entity.HasOne(e => e.BusinessPartner)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CreditNote)
                .WithMany()
                .HasForeignKey(e => e.CreditNoteId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReturnOrder)
                .WithMany()
                .HasForeignKey(e => e.ReturnOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ProcessedBy)
                .WithMany()
                .HasForeignKey(e => e.ProcessedById)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // â”€â”€â”€ Collections & Debt Management FK Configurations â”€â”€â”€

        builder.Entity<CollectionActivity>(entity =>
        {
            entity.ToTable("CollectionActivities");
            entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Invoice)
                .WithMany()
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AssignedTo)
                .WithMany()
                .HasForeignKey(e => e.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PaymentPlan>(entity =>
        {
            entity.ToTable("PaymentPlans");
            entity.Ignore(e => e.RemainingBalance);
            entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ApprovedBy)
                .WithMany()
                .HasForeignKey(e => e.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PaymentPlanInstallment>(entity =>
        {
            entity.ToTable("PaymentPlanInstallments");
            entity.Ignore(e => e.Balance);
            entity.HasOne(e => e.PaymentPlan)
                .WithMany(p => p.Installments)
                .HasForeignKey(e => e.PaymentPlanId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // â”€â”€â”€ Accounts Payable FK Configurations â”€â”€â”€

        builder.Entity<VendorInvoice>(entity =>
        {
            entity.ToTable("VendorInvoice");
            entity.HasOne(e => e.Supplier)
                .WithMany()
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PurchaseOrder)
                .WithMany()
                .HasForeignKey(e => e.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PaymentTerm)
                .WithMany()
                .HasForeignKey(e => e.PaymentTermId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ExpenseAccount)
                .WithMany()
                .HasForeignKey(e => e.ExpenseAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ApAccount)
                .WithMany()
                .HasForeignKey(e => e.ApAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.WithholdingTax)
                .WithMany()
                .HasForeignKey(e => e.WithholdingTaxId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.WithholdingTaxAccount)
                .WithMany()
                .HasForeignKey(e => e.WithholdingTaxAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VendorInvoiceLineItem>(entity =>
        {
            entity.ToTable("VendorInvoiceLineItem");
            entity.HasOne(e => e.VendorInvoice)
                .WithMany(i => i.LineItems)
                .HasForeignKey(e => e.VendorInvoiceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.GLAccount)
                .WithMany()
                .HasForeignKey(e => e.GLAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FixedAsset)
                .WithMany()
                .HasForeignKey(e => e.FixedAssetId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TenantId, e.FixedAssetId });
            entity.HasIndex(e => new { e.TenantId, e.CapitalizationPostingEventId });
            entity.HasOne(e => e.PurchaseOrderItem)
                .WithMany()
                .HasForeignKey(e => e.PurchaseOrderItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.InventoryItem)
                .WithMany()
                .HasForeignKey(e => e.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Warehouse)
                .WithMany()
                .HasForeignKey(e => e.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Location)
                .WithMany()
                .HasForeignKey(e => e.LocationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(e => e.TaxTreatment).HasDefaultValue(TaxTreatment.Standard);
            entity.HasOne(e => e.TaxGroup)
                .WithMany()
                .HasForeignKey(e => e.TaxGroupId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinancePurchaseOrder>(entity =>
        {
            entity.ToTable("FinancePurchaseOrders");
            entity.Property(e => e.OrderNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(e => e.ExchangeRate).HasColumnType("decimal(18,4)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Remarks).HasMaxLength(500);

            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.VendorId);
            entity.HasIndex(e => e.TaxGroupId);
            entity.HasIndex(e => e.PaymentTermId);

            entity.HasOne(e => e.Vendor)
                .WithMany()
                .HasForeignKey(e => e.VendorId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.PaymentTerm)
                .WithMany()
                .HasForeignKey(e => e.PaymentTermId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.TaxGroup)
                .WithMany()
                .HasForeignKey(e => e.TaxGroupId);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinancePurchaseOrderItem>(entity =>
        {
            entity.ToTable("FinancePurchaseOrderItems");
            entity.Property(e => e.Description).HasMaxLength(500).IsRequired();
            entity.Property(e => e.OrderedQuantity).HasColumnType("decimal(18,4)");
            entity.Property(e => e.ReceivedQuantity).HasColumnType("decimal(18,4)");
            entity.Property(e => e.InvoicedQuantity).HasColumnType("decimal(18,4)");
            entity.Property(e => e.CancelledQuantity).HasColumnType("decimal(18,4)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
            entity.Property(e => e.CurrencyCode).HasMaxLength(3);
            entity.Property(e => e.ExchangeRate).HasColumnType("decimal(18,4)");
            entity.Property(e => e.TaxCode).HasMaxLength(50);
            entity.Property(e => e.TaxRate).HasColumnType("decimal(18,4)");
            entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.DiscountPercentage).HasColumnType("decimal(18,4)");
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.LineTotal).HasColumnType("decimal(18,2)");

            entity.HasIndex(e => e.FinancePurchaseOrderId);
            entity.HasIndex(e => e.GlAccountId);
            entity.HasIndex(e => e.InventoryItemId);
            entity.HasIndex(e => e.TaxGroupId);
            entity.HasIndex(e => e.TenantId);

            entity.HasOne(e => e.FinancePurchaseOrder)
                .WithMany(e => e.Items)
                .HasForeignKey(e => e.FinancePurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.GlAccount)
                .WithMany()
                .HasForeignKey(e => e.GlAccountId);
            entity.HasOne(e => e.InventoryItem)
                .WithMany()
                .HasForeignKey(e => e.InventoryItemId);
            entity.HasOne(e => e.TaxGroup)
                .WithMany()
                .HasForeignKey(e => e.TaxGroupId);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinancePurchaseOrderReceipt>(entity =>
        {
            entity.ToTable("FinancePurchaseOrderReceipts");
            entity.Property(e => e.ReceiptNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Remarks).HasMaxLength(500);

            entity.HasIndex(e => e.FinancePurchaseOrderId);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.VendorInvoiceId);

            entity.HasOne(e => e.FinancePurchaseOrder)
                .WithMany(e => e.Receipts)
                .HasForeignKey(e => e.FinancePurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.VendorInvoice)
                .WithMany()
                .HasForeignKey(e => e.VendorInvoiceId);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinancePurchaseOrderReceiptItem>(entity =>
        {
            entity.ToTable("FinancePurchaseOrderReceiptItems");
            entity.Property(e => e.QuantityReceived).HasColumnType("decimal(18,4)");
            entity.Property(e => e.InvoicedQuantity).HasColumnType("decimal(18,4)");
            entity.Property(e => e.DiscountPercentage).HasColumnType("decimal(18,4)");
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");

            entity.HasIndex(e => e.FinancePurchaseOrderItemId);
            entity.HasIndex(e => e.FinancePurchaseOrderReceiptId);
            entity.HasIndex(e => e.TenantId);

            entity.HasOne(e => e.FinancePurchaseOrderItem)
                .WithMany()
                .HasForeignKey(e => e.FinancePurchaseOrderItemId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.FinancePurchaseOrderReceipt)
                .WithMany(e => e.Items)
                .HasForeignKey(e => e.FinancePurchaseOrderReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SupplierReturn>(entity =>
        {
            entity.ToTable("SupplierReturns");
            entity.Property(e => e.ReturnNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.VendorName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(e => e.ExchangeRate).HasColumnType("decimal(18,4)");
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.BaseCurrencyAmount).HasColumnType("decimal(18,2)");
            entity.HasOne(e => e.Vendor)
                .WithMany()
                .HasForeignKey(e => e.VendorId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.OriginalVendorInvoice)
                .WithMany()
                .HasForeignKey(e => e.OriginalVendorInvoiceId);
            entity.HasOne(e => e.OriginalFinancePurchaseOrderReceipt)
                .WithMany()
                .HasForeignKey(e => e.OriginalFinancePurchaseOrderReceiptId);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SupplierReturnLineItem>(entity =>
        {
            entity.ToTable("SupplierReturnLineItems");
            entity.Property(e => e.Description).HasMaxLength(500).IsRequired();
            entity.Property(e => e.QuantityReturned).HasColumnType("decimal(18,4)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TaxRate).HasColumnType("decimal(18,4)");
            entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.DiscountPercentage).HasColumnType("decimal(18,4)");
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.LineTotal).HasColumnType("decimal(18,2)");
            entity.HasOne(e => e.SupplierReturn)
                .WithMany(r => r.LineItems)
                .HasForeignKey(e => e.SupplierReturnId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SupplierDebitNote>(entity =>
        {
            entity.ToTable("SupplierDebitNotes");
            entity.Property(e => e.DebitNoteNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(e => e.ExchangeRate).HasColumnType("decimal(18,4)");
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.BaseCurrencyAmount).HasColumnType("decimal(18,2)");
            entity.HasOne(e => e.Vendor)
                .WithMany()
                .HasForeignKey(e => e.VendorId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.SupplierReturn)
                .WithMany()
                .HasForeignKey(e => e.SupplierReturnId);
            entity.HasOne(e => e.OriginalVendorInvoice)
                .WithMany()
                .HasForeignKey(e => e.OriginalVendorInvoiceId);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SupplierDebitNoteLineItem>(entity =>
        {
            entity.ToTable("SupplierDebitNoteLineItems");
            entity.Property(e => e.Description).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Quantity).HasColumnType("decimal(18,4)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TaxRate).HasColumnType("decimal(18,4)");
            entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.DiscountPercentage).HasColumnType("decimal(18,4)");
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.LineTotal).HasColumnType("decimal(18,2)");
            entity.HasOne(e => e.SupplierDebitNote)
                .WithMany(d => d.LineItems)
                .HasForeignKey(e => e.SupplierDebitNoteId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VendorPayment>(entity =>
        {
            entity.ToTable("VendorPayment");
            entity.HasOne(e => e.Supplier)
                .WithMany()
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.BankAccount)
                .WithMany()
                .HasForeignKey(e => e.BankAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ConfiguredPaymentMethod)
                .WithMany()
                .HasForeignKey(e => e.PaymentMethodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PaymentBatch)
                .WithMany()
                .HasForeignKey(e => e.PaymentBatchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.WithholdingTax)
                .WithMany()
                .HasForeignKey(e => e.WithholdingTaxId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.WithholdingTaxAccount)
                .WithMany()
                .HasForeignKey(e => e.WithholdingTaxAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TenantId, e.PaymentMethodId });
        });

        builder.Entity<VendorPaymentAllocation>(entity =>
        {
            entity.ToTable("VendorPaymentAllocation");
            entity.HasOne(e => e.VendorPayment)
                .WithMany(p => p.Allocations)
                .HasForeignKey(e => e.VendorPaymentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.VendorInvoice)
                .WithMany(i => i.PaymentAllocations)
                .HasForeignKey(e => e.VendorInvoiceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(e => e.ApplicationJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinancePostingEvent>()
                .WithMany()
                .HasForeignKey(e => e.ApplicationPostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TenantId, e.ApplicationPostingEventId });
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PaymentBatch>(entity =>
        {
            entity.ToTable("PaymentBatch");
            entity.HasOne(e => e.ConfiguredPaymentMethod)
                .WithMany()
                .HasForeignKey(e => e.PaymentMethodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TenantId, e.PaymentMethodId });
        });

        builder.Entity<PaymentBatchItem>(entity =>
        {
            entity.ToTable("PaymentBatchItem");
            entity.HasOne(e => e.PaymentBatch)
                .WithMany(b => b.Items)
                .HasForeignKey(e => e.PaymentBatchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.VendorPayment)
                .WithMany()
                .HasForeignKey(e => e.VendorPaymentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // â”€â”€â”€ General Ledger FK Configurations â”€â”€â”€

        builder.Entity<FiscalYear>(entity =>
        {
            entity.ToTable("FiscalYears");
            entity.HasOne(e => e.NextFiscalYear)
                .WithOne(e => e.PreviousFiscalYear)
                .HasForeignKey<FiscalYear>(e => e.NextFiscalYearId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ClosingJournalEntry)
                .WithMany()
                .HasForeignKey(e => e.ClosingJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.OpeningBalanceJournalEntry)
                .WithMany()
                .HasForeignKey(e => e.OpeningBalanceJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FiscalPeriod>(entity =>
        {
            entity.ToTable("FiscalPeriods");
            entity.HasOne(e => e.FiscalYear)
                .WithMany(y => y.FiscalPeriods)
                .HasForeignKey(e => e.FiscalYearId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccountingBook>(entity =>
        {
            entity.ToTable("AccountingBooks");
            entity.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
            entity.Property(e => e.Code).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Purpose).HasMaxLength(50);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccountAccountingBook>(entity =>
        {
            entity.ToTable("AccountAccountingBooks");
            entity.HasIndex(e => new { e.TenantId, e.AccountId, e.AccountingBookId }).IsUnique();
            entity.Property(e => e.FinancialStatementLineItem).HasMaxLength(100);
            entity.HasOne(e => e.Account)
                .WithMany(a => a.AccountingBooks)
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AccountingBook)
                .WithMany(book => book.AccountMappings)
                .HasForeignKey(e => e.AccountingBookId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JournalEntry>(entity =>
        {
            entity.ToTable("JournalEntries");
            entity.HasOne(e => e.FiscalPeriod)
                .WithMany(p => p.JournalEntries)
                .HasForeignKey(e => e.FiscalPeriodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReversalJournalEntry)
                .WithMany()
                .HasForeignKey(e => e.ReversalJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.OriginalJournalEntry)
                .WithMany()
                .HasForeignKey(e => e.OriginalJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.RecurringTemplate)
                .WithMany()
                .HasForeignKey(e => e.RecurringTemplateId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinancePostingEvent>(entity =>
        {
            entity.ToTable("FinancePostingEvents");
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.SourceModule, e.SourceDocumentType, e.SourceDocumentId, e.PostingAction })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(e => new { e.TenantId, e.SourceDocumentType, e.SourceDocumentId, e.PostingAction })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [IdempotencyKey] IS NOT NULL");
            entity.HasIndex(e => e.JournalEntryId);
            entity.HasIndex(e => e.PrimaryExchangeRateId);
            entity.HasIndex(e => new { e.TenantId, e.HasForeignCurrencyLines });
            entity.Property(e => e.PrimaryExchangeRate).HasColumnType("decimal(18,6)");
            entity.HasOne(e => e.JournalEntry)
                .WithMany()
                .HasForeignKey(e => e.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PrimaryExchangeRateRecord)
                .WithMany()
                .HasForeignKey(e => e.PrimaryExchangeRateId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccountTransaction>(entity =>
        {
            entity.ToTable("AccountTransactions");
            entity.Property(e => e.FunctionalCurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(e => e.TransactionDebitAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TransactionCreditAmount).HasColumnType("decimal(18,2)");
            entity.HasIndex(e => e.ExchangeRateId);
            entity.HasIndex(e => new { e.TenantId, e.TransactionCurrency, e.ExchangeRateId });
            entity.HasOne(e => e.Account)
                .WithMany(a => a.Transactions)
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ExchangeRateRecord)
                .WithMany()
                .HasForeignKey(e => e.ExchangeRateId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.JournalEntry)
                .WithMany(j => j.Transactions)
                .HasForeignKey(e => e.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FiscalPeriod)
                .WithMany(p => p.Transactions)
                .HasForeignKey(e => e.FiscalPeriodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReversalTransaction)
                .WithMany()
                .HasForeignKey(e => e.ReversalTransactionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.OriginalTransaction)
                .WithMany()
                .HasForeignKey(e => e.OriginalTransactionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FxRealizedSettlement>(entity =>
        {
            entity.ToTable("FxRealizedSettlements");
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.SettlementDocumentType, e.SettlementDocumentId, e.SettlementAllocationId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(e => e.JournalEntryId);
            entity.HasIndex(e => e.PostingEventId);
            entity.HasOne(e => e.ControlAccount)
                .WithMany()
                .HasForeignKey(e => e.ControlAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.GainLossAccount)
                .WithMany()
                .HasForeignKey(e => e.GainLossAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.JournalEntry)
                .WithMany()
                .HasForeignKey(e => e.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PostingEvent)
                .WithMany()
                .HasForeignKey(e => e.PostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FxRevaluationBatch>(entity =>
        {
            entity.ToTable("FxRevaluationBatches");
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.Scope, e.RevaluationDate, e.FiscalPeriodId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(e => e.JournalEntryId);
            entity.HasIndex(e => e.PostingEventId);
            entity.HasOne(e => e.FiscalPeriod)
                .WithMany()
                .HasForeignKey(e => e.FiscalPeriodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.JournalEntry)
                .WithMany()
                .HasForeignKey(e => e.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PostingEvent)
                .WithMany()
                .HasForeignKey(e => e.PostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReversalJournalEntry)
                .WithMany()
                .HasForeignKey(e => e.ReversalJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReversalPostingEvent)
                .WithMany()
                .HasForeignKey(e => e.ReversalPostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FxRevaluationLine>(entity =>
        {
            entity.ToTable("FxRevaluationLines");
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.FxRevaluationBatchId);
            entity.HasIndex(e => new { e.TenantId, e.AccountId, e.TransactionCurrency });
            entity.HasIndex(e => e.JournalEntryId);
            entity.HasIndex(e => e.PostingEventId);
            entity.HasOne(e => e.Batch)
                .WithMany(b => b.Lines)
                .HasForeignKey(e => e.FxRevaluationBatchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Account)
                .WithMany()
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ClosingExchangeRateRecord)
                .WithMany()
                .HasForeignKey(e => e.ClosingExchangeRateId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.GainLossAccount)
                .WithMany()
                .HasForeignKey(e => e.GainLossAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.JournalEntry)
                .WithMany()
                .HasForeignKey(e => e.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PostingEvent)
                .WithMany()
                .HasForeignKey(e => e.PostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SubledgerSettlementBalance>(entity =>
        {
            entity.ToTable("SubledgerSettlementBalances");
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.SourceModule, e.SourceDocumentType, e.SourceDocumentId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(e => new { e.TenantId, e.SourceModule, e.CounterpartyId, e.DueDate });
            entity.HasIndex(e => new { e.TenantId, e.SourcePostingEventId });
            entity.HasIndex(e => new { e.TenantId, e.SourceJournalEntryId });
            entity.Property(e => e.SourceModule).HasMaxLength(10).IsRequired();
            entity.Property(e => e.SourceDocumentType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.SourceDocumentNumber).HasMaxLength(100).IsRequired();
            entity.Property(e => e.DocumentCurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(e => e.FunctionalCurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(e => e.SettlementStatus).HasMaxLength(30).IsRequired();
            entity.Property(e => e.DiagnosticFlags).HasMaxLength(1000);
            entity.HasOne(e => e.SourcePostingEvent)
                .WithMany()
                .HasForeignKey(e => e.SourcePostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.SourceJournalEntry)
                .WithMany()
                .HasForeignKey(e => e.SourceJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SubledgerSettlementApplication>(entity =>
        {
            entity.ToTable("SubledgerSettlementApplications");
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.SourceModule, e.SourceDocumentType, e.SourceDocumentId });
            entity.HasIndex(e => new { e.TenantId, e.SettlementSourceType, e.SettlementSourceId, e.SettlementAllocationId });
            entity.HasIndex(e => new { e.TenantId, e.SettlementPostingEventId });
            entity.HasIndex(e => new { e.TenantId, e.SettlementJournalEntryId });
            entity.HasIndex(e => e.SubledgerSettlementBalanceId);
            entity.Property(e => e.SourceModule).HasMaxLength(10).IsRequired();
            entity.Property(e => e.SourceDocumentType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.SettlementSourceType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.DocumentCurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(e => e.FunctionalCurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.HasOne(e => e.Balance)
                .WithMany(e => e.Applications)
                .HasForeignKey(e => e.SubledgerSettlementBalanceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.SettlementPostingEvent)
                .WithMany()
                .HasForeignKey(e => e.SettlementPostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.SettlementJournalEntry)
                .WithMany()
                .HasForeignKey(e => e.SettlementJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FxRealizedSettlement)
                .WithMany()
                .HasForeignKey(e => e.FxRealizedSettlementId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SubledgerUnappliedSettlementBalance>(entity =>
        {
            entity.ToTable("SubledgerUnappliedSettlementBalances");
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.SourceModule, e.SettlementSourceType, e.SettlementSourceId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(e => new { e.TenantId, e.SourceModule, e.CounterpartyId, e.SettlementDate });
            entity.HasIndex(e => new { e.TenantId, e.SettlementPostingEventId });
            entity.HasIndex(e => new { e.TenantId, e.SettlementJournalEntryId });
            entity.Property(e => e.SourceModule).HasMaxLength(10).IsRequired();
            entity.Property(e => e.SettlementSourceType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.SettlementSourceNumber).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Classification).HasMaxLength(50).IsRequired();
            entity.Property(e => e.DocumentCurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(e => e.FunctionalCurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(e => e.DiagnosticFlags).HasMaxLength(1000);
            entity.HasOne(e => e.SettlementPostingEvent)
                .WithMany()
                .HasForeignKey(e => e.SettlementPostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.SettlementJournalEntry)
                .WithMany()
                .HasForeignKey(e => e.SettlementJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RecurringJournalTemplate>(entity =>
        {
            entity.ToTable("RecurringJournalTemplates");
            entity.HasIndex(e => new { e.TenantId, e.TemplateNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
            entity.HasIndex(e => new { e.TenantId, e.DefinitionKey, e.Version }).IsUnique().HasFilter("[IsDeleted] = 0");
            entity.HasIndex(e => new { e.TenantId, e.Status, e.NextDueDate });
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RecurringJournalTemplateLine>(entity =>
        {
            entity.ToTable("RecurringJournalTemplateLines");
            entity.HasIndex(e => new { e.TenantId, e.TemplateId, e.LineNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
            entity.HasOne(e => e.Template).WithMany(e => e.Lines).HasForeignKey(e => e.TemplateId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RecurringJournalOccurrence>(entity =>
        {
            entity.ToTable("RecurringJournalOccurrences");
            entity.HasIndex(e => new { e.TenantId, e.TemplateId, e.ScheduledDate }).IsUnique().HasFilter("[IsDeleted] = 0");
            entity.HasIndex(e => new { e.TenantId, e.Status, e.EffectiveDate });
            entity.HasOne(e => e.Template).WithMany(e => e.Occurrences).HasForeignKey(e => e.TemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.JournalEntry).WithOne(e => e.RecurringOccurrence).HasForeignKey<RecurringJournalOccurrence>(e => e.JournalEntryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OpeningBalanceBatch>(entity =>
        {
            entity.ToTable("OpeningBalanceBatches");
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.BatchNumber })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(e => new { e.TenantId, e.Status });
            entity.HasIndex(e => new { e.TenantId, e.PostingEventId });
            entity.HasIndex(e => new { e.TenantId, e.JournalEntryId });
            entity.Property(e => e.BatchNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.SourceReference).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.BookClassification).HasMaxLength(30).IsRequired();
            entity.Property(e => e.IdempotencyKey).HasMaxLength(120).IsRequired();
            entity.Property(e => e.TotalDebit).HasPrecision(18, 2);
            entity.Property(e => e.TotalCredit).HasPrecision(18, 2);
            entity.Property(e => e.Difference).HasPrecision(18, 2);
            entity.Property(e => e.FailureReason).HasMaxLength(1000);
            entity.HasOne(e => e.FiscalPeriod)
                .WithMany()
                .HasForeignKey(e => e.FiscalPeriodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(e => e.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinancePostingEvent>()
                .WithMany()
                .HasForeignKey(e => e.PostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OpeningBalanceLine>(entity =>
        {
            entity.ToTable("OpeningBalanceLines");
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.OpeningBalanceBatchId, e.LineNumber })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(e => new { e.TenantId, e.AccountId });
            entity.HasIndex(e => new { e.TenantId, e.BankAccountId });
            entity.Property(e => e.DebitAmount).HasPrecision(18, 2);
            entity.Property(e => e.CreditAmount).HasPrecision(18, 2);
            entity.Property(e => e.TransactionCurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(e => e.FunctionalCurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(e => e.SegmentString).HasMaxLength(500);
            entity.Property(e => e.CounterpartyType).HasMaxLength(30);
            entity.Property(e => e.SourceReference).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.HasOne(e => e.Batch)
                .WithMany(e => e.Lines)
                .HasForeignKey(e => e.OpeningBalanceBatchId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Account)
                .WithMany()
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ExchangeRate)
                .WithMany()
                .HasForeignKey(e => e.ExchangeRateId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SubledgerAdjustmentJournal>(entity =>
        {
            entity.ToTable("SubledgerAdjustmentJournals");
            entity.HasIndex(e => new { e.TenantId, e.Module, e.AdjustmentNumber }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.Module, e.AdjustmentDate });
            entity.HasIndex(e => new { e.TenantId, e.Module, e.Purpose, e.AdjustmentDate });
            entity.Property(e => e.Purpose).HasMaxLength(30).IsRequired();
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.SupplierId);
            entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Supplier)
                .WithMany()
                .HasForeignKey(e => e.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ContraAccount)
                .WithMany()
                .HasForeignKey(e => e.ContraAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.JournalEntry)
                .WithMany()
                .HasForeignKey(e => e.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.OriginalAdjustment)
                .WithMany()
                .HasForeignKey(e => e.OriginalAdjustmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReversalAdjustment)
                .WithMany()
                .HasForeignKey(e => e.ReversalAdjustmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MaintenanceAttachmentAccess>(entity =>
        {
            entity.ToTable("MaintenanceAttachmentAccessLogs");
            entity.HasIndex(x => x.AttachmentId);
            entity.HasIndex(x => x.AccessedByUserId);
            entity.HasOne(x => x.Attachment)
                .WithMany()
                .HasForeignKey(x => x.AttachmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AccessedByUser)
                .WithMany()
                .HasForeignKey(x => x.AccessedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ConsignmentSettlement>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.StockMovementId }).IsUnique();
            entity.HasOne(e => e.StockMovement)
                .WithMany()
                .HasForeignKey(e => e.StockMovementId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure FixedAssetCategory â†’ Account relationships (prevent cascade cycles with multiple FKs)
        builder.Entity<FixedAsset>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.AssetCode }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.SourceDocumentType, e.SourceDocumentId });
            entity.HasIndex(e => new { e.TenantId, e.JournalEntryId });
            entity.HasIndex(e => new { e.TenantId, e.PostingEventId });
            entity.Property(e => e.Location).HasMaxLength(500);
            entity.Property(e => e.CurrentSegmentString).HasMaxLength(500);
            entity.Property(e => e.FunctionalCurrencyCode).HasMaxLength(3);
            entity.Property(e => e.TransactionCurrencyCode).HasMaxLength(3);
            entity.Property(e => e.ExchangeRate).HasColumnType("decimal(18,6)");
            entity.Property(e => e.SourceDocumentType).HasMaxLength(50);
            entity.HasOne(e => e.CurrentCustodian)
                .WithMany()
                .HasForeignKey(e => e.CurrentCustodianId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CurrentSegmentLookupValue)
                .WithMany()
                .HasForeignKey(e => e.CurrentSegmentLookupValueId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(e => e.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinancePostingEvent>()
                .WithMany()
                .HasForeignKey(e => e.PostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ExchangeRate>()
                .WithMany()
                .HasForeignKey(e => e.ExchangeRateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AssetTransfer>(entity =>
        {
            entity.ToTable("AssetTransfers");
            entity.HasIndex(e => new { e.TenantId, e.FixedAssetId, e.TransferDate });
            entity.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
                .IsUnique()
                .HasFilter("[IdempotencyKey] IS NOT NULL");
            entity.HasIndex(e => new { e.TenantId, e.JournalEntryId });
            entity.HasIndex(e => new { e.TenantId, e.PostingEventId });
            entity.HasIndex(e => new { e.TenantId, e.ToSegmentLookupValueId });
            entity.Property(e => e.FromLocation).HasMaxLength(500);
            entity.Property(e => e.ToLocation).HasMaxLength(500);
            entity.Property(e => e.FromSegmentString).HasMaxLength(500);
            entity.Property(e => e.ToSegmentString).HasMaxLength(500);
            entity.Property(e => e.IdempotencyKey).HasMaxLength(150);
            entity.Property(e => e.ReferenceNumber).HasMaxLength(50);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.Comments).HasMaxLength(2000);
            entity.Property(e => e.FailureReason).HasMaxLength(1000);
            entity.HasOne(e => e.FixedAsset)
                .WithMany()
                .HasForeignKey(e => e.FixedAssetId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FiscalPeriod)
                .WithMany()
                .HasForeignKey(e => e.FiscalPeriodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FromSegmentLookupValue)
                .WithMany()
                .HasForeignKey(e => e.FromSegmentLookupValueId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ToSegmentLookupValue)
                .WithMany()
                .HasForeignKey(e => e.ToSegmentLookupValueId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.JournalEntry)
                .WithMany()
                .HasForeignKey(e => e.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PostingEvent)
                .WithMany()
                .HasForeignKey(e => e.PostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AssetDisposal>(entity =>
        {
            entity.ToTable("AssetDisposals");
            entity.HasIndex(e => new { e.TenantId, e.FixedAssetId, e.BookClassification });
            entity.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
                .IsUnique()
                .HasFilter("[IdempotencyKey] IS NOT NULL");
            entity.HasIndex(e => new { e.TenantId, e.JournalEntryId });
            entity.HasIndex(e => new { e.TenantId, e.PostingEventId });
            entity.HasIndex(e => new { e.TenantId, e.FiscalPeriodId });
            entity.HasIndex(e => new { e.TenantId, e.AccountingBookId });
            entity.HasIndex(e => new { e.TenantId, e.ProceedsAccountId });
            entity.Property(e => e.BookClassification).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ProceedsCurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(e => e.IdempotencyKey).HasMaxLength(150);
            entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.Comments).HasMaxLength(1000);
            entity.Property(e => e.FailureReason).HasMaxLength(1000);
            entity.Property(e => e.CostAtDisposal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.AccumulatedDepreciationAtDisposal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.AccumulatedImpairmentAtDisposal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.RevaluationSurplusAtDisposal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.NetProceeds).HasColumnType("decimal(18,2)");
            entity.Property(e => e.NetBookValueAtDisposal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.GainOrLoss).HasColumnType("decimal(18,2)");
            entity.HasOne(e => e.FixedAsset)
                .WithMany()
                .HasForeignKey(e => e.FixedAssetId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AccountingBook)
                .WithMany()
                .HasForeignKey(e => e.AccountingBookId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FiscalPeriod)
                .WithMany()
                .HasForeignKey(e => e.FiscalPeriodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ProceedsAccount)
                .WithMany()
                .HasForeignKey(e => e.ProceedsAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.JournalEntry)
                .WithMany()
                .HasForeignKey(e => e.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PostingEvent)
                .WithMany()
                .HasForeignKey(e => e.PostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FixedAssetBookValue>(entity =>
        {
            entity.ToTable("FixedAssetBookValues");
            entity.HasIndex(e => new { e.TenantId, e.FixedAssetId, e.AccountingBookId }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.BookClassification });
            entity.HasIndex(e => new { e.TenantId, e.CapitalizationPostingEventId });
            entity.HasIndex(e => new { e.TenantId, e.CapitalizationJournalEntryId });
            entity.Property(e => e.BookClassification).HasMaxLength(20).IsRequired();
            entity.Property(e => e.OpeningSource).HasMaxLength(50);
            entity.Property(e => e.SourceDocumentType).HasMaxLength(50);
            entity.HasOne(e => e.FixedAsset)
                .WithMany(asset => asset.BookValues)
                .HasForeignKey(e => e.FixedAssetId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AccountingBook)
                .WithMany()
                .HasForeignKey(e => e.AccountingBookId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(e => e.CapitalizationJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinancePostingEvent>()
                .WithMany()
                .HasForeignKey(e => e.CapitalizationPostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AssetValuation>(entity =>
        {
            entity.ToTable("AssetValuations");
            entity.HasIndex(e => new { e.TenantId, e.FixedAssetId, e.BookClassification, e.ValuationDate });
            entity.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
                .IsUnique()
                .HasFilter("[IdempotencyKey] IS NOT NULL");
            entity.HasIndex(e => new { e.TenantId, e.JournalEntryId });
            entity.HasIndex(e => new { e.TenantId, e.PostingEventId });
            entity.Property(e => e.BookClassification).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
            entity.Property(e => e.IdempotencyKey).HasMaxLength(150);
            entity.Property(e => e.FailureReason).HasMaxLength(1000);
            entity.Property(e => e.CarryingAmountBefore).HasColumnType("decimal(18,2)");
            entity.Property(e => e.AccumulatedDepreciationBefore).HasColumnType("decimal(18,2)");
            entity.Property(e => e.NetBookValueBefore).HasColumnType("decimal(18,2)");
            entity.Property(e => e.FairValue).HasColumnType("decimal(18,2)");
            entity.Property(e => e.CarryingAmountAfter).HasColumnType("decimal(18,2)");
            entity.Property(e => e.RevaluationSurplus).HasColumnType("decimal(18,2)");
            entity.Property(e => e.RevaluationDeficit).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ImpairmentLoss).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ImpairmentReversal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.AdjustmentAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.RevaluationSurplusApplied).HasColumnType("decimal(18,2)");
            entity.Property(e => e.RevaluationLossRecognized).HasColumnType("decimal(18,2)");
            entity.HasOne(e => e.AccountingBook)
                .WithMany()
                .HasForeignKey(e => e.AccountingBookId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FiscalPeriod)
                .WithMany()
                .HasForeignKey(e => e.FiscalPeriodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.JournalEntry)
                .WithMany()
                .HasForeignKey(e => e.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PostingEvent)
                .WithMany()
                .HasForeignKey(e => e.PostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AssetTransaction>(entity =>
        {
            entity.Property(e => e.BookClassification).HasMaxLength(20);
            entity.HasOne(e => e.AccountingBook)
                .WithMany()
                .HasForeignKey(e => e.AccountingBookId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AssetDepreciationSchedule>(entity =>
        {
            entity.ToTable("AssetDepreciationSchedules");
            entity.Property(e => e.BookClassification).HasMaxLength(20);
            entity.Property(e => e.DepreciationAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.AccumulatedDepreciationBefore).HasColumnType("decimal(18,2)");
            entity.Property(e => e.AccumulatedDepreciation).HasColumnType("decimal(18,2)");
            entity.Property(e => e.NetBookValueBefore).HasColumnType("decimal(18,2)");
            entity.Property(e => e.NetBookValue).HasColumnType("decimal(18,2)");
            entity.Property(e => e.DepreciableAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ResidualValueSnapshot).HasColumnType("decimal(18,2)");
            entity.HasIndex(e => new { e.TenantId, e.FixedAssetId, e.FiscalPeriodId, e.BookClassification }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.FixedAssetDepreciationRunId });
            entity.HasIndex(e => new { e.TenantId, e.PostingEventId });
            entity.HasIndex(e => new { e.TenantId, e.JournalEntryId });
            entity.HasOne(e => e.DepreciationRun)
                .WithMany(e => e.Lines)
                .HasForeignKey(e => e.FixedAssetDepreciationRunId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AccountingBook)
                .WithMany()
                .HasForeignKey(e => e.AccountingBookId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PostingEvent)
                .WithMany()
                .HasForeignKey(e => e.PostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FixedAssetDepreciationRun>(entity =>
        {
            entity.ToTable("FixedAssetDepreciationRuns");
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.IdempotencyKey }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.FiscalPeriodId, e.BookClassification });
            entity.HasIndex(e => new { e.TenantId, e.PostingEventId });
            entity.Property(e => e.BookClassification).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
            entity.Property(e => e.IdempotencyKey).HasMaxLength(150).IsRequired();
            entity.Property(e => e.FailureReason).HasMaxLength(1000);
            entity.Property(e => e.TotalDepreciationAmount).HasColumnType("decimal(18,2)");
            entity.HasOne(e => e.FiscalPeriod)
                .WithMany()
                .HasForeignKey(e => e.FiscalPeriodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FixedAsset)
                .WithMany()
                .HasForeignKey(e => e.FixedAssetId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.JournalEntry)
                .WithMany()
                .HasForeignKey(e => e.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PostingEvent)
                .WithMany()
                .HasForeignKey(e => e.PostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure FixedAssetCategory → Account relationships (prevent cascade cycles with multiple FKs)
        builder.Entity<FixedAssetCategory>(entity =>
        {
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

            entity.HasOne(c => c.DisposalProceedsClearingAccount)
                .WithMany()
                .HasForeignKey(c => c.DisposalProceedsClearingAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.RevaluationSurplusAccount)
                .WithMany()
                .HasForeignKey(c => c.RevaluationSurplusAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.RevaluationLossAccount)
                .WithMany()
                .HasForeignKey(c => c.RevaluationLossAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.ImpairmentLossAccount)
                .WithMany()
                .HasForeignKey(c => c.ImpairmentLossAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.AccumulatedImpairmentAccount)
                .WithMany()
                .HasForeignKey(c => c.AccumulatedImpairmentAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.ImpairmentReversalAccount)
                .WithMany()
                .HasForeignKey(c => c.ImpairmentReversalAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(c => c.AucAccount)
                .WithMany()
                .HasForeignKey(c => c.AucAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure CashTransaction â†’ BankAccount relationships (disambiguate two FKs)
        // Configure CashTransaction → BankAccount relationships (disambiguate two FKs)
        builder.Entity<BankAccount>(entity =>
        {
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.AccountNumber }).IsUnique();
        });

        builder.Entity<FinancePaymentMethod>(entity =>
        {
            entity.ToTable("PaymentMethod");
            entity.HasIndex(e => e.TenantId);
            // DB-level guard against duplicate codes within a tenant (codes are optional).
            entity.HasIndex(e => new { e.TenantId, e.Code })
                .IsUnique()
                .HasFilter("[Code] IS NOT NULL");
        });

        builder.Entity<Cheque>(entity =>
        {
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.BankAccountId, e.Status });
            // A cheque number is unique within its bank account per tenant.
            entity.HasIndex(e => new { e.TenantId, e.BankAccountId, e.ChequeNumber }).IsUnique();
        });

        builder.Entity<BankStatement>(entity =>
        {
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.BankAccountId, e.StatementDate });
        });

        builder.Entity<BankStatementLine>(entity =>
        {
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.BankStatementId });
        });

        builder.Entity<BankReconciliation>(entity =>
        {
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.BankAccountId, e.ReconciliationDate });
        });

        builder.Entity<CashTransaction>(entity =>
        {
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.BankAccountId, e.TransactionDate });
            entity.HasIndex(e => new { e.TenantId, e.TransactionNumber }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.JournalEntryId });
            entity.HasIndex(e => new { e.TenantId, e.ApprovalStatus });
            entity.HasIndex(e => new { e.TenantId, e.WorkflowInstanceId });

            entity.Property(e => e.ApprovalComments).HasMaxLength(1000);
            entity.Property(e => e.RejectionReason).HasMaxLength(1000);
            entity.Property(e => e.CancellationReason).HasMaxLength(1000);

            entity.HasOne(ct => ct.BankAccount)
                .WithMany(ba => ba.Transactions)
                .HasForeignKey(ct => ct.BankAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ct => ct.ToBankAccount)
                .WithMany()
                .HasForeignKey(ct => ct.ToBankAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            // 1:1 with Cheque â€” Cheque is the dependent (has CashTransactionId FK)
            entity.HasOne(ct => ct.Cheque)
                .WithOne(c => c.CashTransaction)
                .HasForeignKey<Cheque>(c => c.CashTransactionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Reconciliation link
            entity.HasOne(ct => ct.Reconciliation)
                .WithMany(r => r.ReconciledTransactions)
                .HasForeignKey(ct => ct.ReconciliationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ct => ct.JournalEntry)
                .WithMany()
                .HasForeignKey(ct => ct.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure ReconciliationMatch â†’ BankStatementLine 1:1 (ReconciliationMatch is dependent)
        builder.Entity<ReconciliationMatch>(entity =>
        {
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.ReconciliationId });

            entity.HasOne(rm => rm.BankStatementLine)
                .WithOne(bsl => bsl.ReconciliationMatch)
                .HasForeignKey<ReconciliationMatch>(rm => rm.BankStatementLineId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Award Verification configurations
        builder.ApplyConfiguration(new AwardVerificationChecklistTemplateConfiguration());
        builder.ApplyConfiguration(new AwardVerificationChecklistItemConfiguration());
        builder.ApplyConfiguration(new TenderAwardVerificationConfiguration());
        builder.ApplyConfiguration(new TenderAwardVerificationBidderConfiguration());
        builder.ApplyConfiguration(new TenderAwardVerificationItemResultConfiguration());
        builder.ApplyConfiguration(new TenderAwardVerificationItemDocumentConfiguration());

        // Contract Management configurations
        builder.ApplyConfiguration(new ContractConfiguration());
        builder.ApplyConfiguration(new ContractMilestoneConfiguration());
        builder.ApplyConfiguration(new ContractAmendmentConfiguration());
        builder.ApplyConfiguration(new ContractDocumentConfiguration());

        // Project management configurations
        builder.ApplyConfiguration(new ProjectTypeConfiguration());
        builder.ApplyConfiguration(new ProjectPriorityConfiguration());
        builder.ApplyConfiguration(new ProjectTemplateConfiguration());
        builder.ApplyConfiguration(new ProjectPhaseTemplateConfiguration());
        builder.ApplyConfiguration(new ProjectStageGateRuleConfiguration());
        builder.ApplyConfiguration(new ProjectPortfolioConfiguration());
        builder.ApplyConfiguration(new ProjectProgramConfiguration());
        builder.ApplyConfiguration(new ProjectManagementSettingsConfiguration());
        builder.ApplyConfiguration(new ProjectCatalogEntryConfiguration());
        builder.ApplyConfiguration(new ProjectConfiguration());
        builder.ApplyConfiguration(new ProjectDevelopmentProfileConfiguration());
        builder.ApplyConfiguration(new ProjectPhaseConfiguration());
        builder.ApplyConfiguration(new ProjectPackageConfiguration());
        builder.ApplyConfiguration(new ProjectBoqItemConfiguration());
        builder.ApplyConfiguration(new ProjectApprovalRegisterItemConfiguration());
        builder.ApplyConfiguration(new ProjectDrawingConfiguration());
        builder.ApplyConfiguration(new ProjectSubmittalConfiguration());
        builder.ApplyConfiguration(new ProjectRfiConfiguration());
        builder.ApplyConfiguration(new ProjectSiteInstructionConfiguration());
        builder.ApplyConfiguration(new ProjectVariationOrderConfiguration());
        builder.ApplyConfiguration(new ProjectInterimValuationConfiguration());
        builder.ApplyConfiguration(new ProjectInterimValuationPackageCompletionConfiguration());
        builder.ApplyConfiguration(new ProjectPaymentCertificateConfiguration());
        builder.ApplyConfiguration(new ProjectExtensionOfTimeConfiguration());
        builder.ApplyConfiguration(new ProjectFinalAccountConfiguration());
        builder.ApplyConfiguration(new ProjectBuildingConfiguration());
        builder.ApplyConfiguration(new ProjectFloorConfiguration());
        builder.ApplyConfiguration(new ProjectUnitReleaseBatchConfiguration());
        builder.ApplyConfiguration(new ProjectUnitHandoverBatchConfiguration());
        builder.ApplyConfiguration(new ProjectUnitTypeTemplateConfiguration());
        builder.ApplyConfiguration(new ProjectUnitTypeTemplateAmenityConfiguration());
        builder.ApplyConfiguration(new ProjectUnitConfiguration());
        builder.ApplyConfiguration(new ProjectUnitAmenityConfiguration());
        builder.ApplyConfiguration(new ProjectCustomerVariationConfiguration());
        builder.ApplyConfiguration(new ProjectCommissioningItemConfiguration());
        builder.ApplyConfiguration(new ProjectHandoverItemConfiguration());
        builder.ApplyConfiguration(new ProjectSnagItemConfiguration());
        builder.ApplyConfiguration(new ProjectDefectLiabilityCaseConfiguration());
        builder.ApplyConfiguration(new ProjectInitiationVersionConfiguration());
        builder.ApplyConfiguration(new ProjectMemberConfiguration());
        builder.ApplyConfiguration(new ProjectWorkItemConfiguration());
        builder.ApplyConfiguration(new ProjectMilestoneConfiguration());
        builder.ApplyConfiguration(new ProjectMilestonePhaseConfiguration());
        builder.ApplyConfiguration(new ProjectResourceAllocationConfiguration());
        builder.ApplyConfiguration(new ProjectRiskConfiguration());
        builder.ApplyConfiguration(new ProjectIssueConfiguration());
        builder.ApplyConfiguration(new ProjectChangeRequestConfiguration());
        builder.ApplyConfiguration(new ProjectBillingScheduleConfiguration());
        builder.ApplyConfiguration(new ProjectInvoiceRequestConfiguration());
        builder.ApplyConfiguration(new ProjectDeliverableConfiguration());
        builder.ApplyConfiguration(new ProjectDeliverableExternalReviewConfiguration());
        builder.ApplyConfiguration(new ProjectTaskDependencyConfiguration());
        builder.ApplyConfiguration(new ProjectInterdependencyConfiguration());
        builder.ApplyConfiguration(new ProjectBaselineConfiguration());
        builder.ApplyConfiguration(new ProjectTimesheetEntryConfiguration());
        builder.ApplyConfiguration(new ProjectExpenseConfiguration());
        builder.ApplyConfiguration(new ProjectMaterialCostEntryConfiguration());
        builder.ApplyConfiguration(new ProjectRevenueRecognitionConfiguration());
        builder.ApplyConfiguration(new ProjectBudgetRevisionConfiguration());
        builder.ApplyConfiguration(new ProjectForecastVersionConfiguration());
        builder.ApplyConfiguration(new ProjectAssetLinkConfiguration());
        builder.ApplyConfiguration(new ProjectExternalAccessPolicyConfiguration());
        builder.ApplyConfiguration(new ProjectDecisionConfiguration());
        builder.ApplyConfiguration(new ProjectMeetingMinuteConfiguration());
        builder.ApplyConfiguration(new ProjectActionItemConfiguration());
        builder.ApplyConfiguration(new ProjectLessonLearnedConfiguration());
        builder.ApplyConfiguration(new ProjectDocumentConfiguration());
        builder.ApplyConfiguration(new ProjectCommentConfiguration());
        builder.ApplyConfiguration(new ProjectClosureConfiguration());

        // Sales setup
        builder.ApplyConfiguration(new SalesSaleableSourceConfiguration());
        builder.ApplyConfiguration(new SalesAllocationConfiguration());
        builder.ApplyConfiguration(new SalesAllocationHistoryConfiguration());

        // Price List configurations
        builder.ApplyConfiguration(new PriceListConfiguration());
        builder.ApplyConfiguration(new PriceListLineConfiguration());
        builder.ApplyConfiguration(new CustomerGroupConfiguration());
        builder.ApplyConfiguration(new SupplierGroupConfiguration());
        builder.ApplyConfiguration(new PriceListChangeHistoryConfiguration());

        builder.Ignore<Payment>();

        // CRM uses BusinessPartner as the account backbone, so we keep the sales entities
        // in the model but opt out of the legacy duplicate customer/invoice navigations.
        builder.Entity<Campaign>(entity =>
        {
            entity.HasOne(x => x.Manager)
                .WithMany()
                .HasForeignKey(x => x.ManagerId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.TenantId, x.CampaignStatus });
            entity.HasIndex(x => new { x.TenantId, x.CampaignType });
        });

        builder.Entity<CampaignMember>(entity =>
        {
            entity.Ignore(x => x.Customer);
            entity.HasOne(x => x.Campaign)
                .WithMany(x => x.CampaignMembers)
                .HasForeignKey(x => x.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Lead)
                .WithMany()
                .HasForeignKey(x => x.LeadId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.CampaignId, x.LeadId });
            entity.HasIndex(x => new { x.TenantId, x.MemberStatus });
        });

        builder.Entity<Lead>(entity =>
        {
            entity.Ignore(x => x.ConvertedCustomer);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Opportunity>(entity =>
        {
            entity.Ignore(x => x.Customer);
            entity.HasOne(x => x.Lead)
                .WithMany(x => x.Opportunities)
                .HasForeignKey(x => x.LeadId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Activity>(entity =>
        {
            entity.Ignore(x => x.Customer);
            entity.HasOne(x => x.Lead)
                .WithMany(x => x.Activities)
                .HasForeignKey(x => x.LeadId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Opportunity)
                .WithMany(x => x.Activities)
                .HasForeignKey(x => x.OpportunityId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Quote>(entity =>
        {
            entity.Ignore(x => x.Customer);
            entity.Ignore(x => x.ConvertedInvoice);
            entity.HasOne(x => x.Opportunity)
                .WithMany(x => x.Quotes)
                .HasForeignKey(x => x.OpportunityId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<QuoteLineItem>(entity =>
        {
            entity.HasOne(x => x.Quote)
                .WithMany(x => x.LineItems)
                .HasForeignKey(x => x.QuoteId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

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

        builder.Entity<ModuleDefinition>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ModuleCode }).IsUnique();
        });

        builder.Entity<PeriodModuleLock>(entity =>
        {
            entity.ToTable("PeriodModuleLock");
            entity.HasIndex(e => new { e.TenantId, e.FiscalPeriodId, e.ModuleDefinitionId }).IsUnique();
            entity.HasIndex(e => new { e.IsLocked, e.ReopenExpiresAtUtc });
        });

        builder.Entity<TransactionDocumentModuleMapping>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.DocumentType }).IsUnique();
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

        builder.Entity<FileUploadPolicy>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Category }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.IsEnabled });
        });

        builder.Entity<FileUploadRecord>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Category });
            entity.HasIndex(x => new { x.TenantId, x.CreatedAt });
            entity.HasIndex(x => new { x.TenantId, x.FilePath }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.UploadedByUserId });
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
        ConfigureSalesAllocationPrecision(builder);

        // Configure all tenant relationships to avoid cascade conflicts
        ConfigureGlobalTenantRelationships(builder);

        // Configure HR Management entities
        ConfigureHREntities(builder);
        ConfigurePayrollEntities(builder);

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

        // Configure Procurement Planning entities
        ConfigureProcurementPlanningEntities(builder);
        ConfigureProcurementAppSubmissions(builder);
        ConfigureProcurementSpecificationTemplates(builder);
        ConfigureProcurementCalendar(builder);
        ConfigurePurchaseRequisitionLinkage(builder);

        // Configure Finance Common entities
        ConfigureFinanceCommonEntities(builder);

        // Configure Compliance/Settings entities
        ConfigureComplianceAndSettingsEntities(builder);

        // Apply global query filters for soft delete and multitenancy
        ApplyGlobalFilters(builder);

        // Seed initial data
        SeedData(builder);
    }

    private static void ConfigurePayrollEntities(ModelBuilder builder)
    {
        builder.Entity<PayrollBudgetAnalysisRow>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.CompanyCode, e.PayPeriod, e.OrderField, e.TransactionType, e.ActualTransaction }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.CompanyCode, e.PayPeriod });
            entity.Property(e => e.BaseAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Amount1).HasColumnType("decimal(18,2)");
            entity.Property(e => e.NewAmount1).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Amount2).HasColumnType("decimal(18,2)");
            entity.Property(e => e.NewAmount2).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Amount3).HasColumnType("decimal(18,2)");
            entity.Property(e => e.NewAmount3).HasColumnType("decimal(18,2)");
        });

        builder.Entity<PayrollParameterSet>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.IsActive });
        });

        builder.Entity<PayrollComponent>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ComponentType, e.Code }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.ComponentType, e.IsActive });
        });

        builder.Entity<PayrollComponentRule>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ComponentType, e.ComponentCode, e.Category }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.ComponentType, e.ComponentCode });
        });

        builder.Entity<PayrollTaxBand>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.TaxType, e.SerialNo, e.EffectiveFrom, e.IsAnnual, e.LegacyCompanyCode }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.TaxType, e.IsActive });
            entity.HasIndex(e => new { e.TenantId, e.PayPeriod, e.LegacyCompanyCode });
        });

        builder.Entity<PayrollTaxRelief>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.AppliesByDefault, e.IsActive });
        });

        builder.Entity<PayrollEmployeeTaxRelief>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ReliefCode, e.EmployeeProfileId }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.ReliefCode, e.IsActive });
            entity.HasIndex(e => new { e.TenantId, e.EmployeeProfileId, e.IsActive });

            entity.HasOne(e => e.EmployeeProfile)
                .WithMany()
                .HasForeignKey(e => e.EmployeeProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayrollPensionScheme>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.IsDefault, e.IsActive });
        });

        builder.Entity<PayrollOvertimePolicy>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Code, e.LegacyCompanyCode }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.IsDefault, e.IsActive });

            entity.HasMany(e => e.Ranges)
                .WithOne(e => e.PayrollOvertimePolicy)
                .HasForeignKey(e => e.PayrollOvertimePolicyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayrollLoanPolicy>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Code, e.LegacyCompanyCode }).IsUnique();
        });

        builder.Entity<PayrollBonusPolicy>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
        });

        builder.Entity<PayrollBonusRule>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.BonusCode, e.GroupCode }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.BonusCode });
        });

        builder.Entity<PayrollBonusException>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.BonusCode, e.EmployeeNumber }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.BonusCode });

            entity.HasOne(e => e.EmployeeProfile)
                .WithMany()
                .HasForeignKey(e => e.EmployeeProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayrollBackpayPolicy>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.OperationType }).IsUnique();
        });

        builder.Entity<PayrollBackpayRule>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.OperationType, e.CategoryType, e.CategoryCode }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.OperationType, e.CategoryType });
        });

        builder.Entity<PayrollBackpayException>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.OperationType, e.EmployeeNumber }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.OperationType });
        });

        builder.Entity<PayrollJournalMapping>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.TransactionType, e.ComponentCode });
            entity.HasIndex(e => new { e.TenantId, e.AccountCode });
            entity.HasIndex(e => new { e.TenantId, e.LegacyCompanyCode, e.SequenceNo });
        });

        builder.Entity<PayrollCodeType>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.CodeType, e.LegacyCompanyCode }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.Blocked });

            entity.HasMany(e => e.Values)
                .WithOne(e => e.PayrollCodeType)
                .HasForeignKey(e => e.PayrollCodeTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayrollCodeValue>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.CodeType, e.ActualCode, e.LegacyCompanyCode }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.CodeType, e.Blocked });
        });

        builder.Entity<PayrollHoliday>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.HolidayDate, e.LegacyCompanyCode }).IsUnique();
        });

        builder.Entity<PayrollNonWorkingDay>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.DayCode, e.LegacyCompanyCode }).IsUnique();
        });

        builder.Entity<PayrollExchangeRate>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.CurrencyCode, e.PayPeriod, e.LegacyCompanyCode }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.PayPeriodFrom, e.PayPeriodTo });
        });

        builder.Entity<PayrollBankBranch>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.BankCode, e.BranchCode, e.LegacyCompanyCode }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.BankCode });
        });

        builder.Entity<PayrollLeaveSetup>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Category, e.CategoryDetail, e.LegacyCompanyCode }).IsUnique();

            entity.HasMany(e => e.Details)
                .WithOne(e => e.PayrollLeaveSetup)
                .HasForeignKey(e => e.PayrollLeaveSetupId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayrollLeaveSetupDetail>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.PayrollLeaveSetupId, e.SequenceNo });
            entity.HasIndex(e => new { e.TenantId, e.Category, e.CategoryDetail, e.LegacyCompanyCode });
        });

        builder.Entity<PayrollOvertimeRange>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.PayrollOvertimePolicyId, e.MinRange, e.MaxRange }).IsUnique();
        });

        builder.Entity<PayrollLegacyMenuUser>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.UserName, e.LegacyCompanyCode }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.UserGroup });
            entity.HasIndex(e => new { e.TenantId, e.LoginEnabled, e.Locked });
        });

        builder.Entity<PayrollLegacyMenuSecurity>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.FormCode, e.UserName, e.GroupName, e.LegacyCompanyCode }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.GroupName, e.Allowed });
        });

        builder.Entity<PayrollCompanyProfile>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.CompanyId }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.CompanyCode, e.LegacyCompanyCode });

            entity.HasMany(e => e.BusinessUnits)
                .WithOne(e => e.PayrollCompanyProfile)
                .HasForeignKey(e => e.PayrollCompanyProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Bankers)
                .WithOne(e => e.PayrollCompanyProfile)
                .HasForeignKey(e => e.PayrollCompanyProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayrollBusinessUnit>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.BusinessUnitId, e.LegacyCompanyCode }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.CompanyId, e.CompanyCode });
        });

        builder.Entity<PayrollCompanyBanker>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.CompanyId, e.CompanyCode, e.BankCode, e.BankBranch });
            entity.HasIndex(e => new { e.TenantId, e.LegacyCompanyCode });
        });

        builder.Entity<PayrollGrade>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.GradeName, e.CurrencyCode, e.LegacyCompanyCode }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.GradeId, e.LegacyCompanyCode });

            entity.HasMany(e => e.Notches)
                .WithOne(e => e.PayrollGrade)
                .HasForeignKey(e => e.PayrollGradeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayrollGradeNotch>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.PayrollGradeId, e.Notch, e.CurrencyCode, e.LegacyCompanyCode }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.GradeName, e.Notch, e.LegacyCompanyCode });
        });

        builder.Entity<PayrollEmployeeProfile>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.EmployeeId }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.EmployeeNumber }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.LegacyEmployeeNumber });
            entity.HasIndex(e => new { e.TenantId, e.PayrollActive });

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.DefaultPaymentMethod)
                .WithMany()
                .HasForeignKey(e => e.DefaultPaymentMethodId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.SalaryBasis)
                .WithOne(e => e.EmployeeProfile)
                .HasForeignKey<PayrollSalaryBasis>(e => e.EmployeeProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.PaymentMethods)
                .WithOne(e => e.EmployeeProfile)
                .HasForeignKey(e => e.EmployeeProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.EmployeeComponents)
                .WithOne(e => e.EmployeeProfile)
                .HasForeignKey(e => e.EmployeeProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Loans)
                .WithOne(e => e.EmployeeProfile)
                .HasForeignKey(e => e.EmployeeProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.SalaryAdvances)
                .WithOne(e => e.EmployeeProfile)
                .HasForeignKey(e => e.EmployeeProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayrollSalaryBasis>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.EmployeeProfileId }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.EffectiveFrom, e.EffectiveTo });
        });

        builder.Entity<PayrollPaymentMethod>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.EmployeeProfileId, e.SequenceNo });
            entity.HasIndex(e => new { e.TenantId, e.PaymentType, e.IsActive });
            entity.Property(e => e.ExchangeRate).HasColumnType("decimal(18,4)");
        });

        builder.Entity<PayrollEmployeeComponent>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.EmployeeProfileId, e.PayrollComponentId }).IsUnique();
            entity.Property(e => e.TaxFreeCeilingOverride).HasColumnType("decimal(18,2)");

            entity.HasOne(e => e.PayrollComponent)
                .WithMany(e => e.EmployeeComponents)
                .HasForeignKey(e => e.PayrollComponentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayrollLoan>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.EmployeeProfileId, e.FacilityNumber }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.IsActive, e.PaymentStartDate });
            entity.HasIndex(e => new { e.TenantId, e.Status, e.PaymentStartDate });

            entity.HasOne(e => e.LoanPolicy)
                .WithMany()
                .HasForeignKey(e => e.LoanPolicyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayrollLoanSchedule>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.PayrollLoanId, e.SequenceNo }).IsUnique();

            entity.HasOne(e => e.PayrollLoan)
                .WithMany(e => e.Schedules)
                .HasForeignKey(e => e.PayrollLoanId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayrollSalaryAdvance>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.EmployeeProfileId, e.AdvanceDate });
            entity.HasIndex(e => new { e.TenantId, e.EmployeeNumber, e.AdvanceDate });
            entity.HasIndex(e => new { e.TenantId, e.IsActive, e.AdvanceDate });

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayrollPromotionArrearsEntry>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.EmployeeProfileId }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.EmployeeNumber }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.IsActive, e.EffectiveDate });
            entity.HasIndex(e => new { e.TenantId, e.PayPeriod, e.LegacyCompanyCode });

            entity.HasOne(e => e.EmployeeProfile)
                .WithMany()
                .HasForeignKey(e => e.EmployeeProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayrollTimesheetSummary>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.EmployeeProfileId, e.PayPeriod }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.EmployeeNumber, e.PayPeriod });
            entity.HasIndex(e => new { e.TenantId, e.IsActive, e.PayPeriod });
            entity.Property(e => e.NormalHours).HasColumnType("decimal(18,4)");
            entity.Property(e => e.WeekdayHours).HasColumnType("decimal(18,4)");
            entity.Property(e => e.HolidayHours).HasColumnType("decimal(18,4)");
            entity.Property(e => e.SaturdayHours).HasColumnType("decimal(18,4)");
            entity.Property(e => e.SundayHours).HasColumnType("decimal(18,4)");
            entity.Property(e => e.AbsentHours).HasColumnType("decimal(18,4)");
            entity.Property(e => e.NightShiftCount).HasColumnType("decimal(18,4)");
            entity.Property(e => e.AttendanceCount).HasColumnType("decimal(18,4)");
            entity.Property(e => e.OvertimeAmount).HasColumnType("decimal(18,2)");

            entity.HasOne(e => e.EmployeeProfile)
                .WithMany()
                .HasForeignKey(e => e.EmployeeProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayrollContributionOpeningBalance>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ContributionCodeType, e.ContributionCode, e.EmployeeProfileId }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.ContributionCodeType, e.ContributionCode, e.IsActive });
            entity.HasIndex(e => new { e.TenantId, e.EmployeeNumber });
            entity.Property(e => e.OpeningBalance).HasColumnType("decimal(18,2)");

            entity.HasOne(e => e.EmployeeProfile)
                .WithMany()
                .HasForeignKey(e => e.EmployeeProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayrollContributionTransaction>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.EmployeeProfileId, e.ContributionCode, e.EffectiveDate });
            entity.HasIndex(e => new { e.TenantId, e.ContributionCodeType, e.ContributionCode, e.TransactionType, e.EffectiveDate });
            entity.HasIndex(e => new { e.TenantId, e.IsActive, e.EffectiveDate });
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");

            entity.HasOne(e => e.EmployeeProfile)
                .WithMany()
                .HasForeignKey(e => e.EmployeeProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayrollImportBatch>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.ImportType, e.ImportedAt });

            entity.HasMany(e => e.Rows)
                .WithOne(e => e.PayrollImportBatch)
                .HasForeignKey(e => e.PayrollImportBatchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayrollImportRow>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.PayrollImportBatchId, e.RowNumber }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.LegacyEmployeeNumber });

            entity.HasOne(e => e.MatchedEmployee)
                .WithMany()
                .HasForeignKey(e => e.MatchedEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayrollRun>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.RunNumber }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.PayPeriod, e.Status });

            entity.HasMany(e => e.Employees)
                .WithOne(e => e.PayrollRun)
                .HasForeignKey(e => e.PayrollRunId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Transactions)
                .WithOne(e => e.PayrollRun)
                .HasForeignKey(e => e.PayrollRunId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.JournalLines)
                .WithOne(e => e.PayrollRun)
                .HasForeignKey(e => e.PayrollRunId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.ReportSnapshots)
                .WithOne(e => e.PayrollRun)
                .HasForeignKey(e => e.PayrollRunId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.PayslipSnapshots)
                .WithOne(e => e.PayrollRun)
                .HasForeignKey(e => e.PayrollRunId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayrollRunEmployee>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.PayrollRunId, e.EmployeeId }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.EmployeeNumber });

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Transactions)
                .WithOne(e => e.PayrollRunEmployee)
                .HasForeignKey(e => e.PayrollRunEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayrollTransaction>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.PayrollRunId, e.TransactionType });
            entity.HasIndex(e => new { e.TenantId, e.PayrollRunEmployeeId });

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.PayrollComponent)
                .WithMany(e => e.Transactions)
                .HasForeignKey(e => e.PayrollComponentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PayrollJournalLine>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.PayrollRunId, e.SequenceNo }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.AccountCode });
        });

        builder.Entity<PayrollReportSnapshot>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.PayrollRunId, e.ReportType, e.GeneratedAt });
            entity.HasIndex(e => new { e.TenantId, e.SnapshotNumber }).IsUnique();
        });

        builder.Entity<PayrollPayslipSnapshot>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.PayrollRunId, e.EmployeeId }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.PayslipNumber }).IsUnique();

            entity.HasOne(e => e.PayrollRunEmployee)
                .WithMany()
                .HasForeignKey(e => e.PayrollRunEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureWorkflowEntities(ModelBuilder builder)
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
            entity.HasIndex(wd => wd.LifecycleStatus);
            entity.HasIndex(wd => new { wd.TenantId, wd.DefinitionKey, wd.Version }).IsUnique();

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
            entity.HasIndex(a => a.CurrentProjectId);
            entity.HasIndex(a => a.CurrentSiteLocationId);

            entity.HasOne(a => a.AssetCategory)
                .WithMany(c => c.Assets)
                .HasForeignKey(a => a.AssetCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.ParentAsset)
                .WithMany(pa => pa.ChildAssets)
                .HasForeignKey(a => a.ParentAssetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(a => a.CurrentProject)
                .WithMany()
                .HasForeignKey(a => a.CurrentProjectId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(a => a.CurrentSiteLocation)
                .WithMany()
                .HasForeignKey(a => a.CurrentSiteLocationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<MaintenanceAssetMovement>(entity =>
        {
            entity.HasIndex(m => m.AssetId);
            entity.HasIndex(m => m.EffectiveDate);
            entity.HasIndex(m => m.ToProjectId);
            entity.HasIndex(m => m.ToSiteLocationId);

            entity.HasOne(m => m.Asset)
                .WithMany(a => a.Movements)
                .HasForeignKey(m => m.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
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

            entity.HasOne<ApplicationUser>(wo => wo.RequestedBy)
                .WithMany()
                .HasForeignKey(wo => wo.RequestedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne<ApplicationUser>(wo => wo.ApprovedBy)
                .WithMany()
                .HasForeignKey(wo => wo.ApprovedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne<ApplicationUser>(wo => wo.Supervisor)
                .WithMany()
                .HasForeignKey(wo => wo.SupervisorId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne<ApplicationUser>(wo => wo.CompletedBy)
                .WithMany()
                .HasForeignKey(wo => wo.CompletedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne<ApplicationUser>(wo => wo.QualityCheckedBy)
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
                .WithMany(mt => mt.MaintenanceSchedules)
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
            entity.Property(it => it.TemplateScope).HasMaxLength(50).HasDefaultValue("General");
            entity.Property(it => it.FleetInspectionKind).HasMaxLength(30).HasDefaultValue("Any");
            entity.Property(it => it.QrPayloadVersion).HasDefaultValue(1);
            entity.Property(it => it.AutoCreateWorkOrderOnFailure).HasDefaultValue(true);

            entity.HasIndex(it => it.Category);
            entity.HasIndex(it => it.InspectionType);
            entity.HasIndex(it => it.IsActive);
            entity.HasIndex(it => it.TenantId);
            entity.HasIndex(it => new { it.TenantId, it.TemplateScope, it.IsActive, it.IsDeleted });
            entity.HasIndex(it => new { it.TenantId, it.TemplateScope, it.FleetInspectionKind, it.IsActive, it.IsDeleted });
            entity.HasIndex(it => new { it.TenantId, it.AssignedAssetCategoryId, it.IsDeleted });
            entity.HasIndex(it => new { it.TenantId, it.AssignedAssetId, it.IsDeleted });
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

        // Configure Technician entity
        builder.Entity<Technician>(entity =>
        {
            entity.HasIndex(t => t.EmployeeId);
            entity.HasIndex(t => t.EmployeeNumber);
            entity.HasIndex(t => t.Specialization);
            entity.HasIndex(t => t.IsActive);

            entity.HasOne(t => t.Employee)
                .WithMany()
                .HasForeignKey(t => t.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            // Ignore SkillAssignments collection - TechnicianSkillAssignment.TechnicianId references Employee, not Technician
            entity.Ignore(t => t.SkillAssignments);

            // Ignore TeamsLed collection - TechnicianTeam doesn't have a proper FK to Technician
            entity.Ignore(t => t.TeamsLed);
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
                .OnDelete(DeleteBehavior.NoAction);
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

        // Tool checkouts are employee-driven because work order scheduling uses HR Employee IDs.
        builder.Entity<ToolCheckout>(entity =>
        {
            entity.HasIndex(tc => tc.ToolId);
            entity.HasIndex(tc => tc.CheckedOutById);
            entity.HasIndex(tc => tc.CheckedInById);
            entity.HasIndex(tc => tc.WorkOrderId);
            entity.HasIndex(tc => tc.JobCardId);

            entity.HasOne(tc => tc.CheckedOutBy)
                .WithMany()
                .HasForeignKey(tc => tc.CheckedOutById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(tc => tc.CheckedInBy)
                .WithMany()
                .HasForeignKey(tc => tc.CheckedInById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(tc => tc.WorkOrder)
                .WithMany()
                .HasForeignKey(tc => tc.WorkOrderId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(tc => tc.JobCard)
                .WithMany()
                .HasForeignKey(tc => tc.JobCardId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<WorkOrderTool>(entity =>
        {
            entity.HasIndex(wot => wot.WorkOrderId);
            entity.HasIndex(wot => wot.ToolId);
            entity.HasIndex(wot => wot.CheckoutId);

            entity.HasOne(wot => wot.WorkOrder)
                .WithMany(wo => wo.Tools)
                .HasForeignKey(wot => wot.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(wot => wot.Checkout)
                .WithMany()
                .HasForeignKey(wot => wot.CheckoutId)
                .OnDelete(DeleteBehavior.NoAction);
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
            entity.HasIndex(jc => jc.CustomerBusinessPartnerId);
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

            entity.HasOne(jc => jc.CustomerBusinessPartner)
                .WithMany()
                .HasForeignKey(jc => jc.CustomerBusinessPartnerId)
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

        // Configure Technician entity (now using Employee entity directly)
        // Technician-specific configurations are handled in the Employee entity configuration
    }

    private static void ConfigureQualityControlEntities(ModelBuilder builder)
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

    private static void ConfigureHREntities(ModelBuilder builder)
    {
        builder.Entity<OrganizationStructure>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Code })
                .IsUnique()
                .HasDatabaseName("IX_OrgStructure_Tenant_Code");

            entity.HasIndex(e => new { e.TenantId, e.IsDefault })
                .HasDatabaseName("IX_OrgStructure_Tenant_Default");

            entity.HasIndex(e => new { e.TenantId, e.IsActive })
                .HasDatabaseName("IX_OrgStructure_Tenant_Active");

            entity.HasMany(e => e.Levels)
                .WithOne(e => e.OrganizationStructure)
                .HasForeignKey(e => e.StructureId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OrganizationLevel>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.StructureId, e.LevelNumber })
                .IsUnique()
                .HasDatabaseName("IX_OrgLevel_Tenant_Structure_LevelNum");

            entity.HasIndex(e => new { e.TenantId, e.StructureId, e.Code })
                .IsUnique()
                .HasDatabaseName("IX_OrgLevel_Tenant_Structure_Code");

            entity.HasIndex(e => new { e.StructureId, e.IsActive })
                .HasDatabaseName("IX_OrgLevel_Structure_Active");

            entity.HasOne(e => e.OrganizationStructure)
                .WithMany(e => e.Levels)
                .HasForeignKey(e => e.StructureId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.OrganizationUnits)
                .WithOne(e => e.OrganizationLevel)
                .HasForeignKey(e => e.OrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OrganizationUnit>(entity =>
        {
            // Self-referencing relationship for hierarchy
            entity.HasOne(e => e.ParentUnit)
                .WithMany(e => e.ChildUnits)
                .HasForeignKey(e => e.ParentUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.OrganizationLevel)
                .WithMany(e => e.OrganizationUnits)
                .HasForeignKey(e => e.OrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.HeadEmployee)
                .WithMany()
                .HasForeignKey(e => e.HeadEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Employees)
                .WithOne(e => e.OrganizationUnit)
                .HasForeignKey(e => e.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Positions)
                .WithOne(p => p.OrganizationUnit)
                .HasForeignKey(p => p.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.TenantId, e.Code })
                .IsUnique()
                .HasDatabaseName("IX_OrgUnit_Tenant_Code");

            entity.HasIndex(e => e.ParentUnitId)
                .HasDatabaseName("IX_OrgUnit_ParentId");

            entity.HasIndex(e => e.OrganizationLevelId)
                .HasDatabaseName("IX_OrgUnit_LevelId");

            entity.HasIndex(e => e.HeadEmployeeId)
                .HasDatabaseName("IX_OrgUnit_HeadEmployeeId");

            entity.HasIndex(e => new { e.IsActive })
                .HasDatabaseName("IX_OrgUnit_Active");

            entity.HasIndex(e => new { e.ParentUnitId, e.Sequence })
                .HasDatabaseName("IX_OrgUnit_Parent_Sequence");

            entity.HasIndex(e => new { e.OrganizationLevelId })
                .HasDatabaseName("IX_OrgUnit_Level");
        });

        builder.Entity<OrganizationUnitHistory>(entity =>
        {
            entity.ToTable("OrganizationUnitHistories");

            entity.HasOne(e => e.OrganizationUnit)
                .WithMany()
                .HasForeignKey(e => e.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict); // Keep history even if unit is deleted

            // Indexes
            entity.HasIndex(e => e.OrganizationUnitId)
                .HasDatabaseName("IX_OrgUnitHistory_UnitId");

            entity.HasIndex(e => new { e.OrganizationUnitId, e.EffectiveFrom })
                .HasDatabaseName("IX_OrgUnitHistory_Unit_EffectiveFrom");

            entity.HasIndex(e => new { e.TenantId, e.EffectiveFrom })
                .HasDatabaseName("IX_OrgUnitHistory_Tenant_EffectiveFrom");
        });

        builder.Entity<LocationStructure>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Code })
                .IsUnique()
                .HasDatabaseName("IX_LocStructure_Tenant_Code");

            entity.HasIndex(e => new { e.TenantId, e.IsDefault })
                .HasDatabaseName("IX_LocStructure_Tenant_Default");

            entity.HasMany(e => e.LocationLevels)
                .WithOne(e => e.Structure)
                .HasForeignKey(e => e.StructureId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Locations)
                .WithOne(e => e.Structure)
                .HasForeignKey(e => e.StructureId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<LocationLevel>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.StructureId, e.LevelNumber })
                .IsUnique()
                .HasDatabaseName("IX_LocLevel_Tenant_Structure_LevelNum");

            entity.HasIndex(e => new { e.TenantId, e.StructureId, e.Code })
                .IsUnique()
                .HasDatabaseName("IX_LocLevel_Tenant_Structure_Code");

            entity.HasOne(e => e.Structure)
                .WithMany(e => e.LocationLevels)
                .HasForeignKey(e => e.StructureId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Locations)
                .WithOne(e => e.LocationLevel)
                .HasForeignKey(e => e.LocationLevelId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Location>(entity =>
        {
            // Self-referencing relationship
            entity.HasOne(e => e.ParentLocation)
                .WithMany(e => e.ChildLocations)
                .HasForeignKey(e => e.ParentLocationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with Structure
            entity.HasOne(e => e.Structure)
                .WithMany(e => e.Locations)
                .HasForeignKey(e => e.StructureId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with LocationLevel
            entity.HasOne(e => e.LocationLevel)
                .WithMany(e => e.Locations)
                .HasForeignKey(e => e.LocationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with Country
            entity.HasOne(e => e.Country)
                .WithMany()
                .HasForeignKey(e => e.CountryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Employees collection
            entity.HasMany(e => e.Employees)
                .WithOne(e => e.Location)
                .HasForeignKey(e => e.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Contacts collection
            entity.HasMany(e => e.LocationContacts)
                .WithOne(e => e.Location)
                .HasForeignKey(e => e.LocationId)
                .OnDelete(DeleteBehavior.Cascade); // Delete contacts when location is deleted

            // Indexes
            entity.HasIndex(e => new { e.TenantId, e.StructureId, e.Code })
                .IsUnique()
                .HasDatabaseName("IX_Location_Tenant_Structure_Code");

            entity.HasIndex(e => e.ParentLocationId)
                .HasDatabaseName("IX_Location_ParentId");

            entity.HasIndex(e => e.LocationLevelId)
                .HasDatabaseName("IX_Location_LevelId");

            entity.HasIndex(e => e.CountryId)
                .HasDatabaseName("IX_Location_CountryId");

            entity.HasIndex(e => new { e.StructureId, e.IsActive })
                .HasDatabaseName("IX_Location_Structure_Active");

            entity.HasIndex(e => new { e.ParentLocationId, e.Sequence })
                .HasDatabaseName("IX_Location_Parent_Sequence");
        });

        builder.Entity<LocationContact>(entity =>
        {
            entity.HasOne(e => e.Location)
                .WithMany(e => e.LocationContacts)
                .HasForeignKey(e => e.LocationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            entity.HasIndex(e => e.LocationId)
                .HasDatabaseName("IX_LocContact_LocationId");

            entity.HasIndex(e => new { e.LocationId, e.IsPrimary })
                .HasDatabaseName("IX_LocContact_Location_Primary");

            entity.HasIndex(e => e.EmployeeId)
                .HasDatabaseName("IX_LocContact_EmployeeId");
        });

        builder.Entity<SalaryGrade>(entity =>
        {
            entity.HasMany(e => e.Levels)
                .WithOne(e => e.Grade)
                .HasForeignKey(e => e.SalaryGradeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.TenantId, e.Code })
                .IsUnique()
                .HasDatabaseName("IX_SalaryGrade_Tenant_Code");

            entity.HasIndex(e => new { e.TenantId, e.IsActive })
                .HasDatabaseName("IX_SalaryGrade_Tenant_Active");

            entity.HasIndex(e => new { e.TenantId, e.EffectiveDate })
                .HasDatabaseName("IX_SalaryGrade_Tenant_EffectiveDate");
        });

        builder.Entity<SalaryLevel>(entity =>
        {
            entity.HasOne(e => e.Grade)
                .WithMany(e => e.Levels)
                .HasForeignKey(e => e.SalaryGradeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Notches)
                .WithOne(e => e.Level)
                .HasForeignKey(e => e.SalaryLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.SalaryGradeId)
                .HasDatabaseName("IX_SalaryLevel_GradeId");

            entity.HasIndex(e => new { e.TenantId, e.SalaryGradeId, e.Code })
                .IsUnique()
                .HasDatabaseName("IX_SalaryLevel_Tenant_Grade_Code");

            entity.HasIndex(e => new { e.TenantId, e.SalaryGradeId, e.Sequence })
                .IsUnique()
                .HasDatabaseName("IX_SalaryLevel_Tenant_Grade_Sequence");

            entity.HasIndex(e => new { e.TenantId, e.IsActive })
                .HasDatabaseName("IX_SalaryLevel_Tenant_Active");
        });

        builder.Entity<SalaryNotch>(entity =>
        {
            entity.HasOne(e => e.Level)
                .WithMany(e => e.Notches)
                .HasForeignKey(e => e.SalaryLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.SalaryLevelId)
                .HasDatabaseName("IX_SalaryNotch_LevelId");

            entity.HasIndex(e => new { e.TenantId, e.SalaryLevelId, e.NotchNumber })
                .IsUnique()
                .HasDatabaseName("IX_SalaryNotch_Tenant_Level_NotchNumber");

            entity.HasIndex(e => new { e.TenantId, e.IsActive })
                .HasDatabaseName("IX_SalaryNotch_Tenant_Active");
        });

        // Configure Employee entity
        builder.Entity<Employee>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.EmployeeNumber })
                  .IsUnique()
                  .HasDatabaseName("IX_Employee_Tenant_EmployeeNumber");

            entity.HasIndex(e => new { e.TenantId, e.EmailAddress })
                  .IsUnique()
                  .HasDatabaseName("IX_Employee_Tenant_EmailAddress");

            entity.HasIndex(e => e.EmployeeNumber).IsUnique();
            entity.HasIndex(e => e.CorporateEmployeeID);
            entity.HasIndex(e => e.EmailAddress).IsUnique();
            entity.HasIndex(e => e.OrganizationLevelId);
            entity.HasIndex(e => e.OrganizationUnitId);
            entity.HasIndex(e => e.LocationLevelId);
            entity.HasIndex(e => e.LocationId);
            entity.HasIndex(e => e.CountryId);
            entity.HasIndex(e => e.ShiftId);
            entity.HasIndex(e => e.DepartmentId);
            entity.HasIndex(e => e.SectionId);
            entity.HasIndex(e => e.PositionId);
            entity.HasIndex(e => e.StaffStatus);
            entity.HasIndex(e => e.ManagerId);
            entity.HasIndex(e => e.DateEmployed);
            entity.HasIndex(e => e.IsActive);

            // Self-referencing relationship for Manager/DirectReports
            entity.HasOne(e => e.Manager)
                .WithMany(m => m.DirectReports)
                .HasForeignKey(e => e.ManagerId)
                .OnDelete(DeleteBehavior.NoAction); // Prevent cascading deletes

            // Division relationship
            entity.HasOne(e => e.Division)
                .WithMany(d => d.Employees)
                .HasForeignKey(e => e.DivisionId)
                .OnDelete(DeleteBehavior.NoAction);

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

            // Unit relationship
            entity.HasOne(e => e.Unit)
                .WithMany(u => u.Employees)
                .HasForeignKey(e => e.UnitId)
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

            entity.HasOne(e => e.LocationLevel)
                .WithMany()
                .HasForeignKey(e => e.LocationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.OrganizationLevel)
                .WithMany()
                .HasForeignKey(e => e.OrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);
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

        // Configure Section entity
        builder.Entity<Unit>(entity =>
        {
            entity.HasIndex(u => u.Name);
            entity.HasIndex(u => u.Code);
            entity.HasIndex(u => u.UnitHeadId);
            entity.HasIndex(u => u.IsActive);

            // Section relationship
            entity.HasOne(u => u.Section)
                .WithMany(s => s.Units)
                .HasForeignKey(u => u.SectionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Unit head relationship
            entity.HasOne(u => u.UnitHead)
                .WithMany()
                .HasForeignKey(u => u.UnitHeadId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure Division entity
        builder.Entity<Division>(entity =>
        {
            entity.HasIndex(d => d.Name);
            entity.HasIndex(d => d.Code);
            entity.HasIndex(d => d.DivisionHeadId);
            entity.HasIndex(d => d.IsActive);

            // Division head relationship
            entity.HasOne(d => d.DivisionHead)
                .WithMany()
                .HasForeignKey(d => d.DivisionHeadId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<EmployeeContact>(entity =>
        {
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => new { e.EmployeeId, e.IsPrimary });
            entity.HasIndex(e => e.ContactType);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.Contacts)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Country)
                .WithMany()
                .HasForeignKey(e => e.CountryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Position History entity
        builder.Entity<EmployeePositionHistory>(entity =>
        {
            entity.Property(h => h.ChangeReason).HasConversion<int>();

            entity.HasIndex(h => h.EmployeeId);
            entity.HasIndex(h => h.PositionId);
            entity.HasIndex(h => h.StartDate);
            entity.HasIndex(h => h.EndDate);
            entity.HasIndex(h => new { h.EmployeeId, h.StartDate });

            entity.HasOne(h => h.Employee)
                .WithMany(e => e.PositionHistories)
                .HasForeignKey(h => h.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(h => h.LocationLevel)
                .WithMany()
                .HasForeignKey(h => h.LocationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(h => h.Location)
                .WithMany()
                .HasForeignKey(h => h.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(h => h.OrganizationLevel)
                .WithMany()
                .HasForeignKey(h => h.OrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(h => h.OrganizationUnit)
                .WithMany()
                .HasForeignKey(h => h.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(h => h.Position)
                .WithMany(p => p.PositionHistories)
                .HasForeignKey(h => h.PositionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeEmergencyContact>(entity =>
        {
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => new { e.EmployeeId, e.IsPrimary });
            entity.HasIndex(e => e.IsActive);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.EmergencyContacts)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Country)
                .WithMany()
                .HasForeignKey(e => e.CountryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeDependent>(entity =>
        {
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.Relationship);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.Dependents)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.EmployeeDependentBenefits)
                .WithOne(e => e.EmployeeDependent)
                .HasForeignKey(e => e.EmployeeDependentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EmployeeDependentBenefit>(entity =>
        {
            entity.HasIndex(e => e.EmployeeDependentId);
            entity.HasIndex(e => e.PolicyId);
            entity.HasIndex(e => e.EnrolledDate);
            entity.HasIndex(e => e.IsActive);

            entity.HasOne(e => e.EmployeeDependent)
                .WithMany(d => d.EmployeeDependentBenefits)
                .HasForeignKey(e => e.EmployeeDependentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.BenefitPolicy)
                .WithMany()
                .HasForeignKey(e => e.PolicyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeQualification>(entity =>
        {
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.QualificationId);
            entity.HasIndex(e => e.CountryId);
            entity.HasIndex(e => e.IsVerified);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.Qualifications)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Qualification)
                .WithMany()
                .HasForeignKey(e => e.QualificationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Country)
                .WithMany()
                .HasForeignKey(e => e.CountryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<IdentificationType>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Name })
                .IsUnique()
                .HasDatabaseName("IX_IdentificationType_Tenant_Name");

            entity.HasIndex(e => new { e.TenantId, e.Code })
                .HasDatabaseName("IX_IdentificationType_Tenant_Code");

            entity.HasIndex(e => e.IsActive);

            entity.HasOne(e => e.IssuingCountry)
                .WithMany()
                .HasForeignKey(e => e.IssuingCountryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeIdentificationCard>(entity =>
        {
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.IdentificationTypeId);
            entity.HasIndex(e => e.DocumentNumber);
            entity.HasIndex(e => e.IsVerified);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.IdentificationCards)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.IdentificationType)
                .WithMany(t => t.EmployeeIdentificationCards)
                .HasForeignKey(e => e.IdentificationTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeWorkHistory>(entity =>
        {
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.StartDate);
            entity.HasIndex(e => e.EndDate);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.WorkHistories)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EmployeeContractDetail>(entity =>
        {
            entity.Property(e => e.EmploymentType).HasConversion<int>();
            entity.Property(e => e.PayFrequency).HasConversion<int>();
            entity.Property(e => e.TaxTreatmentType).HasConversion<int>();
            entity.Property(e => e.ContractStatus).HasConversion<int>();

            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => new { e.TenantId, e.ContractNumber })
                .IsUnique()
                .HasDatabaseName("IX_EmployeeContractDetail_Tenant_ContractNumber");
            entity.HasIndex(e => e.IsActive);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.ContractDetails)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ExpatriateAssignment>(entity =>
        {
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.HomeCountryId);
            entity.HasIndex(e => new { e.EmployeeId, e.StartDate });

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.ExpatriateAssignments)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Country)
                .WithMany()
                .HasForeignKey(e => e.HomeCountryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeePositionHistory>(entity =>
        {
            entity.Property(h => h.ChangeReason).HasConversion<int>();

            entity.HasIndex(h => h.EmployeeId);
            entity.HasIndex(h => h.PositionId);
            entity.HasIndex(h => h.StartDate);
            entity.HasIndex(h => h.EndDate);
            entity.HasIndex(h => new { h.EmployeeId, h.StartDate });

            entity.HasOne(h => h.Employee)
                .WithMany(e => e.PositionHistories)
                .HasForeignKey(h => h.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(h => h.LocationLevel)
                .WithMany()
                .HasForeignKey(h => h.LocationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(h => h.Location)
                .WithMany()
                .HasForeignKey(h => h.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(h => h.OrganizationLevel)
                .WithMany()
                .HasForeignKey(h => h.OrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(h => h.OrganizationUnit)
                .WithMany()
                .HasForeignKey(h => h.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(h => h.Position)
                .WithMany(p => p.PositionHistories)
                .HasForeignKey(h => h.PositionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeSalaryAssignment>(entity =>
        {
            entity.Property(e => e.AssignmentReason)
                .HasMaxLength(200);

            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.GradeId);
            entity.HasIndex(e => e.LevelId);
            entity.HasIndex(e => e.NotchId);
            entity.HasIndex(e => e.EffectiveDate);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.SalaryAssignments)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Grade)
                .WithMany()
                .HasForeignKey(e => e.GradeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Level)
                .WithMany()
                .HasForeignKey(e => e.LevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Notch)
                .WithMany()
                .HasForeignKey(e => e.NotchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeReferee>(entity =>
        {
            entity.Property(e => e.RefereeType).HasConversion<int>();

            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.RefereeType);
            entity.HasIndex(e => new { e.EmployeeId, e.IsPrimary });
            entity.HasIndex(e => e.IsActive);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.Referees)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EmployeeGuarantor>(entity =>
        {
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.CountryId);
            entity.HasIndex(e => e.VerifiedByEmployeeId);
            entity.HasIndex(e => e.IsPrimary);
            entity.HasIndex(e => e.IsVerified);
            entity.HasIndex(e => e.IsActive);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.Guarantors)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.VerifiedByEmployee)
                .WithMany()
                .HasForeignKey(e => e.VerifiedByEmployeeId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Country)
                .WithMany()
                .HasForeignKey(e => e.CountryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeBank>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.Code })
                .IsUnique()
                .HasDatabaseName("IX_EmployeeBank_Tenant_Code");

            entity.HasOne(e => e.Country)
                .WithMany()
                .HasForeignKey(e => e.CountryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeBankBranch>(entity =>
        {
            entity.HasIndex(e => e.BankId);
            entity.HasIndex(e => new { e.TenantId, e.BankId, e.Code })
                .HasDatabaseName("IX_EmployeeBankBranch_Tenant_Bank_Code");

            entity.HasOne(e => e.Bank)
                .WithMany(b => b.Branches)
                .HasForeignKey(e => e.BankId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Country)
                .WithMany()
                .HasForeignKey(e => e.CountryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeBankDetail>(entity =>
        {
            entity.Property(e => e.AccountType).HasConversion<int>();

            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.AccountNumber })
                .IsUnique()
                .HasDatabaseName("IX_EmployeeBankDetail_Tenant_Employee_AccountNumber");

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.BankDetails)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Bank)
                .WithMany()
                .HasForeignKey(e => e.BankId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Branch)
                .WithMany()
                .HasForeignKey(e => e.BranchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeSkill>(entity =>
        {
            entity.Property(e => e.SkillLevel).HasConversion<int>();

            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.SkillId);
            entity.HasIndex(e => new { e.TenantId, e.EmployeeId, e.SkillId })
                .IsUnique()
                .HasDatabaseName("IX_EmployeeSkill_Tenant_Employee_Skill");

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.Skills)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Skill)
                .WithMany(s => s.EmployeeSkills)
                .HasForeignKey(e => e.SkillId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AttendanceRecord>(entity =>
        {
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => new { e.EmployeeId, e.Date }).IsUnique(false);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.AttendanceRecords)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EmployeeBiometric>(entity =>
        {
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.IsActive);

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.Biometrics)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ShiftAssignment>(entity =>
        {
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.ShiftId);
            entity.HasIndex(e => new { e.EmployeeId, e.StartDate });

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.ShiftAssignments)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Shift)
                .WithMany(s => s.ShiftAssignments)
                .HasForeignKey(e => e.ShiftId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeShiftPreference>(entity =>
        {
            entity.HasIndex(e => e.EmployeeId);
            entity.HasIndex(e => e.ShiftId);
            entity.HasIndex(e => new { e.EmployeeId, e.ShiftId });

            entity.HasOne(e => e.Employee)
                .WithMany(e => e.ShiftPreferences)
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Shift)
                .WithMany()
                .HasForeignKey(e => e.ShiftId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StaffLevel>(entity =>
        {
            entity.Property(sl => sl.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(sl => sl.Code)
                .HasMaxLength(50);

            entity.Property(sl => sl.Description)
                .HasMaxLength(1000);

            entity.Property(sl => sl.Rank)
                .HasDefaultValue(1);

            entity.Property(sl => sl.IsActive)
                .HasDefaultValue(true);

            // Indexes (avoid unique constraints since Code defaults to empty string)
            entity.HasIndex(sl => new { sl.TenantId, sl.Name })
                .HasDatabaseName("IX_StaffLevel_Tenant_Name");

            entity.HasIndex(sl => new { sl.TenantId, sl.Code })
                .HasDatabaseName("IX_StaffLevel_Tenant_Code");

            entity.HasIndex(sl => new { sl.TenantId, sl.Rank })
                .HasDatabaseName("IX_StaffLevel_Tenant_Rank");

            entity.HasIndex(sl => new { sl.TenantId, sl.IsActive })
                .HasDatabaseName("IX_StaffLevel_Tenant_Active");

            // Relationship: StaffLevel (1) -> EmployeePosition (many)
            entity.HasMany(sl => sl.EmployeePositions)
                .WithOne(ep => ep.StaffLevel)
                .HasForeignKey(ep => ep.StaffLevelId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeePosition>(entity =>
        {
            entity.Property(ep => ep.Title)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(ep => ep.Code)
                .HasMaxLength(20);

            entity.Property(ep => ep.Description)
                .HasMaxLength(1000);

            entity.Property(ep => ep.IsActive)
                .HasDefaultValue(true);

            // Indexes (avoid unique constraints since Code defaults to empty string)
            entity.HasIndex(ep => new { ep.TenantId, ep.Title })
                .HasDatabaseName("IX_EmployeePosition_Tenant_Title");

            entity.HasIndex(ep => new { ep.TenantId, ep.Code })
                .HasDatabaseName("IX_EmployeePosition_Tenant_Code");

            entity.HasIndex(ep => new { ep.TenantId, ep.OrganizationUnitId })
                .HasDatabaseName("IX_EmployeePosition_Tenant_OrgUnit");

            entity.HasIndex(ep => new { ep.TenantId, ep.IsActive })
                .HasDatabaseName("IX_EmployeePosition_Tenant_Active");

            entity.HasIndex(ep => ep.StaffLevelId)
                .HasDatabaseName("IX_EmployeePosition_StaffLevelId");

            entity.HasIndex(ep => ep.ReportsToPositionId)
                .HasDatabaseName("IX_EmployeePosition_ReportsToPositionId");

            // Relationships
            entity.HasOne(ep => ep.OrganizationUnit)
                .WithMany(ou => ou.Positions)
                .HasForeignKey(ep => ep.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ep => ep.ReportsToPosition)
                .WithMany()
                .HasForeignKey(ep => ep.ReportsToPositionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Skill>(entity =>
        {
            entity.Property(s => s.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(s => s.Description)
                .HasMaxLength(1000);

            entity.Property(s => s.Category)
                .HasMaxLength(100);

            entity.Property(s => s.IsActive)
                .HasDefaultValue(true);

            entity.Property(s => s.RequiresCertification)
                .HasDefaultValue(false);

            entity.HasIndex(s => new { s.TenantId, s.Name })
                .IsUnique()
                .HasDatabaseName("IX_Skill_Tenant_Name");

            entity.HasIndex(s => new { s.TenantId, s.Category })
                .HasDatabaseName("IX_Skill_Tenant_Category");

            entity.HasIndex(s => new { s.TenantId, s.IsActive })
                .HasDatabaseName("IX_Skill_Tenant_Active");
        });

        builder.Entity<PositionSkillRequirement>(entity =>
        {
            entity.Property(psr => psr.RequiredLevel)
                .HasConversion<int>();

            entity.Property(psr => psr.IsRequired)
                .HasDefaultValue(true);

            entity.Property(psr => psr.Priority)
                .HasDefaultValue(1);

            entity.HasIndex(psr => new { psr.TenantId, psr.PositionId, psr.SkillId })
                .IsUnique()
                .HasDatabaseName("IX_PositionSkillRequirement_Tenant_Position_Skill");

            entity.HasIndex(psr => new { psr.TenantId, psr.PositionId })
                .HasDatabaseName("IX_PositionSkillRequirement_Tenant_PositionId");

            entity.HasIndex(psr => new { psr.TenantId, psr.SkillId })
                .HasDatabaseName("IX_PositionSkillRequirement_Tenant_SkillId");

            entity.HasOne(psr => psr.Position)
                .WithMany(p => p.SkillRequirements)
                .HasForeignKey(psr => psr.PositionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(psr => psr.Skill)
                .WithMany(s => s.PositionRequirements)
                .HasForeignKey(psr => psr.SkillId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<BenefitPolicy>(entity =>
        {
            entity.Property(bp => bp.PolicyName)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(bp => bp.PolicyCode)
                .HasMaxLength(50);

            entity.Property(bp => bp.Description)
                .HasMaxLength(1000);

            entity.Property(bp => bp.PolicyType)
                .HasConversion<int>()
                .HasDefaultValue(BenefitPolicyType.Medical);

            entity.Property(bp => bp.Recipient)
                .HasConversion<int>()
                .HasDefaultValue(BenefitRecipient.Staff);

            entity.Property(bp => bp.LimitPeriod)
                .HasConversion<int>()
                .HasDefaultValue(BenefitLimitPeriod.Annual);

            entity.Property(bp => bp.IsActive)
                .HasDefaultValue(true);

            entity.HasIndex(bp => new { bp.TenantId, bp.PolicyName })
                .HasDatabaseName("IX_BenefitPolicy_Tenant_Name");

            entity.HasIndex(bp => new { bp.TenantId, bp.PolicyCode })
                .IsUnique()
                .HasDatabaseName("IX_BenefitPolicy_Tenant_Code");

            entity.HasIndex(bp => new { bp.TenantId, bp.PolicyType })
                .HasDatabaseName("IX_BenefitPolicy_Tenant_Type");

            entity.HasIndex(bp => new { bp.TenantId, bp.IsActive })
                .HasDatabaseName("IX_BenefitPolicy_Tenant_Active");

            entity.HasIndex(bp => new { bp.TenantId, bp.EffectiveFrom })
                .HasDatabaseName("IX_BenefitPolicy_Tenant_EffectiveFrom");

            entity.HasMany(bp => bp.BenefitPolicyRelations)
                .WithOne(r => r.BenefitPolicy)
                .HasForeignKey(r => r.BenefitPolicyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(bp => bp.PositionBenefits)
                .WithOne(pb => pb.BenefitPolicy)
                .HasForeignKey(pb => pb.PolicyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<BenefitPolicyRelation>(entity =>
        {
            entity.Property(r => r.RelationType)
                .HasConversion<int>();

            entity.Property(r => r.IsActive)
                .HasDefaultValue(true);

            entity.HasIndex(r => new { r.TenantId, r.BenefitPolicyId })
                .HasDatabaseName("IX_BenefitPolicyRelation_Tenant_PolicyId");

            entity.HasIndex(r => new { r.TenantId, r.BenefitPolicyId, r.RelationType })
                .IsUnique()
                .HasDatabaseName("IX_BenefitPolicyRelation_Tenant_Policy_RelationType");
        });

        builder.Entity<EmployeePositionBenefit>(entity =>
        {
            entity.HasIndex(pb => new { pb.TenantId, pb.PositionId, pb.PolicyId })
                .IsUnique()
                .HasDatabaseName("IX_EmployeePositionBenefit_Tenant_Position_Policy");

            entity.HasOne(pb => pb.Position)
                .WithMany(p => p.PositionBenefits)
                .HasForeignKey(pb => pb.PositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(pb => pb.BenefitPolicy)
                .WithMany(bp => bp.PositionBenefits)
                .HasForeignKey(pb => pb.PolicyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Leave Balance entity
        builder.Entity<LeaveBalance>(entity =>
        {
            entity.HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.Year }).IsUnique();
            entity.HasIndex(x => x.Year);

            entity.HasOne(x => x.LeaveType)
                .WithMany(x => x.LeaveBalances)
                .HasForeignKey(x => x.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany(e => e.LeaveBalances)
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Leave Request entity
        builder.Entity<LeaveRequest>(entity =>
        {
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.LeaveTypeId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => new { x.StartDate, x.EndDate });

            // Relationship with Employee
            entity.HasOne(x => x.Employee)
                .WithMany(x => x.LeaveRequests)
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with Reliever
            entity.HasOne(x => x.RelieverEmployee)
                .WithMany()
                .HasForeignKey(x => x.RelieverEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with Approver
            entity.HasOne(x => x.ApprovedByEmployee)
                .WithMany()
                .HasForeignKey(x => x.ApprovedByEmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with Leave Type
            entity.HasOne(x => x.LeaveType)
                .WithMany(x => x.LeaveRequests)
                .HasForeignKey(x => x.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with Leave Plan
            entity.HasOne(x => x.LeavePlan)
                .WithMany()
                .HasForeignKey(x => x.LeavePlanId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Leave Plan entity
        builder.Entity<LeavePlan>(entity =>
        {
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.LeaveTypeId);
            entity.HasIndex(x => x.DepartmentId);
            entity.HasIndex(x => x.PlannedBy);

            // Relationship with Reliever
            entity.HasOne(x => x.RelieverEmployee)
                .WithMany()
                .HasForeignKey(x => x.RelieverId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relationship with Planner
            entity.HasOne(x => x.PlannedByEmployee)
                .WithMany()
                .HasForeignKey(x => x.PlannedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Employee)
                .WithMany(e => e.LeavePlans)
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Department)
                .WithMany()
                .HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.LeaveType)
                .WithMany()
                .HasForeignKey(x => x.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Leave Sub Type entity
        builder.Entity<LeaveSubType>(entity =>
        {
            entity.HasOne(x => x.LeaveType)
                .WithMany(x => x.LeaveSubTypes)
                .HasForeignKey(x => x.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Leave Category Allocation entity
        builder.Entity<LeaveCategoryAllocation>(entity =>
        {
            entity.HasOne(x => x.LeaveType)
                .WithMany(x => x.LeaveCategoryAllocations)
                .HasForeignKey(x => x.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Public Holiday entity
        builder.Entity<PublicHoliday>(entity =>
        {
            entity.HasIndex(x => x.Date);
            entity.HasIndex(x => x.Year);
        });

        builder.Entity<AppraisalGradeDefinition>(entity =>
        {
            entity.HasIndex(x => x.GradeName);

            entity.HasMany(x => x.MappingGradeRanges)
                .WithOne(x => x.GradeDefinition)
                .HasForeignKey(x => x.GradeDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AppraisalCriteria>(entity =>
        {
            entity.HasIndex(x => x.CriteriaName);
            entity.HasIndex(x => x.Code);
            entity.HasIndex(x => x.CriteriaType);

            entity.HasOne(x => x.KpiDefinition)
                .WithMany(x => x.AppraisalCriterias)
                .HasForeignKey(x => x.KpiDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<KpiDefinition>(entity =>
        {
            entity.HasIndex(x => x.KpiName);
            entity.HasIndex(x => x.MeasurementType);

            entity.HasMany(x => x.EmployeeKpiTargets)
                .WithOne(x => x.KpiDefinition)
                .HasForeignKey(x => x.KpiDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PositionCriteriaMapping>(entity =>
        {
            entity.HasIndex(x => x.DepartmentId);
            entity.HasIndex(x => x.PositionId);
            entity.HasIndex(x => new { x.CriteriaId, x.PositionId }).IsUnique(false);

            entity.HasOne(x => x.Department)
                .WithMany()
                .HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Position)
                .WithMany()
                .HasForeignKey(x => x.PositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AppraisalCriteria)
                .WithMany(x => x.PositionCriteriaMappings)
                .HasForeignKey(x => x.CriteriaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MappingGradeRange>(entity =>
        {
            entity.HasIndex(x => x.PositionCriteriaMappingId);
            entity.HasIndex(x => x.GradeDefinitionId);

            entity.HasOne(x => x.PositionCriteriaMapping)
                .WithMany(x => x.MappingGradeRanges)
                .HasForeignKey(x => x.PositionCriteriaMappingId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PerformanceAppraisal>(entity =>
        {
            entity.HasIndex(x => x.AppraisalNumber).IsUnique(false);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.Year);
            entity.HasIndex(x => x.AppraisalType);
            entity.HasIndex(x => x.Status);

            entity.HasOne(x => x.Employee)
                .WithMany(x => x.PerformanceAppraisals)
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.EvaluatorEvaluations)
                .WithOne(x => x.Appraisal)
                .HasForeignKey(x => x.AppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.EmployeeResponses)
                .WithOne(x => x.Appraisal)
                .HasForeignKey(x => x.AppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Attachments)
                .WithOne(x => x.Appraisal)
                .HasForeignKey(x => x.AppraisalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EvaluatorEvaluation>(entity =>
        {
            entity.HasIndex(x => x.AppraisalId);
            entity.HasIndex(x => x.EvaluatorId);
            entity.HasIndex(x => new { x.AppraisalId, x.EvaluatorId }).IsUnique(false);

            entity.HasOne(x => x.Appraisal)
                .WithMany(x => x.EvaluatorEvaluations)
                .HasForeignKey(x => x.AppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Evaluator)
                .WithMany()
                .HasForeignKey(x => x.EvaluatorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.CriterionScores)
                .WithOne(x => x.EvaluatorEvaluation)
                .HasForeignKey(x => x.EvaluatorEvaluationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CriterionScore>(entity =>
        {
            entity.HasIndex(x => x.EvaluatorEvaluationId);
            entity.HasIndex(x => x.CriteriaId);
            entity.HasIndex(x => x.KpiEvaluationRecordId);

            entity.HasOne(x => x.EvaluatorEvaluation)
                .WithMany(x => x.CriterionScores)
                .HasForeignKey(x => x.EvaluatorEvaluationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AppraisalCriteria)
                .WithMany()
                .HasForeignKey(x => x.CriteriaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.KpiEvaluationRecord)
                .WithMany(x => x.CriterionScores)
                .HasForeignKey(x => x.KpiEvaluationRecordId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AppraisalEmployeeResponse>(entity =>
        {
            entity.HasIndex(x => x.AppraisalId);
            entity.HasIndex(x => x.CriteriaId);

            entity.HasOne(x => x.Appraisal)
                .WithMany(x => x.EmployeeResponses)
                .HasForeignKey(x => x.AppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AppraisalCriteria)
                .WithMany()
                .HasForeignKey(x => x.CriteriaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AppraisalAttachment>(entity =>
        {
            entity.HasIndex(x => x.AppraisalId);
            entity.HasIndex(x => x.FileName);

            entity.HasOne(x => x.Appraisal)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.AppraisalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeKpiTarget>(entity =>
        {
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.KpiDefinitionId);
            entity.HasIndex(x => x.PositionCriteriaMappingId);
            entity.HasIndex(x => new { x.EmployeeId, x.KpiDefinitionId, x.PeriodStart, x.PeriodEnd });

            // Employee
            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            // KPI Definition
            entity.HasOne(x => x.KpiDefinition)
                .WithMany(x => x.EmployeeKpiTargets)
                .HasForeignKey(x => x.KpiDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Position Criteria Mapping
            entity.HasOne(x => x.PositionCriteriaMapping)
                .WithMany()
                .HasForeignKey(x => x.PositionCriteriaMappingId)
                .OnDelete(DeleteBehavior.Restrict);

            // KPI Evaluation Records
            entity.HasMany(x => x.KpiEvaluationRecords)
                .WithOne(x => x.EmployeeKpiTarget)
                .HasForeignKey(x => x.EmployeeKpiTargetId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<KpiEvaluationRecord>(entity =>
        {
            entity.HasIndex(x => x.EmployeeKpiTargetId);
            entity.HasIndex(x => x.EvaluatorId);
            entity.HasIndex(x => x.IsSelfEvaluation);
            entity.HasIndex(x => x.EvaluationDate);

            entity.HasOne(x => x.EmployeeKpiTarget)
                .WithMany(x => x.KpiEvaluationRecords)
                .HasForeignKey(x => x.EmployeeKpiTargetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Evaluator)
                .WithMany()
                .HasForeignKey(x => x.EvaluatorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.CriterionScores)
                .WithOne(x => x.KpiEvaluationRecord)
                .HasForeignKey(x => x.KpiEvaluationRecordId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PerformanceImprovementPlan>(entity =>
        {
            entity.HasIndex(x => x.PipNumber).IsUnique(false);
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.AppraisalId);
            entity.HasIndex(x => x.SupervisorId);
            entity.HasIndex(x => x.Status);

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Appraisal)
                .WithMany()
                .HasForeignKey(x => x.AppraisalId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Supervisor)
                .WithMany()
                .HasForeignKey(x => x.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.ReviewMeetings)
                .WithOne(x => x.Pip)
                .HasForeignKey(x => x.PipId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PipReviewMeeting>(entity =>
        {
            entity.HasIndex(x => x.PipId);
            entity.HasIndex(x => x.ConductedById);
            entity.HasIndex(x => x.MeetingDate);

            entity.HasOne(x => x.Pip)
                .WithMany(x => x.ReviewMeetings)
                .HasForeignKey(x => x.PipId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.ConductedBy)
                .WithMany()
                .HasForeignKey(x => x.ConductedById)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureBusinessPartnerEntities(ModelBuilder builder)
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
            entity.HasIndex(bp => bp.UserId);
            entity.HasIndex(bp => bp.ParentId);

            entity.HasOne(bp => bp.User)
                .WithMany()
                .HasForeignKey(bp => bp.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(bp => bp.ApprovedBy)
                .WithMany()
                .HasForeignKey(bp => bp.ApprovedById)
                .OnDelete(DeleteBehavior.NoAction);

            // Self-referential relationship for parent/child business partners
            entity.HasOne(bp => bp.Parent)
                .WithMany(bp => bp.Children)
                .HasForeignKey(bp => bp.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
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

        // Configure BusinessPartnerUser entity
        builder.Entity<BusinessPartnerUser>(entity =>
        {
            entity.HasKey(bpu => bpu.Id);
            entity.HasIndex(bpu => new { bpu.BusinessPartnerId, bpu.UserId }).IsUnique();
            entity.HasIndex(bpu => bpu.BusinessPartnerId);
            entity.HasIndex(bpu => bpu.UserId);
            entity.HasIndex(bpu => bpu.Role);
            entity.HasIndex(bpu => bpu.IsActive);

            entity.HasOne(bpu => bpu.BusinessPartner)
                .WithMany()
                .HasForeignKey(bpu => bpu.BusinessPartnerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(bpu => bpu.User)
                .WithMany()
                .HasForeignKey(bpu => bpu.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(bpu => bpu.GrantedBy)
                .WithMany()
                .HasForeignKey(bpu => bpu.GrantedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure TenderAssignment entity
        builder.Entity<TenderAssignment>(entity =>
        {
            entity.HasKey(ta => ta.Id);
            entity.HasIndex(ta => new { ta.TenderId, ta.BusinessPartnerId, ta.AssignedToUserId });
            entity.HasIndex(ta => ta.TenderId);
            entity.HasIndex(ta => ta.BusinessPartnerId);
            entity.HasIndex(ta => ta.AssignedToUserId);
            entity.HasIndex(ta => ta.AssignmentType);

            entity.HasOne(ta => ta.Tender)
                .WithMany()
                .HasForeignKey(ta => ta.TenderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ta => ta.BusinessPartner)
                .WithMany()
                .HasForeignKey(ta => ta.BusinessPartnerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ta => ta.AssignedToUser)
                .WithMany()
                .HasForeignKey(ta => ta.AssignedToUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(ta => ta.AssignedBy)
                .WithMany()
                .HasForeignKey(ta => ta.AssignedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure SupplierPerformanceMetric entity
        builder.Entity<SupplierPerformanceMetric>(entity =>
        {
            entity.HasIndex(spm => spm.BusinessPartnerId);
            entity.HasIndex(spm => new { spm.BusinessPartnerId, spm.MetricPeriod, spm.Year, spm.Month, spm.Quarter }).IsUnique();
            entity.HasIndex(spm => spm.Year);
            entity.HasIndex(spm => spm.OverallPerformanceScore);

            entity.HasOne(spm => spm.BusinessPartner)
                .WithMany()
                .HasForeignKey(spm => spm.BusinessPartnerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(spm => spm.CalculatedBy)
                .WithMany()
                .HasForeignKey(spm => spm.CalculatedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure QualityIncident entity
        builder.Entity<QualityIncident>(entity =>
        {
            entity.HasIndex(qi => qi.IncidentNumber).IsUnique();
            entity.HasIndex(qi => qi.BusinessPartnerId);
            entity.HasIndex(qi => qi.Status);
            entity.HasIndex(qi => qi.Severity);
            entity.HasIndex(qi => qi.IncidentDate);

            entity.HasOne(qi => qi.BusinessPartner)
                .WithMany()
                .HasForeignKey(qi => qi.BusinessPartnerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(qi => qi.ReportedBy)
                .WithMany()
                .HasForeignKey(qi => qi.ReportedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(qi => qi.AcknowledgedBy)
                .WithMany()
                .HasForeignKey(qi => qi.AcknowledgedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(qi => qi.ResolvedBy)
                .WithMany()
                .HasForeignKey(qi => qi.ResolvedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure PerformanceReview entity
        builder.Entity<PerformanceReview>(entity =>
        {
            entity.HasIndex(pr => pr.ReviewNumber).IsUnique();
            entity.HasIndex(pr => pr.BusinessPartnerId);
            entity.HasIndex(pr => pr.Status);
            entity.HasIndex(pr => pr.ReviewDate);

            entity.HasOne(pr => pr.BusinessPartner)
                .WithMany()
                .HasForeignKey(pr => pr.BusinessPartnerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pr => pr.ReviewedBy)
                .WithMany()
                .HasForeignKey(pr => pr.ReviewedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(pr => pr.AcknowledgedBy)
                .WithMany()
                .HasForeignKey(pr => pr.AcknowledgedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Configure PerformanceBondRequest entity
        builder.Entity<PerformanceBondRequest>(entity =>
        {
            entity.HasIndex(pb => pb.TenderAwardId);
            entity.HasIndex(pb => pb.TenderBidId);
            entity.HasIndex(pb => pb.BusinessPartnerId);
            entity.HasIndex(pb => pb.Status);

            entity.HasOne(pb => pb.TenderAward)
                .WithMany()
                .HasForeignKey(pb => pb.TenderAwardId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pb => pb.TenderBid)
                .WithMany()
                .HasForeignKey(pb => pb.TenderBidId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(pb => pb.BusinessPartner)
                .WithMany()
                .HasForeignKey(pb => pb.BusinessPartnerId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(pb => pb.RequestedBy)
                .WithMany()
                .HasForeignKey(pb => pb.RequestedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(pb => pb.ReviewedBy)
                .WithMany()
                .HasForeignKey(pb => pb.ReviewedById)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    protected virtual void ApplyGlobalFilters(ModelBuilder builder)
    {
        // Apply soft delete filter to all entities that inherit from BaseEntity
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            var type = entityType.ClrType;

            // IMPORTANT:
            // EF Core supports only one query filter per entity. Multiple calls to HasQueryFilter overwrite.
            // Because TenantEntity inherits BaseEntity, we must apply a single combined filter when tenant scoping is enabled,
            // otherwise the tenant filter would override the soft delete filter (and soft-deleted records will reappear).
            if (typeof(TenantEntity).IsAssignableFrom(type) && _tenantId.HasValue)
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(SetTenantSoftDeleteFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(type);
                method.Invoke(null, new object[] { builder, entityType, _tenantId.Value });
                continue;
            }

            if (typeof(BaseEntity).IsAssignableFrom(type))
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(SetSoftDeleteFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(type);
                method.Invoke(null, new object[] { builder, entityType });
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

        builder.Entity<EhcRootCauseCode>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Name });
            entity.HasIndex(x => new { x.TenantId, x.IsActive });
        });

        builder.Entity<EhcCannedResponse>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.IsActive });
            entity.HasIndex(x => new { x.TenantId, x.AppliesToType });
            entity.HasIndex(x => new { x.TenantId, x.CategoryId });

            entity.HasOne(x => x.Category)
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
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
            entity.HasIndex(x => new { x.TenantId, x.RootCauseId });

            entity.HasOne(x => x.Category)
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Subcategory)
                .WithMany()
                .HasForeignKey(x => x.SubcategoryId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.RootCause)
                .WithMany()
                .HasForeignKey(x => x.RootCauseId)
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

            entity.HasMany(x => x.Watchers)
                .WithOne(w => w.Ticket)
                .HasForeignKey(w => w.TicketId)
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

        builder.Entity<EhcTicketFeedback>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.TicketId });
            entity.HasIndex(x => new { x.TenantId, x.TicketId, x.SubmittedByUserId }).IsUnique();

            entity.HasOne(x => x.SubmittedByUser)
                .WithMany()
                .HasForeignKey(x => x.SubmittedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<EhcTicketWatcher>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.TicketId });
            entity.HasIndex(x => new { x.TenantId, x.TicketId, x.UserId }).IsUnique();

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<EhcTicketLink>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.TicketId });
            entity.HasIndex(x => new { x.TenantId, x.RelatedTicketId });
            entity.HasIndex(x => new { x.TenantId, x.TicketId, x.RelatedTicketId, x.LinkType }).IsUnique();

            entity.HasOne(x => x.Ticket)
                .WithMany(t => t.Links)
                .HasForeignKey(x => x.TicketId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.RelatedTicket)
                .WithMany()
                .HasForeignKey(x => x.RelatedTicketId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<EhcProblem>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.ProblemNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.HasIndex(x => new { x.TenantId, x.Priority });
            entity.HasIndex(x => new { x.TenantId, x.DepartmentId });
            entity.HasIndex(x => new { x.TenantId, x.OwnerUserId });

            entity.HasOne(x => x.Category)
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Subcategory)
                .WithMany()
                .HasForeignKey(x => x.SubcategoryId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Department)
                .WithMany()
                .HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.OwnerUser)
                .WithMany()
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.CreatedFromTicket)
                .WithMany()
                .HasForeignKey(x => x.CreatedFromTicketId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.RootCause)
                .WithMany()
                .HasForeignKey(x => x.RootCauseId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(x => x.TicketLinks)
                .WithOne(l => l.Problem)
                .HasForeignKey(l => l.ProblemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.CapaTasks)
                .WithOne(t => t.Problem)
                .HasForeignKey(t => t.ProblemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.AuditEvents)
                .WithOne(a => a.Problem)
                .HasForeignKey(a => a.ProblemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EhcProblemTicketLink>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.ProblemId });
            entity.HasIndex(x => new { x.TenantId, x.TicketId });
            entity.HasIndex(x => new { x.TenantId, x.ProblemId, x.TicketId }).IsUnique();

            entity.HasOne(x => x.Ticket)
                .WithMany()
                .HasForeignKey(x => x.TicketId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<EhcCapaTask>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.ProblemId });
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.HasIndex(x => new { x.TenantId, x.AssignedToUserId });
            entity.HasIndex(x => new { x.TenantId, x.AssignedDepartmentId });
            entity.HasIndex(x => new { x.TenantId, x.DueAt });

            entity.HasOne(x => x.AssignedToUser)
                .WithMany()
                .HasForeignKey(x => x.AssignedToUserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.AssignedDepartment)
                .WithMany()
                .HasForeignKey(x => x.AssignedDepartmentId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<EhcProblemAuditEvent>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.ProblemId });
            entity.HasIndex(x => new { x.TenantId, x.EventType });
            entity.HasIndex(x => new { x.TenantId, x.ActorUserId });

            entity.HasOne(x => x.ActorUser)
                .WithMany()
                .HasForeignKey(x => x.ActorUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<EhcAgentReplyProfile>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<EhcKnowledgeBaseCategory>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.IsActive });
        });

        builder.Entity<EhcKnowledgeBaseArticle>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.CategoryId });
            entity.HasIndex(x => new { x.TenantId, x.IsPublished });
            entity.HasIndex(x => new { x.TenantId, x.IsInternalOnly });

            entity.HasOne(x => x.Category)
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<EhcFaqCategory>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.IsActive });
        });

        builder.Entity<EhcFaqItem>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.CategoryId });
            entity.HasIndex(x => new { x.TenantId, x.IsPublished });
            entity.HasIndex(x => new { x.TenantId, x.IsInternalOnly });
            entity.HasIndex(x => new { x.TenantId, x.SortOrder });

            entity.HasOne(x => x.Category)
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<EhcTicketPriorityLevel>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.Priority }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.IsActive });
            entity.HasIndex(x => new { x.TenantId, x.SortOrder });
        });

        builder.Entity<EhcEscalationPolicy>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.IsActive });
            entity.HasIndex(x => new { x.TenantId, x.Priority });
            entity.HasIndex(x => new { x.TenantId, x.Trigger });

            entity.HasOne(x => x.Category)
                .WithMany()
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Subcategory)
                .WithMany()
                .HasForeignKey(x => x.SubcategoryId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Department)
                .WithMany()
                .HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(x => x.Levels)
                .WithOne(l => l.Policy)
                .HasForeignKey(l => l.PolicyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EhcEscalationPolicyLevel>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.PolicyId, x.Level }).IsUnique();
        });

        builder.Entity<EhcEscalationExecution>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.TicketId, x.PolicyId, x.Level }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.ExecutedAtUtc });
        });

        builder.Entity<EhcLegalHold>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.TicketId, x.IsActive });
            entity.HasIndex(x => new { x.TenantId, x.CreatedAt });
        });

        builder.Entity<EhcRetentionCategoryException>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.CategoryId }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.IsActive });
        });

        builder.Entity<EhcComplianceAuditExport>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.CreatedAt });
            entity.HasIndex(x => new { x.TenantId, x.FromUtc, x.ToUtc });
        });
    }

    private static void ConfigureComplianceAndSettingsEntities(ModelBuilder builder)
    {
        builder.Entity<SmsSettings>(entity =>
        {
            entity.HasIndex(x => x.TenantId).IsUnique();
        });

        builder.Entity<DataRetentionPolicy>(entity =>
        {
            entity.HasIndex(x => x.TenantId).IsUnique();
        });

        builder.Entity<DataRetentionJobRun>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.JobName, x.StartedAtUtc });
        });
    }

    private static void SetSoftDeleteFilter<TEntity>(ModelBuilder builder, Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType)
        where TEntity : BaseEntity
    {
        builder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }

    private static void SetTenantSoftDeleteFilter<TEntity>(ModelBuilder builder, Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType, Guid tenantId)
        where TEntity : TenantEntity
    {
        builder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted && e.TenantId == tenantId);
    }

    private void SeedData(ModelBuilder builder)
    {
        // IMPORTANT: EF Core captures HasData values into migrations. Avoid DateTime.UtcNow here to prevent constant
        // migration churn across environments/branches.
        var seedDateUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

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
                CreatedAt = seedDateUtc,
                CreatedBy = "System"
            }
        );

        // Seed default roles
        var superAdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var tenantAdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var managerRoleId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var employeeRoleId = Guid.Parse("00000000-0000-0000-0000-000000000004");

        builder.Entity<ApplicationRole>().HasData(
            new ApplicationRole { Id = superAdminRoleId, Name = Shared.Constants.Roles.SuperAdmin, NormalizedName = Shared.Constants.Roles.SuperAdmin.ToUpper(), ConcurrencyStamp = null, IsSystemRole = true, CreatedAt = seedDateUtc, CreatedBy = "System" },
            new ApplicationRole { Id = tenantAdminRoleId, Name = Shared.Constants.Roles.TenantAdmin, NormalizedName = Shared.Constants.Roles.TenantAdmin.ToUpper(), ConcurrencyStamp = null, IsSystemRole = true, CreatedAt = seedDateUtc, CreatedBy = "System" },
            new ApplicationRole { Id = managerRoleId, Name = Shared.Constants.Roles.Manager, NormalizedName = Shared.Constants.Roles.Manager.ToUpper(), ConcurrencyStamp = null, IsSystemRole = true, CreatedAt = seedDateUtc, CreatedBy = "System" },
            new ApplicationRole { Id = employeeRoleId, Name = Shared.Constants.Roles.Employee, NormalizedName = Shared.Constants.Roles.Employee.ToUpper(), ConcurrencyStamp = null, IsSystemRole = true, CreatedAt = seedDateUtc, CreatedBy = "System" }
        );

        // Seed default modules for default tenant (use stable IDs to avoid migration churn)
        var moduleIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
        {
            { Shared.Constants.Modules.Finance, Guid.Parse("00000000-0000-0000-0000-000000010001") },
            { Shared.Constants.Modules.HR, Guid.Parse("00000000-0000-0000-0000-000000010002") },
            { Shared.Constants.Modules.Sales, Guid.Parse("00000000-0000-0000-0000-000000010003") },
            { Shared.Constants.Modules.Procurement, Guid.Parse("00000000-0000-0000-0000-000000010004") },
            { Shared.Constants.Modules.Inventory, Guid.Parse("00000000-0000-0000-0000-000000010005") },
            { Shared.Constants.Modules.Marketing, Guid.Parse("00000000-0000-0000-0000-000000010006") },
            { Shared.Constants.Modules.WorkflowEngine, Guid.Parse("00000000-0000-0000-0000-000000010007") }
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
                    Id = moduleIds[modules[i]],
                    TenantId = defaultTenantId,
                    ModuleName = modules[i],
                    Status = Shared.ModuleStatus.Enabled,
                    EnabledDate = seedDateUtc,
                    CreatedAt = seedDateUtc
                }
            );
        }

        // Seed permissions
        SeedPermissions(builder, seedDateUtc);
    }

    private static void SeedPermissions(ModelBuilder builder, DateTime seedDateUtc)
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
                CreatedAt = seedDateUtc,
                CreatedBy = "System"
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
                CreatedAt = seedDateUtc,
                CreatedBy = "System"
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
                CreatedAt = seedDateUtc,
                CreatedBy = "System"
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
                CreatedAt = seedDateUtc,
                CreatedBy = "System"
            });
            permissionId++;
        }

        var moduleAccessPermissions = new[]
        {
            ("project.access", "Access Project Management", "Access the project management module"),
            ("maintenance.access", "Access Maintenance Management", "Access the maintenance management module"),
            ("fleet.access", "Access Fleet Management", "Access the fleet management module")
        };

        foreach (var (name, displayName, description) in moduleAccessPermissions)
        {
            permissions.Add(new Permission
            {
                Id = Guid.Parse($"00000000-0000-0000-0000-{permissionId:000000000000}"),
                Name = name,
                DisplayName = displayName,
                Description = description,
                Category = "Module Access",
                IsSystemPermission = true,
                CreatedAt = seedDateUtc,
                CreatedBy = "System"
            });
            permissionId++;
        }

        var adminModulePermissions = new[]
        {
            ("admin.project-management", "Admin Project Management", "Manage project administration settings"),
            ("admin.maintenance", "Admin Maintenance Management", "Manage maintenance administration settings"),
            ("admin.fleet-management", "Admin Fleet Management", "Manage fleet administration settings")
        };

        foreach (var (name, displayName, description) in adminModulePermissions)
        {
            permissions.Add(new Permission
            {
                Id = Guid.Parse($"00000000-0000-0000-0000-{permissionId:000000000000}"),
                Name = name,
                DisplayName = displayName,
                Description = description,
                Category = "Administration Modules",
                IsSystemPermission = true,
                CreatedAt = seedDateUtc,
                CreatedBy = "System"
            });
            permissionId++;
        }

        builder.Entity<Permission>().HasData(permissions.ToArray());

        // Seed role-permission relationships
        SeedRolePermissions(builder, permissions, seedDateUtc);
    }

    private static void SeedRolePermissions(ModelBuilder builder, List<Permission> permissions, DateTime seedDateUtc)
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
                GrantedAt = seedDateUtc,
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
                GrantedAt = seedDateUtc,
                GrantedBy = "System"
            });
        }

        // Manager gets read permissions and basic management
        var managerRoleId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var managerPermissions = permissions.Where(p =>
            p.Name.EndsWith(".read") ||
            p.Name == "users.update" ||
            p.Name == "reports.create" ||
            p.Name == "project.access" ||
            p.Name == "maintenance.access" ||
            p.Name == "fleet.access").ToList();

        foreach (var permission in managerPermissions)
        {
            rolePermissions.Add(new RolePermission
            {
                RoleId = managerRoleId,
                PermissionId = permission.Id,
                GrantedAt = seedDateUtc,
                GrantedBy = "System"
            });
        }

        // Employee gets basic read permissions
        var employeeRoleId = Guid.Parse("00000000-0000-0000-0000-000000000004");
        var employeePermissions = permissions.Where(p =>
            p.Name == "dashboard.read" ||
            p.Name == "reports.read" ||
            p.Name == "users.read" ||
            p.Name == "project.access" ||
            p.Name == "maintenance.access" ||
            p.Name == "fleet.access").ToList();

        foreach (var permission in employeePermissions)
        {
            rolePermissions.Add(new RolePermission
            {
                RoleId = employeeRoleId,
                PermissionId = permission.Id,
                GrantedAt = seedDateUtc,
                GrantedBy = "System"
            });
        }

        builder.Entity<RolePermission>().HasData(rolePermissions.ToArray());
    }

    private static void ConfigureCentralDocumentManagementEntities(ModelBuilder builder)
    {
        builder.Entity<CentralDocumentRecord>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.DocumentReference }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.SourceModule, item.SourceRecordReference });
            entity.HasMany(item => item.Versions)
                .WithOne(item => item.DocumentRecord)
                .HasForeignKey(item => item.DocumentRecordId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(item => item.AnnotationReviews)
                .WithOne(item => item.DocumentRecord)
                .HasForeignKey(item => item.DocumentRecordId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(item => item.MetadataValues)
                .WithOne(item => item.DocumentRecord)
                .HasForeignKey(item => item.DocumentRecordId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CentralDocumentVersion>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.DocumentRecordId, item.VersionNumber }).IsUnique();
        });

        builder.Entity<CentralDocumentMetadataTemplate>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.TemplateCode }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.Module, item.DocumentType });
        });

        builder.Entity<CentralDocumentMetadataValue>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.DocumentRecordId, item.FieldKey }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.TemplateCode, item.FieldKey });
        });

        builder.Entity<CentralDocumentGenerationTemplate>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.TemplateCode }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.Module, item.DocumentType });
        });

        builder.Entity<CentralDocumentAnnotationReview>(entity =>
        {
            entity.HasOne(item => item.DocumentVersion)
                .WithMany()
                .HasForeignKey(item => item.DocumentVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => new { item.TenantId, item.DocumentRecordId, item.Status });
        });

        builder.Entity<CentralDocumentAccessRule>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.AccessProfile, item.Module, item.RoleName });
        });

        builder.Entity<CentralDocumentRetentionPolicy>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.PolicyCode }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.Module, item.DocumentType });
        });
    }

    private static void ConfigureLandAcquisitionEntities(ModelBuilder builder)
    {
        builder.Entity<LandAcquisition>(entity =>
        {
            entity.ToTable("LandAcquisitions");
            entity.HasIndex(item => new { item.TenantId, item.ProjectReference }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.StageOrder, item.Status });
            entity.Property(item => item.EstimatedSize).HasPrecision(18, 4);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(60);
            entity.Property(item => item.CurrentStage).HasConversion<string>().HasMaxLength(80);
            entity.Property(item => item.OwnershipType).HasConversion<string>().HasMaxLength(80);

            entity.HasMany(item => item.Documents)
                .WithOne(item => item.LandAcquisition)
                .HasForeignKey(item => item.LandAcquisitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(item => item.Notes)
                .WithOne(item => item.LandAcquisition)
                .HasForeignKey(item => item.LandAcquisitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(item => item.ChecklistResponses)
                .WithOne(item => item.LandAcquisition)
                .HasForeignKey(item => item.LandAcquisitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(item => item.CadastralSurveys)
                .WithOne(item => item.LandAcquisition)
                .HasForeignKey(item => item.LandAcquisitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(item => item.OwnershipHistories)
                .WithOne(item => item.LandAcquisition)
                .HasForeignKey(item => item.LandAcquisitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(item => item.NegotiationOffers)
                .WithOne(item => item.LandAcquisition)
                .HasForeignKey(item => item.LandAcquisitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(item => item.Registrations)
                .WithOne(item => item.LandAcquisition)
                .HasForeignKey(item => item.LandAcquisitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(item => item.LandAssets)
                .WithOne(item => item.LandAcquisition)
                .HasForeignKey(item => item.LandAcquisitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(item => item.PhysicalAssessment)
                .WithOne(item => item.LandAcquisition)
                .HasForeignKey<LandPhysicalAssessment>(item => item.LandAcquisitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(item => item.Agreement)
                .WithOne(item => item.LandAcquisition)
                .HasForeignKey<LandAgreement>(item => item.LandAcquisitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(item => item.LandInstrument)
                .WithOne(item => item.LandAcquisition)
                .HasForeignKey<LandInstrument>(item => item.LandAcquisitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(item => item.StatutoryConsent)
                .WithOne(item => item.LandAcquisition)
                .HasForeignKey<StatutoryConsent>(item => item.LandAcquisitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(item => item.StampDutyAssessment)
                .WithOne(item => item.LandAcquisition)
                .HasForeignKey<StampDutyAssessment>(item => item.LandAcquisitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(item => item.StampDutyPayment)
                .WithOne(item => item.LandAcquisition)
                .HasForeignKey<StampDutyPayment>(item => item.LandAcquisitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<LandAcquisitionDocument>(entity =>
        {
            entity.ToTable("LandAcquisitionDocuments");
            entity.Property(item => item.Procedure).HasConversion<string>().HasMaxLength(80);
        });

        builder.Entity<LandPhysicalAssessment>().ToTable("LandPhysicalAssessments");
        builder.Entity<CadastralSurvey>().ToTable("CadastralSurveys");

        builder.Entity<OwnershipHistory>(entity =>
        {
            entity.ToTable("OwnershipHistories");
            entity.Property(item => item.OwnershipType).HasConversion<string>().HasMaxLength(80);
            entity.Property(item => item.AcquisitionMethod).HasConversion<string>().HasMaxLength(80);
        });

        builder.Entity<NegotiationOffer>(entity =>
        {
            entity.ToTable("NegotiationOffers");
            entity.Property(item => item.OpeningOffer).HasPrecision(18, 2);
            entity.Property(item => item.CounterOffer).HasPrecision(18, 2);
            entity.Property(item => item.NegotiatedValue).HasPrecision(18, 2);
        });

        builder.Entity<LandAgreement>().ToTable("LandAgreements");
        builder.Entity<LandInstrument>().ToTable("LandInstruments");
        builder.Entity<StatutoryConsent>().ToTable("StatutoryConsents");

        builder.Entity<StampDutyAssessment>(entity =>
        {
            entity.ToTable("StampDutyAssessments");
            entity.Property(item => item.AssessedValue).HasPrecision(18, 2);
            entity.Property(item => item.DutyAmount).HasPrecision(18, 2);
        });

        builder.Entity<StampDutyPayment>(entity =>
        {
            entity.ToTable("StampDutyPayments");
            entity.Property(item => item.AmountPaid).HasPrecision(18, 2);
            entity.HasIndex(item => new { item.TenantId, item.AccountsPayableInvoiceId });
            entity.HasIndex(item => new { item.TenantId, item.AccountsPayablePaymentId });
        });

        builder.Entity<LandRegistration>().ToTable("LandRegistrations");

        builder.Entity<LandAsset>(entity =>
        {
            entity.ToTable("LandAssets");
            entity.Property(item => item.CapitalizationValue).HasPrecision(18, 2);
        });

        builder.Entity<EstateManagedAsset>(entity =>
        {
            entity.ToTable("EstateManagedAssets");
            entity.HasIndex(item => new { item.TenantId, item.AssetCode }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.AssetType, item.Status });
            entity.HasIndex(item => new { item.TenantId, item.ProjectUnitId });
            entity.HasIndex(item => new { item.TenantId, item.LandAcquisitionId });
            entity.HasIndex(item => new { item.TenantId, item.IsPublishedToExternalPortal, item.ExternalListingStatus });
            entity.Property(item => item.AssetType).HasConversion<string>().HasMaxLength(40);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(item => item.SourceType).HasConversion<string>().HasMaxLength(40);
            entity.Property(item => item.AreaSquareMeters).HasPrecision(18, 4);
            entity.Property(item => item.AreaValue).HasPrecision(18, 4);
            entity.Property(item => item.ValuationAmount).HasPrecision(18, 2);
            entity.Property(item => item.ExternalListingPrice).HasPrecision(18, 2);
            entity.HasMany(item => item.Documents)
                .WithOne(item => item.EstateManagedAsset)
                .HasForeignKey(item => item.EstateManagedAssetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EstateManagedAssetDocument>(entity =>
        {
            entity.ToTable("EstateManagedAssetDocuments");
            entity.HasIndex(item => new { item.TenantId, item.EstateManagedAssetId });
            entity.HasIndex(item => new { item.TenantId, item.CentralDocumentRecordId });
            entity.HasIndex(item => new { item.TenantId, item.EstateManagedAssetId, item.IsListingImage });
        });

        builder.Entity<LandAcquisitionNote>(entity =>
        {
            entity.ToTable("LandAcquisitionNotes");
            entity.Property(item => item.Stage).HasConversion<string>().HasMaxLength(80);
        });

        builder.Entity<LandAcquisitionChecklistResponse>(entity =>
        {
            entity.ToTable("LandAcquisitionChecklistResponses");
            entity.Property(item => item.Procedure).HasConversion<string>().HasMaxLength(80);
        });
    }

    private static void ConfigureProcedureCaseEntities(ModelBuilder builder)
    {
        builder.Entity<ProcedureCase>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.Module, item.EntityType, item.Status });
            entity.HasIndex(item => new { item.TenantId, item.ReferenceNumber });
            entity.HasIndex(item => new { item.TenantId, item.CurrentAssignedRole });
            entity.HasIndex(item => item.WorkflowDefinitionId);
            entity.HasIndex(item => item.WorkflowInstanceId);
            entity.HasIndex(item => item.WorkflowStepId);

            entity.HasMany(item => item.Fields)
                .WithOne(item => item.ProcedureCase)
                .HasForeignKey(item => item.ProcedureCaseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(item => item.ChecklistItems)
                .WithOne(item => item.ProcedureCase)
                .HasForeignKey(item => item.ProcedureCaseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(item => item.Documents)
                .WithOne(item => item.ProcedureCase)
                .HasForeignKey(item => item.ProcedureCaseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(item => item.Activities)
                .WithOne(item => item.ProcedureCase)
                .HasForeignKey(item => item.ProcedureCaseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProcedureCaseField>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.ProcedureCaseId, item.Key }).IsUnique();
        });

        builder.Entity<ProcedureCaseChecklistItem>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.ProcedureCaseId, item.StageIndex });
        });

        builder.Entity<ProcedureCaseDocument>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.ProcedureCaseId, item.IsMandatory });
        });

        builder.Entity<ProcedureCaseActivity>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.ProcedureCaseId, item.PerformedAt });
        });
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
            if (entry.Entity is WorkflowActivityLog && entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Workflow audit events are immutable and cannot be changed or deleted.");
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    
                    // Set TenantId for TenantEntity objects if not already set
                    if (entry.Entity is TenantEntity tenantEntity && tenantEntity.TenantId == Guid.Empty)
                    {
                        if (_tenantId.HasValue && _tenantId.Value != Guid.Empty)
                        {
                            tenantEntity.TenantId = _tenantId.Value;
                        }
                    }
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

    private static void ConfigureDecimalPrecision(ModelBuilder builder)
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

    private static void ConfigureSalesAllocationPrecision(ModelBuilder builder)
    {
        builder.Entity<SalesAllocation>(entity =>
        {
            entity.Property(e => e.EstimatedValue).HasColumnType("decimal(18,2)");
            entity.Property(e => e.AgreedValue).HasColumnType("decimal(18,2)");
        });
    }

    private static void ConfigureGlobalTenantRelationships(ModelBuilder builder)
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

    private static void ConfigureProcurementPlanningEntities(ModelBuilder builder)
    {
        // ProcurementPlan entity
        builder.Entity<ProcurementPlan>(entity =>
        {
            entity.HasIndex(p => p.PlanNumber).IsUnique();
            entity.HasIndex(p => p.DepartmentId);
            entity.HasIndex(p => p.FiscalYear);
            entity.HasIndex(p => p.PlanningCycle);
            entity.HasIndex(p => p.PlanningQuarter);
            entity.HasIndex(p => p.Status);
            entity.HasIndex(p => p.PublishedDate);

            entity.HasOne(p => p.Department)
                .WithMany()
                .HasForeignKey(p => p.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(p => p.Items)
                .WithOne(i => i.ProcurementPlan)
                .HasForeignKey(i => i.ProcurementPlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ProcurementPlanItem entity
        builder.Entity<ProcurementPlanItem>(entity =>
        {
            entity.HasIndex(i => i.ProcurementPlanId);
            entity.HasIndex(i => i.ProcurementBudgetId);
            entity.HasIndex(i => i.ProcurementBudgetAllocationId);
            entity.HasIndex(i => i.MarketAnalysisId);
            entity.HasIndex(i => i.BudgetLineCode);
            entity.HasIndex(i => i.ItemCategory);
            entity.HasIndex(i => i.IsCritical);

            entity.HasMany(i => i.ItemSuppliers)
                .WithOne(s => s.ProcurementPlanItem)
                .HasForeignKey(s => s.ProcurementPlanItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(i => i.ProcurementBudget)
                .WithMany()
                .HasForeignKey(i => i.ProcurementBudgetId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(i => i.ProcurementBudgetAllocation)
                .WithMany()
                .HasForeignKey(i => i.ProcurementBudgetAllocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(i => i.MarketAnalysis)
                .WithMany()
                .HasForeignKey(i => i.MarketAnalysisId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ProcurementPlanItemSupplier entity
        builder.Entity<ProcurementPlanItemSupplier>(entity =>
        {
            entity.HasIndex(s => s.ProcurementPlanItemId);
            entity.HasIndex(s => s.SupplierId);
            entity.HasIndex(s => new { s.ProcurementPlanItemId, s.SupplierId }).IsUnique();

            entity.HasOne(s => s.BusinessPartner)
                .WithMany()
                .HasForeignKey(s => s.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ProcurementBudget entity
        builder.Entity<ProcurementBudget>(entity =>
        {
            entity.HasIndex(b => new { b.TenantId, b.BudgetCode }).IsUnique();
            entity.HasIndex(b => b.DepartmentId);
            entity.HasIndex(b => b.FiscalYear);
            entity.HasIndex(b => b.Status);

            entity.HasOne(b => b.Department)
                .WithMany()
                .HasForeignKey(b => b.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(b => b.Allocations)
                .WithOne(a => a.ProcurementBudget)
                .HasForeignKey(a => a.ProcurementBudgetId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(b => b.Revisions)
                .WithOne(r => r.ProcurementBudget)
                .HasForeignKey(r => r.ProcurementBudgetId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(b => b.Commitments)
                .WithOne(c => c.ProcurementBudget)
                .HasForeignKey(c => c.ProcurementBudgetId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ProcurementBudgetAllocation entity
        builder.Entity<ProcurementBudgetAllocation>(entity =>
        {
            entity.HasIndex(a => a.ProcurementBudgetId);
            entity.HasIndex(a => a.CategoryName);
        });

        // ProcurementBudgetRevision entity
        builder.Entity<ProcurementBudgetRevision>(entity =>
        {
            entity.HasIndex(r => r.ProcurementBudgetId);
            entity.HasIndex(r => r.RevisionNumber);
            entity.HasIndex(r => r.Status);
        });

        builder.Entity<ProcurementBudgetCommitment>(entity =>
        {
            entity.HasIndex(c => new { c.TenantId, c.PurchaseRequisitionId }).IsUnique();
            entity.HasIndex(c => new { c.TenantId, c.ReservationReference }).IsUnique();
            entity.HasIndex(c => new { c.TenantId, c.ProcurementBudgetId, c.Status });
            entity.HasIndex(c => new { c.TenantId, c.Status, c.ReservedAtUtc });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProcurementBudgetCommitments_NoDelete");
                table.HasTrigger("TR_ProcurementBudgetCommitments_TenantAndEvidenceGuard");
                table.HasTrigger("TR_ProcurementBudgetCommitments_LifecycleGuard");
                table.HasCheckConstraint("CK_ProcurementBudgetCommitments_Amount", "[ReservedAmount] > 0");
                table.HasCheckConstraint("CK_ProcurementBudgetCommitments_Sequence", "[ReservationSequence] > 0");
                table.HasCheckConstraint("CK_ProcurementBudgetCommitments_Status", "[Status] IN (1, 2, 3)");
            });

            entity.HasOne(c => c.PurchaseRequisition)
                .WithOne(r => r.BudgetCommitment)
                .HasForeignKey<ProcurementBudgetCommitment>(c => c.PurchaseRequisitionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(c => c.OverrideRule)
                .WithMany()
                .HasForeignKey(c => c.OverrideRuleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(c => c.OverrideWorkflowInstance)
                .WithMany()
                .HasForeignKey(c => c.OverrideWorkflowInstanceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ProcurementSchedule entity
        builder.Entity<ProcurementSchedule>(entity =>
        {
            entity.HasIndex(s => s.ProcurementPlanId);
            entity.HasIndex(s => s.DepartmentId);
            entity.HasIndex(s => s.PlannedStartDate);
            entity.HasIndex(s => s.Status);

            entity.HasOne(s => s.ProcurementPlan)
                .WithMany(p => p.Schedules)
                .HasForeignKey(s => s.ProcurementPlanId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Department)
                .WithMany()
                .HasForeignKey(s => s.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // MarketAnalysis entity
        builder.Entity<MarketAnalysis>(entity =>
        {
            entity.HasIndex(m => m.ItemCategory);
            entity.HasIndex(m => m.ItemDescription);
            entity.HasIndex(m => m.AnalysisPeriodStart);
            entity.Property(m => m.HistoricalAveragePrice).HasColumnType("decimal(18,4)");
            entity.Property(m => m.PreviousPrice).HasColumnType("decimal(18,4)");
            entity.Property(m => m.CurrentMarketPrice).HasColumnType("decimal(18,4)");
            entity.Property(m => m.ForecastedPrice).HasColumnType("decimal(18,4)");
            entity.Property(m => m.PriceChangePercent).HasColumnType("decimal(8,2)");
            entity.Property(m => m.PriceVariancePercent).HasColumnType("decimal(8,2)");
            entity.Property(m => m.InflationImpactPercent).HasColumnType("decimal(8,2)");
            entity.Property(m => m.RecommendedBudgetAdjustmentPercent).HasColumnType("decimal(8,2)");

            entity.HasMany(m => m.PriceHistories)
                .WithOne(p => p.MarketAnalysis)
                .HasForeignKey(p => p.MarketAnalysisId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PriceHistory entity
        builder.Entity<PriceHistory>(entity =>
        {
            entity.HasIndex(p => p.MarketAnalysisId);
            entity.HasIndex(p => p.PriceDate);
        });

        // SupplierConsolidation entity
        builder.Entity<SupplierConsolidation>(entity =>
        {
            entity.HasIndex(s => s.ItemCategory);
            entity.HasIndex(s => s.Status);
        });

        // EmergencyProcurementPlan entity
        builder.Entity<EmergencyProcurementPlan>(entity =>
        {
            entity.HasIndex(e => e.PlanCode).IsUnique();
            entity.HasIndex(e => e.EmergencyType);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CriticalityLevel);

            entity.HasMany(e => e.CriticalItems)
                .WithOne(i => i.EmergencyProcurementPlan)
                .HasForeignKey(i => i.EmergencyProcurementPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.EmergencySuppliers)
                .WithOne(s => s.EmergencyProcurementPlan)
                .HasForeignKey(s => s.EmergencyProcurementPlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // EmergencyProcurementItem entity
        builder.Entity<EmergencyProcurementItem>(entity =>
        {
            entity.HasIndex(i => i.EmergencyProcurementPlanId);
            entity.HasIndex(i => i.ItemCategory);
            entity.HasIndex(i => i.CriticalityLevel);
        });

        // EmergencySupplier entity
        builder.Entity<EmergencySupplier>(entity =>
        {
            entity.HasIndex(s => s.EmergencyProcurementPlanId);
            entity.HasIndex(s => s.SupplierId);
            entity.HasIndex(s => s.IsActive);
        });
    }

    private static void ConfigureProcurementAppSubmissions(ModelBuilder builder)
    {
        builder.Entity<ProcurementAppSubmission>(entity =>
        {
            entity.Property(item => item.SubmissionNumber).IsUnicode(false);
            entity.Property(item => item.TimelineCorrelationId).IsUnicode(false);
            entity.Property(item => item.ExportFormat).IsUnicode(false);
            entity.Property(item => item.ExportTemplateVersion).IsUnicode(false);
            entity.Property(item => item.ExportChecksumSha256).IsUnicode(false).IsFixedLength();
            entity.Property(item => item.ExternalSubmissionReference).IsUnicode(false);
            entity.Property(item => item.AcknowledgementReference).IsUnicode(false);
            entity.Property(item => item.RejectionReference).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.SubmissionNumber, item.AttemptNumber }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ProcurementPlanId, item.AttemptNumber }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ProcurementPlanId })
                .IsUnique().HasFilter("[AttemptNumber] = 1 AND [IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.Status, item.UpdatedAt });
            entity.HasIndex(item => new { item.TenantId, item.TimelineCorrelationId, item.AttemptNumber });
            entity.ToTable("ProcurementAppSubmissions", table =>
            {
                table.HasTrigger("TR_ProcurementAppSubmissions_NoDelete");
                table.HasCheckConstraint("CK_ProcurementAppSubmissions_Status", "[Status] BETWEEN 0 AND 3");
                table.HasCheckConstraint("CK_ProcurementAppSubmissions_Attempt", "[AttemptNumber] >= 1");
                table.HasCheckConstraint("CK_ProcurementAppSubmissions_Checksum", "LEN([ExportChecksumSha256]) = 64");
                table.HasCheckConstraint("CK_ProcurementAppSubmissions_Submitted",
                    "([Status] = 0 AND [ExternalSubmissionReference] IS NULL AND [SubmittedAtUtc] IS NULL) OR " +
                    "([Status] IN (1, 2, 3) AND [ExternalSubmissionReference] IS NOT NULL AND [SubmittedAtUtc] IS NOT NULL)");
                table.HasCheckConstraint("CK_ProcurementAppSubmissions_Outcome",
                    "([Status] = 2 AND [AcknowledgementReference] IS NOT NULL AND [AcknowledgedAtUtc] IS NOT NULL AND [RejectionReference] IS NULL AND [RejectedAtUtc] IS NULL) OR " +
                    "([Status] = 3 AND [RejectionReference] IS NOT NULL AND [RejectionReason] IS NOT NULL AND [RejectedAtUtc] IS NOT NULL AND [AcknowledgementReference] IS NULL AND [AcknowledgedAtUtc] IS NULL) OR " +
                    "([Status] IN (0, 1) AND [AcknowledgementReference] IS NULL AND [AcknowledgedAtUtc] IS NULL AND [RejectionReference] IS NULL AND [RejectedAtUtc] IS NULL)");
            });
            entity.HasOne(item => item.ProcurementPlan).WithMany()
                .HasForeignKey(item => item.ProcurementPlanId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SupersedesSubmission).WithMany()
                .HasForeignKey(item => item.SupersedesSubmissionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany()
                .HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProcurementSpecificationTemplates(ModelBuilder builder)
    {
        builder.Entity<ProcurementSpecificationTemplate>(entity =>
        {
            entity.Property(item => item.TemplateCode).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.TemplateKey, item.Version }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.TemplateCode, item.Version }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.TemplateKey, item.Status });
            entity.HasIndex(item => new { item.TenantId, item.Status, item.EffectiveFromUtc });
            entity.HasIndex(item => new { item.TenantId, item.Kind, item.Status });
            entity.ToTable("ProcurementSpecificationTemplates", table =>
            {
                table.HasTrigger("TR_ProcurementSpecificationTemplates_NoDelete");
                table.HasTrigger("TR_ProcurementSpecificationTemplates_LifecycleGuard");
                table.HasCheckConstraint("CK_ProcurementSpecificationTemplates_Kind", "[Kind] BETWEEN 0 AND 2");
                table.HasCheckConstraint("CK_ProcurementSpecificationTemplates_Status", "[Status] BETWEEN 0 AND 3");
                table.HasCheckConstraint("CK_ProcurementSpecificationTemplates_Version", "[Version] >= 1");
                table.HasCheckConstraint("CK_ProcurementSpecificationTemplates_Revision", "[RevisionNumber] >= 1");
                table.HasCheckConstraint("CK_ProcurementSpecificationTemplates_EffectivePeriod",
                    "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] >= [EffectiveFromUtc]");
            });
            entity.HasOne(item => item.WorkflowDefinition).WithMany()
                .HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.WorkflowInstance).WithMany()
                .HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SupersedesTemplate).WithMany()
                .HasForeignKey(item => item.SupersedesTemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany()
                .HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProcurementCalendar(ModelBuilder builder)
    {
        builder.Entity<ProcurementCalendarProfile>(entity =>
        {
            entity.Property(item => item.ProfileCode).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ProfileKey, item.Version }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ProfileCode, item.Version }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ProfileKey, item.Status })
                .IsUnique().HasFilter("[Status] = 0 AND [IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.Status, item.EffectiveFromUtc });
            entity.ToTable("ProcurementCalendarProfiles", table =>
            {
                table.HasTrigger("TR_ProcurementCalendarProfiles_LifecycleGuard");
                table.HasCheckConstraint("CK_ProcurementCalendarProfiles_Status", "[Status] BETWEEN 0 AND 2");
                table.HasCheckConstraint("CK_ProcurementCalendarProfiles_Version", "[Version] >= 1");
                table.HasCheckConstraint("CK_ProcurementCalendarProfiles_Horizon", "[GenerationHorizonDays] BETWEEN 1 AND 730");
                table.HasCheckConstraint("CK_ProcurementCalendarProfiles_CatchUp", "[CatchUpDays] BETWEEN 0 AND 365");
                table.HasCheckConstraint("CK_ProcurementCalendarProfiles_EffectivePeriod",
                    "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] >= [EffectiveFromUtc]");
            });
            entity.HasOne(item => item.SupersedesProfile).WithMany()
                .HasForeignKey(item => item.SupersedesProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany()
                .HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProcurementCalendarRule>(entity =>
        {
            entity.Property(item => item.RuleCode).IsUnicode(false);
            entity.Property(item => item.DueLocalTime).HasColumnType("time");
            entity.HasIndex(item => new { item.TenantId, item.ProfileId, item.EventType }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ProfileId, item.RuleKey }).IsUnique();
            entity.ToTable("ProcurementCalendarRules", table =>
            {
                table.HasTrigger("TR_ProcurementCalendarRules_LifecycleGuard");
                table.HasCheckConstraint("CK_ProcurementCalendarRules_EventType", "[EventType] BETWEEN 0 AND 6");
                table.HasCheckConstraint("CK_ProcurementCalendarRules_DueMonth", "[DueMonth] BETWEEN 1 AND 12");
                table.HasCheckConstraint("CK_ProcurementCalendarRules_DueDay", "[DueDay] BETWEEN 1 AND 31");
                table.HasCheckConstraint("CK_ProcurementCalendarRules_Reminder", "[ReminderLeadDays] BETWEEN 0 AND 365");
                table.HasCheckConstraint("CK_ProcurementCalendarRules_Escalation", "[EscalationAfterDays] BETWEEN 0 AND 365");
                table.HasCheckConstraint("CK_ProcurementCalendarRules_Owner",
                    "([OwnerUserId] IS NOT NULL AND [OwnerRoleName] IS NULL) OR ([OwnerUserId] IS NULL AND [OwnerRoleName] IS NOT NULL)");
                table.HasCheckConstraint("CK_ProcurementCalendarRules_EscalationOwner",
                    "([EscalationUserId] IS NOT NULL AND [EscalationRoleName] IS NULL) OR ([EscalationUserId] IS NULL AND [EscalationRoleName] IS NOT NULL)");
            });
            entity.HasOne(item => item.Profile).WithMany(item => item.Rules)
                .HasForeignKey(item => item.ProfileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Tenant).WithMany()
                .HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProcurementCalendarOccurrence>(entity =>
        {
            entity.Property(item => item.OccurrenceKey).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.OccurrenceKey }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ProfileKey, item.RuleKey, item.CalendarYear }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.Status, item.DueAtUtc });
            entity.HasIndex(item => new { item.TenantId, item.OwnerUserId, item.Status });
            entity.ToTable("ProcurementCalendarOccurrences", table =>
            {
                table.HasTrigger("TR_ProcurementCalendarOccurrences_TenantGuard");
                table.HasCheckConstraint("CK_ProcurementCalendarOccurrences_EventType", "[EventType] BETWEEN 0 AND 6");
                table.HasCheckConstraint("CK_ProcurementCalendarOccurrences_Status", "[Status] BETWEEN 0 AND 6");
                table.HasCheckConstraint("CK_ProcurementCalendarOccurrences_Year", "[CalendarYear] BETWEEN 2000 AND 9999");
                table.HasCheckConstraint("CK_ProcurementCalendarOccurrences_ProfileVersion", "[ProfileVersion] >= 1");
            });
            entity.HasOne(item => item.Profile).WithMany()
                .HasForeignKey(item => item.ProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Rule).WithMany()
                .HasForeignKey(item => item.RuleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany()
                .HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProcurementCalendarRun>(entity =>
        {
            entity.Property(item => item.RunKey).IsUnicode(false);
            entity.HasIndex(item => new { item.TenantId, item.RunKey }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.StartedAtUtc });
            entity.ToTable("ProcurementCalendarRuns", table =>
            {
                table.HasCheckConstraint("CK_ProcurementCalendarRuns_Trigger", "[Trigger] BETWEEN 0 AND 2");
                table.HasCheckConstraint("CK_ProcurementCalendarRuns_Status", "[Status] BETWEEN 0 AND 3");
                table.HasCheckConstraint("CK_ProcurementCalendarRuns_Attempt", "[AttemptCount] >= 1");
            });
            entity.HasOne(item => item.Tenant).WithMany()
                .HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePurchaseRequisitionLinkage(ModelBuilder builder)
    {
        builder.Entity<PurchaseRequisition>(entity =>
        {
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.SourcePlanItemId });
            entity.HasIndex(item => new { item.TenantId, item.BudgetId });
            entity.HasIndex(item => new { item.TenantId, item.ProjectId });
            entity.HasIndex(item => new { item.TenantId, item.ProcurementCategory });
            entity.HasIndex(item => new { item.TenantId, item.SpecificationTemplateId });
            entity.HasIndex(item => new { item.TenantId, item.ApprovedExceptionRuleId });
            entity.ToTable("PurchaseRequisitions", table =>
            {
                table.HasTrigger("TR_PurchaseRequisitions_LinkageGuard");
                table.HasCheckConstraint("CK_PurchaseRequisitions_LinkageRevision", "[LinkageRevision] >= 0");
                table.HasCheckConstraint("CK_PurchaseRequisitions_RequisitionType", "[RequisitionType] BETWEEN 1 AND 5");
                table.HasCheckConstraint("CK_PurchaseRequisitions_ProcurementCategory",
                    "[ProcurementCategory] IS NULL OR [ProcurementCategory] BETWEEN 0 AND 4");
                table.HasCheckConstraint("CK_PurchaseRequisitions_PlanItemLink",
                    "[SourcePlanItemId] IS NULL OR [SourcePlanId] IS NOT NULL");
                table.HasCheckConstraint("CK_PurchaseRequisitions_ExceptionLink",
                    "([ApprovedExceptionRuleId] IS NULL AND [ExceptionWorkflowInstanceId] IS NULL AND [ExceptionApprovalReference] IS NULL AND [ExceptionApprovedAtUtc] IS NULL) OR " +
                    "([ApprovedExceptionRuleId] IS NOT NULL AND [ExceptionWorkflowInstanceId] IS NOT NULL AND [ExceptionApprovalReference] IS NOT NULL AND [ExceptionApprovedAtUtc] IS NOT NULL)");
            });
            entity.HasOne(item => item.SourcePlan).WithMany()
                .HasForeignKey(item => item.SourcePlanId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SourcePlanItem).WithMany()
                .HasForeignKey(item => item.SourcePlanItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Budget).WithMany()
                .HasForeignKey(item => item.BudgetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ErpSystem.Core.Entities.Projects.Project>().WithMany()
                .HasForeignKey(item => item.ProjectId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SpecificationTemplate).WithMany()
                .HasForeignKey(item => item.SpecificationTemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ApprovedExceptionRule).WithMany()
                .HasForeignKey(item => item.ApprovedExceptionRuleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<WorkflowInstance>().WithMany()
                .HasForeignKey(item => item.ExceptionWorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(item => item.ExceptionApprovedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(item => item.LinkageLastUpdatedById).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureInventoryEntities(ModelBuilder builder)
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
            // TODO: Uncomment when GP-style inventory entities are implemented
            // entity.HasIndex(i => i.ItemClassId);
            // entity.HasIndex(i => i.PriceGroupId);
            // entity.HasIndex(i => i.SubstituteItem1Id);
            // entity.HasIndex(i => i.SubstituteItem2Id);
            // entity.HasIndex(i => i.SubstituteItem3Id).IsUnique(false);
            // entity.HasIndex(i => i.SubstituteItem4Id).IsUnique(false);
            // entity.HasIndex(i => i.IsKit);
            // entity.HasIndex(i => i.IsFinishedGood);

            entity.HasOne(i => i.Category)
                .WithMany(c => c.Items)
                .HasForeignKey(i => i.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // entity.HasOne(i => i.ItemClass)
            //     .WithMany(ic => ic.Items)
            //     .HasForeignKey(i => i.ItemClassId)
            //     .OnDelete(DeleteBehavior.SetNull);

            // entity.HasOne(i => i.PriceGroup)
            //     .WithMany(pg => pg.Items)
            //     .HasForeignKey(i => i.PriceGroupId)
            //     .OnDelete(DeleteBehavior.SetNull);

            // entity.HasOne(i => i.SubstituteItem1)
            //     .WithMany()
            //     .HasForeignKey(i => i.SubstituteItem1Id)
            //     .OnDelete(DeleteBehavior.NoAction);

            // entity.HasOne(i => i.SubstituteItem2)
            //     .WithMany()
            //     .HasForeignKey(i => i.SubstituteItem2Id)
            //     .OnDelete(DeleteBehavior.NoAction);

            // entity.HasOne(i => i.SubstituteItem3)
            //     .WithMany()
            //     .HasForeignKey(i => i.SubstituteItem3Id)
            //     .OnDelete(DeleteBehavior.NoAction);

            // entity.HasOne(i => i.SubstituteItem4)
            //     .WithMany()
            //     .HasForeignKey(i => i.SubstituteItem4Id)
            //     .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(i => i.UnitOfMeasureSchedule)
                .WithMany(s => s.Items)
                .HasForeignKey(i => i.UnitOfMeasureScheduleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // TODO: Uncomment when GP-style inventory entities are implemented
        // builder.Entity<ItemClass>(entity =>
        // {
        //     entity.HasIndex(ic => ic.ClassId).IsUnique();
        //     entity.HasIndex(ic => ic.IsActive);
        // });

        // builder.Entity<PriceGroup>(entity =>
        // {
        //     entity.HasIndex(pg => pg.PriceGroupCode).IsUnique();
        //     entity.HasIndex(pg => pg.IsActive);
        // });

        // UnitOfMeasureSchedule entity (GP: U of M Schedule)
        builder.Entity<UnitOfMeasureSchedule>(entity =>
        {
            entity.HasIndex(s => s.ScheduleId).IsUnique();
            entity.HasIndex(s => s.IsActive);

            entity.HasOne(s => s.BaseUnitOfMeasure)
                .WithMany()
                .HasForeignKey(s => s.BaseUnitOfMeasureId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // UnitOfMeasureScheduleDetail entity
        builder.Entity<UnitOfMeasureScheduleDetail>(entity =>
        {
            entity.HasIndex(d => d.ScheduleId);
            entity.HasIndex(d => d.UnitOfMeasureId);

            entity.HasOne(d => d.Schedule)
                .WithMany(s => s.Details)
                .HasForeignKey(d => d.ScheduleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.UnitOfMeasure)
                .WithMany()
                .HasForeignKey(d => d.UnitOfMeasureId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // TODO: Uncomment when GP-style inventory entities are implemented
        // builder.Entity<SuggestedSalesItem>(entity =>
        // {
        //     entity.HasIndex(s => s.InventoryItemId);
        //     entity.HasIndex(s => s.SuggestedItemId);
        //     entity.HasIndex(s => s.IsActive);
        //
        //     entity.HasOne(s => s.InventoryItem)
        //         .WithMany(i => i.SuggestedItems)
        //         .HasForeignKey(s => s.InventoryItemId)
        //         .OnDelete(DeleteBehavior.Cascade);
        //
        //     entity.HasOne(s => s.SuggestedItem)
        //         .WithMany()
        //         .HasForeignKey(s => s.SuggestedItemId)
        //         .OnDelete(DeleteBehavior.NoAction);
        // });

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

            // TODO: Uncomment when GP-style warehouse properties are implemented
            // entity.HasOne(w => w.Manager)
            //     .WithMany()
            //     .HasForeignKey(w => w.ManagerId)
            //     .OnDelete(DeleteBehavior.SetNull);

            // entity.HasOne(w => w.DefaultQuarantineLocation)
            //     .WithMany()
            //     .HasForeignKey(w => w.DefaultQuarantineLocationId)
            //     .OnDelete(DeleteBehavior.NoAction);

            // entity.HasOne(w => w.DefaultReceivingLocation)
            //     .WithMany()
            //     .HasForeignKey(w => w.DefaultReceivingLocationId)
            //     .OnDelete(DeleteBehavior.NoAction);

            // entity.HasOne(w => w.DefaultShippingLocation)
            //     .WithMany()
            //     .HasForeignKey(w => w.DefaultShippingLocationId)
            //     .OnDelete(DeleteBehavior.NoAction);

            // entity.HasOne(w => w.DefaultInTransitLocation)
            //     .WithMany()
            //     .HasForeignKey(w => w.DefaultInTransitLocationId)
            //     .OnDelete(DeleteBehavior.NoAction);
        });

        // WarehouseLocation entity
        builder.Entity<WarehouseLocation>(entity =>
        {
            entity.HasIndex(wl => wl.LocationCode);
            entity.HasIndex(wl => wl.WarehouseId);
            entity.HasIndex(wl => wl.ParentLocationId);
            entity.HasIndex(wl => wl.IsActive);
            entity.HasIndex(wl => wl.ConsignmentWarehouseId);

            // Warehouse relationship with Restrict to avoid cascade conflicts
            entity.HasOne(wl => wl.Warehouse)
                .WithMany(w => w.Locations)
                .HasForeignKey(wl => wl.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(wl => wl.ConsignmentWarehouse)
                .WithMany()
                .HasForeignKey(wl => wl.ConsignmentWarehouseId)
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
            entity.HasIndex(po => po.TenderAwardId);

            entity.HasOne(po => po.RequestedBy)
                .WithMany()
                .HasForeignKey(po => po.RequestedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(po => po.TenderAward)
                .WithMany()
                .HasForeignKey(po => po.TenderAwardId)
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

        // PurchaseOrderLandedCostPlan entity
        builder.Entity<PurchaseOrderLandedCostPlan>(entity =>
        {
            entity.HasIndex(p => p.PurchaseOrderId).IsUnique();
            entity.HasIndex(p => p.Status);

            entity.HasOne(p => p.PurchaseOrder)
                .WithMany()
                .HasForeignKey(p => p.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PurchaseOrderLandedCostPlanItem entity
        builder.Entity<PurchaseOrderLandedCostPlanItem>(entity =>
        {
            entity.HasIndex(i => i.PurchaseOrderLandedCostPlanId);
            entity.HasIndex(i => i.SupplierId);

            entity.HasOne(i => i.PurchaseOrderLandedCostPlan)
                .WithMany(p => p.Items)
                .HasForeignKey(i => i.PurchaseOrderLandedCostPlanId)
                .OnDelete(DeleteBehavior.Cascade);
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

        // UnitOfMeasure entity
        builder.Entity<UnitOfMeasure>(entity =>
        {
            entity.HasIndex(u => u.Code);
            entity.HasIndex(u => u.Category);
            entity.HasIndex(u => u.IsActive);

            entity.HasMany(u => u.ConversionsFrom)
                .WithOne(c => c.FromUnit)
                .HasForeignKey(c => c.FromUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(u => u.ConversionsTo)
                .WithOne(c => c.ToUnit)
                .HasForeignKey(c => c.ToUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(u => u.ItemUnits)
                .WithOne(iu => iu.UnitOfMeasure)
                .HasForeignKey(iu => iu.UnitOfMeasureId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // UnitOfMeasureConversion entity
        builder.Entity<UnitOfMeasureConversion>(entity =>
        {
            entity.HasIndex(c => new { c.FromUnitId, c.ToUnitId }).IsUnique();
            entity.HasIndex(c => c.IsActive);
        });

        // ItemUnitOfMeasure entity
        builder.Entity<ItemUnitOfMeasure>(entity =>
        {
            entity.HasIndex(iu => new { iu.InventoryItemId, iu.UnitOfMeasureId }).IsUnique();
            entity.HasIndex(iu => iu.IsActive);

            entity.HasOne(iu => iu.InventoryItem)
                .WithMany() // TODO: restore .WithMany(i => i.ItemUnitsOfMeasure) when property exists
                .HasForeignKey(iu => iu.InventoryItemId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(iu => iu.UnitOfMeasure)
                .WithMany(u => u.ItemUnits)
                .HasForeignKey(iu => iu.UnitOfMeasureId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // InventoryTransfer entity - use NoAction to avoid multiple cascade paths
        builder.Entity<InventoryTransfer>(entity =>
        {
            entity.HasIndex(it => it.TransferNumber).IsUnique();
            entity.HasIndex(it => it.SourceWarehouseId);
            entity.HasIndex(it => it.DestinationWarehouseId);
            entity.HasIndex(it => it.Status);
            entity.HasIndex(it => it.RequestDate);

            entity.HasOne(it => it.SourceWarehouse)
                .WithMany()
                .HasForeignKey(it => it.SourceWarehouseId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(it => it.DestinationWarehouse)
                .WithMany()
                .HasForeignKey(it => it.DestinationWarehouseId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(it => it.SourceLocation)
                .WithMany()
                .HasForeignKey(it => it.SourceLocationId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(it => it.DestinationLocation)
                .WithMany()
                .HasForeignKey(it => it.DestinationLocationId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(it => it.InTransitLocation)
                .WithMany()
                .HasForeignKey(it => it.InTransitLocationId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(it => it.RequestedBy)
                .WithMany()
                .HasForeignKey(it => it.RequestedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(it => it.ApprovedBy)
                .WithMany()
                .HasForeignKey(it => it.ApprovedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(it => it.ShippedBy)
                .WithMany()
                .HasForeignKey(it => it.ShippedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(it => it.ReceivedBy)
                .WithMany()
                .HasForeignKey(it => it.ReceivedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // InventoryTransferItem entity
        builder.Entity<InventoryTransferItem>(entity =>
        {
            entity.HasIndex(iti => iti.InventoryTransferId);
            entity.HasIndex(iti => iti.InventoryItemId);

            entity.HasOne(iti => iti.InventoryTransfer)
                .WithMany(it => it.Items)
                .HasForeignKey(iti => iti.InventoryTransferId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(iti => iti.InventoryItem)
                .WithMany()
                .HasForeignKey(iti => iti.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(iti => iti.SourceLocation)
                .WithMany()
                .HasForeignKey(iti => iti.SourceLocationId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(iti => iti.DestinationLocation)
                .WithMany()
                .HasForeignKey(iti => iti.DestinationLocationId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // InventoryRequisition entity - department requisitions for internal issues
        builder.Entity<InventoryRequisition>(entity =>
        {
            entity.HasIndex(ir => ir.RequisitionNumber).IsUnique();
            entity.HasIndex(ir => ir.WarehouseId);
            entity.HasIndex(ir => ir.DepartmentId);
            entity.HasIndex(ir => ir.Status);
            entity.HasIndex(ir => ir.RequestDate);
            entity.HasIndex(ir => ir.RequiredDate);
            entity.HasIndex(ir => new { ir.TenantId, ir.Status });

            entity.HasOne(ir => ir.Warehouse)
                .WithMany()
                .HasForeignKey(ir => ir.WarehouseId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(ir => ir.Location)
                .WithMany()
                .HasForeignKey(ir => ir.LocationId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(ir => ir.RequestedBy)
                .WithMany()
                .HasForeignKey(ir => ir.RequestedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(ir => ir.ApprovedBy)
                .WithMany()
                .HasForeignKey(ir => ir.ApprovedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(ir => ir.IssuedBy)
                .WithMany()
                .HasForeignKey(ir => ir.IssuedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // InventoryRequisitionItem entity
        builder.Entity<InventoryRequisitionItem>(entity =>
        {
            entity.HasIndex(iri => iri.InventoryRequisitionId);
            entity.HasIndex(iri => iri.InventoryItemId);

            entity.HasOne(iri => iri.InventoryRequisition)
                .WithMany(ir => ir.Items)
                .HasForeignKey(iri => iri.InventoryRequisitionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(iri => iri.InventoryItem)
                .WithMany()
                .HasForeignKey(iri => iri.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(iri => iri.Location)
                .WithMany()
                .HasForeignKey(iri => iri.LocationId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // GoodsReceiptNote entity
        builder.Entity<GoodsReceiptNote>(entity =>
        {
            entity.HasIndex(grn => grn.GRNNumber).IsUnique();
            entity.HasIndex(grn => grn.WarehouseId);
            entity.HasIndex(grn => grn.SupplierId);
            entity.HasIndex(grn => grn.Status);
            entity.HasIndex(grn => grn.ReceiptDate);

            entity.HasOne(grn => grn.Warehouse)
                .WithMany()
                .HasForeignKey(grn => grn.WarehouseId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(grn => grn.ReceivingLocation)
                .WithMany()
                .HasForeignKey(grn => grn.ReceivingLocationId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(grn => grn.ReceivedBy)
                .WithMany()
                .HasForeignKey(grn => grn.ReceivedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(grn => grn.InspectedBy)
                .WithMany()
                .HasForeignKey(grn => grn.InspectedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // GoodsReceiptNoteItem entity
        builder.Entity<GoodsReceiptNoteItem>(entity =>
        {
            entity.HasIndex(grni => grni.GoodsReceiptNoteId);
            entity.HasIndex(grni => grni.InventoryItemId);

            entity.HasOne(grni => grni.GoodsReceiptNote)
                .WithMany(grn => grn.Items)
                .HasForeignKey(grni => grni.GoodsReceiptNoteId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(grni => grni.InventoryItem)
                .WithMany()
                .HasForeignKey(grni => grni.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(grni => grni.StorageLocation)
                .WithMany()
                .HasForeignKey(grni => grni.StorageLocationId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // PhysicalCount entity
        builder.Entity<PhysicalCount>(entity =>
        {
            entity.HasIndex(pc => pc.CountNumber).IsUnique();
            entity.HasIndex(pc => pc.WarehouseId);
            entity.HasIndex(pc => pc.Status);
            entity.HasIndex(pc => pc.CountDate);

            entity.HasOne(pc => pc.Warehouse)
                .WithMany()
                .HasForeignKey(pc => pc.WarehouseId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(pc => pc.Location)
                .WithMany()
                .HasForeignKey(pc => pc.LocationId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(pc => pc.Category)
                .WithMany()
                .HasForeignKey(pc => pc.CategoryId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(pc => pc.InitiatedBy)
                .WithMany()
                .HasForeignKey(pc => pc.InitiatedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(pc => pc.CountedBy)
                .WithMany()
                .HasForeignKey(pc => pc.CountedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(pc => pc.ApprovedBy)
                .WithMany()
                .HasForeignKey(pc => pc.ApprovedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(pc => pc.PostedBy)
                .WithMany()
                .HasForeignKey(pc => pc.PostedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // PhysicalCountItem entity
        builder.Entity<PhysicalCountItem>(entity =>
        {
            entity.HasIndex(pci => pci.PhysicalCountId);
            entity.HasIndex(pci => pci.InventoryItemId);

            entity.HasOne(pci => pci.PhysicalCount)
                .WithMany(pc => pc.Items)
                .HasForeignKey(pci => pci.PhysicalCountId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pci => pci.InventoryItem)
                .WithMany()
                .HasForeignKey(pci => pci.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(pci => pci.Location)
                .WithMany()
                .HasForeignKey(pci => pci.LocationId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(pci => pci.CountedBy)
                .WithMany()
                .HasForeignKey(pci => pci.CountedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // LandedCost entity
        builder.Entity<LandedCost>(entity =>
        {
            entity.HasIndex(lc => lc.LandedCostNumber).IsUnique();
            entity.HasIndex(lc => lc.GoodsReceiptNoteId);
            entity.HasIndex(lc => lc.Status);

            entity.HasOne(lc => lc.GoodsReceiptNote)
                .WithMany()
                .HasForeignKey(lc => lc.GoodsReceiptNoteId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(lc => lc.ApprovedBy)
                .WithMany()
                .HasForeignKey(lc => lc.ApprovedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(lc => lc.PostedBy)
                .WithMany()
                .HasForeignKey(lc => lc.PostedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // LandedCostItem entity
        builder.Entity<LandedCostItem>(entity =>
        {
            entity.HasIndex(lci => lci.LandedCostId);

            entity.HasOne(lci => lci.LandedCost)
                .WithMany(lc => lc.Items)
                .HasForeignKey(lci => lci.LandedCostId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // LandedCostAllocation entity
        builder.Entity<LandedCostAllocation>(entity =>
        {
            entity.HasIndex(lca => lca.LandedCostId);
            entity.HasIndex(lca => lca.InventoryItemId);
            entity.HasIndex(lca => lca.GoodsReceiptNoteItemId);

            entity.HasOne(lca => lca.LandedCost)
                .WithMany(lc => lc.Allocations)
                .HasForeignKey(lca => lca.LandedCostId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(lca => lca.LandedCostItem)
                .WithMany()
                .HasForeignKey(lca => lca.LandedCostItemId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(lca => lca.GoodsReceiptNoteItem)
                .WithMany()
                .HasForeignKey(lca => lca.GoodsReceiptNoteItemId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(lca => lca.InventoryItem)
                .WithMany()
                .HasForeignKey(lca => lca.InventoryItemId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(lca => lca.CostLayer)
                .WithMany()
                .HasForeignKey(lca => lca.CostLayerId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // InventoryCostLayer entity
        builder.Entity<InventoryCostLayer>(entity =>
        {
            entity.HasIndex(icl => icl.InventoryItemId);
            entity.HasIndex(icl => icl.WarehouseId);
            entity.HasIndex(icl => icl.LayerDate);
            entity.HasIndex(icl => icl.IsFullyConsumed);

            entity.HasOne(icl => icl.InventoryItem)
                .WithMany()
                .HasForeignKey(icl => icl.InventoryItemId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(icl => icl.Warehouse)
                .WithMany()
                .HasForeignKey(icl => icl.WarehouseId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // PurchaseReturn entity
        builder.Entity<PurchaseReturn>(entity =>
        {
            entity.HasIndex(pr => pr.ReturnNumber).IsUnique();
            entity.HasIndex(pr => pr.SupplierId);
            entity.HasIndex(pr => pr.WarehouseId);
            entity.HasIndex(pr => pr.Status);
            entity.HasIndex(pr => pr.ReturnDate);

            entity.HasOne(pr => pr.GoodsReceiptNote)
                .WithMany()
                .HasForeignKey(pr => pr.GoodsReceiptNoteId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(pr => pr.Warehouse)
                .WithMany()
                .HasForeignKey(pr => pr.WarehouseId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(pr => pr.RequestedBy)
                .WithMany()
                .HasForeignKey(pr => pr.RequestedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(pr => pr.ApprovedBy)
                .WithMany()
                .HasForeignKey(pr => pr.ApprovedById)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // PurchaseReturnItem entity
        builder.Entity<PurchaseReturnItem>(entity =>
        {
            entity.HasIndex(pri => pri.PurchaseReturnId);
            entity.HasIndex(pri => pri.InventoryItemId);

            entity.HasOne(pri => pri.PurchaseReturn)
                .WithMany(pr => pr.Items)
                .HasForeignKey(pri => pri.PurchaseReturnId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pri => pri.InventoryItem)
                .WithMany()
                .HasForeignKey(pri => pri.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(pri => pri.GoodsReceiptNoteItem)
                .WithMany()
                .HasForeignKey(pri => pri.GoodsReceiptNoteItemId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(pri => pri.Location)
                .WithMany()
                .HasForeignKey(pri => pri.LocationId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // InventoryMovement entity - Immutable source of truth for all inventory transactions
        builder.Entity<InventoryMovement>(entity =>
        {
            // Unique index on MovementNumber per tenant
            entity.HasIndex(im => new { im.TenantId, im.MovementNumber })
                .IsUnique()
                .HasDatabaseName("IX_InventoryMovements_TenantId_MovementNumber");

            // Performance indexes
            entity.HasIndex(im => im.InventoryItemId)
                .HasDatabaseName("IX_InventoryMovements_InventoryItemId");
            entity.HasIndex(im => im.WarehouseId)
                .HasDatabaseName("IX_InventoryMovements_WarehouseId");
            entity.HasIndex(im => im.MovementDate)
                .HasDatabaseName("IX_InventoryMovements_MovementDate");
            entity.HasIndex(im => im.PostingDate)
                .HasDatabaseName("IX_InventoryMovements_PostingDate");
            entity.HasIndex(im => im.MovementType)
                .HasDatabaseName("IX_InventoryMovements_MovementType");
            entity.HasIndex(im => im.ReferenceType)
                .HasDatabaseName("IX_InventoryMovements_ReferenceType");
            entity.HasIndex(im => im.ReferenceId)
                .HasDatabaseName("IX_InventoryMovements_ReferenceId");
            entity.HasIndex(im => im.IsPosted)
                .HasDatabaseName("IX_InventoryMovements_IsPosted");
            entity.HasIndex(im => im.CostLayerId)
                .HasDatabaseName("IX_InventoryMovements_CostLayerId");

            // Composite index for common queries
            entity.HasIndex(im => new { im.TenantId, im.InventoryItemId, im.WarehouseId, im.MovementDate })
                .HasDatabaseName("IX_InventoryMovements_Item_Warehouse_Date");

            // Relationships
            entity.HasOne(im => im.InventoryItem)
                .WithMany()
                .HasForeignKey(im => im.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(im => im.Warehouse)
                .WithMany()
                .HasForeignKey(im => im.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(im => im.Location)
                .WithMany()
                .HasForeignKey(im => im.LocationId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(im => im.CostLayer)
                .WithMany(il => il.Movements)
                .HasForeignKey(im => im.CostLayerId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(im => im.CreatedBy)
                .WithMany()
                .HasForeignKey(im => im.CreatedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(im => im.PostedBy)
                .WithMany()
                .HasForeignKey(im => im.PostedById)
                .OnDelete(DeleteBehavior.NoAction);

            // Self-referencing relationship for reversals
            entity.HasOne(im => im.ReversedMovement)
                .WithMany()
                .HasForeignKey(im => im.ReversedMovementId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // InventoryLayer entity - FIFO cost layers
        builder.Entity<InventoryLayer>(entity =>
        {
            // Unique index on LayerNumber per tenant
            entity.HasIndex(il => new { il.TenantId, il.LayerNumber })
                .IsUnique()
                .HasDatabaseName("IX_InventoryLayers_TenantId_LayerNumber");

            // Performance indexes
            entity.HasIndex(il => il.InventoryItemId)
                .HasDatabaseName("IX_InventoryLayers_InventoryItemId");
            entity.HasIndex(il => il.WarehouseId)
                .HasDatabaseName("IX_InventoryLayers_WarehouseId");
            entity.HasIndex(il => il.LayerDate)
                .HasDatabaseName("IX_InventoryLayers_LayerDate");
            entity.HasIndex(il => il.IsFullyConsumed)
                .HasDatabaseName("IX_InventoryLayers_IsFullyConsumed");
            entity.HasIndex(il => il.IsActive)
                .HasDatabaseName("IX_InventoryLayers_IsActive");

            // Composite index for FIFO consumption queries (oldest first with remaining quantity)
            entity.HasIndex(il => new { il.TenantId, il.InventoryItemId, il.WarehouseId, il.IsFullyConsumed, il.LayerDate })
                .HasDatabaseName("IX_InventoryLayers_FIFO_Consumption");

            // Relationships
            entity.HasOne(il => il.InventoryItem)
                .WithMany()
                .HasForeignKey(il => il.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(il => il.Warehouse)
                .WithMany()
                .HasForeignKey(il => il.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(il => il.Location)
                .WithMany()
                .HasForeignKey(il => il.LocationId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // InventoryBalance entity - Performance cache (denormalized view)
        builder.Entity<InventoryBalance>(entity =>
        {
            // Unique composite key on Item + Warehouse + Location per tenant
            entity.HasIndex(ib => new { ib.TenantId, ib.InventoryItemId, ib.WarehouseId, ib.LocationId })
                .IsUnique()
                .HasDatabaseName("IX_InventoryBalances_Unique_Item_Warehouse_Location");

            // Performance indexes
            entity.HasIndex(ib => ib.InventoryItemId)
                .HasDatabaseName("IX_InventoryBalances_InventoryItemId");
            entity.HasIndex(ib => ib.WarehouseId)
                .HasDatabaseName("IX_InventoryBalances_WarehouseId");
            entity.HasIndex(ib => ib.LastMovementDate)
                .HasDatabaseName("IX_InventoryBalances_LastMovementDate");

            // Index for low stock queries
            entity.HasIndex(ib => new { ib.TenantId, ib.QuantityOnHand })
                .HasDatabaseName("IX_InventoryBalances_QuantityOnHand");

            // Relationships
            entity.HasOne(ib => ib.InventoryItem)
                .WithMany()
                .HasForeignKey(ib => ib.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ib => ib.Warehouse)
                .WithMany()
                .HasForeignKey(ib => ib.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ib => ib.Location)
                .WithMany()
                .HasForeignKey(ib => ib.LocationId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private static void ConfigureFinanceCommonEntities(ModelBuilder builder)
    {
        // Configure PaymentTerm entity
        builder.Entity<PaymentTerm>(entity =>
        {
            entity.HasIndex(pt => pt.Code);
            entity.HasIndex(pt => pt.IsActive);
            entity.HasIndex(pt => pt.IsDefault);
            entity.HasIndex(pt => pt.ApplicableTo);
            entity.HasIndex(pt => new { pt.TenantId, pt.Code })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });

        builder.Entity<FinanceSettings>(entity =>
        {
            entity.HasIndex(s => s.TenantId).IsUnique();
            entity.Property(s => s.BaseCurrency).HasMaxLength(3).IsRequired();
            entity.Property(s => s.FunctionalCurrencyLockedReason).HasMaxLength(500);
            entity.HasOne(s => s.UnrealizedFxGainAccount)
                .WithMany()
                .HasForeignKey(s => s.UnrealizedFxGainAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(s => s.UnrealizedFxLossAccount)
                .WithMany()
                .HasForeignKey(s => s.UnrealizedFxLossAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(s => s.SupplierAdvanceAccount)
                .WithMany()
                .HasForeignKey(s => s.SupplierAdvanceAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(s => s.CustomerAdvanceAccount)
                .WithMany()
                .HasForeignKey(s => s.CustomerAdvanceAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ExchangeRate>(entity =>
        {
            entity.HasIndex(r => new { r.TenantId, r.BaseCurrencyCode, r.TargetCurrencyCode, r.RateType, r.EffectiveDate })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(r => new { r.TenantId, r.BaseCurrencyCode, r.TargetCurrencyCode, r.RateType, r.IsActive });
            entity.HasIndex(r => r.HasBeenUsedInTransactions);
            entity.Property(r => r.BaseCurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(r => r.TargetCurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(r => r.RateSource).HasMaxLength(100).IsRequired();
        });

        // Configure Currency entity
        builder.Entity<Currency>(entity =>
        {
            entity.HasIndex(c => c.Code);
            entity.HasIndex(c => c.IsActive);
            entity.HasIndex(c => c.IsBaseCurrency);
            entity.HasIndex(c => new { c.TenantId, c.Code }).IsUnique();
        });
    }

    private static void SeedMaintenanceData(ModelBuilder builder, Guid tenantId)
    {
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

        // â”€â”€ Global: default all FKs to Restrict to prevent cascade-cycle errors on SQL Server â”€â”€
        foreach (var relationship in builder.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys()))
        {
            if (relationship.DeleteBehavior == DeleteBehavior.Cascade)
                relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    private static void ConfigureProcurementConfiguration(ModelBuilder builder)
    {
        builder.Entity<ProcurementConfigurationProfile>(entity =>
        {
            entity.Property(item => item.ProfileCode).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ProfileKey, item.Version }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ProfileCode, item.Version }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ProfileKey, item.LifecycleStatus });
            entity.HasIndex(item => new { item.TenantId, item.LifecycleStatus, item.EffectiveFrom });
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_ProcurementConfigurationProfiles_Version", "[Version] > 0");
                table.HasCheckConstraint("CK_ProcurementConfigurationProfiles_EffectivePeriod", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                table.HasCheckConstraint("CK_ProcurementConfigurationProfiles_Lifecycle", "[LifecycleStatus] IN (0, 1, 2)");
            });
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProcurementConfigurationProfile>()
                .WithMany()
                .HasForeignKey(item => item.SupersedesProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProcurementConfigurationDecision>(entity =>
        {
            entity.Property(item => item.DecisionKey).IsUnicode(false).IsFixedLength();
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ProfileId, item.DecisionKey }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.DecisionKey, item.Status });
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_ProcurementConfigurationDecisions_SchemaVersion", "[SchemaVersion] > 0");
                table.HasCheckConstraint("CK_ProcurementConfigurationDecisions_DecisionKey", "[DecisionKey] LIKE 'DEC-[0-9][0-9][0-9]'");
                table.HasCheckConstraint("CK_ProcurementConfigurationDecisions_EffectivePeriod", "[EffectiveTo] IS NULL OR [EffectiveFrom] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                table.HasCheckConstraint("CK_ProcurementConfigurationDecisions_Status", "[Status] IN (0, 1, 2, 3)");
            });
            entity.HasOne(item => item.Profile)
                .WithMany(item => item.Decisions)
                .HasForeignKey(item => item.ProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProcurementConfigurationDecision>()
                .WithMany()
                .HasForeignKey(item => item.SourceDecisionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProcurementConfigurationEvidenceLink>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.DecisionId });
            entity.HasIndex(item => new { item.TenantId, item.FileUploadRecordId });
            entity.HasOne(item => item.Profile)
                .WithMany(item => item.EvidenceLinks)
                .HasForeignKey(item => item.ProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Decision)
                .WithMany(item => item.EvidenceLinks)
                .HasForeignKey(item => item.DecisionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.FileUploadRecord)
                .WithMany()
                .HasForeignKey(item => item.FileUploadRecordId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProcurementConfigurationRevision>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.ProfileId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId });
            entity.HasOne<ProcurementConfigurationProfile>()
                .WithMany()
                .HasForeignKey(item => item.ProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProcurementConfigurationDecision>()
                .WithMany()
                .HasForeignKey(item => item.DecisionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProcurementPolicy(ModelBuilder builder)
    {
        builder.Entity<ProcurementPolicySet>(entity =>
        {
            entity.Property(item => item.Code).IsUnicode(false);
            entity.Property(item => item.DefaultCurrencyCode).IsUnicode(false).IsFixedLength();
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.PolicyKey, item.Version }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.Code, item.Version }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.PolicyKey, item.LifecycleStatus });
            entity.HasIndex(item => new { item.TenantId, item.LifecycleStatus, item.EffectiveFrom });
            entity.HasIndex(item => new { item.TenantId, item.SourceConfigurationProfileId });
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_ProcurementPolicySets_Version", "[Version] > 0");
                table.HasCheckConstraint("CK_ProcurementPolicySets_EffectivePeriod", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                table.HasCheckConstraint("CK_ProcurementPolicySets_Lifecycle", "[LifecycleStatus] IN (0, 1, 2)");
                table.HasCheckConstraint("CK_ProcurementPolicySets_Currency", "LEN([DefaultCurrencyCode]) = 3");
            });
            entity.HasOne(item => item.SourceConfigurationProfile).WithMany()
                .HasForeignKey(item => item.SourceConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProcurementPolicySet>().WithMany().HasForeignKey(item => item.BasePolicySetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProcurementPolicySet>().WithMany().HasForeignKey(item => item.SupersedesPolicySetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProcurementPolicyCategoryRule>(entity =>
        {
            ConfigurePolicyRule(entity);
            entity.HasIndex(item => new { item.TenantId, item.PolicySetId, item.Category, item.ServiceClass });
            entity.HasOne(item => item.PolicySet).WithMany(item => item.CategoryRules).HasForeignKey(item => item.PolicySetId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ProcurementPolicyMethodRule>(entity =>
        {
            ConfigurePolicyRule(entity);
            entity.HasIndex(item => new { item.TenantId, item.PolicySetId, item.Category, item.Method });
            entity.HasOne(item => item.PolicySet).WithMany(item => item.MethodRules).HasForeignKey(item => item.PolicySetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<WorkflowDefinition>().WithMany().HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ProcurementPolicyThresholdRule>(entity =>
        {
            ConfigurePolicyRule(entity);
            entity.Property(item => item.CurrencyCode).IsUnicode(false).IsFixedLength();
            entity.HasIndex(item => new { item.TenantId, item.PolicySetId, item.Category, item.Method, item.CurrencyCode });
            entity.ToTable(table => table.HasCheckConstraint("CK_ProcurementPolicyThresholdRules_Bounds", "[LowerBound] >= 0 AND ([UpperBound] IS NULL OR [UpperBound] >= [LowerBound])"));
            entity.HasOne(item => item.PolicySet).WithMany(item => item.ThresholdRules).HasForeignKey(item => item.PolicySetId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ProcurementPolicyAuthorityRule>(entity =>
        {
            ConfigurePolicyRule(entity);
            entity.Property(item => item.CurrencyCode).IsUnicode(false).IsFixedLength();
            entity.HasIndex(item => new { item.TenantId, item.PolicySetId, item.Category, item.Sequence });
            entity.ToTable(table => table.HasCheckConstraint("CK_ProcurementPolicyAuthorityRules_Bounds", "[LowerBound] >= 0 AND ([UpperBound] IS NULL OR [UpperBound] >= [LowerBound])"));
            entity.HasOne(item => item.PolicySet).WithMany(item => item.AuthorityRules).HasForeignKey(item => item.PolicySetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<WorkflowDefinition>().WithMany().HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ProcurementPolicyEvidenceRule>(entity =>
        {
            ConfigurePolicyRule(entity);
            entity.HasIndex(item => new { item.TenantId, item.PolicySetId, item.Stage });
            entity.HasOne(item => item.PolicySet).WithMany(item => item.EvidenceRules).HasForeignKey(item => item.PolicySetId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ProcurementPolicyExceptionRule>(entity =>
        {
            ConfigurePolicyRule(entity);
            entity.HasIndex(item => new { item.TenantId, item.PolicySetId, item.ExceptionType });
            entity.HasOne(item => item.PolicySet).WithMany(item => item.ExceptionRules).HasForeignKey(item => item.PolicySetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<WorkflowDefinition>().WithMany().HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ProcurementPolicySodRule>(entity =>
        {
            ConfigurePolicyRule(entity);
            entity.HasIndex(item => new { item.TenantId, item.PolicySetId, item.EntityType, item.Action });
            entity.HasOne(item => item.PolicySet).WithMany(item => item.SodRules).HasForeignKey(item => item.PolicySetId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ProcurementPolicyRevision>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.PolicySetId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId });
            entity.HasOne<ProcurementPolicySet>().WithMany().HasForeignKey(item => item.PolicySetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProcurementRequisitionAuthorityRoutes(ModelBuilder builder)
    {
        builder.Entity<ProcurementRequisitionAuthorityRoute>(entity =>
        {
            entity.Property(item => item.CurrencyCode).IsUnicode(false).IsFixedLength();
            entity.Property(item => item.PolicyCode).IsUnicode(false);
            entity.Property(item => item.WorkflowEntityTypeCode).IsUnicode(false);
            entity.Property(item => item.IntegrityHash).IsUnicode(false).IsFixedLength();
            entity.HasIndex(item => new { item.TenantId, item.PurchaseRequisitionId, item.AttemptNumber }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.RouteReference }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.PolicySetId, item.PolicyVersion });
            entity.HasIndex(item => new { item.TenantId, item.WorkflowDefinitionId, item.WorkflowVersion });
            entity.HasIndex(item => new { item.TenantId, item.CapturedAtUtc });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProcurementRequisitionAuthorityRoutes_NoMutation");
                table.HasTrigger("TR_ProcurementRequisitionAuthorityRoutes_TenantGuard");
                table.HasCheckConstraint("CK_ProcurementRequisitionAuthorityRoutes_Attempt", "[AttemptNumber] > 0");
                table.HasCheckConstraint("CK_ProcurementRequisitionAuthorityRoutes_Amount", "[Amount] >= 0");
                table.HasCheckConstraint("CK_ProcurementRequisitionAuthorityRoutes_Currency", "LEN([CurrencyCode]) = 3");
                table.HasCheckConstraint("CK_ProcurementRequisitionAuthorityRoutes_Hash", "LEN([IntegrityHash]) = 64");
                table.HasCheckConstraint("CK_ProcurementRequisitionAuthorityRoutes_Versions", "[PolicyVersion] > 0 AND [WorkflowVersion] > 0");
            });
            entity.HasOne(item => item.PurchaseRequisition).WithMany()
                .HasForeignKey(item => item.PurchaseRequisitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.PolicySet).WithMany()
                .HasForeignKey(item => item.PolicySetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.WorkflowDefinition).WithMany()
                .HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProcurementConfigurationProfile>().WithMany()
                .HasForeignKey(item => item.SourceConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProcurementPolicySet>().WithMany()
                .HasForeignKey(item => item.BasePolicySetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(item => item.CapturedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany()
                .HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProcurementRequisitionAuthorityRouteStep>(entity =>
        {
            entity.Property(item => item.RulePolicyCode).IsUnicode(false);
            entity.Property(item => item.RuleCode).IsUnicode(false);
            entity.Property(item => item.SourceDecisionKey).IsUnicode(false);
            entity.Property(item => item.CurrencyCode).IsUnicode(false).IsFixedLength();
            entity.Property(item => item.LowerBound).HasColumnType("decimal(18,4)");
            entity.Property(item => item.UpperBound).HasColumnType("decimal(18,4)");
            entity.HasIndex(item => new { item.TenantId, item.AuthorityRouteId, item.Sequence }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.AuthorityRuleId });
            entity.HasIndex(item => new { item.TenantId, item.WorkflowStepId });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProcurementRequisitionAuthorityRouteSteps_NoMutation");
                table.HasTrigger("TR_ProcurementRequisitionAuthorityRouteSteps_TenantGuard");
                table.HasCheckConstraint("CK_ProcurementRequisitionAuthorityRouteSteps_Sequence", "[Sequence] BETWEEN 1 AND 100");
                table.HasCheckConstraint("CK_ProcurementRequisitionAuthorityRouteSteps_Bounds", "[LowerBound] >= 0 AND ([UpperBound] IS NULL OR [UpperBound] >= [LowerBound])");
                table.HasCheckConstraint("CK_ProcurementRequisitionAuthorityRouteSteps_Currency", "LEN([CurrencyCode]) = 3");
                table.HasCheckConstraint("CK_ProcurementRequisitionAuthorityRouteSteps_Quorum", "[Quorum] BETWEEN 1 AND 100");
                table.HasCheckConstraint("CK_ProcurementRequisitionAuthorityRouteSteps_Decision", "[SourceDecisionKey] LIKE 'DEC-[0-9][0-9][0-9]'");
            });
            entity.HasOne(item => item.AuthorityRoute).WithMany(item => item.Steps)
                .HasForeignKey(item => item.AuthorityRouteId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.AuthorityRule).WithMany()
                .HasForeignKey(item => item.AuthorityRuleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.RulePolicySet).WithMany()
                .HasForeignKey(item => item.RulePolicySetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.WorkflowDefinition).WithMany()
                .HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.WorkflowStep).WithMany()
                .HasForeignKey(item => item.WorkflowStepId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany()
                .HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProcurementRequisitionSourcingReleases(ModelBuilder builder)
    {
        builder.Entity<ProcurementRequisitionSourcingRelease>(entity =>
        {
            entity.Property(item => item.ReleaseReference).IsUnicode(false);
            entity.Property(item => item.SpecificationTemplateCode).IsUnicode(false);
            entity.Property(item => item.ControlFingerprint).IsUnicode(false).IsFixedLength();
            entity.Property(item => item.IntegrityHash).IsUnicode(false).IsFixedLength();
            entity.HasIndex(item => new { item.TenantId, item.PurchaseRequisitionId, item.AttemptNumber }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ReleaseReference }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.PurchaseRequisitionId, item.ControlFingerprint }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ReleasedAtUtc });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProcurementRequisitionSourcingReleases_NoMutation");
                table.HasTrigger("TR_ProcurementRequisitionSourcingReleases_TenantGuard");
                table.HasCheckConstraint("CK_ProcurementRequisitionSourcingReleases_Attempt", "[AttemptNumber] > 0");
                table.HasCheckConstraint("CK_ProcurementRequisitionSourcingReleases_Hashes", "LEN([ControlFingerprint]) = 64 AND LEN([IntegrityHash]) = 64");
                table.HasCheckConstraint("CK_ProcurementRequisitionSourcingReleases_Lineage", "[SourcePlanId] <> '00000000-0000-0000-0000-000000000000' AND [SourcePlanItemId] <> '00000000-0000-0000-0000-000000000000' AND [SpecificationTemplateId] <> '00000000-0000-0000-0000-000000000000' AND [BudgetCommitmentId] <> '00000000-0000-0000-0000-000000000000' AND [AuthorityRouteId] <> '00000000-0000-0000-0000-000000000000' AND [WorkflowInstanceId] <> '00000000-0000-0000-0000-000000000000'");
            });
            entity.HasOne(item => item.PurchaseRequisition).WithMany(item => item.SourcingReleases)
                .HasForeignKey(item => item.PurchaseRequisitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.AppSubmission).WithMany().HasForeignKey(item => item.AppSubmissionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ApprovedExceptionRule).WithMany().HasForeignKey(item => item.ApprovedExceptionRuleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ExceptionWorkflowInstance).WithMany().HasForeignKey(item => item.ExceptionWorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SpecificationTemplate).WithMany().HasForeignKey(item => item.SpecificationTemplateId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.BudgetCommitment).WithMany().HasForeignKey(item => item.BudgetCommitmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.AuthorityRoute).WithMany().HasForeignKey(item => item.AuthorityRouteId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.WorkflowInstance).WithMany().HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ReleasedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProcurementAccessControl(ModelBuilder builder)
    {
        builder.Entity<ProcurementResponsibilityAssignment>(entity =>
        {
            entity.Property(item => item.RoleName).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.UserId, item.RoleName })
                .IsUnique().HasFilter("[IsDeleted] = 0 AND [IsActive] = 1");
            entity.HasIndex(item => new { item.TenantId, item.UserId, item.EffectiveFrom });
            entity.HasIndex(item => new { item.TenantId, item.RoleId, item.IsActive });
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_ProcurementResponsibilityAssignments_WarehouseScope", "[WarehouseScopeMode] IN (0, 1, 2)");
                table.HasCheckConstraint("CK_ProcurementResponsibilityAssignments_EffectivePeriod", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
            });
            entity.HasOne(item => item.User).WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Role).WithMany().HasForeignKey(item => item.RoleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProcurementResponsibilityWarehouse>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.AssignmentId, item.WarehouseId })
                .IsUnique().HasFilter("[IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.WarehouseId });
            entity.HasOne(item => item.Assignment).WithMany(item => item.Warehouses)
                .HasForeignKey(item => item.AssignmentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Warehouse).WithMany()
                .HasForeignKey(item => item.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProcurementCommittee>(entity =>
        {
            entity.Property(item => item.Code).IsUnicode(false);
            entity.Property(item => item.RequiredRoleName).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.Code })
                .IsUnique().HasFilter("[IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.CommitteeType, item.Status });
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_ProcurementCommittees_Type", "[CommitteeType] IN (0, 1, 2, 3)");
                table.HasCheckConstraint("CK_ProcurementCommittees_Status", "[Status] IN (0, 1, 2)");
                table.HasCheckConstraint("CK_ProcurementCommittees_Quorum", "[RequiredQuorum] BETWEEN 1 AND 50");
                table.HasCheckConstraint("CK_ProcurementCommittees_EffectivePeriod", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
            });
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProcurementCommitteeMember>(entity =>
        {
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.CommitteeId, item.AssignmentId })
                .IsUnique().HasFilter("[IsDeleted] = 0 AND [IsActive] = 1");
            entity.HasIndex(item => new { item.TenantId, item.AssignmentId, item.IsActive });
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_ProcurementCommitteeMembers_Kind", "[MemberKind] IN (0, 1, 2, 3, 4)");
                table.HasCheckConstraint("CK_ProcurementCommitteeMembers_EffectivePeriod", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
            });
            entity.HasOne(item => item.Committee).WithMany(item => item.Members)
                .HasForeignKey(item => item.CommitteeId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Assignment).WithMany(item => item.CommitteeMemberships)
                .HasForeignKey(item => item.AssignmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProcurementControlEvents(ModelBuilder builder)
    {
        builder.Entity<ProcurementControlEvent>(entity =>
        {
            entity.ToTable("ProcurementControlEvents", table =>
            {
                table.HasTrigger("TR_ProcurementControlEvents_AppendOnly");
                table.HasCheckConstraint("CK_ProcurementControlEvents_Result", "[Result] BETWEEN 0 AND 6");
                table.HasCheckConstraint("CK_ProcurementControlEvents_SchemaVersion", "[SchemaVersion] >= 1");
            });
            entity.Property(item => item.EventKey).IsUnicode(false);
            entity.Property(item => item.EventType).IsUnicode(false);
            entity.Property(item => item.Action).IsUnicode(false);
            entity.Property(item => item.RuleCode).IsUnicode(false);
            entity.Property(item => item.RuleVersion).IsUnicode(false);
            entity.Property(item => item.SourceType).IsUnicode(false);
            entity.Property(item => item.CorrelationId).IsUnicode(false);
            entity.Property(item => item.CausationId).IsUnicode(false);
            entity.Property(item => item.IntegrityHash).IsUnicode(false).IsFixedLength();
            entity.HasIndex(item => new { item.TenantId, item.EventKey }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.OccurredAtUtc });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId, item.OccurredAtUtc });
            entity.HasIndex(item => new { item.TenantId, item.EventType, item.Result, item.OccurredAtUtc });
            entity.HasIndex(item => new { item.TenantId, item.SourceType, item.SourceReference });
            entity.HasIndex(item => new { item.TenantId, item.RuleCode, item.OccurredAtUtc });
            entity.HasOne(item => item.ActorUser).WithMany().HasForeignKey(item => item.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(item => item.EvidenceLinks).WithOne(item => item.ControlEvent)
                .HasForeignKey(item => item.ControlEventId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProcurementControlEventEvidenceLink>(entity =>
        {
            entity.ToTable("ProcurementControlEventEvidenceLinks", table =>
            {
                table.HasTrigger("TR_ProcurementControlEventEvidenceLinks_AppendOnly");
                table.HasCheckConstraint("CK_ProcurementControlEventEvidenceLinks_Kind", "[ReferenceKind] BETWEEN 0 AND 2");
                table.HasCheckConstraint("CK_ProcurementControlEventEvidenceLinks_ReferenceTarget",
                    "([ReferenceKind] = 0 AND [WorkflowEvidenceDocumentId] IS NOT NULL AND [FileUploadRecordId] IS NULL) OR " +
                    "([ReferenceKind] = 1 AND [WorkflowEvidenceDocumentId] IS NULL AND [FileUploadRecordId] IS NOT NULL) OR " +
                    "([ReferenceKind] = 2 AND [WorkflowEvidenceDocumentId] IS NULL AND [FileUploadRecordId] IS NULL)");
            });
            entity.Property(item => item.Reference).IsUnicode(false);
            entity.HasIndex(item => new { item.TenantId, item.ControlEventId, item.ReferenceKind, item.Reference }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.WorkflowEvidenceDocumentId });
            entity.HasIndex(item => new { item.TenantId, item.FileUploadRecordId });
            entity.HasOne(item => item.WorkflowEvidenceDocument).WithMany()
                .HasForeignKey(item => item.WorkflowEvidenceDocumentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.FileUploadRecord).WithMany()
                .HasForeignKey(item => item.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProcurementMasterDataChanges(ModelBuilder builder)
    {
        builder.Entity<ProcurementMasterDataControlPolicy>(entity =>
        {
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.ResourceType, item.Version }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ResourceType, item.Status });
            entity.HasIndex(item => new { item.TenantId, item.Status, item.EffectiveFromUtc });
            entity.ToTable("ProcurementMasterDataControlPolicies", table =>
            {
                table.HasTrigger("TR_ProcurementMasterDataControlPolicies_LifecycleGuard");
                table.HasCheckConstraint("CK_ProcurementMasterDataControlPolicies_ResourceType", "[ResourceType] BETWEEN 0 AND 8");
                table.HasCheckConstraint("CK_ProcurementMasterDataControlPolicies_Status", "[Status] BETWEEN 0 AND 2");
                table.HasCheckConstraint("CK_ProcurementMasterDataControlPolicies_Version", "[Version] >= 1");
                table.HasCheckConstraint("CK_ProcurementMasterDataControlPolicies_EffectivePeriod", "[EffectiveToUtc] IS NULL OR [EffectiveToUtc] >= [EffectiveFromUtc]");
                table.HasCheckConstraint("CK_ProcurementMasterDataControlPolicies_RequiredControls", "[RequireIndependentApproval] = 1 AND [RequireRevalidation] = 1");
            });
            entity.HasOne(item => item.WorkflowDefinition).WithMany().HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProcurementMasterDataControlPolicy>().WithMany().HasForeignKey(item => item.SupersedesPolicyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProcurementMasterDataChangeRequest>(entity =>
        {
            entity.Property(item => item.RequestNumber).IsUnicode(false);
            entity.Property(item => item.BeforeHash).IsUnicode(false).IsFixedLength();
            entity.Property(item => item.ProposedChangesHash).IsUnicode(false).IsFixedLength();
            entity.Property(item => item.AppliedAfterHash).IsUnicode(false).IsFixedLength();
            entity.Property(item => item.RevalidatedSnapshotHash).IsUnicode(false).IsFixedLength();
            entity.Property(item => item.CorrelationId).IsUnicode(false);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.RequestNumber }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.Status, item.EffectiveAtUtc });
            entity.HasIndex(item => new { item.TenantId, item.ResourceType, item.TargetId, item.CreatedAt });
            entity.HasIndex(item => new { item.TenantId, item.PolicyId });
            entity.HasIndex(item => new { item.TenantId, item.CorrelationId });
            entity.ToTable("ProcurementMasterDataChangeRequests", table =>
            {
                table.HasTrigger("TR_ProcurementMasterDataChangeRequests_SnapshotGuard");
                table.HasTrigger("TR_ProcurementMasterDataChangeRequests_NoDelete");
                table.HasCheckConstraint("CK_ProcurementMasterDataChangeRequests_ResourceType", "[ResourceType] BETWEEN 0 AND 8");
                table.HasCheckConstraint("CK_ProcurementMasterDataChangeRequests_TargetKind", "[TargetKind] BETWEEN 0 AND 7");
                table.HasCheckConstraint("CK_ProcurementMasterDataChangeRequests_Status", "[Status] BETWEEN 0 AND 6");
                table.HasCheckConstraint("CK_ProcurementMasterDataChangeRequests_Hashes", "LEN([BeforeHash]) = 64 AND LEN([ProposedChangesHash]) = 64 AND ([AppliedAfterHash] IS NULL OR LEN([AppliedAfterHash]) = 64) AND ([RevalidatedSnapshotHash] IS NULL OR LEN([RevalidatedSnapshotHash]) = 64)");
                table.HasCheckConstraint("CK_ProcurementMasterDataChangeRequests_ApprovalActors", "[CheckerUserId] IS NULL OR [CheckerUserId] <> [MakerUserId]");
            });
            entity.HasOne(item => item.Policy).WithMany().HasForeignKey(item => item.PolicyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.WorkflowDefinition).WithMany().HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.WorkflowInstance).WithMany().HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(item => item.EvidenceLinks).WithOne(item => item.ChangeRequest)
                .HasForeignKey(item => item.ChangeRequestId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProcurementMasterDataChangeEvidenceLink>(entity =>
        {
            entity.Property(item => item.Reference).IsUnicode(false);
            entity.HasIndex(item => new { item.TenantId, item.ChangeRequestId, item.ReferenceKind, item.Reference })
                .IsUnique().HasFilter("[IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.WorkflowEvidenceDocumentId });
            entity.HasIndex(item => new { item.TenantId, item.FileUploadRecordId });
            entity.ToTable("ProcurementMasterDataChangeEvidenceLinks", table =>
            {
                table.HasTrigger("TR_ProcurementMasterDataChangeEvidenceLinks_SubmittedGuard");
                table.HasCheckConstraint("CK_ProcurementMasterDataChangeEvidenceLinks_Kind", "[ReferenceKind] BETWEEN 0 AND 2");
                table.HasCheckConstraint("CK_ProcurementMasterDataChangeEvidenceLinks_ReferenceTarget",
                    "([ReferenceKind] = 0 AND [WorkflowEvidenceDocumentId] IS NOT NULL AND [FileUploadRecordId] IS NULL) OR " +
                    "([ReferenceKind] = 1 AND [WorkflowEvidenceDocumentId] IS NULL AND [FileUploadRecordId] IS NOT NULL) OR " +
                    "([ReferenceKind] = 2 AND [WorkflowEvidenceDocumentId] IS NULL AND [FileUploadRecordId] IS NULL)");
            });
            entity.HasOne(item => item.WorkflowEvidenceDocument).WithMany().HasForeignKey(item => item.WorkflowEvidenceDocumentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.FileUploadRecord).WithMany().HasForeignKey(item => item.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePolicyRule<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity)
        where TEntity : TenantEntity
    {
        entity.Property("RuleCode").IsUnicode(false);
        entity.Property("SourceDecisionKey").IsUnicode(false).IsFixedLength();
        entity.Property("RowVersion").IsRowVersion().IsConcurrencyToken();
        entity.HasIndex("TenantId", "PolicySetId", "RuleCode").IsUnique();
        entity.HasIndex("TenantId", "PolicySetId", "EffectiveFrom");
        entity.ToTable(table =>
        {
            table.HasCheckConstraint($"CK_{typeof(TEntity).Name}_EffectivePeriod", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
            table.HasCheckConstraint($"CK_{typeof(TEntity).Name}_SourceDecision", "[SourceDecisionKey] LIKE 'DEC-[0-9][0-9][0-9]'");
        });
        entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
