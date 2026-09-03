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
        public bool IsActive { get; set; }
        public bool IsDefault { get; set; }
        public bool AllowsPosting { get; set; }
        public bool IsSystemDefined { get; set; }
        public int SortOrder { get; set; }
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
