using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class PartnerPerformanceService : IPartnerPerformanceService
{
    private readonly IBusinessPartnerRepository _partnerRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<PartnerPerformanceService> _logger;

    public PartnerPerformanceService(
        IBusinessPartnerRepository partnerRepository,
        ICurrentUserProvider currentUserProvider,
        ILogger<PartnerPerformanceService> logger)
    {
        _partnerRepository = partnerRepository;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task UpdatePerformanceRatingAsync(Guid partnerId, decimal rating) { await Task.CompletedTask; throw new NotImplementedException(); }
    public async Task UpdateQualityScoreAsync(Guid partnerId, int score) { await Task.CompletedTask; throw new NotImplementedException(); }
    public async Task UpdateDeliveryScoreAsync(Guid partnerId, int score) { await Task.CompletedTask; throw new NotImplementedException(); }
    public async Task UpdateComplianceScoreAsync(Guid partnerId, int score) { await Task.CompletedTask; throw new NotImplementedException(); }
    public async Task RecordPerformanceReviewAsync(Guid partnerId, Guid reviewedById) { await Task.CompletedTask; throw new NotImplementedException(); }
    public async Task<BusinessPartnerDetailDto?> GetPerformanceMetricsAsync(Guid partnerId) { await Task.CompletedTask; throw new NotImplementedException(); }
}
