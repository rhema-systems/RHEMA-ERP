using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>Finance-owned, effective-dated authority for selecting full accounting books.</summary>
public sealed class AccountingBookApplicabilityPolicy : TenantEntity
{
    [MaxLength(30)] public string PolicyCode { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public Guid? SupersedesPolicyId { get; set; }
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    [MaxLength(500)] public string? Description { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public AccountingBookApplicabilityPolicyStatus PolicyStatus { get; set; } = AccountingBookApplicabilityPolicyStatus.Draft;
    [MaxLength(500)] public string Reason { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public Guid PreparedByUserId { get; set; }
    public DateTime PreparedAtUtc { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    [MaxLength(500)] public string? DecisionReason { get; set; }
    public Guid? RetiredByUserId { get; set; }
    public DateTime? RetiredAtUtc { get; set; }
    public Guid? RetirementRequestedByUserId { get; set; }
    public DateTime? RetirementRequestedAtUtc { get; set; }
    [MaxLength(500)] public string? RetirementReason { get; set; }
    public Guid? RetirementWorkflowInstanceId { get; set; }
    [MaxLength(20)] public string? RetirementDecisionStatus { get; set; }
    public Guid? RetirementDecidedByUserId { get; set; }
    public DateTime? RetirementDecidedAtUtc { get; set; }
    [MaxLength(500)] public string? RetirementDecisionReason { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public AccountingBookApplicabilityPolicy? SupersedesPolicy { get; set; }
    public ICollection<AccountingBookApplicabilityRule> Rules { get; set; } = new List<AccountingBookApplicabilityRule>();
    public ICollection<AccountingBookSelectionEvidence> SelectionEvidence { get; set; } = new List<AccountingBookSelectionEvidence>();
}

public sealed class AccountingBookApplicabilityRule : TenantEntity
{
    public Guid AccountingBookApplicabilityPolicyId { get; set; }
    [MaxLength(40)] public string RuleCode { get; set; } = string.Empty;
    public int Priority { get; set; }
    [MaxLength(40)] public string OriginatingModuleCode { get; set; } = string.Empty;
    [MaxLength(80)] public string SourceDocumentType { get; set; } = string.Empty;
    [MaxLength(60)] public string PostingAction { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public AccountingBookApplicabilityPolicy Policy { get; set; } = null!;
    public ICollection<AccountingBookApplicabilityRuleBook> SelectedBooks { get; set; } = new List<AccountingBookApplicabilityRuleBook>();
}

public sealed class AccountingBookApplicabilityRuleBook : TenantEntity
{
    public Guid AccountingBookApplicabilityRuleId { get; set; }
    public Guid AccountingBookId { get; set; }
    public int SelectionOrder { get; set; }
    [MaxLength(20)] public string AccountingBookCodeSnapshot { get; set; } = string.Empty;
    public AccountingBookApplicabilityRule Rule { get; set; } = null!;
    public AccountingBook AccountingBook { get; set; } = null!;
}

/// <summary>Immutable non-economic evidence that one approved selection was consumed.</summary>
public sealed class AccountingBookSelectionEvidence : TenantEntity
{
    public Guid? AccountingBookApplicabilityPolicyId { get; set; }
    public Guid? AccountingBookApplicabilityRuleId { get; set; }
    public int? PolicyVersion { get; set; }
    public DateTime EffectiveDate { get; set; }
    [MaxLength(40)] public string OriginatingModuleCode { get; set; } = string.Empty;
    [MaxLength(80)] public string SourceDocumentType { get; set; } = string.Empty;
    [MaxLength(60)] public string PostingAction { get; set; } = string.Empty;
    [MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [MaxLength(64)] public string CalculationInputHash { get; set; } = string.Empty;
    [MaxLength(64)] public string SelectionFingerprint { get; set; } = string.Empty;
    public Guid FrozenByUserId { get; set; }
    public DateTime FrozenAtUtc { get; set; }
    public AccountingBookApplicabilityPolicy? Policy { get; set; }
    public AccountingBookApplicabilityRule? Rule { get; set; }
    public ICollection<AccountingBookSelectionEvidenceBook> Books { get; set; } = new List<AccountingBookSelectionEvidenceBook>();
}

public sealed class AccountingBookSelectionEvidenceBook : TenantEntity
{
    public Guid AccountingBookSelectionEvidenceId { get; set; }
    public Guid AccountingBookId { get; set; }
    public int SelectionOrder { get; set; }
    [MaxLength(20)] public string AccountingBookCodeSnapshot { get; set; } = string.Empty;
    [MaxLength(64)] public string AuthorityFingerprint { get; set; } = string.Empty;
    public AccountingBookSelectionEvidence Evidence { get; set; } = null!;
    public AccountingBook AccountingBook { get; set; } = null!;
}
