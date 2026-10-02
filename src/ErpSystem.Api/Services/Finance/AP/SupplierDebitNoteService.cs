using System.Data;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Services.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AP;

/// <summary>
/// Controlled AP lifecycle for supplier debit notes. The supplier calls the originating document
/// a credit note; TDC calls its buyer-side record a debit note because it debits AP control.
/// </summary>
public sealed partial class SupplierDebitNoteService : ISupplierDebitNoteService, IInventorySupplierReturnFinanceHandoff
{
    private const string WorkflowEntityType = "SupplierDebitNote";
    private const string AuditResource = "Finance.APSupplierDebitNote";
    private const string CreatedEvent = "Finance.APSupplierDebitNote.Created";
    private const string UpdatedEvent = "Finance.APSupplierDebitNote.Updated";
    private const string SubmittedEvent = "Finance.APSupplierDebitNote.SubmittedForApproval";
    private const string ApprovedEvent = "Finance.APSupplierDebitNote.Approved";
    private const string RejectedEvent = "Finance.APSupplierDebitNote.Rejected";
    private const string PostedEvent = "Finance.APSupplierDebitNote.Posted";
    private const string PostingFailedEvent = "Finance.APSupplierDebitNote.PostingFailed";
    private const string CancelledEvent = "Finance.APSupplierDebitNote.Cancelled";
    private const string ReversedEvent = "Finance.APSupplierDebitNote.Reversed";

    private readonly ApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IDocumentNumberingService _numbering;
    private readonly IWorkflowIntegrationService _workflow;
    private readonly IFinancePostingEngine _posting;
    private readonly IFinanceAuditService _audit;
    private readonly ITaxCalculationEngine _taxEngine;
    private readonly ILogger<SupplierDebitNoteService> _logger;
    private readonly IFinanceSourceDimensionService? _sourceDimensions;
    private readonly ErpSystem.Core.Interfaces.Procurement.IProcurementAcceptedSupplyService? _acceptedSupply;

