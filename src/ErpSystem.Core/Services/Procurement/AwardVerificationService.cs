using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class AwardVerificationService : IAwardVerificationService
{
    private readonly IAwardVerificationChecklistTemplateRepository _templateRepository;
    private readonly IAwardVerificationChecklistItemRepository _itemRepository;
    private readonly ITenderAwardVerificationRepository _verificationRepository;
    private readonly ITenderAwardVerificationBidderRepository _bidderRepository;
    private readonly ITenderAwardVerificationItemResultRepository _resultRepository;
    private readonly ITenderAwardVerificationItemDocumentRepository _documentRepository;
    private readonly ITenderRepository _tenderRepository;
    private readonly ITenderBidRepository _bidRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<AwardVerificationService> _logger;

    public AwardVerificationService(
        IAwardVerificationChecklistTemplateRepository templateRepository,
        IAwardVerificationChecklistItemRepository itemRepository,
        ITenderAwardVerificationRepository verificationRepository,
        ITenderAwardVerificationBidderRepository bidderRepository,
        ITenderAwardVerificationItemResultRepository resultRepository,
        ITenderAwardVerificationItemDocumentRepository documentRepository,
        ITenderRepository tenderRepository,
        ITenderBidRepository bidRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<AwardVerificationService> logger)
    {
        _templateRepository = templateRepository;
        _itemRepository = itemRepository;
        _verificationRepository = verificationRepository;
        _bidderRepository = bidderRepository;
        _resultRepository = resultRepository;
        _documentRepository = documentRepository;
        _tenderRepository = tenderRepository;
        _bidRepository = bidRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    #region Checklist Template Management

    public async Task<IEnumerable<AwardVerificationChecklistTemplateDto>> GetAllTemplatesAsync(bool includeInactive = false)
    {
        var templates = await _templateRepository.GetAllAsync(includeInactive);
        return templates.Select(MapTemplateToDto);
    }

    public async Task<PagedResult<AwardVerificationChecklistTemplateDto>> GetTemplatesPagedAsync(int page, int pageSize, string? search = null, bool includeInactive = false)
    {
        var result = await _templateRepository.GetPagedAsync(page, pageSize, search, includeInactive);
        return new PagedResult<AwardVerificationChecklistTemplateDto>
        {
            Items = result.Items.Select(MapTemplateToDto),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task<AwardVerificationChecklistTemplateDto?> GetTemplateByIdAsync(Guid id)
    {
        var template = await _templateRepository.GetByIdWithItemsAsync(id);
        return template == null ? null : MapTemplateToDto(template);
    }

    public async Task<AwardVerificationChecklistTemplateDto?> GetDefaultTemplateAsync()
    {
        var template = await _templateRepository.GetDefaultTemplateAsync();
        return template == null ? null : MapTemplateToDto(template);
    }

    public async Task<IEnumerable<AwardVerificationChecklistTemplateDto>> GetTemplatesByContractValueAsync(decimal contractValue)
    {
        var templates = await _templateRepository.GetByContractValueAsync(contractValue);
        return templates.Select(MapTemplateToDto);
    }

    public async Task<AwardVerificationChecklistTemplateDto> CreateTemplateAsync(CreateAwardVerificationChecklistTemplateDto dto)
    {
        var existingTemplate = await _templateRepository.GetByNameAsync(dto.Name);
        if (existingTemplate != null)
            throw new InvalidOperationException($"A template with name '{dto.Name}' already exists.");

        var template = new AwardVerificationChecklistTemplate
        {
            Name = dto.Name,
            Description = dto.Description,
            Category = dto.Category,
            MinContractValue = dto.MinContractValue,
            MaxContractValue = dto.MaxContractValue,
            IsActive = dto.IsActive,
            IsDefault = dto.IsDefault,
            DisplayOrder = dto.DisplayOrder,
            TenantId = _currentUserProvider.TenantId,
            CreatedById = _currentUserProvider.UserId,
            CreatedAt = DateTime.UtcNow
        };

        // If this is set as default, unset other defaults
        if (dto.IsDefault)
        {
            var currentDefault = await _templateRepository.GetDefaultTemplateAsync();
            if (currentDefault != null)
            {
                currentDefault.IsDefault = false;
                await _templateRepository.UpdateAsync(currentDefault);
            }
        }

        await _templateRepository.CreateAsync(template);

        // Add items
        foreach (var itemDto in dto.Items)
        {
            var item = new AwardVerificationChecklistItem
            {
                TemplateId = template.Id,
                ItemText = itemDto.ItemText,
                Description = itemDto.Description,
                DisplayOrder = itemDto.DisplayOrder,
                IsRequired = itemDto.IsRequired,
                Category = itemDto.Category,
                IsActive = itemDto.IsActive,
                TenantId = template.TenantId,
                CreatedById = template.CreatedById,
                CreatedAt = DateTime.UtcNow
            };
            await _itemRepository.CreateAsync(item);
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created award verification checklist template {TemplateId} with {ItemCount} items", template.Id, dto.Items.Count);

        return await GetTemplateByIdAsync(template.Id) ?? throw new InvalidOperationException("Failed to retrieve created template");
    }

    public async Task<AwardVerificationChecklistTemplateDto> UpdateTemplateAsync(Guid id, UpdateAwardVerificationChecklistTemplateDto dto)
    {
        var template = await _templateRepository.GetByIdAsync(id);
        if (template == null)
            throw new KeyNotFoundException($"Template with ID {id} not found.");

        // Check for duplicate name
        var existingTemplate = await _templateRepository.GetByNameAsync(dto.Name);
        if (existingTemplate != null && existingTemplate.Id != id)
            throw new InvalidOperationException($"A template with name '{dto.Name}' already exists.");

        template.Name = dto.Name;
        template.Description = dto.Description;
        template.Category = dto.Category;
        template.MinContractValue = dto.MinContractValue;
        template.MaxContractValue = dto.MaxContractValue;
        template.IsActive = dto.IsActive;
        template.DisplayOrder = dto.DisplayOrder;
        template.LastModifiedById = _currentUserProvider.UserId;
        template.UpdatedAt = DateTime.UtcNow;

        // Handle default flag
        if (dto.IsDefault && !template.IsDefault)
        {
            var currentDefault = await _templateRepository.GetDefaultTemplateAsync();
            if (currentDefault != null && currentDefault.Id != id)
            {
                currentDefault.IsDefault = false;
                await _templateRepository.UpdateAsync(currentDefault);
            }
        }
        template.IsDefault = dto.IsDefault;

        await _templateRepository.UpdateAsync(template);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Updated award verification checklist template {TemplateId}", id);

        return await GetTemplateByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve updated template");
    }

    public async Task DeleteTemplateAsync(Guid id)
    {
        var template = await _templateRepository.GetByIdAsync(id);
        if (template == null)
            throw new KeyNotFoundException($"Template with ID {id} not found.");

        await _templateRepository.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Deleted award verification checklist template {TemplateId}", id);
    }

    public async Task<AwardVerificationChecklistItemDto> AddTemplateItemAsync(Guid templateId, CreateAwardVerificationChecklistItemDto dto)
    {
        var template = await _templateRepository.GetByIdAsync(templateId);
        if (template == null)
            throw new KeyNotFoundException($"Template with ID {templateId} not found.");

        var item = new AwardVerificationChecklistItem
        {
            TemplateId = templateId,
            ItemText = dto.ItemText,
            Description = dto.Description,
            DisplayOrder = dto.DisplayOrder,
            IsRequired = dto.IsRequired,
            Category = dto.Category,
            IsActive = dto.IsActive,
            TenantId = template.TenantId,
            CreatedById = _currentUserProvider.UserId,
            CreatedAt = DateTime.UtcNow
        };

        await _itemRepository.CreateAsync(item);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Added item {ItemId} to template {TemplateId}", item.Id, templateId);

        return MapItemToDto(item);
    }

    public async Task<AwardVerificationChecklistItemDto> UpdateTemplateItemAsync(Guid itemId, CreateAwardVerificationChecklistItemDto dto)
    {
        var item = await _itemRepository.GetByIdAsync(itemId);
        if (item == null)
            throw new KeyNotFoundException($"Item with ID {itemId} not found.");

        item.ItemText = dto.ItemText;
        item.Description = dto.Description;
        item.DisplayOrder = dto.DisplayOrder;
        item.IsRequired = dto.IsRequired;
        item.Category = dto.Category;
        item.IsActive = dto.IsActive;
        item.LastModifiedById = _currentUserProvider.UserId;
        item.UpdatedAt = DateTime.UtcNow;

        await _itemRepository.UpdateAsync(item);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Updated item {ItemId}", itemId);

        return MapItemToDto(item);
    }

    public async Task DeleteTemplateItemAsync(Guid itemId)
    {
        var item = await _itemRepository.GetByIdAsync(itemId);
        if (item == null)
            throw new KeyNotFoundException($"Item with ID {itemId} not found.");

        await _itemRepository.DeleteAsync(itemId);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Deleted item {ItemId}", itemId);
    }

    #endregion

    #region Verification Process

    public async Task<TenderAwardVerificationDto> StartVerificationAsync(StartAwardVerificationDto dto)
    {
        var tender = await _tenderRepository.GetByIdAsync(dto.TenderId);
        if (tender == null)
            throw new KeyNotFoundException($"Tender with ID {dto.TenderId} not found.");

        // Check if verification already exists
        var existingVerification = await _verificationRepository.GetByTenderIdAsync(dto.TenderId);
        if (existingVerification != null)
            throw new InvalidOperationException($"Verification already exists for tender {dto.TenderId}.");

        // Get template
        AwardVerificationChecklistTemplate? template = null;
        if (dto.TemplateId.HasValue)
        {
            template = await _templateRepository.GetByIdWithItemsAsync(dto.TemplateId.Value);
            if (template == null)
                throw new KeyNotFoundException($"Template with ID {dto.TemplateId} not found.");
        }
        else
        {
            template = await _templateRepository.GetDefaultTemplateAsync();
        }

        if (template == null || !template.Items.Any())
            throw new InvalidOperationException("No template or checklist items available for verification.");

        var userId = _currentUserProvider.UserId;
        var tenantId = _currentUserProvider.TenantId;

        // Create verification record
        var verification = new TenderAwardVerification
        {
            TenderId = dto.TenderId,
            TemplateId = template.Id,
            Status = "InProgress",
            StartedDate = DateTime.UtcNow,
            StartedById = userId,
            Notes = dto.Notes,
            TenantId = tenantId,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow
        };

        await _verificationRepository.CreateAsync(verification);

        // Create bidder records and item results
        foreach (var bidId in dto.SelectedBidIds)
        {
            var bid = await _bidRepository.GetByIdAsync(bidId);
            if (bid == null)
            {
                _logger.LogWarning("Bid {BidId} not found, skipping", bidId);
                continue;
            }

            var bidder = new TenderAwardVerificationBidder
            {
                VerificationId = verification.Id,
                TenderBidId = bidId,
                BusinessPartnerId = bid.BusinessPartnerId,
                Status = "Pending",
                TenantId = tenantId,
                CreatedById = userId,
                CreatedAt = DateTime.UtcNow
            };

            await _bidderRepository.CreateAsync(bidder);

            // Create item results for each checklist item
            var itemResults = template.Items.Where(i => i.IsActive).Select(item => new TenderAwardVerificationItemResult
            {
                BidderId = bidder.Id,
                ChecklistItemId = item.Id,
                IsVerified = false,
                Status = "Pending",
                TenantId = tenantId,
                CreatedById = userId,
                CreatedAt = DateTime.UtcNow
            });

            await _resultRepository.CreateRangeAsync(itemResults);
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Started verification {VerificationId} for tender {TenderId} with {BidderCount} bidders",
            verification.Id, dto.TenderId, dto.SelectedBidIds.Count);

        return await GetVerificationByIdAsync(verification.Id) ?? throw new InvalidOperationException("Failed to retrieve created verification");
    }

    public async Task<TenderAwardVerificationDto?> GetVerificationByTenderIdAsync(Guid tenderId)
    {
        var verification = await _verificationRepository.GetByTenderIdWithDetailsAsync(tenderId);
        return verification == null ? null : MapVerificationToDto(verification);
    }

    public async Task<TenderAwardVerificationDto?> GetVerificationByIdAsync(Guid id)
    {
        var verification = await _verificationRepository.GetByIdWithDetailsAsync(id);
        return verification == null ? null : MapVerificationToDto(verification);
    }

    public async Task<IEnumerable<TenderAwardVerificationDto>> GetPendingVerificationsAsync()
    {
        var verifications = await _verificationRepository.GetPendingVerificationsAsync();
        return verifications.Select(MapVerificationToDto);
    }

    public async Task<TenderAwardVerificationItemResultDto> VerifyChecklistItemAsync(VerifyChecklistItemDto dto)
    {
        var result = await _resultRepository.GetByBidderAndItemAsync(dto.BidderId, dto.ChecklistItemId);
        if (result == null)
            throw new KeyNotFoundException($"Verification item result not found for bidder {dto.BidderId} and item {dto.ChecklistItemId}.");

        result.Status = dto.Status;
        result.IsVerified = true;
        result.Comments = dto.Comments;
        result.VerifiedDate = DateTime.UtcNow;
        result.VerifiedById = _currentUserProvider.UserId;
        result.LastModifiedById = _currentUserProvider.UserId;
        result.UpdatedAt = DateTime.UtcNow;

        await _resultRepository.UpdateAsync(result);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Verified checklist item {ItemId} for bidder {BidderId} with status {Status}",
            dto.ChecklistItemId, dto.BidderId, dto.Status);

        return MapItemResultToDto(result);
    }

    public async Task<TenderAwardVerificationBidderDto> CompleteBidderVerificationAsync(CompleteBidderVerificationDto dto)
    {
        var bidder = await _bidderRepository.GetByIdWithItemsAsync(dto.BidderId);
        if (bidder == null)
            throw new KeyNotFoundException($"Bidder verification with ID {dto.BidderId} not found.");

        // Check if all required items are verified
        var unverifiedRequired = bidder.ItemResults
            .Where(r => !r.IsDeleted && r.ChecklistItem?.IsRequired == true && !r.IsVerified)
            .ToList();

        if (unverifiedRequired.Any())
            throw new InvalidOperationException($"Cannot complete verification. {unverifiedRequired.Count} required items are not verified.");

        // Determine overall status based on item results
        var failedItems = bidder.ItemResults.Count(r => !r.IsDeleted && r.Status == "Failed");
        bidder.Status = failedItems > 0 ? "Failed" : "Passed";
        bidder.OverallComments = dto.OverallComments;
        bidder.VerifiedDate = DateTime.UtcNow;
        bidder.VerifiedById = _currentUserProvider.UserId;
        bidder.LastModifiedById = _currentUserProvider.UserId;
        bidder.UpdatedAt = DateTime.UtcNow;

        await _bidderRepository.UpdateAsync(bidder);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Completed bidder verification {BidderId} with status {Status}", dto.BidderId, bidder.Status);

        return MapBidderToDto(bidder);
    }

    public async Task<TenderAwardVerificationDto> CompleteVerificationAsync(Guid verificationId, CompleteVerificationDto dto)
    {
        var verification = await _verificationRepository.GetByIdWithDetailsAsync(verificationId);
        if (verification == null)
            throw new KeyNotFoundException($"Verification with ID {verificationId} not found.");

        // Check if all bidders are verified
        var pendingBidders = verification.Bidders.Where(b => !b.IsDeleted && b.Status == "Pending").ToList();
        if (pendingBidders.Any())
            throw new InvalidOperationException($"Cannot complete verification. {pendingBidders.Count} bidders are still pending.");

        verification.Status = "Completed";
        verification.CompletedDate = DateTime.UtcNow;
        verification.CompletedById = _currentUserProvider.UserId;
        verification.Notes = dto.Notes ?? verification.Notes;
        verification.LastModifiedById = _currentUserProvider.UserId;
        verification.UpdatedAt = DateTime.UtcNow;

        await _verificationRepository.UpdateAsync(verification);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Completed verification {VerificationId}", verificationId);

        return await GetVerificationByIdAsync(verificationId) ?? throw new InvalidOperationException("Failed to retrieve completed verification");
    }

    public async Task CancelVerificationAsync(Guid verificationId, string? reason = null)
    {
        var verification = await _verificationRepository.GetByIdAsync(verificationId);
        if (verification == null)
            throw new KeyNotFoundException($"Verification with ID {verificationId} not found.");

        verification.Status = "Cancelled";
        verification.Notes = reason ?? verification.Notes;
        verification.LastModifiedById = _currentUserProvider.UserId;
        verification.UpdatedAt = DateTime.UtcNow;

        await _verificationRepository.UpdateAsync(verification);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Cancelled verification {VerificationId}", verificationId);
    }

    #endregion

    #region Document Management

    public async Task<TenderAwardVerificationItemDocumentDto> UploadDocumentAsync(Guid itemResultId, UploadVerificationDocumentDto dto)
    {
        var itemResult = await _resultRepository.GetByIdAsync(itemResultId);
        if (itemResult == null)
            throw new KeyNotFoundException($"Verification item result with ID {itemResultId} not found.");

        var document = new TenderAwardVerificationItemDocument
        {
            ItemResultId = itemResultId,
            FileName = dto.FileName,
            FilePath = dto.FilePath,
            FileSize = dto.FileSize,
            ContentType = dto.ContentType,
            DocumentType = dto.DocumentType,
            Description = dto.Description,
            UploadedDate = DateTime.UtcNow,
            UploadedById = _currentUserProvider.UserId,
            TenantId = _currentUserProvider.TenantId,
            CreatedById = _currentUserProvider.UserId,
            CreatedAt = DateTime.UtcNow
        };

        await _documentRepository.CreateAsync(document);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Uploaded document {DocumentId} for item result {ItemResultId}", document.Id, itemResultId);

        return MapDocumentToDto(document);
    }

    public async Task<IEnumerable<TenderAwardVerificationItemDocumentDto>> GetDocumentsByItemResultIdAsync(Guid itemResultId)
    {
        var documents = await _documentRepository.GetByItemResultIdAsync(itemResultId);
        return documents.Select(MapDocumentToDto);
    }

    public async Task<TenderAwardVerificationItemDocumentDto?> GetDocumentByIdAsync(Guid documentId)
    {
        var document = await _documentRepository.GetByIdAsync(documentId);
        return document == null ? null : MapDocumentToDto(document);
    }

    public async Task DeleteDocumentAsync(Guid documentId)
    {
        var document = await _documentRepository.GetByIdAsync(documentId);
        if (document == null)
            throw new KeyNotFoundException($"Document with ID {documentId} not found.");

        await _documentRepository.DeleteAsync(documentId);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Deleted document {DocumentId}", documentId);
    }

    #endregion

    #region Mapping Methods

    private static AwardVerificationChecklistTemplateDto MapTemplateToDto(AwardVerificationChecklistTemplate template)
    {
        return new AwardVerificationChecklistTemplateDto
        {
            Id = template.Id,
            Name = template.Name,
            Description = template.Description,
            Category = template.Category,
            MinContractValue = template.MinContractValue,
            MaxContractValue = template.MaxContractValue,
            IsActive = template.IsActive,
            IsDefault = template.IsDefault,
            DisplayOrder = template.DisplayOrder,
            ItemCount = template.Items?.Count(i => !i.IsDeleted) ?? 0,
            CreatedAt = template.CreatedAt,
            Items = template.Items?.Where(i => !i.IsDeleted).Select(MapItemToDto).ToList() ?? new List<AwardVerificationChecklistItemDto>()
        };
    }

    private static AwardVerificationChecklistItemDto MapItemToDto(AwardVerificationChecklistItem item)
    {
        return new AwardVerificationChecklistItemDto
        {
            Id = item.Id,
            TemplateId = item.TemplateId,
            ItemText = item.ItemText,
            Description = item.Description,
            DisplayOrder = item.DisplayOrder,
            IsRequired = item.IsRequired,
            Category = item.Category,
            IsActive = item.IsActive
        };
    }

    private static TenderAwardVerificationDto MapVerificationToDto(TenderAwardVerification verification)
    {
        return new TenderAwardVerificationDto
        {
            Id = verification.Id,
            TenderId = verification.TenderId,
            TenderNumber = verification.Tender?.TenderNumber ?? string.Empty,
            TenderTitle = verification.Tender?.Title ?? string.Empty,
            TemplateId = verification.TemplateId,
            TemplateName = verification.Template?.Name,
            Status = verification.Status,
            StartedDate = verification.StartedDate,
            CompletedDate = verification.CompletedDate,
            StartedByName = verification.StartedBy?.FullName,
            CompletedByName = verification.CompletedBy?.FullName,
            Notes = verification.Notes,
            CreatedAt = verification.CreatedAt,
            Bidders = verification.Bidders?.Where(b => !b.IsDeleted).Select(MapBidderToDto).ToList() ?? new List<TenderAwardVerificationBidderDto>()
        };
    }

    private static TenderAwardVerificationBidderDto MapBidderToDto(TenderAwardVerificationBidder bidder)
    {
        var itemResults = bidder.ItemResults?.Where(r => !r.IsDeleted).ToList() ?? new List<TenderAwardVerificationItemResult>();
        return new TenderAwardVerificationBidderDto
        {
            Id = bidder.Id,
            VerificationId = bidder.VerificationId,
            TenderBidId = bidder.TenderBidId,
            BidNumber = bidder.TenderBid?.BidNumber ?? string.Empty,
            BusinessPartnerId = bidder.BusinessPartnerId,
            BusinessPartnerName = bidder.BusinessPartner?.PartnerName ?? string.Empty,
            BidAmount = bidder.TenderBid?.TotalBidAmount ?? 0,
            Status = bidder.Status,
            VerifiedDate = bidder.VerifiedDate,
            VerifiedByName = bidder.VerifiedBy?.FullName,
            OverallComments = bidder.OverallComments,
            TotalItems = itemResults.Count,
            VerifiedItems = itemResults.Count(r => r.IsVerified),
            PassedItems = itemResults.Count(r => r.Status == "Passed"),
            FailedItems = itemResults.Count(r => r.Status == "Failed"),
            ItemResults = itemResults.Select(MapItemResultToDto).ToList()
        };
    }

    private static TenderAwardVerificationItemResultDto MapItemResultToDto(TenderAwardVerificationItemResult result)
    {
        return new TenderAwardVerificationItemResultDto
        {
            Id = result.Id,
            BidderId = result.BidderId,
            ChecklistItemId = result.ChecklistItemId,
            ItemText = result.ChecklistItem?.ItemText ?? string.Empty,
            ItemDescription = result.ChecklistItem?.Description,
            ItemCategory = result.ChecklistItem?.Category,
            IsRequired = result.ChecklistItem?.IsRequired ?? false,
            IsVerified = result.IsVerified,
            Status = result.Status,
            Comments = result.Comments,
            VerifiedDate = result.VerifiedDate,
            VerifiedByName = result.VerifiedBy?.FullName,
            Documents = result.Documents?.Where(d => !d.IsDeleted).Select(MapDocumentToDto).ToList() ?? new List<TenderAwardVerificationItemDocumentDto>()
        };
    }

    private static TenderAwardVerificationItemDocumentDto MapDocumentToDto(TenderAwardVerificationItemDocument document)
    {
        return new TenderAwardVerificationItemDocumentDto
        {
            Id = document.Id,
            ItemResultId = document.ItemResultId,
            FileName = document.FileName,
            FilePath = document.FilePath,
            FileSize = document.FileSize,
            ContentType = document.ContentType,
            DocumentType = document.DocumentType,
            Description = document.Description,
            UploadedDate = document.UploadedDate,
            UploadedByName = document.UploadedBy?.FullName
        };
    }

    #endregion
}
