using System.Text.Json.Serialization;

namespace ErpSystem.Core.DTOs.Procurement;

/// <summary>Customer posting defaults are independent of the supplier AP defaults.</summary>
public sealed class BusinessPartnerReceivablesDefaultsDto
{
    private Guid? _defaultArAccountId;
    public Guid? DefaultArAccountId { get => _defaultArAccountId; set { _defaultArAccountId = value; ProvidedFields.Add(nameof(DefaultArAccountId)); } }
    [JsonIgnore]
    public HashSet<string> ProvidedFields { get; } = new(StringComparer.Ordinal);
    private Guid? _salesAccountId;
    public Guid? SalesAccountId { get => _salesAccountId; set { _salesAccountId = value; ProvidedFields.Add(nameof(SalesAccountId)); } }
    private Guid? _costOfSalesAccountId;
    public Guid? CostOfSalesAccountId { get => _costOfSalesAccountId; set { _costOfSalesAccountId = value; ProvidedFields.Add(nameof(CostOfSalesAccountId)); } }
    private Guid? _inventoryAccountId;
    public Guid? InventoryAccountId { get => _inventoryAccountId; set { _inventoryAccountId = value; ProvidedFields.Add(nameof(InventoryAccountId)); } }
    private Guid? _termsDiscountsTakenAccountId;
    public Guid? TermsDiscountsTakenAccountId { get => _termsDiscountsTakenAccountId; set { _termsDiscountsTakenAccountId = value; ProvidedFields.Add(nameof(TermsDiscountsTakenAccountId)); } }
    private Guid? _salesReturnsAccountId;
    public Guid? SalesReturnsAccountId { get => _salesReturnsAccountId; set { _salesReturnsAccountId = value; ProvidedFields.Add(nameof(SalesReturnsAccountId)); } }
    private Guid? _financeChargesAccountId;
    public Guid? FinanceChargesAccountId { get => _financeChargesAccountId; set { _financeChargesAccountId = value; ProvidedFields.Add(nameof(FinanceChargesAccountId)); } }
    private Guid? _writeoffAccountId;
    public Guid? WriteoffAccountId { get => _writeoffAccountId; set { _writeoffAccountId = value; ProvidedFields.Add(nameof(WriteoffAccountId)); } }
    private Guid? _overpaymentWriteoffAccountId;
    public Guid? OverpaymentWriteoffAccountId { get => _overpaymentWriteoffAccountId; set { _overpaymentWriteoffAccountId = value; ProvidedFields.Add(nameof(OverpaymentWriteoffAccountId)); } }
}
