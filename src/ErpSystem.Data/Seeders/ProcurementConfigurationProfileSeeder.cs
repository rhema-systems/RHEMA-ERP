using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

public sealed class ProcurementConfigurationProfileSeeder
{
    public const string DefaultProfileCode = "TDC-PROCUREMENT";

    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProcurementConfigurationProfileSeeder> _logger;

    public ProcurementConfigurationProfileSeeder(
        ApplicationDbContext context,
        ILogger<ProcurementConfigurationProfileSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
    {
        var tenantIds = await _context.Tenants
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var created = 0;
        foreach (var tenantId in tenantIds)
            created += await SeedTenantAsync(tenantId, null, cancellationToken) ? 1 : 0;
        return created;
    }

    public async Task<bool> SeedTenantAsync(
        Guid tenantId,
        Guid? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        var exists = await _context.ProcurementConfigurationProfiles.AnyAsync(item =>
            item.TenantId == tenantId && !item.IsDeleted && item.ProfileCode == DefaultProfileCode,
            cancellationToken);
        if (exists) return false;

        var now = DateTime.UtcNow;
        var profile = new ProcurementConfigurationProfile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProfileKey = Guid.NewGuid(),
            ProfileCode = DefaultProfileCode,
            Name = "TDC Procurement Configuration",
            Version = 1,
            LifecycleStatus = ProcurementConfigurationProfileStatus.Draft,
            EffectiveFrom = now.Date,
            ChangeSummary = "Initial tenant draft created by the idempotent TDC-0001 initializer. Values remain unapproved.",
            IsDefault = true,
            CreatedAt = now,
            CreatedBy = "System",
            CreatedById = actorUserId
        };
        _context.ProcurementConfigurationProfiles.Add(profile);

        foreach (var definition in ProcurementConfigurationDecisionRegistry.Definitions)
        {
            _context.ProcurementConfigurationDecisions.Add(new ProcurementConfigurationDecision
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProfileId = profile.Id,
                DecisionKey = definition.DecisionKey,
                SchemaVersion = definition.SchemaVersion,
                OwnerGroup = definition.OwnerGroup,
                Status = ProcurementConfigurationDecisionStatus.Draft,
                ApprovalStatus = ProcurementConfigurationApprovalStatus.Pending,
                EvidenceStatus = ProcurementConfigurationEvidenceStatus.Missing,
                ValueJson = "{}",
                SourceLineage = "TDC-0001 tenant initializer; unapproved draft",
                CreatedAt = now,
                CreatedBy = "System",
                CreatedById = actorUserId
            });
        }

        _context.ProcurementConfigurationRevisions.Add(new ProcurementConfigurationRevision
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProfileId = profile.Id,
            Action = "SeedDraft",
            Result = "Succeeded",
            CorrelationId = $"seed-{tenantId:N}",
            ActorUserId = actorUserId ?? Guid.Empty,
            ActorName = "System",
            ActorRoles = "System",
            Reason = "Idempotent initialization of the TDC procurement configuration decision register.",
            AfterJson = "{\"profileCode\":\"TDC-PROCUREMENT\",\"version\":1,\"lifecycleStatus\":\"draft\",\"decisionCount\":14}",
            CreatedAt = now,
            CreatedBy = "System",
            CreatedById = actorUserId
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded draft procurement configuration profile for tenant {TenantId}", tenantId);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraint(exception))
        {
            var alreadySeeded = await _context.ProcurementConfigurationProfiles
                .AsNoTracking()
                .AnyAsync(item =>
                    item.TenantId == tenantId && !item.IsDeleted && item.ProfileCode == DefaultProfileCode,
                    cancellationToken);
            if (!alreadySeeded) throw;
            _context.ChangeTracker.Clear();
            return false;
        }
    }

    private static bool IsUniqueConstraint(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true ||
        exception.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true;
}
