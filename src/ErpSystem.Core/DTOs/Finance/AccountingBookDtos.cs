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
        public bool IsEnabled { get; set; }
        public string? FinancialStatementLineItem { get; set; }
    }

    public class AccountAccountingBookUpdateDto
    {
        public Guid? AccountingBookId { get; set; }
        public string? AccountingBookCode { get; set; }
        public bool IsEnabled { get; set; } = true;
        public string? FinancialStatementLineItem { get; set; }
    }
}
