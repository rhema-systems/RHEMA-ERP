using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Entities.Estate;

public sealed class EstateGroundRentAccount : TenantEntity
{
    public Guid EstateManagedAssetId { get; set; }
    public EstateManagedAsset EstateManagedAsset { get; set; } = null!;

    public Guid CustomerBusinessPartnerId { get; set; }

    [Required, MaxLength(30)]
    public string PaymentFrequency { get; set; } = "Annual";

    [Required, MaxLength(30)]
    public string CalculationMethod { get; set; } = "ApprovedAssessment";

    public decimal AnnualAmount { get; set; }
    public decimal? RatePerAcre { get; set; }

    [Required, MaxLength(3)]
    public string CurrencyCode { get; set; } = "GHS";

    public DateTime NextDueDate { get; set; }
    public int PaymentTermsDays { get; set; } = 30;
    public int ReviewFrequencyMonths { get; set; } = 12;
    public DateTime? NextReviewDate { get; set; }

    [Required, MaxLength(30)]
    public string EscalationMethod { get; set; } = "None";

    public decimal EscalationValue { get; set; }
    public int GracePeriodDays { get; set; }

    [Required, MaxLength(40)]
    public string PenaltyMethod { get; set; } = "None";

    public decimal PenaltyValue { get; set; }
    public decimal? PenaltyCapAmount { get; set; }

    public Guid GroundRentIncomeAccountId { get; set; }
    public Account? GroundRentIncomeAccount { get; set; }

    public bool AutoPostInvoices { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "Active";

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public ICollection<EstateGroundRentCharge> Charges { get; set; } = new List<EstateGroundRentCharge>();
    public ICollection<EstateGroundRentReview> Reviews { get; set; } = new List<EstateGroundRentReview>();
}

public sealed class EstateGroundRentCharge : TenantEntity
{
    public Guid GroundRentAccountId { get; set; }
    public EstateGroundRentAccount GroundRentAccount { get; set; } = null!;

    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime DueDate { get; set; }
    public decimal BaseAmount { get; set; }

    public Guid? FinanceInvoiceId { get; set; }

    [MaxLength(50)]
    public string? FinanceInvoiceNumber { get; set; }

    public decimal PenaltyAmount { get; set; }
    public Guid? PenaltyInvoiceId { get; set; }

    [MaxLength(50)]
    public string? PenaltyInvoiceNumber { get; set; }

    [Required, MaxLength(30)]
    public string Status { get; set; } = "Invoiced";

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public sealed class EstateGroundRentReview : TenantEntity
{
    public Guid GroundRentAccountId { get; set; }
    public EstateGroundRentAccount GroundRentAccount { get; set; } = null!;

    public DateTime EffectiveDate { get; set; }
    public decimal PreviousAnnualAmount { get; set; }
    public decimal NewAnnualAmount { get; set; }
    public decimal? PreviousRatePerAcre { get; set; }
    public decimal? NewRatePerAcre { get; set; }

    [Required, MaxLength(30)]
    public string EscalationMethod { get; set; } = "None";

    public decimal EscalationValue { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
