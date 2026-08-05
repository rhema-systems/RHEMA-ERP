using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// STAFF GROUP TRAVEL — bulk "add participants" request.
// ----------------------------------------------------------------------------
// Each selected employee becomes a Draft StaffTravelRequest linked to the group.
// Destination + travel dates are taken from the group; the fields below form the
// shared template applied to every created request.
// ============================================================================

public class AddGroupTravelParticipantsDto
{
    public Guid GroupTravelId { get; set; }

    [Required, MinLength(1, ErrorMessage = "Select at least one employee.")]
    public List<Guid> EmployeeIds { get; set; } = new();

    [Required]
    public TravelInitiatorRole InitiatedByRole { get; set; } = TravelInitiatorRole.TravelDesk;

    [Required]
    public StaffTravelType TravelType { get; set; }

    [Required]
    public StaffTravelPurpose TravelPurpose { get; set; }

    [MaxLength(1000)]
    public string? PurposeDescription { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    public StaffTravelPriority Priority { get; set; } = StaffTravelPriority.Routine;

    [Required]
    public Guid OriginCountryId { get; set; }

    [Required, MaxLength(100)]
    public string OriginCity { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal EstimatedTotalCost { get; set; }

    [Required, MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    public bool IsInternational { get; set; }
    public bool RequiresVisa { get; set; }
    public bool RequiresHealthClearance { get; set; }

    public TravelRiskLevel RiskLevel { get; set; } = TravelRiskLevel.Low;
}
