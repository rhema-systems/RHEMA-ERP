using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class BlacklistHistoryService : IBlacklistHistoryService
{
    private readonly IBlacklistHistoryRepository _historyRepository;
    private readonly IBusinessPartnerRepository _partnerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<BlacklistHistoryService> _logger;

    public BlacklistHistoryService(
        IBlacklistHistoryRepository historyRepository,
        IBusinessPartnerRepository partnerRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<BlacklistHistoryService> logger)
    {
        _historyRepository = historyRepository;
        _partnerRepository = partnerRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<IEnumerable<BlacklistHistoryDto>> GetByBusinessPartnerAsync(Guid businessPartnerId)
    {
        var history = await _historyRepository.GetByBusinessPartnerAsync(businessPartnerId);
        return history.Select(MapToDto);
    }

    public async Task<IEnumerable<BlacklistHistoryDto>> GetByActionAsync(string action)
    {
        var history = await _historyRepository.GetByActionAsync(action);
        return history.Select(MapToDto);
    }

    public async Task<IEnumerable<BlacklistHistoryDto>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        var history = await _historyRepository.GetByDateRangeAsync(startDate, endDate);
        return history.Select(MapToDto);
    }

    public async Task RecordHistoryAsync(Guid businessPartnerId, string action, string? reason = null, Guid? relatedAppealId = null, string? notes = null)
    {
        _logger.LogInformation("Recording blacklist history for BusinessPartner {BusinessPartnerId}, Action: {Action}", businessPartnerId, action);

        var partner = await _partnerRepository.GetByIdAsync(businessPartnerId);
        if (partner == null)
        {
            _logger.LogWarning("Business partner {BusinessPartnerId} not found, skipping history record", businessPartnerId);
            return;
        }

        var history = new BlacklistHistory
        {
            Id = Guid.NewGuid(),
            BusinessPartnerId = businessPartnerId,
            Action = action,
            ActionDate = DateTime.UtcNow,
            ActionById = _currentUserProvider.UserId,
            Reason = reason,
            BlacklistDate = partner.BlacklistDate,
            BlacklistExpiryDate = partner.BlacklistExpiryDate,
            RelatedAppealId = relatedAppealId,
            Notes = notes,
            TenantId = _currentUserProvider.TenantId,
            CreatedAt = DateTime.UtcNow,
            CreatedById = _currentUserProvider.UserId
        };

        await _historyRepository.CreateAsync(history);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Blacklist history recorded with ID {HistoryId}", history.Id);
    }

    private BlacklistHistoryDto MapToDto(BlacklistHistory history)
    {
        return new BlacklistHistoryDto
        {
            Id = history.Id,
            BusinessPartnerId = history.BusinessPartnerId,
            PartnerName = history.BusinessPartner?.PartnerName,
            Action = history.Action,
            ActionDate = history.ActionDate,
            ActionById = history.ActionById,
            ActionByName = history.ActionByName ?? history.ActionBy?.FullName,
            Reason = history.Reason,
            BlacklistDate = history.BlacklistDate,
            BlacklistExpiryDate = history.BlacklistExpiryDate,
            RelatedAppealId = history.RelatedAppealId,
            RelatedAppealNumber = history.RelatedAppeal?.AppealNumber,
            Notes = history.Notes
        };
    }
}

