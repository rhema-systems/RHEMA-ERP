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

    /// <summary>
    /// The Finance (Sales) customer this client is billed as (lane 8, slice 6). Null means the
    /// client's invoices stay HR-side: the AR hand-off records them Skipped until it is set.
    /// Not a navigation: the customer is Finance's row, read through <c>api/hr/customers</c>.
    /// </summary>
    public Guid? FinanceCustomerId { get; set; }

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
    public virtual ICollection<ConsultantClientContact> Contacts { get; set; } = new List<ConsultantClientContact>();
}

// =========================================================================
// ConsultantClientContact
// Links a main-scheme Identity account (ConsultantClient role) to the
// client organisation it may confirm timesheets for. Replaced the bespoke
// ConsultantClientPortalAccount 2026-08-31 (dropped with zero rows) when
// the PortalBearer portal was retired — Identity now owns every credential
// concern (password, lockout, confirmation), and this row is purely the
// authorisation anchor: no contact row, no client access. Invite-only;
// a contact serving several clients holds one row per client.
// =========================================================================

public class ConsultantClientContact : TenantEntity
{
    [Required]
    public Guid ConsultantClientId { get; set; }

    [ForeignKey(nameof(ConsultantClientId))]
    public virtual ConsultantClient ConsultantClient { get; set; } = null!;

    /// <summary>The Identity account this contact signs in with.</summary>
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual ApplicationUser User { get; set; } = null!;

    [MaxLength(200)]
    public string? ContactName { get; set; }

    [MaxLength(100)]
    public string? ContactRole { get; set; }

    /// <summary>Deactivated contacts keep their history but lose portal access to this client.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>The HR employee who sent the invite — the actor, from the token, never the body.</summary>
    public Guid? InvitedById { get; set; }

    [ForeignKey(nameof(InvitedById))]
    public virtual Employee? InvitedBy { get; set; }

    public DateTime? InvitedAtUtc { get; set; }

    /// <summary>
    /// When the last invite/setup email was dispatched. Backs the resend cooldown —
    /// without it, a resend endpoint is a mailbox-bombing tool aimed at a third party.
    /// </summary>
    public DateTime? LastInviteSentAtUtc { get; set; }
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
