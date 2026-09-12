using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds the shipped default <see cref="EmailTemplate"/> rows — one per event key, per tenant —
/// from <b>every registered</b> <see cref="IEmailEventCatalog"/>, so each module's documents appear
/// in the email-template designer as editable rows rather than living only in code.
/// </summary>
/// <remarks>
/// <para><b>Why one seeder rather than one per module.</b> This replaces
/// <c>RecruitmentEmailTemplateSeeder</c> and <c>ProbationEmailTemplateSeeder</c>, which were
/// identical but for the catalog they read. The probation copy said so in as many words — <i>"if a
/// third module ships documents, merge the two into one seeder driven by the registered
/// IEmailEventCatalog set rather than adding a third copy"</i> — and staff assets (AST-5's
/// responsibility-and-terms form) is that third module. A module now ships documents by registering
/// a catalog; nothing here needs to change again.</para>
///
/// <para><b>What "editable" depends on.</b> <c>TemplatedEmailService</c> resolves a stored template
/// first and falls back to the catalog default, so every document renders correctly with no row
/// present. But a letter that exists only as a fallback is a letter HR cannot reword: the designer
/// at <c>/administration/settings/email</c> lists stored rows. This seeder is what puts them on that
/// list.</para>
///
/// <para>Idempotent and best-effort: inserts a default only where no template exists for that tenant
/// with that event key, or a clashing name. It never overwrites an edited row — a tenant that has
/// reworded a document keeps their wording across restarts and upgrades.</para>
/// </remarks>
public class EmailTemplateCatalogSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly IEnumerable<IEmailEventCatalog> _catalogs;
    private readonly ILogger<EmailTemplateCatalogSeeder> _logger;

    public EmailTemplateCatalogSeeder(
        ApplicationDbContext context,
        IEnumerable<IEmailEventCatalog> catalogs,
        ILogger<EmailTemplateCatalogSeeder> logger)
    {
        _context = context;
        _catalogs = catalogs;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        try
        {
            var descriptors = _catalogs.SelectMany(c => c.Events).ToList();
            if (descriptors.Count == 0) return;

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

            var added = 0;
            foreach (var tenantId in tenantIds)
            {
                foreach (var descriptor in descriptors)
                {
                    if (byEvent.Contains((tenantId, descriptor.EventKey))) continue;
                    if (byName.Contains((tenantId, descriptor.Name))) continue; // unique (TenantId,Name)

                    _context.EmailTemplates.Add(new EmailTemplate
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        Module = descriptor.Module,
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

                    // Two rows for the same tenant are added in one pass when a catalog declares two
                    // events with the same name; recording them here keeps the guard honest within
                    // the loop as well as against the database.
                    byEvent.Add((tenantId, descriptor.EventKey));
                    byName.Add((tenantId, descriptor.Name));
                    added++;
                }
            }

            if (added > 0)
            {
                await _context.SaveChangesAsync(ct);
                _logger.LogInformation(
                    "Seeded {Count} default email templates across {Modules} module catalog(s).",
                    added, _catalogs.Select(c => c.Module).Distinct().Count());
            }
        }
        catch (Exception ex)
        {
            // Best-effort — never block startup on template seeding. Every document still renders
            // from its catalog default; it simply cannot be edited until a row exists.
            _logger.LogError(ex,
                "EmailTemplateCatalogSeeder failed; documents will render from their built-in catalog defaults.");
        }
    }
}
