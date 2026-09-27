# Procurement regression evidence — 24 September 2026

These are broad-suite observations, not a clean release gate. A failing test is not automatically a regression introduced by this task; a passing focused test does not erase a broad-suite failure.

25 September follow-up: the original broad-run observations below remain intact. The isolated baseline comparison is recorded in [procurement-baseline-comparison-20260925.md](procurement-baseline-comparison-20260925.md), and the migrated verification environment in [procurement-verification-20260925.md](procurement-verification-20260925.md). The legacy restore blocker is resolved: Logging.Abstractions now matches the API's actually resolved 10.0.0 dependency, and all 79 legacy tests passed. The previous incomplete-run statement below is historical, not the current legacy-suite outcome.

Confirmed follow-up evidence:

- The 18 receipt-account tests initially failed before their assertions because their fixtures lacked accounting-book identities required by the existing persistence guard. After supplying those identities, all 18 passed together with all 25 Auto Invoice tests (`tmp/procurement-approved-source-tests.log`). No production validation or test assertion was relaxed.
- Three existing procurement reconciliation failures reproduced with both the service and test source from HEAD (`tmp/procurement-reconciliation-baseline-tests.log`). The two new consolidated-invoice reconciliation tests pass.
- API host startup failures include the exact configuration error: `CandidatePortal:PortalUrl must be configured with an absolute portal base URL.` Migration discovery/archive expectations and other Finance fixtures also fail. These categories have not all been independently reproduced against HEAD.
- Core failures include obsolete archive/migration expectations and missing permission/repository/organisation fixtures. Their full baseline classification is outstanding; do not describe all failures as pre-existing.

## ErpSystem.Core.Tests

Failed!  - Failed:    76, Passed:  3372, Skipped:    34, Total:  3482, Duration: 5 m 34 s - ErpSystem.Core.Tests.dll (net8.0)

Failure records observed: 76.

| Test class | Failed cases |
|---|---:|
| `ErpSystem.Core.Tests.Services.Crm.CrmServiceTests` | 5 |
| `ErpSystem.Core.Tests.Services.Inventory.E2E010ProjectMaterialLifecycleTests` | 2 |
| `ErpSystem.Core.Tests.Services.Inventory.E2E012CycleCountFinanceLifecycleTests` | 1 |
| `ErpSystem.Core.Tests.Services.Inventory.InventoryRequisitionDraftTests` | 16 |
| `ErpSystem.Core.Tests.Services.Inventory.InventoryReturnAdjustmentControlTests` | 1 |
| `ErpSystem.Core.Tests.Services.Inventory.StockAdjustmentCountFreezeOrderingTests` | 5 |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementBidderCommunicationServiceTests` | 1 |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementBudgetCommitmentLifecycleMigrationGuardTests` | 1 |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementContractBudgetLifecycleRegressionTests` | 1 |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementControlEventServiceTests` | 1 |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementFrameworkCallOffExpiryServiceTests` | 1 |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementPlanNumberReservationTests` | 3 |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionAuthorityRouteServiceTests` | 1 |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionLinkageServiceTests` | 7 |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionSubmissionControlServiceTests` | 5 |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementReviewRegressionTests` | 1 |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementSpecificationTemplateServiceTests` | 4 |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierApplicantAccessServiceTests` | 2 |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierAvlServiceTests` | 5 |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierDueDiligenceServiceTests` | 6 |
| `ErpSystem.Core.Tests.Services.Procurement.PurchaseOrderLineRulesTests` | 2 |
| `ErpSystem.Core.Tests.Services.Procurement.PurchaseRequisitionLinePlanItemLineageMigrationTests` | 1 |
| `ErpSystem.Core.Tests.Services.Procurement.SupplierOnboardingCategoryAlignmentMigrationTests` | 1 |
| `ErpSystem.Core.Tests.Services.Procurement.TenderPaymentFinancePostingTests` | 1 |
| `ErpSystem.Core.Tests.Services.Procurement.TenderPaymentVerificationPermissionMigrationTests` | 1 |
| `ErpSystem.Core.Tests.Services.Projects.ProjectAssetLinkFixedAssetReconciliationMigrationGuardTests` | 1 |

