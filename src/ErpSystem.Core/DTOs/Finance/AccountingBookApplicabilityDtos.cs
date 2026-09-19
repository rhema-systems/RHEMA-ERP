namespace ErpSystem.Core.DTOs.Finance;

public sealed class AccountingBookApplicabilityPolicyDto
{
    public Guid Id { get; set; }
    public string PolicyCode { get; set; } = string.Empty;
    public int Version { get; set; }
    public Guid? SupersedesPolicyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public Guid PreparedByUserId { get; set; }
    public DateTime PreparedAtUtc { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? RetirementRequestedByUserId { get; set; }
    public DateTime? RetirementRequestedAtUtc { get; set; }
    public string? RetirementReason { get; set; }
    public Guid? RetirementWorkflowInstanceId { get; set; }
    public string? RetirementDecisionStatus { get; set; }
    public Guid? RetirementDecidedByUserId { get; set; }
    public DateTime? RetirementDecidedAtUtc { get; set; }
    public string? RetirementDecisionReason { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public IReadOnlyList<AccountingBookApplicabilityRuleDto> Rules { get; set; } = [];
}

public sealed class AccountingBookApplicabilityEligibleBookDto
{
    public Guid AccountingBookId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BookType { get; set; } = string.Empty;
    public string LifecycleStatus { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public bool AllowsPosting { get; set; }
    public bool InitializationReconciled { get; set; }
    public bool MappingClassificationReady { get; set; }
}

public sealed class AccountingBookApplicabilityRuleDto
{
    public Guid Id { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string OriginatingModuleCode { get; set; } = string.Empty;
    public string SourceDocumentType { get; set; } = string.Empty;
    public string PostingAction { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public IReadOnlyList<AccountingBookApplicabilityRuleBookDto> SelectedBooks { get; set; } = [];
}

public sealed class AccountingBookApplicabilityRuleBookDto
{
    public Guid AccountingBookId { get; set; }
    public string AccountingBookCode { get; set; } = string.Empty;
    public int SelectionOrder { get; set; }
}

public sealed class SaveAccountingBookApplicabilityPolicyDto
{
    public string PolicyCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? RowVersion { get; set; }
    public IReadOnlyList<SaveAccountingBookApplicabilityRuleDto> Rules { get; set; } = [];
}

public sealed class SaveAccountingBookApplicabilityRuleDto
{
    public string RuleCode { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string OriginatingModuleCode { get; set; } = string.Empty;
    public string SourceDocumentType { get; set; } = string.Empty;
    public string PostingAction { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public IReadOnlyList<Guid> AccountingBookIds { get; set; } = [];
}

public sealed class DecideAccountingBookApplicabilityPolicyDto
{
    public string Reason { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
}

public class ResolveAccountingBookApplicabilityDto
{
    public DateTime EffectiveDate { get; set; }
    public string OriginatingModuleCode { get; set; } = string.Empty;
    public string SourceDocumentType { get; set; } = string.Empty;
    public string PostingAction { get; set; } = string.Empty;
    public string? ExpectedCalculationInputHash { get; set; }
    public string? ExpectedSelectionFingerprint { get; set; }
}

public sealed class AccountingBookSelectionDto
{
    public Guid? SelectionEvidenceId { get; set; }
    public Guid? PolicyId { get; set; }
    public Guid? RuleId { get; set; }
    public int? PolicyVersion { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string OriginatingModuleCode { get; set; } = string.Empty;
    public string SourceDocumentType { get; set; } = string.Empty;
    public string PostingAction { get; set; } = string.Empty;
    public bool UsedPrimaryOnlyFallback { get; set; }
    public IReadOnlyList<AccountingBookSelectionBookDto> Books { get; set; } = [];
    public IReadOnlyList<AccountingBookSelectionBlockerDto> Blockers { get; set; } = [];
    public string CalculationInputHash { get; set; } = string.Empty;
    public string SelectionFingerprint { get; set; } = string.Empty;
}

public sealed class AccountingBookSelectionBookDto
{
    public Guid AccountingBookId { get; set; }
    public string AccountingBookCode { get; set; } = string.Empty;
    public int SelectionOrder { get; set; }
    public string AuthorityFingerprint { get; set; } = string.Empty;
}

public sealed class AccountingBookSelectionBlockerDto
{
    public string Code { get; set; } = string.Empty;
    public Guid? AccountingBookId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class FreezeAccountingBookSelectionDto : ResolveAccountingBookApplicabilityDto
{
    public string IdempotencyKey { get; set; } = string.Empty;
}
