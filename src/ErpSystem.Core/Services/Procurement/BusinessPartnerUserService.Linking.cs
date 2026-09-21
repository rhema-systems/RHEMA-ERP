using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Services.Procurement;

public partial class BusinessPartnerUserService
{
    // Link an already provisioned portal identity without accepting, logging or changing credentials.
    public async Task<BusinessPartnerUserDto> LinkExistingExternalUserAsync(Guid businessPartnerId, Guid userId)
    {
        var tenantId = _tenantContext.GetCurrentTenantId();
        if (!_currentUserService.IsAuthenticated || tenantId == Guid.Empty ||
            _currentUserService.TenantId != tenantId ||
            !(_currentUserService.IsInRole("SuperAdmin") || _currentUserService.IsInRole("TenantAdmin")))
            throw new UnauthorizedAccessException("Only a tenant administrator can link an existing portal account.");

        return await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            await _unitOfWork.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                await _unitOfWork.AcquireTransactionLockAsync($"partner-portal-user:{userId:N}");
                var result = await LinkExistingExternalUserCoreAsync(businessPartnerId, userId, tenantId);
                await _unitOfWork.CommitAsync();
                return result;
            }
            catch { await _unitOfWork.RollbackAsync(); _unitOfWork.ClearTrackedChanges(); throw; }
        });
    }

    private async Task<BusinessPartnerUserDto> LinkExistingExternalUserCoreAsync(Guid businessPartnerId, Guid userId, Guid tenantId)
    {

        var partner = await _unitOfWork.Repository<BusinessPartner>().FirstOrDefaultAsync(p =>
            p.Id == businessPartnerId && p.TenantId == tenantId && !p.IsDeleted);
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (partner is null || user is null || user.TenantId != tenantId || !user.IsActive)
            throw new InvalidOperationException("An active account and business partner in the current tenant are required.");
        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Count != 1 || !string.Equals(roles[0], "ExternalUser", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Select an account with only the ExternalUser role.");

        var links = (await _unitOfWork.Repository<BusinessPartnerUser>().FindAsync(p =>
            p.UserId == userId && !p.IsDeleted)).ToList();
        if (links.Any(p => p.TenantId != tenantId || p.BusinessPartnerId != businessPartnerId) ||
            await _unitOfWork.Repository<BusinessPartner>().ExistsAsync(p =>
                p.UserId == userId && !p.IsDeleted && (p.TenantId != tenantId || p.Id != businessPartnerId)))
            throw new InvalidOperationException("This account is already associated with another business partner.");
        var existing = links.SingleOrDefault();
        if (existing is not null)
        {
            if (!existing.IsActive)
                throw new InvalidOperationException("The existing partner access is inactive; review it before reactivation.");
            return MapToDto((await _repository.GetByIdAsync(existing.Id))!);
        }

        var link = new BusinessPartnerUser
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BusinessPartnerId = businessPartnerId,
            UserId = userId, Role = "User", IsActive = true, GrantedAt = DateTime.UtcNow,
            GrantedById = Guid.Parse(_currentUserService.UserId!),
            Notes = "Existing ExternalUser linked by tenant administrator; access restricted to this business partner."
        };
        await _repository.CreateAsync(link);
        await _unitOfWork.SaveChangesAsync();
        return MapToDto((await _repository.GetByIdAsync(link.Id))!);
    }
}
