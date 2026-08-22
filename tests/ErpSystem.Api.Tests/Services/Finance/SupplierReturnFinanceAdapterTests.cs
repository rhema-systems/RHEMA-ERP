using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class SupplierReturnFinanceAdapterTests
{
    [Fact]
    public async Task Uninvoiced_dispatch_remains_non_posting_until_authoritative_evidence_is_derived()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Context();
        var posting = Posting();
        var adapter = Adapter(db, tenantId, posting.Object);

        var result = await adapter.ConsumeDispatchAsync(Dispatch(tenantId));

        result.Status.Should().Be(SupplierReturnFinanceOutcomeStatus.DecisionRequired);
        result.PostingEventId.Should().BeNull();
        result.JournalEntryId.Should().BeNull();
        result.DecisionCodes.Should().Contain("FIN-INT-012-AUTHORITATIVE-EVIDENCE-LOOKUP");
        posting.Verify(engine => engine.PostAsync(
            It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Same_immutable_dispatch_never_creates_a_posting_while_contract_is_planned()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Context();
        var posting = Posting();
        var adapter = Adapter(db, tenantId, posting.Object);
        var dispatch = Dispatch(tenantId);

        var first = await adapter.ConsumeDispatchAsync(dispatch);
        var retry = await adapter.ConsumeDispatchAsync(dispatch);

        first.Status.Should().Be(SupplierReturnFinanceOutcomeStatus.DecisionRequired);
        retry.Status.Should().Be(SupplierReturnFinanceOutcomeStatus.DecisionRequired);
        first.DecisionCodes.Should().Equal(retry.DecisionCodes);
        posting.Verify(engine => engine.PostAsync(
            It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Invoiced_dispatch_fails_closed_until_return_clearing_is_approved()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Context();
        var posting = Posting();
        var adapter = Adapter(db, tenantId, posting.Object);
        var dispatch = Rehash(Dispatch(tenantId) with
        {
            InvoiceState = SupplierReturnInvoiceState.Invoiced,
            OriginalVendorInvoiceId = Guid.NewGuid()
        });

        var result = await adapter.ConsumeDispatchAsync(dispatch);

        result.Status.Should().Be(SupplierReturnFinanceOutcomeStatus.DecisionRequired);
        result.DecisionCodes.Should().Contain("FIN-INT-012-INVOICED-RETURN-CLEARING");
        posting.Verify(engine => engine.PostAsync(
            It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Dispatch_rejects_tenant_context_mismatch()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Context();
        var posting = Posting();
        var adapter = Adapter(db, Guid.NewGuid(), posting.Object);

        Func<Task> action = () => adapter.ConsumeDispatchAsync(Dispatch(tenantId));

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tenant does not match*");
        posting.Verify(engine => engine.PostAsync(
            It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Dispatch_rejects_unapproved_or_unposted_producer_evidence()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Context();
        var adapter = Adapter(db, tenantId, Posting().Object);
        var missingApprover = Dispatch(tenantId) with { ApprovedByUserId = Guid.Empty };
        var unpostedLine = Dispatch(tenantId).Lines.Single() with { IsInventoryMovementPosted = false };
        var unpostedMovement = Rehash(Dispatch(tenantId) with { Lines = [unpostedLine] });

        Func<Task> missingApproval = () => adapter.ConsumeDispatchAsync(missingApprover);
        Func<Task> movementNotPosted = () => adapter.ConsumeDispatchAsync(unpostedMovement);

        await missingApproval.Should().ThrowAsync<ArgumentException>().WithMessage("*Approval actor*");
        await movementNotPosted.Should().ThrowAsync<ArgumentException>().WithMessage("*posted by Inventory*");
    }

    [Fact]
    public async Task Dispatch_rejects_duplicate_inventory_movement_identity()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Context();
        var adapter = Adapter(db, tenantId, Posting().Object);
        var first = Dispatch(tenantId).Lines.Single();
        var duplicate = first with
        {
            PurchaseOrderLineId = Guid.NewGuid(),
            InventoryItemId = Guid.NewGuid(),
            InventoryMovementReference = "IMV-RTV-002"
        };
        var request = Rehash(Dispatch(tenantId) with { Lines = [first, duplicate] });

        Func<Task> action = () => adapter.ConsumeDispatchAsync(request);

        await action.Should().ThrowAsync<ArgumentException>().WithMessage("*movement ids must be unique*");
    }

    [Fact]
    public async Task Dispatch_fails_closed_when_inventory_already_has_a_finance_posting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Context();
        var posting = Posting();
        var adapter = Adapter(db, tenantId, posting.Object);

        var result = await adapter.ConsumeDispatchAsync(Rehash(Dispatch(tenantId) with
        {
            InventoryMovementHasSeparateFinancePosting = true
        }));

        result.Status.Should().Be(SupplierReturnFinanceOutcomeStatus.DecisionRequired);
        result.DecisionCodes.Should().Contain("FIN-INT-012-DUPLICATE-GL-OWNER");
        posting.Verify(engine => engine.PostAsync(
            It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Dispatch_fails_closed_on_unapproved_return_variance()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Context();
        var posting = Posting();
        var adapter = Adapter(db, tenantId, posting.Object);
        var changedLine = Dispatch(tenantId).Lines.Single() with
        {
            OriginalGrvAccrualAmountFunctional = 125m
        };

        var result = await adapter.ConsumeDispatchAsync(Rehash(
            Dispatch(tenantId) with { Lines = [changedLine] }));

        result.Status.Should().Be(SupplierReturnFinanceOutcomeStatus.DecisionRequired);
        result.DecisionCodes.Should().Contain("FIN-INT-012-RETURN-VARIANCE-POLICY");
        posting.Verify(engine => engine.PostAsync(
            It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Dispatch_requires_planned_version_and_retry_identity()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Context();
        var adapter = Adapter(db, tenantId, Posting().Object);

        Func<Task> wrongVersion = () =>
            adapter.ConsumeDispatchAsync(Dispatch(tenantId) with { ContractVersion = "1.0" });
        Func<Task> missingRetryIdentity = () =>
            adapter.ConsumeDispatchAsync(Dispatch(tenantId) with { IdempotencyKey = "" });

        await wrongVersion.Should().ThrowAsync<ArgumentException>().WithMessage("*0.1*");
        await missingRetryIdentity.Should().ThrowAsync<ArgumentException>().WithMessage("*Idempotency key*");
    }

    [Fact]
    public async Task Dispatch_rejects_payload_mutation_when_producer_reuses_old_integrity_hash()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Context();
        var posting = Posting();
        var adapter = Adapter(db, tenantId, posting.Object);
        var original = Dispatch(tenantId);
        var changedLine = original.Lines.Single() with
        {
            InventoryCarryingAmountFunctional = 121m,
            OriginalGrvAccrualAmountFunctional = 121m
        };
        var changedWithoutNewHash = original with { Lines = [changedLine] };

        Func<Task> action = () => adapter.ConsumeDispatchAsync(changedWithoutNewHash);

        await action.Should().ThrowAsync<ArgumentException>().WithMessage("*canonical FIN-INT-012*");
        posting.Verify(engine => engine.PostAsync(
            It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Dispatch_correction_retains_lineage_but_does_not_post_without_reversal_policy()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Context();
        var posting = Posting();
        var adapter = Adapter(db, tenantId, posting.Object);
        var correction = Rehash(Dispatch(tenantId) with
        {
            CorrectionOfDispatchId = Guid.NewGuid(),
            CorrectionOrReversalReason = "Correction after carrier evidence review"
        });

        var result = await adapter.ConsumeDispatchAsync(correction);

        result.Status.Should().Be(SupplierReturnFinanceOutcomeStatus.DecisionRequired);
        result.DecisionCodes.Should().Contain("FIN-INT-012-CORRECTION-REVERSAL-POLICY");
        posting.Verify(engine => engine.PostAsync(
            It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Credit_resolution_returns_no_executable_debit_note_preview_until_clearing_mode_exists()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Context();
        var posting = Posting();
        var adapter = Adapter(db, tenantId, posting.Object);
        var resolution = Resolution(tenantId, SupplierReturnCommercialResolutionType.SupplierCreditNote);

        var result = await adapter.ConsumeCommercialResolutionAsync(resolution);

        result.Status.Should().Be(SupplierReturnFinanceOutcomeStatus.DecisionRequired);
        result.SupplierDebitNoteId.Should().BeNull();
        result.DecisionCodes.Should().Contain("FIN-INT-013-DURABLE-FINANCE-LINKAGE");
        result.DecisionCodes.Should().Contain("FIN-INT-013-RETURN-CLEARING-LINE-MODE");
        posting.Verify(engine => engine.PostAsync(
            It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cash_refund_preview_does_not_pretend_bank_settlement_occurred()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Context();
        var adapter = Adapter(db, tenantId, Posting().Object);

        var result = await adapter.ConsumeCommercialResolutionAsync(
            Rehash(Resolution(tenantId, SupplierReturnCommercialResolutionType.CashRefund) with
            {
                SupplierCashRefundReference = "BANK-REFUND-PENDING"
            }));

        result.Status.Should().Be(SupplierReturnFinanceOutcomeStatus.DecisionRequired);
        result.RequiresCashSettlement.Should().BeTrue();
        result.PostingEventId.Should().BeNull();
        result.DecisionCodes.Should().Contain("FIN-INT-013-CASH-REFUND-SETTLEMENT");
    }

    [Theory]
    [InlineData(SupplierReturnCommercialResolutionType.Replacement)]
    [InlineData(SupplierReturnCommercialResolutionType.RepairOrWarranty)]
    public async Task No_charge_replacement_or_repair_stays_pending_until_durable_linkage_exists(
        SupplierReturnCommercialResolutionType type)
    {
        var tenantId = Guid.NewGuid();
        await using var db = Context();
        var posting = Posting();
        var adapter = Adapter(db, tenantId, posting.Object);
        var request = Rehash(Resolution(tenantId, type) with
        {
            OriginalVendorInvoiceId = null,
            SupplierCreditNoteReference = null,
            ReplacementReference = type == SupplierReturnCommercialResolutionType.Replacement
                ? "REPL-ACK-001"
                : null,
            CurrencyCode = string.Empty,
            Lines = Array.Empty<SupplierReturnCommercialResolutionLineDto>()
        });

        var result = await adapter.ConsumeCommercialResolutionAsync(request);

        result.Status.Should().Be(SupplierReturnFinanceOutcomeStatus.DecisionRequired);
        result.DecisionCodes.Should().Contain("FIN-INT-013-DURABLE-FINANCE-LINKAGE");
        result.Message.Should().Contain("No journal is expected");
        posting.Verify(engine => engine.PostAsync(
            It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Agreed_future_credit_requires_durable_supplier_credit_lifecycle()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Context();
        var adapter = Adapter(db, tenantId, Posting().Object);

        var result = await adapter.ConsumeCommercialResolutionAsync(
            Resolution(tenantId, SupplierReturnCommercialResolutionType.AgreedFutureCredit));

        result.Status.Should().Be(SupplierReturnFinanceOutcomeStatus.DecisionRequired);
        result.DecisionCodes.Should().Contain("FIN-INT-013-FUTURE-CREDIT-CLAIM-POLICY");
    }

    [Fact]
    public async Task Rejected_supplier_claim_does_not_silently_create_credit_or_writeoff()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Context();
        var posting = Posting();
        var adapter = Adapter(db, tenantId, posting.Object);
        var request = Rehash(
            Resolution(tenantId, SupplierReturnCommercialResolutionType.ClaimRejected) with
            {
                OriginalVendorInvoiceId = null,
                SupplierCreditNoteReference = null,
                CurrencyCode = string.Empty,
                Lines = Array.Empty<SupplierReturnCommercialResolutionLineDto>()
            });

        var result = await adapter.ConsumeCommercialResolutionAsync(request);

        result.Status.Should().Be(SupplierReturnFinanceOutcomeStatus.DecisionRequired);
        result.DecisionCodes.Should().Contain("FIN-INT-013-CLAIM-REJECTED-CUSTODY-WRITEOFF");
        posting.Verify(engine => engine.PostAsync(
            It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cash_refund_requires_supplier_refund_identity()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Context();
        var adapter = Adapter(db, tenantId, Posting().Object);
        var request = Rehash(
            Resolution(tenantId, SupplierReturnCommercialResolutionType.CashRefund) with
            {
                SupplierCashRefundReference = null
            });

        Func<Task> action = () => adapter.ConsumeCommercialResolutionAsync(request);

        await action.Should().ThrowAsync<ArgumentException>().WithMessage("*cash refund reference*");
    }

    [Fact]
    public async Task Dispatch_rejects_non_utc_contract_timestamps()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Context();
        var adapter = Adapter(db, tenantId, Posting().Object);
        var request = Rehash(Dispatch(tenantId) with
        {
            DispatchedAtUtc = DateTime.SpecifyKind(
                new DateTime(2026, 8, 18, 11, 0, 0),
                DateTimeKind.Local)
        });

        Func<Task> action = () => adapter.ConsumeDispatchAsync(request);

        await action.Should().ThrowAsync<ArgumentException>().WithMessage("*Dispatch timestamp must be expressed in UTC*");
    }

    [Fact]
    public async Task Commercial_resolution_rejects_mutation_when_old_integrity_hash_is_reused()
    {
        var tenantId = Guid.NewGuid();
        await using var db = Context();
        var adapter = Adapter(db, tenantId, Posting().Object);
        var original = Resolution(tenantId, SupplierReturnCommercialResolutionType.SupplierCreditNote);
        var changedLine = original.Lines.Single() with { UnitPrice = 60m, LineTotal = 144m };
        var changedWithoutNewHash = original with { Lines = [changedLine] };

        Func<Task> action = () => adapter.ConsumeCommercialResolutionAsync(changedWithoutNewHash);

        await action.Should().ThrowAsync<ArgumentException>().WithMessage("*canonical FIN-INT-013*");
    }

    private static SupplierReturnFinanceAdapter Adapter(
        ApplicationDbContext db,
        Guid currentTenantId,
        IFinancePostingEngine posting)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.TenantId).Returns(currentTenantId);
        currentUser.SetupGet(user => user.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(user => user.UserName).Returns("finance.test");
        currentUser.SetupGet(user => user.IsAuthenticated).Returns(true);
        return new SupplierReturnFinanceAdapter(currentUser.Object);
    }

    private static Mock<IFinancePostingEngine> Posting(Action<FinancePostingRequestDto>? capture = null)
    {
        var posting = new Mock<IFinancePostingEngine>();
        posting.Setup(engine => engine.PostAsync(
                It.IsAny<FinancePostingRequestDto>(), It.IsAny<CancellationToken>()))
            .Callback<FinancePostingRequestDto, CancellationToken>((request, _) => capture?.Invoke(request))
            .ReturnsAsync(() => new FinancePostingResultDto
            {
                PostingEventId = Guid.NewGuid(),
                JournalEntryId = Guid.NewGuid(),
                JournalEntryNumber = "JE-RTV-TEST",
                PostingStatus = "Posted",
                FunctionalCurrencyCode = "GHS"
            });
        return posting;
    }

    private static SupplierReturnDispatchFinanceDto Dispatch(Guid tenantId)
    {
        var approvedAt = new DateTime(2026, 8, 18, 9, 0, 0, DateTimeKind.Utc);
        var dispatch = new SupplierReturnDispatchFinanceDto
        {
            TenantId = tenantId,
            ContractVersion = "0.1",
            CorrelationId = "RTV-CORR-001",
            IdempotencyKey = "proc-rtv-dispatch-001",
            SupplierReturnId = Guid.NewGuid(),
            SupplierReturnReference = "RTV-2026-0001",
            DispatchId = Guid.NewGuid(),
            DispatchReference = "RTV-DSP-2026-0001",
            SupplierBusinessPartnerId = Guid.NewGuid(),
            PurchaseOrderId = Guid.NewGuid(),
            PurchaseOrderReceiptId = Guid.NewGuid(),
            InvoiceState = SupplierReturnInvoiceState.Uninvoiced,
            ApprovedAtUtc = approvedAt,
            ApprovedByUserId = Guid.NewGuid(),
            ApprovalReference = "PROC-RTV-APP-0001",
            DispatchedAtUtc = approvedAt.AddHours(2),
            InventoryMovementHasSeparateFinancePosting = false,
            FunctionalCurrencyCode = "GHS",
            Reason = "Latent manufacturing defect",
            EvidenceReferences = ["DOC:return-authorisation:001", "DOC:carrier-note:001"],
            Lines =
            [
                new SupplierReturnDispatchLineDto
                {
                    PurchaseOrderLineId = Guid.NewGuid(),
                    InventoryItemId = Guid.NewGuid(),
                    InventoryMovementId = Guid.NewGuid(),
                    InventoryMovementReference = "IMV-RTV-001",
                    IsInventoryMovementPosted = true,
                    InventoryMovementPostedAtUtc = approvedAt.AddMinutes(90),
                    WarehouseId = Guid.NewGuid(),
                    Quantity = 2m,
                    UnitOfMeasure = "EA",
                    InventoryCarryingAmountFunctional = 120m,
                    OriginalGrvAccrualAmountFunctional = 120m
                }
            ]
        };
        return dispatch with
        {
            SourceIntegrityHash = SupplierReturnFinanceContractIntegrity.ComputeDispatchHash(dispatch)
        };
    }

    private static SupplierReturnCommercialResolutionFinanceDto Resolution(
        Guid tenantId,
        SupplierReturnCommercialResolutionType type)
    {
        var resolution = new SupplierReturnCommercialResolutionFinanceDto
        {
            TenantId = tenantId,
            ContractVersion = "0.1",
            CorrelationId = "RTV-CORR-001",
            IdempotencyKey = "proc-rtv-resolution-001",
            ResolutionId = Guid.NewGuid(),
            SupplierReturnId = Guid.NewGuid(),
            SupplierReturnReference = "RTV-2026-0001",
            DispatchId = Guid.NewGuid(),
            SupplierBusinessPartnerId = Guid.NewGuid(),
            PurchaseOrderId = Guid.NewGuid(),
            OriginalVendorInvoiceId = Guid.NewGuid(),
            ResolutionType = type,
            ResolvedAtUtc = new DateTime(2026, 8, 25, 10, 0, 0, DateTimeKind.Utc),
            ApprovedByUserId = Guid.NewGuid(),
            ApprovalReference = "PROC-RTV-RES-APP-0001",
            SupplierCreditNoteReference = "SCN-2026-0042",
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            Reason = "Supplier accepted the latent-defect claim",
            EvidenceReferences = ["DOC:supplier-credit-note:0042"],
            Lines =
            [
                new SupplierReturnCommercialResolutionLineDto
                {
                    OriginalVendorInvoiceLineItemId = Guid.NewGuid(),
                    Description = "Returned pumps",
                    Quantity = 2m,
                    UnitPrice = 50m,
                    TaxRate = 20m,
                    TaxAmount = 20m,
                    LineTotal = 120m
                }
            ]
        };
        return resolution with
        {
            SourceIntegrityHash =
                SupplierReturnFinanceContractIntegrity.ComputeCommercialResolutionHash(resolution)
        };
    }

    private static FinanceSettings Settings(Guid tenantId, Guid inventoryAccountId, Guid grvAccountId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        BaseCurrency = "GHS",
        ControlAccountInventoryId = inventoryAccountId,
        ControlAccountGRVAccrualId = grvAccountId
    };

    private static SupplierReturnDispatchFinanceDto Rehash(SupplierReturnDispatchFinanceDto value) =>
        value with
        {
            SourceIntegrityHash = SupplierReturnFinanceContractIntegrity.ComputeDispatchHash(value)
        };

    private static SupplierReturnCommercialResolutionFinanceDto Rehash(
        SupplierReturnCommercialResolutionFinanceDto value) => value with
    {
        SourceIntegrityHash =
            SupplierReturnFinanceContractIntegrity.ComputeCommercialResolutionHash(value)
    };

    private static ApplicationDbContext Context()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }
}
