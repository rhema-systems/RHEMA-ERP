namespace ErpSystem.Core.DTOs.Finance
{
    public class AccountingBookDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Purpose { get; set; } = string.Empty;
        public string BookType { get; set; } = string.Empty;
        public string LifecycleStatus { get; set; } = string.Empty;
        public string? FunctionalCurrencyCode { get; set; }
        public DateTime? EffectiveFromUtc { get; set; }
        public DateTime? EffectiveToUtc { get; set; }
        public Guid? BaseAccountingBookId { get; set; }
        public string? BaseAccountingBookCode { get; set; }
        public DateTime? ReplicationStartDate { get; set; }
        public string? ParallelOpeningMode { get; set; }
        public string? ParallelTranslationMethod { get; set; }
        public Guid? CurrencyTranslationReserveAccountId { get; set; }
        public string? CurrencyTranslationReserveAccountLabel { get; set; }
        public Guid? CurrencyRoundingAccountId { get; set; }
        public string? CurrencyRoundingAccountLabel { get; set; }
        public DateTime? InitializationStartedAtUtc { get; set; }
        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
        public bool AllowsPosting { get; set; }
        public bool IsSystemDefined { get; set; }
        public int SortOrder { get; set; }
        public string? PendingLifecycleStatus { get; set; }
        public string? PendingTransitionReason { get; set; }
        public Guid? TransitionRequestedByUserId { get; set; }
        public DateTime? TransitionRequestedAtUtc { get; set; }
        public Guid? TransitionWorkflowInstanceId { get; set; }
        public Guid? PrimaryReplacementFromBookId { get; set; }
        public DateTime? PrimaryReplacementEffectiveDate { get; set; }
        public string? PrimaryReplacementReason { get; set; }
        public Guid? PrimaryReplacementRequestedByUserId { get; set; }
        public DateTime? PrimaryReplacementRequestedAtUtc { get; set; }
        public Guid? PrimaryReplacementWorkflowInstanceId { get; set; }
        public Guid? ReversiblePrimaryDesignationId { get; set; }
        public Guid? ReversiblePrimaryDesignationPreviousBookId { get; set; }
        public DateTime? ReversiblePrimaryDesignationEffectiveDate { get; set; }
        public string? PrimaryReversalReason { get; set; }
        public Guid? PrimaryReversalRequestedByUserId { get; set; }
        public DateTime? PrimaryReversalRequestedAtUtc { get; set; }
        public Guid? PrimaryReversalWorkflowInstanceId { get; set; }
        public bool HasAccountingUse { get; set; }
        public bool ActivationReady { get; set; }
        public string? ReadinessMessage { get; set; }
        public string RowVersion { get; set; } = string.Empty;
    }

    public class CreateAccountingBookDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Purpose { get; set; } = string.Empty;
        public string BookType { get; set; } = string.Empty;
        public string? FunctionalCurrencyCode { get; set; }
        public DateTime? EffectiveFromUtc { get; set; }
        public DateTime? EffectiveToUtc { get; set; }
        public Guid? BaseAccountingBookId { get; set; }
        public DateTime? ReplicationStartDate { get; set; }
        public string? ParallelOpeningMode { get; set; }
        public string? ParallelTranslationMethod { get; set; }
        public Guid? CurrencyTranslationReserveAccountId { get; set; }
        public Guid? CurrencyRoundingAccountId { get; set; }
        public int SortOrder { get; set; }
    }

    public sealed class UpdateAccountingBookDto : CreateAccountingBookDto
    {
        public string RowVersion { get; set; } = string.Empty;
    }

    public sealed class RequestAccountingBookTransitionDto
    {
        public string TargetStatus { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string RowVersion { get; set; } = string.Empty;
    }

    public sealed class DecideAccountingBookTransitionDto
    {
        public string Reason { get; set; } = string.Empty;
        public string RowVersion { get; set; } = string.Empty;
    }

    public sealed class RequestPrimaryAccountingBookReplacementDto
    {
        public DateTime EffectiveDate { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string RowVersion { get; set; } = string.Empty;
    }

    public sealed class RequestPrimaryAccountingBookReversalDto
    {
        public string Reason { get; set; } = string.Empty;
        public string RowVersion { get; set; } = string.Empty;
    }

    public sealed class DeltaBookStructurePreparationDto
    {
        public Guid AccountingBookId { get; set; }
        public string AccountingBookCode { get; set; } = string.Empty;
        public Guid BaseAccountingBookId { get; set; }
        public string BaseAccountingBookCode { get; set; } = string.Empty;
        public int ClassificationCount { get; set; }
        public int AccountMappingCount { get; set; }
    }

    public sealed class DeltaBookCombinedReportLineDto
    {
        public Guid AccountId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public decimal BaseSignedBalance { get; set; }
        public decimal DeltaSignedBalance { get; set; }
        public decimal CombinedSignedBalance { get; set; }
    }

    public sealed class DeltaBookCombinedReportDto
    {
        public Guid DeltaAccountingBookId { get; set; }
        public string DeltaAccountingBookCode { get; set; } = string.Empty;
        public IReadOnlyList<Guid> DeltaAccountingBookIds { get; set; } = Array.Empty<Guid>();
        public IReadOnlyList<string> DeltaAccountingBookCodes { get; set; } = Array.Empty<string>();
        public Guid BaseAccountingBookId { get; set; }
        public string BaseAccountingBookCode { get; set; } = string.Empty;
        public string FunctionalCurrencyCode { get; set; } = string.Empty;
        public DateTime AsOfDate { get; set; }
        public decimal BaseTotal { get; set; }
        public decimal DeltaTotal { get; set; }
        public decimal CombinedTotal { get; set; }
        public IReadOnlyList<DeltaBookCombinedReportLineDto> Lines { get; set; } = Array.Empty<DeltaBookCombinedReportLineDto>();
    }

    public sealed class DeltaBookLedgerEntryDto
    {
        public Guid JournalEntryId { get; set; }
        public string JournalEntryNumber { get; set; } = string.Empty;
        public DateTime AccountingDate { get; set; }
        public DateTime? PostedAtUtc { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? ReferenceNumber { get; set; }
        public string SourceBookCode { get; set; } = string.Empty;
        public string Layer { get; set; } = string.Empty;
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
    }

    public sealed class DeltaBookLedgerInquiryDto
    {
        public Guid DeltaAccountingBookId { get; set; }
        public string DeltaAccountingBookCode { get; set; } = string.Empty;
        public Guid BaseAccountingBookId { get; set; }
        public string BaseAccountingBookCode { get; set; } = string.Empty;
        public string FunctionalCurrencyCode { get; set; } = string.Empty;
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public IReadOnlyList<DeltaBookLedgerEntryDto> Entries { get; set; } = Array.Empty<DeltaBookLedgerEntryDto>();
    }

    public class AccountAccountingBookDto
    {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public Guid AccountingBookId { get; set; }
        public string AccountingBookCode { get; set; } = string.Empty;
        public string AccountingBookName { get; set; } = string.Empty;
        public bool AccountingBookIsDefault { get; set; }
        public bool IsEnabled { get; set; }
        public Guid? AccountClassificationId { get; set; }
        public string? AccountClassificationCode { get; set; }
        public string? AccountClassificationName { get; set; }
        public string? AccountClassificationSystemRole { get; set; }
        public string? AccountClassificationStatus { get; set; }
        public bool IsMigrationReady { get; set; }
        public string? FinancialStatementLineItem { get; set; }
        public string RowVersion { get; set; } = string.Empty;
    }

    public class AccountAccountingBookUpdateDto
    {
        public Guid? AccountingBookId { get; set; }
        public string? AccountingBookCode { get; set; }
        public bool IsEnabled { get; set; } = true;
        /// <summary>
        /// Required for every enabled Finance-created or edited assignment. Posting producers do
        /// not supply this value; Finance resolves it from the account/book master data.
        /// </summary>
        public Guid? AccountClassificationId { get; set; }
        public string? FinancialStatementLineItem { get; set; }
        /// <summary>Required when updating an existing account/book assignment.</summary>
        public string? RowVersion { get; set; }
    }
}
