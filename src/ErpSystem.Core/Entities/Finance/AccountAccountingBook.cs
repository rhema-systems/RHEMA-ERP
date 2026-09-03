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

        /// <summary>
        /// Finance-owned detailed classification for this account in this book. Nullable only
        /// while pre-live legacy mappings are identified and migrated.
        /// </summary>
        public Guid? AccountClassificationId { get; set; }

        public bool IsEnabled { get; set; } = true;

        [MaxLength(100)]
        public string? FinancialStatementLineItem { get; set; }

        public virtual Account Account { get; set; } = null!;

        public virtual AccountingBook AccountingBook { get; set; } = null!;

        public virtual AccountClassification? AccountClassification { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
