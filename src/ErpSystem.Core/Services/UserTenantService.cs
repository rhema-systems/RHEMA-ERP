using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services;

/// <summary>
/// Service for managing user-tenant relationships without deleting users
/// </summary>
public interface IUserTenantService
{
    // Grant/Revoke Access
    Task<UserTenant> GrantUserAccessToTenantAsync(Guid userId, Guid tenantId, UserTenantAccessLevel accessLevel = UserTenantAccessLevel.Standard, string? grantedBy = null, DateTime? expiresAt = null, string? notes = null);
    Task RevokeUserAccessFromTenantAsync(Guid userId, Guid tenantId, string? revokedBy = null, string? reason = null);
    Task<bool> SuspendUserFromTenantAsync(Guid userId, Guid tenantId, string? suspendedBy = null, string? reason = null);
    Task<bool> ReactivateUserInTenantAsync(Guid userId, Guid tenantId, string? reactivatedBy = null);

    // Status Management
    Task<bool> UpdateUserTenantStatusAsync(Guid userId, Guid tenantId, UserTenantStatus newStatus, string? changedBy = null, string? reason = null);
    Task<bool> SetDefaultTenantAsync(Guid userId, Guid tenantId);
    Task<bool> UpdateAccessLevelAsync(Guid userId, Guid tenantId, UserTenantAccessLevel newAccessLevel, string? changedBy = null);

    // Query Methods
    Task<IEnumerable<UserTenant>> GetActiveUserTenantsAsync(Guid userId);
    Task<IEnumerable<UserTenant>> GetAllUserTenantsAsync(Guid userId);
    Task<IEnumerable<ApplicationUser>> GetActiveTenantUsersAsync(Guid tenantId);
    Task<UserTenant?> GetUserTenantRelationshipAsync(Guid userId, Guid tenantId);
    Task<bool> HasActiveAccessAsync(Guid userId, Guid tenantId);

    // Bulk Operations
    Task<int> BulkGrantAccessAsync(IEnumerable<Guid> userIds, Guid tenantId, UserTenantAccessLevel accessLevel = UserTenantAccessLevel.Standard, string? grantedBy = null);
    Task<int> BulkRevokeAccessAsync(IEnumerable<Guid> userIds, Guid tenantId, string? revokedBy = null);
    Task<int> ExpireAccessForAllUsersInTenantAsync(Guid tenantId, string? expiredBy = null);
}

