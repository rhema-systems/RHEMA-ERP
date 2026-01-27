using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class TenderDocumentTypeService : ITenderDocumentTypeService
{
    private readonly ITenderDocumentTypeRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<TenderDocumentTypeService> _logger;

    public TenderDocumentTypeService(
        ITenderDocumentTypeRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<TenderDocumentTypeService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<TenderDocumentTypeDto?> GetByIdAsync(Guid id)
    {
        var documentType = await _repository.GetByIdAsync(id);
        return documentType == null ? null : MapToDto(documentType);
    }

    public async Task<IEnumerable<TenderDocumentTypeDto>> GetAllAsync()
    {
        var documentTypes = await _repository.GetAllAsync();
        return documentTypes.Select(MapToDto);
    }

    public async Task<IEnumerable<TenderDocumentTypeDto>> GetActiveAsync()
    {
        var documentTypes = await _repository.GetActiveAsync();
        return documentTypes.Select(MapToDto);
    }

    public async Task<IEnumerable<TenderDocumentTypeDto>> GetByCategoryAsync(string category)
    {
        var documentTypes = await _repository.GetByCategoryAsync(category);
        return documentTypes.Select(MapToDto);
    }

    public async Task<TenderDocumentTypeDto> CreateAsync(CreateTenderDocumentTypeDto dto)
    {
        // Check if code already exists
        var existing = await _repository.GetByCodeAsync(dto.DocumentCode);
        if (existing != null)
        {
            throw new InvalidOperationException($"Document type with code '{dto.DocumentCode}' already exists");
        }

        var documentType = new TenderDocumentType
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUserProvider.TenantId,
            DocumentName = dto.DocumentName,
            DocumentCode = dto.DocumentCode,
            Category = dto.Category,
            Description = dto.Description,
            IsRequired = dto.IsRequired,
            MaxFileSizeMB = dto.MaxFileSizeMB,
            AllowedFileTypes = dto.AllowedFileTypes,
            IsActive = dto.IsActive,
            DisplayOrder = dto.DisplayOrder,
            CreatedById = _currentUserProvider.UserId,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(documentType);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Document type created: {DocumentName} ({DocumentCode})", 
            documentType.DocumentName, documentType.DocumentCode);

        return MapToDto(documentType);
    }

    public async Task<TenderDocumentTypeDto> UpdateAsync(Guid id, UpdateTenderDocumentTypeDto dto)
    {
        var documentType = await _repository.GetByIdAsync(id);
        if (documentType == null)
        {
            throw new InvalidOperationException($"Document type with ID '{id}' not found");
        }

        documentType.DocumentName = dto.DocumentName;
        documentType.Description = dto.Description;
        documentType.IsRequired = dto.IsRequired;
        documentType.MaxFileSizeMB = dto.MaxFileSizeMB;
        documentType.AllowedFileTypes = dto.AllowedFileTypes;
        documentType.IsActive = dto.IsActive;
        documentType.DisplayOrder = dto.DisplayOrder;
        documentType.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateAsync(documentType);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Document type updated: {DocumentName} ({DocumentCode})", 
            documentType.DocumentName, documentType.DocumentCode);

        return MapToDto(documentType);
    }

    public async Task DeleteAsync(Guid id)
    {
        var documentType = await _repository.GetByIdAsync(id);
        if (documentType == null)
        {
            throw new InvalidOperationException($"Document type with ID '{id}' not found");
        }

        await _repository.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Document type deleted: {DocumentName} ({DocumentCode})", 
            documentType.DocumentName, documentType.DocumentCode);
    }

    private static TenderDocumentTypeDto MapToDto(TenderDocumentType documentType)
    {
        return new TenderDocumentTypeDto
        {
            Id = documentType.Id,
            DocumentName = documentType.DocumentName,
            DocumentCode = documentType.DocumentCode,
            Category = documentType.Category,
            Description = documentType.Description,
            IsRequired = documentType.IsRequired,
            MaxFileSizeMB = documentType.MaxFileSizeMB,
            AllowedFileTypes = documentType.AllowedFileTypes,
            IsActive = documentType.IsActive,
            DisplayOrder = documentType.DisplayOrder,
            CreatedAt = documentType.CreatedAt,
            CreatedByName = documentType.CreatedBy?.FullName
        };
    }
}

