using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.DTOs.HR.Payroll;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Payroll;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// The bridge between HR's "is this person paid through the payroll run?" and payroll's own answer.
///
/// <para><b>Two facts, two owners.</b> <see cref="Employee.IsOnPayroll"/> is HR's statement of intent:
/// this person should be paid through the run. Whether they actually ARE is
/// <c>PayrollEmployeeProfile.PayrollActive</c>, a row payroll owns and a payroll run selects on.
/// Payroll belongs to another developer and HR integrates with it read-only, so this service never
/// collapses the two into one column: it reads both and names the discrepancy.</para>
///
/// <para><b>How far HR reaches.</b> Exactly one write, and only through payroll's own published
/// contract: when HR marks someone on payroll and payroll has no profile for them, HR calls
/// <see cref="IPayrollService.UpsertEmployeeProfileAsync"/> to create one. Never an update — that
/// upsert is a replace-set over payment methods and components, and re-sending it from HR would
/// overwrite what payroll has configured since. And never a deactivation: turning a profile off
/// mid-month is a payroll decision, so an HR flip to "off payroll" surfaces on the reconciliation
/// read for the payroll owner to act on rather than silently dropping someone from a run.</para>
///
/// <para>The push is best-effort by design. The HR save that triggered it has already committed;
/// a failure here is logged and shows up as <c>AwaitingPayrollSetup</c>, not as a failed HR save.</para>
/// </summary>
public class PayrollMembershipService : IPayrollMembershipService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPayrollService _payroll;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<PayrollMembershipService> _logger;

    public PayrollMembershipService(
        IUnitOfWork unitOfWork,
        IPayrollService payroll,
        ICurrentUserProvider currentUser,
        ILogger<PayrollMembershipService> logger)
    {
        _unitOfWork = unitOfWork;
        _payroll = payroll;
        _currentUser = currentUser;
        _logger = logger;
    }

    private Guid GetTenantId()
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // The DbContext's tenant filter is inert (see EmployeeService), so every read scopes itself.
    private IQueryable<PayrollEmployeeProfile> Profiles(Guid tenantId)
        => _unitOfWork.Repository<PayrollEmployeeProfile>().GetQueryable()
            .Where(p => p.TenantId == tenantId && !p.IsDeleted);

    private IQueryable<EmployeeSalaryAssignment> ActiveAssignments(Guid tenantId, DateTime asOf)
        => _unitOfWork.Repository<EmployeeSalaryAssignment>().GetQueryable()
            .Include(a => a.Notch)
            .Include(a => a.Level)
            .Where(a => a.TenantId == tenantId && !a.IsDeleted
                     && a.EffectiveDate <= asOf
                     && (a.EffectiveTo == null || a.EffectiveTo >= asOf));

    public async Task<EmployeePayrollStatusDto> GetStatusAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var employee = await _unitOfWork.Repository<Employee>().GetQueryable()
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.TenantId == tenantId && !e.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        var today = DateTime.UtcNow.Date;
        var assignment = await ActiveAssignments(tenantId, today)
            .Where(a => a.EmployeeId == employeeId)
            .OrderByDescending(a => a.EffectiveDate)
            .FirstOrDefaultAsync(cancellationToken);
        var profile = await Profiles(tenantId)
            .Include(p => p.SalaryBasis)
            .FirstOrDefaultAsync(p => p.EmployeeId == employeeId, cancellationToken);

        var hrBasic = employee.IsOnPayroll ? HrBasicPay(employee, assignment) : null;
        var payrollBasic = profile?.SalaryBasis is { IsActive: true } basis ? basis.MonthlyBasicSalary : (decimal?)null;

        return new EmployeePayrollStatusDto
        {
            EmployeeId = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            IsOnPayroll = employee.IsOnPayroll,
            OffPayrollReason = employee.OffPayrollReason,
            OffPayrollNote = employee.OffPayrollNote,
            HrMonthlyBasicPay = hrBasic,
            HasActiveSalaryAssignment = assignment != null,
            HasPayrollProfile = profile != null,
            PayrollActive = profile?.PayrollActive,
            PayrollMonthlyBasicSalary = payrollBasic,
            PayrollCurrencyCode = profile?.SalaryBasis?.CurrencyCode ?? profile?.CurrencyCode,
            Issue = IssueFor(employee.IsOnPayroll, profile != null, profile?.PayrollActive, hrBasic, payrollBasic)?.ToString(),
        };
    }

    public async Task<PayrollReconciliationDto> GetReconciliationAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var today = DateTime.UtcNow.Date;

        // Live staff only. A terminated employee with a still-active payroll profile is a real
        // problem, but it is separation's (area 9b) and that area already reports it.
        var employees = await _unitOfWork.Repository<Employee>().GetQueryable()
            .Include(e => e.Position)
            .Include(e => e.OrganizationUnit)
            .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.IsActive)
            .OrderBy(e => e.EmployeeNumber)
            .ToListAsync(cancellationToken);

        var profiles = await Profiles(tenantId)
            .Include(p => p.SalaryBasis)
            .ToListAsync(cancellationToken);
        var profileByEmployee = profiles
            .GroupBy(p => p.EmployeeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.PayrollActive).First());

        var assignmentByEmployee = (await ActiveAssignments(tenantId, today).ToListAsync(cancellationToken))
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.EffectiveDate).First());

        var result = new PayrollReconciliationDto { GeneratedAt = DateTime.UtcNow };
        foreach (var e in employees)
        {
            if (e.IsOnPayroll) result.OnPayrollCount++; else result.OffPayrollCount++;

            profileByEmployee.TryGetValue(e.Id, out var profile);
            assignmentByEmployee.TryGetValue(e.Id, out var assignment);
            var hrBasic = e.IsOnPayroll ? HrBasicPay(e, assignment) : null;
            var payrollBasic = profile?.SalaryBasis is { IsActive: true } basis ? basis.MonthlyBasicSalary : (decimal?)null;

            var issue = IssueFor(e.IsOnPayroll, profile != null, profile?.PayrollActive, hrBasic, payrollBasic);
            if (issue == null) continue;

            switch (issue.Value)
            {
                case PayrollReconciliationIssue.AwaitingPayrollSetup: result.AwaitingPayrollSetup++; break;
                case PayrollReconciliationIssue.InactiveInPayroll: result.InactiveInPayroll++; break;
                case PayrollReconciliationIssue.StillActiveInPayroll: result.StillActiveInPayroll++; break;
                case PayrollReconciliationIssue.NoPayBasis: result.NoPayBasis++; break;
            }

            result.Rows.Add(new PayrollReconciliationRowDto
            {
                EmployeeId = e.Id,
                EmployeeNumber = e.EmployeeNumber,
                FullName = e.FullName,
                PositionTitle = e.Position?.Title,
                OrganizationUnitName = e.OrganizationUnit?.Name,
                EmploymentType = e.EmploymentType,
                StaffStatus = e.StaffStatus,
                IsOnPayroll = e.IsOnPayroll,
                OffPayrollReason = e.OffPayrollReason,
                HasPayrollProfile = profile != null,
                PayrollActive = profile?.PayrollActive,
                HrMonthlyBasicPay = hrBasic,
                Issue = issue.Value,
            });
        }

        // The ones the payroll owner must act on first: people HR says are off payroll whom the run
        // will still pay, then people waiting to be set up, then the rest.
        result.Rows = result.Rows
            .OrderBy(r => r.Issue == PayrollReconciliationIssue.StillActiveInPayroll ? 0
                        : r.Issue == PayrollReconciliationIssue.AwaitingPayrollSetup ? 1
                        : r.Issue == PayrollReconciliationIssue.InactiveInPayroll ? 2 : 3)
            .ThenBy(r => r.EmployeeNumber)
            .ToList();
        return result;
    }

    public async Task<(bool HasProfile, bool PayrollActive)> GetPayrollProfileStateAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var profile = await Profiles(tenantId)
            .Select(p => new { p.EmployeeId, p.PayrollActive })
            .FirstOrDefaultAsync(p => p.EmployeeId == employeeId, cancellationToken);
        return (profile != null, profile?.PayrollActive ?? false);
    }

    public async Task<bool> EnsurePayrollProfileAsync(Employee employee, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employee);
        if (!employee.IsOnPayroll) return false;

        var tenantId = employee.TenantId != Guid.Empty ? employee.TenantId : GetTenantId();
        if (await Profiles(tenantId).AnyAsync(p => p.EmployeeId == employee.Id, cancellationToken))
            return false; // payroll already knows them — theirs to maintain from here

        // ⚠ Create-only, through payroll's published upsert. Everything mapped here is what HR's
        // employee record already says; payroll's own screen is where it is refined afterwards.
        // No payment methods are sent: payroll seeds its own default (Bank, 100%) for a new profile.
        var dto = new UpsertPayrollEmployeeProfileDto
        {
            EmployeeId = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            PayrollActive = true,
            PayTax = employee.PayTax,
            SsfApplicable = employee.SSFund,
            GrossUp = employee.GrossUp,
            Tier2Only = employee.Tier2Only,
            OvertimeEligible = employee.Overtime,
            SsfNumber = employee.SocialSecurityNumber,
            TinNumber = employee.TINNumber,
            CurrencyCode = "GHS",
            SalaryBasis = employee.Salary is > 0 ? new UpsertPayrollSalaryBasisDto
            {
                MonthlyBasicSalary = employee.Salary.Value,
                AnnualBasicSalary = employee.Salary.Value * 12,
                CurrencyCode = "GHS",
                // From the day they joined, not the day HR pressed save — a basis dated later than
                // the first pay period would leave that period with nothing to pay.
                EffectiveFrom = employee.DateEmployed?.ToDateTime(TimeOnly.MinValue) ?? DateTime.UtcNow.Date,
                IsActive = true,
            } : null,
        };

        try
        {
            await _payroll.UpsertEmployeeProfileAsync(tenantId, dto, cancellationToken);
            _logger.LogInformation(
                "Payroll profile created for {EmployeeNumber} ({EmployeeId}) from HR's on-payroll flag.",
                employee.EmployeeNumber, employee.Id);
            return true;
        }
        catch (Exception ex)
        {
            // ⚠ Cross-module defect #23: payroll's upsert cannot create a NEW profile at all — it
            // points the profile at its default payment method in the same insert, and the FK
            // fires (proven 2026-09-02, with and without a supplied method). Until the payroll
            // owner fixes it, HR writes the same minimal shape itself, in two saves so the cycle
            // never forms. Logged loudly every time so the workaround cannot quietly outlive the bug.
            _logger.LogWarning(ex,
                "Payroll's employee-profile upsert failed for {EmployeeNumber} ({EmployeeId}); falling back to a direct minimal profile (cross-module defect #23).",
                employee.EmployeeNumber, employee.Id);
        }

        try
        {
            // The failed upsert leaves its half-built graph in the scoped change tracker in the
            // Added state; a later SaveChanges would retry it and fail the same way. Detach it.
            _unitOfWork.ClearTrackedChanges();
            await CreateMinimalProfileDirectlyAsync(tenantId, employee, dto, cancellationToken);
            _logger.LogInformation(
                "Payroll profile created directly for {EmployeeNumber} ({EmployeeId}) from HR's on-payroll flag (defect #23 fallback).",
                employee.EmployeeNumber, employee.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not enrol {EmployeeNumber} ({EmployeeId}) in payroll; the reconciliation view will list them as awaiting setup.",
                employee.EmployeeNumber, employee.Id);
            return false;
        }
    }

    /// <summary>
    /// The rows payroll's upsert would have produced for a new profile — profile, salary basis, one
    /// 100 % bank payment method — written in two saves: the default-method pointer is set only
    /// after both rows exist. Nothing else about payroll is touched.
    /// </summary>
    private async Task CreateMinimalProfileDirectlyAsync(
        Guid tenantId, Employee employee, UpsertPayrollEmployeeProfileDto dto, CancellationToken cancellationToken)
    {
        var profiles = _unitOfWork.Repository<PayrollEmployeeProfile>();
        var methods = _unitOfWork.Repository<PayrollPaymentMethod>();
        var bases = _unitOfWork.Repository<PayrollSalaryBasis>();

        // The failed upsert may have left a half-tracked graph behind on this scoped context;
        // if the profile row did land, do not add a second one.
        if (await profiles.GetQueryable().AnyAsync(
                p => p.TenantId == tenantId && p.EmployeeId == employee.Id && !p.IsDeleted, cancellationToken))
            return;

        var profile = new PayrollEmployeeProfile
        {
            TenantId = tenantId,
            EmployeeId = employee.Id,
            EmployeeNumber = dto.EmployeeNumber,
            PayrollActive = true,
            PayTax = dto.PayTax,
            SsfApplicable = dto.SsfApplicable,
            GrossUp = dto.GrossUp,
            Tier2Only = dto.Tier2Only,
            OvertimeEligible = dto.OvertimeEligible,
            SsfNumber = dto.SsfNumber,
            TinNumber = dto.TinNumber,
            CurrencyCode = dto.CurrencyCode,
        };
        await profiles.AddAsync(profile);

        var method = new PayrollPaymentMethod
        {
            TenantId = tenantId,
            EmployeeProfileId = profile.Id,
            PaymentType = "Bank",
            PaymentMode = "Percentage",
            PaymentPercent = 100m,
            CurrencyCode = dto.CurrencyCode,
            ExchangeRate = 1m,
            SequenceNo = 1,
            IsActive = true,
        };
        await methods.AddAsync(method);

        if (dto.SalaryBasis is { } basis)
        {
            await bases.AddAsync(new PayrollSalaryBasis
            {
                TenantId = tenantId,
                EmployeeProfileId = profile.Id,
                MonthlyBasicSalary = basis.MonthlyBasicSalary,
                AnnualBasicSalary = basis.AnnualBasicSalary,
                HourlyRate = basis.HourlyRate,
                CurrencyCode = basis.CurrencyCode,
                EffectiveFrom = basis.EffectiveFrom,
                EffectiveTo = basis.EffectiveTo,
                IsActive = basis.IsActive,
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Second save: the pointer, now that both ends exist.
        profile.DefaultPaymentMethodId = method.Id;
        await profiles.UpdateAsync(profile);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The HR-side basic pay, the same way EmolumentService resolves it: the current notch's amount,
    /// else the level's mid-point, else the flat figure on the record.
    /// </summary>
    private static decimal? HrBasicPay(Employee employee, EmployeeSalaryAssignment? assignment)
        => assignment?.Notch?.SalaryAmount ?? assignment?.Level?.MidSalary ?? employee.Salary;

    private static PayrollReconciliationIssue? IssueFor(
        bool isOnPayroll, bool hasProfile, bool? payrollActive, decimal? hrBasic, decimal? payrollBasic)
    {
        if (isOnPayroll)
        {
            if (!hasProfile) return PayrollReconciliationIssue.AwaitingPayrollSetup;
            if (payrollActive == false) return PayrollReconciliationIssue.InactiveInPayroll;
            // A run skips a zero basis silently (PayrollService: "if (originalBasicSalary <= 0) continue"),
            // which is exactly the case nobody would notice until payday.
            if ((hrBasic ?? 0m) <= 0m && (payrollBasic ?? 0m) <= 0m) return PayrollReconciliationIssue.NoPayBasis;
            return null;
        }

        return hasProfile && payrollActive == true
            ? PayrollReconciliationIssue.StillActiveInPayroll
            : null;
    }
}
