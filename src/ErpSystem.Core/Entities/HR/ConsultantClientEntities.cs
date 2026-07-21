using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.HR.StaffAttendance;

namespace ErpSystem.Core.Entities.HR;

// =========================================================================
// ConsultantClient
// Represents an external client organisation that your company places
// consultants with. Acts as the billing target for ConsultantTimesheets
// and TimesheetInvoices.
// =========================================================================

public class ConsultantClient : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string ClientName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string ClientCode { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Industry { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    // ── Primary Contact ───────────────────────────────────────────────────

    [MaxLength(200)]
    public string? PrimaryContactName { get; set; }

    [MaxLength(100)]
    [EmailAddress]
    public string? PrimaryContactEmail { get; set; }

    [MaxLength(50)]
    public string? PrimaryContactPhone { get; set; }

    // ── Address ───────────────────────────────────────────────────────────

    [MaxLength(500)]
    public string? AddressLine1 { get; set; }

    [MaxLength(500)]
    public string? AddressLine2 { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? Region { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    public Guid? CountryId { get; set; }

    // ── Billing Contact ───────────────────────────────────────────────────

    [MaxLength(200)]
    public string? BillingContactName { get; set; }

    [MaxLength(100)]
    [EmailAddress]
    public string? BillingContactEmail { get; set; }

    [MaxLength(50)]
    public string? BillingContactPhone { get; set; }

    [MaxLength(50)]
    public string? TaxIdentificationNumber { get; set; }

    /// <summary>Default billing currency for this client (ISO 4217 code, e.g. "GHS", "USD").</summary>
    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    /// <summary>Standard payment terms in days (e.g. 30 = Net 30). Null = no default set.</summary>
    public int? DefaultPaymentTermsDays { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    [ForeignKey(nameof(CountryId))]
    public virtual Country? Country { get; set; }

    public virtual ICollection<ClientEngagement> Engagements { get; set; } = new List<ClientEngagement>();
    public virtual ICollection<ConsultantTimesheet> Timesheets { get; set; } = new List<ConsultantTimesheet>();
    public virtual ICollection<TimesheetInvoice> Invoices { get; set; } = new List<TimesheetInvoice>();
    public virtual ICollection<ConsultantClientPortalAccount> PortalAccounts { get; set; } = new List<ConsultantClientPortalAccount>();
}

// =========================================================================
// ConsultantClientPortalAccount
// Lightweight auth for the external consultant client portal.
// Linked to a ConsultantClient organisation for timesheet approval access.
// =========================================================================

public class ConsultantClientPortalAccount : TenantEntity
{
    [Required, MaxLength(200), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public bool IsEmailVerified { get; set; }

    [MaxLength(512)]
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationExpiry { get; set; }

    [MaxLength(512)]
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpiry { get; set; }

    /// <summary>HR invite setup token — set until the contact completes initial password setup.</summary>
    [MaxLength(512)]
    public string? AccountSetupToken { get; set; }
    public DateTime? AccountSetupExpiry { get; set; }

    public DateTime? LastLoginAt { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedOutUntil { get; set; }

    public bool IsActive { get; set; } = true;

    [Required]
    public Guid ConsultantClientId { get; set; }

    [ForeignKey(nameof(ConsultantClientId))]
    public virtual ConsultantClient ConsultantClient { get; set; } = null!;

    [MaxLength(200)]
    public string? ContactName { get; set; }

    [MaxLength(100)]
    public string? ContactRole { get; set; }
}

// =========================================================================
// ClientEngagement
// A specific placement contract between a ConsultantClient and one of
// your employees (the consultant). Carries billing rate, cycle, and
// contract dates. ConsultantTimesheets reference this for rate lookups
// and grouping.
// =========================================================================

public class ClientEngagement : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string EngagementCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public Guid ClientId { get; set; }

    /// <summary>The primary consultant placed under this engagement.</summary>
    [Required]
    public Guid ConsultantId { get; set; }

    public DateOnly StartDate { get; set; }

    /// <summary>Null = open-ended engagement.</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>Date the engagement was actually completed (may differ from EndDate).</summary>
    public DateOnly? ActualEndDate { get; set; }

    // ── Billing Terms ─────────────────────────────────────────────────────

    /// <summary>Agreed hourly rate billed to the client.</summary>
    public decimal HourlyRate { get; set; }

    /// <summary>ISO 4217 billing currency for this engagement.</summary>
    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;

    /// <summary>Cap on billable hours per week. Null = uncapped.</summary>
    public decimal? MaxHoursPerWeek { get; set; }

    /// <summary>Fixed or capped contract value. Null = time-and-materials.</summary>
    public decimal? ContractValue { get; set; }

    [MaxLength(100)]
    public string? PurchaseOrderNumber { get; set; }

    public ClientEngagementStatus Status { get; set; } = ClientEngagementStatus.Active;

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // ── Navigation ────────────────────────────────────────────────────────

    [ForeignKey(nameof(ClientId))]
    public virtual ConsultantClient Client { get; set; } = null!;

    [ForeignKey(nameof(ConsultantId))]
    public virtual Employee Consultant { get; set; } = null!;

    public virtual ICollection<ConsultantTimesheet> Timesheets { get; set; } = new List<ConsultantTimesheet>();
}
