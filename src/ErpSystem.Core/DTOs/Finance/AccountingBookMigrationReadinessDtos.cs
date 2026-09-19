namespace ErpSystem.Core.DTOs.Finance;

public sealed class AccountingBookMigrationReadinessDto
{
    public bool IsReady { get; set; }
    public int AccountCount { get; set; }
    public int EnabledMappingCount { get; set; }
    public IReadOnlyList<AccountingBookMigrationBlockerDto> Blockers { get; set; } = Array.Empty<AccountingBookMigrationBlockerDto>();
}

public sealed class AccountingBookMigrationBlockerDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? AccountId { get; set; }
    public string? AccountCode { get; set; }
    public Guid? AccountingBookId { get; set; }
    public string? AccountingBookCode { get; set; }
}
