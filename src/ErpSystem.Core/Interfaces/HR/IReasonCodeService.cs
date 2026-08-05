using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// CRUD for the system-wide reusable reason-code lookup.
/// </summary>
public interface IReasonCodeService
{
    Task<IEnumerable<ReasonCodeDto>> GetAllAsync(ReasonCodeCategory? category = null, bool activeOnly = false);
    Task<ReasonCodeDto?> GetByIdAsync(Guid id);
    Task<ReasonCodeDto> CreateAsync(CreateReasonCodeDto dto);
    Task<ReasonCodeDto> UpdateAsync(Guid id, UpdateReasonCodeDto dto);
    Task DeactivateAsync(Guid id);
}
