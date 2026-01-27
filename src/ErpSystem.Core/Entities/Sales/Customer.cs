using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Entities.Sales;

public class Customer : BusinessEntity
{
    [Required]
    [MaxLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string CustomerCode { get; set; } = string.Empty;

    [MaxLength(20)]
    public string CustomerType { get; set; } = "Individual"; // Individual, Corporate

    [MaxLength(100)]
    public string? ContactPerson { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(50)]
    public string? State { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(50)]
    public string? Country { get; set; }

    [MaxLength(50)]
    public string? TaxId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CreditLimit { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal OutstandingBalance { get; set; } = 0;

    public new bool IsActive { get; set; } = true;

    public DateTime? LastOrderDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation properties
    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
