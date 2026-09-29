# Full Core suite triage — 27 September 2026

## Evidence and timing

The full run in `tmp/inventory-full-core-tests.log` completed: **3,520 passed / 75 failed / 35 skipped / 3,630 total**, duration **7m50s**. Machine-readable results: `tmp/inventory-workflows-final-tests/core-full.trx`. This is not a green-suite claim. Its compiled Core/test binaries predate the null-safe legacy-adjustment correction, the two assertion corrections and the investigation recovery below. Source-reading tests can see newer worktree files during this run, so they are not necessarily evaluating the same source version as the loaded DLL.

Comparison base: `1413bb6fa5ac2a6ec20637ceb03416669a333e63` (`HEAD`). “Unchanged” below means `git diff HEAD -- <service> <test>` returned no changes for the named files. That is source evidence of an existing mismatch, not a separate clean-HEAD execution. No unrelated authorization rules or old fixtures were relaxed to make this run green.

## In-scope corrections requiring rerun

| Failure | Cause and correction | Remaining check |
| --- | --- | --- |
| Five `StockAdjustmentCountFreezeOrderingTests` cases | `StockAdjustment.IdempotencyKey` is nullable, but `GetDirectDisposalSourceAsync` called `StartsWith` unconditionally. This null bug also exists at HEAD and is exposed by valid legacy/count adjustment fixtures. Approved preservation fix changes the prefix test to `IdempotencyKey?.StartsWith(...) != true`; all disposal lineage, workflow and authorization guards remain. | Rebuild Core and rerun the five existing ordering/rollback/authorization cases. Do not add a key to the fixtures to hide the null path. |
| `InventoryItemProfileServiceTests.Posting_account_updates_preserve_omitted_fields_and_clear_explicit_null` | This change adds the configured Inventory Disposal Account, so the catalogue now has 17 mappings. Updated the expected count from 16 to 17 and explicitly requires the new purpose. Omitted-field and explicit-null assertions remain. | Recompile tests and rerun the profile class. |
| `InventoryNegativeStockControlTests.Concurrent_decrement_owners_compose_the_same_atomic_guard` | The transfer decrement moved into `InventoryTransferService.AllocationOperations.cs:59`. Updated only the source path checked by the existing guard-composition assertion. The production call still uses `PrepareDecreaseAsync`; all other owners and atomic-guard assertions remain. | Recompile tests and rerun this case. |

Earlier supplemental-run correction: `InventoryRequisitionDraftTests` now seeds active tenant `OrganizationUnit` records and supplies `OrganizationUnitId`, matching the existing production contract. Cost-centre, valuation, stock, return-capacity and immutable-history assertions were preserved. The prior supplemental result was 231 pass / 16 fail, all 16 blocked by the stale Department-only fixture; it is not evidence of current corrected test results.

### E2E-012 count lifecycle findings

- One caller-owned adjustment transaction case encounters the same nullable legacy `IdempotencyKey` bug above; retain the original fixture and rerun with rebuilt Core.
- Two transaction-disposal cases use a plain mock repository. The count transaction now resolves the root lock through EF async before running the supplied operation. Replaced only the count repository with the fixture's real EF repository; original exception identity, rollback count and tracker-clear assertions remain.
- The legacy ambiguous-item-code import test lacked row-version and idempotency tokens even on its first valid row. Supplied saved row versions and distinct keys so it reaches its intended multi-bin ambiguity guard; retained the assertion that no row changes.
- Three posting lifecycle cases mock adjustment creation without persisting any adjustment lines. New root-line resolution claims correctly require exact retained item/bin/quantity/cost/lot/serial lineage. The mock now persists the returned adjustment header and actual request lines; no production lineage guard was relaxed.
- A real compatibility dead end was found: an omitted-committee legacy count submitted and returned for investigation could neither rewrite its retained observations nor create a child recount, since initial committee assignment was Draft-only. The approved correction permits an independent, scoped investigator with both count and adjustment-approval capabilities to assign the first eligible Employee committee to a submitted root under investigation, only after retirement of its adjustment and before any descendant. It requires a reason, optimistic concurrency and replay-safe action 17; SQL checks recovery audit membership. Original observations remain immutable, and correction occurs on a retained child recount. UI exposes only server-authorized recovery and requires the reason. Added focused lifecycle, denied-permission, wrong-state, invalid-employee and replay coverage. These new/updated tests require rebuilt binaries and are not included in the result above.

## Expected migration/model gate, not a test to weaken

Snapshot-parity failures are observed in `InventoryProjectReservationControlTests`, `InventoryReplenishmentControlTests`, `InventoryWorkOrderReservationControlTests`, `InventoryNegativeStockControlTests` and `TenderBidItemDocumentTests`. The diff reports the intended new transfer allocation, count committee/recount, receipt cost basis, supplier return, disposal account and Sales invoice lineage schema. The compiled snapshot precedes the combined migration. Complete the migration/snapshot, rebuild the matching Data assembly, and rerun these model gates. Do not remove or loosen them.

## Unchanged authorization and fixture mismatches

