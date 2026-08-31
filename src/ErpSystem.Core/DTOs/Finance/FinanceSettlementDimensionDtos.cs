using ErpSystem.Core.Finance.Integration;

namespace ErpSystem.Core.DTOs.Finance;

public sealed record FinanceSettlementComponentAmountInput(
    FinanceSettlementComponentType ComponentType,
    decimal TransactionAmount,
    decimal FunctionalAmount);

public sealed record FinanceSettlementOriginLineInput(
    Guid OriginatingSourceLineId,
    decimal AllocationWeight,
    Guid? FinanceDimensionSetId,
    Guid? FinanceDimensionSnapshotId);

/// <summary>
/// Server-built adapter input. Browser payloads must not choose origin snapshots, canonical sets,
/// route provenance, allocation identity, or exchange-rate evidence.
/// </summary>
public sealed record FinanceSettlementAllocationInput(
    Guid SettlementSourceLineId,
    Guid? SettlementAllocationId,
    Guid? OriginatingDocumentId,
    string TransactionCurrencyCode,
    Guid? ExchangeRateId,
    decimal ExchangeRate,
    IReadOnlyList<FinanceSettlementComponentAmountInput> Components,
    IReadOnlyList<FinanceSettlementOriginLineInput> OriginatingLines);

public sealed class FinanceSettlementDimensionComponentDto
{
    public Guid Id { get; set; }
    public Guid SettlementSourceLineId { get; set; }
    public Guid? SettlementAllocationId { get; set; }
    public Guid? OriginatingDocumentId { get; set; }
    public Guid? OriginatingSourceLineId { get; set; }
    public FinanceSettlementComponentType ComponentType { get; set; }
    public Guid? FinanceDimensionSetId { get; set; }
    public Guid? FinanceDimensionSnapshotId { get; set; }
    public string? DimensionCombination { get; set; }
    public string? DimensionHash { get; set; }
    public string TransactionCurrencyCode { get; set; } = string.Empty;
    public decimal TransactionAmount { get; set; }
    public decimal FunctionalAmount { get; set; }
    public Guid? ExchangeRateId { get; set; }
    public decimal ExchangeRate { get; set; }
    public bool IsFinalResidualRecipient { get; set; }
    public decimal RoundingResidualTransactionAmount { get; set; }
    public decimal RoundingResidualFunctionalAmount { get; set; }
    public string EvidenceHash { get; set; } = string.Empty;
    public DateTime? EvidenceFrozenAt { get; set; }
}
