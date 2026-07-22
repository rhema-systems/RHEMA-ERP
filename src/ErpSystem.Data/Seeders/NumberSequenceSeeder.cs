using ErpSystem.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Aligns the <see cref="NumberSequence"/> table with numbers that already exist in the document
/// tables, so switching a generator from the old <c>Count()+1</c> / <c>Max()+1</c> scheme to the
/// sequence table never re-issues a number a document already uses.
///
/// <para>Without this, an existing database (with, say, <c>REQ-2026-00007</c> already present) would
/// start the sequence at 1 and every create would collide on the unique index until the sequence
/// climbed past the existing maximum. Runs at startup, is idempotent, and only ever raises a
/// sequence — never lowers it.</para>
/// </summary>
public class NumberSequenceSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NumberSequenceSeeder> _logger;

    public NumberSequenceSeeder(ApplicationDbContext context, ILogger<NumberSequenceSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        try
        {
            // Year-scoped: REQ-{year}-{seq}
            var reqs = await _context.StaffRequisitions.IgnoreQueryFilters()
                .Where(r => r.RequisitionNumber != null && r.RequisitionNumber != "")
                .Select(r => new { r.TenantId, Number = r.RequisitionNumber })
                .ToListAsync(ct);
            SeedYearScoped("REQ", reqs.Select(x => (x.TenantId, x.Number)));

            // Flat (no year in the printed number): APP / CAND / VAC
            var apps = await _context.JobApplications.IgnoreQueryFilters()
                .Where(a => a.ApplicationNumber != null && a.ApplicationNumber != "")
                .Select(a => new { a.TenantId, Number = a.ApplicationNumber }).ToListAsync(ct);
            SeedFlat("APP", apps.Select(x => (x.TenantId, x.Number)));

            var cands = await _context.JobCandidates.IgnoreQueryFilters()
                .Where(c => c.CandidateNumber != null && c.CandidateNumber != "")
                .Select(c => new { c.TenantId, Number = c.CandidateNumber }).ToListAsync(ct);
            SeedFlat("CAND", cands.Select(x => (x.TenantId, x.Number)));

            var vacs = await _context.JobVacancies.IgnoreQueryFilters()
                .Where(v => v.VacancyNumber != null && v.VacancyNumber != "")
                .Select(v => new { v.TenantId, Number = v.VacancyNumber }).ToListAsync(ct);
            SeedFlat("VAC", vacs.Select(x => (x.TenantId, x.Number)));

            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Seeding is best-effort — never block startup on it.
            _logger.LogError(ex, "NumberSequenceSeeder failed; reference-number sequences may collide with existing data.");
        }
    }

    private void SeedYearScoped(string key, IEnumerable<(Guid TenantId, string Number)> numbers)
    {
        // max sequence per (tenant, year)
        var maxByTenantYear = new Dictionary<(Guid, int), long>();
        foreach (var (tenantId, number) in numbers)
        {
            var (year, seq) = ParseYearAndTrailing(number);
            if (year is null || seq is null) continue;
            var k = (tenantId, year.Value);
            maxByTenantYear[k] = Math.Max(maxByTenantYear.GetValueOrDefault(k), seq.Value);
        }

        foreach (var ((tenantId, year), max) in maxByTenantYear)
            EnsureAtLeast(tenantId, key, year, max);
    }

    private void SeedFlat(string key, IEnumerable<(Guid TenantId, string Number)> numbers)
    {
        var maxByTenant = new Dictionary<Guid, long>();
        foreach (var (tenantId, number) in numbers)
        {
            var seq = ParseTrailing(number);
            if (seq is null) continue;
            maxByTenant[tenantId] = Math.Max(maxByTenant.GetValueOrDefault(tenantId), seq.Value);
        }

        foreach (var (tenantId, max) in maxByTenant)
            EnsureAtLeast(tenantId, key, 0, max);
    }

    private void EnsureAtLeast(Guid tenantId, string key, int year, long value)
    {
        if (value <= 0) return;

        var existing = _context.Set<NumberSequence>().Local
            .FirstOrDefault(s => s.TenantId == tenantId && s.SequenceKey == key && s.Year == year)
            ?? _context.Set<NumberSequence>().IgnoreQueryFilters()
                .FirstOrDefault(s => s.TenantId == tenantId && s.SequenceKey == key && s.Year == year);

        if (existing == null)
        {
            _context.Set<NumberSequence>().Add(new NumberSequence
            {
                TenantId = tenantId,
                SequenceKey = key,
                Year = year,
                NextValue = value,
            });
        }
        else if (existing.NextValue < value)
        {
            existing.NextValue = value;
        }
    }

    private static readonly Regex TrailingDigits = new(@"(\d+)\s*$", RegexOptions.Compiled);

    private static long? ParseTrailing(string number)
    {
        var m = TrailingDigits.Match(number);
        return m.Success && long.TryParse(m.Groups[1].Value, out var v) ? v : null;
    }

    private static (int? Year, long? Seq) ParseYearAndTrailing(string number)
    {
        // e.g. REQ-2026-00007  ->  year 2026, seq 7
        var parts = number.Split('-');
        int? year = null;
        foreach (var p in parts)
        {
            if (p.Length == 4 && int.TryParse(p, out var y) && y is >= 2000 and <= 2100) { year = y; break; }
        }
        return (year, ParseTrailing(number));
    }
}
