# Procurement baseline comparison — 25 September 2026

Baseline: `5b63f41f9343bff92712322a021a317d4ca3e382` in the detached validation worktree. Both runs use the same filters of methods that failed the earlier broad suites, with test collections executed sequentially. This is a failure triage run, not a full-suite release pass.

The pristine baseline Core test project does not compile: BusinessPartnerPostingDefaultsTests references an archived migration type, and ProcurementPlanBudgetSelectionTests passes nullable department IDs to a non-nullable fixture helper. The filtered baseline run excludes those two unselected classes through an external MSBuild target. No selected test or production source was changed. The original compilation failure is retained in ErpSystem.Core.Tests-baseline.log.

“Same boundary” means the first exception/assertion line and originating ErpSystem symbol agree, normalising checkout roots, GUIDs, timestamps, the moved ReceiptAccounts diagnostic line number, and the manually reviewed obsolete exactly-one-migration assertion lists. Full messages and stacks are retained in the JSON and TRX evidence; matching boundaries do not establish that every assertion was reached.

## ErpSystem.Core.Tests

| Classification | Cases |
|---|---:|
| Both failed — same boundary | 76 |
| Both passed on rerun | 2 |

| Test | Classification |
|---|---|
| `ErpSystem.Core.Tests.Services.Crm.CrmServiceTests.CreateOpportunityAsync_ShouldUseBusinessPartnerBackedAccountLink` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Crm.CrmServiceTests.GetAccountDetailAsync_ShouldReturnLinkedAccountContext` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Crm.CrmServiceTests.GetConversionsAsync_ShouldReturnFunnelJourneysAndLeakage` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Crm.CrmServiceTests.GetOpportunityByIdAsync_ShouldReturnConversionChainAndDeliveryLinks` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Crm.CrmServiceTests.GetQuoteByIdAsync_ShouldReturnLineItemsAndConversionChain` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.E2E010ProjectMaterialLifecycleTests.Approved_project_reservation_partial_issue_and_return_reconcile_notifications_stock_and_cost` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.E2E010ProjectMaterialLifecycleTests.Reservation_substitution_refreshes_item_unit_and_requisition_value_atomically` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.E2E012CycleCountFinanceLifecycleTests.Adjustment_posting_joins_an_existing_count_transaction_on_failure_and_replay(replay: False)` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.E2E012CycleCountFinanceLifecycleTests.Adjustment_posting_joins_an_existing_count_transaction_on_failure_and_replay(replay: True)` | Both passed on rerun |
| `ErpSystem.Core.Tests.Services.Inventory.InventoryRequisitionDraftTests.Add_update_remove_merge_unsaved_changes_into_persisted_totals` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.InventoryRequisitionDraftTests.Approved_lines_cannot_be_repriced_through_draft_edit` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.InventoryRequisitionDraftTests.Create_derives_cost_centre_from_department_not_client_text(account: " CC-OPS ", code: "OPS", expected: "CC-OPS")` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.InventoryRequisitionDraftTests.Create_derives_cost_centre_from_department_not_client_text(account: "", code: " OPS ", expected: "OPS")` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.InventoryRequisitionDraftTests.Create_persists_aggregates_and_uses_configured_scoped_cost_without_consuming_stock(method: FIFO, expectedCost: 1900)` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.InventoryRequisitionDraftTests.Create_persists_aggregates_and_uses_configured_scoped_cost_without_consuming_stock(method: LIFO, expectedCost: 2100)` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.InventoryRequisitionDraftTests.Create_persists_aggregates_and_uses_configured_scoped_cost_without_consuming_stock(method: StandardCost, expectedCost: 2000)` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.InventoryRequisitionDraftTests.Create_persists_aggregates_and_uses_configured_scoped_cost_without_consuming_stock(method: WeightedAverage, expectedCost: 2000)` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.InventoryRequisitionDraftTests.Detail_resolves_the_saved_issue_location_without_request_header_or_loaded_navigation` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.InventoryRequisitionDraftTests.Draft_edit_preserves_saved_cost_centre_but_department_change_rederives_it` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.InventoryRequisitionDraftTests.Legacy_blank_draft_gets_cost_centre_on_save_without_rewriting_approved_history` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.InventoryRequisitionDraftTests.Reads_preserve_gross_fulfilment_and_show_returns_without_reopening_issue_capacity(approved: 2, netIssued: 0, returned: 2, expected: Issued, remaining: 0)` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.InventoryRequisitionDraftTests.Reads_preserve_gross_fulfilment_and_show_returns_without_reopening_issue_capacity(approved: 2, netIssued: 1, returned: 1, expected: Issued, remaining: 0)` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.InventoryRequisitionDraftTests.Reads_preserve_gross_fulfilment_and_show_returns_without_reopening_issue_capacity(approved: 5, netIssued: 1, returned: 1, expected: PartiallyIssued, remaining: 3)` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.InventoryRequisitionDraftTests.Reads_reconcile_legacy_header_without_rewriting_approved_record` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.InventoryRequisitionDraftTests.Requester_can_leave_storage_location_unspecified` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.InventoryReturnAdjustmentControlTests.Controlled_adjustments_accept_inventory_held_fixed_assets_but_opening_stock_remains_stock_item_only` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.StockAdjustmentCountFreezeOrderingTests.Later_line_failure_stages_no_movements_and_preserves_transaction_ownership(outerCountOwnsTransaction: False)` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.StockAdjustmentCountFreezeOrderingTests.Later_line_failure_stages_no_movements_and_preserves_transaction_ownership(outerCountOwnsTransaction: True)` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.StockAdjustmentCountFreezeOrderingTests.Ordering_fix_does_not_allow_a_requester_or_an_unassigned_scope_to_post(requester: False)` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.StockAdjustmentCountFreezeOrderingTests.Ordering_fix_does_not_allow_a_requester_or_an_unassigned_scope_to_post(requester: True)` | Both passed on rerun |
| `ErpSystem.Core.Tests.Services.Inventory.StockAdjustmentCountFreezeOrderingTests.Same_item_in_two_bins_flushes_each_signed_balance_delta_before_any_stock_movement(negativeFirst: False)` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Inventory.StockAdjustmentCountFreezeOrderingTests.Same_item_in_two_bins_flushes_each_signed_balance_delta_before_any_stock_movement(negativeFirst: True)` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementBidderCommunicationServiceTests.CrossTenantOverviewCannotDiscoverExistingRegister` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementBudgetCommitmentLifecycleMigrationGuardTests.Corrective_migration_guards_only_entry_to_final_exposure_and_requires_exact_po_ledger` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementContractBudgetLifecycleRegressionTests.Reservation_is_deferred_until_the_final_activation_transaction` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementControlEventServiceTests.AuthenticatedActorCanQueryAfterAuditPermissionIsEnforcedAtApiBoundary` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementFrameworkCallOffExpiryServiceTests.ExpiryProcessingCreatesLedgerForUnusedGoverningAgreement` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementPlanNumberReservationTests.CreateHoldsFiscalYearNumberLockUntilPlanIsCommitted` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementPlanNumberReservationTests.ExistingCallerTransactionIsJoinedWithoutStartingRetryStrategy` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementPlanNumberReservationTests.UncertainCommitRetryReturnsFirstCommittedPlanWithoutDuplicateInsert` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionAuthorityRouteServiceTests.ReadyDecisionCapturesImmutablePolicyWorkflowAndRuleSnapshotWithAuditHistory` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionLinkageServiceTests.CompatiblePlanItemsCanShareOneRequisitionAndRetainPrimaryHeaderLineage` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionLinkageServiceTests.CompleteLinkagePersistsSnapshotsAndImmutableHistory` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionLinkageServiceTests.CompletedExceptionWorkflowWithoutCompletionTimestampIsNotAcceptedOrOffered` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionLinkageServiceTests.FutureEffectiveApprovedBudgetCanBeLinkedWhileRequisitionRemainsDraft` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionLinkageServiceTests.OptionsExposeOnlyEffectivePublishedTemplatesAndCompletedExceptionWorkflows` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionLinkageServiceTests.PlanItemAutomaticallyCarriesItsBudgetWithoutMakingLinkageMandatory` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionLinkageServiceTests.PlanItemPrefersDepartmentAccountingCodeForCostCenter` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionSubmissionControlServiceTests.CompletedExceptionWorkflowForAnotherRequisitionIsNotReusable` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionSubmissionControlServiceTests.CrossTenantRequisitionIsNotDiscoverable` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionSubmissionControlServiceTests.LatestAcknowledgedAppAttemptAllowsSubmissionAndRecordsIntegrityProtectedEvidence` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionSubmissionControlServiceTests.MissingAppAndExceptionAllowsConfiguredWorkflowAndRecordsTraceabilityDecision` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementRequisitionSubmissionControlServiceTests.NewerRejectedAppAttemptIsRetainedForTraceabilityWithoutBlockingSubmission` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementReviewRegressionTests.Draft_and_submitted_purchase_orders_check_capacity_while_final_approval_posts_finance` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementSpecificationTemplateServiceTests.FutureReplacementKeepsCurrentPublishedTemplateEffectiveUntilItsStartDate` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementSpecificationTemplateServiceTests.SearchAndDetailNeverCrossTenantBoundary` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementSpecificationTemplateServiceTests.StaleRowVersionIsRejectedBeforeMutation` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementSpecificationTemplateServiceTests.SubmissionRequiresEveryStandardSectionAndEvidence` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierApplicantAccessServiceTests.VerifiedContactsAreNormalizedBeforeHashingOrDelivery(channel: Email, input: " SUPPLIER@EXAMPLE.COM ", expected: "supplier@example.com")` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierApplicantAccessServiceTests.VerifiedContactsAreNormalizedBeforeHashingOrDelivery(channel: Sms, input: " +233 24-123-4567 ", expected: "+233241234567")` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierAvlServiceTests.CompletedWorkflowCannotBeRecordedAsRejected` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierAvlServiceTests.CompletedWorkflowPublishesAnnualRegisterAndSupportsEvidencedStatusHistory` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierAvlServiceTests.CreateFailsClosedWithoutOneExactEffectiveDec011Policy` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierAvlServiceTests.FutureReplacementKeepsCurrentPublicationUntilReplacementEffectiveDate` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierAvlServiceTests.RegisterReadsDoNotDiscloseAcrossTenants` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierDueDiligenceServiceTests.CompletedReviewCanBeGovernedlySupersededAfterPolicyReplacement` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierDueDiligenceServiceTests.CompletedWorkflowCannotBeRecordedAsRejected` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierDueDiligenceServiceTests.CreateFailsClosedWithoutOneExactEffectiveDec011Policy` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierDueDiligenceServiceTests.ExactSixCurrentEvidenceChecksCanCompleteIndependentApproval` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierDueDiligenceServiceTests.RejectedReviewRetryUsesNextRetainedCycleNumber` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.ProcurementSupplierDueDiligenceServiceTests.ReviewReadsDoNotDiscloseAcrossTenants` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.PurchaseOrderLineRulesTests.NonStockCannotReachInventoryDependenciesEvenWithCatalogueId(type: NonStock)` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.PurchaseOrderLineRulesTests.NonStockCannotReachInventoryDependenciesEvenWithCatalogueId(type: Service)` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.PurchaseRequisitionLinePlanItemLineageMigrationTests.ArchivedMigrationBackfillIsRetainedWhileOnlyCurrentBaselineIsDiscoverable` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.SupplierOnboardingCategoryAlignmentMigrationTests.ArchivedRepairIsRetainedWhileOnlyCurrentBaselineIsDiscoverable` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.TenderPaymentFinancePostingTests.ArchivedMigrationContainsPostingLineageWhileOnlyCurrentBaselineIsDiscoverable` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Procurement.TenderPaymentVerificationPermissionMigrationTests.ArchivedPermissionRepairIsRetainedWhileOnlyCurrentBaselineIsDiscoverable` | Both failed — same boundary |
| `ErpSystem.Core.Tests.Services.Projects.ProjectAssetLinkFixedAssetReconciliationMigrationGuardTests.Migration_extends_the_existing_project_link_with_tenant_safe_fixed_asset_lineage_only` | Both failed — same boundary |

