using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// TDC's 2026 salary scale, for the <b>DEFAULT tenant</b>: eight grades (M1–M5, S1–S3) and their
/// monthly notch amounts, written into <b>payroll's</b> grade and notch tables.
/// </summary>
/// <remarks>
/// <para><b>Source.</b> <c>records shared/ERP Salary Scale 2026.xlsx</c>, one sheet per grade,
/// transcribed by a script rather than by hand. The column rules the user settled on 2026-09-09:
/// S1–S3 carry a 2025 column and a rationalised column (+15 % / +20 % / +25 %) — the rationalised
/// one is the 2026 scale; M1–M4 carry a single column headed 2025 and it IS the 2026 figure; M5 is
/// headed "Rationalised 10 %" and is already rationalised, rounded here to 2 dp. <b>M4 has no notch
/// 19</b> — the sheet goes 18 → 20 and that gap is real; it is seeded exactly as it stands.</para>
///
/// <para><b>Why payroll's tables and not HR's.</b> Payroll owns the salary structure (decision of
/// 2026-08-02): HR's <c>SalaryGrade</c>/<c>SalaryLevel</c>/<c>SalaryNotch</c> are a projection of
/// payroll's rows, rebuilt on read, and anything seeded into them directly would be overwritten or
/// orphaned by the next pass. Seeding the master is the only way the scale arrives everywhere once.
/// Writing rows into payroll's tables is data setup, not a change to payroll's code — the same
/// footing as the demo-smoke scenario that seeded the five placeholder notches this replaces.</para>
///
/// <para><b>Value is MONTHLY.</b> Payroll's <c>Value</c> is the monthly figure and
/// <c>AnnualisedValue</c> is ×12 — the existing rows and <c>PayrollService</c> both read it so, and
/// HR's <c>SalaryNotch.SalaryAmount</c> (which the projection copies from <c>Value</c>) is what
/// <c>HrBasicPay</c> quotes as monthly basic pay. The annual figure is recomputed as monthly × 12
/// rather than copied from the sheet, whose annual columns are laid out differently per sheet.</para>
///
/// <para><b>An ensure, not an insert.</b> Grades are matched by code and notches by grade + number;
/// an existing notch whose amount differs is updated, a missing one is added, an existing grade's
/// band is recomputed from its notches. The five invented notches the demo scenario wrote (18,000 …
/// 28,000 on M1) are corrected to the scale by the same rule. Nothing is deleted: a notch that is
/// on payroll's side and not on the sheet is left as it is, and the projection keeps it.</para>
/// </remarks>
public class TdcSalaryScaleSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TdcSalaryScaleSeeder> _logger;

    private const string By = "TdcSalaryScaleSeeder";
    private const string Currency = "GHS";

    private sealed record GradeDef(string Code, string Name, string Type, int Order, (int Notch, decimal Monthly)[] Notches);

    // ⚠ Generated from the workbook. Do not hand-edit an amount here — change the sheet and re-run
    // the generator, so the seed and the document the auditors hold cannot drift.
    private static readonly GradeDef[] Scale =
    {
        new("M1", "Management grade M1", "Management", 1, new (int Notch, decimal Monthly)[]
        {
            (1, 32110.11m), (2, 32912.88m), (3, 33735.70m), (4, 34579.10m), (5, 35443.57m), (6, 36329.66m), (7, 37237.92m), (8, 38168.84m), (9, 39123.05m), (10, 40101.16m), (11, 41103.66m), (12, 42131.25m), (13, 43184.55m), (14, 44264.18m), (15, 45370.76m), (16, 46505.04m), (17, 47667.67m), (18, 48859.35m), (19, 50080.82m), (20, 51332.86m)
        }),
        new("M2", "Management grade M2", "Management", 2, new (int Notch, decimal Monthly)[]
        {
            (1, 19994.76m), (2, 20494.62m), (3, 21006.97m), (4, 21532.15m), (5, 22070.47m), (6, 22622.23m), (7, 23187.80m), (8, 23767.48m), (9, 24361.66m), (10, 24970.73m), (11, 25594.96m), (12, 26234.87m), (13, 26890.73m), (14, 27563.00m), (15, 28252.07m), (16, 28958.39m), (17, 29682.33m), (18, 30424.42m), (19, 31185.00m), (20, 31964.62m)
        }),
        new("M3", "Senior staff grade M3", "Senior", 3, new (int Notch, decimal Monthly)[]
        {
            (1, 13041.08m), (2, 13367.10m), (3, 13701.28m), (4, 14043.81m), (5, 14394.89m), (6, 14754.77m), (7, 15123.66m), (8, 15501.75m), (9, 15889.29m), (10, 16286.53m), (11, 16693.69m), (12, 17111.03m), (13, 17538.79m), (14, 17977.26m), (15, 18426.71m), (16, 18887.36m), (17, 19359.55m), (18, 19843.54m), (19, 20339.62m), (20, 20848.12m)
        }),
        new("M4", "Senior staff grade M4", "Senior", 4, new (int Notch, decimal Monthly)[]
        {
            (1, 8993.85m), (2, 9218.70m), (3, 9449.15m), (4, 9685.39m), (5, 9927.53m), (6, 10175.72m), (7, 10430.09m), (8, 10690.86m), (9, 10958.13m), (10, 11232.10m), (11, 11512.89m), (12, 11800.70m), (13, 12095.72m), (14, 12398.10m), (15, 12708.07m), (16, 13025.77m), (17, 13351.40m), (18, 13685.20m), (20, 14027.31m), (21, 14378.00m), (22, 14737.47m), (23, 15105.89m)
        }),
        new("M5", "Senior staff grade M5", "Senior", 5, new (int Notch, decimal Monthly)[]
        {
            (1, 6822.90m), (2, 6993.48m), (3, 7168.32m), (4, 7347.55m), (5, 7531.23m), (6, 7719.51m), (7, 7912.51m), (8, 8110.30m), (9, 8313.06m), (10, 8520.90m), (11, 8733.91m), (12, 8952.25m), (13, 9176.05m), (14, 9405.46m), (15, 9640.63m), (16, 9881.60m), (17, 10128.66m), (18, 10381.88m), (19, 10641.42m), (20, 10907.46m), (21, 11180.16m), (22, 11459.65m)
        }),
        new("S1", "Junior staff grade S1", "Junior", 6, new (int Notch, decimal Monthly)[]
        {
            (1, 4919.34m), (2, 5042.34m), (3, 5168.39m), (4, 5297.61m), (5, 5430.05m), (6, 5565.79m), (7, 5704.94m), (8, 5847.57m), (9, 5993.74m), (10, 6143.60m), (11, 6297.18m), (12, 6454.60m), (13, 6615.96m), (14, 6781.39m), (15, 6950.91m), (16, 7124.68m), (17, 7302.80m), (18, 7485.36m), (19, 7672.51m), (20, 7864.33m), (21, 8060.91m), (22, 8262.46m), (23, 8469.00m), (24, 8680.73m), (25, 8897.77m), (26, 9120.16m), (27, 9348.21m)
        }),
        new("S2", "Junior staff grade S2", "Junior", 7, new (int Notch, decimal Monthly)[]
        {
            (1, 3719.74m), (2, 3812.74m), (3, 3908.06m), (4, 4005.76m), (5, 4105.91m), (6, 4208.53m), (7, 4313.75m), (8, 4421.58m), (9, 4532.16m), (10, 4645.42m), (11, 4761.58m), (12, 4880.62m), (13, 5002.64m), (14, 5127.68m), (15, 5255.88m), (16, 5387.29m), (17, 5521.97m), (18, 5660.02m), (19, 5801.52m), (20, 5946.58m), (21, 6095.22m), (22, 6247.61m), (23, 6403.76m), (24, 6563.87m), (25, 6727.98m), (26, 6896.18m), (27, 7068.59m)
        }),
        new("S3", "Junior staff grade S3", "Junior", 8, new (int Notch, decimal Monthly)[]
        {
            (1, 2707.49m), (2, 2775.17m), (3, 2844.59m), (4, 2915.69m), (5, 2988.58m), (6, 3063.27m), (7, 3139.90m), (8, 3218.36m), (9, 3298.81m), (10, 3381.28m), (11, 3465.82m), (12, 3552.46m), (13, 3641.29m), (14, 3732.30m), (15, 3825.61m), (16, 3921.26m), (17, 4019.28m), (18, 4119.78m), (19, 4222.77m), (20, 4328.34m), (21, 4436.52m), (22, 4547.48m), (23, 4661.16m), (24, 4777.69m), (25, 4897.11m), (26, 5019.52m), (27, 5145.03m)
        }),
    };

    public TdcSalaryScaleSeeder(ApplicationDbContext context, ILogger<TdcSalaryScaleSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var tenant = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Code == "DEFAULT", ct);
        if (tenant is null)
        {
            _logger.LogError("DEFAULT tenant not found — cannot seed the salary scale.");
            return;
        }

        var tenantId = tenant.Id;
        var now = DateTime.UtcNow;
        var effective = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var grades = await _context.Set<PayrollGrade>().IgnoreQueryFilters()
            .Where(g => g.TenantId == tenantId && !g.IsDeleted)
            .ToListAsync(ct);
        var notches = await _context.Set<PayrollGradeNotch>().IgnoreQueryFilters()
            .Where(n => n.TenantId == tenantId && !n.IsDeleted)
            .ToListAsync(ct);

        int gradesAdded = 0, gradesUpdated = 0, notchesAdded = 0, notchesUpdated = 0;

        foreach (var def in Scale)
        {
            var min = def.Notches.Min(n => n.Monthly);
            var max = def.Notches.Max(n => n.Monthly);
            var mid = decimal.Round((min + max) / 2m, 2);

            var grade = grades.FirstOrDefault(g => string.Equals(g.GradeId, def.Code, StringComparison.OrdinalIgnoreCase));
            if (grade is null)
            {
                grade = new PayrollGrade
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    GradeId = def.Code,
                    GradeName = def.Name,
                    SystemGradeName = def.Code,
                    GradeType = def.Type,
                    MinValue = min,
                    MaxValue = max,
                    MidPoint = mid,
                    CurrencyCode = Currency,
                    StartDate = effective,
                    OrderField = def.Order,
                    ReportingName = $"{def.Code} — {def.Name}",
                    EnforceNotchConsistency = true,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = By,
                };
                _context.Set<PayrollGrade>().Add(grade);
                grades.Add(grade);
                gradesAdded++;
            }
            else if (grade.MinValue != min || grade.MaxValue != max || grade.MidPoint != mid || grade.GradeType != def.Type)
            {
                grade.MinValue = min;
                grade.MaxValue = max;
                grade.MidPoint = mid;
                grade.GradeType = def.Type;
                grade.UpdatedAt = now;
                grade.UpdatedBy = By;
                gradesUpdated++;
            }

            foreach (var (number, monthly) in def.Notches)
            {
                var key = number.ToString();
                var annual = decimal.Round(monthly * 12m, 2);
                var notch = notches.FirstOrDefault(n => n.PayrollGradeId == grade.Id && n.Notch == key);
                if (notch is null)
                {
                    _context.Set<PayrollGradeNotch>().Add(new PayrollGradeNotch
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        PayrollGradeId = grade.Id,
                        GradeId = def.Code,
                        SystemGradeName = def.Code,
                        GradeName = def.Name,
                        Notch = key,
                        Value = monthly,
                        AnnualisedValue = annual,
                        CurrencyCode = Currency,
                        StartDate = effective,
                        OrderField = number,
                        ReportingName = $"{def.Code} notch {number}",
                        CreatedAt = now,
                        CreatedBy = By,
                    });
                    notchesAdded++;
                }
                else if (notch.Value != monthly || notch.AnnualisedValue != annual)
                {
                    notch.Value = monthly;
                    notch.AnnualisedValue = annual;
                    notch.UpdatedAt = now;
                    notch.UpdatedBy = By;
                    notchesUpdated++;
                }
            }
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Salary scale 2026 seeded into payroll: grades +{GradesAdded}/~{GradesUpdated}, notches +{NotchesAdded}/~{NotchesUpdated}. "
            + "HR's mirror follows on the next salary-structure read.",
            gradesAdded, gradesUpdated, notchesAdded, notchesUpdated);
    }
}
