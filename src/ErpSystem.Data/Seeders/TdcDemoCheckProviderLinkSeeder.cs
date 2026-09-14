using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Points every pre-employment check — the ones already run, and the template they are raised from —
/// at the supplier that actually performs it.
///
/// <para>It does three things: creates the three provider suppliers, maps each to the checks it
/// performs, and stamps that link onto every check item and template item that has none.</para>
///
/// <para><b>⚠ The first of those writes Procurement's table directly, which is normally not done
/// here.</b> It is done because the door cannot: <c>POST /api/Suppliers</c> answers <b>2xx with an
/// empty body and writes no row</b> (measured 2026-09-14) — a silent false success. See
/// <see cref="EnsureProvidersAsync"/> for the detail and for what to delete when it is fixed. The
/// HR half, <c>POST /api/pre-employment-checks/providers</c>, works fine and is only bypassed
/// because it needs a supplier id that cannot be obtained.</para>
///
/// <para><b>Why it is a separate step.</b> The check items are written by
/// <see cref="TdcDemoRecruitmentHistorySeeder"/> and by the API scenario. Nothing can stamp the link
/// until those exist, and that seeder's own guard (a filled vacancy exists) will have closed by
/// then. Its own probe — "does any check item name a supplier?" — lets it run on a later pass over
/// a database that is otherwise complete.</para>
///
/// <para>Idempotent: only items with no supplier are touched, and the step is skipped once any item
/// carries one.</para>
/// </summary>
public class TdcDemoCheckProviderLinkSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TdcDemoCheckProviderLinkSeeder> _logger;

    private const string By = "TdcDemoCheckProviderLinkSeeder";

    public TdcDemoCheckProviderLinkSeeder(
        ApplicationDbContext context, ILogger<TdcDemoCheckProviderLinkSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var tenant = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Code == "DEFAULT", ct);
        if (tenant is null)
        {
            _logger.LogError("DEFAULT tenant not found — cannot link the check providers.");
            return;
        }

        var tenantId = tenant.Id;

        await EnsureProvidersAsync(tenantId, ct);

        var services = await _context.Set<PreEmploymentCheckProviderService>().IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && s.IsActive)
            .ToListAsync(ct);

        if (services.Count == 0)
        {
            _logger.LogWarning("No pre-employment check providers could be registered; nothing to link.");
            return;
        }

        var supplierNames = await _context.Set<Supplier>().IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        // First registered provider wins for a check type — the demo registers exactly one each.
        var providerFor = services
            .GroupBy(s => s.CheckType)
            .ToDictionary(g => g.Key, g => g.First().SupplierId);

        var now = DateTime.UtcNow;
        int items = 0, templateItems = 0;

        // ── The checks already run against an offer ──────────────────────────────────────────────
        var checkItems = await _context.Set<PreEmploymentCheckItem>().IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId && !i.IsDeleted && i.ServiceProviderSupplierId == null)
            .ToListAsync(ct);

        foreach (var item in checkItems)
        {
            if (!providerFor.TryGetValue(item.CheckType, out var supplierId)) continue;

            item.ServiceProviderSupplierId = supplierId;
            // The free-text name is kept in step with the link, so the screen reads the registered
            // provider rather than whatever was typed when the check was raised.
            if (supplierNames.TryGetValue(supplierId, out var name)) item.ServiceProviderName = name;
            item.UpdatedAt = now;
            item.UpdatedBy = By;
            items++;
        }

        // ── The template every future check is raised from ───────────────────────────────────────
        var templateRows = await _context.Set<PreEmploymentCheckTemplateItem>().IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId && !i.IsDeleted && i.DefaultServiceProviderSupplierId == null)
            .ToListAsync(ct);

        foreach (var row in templateRows)
        {
            if (!providerFor.TryGetValue(row.CheckType, out var supplierId)) continue;

            row.DefaultServiceProviderSupplierId = supplierId;
            if (supplierNames.TryGetValue(supplierId, out var name)) row.DefaultServiceProvider = name;
            row.UpdatedAt = now;
            row.UpdatedBy = By;
            templateItems++;
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Pre-employment checks linked to their providers: {Providers} registered providers covering "
            + "{Types} check types, {Items} check items and {TemplateItems} template items stamped.",
            services.Select(s => s.SupplierId).Distinct().Count(), providerFor.Count, items, templateItems);
    }

    /// <summary>The three providers, and which checks each performs.</summary>
    private static readonly (string Code, string Name, string Type, string Contact, string Phone, string Email,
                             string City, PreEmploymentCheckType[] Checks, string Notes)[] Providers =
    {
        ("SUP-MED-001", "Tema Diagnostic & Occupational Health Centre", "Service Provider",
            "Dr Yaw Boadu", "0303204417", "occupational@temadiagnostic.com.gh", "Tema",
            new[] { PreEmploymentCheckType.MedicalExamination, PreEmploymentCheckType.DrugTest },
            "Panel provider for pre-employment medicals and drug screening. Turnaround five working days."),

        ("SUP-VER-001", "Sentinel Verification Services Ltd", "Service Provider",
            "Mrs Akosua Boateng", "0302771902", "checks@sentinelverify.com.gh", "Accra",
            new[] { PreEmploymentCheckType.BackgroundCheck, PreEmploymentCheckType.AcademicVerification,
                    PreEmploymentCheckType.ProfessionalLicenceVerification, PreEmploymentCheckType.ReferenceCheck,
                    PreEmploymentCheckType.CreditCheck },
            "Retained for certificate verification with the awarding institutions, employment history and referee follow-up."),

        ("SUP-PCC-001", "Ghana Police Service — Criminal Investigations Department", "Government Agency",
            "Police Clearance Unit", "0302773906", "clearance@police.gov.gh", "Accra",
            new[] { PreEmploymentCheckType.PoliceClearance },
            "Statutory criminal record clearance. Fee paid per applicant; certificate issued to the corporation."),
    };

    /// <summary>
    /// Creates the three provider suppliers and maps each to the checks it performs.
    ///
    /// <para><b>⚠ This writes Procurement's Supplier table directly, which is normally not done.</b>
    /// It is done here because the door cannot: measured 2026-09-14,
    /// <c>POST /api/Suppliers</c> answers <b>2xx with an empty body and writes no row</b> — a silent
    /// false success, with no error and no id for the caller to act on. (Its companion defect, every
    /// endpoint on that controller answering 400 because two repositories were missing from DI, was
    /// fixed the same day; reads work now, the write does not.) Both are recorded as items 26 and 28
    /// in <c>docs/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md</c>. <b>When the write is fixed, this
    /// method should be deleted and scenario 050 §21 restored</b> — the mapping endpoint,
    /// <c>POST /api/pre-employment-checks/providers</c>, works and should be used.</para>
    /// </summary>
    private async Task EnsureProvidersAsync(Guid tenantId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var codes = Providers.Select(p => p.Code).ToList();
        var existing = await _context.Set<Supplier>().IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && codes.Contains(s.SupplierCode))
            .ToDictionaryAsync(s => s.SupplierCode, s => s, ct);

        var mapped = await _context.Set<PreEmploymentCheckProviderService>().IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => new { s.SupplierId, s.CheckType })
            .ToListAsync(ct);

        var added = 0;
        foreach (var p in Providers)
        {
            if (!existing.TryGetValue(p.Code, out var supplier))
            {
                supplier = new Supplier
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SupplierCode = p.Code,
                    Name = p.Name,
                    SupplierType = p.Type,
                    Description = p.Notes,
                    City = p.City,
                    Country = "Ghana",
                    Phone = p.Phone,
                    Email = p.Email,
                    PrimaryContactName = p.Contact,
                    PrimaryContactPhone = p.Phone,
                    PrimaryContactEmail = p.Email,
                    PaymentTerms = "Net 30",
                    LeadTimeDays = 5,
                    IsActive = true,
                    Status = "Active",
                    IsWithholdingTaxApplicable = false,
                    CreatedAt = now,
                    CreatedBy = By,
                };
                _context.Set<Supplier>().Add(supplier);
                existing[p.Code] = supplier;
            }

            foreach (var check in p.Checks)
            {
                if (mapped.Any(m => m.SupplierId == supplier.Id && m.CheckType == check)) continue;

                _context.Set<PreEmploymentCheckProviderService>().Add(new PreEmploymentCheckProviderService
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SupplierId = supplier.Id,
                    CheckType = check,
                    Notes = p.Notes,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = By,
                });
                added++;
            }
        }

        if (added > 0)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Registered {Count} check-provider mappings across {Providers} suppliers, written directly "
                + "because POST /api/Suppliers is a silent no-op on this build (defect 28).",
                added, Providers.Length);
        }
    }
}
