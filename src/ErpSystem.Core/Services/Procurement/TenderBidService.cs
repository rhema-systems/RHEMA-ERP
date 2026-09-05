using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class TenderBidService : ITenderBidService
{
    private readonly ITenderBidRepository _bidRepository;
    private readonly ITenderRepository _tenderRepository;
    private readonly ITenderBidItemRepository _bidItemRepository;
    private readonly ITenderBidDocumentRepository _bidDocumentRepository;
    private readonly ITenderPaymentRepository _paymentRepository;
    private readonly ITenderFeeRepository _feeRepository;
    private readonly ITenderInterviewRepository _interviewRepository;
    private readonly ITenderAssignmentRepository _assignmentRepository;
    private readonly ITenderBidLotRepository _bidLotRepository;
    private readonly ITenderNotificationService _notificationService;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;
    private readonly IBusinessPartnerUserRepository _businessPartnerUserRepository;
    private readonly ISupplierValidationService _supplierValidationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<TenderBidService> _logger;
    private readonly IAppEventBus _appEventBus;
    private readonly IProcurementTenderControlService _tenderControlService;
    private readonly IProcurementTenderDocumentControlService _tenderDocumentControlService;
    private readonly IProcurementExceptionalSourcingControlService _exceptionalSourcingControlService;
    private readonly IQuantitySurveyTenderBoqSubmissionService _quantitySurveyTenderBoqSubmissions;
    private readonly IFinancePostingEngine? _financePostingEngine;

    public TenderBidService(
        ITenderBidRepository bidRepository,
        ITenderRepository tenderRepository,
        ITenderBidItemRepository bidItemRepository,
        ITenderBidDocumentRepository bidDocumentRepository,
        ITenderPaymentRepository paymentRepository,
        ITenderFeeRepository feeRepository,
        ITenderInterviewRepository interviewRepository,
        ITenderAssignmentRepository assignmentRepository,
        ITenderBidLotRepository bidLotRepository,
        ITenderNotificationService notificationService,
        IBusinessPartnerRepository businessPartnerRepository,
        IBusinessPartnerUserRepository businessPartnerUserRepository,
        ISupplierValidationService supplierValidationService,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IAppEventBus appEventBus,
        IProcurementTenderControlService tenderControlService,
        IProcurementTenderDocumentControlService tenderDocumentControlService,
        IProcurementExceptionalSourcingControlService exceptionalSourcingControlService,
        IQuantitySurveyTenderBoqSubmissionService quantitySurveyTenderBoqSubmissions,
        ILogger<TenderBidService> logger,
        IFinancePostingEngine? financePostingEngine = null)
    {
        _bidRepository = bidRepository;
        _tenderRepository = tenderRepository;
        _bidItemRepository = bidItemRepository;
        _bidDocumentRepository = bidDocumentRepository;
        _paymentRepository = paymentRepository;
        _feeRepository = feeRepository;
        _interviewRepository = interviewRepository;
        _assignmentRepository = assignmentRepository;
        _bidLotRepository = bidLotRepository;
        _notificationService = notificationService;
        _businessPartnerRepository = businessPartnerRepository;
        _businessPartnerUserRepository = businessPartnerUserRepository;
        _supplierValidationService = supplierValidationService;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _appEventBus = appEventBus;
        _tenderControlService = tenderControlService;
        _tenderDocumentControlService = tenderDocumentControlService;
        _exceptionalSourcingControlService = exceptionalSourcingControlService;
        _quantitySurveyTenderBoqSubmissions = quantitySurveyTenderBoqSubmissions;
        _logger = logger;
        _financePostingEngine = financePostingEngine;
    }

    public async Task<TenderBidDetailDto?> GetBidByIdAsync(Guid id)
    {
        try
        {
            var bid = await _bidRepository.GetWithAllRelatedDataAsync(id);
            if (bid == null) return null;

            var tender = await _tenderRepository.GetByIdAsync(bid.TenderId);
            var items = await _bidItemRepository.GetByBidIdAsync(id);
            var documents = await _bidDocumentRepository.GetByBidIdAsync(id);

            return await ProtectFinancialProposalAsync(MapToDetailDto(bid, tender, items, documents));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bid {BidId}", id);
            throw;
        }
    }

    public async Task<TenderBidDetailDto?> GetBidByNumberAsync(string bidNumber)
    {
        try
        {
            var bid = await _bidRepository.GetByBidNumberAsync(bidNumber);
            if (bid == null) return null;

            // Fetch all related data including evaluations and interviews
            var bidWithAllData = await _bidRepository.GetWithAllRelatedDataAsync(bid.Id);
            if (bidWithAllData == null) return null;

            var tender = await _tenderRepository.GetByIdAsync(bid.TenderId);
            var items = await _bidItemRepository.GetByBidIdAsync(bid.Id);
            var documents = await _bidDocumentRepository.GetByBidIdAsync(bid.Id);

            return await ProtectFinancialProposalAsync(MapToDetailDto(bidWithAllData, tender, items, documents));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bid by number {BidNumber}", bidNumber);
            throw;
        }
    }

    public async Task<PagedResult<TenderBidSummaryDto>> GetBidsAsync(int page, int pageSize, string? search = null, string? status = null, Guid? tenderId = null)
    {
        try
        {
            var pagedBids = await _bidRepository.GetBidsAsync(page, pageSize, search, status, tenderId);
            var items = new List<TenderBidSummaryDto>();

            foreach (var bid in pagedBids.Items)
            {
                var tender = await _tenderRepository.GetByIdAsync(bid.TenderId);

                // Get payment information
                var completedPayment = await GetSatisfiedPaymentForBidAsync(bid);

                items.Add(await ProtectFinancialProposalAsync(MapToSummaryDto(bid, tender, completedPayment)));
            }

            return new PagedResult<TenderBidSummaryDto>
            {
                Items = items,
                TotalCount = pagedBids.TotalCount,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bids");
            throw;
        }
    }

    public async Task<IEnumerable<TenderBidSummaryDto>> GetBidsByTenderIdAsync(Guid tenderId)
    {
        try
        {
            var bids = await _bidRepository.GetByTenderIdAsync(tenderId);
            var tender = await _tenderRepository.GetByIdAsync(tenderId);
            var summaries = new List<TenderBidSummaryDto>();

            foreach (var bid in bids)
            {
                // Get payment information
                var completedPayment = await GetSatisfiedPaymentForBidAsync(bid);

                summaries.Add(await ProtectFinancialProposalAsync(MapToSummaryDto(bid, tender, completedPayment)));
            }

            return summaries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bids for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderBidSummaryDto>> GetMyBidsAsync(Guid businessPartnerId)
    {
        try
        {
            // Get bids created by this business partner
            var bids = await _bidRepository.GetByBusinessPartnerIdAsync(businessPartnerId);
            var bidList = bids.ToList();

            var summaries = new List<TenderBidSummaryDto>();

            foreach (var bid in bidList)
            {
                var tender = await _tenderRepository.GetByIdAsync(bid.TenderId);

                // Get payment information
                var completedPayment = await GetSatisfiedPaymentForBidAsync(bid);

                summaries.Add(await ProtectFinancialProposalAsync(MapToSummaryDto(bid, tender, completedPayment)));
            }

            return summaries.OrderByDescending(s => s.SubmittedDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bids for business partner {BusinessPartnerId}", businessPartnerId);
            throw;
        }
    }

    public async Task<TenderBidDetailDto?> GetMyDraftBidByTenderIdAsync(Guid tenderId)
    {
        try
        {
            // Get the business partner for the current user
            // First, try to get as main account owner
            var businessPartner = await _businessPartnerRepository.GetByUserIdAsync(_currentUserProvider.UserId);

            // If not found, try to get as sub-user via BusinessPartnerUser table
            if (businessPartner == null)
            {
                var businessPartnerUser = await _businessPartnerUserRepository.GetByUserIdAsync(_currentUserProvider.UserId);
                if (businessPartnerUser != null && businessPartnerUser.IsActive)
                {
                    businessPartner = businessPartnerUser.BusinessPartner;
                }
            }

            if (businessPartner == null)
            {
                return null;
            }

            // Get the draft bid for this tender and business partner
            var bid = await _bidRepository.GetByTenderAndPartnerAsync(tenderId, businessPartner.Id);
            if (bid == null || bid.Status != "Draft")
            {
                return null;
            }

            var tender = await _tenderRepository.GetByIdAsync(bid.TenderId);
            var items = await _bidItemRepository.GetByBidIdAsync(bid.Id);
            var documents = await _bidDocumentRepository.GetByBidIdAsync(bid.Id);

            return MapToDetailDto(bid, tender, items, documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving draft bid for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<TenderBidInitiationStatusDto> GetInitiationStatusAsync(Guid tenderId)
    {
        var tender = await _tenderRepository.GetByIdAsync(tenderId)
            ?? throw new TenderBidInitiationValidationException(
                "TENDER_NOT_FOUND", "The tender was not found.");
        var businessPartner = await GetCurrentBusinessPartnerAsync()
            ?? throw new TenderBidInitiationValidationException(
                "TENDER_BID_SUPPLIER_REQUIRED",
                "The current account is not linked to an active supplier.");
        var assignments = await _assignmentRepository.GetByTenderAndBusinessPartnerAsync(
            tenderId, businessPartner.Id);
        var assignment = assignments.FirstOrDefault();
        var bid = await _bidRepository.GetByTenderAndPartnerAsync(tenderId, businessPartner.Id);
        var fees = (await _feeRepository.GetByTenderIdAsync(tenderId)).ToList();
        var payments = (await _paymentRepository.GetByBusinessPartnerIdAsync(businessPartner.Id))
            .Where(payment => fees.Any(fee => fee.Id == payment.TenderFeeId))
            .ToList();

        return BuildInitiationStatus(tender, businessPartner.Id, assignment, bid, fees, payments);
    }

    public async Task<TenderBidDetailDto> CreateBidAsync(CreateTenderBidDto dto)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(dto.TenderId)
                ?? throw new InvalidOperationException($"Tender with ID {dto.TenderId} not found");

            if (tender.Status != "Published")
            {
                throw new InvalidOperationException("Bids can only be submitted for published tenders");
            }

            var usesControlledTenderLifecycle = await _tenderControlService.IsControlledTenderMethodAsync(tender.Id);
            if (!usesControlledTenderLifecycle && DateTime.UtcNow > tender.SubmissionDeadline)
            {
                throw new InvalidOperationException("Tender submission deadline has passed");
            }

            // Get the business partner for the current user
            // First, try to get as main account owner
            var businessPartner = await _businessPartnerRepository.GetByUserIdAsync(_currentUserProvider.UserId);

            // If not found, try to get as sub-user via BusinessPartnerUser table
            if (businessPartner == null)
            {
                var businessPartnerUser = await _businessPartnerUserRepository.GetByUserIdAsync(_currentUserProvider.UserId);
                if (businessPartnerUser != null && businessPartnerUser.IsActive)
                {
                    businessPartner = businessPartnerUser.BusinessPartner;
                }
            }

            if (businessPartner == null)
            {
                throw new InvalidOperationException("No business partner found for the current user. Please complete your business partner registration first.");
            }

            await _exceptionalSourcingControlService.EnsureBidSupplierAllowedAsync(tender.Id, businessPartner.Id);

            // Validate supplier eligibility for this tender
            var validationResult = await _supplierValidationService.ValidateForTenderAsync(
                businessPartner.Id,
                tender.RequiresPrequalification,
                tender.MinimumPerformanceRating
            );

            if (!validationResult.IsValid)
            {
                var errorMessage = string.Join("; ", validationResult.Errors);
                _logger.LogWarning("Supplier {PartnerId} failed validation for tender {TenderId}: {Errors}",
                    businessPartner.Id, dto.TenderId, errorMessage);
                throw new InvalidOperationException($"You are not eligible to bid on this tender. {errorMessage}");
            }

            var selectedLotIds = (dto.SelectedLotIds ?? new List<Guid>())
                .Distinct()
                .ToHashSet();
            var selectedLots = tender.Lots
                .Where(lot => selectedLotIds.Contains(lot.Id))
                .ToList();
            if (selectedLots.Count != selectedLotIds.Count)
            {
                throw new InvalidOperationException(
                    "One or more selected lots do not belong to this tender.");
            }

            if (selectedLots.Any(lot => string.Equals(
                    lot.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Cancelled tender lots cannot be selected for a bid.");
            }

            var selectedTenderItems = selectedLots
                .SelectMany(lot => lot.Items)
                .ToDictionary(item => item.Id);
            var suppliedItemIds = (dto.Items ?? new List<CreateTenderBidItemDto>())
                .Select(item => item.TenderItemId)
                .ToList();
            if (suppliedItemIds.Count != suppliedItemIds.Distinct().Count())
            {
                throw new InvalidOperationException("A tender item can appear only once in a bid draft.");
            }
            if (selectedLotIds.Count > 0 &&
                (suppliedItemIds.Any(id => !selectedTenderItems.ContainsKey(id)) ||
                 selectedTenderItems.Keys.Any(id => !suppliedItemIds.Contains(id))))
            {
                throw new InvalidOperationException(
                    "A bid draft must include every item, and only items, from its selected lots.");
            }

            // Generate bid number
            var bidNumber = await GenerateBidNumberAsync(tender.TenderNumber);

            var bid = new TenderBid
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderId = dto.TenderId,
                BusinessPartnerId = businessPartner.Id,
                BidNumber = bidNumber,
                SubmittedDate = DateTime.UtcNow,
                Status = "Draft",
                DeliveryDays = dto.DeliveryDays,
                PaymentTerms = dto.PaymentTerms,
                WarrantyTerms = dto.WarrantyTerms,
                TechnicalProposal = dto.TechnicalProposal,
                CommercialProposal = dto.CommercialProposal,
                AssociationType = dto.AssociationType,
                AcceptedDeclaration = dto.AcceptedDeclaration,
                DeclarationAcceptedAt = dto.AcceptedDeclaration ? DateTime.UtcNow : null,
                Currency = tender.Currency,
                CreatedAt = DateTime.UtcNow
            };

            await _bidRepository.CreateAsync(bid);

            // Save the bid first to satisfy foreign key constraint
            await _unitOfWork.SaveChangesAsync();

            var bidLots = new List<TenderBidLot>();
            var bidLotByTenderLotId = new Dictionary<Guid, TenderBidLot>();
            foreach (var selectedLot in selectedLots)
            {
                var bidLot = new TenderBidLot
                {
                    Id = Guid.NewGuid(),
                    TenantId = _currentUserProvider.TenantId,
                    TenderBidId = bid.Id,
                    LotId = selectedLot.Id,
                    TotalLotAmount = 0m,
                    Currency = tender.Currency,
                    Status = "Draft",
                    CreatedAt = DateTime.UtcNow,
                    Lot = selectedLot,
                    TenderBid = bid
                };
                bidLots.Add(bidLot);
                bidLotByTenderLotId[selectedLot.Id] = bidLot;
                bid.BidLots.Add(bidLot);
                await _bidLotRepository.CreateAsync(bidLot);
            }

            // Add bid items
            var items = new List<TenderBidItem>();
            if (dto.Items != null && dto.Items.Any())
            {
                decimal totalBidAmount = 0;

                foreach (var itemDto in dto.Items)
                {
                    selectedTenderItems.TryGetValue(itemDto.TenderItemId, out var tenderItem);
                    TenderBidLot? bidLot = null;
                    if (tenderItem?.LotId is Guid tenderLotId)
                    {
                        bidLotByTenderLotId.TryGetValue(tenderLotId, out bidLot);
                    }
                    var item = new TenderBidItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _currentUserProvider.TenantId,
                        TenderBidId = bid.Id,
                        BidLotId = bidLot?.Id,
                        TenderItemId = itemDto.TenderItemId,
                        OfferedQuantity = itemDto.OfferedQuantity,
                        UnitPrice = itemDto.UnitPrice,
                        TotalPrice = itemDto.OfferedQuantity * itemDto.UnitPrice,
                        DeliveryDays = itemDto.DeliveryDays,
                        Specifications = itemDto.Specifications,
                        Brand = itemDto.Brand,
                        Model = itemDto.Model,
                        TechnicalDetails = itemDto.TechnicalDetails,
                        CreatedAt = DateTime.UtcNow,
                        TenderBid = bid,
                        TenderItem = tenderItem!
                    };

                    totalBidAmount += item.TotalPrice;
                    if (bidLot != null)
                    {
                        bidLot.TotalLotAmount += item.TotalPrice;
                        bidLot.Items.Add(item);
                    }
                    await _bidItemRepository.CreateAsync(item);
                    items.Add(item);
                }

                bid.TotalBidAmount = totalBidAmount;
                await _bidRepository.UpdateAsync(bid);

                // Save bid items and updated total
                await _unitOfWork.SaveChangesAsync();
            }

            _logger.LogInformation("Created bid {BidId} for tender {TenderId}", bid.Id, dto.TenderId);

            // Publish events for admin-configurable notification topics (best-effort).
            try
            {
                var baseData = new Dictionary<string, object>
                {
                    ["BidId"] = bid.Id,
                    ["BidNumber"] = bid.BidNumber ?? string.Empty,
                    ["TenderId"] = bid.TenderId,
                    ["TenderNumber"] = tender.TenderNumber ?? string.Empty,
                    ["BusinessPartnerId"] = bid.BusinessPartnerId,
                    ["Status"] = bid.Status ?? string.Empty,
                    ["TotalBidAmount"] = bid.TotalBidAmount,
                    ["Currency"] = bid.Currency ?? string.Empty
                };

                await _appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = bid.TenantId,
                    EntityType = "Bid",
                    Activity = "Created",
                    Audience = "Supplier",
                    EntityId = bid.Id,
                    TriggeredByUserId = _currentUserProvider.UserId,
                    Data = new Dictionary<string, object>(baseData)
                });

                await _appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = bid.TenantId,
                    EntityType = "Bid",
                    Activity = "Created",
                    Audience = "Internal",
                    EntityId = bid.Id,
                    TriggeredByUserId = _currentUserProvider.UserId,
                    Data = new Dictionary<string, object>(baseData)
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish Bid.Created entity activity event for bid {BidId}", bid.Id);
            }

            return MapToDetailDto(bid, tender, items, new List<TenderBidDocument>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bid for tender {TenderId}", dto.TenderId);
            throw;
        }
    }

    public async Task<TenderBidDetailDto> UpdateBidAsync(Guid id, UpdateTenderBidDto dto)
    {
        try
        {
            var bid = await _bidRepository.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Bid with ID {id} not found");

            if (bid.Status != "Draft")
            {
                throw new InvalidOperationException("Only draft bids can be updated");
            }

            var tender = await _tenderRepository.GetByIdAsync(bid.TenderId)
                ?? throw new InvalidOperationException($"Tender with ID {bid.TenderId} not found");

            var existingItems = (await _bidItemRepository.GetByBidIdAsync(id)).ToList();
            var allTenderItems = tender.Items
                .Concat(tender.Lots.SelectMany(lot => lot.Items))
                .GroupBy(item => item.Id)
                .ToDictionary(group => group.Key, group => group.First());
            if (dto.Items is not null &&
                dto.Items.Select(item => item.TenderItemId).Distinct().Count() != dto.Items.Count)
            {
                throw new InvalidOperationException(
                    "A tender item can appear only once in a bid draft.");
            }
            var selectedTenderItems = new Dictionary<Guid, TenderItem>();
            if (dto.SelectedLotIds != null)
            {
                var selectedLotIds = dto.SelectedLotIds.Distinct().ToHashSet();
                var selectedLots = tender.Lots
                    .Where(lot => selectedLotIds.Contains(lot.Id))
                    .ToList();
                if (selectedLots.Count != selectedLotIds.Count)
                {
                    throw new InvalidOperationException(
                        "One or more selected lots do not belong to this tender.");
                }
                if (selectedLots.Any(lot => string.Equals(
                        lot.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("Cancelled tender lots cannot be selected for a bid.");
                }

                selectedTenderItems = selectedLots
                    .SelectMany(lot => lot.Items)
                    .ToDictionary(item => item.Id);
                var suppliedItemIds = (dto.Items ?? new List<UpdateTenderBidItemDto>())
                    .Select(item => item.TenderItemId)
                    .ToList();
                if (suppliedItemIds.Count != suppliedItemIds.Distinct().Count())
                {
                    throw new InvalidOperationException("A tender item can appear only once in a bid draft.");
                }
                if (selectedLotIds.Count > 0 &&
                    (suppliedItemIds.Any(id => !selectedTenderItems.ContainsKey(id)) ||
                     selectedTenderItems.Keys.Any(id => !suppliedItemIds.Contains(id))))
                {
                    throw new InvalidOperationException(
                        "A bid draft must include every item, and only items, from its selected lots.");
                }

                var persistedLots = bid.BidLots
                    .Where(lot => !lot.IsDeleted)
                    .ToList();
                var removedLots = persistedLots
                    .Where(lot => !selectedLotIds.Contains(lot.LotId))
                    .ToList();
                foreach (var removedLot in removedLots)
                {
                    var removedItems = existingItems
                        .Where(item => item.BidLotId == removedLot.Id)
                        .ToList();
                    foreach (var removedItem in removedItems)
                    {
                        await _bidItemRepository.DeleteAsync(removedItem.Id);
                        existingItems.Remove(removedItem);
                    }
                    await _bidLotRepository.DeleteAsync(removedLot.Id);
                    bid.BidLots.Remove(removedLot);
                }

                var persistedLotIds = bid.BidLots
                    .Where(lot => !lot.IsDeleted)
                    .Select(lot => lot.LotId)
                    .ToHashSet();
                foreach (var selectedLot in selectedLots.Where(
                             lot => !persistedLotIds.Contains(lot.Id)))
                {
                    var bidLot = new TenderBidLot
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _currentUserProvider.TenantId,
                        TenderBidId = bid.Id,
                        LotId = selectedLot.Id,
                        TotalLotAmount = 0m,
                        Currency = tender.Currency,
                        Status = "Draft",
                        CreatedAt = DateTime.UtcNow,
                        Lot = selectedLot,
                        TenderBid = bid
                    };
                    bid.BidLots.Add(bidLot);
                    await _bidLotRepository.CreateAsync(bidLot);
                }
            }

            bid.DeliveryDays = dto.DeliveryDays;
            bid.PaymentTerms = dto.PaymentTerms;
            bid.WarrantyTerms = dto.WarrantyTerms;
            bid.TechnicalProposal = dto.TechnicalProposal;
            bid.CommercialProposal = dto.CommercialProposal;
            if (dto.AssociationType != null)
                bid.AssociationType = dto.AssociationType;
            if (dto.AcceptedDeclaration.HasValue)
            {
                bid.AcceptedDeclaration = dto.AcceptedDeclaration.Value;
                bid.DeclarationAcceptedAt = dto.AcceptedDeclaration.Value
                    ? bid.DeclarationAcceptedAt ?? DateTime.UtcNow
                    : null;
            }
            bid.UpdatedAt = DateTime.UtcNow;

            await _bidRepository.UpdateAsync(bid);

            // Update bid items if provided
            if (dto.Items != null)
            {
                var suppliedTenderItemIds = dto.Items
                    .Select(item => item.TenderItemId)
                    .ToHashSet();
                foreach (var removedItem in existingItems
                             .Where(item => !suppliedTenderItemIds.Contains(item.TenderItemId))
                             .ToList())
                {
                    await _bidItemRepository.DeleteAsync(removedItem.Id);
                    existingItems.Remove(removedItem);
                }

                var bidLotByTenderLotId = bid.BidLots
                    .Where(item => !item.IsDeleted)
                    .ToDictionary(item => item.LotId);

                decimal totalBidAmount = 0;

                foreach (var itemDto in dto.Items)
                {
                    // Find existing item by TenderItemId
                    var existingItem = existingItems.FirstOrDefault(i => i.TenderItemId == itemDto.TenderItemId);

                    if (existingItem != null)
                    {
                        // Update existing item
                        existingItem.OfferedQuantity = itemDto.OfferedQuantity;
                        existingItem.UnitPrice = itemDto.UnitPrice;
                        existingItem.TotalPrice = itemDto.OfferedQuantity * itemDto.UnitPrice;
                        existingItem.DeliveryDays = itemDto.DeliveryDays;
                        existingItem.Specifications = itemDto.Specifications;
                        existingItem.Brand = itemDto.Brand;
                        existingItem.Model = itemDto.Model;
                        existingItem.TechnicalDetails = itemDto.TechnicalDetails;
                        if (selectedTenderItems.TryGetValue(itemDto.TenderItemId, out var tenderItem) &&
                            tenderItem.LotId is Guid tenderLotId &&
                            bidLotByTenderLotId.TryGetValue(tenderLotId, out var bidLot))
                        {
                            existingItem.BidLotId = bidLot.Id;
                        }
                        existingItem.UpdatedAt = DateTime.UtcNow;

                        await _bidItemRepository.UpdateAsync(existingItem);
                    }
                    else
                    {
                        if (!selectedTenderItems.TryGetValue(itemDto.TenderItemId, out var tenderItem) &&
                            !allTenderItems.TryGetValue(itemDto.TenderItemId, out tenderItem))
                        {
                            throw new InvalidOperationException(
                                "One or more bid items do not belong to this tender.");
                        }
                        TenderBidLot? bidLot = null;
                        if (tenderItem?.LotId is Guid tenderLotId)
                            bidLotByTenderLotId.TryGetValue(tenderLotId, out bidLot);
                        // Create new item
                        var newItem = new TenderBidItem
                        {
                            Id = Guid.NewGuid(),
                            TenantId = _currentUserProvider.TenantId,
                            TenderBidId = bid.Id,
                            BidLotId = bidLot?.Id,
                            TenderItemId = itemDto.TenderItemId,
                            OfferedQuantity = itemDto.OfferedQuantity,
                            UnitPrice = itemDto.UnitPrice,
                            TotalPrice = itemDto.OfferedQuantity * itemDto.UnitPrice,
                            DeliveryDays = itemDto.DeliveryDays,
                            Specifications = itemDto.Specifications,
                            Brand = itemDto.Brand,
                            Model = itemDto.Model,
                            TechnicalDetails = itemDto.TechnicalDetails,
                            CreatedAt = DateTime.UtcNow,
                            TenderBid = bid,
                            TenderItem = tenderItem!
                        };

                        await _bidItemRepository.CreateAsync(newItem);
                        existingItems.Add(newItem);
                        bidLot?.Items.Add(newItem);
                    }

                    totalBidAmount += itemDto.OfferedQuantity * itemDto.UnitPrice;
                }

                // Update total bid amount
                bid.TotalBidAmount = totalBidAmount;
                await _bidRepository.UpdateAsync(bid);

                foreach (var bidLot in bid.BidLots.Where(lot => !lot.IsDeleted))
                {
                    bidLot.TotalLotAmount = existingItems
                        .Where(item => item.BidLotId == bidLot.Id)
                        .Sum(item => item.TotalPrice);
                    bidLot.UpdatedAt = DateTime.UtcNow;
                    await _bidLotRepository.UpdateAsync(bidLot);
                }
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated bid {BidId}", id);

            var items = await _bidItemRepository.GetByBidIdAsync(id);
            var documents = await _bidDocumentRepository.GetByBidIdAsync(id);

            return MapToDetailDto(bid, tender, items, documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating bid {BidId}", id);
            throw;
        }
    }

    public async Task<TenderBidDetailDto> SubmitBidAsync(Guid id, SubmitTenderBidDto dto)
    {
        try
        {
            var bid = await _bidRepository.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Bid with ID {id} not found");

            if (bid.Status != "Draft")
            {
                throw new InvalidOperationException("Only draft bids can be submitted");
            }

            var tender = await _tenderRepository.GetByIdAsync(bid.TenderId)
                ?? throw new InvalidOperationException($"Tender with ID {bid.TenderId} not found");

            var submittedAtUtc = DateTime.UtcNow;
            var assignments = (await _assignmentRepository.GetByTenderAndBusinessPartnerAsync(
                tender.Id, bid.BusinessPartnerId)).ToList();
            var fees = (await _feeRepository.GetByTenderIdAsync(tender.Id)).ToList();
            var payments = (await _paymentRepository.GetByBusinessPartnerIdAsync(bid.BusinessPartnerId))
                .Where(payment => fees.Any(fee => fee.Id == payment.TenderFeeId))
                .ToList();
            var paymentAdmission = ValidateInitiationRequirements(tender, bid, assignments, fees, payments);
            await _exceptionalSourcingControlService.EnsureBidSupplierAllowedAsync(tender.Id, bid.BusinessPartnerId);
            // The controlled-document register is owned by an advanced locked
            // sourcing case. A published direct tender can legitimately retain
            // only its approved requisition and immutable sourcing release; its
            // publication path deliberately does not fabricate a sourcing case or
            // controlled-document register. Keep the advanced guard fail-closed,
            // but do not apply it to that release-only route.
            var usesAdvancedSourcingControls = tender.SourcingCaseId.HasValue &&
                                               tender.SourcingCaseId.Value != Guid.Empty;
            if (!usesAdvancedSourcingControls &&
                (!tender.SourcePurchaseRequisitionId.HasValue ||
                 tender.SourcePurchaseRequisitionId.Value == Guid.Empty ||
                 !tender.SourcingReleaseId.HasValue ||
                 tender.SourcingReleaseId.Value == Guid.Empty))
            {
                throw new ProcurementRequisitionSourcingValidationException(
                    "TENDER_SOURCE_LINEAGE_REQUIRED",
                    "The published tender must retain its approved requisition and immutable sourcing-release lineage before bid submission.");
            }
            if (usesAdvancedSourcingControls)
            {
                await _tenderDocumentControlService.EnsureSubmissionReadyAsync(
                    ProcurementTenderDocumentSourceType.Tender, tender.Id, bid.BusinessPartnerId,
                    Guid.NewGuid().ToString("N"));
            }
            var usesControlledTenderLifecycle = await _tenderControlService.IsControlledTenderMethodAsync(tender.Id);
            if (!usesControlledTenderLifecycle && submittedAtUtc > tender.SubmissionDeadline)
            {
                throw new InvalidOperationException("Tender submission deadline has passed");
            }

            // Validate bid items before submission
            var items = await _bidItemRepository.GetByBidIdAsync(id);
            if (items == null || !items.Any())
            {
                throw new InvalidOperationException("Cannot submit bid without any items");
            }

            // Validate that all items have valid pricing
            var invalidItems = items.Where(item => item.UnitPrice <= 0 || item.OfferedQuantity <= 0).ToList();
            if (invalidItems.Any())
            {
                throw new InvalidOperationException("All bid items must have valid unit price (greater than 0) and offered quantity (greater than 0) before submission");
            }

            // Project-linked tenders must pass the shared QS intake gate. Non-project
            // procurement remains unchanged; the gate is a deliberate no-op there.
            await _quantitySurveyTenderBoqSubmissions.EnsureReadyForTenderSubmissionAsync(
                id,
                Guid.NewGuid().ToString("N"));

            bid.Status = "Submitted";
            bid.SubmittedDate = submittedAtUtc;
            bid.UpdatedAt = submittedAtUtc;

            await _bidRepository.UpdateAsync(bid);
            var documents = await _bidDocumentRepository.GetByBidIdAsync(id);
            var disposition = await _tenderControlService.RecordSubmissionAsync(
                bid, bid.SubmittedDate, Guid.NewGuid().ToString("N"));
            if (disposition == ProcurementTenderSubmissionDisposition.LateRejected)
            {
                bid.Status = "Rejected";
                bid.RejectionReason = "Late submission rejected by the statutory NCT/ICT deadline control.";
                await _bidRepository.UpdateAsync(bid);
            }
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Submitted bid {BidId}", id);

            // Send notification only for accepted submissions.
            if (disposition != ProcurementTenderSubmissionDisposition.LateRejected)
                await _notificationService.SendBidSubmittedNotificationAsync(id);

            // Publish events for admin-configurable notification topics (best-effort).
            try
            {
                var baseData = new Dictionary<string, object>
                {
                    ["BidId"] = bid.Id,
                    ["BidNumber"] = bid.BidNumber ?? string.Empty,
                    ["TenderId"] = bid.TenderId,
                    ["TenderNumber"] = tender.TenderNumber ?? string.Empty,
                    ["BusinessPartnerId"] = bid.BusinessPartnerId,
                    ["Status"] = bid.Status ?? string.Empty,
                    ["SubmittedDate"] = bid.SubmittedDate.ToString("o"),
                    ["TotalBidAmount"] = bid.TotalBidAmount,
                    ["Currency"] = bid.Currency ?? string.Empty,
                    ["PaymentAdmissionStatus"] = paymentAdmission.BlockingStatus.ToString(),
                    ["PaymentPendingVerification"] = paymentAdmission.PendingVerification
                };

                await _appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = bid.TenantId,
                    EntityType = "Bid",
                    Activity = "Submitted",
                    Audience = "Supplier",
                    EntityId = bid.Id,
                    TriggeredByUserId = _currentUserProvider.UserId,
                    Data = new Dictionary<string, object>(baseData)
                });

                await _appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = bid.TenantId,
                    EntityType = "Bid",
                    Activity = "Submitted",
                    Audience = "Internal",
                    EntityId = bid.Id,
                    TriggeredByUserId = _currentUserProvider.UserId,
                    Data = new Dictionary<string, object>(baseData)
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish Bid.Submitted entity activity event for bid {BidId}", bid.Id);
            }

            return MapToDetailDto(bid, tender, items, documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting bid {BidId}", id);
            throw;
        }
    }

    public async Task WithdrawBidAsync(Guid id, WithdrawTenderBidDto dto)
    {
        try
        {
            var bid = await _bidRepository.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Bid with ID {id} not found");

            if (bid.Status == "Awarded" || bid.Status == "Withdrawn")
            {
                throw new InvalidOperationException($"Cannot withdraw bid with status {bid.Status}");
            }

            bid.Status = "Withdrawn";
            bid.RejectionReason = dto.Reason;
            bid.UpdatedAt = DateTime.UtcNow;

            await _bidRepository.UpdateAsync(bid);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Withdrew bid {BidId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error withdrawing bid {BidId}", id);
            throw;
        }
    }

    public async Task<TenderBidDetailDto> OpenBidAsync(Guid id)
    {
        try
        {
            var bid = await _bidRepository.GetWithAllRelatedDataAsync(id)
                ?? throw new InvalidOperationException($"Bid with ID {id} not found");
            if (await _tenderControlService.IsControlledTenderMethodAsync(bid.TenderId))
                throw new ProcurementTenderControlConflictException(
                    "TENDER_STATUTORY_OPENING_REQUIRED",
                    "NCT, ICT, QBS, and QCBS bids can be opened only through the signed public-opening control.");
            var tender = bid.Tender ?? await _tenderRepository.GetByIdAsync(bid.TenderId)
                ?? throw new InvalidOperationException($"Tender with ID {bid.TenderId} not found");
            EnsureLegacyOpeningReady(tender, DateTime.UtcNow);
            await _tenderControlService.EnsureStandardOpeningReadyAsync(tender.Id);

            if (bid.Status != "Submitted")
            {
                throw new InvalidOperationException($"Only submitted bids can be opened. Current status: {bid.Status}");
            }

            var paymentAdmission = await GetPaymentAdmissionAsync(bid);
            if (!paymentAdmission.CanOpenOrEvaluate)
            {
                await AuditPaymentAdmissionDeniedAsync(bid, paymentAdmission, "Opening");
                if (paymentAdmission.BlockingStatus is TenderBidPaymentAdmissionStatus.Rejected or
                    TenderBidPaymentAdmissionStatus.EvidenceMissing)
                {
                    bid.Status = "Rejected";
                    bid.RejectionReason = paymentAdmission.Message;
                    bid.UpdatedAt = DateTime.UtcNow;
                    await _bidRepository.UpdateAsync(bid);
                    await _unitOfWork.SaveChangesAsync();
                }
                throw new TenderBidInitiationValidationException(
                    paymentAdmission.Code, paymentAdmission.Message);
            }

            bid.Status = "Opened";
            bid.OpenedDate = DateTime.UtcNow;
            bid.OpenedById = _currentUserProvider.UserId;
            bid.UpdatedAt = DateTime.UtcNow;

            await _bidRepository.UpdateAsync(bid);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Opened bid {BidId} by user {UserId}", id, _currentUserProvider.UserId);

            // Reload to get the updated data with navigation properties
            bid = await _bidRepository.GetWithAllRelatedDataAsync(id)
                ?? throw new InvalidOperationException($"Bid with ID {id} not found after update");

            return MapToDetailDto(bid, bid.Tender, bid.Items.ToList(), bid.Documents.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening bid {BidId}", id);
            throw;
        }
    }

    public async Task<int> OpenAllBidsByTenderAsync(Guid tenderId)
    {
        try
        {
            if (await _tenderControlService.IsControlledTenderMethodAsync(tenderId))
                throw new ProcurementTenderControlConflictException(
                    "TENDER_STATUTORY_OPENING_REQUIRED",
                    "NCT, ICT, QBS, and QCBS bids can be opened only through the signed public-opening control.");
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");
            EnsureLegacyOpeningReady(tender, DateTime.UtcNow);
            await _tenderControlService.EnsureStandardOpeningReadyAsync(tender.Id);
            var bids = await _bidRepository.GetByTenderIdAsync(tenderId);
            var submittedBids = bids.Where(b => b.Status == "Submitted").ToList();

            if (!submittedBids.Any())
            {
                return 0;
            }

            var decisions = new List<(TenderBid Bid, TenderBidPaymentAdmissionDecision Admission)>();
            foreach (var bid in submittedBids)
                decisions.Add((bid, await GetPaymentAdmissionAsync(bid)));

            var pending = decisions.FirstOrDefault(item =>
                item.Admission.BlockingStatus is TenderBidPaymentAdmissionStatus.PendingVerification or
                    TenderBidPaymentAdmissionStatus.PendingProviderConfirmation);
            if (pending.Bid is not null)
            {
                await AuditPaymentAdmissionDeniedAsync(pending.Bid, pending.Admission, "BulkOpening");
                throw new TenderBidInitiationValidationException(
                    pending.Admission.Code, pending.Admission.Message);
            }

            var excluded = decisions.Where(item => !item.Admission.CanOpenOrEvaluate).ToList();
            foreach (var item in excluded)
            {
                item.Bid.Status = "Rejected";
                item.Bid.RejectionReason = item.Admission.Message;
                item.Bid.UpdatedAt = DateTime.UtcNow;
                await _bidRepository.UpdateAsync(item.Bid);
                await AuditPaymentAdmissionDeniedAsync(item.Bid, item.Admission, "BulkOpening");
            }

            var admittedBids = decisions
                .Where(item => item.Admission.CanOpenOrEvaluate)
                .Select(item => item.Bid)
                .ToList();
            if (admittedBids.Count == 0)
            {
                await _unitOfWork.SaveChangesAsync();
                var first = excluded[0].Admission;
                throw new TenderBidInitiationValidationException(first.Code, first.Message);
            }

            var openedDate = DateTime.UtcNow;
            var openedById = _currentUserProvider.UserId;

            foreach (var bid in admittedBids)
            {
                bid.Status = "Opened";
                bid.OpenedDate = openedDate;
                bid.OpenedById = openedById;
                bid.UpdatedAt = openedDate;
                await _bidRepository.UpdateAsync(bid);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Opened {Count} bids for tender {TenderId} by user {UserId}",
                admittedBids.Count, tenderId, _currentUserProvider.UserId);

            return admittedBids.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening bids for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<List<SupplierBidListItemDto>> GetSupplierBidListByTenderAsync(Guid tenderId)
    {
        try
        {
            var bids = (await _bidRepository.GetByTenderIdAsync(tenderId))
                .OrderBy(bid => bid.SubmittedDate)
                .ToList();
            var result = new List<SupplierBidListItemDto>(bids.Count);
            foreach (var bid in bids)
            {
                var item = new SupplierBidListItemDto
                {
                    BidNumber = bid.BidNumber,
                    BusinessPartnerName = bid.BusinessPartner?.PartnerName ?? "Unknown",
                    BusinessPartnerCode = bid.BusinessPartner?.PartnerCode ?? "N/A",
                    SubmittedDate = bid.SubmittedDate,
                    Status = bid.Status,
                    OpenedDate = bid.OpenedDate,
                    TotalBidAmount = bid.TotalBidAmount,
                    Currency = bid.Currency
                };
                if (await ShouldConcealFinancialProposalAsync(tenderId, bid.Id))
                {
                    item.TotalBidAmount = 0m;
                    item.Currency = null;
                }
                result.Add(item);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting supplier bid list for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task DeleteBidAsync(Guid id)
    {
        try
        {
            var bid = await _bidRepository.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Bid with ID {id} not found");

            if (bid.Status != "Draft")
            {
                throw new InvalidOperationException("Only draft bids can be deleted");
            }

            await _bidRepository.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted bid {BidId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting bid {BidId}", id);
            throw;
        }
    }

    // Bid Items
    public async Task<TenderBidItemDto> AddBidItemAsync(Guid bidId, CreateTenderBidItemDto dto)
    {
        try
        {
            var bid = await _bidRepository.GetByIdAsync(bidId)
                ?? throw new InvalidOperationException($"Bid with ID {bidId} not found");

            if (bid.Status != "Draft")
            {
                throw new InvalidOperationException("Can only add items to draft bids");
            }

            var item = new TenderBidItem
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderBidId = bidId,
                TenderItemId = dto.TenderItemId,
                OfferedQuantity = dto.OfferedQuantity,
                UnitPrice = dto.UnitPrice,
                TotalPrice = dto.OfferedQuantity * dto.UnitPrice,
                DeliveryDays = dto.DeliveryDays,
                Specifications = dto.Specifications,
                Brand = dto.Brand,
                Model = dto.Model,
                TechnicalDetails = dto.TechnicalDetails,
                CreatedAt = DateTime.UtcNow
            };

            await _bidItemRepository.CreateAsync(item);

            // Update total bid amount
            var allItems = await _bidItemRepository.GetByBidIdAsync(bidId);
            bid.TotalBidAmount = allItems.Sum(i => i.TotalPrice) + item.TotalPrice;
            await _bidRepository.UpdateAsync(bid);

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Added item {ItemId} to bid {BidId}", item.Id, bidId);

            return MapBidItemToDto(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item to bid {BidId}", bidId);
            throw;
        }
    }

    public async Task<TenderBidItemDto> UpdateBidItemAsync(Guid itemId, CreateTenderBidItemDto dto)
    {
        try
        {
            var item = await _bidItemRepository.GetByIdAsync(itemId)
                ?? throw new InvalidOperationException($"Item with ID {itemId} not found");

            var bid = await _bidRepository.GetByIdAsync(item.TenderBidId);
            if (bid != null && bid.Status != "Draft")
            {
                throw new InvalidOperationException("Can only update items in draft bids");
            }

            item.TenderItemId = dto.TenderItemId;
            item.OfferedQuantity = dto.OfferedQuantity;
            item.UnitPrice = dto.UnitPrice;
            item.TotalPrice = dto.OfferedQuantity * dto.UnitPrice;
            item.DeliveryDays = dto.DeliveryDays;
            item.Specifications = dto.Specifications;
            item.Brand = dto.Brand;
            item.Model = dto.Model;
            item.TechnicalDetails = dto.TechnicalDetails;
            item.UpdatedAt = DateTime.UtcNow;

            await _bidItemRepository.UpdateAsync(item);

            // Update total bid amount
            if (bid != null)
            {
                var allItems = await _bidItemRepository.GetByBidIdAsync(bid.Id);
                bid.TotalBidAmount = allItems.Sum(i => i.TotalPrice);
                await _bidRepository.UpdateAsync(bid);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated bid item {ItemId}", itemId);

            return MapBidItemToDto(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating bid item {ItemId}", itemId);
            throw;
        }
    }

    public async Task DeleteBidItemAsync(Guid itemId)
    {
        try
        {
            var item = await _bidItemRepository.GetByIdAsync(itemId);
            if (item != null)
            {
                var bid = await _bidRepository.GetByIdAsync(item.TenderBidId);
                if (bid != null && bid.Status != "Draft")
                {
                    throw new InvalidOperationException("Can only delete items from draft bids");
                }

                await _bidItemRepository.DeleteAsync(itemId);

                // Update total bid amount
                if (bid != null)
                {
                    var allItems = await _bidItemRepository.GetByBidIdAsync(bid.Id);
                    bid.TotalBidAmount = allItems.Sum(i => i.TotalPrice);
                    await _bidRepository.UpdateAsync(bid);
                }

                await _unitOfWork.SaveChangesAsync();
            }

            _logger.LogInformation("Deleted bid item {ItemId}", itemId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting bid item {ItemId}", itemId);
            throw;
        }
    }

    // Bid Documents
    public async Task<TenderBidDocumentDto> UploadBidDocumentAsync(Guid bidId, UploadBidDocumentDto dto,
        string logicalFileReference, string? fileType, long? fileSize, Guid fileUploadRecordId,
        Guid centralDocumentRecordId, Guid centralDocumentVersionId)
    {
        try
        {
            var bid = await _bidRepository.GetByIdAsync(bidId)
                ?? throw new InvalidOperationException($"Bid with ID {bidId} not found");

            var document = new TenderBidDocument
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderBidId = bidId,
                DocumentName = dto.DocumentName,
                DocumentType = dto.DocumentType,
                FilePath = logicalFileReference,
                FileType = fileType,
                FileSize = fileSize,
                UploadedDate = DateTime.UtcNow,
                UploadedById = _currentUserProvider.UserId,
                FileUploadRecordId = fileUploadRecordId,
                CentralDocumentRecordId = centralDocumentRecordId,
                CentralDocumentVersionId = centralDocumentVersionId,
                CreatedAt = DateTime.UtcNow
            };

            await _bidDocumentRepository.CreateAsync(document);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Uploaded document {DocumentId} to bid {BidId}", document.Id, bidId);

            return MapBidDocumentToDto(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document to bid {BidId}", bidId);
            throw;
        }
    }

    public async Task DeleteBidDocumentAsync(Guid documentId)
    {
        try
        {
            await _bidDocumentRepository.DeleteAsync(documentId);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted bid document {DocumentId}", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting bid document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderBidDocumentDto>> GetBidDocumentsAsync(Guid bidId)
    {
        try
        {
            var documents = await _bidDocumentRepository.GetByBidIdAsync(bidId);
            return documents.Select(MapBidDocumentToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving documents for bid {BidId}", bidId);
            throw;
        }
    }

    // Bid Payments
    public async Task<TenderPaymentDto> RecordPaymentAsync(CreateTenderPaymentDto dto)
    {
        try
        {
            var businessPartner = await GetCurrentBusinessPartnerAsync()
                ?? throw new TenderBidInitiationValidationException(
                    "TENDER_BID_SUPPLIER_REQUIRED",
                    "The current account is not linked to an active supplier.");
            if (!dto.TenderBidId.HasValue || dto.TenderBidId.Value == Guid.Empty)
                throw new TenderBidInitiationValidationException(
                    "TENDER_BID_PAYMENT_BID_REQUIRED",
                    "Create the tender bid draft before recording its fee payment.");
            var bid = await _bidRepository.GetByIdAsync(dto.TenderBidId.Value)
                ?? throw new TenderBidInitiationValidationException(
                    "TENDER_BID_PAYMENT_BID_NOT_FOUND", "The tender bid draft was not found.");
            if (bid.BusinessPartnerId != businessPartner.Id || !string.Equals(bid.Status, "Draft", StringComparison.OrdinalIgnoreCase))
                throw new TenderBidInitiationValidationException(
                    "TENDER_BID_PAYMENT_BID_INVALID",
                    "Fee payment can be recorded only against the current supplier's draft bid.");
            var fee = await _feeRepository.GetByIdAsync(dto.TenderFeeId)
                ?? throw new TenderBidInitiationValidationException(
                    "TENDER_BID_PAYMENT_FEE_NOT_FOUND", "The selected tender fee was not found.");
            if (fee.TenderId != bid.TenderId || fee.Amount <= 0m)
                throw new TenderBidInitiationValidationException(
                    "TENDER_BID_PAYMENT_FEE_INVALID",
                    "The selected fee does not belong to this tender or does not require payment.");
            var existingPayments = await _paymentRepository.GetByBusinessPartnerIdAsync(businessPartner.Id);
            var existing = existingPayments
                .Where(item => !item.IsDeleted && item.TenderFeeId == fee.Id)
                .OrderByDescending(item => item.PaymentDate)
                .FirstOrDefault();
            if (existing is not null &&
                (IsPaymentSatisfied(existing) || string.Equals(existing.Status, "Pending", StringComparison.OrdinalIgnoreCase)))
                return MapPaymentToDto(existing);
            var transactionReference = string.IsNullOrWhiteSpace(dto.TransactionId)
                ? dto.PaymentReference?.Trim()
                : dto.TransactionId.Trim();
            if (string.IsNullOrWhiteSpace(transactionReference))
                throw new TenderBidInitiationValidationException(
                    "TENDER_BID_PAYMENT_REFERENCE_REQUIRED",
                    "Enter the bank, receipt, or transaction reference for this fee payment.");

            var payment = new TenderPayment
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderFeeId = dto.TenderFeeId,
                BusinessPartnerId = businessPartner.Id,
                PaymentReference = await _paymentRepository.GeneratePaymentReferenceAsync(),
                Amount = fee.Amount,
                Currency = fee.Currency,
                PaymentMethod = fee.PaymentMethod,
                Status = "Pending",
                PaymentDate = dto.PaymentDate ?? DateTime.UtcNow,
                TransactionId = transactionReference,
                PaymentProof = string.IsNullOrWhiteSpace(dto.PaymentProof)
                    ? transactionReference
                    : dto.PaymentProof.Trim(),
                Notes = dto.Notes,
                CreatedAt = DateTime.UtcNow
            };

            await _paymentRepository.CreateAsync(payment);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Recorded payment {PaymentId}", payment.Id);

            return MapPaymentToDto(payment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording payment");
            throw;
        }
    }

    public async Task<TenderPaymentDto> VerifyPaymentAsync(
        Guid bidId,
        Guid paymentId,
        VerifyPaymentDto dto)
    {
        try
        {
            TenderBid? committedBid = null;
            TenderFee? committedFee = null;
            TenderPayment? committedPayment = null;
            var publishDecisionAudit = false;

            await _unitOfWork.ExecuteInStrategyAsync(async () =>
            {
                await _unitOfWork.BeginTransactionAsync();
                try
                {
                    await _unitOfWork.AcquireTransactionLockAsync(
                        $"procurement:tender-fee-payment:{paymentId:N}");

                    var bid = await _bidRepository.GetByIdAsync(bidId)
                        ?? throw new TenderBidInitiationValidationException(
                            "TENDER_BID_PAYMENT_BID_NOT_FOUND", "The tender bid was not found.");
                    var payment = await _paymentRepository.GetByIdAsync(paymentId)
                        ?? throw new TenderBidInitiationValidationException(
                            "TENDER_BID_PAYMENT_NOT_FOUND", "The tender fee payment was not found.");
                    var fee = await _feeRepository.GetByIdAsync(payment.TenderFeeId)
                        ?? throw new TenderBidInitiationValidationException(
                            "TENDER_BID_PAYMENT_FEE_NOT_FOUND", "The tender fee was not found.");

                    if (bid.TenantId != _currentUserProvider.TenantId ||
                        payment.TenantId != _currentUserProvider.TenantId ||
                        fee.TenantId != _currentUserProvider.TenantId ||
                        payment.BusinessPartnerId != bid.BusinessPartnerId ||
                        fee.TenderId != bid.TenderId)
                        throw new TenderBidInitiationValidationException(
                            "TENDER_BID_PAYMENT_ROUTE_MISMATCH",
                            "The payment does not belong to this bid, supplier, tender, and tenant.");

                    var targetStatus = dto.IsApproved ? "Verified" : "Rejected";
                    var alreadyAtTarget = string.Equals(
                        payment.Status,
                        targetStatus,
                        StringComparison.OrdinalIgnoreCase);
                    var hasCompletePostingLineage =
                        payment.PostingEventId.HasValue &&
                        payment.JournalEntryId.HasValue &&
                        payment.PostedAtUtc.HasValue;

                    if (alreadyAtTarget && (!dto.IsApproved || hasCompletePostingLineage))
                    {
                        committedBid = bid;
                        committedFee = fee;
                        committedPayment = payment;
                        await _unitOfWork.CommitAsync();
                        return;
                    }

                    // A Verified legacy row without Finance lineage is deliberately repairable:
                    // the stable payment id makes this backfill idempotent. Every other terminal
                    // decision remains immutable.
                    if (!alreadyAtTarget &&
                        !string.Equals(payment.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                        throw new TenderBidInitiationValidationException(
                            "TENDER_BID_PAYMENT_DECISION_FINAL",
                            $"A {payment.Status} payment decision cannot be changed through verification.");

                    if (!dto.IsApproved &&
                        (payment.PostingEventId.HasValue || payment.JournalEntryId.HasValue))
                        throw new TenderBidInitiationValidationException(
                            "TENDER_BID_PAYMENT_ALREADY_POSTED",
                            "A Finance-posted tender fee payment cannot be rejected; use a controlled Finance reversal.");

                    var decidedAtUtc = DateTime.UtcNow;
                    if (dto.IsApproved)
                    {
                        if (_financePostingEngine is null)
                            throw new TenderBidInitiationValidationException(
                                "TENDER_BID_PAYMENT_FINANCE_UNAVAILABLE",
                                "Central Finance posting is not configured for tender fee verification.");

                        var postingAccounts = await ValidateTenderFeePostingConfigurationAsync(
                            bid,
                            fee,
                            payment);
                        var posting = await _financePostingEngine.PostAsync(
                            BuildTenderFeePostingRequest(
                                bid,
                                fee,
                                payment,
                                postingAccounts,
                                decidedAtUtc));

                        // These fields are changed only after Finance has completed successfully.
                        // Both Finance and the payment decision share this transaction, so neither
                        // side can commit without the other.
                        payment.PostingEventId = posting.PostingEventId;
                        payment.JournalEntryId = posting.JournalEntryId;
                        payment.PostedAtUtc = decidedAtUtc;
                    }

                    payment.Status = targetStatus;
                    payment.VerifiedDate = decidedAtUtc;
                    payment.VerifiedById = _currentUserProvider.UserId;
                    payment.Notes = string.Join(Environment.NewLine,
                        new[] { payment.Notes, dto.Notes }
                            .Where(value => !string.IsNullOrWhiteSpace(value)));
                    payment.UpdatedAt = decidedAtUtc;

                    await _paymentRepository.UpdateAsync(payment);
                    await _unitOfWork.SaveChangesAsync();
                    await _unitOfWork.CommitAsync();

                    committedBid = bid;
                    committedFee = fee;
                    committedPayment = payment;
                    publishDecisionAudit = true;
                }
                catch
                {
                    await _unitOfWork.RollbackAsync();
                    _unitOfWork.ClearTrackedChanges();
                    throw;
                }
            });

            var savedBid = committedBid
                ?? throw new InvalidOperationException("Tender payment verification did not load its bid.");
            var savedFee = committedFee
                ?? throw new InvalidOperationException("Tender payment verification did not load its fee.");
            var savedPayment = committedPayment
                ?? throw new InvalidOperationException("Tender payment verification did not produce a payment result.");

            _logger.LogInformation(
                "Tender payment {PaymentId} decision {PaymentStatus}; Finance posting {PostingEventId}",
                paymentId,
                savedPayment.Status,
                savedPayment.PostingEventId);

            if (publishDecisionAudit)
            {
                try
                {
                    await _appEventBus.PublishAsync(new EntityActivityEvent
                    {
                        TenantId = savedBid.TenantId,
                        EntityType = "Bid",
                        Activity = dto.IsApproved ? "PaymentVerified" : "PaymentRejected",
                        Audience = "Internal",
                        EntityId = savedBid.Id,
                        TriggeredByUserId = _currentUserProvider.UserId,
                        Data = new Dictionary<string, object>
                        {
                            ["BidId"] = savedBid.Id,
                            ["TenderId"] = savedBid.TenderId,
                            ["BusinessPartnerId"] = savedBid.BusinessPartnerId,
                            ["TenderFeeId"] = savedFee.Id,
                            ["PaymentId"] = savedPayment.Id,
                            ["PaymentStatus"] = savedPayment.Status,
                            ["PostingEventId"] = savedPayment.PostingEventId?.ToString() ?? string.Empty,
                            ["JournalEntryId"] = savedPayment.JournalEntryId?.ToString() ?? string.Empty,
                            ["DecisionNotes"] = dto.Notes ?? string.Empty
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to publish tender payment decision audit for payment {PaymentId}", paymentId);
                }
            }

            return MapPaymentToDto(savedPayment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying payment {PaymentId}", paymentId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderPaymentDto>> GetBidPaymentsAsync(Guid bidId)
    {
        try
        {
            var bid = await _bidRepository.GetByIdAsync(bidId);
            if (bid == null) return Enumerable.Empty<TenderPaymentDto>();
            if (bid.TenantId != _currentUserProvider.TenantId)
                return Enumerable.Empty<TenderPaymentDto>();
            if (_currentUserProvider.IsExternalUser)
            {
                var currentPartner = await GetCurrentBusinessPartnerAsync();
                if (currentPartner?.Id != bid.BusinessPartnerId)
                    throw new UnauthorizedAccessException(
                        "A supplier can view tender-fee payments only for its own bid.");
            }

            var feeIds = (await _feeRepository.GetByTenderIdAsync(bid.TenderId))
                .Where(item => !item.IsDeleted && item.TenantId == bid.TenantId)
                .Select(item => item.Id)
                .ToHashSet();
            var payments = await _paymentRepository.GetByBusinessPartnerIdAsync(bid.BusinessPartnerId);
            return payments
                .Where(item => item.TenantId == bid.TenantId && feeIds.Contains(item.TenderFeeId))
                .Select(MapPaymentToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payments for bid {BidId}", bidId);
            throw;
        }
    }

    // Bid Interviews
    public async Task<TenderInterviewDto> ScheduleInterviewAsync(ScheduleInterviewDto dto)
    {
        try
        {
            var bid = await _bidRepository.GetByIdAsync(dto.TenderBidId)
                ?? throw new InvalidOperationException($"Bid with ID {dto.TenderBidId} not found");

            var interview = new TenderInterview
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderId = bid.TenderId,
                TenderBidId = dto.TenderBidId,
                Title = dto.Title,
                ScheduledDate = dto.ScheduledDate,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                Location = dto.Location,
                InterviewType = dto.InterviewType,
                PanelMembers = dto.PanelMembers,
                Agenda = dto.Agenda,
                Status = "Scheduled",
                ConductedById = _currentUserProvider.UserId,
                CreatedAt = DateTime.UtcNow
            };

            await _interviewRepository.CreateAsync(interview);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Scheduled interview {InterviewId} for bid {BidId}", interview.Id, dto.TenderBidId);

            // Send notification
            await _notificationService.SendInterviewScheduledNotificationAsync(interview.Id);

            return MapInterviewToDto(interview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling interview for bid {BidId}", dto.TenderBidId);
            throw;
        }
    }

    public async Task<TenderInterviewDto> UpdateInterviewAsync(Guid interviewId, ScheduleInterviewDto dto)
    {
        try
        {
            var interview = await _interviewRepository.GetByIdAsync(interviewId)
                ?? throw new InvalidOperationException($"Interview with ID {interviewId} not found");

            interview.Title = dto.Title;
            interview.ScheduledDate = dto.ScheduledDate;
            interview.StartTime = dto.StartTime;
            interview.EndTime = dto.EndTime;
            interview.Location = dto.Location;
            interview.InterviewType = dto.InterviewType;
            interview.PanelMembers = dto.PanelMembers;
            interview.Agenda = dto.Agenda;
            interview.UpdatedAt = DateTime.UtcNow;

            await _interviewRepository.UpdateAsync(interview);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated interview {InterviewId}", interviewId);

            return MapInterviewToDto(interview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating interview {InterviewId}", interviewId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderInterviewDto>> GetBidInterviewsAsync(Guid bidId)
    {
        try
        {
            var interviews = await _interviewRepository.GetByBidIdAsync(bidId);
            return interviews.Select(MapInterviewToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving interviews for bid {BidId}", bidId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderInterviewDto>> GetUpcomingInterviewsAsync()
    {
        try
        {
            var interviews = await _interviewRepository.GetUpcomingInterviewsAsync();
            return interviews.Select(MapInterviewToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving upcoming interviews");
            throw;
        }
    }

    // Helper methods
    private async Task<string> GenerateBidNumberAsync(string tenderNumber)
    {
        return await _bidRepository.GenerateBidNumberAsync();
    }

    private async Task<bool> ShouldConcealFinancialProposalAsync(Guid tenderId, Guid bidId) =>
        !_currentUserProvider.IsExternalUser &&
        await _tenderControlService.ShouldConcealFinancialProposalAsync(tenderId, bidId);

    private async Task<TenderBidSummaryDto> ProtectFinancialProposalAsync(TenderBidSummaryDto value)
    {
        if (!await ShouldConcealFinancialProposalAsync(value.TenderId, value.Id)) return value;
        value.TotalBidAmount = 0m;
        value.Currency = null;
        value.FinancialScore = null;
        value.CombinedScore = null;
        return value;
    }

    private async Task<TenderBidDetailDto> ProtectFinancialProposalAsync(TenderBidDetailDto value)
    {
        if (!await ShouldConcealFinancialProposalAsync(value.TenderId, value.Id)) return value;
        value.TotalBidAmount = 0m;
        value.Currency = null;
        value.PaymentTerms = null;
        value.CommercialProposal = null;
        value.PriceScore = null;
        value.FinancialScore = null;
        value.CombinedScore = null;
        value.Evaluations.Clear();
        foreach (var item in value.Items)
        {
            item.UnitPrice = 0m;
            item.TotalPrice = 0m;
        }
        value.Documents = value.Documents.Where(IsExplicitTechnicalProposalDocument).ToList();
        return value;
    }

    private static bool IsExplicitTechnicalProposalDocument(TenderBidDocumentDto document)
    {
        var marker = $"{document.DocumentType} {document.DocumentName}";
        return marker.Contains("technical", StringComparison.OrdinalIgnoreCase) ||
               marker.Contains("qualification", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<TenderPayment?> GetSatisfiedPaymentForBidAsync(TenderBid bid)
    {
        var tenderFeeIds = (await _feeRepository.GetByTenderIdAsync(bid.TenderId))
            .Where(fee => !fee.IsDeleted && fee.TenantId == bid.TenantId)
            .Select(fee => fee.Id)
            .ToHashSet();
        if (tenderFeeIds.Count == 0) return null;

        return (await _paymentRepository.GetByBusinessPartnerIdAsync(bid.BusinessPartnerId))
            .Where(payment =>
                !payment.IsDeleted &&
                payment.TenantId == bid.TenantId &&
                payment.BusinessPartnerId == bid.BusinessPartnerId &&
                tenderFeeIds.Contains(payment.TenderFeeId) &&
                IsPaymentSatisfied(payment))
            .OrderByDescending(payment => payment.VerifiedDate ?? payment.PaymentDate)
            .FirstOrDefault();
    }

    private async Task<TenderBidPaymentAdmissionDecision> GetPaymentAdmissionAsync(TenderBid bid)
    {
        var fees = await _feeRepository.GetByTenderIdAsync(bid.TenderId);
        var payments = await _paymentRepository.GetByBusinessPartnerIdAsync(bid.BusinessPartnerId);
        return TenderBidPaymentRules.Assess(bid, fees, payments);
    }

    private async Task AuditPaymentAdmissionDeniedAsync(
        TenderBid bid,
        TenderBidPaymentAdmissionDecision admission,
        string stage)
    {
        try
        {
            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = bid.TenantId,
                EntityType = "Bid",
                Activity = "PaymentAdmissionDenied",
                Audience = "Internal",
                EntityId = bid.Id,
                TriggeredByUserId = _currentUserProvider.UserId,
                Data = new Dictionary<string, object>
                {
                    ["BidId"] = bid.Id,
                    ["TenderId"] = bid.TenderId,
                    ["BusinessPartnerId"] = bid.BusinessPartnerId,
                    ["Stage"] = stage,
                    ["Code"] = admission.Code,
                    ["PaymentAdmissionStatus"] = admission.BlockingStatus.ToString(),
                    ["TenderFeeIds"] = admission.Fees.Select(item => item.Fee.Id).ToArray(),
                    ["PaymentIds"] = admission.Fees
                        .Where(item => item.Payment is not null)
                        .Select(item => item.Payment!.Id)
                        .ToArray()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to publish payment admission denial audit for bid {BidId}", bid.Id);
        }
    }

    // Mapping methods
    private static TenderBidSummaryDto MapToSummaryDto(TenderBid bid, Tender? tender, TenderPayment? payment)
    {
        return new TenderBidSummaryDto
        {
            Id = bid.Id,
            BidNumber = bid.BidNumber,
            TenderId = bid.TenderId,
            TenderNumber = tender?.TenderNumber ?? string.Empty,
            TenderTitle = tender?.Title ?? string.Empty,
            BusinessPartnerId = bid.BusinessPartnerId,
            BusinessPartnerName = bid.BusinessPartner?.PartnerName ?? string.Empty,
            SubmittedDate = bid.SubmittedDate,
            TotalBidAmount = bid.TotalBidAmount,
            Currency = bid.Currency,
            Status = bid.Status,
            TotalScore = bid.TotalScore,
            Rank = bid.Rank,
            HasPaidFees = payment != null && IsPaymentSatisfied(payment),
            PaymentStatus = payment?.Status,
            // QCBS Scores
            TechnicalScore = bid.TechnicalScore,
            FinancialScore = bid.FinancialScore,
            CombinedScore = bid.CombinedScore,
            IsQualifiedTechnically = bid.IsQualifiedTechnically,
            DisqualificationReason = bid.DisqualificationReason
        };
    }

    private static TenderBidDetailDto MapToDetailDto(TenderBid bid, Tender? tender, IEnumerable<TenderBidItem> items, IEnumerable<TenderBidDocument> documents)
    {
        return new TenderBidDetailDto
        {
            Id = bid.Id,
            BidNumber = bid.BidNumber,
            TenderId = bid.TenderId,
            TenderNumber = tender?.TenderNumber ?? string.Empty,
            TenderTitle = tender?.Title ?? string.Empty,
            BusinessPartnerId = bid.BusinessPartnerId,
            BusinessPartnerName = bid.BusinessPartner?.PartnerName ?? string.Empty,
            SubmittedDate = bid.SubmittedDate,
            Status = bid.Status,
            TotalBidAmount = bid.TotalBidAmount,
            Currency = bid.Currency,
            DeliveryDays = bid.DeliveryDays,
            PaymentTerms = bid.PaymentTerms,
            WarrantyTerms = bid.WarrantyTerms,
            TechnicalProposal = bid.TechnicalProposal,
            CommercialProposal = bid.CommercialProposal,
            AssociationType = bid.AssociationType,
            AcceptedDeclaration = bid.AcceptedDeclaration,
            DeclarationAcceptedAt = bid.DeclarationAcceptedAt,
            IsCompliant = bid.IsCompliant,
            NonComplianceReasons = bid.NonComplianceReasons,
            EvaluationTemplateId = tender?.EvaluationTemplateId,
            EvaluationTemplateName = tender?.EvaluationTemplate?.TemplateName,
            PriceScore = bid.PriceScore,
            QualityScore = bid.QualityScore,
            DeliveryScore = bid.DeliveryScore,
            ExperienceScore = bid.ExperienceScore,
            TotalScore = bid.TotalScore,
            Rank = bid.Rank,
            // QCBS Scores
            TechnicalScore = bid.TechnicalScore,
            FinancialScore = bid.FinancialScore,
            CombinedScore = bid.CombinedScore,
            IsQualifiedTechnically = bid.IsQualifiedTechnically,
            DisqualificationReason = bid.DisqualificationReason,
            OpenedDate = bid.OpenedDate,
            OpenedByName = bid.OpenedBy != null ? $"{bid.OpenedBy.FirstName} {bid.OpenedBy.LastName}" : null,
            EvaluationNotes = bid.EvaluationNotes,
            RejectionReason = bid.RejectionReason,
            CreatedAt = bid.CreatedAt,
            UpdatedAt = bid.UpdatedAt ?? bid.CreatedAt,
            SelectedLotIds = bid.BidLots?
                .Where(lot => !lot.IsDeleted)
                .Select(lot => lot.LotId)
                .Distinct()
                .ToList() ?? new(),
            BidLots = bid.BidLots?
                .Where(lot => !lot.IsDeleted)
                .Select(MapToBidLotDto)
                .ToList() ?? new(),
            BidLotCount = bid.BidLots?.Count(lot => !lot.IsDeleted) ?? 0,
            Items = items.Select(MapBidItemToDto).ToList(),
            Documents = documents.Select(MapBidDocumentToDto).ToList(),
            Evaluations = bid.Evaluations?.Select(MapEvaluationToDto).ToList() ?? new(),
            Interviews = bid.Interviews?.Select(MapInterviewToDto).ToList() ?? new()
        };
    }

    private static TenderBidItemDto MapBidItemToDto(TenderBidItem item)
    {
        return new TenderBidItemDto
        {
            Id = item.Id,
            TenderBidId = item.TenderBidId,
            BidLotId = item.BidLotId,
            LotCode = item.TenderItem?.Lot?.LotCode,
            TenderItemId = item.TenderItemId,
            TenderItemDescription = item.TenderItem?.Description ?? string.Empty,
            RequestedQuantity = item.TenderItem?.Quantity ?? 0,
            OfferedQuantity = item.OfferedQuantity,
            UnitOfMeasure = item.TenderItem?.UnitOfMeasure,
            UnitPrice = item.UnitPrice,
            TotalPrice = item.TotalPrice,
            DeliveryDays = item.DeliveryDays,
            Specifications = item.Specifications,
            Brand = item.Brand,
            Model = item.Model,
            TechnicalDetails = item.TechnicalDetails
        };
    }

    private static TenderBidDocumentDto MapBidDocumentToDto(TenderBidDocument document)
    {
        return new TenderBidDocumentDto
        {
            Id = document.Id,
            TenderBidId = document.TenderBidId,
            DocumentName = document.DocumentName,
            DocumentType = document.DocumentType,
            FilePath = document.FilePath,
            FileType = document.FileType,
            FileSize = document.FileSize,
            UploadedDate = document.UploadedDate,
            UploadedByName = string.Empty, // Would need to fetch from User entity
            FileUploadRecordId = document.FileUploadRecordId,
            CentralDocumentRecordId = document.CentralDocumentRecordId,
            CentralDocumentVersionId = document.CentralDocumentVersionId
        };
    }

    private async Task<TenderFeePostingAccounts> ValidateTenderFeePostingConfigurationAsync(
        TenderBid bid,
        TenderFee fee,
        TenderPayment payment)
    {
        if (!fee.ReceivingAccountId.HasValue || fee.ReceivingAccountId == Guid.Empty)
            throw new TenderBidInitiationValidationException(
                "TENDER_FEE_RECEIVING_ACCOUNT_REQUIRED",
                "The tender fee has no configured receiving GL account.");
        if (!fee.RevenueAccountId.HasValue || fee.RevenueAccountId == Guid.Empty)
            throw new TenderBidInitiationValidationException(
                "TENDER_FEE_REVENUE_ACCOUNT_REQUIRED",
                "The tender fee has no configured revenue GL account.");
        if (fee.ReceivingAccountId == fee.RevenueAccountId)
            throw new TenderBidInitiationValidationException(
                "TENDER_FEE_POSTING_ACCOUNTS_INVALID",
                "Tender fee receiving and revenue accounts must be different.");
        if (payment.Amount <= 0 || payment.Amount != fee.Amount)
            throw new TenderBidInitiationValidationException(
                "TENDER_FEE_PAYMENT_AMOUNT_MISMATCH",
                "The payment amount does not match the tender fee amount.");

        var paymentCurrency = payment.Currency?.Trim().ToUpperInvariant();
        var feeCurrency = fee.Currency?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(paymentCurrency) ||
            paymentCurrency.Length != 3 ||
            !string.Equals(paymentCurrency, feeCurrency, StringComparison.Ordinal))
            throw new TenderBidInitiationValidationException(
                "TENDER_FEE_PAYMENT_CURRENCY_MISMATCH",
                "The payment currency does not match the tender fee currency.");

        var accountIds = new[]
        {
            fee.ReceivingAccountId.Value,
            fee.RevenueAccountId.Value
        };
        var accounts = await _unitOfWork.Repository<Account>()
            .GetQueryable(account =>
                account.TenantId == bid.TenantId &&
                accountIds.Contains(account.Id) &&
                !account.IsDeleted)
            .AsNoTracking()
            .ToListAsync();
        if (accounts.Count != accountIds.Length)
            throw new TenderBidInitiationValidationException(
                "TENDER_FEE_POSTING_ACCOUNT_TENANT_MISMATCH",
                "One or more tender fee posting accounts do not belong to this tenant.");

        var receivingAccount = accounts.Single(account => account.Id == fee.ReceivingAccountId);
        if (receivingAccount.Status != AccountStatus.Active ||
            receivingAccount.AccountType != AccountType.Asset ||
            !receivingAccount.AllowDirectPosting ||
            receivingAccount.IsControlAccount)
            throw new TenderBidInitiationValidationException(
                "TENDER_FEE_RECEIVING_ACCOUNT_INVALID",
                "The tender fee receiving account must be an active, direct-posting, non-control asset account.");

        var revenueAccount = accounts.Single(account => account.Id == fee.RevenueAccountId);
        if (revenueAccount.Status != AccountStatus.Active ||
            revenueAccount.AccountType != AccountType.Revenue ||
            !revenueAccount.AllowDirectPosting ||
            revenueAccount.IsControlAccount)
            throw new TenderBidInitiationValidationException(
                "TENDER_FEE_REVENUE_ACCOUNT_INVALID",
                "The tender fee revenue account must be an active, direct-posting, non-control revenue account.");

        var settings = await _unitOfWork.Repository<FinanceSettings>()
            .GetQueryable(item => item.TenantId == bid.TenantId && !item.IsDeleted)
            .AsNoTracking()
            .FirstOrDefaultAsync();
        var functionalCurrency = settings?.BaseCurrency?.Trim().ToUpperInvariant() ?? "GHS";
        if (!string.Equals(paymentCurrency, functionalCurrency, StringComparison.Ordinal))
            throw new TenderBidInitiationValidationException(
                "TENDER_FEE_FOREIGN_CURRENCY_NOT_SUPPORTED",
                "Tender fee payments must use the tenant functional currency until a controlled exchange-rate snapshot is provided.");

        return new TenderFeePostingAccounts(
            receivingAccount.Id,
            revenueAccount.Id,
            functionalCurrency);
    }

    private static FinancePostingRequestDto BuildTenderFeePostingRequest(
        TenderBid bid,
        TenderFee fee,
        TenderPayment payment,
        TenderFeePostingAccounts accounts,
        DateTime verifiedAtUtc)
    {
        var reference = payment.PaymentReference.Trim();
        var description = $"Tender fee {fee.FeeType} for bid {bid.BidNumber}";
        return new FinancePostingRequestDto
        {
            SourceModule = "Procurement",
            OriginModuleCode = "PROC",
            SourceDocumentType = "TenderFeePayment",
            SourceDocumentId = payment.Id,
            SourceDocumentTenantId = bid.TenantId,
            SourceDocumentReference = reference,
            PostingAction = "Post",
            PostingDate = verifiedAtUtc.Date,
            Description = description,
            JournalType = "System Generated",
            FunctionalCurrencyCode = accounts.FunctionalCurrencyCode,
            IdempotencyKey = $"PROCUREMENT|TENDER-FEE|{payment.Id:N}|POST",
            ReturnExistingOnDuplicate = true,
            Lines =
            [
                new FinancePostingLineDto
                {
                    AccountId = accounts.ReceivingAccountId,
                    Description = $"Tender fee receipt {payment.PaymentReference}",
                    DebitAmount = payment.Amount,
                    CreditAmount = 0,
                    TransactionCurrency = payment.Currency,
                    SourceReferenceNumber = reference,
                    LineNumber = 1,
                    TransactionTag = "TenderFeeReceipt"
                },
                new FinancePostingLineDto
                {
                    AccountId = accounts.RevenueAccountId,
                    Description = description,
                    DebitAmount = 0,
                    CreditAmount = payment.Amount,
                    TransactionCurrency = payment.Currency,
                    SourceReferenceNumber = reference,
                    LineNumber = 2,
                    TransactionTag = "TenderFeeRevenue"
                }
            ]
        };
    }

    private sealed record TenderFeePostingAccounts(
        Guid ReceivingAccountId,
        Guid RevenueAccountId,
        string FunctionalCurrencyCode);

    private static TenderPaymentDto MapPaymentToDto(TenderPayment payment)
    {
        return new TenderPaymentDto
        {
            Id = payment.Id,
            TenderFeeId = payment.TenderFeeId,
            BusinessPartnerId = payment.BusinessPartnerId,
            BusinessPartnerName = string.Empty, // Would need to fetch from BusinessPartner entity
            PaymentReference = payment.PaymentReference,
            Amount = payment.Amount,
            Currency = payment.Currency,
            PaymentMethod = payment.PaymentMethod,
            Status = payment.Status,
            PaymentDate = payment.PaymentDate,
            VerifiedDate = payment.VerifiedDate,
            VerifiedByName = string.Empty, // Would need to fetch from User entity
            PostingEventId = payment.PostingEventId,
            JournalEntryId = payment.JournalEntryId,
            PostedAtUtc = payment.PostedAtUtc,
            TransactionId = payment.TransactionId,
            PaymentProof = payment.PaymentProof
        };
    }

    internal static TenderBidInitiationStatusDto BuildInitiationStatus(
        Tender tender,
        Guid businessPartnerId,
        TenderAssignment? assignment,
        TenderBid? bid,
        IReadOnlyCollection<TenderFee> fees,
        IReadOnlyCollection<TenderPayment> payments)
    {
        var scopedFees = fees
            .Where(fee => !fee.IsDeleted &&
                          fee.TenantId == tender.TenantId &&
                          fee.TenderId == tender.Id)
            .ToList();
        var scopedPayments = payments
            .Where(payment => !payment.IsDeleted &&
                              payment.TenantId == tender.TenantId &&
                              payment.BusinessPartnerId == businessPartnerId)
            .ToList();
        var feeStatuses = scopedFees
            .Select(fee =>
            {
                var feePayments = scopedPayments
                    .Where(payment => payment.TenderFeeId == fee.Id)
                    .OrderByDescending(payment => payment.PaymentDate)
                    .ToList();
                var satisfied = feePayments.FirstOrDefault(IsPaymentSatisfied);
                var latest = satisfied ?? feePayments.FirstOrDefault();
                var status = satisfied is not null
                    ? "Verified"
                    : latest is null
                        ? "NotPaid"
                        : string.Equals(latest.Status, "Pending", StringComparison.OrdinalIgnoreCase)
                            ? "Pending"
                            : string.Equals(latest.Status, "Rejected", StringComparison.OrdinalIgnoreCase)
                                ? "Rejected"
                                : "NotPaid";
                return new TenderFeePaymentStatusDto
                {
                    TenderFeeId = fee.Id,
                    FeeType = fee.FeeType,
                    Amount = fee.Amount,
                    Currency = fee.Currency,
                    IsMandatory = fee.IsMandatory,
                    Status = status,
                    PaymentId = latest?.Id
                };
            })
            .ToList();
        var paymentAdmission = TenderBidPaymentRules.Assess(
            bid ?? new TenderBid
            {
                TenantId = tender.TenantId,
                TenderId = tender.Id,
                BusinessPartnerId = businessPartnerId
            },
            scopedFees,
            scopedPayments);
        var declarationSatisfied = !tender.RequiresAcceptanceDeclaration || bid?.AcceptedDeclaration == true;
        var validAssignment = assignment is not null &&
                              !assignment.IsDeleted &&
                              assignment.TenantId == tender.TenantId &&
                              assignment.TenderId == tender.Id &&
                              assignment.BusinessPartnerId == businessPartnerId;

        return new TenderBidInitiationStatusDto
        {
            TenderId = tender.Id,
            BusinessPartnerId = businessPartnerId,
            DraftBidId = bid is { Status: "Draft" } ? bid.Id : null,
            HasAssignment = validAssignment,
            AssignmentType = validAssignment ? assignment!.AssignmentType : null,
            RequiresAcceptanceDeclaration = tender.RequiresAcceptanceDeclaration,
            DeclarationAccepted = bid?.AcceptedDeclaration == true,
            DeclarationSatisfied = declarationSatisfied,
            PaymentRequired = paymentAdmission.PaymentRequired,
            HasPayment = paymentAdmission.HasPayment,
            PaymentSatisfied = paymentAdmission.PaymentSatisfied,
            PaymentEvidenceAccepted = paymentAdmission.CanSubmitSealed,
            PaymentPendingVerification = paymentAdmission.PendingVerification,
            CanProceed = validAssignment && declarationSatisfied && paymentAdmission.CanSubmitSealed,
            Fees = feeStatuses
        };
    }

    internal static TenderBidPaymentAdmissionDecision ValidateInitiationRequirements(
        Tender tender,
        TenderBid bid,
        IReadOnlyCollection<TenderAssignment> assignments,
        IReadOnlyCollection<TenderFee> fees,
        IReadOnlyCollection<TenderPayment> payments)
    {
        if (!assignments.Any())
            throw new TenderBidInitiationValidationException(
                "TENDER_BID_ASSIGNMENT_REQUIRED",
                "Create the supplier tender assignment before submitting the bid.");
        if (tender.RequiresAcceptanceDeclaration && !bid.AcceptedDeclaration)
            throw new TenderBidInitiationValidationException(
                "TENDER_BID_DECLARATION_REQUIRED",
                "Accept the required supplier declaration before submitting the bid.");

        var paymentAdmission = TenderBidPaymentRules.Assess(bid, fees, payments);
        if (!paymentAdmission.CanSubmitSealed)
            throw new TenderBidInitiationValidationException(paymentAdmission.Code, paymentAdmission.Message);
        return paymentAdmission;
    }

    private static bool IsPaymentSatisfied(TenderPayment payment) =>
        TenderBidPaymentRules.IsSatisfied(payment);

    internal static void EnsureLegacyOpeningReady(Tender tender, DateTime nowUtc)
    {
        if (!string.Equals(tender.Status, "Closed", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Bids can be opened only after the tender is closed (current status: '{tender.Status}').");
        if (!tender.SubmissionDeadline.HasValue || nowUtc < tender.SubmissionDeadline.Value)
            throw new InvalidOperationException(
                "Bids cannot be opened before the tender submission deadline.");
        if (tender.OpeningDate.HasValue && nowUtc < tender.OpeningDate.Value)
            throw new InvalidOperationException(
                "Bids cannot be opened before the scheduled opening time.");
    }

    private async Task<BusinessPartner?> GetCurrentBusinessPartnerAsync()
    {
        var businessPartner = await _businessPartnerRepository.GetByUserIdAsync(_currentUserProvider.UserId);
        if (businessPartner is not null) return businessPartner;
        var link = await _businessPartnerUserRepository.GetByUserIdAsync(_currentUserProvider.UserId);
        return link is { IsActive: true } ? link.BusinessPartner : null;
    }

    private static TenderEvaluationDto MapEvaluationToDto(TenderEvaluation evaluation)
    {
        return new TenderEvaluationDto
        {
            Id = evaluation.Id,
            TenderBidId = evaluation.TenderBidId,
            TenderEvaluatorId = evaluation.TenderEvaluatorId,
            EvaluatorName = evaluation.TenderEvaluator != null ? $"{evaluation.TenderEvaluator.User?.FirstName} {evaluation.TenderEvaluator.User?.LastName}".Trim() : string.Empty,
            EvaluatorRole = string.Empty,
            EvaluationDate = evaluation.CreatedAt,
            Status = evaluation.Status,
            PriceScore = evaluation.PriceScore,
            QualityScore = evaluation.QualityScore,
            DeliveryScore = evaluation.DeliveryScore,
            ExperienceScore = evaluation.ExperienceScore,
            TechnicalScore = evaluation.TechnicalScore,
            ComplianceScore = evaluation.ComplianceScore,
            TotalScore = evaluation.TotalScore,
            EvaluationCriteriaJson = evaluation.EvaluationCriteriaJson,
            TechnicalComments = evaluation.TechnicalComments,
            CommercialComments = evaluation.CommercialComments,
            OverallComments = evaluation.OverallComments,
            IsRecommended = evaluation.IsRecommended,
            Recommendation = evaluation.Recommendation
        };
    }

    private static TenderInterviewDto MapInterviewToDto(TenderInterview interview)
    {
        return new TenderInterviewDto
        {
            Id = interview.Id,
            TenderId = interview.TenderId,
            TenderBidId = interview.TenderBidId,
            BusinessPartnerName = string.Empty, // Would need to fetch from BusinessPartner entity
            Title = interview.Title,
            ScheduledDate = interview.ScheduledDate,
            StartTime = interview.StartTime,
            EndTime = interview.EndTime,
            Location = interview.Location,
            InterviewType = interview.InterviewType,
            Agenda = interview.Agenda,
            Status = interview.Status,
            InterviewNotes = interview.InterviewNotes,
            InterviewScore = interview.InterviewScore,
            CompletedDate = interview.CompletedDate,
            ConductedByName = string.Empty // Would need to fetch from User entity
        };
    }

    #region Bid LOT Methods

    public async Task<TenderBidLotDto> AddBidLotAsync(Guid bidId, CreateTenderBidLotDto dto)
    {
        try
        {
            var bid = await _bidRepository.GetByIdAsync(bidId);
            if (bid == null)
                throw new InvalidOperationException($"Bid with ID {bidId} not found");

            var bidLot = new TenderBidLot
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderBidId = bidId,
                LotId = dto.LotId,
                TotalLotAmount = 0, // Will be calculated from items
                Currency = bid.Currency,
                DeliveryDays = dto.DeliveryDays,
                PaymentTerms = dto.PaymentTerms,
                WarrantyTerms = dto.WarrantyTerms,
                TechnicalProposal = dto.TechnicalProposal,
                CommercialProposal = dto.CommercialProposal,
                Notes = dto.Notes,
                Status = "Draft"
            };

            await _bidLotRepository.CreateAsync(bidLot);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created bid lot for bid {BidId}, lot {LotId}", bidId, dto.LotId);

            return MapToBidLotDto(bidLot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bid lot for bid {BidId}", bidId);
            throw;
        }
    }

    public async Task<TenderBidLotDto> UpdateBidLotAsync(Guid bidLotId, UpdateTenderBidLotDto dto)
    {
        try
        {
            var bidLot = await _bidLotRepository.GetByIdAsync(bidLotId);
            if (bidLot == null)
                throw new InvalidOperationException($"Bid lot with ID {bidLotId} not found");

            bidLot.DeliveryDays = dto.DeliveryDays ?? bidLot.DeliveryDays;
            bidLot.PaymentTerms = dto.PaymentTerms ?? bidLot.PaymentTerms;
            bidLot.WarrantyTerms = dto.WarrantyTerms ?? bidLot.WarrantyTerms;
            bidLot.TechnicalProposal = dto.TechnicalProposal ?? bidLot.TechnicalProposal;
            bidLot.CommercialProposal = dto.CommercialProposal ?? bidLot.CommercialProposal;
            bidLot.Notes = dto.Notes ?? bidLot.Notes;

            await _bidLotRepository.UpdateAsync(bidLot);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated bid lot {BidLotId}", bidLotId);

            return MapToBidLotDto(bidLot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating bid lot {BidLotId}", bidLotId);
            throw;
        }
    }

    public async Task DeleteBidLotAsync(Guid bidLotId)
    {
        try
        {
            var bidLot = await _bidLotRepository.GetByIdWithItemsAsync(bidLotId);
            if (bidLot == null)
                throw new InvalidOperationException($"Bid lot with ID {bidLotId} not found");

            // Remove lot assignment from bid items
            if (bidLot.Items != null)
            {
                foreach (var item in bidLot.Items)
                {
                    item.BidLotId = null;
                    await _bidItemRepository.UpdateAsync(item);
                }
            }

            await _bidLotRepository.DeleteAsync(bidLotId);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted bid lot {BidLotId}", bidLotId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting bid lot {BidLotId}", bidLotId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderBidLotDto>> GetBidLotsAsync(Guid bidId)
    {
        try
        {
            var bidLots = await _bidLotRepository.GetByBidIdAsync(bidId);
            return bidLots.Select(MapToBidLotDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bid lots for bid {BidId}", bidId);
            throw;
        }
    }

    public async Task<TenderBidLotDto?> GetBidLotByIdAsync(Guid bidLotId)
    {
        try
        {
            var bidLot = await _bidLotRepository.GetByIdWithItemsAsync(bidLotId);
            return bidLot != null ? MapToBidLotDto(bidLot) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bid lot {BidLotId}", bidLotId);
            throw;
        }
    }

    public async Task AssignBidItemToLotAsync(Guid bidItemId, Guid bidLotId)
    {
        try
        {
            var bidItem = await _bidItemRepository.GetByIdAsync(bidItemId);
            if (bidItem == null)
                throw new InvalidOperationException($"Bid item with ID {bidItemId} not found");

            var bidLot = await _bidLotRepository.GetByIdAsync(bidLotId);
            if (bidLot == null)
                throw new InvalidOperationException($"Bid lot with ID {bidLotId} not found");

            if (bidItem.TenderBidId != bidLot.TenderBidId)
                throw new InvalidOperationException("Bid item and bid lot must belong to the same bid");

            bidItem.BidLotId = bidLotId;
            await _bidItemRepository.UpdateAsync(bidItem);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Assigned bid item {BidItemId} to bid lot {BidLotId}", bidItemId, bidLotId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning bid item {BidItemId} to bid lot {BidLotId}", bidItemId, bidLotId);
            throw;
        }
    }

    public async Task RemoveBidItemFromLotAsync(Guid bidItemId)
    {
        try
        {
            var bidItem = await _bidItemRepository.GetByIdAsync(bidItemId);
            if (bidItem == null)
                throw new InvalidOperationException($"Bid item with ID {bidItemId} not found");

            bidItem.BidLotId = null;
            await _bidItemRepository.UpdateAsync(bidItem);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Removed bid item {BidItemId} from lot", bidItemId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing bid item {BidItemId} from lot", bidItemId);
            throw;
        }
    }

    private static TenderBidLotDto MapToBidLotDto(TenderBidLot bidLot)
    {
        return new TenderBidLotDto
        {
            Id = bidLot.Id,
            TenderBidId = bidLot.TenderBidId,
            LotId = bidLot.LotId,
            LotCode = bidLot.Lot?.LotCode ?? string.Empty,
            LotTitle = bidLot.Lot?.Title ?? string.Empty,
            TotalLotAmount = bidLot.TotalLotAmount,
            Currency = bidLot.Currency,
            DeliveryDays = bidLot.DeliveryDays,
            PaymentTerms = bidLot.PaymentTerms,
            WarrantyTerms = bidLot.WarrantyTerms,
            TechnicalProposal = bidLot.TechnicalProposal,
            CommercialProposal = bidLot.CommercialProposal,
            Status = bidLot.Status,
            PriceScore = bidLot.PriceScore,
            QualityScore = bidLot.QualityScore,
            DeliveryScore = bidLot.DeliveryScore,
            TotalScore = bidLot.TotalScore,
            Rank = bidLot.Rank,
            EvaluationNotes = bidLot.EvaluationNotes,
            Notes = bidLot.Notes,
            ItemCount = bidLot.Items?.Count(item => !item.IsDeleted) ?? 0,
            Items = bidLot.Items?.Where(item => !item.IsDeleted).Select(i => new TenderBidItemDto
            {
                Id = i.Id,
                TenderBidId = i.TenderBidId,
                BidLotId = i.BidLotId,
                TenderItemId = i.TenderItemId,
                TenderItemDescription = i.TenderItem?.Description ?? string.Empty,
                RequestedQuantity = i.TenderItem?.Quantity ?? 0,
                OfferedQuantity = i.OfferedQuantity,
                UnitOfMeasure = i.TenderItem?.UnitOfMeasure,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice,
                DeliveryDays = i.DeliveryDays,
                Specifications = i.Specifications,
                Brand = i.Brand,
                Model = i.Model,
                TechnicalDetails = i.TechnicalDetails
            }).ToList() ?? new List<TenderBidItemDto>()
        };
    }

    #endregion
}
