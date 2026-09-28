using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Projects;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services;

/// <summary>Namespaced test master data only; no projects or approved BOQ records.</summary>
public sealed class QsUatCatalogueSeeder(ApplicationDbContext db, ICurrentUserService actor, IProjectSetupService owner,
    ILocationStructureService locationStructures, ILocationLevelService locationLevels, ILocationService locations)
{
    public async Task<QsUatCatalogueResult> PrepareAsync(CancellationToken token = default)
    {
        if (actor.UserName != "qs.uat.bootstrap" || !actor.IsAuthenticated || actor.TenantId is not { } tenantId ||
            !Guid.TryParse(actor.UserId, out var actorId) || !actor.IsInRole("SuperAdmin"))
            throw new InvalidOperationException("QS UAT catalogue preparation requires the guarded CLI bootstrap context.");
        var locationId = await PrepareLocationAsync(tenantId, token);
        var type = await db.ProjectTypes.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(p => p.TenantId == tenantId && p.Code == "QS-UAT-CONSTRUCTION", token);
        if (type is not null && (type.IsDeleted || !type.IsActive || type.Description != QsUatPreparationSeeder.SeedMarker ||
            !type.RequiresApproval || !type.RequiresSponsor || type.CreatedById != actorId))
            throw new InvalidOperationException("QS-UAT-CONSTRUCTION conflicts with existing project-type configuration.");
        var projectTypeId = type?.Id ?? (await owner.CreateProjectTypeAsync(new CreateProjectTypeDto
        {
            Code = "QS-UAT-CONSTRUCTION", Name = "QS UAT Construction Development",
            Description = QsUatPreparationSeeder.SeedMarker, IsActive = true, RequiresApproval = true, RequiresSponsor = true
        })).Id;

        var unit = await db.UnitsOfMeasure.AsNoTracking()
            .SingleOrDefaultAsync(u => u.TenantId == tenantId && !u.IsDeleted && u.IsActive && u.Code == "EA", token)
            ?? throw new InvalidOperationException("The operational Inventory baseline must provide active EA before QS catalogue preparation.");
        var ids = new Dictionary<string, Guid>();
        foreach (var (catalogue, code, name) in new[]
        {
            (ProjectCatalogDefaults.QuantitySurveySections, "QS-UAT-PRELIM", "QS UAT preliminaries"),
            (ProjectCatalogDefaults.QuantitySurveyTrades, "QS-UAT-GENERAL", "QS UAT general building work"),
            (ProjectCatalogDefaults.QuantitySurveyCostCodes, "QS-UAT-001", "QS UAT controlled Works item"),
            (ProjectCatalogDefaults.QuantitySurveyMeasurementCodes, "QS-UAT-COUNT", "QS UAT measured count")
        })
        {
            var existing = await db.ProjectCatalogEntries.IgnoreQueryFilters().AsNoTracking()
                .SingleOrDefaultAsync(c => c.TenantId == tenantId && c.CatalogType == catalogue && c.Code == code, token);
            if (existing is not null && (existing.IsDeleted || !existing.IsActive ||
                existing.Description != QsUatPreparationSeeder.SeedMarker || existing.CreatedById != actorId ||
                !string.Equals(existing.StandardCode, "cesmm4", StringComparison.OrdinalIgnoreCase) || existing.DefaultUnitOfMeasure != unit.Code))
                throw new InvalidOperationException($"Catalogue entry '{code}' conflicts with existing user configuration.");
            var id = existing?.Id ?? (await owner.CreateQuantitySurveyCatalogEntryAsync(new CreateProjectCatalogEntryDto
            {
                CatalogType = catalogue, Code = code, Name = name, Description = QsUatPreparationSeeder.SeedMarker,
                StandardCode = "cesmm4", DefaultUnitOfMeasure = unit.Code, IsActive = true,
                MeasurementRule = "UAT demonstration: record the actual whole quantity counted; no unmeasured or assumed work.",
                EffectiveFrom = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc), SortOrder = 900
            })).Id;
            ids.Add(code, id);
        }
        return new(projectTypeId, unit.Id, locationId, ids,
            "Rate item/value proposals must be authored through the rate library after effective QS configuration is independently approved; no market rate or approved business rate was invented.");
    }

    private async Task<Guid> PrepareLocationAsync(Guid tenantId, CancellationToken token)
    {
        const string structureCode = "QS-UAT-LOC";
        const string siteCode = "QS-UAT-SITE";
        const string marker = "QS-UAT-PREPARATION-V1: fictional UAT rate location; not a real geographic or attendance site";
        var structure = await db.LocationStructures.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(v => v.TenantId == tenantId && v.Code == structureCode, token);
        if (structure is not null && (structure.IsDeleted || !structure.IsActive || structure.IsDefault || structure.Description != marker))
            throw new InvalidOperationException("QS UAT location structure conflicts with existing configuration.");
        var structureId = structure?.Id ?? (await locationStructures.CreateAsync(new CreateLocationStructureDto
        { Code = structureCode, Name = "QS UAT fictional locations", Description = marker, IsActive = true, IsDefault = false }, token)).Id;
        var level = await db.LocationLevels.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(v => v.TenantId == tenantId && v.StructureId == structureId && v.Code == siteCode, token);
        if (level is not null && (level.IsDeleted || !level.IsActive || level.Description != marker ||
            level.LevelNumber != 1 || level.AllowsEmployeeAssignment || level.RequiresAddress || level.RequiresContactInfo))
            throw new InvalidOperationException("QS UAT location level conflicts with existing configuration.");
        var levelId = level?.Id ?? (await locationLevels.CreateAsync(new CreateLocationLevelDto
        {
            StructureId = structureId, Code = siteCode, Name = "QS UAT rate site", Description = marker,
            LevelNumber = 1, IsActive = true, AllowsEmployeeAssignment = false, RequiresAddress = false, RequiresContactInfo = false
        }, token)).Id;
        var site = await db.Locations.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(v => v.TenantId == tenantId && v.Code == siteCode, token);
        if (site is not null && (site.IsDeleted || !site.IsActive || site.Description != marker || site.StructureId != structureId ||
            site.LocationLevelId != levelId || site.ParentLocationId.HasValue || site.GeofenceZoneId.HasValue || site.GeoAreaId.HasValue))
            throw new InvalidOperationException("QS UAT rate site conflicts with existing configuration.");
        return site?.Id ?? (await locations.CreateAsync(new CreateLocationDto
        {
            Code = siteCode, Name = "QS UAT fictional rate site", Description = marker,
            StructureId = structureId, LocationLevelId = levelId, IsActive = true
        }, token)).Id;
    }
}

public sealed record QsUatCatalogueResult(Guid ProjectTypeId, Guid UnitOfMeasureId, Guid LocationId,
    IReadOnlyDictionary<string, Guid> CatalogueEntries, string RatePreparationBoundary);
