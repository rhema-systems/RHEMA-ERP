using ErpSystem.Core.Finance.Integration;

namespace ErpSystem.Core.DTOs.Finance;

/// <summary>
/// Additive server-to-server envelope. The producer owns approval and source facts; Finance owns
/// account/dimension validation, canonical sets, frozen evidence, idempotency and GL persistence.
/// </summary>
public abstract class FinanceExternalPostingEnvelopeBaseDto
{
    public FinanceExternalProducerContractId ContractId { get; set; }
    public Guid TenantId { get; set; }
    public Guid SourceDocumentId { get; set; }
    public string SourceDocumentReference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime PostingDate { get; set; }
    public string PostingAction { get; set; } = "Post";
    public string JournalType { get; set; } = "System Generated";
    public string FunctionalCurrencyCode { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public bool SourceApproved { get; set; }
    public Guid ApprovedByUserId { get; set; }
    public DateTime ApprovedAtUtc { get; set; }
    public string ApprovalReference { get; set; } = string.Empty;
    public string SourceEvidenceHash { get; set; } = string.Empty;
    public FinanceSourceDocumentDimensionInputDto? FinanceDimensions { get; set; }
    public IReadOnlyList<FinancePostingLineDto> Lines { get; set; } = Array.Empty<FinancePostingLineDto>();
}

public sealed class FinanceExternalPostingEnvelopeV2Dto : FinanceExternalPostingEnvelopeBaseDto
{
    /// <summary>
    /// Canonical accounting-book code. Producers never submit a detailed AccountClassificationId;
    /// Finance resolves classification from each account/book assignment.
    /// </summary>
    public string AccountingBookCode { get; set; } = "IFRS";
}