## ErpSystem.Api.Tests

| Classification | Cases |
|---|---:|
| Baseline failed — current passed | 18 |
| Both failed — same boundary | 284 |
| Both passed on rerun | 21 |
| Current-only failure — corrected and follow-up passed | 4 |
| New regression test — current passed | 1 |

| Test | Classification |
|---|---|
| `ErpSystem.Api.Tests.Controllers.Auth.AuthControllerRouteTests.GetSessionSettings_ShouldReturnPersistedTenantTimeout` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.AddCampaignMember_ShouldReturnBadRequestWhenServiceRejectsMembership` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.CreateActivity_ShouldReturnCreatedActivity` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.CreateCampaign_ShouldReturnCreatedCampaign` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.CreateLead_ShouldReturnCreatedLead` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetAccountDetail_ShouldReturnCrmAccountDetail` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetAccounts_ShouldReturnPagedCrmAccounts` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetActivities_ShouldReturnPagedCrmActivities` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetActivity_ShouldReturnCrmActivityDetail` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetCampaign_ShouldReturnCrmCampaignDetail` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetCampaigns_ShouldReturnPagedCrmCampaigns` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetCollaborationDetail_ShouldReturnCrmCollaborationDetail` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetCollaboration_ShouldReturnPagedCrmCollaboration` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetContacts_ShouldReturnPagedCrmContacts` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetContract_ShouldReturnCrmContractDetail` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetContracts_ShouldReturnPagedCrmContracts` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetConversions_ShouldReturnCrmConversions` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetForecast_ShouldReturnCrmForecast` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetOpportunities_ShouldReturnPagedCrmOpportunities` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetOverview_ShouldAllowLocalInternalUsers` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetOverview_ShouldReturnCrmOverview` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetProject_ShouldReturnCrmProjectDetail` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetProjects_ShouldReturnPagedCrmProjects` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetQuote_ShouldReturnCrmQuoteDetail` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetQuotes_ShouldReturnPagedCrmQuotes` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetReadinessDetail_ShouldReturnCrmReadinessDetail` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetReadiness_ShouldReturnPagedCrmReadiness` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetReporting_ShouldReturnCrmReporting` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetRiskDetail_ShouldReturnCrmRiskDetail` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetRisk_ShouldReturnPagedCrmRiskRegister` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetServiceDetail_ShouldReturnCrmServiceDetail` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Crm.CrmControllerRouteTests.GetService_ShouldReturnPagedCrmService` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.CrossModuleLegacyAuthorizationSecurityTests.CompleteModuleEndpointSurfaceDoesNotUseLegacyGenericRolesOrPolicies` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.CrossModuleLegacyAuthorizationSecurityTests.DeliberateTdcRoleGatesDoNotContainGenericFallbackRoles` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.DocumentManagement.PaymentDocumentScopeTests.Even_scoped_DMS_admin_cannot_replace_archive_annotate_or_republish_payment_documents` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Estate.PropertyListingEnquiryTests.SalesQueueRequiresSalesAndMarketingOrganizationUnitAndStatusBeyondNew` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Finance.FinanceControllerSecurityTests.Diagnostic_HighRiskFinanceSource_ShouldNotUseDefaultTenantFallbacksOrFindAsync` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Finance.FinanceControllerSecurityTests.FinanceControllerActions_ShouldHaveMappedFinancePermissionPolicies` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Finance.PaymentMethodControllerTests.GetAll_ShouldSeedDefaultsPerTenantAndNeverLeakAcrossTenants` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.BusinessPartnersControllerContactRouteTests.SetPrimaryContact_ShouldReturnNoContent` | Baseline failed — current passed |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementAccessControlsControllerTests.AdministratorCanReadReadinessCommitteesAndAudit` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementAccessControlsControllerTests.AnonymousCallerCannotReachCapabilityEnforcement` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementAccessControlsControllerTests.DirectApiDeniedCapabilityReturnsForbiddenDecision` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementAppSubmissionsControllerTests.AnonymousCallerCannotReadSummary` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementAppSubmissionsControllerTests.AuthorizedManagerCanTraverseAllMutationContracts` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementAppSubmissionsControllerTests.AuthorizedReaderCanUseTenantSafeRegisterQueries` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementCalendarControllerTests.AnonymousCallerCannotReadCalendar` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementCalendarControllerTests.AuthorizedCallerCanTraverseQueriesLifecycleTasksAndRuns` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementComplianceDecisionsControllerTests.AuthorizedAdministratorCanReadEffectiveTenantPolicyOptions` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementComplianceDecisionsControllerTests.AuthorizedEvaluationReturnsExplainableReadOnlyDecisionAndCorrelationId` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementComplianceDecisionsControllerTests.UnauthorizedCallersCannotReachTheEvaluator(modeValue: 0, expected: Unauthorized)` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementComplianceDecisionsControllerTests.UnauthorizedCallersCannotReachTheEvaluator(modeValue: 1, expected: Forbidden)` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementConfigurationProfilesControllerTests.AuthorizedAdministratorCanReadTenantProfileList` | Both passed on rerun |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementControlEventsControllerTests.AnonymousCallerCannotReadSummary` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementControlEventsControllerTests.AuthorizedReaderCanSearchInspectCorrelateAndVerify` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementControlEventsControllerTests.GenericClientAppendRouteDoesNotExist` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementGhanepsExchangesControllerTests.AnonymousCallerCannotReadSourceStatus` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementGhanepsExchangesControllerTests.RawQueueAndArbitraryMutationRoutesDoNotExist` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementLegacyRoleAuthorizationSecurityTests.SweptControllersDoNotUseLegacyGenericRoleGates` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementMasterDataChangesControllerTests.AnonymousCallerCannotReadRegistry` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementMasterDataChangesControllerTests.AuthorizedMakerCanCreateDraftThroughTenantSafeContract` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementMasterDataChangesControllerTests.AuthorizedReaderCanReadRegistrySummarySearchAndDetail` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementMasterDataChangesControllerTests.ProtectedMasterDataCreateEndpointsRequireStagedChange` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementMasterDataChangesControllerTests.ProtectedSupplierSuspendAndActivateRequireComplianceStaging` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementPolicySetsControllerTests.AnonymousCallerReceivesUnauthorized` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementPolicySetsControllerTests.AuthorizedAdministratorCanReadTenantPolicyList` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementPolicySetsControllerTests.CallerWithoutAdministrativeRoleReceivesForbidden` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementSodControlsControllerTests.AnonymousCallerCannotReachTheRuntimeGuard` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementSodControlsControllerTests.AuthorizedAdministratorCanReadSixControlCoverageAndBlockedAttempts` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementSodControlsControllerTests.DirectApiConflictReturnsForbiddenAuditedDecision` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementSodControlsControllerTests.IndependentRuntimeActorReceivesAllowedDecision` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementSodControlsControllerTests.PublicRuntimeGuardCannotBindInternalIndependentActorOverrides` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementSourcingCasesControllerTests.AnonymousCallerCannotReadOrMutateAnySourcingCaseRoute` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementSpecificationTemplatesControllerTests.AnonymousCallerCannotReadRegister` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.ProcurementSpecificationTemplatesControllerTests.AuthorizedUserCanTraverseQueriesAndLifecycleMutations` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.PurchaseRequisitionAuthorityControlsControllerTests.AnonymousCallerCannotReadAuthorityControlsSubmitOrApprove` | Both passed on rerun |
| `ErpSystem.Api.Tests.Controllers.Procurement.PurchaseRequisitionBudgetControlsControllerTests.AnonymousCallerCannotReadBudgetReadinessOrHistoryOrSubmit` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.PurchaseRequisitionLinkagesControllerTests.AnonymousCallerCannotReadLinkageOptionsOrAuditHistoryOrExport` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.PurchaseRequisitionSourcingReleaseControllerTests.AnonymousCallerCannotReadReleaseOrEnterSourcingRoutes` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Procurement.PurchaseRequisitionSubmissionControlsControllerTests.AnonymousCallerCannotReadReadinessOrHistoryOrSubmit` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Controllers.Projects.ProjectMobileControllerTests.SubmitTimesheet_ShouldReturnMockedEntry` | Both passed on rerun |
| `ErpSystem.Api.Tests.Middleware.GlobalExceptionHandlingMiddlewareTests.UnexpectedException_PersistsRedactedDiagnosticsForTenantAdministratorAudit` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.ClamAvFileVirusScanServiceTests.ScanAsyncReturnsInfectedForFoundResponse` | Both passed on rerun |
| `ErpSystem.Api.Tests.Services.ClamAvFileVirusScanServiceTests.ScanAsyncStreamsContentAndReturnsClean` | Both passed on rerun |
| `ErpSystem.Api.Tests.Services.DatabaseSeedingDependencyGraphTests.ProductionSeedingRegistrations_ShouldResolveEveryRegisteredSeederWithScopedFinanceBoundary` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.DocumentManagement.CentralDocumentWordConversionTests.PdfDownloadAsWordPreservesVisiblePagesForAnyDocument(documentName: "agreement")` | Current-only failure — corrected and follow-up passed |
| `ErpSystem.Api.Tests.Services.DocumentManagement.CentralDocumentWordConversionTests.PdfDownloadAsWordPreservesVisiblePagesForAnyDocument(documentName: "invoice")` | Current-only failure — corrected and follow-up passed |
| `ErpSystem.Api.Tests.Services.Documents.DmsGenerationTemplateMigrationCompatibilityTests.BothHistoricalMigrations_ShouldRemainDiscoverableInOrder` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Estate.EstateWorkflowIntegrationRegressionTests.PropertyAgreementRelease_RequiresLegalApprovalAcrossPortalAndDms` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.AccountBookCurrencyPolicyServiceTests.ExistingPolicy_RejectsAStaleRowVersion` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.AccountBookCurrencyPolicyServiceTests.NonstandardApproval_RollsBackActivationWhenAuditFails` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.AccountBookCurrencyPolicyServiceTests.NonstandardRequest_RollsBackWhenWorkflowStartFails` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.AccountBookCurrencyPolicyServiceTests.OrdinaryOverride_RollsBackWhenAuditPersistenceFails` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.AccountBookCurrencyPolicyServiceTests.PhaseFourMigration_IsDiscoverableByEfCore` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.AccountSegmentIdentityPhase5Tests.Migration_IsDiscoveredByEfCoreWithoutOpeningADatabaseConnection` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.AccountingBookApplicabilityC5MigrationTests.Migration_IsDiscoverableWithoutOpeningDatabaseConnection` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.AccountingBookLifecycleC3MigrationTests.Migration_IsDiscoverableWithoutOpeningDatabaseConnection` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.AccountingBookPeriodInitializationC4MigrationTests.Migration_IsDiscoverableWithoutOpeningDatabaseConnection` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.AccountingPeriodClosePostingDateTests.ValidatePeriodClose_ShouldDetectPostedJournalMissingPostingEvent` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ApInvoicePartnerDefaultsTests.NewFinanceSupplierIdentityCopiesPartnerAccounts` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ApInvoicePostingMigrationTests.DraftDistribution_ShouldSharePostingAccountsWithoutApprovingSavingOrPostingWht` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ApInvoicePostingMigrationTests.ForeignApInvoice_ShouldRejectCreateWithoutRateEvidence(isOpeningBalance: False)` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ApInvoicePostingMigrationTests.ForeignApInvoice_ShouldRejectCreateWithoutRateEvidence(isOpeningBalance: True)` | Both passed on rerun |
| `ErpSystem.Api.Tests.Services.Finance.ApPaymentVoucherDocumentBuilderTests.Render_ShouldConcealCrossTenantPaymentEvenWhenIdentifierIsKnown` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ApPaymentVoucherDocumentBuilderTests.Render_ShouldEnforceBankScopeBeforeDisclosingVoucher` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ApPaymentVoucherDocumentBuilderTests.Render_ShouldProduceTenantScopedAuditedPdfFromCanonicalPayment` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ApReceiptAccountResolutionTests.A_second_partial_invoice_does_not_reclear_the_first_receipt` | Baseline failed — current passed |
| `ErpSystem.Api.Tests.Services.Finance.ApReceiptAccountResolutionTests.Consolidated_prior_invoice_only_consumes_the_current_orders_accrual` | New regression test — current passed |
| `ErpSystem.Api.Tests.Services.Finance.ApReceiptAccountResolutionTests.Each_invoice_item_uses_its_original_receipt_accrual_not_current_global` | Baseline failed — current passed |
| `ErpSystem.Api.Tests.Services.Finance.ApReceiptAccountResolutionTests.Foreign_currency_split_difference_is_explicit_and_does_not_return_invalid_posting` | Baseline failed — current passed |
| `ErpSystem.Api.Tests.Services.Finance.ApReceiptAccountResolutionTests.Foreign_receipt_journal_cannot_supply_an_invoice_account` | Baseline failed — current passed |
| `ErpSystem.Api.Tests.Services.Finance.ApReceiptAccountResolutionTests.Legacy_and_new_receipts_both_contribute_their_original_item_accounts` | Baseline failed — current passed |
| `ErpSystem.Api.Tests.Services.Finance.ApReceiptAccountResolutionTests.Legacy_receipt_without_item_lineage_uses_its_recorded_single_account` | Baseline failed — current passed |
| `ErpSystem.Api.Tests.Services.Finance.ApReceiptAccountResolutionTests.Mixed_legacy_accounts_without_movements_are_not_guessed` | Baseline failed — current passed |
| `ErpSystem.Api.Tests.Services.Finance.ApReceiptAccountResolutionTests.Mixed_receipt_invoice_request_balances_and_clears_both_original_accounts` | Baseline failed — current passed |
| `ErpSystem.Api.Tests.Services.Finance.ApReceiptAccountResolutionTests.Mixed_service_line_does_not_guess_a_stock_receipt_account` | Baseline failed — current passed |
| `ErpSystem.Api.Tests.Services.Finance.ApReceiptAccountResolutionTests.Multiple_account_targets_remain_on_one_dimension_source_identity` | Baseline failed — current passed |
| `ErpSystem.Api.Tests.Services.Finance.ApReceiptAccountResolutionTests.Noncurrent_clearings_do_not_consume_this_tenant_receipt(state: "foreign")` | Baseline failed — current passed |
| `ErpSystem.Api.Tests.Services.Finance.ApReceiptAccountResolutionTests.Noncurrent_clearings_do_not_consume_this_tenant_receipt(state: "reversed")` | Baseline failed — current passed |
| `ErpSystem.Api.Tests.Services.Finance.ApReceiptAccountResolutionTests.Noncurrent_clearings_do_not_consume_this_tenant_receipt(state: "voided")` | Baseline failed — current passed |
| `ErpSystem.Api.Tests.Services.Finance.ApReceiptAccountResolutionTests.Partially_consumed_receipt_weights_use_only_the_remaining_balance` | Baseline failed — current passed |
| `ErpSystem.Api.Tests.Services.Finance.ApReceiptAccountResolutionTests.Posted_invoice_history_uses_its_own_journal_after_further_receipts` | Baseline failed — current passed |
| `ErpSystem.Api.Tests.Services.Finance.ApReceiptAccountResolutionTests.Tiny_invoice_split_over_four_accounts_never_produces_negative_debits` | Baseline failed — current passed |
| `ErpSystem.Api.Tests.Services.Finance.ApReceiptAccountResolutionTests.Zero_net_lines_do_not_invent_receipt_accounts_or_dimension_contexts` | Baseline failed — current passed |
| `ErpSystem.Api.Tests.Services.Finance.BudgetServiceHardeningTests.GetConsolidatedViewAsync_ReturnsExactDimensionCellPositionAndActiveReservation` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.CashBankControlledDocumentBuilderTests.Builders_ShouldRejectUnpostedOrWrongSourceRecords` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.CashBankControlledDocumentBuilderTests.CustomerReceipt_ShouldRenderFromPostedCanonicalArPaymentAndRetainIssue` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.CashBankControlledDocumentBuilderTests.PaymentSlip_ShouldIssueOneOriginalThenReasonBackedReplacement` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.CashierTillControlTests.LegacyCustomerPaymentCreditNote_ShouldRejectNewWrites` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.BalancedGlOpeningBalanceBatch_ShouldPostThroughFinancePostingEngine` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.BankSnapshotDiagnosticAndRepair_ShouldUsePostedGlOnly` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.ClosedPeriodOpeningBalancePosting_ShouldBeRejectedByPostingEngine` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.DuplicateOpeningBalancePosting_ShouldReturnExistingJournal` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.FinalSignOffActiveCurrentCovidLevy_ShouldFail` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.FinalSignOffBackReferenceCandidate_ShouldAppearInDiagnostics` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.FinalSignOffBankSnapshotVariance_ShouldFailUnlessAccepted` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.FinalSignOffCleanFixture_ShouldPassWithAcceptedLimitations` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.FinalSignOffFinLim0048_ShouldBlockWhenSubledgerOpeningsAreRequired` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.FinalSignOffUnacceptedGoLiveLimitation_ShouldFail` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.FinalSignOffUnbalancedTrialBalance_ShouldFail` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.FinalSignOff_ShouldIgnoreCrossTenantData` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.FixedAssetOpeningBatch_ShouldDerivePostingAndLinkImportedRegisterEvidence` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.ForeignCurrencyBankOpening_ShouldFreezeApprovedRateAndKeepNativeSnapshotSeparateFromFunctionalGl` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.ForeignSupplierAdvanceOpening_ShouldPreserveNativeAndFunctionalEvidence` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.GovernedBankOpening_ShouldDeriveAccountsPostOnceAndUpdateSnapshotAtomically` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.GovernedBankPosting_ShouldRemainDurablyPostedWhenSuccessAuditFails` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.GovernedResidualOpening_ShouldDeriveClearingAndRetainedEarningsWithoutInventoryLine` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.GovernedResidualOpening_ShouldTreatPostedOriginalAndReversalAsNetZero` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.PostedOpeningBalanceReversal_ShouldRequireIndependentReviewAndPostCompensatingEvidence` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.PostingBackReferenceRepairDryRun_ShouldDetectMissingLinksWithoutMutation` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.PostingBackReferenceRepair_ShouldRefuseAmbiguousMatches` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.PostingBackReferenceRepair_ShouldRepairOnlyUnambiguousLinks` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.RejectedGovernedBankOpening_ShouldPreserveHistoryAndChainCorrectedReplacements` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.RejectedGovernedResidualOpeningWithEventOnlyPostingEvidence_ShouldBlockLogicalDuplicate` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.RejectedGovernedResidualOpening_ShouldAllowCorrectedReplacementButActiveDuplicateBlocks` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.SupplierAndCustomerAdvanceOpenings_ShouldPostAndRemainVisibleAsUnappliedAdvances` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ControlledOpeningBalancePostingTests.WithholdingOpenings_ShouldFeedExistingCertificateAndStatutoryEvidenceWorkflows` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.CoreFinancialReportingFoundationTests.BalanceSheet_ShouldAttachSelectedLayoutAndRetainCompatibilityTotals` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.CoreFinancialReportingFoundationTests.BalanceSheet_ShouldDeriveFromPostedGlAndNormalizeCreditBalanceSections` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.CoreFinancialReportingFoundationTests.CashBankLedger_ShouldUsePostedGlAndExcludeUnpostedCashTransactions` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.CoreFinancialReportingFoundationTests.CashFlow_DirectAndIndirectMethods_ShouldUseDistinctReconciledPresentations` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.CoreFinancialReportingFoundationTests.CashFlow_ShouldFailClosedForUnclassifiedCashCounterpart` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.CoreFinancialReportingFoundationTests.CashFlow_ShouldFailClosedWhenCashCounterpartAccountIsUnavailable` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.CoreFinancialReportingFoundationTests.CashFlow_ShouldKeepOrdinaryFirstDayReceiptInPeriodActivity` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.CoreFinancialReportingFoundationTests.CashFlow_ShouldRecognizeConfiguredCashRoleAndUseSelectedBook` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.CoreFinancialReportingFoundationTests.CashFlow_ShouldTreatGovernedBankOpeningOnPeriodStartAsBeginningCash` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.CoreFinancialReportingFoundationTests.IncomeStatement_ShouldDeriveFromPostedGlAndExcludeDraftJournals` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.CoreFinancialReportingFoundationTests.NaturalAccountSegmentFilter_ShouldApplyAcrossAllThreeCoreReports` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.CoreFinancialReportingFoundationTests.TrialBalance_ShouldDeriveFromPostedGlAndExcludeDraftJournals` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.DatabaseSeedFinanceProvisioningIsolationTests.FinanceThenSupplierSeedOrder_Twice_PreservesUnchangedGraphAndConvergesFinanceManifests` | Current-only failure — corrected and follow-up passed |
| `ErpSystem.Api.Tests.Services.Finance.FinanceBudgetCommitmentServiceTests.Dimensioned_position_counts_only_posted_sets_containing_the_budget_assignments` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FinanceBudgetCommitmentServiceTests.Position_uses_adopted_budget_posted_actuals_and_active_reservations` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FinanceBudgetCommitmentServiceTests.Posting_outcome_requires_exact_related_source_action_and_never_writes_actuals` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FinanceBudgetControlServiceTests.Department_budget_return_must_match_account_combination_segment` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FinanceBudgetControlServiceTests.Editing_source_journal_supersedes_override_and_cancels_pending_workflow` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FinanceBudgetControlServiceTests.Exact_adopted_budget_cell_reserves_and_releases_available_amount` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FinanceBudgetControlServiceTests.Missing_budget_cell_names_the_adopted_scenario_control_dimensions` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FinanceBudgetControlServiceTests.Other_pending_journal_reservation_reduces_available_budget` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FinanceBudgetControlServiceTests.Posted_journal_uses_immutable_budget_evidence_and_retains_approved_override` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FinanceBudgetControlServiceTests.Posting_consumes_only_exact_reserved_evidence` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FinanceBudgetControlServiceTests.Tracked_expense_without_adopted_budget_fails_closed` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FinanceBudgetControlServiceTests.Untracked_expense_account_is_outside_control` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FinanceConcurrencyHardeningTests.CustomerPaymentMigration_ShouldReplaceLegacyCustomerFkWithBusinessPartnerFk` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FinanceConcurrencyHardeningTests.SalesReturnBusinessPartnerMigration_ShouldBeArchivedWhileTheCurrentBaselineIsDiscovered` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FinancePostingEngineTests.StableBookIdentityMigration_ShouldPreflightBackfillAndCreateBookQualifiedConstraints` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FinanceSettingsMigrationGapTests.RepairMigration_ShouldBeDiscoveredBeforeTheMetadataReconciliationMigration` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FinanceSettingsWriteOffMappingTests.Mapping_rejects_an_ineligible_account_before_changing_any_settings(mapping: "expense", defect: "control")` | Both passed on rerun |
| `ErpSystem.Api.Tests.Services.Finance.FinanceSettingsWriteOffMappingTests.Mapping_rejects_an_ineligible_account_before_changing_any_settings(mapping: "expense", defect: "deleted")` | Both passed on rerun |
| `ErpSystem.Api.Tests.Services.Finance.FinanceSettingsWriteOffMappingTests.Mapping_rejects_an_ineligible_account_before_changing_any_settings(mapping: "expense", defect: "empty")` | Both passed on rerun |
| `ErpSystem.Api.Tests.Services.Finance.FinanceSettingsWriteOffMappingTests.Mapping_rejects_an_ineligible_account_before_changing_any_settings(mapping: "expense", defect: "foreign-tenant")` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FinanceSettingsWriteOffMappingTests.Mapping_rejects_an_ineligible_account_before_changing_any_settings(mapping: "expense", defect: "inactive")` | Both passed on rerun |
| `ErpSystem.Api.Tests.Services.Finance.FinanceSettingsWriteOffMappingTests.Mapping_rejects_an_ineligible_account_before_changing_any_settings(mapping: "expense", defect: "missing")` | Both passed on rerun |
| `ErpSystem.Api.Tests.Services.Finance.FinanceSettingsWriteOffMappingTests.Mapping_rejects_an_ineligible_account_before_changing_any_settings(mapping: "expense", defect: "summary")` | Both passed on rerun |
| `ErpSystem.Api.Tests.Services.Finance.FinanceSettingsWriteOffMappingTests.Mapping_rejects_an_ineligible_account_before_changing_any_settings(mapping: "expense", defect: "wrong-type")` | Both passed on rerun |
| `ErpSystem.Api.Tests.Services.Finance.FinanceSettingsWriteOffMappingTests.Mapping_rejects_an_ineligible_account_before_changing_any_settings(mapping: "recovery", defect: "control")` | Both passed on rerun |
| `ErpSystem.Api.Tests.Services.Finance.FinanceSettingsWriteOffMappingTests.Mapping_rejects_an_ineligible_account_before_changing_any_settings(mapping: "recovery", defect: "deleted")` | Both passed on rerun |
| `ErpSystem.Api.Tests.Services.Finance.FinanceSettingsWriteOffMappingTests.Mapping_rejects_an_ineligible_account_before_changing_any_settings(mapping: "recovery", defect: "empty")` | Both passed on rerun |
| `ErpSystem.Api.Tests.Services.Finance.FinanceSettingsWriteOffMappingTests.Mapping_rejects_an_ineligible_account_before_changing_any_settings(mapping: "recovery", defect: "foreign-tenant")` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FinanceSettingsWriteOffMappingTests.Mapping_rejects_an_ineligible_account_before_changing_any_settings(mapping: "recovery", defect: "inactive")` | Both passed on rerun |
| `ErpSystem.Api.Tests.Services.Finance.FinanceSettingsWriteOffMappingTests.Mapping_rejects_an_ineligible_account_before_changing_any_settings(mapping: "recovery", defect: "missing")` | Both passed on rerun |
| `ErpSystem.Api.Tests.Services.Finance.FinanceSettingsWriteOffMappingTests.Mapping_rejects_an_ineligible_account_before_changing_any_settings(mapping: "recovery", defect: "summary")` | Both passed on rerun |
| `ErpSystem.Api.Tests.Services.Finance.FinanceSettingsWriteOffMappingTests.Mapping_rejects_an_ineligible_account_before_changing_any_settings(mapping: "recovery", defect: "wrong-type")` | Both passed on rerun |
| `ErpSystem.Api.Tests.Services.Finance.FinanceSourceDimensionServiceTests.Additional_account_cannot_be_foreign_or_inactive(foreign: False)` | Both passed on rerun |
| `ErpSystem.Api.Tests.Services.Finance.FinanceSourceDimensionServiceTests.Additional_account_cannot_be_foreign_or_inactive(foreign: True)` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FinancialStatementClassificationLayoutPhase3Tests.ProtectedStandards_ShouldSeedPerBookAndRemainIdempotent` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FinancialStatementLayoutServiceTests.Publish_ShouldRollBackSnapshotAndStatusWhenAuditFails` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FixedAssetCapitalizationFoundationTests.ProcurementAcceptedSupplyInvoice_ShouldClearGrvAndNotCapitalizeAssetAgain` | Current-only failure — corrected and follow-up passed |
| `ErpSystem.Api.Tests.Services.Finance.FixedAssetDepreciationServiceTests.RunDepreciationAsync_WhenRequestedBookIsFullyDepreciatedButAnotherBookIsNot_KeepsAssetActive` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FixedAssetReportingReconciliationFoundationTests.AdditionsReportIncludesApAndDirectCapitalizationAndExcludesExpenseLines` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FixedAssetReportingReconciliationFoundationTests.CrossTenantAssetCategoryAndAccountFiltersAreRejected` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FixedAssetReportingReconciliationFoundationTests.DepreciationAndAccumulatedDepreciationReportsTiePostedSchedulesToGl` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FixedAssetReportingReconciliationFoundationTests.DisposalReportShowsProceedsNbvGainAndPresentationWarning` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FixedAssetReportingReconciliationFoundationTests.FixedAssetRegisterIsTenantScopedAndAudited` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FixedAssetReportingReconciliationFoundationTests.FixedAssetRegisterUsesRequestedBookValuesAndExcludesMasterOnlyAssets` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FixedAssetReportingReconciliationFoundationTests.GlReconciliationShowsZeroVarianceForCleanData` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FixedAssetReportingReconciliationFoundationTests.ReconciliationDetectsGlMovementWithoutFixedAssetSourceReference` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FixedAssetReportingReconciliationFoundationTests.ReconciliationDetectsMissingPostingReferencesAndSubledgerWithoutGl` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FixedAssetReportingReconciliationFoundationTests.RegisterShowsDisposedAssetWithZeroCurrentNbv` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FixedAssetReportingReconciliationFoundationTests.ReportsDoNotRelySolelyOnMutableBookValueFields` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FixedAssetReportingReconciliationFoundationTests.RevaluationAndImpairmentMovementReportTiesRecordsToGl` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FixedAssetReportingReconciliationFoundationTests.RevaluationSurplusReconciliationSubtractsCompletedDisposalEquityTransfer` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FixedAssetReportingReconciliationFoundationTests.RollForwardTiesOpeningPlusMovementsToClosingAndCategoryTotals` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FixedAssetReportingReconciliationFoundationTests.TransferReportShowsCustodySegmentHistoryWithoutGlMutation` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.ApPaymentAllocatedToMultipleInvoicesCalculatesFxPerAllocation` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.ApSettlementRateDecreasePostsRealizedGain` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.ApSettlementRateIncreasePostsRealizedLoss` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.ArReceiptAllocatedToMultipleInvoicesCalculatesFxPerAllocation` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.ArSettlementRateDecreasePostsRealizedLoss` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.ArSettlementRateIncreasePostsRealizedGain` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.ClosedPeriodRealizedFxSettlementIsRejectedThroughPostingEngine` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.ClosedPeriodRevaluationIsRejected` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.CrossTenantClosingRateIsRejectedForRevaluation` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.CrossTenantExchangeRateSnapshotIsRejectedForSettlement` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.CrossTenantRealizedFxAccountMappingIsRejected` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.CrossTenantUnrealizedFxAccountMappingIsRejected` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.DuplicateRealizedFxPostingReturnsExistingSettlement` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.DuplicateRevaluationIsIdempotentAndReversalPostsInOpenPeriod` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.ExplicitPolicyCanRevalueEveryCoreAccountTypeUsingSignedBalances(accountType: Asset, expectedAdjustment: 200, expectedGainLossType: "Gain")` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.ExplicitPolicyCanRevalueEveryCoreAccountTypeUsingSignedBalances(accountType: Equity, expectedAdjustment: -200, expectedGainLossType: "Loss")` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.ExplicitPolicyCanRevalueEveryCoreAccountTypeUsingSignedBalances(accountType: Expense, expectedAdjustment: 200, expectedGainLossType: "Gain")` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.ExplicitPolicyCanRevalueEveryCoreAccountTypeUsingSignedBalances(accountType: Liability, expectedAdjustment: -200, expectedGainLossType: "Loss")` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.ExplicitPolicyCanRevalueEveryCoreAccountTypeUsingSignedBalances(accountType: Revenue, expectedAdjustment: -200, expectedGainLossType: "Loss")` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.ForeignApPaymentSettlingFunctionalInvoiceUsesRateOneHistoricalBasis` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.ForeignArReceiptSettlingFunctionalInvoiceUsesRateOneHistoricalBasis` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.ForeignBankRateDecreasePostsUnrealizedLossAndBankCredit` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.ForeignBankRateIncreasePostsBankDebitAndUnrealizedGain` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.ForeignBankRevaluationUsesPostedGlSnapshotsNotBankCurrentBalance` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.FunctionalOrThirdCurrencySettlementUsesFrozenPaymentSideEvidence` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.GainLossPostingAccountDriftInvalidatesPreviewFingerprint` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.GovernanceWarningDriftInvalidatesPreviewFingerprint` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.LaterExchangeRateEditDoesNotMutatePostedRealizedFxSnapshot` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.LaterExchangeRateEditDoesNotMutatePostedUnrealizedRevaluationSnapshot` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.MarkedGeneralLedgerMonetaryAccountIsRevaluedAndUnmarkedAccountIsExcluded` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.MissingClosingRateBlocksUnrealizedRevaluation` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.MissingRealizedFxMappingRejectsSettlementPosting` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.MissingUnrealizedFxMappingRejectsRevaluationPosting` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.NextPeriodRevaluationAfterUnreversedPriorBatchUsesPriorCarryingAdjustment` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.PartialApSettlementCalculatesProportionalFx` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.PartialArReceiptCalculatesProportionalFx` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.PostedHistoryRetainsNonstandardPolicyWarningCount` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.PostingRejectsWhenPreviewEvidenceHasChanged` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.PostingRequiresCanonicalPreviewFingerprint` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.RetryAfterPartialAncillaryFinalizationRecordsEverySideEffectExactlyOnce(faultPoint: AfterBankAudit)` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.RetryAfterPartialAncillaryFinalizationRecordsEverySideEffectExactlyOnce(faultPoint: AfterPostedAudit)` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.RetryAfterPartialAncillaryFinalizationRecordsEverySideEffectExactlyOnce(faultPoint: AfterRateAudit)` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.RetryAfterPartialAncillaryFinalizationRecordsEverySideEffectExactlyOnce(faultPoint: AfterRateUsage)` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.RetryAfterPostCommitFailureReconcilesSingleJournalAndAuditEvidence` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.RevaluationAfterReversalDoesNotDoubleCountPriorAdjustment` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.RevaluationFrequencyControlsWhichRunIncludesTheAccount` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.RevaluationUsesConfiguredClosingQuoteSide` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.RevaluationUsesRequestedQuarterEndRateType` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.SameRateSettlementProducesNoRealizedFxJournal` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.UnrealizedRevaluationPostsApArAndForeignBankSignsThroughPostingEngine` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.FxRealizedUnrealizedRevaluationTests.WithholdingSettlementDoesNotOverstateRealizedFxBasis` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.GhanaTaxReportingExportFoundationTests.CovidLevyDiagnostic_ShouldDetectCurrentActiveCovidConfiguration` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.GhanaTaxReportingExportFoundationTests.NonTaxableSuppliesReport_ShouldExposeExplicitLineTreatments` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.GhanaTaxReportingExportFoundationTests.OutputInputAndNetReports_ShouldUsePostedTaxSnapshots_NotCurrentRates` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.GhanaTaxReportingExportFoundationTests.TaxAccountReconciliation_ShouldTieSnapshotsAndWithholdingToPostedGl` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.GhanaTaxReportingExportFoundationTests.TaxOutputExport_ShouldUseTaxReportServiceAndCreateAuditEvent` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.GhanaTaxReportingExportFoundationTests.TaxReports_ShouldRejectCrossTenantTaxAccountFilter` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.GhanaTaxReportingExportFoundationTests.WithholdingReports_ShouldUsePostedPaymentDataAndCertificateReferences` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.JournalBatchSpreadsheetServiceTests.TemplatePreviewCommitAndExport_ShouldRoundTripBatchAndJournalValues` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.JournalVoucherDocumentBuilderTests.Render_ForeignCurrencyJournal_ShouldLabelFunctionalAmountsAndPrintTransactionEvidence` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.JournalVoucherDocumentBuilderTests.Render_ManualJournal_ShouldRetainItsOwnApprovalEvidence` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.JournalVoucherDocumentBuilderTests.Render_OpeningBalanceJournal_ShouldFailClosedWhenApprovalWorkflowIsIncomplete` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.JournalVoucherDocumentBuilderTests.Render_OpeningBalanceJournal_ShouldFailClosedWhenPostingEventDoesNotMatch` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.JournalVoucherDocumentBuilderTests.Render_OpeningBalanceJournal_ShouldFailClosedWhenSourceApprovalEvidenceIsInvalid(includeSource: False, linkJournal: True, includeApprovalDate: True, sameTenant: True, sourceStatus: "Posted")` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.JournalVoucherDocumentBuilderTests.Render_OpeningBalanceJournal_ShouldFailClosedWhenSourceApprovalEvidenceIsInvalid(includeSource: True, linkJournal: False, includeApprovalDate: True, sameTenant: True, sourceStatus: "Posted")` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.JournalVoucherDocumentBuilderTests.Render_OpeningBalanceJournal_ShouldFailClosedWhenSourceApprovalEvidenceIsInvalid(includeSource: True, linkJournal: True, includeApprovalDate: False, sameTenant: True, sourceStatus: "Posted")` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.JournalVoucherDocumentBuilderTests.Render_OpeningBalanceJournal_ShouldFailClosedWhenSourceApprovalEvidenceIsInvalid(includeSource: True, linkJournal: True, includeApprovalDate: True, sameTenant: False, sourceStatus: "Posted")` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.JournalVoucherDocumentBuilderTests.Render_OpeningBalanceJournal_ShouldFailClosedWhenSourceApprovalEvidenceIsInvalid(includeSource: True, linkJournal: True, includeApprovalDate: True, sameTenant: True, sourceStatus: "Approved")` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.JournalVoucherDocumentBuilderTests.Render_OpeningBalanceJournal_ShouldPrintCanonicalSourceApprovalWithoutFalsifyingJournalApproval` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ProcurementFinanceReconciliationTests.Reconciliation_ShouldExcludeReversedAllocationOriginalAtCutoff` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ProcurementFinanceReconciliationTests.Reconciliation_ShouldExplainUnbalancedAndUncontrolledReversalGaps` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.ProcurementFinanceReconciliationTests.Reconciliation_ShouldWeightPaymentPostingByAllEffectiveAllocations` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.ApAgingConsolidatesForeignCurrencyBalancesInFunctionalCurrency` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.ApAgingUsesSettlementReadModelInsteadOfMutablePaidField` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.ApControlReconciliationShowsZeroVarianceForCleanData` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.ApPostedInvoiceAppearsInSettlementReadModel` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.ArAgingConsolidatesForeignCurrencyBalancesInFunctionalCurrency` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.ArAgingUsesSettlementReadModelInsteadOfMutablePaidFields` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.ArControlReconciliationShowsZeroVarianceForCleanData` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.ArCreditNoteReducesOutstanding` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.ArPostedInvoiceAppearsInSettlementReadModel` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.CrossTenantAllocationsAreDiagnosedAndExcluded` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.CustomerAdvanceAllocationUsesItsPostedApplicationEvent` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.ForeignCurrencyApSettlementUsesPostedSnapshotsAndFxLink` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.ForeignCurrencyArSettlementUsesPostedSnapshotsAndFxLink` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.FullApPaymentClosesOutstanding` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.FullArReceiptClosesOutstanding` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.LegacyUnappliedReceiptIsNotMisclassifiedAsCustomerAdvance` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.MissingSourceJournalIsDiagnosed` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.PartialApPaymentReducesOutstandingAndHandlesWithholding` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.PartialArReceiptReducesOutstandingAndReportsWithholding` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.PostedCustomerAdvanceAppearsSeparatelyFromInvoiceAgingAndRebuildIsIdempotent` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.PostedSupplierAdvanceAppearsSeparatelyFromInvoiceAging` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.RebuildAndControlReportsEmitAuditEvents` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.RebuildIsIdempotent` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.ReversedSalesCreditNoteDoesNotRemainInArSettlementProjection` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.SalesCreditNoteWithLegacyConflictingReferencesUsesItsAppliedInvoiceOnly` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SubledgerSettlementReadModelFoundationTests.SupplierAdvanceAllocationWithoutPostedApplicationEventIsDiagnosedAndExcluded` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.SupplierDebitNoteFoundationTests.MigrationRepairsHistoricalTablesAndCreatesApplicationConstraints` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.WhtCertificateDocumentBuilderTests.Issue_ShouldFailClosedForNonIssuedOrUnpostedCertificate` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.WhtCertificateDocumentBuilderTests.Issue_ShouldRetainExactOriginalAndReasonBackedReplacement` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.WhtCertificateDocumentBuilderTests.Retrieval_ShouldRejectTamperedPrivateBytes` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.WhtComplianceLifecycleTests.CertificateLifecycle_ShouldRetainImmutableVersionsOnReissueAndCancellation` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Finance.WhtComplianceLifecycleTests.RemittanceLifecycle_ShouldSnapshotLiabilityEvidenceAndPreventCertificateDrift` | Both failed — same boundary |
| `ErpSystem.Api.Tests.Services.Inventory.InventoryDisposalServiceTests.Draft_edit_retains_old_lines_in_history_and_changes_only_active_quantities` | Both failed — same boundary |

The focused follow-up covers the full PDF conversion, Finance seed-isolation and fixed-asset capitalization test classes. The API test restore was refreshed to restore missing native PDFium assets. The SQLite test adapter now preserves the plan-line and landed-cost supplier-document computed expressions using SQLite syntax, and the procurement fixed-asset fixture supplies the PO, accepted-supply, configuration and audit dependencies required for posting-time matching. Production validation and existing assertions were retained.

Latest follow-up outcome per test: {'Passed': 25}. Evidence: `tmp/procurement-validation-20260925/api-current-only-followup.trx` and `api-seed-isolation-followup.trx` (the latter supersedes the seed-isolation case).
