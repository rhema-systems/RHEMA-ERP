using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

/// <summary>
/// Service for managing tender negotiations
/// </summary>
public class TenderNegotiationService : ITenderNegotiationService
{
    private readonly ITenderNegotiationRepository _negotiationRepository;
    private readonly ITenderBidRepository _bidRepository;
    private readonly ITenderBidItemRepository _bidItemRepository;
    private readonly ITenderAwardRepository _awardRepository;
    private readonly ITenderRepository _tenderRepository;
    private readonly IContractRepository _contractRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<TenderNegotiationService> _logger;
    private readonly IProcurementExceptionalSourcingControlService _exceptionalSourcingControlService;

    public TenderNegotiationService(
        ITenderNegotiationRepository negotiationRepository,
        ITenderBidRepository bidRepository,
        ITenderBidItemRepository bidItemRepository,
        ITenderAwardRepository awardRepository,
        ITenderRepository tenderRepository,
        IContractRepository contractRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        IProcurementExceptionalSourcingControlService exceptionalSourcingControlService,
        ILogger<TenderNegotiationService> logger)
    {
        _negotiationRepository = negotiationRepository;
        _bidRepository = bidRepository;
        _bidItemRepository = bidItemRepository;
        _awardRepository = awardRepository;
        _tenderRepository = tenderRepository;
        _contractRepository = contractRepository;
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _exceptionalSourcingControlService = exceptionalSourcingControlService;
        _logger = logger;
    }

    public async Task<TenderNegotiationDto?> GetByIdAsync(Guid id)
    {
        var negotiation = await _negotiationRepository.GetByIdWithItemsAsync(id);
        if (negotiation != null) EnsureCurrentTenant(negotiation);
        return negotiation != null ? MapToDto(negotiation) : null;
    }

    public async Task<TenderNegotiationDto?> GetByTenderAndBidAsync(Guid tenderId, Guid bidId)
    {
        var negotiation = await _negotiationRepository.GetByTenderAndBidAsync(tenderId, bidId);
        if (negotiation == null) return null;
        EnsureCurrentTenant(negotiation);
        
        // Load full details
        return await GetByIdAsync(negotiation.Id);
    }

    public async Task<IEnumerable<TenderNegotiationDto>> GetByTenderIdAsync(Guid tenderId)
    {
        var negotiations = await _negotiationRepository.GetByTenderIdAsync(tenderId);
        return negotiations.Where(IsCurrentTenant).Select(MapToDto);
    }

    public async Task<IEnumerable<TenderNegotiationDto>> GetByBidIdAsync(Guid bidId)
    {
        var negotiations = await _negotiationRepository.GetByBidIdAsync(bidId);
        return negotiations.Where(IsCurrentTenant).Select(MapToDto);
    }

    public async Task<TenderNegotiationDto> CreateNegotiationAsync(CreateNegotiationDto dto)
    {
        var tenantId = RequireTenantId();
        // Check if negotiation already exists
        var existing = await _negotiationRepository.GetByTenderBidAndLotAsync(dto.TenderId, dto.TenderBidId, dto.LotId);
        if (existing != null)
        {
            throw new InvalidOperationException("A negotiation already exists for this bid");
        }

        // Get the bid with items
        var bid = await _bidRepository.GetByIdAsync(dto.TenderBidId);
        if (bid == null)
        {
            throw new InvalidOperationException("Bid not found");
        }
        var tender = await _tenderRepository.GetByIdAsync(dto.TenderId)
            ?? throw new InvalidOperationException("Tender not found");
        if (bid.TenderId != dto.TenderId)
            throw new InvalidOperationException("The bid does not belong to the selected tender.");
        if (bid.TenantId != tenantId || tender.TenantId != tenantId)
            throw new UnauthorizedAccessException(
                "The tender and bid must belong to the current tenant.");

        var award = await _awardRepository.GetByTenderIdAsync(dto.TenderId)
            ?? throw new InvalidOperationException(
                "An award recommendation is required before commercial negotiation.");
        if (award.TenderBidId != bid.Id || award.BusinessPartnerId != bid.BusinessPartnerId ||
            award.TenantId != tenantId)
            throw new InvalidOperationException(
                "The negotiation bid does not match the current award recommendation.");
        if (award.Status is not ("PendingApproval" or "Awarded"))
            throw new InvalidOperationException(
                $"Negotiation requires a pending or approved award (current status: '{award.Status}').");
        if (bid.Status is not ("Evaluated" or "Awarded") ||
            tender.Status is not ("Evaluated" or "Awarded"))
            throw new InvalidOperationException(
                "The tender and bid are not in a negotiable evaluated/awarded state.");
        await EnsureAwardHasNoDownstreamCommitmentAsync(award);
        await _exceptionalSourcingControlService.EnsureNegotiationAllowedAsync(
            dto.TenderId, dto.TenderBidId);

        // Get bid items
        var bidItems = await _bidItemRepository.GetByBidIdAsync(dto.TenderBidId);
        if (dto.BidLotId.HasValue)
        {
            bidItems = bidItems.Where(i => i.BidLotId == dto.BidLotId.Value);
        }

        var userId = Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : (Guid?)null;
        // Calculate original amount
        var originalAmount = bidItems.Sum(i => i.TotalPrice);

        var negotiation = new TenderNegotiation
        {
            TenantId = tenantId,
            TenderId = dto.TenderId,
            TenderBidId = dto.TenderBidId,
            BusinessPartnerId = bid.BusinessPartnerId,
            LotId = dto.LotId,
            BidLotId = dto.BidLotId,
            Status = "Invited",
            InvitedDate = DateTime.UtcNow,
            InvitedById = userId,
            OriginalAmount = originalAmount,
            Currency = bid.Currency ?? "GHS",
            Notes = dto.Notes
        };

        // Create negotiation items from bid items
        foreach (var bidItem in bidItems)
        {
            var negotiationItem = new TenderNegotiationItem
            {
                TenantId = tenantId,
                TenderBidItemId = bidItem.Id,
                ItemDescription = bidItem.TenderItem?.Description ?? "Item",
                Quantity = bidItem.OfferedQuantity,
                UnitOfMeasure = bidItem.TenderItem?.UnitOfMeasure,
                OriginalUnitPrice = bidItem.UnitPrice,
                OriginalTotalPrice = bidItem.TotalPrice
            };
            negotiation.Items.Add(negotiationItem);
        }

        await _negotiationRepository.CreateAsync(negotiation);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created negotiation {NegotiationId} for bid {BidId}", negotiation.Id, dto.TenderBidId);

        return await GetByIdAsync(negotiation.Id) ?? throw new InvalidOperationException("Failed to create negotiation");
    }

