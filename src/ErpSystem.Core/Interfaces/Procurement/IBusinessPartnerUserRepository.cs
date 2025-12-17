using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

/// <summary>
/// Repository interface for BusinessPartnerUser entity
/// </summary>
public interface IBusinessPartnerUserRepository
{
    Task<BusinessPartnerUser?> GetByIdAsync(Guid id);
    Task<BusinessPartnerUser?> GetByUserIdAsync(Guid userId);
    Task<BusinessPartnerUser?> GetByBusinessPartnerAndUserAsync(Guid businessPartnerId, Guid userId);
    Task<IEnumerable<BusinessPartnerUser>> GetByBusinessPartnerIdAsync(Guid businessPartnerId);
    Task<IEnumerable<BusinessPartnerUser>> GetActiveByBusinessPartnerIdAsync(Guid businessPartnerId);
    Task<BusinessPartnerUser> CreateAsync(BusinessPartnerUser entity);
    Task<BusinessPartnerUser> UpdateAsync(BusinessPartnerUser entity);
    Task DeleteAsync(Guid id);
    Task<bool> ExistsAsync(Guid businessPartnerId, Guid userId);
}

