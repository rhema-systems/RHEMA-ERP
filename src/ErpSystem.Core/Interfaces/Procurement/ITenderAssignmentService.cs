using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

/// <summary>
/// Service interface for managing tender assignments
/// </summary>
public interface ITenderAssignmentService
{
    /// <summary>
    /// Get all assignments for a tender
    /// </summary>
    Task<IEnumerable<TenderAssignmentDto>> GetByTenderIdAsync(Guid tenderId);

    /// <summary>
    /// Get all tenders assigned to a user
    /// </summary>
    Task<IEnumerable<TenderAssignmentDto>> GetByUserIdAsync(Guid userId);

    /// <summary>
    /// Get all tenders assigned to a business partner
    /// </summary>
    Task<IEnumerable<TenderAssignmentDto>> GetByBusinessPartnerIdAsync(Guid businessPartnerId);

    /// <summary>
    /// Create a new tender assignment
    /// </summary>
    Task<TenderAssignmentDto> CreateAsync(CreateTenderAssignmentDto dto);

    /// <summary>
    /// Delete a tender assignment
    /// </summary>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Check if a user has access to a tender
    /// </summary>
    Task<bool> HasAccessAsync(Guid userId, Guid tenderId);

    /// <summary>
    /// Get tenders accessible by a user (considering AllUsers assignments)
    /// </summary>
    Task<IEnumerable<Guid>> GetAccessibleTenderIdsAsync(Guid userId, Guid businessPartnerId);
}

