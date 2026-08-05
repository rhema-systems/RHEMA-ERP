using ErpSystem.Core.DTOs.Inventory;

namespace ErpSystem.Core.Interfaces.Inventory;

public interface IInventoryReturnControlService
{
    Task<InventoryReturnVoucherDto> RequestAsync(Guid requisitionId, ReturnRequisitionDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryReturnVoucherDto>> GetAsync(Guid? requisitionId = null, CancellationToken cancellationToken = default);
    Task<InventoryReturnVoucherDto?> GetAsync(Guid voucherId, CancellationToken cancellationToken = default);
    Task<InventoryReturnVoucherDto> DecideAsync(Guid voucherId, DecideInventoryReturnVoucherRequest request, CancellationToken cancellationToken = default);
    Task<InventoryReturnVoucherDto> PostAsync(Guid voucherId, PostInventoryReturnVoucherRequest request, CancellationToken cancellationToken = default);
    Task<InventoryReturnVoucherDto> ReverseAsync(Guid voucherId, ReverseInventoryReturnVoucherRequest request, CancellationToken cancellationToken = default);
}

public sealed class InventoryReturnControlException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class InventoryReturnAuthorizationException(string message) : Exception(message);

public static class InventoryReturnReasonCodes
{
    public const string Unused = "UNUSED";
    public const string Excess = "EXCESS";
    public const string WrongItem = "WRONG_ITEM";
    public const string Defective = "DEFECTIVE";
    public const string ProjectComplete = "PROJECT_COMPLETE";
    public const string Other = "OTHER";

    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [Unused] = "Unused stock",
        [Excess] = "Excess quantity",
        [WrongItem] = "Wrong item issued",
        [Defective] = "Defective item returned",
        [ProjectComplete] = "Project or work activity completed",
        [Other] = "Other controlled return"
    };
}
