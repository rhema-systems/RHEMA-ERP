using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Entities.Maintenance;


/// <summary>
/// Tracks technician movement and travel time
/// </summary>
public class TechnicianMovement
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid TechnicianId { get; set; }

    [Required]
    public DateTime MovementDateTime { get; set; }

    /// <summary>
    /// Starting location
    /// </summary>
    [MaxLength(200)]
    public string FromLocation { get; set; } = string.Empty;

    /// <summary>
    /// Destination location
    /// </summary>
    [MaxLength(200)]
    public string ToLocation { get; set; } = string.Empty;

    /// <summary>
    /// GPS coordinates for from location
    /// </summary>
    public double? FromLatitude { get; set; }
    public double? FromLongitude { get; set; }

    /// <summary>
    /// GPS coordinates for to location
    /// </summary>
    public double? ToLatitude { get; set; }
    public double? ToLongitude { get; set; }

    /// <summary>
    /// Reason for movement
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string MovementReason { get; set; } = string.Empty; // WorkOrder, Break, EndOfDay, Emergency

    /// <summary>
    /// Related work order if applicable
    /// </summary>
    public Guid? RelatedWorkOrderId { get; set; }

    /// <summary>
    /// Estimated travel time in minutes
    /// </summary>
    public int? EstimatedTravelMinutes { get; set; }

    /// <summary>
    /// Actual travel time in minutes
    /// </summary>
    public int? ActualTravelMinutes { get; set; }

    /// <summary>
    /// Distance traveled in miles/kilometers
    /// </summary>
    public double? DistanceTraveled { get; set; }

    /// <summary>
    /// Transportation method
    /// </summary>
    [MaxLength(50)]
    public string? TransportationMethod { get; set; }

    public Guid TenantId { get; set; }
}

/// <summary>
/// Enhanced technician scheduling with optimization
/// </summary>
public class TechnicianScheduleOptimization
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid TechnicianId { get; set; }

    [Required]
    public DateTime ScheduleDate { get; set; }

    /// <summary>
    /// Optimized work order sequence
    /// </summary>
    [Required]
    public string WorkOrderSequence { get; set; } = "[]"; // JSON array of work order IDs

    /// <summary>
    /// Total estimated time for all work orders
    /// </summary>
    public double TotalEstimatedHours { get; set; }

    /// <summary>
    /// Total estimated travel time
    /// </summary>
    public double TotalTravelHours { get; set; }

    /// <summary>
    /// Optimization score (0-100)
    /// </summary>
    public double OptimizationScore { get; set; }

    /// <summary>
    /// Optimization algorithm used
    /// </summary>
    [MaxLength(50)]
    public string OptimizationMethod { get; set; } = "Manual";

    /// <summary>
    /// Factors considered in optimization
    /// </summary>
    public string OptimizationFactors { get; set; } = "[]"; // JSON array

    /// <summary>
    /// Whether this schedule is locked/confirmed
    /// </summary>
    public bool IsLocked { get; set; } = false;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastOptimizedDate { get; set; }
    public Guid CreatedById { get; set; }
    public Guid TenantId { get; set; }
}

/// <summary>
/// Tracks technician capacity and workload
/// </summary>
public class TechnicianCapacity
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid TechnicianId { get; set; }

    [Required]
    public DateTime WeekStartDate { get; set; }

    /// <summary>
    /// Total available hours for the week
    /// </summary>
    public double AvailableHours { get; set; }

    /// <summary>
    /// Scheduled work hours
    /// </summary>
    public double ScheduledHours { get; set; }

    /// <summary>
    /// Actual worked hours
    /// </summary>
    public double ActualHours { get; set; }

    /// <summary>
    /// Overtime hours worked
    /// </summary>
    public double OvertimeHours { get; set; }

    /// <summary>
    /// Utilization percentage
    /// </summary>
    public double UtilizationPercentage { get; set; }

    /// <summary>
    /// Number of work orders assigned
    /// </summary>
    public int WorkOrdersAssigned { get; set; }

    /// <summary>
    /// Number of work orders completed
    /// </summary>
    public int WorkOrdersCompleted { get; set; }

    /// <summary>
    /// Number of emergency calls handled
    /// </summary>
    public int EmergencyCallsHandled { get; set; }

    /// <summary>
    /// Travel time for the week
    /// </summary>
    public double TravelHours { get; set; }

    /// <summary>
    /// Performance score for the week
    /// </summary>
    public double PerformanceScore { get; set; }

    public Guid TenantId { get; set; }
}

#region Enhanced Resource Management

