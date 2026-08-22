using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Services.HR.Extensions;

/// <summary>
/// Where a collective agreement stands today, named once.
/// </summary>
/// <remarks>
/// <para>Added in areas 19-23 slice 6, because <c>IsActive</c> answers a different question from the
/// one every screen asks. It is a flag somebody sets and nothing ever clears; the probe found an
/// agreement that ran 2019-2021 still reading <c>isActive: true</c>. "Is this agreement in force"
/// depends on two dates as well, and if the payload does not answer it then the register, the union
/// detail and the job-description screen each answer it themselves — differently, eventually.</para>
///
/// <para>Order matters and is deliberate: a switched-off agreement is <c>Inactive</c> whatever its
/// dates say, because that is an explicit act by a person and outranks the calendar.</para>
/// </remarks>
public static class CollectiveBargainingAgreementStatuses
{
    /// <summary>Switched off by hand. Outranks the dates.</summary>
    public const string Inactive = "Inactive";

    /// <summary>Signed, but its effective date has not arrived.</summary>
    public const string Pending = "Pending";

    /// <summary>In force today.</summary>
    public const string Active = "Active";

    /// <summary>Its expiry date has passed.</summary>
    public const string Expired = "Expired";

    public static readonly IReadOnlyList<string> All = new[] { Inactive, Pending, Active, Expired };

    /// <summary>
    /// Classifies against a caller-supplied "today", so a screen, a report and a test can all ask
    /// the same question about the same day rather than about whenever each of them ran.
    /// </summary>
    public static string Classify(CollectiveBargainingAgreement entity, DateTime asOf)
    {
        if (!entity.IsActive)
            return Inactive;
        if (entity.EffectiveDate.Date > asOf.Date)
            return Pending;
        if (entity.ExpiryDate.HasValue && entity.ExpiryDate.Value.Date < asOf.Date)
            return Expired;
        return Active;
    }

    public static bool IsInForce(CollectiveBargainingAgreement entity, DateTime asOf)
        => Classify(entity, asOf) == Active;
}

public static class UnionMappingExtensions
{
    #region Union

    public static UnionDto ToDto(this Union entity)
    {
        return new UnionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Code = entity.Code,
            Name = entity.Name,
            Description = entity.Description,
            ContactPerson = entity.ContactPerson,
            ContactEmail = entity.ContactEmail,
            ContactPhone = entity.ContactPhone,
            IsActive = entity.IsActive,
            AgreementCount = entity.Agreements?.Count ?? 0,
            // Same classifier the agreements themselves are mapped through, so the union's headline
            // number and the rows beneath it cannot disagree.
            InForceAgreementCount = entity.Agreements?.Count(a =>
                CollectiveBargainingAgreementStatuses.IsInForce(a, DateTime.UtcNow)) ?? 0,
            Agreements = entity.Agreements?
                .OrderByDescending(a => a.EffectiveDate)
                .Select(a => a.ToDto())
                .ToList() ?? new List<CollectiveBargainingAgreementDto>(),
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static Union ToEntity(this CreateUnionDto dto)
    {
        return new Union
        {
            Code = dto.Code,
            Name = dto.Name,
            Description = dto.Description,
            ContactPerson = dto.ContactPerson,
            ContactEmail = dto.ContactEmail,
            ContactPhone = dto.ContactPhone,
            IsActive = dto.IsActive
        };
    }

    public static void UpdateEntity(this UpdateUnionDto dto, Union entity)
    {
        entity.Code = dto.Code;
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.ContactPerson = dto.ContactPerson;
        entity.ContactEmail = dto.ContactEmail;
        entity.ContactPhone = dto.ContactPhone;
        entity.IsActive = dto.IsActive;
    }

    public static List<UnionDto> ToDtoList(this IEnumerable<Union> entities)
        => entities.Select(e => e.ToDto()).ToList();

    #endregion

    #region CollectiveBargainingAgreement

    public static CollectiveBargainingAgreementDto ToDto(this CollectiveBargainingAgreement entity)
    {
        return new CollectiveBargainingAgreementDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            UnionId = entity.UnionId,
            UnionName = entity.Union?.Name,
            ReferenceNumber = entity.ReferenceNumber,
            Title = entity.Title,
            EffectiveDate = entity.EffectiveDate,
            ExpiryDate = entity.ExpiryDate,
            Summary = entity.Summary,
            DocumentReference = entity.DocumentReference,
            IsActive = entity.IsActive,
            Status = CollectiveBargainingAgreementStatuses.Classify(entity, DateTime.UtcNow),
            IsInForce = CollectiveBargainingAgreementStatuses.IsInForce(entity, DateTime.UtcNow),
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static CollectiveBargainingAgreement ToEntity(this CreateCollectiveBargainingAgreementDto dto)
    {
        return new CollectiveBargainingAgreement
        {
            UnionId = dto.UnionId,
            ReferenceNumber = dto.ReferenceNumber,
            Title = dto.Title,
            EffectiveDate = dto.EffectiveDate,
            ExpiryDate = dto.ExpiryDate,
            Summary = dto.Summary,
            DocumentReference = dto.DocumentReference,
            IsActive = dto.IsActive
        };
    }

    public static void UpdateEntity(this UpdateCollectiveBargainingAgreementDto dto, CollectiveBargainingAgreement entity)
    {
        entity.ReferenceNumber = dto.ReferenceNumber;
        entity.Title = dto.Title;
        entity.EffectiveDate = dto.EffectiveDate;
        entity.ExpiryDate = dto.ExpiryDate;
        entity.Summary = dto.Summary;
        entity.DocumentReference = dto.DocumentReference;
        entity.IsActive = dto.IsActive;
    }

    public static List<CollectiveBargainingAgreementDto> ToDtoList(this IEnumerable<CollectiveBargainingAgreement> entities)
        => entities.OrderByDescending(e => e.EffectiveDate).Select(e => e.ToDto()).ToList();

    #endregion
}
