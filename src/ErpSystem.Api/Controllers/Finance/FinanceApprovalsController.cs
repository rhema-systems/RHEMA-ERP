using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Finance;

[Authorize]
[ApiController]
[Route("api/finance/approvals")]
public class FinanceApprovalsController : ControllerBase
{
    private static readonly HashSet<string> FinanceWorkflowEntityKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        Normalize("JournalEntry"),
        Normalize("FinancePurchaseOrder"),
        Normalize("FinancePurchaseOrderReceipt"),
        Normalize("VendorInvoice"),
        Normalize("VendorPayment"),
        Normalize("PaymentBatch"),
        Normalize("PurchaseReturn"),
        Normalize("Quote"),
        Normalize("SalesOrder"),
        Normalize("DeliveryNote"),
        Normalize("Invoice"),
        Normalize("ReturnOrder"),
        Normalize("CreditNote"),
        Normalize("CustomerPayment"),
        Normalize("Refund"),
        Normalize("BudgetScenario"),
        Normalize("BudgetReturn"),
        Normalize("UnitJournalEntry"),
        Normalize("UnitAccountBudget"),
        Normalize("AllocationRule"),
        Normalize("CashTransaction"),
        Normalize("BankReconciliation"),
        Normalize("Cheque"),
        Normalize("FixedAsset"),
        Normalize("AssetDepreciationSchedule"),
        Normalize("AssetValuation"),
        Normalize("AssetTransfer"),
        Normalize("AssetDisposal"),
        Normalize("AssetVerificationSession"),
        Normalize("CapitalProject"),
        Normalize("LeaseContract")
    };

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWorkflowService _workflowService;
    private readonly IWorkflowEntityDisplayService _displayService;
    private readonly IJournalEntryService _journalEntryService;
    private readonly IInvoiceService _invoiceService;
    private readonly ISubledgerPostingService _subledgerPostingService;
    private readonly ErpSystem.Core.Interfaces.Inventory.IInventoryValuationService _inventoryValuationService;
    private readonly ILogger<FinanceApprovalsController> _logger;

    public FinanceApprovalsController(
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        IWorkflowService workflowService,
        IWorkflowEntityDisplayService displayService,
        IJournalEntryService journalEntryService,
        IInvoiceService invoiceService,
        ISubledgerPostingService subledgerPostingService,
        ErpSystem.Core.Interfaces.Inventory.IInventoryValuationService inventoryValuationService,
        ILogger<FinanceApprovalsController> logger)
    {
        _db = db;
        _currentUserService = currentUserService;
        _workflowService = workflowService;
        _displayService = displayService;
        _journalEntryService = journalEntryService;
        _invoiceService = invoiceService;
        _subledgerPostingService = subledgerPostingService;
        _inventoryValuationService = inventoryValuationService;
        _logger = logger;
    }

    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<FinanceApprovalQueueItemDto>>> GetPending(CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var roleSet = new HashSet<string>(_currentUserService.Roles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        var approvals = await QueryPendingApprovals(tenantId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var results = new List<FinanceApprovalQueueItemDto>();
        foreach (var approval in approvals.Where(a => CanActOnApproval(a, currentUserId.Value, roleSet)))
        {
            var instance = approval.StepInstance.WorkflowInstance;
            var entityType = instance.EntityType.Code ?? instance.EntityType.Name;
            if (!IsFinanceEntity(entityType))
            {
                continue;
            }

            results.Add(await MapApprovalAsync(approval, cancellationToken));
        }

        return Ok(results
            .OrderBy(item => item.SubmittedAt ?? DateTime.MaxValue)
            .ThenBy(item => item.Reference)
            .ToList());
    }

    [HttpPost("{approvalId:guid}/approve")]
    public async Task<ActionResult<WorkflowExecutionResult>> Approve(Guid approvalId, [FromBody] FinanceApprovalActionRequest? request, CancellationToken cancellationToken)
        => await ProcessApprovalAsync(approvalId, "Approve", request?.Comments, cancellationToken);

    [HttpPost("{approvalId:guid}/reject")]
    public async Task<ActionResult<WorkflowExecutionResult>> Reject(Guid approvalId, [FromBody] FinanceApprovalActionRequest? request, CancellationToken cancellationToken)
    {
        var reason = request?.Reason ?? request?.Comments;
        if (string.IsNullOrWhiteSpace(reason))
        {
            return BadRequest("A rejection reason is required.");
        }

        return await ProcessApprovalAsync(approvalId, "Reject", reason, cancellationToken);
    }

    private async Task<ActionResult<WorkflowExecutionResult>> ProcessApprovalAsync(
        Guid approvalId,
        string action,
        string? comments,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return Unauthorized();
        }

        var tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var approval = await QueryPendingApprovals(tenantId)
            .FirstOrDefaultAsync(a => a.Id == approvalId, cancellationToken);
        if (approval == null)
        {
            return NotFound();
        }

        var roleSet = new HashSet<string>(_currentUserService.Roles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        if (!CanActOnApproval(approval, currentUserId.Value, roleSet))
        {
            return Forbid();
        }

        var instance = approval.StepInstance.WorkflowInstance;
        var entityType = instance.EntityType.Code ?? instance.EntityType.Name;
        if (!IsFinanceEntity(entityType))
        {
            return BadRequest("This approval is not a finance workflow approval.");
        }

        var workflowResult = await _workflowService.ProcessApprovalStepAsync(
            entityType,
            instance.EntityId,
            currentUserId.Value,
            action,
            comments);

        if (!workflowResult.Success)
        {
            return BadRequest(workflowResult);
        }

        if (workflowResult.Status == WorkflowInstanceStatus.Completed)
        {
            await ApplyApprovedOutcomeAsync(entityType, instance.EntityId, currentUserId.Value, comments, cancellationToken);
        }
        else if (workflowResult.Status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed)
        {
            await ApplyRejectedOutcomeAsync(entityType, instance.EntityId, comments, cancellationToken);
        }

        return Ok(workflowResult);
    }

    private IQueryable<WorkflowApproval> QueryPendingApprovals(Guid tenantId)
        => _db.WorkflowApprovals
            .Include(a => a.StepInstance)
                .ThenInclude(si => si.WorkflowStep)
            .Include(a => a.StepInstance)
                .ThenInclude(si => si.WorkflowInstance)
                    .ThenInclude(wi => wi.EntityType)
            .Include(a => a.StepInstance)
                .ThenInclude(si => si.WorkflowInstance)
                    .ThenInclude(wi => wi.WorkflowDefinition)
            .Include(a => a.StepInstance)
                .ThenInclude(si => si.WorkflowInstance)
                    .ThenInclude(wi => wi.InitiatedBy)
            .Where(a =>
                a.TenantId == tenantId &&
                a.Status == WorkflowApprovalStatus.Pending &&
                !a.IsDeleted &&
                !a.StepInstance.IsDeleted &&
                !a.StepInstance.WorkflowInstance.IsDeleted);

    private async Task<FinanceApprovalQueueItemDto> MapApprovalAsync(WorkflowApproval approval, CancellationToken cancellationToken)
    {
        var instance = approval.StepInstance.WorkflowInstance;
        var entityType = instance.EntityType.Code ?? instance.EntityType.Name;
        var display = await _displayService.GetEntityDisplayInfoAsync(entityType, instance.EntityId);
        var facts = await ResolveFactsAsync(entityType, instance.EntityId, cancellationToken);
        var reference = FirstNonEmpty(display.EntityNumber, facts.Reference, instance.EntityId.ToString("N")[..8].ToUpperInvariant());
        var title = FirstNonEmpty(display.EntityName, facts.Title, display.EntityType, entityType);

        return new FinanceApprovalQueueItemDto
        {
            ApprovalId = approval.Id,
            EntityId = instance.EntityId,
            EntityType = display.EntityType,
            Reference = reference,
            Title = title,
            DetailHref = display.ActionUrl ?? "/finance/approvals",
            DocumentType = GetDocumentType(entityType),
            Module = GetModule(entityType),
            CurrentStep = approval.StepInstance.WorkflowStep?.Name ?? "Approval",
            StatusLabel = facts.StatusLabel ?? "Pending Approval",
            SubmittedAt = instance.StartedDate ?? instance.CreatedDate,
            Amount = facts.Amount,
            CurrencyCode = facts.CurrencyCode,
            SubmittedBy = instance.InitiatedBy == null
                ? null
                : string.Join(" ", new[] { instance.InitiatedBy.FirstName, instance.InitiatedBy.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))),
            ApproverRole = approval.ApproverRole,
            WorkflowName = instance.WorkflowDefinition?.Name,
            Metadata = facts.Metadata
        };
    }

    private async Task<FinanceApprovalFacts> ResolveFactsAsync(string entityType, Guid entityId, CancellationToken cancellationToken)
    {
        var key = Normalize(entityType);

        if (key == Normalize("JournalEntry"))
        {
            var item = await _db.JournalEntries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.JournalEntryNumber, item.Description, item.PostingStatus, item.EntryDate, item.TotalDebitAmount, item.PrimaryCurrency ?? "GHS");
        }

        if (key == Normalize("FinancePurchaseOrder"))
        {
            var item = await _db.FinancePurchaseOrders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.OrderNumber, null, MapFinancePurchaseOrderStatus(item.Status), item.OrderDate, item.TotalAmount, item.CurrencyCode);
        }

        if (key == Normalize("VendorInvoice"))
        {
            var item = await _db.VendorInvoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.InvoiceNumber, item.SupplierName, item.Status.ToString(), item.InvoiceDate, item.TotalAmount, item.CurrencyCode);
        }

        if (key == Normalize("Invoice"))
        {
            var item = await _db.Invoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.InvoiceNumber, item.CustomerName, item.Status.ToString(), item.InvoiceDate, item.TotalAmount, item.CurrencyCode);
        }

        if (key == Normalize("VendorPayment"))
        {
            var item = await _db.Set<VendorPayment>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.PaymentNumber, null, item.Status.ToString(), item.PaymentDate, item.TotalAmount, item.CurrencyCode);
        }

        if (key == Normalize("PaymentBatch"))
        {
            var item = await _db.Set<PaymentBatch>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.BatchNumber, item.Description, item.Status.ToString(), item.BatchDate, item.TotalAmount, null);
        }

        if (key == Normalize("CustomerPayment"))
        {
            var item = await _db.Set<CustomerPayment>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.PaymentNumber, null, item.Status, item.PaymentDate, item.TotalAmount, item.CurrencyCode);
        }

        if (key == Normalize("BudgetReturn"))
        {
            var item = await _db.BudgetReturns.AsNoTracking().Include(x => x.BudgetScenario).FirstOrDefaultAsync(x => x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.BudgetScenario?.Name, item.Notes, item.Status, item.SubmittedDate, null, null);
        }

        if (key == Normalize("UnitJournalEntry"))
        {
            var item = await _db.Set<UnitJournalEntry>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.EntryNumber, item.Description, item.Status.ToString(), item.EntryDate, null, null);
        }

        if (key == Normalize("CashTransaction"))
        {
            var item = await _db.Set<CashTransaction>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.TransactionNumber, item.Description ?? item.PayeeOrPayer, item.IsPosted ? "Posted" : "Pending Approval", item.TransactionDate, item.Amount, item.Currency);
        }

        if (key == Normalize("BankReconciliation"))
        {
            var item = await _db.Set<BankReconciliation>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new($"REC-{item.ReconciliationDate:yyyyMMdd}", null, item.Status.ToString(), item.ReconciliationDate, item.StatementBalance, null);
        }

        if (key == Normalize("FixedAsset"))
        {
            var item = await _db.FixedAssets.AsNoTracking().FirstOrDefaultAsync(x => x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.AssetCode, item.Name, item.Status.ToString(), item.PurchaseDate, item.AcquisitionCost, null);
        }

        if (key == Normalize("AssetTransfer"))
        {
            var item = await _db.AssetTransfers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.ReferenceNumber, item.ToLocation, item.Status.ToString(), item.TransferDate, item.TransferCost, null);
        }

        if (key == Normalize("AssetDisposal"))
        {
            var item = await _db.AssetDisposals.AsNoTracking().FirstOrDefaultAsync(x => x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.ReferenceNumber, item.Reason, item.Status.ToString(), item.DisposalDate, item.SaleProceeds, null);
        }

        if (key == Normalize("CapitalProject"))
        {
            var item = await _db.CapitalProjects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.ProjectCode, item.Name, item.Status.ToString(), item.StartDate, item.TotalBudgetAmount, null);
        }

        if (key == Normalize("LeaseContract"))
        {
            var item = await _db.LeaseContracts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == entityId, cancellationToken);
            return item == null ? FinanceApprovalFacts.Empty : new(item.ContractNumber, item.Description, item.Status.ToString(), item.StartDate, item.PresentValue, null);
        }

        return FinanceApprovalFacts.Empty;
    }

    private async Task ApplyApprovedOutcomeAsync(string entityType, Guid entityId, Guid userId, string? comments, CancellationToken cancellationToken)
    {
        var key = Normalize(entityType);
        var now = DateTime.UtcNow;

        if (key == Normalize("JournalEntry"))
        {
            await _journalEntryService.UpdateApprovalStatusAsync(entityId, "Approved", "Approved", userId, cancellationToken: cancellationToken);
            return;
        }

        if (key == Normalize("Invoice"))
        {
            var invoice = await _db.Invoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == entityId, cancellationToken);
            if (invoice?.Status == InvoiceStatus.Draft)
            {
                await _invoiceService.SendInvoiceAsync(entityId, cancellationToken);
            }
            return;
        }

        if (key == Normalize("VendorInvoice"))
        {
            await FinalizeVendorInvoiceApprovalAsync(entityId, userId, comments, cancellationToken);
            return;
        }

        if (key == Normalize("FinancePurchaseOrder"))
        {
            await UpdateIfFoundAsync(_db.FinancePurchaseOrders, entityId, item => item.Status = 2, cancellationToken);
            return;
        }

        if (key == Normalize("VendorPayment"))
        {
            await UpdateIfFoundAsync(_db.Set<VendorPayment>(), entityId, item =>
            {
                item.Status = VendorPaymentStatus.Authorized;
                item.AuthorizedById = userId;
                item.AuthorizedDate = now;
            }, cancellationToken);
            return;
        }

        if (key == Normalize("PaymentBatch"))
        {
            await UpdateIfFoundAsync(_db.Set<PaymentBatch>(), entityId, item =>
            {
                item.Status = PaymentBatchStatus.Approved;
                item.ApprovedById = userId;
                item.ApprovedDate = now;
            }, cancellationToken);
            return;
        }

        if (key == Normalize("UnitJournalEntry"))
        {
            await UpdateIfFoundAsync(_db.Set<UnitJournalEntry>(), entityId, item =>
            {
                item.Status = UnitJournalEntryStatus.Approved;
                item.ApprovedAt = now;
                item.ApprovedBy = userId;
                item.ApprovedByName = _currentUserService.UserName;
            }, cancellationToken);
            return;
        }

        if (key == Normalize("BudgetReturn"))
        {
            await UpdateIfFoundAsync(_db.BudgetReturns, entityId, item =>
            {
                item.Status = "Approved";
                item.ApprovedDate = now;
            }, cancellationToken);
            return;
        }

        if (key == Normalize("BudgetScenario"))
        {
            await UpdateIfFoundAsync(_db.BudgetScenarios, entityId, item =>
            {
                item.Status = "Locked";
                item.LockedDate = now;
                item.LockedByUserId = userId;
            }, cancellationToken);
            return;
        }

        if (key == Normalize("SalesOrder"))
        {
            await UpdateIfFoundAsync(_db.SalesOrders, entityId, item => item.OrderStatus = SalesOrderStatus.Confirmed, cancellationToken);
            return;
        }

        if (key == Normalize("Quote"))
        {
            await UpdateIfFoundAsync(_db.Quotes, entityId, item =>
            {
                item.QuoteStatus = "Sent";
                item.SentDate = now;
            }, cancellationToken);
            return;
        }

        if (key == Normalize("ReturnOrder"))
        {
            await UpdateIfFoundAsync(_db.ReturnOrders, entityId, item => item.ReturnStatus = ReturnOrderStatus.Approved, cancellationToken);
            return;
        }

        if (key == Normalize("CreditNote"))
        {
            await UpdateIfFoundAsync(_db.CreditNotes, entityId, item => item.CreditNoteStatus = CreditNoteStatus.Approved, cancellationToken);
            return;
        }

        if (key == Normalize("Refund"))
        {
            await UpdateIfFoundAsync(_db.Refunds, entityId, item => item.RefundStatus = RefundStatus.Approved, cancellationToken);
            return;
        }

        if (key == Normalize("BankReconciliation"))
        {
            await UpdateIfFoundAsync(_db.Set<BankReconciliation>(), entityId, item =>
            {
                item.Status = ReconciliationStatus.Approved;
                item.ApprovedBy = userId;
                item.ApprovedAt = now;
            }, cancellationToken);
            return;
        }

        if (key == Normalize("AssetTransfer"))
        {
            await UpdateIfFoundAsync(_db.AssetTransfers, entityId, item =>
            {
                item.Status = AssetTransferStatus.Approved;
                item.ApprovedAt = now;
                item.Comments = comments ?? item.Comments;
            }, cancellationToken);
            return;
        }

        if (key == Normalize("AssetDisposal"))
        {
            await UpdateIfFoundAsync(_db.AssetDisposals, entityId, item =>
            {
                item.Status = AssetDisposalStatus.Approved;
                item.ApprovedAt = now;
                item.Comments = comments ?? item.Comments;
            }, cancellationToken);
        }
    }

    private async Task ApplyRejectedOutcomeAsync(string entityType, Guid entityId, string? reason, CancellationToken cancellationToken)
    {
        var key = Normalize(entityType);

        if (key == Normalize("JournalEntry"))
        {
            await _journalEntryService.UpdateApprovalStatusAsync(entityId, "Rejected", "Rejected", rejectionReason: reason, cancellationToken: cancellationToken);
            return;
        }

        if (key == Normalize("FinancePurchaseOrder"))
        {
            await UpdateIfFoundAsync(_db.FinancePurchaseOrders, entityId, item =>
            {
                item.Status = 10;
                item.Remarks = AppendReason(item.Remarks, reason);
            }, cancellationToken);
            return;
        }

        if (key == Normalize("VendorInvoice"))
        {
            await UpdateIfFoundAsync(_db.VendorInvoices, entityId, item =>
            {
                item.Status = VendorInvoiceStatus.Rejected;
                item.ApprovalStatus = "Rejected";
                item.ApprovalComments = reason;
            }, cancellationToken);
            return;
        }

        if (key == Normalize("UnitJournalEntry"))
        {
            await UpdateIfFoundAsync(_db.Set<UnitJournalEntry>(), entityId, item =>
            {
                item.Status = UnitJournalEntryStatus.Rejected;
                item.RejectionReason = reason;
            }, cancellationToken);
            return;
        }

        if (key == Normalize("BudgetReturn"))
        {
            await UpdateIfFoundAsync(_db.BudgetReturns, entityId, item =>
            {
                item.Status = "Rejected";
                item.RejectionReason = reason;
            }, cancellationToken);
            return;
        }

        if (key == Normalize("SalesOrder"))
        {
            await UpdateIfFoundAsync(_db.SalesOrders, entityId, item => item.OrderStatus = SalesOrderStatus.Rejected, cancellationToken);
            return;
        }

        if (key == Normalize("Quote"))
        {
            await UpdateIfFoundAsync(_db.Quotes, entityId, item => item.QuoteStatus = "Rejected", cancellationToken);
            return;
        }

        if (key == Normalize("ReturnOrder"))
        {
            await UpdateIfFoundAsync(_db.ReturnOrders, entityId, item => item.ReturnStatus = ReturnOrderStatus.Rejected, cancellationToken);
            return;
        }

        if (key == Normalize("CreditNote"))
        {
            await UpdateIfFoundAsync(_db.CreditNotes, entityId, item =>
            {
                item.CreditNoteStatus = CreditNoteStatus.Voided;
                item.Reason = AppendReason(item.Reason, reason);
            }, cancellationToken);
            return;
        }

        if (key == Normalize("Refund"))
        {
            await UpdateIfFoundAsync(_db.Refunds, entityId, item => item.RefundStatus = RefundStatus.Rejected, cancellationToken);
            return;
        }

        if (key == Normalize("BankReconciliation"))
        {
            await UpdateIfFoundAsync(_db.Set<BankReconciliation>(), entityId, item => item.Status = ReconciliationStatus.Rejected, cancellationToken);
            return;
        }

        if (key == Normalize("AssetTransfer"))
        {
            await UpdateIfFoundAsync(_db.AssetTransfers, entityId, item =>
            {
                item.Status = AssetTransferStatus.Rejected;
                item.Comments = reason ?? item.Comments;
            }, cancellationToken);
            return;
        }

        if (key == Normalize("AssetDisposal"))
        {
            await UpdateIfFoundAsync(_db.AssetDisposals, entityId, item =>
            {
                item.Status = AssetDisposalStatus.Rejected;
                item.Comments = reason ?? item.Comments;
            }, cancellationToken);
        }
    }

    private async Task FinalizeVendorInvoiceApprovalAsync(Guid entityId, Guid userId, string? comments, CancellationToken cancellationToken)
    {
        var invoice = await _db.VendorInvoices
            .Include(i => i.LineItems)
            .FirstOrDefaultAsync(i => i.Id == entityId, cancellationToken);
        if (invoice == null || invoice.Status == VendorInvoiceStatus.Approved)
        {
            return;
        }

        invoice.Status = VendorInvoiceStatus.Approved;
        invoice.ApprovalStatus = "Approved";
        invoice.ApprovedById = userId;
        invoice.ApprovedDate = DateTime.UtcNow;
        invoice.ApprovalComments = comments;
        invoice.UpdatedAt = DateTime.UtcNow;
        invoice.UpdatedBy = _currentUserService.UserName ?? "system";

        foreach (var line in invoice.LineItems.Where(l => l.LineItemType == "Inventory"))
        {
            if (line.InventoryItemId.HasValue && line.WarehouseId.HasValue)
            {
                await _inventoryValuationService.ProcessReceiptAsync(
                    line.InventoryItemId.Value,
                    line.WarehouseId.Value,
                    line.LocationId,
                    line.Quantity,
                    line.UnitPrice,
                    ReferenceType.VendorInvoice,
                    invoice.InvoiceNumber,
                    invoice.Id,
                    line.LotNumber,
                    line.SerialNumber,
                    line.ExpirationDate);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _subledgerPostingService.PostApInvoiceAsync(invoice.Id, cancellationToken);
    }

    private async Task UpdateIfFoundAsync<TEntity>(DbSet<TEntity> set, Guid id, Action<TEntity> apply, CancellationToken cancellationToken)
        where TEntity : class
    {
        var entity = await set.FindAsync(new object[] { id }, cancellationToken);
        if (entity == null)
        {
            return;
        }

        apply(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private Guid? GetCurrentUserId()
        => Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : null;

    private static bool CanActOnApproval(WorkflowApproval approval, Guid currentUserId, HashSet<string> roles)
        => (approval.ApproverId.HasValue && approval.ApproverId.Value == currentUserId)
           || (!string.IsNullOrWhiteSpace(approval.ApproverRole) && roles.Contains(approval.ApproverRole));

    private static bool IsFinanceEntity(string? entityType)
        => FinanceWorkflowEntityKeys.Contains(Normalize(entityType));

    private static string Normalize(string? value)
        => new((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string AppendReason(string? existing, string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return existing ?? string.Empty;
        }

        return string.IsNullOrWhiteSpace(existing)
            ? $"Rejected: {reason.Trim()}"
            : $"{existing}{Environment.NewLine}Rejected: {reason.Trim()}";
    }

    private static string MapFinancePurchaseOrderStatus(int status)
        => status switch
        {
            1 => "Draft",
            2 => "Approved",
            3 => "Partially Received",
            4 => "Received",
            5 => "Partially Invoiced",
            6 => "Invoiced",
            7 => "Closed",
            8 => "Cancelled",
            9 => "Pending Approval",
            10 => "Rejected",
            _ => "Unknown"
        };

    private static string GetModule(string entityType)
    {
        var key = Normalize(entityType);
        if (new[] { "FINANCEPURCHASEORDER", "FINANCEPURCHASEORDERRECEIPT", "VENDORINVOICE", "VENDORPAYMENT", "PAYMENTBATCH", "PURCHASERETURN" }.Contains(key))
        {
            return "Accounts Payable";
        }

        if (new[] { "QUOTE", "SALESORDER", "DELIVERYNOTE", "INVOICE", "RETURNORDER", "CREDITNOTE", "CUSTOMERPAYMENT", "REFUND" }.Contains(key))
        {
            return "Accounts Receivable";
        }

        if (new[] { "BUDGETSCENARIO", "BUDGETRETURN" }.Contains(key))
        {
            return "Budgeting";
        }

        if (new[] { "UNITJOURNALENTRY", "UNITACCOUNTBUDGET", "ALLOCATIONRULE" }.Contains(key))
        {
            return "Unit Accounting";
        }

        if (new[] { "CASHTRANSACTION", "BANKRECONCILIATION", "CHEQUE" }.Contains(key))
        {
            return "Cash Management";
        }

        if (key.StartsWith("ASSET", StringComparison.OrdinalIgnoreCase) || key is "FIXEDASSET" or "CAPITALPROJECT" or "LEASECONTRACT")
        {
            return "Fixed Assets";
        }

        return "General Ledger";
    }

    private static string GetDocumentType(string entityType)
        => Normalize(entityType) switch
        {
            "FINANCEPURCHASEORDER" => "Purchase Order",
            "FINANCEPURCHASEORDERRECEIPT" => "Goods Receipt",
            "VENDORINVOICE" => "Supplier Invoice",
            "VENDORPAYMENT" => "Supplier Payment",
            "PAYMENTBATCH" => "Payment Batch",
            "PURCHASERETURN" => "Supplier Return",
            "QUOTE" => "Quotation",
            "SALESORDER" => "Sales Order",
            "DELIVERYNOTE" => "Delivery",
            "INVOICE" => "Customer Invoice",
            "RETURNORDER" => "Customer Return",
            "CREDITNOTE" => "Credit Note",
            "CUSTOMERPAYMENT" => "Customer Payment",
            "REFUND" => "Refund",
            "BUDGETSCENARIO" => "Budget Scenario",
            "BUDGETRETURN" => "Budget Return",
            "UNITJOURNALENTRY" => "Unit Journal",
            "UNITACCOUNTBUDGET" => "Unit Budget",
            "ALLOCATIONRULE" => "Allocation",
            "CASHTRANSACTION" => "Bank Transaction",
            "BANKRECONCILIATION" => "Bank Reconciliation",
            "CHEQUE" => "Cheque",
            "FIXEDASSET" => "Fixed Asset",
            "ASSETDEPRECIATIONSCHEDULE" => "Depreciation",
            "ASSETVALUATION" => "Asset Valuation",
            "ASSETTRANSFER" => "Asset Transfer",
            "ASSETDISPOSAL" => "Asset Disposal",
            "ASSETVERIFICATIONSESSION" => "Asset Verification",
            "CAPITALPROJECT" => "Capital Project",
            "LEASECONTRACT" => "Lease Contract",
            _ => entityType
        };

    public sealed class FinanceApprovalActionRequest
    {
        public string? Comments { get; set; }
        public string? Reason { get; set; }
    }

    public sealed class FinanceApprovalQueueItemDto
    {
        public Guid ApprovalId { get; set; }
        public Guid EntityId { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string DetailHref { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public string CurrentStep { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public DateTime? SubmittedAt { get; set; }
        public decimal? Amount { get; set; }
        public string? CurrencyCode { get; set; }
        public string? SubmittedBy { get; set; }
        public string? ApproverRole { get; set; }
        public string? WorkflowName { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    private sealed record FinanceApprovalFacts(
        string? Reference,
        string? Title,
        string? StatusLabel,
        DateTime? Date,
        decimal? Amount,
        string? CurrencyCode)
    {
        public static readonly FinanceApprovalFacts Empty = new(null, null, "Pending Approval", null, null, null);
        public Dictionary<string, string> Metadata { get; init; } = new();
    }
}
