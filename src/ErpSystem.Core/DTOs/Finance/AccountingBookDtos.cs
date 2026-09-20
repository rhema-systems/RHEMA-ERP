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
