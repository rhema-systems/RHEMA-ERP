using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;

namespace ErpSystem.Core.Interfaces.Inventory;

public interface IInventoryDisposalService
{
    Task<IReadOnlyList<InventoryDisposalDto>> SearchAsync(string search, int take = 8, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryDisposalDto>> GetAsync(InventoryDisposalStatus? status, Guid? warehouseId, int take, CancellationToken cancellationToken = default);
    Task<InventoryDisposalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<InventoryDisposalDto> CreateAsync(CreateInventoryDisposalRequest request, CancellationToken cancellationToken = default);
    Task<InventoryDisposalDto> UpdateAsync(Guid id, UpdateInventoryDisposalRequest request, CancellationToken cancellationToken = default);
    Task<InventoryDisposalDto> CancelAsync(Guid id, CancelInventoryDisposalRequest request, CancellationToken cancellationToken = default);
    Task<InventoryDisposalDto> VerifyAsync(Guid id, VerifyInventoryDisposalRequest request, CancellationToken cancellationToken = default);
    Task<InventoryDisposalDto> ScheduleCommitteeAsync(Guid id, ScheduleInventoryDisposalCommitteeRequest request, CancellationToken cancellationToken = default);
    Task<InventoryDisposalDto> VoteAsync(Guid id, VoteInventoryDisposalRequest request, CancellationToken cancellationToken = default);
    Task<InventoryDisposalDto> SubmitAsync(Guid id, SubmitInventoryDisposalRequest request, CancellationToken cancellationToken = default);
    Task<InventoryDisposalDto> DecideAsync(Guid id, DecideInventoryDisposalRequest request, CancellationToken cancellationToken = default);
    Task<InventoryDisposalDto> StageExecutionAsync(Guid id, StageInventoryDisposalExecutionRequest request, CancellationToken cancellationToken = default);
    Task<InventoryDisposalDto> CompleteAsync(Guid id, CompleteInventoryDisposalRequest request, CancellationToken cancellationToken = default);
}

public interface IInventoryDisposalReportSource
{
    Task<IReadOnlyList<InventoryDisposalDto>> GetReportSourceAsync(
        InventoryDisposalStatus? status,
        Guid? warehouseId,
        CancellationToken cancellationToken = default);
}

public class InventoryDisposalException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class InventoryDisposalAuthorizationException(string message) : Exception(message);

public sealed class InventoryDisposalNotFoundException(string message)
    : InventoryDisposalException("INV_DISPOSAL_NOT_FOUND", message);
