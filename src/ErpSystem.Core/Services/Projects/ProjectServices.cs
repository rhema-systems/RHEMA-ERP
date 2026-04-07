using System.Text.Json;
using System.Text.RegularExpressions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Assets;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Core.Services;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService : IProjectService
{
    private const string ProjectDeliverableWorkflowEntityType = "ProjectDeliverable";
    private const string ProjectClosureWorkflowEntityType = "ProjectClosure";
    private const string ProjectBudgetRevisionWorkflowEntityType = "ProjectBudgetRevision";
    private const string DeliverableStatusDraft = "Draft";
    private const string DeliverableStatusPendingExternalSignOff = "PendingExternalSignOff";
    private const string DeliverableStatusPendingApproval = "PendingApproval";
    private const string DeliverableStatusApproved = "Approved";
    private const string DeliverableStatusRejected = "Rejected";

    private readonly IProjectRepository _projectRepository;
    private readonly IProjectManagementSettingsRepository _settingsRepository;
    private readonly IProjectTemplateRepository _templateRepository;
    private readonly IProjectTypeRepository _projectTypeRepository;
    private readonly IProjectPortfolioRepository _projectPortfolioRepository;
    private readonly IProjectProgramRepository _projectProgramRepository;
    private readonly IContractService _contractService;
    private readonly IBusinessPartnerService _businessPartnerService;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly IWorkflowStatusAdapterRegistry _workflowStatusAdapterRegistry;
    private readonly IWorkflowService _workflowService;
    private readonly IUserService _userService;
    private readonly IJobCardService _jobCardService;
    private readonly IWorkOrderService _workOrderService;
    private readonly ISalesAgreementService _salesAgreementService;
    private readonly ISalesOrderService _salesOrderService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantSettingsService _tenantSettingsService;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IAppEventBus _appEventBus;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(
        IProjectRepository projectRepository,
        IProjectManagementSettingsRepository settingsRepository,
        IProjectTemplateRepository templateRepository,
        IProjectTypeRepository projectTypeRepository,
        IProjectPortfolioRepository projectPortfolioRepository,
        IProjectProgramRepository projectProgramRepository,
        IContractService contractService,
        IBusinessPartnerService businessPartnerService,
        IWorkflowIntegrationService workflowIntegrationService,
        IWorkflowStatusAdapterRegistry workflowStatusAdapterRegistry,
        IWorkflowService workflowService,
        IUserService userService,
        IJobCardService jobCardService,
        IWorkOrderService workOrderService,
        ISalesAgreementService salesAgreementService,
        ISalesOrderService salesOrderService,
        IUnitOfWork unitOfWork,
        ITenantSettingsService tenantSettingsService,
        ICurrentUserProvider currentUserProvider,
        IAppEventBus appEventBus,
        ILogger<ProjectService> logger)
    {
        _projectRepository = projectRepository;
        _settingsRepository = settingsRepository;
        _templateRepository = templateRepository;
        _projectTypeRepository = projectTypeRepository;
        _projectPortfolioRepository = projectPortfolioRepository;
        _projectProgramRepository = projectProgramRepository;
        _contractService = contractService;
        _businessPartnerService = businessPartnerService;
        _workflowIntegrationService = workflowIntegrationService;
        _workflowStatusAdapterRegistry = workflowStatusAdapterRegistry;
        _workflowService = workflowService;
        _userService = userService;
        _jobCardService = jobCardService;
        _workOrderService = workOrderService;
        _salesAgreementService = salesAgreementService;
        _salesOrderService = salesOrderService;
        _unitOfWork = unitOfWork;
        _tenantSettingsService = tenantSettingsService;
        _currentUserProvider = currentUserProvider;
        _appEventBus = appEventBus;
        _logger = logger;
    }

    public async Task<PagedResult<ProjectDto>> GetProjectsAsync(int page, int pageSize, string? search = null, string? status = null, Guid? projectTypeId = null, Guid? portfolioId = null, Guid? programId = null)
    {
        EnsureInternalProjectAccess();

        static Dictionary<Guid, int> BuildCountLookup<T>(IEnumerable<T> items, Func<T, Guid> keySelector)
            => items
                .GroupBy(keySelector)
                .ToDictionary(group => group.Key, group => group.Count());

        if (!CanReadAllProjects())
        {
            var accessibleProjects = await GetAccessibleProjectsAsync(search, status, projectTypeId, portfolioId, programId, take: 5000);
            var accessibleProjectIds = accessibleProjects.Select(x => x.Id).ToList();
            var accessibleRiskRepository = _unitOfWork.Repository<ProjectRisk>();
            var accessibleIssueRepository = _unitOfWork.Repository<ProjectIssue>();
            var accessibleMilestoneRepository = _unitOfWork.Repository<ProjectMilestone>();
            var accessibleRisks = await accessibleRiskRepository.FindAsync(x => accessibleProjectIds.Contains(x.ProjectId));
            var accessibleIssues = await accessibleIssueRepository.FindAsync(x => accessibleProjectIds.Contains(x.ProjectId));
            var accessibleMilestones = await accessibleMilestoneRepository.FindAsync(x => accessibleProjectIds.Contains(x.ProjectId));
            var accessibleOpenRiskCounts = BuildCountLookup(
                accessibleRisks.Where(x => string.Equals(x.Status, "Open", StringComparison.OrdinalIgnoreCase)),
                x => x.ProjectId);
            var accessibleOpenIssueCounts = BuildCountLookup(
                accessibleIssues.Where(x => string.Equals(x.Status, "Open", StringComparison.OrdinalIgnoreCase)),
                x => x.ProjectId);
            var accessibleOverdueMilestoneCounts = BuildCountLookup(
                accessibleMilestones.Where(x => x.TargetDate < DateTime.UtcNow && !string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase)),
                x => x.ProjectId);

            var pagedItems = accessibleProjects
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var accessibleItems = new List<ProjectDto>();
            foreach (var project in pagedItems)
            {
                var dto = MapToDto(project);
                dto.OpenRiskCount = accessibleOpenRiskCounts.TryGetValue(project.Id, out var openRiskCount) ? openRiskCount : 0;
                dto.OpenIssueCount = accessibleOpenIssueCounts.TryGetValue(project.Id, out var openIssueCount) ? openIssueCount : 0;
                dto.OverdueMilestoneCount = accessibleOverdueMilestoneCounts.TryGetValue(project.Id, out var overdueMilestoneCount) ? overdueMilestoneCount : 0;
                accessibleItems.Add(dto);
            }

            return new PagedResult<ProjectDto>
            {
                Items = accessibleItems,
                TotalCount = accessibleProjects.Count,
                Page = page,
                PageSize = pageSize
            };
        }

        var result = await _projectRepository.GetPagedAsync(page, pageSize, search, status, projectTypeId, portfolioId, programId);
        var projectIds = result.Items.Select(x => x.Id).ToList();
        var riskRepo = _unitOfWork.Repository<ProjectRisk>();
        var issueRepo = _unitOfWork.Repository<ProjectIssue>();
        var milestoneRepo = _unitOfWork.Repository<ProjectMilestone>();
        var risks = await riskRepo.FindAsync(x => projectIds.Contains(x.ProjectId));
        var issues = await issueRepo.FindAsync(x => projectIds.Contains(x.ProjectId));
        var milestones = await milestoneRepo.FindAsync(x => projectIds.Contains(x.ProjectId));
        var openRiskCounts = BuildCountLookup(
            risks.Where(x => string.Equals(x.Status, "Open", StringComparison.OrdinalIgnoreCase)),
            x => x.ProjectId);
        var openIssueCounts = BuildCountLookup(
            issues.Where(x => string.Equals(x.Status, "Open", StringComparison.OrdinalIgnoreCase)),
            x => x.ProjectId);
        var overdueMilestoneCounts = BuildCountLookup(
            milestones.Where(x => x.TargetDate < DateTime.UtcNow && !string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase)),
            x => x.ProjectId);

        var items = new List<ProjectDto>();
        foreach (var project in result.Items)
        {
            var dto = MapToDto(project);
            dto.OpenRiskCount = openRiskCounts.TryGetValue(project.Id, out var openRiskCount) ? openRiskCount : 0;
            dto.OpenIssueCount = openIssueCounts.TryGetValue(project.Id, out var openIssueCount) ? openIssueCount : 0;
            dto.OverdueMilestoneCount = overdueMilestoneCounts.TryGetValue(project.Id, out var overdueMilestoneCount) ? overdueMilestoneCount : 0;

            if (string.Equals(project.Status, ProjectStatuses.PendingApproval, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var step = await _workflowService.GetCurrentWorkflowStepAsync("Project", project.Id);
                    dto.CurrentWorkflowStepName = step?.StepName;
                }
                catch
                {
                }
            }

            items.Add(dto);
        }

        return new PagedResult<ProjectDto>
        {
            Items = items,
            TotalCount = result.TotalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ProjectDetailDto?> GetProjectByIdAsync(Guid id)
    {
        await GetProjectForOperationAsync(id, ProjectAccessOperation.View);
        await SyncProjectMaterialCostAsync(id);
        var project = await _projectRepository.GetDetailByIdAsync(id);
        if (project == null)
        {
            return null;
        }

        var dto = MapToDetailDto(project);
        dto.ResourceAllocations = await EnrichResourceAllocationsAsync(dto.ResourceAllocations, project.ResourceAllocations.ToList());
        dto.Packages = (await GetProjectPackagesAsync(id)).ToList();
        dto.BoqItems = (await GetProjectBoqItemsAsync(id)).ToList();
        dto.ApprovalRegister = (await GetApprovalRegisterAsync(id)).ToList();
        dto.Units = (await GetProjectUnitsAsync(id)).ToList();
        dto.CustomerVariations = (await GetCustomerVariationsAsync(id)).ToList();
        dto.CommissioningItems = (await GetProjectCommissioningItemsAsync(id)).ToList();
        dto.HandoverItems = (await GetProjectHandoverItemsAsync(id)).ToList();
        dto.SnagItems = (await GetProjectSnagItemsAsync(id)).ToList();
        dto.DefectLiabilityCases = (await GetProjectDefectLiabilityCasesAsync(id)).ToList();
        dto.WorkItems = (await GetWorkItemsAsync(id)).ToList();
        dto.Deliverables = (await GetDeliverablesAsync(id)).ToList();
        dto.TaskDependencies = (await GetTaskDependenciesAsync(id)).ToList();
        dto.Baselines = (await GetBaselinesAsync(id)).ToList();
        dto.TimesheetEntries = (await GetTimesheetEntriesAsync(id)).ToList();
        dto.Expenses = (await GetExpensesAsync(id)).ToList();
        dto.MaterialCostEntries = (await GetMaterialCostEntriesAsync(id)).ToList();
        dto.QualityCheckpoints = (await GetQualityCheckpointsAsync(id)).ToList();
        dto.NonConformances = (await GetNonConformancesAsync(id)).ToList();
        dto.RevenueRecognitions = (await GetRevenueRecognitionsAsync(id)).ToList();
        dto.AssetLinks = (await GetAssetLinksAsync(id)).ToList();
        dto.ExternalAccessPolicies = (await GetExternalAccessPoliciesAsync(id)).ToList();
        dto.Decisions = (await GetDecisionsAsync(id)).ToList();
        dto.Meetings = (await GetMeetingsAsync(id)).ToList();
        dto.ActionItems = (await GetActionItemsAsync(id)).ToList();
        dto.LessonsLearned = (await GetLessonsLearnedAsync(id)).ToList();
        dto.Closure = await GetClosureAsync(id);
        await ApplyBaselineMetadataAsync(dto);
        await EnrichUserDisplayNamesAsync(dto);
        return dto;
    }

    public async Task<IEnumerable<ProjectLookupDto>> LookupProjectsAsync(string? search = null, string? status = null, Guid? projectTypeId = null, Guid? portfolioId = null, Guid? programId = null, int take = 20)
        => (await GetAccessibleProjectsAsync(search, status, projectTypeId, portfolioId, programId, take)).Select(MapToLookupDto);

    public async Task<IEnumerable<ProjectResourceLookupDto>> LookupResourcesAsync(string? search = null, int take = 50)
    {
        EnsureInternalAuthenticatedProjectAccess();
        var normalizedSearch = search?.Trim();
        var employees = await _unitOfWork.Repository<Employee>().FindAsync(x =>
            x.TenantId == _currentUserProvider.TenantId
            && !x.IsDeleted
            && (string.IsNullOrWhiteSpace(normalizedSearch)
                || x.FirstName.Contains(normalizedSearch)
                || x.LastName.Contains(normalizedSearch)
                || (x.MiddleName != null && x.MiddleName.Contains(normalizedSearch))
                || x.EmployeeNumber.Contains(normalizedSearch)
                || (x.CorporateEmployeeID != null && x.CorporateEmployeeID.Contains(normalizedSearch))));

        return employees
            .OrderBy(x => x.FirstName)
            .ThenBy(x => x.LastName)
            .Take(Math.Max(1, take))
            .Select(x => new ProjectResourceLookupDto
            {
                Id = x.Id,
                DisplayName = string.IsNullOrWhiteSpace(x.DisplayName) ? x.FullName : x.DisplayName,
                EmployeeNumber = x.EmployeeNumber
            })
            .ToList();
    }

    public async Task<ProjectDetailDto> CreateProjectAsync(CreateProjectDto dto)
    {
        EnsureInternalProjectAccess();
        if (IsReadOnlyUser())
        {
            throw new UnauthorizedAccessException("Read-only users cannot create projects.");
        }

        var settings = await _settingsRepository.GetOrCreateDefaultAsync(_currentUserProvider.TenantId, _currentUserProvider.UserId);
        var projectType = dto.ProjectTypeId.HasValue ? await _projectTypeRepository.GetByIdAsync(dto.ProjectTypeId.Value) : null;
        await ValidatePortfolioProgramAsync(dto.PortfolioId, dto.ProgramId);

        if ((projectType?.RequiresSponsor ?? settings.RequireSponsor) && !dto.SponsorId.HasValue)
        {
            throw new InvalidOperationException("Sponsor is required for this project type");
        }

        var project = new Project
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectCode = string.IsNullOrWhiteSpace(dto.ProjectCode) ? await GenerateProjectCodeAsync(settings.ProjectNumberFormat) : dto.ProjectCode.Trim(),
            Title = dto.Title.Trim(),
            Summary = dto.Summary,
            BusinessCase = dto.BusinessCase,
            Objectives = dto.Objectives,
            StrategicAlignment = dto.StrategicAlignment,
            ProjectTypeId = dto.ProjectTypeId ?? settings.DefaultProjectTypeId,
            ProjectPriorityId = dto.ProjectPriorityId ?? settings.DefaultProjectPriorityId,
            TemplateId = dto.TemplateId ?? settings.DefaultTemplateId,
            PortfolioId = dto.PortfolioId,
            ProgramId = dto.ProgramId,
            Methodology = dto.Methodology,
            SponsorId = dto.SponsorId,
            ProjectManagerId = dto.ProjectManagerId,
            DepartmentId = dto.DepartmentId,
            LocationId = dto.LocationId,
            CustomerId = dto.CustomerId,
            BusinessPartnerId = dto.BusinessPartnerId,
            ContractId = dto.ContractId,
            TenderId = dto.TenderId,
            StartDate = dto.StartDate,
            TargetEndDate = dto.TargetEndDate,
            EstimatedBudget = dto.EstimatedBudget,
            ScopeStatement = dto.ScopeStatement,
            Assumptions = dto.Assumptions,
            Constraints = dto.Constraints,
            ExpectedBenefits = dto.ExpectedBenefits,
            FundingSource = dto.FundingSource,
            ApprovalRequired = dto.ApprovalRequired ?? settings.DefaultApprovalRequired,
            ExternalPortalAccessEnabled = dto.ExternalPortalAccessEnabled ?? false,
            ExternalCollaborationEnabled = dto.ExternalCollaborationEnabled ?? false,
            Status = ProjectStatuses.Draft,
            BudgetStatus = dto.EstimatedBudget.HasValue ? "Estimated" : "NotStarted",
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _projectRepository.CreateAsync(project);
        await _unitOfWork.SaveChangesAsync();
        await EnsureProjectMembershipsAsync(project);
        await _unitOfWork.SaveChangesAsync();

        if (project.TemplateId.HasValue)
        {
            await ApplyTemplateAsync(project, project.TemplateId.Value, dto.DevelopmentProfile);
        }
        else if (dto.DevelopmentProfile != null)
        {
            await UpsertProjectConstructionFoundationAsync(project, dto.DevelopmentProfile);
        }

        await SaveInitiationSnapshotAsync(project, "Created", "Initial project creation");
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "Created");

        return (await GetProjectByIdAsync(project.Id))!;
    }

    public async Task<ProjectDetailDto> UpdateProjectAsync(Guid id, UpdateProjectDto dto)
    {
        var project = await GetProjectForOperationAsync(id, ProjectAccessOperation.UpdateOverview);

        if (string.Equals(project.Status, ProjectStatuses.Closed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(project.Status, ProjectStatuses.Archived, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Closed or archived projects are read-only");
        }

        var touchesFinancialFields =
            dto.ApprovedBudget != project.ApprovedBudget ||
            dto.ActualCost != project.ActualCost ||
            !string.Equals(dto.BudgetStatus, project.BudgetStatus, StringComparison.OrdinalIgnoreCase);
        if (touchesFinancialFields)
        {
            await EnsureProjectAccessAsync(project, ProjectAccessOperation.ManageFinancials);
        }

        await ValidatePortfolioProgramAsync(dto.PortfolioId, dto.ProgramId);
        project.Title = dto.Title.Trim();
        project.Summary = dto.Summary;
        project.BusinessCase = dto.BusinessCase;
        project.Objectives = dto.Objectives;
        project.StrategicAlignment = dto.StrategicAlignment;
        project.ProjectTypeId = dto.ProjectTypeId;
        project.ProjectPriorityId = dto.ProjectPriorityId;
        project.TemplateId = dto.TemplateId;
        project.PortfolioId = dto.PortfolioId;
        project.ProgramId = dto.ProgramId;
        project.Methodology = dto.Methodology;
        project.SponsorId = dto.SponsorId;
        project.ProjectManagerId = dto.ProjectManagerId;
        project.DepartmentId = dto.DepartmentId;
        project.LocationId = dto.LocationId;
        project.CustomerId = dto.CustomerId;
        project.BusinessPartnerId = dto.BusinessPartnerId;
        project.ContractId = dto.ContractId;
        project.TenderId = dto.TenderId;
        project.StartDate = dto.StartDate;
        project.TargetEndDate = dto.TargetEndDate;
        project.EstimatedBudget = dto.EstimatedBudget;
        project.ApprovedBudget = dto.ApprovedBudget ?? project.ApprovedBudget;
        project.ActualCost = dto.ActualCost ?? project.ActualCost;
        project.BudgetStatus = dto.BudgetStatus ?? project.BudgetStatus;
        project.ScopeStatement = dto.ScopeStatement;
        project.Assumptions = dto.Assumptions;
        project.Constraints = dto.Constraints;
        project.ExpectedBenefits = dto.ExpectedBenefits;
        project.FundingSource = dto.FundingSource;
        project.ApprovalRequired = dto.ApprovalRequired ?? project.ApprovalRequired;
        project.ExternalPortalAccessEnabled = dto.ExternalPortalAccessEnabled ?? project.ExternalPortalAccessEnabled;
        project.ExternalCollaborationEnabled = dto.ExternalCollaborationEnabled ?? project.ExternalCollaborationEnabled;
        project.StatusRemarks = dto.StatusRemarks;
        project.UpdatedBy = _currentUserProvider.Username;
        project.LastModifiedById = _currentUserProvider.UserId;

        await _projectRepository.UpdateAsync(project);
        await SaveInitiationSnapshotAsync(project, "Updated", "Project initiation details updated");
        await _unitOfWork.SaveChangesAsync();
        await EnsureProjectMembershipsAsync(project);
        await _unitOfWork.SaveChangesAsync();

        if (dto.DevelopmentProfile != null)
        {
            await UpsertProjectConstructionFoundationAsync(project, dto.DevelopmentProfile);
        }

        await PublishActivityAsync(project, "Updated");

        return (await GetProjectByIdAsync(project.Id))!;
    }

    public async Task SubmitProjectForApprovalAsync(Guid id, Guid userId)
    {
        var project = await GetProjectForOperationAsync(id, ProjectAccessOperation.SubmitForApproval);

        if (!string.Equals(project.Status, ProjectStatuses.Draft, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Project must be in Draft status to submit for approval (current status: '{project.Status}')");
        }

        var workflowResult = await _workflowIntegrationService.SubmitAsync("Project", id);
        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start workflow");
        }

        var adapter = _workflowStatusAdapterRegistry.GetAdapter("Project");
        adapter.ApplySubmitOutcome(project, workflowResult.Outcome, userId);
        project.SubmittedAt = DateTime.UtcNow;
        project.UpdatedBy = _currentUserProvider.Username;
        project.LastModifiedById = _currentUserProvider.UserId;

        await _projectRepository.UpdateAsync(project);
        await SaveInitiationSnapshotAsync(project, "Submitted", "Project submitted for approval");
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "Submitted");
    }

    public async Task ApproveProjectAsync(Guid id, Guid userId, string? comments = null)
    {
        var project = await GetProjectForOperationAsync(id, ProjectAccessOperation.ApproveWorkflow);

        if (!string.Equals(project.Status, ProjectStatuses.PendingApproval, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Project must be in PendingApproval status to approve (current status: '{project.Status}')");
        }

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync("Project", id, userId);
        if (!canApprove)
        {
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step");
        }

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync("Project", id, userId, "Approve", comments);
        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process approval");
        }

        var adapter = _workflowStatusAdapterRegistry.GetAdapter("Project");
        adapter.ApplyApprovalOutcome(project, workflowResult.Outcome, userId);
        project.ApprovedAt = DateTime.UtcNow;
        project.UpdatedBy = _currentUserProvider.Username;
        project.LastModifiedById = _currentUserProvider.UserId;

        await _projectRepository.UpdateAsync(project);
        await SaveInitiationSnapshotAsync(project, "Approved", comments);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "Approved");
    }

    public async Task RejectProjectAsync(Guid id, Guid userId, string reason, string? comments = null)
    {
        var project = await GetProjectForOperationAsync(id, ProjectAccessOperation.ApproveWorkflow);

        if (!string.Equals(project.Status, ProjectStatuses.PendingApproval, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Project must be in PendingApproval status to reject (current status: '{project.Status}')");
        }

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync("Project", id, userId);
        if (!canApprove)
        {
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current workflow step");
        }

        var rejectionText = !string.IsNullOrWhiteSpace(comments) ? comments : reason;
        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync("Project", id, userId, "Reject", rejectionText);
        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to process rejection");
        }

        var adapter = _workflowStatusAdapterRegistry.GetAdapter("Project");
        adapter.ApplyApprovalOutcome(project, workflowResult.Outcome, userId, rejectionText);
        project.StatusRemarks = rejectionText;
        project.UpdatedBy = _currentUserProvider.Username;
        project.LastModifiedById = _currentUserProvider.UserId;

        await _projectRepository.UpdateAsync(project);
        await SaveInitiationSnapshotAsync(project, "Rejected", rejectionText);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "Rejected");
    }

    public async Task<IEnumerable<ProjectMemberDto>> GetMembersAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return (await _unitOfWork.Repository<ProjectMember>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderBy(x => x.JoinedAt)
            .Select(MapToDto);
    }

    public async Task<ProjectMemberDto> AddMemberAsync(Guid projectId, AddProjectMemberDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageMembers);
        var repo = _unitOfWork.Repository<ProjectMember>();
        var existing = await repo.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == dto.UserId && x.Role == dto.Role && x.TenantId == _currentUserProvider.TenantId);
        if (existing != null)
        {
            if (!existing.IsActive)
            {
                existing.IsActive = true;
                existing.UpdatedBy = _currentUserProvider.Username;
                existing.LastModifiedById = _currentUserProvider.UserId;
                await repo.UpdateAsync(existing);
                await _unitOfWork.SaveChangesAsync();
            }

            return MapToDto(existing);
        }

        var entity = new ProjectMember
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            UserId = dto.UserId,
            Role = dto.Role,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task RemoveMemberAsync(Guid memberId)
    {
        var member = await _unitOfWork.Repository<ProjectMember>().FirstOrDefaultAsync(x => x.Id == memberId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project member with ID {memberId} not found");
        await RequireProjectAsync(member.ProjectId, ProjectAccessOperation.ManageMembers);
        await _unitOfWork.Repository<ProjectMember>().DeleteAsync(memberId);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectWorkItemDto>> GetWorkItemsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var items = (await _unitOfWork.Repository<ProjectWorkItem>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CreatedAt)
            .ToList();
        return BuildWorkItemTree(items, null);
    }

    public async Task<ProjectWorkItemDto> AddWorkItemAsync(Guid projectId, CreateProjectWorkItemDto dto)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.ManagePlan);
        await ValidateWorkItemScheduleAsync(project, null, dto);
        var repo = _unitOfWork.Repository<ProjectWorkItem>();
        var siblings = await repo.FindAsync(x => x.ProjectId == projectId && x.ParentId == dto.ParentId && x.TenantId == _currentUserProvider.TenantId);
        var entity = new ProjectWorkItem
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ParentId = dto.ParentId,
            NodeType = dto.NodeType,
            Title = dto.Title,
            Description = dto.Description,
            Status = dto.Status,
            Priority = dto.Priority,
            AssignedToUserId = dto.AssignedToUserId,
            PlannedStartDate = dto.PlannedStartDate,
            PlannedEndDate = dto.PlannedEndDate,
            ActualStartDate = dto.ActualStartDate,
            ActualEndDate = dto.ActualEndDate,
            PercentComplete = dto.PercentComplete,
            IsRollupEnabled = dto.IsRollupEnabled,
            EffortEstimateHours = dto.EffortEstimateHours,
            ActualEffortHours = dto.ActualEffortHours,
            SortOrder = siblings.Count(),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await UpdateProjectProgressAsync(project);
        await PublishActivityAsync(project, "WorkItemAdded", new Dictionary<string, object> { ["WorkItemId"] = entity.Id, ["Title"] = entity.Title });
        return MapToDto(entity);
    }

    public async Task<ProjectWorkItemDto> UpdateWorkItemAsync(Guid workItemId, CreateProjectWorkItemDto dto)
    {
        var repo = _unitOfWork.Repository<ProjectWorkItem>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == workItemId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project work item with ID {workItemId} not found");
        var project = await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManagePlan);
        var planningChanged = entity.PlannedStartDate?.Date != dto.PlannedStartDate?.Date || entity.PlannedEndDate?.Date != dto.PlannedEndDate?.Date;
        await ValidateWorkItemScheduleAsync(project, entity, dto);

        entity.ParentId = dto.ParentId;
        entity.NodeType = dto.NodeType;
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.Status = dto.Status;
        entity.Priority = dto.Priority;
        entity.AssignedToUserId = dto.AssignedToUserId;
        entity.PlannedStartDate = dto.PlannedStartDate;
        entity.PlannedEndDate = dto.PlannedEndDate;
        entity.ActualStartDate = dto.ActualStartDate;
        entity.ActualEndDate = dto.ActualEndDate;
        entity.PercentComplete = dto.PercentComplete;
        entity.IsRollupEnabled = dto.IsRollupEnabled;
        entity.EffortEstimateHours = dto.EffortEstimateHours;
        entity.ActualEffortHours = dto.ActualEffortHours;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await UpdateProjectProgressAsync(project);
        if (planningChanged && !string.IsNullOrWhiteSpace(dto.ScheduleChangeReason))
        {
            await _unitOfWork.Repository<ProjectComment>().AddAsync(new ProjectComment
            {
                TenantId = _currentUserProvider.TenantId,
                ProjectId = project.Id,
                WorkItemId = entity.Id,
                CommentType = "ScheduleReplan",
                Body = dto.ScheduleChangeReason.Trim(),
                CreatedBy = _currentUserProvider.Username,
                CreatedById = _currentUserProvider.UserId
            });
            await _unitOfWork.SaveChangesAsync();
            await PublishActivityAsync(project, "ScheduleReplanned", new Dictionary<string, object>
            {
                ["WorkItemId"] = entity.Id,
                ["Title"] = entity.Title,
                ["Reason"] = dto.ScheduleChangeReason.Trim()
            });
        }
        return MapToDto(entity);
    }

    public async Task ReorderWorkItemsAsync(Guid projectId, ReorderProjectWorkItemsDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManagePlan);
        var repo = _unitOfWork.Repository<ProjectWorkItem>();
        var items = (await repo.FindAsync(x => x.ProjectId == projectId && x.ParentId == dto.ParentId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        for (var index = 0; index < dto.OrderedIds.Count; index++)
        {
            var item = items.FirstOrDefault(x => x.Id == dto.OrderedIds[index]);
            if (item == null)
            {
                continue;
            }

            item.SortOrder = index;
            item.UpdatedBy = _currentUserProvider.Username;
            item.LastModifiedById = _currentUserProvider.UserId;
            await repo.UpdateAsync(item);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DeleteWorkItemAsync(Guid workItemId)
    {
        var repo = _unitOfWork.Repository<ProjectWorkItem>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == workItemId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project work item with ID {workItemId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManagePlan);
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await UpdateProjectProgressAsync(await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManagePlan));
    }

    public async Task<IEnumerable<ProjectMilestoneDto>> GetMilestonesAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return (await _unitOfWork.Repository<ProjectMilestone>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderBy(x => x.TargetDate)
            .Select(MapToDto);
    }

    public async Task<ProjectMilestoneDto> AddMilestoneAsync(Guid projectId, CreateProjectMilestoneDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManagePlan);
        var entity = new ProjectMilestone
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            WorkItemId = dto.WorkItemId,
            Title = dto.Title,
            Description = dto.Description,
            TargetDate = dto.TargetDate,
            ActualDate = dto.ActualDate,
            Status = dto.Status,
            RequiresApproval = dto.RequiresApproval,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectMilestone>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<IEnumerable<ProjectResourceAllocationDto>> GetResourceAllocationsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var allocations = (await _unitOfWork.Repository<ProjectResourceAllocation>()
                .FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderBy(x => x.StartDate)
            .ThenBy(x => x.UserId)
            .ToList();

        return await EnrichResourceAllocationsAsync(allocations.Select(x => MapToDto(x, allocations)).ToList(), allocations);
    }

    public async Task<ProjectResourceAllocationDto> AddResourceAllocationAsync(Guid projectId, CreateProjectResourceAllocationDto dto)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.ManageExecution);
        var entity = new ProjectResourceAllocation
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            WorkItemId = dto.WorkItemId,
            UserId = dto.UserId,
            AllocationRole = dto.AllocationRole,
            AllocationType = dto.AllocationType,
            AllocationValue = dto.AllocationValue,
            PlannedHours = dto.PlannedHours,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            BookingType = dto.BookingType,
            Status = dto.Status,
            Notes = dto.Notes,
            RequiredSkillsJson = SerializeJsonList(dto.RequiredSkills),
            RequiredCertificationsJson = SerializeJsonList(dto.RequiredCertifications),
            RoutingPolicy = NormalizeRoutingPolicy(dto.RoutingPolicy),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectResourceAllocation>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        var allocations = (await _unitOfWork.Repository<ProjectResourceAllocation>().FindAsync(x => x.UserId == dto.UserId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        await PublishActivityAsync(project, "ResourceAllocationAdded", new Dictionary<string, object>
        {
            ["AllocationId"] = entity.Id,
            ["UserId"] = entity.UserId,
            ["Status"] = entity.Status,
            ["BookingType"] = entity.BookingType
        });
        return (await EnrichResourceAllocationsAsync(new List<ProjectResourceAllocationDto> { MapToDto(entity, allocations) }, new List<ProjectResourceAllocation> { entity })).Single();
    }

    public async Task<ProjectResourceAllocationDto> UpdateResourceAllocationAsync(Guid allocationId, CreateProjectResourceAllocationDto dto)
    {
        var repo = _unitOfWork.Repository<ProjectResourceAllocation>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == allocationId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project resource allocation with ID {allocationId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);

        entity.WorkItemId = dto.WorkItemId;
        entity.UserId = dto.UserId;
        entity.AllocationRole = dto.AllocationRole;
        entity.AllocationType = dto.AllocationType;
        entity.AllocationValue = dto.AllocationValue;
        entity.PlannedHours = dto.PlannedHours;
        entity.StartDate = dto.StartDate;
        entity.EndDate = dto.EndDate;
        entity.BookingType = dto.BookingType;
        entity.Status = dto.Status;
        entity.Notes = dto.Notes;
        entity.RequiredSkillsJson = SerializeJsonList(dto.RequiredSkills);
        entity.RequiredCertificationsJson = SerializeJsonList(dto.RequiredCertifications);
        entity.RoutingPolicy = NormalizeRoutingPolicy(dto.RoutingPolicy);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        var allocations = (await repo.FindAsync(x => x.UserId == dto.UserId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        return (await EnrichResourceAllocationsAsync(new List<ProjectResourceAllocationDto> { MapToDto(entity, allocations) }, new List<ProjectResourceAllocation> { entity })).Single();
    }

    public async Task<ProjectResourceAllocationDto> ApproveResourceAllocationAsync(Guid allocationId)
    {
        var repo = _unitOfWork.Repository<ProjectResourceAllocation>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == allocationId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project resource allocation with ID {allocationId} not found");
        var project = await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);

        entity.Status = "Approved";
        entity.ApprovedAt = DateTime.UtcNow;
        entity.ApprovedById = _currentUserProvider.UserId;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        var allocations = (await repo.FindAsync(x => x.UserId == entity.UserId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        await PublishActivityAsync(project, "ResourceAllocationApproved", new Dictionary<string, object>
        {
            ["AllocationId"] = entity.Id,
            ["UserId"] = entity.UserId,
            ["ApprovedAt"] = entity.ApprovedAt ?? DateTime.UtcNow
        });
        return (await EnrichResourceAllocationsAsync(new List<ProjectResourceAllocationDto> { MapToDto(entity, allocations) }, new List<ProjectResourceAllocation> { entity })).Single();
    }

    public async Task<ProjectResourceSubstitutionResultDto> SubstituteResourceAllocationAsync(Guid allocationId, SubstituteProjectResourceAllocationDto dto)
    {
        var repo = _unitOfWork.Repository<ProjectResourceAllocation>();
        var source = await repo.FirstOrDefaultAsync(x => x.Id == allocationId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project resource allocation with ID {allocationId} not found");
        var project = await RequireProjectAsync(source.ProjectId, ProjectAccessOperation.ManageExecution);

        if (dto.ReplacementUserId == source.UserId)
        {
            throw new InvalidOperationException("Replacement user must be different from the current allocation user.");
        }

        if (!IsAllocationActiveForCapacity(source))
        {
            throw new InvalidOperationException("Only active allocations can be substituted.");
        }

        var replacementAllocationValue = dto.TransferAllocationValue ?? source.AllocationValue;
        var replacementPlannedHours = dto.TransferPlannedHours ?? source.PlannedHours ?? replacementAllocationValue;
        if (replacementAllocationValue <= 0m)
        {
            throw new InvalidOperationException("Transfer allocation value must be greater than zero.");
        }

        if (replacementAllocationValue > source.AllocationValue)
        {
            throw new InvalidOperationException("Transfer allocation value cannot exceed the source allocation.");
        }

        if (source.PlannedHours.HasValue && replacementPlannedHours > source.PlannedHours.Value)
        {
            throw new InvalidOperationException("Transfer planned hours cannot exceed the source planned hours.");
        }

        var fullReplacement = dto.FullReplacement || replacementAllocationValue == source.AllocationValue;
        var replacementStatus = dto.ApproveReplacement && string.Equals(source.Status, "Approved", StringComparison.OrdinalIgnoreCase)
            ? "Approved"
            : "Requested";
        var replacement = new ProjectResourceAllocation
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = source.ProjectId,
            WorkItemId = dto.WorkItemId ?? source.WorkItemId,
            UserId = dto.ReplacementUserId,
            AllocationRole = source.AllocationRole,
            AllocationType = source.AllocationType,
            AllocationValue = replacementAllocationValue,
            PlannedHours = replacementPlannedHours,
            StartDate = dto.StartDate ?? source.StartDate,
            EndDate = dto.EndDate ?? source.EndDate,
            BookingType = string.IsNullOrWhiteSpace(dto.BookingType) ? source.BookingType : dto.BookingType.Trim(),
            Status = replacementStatus,
            Notes = AppendResourceNote(source.Notes, "SubstitutionTarget", dto.Reason),
            RequiredSkillsJson = source.RequiredSkillsJson,
            RequiredCertificationsJson = source.RequiredCertificationsJson,
            RoutingPolicy = source.RoutingPolicy,
            SourceAllocationId = source.Id,
            SubstitutionReason = dto.Reason,
            ApprovedAt = string.Equals(replacementStatus, "Approved", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : null,
            ApprovedById = string.Equals(replacementStatus, "Approved", StringComparison.OrdinalIgnoreCase) ? _currentUserProvider.UserId : null,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await repo.AddAsync(replacement);
        await _unitOfWork.SaveChangesAsync();

        source.ReplacementAllocationId = replacement.Id;
        source.SubstitutionReason = dto.Reason;
        source.Notes = AppendResourceNote(source.Notes, fullReplacement ? "Substituted" : "Rebalanced", dto.Reason);
        source.UpdatedBy = _currentUserProvider.Username;
        source.LastModifiedById = _currentUserProvider.UserId;
        if (fullReplacement)
        {
            source.Status = "Substituted";
        }
        else
        {
            source.AllocationValue = decimal.Round(source.AllocationValue - replacementAllocationValue, 2);
            if (source.PlannedHours.HasValue)
            {
                source.PlannedHours = decimal.Round(Math.Max(0m, source.PlannedHours.Value - replacementPlannedHours), 2);
            }

            source.Status = "Rebalanced";
        }

        await repo.UpdateAsync(source);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "ResourceAllocationSubstituted", new Dictionary<string, object>
        {
            ["SourceAllocationId"] = source.Id,
            ["ReplacementAllocationId"] = replacement.Id,
            ["SourceUserId"] = source.UserId,
            ["ReplacementUserId"] = replacement.UserId,
            ["FullReplacement"] = fullReplacement,
            ["TransferredAllocationValue"] = replacementAllocationValue
        });

        var sourceAllocations = (await repo.FindAsync(x => x.UserId == source.UserId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var replacementAllocations = (await repo.FindAsync(x => x.UserId == replacement.UserId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var sourceDto = (await EnrichResourceAllocationsAsync(new List<ProjectResourceAllocationDto> { MapToDto(source, sourceAllocations) }, new List<ProjectResourceAllocation> { source })).Single();
        var replacementDto = (await EnrichResourceAllocationsAsync(new List<ProjectResourceAllocationDto> { MapToDto(replacement, replacementAllocations) }, new List<ProjectResourceAllocation> { replacement })).Single();
        return new ProjectResourceSubstitutionResultDto
        {
            SourceAllocation = sourceDto,
            ReplacementAllocation = replacementDto
        };
    }

    public async Task DeleteResourceAllocationAsync(Guid allocationId)
    {
        var entity = await _unitOfWork.Repository<ProjectResourceAllocation>().FirstOrDefaultAsync(x => x.Id == allocationId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project resource allocation with ID {allocationId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);
        await _unitOfWork.Repository<ProjectResourceAllocation>().DeleteAsync(allocationId);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<ProjectMilestoneDto> UpdateMilestoneAsync(Guid milestoneId, CreateProjectMilestoneDto dto)
    {
        var repo = _unitOfWork.Repository<ProjectMilestone>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == milestoneId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project milestone with ID {milestoneId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManagePlan);
        entity.WorkItemId = dto.WorkItemId;
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.TargetDate = dto.TargetDate;
        entity.ActualDate = dto.ActualDate;
        entity.Status = dto.Status;
        entity.RequiresApproval = dto.RequiresApproval;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task DeleteMilestoneAsync(Guid milestoneId)
    {
        var entity = await _unitOfWork.Repository<ProjectMilestone>().FirstOrDefaultAsync(x => x.Id == milestoneId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project milestone with ID {milestoneId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManagePlan);
        await _unitOfWork.Repository<ProjectMilestone>().DeleteAsync(milestoneId);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectRiskDto>> GetRisksAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return (await _unitOfWork.Repository<ProjectRisk>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderByDescending(x => x.Exposure)
            .Select(MapToDto);
    }

    public async Task<ProjectRiskDto> AddRiskAsync(Guid projectId, CreateProjectRiskDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageGovernance);
        var entity = new ProjectRisk
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            Title = dto.Title,
            Description = dto.Description,
            OwnerId = dto.OwnerId,
            Status = dto.Status,
            Category = dto.Category,
            Probability = dto.Probability,
            Impact = dto.Impact,
            Exposure = dto.Probability * dto.Impact,
            ResponseStrategy = dto.ResponseStrategy,
            MitigationPlan = dto.MitigationPlan,
            DueDate = dto.DueDate,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };
        await _unitOfWork.Repository<ProjectRisk>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<ProjectRiskDto> UpdateRiskAsync(Guid riskId, CreateProjectRiskDto dto)
    {
        var repo = _unitOfWork.Repository<ProjectRisk>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == riskId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project risk with ID {riskId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.OwnerId = dto.OwnerId;
        entity.Status = dto.Status;
        entity.Category = dto.Category;
        entity.Probability = dto.Probability;
        entity.Impact = dto.Impact;
        entity.Exposure = dto.Probability * dto.Impact;
        entity.ResponseStrategy = dto.ResponseStrategy;
        entity.MitigationPlan = dto.MitigationPlan;
        entity.DueDate = dto.DueDate;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task DeleteRiskAsync(Guid riskId)
    {
        var entity = await _unitOfWork.Repository<ProjectRisk>().FirstOrDefaultAsync(x => x.Id == riskId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project risk with ID {riskId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);
        await _unitOfWork.Repository<ProjectRisk>().DeleteAsync(riskId);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectIssueDto>> GetIssuesAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return (await _unitOfWork.Repository<ProjectIssue>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderBy(x => x.TargetResolutionDate)
            .Select(MapToDto);
    }

    public async Task<ProjectIssueDto> AddIssueAsync(Guid projectId, CreateProjectIssueDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageGovernance);
        var entity = new ProjectIssue
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            Title = dto.Title,
            Description = dto.Description,
            OwnerId = dto.OwnerId,
            Status = dto.Status,
            Severity = dto.Severity,
            TargetResolutionDate = dto.TargetResolutionDate,
            RootCause = dto.RootCause,
            CorrectiveAction = dto.CorrectiveAction,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };
        await _unitOfWork.Repository<ProjectIssue>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<ProjectIssueDto> UpdateIssueAsync(Guid issueId, CreateProjectIssueDto dto)
    {
        var repo = _unitOfWork.Repository<ProjectIssue>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == issueId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project issue with ID {issueId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.OwnerId = dto.OwnerId;
        entity.Status = dto.Status;
        entity.Severity = dto.Severity;
        entity.TargetResolutionDate = dto.TargetResolutionDate;
        entity.RootCause = dto.RootCause;
        entity.CorrectiveAction = dto.CorrectiveAction;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task DeleteIssueAsync(Guid issueId)
    {
        var entity = await _unitOfWork.Repository<ProjectIssue>().FirstOrDefaultAsync(x => x.Id == issueId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project issue with ID {issueId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);
        await _unitOfWork.Repository<ProjectIssue>().DeleteAsync(issueId);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectQualityCheckpointDto>> GetQualityCheckpointsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return (await _unitOfWork.Repository<ProjectQualityCheckpoint>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderBy(x => x.DueDate ?? DateTime.MaxValue)
            .ThenBy(x => x.Title)
            .Select(MapToDto);
    }

    public async Task<ProjectQualityCheckpointDto> AddQualityCheckpointAsync(Guid projectId, CreateProjectQualityCheckpointDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageGovernance);
        if (dto.WorkItemId.HasValue)
        {
            var workItem = await _unitOfWork.Repository<ProjectWorkItem>().FirstOrDefaultAsync(x => x.Id == dto.WorkItemId.Value && x.TenantId == _currentUserProvider.TenantId);
            if (workItem == null || workItem.ProjectId != projectId)
            {
                throw new InvalidOperationException("The selected work item does not belong to this project.");
            }
        }
        if (dto.DeliverableId.HasValue)
        {
            var deliverable = await _unitOfWork.Repository<ProjectDeliverable>().FirstOrDefaultAsync(x => x.Id == dto.DeliverableId.Value && x.TenantId == _currentUserProvider.TenantId);
            if (deliverable == null || deliverable.ProjectId != projectId)
            {
                throw new InvalidOperationException("The selected deliverable does not belong to this project.");
            }
        }

        var entity = new ProjectQualityCheckpoint
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            WorkItemId = dto.WorkItemId,
            DeliverableId = dto.DeliverableId,
            QaOwnerId = dto.QaOwnerId,
            Title = dto.Title,
            Description = dto.Description,
            Status = dto.Status,
            DueDate = dto.DueDate,
            RequiresQaSignOff = dto.RequiresQaSignOff,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };
        await _unitOfWork.Repository<ProjectQualityCheckpoint>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<ProjectQualityCheckpointDto> SignOffQualityCheckpointAsync(Guid checkpointId, string? notes = null)
    {
        var repo = _unitOfWork.Repository<ProjectQualityCheckpoint>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == checkpointId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project quality checkpoint with ID {checkpointId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);

        entity.Status = "SignedOff";
        entity.SignedOffAt = DateTime.UtcNow;
        entity.SignedOffById = _currentUserProvider.UserId;
        entity.SignOffNotes = notes;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task DeleteQualityCheckpointAsync(Guid checkpointId)
    {
        var entity = await _unitOfWork.Repository<ProjectQualityCheckpoint>().FirstOrDefaultAsync(x => x.Id == checkpointId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project quality checkpoint with ID {checkpointId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);
        await _unitOfWork.Repository<ProjectQualityCheckpoint>().DeleteAsync(checkpointId);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectNonConformanceDto>> GetNonConformancesAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return (await _unitOfWork.Repository<ProjectNonConformance>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderByDescending(x => x.ReportedAt)
            .Select(MapToDto);
    }

    public async Task<ProjectNonConformanceDto> AddNonConformanceAsync(Guid projectId, CreateProjectNonConformanceDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageGovernance);
        if (dto.QualityCheckpointId.HasValue)
        {
            var checkpoint = await _unitOfWork.Repository<ProjectQualityCheckpoint>().FirstOrDefaultAsync(x => x.Id == dto.QualityCheckpointId.Value && x.TenantId == _currentUserProvider.TenantId);
            if (checkpoint == null || checkpoint.ProjectId != projectId)
            {
                throw new InvalidOperationException("The selected quality checkpoint does not belong to this project.");
            }
        }
        if (dto.DeliverableId.HasValue)
        {
            var deliverable = await _unitOfWork.Repository<ProjectDeliverable>().FirstOrDefaultAsync(x => x.Id == dto.DeliverableId.Value && x.TenantId == _currentUserProvider.TenantId);
            if (deliverable == null || deliverable.ProjectId != projectId)
            {
                throw new InvalidOperationException("The selected deliverable does not belong to this project.");
            }
        }

        var entity = new ProjectNonConformance
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            QualityCheckpointId = dto.QualityCheckpointId,
            DeliverableId = dto.DeliverableId,
            OwnerId = dto.OwnerId,
            Title = dto.Title,
            Description = dto.Description,
            Severity = dto.Severity,
            Status = dto.Status,
            TargetResolutionDate = dto.TargetResolutionDate,
            CorrectiveAction = dto.CorrectiveAction,
            PreventiveAction = dto.PreventiveAction,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };
        await _unitOfWork.Repository<ProjectNonConformance>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<ProjectNonConformanceDto> ResolveNonConformanceAsync(Guid nonConformanceId, string? notes = null)
    {
        var repo = _unitOfWork.Repository<ProjectNonConformance>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == nonConformanceId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project non-conformance with ID {nonConformanceId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);

        entity.Status = "Resolved";
        entity.ResolvedAt = DateTime.UtcNow;
        entity.ResolutionNotes = notes;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task DeleteNonConformanceAsync(Guid nonConformanceId)
    {
        var entity = await _unitOfWork.Repository<ProjectNonConformance>().FirstOrDefaultAsync(x => x.Id == nonConformanceId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project non-conformance with ID {nonConformanceId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);
        await _unitOfWork.Repository<ProjectNonConformance>().DeleteAsync(nonConformanceId);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectChangeRequestDto>> GetChangeRequestsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return (await _unitOfWork.Repository<ProjectChangeRequest>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderByDescending(x => x.CreatedAt)
            .Select(MapToDto);
    }

    public async Task<ProjectChangeRequestDto> AddChangeRequestAsync(Guid projectId, CreateProjectChangeRequestDto dto)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManageGovernance);
        var entity = new ProjectChangeRequest
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            Title = dto.Title,
            Description = dto.Description,
            ChangeType = dto.ChangeType,
            Status = dto.Status,
            BusinessImpact = dto.BusinessImpact,
            RiskImpact = dto.RiskImpact,
            CostImpact = dto.CostImpact,
            ScheduleImpactDays = dto.ScheduleImpactDays,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };
        await _unitOfWork.Repository<ProjectChangeRequest>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<ProjectChangeRequestDto> UpdateChangeRequestAsync(Guid changeRequestId, CreateProjectChangeRequestDto dto)
    {
        var repo = _unitOfWork.Repository<ProjectChangeRequest>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == changeRequestId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project change request with ID {changeRequestId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.ChangeType = dto.ChangeType;
        entity.Status = dto.Status;
        entity.BusinessImpact = dto.BusinessImpact;
        entity.RiskImpact = dto.RiskImpact;
        entity.CostImpact = dto.CostImpact;
        entity.ScheduleImpactDays = dto.ScheduleImpactDays;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task DeleteChangeRequestAsync(Guid changeRequestId)
    {
        var entity = await _unitOfWork.Repository<ProjectChangeRequest>().FirstOrDefaultAsync(x => x.Id == changeRequestId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project change request with ID {changeRequestId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);
        await _unitOfWork.Repository<ProjectChangeRequest>().DeleteAsync(changeRequestId);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectBillingScheduleDto>> GetBillingSchedulesAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return (await _unitOfWork.Repository<ProjectBillingSchedule>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderBy(x => x.BillingDate)
            .Select(MapToDto);
    }

    public async Task<ProjectBillingScheduleDto> AddBillingScheduleAsync(Guid projectId, CreateProjectBillingScheduleDto dto)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.ManageFinancials);
        var contractMilestone = await GetContractMilestoneAsync(dto.ContractId ?? project.ContractId, dto.ContractMilestoneId);
        var entity = new ProjectBillingSchedule
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            ContractId = dto.ContractId ?? project.ContractId,
            ContractMilestoneId = dto.ContractMilestoneId,
            MilestoneId = dto.MilestoneId,
            Name = string.IsNullOrWhiteSpace(dto.Name) ? contractMilestone?.MilestoneName ?? "Billing Event" : dto.Name,
            BillingType = dto.BillingType,
            Amount = dto.Amount > 0m ? dto.Amount : contractMilestone?.PaymentAmount ?? 0m,
            BillingPercentage = dto.BillingPercentage ?? contractMilestone?.PaymentPercentage,
            BillingDate = dto.BillingDate == default ? contractMilestone?.PlannedDate ?? DateTime.UtcNow.Date : dto.BillingDate,
            Status = dto.Status,
            Description = dto.Description,
            IsBillable = dto.IsBillable,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectBillingSchedule>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "BillingScheduleAdded", new Dictionary<string, object>
        {
            ["BillingScheduleId"] = entity.Id,
            ["BillingType"] = entity.BillingType,
            ["Amount"] = entity.Amount,
            ["BillingDate"] = entity.BillingDate
        });
        return MapToDto(entity);
    }

    public async Task<ProjectBillingScheduleDto> UpdateBillingScheduleAsync(Guid billingScheduleId, CreateProjectBillingScheduleDto dto)
    {
        var repo = _unitOfWork.Repository<ProjectBillingSchedule>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == billingScheduleId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project billing schedule with ID {billingScheduleId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        var contractMilestone = await GetContractMilestoneAsync(dto.ContractId ?? entity.ContractId, dto.ContractMilestoneId);

        entity.ContractId = dto.ContractId ?? entity.ContractId;
        entity.ContractMilestoneId = dto.ContractMilestoneId;
        entity.MilestoneId = dto.MilestoneId;
        entity.Name = string.IsNullOrWhiteSpace(dto.Name) ? contractMilestone?.MilestoneName ?? entity.Name : dto.Name;
        entity.BillingType = dto.BillingType;
        entity.Amount = dto.Amount > 0m ? dto.Amount : contractMilestone?.PaymentAmount ?? entity.Amount;
        entity.BillingPercentage = dto.BillingPercentage ?? contractMilestone?.PaymentPercentage;
        entity.BillingDate = dto.BillingDate == default ? contractMilestone?.PlannedDate ?? entity.BillingDate : dto.BillingDate;
        entity.Status = dto.Status;
        entity.Description = dto.Description;
        entity.IsBillable = dto.IsBillable;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task DeleteBillingScheduleAsync(Guid billingScheduleId)
    {
        var entity = await _unitOfWork.Repository<ProjectBillingSchedule>().FirstOrDefaultAsync(x => x.Id == billingScheduleId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project billing schedule with ID {billingScheduleId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        await _unitOfWork.Repository<ProjectBillingSchedule>().DeleteAsync(billingScheduleId);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectInvoiceRequestDto>> GetInvoiceRequestsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return (await _unitOfWork.Repository<ProjectInvoiceRequest>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderByDescending(x => x.RequestedAt)
            .Select(MapToDto);
    }

    public async Task<ProjectInvoiceRequestDto> CreateInvoiceRequestAsync(Guid projectId, CreateProjectInvoiceRequestDto dto)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.ManageFinancials);
        var currencyCode = await ResolveProjectCurrencyAsync(dto.Currency);
        var entity = new ProjectInvoiceRequest
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            BillingScheduleId = dto.BillingScheduleId,
            ContractId = dto.ContractId ?? project.ContractId,
            RequestNumber = await GenerateInvoiceRequestNumberAsync(),
            RequestedAmount = dto.RequestedAmount,
            Currency = currencyCode,
            Status = dto.Status,
            ExternalReference = dto.ExternalReference,
            Notes = dto.Notes,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectInvoiceRequest>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "InvoiceRequestCreated", new Dictionary<string, object>
        {
            ["InvoiceRequestId"] = entity.Id,
            ["RequestNumber"] = entity.RequestNumber,
            ["RequestedAmount"] = entity.RequestedAmount,
            ["Status"] = entity.Status
        });
        return MapToDto(entity);
    }

    public async Task<ProjectInvoiceRequestDto> GenerateInvoiceRequestFromScheduleAsync(Guid billingScheduleId, string? notes = null)
    {
        var scheduleRepo = _unitOfWork.Repository<ProjectBillingSchedule>();
        var schedule = await scheduleRepo.FirstOrDefaultAsync(x => x.Id == billingScheduleId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project billing schedule with ID {billingScheduleId} not found");
        var project = await RequireProjectAsync(schedule.ProjectId, ProjectAccessOperation.ManageFinancials);
        var currencyCode = await GetProjectBaseCurrencyCodeAsync();

        var entity = new ProjectInvoiceRequest
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = schedule.ProjectId,
            BillingScheduleId = schedule.Id,
            ContractId = schedule.ContractId,
            RequestNumber = await GenerateInvoiceRequestNumberAsync(),
            RequestedAmount = schedule.Amount,
            Currency = currencyCode,
            Status = "Draft",
            Notes = notes,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        schedule.Status = "Invoiced";
        schedule.UpdatedBy = _currentUserProvider.Username;
        schedule.LastModifiedById = _currentUserProvider.UserId;
        await _unitOfWork.Repository<ProjectInvoiceRequest>().AddAsync(entity);
        await scheduleRepo.UpdateAsync(schedule);
        if (schedule.ContractMilestoneId.HasValue)
        {
            await _contractService.UpdateMilestoneStatusAsync(schedule.ContractMilestoneId.Value, new UpdateMilestoneStatusDto
            {
                Status = "Invoiced",
                InvoiceNumber = entity.RequestNumber,
                Notes = notes
            });
        }
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "InvoiceRequestGenerated", new Dictionary<string, object>
        {
            ["InvoiceRequestId"] = entity.Id,
            ["BillingScheduleId"] = schedule.Id,
            ["RequestNumber"] = entity.RequestNumber,
            ["RequestedAmount"] = entity.RequestedAmount
        });
        return MapToDto(entity);
    }

    public async Task<ProjectInvoiceRequestDto> SubmitInvoiceRequestAsync(Guid invoiceRequestId, string? comments = null)
    {
        var repo = _unitOfWork.Repository<ProjectInvoiceRequest>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == invoiceRequestId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project invoice request with ID {invoiceRequestId} not found");
        var project = await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        if (!string.Equals(entity.Status, "Draft", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(entity.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only draft or rejected invoice requests can be submitted.");
        }

        entity.Status = "Submitted";
        entity.SubmittedAt = DateTime.UtcNow;
        entity.Notes = AppendDecisionNote(entity.Notes, "Submitted", comments);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "InvoiceRequestSubmitted", new Dictionary<string, object>
        {
            ["InvoiceRequestId"] = entity.Id,
            ["RequestNumber"] = entity.RequestNumber,
            ["RequestedAmount"] = entity.RequestedAmount
        });
        return MapToDto(entity);
    }

    public async Task<ProjectInvoiceRequestDto> MarkInvoiceRequestSentToFinanceAsync(Guid invoiceRequestId, string? externalReference = null, string? comments = null)
    {
        var repo = _unitOfWork.Repository<ProjectInvoiceRequest>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == invoiceRequestId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project invoice request with ID {invoiceRequestId} not found");
        var project = await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        if (!string.Equals(entity.Status, "Submitted", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(entity.Status, "Draft", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only draft or submitted invoice requests can be sent to finance.");
        }

        entity.Status = "SentToFinance";
        entity.SubmittedAt ??= DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(externalReference))
        {
            entity.ExternalReference = externalReference.Trim();
        }
        entity.Notes = AppendDecisionNote(entity.Notes, "SentToFinance", comments);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "InvoiceRequestSentToFinance", new Dictionary<string, object>
        {
            ["InvoiceRequestId"] = entity.Id,
            ["RequestNumber"] = entity.RequestNumber,
            ["ExternalReference"] = entity.ExternalReference ?? string.Empty
        });
        return MapToDto(entity);
    }

    public async Task<ProjectInvoiceRequestDto> MarkInvoiceRequestInvoicedAsync(Guid invoiceRequestId, string? externalReference = null, string? comments = null)
    {
        var repo = _unitOfWork.Repository<ProjectInvoiceRequest>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == invoiceRequestId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project invoice request with ID {invoiceRequestId} not found");
        var project = await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        if (!string.Equals(entity.Status, "SentToFinance", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(entity.Status, "Submitted", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only submitted or finance-sent invoice requests can be marked as invoiced.");
        }

        entity.Status = "Invoiced";
        if (!string.IsNullOrWhiteSpace(externalReference))
        {
            entity.ExternalReference = externalReference.Trim();
        }
        entity.Notes = AppendDecisionNote(entity.Notes, "Invoiced", comments);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);

        if (entity.BillingScheduleId.HasValue)
        {
            var scheduleRepo = _unitOfWork.Repository<ProjectBillingSchedule>();
            var schedule = await scheduleRepo.FirstOrDefaultAsync(x => x.Id == entity.BillingScheduleId.Value && x.TenantId == _currentUserProvider.TenantId);
            if (schedule != null && !string.Equals(schedule.Status, "Invoiced", StringComparison.OrdinalIgnoreCase))
            {
                schedule.Status = "Invoiced";
                schedule.UpdatedBy = _currentUserProvider.Username;
                schedule.LastModifiedById = _currentUserProvider.UserId;
                await scheduleRepo.UpdateAsync(schedule);
            }
        }

        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "InvoiceRequestMarkedInvoiced", new Dictionary<string, object>
        {
            ["InvoiceRequestId"] = entity.Id,
            ["RequestNumber"] = entity.RequestNumber,
            ["ExternalReference"] = entity.ExternalReference ?? string.Empty
        });
        return MapToDto(entity);
    }

    public async Task<ProjectInvoiceRequestDto> MarkInvoiceRequestPaidAsync(Guid invoiceRequestId, string? comments = null)
    {
        var repo = _unitOfWork.Repository<ProjectInvoiceRequest>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == invoiceRequestId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project invoice request with ID {invoiceRequestId} not found");
        var project = await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        if (!string.Equals(entity.Status, "Invoiced", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(entity.Status, "SentToFinance", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only invoiced or finance-sent invoice requests can be marked as paid.");
        }

        entity.Status = "Paid";
        entity.Notes = AppendDecisionNote(entity.Notes, "Paid", comments);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);

        var revenueRecognitionRepo = _unitOfWork.Repository<ProjectRevenueRecognition>();
        var recognition = await revenueRecognitionRepo.FirstOrDefaultAsync(x =>
            x.ProjectId == entity.ProjectId
            && x.TenantId == _currentUserProvider.TenantId
            && x.InvoiceRequestId == entity.Id);
        if (recognition != null)
        {
            recognition.CashCollected = Math.Max(recognition.CashCollected, entity.RequestedAmount);
            recognition.Status = "Collected";
            recognition.Notes = AppendDecisionNote(recognition.Notes, "Paid", comments);
            recognition.UpdatedBy = _currentUserProvider.Username;
            recognition.LastModifiedById = _currentUserProvider.UserId;
            await revenueRecognitionRepo.UpdateAsync(recognition);
        }

        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "InvoiceRequestMarkedPaid", new Dictionary<string, object>
        {
            ["InvoiceRequestId"] = entity.Id,
            ["RequestNumber"] = entity.RequestNumber,
            ["RequestedAmount"] = entity.RequestedAmount
        });
        return MapToDto(entity);
    }

    public async Task<IEnumerable<ProjectContractLookupDto>> GetContractLookupAsync(Guid? businessPartnerId = null, string? search = null)
    {
        var contracts = businessPartnerId.HasValue
            ? await _contractService.GetByBusinessPartnerIdAsync(businessPartnerId.Value)
            : await _contractService.GetActiveContractsAsync();

        return contracts
            .Where(x => string.IsNullOrWhiteSpace(search)
                || x.ContractNumber.Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.ContractTitle.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.ContractNumber)
            .Select(x => new ProjectContractLookupDto
            {
                Id = x.Id,
                ContractNumber = x.ContractNumber,
                ContractTitle = x.ContractTitle,
                BusinessPartnerId = Guid.Empty,
                BusinessPartnerName = x.BusinessPartnerName,
                Status = x.Status,
                ContractValue = x.ContractValue,
                Currency = x.Currency
            })
            .ToList();
    }

    public async Task<IEnumerable<ProjectContractMilestoneLookupDto>> GetContractMilestonesAsync(Guid contractId)
    {
        var contract = await _contractService.GetByIdAsync(contractId)
            ?? throw new InvalidOperationException($"Contract with ID {contractId} not found");

        return contract.Milestones
            .OrderBy(x => x.SequenceNumber)
            .Select(x => new ProjectContractMilestoneLookupDto
            {
                Id = x.Id,
                ContractId = x.ContractId,
                MilestoneName = x.MilestoneName,
                PaymentPercentage = x.PaymentPercentage,
                PaymentAmount = x.PaymentAmount,
                PlannedDate = x.PlannedDate,
                Status = x.Status,
                InvoiceNumber = x.InvoiceNumber
            })
            .ToList();
    }

    public async Task<IEnumerable<ProjectDeliverableDto>> GetDeliverablesAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var deliverables = (await _unitOfWork.Repository<ProjectDeliverable>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderBy(x => x.TargetDate)
            .ThenBy(x => x.Title)
            .Select(MapToDto)
            .ToList();
        return await AttachDeliverableExternalReviewsAsync(deliverables);
    }

    public async Task<ProjectDeliverableDto> AddDeliverableAsync(Guid projectId, CreateProjectDeliverableDto dto)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.ManageGovernance);
        var entity = new ProjectDeliverable
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            WorkItemId = dto.WorkItemId,
            MilestoneId = dto.MilestoneId,
            Title = dto.Title,
            Description = dto.Description,
            Status = dto.Status,
            TargetDate = dto.TargetDate,
            ExternalSubmissionAllowed = dto.ExternalSubmissionAllowed,
            ExternalSignOffRequired = dto.ExternalSignOffRequired,
            IsExternalVisible = dto.IsExternalVisible,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };
        await _unitOfWork.Repository<ProjectDeliverable>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "DeliverableAdded", new Dictionary<string, object> { ["DeliverableId"] = entity.Id, ["Title"] = entity.Title });
        return MapToDto(entity);
    }

    public async Task<ProjectDeliverableDto> UpdateDeliverableAsync(Guid deliverableId, CreateProjectDeliverableDto dto)
    {
        var repo = _unitOfWork.Repository<ProjectDeliverable>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == deliverableId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project deliverable with ID {deliverableId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);
        entity.WorkItemId = dto.WorkItemId;
        entity.MilestoneId = dto.MilestoneId;
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.Status = dto.Status;
        entity.TargetDate = dto.TargetDate;
        entity.ExternalSubmissionAllowed = dto.ExternalSubmissionAllowed;
        entity.ExternalSignOffRequired = dto.ExternalSignOffRequired;
        entity.IsExternalVisible = dto.IsExternalVisible;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<ProjectDeliverableDto> SubmitDeliverableAsync(Guid deliverableId, SubmitProjectDeliverableDto dto)
    {
        var repo = _unitOfWork.Repository<ProjectDeliverable>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == deliverableId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project deliverable with ID {deliverableId} not found");
        var project = await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);
        ValidateDeliverableSubmission(entity);
        entity.SubmittedDocumentId = dto.SubmittedDocumentId;
        entity.SubmittedAt = DateTime.UtcNow;
        entity.SubmittedById = _currentUserProvider.UserId;
        entity.Status = entity.ExternalSignOffRequired
            ? DeliverableStatusPendingExternalSignOff
            : DeliverableStatusPendingApproval;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);

        await AddDeliverableCommentAsync(entity, "DeliverableSubmission", dto.Notes);

        if (!entity.ExternalSignOffRequired)
        {
            await SubmitDeliverableWorkflowAsync(entity);
        }

        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "DeliverableSubmitted", new Dictionary<string, object>
        {
            ["DeliverableId"] = entity.Id,
            ["Title"] = entity.Title,
            ["Status"] = entity.Status
        });
        return MapToDto(entity);
    }

    public async Task<ProjectDeliverableDto> ApproveDeliverableAsync(Guid deliverableId, string? notes = null)
    {
        var repo = _unitOfWork.Repository<ProjectDeliverable>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == deliverableId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project deliverable with ID {deliverableId} not found");
        var project = await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ApproveWorkflow);
        if (!string.Equals(entity.Status, DeliverableStatusPendingApproval, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Project deliverable must be in {DeliverableStatusPendingApproval} status to approve.");
        }

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync(ProjectDeliverableWorkflowEntityType, deliverableId, _currentUserProvider.UserId);
        if (!canApprove)
        {
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current deliverable workflow step.");
        }

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(ProjectDeliverableWorkflowEntityType, deliverableId, _currentUserProvider.UserId, "Approve", notes);
        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to approve deliverable.");
        }

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(ProjectDeliverableWorkflowEntityType);
        adapter.ApplyApprovalOutcome(entity, workflowResult.Outcome, _currentUserProvider.UserId, notes);
        entity.AcceptanceNotes = notes;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await AddDeliverableCommentAsync(entity, "DeliverableApproval", notes);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "DeliverableApproved", new Dictionary<string, object>
        {
            ["DeliverableId"] = entity.Id,
            ["Title"] = entity.Title,
            ["Status"] = entity.Status
        });
        return MapToDto(entity);
    }

    public async Task<ProjectDeliverableDto> RejectDeliverableAsync(Guid deliverableId, string? notes = null)
    {
        var repo = _unitOfWork.Repository<ProjectDeliverable>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == deliverableId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project deliverable with ID {deliverableId} not found");
        var project = await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ApproveWorkflow);
        if (!string.Equals(entity.Status, DeliverableStatusPendingApproval, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Project deliverable must be in {DeliverableStatusPendingApproval} status to reject.");
        }

        var rejectionText = !string.IsNullOrWhiteSpace(notes) ? notes.Trim() : "Rejected";
        var canApprove = await _workflowIntegrationService.CanUserApproveAsync(ProjectDeliverableWorkflowEntityType, deliverableId, _currentUserProvider.UserId);
        if (!canApprove)
        {
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current deliverable workflow step.");
        }

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(ProjectDeliverableWorkflowEntityType, deliverableId, _currentUserProvider.UserId, "Reject", rejectionText);
        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to reject deliverable.");
        }

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(ProjectDeliverableWorkflowEntityType);
        adapter.ApplyApprovalOutcome(entity, workflowResult.Outcome, _currentUserProvider.UserId, rejectionText);
        entity.AcceptanceNotes = notes;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await AddDeliverableCommentAsync(entity, "DeliverableRejection", rejectionText);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "DeliverableRejected", new Dictionary<string, object>
        {
            ["DeliverableId"] = entity.Id,
            ["Title"] = entity.Title,
            ["Status"] = entity.Status
        });
        return MapToDto(entity);
    }

    public async Task DeleteDeliverableAsync(Guid deliverableId)
    {
        var repo = _unitOfWork.Repository<ProjectDeliverable>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == deliverableId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project deliverable with ID {deliverableId} not found");
        var project = await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);

        if (string.Equals(entity.Status, DeliverableStatusApproved, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Approved deliverables cannot be deleted.");
        }

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "DeliverableDeleted", new Dictionary<string, object>
        {
            ["DeliverableId"] = entity.Id,
            ["Title"] = entity.Title
        });
    }

    public async Task<IEnumerable<ProjectTaskDependencyDto>> GetTaskDependenciesAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return (await _unitOfWork.Repository<ProjectTaskDependency>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderBy(x => x.PredecessorWorkItemId)
            .ThenBy(x => x.SuccessorWorkItemId)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<ProjectTaskDependencyDto> AddTaskDependencyAsync(Guid projectId, CreateProjectTaskDependencyDto dto)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.ManagePlan);
        if (dto.PredecessorWorkItemId == dto.SuccessorWorkItemId)
        {
            throw new InvalidOperationException("A work item cannot depend on itself.");
        }

        var workItems = (await _unitOfWork.Repository<ProjectWorkItem>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        if (!workItems.Any(x => x.Id == dto.PredecessorWorkItemId) || !workItems.Any(x => x.Id == dto.SuccessorWorkItemId))
        {
            throw new InvalidOperationException("Both predecessor and successor work items must belong to the same project.");
        }

        var repo = _unitOfWork.Repository<ProjectTaskDependency>();
        var existing = await repo.FirstOrDefaultAsync(x =>
            x.ProjectId == projectId &&
            x.PredecessorWorkItemId == dto.PredecessorWorkItemId &&
            x.SuccessorWorkItemId == dto.SuccessorWorkItemId &&
            x.TenantId == _currentUserProvider.TenantId);
        if (existing != null)
        {
            throw new InvalidOperationException("This task dependency already exists.");
        }

        var dependencies = (await repo.FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        if (WouldCreateDependencyCycle(dependencies, dto.PredecessorWorkItemId, dto.SuccessorWorkItemId))
        {
            throw new InvalidOperationException("This dependency would create a circular schedule path.");
        }

        var entity = new ProjectTaskDependency
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            PredecessorWorkItemId = dto.PredecessorWorkItemId,
            SuccessorWorkItemId = dto.SuccessorWorkItemId,
            DependencyType = dto.DependencyType,
            LagDays = dto.LagDays,
            IsEnforced = dto.IsEnforced,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };
        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "TaskDependencyAdded", new Dictionary<string, object> { ["DependencyId"] = entity.Id });
        return MapToDto(entity);
    }

    public async Task DeleteTaskDependencyAsync(Guid dependencyId)
    {
        var repo = _unitOfWork.Repository<ProjectTaskDependency>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == dependencyId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project dependency with ID {dependencyId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManagePlan);
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectInterdependencyDto>> GetInterdependenciesAsync(Guid? projectId = null, Guid? portfolioId = null, Guid? programId = null)
    {
        EnsureInternalProjectAccess();
        var accessibleProjects = (await GetAccessibleProjectsAsync(take: 2000))
            .Where(x => !portfolioId.HasValue || x.PortfolioId == portfolioId.Value)
            .Where(x => !programId.HasValue || x.ProgramId == programId.Value)
            .ToDictionary(x => x.Id);

        if (accessibleProjects.Count == 0)
        {
            return Array.Empty<ProjectInterdependencyDto>();
        }

        var repo = _unitOfWork.Repository<ProjectInterdependency>();
        var dependencies = (await repo.FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && (!projectId.HasValue || x.SourceProjectId == projectId.Value || x.TargetProjectId == projectId.Value)
                && accessibleProjects.Keys.Contains(x.SourceProjectId)
                && accessibleProjects.Keys.Contains(x.TargetProjectId)))
            .OrderByDescending(x => x.DueDate.HasValue)
            .ThenBy(x => x.DueDate)
            .ThenBy(x => x.Title)
            .ToList();

        return dependencies.Select(x => MapToDto(x, accessibleProjects)).ToList();
    }

    public async Task<ProjectInterdependencyDto> CreateInterdependencyAsync(CreateProjectInterdependencyDto dto)
    {
        EnsureInternalProjectAccess();
        if (dto.SourceProjectId == dto.TargetProjectId)
        {
            throw new InvalidOperationException("A project cannot depend on itself.");
        }

        var sourceProject = await RequireProjectAsync(dto.SourceProjectId, ProjectAccessOperation.ManagePlan);
        var targetProject = await RequireProjectAsync(dto.TargetProjectId, ProjectAccessOperation.View);
        var repo = _unitOfWork.Repository<ProjectInterdependency>();
        var existing = await repo.FirstOrDefaultAsync(x =>
            x.TenantId == _currentUserProvider.TenantId
            && x.SourceProjectId == dto.SourceProjectId
            && x.TargetProjectId == dto.TargetProjectId
            && string.Equals(x.DependencyType, dto.DependencyType, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            throw new InvalidOperationException("This inter-project dependency already exists.");
        }

        var entity = new ProjectInterdependency
        {
            TenantId = _currentUserProvider.TenantId,
            SourceProjectId = dto.SourceProjectId,
            TargetProjectId = dto.TargetProjectId,
            DependencyType = dto.DependencyType.Trim(),
            Status = string.IsNullOrWhiteSpace(dto.Status) ? "Open" : dto.Status.Trim(),
            ImpactLevel = string.IsNullOrWhiteSpace(dto.ImpactLevel) ? "Medium" : dto.ImpactLevel.Trim(),
            OwnerId = dto.OwnerId,
            DueDate = dto.DueDate,
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim(),
            MitigationPlan = dto.MitigationPlan?.Trim(),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(sourceProject, "InterdependencyCreated", new Dictionary<string, object>
        {
            ["InterdependencyId"] = entity.Id,
            ["TargetProjectId"] = targetProject.Id
        });

        return MapToDto(entity, new Dictionary<Guid, Project>
        {
            [sourceProject.Id] = sourceProject,
            [targetProject.Id] = targetProject
        });
    }

    public async Task<ProjectInterdependencyDto> UpdateInterdependencyAsync(Guid id, CreateProjectInterdependencyDto dto)
    {
        EnsureInternalProjectAccess();
        if (dto.SourceProjectId == dto.TargetProjectId)
        {
            throw new InvalidOperationException("A project cannot depend on itself.");
        }

        var repo = _unitOfWork.Repository<ProjectInterdependency>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project interdependency with ID {id} not found");
        var sourceProject = await RequireProjectAsync(dto.SourceProjectId, ProjectAccessOperation.ManagePlan);
        var targetProject = await RequireProjectAsync(dto.TargetProjectId, ProjectAccessOperation.View);
        var duplicate = await repo.FirstOrDefaultAsync(x =>
            x.TenantId == _currentUserProvider.TenantId
            && x.Id != id
            && x.SourceProjectId == dto.SourceProjectId
            && x.TargetProjectId == dto.TargetProjectId
            && string.Equals(x.DependencyType, dto.DependencyType, StringComparison.OrdinalIgnoreCase));
        if (duplicate != null)
        {
            throw new InvalidOperationException("This inter-project dependency already exists.");
        }

        entity.SourceProjectId = dto.SourceProjectId;
        entity.TargetProjectId = dto.TargetProjectId;
        entity.DependencyType = dto.DependencyType.Trim();
        entity.Status = string.IsNullOrWhiteSpace(dto.Status) ? "Open" : dto.Status.Trim();
        entity.ImpactLevel = string.IsNullOrWhiteSpace(dto.ImpactLevel) ? "Medium" : dto.ImpactLevel.Trim();
        entity.OwnerId = dto.OwnerId;
        entity.DueDate = dto.DueDate;
        entity.Title = dto.Title.Trim();
        entity.Description = dto.Description?.Trim();
        entity.MitigationPlan = dto.MitigationPlan?.Trim();
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity, new Dictionary<Guid, Project>
        {
            [sourceProject.Id] = sourceProject,
            [targetProject.Id] = targetProject
        });
    }

    public async Task DeleteInterdependencyAsync(Guid id)
    {
        EnsureInternalProjectAccess();
        var repo = _unitOfWork.Repository<ProjectInterdependency>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project interdependency with ID {id} not found");
        await RequireProjectAsync(entity.SourceProjectId, ProjectAccessOperation.ManagePlan);
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectBaselineDto>> GetBaselinesAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var baselines = (await _unitOfWork.Repository<ProjectBaseline>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderByDescending(x => x.CreatedOn)
            .ToList();
        return baselines.Select(MapToDto).ToList();
    }

    public async Task<ProjectBaselineDto> CreateBaselineAsync(Guid projectId, CreateProjectBaselineDto dto)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.ManagePlan);
        var workItems = (await _unitOfWork.Repository<ProjectWorkItem>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var milestones = (await _unitOfWork.Repository<ProjectMilestone>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var snapshotJson = JsonSerializer.Serialize(new
        {
            project.ProgressPercent,
            project.EstimatedBudget,
            project.ApprovedBudget,
            WorkItems = workItems.Select(x => new { x.Id, x.Title, x.PlannedStartDate, x.PlannedEndDate, x.PercentComplete }),
            Milestones = milestones.Select(x => new { x.Id, x.Title, x.TargetDate, x.Status })
        });
        var entity = new ProjectBaseline
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            Name = dto.Name,
            Notes = dto.Notes,
            SnapshotJson = snapshotJson,
            IsLocked = true,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };
        await _unitOfWork.Repository<ProjectBaseline>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "BaselineCreated", new Dictionary<string, object> { ["BaselineId"] = entity.Id, ["Name"] = entity.Name });
        return MapToDto(entity);
    }

    public async Task<ProjectBaselineComparisonDto> CompareBaselineAsync(Guid baselineId)
    {
        var baseline = await _unitOfWork.Repository<ProjectBaseline>().FirstOrDefaultAsync(x => x.Id == baselineId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project baseline with ID {baselineId} not found");
        var project = await RequireProjectAsync(baseline.ProjectId, ProjectAccessOperation.View);
        using var document = JsonDocument.Parse(baseline.SnapshotJson);
        var root = document.RootElement;
        var baselineProgress = root.TryGetProperty("ProgressPercent", out var progress) ? progress.GetDecimal() : 0m;
        var baselineBudget = root.TryGetProperty("ApprovedBudget", out var budget) && budget.ValueKind != JsonValueKind.Null ? budget.GetDecimal() : 0m;
        var baselineWorkItems = root.TryGetProperty("WorkItems", out var workItemsJson) ? workItemsJson.EnumerateArray().ToList() : new List<JsonElement>();
        var baselineMilestones = root.TryGetProperty("Milestones", out var milestonesJson) ? milestonesJson.EnumerateArray().ToList() : new List<JsonElement>();
        var currentWorkItems = (await _unitOfWork.Repository<ProjectWorkItem>().FindAsync(x => x.ProjectId == project.Id && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var currentMilestones = (await _unitOfWork.Repository<ProjectMilestone>().FindAsync(x => x.ProjectId == project.Id && x.TenantId == _currentUserProvider.TenantId)).ToList();

        var changedWorkItems = baselineWorkItems.Count(item =>
        {
            var id = item.GetProperty("Id").GetGuid();
            var current = currentWorkItems.FirstOrDefault(x => x.Id == id);
            return current == null
                || current.PlannedStartDate != (item.TryGetProperty("PlannedStartDate", out var ps) && ps.ValueKind != JsonValueKind.Null ? ps.GetDateTime() : null)
                || current.PlannedEndDate != (item.TryGetProperty("PlannedEndDate", out var pe) && pe.ValueKind != JsonValueKind.Null ? pe.GetDateTime() : null)
                || current.PercentComplete != (item.TryGetProperty("PercentComplete", out var pc) ? pc.GetDecimal() : 0m);
        });

        var workItemChanges = baselineWorkItems
            .Select(item =>
            {
                var id = item.GetProperty("Id").GetGuid();
                var current = currentWorkItems.FirstOrDefault(x => x.Id == id);
                DateTime? baselineStart = item.TryGetProperty("PlannedStartDate", out var ps) && ps.ValueKind != JsonValueKind.Null ? ps.GetDateTime() : null;
                DateTime? baselineEnd = item.TryGetProperty("PlannedEndDate", out var pe) && pe.ValueKind != JsonValueKind.Null ? pe.GetDateTime() : null;
                var baselinePct = item.TryGetProperty("PercentComplete", out var pc) ? pc.GetDecimal() : 0m;
                if (current == null)
                {
                    return new ProjectBaselineWorkItemChangeDto
                    {
                        WorkItemId = id,
                        WorkItemTitle = item.TryGetProperty("Title", out var title) ? title.GetString() ?? id.ToString() : id.ToString(),
                        BaselinePlannedStartDate = baselineStart,
                        BaselinePlannedEndDate = baselineEnd,
                        BaselinePercentComplete = baselinePct,
                        ScheduleVarianceDays = 0
                    };
                }

                var varianceDays = baselineEnd.HasValue && current.PlannedEndDate.HasValue
                    ? (current.PlannedEndDate.Value.Date - baselineEnd.Value.Date).Days
                    : baselineStart.HasValue && current.PlannedStartDate.HasValue
                        ? (current.PlannedStartDate.Value.Date - baselineStart.Value.Date).Days
                        : 0;

                return new ProjectBaselineWorkItemChangeDto
                {
                    WorkItemId = current.Id,
                    WorkItemTitle = current.Title,
                    BaselinePlannedStartDate = baselineStart,
                    CurrentPlannedStartDate = current.PlannedStartDate,
                    BaselinePlannedEndDate = baselineEnd,
                    CurrentPlannedEndDate = current.PlannedEndDate,
                    BaselinePercentComplete = baselinePct,
                    CurrentPercentComplete = current.PercentComplete,
                    ScheduleVarianceDays = varianceDays
                };
            })
            .Where(x => x.CurrentPlannedStartDate != x.BaselinePlannedStartDate
                || x.CurrentPlannedEndDate != x.BaselinePlannedEndDate
                || x.CurrentPercentComplete != x.BaselinePercentComplete)
            .OrderByDescending(x => Math.Abs(x.ScheduleVarianceDays))
            .ThenBy(x => x.WorkItemTitle)
            .ToList();

        var milestoneChanges = baselineMilestones
            .Select(item =>
            {
                var id = item.GetProperty("Id").GetGuid();
                var current = currentMilestones.FirstOrDefault(x => x.Id == id);
                if (current == null)
                {
                    return null;
                }

                var baselineTarget = item.GetProperty("TargetDate").GetDateTime();
                var baselineStatus = item.GetProperty("Status").GetString() ?? string.Empty;
                return new ProjectBaselineMilestoneChangeDto
                {
                    MilestoneId = id,
                    MilestoneTitle = current.Title,
                    BaselineTargetDate = baselineTarget,
                    CurrentTargetDate = current.TargetDate,
                    BaselineStatus = baselineStatus,
                    CurrentStatus = current.Status,
                    ScheduleVarianceDays = (current.TargetDate.Date - baselineTarget.Date).Days
                };
            })
            .Where(x => x != null
                && (x.CurrentTargetDate != x.BaselineTargetDate
                    || !string.Equals(x.CurrentStatus, x.BaselineStatus, StringComparison.OrdinalIgnoreCase)))
            .Cast<ProjectBaselineMilestoneChangeDto>()
            .OrderByDescending(x => Math.Abs(x.ScheduleVarianceDays))
            .ThenBy(x => x.MilestoneTitle)
            .ToList();

        var changedMilestones = milestoneChanges.Count;

        var baselineFinish = baselineMilestones.Count > 0 ? baselineMilestones.Max(x => x.GetProperty("TargetDate").GetDateTime()) : (DateTime?)null;
        var currentFinish = currentMilestones.Count > 0 ? currentMilestones.Max(x => x.TargetDate) : (DateTime?)null;
        return new ProjectBaselineComparisonDto
        {
            BaselineId = baseline.Id,
            BaselineName = baseline.Name,
            BaselineCreatedOn = baseline.CreatedOn,
            BaselineProgressPercent = baselineProgress,
            CurrentProgressPercent = project.ProgressPercent,
            BaselineFinishDate = baselineFinish,
            CurrentFinishDate = currentFinish,
            BudgetVariance = (project.ApprovedBudget ?? 0m) - baselineBudget,
            ScheduleVarianceDays = baselineFinish.HasValue && currentFinish.HasValue ? (currentFinish.Value.Date - baselineFinish.Value.Date).Days : 0,
            ChangedWorkItemCount = changedWorkItems,
            ChangedMilestoneCount = changedMilestones,
            WorkItemChanges = workItemChanges,
            MilestoneChanges = milestoneChanges
        };
    }

    public async Task<ProjectScheduleAnalysisDto> AnalyzeScheduleAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var workItems = (await _unitOfWork.Repository<ProjectWorkItem>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var dependencies = (await _unitOfWork.Repository<ProjectTaskDependency>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        return BuildScheduleAnalysis(projectId, workItems, dependencies);
    }

    public async Task<ProjectScheduleAnalysisDto> RecalculateScheduleAsync(Guid projectId)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.ManagePlan);
        var workItemRepo = _unitOfWork.Repository<ProjectWorkItem>();
        var dependencyRepo = _unitOfWork.Repository<ProjectTaskDependency>();
        var workItems = (await workItemRepo.FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var dependencies = (await dependencyRepo.FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .Where(x => x.IsEnforced)
            .ToList();

        var sort = TopologicallySortWorkItems(workItems, dependencies);
        if (sort.HasCircularDependencies)
        {
            return BuildScheduleAnalysis(projectId, workItems, dependencies, hasCircularDependencies: true);
        }

        var workItemLookup = workItems.ToDictionary(x => x.Id);
        var changed = 0;
        foreach (var successorId in sort.OrderedIds)
        {
            if (!workItemLookup.TryGetValue(successorId, out var successor))
            {
                continue;
            }

            foreach (var dependency in dependencies.Where(x => x.SuccessorWorkItemId == successorId))
            {
                if (!workItemLookup.TryGetValue(dependency.PredecessorWorkItemId, out var predecessor))
                {
                    continue;
                }

                if (!ApplyDependencyDates(predecessor, successor, dependency))
                {
                    continue;
                }

                successor.UpdatedBy = _currentUserProvider.Username;
                successor.LastModifiedById = _currentUserProvider.UserId;
                await workItemRepo.UpdateAsync(successor);
                changed++;
            }
        }

        if (changed > 0)
        {
            await _unitOfWork.SaveChangesAsync();
            await UpdateProjectProgressAsync(project);
            await PublishActivityAsync(project, "ScheduleRecalculated", new Dictionary<string, object> { ["RecalculatedItemCount"] = changed });
        }

        return BuildScheduleAnalysis(projectId, workItems, dependencies, recalculatedItemCount: changed);
    }

    public async Task<IEnumerable<ProjectTimesheetEntryDto>> GetTimesheetEntriesAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return await MapTimesheetEntriesAsync((await _unitOfWork.Repository<ProjectTimesheetEntry>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderByDescending(x => x.EntryDate)
            .ToList());
    }

    public async Task<IEnumerable<ProjectTimesheetEntryDto>> GetMyTimesheetEntriesAsync(string? status = null)
    {
        var entries = (await _unitOfWork.Repository<ProjectTimesheetEntry>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId &&
                x.UserId == _currentUserProvider.UserId &&
                (string.IsNullOrWhiteSpace(status) || x.Status == status)))
            .OrderByDescending(x => x.EntryDate)
            .ToList();
        return await MapTimesheetEntriesAsync(entries);
    }

    public async Task<ProjectApprovalQueueSummaryDto> GetTimesheetApprovalSummaryAsync(Guid? projectId = null)
    {
        if (projectId.HasValue)
        {
            await RequireProjectAsync(projectId.Value, ProjectAccessOperation.ManageFinancials);
        }

        var entries = (await _unitOfWork.Repository<ProjectTimesheetEntry>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId &&
                (!projectId.HasValue || x.ProjectId == projectId.Value)))
            .ToList();
        return BuildApprovalSummary(entries.Select(x => x.Status), entries.Sum(x => x.Hours), entries.Sum(x => x.CostAmount));
    }

    public async Task<IEnumerable<ProjectTimesheetApprovalQueueItemDto>> GetTimesheetApprovalQueueAsync(Guid? projectId = null, string? status = null, Guid? userId = null, int take = 200)
    {
        if (projectId.HasValue)
        {
            await RequireProjectAsync(projectId.Value, ProjectAccessOperation.ManageFinancials);
        }

        var accessibleProjects = (await GetAccessibleProjectsAsync(take: Math.Max(take, 200))).ToList();
        var accessibleProjectIds = accessibleProjects.Select(x => x.Id).ToHashSet();
        var entries = (await _unitOfWork.Repository<ProjectTimesheetEntry>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId &&
                (!projectId.HasValue || x.ProjectId == projectId.Value) &&
                (!userId.HasValue || x.UserId == userId.Value) &&
                (string.IsNullOrWhiteSpace(status) || x.Status == status)))
            .Where(x => accessibleProjectIds.Contains(x.ProjectId))
            .OrderByDescending(x => GetApprovalQueuePriority(x.Status))
            .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Take(take)
            .ToList();

        if (!entries.Any())
        {
            return Array.Empty<ProjectTimesheetApprovalQueueItemDto>();
        }

        var workItemIds = entries.Where(x => x.WorkItemId.HasValue).Select(x => x.WorkItemId!.Value).Distinct().ToList();
        var workItemTitles = workItemIds.Any()
            ? (await _unitOfWork.Repository<ProjectWorkItem>().FindAsync(x => workItemIds.Contains(x.Id))).ToDictionary(x => x.Id, x => x.Title)
            : new Dictionary<Guid, string>();
        var projectLookup = accessibleProjects.ToDictionary(x => x.Id);
        var today = DateTime.UtcNow.Date;

        return entries.Select(x =>
        {
            var project = projectLookup[x.ProjectId];
            var queueDate = (x.UpdatedAt ?? x.CreatedAt).Date;
            return new ProjectTimesheetApprovalQueueItemDto
            {
                EntryId = x.Id,
                ProjectId = x.ProjectId,
                ProjectCode = project.ProjectCode,
                ProjectTitle = project.Title,
                UserId = x.UserId,
                WorkItemId = x.WorkItemId,
                WorkItemTitle = x.WorkItemId.HasValue && workItemTitles.TryGetValue(x.WorkItemId.Value, out var workItemTitle) ? workItemTitle : null,
                EntryDate = x.EntryDate,
                Hours = x.Hours,
                HourlyRate = x.HourlyRate,
                CostAmount = x.CostAmount,
                IsBillable = x.IsBillable,
                WorkType = x.WorkType,
                Status = x.Status,
                QueueStage = ResolveApprovalQueueStage(x.Status),
                DaysOpen = Math.Max((today - queueDate).Days, 0),
                Notes = x.Notes
            };
        }).ToList();
    }

    public async Task<ProjectTimesheetEntryDto> AddTimesheetEntryAsync(Guid projectId, CreateProjectTimesheetEntryDto dto)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.ManageExecution);
        await ValidateTimesheetEntryAsync(project, dto);
        var entity = new ProjectTimesheetEntry
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            WorkItemId = dto.WorkItemId,
            UserId = dto.UserId,
            EntryDate = dto.EntryDate == default ? DateTime.UtcNow.Date : dto.EntryDate.Date,
            Hours = dto.Hours,
            IsBillable = dto.IsBillable,
            HourlyRate = dto.HourlyRate,
            CostAmount = decimal.Round(dto.Hours * dto.HourlyRate, 2),
            WorkType = dto.WorkType,
            Notes = dto.Notes,
            Status = NormalizeEntryStatus(dto.Status),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };
        await _unitOfWork.Repository<ProjectTimesheetEntry>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, entity.Status == "Draft" ? "TimesheetDraftSaved" : "TimesheetSubmitted", new Dictionary<string, object> { ["TimesheetEntryId"] = entity.Id, ["Hours"] = entity.Hours });
        return await MapTimesheetEntryAsync(entity);
    }

    public async Task<ProjectTimesheetEntryDto> UpdateTimesheetEntryAsync(Guid entryId, CreateProjectTimesheetEntryDto dto)
    {
        var repo = _unitOfWork.Repository<ProjectTimesheetEntry>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == entryId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project timesheet entry with ID {entryId} not found");
        var project = await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);
        EnsureEntryEditable(entity.Status, "timesheet");
        await ValidateTimesheetEntryAsync(project, dto, entryId);

        entity.WorkItemId = dto.WorkItemId;
        entity.UserId = dto.UserId;
        entity.EntryDate = dto.EntryDate == default ? entity.EntryDate.Date : dto.EntryDate.Date;
        entity.Hours = dto.Hours;
        entity.IsBillable = dto.IsBillable;
        entity.HourlyRate = dto.HourlyRate;
        entity.CostAmount = decimal.Round(dto.Hours * dto.HourlyRate, 2);
        entity.WorkType = dto.WorkType;
        entity.Notes = dto.Notes;
        entity.Status = NormalizeEntryStatus(dto.Status);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, entity.Status == "Draft" ? "TimesheetDraftUpdated" : "TimesheetUpdated", new Dictionary<string, object> { ["TimesheetEntryId"] = entity.Id, ["Hours"] = entity.Hours });
        return await MapTimesheetEntryAsync(entity);
    }

    public async Task<ProjectTimesheetEntryDto> SubmitTimesheetEntryAsync(Guid entryId)
    {
        var repo = _unitOfWork.Repository<ProjectTimesheetEntry>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == entryId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project timesheet entry with ID {entryId} not found");
        var project = await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);
        if (!string.Equals(entity.Status, "Draft", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(entity.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only draft or rejected timesheets can be submitted.");
        }
        await EnsureOwnedByCurrentUserOrAuthorizedAsync(project, entity.UserId);

        entity.Status = "Submitted";
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "TimesheetSubmitted", new Dictionary<string, object> { ["TimesheetEntryId"] = entity.Id, ["Hours"] = entity.Hours });
        return await MapTimesheetEntryAsync(entity);
    }

    public async Task<ProjectTimesheetEntryDto> ApproveTimesheetEntryAsync(Guid entryId, string? comments = null)
    {
        var repo = _unitOfWork.Repository<ProjectTimesheetEntry>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == entryId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project timesheet entry with ID {entryId} not found");
        var project = await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        if (!string.Equals(entity.Status, "Submitted", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only submitted timesheets can be approved.");
        }
        entity.Status = "Approved";
        entity.Notes = AppendDecisionNote(entity.Notes, "Approved", comments);
        entity.ApprovedAt = DateTime.UtcNow;
        entity.ApprovedById = _currentUserProvider.UserId;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await ApplyApprovedTimesheetAsync(project, entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "TimesheetApproved", new Dictionary<string, object> { ["TimesheetEntryId"] = entity.Id, ["CostAmount"] = entity.CostAmount });
        return await MapTimesheetEntryAsync(entity);
    }

    public async Task<ProjectTimesheetEntryDto> RejectTimesheetEntryAsync(Guid entryId, string? comments = null)
    {
        var repo = _unitOfWork.Repository<ProjectTimesheetEntry>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == entryId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project timesheet entry with ID {entryId} not found");
        var project = await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        if (!string.Equals(entity.Status, "Submitted", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only submitted timesheets can be rejected.");
        }

        entity.Status = "Rejected";
        entity.Notes = AppendDecisionNote(entity.Notes, "Rejected", comments);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "TimesheetRejected", new Dictionary<string, object> { ["TimesheetEntryId"] = entity.Id });
        return await MapTimesheetEntryAsync(entity);
    }

    public async Task DeleteTimesheetEntryAsync(Guid entryId)
    {
        var repo = _unitOfWork.Repository<ProjectTimesheetEntry>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == entryId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project timesheet entry with ID {entryId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);
        EnsureEntryDeletable(entity.Status, "timesheet");
        await EnsureOwnedByCurrentUserOrAuthorizedAsync(await _projectRepository.GetByIdAsync(entity.ProjectId) ?? throw new InvalidOperationException($"Project with ID {entity.ProjectId} not found"), entity.UserId);
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectExpenseDto>> GetExpensesAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return await MapExpensesAsync((await _unitOfWork.Repository<ProjectExpense>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderByDescending(x => x.ExpenseDate)
            .ToList());
    }

    public async Task<IEnumerable<ProjectExpenseDto>> GetMyExpensesAsync(string? status = null)
    {
        var entries = (await _unitOfWork.Repository<ProjectExpense>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId &&
                x.UserId == _currentUserProvider.UserId &&
                (string.IsNullOrWhiteSpace(status) || x.Status == status)))
            .OrderByDescending(x => x.ExpenseDate)
            .ToList();
        return await MapExpensesAsync(entries);
    }

    public async Task<ProjectApprovalQueueSummaryDto> GetExpenseApprovalSummaryAsync(Guid? projectId = null)
    {
        if (projectId.HasValue)
        {
            await RequireProjectAsync(projectId.Value, ProjectAccessOperation.ManageFinancials);
        }

        var entries = (await _unitOfWork.Repository<ProjectExpense>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId &&
                (!projectId.HasValue || x.ProjectId == projectId.Value)))
            .ToList();
        return BuildApprovalSummary(entries.Select(x => x.Status), 0m, entries.Sum(x => x.Amount + x.TaxAmount));
    }

    public async Task<IEnumerable<ProjectExpenseApprovalQueueItemDto>> GetExpenseApprovalQueueAsync(Guid? projectId = null, string? status = null, Guid? userId = null, int take = 200)
    {
        if (projectId.HasValue)
        {
            await RequireProjectAsync(projectId.Value, ProjectAccessOperation.ManageFinancials);
        }

        var accessibleProjects = (await GetAccessibleProjectsAsync(take: Math.Max(take, 200))).ToList();
        var accessibleProjectIds = accessibleProjects.Select(x => x.Id).ToHashSet();
        var entries = (await _unitOfWork.Repository<ProjectExpense>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId &&
                (!projectId.HasValue || x.ProjectId == projectId.Value) &&
                (!userId.HasValue || x.UserId == userId.Value) &&
                (string.IsNullOrWhiteSpace(status) || x.Status == status)))
            .Where(x => accessibleProjectIds.Contains(x.ProjectId))
            .OrderByDescending(x => GetApprovalQueuePriority(x.Status))
            .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Take(take)
            .ToList();

        if (!entries.Any())
        {
            return Array.Empty<ProjectExpenseApprovalQueueItemDto>();
        }

        var workItemIds = entries.Where(x => x.WorkItemId.HasValue).Select(x => x.WorkItemId!.Value).Distinct().ToList();
        var workItemTitles = workItemIds.Any()
            ? (await _unitOfWork.Repository<ProjectWorkItem>().FindAsync(x => workItemIds.Contains(x.Id))).ToDictionary(x => x.Id, x => x.Title)
            : new Dictionary<Guid, string>();
        var projectLookup = accessibleProjects.ToDictionary(x => x.Id);
        var today = DateTime.UtcNow.Date;

        return entries.Select(x =>
        {
            var project = projectLookup[x.ProjectId];
            var queueDate = (x.UpdatedAt ?? x.CreatedAt).Date;
            return new ProjectExpenseApprovalQueueItemDto
            {
                ExpenseId = x.Id,
                ProjectId = x.ProjectId,
                ProjectCode = project.ProjectCode,
                ProjectTitle = project.Title,
                UserId = x.UserId,
                WorkItemId = x.WorkItemId,
                WorkItemTitle = x.WorkItemId.HasValue && workItemTitles.TryGetValue(x.WorkItemId.Value, out var workItemTitle) ? workItemTitle : null,
                ExpenseDate = x.ExpenseDate,
                Category = x.Category,
                Currency = x.Currency,
                Amount = x.Amount,
                TaxAmount = x.TaxAmount,
                TotalAmount = x.Amount + x.TaxAmount,
                IsBillable = x.IsBillable,
                Status = x.Status,
                QueueStage = ResolveApprovalQueueStage(x.Status),
                DaysOpen = Math.Max((today - queueDate).Days, 0),
                Notes = x.Notes
            };
        }).ToList();
    }

    public async Task<ProjectExpenseDto> AddExpenseAsync(Guid projectId, CreateProjectExpenseDto dto)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.ManageExecution);
        await ValidateExpenseAsync(project, dto);
        var currencyCode = await ResolveProjectCurrencyAsync(dto.Currency);
        var entity = new ProjectExpense
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            WorkItemId = dto.WorkItemId,
            UserId = dto.UserId,
            ExpenseDate = dto.ExpenseDate == default ? DateTime.UtcNow.Date : dto.ExpenseDate.Date,
            Category = dto.Category,
            Currency = currencyCode,
            Amount = dto.Amount,
            TaxAmount = dto.TaxAmount,
            IsBillable = dto.IsBillable,
            ReceiptDocumentId = dto.ReceiptDocumentId,
            Notes = dto.Notes,
            Status = NormalizeEntryStatus(dto.Status),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };
        await _unitOfWork.Repository<ProjectExpense>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, entity.Status == "Draft" ? "ExpenseDraftSaved" : "ExpenseSubmitted", new Dictionary<string, object> { ["ExpenseId"] = entity.Id, ["Amount"] = entity.Amount });
        return await MapExpenseAsync(entity);
    }

    public async Task<ProjectExpenseDto> UpdateExpenseAsync(Guid expenseId, CreateProjectExpenseDto dto)
    {
        var repo = _unitOfWork.Repository<ProjectExpense>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == expenseId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project expense with ID {expenseId} not found");
        var project = await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);
        EnsureEntryEditable(entity.Status, "expense");
        await ValidateExpenseAsync(project, dto);
        var currencyCode = await ResolveProjectCurrencyAsync(dto.Currency);

        entity.WorkItemId = dto.WorkItemId;
        entity.UserId = dto.UserId;
        entity.ExpenseDate = dto.ExpenseDate == default ? entity.ExpenseDate.Date : dto.ExpenseDate.Date;
        entity.Category = dto.Category;
        entity.Currency = currencyCode;
        entity.Amount = dto.Amount;
        entity.TaxAmount = dto.TaxAmount;
        entity.IsBillable = dto.IsBillable;
        entity.ReceiptDocumentId = dto.ReceiptDocumentId;
        entity.Notes = dto.Notes;
        entity.Status = NormalizeEntryStatus(dto.Status);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, entity.Status == "Draft" ? "ExpenseDraftUpdated" : "ExpenseUpdated", new Dictionary<string, object> { ["ExpenseId"] = entity.Id, ["Amount"] = entity.Amount + entity.TaxAmount });
        return await MapExpenseAsync(entity);
    }

    public async Task<ProjectExpenseDto> SubmitExpenseAsync(Guid expenseId)
    {
        var repo = _unitOfWork.Repository<ProjectExpense>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == expenseId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project expense with ID {expenseId} not found");
        var project = await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);
        if (!string.Equals(entity.Status, "Draft", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(entity.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only draft or rejected expenses can be submitted.");
        }
        await EnsureOwnedByCurrentUserOrAuthorizedAsync(project, entity.UserId);

        entity.Status = "Submitted";
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "ExpenseSubmitted", new Dictionary<string, object> { ["ExpenseId"] = entity.Id, ["Amount"] = entity.Amount + entity.TaxAmount });
        return await MapExpenseAsync(entity);
    }

    public async Task<ProjectExpenseDto> ApproveExpenseAsync(Guid expenseId, string? comments = null)
    {
        var repo = _unitOfWork.Repository<ProjectExpense>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == expenseId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project expense with ID {expenseId} not found");
        var project = await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        if (!string.Equals(entity.Status, "Submitted", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only submitted expenses can be approved.");
        }
        entity.Status = "Approved";
        entity.Notes = AppendDecisionNote(entity.Notes, "Approved", comments);
        entity.ApprovedAt = DateTime.UtcNow;
        entity.ApprovedById = _currentUserProvider.UserId;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        project.ActualCost = (project.ActualCost ?? 0m) + entity.Amount + entity.TaxAmount;
        await _projectRepository.UpdateAsync(project);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "ExpenseApproved", new Dictionary<string, object> { ["ExpenseId"] = entity.Id, ["Amount"] = entity.Amount + entity.TaxAmount });
        return await MapExpenseAsync(entity);
    }

    public async Task<ProjectExpenseDto> RejectExpenseAsync(Guid expenseId, string? comments = null)
    {
        var repo = _unitOfWork.Repository<ProjectExpense>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == expenseId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project expense with ID {expenseId} not found");
        var project = await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageFinancials);
        if (!string.Equals(entity.Status, "Submitted", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only submitted expenses can be rejected.");
        }

        entity.Status = "Rejected";
        entity.Notes = AppendDecisionNote(entity.Notes, "Rejected", comments);
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "ExpenseRejected", new Dictionary<string, object> { ["ExpenseId"] = entity.Id });
        return await MapExpenseAsync(entity);
    }

    public async Task DeleteExpenseAsync(Guid expenseId)
    {
        var repo = _unitOfWork.Repository<ProjectExpense>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == expenseId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project expense with ID {expenseId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);
        EnsureEntryDeletable(entity.Status, "expense");
        await EnsureOwnedByCurrentUserOrAuthorizedAsync(await _projectRepository.GetByIdAsync(entity.ProjectId) ?? throw new InvalidOperationException($"Project with ID {entity.ProjectId} not found"), entity.UserId);
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectRevenueRecognitionDto>> GetRevenueRecognitionsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return (await _unitOfWork.Repository<ProjectRevenueRecognition>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderByDescending(x => x.RecognitionPeriod)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<IEnumerable<ProjectRevenueRecognitionDto>> GenerateRevenueRecognitionAsync(Guid projectId)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.ManageFinancials);
        var invoiceRequests = (await _unitOfWork.Repository<ProjectInvoiceRequest>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var invoiceReferences = invoiceRequests.Select(x => x.RequestNumber).ToList();
        var invoices = (await _unitOfWork.Repository<Invoice>().FindAsync(x => !x.IsDeleted && (x.Reference == project.ProjectCode || (!string.IsNullOrWhiteSpace(x.Reference) && invoiceReferences.Contains(x.Reference))))).ToList();
        var payments = (await _unitOfWork.Repository<Payment>().FindAsync(x => !x.IsDeleted && invoices.Select(i => i.Id).Contains(x.InvoiceId))).ToList();
        var cost = project.ActualCost ?? 0m;
        var recognitionPeriod = DateTime.UtcNow.ToString("yyyy-MM");
        var repo = _unitOfWork.Repository<ProjectRevenueRecognition>();
        var entity = await repo.FirstOrDefaultAsync(x =>
            x.ProjectId == projectId
            && x.TenantId == _currentUserProvider.TenantId
            && x.RecognitionPeriod == recognitionPeriod);
        var wasExisting = entity != null;
        if (entity == null)
        {
            entity = new ProjectRevenueRecognition
            {
                TenantId = _currentUserProvider.TenantId,
                ProjectId = projectId,
                RecognitionPeriod = recognitionPeriod,
                CreatedBy = _currentUserProvider.Username,
                CreatedById = _currentUserProvider.UserId
            };
            await repo.AddAsync(entity);
        }

        var latestInvoiceRequest = invoiceRequests
            .OrderByDescending(x => x.SubmittedAt ?? x.RequestedAt)
            .ThenByDescending(x => x.RequestedAt)
            .FirstOrDefault();
        entity.InvoiceRequestId = latestInvoiceRequest?.Id;
        entity.RecognizedRevenue = invoices.Sum(x => x.TotalAmount);
        entity.RecognizedCost = cost;
        entity.GrossMargin = entity.RecognizedRevenue - cost;
        entity.CashCollected = payments.Where(x => x.Status == PaymentStatus.Completed).Sum(x => x.Amount);
        entity.Status = "Recognized";
        entity.Notes = $"Generated from {invoiceRequests.Count} invoice requests and {invoices.Count} finance invoices.";
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        if (wasExisting)
        {
            await repo.UpdateAsync(entity);
        }
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, wasExisting ? "RevenueRecognitionRefreshed" : "RevenueRecognitionGenerated", new Dictionary<string, object> { ["RevenueRecognitionId"] = entity.Id, ["RecognizedRevenue"] = entity.RecognizedRevenue });
        return await GetRevenueRecognitionsAsync(projectId);
    }

    public async Task<IEnumerable<ProjectAssetLinkDto>> GetAssetLinksAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var links = (await _unitOfWork.Repository<ProjectAssetLink>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var maintenanceAssetIds = links.Where(l => l.MaintenanceAssetId.HasValue).Select(l => l.MaintenanceAssetId!.Value).Distinct().ToList();
        var companyAssetIds = links.Where(l => l.CompanyAssetId.HasValue).Select(l => l.CompanyAssetId!.Value).Distinct().ToList();
        var jobCardIds = links.Where(l => l.JobCardId.HasValue).Select(l => l.JobCardId!.Value).Distinct().ToList();

        var maintenanceAssets = maintenanceAssetIds.Count == 0
            ? new Dictionary<Guid, MaintenanceAsset>()
            : (await _unitOfWork.Repository<MaintenanceAsset>().FindAsync(x => maintenanceAssetIds.Contains(x.Id))).ToDictionary(x => x.Id);
        var companyAssets = companyAssetIds.Count == 0
            ? new Dictionary<Guid, CompanyAsset>()
            : (await _unitOfWork.Repository<CompanyAsset>().FindAsync(x => companyAssetIds.Contains(x.Id))).ToDictionary(x => x.Id);
        var jobCards = jobCardIds.Count == 0
            ? new Dictionary<Guid, JobCard>()
            : (await _unitOfWork.Repository<JobCard>().FindAsync(x => jobCardIds.Contains(x.Id))).ToDictionary(x => x.Id);

        return links.Select(x => MapToDto(
            x,
            x.MaintenanceAssetId.HasValue && maintenanceAssets.TryGetValue(x.MaintenanceAssetId.Value, out var maintenanceAsset) ? maintenanceAsset.Name : x.CompanyAssetId.HasValue && companyAssets.TryGetValue(x.CompanyAssetId.Value, out var companyAsset) ? companyAsset.AssetName : null,
            x.JobCardId.HasValue && jobCards.TryGetValue(x.JobCardId.Value, out var jobCard) ? jobCard.JobCardNumber : null))
            .ToList();
    }

    public async Task<ProjectAssetLinkDto> AddAssetLinkAsync(Guid projectId, CreateProjectAssetLinkDto dto)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.ManageExecution);
        var entity = new ProjectAssetLink
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            MaintenanceAssetId = dto.MaintenanceAssetId,
            CompanyAssetId = dto.CompanyAssetId,
            JobCardId = dto.JobCardId,
            LinkType = dto.LinkType,
            Status = dto.Status,
            Notes = dto.Notes,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };
        await _unitOfWork.Repository<ProjectAssetLink>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "AssetLinked", new Dictionary<string, object> { ["ProjectAssetLinkId"] = entity.Id, ["LinkType"] = entity.LinkType });
        return (await GetAssetLinksAsync(projectId)).First(x => x.Id == entity.Id);
    }

    public async Task<IEnumerable<ProjectExternalAccessPolicyDto>> GetExternalAccessPoliciesAsync(Guid projectId)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var policies = (await _unitOfWork.Repository<ProjectExternalAccessPolicy>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderBy(x => x.ArtifactType)
            .ThenBy(x => x.BusinessPartnerId)
            .ToList();

        var results = new List<ProjectExternalAccessPolicyDto>(policies.Count);
        foreach (var policy in policies)
        {
            results.Add(await MapToDtoAsync(project, policy));
        }

        return results;
    }

    public async Task<ProjectExternalAccessPolicyDto> UpsertExternalAccessPolicyAsync(Guid projectId, CreateProjectExternalAccessPolicyDto dto)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.ManageExternalAccess);
        await ValidateExternalAccessPolicyAsync(project, dto);
        var repo = _unitOfWork.Repository<ProjectExternalAccessPolicy>();
        var entity = await repo.FirstOrDefaultAsync(x => x.ProjectId == projectId
            && x.BusinessPartnerId == dto.BusinessPartnerId
            && x.ArtifactType == NormalizeExternalArtifactType(dto.ArtifactType)
            && x.ArtifactId == dto.ArtifactId
            && x.TenantId == _currentUserProvider.TenantId);

        if (entity == null)
        {
            entity = new ProjectExternalAccessPolicy
            {
                TenantId = _currentUserProvider.TenantId,
                ProjectId = projectId,
                BusinessPartnerId = dto.BusinessPartnerId,
                ArtifactType = NormalizeExternalArtifactType(dto.ArtifactType),
                ArtifactId = dto.ArtifactId,
                AccessLevel = dto.AccessLevel,
                CanComment = dto.CanComment,
                CanUpload = dto.CanUpload,
                CanApprove = dto.CanApprove,
                Notes = dto.Notes,
                CreatedBy = _currentUserProvider.Username,
                CreatedById = _currentUserProvider.UserId
            };
            await repo.AddAsync(entity);
        }
        else
        {
            entity.AccessLevel = dto.AccessLevel;
            entity.CanComment = dto.CanComment;
            entity.CanUpload = dto.CanUpload;
            entity.CanApprove = dto.CanApprove;
            entity.Notes = dto.Notes;
            entity.UpdatedBy = _currentUserProvider.Username;
            entity.LastModifiedById = _currentUserProvider.UserId;
            await repo.UpdateAsync(entity);
        }

        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "ExternalAccessPolicyUpdated", new Dictionary<string, object> { ["BusinessPartnerId"] = dto.BusinessPartnerId, ["ArtifactType"] = dto.ArtifactType });
        return await MapToDtoAsync(project, entity);
    }

    public async Task DeleteExternalAccessPolicyAsync(Guid policyId)
    {
        var repo = _unitOfWork.Repository<ProjectExternalAccessPolicy>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == policyId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project external access policy with ID {policyId} not found");
        var project = await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageExternalAccess);
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "ExternalAccessPolicyDeleted", new Dictionary<string, object>
        {
            ["BusinessPartnerId"] = entity.BusinessPartnerId,
            ["ArtifactType"] = entity.ArtifactType,
            ["ArtifactId"] = entity.ArtifactId ?? Guid.Empty
        });
    }

    public async Task<IEnumerable<ProjectDecisionDto>> GetDecisionsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return (await _unitOfWork.Repository<ProjectDecision>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderByDescending(x => x.DecisionDate)
            .ThenByDescending(x => x.CreatedAt)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<ProjectDecisionDto> AddDecisionAsync(Guid projectId, CreateProjectDecisionDto dto)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.ManageGovernance);
        var entity = new ProjectDecision
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            Title = dto.Title.Trim(),
            DecisionDate = dto.DecisionDate?.Date ?? DateTime.UtcNow.Date,
            ApproverId = dto.ApproverId,
            Rationale = dto.Rationale,
            AlternativesConsidered = dto.AlternativesConsidered,
            ImpactSummary = dto.ImpactSummary,
            Status = dto.Status,
            ApprovedAt = string.Equals(dto.Status, "Approved", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : null,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };
        await _unitOfWork.Repository<ProjectDecision>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "DecisionLogged", new Dictionary<string, object> { ["DecisionId"] = entity.Id, ["Title"] = entity.Title });
        return MapToDto(entity);
    }

    public async Task<ProjectDecisionDto> UpdateDecisionAsync(Guid decisionId, CreateProjectDecisionDto dto)
    {
        var repo = _unitOfWork.Repository<ProjectDecision>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == decisionId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project decision with ID {decisionId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);
        if (string.Equals(entity.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Approved decisions are immutable.");
        }

        entity.Title = dto.Title.Trim();
        entity.DecisionDate = dto.DecisionDate?.Date ?? entity.DecisionDate;
        entity.ApproverId = dto.ApproverId;
        entity.Rationale = dto.Rationale;
        entity.AlternativesConsidered = dto.AlternativesConsidered;
        entity.ImpactSummary = dto.ImpactSummary;
        entity.Status = dto.Status;
        entity.ApprovedAt = string.Equals(dto.Status, "Approved", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : null;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task DeleteDecisionAsync(Guid decisionId)
    {
        var repo = _unitOfWork.Repository<ProjectDecision>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == decisionId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project decision with ID {decisionId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);
        if (string.Equals(entity.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Approved decisions are immutable.");
        }
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectMeetingMinuteDto>> GetMeetingsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return (await _unitOfWork.Repository<ProjectMeetingMinute>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderByDescending(x => x.MeetingDate)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<ProjectMeetingMinuteDto> AddMeetingAsync(Guid projectId, CreateProjectMeetingMinuteDto dto)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.ManageGovernance);
        var entity = new ProjectMeetingMinute
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            Title = dto.Title.Trim(),
            MeetingDate = dto.MeetingDate ?? DateTime.UtcNow,
            FacilitatorId = dto.FacilitatorId,
            MeetingType = dto.MeetingType,
            Minutes = dto.Minutes,
            AttendeesJson = dto.AttendeesJson,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };
        await _unitOfWork.Repository<ProjectMeetingMinute>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "MeetingLogged", new Dictionary<string, object> { ["MeetingId"] = entity.Id, ["Title"] = entity.Title });
        return MapToDto(entity);
    }

    public async Task<ProjectMeetingMinuteDto> UpdateMeetingAsync(Guid meetingId, CreateProjectMeetingMinuteDto dto)
    {
        var repo = _unitOfWork.Repository<ProjectMeetingMinute>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == meetingId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project meeting with ID {meetingId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);
        entity.Title = dto.Title.Trim();
        entity.MeetingDate = dto.MeetingDate ?? entity.MeetingDate;
        entity.FacilitatorId = dto.FacilitatorId;
        entity.MeetingType = dto.MeetingType;
        entity.Minutes = dto.Minutes;
        entity.AttendeesJson = dto.AttendeesJson;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task DeleteMeetingAsync(Guid meetingId)
    {
        var entity = await _unitOfWork.Repository<ProjectMeetingMinute>().FirstOrDefaultAsync(x => x.Id == meetingId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project meeting with ID {meetingId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);
        await _unitOfWork.Repository<ProjectMeetingMinute>().DeleteAsync(meetingId);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectActionItemDto>> GetActionItemsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var actionItems = (await _unitOfWork.Repository<ProjectActionItem>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var meetingIds = actionItems.Where(x => x.MeetingMinuteId.HasValue).Select(x => x.MeetingMinuteId!.Value).Distinct().ToList();
        var workItemIds = actionItems.Where(x => x.WorkItemId.HasValue).Select(x => x.WorkItemId!.Value).Distinct().ToList();
        var meetings = (await _unitOfWork.Repository<ProjectMeetingMinute>().FindAsync(x => meetingIds.Contains(x.Id) && x.TenantId == _currentUserProvider.TenantId)).ToDictionary(x => x.Id);
        var workItems = (await _unitOfWork.Repository<ProjectWorkItem>().FindAsync(x => workItemIds.Contains(x.Id) && x.TenantId == _currentUserProvider.TenantId)).ToDictionary(x => x.Id);
        return actionItems.OrderBy(x => x.Status).ThenBy(x => x.DueDate).Select(x => MapToDto(x, x.MeetingMinuteId.HasValue ? meetings.GetValueOrDefault(x.MeetingMinuteId.Value) : null, x.WorkItemId.HasValue ? workItems.GetValueOrDefault(x.WorkItemId.Value) : null)).ToList();
    }

    public async Task<ProjectActionItemDto> AddActionItemAsync(Guid projectId, CreateProjectActionItemDto dto)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.ManageGovernance);
        await ValidateActionItemReferencesAsync(projectId, dto.MeetingMinuteId, dto.WorkItemId);
        var entity = new ProjectActionItem
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            MeetingMinuteId = dto.MeetingMinuteId,
            WorkItemId = dto.WorkItemId,
            Title = dto.Title.Trim(),
            Description = dto.Description,
            OwnerId = dto.OwnerId,
            DueDate = dto.DueDate,
            Status = dto.Status,
            Priority = dto.Priority,
            CompletedAt = string.Equals(dto.Status, "Completed", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : null,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };
        await _unitOfWork.Repository<ProjectActionItem>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "ActionItemCreated", new Dictionary<string, object> { ["ActionItemId"] = entity.Id, ["Title"] = entity.Title });
        return (await GetActionItemsAsync(projectId)).First(x => x.Id == entity.Id);
    }

    public async Task<ProjectActionItemDto> UpdateActionItemAsync(Guid actionItemId, CreateProjectActionItemDto dto)
    {
        var repo = _unitOfWork.Repository<ProjectActionItem>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == actionItemId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project action item with ID {actionItemId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);
        await ValidateActionItemReferencesAsync(entity.ProjectId, dto.MeetingMinuteId, dto.WorkItemId);
        entity.MeetingMinuteId = dto.MeetingMinuteId;
        entity.WorkItemId = dto.WorkItemId;
        entity.Title = dto.Title.Trim();
        entity.Description = dto.Description;
        entity.OwnerId = dto.OwnerId;
        entity.DueDate = dto.DueDate;
        entity.Status = dto.Status;
        entity.Priority = dto.Priority;
        entity.CompletedAt = string.Equals(dto.Status, "Completed", StringComparison.OrdinalIgnoreCase) ? entity.CompletedAt ?? DateTime.UtcNow : null;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return (await GetActionItemsAsync(entity.ProjectId)).First(x => x.Id == entity.Id);
    }

    public async Task DeleteActionItemAsync(Guid actionItemId)
    {
        var entity = await _unitOfWork.Repository<ProjectActionItem>().FirstOrDefaultAsync(x => x.Id == actionItemId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project action item with ID {actionItemId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);
        await _unitOfWork.Repository<ProjectActionItem>().DeleteAsync(actionItemId);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectLessonLearnedDto>> GetLessonsLearnedAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return (await _unitOfWork.Repository<ProjectLessonLearned>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderBy(x => x.Category)
            .ThenByDescending(x => x.CreatedAt)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<ProjectLessonLearnedDto> AddLessonLearnedAsync(Guid projectId, CreateProjectLessonLearnedDto dto)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.ManageGovernance);
        var entity = new ProjectLessonLearned
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            Title = dto.Title.Trim(),
            Category = dto.Category,
            Description = dto.Description,
            Recommendation = dto.Recommendation,
            AppliedPhase = dto.AppliedPhase,
            Visibility = dto.Visibility,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };
        await _unitOfWork.Repository<ProjectLessonLearned>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "LessonLearnedAdded", new Dictionary<string, object> { ["LessonId"] = entity.Id, ["Title"] = entity.Title });
        return MapToDto(entity);
    }

    public async Task<ProjectLessonLearnedDto> UpdateLessonLearnedAsync(Guid lessonId, CreateProjectLessonLearnedDto dto)
    {
        var repo = _unitOfWork.Repository<ProjectLessonLearned>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == lessonId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project lesson learned with ID {lessonId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);
        entity.Title = dto.Title.Trim();
        entity.Category = dto.Category;
        entity.Description = dto.Description;
        entity.Recommendation = dto.Recommendation;
        entity.AppliedPhase = dto.AppliedPhase;
        entity.Visibility = dto.Visibility;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task DeleteLessonLearnedAsync(Guid lessonId)
    {
        var entity = await _unitOfWork.Repository<ProjectLessonLearned>().FirstOrDefaultAsync(x => x.Id == lessonId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project lesson learned with ID {lessonId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageGovernance);
        await _unitOfWork.Repository<ProjectLessonLearned>().DeleteAsync(lessonId);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<ProjectClosureDto?> GetClosureAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var closure = await _unitOfWork.Repository<ProjectClosure>().FirstOrDefaultAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId);
        return closure == null ? null : MapToDto(closure);
    }

    public async Task<ProjectClosureDto> UpsertClosureAsync(Guid projectId, UpsertProjectClosureDto dto)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.ManageGovernance);
        var repo = _unitOfWork.Repository<ProjectClosure>();
        var entity = await repo.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId);
        if (entity != null && string.Equals(entity.Status, ProjectStatuses.Closed, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Approved project closure cannot be edited.");
        }

        if (entity == null)
        {
            entity = new ProjectClosure
            {
                TenantId = _currentUserProvider.TenantId,
                ProjectId = projectId,
                CreatedBy = _currentUserProvider.Username,
                CreatedById = _currentUserProvider.UserId
            };
            await repo.AddAsync(entity);
        }

        entity.FinalBudget = dto.FinalBudget;
        entity.FinalCost = dto.FinalCost;
        entity.DeliverablesAccepted = dto.DeliverablesAccepted;
        entity.TasksCompletedOrWaived = dto.TasksCompletedOrWaived;
        entity.AssetsReconciled = dto.AssetsReconciled;
        entity.OpenItemsDisposed = dto.OpenItemsDisposed;
        entity.ClosureChecklistJson = dto.ClosureChecklistJson;
        entity.OpenItemsDisposition = dto.OpenItemsDisposition;
        entity.AssetReconciliationNotes = dto.AssetReconciliationNotes;
        entity.LessonsLearnedSummary = dto.LessonsLearnedSummary;
        entity.PostImplementationReview = dto.PostImplementationReview;
        entity.OverrideReason = dto.OverrideReason;
        entity.RejectionReason = null;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "ClosureDraftUpdated", new Dictionary<string, object> { ["ProjectClosureId"] = entity.Id });
        return MapToDto(entity);
    }

    public async Task SubmitClosureForApprovalAsync(Guid projectId, Guid userId)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.SubmitForApproval);
        var repo = _unitOfWork.Repository<ProjectClosure>();
        var closure = await repo.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException("Create a closure record before submitting it for approval.");
        await ValidateClosureSubmissionAsync(project, closure);
        var workflowResult = await _workflowIntegrationService.SubmitAsync(ProjectClosureWorkflowEntityType, closure.Id);
        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to start closure workflow");
        }

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(ProjectClosureWorkflowEntityType);
        adapter.ApplySubmitOutcome(closure, workflowResult.Outcome, userId);
        closure.SubmittedAt = DateTime.UtcNow;
        closure.RejectionReason = null;
        closure.UpdatedBy = _currentUserProvider.Username;
        closure.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(closure);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "ClosureSubmitted", new Dictionary<string, object> { ["ProjectClosureId"] = closure.Id });
    }

    public async Task ApproveClosureAsync(Guid closureId, Guid userId, string? comments = null)
    {
        var repo = _unitOfWork.Repository<ProjectClosure>();
        var closure = await repo.FirstOrDefaultAsync(x => x.Id == closureId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project closure with ID {closureId} not found");
        var project = await RequireProjectAsync(closure.ProjectId, ProjectAccessOperation.ApproveWorkflow);
        if (!string.Equals(closure.Status, "PendingApproval", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Project closure must be pending approval.");
        }

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync(ProjectClosureWorkflowEntityType, closureId, userId);
        if (!canApprove)
        {
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current closure workflow step");
        }

        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(ProjectClosureWorkflowEntityType, closureId, userId, "Approve", comments);
        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to approve closure");
        }

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(ProjectClosureWorkflowEntityType);
        adapter.ApplyApprovalOutcome(closure, workflowResult.Outcome, userId);
        closure.ApprovedAt = DateTime.UtcNow;
        closure.ApprovedById = userId;
        closure.UpdatedBy = _currentUserProvider.Username;
        closure.LastModifiedById = _currentUserProvider.UserId;
        project.Status = ProjectStatuses.Closed;
        project.ActualEndDate ??= DateTime.UtcNow.Date;
        project.StatusRemarks = string.IsNullOrWhiteSpace(comments) ? project.StatusRemarks : comments;
        project.UpdatedBy = _currentUserProvider.Username;
        project.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(closure);
        await _projectRepository.UpdateAsync(project);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "ClosureApproved", new Dictionary<string, object> { ["ProjectClosureId"] = closure.Id });
    }

    public async Task RejectClosureAsync(Guid closureId, Guid userId, string reason, string? comments = null)
    {
        var repo = _unitOfWork.Repository<ProjectClosure>();
        var closure = await repo.FirstOrDefaultAsync(x => x.Id == closureId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project closure with ID {closureId} not found");
        var project = await RequireProjectAsync(closure.ProjectId, ProjectAccessOperation.ApproveWorkflow);
        if (!string.Equals(closure.Status, "PendingApproval", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Project closure must be pending approval.");
        }

        var canApprove = await _workflowIntegrationService.CanUserApproveAsync(ProjectClosureWorkflowEntityType, closureId, userId);
        if (!canApprove)
        {
            throw new UnauthorizedAccessException("You are not assigned as an approver for the current closure workflow step");
        }

        var rejectionText = string.IsNullOrWhiteSpace(comments) ? reason : comments;
        var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(ProjectClosureWorkflowEntityType, closureId, userId, "Reject", rejectionText);
        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to reject closure");
        }

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(ProjectClosureWorkflowEntityType);
        adapter.ApplyApprovalOutcome(closure, workflowResult.Outcome, userId, rejectionText);
        closure.RejectionReason = rejectionText;
        closure.UpdatedBy = _currentUserProvider.Username;
        closure.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(closure);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "ClosureRejected", new Dictionary<string, object> { ["ProjectClosureId"] = closure.Id });
    }

    public async Task<IEnumerable<ProjectAiInsightDto>> GetAiInsightsAsync(Guid projectId)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var risks = (await _unitOfWork.Repository<ProjectRisk>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var issues = (await _unitOfWork.Repository<ProjectIssue>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var workItems = (await _unitOfWork.Repository<ProjectWorkItem>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var allocations = (await _unitOfWork.Repository<ProjectResourceAllocation>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var insights = new List<ProjectAiInsightDto>();

        if (risks.Count(x => x.Exposure >= 12 && string.Equals(x.Status, "Open", StringComparison.OrdinalIgnoreCase)) > 0)
        {
            insights.Add(new ProjectAiInsightDto { Category = "Risk", Severity = "High", Title = "High-risk exposure detected", Recommendation = "Escalate mitigation ownership and review response plans this week." });
        }
        if (issues.Count(x => string.Equals(x.Status, "Open", StringComparison.OrdinalIgnoreCase) && string.Equals(x.Severity, "High", StringComparison.OrdinalIgnoreCase)) > 0)
        {
            insights.Add(new ProjectAiInsightDto { Category = "Issue", Severity = "High", Title = "Critical issues may delay delivery", Recommendation = "Assign resolution owners and convert blocking issues into dated action items." });
        }
        if (workItems.Any(x => x.PlannedEndDate.HasValue && x.PlannedEndDate.Value.Date < DateTime.UtcNow.Date && x.PercentComplete < 100m))
        {
            insights.Add(new ProjectAiInsightDto { Category = "Schedule", Severity = "Medium", Title = "Overdue work items are accumulating", Recommendation = "Re-baseline or recover the plan using dependency-aware sequencing." });
        }
        if (allocations.Any(x => HasAllocationConflict(x, allocations)))
        {
            insights.Add(new ProjectAiInsightDto { Category = "Capacity", Severity = "Medium", Title = "Resource conflicts detected", Recommendation = "Apply substitution or redistribute hours from overloaded resources." });
        }
        if ((project.ActualCost ?? 0m) > (project.ApprovedBudget ?? project.EstimatedBudget ?? decimal.MaxValue))
        {
            insights.Add(new ProjectAiInsightDto { Category = "Cost", Severity = "High", Title = "Actual cost exceeds budget baseline", Recommendation = "Freeze new discretionary spending and approve a budget revision if justified." });
        }
        if (insights.Count == 0)
        {
            insights.Add(new ProjectAiInsightDto { Category = "Health", Severity = "Low", Title = "No major warnings detected", Recommendation = "Continue monitoring schedule, cost, and open governance items." });
        }

        return insights;
    }

    public async Task<IEnumerable<ProjectResourceOptimizationSuggestionDto>> GetResourceOptimizationSuggestionsAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var periodStart = startDate ?? DateTime.UtcNow.Date;
        var periodEnd = endDate ?? periodStart.AddDays(30);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var accessibleProjectIds = (await GetAccessibleProjectsAsync(take: 2000)).Select(x => x.Id).ToHashSet();
        var allocations = (await _unitOfWork.Repository<ProjectResourceAllocation>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId && x.StartDate <= periodEnd && x.EndDate >= periodStart))
            .Where(x => accessibleProjectIds.Contains(x.ProjectId))
            .Where(IsAllocationActiveForCapacity)
            .ToList();
        var employees = (await _unitOfWork.Repository<Employee>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId && !x.IsDeleted)).ToList();
        var skills = (await _unitOfWork.Repository<EmployeeSkill>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId && x.IsVerified)).ToList();
        var employeeDisplayNames = employees.ToDictionary(x => x.Id, x => string.IsNullOrWhiteSpace(x.DisplayName) ? x.FullName : x.DisplayName);
        var skillLookup = skills
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var qualificationLookup = BuildQualificationMetricsLookup(skills, today);
        var suggestions = new List<ProjectResourceOptimizationSuggestionDto>();

        foreach (var group in allocations.GroupBy(x => x.UserId))
        {
            var allocationList = group.ToList();
            if (!allocationList.Any(x => HasAllocationConflict(x, allocationList)))
            {
                continue;
            }

            var employeeSkills = skillLookup.GetValueOrDefault(group.Key, new List<EmployeeSkill>());
            var employeeSkillIds = employeeSkills.Select(x => x.SkillId).ToHashSet();
            var sourceQualification = qualificationLookup.GetValueOrDefault(group.Key, ResourceQualificationMetrics.Empty);
            var replacement = employees
                .Where(x => x.Id != group.Key)
                .Select(candidate =>
                {
                    var candidateSkills = skillLookup.GetValueOrDefault(candidate.Id, new List<EmployeeSkill>());
                    var sharedSkills = candidateSkills.Where(s => employeeSkillIds.Contains(s.SkillId)).ToList();
                    var sharedSkillNames = sharedSkills
                        .Select(x => x.Skill?.Name)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct()
                        .OrderBy(x => x)
                        .Cast<string>()
                        .ToList();
                    var matchedCertifiedSkillCount = sharedSkills.Count(x => x.IsCertified && !IsCertificationExpired(x, today));
                    var expiredCertifiedSkillCount = sharedSkills.Count(x => x.IsCertified && IsCertificationExpired(x, today));
                    var expiringCertifiedSkillCount = sharedSkills.Count(x => x.IsCertified && IsCertificationExpiring(x, today));
                    var candidateQualification = qualificationLookup.GetValueOrDefault(candidate.Id, ResourceQualificationMetrics.Empty);
                    var score = (matchedCertifiedSkillCount * 100)
                        + (sharedSkills.Count * 10)
                        - (expiredCertifiedSkillCount * 50)
                        - (expiringCertifiedSkillCount * 10);

                    return new
                    {
                        candidate.Id,
                        SharedSkillCount = sharedSkills.Count,
                        SharedSkillNames = sharedSkillNames,
                        MatchedCertifiedSkillCount = matchedCertifiedSkillCount,
                        CandidateQualification = candidateQualification,
                        Score = score
                    };
                })
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.SharedSkillCount)
                .ThenByDescending(x => x.MatchedCertifiedSkillCount)
                .FirstOrDefault(x => x.SharedSkillCount > 0);

            var severity = sourceQualification.ExpiredCertificationCount > 0
                ? "Critical"
                : sourceQualification.ExpiringCertificationCount > 0
                    ? "High"
                    : "High";
            var recommendation = replacement == null
                ? "No skill-matched substitute found. Reduce overlapping hours or widen the assignment window."
                : "Reassign a portion of the overlapping work to the suggested replacement resource.";
            if (sourceQualification.ExpiredCertificationCount > 0)
            {
                recommendation = $"{recommendation} The current assignee has expired certifications that should be covered immediately.";
            }
            else if (sourceQualification.ExpiringCertificationCount > 0)
            {
                recommendation = $"{recommendation} Plan coverage before certifications expire in the current window.";
            }

            suggestions.Add(new ProjectResourceOptimizationSuggestionDto
            {
                UserId = group.Key,
                UserDisplayName = employeeDisplayNames.GetValueOrDefault(group.Key),
                SuggestedReplacementUserId = replacement?.Id,
                SuggestedReplacementUserDisplayName = replacement == null ? null : employeeDisplayNames.GetValueOrDefault(replacement.Id),
                Severity = severity,
                Recommendation = recommendation,
                MatchedSkills = replacement?.SharedSkillNames ?? new List<string>(),
                MatchedSkillCount = replacement?.SharedSkillCount ?? 0,
                MatchedCertifiedSkillCount = replacement?.MatchedCertifiedSkillCount ?? 0,
                ReplacementVerifiedSkillCount = replacement?.CandidateQualification.VerifiedSkillCount ?? 0,
                ReplacementCertifiedSkillCount = replacement?.CandidateQualification.CertifiedSkillCount ?? 0,
                ReplacementExpiringCertificationCount = replacement?.CandidateQualification.ExpiringCertificationCount ?? 0,
                ReplacementExpiredCertificationCount = replacement?.CandidateQualification.ExpiredCertificationCount ?? 0,
                ReplacementQualificationRisk = replacement?.CandidateQualification.QualificationRisk ?? "Healthy",
                AffectedAllocationIds = allocationList.Where(x => HasAllocationConflict(x, allocationList)).Select(x => x.Id).ToList()
            });
        }

        return suggestions;
    }

    public async Task<ProjectMobileSummaryDto> GetMobileSummaryAsync(Guid userId)
    {
        var assignments = (await _unitOfWork.Repository<ProjectWorkItem>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId && x.AssignedToUserId == userId)).OrderBy(x => x.PlannedEndDate).ToList();
        var projects = (await _projectRepository.LookupAsync(take: 500)).ToDictionary(x => x.Id);
        var timesheetEntries = (await _unitOfWork.Repository<ProjectTimesheetEntry>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId && x.UserId == userId)).ToList();
        var expenseEntries = (await _unitOfWork.Repository<ProjectExpense>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId && x.UserId == userId)).ToList();
        var pendingHours = timesheetEntries.Where(x => !HasApprovedEntryStatus(x.Status)).Sum(x => x.Hours);
        var pendingExpenses = expenseEntries.Where(x => !HasApprovedEntryStatus(x.Status)).Sum(x => x.Amount + x.TaxAmount);

        return new ProjectMobileSummaryDto
        {
            AssignmentCount = assignments.Count,
            OverdueCount = assignments.Count(x => x.PlannedEndDate.HasValue && x.PlannedEndDate.Value.Date < DateTime.UtcNow.Date && x.PercentComplete < 100m),
            PendingHours = pendingHours,
            PendingExpenses = pendingExpenses,
            Assignments = assignments.Select(x => new ProjectMobileAssignmentDto
            {
                ProjectId = x.ProjectId,
                WorkItemId = x.Id,
                ProjectCode = projects.TryGetValue(x.ProjectId, out var project) ? project.ProjectCode : string.Empty,
                ProjectTitle = projects.TryGetValue(x.ProjectId, out project) ? project.Title : string.Empty,
                WorkItemTitle = x.Title,
                Status = x.Status,
                PercentComplete = x.PercentComplete,
                PlannedEndDate = x.PlannedEndDate
            }).ToList()
        };
    }

    public async Task<IEnumerable<ProjectDocumentDto>> GetDocumentsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return (await _unitOfWork.Repository<ProjectDocument>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderByDescending(x => x.CreatedAt)
            .Select(MapToDto);
    }

    public async Task<ProjectDocumentDto> AttachDocumentAsync(Guid projectId, AttachProjectDocumentDto dto)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.ManageExecution);
        var uploadRecord = await _unitOfWork.Repository<FileUploadRecord>()
            .FirstOrDefaultAsync(x => x.TenantId == _currentUserProvider.TenantId && x.FilePath == dto.FilePath);
        var entity = new ProjectDocument
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            FileUploadRecordId = uploadRecord?.Id,
            DocumentName = dto.DocumentName,
            Category = dto.Category,
            DocumentType = dto.DocumentType,
            FilePath = dto.FilePath,
            PublicUrl = dto.PublicUrl,
            FileType = dto.FileType,
            FileSize = dto.FileSize,
            VersionLabel = dto.VersionLabel,
            Status = dto.Status,
            EffectiveDate = dto.EffectiveDate,
            IsExternalVisible = dto.IsExternalVisible,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };
        await _unitOfWork.Repository<ProjectDocument>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "DocumentAttached", new Dictionary<string, object>
        {
            ["DocumentId"] = entity.Id,
            ["DocumentName"] = entity.DocumentName,
            ["Category"] = entity.Category,
            ["IsExternalVisible"] = entity.IsExternalVisible
        });
        return MapToDto(entity);
    }

    public async Task DeleteDocumentAsync(Guid documentId)
    {
        var entity = await _unitOfWork.Repository<ProjectDocument>().FirstOrDefaultAsync(x => x.Id == documentId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project document with ID {documentId} not found");
        await RequireProjectAsync(entity.ProjectId, ProjectAccessOperation.ManageExecution);
        await _unitOfWork.Repository<ProjectDocument>().DeleteAsync(documentId);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectCommentDto>> GetCommentsAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        return (await _unitOfWork.Repository<ProjectComment>().FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderByDescending(x => x.CreatedAt)
            .Select(MapToDto);
    }

    public async Task<ProjectCommentDto> AddCommentAsync(Guid projectId, CreateProjectCommentDto dto)
    {
        var project = await RequireProjectAsync(projectId, ProjectAccessOperation.ManageExecution);
        var entity = new ProjectComment
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            WorkItemId = dto.WorkItemId,
            CommentType = dto.CommentType,
            Body = dto.Body,
            MentionedUsersJson = dto.MentionedUsersJson,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };
        await _unitOfWork.Repository<ProjectComment>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "CommentAdded", new Dictionary<string, object>
        {
            ["CommentId"] = entity.Id,
            ["CommentType"] = entity.CommentType,
            ["WorkItemId"] = entity.WorkItemId ?? Guid.Empty
        });
        return MapToDto(entity);
    }

    public async Task<IEnumerable<ProjectExternalSummaryDto>> GetExternalProjectsAsync(Guid userId)
    {
        var partner = await RequireExternalBusinessPartnerAsync(userId);
        var policies = (await _unitOfWork.Repository<ProjectExternalAccessPolicy>().FindAsync(x =>
                x.BusinessPartnerId == partner.Id &&
                x.TenantId == _currentUserProvider.TenantId))
            .ToList();
        var policyProjectIds = policies.Select(x => x.ProjectId).Distinct().ToHashSet();
        var projects = (await _projectRepository.LookupAsync(take: 500))
            .Where(x => x.ExternalPortalAccessEnabled && (x.BusinessPartnerId == partner.Id || policyProjectIds.Contains(x.Id)))
            .ToList();
        var milestones = (await _unitOfWork.Repository<ProjectMilestone>().FindAsync(x => projects.Select(p => p.Id).Contains(x.ProjectId))).ToList();

        return projects.Select(x => new ProjectExternalSummaryDto
        {
            Id = x.Id,
            ProjectCode = x.ProjectCode,
            Title = x.Title,
            Status = x.Status,
            Summary = x.Summary,
            StartDate = x.StartDate,
            TargetEndDate = x.TargetEndDate,
            ProgressPercent = x.ProgressPercent,
            ExternalCollaborationEnabled = x.ExternalCollaborationEnabled,
            OpenMilestoneCount = milestones.Count(m => m.ProjectId == x.Id && !string.Equals(m.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        }).ToList();
    }

    public async Task<ProjectExternalDetailDto?> GetExternalProjectByIdAsync(Guid projectId, Guid userId)
    {
        var partner = await RequireExternalBusinessPartnerAsync(userId);
        var project = await _projectRepository.GetDetailByIdAsync(projectId);
        if (project == null || !project.ExternalPortalAccessEnabled)
        {
            return null;
        }

        var policies = await GetExternalPoliciesAsync(projectId, partner.Id);
        if (!HasExternalProjectAccess(project, partner.Id, policies))
        {
            return null;
        }

        project.WorkItems = await LoadWorkTreeAsync(project.Id);
        var canCollaborate = project.ExternalCollaborationEnabled
            && HasExternalActionPermission(project, policies, partner.Id, "Project", null, requireApprove: false, requireUpload: false, requireComment: false, requireCollaboration: true);
        var canComment = project.ExternalCollaborationEnabled
            && HasExternalActionPermission(project, policies, partner.Id, "Project", null, requireApprove: false, requireUpload: false, requireComment: true, requireCollaboration: false);
        var canUploadDocuments = project.ExternalCollaborationEnabled
            && HasExternalActionPermission(project, policies, partner.Id, "Project", null, requireApprove: false, requireUpload: true, requireComment: false, requireCollaboration: false);
        var visibleWorkItems = project.WorkItems
            .Where(x => HasArtifactAccess(project, policies, partner.Id, "WorkItem", x.Id))
            .OrderBy(x => x.SortOrder)
            .Select(x =>
            {
                var dto = MapToDto(x);
                dto.CanExternalUpdate = project.ExternalCollaborationEnabled
                    && HasExternalActionPermission(project, policies, partner.Id, "WorkItem", x.Id, requireApprove: false, requireUpload: false, requireComment: false, requireCollaboration: true);
                dto.CanExternalComment = project.ExternalCollaborationEnabled
                    && HasExternalActionPermission(project, policies, partner.Id, "WorkItem", x.Id, requireApprove: false, requireUpload: false, requireComment: true, requireCollaboration: false);
                return dto;
            })
            .ToList();
        var visibleDeliverables = project.Deliverables
            .Where(x => x.IsExternalVisible && HasArtifactAccess(project, policies, partner.Id, "Deliverable", x.Id))
            .OrderBy(x => x.TargetDate)
            .Select(x =>
            {
                var dto = MapToDto(x);
                dto.CanExternalSubmit = project.ExternalCollaborationEnabled
                    && x.ExternalSubmissionAllowed
                    && HasExternalActionPermission(project, policies, partner.Id, "Deliverable", x.Id, requireApprove: false, requireUpload: true, requireComment: false, requireCollaboration: false);
                dto.CanExternalApprove = project.ExternalCollaborationEnabled
                    && x.ExternalSignOffRequired
                    && HasExternalActionPermission(project, policies, partner.Id, "Deliverable", x.Id, requireApprove: true, requireUpload: false, requireComment: false, requireCollaboration: false);
                return dto;
            })
            .ToList();
        visibleDeliverables = await AttachDeliverableExternalReviewsAsync(visibleDeliverables);
        var visibleDocuments = project.Documents
            .Where(x => x.IsExternalVisible && HasArtifactAccess(project, policies, partner.Id, "Document", x.Id))
            .OrderByDescending(x => x.CreatedAt)
            .Select(MapToDto)
            .ToList();
        var actionableWorkItemCount = visibleWorkItems.Count(x => x.CanExternalUpdate);
        var blockedWorkItemCount = visibleWorkItems.Count(x => string.Equals(x.Status, "Blocked", StringComparison.OrdinalIgnoreCase));
        var pendingExternalSubmissionCount = visibleDeliverables.Count(x =>
            x.ExternalSubmissionAllowed
            && x.CanExternalSubmit
            && !string.Equals(x.Status, "PendingApproval", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(x.Status, "Approved", StringComparison.OrdinalIgnoreCase));
        var pendingExternalSignOffCount = visibleDeliverables.Count(x =>
            x.ExternalSignOffRequired
            && x.CanExternalApprove
            && string.Equals(x.Status, "PendingExternalSignOff", StringComparison.OrdinalIgnoreCase));

        return new ProjectExternalDetailDto
        {
            Id = project.Id,
            ProjectCode = project.ProjectCode,
            Title = project.Title,
            Status = project.Status,
            Summary = project.Summary,
            StartDate = project.StartDate,
            TargetEndDate = project.TargetEndDate,
            ProgressPercent = project.ProgressPercent,
            ExternalCollaborationEnabled = project.ExternalCollaborationEnabled,
            OpenMilestoneCount = project.Milestones.Count(x => !string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase)),
            Methodology = project.Methodology,
            StatusRemarks = project.StatusRemarks,
            CanCollaborate = canCollaborate,
            CanComment = canComment,
            CanUploadDocuments = canUploadDocuments,
            ActionableWorkItemCount = actionableWorkItemCount,
            BlockedWorkItemCount = blockedWorkItemCount,
            PendingExternalSubmissionCount = pendingExternalSubmissionCount,
            PendingExternalSignOffCount = pendingExternalSignOffCount,
            WorkItems = visibleWorkItems,
            Milestones = project.Milestones.OrderBy(x => x.TargetDate).Select(MapToDto).ToList(),
            Deliverables = visibleDeliverables,
            Documents = visibleDocuments,
            Comments = project.Comments.Where(x => string.Equals(x.CommentType, "ExternalUpdate", StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x.CreatedAt).Select(MapToDto).ToList()
        };
    }

    public async Task<ProjectCommentDto> AddExternalCommentAsync(Guid projectId, CreateProjectCommentDto dto, Guid userId)
    {
        var project = await RequireExternalProjectAsync(projectId, userId, requireCollaboration: true);
        await EnsureExternalPolicyAsync(projectId, userId, dto.WorkItemId.HasValue ? "WorkItem" : "Project", dto.WorkItemId, requireApprove: false, requireUpload: false, requireComment: true, requireCollaboration: false);
        var entity = new ProjectComment
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            WorkItemId = dto.WorkItemId,
            CommentType = "ExternalUpdate",
            Body = dto.Body,
            MentionedUsersJson = dto.MentionedUsersJson,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _unitOfWork.Repository<ProjectComment>().AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "ExternalCommentAdded", new Dictionary<string, object>
        {
            ["CommentId"] = entity.Id,
            ["CommentType"] = entity.CommentType,
            ["WorkItemId"] = entity.WorkItemId ?? Guid.Empty
        }, audience: "External");
        return MapToDto(entity);
    }

    public async Task<ProjectDocumentDto> AttachExternalDocumentAsync(Guid projectId, AttachProjectDocumentDto dto, Guid userId)
    {
        var project = await RequireExternalProjectAsync(projectId, userId, requireCollaboration: true);
        await EnsureExternalPolicyAsync(projectId, userId, "Project", null, requireApprove: false, requireUpload: true, requireComment: false, requireCollaboration: false);
        dto.IsExternalVisible = true;
        var document = await AttachDocumentAsync(projectId, dto);
        await PublishActivityAsync(project, "ExternalDocumentAttached", new Dictionary<string, object>
        {
            ["DocumentId"] = document.Id,
            ["DocumentName"] = document.DocumentName
        }, audience: "External");
        return document;
    }

    public async Task<ProjectDeliverableDto> SubmitExternalDeliverableAsync(Guid projectId, Guid deliverableId, SubmitProjectDeliverableDto dto, Guid userId)
    {
        var project = await RequireExternalProjectAsync(projectId, userId, requireCollaboration: true);
        await EnsureExternalPolicyAsync(projectId, userId, "Deliverable", deliverableId, requireApprove: false, requireUpload: true, requireComment: false, requireCollaboration: false);
        var result = await SubmitDeliverableAsync(deliverableId, dto);
        await AddDeliverableExternalReviewAsync(projectId, deliverableId, userId, "Submitted", result.Status, dto.Notes, result.SubmittedDocumentId);
        await _unitOfWork.SaveChangesAsync();
        result = (await AttachDeliverableExternalReviewsAsync(new List<ProjectDeliverableDto> { result })).Single();
        await PublishActivityAsync(project, "ExternalDeliverableSubmitted", new Dictionary<string, object> { ["DeliverableId"] = deliverableId }, audience: "External");
        return result;
    }

    public async Task<ProjectDeliverableDto> ApproveExternalDeliverableAsync(Guid projectId, Guid deliverableId, string? notes, Guid userId)
    {
        var project = await RequireExternalProjectAsync(projectId, userId, requireCollaboration: true);
        await EnsureExternalPolicyAsync(projectId, userId, "Deliverable", deliverableId, requireApprove: true, requireUpload: false, requireComment: false, requireCollaboration: false);
        var repo = _unitOfWork.Repository<ProjectDeliverable>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == deliverableId && x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project deliverable with ID {deliverableId} not found");
        if (!entity.ExternalSignOffRequired)
        {
            throw new InvalidOperationException("This deliverable does not require external sign-off.");
        }

        if (!string.Equals(entity.Status, DeliverableStatusPendingExternalSignOff, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Project deliverable must be in {DeliverableStatusPendingExternalSignOff} status for external sign-off.");
        }

        entity.ExternalApprovedAt = DateTime.UtcNow;
        entity.ExternalApprovedById = userId;
        entity.ExternalApprovalNotes = notes;
        entity.Status = DeliverableStatusPendingApproval;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await AddDeliverableCommentAsync(entity, "ExternalDeliverableApproval", notes);
        await AddDeliverableExternalReviewAsync(entity.ProjectId, entity.Id, userId, "Approved", entity.Status, notes, entity.SubmittedDocumentId);
        await SubmitDeliverableWorkflowAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "ExternalDeliverableApproved", new Dictionary<string, object>
        {
            ["DeliverableId"] = deliverableId,
            ["Status"] = entity.Status
        }, audience: "External");
        return (await AttachDeliverableExternalReviewsAsync(new List<ProjectDeliverableDto> { MapToDto(entity) })).Single();
    }

    public async Task<ProjectDeliverableDto> RejectExternalDeliverableAsync(Guid projectId, Guid deliverableId, string? notes, Guid userId)
    {
        var project = await RequireExternalProjectAsync(projectId, userId, requireCollaboration: true);
        await EnsureExternalPolicyAsync(projectId, userId, "Deliverable", deliverableId, requireApprove: true, requireUpload: false, requireComment: false, requireCollaboration: false);
        var repo = _unitOfWork.Repository<ProjectDeliverable>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == deliverableId && x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project deliverable with ID {deliverableId} not found");
        if (!entity.ExternalSignOffRequired)
        {
            throw new InvalidOperationException("This deliverable does not require external sign-off.");
        }

        if (!string.Equals(entity.Status, DeliverableStatusPendingExternalSignOff, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Project deliverable must be in {DeliverableStatusPendingExternalSignOff} status for external rejection.");
        }

        var rejectionText = !string.IsNullOrWhiteSpace(notes) ? notes.Trim() : "Rejected by external reviewer";
        entity.ExternalApprovedAt = null;
        entity.ExternalApprovedById = null;
        entity.ExternalApprovalNotes = rejectionText;
        entity.Status = DeliverableStatusRejected;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await repo.UpdateAsync(entity);
        await AddDeliverableCommentAsync(entity, "ExternalDeliverableRejection", rejectionText);
        await AddDeliverableExternalReviewAsync(entity.ProjectId, entity.Id, userId, "Rejected", entity.Status, rejectionText, entity.SubmittedDocumentId);
        await _unitOfWork.SaveChangesAsync();
        await PublishActivityAsync(project, "ExternalDeliverableRejected", new Dictionary<string, object>
        {
            ["DeliverableId"] = deliverableId,
            ["Status"] = entity.Status
        }, audience: "External");
        return (await AttachDeliverableExternalReviewsAsync(new List<ProjectDeliverableDto> { MapToDto(entity) })).Single();
    }

    public async Task<ProjectWorkItemDto> UpdateExternalWorkItemProgressAsync(Guid projectId, Guid workItemId, UpdateProjectWorkItemProgressDto dto, Guid userId)
    {
        var project = await RequireExternalProjectAsync(projectId, userId, requireCollaboration: true);
        await EnsureExternalPolicyAsync(projectId, userId, "WorkItem", workItemId, requireApprove: false, requireUpload: false, requireComment: false, requireCollaboration: true);
        var repo = _unitOfWork.Repository<ProjectWorkItem>();
        var entity = await repo.FirstOrDefaultAsync(x => x.Id == workItemId && x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId)
            ?? throw new InvalidOperationException($"Project work item with ID {workItemId} not found");

        entity.Status = dto.Status;
        entity.PercentComplete = Math.Clamp(dto.PercentComplete, 0m, 100m);
        entity.ActualStartDate = dto.ActualStartDate ?? entity.ActualStartDate ?? DateTime.UtcNow;
        entity.ActualEndDate = entity.PercentComplete >= 100m
            ? dto.ActualEndDate ?? entity.ActualEndDate ?? DateTime.UtcNow
            : dto.ActualEndDate;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await repo.UpdateAsync(entity);

        ProjectComment? comment = null;
        if (!string.IsNullOrWhiteSpace(dto.Notes))
        {
            comment = new ProjectComment
            {
                TenantId = _currentUserProvider.TenantId,
                ProjectId = projectId,
                WorkItemId = workItemId,
                CommentType = "ExternalUpdate",
                Body = dto.Notes.Trim(),
                CreatedBy = _currentUserProvider.Username,
                CreatedById = _currentUserProvider.UserId
            };
            await _unitOfWork.Repository<ProjectComment>().AddAsync(comment);
        }

        await _unitOfWork.SaveChangesAsync();
        await UpdateProjectProgressAsync(project);
        await PublishActivityAsync(project, "ExternalWorkItemProgressUpdated", new Dictionary<string, object>
        {
            ["WorkItemId"] = entity.Id,
            ["Title"] = entity.Title,
            ["Status"] = entity.Status,
            ["PercentComplete"] = entity.PercentComplete,
            ["CommentId"] = comment?.Id ?? Guid.Empty
        }, audience: "External");
        return MapToDto(entity);
    }

    public async Task<ProjectDashboardDto> GetDashboardAsync()
    {
        var projects = await GetAccessibleProjectsAsync(take: 1000);
        var projectIds = projects.Select(x => x.Id).ToList();
        var workItems = (await _unitOfWork.Repository<ProjectWorkItem>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId)).ToList();
        workItems = workItems.Where(x => projectIds.Contains(x.ProjectId)).ToList();
        var milestones = (await _unitOfWork.Repository<ProjectMilestone>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId)).Where(x => projectIds.Contains(x.ProjectId)).ToList();
        var risks = (await _unitOfWork.Repository<ProjectRisk>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId)).Where(x => projectIds.Contains(x.ProjectId)).ToList();
        var issues = (await _unitOfWork.Repository<ProjectIssue>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId)).Where(x => projectIds.Contains(x.ProjectId)).ToList();

        return new ProjectDashboardDto
        {
            TotalProjects = projects.Count,
            DraftProjects = projects.Count(x => x.Status == ProjectStatuses.Draft),
            ActiveProjects = projects.Count(x => x.Status == ProjectStatuses.Planned || x.Status == ProjectStatuses.InProgress || x.Status == ProjectStatuses.OnHold),
            PendingApprovalProjects = projects.Count(x => x.Status == ProjectStatuses.PendingApproval),
            CompletedProjects = projects.Count(x => x.Status == ProjectStatuses.Completed || x.Status == ProjectStatuses.Closed),
            OverdueTasks = workItems.Count(x => x.NodeType == ProjectWorkItemNodeTypes.Task && x.PlannedEndDate.HasValue && x.PlannedEndDate.Value < DateTime.UtcNow && !string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase)),
            DueMilestonesThisMonth = milestones.Count(x => x.TargetDate.Month == DateTime.UtcNow.Month && x.TargetDate.Year == DateTime.UtcNow.Year),
            OverdueMilestones = milestones.Count(x => x.TargetDate < DateTime.UtcNow && !string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase)),
            OpenRisks = risks.Count(x => string.Equals(x.Status, "Open", StringComparison.OrdinalIgnoreCase)),
            OpenIssues = issues.Count(x => string.Equals(x.Status, "Open", StringComparison.OrdinalIgnoreCase)),
            TotalEstimatedBudget = projects.Sum(x => x.EstimatedBudget ?? 0m),
            TotalApprovedBudget = projects.Sum(x => x.ApprovedBudget ?? 0m),
            TotalActualCost = projects.Sum(x => x.ActualCost ?? 0m),
            AtRiskProjects = projects.Where(x => risks.Any(r => r.ProjectId == x.Id && r.Exposure >= 12)).Take(5).Select(MapToDto).ToList()
        };
    }

    public async Task<IEnumerable<ProjectDto>> GetProjectRegisterReportAsync(string? search = null, string? status = null, Guid? projectTypeId = null, int take = 200)
        => (await GetAccessibleProjectsAsync(search, status, projectTypeId, null, null, take))
            .OrderBy(x => x.ProjectCode)
            .Select(MapToDto);

    public async Task<IEnumerable<ProjectTaskAgingReportItemDto>> GetTaskAgingReportAsync(Guid? projectId = null, int take = 100)
    {
        if (projectId.HasValue)
        {
            await RequireProjectAsync(projectId.Value, ProjectAccessOperation.View);
        }

        var projects = (await GetAccessibleProjectsAsync(take: 1000)).ToDictionary(x => x.Id);
        var accessibleProjectIds = projects.Keys.ToHashSet();
        var now = DateTime.UtcNow;
        var workItems = (await _unitOfWork.Repository<ProjectWorkItem>()
                .FindAsync(x => x.TenantId == _currentUserProvider.TenantId
                    && x.NodeType == ProjectWorkItemNodeTypes.Task
                    && (!projectId.HasValue || x.ProjectId == projectId.Value)))
            .Where(x => accessibleProjectIds.Contains(x.ProjectId))
            .Where(x => x.PlannedEndDate.HasValue && x.PlannedEndDate.Value < now && !string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.PlannedEndDate)
            .Take(take)
            .ToList();

        return workItems.Select(x =>
        {
            projects.TryGetValue(x.ProjectId, out var project);
            return new ProjectTaskAgingReportItemDto
            {
                ProjectId = x.ProjectId,
                ProjectCode = project?.ProjectCode ?? string.Empty,
                ProjectTitle = project?.Title ?? string.Empty,
                WorkItemId = x.Id,
                WorkItemTitle = x.Title,
                Status = x.Status,
                Priority = x.Priority,
                PlannedEndDate = x.PlannedEndDate,
                DaysOverdue = x.PlannedEndDate.HasValue ? Math.Max(0, (now.Date - x.PlannedEndDate.Value.Date).Days) : 0
            };
        }).ToList();
    }

    public async Task<IEnumerable<ProjectMilestoneTrackerReportItemDto>> GetMilestoneTrackerReportAsync(Guid? projectId = null, int take = 100)
    {
        if (projectId.HasValue)
        {
            await RequireProjectAsync(projectId.Value, ProjectAccessOperation.View);
        }

        var projects = (await GetAccessibleProjectsAsync(take: 1000)).ToDictionary(x => x.Id);
        var accessibleProjectIds = projects.Keys.ToHashSet();
        var today = DateTime.UtcNow.Date;
        var milestones = (await _unitOfWork.Repository<ProjectMilestone>()
                .FindAsync(x => x.TenantId == _currentUserProvider.TenantId && (!projectId.HasValue || x.ProjectId == projectId.Value)))
            .Where(x => accessibleProjectIds.Contains(x.ProjectId))
            .OrderBy(x => x.TargetDate)
            .Take(take)
            .ToList();

        return milestones.Select(x =>
        {
            projects.TryGetValue(x.ProjectId, out var project);
            var daysFromToday = (x.TargetDate.Date - today).Days;
            return new ProjectMilestoneTrackerReportItemDto
            {
                ProjectId = x.ProjectId,
                ProjectCode = project?.ProjectCode ?? string.Empty,
                ProjectTitle = project?.Title ?? string.Empty,
                MilestoneId = x.Id,
                MilestoneTitle = x.Title,
                Status = x.Status,
                TargetDate = x.TargetDate,
                IsOverdue = x.TargetDate.Date < today && !string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase),
                DaysFromToday = daysFromToday
            };
        }).ToList();
    }

    public async Task<IEnumerable<ProjectBudgetActualReportItemDto>> GetBudgetActualReportAsync(int take = 200)
        => (await GetAccessibleProjectsAsync(take: take))
            .OrderBy(x => x.ProjectCode)
            .Select(x => new ProjectBudgetActualReportItemDto
            {
                ProjectId = x.Id,
                ProjectCode = x.ProjectCode,
                ProjectTitle = x.Title,
                Status = x.Status,
                BudgetStatus = x.BudgetStatus,
                EstimatedBudget = x.EstimatedBudget,
                ApprovedBudget = x.ApprovedBudget,
                ActualCost = x.ActualCost,
                BudgetVariance = (x.ApprovedBudget ?? x.EstimatedBudget ?? 0m) - (x.ActualCost ?? 0m),
                ProgressPercent = x.ProgressPercent
            }).ToList();

    public async Task<IEnumerable<ProjectRiskIssueSummaryReportItemDto>> GetRiskIssueSummaryReportAsync(int take = 200)
    {
        var projects = (await GetAccessibleProjectsAsync(take: take)).ToList();
        var projectIds = projects.Select(x => x.Id).ToList();
        var risks = await _unitOfWork.Repository<ProjectRisk>().FindAsync(x => projectIds.Contains(x.ProjectId));
        var issues = await _unitOfWork.Repository<ProjectIssue>().FindAsync(x => projectIds.Contains(x.ProjectId));

        return projects.Select(x => new ProjectRiskIssueSummaryReportItemDto
        {
            ProjectId = x.Id,
            ProjectCode = x.ProjectCode,
            ProjectTitle = x.Title,
            OpenRiskCount = risks.Count(r => r.ProjectId == x.Id && string.Equals(r.Status, "Open", StringComparison.OrdinalIgnoreCase)),
            HighRiskCount = risks.Count(r => r.ProjectId == x.Id && string.Equals(r.Status, "Open", StringComparison.OrdinalIgnoreCase) && r.Exposure >= 12),
            OpenIssueCount = issues.Count(i => i.ProjectId == x.Id && string.Equals(i.Status, "Open", StringComparison.OrdinalIgnoreCase))
        }).ToList();
    }

    public async Task<IEnumerable<ProjectPortfolioSummaryReportItemDto>> GetPortfolioSummaryReportAsync(int take = 100)
    {
        var portfolios = (await _projectPortfolioRepository.GetAllAsync()).Take(take).ToList();
        var portfolioIds = portfolios.Select(x => x.Id).ToList();
        var programs = (await _projectProgramRepository.GetAllAsync()).Where(x => x.PortfolioId.HasValue && portfolioIds.Contains(x.PortfolioId.Value)).ToList();
        var projects = (await GetAccessibleProjectsAsync(take: 1000)).Where(x => x.PortfolioId.HasValue && portfolioIds.Contains(x.PortfolioId.Value)).ToList();
        var projectIds = projects.Select(x => x.Id).ToList();
        var risks = await _unitOfWork.Repository<ProjectRisk>().FindAsync(x => projectIds.Contains(x.ProjectId));

        return portfolios.Select(x => new ProjectPortfolioSummaryReportItemDto
        {
            PortfolioId = x.Id,
            PortfolioCode = x.Code,
            PortfolioName = x.Name,
            ProgramCount = programs.Count(p => p.PortfolioId == x.Id),
            ProjectCount = projects.Count(p => p.PortfolioId == x.Id),
            ActiveProjectCount = projects.Count(p => p.PortfolioId == x.Id && IsActiveProjectStatus(p.Status)),
            TotalEstimatedBudget = projects.Where(p => p.PortfolioId == x.Id).Sum(p => p.EstimatedBudget ?? 0m),
            TotalActualCost = projects.Where(p => p.PortfolioId == x.Id).Sum(p => p.ActualCost ?? 0m),
            HighRiskProjectCount = projects.Count(p => p.PortfolioId == x.Id && risks.Any(r => r.ProjectId == p.Id && string.Equals(r.Status, "Open", StringComparison.OrdinalIgnoreCase) && r.Exposure >= 12))
        }).ToList();
    }

    public async Task<IEnumerable<ProjectProgramSummaryReportItemDto>> GetProgramSummaryReportAsync(Guid? portfolioId = null, int take = 100)
    {
        var programs = (portfolioId.HasValue ? await _projectProgramRepository.GetByPortfolioIdAsync(portfolioId.Value) : await _projectProgramRepository.GetAllAsync())
            .Take(take)
            .ToList();
        var programIds = programs.Select(x => x.Id).ToList();
        var projects = (await GetAccessibleProjectsAsync(take: 1000)).Where(x => x.ProgramId.HasValue && programIds.Contains(x.ProgramId.Value)).ToList();

        return programs.Select(x =>
        {
            var programProjects = projects.Where(p => p.ProgramId == x.Id).ToList();
            return new ProjectProgramSummaryReportItemDto
            {
                ProgramId = x.Id,
                PortfolioId = x.PortfolioId,
                ProgramCode = x.Code,
                ProgramName = x.Name,
                PortfolioName = x.Portfolio?.Name,
                ProjectCount = programProjects.Count,
                ActiveProjectCount = programProjects.Count(p => IsActiveProjectStatus(p.Status)),
                TotalEstimatedBudget = programProjects.Sum(p => p.EstimatedBudget ?? 0m),
                TotalActualCost = programProjects.Sum(p => p.ActualCost ?? 0m),
                AverageProgressPercent = programProjects.Count == 0 ? 0m : decimal.Round(programProjects.Average(p => p.ProgressPercent), 2)
            };
        }).ToList();
    }

    public async Task<IEnumerable<ProjectPerformanceAnalyticsReportItemDto>> GetPerformanceAnalyticsReportAsync(int take = 200)
        => (await GetAccessibleProjectsAsync(take: take))
            .OrderBy(x => x.ProjectCode)
            .Select(x =>
            {
                var budgetBaseline = x.ApprovedBudget ?? x.EstimatedBudget ?? 0m;
                var progressRatio = x.ProgressPercent <= 0 ? 0m : Math.Min(1m, x.ProgressPercent / 100m);
                var actualCost = x.ActualCost ?? 0m;
                var earnedValue = decimal.Round(budgetBaseline * progressRatio, 2);
                var plannedValue = earnedValue;
                var estimateAtCompletion = progressRatio > 0m && actualCost > 0m
                    ? decimal.Round(actualCost / progressRatio, 2)
                    : budgetBaseline;
                var estimateToComplete = Math.Max(0m, estimateAtCompletion - actualCost);
                var projectedVariance = decimal.Round(budgetBaseline - estimateAtCompletion, 2);
                decimal? cpi = actualCost > 0m ? decimal.Round(earnedValue / actualCost, 2) : null;
                var healthStatus = projectedVariance < 0m || (cpi.HasValue && cpi.Value < 0.9m)
                    ? "Watch"
                    : progressRatio >= 1m && actualCost <= budgetBaseline
                        ? "Complete"
                        : "OnTrack";

                return new ProjectPerformanceAnalyticsReportItemDto
                {
                    ProjectId = x.Id,
                    ProjectCode = x.ProjectCode,
                    ProjectTitle = x.Title,
                    Status = x.Status,
                    BudgetBaseline = budgetBaseline,
                    ProgressPercent = x.ProgressPercent,
                    EarnedValue = earnedValue,
                    PlannedValue = plannedValue,
                    ActualCost = actualCost,
                    CostPerformanceIndex = cpi,
                    EstimateAtCompletion = estimateAtCompletion,
                    EstimateToComplete = estimateToComplete,
                    ProjectedVariance = projectedVariance,
                    HealthStatus = healthStatus
                };
            }).ToList();

    public async Task<IEnumerable<ProjectPortfolioPrioritizationReportItemDto>> GetPortfolioPrioritizationReportAsync(Guid? portfolioId = null, int take = 100)
    {
        var projects = (await GetAccessibleProjectsAsync(take: Math.Max(take * 4, 200)))
            .Where(x => !portfolioId.HasValue || x.PortfolioId == portfolioId.Value)
            .ToList();

        if (projects.Count == 0)
        {
            return Array.Empty<ProjectPortfolioPrioritizationReportItemDto>();
        }

        var projectIds = projects.Select(x => x.Id).ToHashSet();
        var risks = (await _unitOfWork.Repository<ProjectRisk>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId && projectIds.Contains(x.ProjectId))).ToList();
        var issues = (await _unitOfWork.Repository<ProjectIssue>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId && projectIds.Contains(x.ProjectId))).ToList();
        var milestones = (await _unitOfWork.Repository<ProjectMilestone>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId && projectIds.Contains(x.ProjectId))).ToList();
        var performance = (await GetPerformanceAnalyticsReportAsync(Math.Max(take * 4, 200))).ToDictionary(x => x.ProjectId);
        var today = DateTime.UtcNow.Date;

        return projects
            .Select(project =>
            {
                var openRiskCount = risks.Count(x =>
                    x.ProjectId == project.Id
                    && !string.Equals(x.Status, "Closed", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(x.Status, "Mitigated", StringComparison.OrdinalIgnoreCase));
                var openIssueCount = issues.Count(x =>
                    x.ProjectId == project.Id
                    && !string.Equals(x.Status, "Closed", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(x.Status, "Resolved", StringComparison.OrdinalIgnoreCase));
                var overdueMilestoneCount = milestones.Count(x =>
                    x.ProjectId == project.Id
                    && x.TargetDate.Date < today
                    && !string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase));

                performance.TryGetValue(project.Id, out var analytics);
                var healthStatus = analytics?.HealthStatus ?? "OnTrack";
                var projectedVariance = analytics?.ProjectedVariance ?? 0m;
                var budgetBaseline = analytics?.BudgetBaseline ?? (project.ApprovedBudget ?? project.EstimatedBudget ?? 0m);
                var actualCost = analytics?.ActualCost ?? (project.ActualCost ?? 0m);

                var score = 100m;
                if (string.Equals(healthStatus, "Watch", StringComparison.OrdinalIgnoreCase)) score -= 20m;
                if (string.Equals(project.Status, "AtRisk", StringComparison.OrdinalIgnoreCase) || string.Equals(project.Status, "Delayed", StringComparison.OrdinalIgnoreCase)) score -= 20m;
                if (string.Equals(project.Status, ProjectStatuses.OnHold, StringComparison.OrdinalIgnoreCase)) score -= 12m;
                score -= Math.Min(24m, openRiskCount * 6m);
                score -= Math.Min(30m, openIssueCount * 8m);
                score -= Math.Min(18m, overdueMilestoneCount * 6m);
                if (projectedVariance < 0m) score -= 15m;
                score = Math.Max(0m, decimal.Round(score, 2));

                var priorityBand = score switch
                {
                    <= 45m => "Stabilize",
                    <= 70m => "Focus",
                    _ => "Maintain"
                };

                var recommendedAction = priorityBand switch
                {
                    "Stabilize" => "Escalate governance review, address blockers, and rebaseline near-term commitments.",
                    "Focus" => "Monitor delivery exceptions closely and close open risks, issues, and overdue milestones.",
                    _ => "Maintain current execution cadence and monitor for emerging exceptions."
                };

                return new ProjectPortfolioPrioritizationReportItemDto
                {
                    ProjectId = project.Id,
                    PortfolioId = project.PortfolioId,
                    ProgramId = project.ProgramId,
                    ProjectCode = project.ProjectCode,
                    ProjectTitle = project.Title,
                    Status = project.Status,
                    PortfolioName = project.Portfolio?.Name,
                    ProgramName = project.Program?.Name,
                    HealthStatus = healthStatus,
                    BudgetBaseline = budgetBaseline,
                    ActualCost = actualCost,
                    ProjectedVariance = projectedVariance,
                    OpenRiskCount = openRiskCount,
                    OpenIssueCount = openIssueCount,
                    OverdueMilestoneCount = overdueMilestoneCount,
                    PriorityScore = score,
                    PriorityBand = priorityBand,
                    RecommendedAction = recommendedAction
                };
            })
            .OrderBy(x => x.PriorityScore)
            .ThenByDescending(x => x.OverdueMilestoneCount)
            .ThenByDescending(x => x.OpenIssueCount)
            .Take(take)
            .ToList();
    }

    public async Task<IEnumerable<ProjectDependencyWatchReportItemDto>> GetDependencyWatchReportAsync(Guid? portfolioId = null, Guid? programId = null, int take = 100)
    {
        var projects = (await GetAccessibleProjectsAsync(take: Math.Max(take * 4, 300)))
            .Where(x => !portfolioId.HasValue || x.PortfolioId == portfolioId.Value)
            .Where(x => !programId.HasValue || x.ProgramId == programId.Value)
            .ToList();

        if (projects.Count == 0)
        {
            return Array.Empty<ProjectDependencyWatchReportItemDto>();
        }

        var projectById = projects.ToDictionary(x => x.Id);
        var dependencies = (await _unitOfWork.Repository<ProjectInterdependency>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && projectById.Keys.Contains(x.SourceProjectId)
                && projectById.Keys.Contains(x.TargetProjectId)))
            .ToList();
        var portfolioNames = (await _projectPortfolioRepository.GetAllAsync()).ToDictionary(x => x.Id, x => x.Name);
        var programNames = (await _projectProgramRepository.GetAllAsync()).ToDictionary(x => x.Id, x => x.Name);
        var today = DateTime.UtcNow.Date;

        return dependencies
            .Select(x =>
            {
                var sourceProject = projectById[x.SourceProjectId];
                var targetProject = projectById[x.TargetProjectId];
                var daysToDue = x.DueDate.HasValue ? (x.DueDate.Value.Date - today).Days : int.MaxValue;
                var coordinationState = string.Equals(x.Status, "Closed", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(x.Status, "Resolved", StringComparison.OrdinalIgnoreCase)
                    ? "Resolved"
                    : x.DueDate.HasValue && x.DueDate.Value.Date < today
                        ? "Overdue"
                        : string.Equals(x.ImpactLevel, "Critical", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(x.ImpactLevel, "High", StringComparison.OrdinalIgnoreCase)
                            ? "Watch"
                            : "Tracked";

                return new ProjectDependencyWatchReportItemDto
                {
                    InterdependencyId = x.Id,
                    SourceProjectId = sourceProject.Id,
                    TargetProjectId = targetProject.Id,
                    SourceProjectCode = sourceProject.ProjectCode,
                    SourceProjectTitle = sourceProject.Title,
                    TargetProjectCode = targetProject.ProjectCode,
                    TargetProjectTitle = targetProject.Title,
                    PortfolioId = sourceProject.PortfolioId,
                    PortfolioName = sourceProject.PortfolioId.HasValue && portfolioNames.TryGetValue(sourceProject.PortfolioId.Value, out var portfolioName) ? portfolioName : null,
                    ProgramId = sourceProject.ProgramId,
                    ProgramName = sourceProject.ProgramId.HasValue && programNames.TryGetValue(sourceProject.ProgramId.Value, out var programName) ? programName : null,
                    DependencyType = x.DependencyType,
                    Status = x.Status,
                    ImpactLevel = x.ImpactLevel,
                    Title = x.Title,
                    DueDate = x.DueDate,
                    DaysToDue = daysToDue == int.MaxValue ? 0 : daysToDue,
                    CoordinationState = coordinationState
                };
            })
            .OrderByDescending(x => x.CoordinationState == "Overdue")
            .ThenByDescending(x => string.Equals(x.ImpactLevel, "Critical", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(x => string.Equals(x.ImpactLevel, "High", StringComparison.OrdinalIgnoreCase))
            .ThenBy(x => x.DaysToDue)
            .ThenBy(x => x.SourceProjectCode)
            .Take(take)
            .ToList();
    }

    public async Task<IEnumerable<ProjectStrategicInitiativeReportItemDto>> GetStrategicInitiativeReportAsync(Guid? portfolioId = null, int take = 100)
    {
        var projects = (await GetAccessibleProjectsAsync(take: Math.Max(take * 5, 300)))
            .Where(x => !portfolioId.HasValue || x.PortfolioId == portfolioId.Value)
            .ToList();

        if (projects.Count == 0)
        {
            return Array.Empty<ProjectStrategicInitiativeReportItemDto>();
        }

        var projectIds = projects.Select(x => x.Id).ToHashSet();
        var risks = (await _unitOfWork.Repository<ProjectRisk>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId && projectIds.Contains(x.ProjectId))).ToList();
        var portfolioNames = (await _projectPortfolioRepository.GetAllAsync()).ToDictionary(x => x.Id, x => x.Name);
        var programNames = (await _projectProgramRepository.GetAllAsync()).ToDictionary(x => x.Id, x => x.Name);

        return projects
            .GroupBy(x => string.IsNullOrWhiteSpace(x.StrategicAlignment) ? "Unassigned" : x.StrategicAlignment!.Trim())
            .Select(group =>
            {
                var initiativeProjects = group.ToList();
                var initiativeProjectIds = initiativeProjects.Select(x => x.Id).ToHashSet();
                return new ProjectStrategicInitiativeReportItemDto
                {
                    Initiative = group.Key,
                    ProjectCount = initiativeProjects.Count,
                    ActiveProjectCount = initiativeProjects.Count(x => IsActiveProjectStatus(x.Status)),
                    AtRiskProjectCount = initiativeProjects.Count(x =>
                        string.Equals(x.Status, "AtRisk", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(x.Status, "Delayed", StringComparison.OrdinalIgnoreCase)),
                    DelayedProjectCount = initiativeProjects.Count(x => string.Equals(x.Status, "Delayed", StringComparison.OrdinalIgnoreCase)),
                    HighRiskItemCount = risks.Count(x =>
                        initiativeProjectIds.Contains(x.ProjectId)
                        && !string.Equals(x.Status, "Closed", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(x.Status, "Mitigated", StringComparison.OrdinalIgnoreCase)
                        && x.Exposure >= 12),
                    TotalEstimatedBudget = initiativeProjects.Sum(x => x.EstimatedBudget ?? 0m),
                    TotalActualCost = initiativeProjects.Sum(x => x.ActualCost ?? 0m),
                    AverageProgressPercent = initiativeProjects.Count == 0 ? 0m : decimal.Round(initiativeProjects.Average(x => x.ProgressPercent), 2),
                    PortfolioNames = initiativeProjects
                        .Where(x => x.PortfolioId.HasValue && portfolioNames.ContainsKey(x.PortfolioId.Value))
                        .Select(x => portfolioNames[x.PortfolioId!.Value])
                        .Distinct()
                        .OrderBy(x => x)
                        .ToList(),
                    ProgramNames = initiativeProjects
                        .Where(x => x.ProgramId.HasValue && programNames.ContainsKey(x.ProgramId.Value))
                        .Select(x => programNames[x.ProgramId!.Value])
                        .Distinct()
                        .OrderBy(x => x)
                        .ToList()
                };
            })
            .OrderByDescending(x => x.ProjectCount)
            .ThenByDescending(x => x.TotalEstimatedBudget)
            .Take(take)
            .ToList();
    }

    public async Task<IEnumerable<ProjectMaterialReconciliationReportItemDto>> GetMaterialReconciliationReportAsync(int take = 200, string? reconciliationStatus = null)
    {
        var projects = (await GetAccessibleProjectsAsync(take: Math.Max(take * 4, 300))).ToList();
        if (projects.Count == 0)
        {
            return Array.Empty<ProjectMaterialReconciliationReportItemDto>();
        }

        var projectIds = projects.Select(x => x.Id).ToHashSet();
        await SyncProjectMaterialCostsAsync(projectIds);
        var inventoryRequisitions = (await _unitOfWork.Repository<InventoryRequisition>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && x.ProjectId.HasValue
                && projectIds.Contains(x.ProjectId.Value),
                x => x.Items))
            .ToList();
        var ledgerEntries = (await GetMaterialCostEntryEntitiesAsync(projectIds))
            .ToList();
        var ledgerEntriesByProjectId = ledgerEntries
            .GroupBy(x => x.ProjectId)
            .ToDictionary(x => x.Key, x => x.ToList());

        var report = projects
            .Select(project =>
            {
                var projectRequisitions = inventoryRequisitions.Where(x => x.ProjectId == project.Id).ToList();
                var projectLedgerEntries = ledgerEntriesByProjectId.TryGetValue(project.Id, out var entries)
                    ? entries
                    : new List<ProjectMaterialCostEntry>();
                var requestedValue = projectRequisitions.Sum(x => x.TotalValue);
                var issuedValue = projectLedgerEntries
                    .Where(x => string.Equals(x.EntryType, "InventoryIssue", StringComparison.OrdinalIgnoreCase))
                    .Sum(x => Math.Abs(x.Amount));
                var returnedValue = projectLedgerEntries
                    .Where(x => string.Equals(x.EntryType, "InventoryReturn", StringComparison.OrdinalIgnoreCase))
                    .Sum(x => Math.Abs(x.Amount));
                var netIssuedValue = issuedValue - returnedValue;
                var trackedMaterialCost = projectLedgerEntries
                    .Where(x => x.AffectsActualCost)
                    .Sum(x => x.Amount);
                var materialCostVariance = decimal.Round(netIssuedValue - trackedMaterialCost, 2);
                var pendingRequisitionCount = projectRequisitions.Count(x => x.Status == RequisitionStatus.Submitted || x.Status == RequisitionStatus.Approved);
                var missingSourceLinkCount = projectLedgerEntries.Count(x => x.HasMissingSourceLink);
                var reversalGapCount = projectLedgerEntries.Count(x => x.HasReversalGap);
                var reconciliationState = materialCostVariance switch
                {
                    > 0.01m => "UnderTracked",
                    < -0.01m => "OverTracked",
                    _ => "Balanced"
                };
                if (missingSourceLinkCount > 0 || reversalGapCount > 0)
                {
                    reconciliationState = "Exception";
                }

                return new ProjectMaterialReconciliationReportItemDto
                {
                    ProjectId = project.Id,
                    ProjectCode = project.ProjectCode,
                    ProjectTitle = project.Title,
                    Status = project.Status,
                    RequisitionCount = projectRequisitions.Count,
                    PendingRequisitionCount = pendingRequisitionCount,
                    IssuedRequisitionCount = projectRequisitions.Count(x => x.Status == RequisitionStatus.PartiallyIssued || x.Status == RequisitionStatus.Issued || x.Status == RequisitionStatus.Completed),
                    RequestedValue = requestedValue,
                    IssuedValue = issuedValue,
                    ReturnedValue = returnedValue,
                    NetIssuedValue = netIssuedValue,
                    TrackedMaterialCost = trackedMaterialCost,
                    MaterialCostVariance = materialCostVariance,
                    MaterialLedgerEntryCount = projectLedgerEntries.Count,
                    MissingSourceLinkCount = missingSourceLinkCount,
                    ReversalGapCount = reversalGapCount,
                    ReconciliationStatus = reconciliationState
                };
            })
            .Where(x => string.IsNullOrWhiteSpace(reconciliationStatus) || string.Equals(x.ReconciliationStatus, reconciliationStatus, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => Math.Abs(x.MaterialCostVariance))
            .ThenByDescending(x => x.PendingRequisitionCount)
            .ThenBy(x => x.ProjectCode)
            .Take(take)
            .ToList();

        return report;
    }

    public async Task<IEnumerable<ProjectProcurementReconciliationReportItemDto>> GetProcurementReconciliationReportAsync(int take = 200, string? reconciliationStatus = null)
    {
        var projects = (await GetAccessibleProjectsAsync(take: Math.Max(take * 4, 300))).ToList();
        if (projects.Count == 0)
        {
            return Array.Empty<ProjectProcurementReconciliationReportItemDto>();
        }

        var projectIds = projects.Select(x => x.Id).ToHashSet();
        await SyncProjectMaterialCostsAsync(projectIds);
        var purchaseRequisitions = (await _unitOfWork.Repository<PurchaseRequisition>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && x.ProjectId.HasValue
                && projectIds.Contains(x.ProjectId.Value)))
            .ToList();
        var purchaseRequisitionIds = purchaseRequisitions.Select(x => x.Id).ToHashSet();
        var purchaseOrders = purchaseRequisitionIds.Count == 0
            ? new List<PurchaseOrder>()
            : (await _unitOfWork.Repository<PurchaseOrder>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && x.SourceRequisitionId.HasValue
                    && purchaseRequisitionIds.Contains(x.SourceRequisitionId.Value)))
                .ToList();
        var purchaseOrderIds = purchaseOrders.Select(x => x.Id).ToHashSet();
        var purchaseOrderItems = purchaseOrderIds.Count == 0
            ? new List<PurchaseOrderItem>()
            : (await _unitOfWork.Repository<PurchaseOrderItem>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && purchaseOrderIds.Contains(x.PurchaseOrderId)))
                .ToList();
        var purchaseReceipts = purchaseOrderIds.Count == 0
            ? new List<PurchaseOrderReceipt>()
            : (await _unitOfWork.Repository<PurchaseOrderReceipt>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && purchaseOrderIds.Contains(x.PurchaseOrderId)))
                .ToList();
        var purchaseReceiptIds = purchaseReceipts.Select(x => x.Id).ToHashSet();
        var purchaseReceiptItems = purchaseReceiptIds.Count == 0
            ? new List<PurchaseOrderReceiptItem>()
            : (await _unitOfWork.Repository<PurchaseOrderReceiptItem>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && purchaseReceiptIds.Contains(x.ReceiptId)))
                .ToList();
        var inventoryRequisitions = (await _unitOfWork.Repository<InventoryRequisition>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && x.ProjectId.HasValue
                && projectIds.Contains(x.ProjectId.Value)))
            .ToList();
        var ledgerEntries = await GetMaterialCostEntryEntitiesAsync(projectIds);

        var purchaseOrderItemsById = purchaseOrderItems.ToDictionary(x => x.Id, x => x);
        var purchaseRequisitionsById = purchaseRequisitions.ToDictionary(x => x.Id, x => x);
        var purchaseOrdersById = purchaseOrders.ToDictionary(x => x.Id, x => x);
        var purchaseOrdersByProjectId = purchaseOrders
            .Where(x => x.SourceRequisitionId.HasValue && purchaseRequisitionsById.ContainsKey(x.SourceRequisitionId.Value))
            .GroupBy(x => purchaseRequisitionsById[x.SourceRequisitionId!.Value].ProjectId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());
        var purchaseReceiptsByProjectId = purchaseReceipts
            .Where(x =>
            {
                var order = purchaseOrdersById.GetValueOrDefault(x.PurchaseOrderId);
                return order?.SourceRequisitionId.HasValue == true
                    && purchaseRequisitionsById.ContainsKey(order.SourceRequisitionId.Value)
                    && purchaseRequisitionsById[order.SourceRequisitionId.Value].ProjectId.HasValue;
            })
            .GroupBy(x =>
            {
                var order = purchaseOrdersById[x.PurchaseOrderId];
                return purchaseRequisitionsById[order.SourceRequisitionId!.Value].ProjectId!.Value;
            })
            .ToDictionary(x => x.Key, x => x.ToList());
        var purchaseReceiptItemsByReceiptId = purchaseReceiptItems
            .GroupBy(x => x.ReceiptId)
            .ToDictionary(x => x.Key, x => x.ToList());
        var inventoryRequisitionsByProjectId = inventoryRequisitions
            .Where(x => x.ProjectId.HasValue)
            .GroupBy(x => x.ProjectId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());
        var ledgerEntriesByProjectId = ledgerEntries
            .GroupBy(x => x.ProjectId)
            .ToDictionary(x => x.Key, x => x.ToList());

        var report = projects
            .Select(project =>
            {
                var projectPurchaseRequisitions = purchaseRequisitions.Where(x => x.ProjectId == project.Id).ToList();
                var projectPurchaseOrders = purchaseOrdersByProjectId.TryGetValue(project.Id, out var poList) ? poList : new List<PurchaseOrder>();
                var projectPurchaseReceipts = purchaseReceiptsByProjectId.TryGetValue(project.Id, out var receiptList) ? receiptList : new List<PurchaseOrderReceipt>();
                var projectReceiptItems = projectPurchaseReceipts
                    .SelectMany(x => purchaseReceiptItemsByReceiptId.TryGetValue(x.Id, out var items) ? items : Enumerable.Empty<PurchaseOrderReceiptItem>())
                    .ToList();
                var projectInventoryRequisitions = inventoryRequisitionsByProjectId.TryGetValue(project.Id, out var requisitionList)
                    ? requisitionList
                    : new List<InventoryRequisition>();
                var projectLedgerEntries = ledgerEntriesByProjectId.TryGetValue(project.Id, out var ledgerList)
                    ? ledgerList
                    : new List<ProjectMaterialCostEntry>();
                var projectPurchaseReceiptsById = projectPurchaseReceipts.ToDictionary(x => x.Id, x => x);

                var receivedAmount = decimal.Round(projectReceiptItems.Sum(receiptItem =>
                {
                    var receipt = projectPurchaseReceiptsById.GetValueOrDefault(receiptItem.ReceiptId);
                    if (receipt == null || !purchaseOrderItemsById.TryGetValue(receiptItem.PurchaseOrderItemId, out var orderItem))
                    {
                        return 0m;
                    }

                    var valuedQuantity = ResolveReceiptReceivedQuantity(receipt, receiptItem);
                    return decimal.Round(ResolvePurchaseOrderItemUnitCost(orderItem) * valuedQuantity, 2);
                }), 2);

                var acceptedReceiptAmount = decimal.Round(projectLedgerEntries
                    .Where(x => string.Equals(x.EntryType, "PurchaseReceiptAccepted", StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.Amount), 2);
                var pendingInspectionAmount = decimal.Round(projectLedgerEntries
                    .Where(x => string.Equals(x.EntryType, "PurchaseReceiptPendingInspection", StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.Amount), 2);
                var supplierReturnAmount = decimal.Round(projectLedgerEntries
                    .Where(x => string.Equals(x.EntryType, "PurchaseReturn", StringComparison.OrdinalIgnoreCase))
                    .Sum(x => Math.Abs(x.Amount)), 2);
                var issuedInventoryValue = decimal.Round(projectLedgerEntries
                    .Where(x => string.Equals(x.EntryType, "InventoryIssue", StringComparison.OrdinalIgnoreCase))
                    .Sum(x => Math.Abs(x.Amount)), 2);
                var returnedInventoryValue = decimal.Round(projectLedgerEntries
                    .Where(x => string.Equals(x.EntryType, "InventoryReturn", StringComparison.OrdinalIgnoreCase))
                    .Sum(x => Math.Abs(x.Amount)), 2);
                var netIssuedInventoryValue = decimal.Round(issuedInventoryValue - returnedInventoryValue, 2);
                var postedMaterialCost = decimal.Round(projectLedgerEntries
                    .Where(x => x.AffectsActualCost)
                    .Sum(x => x.Amount), 2);
                var receiptToIssueVariance = decimal.Round((acceptedReceiptAmount - supplierReturnAmount) - netIssuedInventoryValue, 2);
                var issueToPostingVariance = decimal.Round(netIssuedInventoryValue - postedMaterialCost, 2);
                var missingSourceLinkCount = projectLedgerEntries.Count(x => x.HasMissingSourceLink);
                var reversalGapCount = projectLedgerEntries.Count(x => x.HasReversalGap);

                var state = pendingInspectionAmount > 0.01m
                    ? "PendingInspection"
                    : Math.Abs(receiptToIssueVariance) <= 0.01m && Math.Abs(issueToPostingVariance) <= 0.01m
                        ? "Balanced"
                        : receiptToIssueVariance > 0.01m
                            ? "ReceiptAheadOfIssue"
                            : receiptToIssueVariance < -0.01m
                                ? "IssueAheadOfReceipt"
                                : issueToPostingVariance > 0.01m
                                    ? "IssueAheadOfPosting"
                                    : "PostingAheadOfIssue";
                if (missingSourceLinkCount > 0 || reversalGapCount > 0)
                {
                    state = "Exception";
                }

                return new ProjectProcurementReconciliationReportItemDto
                {
                    ProjectId = project.Id,
                    ProjectCode = project.ProjectCode,
                    ProjectTitle = project.Title,
                    Status = project.Status,
                    PurchaseRequisitionCount = projectPurchaseRequisitions.Count,
                    OpenPurchaseRequisitionCount = projectPurchaseRequisitions.Count(IsPendingPurchaseRequisitionStatus),
                    PurchaseRequisitionAmount = projectPurchaseRequisitions.Sum(x => x.TotalAmount),
                    PurchaseOrderCount = projectPurchaseOrders.Count,
                    OpenPurchaseOrderCount = projectPurchaseOrders.Count(IsOpenPurchaseOrderStatus),
                    PurchaseOrderAmount = projectPurchaseOrders.Where(x => !string.Equals(x.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)).Sum(x => x.TotalAmount),
                    PurchaseReceiptCount = projectPurchaseReceipts.Count,
                    ReceivedAmount = receivedAmount,
                    AcceptedReceiptAmount = acceptedReceiptAmount,
                    PendingInspectionAmount = pendingInspectionAmount,
                    SupplierReturnAmount = supplierReturnAmount,
                    IssuedInventoryValue = issuedInventoryValue,
                    NetIssuedInventoryValue = netIssuedInventoryValue,
                    PostedMaterialCost = postedMaterialCost,
                    ReceiptToIssueVariance = receiptToIssueVariance,
                    IssueToPostingVariance = issueToPostingVariance,
                    ProcurementLedgerEntryCount = projectLedgerEntries.Count,
                    MissingSourceLinkCount = missingSourceLinkCount,
                    ReversalGapCount = reversalGapCount,
                    ReconciliationStatus = state
                };
            })
            .Where(x => string.IsNullOrWhiteSpace(reconciliationStatus) || string.Equals(x.ReconciliationStatus, reconciliationStatus, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.PendingInspectionAmount)
            .ThenByDescending(x => Math.Abs(x.ReceiptToIssueVariance))
            .ThenByDescending(x => Math.Abs(x.IssueToPostingVariance))
            .ThenBy(x => x.ProjectCode)
            .Take(take)
            .ToList();

        return report;
    }

    public async Task<IEnumerable<ProjectResourceCapacityReportItemDto>> GetResourceCapacityReportAsync(DateTime? startDate = null, DateTime? endDate = null, Guid? userId = null)
    {
        var periodStart = startDate ?? DateTime.UtcNow.Date;
        var periodEnd = endDate ?? periodStart.AddDays(30);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var accessibleProjectIds = (await GetAccessibleProjectsAsync(take: 2000)).Select(x => x.Id).ToHashSet();
        var allocations = (await _unitOfWork.Repository<ProjectResourceAllocation>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && (!userId.HasValue || x.UserId == userId.Value)
                && x.StartDate <= periodEnd
                && x.EndDate >= periodStart))
            .Where(x => accessibleProjectIds.Contains(x.ProjectId))
            .Where(IsAllocationActiveForCapacity)
            .ToList();
        var resourceIds = allocations.Select(x => x.UserId).Distinct().ToList();
        var employeeDisplayNames = await GetEmployeeDisplayNamesAsync(resourceIds);
        var leaveMetrics = await GetApprovedLeaveMetricsAsync(resourceIds, periodStart, periodEnd);
        var qualificationLookup = BuildQualificationMetricsLookup(
            (await _unitOfWork.Repository<EmployeeSkill>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && x.IsVerified
                    && resourceIds.Contains(x.EmployeeId)))
                .ToList(),
            today);
        var standardCapacityHours = GetCapacityHours(periodStart, periodEnd);

        return allocations
            .GroupBy(x => x.UserId)
            .Select(group =>
            {
                var allocationList = group.ToList();
                var allocatedHours = allocationList.Sum(GetEffectiveHours);
                var allocatedPercent = allocationList.Sum(x => string.Equals(x.AllocationType, "Percent", StringComparison.OrdinalIgnoreCase) ? x.AllocationValue : 0m);
                var leave = leaveMetrics.GetValueOrDefault(group.Key, ResourceLeaveMetrics.Empty);
                var qualification = qualificationLookup.GetValueOrDefault(group.Key, ResourceQualificationMetrics.Empty);
                var effectiveCapacityHours = Math.Max(standardCapacityHours - leave.ApprovedLeaveHours, 0m);
                var capacityPercent = Math.Max(
                    allocatedPercent,
                    GetCapacityUtilizationPercent(allocatedHours, effectiveCapacityHours));

                return new ProjectResourceCapacityReportItemDto
                {
                    UserId = group.Key,
                    UserDisplayName = employeeDisplayNames.GetValueOrDefault(group.Key),
                    AllocationCount = allocationList.Count,
                    TotalAllocatedHours = decimal.Round(allocatedHours, 2),
                    TotalAllocatedPercent = decimal.Round(allocatedPercent, 2),
                    StandardCapacityHours = decimal.Round(standardCapacityHours, 2),
                    EffectiveCapacityHours = decimal.Round(effectiveCapacityHours, 2),
                    ApprovedLeaveHours = decimal.Round(leave.ApprovedLeaveHours, 2),
                    ApprovedLeaveDays = decimal.Round(leave.ApprovedLeaveDays, 2),
                    LeaveRequestCount = leave.LeaveRequestCount,
                    CapacityUtilizationPercent = decimal.Round(capacityPercent, 2),
                    ConflictCount = allocationList.Count(x => HasAllocationConflict(x, allocationList)),
                    VerifiedSkillCount = qualification.VerifiedSkillCount,
                    CertifiedSkillCount = qualification.CertifiedSkillCount,
                    ExpiringCertificationCount = qualification.ExpiringCertificationCount,
                    ExpiredCertificationCount = qualification.ExpiredCertificationCount,
                    QualificationRisk = qualification.QualificationRisk,
                    AllocationIds = allocationList.Select(x => x.Id).ToList()
                };
            })
            .OrderByDescending(x => x.CapacityUtilizationPercent)
            .ToList();
    }

    public async Task<IEnumerable<ProjectResourceCapacityRecommendationDto>> GetResourceCapacityRecommendationsAsync(DateTime? startDate = null, DateTime? endDate = null, Guid? userId = null)
    {
        var periodStart = startDate ?? DateTime.UtcNow.Date;
        var periodEnd = endDate ?? periodStart.AddDays(30);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var accessibleProjectIds = (await GetAccessibleProjectsAsync(take: 2000)).Select(x => x.Id).ToHashSet();
        var allocations = (await _unitOfWork.Repository<ProjectResourceAllocation>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && (!userId.HasValue || x.UserId == userId.Value)
                && x.StartDate <= periodEnd
                && x.EndDate >= periodStart))
            .Where(x => accessibleProjectIds.Contains(x.ProjectId))
            .Where(IsAllocationActiveForCapacity)
            .ToList();

        var projects = (await GetAccessibleProjectsAsync(take: 1000)).ToDictionary(x => x.Id);
        var resourceIds = allocations.Select(x => x.UserId).Distinct().ToList();
        var employeeDisplayNames = await GetEmployeeDisplayNamesAsync(resourceIds);
        var leaveMetrics = await GetApprovedLeaveMetricsAsync(resourceIds, periodStart, periodEnd);
        var qualificationLookup = BuildQualificationMetricsLookup(
            (await _unitOfWork.Repository<EmployeeSkill>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && x.IsVerified
                    && resourceIds.Contains(x.EmployeeId)))
                .ToList(),
            today);
        var standardCapacityHours = GetCapacityHours(periodStart, periodEnd);
        var optimizationSuggestions = (await GetResourceOptimizationSuggestionsAsync(periodStart, periodEnd)).ToDictionary(x => x.UserId);
        return allocations
            .GroupBy(x => x.UserId)
            .Select(group =>
            {
                var allocationList = group.ToList();
                var allocatedHours = allocationList.Sum(GetEffectiveHours);
                var allocatedPercent = allocationList.Sum(x => string.Equals(x.AllocationType, "Percent", StringComparison.OrdinalIgnoreCase) ? x.AllocationValue : 0m);
                var leave = leaveMetrics.GetValueOrDefault(group.Key, ResourceLeaveMetrics.Empty);
                var qualification = qualificationLookup.GetValueOrDefault(group.Key, ResourceQualificationMetrics.Empty);
                var effectiveCapacityHours = Math.Max(standardCapacityHours - leave.ApprovedLeaveHours, 0m);
                var capacityPercent = Math.Max(
                    allocatedPercent,
                    GetCapacityUtilizationPercent(allocatedHours, effectiveCapacityHours));
                var conflictCount = allocationList.Count(x => HasAllocationConflict(x, allocationList));
                var overagePercent = Math.Max(0m, capacityPercent - 100m);
                var overageHours = Math.Max(allocatedHours - effectiveCapacityHours, 0m);
                var severity = overagePercent > 20m || conflictCount >= 2
                    ? "Critical"
                    : overagePercent > 0m || conflictCount > 0
                        ? "High"
                        : capacityPercent < 40m
                            ? "Low"
                            : "Balanced";
                if (qualification.ExpiredCertificationCount > 0)
                {
                    severity = MaxSeverity(severity, "Critical");
                }
                else if (qualification.ExpiringCertificationCount > 0)
                {
                    severity = MaxSeverity(severity, "High");
                }
                var recommendation = severity switch
                {
                    "Critical" => leave.LeaveRequestCount > 0
                        ? "Approved leave is reducing available capacity. Reassign overlapping work or cover the leave window."
                        : "Reassign overlapping allocations or reduce booked hours this period.",
                    "High" => leave.LeaveRequestCount > 0
                        ? "Review the leave window and shift part of the booked work to maintain coverage."
                        : "Review overlapping work and shift a portion of the allocation window.",
                    "Low" => "Resource has spare capacity and can absorb planned work.",
                    _ => "Current load is acceptable. Monitor for new conflicts."
                };
                optimizationSuggestions.TryGetValue(group.Key, out var optimization);
                var finalRecommendation = optimization?.Recommendation ?? recommendation;
                if (leave.LeaveRequestCount > 0 && !finalRecommendation.Contains("leave", StringComparison.OrdinalIgnoreCase))
                {
                    finalRecommendation = $"{finalRecommendation} Approved leave is reducing the available capacity in this window.";
                }
                if (qualification.ExpiredCertificationCount > 0 && !finalRecommendation.Contains("expired certification", StringComparison.OrdinalIgnoreCase))
                {
                    finalRecommendation = $"{finalRecommendation} Expired certifications are increasing delivery risk for this resource.";
                }
                else if (qualification.ExpiringCertificationCount > 0 && !finalRecommendation.Contains("expire", StringComparison.OrdinalIgnoreCase))
                {
                    finalRecommendation = $"{finalRecommendation} One or more certifications are expiring soon and should be renewed or covered.";
                }

                return new ProjectResourceCapacityRecommendationDto
                {
                    UserId = group.Key,
                    UserDisplayName = employeeDisplayNames.GetValueOrDefault(group.Key),
                    CapacityUtilizationPercent = decimal.Round(capacityPercent, 2),
                    EffectiveCapacityHours = decimal.Round(effectiveCapacityHours, 2),
                    ApprovedLeaveHours = decimal.Round(leave.ApprovedLeaveHours, 2),
                    ApprovedLeaveDays = decimal.Round(leave.ApprovedLeaveDays, 2),
                    LeaveRequestCount = leave.LeaveRequestCount,
                    ConflictCount = conflictCount,
                    VerifiedSkillCount = qualification.VerifiedSkillCount,
                    CertifiedSkillCount = qualification.CertifiedSkillCount,
                    ExpiringCertificationCount = qualification.ExpiringCertificationCount,
                    ExpiredCertificationCount = qualification.ExpiredCertificationCount,
                    QualificationRisk = qualification.QualificationRisk,
                    Severity = severity,
                    Recommendation = finalRecommendation,
                    SuggestedReductionHours = overageHours <= 0m
                        ? 0m
                        : decimal.Round(overageHours, 2),
                    ProjectCodes = allocationList
                        .Select(x => projects.TryGetValue(x.ProjectId, out var project) ? project.ProjectCode : null)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct()
                        .OrderBy(x => x)
                        .Cast<string>()
                        .ToList(),
                    SuggestedReplacementUserId = optimization?.SuggestedReplacementUserId,
                    SuggestedReplacementUserDisplayName = optimization?.SuggestedReplacementUserDisplayName,
                    MatchedSkills = optimization?.MatchedSkills ?? new List<string>(),
                    AffectedAllocationIds = optimization?.AffectedAllocationIds ?? allocationList.Where(x => HasAllocationConflict(x, allocationList)).Select(x => x.Id).ToList()
                };
            })
            .Where(x => x.Severity != "Balanced" || x.ConflictCount > 0)
            .OrderByDescending(x => x.Severity == "Critical")
            .ThenByDescending(x => x.CapacityUtilizationPercent)
            .ThenByDescending(x => x.ConflictCount)
            .ToList();
    }

    public async Task<IEnumerable<ProjectBillingSummaryReportItemDto>> GetBillingSummaryReportAsync(int take = 200)
    {
        var projects = (await GetAccessibleProjectsAsync(take: take)).ToList();
        var projectIds = projects.Select(x => x.Id).ToList();
        var schedules = await _unitOfWork.Repository<ProjectBillingSchedule>().FindAsync(x => projectIds.Contains(x.ProjectId));
        var requests = await _unitOfWork.Repository<ProjectInvoiceRequest>().FindAsync(x => projectIds.Contains(x.ProjectId));
        var revenueRecognitions = await _unitOfWork.Repository<ProjectRevenueRecognition>().FindAsync(x => projectIds.Contains(x.ProjectId));
        var today = DateTime.UtcNow.Date;

        return projects.Select(x =>
        {
            var projectSchedules = schedules.Where(s => s.ProjectId == x.Id && s.IsBillable).ToList();
            var projectRequests = requests.Where(r => r.ProjectId == x.Id).ToList();
            var scheduledBillingAmount = projectSchedules.Sum(s => s.Amount);
            var invoiceRequestedAmount = projectRequests.Sum(r => r.RequestedAmount);
            var unbilledAmount = Math.Max(scheduledBillingAmount - invoiceRequestedAmount, 0m);
            var readyBillingScheduleCount = projectSchedules.Count(s =>
                !string.Equals(s.Status, "Invoiced", StringComparison.OrdinalIgnoreCase)
                && s.BillingDate.Date <= today);
            var overdueBillingScheduleCount = projectSchedules.Count(s =>
                !string.Equals(s.Status, "Invoiced", StringComparison.OrdinalIgnoreCase)
                && s.BillingDate.Date < today);
            var draftInvoiceRequestCount = projectRequests.Count(r => string.Equals(r.Status, "Draft", StringComparison.OrdinalIgnoreCase));
            var submittedInvoiceRequestCount = projectRequests.Count(r =>
                string.Equals(r.Status, "Submitted", StringComparison.OrdinalIgnoreCase)
                || string.Equals(r.Status, "Requested", StringComparison.OrdinalIgnoreCase));
            var sentToFinanceInvoiceRequestCount = projectRequests.Count(r => string.Equals(r.Status, "SentToFinance", StringComparison.OrdinalIgnoreCase));
            var invoicedInvoiceRequestCount = projectRequests.Count(r => string.Equals(r.Status, "Invoiced", StringComparison.OrdinalIgnoreCase));
            var paidInvoiceRequestCount = projectRequests.Count(r => string.Equals(r.Status, "Paid", StringComparison.OrdinalIgnoreCase));
            var collectedCashAmount = projectRequests
                .Where(r => string.Equals(r.Status, "Paid", StringComparison.OrdinalIgnoreCase))
                .Sum(r => r.RequestedAmount);
            var recognizedRevenue = revenueRecognitions
                .Where(r => r.ProjectId == x.Id && !string.Equals(r.Status, "Reversed", StringComparison.OrdinalIgnoreCase))
                .Sum(r => r.RecognizedRevenue);
            var actualCost = x.ActualCost ?? 0m;
            var marginAmount = invoiceRequestedAmount - actualCost;
            var marginPercent = invoiceRequestedAmount <= 0m ? 0m : decimal.Round((marginAmount / invoiceRequestedAmount) * 100m, 2);
            var billingCoveragePercent = scheduledBillingAmount <= 0m
                ? 0m
                : decimal.Round((invoiceRequestedAmount / scheduledBillingAmount) * 100m, 2);
            var revenueCoveragePercent = invoiceRequestedAmount <= 0m
                ? 0m
                : decimal.Round((recognizedRevenue / invoiceRequestedAmount) * 100m, 2);
            var revenueGapAmount = Math.Max(invoiceRequestedAmount - recognizedRevenue, 0m);

            return new ProjectBillingSummaryReportItemDto
            {
                ProjectId = x.Id,
                ProjectCode = x.ProjectCode,
                ProjectTitle = x.Title,
                ContractId = x.ContractId,
                ReadyBillingScheduleCount = readyBillingScheduleCount,
                OverdueBillingScheduleCount = overdueBillingScheduleCount,
                DraftInvoiceRequestCount = draftInvoiceRequestCount,
                SubmittedInvoiceRequestCount = submittedInvoiceRequestCount,
                SentToFinanceInvoiceRequestCount = sentToFinanceInvoiceRequestCount,
                InvoicedInvoiceRequestCount = invoicedInvoiceRequestCount,
                PaidInvoiceRequestCount = paidInvoiceRequestCount,
                ScheduledBillingAmount = scheduledBillingAmount,
                InvoiceRequestedAmount = invoiceRequestedAmount,
                CollectedCashAmount = collectedCashAmount,
                UnbilledAmount = unbilledAmount,
                BillingCoveragePercent = billingCoveragePercent,
                RecognizedRevenue = recognizedRevenue,
                RevenueCoveragePercent = revenueCoveragePercent,
                RevenueGapAmount = revenueGapAmount,
                ActualCost = actualCost,
                MarginAmount = marginAmount,
                MarginPercent = marginPercent
            };
        }).ToList();
    }

    public async Task<IEnumerable<ProjectInvoiceRequestQueueItemDto>> GetInvoiceRequestQueueReportAsync(int take = 200, string? status = null)
    {
        var projects = (await GetAccessibleProjectsAsync(take: take)).ToList();
        var projectIds = projects.Select(x => x.Id).ToList();
        if (!projectIds.Any())
        {
            return Array.Empty<ProjectInvoiceRequestQueueItemDto>();
        }

        var requests = (await _unitOfWork.Repository<ProjectInvoiceRequest>().FindAsync(x => projectIds.Contains(x.ProjectId))).ToList();
        var scheduleIds = requests.Where(x => x.BillingScheduleId.HasValue).Select(x => x.BillingScheduleId!.Value).Distinct().ToList();
        var schedules = scheduleIds.Any()
            ? (await _unitOfWork.Repository<ProjectBillingSchedule>().FindAsync(x => scheduleIds.Contains(x.Id))).ToList()
            : new List<ProjectBillingSchedule>();
        var projectLookup = projects.ToDictionary(x => x.Id);
        var scheduleLookup = schedules.ToDictionary(x => x.Id);

        if (!string.IsNullOrWhiteSpace(status))
        {
            requests = requests
                .Where(x => string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var now = DateTime.UtcNow;

        return requests
            .Select(request =>
            {
                var project = projectLookup[request.ProjectId];
                var schedule = request.BillingScheduleId.HasValue
                    && scheduleLookup.TryGetValue(request.BillingScheduleId.Value, out var billingSchedule)
                        ? billingSchedule
                    : null;
                var queueStage = ResolveInvoiceRequestQueueStage(request.Status);
                var referenceDate = request.SubmittedAt ?? request.RequestedAt;

                return new ProjectInvoiceRequestQueueItemDto
                {
                    ProjectId = project.Id,
                    ProjectCode = project.ProjectCode,
                    ProjectTitle = project.Title,
                    InvoiceRequestId = request.Id,
                    RequestNumber = request.RequestNumber,
                    BillingScheduleId = request.BillingScheduleId,
                    BillingScheduleName = schedule?.Name,
                    BillingDate = schedule?.BillingDate,
                    ContractId = request.ContractId ?? project.ContractId,
                    Status = request.Status,
                    RequestedAmount = request.RequestedAmount,
                    Currency = request.Currency,
                    RequestedAt = request.RequestedAt,
                    SubmittedAt = request.SubmittedAt,
                ExternalReference = request.ExternalReference,
                Notes = request.Notes,
                DaysOutstanding = Math.Max((now.Date - referenceDate.Date).Days, 0),
                QueueStage = queueStage,
                CanMarkInvoiced = string.Equals(request.Status, "Submitted", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(request.Status, "SentToFinance", StringComparison.OrdinalIgnoreCase),
                CanMarkPaid = string.Equals(request.Status, "Invoiced", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(request.Status, "SentToFinance", StringComparison.OrdinalIgnoreCase)
            };
        })
            .OrderBy(x => GetInvoiceRequestQueuePriority(x.Status))
            .ThenByDescending(x => x.DaysOutstanding)
            .ThenByDescending(x => x.RequestedAt)
            .Take(take)
            .ToList();
    }

    public async Task<IEnumerable<ProjectWorkflowApprovalQueueItemDto>> GetWorkflowApprovalQueueReportAsync(int take = 200, string? entityType = null)
    {
        var projects = (await GetAccessibleProjectsAsync(take: take)).ToList();
        var projectIds = projects.Select(x => x.Id).ToList();
        if (!projectIds.Any())
        {
            return Array.Empty<ProjectWorkflowApprovalQueueItemDto>();
        }

        var includeProjects = string.IsNullOrWhiteSpace(entityType) || string.Equals(entityType, "Project", StringComparison.OrdinalIgnoreCase);
        var includeBudgetRevisions = string.IsNullOrWhiteSpace(entityType) || string.Equals(entityType, "ProjectBudgetRevision", StringComparison.OrdinalIgnoreCase);
        var includeDeliverables = string.IsNullOrWhiteSpace(entityType) || string.Equals(entityType, "ProjectDeliverable", StringComparison.OrdinalIgnoreCase);
        var includeClosures = string.IsNullOrWhiteSpace(entityType) || string.Equals(entityType, "ProjectClosure", StringComparison.OrdinalIgnoreCase);

        var budgetRevisions = includeBudgetRevisions
            ? (await _unitOfWork.Repository<ProjectBudgetRevision>().FindAsync(x => projectIds.Contains(x.ProjectId))).ToList()
            : new List<ProjectBudgetRevision>();
        var deliverables = includeDeliverables
            ? (await _unitOfWork.Repository<ProjectDeliverable>().FindAsync(x => projectIds.Contains(x.ProjectId))).ToList()
            : new List<ProjectDeliverable>();
        var closures = includeClosures
            ? (await _unitOfWork.Repository<ProjectClosure>().FindAsync(x => projectIds.Contains(x.ProjectId))).ToList()
            : new List<ProjectClosure>();

        var now = DateTime.UtcNow.Date;
        var projectLookup = projects.ToDictionary(x => x.Id);
        var items = new List<ProjectWorkflowApprovalQueueItemDto>();

        if (includeProjects)
        {
            items.AddRange(projects
                .Where(x => IsWorkflowApprovalQueueStatus(x.Status))
                .Select(x => new ProjectWorkflowApprovalQueueItemDto
                {
                    EntityType = "Project",
                    EntityId = x.Id,
                    ProjectId = x.Id,
                    ProjectCode = x.ProjectCode,
                    ProjectTitle = x.Title,
                    ItemTitle = x.Title,
                    Status = x.Status,
                    SubmittedAt = x.SubmittedAt,
                    ApprovedAt = x.ApprovedAt,
                    DaysPending = CalculateWorkflowQueueDays(now, x.SubmittedAt, x.ApprovedAt, x.Status),
                    QueueStage = ResolveWorkflowApprovalQueueStage(x.Status)
                }));
        }

        items.AddRange(budgetRevisions
            .Where(x => IsWorkflowApprovalQueueStatus(x.Status))
            .Select(x =>
            {
                var project = projectLookup[x.ProjectId];
                return new ProjectWorkflowApprovalQueueItemDto
                {
                    EntityType = "ProjectBudgetRevision",
                    EntityId = x.Id,
                    ProjectId = project.Id,
                    ProjectCode = project.ProjectCode,
                    ProjectTitle = project.Title,
                    ItemTitle = x.RevisionName,
                    Status = x.Status,
                    SubmittedAt = x.SubmittedAt,
                    ApprovedAt = x.ApprovedAt,
                    DaysPending = CalculateWorkflowQueueDays(now, x.SubmittedAt, x.ApprovedAt, x.Status),
                    QueueStage = ResolveWorkflowApprovalQueueStage(x.Status)
                };
            }));

        items.AddRange(deliverables
            .Where(x => IsWorkflowApprovalQueueStatus(x.Status))
            .Select(x =>
            {
                var project = projectLookup[x.ProjectId];
                return new ProjectWorkflowApprovalQueueItemDto
                {
                    EntityType = "ProjectDeliverable",
                    EntityId = x.Id,
                    ProjectId = project.Id,
                    ProjectCode = project.ProjectCode,
                    ProjectTitle = project.Title,
                    ItemTitle = x.Title,
                    Status = x.Status,
                    SubmittedAt = x.SubmittedAt,
                    ApprovedAt = x.ApprovedAt,
                    DaysPending = CalculateWorkflowQueueDays(now, x.SubmittedAt, x.ApprovedAt, x.Status),
                    QueueStage = ResolveWorkflowApprovalQueueStage(x.Status)
                };
            }));

        items.AddRange(closures
            .Where(x => IsWorkflowApprovalQueueStatus(x.Status))
            .Select(x =>
            {
                var project = projectLookup[x.ProjectId];
                return new ProjectWorkflowApprovalQueueItemDto
                {
                    EntityType = "ProjectClosure",
                    EntityId = x.Id,
                    ProjectId = project.Id,
                    ProjectCode = project.ProjectCode,
                    ProjectTitle = project.Title,
                    ItemTitle = $"{project.ProjectCode} closure",
                    Status = x.Status,
                    SubmittedAt = x.SubmittedAt,
                    ApprovedAt = x.ApprovedAt,
                    DaysPending = CalculateWorkflowQueueDays(now, x.SubmittedAt, x.ApprovedAt, x.Status),
                    QueueStage = ResolveWorkflowApprovalQueueStage(x.Status)
                };
            }));

        return items
            .OrderBy(x => GetWorkflowApprovalQueuePriority(x.Status))
            .ThenByDescending(x => x.DaysPending)
            .ThenByDescending(x => x.SubmittedAt ?? DateTime.MinValue)
            .Take(take)
            .ToList();
    }

    public async Task<IEnumerable<ProjectExternalCollaborationReportItemDto>> GetExternalCollaborationReportAsync(int take = 200, string? collaborationState = null)
    {
        var projects = (await GetAccessibleProjectsAsync(take: take)).ToList();
        var eligibleProjects = projects
            .Where(x => x.ExternalPortalAccessEnabled || x.ExternalCollaborationEnabled)
            .ToList();
        var projectIds = eligibleProjects.Select(x => x.Id).ToList();
        if (!projectIds.Any())
        {
            return Array.Empty<ProjectExternalCollaborationReportItemDto>();
        }

        var policies = (await _unitOfWork.Repository<ProjectExternalAccessPolicy>().FindAsync(x => projectIds.Contains(x.ProjectId))).ToList();
        var documents = (await _unitOfWork.Repository<ProjectDocument>().FindAsync(x => projectIds.Contains(x.ProjectId) && x.IsExternalVisible)).ToList();
        var deliverables = (await _unitOfWork.Repository<ProjectDeliverable>().FindAsync(x => projectIds.Contains(x.ProjectId) && x.IsExternalVisible)).ToList();
        var comments = (await _unitOfWork.Repository<ProjectComment>().FindAsync(x => projectIds.Contains(x.ProjectId) && x.CommentType == "ExternalUpdate")).ToList();

        return eligibleProjects
            .Select(project =>
            {
                var projectPolicies = policies.Where(x => x.ProjectId == project.Id).ToList();
                var projectDocuments = documents.Where(x => x.ProjectId == project.Id).ToList();
                var projectDeliverables = deliverables.Where(x => x.ProjectId == project.Id).ToList();
                var projectComments = comments.Where(x => x.ProjectId == project.Id).OrderByDescending(x => x.CreatedAt).ToList();
                var pendingExternalSubmissionCount = projectDeliverables.Count(x =>
                    x.ExternalSubmissionAllowed
                    && !string.Equals(x.Status, "PendingApproval", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(x.Status, "Approved", StringComparison.OrdinalIgnoreCase));
                var pendingExternalSignOffCount = projectDeliverables.Count(x =>
                    x.ExternalSignOffRequired
                    && string.Equals(x.Status, DeliverableStatusPendingExternalSignOff, StringComparison.OrdinalIgnoreCase));
                var state = pendingExternalSignOffCount > 0 || pendingExternalSubmissionCount > 0
                    ? "ActionRequired"
                    : !project.ExternalCollaborationEnabled
                        ? "ReadOnly"
                        : projectComments.Count == 0 && projectPolicies.Count > 0
                            ? "Quiet"
                            : "Active";

                return new ProjectExternalCollaborationReportItemDto
                {
                    ProjectId = project.Id,
                    ProjectCode = project.ProjectCode,
                    ProjectTitle = project.Title,
                    Status = project.Status,
                    ExternalPortalAccessEnabled = project.ExternalPortalAccessEnabled,
                    ExternalCollaborationEnabled = project.ExternalCollaborationEnabled,
                    PolicyCount = projectPolicies.Count,
                    ExternalVisibleDocumentCount = projectDocuments.Count,
                    ExternalVisibleDeliverableCount = projectDeliverables.Count,
                    PendingExternalSubmissionCount = pendingExternalSubmissionCount,
                    PendingExternalSignOffCount = pendingExternalSignOffCount,
                    ExternalCommentCount = projectComments.Count,
                    LastExternalCommentAt = projectComments.FirstOrDefault()?.CreatedAt,
                    CollaborationState = state
                };
            })
            .Where(x => string.IsNullOrWhiteSpace(collaborationState) || string.Equals(x.CollaborationState, collaborationState, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.PendingExternalSignOffCount)
            .ThenByDescending(x => x.PendingExternalSubmissionCount)
            .ThenByDescending(x => x.ExternalCommentCount)
            .ThenBy(x => x.ProjectCode)
            .Take(take)
            .ToList();
    }

    private static string ResolveInvoiceRequestQueueStage(string? status)
        => status?.Trim().ToLowerInvariant() switch
        {
            "draft" => "Drafting",
            "submitted" => "Approval Complete",
            "requested" => "Approval Complete",
            "senttofinance" => "Finance Processing",
            "invoiced" => "Invoice Raised",
            "paid" => "Cash Collected",
            _ => "Other"
        };

    private static int GetInvoiceRequestQueuePriority(string? status)
        => status?.Trim().ToLowerInvariant() switch
        {
            "draft" => 0,
            "submitted" => 1,
            "requested" => 1,
            "senttofinance" => 2,
            "invoiced" => 3,
            "paid" => 4,
            _ => 5
        };

    private static bool IsWorkflowApprovalQueueStatus(string? status)
        => status?.Trim().ToLowerInvariant() switch
        {
            "pendingapproval" => true,
            "approved" => true,
            "rejected" => true,
            _ => false
        };

    private static string ResolveWorkflowApprovalQueueStage(string? status)
        => status?.Trim().ToLowerInvariant() switch
        {
            "pendingapproval" => "Awaiting Approval",
            "approved" => "Approved",
            "rejected" => "Rejected",
            _ => "Other"
        };

    private static int GetWorkflowApprovalQueuePriority(string? status)
        => status?.Trim().ToLowerInvariant() switch
        {
            "pendingapproval" => 0,
            "rejected" => 1,
            "approved" => 2,
            _ => 3
        };

    private static int CalculateWorkflowQueueDays(DateTime today, DateTime? submittedAt, DateTime? approvedAt, string? status)
    {
        var referenceDate = string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase)
            ? approvedAt ?? submittedAt
            : submittedAt ?? approvedAt;

        return referenceDate.HasValue
            ? Math.Max((today - referenceDate.Value.Date).Days, 0)
            : 0;
    }

    private async Task ValidateActionItemReferencesAsync(Guid projectId, Guid? meetingMinuteId, Guid? workItemId)
    {
        if (meetingMinuteId.HasValue)
        {
            var meeting = await _unitOfWork.Repository<ProjectMeetingMinute>().FirstOrDefaultAsync(x => x.Id == meetingMinuteId.Value && x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId);
            if (meeting == null)
            {
                throw new InvalidOperationException("The selected meeting does not belong to this project.");
            }
        }

        if (workItemId.HasValue)
        {
            var workItem = await _unitOfWork.Repository<ProjectWorkItem>().FirstOrDefaultAsync(x => x.Id == workItemId.Value && x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId);
            if (workItem == null)
            {
                throw new InvalidOperationException("The selected work item does not belong to this project.");
            }
        }
    }

    private async Task ValidateClosureSubmissionAsync(Project project, ProjectClosure closure)
    {
        if (!closure.DeliverablesAccepted)
        {
            throw new InvalidOperationException("Deliverable acceptance must be confirmed before project closure.");
        }

        if (!closure.TasksCompletedOrWaived && string.IsNullOrWhiteSpace(closure.OverrideReason))
        {
            throw new InvalidOperationException("Project closure requires completed tasks or an override reason.");
        }

        if (!closure.AssetsReconciled)
        {
            throw new InvalidOperationException("Asset and inventory reconciliation must be completed before closure.");
        }

        if (!closure.OpenItemsDisposed || string.IsNullOrWhiteSpace(closure.OpenItemsDisposition))
        {
            throw new InvalidOperationException("Open risks and issues must be dispositioned before closure.");
        }

        if (string.IsNullOrWhiteSpace(closure.LessonsLearnedSummary))
        {
            throw new InvalidOperationException("Closure requires a lessons learned summary.");
        }

        var unfinishedTasks = (await _unitOfWork.Repository<ProjectWorkItem>().FindAsync(x => x.ProjectId == project.Id && x.TenantId == _currentUserProvider.TenantId))
            .Any(x => !string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(x.Status, "Closed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(x.Status, "Cancelled", StringComparison.OrdinalIgnoreCase));
        if (unfinishedTasks && string.IsNullOrWhiteSpace(closure.OverrideReason))
        {
            throw new InvalidOperationException("All project work items must be completed, closed, or cancelled before closure unless an override reason is provided.");
        }
    }

    private async Task<Project> RequireProjectAsync(Guid projectId, ProjectAccessOperation operation = ProjectAccessOperation.View)
    {
        var project = await _projectRepository.GetByIdAsync(projectId) ?? throw new InvalidOperationException($"Project with ID {projectId} not found");
        await EnsureProjectAccessAsync(project, operation);
        return project;
    }

    private async Task<string> GenerateProjectCodeAsync(string numberFormat)
    {
        var year = DateTime.UtcNow.Year;
        var yearCount = (await _projectRepository.LookupAsync(take: 10000)).Count(x => x.CreatedAt.Year == year) + 1;
        var format = (numberFormat ?? "PRJ-{YYYY}-{####}")
            .Replace("{YYYY}", year.ToString())
            .Replace("{YY}", year.ToString()[2..])
            .Replace("{####}", yearCount.ToString("D4"));

        return Regex.Replace(format, @"\{SEQ:(0+)\}", match =>
        {
            var digits = match.Groups[1].Value.Length;
            return yearCount.ToString($"D{digits}");
        });
    }

    private async Task<string> GenerateInvoiceRequestNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var count = (await _unitOfWork.Repository<ProjectInvoiceRequest>().FindAsync(x => x.TenantId == _currentUserProvider.TenantId && x.RequestedAt.Year == year)).Count() + 1;
        return $"INVREQ-{year}-{count:D4}";
    }

    private async Task SaveInitiationSnapshotAsync(Project project, string changeType, string? notes)
    {
        var repo = _unitOfWork.Repository<ProjectInitiationVersion>();
        var existing = (await repo.FindAsync(x => x.ProjectId == project.Id && x.TenantId == _currentUserProvider.TenantId)).ToList();
        var snapshot = JsonSerializer.Serialize(new
        {
            project.ProjectCode,
            project.Title,
            project.Summary,
            project.BusinessCase,
            project.Objectives,
            project.StrategicAlignment,
            project.ProjectTypeId,
            project.ProjectPriorityId,
            project.PortfolioId,
            project.ProgramId,
            project.Status,
            project.Methodology,
            project.SponsorId,
            project.ProjectManagerId,
            project.StartDate,
            project.TargetEndDate,
            project.EstimatedBudget,
            project.ScopeStatement,
            project.Assumptions,
            project.Constraints,
            project.ExpectedBenefits,
            project.FundingSource,
            project.ApprovalRequired,
            project.StatusRemarks
        });
        await repo.AddAsync(new ProjectInitiationVersion
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = project.Id,
            VersionNumber = existing.Count + 1,
            SnapshotJson = snapshot,
            ChangeType = changeType,
            Notes = notes,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        });
    }

    private async Task ApplyTemplateAsync(Project project, Guid templateId, UpsertProjectDevelopmentProfileDto? requestedDevelopmentProfile = null)
    {
        var template = await _templateRepository.GetByIdAsync(templateId);
        if (template == null)
        {
            if (requestedDevelopmentProfile != null)
            {
                await UpsertProjectConstructionFoundationAsync(project, requestedDevelopmentProfile);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(template.TemplateDefinitionJson))
        {
            if (requestedDevelopmentProfile != null)
            {
                await UpsertProjectConstructionFoundationAsync(project, requestedDevelopmentProfile);
            }

            return;
        }

        try
        {
            using var document = JsonDocument.Parse(template.TemplateDefinitionJson);
            await ApplyProjectConstructionTemplateAsync(project, document.RootElement, requestedDevelopmentProfile);
            if (document.RootElement.TryGetProperty("workItems", out var workItems) && workItems.ValueKind == JsonValueKind.Array)
            {
                foreach (var workItem in workItems.EnumerateArray())
                {
                    if (workItem.ValueKind == JsonValueKind.String)
                    {
                        await _unitOfWork.Repository<ProjectWorkItem>().AddAsync(new ProjectWorkItem
                        {
                            TenantId = _currentUserProvider.TenantId,
                            ProjectId = project.Id,
                            NodeType = ProjectWorkItemNodeTypes.Task,
                            Title = workItem.GetString() ?? "Untitled",
                            Status = "New",
                            Priority = "Normal",
                            PercentComplete = 0,
                            CreatedBy = _currentUserProvider.Username,
                            CreatedById = _currentUserProvider.UserId
                        });
                        continue;
                    }

                    await _unitOfWork.Repository<ProjectWorkItem>().AddAsync(new ProjectWorkItem
                    {
                        TenantId = _currentUserProvider.TenantId,
                        ProjectId = project.Id,
                        NodeType = workItem.TryGetProperty("nodeType", out var nodeType) ? nodeType.GetString() ?? ProjectWorkItemNodeTypes.Task : ProjectWorkItemNodeTypes.Task,
                        Title = workItem.TryGetProperty("title", out var title) ? title.GetString() ?? "Untitled" : "Untitled",
                        Description = workItem.TryGetProperty("description", out var description) ? description.GetString() : null,
                        Status = workItem.TryGetProperty("status", out var status) ? status.GetString() ?? "New" : "New",
                        Priority = workItem.TryGetProperty("priority", out var priority) ? priority.GetString() ?? "Normal" : "Normal",
                        SortOrder = workItem.TryGetProperty("sortOrder", out var sortOrder) ? sortOrder.GetInt32() : 0,
                        PercentComplete = workItem.TryGetProperty("percentComplete", out var percent) ? percent.GetDecimal() : 0,
                        CreatedBy = _currentUserProvider.Username,
                        CreatedById = _currentUserProvider.UserId
                    });
                }
            }

            if (document.RootElement.TryGetProperty("milestones", out var milestones) && milestones.ValueKind == JsonValueKind.Array)
            {
                foreach (var milestone in milestones.EnumerateArray())
                {
                    if (milestone.ValueKind == JsonValueKind.String)
                    {
                        await _unitOfWork.Repository<ProjectMilestone>().AddAsync(new ProjectMilestone
                        {
                            TenantId = _currentUserProvider.TenantId,
                            ProjectId = project.Id,
                            Title = milestone.GetString() ?? "Milestone",
                            TargetDate = DateTime.UtcNow.Date,
                            Status = "Draft",
                            CreatedBy = _currentUserProvider.Username,
                            CreatedById = _currentUserProvider.UserId
                        });
                        continue;
                    }

                    await _unitOfWork.Repository<ProjectMilestone>().AddAsync(new ProjectMilestone
                    {
                        TenantId = _currentUserProvider.TenantId,
                        ProjectId = project.Id,
                        Title = milestone.TryGetProperty("title", out var title) ? title.GetString() ?? "Milestone" : "Milestone",
                        Description = milestone.TryGetProperty("description", out var description) ? description.GetString() : null,
                        TargetDate = milestone.TryGetProperty("targetDate", out var targetDate) && targetDate.TryGetDateTime(out var parsedDate) ? parsedDate : DateTime.UtcNow.Date,
                        Status = milestone.TryGetProperty("status", out var status) ? status.GetString() ?? "Draft" : "Draft",
                        RequiresApproval = milestone.TryGetProperty("requiresApproval", out var requiresApproval) && requiresApproval.GetBoolean(),
                        CreatedBy = _currentUserProvider.Username,
                        CreatedById = _currentUserProvider.UserId
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply template {TemplateId} to project {ProjectId}", templateId, project.Id);
            if (requestedDevelopmentProfile != null)
            {
                await UpsertProjectConstructionFoundationAsync(project, requestedDevelopmentProfile);
            }
        }
    }

    private async Task UpdateProjectProgressAsync(Project project)
    {
        var workItems = (await _unitOfWork.Repository<ProjectWorkItem>().FindAsync(x => x.ProjectId == project.Id && x.TenantId == _currentUserProvider.TenantId)).ToList();
        project.ProgressPercent = CalculateProgress(workItems, null);
        project.UpdatedBy = _currentUserProvider.Username;
        project.LastModifiedById = _currentUserProvider.UserId;
        await _projectRepository.UpdateAsync(project);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task ValidatePortfolioProgramAsync(Guid? portfolioId, Guid? programId)
    {
        if (!portfolioId.HasValue && !programId.HasValue)
        {
            return;
        }

        ProjectProgram? program = null;
        if (programId.HasValue)
        {
            program = await _projectProgramRepository.GetByIdAsync(programId.Value)
                ?? throw new InvalidOperationException($"Program with ID {programId} not found");
        }

        if (portfolioId.HasValue)
        {
            var portfolio = await _projectPortfolioRepository.GetByIdAsync(portfolioId.Value)
                ?? throw new InvalidOperationException($"Portfolio with ID {portfolioId} not found");

            if (program != null && program.PortfolioId.HasValue && program.PortfolioId != portfolio.Id)
            {
                throw new InvalidOperationException("Selected program does not belong to the selected portfolio");
            }
        }
        else if (program?.PortfolioId.HasValue == true)
        {
            throw new InvalidOperationException("Program is linked to a portfolio. Provide the matching portfolio on the project.");
        }
    }

    private static decimal CalculateProgress(List<ProjectWorkItem> items, Guid? parentId)
    {
        var children = items.Where(x => x.ParentId == parentId).OrderBy(x => x.SortOrder).ToList();
        if (children.Count == 0)
        {
            return 0m;
        }

        decimal total = 0m;
        foreach (var child in children)
        {
            total += items.Any(x => x.ParentId == child.Id) && child.IsRollupEnabled
                ? CalculateProgress(items, child.Id)
                : child.PercentComplete;
        }

        return Math.Round(total / children.Count, 2);
    }

    private static bool IsActiveProjectStatus(string status)
        => string.Equals(status, ProjectStatuses.Planned, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, ProjectStatuses.InProgress, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, ProjectStatuses.OnHold, StringComparison.OrdinalIgnoreCase);

    private static decimal GetEffectiveHours(ProjectResourceAllocation allocation)
    {
        if (string.Equals(allocation.AllocationType, "Percent", StringComparison.OrdinalIgnoreCase))
        {
            var availableHours = GetCapacityHours(allocation.StartDate, allocation.EndDate);
            return decimal.Round(availableHours * (allocation.AllocationValue / 100m), 2);
        }

        return allocation.PlannedHours ?? allocation.AllocationValue;
    }

    private static decimal GetCapacityUtilizationPercent(decimal allocatedHours, DateTime startDate, DateTime endDate)
    {
        return GetCapacityUtilizationPercent(allocatedHours, GetCapacityHours(startDate, endDate));
    }

    private static decimal GetCapacityUtilizationPercent(decimal allocatedHours, decimal availableHours)
        => availableHours <= 0m
            ? allocatedHours <= 0m ? 0m : 1000m
            : (allocatedHours / availableHours) * 100m;

    private static decimal GetCapacityHours(DateTime startDate, DateTime endDate)
    {
        var durationDays = Math.Max(1, (endDate.Date - startDate.Date).Days + 1);
        var workingDays = Math.Max(1m, decimal.Round(durationDays * 5m / 7m, 2));
        return workingDays * 8m;
    }

    private async Task<Dictionary<Guid, string>> GetEmployeeDisplayNamesAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Where(x => x != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return (await _unitOfWork.Repository<Employee>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && ids.Contains(x.Id)
                && !x.IsDeleted))
            .ToDictionary(
                x => x.Id,
                x => string.IsNullOrWhiteSpace(x.DisplayName) ? x.FullName : x.DisplayName);
    }

    private async Task<List<ProjectResourceAllocationDto>> EnrichResourceAllocationsAsync(List<ProjectResourceAllocationDto> allocations, List<ProjectResourceAllocation> sourceAllocations)
    {
        if (allocations.Count == 0)
        {
            return allocations;
        }

        var allocationLookup = sourceAllocations.ToDictionary(x => x.Id, x => x);
        var periodStart = allocations.Min(x => x.StartDate);
        var periodEnd = allocations.Max(x => x.EndDate);
        var normalizedRequiredNames = allocations
            .SelectMany(x => x.RequiredSkills.Concat(x.RequiredCertifications))
            .Select(NormalizeRequirementValue)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var requiredSkills = normalizedRequiredNames.Count == 0
            ? new List<Skill>()
            : (await _unitOfWork.Repository<Skill>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && normalizedRequiredNames.Contains(x.Name)))
                .ToList();
        var requiredSkillIds = requiredSkills.Select(x => x.Id).ToHashSet();
        var skills = requiredSkillIds.Count == 0
            ? new List<EmployeeSkill>()
            : (await _unitOfWork.Repository<EmployeeSkill>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && x.IsVerified
                    && requiredSkillIds.Contains(x.SkillId)))
                .ToList();
        var candidateEmployeeIds = sourceAllocations
            .Select(x => x.UserId)
            .Concat(skills.Select(x => x.EmployeeId))
            .Distinct()
            .ToList();
        var employees = candidateEmployeeIds.Count == 0
            ? new List<Employee>()
            : (await _unitOfWork.Repository<Employee>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId
                    && !x.IsDeleted
                    && candidateEmployeeIds.Contains(x.Id)))
                .ToList();
        var employeeDisplayNames = employees.ToDictionary(
            x => x.Id,
            x => string.IsNullOrWhiteSpace(x.DisplayName) ? x.FullName : x.DisplayName);
        var employeeIds = employees.Select(x => x.Id).ToHashSet();
        var tenantAllocations = (await _unitOfWork.Repository<ProjectResourceAllocation>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && x.StartDate <= periodEnd
                && x.EndDate >= periodStart))
            .Where(x => employeeIds.Contains(x.UserId))
            .Where(IsAllocationActiveForCapacity)
            .ToList();
        var skillLookup = requiredSkills.ToDictionary(
            x => x.Id,
            x => NormalizeRequirementValue(x.Name));
        var skillProfiles = BuildEmployeeSkillProfiles(skills, skillLookup, DateOnly.FromDateTime(DateTime.UtcNow.Date));

        foreach (var allocation in allocations)
        {
            if (!allocationLookup.TryGetValue(allocation.Id, out var entity))
            {
                continue;
            }

            var allocationRequiredSkills = allocation.RequiredSkills
                .Select(NormalizeRequirementValue)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var requiredCertifications = allocation.RequiredCertifications
                .Select(NormalizeRequirementValue)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var assignedProfile = skillProfiles.GetValueOrDefault(entity.UserId, EmployeeSkillProfile.Empty);
            var missingSkills = allocationRequiredSkills.Where(x => !assignedProfile.SkillNames.Contains(x)).ToList();
            var missingCertifications = requiredCertifications.Where(x => !assignedProfile.CertifiedSkillNames.Contains(x)).ToList();
            var expiringCertifications = requiredCertifications.Where(x => assignedProfile.ExpiringCertifiedSkillNames.Contains(x)).ToList();
            var totalRequirements = allocationRequiredSkills.Count + requiredCertifications.Count;
            var matchedRequirements = (allocationRequiredSkills.Count - missingSkills.Count) + (requiredCertifications.Count - missingCertifications.Count);

            allocation.RequiredSkills = allocationRequiredSkills;
            allocation.RequiredCertifications = requiredCertifications;
            allocation.RoutingPolicy = NormalizeRoutingPolicy(allocation.RoutingPolicy);
            allocation.MissingSkills = missingSkills;
            allocation.MissingCertifications = missingCertifications;
            allocation.QualificationMatchPercent = totalRequirements == 0
                ? 100m
                : decimal.Round((matchedRequirements * 100m) / totalRequirements, 2);
            allocation.QualificationRisk = missingCertifications.Count > 0
                ? "Critical"
                : missingSkills.Count > 0
                    ? "High"
                    : expiringCertifications.Count > 0
                        ? "Watch"
                        : "Healthy";

            var currentUtilization = GetCandidateCapacityUtilization(entity.UserId, allocation.StartDate, allocation.EndDate, entity.Id, tenantAllocations);
            var bestCandidate = employees
                .Where(x => x.Id != entity.UserId)
                .Select(candidate => EvaluateResourceRoutingCandidate(
                    candidate.Id,
                    employeeDisplayNames.GetValueOrDefault(candidate.Id),
                    allocation.RoutingPolicy,
                    allocationRequiredSkills,
                    requiredCertifications,
                    allocation.StartDate,
                    allocation.EndDate,
                    entity.Id,
                    skillProfiles.GetValueOrDefault(candidate.Id, EmployeeSkillProfile.Empty),
                    tenantAllocations))
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.MissingCertificationCount)
                .ThenBy(x => x.MissingSkillCount)
                .FirstOrDefault();

            if (bestCandidate is null || totalRequirements == 0 && !allocation.HasConflict)
            {
                allocation.RoutingRecommendation = totalRequirements == 0
                    ? "No explicit skill routing requirements defined for this allocation."
                    : $"Assigned resource currently matches {allocation.QualificationMatchPercent}% of the requirement profile.";
                continue;
            }

            var currentGapCount = missingSkills.Count + missingCertifications.Count;
            var candidateGapCount = bestCandidate.MissingSkillCount + bestCandidate.MissingCertificationCount;
            var candidateImprovesCoverage = candidateGapCount < currentGapCount
                || candidateGapCount == currentGapCount && bestCandidate.CapacityUtilizationPercent < currentUtilization;

            allocation.RecommendedUserId = candidateImprovesCoverage ? bestCandidate.UserId : null;
            allocation.RecommendedUserDisplayName = candidateImprovesCoverage ? bestCandidate.UserDisplayName : null;
            allocation.RoutingRecommendation = candidateImprovesCoverage
                ? BuildRoutingRecommendation(allocation, bestCandidate)
                : BuildAssignedResourceRecommendation(allocation, expiringCertifications);
        }

        return allocations;
    }

    private async Task<Dictionary<Guid, string>> GetSkillNamesByIdAsync(IEnumerable<Guid> skillIds)
    {
        var ids = skillIds.Where(x => x != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return (await _unitOfWork.Repository<Skill>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId
                && ids.Contains(x.Id)))
            .ToDictionary(
                x => x.Id,
                x => NormalizeRequirementValue(x.Name));
    }

    private async Task<Dictionary<Guid, ResourceLeaveMetrics>> GetApprovedLeaveMetricsAsync(IEnumerable<Guid> userIds, DateTime periodStart, DateTime periodEnd)
    {
        var ids = userIds.Where(x => x != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, ResourceLeaveMetrics>();
        }

        var approvedLeaves = await _unitOfWork.Repository<LeaveRequest>().FindAsync(x =>
            x.TenantId == _currentUserProvider.TenantId
            && ids.Contains(x.EmployeeId)
            && x.Status == LeaveStatus.Approved
            && x.StartDate <= periodEnd
            && x.EndDate >= periodStart);

        return approvedLeaves
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var approvedLeaveHours = group.Sum(x =>
                    {
                        var overlapStart = x.StartDate > periodStart ? x.StartDate : periodStart;
                        var overlapEnd = x.EndDate < periodEnd ? x.EndDate : periodEnd;
                        return overlapEnd < overlapStart ? 0m : GetCapacityHours(overlapStart, overlapEnd);
                    });
                    return new ResourceLeaveMetrics(
                        decimal.Round(approvedLeaveHours / 8m, 2),
                        decimal.Round(approvedLeaveHours, 2),
                        group.Count());
                });
    }

    private readonly record struct ResourceLeaveMetrics(decimal ApprovedLeaveDays, decimal ApprovedLeaveHours, int LeaveRequestCount)
    {
        public static ResourceLeaveMetrics Empty => new(0m, 0m, 0);
    }

    private readonly record struct ResourceQualificationMetrics(int VerifiedSkillCount, int CertifiedSkillCount, int ExpiringCertificationCount, int ExpiredCertificationCount)
    {
        public static ResourceQualificationMetrics Empty => new(0, 0, 0, 0);

        public string QualificationRisk
            => ExpiredCertificationCount > 0
                ? "Critical"
                : ExpiringCertificationCount > 0
                    ? "Watch"
                    : CertifiedSkillCount > 0 || VerifiedSkillCount > 0
                        ? "Healthy"
                        : "Limited";
    }

    private sealed record EmployeeSkillProfile(
        HashSet<string> SkillNames,
        HashSet<string> CertifiedSkillNames,
        HashSet<string> ExpiringCertifiedSkillNames)
    {
        public static EmployeeSkillProfile Empty { get; } = new(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private sealed record ResourceRoutingCandidate(
        Guid UserId,
        string? UserDisplayName,
        decimal Score,
        decimal CapacityUtilizationPercent,
        int MissingSkillCount,
        int MissingCertificationCount,
        List<string> MatchedSkills,
        List<string> MissingSkills,
        List<string> MissingCertifications);

    private static Dictionary<Guid, ResourceQualificationMetrics> BuildQualificationMetricsLookup(IEnumerable<EmployeeSkill> skills, DateOnly today)
        => skills
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(
                x => x.Key,
                x =>
                {
                    var verified = x.Where(skill => skill.IsVerified).ToList();
                    return new ResourceQualificationMetrics(
                        verified.Count,
                        verified.Count(skill => skill.IsCertified),
                        verified.Count(skill => skill.IsCertified && IsCertificationExpiring(skill, today)),
                        verified.Count(skill => skill.IsCertified && IsCertificationExpired(skill, today)));
                });

    private static Dictionary<Guid, EmployeeSkillProfile> BuildEmployeeSkillProfiles(IEnumerable<EmployeeSkill> skills, IReadOnlyDictionary<Guid, string> skillLookup, DateOnly today)
        => skills
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(
                x => x.Key,
                x =>
                {
                    var verified = x.Where(skill => skill.IsVerified).ToList();
                    var skillNames = verified
                        .Select(skill => skillLookup.TryGetValue(skill.SkillId, out var name) ? name : string.Empty)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var certifiedSkillNames = verified
                        .Where(skill => skill.IsCertified && !IsCertificationExpired(skill, today))
                        .Select(skill => skillLookup.TryGetValue(skill.SkillId, out var name) ? name : string.Empty)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var expiringCertifiedSkillNames = verified
                        .Where(skill => skill.IsCertified && IsCertificationExpiring(skill, today))
                        .Select(skill => skillLookup.TryGetValue(skill.SkillId, out var name) ? name : string.Empty)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    return new EmployeeSkillProfile(skillNames, certifiedSkillNames, expiringCertifiedSkillNames);
                });

    private static bool IsCertificationExpired(EmployeeSkill skill, DateOnly today)
        => skill.CertificationExpiryDate.HasValue
            && skill.CertificationExpiryDate.Value < today;

    private static bool IsCertificationExpiring(EmployeeSkill skill, DateOnly today)
        => skill.CertificationExpiryDate.HasValue
            && skill.CertificationExpiryDate.Value >= today
            && skill.CertificationExpiryDate.Value <= today.AddDays(30);

    private static string NormalizeRequirementValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string NormalizeRoutingPolicy(string? routingPolicy)
        => routingPolicy?.Trim() switch
        {
            "BestMatch" => "BestMatch",
            "CertifiedFirst" => "CertifiedFirst",
            "AvailabilityFirst" => "AvailabilityFirst",
            _ => "Balanced"
        };

    private static string? SerializeJsonList(IEnumerable<string>? values)
    {
        var sanitized = values?
            .Select(NormalizeRequirementValue)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? new List<string>();

        return sanitized.Count == 0 ? null : JsonSerializer.Serialize(sanitized);
    }

    private static List<string> DeserializeJsonList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        try
        {
            return (JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>())
                .Select(NormalizeRequirementValue)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static decimal GetCandidateCapacityUtilization(Guid userId, DateTime startDate, DateTime endDate, Guid currentAllocationId, IEnumerable<ProjectResourceAllocation> allocations)
    {
        var overlapping = allocations
            .Where(x => x.UserId == userId && x.Id != currentAllocationId && x.StartDate <= endDate && x.EndDate >= startDate)
            .ToList();
        var allocatedPercent = overlapping
            .Where(x => string.Equals(x.AllocationType, "Percent", StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.AllocationValue);
        var allocatedHours = overlapping
            .Where(x => !string.Equals(x.AllocationType, "Percent", StringComparison.OrdinalIgnoreCase))
            .Sum(GetEffectiveHours);
        return Math.Max(
            allocatedPercent,
            GetCapacityUtilizationPercent(allocatedHours, startDate, endDate));
    }

    private static ResourceRoutingCandidate EvaluateResourceRoutingCandidate(
        Guid userId,
        string? userDisplayName,
        string routingPolicy,
        IReadOnlyCollection<string> requiredSkills,
        IReadOnlyCollection<string> requiredCertifications,
        DateTime startDate,
        DateTime endDate,
        Guid currentAllocationId,
        EmployeeSkillProfile profile,
        IEnumerable<ProjectResourceAllocation> allocations)
    {
        var matchedSkills = requiredSkills.Where(x => profile.SkillNames.Contains(x)).ToList();
        var missingSkills = requiredSkills.Where(x => !profile.SkillNames.Contains(x)).ToList();
        var missingCertifications = requiredCertifications.Where(x => !profile.CertifiedSkillNames.Contains(x)).ToList();
        var matchedCertificationCount = requiredCertifications.Count - missingCertifications.Count;
        var capacityUtilizationPercent = GetCandidateCapacityUtilization(userId, startDate, endDate, currentAllocationId, allocations);
        var capacityHeadroom = Math.Max(0m, 100m - capacityUtilizationPercent);

        var score = NormalizeRoutingPolicy(routingPolicy) switch
        {
            "CertifiedFirst" => (matchedCertificationCount * 140m) + (matchedSkills.Count * 35m) + capacityHeadroom - (missingCertifications.Count * 160m) - (missingSkills.Count * 80m),
            "AvailabilityFirst" => (capacityHeadroom * 3m) + (matchedCertificationCount * 60m) + (matchedSkills.Count * 30m) - (missingCertifications.Count * 120m) - (missingSkills.Count * 60m),
            "BestMatch" => (matchedCertificationCount * 110m) + (matchedSkills.Count * 55m) + (capacityHeadroom * 0.5m) - (missingCertifications.Count * 150m) - (missingSkills.Count * 90m),
            _ => (matchedCertificationCount * 100m) + (matchedSkills.Count * 45m) + (capacityHeadroom * 1.5m) - (missingCertifications.Count * 140m) - (missingSkills.Count * 75m)
        };

        return new ResourceRoutingCandidate(
            userId,
            userDisplayName,
            score,
            decimal.Round(capacityUtilizationPercent, 2),
            missingSkills.Count,
            missingCertifications.Count,
            matchedSkills,
            missingSkills,
            missingCertifications);
    }

    private static string BuildAssignedResourceRecommendation(ProjectResourceAllocationDto allocation, IReadOnlyCollection<string> expiringCertifications)
    {
        if (allocation.MissingCertifications.Count > 0)
        {
            return $"Assigned resource is missing required certifications: {string.Join(", ", allocation.MissingCertifications)}.";
        }

        if (allocation.MissingSkills.Count > 0)
        {
            return $"Assigned resource is missing required skills: {string.Join(", ", allocation.MissingSkills)}.";
        }

        if (expiringCertifications.Count > 0)
        {
            return $"Assigned resource meets current routing requirements, but certifications are expiring soon: {string.Join(", ", expiringCertifications)}.";
        }

        return $"Assigned resource satisfies the requirement profile under {allocation.RoutingPolicy} routing.";
    }

    private static string BuildRoutingRecommendation(ProjectResourceAllocationDto allocation, ResourceRoutingCandidate candidate)
    {
        var baseText = $"Route this allocation using {allocation.RoutingPolicy}: {candidate.UserDisplayName ?? candidate.UserId.ToString()} is the best-fit backup at {candidate.CapacityUtilizationPercent}% utilization.";
        if (candidate.MissingCertificationCount > 0 || candidate.MissingSkillCount > 0)
        {
            var gaps = candidate.MissingCertifications.Concat(candidate.MissingSkills).ToList();
            return $"{baseText} Remaining coverage gaps: {string.Join(", ", gaps)}.";
        }

        if (candidate.MatchedSkills.Count > 0)
        {
            return $"{baseText} Matched skills: {string.Join(", ", candidate.MatchedSkills)}.";
        }

        return baseText;
    }

    private static string MaxSeverity(string left, string right)
        => GetSeverityRank(left) >= GetSeverityRank(right) ? left : right;

    private static int GetSeverityRank(string severity)
        => severity switch
        {
            "Critical" => 4,
            "High" => 3,
            "Low" => 2,
            "Balanced" => 1,
            _ => 0
        };

    private static bool IsAllocationActiveForCapacity(ProjectResourceAllocation allocation)
        => !string.Equals(allocation.Status, "Rejected", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(allocation.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(allocation.Status, "Substituted", StringComparison.OrdinalIgnoreCase);

    private static bool HasAllocationConflict(ProjectResourceAllocation allocation, List<ProjectResourceAllocation> allocations)
    {
        if (!IsAllocationActiveForCapacity(allocation))
        {
            return false;
        }

        var overlapping = allocations.Where(x =>
                x.UserId == allocation.UserId
                && x.Id != allocation.Id
                && x.StartDate <= allocation.EndDate
                && x.EndDate >= allocation.StartDate
                && IsAllocationActiveForCapacity(x))
            .ToList();

        var totalPercent = overlapping.Where(x => string.Equals(x.AllocationType, "Percent", StringComparison.OrdinalIgnoreCase)).Sum(x => x.AllocationValue);
        if (string.Equals(allocation.AllocationType, "Percent", StringComparison.OrdinalIgnoreCase))
        {
            totalPercent += allocation.AllocationValue;
        }

        var totalHours = overlapping.Where(x => !string.Equals(x.AllocationType, "Percent", StringComparison.OrdinalIgnoreCase)).Sum(GetEffectiveHours);
        if (!string.Equals(allocation.AllocationType, "Percent", StringComparison.OrdinalIgnoreCase))
        {
            totalHours += GetEffectiveHours(allocation);
        }

        var capacityPercent = GetCapacityUtilizationPercent(totalHours, allocation.StartDate, allocation.EndDate);
        return totalPercent > 100m || capacityPercent > 100m;
    }

    private static string? AppendResourceNote(string? existing, string action, string? reason)
    {
        var note = string.IsNullOrWhiteSpace(reason)
            ? $"{action} on {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC"
            : $"{action} on {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC: {reason.Trim()}";
        return string.IsNullOrWhiteSpace(existing) ? note : $"{existing}\n{note}";
    }

    private static string ResolveApprovalQueueStage(string? status)
        => status?.Trim().ToLowerInvariant() switch
        {
            "draft" => "Draft",
            "submitted" => "Pending Approval",
            "approved" => "Approved",
            "rejected" => "Rejected",
            _ => "Other"
        };

    private static int GetApprovalQueuePriority(string? status)
        => status?.Trim().ToLowerInvariant() switch
        {
            "submitted" => 3,
            "rejected" => 2,
            "draft" => 1,
            "approved" => 0,
            _ => -1
        };

    private async Task ApplyApprovedTimesheetAsync(Project project, ProjectTimesheetEntry entry)
    {
        project.ActualCost = (project.ActualCost ?? 0m) + entry.CostAmount;
        await _projectRepository.UpdateAsync(project);

        if (!entry.WorkItemId.HasValue)
        {
            return;
        }

        var workItemRepo = _unitOfWork.Repository<ProjectWorkItem>();
        var workItem = await workItemRepo.FirstOrDefaultAsync(x => x.Id == entry.WorkItemId.Value && x.TenantId == _currentUserProvider.TenantId);
        if (workItem == null)
        {
            return;
        }

        workItem.ActualEffortHours = (workItem.ActualEffortHours ?? 0m) + entry.Hours;
        workItem.UpdatedBy = _currentUserProvider.Username;
        workItem.LastModifiedById = _currentUserProvider.UserId;
        await workItemRepo.UpdateAsync(workItem);
    }

    private async Task ValidateTimesheetEntryAsync(Project project, CreateProjectTimesheetEntryDto dto, Guid? entryId = null)
    {
        EnsureProjectOpenForCostCapture(project);
        await EnsureOwnedByCurrentUserOrAuthorizedAsync(project, dto.UserId);
        if (dto.Hours <= 0)
        {
            throw new InvalidOperationException("Timesheet hours must be greater than zero.");
        }

        if (dto.Hours > 24)
        {
            throw new InvalidOperationException("A single timesheet entry cannot exceed 24 hours.");
        }

        var entryDate = dto.EntryDate == default ? DateTime.UtcNow.Date : dto.EntryDate.Date;
        var dayEntries = (await _unitOfWork.Repository<ProjectTimesheetEntry>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId &&
                x.UserId == dto.UserId &&
                x.EntryDate.Date == entryDate &&
                (!entryId.HasValue || x.Id != entryId.Value) &&
                !string.Equals(x.Status, "Rejected", StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (dayEntries.Sum(x => x.Hours) + dto.Hours > 24m)
        {
            throw new InvalidOperationException("Total logged hours for the selected day cannot exceed 24.");
        }

        if (dto.WorkItemId.HasValue)
        {
            var workItem = await _unitOfWork.Repository<ProjectWorkItem>().FirstOrDefaultAsync(x => x.Id == dto.WorkItemId.Value && x.TenantId == _currentUserProvider.TenantId);
            if (workItem == null || workItem.ProjectId != project.Id)
            {
                throw new InvalidOperationException("The selected work item does not belong to this project.");
            }
        }

        if (dto.IsBillable)
        {
            var hasBillingConfig = project.ContractId.HasValue || (await _unitOfWork.Repository<ProjectBillingSchedule>().FindAsync(x => x.ProjectId == project.Id && x.TenantId == _currentUserProvider.TenantId && x.IsBillable)).Any();
            if (!hasBillingConfig)
            {
                throw new InvalidOperationException("Billable time requires project billing configuration.");
            }
        }
    }

    private async Task ValidateExpenseAsync(Project project, CreateProjectExpenseDto dto)
    {
        EnsureProjectOpenForCostCapture(project);
        await EnsureOwnedByCurrentUserOrAuthorizedAsync(project, dto.UserId);
        if (dto.Amount <= 0)
        {
            throw new InvalidOperationException("Expense amount must be greater than zero.");
        }

        if (dto.TaxAmount < 0)
        {
            throw new InvalidOperationException("Tax amount cannot be negative.");
        }

        if (dto.WorkItemId.HasValue)
        {
            var workItem = await _unitOfWork.Repository<ProjectWorkItem>().FirstOrDefaultAsync(x => x.Id == dto.WorkItemId.Value && x.TenantId == _currentUserProvider.TenantId);
            if (workItem == null || workItem.ProjectId != project.Id)
            {
                throw new InvalidOperationException("The selected work item does not belong to this project.");
            }
        }

        if (dto.IsBillable)
        {
            var hasBillingConfig = project.ContractId.HasValue || (await _unitOfWork.Repository<ProjectBillingSchedule>().FindAsync(x => x.ProjectId == project.Id && x.TenantId == _currentUserProvider.TenantId && x.IsBillable)).Any();
            if (!hasBillingConfig)
            {
                throw new InvalidOperationException("Billable expenses require project billing configuration.");
            }
        }
    }

    private static void EnsureProjectOpenForCostCapture(Project project)
    {
        if (string.Equals(project.Status, ProjectStatuses.Closed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(project.Status, ProjectStatuses.Archived, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(project.Status, ProjectStatuses.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Time and expenses cannot be captured against a closed, archived, or cancelled project.");
        }
    }

    private static string NormalizeEntryStatus(string? status)
    {
        var normalized = string.IsNullOrWhiteSpace(status) ? "Draft" : status.Trim();
        return string.Equals(normalized, "Submitted", StringComparison.OrdinalIgnoreCase) ? "Submitted"
            : string.Equals(normalized, "Rejected", StringComparison.OrdinalIgnoreCase) ? "Rejected"
            : string.Equals(normalized, "Approved", StringComparison.OrdinalIgnoreCase) ? "Approved"
            : "Draft";
    }

    private static bool HasApprovedEntryStatus(string? status)
        => string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase);

    private static void EnsureEntryEditable(string status, string entryType)
    {
        if (string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Submitted", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Only draft or rejected {entryType} entries can be edited.");
        }
    }

    private static void EnsureEntryDeletable(string status, string entryType)
    {
        if (!string.Equals(status, "Draft", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Only draft or rejected {entryType} entries can be deleted.");
        }
    }

    private static string AppendDecisionNote(string? existingNotes, string decision, string? comments)
    {
        if (string.IsNullOrWhiteSpace(comments))
        {
            return existingNotes ?? string.Empty;
        }

        var prefix = $"[{decision} {DateTime.UtcNow:yyyy-MM-dd}] {comments.Trim()}";
        return string.IsNullOrWhiteSpace(existingNotes) ? prefix : $"{existingNotes}\n{prefix}";
    }

    private async Task<IEnumerable<ProjectTimesheetEntryDto>> MapTimesheetEntriesAsync(List<ProjectTimesheetEntry> entries)
    {
        var projectIds = entries.Select(x => x.ProjectId).Distinct().ToList();
        var workItemIds = entries.Where(x => x.WorkItemId.HasValue).Select(x => x.WorkItemId!.Value).Distinct().ToList();
        var projects = new Dictionary<Guid, Project>();
        foreach (var projectId in projectIds)
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project != null)
            {
                projects[projectId] = project;
            }
        }
        var workItems = (await _unitOfWork.Repository<ProjectWorkItem>().FindAsync(x => workItemIds.Contains(x.Id) && x.TenantId == _currentUserProvider.TenantId)).ToDictionary(x => x.Id);

        return entries.Select(entry => MapToDto(
            entry,
            projects.GetValueOrDefault(entry.ProjectId),
            entry.WorkItemId.HasValue ? workItems.GetValueOrDefault(entry.WorkItemId.Value) : null))
            .ToList();
    }

    private async Task<ProjectTimesheetEntryDto> MapTimesheetEntryAsync(ProjectTimesheetEntry entry)
        => (await MapTimesheetEntriesAsync(new List<ProjectTimesheetEntry> { entry })).First();

    private async Task<IEnumerable<ProjectExpenseDto>> MapExpensesAsync(List<ProjectExpense> entries)
    {
        var projectIds = entries.Select(x => x.ProjectId).Distinct().ToList();
        var workItemIds = entries.Where(x => x.WorkItemId.HasValue).Select(x => x.WorkItemId!.Value).Distinct().ToList();
        var projects = new Dictionary<Guid, Project>();
        foreach (var projectId in projectIds)
        {
            var project = await _projectRepository.GetByIdAsync(projectId);
            if (project != null)
            {
                projects[projectId] = project;
            }
        }
        var workItems = (await _unitOfWork.Repository<ProjectWorkItem>().FindAsync(x => workItemIds.Contains(x.Id) && x.TenantId == _currentUserProvider.TenantId)).ToDictionary(x => x.Id);

        return entries.Select(entry => MapToDto(
            entry,
            projects.GetValueOrDefault(entry.ProjectId),
            entry.WorkItemId.HasValue ? workItems.GetValueOrDefault(entry.WorkItemId.Value) : null))
            .ToList();
    }

    private async Task<ProjectExpenseDto> MapExpenseAsync(ProjectExpense entry)
        => (await MapExpensesAsync(new List<ProjectExpense> { entry })).First();

    private static ProjectApprovalQueueSummaryDto BuildApprovalSummary(IEnumerable<string> statuses, decimal totalHours, decimal totalAmount)
    {
        var list = statuses.ToList();
        return new ProjectApprovalQueueSummaryDto
        {
            DraftCount = list.Count(x => string.Equals(x, "Draft", StringComparison.OrdinalIgnoreCase)),
            SubmittedCount = list.Count(x => string.Equals(x, "Submitted", StringComparison.OrdinalIgnoreCase)),
            ApprovedCount = list.Count(x => string.Equals(x, "Approved", StringComparison.OrdinalIgnoreCase)),
            RejectedCount = list.Count(x => string.Equals(x, "Rejected", StringComparison.OrdinalIgnoreCase)),
            TotalHours = totalHours,
            TotalAmount = totalAmount
        };
    }

    private async Task SubmitDeliverableWorkflowAsync(ProjectDeliverable deliverable)
    {
        var workflowResult = await _workflowIntegrationService.SubmitAsync(ProjectDeliverableWorkflowEntityType, deliverable.Id);
        if (!workflowResult.ExecutionResult.Success)
        {
            throw new InvalidOperationException(workflowResult.ExecutionResult.Message ?? "Failed to submit deliverable workflow.");
        }

        var adapter = _workflowStatusAdapterRegistry.GetAdapter(ProjectDeliverableWorkflowEntityType);
        adapter.ApplySubmitOutcome(deliverable, workflowResult.Outcome, _currentUserProvider.UserId);
        deliverable.UpdatedBy = _currentUserProvider.Username;
        deliverable.LastModifiedById = _currentUserProvider.UserId;
    }

    private void ValidateDeliverableSubmission(ProjectDeliverable deliverable)
    {
        if (string.Equals(deliverable.Status, DeliverableStatusPendingApproval, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(deliverable.Status, DeliverableStatusApproved, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Project deliverable cannot be submitted from status '{deliverable.Status}'.");
        }
    }

    private async Task AddDeliverableCommentAsync(ProjectDeliverable deliverable, string commentType, string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return;
        }

        await _unitOfWork.Repository<ProjectComment>().AddAsync(new ProjectComment
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = deliverable.ProjectId,
            WorkItemId = deliverable.WorkItemId,
            CommentType = commentType,
            Body = notes.Trim(),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        });
    }

    private async Task AddDeliverableExternalReviewAsync(Guid projectId, Guid deliverableId, Guid? reviewedById, string decision, string? statusSnapshot, string? notes, Guid? submittedDocumentId)
    {
        await _unitOfWork.Repository<ProjectDeliverableExternalReview>().AddAsync(new ProjectDeliverableExternalReview
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = projectId,
            DeliverableId = deliverableId,
            ReviewDate = DateTime.UtcNow,
            ReviewedById = reviewedById,
            SubmittedDocumentId = submittedDocumentId,
            Decision = decision,
            StatusSnapshot = statusSnapshot,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        });
    }

    private async Task<List<ProjectDeliverableDto>> AttachDeliverableExternalReviewsAsync(List<ProjectDeliverableDto> deliverables)
    {
        if (deliverables.Count == 0)
        {
            return deliverables;
        }

        var deliverableIds = deliverables.Select(x => x.Id).Distinct().ToList();
        var reviews = (await _unitOfWork.Repository<ProjectDeliverableExternalReview>().FindAsync(x =>
                x.TenantId == _currentUserProvider.TenantId &&
                deliverableIds.Contains(x.DeliverableId)))
            .OrderByDescending(x => x.ReviewDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToList();

        var submittedDocumentIds = reviews
            .Where(x => x.SubmittedDocumentId.HasValue)
            .Select(x => x.SubmittedDocumentId!.Value)
            .Distinct()
            .ToList();
        var documentLookup = submittedDocumentIds.Count == 0
            ? new Dictionary<Guid, ProjectDocument>()
            : (await _unitOfWork.Repository<ProjectDocument>().FindAsync(x =>
                    x.TenantId == _currentUserProvider.TenantId &&
                    submittedDocumentIds.Contains(x.Id)))
                .ToDictionary(x => x.Id, x => x);

        var reviewLookup = reviews
            .GroupBy(x => x.DeliverableId)
            .ToDictionary(
                x => x.Key,
                x => x.Select(review => MapToDto(review, documentLookup)).ToList());

        foreach (var deliverable in deliverables)
        {
            deliverable.ExternalReviews = reviewLookup.TryGetValue(deliverable.Id, out var entries)
                ? entries
                : new List<ProjectDeliverableExternalReviewDto>();
        }

        return deliverables;
    }

    private async Task<List<ProjectExternalAccessPolicy>> GetExternalPoliciesAsync(Guid projectId, Guid businessPartnerId)
        => (await _unitOfWork.Repository<ProjectExternalAccessPolicy>().FindAsync(x =>
                x.ProjectId == projectId &&
                x.BusinessPartnerId == businessPartnerId &&
                x.TenantId == _currentUserProvider.TenantId))
            .ToList();

    private static bool HasExternalProjectAccess(Project project, Guid businessPartnerId, IEnumerable<ProjectExternalAccessPolicy> policies)
        => project.BusinessPartnerId == businessPartnerId
            || policies.Any(x =>
                string.Equals(x.ArtifactType, "Project", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.ArtifactType, "ProjectHeader", StringComparison.OrdinalIgnoreCase));

    private static bool HasArtifactAccess(Project project, IEnumerable<ProjectExternalAccessPolicy> policies, Guid businessPartnerId, string artifactType, Guid artifactId)
    {
        if (project.BusinessPartnerId == businessPartnerId)
        {
            return true;
        }

        return policies.Any(x =>
            (string.Equals(x.ArtifactType, "Project", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.ArtifactType, artifactType, StringComparison.OrdinalIgnoreCase))
            && (!x.ArtifactId.HasValue || x.ArtifactId == artifactId));
    }

    private async Task EnsureExternalPolicyAsync(Guid projectId, Guid userId, string artifactType, Guid? artifactId, bool requireApprove, bool requireUpload, bool requireComment, bool requireCollaboration)
    {
        var partner = await RequireExternalBusinessPartnerAsync(userId);
        var project = await _projectRepository.GetByIdAsync(projectId)
            ?? throw new InvalidOperationException($"Project with ID {projectId} not found");
        if (project.BusinessPartnerId == partner.Id)
        {
            return;
        }

        var policies = (await _unitOfWork.Repository<ProjectExternalAccessPolicy>().FindAsync(x =>
                x.ProjectId == projectId
                && x.BusinessPartnerId == partner.Id
                && x.TenantId == _currentUserProvider.TenantId
                && (x.ArtifactType == artifactType || x.ArtifactType == "Project")
                && (!x.ArtifactId.HasValue || (artifactId.HasValue && x.ArtifactId == artifactId))))
            .ToList();

        if (policies.Count == 0)
        {
            throw new UnauthorizedAccessException("External access policy does not permit this action.");
        }

        var allowed = policies.Any(x =>
            HasExternalActionPermission(project, new[] { x }, partner.Id, artifactType, artifactId, requireApprove, requireUpload, requireComment, requireCollaboration));

        if (!allowed)
        {
            throw new UnauthorizedAccessException("External access policy does not permit this action.");
        }
    }

    private static bool HasExternalActionPermission(Project project, IEnumerable<ProjectExternalAccessPolicy> policies, Guid businessPartnerId, string artifactType, Guid? artifactId, bool requireApprove, bool requireUpload, bool requireComment, bool requireCollaboration)
    {
        if (project.BusinessPartnerId == businessPartnerId)
        {
            return true;
        }

        return policies.Any(x =>
            (string.Equals(x.ArtifactType, "Project", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.ArtifactType, artifactType, StringComparison.OrdinalIgnoreCase))
            && (!x.ArtifactId.HasValue || (artifactId.HasValue && x.ArtifactId == artifactId))
            && (!requireApprove || x.CanApprove)
            && (!requireUpload || x.CanUpload)
            && (!requireComment || x.CanComment)
            && IsExternalAccessLevelAllowed(x.AccessLevel, requireApprove, requireUpload, requireComment, requireCollaboration));
    }

    private static bool IsExternalAccessLevelAllowed(string? accessLevel, bool requireApprove, bool requireUpload, bool requireComment, bool requireCollaboration)
    {
        if (string.IsNullOrWhiteSpace(accessLevel))
        {
            return !requireApprove && !requireUpload && !requireComment && !requireCollaboration;
        }

        return accessLevel.Trim() switch
        {
            "Read" => !requireApprove && !requireUpload && !requireComment && !requireCollaboration,
            "Collaborate" => !requireApprove,
            "Approve" => !requireCollaboration,
            "Write" => !requireApprove,
            "Full" => true,
            _ => false
        };
    }

    private async Task ValidateExternalAccessPolicyAsync(Project project, CreateProjectExternalAccessPolicyDto dto)
    {
        var artifactType = NormalizeExternalArtifactType(dto.ArtifactType);
        var partner = await _businessPartnerService.GetByIdAsync(dto.BusinessPartnerId)
            ?? throw new InvalidOperationException($"Business partner with ID {dto.BusinessPartnerId} not found");

        if (!string.Equals(partner.Status, "Active", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(partner.Status, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("External access policies can only be assigned to active business partners.");
        }

        dto.ArtifactType = artifactType;
        if (string.Equals(artifactType, "Project", StringComparison.OrdinalIgnoreCase))
        {
            dto.ArtifactId = null;
        }
        else if (!dto.ArtifactId.HasValue)
        {
            throw new InvalidOperationException($"{artifactType} access policies require an artifact ID.");
        }

        switch (artifactType)
        {
            case "Project":
                return;
            case "WorkItem":
                _ = await _unitOfWork.Repository<ProjectWorkItem>().FirstOrDefaultAsync(x => x.Id == dto.ArtifactId && x.ProjectId == project.Id && x.TenantId == _currentUserProvider.TenantId)
                    ?? throw new InvalidOperationException($"Project work item with ID {dto.ArtifactId} not found");
                return;
            case "Deliverable":
                _ = await _unitOfWork.Repository<ProjectDeliverable>().FirstOrDefaultAsync(x => x.Id == dto.ArtifactId && x.ProjectId == project.Id && x.TenantId == _currentUserProvider.TenantId)
                    ?? throw new InvalidOperationException($"Project deliverable with ID {dto.ArtifactId} not found");
                return;
            case "Document":
                _ = await _unitOfWork.Repository<ProjectDocument>().FirstOrDefaultAsync(x => x.Id == dto.ArtifactId && x.ProjectId == project.Id && x.TenantId == _currentUserProvider.TenantId)
                    ?? throw new InvalidOperationException($"Project document with ID {dto.ArtifactId} not found");
                return;
            default:
                throw new InvalidOperationException($"External access policy artifact type '{dto.ArtifactType}' is not supported.");
        }
    }

    private static string NormalizeExternalArtifactType(string? artifactType)
        => string.IsNullOrWhiteSpace(artifactType)
            ? "Project"
            : artifactType.Trim() switch
            {
                "ProjectHeader" => "Project",
                "Project" => "Project",
                "WorkItem" => "WorkItem",
                "Deliverable" => "Deliverable",
                "Document" => "Document",
                _ => artifactType.Trim()
            };

    private async Task<List<ProjectWorkItem>> LoadWorkTreeAsync(Guid projectId)
        => (await _unitOfWork.Repository<ProjectWorkItem>()
                .FindAsync(x => x.ProjectId == projectId && x.TenantId == _currentUserProvider.TenantId))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CreatedAt)
            .ToList();

    private async Task<ContractMilestoneDto?> GetContractMilestoneAsync(Guid? contractId, Guid? contractMilestoneId)
    {
        if (!contractId.HasValue || !contractMilestoneId.HasValue)
        {
            return null;
        }

        var contract = await _contractService.GetByIdAsync(contractId.Value)
            ?? throw new InvalidOperationException($"Contract with ID {contractId.Value} not found");

        return contract.Milestones.SingleOrDefault(x => x.Id == contractMilestoneId.Value)
            ?? throw new InvalidOperationException($"Contract milestone with ID {contractMilestoneId.Value} not found");
    }

    private async Task<BusinessPartnerDetailDto> RequireExternalBusinessPartnerAsync(Guid userId)
        => await _businessPartnerService.GetByUserIdAsync(userId)
            ?? throw new UnauthorizedAccessException("No business partner is linked to the current portal user.");

    private async Task<Project> RequireExternalProjectAsync(Guid projectId, Guid userId, bool requireCollaboration)
    {
        var partner = await RequireExternalBusinessPartnerAsync(userId);
        var project = await _projectRepository.GetByIdAsync(projectId)
            ?? throw new InvalidOperationException($"Project with ID {projectId} not found");
        var policies = await GetExternalPoliciesAsync(projectId, partner.Id);

        if (!project.ExternalPortalAccessEnabled || !HasExternalProjectAccess(project, partner.Id, policies))
        {
            throw new UnauthorizedAccessException("You do not have access to this project.");
        }

        if (requireCollaboration && !project.ExternalCollaborationEnabled)
        {
            throw new UnauthorizedAccessException("External collaboration is not enabled for this project.");
        }

        return project;
    }

    private IEnumerable<ProjectWorkItemDto> BuildWorkItemTree(List<ProjectWorkItem> allItems, Guid? parentId)
        => allItems.Where(x => x.ParentId == parentId).OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedAt).Select(x =>
        {
            var dto = MapToDto(x);
            dto.Children = BuildWorkItemTree(allItems, x.Id).ToList();
            return dto;
        });

    private async Task ValidateWorkItemScheduleAsync(Project project, ProjectWorkItem? existing, CreateProjectWorkItemDto dto)
    {
        if (dto.PlannedStartDate.HasValue && dto.PlannedEndDate.HasValue && dto.PlannedStartDate.Value.Date > dto.PlannedEndDate.Value.Date)
        {
            throw new InvalidOperationException("Planned start date cannot be later than planned end date.");
        }

        if (dto.ActualStartDate.HasValue && dto.ActualEndDate.HasValue && dto.ActualStartDate.Value.Date > dto.ActualEndDate.Value.Date)
        {
            throw new InvalidOperationException("Actual start date cannot be later than actual end date.");
        }

        if (dto.ParentId.HasValue)
        {
            var parent = await _unitOfWork.Repository<ProjectWorkItem>().FirstOrDefaultAsync(x => x.Id == dto.ParentId.Value && x.TenantId == _currentUserProvider.TenantId);
            if (parent == null || parent.ProjectId != project.Id)
            {
                throw new InvalidOperationException("The selected parent work item does not belong to this project.");
            }

            if (existing != null && dto.ParentId == existing.Id)
            {
                throw new InvalidOperationException("A work item cannot be its own parent.");
            }
        }

        var planningChanged = existing != null &&
            (existing.PlannedStartDate?.Date != dto.PlannedStartDate?.Date || existing.PlannedEndDate?.Date != dto.PlannedEndDate?.Date);
        if (planningChanged)
        {
            var hasLockedBaseline = (await _unitOfWork.Repository<ProjectBaseline>().FindAsync(x =>
                x.ProjectId == project.Id &&
                x.TenantId == _currentUserProvider.TenantId &&
                x.IsLocked)).Any();
            if (hasLockedBaseline && string.IsNullOrWhiteSpace(dto.ScheduleChangeReason))
            {
                throw new InvalidOperationException("A schedule change reason is required once a baseline has been locked.");
            }
        }

        if (existing == null)
        {
            return;
        }

        var dependencies = (await _unitOfWork.Repository<ProjectTaskDependency>().FindAsync(x =>
                x.ProjectId == project.Id &&
                x.SuccessorWorkItemId == existing.Id &&
                x.TenantId == _currentUserProvider.TenantId &&
                x.IsEnforced))
            .ToList();
        if (dependencies.Count == 0)
        {
            return;
        }

        var predecessorIds = dependencies.Select(x => x.PredecessorWorkItemId).Distinct().ToList();
        var predecessors = (await _unitOfWork.Repository<ProjectWorkItem>().FindAsync(x =>
                predecessorIds.Contains(x.Id) &&
                x.TenantId == _currentUserProvider.TenantId))
            .ToDictionary(x => x.Id);

        foreach (var dependency in dependencies)
        {
            if (!predecessors.TryGetValue(dependency.PredecessorWorkItemId, out var predecessor))
            {
                continue;
            }

            var violation = BuildScheduleViolation(existing.Id, dto.Title, predecessor, dependency, dto);
            if (violation != null)
            {
                throw new InvalidOperationException(violation.Message);
            }
        }
    }

    private ProjectScheduleAnalysisDto BuildScheduleAnalysis(Guid projectId, List<ProjectWorkItem> workItems, List<ProjectTaskDependency> dependencies, bool? hasCircularDependencies = null, int recalculatedItemCount = 0)
    {
        var durations = workItems.ToDictionary(
            x => x.Id,
            x => Math.Max(1, (x.PlannedEndDate?.Date - x.PlannedStartDate?.Date)?.Days + 1 ?? 1));
        var successors = dependencies.GroupBy(x => x.PredecessorWorkItemId).ToDictionary(x => x.Key, x => x.ToList());
        var predecessorCounts = workItems.ToDictionary(x => x.Id, x => dependencies.Count(d => d.SuccessorWorkItemId == x.Id));
        var queue = new Queue<Guid>(predecessorCounts.Where(x => x.Value == 0).Select(x => x.Key));
        var earliestFinish = workItems.ToDictionary(x => x.Id, _ => 0);
        var processed = 0;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            processed++;
            if (!successors.TryGetValue(current, out var linked))
            {
                continue;
            }

            foreach (var dependency in linked)
            {
                earliestFinish[dependency.SuccessorWorkItemId] = Math.Max(
                    earliestFinish[dependency.SuccessorWorkItemId],
                    earliestFinish[current] + durations[current] + Math.Max(0, dependency.LagDays));
                predecessorCounts[dependency.SuccessorWorkItemId]--;
                if (predecessorCounts[dependency.SuccessorWorkItemId] <= 0)
                {
                    queue.Enqueue(dependency.SuccessorWorkItemId);
                }
            }
        }

        var hasCycle = hasCircularDependencies ?? processed < workItems.Count;
        var longest = earliestFinish.OrderByDescending(x => x.Value).FirstOrDefault();
        var criticalIds = new List<Guid>();
        if (!hasCycle && longest.Key != Guid.Empty)
        {
            criticalIds.Add(longest.Key);
            var current = longest.Key;
            while (true)
            {
                var predecessor = dependencies
                    .Where(x => x.SuccessorWorkItemId == current)
                    .OrderByDescending(x => earliestFinish.GetValueOrDefault(x.PredecessorWorkItemId))
                    .FirstOrDefault();
                if (predecessor == null)
                {
                    break;
                }

                criticalIds.Add(predecessor.PredecessorWorkItemId);
                current = predecessor.PredecessorWorkItemId;
            }
            criticalIds.Reverse();
        }

        var forecastStart = workItems.Where(x => x.PlannedStartDate.HasValue).Select(x => x.PlannedStartDate!.Value.Date).DefaultIfEmpty(DateTime.UtcNow.Date).Min();
        return new ProjectScheduleAnalysisDto
        {
            ProjectId = projectId,
            DependencyCount = dependencies.Count,
            CriticalPathTaskCount = criticalIds.Count,
            CriticalPathWorkItemIds = criticalIds,
            ForecastFinishDate = longest.Key == Guid.Empty ? null : forecastStart.AddDays(longest.Value),
            TotalSlackDays = Math.Max(0, durations.Values.Sum() - longest.Value),
            HasCircularDependencies = hasCycle,
            RecalculatedItemCount = recalculatedItemCount,
            Violations = BuildScheduleViolations(workItems, dependencies)
        };
    }

    private List<ProjectScheduleViolationDto> BuildScheduleViolations(List<ProjectWorkItem> workItems, List<ProjectTaskDependency> dependencies)
    {
        var lookup = workItems.ToDictionary(x => x.Id);
        var violations = new List<ProjectScheduleViolationDto>();
        foreach (var dependency in dependencies.Where(x => x.IsEnforced))
        {
            if (!lookup.TryGetValue(dependency.PredecessorWorkItemId, out var predecessor) || !lookup.TryGetValue(dependency.SuccessorWorkItemId, out var successor))
            {
                continue;
            }

            var violation = BuildScheduleViolation(successor.Id, successor.Title, predecessor, dependency, new CreateProjectWorkItemDto
            {
                Title = successor.Title,
                Status = successor.Status,
                PlannedStartDate = successor.PlannedStartDate,
                PlannedEndDate = successor.PlannedEndDate,
                ActualStartDate = successor.ActualStartDate,
                ActualEndDate = successor.ActualEndDate,
                PercentComplete = successor.PercentComplete
            });

            if (violation != null)
            {
                violations.Add(violation);
            }
        }

        return violations
            .GroupBy(x => $"{x.WorkItemId}:{x.BlockingWorkItemId}:{x.DependencyType}:{x.Message}")
            .Select(x => x.First())
            .ToList();
    }

    private static ProjectScheduleViolationDto? BuildScheduleViolation(Guid workItemId, string workItemTitle, ProjectWorkItem predecessor, ProjectTaskDependency dependency, CreateProjectWorkItemDto candidate)
    {
        var dependencyType = NormalizeDependencyType(dependency.DependencyType);
        var plannedStart = candidate.PlannedStartDate?.Date;
        var plannedEnd = candidate.PlannedEndDate?.Date;
        var actualStart = candidate.ActualStartDate?.Date;
        var actualEnd = candidate.ActualEndDate?.Date;

        DateTime? expectedDate;
        string message;

        switch (dependencyType)
        {
            case "SS":
                expectedDate = predecessor.PlannedStartDate?.Date.AddDays(dependency.LagDays);
                if (expectedDate.HasValue && plannedStart.HasValue && plannedStart.Value < expectedDate.Value)
                {
                    message = $"'{workItemTitle}' cannot start before '{predecessor.Title}' starts ({expectedDate.Value:yyyy-MM-dd}).";
                    return CreateViolation();
                }
                break;
            case "FF":
                expectedDate = predecessor.PlannedEndDate?.Date.AddDays(dependency.LagDays);
                if (expectedDate.HasValue && plannedEnd.HasValue && plannedEnd.Value < expectedDate.Value)
                {
                    message = $"'{workItemTitle}' cannot finish before '{predecessor.Title}' finishes ({expectedDate.Value:yyyy-MM-dd}).";
                    return CreateViolation();
                }
                if (IsStarted(candidate) && !IsCompleted(predecessor))
                {
                    message = $"'{workItemTitle}' cannot be finished while '{predecessor.Title}' is still incomplete.";
                    return CreateViolation();
                }
                break;
            case "SF":
                expectedDate = predecessor.PlannedStartDate?.Date.AddDays(dependency.LagDays);
                if (expectedDate.HasValue && plannedEnd.HasValue && plannedEnd.Value < expectedDate.Value)
                {
                    message = $"'{workItemTitle}' cannot finish before '{predecessor.Title}' starts ({expectedDate.Value:yyyy-MM-dd}).";
                    return CreateViolation();
                }
                break;
            default:
                expectedDate = predecessor.PlannedEndDate?.Date.AddDays(dependency.LagDays);
                if (expectedDate.HasValue && plannedStart.HasValue && plannedStart.Value < expectedDate.Value)
                {
                    message = $"'{workItemTitle}' cannot start before '{predecessor.Title}' finishes ({expectedDate.Value:yyyy-MM-dd}).";
                    return CreateViolation();
                }
                if (IsStarted(candidate) && !IsCompleted(predecessor))
                {
                    message = $"'{workItemTitle}' cannot start until '{predecessor.Title}' is completed.";
                    return CreateViolation();
                }
                break;
        }

        return null;

        ProjectScheduleViolationDto CreateViolation() => new()
        {
            WorkItemId = workItemId,
            WorkItemTitle = workItemTitle,
            BlockingWorkItemId = predecessor.Id,
            BlockingWorkItemTitle = predecessor.Title,
            DependencyType = dependencyType,
            Severity = "High",
            Message = message,
            ExpectedDate = expectedDate
        };
    }

    private static (List<Guid> OrderedIds, bool HasCircularDependencies) TopologicallySortWorkItems(List<ProjectWorkItem> workItems, List<ProjectTaskDependency> dependencies)
    {
        var predecessorCounts = workItems.ToDictionary(x => x.Id, _ => 0);
        var successors = new Dictionary<Guid, List<Guid>>();

        foreach (var dependency in dependencies)
        {
            predecessorCounts[dependency.SuccessorWorkItemId] = predecessorCounts.GetValueOrDefault(dependency.SuccessorWorkItemId) + 1;
            if (!successors.TryGetValue(dependency.PredecessorWorkItemId, out var linked))
            {
                linked = new List<Guid>();
                successors[dependency.PredecessorWorkItemId] = linked;
            }

            linked.Add(dependency.SuccessorWorkItemId);
        }

        var queue = new Queue<Guid>(predecessorCounts.Where(x => x.Value == 0).Select(x => x.Key));
        var ordered = new List<Guid>();
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            ordered.Add(current);
            if (!successors.TryGetValue(current, out var linked))
            {
                continue;
            }

            foreach (var successor in linked)
            {
                predecessorCounts[successor]--;
                if (predecessorCounts[successor] <= 0)
                {
                    queue.Enqueue(successor);
                }
            }
        }

        return (ordered, ordered.Count < workItems.Count);
    }

    private static bool WouldCreateDependencyCycle(List<ProjectTaskDependency> dependencies, Guid predecessorId, Guid successorId)
    {
        var graph = dependencies
            .GroupBy(x => x.PredecessorWorkItemId)
            .ToDictionary(x => x.Key, x => x.Select(y => y.SuccessorWorkItemId).ToList());
        if (!graph.TryGetValue(predecessorId, out var items))
        {
            items = new List<Guid>();
            graph[predecessorId] = items;
        }

        items.Add(successorId);
        var queue = new Queue<Guid>();
        var visited = new HashSet<Guid>();
        queue.Enqueue(successorId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current))
            {
                continue;
            }

            if (current == predecessorId)
            {
                return true;
            }

            if (!graph.TryGetValue(current, out var linked))
            {
                continue;
            }

            foreach (var next in linked)
            {
                queue.Enqueue(next);
            }
        }

        return false;
    }

    private static bool ApplyDependencyDates(ProjectWorkItem predecessor, ProjectWorkItem successor, ProjectTaskDependency dependency)
    {
        var duration = Math.Max(1, (successor.PlannedEndDate?.Date - successor.PlannedStartDate?.Date)?.Days + 1 ?? 1);
        var dependencyType = NormalizeDependencyType(dependency.DependencyType);
        DateTime? requiredStart = null;
        DateTime? requiredEnd = null;

        switch (dependencyType)
        {
            case "SS":
                requiredStart = predecessor.PlannedStartDate?.Date.AddDays(dependency.LagDays);
                break;
            case "FF":
                requiredEnd = predecessor.PlannedEndDate?.Date.AddDays(dependency.LagDays);
                break;
            case "SF":
                requiredEnd = predecessor.PlannedStartDate?.Date.AddDays(dependency.LagDays);
                break;
            default:
                requiredStart = predecessor.PlannedEndDate?.Date.AddDays(dependency.LagDays);
                break;
        }

        var changed = false;
        if (requiredStart.HasValue && (!successor.PlannedStartDate.HasValue || successor.PlannedStartDate.Value.Date < requiredStart.Value))
        {
            successor.PlannedStartDate = requiredStart.Value;
            successor.PlannedEndDate = requiredStart.Value.AddDays(duration - 1);
            changed = true;
        }

        if (requiredEnd.HasValue && (!successor.PlannedEndDate.HasValue || successor.PlannedEndDate.Value.Date < requiredEnd.Value))
        {
            successor.PlannedEndDate = requiredEnd.Value;
            successor.PlannedStartDate = requiredEnd.Value.AddDays(-(duration - 1));
            changed = true;
        }

        return changed;
    }

    private static string NormalizeDependencyType(string? dependencyType)
        => string.IsNullOrWhiteSpace(dependencyType) ? "FS" : dependencyType.Trim().ToUpperInvariant();

    private static bool IsStarted(CreateProjectWorkItemDto workItem)
        => workItem.PercentComplete > 0m
            || workItem.ActualStartDate.HasValue
            || !string.Equals(workItem.Status, "New", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(workItem.Status, "Assigned", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(workItem.Status, "Draft", StringComparison.OrdinalIgnoreCase);

    private static bool IsCompleted(ProjectWorkItem workItem)
        => workItem.PercentComplete >= 100m
            || workItem.ActualEndDate.HasValue
            || string.Equals(workItem.Status, "Completed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(workItem.Status, "Closed", StringComparison.OrdinalIgnoreCase);

    private async Task ApplyBaselineMetadataAsync(ProjectDetailDto dto)
    {
        var activeBaseline = dto.Baselines
            .Where(x => x.IsLocked)
            .OrderByDescending(x => x.CreatedOn)
            .FirstOrDefault();
        if (activeBaseline == null)
        {
            return;
        }

        dto.ActiveBaselineId = activeBaseline.Id;
        dto.ActiveBaselineName = activeBaseline.Name;
        dto.ActiveBaselineCreatedOn = activeBaseline.CreatedOn;
        dto.HasLockedBaseline = true;

        var comparison = await CompareBaselineAsync(activeBaseline.Id);
        var workItemLookup = comparison.WorkItemChanges.ToDictionary(x => x.WorkItemId);
        ApplyBaselineMetadata(dto.WorkItems, workItemLookup);
    }

    private static void ApplyBaselineMetadata(IEnumerable<ProjectWorkItemDto> items, IReadOnlyDictionary<Guid, ProjectBaselineWorkItemChangeDto> changes)
    {
        foreach (var item in items)
        {
            if (changes.TryGetValue(item.Id, out var change))
            {
                item.BaselinePlannedStartDate = change.BaselinePlannedStartDate;
                item.BaselinePlannedEndDate = change.BaselinePlannedEndDate;
                item.BaselineVarianceDays = change.ScheduleVarianceDays;
                item.IsOffBaseline = change.ScheduleVarianceDays != 0
                    || change.BaselinePlannedStartDate != change.CurrentPlannedStartDate
                    || change.BaselinePlannedEndDate != change.CurrentPlannedEndDate;
            }

            if (item.Children.Count > 0)
            {
                ApplyBaselineMetadata(item.Children, changes);
            }
        }
    }

    private async Task PublishActivityAsync(Project project, string activity, Dictionary<string, object>? data = null, string audience = "Internal")
    {
        try
        {
            var eventData = new Dictionary<string, object>
            {
                ["ProjectId"] = project.Id,
                ["ProjectCode"] = project.ProjectCode,
                ["Title"] = project.Title,
                ["Status"] = project.Status
            };

            if (project.BusinessPartnerId.HasValue)
            {
                eventData["BusinessPartnerId"] = project.BusinessPartnerId.Value;
            }

            if (data != null)
            {
                foreach (var pair in data)
                {
                    eventData[pair.Key] = pair.Value;
                }
            }

            await _appEventBus.PublishAsync(new EntityActivityEvent
            {
                TenantId = project.TenantId,
                EntityType = "Project",
                Activity = activity,
                Audience = audience,
                EntityId = project.Id,
                TriggeredByUserId = _currentUserProvider.UserId,
                Data = eventData
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish project activity {Activity} for project {ProjectId}", activity, project.Id);
        }
    }

    private static ProjectLookupDto MapToLookupDto(Project entity) => new() { Id = entity.Id, ProjectCode = entity.ProjectCode, Title = entity.Title, Status = entity.Status, ProjectTypeName = entity.ProjectType?.Name, PortfolioName = entity.Portfolio?.Name, ProgramName = entity.Program?.Name };
    private static ProjectDto MapToDto(Project entity) => new() { Id = entity.Id, ProjectCode = entity.ProjectCode, Title = entity.Title, Status = entity.Status, Summary = entity.Summary, ProjectTypeName = entity.ProjectType?.Name, ProjectPriorityName = entity.ProjectPriority?.Name, PortfolioId = entity.PortfolioId, PortfolioName = entity.Portfolio?.Name, ProgramId = entity.ProgramId, ProgramName = entity.Program?.Name, ProjectManagerId = entity.ProjectManagerId, SponsorId = entity.SponsorId, StartDate = entity.StartDate, TargetEndDate = entity.TargetEndDate, EstimatedBudget = entity.EstimatedBudget, ApprovedBudget = entity.ApprovedBudget, ActualCost = entity.ActualCost, ProgressPercent = entity.ProgressPercent, ExternalPortalAccessEnabled = entity.ExternalPortalAccessEnabled, ExternalCollaborationEnabled = entity.ExternalCollaborationEnabled, CreatedAt = entity.CreatedAt };
    private static ProjectDetailDto MapToDetailDto(Project entity) => new()
    {
        Id = entity.Id,
        ProjectCode = entity.ProjectCode,
        Title = entity.Title,
        Status = entity.Status,
        Summary = entity.Summary,
        ProjectTypeId = entity.ProjectTypeId,
        ProjectTypeName = entity.ProjectType?.Name,
        ProjectPriorityId = entity.ProjectPriorityId,
        ProjectPriorityName = entity.ProjectPriority?.Name,
        TemplateId = entity.TemplateId,
        PortfolioId = entity.PortfolioId,
        PortfolioName = entity.Portfolio?.Name,
        ProgramId = entity.ProgramId,
        ProgramName = entity.Program?.Name,
        BusinessCase = entity.BusinessCase,
        Objectives = entity.Objectives,
        StrategicAlignment = entity.StrategicAlignment,
        Methodology = entity.Methodology,
        SponsorId = entity.SponsorId,
        ProjectManagerId = entity.ProjectManagerId,
        DepartmentId = entity.DepartmentId,
        LocationId = entity.LocationId,
        CustomerId = entity.CustomerId,
        BusinessPartnerId = entity.BusinessPartnerId,
        ContractId = entity.ContractId,
        TenderId = entity.TenderId,
        StartDate = entity.StartDate,
        TargetEndDate = entity.TargetEndDate,
        ActualStartDate = entity.ActualStartDate,
        ActualEndDate = entity.ActualEndDate,
        EstimatedBudget = entity.EstimatedBudget,
        ApprovedBudget = entity.ApprovedBudget,
        ActualCost = entity.ActualCost,
        BudgetStatus = entity.BudgetStatus,
        ProgressPercent = entity.ProgressPercent,
        ApprovalRequired = entity.ApprovalRequired,
        SubmittedAt = entity.SubmittedAt,
        ApprovedAt = entity.ApprovedAt,
        ScopeStatement = entity.ScopeStatement,
        Assumptions = entity.Assumptions,
        Constraints = entity.Constraints,
        ExpectedBenefits = entity.ExpectedBenefits,
        FundingSource = entity.FundingSource,
        StatusRemarks = entity.StatusRemarks,
        ExternalPortalAccessEnabled = entity.ExternalPortalAccessEnabled,
        ExternalCollaborationEnabled = entity.ExternalCollaborationEnabled,
        CreatedAt = entity.CreatedAt,
        HasLockedBaseline = entity.Baselines.Any(x => x.IsLocked),
        DevelopmentProfile = entity.DevelopmentProfile == null ? null : MapToDto(entity.DevelopmentProfile),
        Members = entity.Members.OrderBy(x => x.JoinedAt).Select(MapToDto).ToList(),
        Phases = MapToPhaseTree(entity.Phases),
        Milestones = entity.Milestones.OrderBy(x => x.TargetDate).Select(MapToDto).ToList(),
        ResourceAllocations = entity.ResourceAllocations.OrderBy(x => x.StartDate).ThenBy(x => x.UserId).Select(x => MapToDto(x, entity.ResourceAllocations.ToList())).ToList(),
        Risks = entity.Risks.OrderByDescending(x => x.Exposure).Select(MapToDto).ToList(),
        Issues = entity.Issues.OrderBy(x => x.TargetResolutionDate).Select(MapToDto).ToList(),
        ChangeRequests = entity.ChangeRequests.OrderByDescending(x => x.CreatedAt).Select(MapToDto).ToList(),
        BillingSchedules = entity.BillingSchedules.OrderBy(x => x.BillingDate).Select(MapToDto).ToList(),
        InvoiceRequests = entity.InvoiceRequests.OrderByDescending(x => x.RequestedAt).Select(MapToDto).ToList(),
        Documents = entity.Documents.OrderByDescending(x => x.CreatedAt).Select(MapToDto).ToList(),
        Comments = entity.Comments.OrderByDescending(x => x.CreatedAt).Select(MapToDto).ToList(),
        InitiationVersions = entity.InitiationVersions.OrderByDescending(x => x.VersionNumber).Select(MapToDto).ToList()
    };
    private static ProjectInitiationVersionDto MapToDto(ProjectInitiationVersion entity) => new() { Id = entity.Id, VersionNumber = entity.VersionNumber, ChangeType = entity.ChangeType, Notes = entity.Notes, CreatedAt = entity.CreatedAt };
    private static ProjectMemberDto MapToDto(ProjectMember entity) => new() { Id = entity.Id, UserId = entity.UserId, Role = entity.Role, IsActive = entity.IsActive, JoinedAt = entity.JoinedAt };
    private static ProjectWorkItemDto MapToDto(ProjectWorkItem entity) => new() { Id = entity.Id, ProjectId = entity.ProjectId, ParentId = entity.ParentId, NodeType = entity.NodeType, Title = entity.Title, Description = entity.Description, Status = entity.Status, Priority = entity.Priority, SortOrder = entity.SortOrder, AssignedToUserId = entity.AssignedToUserId, PlannedStartDate = entity.PlannedStartDate, PlannedEndDate = entity.PlannedEndDate, ActualStartDate = entity.ActualStartDate, ActualEndDate = entity.ActualEndDate, PercentComplete = entity.PercentComplete, IsRollupEnabled = entity.IsRollupEnabled, EffortEstimateHours = entity.EffortEstimateHours, ActualEffortHours = entity.ActualEffortHours, BaselineVarianceDays = 0, IsOffBaseline = false, CanExternalUpdate = false, CanExternalComment = false };
    private static ProjectMilestoneDto MapToDto(ProjectMilestone entity) => new() { Id = entity.Id, ProjectId = entity.ProjectId, WorkItemId = entity.WorkItemId, Title = entity.Title, Description = entity.Description, TargetDate = entity.TargetDate, ActualDate = entity.ActualDate, Status = entity.Status, RequiresApproval = entity.RequiresApproval };
    private static ProjectResourceAllocationDto MapToDto(ProjectResourceAllocation entity, List<ProjectResourceAllocation> allocations) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        WorkItemId = entity.WorkItemId,
        UserId = entity.UserId,
        AllocationRole = entity.AllocationRole,
        AllocationType = entity.AllocationType,
        AllocationValue = entity.AllocationValue,
        PlannedHours = entity.PlannedHours,
        StartDate = entity.StartDate,
        EndDate = entity.EndDate,
        BookingType = entity.BookingType,
        Status = entity.Status,
        Notes = entity.Notes,
        RequiredSkills = DeserializeJsonList(entity.RequiredSkillsJson),
        RequiredCertifications = DeserializeJsonList(entity.RequiredCertificationsJson),
        RoutingPolicy = NormalizeRoutingPolicy(entity.RoutingPolicy),
        SourceAllocationId = entity.SourceAllocationId,
        ReplacementAllocationId = entity.ReplacementAllocationId,
        SubstitutionReason = entity.SubstitutionReason,
        CanSubstitute = IsAllocationActiveForCapacity(entity),
        HasConflict = HasAllocationConflict(entity, allocations),
        CapacityUtilizationPercent = decimal.Round(
            string.Equals(entity.AllocationType, "Percent", StringComparison.OrdinalIgnoreCase)
                ? entity.AllocationValue
                : GetCapacityUtilizationPercent(GetEffectiveHours(entity), entity.StartDate, entity.EndDate),
            2)
    };
    private static ProjectRiskDto MapToDto(ProjectRisk entity) => new() { Id = entity.Id, Title = entity.Title, Description = entity.Description, OwnerId = entity.OwnerId, Status = entity.Status, Category = entity.Category, Probability = entity.Probability, Impact = entity.Impact, Exposure = entity.Exposure, ResponseStrategy = entity.ResponseStrategy, MitigationPlan = entity.MitigationPlan, DueDate = entity.DueDate };
    private static ProjectIssueDto MapToDto(ProjectIssue entity) => new() { Id = entity.Id, Title = entity.Title, Description = entity.Description, OwnerId = entity.OwnerId, Status = entity.Status, Severity = entity.Severity, TargetResolutionDate = entity.TargetResolutionDate, RootCause = entity.RootCause, CorrectiveAction = entity.CorrectiveAction };
    private static ProjectQualityCheckpointDto MapToDto(ProjectQualityCheckpoint entity) => new() { Id = entity.Id, ProjectId = entity.ProjectId, WorkItemId = entity.WorkItemId, DeliverableId = entity.DeliverableId, QaOwnerId = entity.QaOwnerId, Title = entity.Title, Description = entity.Description, Status = entity.Status, DueDate = entity.DueDate, RequiresQaSignOff = entity.RequiresQaSignOff, SignedOffAt = entity.SignedOffAt, SignedOffById = entity.SignedOffById, SignOffNotes = entity.SignOffNotes };
    private static ProjectNonConformanceDto MapToDto(ProjectNonConformance entity) => new() { Id = entity.Id, ProjectId = entity.ProjectId, QualityCheckpointId = entity.QualityCheckpointId, DeliverableId = entity.DeliverableId, OwnerId = entity.OwnerId, Title = entity.Title, Description = entity.Description, Severity = entity.Severity, Status = entity.Status, ReportedAt = entity.ReportedAt, TargetResolutionDate = entity.TargetResolutionDate, ResolvedAt = entity.ResolvedAt, CorrectiveAction = entity.CorrectiveAction, PreventiveAction = entity.PreventiveAction, ResolutionNotes = entity.ResolutionNotes };
    private static ProjectChangeRequestDto MapToDto(ProjectChangeRequest entity) => new() { Id = entity.Id, Title = entity.Title, Description = entity.Description, ChangeType = entity.ChangeType, Status = entity.Status, BusinessImpact = entity.BusinessImpact, RiskImpact = entity.RiskImpact, CostImpact = entity.CostImpact, ScheduleImpactDays = entity.ScheduleImpactDays };
    private static ProjectBillingScheduleDto MapToDto(ProjectBillingSchedule entity) => new() { Id = entity.Id, ProjectId = entity.ProjectId, ContractId = entity.ContractId, ContractMilestoneId = entity.ContractMilestoneId, MilestoneId = entity.MilestoneId, Name = entity.Name, BillingType = entity.BillingType, Amount = entity.Amount, BillingPercentage = entity.BillingPercentage, BillingDate = entity.BillingDate, Status = entity.Status, Description = entity.Description, IsBillable = entity.IsBillable };
    private static ProjectInvoiceRequestDto MapToDto(ProjectInvoiceRequest entity) => new() { Id = entity.Id, ProjectId = entity.ProjectId, BillingScheduleId = entity.BillingScheduleId, ContractId = entity.ContractId, RequestNumber = entity.RequestNumber, RequestedAmount = entity.RequestedAmount, Currency = entity.Currency, Status = entity.Status, RequestedAt = entity.RequestedAt, SubmittedAt = entity.SubmittedAt, ExternalReference = entity.ExternalReference, Notes = entity.Notes };
    private static ProjectDeliverableDto MapToDto(ProjectDeliverable entity) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        WorkItemId = entity.WorkItemId,
        MilestoneId = entity.MilestoneId,
        SubmittedDocumentId = entity.SubmittedDocumentId,
        Title = entity.Title,
        Description = entity.Description,
        Status = entity.Status,
        TargetDate = entity.TargetDate,
        SubmittedAt = entity.SubmittedAt,
        SubmittedById = entity.SubmittedById,
        ExternalApprovedAt = entity.ExternalApprovedAt,
        ExternalApprovedById = entity.ExternalApprovedById,
        ApprovedAt = entity.ApprovedAt,
        ApprovedById = entity.ApprovedById,
        ExternalSubmissionAllowed = entity.ExternalSubmissionAllowed,
        ExternalSignOffRequired = entity.ExternalSignOffRequired,
        IsExternalVisible = entity.IsExternalVisible,
        AcceptanceNotes = entity.AcceptanceNotes,
        ExternalApprovalNotes = entity.ExternalApprovalNotes,
        CanExternalSubmit = false,
        CanExternalApprove = false
    };
    private static ProjectDeliverableExternalReviewDto MapToDto(ProjectDeliverableExternalReview entity, IReadOnlyDictionary<Guid, ProjectDocument> documents) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        DeliverableId = entity.DeliverableId,
        ReviewDate = entity.ReviewDate,
        ReviewedById = entity.ReviewedById,
        SubmittedDocumentId = entity.SubmittedDocumentId,
        SubmittedDocumentName = entity.SubmittedDocumentId.HasValue && documents.TryGetValue(entity.SubmittedDocumentId.Value, out var document)
            ? document.DocumentName
            : null,
        Decision = entity.Decision,
        StatusSnapshot = entity.StatusSnapshot,
        Notes = entity.Notes
    };
    private static ProjectTaskDependencyDto MapToDto(ProjectTaskDependency entity) => new() { Id = entity.Id, ProjectId = entity.ProjectId, PredecessorWorkItemId = entity.PredecessorWorkItemId, SuccessorWorkItemId = entity.SuccessorWorkItemId, DependencyType = entity.DependencyType, LagDays = entity.LagDays, IsEnforced = entity.IsEnforced };
    private static ProjectInterdependencyDto MapToDto(ProjectInterdependency entity, IReadOnlyDictionary<Guid, Project> projects) => new()
    {
        Id = entity.Id,
        SourceProjectId = entity.SourceProjectId,
        SourceProjectCode = projects.TryGetValue(entity.SourceProjectId, out var sourceProject) ? sourceProject.ProjectCode : string.Empty,
        SourceProjectTitle = projects.TryGetValue(entity.SourceProjectId, out sourceProject) ? sourceProject.Title : string.Empty,
        TargetProjectId = entity.TargetProjectId,
        TargetProjectCode = projects.TryGetValue(entity.TargetProjectId, out var targetProject) ? targetProject.ProjectCode : string.Empty,
        TargetProjectTitle = projects.TryGetValue(entity.TargetProjectId, out targetProject) ? targetProject.Title : string.Empty,
        DependencyType = entity.DependencyType,
        Status = entity.Status,
        ImpactLevel = entity.ImpactLevel,
        OwnerId = entity.OwnerId,
        DueDate = entity.DueDate,
        Title = entity.Title,
        Description = entity.Description,
        MitigationPlan = entity.MitigationPlan
    };
    private static ProjectBaselineDto MapToDto(ProjectBaseline entity)
    {
        using var document = JsonDocument.Parse(entity.SnapshotJson);
        var root = document.RootElement;
        var snapshotBudget = root.TryGetProperty("ApprovedBudget", out var budget) && budget.ValueKind != JsonValueKind.Null ? budget.GetDecimal() : (decimal?)null;
        var snapshotProgress = root.TryGetProperty("ProgressPercent", out var progress) ? progress.GetDecimal() : 0m;
        var finishDate = root.TryGetProperty("Milestones", out var milestonesJson) && milestonesJson.ValueKind == JsonValueKind.Array && milestonesJson.GetArrayLength() > 0
            ? milestonesJson.EnumerateArray().Max(x => x.GetProperty("TargetDate").GetDateTime())
            : (DateTime?)null;
        return new ProjectBaselineDto { Id = entity.Id, ProjectId = entity.ProjectId, Name = entity.Name, Notes = entity.Notes, IsLocked = entity.IsLocked, CreatedOn = entity.CreatedOn, SnapshotProgressPercent = snapshotProgress, SnapshotApprovedBudget = snapshotBudget, SnapshotFinishDate = finishDate };
    }
    private static ProjectTimesheetEntryDto MapToDto(ProjectTimesheetEntry entity, Project? project = null, ProjectWorkItem? workItem = null) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        ProjectCode = project?.ProjectCode,
        ProjectTitle = project?.Title,
        WorkItemId = entity.WorkItemId,
        WorkItemTitle = workItem?.Title,
        UserId = entity.UserId,
        EntryDate = entity.EntryDate,
        Hours = entity.Hours,
        IsBillable = entity.IsBillable,
        HourlyRate = entity.HourlyRate,
        CostAmount = entity.CostAmount,
        WorkType = entity.WorkType,
        Notes = entity.Notes,
        Status = entity.Status,
        ApprovedById = entity.ApprovedById,
        ApprovedAt = entity.ApprovedAt,
        CanEdit = !string.Equals(entity.Status, "Submitted", StringComparison.OrdinalIgnoreCase) && !string.Equals(entity.Status, "Approved", StringComparison.OrdinalIgnoreCase),
        CanDelete = string.Equals(entity.Status, "Draft", StringComparison.OrdinalIgnoreCase) || string.Equals(entity.Status, "Rejected", StringComparison.OrdinalIgnoreCase)
    };
    private static ProjectExpenseDto MapToDto(ProjectExpense entity, Project? project = null, ProjectWorkItem? workItem = null) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        ProjectCode = project?.ProjectCode,
        ProjectTitle = project?.Title,
        WorkItemId = entity.WorkItemId,
        WorkItemTitle = workItem?.Title,
        UserId = entity.UserId,
        ExpenseDate = entity.ExpenseDate,
        Category = entity.Category,
        Currency = entity.Currency,
        Amount = entity.Amount,
        TaxAmount = entity.TaxAmount,
        IsBillable = entity.IsBillable,
        Status = entity.Status,
        ReceiptDocumentId = entity.ReceiptDocumentId,
        Notes = entity.Notes,
        ApprovedById = entity.ApprovedById,
        ApprovedAt = entity.ApprovedAt,
        CanEdit = !string.Equals(entity.Status, "Submitted", StringComparison.OrdinalIgnoreCase) && !string.Equals(entity.Status, "Approved", StringComparison.OrdinalIgnoreCase),
        CanDelete = string.Equals(entity.Status, "Draft", StringComparison.OrdinalIgnoreCase) || string.Equals(entity.Status, "Rejected", StringComparison.OrdinalIgnoreCase)
    };
    private static ProjectRevenueRecognitionDto MapToDto(ProjectRevenueRecognition entity) => new() { Id = entity.Id, ProjectId = entity.ProjectId, InvoiceRequestId = entity.InvoiceRequestId, RecognitionPeriod = entity.RecognitionPeriod, RecognizedRevenue = entity.RecognizedRevenue, RecognizedCost = entity.RecognizedCost, GrossMargin = entity.GrossMargin, CashCollected = entity.CashCollected, Status = entity.Status, Notes = entity.Notes };
    private static ProjectAssetLinkDto MapToDto(ProjectAssetLink entity, string? assetName, string? jobCardNumber) => new() { Id = entity.Id, ProjectId = entity.ProjectId, MaintenanceAssetId = entity.MaintenanceAssetId, CompanyAssetId = entity.CompanyAssetId, JobCardId = entity.JobCardId, LinkType = entity.LinkType, Status = entity.Status, Notes = entity.Notes, AssetName = assetName, JobCardNumber = jobCardNumber };
    private static ProjectExternalAccessPolicyDto MapToDto(ProjectExternalAccessPolicy entity) => new() { Id = entity.Id, ProjectId = entity.ProjectId, BusinessPartnerId = entity.BusinessPartnerId, ArtifactType = entity.ArtifactType, ArtifactId = entity.ArtifactId, AccessLevel = entity.AccessLevel, CanComment = entity.CanComment, CanUpload = entity.CanUpload, CanApprove = entity.CanApprove, Notes = entity.Notes };
    private static ProjectDecisionDto MapToDto(ProjectDecision entity) => new() { Id = entity.Id, ProjectId = entity.ProjectId, Title = entity.Title, DecisionDate = entity.DecisionDate, ApproverId = entity.ApproverId, Rationale = entity.Rationale, AlternativesConsidered = entity.AlternativesConsidered, ImpactSummary = entity.ImpactSummary, Status = entity.Status, ApprovedAt = entity.ApprovedAt };
    private static ProjectMeetingMinuteDto MapToDto(ProjectMeetingMinute entity) => new() { Id = entity.Id, ProjectId = entity.ProjectId, Title = entity.Title, MeetingDate = entity.MeetingDate, FacilitatorId = entity.FacilitatorId, MeetingType = entity.MeetingType, Minutes = entity.Minutes, AttendeesJson = entity.AttendeesJson };
    private static ProjectActionItemDto MapToDto(ProjectActionItem entity, ProjectMeetingMinute? meeting = null, ProjectWorkItem? workItem = null) => new() { Id = entity.Id, ProjectId = entity.ProjectId, MeetingMinuteId = entity.MeetingMinuteId, WorkItemId = entity.WorkItemId, Title = entity.Title, Description = entity.Description, OwnerId = entity.OwnerId, DueDate = entity.DueDate, CompletedAt = entity.CompletedAt, Status = entity.Status, Priority = entity.Priority, MeetingTitle = meeting?.Title, WorkItemTitle = workItem?.Title };
    private static ProjectLessonLearnedDto MapToDto(ProjectLessonLearned entity) => new() { Id = entity.Id, ProjectId = entity.ProjectId, Title = entity.Title, Category = entity.Category, Description = entity.Description, Recommendation = entity.Recommendation, AppliedPhase = entity.AppliedPhase, Visibility = entity.Visibility };
    private static ProjectClosureDto MapToDto(ProjectClosure entity) => new() { Id = entity.Id, ProjectId = entity.ProjectId, Status = entity.Status, SubmittedAt = entity.SubmittedAt, ApprovedAt = entity.ApprovedAt, ApprovedById = entity.ApprovedById, FinalBudget = entity.FinalBudget, FinalCost = entity.FinalCost, DeliverablesAccepted = entity.DeliverablesAccepted, TasksCompletedOrWaived = entity.TasksCompletedOrWaived, AssetsReconciled = entity.AssetsReconciled, OpenItemsDisposed = entity.OpenItemsDisposed, ClosureChecklistJson = entity.ClosureChecklistJson, OpenItemsDisposition = entity.OpenItemsDisposition, AssetReconciliationNotes = entity.AssetReconciliationNotes, LessonsLearnedSummary = entity.LessonsLearnedSummary, PostImplementationReview = entity.PostImplementationReview, OverrideReason = entity.OverrideReason, RejectionReason = entity.RejectionReason };
    private static ProjectDocumentDto MapToDto(ProjectDocument entity) => new() { Id = entity.Id, DocumentName = entity.DocumentName, Category = entity.Category, DocumentType = entity.DocumentType, FilePath = entity.FilePath, PublicUrl = entity.PublicUrl, FileType = entity.FileType, FileSize = entity.FileSize, VersionLabel = entity.VersionLabel, Status = entity.Status, IsExternalVisible = entity.IsExternalVisible, CreatedAt = entity.CreatedAt, ArtifactLabel = entity.DocumentName };
    private static ProjectCommentDto MapToDto(ProjectComment entity) => new() { Id = entity.Id, WorkItemId = entity.WorkItemId, CommentType = entity.CommentType, Body = entity.Body, MentionedUsersJson = entity.MentionedUsersJson, CreatedAt = entity.CreatedAt, CreatedBy = entity.CreatedBy };

    private async Task EnrichUserDisplayNamesAsync(ProjectDetailDto dto)
    {
        var userLookup = await BuildUserDisplayNameLookupAsync(CollectProjectUserIds(dto));

        dto.ProjectManagerDisplayName = ResolveUserDisplayName(userLookup, dto.ProjectManagerId);
        dto.SponsorDisplayName = ResolveUserDisplayName(userLookup, dto.SponsorId);

        foreach (var member in dto.Members)
        {
            member.UserDisplayName = ResolveUserDisplayName(userLookup, member.UserId);
        }

        foreach (var resource in dto.ResourceAllocations)
        {
            resource.UserDisplayName = ResolveUserDisplayName(userLookup, resource.UserId);
        }

        foreach (var risk in dto.Risks)
        {
            risk.OwnerDisplayName = ResolveUserDisplayName(userLookup, risk.OwnerId);
        }

        foreach (var issue in dto.Issues)
        {
            issue.OwnerDisplayName = ResolveUserDisplayName(userLookup, issue.OwnerId);
        }

        foreach (var checkpoint in dto.QualityCheckpoints)
        {
            checkpoint.QaOwnerDisplayName = ResolveUserDisplayName(userLookup, checkpoint.QaOwnerId);
            checkpoint.SignedOffByDisplayName = ResolveUserDisplayName(userLookup, checkpoint.SignedOffById);
        }

        foreach (var nonConformance in dto.NonConformances)
        {
            nonConformance.OwnerDisplayName = ResolveUserDisplayName(userLookup, nonConformance.OwnerId);
        }

        foreach (var entry in dto.TimesheetEntries)
        {
            entry.UserDisplayName = ResolveUserDisplayName(userLookup, entry.UserId);
            entry.ApprovedByDisplayName = ResolveUserDisplayName(userLookup, entry.ApprovedById);
        }

        foreach (var expense in dto.Expenses)
        {
            expense.UserDisplayName = ResolveUserDisplayName(userLookup, expense.UserId);
            expense.ApprovedByDisplayName = ResolveUserDisplayName(userLookup, expense.ApprovedById);
        }

        foreach (var decision in dto.Decisions)
        {
            decision.ApproverDisplayName = ResolveUserDisplayName(userLookup, decision.ApproverId);
        }

        foreach (var meeting in dto.Meetings)
        {
            meeting.FacilitatorDisplayName = ResolveUserDisplayName(userLookup, meeting.FacilitatorId);
        }

        foreach (var actionItem in dto.ActionItems)
        {
            actionItem.OwnerDisplayName = ResolveUserDisplayName(userLookup, actionItem.OwnerId);
        }

        EnrichWorkItemUserDisplayNames(dto.WorkItems, userLookup);
    }

    private static void EnrichWorkItemUserDisplayNames(IEnumerable<ProjectWorkItemDto> workItems, IReadOnlyDictionary<Guid, string> userLookup)
    {
        foreach (var workItem in workItems)
        {
            workItem.AssignedToUserDisplayName = ResolveUserDisplayName(userLookup, workItem.AssignedToUserId);
            if (workItem.Children.Count > 0)
            {
                EnrichWorkItemUserDisplayNames(workItem.Children, userLookup);
            }
        }
    }

    private async Task<Dictionary<Guid, string>> BuildUserDisplayNameLookupAsync(IEnumerable<Guid> userIds)
    {
        var distinctUserIds = userIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (distinctUserIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var users = await _userService.GetUsersByIdsAsync(distinctUserIds);

        return users
            .GroupBy(user => user.Id)
            .ToDictionary(group => group.Key, group => FormatUserDisplayName(group.First()));
    }

    private static IEnumerable<Guid> CollectProjectUserIds(ProjectDetailDto dto)
    {
        var userIds = new HashSet<Guid>();

        AddUserId(userIds, dto.ProjectManagerId);
        AddUserId(userIds, dto.SponsorId);

        foreach (var member in dto.Members)
        {
            AddUserId(userIds, member.UserId);
        }

        foreach (var workItem in FlattenWorkItems(dto.WorkItems))
        {
            AddUserId(userIds, workItem.AssignedToUserId);
        }

        foreach (var resource in dto.ResourceAllocations)
        {
            AddUserId(userIds, resource.UserId);
            AddUserId(userIds, resource.RecommendedUserId);
        }

        foreach (var risk in dto.Risks)
        {
            AddUserId(userIds, risk.OwnerId);
        }

        foreach (var issue in dto.Issues)
        {
            AddUserId(userIds, issue.OwnerId);
        }

        foreach (var checkpoint in dto.QualityCheckpoints)
        {
            AddUserId(userIds, checkpoint.QaOwnerId);
            AddUserId(userIds, checkpoint.SignedOffById);
        }

        foreach (var nonConformance in dto.NonConformances)
        {
            AddUserId(userIds, nonConformance.OwnerId);
        }

        foreach (var entry in dto.TimesheetEntries)
        {
            AddUserId(userIds, entry.UserId);
            AddUserId(userIds, entry.ApprovedById);
        }

        foreach (var expense in dto.Expenses)
        {
            AddUserId(userIds, expense.UserId);
            AddUserId(userIds, expense.ApprovedById);
        }

        foreach (var decision in dto.Decisions)
        {
            AddUserId(userIds, decision.ApproverId);
        }

        foreach (var meeting in dto.Meetings)
        {
            AddUserId(userIds, meeting.FacilitatorId);
        }

        foreach (var actionItem in dto.ActionItems)
        {
            AddUserId(userIds, actionItem.OwnerId);
        }

        return userIds;
    }

    private static IEnumerable<ProjectWorkItemDto> FlattenWorkItems(IEnumerable<ProjectWorkItemDto> workItems)
    {
        foreach (var workItem in workItems)
        {
            yield return workItem;

            foreach (var child in FlattenWorkItems(workItem.Children))
            {
                yield return child;
            }
        }
    }

    private static void AddUserId(ISet<Guid> userIds, Guid? userId)
    {
        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            userIds.Add(userId.Value);
        }
    }

    private static string? ResolveUserDisplayName(IReadOnlyDictionary<Guid, string> userLookup, Guid? userId)
    {
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return null;
        }

        return userLookup.TryGetValue(userId.Value, out var displayName)
            ? displayName
            : null;
    }

    private static string FormatUserDisplayName(ApplicationUser user)
    {
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return user.UserName ?? user.Email ?? user.Id.ToString();
        }

        return string.IsNullOrWhiteSpace(user.UserName)
            ? fullName
            : $"{fullName} ({user.UserName})";
    }

    private async Task<ProjectExternalAccessPolicyDto> MapToDtoAsync(Project project, ProjectExternalAccessPolicy entity)
    {
        var dto = MapToDto(entity);
        dto.BusinessPartnerName = (await _businessPartnerService.GetByIdAsync(entity.BusinessPartnerId))?.PartnerName;
        dto.ArtifactLabel = await ResolveExternalArtifactLabelAsync(project, entity.ArtifactType, entity.ArtifactId);
        return dto;
    }

    private async Task<string?> ResolveExternalArtifactLabelAsync(Project project, string artifactType, Guid? artifactId)
    {
        var normalized = NormalizeExternalArtifactType(artifactType);
        if (string.Equals(normalized, "Project", StringComparison.OrdinalIgnoreCase) || !artifactId.HasValue)
        {
            return $"{project.ProjectCode} - {project.Title}";
        }

        return normalized switch
        {
            "WorkItem" => (await _unitOfWork.Repository<ProjectWorkItem>().FirstOrDefaultAsync(x => x.Id == artifactId.Value && x.ProjectId == project.Id && x.TenantId == _currentUserProvider.TenantId))?.Title,
            "Deliverable" => (await _unitOfWork.Repository<ProjectDeliverable>().FirstOrDefaultAsync(x => x.Id == artifactId.Value && x.ProjectId == project.Id && x.TenantId == _currentUserProvider.TenantId))?.Title,
            "Document" => (await _unitOfWork.Repository<ProjectDocument>().FirstOrDefaultAsync(x => x.Id == artifactId.Value && x.ProjectId == project.Id && x.TenantId == _currentUserProvider.TenantId))?.DocumentName,
            _ => artifactId.Value.ToString()
        };
    }
}

public class ProjectSetupService : IProjectSetupService
{
    private readonly IProjectTypeRepository _projectTypeRepository;
    private readonly IProjectPriorityRepository _projectPriorityRepository;
    private readonly IProjectTemplateRepository _projectTemplateRepository;
    private readonly IProjectPortfolioRepository _projectPortfolioRepository;
    private readonly IProjectProgramRepository _projectProgramRepository;
    private readonly IProjectCatalogRepository _projectCatalogRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;

    public ProjectSetupService(
        IProjectTypeRepository projectTypeRepository,
        IProjectPriorityRepository projectPriorityRepository,
        IProjectTemplateRepository projectTemplateRepository,
        IProjectPortfolioRepository projectPortfolioRepository,
        IProjectProgramRepository projectProgramRepository,
        IProjectCatalogRepository projectCatalogRepository,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork)
    {
        _projectTypeRepository = projectTypeRepository;
        _projectPriorityRepository = projectPriorityRepository;
        _projectTemplateRepository = projectTemplateRepository;
        _projectPortfolioRepository = projectPortfolioRepository;
        _projectProgramRepository = projectProgramRepository;
        _projectCatalogRepository = projectCatalogRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProjectMasterDataOverviewDto> GetMasterDataOverviewAsync()
    {
        EnsureAdministrationAccess();
        var typeCount = (await _projectTypeRepository.GetAllAsync()).Count();
        var priorityCount = (await _projectPriorityRepository.GetAllAsync()).Count();
        var templateCount = (await _projectTemplateRepository.GetActiveAsync()).Count();
        var portfolioCount = (await _projectPortfolioRepository.GetAllAsync()).Count();
        var programCount = (await _projectProgramRepository.GetAllAsync()).Count();
        var configuredCatalogs = (await _projectCatalogRepository.GetAllAsync()).ToList();
        var recommendedCatalogs = ProjectCatalogDefaults.GetRecommendedCatalogs();

        return new ProjectMasterDataOverviewDto
        {
            ProjectTypeCount = typeCount,
            ProjectPriorityCount = priorityCount,
            ProjectTemplateCount = templateCount,
            PortfolioCount = portfolioCount,
            ProgramCount = programCount,
            RecommendedCatalogs = recommendedCatalogs,
            CatalogCoverage = recommendedCatalogs.Select(group => new ProjectCatalogTypeSummaryDto
            {
                CatalogType = group.Key,
                DisplayName = group.DisplayName,
                ConfiguredCount = configuredCatalogs.Count(x => string.Equals(x.CatalogType, group.Key, StringComparison.OrdinalIgnoreCase) && x.IsActive),
                RecommendedCount = group.Items.Count
            }).ToList()
        };
    }

    public async Task<IEnumerable<ProjectCatalogEntryDto>> GetCatalogEntriesAsync(string catalogType)
    {
        EnsureAdministrationAccess();
        var normalizedCatalogType = NormalizeCatalogType(catalogType);
        return (await _projectCatalogRepository.GetByCatalogTypeAsync(normalizedCatalogType)).Select(MapToDto);
    }

    public async Task<ProjectCatalogEntryDto> CreateCatalogEntryAsync(CreateProjectCatalogEntryDto dto)
    {
        EnsureAdministrationAccess();
        var normalizedCatalogType = NormalizeCatalogType(dto.CatalogType);
        var normalizedCode = NormalizeCatalogCode(dto.Code);
        if (await _projectCatalogRepository.GetByCodeAsync(normalizedCatalogType, normalizedCode) != null)
        {
            throw new InvalidOperationException($"A project catalog entry with code '{normalizedCode}' already exists in '{normalizedCatalogType}'.");
        }

        var entity = new ProjectCatalogEntry
        {
            TenantId = _currentUserProvider.TenantId,
            CatalogType = normalizedCatalogType,
            Code = normalizedCode,
            Name = NormalizeCatalogName(dto.Name),
            Description = dto.Description?.Trim(),
            SortOrder = dto.SortOrder,
            IsActive = dto.IsActive,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await _projectCatalogRepository.CreateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task<ProjectCatalogEntryDto> UpdateCatalogEntryAsync(Guid id, CreateProjectCatalogEntryDto dto)
    {
        EnsureAdministrationAccess();
        var entity = await _projectCatalogRepository.GetByIdAsync(id) ?? throw new InvalidOperationException($"Project catalog entry with ID {id} not found");
        var normalizedCatalogType = NormalizeCatalogType(dto.CatalogType);
        var normalizedCode = NormalizeCatalogCode(dto.Code);
        var existing = await _projectCatalogRepository.GetByCodeAsync(normalizedCatalogType, normalizedCode);
        if (existing != null && existing.Id != id)
        {
            throw new InvalidOperationException($"A project catalog entry with code '{normalizedCode}' already exists in '{normalizedCatalogType}'.");
        }

        entity.CatalogType = normalizedCatalogType;
        entity.Code = normalizedCode;
        entity.Name = NormalizeCatalogName(dto.Name);
        entity.Description = dto.Description?.Trim();
        entity.SortOrder = dto.SortOrder;
        entity.IsActive = dto.IsActive;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;

        await _projectCatalogRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(entity);
    }

    public async Task DeleteCatalogEntryAsync(Guid id)
    {
        EnsureAdministrationAccess();
        await _projectCatalogRepository.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task SeedCatalogDefaultsAsync(string? catalogType = null)
    {
        EnsureAdministrationAccess();
        var catalogGroups = string.IsNullOrWhiteSpace(catalogType)
            ? ProjectCatalogDefaults.GetRecommendedCatalogs()
            : [ProjectCatalogDefaults.GetRecommendedCatalog(catalogType)];

        foreach (var group in catalogGroups)
        {
            var existing = (await _projectCatalogRepository.GetByCatalogTypeAsync(group.Key))
                .ToDictionary(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < group.Items.Count; index++)
            {
                var item = group.Items[index];
                if (existing.ContainsKey(item.Code))
                {
                    continue;
                }

                await _projectCatalogRepository.CreateAsync(new ProjectCatalogEntry
                {
                    TenantId = _currentUserProvider.TenantId,
                    CatalogType = group.Key,
                    Code = item.Code,
                    Name = item.Name,
                    SortOrder = (index + 1) * 10,
                    IsActive = true,
                    CreatedBy = _currentUserProvider.Username,
                    CreatedById = _currentUserProvider.UserId
                });
            }
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectTypeDto>> GetProjectTypesAsync()
    {
        EnsureAdministrationAccess();
        return (await _projectTypeRepository.GetAllAsync()).Select(x => new ProjectTypeDto { Id = x.Id, Code = x.Code, Name = x.Name, Description = x.Description, IsActive = x.IsActive, RequiresSponsor = x.RequiresSponsor, RequiresApproval = x.RequiresApproval, MandatoryFieldsJson = x.MandatoryFieldsJson });
    }
    public async Task<ProjectTypeDto> CreateProjectTypeAsync(CreateProjectTypeDto dto)
    {
        EnsureAdministrationAccess();
        var entity = new ProjectType { TenantId = _currentUserProvider.TenantId, Code = dto.Code, Name = dto.Name, Description = dto.Description, IsActive = dto.IsActive, RequiresSponsor = dto.RequiresSponsor, RequiresApproval = dto.RequiresApproval, MandatoryFieldsJson = dto.MandatoryFieldsJson, CreatedBy = _currentUserProvider.Username, CreatedById = _currentUserProvider.UserId };
        await _projectTypeRepository.CreateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return (await GetProjectTypesAsync()).First(x => x.Id == entity.Id);
    }
    public async Task<ProjectTypeDto> UpdateProjectTypeAsync(Guid id, CreateProjectTypeDto dto)
    {
        EnsureAdministrationAccess();
        var entity = await _projectTypeRepository.GetByIdAsync(id) ?? throw new InvalidOperationException($"Project type with ID {id} not found");
        entity.Code = dto.Code; entity.Name = dto.Name; entity.Description = dto.Description; entity.IsActive = dto.IsActive; entity.RequiresSponsor = dto.RequiresSponsor; entity.RequiresApproval = dto.RequiresApproval; entity.MandatoryFieldsJson = dto.MandatoryFieldsJson; entity.UpdatedBy = _currentUserProvider.Username; entity.LastModifiedById = _currentUserProvider.UserId;
        await _projectTypeRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return (await GetProjectTypesAsync()).First(x => x.Id == entity.Id);
    }
    public async Task DeleteProjectTypeAsync(Guid id)
    {
        EnsureAdministrationAccess();
        await _projectTypeRepository.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectPriorityDto>> GetProjectPrioritiesAsync()
    {
        EnsureAdministrationAccess();
        await EnsureDefaultPrioritiesAsync();
        return (await _projectPriorityRepository.GetAllAsync()).Select(x => new ProjectPriorityDto { Id = x.Id, Code = x.Code, Name = x.Name, ColorHex = x.ColorHex, SortOrder = x.SortOrder, IsActive = x.IsActive });
    }
    public async Task<ProjectPriorityDto> CreateProjectPriorityAsync(CreateProjectPriorityDto dto)
    {
        EnsureAdministrationAccess();
        var entity = new ProjectPriority { TenantId = _currentUserProvider.TenantId, Code = dto.Code, Name = dto.Name, ColorHex = dto.ColorHex, SortOrder = dto.SortOrder, IsActive = dto.IsActive, CreatedBy = _currentUserProvider.Username, CreatedById = _currentUserProvider.UserId };
        await _projectPriorityRepository.CreateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return (await GetProjectPrioritiesAsync()).First(x => x.Id == entity.Id);
    }
    public async Task<ProjectPriorityDto> UpdateProjectPriorityAsync(Guid id, CreateProjectPriorityDto dto)
    {
        EnsureAdministrationAccess();
        var entity = await _projectPriorityRepository.GetByIdAsync(id) ?? throw new InvalidOperationException($"Project priority with ID {id} not found");
        entity.Code = dto.Code; entity.Name = dto.Name; entity.ColorHex = dto.ColorHex; entity.SortOrder = dto.SortOrder; entity.IsActive = dto.IsActive; entity.UpdatedBy = _currentUserProvider.Username; entity.LastModifiedById = _currentUserProvider.UserId;
        await _projectPriorityRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return (await GetProjectPrioritiesAsync()).First(x => x.Id == entity.Id);
    }
    public async Task DeleteProjectPriorityAsync(Guid id)
    {
        EnsureAdministrationAccess();
        await _projectPriorityRepository.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectTemplateDto>> GetProjectTemplatesAsync(Guid? projectTypeId = null)
    {
        EnsureAdministrationAccess();
        var templates = projectTypeId.HasValue ? await _projectTemplateRepository.GetByProjectTypeAsync(projectTypeId.Value) : await _projectTemplateRepository.GetActiveAsync();
        return templates.Select(x => new ProjectTemplateDto { Id = x.Id, Code = x.Code, Name = x.Name, Description = x.Description, ProjectTypeId = x.ProjectTypeId, ProjectTypeName = x.ProjectType?.Name, VersionLabel = x.VersionLabel, TemplateDefinitionJson = x.TemplateDefinitionJson, IsActive = x.IsActive });
    }
    public async Task<ProjectTemplateDto> CreateProjectTemplateAsync(CreateProjectTemplateDto dto)
    {
        EnsureAdministrationAccess();
        var entity = new ProjectTemplate { TenantId = _currentUserProvider.TenantId, Code = dto.Code, Name = dto.Name, Description = dto.Description, ProjectTypeId = dto.ProjectTypeId, VersionLabel = dto.VersionLabel, TemplateDefinitionJson = dto.TemplateDefinitionJson, IsActive = dto.IsActive, CreatedBy = _currentUserProvider.Username, CreatedById = _currentUserProvider.UserId };
        await _projectTemplateRepository.CreateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return (await GetProjectTemplatesAsync(dto.ProjectTypeId)).First(x => x.Id == entity.Id);
    }
    public async Task<ProjectTemplateDto> UpdateProjectTemplateAsync(Guid id, CreateProjectTemplateDto dto)
    {
        EnsureAdministrationAccess();
        var entity = await _projectTemplateRepository.GetByIdAsync(id) ?? throw new InvalidOperationException($"Project template with ID {id} not found");
        entity.Code = dto.Code; entity.Name = dto.Name; entity.Description = dto.Description; entity.ProjectTypeId = dto.ProjectTypeId; entity.VersionLabel = dto.VersionLabel; entity.TemplateDefinitionJson = dto.TemplateDefinitionJson; entity.IsActive = dto.IsActive; entity.UpdatedBy = _currentUserProvider.Username; entity.LastModifiedById = _currentUserProvider.UserId;
        await _projectTemplateRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return (await GetProjectTemplatesAsync(dto.ProjectTypeId)).First(x => x.Id == entity.Id);
    }
    public async Task DeleteProjectTemplateAsync(Guid id)
    {
        EnsureAdministrationAccess();
        await _projectTemplateRepository.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectPortfolioDto>> GetPortfoliosAsync()
    {
        EnsureAdministrationAccess();
        return (await _projectPortfolioRepository.GetAllAsync()).Select(MapToDto);
    }

    public async Task<ProjectPortfolioDto> CreatePortfolioAsync(CreateProjectPortfolioDto dto)
    {
        EnsureAdministrationAccess();
        var entity = new ProjectPortfolio
        {
            TenantId = _currentUserProvider.TenantId,
            Code = dto.Code,
            Name = dto.Name,
            Description = dto.Description,
            Status = dto.Status,
            StrategicObjective = dto.StrategicObjective,
            OwnerId = dto.OwnerId,
            SponsorId = dto.SponsorId,
            StartDate = dto.StartDate,
            TargetEndDate = dto.TargetEndDate,
            BudgetCap = dto.BudgetCap,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };
        await _projectPortfolioRepository.CreateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto((await _projectPortfolioRepository.GetByIdAsync(entity.Id))!);
    }

    public async Task<ProjectPortfolioDto> UpdatePortfolioAsync(Guid id, CreateProjectPortfolioDto dto)
    {
        EnsureAdministrationAccess();
        var entity = await _projectPortfolioRepository.GetByIdAsync(id) ?? throw new InvalidOperationException($"Project portfolio with ID {id} not found");
        entity.Code = dto.Code;
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.Status = dto.Status;
        entity.StrategicObjective = dto.StrategicObjective;
        entity.OwnerId = dto.OwnerId;
        entity.SponsorId = dto.SponsorId;
        entity.StartDate = dto.StartDate;
        entity.TargetEndDate = dto.TargetEndDate;
        entity.BudgetCap = dto.BudgetCap;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await _projectPortfolioRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto((await _projectPortfolioRepository.GetByIdAsync(entity.Id))!);
    }

    public async Task DeletePortfolioAsync(Guid id)
    {
        EnsureAdministrationAccess();
        await _projectPortfolioRepository.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<IEnumerable<ProjectProgramDto>> GetProgramsAsync(Guid? portfolioId = null)
    {
        EnsureAdministrationAccess();
        var programs = portfolioId.HasValue
            ? await _projectProgramRepository.GetByPortfolioIdAsync(portfolioId.Value)
            : await _projectProgramRepository.GetAllAsync();
        return programs.Select(MapToDto);
    }

    public async Task<ProjectProgramDto> CreateProgramAsync(CreateProjectProgramDto dto)
    {
        EnsureAdministrationAccess();
        if (dto.PortfolioId.HasValue && await _projectPortfolioRepository.GetByIdAsync(dto.PortfolioId.Value) == null)
        {
            throw new InvalidOperationException($"Project portfolio with ID {dto.PortfolioId} not found");
        }

        var entity = new ProjectProgram
        {
            TenantId = _currentUserProvider.TenantId,
            Code = dto.Code,
            Name = dto.Name,
            Description = dto.Description,
            PortfolioId = dto.PortfolioId,
            Status = dto.Status,
            ProgramManagerId = dto.ProgramManagerId,
            SponsorId = dto.SponsorId,
            StartDate = dto.StartDate,
            TargetEndDate = dto.TargetEndDate,
            BudgetCap = dto.BudgetCap,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };
        await _projectProgramRepository.CreateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto((await _projectProgramRepository.GetByIdAsync(entity.Id))!);
    }

    public async Task<ProjectProgramDto> UpdateProgramAsync(Guid id, CreateProjectProgramDto dto)
    {
        EnsureAdministrationAccess();
        if (dto.PortfolioId.HasValue && await _projectPortfolioRepository.GetByIdAsync(dto.PortfolioId.Value) == null)
        {
            throw new InvalidOperationException($"Project portfolio with ID {dto.PortfolioId} not found");
        }

        var entity = await _projectProgramRepository.GetByIdAsync(id) ?? throw new InvalidOperationException($"Project program with ID {id} not found");
        entity.Code = dto.Code;
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.PortfolioId = dto.PortfolioId;
        entity.Status = dto.Status;
        entity.ProgramManagerId = dto.ProgramManagerId;
        entity.SponsorId = dto.SponsorId;
        entity.StartDate = dto.StartDate;
        entity.TargetEndDate = dto.TargetEndDate;
        entity.BudgetCap = dto.BudgetCap;
        entity.UpdatedBy = _currentUserProvider.Username;
        entity.LastModifiedById = _currentUserProvider.UserId;
        await _projectProgramRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto((await _projectProgramRepository.GetByIdAsync(entity.Id))!);
    }

    public async Task DeleteProgramAsync(Guid id)
    {
        EnsureAdministrationAccess();
        await _projectProgramRepository.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();
    }

    private void EnsureAdministrationAccess()
    {
        if (_currentUserProvider.IsExternalUser)
        {
            throw new UnauthorizedAccessException("External users cannot administer project setup.");
        }

        if (_currentUserProvider.HasRole(ErpSystem.Shared.Constants.Roles.SuperAdmin)
            || _currentUserProvider.HasRole(ErpSystem.Shared.Constants.Roles.TenantAdmin)
            || _currentUserProvider.HasRole(ErpSystem.Shared.Constants.Roles.Manager))
        {
            return;
        }

        throw new UnauthorizedAccessException("You do not have permission to administer project setup.");
    }

    private async Task EnsureDefaultPrioritiesAsync()
    {
        if ((await _projectPriorityRepository.GetAllAsync()).Any()) return;
        foreach (var item in new[] { new CreateProjectPriorityDto { Code = "LOW", Name = "Low", ColorHex = "#22C55E", SortOrder = 10 }, new CreateProjectPriorityDto { Code = "MEDIUM", Name = "Medium", ColorHex = "#F59E0B", SortOrder = 20 }, new CreateProjectPriorityDto { Code = "HIGH", Name = "High", ColorHex = "#EF4444", SortOrder = 30 }, new CreateProjectPriorityDto { Code = "CRITICAL", Name = "Critical", ColorHex = "#7F1D1D", SortOrder = 40 } })
        {
            await CreateProjectPriorityAsync(item);
        }
    }

    private static ProjectCatalogEntryDto MapToDto(ProjectCatalogEntry entity) => new()
    {
        Id = entity.Id,
        CatalogType = entity.CatalogType,
        Code = entity.Code,
        Name = entity.Name,
        Description = entity.Description,
        SortOrder = entity.SortOrder,
        IsActive = entity.IsActive
    };

    private static string NormalizeCatalogCode(string code)
        => string.IsNullOrWhiteSpace(code)
            ? throw new InvalidOperationException("Project catalog code is required.")
            : code.Trim();

    private static string NormalizeCatalogName(string name)
        => string.IsNullOrWhiteSpace(name)
            ? throw new InvalidOperationException("Project catalog name is required.")
            : name.Trim();

    private static string NormalizeCatalogType(string catalogType)
    {
        var normalized = ProjectCatalogDefaults.NormalizeCatalogType(catalogType);
        if (!ProjectCatalogDefaults.IsSupportedCatalogType(normalized))
        {
            throw new InvalidOperationException($"Project catalog type '{catalogType}' is not supported.");
        }

        return normalized;
    }

    private static bool IsActiveProjectStatus(string status)
        => string.Equals(status, ProjectStatuses.Planned, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, ProjectStatuses.InProgress, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, ProjectStatuses.OnHold, StringComparison.OrdinalIgnoreCase);

    private static ProjectPortfolioDto MapToDto(ProjectPortfolio entity) => new()
    {
        Id = entity.Id,
        Code = entity.Code,
        Name = entity.Name,
        Description = entity.Description,
        Status = entity.Status,
        StrategicObjective = entity.StrategicObjective,
        OwnerId = entity.OwnerId,
        SponsorId = entity.SponsorId,
        StartDate = entity.StartDate,
        TargetEndDate = entity.TargetEndDate,
        BudgetCap = entity.BudgetCap,
        ProgramCount = entity.Programs.Count,
        ProjectCount = entity.Projects.Count,
        ActiveProjectCount = entity.Projects.Count(x => IsActiveProjectStatus(x.Status)),
        TotalEstimatedBudget = entity.Projects.Sum(x => x.EstimatedBudget ?? 0m),
        TotalActualCost = entity.Projects.Sum(x => x.ActualCost ?? 0m)
    };

    private static ProjectProgramDto MapToDto(ProjectProgram entity) => new()
    {
        Id = entity.Id,
        PortfolioId = entity.PortfolioId,
        PortfolioName = entity.Portfolio?.Name,
        Code = entity.Code,
        Name = entity.Name,
        Description = entity.Description,
        Status = entity.Status,
        ProgramManagerId = entity.ProgramManagerId,
        SponsorId = entity.SponsorId,
        StartDate = entity.StartDate,
        TargetEndDate = entity.TargetEndDate,
        BudgetCap = entity.BudgetCap,
        ProjectCount = entity.Projects.Count,
        ActiveProjectCount = entity.Projects.Count(x => IsActiveProjectStatus(x.Status)),
        TotalEstimatedBudget = entity.Projects.Sum(x => x.EstimatedBudget ?? 0m),
        TotalActualCost = entity.Projects.Sum(x => x.ActualCost ?? 0m)
    };
}

public class ProjectManagementSettingsService : IProjectManagementSettingsService
{
    private readonly IProjectManagementSettingsRepository _settingsRepository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;

    public ProjectManagementSettingsService(IProjectManagementSettingsRepository settingsRepository, ICurrentUserProvider currentUserProvider, IUnitOfWork unitOfWork)
    {
        _settingsRepository = settingsRepository;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProjectManagementSettingsDto> GetSettingsAsync()
    {
        EnsureAdministrationAccess();
        return MapToDto(await _settingsRepository.GetOrCreateDefaultAsync(_currentUserProvider.TenantId, _currentUserProvider.UserId));
    }
    public async Task<ProjectManagementSettingsDto> UpdateSettingsAsync(UpdateProjectManagementSettingsDto dto)
    {
        EnsureAdministrationAccess();
        var settings = await _settingsRepository.GetOrCreateDefaultAsync(_currentUserProvider.TenantId, _currentUserProvider.UserId);
        settings.ProjectNumberFormat = dto.ProjectNumberFormat; settings.RequireSponsor = dto.RequireSponsor; settings.DefaultApprovalRequired = dto.DefaultApprovalRequired; settings.DefaultProjectTypeId = dto.DefaultProjectTypeId; settings.DefaultProjectPriorityId = dto.DefaultProjectPriorityId; settings.DefaultTemplateId = dto.DefaultTemplateId; settings.MandatoryFieldsByTypeJson = dto.MandatoryFieldsByTypeJson; settings.Notes = dto.Notes; settings.UpdatedBy = _currentUserProvider.Username; settings.LastModifiedById = _currentUserProvider.UserId;
        await _settingsRepository.UpdateAsync(settings);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto(settings);
    }

    private static ProjectManagementSettingsDto MapToDto(ProjectManagementSettings entity) => new() { Id = entity.Id, TenantId = entity.TenantId, ProjectNumberFormat = entity.ProjectNumberFormat, RequireSponsor = entity.RequireSponsor, DefaultApprovalRequired = entity.DefaultApprovalRequired, DefaultProjectTypeId = entity.DefaultProjectTypeId, DefaultProjectPriorityId = entity.DefaultProjectPriorityId, DefaultTemplateId = entity.DefaultTemplateId, MandatoryFieldsByTypeJson = entity.MandatoryFieldsByTypeJson, Notes = entity.Notes };

    private void EnsureAdministrationAccess()
    {
        if (_currentUserProvider.IsExternalUser)
        {
            throw new UnauthorizedAccessException("External users cannot administer project settings.");
        }

        if (_currentUserProvider.HasRole(ErpSystem.Shared.Constants.Roles.SuperAdmin)
            || _currentUserProvider.HasRole(ErpSystem.Shared.Constants.Roles.TenantAdmin)
            || _currentUserProvider.HasRole(ErpSystem.Shared.Constants.Roles.Manager))
        {
            return;
        }

        throw new UnauthorizedAccessException("You do not have permission to administer project settings.");
    }
}