/// <summary>
/// Maintenance tools and equipment inventory
/// </summary>
public class MaintenanceTool : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string ToolCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string Category { get; set; } = "General"; // General, Specialized, Safety, Diagnostic, etc.

    [MaxLength(100)]
    public string? Manufacturer { get; set; }

    [MaxLength(100)]
    public string? Model { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    // Availability and location
    [MaxLength(20)]
    public string Status { get; set; } = "Available"; // Available, InUse, Maintenance, OutOfService

    [MaxLength(200)]
    public string? CurrentLocation { get; set; }

    [MaxLength(200)]
    public string? HomeLocation { get; set; }

    // Maintenance and calibration
    public DateTime? LastMaintenanceDate { get; set; }
    public DateTime? NextMaintenanceDate { get; set; }
    public DateTime? LastCalibrationDate { get; set; }
    public DateTime? NextCalibrationDate { get; set; }

    // Cost and value
    [Column(TypeName = "decimal(18,2)")]
    public decimal PurchasePrice { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal CurrentValue { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal DailyRentalRate { get; set; } = 0;

    // Usage tracking
    public int TotalUsageDays { get; set; } = 0;
    public DateTime? LastUsedDate { get; set; }

    // Safety and certification requirements
    public bool RequiresCertification { get; set; } = false;
    public bool RequiresTraining { get; set; } = false;

    [MaxLength(1000)]
    public string? SafetyNotes { get; set; }

    // Documentation
    [Column(TypeName = "nvarchar(max)")]
    public string? DocumentPaths { get; set; } // JSON: manuals, certificates, etc.

    public bool IsActive { get; set; } = true;

    // Note: This entity is deprecated. Use InventoryItems with ItemType = 4 (FixedAsset) for tools instead.
    // WorkOrderTool and ToolCheckout now reference InventoryItem directly.
}

/// <summary>
/// Tool checkout/check-in tracking
/// </summary>
public class ToolCheckout : TenantEntity
{
    [Required]
    public Guid ToolId { get; set; }

    [Required]
    public Guid CheckedOutById { get; set; }

    public Guid? WorkOrderId { get; set; }
    public Guid? JobCardId { get; set; }

    [Required]
    public DateTime CheckoutDate { get; set; } = DateTime.UtcNow;

    public DateTime? ExpectedReturnDate { get; set; }
    public DateTime? ActualReturnDate { get; set; }

    public Guid? CheckedInById { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "CheckedOut"; // CheckedOut, Returned, Overdue, Lost, Damaged

    [MaxLength(1000)]
    public string? CheckoutNotes { get; set; }

    [MaxLength(1000)]
    public string? ReturnNotes { get; set; }

    // Condition tracking
    [MaxLength(20)]
    public string? ConditionOnCheckout { get; set; }

    [MaxLength(20)]
    public string? ConditionOnReturn { get; set; }

    // Damage or issues
    public bool DamageReported { get; set; } = false;

    [MaxLength(2000)]
    public string? DamageDescription { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? DamageCost { get; set; }

    // Navigation properties
    [ForeignKey(nameof(ToolId))]
    public virtual ErpSystem.Core.Entities.Inventory.InventoryItem Tool { get; set; } = null!;
    public virtual ApplicationUser CheckedOutBy { get; set; } = null!;
    public virtual ApplicationUser? CheckedInBy { get; set; }
    public virtual WorkOrder? WorkOrder { get; set; }
    public virtual JobCard? JobCard { get; set; }
}

/// <summary>
/// Links work orders to required tools
/// </summary>
public class WorkOrderTool : TenantEntity
{
    [Required]
    public Guid WorkOrderId { get; set; }

    [Required]
    public Guid ToolId { get; set; }

    [Required]
    public bool IsRequired { get; set; } = true;

    public bool IsAllocated { get; set; } = false;
    public DateTime? AllocationDate { get; set; }

    public Guid? CheckoutId { get; set; }
    public Guid? AllocationId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation properties
    public virtual WorkOrder WorkOrder { get; set; } = null!;
    [ForeignKey(nameof(ToolId))]
    public virtual ErpSystem.Core.Entities.Inventory.InventoryItem Tool { get; set; } = null!;
    public virtual ToolCheckout? Checkout { get; set; }
    public virtual ErpSystem.Core.Entities.Inventory.InventoryAllocation? Allocation { get; set; }
}

/// <summary>
/// Staff availability and scheduling for maintenance work
/// </summary>
public class MaintenanceStaffSchedule : TenantEntity
{
    [Required]
    public Guid TechnicianId { get; set; }

    [Required]
    public DateTime StartDateTime { get; set; }

    [Required]
    public DateTime EndDateTime { get; set; }

    [MaxLength(50)]
    public string ScheduleType { get; set; } = "WorkOrder"; // WorkOrder, Available, Training, Leave, Travel

    [MaxLength(20)]
    public string Status { get; set; } = "Scheduled"; // Scheduled, InProgress, Completed, Cancelled

    // Work assignment
    public Guid? WorkOrderId { get; set; }
    public Guid? JobCardId { get; set; }
    public Guid? TeamId { get; set; }

    // Location information
    [MaxLength(200)]
    public string? WorkLocation { get; set; }

    [MaxLength(200)]
    public string? Address { get; set; }

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    // Travel information
    public bool RequiresTravel { get; set; } = false;
    public DateTime? DepartureTime { get; set; }
    public DateTime? ArrivalTime { get; set; }
    public int? EstimatedTravelMinutes { get; set; }
    public int? ActualTravelMinutes { get; set; }

    // Vehicle/transportation
    public Guid? AssignedVehicleId { get; set; }

    [MaxLength(100)]
    public string? TransportationType { get; set; } // Company Vehicle, Personal Vehicle, Public Transport

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Time tracking
    public DateTime? ActualStartTime { get; set; }
    public DateTime? ActualEndTime { get; set; }

    // Navigation properties
    // TechnicianId now references ApplicationUser (Users table) instead of Employee
    public virtual WorkOrder? WorkOrder { get; set; }
    public virtual JobCard? JobCard { get; set; }
    public virtual TechnicianTeam? Team { get; set; }
    public virtual MaintenanceAsset? AssignedVehicle { get; set; }
    public virtual ICollection<MaintenanceExpense> Expenses { get; set; } = new List<MaintenanceExpense>();
}

/// <summary>
/// Transportation and travel expenses for maintenance operations
/// </summary>
public class MaintenanceExpense : TenantEntity
{
    [Required]
    public Guid WorkOrderId { get; set; }

    public Guid? ScheduleId { get; set; }
    public Guid? TechnicianId { get; set; }

    [Required]
    [MaxLength(50)]
    public string ExpenseType { get; set; } = "Travel"; // Travel, Fuel, Accommodation, Meals, Tools, Parts, Other

    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow;

    // Mileage tracking
    public decimal? MileageDriven { get; set; }
    public decimal? MileageRate { get; set; }

    // Fuel tracking
    public decimal? FuelQuantity { get; set; }
    public decimal? FuelPricePerUnit { get; set; }

    // Receipt and documentation
    [MaxLength(500)]
    public string? ReceiptPath { get; set; }

    [MaxLength(100)]
    public string? VendorName { get; set; }

    [MaxLength(50)]
    public string? ReferenceNumber { get; set; }

    // Approval and reimbursement
    [MaxLength(20)]
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Reimbursed

    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }

    [MaxLength(1000)]
    public string? ApprovalNotes { get; set; }

    public bool IsReimbursable { get; set; } = true;
    public bool IsReimbursed { get; set; } = false;
    public DateTime? ReimbursedDate { get; set; }

    // Vehicle tracking
    public Guid? VehicleId { get; set; }

    // Location information
    [MaxLength(200)]
    public string? Location { get; set; }

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    // Navigation properties
    public virtual WorkOrder WorkOrder { get; set; } = null!;
    public virtual MaintenanceStaffSchedule? Schedule { get; set; }
    public virtual Employee? Technician { get; set; }
    public virtual Employee? ApprovedBy { get; set; }
    public virtual MaintenanceAsset? Vehicle { get; set; }
}

/// <summary>
/// Vehicle/transportation asset management for maintenance teams
/// </summary>
public class MaintenanceVehicle : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string VehicleCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public string VehicleType { get; set; } = "Van"; // Van, Truck, Car, Motorcycle, Other

    [MaxLength(20)]
    public string LicensePlate { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Make { get; set; }

    [MaxLength(100)]
    public string? Model { get; set; }

    public int? Year { get; set; }

    [MaxLength(50)]
    public string? VIN { get; set; }

    // Current status and location
    [MaxLength(20)]
    public string Status { get; set; } = "Available"; // Available, InUse, Maintenance, OutOfService

    [MaxLength(200)]
    public string? CurrentLocation { get; set; }

    [MaxLength(200)]
    public string? HomeBase { get; set; }

    // Usage tracking
    public decimal CurrentMileage { get; set; } = 0;
    public DateTime? LastServiceDate { get; set; }
    public DateTime? NextServiceDate { get; set; }

    // Fuel and efficiency
    public decimal FuelCapacity { get; set; } = 0;
    public decimal FuelLevel { get; set; } = 0;
    public decimal AverageFuelConsumption { get; set; } = 0;

    // Assignment
    public Guid? AssignedTechnicianId { get; set; }
    public Guid? AssignedTeamId { get; set; }

    // Insurance and registration
    public DateTime? InsuranceExpiry { get; set; }
    public DateTime? RegistrationExpiry { get; set; }
    public DateTime? InspectionExpiry { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation properties
    public virtual Employee? AssignedTechnician { get; set; }
    public virtual TechnicianTeam? AssignedTeam { get; set; }
    public virtual ICollection<MaintenanceStaffSchedule> Schedules { get; set; } = new List<MaintenanceStaffSchedule>();
}

#endregion
