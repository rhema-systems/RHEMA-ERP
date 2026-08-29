using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ErpSystem.Core.Services.Procurement;

public class RfqService : IRfqService
{
    private const string QuoteSubmittedAuditAction = "RFQ_QUOTE_SUBMITTED";
    private const string QuoteRevisedAuditAction = "RFQ_QUOTE_REVISED";
    private const string QuoteLateRejectedAuditAction = "RFQ_QUOTE_LATE_REJECTED";

    private readonly IRequestForQuotationRepository _rfqRepository;
    private readonly IRequestForQuotationItemRepository _rfqItemRepository;
    private readonly IRequestForQuotationInvitationRepository _invitationRepository;
    private readonly IRequestForQuotationQuoteRepository _quoteRepository;
    private readonly IPurchaseRequisitionRepository _purchaseRequisitionRepository;
    private readonly IPurchaseRequisitionItemRepository _purchaseRequisitionItemRepository;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IRfqNotificationService _rfqNotificationService;
    private readonly IAppEventBus _appEventBus;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProcurementSourcingCaseService _sourcingCaseService;
    private readonly IProcurementRfqControlService _rfqControlService;
    private readonly IProcurementTenderDocumentControlService _tenderDocumentControlService;
    private readonly ISupplierValidationService _supplierValidation;
    private readonly IProcurementPurchaseOrderSourceService _purchaseOrderSources;
    private readonly ILogger<RfqService> _logger;

    public RfqService(
        IRequestForQuotationRepository rfqRepository,
        IRequestForQuotationItemRepository rfqItemRepository,
        IRequestForQuotationInvitationRepository invitationRepository,
        IRequestForQuotationQuoteRepository quoteRepository,
        IPurchaseRequisitionRepository purchaseRequisitionRepository,
        IPurchaseRequisitionItemRepository purchaseRequisitionItemRepository,
        IBusinessPartnerRepository businessPartnerRepository,
        IPurchaseOrderRepository purchaseOrderRepository,
        IRfqNotificationService rfqNotificationService,
        IAppEventBus appEventBus,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        IProcurementSourcingCaseService sourcingCaseService,
        IProcurementRfqControlService rfqControlService,
        IProcurementTenderDocumentControlService tenderDocumentControlService,
        ISupplierValidationService supplierValidation,
        IProcurementPurchaseOrderSourceService purchaseOrderSources,
        ILogger<RfqService> logger)
    {
        _rfqRepository = rfqRepository;
        _rfqItemRepository = rfqItemRepository;
        _invitationRepository = invitationRepository;
        _quoteRepository = quoteRepository;
        _purchaseRequisitionRepository = purchaseRequisitionRepository;
        _purchaseRequisitionItemRepository = purchaseRequisitionItemRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _rfqNotificationService = rfqNotificationService;
        _appEventBus = appEventBus;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _sourcingCaseService = sourcingCaseService;
        _rfqControlService = rfqControlService;
        _tenderDocumentControlService = tenderDocumentControlService;
        _supplierValidation = supplierValidation;
        _purchaseOrderSources = purchaseOrderSources;
        _logger = logger;
    }

    public async Task<PagedResult<RfqDto>> GetRfqsAsync(int page, int pageSize, string? search = null, string? status = null)
    {
        var result = await _rfqRepository.GetRfqsAsync(page, pageSize, search, status);

        return new PagedResult<RfqDto>
        {
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            Items = result.Items.Select(MapToDto).ToList()
        };
    }

    public async Task<RfqDetailDto?> GetRfqByIdAsync(Guid id)
    {
        var rfq = await _rfqRepository.GetWithDetailsAsync(id);
        if (rfq == null) return null;

        var detail = MapToDetailDto(rfq);
        var quotesAreOpen = UsesAdvancedSourcingControls(rfq)
            ? await _rfqControlService.AreQuotesOpenAsync(id)
            : rfq.SubmissionDeadline.HasValue &&
              EnsureUtc(rfq.SubmissionDeadline.Value) <= DateTime.UtcNow;
        detail.QuoteDetailsVisible = quotesAreOpen;
        if (!quotesAreOpen)
        {
            foreach (var quote in detail.Quotes)
            {
                quote.Notes = null;
                quote.Items.Clear();
                quote.TotalAmount = 0;
            }
        }
        return detail;
    }

