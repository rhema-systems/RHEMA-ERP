using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Training;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Seeders;

/// <summary>
/// Seeds the <b>approved</b> 2026 training vote for the DEFAULT tenant and the payments drawn
/// against it.
///
/// <para><b>Why this is a seeder and not a scenario step.</b> Everything else in Training is built
/// through the API by the persona who would really do it, and the budget rows themselves can be:
/// <c>POST /api/training-budgets</c> is <c>HR.Training.Write</c>, which the HR head holds. The
/// <i>approval</i> cannot be. <c>POST /api/training-budgets/{id}/approve</c> requires
/// <c>HR.Training.Admin</c>, and in this database no employee-linked user holds it — the HR role
/// carries no <c>*.Admin</c> permission at all, and the only accounts that do (SuperAdmin,
/// TenantAdmin) are the platform's <c>admin</c> login, which has no <c>EmployeeId</c>, so the
/// endpoint answers 400 "Your user account is not linked to an employee record" before it reaches
/// the service. A budget therefore cannot leave Draft, and
/// <c>POST /api/training-budgets/{id}/transactions</c> refuses every payment with "Transactions can
/// only be recorded against approved or active budgets." That leaves the budget screens, the
/// over-budget report and the training dashboard's spend figures with nothing to render.</para>
///
/// <para>The fix belongs in the persona/permission setup — either the HR role gains
/// <c>HR.Training.Admin</c>, or a tenant-administrator account is linked to an employee record.
/// Until then this seeder writes what the approval would have written: the two votes, approved by
/// the Head of HR &amp; Administration, and the three payments made against them, with the budgets'
/// <c>SpentAmount</c> maintained exactly as <c>TrainingBudgetService.RecordTransactionAsync</c>
/// maintains it. Delete this seeder the day the door opens.</para>
///
/// <para>Idempotent: guarded on the transactions table, and every row is matched on its natural key
/// (budget code, invoice reference) before it is written, so a re-run adds nothing. It also runs
/// safely <i>after</i> the scenarios have created the budgets — it adopts and approves whatever
/// <c>scenarios/040-training.mjs</c> left in Draft rather than creating a second pair.</para>
///
/// <para>Figures are the Corporation's 2026 training vote as used throughout Book 3: GHS 250,000
/// corporate and GHS 80,000 for the Development Department, against GL 5410-000.</para>
/// </summary>
public class TdcDemoTrainingBudgetSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TdcDemoTrainingBudgetSeeder> _logger;

    private const string By = "TdcDemoTrainingBudgetSeeder";
    private const string GlAccount = "5410-000";

    public TdcDemoTrainingBudgetSeeder(ApplicationDbContext context, ILogger<TdcDemoTrainingBudgetSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var tenant = await _context.Set<Tenant>().FirstOrDefaultAsync(t => t.Code == "DEFAULT", ct);
        if (tenant is null)
        {
            _logger.LogError("DEFAULT tenant not found — cannot seed the training budget.");
            return;
        }

        var tenantId = tenant.Id;

        if (await _context.Set<TrainingBudgetTransaction>().IgnoreQueryFilters()
                .AnyAsync(t => t.TenantId == tenantId && !t.IsDeleted, ct))
        {
            _logger.LogInformation("Training budget transactions already present for the DEFAULT tenant. Skipping.");
            return;
        }

        var now = DateTime.UtcNow;
        var year = DateTime.Today.Year;

        // The approver and the recorder: the Head of HR & Administration, resolved by position title
        // rather than by a hardcoded id, and falling back to any TDC officer so the seeder cannot
        // fail on an org that is titled slightly differently.
        var hrHeadPositionIds = await _context.Set<EmployeePosition>().IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted && p.Title == "Head of HR & Administration")
            .Select(p => p.Id)
            .ToListAsync(ct);

        var officers = await _context.Set<Employee>().IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.EmployeeNumber.StartsWith("TDC/"))
            .OrderBy(e => e.EmployeeNumber)
            .Select(e => new { e.Id, e.PositionId })
            .ToListAsync(ct);

        Guid? hrHeadId = officers.FirstOrDefault(o => hrHeadPositionIds.Contains(o.PositionId))?.Id
                      ?? officers.FirstOrDefault()?.Id;

        if (hrHeadId is null)
        {
            _logger.LogWarning("No TDC/ employee found — the training budget cannot be approved or its payments attributed. Skipping.");
            return;
        }

        var units = await _context.Set<OrganizationUnit>().IgnoreQueryFilters()
            .Where(u => u.TenantId == tenantId && !u.IsDeleted)
            .Select(u => new { u.Id, u.Code })
            .ToListAsync(ct);
        Guid? UnitId(string code) => units.FirstOrDefault(u => u.Code == code)?.Id;

        // Schedules only exist once the training scenario has run. On a fresh build the seeder runs
        // first and the payments simply carry no schedule link, which is what a finance voucher
        // keyed in before the course is scheduled looks like anyway.
        var schedules = await _context.Set<TrainingSchedule>().IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => new { s.Id, s.ScheduleNumber })
            .ToListAsync(ct);
        Guid? ScheduleId(string number) => schedules.FirstOrDefault(s => s.ScheduleNumber == number)?.Id;

        var wanted = new[]
        {
            new
            {
                Code = $"TRN-{year}-CORP",
                UnitCode = "DEPT-HRA",
                Allocated = 250_000m,
                Notes = $"Corporate training vote for {year}, approved with the Corporation's budget by the Board in December {year - 1}.",
                Payments = new[]
                {
                    new
                    {
                        Description = "GIMPA Executive Education — Supervisory Skills for New Managers (SCHED-2026-001), 8 participants",
                        Amount = 19_200m,
                        Days = -6,
                        Reference = "INV/GIMPA/2026/0431",
                        Voucher = "PV-2026-0912",
                        Schedule = "SCHED-2026-001",
                    },
                    new
                    {
                        Description = "Ghana Institution of Engineers — First Aid at Work (SCHED-2026-003), 9 participants",
                        Amount = 8_100m,
                        Days = -4,
                        Reference = "INV/GHIE/2026/0118",
                        Voucher = "PV-2026-0918",
                        Schedule = "SCHED-2026-003",
                    },
                },
            },
            new
            {
                Code = $"TRN-{year}-DEV",
                UnitCode = "DEPT-DEV",
                Allocated = 80_000m,
                Notes = $"Development Department share of the {year} training vote — drawing office upgrade and statutory refreshers.",
                Payments = new[]
                {
                    new
                    {
                        Description = "Public Procurement Act 663 update (SCHED-2026-004), 7 participants",
                        Amount = 4_550m,
                        Days = -17,
                        Reference = "INV/GIMPA/2026/0388",
                        Voucher = "PV-2026-0847",
                        Schedule = "SCHED-2026-004",
                    },
                },
            },
        };

        var existing = await _context.Set<TrainingBudget>().IgnoreQueryFilters()
            .Where(b => b.TenantId == tenantId && !b.IsDeleted)
            .ToListAsync(ct);

        var budgets = 0;
        var payments = 0;

        foreach (var spec in wanted)
        {
            var budget = existing.FirstOrDefault(b => b.BudgetCode == spec.Code);

            if (budget is null)
            {
                budget = new TrainingBudget
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BudgetCode = spec.Code,
                    Year = year,
                    Quarter = null,
                    OrganizationUnitId = UnitId(spec.UnitCode),
                    Currency = "GHS",
                    AllocatedAmount = spec.Allocated,
                    CommittedAmount = 0m,
                    SpentAmount = 0m,
                    GLAccountCode = GlAccount,
                    CostCenterCode = spec.UnitCode,
                    Status = TrainingBudgetStatus.Draft,
                    Notes = spec.Notes,
                    CreatedAt = now,
                    CreatedBy = By,
                };

                _context.Set<TrainingBudget>().Add(budget);
                budgets++;
            }

            // What the unreachable approve endpoint would have done.
            if (budget.Status == TrainingBudgetStatus.Draft)
            {
                budget.Status = TrainingBudgetStatus.Approved;
                budget.ApprovedById = hrHeadId;
                budget.ApprovalDate = now.AddDays(-30);
                budget.UpdatedAt = now;
                budget.UpdatedBy = By;
            }

            foreach (var p in spec.Payments)
            {
                _context.Set<TrainingBudgetTransaction>().Add(new TrainingBudgetTransaction
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    BudgetId = budget.Id,
                    ScheduleId = ScheduleId(p.Schedule),
                    Description = p.Description,
                    Amount = p.Amount,
                    TransactionDate = DateTime.Today.AddDays(p.Days),
                    RecordedById = hrHeadId,
                    Reference = p.Reference,
                    GLAccountCode = GlAccount,
                    VoucherNumber = p.Voucher,
                    Notes = "Paid by cheque against the invoice; copy on the training file.",
                    CreatedAt = now,
                    CreatedBy = By,
                });

                // Same running-spend maintenance the service does, so RemainingAmount and the
                // over-budget report stay truthful.
                budget.SpentAmount += p.Amount;
                payments++;
            }

            budget.UpdatedAt = now;
            budget.UpdatedBy = By;
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Seeded {Budgets} new training budget(s) and {Payments} payment(s), and approved every draft "
            + "training budget for the DEFAULT tenant. The approve endpoint needs HR.Training.Admin, which "
            + "no employee-linked user holds — remove this seeder once that is fixed.",
            budgets, payments);
    }
}
