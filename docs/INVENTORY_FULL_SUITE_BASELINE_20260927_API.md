# API full-suite regression triage — 2026-09-27

Fresh-process follow-up: `api-fresh-host-rerun.trx` completed 40 cases from `VendorInvoiceMatchExceptionServiceTests`, `UnitOfWorkTransactionLockTests` and `FinanceSettingsWriteOffMappingTests`: 38 passed, two failed, no OOM. All four previously OOM-affected cases passed. The remaining failures are the unchanged `expense`/`recovery` foreign-tenant write-off fixtures mutating an already persisted account's tenant key before their intended service call. This follow-up used the original normal-metadata test binary, before the final combined migration/recovery changes.

The coordinator attempted the complete API test project against normal migration metadata (not the fast-EF assembly). The monolithic testhost hit its 3 GiB memory cap and the coordinator stopped it. The run is **aborted**, not complete: partial results are 2,180 passed, 218 failed, 35 skipped, 2,433 recorded cases. Evidence: `tmp/inventory-full-api-tests.log` and `tmp/inventory-workflows-final-tests/api-full.trx`. This document records source-level triage; it does not claim a separate clean-HEAD execution or turn failed tests into passed acceptance.

The first explicit OutOfMemoryException is log line 4211, in `VendorInvoiceMatchExceptionServiceTests.OverviewExposesRequestCapabilityOnlyToApInvoiceManagers(canManage: True)` at elapsed 10:57.92. There are 198 failure records before that failed case and 20 at/after it; four failure bodies explicitly show OOM. Failures at/after that boundary require fresh-host reruns before behavior can be judged. In particular, unchanged source hashes do **not** classify those failures as baseline product defects. The coordinator is running bounded fresh-host batches and will retest affected families against the final migration build.

Comparison base: `1413bb6fa5ac2a6ec20637ceb03416669a333e63`. Git blob identities below compare tracked source with that HEAD, including Git's normal line-ending normalization. No production changes, runtime database mutations, compiler processes or assertion relaxations were made by this triage.

## Diagnosed unchanged boundaries

- **CandidatePortal startup configuration:** affected WebApplicationFactory tests fail during startup before their endpoint executes because `CandidatePortal:PortalUrl` is absent. The option validator and tests are unchanged. `ServiceCollectionExtensions.cs` is modified for Sales/disposal registrations, but its CandidatePortal binding/ValidateOnStart block at lines 174–178 is unchanged.
- **Finance budget fixtures:** the prior journal in `FinanceBudgetControlServiceTests.cs:233` omits AccountingBookId; SaveChanges at 253 fails before the intended budget evaluation. The budget service/test and existing journal composite-book mapping are unchanged. Related commitment cases likewise fail during fixture persistence and need Finance-owned canonical fixture repair.
- **Accounting-book lifecycle:** tests manually request PrimaryFull creation or use legacy authority fixtures. Existing AccountingBookService rejects manual Primary creation before the expected currency assertion; other cases fail existing lineage/state setup. Test and service are unchanged.
- **Fixed-asset depreciation:** old fixtures fail book-functional-currency or fiscal-period authority before depreciation assertions. For example, the service includes the authoritative FiscalYear at lines 64–67; the older test's period fixture does not provide a corresponding year. Test/service files are unchanged.
- **Exact migration inventory:** two C3/C4 tests require the migration set to contain only the disposable baseline, while the normal assembly contains the existing full chain. This is distinct from missing migration metadata; do not weaken it silently or claim migration deployment acceptance.
- **Legacy role audit:** the audit reports existing SuperAdmin/TenantAdmin gates on BusinessPartnersController and BusinessPartnerUsersController. Both controllers and the audit are unchanged.
- **Finance permission audit:** `CurrenciesController.GetActive` has `Finance.Policy.ProjectCurrencyLookup`, which the audit does not accept as a registered Finance policy. Controller, permission registry and test are unchanged.
- **Finance source audit false positive:** `VendorInvoiceService.ReceiptAccounts.cs:82` explicitly filters the required tenant, then coalesces the nullable movement **ReferenceId**, not TenantId. The unchanged text-based test flags any line containing TenantId and `?? Guid.Empty`; this does not demonstrate tenant fallback.
- **Other unchanged families requiring their owners:** Estate supplier-source text assertions, database seeder exact count 11 versus 12, CashBank FX amount expectations and PaymentDocumentScope's expected Forbid result are tracked separately. No broad Finance/Estate/security fixes were attempted here.
- **FX settlement/revaluation fixture:** many AP/AR-named cases fail in `FxFixture.CreateAsync` at `FxRealizedUnrealizedRevaluationTests.cs:1276`, before their individual financial operations. The fixture calls FinanceClassificationManifestSeeder then requires one IFRS book; the query finds none. Keep this distinct from an observed invoice-posting regression.
- **Account-book currency-policy fixture:** SQLite rejects legacy book shapes with `CK_AccountingBooks_BaseShape` before the intended policy operation. Other tests in that family assert an obsolete exact migration list.
- **Estate Sales queue:** `PropertyListingEnquiryTests.SalesQueueRequiresSalesAndMarketingOrganizationUnitAndStatusBeyondNew` expects one result but gets two; this remains an Estate queue issue to triage independently from the new Sales invoice adapter.
- **Write-off fixture after the memory boundary:** the foreign-tenant cases mutate an already-saved Account.TenantId at test line 94, then SaveChanges at 100; TenantId participates in its alternate key. This is an invalid EF key mutation. The call to DetectChanges in `ApplicationDbContext.InventoryCosts.cs` is **already present in HEAD**, exact blob `be90296112b95b622f7f2d41c32bf18beaa45aaf`; the complete SaveChanges override block also matches HEAD. Thus the observed detection ordering was not added by this working-tree change. Test blob `7927262e99e41d8fb066f3f9ecb97fdbda7b2f97` likewise matches HEAD. Fresh-host rerun remains the runtime discriminator after OOM; no fixture/assertion was changed by this triage.

