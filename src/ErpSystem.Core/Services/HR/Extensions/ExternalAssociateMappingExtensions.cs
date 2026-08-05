using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Application.HR.Extensions;

public static class ExternalAssociateMappingExtensions
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string BuildFullName(ExternalAssociate e)
    {
        var parts = new[] { e.Title, e.FirstName, e.MiddleName, e.LastName }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(" ", parts);
    }

    // ── Entity → DTO ─────────────────────────────────────────────────────────

    public static ExternalAssociateDto ToDto(this ExternalAssociate entity) =>
        new()
        {
            Id              = entity.Id,
            TenantId        = entity.TenantId,
            CreatedAt       = entity.CreatedAt,
            CreatedBy       = entity.CreatedBy,
            UpdatedAt       = entity.UpdatedAt,
            UpdatedBy       = entity.UpdatedBy,
            AssociateNumber = entity.AssociateNumber,
            Title           = entity.Title,
            FirstName       = entity.FirstName,
            MiddleName      = entity.MiddleName,
            LastName        = entity.LastName,
            Email           = entity.Email,
            PhoneNumber     = entity.PhoneNumber,
            CompanyName     = entity.CompanyName,
            Role            = entity.Role,
            PicturePath     = entity.PicturePath,
            HasFixedModule  = entity.HasFixedModule,
            ModuleId        = entity.ModuleId,
            IsActive        = entity.IsActive,
            DateAdded       = entity.DateAdded,
        };

    public static ExternalAssociateSummaryDto ToSummaryDto(this ExternalAssociate entity) =>
        new()
        {
            Id              = entity.Id,
            AssociateNumber = entity.AssociateNumber,
            Title           = entity.Title,
            FirstName       = entity.FirstName,
            LastName        = entity.LastName,
            Email           = entity.Email,
            PhoneNumber     = entity.PhoneNumber,
            CompanyName     = entity.CompanyName,
            Role            = entity.Role,
            IsActive        = entity.IsActive,
        };

    public static ExternalAssociateSearchResultDto ToSearchResultDto(this ExternalAssociate entity) =>
        new()
        {
            Id              = entity.Id,
            AssociateNumber = entity.AssociateNumber,
            FullName        = BuildFullName(entity),
            Email           = entity.Email,
            PhoneNumber     = entity.PhoneNumber,
            Role            = entity.Role,
            CompanyName     = entity.CompanyName,
        };

    public static IEnumerable<ExternalAssociateSummaryDto> ToSummaryDtoList(
        this IEnumerable<ExternalAssociate> entities) =>
        entities.Select(e => e.ToSummaryDto());

    public static IEnumerable<ExternalAssociateSearchResultDto> ToSearchResultDtoList(
        this IEnumerable<ExternalAssociate> entities) =>
        entities.Select(e => e.ToSearchResultDto());

    // ── Create DTO → Entity ───────────────────────────────────────────────────

    public static ExternalAssociate ToEntity(
        this CreateExternalAssociateDto dto,
        string associateNumber,
        Guid tenantId,
        Guid userId) =>
        new()
        {
            TenantId        = tenantId,
            AssociateNumber = associateNumber,
            Title           = dto.Title?.Trim(),
            FirstName       = dto.FirstName.Trim(),
            MiddleName      = dto.MiddleName?.Trim() ?? string.Empty,
            LastName        = dto.LastName.Trim(),
            Email           = dto.Email.Trim().ToLowerInvariant(),
            PhoneNumber     = dto.PhoneNumber.Trim(),
            CompanyName     = dto.CompanyName?.Trim(),
            Role            = dto.Role?.Trim(),
            PicturePath     = dto.PicturePath?.Trim() ?? string.Empty,
            HasFixedModule  = dto.HasFixedModule,
            ModuleId        = dto.ModuleId,
            IsActive        = dto.IsActive,
            DateAdded       = DateTime.UtcNow,
            CreatedBy       = userId.ToString(),
        };

    // ── Update DTO → Entity ───────────────────────────────────────────────────

    public static void ApplyUpdate(this ExternalAssociate entity, UpdateExternalAssociateDto dto, Guid userId)
    {
        entity.Title          = dto.Title?.Trim();
        entity.FirstName      = dto.FirstName.Trim();
        entity.MiddleName     = dto.MiddleName?.Trim() ?? string.Empty;
        entity.LastName       = dto.LastName.Trim();
        entity.Email          = dto.Email.Trim().ToLowerInvariant();
        entity.PhoneNumber    = dto.PhoneNumber.Trim();
        entity.CompanyName    = dto.CompanyName?.Trim();
        entity.Role           = dto.Role?.Trim();
        entity.PicturePath    = dto.PicturePath?.Trim() ?? entity.PicturePath;
        entity.HasFixedModule = dto.HasFixedModule;
        entity.ModuleId       = dto.ModuleId;
        entity.IsActive       = dto.IsActive;
        entity.UpdatedAt      = DateTime.UtcNow;
        entity.UpdatedBy      = userId.ToString();
    }
}
