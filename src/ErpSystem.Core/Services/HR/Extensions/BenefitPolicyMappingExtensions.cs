using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Services.HR.Extensions;

/// <summary>
/// Explicit mapping extensions for the Benefit Policy module.
/// These mappings convert between DTOs and domain entities without using AutoMapper,
/// and are designed to be safe, readable, and unit-test friendly.
/// </summary>
public static class BenefitPolicyMappingExtensions
{
    /// <summary>
    /// Maps a create DTO to a new <see cref="BenefitPolicy"/> entity instance.
    /// Note: multi-tenant fields such as TenantId must be set by the caller/service layer.
    /// </summary>
    /// <param name="dto">The create DTO.</param>
    /// <returns>A new entity populated with scalar fields and initialized child collections.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dto"/> is null.</exception>
    public static BenefitPolicy ToEntity(this CreateBenefitPolicyDto dto)
    {
        if (dto is null) throw new ArgumentNullException(nameof(dto));

        var entity = new BenefitPolicy
        {
            PolicyType = dto.PolicyType,
            PolicyName = dto.PolicyName,
            PolicyCode = string.IsNullOrWhiteSpace(dto.PolicyCode) ? null : dto.PolicyCode.Trim(),
            Description = dto.Description,
            Recipient = dto.Recipient,
            MaxDependents = dto.MaxDependents,
            EmployeeContribution = dto.EmployeeContribution,
            EmployerContribution = dto.EmployerContribution,
            CoverageLimit = dto.CoverageLimit,
            LimitPeriod = dto.LimitPeriod,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            IsMandatory = dto.IsMandatory,
            IsActive = dto.IsActive,
            BenefitPolicyRelations = new List<BenefitPolicyRelation>(),
            PositionBenefits = new List<EmployeePositionBenefit>(),
            GradeValues = new List<BenefitGradeValue>()
        };

        ApplyDefinition(dto.Definition, entity);

        if (dto.Relations is { Count: > 0 })
        {
            foreach (var relationDto in dto.Relations)
            {
                if (relationDto is null) continue;
                entity.BenefitPolicyRelations.Add(relationDto.ToEntity());
            }
        }

        if (dto.GradeValues is { Count: > 0 })
        {
            foreach (var gradeDto in dto.GradeValues)
            {
                if (gradeDto is null) continue;
                entity.GradeValues.Add(gradeDto.ToEntity());
            }
        }

        return entity;
    }

    /// <summary>Copies the enterprise benefit-definition fields from a DTO bundle onto an entity.</summary>
    private static void ApplyDefinition(BenefitDefinitionFields def, BenefitPolicy entity)
    {
        if (def is null) return;

        entity.DeliveryType = def.DeliveryType;
        entity.Currency = string.IsNullOrWhiteSpace(def.Currency) ? "GHS" : def.Currency.Trim().ToUpperInvariant();
        entity.Frequency = def.Frequency;
        entity.CalculationBasis = def.CalculationBasis;
        entity.IsTaxable = def.IsTaxable;
        entity.TaxTreatment = def.TaxTreatment;
        entity.TaxablePercentage = def.TaxablePercentage;
        entity.ValuationMethod = def.ValuationMethod;
        entity.FlatValue = def.FlatValue;
        entity.ValuationRate = def.ValuationRate;
        entity.ValuationCap = def.ValuationCap;
        entity.TaxExemptThreshold = def.TaxExemptThreshold;
        entity.IsPensionable = def.IsPensionable;
        entity.AffectsGrossPay = def.AffectsGrossPay;
        entity.AffectsNetPay = def.AffectsNetPay;
        entity.ContributionResponsibility = def.ContributionResponsibility;
        entity.EmployerContributionRate = def.EmployerContributionRate;
        entity.EmployeeContributionRate = def.EmployeeContributionRate;
        entity.MinServiceMonths = def.MinServiceMonths;
        entity.AvailableDuringProbation = def.AvailableDuringProbation;
        entity.PayComponentId = def.PayComponentId;
    }

    /// <summary>Maps a create grade-value DTO to a new entity (TenantId/PolicyId set by caller).</summary>
    public static BenefitGradeValue ToEntity(this CreateBenefitGradeValueDto dto)
    {
        if (dto is null) throw new ArgumentNullException(nameof(dto));

        return new BenefitGradeValue
        {
            SalaryGradeId = dto.SalaryGradeId,
            StaffLevelId = dto.StaffLevelId,
            Amount = dto.Amount,
            Rate = dto.Rate,
            CoverageLimit = dto.CoverageLimit,
            IsActive = dto.IsActive
        };
    }

