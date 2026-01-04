using ErpSystem.Core.DTOs.Notifications;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class PerformanceBondService : IPerformanceBondService
{
    private readonly IPerformanceBondRequestRepository _repository;
    private readonly ITenderAwardRepository _awardRepository;
    private readonly ITenderBidRepository _bidRepository;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;
    private readonly ITenderRepository _tenderRepository;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<PerformanceBondService> _logger;

    public PerformanceBondService(
        IPerformanceBondRequestRepository repository,
        ITenderAwardRepository awardRepository,
        ITenderBidRepository bidRepository,
        IBusinessPartnerRepository businessPartnerRepository,
        ITenderRepository tenderRepository,
        INotificationService notificationService,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<PerformanceBondService> logger)
    {
        _repository = repository;
        _awardRepository = awardRepository;
        _bidRepository = bidRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _tenderRepository = tenderRepository;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<PerformanceBondRequestDto?> GetByIdAsync(Guid id)
    {
        var request = await _repository.GetByIdAsync(id);
        return request != null ? MapToDto(request) : null;
    }

    public async Task<PerformanceBondRequestDto?> GetByAwardIdAsync(Guid awardId)
    {
        var request = await _repository.GetByAwardIdAsync(awardId);
        return request != null ? MapToDto(request) : null;
    }

    public async Task<PerformanceBondRequestDto?> GetByBidIdAsync(Guid bidId)
    {
        var request = await _repository.GetByBidIdAsync(bidId);
        return request != null ? MapToDto(request) : null;
    }

    public async Task<IEnumerable<PerformanceBondRequestDto>> GetByBusinessPartnerIdAsync(Guid businessPartnerId)
    {
        var requests = await _repository.GetByBusinessPartnerIdAsync(businessPartnerId);
        return requests.Select(MapToDto);
    }

    public async Task<IEnumerable<PerformanceBondRequestDto>> GetPendingRequestsAsync()
    {
        var requests = await _repository.GetPendingRequestsAsync();
        return requests.Select(MapToDto);
    }

    public async Task<IEnumerable<PerformanceBondRequestDto>> GetByStatusAsync(string status)
    {
        var requests = await _repository.GetByStatusAsync(status);
        return requests.Select(MapToDto);
    }

    public async Task<PerformanceBondRequestDto> CreateRequestAsync(
        CreatePerformanceBondRequestDto dto,
        string? templateFilePath,
        string? templateFileName,
        string? templateFileType,
        long? templateFileSize)
    {
        var award = await _awardRepository.GetByIdAsync(dto.TenderAwardId)
            ?? throw new InvalidOperationException($"Award with ID {dto.TenderAwardId} not found");

        // Check if a request already exists
        var existing = await _repository.GetByAwardIdAsync(dto.TenderAwardId);
        if (existing != null)
        {
            throw new InvalidOperationException("A performance bond request already exists for this award");
        }

        // Get related entities for notification
        var businessPartner = await _businessPartnerRepository.GetByIdAsync(dto.BusinessPartnerId);
        var bid = await _bidRepository.GetByIdAsync(dto.TenderBidId);
        var tender = bid != null ? await _tenderRepository.GetByIdAsync(bid.TenderId) : null;

        var request = new PerformanceBondRequest
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUserProvider.TenantId,
            TenderAwardId = dto.TenderAwardId,
            TenderBidId = dto.TenderBidId,
            BusinessPartnerId = dto.BusinessPartnerId,
            Status = "Pending",
            TemplateFilePath = templateFilePath,
            TemplateFileName = templateFileName,
            TemplateFileType = templateFileType,
            TemplateFileSize = templateFileSize,
            RequestedDate = DateTime.UtcNow,
            RequestedById = _currentUserProvider.UserId,
            Notes = dto.Notes,
            CreatedAt = DateTime.UtcNow,
            CreatedById = _currentUserProvider.UserId
        };

        await _repository.CreateAsync(request);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created performance bond request {RequestId} for award {AwardId}", request.Id, dto.TenderAwardId);

        // Send in-app notification to business partner if they have a linked user account
        if (businessPartner?.UserId.HasValue == true)
        {
            var tenderInfo = tender != null ? $"{tender.TenderNumber} - {tender.Title}" : "your awarded tender";

            await _notificationService.CreateNotificationAsync(
                new CreateNotificationDto
                {
                    RecipientId = businessPartner.UserId.Value,
                    Type = "InApp",
                    Title = "📋 Performance Bond Required",
                    Message = $"A performance bond has been requested for {tenderInfo}. Please download the template and submit your completed bond.",
                    Priority = "High",
                    EntityType = "PerformanceBond",
                    EntityId = request.Id,
                    ActionUrl = $"/external-portal/my-bids/{dto.TenderBidId}"
                },
                _currentUserProvider.UserId,
                _currentUserProvider.TenantId
            );

            _logger.LogInformation("Sent in-app notification to user {UserId} for performance bond request {RequestId}",
                businessPartner.UserId.Value, request.Id);
        }
        else
        {
            _logger.LogWarning("Business partner {PartnerId} does not have a linked user account, skipping in-app notification",
                dto.BusinessPartnerId);
        }

        var created = await _repository.GetByIdAsync(request.Id);
        return MapToDto(created!);
    }

    public async Task<PerformanceBondRequestDto> SubmitBondAsync(
        Guid requestId,
        SubmitPerformanceBondDto dto,
        string filePath,
        string fileName,
        string? fileType,
        long? fileSize)
    {
        var request = await _repository.GetByIdAsync(requestId)
            ?? throw new InvalidOperationException($"Performance bond request with ID {requestId} not found");

        if (request.Status != "Pending" && request.Status != "Rejected")
        {
            throw new InvalidOperationException("Can only submit bond for pending or rejected requests");
        }

        request.SubmittedFilePath = filePath;
        request.SubmittedFileName = fileName;
        request.SubmittedFileType = fileType;
        request.SubmittedFileSize = fileSize;
        request.SubmittedDate = DateTime.UtcNow;
        request.SubmittedById = _currentUserProvider.UserId;
        request.Status = "Submitted";
        request.Notes = dto.Notes ?? request.Notes;
        request.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(request);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Submitted performance bond for request {RequestId}", requestId);

        var updated = await _repository.GetByIdAsync(requestId);
        return MapToDto(updated!);
    }

    public async Task<PerformanceBondRequestDto> ReviewBondAsync(Guid requestId, ReviewPerformanceBondDto dto)
    {
        var request = await _repository.GetByIdAsync(requestId)
            ?? throw new InvalidOperationException($"Performance bond request with ID {requestId} not found");

        if (request.Status != "Submitted")
        {
            throw new InvalidOperationException("Can only review submitted bonds");
        }

        request.Status = dto.IsApproved ? "Approved" : "Rejected";
        request.ReviewedDate = DateTime.UtcNow;
        request.ReviewedById = _currentUserProvider.UserId;
        request.RejectionReason = dto.IsApproved ? null : dto.RejectionReason;
        request.Notes = dto.Notes ?? request.Notes;
        request.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(request);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Reviewed performance bond request {RequestId}, approved: {IsApproved}", requestId, dto.IsApproved);

        var updated = await _repository.GetByIdAsync(requestId);
        return MapToDto(updated!);
    }

    private static PerformanceBondRequestDto MapToDto(PerformanceBondRequest request)
    {
        return new PerformanceBondRequestDto
        {
            Id = request.Id,
            TenderAwardId = request.TenderAwardId,
            TenderBidId = request.TenderBidId,
            BusinessPartnerId = request.BusinessPartnerId,
            BusinessPartnerName = request.BusinessPartner?.PartnerName ?? string.Empty,
            TenderNumber = request.TenderAward?.Tender?.TenderNumber ?? string.Empty,
            TenderTitle = request.TenderAward?.Tender?.Title ?? string.Empty,
            BidNumber = request.TenderBid?.BidNumber ?? string.Empty,
            Status = request.Status,
            TemplateFileName = request.TemplateFileName,
            TemplateFileType = request.TemplateFileType,
            TemplateFileSize = request.TemplateFileSize,
            RequestedDate = request.RequestedDate,
            RequestedByName = request.RequestedBy != null ? $"{request.RequestedBy.FirstName} {request.RequestedBy.LastName}" : null,
            SubmittedFileName = request.SubmittedFileName,
            SubmittedFileType = request.SubmittedFileType,
            SubmittedFileSize = request.SubmittedFileSize,
            SubmittedDate = request.SubmittedDate,
            SubmittedByName = request.SubmittedBy != null ? $"{request.SubmittedBy.FirstName} {request.SubmittedBy.LastName}" : null,
            ReviewedDate = request.ReviewedDate,
            ReviewedByName = request.ReviewedBy != null ? $"{request.ReviewedBy.FirstName} {request.ReviewedBy.LastName}" : null,
            RejectionReason = request.RejectionReason,
            Notes = request.Notes,
            CreatedAt = request.CreatedAt
        };
    }
}