Full local log: `tmp/procurement-full-ErpSystem.Core.Tests.log`.

## ErpSystem.Api.Tests

Failed!  - Failed:   312, Passed:  3362, Skipped:    64, Total:  3738, Duration: 23 m 49 s - ErpSystem.Api.Tests.dll (net8.0)

Failure records observed: 312.

| Test class | Failed cases |
|---|---:|
| `ErpSystem.Api.Tests.Controllers.Auth.AuthControllerRouteTests` | 1 |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests` | 31 |
| `ErpSystem.Api.Tests.Controllers.CrossModuleLegacyAuthorizationSecurityTests` | 2 |
| `ErpSystem.Api.Tests.Controllers.DocumentManagement.PaymentDocumentScopeTests` | 1 |
| `ErpSystem.Api.Tests.Controllers.Estate.PropertyListingEnquiryTests` | 1 |
| `ErpSystem.Api.Tests.Controllers.Finance.FinanceControllerSecurityTests` | 2 |
| `ErpSystem.Api.Tests.Controllers.Finance.PaymentMethodControllerTests` | 1 |
| `ErpSystem.Api.Tests.Controllers.Procurement.BusinessPartnersControllerContactRouteTests` | 1 |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementAccessControlsControllerTests` | 3 |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementAppSubmissionsControllerTests` | 3 |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementCalendarControllerTests` | 2 |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementComplianceDecisionsControllerTests` | 4 |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementConfigurationProfilesControllerTests` | 1 |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementControlEventsControllerTests` | 3 |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementGhanepsExchangesControllerTests` | 2 |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementLegacyRoleAuthorizationSecurityTests` | 1 |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementMasterDataChangesControllerTests` | 5 |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementPolicySetsControllerTests` | 3 |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementSodControlsControllerTests` | 5 |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementSourcingCasesControllerTests` | 1 |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementSpecificationTemplatesControllerTests` | 2 |
| `ErpSystem.Api.Tests.Controllers.Procurement.PurchaseRequisitionAuthorityControlsControllerTests` | 1 |
| `ErpSystem.Api.Tests.Controllers.Procurement.PurchaseRequisitionBudgetControlsControllerTests` | 1 |
| `ErpSystem.Api.Tests.Controllers.Procurement.PurchaseRequisitionLinkagesControllerTests` | 1 |
| `ErpSystem.Api.Tests.Controllers.Procurement.PurchaseRequisitionSourcingReleaseControllerTests` | 1 |
| `ErpSystem.Api.Tests.Controllers.Procurement.PurchaseRequisitionSubmissionControlsControllerTests` | 1 |
| `ErpSystem.Api.Tests.Controllers.Projects.ProjectMobileControllerTests` | 1 |
| `ErpSystem.Api.Tests.Middleware.GlobalExceptionHandlingMiddlewareTests` | 1 |
| `ErpSystem.Api.Tests.Services.ClamAvFileVirusScanServiceTests` | 2 |
| `ErpSystem.Api.Tests.Services.DatabaseSeedingDependencyGraphTests` | 1 |
| `ErpSystem.Api.Tests.Services.DocumentManagement.CentralDocumentWordConversionTests` | 2 |
| `ErpSystem.Api.Tests.Services.Documents.DmsGenerationTemplateMigrationCompatibilityTests` | 1 |
| `ErpSystem.Api.Tests.Services.Estate.EstateWorkflowIntegrationRegressionTests` | 1 |
| `ErpSystem.Api.Tests.Services.Finance.AccountBookCurrencyPolicyServiceTests` | 5 |
| `ErpSystem.Api.Tests.Services.Finance.AccountSegmentIdentityPhase5Tests` | 1 |
| `ErpSystem.Api.Tests.Services.Finance.AccountingBookApplicabilityC5MigrationTests` | 1 |
| `ErpSystem.Api.Tests.Services.Finance.AccountingBookLifecycleC3MigrationTests` | 1 |
| `ErpSystem.Api.Tests.Services.Finance.AccountingBookPeriodInitializationC4MigrationTests` | 1 |
| `ErpSystem.Api.Tests.Services.Finance.AccountingPeriodClosePostingDateTests` | 1 |
| `ErpSystem.Api.Tests.Services.Finance.ApInvoicePartnerDefaultsTests` | 1 |
| `ErpSystem.Api.Tests.Services.Finance.ApInvoicePostingMigrationTests` | 2 |
| `ErpSystem.Api.Tests.Services.Finance.ApPaymentVoucherDocumentBuilderTests` | 3 |
| `ErpSystem.Api.Tests.Services.Finance.ApReceiptAccountResolutionTests` | 18 |
| `ErpSystem.Api.Tests.Services.Finance.BudgetServiceHardeningTests` | 1 |
| `ErpSystem.Api.Tests.Services.Finance.CashBankControlledDocumentBuilderTests` | 3 |
| `ErpSystem.Api.Tests.Services.Finance.CashierTillControlTests` | 1 |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests` | 28 |
| `ErpSystem.Api.Tests.Services.Finance.CoreFinancialReportingFoundationTests` | 12 |
| `ErpSystem.Api.Tests.Services.Finance.DatabaseSeedFinanceProvisioningIsolationTests` | 1 |
| `ErpSystem.Api.Tests.Services.Finance.FinanceBudgetCommitmentServiceTests` | 3 |
| `ErpSystem.Api.Tests.Services.Finance.FinanceBudgetControlServiceTests` | 9 |
| `ErpSystem.Api.Tests.Services.Finance.FinanceConcurrencyHardeningTests` | 2 |
| `ErpSystem.Api.Tests.Services.Finance.FinancePostingEngineTests` | 1 |
| `ErpSystem.Api.Tests.Services.Finance.FinanceSettingsMigrationGapTests` | 1 |
| `ErpSystem.Api.Tests.Services.Finance.FinanceSettingsWriteOffMappingTests` | 2 |
| `ErpSystem.Api.Tests.Services.Finance.FinanceSourceDimensionServiceTests` | 1 |
| `ErpSystem.Api.Tests.Services.Finance.FinancialStatementClassificationLayoutPhase3Tests` | 1 |
| `ErpSystem.Api.Tests.Services.Finance.FinancialStatementLayoutServiceTests` | 1 |
| `ErpSystem.Api.Tests.Services.Finance.FixedAssetCapitalizationFoundationTests` | 1 |
| `ErpSystem.Api.Tests.Services.Finance.FixedAssetDepreciationServiceTests` | 1 |
| `ErpSystem.Api.Tests.Services.Finance.FixedAssetReportingReconciliationFoundationTests` | 15 |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests` | 51 |
| `ErpSystem.Api.Tests.Services.Finance.GhanaTaxReportingExportFoundationTests` | 7 |
| `ErpSystem.Api.Tests.Services.Finance.JournalBatchSpreadsheetServiceTests` | 1 |
| `ErpSystem.Api.Tests.Services.Finance.JournalVoucherDocumentBuilderTests` | 10 |
| `ErpSystem.Api.Tests.Services.Finance.ProcurementFinanceReconciliationTests` | 3 |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests` | 26 |
| `ErpSystem.Api.Tests.Services.Finance.SupplierDebitNoteFoundationTests` | 1 |
| `ErpSystem.Api.Tests.Services.Finance.WhtCertificateDocumentBuilderTests` | 3 |
| `ErpSystem.Api.Tests.Services.Finance.WhtComplianceLifecycleTests` | 2 |
| `ErpSystem.Api.Tests.Services.Inventory.InventoryDisposalServiceTests` | 1 |

Full local log: `tmp/procurement-full-ErpSystem.Api.Tests.log`.

## ErpSystem.Tests

The first run failed with NETSDK1005 (missing net8.0 assets). Restore was then attempted and failed with NU1605: the test project pins Microsoft.Extensions.Logging.Abstractions 8.0.3, but the existing API dependency requires 9.0.0. No tests executed. See `tmp/procurement-legacy-restore.log`.

No terminal test summary was emitted. This run is incomplete or stopped before test completion.

Failure records observed: 0.

| Test class | Failed cases |
|---|---:|

Full local log: `tmp/procurement-full-ErpSystem.Tests.log`.
