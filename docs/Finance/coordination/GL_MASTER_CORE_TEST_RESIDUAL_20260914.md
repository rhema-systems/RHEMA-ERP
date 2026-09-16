# Latest-master Core test residual — 2026-09-14

This file records the exact non-connection Core-test residual after latest-master semantic integration and the D3 migration archive move. It is classification evidence, not a waiver for deployment or feature activation.

## Reproduction boundary

- Integrated candidate parent: `cb5da5de7b92426e9d3800558ba8dbbc27eda477`.
- Exact latest-master parent: `efdfbb7b5105b649b0cfcf2736eff0f7a15f7954`.
- Configuration: `Release`, no restore, and no configured database or connection environment.
- The filter excluded only the following classes that directly read a connection or tooling environment variable:

  - `BusinessPartnerRegistrationApprovalSqlServerTests`
  - `AwardReadinessSqlServerTests`
  - `ControlledSourcingMethodsMigrationTests`
  - `QuantitySurveyArchitectureSqlServerMigrationTests`
  - `ProcurementArchitectureSqlServerIntegrationTests`
  - `InventoryReturnValuationSqlTests`
  - `InventoryStoresArchitectureSqlServerIntegrationTests`
  - `CivilEngineeringMaintenanceCompletionControlSqlServerTests`
  - `CivilEngineeringMaintenanceExecutionLinkSqlServerTests`
  - `CivilEngineeringMaintenanceCostingHandoffSqlServerTests`
  - `CivilEngineeringMaintenanceAssessmentSqlServerTests`
  - `CivilEngineeringMaintenanceIntakeSqlServerTests`
  - `CivilEngineeringProjectEngineerAssignmentSqlServerTests`
  - `ProcurementStatutoryReportServiceTests`
  - `SupplierApplicantContactCorrectionSqlServerTests`
  - `InventoryNegativeStockControlTests`

The filter is the case-sensitive conjunction of `FullyQualifiedName!~<class>` for the entries above. Run:

```powershell
dotnet test tests/ErpSystem.Core.Tests/ErpSystem.Core.Tests.csproj -c Release --no-build --no-restore --filter $filter
```

Exact latest master requires its existing nullable test compile correction at `ProcurementAwardReadinessEvaluatorSodTests` (`WorkflowDefinitionId!.Value`) to execute this comparison; that transient adaptation was not committed and the detached comparison worktree was returned content-clean.

## Classification

- Before this correction, the integrated candidate reported 3,159 passed / 111 failed / 0 skipped.
- Redirecting all 67 direct historical migration-source reads in 44 test files to the exact `LegacyMigrationsArchive` closed 52 merge-induced file-not-found failures.
- Seeding the master-originated Inventory current-cost fixture with the cutover-required accounting book closed the remaining two merge fixture failures.
- Final integrated result: 3,213 passed / 57 failed / 0 skipped (3,270 total).
- Parent result: 3,193 passed / 59 failed / 0 skipped (3,252 total).
- All 57 integrated failures below reproduce by exact test name on the parent. The parent-only failures are the tender-payment disclosure and ordinary-opening-stock fixtures already corrected on the integrated branch. There are no integrated-only residual failures.

## Exact parent-reproduced residual (57)

