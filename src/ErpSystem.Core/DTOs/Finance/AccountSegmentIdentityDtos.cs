namespace ErpSystem.Core.DTOs.Finance;

public sealed class AccountSegmentIdentityResultDto
{
    public string AccountNumber { get; set; } = string.Empty;
    public string NaturalAccountCode { get; set; } = string.Empty;
    public IReadOnlyList<AccountSegmentValueCreateDto> Values { get; set; } = [];
}

public sealed class AccountSegmentReadinessDto
{
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public bool IsReady { get; set; }
    public IReadOnlyList<string> MissingSegmentCodes { get; set; } = [];
    public IReadOnlyList<string> ExtraSegmentCodes { get; set; } = [];
    public IReadOnlyList<string> Issues { get; set; } = [];
}
