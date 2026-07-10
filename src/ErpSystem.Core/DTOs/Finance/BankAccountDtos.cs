using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Finance;

#region BankAccount DTOs

public class BankAccountDto
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string? BankBranch { get; set; }
    public string Currency { get; set; } = "GHS";
    public BankAccountType AccountType { get; set; }
    public Guid? GLAccountId { get; set; }
    public string? GLAccountNumber { get; set; }
    public string? GLAccountName { get; set; }
    public decimal CurrentBalance { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal OpeningBalance { get; set; }
    public bool IsActive { get; set; }
    public DateTime OpeningDate { get; set; }
    public DateTime? ClosingDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

public class CreateBankAccountDto
{
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string? BankBranch { get; set; }
    public string Currency { get; set; } = "GHS";
    public BankAccountType AccountType { get; set; }
    public Guid? GLAccountId { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal? OpeningBalanceExchangeRate { get; set; }
    public DateTime OpeningDate { get; set; }
    public string? Notes { get; set; }
}

public class UpdateBankAccountDto
{
    public string AccountName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string? BankBranch { get; set; }
    public Guid? GLAccountId { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

public class BankAccountBalanceDto
{
    public Guid BankAccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; }
    public decimal AvailableBalance { get; set; }
    public string Currency { get; set; } = "GHS";
    public DateTime AsOfDate { get; set; }
}

public class CashPositionSummaryDto
{
    public decimal TotalBalance { get; set; }
    public string Currency { get; set; } = "GHS";
    public int AccountCount { get; set; }
    public List<CashPositionByAccountTypeDto> ByAccountType { get; set; } = new();
    public List<CashPositionByCurrencyDto> ByCurrency { get; set; } = new();
}

public class CashPositionByAccountTypeDto
{
    public BankAccountType Type { get; set; }
    public decimal Balance { get; set; }
    public int Count { get; set; }
}

public class CashPositionByCurrencyDto
{
    public string Currency { get; set; } = "GHS";
    public decimal Balance { get; set; }
    public int Count { get; set; }
}

#endregion
