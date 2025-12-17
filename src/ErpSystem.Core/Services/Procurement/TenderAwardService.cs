using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class TenderAwardService : ITenderAwardService
{
    private readonly ITenderAwardRepository _awardRepository;
    private readonly ITenderRepository _tenderRepository;
    private readonly ITenderBidRepository _bidRepository;
    private readonly ITenderEvaluationRepository _evaluationRepository;
    private readonly ITenderNotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<TenderAwardService> _logger;

    public TenderAwardService(
        ITenderAwardRepository awardRepository,
        ITenderRepository tenderRepository,
        ITenderBidRepository bidRepository,
        ITenderEvaluationRepository evaluationRepository,
        ITenderNotificationService notificationService,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<TenderAwardService> logger)
    {
        _awardRepository = awardRepository;
        _tenderRepository = tenderRepository;
        _bidRepository = bidRepository;
        _evaluationRepository = evaluationRepository;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
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
            var evaluatedBids = bids.Where(b => b.Status == "Evaluated").ToList();

            var bidRecommendations = new List<BidRecommendationDto>();

            foreach (var bid in evaluatedBids)
            {
                var evaluations = await _evaluationRepository.GetByBidIdAsync(bid.Id);
                var submittedEvaluations = evaluations.Where(e => e.Status == "Submitted").ToList();
                var avgScore = submittedEvaluations.Any() ? (submittedEvaluations.Average(e => e.TotalScore) ?? 0) : 0;

                // Count how many evaluators recommended this bid
                var recommendationCount = submittedEvaluations.Count(e => e.IsRecommended);
                var totalEvaluators = submittedEvaluations.Count;

                bidRecommendations.Add(new BidRecommendationDto
                {
                    BidId = bid.Id,
                    BidNumber = bid.BidNumber,
                    BusinessPartnerId = bid.BusinessPartnerId,
                    BusinessPartnerName = bid.BusinessPartner?.PartnerName ?? string.Empty,
                    TotalBidAmount = bid.TotalBidAmount,
                    AverageScore = avgScore,
                    EvaluationCount = totalEvaluators,
                    RecommendationCount = recommendationCount,
                    TotalEvaluators = totalEvaluators,
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
                EvaluatedBids = evaluatedBids.Count,
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

    public async Task<TenderAwardDto> CreateAwardAsync(Guid tenderId, CreateAwardDto dto)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");

            var bid = await _bidRepository.GetByIdAsync(dto.TenderBidId)
                ?? throw new InvalidOperationException($"Bid with ID {dto.TenderBidId} not found");

            // Check if award already exists for this tender
            var existingAward = await _awardRepository.GetByTenderIdAsync(tenderId);
            if (existingAward != null)
            {
                throw new InvalidOperationException($"Award already exists for tender {tender.TenderNumber}");
            }

            var award = new TenderAward
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderId = tenderId,
                TenderBidId = dto.TenderBidId,
                BusinessPartnerId = bid.BusinessPartnerId,
                AwardDate = dto.AwardDate ?? DateTime.UtcNow,
                AwardedAmount = dto.AwardedAmount,
                Currency = dto.Currency ?? "USD",
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

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created award {AwardId} for tender {TenderId}. Rejected {RejectedCount} other bids.",
                award.Id, tenderId, rejectedBids.Count);

            // Send award notification to winner
            await _notificationService.SendAwardNotificationAsync(award.Id);

            // Send rejection notifications to unsuccessful bidders
            var rejectedBidIds = rejectedBids.Select(b => b.Id).ToList();
            if (rejectedBidIds.Any())
            {
                await _notificationService.SendRejectionNotificationsAsync(tenderId, rejectedBidIds);
            }

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

            var bid = await _bidRepository.GetByIdAsync(dto.TenderBidId)
                ?? throw new InvalidOperationException($"Bid with ID {dto.TenderBidId} not found");

            var tender = await _tenderRepository.GetByIdAsync(award.TenderId)
                ?? throw new InvalidOperationException($"Tender with ID {award.TenderId} not found");

            award.AwardedAmount = dto.AwardedAmount;
            award.Currency = dto.Currency ?? "USD";
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

    private static TenderAwardDto MapToDto(TenderAward award, TenderBid? bid, Tender? tender)
    {
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
            AwardedAmount = award.AwardedAmount,
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
}

