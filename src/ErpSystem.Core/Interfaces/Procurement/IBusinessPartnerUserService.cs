using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

/// <summary>
/// Service interface for managing business partner users
/// </summary>
public interface IBusinessPartnerUserService
{
    Task<BusinessPartnerUserDto> LinkExistingExternalUserAsync(Guid businessPartnerId, Guid userId);
    /// <summary>
    /// Get all users for a business partner
    /// </summary>
    Task<IEnumerable<BusinessPartnerUserDto>> GetUsersByBusinessPartnerIdAsync(Guid businessPartnerId);

    /// <summary>
    /// Get a specific business partner user by ID
    /// </summary>
    Task<BusinessPartnerUserDto?> GetByIdAsync(Guid id);

    /// <summary>
    /// Get business partner user by user ID
    /// </summary>
    Task<BusinessPartnerUserDto?> GetByUserIdAsync(Guid userId);

    /// <summary>
    /// Create a new business partner user
    /// </summary>
    Task<BusinessPartnerUserDto> CreateAsync(CreateBusinessPartnerUserDto dto);

    /// <summary>
    /// Update an existing business partner user
    /// </summary>
    Task<BusinessPartnerUserDto> UpdateAsync(Guid id, UpdateBusinessPartnerUserDto dto);

    /// <summary>
    /// Activate a business partner user
    /// </summary>
    Task ActivateAsync(Guid id);

    /// <summary>
    /// Deactivate a business partner user
    /// </summary>
    Task DeactivateAsync(Guid id);

    /// <summary>
    /// Delete a business partner user
    /// </summary>
    Task DeleteAsync(Guid id);

    /// <summary>
    /// Check if a user has access to a business partner
    /// </summary>
    Task<bool> HasAccessAsync(Guid userId, Guid businessPartnerId);

    /// <summary>
    /// Get the role of a user for a business partner
    /// </summary>
    Task<string?> GetUserRoleAsync(Guid userId, Guid businessPartnerId);

    /// <summary>
    /// Reset password for a business partner user
    /// </summary>
    Task ResetPasswordAsync(Guid id, string newPassword);
}