    /// <summary>Maps a grade-value entity to its read DTO.</summary>
    public static BenefitGradeValueDto ToReadDto(this BenefitGradeValue entity)
    {
        if (entity is null) throw new ArgumentNullException(nameof(entity));

        return new BenefitGradeValueDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            SalaryGradeId = entity.SalaryGradeId,
            StaffLevelId = entity.StaffLevelId,
            Amount = entity.Amount,
            Rate = entity.Rate,
            CoverageLimit = entity.CoverageLimit,
            IsActive = entity.IsActive
        };
    }

    /// <summary>Builds the definition-fields DTO bundle from an entity.</summary>
    private static BenefitDefinitionFields ToDefinition(this BenefitPolicy entity) => new()
    {
        DeliveryType = entity.DeliveryType,
        Currency = entity.Currency,
        Frequency = entity.Frequency,
        CalculationBasis = entity.CalculationBasis,
        IsTaxable = entity.IsTaxable,
        TaxTreatment = entity.TaxTreatment,
        TaxablePercentage = entity.TaxablePercentage,
        ValuationMethod = entity.ValuationMethod,
        FlatValue = entity.FlatValue,
        ValuationRate = entity.ValuationRate,
        ValuationCap = entity.ValuationCap,
        TaxExemptThreshold = entity.TaxExemptThreshold,
        IsPensionable = entity.IsPensionable,
        AffectsGrossPay = entity.AffectsGrossPay,
        AffectsNetPay = entity.AffectsNetPay,
        ContributionResponsibility = entity.ContributionResponsibility,
        EmployerContributionRate = entity.EmployerContributionRate,
        EmployeeContributionRate = entity.EmployeeContributionRate,
        MinServiceMonths = entity.MinServiceMonths,
        AvailableDuringProbation = entity.AvailableDuringProbation,
        PayComponentId = entity.PayComponentId
    };

    /// <summary>
    /// Updates an existing <see cref="BenefitPolicy"/> entity from an update DTO.
    /// This method updates only mutable business fields and safely synchronizes child relations.
    /// </summary>
    /// <param name="dto">The update DTO.</param>
    /// <param name="entity">The entity instance to update.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dto"/> or <paramref name="entity"/> is null.</exception>
    public static void UpdateEntity(this UpdateBenefitPolicyDto dto, BenefitPolicy entity)
    {
        if (dto is null) throw new ArgumentNullException(nameof(dto));
        if (entity is null) throw new ArgumentNullException(nameof(entity));

        // Scalar fields
        entity.PolicyType = dto.PolicyType;
        entity.PolicyName = dto.PolicyName;
        entity.PolicyCode = string.IsNullOrWhiteSpace(dto.PolicyCode) ? null : dto.PolicyCode.Trim();
        entity.Description = dto.Description;
        entity.Recipient = dto.Recipient;
        entity.MaxDependents = dto.MaxDependents;
        entity.EmployeeContribution = dto.EmployeeContribution;
        entity.EmployerContribution = dto.EmployerContribution;
        entity.CoverageLimit = dto.CoverageLimit;
        entity.LimitPeriod = dto.LimitPeriod;
        entity.EffectiveFrom = dto.EffectiveFrom;
        entity.EffectiveTo = dto.EffectiveTo;
        entity.IsMandatory = dto.IsMandatory;
        entity.IsActive = dto.IsActive;

        ApplyDefinition(dto.Definition, entity);

        // Child collection sync: BenefitPolicyRelations
        SyncBenefitPolicyRelations(dto, entity);
    }

    /// <summary>
    /// Maps a <see cref="BenefitPolicy"/> entity to a full read DTO suitable for UI/API consumption.
    /// </summary>
    /// <param name="entity">The entity instance.</param>
    /// <returns>A read DTO containing all business fields and child relation DTOs.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity"/> is null.</exception>
    public static BenefitPolicyDto ToReadDto(this BenefitPolicy entity)
    {
        if (entity is null) throw new ArgumentNullException(nameof(entity));

        return new BenefitPolicyDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,

            PolicyType = entity.PolicyType,
            PolicyName = entity.PolicyName,
            PolicyCode = entity.PolicyCode,
            Description = entity.Description,
            Recipient = entity.Recipient,
            MaxDependents = entity.MaxDependents,
            EmployeeContribution = entity.EmployeeContribution,
            EmployerContribution = entity.EmployerContribution,
            CoverageLimit = entity.CoverageLimit,
            LimitPeriod = entity.LimitPeriod,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo,
            IsMandatory = entity.IsMandatory,
            IsActive = entity.IsActive,

            Definition = entity.ToDefinition(),

            Relations = (entity.BenefitPolicyRelations ?? new List<BenefitPolicyRelation>())
                .Where(r => r is not null)
                .Select(r => r.ToReadDto())
                .ToList(),

            GradeValues = (entity.GradeValues ?? new List<BenefitGradeValue>())
                .Where(g => g is not null)
                .Select(g => g.ToReadDto())
                .ToList()
        };
    }

    /// <summary>
    /// Maps a <see cref="BenefitPolicy"/> entity to a lightweight list DTO suitable for tables/grids.
    /// </summary>
    /// <param name="entity">The entity instance.</param>
    /// <returns>A list DTO with minimal fields and no child collections.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity"/> is null.</exception>
    public static BenefitPolicyListDto ToListDto(this BenefitPolicy entity)
    {
        if (entity is null) throw new ArgumentNullException(nameof(entity));

        return new BenefitPolicyListDto
        {
            Id = entity.Id,
            PolicyName = entity.PolicyName,
            PolicyCode = entity.PolicyCode,
            PolicyType = entity.PolicyType,
            Recipient = entity.Recipient,
            IsActive = entity.IsActive,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo
        };
    }

    /// <summary>
    /// Maps a sequence of <see cref="BenefitPolicy"/> entities to list DTOs.
    /// </summary>
    /// <param name="entities">The entities to map.</param>
    /// <returns>A list of <see cref="BenefitPolicyListDto"/> items.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entities"/> is null.</exception>
    public static List<BenefitPolicyListDto> ToListDtoList(this IEnumerable<BenefitPolicy> entities)
    {
        if (entities is null) throw new ArgumentNullException(nameof(entities));
        return entities.Select(e => e.ToListDto()).ToList();
    }

    /// <summary>
    /// Maps a create relation DTO to a new <see cref="BenefitPolicyRelation"/> entity instance.
    /// Note: TenantId and BenefitPolicyId should be set by the caller/service layer when attaching to a policy.
    /// </summary>
    /// <param name="dto">The create relation DTO.</param>
    /// <returns>A new relation entity populated with scalar fields.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dto"/> is null.</exception>
    public static BenefitPolicyRelation ToEntity(this CreateBenefitPolicyRelationDto dto)
    {
        if (dto is null) throw new ArgumentNullException(nameof(dto));

        return new BenefitPolicyRelation
        {
            RelationType = dto.RelationType,
            MinAge = dto.MinAge,
            MaxAge = dto.MaxAge,
            IsActive = dto.IsActive
        };
    }

    /// <summary>
    /// Maps a <see cref="BenefitPolicyRelation"/> entity to a read DTO.
    /// </summary>
    /// <param name="entity">The entity instance.</param>
    /// <returns>A read DTO.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entity"/> is null.</exception>
    public static BenefitPolicyRelationDto ToReadDto(this BenefitPolicyRelation entity)
    {
        if (entity is null) throw new ArgumentNullException(nameof(entity));

        return new BenefitPolicyRelationDto
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,

            RelationType = entity.RelationType,
            MinAge = entity.MinAge,
            MaxAge = entity.MaxAge,
            IsActive = entity.IsActive
        };
    }

    private static void SyncBenefitPolicyRelations(UpdateBenefitPolicyDto dto, BenefitPolicy entity)
    {
        entity.BenefitPolicyRelations ??= new List<BenefitPolicyRelation>();

        var incoming = dto.Relations ?? new List<UpdateBenefitPolicyRelationDto>();

        // Index existing relations by Id for efficient lookup.
        var existingById = entity.BenefitPolicyRelations
            .Where(r => r is not null && r.Id != Guid.Empty)
            .ToDictionary(r => r.Id, r => r);

        var keepIds = new HashSet<Guid>();

        foreach (var relationDto in incoming)
        {
            if (relationDto is null) continue;

            if (relationDto.Id.HasValue && relationDto.Id.Value != Guid.Empty &&
                existingById.TryGetValue(relationDto.Id.Value, out var existingEntity))
            {
                // Update existing relation (never mutate TenantId/CreatedAt)
                existingEntity.RelationType = relationDto.RelationType;
                existingEntity.MinAge = relationDto.MinAge;
                existingEntity.MaxAge = relationDto.MaxAge;
                existingEntity.IsActive = relationDto.IsActive;

                keepIds.Add(existingEntity.Id);
                continue;
            }

            // Add new relation
            var newRelation = new BenefitPolicyRelation
            {
                RelationType = relationDto.RelationType,
                MinAge = relationDto.MinAge,
                MaxAge = relationDto.MaxAge,
                IsActive = relationDto.IsActive,

                // Tenant boundaries: new children inherit the parent's tenant
                TenantId = entity.TenantId
            };

            entity.BenefitPolicyRelations.Add(newRelation);
        }

        // Remove relations not present in incoming list.
        // Note: only remove persisted relations (Id != Guid.Empty).
        entity.BenefitPolicyRelations.RemoveAll(r => r is not null && r.Id != Guid.Empty && !keepIds.Contains(r.Id));
    }
}
