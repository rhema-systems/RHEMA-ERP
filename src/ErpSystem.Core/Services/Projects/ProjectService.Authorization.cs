using System.Text.RegularExpressions;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Interfaces;
using ErpSystem.Shared;

namespace ErpSystem.Core.Services.Projects;

internal enum ProjectAccessOperation
{
    View,
    Create,
    UpdateOverview,
    ManageMembers,
    ManagePlan,
    ManageExecution,
    ManageFinancials,
    ManageGovernance,
    ManageExternalAccess,
    SubmitForApproval,
    ApproveWorkflow
}

public partial class ProjectService
{
    private static readonly HashSet<string> ManagementProjectRoles =
    [
        "owner",
        "projectowner",
        "projectmanager",
        "assistantprojectmanager",
        "programmanager",
        "portfoliomanager",
        "projectsponsor",
        "sponsor",
        "projectdirector",
        "pmoadministrator"
    ];

    private static readonly HashSet<string> FinancialProjectRoles =
    [
        "financeofficer",
        "billingofficer",
        "commercialmanager"
    ];

    private static readonly HashSet<string> GovernanceProjectRoles =
    [
        "riskofficer",
        "complianceofficer",
        "auditor"
    ];

    private static readonly HashSet<string> ExecutionProjectRoles =
    [
        "taskowner",
        "teammember",
        "resource",
        "resourcemanager"
    ];

    public async Task<bool> HasProjectAccessAsync(Guid projectId)
    {
        if (projectId == Guid.Empty)
        {
            return false;
        }

        try
        {
            await GetProjectForOperationAsync(projectId, ProjectAccessOperation.View);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (InvalidOperationException exception) when (
            exception.Message.StartsWith("Project with ID ", StringComparison.Ordinal) &&
            exception.Message.EndsWith(" not found", StringComparison.Ordinal))
        {
            return false;
        }
    }

    private async Task<Project> GetProjectForOperationAsync(Guid projectId, ProjectAccessOperation operation)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project is null || project.IsDeleted || project.TenantId != _currentUserProvider.TenantId)
        {
            throw new InvalidOperationException($"Project with ID {projectId} not found");
        }

