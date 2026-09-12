using System.Text.Json;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Shared;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for managing job card lifecycle and approval workflow
/// </summary>
public class JobCardService : IJobCardService
{
    private const string WorkOrderBillingTypeField = "workOrderBillingType";

    private readonly IJobCardRepository _jobCardRepository;
    private readonly IWorkOrderService _workOrderService;
    private readonly IMaintenanceAssetRepository _assetRepository;
    private readonly IMaintenanceTypeRepository _maintenanceTypeRepository;
    private readonly ErpSystem.Core.Interfaces.HR.IEmployeeRepository _employeeRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorageService;
    private readonly IMaintenanceNotificationService _maintenanceNotificationService;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly IAssetConditionService _assetConditionService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<JobCardService> _logger;
    private readonly IAppEventBus _appEventBus;

    public JobCardService(
        IJobCardRepository jobCardRepository,
        IWorkOrderService workOrderService,
        IMaintenanceAssetRepository assetRepository,
        IMaintenanceTypeRepository maintenanceTypeRepository,
        ErpSystem.Core.Interfaces.HR.IEmployeeRepository employeeRepository,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorageService,
        IMaintenanceNotificationService maintenanceNotificationService,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        IAssetConditionService assetConditionService,
        IUnitOfWork unitOfWork,
        ILogger<JobCardService> logger,
        IAppEventBus appEventBus)
    {
        _jobCardRepository = jobCardRepository;
        _workOrderService = workOrderService;
        _assetRepository = assetRepository;
        _maintenanceTypeRepository = maintenanceTypeRepository;
        _employeeRepository = employeeRepository;
        _currentUserService = currentUserService;
        _fileStorageService = fileStorageService;
        _maintenanceNotificationService = maintenanceNotificationService;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _assetConditionService = assetConditionService;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _appEventBus = appEventBus;
    }

    private static string NormalizeWorkOrderBillingType(string? value)
    {
        return string.Equals(value, "Maintenance", StringComparison.OrdinalIgnoreCase)
            ? "Maintenance"
            : "Repairs";
    }

