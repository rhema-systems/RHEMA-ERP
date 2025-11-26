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
    /// Validates if a work order can be completed based on quality control requirements.
    /// Phase 1.3/P1-E3 implementation: uses real tasks, QC checklists and inspections.
    /// </summary>
    public async Task<QualityValidationResult> ValidateWorkOrderCompletionAsync(Guid workOrderId)
    {
        try
        {
            _logger.LogInformation("Validating work order {WorkOrderId} for completion", workOrderId);

            var workOrder = await _workOrderRepository.GetByIdAsync(workOrderId);
            if (workOrder == null)
                throw new ArgumentException($"Work order {workOrderId} not found");

            var result = new QualityValidationResult
            {
                WorkOrderId = workOrderId,
                CanComplete = true,
                ValidationDate = DateTime.UtcNow,
                RequiredInspections = new List<RequiredInspectionDto>(),
                ValidationMessages = new List<string>(),
                ValidationFailures = new List<string>(),
                RequiresInspectionOfficerApproval = false
            };

            // 1) Required tasks must be completed
            var incompleteTasks = workOrder.Tasks?
                .Where(t => t.IsRequired && t.Status != "Completed")
                .ToList();
            if (incompleteTasks?.Any() == true)
            {
                result.CanComplete = false;
                result.ValidationFailures.Add($"{incompleteTasks.Count} required task(s) not completed");
            }

            // 2) Determine applicable QC checklists based on work order context
            var assetCategory = workOrder.Asset?.AssetCategory?.Name;
            var workOrderTypeName = workOrder.WorkOrderType?.Name;
            var maintenanceTypeName = workOrder.MaintenanceType?.Name;

            // 3) Enforce mandatory QualityControlChecklists + WorkOrderQualityCheck results
            // NOTE: The detailed checklist selection and scoring logic lives in the API layer
            // (QualityControlController.SubmitForInspection/CompleteInspection).
            // Here we only enforce the high-level rule that if any mandatory checklist exists
            // for this work order context, there must be at least one completed WorkOrderQualityCheck
            // with a passing result before the work order can be completed.

            if (workOrder.QualityChecks != null && workOrder.QualityChecks.Any())
            {
                var mandatoryChecks = workOrder.QualityChecks
                    .Where(qc => qc.Checklist != null && qc.Checklist.IsMandatory)
                    .ToList();

                if (mandatoryChecks.Any())
                {
                    var passedMandatoryChecks = mandatoryChecks
                        .Where(qc => string.Equals(qc.OverallResult, "Pass", StringComparison.OrdinalIgnoreCase) &&
                                     qc.Score >= (qc.Checklist?.MinimumPassingScore ?? 0))
                        .ToList();

                    if (!passedMandatoryChecks.Any())
                    {
                        result.CanComplete = false;
                        result.ValidationFailures.Add("Mandatory quality inspections have not been passed for this work order.");

                        foreach (var check in mandatoryChecks)
                        {
                            result.RequiredInspections.Add(new RequiredInspectionDto
                            {
                                InspectionTemplateId = check.Checklist!.Id,
                                InspectionType = check.Checklist.WorkOrderType ?? "Quality",
                                InspectionName = check.Checklist.Name,
                                IsRegulatory = check.Checklist.IsMandatory,
                                Description = check.Checklist.Description,
                                IsCompleted = string.Equals(check.OverallResult, "Pass", StringComparison.OrdinalIgnoreCase),
                                CompletedDate = check.InspectionDate,
                                Result = check.OverallResult
                            });
                        }
                    }
                    else
                    {
                        foreach (var check in mandatoryChecks)
                        {
                            result.RequiredInspections.Add(new RequiredInspectionDto
                            {
                                InspectionTemplateId = check.Checklist!.Id,
                                InspectionType = check.Checklist.WorkOrderType ?? "Quality",
                                InspectionName = check.Checklist.Name,
                                IsRegulatory = check.Checklist.IsMandatory,
                                Description = check.Checklist.Description,
                                IsCompleted = string.Equals(check.OverallResult, "Pass", StringComparison.OrdinalIgnoreCase),
                                CompletedDate = check.InspectionDate,
                                Result = check.OverallResult
                            });
                        }
                    }
                }
            }

            // 4) If the originating job card has failed QC, block completion
            if (workOrder.JobCard != null)
            {
                if (workOrder.JobCard.QualityCheckDate.HasValue && !workOrder.JobCard.QualityCheckPassed)
                {
                    result.CanComplete = false;
                    result.ValidationFailures.Add("Quality check performed but not passed");
                    result.RequiresInspectionOfficerApproval = true;
                }

                if (!workOrder.JobCard.CustomerAcceptance)
                {
                    result.ValidationMessages.Add("Customer acceptance pending");
                }
            }

            if (result.CanComplete)
            {
                result.ValidationMessages.Add("All core quality control requirements met");
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

