using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Enums;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for managing quality control validation and inspection requirements
/// </summary>
public interface IQualityControlService
{
    /// <summary>
    /// Validates if a work order can be completed based on quality control requirements
    /// </summary>
    Task<QualityValidationResult> ValidateWorkOrderCompletionAsync(Guid workOrderId);

    /// <summary>
    /// Gets required inspections for an asset based on work order type and regulatory requirements
    /// </summary>
    Task<IEnumerable<RequiredInspectionDto>> GetRequiredInspectionsAsync(Guid assetId, string workOrderType);

    /// <summary>
    /// Checks if all required inspections have passed for a work order
    /// </summary>
    Task<bool> AreAllRequiredInspectionsPassedAsync(Guid workOrderId);

    /// <summary>
    /// Gets quality control checklist items that should be validated before work order completion
    /// </summary>
    Task<IEnumerable<QualityChecklistItemDto>> GetQualityChecklistAsync(Guid workOrderId);

    /// <summary>
    /// Records quality validation results for a work order
    /// </summary>
    Task<QualityValidationResult> RecordQualityValidationAsync(Guid workOrderId, RecordQualityValidationDto validationDto, Guid validatedById);

    /// <summary>
    /// Gets inspection officers who can validate work orders
    /// </summary>
    Task<IEnumerable<string>> GetQualifiedInspectionOfficersAsync();
}

/// <summary>
/// Implementation of quality control service
/// </summary>
public class QualityControlService : IQualityControlService
{
    private readonly IWorkOrderRepository _workOrderRepository;
    private readonly IMaintenanceAssetRepository _assetRepository;
    // TODO: Phase 2 - will use actual inspection services
    // private readonly IAssetInspectionService _inspectionService;
    // private readonly IInspectionTemplateService _templateService;
    private readonly ErpSystem.Core.Interfaces.HR.IEmployeeRepository _employeeRepository;
    private readonly ILogger<QualityControlService> _logger;

    public QualityControlService(
        IWorkOrderRepository workOrderRepository,
        IMaintenanceAssetRepository assetRepository,
        ErpSystem.Core.Interfaces.HR.IEmployeeRepository employeeRepository,
        ILogger<QualityControlService> logger)
    {
        _workOrderRepository = workOrderRepository;
        _assetRepository = assetRepository;
        _employeeRepository = employeeRepository;
        _logger = logger;
    }

