using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Projects;
using ErpSystem.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ErpSystem.Data.Seeders;

public sealed class CivilEngineeringConfigurationProfileSeeder(
    ApplicationDbContext context,
    ILogger<CivilEngineeringConfigurationProfileSeeder> logger)
{
    public async Task<int> SeedAsync(CancellationToken token = default)
    {
        var tenantIds = await context.Tenants.AsNoTracking().Where(item => !item.IsDeleted).Select(item => item.Id).ToListAsync(token);
        var count = 0;
        foreach (var tenantId in tenantIds)
            if (await SeedTenantAsync(tenantId, null, token)) count++;
        return count;
    }

    public async Task<bool> SeedTenantAsync(Guid tenantId, Guid? actorUserId = null, CancellationToken token = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        var now = DateTime.UtcNow;
        var definitions = CivilEngineeringConfigurationDecisionRegistry.Definitions;
        var family = await context.CivilEngineeringConfigurationProfiles
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.ProfileCode == CivilEngineeringConfigurationService.ProfileCode)
            .OrderBy(item => item.Version)
            .ToListAsync(token);
        var activeProfiles = family.Where(item => !item.IsDeleted).ToList();
        var draft = activeProfiles
            .Where(item => item.LifecycleStatus == CivilEngineeringConfigurationProfileStatus.Draft)
            .OrderByDescending(item => item.Version)
            .FirstOrDefault();

        if (draft is not null)
        {
            var allDecisions = await context.CivilEngineeringConfigurationDecisions
                .IgnoreQueryFilters()
                .Where(item => item.TenantId == tenantId && item.ProfileId == draft.Id)
                .ToListAsync(token);
            var changedKeys = new List<string>();
            foreach (var definition in definitions)
            {
                var existing = allDecisions.FirstOrDefault(item =>
                    string.Equals(item.ConfigurationKey, definition.ConfigurationKey, StringComparison.OrdinalIgnoreCase));
                if (existing is null)
                {
                    context.CivilEngineeringConfigurationDecisions.Add(NewDecision(tenantId, draft, definition, now));
                    changedKeys.Add(definition.ConfigurationKey);
                    continue;
                }

                if (!existing.IsDeleted) continue;
                existing.IsDeleted = false;
                existing.DeletedAt = null;
                existing.DeletedBy = null;
                existing.SchemaVersion = 1;
                existing.OwnerGroup = definition.OwnerGroup;
                existing.Status = CivilEngineeringConfigurationDecisionStatus.Draft;
                existing.ApprovalStatus = CivilEngineeringConfigurationApprovalStatus.Pending;
                existing.EvidenceStatus = CivilEngineeringConfigurationEvidenceStatus.Missing;
                existing.ValueJson = "{}";
                existing.DecisionDate = null;
                existing.EffectiveFrom = null;
                existing.EffectiveTo = null;
                existing.ApprovedById = null;
                existing.ApprovedAt = null;
                existing.ApprovalWorkflowInstanceId = null;
                existing.ApprovalReference = null;
                existing.SourceDecisionId = null;
                existing.SourceLineage = $"{definition.ConfigurationKey} tenant catalogue reconciliation; unapproved draft";
                existing.Notes = null;
                existing.UpdatedAt = now;
                existing.UpdatedBy = "System";
                changedKeys.Add(definition.ConfigurationKey);
            }

            if (changedKeys.Count == 0) return false;
            AddSeedRevision(draft, tenantId, actorUserId, now,
                "Reconciled newly registered Civil configuration decisions into the existing draft.",
                changedKeys);
            return await SaveReconciliationAsync(tenantId, changedKeys.Count, token);
        }

        var latest = activeProfiles.OrderByDescending(item => item.Version).FirstOrDefault();
        var latestDecisions = latest is null
            ? new List<CivilEngineeringConfigurationDecision>()
            : await context.CivilEngineeringConfigurationDecisions.AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.ProfileId == latest.Id && !item.IsDeleted)
                .ToListAsync(token);
        var latestKeys = latestDecisions.Select(item => item.ConfigurationKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (latest is not null && definitions.All(item => latestKeys.Contains(item.ConfigurationKey))) return false;

        var profile = new CivilEngineeringConfigurationProfile
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProfileKey = latest?.ProfileKey ?? Guid.NewGuid(),
            ProfileCode = CivilEngineeringConfigurationService.ProfileCode,
            Name = latest?.Name ?? "TDC Civil Engineering Configuration",
            Version = CivilEngineeringConfigurationLifecyclePolicy.NextVersion(family),
            LifecycleStatus = CivilEngineeringConfigurationProfileStatus.Draft,
            EffectiveFrom = latest?.EffectiveFrom ?? now.Date,
            EffectiveTo = latest?.EffectiveTo,
            ChangeSummary = latest is null
                ? "Initial tenant draft. Values require controlled selection, central-DMS evidence and independent approval before publication."
                : "System-created draft for newly registered Civil configuration decisions. Every value requires controlled review, evidence and independent reapproval.",
            IsDefault = latest?.IsDefault ?? true,
            SupersedesProfileId = latest?.Id,
            CreatedAt = now, CreatedBy = "System", CreatedById = actorUserId
        };
        context.CivilEngineeringConfigurationProfiles.Add(profile);

        var sourceByKey = latestDecisions.ToDictionary(item => item.ConfigurationKey, StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
            context.CivilEngineeringConfigurationDecisions.Add(NewDecision(
                tenantId,
                profile,
                definition,
                now,
                sourceByKey.GetValueOrDefault(definition.ConfigurationKey)));

        AddSeedRevision(profile, tenantId, actorUserId, now,
            latest is null
                ? "Idempotent CIV-0001/CIV-0002 tenant draft seed."
                : "Created an unapproved successor draft because the latest immutable profile did not contain every registered Civil configuration decision.",
            definitions.Select(item => item.ConfigurationKey));

        return await SaveReconciliationAsync(tenantId, definitions.Count, token);
    }

    private async Task<bool> SaveReconciliationAsync(Guid tenantId, int decisionCount, CancellationToken token)
    {
        try
        {
            await context.SaveChangesAsync(token);
            logger.LogInformation(
                "Reconciled Civil Engineering configuration for tenant {TenantId} with {DecisionCount} decision catalogue change(s)",
                tenantId,
                decisionCount);
            return true;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true ||
            exception.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true)
        {
            context.ChangeTracker.Clear();
            return false;
        }
    }

    private static CivilEngineeringConfigurationDecision NewDecision(
        Guid tenantId,
        CivilEngineeringConfigurationProfile profile,
        CivilEngineeringDecisionDefinition definition,
        DateTime now,
        CivilEngineeringConfigurationDecision? source = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        ProfileId = profile.Id,
        ConfigurationKey = definition.ConfigurationKey,
        SchemaVersion = source?.SchemaVersion ?? 1,
        OwnerGroup = definition.OwnerGroup,
        Status = CivilEngineeringConfigurationDecisionStatus.Draft,
        ApprovalStatus = CivilEngineeringConfigurationApprovalStatus.Pending,
        EvidenceStatus = CivilEngineeringConfigurationEvidenceStatus.Missing,
        ValueJson = source?.ValueJson ?? "{}",
        EffectiveFrom = source?.EffectiveFrom,
        EffectiveTo = source?.EffectiveTo,
        SourceDecisionId = source?.Id,
        SourceLineage = source is null
            ? $"{definition.ConfigurationKey} tenant catalogue initializer; unapproved draft"
            : $"Cloned from {profile.ProfileCode}/v{profile.Version - 1}/{definition.ConfigurationKey}; independent reapproval required",
        CreatedAt = now,
        CreatedBy = "System"
    };

    private void AddSeedRevision(
        CivilEngineeringConfigurationProfile profile,
        Guid tenantId,
        Guid? actorUserId,
        DateTime now,
        string reason,
        IEnumerable<string> decisionKeys)
    {
        var keys = decisionKeys.OrderBy(item => item, StringComparer.Ordinal).ToArray();
        var revision = new CivilEngineeringConfigurationRevision
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProfileId = profile.Id,
            Action = CivilEngineeringAuditEventMap.SeedDraft, Result = "Succeeded",
            CorrelationId = $"civil-seed-{tenantId:N}-v{profile.Version}-{keys.Length}-{keys.LastOrDefault() ?? "none"}",
            ActorUserId = actorUserId ?? Guid.Empty,
            ActorName = "System", ActorRoles = "System", Reason = reason,
            AfterJson = JsonSerializer.Serialize(new
            {
                profileCode = profile.ProfileCode,
                version = profile.Version,
                lifecycleStatus = "draft",
                decisionCount = keys.Length,
                decisionKeys = keys
            }),
            CreatedAt = now, CreatedBy = "System", CreatedById = actorUserId
        };
        profile.UpdatedAt = now;
        profile.UpdatedBy = "System";
        context.CivilEngineeringConfigurationRevisions.Add(revision);
    }
}
