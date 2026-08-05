using ErpSystem.Core.Entities;
using ErpSystem.Core.Services.HR.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds the shipped default recruitment <see cref="EmailTemplate"/> rows (one per event key, per
/// tenant) from <see cref="RecruitmentEmailCatalog"/> so that HR sees the current transactional
/// emails as editable templates on day one, and the templated-email pipeline resolves a stored row
/// rather than the code fallback.
///
/// <para>Idempotent and best-effort: only inserts a default when no template already exists for the
/// tenant with that event key (or a clashing name). Runs at startup after migration.</para>
/// </summary>
public class RecruitmentEmailTemplateSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RecruitmentEmailTemplateSeeder> _logger;

    public RecruitmentEmailTemplateSeeder(ApplicationDbContext context, ILogger<RecruitmentEmailTemplateSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        try
        {
            var tenantIds = await _context.Tenants.IgnoreQueryFilters()
                .Select(t => t.Id)
                .ToListAsync(ct);
            if (tenantIds.Count == 0) return;

            // Existing recruitment templates across all tenants (bypassing the tenant filter).
            var existing = await _context.EmailTemplates.IgnoreQueryFilters()
                .Where(t => t.Module == RecruitmentEmailCatalog.Module)
                .Select(t => new { t.TenantId, t.EventKey, t.Name })
                .ToListAsync(ct);

            var byEvent = existing
                .Where(e => !string.IsNullOrEmpty(e.EventKey))
                .Select(e => (e.TenantId, Key: e.EventKey!))
                .ToHashSet();
            var byName = existing
                .Select(e => (e.TenantId, Name: e.Name))
                .ToHashSet();

            int added = 0;
            foreach (var tenantId in tenantIds)
            {
                foreach (var descriptor in RecruitmentEmailCatalog.All)
                {
                    if (byEvent.Contains((tenantId, descriptor.EventKey))) continue;
                    if (byName.Contains((tenantId, descriptor.Name))) continue; // avoid unique (TenantId,Name) clash

                    _context.EmailTemplates.Add(new EmailTemplate
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        Module = RecruitmentEmailCatalog.Module,
                        EventKey = descriptor.EventKey,
                        IsSystemDefault = true,
                        Name = descriptor.Name,
                        Subject = descriptor.DefaultSubject,
                        HtmlBody = descriptor.DefaultHtmlBody,
                        Category = descriptor.Category,
                        Description = descriptor.Description,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "system",
                    });
                    added++;
                }
            }

            if (added > 0)
            {
                await _context.SaveChangesAsync(ct);
                _logger.LogInformation("Seeded {Count} default recruitment email templates.", added);
            }
        }
        catch (Exception ex)
        {
            // Best-effort — never block startup on template seeding.
            _logger.LogError(ex, "RecruitmentEmailTemplateSeeder failed; recruitment emails will use built-in defaults.");
        }
    }
}
