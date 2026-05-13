using ErpSystem.Core.DTOs.HR.Payroll;
using ErpSystem.Core.Entities;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Payroll;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using WebEmailAttachment = ErpSystem.Web.Services.EmailAttachment;
using WebEmailService = ErpSystem.Web.Services.IEmailService;

namespace ErpSystem.Api.Services.HR;

public class PayrollService : IPayrollService
{
    private const string BasicSalaryTransactionType = "BasicSalary";
    private const string EmployeePensionTransactionType = "EmployeePension";
    private const string EmployerPensionTransactionType = "EmployerPension";
    private const string IncomeTaxTransactionType = "IncomeTax";
    private const string LoanRepaymentTransactionType = "REP";
    private const string LoanInterestTransactionType = "INT";
    private const string SalaryAdvanceTransactionType = "ADV";
    private const string TaxReliefTransactionType = "REF";
    private const string BonusTransactionType = "BON";
    private const string OvertimeTransactionType = "OVE";
    private const string AbsenceTransactionType = "ABC";
    private const string PromotionBasicArrearsTransactionType = "BAR";
    private const string PromotionAllowanceArrearsTransactionType = "ALR";
    private const string PromotionEmployeeContributionArrearsTransactionType = "ESR";
    private const string PromotionEmployerContributionArrearsTransactionType = "CSR";
    private const string PromotionContributionArrearsTransactionType = "COR";
    private const string NetPayTransactionType = "NetPay";

    private static readonly IReadOnlyList<PayrollLegacyMenuItemDto> LegacyMenuItems =
    [
        Menu("A000", null, "Main Menu", "Main Menu", "Folder", null, "001", 0),
        Menu("A00001", "A000", "Setup", "Main Menu > Setup", "Folder", null, "001", 1),
        Menu("A0000101", "A00001", "Quick Company Creation", "Main Menu > Setup > Quick Company Creation", "Form", "GE3_015.fmb", "001", 1),
        Menu("A0000102", "A00001", "Codes Description", "Main Menu > Setup > Codes Description", "Form", "GE3_004.fmb", "001", 2, true),
        Menu("A0000103", "A00001", "Parameters", "Main Menu > Setup > Parameters", "Form", "GE3_007.fmb", "001", 3, true),
        Menu("A0000104", "A00001", "Holiday Controls", "Main Menu > Setup > Holiday Controls", "Form", "GE3_005.fmb", "001", 4, true),
        Menu("A0000105", "A00001", "Exchange Rate Setup Screen", "Main Menu > Setup > Exchange Rate Setup Screen", "Form", "GE3_012.fmb", "001", 5, true),
        Menu("A0000106", "A00001", "Bank Branch Setup", "Main Menu > Setup > Bank Branch Setup", "Form", "GE3_010.fmb", "001", 6, true),
        Menu("A0000107", "A00001", "Loan Setup", "Main Menu > Setup > Loan Setup", "Form", "PR3_003.fmb", "001", 7, true),
        Menu("A0000108", "A00001", "Leave Setup", "Main Menu > Setup > Leave Setup", "Form", "PR3_014.fmb", "001", 8, true),
        Menu("A0000109", "A00001", "Overtime Setup", "Main Menu > Setup > Overtime Setup", "Form", "PR3_002.fmb", "001", 9, true),
        Menu("A0000114", "A00001", "Grades Setup", "Main Menu > Setup > Grades Setup", "Form", "PR3_007.fmb", "001", 14, true),
        Menu("A0000115", "A00001", "Tax Table", "Main Menu > Setup > Tax Table", "Form", "PR3_004.fmb", "001", 15, true),
        Menu("A0000116", "A00001", "Tax Relief Setup", "Main Menu > Setup > Tax Relief Setup", "Form", "PR3_017.fmb", "001", 16),
        Menu("A0000117", "A00001", "Allowances & Deductions Setup", "Main Menu > Setup > Allowances & Deductions Setup", "Form", "PR3_009.fmb", "001", 17),
        Menu("A0000118", "A00001", "Bonus Setup", "Main Menu > Setup > Bonus Setup", "Form", "PR3_022.fmb", "001", 18),
        Menu("A0000119", "A00001", "Backpay / Salary Increase", "Main Menu > Setup > Backpay / Salary Increase", "Form", "PR3_023.fmb", "001", 19, true),
        Menu("A00002", "A000", "Payroll Process", "Main Menu > Payroll Process", "Folder", null, "001", 2),
        Menu("A0000212", "A00002", "Allowances & Ded Exception", "Main Menu > Payroll Process > Allowances & Ded Exception", "Form", "PR3_021.fmb", "001", 17, true),
        Menu("A0000215", "A00002", "Overtime Summary", "Main Menu > Payroll Process > Overtime Summary", "Form", "PR3_016.fmb", "001", 21, true),
        Menu("A0000217", "A00002", "Opening Balance", "Main Menu > Payroll Process > Opening Balance", "Form", "PR3_024.fmb", "BUCK", 23, true),
        Menu("A0000221", "A00002", "Contribution Transactions", "Main Menu > Payroll Process > Contribution Transactions", "Form", "PR3_030.fmb", "DEMO", 24, true),
        Menu("A0000225", "A00002", "Promotion Arrears Entry", "Main Menu > Payroll Process > Promotion Arrears Entry", "Form", "PR3_033.fmb", "001", 25, true)
    ];

    private readonly ApplicationDbContext _context;
    private readonly IJournalEntryService _journalEntryService;
    private readonly WebEmailService _emailService;
    private readonly ILogger<PayrollService> _logger;

    public PayrollService(
        ApplicationDbContext context,
        IJournalEntryService journalEntryService,
        WebEmailService emailService,
        ILogger<PayrollService> logger)
    {
        _context = context;
        _journalEntryService = journalEntryService;
        _emailService = emailService;
        _logger = logger;
    }

    public Task<IReadOnlyList<PayrollLegacyMenuItemDto>> GetLegacyMenuAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(LegacyMenuItems);

    public async Task<PayrollSetupSummaryDto> GetSetupSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var parameters = await _context.PayrollParameterSets
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.IsActive)
            .ThenBy(e => e.Code)
            .FirstOrDefaultAsync(cancellationToken);

        var components = await _context.PayrollComponents
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.ComponentType)
            .ThenBy(e => e.Code)
            .ToListAsync(cancellationToken);

        var componentRules = await _context.PayrollComponentRules
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.ComponentType)
            .ThenBy(e => e.ComponentCode)
            .ThenBy(e => e.Category)
            .ToListAsync(cancellationToken);

        var taxBands = await _context.PayrollTaxBands
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.TaxType)
            .ThenBy(e => e.SerialNo)
            .ToListAsync(cancellationToken);

        var taxReliefs = await _context.PayrollTaxReliefs
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.Code)
            .ToListAsync(cancellationToken);

        var pensionSchemes = await _context.PayrollPensionSchemes
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.IsDefault)
            .ThenBy(e => e.Code)
            .ToListAsync(cancellationToken);

        var overtimePolicies = await _context.PayrollOvertimePolicies
            .AsNoTracking()
            .Include(e => e.Ranges)
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.IsDefault)
            .ThenBy(e => e.Code)
            .ToListAsync(cancellationToken);

        var loanPolicies = await _context.PayrollLoanPolicies
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.Code)
            .ToListAsync(cancellationToken);

        var bonusPolicies = await _context.PayrollBonusPolicies
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.Code)
            .ToListAsync(cancellationToken);

        var bonusRules = await _context.PayrollBonusRules
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.BonusCode)
            .ThenBy(e => e.GroupCode)
            .ToListAsync(cancellationToken);

        var bonusExceptions = await _context.PayrollBonusExceptions
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.BonusCode)
            .ThenBy(e => e.EmployeeNumber)
            .ToListAsync(cancellationToken);

        var backpayPolicies = await _context.PayrollBackpayPolicies
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.OperationType)
            .ToListAsync(cancellationToken);

        var backpayRules = await _context.PayrollBackpayRules
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.OperationType)
            .ThenBy(e => e.CategoryType)
            .ThenBy(e => e.CategoryCode)
            .ToListAsync(cancellationToken);

        var backpayExceptions = await _context.PayrollBackpayExceptions
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.OperationType)
            .ThenBy(e => e.EmployeeNumber)
            .ToListAsync(cancellationToken);

        var journalMappings = await _context.PayrollJournalMappings
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.TransactionType)
            .ThenBy(e => e.ComponentCode)
            .ToListAsync(cancellationToken);

        var codeTypes = await _context.PayrollCodeTypes
            .AsNoTracking()
            .Include(e => e.Values)
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.CodeType)
            .ToListAsync(cancellationToken);

        var holidayCount = await _context.PayrollHolidays
            .AsNoTracking()
            .CountAsync(e => e.TenantId == tenantId, cancellationToken);

        var nonWorkingDayCount = await _context.PayrollNonWorkingDays
            .AsNoTracking()
            .CountAsync(e => e.TenantId == tenantId, cancellationToken);

        var exchangeRateCount = await _context.PayrollExchangeRates
            .AsNoTracking()
            .CountAsync(e => e.TenantId == tenantId, cancellationToken);

        var bankBranches = await _context.PayrollBankBranches
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.BankCode)
            .ThenBy(e => e.BranchCode)
            .ToListAsync(cancellationToken);

        var leaveSetups = await _context.PayrollLeaveSetups
            .AsNoTracking()
            .Include(e => e.Details)
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.Category)
            .ThenBy(e => e.CategoryDetail)
            .ToListAsync(cancellationToken);

        var overtimeRangeCount = await _context.PayrollOvertimeRanges
            .AsNoTracking()
            .CountAsync(e => e.TenantId == tenantId, cancellationToken);

        var legacyMenuUsers = await _context.PayrollLegacyMenuUsers
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.UserName)
            .ToListAsync(cancellationToken);

        var menuSecurity = await _context.PayrollLegacyMenuSecurity
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.FormCode)
            .ThenBy(e => e.UserName)
            .ThenBy(e => e.GroupName)
            .ToListAsync(cancellationToken);

        var companyProfiles = await _context.PayrollCompanyProfiles
            .AsNoTracking()
            .Include(e => e.BusinessUnits)
            .Include(e => e.Bankers)
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.CompanyId)
            .ToListAsync(cancellationToken);

        var businessUnitCount = await _context.PayrollBusinessUnits
            .AsNoTracking()
            .CountAsync(e => e.TenantId == tenantId, cancellationToken);

        var companyBankerCount = await _context.PayrollCompanyBankers
            .AsNoTracking()
            .CountAsync(e => e.TenantId == tenantId, cancellationToken);

        var grades = await _context.PayrollGrades
            .AsNoTracking()
            .Include(e => e.Notches)
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.OrderField)
            .ThenBy(e => e.GradeName)
            .ToListAsync(cancellationToken);

        var gradeNotchCount = await _context.PayrollGradeNotches
            .AsNoTracking()
            .CountAsync(e => e.TenantId == tenantId, cancellationToken);

        return new PayrollSetupSummaryDto
        {
            ActiveParameters = parameters == null ? null : ToDto(parameters),
            ComponentCount = components.Count,
            ComponentRuleCount = componentRules.Count,
            CodeTypeCount = codeTypes.Count,
            HolidayCount = holidayCount,
            NonWorkingDayCount = nonWorkingDayCount,
            ExchangeRateCount = exchangeRateCount,
            BankBranchCount = bankBranches.Count,
            LeaveSetupCount = leaveSetups.Count,
            OvertimeRangeCount = overtimeRangeCount,
            LegacyMenuUserCount = legacyMenuUsers.Count,
            MenuSecurityCount = menuSecurity.Count,
            CompanyProfileCount = companyProfiles.Count,
            BusinessUnitCount = businessUnitCount,
            CompanyBankerCount = companyBankerCount,
            GradeCount = grades.Count,
            GradeNotchCount = gradeNotchCount,
            TaxBandCount = taxBands.Count,
            TaxReliefCount = taxReliefs.Count,
            PensionSchemeCount = pensionSchemes.Count,
            OvertimePolicyCount = overtimePolicies.Count,
            LoanPolicyCount = loanPolicies.Count,
            BonusPolicyCount = bonusPolicies.Count,
            BonusRuleCount = bonusRules.Count,
            BonusExceptionCount = bonusExceptions.Count,
            BackpayPolicyCount = backpayPolicies.Count,
            BackpayRuleCount = backpayRules.Count,
            BackpayExceptionCount = backpayExceptions.Count,
            JournalMappingCount = journalMappings.Count,
            Components = components.Select(ToDto).ToList(),
            ComponentRules = componentRules.Select(ToDto).ToList(),
            TaxBands = taxBands.Select(ToDto).ToList(),
            TaxReliefs = taxReliefs.Select(ToDto).ToList(),
            PensionSchemes = pensionSchemes.Select(ToDto).ToList(),
            OvertimePolicies = overtimePolicies.Select(ToDto).ToList(),
            LoanPolicies = loanPolicies.Select(ToDto).ToList(),
            BonusPolicies = bonusPolicies.Select(ToDto).ToList(),
            BonusRules = bonusRules.Select(ToDto).ToList(),
            BonusExceptions = bonusExceptions.Select(ToDto).ToList(),
            BackpayPolicies = backpayPolicies.Select(ToDto).ToList(),
            BackpayRules = backpayRules.Select(ToDto).ToList(),
            BackpayExceptions = backpayExceptions.Select(ToDto).ToList(),
            JournalMappings = journalMappings.Select(ToDto).ToList(),
            CodeTypes = codeTypes.Select(ToDto).ToList(),
            BankBranches = bankBranches.Select(ToDto).ToList(),
            LeaveSetups = leaveSetups.Select(ToDto).ToList(),
            LegacyMenuUsers = legacyMenuUsers.Select(ToDto).ToList(),
            MenuSecurity = menuSecurity.Select(ToDto).ToList(),
            CompanyProfiles = companyProfiles.Select(ToDto).ToList(),
            Grades = grades.Select(ToDto).ToList()
        };
    }

    public async Task<PayrollCodeSetupDto> GetCodeSetupAsync(Guid tenantId, string? codeType = null, CancellationToken cancellationToken = default)
    {
        var codeTypes = await _context.PayrollCodeTypes
            .AsNoTracking()
            .Include(e => e.Values)
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.CodeType)
            .ToListAsync(cancellationToken);

        string? selectedCodeType;
        if (string.IsNullOrWhiteSpace(codeType))
        {
            selectedCodeType = codeTypes.FirstOrDefault()?.CodeType;
        }
        else
        {
            var requestedCodeType = codeType.Trim().ToUpperInvariant();
            selectedCodeType = requestedCodeType.Length > 5 ? null : NormalizeLegacyCode(requestedCodeType, nameof(codeType), 5);
        }

        List<PayrollCodeValue> codeValues = string.IsNullOrWhiteSpace(selectedCodeType)
            ? []
            : await _context.PayrollCodeValues
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId && e.CodeType == selectedCodeType)
                .OrderBy(e => e.ActualCode)
                .ToListAsync(cancellationToken);

        return new PayrollCodeSetupDto
        {
            SelectedCodeType = selectedCodeType,
            CodeTypes = codeTypes.Select(ToDto).ToList(),
            CodeValues = codeValues.Select(ToDto).ToList()
        };
    }

    public async Task<PayrollCodeTypeDto> UpsertCodeTypeAsync(Guid tenantId, UpsertPayrollCodeTypeDto dto, CancellationToken cancellationToken = default)
    {
        var codeType = NormalizeLegacyCode(dto.CodeType, nameof(dto.CodeType), 5);
        var legacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode, 5);
        var entity = await FindForUpsertAsync(_context.PayrollCodeTypes, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollCodeTypes
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.CodeType == codeType && e.LegacyCompanyCode == legacyCompanyCode, cancellationToken)
            ?? new PayrollCodeType { TenantId = tenantId };

        entity.CodeType = codeType;
        entity.Description = RequireText(dto.Description, nameof(dto.Description));
        entity.AccountCode = TrimOrNull(dto.AccountCode);
        entity.Blocked = dto.Blocked;
        entity.Dependent = dto.Dependent;
        entity.DependentOnCodeType = NormalizeOptionalLegacyCode(dto.DependentOnCodeType, 5);
        entity.LegacyCompanyCode = legacyCompanyCode;

        AddIfNew(_context.PayrollCodeTypes, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollCodeValueDto> UpsertCodeValueAsync(Guid tenantId, UpsertPayrollCodeValueDto dto, CancellationToken cancellationToken = default)
    {
        var codeType = NormalizeLegacyCode(dto.CodeType, nameof(dto.CodeType), 5);
        var actualCode = NormalizeLegacyCode(dto.ActualCode, nameof(dto.ActualCode), 15);
        var legacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode, 5);

        var parent = dto.PayrollCodeTypeId == Guid.Empty
            ? null
            : await _context.PayrollCodeTypes.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == dto.PayrollCodeTypeId, cancellationToken);

        parent ??= await _context.PayrollCodeTypes
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.CodeType == codeType && e.LegacyCompanyCode == legacyCompanyCode, cancellationToken);

        if (parent == null)
        {
            throw new InvalidOperationException($"Code type {codeType} must be created before adding actual codes.");
        }

        var entity = await FindForUpsertAsync(_context.PayrollCodeValues, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollCodeValues
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.CodeType == codeType && e.ActualCode == actualCode && e.LegacyCompanyCode == legacyCompanyCode, cancellationToken)
            ?? new PayrollCodeValue { TenantId = tenantId };

        entity.PayrollCodeTypeId = parent.Id;
        entity.CodeType = codeType;
        entity.ActualCode = actualCode;
        entity.Description = RequireText(dto.Description, nameof(dto.Description));
        entity.AdditionalDescription = TrimOrNull(dto.AdditionalDescription);
        entity.AccountCode = TrimOrNull(dto.AccountCode);
        entity.Blocked = dto.Blocked;
        entity.DependentCodeType = NormalizeOptionalLegacyCode(dto.DependentCodeType, 5);
        entity.DependentActualCode = NormalizeOptionalLegacyCode(dto.DependentActualCode, 5);
        entity.LegacyCompanyCode = legacyCompanyCode;

        AddIfNew(_context.PayrollCodeValues, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollHolidaySetupDto> GetHolidaySetupAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var holidays = await _context.PayrollHolidays
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.HolidayDate)
            .ToListAsync(cancellationToken);

        var nonWorkingDays = await _context.PayrollNonWorkingDays
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.DayCode)
            .ToListAsync(cancellationToken);

        return new PayrollHolidaySetupDto
        {
            Holidays = holidays.Select(ToDto).ToList(),
            NonWorkingDays = nonWorkingDays.Select(ToDto).ToList()
        };
    }

    public async Task<PayrollHolidayDto> UpsertHolidayAsync(Guid tenantId, UpsertPayrollHolidayDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.HolidayDate == default)
        {
            throw new InvalidOperationException("Holiday date is required.");
        }

        var holidayDate = dto.HolidayDate.Date;
        var legacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode, 5);
        var entity = await FindForUpsertAsync(_context.PayrollHolidays, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollHolidays
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.HolidayDate == holidayDate && e.LegacyCompanyCode == legacyCompanyCode, cancellationToken)
            ?? new PayrollHoliday { TenantId = tenantId };

        entity.HolidayDate = holidayDate;
        entity.Description = RequireText(dto.Description, nameof(dto.Description));
        entity.LegacyCompanyCode = legacyCompanyCode;

        AddIfNew(_context.PayrollHolidays, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollNonWorkingDayDto> UpsertNonWorkingDayAsync(Guid tenantId, UpsertPayrollNonWorkingDayDto dto, CancellationToken cancellationToken = default)
    {
        var dayCode = NormalizeLegacyCode(dto.DayCode, nameof(dto.DayCode), 1);
        var legacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode, 5);
        var entity = await FindForUpsertAsync(_context.PayrollNonWorkingDays, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollNonWorkingDays
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.DayCode == dayCode && e.LegacyCompanyCode == legacyCompanyCode, cancellationToken)
            ?? new PayrollNonWorkingDay { TenantId = tenantId };

        entity.DayCode = dayCode;
        entity.Description = RequireText(dto.Description, nameof(dto.Description));
        entity.OvertimeRate = dto.OvertimeRate;
        entity.LegacyCompanyCode = legacyCompanyCode;

        AddIfNew(_context.PayrollNonWorkingDays, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<PayrollExchangeRateDto>> GetExchangeRatesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var rates = await _context.PayrollExchangeRates
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.PayPeriod)
            .ThenBy(e => e.CurrencyCode)
            .ToListAsync(cancellationToken);

        return rates.Select(ToDto).ToList();
    }

    public async Task<PayrollExchangeRateDto> UpsertExchangeRateAsync(Guid tenantId, UpsertPayrollExchangeRateDto dto, CancellationToken cancellationToken = default)
    {
        var currencyCode = NormalizeLegacyCode(dto.CurrencyCode, nameof(dto.CurrencyCode), 5);
        if (dto.Rate <= 0)
        {
            throw new InvalidOperationException("Exchange rate must be greater than zero.");
        }

        if (dto.PayPeriod <= 0)
        {
            throw new InvalidOperationException("Pay period is required.");
        }

        if (dto.PayPeriodFrom == default || dto.PayPeriodTo == default)
        {
            throw new InvalidOperationException("Pay period dates are required.");
        }

        var legacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode, 5);
        var entity = await FindForUpsertAsync(_context.PayrollExchangeRates, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollExchangeRates
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.CurrencyCode == currencyCode && e.PayPeriod == dto.PayPeriod && e.LegacyCompanyCode == legacyCompanyCode, cancellationToken)
            ?? new PayrollExchangeRate { TenantId = tenantId };

        entity.CurrencyCode = currencyCode;
        entity.Rate = dto.Rate;
        entity.PayPeriod = dto.PayPeriod;
        entity.PayPeriodFrom = dto.PayPeriodFrom.Date;
        entity.PayPeriodTo = dto.PayPeriodTo.Date;
        entity.LegacyCompanyCode = legacyCompanyCode;

        AddIfNew(_context.PayrollExchangeRates, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<PayrollBankBranchDto>> GetBankBranchesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var branches = await _context.PayrollBankBranches
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.BankCode)
            .ThenBy(e => e.BranchCode)
            .ToListAsync(cancellationToken);

        return branches.Select(ToDto).ToList();
    }

    public async Task<PayrollBankBranchDto> UpsertBankBranchAsync(Guid tenantId, UpsertPayrollBankBranchDto dto, CancellationToken cancellationToken = default)
    {
        var bankCode = NormalizeLegacyCode(dto.BankCode, nameof(dto.BankCode), 8);
        var branchCode = NormalizeLegacyCode(dto.BranchCode, nameof(dto.BranchCode), 8);
        var legacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode, 5);
        var entity = await FindForUpsertAsync(_context.PayrollBankBranches, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollBankBranches
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.BankCode == bankCode && e.BranchCode == branchCode && e.LegacyCompanyCode == legacyCompanyCode, cancellationToken)
            ?? new PayrollBankBranch { TenantId = tenantId };

        entity.BranchSetupCode = NormalizeOptionalLegacyCode(dto.BranchSetupCode, 5);
        entity.BankCode = bankCode;
        entity.BranchCode = branchCode;
        entity.BranchDescription = RequireText(dto.BranchDescription, nameof(dto.BranchDescription));
        entity.Region = TrimOrNull(dto.Region);
        entity.SortCode = TrimOrNull(dto.SortCode);
        entity.AccountCode = TrimOrNull(dto.AccountCode);
        entity.LegacyCompanyCode = legacyCompanyCode;

        AddIfNew(_context.PayrollBankBranches, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollLeaveSetupCollectionDto> GetLeaveSetupAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var leaveSetups = await _context.PayrollLeaveSetups
            .AsNoTracking()
            .Include(e => e.Details)
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.Category)
            .ThenBy(e => e.CategoryDetail)
            .ToListAsync(cancellationToken);

        var details = leaveSetups
            .SelectMany(e => e.Details)
            .OrderBy(e => e.Category)
            .ThenBy(e => e.CategoryDetail)
            .ThenBy(e => e.SequenceNo)
            .ToList();

        return new PayrollLeaveSetupCollectionDto
        {
            LeaveSetups = leaveSetups.Select(ToDto).ToList(),
            LeaveSetupDetails = details.Select(ToDto).ToList()
        };
    }

    public async Task<PayrollLeaveSetupDto> UpsertLeaveSetupAsync(Guid tenantId, UpsertPayrollLeaveSetupDto dto, CancellationToken cancellationToken = default)
    {
        var category = NormalizeLegacyCode(dto.Category, nameof(dto.Category), 5);
        var categoryDetail = NormalizeLegacyCode(dto.CategoryDetail, nameof(dto.CategoryDetail), 5);
        var legacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode, 5);
        var entity = await FindForUpsertAsync(_context.PayrollLeaveSetups, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollLeaveSetups
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Category == category && e.CategoryDetail == categoryDetail && e.LegacyCompanyCode == legacyCompanyCode, cancellationToken)
            ?? new PayrollLeaveSetup { TenantId = tenantId };

        entity.Category = category;
        entity.CategoryDetail = categoryDetail;
        entity.LegacyCompanyCode = legacyCompanyCode;

        AddIfNew(_context.PayrollLeaveSetups, entity);
        await _context.SaveChangesAsync(cancellationToken);

        await _context.Entry(entity).Collection(e => e.Details).LoadAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollLeaveSetupDetailDto> UpsertLeaveSetupDetailAsync(Guid tenantId, UpsertPayrollLeaveSetupDetailDto dto, CancellationToken cancellationToken = default)
    {
        var category = NormalizeLegacyCode(dto.Category, nameof(dto.Category), 5);
        var categoryDetail = NormalizeLegacyCode(dto.CategoryDetail, nameof(dto.CategoryDetail), 5);
        var legacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode, 5);

        var parent = dto.PayrollLeaveSetupId == Guid.Empty
            ? null
            : await _context.PayrollLeaveSetups.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == dto.PayrollLeaveSetupId, cancellationToken);

        parent ??= await _context.PayrollLeaveSetups
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Category == category && e.CategoryDetail == categoryDetail && e.LegacyCompanyCode == legacyCompanyCode, cancellationToken);

        if (parent == null)
        {
            throw new InvalidOperationException($"Leave setup {category}/{categoryDetail} must be created before adding detail rows.");
        }

        var entity = await FindForUpsertAsync(_context.PayrollLeaveSetupDetails, tenantId, dto.Id, cancellationToken)
            ?? new PayrollLeaveSetupDetail { TenantId = tenantId };

        entity.PayrollLeaveSetupId = parent.Id;
        entity.Category = category;
        entity.CategoryDetail = categoryDetail;
        entity.Days = dto.Days;
        entity.ServiceFrom = dto.ServiceFrom;
        entity.ServiceTo = dto.ServiceTo;
        entity.SequenceNo = dto.SequenceNo;
        entity.LegacyCompanyCode = legacyCompanyCode;

        AddIfNew(_context.PayrollLeaveSetupDetails, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollOvertimeSetupDto> GetOvertimeSetupAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var policies = await _context.PayrollOvertimePolicies
            .AsNoTracking()
            .Include(e => e.Ranges)
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.IsDefault)
            .ThenBy(e => e.Code)
            .ToListAsync(cancellationToken);

        var ranges = policies
            .SelectMany(e => e.Ranges)
            .OrderBy(e => e.MinRange)
            .ThenBy(e => e.MaxRange)
            .ToList();

        return new PayrollOvertimeSetupDto
        {
            OvertimePolicies = policies.Select(ToDto).ToList(),
            OvertimeRanges = ranges.Select(ToDto).ToList()
        };
    }

    public async Task<PayrollOvertimeRangeDto> UpsertOvertimeRangeAsync(Guid tenantId, UpsertPayrollOvertimeRangeDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.MaxRange < dto.MinRange)
        {
            throw new InvalidOperationException("Overtime max range must be greater than or equal to min range.");
        }

        var parent = await _context.PayrollOvertimePolicies
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == dto.PayrollOvertimePolicyId, cancellationToken);

        if (parent == null)
        {
            throw new InvalidOperationException("Overtime policy must be created before adding range rows.");
        }

        var entity = await FindForUpsertAsync(_context.PayrollOvertimeRanges, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollOvertimeRanges
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.PayrollOvertimePolicyId == parent.Id && e.MinRange == dto.MinRange && e.MaxRange == dto.MaxRange, cancellationToken)
            ?? new PayrollOvertimeRange { TenantId = tenantId };

        entity.PayrollOvertimePolicyId = parent.Id;
        entity.MinRange = dto.MinRange;
        entity.MaxRange = dto.MaxRange;
        entity.Rate = dto.Rate;
        entity.LegacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode ?? parent.LegacyCompanyCode, 5);

        AddIfNew(_context.PayrollOvertimeRanges, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<PayrollLegacyMenuUserDto>> GetLegacyMenuUsersAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var users = await _context.PayrollLegacyMenuUsers
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.UserName)
            .ToListAsync(cancellationToken);

        return users.Select(ToDto).ToList();
    }

    public async Task<PayrollLegacyMenuUserDto> UpsertLegacyMenuUserAsync(Guid tenantId, UpsertPayrollLegacyMenuUserDto dto, CancellationToken cancellationToken = default)
    {
        var userName = NormalizeLegacyCode(dto.UserName, nameof(dto.UserName), 10);
        var legacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode, 5);
        var entity = await FindForUpsertAsync(_context.PayrollLegacyMenuUsers, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollLegacyMenuUsers
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.UserName == userName && e.LegacyCompanyCode == legacyCompanyCode, cancellationToken)
            ?? new PayrollLegacyMenuUser { TenantId = tenantId };

        entity.UserName = userName;
        entity.UserGroup = NormalizeLegacyCode(dto.UserGroup, nameof(dto.UserGroup), 5);
        entity.UserNo = NormalizeLegacyCode(dto.UserNo, nameof(dto.UserNo), 6);
        entity.LegacyPasswordHash = TrimOrNull(dto.LegacyPasswordHash);
        entity.LoginEnabled = dto.LoginEnabled;
        entity.Locked = dto.Locked;
        entity.PasswordChangeRequired = dto.PasswordChangeRequired;
        entity.PasswordExpiryDate = dto.PasswordExpiryDate?.Date;
        entity.PasswordFailureCount = dto.PasswordFailureCount;
        entity.CompanyId = NormalizeOptionalLegacyCode(dto.CompanyId, 5);
        entity.BusinessUnitId = NormalizeOptionalLegacyCode(dto.BusinessUnitId, 5);
        entity.LegacyCompanyCode = legacyCompanyCode;
        entity.CanViewSalary = dto.CanViewSalary;
        entity.CanApprove = dto.CanApprove;
        entity.RefreshToken = TrimOrNull(dto.RefreshToken);
        entity.LastPasswordChange = TrimOrNull(dto.LastPasswordChange);

        AddIfNew(_context.PayrollLegacyMenuUsers, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<PayrollLegacyMenuSecurityDto>> GetMenuSecurityAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var rows = await _context.PayrollLegacyMenuSecurity
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.FormCode)
            .ThenBy(e => e.UserName)
            .ThenBy(e => e.GroupName)
            .ToListAsync(cancellationToken);

        return rows.Select(ToDto).ToList();
    }

    public async Task<PayrollLegacyMenuSecurityDto> UpsertMenuSecurityAsync(Guid tenantId, UpsertPayrollLegacyMenuSecurityDto dto, CancellationToken cancellationToken = default)
    {
        var formCode = NormalizeLegacyCode(dto.FormCode, nameof(dto.FormCode), 10);
        var formName = NormalizeLegacyCode(dto.FormName, nameof(dto.FormName), 10);
        var userName = NormalizeLegacyCode(dto.UserName, nameof(dto.UserName), 10);
        var groupName = NormalizeLegacyCode(dto.GroupName, nameof(dto.GroupName), 5);
        var legacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode, 5);

        var entity = await FindForUpsertAsync(_context.PayrollLegacyMenuSecurity, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollLegacyMenuSecurity
                .FirstOrDefaultAsync(e => e.TenantId == tenantId &&
                                          e.FormCode == formCode &&
                                          e.UserName == userName &&
                                          e.GroupName == groupName &&
                                          e.LegacyCompanyCode == legacyCompanyCode,
                    cancellationToken)
            ?? new PayrollLegacyMenuSecurity { TenantId = tenantId };

        entity.FormCode = formCode;
        entity.FormName = formName;
        entity.UserName = userName;
        entity.GroupName = groupName;
        entity.Allowed = dto.Allowed;
        entity.LegacyCompanyCode = legacyCompanyCode;
        entity.TopMenu = TrimOrNull(dto.TopMenu);
        entity.SubMenu = TrimOrNull(dto.SubMenu);
        entity.CanEdit = dto.CanEdit;

        AddIfNew(_context.PayrollLegacyMenuSecurity, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollLegacyMenuUserDto> ChangeLegacyMenuUserPasswordAsync(Guid tenantId, PayrollLegacyPasswordChangeDto dto, CancellationToken cancellationToken = default)
    {
        var userName = NormalizeLegacyCode(dto.UserName, nameof(dto.UserName), 10);
        var legacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode, 5);
        var newPasswordReference = RequireText(dto.NewPasswordReference, nameof(dto.NewPasswordReference));

        var entity = await _context.PayrollLegacyMenuUsers
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.UserName == userName && e.LegacyCompanyCode == legacyCompanyCode, cancellationToken)
            ?? throw new KeyNotFoundException("Legacy payroll menu user not found.");

        entity.LegacyPasswordHash = newPasswordReference;
        entity.PasswordChangeRequired = dto.ForceChange;
        entity.PasswordExpiryDate = dto.PasswordExpiryDate?.Date;
        entity.PasswordFailureCount = 0;
        entity.LastPasswordChange = DateTime.UtcNow.ToString("O");

        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollCompanySetupDto> GetCompanySetupAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var profiles = await _context.PayrollCompanyProfiles
            .AsNoTracking()
            .Include(e => e.BusinessUnits)
            .Include(e => e.Bankers)
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.CompanyId)
            .ToListAsync(cancellationToken);

        var businessUnits = await _context.PayrollBusinessUnits
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.CompanyId)
            .ThenBy(e => e.BusinessUnitId)
            .ToListAsync(cancellationToken);

        var bankers = await _context.PayrollCompanyBankers
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.CompanyId)
            .ThenBy(e => e.BankCode)
            .ThenBy(e => e.BankBranch)
            .ToListAsync(cancellationToken);

        return new PayrollCompanySetupDto
        {
            CompanyProfiles = profiles.Select(ToDto).ToList(),
            BusinessUnits = businessUnits.Select(ToDto).ToList(),
            CompanyBankers = bankers.Select(ToDto).ToList()
        };
    }

    public async Task<PayrollCompanyProfileDto> UpsertCompanyProfileAsync(Guid tenantId, UpsertPayrollCompanyProfileDto dto, CancellationToken cancellationToken = default)
    {
        var companyId = NormalizeLegacyCode(dto.CompanyId, nameof(dto.CompanyId), 5);
        var companyCode = NormalizeOptionalLegacyCode(dto.CompanyCode, 5);
        var legacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode ?? dto.CompanyCode, 5);
        var entity = await FindForUpsertAsync(_context.PayrollCompanyProfiles, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollCompanyProfiles
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.CompanyId == companyId, cancellationToken)
            ?? new PayrollCompanyProfile { TenantId = tenantId };

        entity.CompanyId = companyId;
        entity.CompanyName = RequireText(dto.CompanyName, nameof(dto.CompanyName));
        entity.CompanySystemName = TrimOrNull(dto.CompanySystemName);
        entity.CompanyCode = companyCode;
        entity.MultipleBusinessUnits = dto.MultipleBusinessUnits;
        entity.BusinessNumber = dto.BusinessNumber;
        entity.AddressLine1 = TrimOrNull(dto.AddressLine1);
        entity.AddressLine2 = TrimOrNull(dto.AddressLine2);
        entity.AddressLine3 = TrimOrNull(dto.AddressLine3);
        entity.CityOrTown = TrimOrNull(dto.CityOrTown);
        entity.RegionOrState = TrimOrNull(dto.RegionOrState);
        entity.Country = TrimOrNull(dto.Country);
        entity.LicenceType = TrimOrNull(dto.LicenceType);
        entity.LicenceNo = dto.LicenceNo;
        entity.SocialSecurityNo = TrimOrNull(dto.SocialSecurityNo);
        entity.TaxpayerIdNo = TrimOrNull(dto.TaxpayerIdNo);
        entity.PictureName = TrimOrNull(dto.PictureName);
        entity.LogoName = TrimOrNull(dto.LogoName);
        entity.SplitLicenceOnBusinessUnits = dto.SplitLicenceOnBusinessUnits;
        entity.LicencePercentOrNumber = NormalizeOptionalLegacyCode(dto.LicencePercentOrNumber, 1);
        entity.MakeCompanyABusinessUnit = dto.MakeCompanyABusinessUnit;
        entity.MultiplePayBasis = dto.MultiplePayBasis;
        entity.CompanyPayBasis = TrimOrNull(dto.CompanyPayBasis);
        entity.MultipleEmployeePaymentMethods = dto.MultipleEmployeePaymentMethods;
        entity.DefaultEmployeePaymentMethod = TrimOrNull(dto.DefaultEmployeePaymentMethod);
        entity.LegacyCompanyCode = legacyCompanyCode;
        entity.IsActive = dto.IsActive;

        AddIfNew(_context.PayrollCompanyProfiles, entity);
        await _context.SaveChangesAsync(cancellationToken);

        await _context.Entry(entity).Collection(e => e.BusinessUnits).LoadAsync(cancellationToken);
        await _context.Entry(entity).Collection(e => e.Bankers).LoadAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollBusinessUnitDto> UpsertBusinessUnitAsync(Guid tenantId, UpsertPayrollBusinessUnitDto dto, CancellationToken cancellationToken = default)
    {
        var businessUnitId = NormalizeLegacyCode(dto.BusinessUnitId, nameof(dto.BusinessUnitId), 5);
        var legacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode ?? dto.CompanyCode, 5);
        var entity = await FindForUpsertAsync(_context.PayrollBusinessUnits, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollBusinessUnits
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.BusinessUnitId == businessUnitId && e.LegacyCompanyCode == legacyCompanyCode, cancellationToken)
            ?? new PayrollBusinessUnit { TenantId = tenantId };

        var profile = dto.PayrollCompanyProfileId.HasValue && dto.PayrollCompanyProfileId.Value != Guid.Empty
            ? await _context.PayrollCompanyProfiles.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == dto.PayrollCompanyProfileId.Value, cancellationToken)
            : null;

        profile ??= !string.IsNullOrWhiteSpace(dto.CompanyId)
            ? await _context.PayrollCompanyProfiles.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.CompanyId == dto.CompanyId.Trim().ToUpper(), cancellationToken)
            : null;

        entity.PayrollCompanyProfileId = profile?.Id;
        entity.BusinessUnitId = businessUnitId;
        entity.BusinessUnitCode = NormalizeOptionalLegacyCode(dto.BusinessUnitCode, 5);
        entity.BusinessUnitName = TrimOrNull(dto.BusinessUnitName);
        entity.Location = TrimOrNull(dto.Location);
        entity.Country = TrimOrNull(dto.Country);
        entity.DistributeLicence = dto.DistributeLicence;
        entity.LicencePercentOrNumber = NormalizeOptionalLegacyCode(dto.LicencePercentOrNumber, 1);
        entity.LicenceValue = dto.LicenceValue;
        entity.AddressLine1 = TrimOrNull(dto.AddressLine1);
        entity.AddressLine2 = TrimOrNull(dto.AddressLine2);
        entity.AddressLine3 = TrimOrNull(dto.AddressLine3);
        entity.RegionOrState = TrimOrNull(dto.RegionOrState);
        entity.CompanyId = NormalizeOptionalLegacyCode(dto.CompanyId ?? profile?.CompanyId, 5);
        entity.CompanyCode = NormalizeOptionalLegacyCode(dto.CompanyCode ?? profile?.CompanyCode, 5);
        entity.AccountsCode = TrimOrNull(dto.AccountsCode);
        entity.MultiplePayBasis = dto.MultiplePayBasis;
        entity.CompanyPayBasis = TrimOrNull(dto.CompanyPayBasis);
        entity.MultipleEmployeePaymentMethods = dto.MultipleEmployeePaymentMethods;
        entity.DefaultEmployeePaymentMethod = TrimOrNull(dto.DefaultEmployeePaymentMethod);
        entity.LegacyCompanyCode = legacyCompanyCode;

        AddIfNew(_context.PayrollBusinessUnits, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollCompanyBankerDto> UpsertCompanyBankerAsync(Guid tenantId, UpsertPayrollCompanyBankerDto dto, CancellationToken cancellationToken = default)
    {
        var bankCode = NormalizeLegacyCode(dto.BankCode, nameof(dto.BankCode), 5);
        var companyCode = NormalizeLegacyCode(dto.CompanyCode, nameof(dto.CompanyCode), 5);
        var companyId = NormalizeLegacyCode(dto.CompanyId, nameof(dto.CompanyId), 5);
        var bankBranch = NormalizeOptionalLegacyCode(dto.BankBranch, 5);
        var legacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode ?? dto.CompanyCode, 5);

        var entity = await FindForUpsertAsync(_context.PayrollCompanyBankers, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollCompanyBankers
                .FirstOrDefaultAsync(e => e.TenantId == tenantId &&
                                          e.CompanyId == companyId &&
                                          e.CompanyCode == companyCode &&
                                          e.BankCode == bankCode &&
                                          e.BankBranch == bankBranch,
                    cancellationToken)
            ?? new PayrollCompanyBanker { TenantId = tenantId };

        var profile = dto.PayrollCompanyProfileId.HasValue && dto.PayrollCompanyProfileId.Value != Guid.Empty
            ? await _context.PayrollCompanyProfiles.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == dto.PayrollCompanyProfileId.Value, cancellationToken)
            : await _context.PayrollCompanyProfiles.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.CompanyId == companyId, cancellationToken);

        entity.PayrollCompanyProfileId = profile?.Id;
        entity.BankCode = bankCode;
        entity.BankBranch = bankBranch;
        entity.Address1 = TrimOrNull(dto.Address1);
        entity.Address2 = TrimOrNull(dto.Address2);
        entity.Address3 = TrimOrNull(dto.Address3);
        entity.AccountNumber1 = TrimOrNull(dto.AccountNumber1);
        entity.AccountNumber2 = TrimOrNull(dto.AccountNumber2);
        entity.AccountNumber3 = TrimOrNull(dto.AccountNumber3);
        entity.CompanyCode = companyCode;
        entity.CompanyId = companyId;
        entity.Signatory1 = TrimOrNull(dto.Signatory1);
        entity.Signatory2 = TrimOrNull(dto.Signatory2);
        entity.Signatory3 = TrimOrNull(dto.Signatory3);
        entity.SignatoryPosition1 = TrimOrNull(dto.SignatoryPosition1);
        entity.SignatoryPosition2 = TrimOrNull(dto.SignatoryPosition2);
        entity.SignatoryPosition3 = TrimOrNull(dto.SignatoryPosition3);
        entity.BankRegion = NormalizeOptionalLegacyCode(dto.BankRegion, 5);
        entity.LegacyCompanyCode = legacyCompanyCode;
        entity.IsActive = dto.IsActive;

        AddIfNew(_context.PayrollCompanyBankers, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollGradeSetupDto> GetGradeSetupAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var grades = await _context.PayrollGrades
            .AsNoTracking()
            .Include(e => e.Notches)
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.OrderField)
            .ThenBy(e => e.GradeName)
            .ToListAsync(cancellationToken);

        var notches = grades
            .SelectMany(e => e.Notches)
            .OrderBy(e => e.OrderField)
            .ThenBy(e => e.Notch)
            .ToList();

        return new PayrollGradeSetupDto
        {
            Grades = grades.Select(ToDto).ToList(),
            Notches = notches.Select(ToDto).ToList()
        };
    }

    public async Task<PayrollGradeDto> UpsertGradeAsync(Guid tenantId, UpsertPayrollGradeDto dto, CancellationToken cancellationToken = default)
    {
        var gradeName = RequireText(dto.GradeName, nameof(dto.GradeName));
        var currencyCode = NormalizeCurrency(dto.CurrencyCode);
        var legacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode, 5);
        var entity = await FindForUpsertAsync(_context.PayrollGrades, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollGrades
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.GradeName == gradeName && e.CurrencyCode == currencyCode && e.LegacyCompanyCode == legacyCompanyCode, cancellationToken)
            ?? new PayrollGrade { TenantId = tenantId };

        entity.GradeId = NormalizeOptionalLegacyCode(dto.GradeId, 5);
        entity.GradeType = TrimOrNull(dto.GradeType);
        entity.GradeName = gradeName;
        entity.SystemGradeName = TrimOrNull(dto.SystemGradeName);
        entity.MinValue = dto.MinValue;
        entity.MaxValue = dto.MaxValue;
        entity.MidPoint = dto.MidPoint;
        entity.CurrencyCode = currencyCode;
        entity.StartPoint = NormalizeOptionalLegacyCode(dto.StartPoint, 5);
        entity.IncrementStep = dto.IncrementStep;
        entity.Ceiling = NormalizeOptionalLegacyCode(dto.Ceiling, 5);
        entity.StartDate = dto.StartDate?.Date;
        entity.EndDate = dto.EndDate?.Date;
        entity.BusinessUnitId = NormalizeOptionalLegacyCode(dto.BusinessUnitId, 5);
        entity.CompanyId = NormalizeOptionalLegacyCode(dto.CompanyId, 5);
        entity.OrderField = dto.OrderField;
        entity.ReportingName = TrimOrNull(dto.ReportingName);
        entity.EnforceNotchConsistency = dto.EnforceNotchConsistency;
        entity.LegacyCompanyCode = legacyCompanyCode;
        entity.IsActive = dto.IsActive;

        AddIfNew(_context.PayrollGrades, entity);
        await _context.SaveChangesAsync(cancellationToken);

        await _context.Entry(entity).Collection(e => e.Notches).LoadAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollGradeNotchDto> UpsertGradeNotchAsync(Guid tenantId, UpsertPayrollGradeNotchDto dto, CancellationToken cancellationToken = default)
    {
        var notch = NormalizeLegacyCode(dto.Notch, nameof(dto.Notch), 5);
        var currencyCode = NormalizeCurrency(dto.CurrencyCode);
        var legacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode, 5);

        var grade = dto.PayrollGradeId == Guid.Empty
            ? null
            : await _context.PayrollGrades.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == dto.PayrollGradeId, cancellationToken);

        grade ??= !string.IsNullOrWhiteSpace(dto.GradeName)
            ? await _context.PayrollGrades.FirstOrDefaultAsync(e => e.TenantId == tenantId &&
                                                                    e.GradeName == dto.GradeName.Trim() &&
                                                                    e.CurrencyCode == currencyCode &&
                                                                    e.LegacyCompanyCode == legacyCompanyCode,
                cancellationToken)
            : null;

        if (grade == null)
        {
            throw new InvalidOperationException("Grade must be created before adding notch rows.");
        }

        var entity = await FindForUpsertAsync(_context.PayrollGradeNotches, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollGradeNotches
                .FirstOrDefaultAsync(e => e.TenantId == tenantId &&
                                          e.PayrollGradeId == grade.Id &&
                                          e.Notch == notch &&
                                          e.CurrencyCode == currencyCode &&
                                          e.LegacyCompanyCode == legacyCompanyCode,
                    cancellationToken)
            ?? new PayrollGradeNotch { TenantId = tenantId };

        entity.PayrollGradeId = grade.Id;
        entity.GradeId = NormalizeOptionalLegacyCode(dto.GradeId ?? grade.GradeId, 5);
        entity.SystemGradeName = TrimOrNull(dto.SystemGradeName ?? grade.SystemGradeName);
        entity.Notch = notch;
        entity.Value = dto.Value;
        entity.CurrencyCode = currencyCode;
        entity.StartDate = dto.StartDate?.Date;
        entity.EndDate = dto.EndDate?.Date;
        entity.OrderField = dto.OrderField;
        entity.BusinessUnitId = NormalizeOptionalLegacyCode(dto.BusinessUnitId ?? grade.BusinessUnitId, 5);
        entity.CompanyId = NormalizeOptionalLegacyCode(dto.CompanyId ?? grade.CompanyId, 5);
        entity.AnnualisedValue = dto.AnnualisedValue;
        entity.GradeName = TrimOrNull(dto.GradeName ?? grade.GradeName);
        entity.ReportingName = TrimOrNull(dto.ReportingName);
        entity.LegacyCompanyCode = legacyCompanyCode;
        entity.HourlyRate = dto.HourlyRate;

        AddIfNew(_context.PayrollGradeNotches, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollTaxTableDto> GetTaxTableAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var taxBands = await _context.PayrollTaxBands
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.IsAnnual)
            .ThenBy(e => e.TaxType)
            .ThenBy(e => e.PayPeriod)
            .ThenBy(e => e.SerialNo)
            .ToListAsync(cancellationToken);

        return new PayrollTaxTableDto
        {
            TaxBands = taxBands.Select(ToDto).ToList()
        };
    }

    public async Task<PayrollParameterSetDto> UpsertParameterSetAsync(Guid tenantId, UpsertPayrollParameterSetDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await FindForUpsertAsync(_context.PayrollParameterSets, tenantId, dto.Id, cancellationToken)
            ?? new PayrollParameterSet { TenantId = tenantId };

        entity.Code = NormalizeCode(dto.Code);
        entity.Name = RequireText(dto.Name, nameof(dto.Name));
        entity.BaseCurrency = NormalizeCurrency(dto.BaseCurrency);
        entity.DateFormat = string.IsNullOrWhiteSpace(dto.DateFormat) ? "dd-MM-yyyy" : dto.DateFormat.Trim();
        entity.MultiLoginEnabled = dto.MultiLoginEnabled;
        entity.MaxLoginCount = dto.MaxLoginCount;
        entity.CurrentPayPeriod = dto.CurrentPayPeriod <= 0 ? 1 : dto.CurrentPayPeriod;
        entity.MonthDays = dto.MonthDays <= 0 ? 30 : dto.MonthDays;
        entity.PayFrequencyMonths = dto.PayFrequencyMonths <= 0 ? 1 : dto.PayFrequencyMonths;
        entity.PayMode = NormalizeOptionalLegacyCode(dto.PayMode, 1);
        var currentPeriod = NormalizePayrollParameterPeriod(dto.CurrentPeriodFrom, dto.CurrentPeriodTo, entity.PayMode);
        entity.CurrentPeriodFrom = currentPeriod.From;
        entity.CurrentPeriodTo = currentPeriod.To;
        entity.TimeSheetMode = NormalizeOptionalLegacyCode(dto.TimeSheetMode, 1);
        entity.LeaveClassification = NormalizeOptionalLegacyCode(dto.LeaveClassification, 5);
        entity.MultiplePayBasisEnabled = dto.MultiplePayBasisEnabled;
        entity.DefaultPayBasis = TrimOrNull(dto.DefaultPayBasis);
        entity.AllowEmployeePaymentMethod = dto.AllowEmployeePaymentMethod;
        entity.DefaultEmployeePaymentMethod = TrimOrNull(dto.DefaultEmployeePaymentMethod);
        entity.EmployeeSsfRate = dto.EmployeeSsfRate;
        entity.EmployerSsfRate = dto.EmployerSsfRate;
        entity.SsfLimit = dto.SsfLimit;
        entity.BonusTaxRate = dto.BonusTaxRate;
        entity.MinimumBonusToTax = dto.MinimumBonusToTax;
        entity.WithholdingTaxRate = dto.WithholdingTaxRate;
        entity.TaxRate = dto.TaxRate;
        entity.MinimumTaxableIncome = dto.MinimumTaxableIncome;
        entity.DebitRatio = dto.DebitRatio;
        entity.MinimumHireAge = dto.MinimumHireAge;
        entity.MaleRetireAge = dto.MaleRetireAge;
        entity.FemaleRetireAge = dto.FemaleRetireAge;
        entity.TotalAllowanceTaxCeiling = dto.TotalAllowanceTaxCeiling;
        entity.ExchangeRate = dto.ExchangeRate;
        entity.ReportingCurrency = NormalizeOptionalCurrency(dto.ReportingCurrency);
        entity.PictureDirectory = TrimOrNull(dto.PictureDirectory);
        entity.NightAllowanceAmount = dto.NightAllowanceAmount;
        entity.NightAllowancePercent = dto.NightAllowancePercent;
        entity.SeparateBonusTax = dto.SeparateBonusTax;
        entity.MultiCurrencyEnabled = dto.MultiCurrencyEnabled;
        entity.TimesheetEnabled = dto.TimesheetEnabled || !string.IsNullOrWhiteSpace(entity.TimeSheetMode);
        entity.MinimumPasswordLength = dto.MinimumPasswordLength;
        entity.PasswordExpirationDays = dto.PasswordExpirationDays;
        entity.PasswordReuseCount = dto.PasswordReuseCount;
        entity.MinimumPasswordNumbers = dto.MinimumPasswordNumbers;
        entity.MinimumPasswordSpecialCharacters = dto.MinimumPasswordSpecialCharacters;
        entity.MinimumPasswordUppercase = dto.MinimumPasswordUppercase;
        entity.MinimumPasswordLowercase = dto.MinimumPasswordLowercase;
        entity.LegacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode, 5);
        entity.IsActive = dto.IsActive;

        AddIfNew(_context.PayrollParameterSets, entity);

        if (entity.IsActive)
        {
            await DeactivateOtherParameterSetsAsync(tenantId, entity.Id, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollComponentDto> UpsertComponentAsync(Guid tenantId, UpsertPayrollComponentDto dto, CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(dto.Code);
        var entity = await FindForUpsertAsync(_context.PayrollComponents, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollComponents
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.ComponentType == dto.ComponentType && e.Code == code, cancellationToken)
            ?? new PayrollComponent { TenantId = tenantId };

        entity.Code = code;
        entity.Name = RequireText(dto.Name, nameof(dto.Name));
        entity.ComponentType = dto.ComponentType;
        entity.CalculationType = dto.CalculationType;
        entity.Amount = dto.Amount;
        entity.Rate = dto.Rate;
        entity.TaxFreeCeiling = dto.TaxFreeCeiling;
        entity.SeparateTaxPercent = dto.SeparateTaxPercent;
        entity.MaxBenefitToTax = dto.MaxBenefitToTax;
        entity.EmployerAmount = dto.EmployerAmount;
        entity.Category = TrimOrNull(dto.Category);
        entity.Cycle = TrimOrNull(dto.Cycle);
        entity.NextPayPeriod = dto.NextPayPeriod;
        entity.NextPayPeriodDate = dto.NextPayPeriodDate?.Date;
        entity.LastPayDate = dto.LastPayDate?.Date;
        entity.Taxable = dto.Taxable;
        entity.EmployerTaxable = dto.EmployerTaxable;
        entity.SeparateTax = dto.SeparateTax;
        entity.ApplyToBenefit = dto.ApplyToBenefit;
        entity.AfterTaxContribution = dto.AfterTaxContribution;
        entity.IncludeInGross = dto.IncludeInGross;
        entity.GrossUp = dto.GrossUp;
        entity.Prorate = dto.Prorate;
        entity.AppliesByDefault = dto.AppliesByDefault;
        entity.CurrencyCode = NormalizeCurrency(dto.CurrencyCode);
        entity.IsActive = dto.IsActive;

        AddIfNew(_context.PayrollComponents, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollComponentRuleDto> UpsertComponentRuleAsync(Guid tenantId, UpsertPayrollComponentRuleDto dto, CancellationToken cancellationToken = default)
    {
        var componentCode = NormalizeCode(dto.ComponentCode);
        var category = RequireText(dto.Category, nameof(dto.Category));
        var entity = await FindForUpsertAsync(_context.PayrollComponentRules, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollComponentRules
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.ComponentType == dto.ComponentType && e.ComponentCode == componentCode && e.Category == category, cancellationToken)
            ?? new PayrollComponentRule { TenantId = tenantId };

        entity.ComponentType = dto.ComponentType;
        entity.ComponentCode = componentCode;
        entity.Category = category;
        entity.CalculationType = dto.CalculationType;
        entity.Amount = dto.Amount;
        entity.TaxFreeCeiling = dto.TaxFreeCeiling;
        entity.EmployerAmount = dto.EmployerAmount;
        entity.AfterTax = dto.AfterTax;
        entity.EmployerTaxable = dto.EmployerTaxable;
        entity.Applicable = dto.Applicable;
        entity.LegacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode, 5);

        AddIfNew(_context.PayrollComponentRules, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollTaxBandDto> UpsertTaxBandAsync(Guid tenantId, UpsertPayrollTaxBandDto dto, CancellationToken cancellationToken = default)
    {
        var lowerBound = dto.TaxableIncome ?? dto.LowerBound;
        if (dto.UpperBound.HasValue && dto.UpperBound.Value <= lowerBound)
        {
            throw new InvalidOperationException("UpperBound must be greater than LowerBound.");
        }

        var taxType = NormalizeCode(dto.TaxType);
        var effectiveFrom = dto.EffectiveFrom == default ? DateTime.UtcNow.Date : dto.EffectiveFrom.Date;
        var legacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode, 5);
        var entity = await FindForUpsertAsync(_context.PayrollTaxBands, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollTaxBands
                .FirstOrDefaultAsync(e => e.TenantId == tenantId &&
                                          e.TaxType == taxType &&
                                          e.SerialNo == dto.SerialNo &&
                                          e.EffectiveFrom == effectiveFrom &&
                                          e.IsAnnual == dto.IsAnnual &&
                                          e.LegacyCompanyCode == legacyCompanyCode,
                    cancellationToken)
            ?? new PayrollTaxBand { TenantId = tenantId };

        entity.TaxType = taxType;
        entity.SerialNo = dto.SerialNo;
        entity.Description = TrimOrNull(dto.Description);
        entity.LowerBound = lowerBound;
        entity.UpperBound = dto.UpperBound;
        entity.TaxableIncome = dto.TaxableIncome ?? lowerBound;
        entity.RatePercent = dto.RatePercent;
        entity.FixedAmount = dto.PerMonthAmount ?? dto.FixedAmount;
        entity.PerMonthAmount = dto.PerMonthAmount ?? dto.FixedAmount;
        entity.CumulativeTax = dto.CumulativeTax;
        entity.CumulativeSalary = dto.CumulativeSalary;
        entity.PayPeriod = dto.PayPeriod;
        entity.PayPeriodFrom = dto.PayPeriodFrom?.Date;
        entity.PayPeriodTo = dto.PayPeriodTo?.Date;
        entity.LegacyCompanyCode = legacyCompanyCode;
        entity.IsAnnual = dto.IsAnnual;
        entity.EffectiveFrom = effectiveFrom;
        entity.EffectiveTo = dto.EffectiveTo?.Date;
        entity.IsActive = dto.IsActive;

        AddIfNew(_context.PayrollTaxBands, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollTaxReliefDto> UpsertTaxReliefAsync(Guid tenantId, UpsertPayrollTaxReliefDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await FindForUpsertAsync(_context.PayrollTaxReliefs, tenantId, dto.Id, cancellationToken)
            ?? new PayrollTaxRelief { TenantId = tenantId };

        entity.Code = NormalizeCode(dto.Code);
        entity.Name = RequireText(dto.Name, nameof(dto.Name));
        entity.CalculationType = dto.CalculationType;
        entity.Amount = dto.Amount;
        entity.Factor = dto.Factor == 0 ? 1 : dto.Factor;
        entity.AppliesByDefault = dto.AppliesByDefault;
        entity.IsActive = dto.IsActive;

        AddIfNew(_context.PayrollTaxReliefs, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollPensionSchemeDto> UpsertPensionSchemeAsync(Guid tenantId, UpsertPayrollPensionSchemeDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await FindForUpsertAsync(_context.PayrollPensionSchemes, tenantId, dto.Id, cancellationToken)
            ?? new PayrollPensionScheme { TenantId = tenantId };

        entity.Code = NormalizeCode(dto.Code);
        entity.Name = RequireText(dto.Name, nameof(dto.Name));
        entity.EmployeeRatePercent = dto.EmployeeRatePercent;
        entity.EmployerRatePercent = dto.EmployerRatePercent;
        entity.ContributionLimit = dto.ContributionLimit;
        entity.IsDefault = dto.IsDefault;
        entity.IsActive = dto.IsActive;

        AddIfNew(_context.PayrollPensionSchemes, entity);
        if (entity.IsDefault && entity.IsActive)
        {
            await _context.PayrollPensionSchemes
                .Where(e => e.TenantId == tenantId && e.Id != entity.Id && e.IsDefault)
                .ExecuteUpdateAsync(setters => setters.SetProperty(e => e.IsDefault, false), cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollOvertimePolicyDto> UpsertOvertimePolicyAsync(Guid tenantId, UpsertPayrollOvertimePolicyDto dto, CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(dto.Code);
        var legacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode, 5);
        var entity = await FindForUpsertAsync(_context.PayrollOvertimePolicies, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollOvertimePolicies
                .Include(e => e.Ranges)
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Code == code && e.LegacyCompanyCode == legacyCompanyCode, cancellationToken)
            ?? new PayrollOvertimePolicy { TenantId = tenantId };

        entity.Code = code;
        entity.Name = RequireText(dto.Name, nameof(dto.Name));
        entity.NormalWorkingHours = dto.NormalWorkingHours;
        entity.NormalHours = dto.NormalHours;
        entity.WeekdayRate = dto.WeekdayRate;
        entity.HolidayRate = dto.HolidayRate;
        entity.SpecialDutyRate = dto.SpecialDutyRate;
        entity.Taxable = dto.Taxable;
        entity.TaxCeiling = dto.TaxCeiling;
        entity.SeparateOvertimeTax = dto.SeparateOvertimeTax;
        entity.MaxOvertimeAmount = dto.MaxOvertimeAmount;
        entity.MaxOvertimeIsPercent = dto.MaxOvertimeIsPercent;
        entity.MaxOvertimeType = NormalizeOptionalLegacyCode(dto.MaxOvertimeType, 1);
        entity.LeaveRecallRate = dto.LeaveRecallRate;
        entity.PayPeriod = dto.PayPeriod;
        entity.PayPeriodFrom = dto.PayPeriodFrom?.Date;
        entity.PayPeriodTo = dto.PayPeriodTo?.Date;
        entity.MaxOvertimeSeparateTax = dto.MaxOvertimeSeparateTax;
        entity.MinimumBasicForSeparateTax = dto.MinimumBasicForSeparateTax;
        entity.MinimumSeparateOvertimePercent = dto.MinimumSeparateOvertimePercent;
        entity.LegacyCompanyCode = legacyCompanyCode;
        entity.IsDefault = dto.IsDefault;
        entity.IsActive = dto.IsActive;

        AddIfNew(_context.PayrollOvertimePolicies, entity);
        if (entity.IsDefault && entity.IsActive)
        {
            await _context.PayrollOvertimePolicies
                .Where(e => e.TenantId == tenantId && e.Id != entity.Id && e.IsDefault)
                .ExecuteUpdateAsync(setters => setters.SetProperty(e => e.IsDefault, false), cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollLoanPolicyDto> UpsertLoanPolicyAsync(Guid tenantId, UpsertPayrollLoanPolicyDto dto, CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(dto.Code);
        var legacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode, 5);
        var entity = await FindForUpsertAsync(_context.PayrollLoanPolicies, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollLoanPolicies
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Code == code && e.LegacyCompanyCode == legacyCompanyCode, cancellationToken)
            ?? new PayrollLoanPolicy { TenantId = tenantId };

        entity.Code = code;
        entity.Name = RequireText(dto.Name, nameof(dto.Name));
        entity.MaxLoanAmount = dto.MaxLoanAmount;
        entity.InterestRatePercent = dto.InterestRatePercent;
        entity.ApplyInterest = dto.ApplyInterest;
        entity.MaxPaybackPeriods = dto.MaxPaybackPeriods;
        entity.MaxDebitRatioPercent = dto.MaxDebitRatioPercent;
        entity.InterestType = string.IsNullOrWhiteSpace(dto.InterestType) ? "Flat" : dto.InterestType.Trim();
        entity.LegacyCompanyCode = legacyCompanyCode;
        entity.IsActive = dto.IsActive;

        AddIfNew(_context.PayrollLoanPolicies, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollBonusPolicyDto> UpsertBonusPolicyAsync(Guid tenantId, UpsertPayrollBonusPolicyDto dto, CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(dto.Code);
        var entity = await FindForUpsertAsync(_context.PayrollBonusPolicies, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollBonusPolicies
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Code == code, cancellationToken)
            ?? new PayrollBonusPolicy { TenantId = tenantId };

        entity.Code = code;
        entity.Name = RequireText(dto.Name, nameof(dto.Name));
        entity.CalculationType = dto.CalculationType;
        entity.Amount = dto.Amount;
        entity.Taxable = dto.Taxable;
        entity.SeparateTax = dto.SeparateTax;
        entity.TaxFreeCeiling = dto.TaxFreeCeiling;
        entity.MinimumToTax = dto.MinimumToTax;
        entity.AnnualSalaryPercentToTax = dto.AnnualSalaryPercentToTax;
        entity.TaxRate = dto.TaxRate;
        entity.NextPayPeriod = dto.NextPayPeriod;
        entity.LastPayPeriod = dto.LastPayPeriod;
        entity.NextPayPeriodDate = dto.NextPayPeriodDate?.Date;
        entity.LastPayPeriodDate = dto.LastPayPeriodDate?.Date;
        entity.Category = TrimOrNull(dto.Category);
        entity.Cycle = TrimOrNull(dto.Cycle);
        entity.PaySeparate = dto.PaySeparate;
        entity.PerAnnual = dto.PerAnnual;
        entity.MinimumMonths = dto.MinimumMonths;
        entity.Prorate = dto.Prorate;
        entity.IsActive = dto.IsActive;

        AddIfNew(_context.PayrollBonusPolicies, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollBonusRuleDto> UpsertBonusRuleAsync(Guid tenantId, UpsertPayrollBonusRuleDto dto, CancellationToken cancellationToken = default)
    {
        var bonusCode = NormalizeCode(dto.BonusCode);
        var groupCode = RequireText(dto.GroupCode, nameof(dto.GroupCode));
        var entity = await FindForUpsertAsync(_context.PayrollBonusRules, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollBonusRules
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.BonusCode == bonusCode && e.GroupCode == groupCode, cancellationToken)
            ?? new PayrollBonusRule { TenantId = tenantId };

        entity.BonusCode = bonusCode;
        entity.GroupCode = groupCode;
        entity.CalculationType = dto.CalculationType;
        entity.Amount = dto.Amount;
        entity.Applicable = dto.Applicable;
        entity.LegacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode, 5);

        AddIfNew(_context.PayrollBonusRules, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<PayrollBonusExceptionDto>> GetBonusExceptionsAsync(
        Guid tenantId,
        string? bonusCode = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PayrollBonusExceptions
            .AsNoTracking()
            .Include(e => e.EmployeeProfile)
                .ThenInclude(e => e!.SalaryBasis)
            .Where(e => e.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(bonusCode))
        {
            var normalizedBonusCode = NormalizeCode(bonusCode);
            query = query.Where(e => e.BonusCode == normalizedBonusCode);
        }

        var entries = await query
            .OrderBy(e => e.BonusCode)
            .ThenBy(e => e.EmployeeNumber)
            .ToListAsync(cancellationToken);

        return entries.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<PayrollBonusExceptionDto>> SaveBonusExceptionsAsync(
        Guid tenantId,
        PayrollBonusExceptionBulkSaveDto dto,
        CancellationToken cancellationToken = default)
    {
        var bonusCode = NormalizeCode(dto.BonusCode);
        var policy = await _context.PayrollBonusPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Code == bonusCode, cancellationToken)
            ?? throw new KeyNotFoundException("Payroll bonus policy was not found.");

        var profileIds = dto.Entries
            .Where(e => e.EmployeeProfileId.HasValue && e.EmployeeProfileId.Value != Guid.Empty)
            .Select(e => e.EmployeeProfileId!.Value)
            .Distinct()
            .ToList();

        var employeeNumbers = dto.Entries
            .Select(e => e.EmployeeNumber?.Trim())
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var profiles = await PayrollProfileQuery(tenantId)
            .Where(e =>
                profileIds.Contains(e.Id) ||
                employeeNumbers.Contains(e.EmployeeNumber) ||
                (e.LegacyEmployeeNumber != null && employeeNumbers.Contains(e.LegacyEmployeeNumber)))
            .ToListAsync(cancellationToken);

        var profilesById = profiles.ToDictionary(e => e.Id);
        var profilesByNumber = profiles
            .GroupBy(e => e.EmployeeNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => e.First(), StringComparer.OrdinalIgnoreCase);

        var profilesByLegacyNumber = profiles
            .Where(e => !string.IsNullOrWhiteSpace(e.LegacyEmployeeNumber))
            .GroupBy(e => e.LegacyEmployeeNumber!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => e.First(), StringComparer.OrdinalIgnoreCase);

        var existingEntries = await _context.PayrollBonusExceptions
            .Where(e => e.TenantId == tenantId && e.BonusCode == bonusCode)
            .ToListAsync(cancellationToken);

        var existingById = existingEntries.ToDictionary(e => e.Id);
        var existingByProfile = existingEntries
            .Where(e => e.EmployeeProfileId.HasValue)
            .GroupBy(e => e.EmployeeProfileId!.Value)
            .ToDictionary(e => e.Key, e => e.First());
        var existingByEmployeeNumber = existingEntries
            .GroupBy(e => e.EmployeeNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => e.First(), StringComparer.OrdinalIgnoreCase);
        var legacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode, 5);
        var now = DateTime.UtcNow;

        foreach (var line in dto.Entries)
        {
            PayrollBonusException? existing = null;
            if (line.Id.HasValue && line.Id.Value != Guid.Empty)
            {
                existingById.TryGetValue(line.Id.Value, out existing);
            }

            if (existing == null && line.EmployeeProfileId.HasValue && line.EmployeeProfileId.Value != Guid.Empty)
            {
                existingByProfile.TryGetValue(line.EmployeeProfileId.Value, out existing);
            }

            var employeeNumber = line.EmployeeNumber?.Trim();
            if (existing == null && !string.IsNullOrWhiteSpace(employeeNumber))
            {
                existingByEmployeeNumber.TryGetValue(employeeNumber, out existing);
            }

            if (!line.IsSelected)
            {
                if (existing != null)
                {
                    _context.PayrollBonusExceptions.Remove(existing);
                    if (existing.EmployeeProfileId.HasValue)
                    {
                        existingByProfile.Remove(existing.EmployeeProfileId.Value);
                    }

                    existingByEmployeeNumber.Remove(existing.EmployeeNumber);
                }

                continue;
            }

            PayrollEmployeeProfile? profile = null;
            if (line.EmployeeProfileId.HasValue && line.EmployeeProfileId.Value != Guid.Empty)
            {
                profilesById.TryGetValue(line.EmployeeProfileId.Value, out profile);
            }

            if (profile == null && !string.IsNullOrWhiteSpace(employeeNumber))
            {
                profilesByNumber.TryGetValue(employeeNumber, out profile);
                profile ??= profilesByLegacyNumber.GetValueOrDefault(employeeNumber);
            }

            if (profile == null)
            {
                throw new KeyNotFoundException($"Payroll employee profile not found for {line.EmployeeNumber}.");
            }

            var entity = existing ?? existingByProfile.GetValueOrDefault(profile.Id) ?? existingByEmployeeNumber.GetValueOrDefault(profile.EmployeeNumber);
            entity ??= new PayrollBonusException { TenantId = tenantId };

            entity.BonusCode = bonusCode;
            entity.EmployeeProfileId = profile.Id;
            entity.EmployeeNumber = profile.EmployeeNumber;
            entity.EmployeeName = profile.Employee.FullName;
            entity.CalculationType = line.CalculationType;
            entity.Amount = Math.Round(line.Amount, 2);
            entity.Taxable = line.Taxable ?? policy.Taxable;
            entity.Applicable = line.Applicable;
            entity.CurrencyCode = NormalizeOptionalCurrency(line.CurrencyCode) ?? "GHS";
            entity.LegacyCompanyCode = NormalizeOptionalLegacyCode(line.LegacyCompanyCode ?? legacyCompanyCode, 5);
            entity.UpdatedAt = now;

            AddIfNew(_context.PayrollBonusExceptions, entity);
            existingByProfile[profile.Id] = entity;
            existingByEmployeeNumber[profile.EmployeeNumber] = entity;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await GetBonusExceptionsAsync(tenantId, bonusCode, cancellationToken);
    }

    public async Task<PayrollBackpayPolicyDto> UpsertBackpayPolicyAsync(Guid tenantId, UpsertPayrollBackpayPolicyDto dto, CancellationToken cancellationToken = default)
    {
        var operationType = NormalizeBackpayOperation(dto.OperationType);
        var entity = await FindForUpsertAsync(_context.PayrollBackpayPolicies, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollBackpayPolicies
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.OperationType == operationType, cancellationToken)
            ?? new PayrollBackpayPolicy { TenantId = tenantId };

        entity.OperationType = operationType;
        entity.CalculationType = dto.CalculationType;
        entity.Amount = dto.Amount;
        entity.NumberOfMonths = dto.NumberOfMonths;
        entity.EffectiveDate = dto.EffectiveDate?.Date;
        entity.ApplyTax = dto.ApplyTax;
        entity.ApplySsf = dto.ApplySsf;
        entity.MinimumServiceMode = TrimOrNull(dto.MinimumServiceMode);
        entity.MinimumServiceValue = dto.MinimumServiceValue;
        entity.MinimumServiceDate = dto.MinimumServiceDate?.Date;
        entity.CategoryType = NormalizeBackpayCategory(dto.CategoryType);
        entity.LegacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode, 5);

        AddIfNew(_context.PayrollBackpayPolicies, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollBackpayRuleDto> UpsertBackpayRuleAsync(Guid tenantId, UpsertPayrollBackpayRuleDto dto, CancellationToken cancellationToken = default)
    {
        var operationType = NormalizeBackpayOperation(dto.OperationType);
        var categoryType = NormalizeBackpayCategory(dto.CategoryType);
        var categoryCode = RequireText(dto.CategoryCode, nameof(dto.CategoryCode));
        var entity = await FindForUpsertAsync(_context.PayrollBackpayRules, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollBackpayRules
                .FirstOrDefaultAsync(e =>
                    e.TenantId == tenantId &&
                    e.OperationType == operationType &&
                    e.CategoryType == categoryType &&
                    e.CategoryCode == categoryCode,
                    cancellationToken)
            ?? new PayrollBackpayRule { TenantId = tenantId };

        entity.OperationType = operationType;
        entity.CategoryType = categoryType;
        entity.CategoryCode = categoryCode;
        entity.CategoryName = TrimOrNull(dto.CategoryName);
        entity.CalculationType = dto.CalculationType;
        entity.Amount = dto.Amount;
        entity.Applicable = dto.Applicable;
        entity.LegacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode, 5);

        AddIfNew(_context.PayrollBackpayRules, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollBackpayExceptionDto> UpsertBackpayExceptionAsync(Guid tenantId, UpsertPayrollBackpayExceptionDto dto, CancellationToken cancellationToken = default)
    {
        var operationType = NormalizeBackpayOperation(dto.OperationType);
        var employeeNumber = RequireText(dto.EmployeeNumber, nameof(dto.EmployeeNumber));
        var entity = await FindForUpsertAsync(_context.PayrollBackpayExceptions, tenantId, dto.Id, cancellationToken)
            ?? await _context.PayrollBackpayExceptions
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.OperationType == operationType && e.EmployeeNumber == employeeNumber, cancellationToken)
            ?? new PayrollBackpayException { TenantId = tenantId };

        entity.OperationType = operationType;
        entity.EmployeeProfileId = dto.EmployeeProfileId.HasValue && dto.EmployeeProfileId.Value != Guid.Empty ? dto.EmployeeProfileId : null;
        entity.EmployeeNumber = employeeNumber;
        entity.EmployeeName = RequireText(dto.EmployeeName, nameof(dto.EmployeeName));
        entity.CalculationType = dto.CalculationType;
        entity.Amount = dto.Amount;
        entity.Applicable = dto.Applicable;
        entity.LegacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode, 5);

        AddIfNew(_context.PayrollBackpayExceptions, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollJournalMappingDto> UpsertJournalMappingAsync(Guid tenantId, UpsertPayrollJournalMappingDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await FindForUpsertAsync(_context.PayrollJournalMappings, tenantId, dto.Id, cancellationToken)
            ?? new PayrollJournalMapping { TenantId = tenantId };

        entity.TransactionType = RequireText(dto.TransactionType, nameof(dto.TransactionType)).Trim();
        entity.ComponentCode = string.IsNullOrWhiteSpace(dto.ComponentCode) ? null : NormalizeCode(dto.ComponentCode);
        entity.Description = RequireText(dto.Description, nameof(dto.Description));
        entity.DebitCredit = NormalizeDebitCredit(dto.DebitCredit);
        entity.AccountCode = RequireText(dto.AccountCode, nameof(dto.AccountCode)).Trim();
        entity.AccountType = string.IsNullOrWhiteSpace(dto.AccountType) ? null : dto.AccountType.Trim();
        entity.IsActive = dto.IsActive;

        AddIfNew(_context.PayrollJournalMappings, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<PayrollEmployeeProfileDto>> GetEmployeeProfilesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
    {
        var query = PayrollProfileQuery(tenantId);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(e =>
                e.EmployeeNumber.Contains(term) ||
                (e.LegacyEmployeeNumber != null && e.LegacyEmployeeNumber.Contains(term)) ||
                e.Employee.FirstName.Contains(term) ||
                e.Employee.LastName.Contains(term));
        }

        var profiles = await query
            .OrderBy(e => e.EmployeeNumber)
            .Take(250)
            .ToListAsync(cancellationToken);

        return profiles.Select(ToDto).ToList();
    }

    public async Task<PayrollEmployeeProfileDto> UpsertEmployeeProfileAsync(Guid tenantId, UpsertPayrollEmployeeProfileDto dto, CancellationToken cancellationToken = default)
    {
        var employee = await _context.Employees
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == dto.EmployeeId, cancellationToken)
            ?? throw new KeyNotFoundException("Employee not found.");

        var profile = await _context.PayrollEmployeeProfiles
            .Include(e => e.SalaryBasis)
            .Include(e => e.PaymentMethods)
            .Include(e => e.EmployeeComponents)
                .ThenInclude(e => e.PayrollComponent)
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && (e.Id == dto.Id || e.EmployeeId == dto.EmployeeId), cancellationToken);

        profile ??= new PayrollEmployeeProfile { TenantId = tenantId, EmployeeId = employee.Id };
        profile.EmployeeNumber = string.IsNullOrWhiteSpace(dto.EmployeeNumber) ? employee.EmployeeNumber : dto.EmployeeNumber.Trim();
        profile.LegacyEmployeeId = TrimOrNull(dto.LegacyEmployeeId);
        profile.LegacyEmployeeNumber = TrimOrNull(dto.LegacyEmployeeNumber);
        profile.PayrollActive = dto.PayrollActive;
        profile.PayTax = dto.PayTax;
        profile.SsfApplicable = dto.SsfApplicable;
        profile.GrossUp = dto.GrossUp;
        profile.Tier2Only = dto.Tier2Only;
        profile.OvertimeEligible = dto.OvertimeEligible;
        employee.Overtime = dto.OvertimeEligible;
        profile.SsfNumber = TrimOrNull(dto.SsfNumber);
        profile.TinNumber = TrimOrNull(dto.TinNumber);
        profile.CurrencyCode = NormalizeCurrency(dto.CurrencyCode);

        if (profile.Id == Guid.Empty || _context.Entry(profile).State == EntityState.Detached)
        {
            _context.PayrollEmployeeProfiles.Add(profile);
        }

        if (dto.SalaryBasis != null)
        {
            UpsertSalaryBasis(tenantId, profile, dto.SalaryBasis);
        }

        UpsertPaymentMethods(tenantId, profile, dto.PaymentMethods);
        UpsertEmployeeComponents(tenantId, profile, dto.EmployeeComponents);

        await _context.SaveChangesAsync(cancellationToken);

        var result = await PayrollProfileQuery(tenantId)
            .FirstAsync(e => e.Id == profile.Id, cancellationToken);

        return ToDto(result);
    }

    public async Task<IReadOnlyList<PayrollLoanDto>> GetLoansAsync(
        Guid tenantId,
        string? employeeNumber = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = PayrollLoanQuery(tenantId);

        if (!includeInactive)
        {
            query = query.Where(e => e.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(employeeNumber))
        {
            var employeeNo = employeeNumber.Trim();
            query = query.Where(e =>
                e.EmployeeProfile.EmployeeNumber.Contains(employeeNo) ||
                (e.EmployeeProfile.LegacyEmployeeNumber != null && e.EmployeeProfile.LegacyEmployeeNumber.Contains(employeeNo)));
        }

        var loans = await query
            .OrderBy(e => e.EmployeeProfile.EmployeeNumber)
            .ThenBy(e => e.FacilityNumber)
            .Take(500)
            .ToListAsync(cancellationToken);

        return loans.Select(ToDto).ToList();
    }

    public async Task<PayrollLoanDto> UpsertLoanAsync(Guid tenantId, UpsertPayrollLoanDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.AmountGranted <= 0)
        {
            throw new InvalidOperationException("Amount granted must be greater than zero.");
        }

        PayrollEmployeeProfile? profile = null;
        if (dto.EmployeeProfileId.HasValue && dto.EmployeeProfileId.Value != Guid.Empty)
        {
            profile = await PayrollProfileQuery(tenantId)
                .FirstOrDefaultAsync(e => e.Id == dto.EmployeeProfileId.Value, cancellationToken);
        }

        if (profile == null && !string.IsNullOrWhiteSpace(dto.EmployeeNumber))
        {
            var employeeNumber = dto.EmployeeNumber.Trim();
            profile = await PayrollProfileQuery(tenantId)
                .FirstOrDefaultAsync(e =>
                    e.EmployeeNumber == employeeNumber ||
                    (e.LegacyEmployeeNumber != null && e.LegacyEmployeeNumber == employeeNumber),
                    cancellationToken);
        }

        if (profile == null)
        {
            throw new KeyNotFoundException("Payroll employee profile not found.");
        }

        var entity = await _context.PayrollLoans
            .Include(e => e.Schedules)
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && dto.Id.HasValue && e.Id == dto.Id.Value, cancellationToken);

        PayrollLoanPolicy? loanPolicy = null;
        if (dto.LoanPolicyId.HasValue && dto.LoanPolicyId.Value != Guid.Empty)
        {
            loanPolicy = await _context.PayrollLoanPolicies
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == dto.LoanPolicyId.Value, cancellationToken);
        }

        var amountGranted = Math.Round(dto.AmountGranted, 2);
        if (loanPolicy?.MaxLoanAmount is > 0 && amountGranted > loanPolicy.MaxLoanAmount.Value)
        {
            throw new InvalidOperationException("Amount granted exceeds the selected loan type maximum.");
        }

        var dateGranted = dto.DateGranted == default ? DateTime.UtcNow.Date : dto.DateGranted.Date;
        var paymentStartDate = (dto.PaymentStartDate ?? dateGranted).Date;
        var repaymentMode = NormalizeLoanRepaymentMode(dto.RepaymentMode);
        var interestRate = Math.Round(dto.InterestRatePercent ?? loanPolicy?.InterestRatePercent ?? 0m, 4);
        var interestType = NormalizeLoanInterestType(loanPolicy?.InterestType);
        var basicSalary = profile.SalaryBasis?.MonthlyBasicSalary ?? 0m;
        var monthlyInput = Math.Round(dto.MonthlyRepaymentAmount, 2);
        var numberOfRepayments = dto.NumberOfRepayments > 0
            ? dto.NumberOfRepayments
            : dto.PaymentEndDate.HasValue
                ? InclusiveMonthCount(paymentStartDate, dto.PaymentEndDate.Value.Date)
                : 0;
        var repaymentAmount = CalculateLoanPrincipalRepaymentAmount(amountGranted, monthlyInput, repaymentMode, numberOfRepayments, basicSalary);

        if (numberOfRepayments <= 0 && repaymentAmount > 0)
        {
            numberOfRepayments = (int)Math.Ceiling(amountGranted / repaymentAmount);
        }

        if (numberOfRepayments <= 0)
        {
            numberOfRepayments = 1;
        }

        if (loanPolicy?.MaxPaybackPeriods is > 0 && numberOfRepayments > loanPolicy.MaxPaybackPeriods.Value)
        {
            throw new InvalidOperationException("No repayments exceeds the selected loan type limit.");
        }

        if (repaymentAmount <= 0)
        {
            repaymentAmount = Math.Round(amountGranted / numberOfRepayments, 2);
        }

        var applyInterest = loanPolicy?.ApplyInterest ?? (interestRate > 0 || dto.TotalInterest.GetValueOrDefault() > 0);
        var totalInterest = CalculateLoanTotalInterest(amountGranted, repaymentAmount, numberOfRepayments, interestRate, interestType, applyInterest);
        var interestRepaymentAmount = Math.Round(totalInterest <= 0 ? 0 : totalInterest / numberOfRepayments, 2);
        var totalMonthlyCommitment = Math.Round(repaymentAmount + interestRepaymentAmount, 2);
        var debitRatioLimit = loanPolicy?.MaxDebitRatioPercent;
        if (!debitRatioLimit.HasValue || debitRatioLimit <= 0)
        {
            debitRatioLimit = await _context.PayrollParameterSets
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId && e.IsActive)
                .OrderBy(e => e.Code)
                .Select(e => e.DebitRatio)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (debitRatioLimit is > 0 && basicSalary > 0)
        {
            var existingCommitment = profile.Loans
                .Where(e => e.IsActive &&
                            e.Status != "Closed" &&
                            (!dto.Id.HasValue || e.Id != dto.Id.Value) &&
                            !string.Equals(e.FacilityNumber, dto.FacilityNumber, StringComparison.OrdinalIgnoreCase))
                .Sum(e => e.MonthlyRepaymentAmount + e.InterestRepaymentAmount);
            var employeeDebtRatio = Math.Round((existingCommitment + totalMonthlyCommitment) / basicSalary * 100m, 2);
            if (employeeDebtRatio > debitRatioLimit.Value)
            {
                throw new InvalidOperationException($"Employee debt ratio {employeeDebtRatio:N2}% exceeds the allowed debit ratio {debitRatioLimit.Value:N2}%.");
            }
        }

        var status = NormalizeLoanStatus(dto.Status);
        if (status == "Suspended" && !dto.SuspensionStartDate.HasValue)
        {
            throw new InvalidOperationException("Suspension start date is required when loan status is Suspended.");
        }

        var facilityNumber = TrimOrNull(dto.FacilityNumber);
        if (string.IsNullOrWhiteSpace(facilityNumber))
        {
            facilityNumber = entity?.FacilityNumber ?? await GenerateLoanFacilityNumberAsync(tenantId, cancellationToken);
        }

        entity ??= await _context.PayrollLoans
            .Include(e => e.Schedules)
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.EmployeeProfileId == profile.Id && e.FacilityNumber == facilityNumber, cancellationToken);

        var entityId = entity?.Id;
        if (await _context.PayrollLoans.AnyAsync(e =>
                e.TenantId == tenantId &&
                e.FacilityNumber == facilityNumber &&
                (!entityId.HasValue || e.Id != entityId.Value),
                cancellationToken))
        {
            throw new InvalidOperationException("Loan reference already exists.");
        }

        var hasPostedRepayments = entity?.Schedules.Any(e =>
            e.AmountPaid > 0 ||
            e.InterestPaid > 0 ||
            e.Posted) == true;
        var paidPrincipal = entity?.Schedules.Sum(e => e.AmountPaid) ?? 0m;
        var outstandingPrincipal = hasPostedRepayments
            ? Math.Max(0, Math.Round(amountGranted - paidPrincipal, 2))
            : Math.Round(dto.OutstandingBalance.GetValueOrDefault(amountGranted), 2);
        if (outstandingPrincipal <= 0 && amountGranted > paidPrincipal)
        {
            outstandingPrincipal = Math.Round(amountGranted - paidPrincipal, 2);
        }

        var suspensionStart = dto.SuspensionStartDate?.Date;
        var suspensionEnd = dto.SuspensionEndDate?.Date;
        if (status == "Suspended" && suspensionStart.HasValue && !suspensionEnd.HasValue && dto.PeriodOfSuspension is > 0)
        {
            suspensionEnd = suspensionStart.Value.AddMonths(dto.PeriodOfSuspension.Value).AddDays(-1);
        }

        var paymentEndDate = dto.PaymentEndDate?.Date ?? paymentStartDate.AddMonths(Math.Max(numberOfRepayments - 1, 0));
        entity ??= new PayrollLoan { TenantId = tenantId };
        entity.EmployeeProfileId = profile.Id;
        entity.LoanPolicyId = loanPolicy?.Id;
        entity.LoanTypeCode = TrimOrNull(dto.LoanTypeCode) ?? loanPolicy?.Code;
        entity.FacilityNumber = facilityNumber;
        entity.DateGranted = dateGranted;
        entity.AmountGranted = amountGranted;
        entity.MonthlyRepaymentAmount = repaymentAmount;
        entity.OutstandingBalance = Math.Round(outstandingPrincipal, 2);
        entity.InterestRatePercent = interestRate;
        entity.TotalInterest = totalInterest;
        entity.InterestRepaymentAmount = interestRepaymentAmount;
        entity.RepaymentMode = repaymentMode;
        entity.PaymentStartDate = paymentStartDate;
        entity.PaymentEndDate = paymentEndDate;
        entity.NumberOfRepayments = numberOfRepayments;
        entity.Status = status;
        entity.PeriodOfSuspension = dto.PeriodOfSuspension;
        entity.SuspensionStartDate = suspensionStart;
        entity.SuspensionEndDate = suspensionEnd;
        entity.SuspensionNarration = TrimOrNull(dto.SuspensionNarration);
        entity.GeneralRemarks = TrimOrNull(dto.GeneralRemarks);
        entity.IsActive = dto.IsActive && status != "Closed";
        entity.UpdatedAt = DateTime.UtcNow;

        AddIfNew(_context.PayrollLoans, entity);
        if (!hasPostedRepayments)
        {
            _context.PayrollLoanSchedules.RemoveRange(entity.Schedules);
            RebuildLoanSchedules(tenantId, entity, interestType);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var result = await PayrollLoanQuery(tenantId)
            .FirstAsync(e => e.Id == entity.Id, cancellationToken);

        return ToDto(result);
    }

    public async Task<PayrollLoanRepaymentDto> PostLoanRepaymentAsync(
        Guid tenantId,
        PayrollLoanRepaymentRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto.RepaymentAmount <= 0)
        {
            throw new InvalidOperationException("Repayment Amount must be greater than zero.");
        }

        var repaymentAmount = Math.Round(dto.RepaymentAmount, 2);
        var actualRepaymentDate = (dto.ActualRepaymentDate ?? DateTime.UtcNow).Date;
        var facilityNumber = dto.FacilityNumber.Trim();
        var employeeNumber = dto.EmployeeNumber.Trim();
        var activeParameters = await _context.PayrollParameterSets
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.IsActive)
            .OrderBy(e => e.Code)
            .FirstOrDefaultAsync(cancellationToken);
        var currentPeriodTo = activeParameters?.CurrentPeriodTo?.Date;

        if (activeParameters?.CurrentPeriodFrom.HasValue == true &&
            activeParameters.CurrentPeriodTo.HasValue &&
            (actualRepaymentDate < activeParameters.CurrentPeriodFrom.Value.Date ||
             actualRepaymentDate > activeParameters.CurrentPeriodTo.Value.Date))
        {
            var payrollPeriodLabel = FormatPayrollPeriodMonth(activeParameters.CurrentPeriodFrom, activeParameters.CurrentPeriodTo);
            throw new InvalidOperationException(
                $"Actual repayment date must be within the current payroll period {payrollPeriodLabel}.");
        }

        if ((!dto.PayrollLoanId.HasValue || dto.PayrollLoanId.Value == Guid.Empty) &&
            (string.IsNullOrWhiteSpace(facilityNumber) || string.IsNullOrWhiteSpace(employeeNumber)))
        {
            throw new InvalidOperationException("Employee No and Facility No are required.");
        }

        var query = PayrollLoanQuery(tenantId);
        PayrollLoan? loan = null;

        if (dto.PayrollLoanId.HasValue && dto.PayrollLoanId.Value != Guid.Empty)
        {
            loan = await query.FirstOrDefaultAsync(e => e.Id == dto.PayrollLoanId.Value, cancellationToken);
        }

        loan ??= await query.FirstOrDefaultAsync(e =>
            e.FacilityNumber == facilityNumber &&
            (e.EmployeeProfile.EmployeeNumber == employeeNumber ||
             (e.EmployeeProfile.LegacyEmployeeNumber != null && e.EmployeeProfile.LegacyEmployeeNumber == employeeNumber)),
            cancellationToken);

        if (loan == null)
        {
            throw new KeyNotFoundException("Loan facility not found for the selected employee.");
        }

        if (!loan.IsActive || !string.Equals(loan.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only active loans can receive repayments.");
        }

        if (loan.OutstandingBalance <= 0)
        {
            throw new InvalidOperationException("The selected loan has no outstanding balance.");
        }

        var schedule = loan.Schedules
            .OrderBy(e => e.SequenceNo)
            .FirstOrDefault(e =>
                RemainingScheduleAmount(e) > 0 &&
                (!currentPeriodTo.HasValue || e.RepaymentDate.Date <= currentPeriodTo.Value));

        if (schedule == null)
        {
            var payrollPeriodLabel = FormatPayrollPeriodMonth(activeParameters?.CurrentPeriodFrom, activeParameters?.CurrentPeriodTo);
            throw new InvalidOperationException(
                $"No unpaid repayment schedule is due for the selected loan in the current payroll period {payrollPeriodLabel}.");
        }

        var totalDue = RemainingScheduleAmount(schedule);
        if (repaymentAmount > totalDue)
        {
            throw new InvalidOperationException($"Repayment Amount cannot exceed the next scheduled balance of {totalDue:N2}.");
        }

        var principalDue = Math.Max(0, Math.Round(schedule.PrincipalAmount - schedule.AmountPaid, 2));
        var interestDue = Math.Max(0, Math.Round(schedule.InterestAmount - schedule.InterestPaid, 2));
        var principalPaid = Math.Min(repaymentAmount, principalDue);
        var interestPaid = Math.Min(Math.Round(repaymentAmount - principalPaid, 2), interestDue);

        if (principalPaid <= 0 && interestPaid <= 0)
        {
            throw new InvalidOperationException("The selected repayment schedule is already fully paid.");
        }

        schedule.AmountPaid = Math.Round(schedule.AmountPaid + principalPaid, 2);
        schedule.InterestPaid = Math.Round(schedule.InterestPaid + interestPaid, 2);
        schedule.ActualRepaymentDate = actualRepaymentDate;
        schedule.Posted = RemainingScheduleAmount(schedule) <= 0;
        schedule.UpdatedAt = DateTime.UtcNow;

        loan.OutstandingBalance = Math.Max(0, Math.Round(loan.OutstandingBalance - principalPaid, 2));
        var remainingLoanBalance = Math.Round(loan.Schedules.Sum(RemainingSchedulePrincipal), 2);
        if (remainingLoanBalance <= 0 || loan.OutstandingBalance <= 0)
        {
            loan.OutstandingBalance = 0;
            loan.Status = "Closed";
            loan.IsActive = false;
            loan.PaymentEndDate = actualRepaymentDate;
        }

        loan.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var result = await PayrollLoanQuery(tenantId)
            .FirstAsync(e => e.Id == loan.Id, cancellationToken);

        return new PayrollLoanRepaymentDto
        {
            PayrollLoanId = result.Id,
            PayrollLoanScheduleId = schedule.Id,
            LoanSequenceNo = schedule.SequenceNo,
            FacilityNumber = result.FacilityNumber,
            EmployeeNumber = result.EmployeeProfile.EmployeeNumber,
            RepaymentAmount = Math.Round(principalPaid + interestPaid, 2),
            PrincipalPaid = principalPaid,
            InterestPaid = interestPaid,
            LoanBalance = result.OutstandingBalance,
            ActualRepaymentDate = actualRepaymentDate,
            Loan = ToDto(result)
        };
    }

    public async Task<IReadOnlyList<PayrollSalaryAdvanceDto>> GetSalaryAdvancesAsync(
        Guid tenantId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? employeeNumber = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PayrollSalaryAdvances
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId);

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            query = query.Where(e => e.AdvanceDate >= from);
        }

        if (toDate.HasValue)
        {
            var to = toDate.Value.Date;
            query = query.Where(e => e.AdvanceDate <= to);
        }

        if (!string.IsNullOrWhiteSpace(employeeNumber))
        {
            var employeeNo = employeeNumber.Trim();
            query = query.Where(e => e.EmployeeNumber.Contains(employeeNo));
        }

        var advances = await query
            .OrderByDescending(e => e.AdvanceDate)
            .ThenBy(e => e.EmployeeNumber)
            .Take(500)
            .ToListAsync(cancellationToken);

        return advances.Select(ToDto).ToList();
    }

    public async Task<PayrollSalaryAdvanceDto> UpsertSalaryAdvanceAsync(Guid tenantId, UpsertPayrollSalaryAdvanceDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.AdvanceAmount <= 0)
        {
            throw new InvalidOperationException("Advance amount must be greater than zero.");
        }

        var advanceDate = dto.AdvanceDate == default ? DateTime.UtcNow.Date : dto.AdvanceDate.Date;
        var activeParameters = await _context.PayrollParameterSets
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.IsActive)
            .OrderBy(e => e.Code)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeParameters?.CurrentPeriodFrom.HasValue == true &&
            activeParameters.CurrentPeriodTo.HasValue &&
            (advanceDate < activeParameters.CurrentPeriodFrom.Value.Date || advanceDate > activeParameters.CurrentPeriodTo.Value.Date))
        {
            var payrollPeriodLabel = FormatPayrollPeriodMonth(activeParameters.CurrentPeriodFrom, activeParameters.CurrentPeriodTo);
            throw new InvalidOperationException(
                $"Advance Date must be within the current payroll period {payrollPeriodLabel}.");
        }

        PayrollEmployeeProfile? profile = null;
        if (dto.EmployeeProfileId.HasValue && dto.EmployeeProfileId.Value != Guid.Empty)
        {
            profile = await PayrollProfileQuery(tenantId)
                .FirstOrDefaultAsync(e => e.Id == dto.EmployeeProfileId.Value, cancellationToken);
        }

        if (profile == null && !string.IsNullOrWhiteSpace(dto.EmployeeNumber))
        {
            var employeeNumber = dto.EmployeeNumber.Trim();
            profile = await PayrollProfileQuery(tenantId)
                .FirstOrDefaultAsync(e =>
                    e.EmployeeNumber == employeeNumber ||
                    (e.LegacyEmployeeNumber != null && e.LegacyEmployeeNumber == employeeNumber),
                    cancellationToken);
        }

        if (profile == null)
        {
            throw new KeyNotFoundException("Payroll employee profile not found.");
        }

        var entity = await FindForUpsertAsync(_context.PayrollSalaryAdvances, tenantId, dto.Id, cancellationToken)
            ?? new PayrollSalaryAdvance { TenantId = tenantId };

        entity.EmployeeProfileId = profile.Id;
        entity.EmployeeId = profile.EmployeeId;
        entity.EmployeeNumber = profile.EmployeeNumber;
        entity.EmployeeName = profile.Employee.FullName;
        entity.AdvanceDate = advanceDate;
        entity.AdvanceAmount = Math.Round(dto.AdvanceAmount, 2);
        entity.Description = TrimOrNull(dto.Description);
        entity.LegacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode ?? activeParameters?.LegacyCompanyCode, 5);
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        AddIfNew(_context.PayrollSalaryAdvances, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<PayrollEmployeeTaxReliefDto>> GetEmployeeTaxReliefsAsync(
        Guid tenantId,
        string? reliefCode = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PayrollEmployeeTaxReliefs
            .AsNoTracking()
            .Include(e => e.EmployeeProfile)
                .ThenInclude(e => e.SalaryBasis)
            .Where(e => e.TenantId == tenantId && e.IsActive);

        if (!string.IsNullOrWhiteSpace(reliefCode))
        {
            var code = NormalizeCode(reliefCode);
            query = query.Where(e => e.ReliefCode.Trim().ToUpper() == code);
        }

        var entries = await query
            .OrderBy(e => e.ReliefCode)
            .ThenBy(e => e.EmployeeNumber)
            .ToListAsync(cancellationToken);

        return entries.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<PayrollEmployeeTaxReliefDto>> SaveEmployeeTaxReliefsAsync(
        Guid tenantId,
        PayrollEmployeeTaxReliefBulkSaveDto dto,
        CancellationToken cancellationToken = default)
    {
        var reliefCode = NormalizeCode(dto.ReliefCode);
        var reliefName = RequireText(dto.ReliefName, "Tax relief");

        if (dto.Amount < 0)
        {
            throw new InvalidOperationException("Relief amount cannot be negative.");
        }

        var activeParameters = await _context.PayrollParameterSets
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.IsActive)
            .OrderBy(e => e.Code)
            .FirstOrDefaultAsync(cancellationToken);

        var profileIds = dto.Entries
            .Where(e => e.EmployeeProfileId.HasValue && e.EmployeeProfileId.Value != Guid.Empty)
            .Select(e => e.EmployeeProfileId!.Value)
            .Distinct()
            .ToList();

        var employeeNumbers = dto.Entries
            .Select(e => e.EmployeeNumber?.Trim())
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var profiles = await PayrollProfileQuery(tenantId)
            .Where(e =>
                profileIds.Contains(e.Id) ||
                employeeNumbers.Contains(e.EmployeeNumber) ||
                (e.LegacyEmployeeNumber != null && employeeNumbers.Contains(e.LegacyEmployeeNumber)))
            .ToListAsync(cancellationToken);

        var profilesById = profiles.ToDictionary(e => e.Id);
        var profilesByNumber = profiles
            .GroupBy(e => e.EmployeeNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => e.First(), StringComparer.OrdinalIgnoreCase);

        var profilesByLegacyNumber = profiles
            .Where(e => !string.IsNullOrWhiteSpace(e.LegacyEmployeeNumber))
            .GroupBy(e => e.LegacyEmployeeNumber!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => e.First(), StringComparer.OrdinalIgnoreCase);

        var existingEntries = await _context.PayrollEmployeeTaxReliefs
            .Where(e => e.TenantId == tenantId && e.ReliefCode == reliefCode)
            .ToListAsync(cancellationToken);

        var existingByProfile = existingEntries.ToDictionary(e => e.EmployeeProfileId);
        var now = DateTime.UtcNow;
        var legacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode ?? activeParameters?.LegacyCompanyCode, 5);

        foreach (var line in dto.Entries)
        {
            PayrollEmployeeProfile? profile = null;
            if (line.EmployeeProfileId.HasValue && line.EmployeeProfileId.Value != Guid.Empty)
            {
                profilesById.TryGetValue(line.EmployeeProfileId.Value, out profile);
            }

            var employeeNumber = line.EmployeeNumber?.Trim();
            if (profile == null && !string.IsNullOrWhiteSpace(employeeNumber))
            {
                profilesByNumber.TryGetValue(employeeNumber, out profile);
                profile ??= profilesByLegacyNumber.GetValueOrDefault(employeeNumber);
            }

            if (profile == null)
            {
                if (line.IsSelected)
                {
                    throw new KeyNotFoundException($"Payroll employee profile not found for {line.EmployeeNumber}.");
                }

                continue;
            }

            if (!line.IsSelected)
            {
                if (existingByProfile.TryGetValue(profile.Id, out var inactiveEntry) && inactiveEntry.IsActive)
                {
                    inactiveEntry.IsActive = false;
                    inactiveEntry.UpdatedAt = now;
                }

                continue;
            }

            var amount = Math.Round(line.Amount ?? dto.Amount, 2);
            var factor = Math.Round(line.Factor.GetValueOrDefault(1), 4);
            if (amount < 0)
            {
                throw new InvalidOperationException("Relief amount cannot be negative.");
            }

            if (factor <= 0)
            {
                throw new InvalidOperationException("Relief factor must be greater than zero.");
            }

            var entity = line.Id.HasValue && line.Id.Value != Guid.Empty
                ? existingEntries.FirstOrDefault(e => e.Id == line.Id.Value) ?? existingByProfile.GetValueOrDefault(profile.Id)
                : existingByProfile.GetValueOrDefault(profile.Id);

            entity ??= new PayrollEmployeeTaxRelief { TenantId = tenantId };

            entity.EmployeeProfileId = profile.Id;
            entity.EmployeeId = profile.EmployeeId;
            entity.EmployeeNumber = profile.EmployeeNumber;
            entity.EmployeeName = profile.Employee.FullName;
            entity.ReliefCode = reliefCode;
            entity.ReliefName = reliefName;
            entity.CalculationType = dto.CalculationType;
            entity.Amount = amount;
            entity.Factor = factor;
            entity.LegacyCompanyCode = legacyCompanyCode;
            entity.IsActive = true;
            entity.UpdatedAt = now;

            AddIfNew(_context.PayrollEmployeeTaxReliefs, entity);
            existingByProfile[profile.Id] = entity;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await GetEmployeeTaxReliefsAsync(tenantId, reliefCode, cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollEmployeeComponentExceptionDto>> GetEmployeeComponentExceptionsAsync(
        Guid tenantId,
        Guid? payrollComponentId = null,
        string? componentCode = null,
        PayrollComponentType? componentType = null,
        CancellationToken cancellationToken = default)
    {
        var component = await FindPayrollComponentAsync(tenantId, payrollComponentId, componentCode, componentType, cancellationToken);
        if (component == null)
        {
            return [];
        }

        var entries = await _context.PayrollEmployeeComponents
            .AsNoTracking()
            .Include(e => e.EmployeeProfile)
                .ThenInclude(e => e.Employee)
            .Include(e => e.EmployeeProfile)
                .ThenInclude(e => e.SalaryBasis)
            .Include(e => e.PayrollComponent)
            .Where(e => e.TenantId == tenantId && e.PayrollComponentId == component.Id)
            .OrderBy(e => e.EmployeeProfile.EmployeeNumber)
            .ToListAsync(cancellationToken);

        return entries.Select(ToComponentExceptionDto).ToList();
    }

    public async Task<IReadOnlyList<PayrollEmployeeComponentExceptionDto>> SaveEmployeeComponentExceptionsAsync(
        Guid tenantId,
        PayrollEmployeeComponentExceptionBulkSaveDto dto,
        CancellationToken cancellationToken = default)
    {
        var component = await FindPayrollComponentAsync(tenantId, dto.PayrollComponentId, dto.ComponentCode, dto.ComponentType, cancellationToken)
            ?? throw new KeyNotFoundException("Payroll allowance/deduction component was not found.");

        var profileIds = dto.Entries
            .Where(e => e.EmployeeProfileId.HasValue && e.EmployeeProfileId.Value != Guid.Empty)
            .Select(e => e.EmployeeProfileId!.Value)
            .Distinct()
            .ToList();

        var employeeNumbers = dto.Entries
            .Select(e => e.EmployeeNumber?.Trim())
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var profiles = await _context.PayrollEmployeeProfiles
            .Include(e => e.Employee)
            .Include(e => e.SalaryBasis)
            .Where(e => e.TenantId == tenantId &&
                        (profileIds.Contains(e.Id) ||
                         employeeNumbers.Contains(e.EmployeeNumber) ||
                         (e.LegacyEmployeeNumber != null && employeeNumbers.Contains(e.LegacyEmployeeNumber))))
            .ToListAsync(cancellationToken);

        var profilesById = profiles.ToDictionary(e => e.Id);
        var profilesByNumber = profiles
            .GroupBy(e => e.EmployeeNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => e.First(), StringComparer.OrdinalIgnoreCase);

        var profilesByLegacyNumber = profiles
            .Where(e => !string.IsNullOrWhiteSpace(e.LegacyEmployeeNumber))
            .GroupBy(e => e.LegacyEmployeeNumber!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => e.First(), StringComparer.OrdinalIgnoreCase);

        var existingEntries = await _context.PayrollEmployeeComponents
            .Where(e => e.TenantId == tenantId && e.PayrollComponentId == component.Id)
            .ToListAsync(cancellationToken);

        var existingByProfile = existingEntries.ToDictionary(e => e.EmployeeProfileId);
        var now = DateTime.UtcNow;

        foreach (var line in dto.Entries)
        {
            PayrollEmployeeProfile? profile = null;
            if (line.EmployeeProfileId.HasValue && line.EmployeeProfileId.Value != Guid.Empty)
            {
                profilesById.TryGetValue(line.EmployeeProfileId.Value, out profile);
            }

            var employeeNumber = line.EmployeeNumber?.Trim();
            if (profile == null && !string.IsNullOrWhiteSpace(employeeNumber))
            {
                profilesByNumber.TryGetValue(employeeNumber, out profile);
                profile ??= profilesByLegacyNumber.GetValueOrDefault(employeeNumber);
            }

            if (profile == null)
            {
                if (line.IsSelected)
                {
                    throw new KeyNotFoundException($"Payroll employee profile not found for {line.EmployeeNumber}.");
                }

                continue;
            }

            if (!line.IsSelected)
            {
                if (existingByProfile.TryGetValue(profile.Id, out var removedEntry))
                {
                    _context.PayrollEmployeeComponents.Remove(removedEntry);
                    existingByProfile.Remove(profile.Id);
                }

                continue;
            }

            var entity = line.Id.HasValue && line.Id.Value != Guid.Empty
                ? existingEntries.FirstOrDefault(e => e.Id == line.Id.Value) ?? existingByProfile.GetValueOrDefault(profile.Id)
                : existingByProfile.GetValueOrDefault(profile.Id);

            entity ??= new PayrollEmployeeComponent { TenantId = tenantId };
            entity.EmployeeProfileId = profile.Id;
            entity.PayrollComponentId = component.Id;
            entity.CalculationTypeOverride = line.CalculationTypeOverride ?? component.CalculationType;
            entity.AmountOverride = Math.Round(line.AmountOverride ?? component.Amount, 2);
            entity.RateOverride = Math.Round(line.RateOverride ?? component.Rate, 4);
            entity.TaxableOverride = line.TaxableOverride ?? component.Taxable;
            entity.TaxFreeCeilingOverride = line.TaxFreeCeilingOverride.HasValue
                ? Math.Round(line.TaxFreeCeilingOverride.Value, 2)
                : component.TaxFreeCeiling;
            entity.EmployerAmountOverride = line.EmployerAmountOverride.HasValue
                ? Math.Round(line.EmployerAmountOverride.Value, 2)
                : component.EmployerAmount;
            entity.EmployerTaxableOverride = line.EmployerTaxableOverride ?? component.EmployerTaxable;
            entity.GrossUpOverride = line.GrossUpOverride ?? component.GrossUp;
            entity.CurrencyCodeOverride = NormalizeOptionalCurrency(line.CurrencyCodeOverride) ?? component.CurrencyCode;
            entity.Applicable = line.Applicable;
            entity.EffectiveFrom = line.EffectiveFrom?.Date;
            entity.EffectiveTo = line.EffectiveTo?.Date;
            entity.UpdatedAt = now;

            AddIfNew(_context.PayrollEmployeeComponents, entity);
            existingByProfile[profile.Id] = entity;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await GetEmployeeComponentExceptionsAsync(tenantId, component.Id, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollPromotionArrearsEntryDto>> GetPromotionArrearsAsync(
        Guid tenantId,
        string? employeeNumber = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PayrollPromotionArrears
            .AsNoTracking()
            .Include(e => e.EmployeeProfile)
                .ThenInclude(e => e.SalaryBasis)
            .Where(e => e.TenantId == tenantId);

        if (!includeInactive)
        {
            query = query.Where(e => e.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(employeeNumber))
        {
            var number = employeeNumber.Trim();
            query = query.Where(e => e.EmployeeNumber == number);
        }

        var entries = await query
            .OrderBy(e => e.EmployeeNumber)
            .ToListAsync(cancellationToken);

        return entries.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<PayrollPromotionArrearsEntryDto>> SavePromotionArrearsAsync(
        Guid tenantId,
        PayrollPromotionArrearsBulkSaveDto dto,
        CancellationToken cancellationToken = default)
    {
        var activeParameters = await _context.PayrollParameterSets
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.IsActive)
            .OrderBy(e => e.Code)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Active payroll parameters are required before saving promotion arrears.");

        var period = NormalizePayrollParameterPeriod(
            activeParameters.CurrentPeriodFrom,
            activeParameters.CurrentPeriodTo,
            activeParameters.PayMode);

        if (!period.From.HasValue || !period.To.HasValue || activeParameters.CurrentPayPeriod <= 0)
        {
            throw new InvalidOperationException("Active payroll period dates are required before saving promotion arrears.");
        }

        var profileIds = dto.Entries
            .Where(e => e.EmployeeProfileId.HasValue && e.EmployeeProfileId.Value != Guid.Empty)
            .Select(e => e.EmployeeProfileId!.Value)
            .Distinct()
            .ToList();

        var employeeNumbers = dto.Entries
            .Select(e => e.EmployeeNumber?.Trim())
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var profiles = await PayrollProfileQuery(tenantId)
            .Where(e =>
                profileIds.Contains(e.Id) ||
                employeeNumbers.Contains(e.EmployeeNumber) ||
                (e.LegacyEmployeeNumber != null && employeeNumbers.Contains(e.LegacyEmployeeNumber)))
            .ToListAsync(cancellationToken);

        var profilesById = profiles.ToDictionary(e => e.Id);
        var profilesByNumber = profiles
            .GroupBy(e => e.EmployeeNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => e.First(), StringComparer.OrdinalIgnoreCase);

        var profilesByLegacyNumber = profiles
            .Where(e => !string.IsNullOrWhiteSpace(e.LegacyEmployeeNumber))
            .GroupBy(e => e.LegacyEmployeeNumber!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => e.First(), StringComparer.OrdinalIgnoreCase);

        var existingEntries = await _context.PayrollPromotionArrears
            .Where(e => e.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var existingById = existingEntries.ToDictionary(e => e.Id);
        var existingByProfile = existingEntries.ToDictionary(e => e.EmployeeProfileId);
        var existingByEmployeeNumber = existingEntries
            .GroupBy(e => e.EmployeeNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => e.First(), StringComparer.OrdinalIgnoreCase);
        var legacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode ?? activeParameters.LegacyCompanyCode, 5);
        var now = DateTime.UtcNow;

        foreach (var line in dto.Entries)
        {
            PayrollPromotionArrearsEntry? existing = null;
            if (line.Id.HasValue && line.Id.Value != Guid.Empty)
            {
                existingById.TryGetValue(line.Id.Value, out existing);
            }

            if (existing == null && line.EmployeeProfileId.HasValue && line.EmployeeProfileId.Value != Guid.Empty)
            {
                existingByProfile.TryGetValue(line.EmployeeProfileId.Value, out existing);
            }

            var employeeNumber = line.EmployeeNumber?.Trim();
            if (existing == null && !string.IsNullOrWhiteSpace(employeeNumber))
            {
                existingByEmployeeNumber.TryGetValue(employeeNumber, out existing);
            }

            if (!line.IsSelected)
            {
                if (existing != null)
                {
                    existing.IsActive = false;
                    existing.UpdatedAt = now;
                }

                continue;
            }

            PayrollEmployeeProfile? profile = null;
            if (line.EmployeeProfileId.HasValue && line.EmployeeProfileId.Value != Guid.Empty)
            {
                profilesById.TryGetValue(line.EmployeeProfileId.Value, out profile);
            }

            if (profile == null && !string.IsNullOrWhiteSpace(employeeNumber))
            {
                profilesByNumber.TryGetValue(employeeNumber, out profile);
                profile ??= profilesByLegacyNumber.GetValueOrDefault(employeeNumber);
            }

            if (profile == null)
            {
                throw new KeyNotFoundException($"Payroll employee profile not found for {line.EmployeeNumber}.");
            }

            if (!line.EffectiveDate.HasValue)
            {
                throw new InvalidOperationException($"Effective date is required for {profile.EmployeeNumber}.");
            }

            var effectiveDate = line.EffectiveDate.Value.Date;
            if (effectiveDate >= period.From.Value.Date)
            {
                throw new InvalidOperationException($"Effective date for {profile.EmployeeNumber} must be before the current payroll period starts.");
            }

            var entity = existing ?? existingByProfile.GetValueOrDefault(profile.Id) ?? existingByEmployeeNumber.GetValueOrDefault(profile.EmployeeNumber);
            entity ??= new PayrollPromotionArrearsEntry { TenantId = tenantId };

            entity.EmployeeProfileId = profile.Id;
            entity.EmployeeId = profile.EmployeeId;
            entity.EmployeeNumber = profile.EmployeeNumber;
            entity.EmployeeName = profile.Employee.FullName;
            entity.LegacyEmployeeId = profile.LegacyEmployeeId;
            entity.EffectiveDate = effectiveDate;
            entity.PayPeriod = activeParameters.CurrentPayPeriod;
            entity.PayPeriodFrom = period.From.Value.Date;
            entity.PayPeriodTo = period.To.Value.Date;
            entity.LegacyCompanyCode = legacyCompanyCode;
            entity.BasicSalary = CalculateBasicSalary(profile.SalaryBasis);
            entity.WorkingDays = null;
            entity.IsActive = true;
            entity.UpdatedAt = now;

            AddIfNew(_context.PayrollPromotionArrears, entity);
            existingByProfile[profile.Id] = entity;
            existingByEmployeeNumber[profile.EmployeeNumber] = entity;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await GetPromotionArrearsAsync(tenantId, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollOvertimeSummaryEntryDto>> GetOvertimeSummariesAsync(
        Guid tenantId,
        string? employeeNumber = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var activeParameters = await GetActivePayrollParametersAsync(tenantId, cancellationToken);
        var query = _context.PayrollTimesheetSummaries
            .AsNoTracking()
            .Include(e => e.EmployeeProfile)
                .ThenInclude(e => e.Employee)
            .Where(e => e.TenantId == tenantId);

        if (!includeInactive)
        {
            query = query.Where(e => e.IsActive);
        }

        if (activeParameters?.CurrentPayPeriod > 0)
        {
            query = query.Where(e => e.PayPeriod == activeParameters.CurrentPayPeriod);
        }

        if (!string.IsNullOrWhiteSpace(employeeNumber))
        {
            var number = employeeNumber.Trim();
            query = query.Where(e => e.EmployeeNumber.Contains(number));
        }

        var entries = await query
            .OrderBy(e => e.EmployeeNumber)
            .Take(500)
            .ToListAsync(cancellationToken);

        return entries.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<PayrollOvertimeSummaryEntryDto>> SaveOvertimeSummariesAsync(
        Guid tenantId,
        PayrollOvertimeSummaryBulkSaveDto dto,
        CancellationToken cancellationToken = default)
    {
        var activeParameters = await GetActivePayrollParametersAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Active payroll parameters are required before saving overtime summary.");

        var period = NormalizePayrollParameterPeriod(
            activeParameters.CurrentPeriodFrom,
            activeParameters.CurrentPeriodTo,
            activeParameters.PayMode);

        if (!period.From.HasValue || !period.To.HasValue || activeParameters.CurrentPayPeriod <= 0)
        {
            throw new InvalidOperationException("Active payroll period dates are required before saving overtime summary.");
        }

        var profiles = await LoadProfilesForEntryLinesAsync(
            tenantId,
            dto.Entries.Select(e => e.EmployeeProfileId),
            dto.Entries.Select(e => e.EmployeeNumber),
            cancellationToken);

        var profileLookup = BuildProfileLookup(profiles);
        var existingEntries = await _context.PayrollTimesheetSummaries
            .Where(e => e.TenantId == tenantId && e.PayPeriod == activeParameters.CurrentPayPeriod)
            .ToListAsync(cancellationToken);

        var existingById = existingEntries.ToDictionary(e => e.Id);
        var existingByProfile = existingEntries.ToDictionary(e => e.EmployeeProfileId);
        var existingByEmployeeNumber = existingEntries
            .GroupBy(e => e.EmployeeNumber, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => e.First(), StringComparer.OrdinalIgnoreCase);
        var legacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode ?? activeParameters.LegacyCompanyCode, 5);
        var now = DateTime.UtcNow;

        foreach (var line in dto.Entries)
        {
            var profile = ResolveProfile(profileLookup, line.EmployeeProfileId, line.EmployeeNumber);
            PayrollTimesheetSummary? existing = null;
            if (line.Id.HasValue && line.Id.Value != Guid.Empty)
            {
                existingById.TryGetValue(line.Id.Value, out existing);
            }

            if (existing == null && profile != null)
            {
                existingByProfile.TryGetValue(profile.Id, out existing);
            }

            var employeeNumber = line.EmployeeNumber?.Trim();
            if (existing == null && !string.IsNullOrWhiteSpace(employeeNumber))
            {
                existingByEmployeeNumber.TryGetValue(employeeNumber, out existing);
            }

            if (!line.IsSelected)
            {
                if (existing != null)
                {
                    existing.IsActive = false;
                    existing.UpdatedAt = now;
                }

                continue;
            }

            if (profile == null)
            {
                throw new KeyNotFoundException($"Payroll employee profile not found for {line.EmployeeNumber}.");
            }

            var absentDays = NormalizeNonNegative(line.AbsentDays, "Absent days", 4);
            var normalDays = NormalizeNonNegative(line.NormalDays, "Normal hours", 4);
            var weekdayDays = NormalizeNonNegative(line.WeekdayDays, "Weekday days", 4);
            var holidayDays = NormalizeNonNegative(line.HolidayDays, "Holiday days", 4);
            var saturdayDays = NormalizeNonNegative(line.SaturdayDays, "Saturday days", 4);
            var sundayDays = NormalizeNonNegative(line.SundayDays, "Sunday days", 4);

            var entity = existing ?? existingByProfile.GetValueOrDefault(profile.Id) ?? existingByEmployeeNumber.GetValueOrDefault(profile.EmployeeNumber);
            entity ??= new PayrollTimesheetSummary { TenantId = tenantId };

            entity.EmployeeProfileId = profile.Id;
            entity.EmployeeId = profile.EmployeeId;
            entity.EmployeeNumber = profile.EmployeeNumber;
            entity.EmployeeName = profile.Employee.FullName;
            entity.LegacyEmployeeId = profile.LegacyEmployeeId;
            entity.PayPeriod = activeParameters.CurrentPayPeriod;
            entity.PayPeriodFrom = period.From.Value.Date;
            entity.PayPeriodTo = period.To.Value.Date;
            entity.AbsentHours = absentDays;
            entity.NormalHours = normalDays;
            entity.WeekdayHours = weekdayDays;
            entity.HolidayHours = holidayDays;
            entity.SaturdayHours = saturdayDays;
            entity.SundayHours = sundayDays;
            entity.NightShiftCount = 0;
            entity.AttendanceCount = 0;
            entity.OvertimeAmount = 0;
            entity.RecordSource = "S";
            entity.LegacyCompanyCode = legacyCompanyCode;
            entity.IsActive = true;
            entity.UpdatedAt = now;

            AddIfNew(_context.PayrollTimesheetSummaries, entity);
            existingByProfile[profile.Id] = entity;
            existingByEmployeeNumber[profile.EmployeeNumber] = entity;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await GetOvertimeSummariesAsync(tenantId, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollContributionOpeningBalanceDto>> GetContributionOpeningBalancesAsync(
        Guid tenantId,
        string? contributionCode = null,
        string? contributionCodeType = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PayrollContributionOpeningBalances
            .AsNoTracking()
            .Include(e => e.EmployeeProfile)
                .ThenInclude(e => e.Employee)
            .Where(e => e.TenantId == tenantId);

        if (!includeInactive)
        {
            query = query.Where(e => e.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(contributionCodeType))
        {
            var codeType = NormalizeCode(contributionCodeType);
            query = query.Where(e => e.ContributionCodeType == codeType);
        }

        if (!string.IsNullOrWhiteSpace(contributionCode))
        {
            var code = NormalizeCode(contributionCode);
            query = query.Where(e => e.ContributionCode == code);
        }

        var entries = await query
            .OrderBy(e => e.ContributionName)
            .ThenBy(e => e.EmployeeNumber)
            .Take(500)
            .ToListAsync(cancellationToken);

        return entries.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<PayrollContributionOpeningBalanceDto>> SaveContributionOpeningBalancesAsync(
        Guid tenantId,
        PayrollContributionOpeningBalanceBulkSaveDto dto,
        CancellationToken cancellationToken = default)
    {
        var contributionCodeType = string.IsNullOrWhiteSpace(dto.ContributionCodeType)
            ? "CON"
            : NormalizeCode(dto.ContributionCodeType);
        var contributionCode = NormalizeCode(dto.ContributionCode);
        var contributionName = RequireText(dto.ContributionName, "Contribution");
        var activeParameters = await GetActivePayrollParametersAsync(tenantId, cancellationToken);
        var defaultBalanceDate = activeParameters?.CurrentPeriodFrom?.Date.AddDays(-1)
            ?? activeParameters?.CurrentPeriodTo?.Date.AddMonths(-1)
            ?? DateTime.UtcNow.Date;

        var profiles = await LoadProfilesForEntryLinesAsync(
            tenantId,
            dto.Entries.Select(e => e.EmployeeProfileId),
            dto.Entries.Select(e => e.EmployeeNumber),
            cancellationToken);

        var profileLookup = BuildProfileLookup(profiles);
        var existingEntries = await _context.PayrollContributionOpeningBalances
            .Where(e => e.TenantId == tenantId &&
                        e.ContributionCodeType == contributionCodeType &&
                        e.ContributionCode == contributionCode)
            .ToListAsync(cancellationToken);

        var existingById = existingEntries.ToDictionary(e => e.Id);
        var existingByProfile = existingEntries.ToDictionary(e => e.EmployeeProfileId);
        var legacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode ?? activeParameters?.LegacyCompanyCode, 5);
        var now = DateTime.UtcNow;

        foreach (var line in dto.Entries)
        {
            var profile = ResolveProfile(profileLookup, line.EmployeeProfileId, line.EmployeeNumber);
            PayrollContributionOpeningBalance? existing = null;
            if (line.Id.HasValue && line.Id.Value != Guid.Empty)
            {
                existingById.TryGetValue(line.Id.Value, out existing);
            }

            if (existing == null && profile != null)
            {
                existingByProfile.TryGetValue(profile.Id, out existing);
            }

            if (!line.IsSelected)
            {
                if (existing != null)
                {
                    existing.IsActive = false;
                    existing.UpdatedAt = now;
                }

                continue;
            }

            if (profile == null)
            {
                throw new KeyNotFoundException($"Payroll employee profile not found for {line.EmployeeNumber}.");
            }

            var openingBalance = NormalizeNonNegative(line.OpeningBalance, "Opening balance", 2);
            var balanceAsAt = (line.BalanceAsAt ?? defaultBalanceDate).Date;
            var entity = existing ?? existingByProfile.GetValueOrDefault(profile.Id);
            entity ??= new PayrollContributionOpeningBalance { TenantId = tenantId };

            entity.EmployeeProfileId = profile.Id;
            entity.EmployeeId = profile.EmployeeId;
            entity.EmployeeNumber = profile.EmployeeNumber;
            entity.EmployeeName = profile.Employee.FullName;
            entity.LegacyEmployeeId = profile.LegacyEmployeeId;
            entity.ContributionCodeType = contributionCodeType;
            entity.ContributionCode = contributionCode;
            entity.ContributionName = contributionName;
            entity.BalanceAsAt = balanceAsAt;
            entity.OpeningBalance = openingBalance;
            entity.LegacyCompanyCode = legacyCompanyCode;
            entity.IsActive = true;
            entity.UpdatedAt = now;

            AddIfNew(_context.PayrollContributionOpeningBalances, entity);
            existingByProfile[profile.Id] = entity;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await GetContributionOpeningBalancesAsync(tenantId, contributionCode, contributionCodeType, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollContributionTransactionDto>> GetContributionTransactionsAsync(
        Guid tenantId,
        string? employeeNumber = null,
        string? contributionCode = null,
        string? contributionCodeType = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PayrollContributionTransactions
            .AsNoTracking()
            .Include(e => e.EmployeeProfile)
                .ThenInclude(e => e.Employee)
            .Where(e => e.TenantId == tenantId);

        if (!includeInactive)
        {
            query = query.Where(e => e.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(employeeNumber))
        {
            var number = employeeNumber.Trim();
            query = query.Where(e => e.EmployeeNumber.Contains(number));
        }

        if (!string.IsNullOrWhiteSpace(contributionCodeType))
        {
            var codeType = NormalizeCode(contributionCodeType);
            query = query.Where(e => e.ContributionCodeType == codeType);
        }

        if (!string.IsNullOrWhiteSpace(contributionCode))
        {
            var code = NormalizeCode(contributionCode);
            query = query.Where(e => e.ContributionCode == code);
        }

        var entries = await query
            .OrderByDescending(e => e.EffectiveDate)
            .ThenBy(e => e.EmployeeNumber)
            .Take(500)
            .ToListAsync(cancellationToken);

        return entries.Select(ToDto).ToList();
    }

    public async Task<PayrollContributionTransactionDto> UpsertContributionTransactionAsync(
        Guid tenantId,
        UpsertPayrollContributionTransactionDto dto,
        CancellationToken cancellationToken = default)
    {
        var amount = NormalizeNonNegative(dto.Amount, "Amount", 2);
        if (amount <= 0)
        {
            throw new InvalidOperationException("Amount must be greater than zero.");
        }

        var transactionType = NormalizeContributionTransactionType(dto.TransactionType);
        var contributionCodeType = string.IsNullOrWhiteSpace(dto.ContributionCodeType)
            ? "CON"
            : NormalizeCode(dto.ContributionCodeType);
        var contributionCode = NormalizeCode(dto.ContributionCode);
        var contributionName = RequireText(dto.ContributionName, "Contribution");
        var effectiveDate = dto.EffectiveDate == default ? DateTime.UtcNow.Date : dto.EffectiveDate.Date;
        var activeParameters = await GetActivePayrollParametersAsync(tenantId, cancellationToken);

        PayrollEmployeeProfile? profile = null;
        if (dto.EmployeeProfileId.HasValue && dto.EmployeeProfileId.Value != Guid.Empty)
        {
            profile = await PayrollProfileQuery(tenantId)
                .FirstOrDefaultAsync(e => e.Id == dto.EmployeeProfileId.Value, cancellationToken);
        }

        if (profile == null && !string.IsNullOrWhiteSpace(dto.EmployeeNumber))
        {
            var employeeNumber = dto.EmployeeNumber.Trim();
            profile = await PayrollProfileQuery(tenantId)
                .FirstOrDefaultAsync(e =>
                    e.EmployeeNumber == employeeNumber ||
                    (e.LegacyEmployeeNumber != null && e.LegacyEmployeeNumber == employeeNumber),
                    cancellationToken);
        }

        if (profile == null)
        {
            throw new KeyNotFoundException("Payroll employee profile not found.");
        }

        var entity = await FindForUpsertAsync(_context.PayrollContributionTransactions, tenantId, dto.Id, cancellationToken)
            ?? new PayrollContributionTransaction { TenantId = tenantId };

        entity.EmployeeProfileId = profile.Id;
        entity.EmployeeId = profile.EmployeeId;
        entity.EmployeeNumber = profile.EmployeeNumber;
        entity.EmployeeName = profile.Employee.FullName;
        entity.LegacyEmployeeId = profile.LegacyEmployeeId;
        entity.ContributionCodeType = contributionCodeType;
        entity.ContributionCode = contributionCode;
        entity.ContributionName = contributionName;
        entity.TransactionType = transactionType;
        entity.EffectiveDate = effectiveDate;
        entity.Amount = amount;
        entity.LegacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode ?? activeParameters?.LegacyCompanyCode, 5);
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        AddIfNew(_context.PayrollContributionTransactions, entity);
        await _context.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<PayrollImportBatchDto> ImportEmployeeReconciliationAsync(Guid tenantId, Guid? importedByUserId, PayrollReconciliationImportDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Rows.Count == 0)
        {
            throw new InvalidOperationException("Import must include at least one row.");
        }

        var legacyNumbers = dto.Rows
            .Select(e => e.LegacyEmployeeNumber?.Trim())
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var employees = await _context.Employees
            .Where(e => e.TenantId == tenantId && (legacyNumbers.Contains(e.EmployeeNumber) || (e.CorporateEmployeeID != null && legacyNumbers.Contains(e.CorporateEmployeeID))))
            .ToListAsync(cancellationToken);

        var employeeKeys = new List<(string Key, Employee Employee)>();
        foreach (var employee in employees)
        {
            if (!string.IsNullOrWhiteSpace(employee.EmployeeNumber))
            {
                employeeKeys.Add((employee.EmployeeNumber, employee));
            }

            if (!string.IsNullOrWhiteSpace(employee.CorporateEmployeeID))
            {
                employeeKeys.Add((employee.CorporateEmployeeID, employee));
            }
        }

        var employeesByNumber = employeeKeys
            .GroupBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => e.Select(x => x.Employee).DistinctBy(x => x.Id).ToList(), StringComparer.OrdinalIgnoreCase);

        var duplicateImportNumbers = dto.Rows
            .Where(e => !string.IsNullOrWhiteSpace(e.LegacyEmployeeNumber))
            .GroupBy(e => e.LegacyEmployeeNumber.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(e => e.Count() > 1)
            .Select(e => e.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var batch = new PayrollImportBatch
        {
            TenantId = tenantId,
            SourceName = string.IsNullOrWhiteSpace(dto.SourceName) ? "Oracle Forms payroll import" : dto.SourceName.Trim(),
            ImportType = "EmployeeReconciliation",
            TotalRows = dto.Rows.Count,
            ImportedAt = DateTime.UtcNow,
            ImportedByUserId = importedByUserId
        };

        var existingProfiles = await _context.PayrollEmployeeProfiles
            .Where(e => e.TenantId == tenantId)
            .ToDictionaryAsync(e => e.EmployeeId, cancellationToken);

        foreach (var rowDto in dto.Rows.Select((row, index) => new { row, index }))
        {
            var legacyNumber = rowDto.row.LegacyEmployeeNumber?.Trim() ?? string.Empty;
            var importRow = new PayrollImportRow
            {
                TenantId = tenantId,
                PayrollImportBatch = batch,
                RowNumber = rowDto.row.RowNumber > 0 ? rowDto.row.RowNumber : rowDto.index + 1,
                LegacyEmployeeNumber = legacyNumber,
                LegacyFullName = TrimOrNull(rowDto.row.LegacyFullName),
                Department = TrimOrNull(rowDto.row.Department)
            };

            if (string.IsNullOrWhiteSpace(legacyNumber) || duplicateImportNumbers.Contains(legacyNumber))
            {
                importRow.Status = PayrollImportRowStatus.DuplicateEmployeeNumber;
                importRow.Message = string.IsNullOrWhiteSpace(legacyNumber)
                    ? "Legacy employee number is blank."
                    : "Legacy employee number appears more than once in the import batch.";
                batch.DuplicateRows++;
                batch.Rows.Add(importRow);
                continue;
            }

            if (!employeesByNumber.TryGetValue(legacyNumber, out var matches) || matches.Count == 0)
            {
                importRow.Status = PayrollImportRowStatus.MissingEmployee;
                importRow.Message = "No matching HR employee was found by EmployeeNumber or CorporateEmployeeID.";
                batch.MissingRows++;
                batch.Rows.Add(importRow);
                continue;
            }

            if (matches.Count > 1)
            {
                importRow.Status = PayrollImportRowStatus.DuplicateEmployeeNumber;
                importRow.Message = "More than one HR employee matched this legacy number.";
                batch.DuplicateRows++;
                batch.Rows.Add(importRow);
                continue;
            }

            var employee = matches[0];
            importRow.Status = PayrollImportRowStatus.Matched;
            importRow.MatchedEmployeeId = employee.Id;
            importRow.MatchedEmployeeNumber = employee.EmployeeNumber;
            importRow.Message = "Matched to HR employee.";
            batch.MatchedRows++;

            if (!existingProfiles.TryGetValue(employee.Id, out var profile))
            {
                profile = new PayrollEmployeeProfile
                {
                    TenantId = tenantId,
                    EmployeeId = employee.Id,
                    EmployeeNumber = employee.EmployeeNumber,
                    PayrollActive = true,
                    PayTax = true,
                    SsfApplicable = true,
                    OvertimeEligible = employee.Overtime,
                    CurrencyCode = "GHS"
                };
                _context.PayrollEmployeeProfiles.Add(profile);
                existingProfiles[employee.Id] = profile;
            }

            profile.LegacyEmployeeId = TrimOrNull(rowDto.row.LegacyEmployeeId);
            profile.LegacyEmployeeNumber = legacyNumber;
            batch.Rows.Add(importRow);
        }

        _context.PayrollImportBatches.Add(batch);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Payroll reconciliation import completed: {BatchId}, matched {MatchedRows}/{TotalRows}",
            batch.Id,
            batch.MatchedRows,
            batch.TotalRows);

        return ToDto(batch);
    }

    public async Task<PayrollImportBatchDto?> GetImportBatchAsync(Guid tenantId, Guid batchId, CancellationToken cancellationToken = default)
    {
        var batch = await _context.PayrollImportBatches
            .AsNoTracking()
            .Include(e => e.Rows.OrderBy(r => r.RowNumber))
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == batchId, cancellationToken);

        return batch == null ? null : ToDto(batch);
    }

    public async Task<IReadOnlyList<PayrollRunDto>> GetRunsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var runs = await _context.PayrollRuns
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.RunDate)
            .ThenByDescending(e => e.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        return runs.Select(e => ToDto(e, includeChildren: false)).ToList();
    }

    public async Task<PayrollRunDto?> GetRunAsync(Guid tenantId, Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await PayrollRunQuery(tenantId)
            .FirstOrDefaultAsync(e => e.Id == runId, cancellationToken);

        return run == null ? null : ToDto(run, includeChildren: true);
    }

    public async Task<PayrollRunDto> CreateRunAsync(Guid tenantId, CreatePayrollRunDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.PayPeriod <= 0)
        {
            throw new InvalidOperationException("PayPeriod is required.");
        }

        if (dto.PayPeriodTo.Date < dto.PayPeriodFrom.Date)
        {
            throw new InvalidOperationException("PayPeriodTo cannot be earlier than PayPeriodFrom.");
        }

        var openRunExists = await _context.PayrollRuns
            .AsNoTracking()
            .AnyAsync(e => e.TenantId == tenantId &&
                           e.PayPeriod == dto.PayPeriod &&
                           e.Status != PayrollRunStatus.Closed &&
                           e.Status != PayrollRunStatus.RolledBack,
                cancellationToken);
        if (openRunExists)
        {
            throw new InvalidOperationException("A payroll run already exists for this payroll period. Post and close the current run before creating a new one.");
        }

        var run = new PayrollRun
        {
            TenantId = tenantId,
            RunNumber = await GenerateRunNumberAsync(tenantId, dto.PayPeriod, cancellationToken),
            PayPeriod = dto.PayPeriod,
            PayPeriodFrom = dto.PayPeriodFrom.Date,
            PayPeriodTo = dto.PayPeriodTo.Date,
            RunDate = dto.RunDate ?? DateTime.UtcNow,
            CurrencyCode = NormalizeCurrency(dto.CurrencyCode),
            IsSeparateBonusRun = dto.IsSeparateBonusRun,
            SeparateBonusCode = dto.IsSeparateBonusRun && !string.IsNullOrWhiteSpace(dto.SeparateBonusCode)
                ? NormalizeCode(dto.SeparateBonusCode)
                : null,
            Status = PayrollRunStatus.Draft,
            Notes = TrimOrNull(dto.Notes)
        };

        _context.PayrollRuns.Add(run);
        await _context.SaveChangesAsync(cancellationToken);

        return ToDto(run, includeChildren: false);
    }

    public async Task<PayrollRunDto> CalculateRunAsync(Guid tenantId, Guid runId, Guid? userId, CancellationToken cancellationToken = default)
    {
        var run = await _context.PayrollRuns
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == runId, cancellationToken)
            ?? throw new KeyNotFoundException("Payroll run not found.");

        if (run.Status == PayrollRunStatus.Closed)
        {
            throw new InvalidOperationException("Closed payroll runs cannot be recalculated.");
        }

        await ClearRunDetailsAsync(tenantId, run.Id, cancellationToken);

        var activeParameters = await _context.PayrollParameterSets
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.IsActive)
            .OrderBy(e => e.Code)
            .FirstOrDefaultAsync(cancellationToken);

        var defaultComponents = await _context.PayrollComponents
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.IsActive && e.AppliesByDefault)
            .ToListAsync(cancellationToken);

        var componentRules = await _context.PayrollComponentRules
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        var componentRulesByKey = componentRules
            .GroupBy(e => ComponentRuleKey(e.ComponentType, e.ComponentCode), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => (IReadOnlyList<PayrollComponentRule>)e.ToList(), StringComparer.OrdinalIgnoreCase);

        var defaultTaxReliefs = await _context.PayrollTaxReliefs
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.IsActive && e.AppliesByDefault)
            .ToListAsync(cancellationToken);

        var employeeTaxReliefs = await _context.PayrollEmployeeTaxReliefs
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.IsActive)
            .ToListAsync(cancellationToken);

        var employeeTaxReliefsByProfile = employeeTaxReliefs
            .GroupBy(e => e.EmployeeProfileId)
            .ToDictionary(e => e.Key, e => e.ToList());

        var pensionScheme = await _context.PayrollPensionSchemes
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.IsActive)
            .OrderByDescending(e => e.IsDefault)
            .ThenBy(e => e.Code)
            .FirstOrDefaultAsync(cancellationToken);

        var effectiveTaxBands = await _context.PayrollTaxBands
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId &&
                        e.IsActive &&
                        !e.IsAnnual &&
                        e.EffectiveFrom <= run.PayPeriodTo &&
                        (e.EffectiveTo == null || e.EffectiveTo >= run.PayPeriodFrom))
            .OrderBy(e => e.SerialNo)
            .ThenBy(e => e.LowerBound)
            .ToListAsync(cancellationToken);
        var taxBands = effectiveTaxBands
            .Where(IsNormalIncomeTaxBand)
            .OrderBy(e => e.SerialNo <= 0 ? int.MaxValue : e.SerialNo)
            .ThenBy(e => e.CumulativeSalary ?? e.UpperBound ?? e.LowerBound)
            .ToList();
        var overtimeTaxBands = effectiveTaxBands
            .Where(IsOvertimeIncomeTaxBand)
            .OrderBy(e => e.SerialNo <= 0 ? int.MaxValue : e.SerialNo)
            .ThenBy(e => e.CumulativeSalary ?? e.UpperBound ?? e.LowerBound)
            .ToList();

        var salaryAdvancesByProfile = await _context.PayrollSalaryAdvances
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId &&
                        e.IsActive &&
                        e.AdvanceDate >= run.PayPeriodFrom.Date &&
                        e.AdvanceDate <= run.PayPeriodTo.Date)
            .GroupBy(e => e.EmployeeProfileId)
            .Select(e => new { EmployeeProfileId = e.Key, Amount = e.Sum(x => x.AdvanceAmount) })
            .ToDictionaryAsync(e => e.EmployeeProfileId, e => e.Amount, cancellationToken);

        var runPayPeriodFromMonth = new DateTime(run.PayPeriodFrom.Year, run.PayPeriodFrom.Month, 1);
        var runPayPeriodToMonth = new DateTime(run.PayPeriodTo.Year, run.PayPeriodTo.Month, DateTime.DaysInMonth(run.PayPeriodTo.Year, run.PayPeriodTo.Month));
        var runPayPeriodFromMonthValue = FormatPayPeriodMonth(run.PayPeriodFrom);
        var runPayPeriodToMonthValue = FormatPayPeriodMonth(run.PayPeriodTo);

        var bonusPolicies = await _context.PayrollBonusPolicies
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId &&
                        e.IsActive &&
                        e.PaySeparate == run.IsSeparateBonusRun &&
                        (!run.IsSeparateBonusRun ||
                         string.IsNullOrWhiteSpace(run.SeparateBonusCode) ||
                         e.Code == run.SeparateBonusCode) &&
                        (!e.NextPayPeriod.HasValue ||
                         e.NextPayPeriod.Value <= 0 ||
                         e.NextPayPeriod.Value == run.PayPeriod ||
                         e.NextPayPeriod.Value == runPayPeriodFromMonthValue ||
                         e.NextPayPeriod.Value == runPayPeriodToMonthValue) &&
                        (!e.NextPayPeriodDate.HasValue ||
                         (e.NextPayPeriodDate.Value.Date >= runPayPeriodFromMonth &&
                          e.NextPayPeriodDate.Value.Date <= runPayPeriodToMonth)))
            .OrderBy(e => e.Code)
            .ToListAsync(cancellationToken);

        var bonusExceptions = await _context.PayrollBonusExceptions
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.BonusCode)
            .ThenBy(e => e.EmployeeNumber)
            .ToListAsync(cancellationToken);

        var bonusExceptionsByCode = bonusExceptions
            .GroupBy(e => NormalizeMatchToken(e.BonusCode), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => (IReadOnlyList<PayrollBonusException>)e.ToList(), StringComparer.OrdinalIgnoreCase);

        var taxYearStart = new DateTime(run.PayPeriodFrom.Year, 1, 1);
        var previousBonusRows = await _context.PayrollTransactions
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId &&
                        e.TransactionType == BonusTransactionType &&
                        e.PayrollRun.PayPeriodFrom >= taxYearStart &&
                        e.PayrollRun.PayPeriodTo < run.PayPeriodFrom.Date &&
                        e.PayrollRun.Status != PayrollRunStatus.Draft &&
                        e.PayrollRun.Status != PayrollRunStatus.RolledBack)
            .GroupBy(e => e.EmployeeNumber)
            .Select(e => new { EmployeeNumber = e.Key, Amount = e.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        var previousBonusByEmployeeNumber = previousBonusRows
            .ToDictionary(e => e.EmployeeNumber, e => e.Amount, StringComparer.OrdinalIgnoreCase);

        var historicalBasicRows = bonusPolicies.Count == 0
            ? []
            : await _context.PayrollRunEmployees
                .AsNoTracking()
                .Include(e => e.PayrollRun)
                .Where(e => e.TenantId == tenantId &&
                            e.PayrollRun.PayPeriodFrom >= taxYearStart &&
                            e.PayrollRun.PayPeriodTo < run.PayPeriodFrom.Date &&
                            e.PayrollRun.Status != PayrollRunStatus.Draft &&
                            e.PayrollRun.Status != PayrollRunStatus.RolledBack)
                .GroupBy(e => e.EmployeeNumber)
                .Select(e => new { EmployeeNumber = e.Key, Amount = e.Sum(x => x.BasicSalary) })
                .ToListAsync(cancellationToken);

        var historicalBasicByEmployeeNumber = historicalBasicRows
            .ToDictionary(e => e.EmployeeNumber, e => e.Amount, StringComparer.OrdinalIgnoreCase);

        var overtimePolicy = await _context.PayrollOvertimePolicies
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.IsActive)
            .OrderByDescending(e => e.IsDefault)
            .ThenBy(e => e.Code)
            .FirstOrDefaultAsync(cancellationToken);

        var nonWorkingDayRates = await _context.PayrollNonWorkingDays
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var saturdayRate = FindNonWorkingOvertimeRate(nonWorkingDayRates, "SAT", overtimePolicy?.HolidayRate ?? 0m);
        var sundayRate = FindNonWorkingOvertimeRate(nonWorkingDayRates, "SUN", overtimePolicy?.HolidayRate ?? saturdayRate);

        var overtimeSummaries = await _context.PayrollTimesheetSummaries
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId &&
                        e.IsActive &&
                        e.PayPeriod == run.PayPeriod &&
                        e.PayPeriodFrom.Date <= run.PayPeriodTo.Date &&
                        e.PayPeriodTo.Date >= run.PayPeriodFrom.Date)
            .ToListAsync(cancellationToken);

        var overtimeSummariesByProfile = overtimeSummaries
            .GroupBy(e => e.EmployeeProfileId)
            .ToDictionary(e => e.Key, e => e.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).First());

        var promotionEntries = await _context.PayrollPromotionArrears
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId &&
                        e.IsActive &&
                        e.EffectiveDate.Date < run.PayPeriodFrom.Date)
            .ToListAsync(cancellationToken);

        var promotionEntriesByProfile = promotionEntries
            .GroupBy(e => e.EmployeeProfileId)
            .ToDictionary(e => e.Key, e => e.OrderByDescending(x => x.EffectiveDate).First());

        var historicalRunEmployeesByNumber = new Dictionary<string, List<PayrollRunEmployee>>(StringComparer.OrdinalIgnoreCase);
        if (promotionEntries.Count > 0)
        {
            var earliestPromotionDate = promotionEntries.Min(e => e.EffectiveDate.Date);
            var historicalRunEmployees = await _context.PayrollRunEmployees
                .AsNoTracking()
                .Include(e => e.PayrollRun)
                .Include(e => e.Transactions)
                .Where(e => e.TenantId == tenantId &&
                            e.PayrollRun.PayPeriod < run.PayPeriod &&
                            e.PayrollRun.PayPeriodFrom.Date >= earliestPromotionDate &&
                            e.PayrollRun.Status == PayrollRunStatus.Closed)
                .ToListAsync(cancellationToken);

            historicalRunEmployeesByNumber = historicalRunEmployees
                .GroupBy(e => e.EmployeeNumber, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(e => e.Key, e => e.OrderBy(x => x.PayrollRun.PayPeriod).ToList(), StringComparer.OrdinalIgnoreCase);
        }

        var profiles = await PayrollProfileQuery(tenantId)
            .Where(e => e.PayrollActive)
            .OrderBy(e => e.EmployeeNumber)
            .ToListAsync(cancellationToken);

        var transactions = new List<PayrollTransaction>();
        var runEmployees = new List<PayrollRunEmployee>();

        foreach (var profile in profiles)
        {
            var salaryBasis = profile.SalaryBasis;
            var originalBasicSalary = CalculateBasicSalary(salaryBasis);
            if (originalBasicSalary <= 0)
            {
                continue;
            }

            overtimeSummariesByProfile.TryGetValue(profile.Id, out var overtimeSummary);
            var absenceAmount = CalculateAbsenceAmount(originalBasicSalary, overtimeSummary, overtimePolicy, activeParameters);
            var basicSalary = Math.Max(0, Math.Round(originalBasicSalary - absenceAmount, 2));
            if (!run.IsSeparateBonusRun && profile.PayTax && IsEmployeeGrossUp(profile))
            {
                basicSalary = GrossUpAmount(basicSalary, ResolveGrossUpRate(taxBands, activeParameters));
            }

            var runEmployee = new PayrollRunEmployee
            {
                TenantId = tenantId,
                PayrollRunId = run.Id,
                EmployeeId = profile.EmployeeId,
                EmployeeNumber = profile.EmployeeNumber,
                EmployeeName = profile.Employee.FullName,
                BasicSalary = run.IsSeparateBonusRun ? 0m : basicSalary,
                CurrencyCode = run.CurrencyCode
            };

            var employeeTransactions = new List<PayrollTransaction>();
            var taxableAllowances = 0m;
            var nonTaxableAllowances = 0m;
            var taxableDeductions = 0m;
            var nonTaxableDeductions = 0m;
            var employeeContribution = 0m;
            var employerContribution = 0m;
            var taxableOvertime = 0m;
            var separateOvertimeTaxAmount = 0m;
            var componentSeparateTaxAmount = 0m;
            var overtimeCalculation = PayrollOvertimeCalculation.Empty;
            PayrollTransaction? overtimeTransaction = null;
            var taxableBenefits = 0m;
            var separateTaxableAllowances = 0m;
            var separateTaxableBenefits = 0m;
            var nonTaxableContribution = 0m;
            var employerTaxableAmount = 0m;
            var taxFreeCeiling = 0m;
            var grossIncome = run.IsSeparateBonusRun ? 0m : basicSalary;
            var netIncomeBeforeTax = run.IsSeparateBonusRun ? 0m : basicSalary;
            var pension = new PayrollPensionCalculation(0m, 0m);

            if (!run.IsSeparateBonusRun)
            {
                employeeTransactions.Add(CreateTransaction(tenantId, run, runEmployee, profile, BasicSalaryTransactionType, "Basic salary", basicSalary, taxable: true));
                if (absenceAmount > 0)
                {
                    employeeTransactions.Add(CreateTransaction(
                        tenantId,
                        run,
                        runEmployee,
                        profile,
                        AbsenceTransactionType,
                        "Absence deduction",
                        absenceAmount,
                        taxable: false,
                        componentCode: AbsenceTransactionType));
                }

                if (profile.OvertimeEligible || profile.Employee.Overtime)
                {
                    overtimeCalculation = CalculateOvertimeAmount(originalBasicSalary, overtimeSummary, overtimePolicy, activeParameters, saturdayRate, sundayRate);
                    if (overtimeCalculation.Amount > 0)
                    {
                        overtimeTransaction = CreateTransaction(
                            tenantId,
                            run,
                            runEmployee,
                            profile,
                            OvertimeTransactionType,
                            "Overtime",
                            overtimeCalculation.Amount,
                            overtimeCalculation.Taxable,
                            componentCode: OvertimeTransactionType);
                        employeeTransactions.Add(overtimeTransaction);

                        if (!overtimeCalculation.Taxable)
                        {
                            nonTaxableAllowances += overtimeCalculation.Amount;
                        }

                        grossIncome += overtimeCalculation.Amount;
                        netIncomeBeforeTax += overtimeCalculation.Amount;
                    }
                }

                foreach (var componentLine in ResolveComponents(defaultComponents, componentRulesByKey, profile, profile.EmployeeComponents, run.PayPeriodFrom, run.PayPeriodTo))
                {
                    var component = componentLine.Component;
                    var amount = CalculateComponentAmount(componentLine, basicSalary);
                    amount = ApplyBenefitCap(componentLine, amount);
                    amount = ApplyComponentProration(componentLine, profile, amount, run.PayPeriodFrom, run.PayPeriodTo, activeParameters);
                    amount = ApplyComponentGrossUp(componentLine, profile, amount, taxBands, activeParameters);
                    amount = ConvertComponentAmountToRunCurrency(componentLine, amount, run.CurrencyCode, activeParameters);
                    if (amount == 0)
                    {
                        continue;
                    }

                    var transactionType = component.ComponentType.ToString();
                    var description = component.Name;
                    var taxable = IsComponentTaxable(componentLine);
                    var employerAmountOverride = GetComponentEmployerAmount(componentLine);
                    if (employerAmountOverride.HasValue)
                    {
                        employerAmountOverride = ConvertComponentAmountToRunCurrency(componentLine, employerAmountOverride.Value, run.CurrencyCode, activeParameters);
                    }

                    var taxFreeCeilingAmount = ConvertComponentAmountToRunCurrency(componentLine, GetComponentTaxFreeCeiling(componentLine) ?? 0m, run.CurrencyCode, activeParameters);
                    var separateTax = IsComponentSeparateTax(componentLine);
                    var separateTaxRate = GetComponentSeparateTaxPercent(componentLine);
                    var separateTaxBasis = component.ComponentType is PayrollComponentType.EmployeeContribution or PayrollComponentType.EmployerContribution
                        ? employerAmountOverride ?? amount
                        : amount;
                    var separateTaxAmount = separateTax && separateTaxRate > 0m
                        ? Math.Round(Math.Abs(separateTaxBasis) * separateTaxRate / 100m, 2)
                        : 0m;

                    var transaction = CreateTransaction(tenantId, run, runEmployee, profile, transactionType, description, Math.Abs(amount), taxable, component);
                    transaction.EmployerTaxable = IsComponentEmployerTaxable(componentLine);
                    if (employerAmountOverride.HasValue)
                    {
                        transaction.EmployerAmount = employerAmountOverride.Value;
                    }

                    transaction.SeparateTax = separateTax;

                    employeeTransactions.Add(transaction);
                    componentSeparateTaxAmount += separateTaxAmount;

                    switch (component.ComponentType)
                    {
                        case PayrollComponentType.Allowance:
                            if (taxable)
                            {
                                taxableAllowances += amount;
                                if (separateTax)
                                {
                                    separateTaxableAllowances += amount;
                                }
                                else
                                {
                                    taxFreeCeiling += taxFreeCeilingAmount;
                                }
                            }
                            else
                            {
                                nonTaxableAllowances += amount;
                            }

                            if (component.IncludeInGross)
                            {
                                grossIncome += amount;
                            }

                            netIncomeBeforeTax += amount;
                            break;
                        case PayrollComponentType.Benefit:
                            if (taxable)
                            {
                                taxableBenefits += amount;
                                if (separateTax)
                                {
                                    separateTaxableBenefits += amount;
                                }
                                else
                                {
                                    taxFreeCeiling += taxFreeCeilingAmount;
                                }
                            }
                            else
                            {
                                nonTaxableAllowances += amount;
                            }

                            if (component.IncludeInGross)
                            {
                                grossIncome += amount;
                            }

                            netIncomeBeforeTax += amount;
                            break;
                        case PayrollComponentType.Deduction:
                            if (taxable)
                            {
                                taxableDeductions += amount;
                            }
                            else
                            {
                                nonTaxableDeductions += amount;
                            }

                            netIncomeBeforeTax -= amount;
                            break;
                        case PayrollComponentType.EmployeeContribution:
                            employeeContribution += amount;
                            var employeeContributionEmployerAmount = transaction.EmployerAmount.GetValueOrDefault();
                            if (employeeContributionEmployerAmount > 0m)
                            {
                                employerContribution += employeeContributionEmployerAmount;
                                if (transaction.EmployerTaxable && !separateTax)
                                {
                                    employerTaxableAmount += employeeContributionEmployerAmount;
                                }
                            }

                            if (!taxable)
                            {
                                nonTaxableContribution += amount;
                            }

                            netIncomeBeforeTax -= amount;
                            break;
                        case PayrollComponentType.EmployerContribution:
                            transaction.EmployerAmount ??= amount;
                            employerContribution += transaction.EmployerAmount.Value;
                            if (transaction.EmployerTaxable && !separateTax)
                            {
                                employerTaxableAmount += transaction.EmployerAmount.Value;
                            }
                            break;
                    }
                }

                pension = CalculatePension(profile, basicSalary, pensionScheme, activeParameters);
                if (pension.EmployeeContribution > 0)
                {
                    employeeContribution += pension.EmployeeContribution;
                    netIncomeBeforeTax -= pension.EmployeeContribution;
                    employeeTransactions.Add(CreateTransaction(tenantId, run, runEmployee, profile, EmployeePensionTransactionType, "Employee pension contribution", pension.EmployeeContribution, taxable: false));
                }

                if (pension.EmployerContribution > 0)
                {
                    employerContribution += pension.EmployerContribution;
                    employeeTransactions.Add(CreateTransaction(tenantId, run, runEmployee, profile, EmployerPensionTransactionType, "Employer pension contribution", pension.EmployerContribution, taxable: false, employerAmount: pension.EmployerContribution));
                }

                if (overtimeCalculation.Amount > 0 && overtimeCalculation.Taxable)
                {
                    var overtimeTaxBasis = basicSalary + taxableAllowances + nonTaxableAllowances + taxableBenefits;
                    var overtimeTaxTreatment = CalculateOvertimeTaxTreatment(
                        overtimeCalculation,
                        overtimePolicy,
                        overtimeTaxBands,
                        overtimeTaxBasis);

                    taxableOvertime += overtimeTaxTreatment.NormalTaxableAmount;
                    separateOvertimeTaxAmount += overtimeTaxTreatment.SeparateTaxAmount;
                    if (overtimeTransaction != null)
                    {
                        overtimeTransaction.SeparateTax = overtimeTaxTreatment.SeparateTax;
                    }

                    if (overtimePolicy?.SeparateOvertimeTax != true)
                    {
                        taxFreeCeiling += overtimePolicy?.TaxCeiling ?? 0m;
                    }
                }
            }

            var previousYearBonus = previousBonusByEmployeeNumber.GetValueOrDefault(profile.EmployeeNumber);
            var annualBasicSalary = CalculateAnnualBasicForBonusTax(
                originalBasicSalary,
                historicalBasicByEmployeeNumber.GetValueOrDefault(profile.EmployeeNumber),
                run.PayPeriodFrom);
            var bonusCalculation = CalculatePayrollBonuses(
                bonusPolicies,
                bonusExceptionsByCode,
                profile,
                basicSalary,
                annualBasicSalary,
                previousYearBonus,
                run.PayPeriodTo,
                activeParameters);

            if (bonusCalculation.TotalAmount > 0)
            {
                foreach (var bonusLine in bonusCalculation.Lines)
                {
                    var bonusTransaction = CreateTransaction(
                        tenantId,
                        run,
                        runEmployee,
                        profile,
                        BonusTransactionType,
                        bonusLine.Description,
                        bonusLine.Amount,
                        bonusLine.Taxable,
                        componentCode: bonusLine.Code);
                    bonusTransaction.SeparateTax = bonusLine.SeparateTax;
                    employeeTransactions.Add(bonusTransaction);
                }

                if (bonusCalculation.NonTaxableAmount > 0)
                {
                    nonTaxableAllowances += bonusCalculation.NonTaxableAmount;
                }

                grossIncome += bonusCalculation.TotalAmount;
                netIncomeBeforeTax += bonusCalculation.TotalAmount;
            }

            var taxRelief = run.IsSeparateBonusRun
                ? 0m
                : employeeTaxReliefs.Count > 0
                ? CalculateEmployeeTaxRelief(employeeTaxReliefsByProfile.GetValueOrDefault(profile.Id) ?? [], basicSalary)
                : CalculateTaxRelief(defaultTaxReliefs, basicSalary);
            var totalAllowanceTaxCeiling = activeParameters?.TotalAllowanceTaxCeiling ?? 0m;
            var taxableAllowanceAmount = Math.Max(0, taxableAllowances - separateTaxableAllowances - totalAllowanceTaxCeiling);
            var taxableBenefitAmount = Math.Max(0, taxableBenefits - separateTaxableBenefits);
            var taxFreeCeilingAdjustment = totalAllowanceTaxCeiling == 0m ? taxFreeCeiling : 0m;
            var taxableBasicSalary = run.IsSeparateBonusRun ? 0m : basicSalary;
            var taxableIncome = Math.Max(0,
                taxableBasicSalary
                - pension.EmployeeContribution
                - nonTaxableDeductions
                - nonTaxableContribution
                + taxableOvertime
                + taxableAllowanceAmount
                + taxableBenefitAmount
                + bonusCalculation.TaxableBonusAmount
                + employerTaxableAmount
                - taxFreeCeilingAdjustment
                - taxRelief);
            var normalIncomeTax = profile.PayTax ? CalculateIncomeTax(taxBands, taxableIncome) : 0m;
            var incomeTax = profile.PayTax ? normalIncomeTax + bonusCalculation.SeparateTaxAmount + separateOvertimeTaxAmount + componentSeparateTaxAmount : 0m;

            if (taxRelief > 0)
            {
                employeeTransactions.Add(CreateTransaction(
                    tenantId,
                    run,
                    runEmployee,
                    profile,
                    TaxReliefTransactionType,
                    "Tax relief",
                    taxRelief,
                    taxable: false,
                    componentCode: TaxReliefTransactionType));
            }

            if (normalIncomeTax > 0)
            {
                employeeTransactions.Add(CreateTransaction(
                    tenantId,
                    run,
                    runEmployee,
                    profile,
                    IncomeTaxTransactionType,
                    "Income tax (normal)",
                    normalIncomeTax,
                    taxable: false,
                    componentCode: "NORMAL"));
            }

            if (bonusCalculation.SeparateTaxAmount > 0 && profile.PayTax)
            {
                employeeTransactions.Add(CreateTransaction(
                    tenantId,
                    run,
                    runEmployee,
                    profile,
                    IncomeTaxTransactionType,
                    "Income tax (bonus)",
                    bonusCalculation.SeparateTaxAmount,
                    taxable: false,
                    componentCode: "BON"));
            }

            if (separateOvertimeTaxAmount > 0 && profile.PayTax)
            {
                employeeTransactions.Add(CreateTransaction(
                    tenantId,
                    run,
                    runEmployee,
                    profile,
                    IncomeTaxTransactionType,
                    "Income tax (overtime)",
                    separateOvertimeTaxAmount,
                    taxable: false,
                    componentCode: OvertimeTransactionType));
            }

            if (componentSeparateTaxAmount > 0 && profile.PayTax)
            {
                employeeTransactions.Add(CreateTransaction(
                    tenantId,
                    run,
                    runEmployee,
                    profile,
                    IncomeTaxTransactionType,
                    "Income tax (separate components)",
                    componentSeparateTaxAmount,
                    taxable: false,
                    componentCode: "SEP"));
            }

            if (!run.IsSeparateBonusRun &&
                promotionEntriesByProfile.TryGetValue(profile.Id, out var promotionEntry) &&
                historicalRunEmployeesByNumber.TryGetValue(profile.EmployeeNumber, out var historicalRunEmployees))
            {
                var currentContributionArrearsSplit = BuildContributionArrearsSplit(employeeTransactions);
                var arrears = CalculatePromotionArrears(
                    promotionEntry,
                    historicalRunEmployees,
                    basicSalary,
                    taxableAllowances + nonTaxableAllowances,
                    currentContributionArrearsSplit,
                    taxableIncome,
                    normalIncomeTax);

                if (arrears.NetAmount > 0)
                {
                    ApplyPromotionArrearsTransactions(
                        tenantId,
                        run,
                        runEmployee,
                        profile,
                        employeeTransactions,
                        arrears);

                    taxableAllowances += arrears.BasicAmount + arrears.AllowanceAmount;
                    employeeContribution += arrears.EmployeeContributionAmount + arrears.OtherContributionAmount;
                    employerContribution += arrears.EmployerContributionAmount + arrears.OtherEmployerContributionAmount;
                    taxableIncome += arrears.TaxableIncomeAmount;
                    incomeTax += arrears.TaxAmount;
                    grossIncome += arrears.BasicAmount + arrears.AllowanceAmount;
                    netIncomeBeforeTax += arrears.BasicAmount + arrears.AllowanceAmount - arrears.EmployeeContributionAmount - arrears.OtherContributionAmount;
                }
            }

            var loanRepayment = run.IsSeparateBonusRun
                ? 0m
                : CalculateLoanRepayments(profile, run.PayPeriodFrom, run.PayPeriodTo, tenantId, run, runEmployee, employeeTransactions);
            var salaryAdvance = run.IsSeparateBonusRun
                ? 0m
                : Math.Round(salaryAdvancesByProfile.GetValueOrDefault(profile.Id), 2);
            if (!run.IsSeparateBonusRun && salaryAdvance > 0)
            {
                nonTaxableDeductions += salaryAdvance;
                employeeTransactions.Add(CreateTransaction(
                    tenantId,
                    run,
                    runEmployee,
                    profile,
                    SalaryAdvanceTransactionType,
                    "Salary advance",
                    salaryAdvance,
                    taxable: false,
                    componentCode: SalaryAdvanceTransactionType));
            }

            var netIncome = netIncomeBeforeTax - incomeTax - loanRepayment - salaryAdvance;

            employeeTransactions.Add(CreateTransaction(tenantId, run, runEmployee, profile, NetPayTransactionType, "Net pay", netIncome, taxable: false));

            runEmployee.TaxableAllowances = taxableAllowances;
            runEmployee.NonTaxableAllowances = nonTaxableAllowances;
            runEmployee.TaxableDeductions = taxableDeductions;
            runEmployee.NonTaxableDeductions = nonTaxableDeductions;
            runEmployee.EmployeeContribution = employeeContribution;
            runEmployee.EmployerContribution = employerContribution;
            runEmployee.TaxRelief = taxRelief;
            runEmployee.TaxableIncome = taxableIncome;
            runEmployee.IncomeTax = incomeTax;
            runEmployee.GrossIncome = grossIncome;
            runEmployee.NetIncome = netIncome;

            runEmployees.Add(runEmployee);
            transactions.AddRange(employeeTransactions);
        }

        run.EmployeeCount = runEmployees.Count;
        run.GrossAmount = runEmployees.Sum(e => e.GrossIncome);
        run.NetAmount = runEmployees.Sum(e => e.NetIncome);
        run.TaxAmount = runEmployees.Sum(e => e.IncomeTax);
        run.EmployeeContributionAmount = runEmployees.Sum(e => e.EmployeeContribution);
        run.EmployerContributionAmount = runEmployees.Sum(e => e.EmployerContribution);
        run.Status = PayrollRunStatus.Calculated;
        run.CalculatedAt = DateTime.UtcNow;
        run.CalculatedByUserId = userId;

        _context.PayrollRunEmployees.AddRange(runEmployees);
        _context.PayrollTransactions.AddRange(transactions);

        var journalLines = await BuildJournalLinesAsync(tenantId, run.Id, transactions, cancellationToken);
        _context.PayrollJournalLines.AddRange(journalLines);

        await _context.SaveChangesAsync(cancellationToken);

        var calculated = await PayrollRunQuery(tenantId)
            .FirstAsync(e => e.Id == run.Id, cancellationToken);

        return ToDto(calculated, includeChildren: true);
    }

    public async Task<PayrollRunDto> SubmitRunForReviewAsync(Guid tenantId, Guid runId, Guid? userId, PayrollRunActionDto dto, CancellationToken cancellationToken = default)
    {
        var run = await GetMutableRunAsync(tenantId, runId, cancellationToken);
        if (run.Status != PayrollRunStatus.Calculated)
        {
            throw new InvalidOperationException("Only calculated payroll runs can be submitted for review.");
        }

        run.Status = PayrollRunStatus.InReview;
        run.ReviewedAt = DateTime.UtcNow;
        run.ReviewedByUserId = userId;
        AppendNotes(run, dto.Notes);
        await _context.SaveChangesAsync(cancellationToken);
        return await RequireRunDtoAsync(tenantId, run.Id, cancellationToken);
    }

    public async Task<PayrollRunDto> ApproveRunAsync(Guid tenantId, Guid runId, Guid? userId, PayrollRunActionDto dto, CancellationToken cancellationToken = default)
    {
        var run = await GetMutableRunAsync(tenantId, runId, cancellationToken);
        if (run.Status != PayrollRunStatus.InReview)
        {
            throw new InvalidOperationException("Only payroll runs in review can be approved.");
        }

        run.Status = PayrollRunStatus.Approved;
        run.ApprovedAt = DateTime.UtcNow;
        run.ApprovedByUserId = userId;
        AppendNotes(run, dto.Notes);
        await _context.SaveChangesAsync(cancellationToken);
        return await RequireRunDtoAsync(tenantId, run.Id, cancellationToken);
    }

    public async Task<PayrollRunDto> CloseRunAsync(Guid tenantId, Guid runId, Guid? userId, PayrollRunActionDto dto, CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            var run = await GetMutableRunAsync(tenantId, runId, cancellationToken);
            if (run.Status != PayrollRunStatus.Approved)
            {
                throw new InvalidOperationException("Only approved payroll runs can be closed.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var hasJournalLines = await _context.PayrollJournalLines.AnyAsync(e => e.TenantId == tenantId && e.PayrollRunId == run.Id, cancellationToken);
            if (!hasJournalLines)
            {
                var transactions = await _context.PayrollTransactions
                    .AsNoTracking()
                    .Where(e => e.TenantId == tenantId && e.PayrollRunId == run.Id)
                    .ToListAsync(cancellationToken);
                var journalLines = await BuildJournalLinesAsync(tenantId, run.Id, transactions, cancellationToken);
                _context.PayrollJournalLines.AddRange(journalLines);
            }

            run.Status = PayrollRunStatus.Closed;
            run.ClosedAt = DateTime.UtcNow;
            run.ClosedByUserId = userId;
            AppendNotes(run, dto.Notes);
            await AdvanceBonusPoliciesAfterCloseAsync(tenantId, run, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return await RequireRunDtoAsync(tenantId, run.Id, cancellationToken);
        });
    }

    public async Task<PayrollRunDto> RollbackRunAsync(Guid tenantId, Guid runId, Guid? userId, PayrollRunActionDto dto, CancellationToken cancellationToken = default)
    {
        var run = await GetMutableRunAsync(tenantId, runId, cancellationToken);
        if (run.Status == PayrollRunStatus.Closed)
        {
            throw new InvalidOperationException("Closed payroll runs cannot be rolled back.");
        }

        run.Status = PayrollRunStatus.RolledBack;
        run.ReviewedAt = null;
        run.ReviewedByUserId = null;
        run.ApprovedAt = null;
        run.ApprovedByUserId = null;
        AppendNotes(run, dto.Notes);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Payroll run {RunId} rolled back by {UserId}", run.Id, userId);
        return await RequireRunDtoAsync(tenantId, run.Id, cancellationToken);
    }

    public async Task<PayrollSummaryReportDto> GetRunSummaryReportAsync(
        Guid tenantId,
        Guid runId,
        Guid? userId,
        bool createSnapshot = false,
        CancellationToken cancellationToken = default)
    {
        var run = await PayrollRunQuery(tenantId)
            .FirstOrDefaultAsync(e => e.Id == runId, cancellationToken)
            ?? throw new KeyNotFoundException("Payroll run not found.");

        EnsureRunHasSavedOutput(run);

        var report = BuildSummaryReport(run);
        if (!createSnapshot)
        {
            var latestSnapshot = await _context.PayrollReportSnapshots
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId && e.PayrollRunId == run.Id && e.ReportType == PayrollReportType.RunSummary)
                .OrderByDescending(e => e.GeneratedAt)
                .FirstOrDefaultAsync(cancellationToken);
            report.Snapshot = latestSnapshot == null ? null : ToDto(latestSnapshot);
            return report;
        }

        if (run.Status != PayrollRunStatus.Closed)
        {
            throw new InvalidOperationException("Post the payroll run before saving final summary snapshots.");
        }

        var snapshot = new PayrollReportSnapshot
        {
            TenantId = tenantId,
            PayrollRunId = run.Id,
            ReportType = PayrollReportType.RunSummary,
            ReportName = "Payroll run summary",
            SnapshotNumber = $"PRS-{run.RunNumber}-{DateTime.UtcNow:yyyyMMddHHmmss}",
            GeneratedAt = DateTime.UtcNow,
            GeneratedByUserId = userId,
            SnapshotJson = JsonSerializer.Serialize(report),
            Notes = $"Summary snapshot for payroll run {run.RunNumber}"
        };

        _context.PayrollReportSnapshots.Add(snapshot);
        await _context.SaveChangesAsync(cancellationToken);
        report.Snapshot = ToDto(snapshot);
        return report;
    }

    public async Task<IReadOnlyList<PayrollPayslipDto>> GetPayslipsAsync(
        Guid tenantId,
        Guid runId,
        Guid? employeeId = null,
        string? categoryType = null,
        string? categoryValue = null,
        CancellationToken cancellationToken = default)
    {
        var run = await PayrollRunQuery(tenantId)
            .FirstOrDefaultAsync(e => e.Id == runId, cancellationToken)
            ?? throw new KeyNotFoundException("Payroll run not found.");

        EnsureRunHasSavedOutput(run);

        var snapshots = await _context.PayrollPayslipSnapshots
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.PayrollRunId == run.Id)
            .ToDictionaryAsync(e => e.PayrollRunEmployeeId, cancellationToken);

        var payslipContext = await LoadPayslipContextAsync(tenantId, run, cancellationToken);

        var employees = employeeId.HasValue
            ? run.Employees.Where(e => e.EmployeeId == employeeId.Value)
            : run.Employees;

        if (!employeeId.HasValue && !string.IsNullOrWhiteSpace(categoryType) && !string.IsNullOrWhiteSpace(categoryValue))
        {
            employees = employees.Where(e => PayslipCategoryMatches(e, payslipContext, categoryType, categoryValue));
        }

        return employees
            .OrderBy(e => e.EmployeeNumber)
            .Select(employee => BuildPayslip(run, employee, snapshots.GetValueOrDefault(employee.Id), payslipContext))
            .ToList();
    }

    public async Task<IReadOnlyList<PayrollPayslipSnapshotDto>> GeneratePayslipSnapshotsAsync(
        Guid tenantId,
        Guid runId,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var run = await PayrollRunQuery(tenantId)
            .FirstOrDefaultAsync(e => e.Id == runId, cancellationToken)
            ?? throw new KeyNotFoundException("Payroll run not found.");

        EnsureRunHasSavedOutput(run);
        if (run.Status != PayrollRunStatus.Closed)
        {
            throw new InvalidOperationException("Post the payroll run before saving final payslip snapshots.");
        }

        var payslipContext = await LoadPayslipContextAsync(tenantId, run, cancellationToken);

        var existing = await _context.PayrollPayslipSnapshots
            .Where(e => e.TenantId == tenantId && e.PayrollRunId == run.Id)
            .ToDictionaryAsync(e => e.PayrollRunEmployeeId, cancellationToken);

        foreach (var runEmployee in run.Employees.OrderBy(e => e.EmployeeNumber))
        {
            existing.TryGetValue(runEmployee.Id, out var snapshot);
            snapshot ??= new PayrollPayslipSnapshot
            {
                TenantId = tenantId,
                PayrollRunId = run.Id,
                PayrollRunEmployeeId = runEmployee.Id,
                EmployeeId = runEmployee.EmployeeId,
                EmployeeNumber = runEmployee.EmployeeNumber,
                PayslipNumber = $"{(run.IsSeparateBonusRun ? "BS" : "PS")}-{run.RunNumber}-{runEmployee.EmployeeNumber}"
            };

            var payslip = BuildPayslip(run, runEmployee, snapshot.Id == Guid.Empty ? null : snapshot, payslipContext);
            snapshot.EmployeeName = runEmployee.EmployeeName;
            snapshot.GeneratedAt = DateTime.UtcNow;
            snapshot.GeneratedByUserId = userId;
            snapshot.GrossIncome = runEmployee.GrossIncome;
            snapshot.NetIncome = runEmployee.NetIncome;
            snapshot.TaxAmount = runEmployee.IncomeTax;
            snapshot.EmployeeContribution = runEmployee.EmployeeContribution;
            snapshot.SnapshotJson = JsonSerializer.Serialize(payslip);

            AddIfNew(_context.PayrollPayslipSnapshots, snapshot);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return await _context.PayrollPayslipSnapshots
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.PayrollRunId == run.Id)
            .OrderBy(e => e.EmployeeNumber)
            .Select(e => ToDto(e))
            .ToListAsync(cancellationToken);
    }

    public async Task<PayrollPayslipEmailResultDto> EmailPayslipsAsync(
        Guid tenantId,
        Guid runId,
        Guid? userId,
        PayrollPayslipEmailRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var run = await PayrollRunQuery(tenantId)
            .FirstOrDefaultAsync(e => e.Id == runId, cancellationToken)
            ?? throw new KeyNotFoundException("Payroll run not found.");

        EnsureRunHasSavedOutput(run);

        var selectedEmployeeIds = (dto.EmployeeIds ?? [])
            .Where(e => e != Guid.Empty)
            .Distinct()
            .ToHashSet();

        var runEmployees = run.Employees
            .Where(e => selectedEmployeeIds.Count == 0 || selectedEmployeeIds.Contains(e.EmployeeId))
            .OrderBy(e => e.EmployeeNumber)
            .ToList();

        if (runEmployees.Count == 0)
        {
            throw new InvalidOperationException("No payslips matched the selected employees.");
        }

        var snapshots = await _context.PayrollPayslipSnapshots
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.PayrollRunId == run.Id)
            .ToDictionaryAsync(e => e.PayrollRunEmployeeId, cancellationToken);

        var payslipContext = await LoadPayslipContextAsync(tenantId, run, cancellationToken);
        var periodLabel = FormatPayrollPeriodMonth(run.PayPeriodFrom, run.PayPeriodTo);
        var subject = TrimOrNull(dto.Subject) ?? $"Payslip - {periodLabel}";
        var message = TrimOrNull(dto.Message);
        var recipients = new List<PayrollPayslipEmailRecipientDto>();

        foreach (var runEmployee in runEmployees)
        {
            var payslip = BuildPayslip(run, runEmployee, snapshots.GetValueOrDefault(runEmployee.Id), payslipContext);
            var email = payslip.EmployeeEmail?.Trim();
            var result = new PayrollPayslipEmailRecipientDto
            {
                EmployeeId = payslip.EmployeeId,
                EmployeeNumber = payslip.EmployeeNumber,
                EmployeeName = payslip.EmployeeName,
                EmailAddress = email
            };

            if (string.IsNullOrWhiteSpace(email))
            {
                result.Message = "Employee email address is not configured.";
                recipients.Add(result);
                continue;
            }

            var html = BuildPayslipEmailHtml(payslip, periodLabel, message);
            var attachments = dto.AttachHtmlCopy
                ? new List<WebEmailAttachment>
                {
                    new()
                    {
                        FileName = BuildPayslipEmailFileName(payslip),
                        Content = Encoding.UTF8.GetBytes(html),
                        ContentType = "text/html"
                    }
                }
                : [];

            var sent = await _emailService.SendEmailWithAttachmentsAsync(email, subject, html, attachments, isHtml: true);
            result.Sent = sent;
            result.Message = sent ? "Sent" : "Email service reported a send failure.";
            recipients.Add(result);
        }

        return new PayrollPayslipEmailResultDto
        {
            PayrollRunId = run.Id,
            RunNumber = run.RunNumber,
            RequestedCount = runEmployees.Count,
            SentCount = recipients.Count(e => e.Sent),
            FailedCount = recipients.Count(e => !e.Sent),
            Recipients = recipients
        };
    }

    public async Task<PayrollJournalPostingDto> PostPayrollJournalAsync(
        Guid tenantId,
        Guid runId,
        Guid? userId,
        PayrollRunActionDto dto,
        CancellationToken cancellationToken = default)
    {
        var run = await PayrollRunQuery(tenantId)
            .FirstOrDefaultAsync(e => e.Id == runId, cancellationToken)
            ?? throw new KeyNotFoundException("Payroll run not found.");

        EnsureRunHasSavedOutput(run);

        if (run.JournalLines.Any(e => e.Posted || e.JournalEntryId.HasValue))
        {
            throw new InvalidOperationException("Payroll journal has already been posted.");
        }

        if (run.JournalLines.Count == 0)
        {
            var journalLines = await BuildJournalLinesAsync(tenantId, run.Id, run.Transactions.ToList(), cancellationToken);
            _context.PayrollJournalLines.AddRange(journalLines);
            await _context.SaveChangesAsync(cancellationToken);

            run = await PayrollRunQuery(tenantId)
                .FirstAsync(e => e.Id == runId, cancellationToken);
        }

        var unmapped = run.JournalLines
            .Where(e => string.IsNullOrWhiteSpace(e.AccountCode) || e.AccountCode.Equals("UNMAPPED", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (unmapped.Count > 0)
        {
            throw new InvalidOperationException("Payroll journal contains unmapped GL accounts. Complete Payroll Journal Mapping before posting.");
        }

        var totalDebit = run.JournalLines.Where(e => e.DebitCredit == "DR").Sum(e => e.Amount);
        var totalCredit = run.JournalLines.Where(e => e.DebitCredit == "CR").Sum(e => e.Amount);
        if (totalDebit != totalCredit)
        {
            throw new InvalidOperationException($"Payroll journal is not balanced. Debit: {totalDebit}, Credit: {totalCredit}.");
        }

        var accountCodes = run.JournalLines.Select(e => e.AccountCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var accounts = await _context.Accounts
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && (accountCodes.Contains(e.AccountCode) || accountCodes.Contains(e.AccountNumber)))
            .ToListAsync(cancellationToken);

        var accountsByCode = accounts
            .SelectMany(e => new[]
            {
                new { Code = e.AccountCode, AccountId = e.Id },
                new { Code = e.AccountNumber, AccountId = e.Id }
            })
            .Where(e => !string.IsNullOrWhiteSpace(e.Code))
            .GroupBy(e => e.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => e.First().AccountId, StringComparer.OrdinalIgnoreCase);

        var missingAccounts = accountCodes.Where(code => !accountsByCode.ContainsKey(code)).ToList();
        if (missingAccounts.Count > 0)
        {
            throw new InvalidOperationException($"Payroll journal account code(s) not found in Chart of Accounts: {string.Join(", ", missingAccounts)}.");
        }

        var postedAt = DateTime.UtcNow;
        var journalDto = new CreateJournalEntryDto
        {
            JournalNumber = $"PAY-{run.RunNumber}",
            TransactionDate = run.ClosedAt ?? postedAt,
            Description = $"Payroll journal for {run.RunNumber}",
            Reference = run.RunNumber,
            SourceModule = "PAYROLL",
            Transactions = run.JournalLines
                .OrderBy(e => e.SequenceNo)
                .Select(e => new CreateAccountTransactionDto
                {
                    AccountId = accountsByCode[e.AccountCode],
                    Amount = e.Amount,
                    TransactionType = e.DebitCredit == "DR" ? "Debit" : "Credit",
                    Description = e.Description,
                    Reference = run.RunNumber,
                    CurrencyCode = run.CurrencyCode,
                    LineNumber = e.SequenceNo
                })
                .ToList()
        };

        var created = await _journalEntryService.CreateJournalEntryAsync(journalDto, cancellationToken);
        var posted = await _journalEntryService.PostJournalEntryAsync(created.Id, cancellationToken);

        foreach (var line in run.JournalLines)
        {
            line.Posted = true;
            line.JournalEntryId = posted.Id;
        }

        await ApplyPostedLoanRepaymentsAsync(tenantId, run, postedAt, cancellationToken);

        if (run.Status != PayrollRunStatus.Closed)
        {
            run.Status = PayrollRunStatus.Closed;
            run.ClosedAt = postedAt;
            run.ClosedByUserId = userId;
        }
        else
        {
            run.ClosedAt ??= postedAt;
            run.ClosedByUserId ??= userId;
        }

        AppendNotes(run, dto.Notes);
        await AdvanceBonusPoliciesAfterCloseAsync(tenantId, run, cancellationToken);
        await AdvanceActivePayrollPeriodAfterCloseAsync(tenantId, run, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await GeneratePayslipSnapshotsAsync(tenantId, run.Id, userId, cancellationToken);

        return new PayrollJournalPostingDto
        {
            PayrollRunId = run.Id,
            RunNumber = run.RunNumber,
            JournalEntryId = posted.Id,
            JournalNumber = posted.JournalNumber,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            PostedAt = postedAt,
            JournalEntry = posted,
            Lines = run.JournalLines.OrderBy(e => e.SequenceNo).Select(ToDto).ToList()
        };
    }

    private async Task<PayrollParameterSet?> GetActivePayrollParametersAsync(Guid tenantId, CancellationToken cancellationToken)
        => await _context.PayrollParameterSets
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.IsActive)
            .OrderBy(e => e.Code)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<IReadOnlyList<PayrollEmployeeProfile>> LoadProfilesForEntryLinesAsync(
        Guid tenantId,
        IEnumerable<Guid?> employeeProfileIds,
        IEnumerable<string?> employeeNumbers,
        CancellationToken cancellationToken)
    {
        var profileIds = employeeProfileIds
            .Where(e => e.HasValue && e.Value != Guid.Empty)
            .Select(e => e!.Value)
            .Distinct()
            .ToList();

        var numbers = employeeNumbers
            .Select(e => e?.Trim())
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (profileIds.Count == 0 && numbers.Count == 0)
        {
            return [];
        }

        return await PayrollProfileQuery(tenantId)
            .Where(e =>
                profileIds.Contains(e.Id) ||
                numbers.Contains(e.EmployeeNumber) ||
                (e.LegacyEmployeeNumber != null && numbers.Contains(e.LegacyEmployeeNumber)))
            .ToListAsync(cancellationToken);
    }

    private static PayrollProfileLookup BuildProfileLookup(IEnumerable<PayrollEmployeeProfile> profiles)
    {
        var profileList = profiles.ToList();
        return new PayrollProfileLookup(
            profileList.ToDictionary(e => e.Id),
            profileList
                .GroupBy(e => e.EmployeeNumber, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(e => e.Key, e => e.First(), StringComparer.OrdinalIgnoreCase),
            profileList
                .Where(e => !string.IsNullOrWhiteSpace(e.LegacyEmployeeNumber))
                .GroupBy(e => e.LegacyEmployeeNumber!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(e => e.Key, e => e.First(), StringComparer.OrdinalIgnoreCase));
    }

    private static PayrollEmployeeProfile? ResolveProfile(PayrollProfileLookup lookup, Guid? employeeProfileId, string? employeeNumber)
    {
        if (employeeProfileId.HasValue && employeeProfileId.Value != Guid.Empty &&
            lookup.ById.TryGetValue(employeeProfileId.Value, out var profileById))
        {
            return profileById;
        }

        var number = employeeNumber?.Trim();
        if (!string.IsNullOrWhiteSpace(number))
        {
            if (lookup.ByEmployeeNumber.TryGetValue(number, out var profileByNumber))
            {
                return profileByNumber;
            }

            if (lookup.ByLegacyEmployeeNumber.TryGetValue(number, out var profileByLegacyNumber))
            {
                return profileByLegacyNumber;
            }
        }

        return null;
    }

    private IQueryable<PayrollEmployeeProfile> PayrollProfileQuery(Guid tenantId)
        => _context.PayrollEmployeeProfiles
            .AsSplitQuery()
            .Include(e => e.Employee)
                .ThenInclude(e => e.Department)
            .Include(e => e.Employee)
                .ThenInclude(e => e.Section)
            .Include(e => e.Employee)
                .ThenInclude(e => e.Position)
                    .ThenInclude(e => e.StaffLevel)
            .Include(e => e.Employee)
                .ThenInclude(e => e.Position)
                    .ThenInclude(e => e.SalaryGrade)
            .Include(e => e.Employee)
                .ThenInclude(e => e.Location)
            .Include(e => e.Employee)
                .ThenInclude(e => e.Station)
            .Include(e => e.SalaryBasis)
            .Include(e => e.PaymentMethods.OrderBy(m => m.SequenceNo))
            .Include(e => e.EmployeeComponents)
                .ThenInclude(e => e.PayrollComponent)
            .Include(e => e.Loans)
                .ThenInclude(e => e.LoanPolicy)
            .Include(e => e.Loans)
                .ThenInclude(e => e.Schedules.OrderBy(s => s.SequenceNo))
            .Where(e => e.TenantId == tenantId);

    private async Task<PayrollComponent?> FindPayrollComponentAsync(
        Guid tenantId,
        Guid? payrollComponentId,
        string? componentCode,
        PayrollComponentType? componentType,
        CancellationToken cancellationToken)
    {
        var query = _context.PayrollComponents
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId);

        if (payrollComponentId.HasValue && payrollComponentId.Value != Guid.Empty)
        {
            return await query.FirstOrDefaultAsync(e => e.Id == payrollComponentId.Value, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(componentCode))
        {
            return null;
        }

        var normalizedCode = NormalizeCode(componentCode);
        query = query.Where(e => e.Code == normalizedCode);
        if (componentType.HasValue)
        {
            query = query.Where(e => e.ComponentType == componentType.Value);
        }

        return await query
            .OrderBy(e => e.ComponentType)
            .ThenBy(e => e.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private IQueryable<PayrollLoan> PayrollLoanQuery(Guid tenantId)
        => _context.PayrollLoans
            .AsSplitQuery()
            .Include(e => e.EmployeeProfile)
                .ThenInclude(e => e.Employee)
            .Include(e => e.EmployeeProfile)
                .ThenInclude(e => e.SalaryBasis)
            .Include(e => e.LoanPolicy)
            .Include(e => e.Schedules.OrderBy(s => s.SequenceNo))
            .Where(e => e.TenantId == tenantId);

    private IQueryable<PayrollRun> PayrollRunQuery(Guid tenantId)
        => _context.PayrollRuns
            .AsSplitQuery()
            .Include(e => e.Employees.OrderBy(x => x.EmployeeNumber))
            .Include(e => e.Transactions.OrderBy(x => x.EmployeeNumber).ThenBy(x => x.TransactionType))
            .Include(e => e.JournalLines.OrderBy(x => x.SequenceNo))
            .Where(e => e.TenantId == tenantId);

    private static void EnsureRunHasSavedOutput(PayrollRun run)
    {
        if (run.Status is PayrollRunStatus.Draft or PayrollRunStatus.RolledBack || run.Employees.Count == 0)
        {
            throw new InvalidOperationException("Calculate the payroll run before generating reports.");
        }
    }

    private static PayrollSummaryReportDto BuildSummaryReport(PayrollRun run)
        => new()
        {
            PayrollRunId = run.Id,
            RunNumber = run.RunNumber,
            PayPeriod = run.PayPeriod,
            PayPeriodFrom = run.PayPeriodFrom,
            PayPeriodTo = run.PayPeriodTo,
            CurrencyCode = run.CurrencyCode,
            EmployeeCount = run.EmployeeCount,
            GrossAmount = run.GrossAmount,
            NetAmount = run.NetAmount,
            TaxAmount = run.TaxAmount,
            EmployeeContributionAmount = run.EmployeeContributionAmount,
            EmployerContributionAmount = run.EmployerContributionAmount,
            Components = run.Transactions
                .GroupBy(e => new { e.TransactionType, e.ComponentCode, e.Description })
                .OrderBy(e => e.Key.TransactionType)
                .ThenBy(e => e.Key.ComponentCode)
                .Select(e => new PayrollSummaryComponentDto
                {
                    TransactionType = e.Key.TransactionType,
                    ComponentCode = e.Key.ComponentCode,
                    Description = e.Key.Description ?? e.Key.TransactionType,
                    Amount = e.Sum(x => x.Amount),
                    EmployerAmount = e.Sum(x => x.EmployerAmount ?? 0m)
                })
                .ToList(),
            Employees = run.Employees
                .OrderBy(e => e.EmployeeNumber)
                .Select(e => new PayrollSummaryEmployeeDto
                {
                    EmployeeId = e.EmployeeId,
                    EmployeeNumber = e.EmployeeNumber,
                    EmployeeName = e.EmployeeName,
                    BasicSalary = e.BasicSalary,
                    GrossIncome = e.GrossIncome,
                    TaxableIncome = e.TaxableIncome,
                    TaxRelief = e.TaxRelief,
                    IncomeTax = e.IncomeTax,
                    EmployeeContribution = e.EmployeeContribution,
                    EmployerContribution = e.EmployerContribution,
                    NetIncome = e.NetIncome
                })
                .ToList()
        };

    private async Task<PayrollPayslipBuildContext> LoadPayslipContextAsync(Guid tenantId, PayrollRun run, CancellationToken cancellationToken)
    {
        var employeeIds = run.Employees
            .Select(e => e.EmployeeId)
            .Distinct()
            .ToList();

        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == tenantId, cancellationToken);

        var companyProfile = await _context.PayrollCompanyProfiles
            .AsNoTracking()
            .Include(e => e.BusinessUnits)
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.IsActive)
            .ThenBy(e => e.CompanyName)
            .FirstOrDefaultAsync(cancellationToken);

        var profiles = employeeIds.Count == 0
            ? []
            : await _context.PayrollEmployeeProfiles
                .AsNoTracking()
                .AsSplitQuery()
                .Include(e => e.Employee)
                    .ThenInclude(e => e.Department)
                .Include(e => e.Employee)
                    .ThenInclude(e => e.Section)
                .Include(e => e.Employee)
                    .ThenInclude(e => e.Position)
                        .ThenInclude(e => e.StaffLevel)
                .Include(e => e.Employee)
                    .ThenInclude(e => e.Location)
                .Include(e => e.Employee)
                    .ThenInclude(e => e.Station)
                .Include(e => e.Employee)
                    .ThenInclude(e => e.BankDetails)
                        .ThenInclude(e => e.Bank)
                .Include(e => e.Employee)
                    .ThenInclude(e => e.BankDetails)
                        .ThenInclude(e => e.Branch)
                .Include(e => e.PaymentMethods.OrderBy(m => m.SequenceNo))
                .Where(e => e.TenantId == tenantId && employeeIds.Contains(e.EmployeeId))
                .ToListAsync(cancellationToken);

        var profileIds = profiles.Select(e => e.Id).Distinct().ToList();
        var openingBalances = profileIds.Count == 0
            ? []
            : await _context.PayrollContributionOpeningBalances
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId && e.IsActive && profileIds.Contains(e.EmployeeProfileId))
                .ToListAsync(cancellationToken);

        var contributionTransactions = profileIds.Count == 0
            ? []
            : await _context.PayrollContributionTransactions
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId &&
                            e.IsActive &&
                            profileIds.Contains(e.EmployeeProfileId) &&
                            e.EffectiveDate.Date <= run.PayPeriodTo.Date)
                .ToListAsync(cancellationToken);

        var employeeBanks = await _context.EmployeeBanks
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var employeeBankBranches = await _context.EmployeeBankBranches
            .AsNoTracking()
            .Include(e => e.Bank)
            .Where(e => e.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var payrollCodeValues = await _context.PayrollCodeValues
            .AsNoTracking()
            .Include(e => e.PayrollCodeType)
            .Where(e => e.TenantId == tenantId && !e.Blocked)
            .ToListAsync(cancellationToken);

        var payrollBankBranches = await _context.PayrollBankBranches
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var timesheetSummaries = profileIds.Count == 0
            ? []
            : await _context.PayrollTimesheetSummaries
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId &&
                            e.IsActive &&
                            profileIds.Contains(e.EmployeeProfileId) &&
                            e.PayPeriod == run.PayPeriod)
                .ToListAsync(cancellationToken);

        return new PayrollPayslipBuildContext(
            tenant,
            companyProfile,
            profiles
                .GroupBy(e => e.EmployeeId)
                .ToDictionary(e => e.Key, e => e.OrderByDescending(x => x.PayrollActive).ThenBy(x => x.EmployeeNumber).First()),
            openingBalances
                .GroupBy(e => e.EmployeeProfileId)
                .ToDictionary(e => e.Key, e => (IReadOnlyList<PayrollContributionOpeningBalance>)e.ToList()),
            contributionTransactions
                .GroupBy(e => e.EmployeeProfileId)
                .ToDictionary(e => e.Key, e => (IReadOnlyList<PayrollContributionTransaction>)e.ToList()),
            BuildEmployeeBankNameLookup(employeeBanks),
            BuildEmployeeBankBranchNameLookup(employeeBankBranches),
            BuildPayrollBankNameLookup(payrollCodeValues),
            BuildPayrollBankBranchNameLookup(payrollBankBranches),
            timesheetSummaries
                .GroupBy(e => e.EmployeeProfileId)
                .ToDictionary(e => e.Key, e => e.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).First()));
    }

    private static PayrollPayslipDto BuildPayslip(PayrollRun run, PayrollRunEmployee runEmployee, PayrollPayslipSnapshot? snapshot, PayrollPayslipBuildContext context)
    {
        var transactions = run.Transactions
            .Where(e => e.PayrollRunEmployeeId == runEmployee.Id)
            .OrderBy(e => e.TransactionType)
            .ThenBy(e => e.ComponentCode)
            .ToList();
        context.ProfilesByEmployeeId.TryGetValue(runEmployee.EmployeeId, out var profile);
        var bonusIncomeTax = Math.Round(transactions
            .Where(e => e.TransactionType.Equals(IncomeTaxTransactionType, StringComparison.OrdinalIgnoreCase) &&
                        ((e.ComponentCode?.Equals("BON", StringComparison.OrdinalIgnoreCase) ?? false) ||
                         (e.Description?.Contains("bonus", StringComparison.OrdinalIgnoreCase) ?? false)))
            .Sum(e => e.Amount), 2);
        var overtimeIncomeTax = Math.Round(transactions
            .Where(e => e.TransactionType.Equals(IncomeTaxTransactionType, StringComparison.OrdinalIgnoreCase) &&
                        ((e.ComponentCode?.Equals(OvertimeTransactionType, StringComparison.OrdinalIgnoreCase) ?? false) ||
                         (e.Description?.Contains("overtime", StringComparison.OrdinalIgnoreCase) ?? false)))
            .Sum(e => e.Amount), 2);
        var normalIncomeTax = Math.Max(0, Math.Round(runEmployee.IncomeTax - bonusIncomeTax - overtimeIncomeTax, 2));
        var overtimeSummary = profile != null && context.TimesheetSummariesByProfileId.TryGetValue(profile.Id, out var summary)
            ? summary
            : null;

        return new PayrollPayslipDto
        {
            PayrollRunId = run.Id,
            PayrollRunEmployeeId = runEmployee.Id,
            EmployeeId = runEmployee.EmployeeId,
            RunNumber = run.RunNumber,
            PayPeriod = run.PayPeriod,
            PayPeriodFrom = run.PayPeriodFrom,
            PayPeriodTo = run.PayPeriodTo,
            IsSeparateBonusRun = run.IsSeparateBonusRun,
            SeparateBonusCode = run.SeparateBonusCode,
            CompanyName = BuildCompanyName(context),
            CompanyAddress = BuildCompanyAddress(context),
            CompanyPhone = BuildCompanyPhone(context),
            EmployeeNumber = runEmployee.EmployeeNumber,
            EmployeeName = runEmployee.EmployeeName,
            EmployeeEmail = profile?.Employee.EmailAddress,
            DepartmentName = profile?.Employee.Department?.Name,
            SectionName = profile?.Employee.Section?.Name,
            PositionTitle = profile?.Employee.Position?.Title,
            StaffCategory = profile?.Employee.Position?.StaffLevel?.Name ?? profile?.Employee.EmploymentType.ToString(),
            JobLocation = profile?.Employee.Location?.Name ?? profile?.Employee.Station?.Name,
            SsfNumber = profile?.SsfNumber ?? profile?.Employee.SocialSecurityNumber,
            StaffTin = profile?.TinNumber ?? profile?.Employee.TINNumber ?? profile?.Employee.TaxNumber,
            CurrencyCode = runEmployee.CurrencyCode,
            BasicSalary = runEmployee.BasicSalary,
            GrossIncome = runEmployee.GrossIncome,
            TaxableIncome = runEmployee.TaxableIncome,
            TaxRelief = runEmployee.TaxRelief,
            IncomeTax = runEmployee.IncomeTax,
            NormalIncomeTax = normalIncomeTax,
            BonusIncomeTax = bonusIncomeTax,
            OvertimeIncomeTax = overtimeIncomeTax,
            EmployeeContribution = runEmployee.EmployeeContribution,
            EmployerContribution = runEmployee.EmployerContribution,
            NetIncome = runEmployee.NetIncome,
            Overtime = BuildPayslipOvertime(overtimeSummary, transactions),
            Earnings = transactions.Where(IsPayslipEarning).Select(ToDto).ToList(),
            Deductions = BuildPayslipDeductions(transactions, runEmployee),
            Contributions = BuildContributionStatement(context, profile, transactions),
            BankDetails = BuildBankDetails(context, profile, runEmployee, run.PayPeriodFrom, run.PayPeriodTo),
            Snapshot = snapshot == null ? null : ToDto(snapshot)
        };
    }

    private static bool PayslipCategoryMatches(
        PayrollRunEmployee runEmployee,
        PayrollPayslipBuildContext context,
        string categoryType,
        string categoryValue)
    {
        var currentValue = ResolvePayslipCategoryValue(runEmployee, context, categoryType);
        return !string.IsNullOrWhiteSpace(currentValue) &&
               currentValue.Trim().Equals(categoryValue.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolvePayslipCategoryValue(
        PayrollRunEmployee runEmployee,
        PayrollPayslipBuildContext context,
        string categoryType)
    {
        context.ProfilesByEmployeeId.TryGetValue(runEmployee.EmployeeId, out var profile);
        var normalizedType = categoryType.Trim().Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty).ToUpperInvariant();

        return normalizedType switch
        {
            "STAFFCATEGORY" or "CATEGORY" => profile?.Employee.Position?.StaffLevel?.Name ?? profile?.Employee.EmploymentType.ToString(),
            "DEPARTMENT" => profile?.Employee.Department?.Name,
            "SECTION" => profile?.Employee.Section?.Name,
            "POSITION" => profile?.Employee.Position?.Title,
            "LOCATION" => profile?.Employee.Location?.Name ?? profile?.Employee.Station?.Name,
            _ => null
        };
    }

    private static string BuildCompanyName(PayrollPayslipBuildContext context)
        => TrimOrNull(context.CompanyProfile?.CompanyName) ?? TrimOrNull(context.Tenant?.Name) ?? string.Empty;

    private static string? BuildCompanyAddress(PayrollPayslipBuildContext context)
    {
        var companyProfile = context.CompanyProfile;
        if (companyProfile != null)
        {
            var addressParts = new[]
            {
                companyProfile.AddressLine1,
                companyProfile.AddressLine2,
                companyProfile.AddressLine3,
                companyProfile.CityOrTown,
                companyProfile.RegionOrState,
                companyProfile.Country
            }
            .Where(e => !string.IsNullOrWhiteSpace(e) && !LooksLikePhoneLine(e))
            .Select(e => e!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

            if (addressParts.Count > 0)
            {
                return string.Join(", ", addressParts);
            }
        }

        return TrimOrNull(context.Tenant?.Address);
    }

    private static string? BuildCompanyPhone(PayrollPayslipBuildContext context)
    {
        var companyProfile = context.CompanyProfile;
        if (companyProfile != null)
        {
            var companyPhone = new[] { companyProfile.AddressLine1, companyProfile.AddressLine2, companyProfile.AddressLine3 }
                .FirstOrDefault(LooksLikePhoneLine);
            if (companyPhone != null)
            {
                return companyPhone;
            }
        }

        return TrimOrNull(context.Tenant?.ContactPhone);
    }

    private static PayrollPayslipOvertimeDto? BuildPayslipOvertime(
        PayrollTimesheetSummary? summary,
        IReadOnlyList<PayrollTransaction> transactions)
    {
        var overtimeAmount = transactions
            .Where(e => e.TransactionType.Equals(OvertimeTransactionType, StringComparison.OrdinalIgnoreCase))
            .Sum(e => e.Amount);
        if (summary == null || overtimeAmount <= 0)
        {
            return null;
        }

        var overtimeHours = summary.WeekdayHours + summary.HolidayHours + summary.SaturdayHours + summary.SundayHours;
        return new PayrollPayslipOvertimeDto
        {
            WorkingHours = summary.NormalHours,
            OvertimeHours = overtimeHours,
            HolidayHours = summary.HolidayHours,
            SaturdayHours = summary.SaturdayHours,
            SundayHours = summary.SundayHours,
            DaySixAndSevenHours = summary.SaturdayHours + summary.SundayHours,
            TotalHours = summary.NormalHours + overtimeHours
        };
    }

    private static string BuildPayslipEmailHtml(PayrollPayslipDto payslip, string periodLabel, string? message)
    {
        var totalEarnings = payslip.Earnings.Sum(e => e.Amount);
        var totalDeductions = payslip.Deductions.Sum(e => e.Amount);
        var builder = new StringBuilder();

        builder.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><style>");
        builder.AppendLine("body{font-family:Arial,sans-serif;color:#111;line-height:1.4;margin:0;padding:24px;background:#f7f7f7}");
        builder.AppendLine(".payslip{max-width:760px;margin:0 auto;background:#fff;border:1px solid #111;padding:24px}");
        builder.AppendLine("h1,h2,h3{margin:0;text-align:center}.muted{color:#555}.grid{display:grid;grid-template-columns:1fr 1fr;gap:12px 32px;margin:18px 0}");
        builder.AppendLine("table{width:100%;border-collapse:collapse;margin-top:12px}th,td{border-bottom:1px solid #ddd;padding:6px;text-align:left}td.amount,th.amount{text-align:right}");
        builder.AppendLine(".summary{margin-top:18px;border:1px solid #111;padding:12px}.message{margin:16px 0;padding:12px;background:#f3f4f6}");
        builder.AppendLine("</style></head><body><div class=\"payslip\">");

        if (!string.IsNullOrWhiteSpace(payslip.CompanyName))
        {
            builder.Append("<h2>").Append(Html(payslip.CompanyName)).AppendLine("</h2>");
        }

        if (!string.IsNullOrWhiteSpace(payslip.CompanyAddress))
        {
            builder.Append("<div style=\"text-align:center\">").Append(Html(payslip.CompanyAddress)).AppendLine("</div>");
        }

        if (!string.IsNullOrWhiteSpace(payslip.CompanyPhone))
        {
            builder.Append("<div style=\"text-align:center\">").Append(Html(payslip.CompanyPhone)).AppendLine("</div>");
        }

        builder.Append("<h1 style=\"margin-top:16px\">").Append(payslip.IsSeparateBonusRun ? "Bonus Slip" : "Payslip").AppendLine("</h1>");
        builder.Append("<div class=\"muted\" style=\"text-align:center\">").Append(Html(periodLabel)).AppendLine("</div>");

        if (!string.IsNullOrWhiteSpace(message))
        {
            builder.Append("<div class=\"message\">").Append(Html(message)).AppendLine("</div>");
        }

        builder.AppendLine("<div class=\"grid\">");
        AppendEmailDetail(builder, "Employee No", payslip.EmployeeNumber);
        AppendEmailDetail(builder, "Employee", payslip.EmployeeName);
        AppendEmailDetail(builder, "Department", payslip.DepartmentName);
        AppendEmailDetail(builder, "Position", payslip.PositionTitle);
        AppendEmailDetail(builder, "Currency", payslip.CurrencyCode);
        AppendEmailDetail(builder, "Run", payslip.RunNumber);
        builder.AppendLine("</div>");

        AppendPayslipEmailLines(builder, "Earnings", payslip.Earnings);
        AppendPayslipEmailLines(builder, "Deductions", payslip.Deductions);

        builder.AppendLine("<div class=\"summary\"><table>");
        AppendAmountRow(builder, "Total Earnings", totalEarnings);
        AppendAmountRow(builder, "Total Deductions", totalDeductions);
        AppendAmountRow(builder, "Taxable Income", payslip.TaxableIncome);
        AppendAmountRow(builder, "Income Tax", payslip.IncomeTax);
        AppendAmountRow(builder, payslip.IsSeparateBonusRun ? "Net Bonus" : "Net Salary", payslip.NetIncome);
        builder.AppendLine("</table></div>");

        if (payslip.BankDetails.Count > 0)
        {
            builder.AppendLine("<h3 style=\"margin-top:18px\">Bank Details</h3><table><thead><tr><th>Bank</th><th>Account</th><th>Currency</th><th class=\"amount\">Amount</th></tr></thead><tbody>");
            foreach (var bank in payslip.BankDetails)
            {
                builder.Append("<tr><td>").Append(Html(bank.BankName))
                    .Append("</td><td>").Append(Html(bank.AccountNumber))
                    .Append("</td><td>").Append(Html(bank.CurrencyCode))
                    .Append("</td><td class=\"amount\">").Append(FormatEmailAmount(bank.Amount))
                    .AppendLine("</td></tr>");
            }
            builder.AppendLine("</tbody></table>");
        }

        builder.AppendLine("</div></body></html>");
        return builder.ToString();
    }

    private static void AppendPayslipEmailLines(StringBuilder builder, string title, IReadOnlyList<PayrollTransactionDto> lines)
    {
        builder.Append("<h3 style=\"margin-top:18px\">").Append(Html(title)).AppendLine("</h3>");
        builder.AppendLine("<table><thead><tr><th>Description</th><th class=\"amount\">Amount</th></tr></thead><tbody>");
        foreach (var line in lines.Where(e => e.Amount != 0))
        {
            builder.Append("<tr><td>").Append(Html(line.Description ?? line.ComponentCode ?? line.TransactionType))
                .Append("</td><td class=\"amount\">").Append(FormatEmailAmount(line.Amount))
                .AppendLine("</td></tr>");
        }

        if (!lines.Any(e => e.Amount != 0))
        {
            builder.AppendLine("<tr><td colspan=\"2\" class=\"muted\">No lines</td></tr>");
        }

        builder.AppendLine("</tbody></table>");
    }

    private static void AppendEmailDetail(StringBuilder builder, string label, string? value)
        => builder.Append("<div><strong>").Append(Html(label)).Append(":</strong> ").Append(Html(value)).AppendLine("</div>");

    private static void AppendAmountRow(StringBuilder builder, string label, decimal amount)
        => builder.Append("<tr><td><strong>").Append(Html(label)).Append("</strong></td><td class=\"amount\"><strong>")
            .Append(FormatEmailAmount(amount)).AppendLine("</strong></td></tr>");

    private static string BuildPayslipEmailFileName(PayrollPayslipDto payslip)
    {
        var employee = new string(payslip.EmployeeNumber.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-').ToArray());
        return $"{(payslip.IsSeparateBonusRun ? "Bonus-Slip" : "Payslip")}-{employee}-{payslip.PayPeriod}.html";
    }

    private static string FormatEmailAmount(decimal amount)
        => amount.ToString("N2", CultureInfo.InvariantCulture);

    private static string Html(string? value)
        => WebUtility.HtmlEncode(value ?? string.Empty);

    private static bool LooksLikePhoneLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains("TEL", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("PHONE", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<PayrollPayslipContributionDto> BuildContributionStatement(
        PayrollPayslipBuildContext context,
        PayrollEmployeeProfile? profile,
        IReadOnlyList<PayrollTransaction> transactions)
    {
        if (profile == null)
        {
            return [];
        }

        var rows = new Dictionary<string, PayrollPayslipContributionDto>(StringComparer.OrdinalIgnoreCase);
        var interestByKey = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        PayrollPayslipContributionDto Row(string key, string item)
        {
            if (!rows.TryGetValue(key, out var row))
            {
                row = new PayrollPayslipContributionDto
                {
                    Item = ToPayslipItemName(item),
                    IsProvidentFund = IsProvidentFundLabel(item)
                };
                rows[key] = row;
            }

            return row;
        }

        foreach (var group in transactions
                     .Where(e => e.TransactionType is "EmployeeContribution" or "EmployerContribution")
                     .GroupBy(e => NormalizePayslipContributionKey(e.ComponentCode, e.Description)))
        {
            var description = group.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Description))?.Description
                ?? group.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.ComponentCode))?.ComponentCode
                ?? "Contribution";
            var row = Row(group.Key, description);
            row.EmployeeContribution += group.Where(e => e.TransactionType == "EmployeeContribution").Sum(e => e.Amount);
            row.EmployerContribution += group.Where(e => e.TransactionType == "EmployerContribution").Sum(e => e.EmployerAmount ?? e.Amount);
        }

        var employeePension = transactions
            .Where(e => e.TransactionType is EmployeePensionTransactionType or PromotionEmployeeContributionArrearsTransactionType)
            .Sum(e => e.Amount);
        var employerPension = transactions
            .Where(e => e.TransactionType is EmployerPensionTransactionType or PromotionEmployerContributionArrearsTransactionType)
            .Sum(e => e.EmployerAmount ?? e.Amount);
        if (employeePension != 0 || employerPension != 0)
        {
            var row = Row("SOCIAL_SECURITY_FUND", "SOCIAL SECURITY FUND");
            row.IsProvidentFund = false;
            row.EmployeeContribution += employeePension;
            row.EmployerContribution += employerPension;
            row.FirstTier += employeePension;
            row.SecondTier += employerPension;
        }

        if (context.OpeningBalancesByProfileId.TryGetValue(profile.Id, out var openingBalances))
        {
            foreach (var balance in openingBalances)
            {
                var row = Row(
                    NormalizePayslipContributionKey(balance.ContributionCode, balance.ContributionName),
                    balance.ContributionName);
                row.OpeningBalance += balance.OpeningBalance;
            }
        }

        if (context.ContributionTransactionsByProfileId.TryGetValue(profile.Id, out var contributionTransactions))
        {
            foreach (var transaction in contributionTransactions)
            {
                var key = NormalizePayslipContributionKey(transaction.ContributionCode, transaction.ContributionName);
                var row = Row(key, transaction.ContributionName);
                if (transaction.TransactionType.Equals("Withdrawal", StringComparison.OrdinalIgnoreCase))
                {
                    row.TotalWithdrawal += transaction.Amount;
                }
                else if (transaction.TransactionType.Equals("Interest", StringComparison.OrdinalIgnoreCase))
                {
                    interestByKey[key] = interestByKey.GetValueOrDefault(key) + transaction.Amount;
                }
            }
        }

        foreach (var entry in rows)
        {
            entry.Value.TotalContribution = entry.Value.EmployeeContribution + entry.Value.EmployerContribution;
            entry.Value.GrandTotal = entry.Value.OpeningBalance + entry.Value.TotalContribution - entry.Value.TotalWithdrawal + interestByKey.GetValueOrDefault(entry.Key);
        }

        return rows.Values
            .Where(e => e.EmployeeContribution != 0 ||
                        e.EmployerContribution != 0 ||
                        e.OpeningBalance != 0 ||
                        e.TotalWithdrawal != 0 ||
                        e.GrandTotal != 0)
            .OrderByDescending(e => e.IsProvidentFund)
            .ThenBy(e => e.Item)
            .ToList();
    }

    private static IReadOnlyList<PayrollPayslipBankDetailDto> BuildBankDetails(
        PayrollPayslipBuildContext context,
        PayrollEmployeeProfile? profile,
        PayrollRunEmployee runEmployee,
        DateTime periodFrom,
        DateTime periodTo)
    {
        if (profile == null)
        {
            return [];
        }

        var payrollMethods = profile.PaymentMethods
            .Where(e => e.IsActive &&
                        e.PaymentType.Equals("Bank", StringComparison.OrdinalIgnoreCase) &&
                        IsEffective(e.StartDate, e.EndDate, periodFrom, periodTo))
            .OrderBy(e => e.SequenceNo)
            .ToList();

        if (payrollMethods.Count > 0)
        {
            return payrollMethods
                .Select((method, index) => new PayrollPayslipBankDetailDto
                {
                    BankName = BuildPaymentMethodBankName(method, context),
                    AccountNumber = method.AccountNumber,
                    CurrencyCode = string.IsNullOrWhiteSpace(method.CurrencyCode) ? runEmployee.CurrencyCode : method.CurrencyCode,
                    Amount = ResolvePaymentMethodAmount(method, runEmployee.NetIncome, payrollMethods.Count, index)
                })
                .ToList();
        }

        var bankDetails = profile.Employee.BankDetails
            .Where(e => e.IsActive)
            .OrderByDescending(e => e.IsPrimary)
            .ThenBy(e => e.BankName)
            .ToList();

        return bankDetails
            .Select((bank, index) => new PayrollPayslipBankDetailDto
            {
                BankName = BuildEmployeeBankName(bank),
                AccountNumber = bank.AccountNumber,
                CurrencyCode = runEmployee.CurrencyCode,
                Amount = bank.AllocationPercentage > 0
                    ? Math.Round(runEmployee.NetIncome * (bank.AllocationPercentage / 100m), 2)
                    : bankDetails.Count == 1 || index == 0
                        ? runEmployee.NetIncome
                        : 0m
            })
            .ToList();
    }

    private static IReadOnlyList<PayrollTransactionDto> BuildPayslipDeductions(IReadOnlyList<PayrollTransaction> transactions, PayrollRunEmployee runEmployee)
    {
        var deductions = transactions
            .Where(e => IsPayslipDeduction(e) && !e.TransactionType.Equals(IncomeTaxTransactionType, StringComparison.OrdinalIgnoreCase))
            .Select(ToDto)
            .ToList();

        var incomeTax = Math.Round(runEmployee.IncomeTax, 2);
        if (incomeTax > 0)
        {
            deductions.Insert(0, new PayrollTransactionDto
            {
                Id = Guid.Empty,
                PayrollRunEmployeeId = runEmployee.Id,
                EmployeeId = runEmployee.EmployeeId,
                EmployeeNumber = runEmployee.EmployeeNumber,
                TransactionType = IncomeTaxTransactionType,
                Description = "Income Tax - Total",
                Amount = incomeTax,
                Taxable = false,
                EmployerTaxable = false,
                SeparateTax = false,
                CurrencyCode = runEmployee.CurrencyCode
            });
        }

        return deductions;
    }

    private static IReadOnlyDictionary<string, string> BuildEmployeeBankNameLookup(IEnumerable<EmployeeBank> banks)
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var bank in banks)
        {
            AddLookupValue(lookup, bank.Id.ToString(), bank.Name);
            AddLookupValue(lookup, bank.Code, bank.Name);
        }

        return lookup;
    }

    private static IReadOnlyDictionary<string, string> BuildEmployeeBankBranchNameLookup(IEnumerable<EmployeeBankBranch> branches)
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var branch in branches)
        {
            AddLookupValue(lookup, branch.Id.ToString(), branch.Name);
            AddLookupValue(lookup, branch.Code, branch.Name);

            var bankCode = TrimOrNull(branch.Bank?.Code);
            var branchCode = TrimOrNull(branch.Code);
            if (bankCode != null && branchCode != null)
            {
                AddLookupValue(lookup, $"{bankCode}|{branchCode}", branch.Name);
            }
        }

        return lookup;
    }

    private static IReadOnlyDictionary<string, string> BuildPayrollBankNameLookup(IEnumerable<PayrollCodeValue> codeValues)
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var codeValue in codeValues.Where(IsPayrollBankCodeValue))
        {
            AddLookupValue(lookup, codeValue.ActualCode, codeValue.Description);
        }

        return lookup;
    }

    private static IReadOnlyDictionary<string, string> BuildPayrollBankBranchNameLookup(IEnumerable<PayrollBankBranch> branches)
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var branch in branches)
        {
            AddLookupValue(lookup, branch.BranchCode, branch.BranchDescription);

            var bankCode = TrimOrNull(branch.BankCode);
            var branchCode = TrimOrNull(branch.BranchCode);
            if (bankCode != null && branchCode != null)
            {
                AddLookupValue(lookup, $"{bankCode}|{branchCode}", branch.BranchDescription);
            }

            var branchSetupCode = TrimOrNull(branch.BranchSetupCode);
            if (branchSetupCode != null && branchCode != null)
            {
                AddLookupValue(lookup, $"{branchSetupCode}|{branchCode}", branch.BranchDescription);
            }
        }

        return lookup;
    }

    private static bool IsPayrollBankCodeValue(PayrollCodeValue codeValue)
    {
        var codeType = codeValue.CodeType.Trim();
        var description = codeValue.PayrollCodeType?.Description ?? string.Empty;
        return codeType.Equals("BANK", StringComparison.OrdinalIgnoreCase) ||
               codeType.Contains("BANK", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("BANK", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddLookupValue(IDictionary<string, string> lookup, string? key, string? value)
    {
        var normalizedKey = TrimOrNull(key);
        var normalizedValue = TrimOrNull(value);
        if (normalizedKey == null || normalizedValue == null || lookup.ContainsKey(normalizedKey))
        {
            return;
        }

        lookup[normalizedKey] = normalizedValue;
    }

    private static string NormalizePayslipContributionKey(string? code, string? name)
        => (string.IsNullOrWhiteSpace(code) ? name : code)?.Trim().ToUpperInvariant() ?? "CONTRIBUTION";

    private static bool IsProvidentFundLabel(string item)
        => item.Contains("PROVIDENT", StringComparison.OrdinalIgnoreCase) ||
           (!item.Contains("SOCIAL", StringComparison.OrdinalIgnoreCase) &&
            !item.Contains("SSF", StringComparison.OrdinalIgnoreCase) &&
            !item.Contains("PENSION", StringComparison.OrdinalIgnoreCase));

    private static string ToPayslipItemName(string? value)
        => string.IsNullOrWhiteSpace(value) ? "CONTRIBUTION" : value.Trim().ToUpperInvariant();

    private static string BuildPaymentMethodBankName(PayrollPaymentMethod method, PayrollPayslipBuildContext context)
    {
        var bankCode = TrimOrNull(method.BankCode);
        var branchCode = TrimOrNull(method.BankBranchCode);
        var bankName = ResolveLookupValue(bankCode, context.EmployeeBankNamesByReference, context.PayrollBankNamesByCode) ?? bankCode;
        var branchKey = bankCode != null && branchCode != null ? $"{bankCode}|{branchCode}" : null;
        var branchName =
            ResolveLookupValue(branchKey, context.EmployeeBankBranchNamesByReference, context.PayrollBankBranchNamesByReference) ??
            ResolveLookupValue(branchCode, context.EmployeeBankBranchNamesByReference, context.PayrollBankBranchNamesByReference) ??
            branchCode;
        var parts = new[] { bankName, branchName }
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e!.Trim())
            .ToList();

        return parts.Count == 0 ? "BANK" : string.Join(" - ", parts);
    }

    private static string? ResolveLookupValue(string? key, params IReadOnlyDictionary<string, string>[] lookups)
    {
        var normalizedKey = TrimOrNull(key);
        if (normalizedKey == null)
        {
            return null;
        }

        foreach (var lookup in lookups)
        {
            if (lookup.TryGetValue(normalizedKey, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string BuildEmployeeBankName(EmployeeBankDetail bank)
    {
        var bankName = bank.Bank?.Name ?? bank.BankName;
        var branchName = bank.Branch?.Name ?? bank.BranchName;
        var parts = new[] { bankName, branchName }
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e!.Trim())
            .ToList();

        return parts.Count == 0 ? "BANK" : string.Join(" - ", parts);
    }

    private static decimal ResolvePaymentMethodAmount(PayrollPaymentMethod method, decimal netIncome, int methodCount, int index)
    {
        if (method.Amount.HasValue && method.Amount.Value > 0)
        {
            return method.Amount.Value;
        }

        if (method.PaymentPercent.HasValue && method.PaymentPercent.Value > 0)
        {
            return Math.Round(netIncome * (method.PaymentPercent.Value / 100m), 2);
        }

        return methodCount == 1 || index == 0 ? netIncome : 0m;
    }

    private static bool IsPayslipEarning(PayrollTransaction transaction)
        => transaction.TransactionType is BasicSalaryTransactionType
            or "Allowance"
            or "Benefit"
            or BonusTransactionType
            or OvertimeTransactionType
            or PromotionBasicArrearsTransactionType
            or PromotionAllowanceArrearsTransactionType;

    private static bool IsPayslipDeduction(PayrollTransaction transaction)
        => transaction.TransactionType is "Deduction"
            or "EmployeeContribution"
            or EmployeePensionTransactionType
            or IncomeTaxTransactionType
            or LoanRepaymentTransactionType
            or LoanInterestTransactionType
            or SalaryAdvanceTransactionType
            or AbsenceTransactionType
            or PromotionEmployeeContributionArrearsTransactionType
            or PromotionContributionArrearsTransactionType;

    private async Task<PayrollRun> GetMutableRunAsync(Guid tenantId, Guid runId, CancellationToken cancellationToken)
        => await _context.PayrollRuns.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == runId, cancellationToken)
           ?? throw new KeyNotFoundException("Payroll run not found.");

    private async Task<PayrollRunDto> RequireRunDtoAsync(Guid tenantId, Guid runId, CancellationToken cancellationToken)
    {
        var run = await PayrollRunQuery(tenantId).FirstOrDefaultAsync(e => e.Id == runId, cancellationToken)
            ?? throw new KeyNotFoundException("Payroll run not found.");
        return ToDto(run, includeChildren: true);
    }

    private async Task ClearRunDetailsAsync(Guid tenantId, Guid runId, CancellationToken cancellationToken)
    {
        await _context.PayrollPayslipSnapshots
            .Where(e => e.TenantId == tenantId && e.PayrollRunId == runId)
            .ExecuteDeleteAsync(cancellationToken);
        await _context.PayrollReportSnapshots
            .Where(e => e.TenantId == tenantId && e.PayrollRunId == runId)
            .ExecuteDeleteAsync(cancellationToken);
        await _context.PayrollJournalLines
            .Where(e => e.TenantId == tenantId && e.PayrollRunId == runId)
            .ExecuteDeleteAsync(cancellationToken);
        await _context.PayrollTransactions
            .Where(e => e.TenantId == tenantId && e.PayrollRunId == runId)
            .ExecuteDeleteAsync(cancellationToken);
        await _context.PayrollRunEmployees
            .Where(e => e.TenantId == tenantId && e.PayrollRunId == runId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task ApplyPostedLoanRepaymentsAsync(
        Guid tenantId,
        PayrollRun run,
        DateTime postedAt,
        CancellationToken cancellationToken)
    {
        var loanTransactions = run.Transactions
            .Where(e => e.TransactionType is LoanRepaymentTransactionType or LoanInterestTransactionType)
            .ToList();
        if (loanTransactions.Count == 0)
        {
            return;
        }

        var employeeIds = loanTransactions.Select(e => e.EmployeeId).Distinct().ToList();
        var loans = await _context.PayrollLoans
            .Include(e => e.EmployeeProfile)
            .Include(e => e.Schedules)
            .Where(e => e.TenantId == tenantId && employeeIds.Contains(e.EmployeeProfile.EmployeeId))
            .ToListAsync(cancellationToken);
        var loansByKey = loans
            .Where(e => !string.IsNullOrWhiteSpace(e.FacilityNumber))
            .GroupBy(e => LoanPostingKey(e.EmployeeProfile.EmployeeId, e.FacilityNumber), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => e.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var group in loanTransactions
                     .Select(e => new { Transaction = e, FacilityNumber = ExtractLoanFacilityNumber(e.Description) })
                     .Where(e => !string.IsNullOrWhiteSpace(e.FacilityNumber))
                     .GroupBy(e => new
                     {
                         e.Transaction.EmployeeId,
                         FacilityNumber = e.FacilityNumber!
                     }))
        {
            if (!loansByKey.TryGetValue(LoanPostingKey(group.Key.EmployeeId, group.Key.FacilityNumber), out var loan))
            {
                continue;
            }

            var principalAmount = group
                .Where(e => e.Transaction.TransactionType == LoanRepaymentTransactionType)
                .Sum(e => e.Transaction.Amount);
            var interestAmount = group
                .Where(e => e.Transaction.TransactionType == LoanInterestTransactionType)
                .Sum(e => e.Transaction.Amount);
            ApplyAmountsToLoanSchedules(loan, principalAmount, interestAmount, run.PayPeriodTo, postedAt);
        }
    }

    private async Task<IReadOnlyList<PayrollJournalLine>> BuildJournalLinesAsync(
        Guid tenantId,
        Guid runId,
        IReadOnlyList<PayrollTransaction> transactions,
        CancellationToken cancellationToken)
    {
        var mappings = await _context.PayrollJournalMappings
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.IsActive)
            .ToListAsync(cancellationToken);

        var groups = transactions
            .Where(e => e.TransactionType != TaxReliefTransactionType)
            .GroupBy(e => new { e.TransactionType, e.ComponentCode })
            .OrderBy(e => e.Key.TransactionType)
            .ThenBy(e => e.Key.ComponentCode)
            .ToList();

        var lines = new List<PayrollJournalLine>();
        var sequence = 1;
        foreach (var group in groups)
        {
            var mapping = mappings.FirstOrDefault(e =>
                    e.TransactionType.Equals(group.Key.TransactionType, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(e.ComponentCode) &&
                    e.ComponentCode.Equals(group.Key.ComponentCode, StringComparison.OrdinalIgnoreCase))
                ?? mappings.FirstOrDefault(e =>
                    e.TransactionType.Equals(group.Key.TransactionType, StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrWhiteSpace(e.ComponentCode));

            var amount = group.Sum(e => e.EmployerAmount ?? e.Amount);
            if (amount == 0)
            {
                continue;
            }

            lines.Add(new PayrollJournalLine
            {
                TenantId = tenantId,
                PayrollRunId = runId,
                SequenceNo = sequence++,
                TransactionType = group.Key.TransactionType,
                DebitCredit = mapping?.DebitCredit ?? InferDebitCredit(group.Key.TransactionType),
                AccountCode = mapping?.AccountCode ?? "UNMAPPED",
                Description = mapping?.Description ?? $"{group.Key.TransactionType} payroll journal",
                Amount = Math.Abs(amount),
                Posted = false
            });
        }

        return lines;
    }

    private static IEnumerable<ResolvedPayrollComponent> ResolveComponents(
        IReadOnlyList<PayrollComponent> defaults,
        IReadOnlyDictionary<string, IReadOnlyList<PayrollComponentRule>> rulesByKey,
        PayrollEmployeeProfile profile,
        IEnumerable<PayrollEmployeeComponent> overrides,
        DateTime periodFrom,
        DateTime periodTo)
    {
        var resolved = new Dictionary<Guid, ResolvedPayrollComponent>();

        foreach (var component in defaults.Where(e => e.IsActive))
        {
            var category = NormalizeMatchToken(component.Category);
            if (string.IsNullOrWhiteSpace(category) || category == "ALL")
            {
                resolved[component.Id] = new ResolvedPayrollComponent(component, null, null);
                continue;
            }

            var rule = FindMatchingComponentRule(component, rulesByKey, profile);
            if (rule?.Applicable == true)
            {
                resolved[component.Id] = new ResolvedPayrollComponent(component, null, rule);
            }
        }

        foreach (var employeeComponent in overrides.Where(e => IsEffective(e.EffectiveFrom, e.EffectiveTo, periodFrom, periodTo)))
        {
            if (!employeeComponent.Applicable)
            {
                resolved.Remove(employeeComponent.PayrollComponentId);
                continue;
            }

            resolved[employeeComponent.PayrollComponentId] = new ResolvedPayrollComponent(employeeComponent.PayrollComponent, employeeComponent, null);
        }

        return resolved.Values.Where(e => e.Component.IsActive);
    }

    private static decimal CalculateComponentAmount(ResolvedPayrollComponent componentLine, decimal basicSalary)
    {
        var component = componentLine.Component;
        var employeeOverride = componentLine.Override;
        var rule = componentLine.Rule;
        var calculationType = employeeOverride?.CalculationTypeOverride ?? rule?.CalculationType ?? component.CalculationType;
        var amount = employeeOverride?.AmountOverride ?? rule?.Amount ?? component.Amount;
        var rate = employeeOverride?.RateOverride ?? rule?.Amount ?? component.Rate;

        return calculationType == PayrollCalculationType.PercentageOfBasic
            ? Math.Round(basicSalary * (rate == 0 ? amount : rate) / 100m, 2)
            : Math.Round(amount, 2);
    }

    private static bool IsComponentTaxable(ResolvedPayrollComponent componentLine)
    {
        if (componentLine.Override?.TaxableOverride is { } overrideValue)
        {
            return overrideValue;
        }

        if (componentLine.Rule is { } rule &&
            componentLine.Component.ComponentType is PayrollComponentType.Deduction or PayrollComponentType.EmployeeContribution)
        {
            return rule.AfterTax;
        }

        return componentLine.Component.Taxable;
    }

    private static bool IsComponentEmployerTaxable(ResolvedPayrollComponent componentLine)
        => componentLine.Override?.EmployerTaxableOverride ?? componentLine.Rule?.EmployerTaxable ?? componentLine.Component.EmployerTaxable;

    private static decimal? GetComponentEmployerAmount(ResolvedPayrollComponent componentLine)
        => componentLine.Override?.EmployerAmountOverride ?? componentLine.Rule?.EmployerAmount ?? componentLine.Component.EmployerAmount;

    private static decimal? GetComponentTaxFreeCeiling(ResolvedPayrollComponent componentLine)
        => componentLine.Override?.TaxFreeCeilingOverride ?? componentLine.Rule?.TaxFreeCeiling ?? componentLine.Component.TaxFreeCeiling;

    private static bool IsComponentSeparateTax(ResolvedPayrollComponent componentLine)
        => componentLine.Component.SeparateTax;

    private static decimal GetComponentSeparateTaxPercent(ResolvedPayrollComponent componentLine)
        => Math.Max(0, componentLine.Component.SeparateTaxPercent ?? 0m);

    private static bool IsComponentGrossUp(ResolvedPayrollComponent componentLine)
        => componentLine.Override?.GrossUpOverride ?? componentLine.Component.GrossUp;

    private static decimal ApplyBenefitCap(ResolvedPayrollComponent componentLine, decimal amount)
    {
        var cap = componentLine.Component.ComponentType == PayrollComponentType.Benefit
            ? componentLine.Component.MaxBenefitToTax.GetValueOrDefault()
            : 0m;

        return cap > 0m && amount > cap
            ? Math.Round(cap, 2)
            : amount;
    }

    private static decimal ApplyComponentProration(
        ResolvedPayrollComponent componentLine,
        PayrollEmployeeProfile profile,
        decimal amount,
        DateTime periodFrom,
        DateTime periodTo,
        PayrollParameterSet? parameters)
    {
        if (!componentLine.Component.Prorate || amount <= 0m)
        {
            return amount;
        }

        var factor = CalculatePayrollPeriodProrationFactor(profile, periodFrom, periodTo, parameters);
        return factor >= 1m ? amount : Math.Round(amount * factor, 2);
    }

    private static decimal ApplyComponentGrossUp(
        ResolvedPayrollComponent componentLine,
        PayrollEmployeeProfile profile,
        decimal amount,
        IReadOnlyList<PayrollTaxBand> taxBands,
        PayrollParameterSet? parameters)
    {
        if (amount <= 0m || !profile.PayTax || !IsComponentGrossUp(componentLine))
        {
            return amount;
        }

        var rate = IsComponentSeparateTax(componentLine)
            ? GetComponentSeparateTaxPercent(componentLine)
            : ResolveGrossUpRate(taxBands, parameters);

        return GrossUpAmount(amount, rate);
    }

    private static decimal ConvertComponentAmountToRunCurrency(
        ResolvedPayrollComponent componentLine,
        decimal amount,
        string runCurrencyCode,
        PayrollParameterSet? parameters)
    {
        if (amount == 0m)
        {
            return 0m;
        }

        var sourceCurrency = NormalizeCurrency(componentLine.Override?.CurrencyCodeOverride ?? componentLine.Component.CurrencyCode);
        var targetCurrency = NormalizeCurrency(runCurrencyCode);
        if (sourceCurrency == targetCurrency)
        {
            return Math.Round(amount, 2);
        }

        var exchangeRate = parameters?.ExchangeRate ?? 1m;
        if (exchangeRate <= 0m)
        {
            exchangeRate = 1m;
        }

        return Math.Round(amount * exchangeRate, 2);
    }

    private static bool IsEmployeeGrossUp(PayrollEmployeeProfile profile)
        => profile.GrossUp || profile.Employee.GrossUp;

    private static decimal ResolveGrossUpRate(IReadOnlyList<PayrollTaxBand> taxBands, PayrollParameterSet? parameters)
    {
        var parameterRate = parameters?.TaxRate.GetValueOrDefault() ?? 0m;
        if (parameterRate > 0m)
        {
            return parameterRate;
        }

        return taxBands.Count == 0 ? 0m : taxBands.Max(e => e.RatePercent);
    }

    private static decimal GrossUpAmount(decimal amount, decimal rate)
    {
        if (amount <= 0m || rate <= 0m)
        {
            return amount;
        }

        var safeRate = Math.Min(rate, 99.99m);
        return Math.Round(amount * 100m / (100m - safeRate), 2);
    }

    private static decimal CalculatePayrollPeriodProrationFactor(
        PayrollEmployeeProfile profile,
        DateTime periodFrom,
        DateTime periodTo,
        PayrollParameterSet? parameters)
    {
        if (periodTo.Date < periodFrom.Date)
        {
            return 1m;
        }

        var effectiveFrom = periodFrom.Date;
        if (profile.Employee.DateEmployed.HasValue)
        {
            var employedDate = profile.Employee.DateEmployed.Value.ToDateTime(TimeOnly.MinValue).Date;
            if (employedDate > effectiveFrom)
            {
                effectiveFrom = employedDate;
            }
        }

        var effectiveTo = periodTo.Date;
        if (profile.Employee.TerminationDate.HasValue && profile.Employee.TerminationDate.Value.Date < effectiveTo)
        {
            effectiveTo = profile.Employee.TerminationDate.Value.Date;
        }

        if (effectiveTo < effectiveFrom)
        {
            return 0m;
        }

        var payableDays = (effectiveTo - effectiveFrom).Days + 1;
        var periodDays = (periodTo.Date - periodFrom.Date).Days + 1;
        var denominator = parameters?.MonthDays > 0 ? parameters.MonthDays : periodDays;
        if (denominator <= 0)
        {
            return 1m;
        }

        return Math.Clamp(payableDays / (decimal)denominator, 0m, 1m);
    }

    private static PayrollComponentRule? FindMatchingComponentRule(
        PayrollComponent component,
        IReadOnlyDictionary<string, IReadOnlyList<PayrollComponentRule>> rulesByKey,
        PayrollEmployeeProfile profile)
    {
        if (!rulesByKey.TryGetValue(ComponentRuleKey(component.ComponentType, component.Code), out var rules))
        {
            return null;
        }

        return rules.FirstOrDefault(rule => ComponentRuleMatches(component, rule, profile));
    }

    private static bool ComponentRuleMatches(PayrollComponent component, PayrollComponentRule rule, PayrollEmployeeProfile profile)
    {
        var categoryType = NormalizeMatchToken(component.Category);
        if (string.IsNullOrWhiteSpace(categoryType) || categoryType == "ALL")
        {
            return true;
        }

        var target = NormalizeMatchToken(rule.Category);
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        return EmployeeCategoryTokens(profile, categoryType).Any(token => token == target);
    }

    private static IEnumerable<string> EmployeeCategoryTokens(PayrollEmployeeProfile profile, string categoryType)
    {
        var employee = profile.Employee;
        return categoryType switch
        {
            "DEP" or "DEPARTMENT" => MatchTokens(employee.DepartmentId, employee.Department?.Code, employee.Department?.Name),
            "SEC" or "SECTION" => MatchTokens(employee.SectionId, employee.Section?.Code, employee.Section?.Name),
            "POS" or "POSITION" => MatchTokens(employee.PositionId, employee.Position?.Code, employee.Position?.Title),
            "CAT" or "STAFFCATEGORY" or "STAFF CATEGORY" => MatchTokens(
                employee.Position?.StaffLevelId,
                employee.Position?.StaffLevel?.Code,
                employee.Position?.StaffLevel?.Name,
                employee.EmploymentType.ToString()),
            "JOB" => MatchTokens(employee.PositionId, employee.Position?.Code, employee.Position?.Title),
            "RAN" or "RANK" or "RANKS" => MatchTokens(employee.Position?.StaffLevel?.Rank.ToString(CultureInfo.InvariantCulture)),
            "GRA" or "GRADE" => MatchTokens(employee.Position?.SalaryGradeId, employee.Position?.SalaryGrade?.Code, employee.Position?.SalaryGrade?.Name),
            "LOC" or "LOCATION" => MatchTokens(employee.LocationId, employee.Location?.Code, employee.Location?.Name, employee.StationId, employee.Station?.Code, employee.Station?.Name),
            _ => MatchTokens(categoryType)
        };
    }

    private static IEnumerable<string> MatchTokens(params object?[] values)
        => values
            .Select(value => NormalizeMatchToken(value?.ToString()))
            .Where(value => !string.IsNullOrWhiteSpace(value));

    private static string ComponentRuleKey(PayrollComponentType componentType, string? componentCode)
        => $"{componentType}:{componentCode?.Trim().ToUpperInvariant() ?? string.Empty}";

    private static decimal CalculateBasicSalary(PayrollSalaryBasis? salaryBasis)
    {
        if (salaryBasis == null || !salaryBasis.IsActive)
        {
            return 0m;
        }

        if (salaryBasis.MonthlyBasicSalary > 0)
        {
            return Math.Round(salaryBasis.MonthlyBasicSalary, 2);
        }

        var annualBasicSalary = salaryBasis.AnnualBasicSalary.GetValueOrDefault();
        return annualBasicSalary > 0
            ? Math.Round(annualBasicSalary / 12m, 2)
            : 0m;
    }

    private static PayrollPensionCalculation CalculatePension(
        PayrollEmployeeProfile profile,
        decimal basicSalary,
        PayrollPensionScheme? pensionScheme,
        PayrollParameterSet? parameters)
    {
        if (!profile.SsfApplicable || basicSalary <= 0)
        {
            return new PayrollPensionCalculation(0, 0);
        }

        var employeeRate = pensionScheme?.EmployeeRatePercent ?? parameters?.EmployeeSsfRate ?? 0m;
        var employerRate = pensionScheme?.EmployerRatePercent ?? parameters?.EmployerSsfRate ?? 0m;
        var limit = pensionScheme?.ContributionLimit ?? parameters?.SsfLimit ?? 0m;
        var basis = limit > 0 ? Math.Min(basicSalary, limit) : basicSalary;

        return new PayrollPensionCalculation(
            Math.Round(basis * employeeRate / 100m, 2),
            Math.Round(basis * employerRate / 100m, 2));
    }

    private static decimal CalculateTaxRelief(IEnumerable<PayrollTaxRelief> reliefs, decimal basicSalary)
        => reliefs.Sum(e =>
            CalculateTaxReliefAmount(e.CalculationType, e.Amount, e.Factor, basicSalary));

    private static decimal CalculateEmployeeTaxRelief(IEnumerable<PayrollEmployeeTaxRelief> reliefs, decimal basicSalary)
        => reliefs.Sum(e =>
            CalculateTaxReliefAmount(e.CalculationType, e.Amount, e.Factor, basicSalary));

    private static decimal CalculateTaxReliefAmount(PayrollCalculationType calculationType, decimal amount, decimal factor, decimal basicSalary)
    {
        var reliefAmount = calculationType == PayrollCalculationType.PercentageOfBasic
            ? basicSalary * amount / 100m
            : amount;
        return Math.Round(reliefAmount * (factor == 0 ? 1 : factor), 2);
    }

    private static PayrollBonusCalculation CalculatePayrollBonuses(
        IReadOnlyList<PayrollBonusPolicy> policies,
        IReadOnlyDictionary<string, IReadOnlyList<PayrollBonusException>> exceptionsByBonusCode,
        PayrollEmployeeProfile profile,
        decimal basicSalary,
        decimal annualBasicSalary,
        decimal previousYearBonus,
        DateTime payPeriodTo,
        PayrollParameterSet? parameters)
    {
        if (policies.Count == 0 || basicSalary <= 0)
        {
            return PayrollBonusCalculation.Empty;
        }

        var lines = new List<PayrollBonusLine>();
        var runningPreviousBonus = previousYearBonus;
        var totalAmount = 0m;
        var nonTaxableAmount = 0m;
        var taxableBonusAmount = 0m;
        var separateTaxableAmount = 0m;
        var separateTaxAmount = 0m;

        foreach (var policy in policies)
        {
            exceptionsByBonusCode.TryGetValue(NormalizeMatchToken(policy.Code), out var policyExceptions);
            var matchingException = FindMatchingBonusException(policyExceptions, profile);
            var isException = matchingException != null;

            if (!isException && !BonusPolicyAppliesToProfile(policy, profile))
            {
                continue;
            }

            if (matchingException is { Applicable: false })
            {
                continue;
            }

            var amount = matchingException == null
                ? CalculateBonusAmount(policy.CalculationType, policy.Amount, basicSalary)
                : CalculateBonusAmount(matchingException.CalculationType, matchingException.Amount, basicSalary);
            amount = ApplyBonusProration(policy, profile, amount, payPeriodTo);
            if (amount <= 0)
            {
                continue;
            }

            var taxable = matchingException?.Taxable ?? policy.Taxable;
            var split = CalculateBonusTaxSplit(policy, amount, annualBasicSalary, runningPreviousBonus, parameters, taxable);
            var code = NormalizeCode(policy.Code);
            var name = string.IsNullOrWhiteSpace(policy.Name) ? code : policy.Name.Trim();

            if (split.NonTaxableAmount > 0)
            {
                lines.Add(new PayrollBonusLine(code, $"{name} (non-taxable)", split.NonTaxableAmount, false, false));
            }

            if (split.SeparateTaxableAmount > 0)
            {
                lines.Add(new PayrollBonusLine(code, $"{name} (separate bonus tax)", split.SeparateTaxableAmount, true, true));
            }

            if (split.TaxableBonusAmount > 0)
            {
                var description = policy.SeparateTax || parameters?.SeparateBonusTax == true
                    ? $"{name} (tax table excess)"
                    : isException
                        ? $"{name} (employee exception)"
                        : name;
                lines.Add(new PayrollBonusLine(code, description, split.TaxableBonusAmount, true, false));
            }

            totalAmount += amount;
            nonTaxableAmount += split.NonTaxableAmount;
            taxableBonusAmount += split.TaxableBonusAmount;
            separateTaxableAmount += split.SeparateTaxableAmount;
            separateTaxAmount += split.SeparateTaxAmount;
            runningPreviousBonus += amount;
        }

        return totalAmount <= 0
            ? PayrollBonusCalculation.Empty
            : new PayrollBonusCalculation(
                Math.Round(totalAmount, 2),
                Math.Round(nonTaxableAmount, 2),
                Math.Round(taxableBonusAmount, 2),
                Math.Round(separateTaxableAmount, 2),
                Math.Round(separateTaxAmount, 2),
                lines);
    }

    private static PayrollBonusTaxSplit CalculateBonusTaxSplit(
        PayrollBonusPolicy policy,
        decimal amount,
        decimal annualBasicSalary,
        decimal previousYearBonus,
        PayrollParameterSet? parameters,
        bool taxable)
    {
        if (!taxable)
        {
            return new PayrollBonusTaxSplit(amount, 0m, 0m, 0m);
        }

        var separateTax = policy.SeparateTax || parameters?.SeparateBonusTax == true;
        if (!separateTax)
        {
            var taxFreeAmount = Math.Min(amount, Math.Max(0, policy.TaxFreeCeiling ?? 0m));
            return new PayrollBonusTaxSplit(
                taxFreeAmount,
                Math.Max(0, amount - taxFreeAmount),
                0m,
                0m);
        }

        var cap = ResolveSeparateBonusTaxCap(policy, annualBasicSalary, parameters);
        var remainingCap = cap.HasValue
            ? Math.Max(0, cap.Value - previousYearBonus)
            : amount;
        var separateTaxableAmount = Math.Min(amount, remainingCap);
        var taxableBonusAmount = Math.Max(0, amount - separateTaxableAmount);
        var separateTaxAmount = Math.Round(separateTaxableAmount * ResolveBonusTaxRate(policy, parameters) / 100m, 2);

        return new PayrollBonusTaxSplit(0m, taxableBonusAmount, separateTaxableAmount, separateTaxAmount);
    }

    private static decimal CalculateBonusAmount(PayrollCalculationType calculationType, decimal amount, decimal basicSalary)
    {
        var calculatedAmount = calculationType == PayrollCalculationType.PercentageOfBasic
            ? basicSalary * amount / 100m
            : amount;

        return Math.Round(Math.Max(0, calculatedAmount), 2);
    }

    private static decimal ApplyBonusProration(PayrollBonusPolicy policy, PayrollEmployeeProfile profile, decimal amount, DateTime payPeriodTo)
    {
        if (!policy.Prorate || amount <= 0 || !profile.Employee.DateEmployed.HasValue)
        {
            return amount;
        }

        var employedDate = profile.Employee.DateEmployed.Value.ToDateTime(TimeOnly.MinValue);
        var completedMonths = CompletedMonthsBetween(employedDate, payPeriodTo.Date);
        if (completedMonths >= 12)
        {
            return amount;
        }

        return Math.Round(amount * Math.Max(0, completedMonths) / 12m, 2);
    }

    private static int CompletedMonthsBetween(DateTime from, DateTime to)
    {
        if (from.Date > to.Date)
        {
            return 0;
        }

        var months = ((to.Year - from.Year) * 12) + to.Month - from.Month;
        if (to.Day < from.Day)
        {
            months--;
        }

        return Math.Max(0, months);
    }

    private static decimal CalculateAnnualBasicForBonusTax(decimal originalBasicSalary, decimal historicalBasicSalary, DateTime payPeriodFrom)
    {
        var remainingMonthsIncludingCurrent = Math.Clamp(13 - payPeriodFrom.Month, 0, 12);
        return Math.Round(Math.Max(0, historicalBasicSalary) + Math.Max(0, originalBasicSalary) * remainingMonthsIncludingCurrent, 2);
    }

    private static decimal? ResolveSeparateBonusTaxCap(
        PayrollBonusPolicy policy,
        decimal annualBasicSalary,
        PayrollParameterSet? parameters)
    {
        var annualBasic = Math.Max(0, annualBasicSalary);
        var annualPercent = Math.Max(0, policy.AnnualSalaryPercentToTax ?? parameters?.MinimumBonusToTax ?? 0m);
        var annualCap = annualPercent > 0
            ? Math.Round(annualBasic * annualPercent / 100m, 2)
            : 0m;
        var configuredMinimum = Math.Max(0, policy.MinimumToTax ?? 0m);
        var fixedCap = configuredMinimum;

        if (annualCap <= 0 && fixedCap <= 0)
        {
            return null;
        }

        if (annualCap > 0 && fixedCap > 0)
        {
            return Math.Min(annualCap, fixedCap);
        }

        return annualCap > 0 ? annualCap : fixedCap;
    }

    private static decimal ResolveBonusTaxRate(PayrollBonusPolicy policy, PayrollParameterSet? parameters)
    {
        var policyRate = policy.TaxRate.GetValueOrDefault();
        if (policyRate > 0)
        {
            return policyRate;
        }

        return Math.Max(0, parameters?.BonusTaxRate ?? 0m);
    }

    private static bool BonusPolicyAppliesToProfile(PayrollBonusPolicy policy, PayrollEmployeeProfile profile)
    {
        if (IsAllBonusCategory(policy.Category))
        {
            return true;
        }

        var category = NormalizeMatchToken(policy.Category);
        return BuildBonusProfileTokens(profile).Contains(category);
    }

    private static PayrollBonusException? FindMatchingBonusException(
        IReadOnlyList<PayrollBonusException>? exceptions,
        PayrollEmployeeProfile profile)
    {
        if (exceptions == null || exceptions.Count == 0)
        {
            return null;
        }

        return exceptions.FirstOrDefault(e =>
            (e.EmployeeProfileId.HasValue && e.EmployeeProfileId.Value == profile.Id) ||
            e.EmployeeNumber.Equals(profile.EmployeeNumber, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(profile.LegacyEmployeeNumber) &&
             e.EmployeeNumber.Equals(profile.LegacyEmployeeNumber, StringComparison.OrdinalIgnoreCase)));
    }

    private static HashSet<string> BuildBonusProfileTokens(PayrollEmployeeProfile profile)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddBonusProfileToken(tokens, "ALL");
        AddBonusProfileToken(tokens, profile.EmployeeNumber);
        AddBonusProfileToken(tokens, profile.LegacyEmployeeNumber);
        AddBonusProfileToken(tokens, profile.Employee.EmploymentType.ToString());
        AddBonusProfileToken(tokens, profile.Employee.DepartmentId.ToString());
        AddBonusProfileToken(tokens, profile.Employee.Department?.Code);
        AddBonusProfileToken(tokens, profile.Employee.Department?.Name);
        AddBonusProfileToken(tokens, profile.Employee.Section?.Code);
        AddBonusProfileToken(tokens, profile.Employee.Section?.Name);
        AddBonusProfileToken(tokens, profile.Employee.PositionId.ToString());
        AddBonusProfileToken(tokens, profile.Employee.Position?.Code);
        AddBonusProfileToken(tokens, profile.Employee.Position?.Title);
        AddBonusProfileToken(tokens, profile.Employee.Position?.StaffLevelId?.ToString());
        AddBonusProfileToken(tokens, profile.Employee.Position?.StaffLevel?.Code);
        AddBonusProfileToken(tokens, profile.Employee.Position?.StaffLevel?.Name);
        AddBonusProfileToken(tokens, profile.Employee.Position?.SalaryGradeId?.ToString());
        AddBonusProfileToken(tokens, profile.Employee.Position?.SalaryGrade?.Code);
        AddBonusProfileToken(tokens, profile.Employee.Position?.SalaryGrade?.Name);
        AddBonusProfileToken(tokens, profile.Employee.LocationId?.ToString());
        AddBonusProfileToken(tokens, profile.Employee.Location?.Code);
        AddBonusProfileToken(tokens, profile.Employee.Location?.Name);
        AddBonusProfileToken(tokens, profile.Employee.StationId?.ToString());
        AddBonusProfileToken(tokens, profile.Employee.Station?.Code);
        AddBonusProfileToken(tokens, profile.Employee.Station?.Name);
        return tokens;
    }

    private static void AddBonusProfileToken(HashSet<string> tokens, string? value)
    {
        var token = NormalizeMatchToken(value);
        if (!string.IsNullOrWhiteSpace(token))
        {
            tokens.Add(token);
        }
    }

    private static bool IsAllBonusCategory(string? value)
    {
        var normalized = NormalizeMatchToken(value);
        return string.IsNullOrWhiteSpace(normalized) ||
               normalized is "ALL" or "ANY" or "*";
    }

    private static string NormalizeMatchToken(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    private static decimal CalculateIncomeTax(IReadOnlyList<PayrollTaxBand> bands, decimal taxableIncome)
    {
        if (taxableIncome <= 0 || bands.Count == 0)
        {
            return 0m;
        }

        var orderedBands = bands
            .OrderBy(e => e.SerialNo <= 0 ? int.MaxValue : e.SerialNo)
            .ThenBy(e => e.CumulativeSalary ?? e.UpperBound ?? e.LowerBound)
            .ToList();

        if (orderedBands.Any(e => e.TaxableIncome.HasValue || e.PerMonthAmount.HasValue || e.CumulativeTax.HasValue || e.CumulativeSalary.HasValue))
        {
            return CalculateCumulativeIncomeTax(orderedBands, taxableIncome);
        }

        if (bands.Any(e => e.FixedAmount > 0))
        {
            var matchingBand = bands.LastOrDefault(e =>
                    taxableIncome >= e.LowerBound &&
                    (!e.UpperBound.HasValue || taxableIncome <= e.UpperBound.Value))
                ?? bands.Last();
            var excess = Math.Max(0, taxableIncome - matchingBand.LowerBound);
            return Math.Round(matchingBand.FixedAmount + excess * matchingBand.RatePercent / 100m, 2);
        }

        var tax = 0m;
        foreach (var band in bands.OrderBy(e => e.LowerBound))
        {
            if (taxableIncome <= band.LowerBound)
            {
                continue;
            }

            var upper = band.UpperBound ?? taxableIncome;
            var taxablePortion = Math.Max(0, Math.Min(taxableIncome, upper) - band.LowerBound);
            tax += taxablePortion * band.RatePercent / 100m;

            if (band.UpperBound == null || taxableIncome <= band.UpperBound.Value)
            {
                break;
            }
        }

        return Math.Round(tax, 2);
    }

    private static decimal CalculateCumulativeIncomeTax(IReadOnlyList<PayrollTaxBand> bands, decimal taxableIncome)
    {
        var previousSalary = 0m;
        var previousTax = 0m;
        PayrollTaxBand? lastBand = null;

        foreach (var band in bands)
        {
            lastBand = band;
            var configuredBandAmount = band.TaxableIncome.GetValueOrDefault();
            var bandAmount = configuredBandAmount > 0
                ? configuredBandAmount
                : Math.Max(0, (band.UpperBound ?? band.CumulativeSalary ?? band.LowerBound) - previousSalary);
            var configuredCumulativeSalary = band.CumulativeSalary.GetValueOrDefault();
            var configuredCumulativeTax = band.CumulativeTax.GetValueOrDefault();
            var cumulativeSalary = configuredCumulativeSalary > 0 ? configuredCumulativeSalary : previousSalary + bandAmount;
            var cumulativeTax = configuredCumulativeTax > 0 ? configuredCumulativeTax : previousTax + (band.PerMonthAmount ?? band.FixedAmount);
            var isExceedingBand = IsExceedingTaxBand(band);

            if (taxableIncome <= cumulativeSalary || isExceedingBand)
            {
                var tax = previousTax + Math.Max(0, taxableIncome - previousSalary) * band.RatePercent / 100m;
                return Math.Round(tax, 2);
            }

            previousSalary = cumulativeSalary;
            previousTax = cumulativeTax;
        }

        if (lastBand == null)
        {
            return 0m;
        }

        return Math.Round(previousTax + Math.Max(0, taxableIncome - previousSalary) * lastBand.RatePercent / 100m, 2);
    }

    private static bool IsNormalIncomeTaxBand(PayrollTaxBand band)
    {
        var taxType = (band.TaxType ?? string.Empty).Trim();
        if (IsOvertimeIncomeTaxBand(band))
        {
            return false;
        }

        return taxType.Equals("T", StringComparison.OrdinalIgnoreCase) ||
               taxType.Equals("TAX", StringComparison.OrdinalIgnoreCase) ||
               taxType.Equals("PAYE", StringComparison.OrdinalIgnoreCase) ||
               taxType.Equals("INCOME TAX", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOvertimeIncomeTaxBand(PayrollTaxBand band)
    {
        var taxType = (band.TaxType ?? string.Empty).Trim();
        return taxType.Equals("O", StringComparison.OrdinalIgnoreCase) ||
               taxType.Equals("OT", StringComparison.OrdinalIgnoreCase) ||
               taxType.Equals("OVERTIME", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExceedingTaxBand(PayrollTaxBand band)
        => (band.Description ?? string.Empty).Contains("exceed", StringComparison.OrdinalIgnoreCase) ||
           (band.Description ?? string.Empty).Contains("above", StringComparison.OrdinalIgnoreCase);

    private static decimal FindNonWorkingOvertimeRate(
        IEnumerable<PayrollNonWorkingDay> nonWorkingDays,
        string dayPrefix,
        decimal fallbackRate)
    {
        var match = nonWorkingDays.FirstOrDefault(e =>
            e.Description.StartsWith(dayPrefix, StringComparison.OrdinalIgnoreCase) ||
            e.DayCode.Equals(dayPrefix[..1], StringComparison.OrdinalIgnoreCase));

        return match?.OvertimeRate ?? fallbackRate;
    }

    private static decimal CalculateAbsenceAmount(
        decimal originalBasicSalary,
        PayrollTimesheetSummary? timesheet,
        PayrollOvertimePolicy? overtimePolicy,
        PayrollParameterSet? parameters)
    {
        if (timesheet == null || timesheet.AbsentHours <= 0 || originalBasicSalary <= 0)
        {
            return 0m;
        }

        var normalHours = overtimePolicy?.NormalWorkingHours > 0 ? overtimePolicy.NormalWorkingHours : 8m;
        var monthDays = parameters?.MonthDays > 0 ? parameters.MonthDays : 30;
        var hourlyRate = normalHours > 0 && monthDays > 0
            ? originalBasicSalary / (normalHours * monthDays)
            : 0m;

        return hourlyRate <= 0
            ? 0m
            : Math.Round(timesheet.AbsentHours * hourlyRate, 2);
    }

    private static PayrollOvertimeCalculation CalculateOvertimeAmount(
        decimal originalBasicSalary,
        PayrollTimesheetSummary? timesheet,
        PayrollOvertimePolicy? overtimePolicy,
        PayrollParameterSet? parameters,
        decimal saturdayRate,
        decimal sundayRate)
    {
        if (timesheet == null || originalBasicSalary <= 0)
        {
            return PayrollOvertimeCalculation.Empty;
        }

        var weekdayHours = timesheet.WeekdayHours;
        var holidayHours = timesheet.HolidayHours;
        var saturdayHours = timesheet.SaturdayHours;
        var sundayHours = timesheet.SundayHours;

        var totalUnits =
            weekdayHours * (overtimePolicy?.WeekdayRate ?? 0m) +
            holidayHours * (overtimePolicy?.HolidayRate ?? 0m) +
            saturdayHours * saturdayRate +
            sundayHours * sundayRate;

        if (totalUnits <= 0)
        {
            return new PayrollOvertimeCalculation(0, overtimePolicy?.Taxable ?? false, false, 0, 0, 0);
        }

        var monthDays = parameters?.MonthDays > 0 ? parameters.MonthDays : 30;
        var hourlyRate = monthDays > 0
            ? originalBasicSalary / (8m * monthDays)
            : 0m;

        var amount = Math.Round(totalUnits * hourlyRate, 2);
        var taxable = overtimePolicy?.Taxable ?? false;
        return amount <= 0
            ? new PayrollOvertimeCalculation(0, taxable, false, 0, 0, 0)
            : new PayrollOvertimeCalculation(amount, taxable, false, taxable ? amount : 0m, 0, 0);
    }

    private static PayrollOvertimeCalculation CalculateOvertimeTaxTreatment(
        PayrollOvertimeCalculation overtime,
        PayrollOvertimePolicy? overtimePolicy,
        IReadOnlyList<PayrollTaxBand> overtimeTaxBands,
        decimal overtimeSeparateTaxBasis)
    {
        if (overtime.Amount <= 0 || !overtime.Taxable)
        {
            return overtime;
        }

        if (overtimePolicy?.SeparateOvertimeTax != true)
        {
            return overtime with
            {
                SeparateTax = false,
                NormalTaxableAmount = overtime.Amount,
                SeparateTaxableAmount = 0m,
                SeparateTaxAmount = 0m
            };
        }

        var minimumBasisForSeparateTax = overtimePolicy.MinimumBasicForSeparateTax ?? 0m;
        var minimumSeparateOvertimePercent = overtimePolicy.MinimumSeparateOvertimePercent ?? 0m;
        var qualifiesForSeparateTax =
            minimumBasisForSeparateTax > 0m &&
            overtimeSeparateTaxBasis < minimumBasisForSeparateTax &&
            minimumSeparateOvertimePercent > 0m &&
            overtimeSeparateTaxBasis * minimumSeparateOvertimePercent / 100m > overtime.Amount;

        if (!qualifiesForSeparateTax)
        {
            return overtime with
            {
                SeparateTax = false,
                NormalTaxableAmount = overtime.Amount,
                SeparateTaxableAmount = 0m,
                SeparateTaxAmount = 0m
            };
        }

        var separateTaxCap = overtimePolicy.MaxOvertimeSeparateTax ?? 0m;
        var separateTaxableAmount = separateTaxCap > 0m
            ? Math.Min(overtime.Amount, separateTaxCap)
            : 0m;
        var normalTaxableAmount = Math.Max(0, overtime.Amount - separateTaxableAmount);
        var separateTaxAmount = separateTaxableAmount > 0m
            ? CalculateSeparateOvertimeTax(overtimeTaxBands, separateTaxableAmount)
            : 0m;

        return overtime with
        {
            SeparateTax = separateTaxableAmount > 0m,
            NormalTaxableAmount = normalTaxableAmount,
            SeparateTaxableAmount = separateTaxableAmount,
            SeparateTaxAmount = separateTaxAmount
        };
    }

    private static decimal CalculateSeparateOvertimeTax(IReadOnlyList<PayrollTaxBand> bands, decimal overtimeTaxableIncome)
    {
        if (overtimeTaxableIncome <= 0m || bands.Count == 0)
        {
            return 0m;
        }

        var orderedBands = bands
            .OrderBy(e => e.SerialNo <= 0 ? int.MaxValue : e.SerialNo)
            .ThenBy(e => e.CumulativeSalary ?? e.UpperBound ?? e.LowerBound)
            .ToList();

        var minimumCumulativeSalary = orderedBands
            .Select(e => e.CumulativeSalary ?? e.UpperBound ?? e.LowerBound)
            .Where(e => e > 0m)
            .DefaultIfEmpty(0m)
            .Min();
        var firstBand = orderedBands.First();
        var lastBand = orderedBands.Last();
        var rate = overtimeTaxableIncome <= minimumCumulativeSalary
            ? firstBand.RatePercent
            : lastBand.RatePercent;

        return Math.Round(overtimeTaxableIncome * rate / 100m, 2);
    }

    private static PayrollContributionArrearsSplit BuildContributionArrearsSplit(IEnumerable<PayrollTransaction> transactions)
    {
        var transactionList = transactions.ToList();
        var employeeSsf = transactionList
            .Where(e => e.TransactionType == EmployeePensionTransactionType)
            .Sum(e => e.Amount);
        var employerSsf = transactionList
            .Where(e => e.TransactionType == EmployerPensionTransactionType)
            .Sum(e => e.EmployerAmount ?? e.Amount);
        var otherEmployeeContribution = transactionList
            .Where(e => e.TransactionType == PayrollComponentType.EmployeeContribution.ToString())
            .Sum(e => e.Amount);
        var otherEmployerContribution = transactionList
            .Where(e => e.TransactionType == PayrollComponentType.EmployeeContribution.ToString())
            .Sum(e => e.EmployerAmount ?? 0m) +
            transactionList
                .Where(e => e.TransactionType == PayrollComponentType.EmployerContribution.ToString())
                .Sum(e => e.EmployerAmount ?? e.Amount);

        return new PayrollContributionArrearsSplit(
            Math.Round(employeeSsf, 2),
            Math.Round(employerSsf, 2),
            Math.Round(otherEmployeeContribution, 2),
            Math.Round(otherEmployerContribution, 2));
    }

    private static decimal CalculatePromotionComparableIncomeTax(PayrollRunEmployee previous)
    {
        var incomeTaxTransactions = previous.Transactions
            .Where(e => e.TransactionType == IncomeTaxTransactionType)
            .ToList();
        if (incomeTaxTransactions.Count == 0)
        {
            return previous.IncomeTax;
        }

        return Math.Round(incomeTaxTransactions
            .Where(e => !string.Equals(e.ComponentCode, OvertimeTransactionType, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(e.ComponentCode, "SEP", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(e.ComponentCode, "BON", StringComparison.OrdinalIgnoreCase))
            .Sum(e => e.Amount), 2);
    }

    private static PayrollPromotionArrearsCalculation CalculatePromotionArrears(
        PayrollPromotionArrearsEntry promotionEntry,
        IReadOnlyList<PayrollRunEmployee> historicalRunEmployees,
        decimal currentBasicSalary,
        decimal currentAllowanceAmount,
        PayrollContributionArrearsSplit currentContributionSplit,
        decimal currentTaxableIncome,
        decimal currentIncomeTax)
    {
        var eligibleHistory = historicalRunEmployees
            .Where(e => e.PayrollRun.PayPeriod < promotionEntry.PayPeriod &&
                        e.PayrollRun.PayPeriodFrom.Date >= promotionEntry.EffectiveDate.Date)
            .ToList();

        if (eligibleHistory.Count == 0)
        {
            return PayrollPromotionArrearsCalculation.Empty;
        }

        var basicAmount = 0m;
        var allowanceAmount = 0m;
        var employeeContributionAmount = 0m;
        var employerContributionAmount = 0m;
        var otherContributionAmount = 0m;
        var otherEmployerContributionAmount = 0m;
        var taxableIncomeAmount = 0m;
        var taxAmount = 0m;
        var netAmount = 0m;

        foreach (var previous in eligibleHistory)
        {
            var previousAllowance = previous.TaxableAllowances + previous.NonTaxableAllowances;
            var previousContributionSplit = BuildContributionArrearsSplit(previous.Transactions);
            var diffBasic = currentBasicSalary - previous.BasicSalary;
            var diffAllowance = currentAllowanceAmount - previousAllowance;
            var diffEmployeeSsf = currentContributionSplit.EmployeeSsfAmount - previousContributionSplit.EmployeeSsfAmount;
            var diffEmployerSsf = currentContributionSplit.EmployerSsfAmount - previousContributionSplit.EmployerSsfAmount;
            var diffOtherContribution = currentContributionSplit.OtherEmployeeContributionAmount - previousContributionSplit.OtherEmployeeContributionAmount;
            var diffOtherEmployerContribution = currentContributionSplit.OtherEmployerContributionAmount - previousContributionSplit.OtherEmployerContributionAmount;
            var diffTaxableIncome = currentTaxableIncome - previous.TaxableIncome;
            var diffTax = currentIncomeTax - CalculatePromotionComparableIncomeTax(previous);
            var diffNet = diffBasic + diffAllowance - diffEmployeeSsf - diffOtherContribution - diffTax;

            if (diffNet <= 0)
            {
                continue;
            }

            basicAmount += Math.Max(0, diffBasic);
            allowanceAmount += Math.Max(0, diffAllowance);
            employeeContributionAmount += Math.Max(0, diffEmployeeSsf);
            employerContributionAmount += Math.Max(0, diffEmployerSsf);
            otherContributionAmount += Math.Max(0, diffOtherContribution);
            otherEmployerContributionAmount += Math.Max(0, diffOtherEmployerContribution);
            taxableIncomeAmount += Math.Max(0, diffTaxableIncome);
            taxAmount += Math.Max(0, diffTax);
            netAmount += diffNet;
        }

        return new PayrollPromotionArrearsCalculation(
            Math.Round(basicAmount, 2),
            Math.Round(allowanceAmount, 2),
            Math.Round(employeeContributionAmount, 2),
            Math.Round(employerContributionAmount, 2),
            Math.Round(otherContributionAmount, 2),
            Math.Round(otherEmployerContributionAmount, 2),
            Math.Round(taxableIncomeAmount, 2),
            Math.Round(taxAmount, 2),
            Math.Round(netAmount, 2));
    }

    private static void ApplyPromotionArrearsTransactions(
        Guid tenantId,
        PayrollRun run,
        PayrollRunEmployee runEmployee,
        PayrollEmployeeProfile profile,
        ICollection<PayrollTransaction> transactions,
        PayrollPromotionArrearsCalculation arrears)
    {
        if (arrears.BasicAmount > 0)
        {
            transactions.Add(CreateTransaction(tenantId, run, runEmployee, profile, PromotionBasicArrearsTransactionType, "Promotion basic arrears", arrears.BasicAmount, taxable: true, componentCode: PromotionBasicArrearsTransactionType));
        }

        if (arrears.AllowanceAmount > 0)
        {
            transactions.Add(CreateTransaction(tenantId, run, runEmployee, profile, PromotionAllowanceArrearsTransactionType, "Promotion allowance arrears", arrears.AllowanceAmount, taxable: true, componentCode: PromotionAllowanceArrearsTransactionType));
        }

        if (arrears.EmployeeContributionAmount > 0)
        {
            transactions.Add(CreateTransaction(tenantId, run, runEmployee, profile, PromotionEmployeeContributionArrearsTransactionType, "Promotion employee contribution arrears", arrears.EmployeeContributionAmount, taxable: false, componentCode: PromotionEmployeeContributionArrearsTransactionType));
        }

        if (arrears.OtherContributionAmount > 0)
        {
            var transaction = CreateTransaction(tenantId, run, runEmployee, profile, PromotionContributionArrearsTransactionType, "Promotion contribution arrears", arrears.OtherContributionAmount, taxable: true, componentCode: PromotionContributionArrearsTransactionType);
            transaction.EmployerAmount = arrears.OtherEmployerContributionAmount;
            transactions.Add(transaction);
        }

        if (arrears.EmployerContributionAmount > 0)
        {
            transactions.Add(CreateTransaction(tenantId, run, runEmployee, profile, PromotionEmployerContributionArrearsTransactionType, "Promotion employer contribution arrears", arrears.EmployerContributionAmount, taxable: false, employerAmount: arrears.EmployerContributionAmount, componentCode: PromotionEmployerContributionArrearsTransactionType));
        }

        if (arrears.TaxAmount > 0)
        {
            transactions.Add(CreateTransaction(tenantId, run, runEmployee, profile, IncomeTaxTransactionType, "Promotion arrears tax", arrears.TaxAmount, taxable: false, componentCode: "ARR"));
        }
    }

    private static decimal CalculateLoanRepayments(
        PayrollEmployeeProfile profile,
        DateTime periodFrom,
        DateTime periodTo,
        Guid tenantId,
        PayrollRun run,
        PayrollRunEmployee runEmployee,
        ICollection<PayrollTransaction> transactions)
    {
        var repayment = 0m;
        foreach (var loan in profile.Loans.Where(e =>
                     e.IsActive &&
                     e.Status == "Active" &&
                     e.OutstandingBalance > 0 &&
                     e.PaymentStartDate.Date <= periodTo.Date &&
                     (!e.PaymentEndDate.HasValue || e.PaymentEndDate.Value.Date >= periodFrom.Date) &&
                     (!e.SuspensionStartDate.HasValue || !e.SuspensionEndDate.HasValue ||
                      e.SuspensionEndDate.Value.Date < periodFrom.Date ||
                      e.SuspensionStartDate.Value.Date > periodTo.Date)))
        {
            var schedule = loan.Schedules
                .OrderBy(e => e.SequenceNo)
                .FirstOrDefault(e =>
                    e.RepaymentDate.Date <= periodTo.Date &&
                    (RemainingSchedulePrincipal(e) > 0 || RemainingScheduleInterest(e) > 0));
            if (schedule == null)
            {
                continue;
            }

            var principalAmount = Math.Min(RemainingSchedulePrincipal(schedule), loan.OutstandingBalance);
            var interestAmount = RemainingScheduleInterest(schedule);
            if (principalAmount <= 0 && interestAmount <= 0)
            {
                continue;
            }

            if (principalAmount > 0)
            {
                repayment += principalAmount;
                transactions.Add(CreateTransaction(tenantId, run, runEmployee, profile, LoanRepaymentTransactionType, $"Loan repayment {loan.FacilityNumber}", principalAmount, taxable: false));
            }

            if (interestAmount > 0)
            {
                repayment += interestAmount;
                transactions.Add(CreateTransaction(tenantId, run, runEmployee, profile, LoanInterestTransactionType, $"Loan interest {loan.FacilityNumber}", interestAmount, taxable: false));
            }
        }

        return Math.Round(repayment, 2);
    }

    private static void ApplyAmountsToLoanSchedules(
        PayrollLoan loan,
        decimal principalAmount,
        decimal interestAmount,
        DateTime periodTo,
        DateTime postedAt)
    {
        var appliedPrincipal = 0m;
        var principalRemaining = Math.Round(principalAmount, 2);
        var dueSchedules = loan.Schedules
            .Where(e => e.RepaymentDate.Date <= periodTo.Date || e.Posted)
            .OrderBy(e => e.SequenceNo)
            .ToList();

        foreach (var schedule in dueSchedules)
        {
            if (principalRemaining <= 0)
            {
                break;
            }

            var principalDue = RemainingSchedulePrincipal(schedule);
            if (principalDue <= 0)
            {
                continue;
            }

            var principalPaid = Math.Min(principalRemaining, principalDue);
            schedule.AmountPaid = Math.Round(schedule.AmountPaid + principalPaid, 2);
            schedule.ActualRepaymentDate = postedAt.Date;
            schedule.Posted = RemainingScheduleAmount(schedule) <= 0;
            schedule.UpdatedAt = DateTime.UtcNow;
            appliedPrincipal += principalPaid;
            principalRemaining = Math.Round(principalRemaining - principalPaid, 2);
        }

        var interestRemaining = Math.Round(interestAmount, 2);
        foreach (var schedule in dueSchedules)
        {
            if (interestRemaining <= 0)
            {
                break;
            }

            var interestDue = RemainingScheduleInterest(schedule);
            if (interestDue <= 0)
            {
                continue;
            }

            var interestPaid = Math.Min(interestRemaining, interestDue);
            schedule.InterestPaid = Math.Round(schedule.InterestPaid + interestPaid, 2);
            schedule.ActualRepaymentDate = postedAt.Date;
            schedule.Posted = RemainingScheduleAmount(schedule) <= 0;
            schedule.UpdatedAt = DateTime.UtcNow;
            interestRemaining = Math.Round(interestRemaining - interestPaid, 2);
        }

        if (appliedPrincipal > 0)
        {
            loan.OutstandingBalance = Math.Max(0, Math.Round(loan.OutstandingBalance - appliedPrincipal, 2));
        }

        if (loan.OutstandingBalance <= 0 || loan.Schedules.All(e => RemainingSchedulePrincipal(e) <= 0))
        {
            loan.OutstandingBalance = 0;
            loan.Status = "Closed";
            loan.IsActive = false;
            loan.PaymentEndDate = postedAt.Date;
        }

        loan.UpdatedAt = DateTime.UtcNow;
    }

    private static PayrollTransaction CreateTransaction(
        Guid tenantId,
        PayrollRun run,
        PayrollRunEmployee runEmployee,
        PayrollEmployeeProfile profile,
        string transactionType,
        string description,
        decimal amount,
        bool taxable,
        PayrollComponent? component = null,
        decimal? employerAmount = null,
        string? componentCode = null)
        => new()
        {
            TenantId = tenantId,
            PayrollRunId = run.Id,
            PayrollRunEmployeeId = runEmployee.Id,
            EmployeeId = profile.EmployeeId,
            EmployeeNumber = profile.EmployeeNumber,
            TransactionType = transactionType,
            PayrollComponentId = component?.Id,
            ComponentCode = component?.Code ?? componentCode,
            Description = description,
            Amount = Math.Round(amount, 2),
            EmployerAmount = employerAmount,
            Taxable = taxable,
            EmployerTaxable = component?.EmployerTaxable ?? false,
            SeparateTax = component?.SeparateTax ?? false,
            CurrencyCode = run.CurrencyCode
        };

    private static void UpsertSalaryBasis(Guid tenantId, PayrollEmployeeProfile profile, UpsertPayrollSalaryBasisDto dto)
    {
        var entity = profile.SalaryBasis ?? new PayrollSalaryBasis { TenantId = tenantId, EmployeeProfile = profile };
        entity.MonthlyBasicSalary = dto.MonthlyBasicSalary;
        entity.AnnualBasicSalary = dto.AnnualBasicSalary;
        entity.HourlyRate = dto.HourlyRate;
        entity.CurrencyCode = NormalizeCurrency(dto.CurrencyCode);
        entity.EffectiveFrom = dto.EffectiveFrom == default ? DateTime.UtcNow.Date : dto.EffectiveFrom.Date;
        entity.EffectiveTo = dto.EffectiveTo?.Date;
        entity.IsActive = dto.IsActive;
        profile.SalaryBasis = entity;
    }

    private void UpsertPaymentMethods(Guid tenantId, PayrollEmployeeProfile profile, IReadOnlyList<UpsertPayrollPaymentMethodDto> dto)
    {
        if (dto.Count == 0)
        {
            return;
        }

        var keepIds = dto.Where(e => e.Id != Guid.Empty).Select(e => e.Id).ToHashSet();
        foreach (var existing in profile.PaymentMethods.Where(e => !keepIds.Contains(e.Id)).ToList())
        {
            _context.PayrollPaymentMethods.Remove(existing);
        }

        foreach (var item in dto)
        {
            var method = profile.PaymentMethods.FirstOrDefault(e => e.Id == item.Id)
                ?? new PayrollPaymentMethod { TenantId = tenantId, EmployeeProfile = profile };
            method.PaymentType = string.IsNullOrWhiteSpace(item.PaymentType) ? "Bank" : item.PaymentType.Trim();
            method.PaymentPercent = item.PaymentPercent;
            method.Amount = item.Amount;
            method.BankCode = TrimOrNull(item.BankCode);
            method.BankBranchCode = TrimOrNull(item.BankBranchCode);
            method.AccountNumber = TrimOrNull(item.AccountNumber);
            method.CurrencyCode = NormalizeCurrency(item.CurrencyCode);
            method.SequenceNo = item.SequenceNo;
            method.StartDate = item.StartDate?.Date;
            method.EndDate = item.EndDate?.Date;
            method.IsActive = item.IsActive;

            if (method.Id == Guid.Empty || !profile.PaymentMethods.Any(e => e.Id == method.Id))
            {
                profile.PaymentMethods.Add(method);
            }
        }

        profile.DefaultPaymentMethod = profile.PaymentMethods
            .Where(e => e.IsActive)
            .OrderBy(e => e.SequenceNo)
            .FirstOrDefault();
    }

    private void UpsertEmployeeComponents(Guid tenantId, PayrollEmployeeProfile profile, IReadOnlyList<UpsertPayrollEmployeeComponentDto> dto)
    {
        if (dto.Count == 0)
        {
            return;
        }

        var keepIds = dto.Where(e => e.Id.HasValue && e.Id.Value != Guid.Empty).Select(e => e.Id!.Value).ToHashSet();
        foreach (var existing in profile.EmployeeComponents.Where(e => !keepIds.Contains(e.Id)).ToList())
        {
            _context.PayrollEmployeeComponents.Remove(existing);
        }

        foreach (var item in dto)
        {
            var component = profile.EmployeeComponents.FirstOrDefault(e => item.Id.HasValue && e.Id == item.Id.Value)
                ?? new PayrollEmployeeComponent { TenantId = tenantId, EmployeeProfile = profile };
            component.PayrollComponentId = item.PayrollComponentId;
            component.CalculationTypeOverride = item.CalculationTypeOverride;
            component.AmountOverride = item.AmountOverride;
            component.RateOverride = item.RateOverride;
            component.TaxableOverride = item.TaxableOverride;
            component.TaxFreeCeilingOverride = item.TaxFreeCeilingOverride;
            component.EmployerAmountOverride = item.EmployerAmountOverride;
            component.EmployerTaxableOverride = item.EmployerTaxableOverride;
            component.GrossUpOverride = item.GrossUpOverride;
            component.CurrencyCodeOverride = NormalizeOptionalCurrency(item.CurrencyCodeOverride);
            component.Applicable = item.Applicable;
            component.EffectiveFrom = item.EffectiveFrom?.Date;
            component.EffectiveTo = item.EffectiveTo?.Date;

            if (component.Id == Guid.Empty || !profile.EmployeeComponents.Any(e => e.Id == component.Id))
            {
                profile.EmployeeComponents.Add(component);
            }
        }
    }

    private async Task<string> GenerateRunNumberAsync(Guid tenantId, int payPeriod, CancellationToken cancellationToken)
    {
        var prefix = $"PAY-{payPeriod}-";
        var count = await _context.PayrollRuns
            .CountAsync(e => e.TenantId == tenantId && e.PayPeriod == payPeriod, cancellationToken);

        return $"{prefix}{count + 1:000}";
    }

    private async Task<string> GenerateLoanFacilityNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var existing = await _context.PayrollLoans
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .Select(e => e.FacilityNumber)
            .ToListAsync(cancellationToken);
        var existingSet = existing
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var next = 1L;
        foreach (var facilityNumber in existingSet)
        {
            if (long.TryParse(facilityNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= next)
            {
                next = parsed + 1;
            }
        }

        string candidate;
        do
        {
            candidate = next.ToString("D8", CultureInfo.InvariantCulture);
            next++;
        }
        while (existingSet.Contains(candidate));

        return candidate;
    }

    private async Task DeactivateOtherParameterSetsAsync(Guid tenantId, Guid activeId, CancellationToken cancellationToken)
    {
        await _context.PayrollParameterSets
            .Where(e => e.TenantId == tenantId && e.Id != activeId && e.IsActive)
            .ExecuteUpdateAsync(setters => setters.SetProperty(e => e.IsActive, false), cancellationToken);
    }

    private async Task AdvanceBonusPoliciesAfterCloseAsync(Guid tenantId, PayrollRun run, CancellationToken cancellationToken)
    {
        var bonusCodes = await _context.PayrollTransactions
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId &&
                        e.PayrollRunId == run.Id &&
                        e.TransactionType == BonusTransactionType &&
                        e.ComponentCode != null)
            .Select(e => e.ComponentCode!)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (bonusCodes.Count == 0)
        {
            return;
        }

        var policies = await _context.PayrollBonusPolicies
            .Where(e => e.TenantId == tenantId && bonusCodes.Contains(e.Code))
            .ToListAsync(cancellationToken);

        foreach (var policy in policies)
        {
            if (policy.PaySeparate != run.IsSeparateBonusRun || !IsBonusPolicyDueForRun(policy, run))
            {
                continue;
            }

            var lastPayPeriodDate = policy.NextPayPeriodDate?.Date
                ?? ParsePayPeriodMonth(policy.NextPayPeriod)
                ?? run.PayPeriodFrom.Date;
            var cycleMonths = ResolveBonusCycleMonths(policy.Cycle);
            var nextPayPeriodDate = cycleMonths > 0
                ? lastPayPeriodDate.AddMonths(cycleMonths)
                : lastPayPeriodDate;

            policy.LastPayPeriodDate = lastPayPeriodDate;
            policy.LastPayPeriod = FormatPayPeriodMonth(lastPayPeriodDate);
            policy.NextPayPeriodDate = nextPayPeriodDate;
            policy.NextPayPeriod = FormatPayPeriodMonth(nextPayPeriodDate);
        }
    }

    private static bool IsBonusPolicyDueForRun(PayrollBonusPolicy policy, PayrollRun run)
    {
        var runPayPeriodFromMonth = new DateTime(run.PayPeriodFrom.Year, run.PayPeriodFrom.Month, 1);
        var runPayPeriodToMonth = new DateTime(run.PayPeriodTo.Year, run.PayPeriodTo.Month, DateTime.DaysInMonth(run.PayPeriodTo.Year, run.PayPeriodTo.Month));
        var dateIsDue = policy.NextPayPeriodDate.HasValue &&
                        policy.NextPayPeriodDate.Value.Date >= runPayPeriodFromMonth &&
                        policy.NextPayPeriodDate.Value.Date <= runPayPeriodToMonth;
        var periodIsDue = policy.NextPayPeriod.HasValue &&
                          policy.NextPayPeriod.Value > 0 &&
                          (policy.NextPayPeriod.Value == run.PayPeriod ||
                           policy.NextPayPeriod.Value == FormatPayPeriodMonth(run.PayPeriodFrom) ||
                           policy.NextPayPeriod.Value == FormatPayPeriodMonth(run.PayPeriodTo));

        return dateIsDue || periodIsDue;
    }

    private static int ResolveBonusCycleMonths(string? cycle)
    {
        var normalized = NormalizeMatchToken(cycle);
        return normalized switch
        {
            "" or "R" or "RECURRING" or "MONTHLY" or "MONTH" => 1,
            "Q" or "QUARTERLY" or "QUARTER" => 3,
            "B" or "BIANNUAL" or "BIANNUALLY" or "BIANNUALY" or "SEMIANNUAL" or "SEMIANNUALLY" => 6,
            "A" or "ANNUAL" or "ANNUALLY" or "YEARLY" or "YEAR" => 12,
            "O" or "ONCE" or "ONEOFF" or "ONETIME" => 0,
            _ => 1
        };
    }

    private static DateTime? ParsePayPeriodMonth(int? payPeriod)
    {
        if (!payPeriod.HasValue || payPeriod.Value <= 0)
        {
            return null;
        }

        var text = payPeriod.Value.ToString(CultureInfo.InvariantCulture);
        if (text.Length != 6 ||
            !int.TryParse(text[..4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var year) ||
            !int.TryParse(text[4..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var month) ||
            month is < 1 or > 12)
        {
            return null;
        }

        return new DateTime(year, month, 1);
    }

    private static int FormatPayPeriodMonth(DateTime date)
        => (date.Year * 100) + date.Month;

    private async Task AdvanceActivePayrollPeriodAfterCloseAsync(Guid tenantId, PayrollRun run, CancellationToken cancellationToken)
    {
        var activeParameters = await _context.PayrollParameterSets
            .Where(e => e.TenantId == tenantId && e.IsActive)
            .OrderBy(e => e.Code)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeParameters is null)
        {
            return;
        }

        var periodFromMatches = !activeParameters.CurrentPeriodFrom.HasValue ||
                                activeParameters.CurrentPeriodFrom.Value.Date == run.PayPeriodFrom.Date;
        var periodToMatches = !activeParameters.CurrentPeriodTo.HasValue ||
                              activeParameters.CurrentPeriodTo.Value.Date == run.PayPeriodTo.Date;

        if (activeParameters.CurrentPayPeriod != run.PayPeriod || !periodFromMatches || !periodToMatches)
        {
            _logger.LogWarning(
                "Skipped payroll period advance after closing run {RunId}. Active period {ActivePeriod} ({ActiveFrom:d}-{ActiveTo:d}) does not match run period {RunPeriod} ({RunFrom:d}-{RunTo:d}).",
                run.Id,
                activeParameters.CurrentPayPeriod,
                activeParameters.CurrentPeriodFrom,
                activeParameters.CurrentPeriodTo,
                run.PayPeriod,
                run.PayPeriodFrom,
                run.PayPeriodTo);
            return;
        }

        await _context.PayrollSalaryAdvances
            .Where(e => e.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        var nextPeriod = GetNextMonthlyPayrollPeriod(activeParameters.CurrentPeriodTo ?? run.PayPeriodTo);
        activeParameters.CurrentPayPeriod += 1;
        activeParameters.CurrentPeriodFrom = nextPeriod.From;
        activeParameters.CurrentPeriodTo = nextPeriod.To;
    }

    private static (DateTime From, DateTime To) GetNextMonthlyPayrollPeriod(DateTime currentPeriodAnchor)
    {
        var nextFrom = new DateTime(currentPeriodAnchor.Year, currentPeriodAnchor.Month, 1).AddMonths(1);
        var nextTo = nextFrom.AddMonths(1).AddDays(-1);
        return (nextFrom, nextTo);
    }

    private static (DateTime? From, DateTime? To) NormalizePayrollParameterPeriod(DateTime? periodFrom, DateTime? periodTo, string? payMode)
    {
        if (string.Equals(payMode, "D", StringComparison.OrdinalIgnoreCase))
        {
            return (periodFrom?.Date, periodTo?.Date);
        }

        var anchor = (periodTo ?? periodFrom)?.Date;
        if (!anchor.HasValue)
        {
            return (periodFrom?.Date, periodTo?.Date);
        }

        var from = new DateTime(anchor.Value.Year, anchor.Value.Month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        return (from, to);
    }

    private static string FormatPayrollPeriodMonth(DateTime? from, DateTime? to)
    {
        var anchor = (to ?? from)?.Date;
        return anchor.HasValue
            ? anchor.Value.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
            : "current payroll period";
    }

    private static async Task<TEntity?> FindForUpsertAsync<TEntity>(DbSet<TEntity> set, Guid tenantId, Guid? id, CancellationToken cancellationToken)
        where TEntity : TenantEntity
        => !id.HasValue || id.Value == Guid.Empty
            ? null
            : await set.FirstOrDefaultAsync(e => e.TenantId == tenantId && e.Id == id.Value, cancellationToken);

    private void AddIfNew<TEntity>(DbSet<TEntity> set, TEntity entity)
        where TEntity : TenantEntity
    {
        if (_context.Entry(entity).State == EntityState.Detached)
        {
            set.Add(entity);
        }
    }

    private static bool IsEffective(DateTime? effectiveFrom, DateTime? effectiveTo, DateTime periodFrom, DateTime periodTo)
        => (!effectiveFrom.HasValue || effectiveFrom.Value.Date <= periodTo.Date) &&
           (!effectiveTo.HasValue || effectiveTo.Value.Date >= periodFrom.Date);

    private static string NormalizeCode(string? code)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Code is required.");
        }

        return normalized;
    }

    private static string NormalizeLegacyCode(string? code, string fieldName, int maxLength)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        if (normalized.Length > maxLength)
        {
            throw new InvalidOperationException($"{fieldName} cannot be longer than {maxLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeOptionalLegacyCode(string? code, int maxLength)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw new InvalidOperationException($"Legacy code cannot be longer than {maxLength} characters.");
        }

        return normalized;
    }

    private static string RequireText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return value.Trim();
    }

    private static string NormalizeCurrency(string? currency)
        => string.IsNullOrWhiteSpace(currency) ? "GHS" : currency.Trim().ToUpperInvariant();

    private static string? NormalizeOptionalCurrency(string? currency)
        => string.IsNullOrWhiteSpace(currency) ? null : currency.Trim().ToUpperInvariant();

    private static string NormalizeDebitCredit(string? debitCredit)
    {
        var value = debitCredit?.Trim().ToUpperInvariant();
        return value is "CR" ? "CR" : "DR";
    }

    private static string NormalizeBackpayOperation(string? operationType)
    {
        var normalized = operationType?.Trim().Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase);
        return string.Equals(normalized, "SalaryArrears", StringComparison.OrdinalIgnoreCase)
            ? "SalaryArrears"
            : "IncreaseSalary";
    }

    private static string NormalizeBackpayCategory(string? categoryType)
    {
        var value = string.IsNullOrWhiteSpace(categoryType) ? "All Staff" : categoryType.Trim();
        return value.ToUpperInvariant() switch
        {
            "ALL" or "ALLSTAFF" or "ALL STAFF" => "All Staff",
            "GRA" or "GRADE" => "Grade",
            "POS" or "POSITION" => "Position",
            "DEP" or "DEPARTMENT" => "Department",
            "CAT" or "STAFFCATEGORY" or "STAFF CATEGORY" => "Staff Category",
            _ => value
        };
    }

    private static string NormalizeLoanRepaymentMode(string? repaymentMode)
    {
        var value = repaymentMode?.Trim().Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("%", "Percent", StringComparison.OrdinalIgnoreCase);
        return value?.ToUpperInvariant() switch
        {
            "NOOFREPAYMENTS" or "NUMBEROFREPAYMENTS" or "REPAYMENTS" => "NoOfRepayments",
            "PERCENTOFBASIC" or "PERCENTBASIC" or "OFBASIC" => "PercentOfBasic",
            _ => "Amount"
        };
    }

    private static string NormalizeLoanInterestType(string? interestType)
    {
        var value = interestType?.Trim().Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase).ToUpperInvariant();
        return value switch
        {
            "S" or "SIMPLE" => "S",
            "F" or "FLAT" => "F",
            _ => "R"
        };
    }

    private static string NormalizeLoanStatus(string? status)
    {
        var value = string.IsNullOrWhiteSpace(status) ? "Active" : status.Trim();
        return value.ToUpperInvariant() switch
        {
            "CLOSED" or "PAID" or "INACTIVE" => "Closed",
            "SUSPENDED" or "ONHOLD" or "ON HOLD" => "Suspended",
            _ => "Active"
        };
    }

    private static string NormalizeContributionTransactionType(string? transactionType)
    {
        var value = transactionType?.Trim().Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase);
        return value?.ToUpperInvariant() switch
        {
            "WITHDRAWAL" => "Withdrawal",
            "INTEREST" => "Interest",
            _ => throw new InvalidOperationException("Transaction type must be Withdrawal or Interest.")
        };
    }

    private static decimal NormalizeNonNegative(decimal? value, string fieldName, int decimals)
    {
        var normalized = Math.Round(value.GetValueOrDefault(), decimals);
        if (normalized < 0)
        {
            throw new InvalidOperationException($"{fieldName} cannot be negative.");
        }

        return normalized;
    }

    private static int InclusiveMonthCount(DateTime startDate, DateTime endDate)
    {
        if (endDate.Date < startDate.Date)
        {
            return 1;
        }

        return Math.Max(1, ((endDate.Year - startDate.Year) * 12) + endDate.Month - startDate.Month + 1);
    }

    private static decimal CalculateLoanPrincipalRepaymentAmount(
        decimal amountGranted,
        decimal monthlyInput,
        string repaymentMode,
        int numberOfRepayments,
        decimal basicSalary)
    {
        if (repaymentMode == "PercentOfBasic")
        {
            return basicSalary <= 0 || monthlyInput <= 0 ? 0 : Math.Round(basicSalary * monthlyInput / 100m, 2);
        }

        if (repaymentMode == "NoOfRepayments" && numberOfRepayments > 0)
        {
            return Math.Round(amountGranted / numberOfRepayments, 2);
        }

        return Math.Round(monthlyInput, 2);
    }

    private static decimal CalculateLoanTotalInterest(
        decimal amountGranted,
        decimal repaymentAmount,
        int numberOfRepayments,
        decimal interestRate,
        string interestType,
        bool applyInterest)
    {
        if (!applyInterest || amountGranted <= 0 || interestRate <= 0 || numberOfRepayments <= 0)
        {
            return 0;
        }

        if (interestType == "S")
        {
            return Math.Round(amountGranted * interestRate * numberOfRepayments / 1200m, 2);
        }

        if (interestType == "F")
        {
            return Math.Round(amountGranted * interestRate / 100m, 2);
        }

        var totalInterest = 0m;
        var remainingPrincipal = amountGranted;
        foreach (var principal in BuildLoanPrincipalSchedule(amountGranted, repaymentAmount, numberOfRepayments))
        {
            totalInterest += Math.Round(remainingPrincipal * interestRate / 1200m, 2);
            remainingPrincipal = Math.Max(0, Math.Round(remainingPrincipal - principal, 2));
        }

        return Math.Round(totalInterest, 2);
    }

    private static IReadOnlyList<decimal> BuildLoanPrincipalSchedule(decimal amountGranted, decimal repaymentAmount, int numberOfRepayments)
    {
        if (amountGranted <= 0 || repaymentAmount <= 0 || numberOfRepayments <= 0)
        {
            return [];
        }

        var schedule = new List<decimal>();
        var remainingPrincipal = Math.Round(amountGranted, 2);
        for (var index = 1; index <= numberOfRepayments; index++)
        {
            var isLast = index == numberOfRepayments;
            var principal = isLast ? remainingPrincipal : Math.Min(repaymentAmount, remainingPrincipal);
            principal = Math.Round(principal, 2);
            schedule.Add(principal);
            remainingPrincipal = Math.Max(0, Math.Round(remainingPrincipal - principal, 2));
            if (remainingPrincipal <= 0)
            {
                break;
            }
        }

        return schedule;
    }

    private static void RebuildLoanSchedules(Guid tenantId, PayrollLoan loan, string interestType)
    {
        loan.Schedules.Clear();

        if (loan.NumberOfRepayments <= 0 || loan.MonthlyRepaymentAmount <= 0)
        {
            return;
        }

        var principalSchedule = BuildLoanPrincipalSchedule(loan.AmountGranted, loan.MonthlyRepaymentAmount, loan.NumberOfRepayments);
        var remainingInterest = loan.TotalInterest;
        var remainingPrincipalForInterest = loan.AmountGranted;

        for (var index = 1; index <= principalSchedule.Count; index++)
        {
            var isLast = index == principalSchedule.Count;
            var principal = principalSchedule[index - 1];
            var interest = 0m;
            if (remainingInterest > 0)
            {
                interest = interestType == "R"
                    ? Math.Round(remainingPrincipalForInterest * loan.InterestRatePercent / 1200m, 2)
                    : Math.Round(loan.TotalInterest / principalSchedule.Count, 2);
                if (isLast)
                {
                    interest = remainingInterest;
                }

                interest = Math.Min(remainingInterest, interest);
            }

            loan.Schedules.Add(new PayrollLoanSchedule
            {
                TenantId = tenantId,
                SequenceNo = index,
                RepaymentDate = loan.PaymentStartDate.AddMonths(index - 1),
                PrincipalAmount = Math.Round(principal, 2),
                InterestAmount = Math.Round(interest, 2),
                AmountPaid = 0,
                InterestPaid = 0,
                ActualRepaymentDate = null,
                Posted = false
            });

            remainingPrincipalForInterest = Math.Max(0, Math.Round(remainingPrincipalForInterest - principal, 2));
            remainingInterest = Math.Max(0, remainingInterest - interest);
        }
    }

    private static string InferDebitCredit(string transactionType)
        => transactionType is IncomeTaxTransactionType
            or EmployeePensionTransactionType
            or EmployerPensionTransactionType
            or LoanRepaymentTransactionType
            or LoanInterestTransactionType
            or SalaryAdvanceTransactionType
            or AbsenceTransactionType
            or PromotionEmployeeContributionArrearsTransactionType
            or PromotionEmployerContributionArrearsTransactionType
            or PromotionContributionArrearsTransactionType
            ? "CR"
            : "DR";

    private static decimal RemainingScheduleAmount(PayrollLoanSchedule schedule)
        => Math.Max(0, Math.Round(schedule.PrincipalAmount + schedule.InterestAmount - schedule.AmountPaid - schedule.InterestPaid, 2));

    private static decimal RemainingSchedulePrincipal(PayrollLoanSchedule schedule)
        => Math.Max(0, Math.Round(schedule.PrincipalAmount - schedule.AmountPaid, 2));

    private static decimal RemainingScheduleInterest(PayrollLoanSchedule schedule)
        => Math.Max(0, Math.Round(schedule.InterestAmount - schedule.InterestPaid, 2));

    private static string LoanPostingKey(Guid employeeId, string facilityNumber)
        => $"{employeeId:N}|{facilityNumber.Trim().ToUpperInvariant()}";

    private static string? ExtractLoanFacilityNumber(string? description)
    {
        var value = TrimOrNull(description);
        if (value == null)
        {
            return null;
        }

        const string repaymentPrefix = "Loan repayment ";
        const string interestPrefix = "Loan interest ";
        if (value.StartsWith(repaymentPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return TrimOrNull(value[repaymentPrefix.Length..]);
        }

        if (value.StartsWith(interestPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return TrimOrNull(value[interestPrefix.Length..]);
        }

        return null;
    }

    private static string? TrimOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void AppendNotes(PayrollRun run, string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return;
        }

        run.Notes = string.IsNullOrWhiteSpace(run.Notes)
            ? notes.Trim()
            : $"{run.Notes}{Environment.NewLine}{notes.Trim()}";
    }

    private static PayrollLegacyMenuItemDto Menu(
        string menuId,
        string? parentMenuId,
        string caption,
        string path,
        string itemType,
        string? target,
        string? companyScope,
        int sequenceNo,
        bool implemented = false)
        => new()
        {
            MenuId = menuId,
            ParentMenuId = parentMenuId,
            Caption = caption,
            Path = path,
            ItemType = itemType,
            Target = target,
            CompanyScope = companyScope,
            SequenceNo = sequenceNo,
            Implemented = implemented
        };

    private static PayrollParameterSetDto ToDto(PayrollParameterSet entity)
        => new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Code = entity.Code,
            Name = entity.Name,
            BaseCurrency = entity.BaseCurrency,
            DateFormat = entity.DateFormat,
            MultiLoginEnabled = entity.MultiLoginEnabled,
            MaxLoginCount = entity.MaxLoginCount,
            CurrentPayPeriod = entity.CurrentPayPeriod,
            CurrentPeriodFrom = entity.CurrentPeriodFrom,
            CurrentPeriodTo = entity.CurrentPeriodTo,
            MonthDays = entity.MonthDays,
            PayFrequencyMonths = entity.PayFrequencyMonths,
            PayMode = entity.PayMode,
            TimeSheetMode = entity.TimeSheetMode,
            LeaveClassification = entity.LeaveClassification,
            MultiplePayBasisEnabled = entity.MultiplePayBasisEnabled,
            DefaultPayBasis = entity.DefaultPayBasis,
            AllowEmployeePaymentMethod = entity.AllowEmployeePaymentMethod,
            DefaultEmployeePaymentMethod = entity.DefaultEmployeePaymentMethod,
            EmployeeSsfRate = entity.EmployeeSsfRate,
            EmployerSsfRate = entity.EmployerSsfRate,
            SsfLimit = entity.SsfLimit,
            BonusTaxRate = entity.BonusTaxRate,
            MinimumBonusToTax = entity.MinimumBonusToTax,
            WithholdingTaxRate = entity.WithholdingTaxRate,
            TaxRate = entity.TaxRate,
            MinimumTaxableIncome = entity.MinimumTaxableIncome,
            DebitRatio = entity.DebitRatio,
            MinimumHireAge = entity.MinimumHireAge,
            MaleRetireAge = entity.MaleRetireAge,
            FemaleRetireAge = entity.FemaleRetireAge,
            TotalAllowanceTaxCeiling = entity.TotalAllowanceTaxCeiling,
            ExchangeRate = entity.ExchangeRate,
            ReportingCurrency = entity.ReportingCurrency,
            PictureDirectory = entity.PictureDirectory,
            NightAllowanceAmount = entity.NightAllowanceAmount,
            NightAllowancePercent = entity.NightAllowancePercent,
            SeparateBonusTax = entity.SeparateBonusTax,
            MultiCurrencyEnabled = entity.MultiCurrencyEnabled,
            TimesheetEnabled = entity.TimesheetEnabled,
            MinimumPasswordLength = entity.MinimumPasswordLength,
            PasswordExpirationDays = entity.PasswordExpirationDays,
            PasswordReuseCount = entity.PasswordReuseCount,
            MinimumPasswordNumbers = entity.MinimumPasswordNumbers,
            MinimumPasswordSpecialCharacters = entity.MinimumPasswordSpecialCharacters,
            MinimumPasswordUppercase = entity.MinimumPasswordUppercase,
            MinimumPasswordLowercase = entity.MinimumPasswordLowercase,
            LegacyCompanyCode = entity.LegacyCompanyCode,
            IsActive = entity.IsActive
        };

    private static PayrollComponentDto ToDto(PayrollComponent entity)
        => new()
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            ComponentType = entity.ComponentType,
            CalculationType = entity.CalculationType,
            Amount = entity.Amount,
            Rate = entity.Rate,
            TaxFreeCeiling = entity.TaxFreeCeiling,
            SeparateTaxPercent = entity.SeparateTaxPercent,
            MaxBenefitToTax = entity.MaxBenefitToTax,
            EmployerAmount = entity.EmployerAmount,
            Category = entity.Category,
            Cycle = entity.Cycle,
            NextPayPeriod = entity.NextPayPeriod,
            NextPayPeriodDate = entity.NextPayPeriodDate,
            LastPayDate = entity.LastPayDate,
            Taxable = entity.Taxable,
            EmployerTaxable = entity.EmployerTaxable,
            SeparateTax = entity.SeparateTax,
            ApplyToBenefit = entity.ApplyToBenefit,
            AfterTaxContribution = entity.AfterTaxContribution,
            IncludeInGross = entity.IncludeInGross,
            GrossUp = entity.GrossUp,
            Prorate = entity.Prorate,
            AppliesByDefault = entity.AppliesByDefault,
            CurrencyCode = entity.CurrencyCode,
            IsActive = entity.IsActive
        };

    private static PayrollComponentRuleDto ToDto(PayrollComponentRule entity)
        => new()
        {
            Id = entity.Id,
            ComponentType = entity.ComponentType,
            ComponentCode = entity.ComponentCode,
            Category = entity.Category,
            CalculationType = entity.CalculationType,
            Amount = entity.Amount,
            TaxFreeCeiling = entity.TaxFreeCeiling,
            EmployerAmount = entity.EmployerAmount,
            AfterTax = entity.AfterTax,
            EmployerTaxable = entity.EmployerTaxable,
            Applicable = entity.Applicable,
            LegacyCompanyCode = entity.LegacyCompanyCode
        };

    private static PayrollTaxBandDto ToDto(PayrollTaxBand entity)
        => new()
        {
            Id = entity.Id,
            TaxType = entity.TaxType,
            SerialNo = entity.SerialNo,
            Description = entity.Description,
            LowerBound = entity.LowerBound,
            UpperBound = entity.UpperBound,
            TaxableIncome = entity.TaxableIncome,
            RatePercent = entity.RatePercent,
            FixedAmount = entity.FixedAmount,
            PerMonthAmount = entity.PerMonthAmount,
            CumulativeTax = entity.CumulativeTax,
            CumulativeSalary = entity.CumulativeSalary,
            PayPeriod = entity.PayPeriod,
            PayPeriodFrom = entity.PayPeriodFrom,
            PayPeriodTo = entity.PayPeriodTo,
            LegacyCompanyCode = entity.LegacyCompanyCode,
            IsAnnual = entity.IsAnnual,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo,
            IsActive = entity.IsActive
        };

    private static PayrollTaxReliefDto ToDto(PayrollTaxRelief entity)
        => new()
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            CalculationType = entity.CalculationType,
            Amount = entity.Amount,
            Factor = entity.Factor,
            AppliesByDefault = entity.AppliesByDefault,
            IsActive = entity.IsActive
        };

    private static PayrollEmployeeTaxReliefDto ToDto(PayrollEmployeeTaxRelief entity)
    {
        var basicSalary = CalculateBasicSalary(entity.EmployeeProfile?.SalaryBasis);

        return new PayrollEmployeeTaxReliefDto
        {
            Id = entity.Id,
            EmployeeProfileId = entity.EmployeeProfileId,
            EmployeeId = entity.EmployeeId,
            EmployeeNumber = entity.EmployeeNumber,
            EmployeeName = entity.EmployeeName,
            ReliefCode = entity.ReliefCode,
            ReliefName = entity.ReliefName,
            CalculationType = entity.CalculationType,
            Amount = entity.Amount,
            Factor = entity.Factor,
            TotalRelief = CalculateTaxReliefAmount(entity.CalculationType, entity.Amount, entity.Factor, basicSalary),
            LegacyCompanyCode = entity.LegacyCompanyCode,
            IsActive = entity.IsActive
        };
    }

    private static PayrollPromotionArrearsEntryDto ToDto(PayrollPromotionArrearsEntry entity)
        => new()
        {
            Id = entity.Id,
            EmployeeProfileId = entity.EmployeeProfileId,
            EmployeeId = entity.EmployeeId,
            EmployeeNumber = entity.EmployeeNumber,
            EmployeeName = entity.EmployeeName,
            LegacyEmployeeId = entity.LegacyEmployeeId,
            EffectiveDate = entity.EffectiveDate,
            PayPeriod = entity.PayPeriod,
            PayPeriodFrom = entity.PayPeriodFrom,
            PayPeriodTo = entity.PayPeriodTo,
            LegacyCompanyCode = entity.LegacyCompanyCode,
            WorkingDays = entity.WorkingDays,
            BasicSalary = entity.BasicSalary ?? CalculateBasicSalary(entity.EmployeeProfile?.SalaryBasis),
            IsActive = entity.IsActive
        };

    private static PayrollOvertimeSummaryEntryDto ToDto(PayrollTimesheetSummary entity)
        => new()
        {
            Id = entity.Id,
            EmployeeProfileId = entity.EmployeeProfileId,
            EmployeeId = entity.EmployeeId ?? entity.EmployeeProfile?.EmployeeId,
            EmployeeNumber = string.IsNullOrWhiteSpace(entity.EmployeeNumber)
                ? entity.EmployeeProfile?.EmployeeNumber ?? string.Empty
                : entity.EmployeeNumber,
            EmployeeName = string.IsNullOrWhiteSpace(entity.EmployeeName)
                ? entity.EmployeeProfile?.Employee?.FullName ?? string.Empty
                : entity.EmployeeName,
            LegacyEmployeeId = entity.LegacyEmployeeId ?? entity.EmployeeProfile?.LegacyEmployeeId,
            PayPeriod = entity.PayPeriod,
            PayPeriodFrom = entity.PayPeriodFrom,
            PayPeriodTo = entity.PayPeriodTo,
            AbsentDays = entity.AbsentHours,
            NormalDays = entity.NormalHours,
            WeekdayDays = entity.WeekdayHours,
            HolidayDays = entity.HolidayHours,
            SaturdayDays = entity.SaturdayHours,
            SundayDays = entity.SundayHours,
            LegacyCompanyCode = entity.LegacyCompanyCode,
            IsActive = entity.IsActive
        };

    private static PayrollContributionOpeningBalanceDto ToDto(PayrollContributionOpeningBalance entity)
        => new()
        {
            Id = entity.Id,
            EmployeeProfileId = entity.EmployeeProfileId,
            EmployeeId = entity.EmployeeId ?? entity.EmployeeProfile?.EmployeeId,
            EmployeeNumber = entity.EmployeeNumber,
            EmployeeName = entity.EmployeeName,
            LegacyEmployeeId = entity.LegacyEmployeeId,
            ContributionCodeType = entity.ContributionCodeType,
            ContributionCode = entity.ContributionCode,
            ContributionName = entity.ContributionName,
            BalanceAsAt = entity.BalanceAsAt,
            OpeningBalance = entity.OpeningBalance,
            LegacyCompanyCode = entity.LegacyCompanyCode,
            IsActive = entity.IsActive
        };

    private static PayrollContributionTransactionDto ToDto(PayrollContributionTransaction entity)
        => new()
        {
            Id = entity.Id,
            EmployeeProfileId = entity.EmployeeProfileId,
            EmployeeId = entity.EmployeeId ?? entity.EmployeeProfile?.EmployeeId,
            EmployeeNumber = entity.EmployeeNumber,
            EmployeeName = entity.EmployeeName,
            LegacyEmployeeId = entity.LegacyEmployeeId,
            ContributionCodeType = entity.ContributionCodeType,
            ContributionCode = entity.ContributionCode,
            ContributionName = entity.ContributionName,
            TransactionType = entity.TransactionType,
            EffectiveDate = entity.EffectiveDate,
            Amount = entity.Amount,
            LegacyCompanyCode = entity.LegacyCompanyCode,
            IsActive = entity.IsActive
        };

    private static PayrollPensionSchemeDto ToDto(PayrollPensionScheme entity)
        => new()
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            EmployeeRatePercent = entity.EmployeeRatePercent,
            EmployerRatePercent = entity.EmployerRatePercent,
            ContributionLimit = entity.ContributionLimit,
            IsDefault = entity.IsDefault,
            IsActive = entity.IsActive
        };

    private static PayrollOvertimePolicyDto ToDto(PayrollOvertimePolicy entity)
        => new()
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            NormalWorkingHours = entity.NormalWorkingHours,
            NormalHours = entity.NormalHours,
            WeekdayRate = entity.WeekdayRate,
            HolidayRate = entity.HolidayRate,
            SpecialDutyRate = entity.SpecialDutyRate,
            Taxable = entity.Taxable,
            TaxCeiling = entity.TaxCeiling,
            SeparateOvertimeTax = entity.SeparateOvertimeTax,
            MaxOvertimeAmount = entity.MaxOvertimeAmount,
            MaxOvertimeIsPercent = entity.MaxOvertimeIsPercent,
            MaxOvertimeType = entity.MaxOvertimeType,
            LeaveRecallRate = entity.LeaveRecallRate,
            PayPeriod = entity.PayPeriod,
            PayPeriodFrom = entity.PayPeriodFrom,
            PayPeriodTo = entity.PayPeriodTo,
            MaxOvertimeSeparateTax = entity.MaxOvertimeSeparateTax,
            MinimumBasicForSeparateTax = entity.MinimumBasicForSeparateTax,
            MinimumSeparateOvertimePercent = entity.MinimumSeparateOvertimePercent,
            LegacyCompanyCode = entity.LegacyCompanyCode,
            IsDefault = entity.IsDefault,
            IsActive = entity.IsActive,
            Ranges = entity.Ranges.OrderBy(e => e.MinRange).ThenBy(e => e.MaxRange).Select(ToDto).ToList()
        };

    private static PayrollLoanPolicyDto ToDto(PayrollLoanPolicy entity)
        => new()
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            MaxLoanAmount = entity.MaxLoanAmount,
            InterestRatePercent = entity.InterestRatePercent,
            ApplyInterest = entity.ApplyInterest,
            MaxPaybackPeriods = entity.MaxPaybackPeriods,
            MaxDebitRatioPercent = entity.MaxDebitRatioPercent,
            InterestType = entity.InterestType,
            LegacyCompanyCode = entity.LegacyCompanyCode,
            IsActive = entity.IsActive
        };

    private static PayrollBonusPolicyDto ToDto(PayrollBonusPolicy entity)
        => new()
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            CalculationType = entity.CalculationType,
            Amount = entity.Amount,
            Taxable = entity.Taxable,
            SeparateTax = entity.SeparateTax,
            TaxFreeCeiling = entity.TaxFreeCeiling,
            MinimumToTax = entity.MinimumToTax,
            AnnualSalaryPercentToTax = entity.AnnualSalaryPercentToTax,
            TaxRate = entity.TaxRate,
            NextPayPeriod = entity.NextPayPeriod,
            LastPayPeriod = entity.LastPayPeriod,
            NextPayPeriodDate = entity.NextPayPeriodDate,
            LastPayPeriodDate = entity.LastPayPeriodDate,
            Category = entity.Category,
            Cycle = entity.Cycle,
            PaySeparate = entity.PaySeparate,
            PerAnnual = entity.PerAnnual,
            MinimumMonths = entity.MinimumMonths,
            Prorate = entity.Prorate,
            IsActive = entity.IsActive
        };

    private static PayrollBonusRuleDto ToDto(PayrollBonusRule entity)
        => new()
        {
            Id = entity.Id,
            BonusCode = entity.BonusCode,
            GroupCode = entity.GroupCode,
            CalculationType = entity.CalculationType,
            Amount = entity.Amount,
            Applicable = entity.Applicable,
            LegacyCompanyCode = entity.LegacyCompanyCode
        };

    private static PayrollBonusExceptionDto ToDto(PayrollBonusException entity)
        => new()
        {
            Id = entity.Id,
            BonusCode = entity.BonusCode,
            EmployeeProfileId = entity.EmployeeProfileId,
            EmployeeNumber = entity.EmployeeNumber,
            EmployeeName = entity.EmployeeName,
            CalculationType = entity.CalculationType,
            Amount = entity.Amount,
            Taxable = entity.Taxable,
            Applicable = entity.Applicable,
            CurrencyCode = entity.CurrencyCode,
            LegacyCompanyCode = entity.LegacyCompanyCode
        };

    private static PayrollBackpayPolicyDto ToDto(PayrollBackpayPolicy entity)
        => new()
        {
            Id = entity.Id,
            OperationType = entity.OperationType,
            CalculationType = entity.CalculationType,
            Amount = entity.Amount,
            NumberOfMonths = entity.NumberOfMonths,
            EffectiveDate = entity.EffectiveDate,
            ApplyTax = entity.ApplyTax,
            ApplySsf = entity.ApplySsf,
            MinimumServiceMode = entity.MinimumServiceMode,
            MinimumServiceValue = entity.MinimumServiceValue,
            MinimumServiceDate = entity.MinimumServiceDate,
            CategoryType = entity.CategoryType,
            LegacyCompanyCode = entity.LegacyCompanyCode
        };

    private static PayrollBackpayRuleDto ToDto(PayrollBackpayRule entity)
        => new()
        {
            Id = entity.Id,
            OperationType = entity.OperationType,
            CategoryType = entity.CategoryType,
            CategoryCode = entity.CategoryCode,
            CategoryName = entity.CategoryName,
            CalculationType = entity.CalculationType,
            Amount = entity.Amount,
            Applicable = entity.Applicable,
            LegacyCompanyCode = entity.LegacyCompanyCode
        };

    private static PayrollBackpayExceptionDto ToDto(PayrollBackpayException entity)
        => new()
        {
            Id = entity.Id,
            OperationType = entity.OperationType,
            EmployeeProfileId = entity.EmployeeProfileId,
            EmployeeNumber = entity.EmployeeNumber,
            EmployeeName = entity.EmployeeName,
            CalculationType = entity.CalculationType,
            Amount = entity.Amount,
            Applicable = entity.Applicable,
            LegacyCompanyCode = entity.LegacyCompanyCode
        };

    private static PayrollJournalMappingDto ToDto(PayrollJournalMapping entity)
        => new()
        {
            Id = entity.Id,
            TransactionType = entity.TransactionType,
            ComponentCode = entity.ComponentCode,
            Description = entity.Description,
            DebitCredit = entity.DebitCredit,
            AccountCode = entity.AccountCode,
            AccountType = entity.AccountType,
            IsActive = entity.IsActive
        };

    private static PayrollCodeTypeDto ToDto(PayrollCodeType entity)
        => new()
        {
            Id = entity.Id,
            CodeType = entity.CodeType,
            Description = entity.Description,
            AccountCode = entity.AccountCode,
            Blocked = entity.Blocked,
            Dependent = entity.Dependent,
            DependentOnCodeType = entity.DependentOnCodeType,
            LegacyCompanyCode = entity.LegacyCompanyCode,
            ValueCount = entity.Values.Count
        };

    private static PayrollCodeValueDto ToDto(PayrollCodeValue entity)
        => new()
        {
            Id = entity.Id,
            PayrollCodeTypeId = entity.PayrollCodeTypeId,
            CodeType = entity.CodeType,
            ActualCode = entity.ActualCode,
            Description = entity.Description,
            AdditionalDescription = entity.AdditionalDescription,
            AccountCode = entity.AccountCode,
            Blocked = entity.Blocked,
            DependentCodeType = entity.DependentCodeType,
            DependentActualCode = entity.DependentActualCode,
            LegacyCompanyCode = entity.LegacyCompanyCode
        };

    private static PayrollHolidayDto ToDto(PayrollHoliday entity)
        => new()
        {
            Id = entity.Id,
            HolidayDate = entity.HolidayDate,
            Description = entity.Description,
            LegacyCompanyCode = entity.LegacyCompanyCode
        };

    private static PayrollNonWorkingDayDto ToDto(PayrollNonWorkingDay entity)
        => new()
        {
            Id = entity.Id,
            DayCode = entity.DayCode,
            Description = entity.Description,
            OvertimeRate = entity.OvertimeRate,
            LegacyCompanyCode = entity.LegacyCompanyCode
        };

    private static PayrollExchangeRateDto ToDto(PayrollExchangeRate entity)
        => new()
        {
            Id = entity.Id,
            CurrencyCode = entity.CurrencyCode,
            Rate = entity.Rate,
            PayPeriod = entity.PayPeriod,
            PayPeriodFrom = entity.PayPeriodFrom,
            PayPeriodTo = entity.PayPeriodTo,
            LegacyCompanyCode = entity.LegacyCompanyCode
        };

    private static PayrollBankBranchDto ToDto(PayrollBankBranch entity)
        => new()
        {
            Id = entity.Id,
            BranchSetupCode = entity.BranchSetupCode,
            BankCode = entity.BankCode,
            BranchCode = entity.BranchCode,
            BranchDescription = entity.BranchDescription,
            Region = entity.Region,
            SortCode = entity.SortCode,
            AccountCode = entity.AccountCode,
            LegacyCompanyCode = entity.LegacyCompanyCode
        };

    private static PayrollLeaveSetupDto ToDto(PayrollLeaveSetup entity)
        => new()
        {
            Id = entity.Id,
            Category = entity.Category,
            CategoryDetail = entity.CategoryDetail,
            LegacyCompanyCode = entity.LegacyCompanyCode,
            DetailCount = entity.Details.Count,
            Details = entity.Details.OrderBy(e => e.SequenceNo).Select(ToDto).ToList()
        };

    private static PayrollLeaveSetupDetailDto ToDto(PayrollLeaveSetupDetail entity)
        => new()
        {
            Id = entity.Id,
            PayrollLeaveSetupId = entity.PayrollLeaveSetupId,
            Category = entity.Category,
            CategoryDetail = entity.CategoryDetail,
            Days = entity.Days,
            ServiceFrom = entity.ServiceFrom,
            ServiceTo = entity.ServiceTo,
            SequenceNo = entity.SequenceNo,
            LegacyCompanyCode = entity.LegacyCompanyCode
        };

    private static PayrollOvertimeRangeDto ToDto(PayrollOvertimeRange entity)
        => new()
        {
            Id = entity.Id,
            PayrollOvertimePolicyId = entity.PayrollOvertimePolicyId,
            MinRange = entity.MinRange,
            MaxRange = entity.MaxRange,
            Rate = entity.Rate,
            LegacyCompanyCode = entity.LegacyCompanyCode
        };

    private static PayrollLegacyMenuUserDto ToDto(PayrollLegacyMenuUser entity)
        => new()
        {
            Id = entity.Id,
            UserName = entity.UserName,
            UserGroup = entity.UserGroup,
            UserNo = entity.UserNo,
            LegacyPasswordHash = entity.LegacyPasswordHash,
            LoginEnabled = entity.LoginEnabled,
            Locked = entity.Locked,
            PasswordChangeRequired = entity.PasswordChangeRequired,
            PasswordExpiryDate = entity.PasswordExpiryDate,
            PasswordFailureCount = entity.PasswordFailureCount,
            CompanyId = entity.CompanyId,
            BusinessUnitId = entity.BusinessUnitId,
            LegacyCompanyCode = entity.LegacyCompanyCode,
            CanViewSalary = entity.CanViewSalary,
            CanApprove = entity.CanApprove,
            RefreshToken = entity.RefreshToken,
            LastPasswordChange = entity.LastPasswordChange
        };

    private static PayrollLegacyMenuSecurityDto ToDto(PayrollLegacyMenuSecurity entity)
        => new()
        {
            Id = entity.Id,
            FormCode = entity.FormCode,
            FormName = entity.FormName,
            UserName = entity.UserName,
            Allowed = entity.Allowed,
            GroupName = entity.GroupName,
            LegacyCompanyCode = entity.LegacyCompanyCode,
            TopMenu = entity.TopMenu,
            SubMenu = entity.SubMenu,
            CanEdit = entity.CanEdit
        };

    private static PayrollCompanyProfileDto ToDto(PayrollCompanyProfile entity)
        => new()
        {
            Id = entity.Id,
            CompanyId = entity.CompanyId,
            CompanyName = entity.CompanyName,
            CompanySystemName = entity.CompanySystemName,
            CompanyCode = entity.CompanyCode,
            MultipleBusinessUnits = entity.MultipleBusinessUnits,
            BusinessNumber = entity.BusinessNumber,
            AddressLine1 = entity.AddressLine1,
            AddressLine2 = entity.AddressLine2,
            AddressLine3 = entity.AddressLine3,
            CityOrTown = entity.CityOrTown,
            RegionOrState = entity.RegionOrState,
            Country = entity.Country,
            LicenceType = entity.LicenceType,
            LicenceNo = entity.LicenceNo,
            SocialSecurityNo = entity.SocialSecurityNo,
            TaxpayerIdNo = entity.TaxpayerIdNo,
            PictureName = entity.PictureName,
            LogoName = entity.LogoName,
            SplitLicenceOnBusinessUnits = entity.SplitLicenceOnBusinessUnits,
            LicencePercentOrNumber = entity.LicencePercentOrNumber,
            MakeCompanyABusinessUnit = entity.MakeCompanyABusinessUnit,
            MultiplePayBasis = entity.MultiplePayBasis,
            CompanyPayBasis = entity.CompanyPayBasis,
            MultipleEmployeePaymentMethods = entity.MultipleEmployeePaymentMethods,
            DefaultEmployeePaymentMethod = entity.DefaultEmployeePaymentMethod,
            LegacyCompanyCode = entity.LegacyCompanyCode,
            IsActive = entity.IsActive,
            BusinessUnits = entity.BusinessUnits.OrderBy(e => e.BusinessUnitId).Select(ToDto).ToList(),
            Bankers = entity.Bankers.OrderBy(e => e.BankCode).ThenBy(e => e.BankBranch).Select(ToDto).ToList()
        };

    private static PayrollBusinessUnitDto ToDto(PayrollBusinessUnit entity)
        => new()
        {
            Id = entity.Id,
            PayrollCompanyProfileId = entity.PayrollCompanyProfileId,
            BusinessUnitId = entity.BusinessUnitId,
            BusinessUnitCode = entity.BusinessUnitCode,
            BusinessUnitName = entity.BusinessUnitName,
            Location = entity.Location,
            Country = entity.Country,
            DistributeLicence = entity.DistributeLicence,
            LicencePercentOrNumber = entity.LicencePercentOrNumber,
            LicenceValue = entity.LicenceValue,
            AddressLine1 = entity.AddressLine1,
            AddressLine2 = entity.AddressLine2,
            AddressLine3 = entity.AddressLine3,
            RegionOrState = entity.RegionOrState,
            CompanyId = entity.CompanyId,
            CompanyCode = entity.CompanyCode,
            AccountsCode = entity.AccountsCode,
            MultiplePayBasis = entity.MultiplePayBasis,
            CompanyPayBasis = entity.CompanyPayBasis,
            MultipleEmployeePaymentMethods = entity.MultipleEmployeePaymentMethods,
            DefaultEmployeePaymentMethod = entity.DefaultEmployeePaymentMethod,
            LegacyCompanyCode = entity.LegacyCompanyCode
        };

    private static PayrollCompanyBankerDto ToDto(PayrollCompanyBanker entity)
        => new()
        {
            Id = entity.Id,
            PayrollCompanyProfileId = entity.PayrollCompanyProfileId,
            BankCode = entity.BankCode,
            BankBranch = entity.BankBranch,
            Address1 = entity.Address1,
            Address2 = entity.Address2,
            Address3 = entity.Address3,
            AccountNumber1 = entity.AccountNumber1,
            AccountNumber2 = entity.AccountNumber2,
            AccountNumber3 = entity.AccountNumber3,
            CompanyCode = entity.CompanyCode,
            CompanyId = entity.CompanyId,
            Signatory1 = entity.Signatory1,
            Signatory2 = entity.Signatory2,
            Signatory3 = entity.Signatory3,
            SignatoryPosition1 = entity.SignatoryPosition1,
            SignatoryPosition2 = entity.SignatoryPosition2,
            SignatoryPosition3 = entity.SignatoryPosition3,
            BankRegion = entity.BankRegion,
            LegacyCompanyCode = entity.LegacyCompanyCode,
            IsActive = entity.IsActive
        };

    private static PayrollGradeDto ToDto(PayrollGrade entity)
        => new()
        {
            Id = entity.Id,
            GradeId = entity.GradeId,
            GradeType = entity.GradeType,
            GradeName = entity.GradeName,
            SystemGradeName = entity.SystemGradeName,
            MinValue = entity.MinValue,
            MaxValue = entity.MaxValue,
            MidPoint = entity.MidPoint,
            CurrencyCode = entity.CurrencyCode,
            StartPoint = entity.StartPoint,
            IncrementStep = entity.IncrementStep,
            Ceiling = entity.Ceiling,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            BusinessUnitId = entity.BusinessUnitId,
            CompanyId = entity.CompanyId,
            OrderField = entity.OrderField,
            ReportingName = entity.ReportingName,
            EnforceNotchConsistency = entity.EnforceNotchConsistency,
            LegacyCompanyCode = entity.LegacyCompanyCode,
            IsActive = entity.IsActive,
            Notches = entity.Notches.OrderBy(e => e.OrderField).ThenBy(e => e.Notch).Select(ToDto).ToList()
        };

    private static PayrollGradeNotchDto ToDto(PayrollGradeNotch entity)
        => new()
        {
            Id = entity.Id,
            PayrollGradeId = entity.PayrollGradeId,
            GradeId = entity.GradeId,
            SystemGradeName = entity.SystemGradeName,
            Notch = entity.Notch,
            Value = entity.Value,
            CurrencyCode = entity.CurrencyCode,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            OrderField = entity.OrderField,
            BusinessUnitId = entity.BusinessUnitId,
            CompanyId = entity.CompanyId,
            AnnualisedValue = entity.AnnualisedValue,
            GradeName = entity.GradeName,
            ReportingName = entity.ReportingName,
            LegacyCompanyCode = entity.LegacyCompanyCode,
            HourlyRate = entity.HourlyRate
        };

    private static PayrollEmployeeProfileDto ToDto(PayrollEmployeeProfile entity)
        => new()
        {
            Id = entity.Id,
            EmployeeId = entity.EmployeeId,
            EmployeeNumber = entity.EmployeeNumber,
            EmployeeName = entity.Employee.FullName,
            DepartmentName = entity.Employee.Department?.Name,
            SectionName = entity.Employee.Section?.Name,
            PositionTitle = entity.Employee.Position?.Title,
            StaffCategory = entity.Employee.Position?.StaffLevel?.Name ?? entity.Employee.EmploymentType.ToString(),
            JobLocation = entity.Employee.Location?.Name ?? entity.Employee.Station?.Name,
            LegacyEmployeeId = entity.LegacyEmployeeId,
            LegacyEmployeeNumber = entity.LegacyEmployeeNumber,
            PayrollActive = entity.PayrollActive,
            PayTax = entity.PayTax,
            SsfApplicable = entity.SsfApplicable,
            GrossUp = entity.GrossUp,
            Tier2Only = entity.Tier2Only,
            OvertimeEligible = entity.OvertimeEligible || entity.Employee.Overtime,
            SsfNumber = entity.SsfNumber,
            TinNumber = entity.TinNumber,
            CurrencyCode = entity.CurrencyCode,
            SalaryBasis = entity.SalaryBasis == null ? null : ToDto(entity.SalaryBasis),
            PaymentMethods = entity.PaymentMethods.OrderBy(e => e.SequenceNo).Select(ToDto).ToList(),
            EmployeeComponents = entity.EmployeeComponents.OrderBy(e => e.PayrollComponent.Code).Select(ToDto).ToList(),
            Loans = entity.Loans.OrderBy(e => e.FacilityNumber).Select(ToDto).ToList()
        };

    private static PayrollSalaryBasisDto ToDto(PayrollSalaryBasis entity)
        => new()
        {
            Id = entity.Id,
            MonthlyBasicSalary = entity.MonthlyBasicSalary,
            AnnualBasicSalary = entity.AnnualBasicSalary,
            HourlyRate = entity.HourlyRate,
            CurrencyCode = entity.CurrencyCode,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo,
            IsActive = entity.IsActive
        };

    private static PayrollPaymentMethodDto ToDto(PayrollPaymentMethod entity)
        => new()
        {
            Id = entity.Id,
            PaymentType = entity.PaymentType,
            PaymentPercent = entity.PaymentPercent,
            Amount = entity.Amount,
            BankCode = entity.BankCode,
            BankBranchCode = entity.BankBranchCode,
            AccountNumber = entity.AccountNumber,
            CurrencyCode = entity.CurrencyCode,
            SequenceNo = entity.SequenceNo,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            IsActive = entity.IsActive
        };

    private static PayrollEmployeeComponentDto ToDto(PayrollEmployeeComponent entity)
        => new()
        {
            Id = entity.Id,
            PayrollComponentId = entity.PayrollComponentId,
            ComponentCode = entity.PayrollComponent.Code,
            ComponentName = entity.PayrollComponent.Name,
            CalculationTypeOverride = entity.CalculationTypeOverride,
            AmountOverride = entity.AmountOverride,
            RateOverride = entity.RateOverride,
            TaxableOverride = entity.TaxableOverride,
            TaxFreeCeilingOverride = entity.TaxFreeCeilingOverride,
            EmployerAmountOverride = entity.EmployerAmountOverride,
            EmployerTaxableOverride = entity.EmployerTaxableOverride,
            GrossUpOverride = entity.GrossUpOverride,
            CurrencyCodeOverride = entity.CurrencyCodeOverride,
            Applicable = entity.Applicable,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo
        };

    private static PayrollEmployeeComponentExceptionDto ToComponentExceptionDto(PayrollEmployeeComponent entity)
    {
        var component = entity.PayrollComponent;
        var basicSalary = CalculateBasicSalary(entity.EmployeeProfile.SalaryBasis);

        return new PayrollEmployeeComponentExceptionDto
        {
            Id = entity.Id,
            EmployeeProfileId = entity.EmployeeProfileId,
            EmployeeId = entity.EmployeeProfile.EmployeeId,
            EmployeeNumber = entity.EmployeeProfile.EmployeeNumber,
            EmployeeName = entity.EmployeeProfile.Employee.FullName,
            MonthlyBasicSalary = basicSalary,
            PayrollComponentId = entity.PayrollComponentId,
            ComponentCode = component.Code,
            ComponentName = component.Name,
            ComponentType = component.ComponentType,
            CalculationType = entity.CalculationTypeOverride ?? component.CalculationType,
            Amount = entity.AmountOverride ?? component.Amount,
            Rate = entity.RateOverride ?? component.Rate,
            Taxable = entity.TaxableOverride ?? component.Taxable,
            TaxFreeCeiling = entity.TaxFreeCeilingOverride ?? component.TaxFreeCeiling,
            EmployerAmount = entity.EmployerAmountOverride ?? component.EmployerAmount,
            EmployerTaxable = entity.EmployerTaxableOverride ?? component.EmployerTaxable,
            GrossUp = entity.GrossUpOverride ?? component.GrossUp,
            CurrencyCode = entity.CurrencyCodeOverride ?? component.CurrencyCode,
            Applicable = entity.Applicable,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo
        };
    }

    private static PayrollLoanDto ToDto(PayrollLoan entity)
    {
        var basicSalary = entity.EmployeeProfile.SalaryBasis?.MonthlyBasicSalary ?? 0m;
        var totalRepayment = entity.MonthlyRepaymentAmount + entity.InterestRepaymentAmount;
        return new PayrollLoanDto
        {
            Id = entity.Id,
            EmployeeProfileId = entity.EmployeeProfileId,
            EmployeeId = entity.EmployeeProfile.EmployeeId,
            EmployeeNumber = entity.EmployeeProfile.EmployeeNumber,
            EmployeeName = entity.EmployeeProfile.Employee.FullName,
            LoanPolicyId = entity.LoanPolicyId,
            LoanPolicyCode = entity.LoanPolicy?.Code,
            LoanPolicyName = entity.LoanPolicy?.Name,
            LoanTypeCode = entity.LoanTypeCode,
            FacilityNumber = entity.FacilityNumber,
            DateGranted = entity.DateGranted,
            AmountGranted = entity.AmountGranted,
            BasicSalary = basicSalary,
            MonthlyRepaymentAmount = entity.MonthlyRepaymentAmount,
            OutstandingBalance = entity.OutstandingBalance,
            InterestRatePercent = entity.InterestRatePercent,
            TotalInterest = entity.TotalInterest,
            InterestRepaymentAmount = entity.InterestRepaymentAmount,
            RepaymentMode = entity.RepaymentMode,
            TotalRepaymentAmount = Math.Round(totalRepayment, 2),
            EmployeeDebtRatio = basicSalary <= 0 ? 0 : Math.Round(totalRepayment / basicSalary * 100m, 2),
            DebtServiceRatio = entity.LoanPolicy?.MaxDebitRatioPercent ?? 0,
            PaymentStartDate = entity.PaymentStartDate,
            PaymentEndDate = entity.PaymentEndDate,
            NumberOfRepayments = entity.NumberOfRepayments,
            Status = entity.Status,
            PeriodOfSuspension = entity.PeriodOfSuspension,
            SuspensionStartDate = entity.SuspensionStartDate,
            SuspensionEndDate = entity.SuspensionEndDate,
            SuspensionNarration = entity.SuspensionNarration,
            GeneralRemarks = entity.GeneralRemarks,
            IsActive = entity.IsActive,
            Schedules = entity.Schedules.OrderBy(e => e.SequenceNo).Select(ToDto).ToList()
        };
    }

    private static PayrollLoanScheduleDto ToDto(PayrollLoanSchedule entity)
        => new()
        {
            Id = entity.Id,
            PayrollLoanId = entity.PayrollLoanId,
            SequenceNo = entity.SequenceNo,
            RepaymentDate = entity.RepaymentDate,
            PrincipalAmount = entity.PrincipalAmount,
            InterestAmount = entity.InterestAmount,
            AmountPaid = entity.AmountPaid,
            InterestPaid = entity.InterestPaid,
            ActualRepaymentDate = entity.ActualRepaymentDate,
            Posted = entity.Posted
        };

    private static PayrollSalaryAdvanceDto ToDto(PayrollSalaryAdvance entity)
        => new()
        {
            Id = entity.Id,
            EmployeeProfileId = entity.EmployeeProfileId,
            EmployeeId = entity.EmployeeId,
            EmployeeNumber = entity.EmployeeNumber,
            EmployeeName = entity.EmployeeName,
            AdvanceDate = entity.AdvanceDate,
            AdvanceAmount = entity.AdvanceAmount,
            Description = entity.Description,
            LegacyCompanyCode = entity.LegacyCompanyCode,
            IsActive = entity.IsActive
        };

    private static PayrollImportBatchDto ToDto(PayrollImportBatch entity)
        => new()
        {
            Id = entity.Id,
            SourceName = entity.SourceName,
            ImportType = entity.ImportType,
            TotalRows = entity.TotalRows,
            MatchedRows = entity.MatchedRows,
            MissingRows = entity.MissingRows,
            DuplicateRows = entity.DuplicateRows,
            ImportedAt = entity.ImportedAt,
            ImportedByUserId = entity.ImportedByUserId,
            Rows = entity.Rows.OrderBy(e => e.RowNumber).Select(ToDto).ToList()
        };

    private static PayrollImportRowDto ToDto(PayrollImportRow entity)
        => new()
        {
            Id = entity.Id,
            RowNumber = entity.RowNumber,
            LegacyEmployeeNumber = entity.LegacyEmployeeNumber,
            LegacyFullName = entity.LegacyFullName,
            Department = entity.Department,
            MatchedEmployeeId = entity.MatchedEmployeeId,
            MatchedEmployeeNumber = entity.MatchedEmployeeNumber,
            Status = entity.Status,
            Message = entity.Message
        };

    private static PayrollRunDto ToDto(PayrollRun entity, bool includeChildren)
        => new()
        {
            Id = entity.Id,
            RunNumber = entity.RunNumber,
            PayPeriod = entity.PayPeriod,
            PayPeriodFrom = entity.PayPeriodFrom,
            PayPeriodTo = entity.PayPeriodTo,
            RunDate = entity.RunDate,
            CurrencyCode = entity.CurrencyCode,
            IsSeparateBonusRun = entity.IsSeparateBonusRun,
            SeparateBonusCode = entity.SeparateBonusCode,
            Status = entity.Status,
            EmployeeCount = entity.EmployeeCount,
            GrossAmount = entity.GrossAmount,
            NetAmount = entity.NetAmount,
            TaxAmount = entity.TaxAmount,
            EmployeeContributionAmount = entity.EmployeeContributionAmount,
            EmployerContributionAmount = entity.EmployerContributionAmount,
            CalculatedAt = entity.CalculatedAt,
            ReviewedAt = entity.ReviewedAt,
            ApprovedAt = entity.ApprovedAt,
            ClosedAt = entity.ClosedAt,
            Notes = entity.Notes,
            Employees = includeChildren ? entity.Employees.OrderBy(e => e.EmployeeNumber).Select(ToDto).ToList() : [],
            Transactions = includeChildren ? entity.Transactions.OrderBy(e => e.EmployeeNumber).ThenBy(e => e.TransactionType).Select(ToDto).ToList() : [],
            JournalLines = includeChildren ? entity.JournalLines.OrderBy(e => e.SequenceNo).Select(ToDto).ToList() : []
        };

    private static PayrollRunEmployeeDto ToDto(PayrollRunEmployee entity)
        => new()
        {
            Id = entity.Id,
            EmployeeId = entity.EmployeeId,
            EmployeeNumber = entity.EmployeeNumber,
            EmployeeName = entity.EmployeeName,
            BasicSalary = entity.BasicSalary,
            TaxableAllowances = entity.TaxableAllowances,
            NonTaxableAllowances = entity.NonTaxableAllowances,
            TaxableDeductions = entity.TaxableDeductions,
            NonTaxableDeductions = entity.NonTaxableDeductions,
            EmployeeContribution = entity.EmployeeContribution,
            EmployerContribution = entity.EmployerContribution,
            TaxRelief = entity.TaxRelief,
            TaxableIncome = entity.TaxableIncome,
            IncomeTax = entity.IncomeTax,
            GrossIncome = entity.GrossIncome,
            NetIncome = entity.NetIncome,
            CurrencyCode = entity.CurrencyCode
        };

    private static PayrollTransactionDto ToDto(PayrollTransaction entity)
        => new()
        {
            Id = entity.Id,
            PayrollRunEmployeeId = entity.PayrollRunEmployeeId,
            EmployeeId = entity.EmployeeId,
            EmployeeNumber = entity.EmployeeNumber,
            TransactionType = entity.TransactionType,
            PayrollComponentId = entity.PayrollComponentId,
            ComponentCode = entity.ComponentCode,
            Description = entity.Description,
            Amount = entity.Amount,
            EmployerAmount = entity.EmployerAmount,
            Taxable = entity.Taxable,
            EmployerTaxable = entity.EmployerTaxable,
            SeparateTax = entity.SeparateTax,
            CurrencyCode = entity.CurrencyCode
        };

    private static PayrollJournalLineDto ToDto(PayrollJournalLine entity)
        => new()
        {
            Id = entity.Id,
            SequenceNo = entity.SequenceNo,
            TransactionType = entity.TransactionType,
            DebitCredit = entity.DebitCredit,
            AccountCode = entity.AccountCode,
            Description = entity.Description,
            Amount = entity.Amount,
            Posted = entity.Posted,
            JournalEntryId = entity.JournalEntryId
        };

    private static PayrollReportSnapshotDto ToDto(PayrollReportSnapshot entity)
        => new()
        {
            Id = entity.Id,
            PayrollRunId = entity.PayrollRunId,
            ReportType = entity.ReportType,
            ReportName = entity.ReportName,
            SnapshotNumber = entity.SnapshotNumber,
            GeneratedAt = entity.GeneratedAt,
            GeneratedByUserId = entity.GeneratedByUserId,
            Notes = entity.Notes
        };

    private static PayrollPayslipSnapshotDto ToDto(PayrollPayslipSnapshot entity)
        => new()
        {
            Id = entity.Id,
            PayrollRunId = entity.PayrollRunId,
            PayrollRunEmployeeId = entity.PayrollRunEmployeeId,
            EmployeeId = entity.EmployeeId,
            EmployeeNumber = entity.EmployeeNumber,
            EmployeeName = entity.EmployeeName,
            PayslipNumber = entity.PayslipNumber,
            GeneratedAt = entity.GeneratedAt,
            GeneratedByUserId = entity.GeneratedByUserId,
            GrossIncome = entity.GrossIncome,
            NetIncome = entity.NetIncome,
            TaxAmount = entity.TaxAmount,
            EmployeeContribution = entity.EmployeeContribution
        };

    private sealed record ResolvedPayrollComponent(PayrollComponent Component, PayrollEmployeeComponent? Override, PayrollComponentRule? Rule);
    private sealed record PayrollPayslipBuildContext(
        Tenant? Tenant,
        PayrollCompanyProfile? CompanyProfile,
        IReadOnlyDictionary<Guid, PayrollEmployeeProfile> ProfilesByEmployeeId,
        IReadOnlyDictionary<Guid, IReadOnlyList<PayrollContributionOpeningBalance>> OpeningBalancesByProfileId,
        IReadOnlyDictionary<Guid, IReadOnlyList<PayrollContributionTransaction>> ContributionTransactionsByProfileId,
        IReadOnlyDictionary<string, string> EmployeeBankNamesByReference,
        IReadOnlyDictionary<string, string> EmployeeBankBranchNamesByReference,
        IReadOnlyDictionary<string, string> PayrollBankNamesByCode,
        IReadOnlyDictionary<string, string> PayrollBankBranchNamesByReference,
        IReadOnlyDictionary<Guid, PayrollTimesheetSummary> TimesheetSummariesByProfileId);
    private sealed record PayrollProfileLookup(
        IReadOnlyDictionary<Guid, PayrollEmployeeProfile> ById,
        IReadOnlyDictionary<string, PayrollEmployeeProfile> ByEmployeeNumber,
        IReadOnlyDictionary<string, PayrollEmployeeProfile> ByLegacyEmployeeNumber);
    private sealed record PayrollPensionCalculation(decimal EmployeeContribution, decimal EmployerContribution);
    private sealed record PayrollOvertimeCalculation(
        decimal Amount,
        bool Taxable,
        bool SeparateTax,
        decimal NormalTaxableAmount,
        decimal SeparateTaxableAmount,
        decimal SeparateTaxAmount)
    {
        public static PayrollOvertimeCalculation Empty { get; } = new(0, false, false, 0, 0, 0);
    }
    private sealed record PayrollBonusLine(string Code, string Description, decimal Amount, bool Taxable, bool SeparateTax);
    private sealed record PayrollBonusTaxSplit(decimal NonTaxableAmount, decimal TaxableBonusAmount, decimal SeparateTaxableAmount, decimal SeparateTaxAmount);
    private sealed record PayrollBonusCalculation(
        decimal TotalAmount,
        decimal NonTaxableAmount,
        decimal TaxableBonusAmount,
        decimal SeparateTaxableAmount,
        decimal SeparateTaxAmount,
        IReadOnlyList<PayrollBonusLine> Lines)
    {
        public static PayrollBonusCalculation Empty { get; } = new(0, 0, 0, 0, 0, Array.Empty<PayrollBonusLine>());
    }

    private sealed record PayrollContributionArrearsSplit(
        decimal EmployeeSsfAmount,
        decimal EmployerSsfAmount,
        decimal OtherEmployeeContributionAmount,
        decimal OtherEmployerContributionAmount);

    private sealed record PayrollPromotionArrearsCalculation(
        decimal BasicAmount,
        decimal AllowanceAmount,
        decimal EmployeeContributionAmount,
        decimal EmployerContributionAmount,
        decimal OtherContributionAmount,
        decimal OtherEmployerContributionAmount,
        decimal TaxableIncomeAmount,
        decimal TaxAmount,
        decimal NetAmount)
    {
        public static PayrollPromotionArrearsCalculation Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
    }
}
