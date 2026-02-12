using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance
{
    public interface IAssetTransferService
    {
        Task<AssetTransferDto?> GetByIdAsync(Guid id);
        Task<IEnumerable<AssetTransferDto>> GetAllAsync();
        Task<IEnumerable<AssetTransferDto>> GetByAssetIdAsync(Guid assetId);
        Task<AssetTransferDto> RequestTransferAsync(RequestAssetTransferDto dto, Guid requestedById);
        Task<AssetTransferDto> ApproveTransferAsync(Guid transferId, Guid approvedById, ApproveAssetTransferDto dto);
        Task<AssetTransferDto> CompleteTransferAsync(Guid transferId);
        Task<AssetTransferDto> RejectTransferAsync(Guid transferId, Guid rejectedById, string comments);
    }
}