        await EnsureProjectAccessAsync(project, operation);
        return project;
    }

    private async Task EnsureProjectAccessAsync(Project project, ProjectAccessOperation operation)
    {
        EnsureInternalProjectAccess();

        if (!_currentUserProvider.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Authentication is required to access project data.");
        }

        if (CanBypassProjectAuthorization(operation))
        {
            return;
        }

        var membershipRoles = await GetCurrentUserProjectRolesAsync(project.Id);
        var isLead = IsProjectLead(project);
        var isExecutionContributor = membershipRoles.Any(IsExecutionProjectRole)
            || await IsOperationalContributorAsync(project.Id);
        var isFinanceContributor = membershipRoles.Any(IsFinancialProjectRole);
        var isGovernanceContributor = membershipRoles.Any(IsGovernanceProjectRole);
        var hasMembership = membershipRoles.Count > 0;
        var canView = isLead || hasMembership || isExecutionContributor;

        var allowed = operation switch
        {
            ProjectAccessOperation.View => canView,
            ProjectAccessOperation.Create => !IsReadOnlyUser(),
            ProjectAccessOperation.UpdateOverview => !IsReadOnlyUser() && (isLead || membershipRoles.Any(IsManagementProjectRole)),
            ProjectAccessOperation.ManageMembers => !IsReadOnlyUser() && (isLead || membershipRoles.Any(IsManagementProjectRole)),
            ProjectAccessOperation.ManagePlan => !IsReadOnlyUser() && (isLead || membershipRoles.Any(IsManagementProjectRole)),
            ProjectAccessOperation.ManageExecution => !IsReadOnlyUser() && (isLead || membershipRoles.Any(IsManagementProjectRole) || isExecutionContributor),
            ProjectAccessOperation.ManageFinancials => !IsReadOnlyUser() && (isLead || membershipRoles.Any(IsManagementProjectRole) || isFinanceContributor),
            ProjectAccessOperation.ManageGovernance => !IsReadOnlyUser() && (isLead || membershipRoles.Any(IsManagementProjectRole) || isGovernanceContributor),
            ProjectAccessOperation.ManageExternalAccess => !IsReadOnlyUser() && (isLead || membershipRoles.Any(IsManagementProjectRole)),
            ProjectAccessOperation.SubmitForApproval => !IsReadOnlyUser() && (isLead || membershipRoles.Any(IsManagementProjectRole)),
            // A configured QS project workflow may assign an independent QS
            // approver who is not a project member. Keep the workflow engine's
            // CanUserApproveAsync check as the final gate; this access check
            // only lets that assigned approver reach it.
            ProjectAccessOperation.ApproveWorkflow => canView ||
                _currentUserProvider.HasRole("TDC_SUPERVISING_QUANTITY_SURVEYOR"),
            _ => false
        };

        if (!allowed)
        {
            throw new UnauthorizedAccessException($"You do not have permission to perform '{operation}' on project {project.ProjectCode}.");
        }
    }

    private async Task<HashSet<Guid>> GetAccessibleProjectIdsAsync()
    {
        EnsureInternalAuthenticatedProjectAccess();

        if (CanReadAllProjects())
        {
            return [];
        }

        var userId = _currentUserProvider.UserId;
        var tenantId = _currentUserProvider.TenantId;
        var ids = new HashSet<Guid>();

        var ownedProjects = await _unitOfWork.Repository<Project>().FindAsync(x =>
            x.TenantId == tenantId
            && !x.IsDeleted
            && (x.ProjectManagerId == userId || x.SponsorId == userId || x.CreatedById == userId));
        ids.UnionWith(ownedProjects.Select(x => x.Id));

        var memberships = await _unitOfWork.Repository<ProjectMember>().FindAsync(x =>
            x.TenantId == tenantId && x.UserId == userId && x.IsActive);
        ids.UnionWith(memberships.Select(x => x.ProjectId));

        var allocations = await _unitOfWork.Repository<ProjectResourceAllocation>().FindAsync(x =>
            x.TenantId == tenantId && x.UserId == userId);
        ids.UnionWith(allocations.Select(x => x.ProjectId));

        var assignments = await _unitOfWork.Repository<ProjectWorkItem>().FindAsync(x =>
            x.TenantId == tenantId && x.AssignedToUserId == userId);
        ids.UnionWith(assignments.Select(x => x.ProjectId));

        var timesheets = await _unitOfWork.Repository<ProjectTimesheetEntry>().FindAsync(x =>
            x.TenantId == tenantId && x.UserId == userId);
        ids.UnionWith(timesheets.Select(x => x.ProjectId));

        var expenses = await _unitOfWork.Repository<ProjectExpense>().FindAsync(x =>
            x.TenantId == tenantId && x.UserId == userId);
        ids.UnionWith(expenses.Select(x => x.ProjectId));

        return ids;
    }

    private async Task<List<Project>> GetAccessibleProjectsAsync(string? search = null, string? status = null, Guid? projectTypeId = null, Guid? portfolioId = null, Guid? programId = null, int take = 1000)
    {
        EnsureInternalAuthenticatedProjectAccess();

        var projects = (await _projectRepository.LookupAsync(search, status, projectTypeId, portfolioId, programId, take)).ToList();
        if (CanReadAllProjects())
        {
            return projects;
        }

        var accessibleIds = await GetAccessibleProjectIdsAsync();
        return projects.Where(x => accessibleIds.Contains(x.Id)).ToList();
    }

    private async Task<List<string>> GetCurrentUserProjectRolesAsync(Guid projectId)
        => (await _unitOfWork.Repository<ProjectMember>().FindAsync(x =>
                x.ProjectId == projectId
                && x.TenantId == _currentUserProvider.TenantId
                && x.UserId == _currentUserProvider.UserId
                && x.IsActive))
            .Select(x => x.Role)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private async Task<bool> IsOperationalContributorAsync(Guid projectId)
    {
        var userId = _currentUserProvider.UserId;
        var tenantId = _currentUserProvider.TenantId;

        if (await _unitOfWork.Repository<ProjectResourceAllocation>().ExistsAsync(x => x.ProjectId == projectId && x.TenantId == tenantId && x.UserId == userId))
        {
            return true;
        }

        if (await _unitOfWork.Repository<ProjectWorkItem>().ExistsAsync(x => x.ProjectId == projectId && x.TenantId == tenantId && x.AssignedToUserId == userId))
        {
            return true;
        }

        if (await _unitOfWork.Repository<ProjectTimesheetEntry>().ExistsAsync(x => x.ProjectId == projectId && x.TenantId == tenantId && x.UserId == userId))
        {
            return true;
        }

        return await _unitOfWork.Repository<ProjectExpense>().ExistsAsync(x => x.ProjectId == projectId && x.TenantId == tenantId && x.UserId == userId);
    }

    private void EnsureInternalProjectAccess()
    {
        if (_currentUserProvider.IsExternalUser)
        {
            throw new UnauthorizedAccessException("External users must use the external project portal endpoints.");
        }
    }

    private void EnsureInternalAuthenticatedProjectAccess()
    {
        EnsureInternalProjectAccess();

        if (!_currentUserProvider.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("Authentication is required to access project data.");
        }
    }

    private bool CanReadAllProjects()
        => IsPlatformAdministrator()
            || _currentUserProvider.HasRole(Constants.Roles.Manager)
            || _currentUserProvider.HasRole(Constants.Roles.ReadOnly);

    private bool CanBypassProjectAuthorization(ProjectAccessOperation operation)
        => IsPlatformAdministrator()
            || (operation == ProjectAccessOperation.View && (_currentUserProvider.HasRole(Constants.Roles.Manager) || _currentUserProvider.HasRole(Constants.Roles.ReadOnly)));

    private bool IsPlatformAdministrator()
        => _currentUserProvider.HasRole(Constants.Roles.SuperAdmin)
            || _currentUserProvider.HasRole(Constants.Roles.TenantAdmin);

    private bool IsReadOnlyUser()
        => _currentUserProvider.HasRole(Constants.Roles.ReadOnly) && !IsPlatformAdministrator();

    private bool IsProjectLead(Project project)
        => project.ProjectManagerId == _currentUserProvider.UserId
            || project.SponsorId == _currentUserProvider.UserId
            || project.CreatedById == _currentUserProvider.UserId;

    private static bool IsManagementProjectRole(string? role)
        => ManagementProjectRoles.Contains(NormalizeProjectRole(role))
            || CivilEngineeringAccessControlRegistry.IsManagementProjectRole(role);

    private static bool IsFinancialProjectRole(string? role)
    {
        var normalized = NormalizeProjectRole(role);
        return FinancialProjectRoles.Contains(normalized) || ManagementProjectRoles.Contains(normalized);
    }

    private static bool IsGovernanceProjectRole(string? role)
    {
        var normalized = NormalizeProjectRole(role);
        return GovernanceProjectRoles.Contains(normalized) || IsManagementProjectRole(role);
    }

    private static bool IsExecutionProjectRole(string? role)
    {
        var normalized = NormalizeProjectRole(role);
        return ExecutionProjectRoles.Contains(normalized)
            || IsManagementProjectRole(role)
            || CivilEngineeringAccessControlRegistry.IsExecutionProjectRole(role);
    }

    private static string NormalizeProjectRole(string? role)
        => string.IsNullOrWhiteSpace(role)
            ? string.Empty
            : Regex.Replace(role, "[^a-z0-9]", string.Empty, RegexOptions.IgnoreCase).ToLowerInvariant();

    private async Task EnsureProjectMembershipsAsync(Project project)
    {
        var memberRepo = _unitOfWork.Repository<ProjectMember>();
        var members = (await memberRepo.FindAsync(x => x.ProjectId == project.Id && x.TenantId == _currentUserProvider.TenantId)).ToList();

        await EnsureProjectMemberAsync(memberRepo, members, project, _currentUserProvider.UserId, "Owner");

        if (project.ProjectManagerId.HasValue)
        {
            await EnsureProjectMemberAsync(memberRepo, members, project, project.ProjectManagerId.Value, "ProjectManager");
        }

        if (project.SponsorId.HasValue)
        {
            await EnsureProjectMemberAsync(memberRepo, members, project, project.SponsorId.Value, "Sponsor");
        }
    }

    private async Task EnsureProjectMemberAsync(IGenericRepository<ProjectMember> memberRepo, List<ProjectMember> existingMembers, Project project, Guid userId, string role)
    {
        if (existingMembers.Any(x => x.UserId == userId && string.Equals(x.Role, role, StringComparison.OrdinalIgnoreCase) && x.IsActive))
        {
            return;
        }

        var entity = new ProjectMember
        {
            TenantId = _currentUserProvider.TenantId,
            ProjectId = project.Id,
            UserId = userId,
            Role = role,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            CreatedBy = _currentUserProvider.Username,
            CreatedById = _currentUserProvider.UserId
        };

        await memberRepo.AddAsync(entity);
        existingMembers.Add(entity);
    }

    private void EnsureSetupAdministrationAccess()
    {
        if (_currentUserProvider.IsExternalUser)
        {
            throw new UnauthorizedAccessException("External users cannot administer project setup.");
        }

        if (IsPlatformAdministrator() || _currentUserProvider.HasRole(Constants.Roles.Manager))
        {
            return;
        }

        throw new UnauthorizedAccessException("You do not have permission to administer project management setup.");
    }

    private async Task EnsureOwnedByCurrentUserOrAuthorizedAsync(Project project, Guid actingForUserId)
    {
        if (actingForUserId == Guid.Empty || actingForUserId == _currentUserProvider.UserId)
        {
            return;
        }

        await EnsureProjectAccessAsync(project, ProjectAccessOperation.ManageFinancials);
    }
}
