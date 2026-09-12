using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR.Performance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Adds one free-text question to every appraisal template of the <b>DEFAULT tenant</b> that does
/// not already carry one.
///
/// <para><b>Why this exists, and why it is a seeder rather than a scenario.</b> An
/// <c>AppraisalCustomQuestionResponse</c> is an employee's written answer to a free-text item on
/// the appraisal form. The form is the template, and the demo template
/// ("Standard Employee Template 2026", from <c>PerformanceAppraisalDataSeeder</c>) has only KPI and
/// competency items — no free-text item at all. So the table has nothing an employee could answer,
/// and the self-evaluation screen shows no question box.</para>
///
/// <para>The API cannot fix that: <c>POST api/AppraisalTemplates/sections/{id}/items</c> answers
/// <b>409 — "This template cannot be modified because it is assigned to an Open or InProgress
/// appraisal cycle"</b> as soon as the cycle is running, which on the demo database it always is.
/// There is no other door onto a template item, so per the agent brief this is seeded.</para>
///
/// <para>The item carries <c>Weight = 0</c> deliberately. A free-text question is not scored; the
/// section's scored items already sum to 100 and a weighted question would change every score on
/// the form. It is also read live from the template by
/// <c>PerformanceAppraisalService.BuildSelfEvaluationSections</c> rather than from the appraisal's
/// criterion-config snapshot, so appraisals generated before this ran still show it.</para>
///
/// <para>Idempotent: a template that already has any free-text item is skipped.</para>
/// </summary>
public class TdcDemoAppraisalCustomQuestionSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TdcDemoAppraisalCustomQuestionSeeder> _logger;

    private const string By = "TdcDemoAppraisalCustomQuestionSeeder";

    private const string QuestionText =
        "What did you contribute this year that you are most proud of, and what support do you need "
        + "from the Corporation next year?";

    public TdcDemoAppraisalCustomQuestionSeeder(
        ApplicationDbContext context, ILogger<TdcDemoAppraisalCustomQuestionSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var tenant = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Code == "DEFAULT", ct);
        if (tenant is null)
        {
            _logger.LogError("DEFAULT tenant not found — cannot seed the appraisal free-text question.");
            return;
        }

        var tenantId = tenant.Id;

        var templateIds = await _context.Set<AppraisalTemplate>()
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted)
            .Select(t => t.Id)
            .ToListAsync(ct);

        if (templateIds.Count == 0)
        {
            _logger.LogInformation("No appraisal templates for the DEFAULT tenant — nothing to do.");
            return;
        }

        var sections = await _context.Set<AppraisalTemplateSection>()
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && templateIds.Contains(s.AppraisalTemplateId))
            .ToListAsync(ct);

        var items = await _context.Set<AppraisalTemplateItem>()
            .IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId && !i.IsDeleted)
            .Select(i => new { i.AppraisalTemplateSectionId, i.CustomQuestion, i.CompetencyId, i.KpiDefinitionId, i.DisplayOrder })
            .ToListAsync(ct);

        var added = 0;

        foreach (var templateId in templateIds)
        {
            var templateSections = sections
                .Where(s => s.AppraisalTemplateId == templateId)
                .OrderBy(s => s.DisplayOrder)
                .ToList();

            if (templateSections.Count == 0)
                continue;

            var sectionIds = templateSections.Select(s => s.Id).ToHashSet();
            var templateItems = items.Where(i => sectionIds.Contains(i.AppraisalTemplateSectionId)).ToList();

            // A free-text item is one with neither a competency nor a KPI behind it.
            var alreadyHasOne = templateItems.Any(i =>
                i.CompetencyId == null && i.KpiDefinitionId == null && !string.IsNullOrWhiteSpace(i.CustomQuestion));

            if (alreadyHasOne)
                continue;

            // Last section, after its last item, so the question closes the form.
            var section = templateSections[^1];
            var nextOrder = templateItems
                .Where(i => i.AppraisalTemplateSectionId == section.Id)
                .Select(i => i.DisplayOrder)
                .DefaultIfEmpty(0)
                .Max() + 1;

            _context.Set<AppraisalTemplateItem>().Add(new AppraisalTemplateItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AppraisalTemplateSectionId = section.Id,
                CompetencyId = null,
                KpiDefinitionId = null,
                CustomQuestion = QuestionText,
                DisplayOrder = nextOrder,
                Weight = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = By,
            });

            added++;
        }

        if (added == 0)
        {
            _logger.LogInformation("Every appraisal template already carries a free-text question — nothing added.");
            return;
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Added a free-text appraisal question to {Count} template(s).", added);
    }
}
