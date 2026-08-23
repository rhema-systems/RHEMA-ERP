using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

#region Asset Type Services

public interface IAssetTypeService
{
    Task<AssetTypeDto?> GetByIdAsync(Guid id);
    Task<AssetTypeDetailDto?> GetWithAttributesAsync(Guid id);
    Task<IEnumerable<AssetTypeSummaryDto>> GetAllAsync();
    Task<PagedResult<AssetTypeSummaryDto>> GetPagedAsync(int page, int pageSize, string? searchTerm = null);
    Task<AssetTypeDto> CreateAsync(CreateAssetTypeDto dto);
    Task<AssetTypeDto> UpdateAsync(Guid id, UpdateAssetTypeDto dto);
    Task DeleteAsync(Guid id);
}

public interface IAssetTypeAttributeService
{
    Task<AssetTypeAttributeDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<AssetTypeAttributeDto>> GetByAssetTypeIdAsync(Guid assetTypeId);
    Task<AssetTypeAttributeDto> CreateAsync(CreateAssetTypeAttributeDto dto);
    Task<AssetTypeAttributeDto> UpdateAsync(Guid id, UpdateAssetTypeAttributeDto dto);
    Task DeleteAsync(Guid id);
}

#endregion

#region Company Asset Services

public interface ICompanyAssetService
{
    Task<CompanyAssetDto?> GetByIdAsync(Guid id);
    Task<CompanyAssetDetailDto?> GetWithDetailsAsync(Guid id);
    Task<IEnumerable<CompanyAssetSummaryDto>> GetAllAsync();
    Task<PagedResult<CompanyAssetSummaryDto>> GetPagedAsync(int page, int pageSize, string? searchTerm = null, CompanyAssetStatus? status = null, Guid? assetTypeId = null);
    Task<IEnumerable<CompanyAssetSummaryDto>> GetByStatusAsync(CompanyAssetStatus status);
    Task<IEnumerable<CompanyAssetSummaryDto>> GetByAssetTypeAsync(Guid assetTypeId);
    Task<IEnumerable<CompanyAssetSummaryDto>> GetAvailableForAssignmentAsync();
    Task<IEnumerable<CompanyAssetSummaryDto>> GetByEmployeeAsync(Guid employeeId);
    Task<IEnumerable<CompanyAssetSummaryDto>> GetDueForMaintenanceAsync(int daysAhead = 30);
    Task<CompanyAssetDto> CreateAsync(CreateCompanyAssetDto dto);

    /// <summary>
    /// AST-11 — the Finance fixed assets HR could register, with the ones it already has marked.
    /// </summary>
    /// <remarks>
    /// Already-linked assets are RETURNED and flagged, not filtered out. A picker that silently
    /// omits them leaves a user hunting for an asset that is right there; one that shows it greyed
    /// with "already registered" answers the question they actually have.
    /// </remarks>
    Task<IEnumerable<FixedAssetPickDto>> GetLinkableFixedAssetsAsync(string? searchTerm = null);

    /// <summary>AST-11 — register an HR asset that stands for an existing Finance fixed asset.</summary>
    Task<CompanyAssetDto> CreateFromFixedAssetAsync(CreateAssetFromFixedAssetDto dto);
    Task<CompanyAssetDto> UpdateAsync(Guid id, UpdateCompanyAssetDto dto);
    Task DeleteAsync(Guid id);
    Task DisposeAssetAsync(DisposeAssetDto dto);
}

public interface IAssetAttributeValueService
{
    Task<AssetAttributeValueDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<AssetAttributeValueDto>> GetByAssetIdAsync(Guid assetId);
    Task<AssetAttributeValueDto> CreateAsync(Guid assetId, CreateAssetAttributeValueDto dto);
    Task<AssetAttributeValueDto> UpdateAsync(Guid id, UpdateAssetAttributeValueDto dto);
    Task DeleteAsync(Guid id);
}

#endregion

#region Asset Assignment Services

public interface IAssetAssignmentService
{
    Task<AssetAssignmentDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<AssetAssignmentSummaryDto>> GetAllAsync();
    Task<PagedResult<AssetAssignmentSummaryDto>> GetPagedAsync(int page, int pageSize, string? searchTerm = null, AssignmentStatus? status = null);
    Task<IEnumerable<AssetAssignmentSummaryDto>> GetByAssetIdAsync(Guid assetId);
    Task<AssetAssignmentDto?> GetActiveAssignmentForAssetAsync(Guid assetId);
    Task<IEnumerable<AssetAssignmentSummaryDto>> GetByEmployeeIdAsync(Guid employeeId);
    Task<IEnumerable<AssetAssignmentSummaryDto>> GetActiveAssignmentsForEmployeeAsync(Guid employeeId);
    Task<IEnumerable<AssetAssignmentSummaryDto>> GetOverdueAssignmentsAsync();
    Task<AssetAssignmentDto> CreateAsync(CreateAssetAssignmentDto dto);
    Task<AssetAssignmentDto> UpdateAsync(Guid id, UpdateAssetAssignmentDto dto);
    Task DeleteAsync(Guid id);
    Task AcknowledgeAssignmentAsync(AcknowledgeAssignmentDto dto);
    Task ReturnAssetAsync(ReturnAssetDto dto);
}