- Five `ProcurementRequisitionSubmissionControlServiceTests` cases: `EnsureReader` (service lines 448–454) requires platform SuperAdmin or a registered `procurement.records.read` grant; fixture lines 228–254 supplies only `TenantAdmin`. The service, test, `ProcurementRolePermissionEvaluator` and registry are unchanged. The service/evaluator authorization change last landed in `e1d470d85b`; the test last changed in `4085a3a713`.
- `ProcurementRequisitionAuthorityRouteServiceTests`, `ProcurementSpecificationTemplateServiceTests`, and read cases in `ProcurementRequisitionLinkageServiceTests` fail at the same explicit procurement reader boundary. These files are unchanged. `ProcurementControlEventServiceTests` requires a registered audit role, while the old test assumes authentication/API enforcement alone; service and test are unchanged.
- `ProcurementSupplierDueDiligenceServiceTests` (six observed) and `ProcurementSupplierAvlServiceTests` (five observed): unchanged fixtures use TenantAdmin and an unconfigured `IProcurementAccessControlService` mock. Current unchanged services call `EnforceCapabilityAsync` and dereference its returned decision. The mock returns null; changing real authorization would be incorrect.
- Other `ProcurementRequisitionLinkageServiceTests` cases seed Department-era data without the HR organization unit code required by the unchanged `ResolveOrganizationUnitCostCenter`. `ProcurementPlanNumberReservationTests` likewise fails on its existing active-HR-unit requirement.
- Two observed `E2E010ProjectMaterialLifecycleTests` cases hit the existing requirement that an approved project requisition have `OrganizationUnitId`. Both the reservation service and tests are unchanged at HEAD; this is distinct from the corrected ordinary requisition draft fixture.
- `ProcurementBidderCommunicationServiceTests.CrossTenantOverviewCannotDiscoverExistingRegister` encounters authorization before its expected not-found result. Service and test are unchanged; retain permission enforcement.

## Unchanged source-text / archive assumptions

- `InventoryReturnAdjustmentControlTests.Controlled_adjustments_accept_inventory_held_fixed_assets_but_opening_stock_remains_stock_item_only` expects literal `var expenseAccount = expense` and `var recoveryAccount = recovery`. The HEAD Finance builder already uses `InventoryPostingAccountResolution.ResolveAsync` for both. The fixed-asset/opening-stock behavior assertions are not the failing boundary. Left unchanged.
- `SupplierOnboardingCategoryAlignmentMigrationTests`, `PurchaseRequisitionLinePlanItemLineageMigrationTests`, `TenderPaymentFinancePostingTests` and `TenderPaymentVerificationPermissionMigrationTests` assume a single discoverable baseline migration, but HEAD already has many subsequent migrations.
- `ProcurementBudgetCommitmentLifecycleMigrationGuardTests` references an absent archived snapshot path. `ProjectAssetLinkFixedAssetReconciliationMigrationGuardTests` expects an archived migration identifier in lightweight discovery metadata. These assertions require separate archive/tooling reconciliation, not changes to Inventory posting behavior.
- `ProcurementContractBudgetLifecycleRegressionTests`, `ProcurementReviewRegressionTests`, `QuantitySurveyAdvanceRecoveryGuardTests` and `PurchaseOrderLineRulesTests` fail on source-string or reflection-signature assumptions. They are outside this slice; their exact historical intent still needs independent review before editing.

## Remaining unclassified baseline candidates

Five observed `CrmServiceTests` failures are null references in conversion/detail methods; service and tests are unchanged. `ProcurementFrameworkCallOffExpiryServiceTests` and `ProcurementSupplierApplicantAccessServiceTests` also report null references. These are **not claimed fixed or proven harmless**; no Inventory product change has yet been identified as their cause. Preserve them in the final full-suite report and triage independently if release policy requires a completely green repository suite.

Recovery SQL acceptance: **35/35 passed** in isolated fixture `RhemaERP_CountGuard_20260927_140122_5a0b0478`; evidence `tmp/count-guards-20260927_140122_5a0b0478.json`. Exact guards allow audited initial recovery and reject missing/wrong actor or employee audit, original-counter/self assignment, active adjustment, assigned-committee rewrite, child-as-root and original observation mutation. Existing concurrent root-line claim test observed a blocked second SQL session and rejected the duplicate. This validates guard SQL, not the compiled service lifecycle or full migration.

Committee UI acceptance: **12/12 passed**, single worker, `tmp/count-recovery-ui.log`; includes required investigation reason, permission-gated recovery and existing lookup, selection, error-retention and retry behavior. The compiled C# recovery lifecycle remains pending the next build.

No compiler, application-database migration, server restart, permission grant or unrelated test rewrite was performed by this triage pass. The SQL fixture is separate from the application database and retained as evidence.

## Current full-suite rerun and Inventory fixture follow-up

The explicitly unfiltered current build discovered 3,642 tests and completed with **3,552 passed, 55 failed and 35 skipped** in 8m6s. Evidence: `tmp/inventory-current-full-core-results/inventory-current-full-core.trx` and `tmp/inventory-current-full-core-summary.json`. All 55 failed identities also occurred in the preceding 75-failure run; 20 earlier failures now pass. This comparison is not a clean-master execution or proof that every remaining failure is harmless.

Three remaining Inventory failures were traced to obsolete fixtures/assertions. E2E010 now seeds the required active tenant HR organization hierarchy and binds `OrganizationUnitId`, preserving stock, cost and notification assertions and verifying organization lineage. The source-oriented return/adjustment test no longer requires obsolete local-variable spellings or the old receipt persistence order. Actual posting-sign behavior is exercised in API C9 tests, and receipt behavior remains covered by `InventoryIssueReceiptTests` and the separate SQL guard/concurrency acceptance.

The final follow-up of those two Core families plus receipt tests passed **45/45**, with zero product-code changes for these fixture corrections. Evidence: `tmp/inventory-current-full-core-results/inventory-core-fixture-final.trx`; normal test-only compile passed with zero errors and eight warnings. The full suite was not rerun after this bounded follow-up; its remaining 52 failed identities still need separate triage.
