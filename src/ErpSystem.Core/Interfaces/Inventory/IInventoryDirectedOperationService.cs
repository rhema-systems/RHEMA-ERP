using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;

namespace ErpSystem.Core.Interfaces.Inventory;

public interface IInventoryDirectedOperationService
{
    Task<IReadOnlyList<InventoryDirectedAssigneeDto>> GetAssigneesAsync(
        Guid? warehouseId = null, InventoryDirectedTaskType? taskType = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryDirectedSuggestionDto>> GetSuggestionsAsync(
        Guid warehouseId, InventoryDirectedTaskType? taskType = null, int take = 100,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryDirectedTaskDto>> GetTasksAsync(
        Guid? warehouseId = null, InventoryDirectedTaskType? taskType = null,
        InventoryDirectedTaskStatus? status = null, int take = 100,
        CancellationToken cancellationToken = default);
    Task<InventoryDirectedTaskDto> GetTaskAsync(Guid id, CancellationToken cancellationToken = default);
    Task<InventoryDirectedTaskDto> CreateTaskAsync(
        CreateInventoryDirectedTaskRequest request, string correlationId,
        CancellationToken cancellationToken = default);
    Task<InventoryDirectedTaskDto> StartTaskAsync(
        Guid id, StartInventoryDirectedTaskRequest request, string correlationId,
        CancellationToken cancellationToken = default);
    Task<InventoryDirectedTaskDto> ConfirmTaskAsync(
        Guid id, ConfirmInventoryDirectedTaskRequest request, string correlationId,
        CancellationToken cancellationToken = default);
    Task<InventoryDirectedTaskDto> ReconcileTaskAsync(
        Guid id, ReconcileInventoryDirectedTaskRequest request, string correlationId,
        CancellationToken cancellationToken = default);
    Task<InventoryDirectedTaskDto> CancelTaskAsync(
        Guid id, CancelInventoryDirectedTaskRequest request, string correlationId,
        CancellationToken cancellationToken = default);
}

public class InventoryDirectedOperationException : InvalidOperationException
{
    public InventoryDirectedOperationException(string code, string message) : base(message) => Code = code;
    public string Code { get; }
}

public sealed class InventoryDirectedOperationAuthorizationException : InventoryDirectedOperationException
{
    public InventoryDirectedOperationAuthorizationException(string message)
        : base("INV_DIRECTED_ACCESS_DENIED", message) { }
}

public sealed class InventoryDirectedOperationNotFoundException : InventoryDirectedOperationException
{
    public InventoryDirectedOperationNotFoundException(string message)
        : base("INV_DIRECTED_NOT_FOUND", message) { }
}

public sealed class InventoryDirectedOperationConflictException : InventoryDirectedOperationException
{
    public InventoryDirectedOperationConflictException(string code, string message) : base(code, message) { }
}
