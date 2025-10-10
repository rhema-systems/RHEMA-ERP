using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Maintenance;

#region Common DTOs

public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public class EmployeeDto
{
    public Guid Id { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public string? Title { get; set; }
    public string? DepartmentName { get; set; }
    public string? PositionName { get; set; }
}

#endregion

#region Maintenance-Inventory Integration DTOs

// Parts Allocation DTOs
public class MaintenancePartsAllocationResult
{
    public Guid WorkOrderId { get; set; }
    public int TotalPartsRequested { get; set; }
    public int SuccessfulAllocations { get; set; }
    public int FailedAllocationCount { get; set; }
    public DateTime AllocationDate { get; set; }
    public List<WorkOrderPartAllocationDto> AllocatedParts { get; set; } = new();
    public List<PartAllocationFailure> FailedAllocations { get; set; } = new();
}

public class WorkOrderPartAllocationDto
{
    public Guid PartId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal QuantityRequired { get; set; }
    public decimal QuantityAllocated { get; set; }
    public Guid AllocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public DateTime AllocatedAt { get; set; }
}

public class PartAllocationFailure
{
    public Guid PartId { get; set; }
    public string PartName { get; set; } = string.Empty;
    public decimal RequestedQuantity { get; set; }
    public string Reason { get; set; } = string.Empty;
}

// Parts Consumption DTOs
public class MaintenancePartsConsumptionResult
{
    public Guid WorkOrderId { get; set; }
    public int TotalPartsProcessed { get; set; }
    public int SuccessfulConsumptions { get; set; }
    public int FailedConsumptions { get; set; }
    public DateTime ConsumptionDate { get; set; }
    public List<PartConsumptionResultDto> ConsumedParts { get; set; } = new();
    public List<PartConsumptionFailure> ConsumptionFailures { get; set; } = new();
}

public class PartConsumptionDto
{
    public Guid PartId { get; set; }
    public decimal QuantityConsumed { get; set; }
}

public class PartConsumptionResultDto
{
    public Guid PartId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal QuantityConsumed { get; set; }
    public decimal TotalQuantityUsed { get; set; }
    public DateTime ConsumptionDate { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}

public class PartConsumptionFailure
{
    public Guid PartId { get; set; }
    public decimal RequestedQuantity { get; set; }
    public string Reason { get; set; } = string.Empty;
}

// Parts Return DTOs
public class MaintenancePartsReturnResult
{
    public Guid WorkOrderId { get; set; }
    public int TotalPartsProcessed { get; set; }
    public int SuccessfulReturns { get; set; }
    public int FailedReturns { get; set; }
    public DateTime ReturnDate { get; set; }
    public List<PartReturnResultDto> ReturnedParts { get; set; } = new();
    public List<PartReturnFailure> ReturnFailures { get; set; } = new();
}

public class PartReturnDto
{
    public Guid PartId { get; set; }
    public decimal QuantityReturned { get; set; }
    public string ReturnReason { get; set; } = string.Empty;
}

public class PartReturnResultDto
{
    public Guid PartId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal QuantityReturned { get; set; }
    public decimal TotalQuantityReturned { get; set; }
    public DateTime ReturnDate { get; set; }
    public string ReturnReason { get; set; } = string.Empty;
}

public class PartReturnFailure
{
    public Guid PartId { get; set; }
    public decimal RequestedQuantity { get; set; }
    public string Reason { get; set; } = string.Empty;
}

// Work Order Parts Status DTOs
public class WorkOrderPartsStatusDto
{
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public int TotalParts { get; set; }
    public int AllocatedParts { get; set; }
    public int ConsumedParts { get; set; }
    public int ReturnedParts { get; set; }
    public decimal TotalEstimatedCost { get; set; }
    public List<WorkOrderPartStatusDto> Parts { get; set; } = new();
}

public class WorkOrderPartStatusDto
{
    public Guid PartId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal QuantityRequired { get; set; }
    public decimal QuantityAllocated { get; set; }
    public decimal QuantityUsed { get; set; }
    public decimal QuantityReturned { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public Guid? AllocationId { get; set; }
    public DateTime? AllocatedAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? Notes { get; set; }
}

// Parts Availability DTOs
public class PartsAvailabilityCheckResult
{
    public Guid WorkOrderId { get; set; }
    public bool AllPartsAvailable { get; set; }
    public int TotalParts { get; set; }
    public int AvailableParts { get; set; }
    public int UnavailableParts { get; set; }
    public DateTime CheckDate { get; set; }
    public List<PartAvailabilityDto> PartAvailability { get; set; } = new();
}

public class PartAvailabilityDto
{
    public Guid PartId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal QuantityRequired { get; set; }
    public decimal AvailableQuantity { get; set; }
    public bool IsAvailable { get; set; }
    public string? AvailabilityIssue { get; set; }
}

// Work Order Lifecycle DTOs
public class WorkOrderStartResult
{
    public Guid WorkOrderId { get; set; }
    public Guid TechnicianId { get; set; }
    public DateTime StartedAt { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int PartsAllocated { get; set; }
    public List<string> SuccessMessages { get; set; } = new();
    public List<string> WarningMessages { get; set; } = new();
    public List<PartAvailabilityDto> PartsAvailabilityIssues { get; set; } = new();
    public List<PartAllocationFailure> AllocationFailures { get; set; } = new();
}

public class CompleteWorkOrderDto
{
    public Guid WorkOrderId { get; set; }
    public string? CompletionNotes { get; set; }
    public double? ActualHours { get; set; }
    public decimal? ActualCost { get; set; }
    public string? PartsUsed { get; set; }
    public string? WorkPerformed { get; set; }
    public string? FailureCode { get; set; }
    public string? CauseCode { get; set; }
    public string? ActionCode { get; set; }
    public string? AssetStatusUpdate { get; set; }
    public List<PartConsumptionDto> PartsConsumed { get; set; } = new();
    public List<PartReturnDto> PartsReturned { get; set; } = new();
}

public class WorkOrderCompletionResult
{
    public Guid WorkOrderId { get; set; }
    public Guid CompletedById { get; set; }
    public DateTime CompletionDate { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int PartsConsumed { get; set; }
    public int PartsReturned { get; set; }
    public decimal TotalPartsCost { get; set; }
    public decimal TotalCost { get; set; }
    public double TotalHours { get; set; }
    public List<string> SuccessMessages { get; set; } = new();
    public List<string> WarningMessages { get; set; } = new();
    public List<PartConsumptionFailure> ConsumptionFailures { get; set; } = new();
    public List<PartReturnFailure> ReturnFailures { get; set; } = new();
    public QualityValidationResult? QualityValidationResult { get; set; }
}

#region Quality Control DTOs

public class QualityValidationResult
{
    public Guid WorkOrderId { get; set; }
    public bool CanComplete { get; set; }
    public DateTime ValidationDate { get; set; }
    public Guid? ValidatedById { get; set; }
    public string? ValidatorName { get; set; }
    public string? OverallResult { get; set; } // Pass, Fail, ConditionalPass
    public string? ValidationNotes { get; set; }
    public bool RequiresInspectionOfficerApproval { get; set; }
    public List<string> ValidationMessages { get; set; } = new();
    public List<string> ValidationFailures { get; set; } = new();
    public List<RequiredInspectionDto> RequiredInspections { get; set; } = new();
    public List<QualityChecklistResultDto>? ChecklistResults { get; set; }
}

public class RequiredInspectionDto
{
    public Guid InspectionTemplateId { get; set; }
    public string InspectionType { get; set; } = string.Empty;
    public string InspectionName { get; set; } = string.Empty;
    public bool IsRegulatory { get; set; }
    public string? Description { get; set; }
}

public class QualityChecklistItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsRequired { get; set; }
    public string Category { get; set; } = string.Empty;
}

public class QualityChecklistResultDto
{
    public string ItemId { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty; // Pass, Fail, N/A
    public string? Comments { get; set; }
    public Guid? CheckedById { get; set; }
    public DateTime? CheckedDate { get; set; }
}

public class RecordQualityValidationDto
{
    public string OverallResult { get; set; } = string.Empty; // Pass, Fail, ConditionalPass
    public string? ValidationNotes { get; set; }
    public List<QualityChecklistResultDto>? ChecklistResults { get; set; }
}

#endregion

public class WorkOrderStatusChangeResult
{
    public Guid WorkOrderId { get; set; }
    public Guid UserId { get; set; }
    public DateTime ChangeDate { get; set; }
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int PartsAllocated { get; set; }
    public int PartsReturned { get; set; }
    public List<string> SuccessMessages { get; set; } = new();
    public List<string> WarningMessages { get; set; } = new();
}

public class CompleteTaskDto
{
    public Guid TaskId { get; set; }
    public string? CompletionNotes { get; set; }
    public double ActualHours { get; set; }
}

public class TaskCompletionResult
{
    public Guid TaskId { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid CompletedById { get; set; }
    public DateTime CompletionDate { get; set; }
    public bool Success { get; set; }
    public bool AllTasksCompleted { get; set; }
    public List<string> SuccessMessages { get; set; } = new();
}

// Work Order Scheduling DTOs

public class RescheduleWorkOrderDto
{
    public Guid WorkOrderId { get; set; }
    public DateTime? NewStartDate { get; set; }
    public DateTime? NewCompletionDate { get; set; }
    public Guid? NewAssignedTechnicianId { get; set; }
    public Guid? NewAssignedTeamId { get; set; }
    public string RescheduleReason { get; set; } = string.Empty;
    public bool UpdateReservations { get; set; } = true;
}

public class WorkOrderSchedulingResult
{
    public Guid WorkOrderId { get; set; }
    public Guid ScheduledById { get; set; }
    public DateTime SchedulingDate { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public bool Success { get; set; }
    public DateTime? ScheduledStartDate { get; set; }
    public DateTime? ScheduledCompletionDate { get; set; }
    public DateTime? PreviousStartDate { get; set; }
    public DateTime? PreviousCompletionDate { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public Guid? AssignedTeamId { get; set; }
    public int PartsReserved { get; set; }
    public List<string> SuccessMessages { get; set; } = new();
    public List<string> WarningMessages { get; set; } = new();
    public List<ReservedPartDto> ReservedParts { get; set; } = new();
    public List<PartReservationFailure> ReservationFailures { get; set; } = new();
    public List<PartAvailabilityDto> PartsAvailabilityIssues { get; set; } = new();
    public List<WorkOrderConflictDto> ResourceConflicts { get; set; } = new();
}

public class WorkOrderForSchedulingDto
{
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public int PriorityLevel { get; set; }
    public string PriorityName { get; set; } = string.Empty;
    public double EstimatedHours { get; set; }
    public decimal EstimatedCost { get; set; }
    public int RequiredParts { get; set; }
    public bool AllPartsAvailable { get; set; }
    public int UnavailableParts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RequestedStartDate { get; set; }
    public bool RequiresPermit { get; set; }
    public bool RequiresLockout { get; set; }
    public bool RequiresConfinedSpaceEntry { get; set; }
}

public class ReservedPartDto
{
    public Guid PartId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal QuantityReserved { get; set; }
    public Guid ReservationId { get; set; }
    public DateTime ReservedAt { get; set; }
}

public class PartReservationFailure
{
    public Guid PartId { get; set; }
    public string PartName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class TechnicianAvailabilityResult
{
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsAvailable { get; set; }
    public double AvailableHours { get; set; }
    public double BookedHours { get; set; }
    public List<WorkOrderConflictDto> ConflictingWorkOrders { get; set; } = new();
}

public class WorkOrderConflictDto
{
    public Guid WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public double EstimatedHours { get; set; }
}

// Inventory Integration DTOs
public class InventoryItemSummaryDto
{
    public Guid Id { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? UnitOfMeasure { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal AvailableStock { get; set; }
    public decimal StandardCost { get; set; }
    public decimal AverageCost { get; set; }
    public bool IsSerialTracked { get; set; }
    public bool IsLotTracked { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string? PrimarySupplier { get; set; }
    public int LeadTimeDays { get; set; }
}

#endregion

#region Asset Management DTOs

// MaintenanceAsset DTOs
public class MaintenanceAssetDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid AssetCategoryId { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? CurrentValue { get; set; }
    public string? Location { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Criticality { get; set; } = string.Empty;
    public DateTime? WarrantyStartDate { get; set; }
    public DateTime? WarrantyEndDate { get; set; }
    public string? WarrantyProvider { get; set; }
    public Guid? ParentAssetId { get; set; }
    public double? OperatingHours { get; set; }
    public DateTime? LastOperatingHoursUpdate { get; set; }
    public double? Mileage { get; set; }
    public DateTime? LastMileageUpdate { get; set; }

    // Navigation properties
    public MaintenanceAssetCategoryDto? AssetCategory { get; set; }
    public MaintenanceAssetDto? ParentAsset { get; set; }
    public ICollection<MaintenanceAssetDto> ChildAssets { get; set; } = new List<MaintenanceAssetDto>();

    // Additional properties
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class MaintenanceAssetListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Criticality { get; set; } = string.Empty;
    public string? Location { get; set; }
    public decimal? CurrentValue { get; set; }
    public int ActiveWorkOrdersCount { get; set; }
    public DateTime? LastMaintenanceDate { get; set; }
    public DateTime? NextMaintenanceDate { get; set; }
}

public class CreateMaintenanceAssetDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string AssetNumber { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public Guid AssetCategoryId { get; set; }

    [StringLength(100)]
    public string? Manufacturer { get; set; }

    [StringLength(100)]
    public string? Model { get; set; }

    [StringLength(50)]
    public string? SerialNumber { get; set; }

    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? CurrentValue { get; set; }

    [StringLength(500)]
    public string? Location { get; set; }

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Active";

    [Required]
    [StringLength(20)]
    public string Criticality { get; set; } = "Medium";

    public DateTime? WarrantyStartDate { get; set; }
    public DateTime? WarrantyEndDate { get; set; }

    [StringLength(200)]
    public string? WarrantyProvider { get; set; }

    public Guid? ParentAssetId { get; set; }
    public double? OperatingHours { get; set; }
    public double? Mileage { get; set; }
    public string? Specifications { get; set; }
    public string? DocumentLinks { get; set; }
    public string? Images { get; set; }
}

public class UpdateMaintenanceAssetDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public Guid AssetCategoryId { get; set; }

    [StringLength(100)]
    public string? Manufacturer { get; set; }

    [StringLength(100)]
    public string? Model { get; set; }

    [StringLength(50)]
    public string? SerialNumber { get; set; }

    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public decimal? CurrentValue { get; set; }

    [StringLength(500)]
    public string? Location { get; set; }

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Criticality { get; set; } = string.Empty;

    public DateTime? WarrantyStartDate { get; set; }
    public DateTime? WarrantyEndDate { get; set; }

    [StringLength(200)]
    public string? WarrantyProvider { get; set; }

    public Guid? ParentAssetId { get; set; }
    public string? Specifications { get; set; }
    public string? DocumentLinks { get; set; }
    public string? Images { get; set; }
}

// MaintenanceAssetCategory DTOs
public class MaintenanceAssetCategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Code { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public bool IsActive { get; set; }
    public int AssetCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateMaintenanceAssetCategoryDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(50)]
    public string? Code { get; set; }

    [StringLength(7)]
    public string? Color { get; set; }

    [StringLength(50)]
    public string? Icon { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateMaintenanceAssetCategoryDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(50)]
    public string? Code { get; set; }

    [StringLength(7)]
    public string? Color { get; set; }

    [StringLength(50)]
    public string? Icon { get; set; }

    public bool IsActive { get; set; } = true;
}

#endregion

#region Work Order DTOs

public class WorkOrderDto
{
    public Guid Id { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid AssetId { get; set; }
    public Guid WorkOrderTypeId { get; set; }
    public Guid MaintenanceTypeId { get; set; }
    public Guid PriorityLevelId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public string AssetLocation { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string RequiredSkills { get; set; } = string.Empty;
    public string SafetyNotes { get; set; } = string.Empty;
    public string AssignedTechnicianName { get; set; } = string.Empty;
    public Guid? AssignedTechnicianId { get; set; }
    public Guid? AssignedTeamId { get; set; }
    public DateTime? RequestedStartDate { get; set; }
    public DateTime? RequestedCompletionDate { get; set; }
    public DateTime? ScheduledStartDate { get; set; }
    public DateTime? ScheduledEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public DateTime? ActualCompletionDate { get; set; }
    public decimal EstimatedCost { get; set; }
    public decimal ActualCost { get; set; }
    public double EstimatedHours { get; set; }
    public double ActualHours { get; set; }
    public Guid? RequestedById { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? CompletionNotes { get; set; }
    public string? FailureCode { get; set; }
    public string? CauseCode { get; set; }
    public string? ActionCode { get; set; }
    public string? SafetyRequirements { get; set; }
    public bool RequiresPermit { get; set; }
    public bool RequiresLockout { get; set; }
    public bool RequiresConfinedSpaceEntry { get; set; }
    public Guid? ParentWorkOrderId { get; set; }
    public Guid? MaintenanceScheduleId { get; set; }
    public bool IsRecurring { get; set; }

    // Navigation properties
    public MaintenanceAssetDto? Asset { get; set; }
    public WorkOrderTypeDto? WorkOrderType { get; set; }
    public MaintenanceTypeDto? MaintenanceType { get; set; }
    public PriorityLevelDto? PriorityLevel { get; set; }
    public EmployeeDto? AssignedTechnician { get; set; }
    public TechnicianTeamDto? AssignedTeam { get; set; }
    public EmployeeDto? RequestedBy { get; set; }
    public EmployeeDto? ApprovedBy { get; set; }
    public WorkOrderDto? ParentWorkOrder { get; set; }
    public ICollection<WorkOrderTaskDto> Tasks { get; set; } = new List<WorkOrderTaskDto>();
    public ICollection<WorkOrderPartDto> Parts { get; set; } = new List<WorkOrderPartDto>();
    public ICollection<WorkOrderLaborDto> Labor { get; set; } = new List<WorkOrderLaborDto>();
    public ICollection<WorkOrderCommentDto> Comments { get; set; } = new List<WorkOrderCommentDto>();

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class WorkOrderListDto
{
    public Guid Id { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public string WorkOrderTypeName { get; set; } = string.Empty;
    public string MaintenanceTypeName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string PriorityName { get; set; } = string.Empty;
    public int PriorityLevel { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public string? AssignedTechnicianName { get; set; }
    public string? AssignedTeamName { get; set; }
    public DateTime? RequestedStartDate { get; set; }
    public DateTime? ScheduledStartDate { get; set; }
    public DateTime? ScheduledEndDate { get; set; }
    public DateTime? RequestedCompletionDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public DateTime? ActualCompletionDate { get; set; }
    public decimal EstimatedCost { get; set; }
    public decimal ActualCost { get; set; }
    public double EstimatedHours { get; set; }
    public double ActualHours { get; set; }
    public bool IsOverdue { get; set; }
    public int TasksCount { get; set; }
    public int CompletedTasksCount { get; set; }
    public double CompletionPercentage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class CreateWorkOrderDto
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    public Guid AssetId { get; set; }

    [Required]
    public Guid WorkOrderTypeId { get; set; }

    [Required]
    public Guid MaintenanceTypeId { get; set; }

    [Required]
    public Guid PriorityLevelId { get; set; }

    public string Type { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string SafetyNotes { get; set; } = string.Empty;
    public string RequiredSkills { get; set; } = string.Empty;
    public bool IsEmergency { get; set; }

    public Guid? AssignedTechnicianId { get; set; }
    public Guid? AssignedTeamId { get; set; }

    public DateTime? RequestedStartDate { get; set; }
    public DateTime? RequestedCompletionDate { get; set; }
    public DateTime? ScheduledStartDate { get; set; }
    public DateTime? ScheduledEndDate { get; set; }

    public decimal EstimatedCost { get; set; }
    public double EstimatedHours { get; set; }

    [StringLength(500)]
    public string? SafetyRequirements { get; set; }

    public bool RequiresPermit { get; set; }
    public bool RequiresLockout { get; set; }
    public bool RequiresConfinedSpaceEntry { get; set; }

    public Guid? ParentWorkOrderId { get; set; }
    public Guid? MaintenanceScheduleId { get; set; }
}

public class UpdateWorkOrderDto
{
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public string Instructions { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    [Required]
    public Guid WorkOrderTypeId { get; set; }

    [Required]
    public Guid MaintenanceTypeId { get; set; }

    [Required]
    public Guid PriorityLevelId { get; set; }

    public Guid? AssignedTechnicianId { get; set; }
    public Guid? AssignedTeamId { get; set; }

    public DateTime? RequestedStartDate { get; set; }
    public DateTime? RequestedCompletionDate { get; set; }

    public decimal EstimatedCost { get; set; }
    public double EstimatedHours { get; set; }

    [StringLength(500)]
    public string? SafetyRequirements { get; set; }

    public bool RequiresPermit { get; set; }
    public bool RequiresLockout { get; set; }
    public bool RequiresConfinedSpaceEntry { get; set; }
}

// Duplicate CompleteWorkOrderDto removed - using the more comprehensive version above

public class WorkOrderFilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? SearchTerm { get; set; }
    public Guid? AssetId { get; set; }
    public string? Status { get; set; }
    public Guid? WorkOrderTypeId { get; set; }
    public Guid? MaintenanceTypeId { get; set; }
    public Guid? PriorityLevelId { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public Guid? AssignedTeamId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool? IsOverdue { get; set; }
    public string SortBy { get; set; } = "CreatedAt";
    public bool SortDescending { get; set; } = true;
}

// Work Order Type DTOs
public class WorkOrderTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public bool IsActive { get; set; }
    public bool RequiresApproval { get; set; }
    public int DefaultPriority { get; set; }
}

public class CreateWorkOrderTypeDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(7)]
    public string? Color { get; set; }

    [StringLength(50)]
    public string? Icon { get; set; }

    public bool IsActive { get; set; } = true;
    public bool RequiresApproval { get; set; }

    [Range(1, 4)]
    public int DefaultPriority { get; set; } = 3;
}

public class UpdateWorkOrderTypeDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(7)]
    public string? Color { get; set; }

    [StringLength(50)]
    public string? Icon { get; set; }

    public bool IsActive { get; set; } = true;
    public bool RequiresApproval { get; set; }

    [Range(1, 4)]
    public int DefaultPriority { get; set; } = 3;
}

// Maintenance Type DTOs
public class MaintenanceTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Color { get; set; }
    public bool IsActive { get; set; }
}

public class CreateMaintenanceTypeDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    [StringLength(20)]
    public string Category { get; set; } = "Scheduled";

    [StringLength(7)]
    public string? Color { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateMaintenanceTypeDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    [StringLength(20)]
    public string Category { get; set; } = string.Empty;

    [StringLength(7)]
    public string? Color { get; set; }

    public bool IsActive { get; set; } = true;
}

// Priority Level DTOs
public class PriorityLevelDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public string? Description { get; set; }
    public string Color { get; set; } = string.Empty;
    public int ResponseTimeHours { get; set; }
    public bool IsActive { get; set; }
}

public class CreatePriorityLevelDto
{
    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public int Level { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(7)]
    public string Color { get; set; } = "#6B7280";

    [Range(1, 8760)]
    public int ResponseTimeHours { get; set; } = 24;

    public bool IsActive { get; set; } = true;
}

public class UpdatePriorityLevelDto
{
    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public int Level { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(7)]
    public string Color { get; set; } = string.Empty;

    [Range(1, 8760)]
    public int ResponseTimeHours { get; set; }

    public bool IsActive { get; set; } = true;
}

#endregion

#region Work Order Detail DTOs

// Work Order Task DTOs
public class WorkOrderTaskDto
{
    public Guid Id { get; set; }
    public Guid WorkOrderId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Sequence { get; set; }
    public string Status { get; set; } = string.Empty;
    public double EstimatedHours { get; set; }
    public double ActualHours { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletionNotes { get; set; }
    public bool IsRequired { get; set; }

    public EmployeeDto? AssignedTechnician { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateWorkOrderTaskDto
{
    [Required]
    public Guid WorkOrderId { get; set; }

    [Required]
    [StringLength(200)]
    public string TaskName { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public int Sequence { get; set; } = 1;
    public double EstimatedHours { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public bool IsRequired { get; set; } = true;
}

public class UpdateWorkOrderTaskDto
{
    [Required]
    [StringLength(200)]
    public string TaskName { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public int Sequence { get; set; }
    public double EstimatedHours { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public bool IsRequired { get; set; }
}

// Work Order Part DTOs
public class WorkOrderPartDto
{
    public Guid Id { get; set; }
    public Guid WorkOrderId { get; set; }
    
    // Inventory Integration
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    // Quantities
    public decimal QuantityRequired { get; set; }
    public decimal QuantityAllocated { get; set; }
    public decimal QuantityUsed { get; set; }
    public decimal QuantityReturned { get; set; }
    
    // Costing
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    
    // Location and tracking
    public string? SerialNumber { get; set; }
    public string? LotNumber { get; set; }
    public string? WarehouseLocationCode { get; set; }
    public string? WarehouseLocationName { get; set; }
    
    // Status and allocation tracking
    public string Status { get; set; } = string.Empty;
    public Guid? AllocationId { get; set; }
    public DateTime? AllocatedAt { get; set; }
    public DateTime? PickedAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? Notes { get; set; }
    
    // Inventory item details
    public InventoryItemSummaryDto? InventoryItem { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateWorkOrderPartDto
{
    [Required]
    public Guid WorkOrderId { get; set; }

    [Required]
    public Guid InventoryItemId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal QuantityRequired { get; set; } = 1;

    [Range(0, double.MaxValue)]
    public decimal UnitCost { get; set; }

    public Guid? WarehouseLocationId { get; set; }
    
    [StringLength(100)]
    public string? SerialNumber { get; set; }
    
    [StringLength(100)]
    public string? LotNumber { get; set; }
    
    [StringLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateWorkOrderPartDto
{
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal QuantityRequired { get; set; }

    [Range(0, double.MaxValue)]
    public decimal QuantityUsed { get; set; }

    [Range(0, double.MaxValue)]
    public decimal QuantityReturned { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitCost { get; set; }

    public Guid? WarehouseLocationId { get; set; }
    
    [StringLength(100)]
    public string? SerialNumber { get; set; }
    
    [StringLength(100)]
    public string? LotNumber { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string? Notes { get; set; }
}

// Work Order Labor DTOs
public class WorkOrderLaborDto
{
    public Guid Id { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid TechnicianId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double Hours { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal TotalCost { get; set; }
    public string? Notes { get; set; }
    public string LaborType { get; set; } = string.Empty;

    public EmployeeDto? Technician { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateWorkOrderLaborDto
{
    [Required]
    public Guid WorkOrderId { get; set; }

    [Required]
    public Guid TechnicianId { get; set; }

    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    [Range(0, double.MaxValue)]
    public decimal HourlyRate { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [StringLength(50)]
    public string LaborType { get; set; } = "Regular";
}

public class UpdateWorkOrderLaborDto
{
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double Hours { get; set; }

    [Range(0, double.MaxValue)]
    public decimal HourlyRate { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [StringLength(50)]
    public string LaborType { get; set; } = string.Empty;
}

// Work Order Comment DTOs
public class WorkOrderCommentDto
{
    public Guid Id { get; set; }
    public Guid WorkOrderId { get; set; }
    public Guid EmployeeId { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string CommentType { get; set; } = string.Empty;
    public bool IsInternal { get; set; }

    public EmployeeDto? Employee { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateWorkOrderCommentDto
{
    [Required]
    public Guid WorkOrderId { get; set; }

    [Required]
    [StringLength(2000)]
    public string Comment { get; set; } = string.Empty;

    [StringLength(20)]
    public string CommentType { get; set; } = "General";

    public bool IsInternal { get; set; } = true;
}

#endregion

#region Analytics and Reporting DTOs

public class AssetMetricsDto
{
    public int TotalAssets { get; set; }
    public int ActiveAssets { get; set; }
    public int MaintenanceAssets { get; set; }
    public int RetiredAssets { get; set; }
    public decimal TotalValue { get; set; }
    public decimal AverageValue { get; set; }
    public Dictionary<string, int> AssetsByCategory { get; set; } = new();
    public Dictionary<string, int> AssetsByStatus { get; set; } = new();
    public Dictionary<string, int> AssetsByCriticality { get; set; } = new();
}

public class CategoryStatisticsDto
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int TotalAssets { get; set; }
    public int ActiveAssets { get; set; }
    public decimal TotalValue { get; set; }
    public decimal AverageValue { get; set; }
    public int ActiveWorkOrders { get; set; }
    public int OverdueMaintenanceCount { get; set; }
}

public class WorkOrderMetricsDto
{
    public int TotalWorkOrders { get; set; }
    public int OpenWorkOrders { get; set; }
    public int InProgressWorkOrders { get; set; }
    public int CompletedWorkOrders { get; set; }
    public int OverdueWorkOrders { get; set; }
    public decimal TotalCost { get; set; }
    public double TotalHours { get; set; }
    public double AverageCompletionTime { get; set; }
    public Dictionary<string, int> WorkOrdersByStatus { get; set; } = new();
    public Dictionary<string, int> WorkOrdersByPriority { get; set; } = new();
    public Dictionary<string, int> WorkOrdersByType { get; set; } = new();
}

public class MaintenanceDashboardDto
{
    public AssetMetricsDto AssetMetrics { get; set; } = new();
    public WorkOrderMetricsDto WorkOrderMetrics { get; set; } = new();
    public IEnumerable<WorkOrderListDto> RecentWorkOrders { get; set; } = new List<WorkOrderListDto>();
    public IEnumerable<WorkOrderListDto> OverdueWorkOrders { get; set; } = new List<WorkOrderListDto>();
    public IEnumerable<MaintenanceAssetListDto> AssetsRequiringMaintenance { get; set; } = new List<MaintenanceAssetListDto>();
    public IEnumerable<AssetDowntimeDto> ActiveDowntime { get; set; } = new List<AssetDowntimeDto>();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class MaintenanceKPIsDto
{
    public double PlannedMaintenancePercentage { get; set; }
    public double ScheduleCompliance { get; set; }
    public double MeanTimeBetweenFailure { get; set; }
    public double MeanTimeToRepair { get; set; }
    public double OverallEquipmentEffectiveness { get; set; }
    public double MaintenanceCostPercentage { get; set; }
    public double TechnicianUtilization { get; set; }
    public double WorkOrderCompletionRate { get; set; }
    public double PreventiveMaintenanceCompliance { get; set; }
    public double AssetAvailability { get; set; }
}

public class LaborReportDto
{
    public Guid TechnicianId { get; set; }
    public string TechnicianName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public double TotalHours { get; set; }
    public decimal TotalCost { get; set; }
    public int WorkOrdersCompleted { get; set; }
    public double RegularHours { get; set; }
    public double OvertimeHours { get; set; }
    public double EmergencyHours { get; set; }
    public double UtilizationPercentage { get; set; }
}

#endregion

#region Maintenance Scheduling DTOs

public class MaintenanceScheduleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid AssetId { get; set; }
    public Guid MaintenanceTypeId { get; set; }
    public string ScheduleType { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public int IntervalValue { get; set; }
    public TimeOnly? PreferredTime { get; set; }
    public int? DayOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public double? UsageInterval { get; set; }
    public string? UsageUnit { get; set; }
    public string? ConditionParameters { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? LastGeneratedDate { get; set; }
    public DateTime? NextDueDate { get; set; }
    public int LeadTimeDays { get; set; }
    public bool IsActive { get; set; }
    public double EstimatedHours { get; set; }
    public decimal EstimatedCost { get; set; }
    public string? WorkOrderTitle { get; set; }
    public string? WorkOrderDescription { get; set; }
    public Guid? DefaultTechnicianId { get; set; }
    public Guid? DefaultTeamId { get; set; }
    public Guid? PriorityLevelId { get; set; }

    public MaintenanceAssetDto? Asset { get; set; }
    public MaintenanceTypeDto? MaintenanceType { get; set; }
    public string? DefaultTechnician { get; set; }
    public TechnicianTeamDto? DefaultTeam { get; set; }
    public PriorityLevelDto? PriorityLevel { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateMaintenanceScheduleDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public Guid AssetId { get; set; }

    [Required]
    public Guid MaintenanceTypeId { get; set; }

    [Required]
    [StringLength(20)]
    public string ScheduleType { get; set; } = "TimeBased";

    [Required]
    [StringLength(20)]
    public string Frequency { get; set; } = "Monthly";

    [Range(1, int.MaxValue)]
    public int IntervalValue { get; set; } = 1;

    public TimeOnly? PreferredTime { get; set; }

    [Range(0, 6)]
    public int? DayOfWeek { get; set; }

    [Range(1, 31)]
    public int? DayOfMonth { get; set; }

    public double? UsageInterval { get; set; }

    [StringLength(20)]
    public string? UsageUnit { get; set; }

    public string? ConditionParameters { get; set; }

    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }

    [Range(0, 365)]
    public int LeadTimeDays { get; set; } = 7;

    public bool IsActive { get; set; } = true;
    public double EstimatedHours { get; set; }
    public decimal EstimatedCost { get; set; }

    [StringLength(200)]
    public string? WorkOrderTitle { get; set; }

    [StringLength(2000)]
    public string? WorkOrderDescription { get; set; }

    public Guid? DefaultTechnicianId { get; set; }
    public Guid? DefaultTeamId { get; set; }
    public Guid? PriorityLevelId { get; set; }
    public string? TaskTemplate { get; set; }
    public string? PartsTemplate { get; set; }
}

public class UpdateMaintenanceScheduleDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public Guid MaintenanceTypeId { get; set; }

    [Required]
    [StringLength(20)]
    public string ScheduleType { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Frequency { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int IntervalValue { get; set; }

    public TimeOnly? PreferredTime { get; set; }

    [Range(0, 6)]
    public int? DayOfWeek { get; set; }

    [Range(1, 31)]
    public int? DayOfMonth { get; set; }

    public double? UsageInterval { get; set; }

    [StringLength(20)]
    public string? UsageUnit { get; set; }

    public string? ConditionParameters { get; set; }
    public DateTime? EndDate { get; set; }

    [Range(0, 365)]
    public int LeadTimeDays { get; set; }

    public bool IsActive { get; set; }
    public double EstimatedHours { get; set; }
    public decimal EstimatedCost { get; set; }

    [StringLength(200)]
    public string? WorkOrderTitle { get; set; }

    [StringLength(2000)]
    public string? WorkOrderDescription { get; set; }

    public Guid? DefaultTechnicianId { get; set; }
    public Guid? DefaultTeamId { get; set; }
    public Guid? PriorityLevelId { get; set; }
    public string? TaskTemplate { get; set; }
    public string? PartsTemplate { get; set; }
}

public class ScheduleComplianceReportDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalSchedules { get; set; }
    public int CompletedOnTime { get; set; }
    public int CompletedLate { get; set; }
    public int Missed { get; set; }
    public double CompliancePercentage { get; set; }
    public Dictionary<string, double> ComplianceByAssetCategory { get; set; } = new();
    public Dictionary<string, double> ComplianceByMaintenanceType { get; set; } = new();
}

#endregion

#region Inspection DTOs

public class InspectionTemplateDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string InspectionType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string ChecklistItems { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateInspectionTemplateDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    [Required]
    [StringLength(50)]
    public string InspectionType { get; set; } = "Safety";

    public bool IsActive { get; set; } = true;
    public string ChecklistItems { get; set; } = "[]";
}

public class UpdateInspectionTemplateDto
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    [Required]
    [StringLength(50)]
    public string InspectionType { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public string ChecklistItems { get; set; } = string.Empty;
}

public class AssetInspectionDto
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public Guid InspectionTemplateId { get; set; }
    public Guid InspectorId { get; set; }
    public string InspectionType { get; set; } = string.Empty;
    public string InspectorName { get; set; } = string.Empty;
    public DateTime InspectionDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? OverallResult { get; set; }
    public string InspectionData { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? RecommendedActions { get; set; }
    public DateTime? NextInspectionDue { get; set; }
    public bool IsRegulatoryRequired { get; set; }
    public string? RegulatoryStandard { get; set; }

    public MaintenanceAssetDto? Asset { get; set; }
    public InspectionTemplateDto? InspectionTemplate { get; set; }
    public string? Inspector { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateAssetInspectionDto
{
    [Required]
    public Guid AssetId { get; set; }

    [Required]
    public Guid InspectionTemplateId { get; set; }

    [Required]
    public Guid InspectorId { get; set; }

    public DateTime InspectionDate { get; set; } = DateTime.UtcNow;

    [StringLength(2000)]
    public string? Notes { get; set; }

    public bool IsRegulatoryRequired { get; set; }

    [StringLength(100)]
    public string? RegulatoryStandard { get; set; }
}

public class UpdateAssetInspectionDto
{
    [Required]
    public Guid InspectorId { get; set; }

    public DateTime InspectionDate { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public bool IsRegulatoryRequired { get; set; }

    [StringLength(100)]
    public string? RegulatoryStandard { get; set; }
}

public class CompleteInspectionDto
{
    [Required]
    [StringLength(20)]
    public string OverallResult { get; set; } = string.Empty;

    public string InspectionData { get; set; } = "{}";

    [StringLength(2000)]
    public string? Notes { get; set; }

    [StringLength(2000)]
    public string? RecommendedActions { get; set; }

    public DateTime? NextInspectionDue { get; set; }
}

public class InspectionComplianceReportDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalInspections { get; set; }
    public int PassedInspections { get; set; }
    public int FailedInspections { get; set; }
    public int OverdueInspections { get; set; }
    public double PassRate { get; set; }
    public Dictionary<string, double> PassRateByAssetCategory { get; set; } = new();
    public Dictionary<string, double> PassRateByInspectionType { get; set; } = new();
}

#endregion

#region Resource Management DTOs

public class TechnicianTeamDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? TeamLeaderId { get; set; }
    public string Status { get; set; } = string.Empty;

    public string? TeamLeader { get; set; }
    public ICollection<TechnicianTeamMemberDto> Members { get; set; } = new List<TechnicianTeamMemberDto>();

    public int TotalMembers { get; set; }
    public int ActiveMembers { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateTechnicianTeamDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public Guid? TeamLeaderId { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = "Active";
}

public class UpdateTechnicianTeamDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public Guid? TeamLeaderId { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = string.Empty;
}

public class TechnicianTeamMemberDto
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public Guid TechnicianId { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedDate { get; set; }
    public DateTime? LeftDate { get; set; }
    public bool IsActive { get; set; }

    public TechnicianTeamDto? Team { get; set; }
    public string? Technician { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateTechnicianTeamMemberDto
{
    [Required]
    public Guid TeamId { get; set; }

    [Required]
    public Guid TechnicianId { get; set; }

    [StringLength(50)]
    public string Role { get; set; } = "Member";

    public DateTime JoinedDate { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}

public class UpdateTechnicianTeamMemberDto
{
    [StringLength(50)]
    public string Role { get; set; } = string.Empty;

    public DateTime? LeftDate { get; set; }
    public bool IsActive { get; set; }
}


public class CreateTechnicianSkillDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateTechnicianSkillDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    public bool IsActive { get; set; }
}

public class UserTechnicianSkillDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid SkillId { get; set; }
    public int ProficiencyLevel { get; set; }
    public DateTime? CertificationDate { get; set; }
    public DateTime? CertificationExpiry { get; set; }
    public string? CertifyingBody { get; set; }
    public string? CertificationNumber { get; set; }

    public EmployeeDto? Employee { get; set; }
    public TechnicianSkillDto? Skill { get; set; }

    public bool IsExpired => CertificationExpiry.HasValue && CertificationExpiry.Value < DateTime.UtcNow;
    public bool IsExpiringSoon => CertificationExpiry.HasValue && CertificationExpiry.Value < DateTime.UtcNow.AddDays(30);

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateUserTechnicianSkillDto
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid SkillId { get; set; }

    [Range(1, 4)]
    public int ProficiencyLevel { get; set; } = 1;

    public DateTime? CertificationDate { get; set; }
    public DateTime? CertificationExpiry { get; set; }

    [StringLength(200)]
    public string? CertifyingBody { get; set; }

    [StringLength(100)]
    public string? CertificationNumber { get; set; }
}

public class UpdateUserTechnicianSkillDto
{
    [Range(1, 4)]
    public int ProficiencyLevel { get; set; }

    public DateTime? CertificationDate { get; set; }
    public DateTime? CertificationExpiry { get; set; }

    [StringLength(200)]
    public string? CertifyingBody { get; set; }

    [StringLength(100)]
    public string? CertificationNumber { get; set; }
}

public class TeamPerformanceReportDto
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int WorkOrdersCompleted { get; set; }
    public double TotalHours { get; set; }
    public decimal TotalCost { get; set; }
    public double AverageCompletionTime { get; set; }
    public double OnTimeCompletionRate { get; set; }
    public Dictionary<Guid, int> MemberWorkOrderCounts { get; set; } = new();
    public Dictionary<Guid, double> MemberHours { get; set; } = new();
}

public class TeamWorkloadReportDto
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public int OpenWorkOrders { get; set; }
    public int AssignedWorkOrders { get; set; }
    public int InProgressWorkOrders { get; set; }
    public double CurrentWorkloadHours { get; set; }
    public double WeeklyCapacityHours { get; set; }
    public double UtilizationPercentage { get; set; }
    public Dictionary<Guid, double> MemberUtilization { get; set; } = new();
}

public class SkillGapAnalysisDto
{
    public Dictionary<string, int> RequiredSkillsCount { get; set; } = new();
    public Dictionary<string, int> AvailableSkillsCount { get; set; } = new();
    public Dictionary<string, int> SkillGaps { get; set; } = new();
    public Dictionary<string, double> AverageProficiencyLevels { get; set; } = new();
    public List<string> SkillsWithShortages { get; set; } = new();
    public List<UserTechnicianSkillDto> ExpiringCertifications { get; set; } = new();
}

#endregion

#region Asset Downtime DTOs

public class AssetDowntimeDto
{
    public Guid Id { get; set; }
    public Guid AssetId { get; set; }
    public Guid? WorkOrderId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public double? DowntimeHours { get; set; }
    public string DowntimeType { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public decimal Duration { get; set; }
    public Guid? RelatedWorkOrderId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal EstimatedCostImpact { get; set; }
    public string Status { get; set; } = string.Empty;

    public MaintenanceAssetDto? Asset { get; set; }
    public WorkOrderDto? WorkOrder { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateAssetDowntimeDto
{
    [Required]
    public Guid AssetId { get; set; }

    public Guid? WorkOrderId { get; set; }

    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    [Required]
    [StringLength(50)]
    public string DowntimeType { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Priority { get; set; } = string.Empty;

    public Guid? RelatedWorkOrderId { get; set; }

    [Required]
    [StringLength(50)]
    public string Reason { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public decimal EstimatedCostImpact { get; set; }
}

public class UpdateAssetDowntimeDto
{
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    [Required]
    [StringLength(50)]
    public string Reason { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public decimal EstimatedCostImpact { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = string.Empty;
}

public class DowntimeAnalyticsDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public double TotalDowntimeHours { get; set; }
    public decimal TotalCostImpact { get; set; }
    public double AverageDowntimePerIncident { get; set; }
    public int TotalIncidents { get; set; }
    public Dictionary<string, double> DowntimeByReason { get; set; } = new();
    public Dictionary<string, double> DowntimeByAsset { get; set; } = new();
    public Dictionary<string, int> IncidentsByReason { get; set; } = new();
    public double OverallAvailabilityPercentage { get; set; }
}

public class AssetDowntimeReportDto
{
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public string AssetNumber { get; set; } = string.Empty;
    public double TotalDowntimeHours { get; set; }
    public decimal TotalCostImpact { get; set; }
    public int IncidentCount { get; set; }
    public double AverageIncidentDuration { get; set; }
    public double AvailabilityPercentage { get; set; }
    public string MostCommonFailureReason { get; set; } = string.Empty;
}

#endregion

#region Report Filter DTOs

public class WorkOrderReportFilterDto
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid? AssetId { get; set; }
    public string? Status { get; set; }
    public Guid? WorkOrderTypeId { get; set; }
    public Guid? MaintenanceTypeId { get; set; }
    public Guid? AssignedTechnicianId { get; set; }
    public Guid? AssignedTeamId { get; set; }
    public string Format { get; set; } = "PDF"; // PDF, Excel, CSV
}

public class AssetReportFilterDto
{
    public Guid? AssetCategoryId { get; set; }
    public string? Status { get; set; }
    public string? Criticality { get; set; }
    public string? Location { get; set; }
    public bool IncludeMaintenanceHistory { get; set; }
    public bool IncludeWorkOrders { get; set; }
    public string Format { get; set; } = "PDF";
}

public class ScheduleReportFilterDto
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? MaintenanceTypeId { get; set; }
    public string? ScheduleType { get; set; }
    public bool ActiveOnly { get; set; } = true;
    public string Format { get; set; } = "PDF";
}

public class DowntimeReportFilterDto
{
    public DateTime StartDate { get; set; } = DateTime.UtcNow.AddMonths(-1);
    public DateTime EndDate { get; set; } = DateTime.UtcNow;
    public Guid? AssetId { get; set; }
    public string? Reason { get; set; }
    public bool IncludeCostAnalysis { get; set; } = true;
    public string Format { get; set; } = "PDF";
}

public class InspectionReportFilterDto
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? InspectionTemplateId { get; set; }
    public string? InspectionType { get; set; }
    public string? OverallResult { get; set; }
    public bool RegulatoryOnly { get; set; }
    public string Format { get; set; } = "PDF";
}

public class CostReportFilterDto
{
    public DateTime StartDate { get; set; } = DateTime.UtcNow.AddMonths(-12);
    public DateTime EndDate { get; set; } = DateTime.UtcNow;
    public Guid? AssetId { get; set; }
    public Guid? AssetCategoryId { get; set; }
    public string? CostType { get; set; } // Labor, Parts, Total
    public bool IncludeBudgetComparison { get; set; }
    public string Format { get; set; } = "Excel";
}

#endregion

#region Asset Type DTOs

/// <summary>
/// Asset type information for lists and selections
/// </summary>
public class AssetTypeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public bool IsActive { get; set; }
    
    // Configuration flags
    public bool RequiresLocation { get; set; }
    public bool RequiresOperatingHours { get; set; }
    public bool RequiresMileageTracking { get; set; }
    public bool RequiresLicensing { get; set; }
    public bool RequiresInspections { get; set; }
    public bool SupportsHierarchy { get; set; }
    public bool RequiresSpecializedFields { get; set; }
    
    // Maintenance Configuration
    public int DefaultMaintenanceIntervalDays { get; set; }
    public bool RequiresPreventiveMaintenance { get; set; }
    public bool RequiresConditionMonitoring { get; set; }
    
    // Safety and Compliance
    public bool RequiresSafetyChecks { get; set; }
    public bool RequiresLockoutTagout { get; set; }
    public bool RequiresPermits { get; set; }
    
    // Workflow Configuration
    public int DefaultWorkOrderPriority { get; set; }
    public double DefaultEstimatedHours { get; set; }
    public string? DefaultWorkInstructions { get; set; }
    
    // Custom fields configuration
    public string? CustomFieldsConfig { get; set; }
    
    // Asset count for this type
    public int AssetCount { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// DTO for creating new asset types
/// </summary>
public class CreateAssetTypeDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string? Description { get; set; }
    
    [StringLength(7)] // Hex color code
    public string? Color { get; set; }
    
    [StringLength(50)]
    public string? Icon { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // Configuration flags
    public bool RequiresLocation { get; set; } = true;
    public bool RequiresOperatingHours { get; set; } = false;
    public bool RequiresMileageTracking { get; set; } = false;
    public bool RequiresLicensing { get; set; } = false;
    public bool RequiresInspections { get; set; } = false;
    public bool SupportsHierarchy { get; set; } = false;
    public bool RequiresSpecializedFields { get; set; } = false;
    
    // Maintenance Configuration
    public int DefaultMaintenanceIntervalDays { get; set; } = 90;
    public bool RequiresPreventiveMaintenance { get; set; } = true;
    public bool RequiresConditionMonitoring { get; set; } = false;
    
    // Safety and Compliance
    public bool RequiresSafetyChecks { get; set; } = false;
    public bool RequiresLockoutTagout { get; set; } = false;
    public bool RequiresPermits { get; set; } = false;
    
    // Workflow Configuration
    [Range(1, 5)]
    public int DefaultWorkOrderPriority { get; set; } = 3; // 1=Critical, 5=Low
    
    [Range(0.1, 9999.0)]
    public double DefaultEstimatedHours { get; set; } = 2.0;
    
    [StringLength(2000)]
    public string? DefaultWorkInstructions { get; set; }
    
    // Custom Fields Configuration (JSON)
    public string? CustomFieldsConfig { get; set; }
}

/// <summary>
/// DTO for updating asset types
/// </summary>
public class UpdateAssetTypeDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(50)]
    public string Code { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string? Description { get; set; }
    
    [StringLength(7)] // Hex color code
    public string? Color { get; set; }
    
    [StringLength(50)]
    public string? Icon { get; set; }
    
    public bool IsActive { get; set; }
    
    // Configuration flags
    public bool RequiresLocation { get; set; }
    public bool RequiresOperatingHours { get; set; }
    public bool RequiresMileageTracking { get; set; }
    public bool RequiresLicensing { get; set; }
    public bool RequiresInspections { get; set; }
    public bool SupportsHierarchy { get; set; }
    public bool RequiresSpecializedFields { get; set; }
    
    // Maintenance Configuration
    public int DefaultMaintenanceIntervalDays { get; set; }
    public bool RequiresPreventiveMaintenance { get; set; }
    public bool RequiresConditionMonitoring { get; set; }
    
    // Safety and Compliance
    public bool RequiresSafetyChecks { get; set; }
    public bool RequiresLockoutTagout { get; set; }
    public bool RequiresPermits { get; set; }
    
    // Workflow Configuration
    [Range(1, 5)]
    public int DefaultWorkOrderPriority { get; set; }
    
    [Range(0.1, 9999.0)]
    public double DefaultEstimatedHours { get; set; }
    
    [StringLength(2000)]
    public string? DefaultWorkInstructions { get; set; }
    
    // Custom Fields Configuration (JSON)
    public string? CustomFieldsConfig { get; set; }
}

#endregion

#region Attachment DTOs

/// <summary>
/// Maintenance attachment information
/// </summary>
public class MaintenanceAttachmentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? Description { get; set; }
    public string AttachmentType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public DateTime UploadedDate { get; set; }
    public string UploadedByUserName { get; set; } = string.Empty;
    public bool IsMainImage { get; set; }
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    public string? ThumbnailPath { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? LocationDescription { get; set; }
    public string? DocumentVersion { get; set; }
    public List<AttachmentTagDto> Tags { get; set; } = new List<AttachmentTagDto>();
    public string FileSizeFormatted => FormatFileSize(FileSizeBytes);

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

/// <summary>
/// DTO for creating maintenance attachments
/// </summary>
public class CreateMaintenanceAttachmentDto
{
    [Required]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    public string FilePath { get; set; } = string.Empty;
    
    [Required]
    public string ContentType { get; set; } = string.Empty;
    
    public long FileSizeBytes { get; set; }
    public string? Description { get; set; }
    
    [Required]
    public string AttachmentType { get; set; } = string.Empty;
    
    [Required]
    public string EntityType { get; set; } = string.Empty;
    
    [Required]
    public Guid EntityId { get; set; }
    
    public bool IsMainImage { get; set; }
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    public string? ThumbnailPath { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? LocationDescription { get; set; }
    public string? Category { get; set; }
}

/// <summary>
/// DTO for updating maintenance attachments
/// </summary>
public class UpdateMaintenanceAttachmentDto
{
    public string? Description { get; set; }
    public bool? IsMainImage { get; set; }
    public string? LocationDescription { get; set; }
    public List<AttachmentTagDto>? Tags { get; set; }
}

/// <summary>
/// Attachment tag information
/// </summary>
public class AttachmentTagDto
{
    public Guid Id { get; set; }
    public string TagName { get; set; } = string.Empty;
    public string? TagValue { get; set; }
}

/// <summary>
/// DTO for adding attachment tags
/// </summary>
public class AddAttachmentTagsDto
{
    [Required]
    public List<AttachmentTagDto> Tags { get; set; } = new();
}

/// <summary>
/// Attachment access log information
/// </summary>
public class AttachmentAccessLogDto
{
    public Guid Id { get; set; }
    public Guid AttachmentId { get; set; }
    public Guid AccessedByUserId { get; set; }
    public string AccessedByUserName { get; set; } = string.Empty;
    public DateTime AccessedDate { get; set; }
    public string AccessType { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
}

#endregion
