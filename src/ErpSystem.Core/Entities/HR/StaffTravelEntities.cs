using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Procurement;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.StaffTravel;

// =========================================================================
//  GROUP 1 — CORE TRAVEL REQUEST
// =========================================================================

/// <summary>Root aggregate. Every workflow, booking and expense traces back here.</summary>
public class StaffTravelRequest : TenantEntity
{
    [Required]
    [MaxLength(30)]
    public string RequestNumber { get; set; } = null!; // e.g. TR-2026-00481

    public Guid EmployeeId { get; set; }                  // FK -> Employee

    public Guid InitiatedById { get; set; }               // FK -> Employee

    public TravelInitiatorRole InitiatedByRole { get; set; }

    public StaffTravelType TravelType { get; set; }

    public StaffTravelPurpose TravelPurpose { get; set; }

    [MaxLength(1000)]
    public string? PurposeDescription { get; set; }

    public Guid? OrganizationUnitId { get; set; }         // FK -> OrganizationUnit

    public StaffTravelRequestStatus Status { get; set; }

    public StaffTravelPriority Priority { get; set; }

    public Guid DestinationCountryId { get; set; }        // FK -> Country

    [Required]
    [MaxLength(100)]
    public string DestinationCity { get; set; } = null!;

    public Guid OriginCountryId { get; set; }             // FK -> Country

    [Required]
    [MaxLength(100)]
    public string OriginCity { get; set; } = null!;

    public DateOnly TravelStartDate { get; set; }

    public DateOnly TravelEndDate { get; set; }

    public int EstimatedDurationDays { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal EstimatedTotalCost { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal? ApprovedBudget { get; set; }

    [Required]
    [Column(TypeName = "char(3)")]
    public string CurrencyCode { get; set; } = null!;

    public Guid? PolicyId { get; set; }                   // FK -> StaffTravelPolicy

    public bool IsInternational { get; set; }

    public bool RequiresVisa { get; set; }

    public bool RequiresHealthClearance { get; set; }

    public TravelRiskLevel RiskLevel { get; set; }

    public Guid? GroupTravelId { get; set; }              // FK -> StaffGroupTravel (nullable)

    public Guid? ParentRequestId { get; set; }            // FK -> StaffTravelRequest (extensions/amendments)

    [MaxLength(1000)]
    public string? AmendmentReason { get; set; }

    [MaxLength(1000)]
    public string? CancellationReason { get; set; }

    public Guid? CancelledById { get; set; }              // FK -> Employee

    public DateTime? CancelledAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    // Navigation — external aggregates
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(InitiatedById))]
    public virtual Employee InitiatedBy { get; set; } = null!;

    [ForeignKey(nameof(CancelledById))]
    public virtual Employee? CancelledBy { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    [ForeignKey(nameof(DestinationCountryId))]
    public virtual Country DestinationCountry { get; set; } = null!;

    [ForeignKey(nameof(OriginCountryId))]
    public virtual Country OriginCountry { get; set; } = null!;

    // Navigation — in-module
    [ForeignKey(nameof(PolicyId))]
    public virtual StaffTravelPolicy? Policy { get; set; }

    [ForeignKey(nameof(GroupTravelId))]
    public virtual StaffGroupTravel? GroupTravel { get; set; }

    [ForeignKey(nameof(ParentRequestId))]
    public virtual StaffTravelRequest? ParentRequest { get; set; }

    public virtual ICollection<StaffTravelRequest> ChildRequests { get; set; } = new List<StaffTravelRequest>();

    public virtual ICollection<StaffTravelRequestComment> Comments { get; set; } = new List<StaffTravelRequestComment>();

    public virtual ICollection<StaffTravelRequestAttachment> Attachments { get; set; } = new List<StaffTravelRequestAttachment>();

    public virtual ICollection<StaffTravelItinerary> Itineraries { get; set; } = new List<StaffTravelItinerary>();

    public virtual ICollection<StaffTravelApprovalInstance> ApprovalInstances { get; set; } = new List<StaffTravelApprovalInstance>();

    public virtual ICollection<StaffTravelFlightBooking> FlightBookings { get; set; } = new List<StaffTravelFlightBooking>();

    public virtual ICollection<StaffTravelHotelBooking> HotelBookings { get; set; } = new List<StaffTravelHotelBooking>();

    public virtual ICollection<StaffTravelGroundTransport> GroundTransports { get; set; } = new List<StaffTravelGroundTransport>();

    public virtual ICollection<StaffTravelCarRentalBooking> CarRentalBookings { get; set; } = new List<StaffTravelCarRentalBooking>();

    public virtual ICollection<StaffTravelExpenseClaim> ExpenseClaims { get; set; } = new List<StaffTravelExpenseClaim>();

    public virtual ICollection<StaffTravelAdvance> Advances { get; set; } = new List<StaffTravelAdvance>();

    public virtual ICollection<StaffTravelPolicyException> PolicyExceptions { get; set; } = new List<StaffTravelPolicyException>();

    public virtual ICollection<StaffTravelVisaApplication> VisaApplications { get; set; } = new List<StaffTravelVisaApplication>();

    public virtual ICollection<StaffTravelRiskAssessment> RiskAssessments { get; set; } = new List<StaffTravelRiskAssessment>();

    public virtual ICollection<StaffTravelInsurancePolicy> InsurancePolicies { get; set; } = new List<StaffTravelInsurancePolicy>();

    public virtual StaffTravelBudget? Budget { get; set; }
}

/// <summary>Parent record for coordinating multiple employees travelling together.</summary>
public class StaffGroupTravel : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string GroupName { get; set; } = null!;

    public Guid LeadEmployeeId { get; set; }              // FK -> Employee

    [MaxLength(200)]
    public string? EventName { get; set; }

    public Guid DestinationCountryId { get; set; }        // FK -> Country

    [Required]
    [MaxLength(100)]
    public string DestinationCity { get; set; } = null!;

