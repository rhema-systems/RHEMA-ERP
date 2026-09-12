using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Services.DocumentManagement;
using ErpSystem.Data;
using ErpSystem.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementReviewRegressionTests
{
    [Fact]
    public async Task Generic_repository_preserves_preassigned_aggregate_identity()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new ApplicationDbContext(options, tenantId);
        var repository = new GenericRepository<Warehouse>(context);
        var expectedId = Guid.NewGuid();
        var warehouse = new Warehouse
        {
            Id = expectedId,
            TenantId = tenantId,
            Code = "ID-001",
            Name = "Identity regression warehouse"
        };

        await repository.AddAsync(warehouse);

        warehouse.Id.Should().Be(expectedId);
        context.Entry(warehouse).Property(item => item.Id).CurrentValue
            .Should().Be(expectedId);
    }

    [Fact]
    public async Task Generic_repository_generates_identity_only_when_explicitly_empty()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new ApplicationDbContext(options, tenantId);
        var repository = new GenericRepository<Warehouse>(context);
        var preservedId = Guid.NewGuid();
        var notifications = new[]
        {
            NewWarehouse(tenantId, preservedId),
            NewWarehouse(tenantId, Guid.Empty)
        };

        await repository.AddRangeAsync(notifications);

        notifications[0].Id.Should().Be(preservedId);
        notifications[1].Id.Should().NotBe(Guid.Empty);
        notifications[1].Id.Should().NotBe(preservedId);
    }

    [Fact]
    public void Purchase_order_source_binding_is_guarded_by_a_transactional_outbox()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementPurchaseOrderSourceService.cs");

        source.Should().Contain("PO_SOURCE_BOUND_TRANSACTION_REQUIRED");
        source.Should().Contain("NotificationTopicPublisher only appends durable Notification queue rows");
        source.Should().Contain("await PublishNotificationAsync(");
    }

    [Fact]
    public void Draft_and_submitted_purchase_orders_check_capacity_while_final_approval_posts_finance()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementPurchaseOrderSourceService.cs");
        var reserveStart = source.IndexOf(
            "public async Task ReserveAsync(",
            StringComparison.Ordinal);
        var reserveEnd = source.IndexOf(
            "public async Task EnsureBudgetCommitmentForIssueAsync(",
            reserveStart,
            StringComparison.Ordinal);
        var reserveBody = source[reserveStart..reserveEnd];

        reserveStart.Should().BeGreaterThanOrEqualTo(0);
        reserveEnd.Should().BeGreaterThan(reserveStart);
        reserveBody.Should().Contain("await EnsureAwardBudgetCapacityAsync(");
        reserveBody.Should().NotContain("await EnsureBudgetCommitmentAsync(",
            "a Draft PO must not create Finance exposure");
        var issueEnd = source.IndexOf(
            "public async Task EnsureBudgetAvailabilityForSubmissionAsync(",
            reserveEnd,
            StringComparison.Ordinal);
        issueEnd.Should().BeGreaterThan(reserveEnd);
        var issueBody = source[reserveEnd..issueEnd];
        issueBody.Should().Contain("await EnsureBudgetCommitmentAsync(",
            "the controlled final-approval path must establish Finance exposure");
        issueBody.IndexOf("EvaluateCurrentAsync(", StringComparison.Ordinal)
            .Should().BeLessThan(
                issueBody.IndexOf("HasIdempotentPurchaseOrderExposureAsync(", StringComparison.Ordinal),
                "replays must still validate immutable source, supplier, lines, and integrity hash");
        var commitmentStart = source.IndexOf(
            "private async Task EnsureBudgetCommitmentAsync(",
            StringComparison.Ordinal);
        var commitmentEnd = source.IndexOf(
            "internal static void EnsureAwardBudgetExposure(",
            commitmentStart,
            StringComparison.Ordinal);
        var commitmentBody = source[commitmentStart..commitmentEnd];
        commitmentBody.Should().Contain("ApprovePermission",
            "the independent approver must not require the maker's PO-create permission to post the final commitment");
        commitmentBody.Should().Contain("GetBudgetForUpdateAsync(",
                "the cumulative exposure calculation must be serialized on the authoritative budget row")
            .And.Contain("ProcurementBudgetCommitmentLedgerEntryType.FormalCommitment")
            .And.NotContain("item.Status != \"Cancelled\"",
                "draft and pending purchase orders are not formal budget exposure");

        var controller = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Controllers", "Procurement",
            "PurchaseOrdersController.cs");
        var approvalStart = controller.IndexOf(
            "public Task<IActionResult> ApprovePurchaseOrder(",
            StringComparison.Ordinal);
        var submissionStart = controller.IndexOf(
            "private async Task<IActionResult> SubmitPurchaseOrderCore(",
            StringComparison.Ordinal);
        var approvalBody = controller[approvalStart..submissionStart];
        var submissionBody = controller[submissionStart..];

        approvalBody.Should().Contain("_unitOfWork.ExecuteInStrategyAsync(",
            "SQL Server retry strategies require the complete approval transaction to run inside the strategy delegate");
        approvalBody.Should().Contain("private async Task<IActionResult> ApprovePurchaseOrderCore(");
        approvalBody.Should().NotContain(
            "return StatusCode(500, \"An error occurred while approving the purchase order\")",
            "unexpected approval failures must reach the global exception logger with their real exception and stack trace");
        approvalBody.Should().Contain("workflowResult.Outcome == WorkflowOutcome.Approved");
        approvalBody.Should().Contain("EnsureBudgetCommitmentForIssueAsync(");
        var reserveIndex = approvalBody.IndexOf(
            "EnsureBudgetCommitmentForIssueAsync(", StringComparison.Ordinal);
        var formalCommitIndex = approvalBody.IndexOf(
            "CommitPurchaseOrderAsync(", reserveIndex, StringComparison.Ordinal);
        var formalSaveIndex = approvalBody.IndexOf(
            "SaveChangesAsync(", formalCommitIndex, StringComparison.Ordinal);
        var applyStatusIndex = approvalBody.IndexOf(
            "ApplyApprovalOutcome(", StringComparison.Ordinal);
        formalCommitIndex.Should().BeGreaterThan(reserveIndex,
            "final approval must reserve before creating the immutable formal exposure");
        formalSaveIndex.Should().BeGreaterThan(formalCommitIndex,
            "the immutable ledger must be persisted inside the transaction before the PO enters Approved");
        applyStatusIndex.Should().BeGreaterThan(formalSaveIndex,
            "the SQL trigger must be able to see the formal ledger before the final PO status is persisted");
        submissionBody.Should().Contain("EnsureBudgetAvailabilityForSubmissionAsync(");
        submissionBody.Should().NotContain("EnsureBudgetCommitmentForIssueAsync(",
            "submission revalidates budget availability but must not post a firm commitment");
        submissionBody.Should().Contain(
            "HasActiveApprovalWorkflowAsync(",
            "a missing PO workflow must be reported as configuration, not as a maker-checker violation");
        submissionBody.Should().Contain("PO_APPROVAL_WORKFLOW_NOT_CONFIGURED");
        submissionBody.IndexOf("HasActiveApprovalWorkflowAsync(", StringComparison.Ordinal)
            .Should().BeLessThan(
                submissionBody.IndexOf("SubmitAsync(\"PurchaseOrder\"", StringComparison.Ordinal),
                "the controller must fail clearly before the shared workflow fallback can auto-complete");
    }

    [Fact]
    public void Routine_startup_does_not_republish_tenant_disabled_uat_workflows()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Services", "DatabaseSeedingService.cs");
        var lightweightStart = source.IndexOf(
            "public async Task SeedWorkflowDefinitionsAsync()",
            StringComparison.Ordinal);
        var lightweightEnd = source.IndexOf(
            "private async Task EnsureFinancePermissionAssignmentsAsync()",
            lightweightStart,
            StringComparison.Ordinal);
        var lightweightBody = source[lightweightStart..lightweightEnd];

        lightweightBody.Should().NotContain("EnsurePublishedPurchaseOrderApprovalWorkflowForUatAsync");
        lightweightBody.Should().NotContain("_procurementAccessControlSeeder.SeedAsync");
        source.Should().Contain("_procurementAccessControlSeeder.SeedAsync(preserveExistingWorkflows: true)",
            "normal development seeding must preserve existing Draft edits as well as inactive workflows");
    }

    [Fact]
    public void Plan_item_purchase_order_conversion_maps_source_authorization_to_forbidden()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Controllers", "Procurement",
            "ProcurementPlansController.cs");

        source.Should().Contain(
            "catch (ProcurementPurchaseOrderSourceAuthorizationException ex)");
        source.Should().Contain(
            "catch (ProcurementAccessAuthorizationException ex)");
        source.Should().Contain("StatusCodes.Status403Forbidden");
        source.Should().Contain("PO_SOURCE_FORBIDDEN");
    }

    [Fact]
    public void Plan_item_purchase_order_conversion_uses_only_the_governed_commitment()
    {
        var plan = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementPlanService.cs");
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementPurchaseOrderSourceService.cs");

        plan.Should().NotContain("Don't fail the PO creation if budget commitment fails");
        plan.Should().NotContain("Failed to commit budget for PO");
        source.Should().Contain("await EnsureBudgetCommitmentAsync(");
        source.Should().Contain(
            "ProcurementPurchaseOrderComplianceRules.ValidateCommitment(");
    }

    [Fact]
    public void Contract_activation_and_commitment_release_are_fail_closed()
    {
        var activation = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementContractActivationService.cs");
        var budget = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementRequisitionBudgetControlService.cs");

        activation.Should().Contain("await EvaluateBudgetAvailabilityAsync(");
        activation.Should().Contain("_budgetControl.GetDownstreamReadinessAsync(");
        budget.Should().Contain("await EnsureNoDownstreamExposureAsync(");
        budget.Should().Contain("PR_BUDGET_DOWNSTREAM_EXPOSURE_ACTIVE");
        budget.Should().Contain("item.Status != \"Terminated\"");
    }

    [Fact]
    public void Commitment_lifecycle_migration_hard_stops_invalid_issuance_and_early_release()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Data", "Migrations",
            "20260812100000_HardenProcurementCommitmentLifecycle.cs");

        source.Should().Contain("TR_PurchaseOrders_GovernedCommitment");
        source.Should().Contain(
            "release.BudgetCommitmentReference <> commitment.ReservationReference");
        source.Should().Contain("budget.Status NOT IN ('Approved', 'Active')");
        source.Should().Contain("SUM(po.TotalAmount)");
        source.Should().Contain(
            "TR_ProcurementBudgetCommitments_DownstreamExposure");
        source.Should().Contain("d.Status = 1");
        source.Should().Contain("i.Status = 2");
        source.Should().Contain("active purchase-order or contract exposure");
    }

    [Fact]
    public void Final_receipt_inspection_workflow_decision_is_inside_atomic_outcome_scope()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementReceiptInspectionService.cs");
        var decideStart = source.IndexOf(
            "public async Task<ProcurementReceiptInspectionDto> DecideAsync",
            StringComparison.Ordinal);
        var executeStart = source.IndexOf(
            "await ExecuteAsync(async () =>",
            decideStart,
            StringComparison.Ordinal);
        var workflowDecision = source.IndexOf(
            "_workflow.ProcessApprovalAsync",
            decideStart,
            StringComparison.Ordinal);
        var outcomeApplication = source.IndexOf(
            "ApplyAcceptedQuantitiesAndStockAsync",
            decideStart,
            StringComparison.Ordinal);
        var decideEnd = source.IndexOf(
            "public async Task<ProcurementReceiptInspectionDto> AcknowledgeAsync",
            decideStart,
            StringComparison.Ordinal);

        decideStart.Should().BeGreaterThanOrEqualTo(0);
        executeStart.Should().BeGreaterThan(decideStart);
        workflowDecision.Should().BeGreaterThan(executeStart,
            "the shared-workflow decision must be consumed inside the serializable transaction");
        outcomeApplication.Should().BeGreaterThan(workflowDecision,
            "stock and inspection outcome application must follow workflow processing in the same transaction");
        outcomeApplication.Should().BeLessThan(decideEnd);
        source[decideStart..executeStart].Should().NotContain("_workflow.ProcessApprovalAsync");
    }

    [Fact]
    public void Receipt_inspection_evidence_is_source_bound_and_revalidated_before_stock()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementReceiptInspectionService.cs");

        source.Should().Contain("workflow.EntityId == inspection.Id");
        source.Should().Contain("workflow.EntityId == inspection.PurchaseOrderReceiptId");
        source.Should().Contain(
            "item.DocumentRecord.SourceRecordId == inspection.PurchaseOrderReceiptId");
        source.Should().Contain("private async Task RevalidateEvidenceAsync(");
        source.Should().Contain("RCV_EVIDENCE_STALE");

        var decisionStart = source.IndexOf(
            "public async Task<ProcurementReceiptInspectionDto> DecideAsync",
            StringComparison.Ordinal);
        var evidenceRevalidation = source.IndexOf(
            "await RevalidateEvidenceAsync(inspection, cancellationToken);",
            decisionStart,
            StringComparison.Ordinal);
        var stockPosting = source.IndexOf(
            "await ApplyAcceptedQuantitiesAndStockAsync(inspection, cancellationToken);",
            decisionStart,
            StringComparison.Ordinal);

        evidenceRevalidation.Should().BeGreaterThan(decisionStart);
        stockPosting.Should().BeGreaterThan(evidenceRevalidation,
            "current evidence state and hashes must be checked inside final approval before stock posting");
    }

    [Fact]
    public void Receipt_source_pending_quantities_are_scoped_to_each_strategy_attempt()
    {
        var contract = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Interfaces", "Procurement",
            "IProcurementReceiptSourceControlService.cs");
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementReceiptSourceControlService.cs");
        var inspection = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementReceiptInspectionService.cs");

        contract.Should().Contain("void ResetInventoryPostingAttempt();");
        source.Should().Contain("_pendingInventoryPostingQuantities.Clear();");
        var strategyStart = inspection.IndexOf(
            "await _unitOfWork.ExecuteInStrategyAsync(async () =>",
            StringComparison.Ordinal);
        var reset = inspection.IndexOf(
            "_sourceControl.ResetInventoryPostingAttempt();",
            strategyStart,
            StringComparison.Ordinal);
        var transaction = inspection.IndexOf(
            "await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable",
            strategyStart,
            StringComparison.Ordinal);

        reset.Should().BeGreaterThan(strategyStart);
        reset.Should().BeLessThan(transaction,
            "a retry must discard quantities accumulated by its rolled-back predecessor");
    }

    [Fact]
    public void Procurement_finance_reconciliation_reconstructs_invoice_state_at_cutoff()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Services", "Finance", "AP",
            "ApReportsService.cs");

        source.Should().Contain("item.SubmittedDate.Value < cutoffExclusive");
        source.Should().Contain("item.ApprovedDate.Value < cutoffExclusive");
        source.Should().Contain("FinanceAuditEvents.ApInvoiceVoided");
        source.Should().Contain("FinanceAuditEvents.ApInvoiceReversed");
        source.Should().Contain("reversedInvoiceIdsAsOf");
    }

    [Fact]
    public void Receipt_document_actions_are_actor_aware_and_require_complete_signatures()
    {
        var service = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Services",
            "PurchaseOrderReceiptDocumentService.cs");
        var component = ReadRepositoryFile(
            "frontend", "src", "components", "procurement",
            "ReceiptDocumentControl.tsx");

        service.Should().Contain("_access.CheckCapabilityAsync");
        service.Should().Contain("AllowedSignatureRoles = allowedSignatureRoles");
        service.Should().Contain("AllowedActions(item, manageAllowed, issueAllowed");
        component.Should().Contain("overview.allowedActions.includes('ensure')");
        component.Should().Contain("document.allowedSignatureRoles.filter");
        component.Should().Contain("allowedActions.has('issue')");
        component.Should().Contain("roles.length > 0");
    }

    [Fact]
    public void Governed_receipts_cannot_use_legacy_completion_or_projection_cancellation()
    {
        var controller = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Controllers", "Procurement",
            "PurchaseOrdersController.cs");
        var grnService = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Inventory",
            "GoodsReceiptNoteService.cs");

        var endpoint = controller.IndexOf(
            "public async Task<IActionResult> CompleteInspectionAndPostToInventory",
            StringComparison.Ordinal);
        var lifecycleGuard = controller.IndexOf(
            "RCV_INSPECTION_LIFECYCLE_REQUIRED", endpoint,
            StringComparison.Ordinal);
        var legacyAcceptance = controller.IndexOf(
            "receipt.Status = \"Accepted\"", endpoint,
            StringComparison.Ordinal);

        lifecycleGuard.Should().BeGreaterThan(endpoint);
        lifecycleGuard.Should().BeLessThan(legacyAcceptance,
            "governed receipts must fail closed before the legacy endpoint mutates state");
        grnService.Should().Contain("if (grn.PurchaseOrderReceiptId.HasValue)");
        grnService.Should().Contain(
            "cannot be cancelled independently; use the linked receipt-inspection lifecycle");
    }

    [Fact]
    public void Quality_hold_closure_revalidates_all_evidence_and_replacements_by_po_line()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementReceiptInspectionService.cs");
        var closeStart = source.IndexOf(
            "public async Task<ProcurementReceiptInspectionDto> CloseAsync",
            StringComparison.Ordinal);
        var closeEnd = source.IndexOf(
            "public async Task EnsureEvidenceCurrentAsync", closeStart,
            StringComparison.Ordinal);
        var evidenceRevalidation = source.IndexOf(
            "await RevalidateEvidenceAsync(inspection, cancellationToken);",
            closeStart, StringComparison.Ordinal);
        var release = source.IndexOf(
            "inspection.QualityHold = false;", closeStart,
            StringComparison.Ordinal);

        evidenceRevalidation.Should().BeGreaterThan(closeStart);
        evidenceRevalidation.Should().BeLessThan(release);
        release.Should().BeLessThan(closeEnd);
        source.Should().Contain("rejectedByPurchaseOrderItem");
        source.Should().Contain("acceptedByPurchaseOrderItem");
        source.Should().Contain(
            "every rejected purchase-order line");
        source.Should().Contain("replacementRequestedAt");
        source.Should().Contain("replacement.CreatedAt < replacementRequestedAt.Value");
        source.Should().Contain("tdc0502-replacement:");
        source.Should().Contain("RCV_REPLACEMENT_ALREADY_ALLOCATED");
        source.Should().Contain("original.ReplacementPurchaseOrderReceiptId = replacement.Id");
        source.Should().Contain("original.ReplacementInspectionCaseId = acceptedCaseId.Value");
    }

    [Fact]
    public void Receipt_document_issue_revalidates_evidence_and_hashes_finalized_content()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Services",
            "PurchaseOrderReceiptDocumentService.cs");
        var issueStart = source.IndexOf(
            "public async Task<ProcurementReceiptDocumentDto> IssueAsync",
            StringComparison.Ordinal);
        var evidenceRevalidation = source.IndexOf(
            "_receiptInspection.EnsureEvidenceCurrentAsync", issueStart,
            StringComparison.Ordinal);
        var issuedSnapshot = source.IndexOf(
            "var issuedSnapshot = IssuedSourceSnapshot(document, inspection);",
            issueStart, StringComparison.Ordinal);
        var pdf = source.IndexOf(
            "var pdf = BuildPdf(document, template, inspection);",
            issueStart, StringComparison.Ordinal);

        evidenceRevalidation.Should().BeGreaterThan(issueStart);
        issuedSnapshot.Should().BeGreaterThan(evidenceRevalidation);
        issuedSnapshot.Should().BeLessThan(pdf,
            "the PDF must advertise the hash of finalized acceptance, evidence, and signatures");
        source.Should().Contain("tdc.receipt-document.issued.v2");
        source.Should().Contain("evidence = inspection.Evidence");
        source.Should().Contain("signatures = document.Signatures");
        source.Should().Contain("private const string IssuePermission = ManagePermission;",
            "an authorised Stores actor must be able to issue a fully signed receipt document");
    }

    [Fact]
    public void Unissued_receipt_documents_cannot_be_cancelled_and_stranded()
    {
        var service = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Services",
            "PurchaseOrderReceiptDocumentService.cs");
        var rules = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementReceiptDocumentRules.cs");

        service.Should().Contain(
            "if (document.Status != ProcurementReceiptDocumentStatus.Issued)");
        service.Should().Contain(
            "unissued register entries must remain available for signature and issue");
        rules.Should().NotContain(
            "(ProcurementReceiptDocumentStatus.Draft, ProcurementReceiptDocumentStatus.Cancelled)");
        rules.Should().NotContain(
            "(ProcurementReceiptDocumentStatus.PendingSignatures, ProcurementReceiptDocumentStatus.Cancelled)");
    }

    [Fact]
    public void Receipt_supplier_and_resolution_events_share_their_state_transaction()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementReceiptInspectionService.cs");

        foreach (var method in new[] { "AcknowledgeAsync", "ResolveAsync" })
        {
            var start = source.IndexOf(
                $"public async Task<ProcurementReceiptInspectionDto> {method}",
                StringComparison.Ordinal);
            var nextMethod = source.IndexOf(
                "public async Task<ProcurementReceiptInspectionDto>",
                start + 1,
                StringComparison.Ordinal);
            var execute = source.IndexOf("await ExecuteAsync(async () =>", start,
                StringComparison.Ordinal);
            var save = source.IndexOf("await _unitOfWork.SaveChangesAsync", execute,
                StringComparison.Ordinal);
            var controlEvent = source.IndexOf("await RecordEventAsync", save,
                StringComparison.Ordinal);

            execute.Should().BeGreaterThan(start);
            save.Should().BeGreaterThan(execute);
            controlEvent.Should().BeGreaterThan(save);
            controlEvent.Should().BeLessThan(nextMethod,
                $"{method} must commit its lifecycle state and immutable event together");
        }
    }

    [Fact]
    public void Receipt_inspection_create_save_and_submit_events_share_their_state_transaction()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementReceiptInspectionService.cs");

        foreach (var method in new[] { "InitializeAsync", "SaveAsync", "SubmitAsync" })
        {
            var start = source.IndexOf(
                $"public async Task<ProcurementReceiptInspectionDto> {method}",
                StringComparison.Ordinal);
            var nextMethod = source.IndexOf(
                "public async Task<ProcurementReceiptInspectionDto>",
                start + 1,
                StringComparison.Ordinal);
            var execute = source.IndexOf("await ExecuteAsync(async () =>", start,
                StringComparison.Ordinal);
            var save = source.IndexOf("await _unitOfWork.SaveChangesAsync", execute,
                StringComparison.Ordinal);
            var controlEvent = source.IndexOf("await RecordEventAsync", save,
                StringComparison.Ordinal);

            execute.Should().BeGreaterThan(start);
            save.Should().BeGreaterThan(execute);
            controlEvent.Should().BeGreaterThan(save);
            controlEvent.Should().BeLessThan(nextMethod,
                $"{method} must persist state and its immutable event in one transaction");
        }
    }

    [Fact]
    public void Receipt_stock_authorization_observes_saved_acceptance_and_reuses_pending_balances()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementReceiptInspectionService.cs");
        var applyStart = source.IndexOf(
            "private async Task ApplyAcceptedQuantitiesAndStockAsync(",
            StringComparison.Ordinal);
        var applyEnd = source.IndexOf(
            "private async Task SynchronizeLinkedGoodsReceiptNoteAsync(",
            applyStart,
            StringComparison.Ordinal);
        var apply = source[applyStart..applyEnd];
        var save = apply.IndexOf("await _unitOfWork.SaveChangesAsync", StringComparison.Ordinal);
        var stock = apply.IndexOf("await PostStockAsync", StringComparison.Ordinal);

        save.Should().BeGreaterThanOrEqualTo(0);
        stock.Should().BeGreaterThan(save,
            "the no-tracking source-control reload must see accepted receipt quantities");
        apply.Should().Contain("pendingInventoryLocations");
        apply.Should().Contain("pendingWarehouseQuantities");
        source.Should().Contain("pendingInventoryLocations.TryGetValue");
        source.Should().Contain("pendingWarehouseQuantities.TryGetValue");
    }

    [Fact]
    public void Procurement_evidence_consumers_require_the_current_published_dms_version()
    {
        var rules = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "DocumentManagement",
            "CentralDocumentEvidenceRules.cs");
        var receipt = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementReceiptInspectionService.cs");
        var apService = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Services", "Finance", "AP",
            "VendorInvoiceMatchExceptionService.cs");
        var apValidator = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Services", "Finance", "AP",
            "VendorInvoiceMatchExceptionEvidenceValidator.cs");

        rules.Should().Contain("LifecycleStatus == ActiveLifecycleStatus");
        rules.Should().Contain("VersionStatus == PublishedVersionStatus");
        rules.Should().Contain("CurrentVersion == version.VersionNumber");
        rules.Should().Contain("version.Status == PublishedVersionStatus");
        rules.Should().Contain("version.PublishedAt.HasValue");
        receipt.Should().Contain("CentralDocumentEvidenceRules.CurrentPublished()");
        apService.Should().Contain("CentralDocumentEvidenceRules.CurrentPublished()");
        apValidator.Should().Contain("CentralDocumentEvidenceRules.CurrentPublished()");
    }

    [Fact]
    public void Central_dms_evidence_rule_rejects_noncurrent_or_inactive_versions()
    {
        var record = new CentralDocumentRecord
        {
            LifecycleStatus = "Active",
            VersionStatus = "Published",
            CurrentVersion = "v2.0"
        };
        var version = new CentralDocumentVersion
        {
            DocumentRecord = record,
            VersionNumber = "v2.0",
            Status = "Published",
            PublishedAt = DateTime.UtcNow
        };
        var isConsumable = CentralDocumentEvidenceRules.CurrentPublished().Compile();

        isConsumable(version).Should().BeTrue();

        record.LifecycleStatus = "Cancelled";
        isConsumable(version).Should().BeFalse();
        record.LifecycleStatus = "Active";
        record.CurrentVersion = "v3.0";
        isConsumable(version).Should().BeFalse();
        record.CurrentVersion = "v2.0";
        version.Status = "Superseded";
        isConsumable(version).Should().BeFalse();
        version.Status = "Published";
        version.PublishedAt = null;
        isConsumable(version).Should().BeFalse();
    }

    [Fact]
    public void Sequential_mrn_issue_action_is_hidden_until_the_grn_is_issued()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Services",
            "PurchaseOrderReceiptDocumentService.cs");

        source.Should().Contain("var grnIssued = documents.Any");
        source.Should().Contain("RequiresPriorGrnIssue(");
        source.Should().Contain("bool issueSequenceSatisfied");
        source.Should().Contain("if (issueAllowed && issueSequenceSatisfied)");
        source.Should().Contain(
            "return Map(document, config, manageAllowed, issueAllowed, issueSequenceSatisfied);");
    }

    [Fact]
    public void Receipt_document_sign_and_cancel_use_atomic_state_event_scope()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Services",
            "PurchaseOrderReceiptDocumentService.cs");

        source.Should().Contain("private async Task<T> ExecuteAtomicAsync<T>(");
        source.Should().Contain("BeginTransactionAsync(");
        source.Should().Contain("IsolationLevel.Serializable");
        source.Should().Contain("await transaction.RollbackAsync(cancellationToken);");

        foreach (var operation in new[] { "Sign", "Cancel" })
        {
            var wrapper = source.IndexOf(
                $"public Task<ProcurementReceiptDocumentDto> {operation}Async",
                StringComparison.Ordinal);
            var core = source.IndexOf(
                $"private async Task<ProcurementReceiptDocumentDto> {operation}CoreAsync",
                wrapper,
                StringComparison.Ordinal);
            var atomicCall = source.IndexOf("ExecuteAtomicAsync(", wrapper,
                StringComparison.Ordinal);
            var save = source.IndexOf("await _db.SaveChangesAsync", core,
                StringComparison.Ordinal);
            var controlEvent = source.IndexOf("await RecordEventAsync", save,
                StringComparison.Ordinal);

            atomicCall.Should().BeGreaterThan(wrapper);
            atomicCall.Should().BeLessThan(core);
            controlEvent.Should().BeGreaterThan(save,
                $"{operation} state and its control event must execute in one atomic delegate");
        }
    }

    [Fact]
    public void Approved_match_exception_evidence_is_revalidated_when_consumed()
    {
        var invoice = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Services", "Finance", "AP",
            "VendorInvoiceService.cs");
        var exception = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Services", "Finance", "AP",
            "VendorInvoiceMatchExceptionService.cs");
        var validator = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Services", "Finance", "AP",
            "VendorInvoiceMatchExceptionEvidenceValidator.cs");

        exception.Should().Contain(
            "VendorInvoiceMatchExceptionEvidenceValidator.RevalidateAsync");
        invoice.Should().Contain("item.ApprovalControlEventId == candidate.Id");
        invoice.Should().Contain(
            "await VendorInvoiceMatchExceptionEvidenceValidator.RevalidateAsync");
        validator.Should().Contain("current.IsCurrent");
        validator.Should().Contain("WorkflowMalwareScanStatus.Clean");
        validator.Should().Contain("FileVirusScanStatus.Clean");
        validator.Should().Contain("row.EvidenceHash");
    }

    [Fact]
    public void Payments_reject_duplicate_invoice_allocations_and_serialize_processing_batches()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Services", "Finance", "AP",
            "VendorPaymentService.cs");

        source.Should().Contain("AP_PAYMENT_DUPLICATE_INVOICE_ALLOCATION");
        source.Should().Contain("ExecuteUpdateAsync");
        source.Should().Contain("AP_PAYMENT_BATCH_ALREADY_PROCESSING");
        source.Should().Contain("interruptedBefore");
        source.Should().Contain("batch.ProcessedDate = DateTime.UtcNow");
        source.Should().Contain("IsDurablyPosted(item.VendorPayment)");
        source.Should().Contain("EnsureBatchResumeAllocationsMatch");
        source.Should().Contain("Checkpoint each item while the batch remains Processing");
        source.Should().Contain("AP_PAYMENT_BATCH_RESUME_ALLOCATION_MISMATCH");
        source.Should().Contain("GetInvoiceUnreservedBalanceAsync");
        source.Should().Contain("liveAllocationReservation");
        source.Should().Contain("batchSelectionReservation");
        source.Should().Contain("availableBalanceByInvoice");
        source.Should().Contain("AP_PAYMENT_BALANCE_RESERVED");
    }

    [Fact]
    public void Inspection_retries_reset_source_and_valuation_attempt_state()
    {
        var inspection = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementReceiptInspectionService.cs");
        var valuation = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Inventory",
            "InventoryValuationService.cs");

        inspection.Should().Contain("_sourceControl.ResetInventoryPostingAttempt();");
        inspection.Should().Contain("_valuation.ResetProcessingAttempt();");
        valuation.Should().Contain("public void ResetProcessingAttempt()");
        valuation.Should().Contain("_balanceCache.Clear();");
        valuation.Should().Contain("_movementNumberPrefix = null;");
        valuation.Should().Contain("_movementNumberNext = 0;");
    }

    [Fact]
    public void Match_exception_assignment_and_completion_fail_before_database_uniqueness_errors()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Services", "Finance", "AP",
            "VendorInvoiceMatchExceptionService.cs");

        source.Should().Contain("AP_MATCH_EXCEPTION_OWNER_PERMISSION_REQUIRED");
        source.Should().Contain("FinancePermissions.ManageApInvoices");
        source.Should().Contain("AP_MATCH_EXCEPTION_EVIDENCE_ALREADY_ATTACHED");
        source.Should().Contain("existingEvidenceKeys.Contains");
    }

    [Fact]
    public void Rejected_only_receipts_do_not_claim_stock_was_updated()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Procurement",
            "ProcurementReceiptInspectionService.cs");

        source.Should().Contain(
            "var stockWasAccepted = grn.TotalQuantityAccepted > 0m;");
        source.Should().Contain("? GRNStatus.StockUpdated");
        source.Should().Contain(": GRNStatus.Rejected;");
        source.Should().Contain("grn.StockUpdated = stockWasAccepted;");
        source.Should().Contain(
            "grn.StockUpdatedAt = stockWasAccepted ? DateTime.UtcNow : null;");
    }

    [Fact]
    public void Receipt_idempotency_replay_is_bound_to_the_effective_payload()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Controllers", "Procurement",
            "PurchaseOrdersController.cs");

        source.Should().Contain(".Include(item => item.Items)");
        source.Should().Contain("ReceiptReplayMatches(existingReceipt, receiveDto)");
        source.Should().Contain("RCV_IDEMPOTENCY_PAYLOAD_MISMATCH");
        source.Should().Contain("stored.ReceivedQuantity != requested.ReceivedQuantity");
        source.Should().Contain("stored.LocationId != requested.LocationId");
    }

    [Fact]
    public void Grn_and_match_exception_replays_are_bound_to_complete_payloads()
    {
        var grn = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Inventory",
            "GoodsReceiptNoteService.cs");
        var exception = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Services", "Finance", "AP",
            "VendorInvoiceMatchExceptionService.cs");

        grn.Should().Contain("BuildIdempotencyRequestHash");
        grn.Should().Contain("existing.IdempotencyRequestHash");
        grn.Should().Contain("RCV_IDEMPOTENCY_PAYLOAD_MISMATCH");
        grn.Should().Contain("item.StorageLocationId");
        exception.Should().Contain("RequestFingerprint(invoiceId, request)");
        exception.Should().Contain("RequestFingerprint(duplicate)");
        exception.Should().Contain("AP_MATCH_EXCEPTION_IDEMPOTENCY_PAYLOAD_MISMATCH");
        exception.Should().Contain("EvidenceReference = item.EvidenceReference.Trim()");
    }

    [Fact]
    public void Historical_reconciliation_uses_applied_po_amendment_commercial_snapshots()
    {
        var report = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Services", "Finance", "AP",
            "ApReportsService.cs");

        report.Should().Contain("ResolvePurchaseOrderCommercialStateAsOf");
        report.Should().Contain("effectiveAmendment.BeforeSnapshotJson");
        report.Should().Contain("effectiveAmendment.ProposedSnapshotJson");
        report.Should().Contain("commercialState.ItemUnitPrices");
        report.Should().Contain("PurchaseOrderAmount = RoundMoney(commercialState.TotalAmount)");
        report.Should().Contain("HashJson(snapshotJson)");
    }

    [Fact]
    public void Payment_batch_currency_and_invoice_picker_queries_are_bounded()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Services", "Finance", "AP",
            "VendorPaymentService.cs");

        source.Should().Contain("AP_PAYMENT_BATCH_CURRENCY_MISMATCH");
        source.Should().Contain("var bySupplierAndCurrency = invoices.GroupBy");
        source.Should().Contain("MaximumOutstandingInvoicePageSize");
        source.Should().Contain(".Take(boundedPageSize)");
        source.Should().Contain("GetInvoiceUnreservedBalancesAsync");
        source.Should().Contain(".GroupBy(allocation => allocation.VendorInvoiceId)");
        source.Should().Contain("preloadedInvoice: i");
    }

    [Fact]
    public void Payment_certificate_history_is_persisted_for_cutoff_reconciliation()
    {
        var projectService = ReadRepositoryFile(
            "src", "ErpSystem.Core", "Services", "Projects",
            "ProjectService.CommercialAdministration.cs");
        var reportService = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Services", "Finance", "AP",
            "ApReportsService.cs");

        projectService.Should().Contain("AddPaymentCertificateSnapshotAuditAsync");
        projectService.Should().Contain("ProjectPaymentCertificateAuditEvents.Snapshot");
        projectService.Should().Contain("OldValues = oldValues is null");
        reportService.Should().Contain("ResolvePaymentCertificateStateAsOf");
        reportService.Should().Contain("firstAfterCutoff?.OldValues");
        reportService.Should().Contain("ProcurementWorksCloseoutActionType.RetentionRelease");
    }

    [Fact]
    public void Issued_document_retry_repairs_post_issue_work_and_unissued_actions_match_api()
    {
        var source = ReadRepositoryFile(
            "src", "ErpSystem.Api", "Services",
            "PurchaseOrderReceiptDocumentService.cs");
        var issuedBranch = source.IndexOf(
            "if (document.Status == ProcurementReceiptDocumentStatus.Issued)",
            StringComparison.Ordinal);
        var resume = source.IndexOf(
            "return await ResumeIssuedAsync(document, correlation, cancellationToken);",
            issuedBranch, StringComparison.Ordinal);
        var allowedActions = source.IndexOf(
            "private static IReadOnlyList<string> AllowedActions(",
            StringComparison.Ordinal);
        var issuedActions = source.IndexOf(
            "if (document.Status == ProcurementReceiptDocumentStatus.Issued)",
            allowedActions, StringComparison.Ordinal);
        var issuedActionsEnd = source.IndexOf(
            "return result;", issuedActions, StringComparison.Ordinal);
        var unissuedStart = source.IndexOf(
            "if (issueAllowed && issueSequenceSatisfied)", issuedActionsEnd,
            StringComparison.Ordinal);
        var unissuedEnd = source.IndexOf(
            "if (canSign)", unissuedStart, StringComparison.Ordinal);
        var unissuedActions = source[unissuedStart..unissuedEnd];

        resume.Should().BeGreaterThan(issuedBranch);
        source.Should().Contain("private async Task<ProcurementReceiptDocumentDto> ResumeIssuedAsync");
        source.Should().Contain("hasIssuedEvent");
        source.Should().Contain("await ReconcileDocumentAsync(document, config, cancellationToken)");
        unissuedActions.Should().NotContain("result.Add(\"cancel\");");
    }

    private static Warehouse NewWarehouse(Guid tenantId, Guid id) => new()
    {
        Id = id,
        TenantId = tenantId,
        Code = $"ID-{Guid.NewGuid():N}"[..12],
        Name = "Identity regression warehouse"
    };

    private static string ReadRepositoryFile(params string[] path)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the repository root should be discoverable");
        return File.ReadAllText(Path.Combine([directory!.FullName, .. path]));
    }
}