    public async Task<TenderNegotiationDto> UpdateNegotiationItemAsync(Guid negotiationId, UpdateNegotiationItemDto dto)
    {
        var negotiation = await _negotiationRepository.GetByIdWithItemsAsync(negotiationId);
        if (negotiation == null)
        {
            throw new InvalidOperationException("Negotiation not found");
        }
        EnsureCurrentTenant(negotiation);

        if (negotiation.Status == "Completed" || negotiation.Status == "Cancelled")
        {
            throw new InvalidOperationException("Cannot update a completed or cancelled negotiation");
        }

        var item = negotiation.Items.FirstOrDefault(i => i.Id == dto.ItemId);
        if (item == null)
        {
            throw new InvalidOperationException("Negotiation item not found");
        }

        item.NegotiatedUnitPrice = dto.NegotiatedUnitPrice;
        item.NegotiatedTotalPrice = dto.NegotiatedUnitPrice.HasValue
            ? dto.NegotiatedUnitPrice.Value * item.Quantity
            : null;
        item.Notes = dto.Notes;

        await _negotiationRepository.UpdateItemAsync(item);

        // Update negotiation status to InProgress if still Invited
        if (negotiation.Status == "Invited")
        {
            negotiation.Status = "InProgress";
            await _negotiationRepository.UpdateAsync(negotiation);
        }

        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(negotiationId) ?? throw new InvalidOperationException("Failed to update negotiation");
    }

    public async Task<TenderNegotiationDto> SaveDraftAsync(Guid negotiationId, CompleteNegotiationDto dto)
    {
        var negotiation = await _negotiationRepository.GetByIdWithItemsAsync(negotiationId);
        if (negotiation == null)
        {
            throw new InvalidOperationException("Negotiation not found");
        }
        EnsureCurrentTenant(negotiation);

        if (negotiation.Status == "Completed" || negotiation.Status == "Cancelled")
        {
            throw new InvalidOperationException("Cannot update a completed or cancelled negotiation");
        }

        // Update all items with negotiated prices
        foreach (var itemDto in dto.Items)
        {
            var item = negotiation.Items.FirstOrDefault(i => i.Id == itemDto.ItemId);
            if (item != null)
            {
                item.NegotiatedUnitPrice = itemDto.NegotiatedUnitPrice;
                item.NegotiatedTotalPrice = itemDto.NegotiatedUnitPrice.HasValue
                    ? itemDto.NegotiatedUnitPrice.Value * item.Quantity
                    : null;
                item.Notes = itemDto.Notes;
                await _negotiationRepository.UpdateItemAsync(item);
            }
        }

        // Update negotiation status to InProgress if still Invited
        if (negotiation.Status == "Invited")
        {
            negotiation.Status = "InProgress";
        }

        // Update notes if provided
        if (!string.IsNullOrEmpty(dto.Notes))
        {
            negotiation.Notes = dto.Notes;
        }

        await _negotiationRepository.UpdateAsync(negotiation);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Saved draft for negotiation {NegotiationId}", negotiationId);

        return await GetByIdAsync(negotiationId) ?? throw new InvalidOperationException("Failed to save draft");
    }