- `ErpSystem.Core.Tests.Services.Crm.CrmServiceTests.CreateOpportunityAsync_ShouldUseBusinessPartnerBackedAccountLink`
- `ErpSystem.Core.Tests.Services.Crm.CrmServiceTests.GetAccountDetailAsync_ShouldReturnLinkedAccountContext`
- `ErpSystem.Core.Tests.Services.Crm.CrmServiceTests.GetConversionsAsync_ShouldReturnFunnelJourneysAndLeakage`
- `ErpSystem.Core.Tests.Services.Crm.CrmServiceTests.GetOpportunityByIdAsync_ShouldReturnConversionChainAndDeliveryLinks`
- `ErpSystem.Core.Tests.Services.Crm.CrmServiceTests.GetQuoteByIdAsync_ShouldReturnLineItemsAndConversionChain`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementBidderCommunicationServiceTests.CrossTenantOverviewCannotDiscoverExistingRegister`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementContractBudgetLifecycleRegressionTests.Reservation_is_deferred_until_the_final_activation_transaction`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementControlEventServiceTests.AuthenticatedActorCanQueryAfterAuditPermissionIsEnforcedAtApiBoundary`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementFrameworkCallOffExpiryServiceTests.ExpiryProcessingCreatesLedgerForUnusedGoverningAgreement`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionAuthorityRouteServiceTests.ReadyDecisionCapturesImmutablePolicyWorkflowAndRuleSnapshotWithAuditHistory`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionBudgetControlServiceTests.ActiveReservationCannotMakeAnIneligibleBudgetCurrentAgain(budgetStatus: "Approved", expireBudget: True)`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionBudgetControlServiceTests.ActiveReservationCannotMakeAnIneligibleBudgetCurrentAgain(budgetStatus: "Closed", expireBudget: False)`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionBudgetControlServiceTests.ApprovedAvailableBudgetIsReservedWithSnapshotAndImmutableAuditLineage`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionBudgetControlServiceTests.ASecondReservationAttemptReusesTheActiveCommitmentWithoutDoubleSpending`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionBudgetControlServiceTests.CrossTenantBudgetCannotBeResolvedThroughARequisitionSnapshot`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionBudgetControlServiceTests.DownstreamReadinessUsesPoExposureInsteadOfLargerPrEstimate`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionBudgetControlServiceTests.ExistingEstimateReservationIsAdjustedToActualAwardWithoutDuplicateCommitment`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionBudgetControlServiceTests.ExpiredBudgetOverrideForTheSameRequisitionDoesNotAuthorizeTheShortfall`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionBudgetControlServiceTests.ExpiredOrForeignSubjectOverrideDoesNotAuthorizeTheShortfall`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionBudgetControlServiceTests.FutureOrUnapprovedBudgetStateIsRejectedAsStaleFinanceData`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionBudgetControlServiceTests.InsufficientBudgetBlocksWithoutCreatingACommitmentOrMutatingFinanceTotals`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionBudgetControlServiceTests.ReadinessUsesActiveAwardReservationInsteadOfOriginalPrEstimate`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionBudgetControlServiceTests.RejectedBudgetOverrideWorkflowDoesNotAuthorizeTheShortfall`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionBudgetControlServiceTests.ReleasedEnvelopeRestoresFinanceAvailabilityButRequiresNewOrAmendedRequisition`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionBudgetControlServiceTests.TerminalRequisitionEnvelopeRequiresNewOrAmendedApprovalForReplacementProcurement(status: Consumed, expectedCode: "PR_BUDGET_COMMITMENT_CONSUMED")`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionBudgetControlServiceTests.TerminalRequisitionEnvelopeRequiresNewOrAmendedApprovalForReplacementProcurement(status: Released, expectedCode: "PR_BUDGET_COMMITMENT_RELEASED")`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionLinkageServiceTests.CompletedExceptionWorkflowWithoutCompletionTimestampIsNotAcceptedOrOffered`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionLinkageServiceTests.CompleteLinkagePersistsSnapshotsAndImmutableHistory`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionLinkageServiceTests.OptionsExposeOnlyEffectivePublishedTemplatesAndCompletedExceptionWorkflows`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionSubmissionControlServiceTests.CompletedExceptionWorkflowForAnotherRequisitionIsNotReusable`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionSubmissionControlServiceTests.CrossTenantRequisitionIsNotDiscoverable`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionSubmissionControlServiceTests.LatestAcknowledgedAppAttemptAllowsSubmissionAndRecordsIntegrityProtectedEvidence`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionSubmissionControlServiceTests.MissingAppAndExceptionAllowsConfiguredWorkflowAndRecordsTraceabilityDecision`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionSubmissionControlServiceTests.NewerRejectedAppAttemptIsRetainedForTraceabilityWithoutBlockingSubmission`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementReviewRegressionTests.Draft_and_submitted_purchase_orders_check_capacity_while_final_approval_posts_finance`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementSpecificationTemplateServiceTests.FutureReplacementKeepsCurrentPublishedTemplateEffectiveUntilItsStartDate`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementSpecificationTemplateServiceTests.SearchAndDetailNeverCrossTenantBoundary`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementSpecificationTemplateServiceTests.StaleRowVersionIsRejectedBeforeMutation`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementSpecificationTemplateServiceTests.SubmissionRequiresEveryStandardSectionAndEvidence`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierApplicantAccessServiceTests.VerifiedContactsAreNormalizedBeforeHashingOrDelivery(channel: Email, input: " SUPPLIER@EXAMPLE.COM ", expected: "supplier@example.com")`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierApplicantAccessServiceTests.VerifiedContactsAreNormalizedBeforeHashingOrDelivery(channel: Sms, input: " +233 24-123-4567 ", expected: "+233241234567")`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierAvlServiceTests.CompletedWorkflowCannotBeRecordedAsRejected`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierAvlServiceTests.CompletedWorkflowPublishesAnnualRegisterAndSupportsEvidencedStatusHistory`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierAvlServiceTests.CreateFailsClosedWithoutOneExactEffectiveDec011Policy`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierAvlServiceTests.FutureReplacementKeepsCurrentPublicationUntilReplacementEffectiveDate`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierAvlServiceTests.RegisterReadsDoNotDiscloseAcrossTenants`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierDueDiligenceServiceTests.CompletedReviewCanBeGovernedlySupersededAfterPolicyReplacement`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierDueDiligenceServiceTests.CompletedWorkflowCannotBeRecordedAsRejected`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierDueDiligenceServiceTests.CreateFailsClosedWithoutOneExactEffectiveDec011Policy`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierDueDiligenceServiceTests.ExactSixCurrentEvidenceChecksCanCompleteIndependentApproval`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierDueDiligenceServiceTests.RejectedReviewRetryUsesNextRetainedCycleNumber`
- `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierDueDiligenceServiceTests.ReviewReadsDoNotDiscloseAcrossTenants`
- `ErpSystem.Core.Tests.Services.Procurement.PurchaseOrderLineRulesTests.NonStockCannotReachInventoryDependenciesEvenWithCatalogueId(type: NonStock)`
- `ErpSystem.Core.Tests.Services.Procurement.PurchaseOrderLineRulesTests.NonStockCannotReachInventoryDependenciesEvenWithCatalogueId(type: Service)`
- `ErpSystem.Core.Tests.Services.Projects.ProjectAssetLinkFixedAssetReconciliationMigrationGuardTests.Migration_extends_the_existing_project_link_with_tenant_safe_fixed_asset_lineage_only`
- `ErpSystem.Core.Tests.Services.Projects.ProjectServiceTests.GetMobileSummaryAsync_ShouldExcludeApprovedEntriesRegardlessOfStatusCasing`
- `ErpSystem.Core.Tests.Services.Projects.ProjectServiceTests.ResolveNonConformanceAsync_ShouldMarkRecordResolved`