## Inventory and supplier accounting boundary

The earlier supplemental receipt-distribution fixture was corrected to specify its intended GHS PO currency explicitly; the entity's USD default was inconsistent with its unchanged 96/80/16 expectations. Two additional public preview cases cover approved foreign conversion and rejection of a pending rate. No approved-rate requirement was removed. All 17 supplier receipt-cost lifecycle cases previously passed the coordinated checkpoint; full-suite outcomes must still be read from this run's final report.

Source equality alone cannot prove the entire application has no regression. Retain full-suite failures as unresolved tests and report separately from focused Inventory acceptance, normal builds, migration/SQL acceptance and visible live checks.

## Captured run and source evidence

The following generated section is refreshed from the running log and immutable HEAD identities as the run settles.

<!-- captured evidence -->

Captured UTC: 2026-09-27 14:01:56

ABORTED; partial results only. Failed!  - Failed:   218, Passed:  2180, Skipped:    35, Total:  2433, Duration: 12 m 15 s - ErpSystem.Api.Tests.dll (net8.0)

| Failed test class | Observed failed cases |
| --- | ---: |
| ErpSystem.Api.Tests.Controllers.Auth.AuthControllerRouteTests | 1 |
| ErpSystem.Api.Tests.Controllers.CrossModuleLegacyAuthorizationSecurityTests | 2 |
| ErpSystem.Api.Tests.Controllers.DocumentManagement.PaymentDocumentScopeTests | 1 |
| ErpSystem.Api.Tests.Controllers.Estate.PropertyListingEnquiryTests | 1 |
| ErpSystem.Api.Tests.Controllers.Finance.FinanceControllerSecurityTests | 2 |
| ErpSystem.Api.Tests.Controllers.Procurement.ProcurementAccessControlsControllerTests | 3 |
| ErpSystem.Api.Tests.Controllers.Procurement.ProcurementComplianceDecisionsControllerTests | 4 |
| ErpSystem.Api.Tests.Controllers.Procurement.ProcurementControlEventsControllerTests | 3 |
| ErpSystem.Api.Tests.Controllers.Procurement.ProcurementGhanepsExchangesControllerTests | 2 |
| ErpSystem.Api.Tests.Controllers.Procurement.ProcurementLegacyRoleAuthorizationSecurityTests | 1 |
| ErpSystem.Api.Tests.Controllers.Procurement.ProcurementMasterDataChangesControllerTests | 5 |
| ErpSystem.Api.Tests.Controllers.Procurement.ProcurementPolicySetsControllerTests | 3 |
| ErpSystem.Api.Tests.Controllers.Procurement.ProcurementSourcingCasesControllerTests | 1 |
| ErpSystem.Api.Tests.Controllers.Procurement.ProcurementSpecificationTemplatesControllerTests | 2 |
| ErpSystem.Api.Tests.Controllers.Procurement.PurchaseRequisitionBudgetControlsControllerTests | 1 |
| ErpSystem.Api.Tests.Controllers.Procurement.PurchaseRequisitionLinkagesControllerTests | 1 |
| ErpSystem.Api.Tests.Controllers.Procurement.PurchaseRequisitionSubmissionControlsControllerTests | 1 |
| ErpSystem.Api.Tests.Services.DatabaseSeedingDependencyGraphTests | 1 |
| ErpSystem.Api.Tests.Services.Documents.DmsGenerationTemplateMigrationCompatibilityTests | 1 |
| ErpSystem.Api.Tests.Services.Estate.EstateWorkflowIntegrationRegressionTests | 2 |
| ErpSystem.Api.Tests.Services.Finance.AccountBookCurrencyPolicyServiceTests | 5 |
| ErpSystem.Api.Tests.Services.Finance.AccountingBookApplicabilityC5MigrationTests | 1 |
| ErpSystem.Api.Tests.Services.Finance.AccountingBookClassificationAuthorityTests | 8 |
| ErpSystem.Api.Tests.Services.Finance.AccountingBookLifecycleC3MigrationTests | 1 |
| ErpSystem.Api.Tests.Services.Finance.AccountingBookLifecycleC3Tests | 33 |
| ErpSystem.Api.Tests.Services.Finance.AccountingBookPeriodInitializationC4MigrationTests | 1 |
| ErpSystem.Api.Tests.Services.Finance.BaseDeltaReportDocumentBuilderTests | 1 |
| ErpSystem.Api.Tests.Services.Finance.BudgetServiceHardeningTests | 1 |
| ErpSystem.Api.Tests.Services.Finance.CashBankTransactionPostingMigrationTests | 2 |
| ErpSystem.Api.Tests.Services.Finance.CoreFinancialReportingFoundationTests | 12 |
| ErpSystem.Api.Tests.Services.Finance.DatabaseSeedFinanceProvisioningIsolationTests | 1 |
| ErpSystem.Api.Tests.Services.Finance.FinanceBudgetCommitmentServiceTests | 3 |
| ErpSystem.Api.Tests.Services.Finance.FinanceBudgetControlServiceTests | 1 |
| ErpSystem.Api.Tests.Services.Finance.FinanceSettingsMigrationGapTests | 1 |
| ErpSystem.Api.Tests.Services.Finance.FinanceSettingsWriteOffMappingTests | 2 |
| ErpSystem.Api.Tests.Services.Finance.FinancialStatementLayoutServiceTests | 1 |
| ErpSystem.Api.Tests.Services.Finance.FixedAssetCapitalizationFoundationTests | 17 |
| ErpSystem.Api.Tests.Services.Finance.FixedAssetDepreciationFoundationTests | 12 |
| ErpSystem.Api.Tests.Services.Finance.FixedAssetDepreciationServiceTests | 1 |
| ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests | 51 |
| ErpSystem.Api.Tests.Services.Finance.GhanaStatutoryTaxEngineTests | 10 |
| ErpSystem.Api.Tests.Services.Finance.JournalBatchSpreadsheetServiceTests | 1 |
| ErpSystem.Api.Tests.Services.Finance.JournalVoucherDocumentBuilderTests | 10 |
| ErpSystem.Api.Tests.Services.Finance.VendorInvoiceMatchExceptionServiceTests | 3 |
| ErpSystem.Api.Tests.Services.UnitOfWorkTransactionLockTests | 1 |

