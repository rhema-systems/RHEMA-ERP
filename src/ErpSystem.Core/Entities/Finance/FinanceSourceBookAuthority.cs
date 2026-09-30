using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Workflow;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Immutable source-document authority for one exact accounting book and currency coordinate.
/// A later posting may bind its original event/journal once; all other evidence is append-only.
/// </summary>
public sealed class FinanceSourceBookAuthority : TenantEntity
{
    [MaxLength(10)] public string OriginModuleCode { get; set; } = string.Empty;
    [MaxLength(100)] public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    [MaxLength(50)] public string PostingAction { get; set; } = string.Empty;
    public int AuthorityVersion { get; set; } = 1;
    public Guid? SupersedesAuthorityId { get; set; }
    public Guid? SourceWorkflowInstanceId { get; set; }
    [MaxLength(100)] public string SourceWorkflowEntityType { get; set; } = string.Empty;
    [MaxLength(30)] public string FreezeStage { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
    public Guid AccountingBookId { get; set; }
    [MaxLength(20)] public string AccountingBookCode { get; set; } = string.Empty;
    [MaxLength(3)] public string FunctionalCurrencyCode { get; set; } = string.Empty;
    [MaxLength(3)] public string TransactionCurrencyCode { get; set; } = string.Empty;
    [MaxLength(30)] public string SelectionBasis { get; set; } = string.Empty;
    [MaxLength(64)] public string AuthorityFingerprint { get; set; } = string.Empty;
    public Guid? FrozenByUserId { get; set; }
    public DateTime FrozenAtUtc { get; set; }
    public Guid? OriginalFinancePostingEventId { get; set; }
    public Guid? OriginalJournalEntryId { get; set; }
    public Guid? BoundByUserId { get; set; }
    public DateTime? BoundAtUtc { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public AccountingBook AccountingBook { get; set; } = null!;
    public FinanceSourceBookAuthority? SupersedesAuthority { get; set; }
    public WorkflowInstance? SourceWorkflowInstance { get; set; }
    public FinancePostingEvent? OriginalFinancePostingEvent { get; set; }
    public JournalEntry? OriginalJournalEntry { get; set; }
    public ICollection<FinanceSourceBookAuthorityOrigin> Origins { get; set; } = new List<FinanceSourceBookAuthorityOrigin>();
}

/// <summary>Immutable exact-source evidence inherited by a settlement or reclassification authority.</summary>
public sealed class FinanceSourceBookAuthorityOrigin : TenantEntity
{
    public Guid FinanceSourceBookAuthorityId { get; set; }
    public Guid OriginAuthorityId { get; set; }
    [MaxLength(30)] public string Role { get; set; } = string.Empty;
    public Guid OriginalFinancePostingEventId { get; set; }
    public Guid OriginalJournalEntryId { get; set; }

    public FinanceSourceBookAuthority FinanceSourceBookAuthority { get; set; } = null!;
    public FinanceSourceBookAuthority OriginAuthority { get; set; } = null!;
    public FinancePostingEvent OriginalFinancePostingEvent { get; set; } = null!;
    public JournalEntry OriginalJournalEntry { get; set; } = null!;
}

public static class FinanceSourceBookAuthorityFreezeStages
{
    public const string Submitted = "SUBMITTED";
    public const string Authorized = "AUTHORIZED";
    public const string PrePost = "PRE_POST";
    public const string LegacyPosted = "LEGACY_POSTED";
}

public static class FinanceSourceBookAuthoritySelectionBases
{
    public const string DefaultPrimary = "DEFAULT_PRIMARY";
    public const string InheritedOriginal = "INHERITED_ORIGINAL";
    public const string RetainedPostedOriginal = "RETAINED_POSTED_ORIGINAL";
}
