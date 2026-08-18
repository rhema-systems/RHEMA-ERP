using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Workflow;

public sealed class FinanceWorkflowStatusAdapter : IWorkflowStatusAdapter
{
    private const int FinancePoDraft = 1;
    private const int FinancePoApproved = 2;
    private const int FinancePoPendingApproval = 9;
    private const int FinancePoRejected = 10;

    public IReadOnlyCollection<string> EntityTypes { get; } = new[]
    {
        "JournalEntry",
        "Journal Entry",
        "JournalBatch",
        "Journal Batch",
        "FinancePurchaseOrder",
        "Finance Purchase Order",
        "FinancePurchaseOrderReceipt",
        "Finance Goods Receipt",
        "VendorInvoice",
        "Vendor Invoice",
        "PaymentBatch",
        "Payment Batch",
        "Vendor Payment Batch",
        "SupplierReturn",
        "Supplier Return",
        "Invoice",
        "Customer Invoice",
        "CustomerPayment",
        "Customer Payment",
        "BudgetScenario",
        "Budget Scenario",
        "BudgetReturn",
        "Budget Return",
        "BudgetRevision",
        "Budget Revision",
        "UnitJournalEntry",
        "Unit Journal Entry",
        "UnitAccountBudget",
        "Unit Budget",
        "AllocationRule",
        "Allocation",
        "CashTransaction",
        "Bank Transaction",
        "BankReconciliation",
        "Bank Reconciliation",
        "BankDepositBatch",
        "Bank Deposit",
        "ReturnedChequeCase",
        "Returned Cheque",
        "OpeningBalanceBatch",
        "Opening Balance Batch",
        "FixedAsset",
        "Fixed Asset",
        "FixedAssetDepreciationRun",
        "Asset Depreciation Run",
        "AssetDepreciationSchedule",
        "Asset Depreciation",
        "AssetValuation",
        "Asset Valuation",
        "AssetTransfer",
        "Asset Transfer",
        "AssetDisposal",
        "Asset Disposal",
        "AssetVerificationSession",
        "Asset Verification",
        "CapitalProject",
        "Capital Project",
        "LeaseContract",
        "Lease Contract"
    };

