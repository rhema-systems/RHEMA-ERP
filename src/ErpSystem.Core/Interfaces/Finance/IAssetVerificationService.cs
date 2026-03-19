using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IAssetVerificationService
{
    // Session Management
    Task<AssetVerificationSessionDto?> GetSessionByIdAsync(Guid sessionId);
    Task<IEnumerable<AssetVerificationSessionDto>> GetAllSessionsAsync();
    Task<AssetVerificationSessionDto> CreateSessionAsync(CreateAssetVerificationSessionDto dto);
    Task<AssetVerificationSessionDto> StartSessionAsync(Guid sessionId);
    Task<AssetVerificationSessionDto> CompleteSessionAsync(Guid sessionId);
    
    // Item Verification
    Task<AssetVerificationItemDto> VerifyAssetAsync(Guid sessionId, Guid assetId, VerifyAssetDto dto);
    Task<IEnumerable<AssetVerificationItemDto>> GetSessionItemsAsync(Guid sessionId);
}
