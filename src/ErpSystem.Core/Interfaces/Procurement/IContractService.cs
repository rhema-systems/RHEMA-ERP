using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

/// <summary>
/// Contract service interface
/// </summary>
public interface IContractService
{
    // Contract CRUD
    Task<ContractDto?> GetByIdAsync(Guid id);
    Task<ContractDto?> GetByContractNumberAsync(string contractNumber);
    Task<ContractDto?> GetByAwardIdAsync(Guid awardId);
    Task<PagedResult<ContractListDto>> GetContractsAsync(int page, int pageSize, string? search = null, string? status = null, string? contractType = null);
    Task<IEnumerable<ContractListDto>> GetByBusinessPartnerIdAsync(Guid businessPartnerId);
    Task<IEnumerable<ContractListDto>> GetActiveContractsAsync();
    Task<IEnumerable<ContractListDto>> GetExpiringContractsAsync(int daysThreshold = 30);
    Task<ContractDto> CreateFromAwardAsync(CreateContractDto dto);
    Task<ContractDto> UpdateAsync(Guid id, UpdateContractDto dto);
    Task DeleteAsync(Guid id);

    // Status management
    Task<ContractDto> UpdateStatusAsync(Guid id, UpdateContractStatusDto dto);
    Task<ContractDto> ActivateContractAsync(Guid id, UpdateContractStatusDto dto);
    Task<ContractDto> CompleteContractAsync(Guid id);
    Task<ContractDto> TerminateContractAsync(Guid id, string reason);

    // Milestones
    Task<ContractMilestoneDto> AddMilestoneAsync(Guid contractId, CreateContractMilestoneDto dto);
    Task<ContractMilestoneDto> UpdateMilestoneAsync(Guid milestoneId, UpdateContractMilestoneDto dto);
    Task<ContractMilestoneDto> UpdateMilestoneStatusAsync(Guid milestoneId, UpdateMilestoneStatusDto dto);
    Task DeleteMilestoneAsync(Guid milestoneId);
    Task<IEnumerable<ContractMilestoneDto>> GetMilestonesByContractIdAsync(Guid contractId);

    // Amendments
    Task<ContractAmendmentDto> CreateAmendmentAsync(Guid contractId, CreateContractAmendmentDto dto);
    Task<ContractAmendmentDto> ProcessAmendmentAsync(Guid amendmentId, ProcessAmendmentDto dto);
    Task DeleteAmendmentAsync(Guid amendmentId);
    Task<IEnumerable<ContractAmendmentDto>> GetAmendmentsByContractIdAsync(Guid contractId);
    Task<IEnumerable<ContractAmendmentDto>> GetPendingAmendmentsAsync();

    // Documents
    Task<ContractDocumentDto> UploadDocumentAsync(Guid contractId, string documentType, string fileName, string filePath, string? contentType, long? fileSize, string? description);
    Task DeleteDocumentAsync(Guid documentId);
    Task<IEnumerable<ContractDocumentDto>> GetDocumentsByContractIdAsync(Guid contractId);
}

