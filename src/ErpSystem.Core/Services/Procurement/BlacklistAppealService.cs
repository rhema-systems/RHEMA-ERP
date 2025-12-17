using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class BlacklistAppealService : IBlacklistAppealService
{
    private readonly IBlacklistAppealRepository _appealRepository;
    private readonly IBusinessPartnerRepository _partnerRepository;
    private readonly IBlacklistHistoryService _historyService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<BlacklistAppealService> _logger;

    public BlacklistAppealService(
        IBlacklistAppealRepository appealRepository,
        IBusinessPartnerRepository partnerRepository,
        IBlacklistHistoryService historyService,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<BlacklistAppealService> logger)
    {
        _appealRepository = appealRepository;
        _partnerRepository = partnerRepository;
        _historyService = historyService;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<BlacklistAppealDto?> GetByIdAsync(Guid id)
    {
        var appeal = await _appealRepository.GetByIdAsync(id);
        return appeal == null ? null : MapToDto(appeal);
    }

    public async Task<BlacklistAppealDto?> GetByAppealNumberAsync(string appealNumber)
    {
        var appeal = await _appealRepository.GetByAppealNumberAsync(appealNumber);
        return appeal == null ? null : MapToDto(appeal);
    }

    public async Task<IEnumerable<BlacklistAppealDto>> GetByBusinessPartnerAsync(Guid businessPartnerId)
    {
        var appeals = await _appealRepository.GetByBusinessPartnerAsync(businessPartnerId);
        return appeals.Select(MapToDto);
    }

    public async Task<IEnumerable<BlacklistAppealDto>> GetByStatusAsync(string status)
    {
        var appeals = await _appealRepository.GetByStatusAsync(status);
        return appeals.Select(MapToDto);
    }

    public async Task<IEnumerable<BlacklistAppealDto>> GetPendingAppealsAsync()
    {
        var appeals = await _appealRepository.GetPendingAppealsAsync();
        return appeals.Select(MapToDto);
    }

    public async Task<BlacklistAppealDto> CreateAppealAsync(CreateBlacklistAppealDto createDto)
    {
        _logger.LogInformation("Creating blacklist appeal for BusinessPartner {BusinessPartnerId}", createDto.BusinessPartnerId);

        var partner = await _partnerRepository.GetByIdAsync(createDto.BusinessPartnerId);
        if (partner == null)
        {
            throw new ArgumentException($"Business partner with ID {createDto.BusinessPartnerId} not found");
        }

        if (!partner.IsBlacklisted)
        {
            throw new InvalidOperationException("Business partner is not blacklisted");
        }

        var appealNumber = await _appealRepository.GenerateAppealNumberAsync();

        var appeal = new BlacklistAppeal
        {
            Id = Guid.NewGuid(),
            BusinessPartnerId = createDto.BusinessPartnerId,
            AppealNumber = appealNumber,
            AppealDate = DateTime.UtcNow,
            Status = "Pending",
            AppealReason = createDto.AppealReason,
            SupportingDocuments = createDto.SupportingDocuments,
            CorrectiveActionsTaken = createDto.CorrectiveActionsTaken,
            PreventiveMeasures = createDto.PreventiveMeasures,
            TenantId = _currentUserProvider.TenantId,
            CreatedAt = DateTime.UtcNow,
            CreatedById = _currentUserProvider.UserId
        };

        await _appealRepository.CreateAsync(appeal);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Blacklist appeal created with ID {AppealId} and number {AppealNumber}", appeal.Id, appeal.AppealNumber);

        return MapToDto(appeal);
    }

    public async Task<BlacklistAppealDto> ReviewAppealAsync(Guid appealId, ReviewBlacklistAppealDto reviewDto)
    {
        var appeal = await _appealRepository.GetByIdAsync(appealId);
        if (appeal == null)
        {
            throw new ArgumentException($"Blacklist appeal with ID {appealId} not found");
        }

        if (appeal.Status != "Pending")
        {
            throw new InvalidOperationException($"Only pending appeals can be reviewed. Current status: {appeal.Status}");
        }

        appeal.Status = "UnderReview";
        appeal.ReviewedById = _currentUserProvider.UserId;
        appeal.ReviewedDate = DateTime.UtcNow;
        appeal.ReviewerComments = reviewDto.ReviewerComments;
        appeal.UpdatedAt = DateTime.UtcNow;
        appeal.LastModifiedById = _currentUserProvider.UserId;

        await _appealRepository.UpdateAsync(appeal);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Blacklist appeal {AppealId} marked as under review", appealId);

        return MapToDto(appeal);
    }

    public async Task<BlacklistAppealDto> ApproveAppealAsync(Guid appealId, ApproveBlacklistAppealDto approveDto)
    {
        var appeal = await _appealRepository.GetByIdAsync(appealId);
        if (appeal == null)
        {
            throw new ArgumentException($"Blacklist appeal with ID {appealId} not found");
        }

        if (appeal.Status == "Approved" || appeal.Status == "Rejected")
        {
            throw new InvalidOperationException($"Appeal has already been {appeal.Status.ToLower()}");
        }

        appeal.Status = "Approved";
        appeal.ApprovedById = _currentUserProvider.UserId;
        appeal.ApprovedDate = DateTime.UtcNow;
        appeal.RemoveBlacklist = approveDto.RemoveBlacklist;
        appeal.NewBlacklistExpiryDate = approveDto.NewBlacklistExpiryDate;
        appeal.DecisionNotes = approveDto.DecisionNotes;
        appeal.UpdatedAt = DateTime.UtcNow;
        appeal.LastModifiedById = _currentUserProvider.UserId;

        await _appealRepository.UpdateAsync(appeal);

        // Update partner blacklist status if approved
        var partner = await _partnerRepository.GetByIdAsync(appeal.BusinessPartnerId);
        if (partner != null)
        {
            if (approveDto.RemoveBlacklist)
            {
                await _partnerRepository.RemoveFromBlacklistAsync(partner.Id);
                await _historyService.RecordHistoryAsync(partner.Id, "AppealApproved", "Blacklist removed via approved appeal", appealId, approveDto.DecisionNotes);
            }
            else if (approveDto.NewBlacklistExpiryDate.HasValue)
            {
                partner.BlacklistExpiryDate = approveDto.NewBlacklistExpiryDate;
                partner.UpdatedAt = DateTime.UtcNow;
                partner.LastModifiedById = _currentUserProvider.UserId;
                await _partnerRepository.UpdateAsync(partner);
                await _historyService.RecordHistoryAsync(partner.Id, "AppealApproved", $"Blacklist expiry updated to {approveDto.NewBlacklistExpiryDate:yyyy-MM-dd}", appealId, approveDto.DecisionNotes);
            }
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Blacklist appeal {AppealId} approved", appealId);

        return MapToDto(appeal);
    }

    public async Task<BlacklistAppealDto> RejectAppealAsync(Guid appealId, RejectBlacklistAppealDto rejectDto)
    {
        var appeal = await _appealRepository.GetByIdAsync(appealId);
        if (appeal == null)
        {
            throw new ArgumentException($"Blacklist appeal with ID {appealId} not found");
        }

        if (appeal.Status == "Approved" || appeal.Status == "Rejected")
        {
            throw new InvalidOperationException($"Appeal has already been {appeal.Status.ToLower()}");
        }

        appeal.Status = "Rejected";
        appeal.ApprovedById = _currentUserProvider.UserId;
        appeal.ApprovedDate = DateTime.UtcNow;
        appeal.RejectionReason = rejectDto.RejectionReason;
        appeal.DecisionNotes = rejectDto.DecisionNotes;
        appeal.UpdatedAt = DateTime.UtcNow;
        appeal.LastModifiedById = _currentUserProvider.UserId;

        await _appealRepository.UpdateAsync(appeal);
        await _historyService.RecordHistoryAsync(appeal.BusinessPartnerId, "AppealRejected", rejectDto.RejectionReason, appealId, rejectDto.DecisionNotes);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Blacklist appeal {AppealId} rejected", appealId);

        return MapToDto(appeal);
    }

    public async Task DeleteAppealAsync(Guid id)
    {
        await _appealRepository.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    private BlacklistAppealDto MapToDto(BlacklistAppeal appeal)
    {
        return new BlacklistAppealDto
        {
            Id = appeal.Id,
            BusinessPartnerId = appeal.BusinessPartnerId,
            PartnerName = appeal.BusinessPartner?.PartnerName,
            PartnerCode = appeal.BusinessPartner?.PartnerCode,
            AppealNumber = appeal.AppealNumber,
            AppealDate = appeal.AppealDate,
            Status = appeal.Status,
            AppealReason = appeal.AppealReason,
            SupportingDocuments = appeal.SupportingDocuments,
            CorrectiveActionsTaken = appeal.CorrectiveActionsTaken,
            PreventiveMeasures = appeal.PreventiveMeasures,
            ReviewedById = appeal.ReviewedById,
            ReviewedByName = appeal.ReviewedBy?.FullName,
            ReviewedDate = appeal.ReviewedDate,
            ReviewerComments = appeal.ReviewerComments,
            RejectionReason = appeal.RejectionReason,
            ApprovedById = appeal.ApprovedById,
            ApprovedByName = appeal.ApprovedBy?.FullName,
            ApprovedDate = appeal.ApprovedDate,
            RemoveBlacklist = appeal.RemoveBlacklist,
            NewBlacklistExpiryDate = appeal.NewBlacklistExpiryDate,
            DecisionNotes = appeal.DecisionNotes,
            CreatedAt = appeal.CreatedAt
        };
    }
}

