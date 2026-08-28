using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;

namespace ErpSystem.Api.Services.Finance.AP;

/// <summary>
/// Finance-owned consumer for the agreed post-acceptance return-to-vendor boundary.
///
/// IMPORTANT FOR PROCUREMENT/INVENTORY OWNERS:
/// - Call FIN-INT-012 only after Procurement approval and after Inventory has posted the authoritative
///   outbound quantity/valuation movement.
/// - That Inventory movement must not also write a Finance journal. Finance is the sole GL writer.
/// - Never pass Finance account ids or write Finance posting/AP tables directly.
/// - Call FIN-INT-013 only when the supplier's commercial response is independently evidenced; physical
///   dispatch is not evidence that a credit, refund, replacement or repair was accepted.
///
/// v0.1 deliberately fails closed where a durable cross-stage link or an approved accounting policy is
/// absent. A DecisionRequired outcome means that no posting or subledger document was created.
/// </summary>
public sealed class SupplierReturnFinanceAdapter : ISupplierReturnFinanceAdapter
{
    private const string DispatchContractId = "FIN-INT-012";
    private const string ResolutionContractId = "FIN-INT-013";
    private const string ContractVersion = "0.1";

    private readonly ICurrentUserService _currentUser;

    public SupplierReturnFinanceAdapter(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public Task<SupplierReturnFinanceOutcomeDto> ConsumeDispatchAsync(
        SupplierReturnDispatchFinanceDto dispatch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        ValidateDispatch(dispatch);
        EnsureCurrentTenant(dispatch.TenantId);

        if (dispatch.CorrectionOfDispatchId.HasValue || dispatch.ReversalOfDispatchId.HasValue)
        {
            return Task.FromResult(DecisionRequired(
                DispatchContractId,
                "Return-dispatch correction/reversal lineage was retained, but v0.1 has no approved compensating-posting orchestration.",
                "FIN-INT-012-CORRECTION-REVERSAL-POLICY"));
        }

        if (dispatch.InventoryMovementHasSeparateFinancePosting)
        {
            return Task.FromResult(DecisionRequired(
                DispatchContractId,
                "Inventory has already requested or created a Finance posting for this movement. Posting again would duplicate the Inventory Control credit.",
                "FIN-INT-012-DUPLICATE-GL-OWNER"));
        }

        if (dispatch.InvoiceState == SupplierReturnInvoiceState.Invoiced)
        {
            // Do not debit AP at dispatch: the supplier has not yet issued/accepted the commercial
            // credit. Do not credit Inventory to a guessed clearing account either. FIN-INT-013 is a
            // separate milestone, and an approved return-clearing policy plus durable cross-stage
            // persistence is required before this branch may post.
            return Task.FromResult(DecisionRequired(
                DispatchContractId,
                "The goods are invoiced. Configure and approve the return-clearing/correlation policy before Finance records the dispatch separately from the supplier credit or refund.",
                "FIN-INT-012-INVOICED-RETURN-CLEARING"));
        }

        if (dispatch.Lines.Any(line => !line.OriginalGrvAccrualAmountFunctional.HasValue))
        {
            return Task.FromResult(DecisionRequired(
                DispatchContractId,
                "An uninvoiced return requires the exact returned portion of the original GRV accrual for every valuation line.",
                "FIN-INT-012-GRV-ALLOCATION-MISSING"));
        }

        var carryingAmount = Round(dispatch.Lines.Sum(line => line.InventoryCarryingAmountFunctional));
        var grvAccrualAmount = Round(dispatch.Lines.Sum(line => line.OriginalGrvAccrualAmountFunctional!.Value));
        if (Math.Abs(carryingAmount - grvAccrualAmount) > 0.01m)
        {
            // Finance Settings currently has no separately governed purchase-return variance policy.
            // Reusing a write-off or migration account would conceal the economic reason for the
            // difference, so v0.1 intentionally does not post it.
            return Task.FromResult(DecisionRequired(
                DispatchContractId,
                $"Inventory carrying value {carryingAmount:0.00} does not equal the returned GRV accrual {grvAccrualAmount:0.00}. An approved purchase-return variance account policy is required.",
                "FIN-INT-012-RETURN-VARIANCE-POLICY"));
        }

        // The canonical hash proves only that this envelope did not change after the producer
        // computed it. It does not prove that the referenced PO receipt, Inventory movement, tenant,
        // remaining GRV accrual, quantities or valuation exist in their authoritative stores.
        //
        // Finance must derive those facts tenant-scoped from approved read contracts before calling
        // IFinancePostingEngine. Until that contract exists, even an apparently balanced uninvoiced
        // return is deliberately non-posting. This prevents a forged/self-asserted DTO from moving GL.
        return Task.FromResult(DecisionRequired(
            DispatchContractId,
            "The producer envelope is structurally valid, but Finance did not post it. Implement tenant-scoped authoritative PO-receipt, Inventory-movement and remaining-GRV evidence lookup before enabling the uninvoiced return reversal.",
            "FIN-INT-012-AUTHORITATIVE-EVIDENCE-LOOKUP",
            "FIN-INT-012-DURABLE-IDEMPOTENCY"));
    }

    public Task<SupplierReturnFinanceOutcomeDto> ConsumeCommercialResolutionAsync(
        SupplierReturnCommercialResolutionFinanceDto resolution,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ValidateResolution(resolution);
        EnsureCurrentTenant(resolution.TenantId);

        if (resolution.CorrectionOfResolutionId.HasValue || resolution.ReversalOfResolutionId.HasValue)
        {
            return Task.FromResult(DecisionRequired(
                ResolutionContractId,
                "Commercial-resolution correction/reversal lineage was retained, but v0.1 has no approved compensating subledger orchestration.",
                "FIN-INT-013-CORRECTION-REVERSAL-POLICY"));
        }

        if (resolution.ResolutionType == SupplierReturnCommercialResolutionType.AgreedFutureCredit)
        {
            return Task.FromResult(DecisionRequired(
                ResolutionContractId,
                "The supplier has promised a future credit, but no AP/tax document was created. Emit SupplierCreditNote only when the actual supplier credit document is received and approved.",
                "FIN-INT-013-DURABLE-FINANCE-LINKAGE",
                "FIN-INT-013-FUTURE-CREDIT-CLAIM-POLICY"));
        }

        if (resolution.ResolutionType == SupplierReturnCommercialResolutionType.ClaimRejected)
        {
            return Task.FromResult(DecisionRequired(
                ResolutionContractId,
                "The supplier rejected the claim. Finance created no credit or journal; agree returned-goods custody, recovery and write-off/variance treatment first.",
                "FIN-INT-013-DURABLE-FINANCE-LINKAGE",
                "FIN-INT-013-CLAIM-REJECTED-CUSTODY-WRITEOFF"));
        }

        if (resolution.ResolutionType is SupplierReturnCommercialResolutionType.Replacement
            or SupplierReturnCommercialResolutionType.RepairOrWarranty)
        {
            if (resolution.Lines.Count > 0)
            {
                return Task.FromResult(DecisionRequired(
                    ResolutionContractId,
                    "A replacement/repair outcome contains a commercial value or tax adjustment. Send the supplier credit document as a separate evidenced resolution.",
                    "FIN-INT-013-COMMERCIAL-ADJUSTMENT-DOCUMENT-REQUIRED"));
            }

            return Task.FromResult(DecisionRequired(
                ResolutionContractId,
                resolution.ResolutionType == SupplierReturnCommercialResolutionType.Replacement
                    ? "No journal is expected for an equal-value replacement, but v0.1 cannot mark the outcome complete until durable dispatch-to-replacement correlation/audit persistence exists."
                    : "No journal is expected for a no-charge repair/warranty outcome, but v0.1 cannot mark it complete until durable dispatch-to-repair correlation/audit persistence exists.",
                "FIN-INT-013-DURABLE-FINANCE-LINKAGE",
                "FIN-INT-013-NONACCOUNTING-OUTCOME-CORRELATION"));
        }

        if (!resolution.OriginalVendorInvoiceId.HasValue)
        {
            return Task.FromResult(DecisionRequired(
                ResolutionContractId,
                "A credit/refund/future-credit outcome without a posted source invoice requires a separately approved advance/refund accounting policy.",
                "FIN-INT-013-UNINVOICED-COMMERCIAL-POLICY"));
        }

        if (string.IsNullOrWhiteSpace(resolution.SupplierCreditNoteReference))
        {
            return Task.FromResult(DecisionRequired(
                ResolutionContractId,
                "The supplier's credit-note reference is required before Finance can prepare an AP/tax adjustment.",
                "FIN-INT-013-SUPPLIER-CREDIT-DOCUMENT-REQUIRED"));
        }

        // Do not expose CreateSupplierDebitNoteDto as an executable preview yet. Its current line
        // resolution reverses the original expense/Inventory destination. That is wrong after
        // FIN-INT-012 has already moved goods into an RTV clearing state: the supplier credit must
        // clear the governed RTV balance, not credit Inventory a second time. The future Finance-owned
        // implementation must add an explicit return-clearing line mode plus durable correlation.
        return Task.FromResult(new SupplierReturnFinanceOutcomeDto
        {
            ContractId = ResolutionContractId,
            ContractVersion = ContractVersion,
            Status = SupplierReturnFinanceOutcomeStatus.DecisionRequired,
            PreparedSupplierDebitNote = null,
            RequiresCashSettlement =
                resolution.ResolutionType == SupplierReturnCommercialResolutionType.CashRefund,
            Message = "The supplier resolution is validated as an integration envelope, but no executable AP/tax preview, subledger document or journal was created. Add a governed return-clearing line mode and durable return-resolution linkage first.",
            DecisionCodes =
            [
                "FIN-INT-013-DURABLE-FINANCE-LINKAGE",
                "FIN-INT-013-RETURN-CLEARING-LINE-MODE",
                resolution.ResolutionType == SupplierReturnCommercialResolutionType.CashRefund
                    ? "FIN-INT-013-CASH-REFUND-SETTLEMENT"
                    : "FIN-INT-013-SUPPLIER-CREDIT-LIFECYCLE"
            ]
        });
    }

    private void EnsureCurrentTenant(Guid sourceTenantId)
    {
        var tenantId = _currentUser.GetRequiredFinanceTenantId();
        if (sourceTenantId != tenantId)
        {
            throw new InvalidOperationException(
                "Supplier-return Finance payload tenant does not match the authenticated tenant context.");
        }
    }

    private static void ValidateDispatch(SupplierReturnDispatchFinanceDto value)
    {
        ValidateCommon(
            value.ContractVersion,
            value.TenantId,
            value.CorrelationId,
            value.IdempotencyKey,
            value.SourceIntegrityHash,
            value.ApprovedByUserId,
            value.ApprovalReference,
            value.EvidenceReferences);
        if (!SupplierReturnFinanceContractIntegrity.VerifyDispatch(value))
            throw new ArgumentException("Source integrity hash does not match the canonical FIN-INT-012 dispatch payload.");
        RequiredId(value.SupplierReturnId, "Supplier return id");
        RequiredText(value.SupplierReturnReference, "Supplier return reference", 100);
        RequiredId(value.DispatchId, "Dispatch id");
        RequiredText(value.DispatchReference, "Dispatch reference", 100);
        RequiredId(value.SupplierBusinessPartnerId, "Supplier BusinessPartner id");
        RequiredId(value.PurchaseOrderId, "Purchase order id");
        RequiredId(value.PurchaseOrderReceiptId, "Purchase order receipt id");
        RequiredUtcDate(value.ApprovedAtUtc, "Approval timestamp");
        RequiredUtcDate(value.DispatchedAtUtc, "Dispatch timestamp");
        if (value.ApprovedAtUtc > value.DispatchedAtUtc)
            throw new ArgumentException("Supplier return must be approved before it is dispatched.");
        RequiredText(value.Reason, "Return reason", 1000);
        Currency(value.FunctionalCurrencyCode);
        ValidateLineage(
            value.CorrectionOfDispatchId,
            value.ReversalOfDispatchId,
            value.CorrectionOrReversalReason,
            "dispatch");
        if (!Enum.IsDefined(value.InvoiceState))
            throw new ArgumentException("Supplier return invoice state is invalid.");
        if (value.InvoiceState == SupplierReturnInvoiceState.Invoiced && !value.OriginalVendorInvoiceId.HasValue)
            throw new ArgumentException("An invoiced return requires the original vendor invoice id.");
        if (value.InvoiceState == SupplierReturnInvoiceState.Uninvoiced && value.OriginalVendorInvoiceId.HasValue)
            throw new ArgumentException("An uninvoiced return cannot reference a posted vendor invoice.");
        if (value.Lines is not { Count: > 0 })
            throw new ArgumentException("Supplier return dispatch requires at least one valuation line.");

        var movementIds = new HashSet<Guid>();
        foreach (var line in value.Lines)
        {
            RequiredId(line.PurchaseOrderLineId, "Purchase order line id");
            RequiredId(line.InventoryItemId, "Inventory item id");
            RequiredId(line.InventoryMovementId, "Inventory movement id");
            if (!movementIds.Add(line.InventoryMovementId))
                throw new ArgumentException("Inventory movement ids must be unique within a supplier return dispatch.");
            RequiredText(line.InventoryMovementReference, "Inventory movement reference", 100);
            if (!line.IsInventoryMovementPosted)
                throw new ArgumentException("Every return movement must be posted by Inventory before Finance is called.");
            RequiredUtcDate(line.InventoryMovementPostedAtUtc, "Inventory movement posting timestamp");
            RequiredId(line.WarehouseId, "Warehouse id");
            if (line.Quantity <= 0m)
                throw new ArgumentException("Returned quantity must be positive.");
            RequiredText(line.UnitOfMeasure, "Unit of measure", 30);
            if (Round(line.InventoryCarryingAmountFunctional) <= 0m)
                throw new ArgumentException("Producer-reported Inventory carrying amount must be positive.");
            if (line.OriginalGrvAccrualAmountFunctional.HasValue &&
                Round(line.OriginalGrvAccrualAmountFunctional.Value) <= 0m)
                throw new ArgumentException("Original GRV accrual allocation must be positive when supplied.");
        }
    }

    private static void ValidateResolution(SupplierReturnCommercialResolutionFinanceDto value)
    {
        ValidateCommon(
            value.ContractVersion,
            value.TenantId,
            value.CorrelationId,
            value.IdempotencyKey,
            value.SourceIntegrityHash,
            value.ApprovedByUserId,
            value.ApprovalReference,
            value.EvidenceReferences);
        if (!SupplierReturnFinanceContractIntegrity.VerifyCommercialResolution(value))
            throw new ArgumentException("Source integrity hash does not match the canonical FIN-INT-013 resolution payload.");
        RequiredId(value.ResolutionId, "Resolution id");
        RequiredId(value.SupplierReturnId, "Supplier return id");
        RequiredText(value.SupplierReturnReference, "Supplier return reference", 100);
        RequiredId(value.DispatchId, "Dispatch id");
        RequiredId(value.SupplierBusinessPartnerId, "Supplier BusinessPartner id");
        RequiredId(value.PurchaseOrderId, "Purchase order id");
        RequiredUtcDate(value.ResolvedAtUtc, "Resolution timestamp");
        RequiredText(value.Reason, "Resolution reason", 1000);
        if (!Enum.IsDefined(value.ResolutionType))
            throw new ArgumentException("Supplier return commercial resolution type is invalid.");
        ValidateLineage(
            value.CorrectionOfResolutionId,
            value.ReversalOfResolutionId,
            value.CorrectionOrReversalReason,
            "resolution");

        if (value.ResolutionType == SupplierReturnCommercialResolutionType.Replacement)
            RequiredText(value.ReplacementReference, "Replacement reference", 100);
        if (value.ResolutionType == SupplierReturnCommercialResolutionType.CashRefund)
            RequiredText(value.SupplierCashRefundReference, "Supplier cash refund reference", 100);

        var isNonAccountingOutcome = value.ResolutionType is
            SupplierReturnCommercialResolutionType.Replacement or
            SupplierReturnCommercialResolutionType.RepairOrWarranty or
            SupplierReturnCommercialResolutionType.AgreedFutureCredit or
            SupplierReturnCommercialResolutionType.ClaimRejected;
        if (isNonAccountingOutcome && value.Lines.Count == 0)
            return;

        Currency(value.CurrencyCode);
        if (value.ExchangeRate <= 0m)
            throw new ArgumentException("Commercial resolution exchange rate must be positive.");
        if (value.Lines is not { Count: > 0 })
            throw new ArgumentException("A commercial credit/refund outcome requires at least one invoice line.");
        var invoiceLineIds = new HashSet<Guid>();
        foreach (var line in value.Lines)
        {
            RequiredId(line.OriginalVendorInvoiceLineItemId, "Original vendor invoice line id");
            if (!invoiceLineIds.Add(line.OriginalVendorInvoiceLineItemId))
                throw new ArgumentException("Original vendor invoice line ids must be unique in one resolution.");
            RequiredText(line.Description, "Resolution line description", 500);
            if (line.Quantity <= 0m || line.UnitPrice <= 0m)
                throw new ArgumentException("Commercial resolution quantity and unit price must be positive.");
            if (line.TaxRate < 0m || line.TaxAmount < 0m || line.DiscountPercentage < 0m ||
                line.DiscountAmount < 0m || line.LineTotal < 0m)
                throw new ArgumentException("Commercial resolution tax, discount and total values cannot be negative.");
        }
    }

    private static void ValidateCommon(
        string contractVersion,
        Guid tenantId,
        string correlationId,
        string idempotencyKey,
        string integrityHash,
        Guid approvedByUserId,
        string approvalReference,
        IReadOnlyList<string> evidenceReferences)
    {
        if (!string.Equals(contractVersion?.Trim(), ContractVersion, StringComparison.Ordinal))
            throw new ArgumentException($"Supplier return contract version must be '{ContractVersion}'.");
        RequiredId(tenantId, "Tenant id");
        RequiredText(correlationId, "Correlation id", 100);
        RequiredText(idempotencyKey, "Idempotency key", 100);
        if (idempotencyKey.Any(char.IsWhiteSpace))
            throw new ArgumentException("Idempotency key cannot contain whitespace.");
        ValidateIntegrityHash(integrityHash);
        RequiredId(approvedByUserId, "Approval actor id");
        RequiredText(approvalReference, "Approval reference", 100);
        if (evidenceReferences is not { Count: > 0 })
            throw new ArgumentException("At least one immutable approval/dispatch evidence reference is required.");
        foreach (var evidenceReference in evidenceReferences)
            RequiredText(evidenceReference, "Evidence reference", 500);
    }

    private static void ValidateLineage(
        Guid? correctionOfId,
        Guid? reversalOfId,
        string? reason,
        string label)
    {
        if (correctionOfId.HasValue && reversalOfId.HasValue)
            throw new ArgumentException($"A {label} cannot be both a correction and a reversal.");
        if ((correctionOfId.HasValue || reversalOfId.HasValue) && string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException($"A {label} correction/reversal reason is required.");
    }

    private static SupplierReturnFinanceOutcomeDto DecisionRequired(
        string contractId,
        string message,
        params string[] decisionCodes) => new()
        {
            ContractId = contractId,
            ContractVersion = ContractVersion,
            Status = SupplierReturnFinanceOutcomeStatus.DecisionRequired,
            Message = message,
            DecisionCodes = decisionCodes
        };

    private static void ValidateIntegrityHash(string? value)
    {
        var normalized = value?.Trim();
        if (normalized is not { Length: 64 } || normalized.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("Source integrity hash must be a 64-character SHA-256 hexadecimal value.");
    }

    private static void RequiredId(Guid value, string label)
    {
        if (value == Guid.Empty)
            throw new ArgumentException($"{label} is required.");
    }

    private static void RequiredUtcDate(DateTime value, string label)
    {
        if (value == default)
            throw new ArgumentException($"{label} is required.");
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException($"{label} must be expressed in UTC.");
    }

    private static string RequiredText(string? value, string label, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{label} is required.");
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
            throw new ArgumentException($"{label} cannot exceed {maximumLength} characters.");
        return normalized;
    }

    private static string Currency(string? value)
    {
        var normalized = RequiredText(value, "Currency code", 3).ToUpperInvariant();
        if (normalized.Length != 3 || normalized.Any(character => character is < 'A' or > 'Z'))
            throw new ArgumentException("Currency code must contain exactly three letters.");
        return normalized;
    }

    private static decimal Round(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
