using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

/// <summary>
/// Tender document type service interface
/// </summary>
public interface ITenderDocumentTypeService
{
    Task<TenderDocumentTypeDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<TenderDocumentTypeDto>> GetAllAsync();
    Task<IEnumerable<TenderDocumentTypeDto>> GetActiveAsync();
    Task<IEnumerable<TenderDocumentTypeDto>> GetByCategoryAsync(string category);
    Task<TenderDocumentTypeDto> CreateAsync(CreateTenderDocumentTypeDto dto);
    Task<TenderDocumentTypeDto> UpdateAsync(Guid id, UpdateTenderDocumentTypeDto dto);
    Task DeleteAsync(Guid id);
}

