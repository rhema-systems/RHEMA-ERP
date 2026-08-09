namespace ErpSystem.Core.DTOs.Estate;

public sealed record EstateGroundRentOptionsDto(
    IReadOnlyList<EstateGroundRentAssetOptionDto> Assets,
    IReadOnlyList<EstateGroundRentIncomeAccountOptionDto> IncomeAccounts);

public sealed record EstateGroundRentAssetOptionDto(
    Guid Id,
    string AssetCode,
    string Name,
    string? Location,
    decimal? AreaAcres,
    Guid? CustomerBusinessPartnerId,
    string? CustomerName,
    decimal? ApprovedAnnualGroundRent,
    decimal? ApprovedRatePerAcre,
    string CurrencyCode,
    bool HasGroundRentAccount);

public sealed record EstateGroundRentIncomeAccountOptionDto(
    Guid Id,
    string AccountNumber,
    string AccountName,
    string CurrencyCode);

public sealed record EstateGroundRentAccountDto(
    Guid Id,
    Guid EstateManagedAssetId,
    string AssetCode,
    string AssetName,
    string? Location,
    Guid CustomerBusinessPartnerId,
    string CustomerName,
    string PaymentFrequency,
    string CalculationMethod,
    decimal AnnualAmount,
    decimal AmountPerPeriod,
    decimal? RatePerAcre,
    string CurrencyCode,
    DateTime? BillingStartDate,
    string BillingStartSource,
    bool CanGenerateInvoice,
    string? InvoiceHoldReason,
    DateTime NextDueDate,
    int PaymentTermsDays,
    int ReviewFrequencyMonths,
    DateTime? NextReviewDate,
    string EscalationMethod,
    decimal EscalationValue,
    int GracePeriodDays,
    string PenaltyMethod,
    decimal PenaltyValue,
    decimal? PenaltyCapAmount,
    Guid GroundRentIncomeAccountId,
    string GroundRentIncomeAccount,
    bool AutoPostInvoices,
    string Status,
    string? Notes,
    decimal OutstandingAmount,
    decimal ArrearsAmount,
    int OverdueChargeCount,
    IReadOnlyList<EstateGroundRentChargeDto> Charges,
    IReadOnlyList<EstateGroundRentReviewDto> Reviews);

public sealed record EstateGroundRentChargeDto(
    Guid Id,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    DateTime DueDate,
    decimal BaseAmount,
    decimal PenaltyAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    string Status,
    Guid? FinanceInvoiceId,
    string? FinanceInvoiceNumber,
    Guid? FinanceJournalEntryId,
    Guid? PenaltyInvoiceId,
    string? PenaltyInvoiceNumber,
    Guid? PenaltyJournalEntryId);

public sealed record EstateGroundRentReviewDto(
    Guid Id,
    DateTime EffectiveDate,
    decimal PreviousAnnualAmount,
    decimal NewAnnualAmount,
    decimal? PreviousRatePerAcre,
    decimal? NewRatePerAcre,
    string EscalationMethod,
    decimal EscalationValue,
    string? Notes,
    DateTime CreatedAt);

public sealed class UpsertEstateGroundRentAccountDto
{
    public Guid EstateManagedAssetId { get; set; }
    public Guid CustomerBusinessPartnerId { get; set; }
    public string PaymentFrequency { get; set; } = "Annual";
    public string CalculationMethod { get; set; } = "ApprovedAssessment";
    public decimal? AnnualAmount { get; set; }
    public decimal? RatePerAcre { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public DateTime NextDueDate { get; set; }
    public int PaymentTermsDays { get; set; } = 30;
    public int ReviewFrequencyMonths { get; set; } = 12;
    public DateTime? NextReviewDate { get; set; }
    public string EscalationMethod { get; set; } = "None";
    public decimal EscalationValue { get; set; }
    public int GracePeriodDays { get; set; }
    public string PenaltyMethod { get; set; } = "None";
    public decimal PenaltyValue { get; set; }
    public decimal? PenaltyCapAmount { get; set; }
    public Guid GroundRentIncomeAccountId { get; set; }
    public bool AutoPostInvoices { get; set; }
    public string Status { get; set; } = "Active";
    public string? Notes { get; set; }
}

public sealed class GenerateEstateGroundRentInvoiceDto
{
    public DateTime? InvoiceDate { get; set; }
    public bool AllowFutureDueDate { get; set; }
}

public sealed class ApplyEstateGroundRentReviewDto
{
    public DateTime EffectiveDate { get; set; }
    public string? EscalationMethod { get; set; }
    public decimal? EscalationValue { get; set; }
    public string? Notes { get; set; }
}

public sealed class RecordEstateGroundRentReceiptDto
{
    public string Target { get; set; } = "Base";
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public Guid? PaymentMethodId { get; set; }
    public Guid? BankAccountId { get; set; }
    public string? CheckNumber { get; set; }
    public string? TransactionReference { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public decimal ExchangeRate { get; set; } = 1m;
    public string? Notes { get; set; }
}

public sealed record EstateGroundRentActionResultDto(
    string Message,
    EstateGroundRentAccountDto Account);