    public DateOnly TravelStartDate { get; set; }

    public DateOnly TravelEndDate { get; set; }

    public GroupTravelStatus Status { get; set; }

    public int? MaxParticipants { get; set; }

    [ForeignKey(nameof(LeadEmployeeId))]
    public virtual Employee LeadEmployee { get; set; } = null!;

    [ForeignKey(nameof(DestinationCountryId))]
    public virtual Country DestinationCountry { get; set; } = null!;

    public virtual ICollection<StaffTravelRequest> Requests { get; set; } = new List<StaffTravelRequest>();
}

/// <summary>Threaded comments and internal notes on a travel request.</summary>
public class StaffTravelRequestComment : TenantEntity
{
    public Guid StaffTravelRequestId { get; set; }

    public Guid AuthorId { get; set; }                    // FK -> Employee

    public TravelRequestCommentType CommentType { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Body { get; set; } = null!;

    public bool IsVisibleToTraveller { get; set; }

    public Guid? ParentCommentId { get; set; }

    [ForeignKey(nameof(StaffTravelRequestId))]
    public virtual StaffTravelRequest StaffTravelRequest { get; set; } = null!;

    [ForeignKey(nameof(AuthorId))]
    public virtual Employee Author { get; set; } = null!;

    [ForeignKey(nameof(ParentCommentId))]
    public virtual StaffTravelRequestComment? ParentComment { get; set; }

    public virtual ICollection<StaffTravelRequestComment> Replies { get; set; } = new List<StaffTravelRequestComment>();
}

/// <summary>Supporting documents attached to a travel request.</summary>
public class StaffTravelRequestAttachment : TenantEntity
{
    public Guid StaffTravelRequestId { get; set; }

    [Required]
    [MaxLength(500)]
    public string FileName { get; set; } = null!;

    /// <summary>
    /// Legacy free-text location. Retained for rows written before the controlled-upload gate and
    /// no longer accepted from callers — a caller-supplied path is the injection sink the medical
    /// exam and claim documents were both fixed for. New rows carry the three DMS ids below and
    /// leave this empty.
    /// </summary>
    [MaxLength(2000)]
    public string FileUrl { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    [Required]
    [MaxLength(100)]
    public string MimeType { get; set; } = null!;

    public TravelAttachmentType AttachmentType { get; set; }

    /// <summary>Scanned controlled upload backing this attachment.</summary>
    public Guid? FileUploadRecordId { get; set; }

    /// <summary>Central-DMS record, once registered.</summary>
    public Guid? DocumentRecordId { get; set; }

    /// <summary>Central-DMS version, once registered.</summary>
    public Guid? DocumentVersionId { get; set; }

    public Guid UploadedById { get; set; }                // FK -> Employee

    public DateTime UploadedAt { get; set; }

    [ForeignKey(nameof(StaffTravelRequestId))]
    public virtual StaffTravelRequest StaffTravelRequest { get; set; } = null!;

    [ForeignKey(nameof(UploadedById))]
    public virtual Employee UploadedBy { get; set; } = null!;
}

// =========================================================================
//  GROUP 2 — ITINERARY & LEGS
// =========================================================================

public class StaffTravelItinerary : TenantEntity
{
    public Guid StaffTravelRequestId { get; set; }

    public int VersionNumber { get; set; }

    public bool IsCurrentVersion { get; set; }

    public TravelItineraryStatus Status { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = null!;

    public int TotalTravelDays { get; set; }

    public int TotalWorkingDays { get; set; }

    public int TotalWeekendDays { get; set; }

    [MaxLength(2000)]
    public string? SummaryNotes { get; set; }

    public DateTime? FinalizedAt { get; set; }

    [ForeignKey(nameof(StaffTravelRequestId))]
    public virtual StaffTravelRequest StaffTravelRequest { get; set; } = null!;

    public virtual ICollection<StaffTravelItineraryLeg> Legs { get; set; } = new List<StaffTravelItineraryLeg>();
}

public class StaffTravelItineraryLeg : TenantEntity
{
    public Guid StaffTravelItineraryId { get; set; }

    public int SequenceOrder { get; set; }

    public TravelItineraryLegType LegType { get; set; }

    public DateOnly LegDate { get; set; }

    [MaxLength(100)]
    public string? OriginCity { get; set; }

    public Guid? OriginCountryId { get; set; }            // FK -> Country

    [MaxLength(100)]
    public string? DestinationCity { get; set; }

    public Guid? DestinationCountryId { get; set; }       // FK -> Country

    public StaffTravelTransportMode? TransportMode { get; set; }

    public DateTime? DepartureDatetime { get; set; }

    public DateTime? ArrivalDatetime { get; set; }

    public Guid? FlightBookingId { get; set; }

    public Guid? HotelBookingId { get; set; }

    public Guid? GroundTransportId { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(StaffTravelItineraryId))]
    public virtual StaffTravelItinerary Itinerary { get; set; } = null!;

    [ForeignKey(nameof(OriginCountryId))]
    public virtual Country? OriginCountry { get; set; }

    [ForeignKey(nameof(DestinationCountryId))]
    public virtual Country? DestinationCountry { get; set; }

    [ForeignKey(nameof(FlightBookingId))]
    public virtual StaffTravelFlightBooking? FlightBooking { get; set; }

    [ForeignKey(nameof(HotelBookingId))]
    public virtual StaffTravelHotelBooking? HotelBooking { get; set; }

    [ForeignKey(nameof(GroundTransportId))]
    public virtual StaffTravelGroundTransport? GroundTransport { get; set; }

    public virtual ICollection<StaffTravelItineraryActivity> Activities { get; set; } = new List<StaffTravelItineraryActivity>();
}

public class StaffTravelItineraryActivity : TenantEntity
{
    public Guid StaffTravelItineraryLegId { get; set; }

