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

        // Check if relationship already exists
        var existingRelationship = await GetUserTenantRelationshipAsync(userId, tenantId);
        
        if (existingRelationship != null)
        {
            // Reactivate existing relationship
            existingRelationship.Status = UserTenantStatus.Active;
            existingRelationship.AccessLevel = accessLevel;
            existingRelationship.ExpiresAt = expiresAt;
            existingRelationship.ReactivatedAt = DateTime.UtcNow;
            existingRelationship.StatusChangedBy = grantedBy ?? _currentUserService.UserName;
            existingRelationship.UpdatedAt = DateTime.UtcNow;
            existingRelationship.UpdatedBy = grantedBy ?? _currentUserService.UserName;
            existingRelationship.Notes = notes;
            
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
        var userTenant = new UserTenant
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            AccessLevel = accessLevel,
            Status = UserTenantStatus.Active,
            GrantedAt = DateTime.UtcNow,
            GrantedBy = grantedBy ?? _currentUserService.UserName,
            ExpiresAt = expiresAt,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = grantedBy ?? _currentUserService.UserName
        };

        var userTenantRepo = _unitOfWork.Repository<UserTenant>();
        await userTenantRepo.AddAsync(userTenant);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created new user-tenant relationship: {UserId} -> {TenantId}", userId, tenantId);
        return userTenant;
    }

    public async Task RevokeUserAccessFromTenantAsync(Guid userId, Guid tenantId, string? revokedBy = null, string? reason = null)
    {
        _logger.LogInformation("Revoking user {UserId} access from tenant {TenantId}", userId, tenantId);

        var relationship = await GetUserTenantRelationshipAsync(userId, tenantId);
        if (relationship == null)
        {
            _logger.LogWarning("No relationship found between user {UserId} and tenant {TenantId}", userId, tenantId);
            return;
        }

        // Update status to revoked instead of soft delete
        relationship.Status = UserTenantStatus.Revoked;
        relationship.StatusChangedBy = revokedBy ?? _currentUserService.UserName;
        relationship.UpdatedAt = DateTime.UtcNow;
        relationship.UpdatedBy = revokedBy ?? _currentUserService.UserName;
        relationship.Notes = $"{relationship.Notes}\n[REVOKED] {DateTime.UtcNow}: {reason ?? "Access revoked"}".Trim();

        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Revoked user-tenant relationship: {UserId} -> {TenantId}", userId, tenantId);
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
        if (relationship == null) return false;

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
        if (relationship == null) return false;

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