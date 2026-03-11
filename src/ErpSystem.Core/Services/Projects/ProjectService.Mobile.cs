using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities.Projects;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    public async Task<ProjectTimesheetEntryDto> SubmitMobileTimesheetAsync(Guid projectId, Guid workItemId, CreateProjectTimesheetEntryDto dto, Guid userId)
    {
        await RequireMobileAssignmentAsync(projectId, workItemId, userId);

        return await AddTimesheetEntryAsync(projectId, new CreateProjectTimesheetEntryDto
        {
            WorkItemId = workItemId,
            UserId = userId,
            EntryDate = dto.EntryDate,
            Hours = dto.Hours,
            IsBillable = dto.IsBillable,
            HourlyRate = dto.HourlyRate,
            WorkType = string.IsNullOrWhiteSpace(dto.WorkType) ? "Field" : dto.WorkType,
            Notes = dto.Notes,
            Status = "Submitted"
        });
    }

    public async Task<ProjectExpenseDto> SubmitMobileExpenseAsync(Guid projectId, Guid workItemId, CreateProjectExpenseDto dto, Guid userId)
    {
        await RequireMobileAssignmentAsync(projectId, workItemId, userId);

        return await AddExpenseAsync(projectId, new CreateProjectExpenseDto
        {
            WorkItemId = workItemId,
            UserId = userId,
            ExpenseDate = dto.ExpenseDate,
            Category = string.IsNullOrWhiteSpace(dto.Category) ? "Travel" : dto.Category,
            Currency = string.IsNullOrWhiteSpace(dto.Currency) ? "USD" : dto.Currency,
            Amount = dto.Amount,
            TaxAmount = dto.TaxAmount,
            IsBillable = dto.IsBillable,
            ReceiptDocumentId = dto.ReceiptDocumentId,
            Notes = dto.Notes,
            Status = "Submitted"
        });
    }

    public async Task<ProjectWorkItemDto> UpdateMobileWorkItemProgressAsync(Guid projectId, Guid workItemId, UpdateProjectWorkItemProgressDto dto, Guid userId)
    {
        var workItem = await RequireMobileAssignmentAsync(projectId, workItemId, userId);

        var updated = await UpdateWorkItemAsync(workItemId, new CreateProjectWorkItemDto
        {
            ParentId = workItem.ParentId,
            NodeType = workItem.NodeType,
            Title = workItem.Title,
            Description = workItem.Description,
            Status = dto.Status,
            Priority = workItem.Priority,
            AssignedToUserId = workItem.AssignedToUserId,
            PlannedStartDate = workItem.PlannedStartDate,
            PlannedEndDate = workItem.PlannedEndDate,
            ActualStartDate = dto.ActualStartDate ?? workItem.ActualStartDate,
            ActualEndDate = dto.ActualEndDate ?? workItem.ActualEndDate,
            PercentComplete = dto.PercentComplete,
            IsRollupEnabled = workItem.IsRollupEnabled,
            EffortEstimateHours = workItem.EffortEstimateHours,
            ActualEffortHours = workItem.ActualEffortHours
        });

        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            await AddCommentAsync(projectId, new CreateProjectCommentDto
            {
                WorkItemId = workItemId,
                CommentType = "MobileUpdate",
                Body = dto.Notes.Trim()
            });
        }

        return updated;
    }

    private async Task<ProjectWorkItem> RequireMobileAssignmentAsync(Guid projectId, Guid workItemId, Guid userId)
    {
        EnsureInternalProjectAccess();

        if (userId == Guid.Empty || !_currentUserProvider.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Authentication is required to perform mobile project actions.");
        }

        if (_currentUserProvider.UserId != Guid.Empty && _currentUserProvider.UserId != userId)
        {
            throw new UnauthorizedAccessException("Mobile project actions can only be performed for the authenticated user.");
        }

        var workItem = await _unitOfWork.Repository<ProjectWorkItem>().FirstOrDefaultAsync(x =>
                x.Id == workItemId
                && x.ProjectId == projectId
                && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project work item with ID {workItemId} was not found.");

        if (workItem.AssignedToUserId != userId)
        {
            throw new UnauthorizedAccessException("Mobile project actions are limited to work items assigned to the current user.");
        }

        return workItem;
    }
}
