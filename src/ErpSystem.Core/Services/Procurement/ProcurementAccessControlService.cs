using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementAccessControlService : IProcurementAccessControlService
{
    private const string AuditPrefix = "PROCUREMENT_ACCESS_";
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementControlEventService _controlEvents;
    private readonly ILogger<ProcurementAccessControlService> _logger;

    public ProcurementAccessControlService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementControlEventService controlEvents,
        ILogger<ProcurementAccessControlService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _controlEvents = controlEvents;
        _logger = logger;
    }

    private IGenericRepository<Permission> Permissions => _unitOfWork.Repository<Permission>();
    private IGenericRepository<UserTenant> UserTenants => _unitOfWork.Repository<UserTenant>();
    private IGenericRepository<Warehouse> Warehouses => _unitOfWork.Repository<Warehouse>();
    private IGenericRepository<WarehouseLocation> WarehouseLocations => _unitOfWork.Repository<WarehouseLocation>();
    private IGenericRepository<ProcurementResponsibilityAssignment> Assignments => _unitOfWork.Repository<ProcurementResponsibilityAssignment>();
    private IGenericRepository<ProcurementResponsibilityWarehouse> AssignmentWarehouses => _unitOfWork.Repository<ProcurementResponsibilityWarehouse>();
    private IGenericRepository<ProcurementResponsibilityLocation> AssignmentLocations => _unitOfWork.Repository<ProcurementResponsibilityLocation>();
    private IGenericRepository<ProcurementCommittee> Committees => _unitOfWork.Repository<ProcurementCommittee>();
    private IGenericRepository<ProcurementCommitteeMember> CommitteeMembers => _unitOfWork.Repository<ProcurementCommitteeMember>();
    private IGenericRepository<WorkflowDefinition> WorkflowDefinitions => _unitOfWork.Repository<WorkflowDefinition>();
    private IGenericRepository<AuditLog> AuditLogs => _unitOfWork.Repository<AuditLog>();

    public async Task<ProcurementAccessReadinessDto> GetReadinessAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdministrator();
        var roles = await GetRolesCoreAsync(cancellationToken);
        var permissions = await GetPermissionsCoreAsync(cancellationToken);
        var committees = await GetCommitteesCoreAsync(cancellationToken);
        var workflows = await GetWorkflowsCoreAsync(cancellationToken);
        var activeAssignments = await Assignments.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted && item.IsActive &&
                item.EffectiveFrom <= DateTime.UtcNow && (!item.EffectiveTo.HasValue || item.EffectiveTo >= DateTime.UtcNow))
            .AsNoTracking().CountAsync(cancellationToken);
        var internalAudit = roles.Single(item => item.Code == ProcurementAccessControlRegistry.InternalAuditRole);
        var internalAuditReadOnly = internalAudit.IsConfigured &&
                                    internalAudit.MissingPermissionCodes.Count == 0 &&
                                    internalAudit.UnexpectedMutationPermissions.Count == 0;
        var issues = new List<string>();
        if (roles.Any(item => !item.IsConfigured || item.MissingPermissionCodes.Count > 0))
            issues.Add("One or more required TDC roles or role-permission grants are missing.");
        if (!internalAuditReadOnly)
            issues.Add("The TDC Internal Audit role is missing required read access or has a mutation permission.");
        if (committees.Any(item => item.Status != ProcurementCommitteeStatus.Active || !item.MeetsQuorum))
            issues.Add("Committee membership, activation, and approved quorum remain incomplete.");
        if (workflows.Any(item => !item.IsPublished))
            issues.Add("TDC workflow templates remain missing or Draft until DEC-003/DEC-004 routes are approved and published.");
        if (activeAssignments == 0)
            issues.Add("No active tenant-scoped warehouse or committee context assignments exist.");

        return new ProcurementAccessReadinessDto
        {
            RequiredRoleCount = roles.Count,
            ConfiguredRoleCount = roles.Count(item => item.IsConfigured && item.MissingPermissionCodes.Count == 0),
            RequiredPermissionCount = permissions.Count,
            ConfiguredPermissionCount = permissions.Count(item => item.IsConfigured),
            RequiredCommitteeCount = committees.Count,
            ConfiguredCommitteeCount = committees.Count,
            ReadyCommitteeCount = committees.Count(item => item.Status == ProcurementCommitteeStatus.Active && item.MeetsQuorum),
            RequiredWorkflowCount = workflows.Count,
            ConfiguredWorkflowCount = workflows.Count(item => item.WorkflowDefinitionId.HasValue),
            PublishedWorkflowCount = workflows.Count(item => item.IsPublished),
            ActiveAssignmentCount = activeAssignments,
            InternalAuditIsReadOnly = internalAuditReadOnly,
            IsReadyForUat = issues.Count == 0,
            Issues = issues
        };
    }

    public async Task<IReadOnlyList<ProcurementAccessRoleDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdministrator();
        return await GetRolesCoreAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProcurementAccessPermissionDto>> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdministrator();
        return await GetPermissionsCoreAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProcurementAccessUserOptionDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdministrator();
        var now = DateTime.UtcNow;
        return await UserTenants.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted && item.Status == UserTenantStatus.Active &&
                (!item.ExpiresAt.HasValue || item.ExpiresAt > now) && item.User.IsActive)
            .AsNoTracking()
            .OrderBy(item => item.User.FirstName).ThenBy(item => item.User.LastName)
            .Select(item => new ProcurementAccessUserOptionDto
            {
                UserId = item.UserId,
                Username = item.User.UserName ?? string.Empty,
                DisplayName = (item.User.FirstName + " " + item.User.LastName).Trim(),
                IsActive = item.User.IsActive,
                RoleNames = item.User.UserRoles
                    .Where(link => link.Role.Name != null)
                    .Select(link => link.Role.Name!)
                    .OrderBy(name => name)
                    .ToList()
            }).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProcurementAccessWarehouseOptionDto>> GetWarehousesAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdministrator();
        return await Warehouses.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .AsNoTracking().OrderBy(item => item.Code).ThenBy(item => item.Name)
            .Select(item => new ProcurementAccessWarehouseOptionDto
            {
                WarehouseId = item.Id,
                Code = item.Code,
                Name = item.Name,
                IsActive = item.IsActive
            }).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProcurementAccessLocationOptionDto>> GetLocationsAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdministrator();
        return await WarehouseLocations.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Warehouse)
            .AsNoTracking().OrderBy(item => item.Warehouse.Code).ThenBy(item => item.LocationCode)
            .Select(item => new ProcurementAccessLocationOptionDto
            {
                LocationId = item.Id,
                WarehouseId = item.WarehouseId,
                WarehouseCode = item.Warehouse.Code,
                Code = item.LocationCode,
                Name = item.Name ?? item.LocationCode,
                IsActive = item.IsActive
            }).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProcurementResponsibilityAssignmentDto>> GetAssignmentsAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdministrator();
        return (await LoadAssignmentsAsync(cancellationToken)).Select(MapAssignment).ToList();
    }

    public async Task<ProcurementResponsibilityAssignmentDto> SaveAssignmentAsync(
        Guid? id,
        SaveProcurementResponsibilityAssignmentRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAdministrator();
        ValidateAssignmentRequest(request);
        var roleDefinition = ProcurementAccessControlRegistry.FindRole(request.RoleName)
            ?? throw new ProcurementAccessValidationException("ROLE_UNKNOWN", "RoleName must identify a required TDC procurement role.");
        var configuredRole = await ResolveConfiguredRoleAsync(roleDefinition.Code, cancellationToken)
            ?? throw new ProcurementAccessConflictException($"Identity role '{roleDefinition.Code}' has not been provisioned.");
        var userBelongs = await UserTenants.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.UserId == request.UserId && !item.IsDeleted &&
                item.Status == UserTenantStatus.Active &&
                (!item.ExpiresAt.HasValue || item.ExpiresAt > DateTime.UtcNow) && item.User.IsActive)
            .AnyAsync(cancellationToken);
        if (!userBelongs) throw new ProcurementAccessNotFoundException("The selected user is not active in the current tenant.");
        if (request.IsActive)
        {
            var hasSecurityRole = await UserTenants.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId && item.UserId == request.UserId && !item.IsDeleted &&
                    item.Status == UserTenantStatus.Active &&
                    (!item.ExpiresAt.HasValue || item.ExpiresAt > DateTime.UtcNow) && item.User.IsActive)
                .SelectMany(item => item.User.UserRoles)
                .AnyAsync(link => link.RoleId == configuredRole.RoleId, cancellationToken);
            if (!hasSecurityRole)
                throw new ProcurementAccessValidationException("SECURITY_ROLE_REQUIRED",
                    $"Assign role '{roleDefinition.Code}' to the user in Security before adding its procurement scope or committee duty.");
        }

        var warehouseRole = roleDefinition.PermissionCodes
            .Select(ProcurementAccessControlRegistry.FindPermission)
            .Any(item => item?.IsWarehouseScoped == true);
        if (warehouseRole && request.WarehouseScopeMode == ProcurementWarehouseScopeMode.None)
            throw new ProcurementAccessValidationException("WAREHOUSE_SCOPE_REQUIRED", "A warehouse-scoped role requires All or Restricted warehouse access.");
        if (!warehouseRole && request.WarehouseScopeMode != ProcurementWarehouseScopeMode.None)
            throw new ProcurementAccessValidationException("WAREHOUSE_SCOPE_NOT_APPLICABLE", "This role does not accept a warehouse scope.");
        if (warehouseRole && request.LocationScopeMode == ProcurementLocationScopeMode.None)
            throw new ProcurementAccessValidationException("LOCATION_SCOPE_REQUIRED", "A warehouse-scoped role requires All or Restricted location access.");
        if (!warehouseRole && request.LocationScopeMode != ProcurementLocationScopeMode.None)
            throw new ProcurementAccessValidationException("LOCATION_SCOPE_NOT_APPLICABLE", "This role does not accept a location scope.");
        var requestedWarehouseIds = request.WarehouseIds.Distinct().ToList();
        var requestedLocationIds = request.LocationIds.Distinct().ToList();
        if (request.WarehouseScopeMode == ProcurementWarehouseScopeMode.Restricted && requestedWarehouseIds.Count == 0)
            throw new ProcurementAccessValidationException("WAREHOUSE_REQUIRED", "Restricted scope requires at least one warehouse.");
        if (request.WarehouseScopeMode != ProcurementWarehouseScopeMode.Restricted && requestedWarehouseIds.Count > 0)
            throw new ProcurementAccessValidationException("WAREHOUSE_SCOPE_INVALID", "Warehouse IDs are accepted only for Restricted scope.");
        if (request.LocationScopeMode == ProcurementLocationScopeMode.Restricted && requestedLocationIds.Count == 0)
            throw new ProcurementAccessValidationException("LOCATION_REQUIRED", "Restricted location scope requires at least one warehouse location.");
        if (request.LocationScopeMode != ProcurementLocationScopeMode.Restricted && requestedLocationIds.Count > 0)
            throw new ProcurementAccessValidationException("LOCATION_SCOPE_INVALID", "Location IDs are accepted only for Restricted scope.");
        if (requestedWarehouseIds.Count > 0)
        {
            var warehouseCount = await Warehouses.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId && !item.IsDeleted && item.IsActive && requestedWarehouseIds.Contains(item.Id))
                .CountAsync(cancellationToken);
            if (warehouseCount != requestedWarehouseIds.Count)
                throw new ProcurementAccessNotFoundException("One or more selected warehouses do not belong to the current tenant.");
        }
        var requestedLocations = new List<WarehouseLocation>();
        if (requestedLocationIds.Count > 0)
        {
            requestedLocations = await WarehouseLocations.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId && !item.IsDeleted && item.IsActive && requestedLocationIds.Contains(item.Id))
                .AsNoTracking().ToListAsync(cancellationToken);
            if (requestedLocations.Count != requestedLocationIds.Count)
                throw new ProcurementAccessNotFoundException("One or more selected locations do not belong to the current tenant.");
            if (request.WarehouseScopeMode == ProcurementWarehouseScopeMode.Restricted &&
                requestedLocations.Any(item => !requestedWarehouseIds.Contains(item.InventoryWarehouseId)))
                throw new ProcurementAccessValidationException("LOCATION_WAREHOUSE_SCOPE_INVALID",
                    "Every restricted location must belong to one of the assignment's restricted warehouses.");
        }

        var duplicate = await Assignments.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted && item.IsActive &&
                item.UserId == request.UserId && item.RoleName == roleDefinition.Code && (!id.HasValue || item.Id != id.Value))
            .AnyAsync(cancellationToken);
        if (duplicate) throw new ProcurementAccessConflictException("This user already has an active assignment for the selected TDC role.");

        ProcurementResponsibilityAssignment assignment;
        object? before = null;
        if (id.HasValue)
        {
            assignment = await Assignments.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId && item.Id == id.Value && !item.IsDeleted)
                .Include(item => item.Warehouses).Include(item => item.Locations)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new ProcurementAccessNotFoundException("Responsibility assignment was not found.");
            EnsureRowVersion(assignment.RowVersion, request.RowVersion);
            before = AssignmentAuditShape(assignment);
            foreach (var scope in assignment.Warehouses.Where(item => !item.IsDeleted))
            {
                scope.IsDeleted = true;
                scope.DeletedAt = DateTime.UtcNow;
                scope.DeletedBy = _currentUser.Username;
                await AssignmentWarehouses.UpdateAsync(scope);
            }
            foreach (var scope in assignment.Locations.Where(item => !item.IsDeleted))
            {
                scope.IsDeleted = true;
                scope.DeletedAt = DateTime.UtcNow;
                scope.DeletedBy = _currentUser.Username;
                await AssignmentLocations.UpdateAsync(scope);
            }
        }
        else
        {
            assignment = new ProcurementResponsibilityAssignment
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUser.TenantId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUser.Username,
                CreatedById = _currentUser.UserId
            };
            await Assignments.AddAsync(assignment);
        }

        assignment.UserId = request.UserId;
        assignment.RoleId = configuredRole.RoleId;
        assignment.RoleName = roleDefinition.Code;
        assignment.WarehouseScopeMode = request.WarehouseScopeMode;
        assignment.LocationScopeMode = request.LocationScopeMode;
        assignment.EffectiveFrom = EnsureUtc(request.EffectiveFrom);
        assignment.EffectiveTo = request.EffectiveTo.HasValue ? EnsureUtc(request.EffectiveTo.Value) : null;
        assignment.IsActive = request.IsActive;
        assignment.Reason = request.Reason.Trim();
        assignment.UpdatedAt = DateTime.UtcNow;
        assignment.UpdatedBy = _currentUser.Username;
        assignment.LastModifiedById = _currentUser.UserId;
        if (id.HasValue) await Assignments.UpdateAsync(assignment);

        foreach (var warehouseId in requestedWarehouseIds)
        {
            await AssignmentWarehouses.AddAsync(new ProcurementResponsibilityWarehouse
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUser.TenantId,
                AssignmentId = assignment.Id,
                WarehouseId = warehouseId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUser.Username,
                CreatedById = _currentUser.UserId
            });
        }

        foreach (var location in requestedLocations)
        {
            await AssignmentLocations.AddAsync(new ProcurementResponsibilityLocation
            {
                Id = Guid.NewGuid(),
                TenantId = _currentUser.TenantId,
                AssignmentId = assignment.Id,
                WarehouseId = location.InventoryWarehouseId,
                WarehouseLocationId = location.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUser.Username,
                CreatedById = _currentUser.UserId
            });
        }

        await AddAuditAsync(id.HasValue ? "ASSIGNMENT_UPDATED" : "ASSIGNMENT_CREATED", "ProcurementResponsibilityAssignment",
            assignment.Id, before, AssignmentAuditShape(assignment, requestedWarehouseIds, requestedLocationIds), correlationId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var saved = (await LoadAssignmentsAsync(cancellationToken)).Single(item => item.Id == assignment.Id);
        return MapAssignment(saved);
    }

    public async Task<IReadOnlyList<ProcurementCommitteeDto>> GetCommitteesAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdministrator();
        return await GetCommitteesCoreAsync(cancellationToken);
    }

    public async Task<ProcurementCommitteeDto> UpdateCommitteeAsync(
        Guid id,
        UpdateProcurementCommitteeRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAdministrator();
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ProcurementAccessValidationException("REASON_REQUIRED", "A reason is required.");
        ValidatePeriod(request.EffectiveFrom, request.EffectiveTo);
        var committee = await Committees.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.Id == id && !item.IsDeleted)
            .Include(item => item.Members.Where(member => !member.IsDeleted && member.IsActive))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementAccessNotFoundException("Committee was not found.");
        EnsureRowVersion(committee.RowVersion, request.RowVersion);
        var activeVoting = committee.Members.Count(item => item.IsVoting && IsEffective(item.EffectiveFrom, item.EffectiveTo));
        if (request.Status == ProcurementCommitteeStatus.Active && activeVoting < request.RequiredQuorum)
            throw new ProcurementAccessValidationException("COMMITTEE_QUORUM_INCOMPLETE", "A committee cannot be activated before active voting membership meets its quorum.");
        var before = CommitteeAuditShape(committee);
        committee.Name = request.Name.Trim();
        committee.Description = request.Description?.Trim();
        committee.RequiredQuorum = request.RequiredQuorum;
        committee.Status = request.Status;
        committee.EffectiveFrom = EnsureUtc(request.EffectiveFrom);
        committee.EffectiveTo = request.EffectiveTo.HasValue ? EnsureUtc(request.EffectiveTo.Value) : null;
        committee.ChangeReason = request.Reason.Trim();
        committee.UpdatedAt = DateTime.UtcNow;
        committee.UpdatedBy = _currentUser.Username;
        committee.LastModifiedById = _currentUser.UserId;
        await Committees.UpdateAsync(committee);
        await AddAuditAsync("COMMITTEE_UPDATED", "ProcurementCommittee", committee.Id, before,
            CommitteeAuditShape(committee), correlationId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (await GetCommitteesCoreAsync(cancellationToken)).Single(item => item.Id == id);
    }

    public async Task<ProcurementCommitteeMemberDto> AddCommitteeMemberAsync(
        Guid committeeId,
        SaveProcurementCommitteeMemberRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAdministrator();
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ProcurementAccessValidationException("REASON_REQUIRED", "A reason is required.");
        ValidatePeriod(request.EffectiveFrom, request.EffectiveTo);
        var committee = await Committees.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.Id == committeeId && !item.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementAccessNotFoundException("Committee was not found.");
        if (committee.Status == ProcurementCommitteeStatus.Retired)
            throw new ProcurementAccessConflictException("Retired committee membership is immutable.");
        var assignment = await Assignments.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.Id == request.AssignmentId && !item.IsDeleted && item.IsActive)
            .Include(item => item.User).SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementAccessNotFoundException("The selected responsibility assignment was not found.");
        var observerAllowed = request.MemberKind == ProcurementCommitteeMemberKind.Observer &&
                              (assignment.RoleName == "TDC_OBSERVER" || assignment.RoleName == ProcurementAccessControlRegistry.InternalAuditRole);
        if (!observerAllowed && assignment.RoleName != committee.RequiredRoleName)
            throw new ProcurementAccessValidationException("COMMITTEE_ROLE_MISMATCH", $"Voting and administrative members require role '{committee.RequiredRoleName}'.");
        var duplicate = await CommitteeMembers.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.CommitteeId == committeeId && !item.IsDeleted && item.IsActive &&
                item.Assignment.UserId == assignment.UserId)
            .AnyAsync(cancellationToken);
        if (duplicate) throw new ProcurementAccessConflictException("The selected user is already an active committee member.");

        var member = new ProcurementCommitteeMember
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            CommitteeId = committeeId,
            AssignmentId = assignment.Id,
            MemberKind = request.MemberKind,
            IsVoting = request.MemberKind != ProcurementCommitteeMemberKind.Observer && request.IsVoting,
            IsActive = true,
            EffectiveFrom = EnsureUtc(request.EffectiveFrom),
            EffectiveTo = request.EffectiveTo.HasValue ? EnsureUtc(request.EffectiveTo.Value) : null,
            Reason = request.Reason.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.Username,
            CreatedById = _currentUser.UserId
        };
        await CommitteeMembers.AddAsync(member);
        await AddAuditAsync("COMMITTEE_MEMBER_ADDED", "ProcurementCommitteeMember", member.Id, null,
            new { committee.Code, assignment.UserId, assignment.RoleName, request.MemberKind, member.IsVoting, member.EffectiveFrom, member.EffectiveTo },
            correlationId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (await GetCommitteesCoreAsync(cancellationToken)).Single(item => item.Id == committeeId).Members.Single(item => item.Id == member.Id);
    }

    public async Task RemoveCommitteeMemberAsync(
        Guid committeeId,
        Guid memberId,
        RemoveProcurementCommitteeMemberRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        EnsureAdministrator();
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ProcurementAccessValidationException("REASON_REQUIRED", "A reason is required.");
        var committee = await Committees.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.Id == committeeId && !item.IsDeleted)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementAccessNotFoundException("Committee was not found.");
        if (committee.Status == ProcurementCommitteeStatus.Retired)
            throw new ProcurementAccessConflictException("Retired committee membership is immutable.");
        var member = await CommitteeMembers.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.CommitteeId == committeeId && item.Id == memberId && !item.IsDeleted)
            .Include(item => item.Assignment).SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProcurementAccessNotFoundException("Committee member was not found.");
        EnsureRowVersion(member.RowVersion, request.RowVersion);
        if (committee.Status == ProcurementCommitteeStatus.Active && member.IsVoting && IsEffective(member.EffectiveFrom, member.EffectiveTo))
        {
            var remaining = await CommitteeMembers.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId && item.CommitteeId == committeeId && item.Id != memberId &&
                    !item.IsDeleted && item.IsActive && item.IsVoting && item.EffectiveFrom <= DateTime.UtcNow &&
                    (!item.EffectiveTo.HasValue || item.EffectiveTo >= DateTime.UtcNow))
                .CountAsync(cancellationToken);
            if (remaining < committee.RequiredQuorum)
                throw new ProcurementAccessValidationException("COMMITTEE_QUORUM_REQUIRED", "Deactivate the committee or add a replacement before removing a voting member below quorum.");
        }
        member.IsActive = false;
        member.IsDeleted = true;
        member.DeletedAt = DateTime.UtcNow;
        member.DeletedBy = _currentUser.Username;
        member.UpdatedAt = DateTime.UtcNow;
        member.UpdatedBy = _currentUser.Username;
        member.LastModifiedById = _currentUser.UserId;
        await CommitteeMembers.UpdateAsync(member);
        await AddAuditAsync("COMMITTEE_MEMBER_REMOVED", "ProcurementCommitteeMember", member.Id,
            new { committee.Code, member.Assignment.UserId, member.Assignment.RoleName, member.MemberKind, member.IsVoting },
            new { Removed = true, Reason = request.Reason.Trim() }, correlationId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProcurementAccessWorkflowDto>> GetWorkflowsAsync(CancellationToken cancellationToken = default)
    {
        EnsureAdministrator();
        return await GetWorkflowsCoreAsync(cancellationToken);
    }

    public Task<ProcurementAccessCapabilityDecisionDto> CheckCapabilityAsync(
        ProcurementAccessCapabilityRequest request,
        string correlationId,
        CancellationToken cancellationToken = default) => EvaluateCapabilityAsync(request, correlationId, false, cancellationToken);

    public Task<ProcurementAccessCapabilityDecisionDto> EnforceCapabilityAsync(
        ProcurementAccessCapabilityRequest request,
        string correlationId,
        CancellationToken cancellationToken = default) => EvaluateCapabilityAsync(request, correlationId, true, cancellationToken);

    public async Task<IReadOnlyList<ProcurementAccessAuditDto>> GetAuditAsync(int take = 100, CancellationToken cancellationToken = default)
    {
        EnsureAdministrator();
        return await AuditLogs.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.Action.StartsWith(AuditPrefix))
            .AsNoTracking().OrderByDescending(item => item.Timestamp).Take(Math.Clamp(take, 1, 250))
            .Select(item => new ProcurementAccessAuditDto
            {
                Id = item.Id,
                Timestamp = item.Timestamp,
                ActorUserId = item.UserId,
                ActorName = item.Username,
                Action = item.Action,
                Resource = item.Resource,
                ResourceId = item.ResourceId,
                OldValues = item.OldValues,
                NewValues = item.NewValues
            }).ToListAsync(cancellationToken);
    }

    private async Task<ProcurementAccessCapabilityDecisionDto> EvaluateCapabilityAsync(
        ProcurementAccessCapabilityRequest request,
        string correlationId,
        bool auditDenied,
        CancellationToken cancellationToken)
    {
        EnsureAuthenticatedTenant();
        var permission = ProcurementAccessControlRegistry.FindPermission(request.PermissionCode)
            ?? throw new ProcurementAccessValidationException("PERMISSION_UNKNOWN", "PermissionCode must identify a required TDC procurement permission.");
        if (string.IsNullOrWhiteSpace(request.SourceType) || string.IsNullOrWhiteSpace(request.SourceReference))
            throw new ProcurementAccessValidationException("SOURCE_REQUIRED", "SourceType and SourceReference are required.");
        if (permission.IsWarehouseScoped && !request.WarehouseId.HasValue)
            throw new ProcurementAccessValidationException("WAREHOUSE_REQUIRED", "A warehouse ID is required for this permission.");
        var now = DateTime.UtcNow;
        var securityRoleNames = await UserTenants.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.UserId == _currentUser.UserId && !item.IsDeleted &&
                item.Status == UserTenantStatus.Active && (!item.ExpiresAt.HasValue || item.ExpiresAt > now) &&
                item.User.IsActive)
            .SelectMany(item => item.User.UserRoles)
            .Where(link => link.Role.Name != null)
            .Select(link => link.Role.Name!)
            .Distinct()
            .ToListAsync(cancellationToken);
        var assignments = await Assignments.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && item.UserId == _currentUser.UserId && !item.IsDeleted && item.IsActive &&
                item.EffectiveFrom <= now && (!item.EffectiveTo.HasValue || item.EffectiveTo >= now))
            .Include(item => item.Warehouses.Where(scope => !scope.IsDeleted))
            .Include(item => item.Locations.Where(scope => !scope.IsDeleted))
            .AsNoTracking().ToListAsync(cancellationToken);
        var permittedRoles = await Permissions.GetQueryable(item =>
                item.Name == permission.Code && !item.IsDeleted)
            .SelectMany(item => item.RolePermissions)
            .Where(item => item.Role.Name != null && securityRoleNames.Contains(item.Role.Name))
            .Select(item => item.Role.Name!)
            .Distinct().ToListAsync(cancellationToken);
        var matches = assignments.Where(item => permittedRoles.Contains(item.RoleName)).ToList();

        var code = "ACCESS_ALLOWED";
        var message = "The current actor's Security role grants this procurement privilege.";
        if (permittedRoles.Count == 0)
        {
            code = "ACCESS_PERMISSION_DENIED";
            message = "The current actor has no Security role granting this procurement privilege.";
        }
        else if (permission.IsWarehouseScoped)
        {
            var warehouseExists = await Warehouses.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId && item.Id == request.WarehouseId!.Value && !item.IsDeleted && item.IsActive)
                .AnyAsync(cancellationToken);
            if (!warehouseExists)
                throw new ProcurementAccessNotFoundException("The selected warehouse does not belong to the current tenant.");
            matches = matches.Where(item => item.WarehouseScopeMode == ProcurementWarehouseScopeMode.All ||
                                            (item.WarehouseScopeMode == ProcurementWarehouseScopeMode.Restricted &&
                                             item.Warehouses.Any(scope => scope.WarehouseId == request.WarehouseId.Value))).ToList();
            if (matches.Count == 0)
            {
                code = "ACCESS_WAREHOUSE_DENIED";
                message = "The Security role grants this privilege, but the current actor has no effective procurement scope for the requested warehouse.";
            }
        }

        if (code == "ACCESS_ALLOWED" && permission.IsWarehouseScoped && request.RequireLocationScope && !request.LocationId.HasValue)
        {
            matches = matches.Where(item => item.LocationScopeMode == ProcurementLocationScopeMode.All).ToList();
            if (matches.Count == 0)
            {
                code = "ACCESS_LOCATION_REQUIRED";
                message = "An explicit assigned warehouse location is required for this operation.";
            }
        }
        else if (code == "ACCESS_ALLOWED" && permission.IsWarehouseScoped && request.LocationId.HasValue)
        {
            var location = await WarehouseLocations.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId && item.Id == request.LocationId.Value &&
                    !item.IsDeleted && item.IsActive)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                ?? throw new ProcurementAccessNotFoundException("The selected location does not belong to the current tenant.");
            if (!request.WarehouseId.HasValue ||
                (location.WarehouseId != request.WarehouseId.Value &&
                 (!location.IsConsignmentBin || location.ConsignmentWarehouseId != request.WarehouseId.Value)))
                throw new ProcurementAccessValidationException("LOCATION_WAREHOUSE_MISMATCH",
                    "The selected location does not belong to the requested warehouse.");
            matches = matches.Where(item => item.LocationScopeMode == ProcurementLocationScopeMode.All ||
                                            (item.LocationScopeMode == ProcurementLocationScopeMode.Restricted &&
                                             item.Locations.Any(scope => scope.WarehouseLocationId == request.LocationId.Value)))
                .ToList();
            if (matches.Count == 0)
            {
                code = "ACCESS_LOCATION_DENIED";
                message = "The current actor is not assigned to the requested warehouse location.";
            }
        }

        if (code == "ACCESS_ALLOWED" && !string.IsNullOrWhiteSpace(request.CommitteeCode))
        {
            var committee = await Committees.GetQueryable(item =>
                    item.TenantId == _currentUser.TenantId && item.Code == request.CommitteeCode.Trim() && !item.IsDeleted &&
                    item.Status == ProcurementCommitteeStatus.Active && item.EffectiveFrom <= now &&
                    (!item.EffectiveTo.HasValue || item.EffectiveTo >= now))
                .Include(item => item.Members.Where(member => !member.IsDeleted && member.IsActive))
                    .ThenInclude(member => member.Assignment)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            var activeVoting = committee?.Members.Count(member => member.IsVoting && IsEffective(member.EffectiveFrom, member.EffectiveTo)) ?? 0;
            var actorIsEffectiveMember = committee?.Members.Any(member =>
                IsEffective(member.EffectiveFrom, member.EffectiveTo) &&
                IsEffective(member.Assignment.EffectiveFrom, member.Assignment.EffectiveTo) &&
                member.Assignment.IsActive &&
                member.Assignment.UserId == _currentUser.UserId) == true;
            if (committee is null || activeVoting < committee.RequiredQuorum ||
                matches.Count == 0 || !actorIsEffectiveMember)
            {
                code = "ACCESS_COMMITTEE_DENIED";
                message = "The committee is inactive, below quorum, or the current actor is not an effective member.";
            }
        }

        var decision = new ProcurementAccessCapabilityDecisionDto
        {
            Allowed = code == "ACCESS_ALLOWED",
            Code = code,
            Message = message,
            ActorUserId = _currentUser.UserId,
            TenantId = _currentUser.TenantId,
            PermissionCode = permission.Code,
            WarehouseId = request.WarehouseId,
            LocationId = request.LocationId,
            CommitteeCode = request.CommitteeCode?.Trim(),
            MatchedAssignmentIds = matches.Select(item => item.Id).ToList(),
            MatchedRoles = permittedRoles,
            CorrelationId = correlationId,
            EvaluatedAtUtc = now
        };
        if (auditDenied)
        {
            if (!decision.Allowed)
            {
                await AddAuditAsync("DENIED", "ProcurementAccessCapability", Guid.Empty, null,
                    new { decision.Code, decision.PermissionCode, decision.WarehouseId, decision.LocationId, decision.CommitteeCode, request.SourceType, request.SourceReference, decision.Message },
                    correlationId, cancellationToken, request.SourceReference);
            }
            await _controlEvents.RecordAsync(new ProcurementControlEventWriteRequest
            {
                EventKey = ProcurementControlEventKey.Create("access", _currentUser.TenantId, _currentUser.UserId,
                    correlationId, permission.Code, request.SourceType, request.SourceReference, Guid.NewGuid()),
                EventType = "AccessDecision",
                Action = "EnforceCapability",
                Result = decision.Allowed ? ProcurementControlEventResult.Allowed : ProcurementControlEventResult.Denied,
                RuleCode = permission.Code,
                DecisionKeys = string.IsNullOrWhiteSpace(request.CommitteeCode) ? new() : new() { "DEC-004" },
                SourceType = request.SourceType,
                SourceReference = request.SourceReference,
                Reason = decision.Message,
                InputValues = new { request.WarehouseId, request.LocationId, request.RequireLocationScope, request.CommitteeCode, Permission = permission.Code },
                ResultValues = new { decision.Allowed, decision.Code, decision.MatchedAssignmentIds, decision.MatchedRoles },
                CorrelationId = correlationId,
                OccurredAtUtc = decision.EvaluatedAtUtc
            }, cancellationToken);
        }
        return decision;
    }

    private async Task<List<ProcurementAccessRoleDto>> GetRolesCoreAsync(CancellationToken cancellationToken)
    {
        var configured = await Permissions.GetQueryable(item =>
                !item.IsDeleted && item.Category == ProcurementAccessControlRegistry.Category)
            .Include(item => item.RolePermissions).ThenInclude(item => item.Role)
            .AsNoTracking().ToListAsync(cancellationToken);
        return ProcurementAccessControlRegistry.Roles.Select(role =>
        {
            var actual = configured.Where(permission => permission.RolePermissions.Any(link => link.Role.Name == role.Code))
                .Select(permission => permission.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var unexpectedMutations = configured.Where(permission => actual.Contains(permission.Name) &&
                    ProcurementAccessControlRegistry.FindPermission(permission.Name)?.IsMutation == true &&
                    !role.PermissionCodes.Contains(permission.Name))
                .Select(permission => permission.Name).OrderBy(item => item).ToList();
            return new ProcurementAccessRoleDto
            {
                Code = role.Code,
                Name = role.Name,
                Description = role.Description,
                IsConfigured = configured.Any(permission => permission.RolePermissions.Any(link => link.Role.Name == role.Code)),
                IsReadOnly = role.IsReadOnly,
                RequiredPermissionCount = role.PermissionCodes.Count,
                ConfiguredPermissionCount = role.PermissionCodes.Count(actual.Contains),
                PermissionCodes = role.PermissionCodes.ToList(),
                MissingPermissionCodes = role.PermissionCodes.Where(item => !actual.Contains(item)).ToList(),
                UnexpectedMutationPermissions = unexpectedMutations
            };
        }).ToList();
    }

    private async Task<List<ProcurementAccessPermissionDto>> GetPermissionsCoreAsync(CancellationToken cancellationToken)
    {
        var configured = await Permissions.GetQueryable(item =>
                !item.IsDeleted && item.Category == ProcurementAccessControlRegistry.Category)
            .AsNoTracking().Select(item => item.Name).ToListAsync(cancellationToken);
        return ProcurementAccessControlRegistry.Permissions.Select(item => new ProcurementAccessPermissionDto
        {
            Code = item.Code,
            Name = item.Name,
            Description = item.Description,
            IsMutation = item.IsMutation,
            IsWarehouseScoped = item.IsWarehouseScoped,
            IsConfigured = configured.Contains(item.Code)
        }).ToList();
    }

    private async Task<List<ProcurementCommitteeDto>> GetCommitteesCoreAsync(CancellationToken cancellationToken)
    {
        var rows = await Committees.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.Members.Where(member => !member.IsDeleted))
                .ThenInclude(member => member.Assignment).ThenInclude(assignment => assignment.User)
            .AsNoTracking().OrderBy(item => item.Code).ToListAsync(cancellationToken);
        return rows.Select(MapCommittee).ToList();
    }

    private async Task<List<ProcurementAccessWorkflowDto>> GetWorkflowsCoreAsync(CancellationToken cancellationToken)
    {
        var definitions = await WorkflowDefinitions.GetQueryable(item =>
                item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.EntityType).Include(item => item.Steps)
            .AsNoTracking().ToListAsync(cancellationToken);
        return ProcurementAccessControlRegistry.Workflows.Select(template =>
        {
            var definition = definitions.Where(item => item.Name == template.Name)
                .OrderByDescending(item => item.Version).FirstOrDefault();
            return new ProcurementAccessWorkflowDto
            {
                TemplateCode = template.Code,
                TemplateName = template.Name,
                EntityTypeCode = template.EntityTypeCode,
                WorkflowDefinitionId = definition?.Id,
                Version = definition?.Version,
                Status = definition?.LifecycleStatus.ToString() ?? "Missing",
                IsPublished = definition?.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published && definition.IsActive,
                InitiatorRoleName = template.InitiatorRoleCode,
                ApprovalRoleName = template.ApprovalRoleCode
            };
        }).ToList();
    }

    private Task<List<ProcurementResponsibilityAssignment>> LoadAssignmentsAsync(CancellationToken cancellationToken) =>
        Assignments.GetQueryable(item => item.TenantId == _currentUser.TenantId && !item.IsDeleted)
            .Include(item => item.User).Include(item => item.Role)
            .Include(item => item.Warehouses.Where(scope => !scope.IsDeleted)).ThenInclude(scope => scope.Warehouse)
            .Include(item => item.Locations.Where(scope => !scope.IsDeleted)).ThenInclude(scope => scope.WarehouseLocation)
            .Include(item => item.Locations.Where(scope => !scope.IsDeleted)).ThenInclude(scope => scope.Warehouse)
            .AsNoTracking().OrderBy(item => item.User.FirstName).ThenBy(item => item.RoleName).ToListAsync(cancellationToken);

    private async Task<(Guid RoleId, string RoleName)?> ResolveConfiguredRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        var configuredRole = await Permissions.GetQueryable(item => !item.IsDeleted)
            .SelectMany(item => item.RolePermissions)
            .Where(item => item.Role.Name == roleName)
            .Select(item => new { item.RoleId, RoleName = item.Role.Name! })
            .FirstOrDefaultAsync(cancellationToken);
        return configuredRole is null ? null : (configuredRole.RoleId, configuredRole.RoleName);
    }

    private async Task AddAuditAsync(
        string action,
        string resource,
        Guid resourceId,
        object? oldValues,
        object? newValues,
        string correlationId,
        CancellationToken cancellationToken,
        string? resourceReference = null)
    {
        await AuditLogs.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            UserId = _currentUser.UserId,
            Username = Truncate(_currentUser.Username, 255),
            Action = AuditPrefix + action,
            Resource = resource,
            ResourceId = Truncate(resourceReference ?? (resourceId == Guid.Empty ? correlationId : resourceId.ToString()), 100),
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = JsonSerializer.Serialize(new { CorrelationId = correlationId, Values = newValues }),
            IpAddress = "Unknown",
            Timestamp = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.FullName,
            CreatedById = _currentUser.UserId
        });
    }

    private static ProcurementResponsibilityAssignmentDto MapAssignment(ProcurementResponsibilityAssignment item) => new()
    {
        Id = item.Id,
        UserId = item.UserId,
        Username = item.User.UserName ?? string.Empty,
        UserDisplayName = (item.User.FirstName + " " + item.User.LastName).Trim(),
        RoleId = item.RoleId,
        RoleName = item.RoleName,
        RoleDisplayName = ProcurementAccessControlRegistry.FindRole(item.RoleName)?.Name ?? item.RoleName,
        WarehouseScopeMode = item.WarehouseScopeMode,
        Warehouses = item.Warehouses.Where(scope => !scope.IsDeleted).Select(scope => new ProcurementAccessWarehouseOptionDto
        {
            WarehouseId = scope.WarehouseId,
            Code = scope.Warehouse.Code,
            Name = scope.Warehouse.Name,
            IsActive = scope.Warehouse.IsActive
        }).ToList(),
        LocationScopeMode = item.LocationScopeMode,
        Locations = item.Locations.Where(scope => !scope.IsDeleted).Select(scope => new ProcurementAccessLocationOptionDto
        {
            LocationId = scope.WarehouseLocationId,
            WarehouseId = scope.WarehouseId,
            WarehouseCode = scope.Warehouse.Code,
            Code = scope.WarehouseLocation.LocationCode,
            Name = scope.WarehouseLocation.Name ?? scope.WarehouseLocation.LocationCode,
            IsActive = scope.WarehouseLocation.IsActive
        }).ToList(),
        EffectiveFrom = item.EffectiveFrom,
        EffectiveTo = item.EffectiveTo,
        IsActive = item.IsActive,
        Reason = item.Reason,
        RowVersion = Convert.ToBase64String(item.RowVersion)
    };

    private static ProcurementCommitteeDto MapCommittee(ProcurementCommittee item)
    {
        var members = item.Members.Where(member => !member.IsDeleted).Select(member => new ProcurementCommitteeMemberDto
        {
            Id = member.Id,
            AssignmentId = member.AssignmentId,
            UserId = member.Assignment.UserId,
            Username = member.Assignment.User.UserName ?? string.Empty,
            UserDisplayName = (member.Assignment.User.FirstName + " " + member.Assignment.User.LastName).Trim(),
            RoleName = member.Assignment.RoleName,
            MemberKind = member.MemberKind,
            IsVoting = member.IsVoting,
            IsActive = member.IsActive,
            EffectiveFrom = member.EffectiveFrom,
            EffectiveTo = member.EffectiveTo,
            RowVersion = Convert.ToBase64String(member.RowVersion)
        }).ToList();
        var activeVoting = members.Count(member => member.IsActive && member.IsVoting && IsEffective(member.EffectiveFrom, member.EffectiveTo));
        return new ProcurementCommitteeDto
        {
            Id = item.Id,
            Code = item.Code,
            Name = item.Name,
            Description = item.Description,
            CommitteeType = item.CommitteeType,
            Status = item.Status,
            RequiredQuorum = item.RequiredQuorum,
            ActiveVotingMemberCount = activeVoting,
            MeetsQuorum = activeVoting >= item.RequiredQuorum,
            RequiredRoleName = item.RequiredRoleName,
            EffectiveFrom = item.EffectiveFrom,
            EffectiveTo = item.EffectiveTo,
            RowVersion = Convert.ToBase64String(item.RowVersion),
            Members = members
        };
    }

    private static object AssignmentAuditShape(
        ProcurementResponsibilityAssignment item,
        IReadOnlyList<Guid>? warehouses = null,
        IReadOnlyList<Guid>? locations = null) => new
    {
        item.UserId,
        item.RoleId,
        item.RoleName,
        item.WarehouseScopeMode,
        WarehouseIds = warehouses ?? item.Warehouses.Where(scope => !scope.IsDeleted).Select(scope => scope.WarehouseId).ToList(),
        item.LocationScopeMode,
        LocationIds = locations ?? item.Locations.Where(scope => !scope.IsDeleted).Select(scope => scope.WarehouseLocationId).ToList(),
        item.EffectiveFrom,
        item.EffectiveTo,
        item.IsActive,
        item.Reason
    };

    private static object CommitteeAuditShape(ProcurementCommittee item) => new
    {
        item.Code,
        item.Name,
        item.Description,
        item.Status,
        item.RequiredQuorum,
        item.RequiredRoleName,
        item.EffectiveFrom,
        item.EffectiveTo,
        item.ChangeReason
    };

    private static void ValidateAssignmentRequest(SaveProcurementResponsibilityAssignmentRequest request)
    {
        if (request.UserId == Guid.Empty) throw new ProcurementAccessValidationException("USER_REQUIRED", "UserId is required.");
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new ProcurementAccessValidationException("REASON_REQUIRED", "A reason is required.");
        ValidatePeriod(request.EffectiveFrom, request.EffectiveTo);
    }

    private static void ValidatePeriod(DateTime from, DateTime? to)
    {
        if (to.HasValue && EnsureUtc(to.Value) < EnsureUtc(from))
            throw new ProcurementAccessValidationException("EFFECTIVE_PERIOD_INVALID", "EffectiveTo cannot precede EffectiveFrom.");
    }

    private static void EnsureRowVersion(byte[] current, string? supplied)
    {
        if (string.IsNullOrWhiteSpace(supplied))
            throw new ProcurementAccessValidationException("ROW_VERSION_REQUIRED", "RowVersion is required for an update.");
        byte[] expected;
        try { expected = Convert.FromBase64String(supplied); }
        catch (FormatException) { throw new ProcurementAccessValidationException("ROW_VERSION_INVALID", "RowVersion is not valid base64."); }
        if (!current.SequenceEqual(expected)) throw new ProcurementAccessConflictException("The record changed after it was loaded. Refresh and retry.");
    }

    private void EnsureAdministrator()
    {
        EnsureAuthenticatedTenant();
        if (!_currentUser.Roles.Any(role =>
                ProcurementAccessControlRegistry.RoleGrantsPermission(
                    role, "procurement.access.manage")))
            throw new ProcurementAccessAuthorizationException(
                "The TDC ICT Administrator role is required to manage procurement access.");
    }

    private void EnsureAuthenticatedTenant()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty || _currentUser.TenantId == Guid.Empty)
            throw new ProcurementAccessAuthorizationException("An authenticated tenant context is required.");
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static bool IsEffective(DateTime from, DateTime? to)
    {
        var now = DateTime.UtcNow;
        return EnsureUtc(from) <= now && (!to.HasValue || EnsureUtc(to.Value) >= now);
    }

    private static string Truncate(string? value, int length) =>
        string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Length <= length ? value : value[..length];
}
