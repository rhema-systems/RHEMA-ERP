using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace ErpSystem.Core.Services.Procurement;

public partial class BusinessPartnerUserService : IBusinessPartnerUserService
{
    private readonly IBusinessPartnerUserRepository _repository;
    private readonly IUserService _userService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<BusinessPartnerUserService> _logger;

    public BusinessPartnerUserService(
        IBusinessPartnerUserRepository repository,
        IUserService userService,
        UserManager<ApplicationUser> userManager,
        IUnitOfWork unitOfWork,
        ITenantContext tenantContext,
        ICurrentUserService currentUserService,
        ILogger<BusinessPartnerUserService> logger)
    {
        _repository = repository;
        _userService = userService;
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _tenantContext = tenantContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<IEnumerable<BusinessPartnerUserDto>> GetUsersByBusinessPartnerIdAsync(Guid businessPartnerId)
    {
        var users = await _repository.GetByBusinessPartnerIdAsync(businessPartnerId);
        return users.Select(MapToDto);
    }

    public async Task<BusinessPartnerUserDto?> GetByIdAsync(Guid id)
    {
        var user = await _repository.GetByIdAsync(id);
        return user != null ? MapToDto(user) : null;
    }

    public async Task<BusinessPartnerUserDto?> GetByUserIdAsync(Guid userId)
    {
        var user = await _repository.GetByUserIdAsync(userId);
        return user != null ? MapToDto(user) : null;
    }

    public async Task<BusinessPartnerUserDto> CreateAsync(CreateBusinessPartnerUserDto dto)
    {
        try
        {
            var tenantId = _tenantContext.GetCurrentTenantId();
            var currentUserId = _currentUserService.UserId != null ? Guid.Parse(_currentUserService.UserId) : Guid.Empty;

            // Check if user already exists
            var existingUser = await _userService.GetUserByEmailAsync(dto.Email);

            ApplicationUser user;
            if (existingUser != null)
            {
                // User exists, just link to business partner
                user = existingUser;

                // Check if already linked
                var exists = await _repository.ExistsAsync(dto.BusinessPartnerId, user.Id);
                if (exists)
                {
                    throw new InvalidOperationException("User is already linked to this business partner");
                }
            }
            else
            {
                // Create new user
                user = new ApplicationUser
                {
                    UserName = dto.UserName,
                    Email = dto.Email,
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    PhoneNumber = dto.PhoneNumber,
                    IsActive = true,
                    TenantId = tenantId,
                    AuthenticationProvider = ErpSystem.Shared.AuthenticationProvider.Local
                };

                user = await _userService.CreateUserAsync(user, dto.Password);
            }

            // Create business partner user link
            var businessPartnerUser = new BusinessPartnerUser
            {
                Id = Guid.NewGuid(),
                BusinessPartnerId = dto.BusinessPartnerId,
                UserId = user.Id,
                Role = dto.Role,
                IsActive = true,
                GrantedAt = DateTime.UtcNow,
                GrantedById = currentUserId,
                Notes = dto.Notes,
                TenantId = tenantId
            };

            await _repository.CreateAsync(businessPartnerUser);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Created business partner user link for user {UserId} and business partner {BusinessPartnerId}", 
                user.Id, dto.BusinessPartnerId);

            // Reload to get navigation properties
            var created = await _repository.GetByIdAsync(businessPartnerUser.Id);
            return MapToDto(created!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating business partner user");
            throw;
        }
    }

    public async Task<BusinessPartnerUserDto> UpdateAsync(Guid id, UpdateBusinessPartnerUserDto dto)
    {
        var businessPartnerUser = await _repository.GetByIdAsync(id);
        if (businessPartnerUser == null)
        {
            throw new InvalidOperationException("Business partner user not found");
        }

        // Update business partner user link
        businessPartnerUser.Role = dto.Role;
        businessPartnerUser.IsActive = dto.IsActive;
        businessPartnerUser.Notes = dto.Notes;

        // Update the ApplicationUser details
        if (businessPartnerUser.User != null)
        {
            businessPartnerUser.User.FirstName = dto.FirstName;
            businessPartnerUser.User.LastName = dto.LastName;
            businessPartnerUser.User.PhoneNumber = dto.PhoneNumber;

            await _userService.UpdateUserAsync(businessPartnerUser.User);
        }

        await _repository.UpdateAsync(businessPartnerUser);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Updated business partner user {Id}", id);

        var updated = await _repository.GetByIdAsync(id);
        return MapToDto(updated!);
    }

    public async Task ActivateAsync(Guid id)
    {
        var businessPartnerUser = await _repository.GetByIdAsync(id);
        if (businessPartnerUser == null)
        {
            throw new InvalidOperationException("Business partner user not found");
        }

        businessPartnerUser.IsActive = true;
        await _repository.UpdateAsync(businessPartnerUser);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Activated business partner user {Id}", id);
    }

    public async Task DeactivateAsync(Guid id)
    {
        var businessPartnerUser = await _repository.GetByIdAsync(id);
        if (businessPartnerUser == null)
        {
            throw new InvalidOperationException("Business partner user not found");
        }

        businessPartnerUser.IsActive = false;
        await _repository.UpdateAsync(businessPartnerUser);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Deactivated business partner user {Id}", id);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Deleted business partner user {Id}", id);
    }

