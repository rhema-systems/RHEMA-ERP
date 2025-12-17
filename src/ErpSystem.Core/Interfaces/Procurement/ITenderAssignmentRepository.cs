using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

/// <summary>
/// Repository interface for TenderAssignment entity
/// </summary>
public interface ITenderAssignmentRepository
{
    Task<TenderAssignment?> GetByIdAsync(Guid id);
    Task<IEnumerable<TenderAssignment>> GetByTenderIdAsync(Guid tenderId);
    Task<IEnumerable<TenderAssignment>> GetByUserIdAsync(Guid userId);
    Task<IEnumerable<TenderAssignment>> GetByBusinessPartnerIdAsync(Guid businessPartnerId);
    Task<IEnumerable<TenderAssignment>> GetByTenderAndBusinessPartnerAsync(Guid tenderId, Guid businessPartnerId);
    Task<TenderAssignment> CreateAsync(TenderAssignment entity);
    Task DeleteAsync(Guid id);
    Task<bool> ExistsAsync(Guid tenderId, Guid businessPartnerId, Guid? userId = null);
}

