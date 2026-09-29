using System.Text.Json.Serialization;

namespace ErpSystem.Core.DTOs.Inventory;

public sealed class InventoryItemPostingAccountsDto
{
    [JsonIgnore]
    public HashSet<string> ProvidedFields { get; } = new(StringComparer.Ordinal);

    private Guid? _inventoryAccountId;
    private Guid? _inventoryDisposalAccountId;
    public Guid? InventoryDisposalAccountId { get => _inventoryDisposalAccountId; set { _inventoryDisposalAccountId = value; ProvidedFields.Add(nameof(InventoryDisposalAccountId)); } }
    public Guid? InventoryAccountId { get => _inventoryAccountId; set { _inventoryAccountId = value; ProvidedFields.Add(nameof(InventoryAccountId)); } }
    private Guid? _inventoryOffsetAccountId;
    public Guid? InventoryOffsetAccountId { get => _inventoryOffsetAccountId; set { _inventoryOffsetAccountId = value; ProvidedFields.Add(nameof(InventoryOffsetAccountId)); } }
    private Guid? _costOfGoodsSoldAccountId;
    public Guid? CostOfGoodsSoldAccountId { get => _costOfGoodsSoldAccountId; set { _costOfGoodsSoldAccountId = value; ProvidedFields.Add(nameof(CostOfGoodsSoldAccountId)); } }
    private Guid? _salesAccountId;
    public Guid? SalesAccountId { get => _salesAccountId; set { _salesAccountId = value; ProvidedFields.Add(nameof(SalesAccountId)); } }
    private Guid? _markdownsAccountId;
    public Guid? MarkdownsAccountId { get => _markdownsAccountId; set { _markdownsAccountId = value; ProvidedFields.Add(nameof(MarkdownsAccountId)); } }
    private Guid? _salesReturnsAccountId;
    public Guid? SalesReturnsAccountId { get => _salesReturnsAccountId; set { _salesReturnsAccountId = value; ProvidedFields.Add(nameof(SalesReturnsAccountId)); } }
    private Guid? _inUseAccountId;
    public Guid? InUseAccountId { get => _inUseAccountId; set { _inUseAccountId = value; ProvidedFields.Add(nameof(InUseAccountId)); } }
    private Guid? _inServiceAccountId;
    public Guid? InServiceAccountId { get => _inServiceAccountId; set { _inServiceAccountId = value; ProvidedFields.Add(nameof(InServiceAccountId)); } }
    private Guid? _damagedAccountId;
    public Guid? DamagedAccountId { get => _damagedAccountId; set { _damagedAccountId = value; ProvidedFields.Add(nameof(DamagedAccountId)); } }
    private Guid? _varianceAccountId;
    public Guid? VarianceAccountId { get => _varianceAccountId; set { _varianceAccountId = value; ProvidedFields.Add(nameof(VarianceAccountId)); } }
    private Guid? _dropShipItemsAccountId;
    public Guid? DropShipItemsAccountId { get => _dropShipItemsAccountId; set { _dropShipItemsAccountId = value; ProvidedFields.Add(nameof(DropShipItemsAccountId)); } }
    private Guid? _purchasePriceVarianceAccountId;
    public Guid? PurchasePriceVarianceAccountId { get => _purchasePriceVarianceAccountId; set { _purchasePriceVarianceAccountId = value; ProvidedFields.Add(nameof(PurchasePriceVarianceAccountId)); } }
    private Guid? _unrealisedPurchasePriceVarianceAccountId;
    public Guid? UnrealisedPurchasePriceVarianceAccountId { get => _unrealisedPurchasePriceVarianceAccountId; set { _unrealisedPurchasePriceVarianceAccountId = value; ProvidedFields.Add(nameof(UnrealisedPurchasePriceVarianceAccountId)); } }
    private Guid? _inventoryReturnsAccountId;
    public Guid? InventoryReturnsAccountId { get => _inventoryReturnsAccountId; set { _inventoryReturnsAccountId = value; ProvidedFields.Add(nameof(InventoryReturnsAccountId)); } }
    private Guid? _assemblyVarianceAccountId;
    public Guid? AssemblyVarianceAccountId { get => _assemblyVarianceAccountId; set { _assemblyVarianceAccountId = value; ProvidedFields.Add(nameof(AssemblyVarianceAccountId)); } }
    private Guid? _standardCostRevaluationAccountId;
    public Guid? StandardCostRevaluationAccountId { get => _standardCostRevaluationAccountId; set { _standardCostRevaluationAccountId = value; ProvidedFields.Add(nameof(StandardCostRevaluationAccountId)); } }
}
