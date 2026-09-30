using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Identity;
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
using ErpSystem.Core.Entities.Reference;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Data.Configuration;
using ErpSystem.Data.Configuration.Finance;
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

public partial class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid,
    Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>, ApplicationUserRole,
    Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>,
    Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>,
    Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>
{
    private readonly Guid? _tenantId;

    // Referenced directly by global query-filter expressions so EF binds the
    // value per DbContext instance instead of caching the first tenant Guid in
    // the shared model.
    private Guid? CurrentTenantId => _tenantId;

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
    public DbSet<FinanceCloseCycle> FinanceCloseCycles { get; set; }
    public DbSet<FinanceCloseTemplate> FinanceCloseTemplates { get; set; }
    public DbSet<FinanceCloseTemplateTaskDefinition> FinanceCloseTemplateTaskDefinitions { get; set; }
    public DbSet<FinanceCloseTask> FinanceCloseTasks { get; set; }
    public DbSet<FinanceCloseCheckSnapshot> FinanceCloseCheckSnapshots { get; set; }
    public DbSet<FinanceCloseCertification> FinanceCloseCertifications { get; set; }
      public DbSet<FinanceCloseEvidenceAttachment> FinanceCloseEvidenceAttachments { get; set; }
      public DbSet<FinanceCloseExceptionWaiver> FinanceCloseExceptionWaivers { get; set; }
      public DbSet<FinanceCloseAlertDelivery> FinanceCloseAlertDeliveries { get; set; }
      public DbSet<FinancePeriodReopenRequest> FinancePeriodReopenRequests { get; set; }
    public DbSet<AccountSegmentStructure> AccountSegmentStructures { get; set; }
    public DbSet<SegmentLookupValue> SegmentLookupValues { get; set; }
    public DbSet<FinanceDimensionDefinition> FinanceDimensionDefinitions { get; set; }
    public DbSet<FinanceDimensionValue> FinanceDimensionValues { get; set; }
    public DbSet<FinanceDimensionSet> FinanceDimensionSets { get; set; }
    public DbSet<FinanceDimensionSetItem> FinanceDimensionSetItems { get; set; }
    public DbSet<FinanceDimensionAccountRule> FinanceDimensionAccountRules { get; set; }
    public DbSet<FinanceDimensionSnapshot> FinanceDimensionSnapshots { get; set; }
    public DbSet<FinanceDimensionSnapshotItem> FinanceDimensionSnapshotItems { get; set; }
    public DbSet<FinanceSourceDimensionAssignment> FinanceSourceDimensionAssignments { get; set; }
    public DbSet<FinanceSourceDimensionChange> FinanceSourceDimensionChanges { get; set; }
    public DbSet<FinanceSettlementDimensionComponent> FinanceSettlementDimensionComponents { get; set; }
    public DbSet<FinanceDimensionRouteCertification> FinanceDimensionRouteCertifications { get; set; }
    public DbSet<FinanceDimensionCertificationTransition> FinanceDimensionCertificationTransitions { get; set; }
    public DbSet<FinanceDimensionReadinessAssessment> FinanceDimensionReadinessAssessments { get; set; }
    public DbSet<Account> Accounts { get; set; }
    public DbSet<AccountingBook> AccountingBooks { get; set; }
    public DbSet<AccountingBookPrimaryDesignation> AccountingBookPrimaryDesignations { get; set; }
    public DbSet<FinanceSourceBookAuthority> FinanceSourceBookAuthorities { get; set; }
    public DbSet<FinanceSourceBookAuthorityOrigin> FinanceSourceBookAuthorityOrigins { get; set; }
    public DbSet<AccountingBookPeriod> AccountingBookPeriods { get; set; }
    public DbSet<YearEndBookCloseCycle> YearEndBookCloseCycles { get; set; }
    public DbSet<AccountingBookInitialization> AccountingBookInitializations { get; set; }
    public DbSet<AccountingBookInitializationLine> AccountingBookInitializationLines { get; set; }
    public DbSet<AccountingBookApplicabilityPolicy> AccountingBookApplicabilityPolicies { get; set; }
    public DbSet<AccountingBookApplicabilityRule> AccountingBookApplicabilityRules { get; set; }
    public DbSet<AccountingBookApplicabilityRuleBook> AccountingBookApplicabilityRuleBooks { get; set; }
    public DbSet<AccountingBookSelectionEvidence> AccountingBookSelectionEvidence { get; set; }
    public DbSet<AccountingBookSelectionEvidenceBook> AccountingBookSelectionEvidenceBooks { get; set; }
    public DbSet<AccountingEvent> AccountingEvents { get; set; }
    public DbSet<AccountingEventPosting> AccountingEventPostings { get; set; }
    public DbSet<AccountingEventAttempt> AccountingEventAttempts { get; set; }
    public DbSet<AccountingEventProducerReceipt> AccountingEventProducerReceipts { get; set; }
    public DbSet<ProducerIntentGroup> ProducerIntentGroups { get; set; }
    public DbSet<ProducerIntentGroupMember> ProducerIntentGroupMembers { get; set; }
    public DbSet<ProducerIntentGroupReceipt> ProducerIntentGroupReceipts { get; set; }
    public DbSet<ProducerIntentGroupAttempt> ProducerIntentGroupAttempts { get; set; }
    public DbSet<AccountAccountingBook> AccountAccountingBooks { get; set; }
    public DbSet<AccountBookCurrencyPolicy> AccountBookCurrencyPolicies { get; set; }
    public DbSet<AccountClassification> AccountClassifications { get; set; }
    public DbSet<FinancialStatementLayout> FinancialStatementLayouts { get; set; }
    public DbSet<FinancialStatementLayoutVersion> FinancialStatementLayoutVersions { get; set; }
    public DbSet<FinancialStatementRow> FinancialStatementRows { get; set; }
    public DbSet<FinancialStatementRowMapping> FinancialStatementRowMappings { get; set; }
    public DbSet<FinancialStatementPublicationAccount> FinancialStatementPublicationAccounts { get; set; }
    public DbSet<AccountSegmentValue> AccountSegmentValues { get; set; }
    public DbSet<FinanceSettings> FinanceSettings { get; set; }
    public DbSet<FinanceAccessScopeGrant> FinanceAccessScopeGrants { get; set; }
    public DbSet<DocumentSequenceDefinition> DocumentSequenceDefinitions { get; set; }
    public DbSet<DocumentNumberReservation> DocumentNumberReservations { get; set; }
    public DbSet<Tax> Taxes { get; set; }
    public DbSet<TaxConfigurationVersion> TaxConfigurationVersions { get; set; }
    public DbSet<TaxGroup> TaxGroups { get; set; }
    public DbSet<TaxGroupComponent> TaxGroupComponents { get; set; }
    public DbSet<WithholdingTaxCertificate> WithholdingTaxCertificates { get; set; }
    public DbSet<WithholdingTaxRemittance> WithholdingTaxRemittances { get; set; }
    public DbSet<WithholdingTaxRemittanceLine> WithholdingTaxRemittanceLines { get; set; }
    public DbSet<ModuleDefinition> ModuleDefinitions { get; set; }
    public DbSet<PeriodModuleLock> PeriodModuleLocks { get; set; }
    public DbSet<TransactionDocumentModuleMapping> TransactionDocumentModuleMappings { get; set; }
    public DbSet<FixedAssetCategory> FixedAssetCategories { get; set; }
    public DbSet<FixedAsset> FixedAssets { get; set; }
    public DbSet<FixedAssetBookValue> FixedAssetBookValues { get; set; }
    public DbSet<ProcurementFixedAssetCapitalization> ProcurementFixedAssetCapitalizations { get; set; }
    public DbSet<FixedAssetCapitalizationReversal> FixedAssetCapitalizationReversals { get; set; }
    public DbSet<FixedAssetDepreciationReversal> FixedAssetDepreciationReversals { get; set; }
    public DbSet<AssetDisposal> AssetDisposals { get; set; }
    public DbSet<AssetTransfer> AssetTransfers { get; set; }
    public DbSet<AssetTransaction> AssetTransactions { get; set; }
    public DbSet<AssetValuation> AssetValuations { get; set; }
    public DbSet<AssetValuationCorrection> AssetValuationCorrections { get; set; }
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
    public DbSet<JournalBatch> JournalBatches { get; set; }
    public DbSet<JournalBatchItem> JournalBatchItems { get; set; }
    public DbSet<JournalBatchItemReview> JournalBatchItemReviews { get; set; }
    public DbSet<JournalBatchPostingRun> JournalBatchPostingRuns { get; set; }
    public DbSet<JournalBatchPostingRunItem> JournalBatchPostingRunItems { get; set; }
    public DbSet<JournalBatchAttachment> JournalBatchAttachments { get; set; }
    public DbSet<JournalBatchImportSession> JournalBatchImportSessions { get; set; }
    public DbSet<RecurringJournalTemplate> RecurringJournalTemplates { get; set; }
    public DbSet<RecurringJournalTemplateLine> RecurringJournalTemplateLines { get; set; }
    public DbSet<RecurringJournalOccurrence> RecurringJournalOccurrences { get; set; }
    public DbSet<FinancePostingEvent> FinancePostingEvents { get; set; }
    public DbSet<AccountTransaction> AccountTransactions { get; set; }
    public DbSet<AccountBalance> AccountBalances { get; set; }
    public DbSet<AccountCurrencyExposure> AccountCurrencyExposures { get; set; }
    public DbSet<FinanceBalanceRebuildRun> FinanceBalanceRebuildRuns { get; set; }
    public DbSet<FxRealizedSettlement> FxRealizedSettlements { get; set; }
    public DbSet<FxRevaluationBatch> FxRevaluationBatches { get; set; }
    public DbSet<FxRevaluationLine> FxRevaluationLines { get; set; }
    public DbSet<FxRevaluationRateUsage> FxRevaluationRateUsages { get; set; }
    public DbSet<SubledgerSettlementBalance> SubledgerSettlementBalances { get; set; }
    public DbSet<SubledgerSettlementApplication> SubledgerSettlementApplications { get; set; }
    public DbSet<SubledgerUnappliedSettlementBalance> SubledgerUnappliedSettlementBalances { get; set; }
    public DbSet<OpeningBalanceBatch> OpeningBalanceBatches { get; set; }
    public DbSet<OpeningBalanceBatchReversal> OpeningBalanceBatchReversals { get; set; }
    public DbSet<OpeningBalanceLine> OpeningBalanceLines { get; set; }
    public DbSet<AccountCurrencyLink> AccountCurrencyLinks { get; set; }
    public DbSet<BudgetScenario> BudgetScenarios { get; set; }
    public DbSet<BudgetScenarioControlDimension> BudgetScenarioControlDimensions { get; set; }
    public DbSet<BudgetEntry> BudgetEntries { get; set; }
    public DbSet<BudgetReturn> BudgetReturns { get; set; }
    public DbSet<BudgetRevision> BudgetRevisions { get; set; }
    public DbSet<BudgetRevisionLine> BudgetRevisionLines { get; set; }
    public DbSet<FinanceBudgetReservation> FinanceBudgetReservations { get; set; }
    public DbSet<FinanceBudgetReservationOperation> FinanceBudgetReservationOperations { get; set; }
    public DbSet<FinanceBudgetOverrideRequest> FinanceBudgetOverrideRequests { get; set; }
    public DbSet<BankAccount> BankAccounts { get; set; }
    public DbSet<LiquidityAccount> LiquidityAccounts { get; set; }
    public DbSet<LiquidityAccountEntry> LiquidityAccountEntries { get; set; }
    public DbSet<CashierTillSession> CashierTillSessions { get; set; }
    public DbSet<CashierTillCountLine> CashierTillCountLines { get; set; }
    public DbSet<FinanceControlledDocumentIssue> FinanceControlledDocumentIssues { get; set; }
    public DbSet<BankDepositBatch> BankDepositBatches { get; set; }
    public DbSet<BankDepositAllocation> BankDepositAllocations { get; set; }
    public DbSet<BankDepositAttachment> BankDepositAttachments { get; set; }
    public DbSet<ReturnedChequeCase> ReturnedChequeCases { get; set; }
    public DbSet<ReturnedChequeAttachment> ReturnedChequeAttachments { get; set; }
    public DbSet<FinancePaymentMethod> PaymentMethods { get; set; }
    public DbSet<TaxRule> TaxRules { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<VendorInvoice> VendorInvoices { get; set; }
    public DbSet<VendorInvoiceReceiptAllocation> VendorInvoiceReceiptAllocations { get; set; }
    public DbSet<VendorInvoiceMatchException> VendorInvoiceMatchExceptions { get; set; }
    public DbSet<VendorInvoiceMatchExceptionVariance> VendorInvoiceMatchExceptionVariances { get; set; }
    public DbSet<VendorInvoiceMatchExceptionEvidence> VendorInvoiceMatchExceptionEvidence { get; set; }
    public DbSet<VendorInvoiceMatchExceptionAction> VendorInvoiceMatchExceptionActions { get; set; }
    public DbSet<SubledgerAdjustmentJournal> SubledgerAdjustmentJournals { get; set; }
    public DbSet<SupplierReturn> SupplierReturns { get; set; }
    public DbSet<SupplierReturnLineItem> SupplierReturnLineItems { get; set; }
    public DbSet<SupplierDebitNote> SupplierDebitNotes { get; set; }
    public DbSet<SupplierDebitNoteLineItem> SupplierDebitNoteLineItems { get; set; }
    public DbSet<SupplierDebitNoteTaxComponent> SupplierDebitNoteTaxComponents { get; set; }
    public DbSet<SupplierDebitNoteApplication> SupplierDebitNoteApplications { get; set; }
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
    public DbSet<AllocationRunBatch> AllocationRunBatches { get; set; }
    public DbSet<AllocationRunBatchLine> AllocationRunBatchLines { get; set; }

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
    public DbSet<FinanceAdHocReportDefinition> FinanceAdHocReportDefinitions { get; set; }

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
    public DbSet<AuditRecordLifecycleEvent> AuditRecordLifecycleEvents { get; set; }

    #region Shared Reference Data

    // Administrative geography — Region / State / Municipal / District / Town / Ward, as one
    // configurable tree per country. Deliberately sits ABOVE the HR region: HR is the first
    // consumer, not the owner. Estate, Sales, Procurement and Inventory all carry the same
    // free-text region and district columns and are expected to move onto this.
    // See docs/GEOGRAPHY-REFERENCE-DESIGN.md.
    //
    // ⚠ Not to be confused with Location/LocationLevel below. A Location is one of OUR sites and
    // is the target of ~30 foreign keys; a GeoArea is a place on the map, true whether or not the
    // company operates there.

    /// <summary>One country's way of dividing itself up (Ghana: Region → District → Town).</summary>
    public DbSet<GeoScheme> GeoSchemes { get; set; } = null!;

    /// <summary>A tier of a scheme. Its Name is the label every address form prints.</summary>
    public DbSet<GeoLevel> GeoLevels { get; set; } = null!;

    /// <summary>An actual administrative area, effective-dated so boundary changes keep history.</summary>
    public DbSet<GeoArea> GeoAreas { get; set; } = null!;

    /// <summary>Alternate names an area answers to, so old spreadsheets still resolve.</summary>
    public DbSet<GeoAreaAlias> GeoAreaAliases { get; set; } = null!;

    #endregion Shared Reference Data

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

    // Named sets — round 2, lane C3 (plan § 6.4). Three masters, three member tables, three
    // position attachments; one shape each.
    public DbSet<BenefitGroup> BenefitGroups { get; set; }
    public DbSet<BenefitGroupMember> BenefitGroupMembers { get; set; }
    public DbSet<SkillSet> SkillSets { get; set; }
    public DbSet<SkillSetMember> SkillSetMembers { get; set; }
    public DbSet<CertificationSet> CertificationSets { get; set; }
    public DbSet<CertificationSetMember> CertificationSetMembers { get; set; }
    public DbSet<EmployeePositionBenefitGroup> EmployeePositionBenefitGroups { get; set; }
    public DbSet<PositionSkillSet> PositionSkillSets { get; set; }
    public DbSet<PositionCertificationSet> PositionCertificationSets { get; set; }
    public DbSet<StaffLevel> StaffLevels { get; set; }
    public DbSet<WorkStation> WorkStations { get; set; }
    public DbSet<EmployeeContractType> EmployeeContractTypes { get; set; }

    /// <summary>How one person is tied to another — the catalogue four free-text columns
    /// used to spell out separately (round 2, lane D2).</summary>
    public DbSet<RelationshipType> RelationshipTypes { get; set; }
    /// <summary>Round 3, lane P2: the disability catalogue the employee and dependant forms pick from.</summary>
    public DbSet<DisabilityType> DisabilityTypes { get; set; }

    // ── Teams and committees: the activity sub-module (round 2, lane F1) ────────────────────
    //
    // ⚠ THESE DbSets ARE LOAD-BEARING, not decoration. An entity that EF first discovers inside
    // `ConfigureHrModule` is discovered AFTER `ConfigureGlobalTenantRelationships` has run, so its
    // Tenant foreign key is minted by convention afterwards and keeps the convention's CASCADE.
    // For a table that also cascades from a parent — a checklist item from its task — that is two
    // cascade paths to Tenants, and SQL Server refuses the constraint outright:
    //
    //     Introducing FOREIGN KEY constraint 'FK_TeamTaskChecklistItems_Tenants_TenantId' ...
    //     may cause cycles or multiple cascade paths.
    //
    // A DbSet makes EF discover the entity before OnModelCreating's body runs, so the global pass
    // catches it and sets Restrict. The same omission also made the first scaffold name the tables
    // in the SINGULAR, after the entity rather than the set. Both symptoms, one cause.
    public DbSet<TeamTermsOfReference> TeamTermsOfReferences { get; set; }
    public DbSet<TeamObjective> TeamObjectives { get; set; }
    public DbSet<TeamTask> TeamTasks { get; set; }
    public DbSet<TeamTaskChecklistItem> TeamTaskChecklistItems { get; set; }
    public DbSet<TeamTaskAttachment> TeamTaskAttachments { get; set; }

    // Slice F2 — the minute book, the reviews, and the sweep's own record. Same reason as above:
    // without a DbSet these are discovered after ConfigureGlobalTenantRelationships and their
    // tenant FK keeps CASCADE, which SQL Server rejects wherever a parent already cascades.
    public DbSet<TeamMeeting> TeamMeetings { get; set; }
    public DbSet<TeamMeetingAttendee> TeamMeetingAttendees { get; set; }
    public DbSet<TeamMeetingDecision> TeamMeetingDecisions { get; set; }
    public DbSet<TeamReview> TeamReviews { get; set; }
    public DbSet<TeamReviewLine> TeamReviewLines { get; set; }
    public DbSet<TeamReminderRun> TeamReminderRuns { get; set; }
    public DbSet<TeamReminderDispatchLog> TeamReminderDispatchLogs { get; set; }

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
    public DbSet<ExpatriateFamilyMember> ExpatriateFamilyMembers { get; set; }
    public DbSet<EmployeePositionHistory> EmployeePositionHistories { get; set; }
    public DbSet<EmployeeSalaryAssignment> EmployeeSalaryAssignments { get; set; }
    public DbSet<EmployeeReferee> EmployeeReferees { get; set; }
    public DbSet<EmployeeGuarantor> EmployeeGuarantors { get; set; }
    public DbSet<EmployeeSkill> EmployeeSkills { get; set; }
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

    #region HR - Miscellaneous

    public DbSet<Country> Countries { get; set; }
    public DbSet<Qualification> Qualifications { get; set; }

    /// <summary>The ordered academic / professional ladder a Qualification can sit on.</summary>
    public DbSet<QualificationLevel> QualificationLevels { get; set; }

    /// <summary>Catalogued bodies that certify a skill, replacing free-text certifier names.</summary>
    public DbSet<CertifyingBody> CertifyingBodies { get; set; }

    // Demo feedback round 2, lane C2 — the certification model.
    public DbSet<Certification> Certifications { get; set; }
    public DbSet<SkillCertification> SkillCertifications { get; set; }
    public DbSet<PositionCertificationRequirement> PositionCertificationRequirements { get; set; }
    public DbSet<EmployeeCertification> EmployeeCertifications { get; set; }
    public DbSet<CertificationExpiryReminderRun> CertificationExpiryReminderRuns { get; set; }
    public DbSet<CertificationExpiryDispatchLog> CertificationExpiryDispatchLogs { get; set; }

    /// <summary>Per-register staff-number formats. One row per employee register per tenant.</summary>
    public DbSet<StaffNumberFormat> StaffNumberFormats { get; set; }
    public DbSet<ExternalAssociate> ExternalAssociates { get; set; }

    #endregion

    // Leave Management
    public DbSet<LeaveType> LeaveTypes { get; set; }
    public DbSet<LeaveSubType> LeaveSubTypes { get; set; }
    public DbSet<LeaveCategoryAllocation> LeaveCategoryAllocations { get; set; }
    public DbSet<LeaveBalance> LeaveBalances { get; set; }
    public DbSet<LeavePlan> LeavePlans { get; set; }
    public DbSet<LeaveRequest> LeaveRequests { get; set; }

    // Performance Management
    public DbSet<AppraisalGradeDefinition> AppraisalGradeDefinitions { get; set; }
    public DbSet<KpiDefinition> KpiDefinitions { get; set; }
    public DbSet<PerformanceAppraisal> PerformanceAppraisals { get; set; }
    public DbSet<EvaluatorEvaluation> EvaluatorEvaluations { get; set; }
    public DbSet<CriterionScore> CriterionScores { get; set; }
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
    public DbSet<StockAdjustmentEvidence> StockAdjustmentEvidence { get; set; }
    public DbSet<StockAdjustmentAction> StockAdjustmentActions { get; set; }
    public DbSet<Warehouse> Warehouses { get; set; }
    public DbSet<WarehouseLocation> WarehouseLocations { get; set; }
    public DbSet<InventoryLocation> InventoryLocations { get; set; }
    public DbSet<WarehouseQuantity> WarehouseQuantities { get; set; }
    public DbSet<InventoryAllocation> InventoryAllocations { get; set; }
    public DbSet<InventoryWorkOrderReservationAction> InventoryWorkOrderReservationActions { get; set; }

    // Enhanced Inventory entities
    public DbSet<UnitOfMeasure> UnitsOfMeasure { get; set; }
    public DbSet<UnitOfMeasureConversion> UnitOfMeasureConversions { get; set; }
    public DbSet<ItemUnitOfMeasure> ItemUnitsOfMeasure { get; set; }
    public DbSet<ItemSupplier> ItemSuppliers { get; set; }
    public DbSet<GoodsReceiptNote> GoodsReceiptNotes { get; set; }
    public DbSet<GoodsReceiptNoteItem> GoodsReceiptNoteItems { get; set; }
    public DbSet<InventoryTransfer> InventoryTransfers { get; set; }
    public DbSet<InventoryTransferItem> InventoryTransferItems { get; set; }
    public DbSet<InventoryTransferAction> InventoryTransferActions { get; set; }
    public DbSet<InventoryTransferActionLine> InventoryTransferActionLines { get; set; }
    public DbSet<InventoryTransferDiscrepancy> InventoryTransferDiscrepancies { get; set; }
    public DbSet<InventoryTransferDiscrepancyEvidence> InventoryTransferDiscrepancyEvidence { get; set; }
    public DbSet<PhysicalCount> PhysicalCounts { get; set; }
    public DbSet<PhysicalCountItem> PhysicalCountItems { get; set; }
    public DbSet<InventoryCycleCountSchedule> InventoryCycleCountSchedules { get; set; }
    public DbSet<PhysicalCountAction> PhysicalCountActions { get; set; }
    public DbSet<InventoryCostLayer> InventoryCostLayers { get; set; }
    public DbSet<LandedCost> LandedCosts { get; set; }
    // Discover before the global decimal and tenant-FK passes run.
    public DbSet<LandedCostReceiptWeight> LandedCostReceiptWeights { get; set; }
    public DbSet<LandedCostSupplierDocument> LandedCostSupplierDocuments { get; set; }
    public DbSet<LandedCostItem> LandedCostItems { get; set; }
    public DbSet<LandedCostAllocation> LandedCostAllocations { get; set; }
    public DbSet<PurchaseReturn> PurchaseReturns { get; set; }
    public DbSet<PurchaseReturnItem> PurchaseReturnItems { get; set; }
    public DbSet<InventoryRequisition> InventoryRequisitions { get; set; }
    public DbSet<InventoryRequisitionItem> InventoryRequisitionItems { get; set; }
    public DbSet<InventoryLabelProfile> InventoryLabelProfiles { get; set; }
    public DbSet<InventoryLabelPrintEvent> InventoryLabelPrintEvents { get; set; }
    public DbSet<InventoryScanBatch> InventoryScanBatches { get; set; }
    public DbSet<InventoryScanLine> InventoryScanLines { get; set; }
    public DbSet<InventoryTrackingException> InventoryTrackingExceptions { get; set; }
    public DbSet<InventoryTraceabilityEvent> InventoryTraceabilityEvents { get; set; }
    public DbSet<InventoryNegativeStockOverride> InventoryNegativeStockOverrides { get; set; }
    public DbSet<InventoryProjectReservationAction> InventoryProjectReservationActions { get; set; }
    public DbSet<InventoryReplenishmentRecommendation> InventoryReplenishmentRecommendations { get; set; }
    public DbSet<InventoryReplenishmentAction> InventoryReplenishmentActions { get; set; }
    public DbSet<InventoryValuationReconciliation> InventoryValuationReconciliations { get; set; }
    public DbSet<InventoryValuationReconciliationAction> InventoryValuationReconciliationActions { get; set; }
    public DbSet<InventoryDisposalCase> InventoryDisposalCases { get; set; }
    public DbSet<InventoryDisposalLine> InventoryDisposalLines { get; set; }
    public DbSet<InventoryDisposalEvidence> InventoryDisposalEvidence { get; set; }
    public DbSet<InventoryDisposalCommitteeMember> InventoryDisposalCommitteeMembers { get; set; }
    public DbSet<InventoryDisposalAction> InventoryDisposalActions { get; set; }
    public DbSet<InventoryDirectedTask> InventoryDirectedTasks { get; set; }
    public DbSet<InventoryDirectedTaskAction> InventoryDirectedTaskActions { get; set; }
    public DbSet<InventoryIssueVoucher> InventoryIssueVouchers { get; set; }
    public DbSet<InventoryIssueVoucherLine> InventoryIssueVoucherLines { get; set; }
    public DbSet<InventoryIssueVoucherAction> InventoryIssueVoucherActions { get; set; }
    public DbSet<InventoryIssueAccountingRule> InventoryIssueAccountingRules { get; set; }
    public DbSet<InventoryIssueFinanceLineage> InventoryIssueFinanceLineages { get; set; }
    public DbSet<InventoryIssueReturnAllocation> InventoryIssueReturnAllocations { get; set; }
    public DbSet<InventoryReturnVoucher> InventoryReturnVouchers { get; set; }
    public DbSet<InventoryReturnVoucherLine> InventoryReturnVoucherLines { get; set; }
    public DbSet<InventoryReturnVoucherEvidence> InventoryReturnVoucherEvidence { get; set; }
    public DbSet<InventoryReturnVoucherAction> InventoryReturnVoucherActions { get; set; }

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
    public DbSet<ProcurementReceiptSourceEvidence> ProcurementReceiptSourceEvidence { get; set; }
    public DbSet<PurchaseOrderLandedCostPlan> PurchaseOrderLandedCostPlans { get; set; }
    public DbSet<PurchaseOrderLandedCostPlanItem> PurchaseOrderLandedCostPlanItems { get; set; }
    public DbSet<ProcurementPurchaseOrderAmendment> ProcurementPurchaseOrderAmendments { get; set; }
    public DbSet<ProcurementPurchaseOrderCommitmentAdjustment> ProcurementPurchaseOrderCommitmentAdjustments { get; set; }
    public DbSet<ProcurementPurchaseOrderAmendmentDispatch> ProcurementPurchaseOrderAmendmentDispatches { get; set; }
    public DbSet<ProcurementPurchaseOrderAmendmentAcknowledgement> ProcurementPurchaseOrderAmendmentAcknowledgements { get; set; }
    public DbSet<PurchaseRequisition> PurchaseRequisitions { get; set; }
    public DbSet<ConsignmentSettlement> ConsignmentSettlements { get; set; }

    // Business Partner Management (Unified Supplier/Contractor)
    public DbSet<BusinessPartner> BusinessPartners { get; set; }
    public DbSet<BusinessPartnerRole> BusinessPartnerRoles { get; set; }
    public DbSet<BusinessPartnerApProfileVersion> BusinessPartnerApProfileVersions { get; set; }
    public DbSet<BusinessPartnerApWhtDefault> BusinessPartnerApWhtDefaults { get; set; }
    public DbSet<BusinessPartnerArProfileVersion> BusinessPartnerArProfileVersions { get; set; }
    public DbSet<PartnerCategory> PartnerCategories { get; set; }
    public DbSet<BusinessPartnerCategory> BusinessPartnerCategories { get; set; }
    public DbSet<ContractorSpecialization> ContractorSpecializations { get; set; }
    public DbSet<BusinessPartnerSpecialization> BusinessPartnerSpecializations { get; set; }
    public DbSet<LicenseType> LicenseTypes { get; set; }
    public DbSet<BusinessPartnerLicense> BusinessPartnerLicenses { get; set; }
    public DbSet<BusinessPartnerContact> BusinessPartnerContacts { get; set; }
    public DbSet<BusinessPartnerBankAccount> BusinessPartnerBankAccounts { get; set; }
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
    public DbSet<ProcurementTenderDocumentTemplateVersion> ProcurementTenderDocumentTemplateVersions { get; set; }
    public DbSet<ProcurementSupplierEvidencePackVersion> ProcurementSupplierEvidencePackVersions { get; set; }
    public DbSet<ProcurementSupplierEvidenceRequirement> ProcurementSupplierEvidenceRequirements { get; set; }
    public DbSet<ProcurementSupplierRegistrationEvidencePackBinding> ProcurementSupplierRegistrationEvidencePackBindings { get; set; }
    public DbSet<ProcurementSupplierOnboardingToken> ProcurementSupplierOnboardingTokens { get; set; }
    public DbSet<ProcurementSupplierOnboardingPayment> ProcurementSupplierOnboardingPayments { get; set; }
    public DbSet<ProcurementSupplierOnboardingExemption> ProcurementSupplierOnboardingExemptions { get; set; }
    public DbSet<ProcurementSupplierApplicantAccess> ProcurementSupplierApplicantAccesses { get; set; }
    public DbSet<ProcurementSupplierApplicantSession> ProcurementSupplierApplicantSessions { get; set; }
    public DbSet<ProcurementSupplierDueDiligenceReview> ProcurementSupplierDueDiligenceReviews { get; set; }
    public DbSet<ProcurementSupplierDueDiligenceCheck> ProcurementSupplierDueDiligenceChecks { get; set; }
    public DbSet<ProcurementSupplierDueDiligenceEvidenceLink> ProcurementSupplierDueDiligenceEvidenceLinks { get; set; }
    public DbSet<ProcurementFrameworkAgreement> ProcurementFrameworkAgreements { get; set; }
    public DbSet<ProcurementFrameworkAgreementCategory> ProcurementFrameworkAgreementCategories { get; set; }
    public DbSet<ProcurementFrameworkPriceListLine> ProcurementFrameworkPriceListLines { get; set; }
    public DbSet<ProcurementFrameworkCallOffAuthority> ProcurementFrameworkCallOffAuthorities { get; set; }
    public DbSet<ProcurementFrameworkAgreementDocument> ProcurementFrameworkAgreementDocuments { get; set; }
    public DbSet<ProcurementFrameworkAgreementExtension> ProcurementFrameworkAgreementExtensions { get; set; }
    public DbSet<ProcurementFrameworkCallOff> ProcurementFrameworkCallOffs { get; set; }
    public DbSet<ProcurementFrameworkCallOffLine> ProcurementFrameworkCallOffLines { get; set; }
    public DbSet<ProcurementFrameworkAgreementBalance> ProcurementFrameworkAgreementBalances { get; set; }
    public DbSet<ProcurementFrameworkBalanceMovement> ProcurementFrameworkBalanceMovements { get; set; }
    public DbSet<ProcurementSupplierAvlRegister> ProcurementSupplierAvlRegisters { get; set; }
    public DbSet<ProcurementSupplierAvlEntry> ProcurementSupplierAvlEntries { get; set; }
    public DbSet<ProcurementSupplierAvlEntryStatusHistory> ProcurementSupplierAvlEntryStatusHistories { get; set; }
    public DbSet<ProcurementSupplierAvlPublicationSnapshot> ProcurementSupplierAvlPublicationSnapshots { get; set; }
    public DbSet<ProcurementSupplierRiskAssessment> ProcurementSupplierRiskAssessments { get; set; }
    public DbSet<ProcurementSupplierRiskAlert> ProcurementSupplierRiskAlerts { get; set; }
    public DbSet<ProcurementSupplierPerformanceScorecard> ProcurementSupplierPerformanceScorecards { get; set; }
    public DbSet<ProcurementTenderDocumentTemplateMethod> ProcurementTenderDocumentTemplateMethods { get; set; }
    public DbSet<ProcurementTenderDocumentRegister> ProcurementTenderDocumentRegisters { get; set; }
    public DbSet<ProcurementTenderDocumentIssuance> ProcurementTenderDocumentIssuances { get; set; }
    public DbSet<ProcurementTenderDocumentChange> ProcurementTenderDocumentChanges { get; set; }
    public DbSet<ProcurementTenderDocumentChangeRecipient> ProcurementTenderDocumentChangeRecipients { get; set; }
    public DbSet<ProcurementTenderDocumentAcknowledgement> ProcurementTenderDocumentAcknowledgements { get; set; }
    public DbSet<ProcurementEvaluationCommitteeControl> ProcurementEvaluationCommitteeControls { get; set; }
    public DbSet<ProcurementEvaluationCommitteeRoleRequirement> ProcurementEvaluationCommitteeRoleRequirements { get; set; }
    public DbSet<ProcurementEvaluationCommitteeAppointment> ProcurementEvaluationCommitteeAppointments { get; set; }
    public DbSet<ProcurementEvaluationConflictDeclaration> ProcurementEvaluationConflictDeclarations { get; set; }
    public DbSet<ProcurementEvaluationMeeting> ProcurementEvaluationMeetings { get; set; }
    public DbSet<ProcurementEvaluationAttendanceRecord> ProcurementEvaluationAttendanceRecords { get; set; }
    public DbSet<ProcurementEvaluationScoreSheet> ProcurementEvaluationScoreSheets { get; set; }
    public DbSet<ProcurementEvaluationScoreRecall> ProcurementEvaluationScoreRecalls { get; set; }
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
    public DbSet<ProcurementAwardReadinessDecision> ProcurementAwardReadinessDecisions { get; set; }
    public DbSet<ProcurementBidderCommunicationRegister> ProcurementBidderCommunicationRegisters { get; set; }
    public DbSet<ProcurementBidderCommunicationRecipient> ProcurementBidderCommunicationRecipients { get; set; }
    public DbSet<ProcurementBidderCommunicationLetterVersion> ProcurementBidderCommunicationLetterVersions { get; set; }
    public DbSet<ProcurementBidderCommunicationDispatch> ProcurementBidderCommunicationDispatches { get; set; }
    public DbSet<ProcurementBidderCommunicationDelivery> ProcurementBidderCommunicationDeliveries { get; set; }
    public DbSet<ProcurementBidderCommunicationAcknowledgement> ProcurementBidderCommunicationAcknowledgements { get; set; }
    public DbSet<ProcurementBidderAppeal> ProcurementBidderAppeals { get; set; }
    public DbSet<ProcurementBidderAppealDecision> ProcurementBidderAppealDecisions { get; set; }
    public DbSet<ProcurementTenderSecurityInstrument> ProcurementTenderSecurityInstruments { get; set; }
    public DbSet<ProcurementTenderSecurityAction> ProcurementTenderSecurityActions { get; set; }
    public DbSet<ProcurementGhanepsExchangeEvent> ProcurementGhanepsExchangeEvents { get; set; }
    public DbSet<ProcurementGhanepsExchangePayload> ProcurementGhanepsExchangePayloads { get; set; }
    public DbSet<ProcurementGhanepsExchangeAttempt> ProcurementGhanepsExchangeAttempts { get; set; }
    public DbSet<ProcurementGhanepsExchangeAcknowledgement> ProcurementGhanepsExchangeAcknowledgements { get; set; }
    public DbSet<ProcurementGhanepsExchangeReconciliation> ProcurementGhanepsExchangeReconciliations { get; set; }

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
    public DbSet<ProcurementContractActivation> ProcurementContractActivations { get; set; }
    public DbSet<ProcurementContractActivationEvidence> ProcurementContractActivationEvidence { get; set; }
    public DbSet<ProcurementWorksCloseoutAction> ProcurementWorksCloseoutActions { get; set; }
    public DbSet<ProcurementWorksCloseoutEvidence> ProcurementWorksCloseoutEvidence { get; set; }
    public DbSet<ProcurementReceiptInspectionCase> ProcurementReceiptInspectionCases { get; set; }
    public DbSet<ProcurementReceiptInspectionLine> ProcurementReceiptInspectionLines { get; set; }
    public DbSet<ProcurementReceiptInspectionEvidence> ProcurementReceiptInspectionEvidence { get; set; }
    public DbSet<ProcurementReceiptInspectionAction> ProcurementReceiptInspectionActions { get; set; }
    public DbSet<ProcurementReceiptDocument> ProcurementReceiptDocuments { get; set; }
    public DbSet<ProcurementReceiptDocumentSignature> ProcurementReceiptDocumentSignatures { get; set; }
    public DbSet<ProcurementReceiptDocumentAction> ProcurementReceiptDocumentActions { get; set; }

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
    public DbSet<ProcurementBudgetCommitmentLedgerEntry> ProcurementBudgetCommitmentLedgerEntries { get; set; }
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
    public DbSet<ProcurementResponsibilityLocation> ProcurementResponsibilityLocations { get; set; }
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
    public DbSet<EhcCrmEngagementLink> EhcCrmEngagementLinks { get; set; }
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
    public DbSet<EstateLandDemarcation> EstateLandDemarcations { get; set; }
    public DbSet<EstateManagedAssetDocument> EstateManagedAssetDocuments { get; set; }
    public DbSet<EstateGroundRentAccount> EstateGroundRentAccounts { get; set; }
    public DbSet<EstateGroundRentCharge> EstateGroundRentCharges { get; set; }
    public DbSet<EstateGroundRentReview> EstateGroundRentReviews { get; set; }
    public DbSet<EstateFacilityDutyRoster> EstateFacilityDutyRosters { get; set; }
    public DbSet<EstateFacilityDutyAttendance> EstateFacilityDutyAttendances { get; set; }
    public DbSet<EstateFacilityProviderRate> EstateFacilityProviderRates { get; set; }
    public DbSet<EstateFacilityProviderAssignment> EstateFacilityProviderAssignments { get; set; }
    public DbSet<EstateGisConfiguration> EstateGisConfigurations { get; set; }
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

    // Shared HR / Identity reconciliation
    public DbSet<HrIdentityReconciliationState> HrIdentityReconciliationStates { get; set; }
    public DbSet<HrIdentityReconciliationRun> HrIdentityReconciliationRuns { get; set; }
    public DbSet<HrIdentityReconciliationItem> HrIdentityReconciliationItems { get; set; }
    public DbSet<HrIdentityWorkflowIssue> HrIdentityWorkflowIssues { get; set; }

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
    public DbSet<ProjectFinalAccountRevision> ProjectFinalAccountRevisions { get; set; }
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
        ErpSystem.Data.Configurations.InventorySupplierReturnFinanceConfiguration.Configure(builder);
        builder.ApplyConfiguration(new ErpSystem.Data.Configurations.InventorySupplierReturnAllocationConfiguration());
        builder.ApplyConfiguration(new ErpSystem.Data.Configurations.InventorySupplierReturnAccountingGroupConfiguration());
        builder.ApplyConfiguration(new ErpSystem.Data.Configurations.InventorySupplierReturnAccrualShareConfiguration());
        ErpSystem.Data.Configurations.BusinessPartnerPostingDefaultsConfiguration.Configure(builder);
        ErpSystem.Data.Configurations.BusinessPartnerFinanceProfileConfiguration.Configure(builder);

        // Apply entity configurations
        builder.ApplyConfiguration(new ApplicationUserConfiguration());
        builder.ApplyConfiguration(new TenantConfiguration());
        builder.ApplyConfiguration(new UserTenantConfiguration());
        builder.ApplyConfiguration(new JournalBatchConfiguration());
        builder.ApplyConfiguration(new JournalBatchEntryApprovalConfiguration());
        builder.ApplyConfiguration(new JournalBatchItemConfiguration());
        builder.ApplyConfiguration(new JournalBatchItemReviewConfiguration());
        builder.ApplyConfiguration(new JournalBatchPostingRunConfiguration());
        builder.ApplyConfiguration(new JournalBatchPostingRunItemConfiguration());
        builder.ApplyConfiguration(new JournalBatchAttachmentConfiguration());
        builder.ApplyConfiguration(new JournalBatchImportSessionConfiguration());
        builder.ApplyConfiguration(new FixedAssetCategoryConfiguration());
        builder.ApplyConfiguration(new VendorInvoiceOptionalApprovalConfiguration());
        builder.ApplyConfiguration<FinancePurchaseOrder>(new FinancePurchasingOptionalApprovalConfiguration());
        builder.ApplyConfiguration<FinancePurchaseOrderReceipt>(new FinancePurchasingOptionalApprovalConfiguration());
        builder.ApplyConfiguration(new JournalEntryOptionalApprovalConfiguration());
        builder.ApplyConfiguration<PhysicalCount>(new InventoryOptionalApprovalConfiguration());
        builder.ApplyConfiguration<StockAdjustment>(new InventoryOptionalApprovalConfiguration());
        builder.ApplyConfiguration<InventoryReturnVoucher>(new InventoryOptionalApprovalConfiguration());
        ConfigureBudgeting(builder);
        ConfigureBankingSettlement(builder);
        builder.ApplyConfiguration(new InventoryLabelProfileConfiguration());
        builder.ApplyConfiguration(new InventoryLabelPrintEventConfiguration());
        builder.ApplyConfiguration(new InventoryScanBatchConfiguration());
        builder.ApplyConfiguration(new InventoryScanLineConfiguration());
        builder.ApplyConfiguration(new InventoryTrackingExceptionConfiguration());
        builder.ApplyConfiguration(new InventoryTraceabilityEventConfiguration());
        builder.ApplyConfiguration(new InventoryNegativeStockOverrideConfiguration());
        builder.ApplyConfiguration(new InventoryNegativeStockWarehouseQuantityConfiguration());
        builder.ApplyConfiguration(new InventoryNegativeStockItemConfiguration());
        builder.ApplyConfiguration(new InventoryLocationNegativeStockTriggerConfiguration());
        builder.ApplyConfiguration(new InventoryWorkOrderAllocationConfiguration());
        builder.ApplyConfiguration(new InventoryWorkOrderReservationActionConfiguration());
        builder.ApplyConfiguration(new InventoryProjectReservationConfiguration());
        builder.ApplyConfiguration(new InventoryProjectReservationActionConfiguration());
        builder.ApplyConfiguration(new InventoryReplenishmentRecommendationConfiguration());
        builder.ApplyConfiguration(new InventoryReplenishmentActionConfiguration());
        builder.ApplyConfiguration(new InventoryValuationReconciliationConfiguration());
        builder.ApplyConfiguration(new InventoryValuationReconciliationActionConfiguration());
        builder.ApplyConfiguration(new InventoryDisposalCaseConfiguration());
        builder.ApplyConfiguration(new InventoryDisposalAuctionInvoiceConfiguration());
        builder.ApplyConfiguration(new InventoryDisposalLineConfiguration());
        builder.ApplyConfiguration(new InventoryDisposalEvidenceConfiguration());
        builder.ApplyConfiguration(new InventoryDisposalCommitteeMemberConfiguration());
        builder.ApplyConfiguration(new InventoryDisposalActionConfiguration());
        builder.ApplyConfiguration(new InventoryDirectedTaskConfiguration());
        builder.ApplyConfiguration(new InventoryDirectedTaskActionConfiguration());
        builder.ApplyConfiguration(new InventoryIssueVoucherConfiguration());
        builder.ApplyConfiguration(new InventoryIssueVoucherLineConfiguration());
        builder.ApplyConfiguration(new InventoryIssueVoucherActionConfiguration());
        builder.ApplyConfiguration(new InventoryIssueVoucherReceiptLineConfiguration());
        builder.ApplyConfiguration(new InventoryIssueAccountingRuleConfiguration());
        builder.ApplyConfiguration(new InventoryIssueFinanceLineageConfiguration());
        builder.ApplyConfiguration(new InventoryIssueReturnAllocationConfiguration());
        builder.ApplyConfiguration(new InventoryReturnVoucherConfiguration());
        builder.ApplyConfiguration(new InventoryReturnVoucherLineConfiguration());
        builder.ApplyConfiguration(new InventoryReturnVoucherEvidenceConfiguration());
        builder.ApplyConfiguration(new InventoryReturnVoucherActionConfiguration());
        builder.ApplyConfiguration(new StockAdjustmentEvidenceConfiguration());
        builder.ApplyConfiguration(new StockAdjustmentActionConfiguration());
        builder.ApplyConfiguration(new InventoryTransferActionConfiguration());
        builder.ApplyConfiguration(new InventoryTransferActionLineConfiguration());
        builder.ApplyConfiguration(new InventoryTransferDiscrepancyConfiguration());
        builder.ApplyConfiguration(new InventoryTransferDiscrepancyEvidenceConfiguration());
        builder.ApplyConfiguration(new InventoryTransferControlRootConfiguration());
        builder.ApplyConfiguration(new InventoryTransferControlItemConfiguration());
        builder.ApplyConfiguration(new InventoryTransferDispatchAllocationConfiguration());
        builder.ApplyConfiguration(new InventoryTransferReceiptAllocationConfiguration());
        builder.ApplyConfiguration(new InventoryTransferMovementAllocationConfiguration());
        builder.ApplyConfiguration(new InventoryTransferStockProjectionAllocationConfiguration());
        builder.ApplyConfiguration(new InventoryTransitWarehouseConfiguration());
        builder.ApplyConfiguration(new InventoryTransitLocationConfiguration());
        builder.ApplyConfiguration(new InventoryCycleCountScheduleConfiguration());
        builder.ApplyConfiguration(new PhysicalCountControlConfiguration());
        builder.ApplyConfiguration(new PhysicalCountCounterConfiguration());
        builder.ApplyConfiguration(new PhysicalCountAdjustmentClaimConfiguration());
        builder.ApplyConfiguration(new WarehouseDefaultLocationConfiguration());
        builder.ApplyConfiguration(new PhysicalCountItemControlConfiguration());
        builder.ApplyConfiguration(new PhysicalCountActionConfiguration());
        builder.ApplyConfiguration(new PhysicalCountWarehouseQuantityConfiguration());
        builder.ApplyConfiguration(new PhysicalCountInventoryItemConfiguration());
        builder.ApplyConfiguration(new PhysicalCountStockMovementConfiguration());
        builder.Entity<WorkflowEscalationExecution>()
            .HasIndex(item => new { item.ApprovalId, item.RuleIndex })
            .IsUnique();
        builder.Entity<WorkflowEvidenceDocument>().HasIndex(item => new { item.TenantId, item.AttachmentId }).IsUnique();
        builder.Entity<WorkflowSignatureEvidence>().HasIndex(item => item.ApprovalId).IsUnique();
        builder.Entity<WorkflowIntegrationExecution>().HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
        builder.Entity<WorkflowOfflineAction>().HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
        ConfigureHrIdentityReconciliation(builder);
        ConfigureCentralDocumentManagementEntities(builder);
        ConfigureLandAcquisitionEntities(builder);
        ConfigureProcedureCaseEntities(builder);
        // Preserve the workflow-delegation indexes introduced by the prior manual migration.
        builder.Entity<WorkflowDelegation>().HasIndex(item => item.WorkflowDefinitionId);
        builder.Entity<WorkflowDelegation>().HasIndex(item => item.WorkflowStepId);
        ConfigureProcurementConfiguration(builder);
        ConfigureQuantitySurveyConfiguration(builder);
        ConfigureCivilEngineeringConfiguration(builder);
        ConfigureProcurementPolicy(builder);
        ConfigureProcurementRequisitionAuthorityRoutes(builder);
        ConfigureProcurementRequisitionSourcingReleases(builder);
        builder.ApplyConfiguration(new ProcurementSourcingCaseConfiguration());
        builder.ApplyConfiguration(new ProcurementSourcingCaseLotConfiguration());
        builder.ApplyConfiguration(new ProcurementSourcingCaseLotItemConfiguration());
        builder.ApplyConfiguration(new ProcurementSourcingCaseSourceRequestConfiguration());
        ConfigureProcurementAccessControl(builder);
        ConfigureProcurementControlEvents(builder);
        ConfigureAuditGovernance(builder);
        ConfigureProcurementMasterDataChanges(builder);
        // The archived-final checks are SQL Server schema authority. Keep their original
        // model-configuration order and do not apply SQL Server predicates to InMemory/SQLite.
        if (Database.IsSqlServer())
        {
            ArchivedCheckConstraintBaselineModel.Apply(builder);
            builder.Entity<PhysicalCountAction>().ToTable(table => table.HasCheckConstraint(
                "CK_PhysicalCountActions_ActionType", "[ActionType] BETWEEN 1 AND 17"));
            builder.Entity<InventoryIssueVoucherAction>().ToTable(table => table.HasCheckConstraint(
                "CK_InventoryIssueVoucherActions_ActionType", "[ActionType] IN (1,2,3)"));
            // Current QS architecture permits internal valuations; retain the historical archive verbatim.
            builder.Entity<ErpSystem.Core.Entities.QuantitySurvey.QuantitySurveyValuationWorksheet>().ToTable(table => table.HasCheckConstraint(
                "CK_QsValuationWorksheets_Policy",
                "([ConfigurationProfileId] IS NULL AND [ValuationDecisionId] IS NULL AND [ExternalSubmissionDecisionId] IS NULL AND [ApprovalWorkflowDefinitionId] IS NULL AND [EvidenceMetadataTemplateId] IS NULL AND [PolicyHash] IS NULL) OR ([ConfigurationProfileId] IS NOT NULL AND [ValuationDecisionId] IS NOT NULL AND (([ContractorSubmissionRequired] = 0 AND [ConsultantEndorsementRequired] = 0) OR [ExternalSubmissionDecisionId] IS NOT NULL) AND [ApprovalWorkflowDefinitionId] IS NOT NULL AND [EvidenceMetadataTemplateId] IS NOT NULL AND LEN([PolicyHash]) = 64)"));
        }
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
        builder.ApplyConfiguration(new TenderEvaluationConfiguration());
        builder.ApplyConfiguration(new TenderLotConfiguration());
        builder.ApplyConfiguration(new TenderBidConfiguration());
        builder.ApplyConfiguration(new TenderFeeConfiguration());
        builder.ApplyConfiguration(new TenderPaymentConfiguration());
        builder.ApplyConfiguration(new TenderBidLotConfiguration());
        builder.ApplyConfiguration(new TenderBidItemConfiguration());
        builder.ApplyConfiguration(new TenderEvaluationConfiguration());
        builder.ApplyConfiguration(new TenderNegotiationConfiguration());
        builder.ApplyConfiguration(new TenderNegotiationItemConfiguration());
        builder.ApplyConfiguration(new TenderAwardConfiguration());
        ConfigureProcurementCentralDocumentLinks<TenderDocument>(builder);
        ConfigureProcurementCentralDocumentLinks<TenderBidDocument>(builder);
        builder.Entity<TenderBidItem>().HasIndex(item => item.TenantId);
        // The composite FK prevents attaching a line from another supplier bid or tenant.
        builder.Entity<TenderBidDocument>()
            .HasOne(document => document.TenderBidItem).WithMany()
            .HasForeignKey(document => new { document.TenantId, document.TenderBidId, document.TenderBidItemId })
            .HasPrincipalKey(item => new { item.TenantId, item.TenderBidId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.ApplyConfiguration(new EvaluationTemplateConfiguration());
        builder.ApplyConfiguration(new EvaluationTemplateCriterionConfiguration());
        builder.ApplyConfiguration(new ProcurementTenderControlConfiguration());
        builder.ApplyConfiguration(new ProcurementTenderDocumentIssueConfiguration());
        builder.ApplyConfiguration(new ProcurementTenderSubmissionReceiptConfiguration());
        builder.ApplyConfiguration(new ProcurementTenderDocumentTemplateVersionConfiguration());
        builder.ApplyConfiguration(new ProcurementTenderDocumentTemplateMethodConfiguration());
        builder.ApplyConfiguration(new ProcurementTenderDocumentRegisterConfiguration());
        builder.ApplyConfiguration(new ProcurementTenderDocumentIssuanceConfiguration());
        builder.ApplyConfiguration(new ProcurementTenderDocumentChangeConfiguration());
        builder.ApplyConfiguration(new ProcurementTenderDocumentChangeRecipientConfiguration());
        builder.ApplyConfiguration(new ProcurementTenderDocumentAcknowledgementConfiguration());
        builder.ApplyConfiguration(new ProcurementSupplierEvidencePackVersionConfiguration());
        builder.ApplyConfiguration(new ProcurementSupplierEvidenceRequirementConfiguration());
        builder.ApplyConfiguration(new ProcurementSupplierRegistrationEvidencePackBindingConfiguration());
        builder.ApplyConfiguration(new ProcurementSupplierOnboardingTokenConfiguration());
        builder.ApplyConfiguration(new ProcurementSupplierOnboardingPaymentConfiguration());
        builder.ApplyConfiguration(new ProcurementSupplierOnboardingExemptionConfiguration());
        builder.ApplyConfiguration(new ProcurementSupplierApplicantAccessConfiguration());
        builder.ApplyConfiguration(new ProcurementSupplierApplicantSessionConfiguration());
        builder.ApplyConfiguration(new ProcurementSupplierDueDiligenceReviewConfiguration());
        builder.ApplyConfiguration(new ProcurementSupplierDueDiligenceCheckConfiguration());
        builder.ApplyConfiguration(new ProcurementSupplierDueDiligenceEvidenceLinkConfiguration());
        builder.ApplyConfiguration(new ProcurementFrameworkAgreementConfiguration());
        builder.ApplyConfiguration(new ProcurementFrameworkAgreementCategoryConfiguration());
        builder.ApplyConfiguration(new ProcurementFrameworkPriceListLineConfiguration());
        builder.ApplyConfiguration(new ProcurementFrameworkCallOffAuthorityConfiguration());
        builder.ApplyConfiguration(new ProcurementFrameworkAgreementDocumentConfiguration());
        builder.ApplyConfiguration(new ProcurementFrameworkAgreementExtensionConfiguration());
        builder.ApplyConfiguration(new ProcurementFrameworkCallOffConfiguration());
        builder.ApplyConfiguration(new ProcurementFrameworkCallOffLineConfiguration());
        builder.ApplyConfiguration(new ProcurementFrameworkAgreementBalanceConfiguration());
        builder.ApplyConfiguration(new ProcurementFrameworkBalanceMovementConfiguration());
        builder.ApplyConfiguration(new ProcurementSupplierAvlRegisterConfiguration());
        builder.ApplyConfiguration(new ProcurementSupplierAvlEntryConfiguration());
        builder.ApplyConfiguration(new ProcurementSupplierAvlEntryStatusHistoryConfiguration());
        builder.ApplyConfiguration(new ProcurementSupplierAvlPublicationSnapshotConfiguration());
        builder.ApplyConfiguration(new ProcurementSupplierRiskAssessmentConfiguration());
        builder.ApplyConfiguration(new ProcurementSupplierRiskAlertConfiguration());
        builder.ApplyConfiguration(new ProcurementSupplierPerformanceScorecardConfiguration());
        builder.ApplyConfiguration(new ProcurementEvaluationCommitteeControlConfiguration());
        builder.ApplyConfiguration(new ProcurementEvaluationCommitteeRoleRequirementConfiguration());
        builder.ApplyConfiguration(new ProcurementEvaluationCommitteeAppointmentConfiguration());
        builder.ApplyConfiguration(new ProcurementEvaluationConflictDeclarationConfiguration());
        builder.ApplyConfiguration(new ProcurementEvaluationMeetingConfiguration());
        builder.ApplyConfiguration(new ProcurementEvaluationAttendanceRecordConfiguration());
        builder.ApplyConfiguration(new ProcurementEvaluationScoreSheetConfiguration());
        builder.ApplyConfiguration(new ProcurementEvaluationScoreRecallConfiguration());
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
        builder.ApplyConfiguration(new AllocationRunBatchConfiguration());
        builder.ApplyConfiguration(new AllocationRunBatchLineConfiguration());

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
            entity.ToTable("Invoices", table =>
            {
                table.HasTrigger("TR_Invoices_OptionalApproval");
                table.HasTrigger("TR_Invoices_DisposalEconomics");
            });
            entity.Property(e => e.ApprovalRequired).HasDefaultValue(true).ValueGeneratedNever();
            entity.Property(e => e.InvoiceNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.CustomerName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.BusinessPartnerCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.BusinessPartnerLegalName).HasMaxLength(200);
            entity.Property(e => e.BusinessPartnerTin).HasMaxLength(100);
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
            entity.Ignore(e => e.BalanceAmount);
            entity.HasIndex(e => e.BusinessPartnerId);
            entity.HasIndex(e => e.BusinessPartnerRoleId);
            entity.HasIndex(e => e.BusinessPartnerArProfileVersionId);
            entity.HasOne(e => e.BusinessPartner)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.BusinessPartnerRole)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerRoleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.BusinessPartnerArProfileVersion)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerArProfileVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.TaxGroup)
                .WithMany()
                .HasForeignKey(e => e.TaxGroupId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PaymentTerm)
                .WithMany()
                .HasForeignKey(e => e.PaymentTermId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ExchangeRateRecord)
                .WithMany()
                .HasForeignKey(e => e.ExchangeRateId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TenantId, e.ExchangeRateId });
            entity.HasIndex(e => new { e.TenantId, e.SourceBookAuthorityId });
            entity.HasOne(e => e.SourceBookAuthority)
                .WithMany()
                .HasForeignKey(e => new { e.TenantId, e.SourceBookAuthorityId })
                .HasPrincipalKey(e => new { e.TenantId, e.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CustomerPayment>(entity =>
        {
            entity.ToTable("CustomerPayment");
            entity.HasIndex(e => e.BusinessPartnerId);
            entity.HasIndex(e => e.BusinessPartnerRoleId);
            entity.HasIndex(e => e.BusinessPartnerArProfileVersionId);
            entity.HasOne(e => e.BusinessPartner)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.BusinessPartnerRole)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerRoleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.BusinessPartnerArProfileVersion)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerArProfileVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.BankAccount)
                .WithMany()
                .HasForeignKey(e => e.BankAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.LiquidityAccount)
                .WithMany()
                .HasForeignKey(e => e.LiquidityAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.LiquidityAccountEntry)
                .WithMany()
                .HasForeignKey(e => e.LiquidityAccountEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            // Reversal links are explicit operational evidence. Restrict deletion so neither the
            // compensating journal nor its cash/liquidity mirror can be removed while referenced.
            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(e => e.ReversalJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinancePostingEvent>()
                .WithMany()
                .HasForeignKey(e => e.ReversalPostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CashTransaction>()
                .WithMany()
                .HasForeignKey(e => e.ReversalCashTransactionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LiquidityAccountEntry>()
                .WithMany()
                .HasForeignKey(e => e.ReversalLiquidityAccountEntryId)
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
            entity.Property(e => e.ReversalReason).HasMaxLength(1000);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TenantId, e.PaymentMethodId });
            entity.HasIndex(e => new { e.TenantId, e.SourceBookAuthorityId });
            entity.HasOne(e => e.SourceBookAuthority)
                .WithMany()
                .HasForeignKey(e => new { e.TenantId, e.SourceBookAuthorityId })
                .HasPrincipalKey(e => new { e.TenantId, e.Id })
                .OnDelete(DeleteBehavior.Restrict);
            // Specialised cutover facts are intentionally linked to their controlled opening batch.
            // This index keeps evidence validation and settlement-read-model rebuilds tenant-local.
            entity.HasIndex(e => new { e.TenantId, e.OpeningBalanceBatchId })
                .HasFilter("[OpeningBalanceBatchId] IS NOT NULL");
            entity.HasIndex(e => new { e.TenantId, e.LiquidityAccountId, e.PaymentDate });
            entity.HasIndex(e => e.ReversalJournalEntryId);
            entity.HasIndex(e => e.ReversalPostingEventId);
            entity.HasIndex(e => e.ReversalCashTransactionId);
            entity.HasIndex(e => e.ReversalLiquidityAccountEntryId);
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
            entity.ToTable("InvoiceLineItem", table => table.HasTrigger("TR_InvoiceLineItem_DisposalEconomics"));
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
            entity.ToTable("SalesOrders", table => table.HasTrigger("TR_SalesOrders_InvoiceSource"));
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => new { e.TenantId, e.InvoiceId }).IsUnique().HasFilter("[InvoiceId] IS NOT NULL");
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
            entity.ToTable("SalesOrderLines", table => table.HasTrigger("TR_SalesOrderLines_InvoiceSource"));
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
            entity.Property(e => e.RowVersion).IsRowVersion();
            // A posted invoice may have many calls/reminders, but Finance must have
            // only one durable primary work item for that exposure. The filtered
            // SQL Server index makes bulk generation idempotent under concurrency.
            entity.HasIndex(e => new { e.TenantId, e.InvoiceId, e.CollectionContext })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [IsPrimaryTask] = 1 AND [InvoiceId] IS NOT NULL");
            entity.HasIndex(e => new { e.TenantId, e.CollectionContext, e.CollectionStatus, e.FollowUpDate });
            entity.HasOne(e => e.BusinessPartner)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Invoice)
                .WithMany()
                .HasForeignKey(e => e.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AssignedTo)
                .WithMany()
                .HasForeignKey(e => e.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CompletedBy)
                .WithMany()
                .HasForeignKey(e => e.CompletedById)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ParentActivity)
                .WithMany(e => e.History)
                .HasForeignKey(e => e.ParentActivityId)
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
            entity.HasOne(e => e.BusinessPartner)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerId)
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

        builder.Entity<LeaseContract>(entity =>
        {
            entity.HasOne<WorkflowInstance>().WithMany()
                .HasForeignKey(item => new { item.TenantId, item.ActivationWorkflowInstanceId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => new { item.TenantId, item.ActivationWorkflowInstanceId });
            entity.HasOne(item => item.AccountingBook).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.AccountingBookId })
                .HasPrincipalKey(book => new { book.TenantId, book.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => new { item.TenantId, item.AccountingBookId });
            entity.HasOne<FinancePostingEvent>().WithMany()
                .HasForeignKey(item => new { item.TenantId, item.RecognitionPostingEventId, item.AccountingBookId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id, item.AccountingBookId })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JournalEntry>().WithMany()
                .HasForeignKey(item => new { item.TenantId, item.RecognitionJournalEntryId, item.AccountingBookId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id, item.AccountingBookId })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany()
                .HasForeignKey(item => item.RouAssetAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany()
                .HasForeignKey(item => item.LeaseLiabilityAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany()
                .HasForeignKey(item => item.InterestExpenseAccountId).OnDelete(DeleteBehavior.Restrict);
        });

        // â”€â”€â”€ Accounts Payable FK Configurations â”€â”€â”€

        builder.Entity<VendorInvoice>(entity =>
        {
            entity.HasAlternateKey(invoice => new { invoice.TenantId, invoice.Id });
            entity.HasIndex(invoice => new { invoice.TenantId, invoice.LeaseScheduleLineId })
                .IsUnique().HasFilter("[LeaseScheduleLineId] IS NOT NULL AND [IsDeleted] = 0 AND [Status] <> 7");
            entity.HasOne(invoice => invoice.LeaseScheduleLine).WithMany(line => line.VendorInvoices)
                .HasForeignKey(invoice => new { invoice.TenantId, invoice.LeaseScheduleLineId })
                .HasPrincipalKey(line => new { line.TenantId, line.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(invoice => new { invoice.TenantId, invoice.ReplacesLeaseVendorInvoiceId })
                .IsUnique().HasFilter("[ReplacesLeaseVendorInvoiceId] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasOne(invoice => invoice.ReplacesLeaseVendorInvoice)
                .WithMany(invoice => invoice.LeaseReplacementInvoices)
                .HasForeignKey(invoice => new { invoice.TenantId, invoice.ReplacesLeaseVendorInvoiceId })
                .HasPrincipalKey(invoice => new { invoice.TenantId, invoice.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(invoice => new { invoice.TenantId, invoice.SourceBookAuthorityId });
            entity.HasOne(invoice => invoice.SourceBookAuthority).WithMany()
                .HasForeignKey(invoice => new { invoice.TenantId, invoice.SourceBookAuthorityId })
                .HasPrincipalKey(authority => new { authority.TenantId, authority.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(invoice => new { invoice.TenantId, invoice.EstateAcquisitionId, invoice.EstatePayableKind })
                .IsUnique().HasFilter("[EstateAcquisitionId] IS NOT NULL");
            entity.HasOne<LandAcquisition>().WithMany()
                .HasForeignKey(invoice => new { invoice.TenantId, invoice.EstateAcquisitionId })
                .HasPrincipalKey(acquisition => new { acquisition.TenantId, acquisition.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(invoice => new { invoice.TenantId, invoice.AutoInvoiceRequestId }).IsUnique()
                .HasFilter("[AutoInvoiceRequestId] IS NOT NULL");
            entity.ToTable("VendorInvoice", table =>
            {
                table.HasTrigger("TR_VendorInvoice_TDC0504MandatoryMatch");
                table.HasTrigger("TR_VendorInvoice_AcceptedSupplyProtected");
                table.HasTrigger("TR_VendorInvoice_ReceiptSource");
                table.HasTrigger("TR_VendorInvoice_EstateSource");
                table.HasCheckConstraint("CK_VendorInvoice_LeaseSourceCoherent",
                    "([LeaseScheduleLineId] IS NULL AND [ReplacesLeaseVendorInvoiceId] IS NULL AND [LeaseAccountingBookId] IS NULL AND [LeaseAccountingBookCode] IS NULL AND [LeaseFunctionalCurrencyCode] IS NULL) OR ([LeaseScheduleLineId] IS NOT NULL AND [LeaseAccountingBookId] IS NOT NULL AND LEN([LeaseAccountingBookCode]) BETWEEN 1 AND 20 AND LEN([LeaseFunctionalCurrencyCode]) = 3 AND [IsOpeningBalance] = 0 AND [PurchaseOrderId] IS NULL AND [AcceptedSupplyKind] IS NULL AND [AutoInvoiceRequestId] IS NULL AND [EstateAcquisitionId] IS NULL)");
                table.HasCheckConstraint("CK_VendorInvoice_EstateSource",
                    "([EstateAcquisitionId] IS NULL AND [EstatePayableKind] IS NULL) OR ([EstateAcquisitionId] IS NOT NULL AND [EstatePayableKind] IS NOT NULL AND [EstatePayableKind] BETWEEN 1 AND 4 AND [IsOpeningBalance] = 0 AND [PurchaseOrderId] IS NULL AND [AcceptedSupplyKind] IS NULL AND [AutoInvoiceRequestId] IS NULL)");
                table.HasCheckConstraint(
                    "CK_VendorInvoice_TDC0504MatchingTolerances",
                    "[MatchingPriceTolerancePercent] BETWEEN 0 AND 100 AND [MatchingQuantityTolerancePercent] BETWEEN 0 AND 100");
                table.HasCheckConstraint(
                    "CK_VendorInvoice_TDC0504SnapshotHash",
                    "[MatchingSnapshotHash] IS NULL OR LEN([MatchingSnapshotHash]) = 64");
                table.HasCheckConstraint(
                    "CK_VendorInvoice_AcceptedSupplyCoherent",
                    "([AcceptedSupplyKind] IS NULL AND [AcceptedSupplySourceId] IS NULL AND [AcceptedSupplySourceReference] IS NULL AND [AcceptedSupplySnapshotHash] IS NULL AND [AcceptedSupplyValidatedAtUtc] IS NULL) OR ([AcceptedSupplyKind] BETWEEN 1 AND 4 AND [AcceptedSupplySourceId] IS NOT NULL AND LEN([AcceptedSupplySourceReference]) BETWEEN 1 AND 100 AND LEN([AcceptedSupplySnapshotHash]) = 64 AND [AcceptedSupplyValidatedAtUtc] IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_VendorInvoice_AcceptedSupplyPurchaseOrder",
                    "[AcceptedSupplyKind] IS NULL OR [AcceptedSupplyKind] = 3 OR ([AcceptedSupplyKind] = 4 AND [AutoInvoiceRequestId] IS NOT NULL) OR [PurchaseOrderId] IS NOT NULL");
                table.HasCheckConstraint(
                    "CK_VendorInvoice_WhtScopeCoherent",
                    "([WithholdingTaxId] IS NULL AND [WithholdingContractReference] IS NULL AND [WithholdingSupplyCategory] IS NULL) OR ([WithholdingTaxId] IS NOT NULL AND LEN([WithholdingContractReference]) BETWEEN 1 AND 100 AND [WithholdingSupplyCategory] BETWEEN 0 AND 2)");
            });
            entity.HasOne(e => e.BusinessPartner)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.BusinessPartnerRole)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerRoleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.BusinessPartnerApProfileVersion)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerApProfileVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TenantId, e.BusinessPartnerId, e.InvoiceDate });
            entity.HasOne(e => e.PurchaseOrder)
                .WithMany()
                .HasForeignKey(e => e.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PaymentTerm)
                .WithMany()
                .HasForeignKey(e => e.PaymentTermId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ExchangeRateRecord)
                .WithMany()
                .HasForeignKey(e => e.ExchangeRateId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountingBook>()
                .WithMany()
                .HasForeignKey(e => new { e.TenantId, e.LeaseAccountingBookId })
                .HasPrincipalKey(e => new { e.TenantId, e.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ExpenseAccount)
                .WithMany()
                .HasForeignKey(e => e.ExpenseAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ApAccount)
                .WithMany()
                .HasForeignKey(e => e.ApAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.SupplierTaxFallbackAccount)
                .WithMany()
                .HasForeignKey(e => e.SupplierTaxFallbackAccountId)
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
            entity.HasOne<ProcurementControlEvent>()
                .WithMany()
                .HasForeignKey(e => e.MatchingControlEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProcurementControlEvent>()
                .WithMany()
                .HasForeignKey(e => e.MatchExceptionControlEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TenantId, e.MatchingControlEventId });
            entity.HasIndex(e => new { e.TenantId, e.MatchExceptionControlEventId });
            entity.HasIndex(e => new { e.TenantId, e.ExchangeRateId });
            entity.HasIndex(e => new
            {
                e.TenantId,
                e.BusinessPartnerId,
                e.WithholdingTaxId,
                e.WithholdingContractReference,
                e.WithholdingSupplyCategory
            }).HasDatabaseName("IX_VendorInvoice_WhtStatutoryScope");
            entity.HasIndex(e => new
                {
                    e.TenantId,
                    e.AcceptedSupplyKind,
                    e.AcceptedSupplySourceId
                },
                "UX_VendorInvoice_AcceptedCertificate")
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [AcceptedSupplyKind] IN (2, 3) AND [AcceptedSupplySourceId] IS NOT NULL");
        });

        builder.ApplyConfiguration(new ProcurementReceiptCostBasisConfiguration());
        builder.ApplyConfiguration(new VendorInvoiceReceiptCostAllocationConfiguration());
        builder.ApplyConfiguration(new VendorInvoiceReceiptCostPostingLineConfiguration());
        builder.ApplyConfiguration(new VendorInvoiceReceiptCostValuationConfiguration());
        builder.Entity<VendorInvoiceReceiptAllocation>(entity =>
        {
            entity.ToTable("VendorInvoiceReceiptAllocations", table => table.HasTrigger("TR_VendorInvoiceReceiptAllocations_Source"));
            entity.HasIndex(value => new { value.TenantId, value.VendorInvoiceLineItemId, value.GoodsReceiptNoteItemId }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.GoodsReceiptNoteItemId });
            entity.HasOne(value => value.VendorInvoice).WithMany().HasForeignKey(value => value.VendorInvoiceId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(value => value.VendorInvoiceLineItem).WithMany().HasForeignKey(value => value.VendorInvoiceLineItemId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(value => value.PurchaseOrder).WithMany().HasForeignKey(value => value.PurchaseOrderId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(value => value.PurchaseOrderItem).WithMany().HasForeignKey(value => value.PurchaseOrderItemId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(value => value.PurchaseOrderReceipt).WithMany().HasForeignKey(value => value.PurchaseOrderReceiptId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(value => value.PurchaseOrderReceiptItem).WithMany().HasForeignKey(value => value.PurchaseOrderReceiptItemId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(value => value.InspectionCase).WithMany().HasForeignKey(value => value.InspectionCaseId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(value => value.GoodsReceiptNote).WithMany().HasForeignKey(value => value.GoodsReceiptNoteId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(value => value.GoodsReceiptNoteItem).WithMany().HasForeignKey(value => value.GoodsReceiptNoteItemId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<VendorInvoiceLineItem>(entity =>
        {
            entity.ToTable("VendorInvoiceLineItem", table => table.HasCheckConstraint(
                "CK_VendorInvoiceLineItem_LeaseComponent", "[LeaseComponent] IS NULL OR [LeaseComponent] BETWEEN 1 AND 2"));
            entity.ToTable("VendorInvoiceLineItem", table => table.HasTrigger("TR_VendorInvoiceLineItem_ReceiptSource"));
            entity.HasOne<LandedCostItem>().WithMany().HasForeignKey(e => e.LandedCostItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.LandedCostItemId).IsUnique()
                .HasFilter("[LandedCostItemId] IS NOT NULL AND [IsDeleted] = 0");
            entity.ToTable("VendorInvoiceLineItem", table =>
                table.HasTrigger("TR_VendorInvoiceLineItem_TDC0504MatchIntegrity"));
            entity.HasOne(e => e.VendorInvoice)
                .WithMany(i => i.LineItems)
                .HasForeignKey(e => e.VendorInvoiceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.GLAccount)
                .WithMany()
                .HasForeignKey(e => e.GLAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.BudgetEntry)
                .WithMany()
                .HasForeignKey(e => e.BudgetEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TenantId, e.BudgetEntryId });
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
            entity.Property(e => e.SupplierCreditNoteReference).HasMaxLength(100);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.RejectionReason).HasMaxLength(1000);
            entity.Property(e => e.ApprovalSource).HasMaxLength(40).IsRequired();
            entity.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(e => e.ExchangeRate).HasColumnType("decimal(18,6)");
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.BaseCurrencyAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ReversalReason).HasMaxLength(1000);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasOne(e => e.Vendor)
                .WithMany()
                .HasForeignKey(e => e.VendorId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.BusinessPartnerRole)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerRoleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.BusinessPartnerApProfileVersion)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerApProfileVersionId)
                .OnDelete(DeleteBehavior.Restrict);
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
            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(e => e.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinancePostingEvent>()
                .WithMany()
                .HasForeignKey(e => e.PostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(e => e.ReversalJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinancePostingEvent>()
                .WithMany()
                .HasForeignKey(e => e.ReversalPostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<WorkflowInstance>()
                .WithMany()
                .HasForeignKey(e => e.WorkflowInstanceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TenantId, e.DebitNoteNumber })
                .HasDatabaseName("UX_SupplierDebitNotes_Tenant_DebitNoteNumber")
                .IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.VendorId, e.SupplierCreditNoteReference })
                .HasDatabaseName("UX_SupplierDebitNotes_Tenant_Vendor_SupplierReference")
                .HasFilter("[SupplierCreditNoteReference] IS NOT NULL AND [IsDeleted] = 0")
                .IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.Status, e.DebitNoteDate })
                .HasDatabaseName("IX_SupplierDebitNotes_Tenant_Status_Date");
        });

        builder.Entity<SupplierDebitNoteLineItem>(entity =>
        {
            entity.ToTable("SupplierDebitNoteLineItems");
            entity.Property(e => e.Description).HasMaxLength(500).IsRequired();
            entity.Property(e => e.LineItemType).HasMaxLength(20).IsRequired();
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
            entity.HasOne<Account>()
                .WithMany()
                .HasForeignKey(e => e.GLAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>()
                .WithMany()
                .HasForeignKey(e => e.ResolvedCreditAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountTransaction>()
                .WithMany()
                .HasForeignKey(e => e.OriginalAccountTransactionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<VendorInvoiceLineItem>()
                .WithMany()
                .HasForeignKey(e => e.OriginalVendorInvoiceLineItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinancePurchaseOrderItem>()
                .WithMany()
                .HasForeignKey(e => e.OriginalFinancePurchaseOrderItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TenantId, e.SupplierDebitNoteId })
                .HasDatabaseName("IX_SupplierDebitNoteLineItems_Tenant_Note");
            entity.HasIndex(e => e.GLAccountId);
        });

        builder.Entity<SupplierDebitNoteTaxComponent>(entity =>
        {
            entity.ToTable("SupplierDebitNoteTaxComponents");
            entity.Property(e => e.BaseAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TaxableAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TaxRate).HasColumnType("decimal(18,4)");
            entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)");
            entity.HasOne(e => e.SupplierDebitNoteLineItem)
                .WithMany(line => line.TaxComponents)
                .HasForeignKey(e => e.SupplierDebitNoteLineItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Tax>()
                .WithMany()
                .HasForeignKey(e => e.TaxId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TaxGroup>()
                .WithMany()
                .HasForeignKey(e => e.TaxGroupId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TaxCalculation>()
                .WithMany()
                .HasForeignKey(e => e.OriginalTaxCalculationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AccountTransaction>()
                .WithMany()
                .HasForeignKey(e => e.OriginalAccountTransactionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>()
                .WithMany()
                .HasForeignKey(e => e.ResolvedCreditAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TenantId, e.SupplierDebitNoteLineItemId, e.CalculationOrder, e.TaxId })
                .IsUnique();
        });

        builder.Entity<SupplierDebitNoteApplication>(entity =>
        {
            entity.ToTable("SupplierDebitNoteApplications", table =>
            {
                table.HasCheckConstraint(
                    "CK_SupplierDebitNoteApplications_Amount",
                    "([IsReversal] = 0 AND [ApplicationAmount] > 0) OR ([IsReversal] = 1 AND [ApplicationAmount] < 0)");
            });
            entity.Property(e => e.ApplicationAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.FunctionalAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(e => e.ExchangeRate).HasColumnType("decimal(18,6)");
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.HasOne(e => e.SupplierDebitNote)
                .WithMany(note => note.Applications)
                .HasForeignKey(e => e.SupplierDebitNoteId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.VendorPayment)
                .WithMany(payment => payment.SupplierDebitNoteApplications)
                .HasForeignKey(e => e.VendorPaymentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.VendorInvoice)
                .WithMany(invoice => invoice.SupplierDebitNoteApplications)
                .HasForeignKey(e => e.VendorInvoiceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(e => e.PaymentJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinancePostingEvent>()
                .WithMany()
                .HasForeignKey(e => e.PaymentPostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TenantId, e.SupplierDebitNoteId, e.VendorPaymentId, e.VendorInvoiceId })
                .HasDatabaseName("IX_SupplierDebitNoteApplications_Tenant_Note_Payment_Invoice");
            entity.HasIndex(e => new { e.TenantId, e.OriginalApplicationId })
                .HasDatabaseName("UX_SupplierDebitNoteApplication_Tenant_Original_Reversal")
                .HasFilter("[IsReversal] = 1 AND [OriginalApplicationId] IS NOT NULL")
                .IsUnique();
        });

        builder.ApplyConfiguration(new VendorPaymentEvidenceLinkConfiguration());
        builder.Entity<VendorPayment>(entity =>
        {
            entity.Property(item => item.ApprovalRequired).HasDefaultValue(true);
            entity.ToTable("VendorPayment", table =>
            {
                table.HasTrigger("TR_VendorPayment_TDC0506InvoiceProcessorSod");
                table.HasTrigger("TR_VendorPayment_OptionalApproval");
                table.HasTrigger("TR_VendorPayment_DirectEvidence");
            });
            entity.HasOne(e => e.BusinessPartner)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.BusinessPartnerRole)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerRoleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.BusinessPartnerApProfileVersion)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerApProfileVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TenantId, e.BusinessPartnerId, e.PaymentDate });
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
            entity.HasOne<AccountingBook>()
                .WithMany()
                .HasForeignKey(e => new { e.TenantId, e.AccountingBookId })
                .HasPrincipalKey(book => new { book.TenantId, book.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TenantId, e.AccountingBookId });
            // Posted AP payments are never edited out of the ledger. These explicit links make
            // the compensating posting first-class and allow source-to-ledger tracing without
            // depending on a convention-based lookup through posting event text fields.
            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(e => e.ReversalJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinancePostingEvent>()
                .WithMany()
                .HasForeignKey(e => e.ReversalPostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            // Workflow and policy references make the exact approval basis independently
            // queryable for audit. Restrict deletion because a submitted payment must retain its
            // immutable control trail even after a definition or policy is retired.
            entity.HasOne<WorkflowInstance>()
                .WithMany()
                .HasForeignKey(e => e.WorkflowInstanceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<WorkflowApprovalPolicySet>()
                .WithMany()
                .HasForeignKey(e => e.AppliedApprovalPolicySetId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.InvoicePaymentSodControlEvent)
                .WithMany()
                .HasForeignKey(e => e.InvoicePaymentSodControlEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TenantId, e.PaymentMethodId });
            // See the equivalent AR index: opening batches are the posting source while the
            // payment remains the canonical AP advance/WHT fact used after go-live.
            entity.HasIndex(e => new { e.TenantId, e.OpeningBalanceBatchId })
                .HasFilter("[OpeningBalanceBatchId] IS NOT NULL");
            entity.HasIndex(e => e.WorkflowInstanceId);
            entity.HasIndex(e => e.AppliedApprovalPolicySetId);
            entity.HasIndex(e => e.ReversalJournalEntryId);
            entity.HasIndex(e => e.ReversalPostingEventId);
            entity.HasIndex(e => new { e.TenantId, e.InvoicePaymentSodControlEventId });
        });

        builder.Entity<VendorPaymentAllocation>(entity =>
        {
            entity.ToTable("VendorPaymentAllocation", table =>
            {
                table.HasTrigger("TR_VendorPaymentAllocation_TDC0505PaymentReadiness");
                table.HasCheckConstraint(
                    "CK_VendorPaymentAllocation_TDC0505Snapshot",
                    "[PaymentReadinessSnapshotHash] IS NULL OR LEN([PaymentReadinessSnapshotHash]) = 64");
            });
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
            entity.HasOne(e => e.PaymentReadinessControlEvent)
                .WithMany()
                .HasForeignKey(e => e.PaymentReadinessControlEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TenantId, e.ApplicationPostingEventId });
            entity.HasIndex(e => new { e.TenantId, e.PaymentReadinessControlEventId });
            entity.HasIndex(e => new { e.TenantId, e.OriginalAllocationId })
                .HasDatabaseName("UX_VendorPaymentAllocation_TenantId_OriginalAllocationId_Reversal")
                .HasFilter("[IsReversal] = 1 AND [OriginalAllocationId] IS NOT NULL")
                .IsUnique();
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PaymentBatch>(entity =>
        {
            entity.Property(item => item.ApprovalRequired).HasDefaultValue(true);
            entity.ToTable("PaymentBatch", table =>
            {
                table.HasTrigger("TR_PaymentBatch_OptionalApproval");
                table.HasTrigger("TR_PaymentBatch_TDC0505Readiness");
                table.HasTrigger("TR_PaymentBatch_TDC0506InvoiceProcessorSod");
            });
            entity.HasOne(e => e.ConfiguredPaymentMethod)
                .WithMany()
                .HasForeignKey(e => e.PaymentMethodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.InvoicePaymentSodControlEvent)
                .WithMany()
                .HasForeignKey(e => e.InvoicePaymentSodControlEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TenantId, e.PaymentMethodId });
            entity.HasIndex(e => new { e.TenantId, e.InvoicePaymentSodControlEventId });
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

        builder.Entity<PaymentBatchInvoice>(entity =>
        {
            entity.ToTable("PaymentBatchInvoice", table =>
            {
                table.HasTrigger("TR_PaymentBatchInvoice_TDC0505PaymentReadiness");
                table.HasTrigger("TR_PaymentBatchInvoice_TDC0505ImmutableSelection");
                table.HasCheckConstraint("CK_PaymentBatchInvoice_TDC0505Amount", "[Amount] > 0");
                table.HasCheckConstraint(
                    "CK_PaymentBatchInvoice_TDC0505Snapshot",
                    "LEN([PaymentReadinessSnapshotHash]) = 64");
                table.HasCheckConstraint(
                    "CK_PaymentBatchInvoice_TDC0505Status",
                    "[Status] IN ('Pending','Processed','Failed')");
            });
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.FailureReason).HasMaxLength(500);
            entity.Property(e => e.PaymentReadinessSnapshotHash).HasMaxLength(64).IsRequired();
            entity.HasOne(e => e.PaymentBatch)
                .WithMany(b => b.Invoices)
                .HasForeignKey(e => e.PaymentBatchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PaymentBatchItem)
                .WithMany(i => i.Invoices)
                .HasForeignKey(e => e.PaymentBatchItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.VendorPayment)
                .WithMany()
                .HasForeignKey(e => e.VendorPaymentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.VendorInvoice)
                .WithMany()
                .HasForeignKey(e => e.VendorInvoiceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PaymentReadinessControlEvent)
                .WithMany()
                .HasForeignKey(e => e.PaymentReadinessControlEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.TenantId, e.PaymentBatchId, e.VendorInvoiceId }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.VendorPaymentId });
            entity.HasIndex(e => new { e.TenantId, e.PaymentReadinessControlEventId });
        });

        // â”€â”€â”€ General Ledger FK Configurations â”€â”€â”€

        builder.Entity<AccountSegmentStructure>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_AccountSegmentStructures_LifecycleActive",
                "([LifecycleStatus] IN (2, 3) AND [IsActive] = 1) OR ([LifecycleStatus] IN (1, 4) AND [IsActive] = 0)"));
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.Property(item => item.LifecycleStatus).HasConversion<int>();
            entity.HasIndex(item => new { item.TenantId, item.SegmentCode }).IsUnique().HasFilter("[IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.SegmentPosition }).IsUnique().HasFilter("[IsDeleted] = 0 AND [IsActive] = 1");
            entity.HasIndex(item => item.TenantId).IsUnique().HasFilter("[IsDeleted] = 0 AND [IsNaturalAccount] = 1 AND [IsActive] = 1");
        });

        builder.Entity<AccountSegmentValue>(entity =>
        {
            entity.HasOne(item => item.Account)
                .WithMany(item => item.SegmentValues)
                .HasForeignKey(item => new { item.TenantId, item.AccountId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SegmentStructure)
                .WithMany()
                .HasForeignKey(item => new { item.TenantId, item.SegmentStructureId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SegmentLookupValue)
                .WithMany(item => item.AccountSegmentValues)
                .HasForeignKey(item => new { item.TenantId, item.SegmentLookupValueId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(item => new { item.TenantId, item.AccountId, item.SegmentStructureId })
                .IsUnique().HasFilter("[IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.AccountId, item.SegmentPosition })
                .IsUnique().HasFilter("[IsDeleted] = 0");
        });

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
            entity.HasAlternateKey(e => new { e.TenantId, e.Id });
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
            entity.ToTable("AccountingBooks", table =>
            {
                // Deleted predecessor rows are retained as historical evidence and were not governed by C3.
                // Live rows alone participate in the new structural contract and lifecycle invariants.
                table.HasCheckConstraint("CK_AccountingBooks_BookType", "[IsDeleted] = 1 OR [BookType] IN (1, 2, 3)");
                table.HasCheckConstraint("CK_AccountingBooks_LifecycleStatus", "[IsDeleted] = 1 OR [LifecycleStatus] IN (1, 2, 3, 4, 5, 6)");
                table.HasCheckConstraint("CK_AccountingBooks_EffectiveDates", "[IsDeleted] = 1 OR [EffectiveToUtc] IS NULL OR [EffectiveFromUtc] IS NULL OR [EffectiveToUtc] > [EffectiveFromUtc]");
                table.HasCheckConstraint("CK_AccountingBooks_BaseShape", "[IsDeleted] = 1 OR ([BookType] = 1 AND [BaseAccountingBookId] IS NULL AND [FunctionalCurrencyCode] IS NOT NULL AND [EffectiveFromUtc] IS NULL AND [EffectiveToUtc] IS NULL AND [ReplicationStartDate] IS NULL AND [ParallelOpeningMode] IS NULL AND [ParallelTranslationMethod] IS NULL) OR ([BookType] = 2 AND [BaseAccountingBookId] IS NOT NULL AND [FunctionalCurrencyCode] IS NOT NULL AND [EffectiveFromUtc] IS NULL AND [EffectiveToUtc] IS NULL AND [ReplicationStartDate] IS NOT NULL AND [ParallelOpeningMode] IS NOT NULL) OR ([BookType] = 3 AND [BaseAccountingBookId] IS NOT NULL AND [FunctionalCurrencyCode] IS NULL AND [ReplicationStartDate] IS NULL AND [ParallelOpeningMode] IS NULL AND [ParallelTranslationMethod] IS NULL)");
                table.HasCheckConstraint("CK_AccountingBooks_DefaultType", "[IsDeleted] = 1 OR ([BookType] = 1 AND [IsDefault] = 1) OR ([BookType] <> 1 AND [IsDefault] = 0)");
                table.HasCheckConstraint("CK_AccountingBooks_NoSelfBase", "[IsDeleted] = 1 OR [BaseAccountingBookId] IS NULL OR [BaseAccountingBookId] <> [Id]");
                table.HasCheckConstraint("CK_AccountingBooks_PostingLifecycle", "[IsDeleted] = 1 OR ([BookType] = 1 AND [LifecycleStatus] = 4 AND [IsActive] = 1 AND [AllowsPosting] = 1) OR ([BookType] <> 1 AND [LifecycleStatus] = 4 AND [IsActive] = 1 AND [AllowsPosting] = 1) OR ([BookType] <> 1 AND [LifecycleStatus] <> 4 AND [IsActive] = 0 AND [AllowsPosting] = 0)");
                table.HasCheckConstraint("CK_AccountingBooks_ParallelTranslationMethod", "[IsDeleted] = 1 OR [BookType] <> 2 OR ([ParallelOpeningMode] = 2 AND [ParallelTranslationMethod] IN (1, 2)) OR ([ParallelOpeningMode] IN (1, 3) AND [ParallelTranslationMethod] IS NULL)");
                if (this.Database.IsSqlServer())
                {
                    table.HasCheckConstraint("CK_AccountingBooks_CodeCanonical", "[IsDeleted] = 1 OR ([Code] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([Code]))) COLLATE Latin1_General_100_BIN2 AND DATALENGTH([Code]) = DATALENGTH(UPPER(LTRIM(RTRIM([Code])))) AND LEFT([Code], 1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [Code] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_]%' AND [Code] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL', N'ALL_ACTIVE_BOOKS', N'ALL_CLASSIFIED_BOOKS', N'ALLCLASSIFIEDBOOKS'))");
                    table.HasCheckConstraint("CK_AccountingBooks_FunctionalCurrencyCanonical", "[IsDeleted] = 1 OR [FunctionalCurrencyCode] IS NULL OR (DATALENGTH([FunctionalCurrencyCode]) = 6 AND [FunctionalCurrencyCode] COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z][A-Z][A-Z]')");
                }
            });
            // Posting evidence uses tenant-qualified book identity so a corrupt foreign tenant
            // reference cannot be legitimized merely because GUIDs are globally unique.
            entity.HasAlternateKey(e => new { e.TenantId, e.Id });
            entity.HasAlternateKey(e => new { e.TenantId, e.Id, e.Code });
            entity.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
            entity.Property(e => e.Code).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Purpose).HasMaxLength(50);
            entity.Property(e => e.FunctionalCurrencyCode).HasMaxLength(3);
            entity.Property(e => e.ReplicationStartDate).HasColumnType("date");
            entity.Property(e => e.PendingTransitionReason).HasMaxLength(500);
            entity.Property(e => e.TransitionDecisionReason).HasMaxLength(500);
            entity.Property(e => e.PrimaryReplacementReason).HasMaxLength(500);
            entity.Property(e => e.RowVersion).IsRowVersion().IsConcurrencyToken();
            // The filtered key makes the one-primary invariant concurrency-safe; service validation
            // remains responsible for the richer PrimaryFull/default/full-book relationship.
            entity.HasIndex(e => new { e.TenantId, e.IsDefault })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [IsDefault] = 1");
            entity.HasIndex(e => new { e.TenantId, e.BaseAccountingBookId });
            entity.HasOne(e => e.BaseAccountingBook)
                .WithMany(e => e.DerivedBooks)
                .HasForeignKey(e => new { e.TenantId, e.BaseAccountingBookId })
                .HasPrincipalKey(e => new { e.TenantId, e.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CurrencyTranslationReserveAccount)
                .WithMany()
                .HasForeignKey(e => new { e.TenantId, e.CurrencyTranslationReserveAccountId })
                .HasPrincipalKey(e => new { e.TenantId, e.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CurrencyRoundingAccount)
                .WithMany()
                .HasForeignKey(e => new { e.TenantId, e.CurrencyRoundingAccountId })
                .HasPrincipalKey(e => new { e.TenantId, e.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccountingBookPrimaryDesignation>(entity =>
        {
            entity.ToTable("AccountingBookPrimaryDesignations", table =>
            {
                table.HasCheckConstraint("CK_AccountingBookPrimaryDesignations_DifferentBooks", "[PreviousPrimaryBookId] <> [NewPrimaryBookId]");
                table.HasCheckConstraint("CK_AccountingBookPrimaryDesignations_NoDelete", "[IsDeleted] = 0");
            });
            entity.HasIndex(item => new { item.TenantId, item.EffectiveFrom }).IsUnique();
            entity.Property(item => item.RequestReason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.DecisionReason).HasMaxLength(500).IsRequired();
            entity.HasOne(item => item.PreviousPrimaryBook).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.PreviousPrimaryBookId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.NewPrimaryBook).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.NewPrimaryBookId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany()
                .HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinanceSourceBookAuthority>(entity =>
        {
            entity.ToTable("FinanceSourceBookAuthorities", table =>
            {
                table.HasTrigger("TR_FinanceSourceBookAuthorities_ImmutableBinding");
                table.HasTrigger("TR_FinanceSourceBookAuthorities_NoDelete");
                table.HasTrigger("TR_FinanceSourceBookAuthorities_Evidence");
                table.HasCheckConstraint("CK_FinanceSourceBookAuthorities_NoDelete", "[IsDeleted] = 0");
                table.HasCheckConstraint("CK_FinanceSourceBookAuthorities_Version", "[AuthorityVersion] > 0");
                table.HasCheckConstraint("CK_FinanceSourceBookAuthorities_Lineage", "([AuthorityVersion] = 1 AND [SupersedesAuthorityId] IS NULL) OR ([AuthorityVersion] > 1 AND [SupersedesAuthorityId] IS NOT NULL)");
                table.HasCheckConstraint("CK_FinanceSourceBookAuthorities_BindingShape", "([OriginalFinancePostingEventId] IS NULL AND [OriginalJournalEntryId] IS NULL AND [BoundByUserId] IS NULL AND [BoundAtUtc] IS NULL) OR ([OriginalFinancePostingEventId] IS NOT NULL AND [OriginalJournalEntryId] IS NOT NULL AND [BoundAtUtc] IS NOT NULL AND (([SelectionBasis] = 'RETAINED_POSTED_ORIGINAL' AND [BoundByUserId] IS NULL) OR ([SelectionBasis] <> 'RETAINED_POSTED_ORIGINAL' AND [BoundByUserId] IS NOT NULL)))");
                table.HasCheckConstraint("CK_FinanceSourceBookAuthorities_FreezeStage", "[FreezeStage] IN ('SUBMITTED','AUTHORIZED','PRE_POST','LEGACY_POSTED')");
                table.HasCheckConstraint("CK_FinanceSourceBookAuthorities_SelectionBasis", "[SelectionBasis] IN ('DEFAULT_PRIMARY','INHERITED_ORIGINAL','RETAINED_POSTED_ORIGINAL')");
                table.HasCheckConstraint("CK_FinanceSourceBookAuthorities_LegacyShape", "([SelectionBasis] = 'RETAINED_POSTED_ORIGINAL' AND [FreezeStage] = 'LEGACY_POSTED' AND [SourceWorkflowInstanceId] IS NULL AND [FrozenByUserId] IS NULL AND [OriginalFinancePostingEventId] IS NOT NULL) OR ([SelectionBasis] <> 'RETAINED_POSTED_ORIGINAL' AND [FreezeStage] <> 'LEGACY_POSTED' AND [FrozenByUserId] IS NOT NULL)");
                if (Database.IsSqlServer())
                {
                    table.HasCheckConstraint("CK_FinanceSourceBookAuthorities_IdentityCanonical", "DATALENGTH([OriginModuleCode])=LEN([OriginModuleCode])*2 AND LEFT([OriginModuleCode],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [OriginModuleCode] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.-]%' AND DATALENGTH([SourceDocumentType])=LEN([SourceDocumentType])*2 AND LEFT([SourceDocumentType],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [SourceDocumentType] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.-]%' AND DATALENGTH([PostingAction])=LEN([PostingAction])*2 AND LEFT([PostingAction],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [PostingAction] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.-]%' ");
                    table.HasCheckConstraint("CK_FinanceSourceBookAuthorities_CurrencyCanonical", "[FunctionalCurrencyCode] COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z][A-Z][A-Z]' AND [TransactionCurrencyCode] COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z][A-Z][A-Z]'");
                    table.HasCheckConstraint("CK_FinanceSourceBookAuthorities_Fingerprint", "LEN([AuthorityFingerprint])=64 AND [AuthorityFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
                }
            });
            entity.HasAlternateKey(item => new { item.TenantId, item.Id });
            entity.Property(item => item.EffectiveDate).HasColumnType("date");
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.OriginModuleCode, item.SourceDocumentType,
                item.SourceDocumentId, item.PostingAction, item.AuthorityVersion }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.OriginModuleCode, item.SourceDocumentType,
                item.SourceDocumentId, item.PostingAction, item.SourceWorkflowInstanceId }).IsUnique()
                .HasFilter("[SourceWorkflowInstanceId] IS NOT NULL");
            entity.HasIndex(item => new { item.TenantId, item.SupersedesAuthorityId }).IsUnique()
                .HasFilter("[SupersedesAuthorityId] IS NOT NULL");
            entity.HasIndex(item => new { item.TenantId, item.OriginalFinancePostingEventId }).IsUnique()
                .HasFilter("[OriginalFinancePostingEventId] IS NOT NULL");
            entity.HasIndex(item => new { item.TenantId, item.OriginalJournalEntryId }).IsUnique()
                .HasFilter("[OriginalJournalEntryId] IS NOT NULL");
            entity.HasIndex(item => new { item.TenantId, item.SourceWorkflowInstanceId });
            entity.HasOne(item => item.AccountingBook).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.AccountingBookId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SupersedesAuthority).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.SupersedesAuthorityId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SourceWorkflowInstance).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.SourceWorkflowInstanceId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.OriginalFinancePostingEvent).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.OriginalFinancePostingEventId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.OriginalJournalEntry).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.OriginalJournalEntryId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinanceSourceBookAuthorityOrigin>(entity =>
        {
            entity.ToTable("FinanceSourceBookAuthorityOrigins", table =>
            {
                table.HasTrigger("TR_FinanceSourceBookAuthorityOrigins_AppendOnly");
                table.HasTrigger("TR_FinanceSourceBookAuthorityOrigins_Evidence");
                table.HasCheckConstraint("CK_FinanceSourceBookAuthorityOrigins_NoDelete", "[IsDeleted] = 0");
                table.HasCheckConstraint("CK_FinanceSourceBookAuthorityOrigins_NoSelf", "[FinanceSourceBookAuthorityId] <> [OriginAuthorityId]");
            });
            entity.HasIndex(item => new { item.TenantId, item.FinanceSourceBookAuthorityId,
                item.OriginAuthorityId, item.Role }).IsUnique();
            entity.HasOne(item => item.FinanceSourceBookAuthority).WithMany(item => item.Origins)
                .HasForeignKey(item => new { item.TenantId, item.FinanceSourceBookAuthorityId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.OriginAuthority).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.OriginAuthorityId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.OriginalFinancePostingEvent).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.OriginalFinancePostingEventId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.OriginalJournalEntry).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.OriginalJournalEntryId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccountingEvent>(entity =>
        {
            entity.ToTable("AccountingEvents", table =>
            {
                table.HasCheckConstraint("CK_AccountingEvents_NoDelete", "[IsDeleted] = 0");
                table.HasCheckConstraint("CK_AccountingEvents_Status", "[Status] IN ('PendingApproval','Pending','Posted','Failed')");
                table.HasCheckConstraint("CK_AccountingEvents_MakerChecker", "[ReleasedByUserId] IS NULL OR [ReleasedByUserId] <> [PreparedByUserId]");
                table.HasCheckConstraint("CK_AccountingEvents_Kind", "[EventKind] IN ('Original','Correction','Reversal')");
                table.HasCheckConstraint("CK_AccountingEvents_Version", "[Version] > 0");
                table.HasCheckConstraint("CK_AccountingEvents_Lineage", "([EventKind] = 'Original' AND [Version] = 1 AND [RootAccountingEventId] = [Id] AND [SupersedesAccountingEventId] IS NULL AND [CorrectsAccountingEventId] IS NULL AND [ReversesAccountingEventId] IS NULL) OR ([EventKind] = 'Correction' AND [Version] > 1 AND [SupersedesAccountingEventId] = [CorrectsAccountingEventId] AND [CorrectsAccountingEventId] IS NOT NULL AND [ReversesAccountingEventId] IS NULL) OR ([EventKind] = 'Reversal' AND [Version] > 1 AND [SupersedesAccountingEventId] = [ReversesAccountingEventId] AND [ReversesAccountingEventId] IS NOT NULL AND [CorrectsAccountingEventId] IS NULL)");
                table.HasCheckConstraint("CK_AccountingEvents_ResultShape", "([Status] = 'PendingApproval' AND [AccountingBookSelectionEvidenceId] IS NULL AND [ReleasedByUserId] IS NULL AND [ReleasedAtUtc] IS NULL AND [ReleaseReason] IS NULL AND [CompletedAtUtc] IS NULL AND [FailureMessage] IS NULL) OR ([Status] = 'Pending' AND [ReleasedByUserId] IS NOT NULL AND [ReleasedAtUtc] IS NOT NULL AND [ReleaseReason] IS NOT NULL AND [CompletedAtUtc] IS NULL AND [FailureMessage] IS NULL) OR ([Status] = 'Posted' AND [AccountingBookSelectionEvidenceId] IS NOT NULL AND LEN([SelectionFingerprint]) = 64 AND [ReleasedByUserId] IS NOT NULL AND [ReleasedAtUtc] IS NOT NULL AND [ReleaseReason] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL AND [FailureMessage] IS NULL) OR ([Status] = 'Failed' AND [ReleasedByUserId] IS NOT NULL AND [ReleasedAtUtc] IS NOT NULL AND [ReleaseReason] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL AND [FailureMessage] IS NOT NULL)");
                table.HasCheckConstraint("CK_AccountingEvents_ProducerDecision", "([ProducerDecisionStatus] = 'NotRequired' AND [ProducerParticipantIdentity] IS NULL AND [ProducerIntentSnapshotJson] IS NULL AND [ProducerIntentSnapshotHash] IS NULL AND [ProducerDecidedByUserId] IS NULL AND [ProducerDecidedAtUtc] IS NULL AND [ProducerDecisionReason] IS NULL) OR ([ProducerDecisionStatus] = 'Pending' AND [ProducerParticipantIdentity] IS NOT NULL AND [ProducerIntentSnapshotJson] IS NOT NULL AND LEN([ProducerIntentSnapshotHash]) = 64 AND [ProducerDecidedByUserId] IS NULL AND [ProducerDecidedAtUtc] IS NULL AND [ProducerDecisionReason] IS NULL) OR ([ProducerDecisionStatus] IN ('Approved','Rejected') AND [ProducerParticipantIdentity] IS NOT NULL AND [ProducerIntentSnapshotJson] IS NOT NULL AND LEN([ProducerIntentSnapshotHash]) = 64 AND [ProducerDecidedByUserId] IS NOT NULL AND [ProducerDecidedAtUtc] IS NOT NULL AND [ProducerDecisionReason] IS NOT NULL)");
                if (Database.IsSqlServer())
                {
                    table.HasCheckConstraint("CK_AccountingEvents_RequestFingerprint", "LEN([RequestFingerprint]) = 64 AND [RequestFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
                    table.HasCheckConstraint("CK_AccountingEvents_SelectionFingerprint", "[SelectionFingerprint] = '' OR (LEN([SelectionFingerprint]) = 64 AND [SelectionFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%')");
                }
            });
            entity.HasAlternateKey(item => new { item.TenantId, item.Id });
            entity.HasAlternateKey(item => new { item.TenantId, item.Id, item.Version });
            entity.HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.RootAccountingEventId, item.Version }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.OriginatingModuleCode, item.SourceDocumentType, item.SourceDocumentId, item.PostingAction, item.Version }).IsUnique();
            entity.Property(item => item.EventDate).HasColumnType("date");
            entity.HasOne(item => item.RootAccountingEvent).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.RootAccountingEventId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SupersedesAccountingEvent).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.SupersedesAccountingEventId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CorrectsAccountingEvent).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.CorrectsAccountingEventId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ReversesAccountingEvent).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.ReversesAccountingEventId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.AccountingBookSelectionEvidence).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.AccountingBookSelectionEvidenceId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccountingEventPosting>(entity =>
        {
            entity.ToTable("AccountingEventPostings", table =>
            {
                table.HasCheckConstraint("CK_AccountingEventPostings_NoDelete", "[IsDeleted] = 0");
                table.HasCheckConstraint("CK_AccountingEventPostings_Status", "[Status] IN ('Pending','Posted','Failed')");
                table.HasCheckConstraint("CK_AccountingEventPostings_EventVersion", "[EventVersion] > 0");
                table.HasCheckConstraint("CK_AccountingEventPostings_ResultShape", "([Status] = 'Pending' AND [FinancePostingEventId] IS NULL AND [JournalEntryId] IS NULL AND [PostedAtUtc] IS NULL AND [FailureMessage] IS NULL) OR ([Status] = 'Posted' AND [FinancePostingEventId] IS NOT NULL AND [JournalEntryId] IS NOT NULL AND [PostedAtUtc] IS NOT NULL AND [FailureMessage] IS NULL) OR ([Status] = 'Failed' AND [FinancePostingEventId] IS NULL AND [JournalEntryId] IS NULL AND [PostedAtUtc] IS NULL AND [FailureMessage] IS NOT NULL)");
                if (Database.IsSqlServer()) table.HasCheckConstraint("CK_AccountingEventPostings_AuthorityFingerprint", "LEN([AuthorityFingerprint]) = 64 AND [AuthorityFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
            });
            entity.HasIndex(item => new { item.TenantId, item.AccountingEventId, item.EventVersion, item.AccountingBookId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.AccountingEventId, item.EventVersion, item.SelectionOrder }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.FinancePostingEventId }).IsUnique()
                .HasFilter("[FinancePostingEventId] IS NOT NULL");
            entity.HasIndex(item => new { item.TenantId, item.JournalEntryId }).IsUnique()
                .HasFilter("[JournalEntryId] IS NOT NULL");
            entity.HasOne(item => item.AccountingEvent).WithMany(item => item.Postings)
                .HasForeignKey(item => new { item.TenantId, item.AccountingEventId, item.EventVersion })
                .HasPrincipalKey(item => new { item.TenantId, item.Id, item.Version }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.AccountingBook).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.AccountingBookId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.FinancePostingEvent).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.FinancePostingEventId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.JournalEntry).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.JournalEntryId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccountingEventAttempt>(entity =>
        {
            entity.ToTable("AccountingEventAttempts", table =>
            {
                table.HasCheckConstraint("CK_AccountingEventAttempts_NoDelete", "[IsDeleted] = 0");
                table.HasCheckConstraint("CK_AccountingEventAttempts_Status", "[Status] IN ('Pending','Posted','Failed')");
                table.HasCheckConstraint("CK_AccountingEventAttempts_Number", "[AttemptNumber] > 0");
                table.HasCheckConstraint("CK_AccountingEventAttempts_ResultShape", "([Status] = 'Pending' AND [CompletedAtUtc] IS NULL AND [FailureMessage] IS NULL) OR ([Status] = 'Posted' AND [CompletedAtUtc] IS NOT NULL AND [FailureMessage] IS NULL) OR ([Status] = 'Failed' AND [CompletedAtUtc] IS NOT NULL AND [FailureMessage] IS NOT NULL)");
                if (Database.IsSqlServer()) table.HasCheckConstraint("CK_AccountingEventAttempts_RequestFingerprint", "LEN([RequestFingerprint]) = 64 AND [RequestFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
            });
            entity.HasIndex(item => new { item.TenantId, item.AccountingEventId, item.AttemptNumber }).IsUnique();
            entity.HasOne(item => item.AccountingEvent).WithMany(item => item.Attempts)
                .HasForeignKey(item => new { item.TenantId, item.AccountingEventId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccountingEventProducerReceipt>(entity =>
        {
            entity.ToTable("AccountingEventProducerReceipts", table =>
            {
                table.HasCheckConstraint("CK_AccountingEventProducerReceipts_NoDelete", "[IsDeleted] = 0");
                if (Database.IsSqlServer())
                {
                    table.HasCheckConstraint("CK_AccountingEventProducerReceipts_EffectFingerprint", "LEN([EffectFingerprint]) = 64 AND [EffectFingerprint] <> REPLICATE('0',64) AND [EffectFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
                    table.HasCheckConstraint("CK_AccountingEventProducerReceipts_RequestFingerprint", "LEN([RequestFingerprint]) = 64 AND [RequestFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
                }
            });
            entity.HasIndex(item => new { item.TenantId, item.AccountingEventId, item.ParticipantCode }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ParticipantCode, item.EffectFingerprint }).IsUnique();
            entity.HasOne(item => item.AccountingEvent).WithOne(item => item.ProducerReceipt)
                .HasForeignKey<AccountingEventProducerReceipt>(item => new { item.TenantId, item.AccountingEventId })
                .HasPrincipalKey<AccountingEvent>(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProducerIntentGroup>(entity =>
        {
            entity.ToTable("ProducerIntentGroups", table =>
            {
                table.HasCheckConstraint("CK_ProducerIntentGroups_NoDelete", "[IsDeleted] = 0");
                table.HasCheckConstraint("CK_ProducerIntentGroups_Status", "[Status] IN ('PendingApproval','Approved','Rejected','Posted','Failed')");
                table.HasCheckConstraint("CK_ProducerIntentGroups_MemberCount", "[MemberCount] BETWEEN 2 AND 20");
                table.HasCheckConstraint("CK_ProducerIntentGroups_Kind", "[GroupKind] IN ('Original','Correction','Reversal')");
                table.HasCheckConstraint("CK_ProducerIntentGroups_Lineage", "([GroupKind] = 'Original' AND [Version] = 1 AND [RootProducerIntentGroupId] = [Id] AND [SupersedesProducerIntentGroupId] IS NULL AND [CorrectsProducerIntentGroupId] IS NULL AND [ReversesProducerIntentGroupId] IS NULL) OR ([GroupKind] = 'Correction' AND [Version] > 1 AND [SupersedesProducerIntentGroupId] = [CorrectsProducerIntentGroupId] AND [CorrectsProducerIntentGroupId] IS NOT NULL AND [ReversesProducerIntentGroupId] IS NULL) OR ([GroupKind] = 'Reversal' AND [Version] > 1 AND [SupersedesProducerIntentGroupId] = [ReversesProducerIntentGroupId] AND [ReversesProducerIntentGroupId] IS NOT NULL AND [CorrectsProducerIntentGroupId] IS NULL)");
                table.HasCheckConstraint("CK_ProducerIntentGroups_Decision", "([Status] = 'PendingApproval' AND [DecidedByUserId] IS NULL AND [DecidedAtUtc] IS NULL AND [DecisionReason] IS NULL AND [CompletedAtUtc] IS NULL AND [FailureMessage] IS NULL) OR ([Status] IN ('Approved','Rejected') AND [DecidedByUserId] IS NOT NULL AND [DecidedAtUtc] IS NOT NULL AND [DecisionReason] IS NOT NULL AND [CompletedAtUtc] IS NULL AND [FailureMessage] IS NULL) OR ([Status] = 'Posted' AND [DecidedByUserId] IS NOT NULL AND [DecidedAtUtc] IS NOT NULL AND [DecisionReason] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL AND [FailureMessage] IS NULL) OR ([Status] = 'Failed' AND [DecidedByUserId] IS NOT NULL AND [DecidedAtUtc] IS NOT NULL AND [DecisionReason] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL AND [FailureMessage] IS NOT NULL)");
                table.HasCheckConstraint("CK_ProducerIntentGroups_MakerChecker", "[DecidedByUserId] IS NULL OR [DecidedByUserId] <> [PreparedByUserId]");
                if (Database.IsSqlServer())
                {
                    table.HasCheckConstraint("CK_ProducerIntentGroups_IdempotencyAscii", "DATALENGTH([IdempotencyKey])=LEN([IdempotencyKey])*2 AND LEFT([IdempotencyKey],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z0-9]' AND [IdempotencyKey] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.:-]%' AND [IdempotencyKey] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')");
                    table.HasCheckConstraint("CK_ProducerIntentGroups_ParticipantAscii", "DATALENGTH([ParticipantCode])=LEN([ParticipantCode])*2 AND LEFT([ParticipantCode],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [ParticipantCode] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.-]%' AND [ParticipantCode] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')");
                    table.HasCheckConstraint("CK_ProducerIntentGroups_OwnerEntityAscii", "DATALENGTH([OwnerEntityType])=LEN([OwnerEntityType])*2 AND LEFT([OwnerEntityType],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [OwnerEntityType] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.-]%' AND [OwnerEntityType] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')");
                    table.HasCheckConstraint("CK_ProducerIntentGroups_OwnerActionAscii", "DATALENGTH([OwnerAction])=LEN([OwnerAction])*2 AND LEFT([OwnerAction],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [OwnerAction] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.-]%' AND [OwnerAction] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')");
                    table.HasCheckConstraint("CK_ProducerIntentGroups_GroupFingerprint", "LEN([GroupFingerprint]) = 64 AND [GroupFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
                    table.HasCheckConstraint("CK_ProducerIntentGroups_SnapshotHash", "LEN([RequestSnapshotHash]) = 64 AND [RequestSnapshotHash] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
                    table.HasCheckConstraint("CK_ProducerIntentGroups_EffectFingerprint", "LEN([ExpectedOwnerEffectFingerprint]) = 64 AND [ExpectedOwnerEffectFingerprint] <> REPLICATE('0',64) AND [ExpectedOwnerEffectFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
                }
            });
            entity.HasAlternateKey(item => new { item.TenantId, item.Id });
            entity.HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.GroupFingerprint }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.RootProducerIntentGroupId, item.Version }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.SupersedesProducerIntentGroupId }).IsUnique()
                .HasFilter("[SupersedesProducerIntentGroupId] IS NOT NULL");
            entity.HasOne(item => item.RootProducerIntentGroup).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.RootProducerIntentGroupId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SupersedesProducerIntentGroup).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.SupersedesProducerIntentGroupId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CorrectsProducerIntentGroup).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.CorrectsProducerIntentGroupId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ReversesProducerIntentGroup).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.ReversesProducerIntentGroupId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProducerIntentGroupMember>(entity =>
        {
            entity.ToTable("ProducerIntentGroupMembers", table =>
            {
                table.HasCheckConstraint("CK_ProducerIntentGroupMembers_NoDelete", "[IsDeleted] = 0");
                table.HasCheckConstraint("CK_ProducerIntentGroupMembers_Order", "[MemberOrder] > 0");
                if (Database.IsSqlServer()) table.HasCheckConstraint("CK_ProducerIntentGroupMembers_Fingerprint", "LEN([MemberFingerprint]) = 64 AND [MemberFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
            });
            entity.HasIndex(item => new { item.TenantId, item.ProducerIntentGroupId, item.MemberOrder }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.AccountingEventId }).IsUnique();
            entity.HasOne(item => item.ProducerIntentGroup).WithMany(item => item.Members)
                .HasForeignKey(item => new { item.TenantId, item.ProducerIntentGroupId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.AccountingEvent).WithOne()
                .HasForeignKey<ProducerIntentGroupMember>(item => new { item.TenantId, item.AccountingEventId })
                .HasPrincipalKey<AccountingEvent>(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProducerIntentGroupReceipt>(entity =>
        {
            entity.ToTable("ProducerIntentGroupReceipts", table =>
            {
                table.HasCheckConstraint("CK_ProducerIntentGroupReceipts_NoDelete", "[IsDeleted] = 0");
                if (Database.IsSqlServer())
                {
                    table.HasCheckConstraint("CK_ProducerIntentGroupReceipts_ParticipantAscii", "DATALENGTH([ParticipantCode])=LEN([ParticipantCode])*2 AND LEFT([ParticipantCode],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [ParticipantCode] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.-]%' AND [ParticipantCode] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')");
                    table.HasCheckConstraint("CK_ProducerIntentGroupReceipts_OwnerEntityAscii", "DATALENGTH([OwnerEntityType])=LEN([OwnerEntityType])*2 AND LEFT([OwnerEntityType],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [OwnerEntityType] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.-]%' AND [OwnerEntityType] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')");
                    table.HasCheckConstraint("CK_ProducerIntentGroupReceipts_OwnerActionAscii", "DATALENGTH([OwnerAction])=LEN([OwnerAction])*2 AND LEFT([OwnerAction],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [OwnerAction] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.-]%' AND [OwnerAction] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')");
                    table.HasCheckConstraint("CK_ProducerIntentGroupReceipts_EffectFingerprint", "LEN([EffectFingerprint]) = 64 AND [EffectFingerprint] <> REPLICATE('0',64) AND [EffectFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
                    table.HasCheckConstraint("CK_ProducerIntentGroupReceipts_GroupFingerprint", "LEN([GroupFingerprint]) = 64 AND [GroupFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
                }
            });
            entity.HasIndex(item => new { item.TenantId, item.ProducerIntentGroupId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ParticipantCode, item.EffectFingerprint }).IsUnique();
            entity.HasOne(item => item.ProducerIntentGroup).WithOne(item => item.Receipt)
                .HasForeignKey<ProducerIntentGroupReceipt>(item => new { item.TenantId, item.ProducerIntentGroupId })
                .HasPrincipalKey<ProducerIntentGroup>(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProducerIntentGroupAttempt>(entity =>
        {
            entity.ToTable("ProducerIntentGroupAttempts", table =>
            {
                table.HasCheckConstraint("CK_ProducerIntentGroupAttempts_NoDelete", "[IsDeleted] = 0");
                table.HasCheckConstraint("CK_ProducerIntentGroupAttempts_Number", "[AttemptNumber] > 0");
                table.HasCheckConstraint("CK_ProducerIntentGroupAttempts_Status", "[Status] IN ('Pending','Posted','Failed')");
                table.HasCheckConstraint("CK_ProducerIntentGroupAttempts_Result", "([Status] = 'Pending' AND [CompletedAtUtc] IS NULL AND [FailureMessage] IS NULL AND [FailedMemberOrder] IS NULL AND [FailedAccountingEventId] IS NULL) OR ([Status] = 'Posted' AND [CompletedAtUtc] IS NOT NULL AND [FailureMessage] IS NULL AND [FailedMemberOrder] IS NULL AND [FailedAccountingEventId] IS NULL) OR ([Status] = 'Failed' AND [CompletedAtUtc] IS NOT NULL AND [FailureMessage] IS NOT NULL AND (([FailedMemberOrder] IS NULL AND [FailedAccountingEventId] IS NULL) OR ([FailedMemberOrder] IS NOT NULL AND [FailedAccountingEventId] IS NOT NULL)))");
                if (Database.IsSqlServer()) table.HasCheckConstraint("CK_ProducerIntentGroupAttempts_Fingerprint", "LEN([GroupFingerprint]) = 64 AND [GroupFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
            });
            entity.HasIndex(item => new { item.TenantId, item.ProducerIntentGroupId, item.AttemptNumber }).IsUnique();
            entity.HasOne(item => item.ProducerIntentGroup).WithMany(item => item.Attempts)
                .HasForeignKey(item => new { item.TenantId, item.ProducerIntentGroupId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccountingBookApplicabilityPolicy>(entity =>
        {
            entity.ToTable("AccountingBookApplicabilityPolicies", table =>
            {
                if (Database.IsSqlServer()) table.HasTrigger("TR_AccountingBookApplicabilityPolicies_C5Authority");
                table.HasCheckConstraint("CK_AccountingBookApplicabilityPolicies_NoDelete", "[IsDeleted] = 0");
                table.HasCheckConstraint("CK_AccountingBookApplicabilityPolicies_Status", "[PolicyStatus] IN (1, 2, 3, 4, 5)");
                table.HasCheckConstraint("CK_AccountingBookApplicabilityPolicies_EffectiveRange", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                table.HasCheckConstraint("CK_AccountingBookApplicabilityPolicies_VersionLineageShape", "([Version] = 1 AND [SupersedesPolicyId] IS NULL) OR ([Version] > 1 AND [SupersedesPolicyId] IS NOT NULL)");
                table.HasCheckConstraint("CK_AccountingBookApplicabilityPolicies_MakerChecker", "[ApprovedByUserId] IS NULL OR [ApprovedByUserId] <> [PreparedByUserId]");
                table.HasCheckConstraint("CK_AccountingBookApplicabilityPolicies_RetirementMakerChecker", "[RetiredByUserId] IS NULL OR [RetiredByUserId] <> [RetirementRequestedByUserId]");
                table.HasCheckConstraint("CK_AccountingBookApplicabilityPolicies_RetirementRequestShape", "([RetirementDecisionStatus] IS NULL AND [RetirementRequestedByUserId] IS NULL AND [RetirementRequestedAtUtc] IS NULL AND [RetirementReason] IS NULL AND [RetirementWorkflowInstanceId] IS NULL AND [RetirementDecidedByUserId] IS NULL AND [RetirementDecidedAtUtc] IS NULL) OR ([RetirementDecisionStatus] = 'Pending' AND [RetirementRequestedByUserId] IS NOT NULL AND [RetirementRequestedAtUtc] IS NOT NULL AND [RetirementReason] IS NOT NULL AND [RetirementWorkflowInstanceId] IS NOT NULL AND [RetirementDecidedByUserId] IS NULL AND [RetirementDecidedAtUtc] IS NULL) OR ([RetirementDecisionStatus] IN ('Approved','Rejected') AND [RetirementRequestedByUserId] IS NOT NULL AND [RetirementRequestedAtUtc] IS NOT NULL AND [RetirementReason] IS NOT NULL AND [RetirementWorkflowInstanceId] IS NOT NULL AND [RetirementDecidedByUserId] IS NOT NULL AND [RetirementDecidedAtUtc] IS NOT NULL AND [RetirementDecisionReason] IS NOT NULL)");
                table.HasCheckConstraint("CK_AccountingBookApplicabilityPolicies_ApprovalShape", "([PolicyStatus] IN (1,2,4) AND [ApprovedByUserId] IS NULL AND [ApprovedAtUtc] IS NULL AND [RetiredByUserId] IS NULL AND [RetiredAtUtc] IS NULL) OR ([PolicyStatus] = 3 AND [ApprovedByUserId] IS NOT NULL AND [ApprovedAtUtc] IS NOT NULL AND [RetiredByUserId] IS NULL AND [RetiredAtUtc] IS NULL) OR ([PolicyStatus] = 5 AND [ApprovedByUserId] IS NOT NULL AND [ApprovedAtUtc] IS NOT NULL AND [RetiredByUserId] IS NOT NULL AND [RetiredAtUtc] IS NOT NULL)");
                if (Database.IsSqlServer())
                    table.HasCheckConstraint("CK_AccountingBookApplicabilityPolicies_CodeCanonical", "LEN([PolicyCode]) > 0 AND [PolicyCode] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([PolicyCode]))) COLLATE Latin1_General_100_BIN2 AND DATALENGTH([PolicyCode]) = DATALENGTH(UPPER(LTRIM(RTRIM([PolicyCode])))) AND LEFT([PolicyCode], 1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [PolicyCode] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_]%' AND [PolicyCode] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')");
            });
            entity.HasAlternateKey(item => new { item.TenantId, item.Id });
            entity.HasAlternateKey(item => new { item.TenantId, item.PolicyCode, item.Id });
            entity.HasAlternateKey(item => new { item.TenantId, item.Id, item.Version });
            entity.HasIndex(item => new { item.TenantId, item.PolicyCode, item.Version }).IsUnique();
            entity.Property(item => item.PolicyCode).HasMaxLength(30).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(150).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(500);
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.DecisionReason).HasMaxLength(500);
            entity.Property(item => item.RetirementReason).HasMaxLength(500);
            entity.Property(item => item.RetirementDecisionStatus).HasMaxLength(20);
            entity.Property(item => item.RetirementDecisionReason).HasMaxLength(500);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasOne(item => item.SupersedesPolicy).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.PolicyCode, item.SupersedesPolicyId })
                .HasPrincipalKey(item => new { item.TenantId, item.PolicyCode, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccountingBookApplicabilityRule>(entity =>
        {
            entity.ToTable("AccountingBookApplicabilityRules", table =>
            {
                if (Database.IsSqlServer()) table.HasTrigger("TR_AccountingBookApplicabilityRules_C5Immutable");
                table.HasCheckConstraint("CK_AccountingBookApplicabilityRules_NoDelete", "[IsDeleted] = 0");
                table.HasCheckConstraint("CK_AccountingBookApplicabilityRules_Priority", "[Priority] >= 0 AND [Priority] <= 1000");
                if (Database.IsSqlServer())
                {
                    table.HasCheckConstraint("CK_AccountingBookApplicabilityRules_RuleCodeCanonical", "LEN([RuleCode]) > 0 AND LEFT([RuleCode],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [RuleCode] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([RuleCode]))) COLLATE Latin1_General_100_BIN2 AND [RuleCode] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_]%' AND [RuleCode] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')");
                    table.HasCheckConstraint("CK_AccountingBookApplicabilityRules_ModuleCanonical", "LEN([OriginatingModuleCode]) > 0 AND [OriginatingModuleCode] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([OriginatingModuleCode]))) COLLATE Latin1_General_100_BIN2 AND [OriginatingModuleCode] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.-]%'");
                    table.HasCheckConstraint("CK_AccountingBookApplicabilityRules_ModuleSupported", "[OriginatingModuleCode] COLLATE Latin1_General_100_BIN2 IN (N'FIN',N'INV',N'PROC',N'SALES',N'HR',N'QS',N'ESTATE',N'LEGAL',N'MAINT')");
                    table.HasCheckConstraint("CK_AccountingBookApplicabilityRules_DocumentCanonical", "LEN([SourceDocumentType]) > 0 AND LEFT([SourceDocumentType],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [SourceDocumentType] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([SourceDocumentType]))) COLLATE Latin1_General_100_BIN2 AND [SourceDocumentType] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.-]%' AND [SourceDocumentType] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')");
                    table.HasCheckConstraint("CK_AccountingBookApplicabilityRules_ActionCanonical", "LEN([PostingAction]) > 0 AND LEFT([PostingAction],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [PostingAction] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([PostingAction]))) COLLATE Latin1_General_100_BIN2 AND [PostingAction] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.-]%' AND [PostingAction] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')");
                }
            });
            entity.HasAlternateKey(item => new { item.TenantId, item.Id });
            entity.HasAlternateKey(item => new { item.TenantId, item.AccountingBookApplicabilityPolicyId, item.Id });
            entity.HasIndex(item => new { item.TenantId, item.AccountingBookApplicabilityPolicyId, item.RuleCode }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.AccountingBookApplicabilityPolicyId, item.OriginatingModuleCode, item.SourceDocumentType, item.PostingAction, item.Priority }).IsUnique();
            entity.Property(item => item.RuleCode).HasMaxLength(40).IsRequired();
            entity.Property(item => item.OriginatingModuleCode).HasMaxLength(40).IsRequired();
            entity.Property(item => item.SourceDocumentType).HasMaxLength(80).IsRequired();
            entity.Property(item => item.PostingAction).HasMaxLength(60).IsRequired();
            entity.HasOne(item => item.Policy).WithMany(item => item.Rules)
                .HasForeignKey(item => new { item.TenantId, item.AccountingBookApplicabilityPolicyId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccountingBookApplicabilityRuleBook>(entity =>
        {
            entity.ToTable("AccountingBookApplicabilityRuleBooks", table =>
            {
                if (Database.IsSqlServer()) table.HasTrigger("TR_AccountingBookApplicabilityRuleBooks_C5Immutable");
                table.HasCheckConstraint("CK_AccountingBookApplicabilityRuleBooks_NoDelete", "[IsDeleted] = 0");
                if (Database.IsSqlServer()) table.HasCheckConstraint("CK_AccountingBookApplicabilityRuleBooks_CodeCanonical", "LEN([AccountingBookCodeSnapshot]) > 0 AND LEFT([AccountingBookCodeSnapshot],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [AccountingBookCodeSnapshot] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([AccountingBookCodeSnapshot]))) COLLATE Latin1_General_100_BIN2 AND [AccountingBookCodeSnapshot] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_]%' AND [AccountingBookCodeSnapshot] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')");
            });
            entity.HasIndex(item => new { item.TenantId, item.AccountingBookApplicabilityRuleId, item.AccountingBookId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.AccountingBookApplicabilityRuleId, item.SelectionOrder }).IsUnique();
            entity.Property(item => item.AccountingBookCodeSnapshot).HasMaxLength(20).IsRequired();
            entity.HasOne(item => item.Rule).WithMany(item => item.SelectedBooks)
                .HasForeignKey(item => new { item.TenantId, item.AccountingBookApplicabilityRuleId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.AccountingBook).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.AccountingBookId, item.AccountingBookCodeSnapshot })
                .HasPrincipalKey(item => new { item.TenantId, item.Id, item.Code }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccountingBookSelectionEvidence>(entity =>
        {
            entity.ToTable("AccountingBookSelectionEvidence", table =>
            {
                table.HasCheckConstraint("CK_AccountingBookSelectionEvidence_NoDelete", "[IsDeleted] = 0");
                table.HasCheckConstraint("CK_AccountingBookSelectionEvidence_RuleLineage", "([AccountingBookApplicabilityPolicyId] IS NULL AND [AccountingBookApplicabilityRuleId] IS NULL AND [PolicyVersion] IS NULL) OR ([AccountingBookApplicabilityPolicyId] IS NOT NULL AND [AccountingBookApplicabilityRuleId] IS NOT NULL AND [PolicyVersion] IS NOT NULL)");
                if (Database.IsSqlServer())
                {
                    table.HasCheckConstraint("CK_AccountingBookSelectionEvidence_ModuleCanonical", "LEN([OriginatingModuleCode]) > 0 AND [OriginatingModuleCode] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([OriginatingModuleCode]))) COLLATE Latin1_General_100_BIN2 AND [OriginatingModuleCode] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.-]%'");
                    table.HasCheckConstraint("CK_AccountingBookSelectionEvidence_ModuleSupported", "[OriginatingModuleCode] COLLATE Latin1_General_100_BIN2 IN (N'FIN',N'INV',N'PROC',N'SALES',N'HR',N'QS',N'ESTATE',N'LEGAL',N'MAINT')");
                    table.HasCheckConstraint("CK_AccountingBookSelectionEvidence_DocumentCanonical", "LEN([SourceDocumentType]) > 0 AND LEFT([SourceDocumentType],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [SourceDocumentType] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([SourceDocumentType]))) COLLATE Latin1_General_100_BIN2 AND [SourceDocumentType] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.-]%' AND [SourceDocumentType] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')");
                    table.HasCheckConstraint("CK_AccountingBookSelectionEvidence_ActionCanonical", "LEN([PostingAction]) > 0 AND LEFT([PostingAction],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [PostingAction] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([PostingAction]))) COLLATE Latin1_General_100_BIN2 AND [PostingAction] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_.-]%' AND [PostingAction] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')");
                    table.HasCheckConstraint("CK_AccountingBookSelectionEvidence_InputHash", "LEN([CalculationInputHash]) = 64 AND [CalculationInputHash] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
                    table.HasCheckConstraint("CK_AccountingBookSelectionEvidence_Fingerprint", "LEN([SelectionFingerprint]) = 64 AND [SelectionFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
                }
            });
            entity.HasAlternateKey(item => new { item.TenantId, item.Id });
            entity.HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
            entity.Property(item => item.IdempotencyKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.OriginatingModuleCode).HasMaxLength(40).IsRequired();
            entity.Property(item => item.SourceDocumentType).HasMaxLength(80).IsRequired();
            entity.Property(item => item.PostingAction).HasMaxLength(60).IsRequired();
            entity.Property(item => item.CalculationInputHash).HasMaxLength(64).IsRequired();
            entity.Property(item => item.SelectionFingerprint).HasMaxLength(64).IsRequired();
            entity.HasOne(item => item.Policy).WithMany(item => item.SelectionEvidence)
                .HasForeignKey(item => new { item.TenantId, item.AccountingBookApplicabilityPolicyId, item.PolicyVersion })
                .HasPrincipalKey(item => new { item.TenantId, item.Id, item.Version }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Rule).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.AccountingBookApplicabilityPolicyId, item.AccountingBookApplicabilityRuleId })
                .HasPrincipalKey(item => new { item.TenantId, item.AccountingBookApplicabilityPolicyId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccountingBookSelectionEvidenceBook>(entity =>
        {
            entity.ToTable("AccountingBookSelectionEvidenceBooks", table =>
            {
                table.HasCheckConstraint("CK_AccountingBookSelectionEvidenceBooks_NoDelete", "[IsDeleted] = 0");
                if (Database.IsSqlServer()) table.HasCheckConstraint("CK_AccountingBookSelectionEvidenceBooks_CodeCanonical", "LEN([AccountingBookCodeSnapshot]) > 0 AND LEFT([AccountingBookCodeSnapshot],1) COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z]' AND [AccountingBookCodeSnapshot] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([AccountingBookCodeSnapshot]))) COLLATE Latin1_General_100_BIN2 AND [AccountingBookCodeSnapshot] COLLATE Latin1_General_100_BIN2 NOT LIKE N'%[^A-Z0-9_]%' AND [AccountingBookCodeSnapshot] COLLATE Latin1_General_100_BIN2 NOT IN (N'ALL',N'ALL_ACTIVE_BOOKS',N'ALL_CLASSIFIED_BOOKS',N'ALLCLASSIFIEDBOOKS')");
                if (Database.IsSqlServer()) table.HasCheckConstraint("CK_AccountingBookSelectionEvidenceBooks_AuthorityFingerprint", "LEN([AuthorityFingerprint]) = 64 AND [AuthorityFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
            });
            entity.HasIndex(item => new { item.TenantId, item.AccountingBookSelectionEvidenceId, item.AccountingBookId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.AccountingBookSelectionEvidenceId, item.SelectionOrder }).IsUnique();
            entity.Property(item => item.AccountingBookCodeSnapshot).HasMaxLength(20).IsRequired();
            entity.Property(item => item.AuthorityFingerprint).HasMaxLength(64).IsRequired();
            entity.HasOne(item => item.Evidence).WithMany(item => item.Books)
                .HasForeignKey(item => new { item.TenantId, item.AccountingBookSelectionEvidenceId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.AccountingBook).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.AccountingBookId, item.AccountingBookCodeSnapshot })
                .HasPrincipalKey(item => new { item.TenantId, item.Id, item.Code }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccountingBookPeriod>(entity =>
        {
            entity.ToTable("AccountingBookPeriods", table =>
            {
                table.HasCheckConstraint("CK_AccountingBookPeriods_NoDelete", "[IsDeleted] = 0");
                table.HasCheckConstraint("CK_AccountingBookPeriods_Status", "[IsDeleted] = 1 OR [PeriodStatus] IN (1, 2, 3, 4)");
                table.HasCheckConstraint("CK_AccountingBookPeriods_PendingStatus", "[PendingStatus] IS NULL OR [PendingStatus] IN (1, 2, 3, 4)");
            });
            entity.HasIndex(item => new { item.TenantId, item.AccountingBookId, item.FiscalPeriodId }).IsUnique();
            entity.Property(item => item.PendingReason).HasMaxLength(500);
            entity.Property(item => item.DecisionReason).HasMaxLength(500);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasOne(item => item.AccountingBook).WithMany(book => book.BookPeriods)
                .HasForeignKey(item => new { item.TenantId, item.AccountingBookId })
                .HasPrincipalKey(book => new { book.TenantId, book.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.FiscalPeriod).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.FiscalPeriodId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<YearEndBookCloseCycle>(entity =>
        {
            entity.ToTable("YearEndBookCloseCycles", table =>
            {
                table.HasTrigger("TR_YearEndBookCloseCycles_ImmutableEvidence");
                table.HasCheckConstraint("CK_YearEndBookCloseCycles_NoDelete", "[IsDeleted] = 0");
                table.HasCheckConstraint("CK_YearEndBookCloseCycles_Status", "[Status] IN ('Closing', 'Closed', 'Reopened')");
                table.HasCheckConstraint("CK_YearEndBookCloseCycles_Cycle", "[CycleNumber] > 0");
                table.HasCheckConstraint("CK_YearEndBookCloseCycles_Reopen", "([Status] <> 'Reopened' AND [ReopenedAtUtc] IS NULL AND [ReopenedByUserId] IS NULL AND [ReopenReason] IS NULL AND [ReversalJournalEntryId] IS NULL) OR ([Status] = 'Reopened' AND [ReopenedAtUtc] IS NOT NULL AND [ReopenedByUserId] IS NOT NULL AND [ReopenReason] IS NOT NULL AND ([ClosingJournalEntryId] IS NULL OR [ReversalJournalEntryId] IS NOT NULL))");
            });
            entity.HasIndex(item => new { item.TenantId, item.FiscalYearId, item.AccountingBookId, item.CycleNumber }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.FiscalYearId, item.AccountingBookId })
                .IsUnique().HasFilter("[Status] IN ('Closing', 'Closed')");
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasOne(item => item.AccountingBook).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.AccountingBookId, item.AccountingBookCode })
                .HasPrincipalKey(item => new { item.TenantId, item.Id, item.Code }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.FiscalYear).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.FiscalYearId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.RetainedEarningsAccount).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.RetainedEarningsAccountId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ClosingJournalEntry).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.ClosingJournalEntryId, item.AccountingBookId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id, item.AccountingBookId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ReversalJournalEntry).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.ReversalJournalEntryId, item.AccountingBookId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id, item.AccountingBookId }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccountingBookInitialization>(entity =>
        {
            entity.ToTable("AccountingBookInitializations", table =>
            {
                table.HasCheckConstraint("CK_AccountingBookInitializations_NoDelete", "[IsDeleted] = 0");
                table.HasCheckConstraint("CK_AccountingBookInitializations_Mode", "[IsDeleted] = 1 OR [Mode] IN (1, 2, 3)");
                table.HasCheckConstraint("CK_AccountingBookInitializations_Status", "[IsDeleted] = 1 OR [InitializationStatus] IN (1, 2, 3, 4)");
                table.HasCheckConstraint("CK_AccountingBookInitializations_Balanced", "[IsDeleted] = 1 OR [TotalDebits] = [TotalCredits]");
                table.HasCheckConstraint("CK_AccountingBookInitializations_MakerChecker", "[ApprovedByUserId] IS NULL OR [ApprovedByUserId] <> [PreparedByUserId]");
                table.HasCheckConstraint("CK_AccountingBookInitializations_DecisionMakerChecker", "[DecidedByUserId] IS NULL OR [DecidedByUserId] <> [PreparedByUserId]");
                table.HasCheckConstraint("CK_AccountingBookInitializations_Coverage", "[RequiredAccountCount] >= 0 AND [CoveredAccountCount] >= 0 AND [CoveredAccountCount] <= [RequiredAccountCount]");
                table.HasCheckConstraint("CK_AccountingBookInitializations_TranslationMethod", "[TranslationMethod] IS NULL OR [TranslationMethod] IN (1, 2)");
                table.HasCheckConstraint("CK_AccountingBookInitializations_SourceShape", "([Mode] = 1 AND [SourceAccountingBookId] IS NULL) OR ([Mode] IN (2, 3) AND [SourceAccountingBookId] IS NOT NULL AND [SourceAccountingBookId] <> [AccountingBookId])");
                table.HasCheckConstraint("CK_AccountingBookInitializations_ApprovalShape", "([InitializationStatus] IN (1, 2) AND [ApprovedByUserId] IS NULL AND [ApprovedAtUtc] IS NULL AND [RejectedByUserId] IS NULL AND [RejectedAtUtc] IS NULL) OR ([InitializationStatus] = 3 AND [ApprovedByUserId] IS NOT NULL AND [ApprovedAtUtc] IS NOT NULL AND [RejectedByUserId] IS NULL AND [RejectedAtUtc] IS NULL) OR ([InitializationStatus] = 4 AND [ApprovedByUserId] IS NULL AND [ApprovedAtUtc] IS NULL AND [RejectedByUserId] IS NOT NULL AND [RejectedAtUtc] IS NOT NULL)");
                if (this.Database.IsSqlServer())
                {
                    table.HasCheckConstraint("CK_AccountingBookInitializations_EvidenceFingerprint", "LEN([EvidenceFingerprint]) = 64 AND [EvidenceFingerprint] = RTRIM([EvidenceFingerprint]) AND [EvidenceFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
                    table.HasCheckConstraint("CK_AccountingBookInitializations_ReconciliationFingerprint", "LEN([ReconciliationFingerprint]) = 64 AND [ReconciliationFingerprint] = RTRIM([ReconciliationFingerprint]) AND [ReconciliationFingerprint] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-F]%'");
                }
            });
            entity.HasAlternateKey(item => new { item.TenantId, item.Id });
            entity.HasAlternateKey(item => new { item.TenantId, item.AccountingBookId, item.Id });
            entity.HasIndex(item => new { item.TenantId, item.AccountingBookId, item.Version }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.AccountingBookId, item.InitializationStatus })
                .IsUnique().HasFilter("[IsDeleted] = 0 AND [InitializationStatus] = 3");
            entity.HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
            entity.Property(item => item.IdempotencyKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.EvidenceFingerprint).HasMaxLength(64).IsRequired();
            entity.Property(item => item.ReconciliationFingerprint).HasMaxLength(64).IsRequired();
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasOne(item => item.AccountingBook).WithMany(book => book.Initializations)
                .HasForeignKey(item => new { item.TenantId, item.AccountingBookId })
                .HasPrincipalKey(book => new { book.TenantId, book.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SourceAccountingBook).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.SourceAccountingBookId })
                .HasPrincipalKey(book => new { book.TenantId, book.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CutoffFiscalPeriod).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.CutoffFiscalPeriodId })
                .HasPrincipalKey(period => new { period.TenantId, period.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SupersedesInitialization).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.AccountingBookId, item.SupersedesInitializationId })
                .HasPrincipalKey(item => new { item.TenantId, item.AccountingBookId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccountingBookInitializationLine>(entity =>
        {
            entity.ToTable("AccountingBookInitializationLines", table =>
            {
                table.HasCheckConstraint("CK_AccountingBookInitializationLines_NoDelete", "[IsDeleted] = 0");
                table.HasCheckConstraint("CK_AccountingBookInitializationLines_Amounts", "[OpeningDebit] >= 0 AND [OpeningCredit] >= 0 AND NOT ([OpeningDebit] > 0 AND [OpeningCredit] > 0)");
                table.HasCheckConstraint("CK_AccountingBookInitializationLines_TranslationEvidence", "([TranslationExchangeRateId] IS NULL AND [TranslationRate] IS NULL AND [TranslationRateDate] IS NULL AND [TranslationRateType] IS NULL AND [TranslationRateSource] IS NULL) OR ([TranslationExchangeRateId] IS NOT NULL AND [TranslationRate] > 0 AND [TranslationRateDate] IS NOT NULL AND [TranslationRateType] IS NOT NULL AND [TranslationRateSource] IS NOT NULL)");
                if (this.Database.IsSqlServer())
                    table.HasCheckConstraint("CK_AccountingBookInitializationLines_Currency", "LEN([CurrencyCode]) = 3 AND [CurrencyCode] = RTRIM([CurrencyCode]) AND [CurrencyCode] COLLATE Latin1_General_100_BIN2 LIKE '[A-Z][A-Z][A-Z]'");
            });
            entity.HasIndex(item => new { item.TenantId, item.AccountingBookInitializationId, item.AccountId }).IsUnique();
            entity.Property(item => item.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(item => item.TranslationRate).HasPrecision(18, 6);
            entity.Property(item => item.TranslationRateType).HasMaxLength(30);
            entity.Property(item => item.TranslationRateSource).HasMaxLength(100);
            entity.HasOne(item => item.Initialization).WithMany(initialization => initialization.Lines)
                .HasForeignKey(item => new { item.TenantId, item.AccountingBookInitializationId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Account).WithMany().HasForeignKey(item => new { item.TenantId, item.AccountId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.TranslationExchangeRate).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.TranslationExchangeRateId })
                .HasPrincipalKey(item => new { item.TenantId, item.Id }).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccountAccountingBook>(entity =>
        {
            entity.ToTable("AccountAccountingBooks");
            entity.HasIndex(e => new { e.TenantId, e.AccountId, e.AccountingBookId }).IsUnique();
            entity.Property(e => e.FinancialStatementLineItem).HasMaxLength(100);
            entity.Property(e => e.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasOne(e => e.Account)
                .WithMany(a => a.AccountingBooks)
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AccountingBook)
                .WithMany(book => book.AccountMappings)
                .HasForeignKey(e => e.AccountingBookId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AccountClassification)
                .WithMany(classification => classification.AccountMappings)
                .HasForeignKey(e => e.AccountClassificationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccountClassification>(entity =>
        {
            entity.ToTable("AccountClassifications");
            entity.HasIndex(item => new { item.TenantId, item.AccountingBookId, item.Code })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.AccountingBookId, item.ParentClassificationId, item.DisplayOrder });
            entity.Property(item => item.Code).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(1000);
            entity.Property(item => item.RetirementReason).HasMaxLength(500);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.AccountingBook).WithMany(book => book.AccountClassifications)
                .HasForeignKey(item => item.AccountingBookId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ParentClassification).WithMany(parent => parent.Children)
                .HasForeignKey(item => item.ParentClassificationId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccountBookCurrencyPolicy>(entity =>
        {
            entity.ToTable("AccountBookCurrencyPolicies");
            entity.HasIndex(item => new { item.TenantId, item.AccountAccountingBookId, item.AccountCurrencyLinkId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.Property(item => item.OverrideReason).HasMaxLength(500);
            entity.Property(item => item.PendingReason).HasMaxLength(500);
            entity.Property(item => item.DecisionReason).HasMaxLength(500);
            entity.Property(item => item.LifecycleStatus).HasMaxLength(30).IsRequired();
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasOne(item => item.AccountAccountingBook)
                .WithMany(mapping => mapping.CurrencyPolicies)
                .HasForeignKey(item => item.AccountAccountingBookId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.AccountCurrencyLink)
                .WithMany(link => link.BookPolicies)
                .HasForeignKey(item => item.AccountCurrencyLinkId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinancialStatementLayout>(entity =>
        {
            entity.ToTable("FinancialStatementLayouts");
            entity.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.AccountingBookId, e.StatementType, e.IsActive });
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Revision).IsConcurrencyToken();
            entity.HasOne(e => e.StandardSourceLayout)
                .WithMany()
                .HasForeignKey(e => e.StandardSourceLayoutId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AccountingBook)
                .WithMany()
                .HasForeignKey(e => e.AccountingBookId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinancialStatementLayoutVersion>(entity =>
        {
            entity.ToTable("FinancialStatementLayoutVersions");
            entity.HasIndex(e => new { e.TenantId, e.FinancialStatementLayoutId, e.VersionNumber }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.FinancialStatementLayoutId, e.Status, e.EffectiveFrom, e.EffectiveTo });
            entity.Property(e => e.PublishedByName).HasMaxLength(200);
            entity.Property(e => e.SubmittedByName).HasMaxLength(200);
            entity.Property(e => e.LastDecisionByName).HasMaxLength(200);
            entity.Property(e => e.LastDecisionReason).HasMaxLength(500);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.PublicationSnapshotSchemaVersion).HasMaxLength(20);
            entity.Property(e => e.PublishedAccountingBookCode).HasMaxLength(20);
            entity.Property(e => e.PublishedAccountingBookName).HasMaxLength(100);
            entity.Property(e => e.HierarchyFingerprint).HasMaxLength(64);
            entity.Property(e => e.ResolutionFingerprint).HasMaxLength(64);
            entity.Property(e => e.Revision).IsConcurrencyToken();
            entity.HasOne(e => e.FinancialStatementLayout)
                .WithMany(layout => layout.Versions)
                .HasForeignKey(e => e.FinancialStatementLayoutId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinancialStatementRow>(entity =>
        {
            entity.ToTable("FinancialStatementRows");
            entity.HasIndex(e => new { e.TenantId, e.FinancialStatementLayoutVersionId, e.RowCode }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.FinancialStatementLayoutVersionId, e.DisplayOrder });
            entity.Property(e => e.RowCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Label).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Formula).HasMaxLength(1000);
            entity.HasOne(e => e.FinancialStatementLayoutVersion)
                .WithMany(version => version.Rows)
                .HasForeignKey(e => e.FinancialStatementLayoutVersionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ParentRow)
                .WithMany(row => row.ChildRows)
                .HasForeignKey(e => e.ParentRowId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinancialStatementRowMapping>(entity =>
        {
            entity.ToTable("FinancialStatementRowMappings");
            entity.HasIndex(e => new { e.TenantId, e.FinancialStatementRowId });
            entity.HasIndex(e => new { e.TenantId, e.AccountId });
            entity.HasIndex(e => new { e.TenantId, e.AccountClassificationId });
            entity.Property(e => e.FromAccountNumber).HasMaxLength(100);
            entity.Property(e => e.ToAccountNumber).HasMaxLength(100);
            entity.HasOne(e => e.FinancialStatementRow)
                .WithMany(row => row.Mappings)
                .HasForeignKey(e => e.FinancialStatementRowId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Account)
                .WithMany()
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AccountClassification)
                .WithMany()
                .HasForeignKey(e => e.AccountClassificationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinancialStatementPublicationAccount>(entity =>
        {
            entity.ToTable("FinancialStatementPublicationAccounts");
            entity.HasIndex(e => new { e.TenantId, e.FinancialStatementLayoutVersionId, e.AccountId }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.AccountClassificationId });
            entity.Property(e => e.RowCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.AccountNumber).HasMaxLength(100).IsRequired();
            entity.Property(e => e.AccountName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.AccountingBookCode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ClassificationCode).HasMaxLength(50);
            entity.Property(e => e.ClassificationName).HasMaxLength(200);
            entity.Property(e => e.ClassificationPath).HasMaxLength(1000);
            entity.Property(e => e.MappingSelector).HasMaxLength(500);
            entity.HasOne(e => e.FinancialStatementLayoutVersion)
                .WithMany(e => e.PublicationAccounts)
                .HasForeignKey(e => e.FinancialStatementLayoutVersionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.FinancialStatementRow)
                .WithMany()
                .HasForeignKey(e => e.FinancialStatementRowId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.FinancialStatementRowMapping)
                .WithMany()
                .HasForeignKey(e => e.FinancialStatementRowMappingId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<JournalEntry>(entity =>
        {
            entity.ToTable("JournalEntries");
            entity.HasAlternateKey(e => new { e.TenantId, e.Id, e.AccountingBookId });
            // The reciprocal flags are useful evidence, but this database invariant is what closes
            // the concurrent double-reversal race for every server-owned reversal path.
            entity.HasIndex(e => new { e.TenantId, e.OriginalJournalEntryId })
                .IsUnique()
                .HasFilter("[OriginalJournalEntryId] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasIndex(e => new { e.TenantId, e.AccountingBookId, e.ReplicatedFromJournalEntryId })
                .IsUnique()
                .HasFilter("[ReplicatedFromJournalEntryId] IS NOT NULL AND [IsDeleted] = 0");
            entity.Property(e => e.ReplicationExchangeRate).HasColumnType("decimal(18,6)");
            entity.Property(e => e.ReplicationRateSource).HasMaxLength(100);
            entity.HasOne(e => e.FiscalPeriod)
                .WithMany(p => p.JournalEntries)
                .HasForeignKey(e => e.FiscalPeriodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AccountingBook)
                .WithMany()
                .HasForeignKey(e => new { e.TenantId, e.AccountingBookId })
                .HasPrincipalKey(e => new { e.TenantId, e.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReplicatedFromJournalEntry)
                .WithMany()
                .HasForeignKey(e => new { e.TenantId, e.ReplicatedFromJournalEntryId })
                .HasPrincipalKey(e => new { e.TenantId, e.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReplicationExchangeRateRecord)
                .WithMany()
                .HasForeignKey(e => e.ReplicationExchangeRateId)
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
            entity.HasAlternateKey(e => new { e.TenantId, e.Id, e.AccountingBookId });
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.AccountingBookId, e.SourceModule, e.SourceDocumentType, e.SourceDocumentId, e.PostingAction })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(e => new { e.TenantId, e.AccountingBookId, e.SourceDocumentType, e.SourceDocumentId, e.PostingAction })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(e => new { e.TenantId, e.AccountingBookId, e.IdempotencyKey })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [IdempotencyKey] IS NOT NULL");
            entity.HasIndex(e => e.JournalEntryId);
            entity.HasIndex(e => e.PrimaryExchangeRateId);
            entity.HasIndex(e => new { e.TenantId, e.HasForeignCurrencyLines });
            entity.Property(e => e.PrimaryExchangeRate).HasColumnType("decimal(18,6)");
            entity.Property(e => e.RequestFingerprintVersion).HasMaxLength(40);
            entity.Property(e => e.RequestFingerprint).HasMaxLength(64);
            entity.HasOne(e => e.AccountingBook)
                .WithMany()
                .HasForeignKey(e => new { e.TenantId, e.AccountingBookId })
                .HasPrincipalKey(e => new { e.TenantId, e.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.JournalEntry)
                .WithMany()
                .HasForeignKey(e => new { e.TenantId, e.JournalEntryId, e.AccountingBookId })
                .HasPrincipalKey(e => new { e.TenantId, e.Id, e.AccountingBookId })
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

        builder.Entity<FinanceDimensionDefinition>(entity =>
        {
            entity.ToTable("FinanceDimensionDefinitions");
            entity.HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
            entity.HasCheckConstraint("CK_FinanceDimensionDefinitions_Classification",
                "[Classification] IN ('Analytical','Balancing','Derived')");
            entity.HasCheckConstraint("CK_FinanceDimensionDefinitions_ValueSourceType",
                "[ValueSourceType] IN ('Lookup','EntityBacked')");
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinanceDimensionValue>(entity =>
        {
            entity.ToTable("FinanceDimensionValues");
            entity.HasIndex(e => new { e.TenantId, e.FinanceDimensionDefinitionId, e.Code }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.FinanceDimensionDefinitionId, e.SourceEntityType, e.SourceEntityId })
                .IsUnique().HasFilter("[SourceEntityId] IS NOT NULL");
            entity.HasOne(e => e.FinanceDimensionDefinition).WithMany(e => e.Values)
                .HasForeignKey(e => e.FinanceDimensionDefinitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ParentValue).WithMany(e => e.ChildValues)
                .HasForeignKey(e => e.ParentValueId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinanceDimensionSet>(entity =>
        {
            entity.ToTable("FinanceDimensionSets");
            entity.HasIndex(e => new { e.TenantId, e.CombinationHash }).IsUnique();
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinanceDimensionSetItem>(entity =>
        {
            entity.ToTable("FinanceDimensionSetItems");
            entity.HasIndex(e => new { e.TenantId, e.FinanceDimensionSetId, e.FinanceDimensionDefinitionId }).IsUnique();
            entity.HasOne(e => e.FinanceDimensionSet).WithMany(e => e.Items)
                .HasForeignKey(e => e.FinanceDimensionSetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FinanceDimensionDefinition).WithMany()
                .HasForeignKey(e => e.FinanceDimensionDefinitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FinanceDimensionValue).WithMany()
                .HasForeignKey(e => e.FinanceDimensionValueId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinanceDimensionAccountRule>(entity =>
        {
            entity.ToTable("FinanceDimensionAccountRules");
            entity.HasIndex(e => new { e.TenantId, e.RuleFamilyId, e.RuleVersion }).IsUnique();
            entity.HasIndex(e => new
            {
                e.TenantId,
                e.AccountId,
                e.FinanceDimensionDefinitionId,
                e.RouteId,
                e.SourceRoute,
                e.ContractVersion,
                e.SourceModule,
                e.SourceDocumentType,
                e.PostingAction
            }).HasDatabaseName("IX_FinanceDimensionAccountRules_RouteScope");
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasCheckConstraint("CK_FinanceDimensionAccountRules_RuleType",
                "[RuleType] IN ('Required','Optional','Prohibited','Fixed')");
            entity.HasOne(e => e.Account).WithMany().HasForeignKey(e => e.AccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FinanceDimensionDefinition).WithMany(e => e.AccountRules)
                .HasForeignKey(e => e.FinanceDimensionDefinitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.DefaultDimensionValue).WithMany()
                .HasForeignKey(e => e.DefaultDimensionValueId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.SupersedesRule).WithMany()
                .HasForeignKey(e => e.SupersedesRuleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinanceDimensionSnapshot>(entity =>
        {
            entity.ToTable("FinanceDimensionSnapshots");
            entity.HasIndex(e => new { e.TenantId, e.FinanceDimensionSetId });
            entity.HasOne(e => e.FinanceDimensionSet).WithMany()
                .HasForeignKey(e => e.FinanceDimensionSetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinanceDimensionSnapshotItem>(entity =>
        {
            entity.ToTable("FinanceDimensionSnapshotItems");
            entity.HasIndex(e => new
                { e.TenantId, e.FinanceDimensionSnapshotId, e.FinanceDimensionDefinitionId })
                .HasDatabaseName("IX_FinanceDimensionSnapshotItems_TenantId_SnapshotId_DefinitionId")
                .IsUnique();
            entity.HasOne(e => e.FinanceDimensionSnapshot).WithMany(e => e.Items)
                .HasForeignKey(e => e.FinanceDimensionSnapshotId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FinanceDimensionDefinition).WithMany()
                .HasForeignKey(e => e.FinanceDimensionDefinitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FinanceDimensionValue).WithMany()
                .HasForeignKey(e => e.FinanceDimensionValueId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FinanceDimensionAccountRule).WithMany()
                .HasForeignKey(e => e.FinanceDimensionAccountRuleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinanceSourceDimensionAssignment>(entity =>
        {
            entity.ToTable("FinanceSourceDimensionAssignments");
            entity.HasIndex(e => new
                { e.TenantId, e.SourceDocumentType, e.SourceDocumentId, e.SourceLineId })
                .HasDatabaseName("IX_FinanceSourceDimensionAssignments_SourceKey")
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(e => new { e.TenantId, e.RouteId, e.SourceDocumentId });
            entity.HasIndex(e => new { e.TenantId, e.ResolvedAccountId });
            entity.HasIndex(e => e.FinanceDimensionSnapshotId).IsUnique()
                .HasFilter("[FinanceDimensionSnapshotId] IS NOT NULL AND [IsDeleted] = 0");
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.Property(e => e.BudgetEvidenceStatus).HasDefaultValue("NotApplicable");
            entity.HasOne(e => e.FinanceDimensionSet).WithMany()
                .HasForeignKey(e => e.FinanceDimensionSetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FinanceDimensionSnapshot).WithMany()
                .HasForeignKey(e => e.FinanceDimensionSnapshotId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany()
                .HasForeignKey(e => e.ResolvedAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinanceSourceDimensionChange>(entity =>
        {
            entity.ToTable("FinanceSourceDimensionChanges");
            entity.HasIndex(e => new { e.TenantId, e.RouteId, e.SourceDocumentId, e.ChangedAt });
            entity.HasIndex(e => new { e.TenantId, e.SourceDocumentType, e.SourceDocumentId, e.SourceLineId });
            entity.HasOne<FinanceDimensionSet>().WithMany()
                .HasForeignKey(e => e.PreviousFinanceDimensionSetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinanceDimensionSet>().WithMany()
                .HasForeignKey(e => e.NewFinanceDimensionSetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PreviousFinanceDimensionSnapshot).WithMany()
                .HasForeignKey(e => e.PreviousFinanceDimensionSnapshotId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.NewFinanceDimensionSnapshot).WithMany()
                .HasForeignKey(e => e.NewFinanceDimensionSnapshotId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinanceSettlementDimensionComponent>(entity =>
        {
            entity.ToTable("FinanceSettlementDimensionComponents");
            entity.HasIndex(e => new
                {
                    e.TenantId,
                    e.RouteId,
                    e.SourceDocumentId,
                    e.SettlementSourceLineId,
                    e.OriginatingSourceLineId,
                    e.ComponentType
                })
                .HasDatabaseName("IX_FinanceSettlementDimensionComponents_EvidenceKey")
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(e => new { e.TenantId, e.RouteId, e.SourceDocumentId });
            entity.HasIndex(e => e.FinanceDimensionSnapshotId);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.Property(e => e.EvidenceVersion).HasDefaultValue("1.0");
            entity.HasCheckConstraint(
                "CK_FinanceSettlementDimensionComponents_ComponentType",
                "[ComponentType] IN (0,1,2,3,4,5,6)");
            entity.HasOne(e => e.FinanceDimensionSet).WithMany()
                .HasForeignKey(e => e.FinanceDimensionSetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FinanceDimensionSnapshot).WithMany()
                .HasForeignKey(e => e.FinanceDimensionSnapshotId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant).WithMany()
                .HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinanceDimensionRouteCertification>(entity =>
        {
            entity.ToTable("FinanceDimensionRouteCertifications");
            entity.HasIndex(e => new { e.TenantId, e.RouteId }).IsUnique();
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasCheckConstraint("CK_FinanceDimensionRouteCertifications_State", "[State] IN (0,1,2)");
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinanceDimensionCertificationTransition>(entity =>
        {
            entity.ToTable("FinanceDimensionCertificationTransitions");
            entity.HasIndex(e => new { e.TenantId, e.FinanceDimensionRouteCertificationId, e.TransitionedAt })
                .HasDatabaseName("IX_FinanceDimensionCertificationTransitions_TenantId_Certification_TransitionedAt");
            entity.HasOne(e => e.FinanceDimensionRouteCertification).WithMany(e => e.Transitions)
                .HasForeignKey(e => e.FinanceDimensionRouteCertificationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReadinessAssessment).WithMany()
                .HasForeignKey(e => e.ReadinessAssessmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinanceDimensionReadinessAssessment>(entity =>
        {
            entity.ToTable("FinanceDimensionReadinessAssessments");
            entity.HasIndex(e => new { e.TenantId, e.RouteId, e.AssessedAt });
            entity.HasCheckConstraint("CK_FinanceDimensionReadinessAssessments_States",
                "[CurrentState] IN (0,1,2) AND [TargetState] IN (0,1,2)");
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AccountTransaction>(entity =>
        {
            entity.ToTable("AccountTransactions");
            entity.Property(e => e.FunctionalCurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(e => e.TransactionDebitAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.TransactionCreditAmount).HasColumnType("decimal(18,2)");
            // Trial balance, ledger inquiry and reconciliation all begin with tenant/book/date
            // scoping before narrowing to accounts. This composite index was added from the
            // NFR-PER workload review so high-volume tenants do not scan the full shared ledger.
            // AccountId is last because both all-account reports and single-account drill-downs
            // can then use the same index prefix.
            entity.HasIndex(e => new
            {
                e.TenantId,
                e.AccountingBookId,
                e.TransactionDate,
                e.AccountId
            });
            entity.HasIndex(e => e.ExchangeRateId);
            entity.HasIndex(e => new { e.TenantId, e.FinanceDimensionSetId, e.TransactionDate, e.AccountId });
            entity.HasIndex(e => e.FinanceDimensionSnapshotId).IsUnique()
                .HasFilter("[FinanceDimensionSnapshotId] IS NOT NULL");
            entity.HasIndex(e => new { e.TenantId, e.TransactionCurrency, e.ExchangeRateId });
            entity.HasIndex(e => new { e.TenantId, e.SourceDocumentId, e.SourceDocumentLineId });
            entity.HasOne(e => e.Account)
                .WithMany(a => a.Transactions)
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ExchangeRateRecord)
                .WithMany()
                .HasForeignKey(e => e.ExchangeRateId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FinanceDimensionSet)
                .WithMany(e => e.AccountTransactions)
                .HasForeignKey(e => e.FinanceDimensionSetId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FinanceDimensionSnapshot)
                .WithMany()
                .HasForeignKey(e => e.FinanceDimensionSnapshotId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AccountingBook)
                .WithMany()
                .HasForeignKey(e => new { e.TenantId, e.AccountingBookId })
                .HasPrincipalKey(e => new { e.TenantId, e.Id })
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.JournalEntry)
                .WithMany(j => j.Transactions)
                .HasForeignKey(e => new { e.TenantId, e.JournalEntryId, e.AccountingBookId })
                .HasPrincipalKey(e => new { e.TenantId, e.Id, e.AccountingBookId })
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

        builder.Entity<TaxCalculation>(entity =>
        {
            entity.HasIndex(e => new
            {
                e.TenantId,
                e.DocumentType,
                e.DocumentId,
                e.DocumentLineId,
                e.TaxId,
                e.TaxGroupId
            }).HasDatabaseName("IX_TaxCalculations_Document_Line_Tax");
            entity.HasOne<Account>()
                .WithMany()
                .HasForeignKey(e => e.PostingAccountId)
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
            entity.HasIndex(e => e.ReversalJournalEntryId);
            entity.HasIndex(e => e.ReversalPostingEventId);
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
            // Realized FX is posted as its own event. Reversing only the cash/AP journal would
            // leave an orphan gain or loss, so the settlement retains its own reversal lineage.
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
        });

        builder.Entity<FxRevaluationBatch>(entity =>
        {
            entity.ToTable("FxRevaluationBatches");
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => new { e.TenantId, e.AccountingBookId, e.Scope, e.RevaluationDate, e.FiscalPeriodId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(e => e.JournalEntryId);
            entity.HasIndex(e => e.PostingEventId);
            entity.Property(e => e.AccountingBookCode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.PreviewFingerprint).HasMaxLength(64);
            entity.HasOne(e => e.FiscalPeriod)
                .WithMany()
                .HasForeignKey(e => e.FiscalPeriodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AccountingBook)
                .WithMany()
                .HasForeignKey(e => e.AccountingBookId)
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
            entity.HasOne(e => e.AccountAccountingBook)
                .WithMany()
                .HasForeignKey(e => e.AccountAccountingBookId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AccountBookCurrencyPolicy)
                .WithMany()
                .HasForeignKey(e => e.AccountBookCurrencyPolicyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AccountClassification)
                .WithMany()
                .HasForeignKey(e => e.AccountClassificationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(e => e.AccountClassificationCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.AccountClassificationName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.CoreAccountType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ClassificationDefault).HasMaxLength(10).IsRequired();
            entity.Property(e => e.EffectivePolicySource).HasMaxLength(30).IsRequired();
            entity.Property(e => e.GovernanceWarning).HasMaxLength(500);
            entity.Property(e => e.ClosingRateType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ClosingQuoteSide).HasMaxLength(20).IsRequired();
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

        builder.Entity<FxRevaluationRateUsage>(entity =>
        {
            entity.ToTable("FxRevaluationRateUsages");
            entity.HasIndex(item => new
                {
                    item.TenantId,
                    item.FxRevaluationBatchId,
                    item.PostingEventId,
                    item.ExchangeRateId
                })
                .IsUnique();
            entity.HasIndex(item => item.PostingEventId);
            entity.HasIndex(item => item.ExchangeRateId);
            entity.HasOne(item => item.Batch)
                .WithMany()
                .HasForeignKey(item => item.FxRevaluationBatchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.PostingEvent)
                .WithMany()
                .HasForeignKey(item => item.PostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ExchangeRate)
                .WithMany()
                .HasForeignKey(item => item.ExchangeRateId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant)
                .WithMany()
                .HasForeignKey(item => item.TenantId)
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
            entity.HasIndex(e => new { e.TenantId, e.ReversalStatus, e.ReversalDueDate });
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
            entity.HasIndex(e => e.BusinessPartnerId);
            entity.HasIndex(e => e.BusinessPartnerRoleId);
            entity.HasIndex(e => e.BusinessPartnerApProfileVersionId);
            entity.HasIndex(e => e.BusinessPartnerArProfileVersionId);
            entity.HasOne(e => e.BusinessPartner)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.BusinessPartnerRole)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerRoleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.BusinessPartnerApProfileVersion)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerApProfileVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.BusinessPartnerArProfileVersion)
                .WithMany()
                .HasForeignKey(e => e.BusinessPartnerArProfileVersionId)
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
            entity.Property(e => e.CapitalizationApprovalSnapshotHash).HasMaxLength(64);
            entity.Property(e => e.CapitalizationApprovalInvalidationReason).HasMaxLength(1000);
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
            entity.HasOne<ExchangeRate>()
                .WithMany()
                .HasForeignKey(e => e.CapitalizationApprovalExchangeRateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OpeningBalanceBatchReversal>(entity =>
        {
            entity.ToTable("OpeningBalanceBatchReversals");
            entity.HasIndex(e => new { e.TenantId, e.OpeningBalanceBatchId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [Status] <> N'Rejected'");
            entity.HasIndex(e => new { e.TenantId, e.Status });
            entity.HasIndex(e => new { e.TenantId, e.ReversalPostingEventId });
            entity.Property(e => e.SourceKind).HasMaxLength(40).IsRequired();
            entity.Property(e => e.BookClassification).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.ImpactAssessment).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.RequestedByUserName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ReviewedByUserName).HasMaxLength(200);
            entity.Property(e => e.ReviewComment).HasMaxLength(2000);
            entity.Property(e => e.FailureReason).HasMaxLength(2000);
            entity.Property(e => e.OriginalTotalDebit).HasPrecision(18, 2);
            entity.Property(e => e.OriginalTotalCredit).HasPrecision(18, 2);
            entity.HasOne(e => e.OpeningBalanceBatch)
                .WithMany(e => e.Reversals)
                .HasForeignKey(e => e.OpeningBalanceBatchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinancePostingEvent>()
                .WithMany()
                .HasForeignKey(e => e.OriginalPostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(e => e.OriginalJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinancePostingEvent>()
                .WithMany()
                .HasForeignKey(e => e.ReversalPostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(e => e.ReversalJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
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
            entity.HasIndex(e => new { e.TenantId, e.ToFixedAssetCategoryId });
            entity.HasIndex(e => new { e.TenantId, e.AccountingBookId });
            entity.Property(e => e.FromLocation).HasMaxLength(500);
            entity.Property(e => e.ToLocation).HasMaxLength(500);
            entity.Property(e => e.FromSegmentString).HasMaxLength(500);
            entity.Property(e => e.ToSegmentString).HasMaxLength(500);
            entity.Property(e => e.IdempotencyKey).HasMaxLength(150);
            entity.Property(e => e.ReferenceNumber).HasMaxLength(50);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.Comments).HasMaxLength(2000);
            entity.Property(e => e.FailureReason).HasMaxLength(1000);
            entity.Property(e => e.BookClassification).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ReclassificationAssetCarryingAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ReclassificationAccumulatedDepreciation).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ReclassificationAccumulatedImpairment).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ReclassificationRevaluationSurplus).HasColumnType("decimal(18,2)");
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
            entity.HasOne(e => e.FromFixedAssetCategory)
                .WithMany()
                .HasForeignKey(e => e.FromFixedAssetCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ToFixedAssetCategory)
                .WithMany()
                .HasForeignKey(e => e.ToFixedAssetCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AccountingBook)
                .WithMany()
                .HasForeignKey(e => e.AccountingBookId)
                .OnDelete(DeleteBehavior.Restrict);
            // Account mappings are retained as immutable evidence on the transfer. Explicit
            // restricted foreign keys prevent deleting an account that explains a posted move.
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.FromAssetAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.ToAssetAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.FromAccumulatedDepreciationAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.ToAccumulatedDepreciationAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.FromAccumulatedImpairmentAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.ToAccumulatedImpairmentAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.FromRevaluationSurplusAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(e => e.ToRevaluationSurplusAccountId).OnDelete(DeleteBehavior.Restrict);
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
            entity.ToTable("AssetDisposals", table =>
            {
                // These constraints mirror the service invariants at the persistence boundary so
                // future import jobs or direct SQL cannot create ambiguous receipt destinations or
                // claim that a disposal was invoiced without retaining its canonical AR document.
                table.HasCheckConstraint(
                    "CK_AssetDisposals_SettlementDestination",
                    "[SettlementBankAccountId] IS NULL OR [SettlementLiquidityAccountId] IS NULL");
                table.HasCheckConstraint(
                    "CK_AssetDisposals_SettlementDocumentState",
                    "([SettlementStatus] IN (0,1,4)) OR ([SettlementStatus] IN (2,3) AND [CustomerInvoiceId] IS NOT NULL)");
            });
            entity.HasIndex(e => new { e.TenantId, e.FixedAssetId, e.BookClassification });
            entity.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
                .IsUnique()
                .HasFilter("[IdempotencyKey] IS NOT NULL");
            entity.HasIndex(e => new { e.TenantId, e.JournalEntryId });
            entity.HasIndex(e => new { e.TenantId, e.PostingEventId });
            entity.HasIndex(e => new { e.TenantId, e.FiscalPeriodId });
            entity.HasIndex(e => new { e.TenantId, e.AccountingBookId });
            entity.HasIndex(e => new { e.TenantId, e.ProceedsAccountId });
            entity.HasIndex(e => new { e.TenantId, e.ProceedsExchangeRateId });
            entity.HasIndex(e => new { e.TenantId, e.RevaluationSurplusAccountId });
            entity.HasIndex(e => new { e.TenantId, e.RetainedEarningsAccountId });
            entity.HasIndex(e => new { e.TenantId, e.BuyerBusinessPartnerId });
            entity.HasIndex(e => new { e.TenantId, e.CustomerInvoiceId })
                .IsUnique()
                .HasFilter("[CustomerInvoiceId] IS NOT NULL");
            entity.HasIndex(e => new { e.TenantId, e.CustomerPaymentId })
                .IsUnique()
                .HasFilter("[CustomerPaymentId] IS NOT NULL");
            entity.Property(e => e.BookClassification).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ProceedsCurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(e => e.ProceedsFunctionalAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ProceedsExchangeRateValue).HasColumnType("decimal(18,6)");
            entity.Property(e => e.ProceedsExchangeRateSource).HasMaxLength(100).IsRequired();
            entity.Property(e => e.IdempotencyKey).HasMaxLength(150);
            entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.Comments).HasMaxLength(1000);
            entity.Property(e => e.FailureReason).HasMaxLength(1000);
            entity.Property(e => e.DisposedPortionPercent).HasColumnType("decimal(18,4)");
            entity.Property(e => e.ComponentReference).HasMaxLength(100);
            entity.Property(e => e.ComponentDescription).HasMaxLength(500);
            entity.Property(e => e.AllocationEvidenceReference).HasMaxLength(200);
            entity.Property(e => e.AllocationEvidenceNotes).HasMaxLength(1000);
            entity.Property(e => e.CostAtDisposal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.AcquisitionCostAllocated).HasColumnType("decimal(18,2)");
            entity.Property(e => e.RevaluationAdjustmentAllocated).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ResidualValueAllocated).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ProductionCapacityAllocated).HasColumnType("decimal(18,4)");
            entity.Property(e => e.AccumulatedProductionUnitsAllocated).HasColumnType("decimal(18,4)");
            entity.Property(e => e.AccumulatedDepreciationAtDisposal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.FinalDepreciationAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.FinalDepreciationProrationBasis).HasMaxLength(30);
            entity.Property(e => e.FinalDepreciationProductionUnits).HasColumnType("decimal(18,4)");
            entity.Property(e => e.FinalDepreciationDiminishingRatePercent).HasColumnType("decimal(18,4)");
            entity.Property(e => e.FinalDepreciationLifetimeProductionCapacity).HasColumnType("decimal(18,4)");
            entity.Property(e => e.FinalDepreciationCumulativeProductionUnitsBefore).HasColumnType("decimal(18,4)");
            entity.Property(e => e.FinalDepreciationCumulativeProductionUnitsAfter).HasColumnType("decimal(18,4)");
            entity.Property(e => e.FinalDepreciationEvidenceReference).HasMaxLength(200);
            entity.Property(e => e.FinalDepreciationEvidenceNotes).HasMaxLength(1000);
            entity.Property(e => e.AccumulatedImpairmentAtDisposal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.RevaluationSurplusAtDisposal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.RevaluationSurplusTransferAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.RemainingAcquisitionCostAfterDisposal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.RemainingAccumulatedDepreciationAfterDisposal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.RemainingNetBookValueAfterDisposal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.NetProceeds).HasColumnType("decimal(18,2)");
            entity.Property(e => e.NetBookValueAtDisposal).HasColumnType("decimal(18,2)");
            entity.Property(e => e.GainOrLoss).HasColumnType("decimal(18,2)");
            entity.Property(e => e.SettlementReference).HasMaxLength(100);
            entity.Property(e => e.SettlementInvoiceAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.SettlementTaxAmount).HasColumnType("decimal(18,2)");
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
            // The referenced rate is durable accounting evidence. Restrict deletion so a posted
            // disposal can always explain how native proceeds became functional-currency GL value.
            entity.HasOne(e => e.ProceedsExchangeRate)
                .WithMany()
                .HasForeignKey(e => e.ProceedsExchangeRateId)
                .OnDelete(DeleteBehavior.Restrict);
            // Disposal evidence retains both equity accounts even if configuration changes later.
            entity.HasOne(e => e.RevaluationSurplusAccount)
                .WithMany()
                .HasForeignKey(e => e.RevaluationSurplusAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.RetainedEarningsAccount)
                .WithMany()
                .HasForeignKey(e => e.RetainedEarningsAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.BuyerBusinessPartner)
                .WithMany()
                .HasForeignKey(e => e.BuyerBusinessPartnerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.SaleTaxGroup)
                .WithMany()
                .HasForeignKey(e => e.SaleTaxGroupId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.SettlementPaymentTerm)
                .WithMany()
                .HasForeignKey(e => e.SettlementPaymentTermId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.SettlementPaymentMethod)
                .WithMany()
                .HasForeignKey(e => e.SettlementPaymentMethodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.SettlementBankAccount)
                .WithMany()
                .HasForeignKey(e => e.SettlementBankAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.SettlementLiquidityAccount)
                .WithMany()
                .HasForeignKey(e => e.SettlementLiquidityAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CustomerInvoice)
                .WithMany()
                .HasForeignKey(e => e.CustomerInvoiceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CustomerPayment)
                .WithMany()
                .HasForeignKey(e => e.CustomerPaymentId)
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

        builder.Entity<ProcurementFixedAssetCapitalization>(entity =>
        {
            entity.ToTable("ProcurementFixedAssetCapitalizations", table =>
            {
                table.HasCheckConstraint("CK_ProcurementFixedAssetCapitalizations_Quantity", "[CapitalizedQuantity] > 0");
                table.HasCheckConstraint("CK_ProcurementFixedAssetCapitalizations_Amounts", "[SourceTransactionAmount] >= 0 AND [FunctionalAmount] > 0");
            });
            entity.HasIndex(value => new { value.TenantId, value.IdempotencyKey }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.PurchaseOrderItemId, value.Status });
            entity.HasIndex(value => new { value.TenantId, value.FixedAssetId, value.Status });
            entity.HasIndex(value => new { value.TenantId, value.PostingEventId });
            entity.HasIndex(value => new { value.TenantId, value.JournalEntryId });
            entity.Property(value => value.AcceptedSupplyReference).HasMaxLength(100).IsRequired();
            entity.Property(value => value.SourceCurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(value => value.FunctionalCurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(value => value.SourceIntegrityHash).HasMaxLength(64).IsRequired();
            entity.Property(value => value.IdempotencyKey).HasMaxLength(100).IsRequired();
            entity.Property(value => value.FailureReason).HasMaxLength(2000);
            entity.HasOne(value => value.FixedAsset)
                .WithMany()
                .HasForeignKey(value => value.FixedAssetId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.PurchaseOrder)
                .WithMany()
                .HasForeignKey(value => value.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.PurchaseOrderItem)
                .WithMany()
                .HasForeignKey(value => value.PurchaseOrderItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.InventoryItem)
                .WithMany()
                .HasForeignKey(value => value.InventoryItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinancePostingEvent>()
                .WithMany()
                .HasForeignKey(value => value.PostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(value => value.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinancePostingEvent>()
                .WithMany()
                .HasForeignKey(value => value.ReversalPostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(value => value.ReversalJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FixedAssetCapitalizationReversal>(entity =>
        {
            entity.ToTable("FixedAssetCapitalizationReversals");
            entity.HasIndex(e => new { e.TenantId, e.FixedAssetId, e.Status });
            entity.HasIndex(e => new { e.TenantId, e.OriginalPostingEventId });
            entity.HasIndex(e => new { e.TenantId, e.ReversalPostingEventId })
                .IsUnique()
                .HasFilter("[ReversalPostingEventId] IS NOT NULL");
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.ImpactAssessment).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.RequestedByUserName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ReviewedByUserName).HasMaxLength(200);
            entity.Property(e => e.ReviewComment).HasMaxLength(2000);
            entity.Property(e => e.FailureReason).HasMaxLength(2000);
            entity.HasOne(e => e.FixedAsset)
                .WithMany(asset => asset.CapitalizationReversals)
                .HasForeignKey(e => e.FixedAssetId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.OriginalPostingEvent)
                .WithMany()
                .HasForeignKey(e => e.OriginalPostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.OriginalJournalEntry)
                .WithMany()
                .HasForeignKey(e => e.OriginalJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReversalPostingEvent)
                .WithMany()
                .HasForeignKey(e => e.ReversalPostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReversalJournalEntry)
                .WithMany()
                .HasForeignKey(e => e.ReversalJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FixedAssetDepreciationReversal>(entity =>
        {
            entity.ToTable("FixedAssetDepreciationReversals");
            entity.HasIndex(e => new { e.TenantId, e.OriginalDepreciationRunId, e.Status });
            entity.HasIndex(e => new { e.TenantId, e.OriginalPostingEventId });
            entity.HasIndex(e => new { e.TenantId, e.ReversalPostingEventId })
                .IsUnique()
                .HasFilter("[ReversalPostingEventId] IS NOT NULL");
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.ImpactAssessment).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.RequestedByUserName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ReviewedByUserName).HasMaxLength(200);
            entity.Property(e => e.ReviewComment).HasMaxLength(2000);
            entity.Property(e => e.FailureReason).HasMaxLength(2000);
            entity.HasOne(e => e.OriginalDepreciationRun)
                .WithMany(run => run.Reversals)
                .HasForeignKey(e => e.OriginalDepreciationRunId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.OriginalPostingEvent)
                .WithMany()
                .HasForeignKey(e => e.OriginalPostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.OriginalJournalEntry)
                .WithMany()
                .HasForeignKey(e => e.OriginalJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReversalPostingEvent)
                .WithMany()
                .HasForeignKey(e => e.ReversalPostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReversalJournalEntry)
                .WithMany()
                .HasForeignKey(e => e.ReversalJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FixedAssetBookValue>(entity =>
        {
            entity.ToTable("FixedAssetBookValues");
            entity.HasIndex(e => new { e.TenantId, e.FixedAssetId, e.AccountingBookId }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.BookClassification });
            entity.HasIndex(e => new { e.TenantId, e.CapitalizationPostingEventId });
            entity.HasIndex(e => new { e.TenantId, e.CapitalizationJournalEntryId });
            entity.HasIndex(e => new { e.TenantId, e.CapitalizationReversalPostingEventId });
            entity.HasIndex(e => new { e.TenantId, e.CapitalizationReversalJournalEntryId });
            entity.Property(e => e.BookClassification).HasMaxLength(20).IsRequired();
            entity.Property(e => e.OpeningSource).HasMaxLength(50);
            entity.Property(e => e.SourceDocumentType).HasMaxLength(50);
            entity.Property(e => e.DiminishingBalanceRatePercent).HasColumnType("decimal(18,4)");
            entity.Property(e => e.LifetimeProductionCapacity).HasColumnType("decimal(18,4)");
            entity.Property(e => e.AccumulatedProductionUnits).HasColumnType("decimal(18,4)");
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
            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(e => e.CapitalizationReversalJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinancePostingEvent>()
                .WithMany()
                .HasForeignKey(e => e.CapitalizationReversalPostingEventId)
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
            entity.Property(e => e.OutstandingImpairmentBefore).HasColumnType("decimal(18,2)");
            entity.Property(e => e.UnimpairedCarryingAmountCap).HasColumnType("decimal(18,2)");
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
            entity.HasOne(e => e.SourceImpairmentValuation)
                .WithMany()
                .HasForeignKey(e => e.SourceImpairmentValuationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AssetValuationCorrection>(entity =>
        {
            entity.ToTable("AssetValuationCorrections");
            entity.HasIndex(e => new { e.TenantId, e.OriginalValuationId });
            entity.HasIndex(e => new { e.TenantId, e.ReversalPostingEventId });
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.ImpactAssessment).HasMaxLength(2000).IsRequired();
            entity.HasOne(e => e.OriginalValuation)
                .WithMany()
                .HasForeignKey(e => e.OriginalValuationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.OriginalPostingEvent)
                .WithMany()
                .HasForeignKey(e => e.OriginalPostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.OriginalJournalEntry)
                .WithMany()
                .HasForeignKey(e => e.OriginalJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReversalPostingEvent)
                .WithMany()
                .HasForeignKey(e => e.ReversalPostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReversalJournalEntry)
                .WithMany()
                .HasForeignKey(e => e.ReversalJournalEntryId)
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
            entity.Property(e => e.DiminishingBalanceRatePercentSnapshot).HasColumnType("decimal(18,4)");
            entity.Property(e => e.LifetimeProductionCapacitySnapshot).HasColumnType("decimal(18,4)");
            entity.Property(e => e.PeriodProductionUnits).HasColumnType("decimal(18,4)");
            entity.Property(e => e.CumulativeProductionUnitsBefore).HasColumnType("decimal(18,4)");
            entity.Property(e => e.CumulativeProductionUnitsAfter).HasColumnType("decimal(18,4)");
            entity.Property(e => e.ProductionEvidenceReference).HasMaxLength(200);
            entity.Property(e => e.ProductionEvidenceNotes).HasMaxLength(1000);
            entity.Property(e => e.ConventionFactor).HasColumnType("decimal(18,8)");
            entity.Property(e => e.ConventionBasis).HasMaxLength(80);
            entity.HasIndex(e => new { e.TenantId, e.FixedAssetId, e.FiscalPeriodId, e.BookClassification, e.CorrectionSequence }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.FixedAssetDepreciationRunId });
            entity.HasIndex(e => new { e.TenantId, e.PostingEventId });
            entity.HasIndex(e => new { e.TenantId, e.JournalEntryId });
            entity.HasIndex(e => new { e.TenantId, e.ReversalPostingEventId });
            entity.HasIndex(e => new { e.TenantId, e.DepreciationReversalId });
            entity.HasIndex(e => new { e.TenantId, e.AssetDisposalId })
                .IsUnique()
                .HasFilter("[AssetDisposalId] IS NOT NULL");
            entity.HasOne(e => e.DepreciationRun)
                .WithMany(e => e.Lines)
                .HasForeignKey(e => e.FixedAssetDepreciationRunId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AssetDisposal)
                .WithMany()
                .HasForeignKey(e => e.AssetDisposalId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AccountingBook)
                .WithMany()
                .HasForeignKey(e => e.AccountingBookId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PostingEvent)
                .WithMany()
                .HasForeignKey(e => e.PostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(e => e.ReversalJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FinancePostingEvent>()
                .WithMany()
                .HasForeignKey(e => e.ReversalPostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.DepreciationReversal)
                .WithMany()
                .HasForeignKey(e => e.DepreciationReversalId)
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
            entity.Property(c => c.DefaultDiminishingBalanceRatePercent).HasColumnType("decimal(18,4)");
            entity.Property(c => c.DefaultLifetimeProductionCapacity).HasColumnType("decimal(18,4)");
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
            entity.HasIndex(e => new { e.TenantId, e.SourceBookAuthorityId });
            entity.HasIndex(e => new { e.TenantId, e.ReversalOfCashTransactionId });
            entity.HasIndex(e => new { e.TenantId, e.ReversalCashTransactionId });
            entity.HasIndex(e => new { e.TenantId, e.ReversalJournalEntryId });
            entity.HasIndex(e => new { e.TenantId, e.ReversalPostingEventId });
            // One OUT and one IN row may exist for a transfer-pair id. This is both the
            // idempotency guard for capture retries and the primary operational lineage key.
            entity.HasIndex(e => new { e.TenantId, e.TransferPairId, e.TransferLeg })
                .IsUnique()
                .HasFilter("[TransferPairId] IS NOT NULL AND [TransferLeg] IS NOT NULL");
            entity.HasIndex(e => new { e.TenantId, e.ExchangeRateId });

            entity.Property(e => e.ApprovalComments).HasMaxLength(1000);
            entity.Property(e => e.RejectionReason).HasMaxLength(1000);
            entity.Property(e => e.CancellationReason).HasMaxLength(1000);
            entity.Property(e => e.ReversalReason).HasMaxLength(1000);
            entity.Property(e => e.ExchangeRate).HasColumnType("decimal(18,6)");
            entity.Property(e => e.TransferCrossRate).HasColumnType("decimal(18,8)");
            entity.Property(e => e.TransferFxGainLossBaseAmount).HasColumnType("decimal(18,2)");

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

            entity.HasOne(ct => ct.SourceBookAuthority)
                .WithMany()
                .HasForeignKey(ct => new { ct.TenantId, ct.SourceBookAuthorityId })
                .HasPrincipalKey(authority => new { authority.TenantId, authority.Id })
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ct => ct.ExchangeRateRecord)
                .WithMany()
                .HasForeignKey(ct => ct.ExchangeRateId)
                .OnDelete(DeleteBehavior.Restrict);

            // Reversals are compensating records, never destructive updates. These two separate
            // self-links retain both directions of the operational correction chain and use
            // Restrict so neither side can be removed while the accounting evidence references it.
            entity.HasOne(ct => ct.ReversalOfCashTransaction)
                .WithMany()
                .HasForeignKey(ct => ct.ReversalOfCashTransactionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ct => ct.ReversalCashTransaction)
                .WithMany()
                .HasForeignKey(ct => ct.ReversalCashTransactionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ct => ct.ReversalJournalEntry)
                .WithMany()
                .HasForeignKey(ct => ct.ReversalJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ct => ct.ReversalPostingEvent)
                .WithMany()
                .HasForeignKey(ct => ct.ReversalPostingEventId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinanceControlledDocumentIssue>(entity =>
        {
            // A source can have exactly one row for each one-based copy number. The issue service
            // allocates the sequence under SERIALIZABLE isolation; this unique index is the final
            // database guard against duplicate originals or replacement numbers.
            entity.HasIndex(item => new
                {
                    item.TenantId,
                    item.DocumentType,
                    item.SourceDocumentId,
                    item.CopyNumber
                })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(item => new
                {
                    item.TenantId,
                    item.DocumentType,
                    item.IssuedAtUtc
                });
            entity.HasIndex(item => new { item.TenantId, item.JournalEntryId });

            entity.HasOne(item => item.IssuedBy)
                .WithMany()
                .HasForeignKey(item => item.IssuedById)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.JournalEntry)
                .WithMany()
                .HasForeignKey(item => item.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_FinanceControlledDocumentIssues_CopyNumber",
                    "[CopyNumber] >= 1");
                table.HasCheckConstraint(
                    "CK_FinanceControlledDocumentIssues_CopyType",
                    "[CopyType] IN ('Original', 'Replacement')");
                table.HasCheckConstraint(
                    "CK_FinanceControlledDocumentIssues_ReplacementReason",
                    "([CopyType] = 'Original' AND [ReplacementReason] IS NULL) OR ([CopyType] = 'Replacement' AND LEN([ReplacementReason]) >= 20)");
                table.HasCheckConstraint(
                    "CK_FinanceControlledDocumentIssues_ContentSha256",
                    "LEN([ContentSha256]) = 64");
                table.HasCheckConstraint(
                    "CK_FinanceControlledDocumentIssues_RetainedArtifact",
                    "([StoragePath] IS NULL AND [StorageProvider] IS NULL AND [FileSize] IS NULL AND [RetainUntilUtc] IS NULL) OR ([StoragePath] IS NOT NULL AND [StorageProvider] IS NOT NULL AND [FileSize] > 0 AND [RetainUntilUtc] IS NOT NULL)");
            });
        });

        // Approved templates are retained as versioned control definitions. The filtered unique
        // index permits many historical/draft rows while guaranteeing that a tenant cannot have
        // two simultaneously active templates for the same close type.
        builder.Entity<FinanceCloseTemplate>(entity =>
        {
            entity.ToTable("FinanceCloseTemplates");
            entity.HasIndex(e => new { e.TenantId, e.TemplateCode, e.Version }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.CloseType, e.IsActive })
                .IsUnique()
                .HasFilter("[IsActive] = 1 AND [IsDeleted] = 0");
            entity.Property(e => e.TemplateCode).HasMaxLength(40).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
            entity.Property(e => e.CloseType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinanceCloseTemplateTaskDefinition>(entity =>
        {
            entity.ToTable("FinanceCloseTemplateTaskDefinitions");
            entity.HasIndex(e => new { e.TenantId, e.FinanceCloseTemplateId, e.TaskCode })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(e => new { e.TenantId, e.FinanceCloseTemplateId, e.Sequence })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.Property(e => e.TaskCode).HasMaxLength(60).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(50).IsRequired();
            entity.HasOne(e => e.FinanceCloseTemplate)
                .WithMany(e => e.TaskDefinitions)
                .HasForeignKey(e => e.FinanceCloseTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Close-cycle evidence is configured beside FiscalPeriod because it is the durable
        // control envelope around that existing aggregate. Cascades are limited to cycle-owned
        // task/check/certificate rows; deleting a period remains restricted elsewhere.
        builder.Entity<FinanceCloseCycle>(entity =>
        {
            entity.ToTable("FinanceCloseCycles");
            entity.HasIndex(e => new { e.TenantId, e.FiscalPeriodId, e.CycleNumber }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.Status, e.StartedAt });
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
            entity.Property(e => e.TemplateCode).HasMaxLength(40).IsRequired();
            entity.Property(e => e.CloseType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.StartedByUserName).HasMaxLength(200);
            entity.Property(e => e.ReopenReason).HasMaxLength(1000);
            entity.HasOne(e => e.FiscalPeriod)
                .WithMany()
                .HasForeignKey(e => e.FiscalPeriodId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FinanceCloseTemplate)
                .WithMany(e => e.CloseCycles)
                .HasForeignKey(e => e.FinanceCloseTemplateId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinanceCloseTask>(entity =>
        {
            entity.ToTable("FinanceCloseTasks");
            entity.HasIndex(e => new { e.TenantId, e.FinanceCloseCycleId, e.TaskCode }).IsUnique();
            entity.Property(e => e.TaskCode).HasMaxLength(60).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
            entity.Property(e => e.AssignedToUserName).HasMaxLength(200);
            entity.HasOne(e => e.FinanceCloseCycle)
                .WithMany(e => e.Tasks)
                .HasForeignKey(e => e.FinanceCloseCycleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinanceCloseCheckSnapshot>(entity =>
        {
            entity.ToTable("FinanceCloseCheckSnapshots");
            entity.HasIndex(e => new { e.TenantId, e.FinanceCloseCycleId, e.EvaluationNumber, e.CheckCode }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.Status, e.EvaluatedAt });
            entity.Property(e => e.CheckCode).HasMaxLength(60).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Severity).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
            entity.Property(e => e.ResultSummary).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.EvidenceFingerprint).HasMaxLength(64);
            entity.HasOne(e => e.FinanceCloseCycle)
                .WithMany(e => e.CheckSnapshots)
                .HasForeignKey(e => e.FinanceCloseCycleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinanceCloseCertification>(entity =>
        {
            entity.ToTable("FinanceCloseCertifications");
            entity.HasIndex(e => new { e.TenantId, e.FinanceCloseCycleId }).IsUnique();
            entity.Property(e => e.PreparedByUserName).HasMaxLength(200);
            entity.Property(e => e.ReviewedByUserName).HasMaxLength(200);
            entity.Property(e => e.ApprovedByUserName).HasMaxLength(200);
            entity.HasOne(e => e.FinanceCloseCycle)
                .WithMany(e => e.Certifications)
                .HasForeignKey(e => e.FinanceCloseCycleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinanceCloseEvidenceAttachment>(entity =>
        {
            entity.ToTable("FinanceCloseEvidenceAttachments");
            entity.HasIndex(e => new { e.TenantId, e.FinanceCloseTaskId, e.FileUploadRecordId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(e => new { e.TenantId, e.FinanceCloseCycleId, e.CreatedAt });
            entity.Property(e => e.EvidenceType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            // The task is the aggregate owner. The direct cycle key is retained for efficient
            // evidence-pack queries, but Restrict avoids two SQL cascade paths from the cycle.
            entity.HasOne(e => e.FinanceCloseCycle)
                .WithMany(e => e.EvidenceAttachments)
                .HasForeignKey(e => e.FinanceCloseCycleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FinanceCloseTask)
                .WithMany(e => e.EvidenceAttachments)
                .HasForeignKey(e => e.FinanceCloseTaskId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.FileUploadRecord)
                .WithMany()
                .HasForeignKey(e => e.FileUploadRecordId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

          builder.Entity<FinanceCloseExceptionWaiver>(entity =>
          {
            entity.ToTable("FinanceCloseExceptionWaivers");
            entity.HasIndex(e => new { e.TenantId, e.FinanceCloseCycleId, e.CheckCode, e.Status });
            entity.HasIndex(e => new { e.TenantId, e.FinanceCloseCheckSnapshotId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.Property(e => e.CheckCode).HasMaxLength(60).IsRequired();
            entity.Property(e => e.EvidenceFingerprint).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Justification).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.RequestedByUserName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ReviewedByUserName).HasMaxLength(200);
            entity.Property(e => e.ReviewComment).HasMaxLength(2000);
            // Waiver evidence is immutable once requested. Restrict relationships make accidental
            // hard deletion fail instead of erasing the approval chain from a historical close.
            entity.HasOne(e => e.FinanceCloseCycle)
                .WithMany(e => e.ExceptionWaivers)
                .HasForeignKey(e => e.FinanceCloseCycleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FinanceCloseTask)
                .WithMany()
                .HasForeignKey(e => e.FinanceCloseTaskId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FinanceCloseCheckSnapshot)
                .WithMany()
                .HasForeignKey(e => e.FinanceCloseCheckSnapshotId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FinanceCloseEvidenceAttachment)
                .WithMany(e => e.ExceptionWaivers)
                .HasForeignKey(e => e.FinanceCloseEvidenceAttachmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Tenant)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                  .OnDelete(DeleteBehavior.Restrict);
          });

          builder.Entity<FinanceCloseAlertDelivery>(entity =>
          {
              entity.ToTable("FinanceCloseAlertDeliveries");
              // The same control stage may legitimately notify several users, but each recipient
              // receives it once. Soft-deleted audit rows are excluded so an administrator can
              // explicitly retire a bad delivery record and allow a controlled retry if needed.
              entity.HasIndex(e => new { e.TenantId, e.DedupeKey })
                  .IsUnique()
                  .HasFilter("[IsDeleted] = 0");
              entity.HasIndex(e => new { e.TenantId, e.FinanceCloseCycleId, e.CreatedAt });
              entity.HasIndex(e => new { e.Status, e.LastAttemptAtUtc });
              entity.Property(e => e.AlertType).HasMaxLength(60).IsRequired();
              entity.Property(e => e.DedupeKey).HasMaxLength(240).IsRequired();
              entity.Property(e => e.RecipientUserName).HasMaxLength(200).IsRequired();
              entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
              entity.Property(e => e.LastError).HasMaxLength(2000);
              // Cycle is the alert aggregate owner. Optional task and waiver links use Restrict
              // to avoid SQL Server multiple-cascade paths and to protect retained escalation
              // evidence from an accidental child-row deletion.
              entity.HasOne(e => e.FinanceCloseCycle)
                  .WithMany(e => e.AlertDeliveries)
                  .HasForeignKey(e => e.FinanceCloseCycleId)
                  .OnDelete(DeleteBehavior.Cascade);
              entity.HasOne(e => e.FinanceCloseTask)
                  .WithMany()
                  .HasForeignKey(e => e.FinanceCloseTaskId)
                  .OnDelete(DeleteBehavior.Restrict);
              entity.HasOne(e => e.FinanceCloseExceptionWaiver)
                  .WithMany()
                  .HasForeignKey(e => e.FinanceCloseExceptionWaiverId)
                  .OnDelete(DeleteBehavior.Restrict);
              entity.HasOne(e => e.FinancePeriodReopenRequest)
                  .WithMany(e => e.AlertDeliveries)
                  .HasForeignKey(e => e.FinancePeriodReopenRequestId)
                  .OnDelete(DeleteBehavior.Restrict);
              entity.HasOne(e => e.Tenant)
                  .WithMany()
                  .HasForeignKey(e => e.TenantId)
                  .OnDelete(DeleteBehavior.Restrict);
          });

          builder.Entity<FinancePeriodReopenRequest>(entity =>
          {
              entity.ToTable("FinancePeriodReopenRequests");
              // Only one live request may exist for a period. The filtered unique index protects
              // the invariant even when two API nodes receive requests concurrently.
              entity.HasIndex(e => new { e.TenantId, e.FiscalPeriodId })
                  .IsUnique()
                  .HasFilter("[IsDeleted] = 0 AND [Status] = 'PendingApproval'");
              entity.HasIndex(e => new { e.TenantId, e.Status, e.RequestedAt });
              entity.Property(e => e.Status).HasMaxLength(30).IsRequired();
              entity.Property(e => e.Reason).HasMaxLength(1000).IsRequired();
              entity.Property(e => e.AffectedPeriodAssessment).HasMaxLength(2000).IsRequired();
              entity.Property(e => e.ImpactSnapshotJson).IsRequired();
              entity.Property(e => e.ImpactFingerprint).HasMaxLength(64).IsRequired();
              entity.Property(e => e.RequestedByUserName).HasMaxLength(200).IsRequired();
              entity.Property(e => e.ReviewedByUserName).HasMaxLength(200);
              entity.Property(e => e.ReviewComment).HasMaxLength(2000);
              entity.HasOne(e => e.FiscalPeriod)
                  .WithMany()
                  .HasForeignKey(e => e.FiscalPeriodId)
                  .OnDelete(DeleteBehavior.Restrict);
              entity.HasOne(e => e.FinanceCloseCycle)
                  .WithMany(e => e.ReopenRequests)
                  .HasForeignKey(e => e.FinanceCloseCycleId)
                  .OnDelete(DeleteBehavior.Restrict);
              // The resulting cycle is optional until approval and is evidence rather than an
              // aggregate owner; restricting deletion protects the complete reopen/reclose chain.
              entity.HasOne(e => e.ResultingFinanceCloseCycle)
                  .WithMany()
                  .HasForeignKey(e => e.ResultingFinanceCloseCycleId)
                  .OnDelete(DeleteBehavior.Restrict);
              entity.HasOne(e => e.Tenant)
                  .WithMany()
                  .HasForeignKey(e => e.TenantId)
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
        ConfigureProcurementCentralDocumentLinks<TenderAwardVerificationItemDocument>(builder);
        builder.ApplyConfiguration(new ProcurementAwardReadinessDecisionConfiguration());
        builder.ApplyConfiguration(new ProcurementBidderCommunicationRegisterConfiguration());
        builder.ApplyConfiguration(new ProcurementBidderCommunicationRecipientConfiguration());
        builder.ApplyConfiguration(new ProcurementBidderCommunicationLetterVersionConfiguration());
        builder.ApplyConfiguration(new ProcurementBidderCommunicationDispatchConfiguration());
        builder.ApplyConfiguration(new ProcurementBidderCommunicationDeliveryConfiguration());
        builder.ApplyConfiguration(new ProcurementBidderCommunicationAcknowledgementConfiguration());
        builder.ApplyConfiguration(new ProcurementBidderAppealConfiguration());
        builder.ApplyConfiguration(new ProcurementBidderAppealDecisionConfiguration());
        builder.ApplyConfiguration(new ProcurementTenderSecurityInstrumentConfiguration());
        builder.ApplyConfiguration(new ProcurementTenderSecurityActionConfiguration());
        builder.ApplyConfiguration(new ProcurementGhanepsExchangeEventConfiguration());
        builder.ApplyConfiguration(new ProcurementGhanepsExchangePayloadConfiguration());
        builder.ApplyConfiguration(new ProcurementGhanepsExchangeAttemptConfiguration());
        builder.ApplyConfiguration(new ProcurementGhanepsExchangeAcknowledgementConfiguration());
        builder.ApplyConfiguration(new ProcurementGhanepsExchangeReconciliationConfiguration());

        // Contract Management configurations
        builder.ApplyConfiguration(new ContractConfiguration());
        builder.ApplyConfiguration(new ContractMilestoneConfiguration());
        builder.ApplyConfiguration(new ContractAmendmentConfiguration());
        builder.ApplyConfiguration(new ContractDocumentConfiguration());
        builder.ApplyConfiguration(new ProcurementContractActivationConfiguration());
        builder.ApplyConfiguration(new ProcurementContractActivationEvidenceConfiguration());
        builder.ApplyConfiguration(new ProcurementWorksCloseoutActionConfiguration());
        builder.ApplyConfiguration(new ProcurementWorksCloseoutEvidenceConfiguration());
        builder.ApplyConfiguration(new ProcurementReceiptInspectionCaseConfiguration());
        builder.ApplyConfiguration(new ProcurementReceiptInspectionLineConfiguration());
        builder.ApplyConfiguration(new ProcurementReceiptInspectionEvidenceConfiguration());
        builder.ApplyConfiguration(new ProcurementReceiptInspectionActionConfiguration());
        builder.ApplyConfiguration(new ProcurementReceiptDocumentConfiguration());
        builder.ApplyConfiguration(new ProcurementReceiptDocumentSignatureConfiguration());
        builder.ApplyConfiguration(new ProcurementReceiptDocumentActionConfiguration());
        builder.ApplyConfiguration(new ProcurementReceiptSourceEvidenceConfiguration());
        builder.ApplyConfiguration(new VendorInvoiceMatchExceptionConfiguration());
        builder.ApplyConfiguration(new VendorInvoiceMatchExceptionVarianceConfiguration());
        builder.ApplyConfiguration(new VendorInvoiceMatchExceptionEvidenceConfiguration());
        builder.ApplyConfiguration(new VendorInvoiceMatchExceptionActionConfiguration());

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
        builder.ApplyConfiguration(new ProjectFinalAccountRevisionConfiguration());
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
            if (this.Database.IsSqlServer())
                entity.ToTable("Tenants", table => table.HasCheckConstraint(
                    "CK_Tenants_BaseCurrencyCanonical_C3",
                    "[IsDeleted] = 1 OR (DATALENGTH([BaseCurrency]) = 6 AND [BaseCurrency] COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z][A-Z][A-Z]')"));
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
            entity.ToTable("AuditLogs", table => table.HasTrigger("TR_AuditLogs_AppendOnly"));
            entity.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(a => a.Tenant).WithMany().HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(a => a.Timestamp);
            entity.HasIndex(a => a.UserId);
            entity.HasIndex(a => a.Resource);
            entity.Property(a => a.IdempotencyKey).HasMaxLength(450);
            entity.HasIndex(a => new { a.TenantId, a.IdempotencyKey })
                .IsUnique()
                .HasFilter("[IdempotencyKey] IS NOT NULL");
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
            entity.ToTable("FileUploadRecords", table =>
                table.HasTrigger(
                    "TR_FileUploadRecords_RegistrationEvidenceDeleteGuard"));
            entity.HasIndex(x => new { x.TenantId, x.Category });
            entity.HasIndex(x => new { x.TenantId, x.CreatedAt });
            entity.HasIndex(x => new { x.TenantId, x.FilePath }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.UploadedByUserId });
            entity.HasIndex(x => new
            {
                x.IsDeleted,
                x.StorageDeletedAtUtc,
                x.StorageDeleteNextAttemptAtUtc
            });
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
            entity.HasOne(s => s.ReportTemplate).WithMany().HasForeignKey(s => s.ReportTemplateId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(s => s.RunAsUser).WithMany().HasForeignKey(s => s.RunAsUserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(s => s.PausedBy).WithMany().HasForeignKey(s => s.PausedById).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(s => s.Tenant).WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.NoAction);
            entity.Property(s => s.RowVersion).IsRowVersion();
            entity.HasIndex(s => s.ReportId);
            entity.HasIndex(s => s.ReportTemplateId);
            entity.HasIndex(s => s.IsActive);
            entity.HasIndex(s => s.NextExecutionDate);
            entity.HasIndex(s => s.Frequency);
            // The production worker scans by tenant/state/due date; this composite
            // index avoids a table scan as schedules accumulate across tenants.
            entity.HasIndex(s => new { s.IsActive, s.Status, s.NextExecutionDate });
        });

        // Configure ReportTemplate entity
        builder.Entity<ReportTemplate>(entity =>
        {
            entity.ToTable("ReportTemplates");
            entity.HasOne(t => t.Tenant).WithMany().HasForeignKey(t => t.TenantId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(t => t.Report).WithMany().HasForeignKey(t => t.ReportId).OnDelete(DeleteBehavior.NoAction);
            entity.Property(t => t.RowVersion).IsRowVersion();
            entity.HasIndex(t => t.TenantId);
            entity.HasIndex(t => t.ReportId);
            entity.HasIndex(t => new { t.TenantId, t.TemplateKey, t.Version }).IsUnique();
            entity.HasIndex(t => t.Category);
            entity.HasIndex(t => t.Type);
            entity.HasIndex(t => new { t.TenantId, t.Status, t.Audience, t.Cadence });
            entity.HasIndex(t => t.UsageCount);
        });

        // Configure ReportExecution entity
        builder.Entity<ReportExecution>(entity =>
        {
            entity.ToTable("ReportExecutions");
            entity.HasOne(e => e.Report).WithMany(r => r.Executions).HasForeignKey(e => e.ReportId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.ReportSchedule).WithMany().HasForeignKey(e => e.ReportScheduleId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.ReportTemplate).WithMany().HasForeignKey(e => e.ReportTemplateId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.ReportExport).WithMany().HasForeignKey(e => e.ReportExportId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.NoAction);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => e.ReportId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ExecutedAt);
            entity.HasIndex(e => e.Status);
            // This filtered key is the final idempotency gate when multiple API
            // instances observe the same due schedule at the same time.
            entity.HasIndex(e => new { e.TenantId, e.ReportScheduleId, e.ScheduledFor })
                .IsUnique()
                .HasFilter("[ReportScheduleId] IS NOT NULL AND [ScheduledFor] IS NOT NULL AND [IsDeleted] = 0");
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
            entity.HasIndex(e => new { e.TenantId, e.Status, e.ExportedAt });
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

        builder.Entity<FinanceAdHocReportDefinition>(entity =>
        {
            entity.ToTable("FinanceAdHocReportDefinitions");
            entity.HasOne(item => item.Report).WithMany().HasForeignKey(item => item.ReportId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.OwnerUser).WithMany().HasForeignKey(item => item.OwnerUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.Property(item => item.RowVersion).IsRowVersion();
            // One shared Report row has exactly one governed Finance builder definition.
            entity.HasIndex(item => item.ReportId).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.OwnerUserId, item.Visibility });
            entity.HasIndex(item => new { item.TenantId, item.DatasetCode });
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
        ConfigureQuantitySurveyRateDecimalPrecision(builder);

        // Retained receipt/return evidence must preserve the source conversion and rate,
        // and fractional provenance needs more precision than ordinary stock quantities.
        // Keep these overrides local to the new evidence model; historical tables retain
        // their existing global precision convention.
        builder.Entity<ProcurementReceiptCostBasis>(entity =>
        {
            entity.Property(e => e.ConversionToBase).HasColumnType("decimal(18,8)");
            entity.Property(e => e.ExchangeRateToFunctional).HasColumnType("decimal(18,6)");
            entity.Property(e => e.PurchaseUnitCost).HasColumnType("decimal(18,6)");
        });
        builder.Entity<InventorySupplierReturnAllocation>()
            .Property(e => e.ConversionToBase).HasColumnType("decimal(18,8)");
        builder.Entity<VendorInvoiceReceiptCostAllocation>(entity =>
        {
            entity.Property(e => e.InvoiceExchangeRateToFunctional).HasColumnType("decimal(18,6)");
            entity.Property(e => e.RevaluedReceiptBaseQuantity).HasColumnType("decimal(28,12)");
        });
        builder.Entity<VendorInvoiceReceiptCostValuation>(entity =>
        {
            entity.Property(e => e.AttributedReceiptBaseQuantity).HasColumnType("decimal(28,12)");
            entity.Property(e => e.ValueChange).HasColumnType("decimal(18,2)");
        });

        // The legacy global precision pass above intentionally normalizes most decimals to four
        // places, but Finance FX evidence requires the approved six-place quote and an eight-place
        // derived cross-rate. Re-apply these narrow overrides after that pass so the database model
        // matches the immutable rate snapshots documented on the affected Finance records.
        builder.Entity<CashTransaction>(entity =>
        {
            entity.Property(e => e.ExchangeRate).HasColumnType("decimal(18,6)");
            entity.Property(e => e.TransferCrossRate).HasColumnType("decimal(18,8)");
        });
        builder.Entity<SupplierDebitNote>(entity =>
            entity.Property(e => e.ExchangeRate).HasColumnType("decimal(18,6)"));
        builder.Entity<SupplierDebitNoteApplication>(entity =>
            entity.Property(e => e.ExchangeRate).HasColumnType("decimal(18,6)"));
        builder.Entity<FinanceSettlementDimensionComponent>(entity =>
        {
            entity.Property(e => e.ExchangeRate).HasColumnType("decimal(18,6)");
            entity.Property(e => e.ComparisonExchangeRate).HasColumnType("decimal(18,6)");
        });
        ConfigureSalesAllocationPrecision(builder);
        builder.ApplyConfiguration(new AccountBalanceConfiguration());
        builder.ApplyConfiguration(new AccountCurrencyExposureConfiguration());
        builder.Entity<FinanceBalanceRebuildRun>(entity =>
        {
            entity.ToTable("FinanceBalanceRebuildRuns");
            entity.HasIndex(item => new { item.TenantId, item.AccountingBookId, item.IdempotencyKey })
                .IsUnique().HasFilter("[IsDeleted] = 0");
            entity.Property(item => item.AccountingBookCode).HasMaxLength(20).IsRequired();
            entity.Property(item => item.IdempotencyKey).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.SourceFingerprint).HasMaxLength(64).IsRequired();
            entity.Property(item => item.CommandFingerprint).HasMaxLength(64).IsRequired();
            entity.Property(item => item.AbsoluteDrift).HasColumnType("decimal(18,2)");
            entity.HasOne(item => item.AccountingBook).WithMany()
                .HasForeignKey(item => new { item.TenantId, item.AccountingBookId })
                .HasPrincipalKey(book => new { book.TenantId, book.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure all tenant relationships to avoid cascade conflicts
        ConfigureGlobalTenantRelationships(builder);

        // Shared cross-module reference data (administrative geography) — see
        // docs/GEOGRAPHY-REFERENCE-DESIGN.md. Configured before HR because HR consumes it, not the
        // other way round.
        ConfigureReferenceModule(builder);

        // Configure HR Management entities
        // [HR-MODULE-PORT] All HR entity configuration lives in ApplicationDbContext.HR.cs.
        ConfigureHrModule(builder);
        // RHEMA-specific HR deltas kept on top of the ported model (Division / Unit / WorkStation).
        ConfigureRhemaHrDeltas(builder);
        // Oracle payroll entities developed on master alongside the HR port. The ported HR
        // configuration in ApplicationDbContext.HR.cs does not cover the Payroll* entities,
        // so this call must be kept — dropping it leaves all 39 payroll entities unconfigured.
        ConfigurePayrollEntities(builder);

        // Configure Maintenance Management entities
        ConfigureMaintenanceEntities(builder);

        // Configure Inventory Management entities
        ConfigureInventoryEntities(builder);
        // These controlled voucher costs require four-decimal source precision.
        // Keep this after the global decimal convention so later migrations do
        // not silently narrow the TDC-0606/TDC-0607 audit values.
        builder.Entity<InventoryIssueVoucherLine>().Property(item => item.UnitCost).HasColumnType("decimal(18,4)");
        builder.Entity<InventoryReturnVoucherLine>().Property(item => item.UnitCost).HasColumnType("decimal(18,4)");
        builder.Entity<GoodsReceiptNoteItem>().Property(item => item.UnitCost).HasColumnType("decimal(18,4)");
        builder.Entity<StockAdjustmentItem>().Property(item => item.UnitCost).HasColumnType("decimal(18,4)");

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
        ConfigureFinanceCommonEntities(builder, Database.IsSqlServer());

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
            entity.HasIndex(e => new { e.TenantId, e.PayrollRunId, e.SequenceNo })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
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
            entity.ToTable("WorkOrderParts", table =>
                table.HasTrigger("TR_WorkOrderParts_ReservationLineageGuard"));
            entity.HasIndex(wop => wop.WorkOrderId);
            entity.HasIndex(wop => wop.ItemCode);
            entity.HasIndex(wop => wop.Status);
            entity.HasIndex(wop => wop.InventoryItemId);
            entity.HasIndex(wop => new { wop.TenantId, wop.AllocationId }).IsUnique()
                .HasFilter("[AllocationId] IS NOT NULL AND [IsDeleted] = 0");

            entity.HasOne(wop => wop.WorkOrder)
                .WithMany(wo => wo.Parts)
                .HasForeignKey(wop => wop.WorkOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(wop => wop.Allocation)
                .WithMany()
                .HasForeignKey(wop => wop.AllocationId)
                .OnDelete(DeleteBehavior.Restrict);
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

    /// <summary>
    /// [HR-MODULE-PORT] Configuration for the RHEMA-specific fields kept on <see cref="Employee"/> on
    /// top of HRApi's model (`DivisionId`, `UnitId`, `StationId`).
    ///
    /// These MUST be configured explicitly: HRApi's <c>WorkStation</c> already has a
    /// <c>ContactPerson</c> navigation to <c>Employee</c>, so adding <c>Employee.Station</c> makes EF
    /// pair the two navigations into a one-to-one relationship and fail with
    /// "the dependent side could not be determined". Declaring them as many-to-one here keeps the two
    /// relationships independent (many employees may sit at one work station).
    ///
    /// Kept in this file (not the generated ApplicationDbContext.HR.cs) so an HR re-sync cannot wipe it.
    /// </summary>
    private static void ConfigureRhemaHrDeltas(ModelBuilder builder)
    {
        builder.Entity<Employee>(entity =>
        {
            entity.HasOne(e => e.Division)
                .WithMany()
                .HasForeignKey(e => e.DivisionId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Unit)
                .WithMany()
                .HasForeignKey(e => e.UnitId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Station)
                .WithMany()
                .HasForeignKey(e => e.StationId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }


    private static void ConfigureBusinessPartnerEntities(ModelBuilder builder)
    {
        // Configure BusinessPartner entity
        builder.Entity<BusinessPartner>(entity =>
        {
            entity.ToTable("BusinessPartners", table =>
            {
                table.HasCheckConstraint(
                    "CK_BusinessPartners_BeneficialOwnershipJson",
                    "[BeneficialOwnershipJson] IS NULL OR ISJSON([BeneficialOwnershipJson]) = 1");
                table.HasCheckConstraint(
                    "CK_BusinessPartners_CompliancePeriod",
                    "[ComplianceReviewDateUtc] IS NULL OR [ComplianceValidUntilUtc] IS NULL OR [ComplianceValidUntilUtc] >= [ComplianceReviewDateUtc]");
                table.HasCheckConstraint(
                    "CK_BusinessPartners_BlacklistEvidence",
                    "[IsBlacklisted] = 0 OR (NULLIF(LTRIM(RTRIM([BlacklistReason])), '') IS NOT NULL AND [BlacklistDate] IS NOT NULL)");
            });

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
            entity.HasIndex(pc => new { pc.TenantId, pc.CategoryCode }).IsUnique();
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

        builder.Entity<BusinessPartnerBankAccount>(entity =>
        {
            entity.ToTable("BusinessPartnerBankAccounts");
            entity.HasIndex(account => new { account.TenantId, account.BusinessPartnerId });
            entity.HasIndex(account => new { account.TenantId, account.BusinessPartnerId, account.IsPrimary })
                .IsUnique()
                .HasFilter("[IsPrimary] = 1 AND [IsDeleted] = 0");

            entity.HasOne(account => account.BusinessPartner)
                .WithMany(partner => partner.BankAccounts)
                .HasForeignKey(account => account.BusinessPartnerId)
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
            entity.ToTable("BusinessPartnerRegistrationDocuments", table =>
                table.HasTrigger("TR_BusinessPartnerRegistrationDocuments_ControlledFileGuard"));
            entity.HasIndex(bprd => bprd.RegistrationId);
            entity.HasIndex(bprd => new { bprd.TenantId, bprd.FileUploadRecordId });
            entity.HasIndex(bprd => new { bprd.TenantId, bprd.CentralDocumentRecordId });
            entity.HasIndex(bprd => new { bprd.TenantId, bprd.CentralDocumentVersionId });
            entity.HasIndex(bprd => bprd.DocumentType);
            entity.HasIndex(bprd => bprd.IsVerified);

            entity.HasOne(bprd => bprd.Registration)
                .WithMany(r => r.Documents)
                .HasForeignKey(bprd => bprd.RegistrationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(bprd => bprd.FileUploadRecord)
                .WithMany()
                .HasForeignKey(bprd => bprd.FileUploadRecordId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<CentralDocumentRecord>()
                .WithMany()
                .HasForeignKey(bprd => bprd.CentralDocumentRecordId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<CentralDocumentVersion>()
                .WithMany()
                .HasForeignKey(bprd => bprd.CentralDocumentVersionId)
                .OnDelete(DeleteBehavior.Restrict);

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

    private static void ConfigureBudgeting(ModelBuilder builder)
    {
        builder.Entity<BudgetScenario>(entity =>
        {
            entity.Property(scenario => scenario.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(scenario => new { scenario.TenantId, scenario.FiscalYearId, scenario.Name })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(scenario => new { scenario.TenantId, scenario.FiscalYearId, scenario.IsActive })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [IsActive] = 1");
            entity.HasOne(scenario => scenario.LockedByUser)
                .WithMany()
                .HasForeignKey(scenario => scenario.LockedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(scenario => scenario.AdoptedByUser)
                .WithMany()
                .HasForeignKey(scenario => scenario.AdoptedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(scenario => scenario.SupersededByUser)
                .WithMany()
                .HasForeignKey(scenario => scenario.SupersededByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(scenario => scenario.ParentScenario)
                .WithMany()
                .HasForeignKey(scenario => scenario.ParentScenarioId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<BudgetScenarioControlDimension>(entity =>
        {
            entity.ToTable("BudgetScenarioControlDimensions");
            entity.HasIndex(item => new
                {
                    item.TenantId,
                    item.BudgetScenarioId,
                    item.FinanceDimensionDefinitionId
                })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasOne(item => item.BudgetScenario)
                .WithMany(scenario => scenario.ControlDimensions)
                .HasForeignKey(item => item.BudgetScenarioId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.FinanceDimensionDefinition)
                .WithMany()
                .HasForeignKey(item => item.FinanceDimensionDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<BudgetReturn>(entity =>
        {
            entity.Property(budgetReturn => budgetReturn.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(budgetReturn => new
                {
                    budgetReturn.TenantId,
                    budgetReturn.BudgetScenarioId,
                    budgetReturn.SegmentValueId
                })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasOne(budgetReturn => budgetReturn.AssignedToUser)
                .WithMany()
                .HasForeignKey(budgetReturn => budgetReturn.AssignedToUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(budgetReturn => budgetReturn.ApproverUser)
                .WithMany()
                .HasForeignKey(budgetReturn => budgetReturn.ApproverUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<BudgetEntry>(entity =>
        {
            entity.Property(entry => entry.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(entry => new
                {
                    entry.TenantId,
                    entry.BudgetReturnId,
                    entry.AccountId,
                    entry.FiscalPeriodId,
                    entry.FinanceDimensionSetId
                })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasOne(entry => entry.FinanceDimensionSet)
                .WithMany()
                .HasForeignKey(entry => entry.FinanceDimensionSetId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinanceBudgetReservation>(entity =>
        {
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_FinanceBudgetReservations_TransactionCurrencyCode",
                    "LEN([TransactionCurrencyCode]) = 3");
                table.HasCheckConstraint(
                    "CK_FinanceBudgetReservations_TransactionAmount",
                    "[TransactionAmount] >= 0");
                table.HasCheckConstraint(
                    "CK_FinanceBudgetReservations_ExchangeRate",
                    "[ExchangeRate] > 0");
                table.HasCheckConstraint(
                    "CK_FinanceBudgetReservations_ReservationVersion",
                    "[ReservationVersion] >= 1");
            });
            entity.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(x => new { x.TenantId, x.BudgetEntryId, x.Status });
            entity.HasIndex(x => new { x.TenantId, x.FinanceDimensionSetId, x.Status });
            entity.HasOne(x => x.FinanceDimensionSet)
                .WithMany()
                .HasForeignKey(x => x.FinanceDimensionSetId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.TenantId, x.SourceDocumentType, x.SourceDocumentId, x.BudgetEntryId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [Status] = 'Reserved'");
            entity.HasIndex(x => x.PostingEventId);
        });

        builder.Entity<FinanceBudgetReservationOperation>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.IdempotencyKey })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(x => new { x.TenantId, x.FinanceBudgetReservationId, x.OccurredAt });
            entity.HasIndex(x => x.PostingEventId);
            entity.HasOne(x => x.FinanceBudgetReservation)
                .WithMany()
                .HasForeignKey(x => x.FinanceBudgetReservationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<FinanceBudgetOverrideRequest>(entity =>
        {
            entity.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(x => new { x.TenantId, x.SourceDocumentType, x.SourceDocumentId, x.EvaluationHash, x.Status });
            entity.HasIndex(x => x.WorkflowInstanceId)
                .IsUnique()
                .HasFilter("[WorkflowInstanceId] IS NOT NULL");
        });

        builder.Entity<BudgetRevision>(entity =>
        {
            entity.Property(revision => revision.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(revision => new { revision.TenantId, revision.RevisionNumber })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(revision => new { revision.TenantId, revision.SourceScenarioId, revision.Status });
            entity.HasOne(revision => revision.SourceScenario)
                .WithMany()
                .HasForeignKey(revision => revision.SourceScenarioId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(revision => revision.ResultScenario)
                .WithMany()
                .HasForeignKey(revision => revision.ResultScenarioId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<BudgetRevisionLine>(entity =>
        {
            // One signed adjustment per budget cell avoids ambiguous duplicate
            // changes and makes the net-zero virement rule deterministic.
            entity.HasIndex(line => new
                {
                    line.TenantId,
                    line.BudgetRevisionId,
                    line.SegmentValueId,
                    line.AccountId,
                    line.FiscalPeriodId,
                    line.FinanceDimensionSetId
                })
                .IsUnique()
                .HasDatabaseName("UX_BudgetRevisionLines_Cell")
                .HasFilter("[IsDeleted] = 0");
            entity.HasOne(line => line.BudgetRevision)
                .WithMany(revision => revision.Lines)
                .HasForeignKey(line => line.BudgetRevisionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(line => line.FinanceDimensionSet)
                .WithMany()
                .HasForeignKey(line => line.FinanceDimensionSetId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureBankingSettlement(ModelBuilder builder)
    {
        builder.Entity<LiquidityAccount>(entity =>
        {
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.Code })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.AccountType, item.Currency, item.IsActive });
            entity.HasIndex(item => new { item.TenantId, item.BankAccountId })
                .IsUnique()
                .HasFilter("[BankAccountId] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasOne(item => item.GLAccount)
                .WithMany()
                .HasForeignKey(item => item.GLAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.BankAccount)
                .WithMany()
                .HasForeignKey(item => item.BankAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<LiquidityAccountEntry>(entity =>
        {
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.EntryNumber })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(item => new
            {
                item.TenantId,
                item.LiquidityAccountId,
                item.Currency,
                item.EntryDate,
                item.IsReversed
            });
            entity.HasIndex(item => new { item.TenantId, item.SourceDocumentType, item.SourceDocumentId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasOne(item => item.LiquidityAccount)
                .WithMany(account => account.Entries)
                .HasForeignKey(item => item.LiquidityAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LiquidityAccountEntry>()
                .WithMany()
                .HasForeignKey(item => item.ReversalOfEntryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CashierTillSession>(entity =>
        {
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.SessionNumber })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.LiquidityAccountId, item.Status, item.OpenedAt });
            // Service-level serializable locking protects the normal command path; this filtered
            // unique index is the final database guard against two active custody windows for one
            // physical till if commands race or data is loaded outside the application service.
            entity.HasIndex(item => new { item.TenantId, item.LiquidityAccountId })
                .HasDatabaseName("UX_CashierTillSessions_ActiveTill")
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [Status] IN (1, 2)");
            entity.HasIndex(item => new { item.TenantId, item.CashierUserId, item.Status });
            entity.HasOne(item => item.LiquidityAccount)
                .WithMany()
                .HasForeignKey(item => item.LiquidityAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.OpeningEvidenceFile)
                .WithMany()
                .HasForeignKey(item => item.OpeningEvidenceFileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ClosingEvidenceFile)
                .WithMany()
                .HasForeignKey(item => item.ClosingEvidenceFileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CorrectsSession)
                .WithMany()
                .HasForeignKey(item => item.CorrectsSessionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CashierTillCountLine>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.CashierTillSessionId, item.Denomination })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasOne(item => item.CashierTillSession)
                .WithMany(session => session.CountLines)
                .HasForeignKey(item => item.CashierTillSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BankDepositBatch>(entity =>
        {
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.DepositNumber })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.BankAccountId, item.DepositReference })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.Status, item.DepositDate });
            entity.HasIndex(item => new { item.TenantId, item.ConfirmationStatus, item.BankConfirmationDate });
            // Bank acknowledgement references are controlled per destination account. The
            // filtered index permits pre-confirmation nulls while preventing duplicate advice or
            // stamped-slip references from being attached to two active deposits.
            entity.HasIndex(item => new { item.TenantId, item.BankAccountId, item.BankConfirmationReference })
                .IsUnique()
                .HasFilter("[BankConfirmationReference] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.WorkflowInstanceId });
            entity.HasOne(item => item.BankAccount)
                .WithMany()
                .HasForeignKey(item => item.BankAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.JournalEntry)
                .WithMany()
                .HasForeignKey(item => item.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CashTransaction)
                .WithMany()
                .HasForeignKey(item => item.CashTransactionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.BankConfirmationEvidenceFile)
                .WithMany()
                .HasForeignKey(item => item.BankConfirmationEvidenceFileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ReversalJournalEntry)
                .WithMany()
                .HasForeignKey(item => item.ReversalJournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<BankDepositAllocation>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.BankDepositBatchId, item.LiquidityAccountEntryId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasOne(item => item.BankDepositBatch)
                .WithMany(batch => batch.Allocations)
                .HasForeignKey(item => item.BankDepositBatchId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.LiquidityAccountEntry)
                .WithMany(entry => entry.DepositAllocations)
                .HasForeignKey(item => item.LiquidityAccountEntryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<BankDepositAttachment>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.BankDepositBatchId, item.FileUploadRecordId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasOne(item => item.BankDepositBatch)
                .WithMany(batch => batch.Attachments)
                .HasForeignKey(item => item.BankDepositBatchId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.FileUploadRecord)
                .WithMany()
                .HasForeignKey(item => item.FileUploadRecordId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReturnedChequeCase>(entity =>
        {
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.CaseNumber })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.CustomerPaymentId, item.Status });
            entity.HasIndex(item => new { item.TenantId, item.WorkflowInstanceId });
            entity.HasOne(item => item.CustomerPayment)
                .WithMany()
                .HasForeignKey(item => item.CustomerPaymentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.BankDepositBatch)
                .WithMany()
                .HasForeignKey(item => item.BankDepositBatchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.BankAccount)
                .WithMany()
                .HasForeignKey(item => item.BankAccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.JournalEntry)
                .WithMany()
                .HasForeignKey(item => item.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ReturnCashTransaction)
                .WithMany()
                .HasForeignKey(item => item.ReturnCashTransactionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ChargeCashTransaction)
                .WithMany()
                .HasForeignKey(item => item.ChargeCashTransactionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ReturnedChequeAttachment>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.ReturnedChequeCaseId, item.FileUploadRecordId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasOne(item => item.ReturnedChequeCase)
                .WithMany(item => item.Attachments)
                .HasForeignKey(item => item.ReturnedChequeCaseId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.FileUploadRecord)
                .WithMany()
                .HasForeignKey(item => item.FileUploadRecordId)
                .OnDelete(DeleteBehavior.Restrict);
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
            if (typeof(TenantEntity).IsAssignableFrom(type))
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(SetTenantSoftDeleteFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(type);
                method.Invoke(this, new object[] { builder, entityType });
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
            entity.HasIndex(x => new { x.TenantId, x.RequesterUserId, x.ExternalSubmissionId })
                .IsUnique().HasFilter("[ExternalSubmissionId] IS NOT NULL");
            entity.HasIndex(x => new { x.TenantId, x.TicketNumber }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.Status });
            entity.HasIndex(x => new { x.TenantId, x.RequesterUserId });
            entity.HasIndex(x => new { x.TenantId, x.AssignedToUserId });
            entity.HasIndex(x => new { x.TenantId, x.AssignedDepartmentId });
            entity.HasIndex(x => new { x.TenantId, x.AssignedOrganizationUnitId });
            entity.HasIndex(x => new { x.TenantId, x.RootCauseId });
            entity.HasIndex(x => new { x.TenantId, x.CrmOpportunityId });
            entity.HasIndex(x => new { x.TenantId, x.EstateListingApplicationCaseId });

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

            entity.HasOne(x => x.AssignedOrganizationUnit)
                .WithMany()
                .HasForeignKey(x => x.AssignedOrganizationUnitId)
                .OnDelete(DeleteBehavior.NoAction);


            entity.HasOne(x => x.CrmLead)
                .WithMany()
                .HasForeignKey(x => x.CrmLeadId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.CrmOpportunity)
                .WithMany()
                .HasForeignKey(x => x.CrmOpportunityId)
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

            entity.HasMany(x => x.CrmEngagementLinks)
                .WithOne(l => l.Ticket)
                .HasForeignKey(l => l.TicketId)
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

        builder.Entity<EhcCrmEngagementLink>(entity =>
        {
            entity.HasIndex(x => new { x.TenantId, x.TicketId });
            entity.HasIndex(x => new { x.TenantId, x.CrmActivityId });
            entity.HasIndex(x => new { x.TenantId, x.SourceKey }).IsUnique();

            entity.HasOne(x => x.CrmActivity)
                .WithMany()
                .HasForeignKey(x => x.CrmActivityId)
                .OnDelete(DeleteBehavior.NoAction);
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

    private static void ConfigureHrIdentityReconciliation(ModelBuilder builder)
    {
        builder.Entity<HrIdentityReconciliationState>(entity =>
        {
            entity.Property(item => item.SourceFingerprint).IsUnicode(false).IsFixedLength();
            entity.HasIndex(item => new { item.TenantId, item.UserId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.EmployeeId }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.ReactivationReviewRequired });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Employee>().WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Department>().WithMany().HasForeignKey(item => item.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Employee>().WithMany().HasForeignKey(item => item.ManagerEmployeeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ManagerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<HrIdentityReconciliationRun>().WithMany().HasForeignKey(item => item.LastRunId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasTrigger("TR_HrIdentityReconciliationStates_TenantGuard"));
        });

        builder.Entity<HrIdentityReconciliationRun>(entity =>
        {
            entity.Property(item => item.IdempotencyKey).IsUnicode(false);
            entity.HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.StartedAtUtc });
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.RequestedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_HrIdentityReconciliationRuns_TenantGuard");
                table.HasTrigger("TR_HrIdentityReconciliationRuns_CompletedImmutable");
            });
        });

        builder.Entity<HrIdentityReconciliationItem>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.RunId, item.UserId, item.AttemptNumber }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.Status, item.ProcessedAtUtc });
            entity.HasOne<HrIdentityReconciliationRun>().WithMany().HasForeignKey(item => item.RunId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Employee>().WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Department>().WithMany().HasForeignKey(item => item.PreviousDepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Department>().WithMany().HasForeignKey(item => item.CurrentDepartmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Employee>().WithMany().HasForeignKey(item => item.PreviousManagerEmployeeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Employee>().WithMany().HasForeignKey(item => item.CurrentManagerEmployeeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_HrIdentityReconciliationItems_TenantGuard");
                table.HasTrigger("TR_HrIdentityReconciliationItems_NoMutation");
            });
        });

        builder.Entity<HrIdentityWorkflowIssue>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.Status, item.DetectedAtUtc });
            entity.HasIndex(item => new { item.TenantId, item.WorkflowApprovalId, item.IssueType })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [Status] = 0 AND [WorkflowApprovalId] IS NOT NULL");
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Employee>().WithMany().HasForeignKey(item => item.EmployeeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<WorkflowInstance>().WithMany().HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<WorkflowStepInstance>().WithMany().HasForeignKey(item => item.WorkflowStepInstanceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<WorkflowApproval>().WithMany().HasForeignKey(item => item.WorkflowApprovalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.StaleAssigneeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.SuggestedReplacementUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ReplacementUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(item => item.ResolvedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant).WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasTrigger("TR_HrIdentityWorkflowIssues_TenantGuard"));
        });
    }

    private static void SetSoftDeleteFilter<TEntity>(ModelBuilder builder, Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType)
        where TEntity : BaseEntity
    {
        builder.Entity<TEntity>().HasQueryFilter(e => !e.IsDeleted);
    }

    private void SetTenantSoftDeleteFilter<TEntity>(ModelBuilder builder, Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entityType)
        where TEntity : TenantEntity
    {
        builder.Entity<TEntity>().HasQueryFilter(e =>
            !e.IsDeleted &&
            (!CurrentTenantId.HasValue ||
             e.TenantId == CurrentTenantId.GetValueOrDefault()));
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
            entity.HasIndex(item => new { item.TenantId, item.TemplateFileUploadRecordId });
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
            entity.HasIndex(item => new { item.TenantId, item.CustomerBusinessPartnerId });
            entity.HasIndex(item => new { item.TenantId, item.IsPublishedToExternalPortal, item.ExternalListingStatus });
            entity.Property(item => item.AssetType).HasConversion<string>().HasMaxLength(40);
            entity.Property(item => item.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(item => item.SourceType).HasConversion<string>().HasMaxLength(40);
            entity.Property(item => item.GisProvider).HasDefaultValue("GeoServer");
            entity.Property(item => item.GisSyncStatus).HasDefaultValue("NotLinked");
            entity.Property(item => item.AreaSquareMeters).HasPrecision(18, 4);
            entity.Property(item => item.AreaValue).HasPrecision(18, 4);
            entity.Property(item => item.GroundRentPayable).HasColumnType("decimal(18,4)");
            entity.Property(item => item.GroundRentRatePerAcre).HasColumnType("decimal(18,4)");
            entity.Property(item => item.GroundRentComputed).HasColumnType("decimal(18,4)");
            entity.Property(item => item.ValuationAmount).HasPrecision(18, 2);
            entity.Property(item => item.ExternalListingPrice).HasPrecision(18, 2);
            entity.Property(item => item.ExternalSalePrice).HasPrecision(18, 2);
            entity.Property(item => item.ExternalMonthlyRent).HasPrecision(18, 2);
            entity.HasMany(item => item.Documents)
                .WithOne(item => item.EstateManagedAsset)
                .HasForeignKey(item => item.EstateManagedAssetId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(item => item.Demarcations)
                .WithOne(item => item.EstateManagedAsset)
                .HasForeignKey(item => item.EstateManagedAssetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EstateLandDemarcation>(entity =>
        {
            entity.ToTable("EstateLandDemarcations");
            entity.HasIndex(item => new { item.TenantId, item.EstateManagedAssetId, item.DemarcationNumber })
                .IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.EstateManagedAssetId, item.ParentDemarcationId });
            entity.HasIndex(item => new { item.TenantId, item.IsReadyForProjectManagement, item.IsPublishedToExternalPortal });
            entity.Property(item => item.AreaSquareFeet).HasPrecision(18, 4);
            entity.Property(item => item.AllocatedCost).HasPrecision(18, 2);
            entity.Property(item => item.CostPerAcre).HasPrecision(18, 2);
            entity.Property(item => item.TargetSalePrice).HasPrecision(18, 2);
            entity.Property(item => item.GroundRentPayable).HasColumnType("decimal(18,4)");
            entity.Property(item => item.GroundRentRatePerAcre).HasColumnType("decimal(18,4)");
            entity.Property(item => item.GroundRentComputed).HasColumnType("decimal(18,4)");
            entity.Property(item => item.ParentLandAssetReference).HasMaxLength(120);
            entity.Property(item => item.ParentFixedAssetReference).HasMaxLength(120);
            entity.Property(item => item.ChildFixedAssetReference).HasMaxLength(120);
            entity.Property(item => item.FixedAssetPostingStatus).HasMaxLength(40);
            entity.Property(item => item.ExternalListingPrice).HasPrecision(18, 2);
            entity.Property(item => item.ExternalSalePrice).HasPrecision(18, 2);
            entity.Property(item => item.ExternalMonthlyRent).HasPrecision(18, 2);
            entity.HasOne(item => item.ParentDemarcation)
                .WithMany(item => item.ChildDemarcations)
                .HasForeignKey(item => item.ParentDemarcationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EstateGisConfiguration>(entity =>
        {
            entity.ToTable("EstateGisConfigurations");
            entity.HasIndex(item => item.TenantId).IsUnique();
        });

        builder.Entity<EstateManagedAssetDocument>(entity =>
        {
            entity.ToTable("EstateManagedAssetDocuments");
            entity.HasIndex(item => new { item.TenantId, item.EstateManagedAssetId });
            entity.HasIndex(item => new { item.TenantId, item.CentralDocumentRecordId });
            entity.HasIndex(item => new { item.TenantId, item.EstateManagedAssetId, item.IsListingImage });
        });

        builder.Entity<EstateGroundRentAccount>(entity =>
        {
            entity.ToTable("EstateGroundRentAccounts");
            entity.HasIndex(item => new { item.TenantId, item.EstateManagedAssetId })
                .IsUnique()
                .HasFilter("[Status] <> 'Closed' AND [IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.CustomerBusinessPartnerId, item.Status });
            entity.HasIndex(item => new { item.TenantId, item.NextDueDate, item.Status });
            entity.HasIndex(item => new { item.TenantId, item.NextReviewDate, item.Status });
            entity.Property(item => item.AnnualAmount).HasPrecision(18, 2);
            entity.Property(item => item.RatePerAcre).HasPrecision(18, 2);
            entity.Property(item => item.EscalationValue).HasPrecision(18, 4);
            entity.Property(item => item.PenaltyValue).HasPrecision(18, 4);
            entity.Property(item => item.PenaltyCapAmount).HasPrecision(18, 2);
            entity.HasOne(item => item.EstateManagedAsset)
                .WithMany()
                .HasForeignKey(item => item.EstateManagedAssetId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(item => item.GroundRentIncomeAccount)
                .WithMany()
                .HasForeignKey(item => item.GroundRentIncomeAccountId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasMany(item => item.Charges)
                .WithOne(item => item.GroundRentAccount)
                .HasForeignKey(item => item.GroundRentAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(item => item.Reviews)
                .WithOne(item => item.GroundRentAccount)
                .HasForeignKey(item => item.GroundRentAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EstateGroundRentCharge>(entity =>
        {
            entity.ToTable("EstateGroundRentCharges");
            entity.HasIndex(item => new { item.TenantId, item.GroundRentAccountId, item.DueDate }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.FinanceInvoiceId });
            entity.HasIndex(item => new { item.TenantId, item.PenaltyInvoiceId });
            entity.Property(item => item.BaseAmount).HasPrecision(18, 2);
            entity.Property(item => item.PenaltyAmount).HasPrecision(18, 2);
        });

        builder.Entity<EstateGroundRentReview>(entity =>
        {
            entity.ToTable("EstateGroundRentReviews");
            entity.HasIndex(item => new { item.TenantId, item.GroundRentAccountId, item.EffectiveDate });
            entity.Property(item => item.PreviousAnnualAmount).HasPrecision(18, 2);
            entity.Property(item => item.NewAnnualAmount).HasPrecision(18, 2);
            entity.Property(item => item.PreviousRatePerAcre).HasPrecision(18, 2);
            entity.Property(item => item.NewRatePerAcre).HasPrecision(18, 2);
            entity.Property(item => item.EscalationValue).HasPrecision(18, 4);
        });

        builder.Entity<EstateFacilityDutyRoster>(entity =>
        {
            entity.ToTable("EstateFacilityDutyRosters");
            entity.HasIndex(item => new { item.TenantId, item.RosterReference }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.StaffName, item.StartDate });
            entity.HasIndex(item => new { item.TenantId, item.PropertyReference, item.PropertyUnit });
            entity.HasIndex(item => new { item.TenantId, item.ServiceAreaType, item.ServiceAreaName });
            entity.HasIndex(item => new { item.TenantId, item.AttendanceStatus, item.CompletionStatus });
            entity.HasIndex(item => new { item.TenantId, item.LinkedMaintenanceReference });
            entity.HasIndex(item => new { item.TenantId, item.LinkedComplaintReference });
        });

        builder.Entity<EstateFacilityDutyAttendance>(entity =>
        {
            entity.ToTable("EstateFacilityDutyAttendances");
            entity.HasIndex(item => new { item.TenantId, item.DutyRosterId, item.DutyDate }).IsUnique();
            entity.HasOne(item => item.DutyRoster).WithMany()
                .HasForeignKey(item => item.DutyRosterId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EstateFacilityProviderRate>(entity =>
        {
            entity.ToTable("EstateFacilityProviderRates");
            entity.Property(item => item.Rate).HasPrecision(18, 4);
            entity.HasIndex(item => new { item.TenantId, item.BusinessPartnerId, item.IsActive });
            entity.HasIndex(item => new { item.TenantId, item.ContractId });
        });

        builder.Entity<EstateFacilityProviderAssignment>(entity =>
        {
            entity.ToTable("EstateFacilityProviderAssignments");
            entity.HasIndex(item => new { item.TenantId, item.BusinessPartnerId, item.AssignmentStatus });
            entity.HasIndex(item => new { item.TenantId, item.EstateManagedAssetId, item.AssignmentStatus });
            entity.HasIndex(item => new { item.TenantId, item.ContractId });
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
            entity.HasIndex(item => item.OrganizationLevelId);
            entity.HasIndex(item => new { item.TenantId, item.OrganizationUnitId });

            entity.HasOne(item => item.OrganizationLevel)
                .WithMany()
                .HasForeignKey(item => item.OrganizationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(item => item.OrganizationUnit)
                .WithMany()
                .HasForeignKey(item => item.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

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
        if (HasInventoryCostProjectionChanges() && Database.IsRelational() && Database.CurrentTransaction == null)
        {
            return await Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
                var result = await SaveWithInventoryCostProjectionAsync(false, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                ChangeTracker.AcceptAllChanges();
                return result;
            });
        }
        return await SaveWithInventoryCostProjectionAsync(true, cancellationToken);
    }

    private async Task<int> SaveWithInventoryCostProjectionAsync(bool acceptAllChanges, CancellationToken cancellationToken)
    {
        await SynchronizeInventoryAverageCostsAsync(true, cancellationToken);
        // HR, round 4 lane O: before the audit pass, so the holders it moves are stamped too.
        await ApplyTechnicianRoleRuleAsync(true, cancellationToken);
        UpdateAuditableEntities();
        NormalizeProcurementAwardReadinessAuditEnvelopes();
        NormalizeProcurementBidderCommunicationAuditEnvelopes();
        return await base.SaveChangesAsync(acceptAllChanges, cancellationToken);
    }

    public override int SaveChanges()
    {
        if (HasInventoryCostProjectionChanges() && Database.IsRelational() && Database.CurrentTransaction == null)
        {
            return Database.CreateExecutionStrategy().Execute(() =>
            {
                using var transaction = Database.BeginTransaction();
                var result = SaveWithInventoryCostProjection(false);
                transaction.Commit();
                ChangeTracker.AcceptAllChanges();
                return result;
            });
        }
        return SaveWithInventoryCostProjection(true);
    }

    private int SaveWithInventoryCostProjection(bool acceptAllChanges)
    {
        SynchronizeInventoryAverageCostsAsync(false, CancellationToken.None).GetAwaiter().GetResult();
        // HR, round 4 lane O — see the async path.
        ApplyTechnicianRoleRuleAsync(false, CancellationToken.None).GetAwaiter().GetResult();
        UpdateAuditableEntities();
        NormalizeProcurementAwardReadinessAuditEnvelopes();
        NormalizeProcurementBidderCommunicationAuditEnvelopes();
        return base.SaveChanges(acceptAllChanges);
    }

    private void NormalizeProcurementAwardReadinessAuditEnvelopes()
    {
        foreach (var entry in ChangeTracker
                     .Entries<ProcurementAwardReadinessDecision>()
                     .Where(item => item.State == EntityState.Added))
        {
            // SQL treats the evaluation time as the immutable creation time.
            // Apply this after the generic audit pass so no convention or
            // interceptor can leave the two persisted values out of sync.
            entry.Entity.CreatedAt = entry.Entity.EvaluatedAtUtc;
            entry.Entity.UpdatedAt = null;
            entry.Entity.UpdatedBy = null;
            entry.Entity.LastModifiedById = null;
        }
    }

    private void NormalizeProcurementBidderCommunicationAuditEnvelopes()
    {
        foreach (var entry in ChangeTracker
                     .Entries<ProcurementBidderCommunicationRegister>()
                     .Where(item => item.State == EntityState.Added))
        {
            // The append-only register trigger binds creation to the service's
            // immutable initialization timestamp. Apply this after the generic
            // audit convention so the persisted envelope cannot drift by a few
            // milliseconds during SaveChanges.
            entry.Entity.CreatedAt = entry.Entity.InitializedAtUtc;
            entry.Entity.UpdatedAt = null;
            entry.Entity.UpdatedBy = null;
            entry.Entity.LastModifiedById = null;
        }
    }

    private void UpdateAuditableEntities()
    {
        var entries = ChangeTracker.Entries<BaseEntity>();

        foreach (var entry in entries)
        {
            if (entry.Entity is WorkflowActivityLog && entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Workflow audit events are immutable and cannot be changed or deleted.");
            if (entry.Entity is FinanceSourceDimensionChange && entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Finance source-dimension change evidence is immutable and cannot be changed or deleted.");
            if (entry.Entity is FinanceSourceBookAuthorityOrigin && entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Finance source-book origin evidence is append-only and cannot be changed or deleted.");
            if (entry.Entity is FinanceSourceBookAuthority authority)
            {
                if (entry.State == EntityState.Deleted)
                    throw new InvalidOperationException("Finance source-book authority cannot be deleted.");
                if (entry.State == EntityState.Modified)
                {
                    var allowed = new HashSet<string>(StringComparer.Ordinal)
                    {
                        nameof(FinanceSourceBookAuthority.OriginalFinancePostingEventId),
                        nameof(FinanceSourceBookAuthority.OriginalJournalEntryId),
                        nameof(FinanceSourceBookAuthority.BoundByUserId),
                        nameof(FinanceSourceBookAuthority.BoundAtUtc)
                    };
                    if (entry.Properties.Where(item => item.IsModified).Any(item => !allowed.Contains(item.Metadata.Name)) ||
                        entry.OriginalValues.GetValue<Guid?>(nameof(FinanceSourceBookAuthority.OriginalFinancePostingEventId)).HasValue ||
                        entry.OriginalValues.GetValue<Guid?>(nameof(FinanceSourceBookAuthority.OriginalJournalEntryId)).HasValue ||
                        !authority.OriginalFinancePostingEventId.HasValue || !authority.OriginalJournalEntryId.HasValue ||
                        !authority.BoundAtUtc.HasValue)
                        throw new InvalidOperationException("Finance source-book authority is immutable except for its first complete original-posting binding.");
                }
            }
            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity is ProcurementAwardReadinessDecision readinessDecision)
                    {
                        // The append-only SQL envelope requires the service's
                        // single evaluation timestamp and forbids update metadata.
                        // Preserve that exact timestamp instead of applying the
                        // generic mutable-entity audit defaults.
                        if (readinessDecision.CreatedAt == default)
                            readinessDecision.CreatedAt = readinessDecision.EvaluatedAtUtc;
                        readinessDecision.UpdatedAt = null;
                        readinessDecision.UpdatedBy = null;
                        readinessDecision.LastModifiedById = null;
                    }
                    else
                    {
                        // Inventory assigns a strictly increasing server timestamp to
                        // each new adjustment line. Preserve that FIFO posting order;
                        // caller DTOs never supply this value. Other audit defaults
                        // and every existing row's historical timestamp are unchanged.
                        if (entry.Entity is not StockAdjustmentItem || entry.Entity.CreatedAt == default)
                            entry.Entity.CreatedAt = DateTime.UtcNow;
                        entry.Entity.UpdatedAt = DateTime.UtcNow;
                    }
                    
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
                    // Year-end closing evidence is a functional-currency amount. Keep the
                    // runtime model aligned with the entity annotation and migration instead
                    // of allowing the broad name-based convention below to widen it to 18,4.
                    if (entityType.ClrType == typeof(YearEndBookCloseCycle)
                        && property.Name == nameof(YearEndBookCloseCycle.NetIncomeTransferred))
                    {
                        property.SetColumnType("decimal(18,2)");
                        continue;
                    }

                    if ((entityType.ClrType == typeof(InventoryItem) && property.Name == nameof(InventoryItem.Weight)) ||
                        (entityType.ClrType == typeof(GoodsReceiptNoteItem) && property.Name == nameof(GoodsReceiptNoteItem.UnitWeightKg)) ||
                        (entityType.ClrType == typeof(PurchaseOrderReceiptItem) && property.Name == nameof(PurchaseOrderReceiptItem.UnitWeightKg)) ||
                        (entityType.ClrType == typeof(LandedCostReceiptWeight) && property.Name == nameof(LandedCostReceiptWeight.UnitWeightKg)))
                    {
                        property.SetColumnType("decimal(22,6)");
                        continue;
                    }
                    // Exchange-rate evidence needs more precision than ordinary quantities. Keep
                    // this explicit because the broad convention below runs after entity-specific
                    // configuration and would otherwise silently reduce it to four decimals.
                    if (property.Name == nameof(JournalEntry.ReplicationExchangeRate)
                        || property.Name == nameof(AccountingBookInitializationLine.TranslationRate))
                    {
                        property.SetColumnType("decimal(18,6)");
                        continue;
                    }

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

    /// <summary>
    /// Shared cross-module reference data: administrative geography. See
    /// docs/GEOGRAPHY-REFERENCE-DESIGN.md for the decisions behind this shape.
    /// </summary>
    private static void ConfigureReferenceModule(ModelBuilder builder)
    {
        // ════════════════════════════════════════════════════════════════════════════════════
        //  GEO SCHEME
        // ════════════════════════════════════════════════════════════════════════════════════
        builder.Entity<GeoScheme>(entity =>
        {
            entity.ToTable("GeoSchemes");

            // Filtered on IsDeleted: a soft-deleted scheme must not hold its code hostage.
            entity.HasIndex(e => new { e.TenantId, e.Code })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0")
                .HasDatabaseName("IX_GeoScheme_Tenant_Code");

            entity.HasIndex(e => new { e.TenantId, e.CountryId })
                .HasDatabaseName("IX_GeoScheme_Tenant_Country");

            // Restrict, not Cascade: deleting a country must not silently take its whole division
            // scheme — and every address that resolved through it — with it.
            entity.HasOne(e => e.Country)
                .WithMany()
                .HasForeignKey(e => e.CountryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Levels)
                .WithOne(e => e.Scheme)
                .HasForeignKey(e => e.SchemeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Areas)
                .WithOne(e => e.Scheme)
                .HasForeignKey(e => e.SchemeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ════════════════════════════════════════════════════════════════════════════════════
        //  GEO LEVEL
        // ════════════════════════════════════════════════════════════════════════════════════
        builder.Entity<GeoLevel>(entity =>
        {
            entity.ToTable("GeoLevels");

            entity.HasIndex(e => new { e.TenantId, e.SchemeId, e.LevelNumber })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0")
                .HasDatabaseName("IX_GeoLevel_Tenant_Scheme_LevelNum");

            entity.HasIndex(e => new { e.TenantId, e.SchemeId, e.Code })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0")
                .HasDatabaseName("IX_GeoLevel_Tenant_Scheme_Code");

            entity.HasOne(e => e.Scheme)
                .WithMany(e => e.Levels)
                .HasForeignKey(e => e.SchemeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Areas)
                .WithOne(e => e.GeoLevel)
                .HasForeignKey(e => e.GeoLevelId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ════════════════════════════════════════════════════════════════════════════════════
        //  GEO AREA
        // ════════════════════════════════════════════════════════════════════════════════════
        builder.Entity<GeoArea>(entity =>
        {
            entity.ToTable("GeoAreas");

            // The code is the seeder's idempotency key, so it is unique per scheme — not per
            // level. Two tiers of the same scheme sharing a code would break re-seeding.
            entity.HasIndex(e => new { e.TenantId, e.SchemeId, e.Code })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0")
                .HasDatabaseName("IX_GeoArea_Tenant_Scheme_Code");

            entity.HasIndex(e => e.ParentAreaId)
                .HasDatabaseName("IX_GeoArea_ParentId");

            entity.HasIndex(e => e.GeoLevelId)
                .HasDatabaseName("IX_GeoArea_LevelId");

            // The address widget's cascade query: "children of this parent, at this level, active".
            entity.HasIndex(e => new { e.TenantId, e.ParentAreaId, e.IsActive })
                .HasDatabaseName("IX_GeoArea_Tenant_Parent_Active");

            // Name lookups are how the import resolver and backfill find an area.
            entity.HasIndex(e => new { e.TenantId, e.SchemeId, e.Name })
                .HasDatabaseName("IX_GeoArea_Tenant_Scheme_Name");

            entity.HasOne(e => e.ParentArea)
                .WithMany(e => e.ChildAreas)
                .HasForeignKey(e => e.ParentAreaId)
                .OnDelete(DeleteBehavior.Restrict);

            // ⚠ Configured explicitly even though nothing navigates back. An unpaired navigation
            // left to convention mints a shadow FK column (GeoAreaId1) that silently duplicates
            // this one.
            entity.HasOne(e => e.SupersededByArea)
                .WithMany()
                .HasForeignKey(e => e.SupersededByGeoAreaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.Aliases)
                .WithOne(e => e.GeoArea)
                .HasForeignKey(e => e.GeoAreaId)
                .OnDelete(DeleteBehavior.Cascade); // aliases are meaningless without their area
        });

        // ════════════════════════════════════════════════════════════════════════════════════
        //  GEO AREA ALIAS
        // ════════════════════════════════════════════════════════════════════════════════════
        builder.Entity<GeoAreaAlias>(entity =>
        {
            entity.ToTable("GeoAreaAliases");

            entity.HasIndex(e => new { e.TenantId, e.GeoAreaId, e.Alias })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0")
                .HasDatabaseName("IX_GeoAreaAlias_Tenant_Area_Alias");

            // The resolver's lookup: "does any area answer to this name?"
            entity.HasIndex(e => new { e.TenantId, e.Alias })
                .HasDatabaseName("IX_GeoAreaAlias_Tenant_Alias");

            entity.HasOne(e => e.GeoArea)
                .WithMany(e => e.Aliases)
                .HasForeignKey(e => e.GeoAreaId)
                .OnDelete(DeleteBehavior.Cascade);
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
            entity.Property(p => p.ApprovalRequired).HasDefaultValue(true);
            entity.ToTable("ProcurementPlans", table => table.HasTrigger("TR_ProcurementPlans_ApprovalPolicy"));
            entity.HasIndex(p => p.PlanNumber).IsUnique();
            entity.HasIndex(p => p.DepartmentId);
            entity.HasIndex(p => p.OrganizationUnitId);
            entity.HasIndex(p => p.FiscalYear);
            entity.HasIndex(p => p.PlanningCycle);
            entity.HasIndex(p => p.PlanningQuarter);
            entity.HasIndex(p => p.Status);
            entity.HasIndex(p => p.PublishedDate);
            entity.HasIndex(p => p.BudgetId);

            entity.HasOne(p => p.Department)
                .WithMany()
                .HasForeignKey(p => p.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.OrganizationUnit)
                .WithMany()
                .HasForeignKey(p => p.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(p => p.Items)
                .WithOne(i => i.ProcurementPlan)
                .HasForeignKey(i => i.ProcurementPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(p => p.Budget)
                .WithMany()
                .HasForeignKey(p => p.BudgetId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ProcurementPlanItem entity
        builder.Entity<ProcurementPlanItem>(entity =>
        {
            entity.Property(i => i.ReferenceNumber).HasMaxLength(36)
                .HasComputedColumnSql("N'PPL-' + LOWER(REPLACE(CONVERT(nvarchar(36), [Id]), N'-', N''))", stored: true);
            entity.HasIndex(i => new { i.TenantId, i.ReferenceNumber }).IsUnique();
            // Retain the existing tenant index when EF discovers the new composite index.
            entity.HasIndex(i => i.TenantId);
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
            entity.Property(b => b.ApprovalRequired).HasDefaultValue(true);
            entity.ToTable("ProcurementBudgets", table => table.HasTrigger("TR_ProcurementBudgets_ApprovalPolicy"));
            entity.HasIndex(b => new { b.TenantId, b.BudgetCode }).IsUnique();
            entity.HasIndex(b => b.DepartmentId);
            entity.HasIndex(b => b.OrganizationUnitId);
            entity.HasIndex(b => b.FiscalYear);
            entity.HasIndex(b => b.Status);

            entity.HasOne(b => b.Department)
                .WithMany()
                .HasForeignKey(b => b.DepartmentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.OrganizationUnit)
                .WithMany()
                .HasForeignKey(b => b.OrganizationUnitId)
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
            entity.Property(r => r.ApprovalRequired).HasDefaultValue(true);
            entity.ToTable("ProcurementBudgetRevisions", table => table.HasTrigger("TR_ProcurementBudgetRevisions_ApprovalPolicy"));
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
                table.HasCheckConstraint(
                    "CK_ProcurementBudgetCommitments_Amount",
                    "([Status] = 2 AND [ReservedAmount] >= 0) OR ([Status] IN (1, 3) AND [ReservedAmount] > 0)");
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

        builder.Entity<ProcurementBudgetCommitmentLedgerEntry>(entity =>
        {
            entity.HasIndex(item => new
                { item.TenantId, item.EntryType, item.SourceType, item.SourceId })
                .IsUnique();
            entity.HasIndex(item => new
                { item.TenantId, item.ProcurementBudgetCommitmentId, item.OccurredAtUtc });
            entity.HasIndex(item => new
                { item.TenantId, item.FormalCommitmentEntryId, item.EntryType });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_ProcurementBudgetCommitmentLedgerEntries_Immutable");
                table.HasCheckConstraint(
                    "CK_ProcurementBudgetCommitmentLedgerEntries_Amount", "[Amount] > 0");
                table.HasCheckConstraint(
                    "CK_ProcurementBudgetCommitmentLedgerEntries_EntryType", "[EntryType] IN (1, 2, 3, 4)");
            });
            entity.HasOne(item => item.ProcurementBudgetCommitment)
                .WithMany(item => item.LedgerEntries)
                .HasForeignKey(item => item.ProcurementBudgetCommitmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ProcurementBudget)
                .WithMany()
                .HasForeignKey(item => item.ProcurementBudgetId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.PurchaseRequisition)
                .WithMany()
                .HasForeignKey(item => item.PurchaseRequisitionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.FormalCommitmentEntry)
                .WithMany(item => item.UtilizationEntries)
                .HasForeignKey(item => item.FormalCommitmentEntryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ProcurementSchedule entity
        builder.Entity<ProcurementSchedule>(entity =>
        {
            entity.HasIndex(s => s.ProcurementPlanId);
            entity.HasIndex(s => s.DepartmentId);
            entity.HasIndex(s => s.OrganizationUnitId);
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

            entity.HasOne(s => s.OrganizationUnit)
                .WithMany()
                .HasForeignKey(s => s.OrganizationUnitId)
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
            entity.HasOne(p => p.Supplier)
                .WithMany()
                .HasForeignKey(p => p.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
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
            entity.HasIndex(e => e.EmergencyType);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CriticalityLevel);
            entity.Property(e => e.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.ToTable("EmergencyProcurementPlans", table =>
            {
                table.HasTrigger("TR_EmergencyProcurementPlans_Governance");
                table.HasCheckConstraint("CK_EmergencyProcurementPlans_GovernedStatus",
                    "[Status] IN ('Draft','Prepared','PendingAudit','AuditVouched','PendingApproval','Approved','Rejected','Triggered','Filed')");
                table.HasCheckConstraint("CK_EmergencyProcurementPlans_PreparedLineage",
                    "([Status] = 'Draft' AND [PurchaseRequisitionId] IS NULL AND [ExceptionRuleId] IS NULL AND [WorkflowDefinitionId] IS NULL AND [PreparedById] IS NULL AND [PreparedAtUtc] IS NULL) OR " +
                    "([Status] <> 'Draft' AND [PurchaseRequisitionId] IS NOT NULL AND [ExceptionRuleId] IS NOT NULL AND [WorkflowDefinitionId] IS NOT NULL AND [CentralDocumentVersionId] IS NOT NULL AND [FileUploadRecordId] IS NOT NULL AND LEN([EvidenceReference]) > 0 AND LEN([ExceptionJustification]) >= 20 AND [PreparedById] IS NOT NULL AND [PreparedAtUtc] IS NOT NULL AND [ApprovalAuthority] IN ('ManagingDirector','Board') AND LEN([IntegrityHash]) = 64)");
                table.HasCheckConstraint("CK_EmergencyProcurementPlans_AuditLineage",
                    "([Status] IN ('Draft','Prepared','PendingAudit') AND [InternalAuditVouchedById] IS NULL AND [InternalAuditVouchedAtUtc] IS NULL) OR " +
                    "([Status] IN ('AuditVouched','PendingApproval','Approved','Rejected','Triggered','Filed') AND [InternalAuditVouchedById] IS NOT NULL AND [InternalAuditVouchedAtUtc] IS NOT NULL AND LEN([InternalAuditVouchNote]) >= 10)");
                table.HasCheckConstraint("CK_EmergencyProcurementPlans_ApprovalLineage",
                    "([Status] IN ('Draft','Prepared','PendingAudit','AuditVouched') AND [WorkflowInstanceId] IS NULL AND [ApprovedById] IS NULL AND [ApprovedDate] IS NULL) OR " +
                    "([Status] IN ('PendingApproval','Rejected') AND [WorkflowInstanceId] IS NOT NULL AND [ApprovedById] IS NULL AND [ApprovedDate] IS NULL AND [ApprovalReference] IS NULL) OR " +
                    "([Status] IN ('Approved','Triggered','Filed') AND [WorkflowInstanceId] IS NOT NULL AND [ApprovedById] IS NOT NULL AND [ApprovedDate] IS NOT NULL AND [ApprovalReference] IS NOT NULL)");
                table.HasCheckConstraint("CK_EmergencyProcurementPlans_FilingLineage",
                    "([Status] <> 'Filed' AND [ExceptionalSourcingTenderId] IS NULL AND [PostAwardJustification] IS NULL AND [PostAwardCentralDocumentVersionId] IS NULL AND [PostAwardFileUploadRecordId] IS NULL AND [PostAwardEvidenceReference] IS NULL AND [FiledAtUtc] IS NULL AND [FiledById] IS NULL) OR " +
                    "([Status] = 'Filed' AND [ExceptionalSourcingTenderId] IS NOT NULL AND [PostAwardJustification] IS NOT NULL AND [PostAwardCentralDocumentVersionId] IS NOT NULL AND [PostAwardFileUploadRecordId] IS NOT NULL AND [PostAwardEvidenceReference] IS NOT NULL AND [FiledAtUtc] IS NOT NULL AND [FiledById] IS NOT NULL)");
            });

            entity.HasOne(e => e.PurchaseRequisition).WithMany()
                .HasForeignKey(e => e.PurchaseRequisitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ExceptionRule).WithMany()
                .HasForeignKey(e => e.ExceptionRuleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.WorkflowDefinition).WithMany()
                .HasForeignKey(e => e.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.WorkflowInstance).WithMany()
                .HasForeignKey(e => e.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CentralDocumentVersion).WithMany()
                .HasForeignKey(e => e.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PostAwardCentralDocumentVersion).WithMany()
                .HasForeignKey(e => e.PostAwardCentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FileUploadRecord>().WithMany()
                .HasForeignKey(e => e.FileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<FileUploadRecord>().WithMany()
                .HasForeignKey(e => e.PostAwardFileUploadRecordId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Tender>().WithMany()
                .HasForeignKey(e => e.ExceptionalSourcingTenderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(e => e.PreparedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(e => e.InternalAuditVouchedById).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(e => e.FiledById).OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.CriticalItems)
                .WithOne(i => i.EmergencyProcurementPlan)
                .HasForeignKey(i => i.EmergencyProcurementPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.EmergencySuppliers)
                .WithOne(s => s.EmergencyProcurementPlan)
                .HasForeignKey(s => s.EmergencyProcurementPlanId)
                .OnDelete(DeleteBehavior.Cascade);

            // Keep the tenant-leading control indexes alongside EF's conventional
            // single-column FK indexes, but retire the legacy tenant-only index.
            entity.HasIndex(e => new { e.TenantId, e.PlanCode }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.PurchaseRequisitionId })
                .IsUnique().HasFilter("[PurchaseRequisitionId] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasIndex(e => new { e.TenantId, e.ExceptionRuleId });
            entity.HasIndex(e => new { e.TenantId, e.WorkflowInstanceId });
            entity.HasIndex(e => new { e.TenantId, e.ExceptionalSourcingTenderId });

            var legacyTenantIndex = entity.Metadata.GetIndexes()
                .SingleOrDefault(index =>
                    index.Properties.Count == 1 &&
                    index.Properties[0].Name == nameof(EmergencyProcurementPlan.TenantId));

            if (legacyTenantIndex != null)
            {
                entity.Metadata.RemoveIndex(legacyTenantIndex);
            }
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
            entity.Property(r => r.ApprovalRequired).HasDefaultValue(true);
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.SourcePlanItemId });
            entity.HasIndex(item => new { item.TenantId, item.BudgetId });
            entity.HasIndex(item => new { item.TenantId, item.ProjectId });
            entity.HasIndex(item => new { item.TenantId, item.OrganizationUnitId });
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
            entity.HasOne(item => item.OrganizationUnit).WithMany()
                .HasForeignKey(item => item.OrganizationUnitId).OnDelete(DeleteBehavior.Restrict);
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

        builder.Entity<PurchaseRequisitionItem>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.SourcePlanItemId });
            entity.HasOne(item => item.SourcePlanItem).WithMany()
                .HasForeignKey(item => item.SourcePlanItemId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureInventoryEntities(ModelBuilder builder)
    {
        // InventoryItem entity
        builder.Entity<InventoryItem>(entity =>
        {
            entity.HasOne<Account>().WithMany().HasForeignKey(item => item.InventoryAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(item => item.InventoryOffsetAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(item => item.CostOfGoodsSoldAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(item => item.SalesAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(item => item.MarkdownsAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(item => item.SalesReturnsAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(item => item.InUseAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(item => item.InServiceAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(item => item.DamagedAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(item => item.VarianceAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(item => item.DropShipItemsAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(item => item.PurchasePriceVarianceAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(item => item.UnrealisedPurchasePriceVarianceAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(item => item.InventoryReturnsAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(item => item.InventoryDisposalAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(item => item.AssemblyVarianceAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Account>().WithMany().HasForeignKey(item => item.StandardCostRevaluationAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("InventoryItems", table =>
            {
                table.HasTrigger("TR_TDC0601_InventoryItems_IdentifierIntegrity");
                table.HasTrigger("TR_TDC0616_InventoryItems_ProfileIntegrity");
                table.HasCheckConstraint(
                    "CK_InventoryItems_Identifiers_Normalized",
                    "([Barcode] IS NULL OR [Barcode] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([Barcode]))) COLLATE Latin1_General_100_BIN2) AND ([AlternateBarcode] IS NULL OR [AlternateBarcode] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([AlternateBarcode]))) COLLATE Latin1_General_100_BIN2) AND ([QRCode] IS NULL OR [QRCode] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([QRCode]))) COLLATE Latin1_General_100_BIN2)");
                table.HasCheckConstraint(
                    "CK_InventoryItems_ProfileRequired",
                    "LEN(LTRIM(RTRIM([ItemCode]))) > 0 AND LEN(LTRIM(RTRIM([Name]))) > 0 AND LEN(LTRIM(RTRIM([UnitOfMeasure]))) > 0");
                table.HasCheckConstraint(
                    "CK_InventoryItems_ProfileLevels",
                    "[MinimumLevel] >= 0 AND [MaximumLevel] >= 0 AND [ReorderLevel] >= 0 AND [ReorderQuantity] >= 0 AND [SafetyStock] >= 0 AND ([MaximumLevel] = 0 OR ([MinimumLevel] <= [MaximumLevel] AND [ReorderLevel] <= [MaximumLevel]))");
                table.HasCheckConstraint(
                    "CK_InventoryItems_ProfileEnums",
                    "[ValuationMethod] BETWEEN 1 AND 5 AND [Status] BETWEEN 1 AND 4 AND [ItemType] BETWEEN 1 AND 4");
                table.HasCheckConstraint(
                    "CK_InventoryItems_ProfileTracking",
                    "[IsExpirationTracked] = 0 OR [ShelfLifeDays] > 0");
            });
            entity.Property(i => i.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.Property(i => i.ValuationMethod).HasDefaultValue(ValuationMethod.WeightedAverage);
            entity.HasIndex(i => new { i.TenantId, i.ItemCode })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(i => i.TenantId);
            entity.HasIndex(i => new { i.TenantId, i.Barcode })
                .IsUnique()
                .HasFilter("[Barcode] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasIndex(i => new { i.TenantId, i.AlternateBarcode })
                .IsUnique()
                .HasFilter("[AlternateBarcode] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasIndex(i => new { i.TenantId, i.QRCode })
                .IsUnique()
                .HasFilter("[QRCode] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasIndex(i => i.CategoryId);
            entity.HasIndex(i => i.Status);
            entity.HasIndex(i => i.ABCClass);
            entity.HasIndex(i => i.IsSerialTracked);
            entity.HasIndex(i => i.IsLotTracked);
            entity.HasIndex(i => new { i.TenantId, i.IsProjectApplicable, i.IsCostCentreApplicable, i.Status });
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
            entity.ToTable("StockMovements", table =>
            {
                table.HasTrigger("TR_StockMovements_GovernedPurchaseReceipt");
                table.HasTrigger("TR_StockMovements_GovernedRequisitionIssue");
            });
            entity.HasIndex(sm => sm.InventoryItemId);
            entity.HasIndex(sm => sm.LocationId);
            entity.HasIndex(sm => sm.MovementType);
            entity.HasIndex(sm => sm.MovementDate);
            entity.HasIndex(sm => sm.ReferenceType);
            entity.HasIndex(sm => sm.ReferenceNumber);
            entity.HasIndex(sm => sm.InventoryIssueVoucherId);
            entity.HasIndex(sm => sm.InventoryReturnVoucherId);

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

            entity.HasOne(sm => sm.InventoryIssueVoucher)
                .WithMany()
                .HasForeignKey(sm => sm.InventoryIssueVoucherId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(sm => sm.InventoryReturnVoucher)
                .WithMany()
                .HasForeignKey(sm => sm.InventoryReturnVoucherId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // PurchaseOrder entity
        builder.Entity<PurchaseOrder>(entity =>
        {
            entity.Property(po => po.ApprovalRequired).HasDefaultValue(true);
            entity.ToTable("PurchaseOrders", table =>
            {
                table.HasTrigger("TR_PurchaseOrders_FrameworkCallOffProtected");
                table.HasTrigger("TR_PurchaseOrders_ApprovedSourceProtected");
                table.HasCheckConstraint(
                    "CK_PurchaseOrders_ProcurementCategory",
                    "[ProcurementCategory] IS NULL OR [ProcurementCategory] BETWEEN 0 AND 4");
                table.HasCheckConstraint(
                    "CK_PurchaseOrders_GovernedCategoryRequired",
                    "[ProcurementSourceType] IS NULL OR [ProcurementSourceType] = 5 OR [ProcurementCategory] IS NOT NULL");
                table.HasCheckConstraint(
                    "CK_PurchaseOrders_ApprovedSourceLineage",
                    "[ProcurementSourceType] BETWEEN 0 AND 5 AND [ProcurementSourceId] IS NOT NULL AND LEN([ProcurementSourceReference]) BETWEEN 1 AND 100 AND ISJSON([SourceSnapshotJson]) = 1 AND LEN([SourceIntegrityHash]) = 64 AND [SourceValidatedAtUtc] IS NOT NULL AND ([ProcurementSourceType] = 5 OR ([SourceRequisitionId] IS NOT NULL AND [SourcingReleaseId] IS NOT NULL AND (([SourcingCaseId] IS NOT NULL AND [AwardReadinessDecisionId] IS NOT NULL) OR ([SourcingCaseId] IS NULL AND [ProcurementSourceType] = 0 AND [AwardReadinessDecisionId] IS NULL) OR ([SourcingCaseId] IS NULL AND [ProcurementSourceType] IN (1, 2) AND [AwardReadinessDecisionId] IS NOT NULL))))");
            });
            entity.HasIndex(po => po.OrderNumber).IsUnique();
            entity.HasIndex(po => po.Status);
            entity.HasIndex(po => po.OrderDate);
            entity.HasIndex(po => po.RequestedById);
            entity.HasIndex(po => po.TenderAwardId);
            entity.HasIndex(po => po.TenantId);
            entity.HasIndex(po => new
            {
                po.TenantId,
                po.ProcurementSourceType,
                po.ProcurementSourceId
            });
            entity.HasIndex(po => new
                {
                    po.TenantId,
                    po.ProcurementSourceType,
                    po.ProcurementSourceId,
                    po.BusinessPartnerId
                },
                "UX_PurchaseOrders_OneTimeApprovedSource")
                .IsUnique()
                .HasFilter("[ProcurementSourceType] IN (0, 1, 3)");
            entity.HasIndex(po => po.SourcingReleaseId);
            entity.HasIndex(po => po.SourcingCaseId);
            entity.HasIndex(po => po.AwardReadinessDecisionId);

            entity.HasOne(po => po.RequestedBy)
                .WithMany()
                .HasForeignKey(po => po.RequestedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(po => po.TenderAward)
                .WithMany()
                .HasForeignKey(po => po.TenderAwardId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne<ProcurementRequisitionSourcingRelease>()
                .WithMany()
                .HasForeignKey(po => po.SourcingReleaseId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne<ProcurementSourcingCase>()
                .WithMany()
                .HasForeignKey(po => po.SourcingCaseId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne<ProcurementAwardReadinessDecision>()
                .WithMany()
                .HasForeignKey(po => po.AwardReadinessDecisionId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // PurchaseOrderItem entity
        builder.Entity<PurchaseOrderItem>(entity =>
        {
            entity.Property(item => item.LineType).HasDefaultValue(ItemType.StockItem);
            entity.ToTable("PurchaseOrderItems", table =>
            {
                table.HasTrigger("TR_PurchaseOrderItems_FrameworkCallOffProtected");
                table.HasCheckConstraint("CK_PurchaseOrderItems_LineType", "[LineType] IN (1, 2, 3, 4)");
            });
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

        builder.Entity<ProcurementPurchaseOrderAmendment>(entity =>
        {
            entity.ToTable("ProcurementPurchaseOrderAmendments", table =>
                table.HasTrigger("TR_ProcurementPurchaseOrderAmendments_Protected"));
            entity.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
            entity.HasIndex(item => new { item.TenantId, item.PurchaseOrderId, item.AmendmentSequence })
                .IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.AmendmentNumber }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.PurchaseOrderId, item.Status });
            entity.HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
            entity.HasIndex(item => item.WorkflowInstanceId);
            entity.HasOne(item => item.PurchaseOrder)
                .WithMany()
                .HasForeignKey(item => item.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ProposedBusinessPartner)
                .WithMany()
                .HasForeignKey(item => item.ProposedBusinessPartnerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.WorkflowDefinition)
                .WithMany()
                .HasForeignKey(item => item.WorkflowDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.WorkflowInstance)
                .WithMany()
                .HasForeignKey(item => item.WorkflowInstanceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProcurementPurchaseOrderCommitmentAdjustment>(entity =>
        {
            entity.ToTable("ProcurementPurchaseOrderCommitmentAdjustments", table =>
                table.HasTrigger("TR_ProcurementPurchaseOrderCommitmentAdjustments_Immutable"));
            entity.HasIndex(item => item.AmendmentId).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.PurchaseOrderId, item.Sequence }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.PurchaseRequisitionId, item.Sequence });
            entity.HasOne(item => item.Amendment)
                .WithMany(item => item.CommitmentAdjustments)
                .HasForeignKey(item => item.AmendmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.PurchaseOrder)
                .WithMany()
                .HasForeignKey(item => item.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.PurchaseRequisition)
                .WithMany()
                .HasForeignKey(item => item.PurchaseRequisitionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ProcurementBudget)
                .WithMany()
                .HasForeignKey(item => item.ProcurementBudgetId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.BudgetCommitment)
                .WithMany()
                .HasForeignKey(item => item.BudgetCommitmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProcurementPurchaseOrderAmendmentDispatch>(entity =>
        {
            entity.ToTable("ProcurementPurchaseOrderAmendmentDispatches", table =>
                table.HasTrigger("TR_ProcurementPurchaseOrderAmendmentDispatches_Immutable"));
            entity.HasIndex(item => new { item.TenantId, item.AmendmentId, item.Sequence }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.PurchaseOrderId, item.RevisionNumber });
            entity.HasOne(item => item.Amendment)
                .WithMany(item => item.Dispatches)
                .HasForeignKey(item => item.AmendmentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.PurchaseOrder)
                .WithMany()
                .HasForeignKey(item => item.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProcurementPurchaseOrderAmendmentAcknowledgement>(entity =>
        {
            entity.ToTable("ProcurementPurchaseOrderAmendmentAcknowledgements", table =>
                table.HasTrigger("TR_ProcurementPurchaseOrderAmendmentAcknowledgements_Immutable"));
            entity.HasIndex(item => new { item.TenantId, item.DispatchId, item.Sequence }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
            entity.HasOne(item => item.Dispatch)
                .WithMany(item => item.Acknowledgements)
                .HasForeignKey(item => item.DispatchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // PurchaseOrderReceipt entity
        builder.Entity<PurchaseOrderReceipt>(entity =>
        {
            entity.ToTable("PurchaseOrderReceipts", table =>
                table.HasTrigger("TR_PurchaseOrderReceipts_GovernedSource"));
            entity.Property(item => item.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .IsRequired(false);
            entity.Property(item => item.ReceiptTolerancePercent)
                .HasColumnType("decimal(5,2)");
            entity.HasIndex(por => por.PurchaseOrderId);
            entity.HasIndex(por => por.ReceiptNumber).IsUnique();
            entity.HasIndex(por => por.ReceiptDate);
            entity.HasIndex(por => por.Status);
            entity.HasIndex(por => por.ReceivedById);
            entity.HasIndex(por => por.InspectedById);
            entity.HasIndex(por => new { por.TenantId, por.PurchaseOrderId, por.IdempotencyKey })
                .IsUnique()
                .HasFilter("[IdempotencyKey] IS NOT NULL AND [IsDeleted] = 0");

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
            entity.ToTable("PurchaseOrderReceiptItems", table =>
                table.HasTrigger("TR_PurchaseOrderReceiptItems_GovernedCapacity"));
            entity.HasIndex(pori => pori.ReceiptId);
            entity.HasIndex(pori => pori.PurchaseOrderItemId);
            entity.HasIndex(pori => pori.LocationId);
            entity.HasIndex(pori => new { pori.TenantId, pori.PurchaseOrderItemId, pori.ReceiptId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

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
            entity.HasOne<PurchaseOrderItem>().WithMany().HasForeignKey(i => i.PurchaseOrderItemId).OnDelete(DeleteBehavior.NoAction);
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
            entity.ToTable("StockAdjustments", table =>
            {
                table.HasTrigger("TR_StockAdjustments_ControlledLifecycle");
                table.HasTrigger("TR_StockAdjustments_FifoSequenceGuard");
            });
            entity.HasIndex(sa => sa.AdjustmentNumber).IsUnique();
            entity.HasIndex(sa => new { sa.TenantId, sa.IdempotencyKey }).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
            entity.HasIndex(sa => sa.AdjustmentDate);
            entity.HasIndex(sa => sa.Status);
            entity.HasIndex(sa => sa.ReasonCode);
            entity.HasIndex(sa => sa.ApprovedById);

            entity.HasOne(sa => sa.ApprovedBy)
                .WithMany()
                .HasForeignKey(sa => sa.ApprovedById)
                .OnDelete(DeleteBehavior.NoAction);

            entity.Property(sa => sa.RowVersion).IsRowVersion();
        });

        // StockAdjustmentItem entity
        builder.Entity<StockAdjustmentItem>(entity =>
        {
            entity.ToTable("StockAdjustmentItems", table => table.HasTrigger("TR_StockAdjustmentItems_ControlledMutation"));
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
            entity.ToTable("ItemUnitsOfMeasure", table =>
            {
                table.HasTrigger("TR_TDC0601_ItemUnitsOfMeasure_IdentifierIntegrity");
                table.HasCheckConstraint(
                    "CK_ItemUnitsOfMeasure_Barcode_Normalized",
                    "[Barcode] IS NULL OR [Barcode] COLLATE Latin1_General_100_BIN2 = UPPER(LTRIM(RTRIM([Barcode]))) COLLATE Latin1_General_100_BIN2");
            });
            entity.HasIndex(iu => new { iu.InventoryItemId, iu.UnitOfMeasureId }).IsUnique();
            entity.HasIndex(iu => iu.IsActive);
            entity.HasIndex(iu => iu.TenantId);
            entity.HasIndex(iu => new { iu.TenantId, iu.Barcode })
                .IsUnique()
                .HasFilter("[Barcode] IS NOT NULL AND [IsDeleted] = 0");

            entity.HasOne(iu => iu.InventoryItem)
                .WithMany(i => i.ItemUnitsOfMeasure)
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
            entity.Property(item => item.RowVersion).IsRowVersion();
            entity.HasIndex(ir => ir.RequisitionNumber).IsUnique();
            entity.HasIndex(ir => ir.WarehouseId);
            entity.HasIndex(ir => ir.DepartmentId);
            entity.HasIndex(ir => ir.OrganizationUnitId);
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

            entity.HasOne(ir => ir.OrganizationUnit)
                .WithMany()
                .HasForeignKey(ir => ir.OrganizationUnitId)
                .OnDelete(DeleteBehavior.Restrict);

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
            entity.ToTable("GoodsReceiptNotes", table =>
            {
                table.HasTrigger("TR_GoodsReceiptNotes_GovernedSource");
                table.HasTrigger("TR_TDC0501_GoodsReceiptIdempotencyFingerprint");
                table.HasCheckConstraint(
                    "CK_GoodsReceiptNotes_IdempotencyRequestHash",
                    "[IdempotencyRequestHash] IS NULL OR LEN([IdempotencyRequestHash]) = 64");
            });
            entity.Property(item => item.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken()
                .IsRequired(false);
            entity.Property(item => item.ReceiptTolerancePercent)
                .HasColumnType("decimal(5,2)");
            entity.HasIndex(grn => grn.GRNNumber).IsUnique();
            entity.HasIndex(grn => grn.WarehouseId);
            entity.HasIndex(grn => grn.SupplierId);
            entity.HasIndex(grn => grn.Status);
            entity.HasIndex(grn => grn.ReceiptDate);
            entity.HasIndex(grn => new { grn.TenantId, grn.PurchaseOrderId, grn.IdempotencyKey })
                .IsUnique()
                .HasFilter("[IdempotencyKey] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasIndex(grn => new { grn.TenantId, grn.PurchaseOrderReceiptId })
                .IsUnique()
                .HasFilter("[PurchaseOrderReceiptId] IS NOT NULL AND [IsDeleted] = 0");

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
            entity.ToTable("GoodsReceiptNoteItems", table =>
                table.HasTrigger("TR_GoodsReceiptNoteItems_GovernedCapacity"));
            entity.HasIndex(grni => grni.GoodsReceiptNoteId);
            entity.HasIndex(grni => grni.InventoryItemId);
            entity.HasIndex(grni => new { grni.TenantId, grni.PurchaseOrderItemId, grni.GoodsReceiptNoteId })
                .IsUnique()
                .HasFilter("[PurchaseOrderItemId] IS NOT NULL AND [IsDeleted] = 0");

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

        builder.Entity<LandedCostReceiptWeight>(entity =>
        {
            entity.ToTable("LandedCostReceiptWeights", table => table.HasTrigger("TR_LandedCostReceiptWeights_Source"));
            entity.HasIndex(w => new { w.TenantId, w.LandedCostId, w.GoodsReceiptNoteItemId }).IsUnique();
            entity.HasOne(w => w.LandedCost).WithMany().HasForeignKey(w => w.LandedCostId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(w => w.GoodsReceiptNoteItem).WithMany().HasForeignKey(w => w.GoodsReceiptNoteItemId).OnDelete(DeleteBehavior.NoAction);
        });

        builder.Entity<LandedCostSupplierDocument>(entity =>
        {
            entity.ToTable("LandedCostSupplierDocuments", table => table.HasTrigger("TR_LandedCostSupplierDocuments_Immutable"));
            entity.Property(d => d.DocumentNumber).HasComputedColumnSql("'LCSD-' + LOWER(REPLACE(CONVERT(varchar(36), [Id]), '-', ''))", stored: true);
            entity.HasIndex(d => new { d.TenantId, d.LandedCostItemId }).IsUnique();
            entity.HasIndex(d => new { d.TenantId, d.DocumentNumber }).IsUnique().HasFilter(null);
            entity.HasOne(d => d.LandedCostItem).WithMany().HasForeignKey(d => d.LandedCostItemId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(d => d.LandedCost).WithMany().HasForeignKey(d => d.LandedCostId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(d => d.GoodsReceiptNote).WithMany().HasForeignKey(d => d.GoodsReceiptNoteId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(d => d.PurchaseOrder).WithMany().HasForeignKey(d => d.PurchaseOrderId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(d => d.BusinessPartner).WithMany().HasForeignKey(d => d.BusinessPartnerId).OnDelete(DeleteBehavior.NoAction);
        });

        // LandedCostItem entity
        builder.Entity<LandedCostItem>(entity =>
        {
            entity.HasOne<PurchaseOrderItem>().WithMany().HasForeignKey(i => i.PurchaseOrderItemId).OnDelete(DeleteBehavior.NoAction);
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
            entity.Property(value => value.ApprovalRequired).HasDefaultValue(true);
            entity.ToTable("PurchaseReturns", table => table.HasTrigger("TR_PurchaseReturns_OptionalApproval"));
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
            entity.ToTable("InventoryMovements", table =>
                table.HasTrigger("TR_InventoryMovements_GovernedPurchaseReceipt"));
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
            entity.ToTable("InventoryBalances", table =>
                table.HasTrigger("TR_InventoryBalances_ExactBinNegativeStockGuard"));

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

    private static void ConfigureFinanceCommonEntities(ModelBuilder builder, bool isSqlServer)
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
            entity.ToTable("FinanceSettings", table =>
            {
                table.HasCheckConstraint(
                    "CK_FinanceSettings_TDC0504ApMatchTolerances",
                    "[ApInvoicePriceTolerancePercent] BETWEEN 0 AND 100 AND [ApInvoiceQuantityTolerancePercent] BETWEEN 0 AND 100");
                table.HasCheckConstraint(
                    "CK_FinanceSettings_WhtStatutoryYearStart",
                    "[WhtStatutoryYearStartMonth] BETWEEN 1 AND 12 AND [WhtStatutoryYearStartDay] BETWEEN 1 AND DAY(EOMONTH(DATEFROMPARTS(2001, [WhtStatutoryYearStartMonth], 1)))");
                if (isSqlServer)
                    table.HasCheckConstraint(
                        "CK_FinanceSettings_BaseCurrencyCanonical_C3",
                        "[IsDeleted] = 1 OR (DATALENGTH([BaseCurrency]) = 6 AND [BaseCurrency] COLLATE Latin1_General_100_BIN2 LIKE N'[A-Z][A-Z][A-Z]')");
            });
            entity.HasIndex(s => s.TenantId).IsUnique();
            entity.Property(s => s.ApInvoicePriceTolerancePercent).HasPrecision(5, 2);
            entity.Property(s => s.ApInvoiceQuantityTolerancePercent).HasPrecision(5, 2);
            entity.Property(s => s.BaseCurrency).HasMaxLength(3).IsRequired();
            entity.Property(s => s.FunctionalCurrencyLockedReason).HasMaxLength(500);
            // SQL default matches the TDC control default even for maintenance/import inserts
            // that do not instantiate the C# entity property initializer.
            entity.Property(s => s.RequireDepreciationBeforePeriodClose).HasDefaultValue(true);
            entity.Property(s => s.WhtStatutoryYearStartMonth).HasDefaultValue(1);
            entity.Property(s => s.WhtStatutoryYearStartDay).HasDefaultValue(1);
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
            entity.Property(s => s.MaximumDepositDeductionAmount).HasColumnType("decimal(18,2)");
            entity.Property(s => s.MaximumDepositDeductionPercentage).HasColumnType("decimal(9,4)");
            entity.HasOne(s => s.ReturnedChequeBankChargeAccount)
                .WithMany()
                .HasForeignKey(s => s.ReturnedChequeBankChargeAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ExchangeRate>(entity =>
        {
            entity.HasAlternateKey(r => new { r.TenantId, r.Id });
            entity.HasIndex(r => new { r.TenantId, r.BaseCurrencyCode, r.TargetCurrencyCode, r.RateType, r.QuoteSide, r.EffectiveDate })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(r => new { r.TenantId, r.BaseCurrencyCode, r.TargetCurrencyCode, r.RateType, r.QuoteSide, r.IsActive });
            entity.HasIndex(r => r.HasBeenUsedInTransactions);
            entity.Property(r => r.BaseCurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(r => r.TargetCurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(r => r.RateSource).HasMaxLength(100).IsRequired();
        });

        builder.Entity<FinanceAccessScopeGrant>(entity =>
        {
            entity.ToTable("FinanceAccessScopeGrants");
            // Database checks mirror service validation so direct imports or maintenance scripts
            // cannot introduce grants that the runtime could misinterpret during authorization.
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_FinanceAccessScopeGrants_AccessLevel",
                    "[AccessLevel] IN (1, 2, 3, 4)");
                table.HasCheckConstraint(
                    "CK_FinanceAccessScopeGrants_EffectivePeriod",
                    "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                table.HasCheckConstraint(
                    "CK_FinanceAccessScopeGrants_ScopeType",
                    "[ScopeType] IN (1, 2, 3, 4, 5)");
                table.HasCheckConstraint(
                    "CK_FinanceAccessScopeGrants_ScopeValue",
                    "([ScopeType] = 1 AND [ScopeValue] IS NULL) OR ([ScopeType] <> 1 AND [ScopeValue] IS NOT NULL)");
            });
            entity.Property(item => item.ScopeValue).HasMaxLength(100);
            entity.Property(item => item.Reason).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.RowVersion).IsRowVersion();
            entity.HasIndex(item => new { item.TenantId, item.UserId, item.IsActive });
            entity.HasIndex(item => new { item.TenantId, item.UserId, item.ScopeType, item.ScopeValue })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [IsActive] = 1");
            entity.HasOne(item => item.User)
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Tenant)
                .WithMany()
                .HasForeignKey(item => item.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TaxConfigurationVersion>(entity =>
        {
            entity.HasIndex(v => new { v.TenantId, v.TaxId, v.VersionNumber })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasOne(v => v.Tax)
                .WithMany(t => t.ConfigurationVersions)
                .HasForeignKey(v => v.TaxId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<WithholdingTaxCertificate>(entity =>
        {
            // Certificate numbers are statutory evidence identifiers. The filtered unique
            // indexes protect both tenant numbering and the invariant of one currently issued
            // version per payment while retaining cancelled/superseded history.
            entity.HasIndex(item => new { item.TenantId, item.CertificateNumber })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.VendorPaymentId, item.VersionNumber })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.VendorPaymentId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0 AND [Status] = 1");
            entity.Property(item => item.RowVersion).IsRowVersion();
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_WithholdingTaxCertificates_Version", "[VersionNumber] >= 1");
                table.HasCheckConstraint("CK_WithholdingTaxCertificates_Amounts", "[TaxableBase] >= 0 AND [WithholdingAmount] > 0 AND [NetPaidAmount] >= 0");
            });
            entity.HasOne(item => item.VendorPayment)
                .WithMany()
                .HasForeignKey(item => item.VendorPaymentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SupersedesCertificate)
                .WithMany()
                .HasForeignKey(item => item.SupersedesCertificateId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SupersededByCertificate)
                .WithMany()
                .HasForeignKey(item => item.SupersededByCertificateId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(item => item.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<WithholdingTaxRemittance>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.RemittanceNumber })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.Status, item.PeriodTo });
            entity.Property(item => item.RowVersion).IsRowVersion();
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_WithholdingTaxRemittances_Period", "[PeriodTo] >= [PeriodFrom] AND [DueDate] >= [PeriodTo]");
                table.HasCheckConstraint("CK_WithholdingTaxRemittances_Total", "[TotalWithholdingAmount] > 0");
            });
        });

        builder.Entity<WithholdingTaxRemittanceLine>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.VendorPaymentId });
            entity.HasIndex(item => new { item.TenantId, item.RemittanceId, item.PaymentNumber });
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_WithholdingTaxRemittanceLines_Amounts",
                "[TaxableBase] >= 0 AND [WithholdingAmount] > 0"));
            entity.HasOne(item => item.Remittance)
                .WithMany(item => item.Lines)
                .HasForeignKey(item => item.RemittanceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.VendorPayment)
                .WithMany()
                .HasForeignKey(item => item.VendorPaymentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Certificate)
                .WithMany()
                .HasForeignKey(item => item.CertificateId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<JournalEntry>()
                .WithMany()
                .HasForeignKey(item => item.JournalEntryId)
                .OnDelete(DeleteBehavior.Restrict);
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
            entity.HasAlternateKey(item => new { item.TenantId, item.ProfileId, item.Id });
            entity.HasIndex(item => new { item.TenantId, item.ProfileId, item.DecisionKey }).IsUnique();
            entity.HasIndex(item => new { item.TenantId, item.DecisionKey, item.Status });
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_ProcurementConfigurationDecisions_SchemaVersion", "[SchemaVersion] > 0");
                table.HasCheckConstraint("CK_ProcurementConfigurationDecisions_DecisionKey", "[DecisionKey] LIKE 'DEC-[0-9][0-9][0-9]'");
                table.HasCheckConstraint("CK_ProcurementConfigurationDecisions_EffectivePeriod", "[EffectiveTo] IS NULL OR [EffectiveFrom] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                table.HasCheckConstraint("CK_ProcurementConfigurationDecisions_Status", "[Status] IN (0, 1, 2, 3) OR ([Status] = 4 AND [DecisionKey] = 'DEC-011')");
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
                table.HasCheckConstraint("CK_ProcurementResponsibilityAssignments_LocationScope", "[LocationScopeMode] IN (0, 1, 2)");
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

        builder.Entity<ProcurementResponsibilityLocation>(entity =>
        {
            entity.HasIndex(item => new { item.TenantId, item.AssignmentId, item.WarehouseLocationId })
                .IsUnique().HasFilter("[IsDeleted] = 0");
            entity.HasIndex(item => new { item.TenantId, item.WarehouseId, item.WarehouseLocationId });
            entity.HasOne(item => item.Assignment).WithMany(item => item.Locations)
                .HasForeignKey(item => item.AssignmentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Warehouse).WithMany()
                .HasForeignKey(item => item.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.WarehouseLocation).WithMany()
                .HasForeignKey(item => item.WarehouseLocationId).OnDelete(DeleteBehavior.Restrict);
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
                table.HasCheckConstraint("CK_ProcurementControlEvents_Operation", "[Operation] BETWEEN 0 AND 9");
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
            entity.HasIndex(item => new { item.TenantId, item.Operation, item.OccurredAtUtc });
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

    private static void ConfigureAuditGovernance(ModelBuilder builder)
    {
        builder.Entity<DataRetentionPolicy>(entity =>
        {
            entity.ToTable("DataRetentionPolicies", table =>
            {
                table.HasCheckConstraint("CK_DataRetentionPolicies_AuditMinimum",
                    "[AuditLogRetentionDays] >= 2555");
                table.HasCheckConstraint("CK_DataRetentionPolicies_WorkflowAuditMinimum",
                    "[WorkflowAuditRetentionDays] >= 2555");
            });
        });

        builder.Entity<AuditRecordLifecycleEvent>(entity =>
        {
            entity.ToTable("AuditRecordLifecycleEvents", table =>
            {
                table.HasTrigger("TR_AuditRecordLifecycleEvents_AppendOnly");
                table.HasCheckConstraint("CK_AuditRecordLifecycleEvents_Action", "[Action] BETWEEN 0 AND 3");
                table.HasCheckConstraint("CK_AuditRecordLifecycleEvents_Sequence", "[SequenceNumber] >= 1");
                table.HasCheckConstraint("CK_AuditRecordLifecycleEvents_Retention",
                    "[RetainUntilUtc] >= DATEADD(day, 2555, [SourceOccurredAtUtc])");
            });
            entity.Property(value => value.StoreKey).IsUnicode(false);
            entity.Property(value => value.RequestKey).IsUnicode(false);
            entity.Property(value => value.CorrelationId).IsUnicode(false);
            entity.Property(value => value.PreviousIntegrityHash).IsUnicode(false).IsFixedLength();
            entity.Property(value => value.IntegrityHash).IsUnicode(false).IsFixedLength();
            entity.HasIndex(value => new { value.TenantId, value.RequestKey }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.StoreKey, value.RecordId, value.SequenceNumber }).IsUnique();
            entity.HasIndex(value => new { value.TenantId, value.StoreKey, value.RecordId, value.CreatedAt });
            entity.HasOne(value => value.ActorUser).WithMany().HasForeignKey(value => value.ActorUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(value => value.Tenant).WithMany().HasForeignKey(value => value.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureProcurementCentralDocumentLinks<TEntity>(ModelBuilder builder)
        where TEntity : TenantEntity
    {
        var entity = builder.Entity<TEntity>();
        entity.HasIndex("TenantId", "FileUploadRecordId")
            .HasFilter("[FileUploadRecordId] IS NOT NULL");
        entity.HasIndex("TenantId", "CentralDocumentRecordId")
            .IsUnique()
            .HasFilter("[CentralDocumentRecordId] IS NOT NULL");
        entity.HasIndex("TenantId", "CentralDocumentVersionId")
            .IsUnique()
            .HasFilter("[CentralDocumentVersionId] IS NOT NULL");
        entity.HasOne<FileUploadRecord>()
            .WithMany()
            .HasForeignKey("FileUploadRecordId")
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<CentralDocumentRecord>()
            .WithMany()
            .HasForeignKey("CentralDocumentRecordId")
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<CentralDocumentVersion>()
            .WithMany()
            .HasForeignKey("CentralDocumentVersionId")
            .OnDelete(DeleteBehavior.Restrict);
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