public class UserTenantService : IUserTenantService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<UserTenantService> _logger;
    private readonly ICurrentUserService _currentUserService;

    public UserTenantService(
        IUnitOfWork unitOfWork,
        UserManager<ApplicationUser> userManager,
        ILogger<UserTenantService> logger,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _logger = logger;
        _currentUserService = currentUserService;
    }

    #region Grant/Revoke Access

    public async Task<UserTenant> GrantUserAccessToTenantAsync(
        Guid userId,
        Guid tenantId,
        UserTenantAccessLevel accessLevel = UserTenantAccessLevel.Standard,
        string? grantedBy = null,
        DateTime? expiresAt = null,
        string? notes = null)
    {
        _logger.LogInformation("Granting user {UserId} access to tenant {TenantId} with level {AccessLevel}",
            userId, tenantId, accessLevel);
        var normalizedNotes = NormalizeNotes(notes);

        var userTenantRepo = _unitOfWork.Repository<UserTenant>();
        // Include a previously soft-deleted relationship so a repeated grant restores the
        // authoritative row instead of creating a duplicate user/tenant mapping.
        var existingRelationship = await userTenantRepo
            .GetQueryableIncludingDeleted(item => item.UserId == userId && item.TenantId == tenantId)
            .FirstOrDefaultAsync();

        if (existingRelationship != null)
        {
            var actor = grantedBy ?? _currentUserService.UserName;
            var actorId = Guid.TryParse(_currentUserService.UserId, out var currentUserId)
                ? currentUserId
                : (Guid?)null;
            var alreadyMatches = !existingRelationship.IsDeleted &&
                                 existingRelationship.Status == UserTenantStatus.Active &&
                                 existingRelationship.AccessLevel == accessLevel &&
                                 existingRelationship.ExpiresAt == expiresAt;
            if (alreadyMatches)
            {
                _logger.LogInformation(
                    "User {UserId} already has the requested access to tenant {TenantId}",
                    userId, tenantId);
                return existingRelationship;
            }

            var wasInactive = existingRelationship.IsDeleted ||
                              existingRelationship.Status != UserTenantStatus.Active;
            var changedAt = DateTime.UtcNow;
            existingRelationship.Status = UserTenantStatus.Active;
            existingRelationship.AccessLevel = accessLevel;
            existingRelationship.ExpiresAt = expiresAt;
            if (wasInactive)
            {
                existingRelationship.ReactivatedAt = changedAt;
                existingRelationship.StatusChangedBy = actor;
            }
            existingRelationship.UpdatedAt = changedAt;
            existingRelationship.UpdatedBy = actor;
            existingRelationship.LastModifiedById = actorId;
            existingRelationship.Notes = AppendAuditNote(
                existingRelationship.Notes,
                wasInactive ? "REACTIVATED" : "ACCESS UPDATED",
                changedAt,
                normalizedNotes ?? (wasInactive ? "Access reactivated" : "Access updated"));

            // If previously soft-deleted, restore it
            if (existingRelationship.IsDeleted)
            {
                existingRelationship.IsDeleted = false;
                existingRelationship.DeletedAt = null;
                existingRelationship.DeletedBy = null;
            }

            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation("Reactivated existing user-tenant relationship: {UserId} -> {TenantId}", userId, tenantId);
            return existingRelationship;
        }

        // Create new relationship
        var createdBy = grantedBy ?? _currentUserService.UserName;
        var createdById = Guid.TryParse(_currentUserService.UserId, out var actorUserId)
            ? actorUserId
            : (Guid?)null;
        var userTenant = new UserTenant
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            AccessLevel = accessLevel,
            Status = UserTenantStatus.Active,
            GrantedAt = DateTime.UtcNow,
            GrantedBy = createdBy,
            ExpiresAt = expiresAt,
            Notes = normalizedNotes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            CreatedById = createdById
        };

        await userTenantRepo.AddAsync(userTenant);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created new user-tenant relationship: {UserId} -> {TenantId}", userId, tenantId);
        return userTenant;
    }

    public async Task RevokeUserAccessFromTenantAsync(Guid userId, Guid tenantId, string? revokedBy = null, string? reason = null)
    {
        _logger.LogInformation("Revoking user {UserId} access from tenant {TenantId}", userId, tenantId);

        // Query the relationship directly by its business key. Revoke does not need
        // navigation loading, and a missing related navigation must never turn a
        // persisted mapping into a false "not found" result.
        var relationship = await _unitOfWork.Repository<UserTenant>()
            .GetQueryableIncludingDeleted(item => item.UserId == userId && item.TenantId == tenantId)
            .FirstOrDefaultAsync();
        if (relationship == null)
        {
            _logger.LogWarning("No relationship found between user {UserId} and tenant {TenantId}", userId, tenantId);
            return;
        }

        if (relationship.IsDeleted || relationship.Status == UserTenantStatus.Revoked)
        {
            _logger.LogInformation(
                "User {UserId} access to tenant {TenantId} is already revoked",
                userId, tenantId);
            return;
        }

        // Update status to revoked instead of soft delete
        var actor = revokedBy ?? _currentUserService.UserName;
        var revokedAt = DateTime.UtcNow;
        relationship.Status = UserTenantStatus.Revoked;
        relationship.StatusChangedBy = actor;
        relationship.UpdatedAt = revokedAt;
        relationship.UpdatedBy = actor;
        relationship.LastModifiedById = Guid.TryParse(_currentUserService.UserId, out var actorUserId)
            ? actorUserId
            : null;
        relationship.Notes = AppendAuditNote(
            relationship.Notes,
            "REVOKED",
            revokedAt,
            reason ?? "Access revoked");

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is not null && user.TenantId == tenantId)
        {
            var now = DateTime.UtcNow;
            var replacement = await _unitOfWork.Repository<UserTenant>()
                .GetQueryable(item => item.UserId == userId && item.TenantId != tenantId &&
                                      !item.IsDeleted && item.Status == UserTenantStatus.Active &&
                                      (!item.ExpiresAt.HasValue || item.ExpiresAt.Value > now))
                .OrderByDescending(item => item.IsDefault)
                .ThenBy(item => item.GrantedAt)
                .FirstOrDefaultAsync();
            user.TenantId = replacement?.TenantId ?? Guid.Empty;
            user.UpdatedAt = revokedAt;
            user.UpdatedBy = actor;
            await _userManager.UpdateAsync(user);
        }

        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Revoked user-tenant relationship: {UserId} -> {TenantId}", userId, tenantId);
    }

    private static string AppendAuditNote(
        string? existing,
        string action,
        DateTime occurredAt,
        string detail)
    {
        const int maxNotesLength = 500;
        var prefix = $"[{action}] {occurredAt:O}: ";
        var normalizedDetail = detail.Trim();
        if (prefix.Length + normalizedDetail.Length > maxNotesLength)
            normalizedDetail = normalizedDetail[..(maxNotesLength - prefix.Length)];

        var entry = prefix + normalizedDetail;
        if (string.IsNullOrWhiteSpace(existing))
            return entry;

        var availableForExisting = maxNotesLength - entry.Length - Environment.NewLine.Length;
        if (availableForExisting <= 0)
            return entry;

        var preservedExisting = existing.Trim();
        if (preservedExisting.Length > availableForExisting)
            preservedExisting = preservedExisting[..availableForExisting];
        return $"{preservedExisting}{Environment.NewLine}{entry}";
    }

    private static string? NormalizeNotes(string? notes)
    {
        const int maxNotesLength = 500;
        if (string.IsNullOrWhiteSpace(notes)) return null;
        var normalized = notes.Trim();
        return normalized.Length <= maxNotesLength
            ? normalized
            : normalized[..maxNotesLength];
    }

    public async Task<bool> SuspendUserFromTenantAsync(Guid userId, Guid tenantId, string? suspendedBy = null, string? reason = null)
    {
        _logger.LogInformation("Suspending user {UserId} from tenant {TenantId}", userId, tenantId);

        var relationship = await GetUserTenantRelationshipAsync(userId, tenantId);
        if (relationship == null || relationship.Status != UserTenantStatus.Active)
        {
            _logger.LogWarning("Cannot suspend: No active relationship found between user {UserId} and tenant {TenantId}", userId, tenantId);
            return false;
        }

        relationship.Status = UserTenantStatus.Suspended;
        relationship.SuspendedAt = DateTime.UtcNow;
        relationship.StatusChangedBy = suspendedBy ?? _currentUserService.UserName;
        relationship.UpdatedAt = DateTime.UtcNow;
        relationship.UpdatedBy = suspendedBy ?? _currentUserService.UserName;
        relationship.Notes = $"{relationship.Notes}\n[SUSPENDED] {DateTime.UtcNow}: {reason ?? "User suspended"}".Trim();

        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Suspended user-tenant relationship: {UserId} -> {TenantId}", userId, tenantId);
        return true;
    }

    public async Task<bool> ReactivateUserInTenantAsync(Guid userId, Guid tenantId, string? reactivatedBy = null)
    {
        _logger.LogInformation("Reactivating user {UserId} in tenant {TenantId}", userId, tenantId);

        var relationship = await GetUserTenantRelationshipAsync(userId, tenantId);
        if (relationship == null || relationship.Status == UserTenantStatus.Active)
        {
            _logger.LogWarning("Cannot reactivate: No suspended relationship found between user {UserId} and tenant {TenantId}", userId, tenantId);
            return false;
        }

        relationship.Status = UserTenantStatus.Active;
        relationship.ReactivatedAt = DateTime.UtcNow;
        relationship.StatusChangedBy = reactivatedBy ?? _currentUserService.UserName;
        relationship.UpdatedAt = DateTime.UtcNow;
        relationship.UpdatedBy = reactivatedBy ?? _currentUserService.UserName;
        relationship.Notes = $"{relationship.Notes}\n[REACTIVATED] {DateTime.UtcNow}: User reactivated".Trim();

        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Reactivated user-tenant relationship: {UserId} -> {TenantId}", userId, tenantId);
        return true;
    }

    #endregion

    #region Status Management

    public async Task<bool> UpdateUserTenantStatusAsync(Guid userId, Guid tenantId, UserTenantStatus newStatus, string? changedBy = null, string? reason = null)
    {
        var relationship = await GetUserTenantRelationshipAsync(userId, tenantId);
        if (relationship == null)
        {
            return false;
        }

        var oldStatus = relationship.Status;
        relationship.Status = newStatus;
        relationship.StatusChangedBy = changedBy ?? _currentUserService.UserName;
        relationship.UpdatedAt = DateTime.UtcNow;
        relationship.UpdatedBy = changedBy ?? _currentUserService.UserName;
        relationship.Notes = $"{relationship.Notes}\n[STATUS CHANGE] {DateTime.UtcNow}: {oldStatus} -> {newStatus} - {reason ?? "Status updated"}".Trim();

        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Updated user-tenant status: {UserId} -> {TenantId} ({OldStatus} -> {NewStatus})",
            userId, tenantId, oldStatus, newStatus);
        return true;
    }

    public async Task<bool> SetDefaultTenantAsync(Guid userId, Guid tenantId)
    {
        var userTenantRepo = _unitOfWork.Repository<UserTenant>();

        // Remove default from all other tenants for this user
        var userTenants = await userTenantRepo.GetQueryable()
            .Where(ut => ut.UserId == userId && !ut.IsDeleted)
            .ToListAsync();

        foreach (var ut in userTenants)
        {
            ut.IsDefault = ut.TenantId == tenantId;
            ut.UpdatedAt = DateTime.UtcNow;
            ut.UpdatedBy = _currentUserService.UserName;
        }

        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Set default tenant for user {UserId} to {TenantId}", userId, tenantId);
        return true;
    }

    public async Task<bool> UpdateAccessLevelAsync(Guid userId, Guid tenantId, UserTenantAccessLevel newAccessLevel, string? changedBy = null)
    {
        var relationship = await GetUserTenantRelationshipAsync(userId, tenantId);
        if (relationship == null)
        {
            return false;
        }

        var oldAccessLevel = relationship.AccessLevel;
        relationship.AccessLevel = newAccessLevel;
        relationship.UpdatedAt = DateTime.UtcNow;
        relationship.UpdatedBy = changedBy ?? _currentUserService.UserName;
        relationship.Notes = $"{relationship.Notes}\n[ACCESS LEVEL CHANGE] {DateTime.UtcNow}: {oldAccessLevel} -> {newAccessLevel}".Trim();

        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Updated access level for user {UserId} in tenant {TenantId}: {OldLevel} -> {NewLevel}",
            userId, tenantId, oldAccessLevel, newAccessLevel);
        return true;
    }

    #endregion

    #region Query Methods

    public async Task<IEnumerable<UserTenant>> GetActiveUserTenantsAsync(Guid userId)
    {
        var userTenantRepo = _unitOfWork.Repository<UserTenant>();
        return await userTenantRepo.GetQueryable()
            .Include(ut => ut.Tenant)
            .Where(ut => ut.UserId == userId &&
                        !ut.IsDeleted &&
                        ut.Status == UserTenantStatus.Active &&
                        (ut.ExpiresAt == null || ut.ExpiresAt > DateTime.UtcNow))
            .ToListAsync();
    }

    public async Task<IEnumerable<UserTenant>> GetAllUserTenantsAsync(Guid userId)
    {
        var userTenantRepo = _unitOfWork.Repository<UserTenant>();
        return await userTenantRepo.GetQueryable()
            .Include(ut => ut.Tenant)
            .Where(ut => ut.UserId == userId && !ut.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<ApplicationUser>> GetActiveTenantUsersAsync(Guid tenantId)
    {
        var userTenantRepo = _unitOfWork.Repository<UserTenant>();
        var activeRelationships = await userTenantRepo.GetQueryable()
            .Include(ut => ut.User)
            .Where(ut => ut.TenantId == tenantId &&
                        !ut.IsDeleted &&
                        ut.Status == UserTenantStatus.Active &&
                        (ut.ExpiresAt == null || ut.ExpiresAt > DateTime.UtcNow))
            .ToListAsync();

        return activeRelationships.Select(ut => ut.User).Distinct();
    }

    public async Task<UserTenant?> GetUserTenantRelationshipAsync(Guid userId, Guid tenantId)
    {
        var userTenantRepo = _unitOfWork.Repository<UserTenant>();
        return await userTenantRepo.GetQueryable()
            .Include(ut => ut.User)
            .Include(ut => ut.Tenant)
            .FirstOrDefaultAsync(ut => ut.UserId == userId && ut.TenantId == tenantId && !ut.IsDeleted);
    }

    public async Task<bool> HasActiveAccessAsync(Guid userId, Guid tenantId)
    {
        var relationship = await GetUserTenantRelationshipAsync(userId, tenantId);
        return relationship != null &&
               relationship.Status == UserTenantStatus.Active &&
               (relationship.ExpiresAt == null || relationship.ExpiresAt > DateTime.UtcNow);
    }

    #endregion

    #region Bulk Operations

    public async Task<int> BulkGrantAccessAsync(IEnumerable<Guid> userIds, Guid tenantId, UserTenantAccessLevel accessLevel = UserTenantAccessLevel.Standard, string? grantedBy = null)
    {
        var count = 0;
        var grantedByUser = grantedBy ?? _currentUserService.UserName;

        foreach (var userId in userIds)
        {
            try
            {
                await GrantUserAccessToTenantAsync(userId, tenantId, accessLevel, grantedByUser);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to grant access to user {UserId} for tenant {TenantId}", userId, tenantId);
            }
        }

        _logger.LogInformation("Bulk granted access to {Count} users for tenant {TenantId}", count, tenantId);
        return count;
    }

    public async Task<int> BulkRevokeAccessAsync(IEnumerable<Guid> userIds, Guid tenantId, string? revokedBy = null)
    {
        var count = 0;
        var revokedByUser = revokedBy ?? _currentUserService.UserName;

        foreach (var userId in userIds)
        {
            try
            {
                await RevokeUserAccessFromTenantAsync(userId, tenantId, revokedByUser, "Bulk revocation");
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to revoke access for user {UserId} from tenant {TenantId}", userId, tenantId);
            }
        }

        _logger.LogInformation("Bulk revoked access for {Count} users from tenant {TenantId}", count, tenantId);
        return count;
    }

    public async Task<int> ExpireAccessForAllUsersInTenantAsync(Guid tenantId, string? expiredBy = null)
    {
        var userTenantRepo = _unitOfWork.Repository<UserTenant>();
        var activeRelationships = await userTenantRepo.GetQueryable()
            .Where(ut => ut.TenantId == tenantId &&
                        !ut.IsDeleted &&
                        ut.Status == UserTenantStatus.Active)
            .ToListAsync();

        var expiredByUser = expiredBy ?? _currentUserService.UserName;
        var expiredAt = DateTime.UtcNow;

        foreach (var relationship in activeRelationships)
        {
            relationship.Status = UserTenantStatus.Expired;
            relationship.ExpiresAt = expiredAt;
            relationship.StatusChangedBy = expiredByUser;
            relationship.UpdatedAt = expiredAt;
            relationship.UpdatedBy = expiredByUser;
            relationship.Notes = $"{relationship.Notes}\n[EXPIRED] {expiredAt}: All access expired for tenant".Trim();
        }

        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Expired access for {Count} users in tenant {TenantId}", activeRelationships.Count, tenantId);
        return activeRelationships.Count;
    }

    #endregion
}
