using ErpSystem.Core.DTOs.Inventory;

namespace ErpSystem.Core.Interfaces.Inventory;

public interface IInventoryIssueFinanceAssetService
{
    Task PostIssueAsync(Guid issueVoucherId, CancellationToken cancellationToken = default);
    Task PostReturnAsync(Guid returnVoucherId, CancellationToken cancellationToken = default);
    Task ReverseReturnAsync(Guid returnVoucherId, string reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryIssueFinanceLineageDto>> GetIssueLineageAsync(
        Guid issueVoucherId,
        CancellationToken cancellationToken = default);
}

public sealed class InventoryIssueAccountingControlException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
