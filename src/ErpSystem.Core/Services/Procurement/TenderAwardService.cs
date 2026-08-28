using System.Data;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class TenderAwardService : ITenderAwardService
{
    private readonly ITenderAwardRepository _awardRepository;
    private readonly ITenderRepository _tenderRepository;
    private readonly ITenderBidRepository _bidRepository;
    private readonly ITenderBidItemRepository _bidItemRepository;
    private readonly ITenderEvaluationRepository _evaluationRepository;
    private readonly ITenderEvaluatorRepository _evaluatorRepository;
    private readonly ITenderNotificationService _notificationService;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IPurchaseOrderItemRepository _purchaseOrderItemRepository;
    private readonly ITenderNegotiationRepository _negotiationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<TenderAwardService> _logger;
    private readonly IProcurementTenderControlService _tenderControlService;
    private readonly IProcurementExceptionalSourcingControlService _exceptionalSourcingControlService;
    private readonly IProcurementAwardReadinessService _awardReadiness;
    private readonly IProcurementPurchaseOrderSourceService _purchaseOrderSources;
    private readonly IProcurementPurchaseOrderSodService _purchaseOrderSod;

    public TenderAwardService(
        ITenderAwardRepository awardRepository,
        ITenderRepository tenderRepository,
        ITenderBidRepository bidRepository,
        ITenderBidItemRepository bidItemRepository,
        ITenderEvaluationRepository evaluationRepository,
        ITenderEvaluatorRepository evaluatorRepository,
        ITenderNotificationService notificationService,
        IPurchaseOrderRepository purchaseOrderRepository,
        IPurchaseOrderItemRepository purchaseOrderItemRepository,
        ITenderNegotiationRepository negotiationRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        IProcurementTenderControlService tenderControlService,
        IProcurementExceptionalSourcingControlService exceptionalSourcingControlService,
        IProcurementAwardReadinessService awardReadiness,
        IProcurementPurchaseOrderSourceService purchaseOrderSources,
        IProcurementPurchaseOrderSodService purchaseOrderSod,
        ILogger<TenderAwardService> logger)
    {
        _awardRepository = awardRepository;
        _tenderRepository = tenderRepository;
        _bidRepository = bidRepository;
        _bidItemRepository = bidItemRepository;
        _evaluationRepository = evaluationRepository;
        _evaluatorRepository = evaluatorRepository;
        _notificationService = notificationService;
        _purchaseOrderRepository = purchaseOrderRepository;
        _purchaseOrderItemRepository = purchaseOrderItemRepository;
        _negotiationRepository = negotiationRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _tenderControlService = tenderControlService;
        _exceptionalSourcingControlService = exceptionalSourcingControlService;
        _awardReadiness = awardReadiness;
        _purchaseOrderSources = purchaseOrderSources;
        _purchaseOrderSod = purchaseOrderSod;
        _logger = logger;
    }

    public async Task<TenderAwardDto?> GetAwardByIdAsync(Guid id)
    {
        try
        {
            var award = await _awardRepository.GetByIdAsync(id);
            if (award == null) return null;

            var bid = await _bidRepository.GetByIdAsync(award.TenderBidId);
            var tender = bid != null ? await _tenderRepository.GetByIdAsync(bid.TenderId) : null;

            return MapToDto(award, bid, tender);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving award {AwardId}", id);
            throw;
        }
    }

    public async Task<TenderAwardDto?> GetAwardByTenderIdAsync(Guid tenderId)
    {
        try
        {
            var award = await _awardRepository.GetByTenderIdAsync(tenderId);
            if (award == null) return null;

            var bid = await _bidRepository.GetByIdAsync(award.TenderBidId);
            var tender = await _tenderRepository.GetByIdAsync(tenderId);

            return MapToDto(award, bid, tender);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving award for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<TenderAwardDto?> GetAwardByBidIdAsync(Guid bidId)
    {
        try
        {
            var award = await _awardRepository.GetByBidIdAsync(bidId);
            if (award == null) return null;

            var bid = await _bidRepository.GetByIdAsync(bidId);
            var tender = bid != null ? await _tenderRepository.GetByIdAsync(bid.TenderId) : null;

            return MapToDto(award, bid, tender);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving award for bid {BidId}", bidId);
            throw;
        }
    }

    public async Task<PagedResult<TenderAwardDto>> GetAwardsAsync(int page, int pageSize, string? search = null, string? status = null)
    {
        try
        {
            var pagedAwards = await _awardRepository.GetAwardsPagedAsync(page, pageSize, search, status);
            var items = new List<TenderAwardDto>();

            foreach (var award in pagedAwards.Items)
            {
                var bid = await _bidRepository.GetByIdAsync(award.TenderBidId);
                var tender = bid != null ? await _tenderRepository.GetByIdAsync(bid.TenderId) : null;
                items.Add(MapToDto(award, bid, tender));
            }

            return new PagedResult<TenderAwardDto>
            {
                Items = items,
                TotalCount = pagedAwards.TotalCount,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving awards");
            throw;
        }
    }

    public async Task<AwardRecommendationDto> GenerateAwardRecommendationAsync(Guid tenderId)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");

            var bids = await _bidRepository.GetByTenderIdAsync(tenderId);

            // Get all assigned evaluators for this tender
            var evaluators = await _evaluatorRepository.GetByTenderIdAsync(tenderId);
            var totalAssignedEvaluators = evaluators.Count();

            var bidRecommendations = new List<BidRecommendationDto>();
            var evaluatedBidsCount = 0;

            // Consider all bids that are Opened, UnderEvaluation, or Evaluated
            var eligibleBids = bids.Where(b => b.Status == "Opened" || b.Status == "UnderEvaluation" || b.Status == "Evaluated").ToList();

            foreach (var bid in eligibleBids)
            {
                var evaluations = await _evaluationRepository.GetByBidIdAsync(bid.Id);
                var submittedEvaluations = evaluations.Where(e => e.Status == "Submitted").ToList();

                // Only include bids that have at least one submitted evaluation
                if (!submittedEvaluations.Any())
                    continue;

                evaluatedBidsCount++;
                var avgScore = submittedEvaluations.Average(e => e.TotalScore) ?? 0;

                // Count how many evaluators recommended this bid
                var recommendationCount = submittedEvaluations.Count(e => e.IsRecommended);
                var evaluatorCount = submittedEvaluations.Count;

                bidRecommendations.Add(new BidRecommendationDto
                {
                    BidId = bid.Id,
                    BidNumber = bid.BidNumber,
                    BusinessPartnerId = bid.BusinessPartnerId,
                    BusinessPartnerName = bid.BusinessPartner?.PartnerName ?? string.Empty,
                    TotalBidAmount = bid.TotalBidAmount,
                    AverageScore = avgScore,
                    EvaluationCount = evaluatorCount,
                    RecommendationCount = recommendationCount,
                    TotalEvaluators = totalAssignedEvaluators,
                    Recommendation = avgScore >= 70 ? "Recommended" : "Not Recommended"
                });
            }

            // Sort by average score descending
            bidRecommendations = bidRecommendations.OrderByDescending(b => b.AverageScore).ToList();
            var topBid = bidRecommendations.FirstOrDefault();

            return new AwardRecommendationDto
            {
                TenderId = tenderId,
                TenderNumber = tender.TenderNumber,
                TenderTitle = tender.Title,
                TotalBids = bids.Count(),
                EvaluatedBids = evaluatedBidsCount,
                RecommendedBidId = topBid?.BidId,
                RecommendedBidNumber = topBid?.BidNumber,
                RecommendedBusinessPartner = topBid?.BusinessPartnerName,
                RecommendedAmount = topBid?.TotalBidAmount ?? 0,
                RecommendedScore = topBid?.AverageScore ?? 0,
                BidRecommendations = bidRecommendations,
                GeneratedAt = DateTime.UtcNow,
                GeneratedById = _currentUserProvider.UserId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating award recommendation for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<TenderAwardDto> CreateAwardAsync(
        Guid tenderId,
        CreateAwardDto dto,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var correlation = string.IsNullOrWhiteSpace(correlationId)
                ? Guid.NewGuid().ToString("N")
                : correlationId.Trim();
            await EnsureLegacyAwardAllowedAsync(tenderId);
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");

            var bid = await _bidRepository.GetByIdAsync(dto.TenderBidId)
                ?? throw new InvalidOperationException($"Bid with ID {dto.TenderBidId} not found");
            if (bid.TenderId != tenderId)
                throw new InvalidOperationException(
                    "The selected bid does not belong to the tender being awarded.");

            // Check if award already exists for this tender
            var existingAward = await _awardRepository.GetByTenderIdAsync(tenderId);
            if (existingAward != null)
            {
                throw new InvalidOperationException($"Award already exists for tender {tender.TenderNumber}");
            }
            await _awardReadiness.EnsureAwardReadyAsync(
                ProcurementAwardReadinessSourceType.Tender,
                tenderId,
                ProcurementAwardReadinessGateRequestFactory.Create(
                    ProcurementAwardReadinessSourceType.Tender,
                    tenderId,
                    correlation,
                    [bid.Id],
                    [bid.BusinessPartnerId]),
                correlation,
                cancellationToken);

            // Award is created with the bid amount as both original and awarded amount
            // Negotiation happens AFTER award creation and will update these values
            var awardAmount = dto.AwardedAmount;

            _logger.LogInformation(
                "Creating award for tender {TenderId}. Amount: {Amount}. Negotiation will update this if completed later.",
                tenderId, awardAmount);

            var award = new TenderAward
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderId = tenderId,
                TenderBidId = dto.TenderBidId,
                BusinessPartnerId = bid.BusinessPartnerId,
                AwardDate = dto.AwardDate ?? DateTime.UtcNow,
                OriginalBidAmount = awardAmount, // Will be preserved when negotiation updates AwardedAmount
                AwardedAmount = awardAmount,
                NegotiationId = null, // Will be set when negotiation is completed
                IsNegotiated = false, // Will be set to true when negotiation is completed
                Currency = NormalizeCurrency(dto.Currency, tender.Currency),
                AwardedById = _currentUserProvider.UserId,
                AwardJustification = dto.AwardJustification,
                Status = "Awarded",
                Notes = dto.Notes,
                CreatedAt = DateTime.UtcNow
            };

            await _awardRepository.CreateAsync(award);

            // Update tender status
            tender.Status = "Awarded";
            tender.AwardDate = award.AwardDate;
            tender.AwardedById = _currentUserProvider.UserId;
            await _tenderRepository.UpdateAsync(tender);

            // Update bid status
            bid.Status = "Awarded";
            await _bidRepository.UpdateAsync(bid);

            // Get all other bids that should be rejected
            var allBids = await _bidRepository.GetByTenderIdAsync(tenderId);
            var rejectedBids = allBids.Where(b => b.Id != dto.TenderBidId && (b.Status == "Evaluated" || b.Status == "Submitted" || b.Status == "Opened")).ToList();

            // Update rejected bids' status to "Rejected"
            foreach (var rejectedBid in rejectedBids)
            {
                rejectedBid.Status = "Rejected";
                await _bidRepository.UpdateAsync(rejectedBid);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created award {AwardId} for tender {TenderId}. Rejected {RejectedCount} other bids.",
                award.Id, tenderId, rejectedBids.Count);

            // NOTE: Award notification is NOT sent automatically here.
            // The workflow is: Award -> Negotiation -> Complete Negotiation -> Send Notification
            // Notifications should be sent manually via SendAwardNotificationsAsync after negotiation is complete.
            // This allows for price negotiation before the final award letter is sent to the supplier.

            return MapToDto(award, bid, tender);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating award for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<TenderAwardDto> UpdateAwardAsync(Guid id, CreateAwardDto dto)
    {
        try
        {
            var award = await _awardRepository.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Award with ID {id} not found");
            await EnsureLegacyAwardAllowedAsync(award.TenderId);

            var bid = await _bidRepository.GetByIdAsync(dto.TenderBidId)
                ?? throw new InvalidOperationException($"Bid with ID {dto.TenderBidId} not found");

            var tender = await _tenderRepository.GetByIdAsync(award.TenderId)
                ?? throw new InvalidOperationException($"Tender with ID {award.TenderId} not found");
            award.AwardedAmount = dto.AwardedAmount;
            award.Currency = NormalizeCurrency(dto.Currency, tender.Currency);
            award.AwardDate = dto.AwardDate ?? award.AwardDate;
            award.AwardJustification = dto.AwardJustification;
            award.Notes = dto.Notes;
            award.UpdatedAt = DateTime.UtcNow;

            await _awardRepository.UpdateAsync(award);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated award {AwardId}", id);

            return MapToDto(award, bid, tender);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating award {AwardId}", id);
            throw;
        }
    }

    public async Task CancelAwardAsync(Guid id, CancelAwardDto dto)
    {
        try
        {
            var award = await _awardRepository.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Award with ID {id} not found");
            await EnsureLegacyAwardAllowedAsync(award.TenderId);

            var bid = await _bidRepository.GetByIdAsync(award.TenderBidId);
            var tender = await _tenderRepository.GetByIdAsync(award.TenderId);

            award.Status = "Cancelled";
            award.Notes = $"Cancelled: {dto.Reason}. {award.Notes}";
            award.UpdatedAt = DateTime.UtcNow;

            await _awardRepository.UpdateAsync(award);

            // Update tender status back to Evaluated
            if (tender != null)
            {
                tender.Status = "Evaluated";
                tender.AwardDate = null;
                tender.AwardedById = null;
                await _tenderRepository.UpdateAsync(tender);
            }

            // Update bid status back to Evaluated
            if (bid != null)
            {
                bid.Status = "Evaluated";
                await _bidRepository.UpdateAsync(bid);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Cancelled award {AwardId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling award {AwardId}", id);
            throw;
        }
    }

    public async Task SendAwardNotificationsAsync(Guid tenderId, AwardNotificationDto dto)
    {
        try
        {
            await _notificationService.SendAwardNotificationAsync(dto.AwardId);

            _logger.LogInformation("Sent award notifications for tender {TenderId}", tenderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending award notifications for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<PurchaseOrderFromAwardResponseDto> CreatePurchaseOrderFromAwardAsync(CreatePurchaseOrderFromAwardDto dto)
    {
        var ownsSourceClaimTransaction = false;
        try
        {
            // Get the tender award
            var award = await _awardRepository.GetByIdAsync(dto.TenderAwardId)
                ?? throw new InvalidOperationException($"Tender award with ID {dto.TenderAwardId} not found");
            await EnsureLegacyAwardAllowedAsync(award.TenderId);

            // Check if PO already exists for this award
            if (award.PurchaseOrderId.HasValue)
            {
                throw new InvalidOperationException($"Purchase order already exists for this award (PO ID: {award.PurchaseOrderId})");
            }

            // Get the bid and tender
            var bid = await _bidRepository.GetByIdAsync(award.TenderBidId)
                ?? throw new InvalidOperationException($"Bid with ID {award.TenderBidId} not found");

            var tender = await _tenderRepository.GetByIdAsync(award.TenderId)
                ?? throw new InvalidOperationException($"Tender with ID {award.TenderId} not found");
            if (dto.ContractId.HasValue)
            {
                var contract = await _unitOfWork.Repository<Contract>()
                    .GetByIdAsync(dto.ContractId.Value)
                    ?? throw new InvalidOperationException(
                        $"Contract with ID {dto.ContractId.Value} not found");
                if (contract.TenantId != _currentUserProvider.TenantId ||
                    contract.IsDeleted ||
                    !ProcurementPurchaseOrderSourceRules.IsContractBoundToAward(
                        contract.TenderAwardId,
                        award.Id))
                {
                    throw new InvalidOperationException(
                        "The selected contract is not derived from the requested tender award.");
                }
            }
            var sourceCorrelationId = Guid.NewGuid().ToString("N");
            var sourceType = dto.ContractId.HasValue
                ? ProcurementPurchaseOrderSourceType.Contract
                : ProcurementPurchaseOrderSourceType.TenderAward;
            var sourceId = dto.ContractId ?? award.Id;
            var approvedSource = await _purchaseOrderSources.ResolveAsync(
                sourceType,
                sourceId,
                award.BusinessPartnerId,
                sourceCorrelationId);

            // Get bid items
            var bidItems = (await _bidItemRepository.GetByBidIdAsync(bid.Id))
                .Where(item =>
                    !award.BidLotId.HasValue ||
                    item.BidLotId == award.BidLotId.Value)
                .ToList();
            if (bidItems.Count == 0)
            {
                throw new InvalidOperationException("Cannot create PO: No items found in the bid");
            }

            // Check if there's a negotiation and get negotiated prices
            TenderNegotiation? negotiation = null;
            Dictionary<Guid, TenderNegotiationItem> negotiatedItemsMap = new();
            
            if (award.NegotiationId.HasValue && award.IsNegotiated)
            {
                negotiation = await _negotiationRepository.GetByIdWithItemsAsync(award.NegotiationId.Value);
                if (negotiation != null && negotiation.Items.Any())
                {
                    // Create a map of bid item ID to negotiated item for quick lookup
                    negotiatedItemsMap = negotiation.Items.ToDictionary(ni => ni.TenderBidItemId, ni => ni);
                    
                    _logger.LogInformation(
                        "Using negotiated prices from negotiation {NegotiationId} for PO creation. Found {Count} negotiated items.",
                        negotiation.Id, negotiatedItemsMap.Count);
                }
            }

            var sourceOrderLines = bidItems.Select(item =>
                {
                    negotiatedItemsMap.TryGetValue(item.Id, out var negotiatedItem);
                    return new ProcurementPurchaseOrderSourceOrderLine
                    {
                        InventoryItemId = null,
                        ItemDescription =
                            item.TenderItem?.Description ?? "Item from tender",
                        OrderedQuantity =
                            negotiatedItem?.Quantity > 0
                                ? negotiatedItem.Quantity
                                : item.OfferedQuantity,
                        UnitOfMeasure =
                            item.TenderItem?.UnitOfMeasure ?? "EA",
                        UnitPrice =
                            negotiatedItem?.NegotiatedUnitPrice ??
                            item.UnitPrice
                    };
                }).ToList();
            await _purchaseOrderSources.ValidateOrderAsync(
                approvedSource,
                sourceOrderLines,
                award.AwardedAmount,
                approvedSource.CurrencyCode,
                sourceCorrelationId);

            // Generate PO number
            var orderNumber = await _purchaseOrderRepository.GenerateOrderNumberAsync();

            // Create purchase order
            var purchaseOrder = new PurchaseOrder
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                OrderNumber = orderNumber,
                BusinessPartnerId = award.BusinessPartnerId,
                OrderDate = DateTime.UtcNow,
                RequiredDate = dto.RequiredDate,
                Status = "Draft",
                RequestedById =
                    approvedSource.PurchaseRequisitionRequestedById,
                
                // Financial details from award (uses negotiated amount if available)
                SubTotal = award.AwardedAmount,
                TaxAmount = 0, // Can be calculated based on items
                ShippingCost = 0,
                DiscountAmount = 0,
                TotalAmount = award.AwardedAmount,
                Currency = approvedSource.CurrencyCode,
                ExchangeRate = 1,
                
                // Terms
                PaymentTerms = dto.PaymentTerms ?? bid.PaymentTerms,
                ShippingTerms = dto.ShippingTerms,
                Notes = dto.Notes,
                
                // Delivery information
                DeliveryWarehouseId = dto.DeliveryWarehouseId,
                DeliveryAddress = dto.DeliveryAddress,
                DeliveryInstructions = dto.DeliveryInstructions,
                
                // Tender/Contract integration
                TenderAwardId = award.Id,
                TenderNumber = tender.TenderNumber,
                ContractId = dto.ContractId,
                ContractNumber = dto.ContractNumber,
                
                // PO Type
                OrderType = "Standard",
                
                CreatedAt = DateTime.UtcNow,
                CreatedById = _currentUserProvider.UserId
            };
            _purchaseOrderSources.Apply(purchaseOrder, approvedSource);
            if (dto.AutoApprove)
            {
                await _purchaseOrderSod.RejectApprovalBypassAsync(
                    purchaseOrder,
                    "AwardAutoApprove",
                    sourceCorrelationId);
            }

            ownsSourceClaimTransaction = !_unitOfWork.HasActiveTransaction;
            if (ownsSourceClaimTransaction)
            {
                await _unitOfWork.BeginTransactionAsync(
                    IsolationLevel.Serializable);
            }
            await _purchaseOrderSources.ReserveAsync(
                approvedSource,
                sourceOrderLines,
                award.AwardedAmount,
                approvedSource.CurrencyCode,
                purchaseOrder.Id,
                sourceCorrelationId);

            await _purchaseOrderRepository.CreatePurchaseOrderAsync(purchaseOrder);

            // Create PO items from bid items, using negotiated prices if available
            var itemCount = 0;
            foreach (var bidItem in bidItems)
            {
                // Check if this item has a negotiated price
                decimal unitPrice = bidItem.UnitPrice;
                decimal lineTotal = bidItem.TotalPrice;
                string? itemNotes = bidItem.Specifications;

                if (negotiatedItemsMap.TryGetValue(bidItem.Id, out var negotiatedItem))
                {
                    // Use negotiated prices
                    unitPrice = negotiatedItem.NegotiatedUnitPrice ?? bidItem.UnitPrice;
                    var orderedQuantity = negotiatedItem.Quantity > 0
                        ? negotiatedItem.Quantity
                        : bidItem.OfferedQuantity;
                    lineTotal = negotiatedItem.NegotiatedTotalPrice ??
                        decimal.Round(
                            orderedQuantity * unitPrice,
                            2,
                            MidpointRounding.AwayFromZero);
                    
                    // Add negotiation note
                    var savingsPerUnit = bidItem.UnitPrice - unitPrice;
                    var savingsTotal = bidItem.TotalPrice - lineTotal;
                    itemNotes = $"[NEGOTIATED] Original: {bidItem.UnitPrice:C} → Negotiated: {unitPrice:C} (Savings: {savingsPerUnit:C}/unit, {savingsTotal:C} total). {bidItem.Specifications}";
                    
                    _logger.LogInformation(
                        "Using negotiated price for item {ItemId}: Original {OriginalPrice} → Negotiated {NegotiatedPrice}",
                        bidItem.Id, bidItem.UnitPrice, unitPrice);
                }

                var poItem = new PurchaseOrderItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = _currentUserProvider.TenantId,
                    PurchaseOrderId = purchaseOrder.Id,
                    InventoryItemId = null, // TenderItem doesn't have InventoryItemId - needs to be mapped separately or created later
                    ItemDescription = bidItem.TenderItem?.Description ?? "Item from tender",
                    OrderedQuantity = negotiatedItem?.Quantity > 0
                        ? negotiatedItem.Quantity
                        : bidItem.OfferedQuantity,
                    ReceivedQuantity = 0,
                    RemainingQuantity = negotiatedItem?.Quantity > 0
                        ? negotiatedItem.Quantity
                        : bidItem.OfferedQuantity,
                    UnitOfMeasure = bidItem.TenderItem?.UnitOfMeasure ?? "EA",
                    UnitPrice = unitPrice,
                    LineTotal = lineTotal,
                    ExpectedDeliveryDate = dto.RequiredDate,
                    Notes = itemNotes,
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = _currentUserProvider.UserId
                };

                await _purchaseOrderItemRepository.CreateItemAsync(poItem);
                itemCount++;
            }

            await _unitOfWork.SaveChangesAsync();
            await _purchaseOrderSources.ClaimTenderAwardAsync(
                award.Id,
                purchaseOrder);
            await _purchaseOrderSources.RecordBoundAsync(
                purchaseOrder,
                "TenderAwardPurchaseOrderCreated",
                sourceCorrelationId);
            if (ownsSourceClaimTransaction)
                await _unitOfWork.CommitAsync();

            var logMessage = negotiation != null
                ? $"Created purchase order {orderNumber} from tender award {award.Id} with {itemCount} items using negotiated prices (Negotiation: {negotiation.Id})"
                : $"Created purchase order {orderNumber} from tender award {award.Id} with {itemCount} items using original bid prices";
            
            _logger.LogInformation(logMessage);

            return new PurchaseOrderFromAwardResponseDto
            {
                PurchaseOrderId = purchaseOrder.Id,
                OrderNumber = orderNumber,
                TenderAwardId = award.Id,
                TenderNumber = tender.TenderNumber,
                BusinessPartnerId = award.BusinessPartnerId,
                BusinessPartnerName = bid.BusinessPartner?.PartnerName ?? string.Empty,
                TotalAmount = purchaseOrder.TotalAmount,
                Status = purchaseOrder.Status,
                OrderDate = purchaseOrder.OrderDate,
                ItemCount = itemCount,
                ContractNumber = dto.ContractNumber
            };
        }
        catch (Exception ex)
        {
            if (ownsSourceClaimTransaction && _unitOfWork.HasActiveTransaction)
            {
                try
                {
                    await _unitOfWork.RollbackAsync();
                }
                catch (Exception rollbackException)
                {
                    _logger.LogError(
                        rollbackException,
                        "Failed to roll back tender-award purchase-order creation");
                }
            }
            _logger.LogError(ex, "Error creating purchase order from tender award {AwardId}", dto.TenderAwardId);
            throw;
        }
    }

    private static TenderAwardDto MapToDto(TenderAward award, TenderBid? bid, Tender? tender)
    {
        var originalAmount = award.OriginalBidAmount;
        var finalAmount = award.AwardedAmount;
        var savings = originalAmount - finalAmount;

        return new TenderAwardDto
        {
            Id = award.Id,
            TenderId = award.TenderId,
            TenderNumber = tender?.TenderNumber ?? string.Empty,
            TenderTitle = tender?.Title ?? string.Empty,
            TenderBidId = award.TenderBidId,
            BidNumber = bid?.BidNumber ?? string.Empty,
            BusinessPartnerId = award.BusinessPartnerId,
            BusinessPartnerName = bid?.BusinessPartner?.PartnerName ?? string.Empty,
            AwardDate = award.AwardDate,
            OriginalBidAmount = originalAmount,
            AwardedAmount = finalAmount,
            NegotiationId = award.NegotiationId,
            IsNegotiated = award.IsNegotiated,
            NegotiationSavings = savings > 0 ? savings : 0,
            Currency = award.Currency,
            Status = award.Status,
            AwardJustification = award.AwardJustification,
            AwardedById = award.AwardedById,
            AwardedByName = award.AwardedBy?.FullName ?? string.Empty,
            PurchaseOrderId = award.PurchaseOrderId,
            Notes = award.Notes,
            CreatedById = award.CreatedById,
            CreatedAt = award.CreatedAt
        };
    }

    private static string NormalizeCurrency(string? preferred, string? fallback) =>
        !string.IsNullOrWhiteSpace(preferred)
            ? preferred.Trim().ToUpperInvariant()
            : !string.IsNullOrWhiteSpace(fallback)
                ? fallback.Trim().ToUpperInvariant()
                : "GHS";

    private async Task EnsureLegacyAwardAllowedAsync(Guid tenderId)
    {
        if (await _tenderControlService.IsControlledTenderMethodAsync(tenderId))
            throw new ProcurementTenderControlConflictException(
                "TENDER_STATUTORY_AWARD_REQUIRED",
                "NCT, ICT, QBS, and QCBS awards and downstream PO handoff require the controlled approval, contract, and bidder-acceptance lifecycle.");
        if (await _exceptionalSourcingControlService.IsExceptionalAsync(tenderId))
            throw new ProcurementExceptionalSourcingConflictException(
                "EXCEPTIONAL_AWARD_CONTROL_REQUIRED",
                "Restricted, single-source, and petty-purchase awards must use the dedicated controlled sourcing lifecycle.");
    }
}

