using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IAssetDisposalService
{
    Task<AssetDisposalDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<AssetDisposalDto>> GetAllAsync();
    Task<AssetDisposalDto> RequestDisposalAsync(RequestAssetDisposalDto dto, Guid requestedById);
    Task<AssetDisposalDto> ApproveDisposalAsync(Guid disposalId, Guid approvedById, ApproveAssetDisposalDto dto);
    Task<AssetDisposalDto> RejectDisposalAsync(Guid disposalId, Guid rejectedById, string comments);
    Task<AssetDisposalDto> CompleteDisposalAsync(Guid disposalId);
    Task<BulkOperationResultDto<AssetDisposalDto>> RequestBulkDisposalAsync(RequestBulkAssetDisposalDto dto, Guid requestedById);
}