#endregion

#region Asset Maintenance Services

public interface IAssetMaintenanceService
{
    Task<AssetMaintenanceDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<AssetMaintenanceSummaryDto>> GetAllAsync();
    Task<PagedResult<AssetMaintenanceSummaryDto>> GetPagedAsync(int page, int pageSize, string? searchTerm = null, MaintenanceStatus? status = null);
    Task<IEnumerable<AssetMaintenanceSummaryDto>> GetByAssetIdAsync(Guid assetId);
    Task<IEnumerable<AssetMaintenanceSummaryDto>> GetScheduledMaintenanceAsync(DateTime from, DateTime to);
    Task<AssetMaintenanceDto> CreateAsync(CreateAssetMaintenanceDto dto);
    Task<AssetMaintenanceDto> UpdateAsync(Guid id, UpdateAssetMaintenanceDto dto);
    Task DeleteAsync(Guid id);
    Task CompleteMaintenanceAsync(Guid id, string? completionNotes = null);
}

#endregion

#region Asset Attachment Services

public interface IAssetAttachmentService
{
    Task<AssetAttachmentDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<AssetAttachmentDto>> GetByAssetIdAsync(Guid assetId);
    Task<AssetAttachmentDto> CreateAsync(Guid assetId, CreateAssetAttachmentDto dto);
    Task DeleteAsync(Guid id);
}

public interface IAssetImageService
{
    Task<AssetImageDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<AssetImageDto>> GetByAssetIdAsync(Guid assetId);
    Task<AssetImageDto> CreateAsync(Guid assetId, CreateAssetImageDto dto);
    Task DeleteAsync(Guid id);
}

#endregion

#region Asset Requisition Services

public interface IAssetRequisitionService
{
    Task<AssetRequisitionDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<AssetRequisitionSummaryDto>> GetAllAsync();
    Task<PagedResult<AssetRequisitionSummaryDto>> GetPagedAsync(int page, int pageSize, string? searchTerm = null, AssetRequisitionStatus? status = null);
    Task<IEnumerable<AssetRequisitionSummaryDto>> GetByRequestedByIdAsync(Guid employeeId);
    Task<IEnumerable<AssetRequisitionSummaryDto>> GetPendingApprovalsAsync();
    Task<AssetRequisitionDto> CreateAsync(CreateAssetRequisitionDto dto);
    Task<AssetRequisitionDto> UpdateAsync(Guid id, UpdateAssetRequisitionDto dto);
    Task DeleteAsync(Guid id);

    /// <summary>Sends a draft requisition for approval (D3 - the workflow engine).</summary>
    Task<AssetRequisitionDto> SubmitAsync(Guid id);

    /// <summary>Pulls a submitted requisition back to draft, for the requester alone.</summary>
    Task<AssetRequisitionDto> RecallAsync(Guid id, string? reason);

    Task ApproveAsync(Guid id, ApproveAssetRequisitionDto dto);
    Task RejectAsync(Guid id, RejectAssetRequisitionDto dto);
    Task FulfillAsync(Guid id, FulfillAssetRequisitionDto dto);
}

#endregion

#region Asset Transfer Services

public interface IAssetTransferService
{
    Task<AssetTransferDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<AssetTransferSummaryDto>> GetAllAsync();
    Task<PagedResult<AssetTransferSummaryDto>> GetPagedAsync(int page, int pageSize, string? searchTerm = null, HRAssetTransferStatus? status = null);
    Task<IEnumerable<AssetTransferSummaryDto>> GetByAssetIdAsync(Guid assetId);
    Task<IEnumerable<AssetTransferSummaryDto>> GetPendingTransfersAsync();
    Task<AssetTransferDto> CreateAsync(CreateAssetTransferDto dto);
    Task<AssetTransferDto> UpdateAsync(Guid id, UpdateAssetTransferDto dto);
    Task DeleteAsync(Guid id);

    /// <summary>Sends a draft transfer for approval (D3 - the workflow engine).</summary>
    Task<AssetTransferDto> SubmitAsync(Guid id);

    /// <summary>Pulls a submitted transfer back to draft, for the initiator alone.</summary>
    Task<AssetTransferDto> RecallAsync(Guid id, string? reason);

    Task ApproveAsync(Guid id);
    Task CompleteAsync(Guid id);
    Task RejectAsync(Guid id);
}

#endregion

