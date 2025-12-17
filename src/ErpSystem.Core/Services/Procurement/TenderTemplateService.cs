using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class TenderTemplateService : ITenderTemplateService
{
    private readonly ITenderTemplateRepository _templateRepository;
    private readonly ITenderRepository _tenderRepository;
    private readonly ITenderItemRepository _itemRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<TenderTemplateService> _logger;

    public TenderTemplateService(
        ITenderTemplateRepository templateRepository,
        ITenderRepository tenderRepository,
        ITenderItemRepository itemRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<TenderTemplateService> logger)
    {
        _templateRepository = templateRepository;
        _tenderRepository = tenderRepository;
        _itemRepository = itemRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<PagedResult<TenderTemplateDto>> GetAllTemplatesAsync(int pageNumber, int pageSize, string? searchTerm = null)
    {
        try
        {
            // Use GetActiveTemplatesAsync which returns all active templates
            var allTemplates = await _templateRepository.GetActiveTemplatesAsync();
            var templates = allTemplates.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                templates = templates.Where(t =>
                    t.TemplateName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    (t.Description != null && t.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                );
            }

            var totalCount = templates.Count();
            var items = templates
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(MapToDto)
                .ToList();

            return new PagedResult<TenderTemplateDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = pageNumber,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tender templates");
            throw;
        }
    }

    public async Task<TenderTemplateDto?> GetTemplateByIdAsync(Guid id)
    {
        try
        {
            var template = await _templateRepository.GetByIdAsync(id);
            return template == null ? null : MapToDto(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tender template {TemplateId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<TenderTemplateDto>> GetActiveTemplatesAsync()
    {
        try
        {
            var templates = await _templateRepository.GetActiveTemplatesAsync();
            return templates.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active tender templates");
            throw;
        }
    }

    public async Task<IEnumerable<TenderTemplateDto>> GetTemplatesByTypeAsync(string tenderType)
    {
        try
        {
            var templates = await _templateRepository.GetByTenderTypeAsync(tenderType);
            return templates.Select(MapToDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tender templates by type {TenderType}", tenderType);
            throw;
        }
    }

    public async Task<TenderTemplateDto> CreateTemplateAsync(CreateTenderTemplateDto dto)
    {
        try
        {
            var template = new TenderTemplate
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TemplateName = dto.TemplateName,
                Description = dto.Description,
                TenderType = dto.TenderType,
                Category = dto.Category,
                PriceWeightage = dto.PriceWeightage,
                QualityWeightage = dto.QualityWeightage,
                DeliveryWeightage = dto.DeliveryWeightage,
                ExperienceWeightage = dto.ExperienceWeightage,
                EvaluationCriteriaJson = dto.EvaluationCriteriaJson,
                DefaultValidityDays = dto.DefaultValidityDays,
                RequiredDocuments = dto.RequiredDocuments,
                TermsAndConditions = dto.TermsAndConditions,
                RequiresPrequalification = dto.RequiresPrequalification,
                AllowPartialBids = dto.AllowPartialBids,
                IsActive = true,
                CreatedById = _currentUserProvider.UserId,
                CreatedAt = DateTime.UtcNow
            };

            await _templateRepository.CreateAsync(template);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created tender template {TemplateId}", template.Id);

            return MapToDto(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tender template");
            throw;
        }
    }

    public async Task<TenderTemplateDto> UpdateTemplateAsync(Guid id, CreateTenderTemplateDto dto)
    {
        try
        {
            var template = await _templateRepository.GetByIdAsync(id)
                ?? throw new InvalidOperationException($"Template with ID {id} not found");

            template.TemplateName = dto.TemplateName;
            template.Description = dto.Description;
            template.TenderType = dto.TenderType;
            template.Category = dto.Category;
            template.PriceWeightage = dto.PriceWeightage;
            template.QualityWeightage = dto.QualityWeightage;
            template.DeliveryWeightage = dto.DeliveryWeightage;
            template.ExperienceWeightage = dto.ExperienceWeightage;
            template.EvaluationCriteriaJson = dto.EvaluationCriteriaJson;
            template.DefaultValidityDays = dto.DefaultValidityDays;
            template.RequiredDocuments = dto.RequiredDocuments;
            template.TermsAndConditions = dto.TermsAndConditions;
            template.RequiresPrequalification = dto.RequiresPrequalification;
            template.AllowPartialBids = dto.AllowPartialBids;
            template.IsActive = dto.IsActive;
            template.UpdatedAt = DateTime.UtcNow;

            await _templateRepository.UpdateAsync(template);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated tender template {TemplateId}", id);

            return MapToDto(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tender template {TemplateId}", id);
            throw;
        }
    }

    public async Task DeleteTemplateAsync(Guid id)
    {
        try
        {
            await _templateRepository.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted tender template {TemplateId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tender template {TemplateId}", id);
            throw;
        }
    }

    public async Task<TenderDetailDto> CreateTenderFromTemplateAsync(Guid templateId, string title)
    {
        try
        {
            var template = await _templateRepository.GetByIdAsync(templateId)
                ?? throw new InvalidOperationException($"Template with ID {templateId} not found");

            // Generate tender number
            var tenderNumber = await _tenderRepository.GenerateTenderNumberAsync();

            var validityDays = template.DefaultValidityDays ?? 30;

            var tender = new Tender
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUserProvider.TenantId,
                TenderNumber = tenderNumber,
                Title = title,
                Description = template.Description ?? string.Empty,
                TenderType = template.TenderType,
                Status = "Draft",
                PublishDate = DateTime.UtcNow,
                SubmissionDeadline = DateTime.UtcNow.AddDays(validityDays),
                OpeningDate = DateTime.UtcNow.AddDays(validityDays + 1),
                EstimatedValue = 0,
                Currency = "USD",
                MinimumPerformanceRating = 0,
                RequiresPrequalification = template.RequiresPrequalification,
                AllowPartialBids = template.AllowPartialBids,
                PriceWeightage = template.PriceWeightage,
                QualityWeightage = template.QualityWeightage,
                DeliveryWeightage = template.DeliveryWeightage,
                ExperienceWeightage = template.ExperienceWeightage,
                EvaluationCriteriaJson = template.EvaluationCriteriaJson,
                TermsAndConditions = template.TermsAndConditions,
                CreatedById = _currentUserProvider.UserId,
                CreatedAt = DateTime.UtcNow
            };

            await _tenderRepository.CreateAsync(tender);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created tender {TenderId} from template {TemplateId}", tender.Id, templateId);

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
                TermsAndConditions = tender.TermsAndConditions,
                Notes = tender.Notes,
                CreatedAt = tender.CreatedAt,
                Items = new List<TenderItemDto>(),
                Documents = new List<TenderDocumentDto>(),
                Fees = new List<TenderFeeDto>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tender from template {TemplateId}", templateId);
            throw;
        }
    }

    private static TenderTemplateDto MapToDto(TenderTemplate template)
    {
        return new TenderTemplateDto
        {
            Id = template.Id,
            TemplateName = template.TemplateName,
            Description = template.Description,
            TenderType = template.TenderType,
            Category = template.Category,
            PriceWeightage = template.PriceWeightage,
            QualityWeightage = template.QualityWeightage,
            DeliveryWeightage = template.DeliveryWeightage,
            ExperienceWeightage = template.ExperienceWeightage,
            EvaluationCriteriaJson = template.EvaluationCriteriaJson,
            DefaultValidityDays = template.DefaultValidityDays,
            RequiredDocuments = template.RequiredDocuments,
            TermsAndConditions = template.TermsAndConditions,
            RequiresPrequalification = template.RequiresPrequalification,
            AllowPartialBids = template.AllowPartialBids,
            IsActive = template.IsActive,
            CreatedById = template.CreatedById,
            CreatedAt = template.CreatedAt
        };
    }
}

