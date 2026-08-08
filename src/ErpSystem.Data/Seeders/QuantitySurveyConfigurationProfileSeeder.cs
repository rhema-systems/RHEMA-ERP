using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

public sealed class QuantitySurveyConfigurationProfileSeeder(ApplicationDbContext context, ILogger<QuantitySurveyConfigurationProfileSeeder> logger)
{
    public async Task<int> SeedAsync(CancellationToken token = default)
    {
        var tenantIds = await context.Tenants.AsNoTracking().Where(x => !x.IsDeleted).Select(x => x.Id).ToListAsync(token); var count = 0;
        foreach (var tenantId in tenantIds) if (await SeedTenantAsync(tenantId, null, token)) count++;
        return count;
    }

    public async Task<bool> SeedTenantAsync(Guid tenantId, Guid? actorUserId = null, CancellationToken token = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        // A soft-deleted family is still historical configuration data. Treat the
        // tenant as initialized so startup seeding never reuses version 1 against
        // the unfiltered unique index or silently reverses an administrator delete.
        if (await context.QuantitySurveyConfigurationProfiles.IgnoreQueryFilters().AnyAsync(x => x.TenantId == tenantId && x.ProfileCode == QuantitySurveyConfigurationService.ProfileCode, token)) return false;
        var now = DateTime.UtcNow; var profile = new QuantitySurveyConfigurationProfile { Id = Guid.NewGuid(), TenantId = tenantId, ProfileKey = Guid.NewGuid(), ProfileCode = QuantitySurveyConfigurationService.ProfileCode, Name = "TDC Quantity Survey Configuration", Version = 1, LifecycleStatus = QuantitySurveyConfigurationProfileStatus.Draft, EffectiveFrom = now.Date, ChangeSummary = "Initial tenant draft and decision register. Values require controlled selection, central-DMS evidence and independent approval before publication.", IsDefault = true, CreatedAt = now, CreatedBy = "System", CreatedById = actorUserId };
        context.QuantitySurveyConfigurationProfiles.Add(profile);
        foreach (var definition in QuantitySurveyConfigurationDecisionRegistry.Definitions) context.QuantitySurveyConfigurationDecisions.Add(new QuantitySurveyConfigurationDecision { Id = Guid.NewGuid(), TenantId = tenantId, ProfileId = profile.Id, DecisionKey = definition.DecisionKey, SchemaVersion = 1, OwnerGroup = definition.OwnerGroup, Status = QuantitySurveyConfigurationDecisionStatus.Draft, ApprovalStatus = QuantitySurveyConfigurationApprovalStatus.Pending, EvidenceStatus = QuantitySurveyConfigurationEvidenceStatus.Missing, SourceLineage = $"{definition.ConfigurationKey} tenant initializer; unapproved draft", CreatedAt = now, CreatedBy = "System", CreatedById = actorUserId });
        context.QuantitySurveyConfigurationRevisions.Add(new QuantitySurveyConfigurationRevision { Id = Guid.NewGuid(), TenantId = tenantId, ProfileId = profile.Id, Action = QuantitySurveyAuditEventMap.SeedDraft, Result = "Succeeded", CorrelationId = $"qs-seed-{tenantId:N}", ActorUserId = actorUserId ?? Guid.Empty, ActorName = "System", ActorRoles = "System", Reason = "Idempotent QS-0001/QS-0002 tenant seed plan.", AfterJson = "{\"profileCode\":\"TDC-QUANTITY-SURVEY\",\"version\":1,\"lifecycleStatus\":\"draft\",\"decisionCount\":17}", CreatedAt = now, CreatedBy = "System", CreatedById = actorUserId });
        try { await context.SaveChangesAsync(token); logger.LogInformation("Seeded draft QS configuration profile for tenant {TenantId}", tenantId); return true; }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true || ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true) { context.ChangeTracker.Clear(); return false; }
    }
}