    public void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId)
        => Apply(entity, outcome, userId, rejectionReason: null);

    public void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null)
        => Apply(entity, outcome, userId, rejectionReason);

    public void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        Apply(entity, WorkflowOutcome.Recalled, userId, reason);
    }

    private static void Apply(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason)
    {
        switch (entity)
        {
            case JournalEntry journal:
                ApplyJournalEntry(journal, outcome, userId, rejectionReason);
                return;
            case JournalBatch journalBatch:
                ApplyJournalBatch(journalBatch, outcome, userId, rejectionReason);
                return;
            case FinancePurchaseOrder purchaseOrder:
                SetScalarStatus(purchaseOrder, "Status", outcome, FinancePoDraft, FinancePoPendingApproval, FinancePoApproved, FinancePoRejected);
                StampWorkflowAudit(purchaseOrder, outcome, userId, rejectionReason);
                return;
            case FinancePurchaseOrderReceipt receipt:
                SetScalarStatus(receipt, "Status", outcome, FinancePurchaseOrderReceiptStatus.Draft, FinancePurchaseOrderReceiptStatus.PendingApproval, FinancePurchaseOrderReceiptStatus.Approved, FinancePurchaseOrderReceiptStatus.Rejected);
                StampWorkflowAudit(receipt, outcome, userId, rejectionReason);
                return;
            case VendorInvoice vendorInvoice:
                SetScalarStatus(vendorInvoice, "Status", outcome, VendorInvoiceStatus.Draft, VendorInvoiceStatus.PendingApproval, VendorInvoiceStatus.Approved, VendorInvoiceStatus.Rejected);
                SetTextStatus(vendorInvoice, "ApprovalStatus", outcome, "Draft", "PendingApproval", "Approved", "Rejected");
                StampWorkflowAudit(vendorInvoice, outcome, userId, rejectionReason);
                return;
            case PaymentBatch paymentBatch:
                SetScalarStatus(paymentBatch, "Status", outcome, PaymentBatchStatus.Draft, PaymentBatchStatus.PendingApproval, PaymentBatchStatus.Approved, PaymentBatchStatus.Draft);
                StampWorkflowAudit(paymentBatch, outcome, userId, rejectionReason);
                return;
            case SupplierReturn supplierReturn:
                SetScalarStatus(supplierReturn, "Status", outcome, SupplierReturnStatus.Draft, SupplierReturnStatus.PendingApproval, SupplierReturnStatus.Approved, SupplierReturnStatus.Rejected);
                StampWorkflowAudit(supplierReturn, outcome, userId, rejectionReason);
                return;
            case Invoice invoice:
                SetScalarStatus(invoice, "Status", outcome, InvoiceStatus.Draft, InvoiceStatus.PendingApproval, InvoiceStatus.Approved, InvoiceStatus.Rejected);
                StampWorkflowAudit(invoice, outcome, userId, rejectionReason);
                return;
            case CustomerPayment customerPayment:
                SetTextStatus(customerPayment, "Status", outcome, "Pending", "PendingApproval", "Approved", "Rejected");
                StampWorkflowAudit(customerPayment, outcome, userId, rejectionReason);
                return;
            case BudgetScenario scenario:
                SetTextStatus(scenario, "Status", outcome, "Collecting", "InReview", "Approved", "Collecting");
                if (outcome == WorkflowOutcome.Approved)
                {
                    scenario.LockedDate = DateTime.UtcNow;
                    scenario.LockedByUserId = userId;
                }
                else
                {
                    scenario.LockedDate = null;
                    scenario.LockedByUserId = null;
                }
                StampWorkflowAudit(scenario, outcome, userId, rejectionReason);
                return;
            case BudgetReturn budgetReturn:
                SetTextStatus(budgetReturn, "Status", outcome, "Draft", "Submitted", "Approved", "Rejected");
                StampWorkflowAudit(budgetReturn, outcome, userId, rejectionReason);
                return;
            case BudgetRevision budgetRevision:
                SetTextStatus(budgetRevision, "Status", outcome, "Draft", "Submitted", "Approved", "Rejected");
                if (outcome == WorkflowOutcome.Approved)
                {
                    budgetRevision.ApprovedAt = DateTime.UtcNow;
                    budgetRevision.ApprovedByUserId = userId;
                    budgetRevision.RejectionReason = null;
                }
                else if (outcome == WorkflowOutcome.Rejected)
                {
                    budgetRevision.ApprovedAt = null;
                    budgetRevision.ApprovedByUserId = null;
                    budgetRevision.RejectionReason = rejectionReason;
                }
                StampWorkflowAudit(budgetRevision, outcome, userId, rejectionReason);
                return;
            case UnitJournalEntry unitJournalEntry:
                SetScalarStatus(unitJournalEntry, "Status", outcome, UnitJournalEntryStatus.Draft, UnitJournalEntryStatus.PendingApproval, UnitJournalEntryStatus.Approved, UnitJournalEntryStatus.Rejected);
                StampWorkflowAudit(unitJournalEntry, outcome, userId, rejectionReason);
                return;
            case UnitAccountBudget unitBudget:
                SetTextStatus(unitBudget, "Status", outcome, "Draft", "PendingApproval", "Approved", "Rejected");
                StampWorkflowAudit(unitBudget, outcome, userId, rejectionReason);
                return;
            case AllocationRule allocationRule:
                SetTextStatus(allocationRule, "ApprovalStatus", outcome, "Draft", "PendingApproval", "Approved", "Rejected");
                allocationRule.IsActive = outcome == WorkflowOutcome.Approved;
                StampWorkflowAudit(allocationRule, outcome, userId, rejectionReason);
                return;
            case CashTransaction cashTransaction:
                SetScalarStatus(cashTransaction, "ApprovalStatus", outcome, CashTransactionApprovalStatus.Captured, CashTransactionApprovalStatus.Submitted, CashTransactionApprovalStatus.Approved, CashTransactionApprovalStatus.Rejected);
                StampWorkflowAudit(cashTransaction, outcome, userId, rejectionReason);
                return;
            case BankReconciliation reconciliation:
                SetScalarStatus(reconciliation, "Status", outcome, ReconciliationStatus.InProgress, ReconciliationStatus.Completed, ReconciliationStatus.Approved, ReconciliationStatus.Rejected);
                StampWorkflowAudit(reconciliation, outcome, userId, rejectionReason);
                return;
            case BankDepositBatch deposit:
                SetScalarStatus(
                    deposit,
                    "Status",
                    outcome,
                    BankDepositStatus.Draft,
                    BankDepositStatus.Submitted,
                    BankDepositStatus.Approved,
                    BankDepositStatus.Rejected);
                StampWorkflowAudit(deposit, outcome, userId, rejectionReason);
                return;
            case ReturnedChequeCase returnedCheque:
                SetScalarStatus(
                    returnedCheque,
                    "Status",
                    outcome,
                    ReturnedChequeCaseStatus.Draft,
                    ReturnedChequeCaseStatus.Submitted,
                    ReturnedChequeCaseStatus.Approved,
                    ReturnedChequeCaseStatus.Rejected);
                StampWorkflowAudit(returnedCheque, outcome, userId, rejectionReason);
                return;
            case OpeningBalanceBatch openingBalance:
                SetTextStatus(openingBalance, "Status", outcome, "Draft", "PendingApproval", "Approved", "Rejected");
                StampWorkflowAudit(openingBalance, outcome, userId, rejectionReason);
                return;
            case FixedAsset fixedAsset:
                SetScalarStatus(fixedAsset, "Status", outcome, FixedAssetStatus.Draft, FixedAssetStatus.PendingApproval, FixedAssetStatus.Acquired, FixedAssetStatus.Rejected);
                StampWorkflowAudit(fixedAsset, outcome, userId, rejectionReason);
                return;
            case FixedAssetDepreciationRun depreciationRun:
                SetTextStatus(depreciationRun, "Status", outcome, "Created", "PendingApproval", "Approved", "Rejected");
                StampWorkflowAudit(depreciationRun, outcome, userId, rejectionReason);
                return;
            case AssetDepreciationSchedule depreciationSchedule:
                SetTextStatus(depreciationSchedule, "ApprovalStatus", outcome, "Draft", "PendingApproval", "Approved", "Rejected");
                StampWorkflowAudit(depreciationSchedule, outcome, userId, rejectionReason);
                return;
            case AssetValuation valuation:
                SetTextStatus(valuation, "Status", outcome, "Calculated", "PendingApproval", "Approved", "Rejected");
                StampWorkflowAudit(valuation, outcome, userId, rejectionReason);
                return;
            case AssetTransfer transfer:
                SetScalarStatus(transfer, "Status", outcome, AssetTransferStatus.Draft, AssetTransferStatus.PendingApproval, AssetTransferStatus.Approved, AssetTransferStatus.Rejected);
                StampWorkflowAudit(transfer, outcome, userId, rejectionReason);
                return;
            case AssetDisposal disposal:
                SetScalarStatus(disposal, "Status", outcome, AssetDisposalStatus.Draft, AssetDisposalStatus.PendingApproval, AssetDisposalStatus.Approved, AssetDisposalStatus.Rejected);
                StampWorkflowAudit(disposal, outcome, userId, rejectionReason);
                return;
            case AssetVerificationSession verification:
                SetScalarStatus(verification, "Status", outcome, VerificationSessionStatus.Draft, VerificationSessionStatus.PendingApproval, VerificationSessionStatus.Approved, VerificationSessionStatus.Rejected);
                StampWorkflowAudit(verification, outcome, userId, rejectionReason);
                return;
            case CapitalProject capitalProject:
                SetScalarStatus(capitalProject, "Status", outcome, ProjectStatus.Planning, ProjectStatus.PendingApproval, ProjectStatus.Approved, ProjectStatus.Rejected);
                StampWorkflowAudit(capitalProject, outcome, userId, rejectionReason);
                return;
            case LeaseContract lease:
                SetScalarStatus(lease, "Status", outcome, LeaseStatus.Draft, LeaseStatus.PendingApproval, LeaseStatus.Active, LeaseStatus.Rejected);
                StampWorkflowAudit(lease, outcome, userId, rejectionReason);
                return;
            default:
                throw new InvalidOperationException($"Expected a Finance workflow entity, got '{entity.GetType().Name}'.");
        }
    }

    private static void ApplyJournalEntry(JournalEntry journal, WorkflowOutcome outcome, Guid? userId, string? rejectionReason)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                journal.PostingStatus = "Approved";
                journal.ApprovalStatus = "Approved";
                journal.ApprovedByUserId = userId;
                journal.ApprovedDate = DateTime.UtcNow;
                journal.RejectionReason = null;
                break;
            case WorkflowOutcome.Rejected:
                journal.PostingStatus = "Rejected";
                journal.ApprovalStatus = "Rejected";
                journal.ApprovedByUserId = null;
                journal.ApprovedDate = null;
                journal.RejectionReason = rejectionReason;
                break;
            case WorkflowOutcome.Recalled:
                journal.PostingStatus = "Draft";
                journal.ApprovalStatus = "Draft";
                journal.ApprovedByUserId = null;
                journal.ApprovedDate = null;
                journal.RejectionReason = rejectionReason;
                break;
            default:
                journal.PostingStatus = "Pending Approval";
                journal.ApprovalStatus = "Pending";
                journal.ApprovedByUserId = null;
                journal.ApprovedDate = null;
                journal.RejectionReason = null;
                break;
        }

        StampWorkflowAudit(journal, outcome, userId, rejectionReason);
    }

    private static void ApplyJournalBatch(JournalBatch batch, WorkflowOutcome outcome, Guid? userId, string? rejectionReason)
    {
        batch.ApprovalStatus = outcome switch
        {
            WorkflowOutcome.Approved => JournalBatchApprovalStatus.Approved,
            WorkflowOutcome.Rejected => JournalBatchApprovalStatus.Rejected,
            WorkflowOutcome.Recalled => JournalBatchApprovalStatus.Draft,
            _ => JournalBatchApprovalStatus.PendingApproval
        };

        if (outcome == WorkflowOutcome.Approved)
        {
            batch.ApprovedAt = DateTime.UtcNow;
            batch.ApprovedByUserId = userId;
        }
        else
        {
            batch.ApprovedAt = null;
            batch.ApprovedByUserId = null;
        }

        if (outcome == WorkflowOutcome.Recalled)
        {
            batch.WorkflowInstanceId = null;
            batch.SubmittedAt = null;
            batch.SubmittedByUserId = null;
        }

        StampWorkflowAudit(batch, outcome, userId, rejectionReason);
    }

    private static void SetTextStatus(object entity, string propertyName, WorkflowOutcome outcome, string draft, string pending, string approved, string rejected)
    {
        var status = outcome switch
        {
            WorkflowOutcome.Approved => approved,
            WorkflowOutcome.Rejected => rejected,
            WorkflowOutcome.Recalled => draft,
            _ => pending
        };

        SetProperty(entity, propertyName, status);
    }

    private static void SetScalarStatus<TStatus>(object entity, string propertyName, WorkflowOutcome outcome, TStatus draft, TStatus pending, TStatus approved, TStatus rejected)
    {
        var status = outcome switch
        {
            WorkflowOutcome.Approved => approved,
            WorkflowOutcome.Rejected => rejected,
            WorkflowOutcome.Recalled => draft,
            _ => pending
        };

        SetProperty(entity, propertyName, status);
    }

    private static void StampWorkflowAudit(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason)
    {
        var now = DateTime.UtcNow;
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                SetPropertyIfPresent(entity, "ApprovedAt", now);
                SetPropertyIfPresent(entity, "ApprovedDate", now);
                SetPropertyIfPresent(entity, "ApprovedById", userId);
                SetPropertyIfPresent(entity, "ApprovedBy", userId);
                SetPropertyIfPresent(entity, "ApprovedByUserId", userId);
                SetPropertyIfPresent(entity, "RejectedAt", null);
                SetPropertyIfPresent(entity, "RejectedById", null);
                SetPropertyIfPresent(entity, "RejectionReason", null);
                break;
            case WorkflowOutcome.Rejected:
                SetPropertyIfPresent(entity, "RejectedAt", now);
                SetPropertyIfPresent(entity, "RejectedById", userId);
                AppendReason(entity, rejectionReason);
                break;
            case WorkflowOutcome.Recalled:
                SetPropertyIfPresent(entity, "ApprovedAt", null);
                SetPropertyIfPresent(entity, "ApprovedDate", null);
                SetPropertyIfPresent(entity, "ApprovedById", null);
                SetPropertyIfPresent(entity, "ApprovedBy", null);
                SetPropertyIfPresent(entity, "ApprovedByUserId", null);
                SetPropertyIfPresent(entity, "RejectedAt", null);
                SetPropertyIfPresent(entity, "RejectedById", null);
                AppendReason(entity, rejectionReason);
                break;
            default:
                SetPropertyIfPresent(entity, "SubmittedAt", now);
                SetPropertyIfPresent(entity, "SubmittedDate", now);
                SetPropertyIfPresent(entity, "SubmittedById", userId);
                SetPropertyIfPresent(entity, "ApprovedAt", null);
                SetPropertyIfPresent(entity, "ApprovedDate", null);
                SetPropertyIfPresent(entity, "ApprovedById", null);
                SetPropertyIfPresent(entity, "ApprovedBy", null);
                SetPropertyIfPresent(entity, "ApprovedByUserId", null);
                SetPropertyIfPresent(entity, "RejectedAt", null);
                SetPropertyIfPresent(entity, "RejectedById", null);
                SetPropertyIfPresent(entity, "RejectionReason", null);
                break;
        }

        SetPropertyIfPresent(entity, "UpdatedAt", now);
        SetPropertyIfPresent(entity, "LastModifiedById", userId);
    }

    private static void AppendReason(object entity, string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return;
        }

        foreach (var propertyName in new[] { "RejectionReason", "FailureReason", "Comments", "Notes", "ApprovalComments" })
        {
            var property = entity.GetType().GetProperty(propertyName);
            if (property == null || !property.CanWrite || property.PropertyType != typeof(string))
            {
                continue;
            }

            var current = property.GetValue(entity) as string;
            property.SetValue(entity, string.IsNullOrWhiteSpace(current)
                ? reason.Trim()
                : $"{current}{Environment.NewLine}{reason.Trim()}");
            return;
        }
    }

    private static void SetPropertyIfPresent(object entity, string propertyName, object? value)
    {
        var property = entity.GetType().GetProperty(propertyName);
        if (property == null || !property.CanWrite)
        {
            return;
        }

        SetProperty(entity, propertyName, value);
    }

    private static void SetProperty(object entity, string propertyName, object? value)
    {
        var property = entity.GetType().GetProperty(propertyName)
            ?? throw new InvalidOperationException($"Entity '{entity.GetType().Name}' does not expose workflow property '{propertyName}'.");

        if (!property.CanWrite)
        {
            throw new InvalidOperationException($"Entity '{entity.GetType().Name}' workflow property '{propertyName}' is not writable.");
        }

        if (value == null)
        {
            if (Nullable.GetUnderlyingType(property.PropertyType) != null || !property.PropertyType.IsValueType)
            {
                property.SetValue(entity, null);
            }

            return;
        }

        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (targetType == value.GetType())
        {
            property.SetValue(entity, value);
            return;
        }

        if (targetType.IsEnum)
        {
            property.SetValue(entity, value is string text
                ? Enum.Parse(targetType, text, ignoreCase: true)
                : Enum.ToObject(targetType, value));
            return;
        }

        property.SetValue(entity, Convert.ChangeType(value, targetType));
    }
}