    public async Task<RfqDetailDto> CreateRfqFromPurchaseRequisitionAsync(Guid purchaseRequisitionId, CreateRfqFromPurchaseRequisitionDto? dto = null)
    {
        dto ??= new CreateRfqFromPurchaseRequisitionDto();

        var pr = await _purchaseRequisitionRepository.GetRequisitionByIdAsync(purchaseRequisitionId)
            ?? throw new InvalidOperationException($"Purchase requisition with ID {purchaseRequisitionId} not found");

        if (!string.Equals(pr.Status, "Approved", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Purchase requisition must be approved to create an RFQ. Current status: {pr.Status}");

        var gate = await _sourcingCaseService.EnforceSourceEntryAsync(
            pr.Id, ProcurementMethodType.RequestForQuotation, "RequestForQuotation",
            $"new-rfq:{pr.RequisitionNumber}", Guid.NewGuid().ToString("N"));

        var prItems = (await _purchaseRequisitionItemRepository.GetItemsByRequisitionIdAsync(purchaseRequisitionId)).ToList();
        if (prItems.Count == 0)
            throw new InvalidOperationException("Purchase requisition has no items");

        var rfqNumber = await _rfqRepository.GenerateRfqNumberAsync();

        var rfq = new RequestForQuotation
        {
            TenantId = _currentUserProvider.TenantId,
            RfqNumber = rfqNumber,
            Title = $"RFQ for {pr.RequisitionNumber}",
            Description = pr.Justification,
            Status = "Draft",
            Currency = gate.CurrencyCode,
            EstimatedValue = gate.EstimatedValue,
            SourcePurchaseRequisitionId = pr.Id,
            SourcingReleaseId = gate.SourcingReleaseId,
            SourcingCaseId = gate.SourcingCaseId,
            ExternalRecipientEmails = string.IsNullOrWhiteSpace(dto.ExternalRecipientEmails) ? null : dto.ExternalRecipientEmails.Trim(),
            CreatedById = _currentUserProvider.UserId,
            CreatedAt = DateTime.UtcNow
        };

        await _rfqRepository.AddAsync(rfq);
        var line = 1;
        foreach (var item in prItems)
        {
            var rfqItem = new RequestForQuotationItem
            {
                TenantId = _currentUserProvider.TenantId,
                RfqId = rfq.Id,
                SourcePurchaseRequisitionItemId = item.Id,
                LineNumber = line++,
                InventoryItemId = item.InventoryItemId,
                ItemCode = item.InventoryItem?.ItemCode,
                Description = !string.IsNullOrWhiteSpace(item.ItemDescription)
                    ? item.ItemDescription
                    : (item.InventoryItem?.Name ?? "Item"),
                Quantity = item.Quantity,
                UnitOfMeasure = item.UnitOfMeasure ?? string.Empty,
                Specifications = item.Specifications,
                RequiredDeliveryDate = item.RequiredDate
            };

            await _rfqItemRepository.AddAsync(rfqItem);
        }

        await SyncInvitationsAsync(rfq.Id, dto.SupplierIds, isDraftSelection: true);
        if (gate.SourcingCaseId.HasValue)
            await _sourcingCaseService.RegisterSourceRequestAsync(gate.SourcingCaseId.Value, "RequestForQuotation",
                rfq.Id, rfq.RfqNumber, Guid.NewGuid().ToString("N"));

        await _unitOfWork.SaveChangesAsync();

        var created = await _rfqRepository.GetWithDetailsAsync(rfq.Id)
            ?? throw new InvalidOperationException("Failed to load created RFQ");

        return MapToDetailDto(created);
    }

    public async Task<RfqDetailDto> UpdateRfqAsync(Guid id, UpdateRfqDto dto)
    {
        var rfq = await _rfqRepository.GetWithDetailsAsync(id)
            ?? throw new InvalidOperationException($"RFQ with ID {id} not found");

        if (!string.Equals(rfq.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"RFQ issue terms are immutable after dispatch. Current status: {rfq.Status}");
        if (rfq.SourcePurchaseRequisitionId.HasValue)
        {
            var gate = await _sourcingCaseService.EnforceSourceEntryAsync(rfq.SourcePurchaseRequisitionId.Value,
                ProcurementMethodType.RequestForQuotation, "RequestForQuotation", rfq.RfqNumber, Guid.NewGuid().ToString("N"));
            EnsureSourceLineage(rfq.SourcingReleaseId, rfq.SourcingCaseId, gate);
            if (dto.EstimatedValue.HasValue && dto.EstimatedValue.Value != gate.EstimatedValue)
                throw new ProcurementRequisitionSourcingValidationException("RFQ_CASE_VALUE_MISMATCH", "RFQ value must remain equal to the locked sourcing-case value.");
            if (!string.IsNullOrWhiteSpace(dto.Currency) && !string.Equals(dto.Currency.Trim(), gate.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                throw new ProcurementRequisitionSourcingValidationException("RFQ_CASE_CURRENCY_MISMATCH", "RFQ currency must remain equal to the locked sourcing-case currency.");
        }

        if (!string.IsNullOrWhiteSpace(dto.Title))
            rfq.Title = dto.Title.Trim();

        if (dto.Description != null)
            rfq.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();

        if (dto.SubmissionDeadline.HasValue)
            rfq.SubmissionDeadline = dto.SubmissionDeadline;

        if (!string.IsNullOrWhiteSpace(dto.Currency))
            rfq.Currency = dto.Currency.Trim();

        if (dto.EstimatedValue.HasValue)
            rfq.EstimatedValue = dto.EstimatedValue.Value;

        if (dto.ExternalRecipientEmails != null)
            rfq.ExternalRecipientEmails = string.IsNullOrWhiteSpace(dto.ExternalRecipientEmails) ? null : dto.ExternalRecipientEmails.Trim();

        await _rfqRepository.UpdateAsync(rfq);

        if (dto.SupplierIds != null)
        {
            await SyncInvitationsAsync(rfq.Id, dto.SupplierIds, isDraftSelection: true);
        }

        await _unitOfWork.SaveChangesAsync();

        var updated = await _rfqRepository.GetWithDetailsAsync(rfq.Id)
            ?? throw new InvalidOperationException("Failed to load updated RFQ");

        return MapToDetailDto(updated);
    }

    public async Task SendRfqAsync(Guid id, SendRfqDto dto)
    {
        var rfq = await _rfqRepository.GetWithDetailsAsync(id)
            ?? throw new InvalidOperationException($"RFQ with ID {id} not found");

        if (!string.Equals(rfq.Status, "Draft", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Only a Draft RFQ can be dispatched. Current status: {rfq.Status}");
        if (!rfq.SourcePurchaseRequisitionId.HasValue)
            throw new ProcurementRequisitionSourcingValidationException(
                "RFQ_SOURCE_REQUISITION_REQUIRED", "An RFQ cannot be dispatched without a source purchase requisition and current sourcing release.");
        var gate = await _sourcingCaseService.EnforceSourceEntryAsync(
            rfq.SourcePurchaseRequisitionId.Value, ProcurementMethodType.RequestForQuotation,
            "RequestForQuotation", rfq.RfqNumber, Guid.NewGuid().ToString("N"));
        EnsureSourceLineage(rfq.SourcingReleaseId, rfq.SourcingCaseId, gate);
        rfq.SourcingReleaseId = gate.SourcingReleaseId;
        rfq.SourcingCaseId = gate.SourcingCaseId;
        if (rfq.EstimatedValue != gate.EstimatedValue || !string.Equals(rfq.Currency, gate.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            throw new ProcurementRequisitionSourcingValidationException("RFQ_CASE_VALUE_MISMATCH", "RFQ value and currency no longer match the locked sourcing case.");

        var supplierIds = (dto.SupplierIds ?? new List<Guid>())
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        var externalEmails = string.IsNullOrWhiteSpace(dto.ExternalRecipientEmails)
            ? null
            : dto.ExternalRecipientEmails.Trim();

        if (supplierIds.Count == 0 && string.IsNullOrWhiteSpace(externalEmails))
            throw new InvalidOperationException("Please select at least one supplier and/or provide external recipient email(s).");

        var correlationId = Guid.NewGuid().ToString("N");
        if (UsesAdvancedSourcingControls(rfq))
        {
            await _rfqControlService.EnsureDispatchReadyAsync(rfq.Id, supplierIds, correlationId);
            await _tenderDocumentControlService.EnsureDispatchReadyAsync(
                ProcurementTenderDocumentSourceType.RequestForQuotation, rfq.Id,
                supplierIds, ParseEmails(externalEmails), correlationId);
        }
        else
        {
            ValidateReleaseOnlyDispatch(rfq, supplierIds, externalEmails, DateTime.UtcNow);
            foreach (var supplierId in supplierIds)
            {
                var eligibility = await _supplierValidation.ValidateForRfqAsync(supplierId);
                if (!eligibility.IsValid)
                    throw new SupplierEligibilityException(
                        eligibility.ValidationCode,
                        $"RFQ supplier is not eligible: {string.Join("; ", eligibility.Errors)}",
                        eligibility);
            }
        }

        rfq.ExternalRecipientEmails = externalEmails;
        rfq.Status = "Sent";
        rfq.SentAt = DateTime.UtcNow;

        await _rfqRepository.UpdateAsync(rfq);

        await SyncInvitationsAsync(rfq.Id, supplierIds, isDraftSelection: false);

        await _unitOfWork.SaveChangesAsync();

        // Send notifications after data is persisted (so portal users can open it).
        await _rfqNotificationService.SendRfqSentNotificationAsync(rfq.Id, supplierIds, ParseEmails(externalEmails));
    }

    public async Task<List<RfqDto>> GetSupplierRfqsAsync(Guid businessPartnerId, Guid tenantId)
    {
        var invitations = await _invitationRepository.GetForSupplierAsync(businessPartnerId, tenantId);

        return invitations
            .Select(i => i.Rfq)
            .Where(r => r != null && !r.IsDeleted)
            .OrderByDescending(r => r.SentAt ?? r.CreatedAt)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<RfqDetailDto?> GetSupplierRfqDetailAsync(Guid rfqId, Guid businessPartnerId, Guid tenantId)
    {
        var invitations = await _invitationRepository.GetForSupplierAsync(businessPartnerId, tenantId);
        var allowed = invitations.Any(i => i.RfqId == rfqId);
        if (!allowed) return null;

        var rfq = await _rfqRepository.GetWithDetailsAsync(rfqId);
        if (rfq == null) return null;

        // Do NOT expose other suppliers' quotes in the supplier portal.
        var detail = MapToDetailDto(rfq);
        detail.Quotes = detail.Quotes
            .Where(q => q.BusinessPartnerId == businessPartnerId)
            .ToList();
        detail.QuoteCount = detail.Quotes.Count;
        detail.QuoteDetailsVisible = true;

        foreach (var supplierQuote in detail.Quotes)
        {
            supplierQuote.History = await GetQuoteHistoryAsync(supplierQuote, tenantId);
            supplierQuote.RevisionNumber = supplierQuote.History.Count == 0
                ? (supplierQuote.SubmittedAt.HasValue ? 1 : 0)
                : supplierQuote.History.Max(entry => entry.RevisionNumber);
        }

        return detail;
    }

    public async Task<RfqQuoteDto> SubmitQuoteAsync(Guid rfqId, Guid businessPartnerId, Guid submittedByUserId, Guid tenantId, SubmitRfqQuoteDto dto)
    {
        var rfq = await _rfqRepository.GetWithDetailsAsync(rfqId)
            ?? throw new InvalidOperationException($"RFQ with ID {rfqId} not found");

        if (!string.Equals(rfq.Status, "Sent", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"RFQ is not open for quotations. Current status: {rfq.Status}");

        var invitations = await _invitationRepository.GetForSupplierAsync(businessPartnerId, tenantId);
        var invitation = invitations.FirstOrDefault(i => i.RfqId == rfqId);
        if (invitation == null)
            throw new InvalidOperationException("You are not invited to this RFQ");
        if (UsesAdvancedSourcingControls(rfq))
        {
            await _tenderDocumentControlService.EnsureSubmissionReadyAsync(
                ProcurementTenderDocumentSourceType.RequestForQuotation, rfq.Id,
                businessPartnerId, Guid.NewGuid().ToString("N"));
        }

        var rfqItems = (rfq.Items ?? new List<RequestForQuotationItem>())
            .Where(i => !i.IsDeleted)
            .OrderBy(i => i.LineNumber)
            .ToList();

        if (rfqItems.Count == 0)
            throw new InvalidOperationException("RFQ has no items");

        var priceByItem = (dto.Items ?? new List<SubmitRfqQuoteItemDto>())
            .Where(x => x.RfqItemId != Guid.Empty)
            .GroupBy(x => x.RfqItemId)
            .ToDictionary(g => g.Key, g => g.Last().UnitPrice);

        foreach (var item in rfqItems)
        {
            if (!priceByItem.ContainsKey(item.Id))
                throw new InvalidOperationException($"Missing unit price for line {item.LineNumber}: {item.Description}");
        }

        RequestForQuotationQuote? quote = null;
        var revisionNumber = 0;
        var wasRevision = false;

        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                quote = await _quoteRepository.GetByRfqAndSupplierAsync(rfqId, businessPartnerId, tenantId);

                var receivedAtUtc = DateTime.UtcNow;
                wasRevision = quote != null &&
                    string.Equals(quote.Status, "Submitted", StringComparison.OrdinalIgnoreCase);

                if (quote != null && string.Equals(quote.Status, "LateRejected", StringComparison.OrdinalIgnoreCase))
                    throw new ProcurementRfqControlConflictException(
                        "RFQ_QUOTE_REVISION_CLOSED",
                        "This quotation was received after the deadline and cannot be revised.");

                if (wasRevision)
                    EnsureQuoteRevisionOpen(rfq.SubmissionDeadline, receivedAtUtc);

                var previousSnapshot = wasRevision && quote != null
                    ? CreateQuoteSnapshot(quote, 0)
                    : null;

                var auditRepository = _unitOfWork.Repository<AuditLog>();
                var recordedRevisionActions = quote == null
                    ? new List<string>()
                    : await auditRepository.GetQueryable(audit =>
                            audit.TenantId == tenantId &&
                            audit.Resource == nameof(RequestForQuotationQuote) &&
                            audit.ResourceId == quote.Id.ToString() &&
                            (audit.Action == QuoteSubmittedAuditAction ||
                             audit.Action == QuoteRevisedAuditAction ||
                             audit.Action == QuoteLateRejectedAuditAction))
                        .OrderBy(audit => audit.Timestamp)
                        .Select(audit => audit.Action)
                        .ToListAsync();
                var hasRecordedInitialSubmission = recordedRevisionActions.Contains(QuoteSubmittedAuditAction);
                revisionNumber = wasRevision
                    ? recordedRevisionActions.Count + (hasRecordedInitialSubmission ? 1 : 2)
                    : recordedRevisionActions.Count + 1;

                if (previousSnapshot != null)
                    previousSnapshot.RevisionNumber = Math.Max(1, revisionNumber - 1);

                if (quote == null)
                {
                    quote = new RequestForQuotationQuote
                    {
                        TenantId = tenantId,
                        RfqId = rfqId,
                        BusinessPartnerId = businessPartnerId,
                        Status = "Draft",
                        CreatedAt = receivedAtUtc,
                        CreatedBy = _currentUserProvider.Username,
                        CreatedById = submittedByUserId
                    };
                    await _quoteRepository.AddAsync(quote);
                    await _unitOfWork.SaveChangesAsync(); // persist Quote Id
                }

                // Retain the same quote and quote-line identities. A pre-deadline revision changes
                // their current values while the immutable audit snapshot below preserves every version.
                var quoteItemRepo = _unitOfWork.Repository<RequestForQuotationQuoteItem>();
                var existingItems = quote.Items?.Where(i => !i.IsDeleted).ToList() ?? new List<RequestForQuotationQuoteItem>();
                var existingByRfqItemId = existingItems
                    .GroupBy(item => item.RfqItemId)
                    .ToDictionary(group => group.Key, group => group.First());

                foreach (var item in rfqItems)
                {
                    var unitPrice = priceByItem[item.Id];
                    var lineTotal = decimal.Round(unitPrice * item.Quantity, 2, MidpointRounding.AwayFromZero);

                    if (existingByRfqItemId.TryGetValue(item.Id, out var quoteItem))
                    {
                        quoteItem.UnitPrice = unitPrice;
                        quoteItem.LineTotal = lineTotal;
                        quoteItem.UpdatedAt = receivedAtUtc;
                        quoteItem.UpdatedBy = _currentUserProvider.Username;
                        quoteItem.LastModifiedById = submittedByUserId;
                        await quoteItemRepo.UpdateAsync(quoteItem);
                    }
                    else
                    {
                        quoteItem = new RequestForQuotationQuoteItem
                        {
                            TenantId = tenantId,
                            QuoteId = quote.Id,
                            RfqItemId = item.Id,
                            UnitPrice = unitPrice,
                            LineTotal = lineTotal,
                            CreatedAt = receivedAtUtc,
                            CreatedBy = _currentUserProvider.Username,
                            CreatedById = submittedByUserId
                        };
                        await quoteItemRepo.AddAsync(quoteItem);
                    }
                }

                var currentRfqItemIds = rfqItems.Select(item => item.Id).ToHashSet();
                foreach (var obsoleteItem in existingItems.Where(item => !currentRfqItemIds.Contains(item.RfqItemId)))
                {
                    obsoleteItem.IsDeleted = true;
                    obsoleteItem.DeletedAt = receivedAtUtc;
                    obsoleteItem.DeletedBy = _currentUserProvider.Username;
                    await quoteItemRepo.UpdateAsync(obsoleteItem);
                }

                quote.Notes = dto.Notes;
                var isLate = rfq.SubmissionDeadline.HasValue && receivedAtUtc > rfq.SubmissionDeadline.Value.ToUniversalTime();
                quote.Status = isLate ? "LateRejected" : "Submitted";
                quote.SubmittedAt = receivedAtUtc;
                quote.SubmittedByUserId = submittedByUserId;
                quote.UpdatedAt = receivedAtUtc;
                quote.UpdatedBy = _currentUserProvider.Username;
                quote.LastModifiedById = submittedByUserId;

                await _quoteRepository.UpdateAsync(quote);

                invitation.Status = isLate ? "LateRejected" : "Responded";
                invitation.RespondedAt = receivedAtUtc;
                await _invitationRepository.UpdateAsync(invitation);

                var currentSnapshot = CreateQuoteSnapshot(
                    quote,
                    revisionNumber,
                    rfqItems.Select(item => new QuoteAuditItemSnapshot
                    {
                        RfqItemId = item.Id,
                        LineNumber = item.LineNumber,
                        Description = item.Description,
                        Quantity = item.Quantity,
                        UnitOfMeasure = item.UnitOfMeasure,
                        UnitPrice = priceByItem[item.Id],
                        LineTotal = decimal.Round(
                            priceByItem[item.Id] * item.Quantity,
                            2,
                            MidpointRounding.AwayFromZero)
                    }).ToList());

                await auditRepository.AddAsync(new AuditLog
                {
                    TenantId = tenantId,
                    UserId = submittedByUserId,
                    Username = string.IsNullOrWhiteSpace(_currentUserProvider.Username)
                        ? "Supplier user"
                        : _currentUserProvider.Username,
                    Action = isLate
                        ? QuoteLateRejectedAuditAction
                        : wasRevision ? QuoteRevisedAuditAction : QuoteSubmittedAuditAction,
                    Resource = nameof(RequestForQuotationQuote),
                    ResourceId = quote.Id.ToString(),
                    OldValues = previousSnapshot == null ? null : JsonSerializer.Serialize(previousSnapshot),
                    NewValues = JsonSerializer.Serialize(currentSnapshot),
                    IpAddress = "SupplierPortal",
                    UserAgent = "Supplier RFQ quotation submission",
                    Timestamp = receivedAtUtc,
                    CreatedAt = receivedAtUtc,
                    CreatedBy = _currentUserProvider.Username,
                    CreatedById = submittedByUserId
                });

                await _unitOfWork.SaveChangesAsync();
                if (UsesAdvancedSourcingControls(rfq))
                    await _rfqControlService.RecordReceiptAsync(
                        rfq.Id, quote.Id, receivedAtUtc, Guid.NewGuid().ToString("N"));
                await _unitOfWork.CommitAsync();
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        });

        if (quote == null)
            throw new InvalidOperationException("Failed to load submitted quote");

        var refreshed = await _rfqRepository.GetWithDetailsAsync(rfqId)
            ?? throw new InvalidOperationException("Failed to load RFQ after quote submission");

        var submitted = refreshed.Quotes.FirstOrDefault(q => q.Id == quote.Id);
        if (submitted == null)
            throw new InvalidOperationException("Failed to load submitted quote");

        // Notify internal procurement users (best-effort; do not fail quote submission if notification fails).
        try
        {
            await _rfqNotificationService.SendRfqQuoteSubmittedNotificationAsync(rfqId, quote.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send RFQ quote submitted notification for RFQ {RfqId} Quote {QuoteId}", rfqId, quote.Id);
        }

        // Publish generic entity activity events to drive admin-configurable notification topics (best-effort).
        try
        {
            var internalData = new Dictionary<string, object>
            {
                ["RfqId"] = rfqId,
                ["RfqNumber"] = rfq.RfqNumber,
                ["QuoteId"] = quote.Id,
                ["Sealed"] = true,
                ["RevisionNumber"] = revisionNumber,
            };
            var supplierData = new Dictionary<string, object>(internalData)
            {
                ["BusinessPartnerId"] = businessPartnerId,
            };

            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = tenantId,
                EntityType = "RFQ",
                Activity = wasRevision ? "QuoteRevised" : "QuoteSubmitted",
                Audience = "Internal",
                EntityId = rfqId,
                TriggeredByUserId = submittedByUserId,
                Data = internalData
            });

            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = tenantId,
                EntityType = "RFQ",
                Activity = wasRevision ? "QuoteRevised" : "QuoteSubmitted",
                Audience = "Supplier",
                EntityId = rfqId,
                TriggeredByUserId = submittedByUserId,
                Data = supplierData
            });

            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = tenantId,
                EntityType = "SupplierQuote",
                Activity = wasRevision ? "Revised" : "Submitted",
                Audience = "Internal",
                EntityId = quote.Id,
                TriggeredByUserId = submittedByUserId,
                Data = internalData
            });

            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = tenantId,
                EntityType = "SupplierQuote",
                Activity = wasRevision ? "Revised" : "Submitted",
                Audience = "Supplier",
                EntityId = quote.Id,
                TriggeredByUserId = submittedByUserId,
                Data = supplierData
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish quotation activity event for RFQ {RfqId} Quote {QuoteId}", rfqId, quote.Id);
        }

        var awardedQuoteIdByItemId = (refreshed.AwardLines ?? new List<RequestForQuotationAwardLine>())
            .Where(a => !a.IsDeleted)
            .GroupBy(a => a.RfqItemId)
            .ToDictionary(g => g.Key, g => g.Last().QuoteId);

        var submittedDto = MapToQuoteDto(submitted, awardedQuoteIdByItemId);
        submittedDto.History = await GetQuoteHistoryAsync(submittedDto, tenantId);
        submittedDto.RevisionNumber = revisionNumber;
        return submittedDto;
    }

    public async Task<CreatePurchaseOrdersFromRfqResponseDto> CreatePurchaseOrdersFromAwardAsync(Guid rfqId, CreatePurchaseOrdersFromRfqDto dto)
    {
        var awardCorrelationId = Guid.NewGuid().ToString("N");
        var sourceRfq = await _rfqRepository.GetWithDetailsAsync(rfqId)
            ?? throw new InvalidOperationException($"RFQ with ID {rfqId} not found");
        var usesAdvancedSourcingControls = UsesAdvancedSourcingControls(sourceRfq);
        if (usesAdvancedSourcingControls)
        {
            dto = await _rfqControlService.GetApprovedAwardAsync(rfqId, awardCorrelationId);
        }
        else
        {
            ValidateReleaseOnlyAward(sourceRfq, dto, DateTime.UtcNow);
        }

        CreatePurchaseOrdersFromRfqResponseDto? response = null;

        await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var rfq = await _rfqRepository.GetWithDetailsAsync(rfqId)
                    ?? throw new InvalidOperationException($"RFQ with ID {rfqId} not found");

                if (UsesAdvancedSourcingControls(rfq) != usesAdvancedSourcingControls)
                    throw new ProcurementRequisitionSourcingValidationException(
                        "RFQ_SOURCE_LINEAGE_CHANGED",
                        "The RFQ sourcing lineage changed while the award was being prepared. Refresh and retry.");

                if (rfq.Status is "Closed" or "Awarded" or "Cancelled" or "Rejected")
                    throw new InvalidOperationException($"RFQ cannot be awarded in current status: {rfq.Status}");

                if (usesAdvancedSourcingControls)
                {
                    if (!string.Equals(rfq.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"RFQ evaluation must be approved before awarding. Current status: {rfq.Status}");
                }
                else
                {
                    ValidateReleaseOnlyAward(rfq, dto, DateTime.UtcNow);
                }

                // Prevent accidental double-awards (creates duplicate POs).
                var hasExistingPo = await _unitOfWork.Repository<PurchaseOrder>()
                    .ExistsAsync(po => po.TenantId == _currentUserProvider.TenantId && po.SourceRfqId == rfqId);
                if (hasExistingPo)
                    throw new InvalidOperationException("This RFQ already has purchase order(s) created from it.");

                var rfqItems = (rfq.Items ?? new List<RequestForQuotationItem>())
                    .Where(i => !i.IsDeleted)
                    .OrderBy(i => i.LineNumber)
                    .ToList();

                if (rfqItems.Count == 0)
                    throw new InvalidOperationException("RFQ has no items");

                var submittedQuotes = (rfq.Quotes ?? new List<RequestForQuotationQuote>())
                    .Where(q => !q.IsDeleted && string.Equals(q.Status, "Submitted", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (submittedQuotes.Count == 0)
                    throw new InvalidOperationException("RFQ has no submitted quotes to award.");

                var mode = (dto.Mode ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(mode)) mode = "WinnerTakesAll";

                // Build selections: one quote line per RFQ item.
                var selections = new List<(RequestForQuotationItem Item, RequestForQuotationQuote Quote, RequestForQuotationQuoteItem QuoteItem)>();
                var awardReasonByItemId = new Dictionary<Guid, string?>();

                if (string.Equals(mode, "WinnerTakesAll", StringComparison.OrdinalIgnoreCase))
                {
                    if (!dto.QuoteId.HasValue || dto.QuoteId.Value == Guid.Empty)
                        throw new InvalidOperationException("QuoteId is required for WinnerTakesAll awarding.");

                    var quote = submittedQuotes.FirstOrDefault(q => q.Id == dto.QuoteId.Value)
                        ?? throw new InvalidOperationException("Selected quote was not found or not submitted.");

                    foreach (var item in rfqItems)
                    {
                        var quoteItem = (quote.Items ?? new List<RequestForQuotationQuoteItem>())
                            .FirstOrDefault(i => !i.IsDeleted && i.RfqItemId == item.Id);

                        if (quoteItem == null)
                            throw new InvalidOperationException($"Selected quote is missing pricing for RFQ line {item.LineNumber}: {item.Description}");

                        selections.Add((item, quote, quoteItem));
                    }
                }
                else if (string.Equals(mode, "SplitAward", StringComparison.OrdinalIgnoreCase))
                {
                    var lines = (dto.Lines ?? new List<RfqSplitAwardLineDto>())
                        .Where(l => l.RfqItemId != Guid.Empty && l.QuoteId != Guid.Empty)
                        .ToList();

                    // Must select a quote for every RFQ item.
                    var distinctItemIds = lines.Select(l => l.RfqItemId).Distinct().ToList();
                    if (distinctItemIds.Count != rfqItems.Count)
                        throw new InvalidOperationException("Split award requires selecting a supplier quote for every RFQ item.");

                    var selectedLineByItem = lines
                        .GroupBy(l => l.RfqItemId)
                        .ToDictionary(g => g.Key, g => g.Last());

                    foreach (var item in rfqItems)
                    {
                        if (!selectedLineByItem.TryGetValue(item.Id, out var selectedLine))
                            throw new InvalidOperationException($"Split award selection missing for RFQ line {item.LineNumber}: {item.Description}");

                        var quoteId = selectedLine.QuoteId;
                        var quote = submittedQuotes.FirstOrDefault(q => q.Id == quoteId)
                            ?? throw new InvalidOperationException($"Selected quote {quoteId} was not found or not submitted.");

                        var quoteItem = (quote.Items ?? new List<RequestForQuotationQuoteItem>())
                            .FirstOrDefault(i => !i.IsDeleted && i.RfqItemId == item.Id);

                        if (quoteItem == null)
                            throw new InvalidOperationException($"Selected quote is missing pricing for RFQ line {item.LineNumber}: {item.Description}");

                        awardReasonByItemId[item.Id] = string.IsNullOrWhiteSpace(selectedLine.AwardReason)
                            ? null
                            : selectedLine.AwardReason.Trim();

                        selections.Add((item, quote, quoteItem));
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Unknown award mode: {dto.Mode}");
                }

                response = new CreatePurchaseOrdersFromRfqResponseDto();

                // Load PR number if RFQ was generated from PR.
                string? sourcePrNumber = null;
                if (rfq.SourcePurchaseRequisitionId.HasValue && rfq.SourcePurchaseRequisitionId.Value != Guid.Empty)
                {
                    var pr = await _purchaseRequisitionRepository.GetRequisitionByIdAsync(rfq.SourcePurchaseRequisitionId.Value);
                    sourcePrNumber = pr?.RequisitionNumber;
                }

                // Group by supplier -> one PO per supplier.
                var groups = selections
                    .GroupBy(x => x.Quote.BusinessPartnerId)
                    .ToList();

                foreach (var group in groups)
                {
                    var eligibility = await _supplierValidation.EvaluateEligibilityAsync(
                        new SupplierEligibilityEvaluationRequest
                        {
                            BusinessPartnerId = group.Key,
                            Boundary = SupplierEligibilityBoundary.Award,
                            SourceType = "RequestForQuotation",
                            SourceId = rfq.Id,
                            SourceReference = rfq.RfqNumber,
                            CorrelationId = awardCorrelationId
                        });
                    if (!eligibility.IsValid)
                        throw new SupplierEligibilityException(
                            eligibility.ValidationCode,
                            $"RFQ award supplier is ineligible: {string.Join("; ", eligibility.Errors)}",
                            eligibility);
                }

                // Persist the final RFQ state and authoritative award lines before
                // resolving the immutable PO snapshot. Revalidation must hash the
                // same final status and commercial terms as creation.
                var awardRepo = _unitOfWork.Repository<RequestForQuotationAwardLine>();
                var awardedAtUtc = DateTime.UtcNow;
                foreach (var (item, quote, quoteItem) in selections)
                {
                    await awardRepo.AddAsync(new RequestForQuotationAwardLine
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _currentUserProvider.TenantId,
                        RfqId = rfq.Id,
                        RfqItemId = item.Id,
                        BusinessPartnerId = quote.BusinessPartnerId,
                        QuoteId = quote.Id,
                        QuoteItemId = quoteItem.Id,
                        UnitPrice = quoteItem.UnitPrice,
                        LineTotal = quoteItem.LineTotal,
                        AwardReason = awardReasonByItemId.TryGetValue(
                            item.Id,
                            out var reason)
                            ? reason
                            : null,
                        CreatedAt = awardedAtUtc,
                        CreatedById = _currentUserProvider.UserId
                    });
                }
                ApplyAwardLineage(
                    rfq,
                    groups.Select(group => group.Key),
                    awardedAtUtc,
                    _currentUserProvider.UserId);
                await _rfqRepository.UpdateAsync(rfq);
                await _unitOfWork.SaveChangesAsync();

                // Create PO headers first (save immediately) to avoid:
                // - OrderNumber duplicate race
                // - FK failure when PO items are inserted but PO header insert fails
                var poItemRepo = _unitOfWork.Repository<PurchaseOrderItem>();

                var createdPos = new Dictionary<Guid, PurchaseOrder>(); // supplierId -> PO
                foreach (var group in groups)
                {
                    var supplierId = group.Key;
                    var first = group.First();
                    var supplierName = first.Quote.BusinessPartner?.PartnerName ?? string.Empty;
                    var approvedSource = await _purchaseOrderSources.ResolveAsync(
                        ProcurementPurchaseOrderSourceType.RfqAward,
                        rfq.Id,
                        supplierId,
                        awardCorrelationId);

                    var subTotal = group.Sum(x => x.QuoteItem.LineTotal);
                    var sourceOrderLines = group.Select(line =>
                        new ProcurementPurchaseOrderSourceOrderLine
                        {
                            InventoryItemId = line.Item.InventoryItemId,
                            ItemDescription = line.Item.Description,
                            OrderedQuantity = line.Item.Quantity,
                            UnitOfMeasure =
                                line.Item.UnitOfMeasure ?? "EA",
                            UnitPrice = line.QuoteItem.UnitPrice
                        }).ToList();
                    await _purchaseOrderSources.ValidateOrderAsync(
                        approvedSource,
                        sourceOrderLines,
                        subTotal,
                        approvedSource.CurrencyCode,
                        awardCorrelationId);

                    var po = new PurchaseOrder
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _currentUserProvider.TenantId,
                        OrderNumber = await _purchaseOrderRepository.GenerateOrderNumberAsync(),
                        BusinessPartnerId = supplierId,
                        OrderDate = DateTime.UtcNow,
                        Status = "Draft",
                        RequestedById =
                            approvedSource.PurchaseRequisitionRequestedById,

                        SubTotal = subTotal,
                        TaxAmount = 0,
                        ShippingCost = 0,
                        MiscellaneousCost = 0,
                        TotalAdditionalCost = 0,
                        DiscountAmount = 0,
                        TotalAmount = subTotal,

                        Currency = approvedSource.CurrencyCode,
                        ExchangeRate = 1,

                        Notes = $"Created from RFQ {rfq.RfqNumber}.",

                        SourceRequisitionId = rfq.SourcePurchaseRequisitionId,
                        SourceRequisitionNumber = sourcePrNumber,
                        SourceRfqId = rfq.Id,
                        SourceRfqNumber = rfq.RfqNumber,
                        SourceRfqAwardType = string.Equals(mode, "SplitAward", StringComparison.OrdinalIgnoreCase) ? "SplitAward" : "WinnerTakesAll",
                        SourceRfqQuoteId = string.Equals(mode, "WinnerTakesAll", StringComparison.OrdinalIgnoreCase) ? first.Quote.Id : null,

                        CreatedAt = DateTime.UtcNow,
                        CreatedById = _currentUserProvider.UserId
                    };
                    _purchaseOrderSources.Apply(po, approvedSource);
                    await _purchaseOrderSources.ReserveAsync(
                        approvedSource,
                        sourceOrderLines,
                        subTotal,
                        approvedSource.CurrencyCode,
                        po.Id,
                        awardCorrelationId);

                    await _purchaseOrderRepository.CreatePurchaseOrderAsync(po);
                    await SaveChangesWithPurchaseOrderNumberRetryAsync(po);

                    createdPos[supplierId] = po;
                    response.PurchaseOrders.Add(new CreatedPurchaseOrderFromRfqDto
                    {
                        PurchaseOrderId = po.Id,
                        OrderNumber = po.OrderNumber,
                        BusinessPartnerId = supplierId,
                        BusinessPartnerName = supplierName,
                        TotalAmount = po.TotalAmount
                    });
                }

                // Create PO items + PR item links from the persisted award lines.
                foreach (var (item, quote, quoteItem) in selections)
                {
                    var supplierId = quote.BusinessPartnerId;
                    var po = createdPos[supplierId];

                    // PO item
                    var orderedQty = item.Quantity;
                    var poItem = new PurchaseOrderItem
                    {
                        TenantId = _currentUserProvider.TenantId,
                        PurchaseOrderId = po.Id,
                        InventoryItemId = item.InventoryItemId,
                        ItemDescription = item.Description,
                        OrderedQuantity = orderedQty,
                        ReceivedQuantity = 0,
                        RemainingQuantity = orderedQty,
                        UnitOfMeasure = item.UnitOfMeasure ?? "EA",
                        UnitPrice = quoteItem.UnitPrice,
                        LineTotal = quoteItem.LineTotal,

                        SourceRfqItemId = item.Id,
                        SourceRfqQuoteId = quote.Id,
                        SourceRfqQuoteItemId = quoteItem.Id,

                        CreatedAt = DateTime.UtcNow,
                        CreatedById = _currentUserProvider.UserId
                    };
                    await poItemRepo.AddAsync(poItem);

                    // Update source PR line to point to the created PO (if available).
                    if (item.SourcePurchaseRequisitionItemId.HasValue && item.SourcePurchaseRequisitionItemId.Value != Guid.Empty)
                    {
                        var prItem = await _purchaseRequisitionItemRepository.GetByIdAsync(item.SourcePurchaseRequisitionItemId.Value);
                        if (prItem != null)
                        {
                            prItem.PurchaseOrderId = po.Id;
                            prItem.Status = "Ordered";
                            prItem.UpdatedAt = DateTime.UtcNow;
                            prItem.LastModifiedById = _currentUserProvider.UserId;
                            await _purchaseRequisitionItemRepository.UpdateAsync(prItem);
                        }
                    }
                }

                foreach (var purchaseOrder in createdPos.Values)
                {
                    await _purchaseOrderSources.RecordBoundAsync(
                        purchaseOrder,
                        "RfqAwardPurchaseOrderCreated",
                        awardCorrelationId);
                }

                await _unitOfWork.CommitAsync();
            }
            catch (DbUpdateException ex) when (IsRfqStatutoryLifecycleViolation(ex))
            {
                await _unitOfWork.RollbackAsync();
                throw new ProcurementRequisitionSourcingValidationException(
                    "RFQ_LIFECYCLE_TRANSITION_INVALID",
                    "The RFQ lifecycle changed or no longer satisfies the controlled award transition. Refresh the RFQ and retry.");
            }
            catch (DbUpdateException ex) when (IsPurchaseOrderApprovedSourceLineageViolation(ex))
            {
                await _unitOfWork.RollbackAsync();
                throw new ProcurementRequisitionSourcingValidationException(
                    "PO_APPROVED_SOURCE_LINEAGE_INVALID",
                    "The purchase-order source lineage is inconsistent with the selected procurement route. No award or purchase order was saved. Refresh and retry; if the problem continues, contact the administrator.");
            }
            catch (DbUpdateException ex) when (IsPurchaseOrderBudgetCommitmentViolation(ex))
            {
                await _unitOfWork.RollbackAsync();
                throw new ProcurementRequisitionSourcingValidationException(
                    "PO_BUDGET_COMMITMENT_INVALID",
                    "The linked approved budget or purchase-order commitment lifecycle is inconsistent. No award or purchase order was saved. Refresh and retry; if the problem continues, ask Finance or the administrator to verify the budget commitment configuration.");
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        });

        // Notify awarded suppliers (best-effort; do not fail award/PO creation if notification fails).
        try
        {
            await _rfqNotificationService.SendRfqAwardedNotificationAsync(rfqId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send RFQ awarded notifications for RFQ {RfqId}", rfqId);
        }

        if (response != null && usesAdvancedSourcingControls)
            await _rfqControlService.RecordAwardHandoffAsync(rfqId, response.PurchaseOrders, awardCorrelationId);

        return response ?? new CreatePurchaseOrdersFromRfqResponseDto();
    }

    internal static void ApplyAwardLineage(
        RequestForQuotation rfq,
        IEnumerable<Guid> awardedBusinessPartnerIds,
        DateTime awardedAtUtc,
        Guid actorUserId)
    {
        var suppliers = awardedBusinessPartnerIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (suppliers.Count == 0)
            throw new InvalidOperationException("An RFQ award must identify at least one supplier.");

        rfq.Status = "Awarded";
        rfq.AwardedAt = awardedAtUtc;
        rfq.AwardedBusinessPartnerId = suppliers.Count == 1 ? suppliers[0] : null;
        rfq.UpdatedAt = awardedAtUtc;
        rfq.LastModifiedById = actorUserId;
    }

    internal static bool UsesAdvancedSourcingControls(RequestForQuotation rfq) =>
        rfq.SourcingCaseId.HasValue && rfq.SourcingCaseId.Value != Guid.Empty;

    internal static bool IsRfqStatutoryLifecycleViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current.Message.Contains(
                    "RFQ issue terms or statutory lifecycle transition is invalid",
                    StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    internal static bool IsPurchaseOrderApprovedSourceLineageViolation(
        DbUpdateException exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current.Message.Contains(
                    "CK_PurchaseOrders_ApprovedSourceLineage",
                    StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains(
                    "A complete immutable approved source lineage is required for every purchase order",
                    StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    internal static bool IsPurchaseOrderBudgetCommitmentViolation(
        DbUpdateException exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current.Message.Contains(
                    "Purchase-order issuance requires the exact active tenant budget commitment with sufficient reserved exposure",
                    StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    internal static void ValidateReleaseOnlyDispatch(
        RequestForQuotation rfq,
        IReadOnlyCollection<Guid> supplierIds,
        string? externalRecipientEmails,
        DateTime nowUtc)
    {
        if (!rfq.SourcePurchaseRequisitionId.HasValue || !rfq.SourcingReleaseId.HasValue)
            throw new ProcurementRequisitionSourcingValidationException(
                "RFQ_SOURCE_LINEAGE_REQUIRED",
                "The RFQ must retain its approved requisition and immutable sourcing-release lineage before dispatch.");
        if (!rfq.SubmissionDeadline.HasValue)
            throw new ProcurementRequisitionSourcingValidationException(
                "RFQ_DEADLINE_REQUIRED",
                "Set a submission deadline before sending the RFQ to suppliers.");
        if (EnsureUtc(rfq.SubmissionDeadline.Value) <= EnsureUtc(nowUtc))
            throw new ProcurementRequisitionSourcingValidationException(
                "RFQ_DEADLINE_PASSED",
                "The RFQ submission deadline must be in the future when it is sent.");
        if (!supplierIds.Any(id => id != Guid.Empty) && string.IsNullOrWhiteSpace(externalRecipientEmails))
            throw new ProcurementRequisitionSourcingValidationException(
                "RFQ_RECIPIENT_REQUIRED",
                "Select at least one eligible supplier or provide an external recipient email address.");
    }

    internal static void ValidateReleaseOnlyAward(
        RequestForQuotation rfq,
        CreatePurchaseOrdersFromRfqDto dto,
        DateTime nowUtc)
    {
        if (!rfq.SourcePurchaseRequisitionId.HasValue || !rfq.SourcingReleaseId.HasValue)
            throw new ProcurementRequisitionSourcingValidationException(
                "RFQ_SOURCE_LINEAGE_REQUIRED",
                "The RFQ must retain its approved requisition and immutable sourcing-release lineage before award.");
        if (UsesAdvancedSourcingControls(rfq))
            throw new ProcurementRequisitionSourcingValidationException(
                "RFQ_ADVANCED_AWARD_REQUIRED",
                "A sourcing-case RFQ must complete its controlled evaluation and award approval route.");
        if (!string.Equals(rfq.Status, "Sent", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"A direct approved-PR RFQ must be Sent before its quotations can be awarded. Current status: {rfq.Status}");
        if (!rfq.SubmissionDeadline.HasValue ||
            EnsureUtc(rfq.SubmissionDeadline.Value) > EnsureUtc(nowUtc))
            throw new ProcurementRequisitionSourcingValidationException(
                "RFQ_AWARD_BEFORE_DEADLINE",
                "Submitted quotations remain sealed until the RFQ submission deadline has passed.");
        if (dto is null)
            throw new InvalidOperationException("Select the winning quotation before creating purchase orders.");
    }

    private static bool IsDuplicatePurchaseOrderNumber(DbUpdateException ex)
    {
        var msg = ex.InnerException?.Message ?? ex.Message;
        return msg.Contains("IX_PurchaseOrders_OrderNumber", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Cannot insert duplicate key row", StringComparison.OrdinalIgnoreCase)
            && msg.Contains("PurchaseOrders", StringComparison.OrdinalIgnoreCase);
    }

    private async Task SaveChangesWithPurchaseOrderNumberRetryAsync(PurchaseOrder po, int maxRetries = 8)
    {
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await _unitOfWork.SaveChangesAsync();
                return;
            }
            catch (DbUpdateException ex) when (IsDuplicatePurchaseOrderNumber(ex))
            {
                if (attempt == maxRetries)
                {
                    throw new InvalidOperationException(
                        $"Failed to generate a unique purchase order number after {maxRetries} attempts. Please retry.",
                        ex);
                }

                // Regenerate order number and retry the insert.
                po.OrderNumber = await _purchaseOrderRepository.GenerateOrderNumberAsync();
            }
        }
    }

    private async Task SyncInvitationsAsync(Guid rfqId, List<Guid> supplierIds, bool isDraftSelection)
    {
        var distinct = (supplierIds ?? new List<Guid>())
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        var existing = (await _invitationRepository.GetByRfqIdAsync(rfqId)).ToList();
        var existingBySupplier = existing
            .Where(i => !i.IsDeleted)
            .ToDictionary(i => i.BusinessPartnerId, i => i);

        // Remove ones no longer selected.
        foreach (var inv in existingBySupplier.Values)
        {
            if (!distinct.Contains(inv.BusinessPartnerId))
            {
                inv.IsDeleted = true;
                await _invitationRepository.UpdateAsync(inv);
            }
        }

        // Add/update selected.
        foreach (var supplierId in distinct)
        {
            if (existingBySupplier.TryGetValue(supplierId, out var inv))
            {
                // keep status if already responded
                if (isDraftSelection)
                {
                    if (string.Equals(inv.Status, "Invited", StringComparison.OrdinalIgnoreCase))
                        inv.Status = "Selected";
                }
                else
                {
                    inv.Status = "Invited";
                    inv.InvitedAt = DateTime.UtcNow;
                }

                await _invitationRepository.UpdateAsync(inv);
                continue;
            }

            // Validate business partner exists (best-effort).
            var bp = await _businessPartnerRepository.GetByIdAsync(supplierId);
            if (bp == null)
            {
                _logger.LogWarning("Skipping RFQ invitation for missing business partner {BusinessPartnerId}", supplierId);
                continue;
            }

            var created = new RequestForQuotationInvitation
            {
                TenantId = _currentUserProvider.TenantId,
                RfqId = rfqId,
                BusinessPartnerId = supplierId,
                Status = isDraftSelection ? "Selected" : "Invited",
                InvitedAt = DateTime.UtcNow
            };

            await _invitationRepository.AddAsync(created);
        }
    }

    private static List<string> ParseEmails(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];

        return raw
            .Split(new[] { ',', ';', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(e => e.Contains('@'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    internal static void EnsureQuoteRevisionOpen(DateTime? submissionDeadline, DateTime nowUtc)
    {
        if (!submissionDeadline.HasValue || EnsureUtc(submissionDeadline.Value) <= EnsureUtc(nowUtc))
            throw new ProcurementRfqControlConflictException(
                "RFQ_QUOTE_REVISION_CLOSED",
                "The quotation deadline has passed. The submitted quotation is retained and can no longer be revised.");
    }

    private async Task<List<RfqQuoteHistoryEntryDto>> GetQuoteHistoryAsync(RfqQuoteDto quote, Guid tenantId)
    {
        var auditLogs = await _unitOfWork.Repository<AuditLog>()
            .GetQueryable(audit =>
                audit.TenantId == tenantId &&
                audit.Resource == nameof(RequestForQuotationQuote) &&
                audit.ResourceId == quote.Id.ToString() &&
                (audit.Action == QuoteSubmittedAuditAction ||
                 audit.Action == QuoteRevisedAuditAction ||
                 audit.Action == QuoteLateRejectedAuditAction))
            .AsNoTracking()
            .OrderBy(audit => audit.Timestamp)
            .ToListAsync();

        var history = new List<RfqQuoteHistoryEntryDto>();
        foreach (var audit in auditLogs)
        {
            var snapshot = DeserializeQuoteSnapshot(audit.NewValues);
            if (snapshot == null)
                continue;

            history.Add(MapQuoteHistoryEntry(audit, snapshot));
        }

        if (!history.Any(entry => entry.RevisionNumber == 1))
        {
            var firstAudit = auditLogs.FirstOrDefault();
            var originalSnapshot = DeserializeQuoteSnapshot(firstAudit?.OldValues);
            if (firstAudit != null && originalSnapshot != null)
            {
                originalSnapshot.RevisionNumber = 1;
                history.Add(new RfqQuoteHistoryEntryDto
                {
                    Id = quote.Id,
                    RevisionNumber = 1,
                    Action = "Submitted",
                    Status = originalSnapshot.Status,
                    PerformedBy = string.IsNullOrWhiteSpace(firstAudit.Username) ? "Supplier user" : firstAudit.Username,
                    Timestamp = originalSnapshot.SubmittedAt ?? firstAudit.Timestamp,
                    TotalAmount = originalSnapshot.TotalAmount,
                    Description = "Initial quotation submitted."
                });
            }
        }

        if (history.Count == 0 && quote.SubmittedAt.HasValue)
        {
            history.Add(new RfqQuoteHistoryEntryDto
            {
                Id = quote.Id,
                RevisionNumber = 1,
                Action = string.Equals(quote.Status, "LateRejected", StringComparison.OrdinalIgnoreCase)
                    ? "Late submission rejected"
                    : "Submitted",
                Status = quote.Status,
                PerformedBy = "Supplier user",
                Timestamp = quote.SubmittedAt.Value,
                TotalAmount = quote.TotalAmount,
                Description = "Quotation submitted before revision tracking was enabled."
            });
        }

        return history
            .OrderByDescending(entry => entry.RevisionNumber)
            .ThenByDescending(entry => entry.Timestamp)
            .ToList();
    }

    private static RfqQuoteHistoryEntryDto MapQuoteHistoryEntry(AuditLog audit, QuoteAuditSnapshot snapshot)
    {
        var action = audit.Action switch
        {
            QuoteRevisedAuditAction => "Revised",
            QuoteLateRejectedAuditAction => "Late submission rejected",
            _ => "Submitted"
        };

        return new RfqQuoteHistoryEntryDto
        {
            Id = audit.Id,
            RevisionNumber = Math.Max(1, snapshot.RevisionNumber),
            Action = action,
            Status = snapshot.Status,
            PerformedBy = string.IsNullOrWhiteSpace(audit.Username) ? "Supplier user" : audit.Username,
            Timestamp = audit.Timestamp,
            TotalAmount = snapshot.TotalAmount,
            Description = action switch
            {
                "Revised" => "Quotation prices or terms updated and resubmitted before the deadline.",
                "Late submission rejected" => "Quotation was received after the deadline and retained for audit only.",
                _ => "Initial quotation submitted."
            }
        };
    }

    private static QuoteAuditSnapshot? DeserializeQuoteSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<QuoteAuditSnapshot>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static QuoteAuditSnapshot CreateQuoteSnapshot(
        RequestForQuotationQuote quote,
        int revisionNumber,
        List<QuoteAuditItemSnapshot>? items = null)
    {
        var snapshotItems = items ?? (quote.Items ?? new List<RequestForQuotationQuoteItem>())
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.RfqItem?.LineNumber ?? 0)
            .Select(item => new QuoteAuditItemSnapshot
            {
                RfqItemId = item.RfqItemId,
                LineNumber = item.RfqItem?.LineNumber ?? 0,
                Description = item.RfqItem?.Description ?? string.Empty,
                Quantity = item.RfqItem?.Quantity ?? 0,
                UnitOfMeasure = item.RfqItem?.UnitOfMeasure ?? string.Empty,
                UnitPrice = item.UnitPrice,
                LineTotal = item.LineTotal
            })
            .ToList();

        return new QuoteAuditSnapshot
        {
            RevisionNumber = revisionNumber,
            Status = quote.Status,
            SubmittedAt = quote.SubmittedAt,
            Notes = quote.Notes,
            TotalAmount = snapshotItems.Sum(item => item.LineTotal),
            Items = snapshotItems
        };
    }

    private sealed class QuoteAuditSnapshot
    {
        public int RevisionNumber { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? SubmittedAt { get; set; }
        public string? Notes { get; set; }
        public decimal TotalAmount { get; set; }
        public List<QuoteAuditItemSnapshot> Items { get; set; } = new();
    }

    private sealed class QuoteAuditItemSnapshot
    {
        public Guid RfqItemId { get; set; }
        public int LineNumber { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string UnitOfMeasure { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    private static void EnsureSourceLineage(Guid? releaseId, Guid? caseId, ProcurementSourcingCaseEntryGateDto gate)
    {
        if (releaseId.HasValue && releaseId.Value != gate.SourcingReleaseId)
            throw new ProcurementRequisitionSourcingValidationException("RFQ_RELEASE_LINEAGE_MISMATCH", "RFQ sourcing-release lineage cannot be replaced.");
        if (caseId.HasValue && caseId.Value != gate.SourcingCaseId)
            throw new ProcurementRequisitionSourcingValidationException("RFQ_CASE_LINEAGE_MISMATCH", "RFQ sourcing-case lineage cannot be replaced.");
    }

    private static RfqDto MapToDto(RequestForQuotation rfq)
    {
        return new RfqDto
        {
            Id = rfq.Id,
            RfqNumber = rfq.RfqNumber,
            Title = rfq.Title,
            Status = rfq.Status,
            SubmissionDeadline = rfq.SubmissionDeadline,
            Currency = rfq.Currency,
            EstimatedValue = rfq.EstimatedValue,
            SourcePurchaseRequisitionId = rfq.SourcePurchaseRequisitionId,
            SourcingReleaseId = rfq.SourcingReleaseId,
            SourcingCaseId = rfq.SourcingCaseId,
            CreatedAt = rfq.CreatedAt,
            SentAt = rfq.SentAt,
            SupplierCount = rfq.Invitations?.Count(i => !i.IsDeleted) ?? 0,
            // Quotes count should reflect supplier submissions (not drafts).
            QuoteCount = rfq.Quotes?.Count(q => !q.IsDeleted && string.Equals(q.Status, "Submitted", StringComparison.OrdinalIgnoreCase)) ?? 0
        };
    }

    private static RfqDetailDto MapToDetailDto(RequestForQuotation rfq)
    {
        var awardedQuoteIdByItemId = (rfq.AwardLines ?? new List<RequestForQuotationAwardLine>())
            .Where(a => !a.IsDeleted)
            .GroupBy(a => a.RfqItemId)
            .ToDictionary(g => g.Key, g => g.Last().QuoteId);

        var detail = new RfqDetailDto
        {
            Id = rfq.Id,
            RfqNumber = rfq.RfqNumber,
            Title = rfq.Title,
            Status = rfq.Status,
            SubmissionDeadline = rfq.SubmissionDeadline,
            Currency = rfq.Currency,
            EstimatedValue = rfq.EstimatedValue,
            SourcePurchaseRequisitionId = rfq.SourcePurchaseRequisitionId,
            SourcingReleaseId = rfq.SourcingReleaseId,
            SourcingCaseId = rfq.SourcingCaseId,
            CreatedAt = rfq.CreatedAt,
            SentAt = rfq.SentAt,
            ExternalRecipientEmails = rfq.ExternalRecipientEmails,
            Description = rfq.Description,
            SupplierCount = rfq.Invitations?.Count(i => !i.IsDeleted) ?? 0,
            // Quotes count should reflect supplier submissions (not drafts).
            QuoteCount = rfq.Quotes?.Count(q => !q.IsDeleted && string.Equals(q.Status, "Submitted", StringComparison.OrdinalIgnoreCase)) ?? 0,
            Items = (rfq.Items ?? new List<RequestForQuotationItem>())
                .Where(i => !i.IsDeleted)
                .OrderBy(i => i.LineNumber)
                .Select(i => new RfqItemDto
                {
                    Id = i.Id,
                    LineNumber = i.LineNumber,
                    InventoryItemId = i.InventoryItemId,
                    ItemCode = i.ItemCode,
                    Description = i.Description,
                    Quantity = i.Quantity,
                    UnitOfMeasure = i.UnitOfMeasure,
                    Specifications = i.Specifications,
                    RequiredDeliveryDate = i.RequiredDeliveryDate
                })
                .ToList(),
            Suppliers = (rfq.Invitations ?? new List<RequestForQuotationInvitation>())
                .Where(i => !i.IsDeleted)
                .OrderByDescending(i => i.InvitedAt)
                .Select(i => new RfqInvitationDto
                {
                    Id = i.Id,
                    BusinessPartnerId = i.BusinessPartnerId,
                    PartnerCode = i.BusinessPartner?.PartnerCode ?? string.Empty,
                    PartnerName = i.BusinessPartner?.PartnerName ?? string.Empty,
                    PrimaryEmail = i.BusinessPartner?.PrimaryEmail,
                    Status = i.Status,
                    InvitedAt = i.InvitedAt
                })
                .ToList(),
            Quotes = (rfq.Quotes ?? new List<RequestForQuotationQuote>())
                .Where(q => !q.IsDeleted && (string.Equals(q.Status, "Submitted", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(q.Status, "LateRejected", StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(q => q.SubmittedAt ?? q.CreatedAt)
                .Select(q => MapToQuoteDto(q, awardedQuoteIdByItemId))
                .ToList()
        };

        // Ensure totals are computed even if stored items do not include it.
        foreach (var q in detail.Quotes)
        {
            q.TotalAmount = q.Items.Sum(i => i.LineTotal);
        }

        return detail;
    }

    private static RfqQuoteDto MapToQuoteDto(RequestForQuotationQuote quote, Dictionary<Guid, Guid>? awardedQuoteIdByRfqItemId)
    {
        var dto = new RfqQuoteDto
        {
            Id = quote.Id,
            BusinessPartnerId = quote.BusinessPartnerId,
            PartnerCode = quote.BusinessPartner?.PartnerCode ?? string.Empty,
            PartnerName = quote.BusinessPartner?.PartnerName ?? string.Empty,
            Status = quote.Status,
            SubmittedAt = quote.SubmittedAt,
            Notes = quote.Notes,
            Items = (quote.Items ?? new List<RequestForQuotationQuoteItem>())
                .Where(i => !i.IsDeleted)
                .OrderBy(i => i.RfqItem.LineNumber)
                .Select(i => new RfqQuoteItemDto
                {
                    RfqItemId = i.RfqItemId,
                    LineNumber = i.RfqItem.LineNumber,
                    Description = i.RfqItem.Description,
                    Quantity = i.RfqItem.Quantity,
                    UnitOfMeasure = i.RfqItem.UnitOfMeasure,
                    UnitPrice = i.UnitPrice,
                    LineTotal = i.LineTotal,
                    IsAwarded = awardedQuoteIdByRfqItemId != null
                        && awardedQuoteIdByRfqItemId.TryGetValue(i.RfqItemId, out var awardedQuoteId)
                        && awardedQuoteId == quote.Id
                })
                .ToList()
        };

        dto.TotalAmount = dto.Items.Sum(i => i.LineTotal);
        return dto;
    }
}
