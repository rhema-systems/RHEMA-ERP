using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.DTOs.HR.Payroll;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Payroll;
using ErpSystem.Core.Enums;
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
            .Include(a => a.Grade)
            // ⚠ WithdrawnAt is part of the predicate, not an afterthought: a placement withdrawn
            // before its start date has no window to exclude it, so this is the only thing keeping
            // it out. See EmployeeSalaryAssignment.WithdrawnAt.
            .Where(a => a.TenantId == tenantId && !a.IsDeleted
                     && a.WithdrawnAt == null
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

        var payrollBasic = profile?.SalaryBasis is { IsActive: true } basis ? basis.MonthlyBasicSalary : (decimal?)null;
        var (hrBasic, source) = HrBasicPay.Resolve(employee, assignment, payrollBasic);

        return new EmployeePayrollStatusDto
        {
            EmployeeId = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            IsOnPayroll = employee.IsOnPayroll,
            OffPayrollReason = employee.OffPayrollReason,
            OffPayrollNote = employee.OffPayrollNote,
            PayBasis = employee.PayBasis,
            PayBasisNote = employee.PayBasisNote,
            HrMonthlyBasicPay = hrBasic,
            HrBasicPaySource = source,
            HasActiveSalaryAssignment = assignment != null,
            HasPayrollProfile = profile != null,
            PayrollActive = profile?.PayrollActive,
            PayrollMonthlyBasicSalary = payrollBasic,
            PayrollCurrencyCode = profile?.SalaryBasis?.CurrencyCode ?? profile?.CurrencyCode,
            Issue = IssueFor(employee, profile != null, profile?.PayrollActive, assignment, hrBasic, payrollBasic)?.ToString(),
        };
    }

    /// <inheritdoc />
    public async Task<PayrollEmployeeProfileDto?> GetPayrollProfileAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var employee = await _unitOfWork.Repository<Employee>().GetQueryable()
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.TenantId == tenantId && !e.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        // The staff number as the search term: payroll's filter is a Contains over number and
        // name, so the term narrows the list to this person and whoever shares a substring of
        // their number — then the id picks the one. Never the bare list, which stops at 250.
        var matches = await _payroll.GetEmployeeProfilesAsync(tenantId, employee.EmployeeNumber, cancellationToken);
        return matches.FirstOrDefault(p => p.EmployeeId == employeeId);
    }

    /// <inheritdoc />
    public async Task<PayrollBasicWriteResult> UpdateMonthlyBasicAsync(
        Guid employeeId, decimal monthlyBasic, string? currencyCode, DateTime effectiveFrom, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        PayrollEmployeeProfileDto? profile;
        try { profile = await GetPayrollProfileAsync(employeeId, cancellationToken); }
        catch (Exception ex) { return PayrollBasicWriteResult.Failed($"Payroll's profile could not be read: {ex.Message}"); }
        if (profile == null)
            return PayrollBasicWriteResult.Failed("Payroll has no profile for this employee yet; create it on the Salary tab, then apply again.");

        var beforeMethods = profile.PaymentMethods.Select(m => m.Id).OrderBy(x => x).ToList();
        var currency = string.IsNullOrWhiteSpace(currencyCode)
            ? (profile.SalaryBasis?.CurrencyCode ?? profile.CurrencyCode)
            : currencyCode.Trim().ToUpperInvariant();

        var dto = new UpsertPayrollEmployeeProfileDto
        {
            Id = profile.Id,
            EmployeeId = profile.EmployeeId,
            EmployeeNumber = profile.EmployeeNumber,
            LegacyEmployeeId = profile.LegacyEmployeeId,
            LegacyEmployeeNumber = profile.LegacyEmployeeNumber,
            PayrollActive = profile.PayrollActive,
            PayTax = profile.PayTax,
            SsfApplicable = profile.SsfApplicable,
            GrossUp = profile.GrossUp,
            Tier2Only = profile.Tier2Only,
            OvertimeEligible = profile.OvertimeEligible,
            SsfNumber = profile.SsfNumber,
            TinNumber = profile.TinNumber,
            CurrencyCode = profile.CurrencyCode,
            SalaryBasis = new UpsertPayrollSalaryBasisDto
            {
                Id = profile.SalaryBasis?.Id,
                MonthlyBasicSalary = monthlyBasic,
                AnnualBasicSalary = monthlyBasic * 12,
                HourlyRate = profile.SalaryBasis?.HourlyRate,
                CurrencyCode = currency,
                EffectiveFrom = effectiveFrom.Date,
                IsActive = true,
            },
            // Carried through unchanged, ids included: the upsert replaces the list it is given.
            PaymentMethods = profile.PaymentMethods.Select(m => new UpsertPayrollPaymentMethodDto
            {
                Id = m.Id, PaymentType = m.PaymentType, PaymentMode = m.PaymentMode, PaymentPercent = m.PaymentPercent,
                Amount = m.Amount, BankCode = m.BankCode, BankBranchCode = m.BankBranchCode, AccountNumber = m.AccountNumber,
                ChequeNumber = m.ChequeNumber, ChequeBankCode = m.ChequeBankCode, CurrencyCode = m.CurrencyCode,
                ExchangeRate = m.ExchangeRate, SequenceNo = m.SequenceNo, StartDate = m.StartDate, EndDate = m.EndDate,
                IsActive = m.IsActive,
            }).ToList(),
            EmployeeComponents = profile.EmployeeComponents.Select(c => new UpsertPayrollEmployeeComponentDto
            {
                Id = c.Id, PayrollComponentId = c.PayrollComponentId, CalculationTypeOverride = c.CalculationTypeOverride,
                AmountOverride = c.AmountOverride, RateOverride = c.RateOverride, TaxableOverride = c.TaxableOverride,
                TaxFreeCeilingOverride = c.TaxFreeCeilingOverride, EmployerAmountOverride = c.EmployerAmountOverride,
                EmployerTaxableOverride = c.EmployerTaxableOverride, GrossUpOverride = c.GrossUpOverride,
                CurrencyCodeOverride = c.CurrencyCodeOverride, Applicable = c.Applicable,
                EffectiveFrom = c.EffectiveFrom, EffectiveTo = c.EffectiveTo,
            }).ToList(),
        };

        try
        {
            await _payroll.UpsertEmployeeProfileAsync(tenantId, dto, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Payroll's upsert refused the approved monthly basic for {EmployeeId}.", employeeId);
            return PayrollBasicWriteResult.Failed($"Payroll refused the change: {ex.Message}");
        }

        PayrollEmployeeProfileDto? after;
        try { after = await GetPayrollProfileAsync(employeeId, cancellationToken); }
        catch (Exception ex) { return PayrollBasicWriteResult.Failed($"Written, but payroll's profile could not be re-read: {ex.Message}"); }

        var afterMethods = after?.PaymentMethods.Select(m => m.Id).OrderBy(x => x).ToList() ?? new List<Guid>();
        if (!beforeMethods.SequenceEqual(afterMethods))
        {
            _logger.LogError(
                "Payroll's upsert changed the payment methods of {EmployeeId} on a basis-only round trip ({Before} → {After}). Report to the payroll owner.",
                employeeId, beforeMethods.Count, afterMethods.Count);
            return PayrollBasicWriteResult.Failed(
                $"Payroll's payment methods changed on the round trip ({beforeMethods.Count} → {afterMethods.Count}); check the profile in payroll before applying again.");
        }
        if (after?.SalaryBasis == null || after.SalaryBasis.MonthlyBasicSalary != monthlyBasic)
            return PayrollBasicWriteResult.Failed(
                $"Payroll answered without refusing, but its basis reads {after?.SalaryBasis?.MonthlyBasicSalary.ToString() ?? "nothing"} rather than {monthlyBasic}.");

        _logger.LogInformation("Payroll monthly basic for {EmployeeId} set to {Amount} {Currency} from {From:yyyy-MM-dd} (approved salary change).",
            employeeId, monthlyBasic, currency, effectiveFrom);
        return PayrollBasicWriteResult.Ok();
    }

    /// <inheritdoc />
    public async Task<(decimal? MonthlyBasicPay, string Source)> ResolveMonthlyBasicPayAsync(
        Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var employee = await _unitOfWork.Repository<Employee>().GetQueryable()
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.TenantId == tenantId && !e.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        if (!employee.IsOnPayroll)
            return (null, "Not on payroll: no basic pay is on record in HR.");

        var today = DateTime.UtcNow.Date;
        var assignment = await ActiveAssignments(tenantId, today)
            .Where(a => a.EmployeeId == employeeId)
            .OrderByDescending(a => a.EffectiveDate)
            .FirstOrDefaultAsync(cancellationToken);
        var profile = await Profiles(tenantId)
            .Include(p => p.SalaryBasis)
            .FirstOrDefaultAsync(p => p.EmployeeId == employeeId, cancellationToken);
        var payrollBasic = profile?.SalaryBasis is { IsActive: true } basis ? basis.MonthlyBasicSalary : (decimal?)null;

        return HrBasicPay.Resolve(employee, assignment, payrollBasic);
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
            var payrollBasic = profile?.SalaryBasis is { IsActive: true } basis ? basis.MonthlyBasicSalary : (decimal?)null;
            var hrBasic = HrBasicPay.Resolve(e, assignment, payrollBasic).Amount;

            var issue = IssueFor(e, profile != null, profile?.PayrollActive, assignment, hrBasic, payrollBasic);
            if (issue == null) continue;

            switch (issue.Value)
            {
                case PayrollReconciliationIssue.AwaitingPayrollSetup: result.AwaitingPayrollSetup++; break;
                case PayrollReconciliationIssue.InactiveInPayroll: result.InactiveInPayroll++; break;
                case PayrollReconciliationIssue.StillActiveInPayroll: result.StillActiveInPayroll++; break;
                case PayrollReconciliationIssue.NoPayBasis: result.NoPayBasis++; break;
                case PayrollReconciliationIssue.BasicPayMismatch: result.BasicPayMismatch++; break;
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
                PayBasis = e.PayBasis,
                HrMonthlyBasicPay = hrBasic,
                PayrollMonthlyBasicSalary = payrollBasic,
                Issue = issue.Value,
            });
        }

        // The ones the payroll owner must act on first: people HR says are off payroll whom the run
        // will still pay, then people waiting to be set up, then the rest.
        result.Rows = result.Rows
            // A mismatch pays somebody the wrong amount every month until it is seen, so it sits
            // above "nothing to pay from", which at least pays nothing.
            .OrderBy(r => r.Issue == PayrollReconciliationIssue.StillActiveInPayroll ? 0
                        : r.Issue == PayrollReconciliationIssue.AwaitingPayrollSetup ? 1
                        : r.Issue == PayrollReconciliationIssue.InactiveInPayroll ? 2
                        : r.Issue == PayrollReconciliationIssue.BasicPayMismatch ? 3 : 4)
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

    private static PayrollReconciliationIssue? IssueFor(
        Employee employee, bool hasProfile, bool? payrollActive,
        EmployeeSalaryAssignment? assignment, decimal? hrBasic, decimal? payrollBasic)
    {
        if (employee.IsOnPayroll)
        {
            if (!hasProfile) return PayrollReconciliationIssue.AwaitingPayrollSetup;
            if (payrollActive == false) return PayrollReconciliationIssue.InactiveInPayroll;
            // A run skips a zero basis silently (PayrollService: "if (originalBasicSalary <= 0) continue"),
            // which is exactly the case nobody would notice until payday.
            if ((hrBasic ?? 0m) <= 0m && (payrollBasic ?? 0m) <= 0m) return PayrollReconciliationIssue.NoPayBasis;

            // ⚠ Scale only, and only from a NOTCH. A level mid-point is an estimate, not a placed
            // figure, and a flat record figure is what payroll was seeded from — neither is a claim
            // that payroll is wrong. A notch is: somebody placed this person there on purpose.
            if (employee.PayBasis == PayBasis.SalaryScale
                && assignment?.Notch?.SalaryAmount is { } placed
                && payrollBasic is > 0m
                && placed != payrollBasic.Value)
                return PayrollReconciliationIssue.BasicPayMismatch;

            return null;
        }

        return hasProfile && payrollActive == true
            ? PayrollReconciliationIssue.StillActiveInPayroll
            : null;
    }
}