    public async Task<bool> HasAccessAsync(Guid userId, Guid businessPartnerId)
    {
        var user = await _repository.GetByBusinessPartnerAndUserAsync(businessPartnerId, userId);
        return user != null && user.IsActive;
    }

    public async Task<string?> GetUserRoleAsync(Guid userId, Guid businessPartnerId)
    {
        var user = await _repository.GetByBusinessPartnerAndUserAsync(businessPartnerId, userId);
        return user?.Role;
    }

    public async Task ResetPasswordAsync(Guid id, string newPassword)
    {
        var businessPartnerUser = await _repository.GetByIdAsync(id);
        if (businessPartnerUser == null)
        {
            throw new InvalidOperationException("Business partner user not found");
        }

        if (businessPartnerUser.User == null)
        {
            throw new InvalidOperationException("User account not found");
        }

        // Prevent resetting password for LDAP users
        if (businessPartnerUser.User.AuthenticationProvider == ErpSystem.Shared.AuthenticationProvider.LDAP)
        {
            throw new InvalidOperationException("Cannot reset password for LDAP users");
        }

        // Remove old password and set new one
        var removeResult = await _userManager.RemovePasswordAsync(businessPartnerUser.User);
        if (!removeResult.Succeeded)
        {
            var errors = string.Join(", ", removeResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to remove old password: {errors}");
        }

        var addResult = await _userManager.AddPasswordAsync(businessPartnerUser.User, newPassword);
        if (!addResult.Succeeded)
        {
            var errors = string.Join(", ", addResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to set new password: {errors}");
        }

        _logger.LogInformation("Password reset for business partner user {Id}", id);
    }

    private static BusinessPartnerUserDto MapToDto(BusinessPartnerUser entity)
    {
        var firstName = entity.User?.FirstName ?? string.Empty;
        var lastName = entity.User?.LastName ?? string.Empty;
        var fullName = string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName)
            ? string.Empty
            : $"{firstName} {lastName}".Trim();

        return new BusinessPartnerUserDto
        {
            Id = entity.Id,
            BusinessPartnerId = entity.BusinessPartnerId,
            BusinessPartnerName = entity.BusinessPartner?.PartnerName ?? string.Empty,
            UserId = entity.UserId,
            UserName = entity.User?.UserName ?? string.Empty,
            UserEmail = entity.User?.Email ?? string.Empty,
            UserFullName = fullName,
            FirstName = firstName,
            LastName = lastName,
            Email = entity.User?.Email ?? string.Empty,
            PhoneNumber = entity.User?.PhoneNumber ?? string.Empty,
            Role = entity.Role,
            IsActive = entity.IsActive,
            GrantedAt = entity.GrantedAt,
            GrantedById = entity.GrantedById,
            GrantedByName = entity.GrantedBy?.FullName,
            Notes = entity.Notes
        };
    }
}
