using ErpSystem.Core.DTOs.Identity;

namespace ErpSystem.Core.Interfaces.Identity;

public interface IHrIdentityAccessService
{
    Task<HrIdentityAccessDecisionDto> EvaluateAsync(Guid userId, CancellationToken cancellationToken = default);
}
