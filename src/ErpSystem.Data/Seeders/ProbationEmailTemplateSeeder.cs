using ErpSystem.Core.Entities;
using ErpSystem.Core.Services.HR.Probation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds the shipped default probation <see cref="EmailTemplate"/> rows (one per event key, per
/// tenant) from <see cref="ProbationEmailCatalog"/>, so the FR-HR-032 confirmation letter appears
/// in the email-template designer as an editable template rather than living only in code.
/// </summary>
/// <remarks>
/// <para><b>Why this exists at all.</b> <c>TemplatedEmailService</c> resolves a stored template
/// first and falls back to the catalog default, so the letter renders correctly with no row
/// present — but a letter that only exists as a fallback is a letter HR cannot reword. The
/// designer at <c>/administration/settings/email</c> (backed by <c>EmailTemplateController</c>)
/// lists stored rows; this seeder is what puts probation on that list.</para>
///
/// <para>Idempotent and best-effort: inserts a default only when no template exists for the tenant
/// with that event key, or a clashing name. Mirrors
/// <see cref="RecruitmentEmailTemplateSeeder"/> — if a third module ships documents, merge the two
/// into one seeder driven by the registered <c>IEmailEventCatalog</c> set rather than adding a
/// third copy.</para>
/// </remarks>
public class ProbationEmailTemplateSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProbationEmailTemplateSeeder> _logger;

    public ProbationEmailTemplateSeeder(ApplicationDbContext context, ILogger<ProbationEmailTemplateSeeder> logger)
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

            var existing = await _context.EmailTemplates.IgnoreQueryFilters()
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
                foreach (var descriptor in ProbationEmailCatalog.All)
                {
                    if (byEvent.Contains((tenantId, descriptor.EventKey))) continue;
                    if (byName.Contains((tenantId, descriptor.Name))) continue; // unique (TenantId,Name)

                    _context.EmailTemplates.Add(new EmailTemplate
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        Module = ProbationEmailCatalog.Module,
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
                _logger.LogInformation("Seeded {Count} default probation email templates.", added);
            }
        }
        catch (Exception ex)
        {
            // Best-effort — never block startup on template seeding.
            _logger.LogError(ex, "ProbationEmailTemplateSeeder failed; the confirmation letter will use its built-in default.");
        }
    }
}
