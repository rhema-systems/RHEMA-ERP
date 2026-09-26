using System;

namespace ErpSystem.Core.DTOs.Finance
{
    public class FinanceSearchDto
    {
        public string? SearchTerm { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; } = false;
    }

    public class InvoiceSearchDto : FinanceSearchDto
    {
        public Guid? BusinessPartnerId { get; set; }
        public string? Status { get; set; }
        public decimal? MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }
        public bool? IsOverdue { get; set; }
    }

    public class AccountSearchDto : FinanceSearchDto
    {
        public string? AccountType { get; set; }
        public bool? IsActive { get; set; }
        public Guid? ParentAccountId { get; set; }
        public string? CurrencyCode { get; set; }
    }
}