    private static string? GetStoredWorkOrderBillingType(string? customFieldValues)
    {
        if (string.IsNullOrWhiteSpace(customFieldValues))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(customFieldValues);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (document.RootElement.TryGetProperty(WorkOrderBillingTypeField, out var billingType) &&
                billingType.ValueKind == JsonValueKind.String)
            {
                return NormalizeWorkOrderBillingType(billingType.GetString());
            }

            if (document.RootElement.TryGetProperty("billingType", out var legacyBillingType) &&
                legacyBillingType.ValueKind == JsonValueKind.String)
            {
                return NormalizeWorkOrderBillingType(legacyBillingType.GetString());
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string ResolveWorkOrderBillingType(string? requestedBillingType, string? customFieldValues)
    {
        if (!string.IsNullOrWhiteSpace(requestedBillingType))
        {
            return NormalizeWorkOrderBillingType(requestedBillingType);
        }

        return GetStoredWorkOrderBillingType(customFieldValues) ?? "Repairs";
    }

    private static string? SerializeCustomFields(
        Dictionary<string, object>? customFieldValues,
        string? workOrderBillingType)
    {
        var fields = customFieldValues != null
            ? new Dictionary<string, object>(customFieldValues, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        var effectiveBillingType = workOrderBillingType;
        if (string.IsNullOrWhiteSpace(effectiveBillingType) &&
            fields.TryGetValue(WorkOrderBillingTypeField, out var storedBillingType) &&
            storedBillingType != null)
        {
            effectiveBillingType = storedBillingType.ToString();
        }

        fields[WorkOrderBillingTypeField] = NormalizeWorkOrderBillingType(effectiveBillingType);

        return fields.Count == 0 ? null : JsonSerializer.Serialize(fields);
    }

    public async Task<PagedResult<JobCardListDto>> GetJobCardsPagedAsync(JobCardFilterDto filter)
    {
        try
        {
            _logger.LogDebug("Getting paged job cards with filter: {@Filter}", filter);

            var jobCards = await _jobCardRepository.GetPagedAsync(filter);
            return jobCards;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting paged job cards");
            throw;
        }
    }

    public async Task<JobCardDto?> GetJobCardByIdAsync(Guid id)
    {
        try
        {
            var jobCard = await _jobCardRepository.GetByIdWithDetailsAsync(id);
            return jobCard;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job card {JobCardId}", id);
            throw;
        }
    }

    public async Task<JobCardDto?> GetJobCardByNumberAsync(string jobCardNumber)
    {
        try
        {
            var jobCard = await _jobCardRepository.GetByNumberAsync(jobCardNumber);
            return jobCard;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job card by number {JobCardNumber}", jobCardNumber);
            throw;
        }
    }

    public async Task<JobCardDto> CreateJobCardAsync(CreateJobCardDto createDto)
    {
        try
        {
            _logger.LogInformation("Creating new job card for asset {AssetId}", createDto.AssetId);

            // Validate asset exists
            var asset = await _assetRepository.GetByIdAsync(createDto.AssetId) ?? throw new ArgumentException($"Asset with ID {createDto.AssetId} not found");
            var currentUserIdString = _currentUserService.UserId;
            if (string.IsNullOrEmpty(currentUserIdString))
            {
                throw new UnauthorizedAccessException("User not authenticated");
            }

            var currentUserId = Guid.Parse(currentUserIdString);
            var tenantId = _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Tenant not found");

            // Get the Employee ID from the current user's claims
            var currentEmployeeId = _currentUserService.EmployeeId;
            if (!currentEmployeeId.HasValue)
            {
                throw new InvalidOperationException("Current user does not have an associated employee record. Please link your user account to an employee record.");
            }

            // Validate that the current employee exists in the Employees table
            var currentEmployee = await _employeeRepository.GetByIdAsync(currentEmployeeId.Value) ?? throw new InvalidOperationException($"Employee with ID {currentEmployeeId.Value} is not found in the Employees table. Please ensure the employee record exists.");

            // Generate job card number
            var jobCardNumber = await GenerateJobCardNumberAsync();
            await ValidateCustomerBusinessPartnerAsync(createDto.CustomerBusinessPartnerId, tenantId);

            var jobCard = new JobCard
            {
                Id = Guid.NewGuid(),
                JobCardNumber = jobCardNumber,
                Title = createDto.Title,
                Description = createDto.Description,
                ProblemDescription = createDto.ProblemDescription,
                AssetId = createDto.AssetId,
                MaintenanceTypeId = createDto.MaintenanceTypeId,
                PriorityLevelId = createDto.PriorityLevelId,
                CustomerBusinessPartnerId = createDto.CustomerBusinessPartnerId,
                MaintenanceLocation = createDto.MaintenanceLocation,
                RequestedById = currentEmployeeId.Value,
                RequestedDate = DateTime.UtcNow,
                RequiredCompletionDate = createDto.RequiredCompletionDate,
                EstimatedHours = createDto.EstimatedHours,
                EstimatedCost = createDto.EstimatedCost,
                PreferredTechnicianId = createDto.PreferredTechnicianId,
                PreferredTeamId = createDto.PreferredTeamId,
                ContractorId = createDto.ContractorId,
                RequiresSpecialTools = createDto.RequiresSpecialTools,
                RequiresShutdown = createDto.RequiresShutdown,
                RequiresSafetyPermit = createDto.RequiresSafetyPermit,
                SpecialInstructions = createDto.SpecialInstructions,
                SafetyRequirements = createDto.SafetyRequirements,
                JobCardStatus = "Draft",
                ApprovalStatus = "NotStarted",
                CustomFieldValues = SerializeCustomFields(createDto.CustomFieldValues, createDto.WorkOrderBillingType),
                TenantId = tenantId,
                CreatedAt = DateTime.UtcNow,
                CreatedById = currentEmployeeId.Value
            };

            await _jobCardRepository.AddAsync(jobCard);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created job card {JobCardNumber} with ID {JobCardId}",
                jobCardNumber, jobCard.Id);

            // Publish event for admin-configurable notification topics (best-effort).
            try
            {
                await _appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = tenantId,
                    EntityType = "JobCard",
                    Activity = "Created",
                    Audience = "Internal",
                    EntityId = jobCard.Id,
                    TriggeredByUserId = currentUserId,
                    Data = new Dictionary<string, object>
                    {
                        ["JobCardId"] = jobCard.Id,
                        ["JobCardNumber"] = jobCard.JobCardNumber ?? string.Empty,
                        ["Title"] = jobCard.Title ?? string.Empty,
                        ["Status"] = jobCard.JobCardStatus ?? string.Empty,
                        ["AssetId"] = jobCard.AssetId,
                        ["RequestedById"] = jobCard.RequestedById,
                        ["CreatedByUserId"] = currentUserId
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish JobCard.Created entity activity event for job card {JobCardId}", jobCard.Id);
            }

            return await GetJobCardByIdAsync(jobCard.Id) ??
                throw new InvalidOperationException("Failed to retrieve created job card");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating job card");
            throw;
        }
    }

    public async Task<JobCardDto> UpdateJobCardAsync(Guid id, UpdateJobCardDto updateDto)
    {
        try
        {
            var existingJobCard = await _jobCardRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Job card with ID {id} not found");

            // Only allow updates if job card is in draft status
            if (existingJobCard.JobCardStatus != "Draft")
            {
                throw new InvalidOperationException($"Cannot update job card in {existingJobCard.JobCardStatus} status");
            }

            // Check permissions
            var currentEmployeeId = _currentUserService.EmployeeId;
            if (!currentEmployeeId.HasValue)
            {
                throw new UnauthorizedAccessException("Current user does not have an associated employee record");
            }

            if (existingJobCard.RequestedById != currentEmployeeId.Value)
            {
                throw new UnauthorizedAccessException("User does not have permission to update this job card");
            }

            // Log before update
            _logger.LogInformation("Updating JobCard {JobCardId} - Before: ProblemDescription='{OldValue}'",
                id, existingJobCard.ProblemDescription);
            _logger.LogInformation("Updating JobCard {JobCardId} - New: ProblemDescription='{NewValue}'",
                id, updateDto.ProblemDescription);

            // Update properties
            existingJobCard.Title = updateDto.Title;
            existingJobCard.Description = updateDto.Description;
            existingJobCard.ProblemDescription = updateDto.ProblemDescription;
            existingJobCard.MaintenanceTypeId = updateDto.MaintenanceTypeId;
            existingJobCard.PriorityLevelId = updateDto.PriorityLevelId;
            await ValidateCustomerBusinessPartnerAsync(updateDto.CustomerBusinessPartnerId, existingJobCard.TenantId);
            existingJobCard.CustomerBusinessPartnerId = updateDto.CustomerBusinessPartnerId;
            existingJobCard.MaintenanceLocation = updateDto.MaintenanceLocation ?? "Internal";
            existingJobCard.RequiredCompletionDate = updateDto.RequiredCompletionDate;
            existingJobCard.EstimatedHours = updateDto.EstimatedHours;
            existingJobCard.EstimatedCost = updateDto.EstimatedCost;
            existingJobCard.PreferredTechnicianId = updateDto.PreferredTechnicianId;
            existingJobCard.PreferredTeamId = updateDto.PreferredTeamId;
            existingJobCard.ContractorId = updateDto.ContractorId;
            existingJobCard.RequiresSpecialTools = updateDto.RequiresSpecialTools;
            existingJobCard.RequiresShutdown = updateDto.RequiresShutdown;
            existingJobCard.RequiresSafetyPermit = updateDto.RequiresSafetyPermit;
            existingJobCard.SpecialInstructions = updateDto.SpecialInstructions;
            existingJobCard.SafetyRequirements = updateDto.SafetyRequirements;
            existingJobCard.CustomFieldValues = SerializeCustomFields(updateDto.CustomFieldValues, updateDto.WorkOrderBillingType);
            existingJobCard.UpdatedAt = DateTime.UtcNow;
            var currentUserIdString = _currentUserService.UserId;
            existingJobCard.UpdatedBy = currentUserIdString ?? "Unknown";

            await _jobCardRepository.UpdateAsync(existingJobCard);

            // Log after setting properties
            _logger.LogInformation("JobCard {JobCardId} - After property update: ProblemDescription='{Value}'",
                id, existingJobCard.ProblemDescription);

            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Updated job card {JobCardId} and saved to database", id);

            return await GetJobCardByIdAsync(id) ??
                throw new InvalidOperationException("Failed to retrieve updated job card");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating job card {JobCardId}", id);
            throw;
        }
    }

    public async Task DeleteJobCardAsync(Guid id)
    {
        try
        {
            var jobCard = await _jobCardRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Job card with ID {id} not found");

            // Only allow deletion if not submitted for approval
            if (jobCard.JobCardStatus != "Draft")
            {
                throw new InvalidOperationException($"Cannot delete job card in {jobCard.JobCardStatus} status");
            }

            // Check permissions
            var currentEmployeeId = _currentUserService.EmployeeId;
            if (!currentEmployeeId.HasValue)
            {
                throw new UnauthorizedAccessException("Current user does not have an associated employee record");
            }

            if (jobCard.RequestedById != currentEmployeeId.Value)
            {
                throw new UnauthorizedAccessException("User does not have permission to delete this job card");
            }

            await _jobCardRepository.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Deleted job card {JobCardId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting job card {JobCardId}", id);
            throw;
        }
    }

    private async Task ValidateCustomerBusinessPartnerAsync(Guid? customerBusinessPartnerId, Guid tenantId)
    {
        if (!customerBusinessPartnerId.HasValue)
        {
            return;
        }

        var partner = await _unitOfWork.Repository<BusinessPartner>()
            .FirstOrDefaultAsync(bp => bp.Id == customerBusinessPartnerId.Value
                && bp.TenantId == tenantId
                && !bp.IsDeleted);

        if (partner == null)
        {
            throw new ArgumentException($"Customer business partner with ID {customerBusinessPartnerId.Value} not found");
        }

        var isCustomer =
            string.Equals(partner.PartnerType, "Customer", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(partner.PartnerType, "Both", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(partner.CustomerType) ||
            !string.IsNullOrWhiteSpace(partner.CustomerAccountNumber);

        if (!isCustomer)
        {
            throw new InvalidOperationException($"Business partner {partner.PartnerName} is not configured as a customer");
        }

        if (!partner.IsActive || partner.IsBlacklisted)
        {
            throw new InvalidOperationException($"Business partner {partner.PartnerName} is not available for customer billing");
        }
    }

    public async Task<JobCardDto> SubmitJobCardAsync(Guid id, SubmitJobCardDto submitDto)
    {
        try
        {
            var jobCard = await _jobCardRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Job card with ID {id} not found");
            if (jobCard.JobCardStatus != "Draft")
            {
                throw new InvalidOperationException($"Job card is already submitted or processed");
            }

            // Check permissions
            var currentEmployeeId = _currentUserService.EmployeeId;
            if (!currentEmployeeId.HasValue)
            {
                throw new UnauthorizedAccessException("Current user does not have an associated employee record");
            }

            if (jobCard.RequestedById != currentEmployeeId.Value)
            {
                throw new UnauthorizedAccessException("User does not have permission to submit this job card");
            }

            var currentUserIdString = _currentUserService.UserId;

            // Start approval workflow
            var workflowResult = await _workflowIntegrationService.SubmitAsync("JobCard", id);
            var statusAdapter = _workflowStatusAdapterRegistry.GetAdapter("JobCard");
            statusAdapter.ApplySubmitOutcome(jobCard, workflowResult, currentEmployeeId.Value);

            jobCard.SubmittedDate = DateTime.UtcNow;
            jobCard.SubmittedById = currentEmployeeId.Value;
            jobCard.UpdatedAt = DateTime.UtcNow;
            jobCard.UpdatedBy = currentUserIdString ?? "Unknown";

            await _jobCardRepository.UpdateAsync(jobCard);
            await _unitOfWork.SaveChangesAsync();

            // Send notification to approvers
            await _maintenanceNotificationService.NotifyJobCardSubmittedAsync(id);

            // Publish event for admin-configurable notification topics (best-effort).
            try
            {
                var triggeredBy = !string.IsNullOrWhiteSpace(currentUserIdString) && Guid.TryParse(currentUserIdString, out var uid) ? uid : (Guid?)null;
                await _appEventBus.PublishAsync(new EntityActivityEvent
                {
                    TenantId = jobCard.TenantId,
                    EntityType = "JobCard",
                    Activity = "Submitted",
                    Audience = "Internal",
                    EntityId = jobCard.Id,
                    TriggeredByUserId = triggeredBy,
                    Data = new Dictionary<string, object>
                    {
                        ["JobCardId"] = jobCard.Id,
                        ["JobCardNumber"] = jobCard.JobCardNumber ?? string.Empty,
                        ["Title"] = jobCard.Title ?? string.Empty,
                        ["Status"] = jobCard.JobCardStatus ?? string.Empty,
                        ["ApprovalStatus"] = jobCard.ApprovalStatus ?? string.Empty,
                        ["SubmittedById"] = jobCard.SubmittedById ?? Guid.Empty,
                        ["SubmittedDate"] = jobCard.SubmittedDate?.ToString("o") ?? string.Empty
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish JobCard.Submitted entity activity event for job card {JobCardId}", jobCard.Id);
            }

            // Add submission comment
            if (!string.IsNullOrEmpty(submitDto.SubmissionNotes))
            {
                await AddCommentAsync(id, new AddJobCardCommentDto
                {
                    Comment = submitDto.SubmissionNotes,
                    CommentType = "Submission",
                    IsInternal = false
                });
            }

            _logger.LogInformation("Submitted job card {JobCardId} for approval", id);

            return await GetJobCardByIdAsync(id) ??
                throw new InvalidOperationException("Failed to retrieve submitted job card");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting job card {JobCardId}", id);
            throw;
        }
    }

    public async Task<JobCardDto> ProcessApprovalAsync(Guid id, JobCardApprovalActionDto approvalDto)
    {
        try
        {
            var jobCard = await _jobCardRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Job card with ID {id} not found");
            if (jobCard.JobCardStatus != "Submitted" && jobCard.ApprovalStatus != "Pending")
            {
                throw new InvalidOperationException("Job card is not awaiting approval");
            }

            var currentUserIdString = _currentUserService.UserId;
            if (string.IsNullOrEmpty(currentUserIdString))
            {
                throw new UnauthorizedAccessException("User not authenticated");
            }

            if (!Guid.TryParse(currentUserIdString, out var currentUserId))
            {
                throw new UnauthorizedAccessException("Current user identifier is invalid");
            }

            var currentEmployeeId = _currentUserService.EmployeeId;
            if (!currentEmployeeId.HasValue)
            {
                throw new UnauthorizedAccessException("Current user does not have an associated employee record");
            }

            var canApprove = await _workflowIntegrationService.CanUserApproveAsync("JobCard", id, currentUserId);
            if (!canApprove && approvalDto.Action.ToLower() != "requestchanges")
                throw new UnauthorizedAccessException("User does not have permission to approve this job card");

            var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
                "JobCard",
                id,
                currentUserId,
                approvalDto.Action,
                approvalDto.Comments);

            if (approvalDto.Action.Equals("requestchanges", StringComparison.OrdinalIgnoreCase))
            {
                await RequestChangesJobCardAsync(jobCard, approvalDto, currentEmployeeId.Value);
                await _workflowIntegrationService.CancelWorkflowAsync("JobCard", id, "Changes requested");
                return await GetJobCardByIdAsync(id) ??
                    throw new InvalidOperationException("Failed to retrieve processed job card");
            }

            if (workflowResult.Outcome == WorkflowOutcome.Approved)
            {
                await ApproveJobCardAsync(jobCard, approvalDto, currentEmployeeId.Value);
            }
            else if (workflowResult.Outcome == WorkflowOutcome.Rejected)
            {
                await RejectJobCardAsync(jobCard, approvalDto, currentEmployeeId.Value);
            }
            else
            {
                var statusAdapter = _workflowStatusAdapterRegistry.GetAdapter("JobCard");
                statusAdapter.ApplyApprovalOutcome(jobCard, workflowResult.Outcome, currentEmployeeId.Value);
                jobCard.UpdatedAt = DateTime.UtcNow;
                jobCard.UpdatedBy = _currentUserService.UserId ?? "System";
                await _jobCardRepository.UpdateAsync(jobCard);
                await _unitOfWork.SaveChangesAsync();
            }

            _logger.LogInformation("Processed approval action {Action} for job card {JobCardId}",
                approvalDto.Action, id);

            return await GetJobCardByIdAsync(id) ??
                throw new InvalidOperationException("Failed to retrieve processed job card");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing approval for job card {JobCardId}", id);
            throw;
        }
    }

    private async Task ApproveJobCardAsync(JobCard jobCard, JobCardApprovalActionDto approvalDto, Guid approverId)
    {
        // Update job card status
        var statusAdapter = _workflowStatusAdapterRegistry.GetAdapter("JobCard");
        statusAdapter.ApplyApprovalOutcome(jobCard, WorkflowOutcome.Approved, approverId);

        // Update planning details if provided
        if (approvalDto.PlannedStartDate.HasValue)
        {
            jobCard.PlannedStartDate = approvalDto.PlannedStartDate;
        }

        if (approvalDto.PlannedEndDate.HasValue)
        {
            jobCard.PlannedEndDate = approvalDto.PlannedEndDate;
        }

        if (approvalDto.AssignedTechnicianId.HasValue)
        {
            jobCard.AssignedTechnicianId = approvalDto.AssignedTechnicianId;
        }

        if (approvalDto.AssignedTeamId.HasValue)
        {
            jobCard.AssignedTeamId = approvalDto.AssignedTeamId;
        }

        // Update revised estimates if provided
        if (approvalDto.RevisedEstimatedHours.HasValue)
        {
            jobCard.EstimatedHours = approvalDto.RevisedEstimatedHours.Value;
        }

        if (approvalDto.RevisedEstimatedCost.HasValue)
        {
            jobCard.EstimatedCost = approvalDto.RevisedEstimatedCost.Value;
        }

        jobCard.UpdatedAt = DateTime.UtcNow;
        jobCard.UpdatedBy = _currentUserService.UserId ?? "System";

        await _jobCardRepository.UpdateAsync(jobCard);
        await _unitOfWork.SaveChangesAsync();

        // Add approval comment
        if (!string.IsNullOrEmpty(approvalDto.Comments))
        {
            await AddCommentAsync(jobCard.Id, new AddJobCardCommentDto
            {
                Comment = approvalDto.Comments,
                CommentType = "Approval",
                IsInternal = false
            });
        }

        // Automatically generate work order from approved job card with billing type
        await GenerateWorkOrderAsync(jobCard.Id, ResolveWorkOrderBillingType(approvalDto.BillingType, jobCard.CustomFieldValues));

        // Notify requestor
        await _maintenanceNotificationService.NotifyJobCardApprovedAsync(jobCard.Id);

        // Publish event for admin-configurable notification topics (best-effort).
        try
        {
            var triggeredBy = !string.IsNullOrWhiteSpace(_currentUserService.UserId) && Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : (Guid?)null;
            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = jobCard.TenantId,
                EntityType = "JobCard",
                Activity = "Approved",
                Audience = "Internal",
                EntityId = jobCard.Id,
                TriggeredByUserId = triggeredBy,
                Data = new Dictionary<string, object>
                {
                    ["JobCardId"] = jobCard.Id,
                    ["JobCardNumber"] = jobCard.JobCardNumber ?? string.Empty,
                    ["Title"] = jobCard.Title ?? string.Empty,
                    ["Status"] = jobCard.JobCardStatus ?? string.Empty,
                    ["ApprovalStatus"] = jobCard.ApprovalStatus ?? string.Empty,
                    ["ApprovedById"] = approverId,
                    ["AssignedTechnicianId"] = jobCard.AssignedTechnicianId ?? Guid.Empty,
                    ["AssignedTeamId"] = jobCard.AssignedTeamId ?? Guid.Empty
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish JobCard.Approved entity activity event for job card {JobCardId}", jobCard.Id);
        }
    }

    private async Task RejectJobCardAsync(JobCard jobCard, JobCardApprovalActionDto approvalDto, Guid approverId)
    {
        var statusAdapter = _workflowStatusAdapterRegistry.GetAdapter("JobCard");
        statusAdapter.ApplyApprovalOutcome(jobCard, WorkflowOutcome.Rejected, approverId);
        jobCard.UpdatedAt = DateTime.UtcNow;
        jobCard.UpdatedBy = _currentUserService.UserId ?? "System";

        await _jobCardRepository.UpdateAsync(jobCard);
        await _unitOfWork.SaveChangesAsync();

        // Add rejection comment
        await AddCommentAsync(jobCard.Id, new AddJobCardCommentDto
        {
            Comment = approvalDto.Comments ?? "Job card rejected",
            CommentType = "Rejection",
            IsInternal = false
        });

        // Notify requestor
        await _maintenanceNotificationService.NotifyJobCardRejectedAsync(jobCard.Id, approvalDto.Comments);

        // Publish event for admin-configurable notification topics (best-effort).
        try
        {
            var triggeredBy = !string.IsNullOrWhiteSpace(_currentUserService.UserId) && Guid.TryParse(_currentUserService.UserId, out var uid) ? uid : (Guid?)null;
            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = jobCard.TenantId,
                EntityType = "JobCard",
                Activity = "Rejected",
                Audience = "Internal",
                EntityId = jobCard.Id,
                TriggeredByUserId = triggeredBy,
                Data = new Dictionary<string, object>
                {
                    ["JobCardId"] = jobCard.Id,
                    ["JobCardNumber"] = jobCard.JobCardNumber ?? string.Empty,
                    ["Title"] = jobCard.Title ?? string.Empty,
                    ["Status"] = jobCard.JobCardStatus ?? string.Empty,
                    ["ApprovalStatus"] = jobCard.ApprovalStatus ?? string.Empty,
                    ["RejectedById"] = approverId,
                    ["Comments"] = approvalDto.Comments ?? string.Empty
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish JobCard.Rejected entity activity event for job card {JobCardId}", jobCard.Id);
        }
    }

    private async Task RequestChangesJobCardAsync(JobCard jobCard, JobCardApprovalActionDto approvalDto, Guid approverId)
    {
        jobCard.JobCardStatus = "ChangesRequested";
        jobCard.ApprovalStatus = "ChangesRequested";
        jobCard.UpdatedAt = DateTime.UtcNow;
        jobCard.UpdatedBy = _currentUserService.UserId ?? "System";

        await _jobCardRepository.UpdateAsync(jobCard);
        await _unitOfWork.SaveChangesAsync();

        // Add changes requested comment
        await AddCommentAsync(jobCard.Id, new AddJobCardCommentDto
        {
            Comment = approvalDto.Comments ?? "Changes requested",
            CommentType = "ChangesRequested",
            IsInternal = false
        });

        // Notify requestor
        await _maintenanceNotificationService.NotifyJobCardChangesRequestedAsync(jobCard.Id, approvalDto.Comments);
    }

    public async Task<Guid> GenerateWorkOrderAsync(Guid jobCardId, string? billingType = null)
    {
        try
        {
            var jobCard = await _jobCardRepository.GetByIdAsync(jobCardId) ?? throw new ArgumentException($"Job card with ID {jobCardId} not found");
            if (jobCard.ApprovalStatus != "Approved")
            {
                throw new InvalidOperationException("Only approved job cards can be converted to work orders");
            }

            if (jobCard.GeneratedWorkOrderId.HasValue)
            {
                throw new InvalidOperationException("Work order has already been generated from this job card");
            }

            var resolvedBillingType = ResolveWorkOrderBillingType(billingType, jobCard.CustomFieldValues);

            // Get default work order type based on maintenance type or use a standard one
            _logger.LogDebug("Getting default work order type for maintenance type {MaintenanceTypeId}", jobCard.MaintenanceTypeId);
            var defaultWorkOrderTypeId = await GetDefaultWorkOrderTypeIdAsync(jobCard.MaintenanceTypeId);
            _logger.LogDebug("Using work order type {WorkOrderTypeId}", defaultWorkOrderTypeId);

            // Get the maintenance type to retrieve fixed amount for Maintenance billing type
            decimal fixedAmount = 0;
            if (resolvedBillingType == "Maintenance")
            {
                var maintenanceType = await _maintenanceTypeRepository.GetByIdAsync(jobCard.MaintenanceTypeId);
                if (maintenanceType != null)
                {
                    fixedAmount = maintenanceType.FixedAmount;
                    _logger.LogDebug("Using fixed amount {FixedAmount} from maintenance type {MaintenanceTypeId}", fixedAmount, jobCard.MaintenanceTypeId);
                }
            }

            // Create work order from job card
            _logger.LogDebug("Creating work order DTO for job card {JobCardId} with billing type {BillingType}", jobCardId, resolvedBillingType);
            var createWorkOrderDto = new CreateWorkOrderDto
            {
                Title = jobCard.Title,
                Description = $"Generated from Job Card {jobCard.JobCardNumber}: {jobCard.Description}",
                AssetId = jobCard.AssetId,
                WorkOrderTypeId = defaultWorkOrderTypeId,
                MaintenanceTypeId = jobCard.MaintenanceTypeId,
                PriorityLevelId = jobCard.PriorityLevelId,
                RequestedStartDate = jobCard.PlannedStartDate,
                RequestedCompletionDate = jobCard.RequiredCompletionDate,
                EstimatedCost = jobCard.EstimatedCost,
                EstimatedHours = jobCard.EstimatedHours,
                AssignedTechnicianId = jobCard.AssignedTechnicianId,
                AssignedTeamId = jobCard.AssignedTeamId,
                SafetyRequirements = jobCard.SafetyRequirements,
                RequiresPermit = jobCard.RequiresSafetyPermit,
                RequiresLockout = jobCard.RequiresShutdown,
                JobCardId = jobCardId, // Link the work order to the job card
                BillingType = resolvedBillingType,
                FixedAmount = fixedAmount
            };

            _logger.LogDebug("Calling WorkOrderService.CreateWorkOrderAsync with DTO: {@CreateWorkOrderDto}", createWorkOrderDto);

            try
            {
                var workOrder = await _workOrderService.CreateWorkOrderAsync(createWorkOrderDto) ?? throw new InvalidOperationException("Failed to create work order from job card - service returned null");
                _logger.LogInformation("Successfully created work order {WorkOrderId} from job card {JobCardId}", workOrder.Id, jobCardId);

                // Update job card with generated work order reference
                jobCard.GeneratedWorkOrderId = workOrder.Id;
                jobCard.WorkOrderGeneratedAt = DateTime.UtcNow;
                jobCard.UpdatedAt = DateTime.UtcNow;

                await _jobCardRepository.UpdateAsync(jobCard);
                await _unitOfWork.SaveChangesAsync();

                // Add comment about work order generation
                await AddCommentAsync(jobCardId, new AddJobCardCommentDto
                {
                    Comment = $"Work Order {workOrder.WorkOrderNumber} generated successfully",
                    CommentType = "WorkOrderGenerated",
                    IsInternal = false
                });

                // Create work order tasks from admission checklist (repair/replace items)
                try
                {
                    var tasksCreated = await _assetConditionService.CreateTasksFromAdmissionChecklistAsync(jobCardId, workOrder.Id);
                    if (tasksCreated > 0)
                    {
                        _logger.LogInformation("Created {TaskCount} tasks from admission checklist for work order {WorkOrderId}",
                            tasksCreated, workOrder.Id);

                        await AddCommentAsync(jobCardId, new AddJobCardCommentDto
                        {
                            Comment = $"{tasksCreated} task(s) added to work order from admission checklist (repair/replace items)",
                            CommentType = "TasksCreated",
                            IsInternal = false
                        });
                    }
                }
                catch (Exception checklistEx)
                {
                    // Log but don't fail work order generation if checklist task creation fails
                    _logger.LogWarning(checklistEx, "Failed to create tasks from admission checklist for job card {JobCardId}, work order {WorkOrderId}",
                        jobCardId, workOrder.Id);
                }

                _logger.LogInformation("Generated work order {WorkOrderId} from job card {JobCardId}",
                    workOrder.Id, jobCardId);

                return workOrder.Id;
            }
            catch (Exception workOrderException)
            {
                _logger.LogError(workOrderException, "Failed to create work order from job card {JobCardId}. CreateWorkOrderDto: {@CreateWorkOrderDto}",
                    jobCardId, createWorkOrderDto);
                throw new InvalidOperationException($"Failed to create work order from job card: {workOrderException.Message}", workOrderException);
            }

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating work order from job card {JobCardId}", jobCardId);
            throw;
        }
    }

    public async Task<JobCardCommentDto> AddCommentAsync(Guid id, AddJobCardCommentDto commentDto)
    {
        try
        {
            var currentUserIdString = _currentUserService.UserId;
            if (string.IsNullOrEmpty(currentUserIdString))
            {
                throw new UnauthorizedAccessException("User not authenticated");
            }

            var currentEmployeeId = _currentUserService.EmployeeId;
            if (!currentEmployeeId.HasValue)
            {
                throw new UnauthorizedAccessException("Current user does not have an associated employee record");
            }

            var tenantId = _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Tenant not found");

            var comment = new JobCardComment
            {
                Id = Guid.NewGuid(),
                JobCardId = id,
                CommentById = currentEmployeeId.Value,
                Comment = commentDto.Comment,
                CommentType = commentDto.CommentType,
                IsInternal = commentDto.IsInternal,
                CommentDate = DateTime.UtcNow,
                TenantId = tenantId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = currentUserIdString
            };

            await _jobCardRepository.AddCommentAsync(comment);
            await _unitOfWork.SaveChangesAsync();

            var employee = await _employeeRepository.GetByIdAsync(currentEmployeeId.Value);

            return new JobCardCommentDto
            {
                Id = comment.Id,
                Comment = comment.Comment,
                CommentType = comment.CommentType,
                IsInternal = comment.IsInternal,
                CommentDate = comment.CommentDate,
                CommentBy = employee?.FullName ?? "Unknown"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding comment to job card {JobCardId}", id);
            throw;
        }
    }

    // Additional implementation methods...
    public async Task<List<JobCardCommentDto>> GetCommentsAsync(Guid id)
    {
        return await _jobCardRepository.GetCommentsAsync(id);
    }

    public async Task<List<JobCardApprovalStepDto>> GetApprovalHistoryAsync(Guid id)
    {
        return await _jobCardRepository.GetApprovalHistoryAsync(id);
    }

    public async Task<PagedResult<JobCardListDto>> GetPendingApprovalsAsync(JobCardFilterDto filter)
    {
        var currentEmployeeId = _currentUserService.EmployeeId;
        if (!currentEmployeeId.HasValue)
        {
            throw new UnauthorizedAccessException("Current user does not have an associated employee record");
        }

        return await _jobCardRepository.GetPendingApprovalsAsync(filter, currentEmployeeId.Value);
    }

    public async Task<List<JobCardListDto>> GetApprovedJobCardsAsync()
    {
        return await _jobCardRepository.GetApprovedJobCardsAsync();
    }

    public async Task<List<JobCardListDto>> GetJobCardsByAssetAsync(Guid assetId)
    {
        return await _jobCardRepository.GetByAssetAsync(assetId);
    }

    public async Task<List<JobCardListDto>> GetJobCardsByRequesterAsync(Guid requesterId)
    {
        return await _jobCardRepository.GetByRequesterAsync(requesterId);
    }

    public async Task<List<JobCardListDto>> GetJobCardsByTechnicianAsync(Guid technicianId)
    {
        return await _jobCardRepository.GetByTechnicianAsync(technicianId);
    }

    public async Task<JobCardDashboardStatsDto> GetDashboardStatsAsync()
    {
        return await _jobCardRepository.GetDashboardStatsAsync();
    }

    public async Task<JobCardDto> CancelJobCardAsync(Guid id, string reason)
    {
        var jobCard = await _jobCardRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Job card with ID {id} not found");
        jobCard.JobCardStatus = "Cancelled";
        jobCard.UpdatedAt = DateTime.UtcNow;
        jobCard.UpdatedBy = _currentUserService.UserId ?? "System";

        await _jobCardRepository.UpdateAsync(jobCard);
        await _unitOfWork.SaveChangesAsync();

        await AddCommentAsync(id, new AddJobCardCommentDto
        {
            Comment = $"Job card cancelled: {reason}",
            CommentType = "Cancellation",
            IsInternal = false
        });

        return await GetJobCardByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve cancelled job card");
    }

    public async Task<JobCardDocumentDto> UploadDocumentAsync(Guid jobCardId, Stream fileStream, string fileName, string documentType)
    {
        var currentUserIdString = _currentUserService.UserId;
        if (string.IsNullOrEmpty(currentUserIdString))
        {
            throw new UnauthorizedAccessException("User not authenticated");
        }

        var currentEmployeeId = _currentUserService.EmployeeId;
        if (!currentEmployeeId.HasValue)
        {
            throw new UnauthorizedAccessException("Current user does not have an associated employee record");
        }

        var tenantId = _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Tenant not found");
        var folderPath = $"maintenance/jobcards/{tenantId}/{jobCardId}";
        var filePath = await _fileStorageService.UploadFileAsync(fileStream, fileName, folderPath);

        var document = new JobCardDocument
        {
            Id = Guid.NewGuid(),
            JobCardId = jobCardId,
            FileName = fileName,
            FilePath = filePath,
            ContentType = ResolveContentType(fileName),
            FileSize = fileStream.CanSeek ? fileStream.Length : 0,
            DocumentType = documentType,
            TenantId = tenantId,
            UploadedById = currentEmployeeId.Value,
            UploadedDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = currentUserIdString
        };

        await _jobCardRepository.AddDocumentAsync(document);
        await _unitOfWork.SaveChangesAsync();

        var uploader = await _employeeRepository.GetByIdAsync(currentEmployeeId.Value);

        return new JobCardDocumentDto
        {
            Id = document.Id,
            FileName = document.FileName,
            FilePath = document.FilePath,
            ContentType = document.ContentType,
            FileSize = document.FileSize,
            DocumentType = document.DocumentType,
            UploadedAt = document.CreatedAt,
            UploadedBy = uploader?.FullName ?? "Unknown"
        };
    }

    public async Task DeleteDocumentAsync(Guid jobCardId, Guid documentId)
    {
        var document = await _jobCardRepository.GetDocumentAsync(jobCardId, documentId);
        if (document == null)
        {
            return;
        }

        await _jobCardRepository.DeleteDocumentAsync(documentId);
        await _unitOfWork.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(document.FilePath))
        {
            await _fileStorageService.DeleteFileAsync(document.FilePath);
        }
    }

    public async Task<List<JobCardDocumentDto>> GetDocumentsAsync(Guid id)
    {
        return await _jobCardRepository.GetDocumentsAsync(id);
    }

    public async Task<JobCardDocumentDownloadDto> DownloadDocumentAsync(Guid jobCardId, Guid documentId)
    {
        var document = await _jobCardRepository.GetDocumentAsync(jobCardId, documentId)
            ?? throw new FileNotFoundException("Document not found");

        if (string.IsNullOrWhiteSpace(document.FilePath) || !await _fileStorageService.FileExistsAsync(document.FilePath))
        {
            throw new FileNotFoundException($"Stored file not found for document {documentId}");
        }

        var content = await _fileStorageService.DownloadFileAsync(document.FilePath, document.Id);
        return new JobCardDocumentDownloadDto
        {
            FileName = document.FileName,
            ContentType = document.ContentType ?? ResolveContentType(document.FileName),
            Content = content
        };
    }

    public async Task<JobCardDto> CompleteJobCardAsync(Guid id, CompleteJobCardDto completeDto)
    {
        try
        {
            var jobCard = await _jobCardRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Job card with ID {id} not found");
            if (jobCard.JobCardStatus == "Completed" || jobCard.JobCardStatus == "Closed")
            {
                throw new InvalidOperationException($"Job card is already {jobCard.JobCardStatus}");
            }

            var currentUserIdString = _currentUserService.UserId;
            if (string.IsNullOrEmpty(currentUserIdString))
            {
                throw new UnauthorizedAccessException("User not authenticated");
            }

            var currentEmployeeId = _currentUserService.EmployeeId;
            if (!currentEmployeeId.HasValue)
            {
                throw new UnauthorizedAccessException("Current user does not have an associated employee record");
            }

            jobCard.CompletedDate = DateTime.UtcNow;
            jobCard.CompletionNotes = completeDto.CompletionNotes;
            jobCard.AssetConditionOnCompletion = completeDto.AssetConditionOnCompletion;
            jobCard.MileageReadingOnCompletion = completeDto.MileageReadingOnCompletion;
            jobCard.HoursReadingOnCompletion = completeDto.HoursReadingOnCompletion;
            jobCard.FuelLevelOnCompletion = completeDto.FuelLevelOnCompletion;
            jobCard.WorkCompletedSummary = completeDto.WorkCompletedSummary;
            jobCard.RemainingIssues = completeDto.RemainingIssues;
            jobCard.WarrantyDays = completeDto.WarrantyDays;
            jobCard.WarrantyTerms = completeDto.WarrantyTerms;
            jobCard.WarrantyExpiration = completeDto.WarrantyDays > 0 ? DateTime.UtcNow.AddDays(completeDto.WarrantyDays) : null;
            jobCard.RequiresFollowUp = completeDto.RequiresFollowUp;
            jobCard.FollowUpDate = completeDto.FollowUpDate;
            jobCard.FollowUpInstructions = completeDto.FollowUpInstructions;
            jobCard.JobCardStatus = "Completed";
            jobCard.UpdatedAt = DateTime.UtcNow;
            jobCard.UpdatedBy = currentUserIdString;

            await _jobCardRepository.UpdateAsync(jobCard);
            await _unitOfWork.SaveChangesAsync();

            await AddCommentAsync(id, new AddJobCardCommentDto
            {
                Comment = $"Job card completed by {currentEmployeeId}. {completeDto.CompletionNotes}",
                CommentType = "Completion",
                IsInternal = false
            });

            _logger.LogInformation("Job card {JobCardId} marked as completed by employee {EmployeeId}", id, currentEmployeeId);

            return await GetJobCardByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve completed job card");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing job card {JobCardId}", id);
            throw;
        }
    }

    public async Task<JobCardDto> PerformQualityCheckAsync(Guid id, JobCardQualityCheckDto qualityCheckDto)
    {
        try
        {
            var jobCard = await _jobCardRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Job card with ID {id} not found");
            if (jobCard.JobCardStatus != "Completed")
            {
                throw new InvalidOperationException("Job card must be completed before quality check");
            }

            var currentUserIdString = _currentUserService.UserId;
            if (string.IsNullOrEmpty(currentUserIdString))
            {
                throw new UnauthorizedAccessException("User not authenticated");
            }

            var currentEmployeeId = _currentUserService.EmployeeId;
            if (!currentEmployeeId.HasValue)
            {
                throw new UnauthorizedAccessException("Current user does not have an associated employee record");
            }

            jobCard.QualityCheckDate = DateTime.UtcNow;
            jobCard.QualityCheckedById = currentEmployeeId.Value;
            jobCard.QualityCheckPassed = qualityCheckDto.QualityCheckPassed;
            jobCard.QualityCheckNotes = qualityCheckDto.QualityCheckNotes;
            jobCard.JobCardStatus = qualityCheckDto.QualityCheckPassed ? "Quality Checked" : "Failed QC";
            jobCard.UpdatedAt = DateTime.UtcNow;
            jobCard.UpdatedBy = currentUserIdString;

            await _jobCardRepository.UpdateAsync(jobCard);
            await _unitOfWork.SaveChangesAsync();

            var checkResult = qualityCheckDto.QualityCheckPassed ? "passed" : "failed";
            await AddCommentAsync(id, new AddJobCardCommentDto
            {
                Comment = $"Quality check {checkResult} by employee {currentEmployeeId}. {qualityCheckDto.QualityCheckNotes}",
                CommentType = "QualityCheck",
                IsInternal = false
            });

            _logger.LogInformation("Quality check performed on job card {JobCardId} by employee {EmployeeId}. Result: {Result}",
                id, currentEmployeeId, checkResult);

            return await GetJobCardByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve job card after quality check");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing quality check on job card {JobCardId}", id);
            throw;
        }
    }

    public async Task<JobCardDto> RecordAcceptanceAsync(Guid id, JobCardAcceptanceDto acceptanceDto)
    {
        try
        {
            var jobCard = await _jobCardRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Job card with ID {id} not found");
            if (jobCard.JobCardStatus != "Quality Checked")
            {
                throw new InvalidOperationException("Job card must pass quality check before acceptance");
            }

            if (!acceptanceDto.Accepted)
            {
                throw new InvalidOperationException("Cannot record non-acceptance. Use rejection workflow instead.");
            }

            var currentUserIdString = _currentUserService.UserId;
            if (string.IsNullOrEmpty(currentUserIdString))
            {
                throw new UnauthorizedAccessException("User not authenticated");
            }

            var currentEmployeeId = _currentUserService.EmployeeId;
            if (!currentEmployeeId.HasValue)
            {
                throw new UnauthorizedAccessException("Current user does not have an associated employee record");
            }

            jobCard.AcceptedDate = DateTime.UtcNow;
            jobCard.AcceptedById = currentEmployeeId.Value;
            jobCard.CustomerAcceptance = true;
            jobCard.AcceptanceNotes = acceptanceDto.AcceptanceNotes;
            jobCard.JobCardStatus = "Accepted";
            jobCard.UpdatedAt = DateTime.UtcNow;
            jobCard.UpdatedBy = currentUserIdString;

            await _jobCardRepository.UpdateAsync(jobCard);
            await _unitOfWork.SaveChangesAsync();

            await AddCommentAsync(id, new AddJobCardCommentDto
            {
                Comment = $"Job card accepted by employee {currentEmployeeId}. {acceptanceDto.AcceptanceNotes}",
                CommentType = "Acceptance",
                IsInternal = false
            });

            _logger.LogInformation("Job card {JobCardId} accepted by employee {EmployeeId}", id, currentEmployeeId);

            return await GetJobCardByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve accepted job card");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting job card {JobCardId}", id);
            throw;
        }
    }

    public async Task<JobCardCertificateDto> GenerateCertificateAsync(Guid jobCardId, GenerateJobCardCertificateDto certificateDto)
    {
        try
        {
            var jobCard = await _jobCardRepository.GetByIdAsync(jobCardId) ?? throw new ArgumentException($"Job card with ID {jobCardId} not found");
            if (jobCard.JobCardStatus != "Accepted")
            {
                throw new InvalidOperationException("Job card must be accepted before generating certificate");
            }

            var currentUserIdString = _currentUserService.UserId;
            if (string.IsNullOrEmpty(currentUserIdString))
            {
                throw new UnauthorizedAccessException("User not authenticated");
            }

            var currentEmployeeId = _currentUserService.EmployeeId;
            if (!currentEmployeeId.HasValue)
            {
                throw new UnauthorizedAccessException("Current user does not have an associated employee record");
            }

            var tenantId = _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Tenant not found");

            var certificateNumber = await GenerateCertificateNumberAsync();

            var certificate = new JobCardCertificate
            {
                Id = Guid.NewGuid(),
                JobCardId = jobCardId,
                AssetId = jobCard.AssetId,
                CertificateNumber = certificateNumber,
                CertificateType = certificateDto.CertificateType,
                IssuedDate = DateTime.UtcNow,
                ValidUntil = certificateDto.ValidUntil,
                IssuedById = currentEmployeeId.Value,
                Description = certificateDto.Description,
                TenantId = tenantId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = currentUserIdString
            };

            await _jobCardRepository.AddCertificateAsync(certificate);

            jobCard.JobCardStatus = "Closed";
            jobCard.UpdatedAt = DateTime.UtcNow;
            jobCard.UpdatedBy = currentUserIdString;
            await _jobCardRepository.UpdateAsync(jobCard);

            await _unitOfWork.SaveChangesAsync();

            await AddCommentAsync(jobCardId, new AddJobCardCommentDto
            {
                Comment = $"Certificate {certificateNumber} generated for job card. Type: {certificateDto.CertificateType}",
                CommentType = "Certificate",
                IsInternal = false
            });

            _logger.LogInformation("Certificate {CertificateNumber} generated for job card {JobCardId} by employee {EmployeeId}",
                certificateNumber, jobCardId, currentEmployeeId);

            var employee = await _employeeRepository.GetByIdAsync(currentEmployeeId.Value);
            var asset = await _assetRepository.GetByIdAsync(jobCard.AssetId);

            return new JobCardCertificateDto
            {
                Id = certificate.Id,
                CertificateNumber = certificateNumber,
                JobCardId = jobCardId,
                JobCardNumber = jobCard.JobCardNumber,
                AssetId = jobCard.AssetId,
                AssetName = asset?.Name ?? "Unknown",
                CertificateType = certificate.CertificateType,
                IssuedDate = certificate.IssuedDate,
                ValidUntil = certificate.ValidUntil,
                IssuedBy = employee?.FullName ?? "Unknown",
                Description = certificate.Description,
                IsActive = certificate.IsActive,
                CreatedAt = certificate.CreatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating certificate for job card {JobCardId}", jobCardId);
            throw;
        }
    }

    private async Task<string> GenerateJobCardNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var sequence = await _jobCardRepository.GetNextSequenceNumberAsync(year);
        return $"JC-{year}-{sequence:D4}";
    }

    public async Task<List<JobCardCertificateDto>> GetCertificatesAsync(Guid jobCardId)
    {
        try
        {
            return await _jobCardRepository.GetCertificatesAsync(jobCardId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting certificates for job card {JobCardId}", jobCardId);
            throw;
        }
    }

    public async Task<JobCardCertificateDto?> GetCertificateByIdAsync(Guid certificateId)
    {
        try
        {
            var certificates = await _jobCardRepository.GetCertificatesAsync(Guid.Empty);
            return certificates.FirstOrDefault(c => c.Id == certificateId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting certificate {CertificateId}", certificateId);
            throw;
        }
    }

    private async Task<string> GenerateCertificateNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var sequence = await _jobCardRepository.GetNextCertificateSequenceNumberAsync(year);
        return $"CERT-{year}-{sequence:D4}";
    }

    private async Task<Guid> GetDefaultWorkOrderTypeIdAsync(Guid maintenanceTypeId)
    {
        try
        {
            _logger.LogDebug("Getting work order types from repository");
            var workOrderTypes = await _jobCardRepository.GetWorkOrderTypesAsync();

            _logger.LogDebug("Found {Count} work order types", workOrderTypes.Count);

            if (!workOrderTypes.Any())
            {
                throw new InvalidOperationException("No work order types found. Please configure at least one work order type in the database.");
            }

            // Try to find a suitable type based on naming conventions
            var defaultType = workOrderTypes.FirstOrDefault(wot =>
                wot.Name.Contains("maintenance", StringComparison.CurrentCultureIgnoreCase) ||
                wot.Name.Contains("standard", StringComparison.CurrentCultureIgnoreCase) ||
                wot.Code.ToLower() == "main" ||
                wot.Code.ToLower() == "std");

            if (defaultType != null)
            {
                _logger.LogDebug("Using work order type '{Name}' (ID: {Id}) as default", defaultType.Name, defaultType.Id);
                return defaultType.Id;
            }

            // If no suitable type found, return the first active one
            var firstActiveType = workOrderTypes.FirstOrDefault(wot => wot.IsActive);
            if (firstActiveType != null)
            {
                _logger.LogDebug("Using first active work order type '{Name}' (ID: {Id}) as fallback", firstActiveType.Name, firstActiveType.Id);
                return firstActiveType.Id;
            }

            // If no active types, use the first one available
            var firstType = workOrderTypes.First();
            _logger.LogWarning("No active work order types found, using first available type '{Name}' (ID: {Id})", firstType.Name, firstType.Id);
            return firstType.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting default work order type for maintenance type {MaintenanceTypeId}", maintenanceTypeId);
            throw;
        }
    }

    private static string ResolveContentType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };
    }
}