| Source | HEAD Git blob | Worktree equals HEAD |
| --- | --- | --- |
| src/ErpSystem.Api/Controllers/DocumentManagement/DocumentManagementController.cs | 06e75ba66f9009b4b7c0d9fdb390795554466c8b | True |
| src/ErpSystem.Api/Controllers/Estate/LandAcquisitionsController.cs | 8d9397dd19dad74cf35d1b4f74aabf8cdecc2a7e | True |
| src/ErpSystem.Api/Controllers/Finance/CurrenciesController.cs | f9d55efcb4da1d09f503844c57f9be5717f70bad | True |
| src/ErpSystem.Api/Controllers/Procurement/BusinessPartnersController.cs | 690412022102675d5d59c5c476852304d0d47cb2 | True |
| src/ErpSystem.Api/Controllers/Procurement/BusinessPartnerUsersController.cs | fdeca48a66605619ea0f198388b55a39f3803cfb | True |
| src/ErpSystem.Api/Services/DatabaseSeedingService.cs | 572d59d1128c434be22be47c7734571f581ff0ba | True |
| src/ErpSystem.Api/Services/Finance/AP/VendorInvoiceService.ReceiptAccounts.cs | 79bd1f8543e5cc3d1687dca0def97ecc12962d7c | True |
| src/ErpSystem.Api/Services/Finance/Budget/FinanceBudgetCommitmentService.cs | 9381d9d90c65cf530e971de0c14fbdf4602dfe26 | True |
| src/ErpSystem.Api/Services/Finance/Budget/FinanceBudgetControlService.cs | 68c7dfdc7542d0bbf2b2a1d255cb29c9565c67f8 | True |
| src/ErpSystem.Api/Services/Finance/Cash/CashTransactionService.cs | 61fd6ae7eba3b83a5acd7c129ce0cafaee172699 | True |
| src/ErpSystem.Api/Services/Finance/FixedAssets/FixedAssetDepreciationService.cs | 3f0997aa62f1e70d2ca55ddd62bd25ebd5d7c092 | True |
| src/ErpSystem.Api/Services/Finance/MultiCurrency/AccountBookCurrencyPolicyService.cs | 413edde03c2caa8ed5527791466cb50be4d7348e | True |
| src/ErpSystem.Api/Services/Finance/Settings/AccountingBookService.cs | e62afbf7fe003f6fb17c565d8b4d8536b9d55b14 | True |
| src/ErpSystem.Core/Models/CandidatePortalOptions.cs | 7688b78d44fe2e406cb7d8d3d51b22a009fed25c | True |
| src/ErpSystem.Data/ApplicationDbContext.InventoryCosts.cs | be90296112b95b622f7f2d41c32bf18beaa45aaf | True |
| src/ErpSystem.Data/Seeders/FinanceClassificationManifestSeeder.cs | 31c9a6f460ac61ec6cea936a5b80f1b79c02dbbe | True |
| src/ErpSystem.Shared/FinancePermissions.cs | 0f0efd1f0542d707c66db5d0a5c6657fd6cba54f | True |
| tests/ErpSystem.Api.Tests/Controllers/Auth/AuthControllerRouteTests.cs | 48aebff164cf32b97bd490134d221ba4772dc353 | True |
| tests/ErpSystem.Api.Tests/Controllers/CrossModuleLegacyAuthorizationSecurityTests.cs | bbc026c715eec3ba966ff9ff8d299a103dd0ec6c | True |
| tests/ErpSystem.Api.Tests/Controllers/DocumentManagement/PaymentDocumentScopeTests.cs | 7c9385cf3cf521a5520f8deeb677dabaef286051 | True |
| tests/ErpSystem.Api.Tests/Controllers/Estate/PropertyListingEnquiryTests.cs | 9a784d840ca12db981d806ab1722dea328d93397 | True |
| tests/ErpSystem.Api.Tests/Controllers/Finance/FinanceControllerSecurityTests.cs | c5d73aaaeb8a8ae339ba04acdfe8f8de01efc78c | True |
| tests/ErpSystem.Api.Tests/Controllers/Procurement/ProcurementAccessControlsControllerTests.cs | f1f7251e6de138866d0967152c46ef30a6035caf | True |
| tests/ErpSystem.Api.Tests/Controllers/Procurement/ProcurementComplianceDecisionsControllerTests.cs | c742fbc357ca75c52ebb70492a639ae9bc6cffb1 | True |
| tests/ErpSystem.Api.Tests/Controllers/Procurement/ProcurementControlEventsControllerTests.cs | 512b3a9f72006ca3b819dea24d6a6c75a499dd4f | True |
| tests/ErpSystem.Api.Tests/Controllers/Procurement/ProcurementGhanepsExchangesControllerTests.cs | 0344df8149359594dddb69f62aaf2e6cb570b7c8 | True |
| tests/ErpSystem.Api.Tests/Controllers/Procurement/ProcurementLegacyRoleAuthorizationSecurityTests.cs | 49c53881431a84ad43e028499b531650ea405804 | True |
| tests/ErpSystem.Api.Tests/Controllers/Procurement/ProcurementMasterDataChangesControllerTests.cs | dc494f113b12da4c8399ca4cbb0da9c848545986 | True |
| tests/ErpSystem.Api.Tests/Controllers/Procurement/ProcurementPolicySetsControllerTests.cs | a98987289b2f19f153e694346ddeacbc2c05bd96 | True |
| tests/ErpSystem.Api.Tests/Controllers/Procurement/ProcurementSourcingCasesControllerTests.cs | d09ec1d76519d248724b28f33493f0dcc614ce41 | True |
| tests/ErpSystem.Api.Tests/Controllers/Procurement/ProcurementSpecificationTemplatesControllerTests.cs | deb4d1a45d0c2ee5e55c6c5f525227b713733c69 | True |
| tests/ErpSystem.Api.Tests/Controllers/Procurement/PurchaseRequisitionBudgetControlsControllerTests.cs | 2b49bfd222e10d5db473c70f183dfc398e0880a1 | True |
| tests/ErpSystem.Api.Tests/Controllers/Procurement/PurchaseRequisitionLinkagesControllerTests.cs | f35cb96380ddb6e2a694272bfe10fe58ba445f8f | True |
| tests/ErpSystem.Api.Tests/Controllers/Procurement/PurchaseRequisitionSubmissionControlsControllerTests.cs | 708a555106e9f950ee94c485e8c62c2dd789b203 | True |
| tests/ErpSystem.Api.Tests/Services/DatabaseSeedingDependencyGraphTests.cs | 2369025786fc2ce96c5ac3e523d1bb1e7a91847f | True |
| tests/ErpSystem.Api.Tests/Services/Documents/DmsGenerationTemplateMigrationCompatibilityTests.cs | 23c53bd64fc0d70f42bbf969abeb798280cd6c18 | True |
| tests/ErpSystem.Api.Tests/Services/Estate/EstateWorkflowIntegrationRegressionTests.cs | cf8bc2c5d773a3b98201b930d3ae7e460153fd31 | True |
| tests/ErpSystem.Api.Tests/Services/Finance/AccountBookCurrencyPolicyServiceTests.cs | e76178e58ce1df09497fd3207c89e22638bcc826 | True |
| tests/ErpSystem.Api.Tests/Services/Finance/AccountingBookApplicabilityC5MigrationTests.cs | 6d30b217e68023eedb0bd3de96f0a2bc50af2f6c | True |
| tests/ErpSystem.Api.Tests/Services/Finance/AccountingBookClassificationAuthorityTests.cs | 06b9d77c190ea61cef92beae9fc39884e860d12d | True |
| tests/ErpSystem.Api.Tests/Services/Finance/AccountingBookLifecycleC3MigrationTests.cs | 34c5d9b2b1a75af12b1d67db4a4c47fec1ade327 | True |
| tests/ErpSystem.Api.Tests/Services/Finance/AccountingBookLifecycleC3Tests.cs | d011ae133a991691b54e3171fc0625188b0bed9e | True |
| tests/ErpSystem.Api.Tests/Services/Finance/AccountingBookPeriodInitializationC4MigrationTests.cs | b5aad45f2f77b129519b56f8d94b6138c177e0c1 | True |
| tests/ErpSystem.Api.Tests/Services/Finance/BaseDeltaReportDocumentBuilderTests.cs | 4723f2bc6725e28d37d40d81ccf6d8437475b858 | True |
| tests/ErpSystem.Api.Tests/Services/Finance/BudgetServiceHardeningTests.cs | e49d60808bae1595947ce9d61a63c40a53fd8abb | True |
| tests/ErpSystem.Api.Tests/Services/Finance/CashBankTransactionPostingMigrationTests.cs | 40b36fb876c41e19ec18aa58ae1adfdeb056fbcf | True |
| tests/ErpSystem.Api.Tests/Services/Finance/CoreFinancialReportingFoundationTests.cs | 621795b3923f8bdcb9918c7ba0f64261ee9806f0 | True |
| tests/ErpSystem.Api.Tests/Services/Finance/DatabaseSeedFinanceProvisioningIsolationTests.cs | c11874a494dab3ac8a9c7f5ff715eb4a23e1446f | True |
| tests/ErpSystem.Api.Tests/Services/Finance/FinanceBudgetCommitmentServiceTests.cs | f1b923e591e9070c86a23f087179086805eb2541 | True |
| tests/ErpSystem.Api.Tests/Services/Finance/FinanceBudgetControlServiceTests.cs | 2039ef0214cee3531f2392f735c97d50dd4dc5c7 | True |
| tests/ErpSystem.Api.Tests/Services/Finance/FinanceSettingsMigrationGapTests.cs | 9f20e67c35d588c7a4cd97ec7f9d12ce4dd84d1e | True |
| tests/ErpSystem.Api.Tests/Services/Finance/FinanceSettingsWriteOffMappingTests.cs | 7927262e99e41d8fb066f3f9ecb97fdbda7b2f97 | True |
| tests/ErpSystem.Api.Tests/Services/Finance/FinancialStatementLayoutServiceTests.cs | 8ab0fdaf1f7883c2271a666efa33d8da7fd64005 | True |
| tests/ErpSystem.Api.Tests/Services/Finance/FixedAssetCapitalizationFoundationTests.cs | 63a22f7184ebd0538e5d7d32f889b8a1181c1a2d | True |
| tests/ErpSystem.Api.Tests/Services/Finance/FixedAssetDepreciationFoundationTests.cs | 0e2260386533b586c73f76fb19a4375198f1653d | True |
| tests/ErpSystem.Api.Tests/Services/Finance/FixedAssetDepreciationServiceTests.cs | 639f3e53f9be4778770ae36090af82bf5464f4bb | True |
| tests/ErpSystem.Api.Tests/Services/Finance/FxRealizedUnrealizedRevaluationTests.cs | b795dc4df33f44b227c30ff86813bf7fbc37b860 | True |
| tests/ErpSystem.Api.Tests/Services/Finance/GhanaStatutoryTaxEngineTests.cs | 34ada9efd41d77ddfefb4ccbdd843d9cb2e2c855 | True |
| tests/ErpSystem.Api.Tests/Services/Finance/JournalBatchSpreadsheetServiceTests.cs | b7fc50581bb23aef4d4c914989ee8d4ee749db9a | True |
| tests/ErpSystem.Api.Tests/Services/Finance/JournalVoucherDocumentBuilderTests.cs | bbc7b8fe4edff17804697ff4b1f38a0ad1965f28 | True |
| tests/ErpSystem.Api.Tests/Services/Finance/VendorInvoiceMatchExceptionServiceTests.cs | deff9622b7c08f74818e107f855e7b7795282636 | True |
| tests/ErpSystem.Api.Tests/Services/UnitOfWorkTransactionLockTests.cs | 52fc28453a530826c5f62b1981203968d31c93f9 | True |
