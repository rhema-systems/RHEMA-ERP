using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces
{
    /// <summary>
    /// Service for managing account segment configuration and validation
    /// </summary>
    public interface ISegmentConfigurationService
    {
        // Segment Structure Management
        Task<SegmentStructureDto> CreateSegmentStructureAsync(SegmentStructureCreateDto dto);
        Task<SegmentStructureDto> UpdateSegmentStructureAsync(SegmentStructureUpdateDto dto);
        Task<List<SegmentStructureDto>> GetAllSegmentStructuresAsync();
        Task<SegmentStructureDto?> GetSegmentStructureAsync(Guid id);
        Task DeleteSegmentStructureAsync(Guid id);

        // Account Number Validation and Construction
        Task<SegmentedAccountValidationDto> ValidateSegmentedAccountAsync(string accountNumber);
        Task<AccountNumberConstructionDto> ConstructAccountNumberAsync(Dictionary<int, string> segmentValues);
        Task<List<SegmentValueDto>> ParseAccountNumberAsync(string accountNumber);
    }
}
