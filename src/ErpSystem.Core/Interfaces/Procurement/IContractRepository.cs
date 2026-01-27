using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

/// <summary>
/// Contract repository interface
/// </summary>
public interface IContractRepository
{
    Task<Contract?> GetByIdAsync(Guid id);
    Task<Contract?> GetByIdWithDetailsAsync(Guid id);
    Task<Contract?> GetByContractNumberAsync(string contractNumber);
    Task<Contract?> GetByAwardIdAsync(Guid awardId);
    Task<IEnumerable<Contract>> GetByBusinessPartnerIdAsync(Guid businessPartnerId);
    Task<IEnumerable<Contract>> GetByTenderIdAsync(Guid tenderId);
    Task<PagedResult<Contract>> GetContractsAsync(int page, int pageSize, string? search = null, string? status = null, string? contractType = null);
    Task<IEnumerable<Contract>> GetActiveContractsAsync();
    Task<IEnumerable<Contract>> GetExpiringContractsAsync(int daysThreshold);
    Task<Contract> CreateAsync(Contract contract);
    Task<Contract> UpdateAsync(Contract contract);
    Task DeleteAsync(Guid id);
    Task<string> GenerateContractNumberAsync();
}

/// <summary>
/// Contract milestone repository interface
/// </summary>
public interface IContractMilestoneRepository
{
    Task<ContractMilestone?> GetByIdAsync(Guid id);
    Task<IEnumerable<ContractMilestone>> GetByContractIdAsync(Guid contractId);
    Task<IEnumerable<ContractMilestone>> GetPendingMilestonesAsync();
    Task<ContractMilestone> CreateAsync(ContractMilestone milestone);
    Task<ContractMilestone> UpdateAsync(ContractMilestone milestone);
    Task DeleteAsync(Guid id);
    Task DeleteByContractIdAsync(Guid contractId);
}

/// <summary>
/// Contract amendment repository interface
/// </summary>
public interface IContractAmendmentRepository
{
    Task<ContractAmendment?> GetByIdAsync(Guid id);
    Task<IEnumerable<ContractAmendment>> GetByContractIdAsync(Guid contractId);
    Task<IEnumerable<ContractAmendment>> GetPendingApprovalAsync();
    Task<ContractAmendment> CreateAsync(ContractAmendment amendment);
    Task<ContractAmendment> UpdateAsync(ContractAmendment amendment);
    Task DeleteAsync(Guid id);
    Task<string> GenerateAmendmentNumberAsync(Guid contractId);
}

/// <summary>
/// Contract document repository interface
/// </summary>
public interface IContractDocumentRepository
{
    Task<ContractDocument?> GetByIdAsync(Guid id);
    Task<IEnumerable<ContractDocument>> GetByContractIdAsync(Guid contractId);
    Task<ContractDocument> CreateAsync(ContractDocument document);
    Task<ContractDocument> UpdateAsync(ContractDocument document);
    Task DeleteAsync(Guid id);
}

