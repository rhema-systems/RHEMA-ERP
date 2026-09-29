using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.DTOs.Finance
{
    // ── Lease Contract DTOs ──────────────────────────────────────────────

    public class CreateLeaseContractDto
    {
        public string ContractNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Guid LessorId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal MonthlyPaymentAmount { get; set; }
        public PaymentFrequency PaymentFrequency { get; set; } = PaymentFrequency.Monthly;
        public decimal AnnualDiscountRate { get; set; }
    }

    public class LeaseContractListDto
    {
        public Guid Id { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? LessorName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal MonthlyPaymentAmount { get; set; }
        public decimal PresentValue { get; set; }
        public LeaseStatus Status { get; set; }
        public Guid? RouAssetId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class LeaseContractDetailDto : LeaseContractListDto
    {
        public Guid LessorId { get; set; }
        public PaymentFrequency PaymentFrequency { get; set; }
        public decimal AnnualDiscountRate { get; set; }
        public int TotalPeriods { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
        public Guid? ActivationWorkflowInstanceId { get; set; }
        public Guid? ActivationSubmittedByUserId { get; set; }
        public DateTime? ActivationSubmittedAtUtc { get; set; }
        public Guid? ActivationApprovedByUserId { get; set; }
        public DateTime? ActivationApprovedAtUtc { get; set; }
        public List<LeaseScheduleLineDto> ScheduleLines { get; set; } = new();
        public FinanceSourceDocumentDimensionDto? RecognitionFinanceDimensions { get; set; }
    }

    public class LeaseScheduleLineDto
    {
        public Guid Id { get; set; }
        public int PeriodNumber { get; set; }
        public DateTime PeriodDate { get; set; }
        public decimal PaymentAmount { get; set; }
        public decimal InterestExpense { get; set; }
        public decimal PrincipalReduction { get; set; }
        public decimal RemainingLiability { get; set; }
        public bool IsPosted { get; set; }
        public Guid? VendorInvoiceId { get; set; }
        public string? VendorInvoiceNumber { get; set; }
        public VendorInvoiceStatus? VendorInvoiceStatus { get; set; }
        public string? VendorInvoiceApprovalStatus { get; set; }
        public Guid? VendorInvoiceJournalEntryId { get; set; }
        public decimal? VendorInvoicePaidAmount { get; set; }
        public decimal? VendorInvoiceBalanceAmount { get; set; }
        public bool CanPreparePayable { get; set; }
        public FinanceSourceDocumentDimensionDto? FinanceDimensions { get; set; }
    }

    public sealed class ActivateLeaseDto
    {
        public FinanceSourceDocumentDimensionInputDto? FinanceDimensions { get; set; }
    }

}
