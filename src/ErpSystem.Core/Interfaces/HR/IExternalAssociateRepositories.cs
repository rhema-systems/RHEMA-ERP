using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Repository interface for ExternalAssociate operations.
/// </summary>
public interface IExternalAssociateRepository : IGenericRepository<ExternalAssociate>
{
    Task<ExternalAssociate?> GetByAssociateNumberAsync(string associateNumber);
    Task<ExternalAssociate?> GetByEmailAsync(string email);
    Task<IEnumerable<ExternalAssociate>> GetActiveAsync();
    Task<IEnumerable<ExternalAssociate>> SearchAsync(string q, int limit = 20);
    Task<bool> AssociateNumberExistsAsync(string associateNumber, Guid? excludeId = null);
    Task<bool> EmailExistsAsync(string email, Guid? excludeId = null);
    Task<string> GenerateAssociateNumberAsync();
}