    public StaffTravelActivityType ActivityType { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = null!;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(300)]
    public string? LocationName { get; set; }

    [MaxLength(500)]
    public string? LocationAddress { get; set; }

    public DateTime? StartDatetime { get; set; }

    public DateTime? EndDatetime { get; set; }

    [MaxLength(200)]
    public string? ContactName { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? ContactEmail { get; set; }

    [MaxLength(50)]
    [Phone]
    public string? ContactPhone { get; set; }

    public bool IsMandatory { get; set; }

    [ForeignKey(nameof(StaffTravelItineraryLegId))]
    public virtual StaffTravelItineraryLeg ItineraryLeg { get; set; } = null!;
}


// =========================================================================
//  GROUP 3 — APPROVAL WORKFLOW
// =========================================================================

public class StaffTravelApprovalWorkflowTemplate : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public StaffTravelType? TravelType { get; set; }

    public Guid? AppliesToLevelFromId { get; set; }       // FK -> StaffLevel

    public Guid? AppliesToLevelToId { get; set; }         // FK -> StaffLevel

    [Column(TypeName = "decimal(14,2)")]
    public decimal? MinBudgetThreshold { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal? MaxBudgetThreshold { get; set; }

    public bool? IsInternational { get; set; }

    public TravelRiskLevel? RiskLevel { get; set; }

    public bool IsActive { get; set; }

    [ForeignKey(nameof(AppliesToLevelFromId))]
    public virtual StaffLevel? AppliesToLevelFrom { get; set; }

    [ForeignKey(nameof(AppliesToLevelToId))]
    public virtual StaffLevel? AppliesToLevelTo { get; set; }

    public virtual ICollection<StaffTravelApprovalWorkflowStep> Steps { get; set; } = new List<StaffTravelApprovalWorkflowStep>();

    public virtual ICollection<StaffTravelApprovalInstance> Instances { get; set; } = new List<StaffTravelApprovalInstance>();
}

public class StaffTravelApprovalWorkflowStep : TenantEntity
{
    public Guid WorkflowTemplateId { get; set; }

    public int StepOrder { get; set; }

    [Required]
    [MaxLength(200)]
    public string StepName { get; set; } = null!;

    public TravelApproverType ApproverType { get; set; }

    [MaxLength(100)]
    public string? ApproverRole { get; set; }   // when ApproverType = RoleBased

    public Guid? SpecificApproverId { get; set; }               // FK -> Employee, when SpecificPerson

    public bool IsMandatory { get; set; }

    public bool CanDelegate { get; set; }

    public int? SlaHours { get; set; }

    public Guid? EscalationApproverId { get; set; }             // FK -> Employee

    [ForeignKey(nameof(WorkflowTemplateId))]
    public virtual StaffTravelApprovalWorkflowTemplate WorkflowTemplate { get; set; } = null!;

    [ForeignKey(nameof(SpecificApproverId))]
    public virtual Employee? SpecificApprover { get; set; }

    [ForeignKey(nameof(EscalationApproverId))]
    public virtual Employee? EscalationApprover { get; set; }
}

public class StaffTravelApprovalInstance : TenantEntity
{
    public Guid StaffTravelRequestId { get; set; }

    public Guid WorkflowTemplateId { get; set; }                // template the instance was created from

    public int CurrentStepOrder { get; set; }

    public TravelApprovalInstanceStatus Status { get; set; }

    public DateTime InitiatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    [ForeignKey(nameof(StaffTravelRequestId))]
    public virtual StaffTravelRequest StaffTravelRequest { get; set; } = null!;

    [ForeignKey(nameof(WorkflowTemplateId))]
    public virtual StaffTravelApprovalWorkflowTemplate WorkflowTemplate { get; set; } = null!;

    public virtual ICollection<StaffTravelApprovalDecision> Decisions { get; set; } = new List<StaffTravelApprovalDecision>();
}

public class StaffTravelApprovalDecision : TenantEntity
{
    public Guid ApprovalInstanceId { get; set; }

    public int StepOrder { get; set; }

    public Guid ApproverId { get; set; }                        // actual approver (may differ if delegated)

    public Guid? OriginalApproverId { get; set; }               // template-defined approver

    public TravelApprovalDecision Decision { get; set; }

    [MaxLength(2000)]
    public string? Comments { get; set; }

    public DateTime? DecidedAt { get; set; }

    public bool IsEscalated { get; set; }

    public DateTime? EscalatedAt { get; set; }

    public DateTime? SlaDeadline { get; set; }

    [ForeignKey(nameof(ApprovalInstanceId))]
    public virtual StaffTravelApprovalInstance ApprovalInstance { get; set; } = null!;

    [ForeignKey(nameof(ApproverId))]
    public virtual Employee Approver { get; set; } = null!;

    [ForeignKey(nameof(OriginalApproverId))]
    public virtual Employee? OriginalApprover { get; set; }
}


// =========================================================================
//  GROUP 4 — BOOKINGS
// =========================================================================

public class StaffTravelFlightBooking : TenantEntity
{
    public Guid StaffTravelRequestId { get; set; }

    [MaxLength(50)]
    public string? BookingReference { get; set; }   // PNR

    [Column(TypeName = "char(2)")]
    public string? AirlineCode { get; set; }

    [MaxLength(200)]
    public string? AirlineName { get; set; }

    public FlightCabinClass BookingClass { get; set; }

    public FlightCabinClass PolicyAllowedClass { get; set; }

    public bool ClassExceptionApproved { get; set; }

    [MaxLength(1000)]
    public string? ClassExceptionReason { get; set; }

    public TravelBookingChannel BookedBy { get; set; }

    public Guid? VendorId { get; set; }                             // FK -> Supplier (Procurement owns the vendor master)

