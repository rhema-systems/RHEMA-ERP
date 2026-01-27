using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class TenderService : ITenderService
{
    private readonly ITenderRepository _tenderRepository;
    private readonly ITenderItemRepository _itemRepository;
    private readonly ITenderDocumentRepository _documentRepository;
    private readonly ITenderInvitationRepository _invitationRepository;
    private readonly ITenderFeeRepository _feeRepository;
    private readonly ITenderEvaluatorRepository _evaluatorRepository;
    private readonly ITenderClarificationRepository _clarificationRepository;
    private readonly ITenderRevisionRepository _revisionRepository;
    private readonly ITenderViewLogRepository _viewLogRepository;
    private readonly ITenderLotRepository _lotRepository;
    private readonly IBusinessPartnerRepository _businessPartnerRepository;
    private readonly ITenderNotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<TenderService> _logger;

    public TenderService(
        ITenderRepository tenderRepository,
        ITenderItemRepository itemRepository,
        ITenderDocumentRepository documentRepository,
        ITenderInvitationRepository invitationRepository,
        ITenderFeeRepository feeRepository,
        ITenderEvaluatorRepository evaluatorRepository,
        ITenderClarificationRepository clarificationRepository,
        ITenderRevisionRepository revisionRepository,
        ITenderViewLogRepository viewLogRepository,
        ITenderLotRepository lotRepository,
        IBusinessPartnerRepository businessPartnerRepository,
        ITenderNotificationService notificationService,
        IUnitOfWork unitOfWork,
        UserManager<ApplicationUser> userManager,
        ICurrentUserProvider currentUserProvider,
        ILogger<TenderService> logger)
    {
        _tenderRepository = tenderRepository;
        _itemRepository = itemRepository;
        _documentRepository = documentRepository;
        _invitationRepository = invitationRepository;
        _feeRepository = feeRepository;
        _evaluatorRepository = evaluatorRepository;
        _clarificationRepository = clarificationRepository;
        _revisionRepository = revisionRepository;
        _viewLogRepository = viewLogRepository;
        _lotRepository = lotRepository;
        _businessPartnerRepository = businessPartnerRepository;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<TenderDetailDto?> GetTenderByIdAsync(Guid id)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(id);
            if (tender == null) return null;

            var items = await _itemRepository.GetByTenderIdAsync(id);
            var lots = await _lotRepository.GetByTenderIdAsync(id);
            var documents = await _documentRepository.GetByTenderIdAsync(id);
            var fees = await _feeRepository.GetByTenderIdAsync(id);
            var invitations = await _invitationRepository.GetByTenderIdAsync(id);
            var clarifications = await _clarificationRepository.GetByTenderIdAsync(id);
            var evaluators = await GetTenderEvaluatorsAsync(id);
            var bidRepository = _unitOfWork.Repository<TenderBid>();
            var bids = await bidRepository.FindAsync(b => b.TenderId == id && !b.IsDeleted);

            return MapToDetailDto(tender, lots, items, documents, fees, invitations, clarifications, evaluators, bids);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tender {TenderId}", id);
            throw;
        }
    }

    public async Task<TenderDetailDto?> GetTenderByNumberAsync(string tenderNumber)
    {
        try
        {
            var tender = await _tenderRepository.GetByTenderNumberAsync(tenderNumber);
            if (tender == null) return null;

            var items = await _itemRepository.GetByTenderIdAsync(tender.Id);
            var lots = await _lotRepository.GetByTenderIdAsync(tender.Id);
            var documents = await _documentRepository.GetByTenderIdAsync(tender.Id);
            var fees = await _feeRepository.GetByTenderIdAsync(tender.Id);
            var invitations = await _invitationRepository.GetByTenderIdAsync(tender.Id);
            var clarifications = await _clarificationRepository.GetByTenderIdAsync(tender.Id);
            var evaluators = await GetTenderEvaluatorsAsync(tender.Id);
            var bidRepository = _unitOfWork.Repository<TenderBid>();
            var bids = await bidRepository.FindAsync(b => b.TenderId == tender.Id && !b.IsDeleted);

            return MapToDetailDto(tender, lots, items, documents, fees, invitations, clarifications, evaluators, bids);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tender by number {TenderNumber}", tenderNumber);
            throw;
        }
    }

    public async Task<PagedResult<TenderDto>> GetTendersAsync(int page, int pageSize, string? search = null, string? status = null, string? tenderType = null)
    {
        try
        {
            var result = await _tenderRepository.GetTendersAsync(page, pageSize, search, status, tenderType);

            return new PagedResult<TenderDto>
            {
                Items = result.Items.Select(MapToDto),
                TotalCount = result.TotalCount,
                Page = result.Page,
                PageSize = result.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tenders");
            throw;
        }
    }

    public async Task<IEnumerable<TenderDto>> GetPublishedTendersAsync()
    {
        try
        {
            var tenders = await _tenderRepository.GetPublishedTendersAsync();
            return tenders.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving published tenders");
            throw;
        }
    }

    public async Task<IEnumerable<TenderDto>> GetActiveTendersAsync()
    {
        try
        {
            var tenders = await _tenderRepository.GetActiveTendersAsync();
            return tenders.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active tenders");
            throw;
        }
    }

    public async Task<TenderDetailDto> CreateTenderAsync(CreateTenderDto dto)
    {
        try
        {
            // Generate tender number
            var tenderNumber = await _tenderRepository.GenerateTenderNumberAsync();

            var tender = new Tender
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderNumber = tenderNumber,
                Title = dto.Title,
                Description = dto.Description,
                TenderType = dto.TenderType,
                Status = "Draft",
                SubmissionDeadline = dto.SubmissionDeadline,
                OpeningDate = dto.OpeningDate,
                EstimatedValue = dto.EstimatedValue,
                Currency = dto.Currency,
                MinimumPerformanceRating = dto.MinimumPerformanceRating,
                RequiresPrequalification = dto.RequiresPrequalification,
                AllowPartialBids = dto.AllowPartialBids,
                PriceWeightage = dto.PriceWeightage,
                QualityWeightage = dto.QualityWeightage,
                DeliveryWeightage = dto.DeliveryWeightage,
                ExperienceWeightage = dto.ExperienceWeightage,
                EvaluationCriteriaJson = dto.EvaluationCriteriaJson,
                // QCBS Configuration
                UseQCBSEvaluation = dto.UseQCBSEvaluation,
                MinimumTechnicalScore = dto.MinimumTechnicalScore,
                TechnicalWeight = dto.TechnicalWeight,
                FinancialWeight = dto.FinancialWeight,
                TermsAndConditions = dto.TermsAndConditions,
                RequiredDocuments = dto.RequiredDocuments,
                RequiresAcceptanceDeclaration = dto.RequiresAcceptanceDeclaration,
                EvaluationTemplateId = dto.EvaluationTemplateId,
                Notes = dto.Notes,
                CreatedById = _currentUserProvider.UserId,
                CreatedAt = DateTime.UtcNow
            };

            await _tenderRepository.CreateAsync(tender);

            // Add tender items if provided
            var items = new List<TenderItem>();
            if (dto.Items != null && dto.Items.Any())
            {
                foreach (var itemDto in dto.Items)
                {
                    var item = new TenderItem
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _currentUserProvider.TenantId,
                        TenderId = tender.Id,
                        LineNumber = itemDto.LineNumber,
                        ItemCode = itemDto.ItemCode,
                        Description = itemDto.Description,
                        Quantity = itemDto.Quantity,
                        UnitOfMeasure = itemDto.UnitOfMeasure,
                        Specifications = itemDto.Specifications,
                        RequiredDeliveryDate = itemDto.RequiredDeliveryDate,
                        DeliveryLocation = itemDto.DeliveryLocation,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _itemRepository.CreateAsync(item);
                    items.Add(item);
                }
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created tender {TenderId}", tender.Id);

            return MapToDetailDto(tender, new List<TenderLot>(), items, new List<TenderDocument>(), new List<TenderFee>(), new List<TenderInvitation>(), new List<TenderClarification>(), new List<TenderEvaluatorDto>(), new List<TenderBid>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tender");
            throw;
        }
    }

    public async Task<TenderDetailDto> UpdateTenderAsync(Guid id, UpdateTenderDto dto)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Tender with ID {id} not found");

            if (tender.Status != "Draft")
            {
                throw new InvalidOperationException("Only draft tenders can be updated");
            }

            tender.Title = dto.Title;
            tender.Description = dto.Description;
            tender.SubmissionDeadline = dto.SubmissionDeadline;
            tender.OpeningDate = dto.OpeningDate;
            tender.EstimatedValue = dto.EstimatedValue;
            tender.Currency = dto.Currency;
            tender.MinimumPerformanceRating = dto.MinimumPerformanceRating;
            tender.RequiresPrequalification = dto.RequiresPrequalification;
            tender.AllowPartialBids = dto.AllowPartialBids;
            tender.PriceWeightage = dto.PriceWeightage;
            tender.QualityWeightage = dto.QualityWeightage;
            tender.DeliveryWeightage = dto.DeliveryWeightage;
            tender.ExperienceWeightage = dto.ExperienceWeightage;
            tender.EvaluationCriteriaJson = dto.EvaluationCriteriaJson;
            // QCBS Configuration
            tender.UseQCBSEvaluation = dto.UseQCBSEvaluation;
            tender.MinimumTechnicalScore = dto.MinimumTechnicalScore;
            tender.TechnicalWeight = dto.TechnicalWeight;
            tender.FinancialWeight = dto.FinancialWeight;
            tender.TermsAndConditions = dto.TermsAndConditions;
            tender.RequiredDocuments = dto.RequiredDocuments;
            tender.RequiresAcceptanceDeclaration = dto.RequiresAcceptanceDeclaration;
            tender.EvaluationTemplateId = dto.EvaluationTemplateId;
            tender.Notes = dto.Notes;
            tender.UpdatedAt = DateTime.UtcNow;

            await _tenderRepository.UpdateAsync(tender);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated tender {TenderId}", id);

            var items = await _itemRepository.GetByTenderIdAsync(id);
            var lots = await _lotRepository.GetByTenderIdAsync(id);
            var documents = await _documentRepository.GetByTenderIdAsync(id);
            var fees = await _feeRepository.GetByTenderIdAsync(id);
            var invitations = await _invitationRepository.GetByTenderIdAsync(id);
            var clarifications = await _clarificationRepository.GetByTenderIdAsync(id);
            var evaluators = await GetTenderEvaluatorsAsync(id);
            var bidRepository = _unitOfWork.Repository<TenderBid>();
            var bids = await bidRepository.FindAsync(b => b.TenderId == id && !b.IsDeleted);

            return MapToDetailDto(tender, lots, items, documents, fees, invitations, clarifications, evaluators, bids);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tender {TenderId}", id);
            throw;
        }
    }

    public async Task<TenderDetailDto> PublishTenderAsync(Guid id, PublishTenderDto dto)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Tender with ID {id} not found");

            if (tender.Status != "Draft")
            {
                throw new InvalidOperationException("Only draft tenders can be published");
            }

            tender.Status = "Published";
            tender.PublishDate = DateTime.UtcNow; // Always publish immediately
            tender.SubmissionDeadline = dto.SubmissionDeadline;
            tender.OpeningDate = dto.OpeningDate;
            tender.PublishedById = _currentUserProvider.UserId;
            tender.UpdatedAt = DateTime.UtcNow;

            await _tenderRepository.UpdateAsync(tender);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Published tender {TenderId}", id);

            // Send notifications to invited business partners
            if (dto.InvitedBusinessPartnerIds != null && dto.InvitedBusinessPartnerIds.Any())
            {
                await _notificationService.SendTenderPublishedNotificationAsync(id, dto.InvitedBusinessPartnerIds);
            }

            var items = await _itemRepository.GetByTenderIdAsync(id);
            var lots = await _lotRepository.GetByTenderIdAsync(id);
            var documents = await _documentRepository.GetByTenderIdAsync(id);
            var fees = await _feeRepository.GetByTenderIdAsync(id);
            var invitations = await _invitationRepository.GetByTenderIdAsync(id);
            var clarifications = await _clarificationRepository.GetByTenderIdAsync(id);
            var evaluators = await GetTenderEvaluatorsAsync(id);
            var bidRepository = _unitOfWork.Repository<TenderBid>();
            var bids = await bidRepository.FindAsync(b => b.TenderId == id && !b.IsDeleted);

            return MapToDetailDto(tender, lots, items, documents, fees, invitations, clarifications, evaluators, bids);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing tender {TenderId}", id);
            throw;
        }
    }

    public async Task DeleteTenderAsync(Guid id)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Tender with ID {id} not found");

            if (tender.Status != "Draft")
            {
                throw new InvalidOperationException("Only draft tenders can be deleted");
            }

            await _tenderRepository.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted tender {TenderId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tender {TenderId}", id);
            throw;
        }
    }

    // Tender Items
    public async Task<TenderItemDto> AddTenderItemAsync(Guid tenderId, CreateTenderItemDto dto)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");

            var item = new TenderItem
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderId = tenderId,
                LotId = dto.LotId,
                LineNumber = dto.LineNumber,
                ItemCode = dto.ItemCode,
                Description = dto.Description,
                Quantity = dto.Quantity,
                UnitOfMeasure = dto.UnitOfMeasure,
                Specifications = dto.Specifications,
                RequiredDeliveryDate = dto.RequiredDeliveryDate,
                DeliveryLocation = dto.DeliveryLocation,
                CreatedAt = DateTime.UtcNow
            };

            await _itemRepository.CreateAsync(item);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Added item {ItemId} to tender {TenderId}", item.Id, tenderId);

            return MapItemToDto(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item to tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<TenderItemDto> UpdateTenderItemAsync(Guid itemId, CreateTenderItemDto dto)
    {
        try
        {
            var item = await _itemRepository.GetByIdAsync(itemId)
                ?? throw new InvalidOperationException($"Item with ID {itemId} not found");

            item.LineNumber = dto.LineNumber;
            item.ItemCode = dto.ItemCode;
            item.Description = dto.Description;
            item.Quantity = dto.Quantity;
            item.UnitOfMeasure = dto.UnitOfMeasure;
            item.Specifications = dto.Specifications;
            item.RequiredDeliveryDate = dto.RequiredDeliveryDate;
            item.DeliveryLocation = dto.DeliveryLocation;
            item.UpdatedAt = DateTime.UtcNow;

            await _itemRepository.UpdateAsync(item);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated tender item {ItemId}", itemId);

            return MapItemToDto(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tender item {ItemId}", itemId);
            throw;
        }
    }

    public async Task DeleteTenderItemAsync(Guid itemId)
    {
        try
        {
            await _itemRepository.DeleteAsync(itemId);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted tender item {ItemId}", itemId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tender item {ItemId}", itemId);
            throw;
        }
    }

    // Tender Documents
    public async Task<TenderDocumentDto> UploadTenderDocumentAsync(Guid tenderId, UploadTenderDocumentDto dto, string filePath, string? fileType, long? fileSize)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");

            var document = new TenderDocument
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderId = tenderId,
                DocumentName = dto.DocumentName,
                DocumentType = dto.DocumentType,
                FilePath = filePath,
                FileType = fileType,
                FileSize = fileSize,
                IsPublic = dto.IsPublic,
                UploadedDate = DateTime.UtcNow,
                UploadedById = _currentUserProvider.UserId,
                CreatedAt = DateTime.UtcNow
            };

            await _documentRepository.CreateAsync(document);

            // If this is an acceptance declaration, update the tender entity
            if (dto.DocumentType == "AcceptanceDeclaration")
            {
                tender.AcceptanceDeclarationDocumentPath = filePath;
                tender.AcceptanceDeclarationDocumentName = dto.DocumentName;
                tender.UpdatedAt = DateTime.UtcNow;
                await _tenderRepository.UpdateAsync(tender);
                _logger.LogInformation("Updated tender {TenderId} with acceptance declaration document", tenderId);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Uploaded document {DocumentId} to tender {TenderId}", document.Id, tenderId);

            return MapDocumentToDto(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document to tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task DeleteTenderDocumentAsync(Guid documentId)
    {
        try
        {
            await _documentRepository.DeleteAsync(documentId);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted tender document {DocumentId}", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tender document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderDocumentDto>> GetTenderDocumentsAsync(Guid tenderId, bool includeInternal = false)
    {
        try
        {
            var documents = await _documentRepository.GetByTenderIdAsync(tenderId);

            if (!includeInternal)
            {
                documents = documents.Where(d => d.IsPublic).ToList();
            }

            return documents.Select(MapDocumentToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving documents for tender {TenderId}", tenderId);
            throw;
        }
    }

    // Tender Invitations
    public async Task InviteTenderersAsync(Guid tenderId, InviteTenderersDto dto)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");

            foreach (var businessPartnerId in dto.BusinessPartnerIds)
            {
                var invitation = new TenderInvitation
                {
                    Id = Guid.NewGuid(),
                    TenantId = _currentUserProvider.TenantId,
                    TenderId = tenderId,
                    BusinessPartnerId = businessPartnerId,
                    InvitedDate = DateTime.UtcNow,
                    InvitedById = _currentUserProvider.UserId,
                    Status = "Invited",
                    CreatedAt = DateTime.UtcNow
                };

                await _invitationRepository.CreateAsync(invitation);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Invited {Count} business partners to tender {TenderId}", dto.BusinessPartnerIds.Count, tenderId);

            // Send notifications
            if (dto.SendNotifications)
            {
                await _notificationService.SendTenderPublishedNotificationAsync(tenderId, dto.BusinessPartnerIds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inviting tenderers to tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderInvitationDto>> GetTenderInvitationsAsync(Guid tenderId)
    {
        try
        {
            var invitations = await _invitationRepository.GetByTenderIdAsync(tenderId);
            return invitations.Select(i => new TenderInvitationDto
            {
                Id = i.Id,
                TenderId = i.TenderId,
                BusinessPartnerId = i.BusinessPartnerId,
                BusinessPartnerName = string.Empty, // Would need to fetch from BusinessPartner entity
                InvitedDate = i.InvitedDate,
                InvitedByName = string.Empty, // Would need to fetch from User entity
                Status = i.Status,
                ViewedDate = i.ViewedDate,
                ResponseDate = i.ResponseDate,
                DeclineReason = i.DeclineReason
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving invitations for tender {TenderId}", tenderId);
            throw;
        }
    }

    // Tender Fees
    public async Task<TenderFeeDto> AddTenderFeeAsync(Guid tenderId, CreateTenderFeeDto dto)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");

            var fee = new TenderFee
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderId = tenderId,
                FeeType = dto.FeeType,
                Amount = dto.Amount,
                Currency = dto.Currency,
                PaymentMethod = dto.PaymentMethod,
                IsMandatory = dto.IsMandatory,
                DueDate = dto.DueDate,
                Description = dto.Description,
                BankAccountDetails = dto.BankAccountDetails,
                CreatedAt = DateTime.UtcNow
            };

            await _feeRepository.CreateAsync(fee);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Added fee {FeeId} to tender {TenderId}", fee.Id, tenderId);

            return MapFeeToDto(fee);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding fee to tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<TenderFeeDto> UpdateTenderFeeAsync(Guid feeId, CreateTenderFeeDto dto)
    {
        try
        {
            var fee = await _feeRepository.GetByIdAsync(feeId)
                ?? throw new InvalidOperationException($"Fee with ID {feeId} not found");

            fee.FeeType = dto.FeeType;
            fee.Amount = dto.Amount;
            fee.Currency = dto.Currency;
            fee.PaymentMethod = dto.PaymentMethod;
            fee.IsMandatory = dto.IsMandatory;
            fee.DueDate = dto.DueDate;
            fee.Description = dto.Description;
            fee.BankAccountDetails = dto.BankAccountDetails;
            fee.UpdatedAt = DateTime.UtcNow;

            await _feeRepository.UpdateAsync(fee);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated tender fee {FeeId}", feeId);

            return MapFeeToDto(fee);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tender fee {FeeId}", feeId);
            throw;
        }
    }

    public async Task DeleteTenderFeeAsync(Guid feeId)
    {
        try
        {
            await _feeRepository.DeleteAsync(feeId);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted tender fee {FeeId}", feeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tender fee {FeeId}", feeId);
            throw;
        }
    }

    // Tender Evaluators
    public async Task AssignEvaluatorsAsync(Guid tenderId, AssignEvaluatorsDto dto)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");

            var evaluatorIds = new List<Guid>();

            foreach (var evaluatorDto in dto.Evaluators)
            {
                var evaluator = new TenderEvaluator
                {
                    Id = Guid.NewGuid(),
                    TenantId = _currentUserProvider.TenantId,
                    TenderId = tenderId,
                    UserId = evaluatorDto.UserId,
                    Role = evaluatorDto.Role,
                    WeightagePercentage = evaluatorDto.WeightagePercentage,
                    AssignedDate = DateTime.UtcNow,
                    AssignedById = _currentUserProvider.UserId,
                    Status = "Assigned",
                    CreatedAt = DateTime.UtcNow
                };

                await _evaluatorRepository.CreateAsync(evaluator);
                evaluatorIds.Add(evaluatorDto.UserId);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Assigned {Count} evaluators to tender {TenderId}", dto.Evaluators.Count, tenderId);

            // Send notifications
            await _notificationService.SendEvaluationAssignedNotificationAsync(tenderId, evaluatorIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning evaluators to tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderEvaluatorDto>> GetTenderEvaluatorsAsync(Guid tenderId)
    {
        try
        {
            var evaluators = await _evaluatorRepository.GetByTenderIdAsync(tenderId);
            var result = new List<TenderEvaluatorDto>();
            var evaluationRepository = _unitOfWork.Repository<TenderEvaluation>();

            foreach (var evaluator in evaluators)
            {
                var user = await _userManager.FindByIdAsync(evaluator.UserId.ToString());
                var assignedByUser = evaluator.AssignedById.HasValue ? await _userManager.FindByIdAsync(evaluator.AssignedById.Value.ToString()) : null;
                var evaluationCount = await evaluationRepository.FindAsync(e => e.TenderEvaluatorId == evaluator.Id && !e.IsDeleted);

                result.Add(new TenderEvaluatorDto
                {
                    Id = evaluator.Id,
                    TenderId = evaluator.TenderId,
                    UserId = evaluator.UserId,
                    UserName = user?.FullName ?? string.Empty,
                    Role = evaluator.Role,
                    AssignedDate = evaluator.AssignedDate,
                    AssignedByName = assignedByUser?.FullName ?? string.Empty,
                    Status = evaluator.Status,
                    AcceptedDate = evaluator.AcceptedDate,
                    CompletedDate = evaluator.CompletedDate,
                    WeightagePercentage = evaluator.WeightagePercentage,
                    EvaluationCount = evaluationCount.Count()
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving evaluators for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task RemoveEvaluatorAsync(Guid evaluatorId)
    {
        try
        {
            await _evaluatorRepository.DeleteAsync(evaluatorId);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Removed evaluator {EvaluatorId}", evaluatorId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing evaluator {EvaluatorId}", evaluatorId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderDto>> GetMyAssignedTendersAsync()
    {
        try
        {
            // Get all TenderEvaluator records for the current user
            var evaluators = await _evaluatorRepository.GetByUserIdAsync(_currentUserProvider.UserId);

            if (!evaluators.Any())
            {
                return new List<TenderDto>();
            }

            // Get unique tenders from evaluator assignments
            var tenderIds = evaluators.Select(e => e.TenderId).Distinct().ToList();

            // Fetch tender details for each tender
            var tenders = new List<Tender>();
            foreach (var tenderId in tenderIds)
            {
                var tender = await _tenderRepository.GetByIdAsync(tenderId);
                if (tender != null)
                {
                    tenders.Add(tender);
                }
            }

            // Map to DTOs
            return tenders.Select(MapToDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assigned tenders for user {UserId}", _currentUserProvider.UserId);
            throw;
        }
    }

    // Tender Clarifications
    public async Task<TenderClarificationDto> CreateClarificationAsync(Guid tenderId, CreateClarificationDto dto)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");

            // Get the business partner for the current user
            var businessPartner = await _businessPartnerRepository.GetByUserIdAsync(_currentUserProvider.UserId)
                ?? throw new InvalidOperationException("No business partner found for the current user. Please complete your business partner registration first.");

            var clarification = new TenderClarification
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderId = tenderId,
                BusinessPartnerId = businessPartner.Id,
                Question = dto.Question,
                QuestionDate = DateTime.UtcNow,
                QuestionById = _currentUserProvider.UserId,
                Status = "Pending",
                IsPublic = dto.IsPublic,
                Category = dto.Category,
                CreatedAt = DateTime.UtcNow
            };

            await _clarificationRepository.CreateAsync(clarification);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created clarification {ClarificationId} for tender {TenderId}", clarification.Id, tenderId);

            // Send notification
            await _notificationService.SendClarificationNotificationAsync(clarification.Id, false);

            return MapClarificationToDto(clarification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating clarification for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<TenderClarificationDto> AnswerClarificationAsync(Guid clarificationId, AnswerClarificationDto dto)
    {
        try
        {
            var clarification = await _clarificationRepository.GetByIdAsync(clarificationId)
                ?? throw new InvalidOperationException($"Clarification with ID {clarificationId} not found");

            clarification.Answer = dto.Answer;
            clarification.AnswerDate = DateTime.UtcNow;
            clarification.AnsweredById = _currentUserProvider.UserId;
            clarification.Status = "Answered";
            clarification.IsPublic = dto.IsPublic;
            clarification.UpdatedAt = DateTime.UtcNow;

            await _clarificationRepository.UpdateAsync(clarification);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Answered clarification {ClarificationId}", clarificationId);

            // Send notification
            await _notificationService.SendClarificationNotificationAsync(clarificationId, true);

            return MapClarificationToDto(clarification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error answering clarification {ClarificationId}", clarificationId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderClarificationDto>> GetTenderClarificationsAsync(Guid tenderId, bool publicOnly = false)
    {
        try
        {
            var clarifications = await _clarificationRepository.GetByTenderIdAsync(tenderId);

            if (publicOnly)
            {
                clarifications = clarifications.Where(c => c.IsPublic).ToList();
            }

            return clarifications.Select(MapClarificationToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving clarifications for tender {TenderId}", tenderId);
            throw;
        }
    }

    // Tender Revisions
    public async Task<TenderRevisionDto> CreateRevisionAsync(Guid tenderId, CreateRevisionDto dto)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId)
                ?? throw new InvalidOperationException($"Tender with ID {tenderId} not found");

            // Get current revision number
            var revisions = await _revisionRepository.GetByTenderIdAsync(tenderId);
            var revisionCount = revisions.Count() + 1;
            var revisionNumber = $"REV-{revisionCount:D3}";

            var revision = new TenderRevision
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderId = tenderId,
                RevisionNumber = revisionNumber,
                RevisionDate = DateTime.UtcNow,
                RevisedById = _currentUserProvider.UserId,
                RevisionType = dto.RevisionType,
                Description = dto.Description,
                Changes = dto.Changes,
                NewSubmissionDeadline = dto.NewSubmissionDeadline,
                RequiresRebid = dto.RequiresRebid,
                NotificationSent = false,
                CreatedAt = DateTime.UtcNow
            };

            await _revisionRepository.CreateAsync(revision);

            // Update tender if submission deadline changed
            if (dto.NewSubmissionDeadline.HasValue)
            {
                tender.SubmissionDeadline = dto.NewSubmissionDeadline.Value;
                await _tenderRepository.UpdateAsync(tender);
            }

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created revision {RevisionId} for tender {TenderId}", revision.Id, tenderId);

            // Send notifications
            if (dto.SendNotifications)
            {
                await _notificationService.SendRevisionNotificationAsync(revision.Id);
                revision.NotificationSent = true;
                revision.NotificationSentDate = DateTime.UtcNow;
                await _revisionRepository.UpdateAsync(revision);
                await _unitOfWork.SaveChangesAsync();
            }

            return MapRevisionToDto(revision);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating revision for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderRevisionDto>> GetTenderRevisionsAsync(Guid tenderId)
    {
        try
        {
            var revisions = await _revisionRepository.GetByTenderIdAsync(tenderId);
            return revisions.Select(MapRevisionToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving revisions for tender {TenderId}", tenderId);
            throw;
        }
    }

    // Tender Statistics
    public async Task<int> GetTotalViewsAsync(Guid tenderId)
    {
        try
        {
            var viewLogs = await _viewLogRepository.GetByTenderIdAsync(tenderId);
            return viewLogs.Count(v => v.ActionType == "View");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving total views for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<int> GetTotalDownloadsAsync(Guid tenderId)
    {
        try
        {
            var viewLogs = await _viewLogRepository.GetByTenderIdAsync(tenderId);
            return viewLogs.Count(v => v.ActionType == "Download");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving total downloads for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task LogTenderViewAsync(Guid tenderId, Guid? businessPartnerId, string actionType)
    {
        try
        {
            var viewLog = new TenderViewLog
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderId = tenderId,
                BusinessPartnerId = businessPartnerId,
                UserId = _currentUserProvider.UserId,
                ActionType = actionType,
                ActionDate = DateTime.UtcNow,
                IpAddress = string.Empty, // Would need to get from HTTP context
                CreatedAt = DateTime.UtcNow
            };

            await _viewLogRepository.CreateAsync(viewLog);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Logged {ActionType} for tender {TenderId}", actionType, tenderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging view for tender {TenderId}", tenderId);
            throw;
        }
    }

    // Mapping methods
    private static TenderDto MapToDto(Tender tender)
    {
        return new TenderDto
        {
            Id = tender.Id,
            TenderNumber = tender.TenderNumber,
            Title = tender.Title,
            TenderType = tender.TenderType,
            Status = tender.Status,
            PublishDate = tender.PublishDate,
            SubmissionDeadline = tender.SubmissionDeadline,
            EstimatedValue = tender.EstimatedValue,
            Currency = tender.Currency,
            BidCount = tender.Bids?.Count(b => !b.IsDeleted && b.Status != "Draft") ?? 0,
            InvitationCount = tender.Invitations?.Count(i => !i.IsDeleted) ?? 0,
            CreatedAt = tender.CreatedAt,
            CreatedByName = tender.CreatedBy != null ? $"{tender.CreatedBy.FirstName} {tender.CreatedBy.LastName}".Trim() : string.Empty
        };
    }

    private static TenderDetailDto MapToDetailDto(Tender tender, IEnumerable<TenderLot> lots, IEnumerable<TenderItem> items, IEnumerable<TenderDocument> documents, IEnumerable<TenderFee> fees, IEnumerable<TenderInvitation> invitations, IEnumerable<TenderClarification> clarifications, IEnumerable<TenderEvaluatorDto> evaluators, IEnumerable<TenderBid> bids)
    {
        return new TenderDetailDto
        {
            Id = tender.Id,
            TenderNumber = tender.TenderNumber,
            Title = tender.Title,
            Description = tender.Description,
            TenderType = tender.TenderType,
            Status = tender.Status,
            PublishDate = tender.PublishDate,
            SubmissionDeadline = tender.SubmissionDeadline,
            OpeningDate = tender.OpeningDate,
            AwardDate = tender.AwardDate,
            EstimatedValue = tender.EstimatedValue,
            Currency = tender.Currency,
            MinimumPerformanceRating = tender.MinimumPerformanceRating,
            RequiresPrequalification = tender.RequiresPrequalification,
            AllowPartialBids = tender.AllowPartialBids,
            PriceWeightage = tender.PriceWeightage,
            QualityWeightage = tender.QualityWeightage,
            DeliveryWeightage = tender.DeliveryWeightage,
            ExperienceWeightage = tender.ExperienceWeightage,
            EvaluationCriteriaJson = tender.EvaluationCriteriaJson,
            // QCBS Configuration
            UseQCBSEvaluation = tender.UseQCBSEvaluation,
            MinimumTechnicalScore = tender.MinimumTechnicalScore,
            TechnicalWeight = tender.TechnicalWeight,
            FinancialWeight = tender.FinancialWeight,
            TermsAndConditions = tender.TermsAndConditions,
            RequiredDocuments = tender.RequiredDocuments,
            RequiresAcceptanceDeclaration = tender.RequiresAcceptanceDeclaration,
            AcceptanceDeclarationDocumentPath = tender.AcceptanceDeclarationDocumentPath,
            AcceptanceDeclarationDocumentName = tender.AcceptanceDeclarationDocumentName,
            EvaluationTemplateId = tender.EvaluationTemplateId,
            EvaluationTemplateName = tender.EvaluationTemplate?.TemplateName,
            Notes = tender.Notes,
            CreatedAt = tender.CreatedAt,
            Lots = lots.Select(MapToLotDto).ToList(),
            Items = items.Select(MapItemToDto).ToList(),
            Documents = documents.Select(MapDocumentToDto).ToList(),
            Fees = fees.Select(MapFeeToDto).ToList(),
            Invitations = invitations.Select(MapInvitationToDto).ToList(),
            Clarifications = clarifications.Select(MapClarificationToDto).ToList(),
            Evaluators = evaluators.ToList(),
            Bids = bids.Select(b => MapBidToSummaryDto(b, tender)).ToList()
        };
    }

    private static TenderItemDto MapItemToDto(TenderItem item)
    {
        return new TenderItemDto
        {
            Id = item.Id,
            TenderId = item.TenderId,
            LineNumber = item.LineNumber,
            ItemCode = item.ItemCode,
            Description = item.Description,
            Quantity = item.Quantity,
            UnitOfMeasure = item.UnitOfMeasure,
            Specifications = item.Specifications,
            RequiredDeliveryDate = item.RequiredDeliveryDate,
            DeliveryLocation = item.DeliveryLocation
        };
    }

    private static TenderDocumentDto MapDocumentToDto(TenderDocument document)
    {
        return new TenderDocumentDto
        {
            Id = document.Id,
            TenderId = document.TenderId,
            DocumentName = document.DocumentName,
            DocumentType = document.DocumentType,
            FilePath = document.FilePath,
            FileType = document.FileType,
            FileSize = document.FileSize,
            IsPublic = document.IsPublic,
            UploadedDate = document.UploadedDate,
            UploadedByName = string.Empty // Would need to fetch from User entity
        };
    }

    private static TenderFeeDto MapFeeToDto(TenderFee fee)
    {
        return new TenderFeeDto
        {
            Id = fee.Id,
            TenderId = fee.TenderId,
            FeeType = fee.FeeType,
            Amount = fee.Amount,
            Currency = fee.Currency,
            PaymentMethod = fee.PaymentMethod,
            IsMandatory = fee.IsMandatory,
            DueDate = fee.DueDate,
            Description = fee.Description,
            BankAccountDetails = fee.BankAccountDetails,
            PaymentCount = 0 // Would need to count from Payments collection
        };
    }

    private static TenderInvitationDto MapInvitationToDto(TenderInvitation invitation)
    {
        return new TenderInvitationDto
        {
            Id = invitation.Id,
            TenderId = invitation.TenderId,
            BusinessPartnerId = invitation.BusinessPartnerId,
            BusinessPartnerName = invitation.BusinessPartner?.PartnerName ?? string.Empty,
            InvitedDate = invitation.InvitedDate,
            InvitedByName = string.Empty, // Would need to fetch from User entity
            Status = invitation.Status,
            ViewedDate = invitation.ViewedDate,
            ResponseDate = invitation.ResponseDate,
            DeclineReason = invitation.DeclineReason
        };
    }

    private static TenderClarificationDto MapClarificationToDto(TenderClarification clarification)
    {
        return new TenderClarificationDto
        {
            Id = clarification.Id,
            TenderId = clarification.TenderId,
            BusinessPartnerId = clarification.BusinessPartnerId,
            BusinessPartnerName = clarification.BusinessPartner?.PartnerName ?? string.Empty,
            Question = clarification.Question,
            QuestionDate = clarification.QuestionDate,
            QuestionByName = clarification.QuestionBy != null
                ? $"{clarification.QuestionBy.FirstName} {clarification.QuestionBy.LastName}".Trim()
                : string.Empty,
            Answer = clarification.Answer,
            AnswerDate = clarification.AnswerDate,
            AnsweredByName = clarification.AnsweredBy != null
                ? $"{clarification.AnsweredBy.FirstName} {clarification.AnsweredBy.LastName}".Trim()
                : string.Empty,
            Status = clarification.Status,
            IsPublic = clarification.IsPublic,
            Category = clarification.Category
        };
    }

    private static TenderRevisionDto MapRevisionToDto(TenderRevision revision)
    {
        return new TenderRevisionDto
        {
            Id = revision.Id,
            TenderId = revision.TenderId,
            RevisionNumber = revision.RevisionNumber,
            RevisionDate = revision.RevisionDate,
            RevisedByName = string.Empty, // Would need to fetch from User entity
            RevisionType = revision.RevisionType,
            Description = revision.Description,
            NewSubmissionDeadline = revision.NewSubmissionDeadline,
            RequiresRebid = revision.RequiresRebid,
            NotificationSent = revision.NotificationSent
        };
    }

    private static TenderBidSummaryDto MapBidToSummaryDto(TenderBid bid, Tender? tender = null)
    {
        return new TenderBidSummaryDto
        {
            Id = bid.Id,
            TenderId = bid.TenderId,
            TenderNumber = tender?.TenderNumber ?? string.Empty,
            TenderTitle = tender?.Title ?? string.Empty,
            BusinessPartnerId = bid.BusinessPartnerId,
            BusinessPartnerName = bid.BusinessPartner?.PartnerName ?? string.Empty,
            BidNumber = bid.BidNumber,
            SubmittedDate = bid.SubmittedDate,
            Status = bid.Status,
            TotalBidAmount = bid.TotalBidAmount,
            Currency = bid.Currency,
            TotalScore = bid.TotalScore,
            Rank = bid.Rank,
            IsCompliant = bid.IsCompliant,
            HasPaidFees = false, // Payment info not available in this context
            // QCBS Scores
            TechnicalScore = bid.TechnicalScore,
            FinancialScore = bid.FinancialScore,
            CombinedScore = bid.CombinedScore,
            IsQualifiedTechnically = bid.IsQualifiedTechnically,
            DisqualificationReason = bid.DisqualificationReason
        };
    }

    #region Tender LOT Methods

    public async Task<TenderLotDto> AddTenderLotAsync(Guid tenderId, CreateTenderLotDto dto)
    {
        try
        {
            var tender = await _tenderRepository.GetByIdAsync(tenderId);
            if (tender == null)
                throw new InvalidOperationException($"Tender with ID {tenderId} not found");

            var lotCode = await _lotRepository.GenerateLotCodeAsync(tenderId);

            var lot = new TenderLot
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderId = tenderId,
                LotNumber = dto.LotNumber,
                LotCode = dto.LotCode ?? lotCode,
                Title = dto.Title,
                Description = dto.Description,
                EstimatedValue = dto.EstimatedValue,
                Currency = dto.Currency ?? tender.Currency,
                RequiredDeliveryDate = dto.RequiredDeliveryDate,
                DeliveryLocation = dto.DeliveryLocation,
                Specifications = dto.Specifications,
                Notes = dto.Notes,
                DisplayOrder = dto.DisplayOrder,
                Status = "Active"
            };

            await _lotRepository.CreateAsync(lot);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created tender lot {LotCode} for tender {TenderId}", lot.LotCode, tenderId);

            return MapToLotDto(lot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tender lot for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<TenderLotDto> UpdateTenderLotAsync(Guid lotId, UpdateTenderLotDto dto)
    {
        try
        {
            var lot = await _lotRepository.GetByIdAsync(lotId);
            if (lot == null)
                throw new InvalidOperationException($"Tender lot with ID {lotId} not found");

            lot.LotCode = dto.LotCode ?? lot.LotCode;
            lot.Title = dto.Title ?? lot.Title;
            lot.Description = dto.Description ?? lot.Description;
            lot.EstimatedValue = dto.EstimatedValue ?? lot.EstimatedValue;
            lot.Currency = dto.Currency ?? lot.Currency;
            lot.RequiredDeliveryDate = dto.RequiredDeliveryDate ?? lot.RequiredDeliveryDate;
            lot.DeliveryLocation = dto.DeliveryLocation ?? lot.DeliveryLocation;
            lot.Specifications = dto.Specifications ?? lot.Specifications;
            lot.Notes = dto.Notes ?? lot.Notes;
            lot.DisplayOrder = dto.DisplayOrder;

            await _lotRepository.UpdateAsync(lot);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated tender lot {LotId}", lotId);

            return MapToLotDto(lot);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tender lot {LotId}", lotId);
            throw;
        }
    }

    public async Task DeleteTenderLotAsync(Guid lotId)
    {
        try
        {
            var lot = await _lotRepository.GetByIdWithItemsAsync(lotId);
            if (lot == null)
                throw new InvalidOperationException($"Tender lot with ID {lotId} not found");

            // Remove lot assignment from items
            if (lot.Items != null)
            {
                foreach (var item in lot.Items)
                {
                    item.LotId = null;
                    await _itemRepository.UpdateAsync(item);
                }
            }

            await _lotRepository.DeleteAsync(lotId);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted tender lot {LotId}", lotId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tender lot {LotId}", lotId);
            throw;
        }
    }

    public async Task<IEnumerable<TenderLotDto>> GetTenderLotsAsync(Guid tenderId)
    {
        try
        {
            var lots = await _lotRepository.GetByTenderIdAsync(tenderId);
            return lots.Select(MapToLotDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tender lots for tender {TenderId}", tenderId);
            throw;
        }
    }

    public async Task<TenderLotDto?> GetTenderLotByIdAsync(Guid lotId)
    {
        try
        {
            var lot = await _lotRepository.GetByIdWithItemsAsync(lotId);
            return lot != null ? MapToLotDto(lot) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tender lot {LotId}", lotId);
            throw;
        }
    }

    public async Task AssignItemToLotAsync(Guid itemId, Guid lotId)
    {
        try
        {
            var item = await _itemRepository.GetByIdAsync(itemId);
            if (item == null)
                throw new InvalidOperationException($"Tender item with ID {itemId} not found");

            var lot = await _lotRepository.GetByIdAsync(lotId);
            if (lot == null)
                throw new InvalidOperationException($"Tender lot with ID {lotId} not found");

            if (item.TenderId != lot.TenderId)
                throw new InvalidOperationException("Item and lot must belong to the same tender");

            item.LotId = lotId;
            await _itemRepository.UpdateAsync(item);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Assigned item {ItemId} to lot {LotId}", itemId, lotId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning item {ItemId} to lot {LotId}", itemId, lotId);
            throw;
        }
    }

    public async Task RemoveItemFromLotAsync(Guid itemId)
    {
        try
        {
            var item = await _itemRepository.GetByIdAsync(itemId);
            if (item == null)
                throw new InvalidOperationException($"Tender item with ID {itemId} not found");

            item.LotId = null;
            await _itemRepository.UpdateAsync(item);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Removed item {ItemId} from lot", itemId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing item {ItemId} from lot", itemId);
            throw;
        }
    }

    private static TenderLotDto MapToLotDto(TenderLot lot)
    {
        return new TenderLotDto
        {
            Id = lot.Id,
            TenderId = lot.TenderId,
            LotNumber = lot.LotNumber,
            LotCode = lot.LotCode,
            Title = lot.Title,
            Description = lot.Description,
            EstimatedValue = lot.EstimatedValue,
            Currency = lot.Currency,
            Status = lot.Status,
            RequiredDeliveryDate = lot.RequiredDeliveryDate,
            DeliveryLocation = lot.DeliveryLocation,
            Specifications = lot.Specifications,
            Notes = lot.Notes,
            DisplayOrder = lot.DisplayOrder,
            ItemCount = lot.Items?.Count ?? 0,
            BidCount = lot.BidLots?.Count ?? 0,
            IsAwarded = lot.Awards?.Any() ?? false,
            AwardedToPartnerName = lot.Awards?.FirstOrDefault()?.BusinessPartner?.PartnerName,
            Items = lot.Items?.Select(i => new TenderItemDto
            {
                Id = i.Id,
                TenderId = i.TenderId,
                LotId = i.LotId,
                LotCode = lot.LotCode,
                LotTitle = lot.Title,
                LineNumber = i.LineNumber,
                ItemCode = i.ItemCode,
                Description = i.Description,
                Quantity = i.Quantity,
                UnitOfMeasure = i.UnitOfMeasure,
                Specifications = i.Specifications,
                RequiredDeliveryDate = i.RequiredDeliveryDate,
                DeliveryLocation = i.DeliveryLocation
            }).ToList() ?? new List<TenderItemDto>(),
            CreatedAt = lot.CreatedAt
        };
    }

    #endregion
}

