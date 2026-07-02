using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Entities.Finance
{
    /// <summary>
    /// Links a GL account to an accounting book and its statement line mapping.
    /// </summary>
    public class AccountAccountingBook : TenantEntity
    {
        public Guid AccountId { get; set; }

        public Guid AccountingBookId { get; set; }

        public bool IsEnabled { get; set; } = true;

        [MaxLength(100)]
        public string? FinancialStatementLineItem { get; set; }

        public virtual Account Account { get; set; } = null!;

        public virtual AccountingBook AccountingBook { get; set; } = null!;
    }
}