    [Column(TypeName = "decimal(14,2)")]
    public decimal TotalFare { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal TaxesAndFees { get; set; }

    [Required]
    [Column(TypeName = "char(3)")]
    public string CurrencyCode { get; set; } = null!;

    [MaxLength(50)]
    public string? TicketNumber { get; set; }

    public TravelBookingStatus Status { get; set; }

    public DateTime? BookedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal? CancellationFee { get; set; }

    [ForeignKey(nameof(StaffTravelRequestId))]
    public virtual StaffTravelRequest StaffTravelRequest { get; set; } = null!;

    [ForeignKey(nameof(VendorId))]
    public virtual Supplier? Vendor { get; set; }

    public virtual ICollection<StaffTravelFlightSegment> Segments { get; set; } = new List<StaffTravelFlightSegment>();
}

public class StaffTravelFlightSegment : TenantEntity
{
    public Guid StaffTravelFlightBookingId { get; set; }

    public int SegmentOrder { get; set; }

    [Required]
    [MaxLength(10)]
    public string FlightNumber { get; set; } = null!;

    [Required]
    [Column(TypeName = "char(2)")]
    public string OperatingCarrier { get; set; } = null!;

    [Required]
    [Column(TypeName = "char(3)")]
    public string OriginAirport { get; set; } = null!;

    [Required]
    [Column(TypeName = "char(3)")]
    public string DestinationAirport { get; set; } = null!;

    public DateTime DepartureDatetime { get; set; }

    public DateTime ArrivalDatetime { get; set; }

    [MaxLength(10)]
    public string? DepartureTerminal { get; set; }

    [MaxLength(10)]
    public string? ArrivalTerminal { get; set; }

    public int DurationMinutes { get; set; }

    [MaxLength(50)]
    public string? AircraftType { get; set; }

    [MaxLength(10)]
    public string? SeatNumber { get; set; }

    public bool IsLayover { get; set; }

    public int? LayoverDurationMinutes { get; set; }

    [Column(TypeName = "decimal(5,1)")]
    public decimal? BaggageAllowanceKg { get; set; }

    [ForeignKey(nameof(StaffTravelFlightBookingId))]
    public virtual StaffTravelFlightBooking StaffTravelFlightBooking { get; set; } = null!;
}

public class StaffTravelHotelBooking : TenantEntity
{
    public Guid StaffTravelRequestId { get; set; }

    [MaxLength(100)]
    public string? BookingReference { get; set; }

    [Required]
    [MaxLength(300)]
    public string HotelName { get; set; } = null!;

    [MaxLength(200)]
    public string? HotelChain { get; set; }

    [MaxLength(500)]
    public string? HotelAddress { get; set; }

    [Required]
    [MaxLength(100)]
    public string City { get; set; } = null!;

    public Guid CountryId { get; set; }                             // FK -> Country

    [Range(1, 7)]
    public short? StarRating { get; set; }

    public DateOnly CheckInDate { get; set; }

    public DateOnly CheckOutDate { get; set; }

    public int NumberOfNights { get; set; }