    public SupplierDebitNoteService(
        ApplicationDbContext db,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IDocumentNumberingService numbering,
        IWorkflowIntegrationService workflow,
        IFinancePostingEngine posting,
        IFinanceAuditService audit,
        ITaxCalculationEngine taxEngine,
        ILogger<SupplierDebitNoteService> logger,
        IFinanceSourceDimensionService? sourceDimensions = null,
        ErpSystem.Core.Interfaces.Procurement.IProcurementAcceptedSupplyService? acceptedSupply = null)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _numbering = numbering;
        _workflow = workflow;
        _posting = posting;
        _audit = audit;
        _taxEngine = taxEngine;
        _logger = logger;
        _sourceDimensions = sourceDimensions;
        _acceptedSupply = acceptedSupply;
    }

    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
    private string UserName => _currentUser.UserName ?? "system";
    private Guid CurrentUserId => Guid.TryParse(_currentUser.UserId, out var id) ? id : Guid.Empty;

    public async Task<IReadOnlyList<SupplierDebitNoteDto>> GetAllAsync(
        SupplierDebitNoteQueryDto query,
        CancellationToken cancellationToken = default)
    {
        var notes = BaseQuery(TenantId);
        if (query.InventoryPurchaseReturnId.HasValue)
            notes = notes.Where(item => item.InventoryPurchaseReturnId == query.InventoryPurchaseReturnId.Value);
        if (query.VendorId.HasValue)
            notes = notes.Where(item => item.VendorId == query.VendorId.Value);
        if (query.BusinessPartnerId.HasValue)
            notes = notes.Where(item => item.VendorId == query.BusinessPartnerId.Value);
        if (query.OriginalVendorInvoiceId.HasValue)
            notes = notes.Where(item => item.OriginalVendorInvoiceId == query.OriginalVendorInvoiceId.Value);
        if (query.Status.HasValue)
            notes = notes.Where(item => item.Status == query.Status.Value);
        if (query.FromDate.HasValue)
            notes = notes.Where(item => item.DebitNoteDate >= query.FromDate.Value.Date);
        if (query.ToDate.HasValue)
            notes = notes.Where(item => item.DebitNoteDate < query.ToDate.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            notes = notes.Where(item =>
                item.DebitNoteNumber.Contains(term) ||
                (item.SupplierCreditNoteReference != null && item.SupplierCreditNoteReference.Contains(term)) ||
                item.Vendor.PartnerName.Contains(term));
        }

        var rows = await notes
            .OrderByDescending(item => item.DebitNoteDate)
            .ThenByDescending(item => item.CreatedAt)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var results = new List<SupplierDebitNoteDto>();
        foreach (var row in rows) results.Add(await MapWithApprovalAsync(row));
        return results;
    }

    public async Task<SupplierDebitNoteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var note = await BaseQuery(TenantId)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return note == null ? null : await MapWithApprovalAsync(note);
    }

    public async Task<SupplierDebitNoteDto?> GetByIdAsync(
        Guid id,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken = default)
    {
        EnsureSupplierDebitNoteRoute(producer);
        var note = await BaseQuery(TenantId).AsSplitQuery().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (note is null) return null;
        var result = await MapWithApprovalAsync(note);
        if (_sourceDimensions is not null)
            result.FinanceDimensions = await _sourceDimensions.GetAsync(
                producer, note.Id, note.DebitNoteDate, DimensionLineContexts(note), cancellationToken);
        return result;
    }

    public Task<SupplierDebitNoteDto> CreateAsync(
        CreateSupplierDebitNoteDto dto,
        CancellationToken cancellationToken = default) =>
        CreateRouteAsync(dto, null, cancellationToken);

    public Task<SupplierDebitNoteDto> CreateAsync(
        CreateSupplierDebitNoteDto dto,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken = default) =>
        CreateRouteAsync(dto, EnsureSupplierDebitNoteRoute(producer), cancellationToken);

    private async Task<SupplierDebitNoteDto> CreateRouteAsync(
        CreateSupplierDebitNoteDto dto,
        FinancePostingProducerContext? producer,
        CancellationToken cancellationToken)
    {
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                if (dto.OriginalVendorInvoiceId.HasValue)
                    await _unitOfWork.AcquireTransactionLockAsync(
                        ApSettlementLockKeys.Invoice(TenantId, dto.OriginalVendorInvoiceId.Value), cancellationToken);
                var created = await CreateCoreAsync(dto, producer, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
                return created;
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction)
                    await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
    }

    private async Task<SupplierDebitNoteDto> CreateCoreAsync(
        CreateSupplierDebitNoteDto dto,
        FinancePostingProducerContext? producer,
        CancellationToken cancellationToken,
        Guid? inventoryReturnId = null,
        Guid? inventoryAccountingGroupId = null)
    {
        var tenantId = TenantId;
        var vendor = await GetVendorAsync(dto.VendorId, cancellationToken);
        var apPartner = await ResolveCanonicalApPartnerAsync(
            vendor, dto.BusinessPartnerRoleId, dto.DebitNoteDate, cancellationToken);
        var invoice = await ValidateInvoiceAsync(dto.OriginalVendorInvoiceId, vendor.Id, cancellationToken);
        await ValidateSupplierReferenceUniqueAsync(
            vendor.Id,
            dto.SupplierCreditNoteReference,
            excludedId: null,
            cancellationToken);

        var debitNoteDate = dto.DebitNoteDate == default ? DateTime.UtcNow.Date : dto.DebitNoteDate.Date;
        var currency = NormalizeCurrency(dto.CurrencyCode, invoice?.CurrencyCode ?? "GHS");
        if (invoice != null && !string.Equals(currency, NormalizeCurrency(invoice.CurrencyCode, currency), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A supplier debit note linked to an invoice must use the invoice currency.");
        var exchangeRate = await ResolveDebitNoteExchangeRateAsync(
            currency, debitNoteDate, dto.ExchangeRate, invoice, cancellationToken);

        var note = new SupplierDebitNote
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InventoryPurchaseReturnId = inventoryReturnId,
            InventorySupplierReturnAccountingGroupId = inventoryAccountingGroupId,
            DebitNoteNumber = await _numbering.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.APSupplierDebitNote,
                tenantId,
                debitNoteDate,
                nameof(SupplierDebitNote),
                cancellationToken: cancellationToken),
            SupplierCreditNoteReference = TrimToNull(dto.SupplierCreditNoteReference),
            VendorId = vendor.Id,
            BusinessPartnerRoleId = apPartner.Role.Id,
            BusinessPartnerApProfileVersionId = apPartner.Profile.Id,
            BusinessPartnerCode = vendor.PartnerCode,
            BusinessPartnerName = vendor.PartnerName,
            BusinessPartnerLegalName = vendor.LegalName,
            BusinessPartnerTaxIdentificationNumber = vendor.TaxIdentificationNumber,
            OriginalVendorInvoiceId = invoice?.Id,
            DebitNoteDate = debitNoteDate,
            Reason = RequiredText(dto.Reason, "Supplier debit note reason"),
            Notes = TrimToNull(dto.Notes),
            CurrencyCode = currency,
            ExchangeRate = exchangeRate,
            Status = SupplierDebitNoteStatus.Draft,
            ApprovalSource = "SupplierDebitNoteWorkflow",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName,
            CreatedById = CurrentUserId == Guid.Empty ? null : CurrentUserId
        };

        await ReplaceLinesAsync(note, dto.Lines, invoice, cancellationToken);
        await ValidateLinkedInvoiceCreditLimitAsync(note, invoice, excludedId: null, cancellationToken);
        _db.SupplierDebitNotes.Add(note);
        await _db.SaveChangesAsync(cancellationToken);
        if (producer is not null)
        {
            if (_sourceDimensions is null)
                throw new InvalidOperationException("Finance source dimensions are not configured for supplier debit notes.");
            await _sourceDimensions.SynchronizeDraftAsync(
                producer, note.Id, note.DebitNoteDate, DimensionLineContexts(note),
                await BuildDebitNoteDimensionInputAsync(note, dto.FinanceDimensions, cancellationToken),
                inheritDefaultForUnassignedLines: true,
                budgetReservationSourceDocumentType: null,
                "Supplier debit note created.", cancellationToken);
        }
        await RecordAuditAsync(CreatedEvent, note, after: Snapshot(note), cancellationToken: cancellationToken);
        return producer is null
            ? await GetRequiredAsync(note.Id, cancellationToken)
            : await GetByIdAsync(note.Id, producer, cancellationToken)
              ?? throw new InvalidOperationException("Supplier debit note could not be reloaded.");
    }

    public Task<SupplierDebitNoteDto> UpdateDraftAsync(
        Guid id,
        UpdateSupplierDebitNoteDto dto,
        CancellationToken cancellationToken = default) =>
        UpdateDraftRouteAsync(id, dto, null, cancellationToken);

    public Task<SupplierDebitNoteDto> UpdateDraftAsync(
        Guid id,
        UpdateSupplierDebitNoteDto dto,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken = default) =>
        UpdateDraftRouteAsync(id, dto, EnsureSupplierDebitNoteRoute(producer), cancellationToken);

    private async Task<SupplierDebitNoteDto> UpdateDraftRouteAsync(
        Guid id,
        UpdateSupplierDebitNoteDto dto,
        FinancePostingProducerContext? producer,
        CancellationToken cancellationToken)
    {
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                await _unitOfWork.AcquireTransactionLockAsync(
                    ApSettlementLockKeys.SupplierDebitNote(TenantId, id), cancellationToken);
                var currentInvoiceId = await _db.SupplierDebitNotes.AsNoTracking()
                    .Where(item => item.TenantId == TenantId && item.Id == id && !item.IsDeleted)
                    .Select(item => item.OriginalVendorInvoiceId)
                    .SingleOrDefaultAsync(cancellationToken);
                foreach (var invoiceId in new[] { currentInvoiceId, dto.OriginalVendorInvoiceId }
                             .Where(item => item.HasValue)
                             .Select(item => item!.Value)
                             .Distinct()
                             .OrderBy(item => item))
                    await _unitOfWork.AcquireTransactionLockAsync(
                        ApSettlementLockKeys.Invoice(TenantId, invoiceId), cancellationToken);
                var updated = await UpdateDraftCoreAsync(id, dto, producer, cancellationToken);
                await _unitOfWork.CommitAsync(cancellationToken);
                return updated;
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction)
                    await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
    }

    private async Task<SupplierDebitNoteDto> UpdateDraftCoreAsync(
        Guid id,
        UpdateSupplierDebitNoteDto dto,
        FinancePostingProducerContext? producer,
        CancellationToken cancellationToken)
    {
        var note = await _db.SupplierDebitNotes
            .Include(item => item.LineItems)
                .ThenInclude(line => line.TaxComponents)
            .FirstOrDefaultAsync(item => item.TenantId == TenantId && item.Id == id && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Supplier debit note was not found for this tenant.");
        if (note.Status is not SupplierDebitNoteStatus.Draft and not SupplierDebitNoteStatus.Rejected)
            throw new InvalidOperationException("Only draft or rejected supplier debit notes can be edited.");

        if (note.InventoryPurchaseReturnId.HasValue)
            throw new InvalidOperationException("RTV_SOURCE_IMMUTABLE: return-credit lines are inherited from the dispatched return and original invoice; they cannot be changed through ordinary debit-note editing.");

        ApplyConcurrencyToken(note, dto.RowVersion);
        var before = Snapshot(note);
        var vendor = await GetVendorAsync(dto.VendorId, cancellationToken);
        var apPartner = await ResolveCanonicalApPartnerAsync(
            vendor, dto.BusinessPartnerRoleId, dto.DebitNoteDate, cancellationToken);
        var invoice = await ValidateInvoiceAsync(dto.OriginalVendorInvoiceId, vendor.Id, cancellationToken);
        await ValidateSupplierReferenceUniqueAsync(vendor.Id, dto.SupplierCreditNoteReference, note.Id, cancellationToken);

        var currency = NormalizeCurrency(dto.CurrencyCode, invoice?.CurrencyCode ?? note.CurrencyCode);
        if (invoice != null && !string.Equals(currency, NormalizeCurrency(invoice.CurrencyCode, currency), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A supplier debit note linked to an invoice must use the invoice currency.");

        note.SupplierCreditNoteReference = TrimToNull(dto.SupplierCreditNoteReference);
        note.VendorId = vendor.Id;
        note.BusinessPartnerRoleId = apPartner.Role.Id;
        note.BusinessPartnerApProfileVersionId = apPartner.Profile.Id;
        note.BusinessPartnerCode = vendor.PartnerCode;
        note.BusinessPartnerName = vendor.PartnerName;
        note.BusinessPartnerLegalName = vendor.LegalName;
        note.BusinessPartnerTaxIdentificationNumber = vendor.TaxIdentificationNumber;
        note.OriginalVendorInvoiceId = invoice?.Id;
        note.DebitNoteDate = dto.DebitNoteDate == default ? note.DebitNoteDate : dto.DebitNoteDate.Date;
        note.Reason = RequiredText(dto.Reason, "Supplier debit note reason");
        note.Notes = TrimToNull(dto.Notes);
        note.CurrencyCode = currency;
        note.ExchangeRate = await ResolveDebitNoteExchangeRateAsync(
            currency, note.DebitNoteDate, dto.ExchangeRate, invoice, cancellationToken);
        note.Status = SupplierDebitNoteStatus.Draft;
        note.RejectionReason = null;
        note.RejectedAt = null;
        note.RejectedById = null;
        note.UpdatedAt = DateTime.UtcNow;
        note.UpdatedBy = UserName;
        note.LastModifiedById = CurrentUserId == Guid.Empty ? null : CurrentUserId;
        await ReplaceLinesAsync(note, dto.Lines, invoice, cancellationToken);
        await ValidateLinkedInvoiceCreditLimitAsync(note, invoice, note.Id, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        if (producer is not null)
        {
            if (_sourceDimensions is null)
                throw new InvalidOperationException("Finance source dimensions are not configured for supplier debit notes.");
            await _sourceDimensions.SynchronizeDraftAsync(
                producer, note.Id, note.DebitNoteDate, DimensionLineContexts(note),
                await BuildDebitNoteDimensionInputAsync(note, dto.FinanceDimensions, cancellationToken),
                inheritDefaultForUnassignedLines: true,
                budgetReservationSourceDocumentType: null,
                "Supplier debit note draft changed.", cancellationToken);
        }
        await RecordAuditAsync(UpdatedEvent, note, before, Snapshot(note), cancellationToken: cancellationToken);
        return producer is null
            ? await GetRequiredAsync(note.Id, cancellationToken)
            : await GetByIdAsync(note.Id, producer, cancellationToken)
              ?? throw new InvalidOperationException("Supplier debit note could not be reloaded.");
    }

    public Task<SupplierDebitNoteDto> SubmitAsync(Guid id, CancellationToken cancellationToken = default) =>
        SubmitRouteAsync(id, null, cancellationToken);

    public Task<SupplierDebitNoteDto> SubmitAsync(
        Guid id,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken = default) =>
        SubmitRouteAsync(id, EnsureSupplierDebitNoteRoute(producer), cancellationToken);

    private async Task<SupplierDebitNoteDto> SubmitRouteAsync(
        Guid id,
        FinancePostingProducerContext? producer,
        CancellationToken cancellationToken)
    {
        return await ExecuteNoteMutationAsync(id, async note =>
        {
            var invoice = await ValidateInvoiceAsync(
                note.OriginalVendorInvoiceId,
                note.VendorId,
                cancellationToken);
            await ValidateLinkedInvoiceCreditLimitAsync(note, invoice, note.Id, cancellationToken);
            return await SubmitCoreAsync(note, producer, cancellationToken);
        }, cancellationToken);
    }

    private async Task<SupplierDebitNoteDto> SubmitCoreAsync(
        SupplierDebitNote note,
        FinancePostingProducerContext? producer,
        CancellationToken cancellationToken)
    {
        if (note.Status != SupplierDebitNoteStatus.Draft)
            throw new InvalidOperationException("Only draft supplier debit notes can be submitted.");
        if (string.IsNullOrWhiteSpace(note.SupplierCreditNoteReference))
            throw new InvalidOperationException("Enter the supplier's credit-note reference before submission.");
        if (note.LineItems.Count == 0 || note.TotalAmount <= 0m)
            throw new InvalidOperationException("Supplier debit note must contain at least one positive-value line.");
        ValidateLineClassificationEvidence(note);
        if (producer is not null)
        {
            if (_sourceDimensions is null)
                throw new InvalidOperationException("Finance source dimensions are not configured for supplier debit notes.");
            await _sourceDimensions.ValidateAndFreezeAsync(
                producer, note.Id, note.DebitNoteDate, DimensionLineContexts(note),
                requireCurrentBudgetEvidence: false, cancellationToken);
        }

        var result = await _workflow.SubmitAsync(WorkflowEntityType, note.Id);
        if (!result.ExecutionResult.Success)
            throw new InvalidOperationException(result.ExecutionResult.Message ?? "Unable to start the supplier debit-note workflow.");

        note.SubmittedById = CurrentUserId == Guid.Empty ? null : CurrentUserId;
        note.SubmittedAt = DateTime.UtcNow;
        note.WorkflowInstanceId = result.ExecutionResult.WorkflowInstanceId;
        note.Status = result.Outcome switch
        {
            WorkflowOutcome.Approved => SupplierDebitNoteStatus.Approved,
            WorkflowOutcome.Rejected => SupplierDebitNoteStatus.Rejected,
            _ => SupplierDebitNoteStatus.PendingApproval
        };
        note.UpdatedAt = DateTime.UtcNow;
        note.UpdatedBy = UserName;
        await _db.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(
            SubmittedEvent,
            note,
            after: new { note.Status, note.WorkflowInstanceId, note.SubmittedById, note.SubmittedAt },
            workflowInstanceId: note.WorkflowInstanceId,
            cancellationToken: cancellationToken);

        if (note.Status == SupplierDebitNoteStatus.Approved)
        {
            // A policy may deliberately contain no human step. Preserve the outcome, but stamp
            // the workflow as the approval source rather than pretending that the maker approved it.
            note.ApprovalSource = result.ApprovalRequired ? "WorkflowAutoApproval" : "NoApprovalWorkflow";
            note.ApprovedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return await GetRequiredAsync(note.Id, producer, cancellationToken);
        }

        return await GetRequiredAsync(note.Id, producer, cancellationToken);
    }

    public Task<SupplierDebitNoteDto> ProcessApprovalAsync(
        Guid id,
        SupplierDebitNoteApprovalDto dto,
        CancellationToken cancellationToken = default) =>
        ProcessApprovalRouteAsync(id, dto, null, cancellationToken);

    public Task<SupplierDebitNoteDto> ProcessApprovalAsync(
        Guid id,
        SupplierDebitNoteApprovalDto dto,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken = default) =>
        ProcessApprovalRouteAsync(id, dto, EnsureSupplierDebitNoteRoute(producer), cancellationToken);

    private async Task<SupplierDebitNoteDto> ProcessApprovalRouteAsync(
        Guid id,
        SupplierDebitNoteApprovalDto dto,
        FinancePostingProducerContext? producer,
        CancellationToken cancellationToken)
    {
        return await ExecuteNoteMutationAsync(id, async note =>
        {
            var invoice = await ValidateInvoiceAsync(
                note.OriginalVendorInvoiceId,
                note.VendorId,
                cancellationToken);
            await ValidateLinkedInvoiceCreditLimitAsync(note, invoice, note.Id, cancellationToken);
            return await ProcessApprovalCoreAsync(note, dto, producer, cancellationToken);
        }, cancellationToken);
    }

    private async Task<SupplierDebitNoteDto> ProcessApprovalCoreAsync(
        SupplierDebitNote note,
        SupplierDebitNoteApprovalDto dto,
        FinancePostingProducerContext? producer,
        CancellationToken cancellationToken)
    {
        if (note.Status != SupplierDebitNoteStatus.PendingApproval)
            throw new InvalidOperationException("Only pending supplier debit notes can be approved or rejected.");
        if (CurrentUserId == Guid.Empty)
            throw new UnauthorizedAccessException("An authenticated application user is required.");
        if (note.SubmittedById == CurrentUserId || note.CreatedById == CurrentUserId)
            throw new InvalidOperationException("Maker-checker control prevents the debit-note maker from approving the same document.");
        if (!dto.Approve && string.IsNullOrWhiteSpace(dto.RejectionReason ?? dto.Comments))
            throw new InvalidOperationException("A rejection reason is required.");
        if (!await _workflow.CanUserApproveAsync(WorkflowEntityType, note.Id, CurrentUserId))
            throw new UnauthorizedAccessException("You are not assigned to the current supplier debit-note approval step.");
        if (dto.Approve && producer is not null)
        {
            if (_sourceDimensions is null)
                throw new InvalidOperationException("Finance source dimensions are not configured for supplier debit notes.");
            await _sourceDimensions.ValidateAndFreezeAsync(
                producer, note.Id, note.DebitNoteDate, DimensionLineContexts(note),
                requireCurrentBudgetEvidence: false, cancellationToken);
        }

        var comments = dto.Approve ? dto.Comments : dto.RejectionReason ?? dto.Comments;
        var result = await _workflow.ProcessApprovalAsync(
            WorkflowEntityType,
            note.Id,
            CurrentUserId,
            dto.Approve ? "Approve" : "Reject",
            comments);
        if (!result.ExecutionResult.Success)
            throw new InvalidOperationException(result.ExecutionResult.Message ?? "Unable to process the supplier debit-note approval.");

        if (result.Outcome == WorkflowOutcome.Rejected || !dto.Approve)
        {
            note.Status = SupplierDebitNoteStatus.Rejected;
            note.RejectedById = CurrentUserId;
            note.RejectedAt = DateTime.UtcNow;
            note.RejectionReason = comments?.Trim();
            note.UpdatedAt = DateTime.UtcNow;
            note.UpdatedBy = UserName;
            await _db.SaveChangesAsync(cancellationToken);
            await RecordAuditAsync(
                RejectedEvent,
                note,
                after: new { note.Status, note.RejectedById, note.RejectedAt, note.RejectionReason },
                reason: note.RejectionReason,
                workflowInstanceId: note.WorkflowInstanceId,
                cancellationToken: cancellationToken);
            return await GetRequiredAsync(note.Id, producer, cancellationToken);
        }

        if (result.Outcome != WorkflowOutcome.Approved)
            return await GetRequiredAsync(note.Id, producer, cancellationToken);

        note.Status = SupplierDebitNoteStatus.Approved;
        note.ApprovedById = CurrentUserId;
        note.ApprovedAt = DateTime.UtcNow;
        note.ApprovalSource = "SupplierDebitNoteWorkflow";
        note.RejectionReason = null;
        note.UpdatedAt = DateTime.UtcNow;
        note.UpdatedBy = UserName;
        await _db.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(
            ApprovedEvent,
            note,
            after: new { note.Status, note.ApprovedById, note.ApprovedAt },
            workflowInstanceId: note.WorkflowInstanceId,
            cancellationToken: cancellationToken);
        // Approval and posting are separate controlled actions. The Finance workflow proves the
        // independent commercial review; a purpose-permissioned poster then creates the journal
        // through the central posting engine. Do not silently borrow the approver's HTTP authority.
        return await GetRequiredAsync(note.Id, producer, cancellationToken);
    }

    public Task<SupplierDebitNoteDto> PostAsync(Guid id, CancellationToken cancellationToken = default) =>
        PostRouteAsync(id, null, cancellationToken);

    public Task<SupplierDebitNoteDto> PostAsync(
        Guid id,
        FinancePostingProducerContext producer,
        CancellationToken cancellationToken = default) =>
        PostRouteAsync(id, EnsureSupplierDebitNoteRoute(producer), cancellationToken);

    private async Task<SupplierDebitNoteDto> PostRouteAsync(
        Guid id,
        FinancePostingProducerContext? producer,
        CancellationToken cancellationToken)
    {
        SupplierDebitNote? failedNote = null;
        try
        {
            return await ExecuteNoteMutationAsync(id, async note =>
            {
                failedNote = note;
                var invoice = await ValidateInvoiceAsync(
                    note.OriginalVendorInvoiceId,
                    note.VendorId,
                    cancellationToken);
                await ValidateLinkedInvoiceCreditLimitAsync(note, invoice, note.Id, cancellationToken);
                return await PostCoreAsync(note, producer, cancellationToken);
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            if (failedNote != null)
            {
                _unitOfWork.ClearTrackedChanges();
                await RecordAuditAsync(
                    PostingFailedEvent,
                    failedNote,
                    after: new { failedNote.Status, error = exception.Message },
                    reason: exception.Message,
                    cancellationToken: cancellationToken);
            }
            _logger.LogError(exception, "Failed to post supplier debit note {DebitNoteId}", id);
            throw;
        }
    }

    private async Task<SupplierDebitNoteDto> PostCoreAsync(
        SupplierDebitNote note,
        FinancePostingProducerContext? producer,
        CancellationToken cancellationToken)
    {
        if (note.Status == SupplierDebitNoteStatus.Posted && note.JournalEntryId.HasValue && note.PostingEventId.HasValue)
            return Map(note);
        if (note.Status != SupplierDebitNoteStatus.Approved)
            throw new InvalidOperationException("Only approved supplier debit notes can be posted.");
        ValidateLineClassificationEvidence(note);
        if (producer is not null)
        {
            if (_sourceDimensions is null)
                throw new InvalidOperationException("Finance source dimensions are not configured for supplier debit notes.");
            await _sourceDimensions.ValidateAndFreezeAsync(
                producer, note.Id, note.DebitNoteDate, DimensionLineContexts(note),
                requireCurrentBudgetEvidence: false, cancellationToken);
        }

        var request = await BuildPostingRequestAsync(note, producer, cancellationToken);
        if (note.InventoryPurchaseReturnId.HasValue)
            await PrepareInventoryReturnPostingAsync(note, request, cancellationToken);
        var result = producer is null
            ? await _posting.PostAsync(request, cancellationToken)
            : await _posting.PostAsync(request, producer, cancellationToken);
        if (note.JournalEntryId.HasValue && note.JournalEntryId != result.JournalEntryId)
            throw new InvalidOperationException("Supplier debit note is already linked to a different journal entry.");

        note.JournalEntryId = result.JournalEntryId;
        note.PostingEventId = result.PostingEventId;
        note.Status = SupplierDebitNoteStatus.Posted;
        if (note.InventoryPurchaseReturnId.HasValue)
            await ApplyInventoryReturnCreditAsync(note, cancellationToken);
        note.UpdatedAt = DateTime.UtcNow;
        note.UpdatedBy = UserName;
        note.LastModifiedById = CurrentUserId == Guid.Empty ? null : CurrentUserId;
        await RecordTaxCalculationSnapshotsAsync(request.TaxCalculationSnapshots, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        await RecordAuditAsync(
            PostedEvent,
            note,
            after: new
            {
                note.Status,
                result.PostingEventId,
                result.JournalEntryId,
                result.JournalEntryNumber,
                result.TotalDebitAmount,
                result.TotalCreditAmount,
                result.WasDuplicate
            },
            postingEventId: result.PostingEventId,
            journalEntryId: result.JournalEntryId,
            cancellationToken: cancellationToken);
        return producer is null
            ? Map(note)
            : await GetRequiredAsync(note.Id, producer, cancellationToken);
    }

    private async Task<SupplierDebitNoteDto> ExecuteNoteMutationAsync(
        Guid id,
        Func<SupplierDebitNote, Task<SupplierDebitNoteDto>> operation,
        CancellationToken cancellationToken)
    {
        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                await _unitOfWork.AcquireTransactionLockAsync(
                    ApSettlementLockKeys.SupplierDebitNote(TenantId, id), cancellationToken);
                var invoiceId = await _db.SupplierDebitNotes.AsNoTracking()
                    .Where(item => item.TenantId == TenantId && item.Id == id && !item.IsDeleted)
                    .Select(item => item.OriginalVendorInvoiceId)
                    .SingleOrDefaultAsync(cancellationToken);
                if (invoiceId.HasValue)
                    await _unitOfWork.AcquireTransactionLockAsync(
                        ApSettlementLockKeys.Invoice(TenantId, invoiceId.Value), cancellationToken);
                var note = await GetTrackedAsync(id, cancellationToken);
                var result = await operation(note);
                await _unitOfWork.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                if (_unitOfWork.HasActiveTransaction)
                    await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
    }

    public async Task<SupplierDebitNoteDto> CancelAsync(
        Guid id,
        string reason,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteNoteMutationAsync(id, note => CancelCoreAsync(note, reason, cancellationToken), cancellationToken);
    }

    private async Task<SupplierDebitNoteDto> CancelCoreAsync(
        SupplierDebitNote note,
        string reason,
        CancellationToken cancellationToken)
    {
        if (note.InventoryPurchaseReturnId.HasValue)
            throw new InvalidOperationException("RTV_CREDIT_HEADER_CORRECTION_REQUIRED: correct the draft credit reference/date instead; cancelling its retained dispatched-return link is not supported.");
        if (note.Status is not SupplierDebitNoteStatus.Draft and not SupplierDebitNoteStatus.Rejected)
            throw new InvalidOperationException("Only draft or rejected supplier debit notes can be cancelled.");
        note.Status = SupplierDebitNoteStatus.Cancelled;
        note.Notes = Append(note.Notes, $"Cancelled: {RequiredText(reason, "Cancellation reason")}");
        note.UpdatedAt = DateTime.UtcNow;
        note.UpdatedBy = UserName;
        await _db.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(CancelledEvent, note, after: new { note.Status }, reason: reason, cancellationToken: cancellationToken);
        return await GetRequiredAsync(note.Id, cancellationToken);
    }

    public async Task<SupplierDebitNoteDto> ReverseAsync(
        Guid id,
        ReverseSupplierDebitNoteDto dto,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteNoteMutationAsync(id, note => ReverseCoreAsync(note, dto, cancellationToken), cancellationToken);
    }

    private async Task<SupplierDebitNoteDto> ReverseCoreAsync(
        SupplierDebitNote note,
        ReverseSupplierDebitNoteDto dto,
        CancellationToken cancellationToken)
    {
        var reason = RequiredText(dto.Reason, "Reversal reason");
        if (note.Status == SupplierDebitNoteStatus.Reversed)
            return Map(note);
        if (note.Status != SupplierDebitNoteStatus.Posted || !note.PostingEventId.HasValue || !note.JournalEntryId.HasValue)
            throw new InvalidOperationException("Only a posted supplier debit note can be reversed.");

        var applied = EffectiveApplications(note.Applications).Sum(item => item.ApplicationAmount) + note.DirectInvoiceAppliedAmount;
        if (Round(applied) > 0m)
            throw new InvalidOperationException("Reverse the linked payment settlement applications before reversing this supplier debit note.");

        var plan = await _posting.GetReversalPlanAsync(note.PostingEventId.Value, reason, dto.ReversalDate, cancellationToken);
        var functionalCurrency = NormalizeCurrency(
            await _db.FinanceSettings.AsNoTracking()
                .Where(item => item.TenantId == TenantId && !item.IsDeleted)
                .Select(item => item.BaseCurrency)
                .FirstOrDefaultAsync(cancellationToken),
            "GHS");
        var result = await _posting.PostAsync(new FinancePostingRequestV2Dto
        {
            SourceModule = "AP",
            SourceDocumentType = "SupplierDebitNoteReversal",
            SourceDocumentId = note.Id,
            SourceDocumentTenantId = note.TenantId,
            PostingAction = plan.PostingAction,
            SourceDocumentReference = $"{note.DebitNoteNumber}-REV",
            Description = $"Reverse supplier debit note {note.DebitNoteNumber}",
            PostingDate = plan.ReversalDate,
            JournalType = "AP Supplier Debit Note Reversal",
            AccountingBookCode = "IFRS",
            FunctionalCurrencyCode = functionalCurrency,
            ReversalOfJournalEntryId = plan.OriginalJournalEntryId,
            ReversalReason = reason,
            ReversalType = "SourceDocument",
            IdempotencyKey = $"AP:SupplierDebitNote:{note.TenantId:N}:{note.Id:N}:Reverse",
            ReturnExistingOnDuplicate = true,
            Lines = plan.ReversalLines.ToList()
        }, cancellationToken);

        note.Status = SupplierDebitNoteStatus.Reversed;
        note.ReversalJournalEntryId = result.JournalEntryId;
        note.ReversalPostingEventId = result.PostingEventId;
        note.ReversalReason = reason;
        note.ReversedAt = DateTime.UtcNow;
        note.ReversedById = CurrentUserId == Guid.Empty ? null : CurrentUserId;
        note.UpdatedAt = DateTime.UtcNow;
        note.UpdatedBy = UserName;
        await _db.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(
            ReversedEvent,
            note,
            after: new { note.Status, note.ReversalJournalEntryId, note.ReversalPostingEventId },
            reason: reason,
            postingEventId: result.PostingEventId,
            journalEntryId: result.JournalEntryId,
            cancellationToken: cancellationToken);
        return await GetRequiredAsync(note.Id, cancellationToken);
    }

    private IQueryable<SupplierDebitNote> BaseQuery(Guid tenantId) => _db.SupplierDebitNotes
        .Include(item => item.Vendor)
        .Include(item => item.BusinessPartnerRole)
        .Include(item => item.BusinessPartnerApProfileVersion)
        .Include(item => item.OriginalVendorInvoice)
            .ThenInclude(invoice => invoice!.LineItems.Where(line => !line.IsDeleted))
        // FIN-INT-012/013 remain quarantined. SupplierReturnId stays as scalar historical
        // evidence, but standalone Finance reads must not join an optional return table that is
        // absent from some legitimate Finance-only databases.
        .Include(item => item.LineItems.Where(line => !line.IsDeleted))
            .ThenInclude(line => line.TaxComponents.Where(component => !component.IsDeleted))
        .Include(item => item.Applications.Where(application => !application.IsDeleted))
            .ThenInclude(application => application.VendorPayment)
        .Include(item => item.Applications.Where(application => !application.IsDeleted))
            .ThenInclude(application => application.VendorInvoice)
        .Where(item => item.TenantId == tenantId && !item.IsDeleted);

    private async Task<SupplierDebitNote> GetTrackedAsync(Guid id, CancellationToken cancellationToken) =>
        await BaseQuery(TenantId)
            .AsSplitQuery()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
        ?? throw new KeyNotFoundException("Supplier debit note was not found for this tenant.");

    private async Task<SupplierDebitNoteDto> GetRequiredAsync(Guid id, CancellationToken cancellationToken) =>
        await GetByIdAsync(id, cancellationToken)
        ?? throw new InvalidOperationException("Supplier debit note could not be reloaded.");

    private async Task<SupplierDebitNoteDto> GetRequiredAsync(
        Guid id,
        FinancePostingProducerContext? producer,
        CancellationToken cancellationToken) =>
        producer is null
            ? await GetRequiredAsync(id, cancellationToken)
            : await GetByIdAsync(id, producer, cancellationToken)
                ?? throw new InvalidOperationException("Supplier debit note could not be reloaded.");

    private static IReadOnlyList<FinanceSourceDocumentLineContext> DimensionLineContexts(
        SupplierDebitNote note) =>
        note.LineItems.Where(line => !line.IsDeleted).Select(line =>
            new FinanceSourceDocumentLineContext(
                line.Id,
                line.ResolvedCreditAccountId
                ?? throw new InvalidOperationException(
                    $"Supplier debit-note line '{line.Description}' has no server-resolved economic account.")))
            .ToArray();

    private async Task<FinanceSourceDocumentDimensionInputDto?> BuildDebitNoteDimensionInputAsync(
        SupplierDebitNote note,
        FinanceSourceDocumentDimensionInputDto? requested,
        CancellationToken cancellationToken)
    {
        if (!note.OriginalVendorInvoiceId.HasValue) return requested;
        if ((requested?.Lines.Count ?? 0) > 0)
            throw new InvalidOperationException(
                "Dimensions on a linked supplier debit note are inherited from the exact posted invoice lines and cannot be overridden.");

        var transactionIds = note.LineItems.Where(line => !line.IsDeleted && line.OriginalAccountTransactionId.HasValue)
            .Select(line => line.OriginalAccountTransactionId!.Value).Distinct().ToArray();
        var transactions = await _db.AccountTransactions.AsNoTracking()
            .Include(item => item.FinanceDimensionSnapshot)!.ThenInclude(snapshot => snapshot!.Items)
            .Include(item => item.FinanceDimensionSet)!.ThenInclude(set => set!.Items)
            .Where(item => item.TenantId == TenantId && transactionIds.Contains(item.Id) && !item.IsDeleted)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        return new FinanceSourceDocumentDimensionInputDto
        {
            DefaultDimensions = requested?.DefaultDimensions ?? Array.Empty<FinancePostingDimensionValueDto>(),
            ApplyDefaultToEligibleLines = false,
            Lines = note.LineItems.Where(line => !line.IsDeleted).Select(line =>
            {
                transactions.TryGetValue(line.OriginalAccountTransactionId ?? Guid.Empty, out var transaction);
                return new FinanceSourceLineDimensionInputDto
                {
                    SourceLineId = line.Id,
                    AccountId = line.ResolvedCreditAccountId
                        ?? throw new InvalidOperationException("Linked supplier debit-note line has no historical account."),
                    Dimensions = InheritedPostingDimensions(transaction)
                };
            }).ToArray()
        };
    }

    private static IReadOnlyList<FinancePostingDimensionValueDto> InheritedPostingDimensions(
        AccountTransaction? transaction)
    {
        if (transaction?.FinanceDimensionSnapshot is { } snapshot)
            return snapshot.Items.OrderBy(item => item.DimensionCodeSnapshot)
                .Select(item => new FinancePostingDimensionValueDto
                {
                    DimensionCode = item.DimensionCodeSnapshot,
                    ValueCode = item.DimensionValueCodeSnapshot
                }).ToArray();
        return transaction?.FinanceDimensionSet?.Items
            .OrderBy(item => item.DimensionCodeSnapshot)
            .Select(item => new FinancePostingDimensionValueDto
            {
                DimensionCode = item.DimensionCodeSnapshot,
                ValueCode = item.DimensionValueCodeSnapshot
            }).ToArray() ?? Array.Empty<FinancePostingDimensionValueDto>();
    }

    private static FinancePostingProducerContext EnsureSupplierDebitNoteRoute(
        FinancePostingProducerContext producer)
    {
        ArgumentNullException.ThrowIfNull(producer);
        if (producer.RouteId != FinanceDimensionRouteId.FinanceApSupplierDebitNote)
            throw new InvalidOperationException("The trusted producer context is not the Finance AP supplier-debit-note route.");
        return producer;
    }

    private async Task<BusinessPartner> GetVendorAsync(Guid vendorId, CancellationToken cancellationToken)
    {
        var vendor = await _db.BusinessPartners.FirstOrDefaultAsync(item =>
            item.TenantId == TenantId &&
            item.Id == vendorId &&
            !item.IsDeleted &&
            item.IsActive &&
            BusinessPartnerRoles.ProcurementTypes.Contains(item.PartnerType),
            cancellationToken);
        return vendor ?? throw new KeyNotFoundException("Active supplier business partner was not found for this tenant.");
    }

    private async Task<VendorInvoice?> ValidateInvoiceAsync(
        Guid? invoiceId,
        Guid supplierId,
        CancellationToken cancellationToken)
    {
        if (!invoiceId.HasValue)
            return null;

        var invoice = await _db.VendorInvoices
            .Include(item => item.BusinessPartner)
            .Include(item => item.LineItems.Where(line => !line.IsDeleted))
            .FirstOrDefaultAsync(item =>
                item.TenantId == TenantId && item.Id == invoiceId && !item.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException("Original supplier invoice was not found for this tenant.");
        if (invoice.BusinessPartnerId != supplierId)
            throw new InvalidOperationException("Original invoice does not belong to the selected supplier.");
        if (!invoice.JournalEntryId.HasValue)
            throw new InvalidOperationException("A supplier debit note can only reference a posted supplier invoice.");
        if (invoice.Status == VendorInvoiceStatus.Voided || await _db.JournalEntries.AsNoTracking().AnyAsync(item =>
                item.TenantId == TenantId &&
                item.Id == invoice.JournalEntryId.Value &&
                !item.IsDeleted &&
                item.IsReversed,
                cancellationToken))
            throw new InvalidOperationException("A supplier debit note cannot reference a voided or reversed supplier invoice.");
        return invoice;
    }

    private async Task ReplaceLinesAsync(
        SupplierDebitNote note,
        IReadOnlyCollection<CreateSupplierDebitNoteLineItemDto> lines,
        VendorInvoice? invoice,
        CancellationToken cancellationToken)
    {
        if (lines.Count == 0)
            throw new InvalidOperationException("At least one supplier debit-note line is required.");

        var existingLines = note.LineItems.Where(line => !line.IsDeleted).ToDictionary(line => line.Id);
        var requestedIds = lines.Where(line => line.Id.HasValue).Select(line => line.Id!.Value).ToArray();
        if (requestedIds.Any(id => id == Guid.Empty) || requestedIds.Distinct().Count() != requestedIds.Length)
            throw new InvalidOperationException("Supplier debit-note line identities are invalid or duplicated.");
        var foreignIds = requestedIds.Where(id => !existingLines.ContainsKey(id)).ToArray();
        if (foreignIds.Length > 0 && await _db.SupplierDebitNoteLineItems.AsNoTracking().AnyAsync(line =>
                line.TenantId == TenantId && foreignIds.Contains(line.Id) && line.SupplierDebitNoteId != note.Id,
                cancellationToken))
            throw new InvalidOperationException("A supplier debit-note line identity belongs to another document.");
        foreach (var existing in existingLines.Values.Where(line => !requestedIds.Contains(line.Id)).ToList())
        {
            if (existing.TaxComponents.Count > 0)
                _db.SupplierDebitNoteTaxComponents.RemoveRange(existing.TaxComponents);
            _db.SupplierDebitNoteLineItems.Remove(existing);
            note.LineItems.Remove(existing);
        }
        foreach (var existing in existingLines.Values.Where(line => requestedIds.Contains(line.Id)))
        {
            if (existing.TaxComponents.Count > 0)
                _db.SupplierDebitNoteTaxComponents.RemoveRange(existing.TaxComponents);
            existing.TaxComponents.Clear();
        }

        note.SubTotal = 0m;
        note.TaxAmount = 0m;
        note.DiscountAmount = 0m;
        var sourceLines = invoice?.LineItems
            .Where(item => !item.IsDeleted)
            .ToDictionary(item => item.Id)
            ?? new Dictionary<Guid, VendorInvoiceLineItem>();
        var requestedSourceIds = new HashSet<Guid>();

        var sourceTransactions = invoice?.JournalEntryId.HasValue == true
            ? await _db.AccountTransactions.AsNoTracking()
                .Where(item =>
                    item.TenantId == TenantId &&
                    item.JournalEntryId == invoice.JournalEntryId.Value &&
                    !item.IsDeleted)
                .ToListAsync(cancellationToken)
            : new List<AccountTransaction>();
        var sourceTaxSnapshots = invoice != null
            ? await _db.Set<TaxCalculation>().AsNoTracking()
                .Where(item =>
                    item.TenantId == TenantId &&
                    item.DocumentType == "VendorInvoice" &&
                    item.DocumentId == invoice.Id &&
                    !item.IsDeleted)
                .ToListAsync(cancellationToken)
            : new List<TaxCalculation>();

        foreach (var dto in lines)
        {
            if (dto.Quantity <= 0m || dto.UnitPrice <= 0m)
                throw new InvalidOperationException("Supplier debit-note quantity and unit price must be positive.");
            var isWriteoff = string.Equals(dto.LineItemType, "Writeoff", StringComparison.OrdinalIgnoreCase);
            if (isWriteoff && (invoice != null || dto.OriginalVendorInvoiceLineItemId.HasValue ||
                dto.TaxGroupId.HasValue || dto.TaxRate != 0m || dto.TaxAmount.GetValueOrDefault() != 0m ||
                dto.DiscountAmount.GetValueOrDefault() != 0m || dto.DiscountPercentage != 0m))
                throw new InvalidOperationException("AP_WRITEOFF_SETTLEMENT_ONLY: a supplier writeoff must be standalone, without invoice source lines, tax or discounts.");
            if (invoice != null)
            {
                if (!dto.OriginalVendorInvoiceLineItemId.HasValue ||
                    !sourceLines.TryGetValue(dto.OriginalVendorInvoiceLineItemId.Value, out var sourceLine))
                    throw new InvalidOperationException($"Debit-note line '{dto.Description}' must reference a line on the original invoice.");
                if (!requestedSourceIds.Add(sourceLine.Id))
                    throw new InvalidOperationException("Each original invoice line may appear only once on a supplier debit note.");

                var linkedLine = BuildLinkedLine(
                    note,
                    dto,
                    sourceLine,
                    sourceTransactions,
                    sourceTaxSnapshots);
                var attachedLinkedLine = AttachOrUpdateLine(note, linkedLine, existingLines);
                note.SubTotal += attachedLinkedLine.LineTotal - attachedLinkedLine.TaxAmount;
                note.TaxAmount += attachedLinkedLine.TaxAmount;
                note.DiscountAmount += attachedLinkedLine.DiscountAmount;
                continue;
            }

            var lineItemType = RequireLineItemType(dto.LineItemType, dto.Description);
            var effectiveAccountId = dto.GLAccountId;
            if (!effectiveAccountId.HasValue)
            {
                throw new InvalidOperationException($"Standalone debit-note line '{dto.Description}' requires a GL account.");
            }

            var account = await RequireStandaloneAccountAsync(effectiveAccountId.Value, note.DebitNoteDate, dto.Description, cancellationToken);
            if (isWriteoff && account.AccountType is not (AccountType.Revenue or AccountType.Expense))
                throw new InvalidOperationException("Supplier writeoffs require a Revenue or Expense GL account.");

            var gross = Round(dto.Quantity * dto.UnitPrice);
            var calculatedDiscount = dto.DiscountAmount ?? Round(gross * Math.Max(dto.DiscountPercentage, 0m) / 100m);
            if (calculatedDiscount < 0m || calculatedDiscount >= gross)
                throw new InvalidOperationException($"Discount on debit-note line '{dto.Description}' must be less than the gross amount.");
            var net = Round(gross - calculatedDiscount);
            if (dto.TaxGroupId.HasValue)
                await ValidatePurchaseTaxGroupAsync(dto.TaxGroupId.Value, cancellationToken);

            var taxResult = dto.TaxGroupId.HasValue
                ? await _taxEngine.CalculateTaxesAsync(new TaxCalculationRequestDto
                {
                    BaseAmount = net,
                    TaxGroupId = dto.TaxGroupId,
                    TransactionDate = note.DebitNoteDate,
                    TransactionType = ResolveApTaxTransactionType(lineItemType),
                    BusinessPartnerId = note.VendorId,
                    BusinessPartnerRole = BusinessPartnerRoleType.Supplier
                }, cancellationToken)
                : new TaxCalculationResultDto { BaseAmount = net, GrandTotal = net };
            var calculatedTax = Round(taxResult.TotalTaxAmount);
            var total = Round(net + calculatedTax);
            EnsureOptionalAmountMatches(dto.DiscountAmount, calculatedDiscount, dto.Description, "discount");
            EnsureOptionalAmountMatches(dto.TaxAmount, calculatedTax, dto.Description, "tax");
            if (dto.LineTotal.HasValue && Math.Abs(Round(dto.LineTotal.Value - total)) > 0.01m)
                throw new InvalidOperationException($"Debit-note line '{dto.Description}' total does not match its quantity, price, discount and tax.");

            var line = new SupplierDebitNoteLineItem
            {
                Id = dto.Id is { } requestedLineId && requestedLineId != Guid.Empty
                    ? requestedLineId
                    : Guid.NewGuid(),
                TenantId = TenantId,
                SupplierDebitNoteId = note.Id,
                GLAccountId = effectiveAccountId,
                ResolvedCreditAccountId = account.Id,
                LineItemType = lineItemType,
                Description = RequiredText(dto.Description, "Debit-note line description"),
                Quantity = dto.Quantity,
                UnitPrice = dto.UnitPrice,
                TaxGroupId = dto.TaxGroupId,
                TaxRate = net > 0m ? decimal.Round(calculatedTax / net * 100m, 4, MidpointRounding.AwayFromZero) : 0m,
                TaxAmount = calculatedTax,
                DiscountPercentage = Math.Max(dto.DiscountPercentage, 0m),
                DiscountAmount = calculatedDiscount,
                LineTotal = total,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName,
                CreatedById = CurrentUserId == Guid.Empty ? null : CurrentUserId
            };

            foreach (var breakdown in taxResult.TaxBreakdowns.Where(item => Round(item.TaxAmount) > 0m))
            {
                var taxAccountId = breakdown.IsInputTaxDeductible
                    ? breakdown.TaxReceivableAccountId
                    : account.Id;
                if (!taxAccountId.HasValue)
                    throw new InvalidOperationException($"Input-tax posting account is not configured for tax '{breakdown.TaxCode}'.");
                await RequirePostingAccountAsync(
                    taxAccountId.Value,
                    note.DebitNoteDate,
                    $"input-tax account for {breakdown.TaxCode}",
                    allowControlAccount: true,
                    cancellationToken);
                line.TaxComponents.Add(new SupplierDebitNoteTaxComponent
                {
                    Id = Guid.NewGuid(),
                    TenantId = TenantId,
                    SupplierDebitNoteLineItemId = line.Id,
                    TaxId = breakdown.TaxId,
                    TaxGroupId = taxResult.TaxGroupId,
                    ResolvedCreditAccountId = taxAccountId.Value,
                    BaseAmount = net,
                    TaxableAmount = Round(breakdown.TaxableAmount),
                    TaxRate = breakdown.TaxRate,
                    TaxAmount = Round(breakdown.TaxAmount),
                    CompoundBasis = breakdown.CompoundBasis,
                    CalculationOrder = breakdown.CalculationOrder,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = UserName,
                    CreatedById = CurrentUserId == Guid.Empty ? null : CurrentUserId
                });
            }
            var attachedLine = AttachOrUpdateLine(note, line, existingLines);
            note.SubTotal += attachedLine.LineTotal - attachedLine.TaxAmount;
            note.TaxAmount += attachedLine.TaxAmount;
            note.DiscountAmount += attachedLine.DiscountAmount;
        }

        note.SubTotal = Round(note.SubTotal);
        note.TaxAmount = Round(note.TaxAmount);
        note.DiscountAmount = Round(note.DiscountAmount);
        note.TotalAmount = Round(note.SubTotal + note.TaxAmount);
        if (note.ExchangeRate <= 0m)
            throw new InvalidOperationException(
                "Supplier debit note has no valid frozen exchange-rate evidence.");
        note.BaseCurrencyAmount = Round(note.TotalAmount * note.ExchangeRate);
    }

    private SupplierDebitNoteLineItem AttachOrUpdateLine(
        SupplierDebitNote note,
        SupplierDebitNoteLineItem candidate,
        IReadOnlyDictionary<Guid, SupplierDebitNoteLineItem> existingLines)
    {
        if (!existingLines.TryGetValue(candidate.Id, out var existing))
        {
            note.LineItems.Add(candidate);
            return candidate;
        }

        existing.OriginalVendorInvoiceLineItemId = candidate.OriginalVendorInvoiceLineItemId;
        existing.OriginalFinancePurchaseOrderItemId = candidate.OriginalFinancePurchaseOrderItemId;
        existing.GLAccountId = candidate.GLAccountId;
        existing.ResolvedCreditAccountId = candidate.ResolvedCreditAccountId;
        existing.OriginalAccountTransactionId = candidate.OriginalAccountTransactionId;
        existing.LineItemType = candidate.LineItemType;
        existing.Description = candidate.Description;
        existing.Quantity = candidate.Quantity;
        existing.UnitPrice = candidate.UnitPrice;
        existing.TaxGroupId = candidate.TaxGroupId;
        existing.TaxRate = candidate.TaxRate;
        existing.TaxAmount = candidate.TaxAmount;
        existing.DiscountPercentage = candidate.DiscountPercentage;
        existing.DiscountAmount = candidate.DiscountAmount;
        existing.LineTotal = candidate.LineTotal;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = UserName;
        existing.LastModifiedById = CurrentUserId == Guid.Empty ? null : CurrentUserId;
        foreach (var component in candidate.TaxComponents)
        {
            component.SupplierDebitNoteLineItemId = existing.Id;
            existing.TaxComponents.Add(component);
        }
        return existing;
    }

    private async Task ValidatePurchaseTaxGroupAsync(
        Guid taxGroupId,
        CancellationToken cancellationToken)
    {
        var group = await _db.TaxGroups.AsNoTracking()
            .Include(item => item.Components.Where(component => !component.IsDeleted))
                .ThenInclude(component => component.Tax)
            .SingleOrDefaultAsync(item =>
                item.TenantId == TenantId &&
                item.Id == taxGroupId &&
                !item.IsDeleted &&
                item.IsActive &&
                (item.Applicability == TaxApplicability.Purchases ||
                 item.Applicability == TaxApplicability.Both),
                cancellationToken)
            ?? throw new InvalidOperationException(
                "The selected tax group is not an active purchase-tax configuration for this tenant.");

        if (group.Components.Count == 0 || group.Components.Any(component =>
                component.Tax == null ||
                component.Tax.TenantId != TenantId ||
                component.Tax.IsDeleted ||
                !component.Tax.IsActive ||
                (component.Tax.Applicability != TaxApplicability.Purchases &&
                 component.Tax.Applicability != TaxApplicability.Both)))
        {
            throw new InvalidOperationException(
                "The selected purchase-tax group has no active components or contains an inactive, cross-tenant, or non-purchase tax component.");
        }
    }

    private SupplierDebitNoteLineItem BuildLinkedLine(
        SupplierDebitNote note,
        CreateSupplierDebitNoteLineItemDto dto,
        VendorInvoiceLineItem sourceLine,
        IReadOnlyCollection<AccountTransaction> sourceTransactions,
        IReadOnlyCollection<TaxCalculation> sourceTaxSnapshots)
    {
        if (dto.GLAccountId.HasValue)
            throw new InvalidOperationException(
                $"Debit-note line '{dto.Description}' is linked to an invoice and must reverse the source line's account; remove the manual GL account.");
        if (dto.Quantity > sourceLine.Quantity + 0.0001m)
            throw new InvalidOperationException($"Debit-note quantity for '{dto.Description}' exceeds the source invoice line quantity.");
        if (Round(dto.UnitPrice) != Round(sourceLine.UnitPrice))
            throw new InvalidOperationException($"Linked debit-note line '{dto.Description}' must use the immutable source invoice unit price.");
        var lineItemType = RequireLineItemType(
            sourceLine.LineItemType,
            sourceLine.Description,
            sourceLineage: true);

        var baseTransactions = sourceTransactions.Where(item =>
                item.SourceDocumentLineId == sourceLine.Id &&
                item.DebitAmount > 0m &&
                !IsTaxTransaction(item))
            .OrderBy(item => item.LineNumber).ThenBy(item => item.Id).ToList();
        if (baseTransactions.Count == 0)
            throw SourceLineageUnavailable(sourceLine, "no posted base transactions were found");
        if ((baseTransactions.Select(item => item.TransactionTag).Distinct().Count() != 1 ||
            baseTransactions.Select(item => item.ExchangeRateId).Distinct().Count() != 1) &&
            !(note.InventorySupplierReturnAccountingGroupId.HasValue && baseTransactions.All(item => IsReceiptCostPurpose(item.TransactionTag))))
            throw SourceLineageUnavailable(sourceLine, "posted base splits do not share the same accounting purpose and exchange-rate evidence");
        var originalTransaction = note.InventorySupplierReturnAccountingGroupId.HasValue
            ? baseTransactions.FirstOrDefault(item => item.TransactionTag == "AP-GRV") ?? baseTransactions[0]
            : baseTransactions[0];
        var ratio = sourceLine.Quantity <= 0m ? 0m : dto.Quantity / sourceLine.Quantity;
        var gross = Round(dto.Quantity * sourceLine.UnitPrice);
        var discount = Round(sourceLine.DiscountAmount * ratio);
        var net = Round(gross - discount);

        var snapshots = sourceTaxSnapshots
            .Where(item => item.DocumentLineId == sourceLine.Id)
            .OrderBy(item => item.CalculationOrder)
            .ThenBy(item => item.Id)
            .ToList();
        if (Round(sourceLine.TaxAmount) > 0m && snapshots.Count == 0)
            throw SourceLineageUnavailable(sourceLine, "server-derived tax component snapshots were not found");

        var line = new SupplierDebitNoteLineItem
        {
            Id = dto.Id is { } requestedLineId && requestedLineId != Guid.Empty
                ? requestedLineId
                : Guid.NewGuid(),
            TenantId = TenantId,
            SupplierDebitNoteId = note.Id,
            OriginalVendorInvoiceLineItemId = sourceLine.Id,
            ResolvedCreditAccountId = originalTransaction.AccountId,
            OriginalAccountTransactionId = originalTransaction.Id,
            LineItemType = lineItemType,
            Description = RequiredText(dto.Description, "Debit-note line description"),
            Quantity = dto.Quantity,
            UnitPrice = sourceLine.UnitPrice,
            TaxGroupId = sourceLine.TaxGroupId,
            DiscountPercentage = sourceLine.DiscountPercentage,
            DiscountAmount = discount,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = UserName,
            CreatedById = CurrentUserId == Guid.Empty ? null : CurrentUserId
        };

        foreach (var snapshot in snapshots)
        {
            if (!snapshot.PostingAccountId.HasValue)
                throw SourceLineageUnavailable(sourceLine, $"tax component {snapshot.TaxId} has no frozen posting account");
            var taxToken = $"TaxId={snapshot.TaxId}";
            var candidates = sourceTransactions.Where(item =>
                    item.SourceDocumentLineId == sourceLine.Id &&
                    item.DebitAmount > 0m &&
                    IsTaxTransaction(item) &&
                    item.AccountId == snapshot.PostingAccountId.Value &&
                    (item.Notes?.Contains(taxToken, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
            if (candidates.Count != 1)
                throw SourceLineageUnavailable(sourceLine, $"tax component {snapshot.TaxId} does not map to one original journal transaction");

            line.TaxComponents.Add(new SupplierDebitNoteTaxComponent
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                SupplierDebitNoteLineItemId = line.Id,
                TaxId = snapshot.TaxId,
                TaxGroupId = snapshot.TaxGroupId,
                OriginalTaxCalculationId = snapshot.Id,
                OriginalAccountTransactionId = candidates[0].Id,
                ResolvedCreditAccountId = snapshot.PostingAccountId.Value,
                BaseAmount = Round(snapshot.BaseAmount * ratio),
                TaxableAmount = Round(snapshot.TaxableAmount * ratio),
                TaxRate = snapshot.TaxRate,
                TaxAmount = Round(snapshot.TaxAmount * ratio),
                CompoundBasis = snapshot.CompoundBasis,
                CalculationOrder = snapshot.CalculationOrder,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName,
                CreatedById = CurrentUserId == Guid.Empty ? null : CurrentUserId
            });
        }

        line.TaxAmount = Round(line.TaxComponents.Sum(item => item.TaxAmount));
        line.TaxRate = net > 0m ? decimal.Round(line.TaxAmount / net * 100m, 4, MidpointRounding.AwayFromZero) : 0m;
        line.LineTotal = Round(net + line.TaxAmount);
        EnsureOptionalAmountMatches(dto.DiscountAmount, discount, dto.Description, "discount");
        EnsureOptionalAmountMatches(dto.TaxAmount, line.TaxAmount, dto.Description, "tax");
        EnsureOptionalAmountMatches(dto.LineTotal, line.LineTotal, dto.Description, "total");
        if (dto.TaxRate > 0m && Math.Abs(dto.TaxRate - line.TaxRate) > 0.0001m)
            throw new InvalidOperationException($"Linked debit-note line '{dto.Description}' tax rate differs from the immutable source invoice tax evidence.");
        return line;
    }

    private async Task<FinancePostingRequestV2Dto> BuildPostingRequestAsync(
        SupplierDebitNote note,
        FinancePostingProducerContext? producer,
        CancellationToken cancellationToken)
    {
        var sourceDimensions = producer is not null && _sourceDimensions is not null
            ? await _sourceDimensions.GetPostingDimensionsAsync(producer, note.Id, cancellationToken)
            : new Dictionary<Guid, IReadOnlyList<FinancePostingDimensionValueDto>>();
        var settings = await _db.FinanceSettings.AsNoTracking().FirstOrDefaultAsync(item =>
            item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Finance settings are not configured for this tenant.");
        var functionalCurrency = NormalizeCurrency(settings.BaseCurrency, "GHS");
        var currency = NormalizeCurrency(note.CurrencyCode, functionalCurrency);
        var linkedInvoice = note.OriginalVendorInvoice;
        var accountingBookCode = "IFRS";
        if (note.InventorySupplierReturnAccountingGroupId.HasValue)
        {
            var originalJournal = await _db.JournalEntries.AsNoTracking().SingleOrDefaultAsync(j =>
                j.Id == linkedInvoice!.JournalEntryId && j.TenantId == TenantId && !j.IsDeleted && !j.IsReversed && j.PostingStatus == "Posted", cancellationToken)
                ?? throw new InvalidOperationException("RTV_ORIGINAL_BOOK_REQUIRED: original invoice posting is unavailable.");
            var originalBook = await _db.AccountingBooks.AsNoTracking().SingleOrDefaultAsync(book =>
                book.Id == originalJournal.AccountingBookId && book.TenantId == TenantId && !book.IsDeleted, cancellationToken)
                ?? throw new InvalidOperationException("RTV_ORIGINAL_BOOK_REQUIRED: original invoice accounting book is unavailable.");
            if (!string.Equals(originalBook.Code, originalJournal.BookClassification, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("RTV_ORIGINAL_BOOK_CHANGED: original invoice accounting book identity no longer matches its retained code.");
            accountingBookCode = originalBook.Code;
        }
        var rate = linkedInvoice != null
            ? RequireLinkedInvoiceRate(note, linkedInvoice, currency, functionalCurrency)
            : await ValidatePersistedExchangeRateEvidenceAsync(
                currency,
                functionalCurrency,
                note.DebitNoteDate,
                note.ExchangeRate,
                settings,
                cancellationToken);
        var sourceInvoiceLines = linkedInvoice?.LineItems
            .Where(item => !item.IsDeleted)
            .ToDictionary(item => item.Id)
            ?? new Dictionary<Guid, VendorInvoiceLineItem>();
        var lineageIds = note.LineItems.Where(item => !item.IsDeleted)
            .SelectMany(item => item.TaxComponents.Where(component => !component.IsDeleted)
                .Select(component => component.OriginalAccountTransactionId))
            .Concat(note.LineItems.Where(item => !item.IsDeleted).Select(item => item.OriginalAccountTransactionId))
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToHashSet();
        var originalJournalId = linkedInvoice?.JournalEntryId;
        var originalTransactions = lineageIds.Count == 0
            ? new Dictionary<Guid, AccountTransaction>()
            : await _db.AccountTransactions.AsNoTracking()
                .Where(item => item.TenantId == TenantId && !item.IsDeleted &&
                    (lineageIds.Contains(item.Id) || (originalJournalId.HasValue &&
                        item.JournalEntryId == originalJournalId.Value)))
                .ToDictionaryAsync(item => item.Id, cancellationToken);

        AccountTransaction? originalApControl = null;
        if (linkedInvoice?.JournalEntryId.HasValue == true)
        {
            var apCandidates = await _db.AccountTransactions.AsNoTracking().Where(item =>
                    item.TenantId == TenantId &&
                    item.JournalEntryId == linkedInvoice.JournalEntryId.Value &&
                    !item.IsDeleted &&
                    item.CreditAmount > 0m &&
                    item.TransactionTag == "AP-Control")
                .ToListAsync(cancellationToken);
            if (apCandidates.Count != 1)
                throw new InvalidOperationException("AP_DEBIT_NOTE_SOURCE_LINEAGE_UNAVAILABLE: the source invoice AP-control transaction is ambiguous or missing.");
            originalApControl = apCandidates[0];
        }

        var apAccountId = originalApControl?.AccountId
            ?? await ResolveStandaloneApControlAccountAsync(note, settings, currency, cancellationToken);
        var originalDiscountTransactions = linkedInvoice?.JournalEntryId.HasValue == true
            ? await _db.AccountTransactions.AsNoTracking().Where(item =>
                    item.TenantId == TenantId
                    && item.JournalEntryId == linkedInvoice.JournalEntryId.Value
                    && !item.IsDeleted
                    && item.CreditAmount > 0m
                    && item.TransactionTag == "AP-Discount")
                .ToListAsync(cancellationToken)
            : [];
        var lines = new List<FinancePostingLineDto>();
        var receiptCostLineIds = linkedInvoice == null ? new HashSet<Guid>() :
            (await _db.Set<VendorInvoiceReceiptCostAllocation>().AsNoTracking().Where(x => x.TenantId == TenantId && !x.IsDeleted &&
                x.VendorInvoiceId == linkedInvoice.Id && !x.ReversalJournalEntryId.HasValue).Select(x => x.VendorInvoiceLineItemId)
                .Distinct().ToListAsync(cancellationToken)).ToHashSet();
        var lineNumber = 2;
        foreach (var line in note.LineItems.Where(item => !item.IsDeleted))
        {
            decimal principalAmount;
            Guid accountId;
            Guid? exchangeRateId = null;
            List<AccountTransaction>? baseSplits = null;
            if (linkedInvoice != null)
            {
                if (!line.OriginalVendorInvoiceLineItemId.HasValue ||
                    !sourceInvoiceLines.TryGetValue(line.OriginalVendorInvoiceLineItemId.Value, out var sourceLine))
                    throw SourceLineageUnavailable(null, "the frozen source invoice line is missing");
                if (!line.OriginalAccountTransactionId.HasValue ||
                    !originalTransactions.TryGetValue(line.OriginalAccountTransactionId.Value, out var sourceTransaction) ||
                    sourceTransaction.SourceDocumentLineId != sourceLine.Id ||
                    line.ResolvedCreditAccountId != sourceTransaction.AccountId)
                    throw SourceLineageUnavailable(sourceLine, "the frozen base transaction no longer reconciles to the source line");
                var ratio = sourceLine.Quantity <= 0m ? 0m : line.Quantity / sourceLine.Quantity;
                baseSplits = originalTransactions.Values.Where(item => item.JournalEntryId == linkedInvoice.JournalEntryId &&
                    item.SourceDocumentLineId == sourceLine.Id && item.DebitAmount > 0m && !IsTaxTransaction(item))
                    .OrderBy(item => item.LineNumber).ThenBy(item => item.Id).ToList();
                var allocatedReturnCost = note.InventorySupplierReturnAccountingGroupId.HasValue && receiptCostLineIds.Contains(sourceLine.Id);
                if (receiptCostLineIds.Contains(sourceLine.Id) && !allocatedReturnCost)
                    throw SourceLineageUnavailable(sourceLine, "receipt-cost adjustments require the governed Inventory return or invoice reversal valuation owner");
                if (baseSplits.Count == 0 || !allocatedReturnCost && baseSplits.Any(item => item.TransactionTag != sourceTransaction.TransactionTag ||
                    item.ExchangeRateId != sourceTransaction.ExchangeRateId))
                    throw SourceLineageUnavailable(sourceLine, "posted base splits no longer reconcile to the source line");
                if (allocatedReturnCost)
                {
                    // The dispatch owner has already issued stock at carrying value.
                    // Produce a net commercial principal for its clearing adapter;
                    // never replay the invoice's Inventory value adjustment here.
                    var sourceCost = originalTransactions.Values.Where(item => item.SourceDocumentLineId == sourceLine.Id && IsReceiptCostPurpose(item.TransactionTag)).ToArray();
                    var originalNet = Round(sourceCost.Sum(item => item.DebitAmount - item.CreditAmount));
                    if (sourceCost.Length == 0 || originalNet <= 0m)
                        throw SourceLineageUnavailable(sourceLine, "retained signed receipt-cost transactions are missing or nonpositive");
                    principalAmount = Round(line.LineTotal - line.TaxAmount);
                    if (Math.Abs(Round(originalNet * ratio) - Functional(principalAmount, currency, functionalCurrency, rate)) > 0.01m)
                        throw SourceLineageUnavailable(sourceLine, "the commercial credit does not reconcile to original signed receipt-cost postings");
                    baseSplits = null;
                }
                else principalAmount = Round(baseSplits.Sum(SourceDebitAmount) * ratio);
                accountId = sourceTransaction.AccountId;
                exchangeRateId = allocatedReturnCost ? originalApControl?.ExchangeRateId : sourceTransaction.ExchangeRateId;
            }
            else
            {
                accountId = line.ResolvedCreditAccountId
                    ?? throw new InvalidOperationException($"Debit-note line '{line.Description}' is missing its server-resolved posting account.");
                if (string.Equals(line.LineItemType, "Writeoff", StringComparison.OrdinalIgnoreCase))
                {
                    var writeoffAccount = await RequireStandaloneAccountAsync(accountId, note.DebitNoteDate, line.Description, cancellationToken);
                    if (writeoffAccount.AccountType is not (AccountType.Revenue or AccountType.Expense))
                        throw new InvalidOperationException("Supplier writeoffs require a Revenue or Expense GL account.");
                }
                principalAmount = Round(line.LineTotal - line.TaxAmount);
            }

            var principalLine = PostingLine(
                accountId,
                $"Supplier debit note {note.DebitNoteNumber} - {line.Description}",
                debit: 0m,
                credit: Functional(principalAmount, currency, functionalCurrency, rate),
                sourceAmount: principalAmount,
                currency,
                functionalCurrency,
                rate,
                note.DebitNoteDate,
                note.DebitNoteNumber,
                lineNumber++,
                string.Equals(line.LineItemType, "Writeoff", StringComparison.OrdinalIgnoreCase)
                    ? "AP-Writeoff" : "AP-SupplierDebitNote-Line",
                line.Id,
                exchangeRateId,
                linkedInvoice != null ? "Original AP invoice exchange-rate snapshot" : null);
            var inheritedDimensions = sourceDimensions.TryGetValue(line.Id, out var dimensionValues)
                ? dimensionValues
                : Array.Empty<FinancePostingDimensionValueDto>();
            principalLine.Dimensions = inheritedDimensions;
            if (baseSplits is { Count: > 1 })
            {
                lineNumber--; // The aggregate principal line is replaced by its account splits.
                var amounts = MonetaryAllocation.Allocate(baseSplits.Select(SourceDebitAmount).ToArray(), principalAmount);
                for (var splitIndex = 0; splitIndex < baseSplits.Count; splitIndex++)
                {
                    if (amounts[splitIndex] == 0m) continue;
                    var splitLine = PostingLine(baseSplits[splitIndex].AccountId, principalLine.Description!,
                        0m, Functional(amounts[splitIndex], currency, functionalCurrency, rate), amounts[splitIndex],
                        currency, functionalCurrency, rate, note.DebitNoteDate, note.DebitNoteNumber,
                        lineNumber++, principalLine.TransactionTag!, line.Id, baseSplits[splitIndex].ExchangeRateId,
                        "Original AP invoice exchange-rate snapshot");
                    splitLine.Dimensions = inheritedDimensions;
                    lines.Add(splitLine);
                }
            }
            else lines.Add(principalLine);

            if (linkedInvoice != null
                && !IsNetPostedSourceTransaction(originalTransactions[line.OriginalAccountTransactionId!.Value])
                && Round(line.DiscountAmount) > 0m)
            {
                var exactDiscounts = originalDiscountTransactions.Where(item =>
                    item.SourceDocumentLineId == line.OriginalVendorInvoiceLineItemId).ToList();
                var discountTransaction = exactDiscounts.Count switch
                {
                    1 => exactDiscounts[0],
                    0 when originalDiscountTransactions.Count == 1
                        && !originalDiscountTransactions[0].SourceDocumentLineId.HasValue =>
                        originalDiscountTransactions[0],
                    _ => throw new InvalidOperationException(
                        "AP_DEBIT_NOTE_SOURCE_LINEAGE_UNAVAILABLE: the source invoice discount transaction is ambiguous or missing.")
                };
                var discountLine = PostingLine(
                    discountTransaction.AccountId,
                    $"Reverse purchase discount - {note.DebitNoteNumber} - {line.Description}",
                    Functional(line.DiscountAmount, currency, functionalCurrency, rate),
                    0m,
                    line.DiscountAmount,
                    currency,
                    functionalCurrency,
                    rate,
                    note.DebitNoteDate,
                    note.DebitNoteNumber,
                    lineNumber++,
                    "AP-SupplierDebitNote-Discount",
                    line.Id,
                    discountTransaction.ExchangeRateId,
                    "Original AP invoice exchange-rate snapshot");
                discountLine.Dimensions = inheritedDimensions;
                lines.Add(discountLine);
            }

            foreach (var component in line.TaxComponents.Where(item => !item.IsDeleted).OrderBy(item => item.CalculationOrder))
            {
                Guid? componentRateId = null;
                if (linkedInvoice != null)
                {
                    if (!component.OriginalAccountTransactionId.HasValue ||
                        !originalTransactions.TryGetValue(component.OriginalAccountTransactionId.Value, out var taxTransaction) ||
                        taxTransaction.SourceDocumentLineId != line.OriginalVendorInvoiceLineItemId ||
                        component.ResolvedCreditAccountId != taxTransaction.AccountId)
                        throw new InvalidOperationException("AP_DEBIT_NOTE_SOURCE_LINEAGE_UNAVAILABLE: a frozen source tax transaction no longer reconciles.");
                    componentRateId = taxTransaction.ExchangeRateId;
                }
                lines.Add(PostingLine(
                    component.ResolvedCreditAccountId,
                    $"Reverse input tax - {note.DebitNoteNumber}",
                    0m,
                    Functional(component.TaxAmount, currency, functionalCurrency, rate),
                    component.TaxAmount,
                    currency,
                    functionalCurrency,
                    rate,
                    note.DebitNoteDate,
                    note.DebitNoteNumber,
                    lineNumber++,
                    "AP-SupplierDebitNote-Tax",
                    line.Id,
                    componentRateId,
                    linkedInvoice != null ? "Original AP invoice exchange-rate snapshot" : null));
            }
        }

        var creditTotal = Round(lines.Sum(item => item.CreditAmount));
        var nonControlDebits = Round(lines.Sum(item => item.DebitAmount));
        var apDebit = Round(creditTotal - nonControlDebits);
        var expectedApDebit = Functional(note.TotalAmount, currency, functionalCurrency, rate);
        if (apDebit <= 0m)
            throw new InvalidOperationException("Supplier debit note has no positive value to post.");
        if (apDebit != expectedApDebit)
            throw new InvalidOperationException("Supplier debit-note immutable source-line amounts do not reconcile to the document total.");
        lines.Insert(0, PostingLine(
            apAccountId,
            $"Supplier debit note {note.DebitNoteNumber}",
            apDebit,
            0m,
            note.TotalAmount,
            currency,
            functionalCurrency,
            rate,
            note.DebitNoteDate,
            note.DebitNoteNumber,
            1,
            "AP-SupplierDebitNote-Control",
            exchangeRateId: originalApControl?.ExchangeRateId,
            exchangeRateSource: linkedInvoice != null ? "Original AP invoice exchange-rate snapshot" : null));
        if (Round(lines.Sum(item => item.DebitAmount)) != Round(lines.Sum(item => item.CreditAmount)))
            throw new InvalidOperationException("Supplier debit-note posting is not balanced.");

        return new FinancePostingRequestV2Dto
        {
            SourceModule = "AP",
            SourceDocumentType = "SupplierDebitNote",
            SourceDocumentId = note.Id,
            SourceDocumentTenantId = note.TenantId,
            PostingAction = "Post",
            SourceDocumentReference = note.DebitNoteNumber,
            Description = $"Supplier debit note {note.DebitNoteNumber} - {note.Vendor.PartnerName}",
            PostingDate = note.DebitNoteDate,
            JournalType = "AP Supplier Debit Note",
            AccountingBookCode = accountingBookCode,
            FunctionalCurrencyCode = functionalCurrency,
            IdempotencyKey = $"AP:SupplierDebitNote:{note.TenantId:N}:{note.Id:N}:Post",
            ReturnExistingOnDuplicate = true,
            PreserveHistoricalExchangeRateSnapshot = linkedInvoice != null,
            ExchangeRateOverrideReason = linkedInvoice != null
                ? "Exact partial reversal of independently approved AP invoice measurement"
                : null,
            ExchangeRateOverrideApprovedByUserId = linkedInvoice != null ? note.ApprovedById : null,
            ExchangeRateOverrideApprovedAt = linkedInvoice != null ? note.ApprovedAt : null,
            Lines = lines,
            TaxCalculationSnapshots = note.LineItems.Where(item => !item.IsDeleted)
                .SelectMany(item => item.TaxComponents.Where(component => !component.IsDeleted).Select(component =>
                    new FinanceTaxCalculationSnapshotDto
                    {
                        DocumentType = "SupplierDebitNote",
                        DocumentId = note.Id,
                        DocumentLineId = item.Id,
                        TaxId = component.TaxId,
                        TaxGroupId = component.TaxGroupId,
                        PostingAccountId = component.ResolvedCreditAccountId,
                        CurrencyCode = note.CurrencyCode,
                        CurrencyDecimalPlaces = 2,
                        BaseAmount = component.BaseAmount,
                        TaxableAmount = component.TaxableAmount,
                        TaxRate = component.TaxRate,
                        TaxAmount = component.TaxAmount,
                        RawTaxAmount = component.TaxAmount,
                        RoundingAdjustment = 0m,
                        AllocationSequence = component.CalculationOrder,
                        CompoundBasis = component.CompoundBasis,
                        CalculationOrder = component.CalculationOrder,
                        CalculationDate = note.DebitNoteDate
                    })).ToList()
        };
    }

    private static FinancePostingLineDto PostingLine(
        Guid accountId,
        string description,
        decimal debit,
        decimal credit,
        decimal sourceAmount,
        string currency,
        string functionalCurrency,
        decimal rate,
        DateTime date,
        string reference,
        int lineNumber,
        string tag,
        Guid? sourceDocumentLineId = null,
        Guid? exchangeRateId = null,
        string? exchangeRateSource = null)
    {
        var sameCurrency = string.Equals(currency, functionalCurrency, StringComparison.OrdinalIgnoreCase);
        return new FinancePostingLineDto
        {
            AccountId = accountId,
            SourceDocumentLineId = sourceDocumentLineId,
            Description = description,
            DebitAmount = Round(debit),
            CreditAmount = Round(credit),
            TransactionCurrency = currency,
            TransactionDebitAmount = debit > 0m ? Round(sourceAmount) : 0m,
            TransactionCreditAmount = credit > 0m ? Round(sourceAmount) : 0m,
            ForeignCurrencyAmount = sameCurrency ? null : Round(sourceAmount),
            ExchangeRateId = sameCurrency ? null : exchangeRateId,
            ExchangeRate = sameCurrency ? null : rate,
            ExchangeRateSource = sameCurrency ? null : exchangeRateSource,
            ExchangeRateDate = sameCurrency ? null : date.Date,
            SourceReferenceNumber = reference,
            LineNumber = lineNumber,
            TransactionTag = tag
        };
    }

    private async Task<decimal> ResolveDebitNoteExchangeRateAsync(
        string currency,
        DateTime debitNoteDate,
        decimal suppliedRate,
        VendorInvoice? invoice,
        CancellationToken cancellationToken)
    {
        if (invoice == null)
            return await ResolveApprovedExchangeRateAsync(currency, debitNoteDate, suppliedRate, cancellationToken);

        var invoiceCurrency = NormalizeCurrency(invoice.CurrencyCode, currency);
        if (!string.Equals(invoiceCurrency, currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A linked supplier debit note must inherit the source invoice transaction currency.");
        if (invoice.ExchangeRate <= 0m)
            throw new InvalidOperationException("AP_DEBIT_NOTE_SOURCE_LINEAGE_UNAVAILABLE: source invoice exchange-rate evidence is invalid.");

        // This is a correction of the original recognition, not a new FX transaction. Reusing the
        // invoice measurement prevents a debit note from manufacturing a realized FX difference;
        // normal realized FX remains a cash-settlement event.
        return invoice.ExchangeRate;
    }

    private static decimal RequireLinkedInvoiceRate(
        SupplierDebitNote note,
        VendorInvoice invoice,
        string currency,
        string functionalCurrency)
    {
        if (!string.Equals(currency, NormalizeCurrency(invoice.CurrencyCode, currency), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Linked supplier debit-note currency no longer matches its source invoice.");
        var expected = string.Equals(currency, functionalCurrency, StringComparison.OrdinalIgnoreCase)
            ? 1m
            : invoice.ExchangeRate;
        if (expected <= 0m || RoundRate(note.ExchangeRate) != RoundRate(expected))
            throw new InvalidOperationException("AP_DEBIT_NOTE_SOURCE_LINEAGE_UNAVAILABLE: debit-note exchange-rate snapshot no longer matches the source invoice.");
        return expected;
    }

    private async Task<Account> RequireStandaloneAccountAsync(
        Guid accountId,
        DateTime effectiveDate,
        string description,
        CancellationToken cancellationToken) =>
        await RequirePostingAccountAsync(
            accountId,
            effectiveDate,
            $"GL account for debit-note line '{description}'",
            allowControlAccount: false,
            cancellationToken,
            requireDirectPosting: true);

    private async Task<Guid> ResolveStandaloneApControlAccountAsync(
        SupplierDebitNote note,
        FinanceSettings settings,
        string transactionCurrency,
        CancellationToken cancellationToken)
    {
        foreach (var candidateId in new[] { settings.ControlAccountApId }
                     .Where(item => item.HasValue)
                     .Select(item => item!.Value)
                     .Distinct())
        {
            var candidate = await _db.Accounts.AsNoTracking().SingleOrDefaultAsync(item =>
                item.TenantId == TenantId && item.Id == candidateId && !item.IsDeleted,
                cancellationToken);
            if (candidate == null ||
                candidate.Status != AccountStatus.Active ||
                candidate.AccountType != AccountType.Liability ||
                !candidate.IsControlAccount ||
                (candidate.EffectiveDate.HasValue && candidate.EffectiveDate.Value.Date > note.DebitNoteDate.Date) ||
                (candidate.ExpirationDate.HasValue && candidate.ExpirationDate.Value.Date < note.DebitNoteDate.Date) ||
                (!candidate.IsMultiCurrency &&
                 !string.Equals(candidate.CurrencyCode, transactionCurrency, StringComparison.OrdinalIgnoreCase)))
                continue;
            return candidate.Id;
        }

        throw new InvalidOperationException(
            "No tenant-owned active/effective Liability AP-control account is configured for this debit-note currency.");
    }

    private async Task<Account> RequirePostingAccountAsync(
        Guid accountId,
        DateTime effectiveDate,
        string label,
        bool allowControlAccount,
        CancellationToken cancellationToken,
        bool requireDirectPosting = false)
    {
        var account = await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == accountId && !item.IsDeleted,
            cancellationToken);
        if (account == null ||
            account.Status != AccountStatus.Active ||
            (account.EffectiveDate.HasValue && account.EffectiveDate.Value.Date > effectiveDate.Date) ||
            (account.ExpirationDate.HasValue && account.ExpirationDate.Value.Date < effectiveDate.Date))
            throw new InvalidOperationException($"{label} was not found or is inactive on the debit-note date.");
        if ((!allowControlAccount && account.IsControlAccount) || (requireDirectPosting && !account.AllowDirectPosting))
            throw new InvalidOperationException($"{label} is not suitable for this controlled posting.");
        return account;
    }

    private static TaxTransactionType ResolveApTaxTransactionType(string? lineItemType) =>
        string.Equals(lineItemType, "Service", StringComparison.OrdinalIgnoreCase)
            ? TaxTransactionType.PurchaseOfServices
            : TaxTransactionType.PurchaseOfGoods;

    private static string RequireLineItemType(
        string? value,
        string? description,
        bool sourceLineage = false)
    {
        if (!sourceLineage && string.Equals(value, "Writeoff", StringComparison.OrdinalIgnoreCase))
            return "Writeoff";
        if (string.Equals(value, "Expense", StringComparison.OrdinalIgnoreCase))
            return "Expense";
        if (string.Equals(value, "Service", StringComparison.OrdinalIgnoreCase))
            return "Service";
        if (string.Equals(value, "FixedAsset", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "Fixed Asset", StringComparison.OrdinalIgnoreCase))
            return "FixedAsset";
        if (string.Equals(value, "Inventory", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "Product", StringComparison.OrdinalIgnoreCase))
            return "Inventory";

        var code = sourceLineage
            ? "AP_DEBIT_NOTE_SOURCE_LINEAGE_UNAVAILABLE"
            : "AP_DEBIT_NOTE_LINE_CLASSIFICATION_REQUIRED";
        throw new InvalidOperationException(
            $"{code}: debit-note line '{TrimToNull(description) ?? "unknown"}' has no supported immutable classification. " +
            "Classify it explicitly; Finance must not guess Inventory versus Expense.");
    }

    private static void ValidateLineClassificationEvidence(SupplierDebitNote note)
    {
        var sourceLineage = note.OriginalVendorInvoiceId.HasValue;
        foreach (var line in note.LineItems.Where(item => !item.IsDeleted))
        {
            _ = RequireLineItemType(line.LineItemType, line.Description, sourceLineage);
            if (string.Equals(line.LineItemType, "Writeoff", StringComparison.OrdinalIgnoreCase) &&
                (line.OriginalVendorInvoiceLineItemId.HasValue || line.TaxGroupId.HasValue ||
                 line.TaxAmount != 0m || line.TaxRate != 0m || line.TaxComponents.Any(component => !component.IsDeleted) ||
                 line.DiscountAmount != 0m || line.DiscountPercentage != 0m))
                throw new InvalidOperationException("AP_WRITEOFF_SETTLEMENT_ONLY: saved supplier writeoff evidence contains invoice source lines, tax or discounts.");
        }
    }

    private static void EnsureOptionalAmountMatches(decimal? supplied, decimal calculated, string description, string label)
    {
        if (supplied.HasValue && Math.Abs(Round(supplied.Value - calculated)) > 0.01m)
            throw new InvalidOperationException(
                $"Debit-note line '{description}' {label} differs from the server-derived source/tax evidence.");
    }

    private static InvalidOperationException SourceLineageUnavailable(VendorInvoiceLineItem? line, string reason) =>
        new($"AP_DEBIT_NOTE_SOURCE_LINEAGE_UNAVAILABLE: source invoice line '{line?.Description ?? "unknown"}' cannot be reversed because {reason}. Use a separately approved standalone adjustment instead of guessing historical accounts.");

    private static bool IsTaxTransaction(AccountTransaction item) =>
        item.TransactionTag?.StartsWith("AP-Tax-", StringComparison.OrdinalIgnoreCase) == true;

    private static bool IsReceiptCostPurpose(string? tag) => tag is "AP-GRV" or "AP-PRICE-VARIANCE" or "AP-INVENTORY-COST" or "AP-RECEIPT-FX";

    private static bool IsNetPostedSourceTransaction(AccountTransaction item) =>
        string.Equals(item.TransactionTag, "AP-FixedAsset", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(item.TransactionTag, "AP-GRV", StringComparison.OrdinalIgnoreCase);

    private static decimal SourceDebitAmount(AccountTransaction transaction) =>
        transaction.TransactionDebitAmount
        ?? transaction.ForeignCurrencyAmount
        ?? transaction.DebitAmount;

    private async Task ValidateSupplierReferenceUniqueAsync(
        Guid vendorId,
        string? reference,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        var normalized = TrimToNull(reference);
        if (normalized == null)
            return;
        if (await _db.SupplierDebitNotes.AnyAsync(item =>
                item.TenantId == TenantId &&
                item.VendorId == vendorId &&
                item.Id != excludedId &&
                !item.IsDeleted &&
                item.SupplierCreditNoteReference == normalized,
                cancellationToken))
            throw new InvalidOperationException($"Supplier credit-note reference '{normalized}' already exists for this supplier.");
    }

    private async Task RecordTaxCalculationSnapshotsAsync(
        IReadOnlyList<FinanceTaxCalculationSnapshotDto> snapshots,
        CancellationToken cancellationToken)
    {
        foreach (var snapshot in snapshots)
        {
            var exists = await _db.Set<TaxCalculation>().AnyAsync(item =>
                item.TenantId == TenantId &&
                item.DocumentType == snapshot.DocumentType &&
                item.DocumentId == snapshot.DocumentId &&
                item.DocumentLineId == snapshot.DocumentLineId &&
                item.TaxId == snapshot.TaxId &&
                item.TaxGroupId == snapshot.TaxGroupId &&
                !item.IsDeleted,
                cancellationToken);
            if (exists)
                continue;
            _db.Set<TaxCalculation>().Add(new TaxCalculation
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                DocumentType = snapshot.DocumentType,
                DocumentId = snapshot.DocumentId,
                DocumentLineId = snapshot.DocumentLineId,
                TaxId = snapshot.TaxId,
                TaxGroupId = snapshot.TaxGroupId,
                PostingAccountId = snapshot.PostingAccountId,
                CurrencyCode = snapshot.CurrencyCode,
                CurrencyDecimalPlaces = snapshot.CurrencyDecimalPlaces,
                BaseAmount = snapshot.BaseAmount,
                TaxableAmount = snapshot.TaxableAmount,
                TaxRate = snapshot.TaxRate,
                TaxAmount = snapshot.TaxAmount,
                RawTaxAmount = snapshot.RawTaxAmount,
                RoundingAdjustment = snapshot.RoundingAdjustment,
                AllocationSequence = snapshot.AllocationSequence,
                CompoundBasis = snapshot.CompoundBasis,
                CalculationOrder = snapshot.CalculationOrder,
                CalculationDate = snapshot.CalculationDate,
                IsManualOverride = snapshot.IsManualOverride,
                OverrideReason = snapshot.OverrideReason,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = UserName,
                CreatedById = CurrentUserId == Guid.Empty ? null : CurrentUserId
            });
        }
    }

    private async Task<decimal> ResolveApprovedExchangeRateAsync(
        string transactionCurrency,
        DateTime documentDate,
        decimal suppliedRate,
        CancellationToken cancellationToken)
    {
        var settings = await _db.FinanceSettings.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Finance settings are not configured for this tenant.");
        var functionalCurrency = NormalizeCurrency(settings.BaseCurrency, "GHS");
        var currency = NormalizeCurrency(transactionCurrency, functionalCurrency);
        if (string.Equals(currency, functionalCurrency, StringComparison.OrdinalIgnoreCase))
            return 1m;

        var side = settings.DirectionalExchangeRatePolicyEnabled
            ? settings.ApInvoiceQuoteSide
            : ExchangeRateQuoteSide.Mid;
        var effectiveDate = documentDate.Date;
        var rate = await _db.ExchangeRates.AsNoTracking()
            .Where(item =>
                item.TenantId == TenantId &&
                !item.IsDeleted &&
                item.BaseCurrencyCode == functionalCurrency &&
                item.TargetCurrencyCode == currency &&
                item.RateType == ExchangeRateType.Daily &&
                item.QuoteSide == side &&
                item.IsActive &&
                item.Rate > 0m &&
                (item.ApprovalStatus == RateApprovalStatus.Approved ||
                 item.ApprovalStatus == RateApprovalStatus.AutoApproved) &&
                item.EffectiveDate.Date <= effectiveDate &&
                (!item.EndDate.HasValue || item.EndDate.Value.Date >= effectiveDate))
            .OrderByDescending(item => item.EffectiveDate)
            .ThenByDescending(item => item.Priority)
            .ThenByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                $"No active approved {side} Daily exchange rate exists for {currency} to {functionalCurrency} on {effectiveDate:yyyy-MM-dd}. Load and approve the rate before saving this supplier debit note.");

        if (suppliedRate <= 0m || RoundRate(suppliedRate) != RoundRate(rate.InverseRate))
            throw new InvalidOperationException(
                $"The supplied exchange-rate snapshot does not match the active approved {side} Daily rate for {currency} to {functionalCurrency} on {effectiveDate:yyyy-MM-dd}. Refresh the approved rate before saving this supplier debit note.");
        return rate.InverseRate;
    }

    private async Task<decimal> ValidatePersistedExchangeRateEvidenceAsync(
        string transactionCurrency,
        string functionalCurrency,
        DateTime documentDate,
        decimal storedRate,
        FinanceSettings settings,
        CancellationToken cancellationToken)
    {
        if (string.Equals(transactionCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase))
        {
            if (RoundRate(storedRate) != 1m)
                throw new InvalidOperationException("A functional-currency supplier debit note must carry an exchange rate of 1.000000.");
            return 1m;
        }
        if (storedRate <= 0m)
            throw new InvalidOperationException("Foreign-currency supplier debit note is missing its approved exchange-rate snapshot.");

        var side = settings.DirectionalExchangeRatePolicyEnabled
            ? settings.ApInvoiceQuoteSide
            : ExchangeRateQuoteSide.Mid;
        var effectiveDate = documentDate.Date;
        var hasEvidence = await _db.ExchangeRates.AsNoTracking().AnyAsync(item =>
            item.TenantId == TenantId &&
            !item.IsDeleted &&
            item.BaseCurrencyCode == functionalCurrency &&
            item.TargetCurrencyCode == transactionCurrency &&
            item.RateType == ExchangeRateType.Daily &&
            item.QuoteSide == side &&
            item.Rate > 0m &&
            (item.ApprovalStatus == RateApprovalStatus.Approved ||
             item.ApprovalStatus == RateApprovalStatus.AutoApproved) &&
            item.EffectiveDate.Date <= effectiveDate &&
            (!item.EndDate.HasValue || item.EndDate.Value.Date >= effectiveDate) &&
            item.InverseRate == storedRate,
            cancellationToken);
        if (!hasEvidence)
            throw new InvalidOperationException(
                $"The stored {side} Daily rate for {transactionCurrency} to {functionalCurrency} on {effectiveDate:yyyy-MM-dd} has no approved rate-master evidence. Correct the draft through the controlled workflow before posting.");
        return storedRate;
    }

    private async Task ValidateLinkedInvoiceCreditLimitAsync(
        SupplierDebitNote note,
        VendorInvoice? invoice,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        if (invoice == null)
            return;

        var reservedByOtherNotes = await _db.SupplierDebitNotes.AsNoTracking()
            .Where(item =>
                item.TenantId == TenantId &&
                item.OriginalVendorInvoiceId == invoice.Id &&
                item.Id != excludedId &&
                !item.IsDeleted &&
                item.Status != SupplierDebitNoteStatus.Cancelled &&
                item.Status != SupplierDebitNoteStatus.Reversed)
            .SumAsync(item => (decimal?)item.TotalAmount, cancellationToken) ?? 0m;
        if (Round(reservedByOtherNotes + note.TotalAmount) > Round(invoice.TotalAmount) + 0.01m)
            throw new InvalidOperationException(
                $"Supplier debit notes would exceed the original total of invoice '{invoice.InvoiceNumber}'. Reduce the note or reverse/cancel an earlier supplier credit.");

        var reservedLines = await _db.SupplierDebitNoteLineItems.AsNoTracking()
            .Where(line =>
                line.TenantId == TenantId &&
                !line.IsDeleted &&
                line.OriginalVendorInvoiceLineItemId.HasValue &&
                line.SupplierDebitNote.OriginalVendorInvoiceId == invoice.Id &&
                line.SupplierDebitNote.Id != excludedId &&
                !line.SupplierDebitNote.IsDeleted &&
                line.SupplierDebitNote.Status != SupplierDebitNoteStatus.Cancelled &&
                line.SupplierDebitNote.Status != SupplierDebitNoteStatus.Reversed)
            .GroupBy(line => line.OriginalVendorInvoiceLineItemId!.Value)
            .Select(group => new
            {
                SourceLineId = group.Key,
                Quantity = group.Sum(item => item.Quantity),
                Discount = group.Sum(item => item.DiscountAmount),
                Tax = group.Sum(item => item.TaxAmount),
                Total = group.Sum(item => item.LineTotal)
            })
            .ToDictionaryAsync(item => item.SourceLineId, cancellationToken);
        var sourceLines = invoice.LineItems.Where(item => !item.IsDeleted).ToDictionary(item => item.Id);
        foreach (var line in note.LineItems.Where(item => !item.IsDeleted && item.OriginalVendorInvoiceLineItemId.HasValue))
        {
            if (!sourceLines.TryGetValue(line.OriginalVendorInvoiceLineItemId!.Value, out var sourceLine))
                throw SourceLineageUnavailable(null, "the referenced invoice line no longer exists");
            reservedLines.TryGetValue(sourceLine.Id, out var reserved);
            var reservedQuantity = reserved?.Quantity ?? 0m;
            var reservedDiscount = reserved?.Discount ?? 0m;
            var reservedTax = reserved?.Tax ?? 0m;
            var reservedTotal = reserved?.Total ?? 0m;
            var sourceNet = Round((sourceLine.Quantity * sourceLine.UnitPrice) - sourceLine.DiscountAmount);
            var sourceTotal = Round(sourceNet + sourceLine.TaxAmount);
            if (reservedQuantity + line.Quantity > sourceLine.Quantity + 0.0001m ||
                Round(reservedDiscount + line.DiscountAmount) > Round(sourceLine.DiscountAmount) + 0.01m ||
                Round(reservedTax + line.TaxAmount) > Round(sourceLine.TaxAmount) + 0.01m ||
                Round(reservedTotal + line.LineTotal) > sourceTotal + 0.01m)
                throw new InvalidOperationException(
                    $"Supplier debit notes would exceed the remaining quantity/value/tax capacity of source invoice line '{sourceLine.Description}'.");
        }
    }

    private async Task RecordAuditAsync(
        string eventType,
        SupplierDebitNote note,
        object? before = null,
        object? after = null,
        string? reason = null,
        Guid? workflowInstanceId = null,
        Guid? postingEventId = null,
        Guid? journalEntryId = null,
        CancellationToken cancellationToken = default)
    {
        await _audit.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = note.TenantId,
            SourceModule = "AP",
            SourceDocumentType = "SupplierDebitNote",
            SourceDocumentId = note.Id,
            Resource = AuditResource,
            ResourceId = note.Id.ToString(),
            WorkflowInstanceId = workflowInstanceId,
            PostingEventId = postingEventId,
            JournalEntryId = journalEntryId,
            BeforeValues = before,
            AfterValues = after,
            Reason = reason
        }, cancellationToken);
    }

    private async Task<SupplierDebitNoteDto> MapWithApprovalAsync(SupplierDebitNote note)
    {
        var dto = Map(note);
        if (note.Status is SupplierDebitNoteStatus.Draft or SupplierDebitNoteStatus.Rejected)
            dto.ApprovalRequired = await _workflow.HasActiveApprovalWorkflowAsync(WorkflowEntityType) ||
                await _workflow.HasActiveApprovalInstanceAsync(WorkflowEntityType, note.Id);
        return dto;
    }

    private SupplierDebitNoteDto Map(SupplierDebitNote note)
    {
        var effectiveApplications = EffectiveApplications(note.Applications);
        var applied = Round(effectiveApplications.Sum(item => item.ApplicationAmount) + note.DirectInvoiceAppliedAmount);
        var remaining = Round(Math.Max(note.TotalAmount - applied, 0m));
        return new SupplierDebitNoteDto
        {
            ApprovalRequired = note.ApprovalSource != "NoApprovalWorkflow",
            Id = note.Id,
            DebitNoteNumber = note.DebitNoteNumber,
            InventoryPurchaseReturnId = note.InventoryPurchaseReturnId,
            InventorySupplierReturnAccountingGroupId = note.InventorySupplierReturnAccountingGroupId,
            ReturnDispatchPostingEventId = note.ReturnDispatchPostingEventId,
            ReturnDispatchJournalEntryId = note.ReturnDispatchJournalEntryId,
            DirectInvoiceAppliedAmount = note.DirectInvoiceAppliedAmount,
            DirectInvoiceAppliedAt = note.DirectInvoiceAppliedAt,
            SupplierCreditNoteReference = note.SupplierCreditNoteReference,
            VendorId = note.VendorId,
            BusinessPartnerRoleId = note.BusinessPartnerRoleId,
            BusinessPartnerApProfileVersionId = note.BusinessPartnerApProfileVersionId,
            BusinessPartnerCode = note.BusinessPartnerCode,
            BusinessPartnerLegalName = note.BusinessPartnerLegalName,
            BusinessPartnerTaxIdentificationNumber = note.BusinessPartnerTaxIdentificationNumber,
            VendorName = note.Vendor?.PartnerName ?? string.Empty,
            SupplierReturnId = note.SupplierReturnId,
            OriginalVendorInvoiceId = note.OriginalVendorInvoiceId,
            OriginalVendorInvoiceNumber = note.OriginalVendorInvoice?.InvoiceNumber,
            DebitNoteDate = note.DebitNoteDate,
            Reason = note.Reason,
            Notes = note.Notes,
            CurrencyCode = note.CurrencyCode,
            ExchangeRate = note.ExchangeRate,
            SubTotal = note.SubTotal,
            TaxAmount = note.TaxAmount,
            DiscountAmount = note.DiscountAmount,
            TotalAmount = note.TotalAmount,
            BaseCurrencyAmount = note.BaseCurrencyAmount,
            AppliedAmount = applied,
            RemainingAmount = remaining,
            ApplicationStatus = applied <= 0m ? "Unapplied" : remaining <= 0m ? "FullyApplied" : "PartiallyApplied",
            JournalEntryId = note.JournalEntryId,
            PostingEventId = note.PostingEventId,
            WorkflowInstanceId = note.WorkflowInstanceId,
            SubmittedById = note.SubmittedById,
            SubmittedAt = note.SubmittedAt,
            ApprovedById = note.ApprovedById,
            ApprovedAt = note.ApprovedAt,
            RejectedById = note.RejectedById,
            RejectedAt = note.RejectedAt,
            RejectionReason = note.RejectionReason,
            ApprovalSource = note.ApprovalSource,
            ReversalJournalEntryId = note.ReversalJournalEntryId,
            ReversalPostingEventId = note.ReversalPostingEventId,
            ReversedAt = note.ReversedAt,
            ReversedById = note.ReversedById,
            ReversalReason = note.ReversalReason,
            Status = (int)note.Status,
            StatusName = note.Status.ToString(),
            CreatedAt = note.CreatedAt,
            RowVersion = note.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(note.RowVersion),
            LineItems = note.LineItems.Where(item => !item.IsDeleted).Select(item => new SupplierDebitNoteLineItemDto
            {
                Id = item.Id,
                OriginalVendorInvoiceLineItemId = item.OriginalVendorInvoiceLineItemId,
                OriginalFinancePurchaseOrderItemId = item.OriginalFinancePurchaseOrderItemId,
                GLAccountId = item.GLAccountId,
                ResolvedCreditAccountId = item.ResolvedCreditAccountId,
                OriginalAccountTransactionId = item.OriginalAccountTransactionId,
                LineItemType = item.LineItemType,
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TaxGroupId = item.TaxGroupId,
                TaxRate = item.TaxRate,
                TaxAmount = item.TaxAmount,
                DiscountPercentage = item.DiscountPercentage,
                DiscountAmount = item.DiscountAmount,
                LineTotal = item.LineTotal
            }).ToList(),
            Applications = note.Applications.Where(item => !item.IsDeleted).Select(MapApplication).ToList()
        };
    }

    internal static SupplierDebitNoteApplicationDto MapApplication(SupplierDebitNoteApplication item) => new()
    {
        Id = item.Id,
        SupplierDebitNoteId = item.SupplierDebitNoteId,
        DebitNoteNumber = item.SupplierDebitNote?.DebitNoteNumber ?? string.Empty,
        SupplierCreditNoteReference = item.SupplierDebitNote?.SupplierCreditNoteReference,
        VendorPaymentId = item.VendorPaymentId,
        PaymentNumber = item.VendorPayment?.PaymentNumber ?? string.Empty,
        VendorInvoiceId = item.VendorInvoiceId,
        InvoiceNumber = item.VendorInvoice?.InvoiceNumber ?? string.Empty,
        ApplicationAmount = item.ApplicationAmount,
        FunctionalAmount = item.FunctionalAmount,
        CurrencyCode = item.CurrencyCode,
        ExchangeRate = item.ExchangeRate,
        ApplicationDate = item.ApplicationDate,
        Notes = item.Notes,
        IsReversal = item.IsReversal,
        OriginalApplicationId = item.OriginalApplicationId,
        PaymentPostingEventId = item.PaymentPostingEventId,
        PaymentJournalEntryId = item.PaymentJournalEntryId,
        AppliedAt = item.AppliedAt
    };

    internal static List<SupplierDebitNoteApplication> EffectiveApplications(
        IEnumerable<SupplierDebitNoteApplication>? applications)
    {
        var live = applications?.Where(item => !item.IsDeleted).ToList() ?? new();
        var reversed = live.Where(item => item.IsReversal && item.OriginalApplicationId.HasValue)
            .Select(item => item.OriginalApplicationId!.Value)
            .ToHashSet();
        return live.Where(item => !item.IsReversal && !reversed.Contains(item.Id)).ToList();
    }

    private static object Snapshot(SupplierDebitNote note) => new
    {
        note.DebitNoteNumber,
        note.SupplierCreditNoteReference,
        note.VendorId,
        note.BusinessPartnerRoleId,
        note.BusinessPartnerApProfileVersionId,
        note.BusinessPartnerCode,
        note.OriginalVendorInvoiceId,
        note.DebitNoteDate,
        note.CurrencyCode,
        note.ExchangeRate,
        note.SubTotal,
        note.TaxAmount,
        note.DiscountAmount,
        note.TotalAmount,
        note.Status
    };

    private sealed record CanonicalApPartner(
        BusinessPartnerRole Role,
        BusinessPartnerApProfileVersion Profile);

    private async Task<CanonicalApPartner> ResolveCanonicalApPartnerAsync(
        BusinessPartner partner,
        Guid? requestedRoleId,
        DateTime accountingDate,
        CancellationToken cancellationToken)
    {
        var roles = await _db.Set<BusinessPartnerRole>()
            .Where(role =>
                role.TenantId == TenantId &&
                role.BusinessPartnerId == partner.Id &&
                !role.IsDeleted &&
                role.Status == BusinessPartnerRoleStatus.Active &&
                (role.RoleType == BusinessPartnerRoleType.Supplier ||
                 role.RoleType == BusinessPartnerRoleType.Contractor))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (requestedRoleId.HasValue)
            roles = roles.Where(role => role.Id == requestedRoleId.Value).ToList();
        else if (roles.Count > 1)
            throw new InvalidOperationException(
                "Select the Supplier or Contractor role for this AP debit note because the Business Partner has both roles.");

        var role = roles.SingleOrDefault();
        var profiles = role is null
            ? new List<BusinessPartnerApProfileVersion>()
            : await _db.Set<BusinessPartnerApProfileVersion>()
                .Where(profile =>
                    profile.TenantId == TenantId &&
                    profile.BusinessPartnerRoleId == role.Id &&
                    !profile.IsDeleted)
                .Include(profile => profile.WithholdingDefaults)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

        var readiness = BusinessPartnerFinanceProfilePolicy.ResolveAp(
            partner, role, profiles, accountingDate == default ? DateTime.UtcNow.Date : accountingDate.Date);
        if (!readiness.IsReady || readiness.ApProfile is null || role is null)
            throw new InvalidOperationException($"{readiness.Code}: {readiness.Message}");

        return new CanonicalApPartner(role, readiness.ApProfile);
    }

    private void ApplyConcurrencyToken(SupplierDebitNote note, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Supplier debit-note row version is required.");
        byte[] expected;
        try { expected = Convert.FromBase64String(token); }
        catch (FormatException) { throw new InvalidOperationException("Supplier debit-note row version is invalid."); }
        if (expected.Length > 0)
            _db.Entry(note).Property(item => item.RowVersion).OriginalValue = expected;
    }

    private static string RequiredText(string? value, string label) =>
        !string.IsNullOrWhiteSpace(value) ? value.Trim() : throw new InvalidOperationException($"{label} is required.");

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Append(string? current, string text) => string.IsNullOrWhiteSpace(current) ? text : $"{current}\n{text}";
    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static decimal RoundRate(decimal value) => decimal.Round(value, 6, MidpointRounding.AwayFromZero);
    private static string NormalizeCurrency(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback.Trim().ToUpperInvariant() : value.Trim().ToUpperInvariant();
    private static decimal Functional(decimal amount, string currency, string functionalCurrency, decimal rate) =>
        Round(string.Equals(currency, functionalCurrency, StringComparison.OrdinalIgnoreCase) ? amount : amount * rate);
}