    public async Task<TenderNegotiationDto> CompleteNegotiationAsync(Guid negotiationId, CompleteNegotiationDto dto)
    {
        var negotiation = await _negotiationRepository.GetByIdWithItemsAsync(negotiationId);
        if (negotiation == null)
        {
            throw new InvalidOperationException("Negotiation not found");
        }
        EnsureCurrentTenant(negotiation);

        if (negotiation.Status == "Completed" || negotiation.Status == "Cancelled")
        {
            throw new InvalidOperationException("Negotiation is already completed or cancelled");
        }

        var award = await _awardRepository.GetByTenderIdAsync(negotiation.TenderId);
        if (award == null || award.TenderBidId != negotiation.TenderBidId ||
            award.TenantId != negotiation.TenantId)
            throw new InvalidOperationException(
                "The negotiation does not match a current award recommendation.");
        if (award.Status is not ("PendingApproval" or "Awarded"))
            throw new InvalidOperationException(
                $"Negotiation completion requires a pending or approved award (current status: '{award.Status}').");
        var tender = await _tenderRepository.GetByIdAsync(award.TenderId)
            ?? throw new InvalidOperationException("Tender not found for negotiated award.");
        var bid = await _bidRepository.GetByIdAsync(award.TenderBidId)
            ?? throw new InvalidOperationException("Bid not found for negotiated award.");
        if (tender.TenantId != award.TenantId || bid.TenantId != award.TenantId ||
            bid.TenderId != tender.Id || bid.BusinessPartnerId != award.BusinessPartnerId)
            throw new InvalidOperationException("The negotiated award lineage is invalid.");
        if (tender.Status is not ("Evaluated" or "Awarded") ||
            bid.Status is not ("Evaluated" or "Awarded"))
            throw new InvalidOperationException(
                "The tender and bid are not in a negotiable evaluated/awarded state.");
        await EnsureAwardHasNoDownstreamCommitmentAsync(award);

        var userId = Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : (Guid?)null;
        if (!userId.HasValue)
            throw new InvalidOperationException("An authenticated negotiator is required.");

        // Update all items with negotiated prices
        foreach (var itemDto in dto.Items)
        {
            var item = negotiation.Items.FirstOrDefault(i => i.Id == itemDto.ItemId);
            if (item != null)
            {
                item.NegotiatedUnitPrice = itemDto.NegotiatedUnitPrice;
                item.NegotiatedTotalPrice = itemDto.NegotiatedUnitPrice.HasValue
                    ? itemDto.NegotiatedUnitPrice.Value * item.Quantity
                    : null;
                item.Notes = itemDto.Notes;
                await _negotiationRepository.UpdateItemAsync(item);
            }
        }

        // Calculate total negotiated amount
        var negotiatedAmount = negotiation.Items.Sum(i => i.NegotiatedTotalPrice ?? i.OriginalTotalPrice);

        negotiation.Status = "Completed";
        negotiation.CompletedDate = DateTime.UtcNow;
        negotiation.CompletedById = userId;
        negotiation.NegotiatedAmount = negotiatedAmount;
        negotiation.Notes = dto.Notes ?? negotiation.Notes;

        await _negotiationRepository.UpdateAsync(negotiation);

        // Update the associated award with negotiation data
        // Try to find award by tender and bid (for tender-level awards)
        if (award.TenderBidId == negotiation.TenderBidId)
        {
            // Store original bid amount if not already set (first negotiation)
            if (!award.IsNegotiated)
            {
                award.OriginalBidAmount = award.AwardedAmount;
            }
            
            // Update award with negotiation data
            award.IsNegotiated = true;
            award.NegotiationId = negotiation.Id;
            award.AwardedAmount = negotiatedAmount;
            award.Status = "PendingApproval";
            award.AwardedById = null;
            award.CreatedById = userId.Value;
            award.LastModifiedById = userId.Value;
            award.UpdatedAt = DateTime.UtcNow;
            award.Notes = string.IsNullOrWhiteSpace(award.Notes)
                ? "Commercial negotiation completed; independent award reapproval is required."
                : $"{award.Notes}\nCommercial negotiation completed; independent award reapproval is required.";
            
            await _awardRepository.UpdateAsync(award);

            tender.Status = "Evaluated";
            tender.AwardDate = null;
            tender.AwardedById = null;
            bid.Status = "Evaluated";
            await _tenderRepository.UpdateAsync(tender);
            await _bidRepository.UpdateAsync(bid);
            
            _logger.LogInformation(
                "Updated award {AwardId} with negotiation data. Original: {OriginalAmount}, Negotiated: {NegotiatedAmount}",
                award.Id, award.OriginalBidAmount, negotiatedAmount);
        }
        else
        {
            _logger.LogWarning(
                "No matching award found for negotiation {NegotiationId} (TenderId: {TenderId}, BidId: {BidId})",
                negotiationId, negotiation.TenderId, negotiation.TenderBidId);
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Completed negotiation {NegotiationId} with amount {Amount}", negotiationId, negotiatedAmount);

        return await GetByIdAsync(negotiationId) ?? throw new InvalidOperationException("Failed to complete negotiation");
    }

    private async Task EnsureAwardHasNoDownstreamCommitmentAsync(Guid tenderId, Guid bidId)
    {
        var award = await _awardRepository.GetByTenderIdAsync(tenderId);
        if (award != null && award.TenderBidId == bidId)
            await EnsureAwardHasNoDownstreamCommitmentAsync(award);
    }

    private async Task EnsureAwardHasNoDownstreamCommitmentAsync(TenderAward award)
    {
        if (award.PurchaseOrderId.HasValue ||
            await _contractRepository.GetByAwardIdAsync(award.Id) != null)
            throw new InvalidOperationException(
                "Negotiation cannot start or change commercials after the award produced a purchase order or contract.");
    }

    public async Task CancelNegotiationAsync(Guid negotiationId)
    {
        var negotiation = await _negotiationRepository.GetByIdAsync(negotiationId);
        if (negotiation == null)
        {
            throw new InvalidOperationException("Negotiation not found");
        }
        EnsureCurrentTenant(negotiation);

        if (negotiation.Status == "Completed")
        {
            throw new InvalidOperationException("Cannot cancel a completed negotiation");
        }

        negotiation.Status = "Cancelled";
        await _negotiationRepository.UpdateAsync(negotiation);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Cancelled negotiation {NegotiationId}", negotiationId);
    }

    private Guid RequireTenantId() => _currentUserService.TenantId
        ?? throw new InvalidOperationException("Tenant ID is required");

    private bool IsCurrentTenant(TenderNegotiation negotiation) =>
        negotiation.TenantId == RequireTenantId();

    private void EnsureCurrentTenant(TenderNegotiation negotiation)
    {
        if (!IsCurrentTenant(negotiation))
            throw new UnauthorizedAccessException(
                "The negotiation does not belong to the current tenant.");
    }

    private static TenderNegotiationDto MapToDto(TenderNegotiation negotiation)
    {
        return new TenderNegotiationDto
        {
            Id = negotiation.Id,
            TenderId = negotiation.TenderId,
            TenderNumber = negotiation.Tender?.TenderNumber,
            TenderTitle = negotiation.Tender?.Title,
            TenderBidId = negotiation.TenderBidId,
            BidNumber = negotiation.TenderBid?.BidNumber,
            BusinessPartnerId = negotiation.BusinessPartnerId,
            BusinessPartnerName = negotiation.BusinessPartner?.PartnerName,
            LotId = negotiation.LotId,
            LotCode = negotiation.Lot?.LotCode,
            LotTitle = negotiation.Lot?.Title,
            BidLotId = negotiation.BidLotId,
            Status = negotiation.Status,
            InvitedDate = negotiation.InvitedDate,
            InvitedById = negotiation.InvitedById,
            InvitedByName = negotiation.InvitedBy != null ? $"{negotiation.InvitedBy.FirstName} {negotiation.InvitedBy.LastName}" : null,
            CompletedDate = negotiation.CompletedDate,
            CompletedById = negotiation.CompletedById,
            CompletedByName = negotiation.CompletedBy != null ? $"{negotiation.CompletedBy.FirstName} {negotiation.CompletedBy.LastName}" : null,
            OriginalAmount = negotiation.OriginalAmount,
            NegotiatedAmount = negotiation.NegotiatedAmount,
            Currency = negotiation.Currency,
            Notes = negotiation.Notes,
            Items = negotiation.Items.Select(i => new TenderNegotiationItemDto
            {
                Id = i.Id,
                NegotiationId = i.NegotiationId,
                TenderBidItemId = i.TenderBidItemId,
                ItemDescription = i.ItemDescription,
                Quantity = i.Quantity,
                UnitOfMeasure = i.UnitOfMeasure,
                OriginalUnitPrice = i.OriginalUnitPrice,
                OriginalTotalPrice = i.OriginalTotalPrice,
                NegotiatedUnitPrice = i.NegotiatedUnitPrice,
                NegotiatedTotalPrice = i.NegotiatedTotalPrice,
                Notes = i.Notes
            }).ToList(),
            CreatedAt = negotiation.CreatedAt
        };
    }
}