    [MaxLength(100)]
    public string? RoomType { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal RatePerNight { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal TotalCost { get; set; }

    [Required]
    [Column(TypeName = "char(3)")]
    public string CurrencyCode { get; set; } = null!;

    [Column(TypeName = "decimal(14,2)")]
    public decimal? PolicyMaxRatePerNight { get; set; }

    public bool RateExceptionApproved { get; set; }

    [MaxLength(1000)]
    public string? RateExceptionReason { get; set; }

    public Guid? VendorId { get; set; }                             // FK -> Supplier (Procurement owns the vendor master)

    public TravelBookingChannel BookedBy { get; set; }

    public TravelBookingStatus Status { get; set; }

    [MaxLength(1000)]
    public string? CancellationPolicy { get; set; }

    public DateTime? BookedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal? CancellationFee { get; set; }

    [ForeignKey(nameof(StaffTravelRequestId))]
    public virtual StaffTravelRequest StaffTravelRequest { get; set; } = null!;

    [ForeignKey(nameof(CountryId))]
    public virtual Country Country { get; set; } = null!;

    [ForeignKey(nameof(VendorId))]
    public virtual Supplier? Vendor { get; set; }
}

public class StaffTravelGroundTransport : TenantEntity
{
    public Guid StaffTravelRequestId { get; set; }

    public GroundTransportType TransportType { get; set; }

    /// <summary>
    /// The fleet trip reserving a company vehicle for this leg. Null for every external mode —
    /// taxi, rideshare, bus, train, metro, private hire — which stay travel-owned against a
    /// supplier.
    /// </summary>
    /// <remarks>
    /// Fleet already models a trip properly: vehicle, driver (an HR Employee FK, so the seam was
    /// half-built), origin, destination, planned window, expected mileage and cost. Recording a
    /// company-vehicle journey as free text here meant two people could be promised the same
    /// vehicle and neither system would know.
    /// </remarks>
    public Guid? FleetTripId { get; set; }

    public Guid? VendorId { get; set; }                             // FK -> Supplier (Procurement owns the vendor master)

    [MaxLength(100)]
    public string? BookingReference { get; set; }

    [MaxLength(300)]
    public string? PickupLocation { get; set; }

    [MaxLength(300)]
    public string? DropoffLocation { get; set; }

    public DateTime? PickupDatetime { get; set; }

    public DateTime? DropoffDatetime { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal? EstimatedCost { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal? ActualCost { get; set; }

    [Required]
    [Column(TypeName = "char(3)")]
    public string CurrencyCode { get; set; } = null!;

    public TravelBookingStatus Status { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(StaffTravelRequestId))]
    public virtual StaffTravelRequest StaffTravelRequest { get; set; } = null!;

    [ForeignKey(nameof(VendorId))]
    public virtual Supplier? Vendor { get; set; }
}

public class StaffTravelCarRentalBooking : TenantEntity
{
    public Guid StaffTravelRequestId { get; set; }

    public Guid? VendorId { get; set; }                             // FK -> Supplier (Procurement owns the vendor master)

    [MaxLength(100)]
    public string? BookingReference { get; set; }

    [MaxLength(300)]
    public string? PickupLocation { get; set; }

    [MaxLength(300)]
    public string? DropoffLocation { get; set; }

    public DateTime PickupDatetime { get; set; }

    public DateTime DropoffDatetime { get; set; }

    public VehicleCategory VehicleCategory { get; set; }

    [MaxLength(100)]
    public string? VehicleModel { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal DailyRate { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal TotalCost { get; set; }

    [Required]
    [Column(TypeName = "char(3)")]
    public string CurrencyCode { get; set; } = null!;

    public bool InsuranceIncluded { get; set; }

    [MaxLength(100)]
    public string? FuelPolicy { get; set; }

    public bool DriverLicenseRequired { get; set; }

    public TravelBookingStatus Status { get; set; }

    public DateTime? BookedAt { get; set; }

    [ForeignKey(nameof(StaffTravelRequestId))]
    public virtual StaffTravelRequest StaffTravelRequest { get; set; } = null!;

    [ForeignKey(nameof(VendorId))]
    public virtual Supplier? Vendor { get; set; }
}

// =========================================================================
//  GROUP 5 — FINANCE: BUDGET, EXPENSES & ADVANCES
// =========================================================================

public class StaffTravelBudget : TenantEntity
{
    public Guid StaffTravelRequestId { get; set; }

    public short BudgetYear { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal ApprovedTotal { get; set; }

    [Required]
    [Column(TypeName = "char(3)")]
    public string CurrencyCode { get; set; } = null!;

    [Column(TypeName = "decimal(14,2)")]
    public decimal FlightBudget { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal AccommodationBudget { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal PerDiemBudget { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal TransportBudget { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal MiscellaneousBudget { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal TotalCommitted { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal TotalActual { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal Variance { get; set; }

    public Guid? ApprovedById { get; set; }                         // FK -> Employee

    public DateTime? ApprovedAt { get; set; }

    [ForeignKey(nameof(StaffTravelRequestId))]
    public virtual StaffTravelRequest StaffTravelRequest { get; set; } = null!;

    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }
}

public class StaffTravelExpenseClaim : TenantEntity
{
    [Required]
    [MaxLength(30)]
    public string ClaimNumber { get; set; } = null!;

    public Guid StaffTravelRequestId { get; set; }

    public Guid EmployeeId { get; set; }                            // FK -> Employee

    public TravelClaimType ClaimType { get; set; }

    public TravelClaimStatus Status { get; set; }

    public Guid? TravelAdvanceId { get; set; }                      // FK -> StaffTravelAdvance (nullable)

    [Column(TypeName = "decimal(14,2)")]
    public decimal TotalClaimed { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal TotalApproved { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal TotalRejected { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal AdvanceDeducted { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal NetPayable { get; set; }

    [Required]
    [Column(TypeName = "char(3)")]
    public string CurrencyCode { get; set; } = null!;

    public TravelPaymentMethod? PaymentMethod { get; set; }

    [MaxLength(100)]
    public string? PaymentReference { get; set; }

    public DateTime? PaidAt { get; set; }

    public Guid? FinanceReviewedById { get; set; }                  // FK -> Employee

    public DateTime? FinanceReviewedAt { get; set; }

    public DateTime? SubmittedAt { get; set; }

    [ForeignKey(nameof(StaffTravelRequestId))]
    public virtual StaffTravelRequest StaffTravelRequest { get; set; } = null!;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(FinanceReviewedById))]
    public virtual Employee? FinanceReviewedBy { get; set; }

    [ForeignKey(nameof(TravelAdvanceId))]
    public virtual StaffTravelAdvance? TravelAdvance { get; set; }

    public virtual ICollection<StaffTravelExpenseClaimLine> Lines { get; set; } = new List<StaffTravelExpenseClaimLine>();
}

public class StaffTravelExpenseClaimLine : TenantEntity
{
    public Guid StaffTravelExpenseClaimId { get; set; }

    public TravelExpenseCategory ExpenseCategory { get; set; }

    public DateOnly ExpenseDate { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(200)]
    public string? MerchantName { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal AmountOriginal { get; set; }

    [Required]
    [Column(TypeName = "char(3)")]
    public string CurrencyOriginal { get; set; } = null!;

    [Column(TypeName = "decimal(10,6)")]
    public decimal ExchangeRate { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal AmountBaseCurrency { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal? PolicyLimit { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal? AmountApproved { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal? AmountRejected { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    public Guid? ReceiptAttachmentId { get; set; }                  // FK -> StaffTravelRequestAttachment

    public bool IsPerDiem { get; set; }

    public Guid? PerDiemRateId { get; set; }                        // FK -> StaffTravelPerDiemRate (nullable)

    public TravelExpenseLineStatus Status { get; set; }

    public Guid? ReviewedById { get; set; }                         // FK -> Employee

    public DateTime? ReviewedAt { get; set; }

    [ForeignKey(nameof(StaffTravelExpenseClaimId))]
    public virtual StaffTravelExpenseClaim ExpenseClaim { get; set; } = null!;

    [ForeignKey(nameof(ReceiptAttachmentId))]
    public virtual StaffTravelRequestAttachment? ReceiptAttachment { get; set; }

    [ForeignKey(nameof(PerDiemRateId))]
    public virtual StaffTravelPerDiemRate? PerDiemRate { get; set; }

    [ForeignKey(nameof(ReviewedById))]
    public virtual Employee? ReviewedBy { get; set; }
}

public class StaffTravelAdvance : TenantEntity
{
    [Required]
    [MaxLength(30)]
    public string AdvanceNumber { get; set; } = null!;

    public Guid StaffTravelRequestId { get; set; }

    public Guid EmployeeId { get; set; }                            // FK -> Employee

    [Column(TypeName = "decimal(14,2)")]
    public decimal RequestedAmount { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal? ApprovedAmount { get; set; }

    [Required]
    [Column(TypeName = "char(3)")]
    public string CurrencyCode { get; set; } = null!;

    public TravelAdvanceType AdvanceType { get; set; }

    public TravelAdvanceStatus Status { get; set; }

    public DateTime? DisbursedAt { get; set; }

    public DateOnly? SettlementDeadline { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal SettledAmount { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal UnsettledAmount { get; set; }

    public Guid? ApprovedById { get; set; }                         // FK -> Employee

    public Guid? DisbursedById { get; set; }                        // FK -> Employee

    [ForeignKey(nameof(StaffTravelRequestId))]
    public virtual StaffTravelRequest StaffTravelRequest { get; set; } = null!;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }

    [ForeignKey(nameof(DisbursedById))]
    public virtual Employee? DisbursedBy { get; set; }

    public virtual ICollection<StaffTravelExpenseClaim> SettlementClaims { get; set; } = new List<StaffTravelExpenseClaim>();
}

public class StaffTravelPerDiemRate : TenantEntity
{
    public Guid CountryId { get; set; }            // FK -> Country

    [MaxLength(100)]
    public string? City { get; set; }              // nullable — city-specific override

    public Guid? StaffLevelId { get; set; }        // FK -> StaffLevel

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal DailyAllowance { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal AccommodationLimit { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal MealAllowance { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal IncidentalAllowance { get; set; }

    [Required]
    [Column(TypeName = "char(3)")]
    public string CurrencyCode { get; set; } = null!;

    [Column(TypeName = "decimal(14,2)")]
    public decimal MealBreakdownBreakfast { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal MealBreakdownLunch { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal MealBreakdownDinner { get; set; }

    public bool IsActive { get; set; }

    [ForeignKey(nameof(CountryId))]
    public virtual Country Country { get; set; } = null!;

    [ForeignKey(nameof(StaffLevelId))]
    public virtual StaffLevel? StaffLevel { get; set; }
}


// =========================================================================
//  GROUP 6 — POLICY & VENDOR
// =========================================================================

public class StaffTravelPolicy : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string PolicyName { get; set; } = null!;

    public int VersionNumber { get; set; }

    public bool IsCurrentVersion { get; set; }

    public Guid? AppliesToLevelFromId { get; set; }                      // FK -> StaffLevel

    public Guid? AppliesToLevelToId { get; set; }                        // FK -> StaffLevel

    public Guid? AppliesToOrganizationUnitId { get; set; }               // FK -> OrganizationUnit

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public FlightCabinClass MaxFlightClassDomestic { get; set; }

    public FlightCabinClass MaxFlightClassInternational { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal MaxHotelRateDomestic { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal MaxHotelRateInternational { get; set; }

    public int AdvanceBookingDaysFlight { get; set; }

    public int AdvanceBookingDaysHotel { get; set; }

    public bool RequiresCheapestFare { get; set; }

    public bool PreferredVendorMandatory { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal MaxSingleTripBudget { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal MaxAnnualTravelBudget { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal ReceiptRequiredAbove { get; set; }

    public int ExpenseSubmissionDays { get; set; }

    public Guid? ApprovedById { get; set; }                         // FK -> Employee

    public DateTime? ApprovedAt { get; set; }

    [ForeignKey(nameof(AppliesToLevelFromId))]
    public virtual StaffLevel? AppliesToLevelFrom { get; set; }

    [ForeignKey(nameof(AppliesToLevelToId))]
    public virtual StaffLevel? AppliesToLevelTo { get; set; }

    [ForeignKey(nameof(AppliesToOrganizationUnitId))]
    public virtual OrganizationUnit? AppliesToOrganizationUnit { get; set; }

    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }

    public virtual ICollection<StaffTravelPolicyRule> Rules { get; set; } = new List<StaffTravelPolicyRule>();

    public virtual ICollection<StaffTravelRequest> TravelRequests { get; set; } = new List<StaffTravelRequest>();
}

public class StaffTravelPolicyRule : TenantEntity
{
    public Guid PolicyId { get; set; }

    [Required]
    [MaxLength(50)]
    public string RuleCode { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string RuleName { get; set; } = null!;

    public TravelPolicyRuleType RuleType { get; set; }

    public TravelExpenseCategory? ExpenseCategory { get; set; }

    public StaffTravelType? TravelType { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal? LimitValue { get; set; }

    [MaxLength(20)]
    public string? LimitUnit { get; set; }          // PER_DAY, PER_TRIP, PER_ITEM

    public bool ExceptionAllowed { get; set; }

    public bool ExceptionRequiresApproval { get; set; }

    public TravelPolicyViolationAction ViolationAction { get; set; }

    public bool IsActive { get; set; }

    [ForeignKey(nameof(PolicyId))]
    public virtual StaffTravelPolicy Policy { get; set; } = null!;

    public virtual ICollection<StaffTravelPolicyException> Exceptions { get; set; } = new List<StaffTravelPolicyException>();
}

public class StaffTravelPolicyException : TenantEntity
{
    public Guid StaffTravelRequestId { get; set; }

    public Guid PolicyRuleId { get; set; }

    [MaxLength(1000)]
    public string? ExceptionReason { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal? RequestedValue { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal? PolicyLimit { get; set; }

    public TravelPolicyExceptionStatus Status { get; set; }

    public Guid? ApprovedById { get; set; }                         // FK -> Employee

    public DateTime? DecidedAt { get; set; }

    [ForeignKey(nameof(StaffTravelRequestId))]
    public virtual StaffTravelRequest StaffTravelRequest { get; set; } = null!;

    [ForeignKey(nameof(PolicyRuleId))]
    public virtual StaffTravelPolicyRule PolicyRule { get; set; } = null!;

    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }
}

public class StaffTravelVendor : TenantEntity
{
    [Required]
    [MaxLength(30)]
    public string VendorCode { get; set; } = null!;

    [Required]
    [MaxLength(300)]
    public string VendorName { get; set; } = null!;

    public TravelVendorType VendorType { get; set; }

    public Guid? CountryId { get; set; }                            // FK -> Country

    [MaxLength(200)]
    [EmailAddress]
    public string? ContactEmail { get; set; }

    [MaxLength(50)]
    [Phone]
    public string? ContactPhone { get; set; }

    [MaxLength(100)]
    public string? AccountNumber { get; set; }

    public DateOnly? ContractStartDate { get; set; }

    public DateOnly? ContractEndDate { get; set; }

    public bool IsPreferred { get; set; }

    public bool IsActive { get; set; }

    [Range(0, 5)]
    [Column(TypeName = "decimal(3,1)")]
    public decimal? Rating { get; set; }

    [MaxLength(200)]
    public string? PaymentTerms { get; set; }

    [ForeignKey(nameof(CountryId))]
    public virtual Country? Country { get; set; }
}

// =========================================================================
//  GROUP 7 — COMPLIANCE & SAFETY
// =========================================================================

public class StaffTravelDocument : TenantEntity
{
    public Guid EmployeeId { get; set; }                            // FK -> Employee

    public TravelDocumentType DocumentType { get; set; }

    [Required]
    [MaxLength(100)]
    public string DocumentNumber { get; set; } = null!; // encrypted at rest

    public Guid IssuingCountryId { get; set; }                      // FK -> Country

    public DateOnly? IssueDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public bool IsPrimary { get; set; }

    public bool IsVerified { get; set; }

    public Guid? VerifiedById { get; set; }                         // FK -> Employee

    public DateTime? VerifiedAt { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(IssuingCountryId))]
    public virtual Country IssuingCountry { get; set; } = null!;

    [ForeignKey(nameof(VerifiedById))]
    public virtual Employee? VerifiedBy { get; set; }
}

public class StaffTravelVisaRequirement : TenantEntity
{
    public Guid PassportCountryId { get; set; }                     // FK -> Country

    public Guid DestinationCountryId { get; set; }                  // FK -> Country

    public VisaRequirementType VisaRequirementType { get; set; }

    [MaxLength(100)]
    public string? VisaCategory { get; set; }

    public int? MaxStayDays { get; set; }

    public int? ProcessingDays { get; set; }

    [MaxLength(2000)]
    public string? OfficialSourceUrl { get; set; }

    public DateOnly? LastVerifiedAt { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(PassportCountryId))]
    public virtual Country PassportCountry { get; set; } = null!;

    [ForeignKey(nameof(DestinationCountryId))]
    public virtual Country DestinationCountry { get; set; } = null!;
}

public class StaffTravelVisaApplication : TenantEntity
{
    public Guid StaffTravelRequestId { get; set; }

    public Guid EmployeeId { get; set; }                            // FK -> Employee

    public Guid DestinationCountryId { get; set; }                  // FK -> Country

    [MaxLength(100)]
    public string? VisaType { get; set; }

    public VisaApplicationStatus Status { get; set; }

    public DateOnly? SubmittedDate { get; set; }

    public DateOnly? ApprovedDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    [MaxLength(100)]
    public string? VisaNumber { get; set; }        // encrypted

    [Column(TypeName = "decimal(14,2)")]
    public decimal? ProcessingFee { get; set; }

    [Column(TypeName = "char(3)")]
    public string? CurrencyCode { get; set; }

    public Guid? VendorId { get; set; }                             // FK -> Supplier (Procurement owns the vendor master)

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(StaffTravelRequestId))]
    public virtual StaffTravelRequest StaffTravelRequest { get; set; } = null!;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(DestinationCountryId))]
    public virtual Country DestinationCountry { get; set; } = null!;

    [ForeignKey(nameof(VendorId))]
    public virtual Supplier? Vendor { get; set; }
}

public class StaffTravelRiskAssessment : TenantEntity
{
    public Guid StaffTravelRequestId { get; set; }

    public Guid DestinationCountryId { get; set; }                  // FK -> Country

    [MaxLength(100)]
    public string? DestinationCity { get; set; }

    public TravelRiskLevel RiskLevel { get; set; }

    public TravelRiskCategory RiskCategory { get; set; }

    [MaxLength(200)]
    public string? AssessmentSource { get; set; }  // UN, UK FCO, US State Dept

    [MaxLength(2000)]
    public string? AssessmentSummary { get; set; }

    public bool MitigationRequired { get; set; }

    [MaxLength(2000)]
    public string? MitigationNotes { get; set; }

    public bool DutyOfCareBriefingSent { get; set; }

    public bool EmployeeAcknowledged { get; set; }

    public DateTime? AcknowledgedAt { get; set; }

    public Guid? AssessedById { get; set; }                         // FK -> Employee

    public DateTime? AssessedAt { get; set; }

    public DateOnly? ValidUntil { get; set; }

    [ForeignKey(nameof(StaffTravelRequestId))]
    public virtual StaffTravelRequest StaffTravelRequest { get; set; } = null!;

    [ForeignKey(nameof(DestinationCountryId))]
    public virtual Country DestinationCountry { get; set; } = null!;

    [ForeignKey(nameof(AssessedById))]
    public virtual Employee? AssessedBy { get; set; }
}

public class StaffTravelAlert : TenantEntity
{
    public TravelAlertType AlertType { get; set; }

    public TravelAlertSeverity Severity { get; set; }

    public Guid CountryId { get; set; }                             // FK -> Country

    [MaxLength(100)]
    public string? City { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = null!;

    [MaxLength(2000)]
    public string? Body { get; set; }

    [MaxLength(200)]
    public string? Source { get; set; }

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; }

    [ForeignKey(nameof(CountryId))]
    public virtual Country Country { get; set; } = null!;

    public virtual ICollection<StaffTravelAlertNotification> Notifications { get; set; } = new List<StaffTravelAlertNotification>();
}

public class StaffTravelAlertNotification : TenantEntity
{
    public Guid TravelAlertId { get; set; }

    public Guid StaffTravelRequestId { get; set; }

    public Guid EmployeeId { get; set; }                            // FK -> Employee

    public DateTime? NotificationSentAt { get; set; }

    public bool IsAcknowledged { get; set; }

    public DateTime? AcknowledgedAt { get; set; }

    [ForeignKey(nameof(TravelAlertId))]
    public virtual StaffTravelAlert TravelAlert { get; set; } = null!;

    [ForeignKey(nameof(StaffTravelRequestId))]
    public virtual StaffTravelRequest StaffTravelRequest { get; set; } = null!;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
}

public class StaffTravelInsurancePolicy : TenantEntity
{
    public Guid StaffTravelRequestId { get; set; }

    public Guid? VendorId { get; set; }                             // FK -> Supplier (Procurement owns the vendor master)

    [MaxLength(100)]
    public string? PolicyNumber { get; set; }

    public TravelInsuranceType InsuranceType { get; set; }

    public TravelInsuranceCoverageType CoverageType { get; set; }

    public DateOnly CoverageStart { get; set; }

    public DateOnly CoverageEnd { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal SumInsured { get; set; }

    [Required]
    [Column(TypeName = "char(3)")]
    public string CurrencyCode { get; set; } = null!;

    [Column(TypeName = "decimal(14,2)")]
    public decimal Premium { get; set; }

    [MaxLength(200)]
    public string? EmergencyContact { get; set; }  // 24h emergency line

    [ForeignKey(nameof(StaffTravelRequestId))]
    public virtual StaffTravelRequest StaffTravelRequest { get; set; } = null!;

    [ForeignKey(nameof(VendorId))]
    public virtual Supplier? Vendor { get; set; }
}

public class StaffTravelHealthRequirement : TenantEntity
{
    public Guid CountryId { get; set; }                             // FK -> Country

    public TravelHealthRequirementType RequirementType { get; set; }

    [Required]
    [MaxLength(200)]
    public string RequirementName { get; set; } = null!; // e.g. "Yellow Fever Vaccine"

    public bool IsMandatory { get; set; }

    public int? ValidityDays { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public bool IsActive { get; set; }

    [ForeignKey(nameof(CountryId))]
    public virtual Country Country { get; set; } = null!;
}


// =========================================================================
//  GROUP 8 — CONFIGURATION
// =========================================================================

public class StaffTravelCurrencyExchangeRate : TenantEntity
{
    [Required]
    [Column(TypeName = "char(3)")]
    public string FromCurrency { get; set; } = null!;

    [Required]
    [Column(TypeName = "char(3)")]
    public string ToCurrency { get; set; } = null!;

    [Column(TypeName = "decimal(18,8)")]
    public decimal Rate { get; set; }

    public DateOnly RateDate { get; set; }

    [MaxLength(100)]
    public string? RateSource { get; set; }        // ECB, XE, INTERNAL

    public bool IsOfficial { get; set; }
}

// =========================================================================
//  GROUP 9 — REMINDER ENGINE (slice 5a)
// =========================================================================

/// <summary>One execution of the staff-travel reminder sweep.</summary>
public class StaffTravelReminderRun : TenantEntity
{
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>"Scheduled" (background service) or "Manual" (run-now endpoint).</summary>
    [MaxLength(20)]
    public string Trigger { get; set; } = "Scheduled";

    public Guid? TriggeredByUserId { get; set; }

    public int RemindersQueued { get; set; }

    public virtual ICollection<StaffTravelReminderDispatchLog> DispatchLogs { get; set; }
        = new List<StaffTravelReminderDispatchLog>();
}

/// <summary>
/// One reminder actually dispatched by a travel sweep.
/// </summary>
/// <remarks>
/// <para>The unique (TenantId, DedupeKey) index is the send-once guarantee: a key encodes the item,
/// the reminder kind, the date it is about and the escalation tier reached, so each rung fires
/// exactly once — and moving a date re-arms the ladder, because it produces fresh keys.</para>
///
/// <para>⚠ Nothing here carries a passport number, a visa number, or an amount. A reminder travels
/// further than the record it is about — into notification lists and email — and "your passport
/// expires on the 3rd" is actionable without publishing the number itself. The same reasoning
/// governs the travel notification templates and the workflow display resolver for this area.</para>
/// </remarks>
public class StaffTravelReminderDispatchLog : TenantEntity
{
    public Guid RunId { get; set; }

    [ForeignKey(nameof(RunId))]
    public virtual StaffTravelReminderRun Run { get; set; } = null!;

    /// <summary>
    /// Machine kind: "TravelDocumentExpiring", "VisaExpiring", "AdvanceSettlementOverdue",
    /// "TripDeparting".
    /// </summary>
    [MaxLength(60)]
    public string Kind { get; set; } = string.Empty;

    /// <summary>Human label for the swept item, e.g. "Passport", "Travel advance".</summary>
    [MaxLength(100)]
    public string ItemType { get; set; } = string.Empty;

    /// <summary>Id of the swept record. No FK — the target table varies by kind.</summary>
    public Guid EntityId { get; set; }

    /// <summary>What the notification shows: a request number or document type, and nothing more.</summary>
    [MaxLength(250)]
    public string Reference { get; set; } = string.Empty;

    public DateTime? DueDate { get; set; }

    /// <summary>Days remaining at dispatch time; negative when overdue.</summary>
    public int DaysRemaining { get; set; }

    /// <summary>0 for a due-soon rung; 1, 2 or 3 for an overdue escalation tier.</summary>
    public int EscalationTier { get; set; }

    [Required]
    [MaxLength(300)]
    public string DedupeKey { get; set; } = string.Empty;
}
