using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class PartnerBlacklistService : IPartnerBlacklistService
{
    private readonly IBusinessPartnerRepository _partnerRepository;
    private readonly IBlacklistHistoryService _historyService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<PartnerBlacklistService> _logger;

    public PartnerBlacklistService(
        IBusinessPartnerRepository partnerRepository,
        IBlacklistHistoryService historyService,
        ICurrentUserProvider currentUserProvider,
        ILogger<PartnerBlacklistService> logger)
    {
        _partnerRepository = partnerRepository;
        _historyService = historyService;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task AddToBlacklistAsync(Guid partnerId, string reason, Guid addedById, DateTime? expiryDate = null)
    {
        _logger.LogInformation("Adding business partner {PartnerId} to blacklist. Reason: {Reason}, AddedBy: {AddedById}",
            partnerId, reason, addedById);

        var partner = await _partnerRepository.GetByIdAsync(partnerId);
        if (partner == null)
        {
            throw new ArgumentException($"Business partner with ID {partnerId} not found");
        }

        if (partner.IsBlacklisted)
        {
            _logger.LogWarning("Business partner {PartnerId} is already blacklisted", partnerId);
            throw new InvalidOperationException("Business partner is already blacklisted");
        }

        // Repository method already calls SaveChangesAsync
        await _partnerRepository.AddToBlacklistAsync(partnerId, reason, expiryDate);

        // Record history
        await _historyService.RecordHistoryAsync(partnerId, "Added", reason, null, expiryDate.HasValue ? $"Expires on {expiryDate:yyyy-MM-dd}" : "No expiry date");

        _logger.LogInformation("Business partner {PartnerId} successfully added to blacklist", partnerId);
    }

    public async Task RemoveFromBlacklistAsync(Guid partnerId, Guid removedById)
    {
        _logger.LogInformation("Removing business partner {PartnerId} from blacklist. RemovedBy: {RemovedById}",
            partnerId, removedById);

        var partner = await _partnerRepository.GetByIdAsync(partnerId);
        if (partner == null)
        {
            throw new ArgumentException($"Business partner with ID {partnerId} not found");
        }

        if (!partner.IsBlacklisted)
        {
            _logger.LogWarning("Business partner {PartnerId} is not blacklisted", partnerId);
            throw new InvalidOperationException("Business partner is not blacklisted");
        }

        // Repository method already calls SaveChangesAsync
        await _partnerRepository.RemoveFromBlacklistAsync(partnerId);

        // Record history
        await _historyService.RecordHistoryAsync(partnerId, "Removed", "Manually removed from blacklist", null, null);

        _logger.LogInformation("Business partner {PartnerId} successfully removed from blacklist", partnerId);
    }

    public async Task<IEnumerable<BusinessPartnerDto>> GetBlacklistedPartnersAsync()
    {
        _logger.LogInformation("Retrieving all blacklisted business partners");

        var partners = await _partnerRepository.GetBlacklistedPartnersAsync();

        return partners.Select(p => new BusinessPartnerDto
        {
            Id = p.Id,
            PartnerCode = p.PartnerCode,
            PartnerName = p.PartnerName,
            PartnerType = p.PartnerType,
            Email = p.PrimaryEmail,
            Phone = p.PrimaryPhone,
            Website = p.Website,
            PhysicalAddress = p.PhysicalAddress,
            City = p.PhysicalCity,
            Country = p.PhysicalCountry,
            Status = p.RegistrationStatus,
            ApprovalStatus = p.ApprovalStatus,
            PerformanceRating = p.PerformanceRating,
            RiskLevel = p.RiskLevel,
            IsPreferred = p.IsPreferred,
            IsBlacklisted = p.IsBlacklisted,
            CreatedAt = p.CreatedAt
        });
    }

    public async Task<IEnumerable<BusinessPartnerDto>> GetPartnersWithExpiringBlacklistAsync(int daysAhead = 30)
    {
        _logger.LogInformation("Retrieving business partners with blacklists expiring in {DaysAhead} days", daysAhead);

        var partners = await _partnerRepository.GetPartnersWithExpiringBlacklistAsync(daysAhead);

        return partners.Select(p => new BusinessPartnerDto
        {
            Id = p.Id,
            PartnerCode = p.PartnerCode,
            PartnerName = p.PartnerName,
            PartnerType = p.PartnerType,
            Email = p.PrimaryEmail,
            Phone = p.PrimaryPhone,
            Website = p.Website,
            PhysicalAddress = p.PhysicalAddress,
            City = p.PhysicalCity,
            Country = p.PhysicalCountry,
            Status = p.RegistrationStatus,
            ApprovalStatus = p.ApprovalStatus,
            PerformanceRating = p.PerformanceRating,
            RiskLevel = p.RiskLevel,
            IsPreferred = p.IsPreferred,
            IsBlacklisted = p.IsBlacklisted,
            CreatedAt = p.CreatedAt
        });
    }

    public async Task<bool> IsBlacklistedAsync(Guid partnerId)
    {
        var partner = await _partnerRepository.GetByIdAsync(partnerId);
        return partner?.IsBlacklisted ?? false;
    }
}
