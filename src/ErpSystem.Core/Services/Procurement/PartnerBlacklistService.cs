using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class PartnerBlacklistService : IPartnerBlacklistService
{
    private readonly IBusinessPartnerRepository _partnerRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<PartnerBlacklistService> _logger;

    public PartnerBlacklistService(
        IBusinessPartnerRepository partnerRepository,
        ICurrentUserProvider currentUserProvider,
        ILogger<PartnerBlacklistService> logger)
    {
        _partnerRepository = partnerRepository;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task AddToBlacklistAsync(Guid partnerId, string reason, Guid addedById, DateTime? expiryDate = null) { await Task.CompletedTask; throw new NotImplementedException(); }
    public async Task RemoveFromBlacklistAsync(Guid partnerId, Guid removedById) { await Task.CompletedTask; throw new NotImplementedException(); }
    public async Task<IEnumerable<BusinessPartnerDto>> GetBlacklistedPartnersAsync() { await Task.CompletedTask; throw new NotImplementedException(); }
    public async Task<IEnumerable<BusinessPartnerDto>> GetPartnersWithExpiringBlacklistAsync(int daysAhead = 30) { await Task.CompletedTask; throw new NotImplementedException(); }
    public async Task<bool> IsBlacklistedAsync(Guid partnerId) { await Task.CompletedTask; throw new NotImplementedException(); }
}