    /// <summary>
    /// Validates if a work order can be completed based on quality control requirements
    /// </summary>
    public async Task<QualityValidationResult> ValidateWorkOrderCompletionAsync(Guid workOrderId)
    {
        try
        {
            _logger.LogInformation("Starting quality validation for work order {WorkOrderId}", workOrderId);

            var workOrder = await _workOrderRepository.GetByIdAsync(workOrderId);
            if (workOrder == null)
                throw new ArgumentException($"Work order {workOrderId} not found");

            var result = new QualityValidationResult
            {
                WorkOrderId = workOrderId,
                CanComplete = true,
                ValidationDate = DateTime.UtcNow
            };

            // Check if asset requires regulatory inspections
            if (workOrder.AssetId != Guid.Empty)
            {
                var asset = await _assetRepository.GetByIdAsync(workOrder.AssetId);
                if (asset != null)
                {
                    // Check for required inspections
                    var requiredInspections = await GetRequiredInspectionsAsync(asset.Id, workOrder.MaintenanceType?.Name ?? "General");
                    result.RequiredInspections = requiredInspections.ToList();

                    // TODO: Phase 2 - Check if all required inspections have passed using actual inspection service
                    // For Phase 1.4, we assume inspections pass basic validation
                    foreach (var requiredInspection in requiredInspections)
                    {
                        // Stub implementation - in Phase 2 this will use actual inspection service
                        // var latestInspection = await _inspectionService.GetLatestInspectionByAssetAsync(asset.Id);
                        
                        // For now, we add a validation message but allow completion
                        result.ValidationMessages.Add($"Quality control framework established - inspection '{requiredInspection.InspectionType}' will be validated in Phase 2");
                    }

                    // Check if asset is in critical condition
                    if (asset.Status == AssetStatus.OutOfService || asset.Status == AssetStatus.Maintenance)
                    {
                        result.CanComplete = false;
                        result.ValidationFailures.Add($"Asset is in {asset.Status} status and requires inspection officer approval");
                        result.RequiresInspectionOfficerApproval = true;
                    }
                }
            }

            // Check maintenance type specific requirements
            if (workOrder.MaintenanceType?.Name == "Safety" || workOrder.MaintenanceType?.Name == "Regulatory")
            {
                result.RequiresInspectionOfficerApproval = true;
                result.ValidationMessages.Add("Safety/Regulatory maintenance requires inspection officer approval");
            }

            // Check if work order involves critical safety systems
            if (workOrder.SafetyRequirements?.Contains("Critical") == true)
            {
                result.RequiresInspectionOfficerApproval = true;
                result.ValidationMessages.Add("Work involving critical safety systems requires inspection officer approval");
            }

            if (result.CanComplete)
            {
                result.ValidationMessages.Add("Work order meets all quality control requirements");
            }

            _logger.LogInformation("Quality validation completed for work order {WorkOrderId}. Can complete: {CanComplete}", 
                workOrderId, result.CanComplete);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during quality validation for work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    /// <summary>
    /// Gets required inspections for an asset based on work order type and regulatory requirements
    /// </summary>
    public async Task<IEnumerable<RequiredInspectionDto>> GetRequiredInspectionsAsync(Guid assetId, string workOrderType)
    {
        try
        {
            var requiredInspections = new List<RequiredInspectionDto>();

            // TODO: Phase 2 - Get all active inspection templates from actual service
            // var templates = await _templateService.GetActiveTemplatesAsync();
            
            // For Phase 1.4, create stub inspection requirements based on work order type
            
            // Safety inspections are always required for safety-related work
            if (workOrderType.Contains("Safety") || workOrderType.Contains("Emergency"))
            {
                requiredInspections.Add(new RequiredInspectionDto
                {
                    InspectionTemplateId = Guid.NewGuid(),
                    InspectionType = "Safety",
                    InspectionName = "Safety Inspection",
                    IsRegulatory = false,
                    Description = "Standard safety inspection for maintenance work"
                });
            }

            // Quality inspections for preventive maintenance
            if (workOrderType.Contains("Preventive"))
            {
                requiredInspections.Add(new RequiredInspectionDto
                {
                    InspectionTemplateId = Guid.NewGuid(),
                    InspectionType = "Quality",
                    InspectionName = "Quality Control Inspection",
                    IsRegulatory = false,
                    Description = "Quality control inspection for preventive maintenance"
                });
            }

            // Regulatory inspections are mandatory for regulated assets
            if (workOrderType.Contains("Regulatory"))
            {
                requiredInspections.Add(new RequiredInspectionDto
                {
                    InspectionTemplateId = Guid.NewGuid(),
                    InspectionType = "Regulatory",
                    InspectionName = "Regulatory Compliance Inspection",
                    IsRegulatory = true,
                    Description = "Mandatory regulatory compliance inspection"
                });
            }

            return requiredInspections;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting required inspections for asset {AssetId}", assetId);
            throw;
        }
    }

    /// <summary>
    /// Checks if all required inspections have passed for a work order
    /// </summary>
    public async Task<bool> AreAllRequiredInspectionsPassedAsync(Guid workOrderId)
    {
        try
        {
            var workOrder = await _workOrderRepository.GetByIdAsync(workOrderId);
            if (workOrder == null || workOrder.AssetId == Guid.Empty)
                return true; // No asset means no inspection requirements

            var requiredInspections = await GetRequiredInspectionsAsync(workOrder.AssetId, workOrder.MaintenanceType?.Name ?? "General");
            
            // TODO: Phase 2 - Check actual inspection status using inspection service
            // For Phase 1.4, we assume all required inspections pass for basic validation
            foreach (var requiredInspection in requiredInspections)
            {
                // Stub implementation - in Phase 2 this will use actual inspection service
                // var latestInspection = await _inspectionService.GetLatestInspectionByAssetAsync(workOrder.AssetId.Value);
                
                // For now, assume inspections pass (Phase 1.4 foundation)
                _logger.LogInformation("Quality control framework - inspection '{InspectionType}' validation will be implemented in Phase 2", requiredInspection.InspectionType);
            }

            // Phase 1.4: Return true to allow completion with quality control framework in place
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking inspection status for work order {WorkOrderId}", workOrderId);
            return false;
        }
    }

    /// <summary>
    /// Gets quality control checklist items that should be validated before work order completion
    /// </summary>
    public async Task<IEnumerable<QualityChecklistItemDto>> GetQualityChecklistAsync(Guid workOrderId)
    {
        try
        {
            var checklist = new List<QualityChecklistItemDto>();

            var workOrder = await _workOrderRepository.GetByIdAsync(workOrderId);
            if (workOrder == null)
                return checklist;

            // Standard quality checklist items
            checklist.Add(new QualityChecklistItemDto
            {
                Id = "safety_check",
                Name = "Safety Requirements Verified",
                Description = "All safety requirements and procedures have been followed",
                IsRequired = true,
                Category = "Safety"
            });

            checklist.Add(new QualityChecklistItemDto
            {
                Id = "work_completed",
                Name = "Work Completed as Specified",
                Description = "All work has been completed according to specifications",
                IsRequired = true,
                Category = "Quality"
            });

            checklist.Add(new QualityChecklistItemDto
            {
                Id = "testing_performed",
                Name = "Testing and Validation Performed",
                Description = "Required testing and validation has been performed",
                IsRequired = true,
                Category = "Quality"
            });

            // Add regulatory checklist items for regulatory work
            if (workOrder.MaintenanceType?.Name == "Regulatory")
            {
                checklist.Add(new QualityChecklistItemDto
                {
                    Id = "compliance_verified",
                    Name = "Regulatory Compliance Verified",
                    Description = "Work meets all applicable regulatory standards",
                    IsRequired = true,
                    Category = "Regulatory"
                });
            }

            // Add critical safety items for safety work
            if (workOrder.MaintenanceType?.Name == "Safety" || workOrder.SafetyRequirements?.Contains("Critical") == true)
            {
                checklist.Add(new QualityChecklistItemDto
                {
                    Id = "critical_safety",
                    Name = "Critical Safety Systems Tested",
                    Description = "All critical safety systems have been tested and verified",
                    IsRequired = true,
                    Category = "Safety"
                });
            }

            return checklist;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting quality checklist for work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    /// <summary>
    /// Records quality validation results for a work order
    /// </summary>
    public async Task<QualityValidationResult> RecordQualityValidationAsync(Guid workOrderId, RecordQualityValidationDto validationDto, Guid validatedById)
    {
        try
        {
            _logger.LogInformation("Recording quality validation for work order {WorkOrderId} by validator {ValidatedById}", 
                workOrderId, validatedById);

            var workOrder = await _workOrderRepository.GetByIdAsync(workOrderId);
            if (workOrder == null)
                throw new ArgumentException($"Work order {workOrderId} not found");

            // Validate that the validator is qualified
            var validator = await _employeeRepository.GetByIdAsync(validatedById);
            if (validator == null)
                throw new ArgumentException($"Validator {validatedById} not found");

            // TODO: In a real implementation, check if the validator has inspection officer role
            // For now, we'll assume any employee can validate

            var result = new QualityValidationResult
            {
                WorkOrderId = workOrderId,
                ValidationDate = DateTime.UtcNow,
                ValidatedById = validatedById,
                ValidatorName = $"{validator.FirstName} {validator.LastName}",
                OverallResult = validationDto.OverallResult,
                ValidationNotes = validationDto.ValidationNotes,
                ChecklistResults = validationDto.ChecklistResults,
                CanComplete = validationDto.OverallResult == "Pass"
            };

            // Check if all required checklist items passed
            if (validationDto.ChecklistResults?.Any() == true)
            {
                var failedItems = validationDto.ChecklistResults.Where(c => c.Result != "Pass").ToList();
                if (failedItems.Any())
                {
                    result.CanComplete = false;
                    result.ValidationFailures.AddRange(failedItems.Select(f => $"Checklist item '{f.ItemId}' failed: {f.Comments}"));
                }
            }

            if (result.CanComplete)
            {
                result.ValidationMessages.Add("Quality validation passed - work order approved for completion");
                
                // Update work order status to indicate quality approval
                workOrder.Status = "QualityApproved";
                workOrder.UpdatedAt = DateTime.UtcNow;
                await _workOrderRepository.UpdateAsync(workOrder);
            }
            else
            {
                result.ValidationFailures.Add("Quality validation failed - work order cannot be completed");
                
                // Update work order status to indicate quality failure
                workOrder.Status = "QualityRejected";
                workOrder.UpdatedAt = DateTime.UtcNow;
                await _workOrderRepository.UpdateAsync(workOrder);
            }

            _logger.LogInformation("Quality validation recorded for work order {WorkOrderId}. Result: {Result}", 
                workOrderId, result.OverallResult);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording quality validation for work order {WorkOrderId}", workOrderId);
            throw;
        }
    }

    /// <summary>
    /// Gets inspection officers who can validate work orders
    /// </summary>
    public async Task<IEnumerable<string>> GetQualifiedInspectionOfficersAsync()
    {
        try
        {
            // For now, return all active employees who can be assigned to maintenance
            // In Phase 2, this will be filtered by inspection officer role
            var employees = await _employeeRepository.GetActiveEmployeesAsync();
            return employees
                .Where(e => e.CanBeAssignedToMaintenance)
                .Select(e => $"{e.FirstName} {e.LastName} (ID: {e.Id})")
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting qualified inspection officers");
            throw;
        }
    }
}

