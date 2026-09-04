using ErpSystem.Core.DTOs.HR.Payroll;
using ErpSystem.Core.Entities;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Payroll;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Data;
using iText.Kernel.Pdf;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

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
    private const string BackpayDeductionArrearsTransactionType = "DER";
    private const string NetPayTransactionType = "NetPay";
    private const string PaymentModePercentage = "Percentage";
    private const string PaymentModeFixedAmount = "FixedAmount";
    private const string PayrollRunWorkflowEntityType = "PayrollRun";
    private const string PayrollRunWorkflowEntityCode = "PAYROLL_RUN";
    private const string PayrollNotificationModule = "Notifications";
    private const string PayrollNotificationCategory = "Payroll";
    private const string PayrollPayslipEmailTemplateName = "Payroll Payslip Email";
    private const string PayrollPayslipEmailEntityType = "PayrollPayslipEmail";
    private const string PayrollPayslipEmailTopicKey = PayrollPayslipEmailEntityType + ".PayslipEmail.Employee";
    private const string PayrollJournalSourceModule = "PAYROLL";
    private const string PayrollJournalNumberPrefix = "PAY-";
    private const decimal PayrollJournalBalanceTolerance = 0m;

    private sealed record PayrollJournalMappingSeed(
        int SequenceNo,
        string TransactionType,
        string? ComponentCode,
        string? ShortDescription,
        string Description,
        string DebitCredit,
        string AccountCode,
        string LegacyCompanyCode,
        string AccountType);

    private sealed record PayrollFinanceAccountSeed(
        string AccountNumber,
        string AccountCode,
        string AccountName,
        AccountType AccountType,
        string AccountCategory,
        string? AccountSubCategory,
        string Description);

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
        Menu("A0000124", "A00001", "Journal Setup", "Main Menu > Setup > Journal Setup", "Form", "PR3_026.fmb", "001", 24, true),
        Menu("A0000131", "A00001", "Budget Analysis", "Main Menu > Setup > Budget Analysis", "Form", "PR3_032.fmb", "DEMO", 31, true),
        Menu("A00002", "A000", "Payroll Process", "Main Menu > Payroll Process", "Folder", null, "001", 2),
        Menu("A0000212", "A00002", "Allowances & Ded Exception", "Main Menu > Payroll Process > Allowances & Ded Exception", "Form", "PR3_021.fmb", "001", 17, true),
        Menu("A0000215", "A00002", "Overtime Summary", "Main Menu > Payroll Process > Overtime Summary", "Form", "PR3_016.fmb", "001", 21, true),
        Menu("A0000217", "A00002", "Opening Balance", "Main Menu > Payroll Process > Opening Balance", "Form", "PR3_024.fmb", "BUCK", 23, true),
        Menu("A0000221", "A00002", "Contribution Transactions", "Main Menu > Payroll Process > Contribution Transactions", "Form", "PR3_030.fmb", "DEMO", 24, true),
        Menu("A0000225", "A00002", "Promotion Arrears Entry", "Main Menu > Payroll Process > Promotion Arrears Entry", "Form", "PR3_033.fmb", "001", 25, true)
    ];

    private readonly ApplicationDbContext _context;
    private readonly IJournalEntryService _journalEntryService;
    private readonly IFinancePostingEngine _financePostingEngine;
    private readonly IWorkflowIntegrationService _workflowIntegrationService;
    private readonly INotificationTopicPublisher _notificationTopicPublisher;
    private readonly ILogger<PayrollService> _logger;

    public PayrollService(
        ApplicationDbContext context,
        IJournalEntryService journalEntryService,
        IFinancePostingEngine financePostingEngine,
        IWorkflowIntegrationService workflowIntegrationService,
        INotificationTopicPublisher notificationTopicPublisher,
        ILogger<PayrollService> logger)
    {
        _context = context;
        _journalEntryService = journalEntryService;
        _financePostingEngine = financePostingEngine;
        _workflowIntegrationService = workflowIntegrationService;
        _notificationTopicPublisher = notificationTopicPublisher;
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
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
            .OrderBy(e => e.SequenceNo)
            .ThenBy(e => e.TransactionType)
            .ThenBy(e => e.ComponentCode)
            .ThenBy(e => e.DebitCredit)
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

    public async Task<PayrollBudgetAnalysisDto> BuildBudgetAnalysisAsync(
        Guid tenantId,
        PayrollBudgetAnalysisRequestDto dto,
        CancellationToken cancellationToken = default)
        => await BuildBudgetAnalysisAsync(tenantId, dto, loadSavedRows: true, cancellationToken);

    public async Task<PayrollBudgetAnalysisDto> SaveBudgetAnalysisAsync(
        Guid tenantId,
        PayrollBudgetAnalysisRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var companyCode = await ResolvePayrollCompanyCodeAsync(tenantId, cancellationToken);
        var submittedRows = dto.Rows ?? [];
        var result = submittedRows.Count > 0
            ? BuildBudgetAnalysisDtoFromRows(
                dto.PayPeriod > 0 ? dto.PayPeriod : submittedRows.First().PayPeriod,
                companyCode,
                dto,
                submittedRows)
            : await BuildBudgetAnalysisAsync(tenantId, dto, loadSavedRows: false, cancellationToken);

        await PersistBudgetAnalysisAsync(tenantId, result, cancellationToken);
        return result;
    }

    private async Task<PayrollBudgetAnalysisDto> BuildBudgetAnalysisAsync(
        Guid tenantId,
        PayrollBudgetAnalysisRequestDto dto,
        bool loadSavedRows,
        CancellationToken cancellationToken = default)
    {
        var payPeriod = await ResolveBudgetAnalysisPayPeriodAsync(tenantId, dto.PayPeriod, cancellationToken);
        var companyCode = await ResolvePayrollCompanyCodeAsync(tenantId, cancellationToken);
        if (payPeriod <= 0)
        {
            return new PayrollBudgetAnalysisDto
            {
                CompanyCode = companyCode,
                BasicPercent1 = RoundMoney(dto.BasicPercent1),
                BasicPercent2 = RoundMoney(dto.BasicPercent2),
                BasicPercent3 = RoundMoney(dto.BasicPercent3)
            };
        }

        var hasAdjustments = (dto.Adjustments ?? []).Count > 0;
        var hasScenarioInput = dto.BasicPercent1 != 0m || dto.BasicPercent2 != 0m || dto.BasicPercent3 != 0m;
        if (loadSavedRows && !hasAdjustments && !hasScenarioInput)
        {
            var savedRows = await _context.PayrollBudgetAnalysisRows
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId && e.CompanyCode == companyCode && e.PayPeriod == payPeriod)
                .OrderBy(e => e.OrderField)
                .ThenBy(e => e.Description)
                .ToListAsync(cancellationToken);
            if (savedRows.Count > 0)
            {
                return BuildBudgetAnalysisDtoFromRows(
                    payPeriod,
                    companyCode,
                    dto,
                    savedRows.Select(ToBudgetAnalysisRowDto).ToList());
            }
        }

        var runs = await _context.PayrollRuns
            .AsNoTracking()
            .Include(e => e.Employees)
            .Include(e => e.Transactions)
                .ThenInclude(e => e.PayrollComponent)
            .Where(e =>
                e.TenantId == tenantId &&
                e.PayPeriod == payPeriod &&
                e.Status != PayrollRunStatus.Draft &&
                e.Status != PayrollRunStatus.RolledBack)
            .OrderBy(e => e.RunDate)
            .ToListAsync(cancellationToken);

        var rowsByKey = new Dictionary<string, PayrollBudgetAnalysisAccumulator>(StringComparer.OrdinalIgnoreCase);
        foreach (var run in runs)
        {
            AddBudgetAnalysisBaseRows(rowsByKey, run, companyCode);
            foreach (var transaction in run.Transactions)
            {
                if (IsBudgetAnalysisAllowance(transaction))
                {
                    AddBudgetAnalysisAmount(
                        rowsByKey,
                        run,
                        companyCode,
                        2,
                        "ALW",
                        transaction.ComponentCode ?? transaction.TransactionType,
                        transaction.Description ?? transaction.PayrollComponent?.Name ?? transaction.ComponentCode ?? "Allowance",
                        transaction.Amount,
                        transaction.PayrollComponent?.CalculationType == PayrollCalculationType.PercentageOfBasic);
                }
                else if (IsBudgetAnalysisContribution(transaction))
                {
                    var amount = transaction.EmployerAmount ?? transaction.Amount;
                    AddBudgetAnalysisAmount(
                        rowsByKey,
                        run,
                        companyCode,
                        3,
                        "CON",
                        transaction.ComponentCode ?? transaction.TransactionType,
                        transaction.Description ?? transaction.PayrollComponent?.Name ?? transaction.ComponentCode ?? "Contribution",
                        amount,
                        transaction.PayrollComponent?.CalculationType == PayrollCalculationType.PercentageOfBasic);
                }
            }
        }

        var adjustments = (dto.Adjustments ?? []).ToDictionary(
            e => BuildBudgetAnalysisKey(e.OrderField, e.TransactionType, e.ActualTransaction),
            StringComparer.OrdinalIgnoreCase);
        var rows = rowsByKey.Values
            .OrderBy(e => e.OrderField)
            .ThenBy(e => e.Description)
            .Select(row =>
            {
                adjustments.TryGetValue(BuildBudgetAnalysisKey(row.OrderField, row.TransactionType, row.ActualTransaction), out var adjustment);
                return BuildBudgetAnalysisRow(row, dto, adjustment);
            })
            .ToList();

        var periodFrom = runs.Count == 0 ? (DateTime?)null : runs.Min(e => e.PayPeriodFrom);
        var periodTo = runs.Count == 0 ? (DateTime?)null : runs.Max(e => e.PayPeriodTo);

        return new PayrollBudgetAnalysisDto
        {
            PayPeriod = payPeriod,
            PayPeriodFrom = periodFrom,
            PayPeriodTo = periodTo,
            CompanyCode = companyCode,
            BasicPercent1 = RoundMoney(dto.BasicPercent1),
            BasicPercent2 = RoundMoney(dto.BasicPercent2),
            BasicPercent3 = RoundMoney(dto.BasicPercent3),
            Rows = rows,
            TotalBaseAmount = RoundMoney(rows.Sum(e => e.BaseAmount)),
            TotalNewAmount1 = RoundMoney(rows.Where(e => e.Include1).Sum(e => e.NewAmount1)),
            TotalNewAmount2 = RoundMoney(rows.Where(e => e.Include2).Sum(e => e.NewAmount2)),
            TotalNewAmount3 = RoundMoney(rows.Where(e => e.Include3).Sum(e => e.NewAmount3)),
            TotalVariance1 = RoundMoney(rows.Where(e => e.Include1).Sum(e => e.Variance1)),
            TotalVariance2 = RoundMoney(rows.Where(e => e.Include2).Sum(e => e.Variance2)),
            TotalVariance3 = RoundMoney(rows.Where(e => e.Include3).Sum(e => e.Variance3))
        };
    }

    private async Task<int> ResolveBudgetAnalysisPayPeriodAsync(Guid tenantId, int requestedPayPeriod, CancellationToken cancellationToken)
    {
        var payPeriod = requestedPayPeriod;
        if (payPeriod <= 0)
        {
            payPeriod = await _context.PayrollParameterSets
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId && e.IsActive && e.CurrentPayPeriod > 0)
                .OrderBy(e => e.Code)
                .Select(e => e.CurrentPayPeriod)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (payPeriod <= 0)
        {
            payPeriod = await _context.PayrollRuns
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId && e.Status != PayrollRunStatus.Draft && e.Status != PayrollRunStatus.RolledBack)
                .OrderByDescending(e => e.PayPeriod)
                .Select(e => e.PayPeriod)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return payPeriod;
    }

    private async Task PersistBudgetAnalysisAsync(Guid tenantId, PayrollBudgetAnalysisDto analysis, CancellationToken cancellationToken)
    {
        var companyCode = NormalizeBudgetAnalysisCode(analysis.CompanyCode, "001");
        var existingRows = await _context.PayrollBudgetAnalysisRows
            .Where(e => e.TenantId == tenantId && e.CompanyCode == companyCode && e.PayPeriod == analysis.PayPeriod)
            .ToListAsync(cancellationToken);

        _context.PayrollBudgetAnalysisRows.RemoveRange(existingRows);

        var periodFrom = analysis.PayPeriodFrom ?? analysis.Rows.FirstOrDefault(e => e.PayPeriodFrom.HasValue)?.PayPeriodFrom ?? DateTime.UtcNow.Date;
        var periodTo = analysis.PayPeriodTo ?? analysis.Rows.FirstOrDefault(e => e.PayPeriodTo.HasValue)?.PayPeriodTo ?? periodFrom;
        foreach (var row in analysis.Rows)
        {
            _context.PayrollBudgetAnalysisRows.Add(new PayrollBudgetAnalysisRow
            {
                TenantId = tenantId,
                OrderField = row.OrderField,
                PayPeriod = analysis.PayPeriod,
                PayPeriodFrom = row.PayPeriodFrom ?? periodFrom,
                PayPeriodTo = row.PayPeriodTo ?? periodTo,
                TransactionType = NormalizeBudgetAnalysisCode(row.TransactionType, "BUD"),
                ActualTransaction = NormalizeBudgetAnalysisCode(row.ActualTransaction, string.Empty),
                Description = TruncateText(row.Description, 200),
                Percentage = row.Percentage,
                BaseAmount = RoundMoney(row.BaseAmount),
                Amount1 = RoundMoney(row.Amount1),
                Include1 = row.Include1,
                NewAmount1 = RoundMoney(row.NewAmount1),
                Amount2 = RoundMoney(row.Amount2),
                Include2 = row.Include2,
                NewAmount2 = RoundMoney(row.NewAmount2),
                Amount3 = RoundMoney(row.Amount3),
                Include3 = row.Include3,
                NewAmount3 = RoundMoney(row.NewAmount3),
                CompanyCode = companyCode
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static PayrollBudgetAnalysisDto BuildBudgetAnalysisDtoFromRows(
        int payPeriod,
        string companyCode,
        PayrollBudgetAnalysisRequestDto request,
        IEnumerable<PayrollBudgetAnalysisRowDto> sourceRows)
    {
        var rows = sourceRows
            .Select(row => RecalculateBudgetAnalysisRow(row, payPeriod, companyCode))
            .OrderBy(e => e.OrderField)
            .ThenBy(e => e.Description)
            .ToList();
        var firstPercentageRow = rows.FirstOrDefault(e => e.Percentage);
        var basicPercent1 = request.BasicPercent1 != 0m ? RoundMoney(request.BasicPercent1) : RoundMoney(firstPercentageRow?.Amount1 ?? 0m);
        var basicPercent2 = request.BasicPercent2 != 0m ? RoundMoney(request.BasicPercent2) : RoundMoney(firstPercentageRow?.Amount2 ?? 0m);
        var basicPercent3 = request.BasicPercent3 != 0m ? RoundMoney(request.BasicPercent3) : RoundMoney(firstPercentageRow?.Amount3 ?? 0m);
        var periodFromValues = rows.Where(e => e.PayPeriodFrom.HasValue).Select(e => e.PayPeriodFrom!.Value).ToList();
        var periodToValues = rows.Where(e => e.PayPeriodTo.HasValue).Select(e => e.PayPeriodTo!.Value).ToList();

        return new PayrollBudgetAnalysisDto
        {
            PayPeriod = payPeriod,
            PayPeriodFrom = periodFromValues.Count == 0 ? null : periodFromValues.Min(),
            PayPeriodTo = periodToValues.Count == 0 ? null : periodToValues.Max(),
            CompanyCode = companyCode,
            BasicPercent1 = basicPercent1,
            BasicPercent2 = basicPercent2,
            BasicPercent3 = basicPercent3,
            Rows = rows,
            TotalBaseAmount = RoundMoney(rows.Sum(e => e.BaseAmount)),
            TotalNewAmount1 = RoundMoney(rows.Where(e => e.Include1).Sum(e => e.NewAmount1)),
            TotalNewAmount2 = RoundMoney(rows.Where(e => e.Include2).Sum(e => e.NewAmount2)),
            TotalNewAmount3 = RoundMoney(rows.Where(e => e.Include3).Sum(e => e.NewAmount3)),
            TotalVariance1 = RoundMoney(rows.Where(e => e.Include1).Sum(e => e.Variance1)),
            TotalVariance2 = RoundMoney(rows.Where(e => e.Include2).Sum(e => e.Variance2)),
            TotalVariance3 = RoundMoney(rows.Where(e => e.Include3).Sum(e => e.Variance3))
        };
    }

    private static PayrollBudgetAnalysisRowDto RecalculateBudgetAnalysisRow(PayrollBudgetAnalysisRowDto row, int payPeriod, string companyCode)
    {
        var baseAmount = RoundMoney(row.BaseAmount);
        var amount1 = RoundMoney(row.Amount1);
        var amount2 = RoundMoney(row.Amount2);
        var amount3 = RoundMoney(row.Amount3);
        var newAmount1 = CalculateBudgetAnalysisNewAmount(baseAmount, amount1);
        var newAmount2 = CalculateBudgetAnalysisNewAmount(baseAmount, amount2);
        var newAmount3 = CalculateBudgetAnalysisNewAmount(baseAmount, amount3);
        var variance1 = RoundMoney(newAmount1 - baseAmount);
        var variance2 = RoundMoney(newAmount2 - baseAmount);
        var variance3 = RoundMoney(newAmount3 - baseAmount);

        return new PayrollBudgetAnalysisRowDto
        {
            OrderField = row.OrderField,
            PayPeriod = payPeriod,
            PayPeriodFrom = row.PayPeriodFrom,
            PayPeriodTo = row.PayPeriodTo,
            TransactionType = NormalizeBudgetAnalysisCode(row.TransactionType, "BUD"),
            ActualTransaction = NormalizeBudgetAnalysisCode(row.ActualTransaction, string.Empty) is { Length: > 0 } actualTransaction ? actualTransaction : null,
            Description = TruncateText(row.Description, 200),
            Percentage = row.Percentage,
            BaseAmount = baseAmount,
            Amount1 = amount1,
            Include1 = row.Include1,
            NewAmount1 = newAmount1,
            Variance1 = variance1,
            Percent1 = CalculateBudgetAnalysisPercent(variance1, baseAmount),
            Amount2 = amount2,
            Include2 = row.Include2,
            NewAmount2 = newAmount2,
            Variance2 = variance2,
            Percent2 = CalculateBudgetAnalysisPercent(variance2, baseAmount),
            Amount3 = amount3,
            Include3 = row.Include3,
            NewAmount3 = newAmount3,
            Variance3 = variance3,
            Percent3 = CalculateBudgetAnalysisPercent(variance3, baseAmount),
            CompanyCode = companyCode
        };
    }

    private static PayrollBudgetAnalysisRowDto ToBudgetAnalysisRowDto(PayrollBudgetAnalysisRow row)
        => new()
        {
            OrderField = row.OrderField,
            PayPeriod = row.PayPeriod,
            PayPeriodFrom = row.PayPeriodFrom,
            PayPeriodTo = row.PayPeriodTo,
            TransactionType = row.TransactionType,
            ActualTransaction = string.IsNullOrWhiteSpace(row.ActualTransaction) ? null : row.ActualTransaction,
            Description = row.Description,
            Percentage = row.Percentage,
            BaseAmount = row.BaseAmount,
            Amount1 = row.Amount1,
            Include1 = row.Include1,
            NewAmount1 = row.NewAmount1,
            Amount2 = row.Amount2,
            Include2 = row.Include2,
            NewAmount2 = row.NewAmount2,
            Amount3 = row.Amount3,
            Include3 = row.Include3,
            NewAmount3 = row.NewAmount3,
            CompanyCode = row.CompanyCode
        };

    private static string NormalizeBudgetAnalysisCode(string? value, string fallback)
    {
        var text = TrimOrNull(value) ?? fallback;
        return text.Length <= 5 ? text : text[..5];
    }

    private static string TruncateText(string? value, int maxLength)
    {
        var text = TrimOrNull(value) ?? string.Empty;
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private async Task<string> ResolvePayrollCompanyCodeAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var parameterCompanyCode = TrimOrNull(await _context.PayrollParameterSets
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.IsActive)
            .OrderBy(e => e.Code)
            .Select(e => e.LegacyCompanyCode)
            .FirstOrDefaultAsync(cancellationToken));
        if (parameterCompanyCode is not null)
        {
            return parameterCompanyCode;
        }

        var profileCompanyCode = TrimOrNull(await _context.PayrollCompanyProfiles
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.IsActive)
            .OrderBy(e => e.CompanyId)
            .Select(e => e.CompanyCode ?? e.LegacyCompanyCode)
            .FirstOrDefaultAsync(cancellationToken));

        return profileCompanyCode ?? "001";
    }

    private static void AddBudgetAnalysisBaseRows(
        IDictionary<string, PayrollBudgetAnalysisAccumulator> rowsByKey,
        PayrollRun run,
        string companyCode)
    {
        var basicTransactions = run.Transactions
            .Where(e => e.TransactionType.Equals(BasicSalaryTransactionType, StringComparison.OrdinalIgnoreCase))
            .ToList();
        AddBudgetAnalysisAmount(
            rowsByKey,
            run,
            companyCode,
            1,
            "BAS",
            "BAS",
            "BASIC SALARY",
            basicTransactions.Count > 0 ? basicTransactions.Sum(e => e.Amount) : run.Employees.Sum(e => e.BasicSalary),
            percentage: true);

        var employerSsf = run.Transactions
            .Where(e => e.TransactionType.Equals(EmployerPensionTransactionType, StringComparison.OrdinalIgnoreCase))
            .Sum(e => e.EmployerAmount ?? e.Amount);
        AddBudgetAnalysisAmount(rowsByKey, run, companyCode, 1, "CSF", "CSF", "EMPLOYER SSF", employerSsf, percentage: true);

        var overtime = run.Transactions
            .Where(e => e.TransactionType.Equals(OvertimeTransactionType, StringComparison.OrdinalIgnoreCase))
            .Sum(e => e.Amount);
        AddBudgetAnalysisAmount(rowsByKey, run, companyCode, 1, "OVE", "OVE", "OVERTIME", overtime, percentage: true);
    }

    private static void AddBudgetAnalysisAmount(
        IDictionary<string, PayrollBudgetAnalysisAccumulator> rowsByKey,
        PayrollRun run,
        string companyCode,
        int orderField,
        string transactionType,
        string? actualTransaction,
        string description,
        decimal amount,
        bool percentage)
    {
        if (amount == 0m)
        {
            return;
        }

        var key = BuildBudgetAnalysisKey(orderField, transactionType, actualTransaction);
        if (!rowsByKey.TryGetValue(key, out var row))
        {
            row = new PayrollBudgetAnalysisAccumulator
            {
                OrderField = orderField,
                PayPeriod = run.PayPeriod,
                PayPeriodFrom = run.PayPeriodFrom,
                PayPeriodTo = run.PayPeriodTo,
                TransactionType = transactionType,
                ActualTransaction = actualTransaction,
                Description = description,
                Percentage = percentage,
                CompanyCode = companyCode
            };
            rowsByKey.Add(key, row);
        }

        row.BaseAmount += amount;
        row.Percentage = row.Percentage || percentage;
        row.PayPeriodFrom = row.PayPeriodFrom <= run.PayPeriodFrom ? row.PayPeriodFrom : run.PayPeriodFrom;
        row.PayPeriodTo = row.PayPeriodTo >= run.PayPeriodTo ? row.PayPeriodTo : run.PayPeriodTo;
    }

    private static bool IsBudgetAnalysisAllowance(PayrollTransaction transaction)
        => transaction.TransactionType.Equals(PayrollComponentType.Allowance.ToString(), StringComparison.OrdinalIgnoreCase) ||
           transaction.TransactionType.Equals("ALW", StringComparison.OrdinalIgnoreCase);

    private static bool IsBudgetAnalysisContribution(PayrollTransaction transaction)
    {
        if (transaction.TransactionType.Equals(EmployerPensionTransactionType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return transaction.TransactionType.Equals(PayrollComponentType.EmployerContribution.ToString(), StringComparison.OrdinalIgnoreCase) ||
               transaction.TransactionType.Equals(PayrollComponentType.EmployeeContribution.ToString(), StringComparison.OrdinalIgnoreCase) && transaction.EmployerAmount.GetValueOrDefault() != 0m ||
               transaction.TransactionType.Equals("CON", StringComparison.OrdinalIgnoreCase);
    }

    private static PayrollBudgetAnalysisRowDto BuildBudgetAnalysisRow(
        PayrollBudgetAnalysisAccumulator source,
        PayrollBudgetAnalysisRequestDto request,
        PayrollBudgetAnalysisRowAdjustmentDto? adjustment)
    {
        var amount1 = adjustment?.Amount1 ?? (source.Percentage ? request.BasicPercent1 : 0m);
        var amount2 = adjustment?.Amount2 ?? (source.Percentage ? request.BasicPercent2 : 0m);
        var amount3 = adjustment?.Amount3 ?? (source.Percentage ? request.BasicPercent3 : 0m);
        var newAmount1 = CalculateBudgetAnalysisNewAmount(source.BaseAmount, amount1);
        var newAmount2 = CalculateBudgetAnalysisNewAmount(source.BaseAmount, amount2);
        var newAmount3 = CalculateBudgetAnalysisNewAmount(source.BaseAmount, amount3);
        var variance1 = RoundMoney(newAmount1 - source.BaseAmount);
        var variance2 = RoundMoney(newAmount2 - source.BaseAmount);
        var variance3 = RoundMoney(newAmount3 - source.BaseAmount);

        return new PayrollBudgetAnalysisRowDto
        {
            OrderField = source.OrderField,
            PayPeriod = source.PayPeriod,
            PayPeriodFrom = source.PayPeriodFrom,
            PayPeriodTo = source.PayPeriodTo,
            TransactionType = source.TransactionType,
            ActualTransaction = source.ActualTransaction,
            Description = source.Description,
            Percentage = source.Percentage,
            BaseAmount = RoundMoney(source.BaseAmount),
            Amount1 = RoundMoney(amount1),
            Include1 = adjustment?.Include1 ?? true,
            NewAmount1 = newAmount1,
            Variance1 = variance1,
            Percent1 = CalculateBudgetAnalysisPercent(variance1, source.BaseAmount),
            Amount2 = RoundMoney(amount2),
            Include2 = adjustment?.Include2 ?? true,
            NewAmount2 = newAmount2,
            Variance2 = variance2,
            Percent2 = CalculateBudgetAnalysisPercent(variance2, source.BaseAmount),
            Amount3 = RoundMoney(amount3),
            Include3 = adjustment?.Include3 ?? true,
            NewAmount3 = newAmount3,
            Variance3 = variance3,
            Percent3 = CalculateBudgetAnalysisPercent(variance3, source.BaseAmount),
            CompanyCode = source.CompanyCode
        };
    }

    private static decimal CalculateBudgetAnalysisNewAmount(decimal baseAmount, decimal amount)
        => RoundMoney(baseAmount + baseAmount * amount / 100m);

    private static decimal CalculateBudgetAnalysisPercent(decimal variance, decimal baseAmount)
        => baseAmount == 0m ? 0m : RoundMoney(variance / baseAmount * 100m);

    private static string BuildBudgetAnalysisKey(int orderField, string transactionType, string? actualTransaction)
        => string.Join("|", orderField.ToString(CultureInfo.InvariantCulture), transactionType.Trim().ToUpperInvariant(), (actualTransaction ?? string.Empty).Trim().ToUpperInvariant());

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

    public async Task<IReadOnlyList<PayrollBonusRuleDto>> SaveBonusRulesAsync(
        Guid tenantId,
        PayrollBonusRuleBulkSaveDto dto,
        CancellationToken cancellationToken = default)
    {
        var bonusCode = NormalizeCode(dto.BonusCode);
        var policyExists = await _context.PayrollBonusPolicies
            .AsNoTracking()
            .AnyAsync(e => e.TenantId == tenantId && e.Code == bonusCode, cancellationToken);
        if (!policyExists)
        {
            throw new KeyNotFoundException("Payroll bonus policy was not found.");
        }

        var existingRows = await _context.PayrollBonusRules
            .Where(e => e.TenantId == tenantId && e.BonusCode == bonusCode)
            .ToListAsync(cancellationToken);

        var incomingRows = dto.Rules
            .Select(e => new
            {
                Line = e,
                GroupCode = TrimOrNull(e.GroupCode)
            })
            .Where(e => e.GroupCode != null)
            .GroupBy(e => e.GroupCode!, StringComparer.OrdinalIgnoreCase)
            .Select(e => e.Last())
            .ToList();

        var incomingCodes = incomingRows
            .Select(e => e.GroupCode!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var incomingIds = incomingRows
            .Select(e => e.Line.Id)
            .Where(e => e != Guid.Empty)
            .ToHashSet();

        foreach (var existing in existingRows.Where(e => !incomingCodes.Contains(e.GroupCode) && !incomingIds.Contains(e.Id)).ToList())
        {
            _context.PayrollBonusRules.Remove(existing);
        }

        foreach (var incoming in incomingRows)
        {
            var line = incoming.Line;
            var groupCode = incoming.GroupCode!;
            var entity = existingRows.FirstOrDefault(e =>
                    line.Id != Guid.Empty && e.Id == line.Id)
                ?? existingRows.FirstOrDefault(e => e.GroupCode.Equals(groupCode, StringComparison.OrdinalIgnoreCase))
                ?? new PayrollBonusRule { TenantId = tenantId };

            entity.BonusCode = bonusCode;
            entity.GroupCode = groupCode;
            entity.CalculationType = line.CalculationType;
            entity.Amount = line.Amount;
            entity.Applicable = line.Applicable;
            entity.LegacyCompanyCode = NormalizeOptionalLegacyCode(line.LegacyCompanyCode ?? dto.LegacyCompanyCode, 5);

            AddIfNew(_context.PayrollBonusRules, entity);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var savedRows = await _context.PayrollBonusRules
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.BonusCode == bonusCode)
            .OrderBy(e => e.GroupCode)
            .ToListAsync(cancellationToken);

        return savedRows.Select(ToDto).ToList();
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

        entity.SequenceNo = dto.SequenceNo > 0 ? dto.SequenceNo : await NextPayrollJournalMappingSequenceAsync(tenantId, cancellationToken);
        entity.TransactionType = RequireText(dto.TransactionType, nameof(dto.TransactionType)).Trim();
        entity.ComponentCode = string.IsNullOrWhiteSpace(dto.ComponentCode) ? null : NormalizeCode(dto.ComponentCode);
        entity.ShortDescription = TrimOrNull(dto.ShortDescription);
        entity.Description = RequireText(dto.Description, nameof(dto.Description));
        entity.DebitCredit = NormalizeDebitCredit(dto.DebitCredit);
        entity.AccountCode = RequireText(dto.AccountCode, nameof(dto.AccountCode)).Trim();
        entity.AccountType = string.IsNullOrWhiteSpace(dto.AccountType) ? null : dto.AccountType.Trim();
        entity.LegacyCompanyCode = NormalizeOptionalLegacyCode(dto.LegacyCompanyCode, 5);
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
        var hasRepaymentSplit = dto.PrincipalAmount.HasValue || dto.InterestAmount.HasValue;
        var requestedPrincipal = hasRepaymentSplit ? Math.Round(dto.PrincipalAmount.GetValueOrDefault(), 2) : 0m;
        var requestedInterest = hasRepaymentSplit ? Math.Round(dto.InterestAmount.GetValueOrDefault(), 2) : 0m;
        if (requestedPrincipal < 0 || requestedInterest < 0)
        {
            throw new InvalidOperationException("Principal and interest amounts cannot be negative.");
        }

        var repaymentAmount = hasRepaymentSplit
            ? Math.Round(requestedPrincipal + requestedInterest, 2)
            : Math.Round(dto.RepaymentAmount, 2);
        if (repaymentAmount <= 0)
        {
            throw new InvalidOperationException("Repayment Amount must be greater than zero.");
        }

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
        decimal principalPaid;
        decimal interestPaid;
        if (hasRepaymentSplit)
        {
            if (requestedPrincipal > principalDue)
            {
                throw new InvalidOperationException($"Principal amount cannot exceed the scheduled principal balance of {principalDue:N2}.");
            }

            if (requestedInterest > interestDue)
            {
                throw new InvalidOperationException($"Interest amount cannot exceed the scheduled interest balance of {interestDue:N2}.");
            }

            principalPaid = requestedPrincipal;
            interestPaid = requestedInterest;
        }
        else
        {
            principalPaid = Math.Min(repaymentAmount, principalDue);
            interestPaid = Math.Min(Math.Round(repaymentAmount - principalPaid, 2), interestDue);
        }

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
            .Where(e => e.TenantId == tenantId && e.IsActive);

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

    public async Task<IReadOnlyList<PayrollJournalMappingDto>> SeedOracleJournalMappingsAsync(
        Guid tenantId,
        string? legacyCompanyCode = null,
        CancellationToken cancellationToken = default)
    {
        var companyCode = NormalizeOptionalLegacyCode(legacyCompanyCode, 5)
            ?? await _context.PayrollParameterSets
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId && e.IsActive)
                .OrderBy(e => e.Code)
                .Select(e => e.LegacyCompanyCode)
                .FirstOrDefaultAsync(cancellationToken)
            ?? "001";

        await EnsurePayrollFinanceAccountsAsync(tenantId, cancellationToken);

        var accounts = await _context.Accounts
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted)
            .ToListAsync(cancellationToken);
        var availableAccountCodes = accounts
            .SelectMany(e => new[]
            {
                NormalizePayrollJournalAccountCode(e.AccountCode),
                NormalizePayrollJournalAccountCode(e.AccountNumber)
            })
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var salaryExpenseAccount = PickPayrollSeedAccount(accounts, ["6020", "001-000-6020", "6000", "001-000-6000"], "6020");
        var accruedPayableAccount = PickPayrollSeedAccount(accounts, ["2120", "001-000-2120", "2100", "001-000-2100", "2000", "001-000-2000"], "2120");
        var bankAccount = PickPayrollSeedAccount(accounts, ["1010", "001-000-1010", "1000", "001-000-1000"], "1010");
        var loanReceivableAccount = PickPayrollSeedAccount(accounts, ["1120", "001-000-1120", "1100", "001-000-1100"], accruedPayableAccount);
        var otherIncomeAccount = PickPayrollSeedAccount(accounts, ["4920", "001-000-4920", "4900", "001-000-4900", "4100", "001-000-4100"], accruedPayableAccount);

        var seeds = BuildOracleJournalMappingSeeds(
            companyCode,
            salaryExpenseAccount,
            accruedPayableAccount,
            bankAccount,
            loanReceivableAccount,
            otherIncomeAccount);

        var codeValues = await _context.PayrollCodeValues
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId &&
                        !e.Blocked &&
                        (e.LegacyCompanyCode == null || e.LegacyCompanyCode == companyCode))
            .OrderBy(e => e.CodeType)
            .ThenBy(e => e.ActualCode)
            .ToListAsync(cancellationToken);
        AddOracleComponentJournalMappingSeeds(
            seeds,
            codeValues,
            companyCode,
            availableAccountCodes,
            salaryExpenseAccount,
            accruedPayableAccount,
            loanReceivableAccount,
            otherIncomeAccount);

        foreach (var seed in seeds)
        {
            await UpsertPayrollJournalMappingSeedAsync(tenantId, seed, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var mappings = await _context.PayrollJournalMappings
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId &&
                        (e.LegacyCompanyCode == null || e.LegacyCompanyCode == companyCode))
            .OrderBy(e => e.SequenceNo)
            .ThenBy(e => e.TransactionType)
            .ThenBy(e => e.ComponentCode)
            .ThenBy(e => e.DebitCredit)
            .ToListAsync(cancellationToken);
        return mappings.Select(ToDto).ToList();
    }

    private async Task EnsurePayrollFinanceAccountsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var seeds = BuildPayrollFinanceAccountSeeds();
        var existingAccounts = await _context.Accounts
            .Where(e => e.TenantId == tenantId && !e.IsDeleted)
            .ToListAsync(cancellationToken);
        var existingByCode = existingAccounts
            .SelectMany(e => new[]
            {
                new { Code = NormalizePayrollJournalAccountCode(e.AccountCode), Account = e },
                new { Code = NormalizePayrollJournalAccountCode(e.AccountNumber), Account = e }
            })
            .Where(e => e.Code.Length > 0)
            .GroupBy(e => e.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => e.First().Account, StringComparer.OrdinalIgnoreCase);

        foreach (var seed in seeds)
        {
            var code = NormalizePayrollJournalAccountCode(seed.AccountCode);
            var number = NormalizePayrollJournalAccountCode(seed.AccountNumber);
            var account = existingByCode.GetValueOrDefault(code) ??
                          existingByCode.GetValueOrDefault(number);

            if (account == null)
            {
                account = new Account
                {
                    TenantId = tenantId,
                    AccountCode = seed.AccountCode,
                    AccountNumber = seed.AccountNumber,
                    AccountName = seed.AccountName,
                    AccountType = seed.AccountType,
                    CurrencyCode = "GHS",
                    IsSegmented = true,
                    AllowDirectPosting = true,
                    IsSystemAccount = true,
                    Status = AccountStatus.Active,
                    ReferenceNumber = seed.AccountCode,
                    EffectiveDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = PayrollJournalSourceModule
                };
                _context.Accounts.Add(account);
            }

            account.AccountCategory = seed.AccountCategory;
            account.AccountSubCategory = seed.AccountSubCategory;
            account.AccountNumber = seed.AccountNumber;
            account.Description = seed.Description;
            account.IsSegmented = true;
            account.AllowDirectPosting = true;
            account.IsSystemAccount = true;
            account.Status = AccountStatus.Active;
            account.UpdatedAt = DateTime.UtcNow;
            account.UpdatedBy = PayrollJournalSourceModule;

            existingByCode[code] = account;
            existingByCode[number] = account;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<PayrollFinanceAccountSeed> BuildPayrollFinanceAccountSeeds()
        =>
        [
            new("000-1010-0000", "1010", "Cash and Bank - Payroll Clearing", AccountType.Asset, "Current Assets", "Cash and Bank", "Default bank and cash clearing account used by payroll net pay journals."),
            new("000-1120-0000", "1120", "Staff Loans and Salary Advances", AccountType.Asset, "Current Assets", "Employee Receivables", "Receivable account for staff loan repayments, salary advances, and related payroll recoveries."),
            new("000-2120-0000", "2120", "Accrued Payroll Payables", AccountType.Liability, "Current Liabilities", "Payroll Payables", "Default liability account for accrued payroll deductions, taxes, pensions, and contribution payables."),
            new("000-4920-0000", "4920", "Payroll Recoveries and Interest Income", AccountType.Revenue, "Other Income", "Payroll Recoveries", "Income account for payroll loan interest and recoveries credited from payroll runs."),
            new("000-6020-0000", "6020", "Salaries, Wages and Payroll Costs", AccountType.Expense, "Operating Expenses", "Payroll Costs", "Default payroll expense account for basic salary, allowances, overtime, employer contributions, and arrears.")
        ];

    private async Task<int> NextPayrollJournalMappingSequenceAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var maxSequence = await _context.PayrollJournalMappings
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .Select(e => (int?)e.SequenceNo)
            .MaxAsync(cancellationToken);
        return maxSequence.GetValueOrDefault() + 1;
    }

    private async Task UpsertPayrollJournalMappingSeedAsync(
        Guid tenantId,
        PayrollJournalMappingSeed seed,
        CancellationToken cancellationToken)
    {
        var componentCode = TrimOrNull(seed.ComponentCode);
        var entity = await _context.PayrollJournalMappings
            .FirstOrDefaultAsync(e =>
                e.TenantId == tenantId &&
                e.TransactionType == seed.TransactionType &&
                e.ComponentCode == componentCode &&
                e.DebitCredit == seed.DebitCredit &&
                e.LegacyCompanyCode == seed.LegacyCompanyCode,
                cancellationToken)
            ?? new PayrollJournalMapping { TenantId = tenantId };

        entity.SequenceNo = seed.SequenceNo;
        entity.TransactionType = seed.TransactionType;
        entity.ComponentCode = componentCode;
        entity.ShortDescription = seed.ShortDescription;
        entity.Description = seed.Description;
        entity.DebitCredit = seed.DebitCredit;
        entity.AccountCode = seed.AccountCode;
        entity.AccountType = seed.AccountType;
        entity.LegacyCompanyCode = seed.LegacyCompanyCode;
        entity.IsActive = true;

        AddIfNew(_context.PayrollJournalMappings, entity);
    }

    private static List<PayrollJournalMappingSeed> BuildOracleJournalMappingSeeds(
        string companyCode,
        string salaryExpenseAccount,
        string accruedPayableAccount,
        string bankAccount,
        string loanReceivableAccount,
        string otherIncomeAccount)
    {
        return
        [
            JournalSeed(10, BonusTransactionType, null, "BONUS", "BONUS", "DR", salaryExpenseAccount, companyCode, "Expense"),
            JournalSeed(20, "BAS", null, "BASIC", "GROSS BASIC SALARY", "DR", salaryExpenseAccount, companyCode, "Expense"),
            JournalSeed(30, "ALW", null, "ALLOWANCES", "ALLOWANCES", "DR", salaryExpenseAccount, companyCode, "Expense"),
            JournalSeed(40, "BEN", null, "BENEFITS", "BENEFITS", "DR", salaryExpenseAccount, companyCode, "Expense"),
            JournalSeed(50, OvertimeTransactionType, OvertimeTransactionType, "OVERTIME", "OVERTIME", "DR", salaryExpenseAccount, companyCode, "Expense"),
            JournalSeed(60, PromotionBasicArrearsTransactionType, null, "BASIC ARREARS", "BASIC ARREARS", "DR", salaryExpenseAccount, companyCode, "Expense"),
            JournalSeed(70, PromotionAllowanceArrearsTransactionType, null, "ALLOWANCE ARREARS", "ALLOWANCE ARREARS", "DR", salaryExpenseAccount, companyCode, "Expense"),
            JournalSeed(80, "DED", null, "DEDUCTIONS", "DEDUCTIONS", "CR", accruedPayableAccount, companyCode, "Payable"),
            JournalSeed(90, "TAX", null, "PAYE", "INCOME TAX", "CR", accruedPayableAccount, companyCode, "Payable"),
            JournalSeed(100, "TXR", null, "PAYE ARREARS", "PAYE ARREARS", "CR", accruedPayableAccount, companyCode, "Payable"),
            JournalSeed(110, "ESF", null, "EMPL SSF", "EMPLOYEE SSF CONTRIBUTION", "CR", accruedPayableAccount, companyCode, "Payable"),
            JournalSeed(120, "CSF", null, "COMP SSF", "EMPLOYER SSF CONTRIBUTION", "DR", salaryExpenseAccount, companyCode, "Expense"),
            JournalSeed(121, "CSF", null, "CEMPL SSF", "EMPLOYER SSF PAY OUT", "CR", accruedPayableAccount, companyCode, "Payable"),
            JournalSeed(130, "CON", null, "EMPLOYEE CON", "EMPLOYEE PF PAYOUT", "CR", accruedPayableAccount, companyCode, "Payable"),
            JournalSeed(140, "ECO", null, "EMPLOYER CON", "EMPLOYER PF COST", "DR", salaryExpenseAccount, companyCode, "Expense"),
            JournalSeed(141, "ECO", null, "EMPLOYER CON", "EMPLOYER PF PAYOUT", "CR", accruedPayableAccount, companyCode, "Payable"),
            JournalSeed(150, PromotionEmployeeContributionArrearsTransactionType, null, "EMPL SSF ARREARS", "STAFF SSF PAYOUT ARREARS", "CR", accruedPayableAccount, companyCode, "Payable"),
            JournalSeed(160, PromotionEmployerContributionArrearsTransactionType, null, "COMP SSF ARREARS", "COMPANY SSF CON. ARREARS", "DR", salaryExpenseAccount, companyCode, "Expense"),
            JournalSeed(161, PromotionEmployerContributionArrearsTransactionType, null, "CEMPL SSF ARREARS", "EMPLOYER SSF PAY OUT ARREARS", "CR", accruedPayableAccount, companyCode, "Payable"),
            JournalSeed(170, "ECR", null, "EMPLOYER CON ARR", "EMPLOYER PF COST ARREARS", "DR", salaryExpenseAccount, companyCode, "Expense"),
            JournalSeed(171, "ECR", null, "EMPLOYER CON ARR", "EMPLOYER PF PAYOUT ARREARS", "CR", accruedPayableAccount, companyCode, "Payable"),
            JournalSeed(180, BackpayDeductionArrearsTransactionType, null, "DEDUCTION ARREARS", "DEDUCTION ARREARS", "CR", accruedPayableAccount, companyCode, "Payable"),
            JournalSeed(190, AbsenceTransactionType, AbsenceTransactionType, "ABSENCE", "ABSENCE DEDUCTION", "CR", accruedPayableAccount, companyCode, "Payable"),
            JournalSeed(200, LoanRepaymentTransactionType, null, "LOAN REPAYMENT", "LOAN REPAYMENT", "CR", loanReceivableAccount, companyCode, "Receivable"),
            JournalSeed(210, LoanInterestTransactionType, null, "LOAN INTEREST", "INTEREST ON LOAN", "CR", otherIncomeAccount, companyCode, "Income"),
            JournalSeed(220, SalaryAdvanceTransactionType, SalaryAdvanceTransactionType, "SALARY ADVANCE", "SALARY ADVANCE", "CR", loanReceivableAccount, companyCode, "Receivable"),
            JournalSeed(230, "BAN", null, "BANK PAYMENT", "NET PAYMENT - BANK", "CR", bankAccount, companyCode, "Bank/Cash"),
            JournalSeed(240, "CAS", null, "CASH PAYMENT", "NET PAYMENT - CASH", "CR", bankAccount, companyCode, "Bank/Cash")
        ];
    }

    private static void AddOracleComponentJournalMappingSeeds(
        ICollection<PayrollJournalMappingSeed> seeds,
        IReadOnlyList<PayrollCodeValue> codeValues,
        string companyCode,
        ISet<string> availableAccountCodes,
        string salaryExpenseAccount,
        string accruedPayableAccount,
        string loanReceivableAccount,
        string otherIncomeAccount)
    {
        var nextSequence = seeds.Max(e => e.SequenceNo) + 10;
        foreach (var codeValue in codeValues)
        {
            var codeType = NormalizeCode(codeValue.CodeType);
            var actualCode = NormalizeCode(codeValue.ActualCode);
            var description = TrimOrNull(codeValue.Description) ?? $"{codeType} {actualCode}";

            switch (codeType)
            {
                case "ALW":
                    seeds.Add(JournalSeed(nextSequence++, "ALW", actualCode, description, description, "DR", ExistingOrFallbackAccount(codeValue.AccountCode, salaryExpenseAccount, availableAccountCodes), companyCode, "Expense"));
                    break;
                case "BEN":
                    seeds.Add(JournalSeed(nextSequence++, "BEN", actualCode, description, description, "DR", ExistingOrFallbackAccount(codeValue.AccountCode, salaryExpenseAccount, availableAccountCodes), companyCode, "Expense"));
                    break;
                case "BON":
                    seeds.Add(JournalSeed(nextSequence++, BonusTransactionType, actualCode, description, description, "DR", ExistingOrFallbackAccount(codeValue.AccountCode, salaryExpenseAccount, availableAccountCodes), companyCode, "Expense"));
                    break;
                case "DED":
                    seeds.Add(JournalSeed(nextSequence++, "DED", actualCode, description, description, "CR", ExistingOrFallbackAccount(codeValue.AccountCode, accruedPayableAccount, availableAccountCodes), companyCode, "Payable"));
                    break;
                case "CON":
                    seeds.Add(JournalSeed(nextSequence++, "CON", actualCode, description, description, "CR", ExistingOrFallbackAccount(codeValue.AccountCode, accruedPayableAccount, availableAccountCodes), companyCode, "Payable"));
                    seeds.Add(JournalSeed(nextSequence++, "ECO", actualCode, $"EMPLOYER {description}", $"EMPLOYER {description}", "DR", salaryExpenseAccount, companyCode, "Expense"));
                    seeds.Add(JournalSeed(nextSequence++, "ECO", actualCode, $"EMPLOYER {description}", $"EMPLOYER {description}", "CR", ExistingOrFallbackAccount(codeValue.AccountCode, accruedPayableAccount, availableAccountCodes), companyCode, "Payable"));
                    break;
                case "LOA":
                    seeds.Add(JournalSeed(nextSequence++, LoanRepaymentTransactionType, actualCode, description, description, "CR", ExistingOrFallbackAccount(codeValue.AccountCode, loanReceivableAccount, availableAccountCodes), companyCode, "Receivable"));
                    seeds.Add(JournalSeed(nextSequence++, LoanInterestTransactionType, actualCode, $"{description} INT", $"{description} INTEREST", "CR", otherIncomeAccount, companyCode, "Income"));
                    break;
            }
        }
    }

    private static PayrollJournalMappingSeed JournalSeed(
        int sequenceNo,
        string transactionType,
        string? componentCode,
        string shortDescription,
        string description,
        string debitCredit,
        string accountCode,
        string companyCode,
        string accountType)
        => new(
            sequenceNo,
            transactionType,
            TrimOrNull(componentCode),
            LimitText(TrimOrNull(shortDescription), 30),
            LimitText(description, 120) ?? description,
            NormalizeDebitCredit(debitCredit),
            accountCode,
            companyCode,
            LimitText(accountType, 120) ?? accountType);

    private static string? LimitText(string? value, int maxLength)
    {
        var trimmed = TrimOrNull(value);
        return trimmed == null || trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string ExistingOrFallbackAccount(string? candidate, string fallback, ISet<string> availableAccountCodes)
    {
        var normalized = NormalizePayrollJournalAccountCode(candidate);
        return normalized.Length > 0 && availableAccountCodes.Contains(normalized) ? normalized : fallback;
    }

    private static string PickPayrollSeedAccount(IReadOnlyList<Account> accounts, IReadOnlyList<string> preferredCodes, string fallback)
    {
        foreach (var preferredCode in preferredCodes.Select(NormalizePayrollJournalAccountCode).Where(e => e.Length > 0))
        {
            var account = accounts.FirstOrDefault(e =>
                NormalizePayrollJournalAccountCode(e.AccountCode).Equals(preferredCode, StringComparison.OrdinalIgnoreCase) ||
                NormalizePayrollJournalAccountCode(e.AccountNumber).Equals(preferredCode, StringComparison.OrdinalIgnoreCase));
            if (account != null)
            {
                return TrimOrNull(account.AccountCode) ?? TrimOrNull(account.AccountNumber) ?? fallback;
            }
        }

        return fallback;
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
            var basicSalary = line.BasicSalary.GetValueOrDefault();
            entity.BasicSalary = basicSalary > 0
                ? Math.Round(basicSalary, 2)
                : CalculateBasicSalary(profile.SalaryBasis);
            entity.WorkingDays = line.WorkingDays.HasValue
                ? NormalizeNonNegative(line.WorkingDays, "Working days", 4)
                : null;
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

        var bonusPolicyCodes = bonusPolicies
            .Select(e => NormalizeMatchToken(e.Code))
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var bonusRules = bonusPolicyCodes.Count == 0
            ? new List<PayrollBonusRule>()
            : (await _context.PayrollBonusRules
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId)
                .OrderBy(e => e.BonusCode)
                .ThenBy(e => e.GroupCode)
                .ToListAsync(cancellationToken))
            .Where(e => bonusPolicyCodes.Contains(NormalizeMatchToken(e.BonusCode)))
            .ToList();

        var bonusExceptions = await _context.PayrollBonusExceptions
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .OrderBy(e => e.BonusCode)
            .ThenBy(e => e.EmployeeNumber)
            .ToListAsync(cancellationToken);

        var bonusRulesByCode = bonusRules
            .GroupBy(e => NormalizeMatchToken(e.BonusCode), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => (IReadOnlyList<PayrollBonusRule>)e.ToList(), StringComparer.OrdinalIgnoreCase);

        var bonusExceptionsByCode = bonusExceptions
            .GroupBy(e => NormalizeMatchToken(e.BonusCode), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => (IReadOnlyList<PayrollBonusException>)e.ToList(), StringComparer.OrdinalIgnoreCase);

        var backpayPolicies = await _context.PayrollBackpayPolicies
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId &&
                        (!e.EffectiveDate.HasValue || e.EffectiveDate.Value.Date <= run.PayPeriodTo.Date))
            .OrderBy(e => e.OperationType)
            .ThenBy(e => e.CategoryType)
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

        var backpayRulesByOperation = backpayRules
            .GroupBy(e => NormalizeBackpayOperation(e.OperationType), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => (IReadOnlyList<PayrollBackpayRule>)e.ToList(), StringComparer.OrdinalIgnoreCase);

        var backpayExceptionsByOperation = backpayExceptions
            .GroupBy(e => NormalizeBackpayOperation(e.OperationType), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(e => e.Key, e => (IReadOnlyList<PayrollBackpayException>)e.ToList(), StringComparer.OrdinalIgnoreCase);

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
        if (promotionEntries.Count > 0 || backpayPolicies.Count > 0)
        {
            var historyStartDates = new List<DateTime>();
            if (promotionEntries.Count > 0)
            {
                var earliestPromotionDate = promotionEntries.Min(e => e.EffectiveDate.Date);
                historyStartDates.Add(new DateTime(earliestPromotionDate.Year, earliestPromotionDate.Month, 1));
            }

            historyStartDates.AddRange(backpayPolicies.Select(e => ResolveBackpayHistoryStart(e, run.PayPeriodFrom)));
            var earliestHistoryDate = historyStartDates.Min();
            var historicalRunEmployees = await _context.PayrollRunEmployees
                .AsNoTracking()
                .Include(e => e.PayrollRun)
                .Include(e => e.Transactions)
                    .ThenInclude(e => e.PayrollComponent)
                .Where(e => e.TenantId == tenantId &&
                            e.PayrollRun.PayPeriod < run.PayPeriod &&
                            e.PayrollRun.PayPeriodFrom.Date >= earliestHistoryDate &&
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
            var previousYearBonus = previousBonusByEmployeeNumber.GetValueOrDefault(profile.EmployeeNumber);
            var annualBasicSalary = CalculateAnnualBasicForBonusTax(
                originalBasicSalary,
                historicalBasicByEmployeeNumber.GetValueOrDefault(profile.EmployeeNumber),
                run.PayPeriodFrom);
            var bonusCalculation = CalculatePayrollBonuses(
                bonusPolicies,
                bonusRulesByCode,
                bonusExceptionsByCode,
                profile,
                basicSalary,
                annualBasicSalary,
                previousYearBonus,
                run.PayPeriodTo,
                run.CurrencyCode,
                activeParameters);
            var benefitPercentageBase = basicSalary + bonusCalculation.TaxableBonusAmount;

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

                var componentLines = ResolveComponents(defaultComponents, componentRulesByKey, profile, profile.EmployeeComponents, run.PayPeriodFrom, run.PayPeriodTo)
                    .OrderBy(e => e.Component.ComponentType == PayrollComponentType.Benefit ? 1 : 0)
                    .ToList();

                foreach (var componentLine in componentLines)
                {
                    var component = componentLine.Component;
                    var percentageBase = component.ComponentType == PayrollComponentType.Benefit
                        ? benefitPercentageBase
                        : basicSalary;
                    var amount = CalculateComponentAmount(componentLine, basicSalary, percentageBase);
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
                            if (component.ApplyToBenefit)
                            {
                                benefitPercentageBase += amount;
                            }

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
                : CalculateEmployeeTaxRelief(employeeTaxReliefsByProfile.GetValueOrDefault(profile.Id) ?? [], basicSalary);
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
                var promotionBasicSalary = promotionEntry.BasicSalary.GetValueOrDefault() > 0m
                    ? Math.Round(promotionEntry.BasicSalary.Value, 2)
                    : basicSalary;
                var arrears = CalculatePromotionArrears(
                    promotionEntry,
                    historicalRunEmployees,
                    promotionBasicSalary,
                    taxableAllowances + nonTaxableAllowances,
                    currentContributionArrearsSplit,
                    taxableIncome,
                    normalIncomeTax,
                    activeParameters);

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

            if (!run.IsSeparateBonusRun &&
                backpayPolicies.Count > 0 &&
                historicalRunEmployeesByNumber.TryGetValue(profile.EmployeeNumber, out var backpayHistory))
            {
                var backpayArrears = CalculateSetupBackpayArrears(
                    backpayPolicies,
                    backpayRulesByOperation,
                    backpayExceptionsByOperation,
                    backpayHistory,
                    profile,
                    taxBands,
                    pensionScheme,
                    activeParameters,
                    run.PayPeriodFrom);

                if (backpayArrears.NetAmount > 0)
                {
                    ApplyPromotionArrearsTransactions(
                        tenantId,
                        run,
                        runEmployee,
                        profile,
                        employeeTransactions,
                        backpayArrears,
                        "Salary back pay");

                    taxableAllowances += backpayArrears.BasicAmount + backpayArrears.AllowanceAmount;
                    nonTaxableDeductions += backpayArrears.DeductionAmount;
                    employeeContribution += backpayArrears.EmployeeContributionAmount + backpayArrears.OtherContributionAmount;
                    employerContribution += backpayArrears.EmployerContributionAmount + backpayArrears.OtherEmployerContributionAmount;
                    taxableIncome += backpayArrears.TaxableIncomeAmount;
                    incomeTax += backpayArrears.TaxAmount;
                    grossIncome += backpayArrears.BasicAmount + backpayArrears.AllowanceAmount;
                    netIncomeBeforeTax += backpayArrears.BasicAmount + backpayArrears.AllowanceAmount - backpayArrears.EmployeeContributionAmount - backpayArrears.OtherContributionAmount - backpayArrears.DeductionAmount;
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
                employeeTransactions.Add(CreateTransaction(
                    tenantId,
                    run,
                    runEmployee,
                    profile,
                    SalaryAdvanceTransactionType,
                    "Salary advance",
                    salaryAdvance,
                    taxable: true,
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

        var journalLines = await BuildJournalLinesAsync(tenantId, run, transactions, cancellationToken);
        _context.PayrollJournalLines.AddRange(journalLines);

        await _context.SaveChangesAsync(cancellationToken);
        await SubmitPayrollRunWorkflowIfConfiguredAsync(tenantId, run, userId, cancellationToken);

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

        if (await IsPayrollRunWorkflowConfiguredAsync(tenantId, cancellationToken))
        {
            var workflowResult = await _workflowIntegrationService.SubmitAsync(PayrollRunWorkflowEntityType, run.Id);
            ApplyPayrollRunWorkflowOutcome(run, workflowResult.Outcome, userId);
        }
        else
        {
            run.Status = PayrollRunStatus.InReview;
            run.ReviewedAt = DateTime.UtcNow;
            run.ReviewedByUserId = userId;
        }

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

        if (await IsPayrollRunWorkflowConfiguredAsync(tenantId, cancellationToken))
        {
            if (!userId.HasValue || userId.Value == Guid.Empty)
            {
                throw new UnauthorizedAccessException("A valid approver user is required.");
            }

            var canApprove = await _workflowIntegrationService.CanUserApproveAsync(PayrollRunWorkflowEntityType, run.Id, userId.Value);
            if (!canApprove)
            {
                throw new UnauthorizedAccessException("You are not assigned as an approver for the current payroll workflow step.");
            }

            var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
                PayrollRunWorkflowEntityType,
                run.Id,
                userId.Value,
                "approve",
                dto.Notes);
            ApplyPayrollRunWorkflowOutcome(run, workflowResult.Outcome, userId);
        }
        else
        {
            run.Status = PayrollRunStatus.Approved;
            run.ApprovedAt = DateTime.UtcNow;
            run.ApprovedByUserId = userId;
        }

        AppendNotes(run, dto.Notes);
        await _context.SaveChangesAsync(cancellationToken);
        return await RequireRunDtoAsync(tenantId, run.Id, cancellationToken);
    }

    public async Task<PayrollRunDto> RejectRunAsync(Guid tenantId, Guid runId, Guid? userId, PayrollRunActionDto dto, CancellationToken cancellationToken = default)
    {
        var run = await GetMutableRunAsync(tenantId, runId, cancellationToken);
        if (run.Status != PayrollRunStatus.InReview)
        {
            throw new InvalidOperationException("Only payroll runs in review can be rejected.");
        }

        if (string.IsNullOrWhiteSpace(dto.Notes))
        {
            throw new InvalidOperationException("A rejection reason is required.");
        }

        if (await IsPayrollRunWorkflowConfiguredAsync(tenantId, cancellationToken))
        {
            if (!userId.HasValue || userId.Value == Guid.Empty)
            {
                throw new UnauthorizedAccessException("A valid approver user is required.");
            }

            var canApprove = await _workflowIntegrationService.CanUserApproveAsync(PayrollRunWorkflowEntityType, run.Id, userId.Value);
            if (!canApprove)
            {
                throw new UnauthorizedAccessException("You are not assigned as an approver for the current payroll workflow step.");
            }

            var workflowResult = await _workflowIntegrationService.ProcessApprovalAsync(
                PayrollRunWorkflowEntityType,
                run.Id,
                userId.Value,
                "reject",
                dto.Notes);
            ApplyPayrollRunWorkflowOutcome(run, workflowResult.Outcome, userId);
        }
        else
        {
            run.Status = PayrollRunStatus.RolledBack;
            run.ReviewedAt = null;
            run.ReviewedByUserId = null;
            run.ApprovedAt = null;
            run.ApprovedByUserId = null;
        }

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
            var workflowRequired = await IsPayrollRunWorkflowConfiguredAsync(tenantId, cancellationToken);
            if (workflowRequired && run.Status != PayrollRunStatus.Approved)
            {
                throw new InvalidOperationException("Payroll run workflow approval is required before closing the run.");
            }

            if (!workflowRequired && run.Status is PayrollRunStatus.Draft or PayrollRunStatus.RolledBack or PayrollRunStatus.Closed)
            {
                throw new InvalidOperationException("Calculate the payroll run before closing it.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var hasJournalLines = await _context.PayrollJournalLines.AnyAsync(e => e.TenantId == tenantId && e.PayrollRunId == run.Id, cancellationToken);
            if (!hasJournalLines)
            {
                var transactions = await _context.PayrollTransactions
                    .AsNoTracking()
                    .Where(e => e.TenantId == tenantId && e.PayrollRunId == run.Id)
                    .ToListAsync(cancellationToken);
                var journalLines = await BuildJournalLinesAsync(tenantId, run, transactions, cancellationToken);
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

    public async Task<PayrollOracleReportDto> GetOracleRunReportAsync(
        Guid tenantId,
        Guid runId,
        PayrollOracleReportRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var run = await PayrollRunQuery(tenantId)
            .FirstOrDefaultAsync(e => e.Id == runId, cancellationToken)
            ?? throw new KeyNotFoundException("Payroll run not found.");

        EnsureRunHasSavedOutput(run);

        var reportCode = NormalizeOracleReportCode(dto.ReportCode);
        IReadOnlyList<PayrollRun>? annualRuns = null;
        IReadOnlyCollection<Guid>? contextEmployeeIds = null;

        if (reportCode == "REP3_034")
        {
            var taxYear = run.PayPeriodTo.Year;
            annualRuns = await PayrollRunQuery(tenantId)
                .Where(e => e.PayPeriodTo.Year == taxYear &&
                            e.Status != PayrollRunStatus.Draft &&
                            e.Status != PayrollRunStatus.RolledBack)
                .OrderBy(e => e.PayPeriodTo)
                .ToListAsync(cancellationToken);
            contextEmployeeIds = annualRuns
                .SelectMany(e => e.Employees)
                .Select(e => e.EmployeeId)
                .Distinct()
                .ToList();
        }

        var payslipContext = await LoadPayslipContextAsync(tenantId, run, cancellationToken, contextEmployeeIds);
        return BuildOracleRunReport(run, payslipContext, dto, annualRuns);
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
        await EnsurePayrollRunWorkflowApprovedForFinalOutputAsync(tenantId, run, "viewing payslips", cancellationToken);

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
        await EnsurePayrollRunWorkflowApprovedForFinalOutputAsync(tenantId, run, "generating payslip snapshots", cancellationToken);

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
        await EnsurePayrollRunWorkflowApprovedForFinalOutputAsync(tenantId, run, "emailing payslips", cancellationToken);

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
        var message = TrimOrNull(dto.Message);
        var defaultSubject = $"Payslip - {periodLabel}";
        var payslipEmailTemplate = await EnsurePayrollPayslipEmailTemplateAsync(tenantId, cancellationToken);
        var payslipEmailTopic = await EnsurePayrollPayslipEmailTopicAsync(tenantId, payslipEmailTemplate.Id, userId, cancellationToken);
        var activePayslipEmailTemplate = payslipEmailTopic.EmailTemplate is { IsActive: true, IsDeleted: false }
            ? payslipEmailTopic.EmailTemplate
            : null;
        var topicBlocker = GetPayrollPayslipEmailTopicBlocker(payslipEmailTopic);
        var recipients = new List<PayrollPayslipEmailRecipientDto>();

        foreach (var runEmployee in runEmployees)
        {
            var payslip = BuildPayslip(run, runEmployee, snapshots.GetValueOrDefault(runEmployee.Id), payslipContext);
            payslipContext.ProfilesByEmployeeId.TryGetValue(runEmployee.EmployeeId, out var profile);
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

            if (!string.IsNullOrWhiteSpace(topicBlocker))
            {
                result.Message = topicBlocker;
                recipients.Add(result);
                continue;
            }

            var payslipPassword = BuildPayslipPdfPassword(profile);
            if (string.IsNullOrWhiteSpace(payslipPassword))
            {
                result.Message = "Employee date of birth or employee number is not configured for encrypted payslip delivery.";
                recipients.Add(result);
                continue;
            }

            var printHtmlDocument = BuildPayslipPrintHtmlDocument(payslip);
            var printInnerHtml = payslip.HtmlContent ?? BuildPayslipPrintInnerHtml(payslip);
            var templateValues = BuildPayslipTemplateValues(
                payslip,
                run,
                periodLabel,
                message,
                printInnerHtml,
                printHtmlDocument);
            var subjectTemplate = TrimOrNull(dto.Subject)
                ?? TrimOrNull(activePayslipEmailTemplate?.Subject)
                ?? defaultSubject;
            var emailSubject = RenderNotificationTemplate(subjectTemplate, templateValues);
            var html = BuildPayslipEmailHtml(activePayslipEmailTemplate, templateValues);
            var textBody = activePayslipEmailTemplate == null
                ? DefaultPayrollPayslipEmailPlainText()
                : TrimOrNull(RenderNotificationTemplate(activePayslipEmailTemplate.PlainTextBody ?? string.Empty, templateValues));
            var attachmentFileName = BuildPayslipEmailFileName(payslip);
            IReadOnlyList<NotificationTopicEmailAttachment> attachments = dto.AttachHtmlCopy
                ?
                [
                    new NotificationTopicEmailAttachment
                    {
                        FileName = attachmentFileName,
                        ContentType = "application/pdf",
                        ContentBase64 = Convert.ToBase64String(BuildPayslipPdfAttachment(payslip, payslipPassword))
                    }
                ]
                : [];

            var topicData = BuildPayslipTopicData(templateValues, run.Id, runEmployee.Id, payslip, userId);
            var metadata = new Dictionary<string, object>
            {
                ["runId"] = run.Id,
                ["runNumber"] = run.RunNumber ?? string.Empty,
                ["payrollRunEmployeeId"] = runEmployee.Id,
                ["employeeId"] = payslip.EmployeeId,
                ["employeeNumber"] = payslip.EmployeeNumber ?? string.Empty,
                ["payslipNumber"] = payslip.Snapshot?.PayslipNumber ?? string.Empty,
                ["requestedByUserId"] = userId?.ToString() ?? string.Empty,
                ["message"] = message ?? string.Empty
            };

            try
            {
                await _notificationTopicPublisher.PublishAsync(new NotificationTopicEvent
                {
                    TenantId = tenantId,
                    TopicKey = PayrollPayslipEmailTopicKey,
                    NotificationType = "PayrollPayslipEmail",
                    EntityType = PayrollPayslipEmailEntityType,
                    EntityId = payslip.Snapshot?.Id ?? run.Id,
                    TriggeredByUserId = userId,
                    Data = topicData,
                    Metadata = metadata,
                    Email = new NotificationTopicEmailOptions
                    {
                        SubjectTemplateOverride = emailSubject,
                        HtmlBodyTemplateOverride = html,
                        TextBodyTemplateOverride = textBody,
                        AttachmentSource = "PayrollPayslipPrintPreviewPdf",
                        Attachments = attachments
                    }
                }, cancellationToken);

                result.Sent = true;
                result.Message = $"Queued via notification topic {PayrollPayslipEmailTopicKey}.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish payslip email topic for payroll run {RunId}, employee {EmployeeId}", run.Id, payslip.EmployeeId);
                result.Message = "Failed to queue via notification topic.";
            }

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

    public async Task<PayrollJournalPreviewDto> GetPayrollJournalPreviewAsync(
        Guid tenantId,
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var run = await PayrollRunQuery(tenantId)
            .FirstOrDefaultAsync(e => e.Id == runId, cancellationToken)
            ?? throw new KeyNotFoundException("Payroll run not found.");

        EnsureRunHasSavedOutput(run);

        var generatedForPreview = false;
        var journalLines = run.JournalLines
            .Where(e => e.Posted || e.JournalEntryId.HasValue)
            .ToList();
        if (journalLines.Count == 0)
        {
            journalLines = (await BuildJournalLinesAsync(tenantId, run, run.Transactions.ToList(), cancellationToken)).ToList();
            generatedForPreview = true;
        }

        var lines = journalLines
            .OrderBy(e => e.SequenceNo)
            .Select(BuildPayrollJournalPreviewLine)
            .ToList();

        var journalNumber = BuildPayrollJournalNumber(run);
        var totalDebit = RoundMoney(lines.Where(e => e.DebitCredit == "DR").Sum(e => e.Amount));
        var totalCredit = RoundMoney(lines.Where(e => e.DebitCredit == "CR").Sum(e => e.Amount));
        var difference = RoundMoney(Math.Abs(totalDebit - totalCredit));
        var missingAccountCodes = await FindMissingPayrollJournalAccountCodesAsync(tenantId, lines, cancellationToken);
        var existingJournal = await _journalEntryService.GetJournalEntryByNumberAsync(journalNumber, cancellationToken);
        var alreadyPosted = lines.Any(e => e.Posted || e.JournalEntryId.HasValue) || existingJournal != null;
        var unmappedLineCount = lines.Count(IsPayrollJournalLineUnmapped);
        var invalidLineCount = lines.Count(e => e.DebitCredit is not "DR" and not "CR" || e.Amount <= 0);
        var blocker = BuildPayrollJournalPreviewBlocker(
            run,
            journalNumber,
            lines,
            missingAccountCodes,
            existingJournal != null,
            alreadyPosted,
            totalDebit,
            totalCredit,
            difference);

        return new PayrollJournalPreviewDto
        {
            PayrollRunId = run.Id,
            RunNumber = run.RunNumber,
            JournalNumber = journalNumber,
            RunStatus = run.Status,
            CurrencyCode = run.CurrencyCode,
            EmployeeCount = run.EmployeeCount,
            GrossAmount = run.GrossAmount,
            NetAmount = run.NetAmount,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            Difference = difference,
            LineCount = lines.Count,
            UnmappedLineCount = unmappedLineCount,
            InvalidLineCount = invalidLineCount,
            GeneratedForPreview = generatedForPreview,
            IsBalanced = difference <= PayrollJournalBalanceTolerance,
            AlreadyPosted = alreadyPosted,
            CanPost = blocker == null,
            Blocker = blocker,
            MissingAccountCodes = missingAccountCodes,
            Lines = lines
        };
    }

    public async Task<PayrollJournalPostingDto> PostPayrollJournalAsync(
        Guid tenantId,
        Guid runId,
        Guid? userId,
        PayrollRunActionDto dto,
        CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            var run = await PayrollRunQuery(tenantId)
                .FirstOrDefaultAsync(e => e.Id == runId, cancellationToken)
                ?? throw new KeyNotFoundException("Payroll run not found.");

            EnsureRunHasSavedOutput(run);
            await EnsurePayrollRunWorkflowApprovedForFinalOutputAsync(tenantId, run, "posting payroll", cancellationToken);

            var journalNumber = BuildPayrollJournalNumber(run);
            if (run.JournalLines.Any(e => e.Posted || e.JournalEntryId.HasValue))
            {
                throw new InvalidOperationException($"Payroll journal {journalNumber} has already been posted.");
            }

            var existingJournal = await _journalEntryService.GetJournalEntryByNumberAsync(journalNumber, cancellationToken);
            if (existingJournal != null)
            {
                throw new InvalidOperationException($"Finance journal {journalNumber} already exists. Review the existing journal before posting this payroll run again.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var journalLines = run.JournalLines.ToList();
            if (journalLines.Count > 0)
            {
                _context.PayrollJournalLines.RemoveRange(journalLines);
                // The Finance posting engine persists through this shared context. Flush the
                // soft-deleted preview rows first so the filtered unique index releases their
                // tenant/run/sequence keys before regenerated rows are tracked.
                await _context.SaveChangesAsync(cancellationToken);
            }

            journalLines = (await BuildJournalLinesAsync(tenantId, run, run.Transactions.ToList(), cancellationToken)).ToList();
            if (journalLines.Count == 0)
            {
                throw new InvalidOperationException("Payroll journal cannot be posted because no journal lines were generated.");
            }

            _context.PayrollJournalLines.AddRange(journalLines);

            foreach (var line in journalLines)
            {
                line.AccountCode = NormalizePayrollJournalAccountCode(line.AccountCode);
                line.DebitCredit = NormalizePayrollDebitCredit(line.DebitCredit);
                line.Amount = RoundMoney(Math.Abs(line.Amount));
                line.Description = TrimOrNull(line.Description) ?? $"{line.TransactionType} payroll journal";
            }

            var invalidSides = journalLines
                .Where(e => e.DebitCredit is not "DR" and not "CR")
                .Select(FormatPayrollJournalLineHint)
                .Take(5)
                .ToList();
            if (invalidSides.Count > 0)
            {
                throw new InvalidOperationException($"Payroll journal contains invalid debit/credit side(s): {string.Join(", ", invalidSides)}.");
            }

            var nonPositiveLines = journalLines
                .Where(e => e.Amount <= 0)
                .Select(FormatPayrollJournalLineHint)
                .Take(5)
                .ToList();
            if (nonPositiveLines.Count > 0)
            {
                throw new InvalidOperationException($"Payroll journal contains zero or negative amount line(s): {string.Join(", ", nonPositiveLines)}.");
            }

            var unmapped = journalLines
                .Where(e => string.IsNullOrWhiteSpace(e.AccountCode) || e.AccountCode.Equals("UNMAPPED", StringComparison.OrdinalIgnoreCase))
                .Select(FormatPayrollJournalLineHint)
                .Take(5)
                .ToList();
            if (unmapped.Count > 0)
            {
                throw new InvalidOperationException($"Payroll journal contains unmapped GL accounts ({string.Join(", ", unmapped)}). Complete Payroll Journal Mapping before posting.");
            }

            var totalDebit = RoundMoney(journalLines.Where(e => e.DebitCredit == "DR").Sum(e => e.Amount));
            var totalCredit = RoundMoney(journalLines.Where(e => e.DebitCredit == "CR").Sum(e => e.Amount));
            var balanceDifference = Math.Abs(totalDebit - totalCredit);
            if (balanceDifference > PayrollJournalBalanceTolerance)
            {
                throw new InvalidOperationException($"Payroll journal is not balanced. Debit: {totalDebit}, Credit: {totalCredit}, Difference: {balanceDifference}.");
            }

            var accountCodes = journalLines
                .Select(e => NormalizePayrollJournalAccountCode(e.AccountCode))
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var accounts = await _context.Accounts
                .AsNoTracking()
                .Where(e => e.TenantId == tenantId && (accountCodes.Contains(e.AccountCode.Trim()) || accountCodes.Contains(e.AccountNumber.Trim())))
                .ToListAsync(cancellationToken);

            var accountsByCode = accounts
                .SelectMany(e => new[]
                {
                    new { Code = NormalizePayrollJournalAccountCode(e.AccountCode), AccountId = e.Id },
                    new { Code = NormalizePayrollJournalAccountCode(e.AccountNumber), AccountId = e.Id }
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
            var postingRequest = new FinancePostingRequestDto
            {
                SourceModule = PayrollJournalSourceModule,
                SourceDocumentType = "PayrollRun",
                SourceDocumentId = run.Id,
                SourceDocumentTenantId = tenantId,
                SourceDocumentReference = run.RunNumber,
                Description = $"Payroll journal for {run.RunNumber}",
                PostingDate = run.ClosedAt ?? postedAt,
                JournalType = "Payroll",
                FunctionalCurrencyCode = run.CurrencyCode,
                IdempotencyKey = $"{PayrollJournalSourceModule}:PayrollRun:{run.Id:N}:Post",
                ReturnExistingOnDuplicate = true,
                Lines = journalLines
                    .OrderBy(e => e.SequenceNo)
                    .Select(e => new FinancePostingLineDto
                    {
                        AccountId = accountsByCode[NormalizePayrollJournalAccountCode(e.AccountCode)],
                        DebitAmount = e.DebitCredit == "DR" ? e.Amount : 0m,
                        CreditAmount = e.DebitCredit == "CR" ? e.Amount : 0m,
                        Description = e.Description,
                        SourceReferenceNumber = run.RunNumber,
                        TransactionCurrency = run.CurrencyCode,
                        LineNumber = e.SequenceNo
                    })
                    .ToList()
            };

            // Payroll is an approved HR source document, not a manually authored GL journal.
            // Route it through the central posting engine so the Finance journal is created as
            // system-generated and Posted in the same caller-owned transaction.
            var postingResult = await _financePostingEngine.PostAsync(postingRequest, cancellationToken);
            var posted = await _journalEntryService.GetJournalEntryByIdAsync(postingResult.JournalEntryId, cancellationToken)
                ?? throw new InvalidOperationException("Payroll journal was posted but could not be reloaded.");

            foreach (var line in journalLines)
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
            await transaction.CommitAsync(cancellationToken);

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
                Lines = journalLines.OrderBy(e => e.SequenceNo).Select(ToDto).ToList()
            };
        });
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

    private async Task SubmitPayrollRunWorkflowIfConfiguredAsync(
        Guid tenantId,
        PayrollRun run,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        await EnsurePayrollRunWorkflowEntityTypeAsync(tenantId, cancellationToken);
        if (!await IsPayrollRunWorkflowConfiguredAsync(tenantId, cancellationToken))
        {
            return;
        }

        var workflowResult = await _workflowIntegrationService.SubmitAsync(PayrollRunWorkflowEntityType, run.Id);
        ApplyPayrollRunWorkflowOutcome(run, workflowResult.Outcome, userId);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsurePayrollRunWorkflowApprovedForFinalOutputAsync(
        Guid tenantId,
        PayrollRun run,
        string action,
        CancellationToken cancellationToken)
    {
        if (!await IsPayrollRunWorkflowRequiredForRunAsync(tenantId, run, cancellationToken))
        {
            return;
        }

        if (run.Status is PayrollRunStatus.Approved or PayrollRunStatus.Closed)
        {
            return;
        }

        throw new InvalidOperationException($"Payroll run {run.RunNumber} must be approved through workflow before {action}.");
    }

    private async Task<bool> IsPayrollRunWorkflowConfiguredAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var entityTypeId = await GetPayrollRunWorkflowEntityTypeIdAsync(tenantId, cancellationToken);
        if (!entityTypeId.HasValue)
        {
            return false;
        }

        return await _context.WorkflowDefinitions
            .AsNoTracking()
            .AnyAsync(e =>
                e.TenantId == tenantId &&
                !e.IsDeleted &&
                e.IsActive &&
                e.EntityTypeId == entityTypeId.Value,
                cancellationToken);
    }

    private async Task<bool> IsPayrollRunWorkflowRequiredForRunAsync(Guid tenantId, PayrollRun run, CancellationToken cancellationToken)
    {
        var entityTypeId = await GetPayrollRunWorkflowEntityTypeIdAsync(tenantId, cancellationToken);
        if (!entityTypeId.HasValue)
        {
            return false;
        }

        var hasActiveDefinition = await _context.WorkflowDefinitions
            .AsNoTracking()
            .AnyAsync(e =>
                e.TenantId == tenantId &&
                !e.IsDeleted &&
                e.IsActive &&
                e.EntityTypeId == entityTypeId.Value,
                cancellationToken);

        if (hasActiveDefinition)
        {
            return true;
        }

        return await _context.WorkflowInstances
            .AsNoTracking()
            .AnyAsync(e =>
                e.TenantId == tenantId &&
                !e.IsDeleted &&
                e.EntityTypeId == entityTypeId.Value &&
                e.EntityId == run.Id,
                cancellationToken);
    }

    private async Task<Guid?> GetPayrollRunWorkflowEntityTypeIdAsync(Guid tenantId, CancellationToken cancellationToken)
        => await _context.WorkflowEntityTypes
            .AsNoTracking()
            .Where(e =>
                e.TenantId == tenantId &&
                !e.IsDeleted &&
                e.IsActive &&
                (e.Code == PayrollRunWorkflowEntityCode ||
                 e.Name == PayrollRunWorkflowEntityType ||
                 e.Name == "Payroll Run"))
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task EnsurePayrollRunWorkflowEntityTypeAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var existing = await _context.WorkflowEntityTypes
            .FirstOrDefaultAsync(e =>
                e.TenantId == tenantId &&
                !e.IsDeleted &&
                (e.Code == PayrollRunWorkflowEntityCode ||
                 e.Name == PayrollRunWorkflowEntityType ||
                 e.Name == "Payroll Run"),
                cancellationToken);

        if (existing != null)
        {
            if (!existing.IsActive)
            {
                existing.IsActive = true;
                existing.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        _context.WorkflowEntityTypes.Add(new WorkflowEntityType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = PayrollRunWorkflowEntityCode,
            Name = PayrollRunWorkflowEntityType,
            Description = "HR payroll run calculation, review, payslip generation, and posting approval.",
            EntityClassName = typeof(PayrollRun).FullName,
            IsActive = true,
            DisplayOrder = 72,
            Icon = "WalletCards",
            ColorCode = "#0EA5E9",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "System"
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyPayrollRunWorkflowOutcome(PayrollRun run, WorkflowOutcome outcome, Guid? userId)
    {
        switch (outcome)
        {
            case WorkflowOutcome.Approved:
                run.Status = PayrollRunStatus.Approved;
                run.ApprovedAt = DateTime.UtcNow;
                run.ApprovedByUserId = userId;
                break;
            case WorkflowOutcome.Rejected:
                run.Status = PayrollRunStatus.RolledBack;
                run.ReviewedAt = null;
                run.ReviewedByUserId = null;
                run.ApprovedAt = null;
                run.ApprovedByUserId = null;
                break;
            default:
                run.Status = PayrollRunStatus.InReview;
                run.ReviewedAt = DateTime.UtcNow;
                run.ReviewedByUserId = userId;
                run.ApprovedAt = null;
                run.ApprovedByUserId = null;
                break;
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

    private static PayrollOracleReportDto BuildOracleRunReport(
        PayrollRun run,
        PayrollPayslipBuildContext context,
        PayrollOracleReportRequestDto dto,
        IReadOnlyList<PayrollRun>? annualRuns = null)
    {
        var reportCode = NormalizeOracleReportCode(dto.ReportCode);
        var variantCode = NormalizeOracleVariantCode(reportCode, dto.VariantCode);
        var metadata = ResolveOracleReportMetadata(reportCode, variantCode);
        var baseEmployees = ResolveOracleReportEmployees(run, reportCode, annualRuns);
        var employees = ApplyOracleEmployeeFilters(baseEmployees, context, dto, reportCode)
            .OrderBy(e => e.EmployeeNumber)
            .ToList();
        var rows = reportCode switch
        {
            "REP3_001" => BuildPayrollRegisterRows(run, context, employees, variantCode),
            "REP3_002" => BuildPayrollAnalysisRows(context, employees, variantCode),
            "REP3_006" => BuildPayeRows(employees, variantCode),
            "REP3_007" => BuildSsfRows(context, employees, variantCode),
            "REP3_009" => BuildEmployerBankAdviceRows(run, context, dto, employees, variantCode),
            "REP3_010" => BuildAllowanceDeductionScheduleRows(run, employees, variantCode, dto),
            "REP3_011" => BuildCashListRows(run, context, employees),
            "REP3_034" => BuildAnnualTaxReturnRows(run, context, employees, annualRuns ?? [run], variantCode),
            "REP3_035" => BuildBankAdviceRows(run, context, dto, employees, variantCode),
            "REP3_036" => BuildLoanStatementRows(run, context, dto, employees, variantCode),
            "REP3_305" => BuildStaffListRows(context, employees, variantCode),
            "REP3_016" => BuildJournalRows(run, employees, variantCode),
            "REP3_019" => BuildOvertimeRows(context, employees),
            "REP3_028" => BuildBonusSlipRows(run, employees, dto),
            "REP3_029" => BuildBonusRegisterRows(run, employees, dto),
            "REP3_030" => BuildBonusTaxRows(run, employees, dto),
            "REP3_031" => BuildBonusBankAdviceRows(run, context, dto, employees),
            "REP3_032" => BuildBonusCashListRows(run, context, employees, dto),
            _ => []
        };
        var companyCode = TrimOrNull(context.CompanyProfile?.CompanyCode)
                          ?? TrimOrNull(context.CompanyProfile?.LegacyCompanyCode)
                          ?? "001";

        return new PayrollOracleReportDto
        {
            PayrollRunId = run.Id,
            RunNumber = run.RunNumber,
            ReportCode = reportCode,
            ReportName = metadata.ReportName,
            VariantCode = variantCode,
            VariantName = metadata.VariantName,
            SourceForm = metadata.SourceForm,
            SourceReport = metadata.SourceReport,
            PeriodLabel = ResolveOracleReportPeriodLabel(reportCode, run),
            CompanyCode = companyCode,
            CurrencyCode = TrimOrNull(dto.ReportingCurrency) ?? run.CurrencyCode,
            GeneratedAt = DateTime.UtcNow,
            Parameters = BuildOracleReportParameters(run, context, dto, reportCode),
            Columns = BuildOracleReportColumns(reportCode, variantCode),
            Rows = rows,
            TotalRows = rows.Count
        };
    }

    private static IReadOnlyList<PayrollRunEmployee> ResolveOracleReportEmployees(
        PayrollRun run,
        string reportCode,
        IReadOnlyList<PayrollRun>? annualRuns)
    {
        if (reportCode != "REP3_034" || annualRuns is not { Count: > 0 })
        {
            return run.Employees.ToList();
        }

        return annualRuns
            .SelectMany(annualRun => annualRun.Employees.Select(employee => new { annualRun, employee }))
            .GroupBy(e => e.employee.EmployeeId)
            .Select(group => group
                .OrderByDescending(e => e.annualRun.PayPeriodTo)
                .ThenByDescending(e => e.annualRun.PayPeriod)
                .First().employee)
            .ToList();
    }

    private static IReadOnlyList<PayrollRunEmployee> ApplyOracleEmployeeFilters(
        IEnumerable<PayrollRunEmployee> employees,
        PayrollPayslipBuildContext context,
        PayrollOracleReportRequestDto dto,
        string reportCode)
    {
        var filtered = employees.AsEnumerable();

        if (reportCode is "REP3_006" or "REP3_007" or "REP3_010" or "REP3_011" or "REP3_016" or "REP3_019" or "REP3_028" or "REP3_029" or "REP3_032" or "REP3_034" or "REP3_035" or "REP3_036")
        {
            filtered = filtered.Where(e => IsWithinOracleRange(e.EmployeeNumber, dto.EmployeeNumberFrom, dto.EmployeeNumberTo));
            if (HasOracleRange(dto.DepartmentFrom, dto.DepartmentTo))
            {
                filtered = filtered.Where(e =>
                {
                    var profile = ResolveOracleProfile(context, e);
                    return IsWithinOracleRange(ResolveOracleDepartmentCode(profile), dto.DepartmentFrom, dto.DepartmentTo);
                });
            }

            if (HasOracleRange(dto.LocationFrom, dto.LocationTo))
            {
                filtered = filtered.Where(e =>
                {
                    var profile = ResolveOracleProfile(context, e);
                    return IsWithinOracleRange(ResolveOracleLocationCode(profile), dto.LocationFrom, dto.LocationTo);
                });
            }
        }

        if (reportCode == "REP3_002")
        {
            var department = TrimOrNull(dto.DepartmentCode);
            if (!string.IsNullOrWhiteSpace(department) && !department.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(e =>
                {
                    var profile = ResolveOracleProfile(context, e);
                    return string.Equals(ResolveOracleDepartmentCode(profile), department, StringComparison.OrdinalIgnoreCase);
                });
            }

            var region = TrimOrNull(dto.RegionCode);
            if (!string.IsNullOrWhiteSpace(region) && !region.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(e =>
                {
                    var profile = ResolveOracleProfile(context, e);
                    return string.Equals(ResolveOracleRegionCode(profile), region, StringComparison.OrdinalIgnoreCase);
                });
            }
        }

        return filtered.ToList();
    }

    private static IReadOnlyList<PayrollOracleReportColumnDto> BuildOracleReportColumns(string reportCode, string variantCode)
    {
        if (reportCode == "REP3_001" && variantCode == "S")
        {
            return
            [
                OracleColumn("description", "Description"),
                OracleColumn("amount", "Amount", "right", "currency")
            ];
        }

        if (reportCode == "REP3_001" && variantCode is "D" or "E")
        {
            return
            [
                OracleColumn("group", variantCode == "E" ? "Section" : "Department"),
                OracleColumn("employeeCount", "Total Count", "right", "number"),
                OracleColumn("basicSalary", "Basic", "right", "currency"),
                OracleColumn("gross", "Gross", "right", "currency"),
                OracleColumn("incomeTax", "Income Tax", "right", "currency"),
                OracleColumn("netIncome", "Net", "right", "currency")
            ];
        }

        return reportCode switch
        {
            "REP3_002" when variantCode == "IND" => new[]
            {
                OracleColumn("staffId", "Staff ID"),
                OracleColumn("employeeName", "Employee Name"),
                OracleColumn("department", "Department"),
                OracleColumn("basic", "Basic", "right", "currency"),
                OracleColumn("allowances", "Allowances", "right", "currency"),
                OracleColumn("total", "Total", "right", "currency")
            },
            "REP3_002" => new[]
            {
                OracleColumn("group", variantCode == "REG" ? "Region" : "Department"),
                OracleColumn("count", "Count", "right", "number"),
                OracleColumn("basic", "Basic", "right", "currency"),
                OracleColumn("allowances", "Allowances", "right", "currency"),
                OracleColumn("total", "Total", "right", "currency")
            },
            "REP3_001" when variantCode == "DR" => new[]
            {
                OracleColumn("employeeNumber", "Employee No"),
                OracleColumn("employeeName", "Employee Name"),
                OracleColumn("basicSalary", "Basic Salary", "right", "currency"),
                OracleColumn("houseSupport", "House Support", "right", "currency"),
                OracleColumn("utility", "Utility", "right", "currency"),
                OracleColumn("fuelTransport", "Fuel/Transport", "right", "currency"),
                OracleColumn("professional", "Professional", "right", "currency"),
                OracleColumn("carMaintenance", "Car Maintenance", "right", "currency"),
                OracleColumn("carSubsidy", "Car Subsidy", "right", "currency"),
                OracleColumn("accomodation", "Accomodation", "right", "currency"),
                OracleColumn("educational", "Educational", "right", "currency"),
                OracleColumn("risk", "Risk", "right", "currency"),
                OracleColumn("leave", "Leave", "right", "currency"),
                OracleColumn("lunch", "Lunch", "right", "currency"),
                OracleColumn("clothing", "Clothing", "right", "currency"),
                OracleColumn("livingAllowance", "Living Allowance", "right", "currency"),
                OracleColumn("shift", "Shift", "right", "currency"),
                OracleColumn("otherNonCashAllowance", "Other Non Cash Allowance", "right", "currency"),
                OracleColumn("grossBonus", "Gross Bonus", "right", "currency"),
                OracleColumn("overtime", "Overtime", "right", "currency"),
                OracleColumn("totalCashEmolument", "Total Cash Emolument", "right", "currency"),
                OracleColumn("excessContributions", "Excess Contributions", "right", "currency"),
                OracleColumn("fuelOnly", "Fuel Only", "right", "currency"),
                OracleColumn("vehicleFuelDriver", "Vehicle Fuel & Driver", "right", "currency"),
                OracleColumn("vehicleFuelOnly", "Vehicle & Fuel Only", "right", "currency"),
                OracleColumn("accomodationBenefit", "Accomodation Benefit", "right", "currency"),
                OracleColumn("totalEmolument", "Total Emolument", "right", "currency"),
                OracleColumn("employeeSsf", "Employee SSF", "right", "currency"),
                OracleColumn("taxRelief", "Tax Relief", "right", "currency"),
                OracleColumn("tier3", "Tier3", "right", "currency"),
                OracleColumn("providentFund", "Provident Fund", "right", "currency"),
                OracleColumn("taxableIncome", "Taxable Income", "right", "currency"),
                OracleColumn("incomeTax", "Income Tax", "right", "currency"),
                OracleColumn("otherNonCashBenefitDed", "Other Non Cash Benefit Ded", "right", "currency"),
                OracleColumn("advanceDedNonLoan", "Advance Ded Non Loan", "right", "currency"),
                OracleColumn("totalDeductions", "Total Deductions", "right", "currency"),
                OracleColumn("netSalary", "Net Salary", "right", "currency"),
                OracleColumn("netUsd", "Net USD", "right", "currency")
            },
            "REP3_001" => new[]
            {
                OracleColumn("rowNumber", "No", "right", "number"),
                OracleColumn("employeeNumber", "Staff No"),
                OracleColumn("employeeName", "Employee Name"),
                OracleColumn("basicSalary", "Basic Salary", "right", "currency"),
                OracleColumn("otherAllowances", "Other Allowances", "right", "currency"),
                OracleColumn("totalOvertime", "Total Overtime", "right", "currency"),
                OracleColumn("grossSalary", "Gross Salary", "right", "currency"),
                OracleColumn("employeeSsf", "Employee SSF", "right", "currency"),
                OracleColumn("benefitsInKind", "Benefits In Kind", "right", "currency"),
                OracleColumn("taxRelief", "Tax Relief", "right", "currency"),
                OracleColumn("taxableIncome", "Taxable Income", "right", "currency"),
                OracleColumn("incomeTax", "Income Tax", "right", "currency"),
                OracleColumn("otherContributions", "Other Contributions", "right", "currency"),
                OracleColumn("otherDeductions", "Other Deductions", "right", "currency"),
                OracleColumn("loans", "Loans", "right", "currency"),
                OracleColumn("salaryAdvance", "Salary Advance", "right", "currency"),
                OracleColumn("totalDeductions", "Total Deductions", "right", "currency"),
                OracleColumn("netPay", "Net Pay", "right", "currency")
            },
            "REP3_009" => new[]
            {
                OracleColumn("bank", "Bank"),
                OracleColumn("staffName", "Staff Name"),
                OracleColumn("accountNo", "Account N0"),
                OracleColumn("amount", "Amount", "right", "currency")
            },
            "REP3_010" => new[]
            {
                OracleColumn("employeeNumber", "Employee"),
                OracleColumn("employeeName", "Name"),
                OracleColumn("transactionType", "Transaction Type"),
                OracleColumn("actualTransaction", "Actual Transaction"),
                OracleColumn("description", "Description"),
                OracleColumn("amount", "Amount", "right", "currency"),
                OracleColumn("employerAmount", "Employer Amount", "right", "currency")
            },
            "REP3_011" => new[]
            {
                OracleColumn("rowNumber", "No.", "right", "number"),
                OracleColumn("employeeNumber", "Employee No."),
                OracleColumn("employeeName", "Employee Name"),
                OracleColumn("amount", "Amount (GHC)", "right", "currency")
            },
            "REP3_034" when variantCode == "L" => new[]
            {
                OracleColumn("location", "Location"),
                OracleColumn("department", "Department"),
                OracleColumn("employeeNumber", "Staff"),
                OracleColumn("employeeName", "Employee"),
                OracleColumn("tin", "TIN"),
                OracleColumn("position", "Position"),
                OracleColumn("basicSalary", "Basic Salary", "right", "currency"),
                OracleColumn("taxableAllowances", "Taxable Allowances", "right", "currency"),
                OracleColumn("taxableOvertime", "Taxable OT", "right", "currency"),
                OracleColumn("bonus", "Bonus", "right", "currency"),
                OracleColumn("employeeSsf", "Employee SSF", "right", "currency"),
                OracleColumn("taxRelief", "Tax Relief", "right", "currency"),
                OracleColumn("taxableIncome", "Taxable Income", "right", "currency"),
                OracleColumn("incomeTax", "Income Tax", "right", "currency"),
                OracleColumn("taxPaid", "Tax Paid", "right", "currency")
            },
            "REP3_034" => new[]
            {
                OracleColumn("employeeNumber", "Staff No"),
                OracleColumn("employeeName", "Name of Employee"),
                OracleColumn("tin", "TIN"),
                OracleColumn("position", "Position"),
                OracleColumn("ssfNo", "SSF No"),
                OracleColumn("basicSalary", "Basic Salary", "right", "currency"),
                OracleColumn("taxableAllowances", "Taxable Allowances", "right", "currency"),
                OracleColumn("taxableOvertime", "Taxable OT", "right", "currency"),
                OracleColumn("bonus", "Bonus", "right", "currency"),
                OracleColumn("taxableBenefits", "Taxable Benefits", "right", "currency"),
                OracleColumn("employeeSsf", "Employee SSF", "right", "currency"),
                OracleColumn("taxRelief", "Tax Relief", "right", "currency"),
                OracleColumn("taxableIncome", "Taxable Income", "right", "currency"),
                OracleColumn("incomeTax", "Income Tax", "right", "currency"),
                OracleColumn("taxPaid", "Tax Paid", "right", "currency"),
                OracleColumn("netTax", "Net Tax", "right", "currency")
            },
            "REP3_006" when variantCode == "MPR" => new[]
            {
                OracleColumn("employeeNumber", "Employee No"),
                OracleColumn("employeeName", "Employee Name"),
                OracleColumn("employeeTin", "Employee TIN"),
                OracleColumn("basicSalary", "Basic Salary", "right", "currency"),
                OracleColumn("basic", "Basic", "right", "currency"),
                OracleColumn("grossAllowance", "Gross Allowance", "right", "currency"),
                OracleColumn("arrearsAllowance", "Arrears Allowance", "right", "currency"),
                OracleColumn("excessBonus", "Excess Bonus", "right", "currency"),
                OracleColumn("totalCashEmolument", "Total Cash Emolument", "right", "currency"),
                OracleColumn("benefitsInKind", "Benefits In Kind", "right", "currency"),
                OracleColumn("bonusInThreshold", "Bonus in Threshold", "right", "currency"),
                OracleColumn("totalEmolument", "Total Emolument", "right", "currency"),
                OracleColumn("deductableReliefs", "Deductable Reliefs", "right", "currency"),
                OracleColumn("employeeSsf", "Employee SSF", "right", "currency"),
                OracleColumn("employeeArrearsSsf", "Employee Arrears SSF", "right", "currency"),
                OracleColumn("employeeTotalSsf", "Employee Total SSF", "right", "currency"),
                OracleColumn("offPayrollExcessBonus", "OffPayroll Excess Bonus", "right", "currency"),
                OracleColumn("taxableIncome", "Taxable Income", "right", "currency"),
                OracleColumn("normalTax", "Normal Tax", "right", "currency"),
                OracleColumn("arrearsTax", "Arrears Tax", "right", "currency"),
                OracleColumn("bonusTax", "Bonus Tax", "right", "currency"),
                OracleColumn("totalTaxCharge", "Total Tax Charge", "right", "currency")
            },
            "REP3_006" => new[]
            {
                OracleColumn("rowNumber", "No", "right", "number"),
                OracleColumn("nameOfEmployee", "Name of Employee"),
                OracleColumn("tin", "TIN"),
                OracleColumn("position", "Position"),
                OracleColumn("resident", "Non - Resident (Y / N)"),
                OracleColumn("basicSalary", "Basic Salary", "right", "currency"),
                OracleColumn("secondaryEmployment", "Secondary Employment (Y / N)"),
                OracleColumn("socialSecurityFund", "Social Security Fund", "right", "currency"),
                OracleColumn("thirdTier", "Third Tier", "right", "currency"),
                OracleColumn("cashAllowances", "Cash Allowances", "right", "currency"),
                OracleColumn("bonusIncome", "Bonus Income (up to 15% of Basic)", "right", "currency"),
                OracleColumn("finalTaxOnBonus", "Final Tax On Bonus", "right", "currency"),
                OracleColumn("excessBonus", "Excess Bonus", "right", "currency"),
                OracleColumn("totalCashEmolument", "Total Cash emolument (6+10+13)", "right", "currency"),
                OracleColumn("accomodationElement", "Accomodation Element", "right", "currency"),
                OracleColumn("vehicleElement", "Vehicle Element", "right", "currency"),
                OracleColumn("nonCashBenefit", "Non Cash Benefit", "right", "currency"),
                OracleColumn("totalAssessableIncome", "Total Assessable Income (14+15+16+17)", "right", "currency"),
                OracleColumn("deductibleReliefs", "Deductible Reliefs", "right", "currency"),
                OracleColumn("totalReliefs", "Total Reliefs (8+9+19)", "right", "currency"),
                OracleColumn("chargeableIncome", "Chargeable Income (18 - 20)", "right", "currency"),
                OracleColumn("taxDeductible", "Tax Deductible", "right", "currency"),
                OracleColumn("overtimeIncome", "Overtime Income", "right", "currency"),
                OracleColumn("overtimeTax", "Overtime Tax", "right", "currency"),
                OracleColumn("graTaxPayable", "GRA Tax Payable (12+22+24)", "right", "currency"),
                OracleColumn("severancePayPaid", "Severance pay paid", "right", "currency"),
                OracleColumn("remarks", "Remarks")
            },
            "REP3_007" => new[]
            {
                OracleColumn("employeeNumber", "Employee No"),
                OracleColumn("employeeName", "Employee Name"),
                OracleColumn("ssfNo", "Social Security No"),
                OracleColumn("basic", "Basic Salary", "right", "currency"),
                OracleColumn("employeeSsf", "Employee SSF 5.5%", "right", "currency"),
                OracleColumn("employerSsf", "Employer SSF 13%", "right", "currency"),
                OracleColumn("total", "Total 18.5%", "right", "currency"),
                OracleColumn("firstTier", "1st Tier 13.5%", "right", "currency"),
                OracleColumn("secondTier", "2nd Tier 13.5%", "right", "currency"),
                OracleColumn("totalContribution", "Total 18.5%", "right", "currency")
            },
            "REP3_035" when IsBankAdviceEmployeeVariant(variantCode) => new[]
            {
                OracleColumn("staffId", "Staff ID"),
                OracleColumn("name", "Name"),
                OracleColumn("acctNo", variantCode == "BNK" ? "Acct No" : "Account No"),
                OracleColumn("net", variantCode == "BNK" ? "Net" : "Net PAY", "right", "currency")
            },
            "REP3_035" => new[]
            {
                OracleColumn("bankName", "Bank Name"),
                OracleColumn("accountCount", "No of Accounts", "right", "number"),
                OracleColumn("total", "Total", "right", "currency")
            },
            "REP3_036" when variantCode == "D" => new[]
            {
                OracleColumn("employeeNumber", "Staff ID"),
                OracleColumn("employeeName", "Employee Name"),
                OracleColumn("loanType", "Loan Type"),
                OracleColumn("facilityNumber", "Loan Reference"),
                OracleColumn("dateGranted", "Date of Loan"),
                OracleColumn("paymentStartDate", "Loan Start Date"),
                OracleColumn("paymentEndDate", "Loan End Date"),
                OracleColumn("amountGranted", "Loan Granted", "right", "currency"),
                OracleColumn("monthlyRepayment", "Monthly Repayment", "right", "currency"),
                OracleColumn("interestRepayment", "Monthly Interest", "right", "currency"),
                OracleColumn("totalPaid", "Paid", "right", "currency"),
                OracleColumn("outstandingBalance", "Loan Bal.", "right", "currency"),
                OracleColumn("status", "Status")
            },
            "REP3_036" => new[]
            {
                OracleColumn("employeeNumber", "Staff No"),
                OracleColumn("employeeName", "Staff Name"),
                OracleColumn("loanType", "Loan Type"),
                OracleColumn("facilityNumber", "Loan Reference"),
                OracleColumn("dateGranted", "Date of Loan"),
                OracleColumn("paymentStartDate", "Loan Start date"),
                OracleColumn("paymentEndDate", "Loan End Date"),
                OracleColumn("numberOfRepayments", "No of Repayments", "right", "number"),
                OracleColumn("repaymentDate", "Repayment Date"),
                OracleColumn("repaymentAmount", "Repayment", "right", "currency"),
                OracleColumn("interestAmount", "Interest", "right", "currency"),
                OracleColumn("amountPaid", "Amount Paid", "right", "currency"),
                OracleColumn("interestPaid", "Interest Paid", "right", "currency"),
                OracleColumn("cumulativePaid", "Cumulative Paid", "right", "currency"),
                OracleColumn("outstandingBalance", "Loan Balance", "right", "currency")
            },
            "REP3_305" => new[]
            {
                OracleColumn("group", "Group"),
                OracleColumn("employeeNumber", "Staff"),
                OracleColumn("employeeName", "Staff on Roll"),
                OracleColumn("dateEmployed", "Date Employed"),
                OracleColumn("gender", "Gender"),
                OracleColumn("department", "Department"),
                OracleColumn("section", "Section"),
                OracleColumn("position", "Position"),
                OracleColumn("location", "Location"),
                OracleColumn("grade", "Grade"),
                OracleColumn("staffCategory", "Staff Category"),
                OracleColumn("region", "Region"),
                OracleColumn("basicSalary", "Basic Salary", "right", "currency"),
                OracleColumn("status", "Status")
            },
            "REP3_016" => new[]
            {
                OracleColumn("accountNumber", "Account Number"),
                OracleColumn("accountDescription", "Account Description"),
                OracleColumn("debit", "Debit", "right", "currency"),
                OracleColumn("credit", "Credit", "right", "currency")
            },
            "REP3_019" => new[]
            {
                OracleColumn("staffNumber", "STAFF NUMBER"),
                OracleColumn("staff", "Staff"),
                OracleColumn("weekHours", "WEEK HRS", "right", "number"),
                OracleColumn("saturdayHours", "Saturday", "right", "number"),
                OracleColumn("sundayHours", "Sunday", "right", "number"),
                OracleColumn("holidayHours", "Holiday", "right", "number"),
                OracleColumn("totalHours", "TOTAL HOURS", "right", "number"),
                OracleColumn("totalAmount", "TOTAL AMOUNT", "right", "currency"),
                OracleColumn("department", "Department")
            },
            "REP3_028" => new[]
            {
                OracleColumn("employeeId", "Employee ID"),
                OracleColumn("staffName", "Staff Name"),
                OracleColumn("amount", "Amount", "right", "currency"),
                OracleColumn("bonusTax", "Bonus Tax", "right", "currency"),
                OracleColumn("netSalary", "Net Salary", "right", "currency")
            },
            "REP3_029" => new[]
            {
                OracleColumn("employeeNumber", "Employee"),
                OracleColumn("employeeName", "Name"),
                OracleColumn("bonus", "Bonus", "right", "currency"),
                OracleColumn("tax", "Tax", "right", "currency"),
                OracleColumn("netBonus", "Bonus", "right", "currency")
            },
            "REP3_030" => new[]
            {
                OracleColumn("employeeNumber", "Employee"),
                OracleColumn("employeeName", "Name"),
                OracleColumn("taxableBonus", "Taxable Bonus", "right", "currency"),
                OracleColumn("nonTaxableBonus", "Non Taxable Bonus", "right", "currency"),
                OracleColumn("bonus", "Bonus", "right", "currency"),
                OracleColumn("bonusTax", "Bonus Tax", "right", "currency"),
                OracleColumn("netBonus", "Net Bonus", "right", "currency")
            },
            "REP3_031" => new[]
            {
                OracleColumn("bank", "Bank"),
                OracleColumn("staffName", "Staff Name"),
                OracleColumn("branch", "Branch"),
                OracleColumn("accountNumber", "Account Number"),
                OracleColumn("amount", "Amount", "right", "currency"),
                OracleColumn("currencyCode", "Currency")
            },
            "REP3_032" => new[]
            {
                OracleColumn("rowNumber", "No.", "right", "number"),
                OracleColumn("employeeNumber", "Employee No."),
                OracleColumn("employeeName", "Employee Name"),
                OracleColumn("amount", "Amount (GHC)", "right", "currency")
            },
            _ => []
        };
    }

    private static IReadOnlyList<PayrollOracleReportRowDto> BuildPayrollRegisterRows(
        PayrollRun run,
        PayrollPayslipBuildContext context,
        IReadOnlyList<PayrollRunEmployee> employees,
        string variantCode)
    {
        if (variantCode == "S")
        {
            return BuildPayrollRegisterSummaryRows(employees);
        }

        if (variantCode is "D" or "E")
        {
            return BuildPayrollRegisterGroupedRows(context, employees, variantCode);
        }

        if (variantCode == "DR")
        {
            return BuildPayrollRegisterDetailRows(run, employees);
        }

        var transactionsByEmployee = run.Transactions
            .GroupBy(e => e.PayrollRunEmployeeId)
            .ToDictionary(e => e.Key, e => (IReadOnlyList<PayrollTransaction>)e.ToList());

        return employees.Select((employee, index) =>
        {
            transactionsByEmployee.TryGetValue(employee.Id, out var transactions);
            transactions ??= [];
            var otherAllowances = employee.TaxableAllowances + employee.NonTaxableAllowances +
                                  SumTransactionsByType(transactions, PromotionAllowanceArrearsTransactionType);
            var totalOvertime = SumTransactionsByType(transactions, OvertimeTransactionType);
            var benefitsInKind = SumTransactionsByType(transactions, "Benefit");
            var otherContributions = SumTransactionsByType(transactions,
                PromotionEmployeeContributionArrearsTransactionType,
                PromotionEmployerContributionArrearsTransactionType,
                PromotionContributionArrearsTransactionType);
            var loans = SumTransactionsByType(transactions, LoanRepaymentTransactionType, LoanInterestTransactionType);
            var salaryAdvance = SumTransactionsByType(transactions, SalaryAdvanceTransactionType);
            var totalDeductions = Math.Max(0, employee.GrossIncome - employee.NetIncome);
            var otherDeductions = Math.Max(0,
                totalDeductions - employee.EmployeeContribution - employee.IncomeTax - otherContributions - loans - salaryAdvance);

            return OracleRow(new Dictionary<string, object?>
            {
                ["rowNumber"] = index + 1,
                ["employeeNumber"] = employee.EmployeeNumber,
                ["employeeName"] = employee.EmployeeName,
                ["basicSalary"] = RoundMoney(employee.BasicSalary),
                ["otherAllowances"] = RoundMoney(otherAllowances),
                ["totalOvertime"] = RoundMoney(totalOvertime),
                ["grossSalary"] = RoundMoney(employee.GrossIncome),
                ["employeeSsf"] = RoundMoney(employee.EmployeeContribution),
                ["benefitsInKind"] = RoundMoney(benefitsInKind),
                ["taxRelief"] = RoundMoney(employee.TaxRelief),
                ["taxableIncome"] = RoundMoney(employee.TaxableIncome),
                ["incomeTax"] = RoundMoney(employee.IncomeTax),
                ["otherContributions"] = RoundMoney(otherContributions),
                ["otherDeductions"] = RoundMoney(otherDeductions),
                ["loans"] = RoundMoney(loans),
                ["salaryAdvance"] = RoundMoney(salaryAdvance),
                ["totalDeductions"] = RoundMoney(totalDeductions),
                ["netPay"] = RoundMoney(employee.NetIncome)
            });
        }).ToList();
    }

    private static IReadOnlyList<PayrollOracleReportRowDto> BuildPayrollRegisterSummaryRows(IReadOnlyList<PayrollRunEmployee> employees)
    {
        var totals = new (string Description, decimal Amount)[]
        {
            ("Basic", employees.Sum(e => e.BasicSalary)),
            ("Allowances", employees.Sum(e => e.TaxableAllowances + e.NonTaxableAllowances)),
            ("Gross", employees.Sum(e => e.GrossIncome)),
            ("Employee SSF", employees.Sum(e => e.EmployeeContribution)),
            ("Employer SSF", employees.Sum(e => e.EmployerContribution)),
            ("Tax Relief", employees.Sum(e => e.TaxRelief)),
            ("Income Tax", employees.Sum(e => e.IncomeTax)),
            ("Total Deductions", employees.Sum(e => Math.Max(0, e.GrossIncome - e.NetIncome))),
            ("Net", employees.Sum(e => e.NetIncome))
        };

        return totals
            .Select(e => OracleRow(new Dictionary<string, object?>
            {
                ["description"] = e.Description,
                ["amount"] = RoundMoney(e.Amount)
            }))
            .ToList();
    }

    private static IReadOnlyList<PayrollOracleReportRowDto> BuildPayrollRegisterGroupedRows(
        PayrollPayslipBuildContext context,
        IReadOnlyList<PayrollRunEmployee> employees,
        string variantCode)
    {
        return employees
            .GroupBy(e =>
            {
                var profile = ResolveOracleProfile(context, e);
                return variantCode == "E"
                    ? ResolveOracleSectionLabel(profile)
                    : ResolveOracleDepartmentLabel(profile);
            }, StringComparer.OrdinalIgnoreCase)
            .OrderBy(e => e.Key)
            .Select(group => OracleRow(new Dictionary<string, object?>
            {
                ["group"] = group.Key,
                ["employeeCount"] = group.Count(),
                ["basicSalary"] = RoundMoney(group.Sum(e => e.BasicSalary)),
                ["gross"] = RoundMoney(group.Sum(e => e.GrossIncome)),
                ["incomeTax"] = RoundMoney(group.Sum(e => e.IncomeTax)),
                ["netIncome"] = RoundMoney(group.Sum(e => e.NetIncome))
            }))
            .ToList();
    }

    private static IReadOnlyList<PayrollOracleReportRowDto> BuildPayrollRegisterDetailRows(PayrollRun run, IReadOnlyList<PayrollRunEmployee> employees)
    {
        var transactionsByEmployee = run.Transactions
            .GroupBy(e => e.PayrollRunEmployeeId)
            .ToDictionary(e => e.Key, e => (IReadOnlyList<PayrollTransaction>)e.ToList());

        return employees
            .OrderBy(e => e.EmployeeNumber)
            .Select(employee =>
            {
                transactionsByEmployee.TryGetValue(employee.Id, out var transactions);
                transactions ??= [];
                var basicArrears = SumTransactionsByType(transactions, PromotionBasicArrearsTransactionType);
                var grossBonus = SumTransactionsByType(transactions, BonusTransactionType);
                var overtime = SumTransactionsByType(transactions, OvertimeTransactionType);
                var tier3 = SumTransactionsByComponent(transactions, "3001");
                var providentFund = SumTransactionsByComponent(transactions, "3002");
                var otherNonCashBenefitDed = SumTransactionsByComponent(transactions, "5001");
                var advanceDedNonLoan = SumTransactionsByComponent(transactions, "5002") +
                                        SumTransactionsByType(transactions, SalaryAdvanceTransactionType);
                var excessContributions = transactions.Sum(e => e.EmployerAmount ?? 0m);
                var totalDeductions = Math.Max(0, employee.GrossIncome - employee.NetIncome);
                var totalCashEmolument = employee.BasicSalary + basicArrears +
                                         SumTransactionsByComponent(transactions, "1001", "1002", "1003", "1004", "1005", "1006", "1007", "1008", "1009", "1010", "1011", "1012", "1013", "1015") +
                                         grossBonus + overtime;

                return OracleRow(new Dictionary<string, object?>
                {
                    ["employeeNumber"] = employee.EmployeeNumber,
                    ["employeeName"] = employee.EmployeeName,
                    ["basicSalary"] = RoundMoney(employee.BasicSalary),
                    ["houseSupport"] = RoundMoney(SumTransactionsByComponent(transactions, "1001")),
                    ["utility"] = RoundMoney(SumTransactionsByComponent(transactions, "1002")),
                    ["fuelTransport"] = RoundMoney(SumTransactionsByComponent(transactions, "1003")),
                    ["professional"] = RoundMoney(SumTransactionsByComponent(transactions, "1004")),
                    ["carMaintenance"] = RoundMoney(SumTransactionsByComponent(transactions, "1005")),
                    ["carSubsidy"] = RoundMoney(SumTransactionsByComponent(transactions, "1006")),
                    ["accomodation"] = RoundMoney(SumTransactionsByComponent(transactions, "1007")),
                    ["educational"] = RoundMoney(SumTransactionsByComponent(transactions, "1008")),
                    ["risk"] = RoundMoney(SumTransactionsByComponent(transactions, "1009")),
                    ["leave"] = RoundMoney(SumTransactionsByComponent(transactions, "1010")),
                    ["lunch"] = RoundMoney(SumTransactionsByComponent(transactions, "1011")),
                    ["clothing"] = RoundMoney(SumTransactionsByComponent(transactions, "1012")),
                    ["livingAllowance"] = RoundMoney(SumTransactionsByComponent(transactions, "1013")),
                    ["shift"] = RoundMoney(SumTransactionsByComponent(transactions, "1015")),
                    ["otherNonCashAllowance"] = RoundMoney(SumTransactionsByComponent(transactions, "1014")),
                    ["grossBonus"] = RoundMoney(grossBonus),
                    ["overtime"] = RoundMoney(overtime),
                    ["totalCashEmolument"] = RoundMoney(totalCashEmolument),
                    ["excessContributions"] = RoundMoney(excessContributions),
                    ["fuelOnly"] = RoundMoney(SumTransactionsByComponent(transactions, "2001")),
                    ["vehicleFuelDriver"] = RoundMoney(SumTransactionsByComponent(transactions, "2002")),
                    ["vehicleFuelOnly"] = RoundMoney(SumTransactionsByComponent(transactions, "2003")),
                    ["accomodationBenefit"] = RoundMoney(SumTransactionsByComponent(transactions, "2004")),
                    ["totalEmolument"] = RoundMoney(employee.GrossIncome),
                    ["employeeSsf"] = RoundMoney(employee.EmployeeContribution),
                    ["taxRelief"] = RoundMoney(employee.TaxRelief),
                    ["tier3"] = RoundMoney(tier3),
                    ["providentFund"] = RoundMoney(providentFund),
                    ["taxableIncome"] = RoundMoney(employee.TaxableIncome),
                    ["incomeTax"] = RoundMoney(employee.IncomeTax),
                    ["otherNonCashBenefitDed"] = RoundMoney(otherNonCashBenefitDed),
                    ["advanceDedNonLoan"] = RoundMoney(advanceDedNonLoan),
                    ["totalDeductions"] = RoundMoney(totalDeductions),
                    ["netSalary"] = RoundMoney(employee.NetIncome),
                    ["netUsd"] = RoundMoney(employee.NetIncome)
                });
            })
            .ToList();
    }

    private static decimal SumTransactionsByType(IEnumerable<PayrollTransaction> transactions, params string[] transactionTypes)
        => transactions
            .Where(e => transactionTypes.Contains(e.TransactionType, StringComparer.OrdinalIgnoreCase))
            .Sum(e => e.Amount);

    private static decimal SumTransactionsByComponent(IEnumerable<PayrollTransaction> transactions, params string[] componentCodes)
        => transactions
            .Where(e => e.ComponentCode != null && componentCodes.Contains(e.ComponentCode, StringComparer.OrdinalIgnoreCase))
            .Sum(e => e.Amount);

    private static IReadOnlyList<PayrollOracleReportRowDto> BuildPayrollAnalysisRows(
        PayrollPayslipBuildContext context,
        IReadOnlyList<PayrollRunEmployee> employees,
        string variantCode)
    {
        static decimal Allowances(PayrollRunEmployee employee)
            => employee.TaxableAllowances + employee.NonTaxableAllowances;

        if (variantCode == "IND")
        {
            var individualRows = employees
                .Select(employee =>
                {
                    var profile = ResolveOracleProfile(context, employee);
                    var allowances = Allowances(employee);

                    return OracleRow(new Dictionary<string, object?>
                    {
                        ["staffId"] = employee.EmployeeNumber,
                        ["employeeName"] = employee.EmployeeName,
                        ["department"] = ResolveOracleDepartmentLabel(profile),
                        ["basic"] = RoundMoney(employee.BasicSalary),
                        ["allowances"] = RoundMoney(allowances),
                        ["total"] = RoundMoney(employee.BasicSalary + allowances)
                    });
                })
                .ToList();

            individualRows.Add(OracleRow(new Dictionary<string, object?>
            {
                ["oracleRowType"] = "TOTAL",
                ["staffId"] = $"Count : {employees.Count}",
                ["employeeName"] = "Total",
                ["basic"] = RoundMoney(employees.Sum(e => e.BasicSalary)),
                ["allowances"] = RoundMoney(employees.Sum(Allowances)),
                ["total"] = RoundMoney(employees.Sum(e => e.BasicSalary + Allowances(e)))
            }));

            return individualRows;
        }

        var groupedRows = employees
            .GroupBy(employee =>
            {
                var profile = ResolveOracleProfile(context, employee);
                return variantCode == "REG"
                    ? ResolveOracleRegionLabel(profile)
                    : ResolveOracleDepartmentLabel(profile);
            }, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key)
            .Select(group => OracleRow(new Dictionary<string, object?>
            {
                ["group"] = group.Key,
                ["count"] = group.Count(),
                ["basic"] = RoundMoney(group.Sum(e => e.BasicSalary)),
                ["allowances"] = RoundMoney(group.Sum(Allowances)),
                ["total"] = RoundMoney(group.Sum(e => e.BasicSalary + Allowances(e)))
            }))
            .ToList();

        groupedRows.Add(OracleRow(new Dictionary<string, object?>
        {
            ["oracleRowType"] = "TOTAL",
            ["group"] = "Total",
            ["count"] = employees.Count,
            ["basic"] = RoundMoney(employees.Sum(e => e.BasicSalary)),
            ["allowances"] = RoundMoney(employees.Sum(Allowances)),
            ["total"] = RoundMoney(employees.Sum(e => e.BasicSalary + Allowances(e)))
        }));

        return groupedRows;
    }

    private static IReadOnlyList<PayrollOracleReportRowDto> BuildEmployerBankAdviceRows(
        PayrollRun run,
        PayrollPayslipBuildContext context,
        PayrollOracleReportRequestDto dto,
        IReadOnlyList<PayrollRunEmployee> employees,
        string variantCode)
    {
        var payments = BuildOracleBankPaymentRows(run, context, dto, employees)
            .OrderBy(e => e.BankName)
            .ThenBy(e => e.BranchName)
            .ThenBy(e => e.Employee.EmployeeNumber)
            .ToList();

        var rows = payments
            .Select(e => OracleRow(new Dictionary<string, object?>
            {
                ["bank"] = e.BankName,
                ["bankBranch"] = e.BranchName ?? string.Empty,
                ["staffName"] = e.Employee.EmployeeName,
                ["accountNo"] = e.AccountNumber ?? string.Empty,
                ["amount"] = RoundMoney(e.Amount),
                ["currencyCode"] = e.CurrencyCode,
                ["sourceReportVariant"] = variantCode
            }))
            .ToList();

        rows.Add(OracleRow(new Dictionary<string, object?>
        {
            ["oracleRowType"] = "TOTAL",
            ["bank"] = "Total",
            ["staffName"] = $"Count : {payments.Count}",
            ["amount"] = RoundMoney(payments.Sum(e => e.Amount))
        }));

        return rows;
    }

    private static IReadOnlyList<PayrollOracleReportRowDto> BuildAllowanceDeductionScheduleRows(
        PayrollRun run,
        IReadOnlyList<PayrollRunEmployee> employees,
        string variantCode,
        PayrollOracleReportRequestDto dto)
    {
        var employeeIds = employees.Select(e => e.Id).ToHashSet();
        var rows = run.Transactions
            .Where(e => employeeIds.Contains(e.PayrollRunEmployeeId))
            .Where(e => IsAllowanceDeductionScheduleTransaction(e, variantCode))
            .Where(e => IsWithinOracleRange(e.ComponentCode ?? e.TransactionType, dto.ComponentCodeFrom, dto.ComponentCodeTo))
            .OrderBy(e => e.EmployeeNumber)
            .ThenBy(e => e.TransactionType)
            .ThenBy(e => e.ComponentCode)
            .Select(transaction =>
            {
                var employee = employees.FirstOrDefault(e => e.Id == transaction.PayrollRunEmployeeId);
                return OracleRow(new Dictionary<string, object?>
                {
                    ["employeeNumber"] = transaction.EmployeeNumber,
                    ["employeeName"] = employee?.EmployeeName ?? string.Empty,
                    ["transactionType"] = transaction.TransactionType,
                    ["actualTransaction"] = transaction.ComponentCode ?? transaction.TransactionType,
                    ["description"] = transaction.Description ?? transaction.TransactionType,
                    ["amount"] = RoundMoney(transaction.Amount),
                    ["employerAmount"] = RoundMoney(transaction.EmployerAmount ?? 0m)
                });
            })
            .ToList();

        rows.Add(OracleRow(new Dictionary<string, object?>
        {
            ["oracleRowType"] = "TOTAL",
            ["employeeNumber"] = $"Count : {rows.Count}",
            ["employeeName"] = "Total",
            ["amount"] = RoundMoney(rows.Sum(row => GetOracleDecimal(row, "amount"))),
            ["employerAmount"] = RoundMoney(rows.Sum(row => GetOracleDecimal(row, "employerAmount")))
        }));

        return rows;
    }

    private static IReadOnlyList<PayrollOracleReportRowDto> BuildCashListRows(
        PayrollRun run,
        PayrollPayslipBuildContext context,
        IReadOnlyList<PayrollRunEmployee> employees)
    {
        var rows = new List<PayrollOracleReportRowDto>();
        foreach (var employee in employees.OrderBy(e => e.EmployeeNumber))
        {
            var profile = ResolveOracleProfile(context, employee);
            if (profile == null)
            {
                continue;
            }

            var cashMethods = profile.PaymentMethods
                .Where(e => e.IsActive &&
                            e.PaymentType.Equals("Cash", StringComparison.OrdinalIgnoreCase) &&
                            IsEffective(e.StartDate, e.EndDate, run.PayPeriodFrom, run.PayPeriodTo))
                .OrderBy(e => e.SequenceNo)
                .ToList();

            var allocations = BuildPaymentAllocations(cashMethods, employee.NetIncome, employee.CurrencyCode);

            rows.AddRange(allocations.Select((allocation, index) => OracleRow(new Dictionary<string, object?>
            {
                ["rowNumber"] = rows.Count + index + 1,
                ["employeeNumber"] = employee.EmployeeNumber,
                ["employeeName"] = employee.EmployeeName,
                ["amount"] = RoundMoney(allocation.Amount)
            })));
        }

        rows.Add(OracleRow(new Dictionary<string, object?>
        {
            ["oracleRowType"] = "TOTAL",
            ["employeeNumber"] = $"COUNT : {rows.Count}",
            ["employeeName"] = "Total",
            ["amount"] = RoundMoney(rows.Sum(row => GetOracleDecimal(row, "amount")))
        }));

        return rows;
    }

    private static IReadOnlyList<PayrollOracleReportRowDto> BuildOvertimeRows(
        PayrollPayslipBuildContext context,
        IReadOnlyList<PayrollRunEmployee> employees)
    {
        var rows = employees
            .Select(employee =>
            {
                var profile = ResolveOracleProfile(context, employee);
                var summary = profile != null && context.TimesheetSummariesByProfileId.TryGetValue(profile.Id, out var current)
                    ? current
                    : null;

                var weekHours = summary?.WeekdayHours ?? 0m;
                var saturdayHours = summary?.SaturdayHours ?? 0m;
                var sundayHours = summary?.SundayHours ?? 0m;
                var holidayHours = summary?.HolidayHours ?? 0m;
                var totalHours = weekHours + saturdayHours + sundayHours + holidayHours;
                var amount = summary?.OvertimeAmount ?? 0m;

                return OracleRow(new Dictionary<string, object?>
                {
                    ["staffNumber"] = employee.EmployeeNumber,
                    ["staff"] = employee.EmployeeName,
                    ["weekHours"] = RoundMoney(weekHours),
                    ["saturdayHours"] = RoundMoney(saturdayHours),
                    ["sundayHours"] = RoundMoney(sundayHours),
                    ["holidayHours"] = RoundMoney(holidayHours),
                    ["totalHours"] = RoundMoney(totalHours),
                    ["totalAmount"] = RoundMoney(amount),
                    ["department"] = ResolveOracleDepartmentLabel(profile)
                });
            })
            .Where(row => GetOracleDecimal(row, "totalHours") != 0 || GetOracleDecimal(row, "totalAmount") != 0)
            .ToList();

        rows.Add(OracleRow(new Dictionary<string, object?>
        {
            ["oracleRowType"] = "TOTAL",
            ["staffNumber"] = $"COUNT: {rows.Count}",
            ["staff"] = "GRAND TOTAL",
            ["weekHours"] = RoundMoney(rows.Sum(row => GetOracleDecimal(row, "weekHours"))),
            ["saturdayHours"] = RoundMoney(rows.Sum(row => GetOracleDecimal(row, "saturdayHours"))),
            ["sundayHours"] = RoundMoney(rows.Sum(row => GetOracleDecimal(row, "sundayHours"))),
            ["holidayHours"] = RoundMoney(rows.Sum(row => GetOracleDecimal(row, "holidayHours"))),
            ["totalHours"] = RoundMoney(rows.Sum(row => GetOracleDecimal(row, "totalHours"))),
            ["totalAmount"] = RoundMoney(rows.Sum(row => GetOracleDecimal(row, "totalAmount")))
        }));

        return rows;
    }

    private static IReadOnlyList<PayrollOracleReportRowDto> BuildBonusSlipRows(
        PayrollRun run,
        IReadOnlyList<PayrollRunEmployee> employees,
        PayrollOracleReportRequestDto dto)
    {
        var bonusRows = BuildOracleBonusRows(run, employees, dto);
        var rows = bonusRows
            .Select(e => OracleRow(new Dictionary<string, object?>
            {
                ["employeeId"] = e.Employee.EmployeeNumber,
                ["staffName"] = e.Employee.EmployeeName,
                ["amount"] = RoundMoney(e.Bonus),
                ["bonusTax"] = RoundMoney(e.BonusTax),
                ["netSalary"] = RoundMoney(e.NetBonus)
            }))
            .ToList();

        rows.Add(OracleRow(new Dictionary<string, object?>
        {
            ["oracleRowType"] = "TOTAL",
            ["employeeId"] = $"Count : {bonusRows.Count}",
            ["staffName"] = "Total",
            ["amount"] = RoundMoney(bonusRows.Sum(e => e.Bonus)),
            ["bonusTax"] = RoundMoney(bonusRows.Sum(e => e.BonusTax)),
            ["netSalary"] = RoundMoney(bonusRows.Sum(e => e.NetBonus))
        }));

        return rows;
    }

    private static IReadOnlyList<PayrollOracleReportRowDto> BuildBonusRegisterRows(
        PayrollRun run,
        IReadOnlyList<PayrollRunEmployee> employees,
        PayrollOracleReportRequestDto dto)
    {
        var bonusRows = BuildOracleBonusRows(run, employees, dto);
        var rows = bonusRows
            .Select(e => OracleRow(new Dictionary<string, object?>
            {
                ["employeeNumber"] = e.Employee.EmployeeNumber,
                ["employeeName"] = e.Employee.EmployeeName,
                ["bonus"] = RoundMoney(e.Bonus),
                ["tax"] = RoundMoney(e.BonusTax),
                ["netBonus"] = RoundMoney(e.NetBonus)
            }))
            .ToList();

        rows.Add(OracleRow(new Dictionary<string, object?>
        {
            ["oracleRowType"] = "TOTAL",
            ["employeeNumber"] = $"Count : {bonusRows.Count}",
            ["employeeName"] = "Total",
            ["bonus"] = RoundMoney(bonusRows.Sum(e => e.Bonus)),
            ["tax"] = RoundMoney(bonusRows.Sum(e => e.BonusTax)),
            ["netBonus"] = RoundMoney(bonusRows.Sum(e => e.NetBonus))
        }));

        return rows;
    }

    private static IReadOnlyList<PayrollOracleReportRowDto> BuildBonusTaxRows(
        PayrollRun run,
        IReadOnlyList<PayrollRunEmployee> employees,
        PayrollOracleReportRequestDto dto)
    {
        var bonusRows = BuildOracleBonusRows(run, employees, dto);
        var rows = bonusRows
            .Select(e => OracleRow(new Dictionary<string, object?>
            {
                ["employeeNumber"] = e.Employee.EmployeeNumber,
                ["employeeName"] = e.Employee.EmployeeName,
                ["taxableBonus"] = RoundMoney(e.TaxableBonus),
                ["nonTaxableBonus"] = RoundMoney(e.NonTaxableBonus),
                ["bonus"] = RoundMoney(e.Bonus),
                ["bonusTax"] = RoundMoney(e.BonusTax),
                ["netBonus"] = RoundMoney(e.NetBonus)
            }))
            .ToList();

        rows.Add(OracleRow(new Dictionary<string, object?>
        {
            ["oracleRowType"] = "TOTAL",
            ["employeeNumber"] = $"Count : {bonusRows.Count}",
            ["employeeName"] = "Total",
            ["taxableBonus"] = RoundMoney(bonusRows.Sum(e => e.TaxableBonus)),
            ["nonTaxableBonus"] = RoundMoney(bonusRows.Sum(e => e.NonTaxableBonus)),
            ["bonus"] = RoundMoney(bonusRows.Sum(e => e.Bonus)),
            ["bonusTax"] = RoundMoney(bonusRows.Sum(e => e.BonusTax)),
            ["netBonus"] = RoundMoney(bonusRows.Sum(e => e.NetBonus))
        }));

        return rows;
    }

    private static IReadOnlyList<PayrollOracleReportRowDto> BuildBonusBankAdviceRows(
        PayrollRun run,
        PayrollPayslipBuildContext context,
        PayrollOracleReportRequestDto dto,
        IReadOnlyList<PayrollRunEmployee> employees)
    {
        var bonusRows = BuildOracleBonusRows(run, employees, dto);
        var payments = BuildOracleBankPaymentRowsForAmounts(
                run,
                context,
                dto,
                bonusRows.Select(e => new OracleEmployeePaymentBase(e.Employee, e.NetBonus)).ToList())
            .OrderBy(e => e.BankName)
            .ThenBy(e => e.BranchName)
            .ThenBy(e => e.Employee.EmployeeNumber)
            .ToList();

        var rows = payments
            .Select(e => OracleRow(new Dictionary<string, object?>
            {
                ["bank"] = e.BankName,
                ["staffName"] = e.Employee.EmployeeName,
                ["branch"] = e.BranchName ?? string.Empty,
                ["accountNumber"] = e.AccountNumber ?? string.Empty,
                ["amount"] = RoundMoney(e.Amount),
                ["currencyCode"] = e.CurrencyCode
            }))
            .ToList();

        rows.Add(OracleRow(new Dictionary<string, object?>
        {
            ["oracleRowType"] = "TOTAL",
            ["bank"] = "Total",
            ["staffName"] = $"Count : {payments.Count}",
            ["amount"] = RoundMoney(payments.Sum(e => e.Amount))
        }));

        return rows;
    }

    private static IReadOnlyList<PayrollOracleReportRowDto> BuildBonusCashListRows(
        PayrollRun run,
        PayrollPayslipBuildContext context,
        IReadOnlyList<PayrollRunEmployee> employees,
        PayrollOracleReportRequestDto dto)
    {
        var bonusRows = BuildOracleBonusRows(run, employees, dto);
        var rows = new List<PayrollOracleReportRowDto>();
        foreach (var bonusRow in bonusRows.OrderBy(e => e.Employee.EmployeeNumber))
        {
            var profile = ResolveOracleProfile(context, bonusRow.Employee);
            if (profile == null)
            {
                continue;
            }

            var cashMethods = profile.PaymentMethods
                .Where(e => e.IsActive &&
                            e.PaymentType.Equals("Cash", StringComparison.OrdinalIgnoreCase) &&
                            IsEffective(e.StartDate, e.EndDate, run.PayPeriodFrom, run.PayPeriodTo))
                .OrderBy(e => e.SequenceNo)
                .ToList();

            var allocations = BuildPaymentAllocations(cashMethods, bonusRow.NetBonus, bonusRow.Employee.CurrencyCode);

            rows.AddRange(allocations.Select((allocation, index) => OracleRow(new Dictionary<string, object?>
            {
                ["rowNumber"] = rows.Count + index + 1,
                ["employeeNumber"] = bonusRow.Employee.EmployeeNumber,
                ["employeeName"] = bonusRow.Employee.EmployeeName,
                ["amount"] = RoundMoney(allocation.Amount)
            })));
        }

        rows.Add(OracleRow(new Dictionary<string, object?>
        {
            ["oracleRowType"] = "TOTAL",
            ["employeeNumber"] = $"COUNT : {rows.Count}",
            ["employeeName"] = "Total",
            ["amount"] = RoundMoney(rows.Sum(row => GetOracleDecimal(row, "amount")))
        }));

        return rows;
    }

    private static IReadOnlyList<OracleBonusEmployeeRow> BuildOracleBonusRows(
        PayrollRun run,
        IReadOnlyList<PayrollRunEmployee> employees,
        PayrollOracleReportRequestDto dto)
    {
        var bonusType = ResolveOracleBonusType(dto);
        var transactionsByEmployee = run.Transactions
            .GroupBy(e => e.PayrollRunEmployeeId)
            .ToDictionary(e => e.Key, e => (IReadOnlyList<PayrollTransaction>)e.ToList());

        return employees
            .Select(employee =>
            {
                transactionsByEmployee.TryGetValue(employee.Id, out var transactions);
                transactions ??= [];
                var bonusTransactions = transactions
                    .Where(e => IsOracleBonusTransaction(e, bonusType))
                    .ToList();
                var bonus = bonusTransactions.Sum(e => e.Amount);
                var taxableBonus = bonusTransactions.Where(e => e.Taxable).Sum(e => e.Amount);
                var nonTaxableBonus = bonusTransactions.Where(e => !e.Taxable).Sum(e => e.Amount);
                var bonusTax = transactions.Where(e => IsOracleBonusTaxTransaction(e, bonusType)).Sum(e => e.Amount);

                if (bonus == 0m && run.IsSeparateBonusRun)
                {
                    bonus = employee.GrossIncome;
                    taxableBonus = employee.TaxableAllowances != 0m ? employee.TaxableAllowances : bonus;
                    nonTaxableBonus = employee.NonTaxableAllowances;
                }

                if (bonusTax == 0m && run.IsSeparateBonusRun)
                {
                    bonusTax = employee.IncomeTax;
                }

                var netBonus = bonus - bonusTax;
                if (netBonus == 0m && run.IsSeparateBonusRun)
                {
                    netBonus = employee.NetIncome;
                }

                return new OracleBonusEmployeeRow(
                    employee,
                    bonus,
                    taxableBonus,
                    nonTaxableBonus,
                    bonusTax,
                    netBonus);
            })
            .Where(e => e.Bonus != 0m || e.BonusTax != 0m || e.NetBonus != 0m)
            .OrderBy(e => e.Employee.EmployeeNumber)
            .ToList();
    }

    private static string? ResolveOracleBonusType(PayrollOracleReportRequestDto dto)
    {
        var bonusType = TrimOrNull(dto.ComponentCodeFrom);
        return bonusType is null ||
               bonusType.Equals("0", StringComparison.OrdinalIgnoreCase) ||
               bonusType.Equals("ALL", StringComparison.OrdinalIgnoreCase) ||
               bonusType.Equals("ZZZZZ", StringComparison.OrdinalIgnoreCase)
            ? null
            : bonusType;
    }

    private static bool IsOracleBonusTransaction(PayrollTransaction transaction, string? bonusType)
        => transaction.TransactionType.Equals(BonusTransactionType, StringComparison.OrdinalIgnoreCase) &&
           OracleBonusTypeMatches(transaction, bonusType);

    private static bool IsOracleBonusTaxTransaction(PayrollTransaction transaction, string? bonusType)
    {
        if (!transaction.TransactionType.Equals(IncomeTaxTransactionType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hasBonusMarker =
            transaction.ComponentCode?.Equals(BonusTransactionType, StringComparison.OrdinalIgnoreCase) == true ||
            transaction.Description?.Contains("bonus", StringComparison.OrdinalIgnoreCase) == true ||
            !string.IsNullOrWhiteSpace(bonusType) && OracleBonusTypeMatches(transaction, bonusType);

        return hasBonusMarker;
    }

    private static bool OracleBonusTypeMatches(PayrollTransaction transaction, string? bonusType)
    {
        if (string.IsNullOrWhiteSpace(bonusType))
        {
            return true;
        }

        return transaction.ComponentCode?.Equals(bonusType, StringComparison.OrdinalIgnoreCase) == true ||
               transaction.Description?.Contains(bonusType, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsAllowanceDeductionScheduleTransaction(PayrollTransaction transaction, string variantCode)
    {
        if (transaction.TransactionType.Equals(BasicSalaryTransactionType, StringComparison.OrdinalIgnoreCase) ||
            transaction.TransactionType.Equals(NetPayTransactionType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return variantCode switch
        {
            "ALW" => IsOracleAllowanceTransaction(transaction),
            "DED" => IsOracleDeductionTransaction(transaction),
            "ADV" => transaction.TransactionType.Equals(SalaryAdvanceTransactionType, StringComparison.OrdinalIgnoreCase),
            "REP" => transaction.TransactionType.Equals(LoanRepaymentTransactionType, StringComparison.OrdinalIgnoreCase),
            "INT" => transaction.TransactionType.Equals(LoanInterestTransactionType, StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    private static bool IsOracleAllowanceTransaction(PayrollTransaction transaction)
        => transaction.TransactionType.Equals("Allowance", StringComparison.OrdinalIgnoreCase) ||
           transaction.TransactionType.Equals(PromotionAllowanceArrearsTransactionType, StringComparison.OrdinalIgnoreCase) ||
           transaction.TransactionType.Equals(BonusTransactionType, StringComparison.OrdinalIgnoreCase) ||
           transaction.Amount > 0 && !IsOracleDeductionTransaction(transaction);

    private static bool IsOracleDeductionTransaction(PayrollTransaction transaction)
        => transaction.TransactionType.Equals("Deduction", StringComparison.OrdinalIgnoreCase) ||
           transaction.TransactionType.Equals(IncomeTaxTransactionType, StringComparison.OrdinalIgnoreCase) ||
           transaction.TransactionType.Equals(EmployeePensionTransactionType, StringComparison.OrdinalIgnoreCase) ||
           transaction.TransactionType.Equals(LoanRepaymentTransactionType, StringComparison.OrdinalIgnoreCase) ||
           transaction.TransactionType.Equals(LoanInterestTransactionType, StringComparison.OrdinalIgnoreCase) ||
           transaction.TransactionType.Equals(SalaryAdvanceTransactionType, StringComparison.OrdinalIgnoreCase) ||
           transaction.TransactionType.Equals(BackpayDeductionArrearsTransactionType, StringComparison.OrdinalIgnoreCase);

    private static decimal GetOracleDecimal(PayrollOracleReportRowDto row, string key)
        => row.Values.TryGetValue(key, out var value) && value is decimal amount ? amount : 0m;

    private static IReadOnlyList<PayrollOracleReportRowDto> BuildPayeRows(IReadOnlyList<PayrollRunEmployee> employees, string variantCode)
    {
        var rows = employees
            .Select((employee, index) =>
            {
                var allowance = employee.TaxableAllowances + employee.NonTaxableAllowances;
                if (variantCode == "MPR")
                {
                    return OracleRow(new Dictionary<string, object?>
                    {
                        ["employeeNumber"] = employee.EmployeeNumber,
                        ["employeeName"] = employee.EmployeeName,
                        ["employeeTin"] = string.Empty,
                        ["basicSalary"] = RoundMoney(employee.BasicSalary),
                        ["basic"] = RoundMoney(0m),
                        ["grossAllowance"] = RoundMoney(allowance),
                        ["arrearsAllowance"] = RoundMoney(0m),
                        ["excessBonus"] = RoundMoney(0m),
                        ["totalCashEmolument"] = RoundMoney(employee.GrossIncome),
                        ["benefitsInKind"] = RoundMoney(0m),
                        ["bonusInThreshold"] = RoundMoney(0m),
                        ["totalEmolument"] = RoundMoney(employee.GrossIncome),
                        ["deductableReliefs"] = RoundMoney(employee.TaxRelief),
                        ["employeeSsf"] = RoundMoney(employee.EmployeeContribution),
                        ["employeeArrearsSsf"] = RoundMoney(0m),
                        ["employeeTotalSsf"] = RoundMoney(employee.EmployeeContribution),
                        ["offPayrollExcessBonus"] = RoundMoney(0m),
                        ["taxableIncome"] = RoundMoney(employee.TaxableIncome),
                        ["normalTax"] = RoundMoney(employee.IncomeTax),
                        ["arrearsTax"] = RoundMoney(0m),
                        ["bonusTax"] = RoundMoney(0m),
                        ["totalTaxCharge"] = RoundMoney(employee.IncomeTax)
                    });
                }

                var totalContribution = employee.EmployeeContribution + employee.EmployerContribution;
                var totalReliefs = employee.TaxRelief + employee.EmployeeContribution;
                var totalAssessableIncome = employee.GrossIncome;
                return OracleRow(new Dictionary<string, object?>
                {
                    ["rowNumber"] = index + 1,
                    ["nameOfEmployee"] = employee.EmployeeName,
                    ["tin"] = string.Empty,
                    ["position"] = string.Empty,
                    ["resident"] = "N",
                    ["basicSalary"] = RoundMoney(employee.BasicSalary),
                    ["secondaryEmployment"] = "N",
                    ["socialSecurityFund"] = RoundMoney(employee.EmployeeContribution),
                    ["thirdTier"] = RoundMoney(0m),
                    ["cashAllowances"] = RoundMoney(allowance),
                    ["bonusIncome"] = RoundMoney(0m),
                    ["finalTaxOnBonus"] = RoundMoney(0m),
                    ["excessBonus"] = RoundMoney(0m),
                    ["totalCashEmolument"] = RoundMoney(employee.BasicSalary + allowance),
                    ["accomodationElement"] = RoundMoney(0m),
                    ["vehicleElement"] = RoundMoney(0m),
                    ["nonCashBenefit"] = RoundMoney(0m),
                    ["totalAssessableIncome"] = RoundMoney(totalAssessableIncome),
                    ["deductibleReliefs"] = RoundMoney(employee.TaxRelief),
                    ["totalReliefs"] = RoundMoney(totalReliefs),
                    ["chargeableIncome"] = RoundMoney(Math.Max(0m, totalAssessableIncome - totalReliefs)),
                    ["taxDeductible"] = RoundMoney(employee.IncomeTax),
                    ["overtimeIncome"] = RoundMoney(0m),
                    ["overtimeTax"] = RoundMoney(0m),
                    ["graTaxPayable"] = RoundMoney(employee.IncomeTax),
                    ["severancePayPaid"] = RoundMoney(0m),
                    ["remarks"] = string.Empty,
                    ["totalContribution"] = RoundMoney(totalContribution)
                });
            })
            .ToList();

        if (variantCode == "MPR")
        {
            rows.Add(OracleRow(new Dictionary<string, object?>
            {
                ["oracleRowType"] = "TOTAL",
                ["employeeNumber"] = $"COUNT : {employees.Count}",
                ["employeeName"] = "TOTAL",
                ["basicSalary"] = RoundMoney(employees.Sum(e => e.BasicSalary)),
                ["basic"] = RoundMoney(0m),
                ["grossAllowance"] = RoundMoney(employees.Sum(e => e.TaxableAllowances + e.NonTaxableAllowances)),
                ["arrearsAllowance"] = RoundMoney(0m),
                ["excessBonus"] = RoundMoney(0m),
                ["totalCashEmolument"] = RoundMoney(employees.Sum(e => e.GrossIncome)),
                ["benefitsInKind"] = RoundMoney(0m),
                ["bonusInThreshold"] = RoundMoney(0m),
                ["totalEmolument"] = RoundMoney(employees.Sum(e => e.GrossIncome)),
                ["deductableReliefs"] = RoundMoney(employees.Sum(e => e.TaxRelief)),
                ["employeeSsf"] = RoundMoney(employees.Sum(e => e.EmployeeContribution)),
                ["employeeArrearsSsf"] = RoundMoney(0m),
                ["employeeTotalSsf"] = RoundMoney(employees.Sum(e => e.EmployeeContribution)),
                ["offPayrollExcessBonus"] = RoundMoney(0m),
                ["taxableIncome"] = RoundMoney(employees.Sum(e => e.TaxableIncome)),
                ["normalTax"] = RoundMoney(employees.Sum(e => e.IncomeTax)),
                ["arrearsTax"] = RoundMoney(0m),
                ["bonusTax"] = RoundMoney(0m),
                ["totalTaxCharge"] = RoundMoney(employees.Sum(e => e.IncomeTax))
            }));

            return rows;
        }

        rows.Add(OracleRow(new Dictionary<string, object?>
        {
            ["oracleRowType"] = "TOTAL",
            ["rowNumber"] = $"Total Count  :  {employees.Count}",
            ["basicSalary"] = RoundMoney(employees.Sum(e => e.BasicSalary)),
            ["socialSecurityFund"] = RoundMoney(employees.Sum(e => e.EmployeeContribution)),
            ["thirdTier"] = RoundMoney(0m),
            ["cashAllowances"] = RoundMoney(employees.Sum(e => e.TaxableAllowances + e.NonTaxableAllowances)),
            ["bonusIncome"] = RoundMoney(0m),
            ["finalTaxOnBonus"] = RoundMoney(0m),
            ["excessBonus"] = RoundMoney(0m),
            ["totalCashEmolument"] = RoundMoney(employees.Sum(e => e.BasicSalary + e.TaxableAllowances + e.NonTaxableAllowances)),
            ["accomodationElement"] = RoundMoney(0m),
            ["vehicleElement"] = RoundMoney(0m),
            ["nonCashBenefit"] = RoundMoney(0m),
            ["totalAssessableIncome"] = RoundMoney(employees.Sum(e => e.GrossIncome)),
            ["deductibleReliefs"] = RoundMoney(employees.Sum(e => e.TaxRelief)),
            ["totalReliefs"] = RoundMoney(employees.Sum(e => e.TaxRelief + e.EmployeeContribution)),
            ["chargeableIncome"] = RoundMoney(employees.Sum(e => Math.Max(0m, e.GrossIncome - e.TaxRelief - e.EmployeeContribution))),
            ["taxDeductible"] = RoundMoney(employees.Sum(e => e.IncomeTax)),
            ["overtimeIncome"] = RoundMoney(0m),
            ["overtimeTax"] = RoundMoney(0m),
            ["graTaxPayable"] = RoundMoney(employees.Sum(e => e.IncomeTax)),
            ["severancePayPaid"] = RoundMoney(0m)
        }));

        return rows;
    }

    private static IReadOnlyList<PayrollOracleReportRowDto> BuildAnnualTaxReturnRows(
        PayrollRun run,
        PayrollPayslipBuildContext context,
        IReadOnlyList<PayrollRunEmployee> employees,
        IReadOnlyList<PayrollRun> annualRuns,
        string variantCode)
    {
        var employeeIds = employees.Select(e => e.EmployeeId).ToHashSet();
        var filterEmployeesById = employees
            .GroupBy(e => e.EmployeeId)
            .ToDictionary(e => e.Key, e => e.First());
        var transactionsByRunEmployeeId = annualRuns
            .SelectMany(e => e.Transactions)
            .GroupBy(e => e.PayrollRunEmployeeId)
            .ToDictionary(e => e.Key, e => (IReadOnlyList<PayrollTransaction>)e.ToList());

        var rows = annualRuns
            .SelectMany(annualRun => annualRun.Employees.Select(employee => new { annualRun, employee }))
            .Where(e => employeeIds.Contains(e.employee.EmployeeId))
            .GroupBy(e => e.employee.EmployeeId)
            .OrderBy(e => filterEmployeesById.TryGetValue(e.Key, out var employee) ? employee.EmployeeNumber : e.First().employee.EmployeeNumber)
            .Select(group =>
            {
                var representative = filterEmployeesById.TryGetValue(group.Key, out var selectedEmployee)
                    ? selectedEmployee
                    : group.OrderByDescending(e => e.annualRun.PayPeriodTo).First().employee;
                var profile = ResolveOracleProfile(context, representative);
                var annualEmployees = group.Select(e => e.employee).ToList();
                var transactions = group
                    .SelectMany(e => transactionsByRunEmployeeId.TryGetValue(e.employee.Id, out var lines) ? lines : [])
                    .ToList();
                var taxableOvertime = SumTransactionsByType(transactions, OvertimeTransactionType);
                var taxableBenefits = SumTransactionsByType(transactions, "Benefit");
                var bonus = SumTransactionsByType(transactions, BonusTransactionType);
                var incomeTax = annualEmployees.Sum(e => e.IncomeTax);
                var taxPaid = incomeTax;
                var values = new Dictionary<string, object?>
                {
                    ["employeeNumber"] = representative.EmployeeNumber,
                    ["employeeName"] = representative.EmployeeName,
                    ["tin"] = ResolveOracleTin(profile),
                    ["position"] = ResolveOraclePositionLabel(profile),
                    ["ssfNo"] = profile?.SsfNumber ?? profile?.Employee.SocialSecurityNumber ?? string.Empty,
                    ["department"] = ResolveOracleDepartmentLabel(profile),
                    ["location"] = ResolveOracleLocationLabel(profile),
                    ["basicSalary"] = RoundMoney(annualEmployees.Sum(e => e.BasicSalary)),
                    ["taxableAllowances"] = RoundMoney(annualEmployees.Sum(e => e.TaxableAllowances)),
                    ["taxableOvertime"] = RoundMoney(taxableOvertime),
                    ["bonus"] = RoundMoney(bonus),
                    ["taxableBenefits"] = RoundMoney(taxableBenefits),
                    ["employeeSsf"] = RoundMoney(annualEmployees.Sum(e => e.EmployeeContribution)),
                    ["taxRelief"] = RoundMoney(annualEmployees.Sum(e => e.TaxRelief)),
                    ["taxableIncome"] = RoundMoney(annualEmployees.Sum(e => e.TaxableIncome)),
                    ["incomeTax"] = RoundMoney(incomeTax),
                    ["taxPaid"] = RoundMoney(taxPaid),
                    ["netTax"] = RoundMoney(Math.Max(0m, incomeTax - taxPaid)),
                    ["taxYear"] = run.PayPeriodTo.Year
                };

                return OracleRow(values);
            })
            .ToList();

        rows.Add(OracleRow(new Dictionary<string, object?>
        {
            ["oracleRowType"] = "TOTAL",
            ["employeeNumber"] = $"COUNT : {rows.Count}",
            ["employeeName"] = "TOTAL",
            ["basicSalary"] = RoundMoney(rows.Sum(e => GetOracleDecimal(e, "basicSalary"))),
            ["taxableAllowances"] = RoundMoney(rows.Sum(e => GetOracleDecimal(e, "taxableAllowances"))),
            ["taxableOvertime"] = RoundMoney(rows.Sum(e => GetOracleDecimal(e, "taxableOvertime"))),
            ["bonus"] = RoundMoney(rows.Sum(e => GetOracleDecimal(e, "bonus"))),
            ["taxableBenefits"] = RoundMoney(rows.Sum(e => GetOracleDecimal(e, "taxableBenefits"))),
            ["employeeSsf"] = RoundMoney(rows.Sum(e => GetOracleDecimal(e, "employeeSsf"))),
            ["taxRelief"] = RoundMoney(rows.Sum(e => GetOracleDecimal(e, "taxRelief"))),
            ["taxableIncome"] = RoundMoney(rows.Sum(e => GetOracleDecimal(e, "taxableIncome"))),
            ["incomeTax"] = RoundMoney(rows.Sum(e => GetOracleDecimal(e, "incomeTax"))),
            ["taxPaid"] = RoundMoney(rows.Sum(e => GetOracleDecimal(e, "taxPaid"))),
            ["netTax"] = RoundMoney(rows.Sum(e => GetOracleDecimal(e, "netTax")))
        }));

        return rows;
    }

    private static IReadOnlyList<PayrollOracleReportRowDto> BuildStaffListRows(
        PayrollPayslipBuildContext context,
        IReadOnlyList<PayrollRunEmployee> employees,
        string variantCode)
    {
        static string GroupLabel(PayrollEmployeeProfile? profile, string variant) => variant switch
        {
            "CAT" => ResolveOracleStaffCategoryLabel(profile),
            "GEN" => ResolveOracleGenderLabel(profile),
            _ => ResolveOracleDepartmentLabel(profile)
        };

        var rows = employees
            .Select(employee =>
            {
                var profile = ResolveOracleProfile(context, employee);
                return OracleRow(new Dictionary<string, object?>
                {
                    ["group"] = GroupLabel(profile, variantCode),
                    ["employeeNumber"] = employee.EmployeeNumber,
                    ["employeeName"] = employee.EmployeeName,
                    ["dateEmployed"] = FormatOracleDate(profile?.Employee.DateEmployed),
                    ["gender"] = ResolveOracleGenderLabel(profile),
                    ["department"] = ResolveOracleDepartmentLabel(profile),
                    ["section"] = ResolveOracleSectionLabel(profile),
                    ["position"] = ResolveOraclePositionLabel(profile),
                    ["location"] = ResolveOracleLocationLabel(profile),
                    ["grade"] = ResolveOracleGradeLabel(profile),
                    ["staffCategory"] = ResolveOracleStaffCategoryLabel(profile),
                    ["region"] = ResolveOracleRegionLabel(profile),
                    ["basicSalary"] = RoundMoney(employee.BasicSalary),
                    ["status"] = profile?.PayrollActive == false ? "Inactive" : profile?.Employee.StaffStatus.ToString() ?? "Active"
                });
            })
            .OrderBy(e => rowText(e, "group"))
            .ThenBy(e => rowText(e, "employeeNumber"))
            .ToList();

        rows.Add(OracleRow(new Dictionary<string, object?>
        {
            ["oracleRowType"] = "TOTAL",
            ["group"] = "TOTAL",
            ["employeeNumber"] = $"COUNT : {employees.Count}",
            ["employeeName"] = "Staff on Roll",
            ["basicSalary"] = RoundMoney(employees.Sum(e => e.BasicSalary))
        }));

        return rows;

        static string rowText(PayrollOracleReportRowDto row, string key)
            => row.Values.TryGetValue(key, out var value) ? value?.ToString() ?? string.Empty : string.Empty;
    }

    private static IReadOnlyList<PayrollOracleReportRowDto> BuildLoanStatementRows(
        PayrollRun run,
        PayrollPayslipBuildContext context,
        PayrollOracleReportRequestDto dto,
        IReadOnlyList<PayrollRunEmployee> employees,
        string variantCode)
    {
        var loanType = NormalizeOracleOptionalFilter(dto.LoanType) ?? NormalizeOracleOptionalFilter(dto.ComponentCodeFrom);
        var facilityNumber = NormalizeOracleOptionalFilter(dto.FacilityNumber);
        var employeeIds = employees.Select(e => e.EmployeeId).ToHashSet();
        var profiles = employees
            .Select(e => ResolveOracleProfile(context, e))
            .Where(e => e != null)
            .GroupBy(e => e!.Id)
            .Select(e => e.First()!)
            .ToList();
        var loans = profiles
            .SelectMany(profile => profile.Loans.Select(loan => new { Profile = profile, Loan = loan }))
            .Where(e => employeeIds.Contains(e.Profile.EmployeeId))
            .Where(e => e.Loan.DateGranted.Date <= run.PayPeriodTo.Date)
            .Where(e => loanType == null ||
                        string.Equals(e.Loan.LoanTypeCode, loanType, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(e.Loan.LoanPolicy?.Code, loanType, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(e.Loan.LoanPolicy?.Name, loanType, StringComparison.OrdinalIgnoreCase))
            .Where(e => facilityNumber == null || e.Loan.FacilityNumber.Equals(facilityNumber, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Profile.EmployeeNumber)
            .ThenBy(e => e.Loan.LoanTypeCode)
            .ThenBy(e => e.Loan.FacilityNumber)
            .ToList();

        if (variantCode == "D")
        {
            return loans
                .Select(row =>
                {
                    var loan = row.Loan;
                    var paid = loan.Schedules.Sum(e => e.AmountPaid + e.InterestPaid);
                    return OracleRow(new Dictionary<string, object?>
                    {
                        ["employeeNumber"] = row.Profile.EmployeeNumber,
                        ["employeeName"] = row.Profile.Employee.FullName,
                        ["loanType"] = ResolveOracleLoanTypeLabel(loan),
                        ["facilityNumber"] = loan.FacilityNumber,
                        ["dateGranted"] = FormatOracleDate(loan.DateGranted),
                        ["paymentStartDate"] = FormatOracleDate(loan.PaymentStartDate),
                        ["paymentEndDate"] = FormatOracleDate(loan.PaymentEndDate),
                        ["amountGranted"] = RoundMoney(loan.AmountGranted),
                        ["monthlyRepayment"] = RoundMoney(loan.MonthlyRepaymentAmount),
                        ["interestRepayment"] = RoundMoney(loan.InterestRepaymentAmount),
                        ["totalPaid"] = RoundMoney(paid),
                        ["outstandingBalance"] = RoundMoney(loan.OutstandingBalance),
                        ["status"] = loan.Status
                    });
                })
                .ToList();
        }

        var rows = new List<PayrollOracleReportRowDto>();
        foreach (var row in loans)
        {
            var loan = row.Loan;
            var cumulativePaid = 0m;
            foreach (var schedule in loan.Schedules.OrderBy(e => e.RepaymentDate).ThenBy(e => e.SequenceNo))
            {
                var paid = schedule.AmountPaid + schedule.InterestPaid;
                if (paid <= 0m)
                {
                    continue;
                }

                cumulativePaid += paid;
                var openingDebt = loan.AmountGranted + loan.TotalInterest;
                rows.Add(OracleRow(new Dictionary<string, object?>
                {
                    ["employeeNumber"] = row.Profile.EmployeeNumber,
                    ["employeeName"] = row.Profile.Employee.FullName,
                    ["loanType"] = ResolveOracleLoanTypeLabel(loan),
                    ["facilityNumber"] = loan.FacilityNumber,
                    ["dateGranted"] = FormatOracleDate(loan.DateGranted),
                    ["paymentStartDate"] = FormatOracleDate(loan.PaymentStartDate),
                    ["paymentEndDate"] = FormatOracleDate(loan.PaymentEndDate),
                    ["numberOfRepayments"] = loan.NumberOfRepayments,
                    ["repaymentDate"] = FormatOracleDate(schedule.RepaymentDate),
                    ["repaymentAmount"] = RoundMoney(schedule.PrincipalAmount),
                    ["interestAmount"] = RoundMoney(schedule.InterestAmount),
                    ["amountPaid"] = RoundMoney(schedule.AmountPaid),
                    ["interestPaid"] = RoundMoney(schedule.InterestPaid),
                    ["cumulativePaid"] = RoundMoney(cumulativePaid),
                    ["outstandingBalance"] = RoundMoney(Math.Max(0m, openingDebt - cumulativePaid))
                }));
            }
        }

        rows.Add(OracleRow(new Dictionary<string, object?>
        {
            ["oracleRowType"] = "TOTAL",
            ["employeeNumber"] = $"COUNT : {loans.Count}",
            ["employeeName"] = "TOTAL",
            ["repaymentAmount"] = RoundMoney(rows.Sum(e => GetOracleDecimal(e, "repaymentAmount"))),
            ["interestAmount"] = RoundMoney(rows.Sum(e => GetOracleDecimal(e, "interestAmount"))),
            ["amountPaid"] = RoundMoney(rows.Sum(e => GetOracleDecimal(e, "amountPaid"))),
            ["interestPaid"] = RoundMoney(rows.Sum(e => GetOracleDecimal(e, "interestPaid")))
        }));

        return rows;
    }

    private static IReadOnlyList<PayrollOracleReportRowDto> BuildSsfRows(
        PayrollPayslipBuildContext context,
        IReadOnlyList<PayrollRunEmployee> employees,
        string variantCode)
    {
        var ssfEmployees = employees
            .Select(employee => (Employee: employee, Profile: ResolveOracleProfile(context, employee)))
            .Where(e => e.Profile?.SsfApplicable == true || e.Employee.EmployeeContribution != 0 || e.Employee.EmployerContribution != 0)
            .ToList();

        var rows = ssfEmployees
            .Select(e =>
            {
                var total = e.Employee.EmployeeContribution + e.Employee.EmployerContribution;
                var firstTier = variantCode is "REP1" or "REP1A"
                    ? total
                    : RoundMoney(total * 13.5m / 18.5m);
                var secondTier = variantCode == "REP2"
                    ? total
                    : RoundMoney(total - firstTier);

                return OracleRow(new Dictionary<string, object?>
                {
                    ["employeeNumber"] = e.Employee.EmployeeNumber,
                    ["employeeName"] = e.Employee.EmployeeName,
                    ["ssfNo"] = e.Profile?.SsfNumber ?? e.Profile?.Employee.SocialSecurityNumber ?? string.Empty,
                    ["basic"] = RoundMoney(e.Employee.BasicSalary),
                    ["employeeSsf"] = RoundMoney(e.Employee.EmployeeContribution),
                    ["employerSsf"] = RoundMoney(e.Employee.EmployerContribution),
                    ["total"] = RoundMoney(total),
                    ["firstTier"] = RoundMoney(firstTier),
                    ["secondTier"] = RoundMoney(secondTier),
                    ["totalContribution"] = RoundMoney(total)
                });
            })
            .ToList();

        rows.Add(OracleRow(new Dictionary<string, object?>
        {
            ["oracleRowType"] = "TOTAL",
            ["employeeNumber"] = $"COUNT : {ssfEmployees.Count}",
            ["employeeName"] = "TOTAL",
            ["basic"] = RoundMoney(ssfEmployees.Sum(e => e.Employee.BasicSalary)),
            ["employeeSsf"] = RoundMoney(ssfEmployees.Sum(e => e.Employee.EmployeeContribution)),
            ["employerSsf"] = RoundMoney(ssfEmployees.Sum(e => e.Employee.EmployerContribution)),
            ["total"] = RoundMoney(ssfEmployees.Sum(e => e.Employee.EmployeeContribution + e.Employee.EmployerContribution)),
            ["firstTier"] = RoundMoney(ssfEmployees.Sum(e =>
            {
                var total = e.Employee.EmployeeContribution + e.Employee.EmployerContribution;
                return variantCode is "REP1" or "REP1A" ? total : RoundMoney(total * 13.5m / 18.5m);
            })),
            ["secondTier"] = RoundMoney(ssfEmployees.Sum(e =>
            {
                var total = e.Employee.EmployeeContribution + e.Employee.EmployerContribution;
                var firstTier = variantCode is "REP1" or "REP1A" ? total : RoundMoney(total * 13.5m / 18.5m);
                return variantCode == "REP2" ? total : RoundMoney(total - firstTier);
            })),
            ["totalContribution"] = RoundMoney(ssfEmployees.Sum(e => e.Employee.EmployeeContribution + e.Employee.EmployerContribution))
        }));

        return rows;
    }

    private static IReadOnlyList<PayrollOracleReportRowDto> BuildBankAdviceRows(
        PayrollRun run,
        PayrollPayslipBuildContext context,
        PayrollOracleReportRequestDto dto,
        IReadOnlyList<PayrollRunEmployee> employees,
        string variantCode)
    {
        var payments = BuildOracleBankPaymentRows(run, context, dto, employees);
        if (IsBankAdviceEmployeeVariant(variantCode))
        {
            var companyName = BuildCompanyName(context);
            var companyAddress = BuildCompanyAddress(context) ?? string.Empty;
            var companyPhone = BuildCompanyPhone(context) ?? string.Empty;

            return payments
                .OrderBy(e => e.BankName)
                .ThenBy(e => e.BranchName)
                .ThenBy(e => e.Employee.EmployeeNumber)
                .Select(e => OracleRow(new Dictionary<string, object?>
                {
                    ["companyName"] = companyName,
                    ["companyAddress"] = companyAddress,
                    ["companyPhone"] = companyPhone,
                    ["bankName"] = e.BankName,
                    ["bankBranch"] = e.BranchName ?? string.Empty,
                    ["staffId"] = e.Employee.EmployeeNumber,
                    ["name"] = e.Employee.EmployeeName,
                    ["acctNo"] = e.AccountNumber ?? string.Empty,
                    ["accountNo"] = e.AccountNumber ?? string.Empty,
                    ["net"] = RoundMoney(e.Amount),
                    ["netPay"] = RoundMoney(e.Amount),
                    ["currencyCode"] = e.CurrencyCode,
                    ["sourceReportVariant"] = variantCode
                }))
                .ToList();
        }

        return payments
            .GroupBy(e => e.BankName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(e => e.Key)
            .Select(group => OracleRow(new Dictionary<string, object?>
            {
                ["bankName"] = group.Key,
                ["accountCount"] = group.Count(),
                ["total"] = RoundMoney(group.Sum(e => e.Amount))
            }))
            .ToList();
    }

    private static IReadOnlyList<OracleBankPaymentRow> BuildOracleBankPaymentRows(
        PayrollRun run,
        PayrollPayslipBuildContext context,
        PayrollOracleReportRequestDto dto,
        IReadOnlyList<PayrollRunEmployee> employees)
        => BuildOracleBankPaymentRowsForAmounts(
            run,
            context,
            dto,
            employees.Select(e => new OracleEmployeePaymentBase(e, e.NetIncome)).ToList());

    private static IReadOnlyList<OracleBankPaymentRow> BuildOracleBankPaymentRowsForAmounts(
        PayrollRun run,
        PayrollPayslipBuildContext context,
        PayrollOracleReportRequestDto dto,
        IReadOnlyList<OracleEmployeePaymentBase> employeePayments)
    {
        var rows = new List<OracleBankPaymentRow>();

        foreach (var employeePayment in employeePayments.Where(e => e.Amount != 0m))
        {
            var employee = employeePayment.Employee;
            var paymentAmount = employeePayment.Amount;
            var profile = ResolveOracleProfile(context, employee);
            if (profile == null)
            {
                continue;
            }

            var payrollMethods = profile.PaymentMethods
                .Where(e => e.IsActive &&
                            e.PaymentType.Equals("Bank", StringComparison.OrdinalIgnoreCase) &&
                            IsEffective(e.StartDate, e.EndDate, run.PayPeriodFrom, run.PayPeriodTo))
                .OrderBy(e => e.SequenceNo)
                .ToList();

            if (payrollMethods.Count > 0)
            {
                rows.AddRange(BuildPaymentAllocations(payrollMethods, paymentAmount, employee.CurrencyCode).Select(allocation =>
                {
                    var method = allocation.Method;
                    var bankName = ResolvePaymentMethodBankName(method, context) ?? "BANK";
                    var branchName = ResolvePaymentMethodBankBranchName(method, context);
                    return new OracleBankPaymentRow(
                        bankName,
                        method.BankCode,
                        method.BankBranchCode,
                        branchName,
                        method.AccountNumber,
                        string.IsNullOrWhiteSpace(method.CurrencyCode) ? employee.CurrencyCode : method.CurrencyCode,
                        allocation.Amount,
                        employee);
                }));
                continue;
            }

            var bankDetails = profile.Employee.BankDetails
                .Where(e => e.IsActive)
                .OrderByDescending(e => e.IsPrimary)
                .ThenBy(e => e.BankName)
                .ToList();

            rows.AddRange(bankDetails.Select((bank, index) => new OracleBankPaymentRow(
                ResolveEmployeeBankName(bank) ?? "BANK",
                bank.Bank?.Code ?? bank.BankName,
                bank.Branch?.Code ?? bank.Branch?.Name,
                ResolveEmployeeBankBranchName(bank),
                bank.AccountNumber,
                employee.CurrencyCode,
                bank.AllocationPercentage > 0
                    ? Math.Round(paymentAmount * (bank.AllocationPercentage / 100m), 2)
                    : bankDetails.Count == 1 || index == 0
                        ? paymentAmount
                        : 0m,
                employee)));
        }

        return rows
            .Where(e =>
                IsWithinOracleRange(e.BankCode ?? e.BankName, dto.BankFrom, dto.BankTo) &&
                IsWithinOracleRange(e.BranchCode ?? string.Empty, dto.BranchFrom, dto.BranchTo))
            .ToList();
    }

    private static IReadOnlyList<PayrollOracleReportRowDto> BuildJournalRows(PayrollRun run, IReadOnlyList<PayrollRunEmployee> employees, string variantCode)
    {
        if (variantCode == "D")
        {
            var employeeIds = employees.Select(e => e.Id).ToHashSet();
            var detailRows = run.Transactions
                .Where(e => employeeIds.Contains(e.PayrollRunEmployeeId))
                .OrderBy(e => e.EmployeeNumber)
                .ThenBy(e => e.TransactionType)
                .ThenBy(e => e.ComponentCode)
                .Select(transaction =>
                {
                    var line = FindJournalLineForTransaction(run, transaction);
                    var debitCredit = line?.DebitCredit ?? InferDebitCredit(transaction.TransactionType);
                    var amount = Math.Abs(transaction.EmployerAmount ?? transaction.Amount);
                    return OracleRow(new Dictionary<string, object?>
                    {
                        ["accountNumber"] = line?.AccountCode ?? "UNMAPPED",
                        ["accountDescription"] = transaction.Description ?? line?.Description ?? transaction.TransactionType,
                        ["debit"] = RoundMoney(debitCredit.Equals("DR", StringComparison.OrdinalIgnoreCase) ? amount : 0m),
                        ["credit"] = RoundMoney(debitCredit.Equals("CR", StringComparison.OrdinalIgnoreCase) ? amount : 0m)
                    });
                })
                .ToList();

            detailRows.Add(BuildOracleJournalTotalRow(detailRows));
            return detailRows;
        }

        var summaryRows = run.JournalLines
            .OrderBy(e => e.AccountCode)
            .ThenBy(e => e.Description)
            .GroupBy(e => new { e.AccountCode, e.Description })
            .Select(group => OracleRow(new Dictionary<string, object?>
            {
                ["accountNumber"] = group.Key.AccountCode,
                ["accountDescription"] = group.Key.Description,
                ["debit"] = RoundMoney(group.Where(e => e.DebitCredit.Equals("DR", StringComparison.OrdinalIgnoreCase)).Sum(e => e.Amount)),
                ["credit"] = RoundMoney(group.Where(e => e.DebitCredit.Equals("CR", StringComparison.OrdinalIgnoreCase)).Sum(e => e.Amount))
            }))
            .ToList();
        summaryRows.Add(BuildOracleJournalTotalRow(summaryRows));
        return summaryRows;
    }

    private static PayrollOracleReportRowDto BuildOracleJournalTotalRow(IReadOnlyList<PayrollOracleReportRowDto> rows)
    {
        static decimal GetAmount(PayrollOracleReportRowDto row, string key)
            => row.Values.TryGetValue(key, out var value) && value is decimal amount ? amount : 0m;

        return OracleRow(new Dictionary<string, object?>
        {
            ["oracleRowType"] = "TOTAL",
            ["accountDescription"] = "Total",
            ["debit"] = RoundMoney(rows.Sum(row => GetAmount(row, "debit"))),
            ["credit"] = RoundMoney(rows.Sum(row => GetAmount(row, "credit")))
        });
    }

    private static PayrollJournalLine? FindJournalLineForTransaction(PayrollRun run, PayrollTransaction transaction)
        => run.JournalLines.FirstOrDefault(e =>
               e.TransactionType.Equals(transaction.TransactionType, StringComparison.OrdinalIgnoreCase) &&
               (transaction.ComponentCode == null || e.Description.Contains(transaction.ComponentCode, StringComparison.OrdinalIgnoreCase)))
           ?? run.JournalLines.FirstOrDefault(e => e.TransactionType.Equals(transaction.TransactionType, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<PayrollOracleReportParameterDto> BuildOracleReportParameters(
        PayrollRun run,
        PayrollPayslipBuildContext context,
        PayrollOracleReportRequestDto dto,
        string reportCode)
    {
        var companyCode = TrimOrNull(context.CompanyProfile?.CompanyCode)
                          ?? TrimOrNull(context.CompanyProfile?.LegacyCompanyCode)
                          ?? "001";
        var parameters = new List<PayrollOracleReportParameterDto>
        {
            OracleParameter("P_PERIOD", reportCode == "REP3_034" ? "Tax Year" : "Pay Period", ResolveOracleReportPeriodLabel(reportCode, run)),
            OracleParameter("P_COMPCODE", "Company", companyCode)
        };

        if (reportCode == "REP3_002")
        {
            parameters.AddRange(
            [
                OracleParameter("P_REGION", "Region", TrimOrNull(dto.RegionCode) ?? "ALL"),
                OracleParameter("P_DEPARTMENT", "Department", TrimOrNull(dto.DepartmentCode) ?? "ALL")
            ]);
        }

        if (reportCode is "REP3_009" or "REP3_031")
        {
            parameters.AddRange(
            [
                OracleParameter("P_CURR_CODE", "Currency", TrimOrNull(dto.ReportingCurrency) ?? run.CurrencyCode),
                OracleParameter("P_ACCT_NO", "Account No", dto.AccountNumber),
                OracleParameter("P_BANK_CODE", "Bank", dto.BankFrom ?? "0"),
                OracleParameter("P_BRANCH_CODE", "Branch", dto.BranchFrom ?? "0"),
                OracleParameter("P_SIG1", "Signatory 1", dto.Signer1),
                OracleParameter("P_SIG2", "Signatory 2", dto.Signer2),
                OracleParameter("P_SIG3", "Signatory 3", dto.Signer3),
                OracleParameter("P_PSN1", "Position 1", dto.Position1),
                OracleParameter("P_PSN2", "Position 2", dto.Position2),
                OracleParameter("P_PSN3", "Position 3", dto.Position3)
            ]);
        }

        if (reportCode == "REP3_010")
        {
            var variantCode = NormalizeOracleVariantCode(reportCode, dto.VariantCode);
            if (variantCode == "ALL")
            {
                parameters.AddRange(
                [
                    OracleParameter("P_ALL_DED_FM", "Allowances / Deductions From", "0"),
                    OracleParameter("P_ALL_DED_TO", "Allowances / Deductions To", "ZZZZZ")
                ]);
            }
            else
            {
                parameters.AddRange(
                [
                    OracleParameter("P_ALL_DED_FM", "Allowances / Deductions From", variantCode),
                    OracleParameter("P_ALL_DED_TO", "Allowances / Deductions To", variantCode)
                ]);
            }

            parameters.AddRange(
            [
                OracleParameter("P_ALW_FM", "Code From", dto.ComponentCodeFrom ?? "0"),
                OracleParameter("P_ALW_TO", "Code To", dto.ComponentCodeTo ?? "ZZZZZ"),
                OracleParameter("P_REP_CURRENCY", "Reporting Currency", TrimOrNull(dto.ReportingCurrency) ?? run.CurrencyCode)
            ]);
        }

        if (reportCode is "REP3_011" or "REP3_019" or "REP3_028" or "REP3_029" or "REP3_032" or "REP3_034" or "REP3_036")
        {
            parameters.AddRange(
            [
                OracleParameter("P_DEPT_FM", "Department From", dto.DepartmentFrom ?? "0"),
                OracleParameter("P_DEPT_TO", "Department To", dto.DepartmentTo ?? "ZZZZZ"),
                OracleParameter("P_LOC_FM", "Location From", dto.LocationFrom ?? "0"),
                OracleParameter("P_LOC_TO", "Location To", dto.LocationTo ?? "ZZZZZ"),
                OracleParameter("P_EMPNO_FM", "Employee From", dto.EmployeeNumberFrom ?? "0"),
                OracleParameter("P_EMPNO_TO", "Employee To", dto.EmployeeNumberTo ?? "ZZZZZ")
            ]);
        }

        if (reportCode == "REP3_036")
        {
            parameters.AddRange(
            [
                OracleParameter("P_LOANTYPE", "Loan Type", NormalizeOracleOptionalFilter(dto.LoanType) ?? NormalizeOracleOptionalFilter(dto.ComponentCodeFrom) ?? "ALL"),
                OracleParameter("P_FACILITYNO", "Facility No", NormalizeOracleOptionalFilter(dto.FacilityNumber) ?? "ALL"),
                OracleParameter("P_CATEGORY", "Category", NormalizeOracleVariantCode(reportCode, dto.VariantCode))
            ]);
        }

        if (reportCode is "REP3_028" or "REP3_029" or "REP3_030" or "REP3_031" or "REP3_032")
        {
            parameters.Add(OracleParameter("P_BONUS_TYPE", "Bonus Type", dto.ComponentCodeFrom));
        }

        if (reportCode == "REP3_035")
        {
            parameters.AddRange(
            [
                OracleParameter("P_DEPT_FM", "Dept From", dto.DepartmentFrom ?? "0"),
                OracleParameter("P_DEPT_TO", "Dept To", dto.DepartmentTo ?? "ZZZZZ"),
                OracleParameter("P_EMPNO_FM", "Employee From", dto.EmployeeNumberFrom ?? "0"),
                OracleParameter("P_EMPNO_TO", "Employee To", dto.EmployeeNumberTo ?? "ZZZZZ")
            ]);
        }

        if (reportCode == "REP3_007")
        {
            parameters.Add(OracleParameter("P_REP_CURRENCY", "Reporting Currency", TrimOrNull(dto.ReportingCurrency) ?? run.CurrencyCode));
        }

        return parameters;
    }

    private static (string ReportName, string SourceForm, string SourceReport, string VariantName) ResolveOracleReportMetadata(string reportCode, string variantCode)
        => reportCode switch
        {
            "REP3_001" => ("Payroll Register Report", "REP3_001.fmb", variantCode switch
            {
                "D" => "REP3_001_DEPT.RDF",
                "B" => "REP3_037.RDF",
                "S" => "REP3_001_SUM.RDF",
                "E" => "REP3_001_SECT.RDF",
                "RAN" => "REP3_001_RAN.RDF",
                "P" => "REP3_039.RDF",
                "N" => "REP3_038.RDF",
                "DR" => "REP3_001_DETAIL.rdf",
                "PD" => "REP3_039_USD.RDF",
                _ => "REP3_001_ALL.RDF"
            }, variantCode switch
            {
                "D" => "Department",
                "B" => "Bank",
                "S" => "Summary",
                "E" => "Section",
                "RAN" => "Range",
                "P" => "Employer Cost",
                "N" => "Net Pay Report",
                "DR" => "Detail Report",
                "PD" => "Employer Cost USD",
                _ => "All"
            }),
            "REP3_006" => ("Monthly PAYE Report", "REP3_006.fmb", variantCode switch
            {
                "GRA" => "REP3_006_GRA.RDF",
                "MPR" => "REP3_006_PWC.rdf",
                _ => "REP3_006.RDF"
            }, variantCode == "GRA" ? "GRA Monthly Payee" : "Monthly Payee"),
            "REP3_007" => ("SSF Report", "REP3_007.fmb", variantCode switch
            {
                "REP1" => "REP3_007_1.rdf",
                "REP2" => "REP3_007_2.rdf",
                "REP1A" => "REP3_007_1A.rdf",
                "REP3" => "REP3_007_3.RDF",
                "FB" => "REP3_001_SSF.RDF",
                _ => "REP3_007.rdf"
            }, variantCode switch
            {
                "REP1" => "1st Tier",
                "REP2" => "2nd Tier",
                "REP1A" => "1st Tier New",
                "REP3" => "Tier3 Contribution",
                "FB" => "Format B",
                _ => "SSF Report"
            }),
            "REP3_002" => ("Payroll Analysis - Month", "REP3_002.fmb", variantCode switch
            {
                "REG" => "ANALYSIS_REG.RDF",
                "IND" => "ANALYSIS_INDV.RDF",
                _ => "ANALYSIS_DEPT.rdf"
            }, variantCode switch
            {
                "REG" => "Region",
                "IND" => "Individual",
                _ => "Department"
            }),
            "REP3_009" => ("Bank Advice By Employer Bank", "REP3_009.fmb", variantCode switch
            {
                "CD" => "REP3_009_GSLTF_USD.RDF",
                "DC" => "REP3_009_USD_CEDI.RDF",
                "DD" => "REP3_009_DOL_DOL.RDF",
                _ => "REP3_009_PHC.RDF"
            }, variantCode switch
            {
                "CD" => "Bank Advice(Cedi to Dollar)",
                "DC" => "Bank Advice(Dollar to Cedi)",
                "DD" => "Bank Advice(Dollar to Dollar)",
                _ => "Bank Advice(Cedi to Cedi)"
            }),
            "REP3_010" => ("Allowances & Deductions Sch.", "REP3_010.fmb", "REP3_010_SLTF.rdf", variantCode switch
            {
                "ALW" => "Allowances",
                "DED" => "Deductions",
                "ADV" => "Advance",
                "REP" => "Loan Repayment",
                "INT" => "Loan Interest",
                _ => "All"
            }),
            "REP3_011" => ("Cash List", "REP3_011.fmb", "CASHLIST.RDF", "Cash List"),
            "REP3_034" => ("Annual Tax Returns", "REP3_034.fmb", variantCode switch
            {
                "A" => "REP3_034_A.RDF",
                "R" => "REP3_034_R.RDF",
                "P" => "REP3_034_P.RDF",
                "L" => "REP3_034_LOC.rdf",
                _ => "REP3_034.RDF"
            }, variantCode switch
            {
                "A" => "Annual Employee Return",
                "R" => "Annual Tax Register",
                "P" => "Employee Tax Certificate",
                "L" => "Annual Tax by Location",
                _ => "Income Tax Deduction Form"
            }),
            "REP3_035" => ("Bank Advice", "BANK_ADV.fmb", variantCode switch
            {
                "BNK" => "REP3_004.RDF",
                "BR1" => "REP3_005_GBC1.RDF",
                "BSU" => "BANK_SUM.RDF",
                "BRA" => "REP3_005_GBC.RDF",
                "REG" => "REP3_020.RDF",
                "ARR" => "REP3_005_ARR.RDF",
                _ => "BANK_SUM.RDF"
            }, variantCode switch
            {
                "BNK" => "Employee Bank",
                "BR1" => "Employee Bank Branch",
                "BSU" => "Bank Summary",
                "BRA" => "Branch Summary",
                "REG" => "Regional Bank Advice",
                "ARR" => "Arreas Advice",
                _ => "Bank Summary"
            }),
            "REP3_036" => ("Loan Statement", "REP3_036.fmb", variantCode switch
            {
                "F" => "REP3_036_A.RDF",
                "T" => "REP3_036_B.RDF",
                "D" => "REP3_036_DETAILS.RDF",
                _ => "REP3_036.RDF"
            }, variantCode switch
            {
                "F" => "Facility Statement",
                "T" => "Loan Type Statement",
                "D" => "Employees Loan Report",
                _ => "Individual Statement"
            }),
            "REP3_305" => ("Staff Lists", "REP3_305.fmb", variantCode switch
            {
                "CAT" => "REP3_305_A.RDF",
                "GEN" => "REP3_305_B.RDF",
                _ => "REP3_305.RDF"
            }, variantCode switch
            {
                "CAT" => "Staff Lists By Category",
                "GEN" => "Staff Lists By Gender",
                _ => "Staff Lists By Department"
            }),
            "REP3_016" => ("Journal Reports", "REP3_016.fmb", variantCode == "D" ? "JOUNAL_DETAIL.rdf" : "JOUNAL_REP.RDF", variantCode == "D" ? "Detail Journal Report" : "Summary Journal Report"),
            "REP3_019" => ("Overtime Reports", "REP3_019.fmb", "REP3_019.RDF", "Overtime Reports"),
            "REP3_028" => ("Bonus Slip", "REP3_028.fmb", "REP3_028.RDF", "Bonus Slip"),
            "REP3_029" => ("Bonus Register", "REP3_029.fmb", "BONUS_REGISTER.rdf", "Bonus Register"),
            "REP3_030" => ("Bonus Tax Report", "REP3_030.fmb", "REP3_030.RDF", "Bonus Tax Report"),
            "REP3_031" => ("Bonus Bank Advice", "REP3_031.fmb", "REP3_031.RDF", "Bonus Bank Advice"),
            "REP3_032" => ("Bonus Cash List", "REP3_032.fmb", "REP3_032.RDF", "Bonus Cash List"),
            _ => ("Payroll Report", reportCode, reportCode, "Default")
        };

    private static string NormalizeOracleReportCode(string? reportCode)
    {
        var normalized = TrimOrNull(reportCode)?.ToUpperInvariant();
        return normalized switch
        {
            "PAYROLL_REGISTER" or "REGISTER" or "A0000401" => "REP3_001",
            "ANALYSIS" or "PAYROLL_ANALYSIS" or "A0000406" => "REP3_002",
            "EMPLOYER_BANK_ADVICE" or "BANK_ADVICE_BY_EMPLOYER_BANK" or "A0000407" => "REP3_009",
            "ALLOWANCES_DEDUCTIONS" or "ALLOWANCES_DEDUCTIONS_SCHEDULE" or "A0000408" => "REP3_010",
            "CASH_LIST" or "CASHLIST" or "A0000410" => "REP3_011",
            "PAYE" or "MONTHLY_PAYE" or "A0000405" => "REP3_006",
            "SSF" or "A0000404" => "REP3_007",
            "BANK" or "BANK_ADVICE" or "A0000402" => "REP3_035",
            "JOURNAL" or "JOURNALS" or "A0000411" => "REP3_016",
            "OVERTIME" or "OVERTIME_REPORTS" or "A0000412" => "REP3_019",
            "ANNUAL_TAX_RETURNS" or "ANNUAL_TAX" or "A0000418" => "REP3_034",
            "STAFF_LIST" or "STAFF_LISTS" or "A0000420" => "REP3_305",
            "LOAN_STATEMENT" or "LOAN_STATEMENTS" or "A0000423" => "REP3_036",
            "BONUS_SLIP" or "A000041402" => "REP3_028",
            "BONUS_REGISTER" or "A0000415" => "REP3_029",
            "BONUS_TAX" or "BONUS_TAX_REPORT" or "A000041403" => "REP3_030",
            "BONUS_BANK_ADVICE" or "A000041404" => "REP3_031",
            "BONUS_CASH_LIST" or "A000041405" => "REP3_032",
            "REP3_002" or "REP3_006" or "REP3_007" or "REP3_009" or "REP3_010" or "REP3_011" or "REP3_034" or "REP3_035" or "REP3_036" or "REP3_305" or "REP3_016" or "REP3_019" or "REP3_028" or "REP3_029" or "REP3_030" or "REP3_031" or "REP3_032" => normalized,
            _ => "REP3_001"
        };
    }

    private static string NormalizeOracleVariantCode(string reportCode, string? variantCode)
    {
        var normalized = TrimOrNull(variantCode)?.ToUpperInvariant();
        return reportCode switch
        {
            "REP3_001" => normalized is "D" or "B" or "S" or "A" or "E" or "RAN" or "P" or "N" or "DR" or "PD" ? normalized : "A",
            "REP3_002" => normalized is "DEP" or "REG" or "IND" ? normalized : "DEP",
            "REP3_006" => normalized is "GRA" or "MPR" ? normalized : "MPR",
            "REP3_007" => normalized is "REP" or "REP1" or "REP2" or "REP1A" or "REP3" or "FB" ? normalized : "REP",
            "REP3_009" => normalized is "CC" or "CD" or "DC" or "DD" ? normalized : "CC",
            "REP3_010" => normalized is "ALL" or "ALW" or "DED" or "ADV" or "REP" or "INT" ? normalized : "ALL",
            "REP3_011" => "CASH",
            "REP3_034" => normalized is "O" or "A" or "R" or "P" or "L" ? normalized : "O",
            "REP3_035" => normalized is "BNK" or "BR1" or "BSU" or "BRA" or "REG" or "ARR" ? normalized : "BNK",
            "REP3_036" => normalized is "I" or "F" or "T" or "D" ? normalized : "I",
            "REP3_305" => normalized is "DEP" or "CAT" or "GEN" ? normalized : "DEP",
            "REP3_016" => normalized == "D" ? "D" : "S",
            "REP3_019" => "OT",
            "REP3_028" => "SLIP",
            "REP3_029" => "REG",
            "REP3_030" => "TAX",
            "REP3_031" => "BANK",
            "REP3_032" => "CASH",
            _ => normalized ?? string.Empty
        };
    }

    private static bool IsBankAdviceEmployeeVariant(string variantCode)
        => variantCode is "BNK" or "BR1" or "BRA" or "REG" or "ARR";

    private static PayrollOracleReportColumnDto OracleColumn(string key, string header, string alignment = "left", string valueType = "text")
        => new()
        {
            Key = key,
            Header = header,
            Alignment = alignment,
            ValueType = valueType
        };

    private static PayrollOracleReportParameterDto OracleParameter(string oracleName, string label, string? value)
        => new()
        {
            OracleName = oracleName,
            Label = label,
            Value = value
        };

    private static PayrollOracleReportRowDto OracleRow(IReadOnlyDictionary<string, object?> values)
        => new()
        {
            Values = values
        };

    private static PayrollEmployeeProfile? ResolveOracleProfile(PayrollPayslipBuildContext context, PayrollRunEmployee employee)
        => context.ProfilesByEmployeeId.TryGetValue(employee.EmployeeId, out var profile) ? profile : null;

    private static string? ResolveOracleDepartmentCode(PayrollEmployeeProfile? profile)
        => TrimOrNull(profile?.Employee.Department?.Code) ?? TrimOrNull(profile?.Employee.Department?.Name);

    private static string ResolveOracleDepartmentLabel(PayrollEmployeeProfile? profile)
    {
        var code = TrimOrNull(profile?.Employee.Department?.Code);
        var name = TrimOrNull(profile?.Employee.Department?.Name);
        return (code, name) switch
        {
            ({ Length: > 0 }, { Length: > 0 }) => $"{code} - {name}",
            ({ Length: > 0 }, _) => code,
            (_, { Length: > 0 }) => name,
            _ => "Unassigned"
        };
    }

    private static string? ResolveOracleLocationCode(PayrollEmployeeProfile? profile)
        => TrimOrNull(profile?.Employee.Location?.Code)
           ?? TrimOrNull(profile?.Employee.Station?.Code)
           ?? TrimOrNull(profile?.Employee.Location?.Name)
           ?? TrimOrNull(profile?.Employee.Station?.Name);

    private static string ResolveOracleLocationLabel(PayrollEmployeeProfile? profile)
    {
        var code = TrimOrNull(profile?.Employee.Location?.Code) ?? TrimOrNull(profile?.Employee.Station?.Code);
        var name = TrimOrNull(profile?.Employee.Location?.Name) ?? TrimOrNull(profile?.Employee.Station?.Name);
        return (code, name) switch
        {
            ({ Length: > 0 }, { Length: > 0 }) => $"{code} - {name}",
            ({ Length: > 0 }, _) => code,
            (_, { Length: > 0 }) => name,
            _ => "Unassigned"
        };
    }

    private static string? ResolveOracleRegionCode(PayrollEmployeeProfile? profile)
        => TrimOrNull(profile?.Employee.State)
           ?? TrimOrNull(profile?.Employee.Location?.Code)
           ?? TrimOrNull(profile?.Employee.Location?.Name);

    private static string ResolveOracleRegionLabel(PayrollEmployeeProfile? profile)
        => TrimOrNull(profile?.Employee.State) ?? ResolveOracleLocationLabel(profile);

    private static string ResolveOracleSectionLabel(PayrollEmployeeProfile? profile)
    {
        var code = TrimOrNull(profile?.Employee.Section?.Code);
        var name = TrimOrNull(profile?.Employee.Section?.Name);
        return (code, name) switch
        {
            ({ Length: > 0 }, { Length: > 0 }) => $"{code} - {name}",
            ({ Length: > 0 }, _) => code,
            (_, { Length: > 0 }) => name,
            _ => "Unassigned"
        };
    }

    private static string ResolveOraclePositionLabel(PayrollEmployeeProfile? profile)
    {
        var code = TrimOrNull(profile?.Employee.Position?.Code);
        var title = TrimOrNull(profile?.Employee.Position?.Title);
        return (code, title) switch
        {
            ({ Length: > 0 }, { Length: > 0 }) => $"{code} - {title}",
            ({ Length: > 0 }, _) => code,
            (_, { Length: > 0 }) => title,
            _ => string.Empty
        };
    }

    private static string ResolveOracleGradeLabel(PayrollEmployeeProfile? profile)
    {
        var code = TrimOrNull(profile?.Employee.Position?.SalaryGrade?.Code);
        var name = TrimOrNull(profile?.Employee.Position?.SalaryGrade?.Name);
        return (code, name) switch
        {
            ({ Length: > 0 }, { Length: > 0 }) => $"{code} - {name}",
            ({ Length: > 0 }, _) => code,
            (_, { Length: > 0 }) => name,
            _ => string.Empty
        };
    }

    private static string ResolveOracleStaffCategoryLabel(PayrollEmployeeProfile? profile)
        => TrimOrNull(profile?.Employee.Position?.StaffLevel?.Name)
           ?? TrimOrNull(profile?.Employee.Position?.StaffLevel?.Code)
           ?? profile?.Employee.EmploymentType.ToString()
           ?? "Unassigned";

    private static string ResolveOracleGenderLabel(PayrollEmployeeProfile? profile)
        => profile?.Employee.Gender?.ToString() ?? "Unassigned";

    private static string ResolveOracleTin(PayrollEmployeeProfile? profile)
        => TrimOrNull(profile?.TinNumber)
           ?? TrimOrNull(profile?.Employee.TINNumber)
           ?? TrimOrNull(profile?.Employee.TaxNumber)
           ?? string.Empty;

    private static string ResolveOracleLoanTypeLabel(PayrollLoan loan)
    {
        var code = TrimOrNull(loan.LoanTypeCode) ?? TrimOrNull(loan.LoanPolicy?.Code);
        var name = TrimOrNull(loan.LoanPolicy?.Name);
        return (code, name) switch
        {
            ({ Length: > 0 }, { Length: > 0 }) => $"{code} - {name}",
            ({ Length: > 0 }, _) => code,
            (_, { Length: > 0 }) => name,
            _ => "Loan"
        };
    }

    private static string ResolveOracleReportPeriodLabel(string reportCode, PayrollRun run)
        => reportCode == "REP3_034"
            ? run.PayPeriodTo.Year.ToString(CultureInfo.InvariantCulture)
            : FormatPayrollPeriodMonth(run.PayPeriodFrom, run.PayPeriodTo);

    private static string FormatOracleDate(DateTime? value)
        => value.HasValue ? value.Value.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture) : string.Empty;

    private static string FormatOracleDate(DateOnly? value)
        => value.HasValue ? value.Value.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture) : string.Empty;

    private static bool IsWithinOracleRange(string? value, string? from, string? to)
    {
        var normalizedValue = TrimOrNull(value);
        var normalizedFrom = NormalizeOracleRangeEndpoint(from);
        var normalizedTo = NormalizeOracleRangeEndpoint(to);

        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            return normalizedFrom == null && normalizedTo == null;
        }

        return (normalizedFrom == null || string.Compare(normalizedValue, normalizedFrom, StringComparison.OrdinalIgnoreCase) >= 0) &&
               (normalizedTo == null || string.Compare(normalizedValue, normalizedTo, StringComparison.OrdinalIgnoreCase) <= 0);
    }

    private static bool HasOracleRange(string? from, string? to)
        => NormalizeOracleRangeEndpoint(from) != null || NormalizeOracleRangeEndpoint(to) != null;

    private static string? NormalizeOracleRangeEndpoint(string? value)
    {
        var normalized = TrimOrNull(value);
        return normalized is null ||
               normalized.Equals("0", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("ZZZZZ", StringComparison.OrdinalIgnoreCase)
            ? null
            : normalized;
    }

    private static string? NormalizeOracleOptionalFilter(string? value)
    {
        var normalized = TrimOrNull(value);
        return normalized is null ||
               normalized.Equals("0", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("ALL", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("ZZZZZ", StringComparison.OrdinalIgnoreCase)
            ? null
            : normalized;
    }

    private static decimal RoundMoney(decimal amount)
        => Math.Round(amount, 2);

    private static string BuildPayrollJournalNumber(PayrollRun run)
        => $"{PayrollJournalNumberPrefix}{run.RunNumber}";

    private static string NormalizePayrollJournalAccountCode(string? value)
        => value?.Trim() ?? string.Empty;

    private static string NormalizePayrollDebitCredit(string? value)
        => value?.Trim().ToUpperInvariant() switch
        {
            "DR" or "D" => "DR",
            "CR" or "C" => "CR",
            _ => string.Empty
        };

    private static string FormatPayrollJournalLineHint(PayrollJournalLine line)
        => $"{line.SequenceNo}:{line.TransactionType}/{line.AccountCode}";

    private static string FormatPayrollJournalLineHint(PayrollJournalLineDto line)
        => $"{line.SequenceNo}:{line.TransactionType}/{line.AccountCode}";

    private static PayrollJournalLineDto BuildPayrollJournalPreviewLine(PayrollJournalLine line)
        => new()
        {
            Id = line.Id,
            SequenceNo = line.SequenceNo,
            TransactionType = line.TransactionType,
            DebitCredit = NormalizePayrollDebitCredit(line.DebitCredit),
            AccountCode = NormalizePayrollJournalAccountCode(line.AccountCode),
            Description = TrimOrNull(line.Description) ?? $"{line.TransactionType} payroll journal",
            Amount = RoundMoney(Math.Abs(line.Amount)),
            Posted = line.Posted,
            JournalEntryId = line.JournalEntryId
        };

    private static bool IsPayrollJournalLineUnmapped(PayrollJournalLineDto line)
        => string.IsNullOrWhiteSpace(line.AccountCode) || line.AccountCode.Equals("UNMAPPED", StringComparison.OrdinalIgnoreCase);

    private static string? BuildPayrollJournalPreviewBlocker(
        PayrollRun run,
        string journalNumber,
        IReadOnlyList<PayrollJournalLineDto> lines,
        IReadOnlyList<string> missingAccountCodes,
        bool existingJournalFound,
        bool alreadyPosted,
        decimal totalDebit,
        decimal totalCredit,
        decimal difference)
    {
        if (lines.Count == 0)
        {
            return "Payroll journal cannot be posted because no journal lines were generated.";
        }

        if (existingJournalFound)
        {
            return $"Finance journal {journalNumber} already exists. Review the existing journal before posting this payroll run again.";
        }

        if (alreadyPosted)
        {
            return $"Payroll journal {journalNumber} has already been posted.";
        }

        if (run.Status == PayrollRunStatus.Closed)
        {
            return "This payroll run is already closed.";
        }

        if (run.Status == PayrollRunStatus.InReview)
        {
            return "Payroll workflow approval is required before posting.";
        }

        if (run.Status is PayrollRunStatus.Draft or PayrollRunStatus.RolledBack)
        {
            return "Calculate the payroll run before posting.";
        }

        var invalidSides = lines
            .Where(e => e.DebitCredit is not "DR" and not "CR")
            .Select(FormatPayrollJournalLineHint)
            .Take(5)
            .ToList();
        if (invalidSides.Count > 0)
        {
            return $"Payroll journal contains invalid debit/credit side(s): {string.Join(", ", invalidSides)}.";
        }

        var invalidAmounts = lines
            .Where(e => e.Amount <= 0)
            .Select(FormatPayrollJournalLineHint)
            .Take(5)
            .ToList();
        if (invalidAmounts.Count > 0)
        {
            return $"Payroll journal contains zero or negative amount line(s): {string.Join(", ", invalidAmounts)}.";
        }

        var unmapped = lines
            .Where(IsPayrollJournalLineUnmapped)
            .Select(FormatPayrollJournalLineHint)
            .Take(5)
            .ToList();
        if (unmapped.Count > 0)
        {
            return $"Payroll journal contains unmapped GL accounts ({string.Join(", ", unmapped)}). Complete Payroll Journal Mapping before posting.";
        }

        if (missingAccountCodes.Count > 0)
        {
            return $"Payroll journal account code(s) not found in Chart of Accounts: {string.Join(", ", missingAccountCodes)}.";
        }

        if (difference > PayrollJournalBalanceTolerance)
        {
            return $"Payroll journal is not balanced. Debit: {totalDebit}, Credit: {totalCredit}, Difference: {difference}.";
        }

        return null;
    }

    private async Task<IReadOnlyList<string>> FindMissingPayrollJournalAccountCodesAsync(
        Guid tenantId,
        IReadOnlyList<PayrollJournalLineDto> lines,
        CancellationToken cancellationToken)
    {
        var accountCodes = lines
            .Select(e => NormalizePayrollJournalAccountCode(e.AccountCode))
            .Where(e => !string.IsNullOrWhiteSpace(e) && !e.Equals("UNMAPPED", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (accountCodes.Count == 0)
        {
            return [];
        }

        var accounts = await _context.Accounts
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId)
            .Select(e => new { e.AccountCode, e.AccountNumber })
            .ToListAsync(cancellationToken);
        var availableCodes = accounts
            .SelectMany(e => new[]
            {
                NormalizePayrollJournalAccountCode(e.AccountCode),
                NormalizePayrollJournalAccountCode(e.AccountNumber)
            })
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return accountCodes
            .Where(code => !availableCodes.Contains(code))
            .ToList();
    }

    private async Task<PayrollPayslipBuildContext> LoadPayslipContextAsync(
        Guid tenantId,
        PayrollRun run,
        CancellationToken cancellationToken,
        IReadOnlyCollection<Guid>? employeeIdsOverride = null)
    {
        var employeeIds = (employeeIdsOverride ?? run.Employees
            .Select(e => e.EmployeeId)
            .ToList())
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
                    .ThenInclude(e => e.Position)
                        .ThenInclude(e => e.SalaryGrade)
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
                .Include(e => e.Loans)
                    .ThenInclude(e => e.LoanPolicy)
                .Include(e => e.Loans)
                    .ThenInclude(e => e.Schedules.OrderBy(s => s.SequenceNo))
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

        var payslip = new PayrollPayslipDto
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

        payslip.HtmlContent = BuildPayslipPrintInnerHtml(payslip);
        return payslip;
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

    private async Task<EmailTemplate> EnsurePayrollPayslipEmailTemplateAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var template = await _context.EmailTemplates
            .FirstOrDefaultAsync(e =>
                e.TenantId == tenantId &&
                !e.IsDeleted &&
                e.Module == PayrollNotificationModule &&
                e.Name == PayrollPayslipEmailTemplateName,
                cancellationToken);

        var variables = JsonSerializer.Serialize(PayrollPayslipTemplateVariables());
        if (template == null)
        {
            template = new EmailTemplate
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = PayrollPayslipEmailTemplateName,
                Module = PayrollNotificationModule,
                Category = PayrollNotificationCategory,
                TableName = "PayrollPayslipEmail",
                Subject = "Payslip - {{pay_period}}",
                HtmlBody = DefaultPayrollPayslipEmailHtmlTemplate(),
                PlainTextBody = DefaultPayrollPayslipEmailPlainText(),
                TemplateVariables = variables,
                Description = "Payroll payslip email body. The full payslip is attached as a PDF generated from the payroll print preview layout.",
                IsActive = true,
                CreatedBy = "System"
            };
            _context.EmailTemplates.Add(template);
            await _context.SaveChangesAsync(cancellationToken);
            return await ResolvePayrollPayslipEmailTemplateAsync(tenantId, cancellationToken) ?? template;
        }

        var changed = false;
        if (string.IsNullOrWhiteSpace(template.TemplateVariables))
        {
            template.TemplateVariables = variables;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(template.Category))
        {
            template.Category = PayrollNotificationCategory;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(template.TableName))
        {
            template.TableName = "PayrollPayslipEmail";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(template.Description))
        {
            template.Description = "Payroll payslip email body. The full payslip is attached as a PDF generated from the payroll print preview layout.";
            changed = true;
        }

        if (IsLegacyPayslipInlineTemplate(template.HtmlBody))
        {
            template.HtmlBody = DefaultPayrollPayslipEmailHtmlTemplate();
            changed = true;
        }

        if (changed)
        {
            template.UpdatedAt = DateTime.UtcNow;
            template.UpdatedBy = "System";
            await _context.SaveChangesAsync(cancellationToken);
        }

        return await ResolvePayrollPayslipEmailTemplateAsync(tenantId, cancellationToken) ?? template;
    }

    private async Task<EmailTemplate?> ResolvePayrollPayslipEmailTemplateAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var templates = await _context.EmailTemplates
            .Where(e =>
                e.TenantId == tenantId &&
                !e.IsDeleted &&
                e.IsActive &&
                e.Module == PayrollNotificationModule &&
                e.TableName != null)
            .OrderByDescending(e => e.UpdatedAt ?? e.CreatedAt)
            .ThenByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        return templates.FirstOrDefault(e =>
            EntityTypeKey(e.TableName) == EntityTypeKey(PayrollPayslipEmailEntityType));
    }

    private static bool IsLegacyPayslipInlineTemplate(string? html)
    {
        var value = (html ?? string.Empty).Trim();
        return value.Equals("{{payslip_print_html_document}}", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("{{payslip_print_html}}", StringComparison.OrdinalIgnoreCase);
    }

    private static string EntityTypeKey(string? value)
        => new((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());

    private async Task<NotificationTopic> EnsurePayrollPayslipEmailTopicAsync(
        Guid tenantId,
        Guid emailTemplateId,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var topic = await _context.NotificationTopics
            .Include(e => e.Recipients)
            .Include(e => e.EmailTemplate)
            .FirstOrDefaultAsync(e =>
                e.TenantId == tenantId &&
                !e.IsDeleted &&
                e.Key == PayrollPayslipEmailTopicKey,
                cancellationToken);

        var changed = false;
        if (topic == null)
        {
            topic = new NotificationTopic
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = PayrollPayslipEmailTopicKey,
                Name = "Payroll Payslip Email",
                Description = "Queues employee payslip emails through the notification topic publisher.",
                EntityType = PayrollPayslipEmailEntityType,
                IsSystem = true,
                IsRequired = false,
                IsActive = true,
                EnableInApp = false,
                EnableEmail = true,
                EnableSms = false,
                InAppTitleTemplate = "Payslip - {{pay_period}}",
                InAppBodyTemplate = "Your payslip for {{pay_period}} is ready.",
                EmailTemplateId = emailTemplateId,
                ActionUrlTemplate = "/hr/payroll?runId={{payroll_run_id}}",
                CreatedAt = DateTime.UtcNow,
                CreatedById = userId,
                CreatedBy = "System"
            };

            _context.NotificationTopics.Add(topic);
            changed = true;
        }
        else
        {
            if (!topic.IsSystem)
            {
                topic.IsSystem = true;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(topic.Name))
            {
                topic.Name = "Payroll Payslip Email";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(topic.Description))
            {
                topic.Description = "Queues employee payslip emails through the notification topic publisher.";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(topic.EntityType))
            {
                topic.EntityType = PayrollPayslipEmailEntityType;
                changed = true;
            }

            if (topic.EmailTemplateId != emailTemplateId)
            {
                topic.EmailTemplateId = emailTemplateId;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(topic.InAppTitleTemplate))
            {
                topic.InAppTitleTemplate = "Payslip - {{pay_period}}";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(topic.InAppBodyTemplate))
            {
                topic.InAppBodyTemplate = "Your payslip for {{pay_period}} is ready.";
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(topic.ActionUrlTemplate))
            {
                topic.ActionUrlTemplate = "/hr/payroll?runId={{payroll_run_id}}";
                changed = true;
            }

            if (changed)
            {
                topic.UpdatedAt = DateTime.UtcNow;
                topic.UpdatedBy = "System";
                topic.LastModifiedById = userId;
            }
        }

        var existingRecipients = (topic.Recipients ?? new List<NotificationTopicRecipient>())
            .Where(e => !e.IsDeleted)
            .ToList();
        var hasEmployeeEmailRecipient = existingRecipients.Any(e =>
            e.IsSystem &&
            string.Equals(e.RecipientKind, "EmailFromData", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.RecipientValue, "employee_email", StringComparison.OrdinalIgnoreCase));

        if (!hasEmployeeEmailRecipient)
        {
            _context.NotificationTopicRecipients.Add(new NotificationTopicRecipient
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TopicId = topic.Id,
                RecipientKind = "EmailFromData",
                RecipientValue = "employee_email",
                IsSystem = true,
                SendInApp = false,
                SendEmail = true,
                SendSms = false,
                CreatedAt = DateTime.UtcNow,
                CreatedById = userId,
                CreatedBy = "System"
            });
            changed = true;
        }

        if (changed)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return await _context.NotificationTopics
            .AsNoTracking()
            .Include(e => e.Recipients)
            .Include(e => e.EmailTemplate)
            .FirstAsync(e =>
                e.TenantId == tenantId &&
                !e.IsDeleted &&
                e.Key == PayrollPayslipEmailTopicKey,
                cancellationToken);
    }

    private static string? GetPayrollPayslipEmailTopicBlocker(NotificationTopic topic)
    {
        if (!topic.IsActive)
        {
            return $"Notification topic {PayrollPayslipEmailTopicKey} is inactive.";
        }

        if (!topic.EnableEmail)
        {
            return $"Notification topic {PayrollPayslipEmailTopicKey} has email delivery disabled.";
        }

        var hasEmailRecipient = (topic.Recipients ?? new List<NotificationTopicRecipient>())
            .Any(e => !e.IsDeleted && e.SendEmail);
        if (!hasEmailRecipient)
        {
            return $"Notification topic {PayrollPayslipEmailTopicKey} has no email recipients.";
        }

        return null;
    }

    private static IReadOnlyList<string> PayrollPayslipTemplateVariables()
        =>
        [
            "employee_id",
            "employee_number",
            "employee_name",
            "employee_email",
            "department",
            "section",
            "position",
            "pay_period",
            "run_number",
            "payslip_number",
            "company_name",
            "company_address",
            "currency_code",
            "gross_amount",
            "total_earnings",
            "total_deductions",
            "net_salary",
            "taxable_income",
            "income_tax",
            "normal_income_tax",
            "bonus_income_tax",
            "employee_contribution",
            "employer_contribution",
            "payslip_password_format",
            "payslip_password_example",
            "payslip_password_help",
            "message",
            "action_url"
        ];

    private static Dictionary<string, object> BuildPayslipTopicData(
        IReadOnlyDictionary<string, string> templateValues,
        Guid payrollRunId,
        Guid payrollRunEmployeeId,
        PayrollPayslipDto payslip,
        Guid? userId)
    {
        var data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in templateValues)
        {
            data[key] = value ?? string.Empty;
        }

        var actionUrl = $"/hr/payroll?runId={payrollRunId}";
        data["ActionUrl"] = actionUrl;
        data["action_url"] = actionUrl;
        data["payroll_run_id"] = payrollRunId.ToString();
        data["payroll_run_employee_id"] = payrollRunEmployeeId.ToString();
        data["payslip_snapshot_id"] = payslip.Snapshot?.Id.ToString() ?? string.Empty;
        data["requested_by_user_id"] = userId?.ToString() ?? string.Empty;
        data["EmployeeId"] = payslip.EmployeeId;

        return data;
    }

    private static Dictionary<string, string> BuildPayslipTemplateValues(
        PayrollPayslipDto payslip,
        PayrollRun run,
        string periodLabel,
        string? message,
        string printInnerHtml,
        string printHtmlDocument)
    {
        var totalEarnings = payslip.Earnings.Sum(e => e.Amount);
        var totalDeductions = payslip.Deductions.Sum(e => e.Amount);
        var bonusIncomeTax = payslip.BonusIncomeTax;
        var normalIncomeTax = payslip.NormalIncomeTax == 0m
            ? Math.Max(0, payslip.IncomeTax - bonusIncomeTax)
            : payslip.NormalIncomeTax;

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["employee_id"] = payslip.EmployeeId.ToString(),
            ["employee_number"] = payslip.EmployeeNumber ?? string.Empty,
            ["employee_name"] = payslip.EmployeeName ?? string.Empty,
            ["employee_email"] = payslip.EmployeeEmail ?? string.Empty,
            ["department"] = payslip.DepartmentName ?? string.Empty,
            ["section"] = payslip.SectionName ?? string.Empty,
            ["position"] = payslip.PositionTitle ?? string.Empty,
            ["pay_period"] = periodLabel,
            ["run_number"] = run.RunNumber ?? string.Empty,
            ["payslip_number"] = payslip.Snapshot?.PayslipNumber ?? string.Empty,
            ["company_name"] = payslip.CompanyName ?? string.Empty,
            ["company_address"] = payslip.CompanyAddress ?? string.Empty,
            ["currency_code"] = payslip.CurrencyCode ?? string.Empty,
            ["gross_amount"] = FormatEmailAmount(payslip.GrossIncome),
            ["total_earnings"] = FormatEmailAmount(totalEarnings),
            ["total_deductions"] = FormatEmailAmount(totalDeductions),
            ["net_salary"] = FormatEmailAmount(payslip.NetIncome),
            ["taxable_income"] = FormatEmailAmount(payslip.TaxableIncome),
            ["income_tax"] = FormatEmailAmount(payslip.IncomeTax),
            ["normal_income_tax"] = FormatEmailAmount(normalIncomeTax),
            ["bonus_income_tax"] = FormatEmailAmount(bonusIncomeTax),
            ["employee_contribution"] = FormatEmailAmount(payslip.EmployeeContribution),
            ["employer_contribution"] = FormatEmailAmount(payslip.EmployerContribution),
            ["payslip_password_format"] = "DDMMYYYY + EMPLOYEENUMBER",
            ["payslip_password_example"] = "26021985PAY001",
            ["payslip_password_help"] = "Your attached payslip PDF is password-protected. Use your date of birth in DDMMYYYY format followed immediately by your employee number in uppercase, for example 26021985PAY001.",
            ["message"] = message ?? string.Empty,
            ["action_url"] = $"/hr/payroll?runId={run.Id}",
            ["payslip_print_html"] = printInnerHtml,
            ["payslip_print_html_document"] = printHtmlDocument
        };
    }

    private static string BuildPayslipEmailHtml(
        EmailTemplate? template,
        IReadOnlyDictionary<string, string> values)
    {
        var body = string.IsNullOrWhiteSpace(template?.HtmlBody)
            ? DefaultPayrollPayslipEmailHtmlTemplate()
            : template!.HtmlBody;

        return RenderNotificationTemplate(body, values);
    }

    private static string DefaultPayrollPayslipEmailHtmlTemplate()
        => """
           <p>Dear {{employee_name}},</p>
           <p>Your payslip for {{pay_period}} is attached as a PDF.</p>
           <p>Run: <strong>{{run_number}}</strong></p>
           <p>Net salary: <strong>{{net_salary}}</strong> {{currency_code}}</p>
           {{message}}
           """;

    private static string DefaultPayrollPayslipEmailPlainText()
        => "Dear {{employee_name}}, your payslip for {{pay_period}} is attached.";

    private static string RenderNotificationTemplate(string template, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        var rendered = template;
        foreach (var (key, value) in values)
        {
            rendered = rendered.Replace("{{" + key + "}}", value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return rendered;
    }

    private static string BuildPayslipPrintHtmlDocument(PayrollPayslipDto payslip)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><title>");
        builder.Append(Html(payslip.IsSeparateBonusRun ? "Bonus Slip" : "Payslip"));
        builder.AppendLine("</title><style>");
        builder.AppendLine("body{margin:0;padding:0;background:#fff;color:#000}");
        builder.AppendLine(".payroll-payslip-print-root{box-sizing:border-box;width:210mm;height:297mm;overflow:hidden;border:1px solid #000;background:#fff;padding:14mm 1mm 3mm 1mm;font-family:Arial,Helvetica,sans-serif;font-size:10.5px;line-height:1.15;color:#000}");
        builder.AppendLine(".payroll-payslip-print-root *{box-sizing:border-box;font-family:Arial,Helvetica,sans-serif}");
        builder.AppendLine("@page{size:A4;margin:0}");
        builder.AppendLine("</style></head><body><article class=\"payroll-payslip-print-root\">");
        builder.Append(BuildPayslipPrintInnerHtml(payslip, includeStyle: true));
        builder.AppendLine("</article></body></html>");
        return builder.ToString();
    }

    private static string BuildPayslipPrintInnerHtml(PayrollPayslipDto payslip, bool includeStyle = true)
    {
        var builder = new StringBuilder();
        if (includeStyle)
        {
            builder.AppendLine("<style>");
            builder.AppendLine(PayslipPrintInnerCss());
            builder.AppendLine("</style>");
        }

        builder.AppendLine("<div class=\"pps-root\">");
        if (payslip.IsSeparateBonusRun)
        {
            AppendBonusSlipHtml(builder, payslip);
        }
        else
        {
            AppendStandardPayslipHtml(builder, payslip);
        }
        builder.AppendLine("</div>");
        return builder.ToString();
    }

    private static string PayslipPrintInnerCss()
        => """
           .pps-root{min-height:100%;display:flex;flex-direction:column;color:#000;font-size:10.5px;line-height:1.15}
           .pps-fill{flex:1 1 auto}
           .pps-center{text-align:center}
           .pps-company{font-size:16px;font-weight:700}
           .pps-title{margin-top:2.5mm;font-size:16px;font-weight:700}
           .pps-underline{display:inline-flex;flex-direction:column;white-space:nowrap;line-height:1.08;vertical-align:bottom}
           .pps-underline::after{content:"";display:block;border-top:1px solid #000;height:0;margin-top:2px;width:100%}
           .pps-details{display:grid;grid-template-columns:1fr 1fr;column-gap:8mm;margin-top:7mm}
           .pps-details-col{display:flex;flex-direction:column;gap:4px}
           .pps-detail{display:grid;grid-template-columns:86px 5px minmax(0,1fr)}
           .pps-detail-label{font-weight:700;text-align:right;white-space:nowrap}
           .pps-detail-colon{font-weight:700;text-align:center}
           .pps-detail-value{font-weight:500;white-space:nowrap;min-width:0}
           .pps-lines{border-top:1px solid #000;margin-top:4mm;padding-top:1.5mm}
           .pps-line-grid{display:grid;grid-template-columns:1fr 1fr;column-gap:6mm}
           .pps-table{border-collapse:collapse;table-layout:fixed;width:100%;font-size:10.5px;line-height:1.15}
           .pps-table th{font-weight:700;padding-bottom:1.5mm}
           .pps-table td{font-weight:500;padding:3px 0;vertical-align:top}
           .pps-item-head{width:32px;text-align:left}
           .pps-line-title{text-align:center;font-size:15px;font-weight:400}
           .pps-amount-head{width:64px;text-align:right}
           .pps-line-label{white-space:nowrap;padding-right:8px}
           .pps-amount{text-align:right;white-space:nowrap;font-variant-numeric:tabular-nums}
           .pps-summary{border:1px solid #000;margin:3.5mm auto 0 auto;padding:1.5mm 3mm;width:84mm;font-size:10.5px}
           .pps-summary-title{text-align:center;font-size:12.5px;font-weight:700;margin-bottom:1.5mm}
           .pps-summary-grid{display:grid;grid-template-columns:1fr 88px;row-gap:1mm}
           .pps-bold{font-weight:700}
           .pps-tax{margin-top:1.5mm;font-size:10.5px}
           .pps-tax-title{text-align:center;font-size:14px;font-weight:400}
           .pps-tax-grid{display:grid;grid-template-columns:1fr 1fr;column-gap:4mm;margin-top:1mm}
           .pps-tax-col{display:grid;grid-template-columns:max-content 4px 72px}
           .pps-tax-col span{white-space:nowrap}
           .pps-contrib{margin-top:3mm;font-size:9.5px;line-height:1.15}
           .pps-contrib-title{text-align:center;font-size:13px;font-weight:400}
           .pps-contrib table{border-collapse:collapse;table-layout:fixed;width:100%;margin-top:1mm}
           .pps-contrib th{font-weight:700;vertical-align:bottom}
           .pps-contrib td{padding:3px 0}
           .pps-bank{border-top:1px solid #000;margin-top:2mm;padding-top:1.5mm;font-size:10px;line-height:1.15}
           .pps-bank-title{text-align:center;font-size:13px;font-weight:400}
           .pps-bank-grid{display:grid;grid-template-columns:74mm 34mm 23mm 24mm minmax(22mm,1fr);column-gap:2mm;margin-top:2mm}
           .pps-bank-row{display:grid;grid-template-columns:74mm 34mm 23mm 24mm minmax(22mm,1fr);column-gap:2mm;margin-top:4px}
           .pps-bank-grid span,.pps-bank-row span{white-space:nowrap}
           .pps-footer{height:2mm;margin-top:auto}
           .pps-bonus-title{margin-top:8mm;font-size:17px;font-weight:700}
           .pps-bonus-details{margin:10mm auto 0 auto;width:150mm;display:flex;flex-direction:column;gap:2mm;font-size:12px}
           .pps-bonus-lines{margin:8mm auto 0 auto;width:150mm;font-size:12px}
           .pps-bonus-row{display:grid;grid-template-columns:1fr 38mm;padding:2mm 0}
           .pps-border-bottom{border-bottom:1px solid #000}
           .pps-border-top{border-top:1px solid #000}
           """;

    private static void AppendStandardPayslipHtml(StringBuilder builder, PayrollPayslipDto payslip)
    {
        var totalEarnings = payslip.Earnings.Sum(e => e.Amount);
        var totalDeductions = payslip.Deductions.Sum(e => e.Amount);
        var bonusIncomeTax = payslip.BonusIncomeTax;
        var normalIncomeTax = payslip.NormalIncomeTax == 0m
            ? Math.Max(0, payslip.IncomeTax - bonusIncomeTax)
            : payslip.NormalIncomeTax;

        builder.AppendLine("<div class=\"pps-fill\">");
        AppendPayslipHeader(builder, payslip, "PAYSLIP", titleClass: "pps-title");

        builder.AppendLine("<section class=\"pps-details\">");
        builder.AppendLine("<div class=\"pps-details-col\">");
        AppendPayslipDetail(builder, "Employee ID", payslip.EmployeeNumber);
        AppendPayslipDetail(builder, "Name", payslip.EmployeeName);
        AppendPayslipDetail(builder, "Department", payslip.DepartmentName);
        AppendPayslipDetail(builder, "Section", payslip.SectionName);
        AppendPayslipDetail(builder, "Positions", payslip.PositionTitle);
        builder.AppendLine("<div style=\"padding-top:5px\">");
        AppendPayslipDetail(builder, "Staff Category", payslip.StaffCategory);
        builder.AppendLine("</div>");
        builder.AppendLine("</div>");
        builder.AppendLine("<div class=\"pps-details-col\">");
        AppendPayslipDetail(builder, "Month", FormatPayslipMonth(payslip.PayPeriodTo));
        AppendPayslipDetail(builder, "Currency Used", CurrencyLabel(payslip.CurrencyCode));
        AppendPayslipDetail(builder, "Job Location", payslip.JobLocation);
        AppendPayslipDetail(builder, "SSF Number", payslip.SsfNumber);
        AppendPayslipDetail(builder, "Staff TIN", payslip.StaffTin);
        builder.AppendLine("</div></section>");

        builder.AppendLine("<section class=\"pps-lines\"><div class=\"pps-line-grid\">");
        AppendPayLinesTable(builder, "EARNINGS", payslip.Earnings);
        AppendPayLinesTable(builder, "DEDUCTIONS", payslip.Deductions);
        builder.AppendLine("</div></section>");

        builder.AppendLine("<section class=\"pps-summary\">");
        builder.Append("<div class=\"pps-summary-title\">").Append(Underline($"SALARY SUMMARY ({CurrencyLabel(payslip.CurrencyCode)})")).AppendLine("</div>");
        builder.AppendLine("<div class=\"pps-summary-grid\">");
        AppendSummaryLine(builder, "Total Earnings :", totalEarnings, bold: false);
        AppendSummaryLine(builder, "Total Deductions :", totalDeductions, bold: false);
        AppendSummaryLine(builder, "Net Salary :", payslip.NetIncome, bold: true);
        builder.AppendLine("</div></section>");

        builder.AppendLine("<section class=\"pps-tax\">");
        builder.Append("<div class=\"pps-tax-title\">").Append(Underline("TAX ANALYSIS")).AppendLine("</div>");
        builder.AppendLine("<div class=\"pps-tax-grid\">");
        builder.AppendLine("<div class=\"pps-tax-col\">");
        AppendTaxLine(builder, "Taxable Earning", payslip.TaxableIncome, bold: false);
        AppendTaxLine(builder, "Tax Relief", payslip.TaxRelief, bold: false);
        builder.AppendLine("</div><div class=\"pps-tax-col\">");
        AppendTaxLine(builder, "Income Tax (Normal)", normalIncomeTax, bold: false);
        AppendTaxLine(builder, "Income Tax (Bonus)", bonusIncomeTax, bold: false);
        AppendTaxLine(builder, "Income Tax (Total)", payslip.IncomeTax, bold: true);
        builder.AppendLine("</div></div></section>");

        AppendContributionStatementHtml(builder, payslip);
        AppendBankDetailsHtml(builder, payslip);
        builder.AppendLine("</div><footer class=\"pps-footer\"></footer>");
    }

    private static void AppendBonusSlipHtml(StringBuilder builder, PayrollPayslipDto payslip)
    {
        var bonusLines = payslip.Earnings
            .Where(e => e.TransactionType.Equals(BonusTransactionType, StringComparison.OrdinalIgnoreCase) && e.Amount != 0)
            .ToList();
        var bonusAmount = bonusLines.Sum(e => e.Amount);
        var bank = payslip.BankDetails.FirstOrDefault();
        var description = string.Join(", ", bonusLines.Select(LineLabel).Where(e => !string.IsNullOrWhiteSpace(e)));
        if (string.IsNullOrWhiteSpace(description))
        {
            description = TrimOrNull(payslip.SeparateBonusCode) ?? "Bonus";
        }

        AppendPayslipHeader(builder, payslip, "Bonus Slip", titleClass: "pps-bonus-title");
        builder.AppendLine("<section class=\"pps-bonus-details\">");
        AppendPayslipDetail(builder, "Staff No", payslip.EmployeeNumber);
        AppendPayslipDetail(builder, "Staff Name", payslip.EmployeeName);
        AppendPayslipDetail(builder, "Bank", bank?.BankName);
        AppendPayslipDetail(builder, "Acct No", bank?.AccountNumber);
        builder.AppendLine("</section>");

        builder.AppendLine("<section class=\"pps-bonus-lines\">");
        builder.AppendLine("<div class=\"pps-bonus-row pps-border-bottom pps-bold\">");
        builder.Append(Underline("Description")).Append("<span class=\"pps-amount\">").Append(Underline("Amount")).AppendLine("</span></div>");
        AppendBonusLine(builder, description, bonusAmount, bold: false, borderTop: false);
        AppendBonusLine(builder, "Income Tax", payslip.IncomeTax, bold: false, borderTop: false);
        AppendBonusLine(builder, "Net Bonus", payslip.NetIncome, bold: true, borderTop: true);
        builder.AppendLine("</section><footer class=\"pps-footer\"></footer>");
    }

    private static void AppendPayslipHeader(StringBuilder builder, PayrollPayslipDto payslip, string title, string titleClass)
    {
        builder.AppendLine("<header class=\"pps-center\">");
        if (!string.IsNullOrWhiteSpace(payslip.CompanyName))
        {
            builder.Append("<div class=\"pps-company\">").Append(Html(payslip.CompanyName)).AppendLine("</div>");
        }
        if (!string.IsNullOrWhiteSpace(payslip.CompanyAddress))
        {
            builder.Append("<div>").Append(Html(payslip.CompanyAddress)).AppendLine("</div>");
        }
        if (!string.IsNullOrWhiteSpace(payslip.CompanyPhone))
        {
            builder.Append("<div>").Append(Html(payslip.CompanyPhone)).AppendLine("</div>");
        }
        builder.Append("<div class=\"").Append(titleClass).Append("\">").Append(Underline(title)).AppendLine("</div>");
        builder.AppendLine("</header>");
    }

    private static void AppendPayslipDetail(StringBuilder builder, string label, string? value)
    {
        builder.Append("<div class=\"pps-detail\"><span class=\"pps-detail-label\">")
            .Append(Html(label))
            .Append("</span><span class=\"pps-detail-colon\">:</span><span class=\"pps-detail-value\">")
            .Append(Html(value))
            .AppendLine("</span></div>");
    }

    private static void AppendPayLinesTable(StringBuilder builder, string title, IReadOnlyList<PayrollTransactionDto> lines)
    {
        var visibleLines = lines
            .Where(e => e.Amount != 0)
            .OrderBy(LineOrder)
            .ThenBy(LineLabel)
            .ToList();

        builder.AppendLine("<table class=\"pps-table\"><thead><tr>");
        builder.Append("<th class=\"pps-item-head\">").Append(Underline("Item")).AppendLine("</th>");
        builder.Append("<th class=\"pps-line-title\">").Append(Underline(title)).AppendLine("</th>");
        builder.Append("<th class=\"pps-amount-head\">").Append(Underline("Amount")).AppendLine("</th>");
        builder.AppendLine("</tr></thead><tbody>");

        foreach (var line in visibleLines)
        {
            builder.Append("<tr><td colspan=\"2\" class=\"pps-line-label\">")
                .Append(Html(LineLabel(line)))
                .Append("</td><td class=\"pps-amount\">")
                .Append(FormatEmailAmount(line.Amount))
                .AppendLine("</td></tr>");
        }

        builder.AppendLine("</tbody></table>");
    }

    private static void AppendSummaryLine(StringBuilder builder, string label, decimal amount, bool bold)
    {
        var css = bold ? " class=\"pps-bold\"" : string.Empty;
        builder.Append("<span").Append(css).Append(">").Append(Html(label)).Append("</span><span class=\"pps-amount");
        if (bold) builder.Append(" pps-bold");
        builder.Append("\">").Append(FormatEmailAmount(amount)).AppendLine("</span>");
    }

    private static void AppendTaxLine(StringBuilder builder, string label, decimal amount, bool bold)
    {
        var boldClass = bold ? " pps-bold" : string.Empty;
        builder.Append("<span class=\"").Append(boldClass.Trim()).Append("\">").Append(Html(label)).Append("</span><span>:</span><span class=\"pps-amount")
            .Append(boldClass).Append("\">").Append(FormatEmailAmount(amount)).AppendLine("</span>");
    }

    private static void AppendContributionStatementHtml(StringBuilder builder, PayrollPayslipDto payslip)
    {
        var providentRows = payslip.Contributions.Where(e => e.IsProvidentFund).ToList();
        var statutoryRows = payslip.Contributions.Where(e => !e.IsProvidentFund).ToList();
        if (statutoryRows.Count == 0)
        {
            statutoryRows.Add(new PayrollPayslipContributionDto { Item = "SOCIAL SECURITY FUND" });
        }

        builder.AppendLine("<section class=\"pps-contrib\">");
        builder.Append("<div class=\"pps-contrib-title\">").Append(Underline("CONTRIBUTION STATEMENT")).AppendLine("</div>");
        builder.AppendLine("<table><thead><tr>");
        builder.Append("<th style=\"width:30mm;text-align:left;font-style:italic\">").Append(Underline("Item")).AppendLine("</th>");
        builder.Append("<th class=\"pps-amount\">").Append(Underline("Employee's Contr.")).AppendLine("</th>");
        builder.Append("<th class=\"pps-amount\">").Append(Underline("Employer's Contr.")).AppendLine("</th>");
        builder.Append("<th class=\"pps-amount\">").Append(Underline("Total Contri.")).AppendLine("</th>");
        builder.Append("<th class=\"pps-amount\">").Append(Underline("Opening Bal.")).AppendLine("</th>");
        builder.Append("<th class=\"pps-amount\">").Append(Underline("Total Withdrawal")).AppendLine("</th>");
        builder.Append("<th class=\"pps-amount\">").Append(Underline("Grand Total")).AppendLine("</th>");
        builder.AppendLine("</tr></thead><tbody>");
        foreach (var line in providentRows)
        {
            builder.Append("<tr><td>").Append(Html(line.Item)).Append("</td><td class=\"pps-amount\">").Append(FormatEmailAmount(line.EmployeeContribution))
                .Append("</td><td class=\"pps-amount\">").Append(FormatEmailAmount(line.EmployerContribution))
                .Append("</td><td class=\"pps-amount\">").Append(FormatEmailAmount(line.TotalContribution))
                .Append("</td><td class=\"pps-amount\">").Append(FormatEmailAmount(line.OpeningBalance))
                .Append("</td><td class=\"pps-amount\">").Append(line.TotalWithdrawal == 0 ? string.Empty : FormatEmailAmount(line.TotalWithdrawal))
                .Append("</td><td class=\"pps-amount\">").Append(FormatEmailAmount(line.GrandTotal))
                .AppendLine("</td></tr>");
        }
        builder.AppendLine("</tbody></table>");

        builder.AppendLine("<table style=\"margin-top:1.5mm\"><thead><tr>");
        builder.Append("<th style=\"width:42mm;text-align:left;font-style:italic\">").Append(Underline("Item")).AppendLine("</th>");
        builder.Append("<th class=\"pps-amount\">").Append(Underline("Employee's Contr.")).AppendLine("</th>");
        builder.Append("<th class=\"pps-amount\">").Append(Underline("Employer's Contr.")).AppendLine("</th>");
        builder.Append("<th class=\"pps-amount\">").Append(Underline("Total Contribution")).AppendLine("</th>");
        builder.Append("<th class=\"pps-amount\">").Append(Underline("1st Tier")).AppendLine("</th>");
        builder.Append("<th class=\"pps-amount\">").Append(Underline("2nd Tier")).AppendLine("</th>");
        builder.AppendLine("</tr></thead><tbody>");
        foreach (var line in statutoryRows)
        {
            builder.Append("<tr><td>").Append(Html(line.Item)).Append("</td><td class=\"pps-amount\">").Append(FormatEmailAmount(line.EmployeeContribution))
                .Append("</td><td class=\"pps-amount\">").Append(FormatEmailAmount(line.EmployerContribution))
                .Append("</td><td class=\"pps-amount\">").Append(FormatEmailAmount(line.TotalContribution))
                .Append("</td><td class=\"pps-amount\">").Append(FormatEmailAmount(line.FirstTier))
                .Append("</td><td class=\"pps-amount\">").Append(FormatEmailAmount(line.SecondTier))
                .AppendLine("</td></tr>");
        }
        builder.AppendLine("</tbody></table></section>");
    }

    private static void AppendBankDetailsHtml(StringBuilder builder, PayrollPayslipDto payslip)
    {
        IReadOnlyList<PayrollPayslipBankDetailDto> rows = payslip.BankDetails.Count > 0
            ? payslip.BankDetails
            : new List<PayrollPayslipBankDetailDto>
            {
                new() { CurrencyCode = payslip.CurrencyCode, Amount = payslip.NetIncome }
            };

        builder.AppendLine("<section class=\"pps-bank\">");
        builder.Append("<div class=\"pps-bank-title\">").Append(Underline("BANK DETAILS")).AppendLine("</div>");
        builder.AppendLine("<div class=\"pps-bank-grid pps-bold\">");
        builder.Append(Underline("Bank")).Append(Underline("Acct No")).Append(Underline("Currency")).Append(Underline("Exch. Rate")).Append("<span class=\"pps-amount\">").Append(Underline("Amount")).AppendLine("</span></div>");
        foreach (var row in rows)
        {
            builder.Append("<div class=\"pps-bank-row\"><span>").Append(Html(row.BankName))
                .Append("</span><span>").Append(Html(row.AccountNumber))
                .Append("</span><span>").Append(Html(CurrencyLabel(row.CurrencyCode)))
                .Append("</span><span>").Append(row.ExchangeRate.HasValue ? FormatEmailAmount(row.ExchangeRate.Value) : string.Empty)
                .Append("</span><span class=\"pps-amount\">").Append(FormatEmailAmount(row.Amount))
                .AppendLine("</span></div>");
        }
        builder.AppendLine("</section>");
    }

    private static void AppendBonusLine(StringBuilder builder, string label, decimal amount, bool bold, bool borderTop)
    {
        var classes = new List<string> { "pps-bonus-row" };
        if (bold) classes.Add("pps-bold");
        if (borderTop) classes.Add("pps-border-top");
        builder.Append("<div class=\"").Append(string.Join(' ', classes)).Append("\"><span>")
            .Append(Html(label))
            .Append("</span><span class=\"pps-amount\">")
            .Append(FormatEmailAmount(amount))
            .AppendLine("</span></div>");
    }

    private static byte[] BuildPayslipPdfAttachment(PayrollPayslipDto payslip, string password)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(8.4f).FontColor(Colors.Black));
                page.Content()
                    .Border(1)
                    .PaddingTop(38)
                    .PaddingHorizontal(6)
                    .PaddingBottom(8)
                    .Element(content =>
                    {
                        if (payslip.IsSeparateBonusRun)
                        {
                            ComposeBonusSlipPdf(content, payslip);
                        }
                        else
                        {
                            ComposeStandardPayslipPdf(content, payslip);
                        }
                    });
            });
        }).GeneratePdf();

        return EncryptPayslipPdf(pdfBytes, password);
    }

    private static byte[] EncryptPayslipPdf(byte[] pdfBytes, string password)
    {
        if (pdfBytes.Length == 0)
        {
            return pdfBytes;
        }

        var userPassword = Encoding.UTF8.GetBytes(password);
        var ownerPassword = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("N"));
        var writerProperties = new WriterProperties()
            .SetStandardEncryption(
                userPassword,
                ownerPassword,
                EncryptionConstants.ALLOW_PRINTING,
                EncryptionConstants.ENCRYPTION_AES_256);

        using var input = new MemoryStream(pdfBytes);
        using var output = new MemoryStream();
        using var reader = new PdfReader(input);
        using var writer = new PdfWriter(output, writerProperties);
        using (var pdf = new PdfDocument(reader, writer))
        {
            pdf.Close();
        }

        return output.ToArray();
    }

    private static string? BuildPayslipPdfPassword(PayrollEmployeeProfile? profile)
    {
        var dateOfBirth = profile?.Employee.DateOfBirth;
        var employeeNumber = TrimOrNull(profile?.Employee.EmployeeNumber);
        if (dateOfBirth == null || string.IsNullOrWhiteSpace(employeeNumber))
        {
            return null;
        }

        return dateOfBirth.Value.ToString("ddMMyyyy", CultureInfo.InvariantCulture) + employeeNumber.ToUpperInvariant();
    }

    private static void ComposeStandardPayslipPdf(IContainer container, PayrollPayslipDto payslip)
    {
        var totalEarnings = payslip.Earnings.Sum(e => e.Amount);
        var totalDeductions = payslip.Deductions.Sum(e => e.Amount);
        var bonusIncomeTax = payslip.BonusIncomeTax;
        var normalIncomeTax = payslip.NormalIncomeTax == 0m
            ? Math.Max(0, payslip.IncomeTax - bonusIncomeTax)
            : payslip.NormalIncomeTax;

        container.Column(column =>
        {
            column.Spacing(6);
            column.Item().Element(c => ComposePayslipPdfHeader(c, payslip, "PAYSLIP", 16));

            column.Item().PaddingTop(15).Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Spacing(2);
                    left.Item().Element(c => ComposePayslipPdfDetail(c, "Employee ID", payslip.EmployeeNumber));
                    left.Item().Element(c => ComposePayslipPdfDetail(c, "Name", payslip.EmployeeName));
                    left.Item().Element(c => ComposePayslipPdfDetail(c, "Department", payslip.DepartmentName));
                    left.Item().Element(c => ComposePayslipPdfDetail(c, "Section", payslip.SectionName));
                    left.Item().Element(c => ComposePayslipPdfDetail(c, "Positions", payslip.PositionTitle));
                    left.Item().PaddingTop(4).Element(c => ComposePayslipPdfDetail(c, "Staff Category", payslip.StaffCategory));
                });

                row.RelativeItem().Column(right =>
                {
                    right.Spacing(2);
                    right.Item().Element(c => ComposePayslipPdfDetail(c, "Month", FormatPayslipMonth(payslip.PayPeriodTo)));
                    right.Item().Element(c => ComposePayslipPdfDetail(c, "Currency Used", CurrencyLabel(payslip.CurrencyCode)));
                    right.Item().Element(c => ComposePayslipPdfDetail(c, "Job Location", payslip.JobLocation));
                    right.Item().Element(c => ComposePayslipPdfDetail(c, "SSF Number", payslip.SsfNumber));
                    right.Item().Element(c => ComposePayslipPdfDetail(c, "Staff TIN", payslip.StaffTin));
                });
            });

            column.Item().PaddingTop(8).LineHorizontal(1);
            column.Item().Row(row =>
            {
                row.RelativeItem().Element(c => ComposePayslipLinesPdf(c, "EARNINGS", payslip.Earnings));
                row.ConstantItem(18);
                row.RelativeItem().Element(c => ComposePayslipLinesPdf(c, "DEDUCTIONS", payslip.Deductions));
            });

            column.Item().AlignCenter().Width(250).Border(1).Padding(6).Column(summary =>
            {
                summary.Spacing(3);
                summary.Item().Element(c => ComposePdfUnderlinedText(c, $"SALARY SUMMARY ({CurrencyLabel(payslip.CurrencyCode)})", 9, bold: true));
                summary.Item().Element(c => ComposePayslipPdfAmountLine(c, "Total Earnings :", totalEarnings, bold: false));
                summary.Item().Element(c => ComposePayslipPdfAmountLine(c, "Total Deductions :", totalDeductions, bold: false));
                summary.Item().Element(c => ComposePayslipPdfAmountLine(c, "Net Salary :", payslip.NetIncome, bold: true));
            });

            column.Item().Column(tax =>
            {
                tax.Spacing(3);
                tax.Item().Element(c => ComposePdfUnderlinedText(c, "TAX ANALYSIS", 10, bold: false));
                tax.Item().Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Spacing(2);
                        left.Item().Element(c => ComposePayslipPdfCompactAmountLine(c, "Taxable Earning", payslip.TaxableIncome, bold: false, labelWidth: 82));
                        left.Item().Element(c => ComposePayslipPdfCompactAmountLine(c, "Tax Relief", payslip.TaxRelief, bold: false, labelWidth: 82));
                    });
                    row.RelativeItem().Column(right =>
                    {
                        right.Spacing(2);
                        right.Item().Element(c => ComposePayslipPdfCompactAmountLine(c, "Income Tax (Normal)", normalIncomeTax, bold: false, labelWidth: 96));
                        right.Item().Element(c => ComposePayslipPdfCompactAmountLine(c, "Income Tax (Bonus)", bonusIncomeTax, bold: false, labelWidth: 96));
                        right.Item().Element(c => ComposePayslipPdfCompactAmountLine(c, "Income Tax (Total)", payslip.IncomeTax, bold: true, labelWidth: 96));
                    });
                });
            });

            column.Item().Element(c => ComposeContributionStatementPdf(c, payslip));
            column.Item().Element(c => ComposeBankDetailsPdf(c, payslip));
        });
    }

    private static void ComposeBonusSlipPdf(IContainer container, PayrollPayslipDto payslip)
    {
        var bonusLines = payslip.Earnings
            .Where(e => e.TransactionType.Equals(BonusTransactionType, StringComparison.OrdinalIgnoreCase) && e.Amount != 0)
            .ToList();
        var bonusAmount = bonusLines.Sum(e => e.Amount);
        var bank = payslip.BankDetails.FirstOrDefault();
        var description = string.Join(", ", bonusLines.Select(LineLabel).Where(e => !string.IsNullOrWhiteSpace(e)));
        if (string.IsNullOrWhiteSpace(description))
        {
            description = TrimOrNull(payslip.SeparateBonusCode) ?? "Bonus";
        }

        container.Column(column =>
        {
            column.Spacing(18);
            column.Item().Element(c => ComposePayslipPdfHeader(c, payslip, "Bonus Slip", 17));
            column.Item().AlignCenter().Width(430).Column(details =>
            {
                details.Spacing(6);
                details.Item().Element(c => ComposePayslipPdfDetail(c, "Staff No", payslip.EmployeeNumber));
                details.Item().Element(c => ComposePayslipPdfDetail(c, "Staff Name", payslip.EmployeeName));
                details.Item().Element(c => ComposePayslipPdfDetail(c, "Bank", bank?.BankName));
                details.Item().Element(c => ComposePayslipPdfDetail(c, "Acct No", bank?.AccountNumber));
            });

            column.Item().AlignCenter().Width(430).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(110);
                });

                table.Header(header =>
                {
                    header.Cell().BorderBottom(1).PaddingVertical(4).Text("Description").Bold();
                    header.Cell().BorderBottom(1).PaddingVertical(4).AlignRight().Text("Amount").Bold();
                });

                ComposeBonusPdfLine(table, description, bonusAmount, bold: false, borderTop: false);
                ComposeBonusPdfLine(table, "Income Tax", payslip.IncomeTax, bold: false, borderTop: false);
                ComposeBonusPdfLine(table, "Net Bonus", payslip.NetIncome, bold: true, borderTop: true);
            });
        });
    }

    private static void ComposePayslipPdfHeader(IContainer container, PayrollPayslipDto payslip, string title, int titleSize)
    {
        container.Column(column =>
        {
            column.Spacing(2);
            if (!string.IsNullOrWhiteSpace(payslip.CompanyName))
            {
                column.Item().AlignCenter().Text(payslip.CompanyName).Bold().FontSize(15);
            }
            if (!string.IsNullOrWhiteSpace(payslip.CompanyAddress))
            {
                column.Item().AlignCenter().Text(payslip.CompanyAddress).FontSize(8);
            }
            if (!string.IsNullOrWhiteSpace(payslip.CompanyPhone))
            {
                column.Item().AlignCenter().Text(payslip.CompanyPhone).FontSize(8);
            }
            column.Item().PaddingTop(8).Element(c => ComposePdfUnderlinedText(c, title, titleSize, bold: true));
        });
    }

    private static void ComposePayslipPdfDetail(IContainer container, string label, string? value)
    {
        container.Row(row =>
        {
            row.ConstantItem(92).AlignRight().Text(label).Bold();
            row.ConstantItem(10).AlignCenter().Text(":").Bold();
            row.RelativeItem().Text(value ?? string.Empty).SemiBold();
        });
    }

    private static void ComposePayslipLinesPdf(IContainer container, string title, IReadOnlyList<PayrollTransactionDto> lines)
    {
        var visibleLines = lines
            .Where(e => e.Amount != 0)
            .OrderBy(LineOrder)
            .ThenBy(LineLabel)
            .ToList();

        container.Column(column =>
        {
            column.Spacing(2);
            column.Item().PaddingBottom(2).Row(header =>
            {
                header.ConstantItem(32).Element(c => ComposePdfUnderlinedText(c, "Item", 8.5f, bold: true, alignment: PdfUnderlineAlignment.Left));
                header.RelativeItem().Element(c => ComposePdfUnderlinedText(c, title, 10.5f, bold: true));
                header.ConstantItem(70).Element(c => ComposePdfUnderlinedText(c, "Amount", 8.5f, bold: true, alignment: PdfUnderlineAlignment.Right));
            });

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.ConstantColumn(70);
                });

                foreach (var line in visibleLines)
                {
                    table.Cell().PaddingVertical(1).Text(LineLabel(line)).FontSize(8);
                    table.Cell().PaddingVertical(1).AlignRight().Text(FormatEmailAmount(line.Amount)).FontSize(8);
                }
            });
        });
    }

    private static void ComposePayslipPdfAmountLine(IContainer container, string label, decimal amount, bool bold)
    {
        container.Row(row =>
        {
            var labelCell = row.RelativeItem().Text(label);
            var amountCell = row.ConstantItem(92).AlignRight().Text(FormatEmailAmount(amount));
            if (bold)
            {
                labelCell.Bold();
                amountCell.Bold();
            }
        });
    }

    private static void ComposePayslipPdfCompactAmountLine(IContainer container, string label, decimal amount, bool bold, float labelWidth)
    {
        container.Row(row =>
        {
            var labelCell = row.ConstantItem(labelWidth).Text(label);
            var colonCell = row.ConstantItem(5).AlignCenter().Text(":");
            var amountCell = row.ConstantItem(72).AlignRight().Text(FormatEmailAmount(amount));
            if (bold)
            {
                labelCell.Bold();
                colonCell.Bold();
                amountCell.Bold();
            }
        });
    }

    private static void ComposeContributionStatementPdf(IContainer container, PayrollPayslipDto payslip)
    {
        var providentRows = payslip.Contributions.Where(e => e.IsProvidentFund).ToList();
        var statutoryRows = payslip.Contributions.Where(e => !e.IsProvidentFund).ToList();
        if (statutoryRows.Count == 0)
        {
            statutoryRows.Add(new PayrollPayslipContributionDto { Item = "SOCIAL SECURITY FUND" });
        }

        container.DefaultTextStyle(x => x.FontSize(6.7f)).Column(column =>
        {
            column.Spacing(4);
            column.Item().Element(c => ComposePdfUnderlinedText(c, "CONTRIBUTION STATEMENT", 9, bold: false));
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.45f);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    ComposePdfHeaderCell(header, "Item", alignRight: false);
                    ComposePdfHeaderCell(header, "Employee's Contr.", alignRight: true);
                    ComposePdfHeaderCell(header, "Employer's Contr.", alignRight: true);
                    ComposePdfHeaderCell(header, "Total Contri.", alignRight: true);
                    ComposePdfHeaderCell(header, "Opening Bal.", alignRight: true);
                    ComposePdfHeaderCell(header, "Total Withdrawal", alignRight: true);
                    ComposePdfHeaderCell(header, "Grand Total", alignRight: true);
                });

                foreach (var line in providentRows)
                {
                    ComposePdfTextCell(table, line.Item, alignRight: false);
                    ComposePdfTextCell(table, FormatEmailAmount(line.EmployeeContribution), alignRight: true);
                    ComposePdfTextCell(table, FormatEmailAmount(line.EmployerContribution), alignRight: true);
                    ComposePdfTextCell(table, FormatEmailAmount(line.TotalContribution), alignRight: true);
                    ComposePdfTextCell(table, FormatEmailAmount(line.OpeningBalance), alignRight: true);
                    ComposePdfTextCell(table, line.TotalWithdrawal == 0 ? string.Empty : FormatEmailAmount(line.TotalWithdrawal), alignRight: true);
                    ComposePdfTextCell(table, FormatEmailAmount(line.GrandTotal), alignRight: true);
                }
            });

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    ComposePdfHeaderCell(header, "Item", alignRight: false);
                    ComposePdfHeaderCell(header, "Employee's Contr.", alignRight: true);
                    ComposePdfHeaderCell(header, "Employer's Contr.", alignRight: true);
                    ComposePdfHeaderCell(header, "Total Contribution", alignRight: true);
                    ComposePdfHeaderCell(header, "1st Tier", alignRight: true);
                    ComposePdfHeaderCell(header, "2nd Tier", alignRight: true);
                });

                foreach (var line in statutoryRows)
                {
                    ComposePdfTextCell(table, line.Item, alignRight: false);
                    ComposePdfTextCell(table, FormatEmailAmount(line.EmployeeContribution), alignRight: true);
                    ComposePdfTextCell(table, FormatEmailAmount(line.EmployerContribution), alignRight: true);
                    ComposePdfTextCell(table, FormatEmailAmount(line.TotalContribution), alignRight: true);
                    ComposePdfTextCell(table, FormatEmailAmount(line.FirstTier), alignRight: true);
                    ComposePdfTextCell(table, FormatEmailAmount(line.SecondTier), alignRight: true);
                }
            });
        });
    }

    private static void ComposeBankDetailsPdf(IContainer container, PayrollPayslipDto payslip)
    {
        IReadOnlyList<PayrollPayslipBankDetailDto> rows = payslip.BankDetails.Count > 0
            ? payslip.BankDetails
            : new List<PayrollPayslipBankDetailDto>
            {
                new() { CurrencyCode = payslip.CurrencyCode, Amount = payslip.NetIncome }
            };

        container.BorderTop(1).PaddingTop(4).DefaultTextStyle(x => x.FontSize(7.5f)).Column(column =>
        {
            column.Spacing(4);
            column.Item().Element(c => ComposePdfUnderlinedText(c, "BANK DETAILS", 9, bold: false));
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2.4f);
                    columns.RelativeColumn(1.3f);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn(1.2f);
                });

                table.Header(header =>
                {
                    ComposePdfHeaderCell(header, "Bank", alignRight: false);
                    ComposePdfHeaderCell(header, "Acct No", alignRight: false);
                    ComposePdfHeaderCell(header, "Currency", alignRight: false);
                    ComposePdfHeaderCell(header, "Exch. Rate", alignRight: false);
                    ComposePdfHeaderCell(header, "Amount", alignRight: true);
                });

                foreach (var row in rows)
                {
                    ComposePdfTextCell(table, row.BankName, alignRight: false);
                    ComposePdfTextCell(table, row.AccountNumber, alignRight: false);
                    ComposePdfTextCell(table, CurrencyLabel(row.CurrencyCode), alignRight: false);
                    ComposePdfTextCell(table, row.ExchangeRate.HasValue ? FormatEmailAmount(row.ExchangeRate.Value) : string.Empty, alignRight: false);
                    ComposePdfTextCell(table, FormatEmailAmount(row.Amount), alignRight: true);
                }
            });
        });
    }

    private static void ComposeBonusPdfLine(TableDescriptor table, string label, decimal amount, bool bold, bool borderTop)
    {
        var labelCell = table.Cell();
        var amountCell = table.Cell();
        var labelContainer = borderTop ? labelCell.BorderTop(1) : labelCell;
        var amountContainer = borderTop ? amountCell.BorderTop(1) : amountCell;
        var labelText = labelContainer.PaddingVertical(5).Text(label);
        var amountText = amountContainer.PaddingVertical(5).AlignRight().Text(FormatEmailAmount(amount));
        if (bold)
        {
            labelText.Bold();
            amountText.Bold();
        }
    }

    private static void ComposePdfUnderlinedText(
        IContainer container,
        string text,
        float fontSize,
        bool bold = false,
        PdfUnderlineAlignment alignment = PdfUnderlineAlignment.Center)
    {
        var width = EstimatePdfUnderlineWidth(text, fontSize, bold);
        var aligned = alignment switch
        {
            PdfUnderlineAlignment.Left => container.AlignLeft(),
            PdfUnderlineAlignment.Right => container.AlignRight(),
            _ => container.AlignCenter()
        };

        aligned.Width(width).Column(column =>
        {
            column.Spacing(1);
            var line = column.Item().AlignCenter().Text(text).FontSize(fontSize);
            if (bold)
            {
                line.Bold();
            }

            column.Item().LineHorizontal(0.65f);
        });
    }

    private static float EstimatePdfUnderlineWidth(string text, float fontSize, bool bold)
    {
        var characterWidth = bold ? 0.82f : 0.76f;
        var estimated = text.Length * fontSize * characterWidth;
        return Math.Max(28, Math.Min(260, estimated));
    }

    private static void ComposePdfHeaderCell(TableCellDescriptor header, string text, bool alignRight)
    {
        var cell = header.Cell().BorderBottom(0.5f).PaddingBottom(2);
        if (alignRight)
        {
            cell.AlignRight().Text(text).Bold();
        }
        else
        {
            cell.Text(text).Bold();
        }
    }

    private enum PdfUnderlineAlignment
    {
        Left,
        Center,
        Right
    }

    private static void ComposePdfTextCell(TableDescriptor table, string? text, bool alignRight)
    {
        var cell = table.Cell().PaddingVertical(1);
        if (alignRight)
        {
            cell.AlignRight().Text(text ?? string.Empty);
        }
        else
        {
            cell.Text(text ?? string.Empty);
        }
    }

    private static string Underline(string value)
        => $"<span class=\"pps-underline\">{Html(value)}</span>";

    private static string CurrencyLabel(string? currency)
    {
        var code = string.IsNullOrWhiteSpace(currency) ? "GHS" : currency.Trim().ToUpperInvariant();
        return code == "GHS" ? "GH\u00a2" : code;
    }

    private static string FormatPayslipMonth(DateTime value)
        => value.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    private static string LineLabel(PayrollTransactionDto line)
    {
        var type = line.TransactionType;
        if (type == BasicSalaryTransactionType) return "BASIC SALARY";
        if (type == IncomeTaxTransactionType)
        {
            if ((line.ComponentCode?.Equals("BON", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (line.Description?.Contains("bonus", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                return "INCOME TAX (BONUS)";
            }

            return "INCOME TAX - TOTAL";
        }
        if (type == LoanInterestTransactionType) return "LOAN INTEREST";
        if (type == EmployeePensionTransactionType) return "SOCIAL SECURITY FUND";
        if (type == SalaryAdvanceTransactionType) return "SALARY ADVANCE";
        if (type == OvertimeTransactionType) return "OVERTIME";
        if (type == PromotionBasicArrearsTransactionType) return "PROMOTION BASIC ARREARS";
        if (type == PromotionAllowanceArrearsTransactionType) return "PROMOTION ALLOWANCE ARREARS";
        if (type is PromotionEmployeeContributionArrearsTransactionType or PromotionEmployerContributionArrearsTransactionType or PromotionContributionArrearsTransactionType)
        {
            return "PROMOTION CONTRIBUTION ARREARS";
        }

        return (line.Description ?? line.ComponentCode ?? type).ToUpperInvariant();
    }

    private static int LineOrder(PayrollTransactionDto line)
        => line.TransactionType switch
        {
            BasicSalaryTransactionType or IncomeTaxTransactionType => 0,
            "Allowance" or "Benefit" or "Deduction" => 10,
            "EmployeeContribution" or EmployeePensionTransactionType => 20,
            "LoanRepayment" or LoanRepaymentTransactionType or LoanInterestTransactionType or SalaryAdvanceTransactionType => 30,
            OvertimeTransactionType or PromotionBasicArrearsTransactionType or PromotionAllowanceArrearsTransactionType or PromotionEmployeeContributionArrearsTransactionType or PromotionEmployerContributionArrearsTransactionType or PromotionContributionArrearsTransactionType => 40,
            _ => 50
        };

    private static string BuildPayslipEmailFileName(PayrollPayslipDto payslip)
    {
        var employee = new string(payslip.EmployeeNumber.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-').ToArray());
        return $"{(payslip.IsSeparateBonusRun ? "Bonus-Slip" : "Payslip")}-{employee}-{payslip.PayPeriod}.pdf";
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
            return BuildPaymentAllocations(payrollMethods, runEmployee.NetIncome, runEmployee.CurrencyCode)
                .Select(allocation => new PayrollPayslipBankDetailDto
                {
                    BankName = BuildPaymentMethodBankName(allocation.Method, context),
                    AccountNumber = allocation.Method.AccountNumber,
                    CurrencyCode = string.IsNullOrWhiteSpace(allocation.Method.CurrencyCode) ? runEmployee.CurrencyCode : allocation.Method.CurrencyCode,
                    ExchangeRate = allocation.ExchangeRate,
                    Amount = allocation.Amount
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
                ExchangeRate = null,
                Amount = bank.AllocationPercentage > 0
                    ? Math.Round(runEmployee.NetIncome * (bank.AllocationPercentage / 100m), 2)
                    : bankDetails.Count == 1 || index == 0
                        ? runEmployee.NetIncome
                        : 0m
            })
            .ToList();
    }

    private static IReadOnlyList<PayrollPaymentAllocation> BuildPaymentAllocations(
        IReadOnlyList<PayrollPaymentMethod> methods,
        decimal basePaymentAmount,
        string baseCurrencyCode)
    {
        var orderedMethods = methods.OrderBy(e => e.SequenceNo).ToList();
        if (orderedMethods.Count == 0)
        {
            return [];
        }

        var baseCurrency = NormalizeCurrency(baseCurrencyCode);
        var allocations = new List<PayrollPaymentAllocation>();
        var fixedMethods = orderedMethods.Where(IsFixedPaymentMethod).ToList();
        var percentageMethods = orderedMethods.Where(e => !IsFixedPaymentMethod(e)).ToList();

        foreach (var method in fixedMethods)
        {
            var amount = RoundMoney(method.Amount.GetValueOrDefault());
            var exchangeRate = ResolvePaymentMethodExchangeRate(method);
            var baseAmount = RoundMoney(amount * exchangeRate);
            allocations.Add(new PayrollPaymentAllocation(method, amount, exchangeRate, baseAmount));
        }

        var fixedBaseTotal = allocations.Sum(e => e.BaseAmount);
        var remainingBase = Math.Max(0m, RoundMoney(basePaymentAmount - fixedBaseTotal));

        if (percentageMethods.Count > 0)
        {
            foreach (var method in percentageMethods)
            {
                var exchangeRate = ResolvePaymentMethodExchangeRate(method);
                var baseAmount = RoundMoney(remainingBase * (method.PaymentPercent.GetValueOrDefault() / 100m));
                var amount = ConvertBaseAmountToPaymentCurrency(method, baseAmount, baseCurrency, exchangeRate);
                allocations.Add(new PayrollPaymentAllocation(method, amount, exchangeRate, baseAmount));
            }

            return allocations.OrderBy(e => e.Method.SequenceNo).ToList();
        }

        if (allocations.Count == 0)
        {
            var method = orderedMethods[0];
            var exchangeRate = ResolvePaymentMethodExchangeRate(method);
            var amount = ConvertBaseAmountToPaymentCurrency(method, basePaymentAmount, baseCurrency, exchangeRate);
            allocations.Add(new PayrollPaymentAllocation(method, amount, exchangeRate, RoundMoney(basePaymentAmount)));
        }

        return allocations.OrderBy(e => e.Method.SequenceNo).ToList();
    }

    private static bool IsFixedPaymentMethod(PayrollPaymentMethod method)
        => NormalizePaymentMode(method.PaymentMode, method.Amount, method.PaymentPercent) == PaymentModeFixedAmount;

    private static decimal ResolvePaymentMethodExchangeRate(PayrollPaymentMethod method)
    {
        var exchangeRate = method.ExchangeRate ?? 1m;
        return exchangeRate > 0m ? exchangeRate : 1m;
    }

    private static decimal ConvertBaseAmountToPaymentCurrency(
        PayrollPaymentMethod method,
        decimal baseAmount,
        string baseCurrency,
        decimal exchangeRate)
    {
        var paymentCurrency = NormalizeCurrency(method.CurrencyCode);
        if (!paymentCurrency.Equals(baseCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return RoundMoney(baseAmount / exchangeRate);
        }

        return RoundMoney(baseAmount);
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
        var bankName = ResolvePaymentMethodBankName(method, context);
        var branchName = ResolvePaymentMethodBankBranchName(method, context);
        var parts = new[] { bankName, branchName }
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e!.Trim())
            .ToList();

        return parts.Count == 0 ? "BANK" : string.Join(" - ", parts);
    }

    private static string? ResolvePaymentMethodBankName(PayrollPaymentMethod method, PayrollPayslipBuildContext context)
    {
        var bankCode = TrimOrNull(method.BankCode);
        return ResolveLookupValue(bankCode, context.EmployeeBankNamesByReference, context.PayrollBankNamesByCode) ?? bankCode;
    }

    private static string? ResolvePaymentMethodBankBranchName(PayrollPaymentMethod method, PayrollPayslipBuildContext context)
    {
        var bankCode = TrimOrNull(method.BankCode);
        var branchCode = TrimOrNull(method.BankBranchCode);
        var branchKey = bankCode != null && branchCode != null ? $"{bankCode}|{branchCode}" : null;
        return ResolveLookupValue(branchKey, context.EmployeeBankBranchNamesByReference, context.PayrollBankBranchNamesByReference) ??
               ResolveLookupValue(branchCode, context.EmployeeBankBranchNamesByReference, context.PayrollBankBranchNamesByReference) ??
               branchCode;
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
        var bankName = ResolveEmployeeBankName(bank);
        var branchName = ResolveEmployeeBankBranchName(bank);
        var parts = new[] { bankName, branchName }
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e!.Trim())
            .ToList();

        return parts.Count == 0 ? "BANK" : string.Join(" - ", parts);
    }

    private static string? ResolveEmployeeBankName(EmployeeBankDetail bank)
        => TrimOrNull(bank.Bank?.Name) ?? TrimOrNull(bank.BankName);

    private static string? ResolveEmployeeBankBranchName(EmployeeBankDetail bank)
        => TrimOrNull(bank.Branch?.Name) ?? TrimOrNull(bank.BranchName) ?? TrimOrNull(bank.Branch?.Code);

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
            or PromotionContributionArrearsTransactionType
            or BackpayDeductionArrearsTransactionType;

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
        PayrollRun run,
        IReadOnlyList<PayrollTransaction> transactions,
        CancellationToken cancellationToken)
    {
        var mappings = await _context.PayrollJournalMappings
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.IsActive)
            .OrderBy(e => e.SequenceNo)
            .ThenBy(e => e.TransactionType)
            .ThenBy(e => e.ComponentCode)
            .ThenBy(e => e.DebitCredit)
            .ThenBy(e => e.AccountCode)
            .ToListAsync(cancellationToken);

        var employeeIds = transactions
            .Select(e => e.EmployeeId)
            .Where(e => e != Guid.Empty)
            .Distinct()
            .ToList();
        var paymentProfiles = employeeIds.Count == 0
            ? new List<PayrollEmployeeProfile>()
            : await _context.PayrollEmployeeProfiles
                .AsNoTracking()
                .Include(e => e.PaymentMethods.OrderBy(m => m.SequenceNo))
                .Where(e => e.TenantId == tenantId && employeeIds.Contains(e.EmployeeId))
                .ToListAsync(cancellationToken);
        var profilesByEmployeeId = paymentProfiles
            .GroupBy(e => e.EmployeeId)
            .ToDictionary(e => e.Key, e => e.First());

        var lines = new List<PayrollJournalLine>();
        var sequence = 1;

        var regularGroups = transactions
            .Where(e => IsRegularPayrollJournalTransaction(e.TransactionType))
            .GroupBy(e => new { e.TransactionType, e.ComponentCode })
            .OrderBy(e => PayrollJournalSortOrder(e.Key.TransactionType))
            .ThenBy(e => e.Key.TransactionType)
            .ThenBy(e => e.Key.ComponentCode)
            .ToList();

        foreach (var group in regularGroups)
        {
            var amount = group.Sum(e => e.Amount);
            var description = group
                .Select(e => TrimOrNull(e.Description))
                .FirstOrDefault(e => e != null) ?? $"{group.Key.TransactionType} payroll journal";
            var matchedMappings = FindPayrollJournalMappings(mappings, group.Key.TransactionType, group.Key.ComponentCode);

            AddPayrollJournalLines(
                lines,
                tenantId,
                run.Id,
                ref sequence,
                group.Key.TransactionType,
                description,
                amount,
                matchedMappings,
                InferDebitCredit(group.Key.TransactionType));
        }

        AddNetPayJournalLines(tenantId, run, transactions, mappings, profilesByEmployeeId, lines, ref sequence);
        AddEmployerContributionJournalLines(tenantId, run.Id, transactions, mappings, lines, ref sequence);

        return lines;
    }

    private static bool IsRegularPayrollJournalTransaction(string transactionType)
        => !transactionType.Equals(TaxReliefTransactionType, StringComparison.OrdinalIgnoreCase) &&
           !transactionType.Equals(NetPayTransactionType, StringComparison.OrdinalIgnoreCase) &&
           !IsEmployerOnlyPayrollJournalTransaction(transactionType);

    private static bool IsEmployerOnlyPayrollJournalTransaction(string transactionType)
        => transactionType.Equals(EmployerPensionTransactionType, StringComparison.OrdinalIgnoreCase) ||
           transactionType.Equals(PayrollComponentType.EmployerContribution.ToString(), StringComparison.OrdinalIgnoreCase) ||
           transactionType.Equals(PromotionEmployerContributionArrearsTransactionType, StringComparison.OrdinalIgnoreCase);

    private static int PayrollJournalSortOrder(string transactionType)
    {
        if (transactionType.Equals(BasicSalaryTransactionType, StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(PayrollComponentType.Allowance.ToString(), StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(PayrollComponentType.Benefit.ToString(), StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(BonusTransactionType, StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(OvertimeTransactionType, StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(PromotionBasicArrearsTransactionType, StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(PromotionAllowanceArrearsTransactionType, StringComparison.OrdinalIgnoreCase))
        {
            return 10;
        }

        if (transactionType.Equals(IncomeTaxTransactionType, StringComparison.OrdinalIgnoreCase))
        {
            return 20;
        }

        if (transactionType.Equals(EmployeePensionTransactionType, StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(PayrollComponentType.EmployeeContribution.ToString(), StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(PayrollComponentType.Deduction.ToString(), StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(LoanRepaymentTransactionType, StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(LoanInterestTransactionType, StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(SalaryAdvanceTransactionType, StringComparison.OrdinalIgnoreCase))
        {
            return 30;
        }

        return 50;
    }

    private static IReadOnlyList<PayrollJournalMapping> FindPayrollJournalMappings(
        IReadOnlyList<PayrollJournalMapping> mappings,
        string transactionType,
        string? componentCode,
        IEnumerable<string>? extraAliases = null)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddPayrollJournalMappingKey(keys, transactionType);
        foreach (var alias in PayrollJournalTransactionAliases(transactionType))
        {
            AddPayrollJournalMappingKey(keys, alias);
        }

        if (extraAliases != null)
        {
            foreach (var alias in extraAliases)
            {
                AddPayrollJournalMappingKey(keys, alias);
            }
        }

        var candidates = mappings
            .Where(e => keys.Contains(e.TransactionType.Trim()))
            .ToList();
        var component = TrimOrNull(componentCode);
        if (component != null)
        {
            var specific = candidates
                .Where(e => !string.IsNullOrWhiteSpace(e.ComponentCode) &&
                            e.ComponentCode.Trim().Equals(component, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (specific.Count > 0)
            {
                return specific;
            }
        }

        return candidates
            .Where(e => string.IsNullOrWhiteSpace(e.ComponentCode))
            .ToList();
    }

    private static void AddPayrollJournalMappingKey(ISet<string> keys, string? value)
    {
        var key = TrimOrNull(value);
        if (key != null)
        {
            keys.Add(key);
        }
    }

    private static IReadOnlyList<string> PayrollJournalTransactionAliases(string transactionType)
    {
        if (transactionType.Equals(BasicSalaryTransactionType, StringComparison.OrdinalIgnoreCase))
        {
            return ["BAS"];
        }

        if (transactionType.Equals(IncomeTaxTransactionType, StringComparison.OrdinalIgnoreCase))
        {
            return ["TAX"];
        }

        if (transactionType.Equals(NetPayTransactionType, StringComparison.OrdinalIgnoreCase))
        {
            return ["BAN", "CAS", "Bank", "Cash"];
        }

        if (transactionType.Equals(EmployeePensionTransactionType, StringComparison.OrdinalIgnoreCase))
        {
            return ["ESF"];
        }

        if (transactionType.Equals(EmployerPensionTransactionType, StringComparison.OrdinalIgnoreCase))
        {
            return ["CSF"];
        }

        if (transactionType.Equals(PayrollComponentType.Allowance.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return ["ALW"];
        }

        if (transactionType.Equals(PayrollComponentType.Benefit.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return ["BEN"];
        }

        if (transactionType.Equals(PayrollComponentType.Deduction.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return ["DED"];
        }

        if (transactionType.Equals(PayrollComponentType.EmployeeContribution.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return ["CON"];
        }

        if (transactionType.Equals(PayrollComponentType.EmployerContribution.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return ["ECO"];
        }

        if (transactionType.Equals(PromotionEmployerContributionArrearsTransactionType, StringComparison.OrdinalIgnoreCase))
        {
            return ["CSR"];
        }

        if (transactionType.Equals(PromotionBasicArrearsTransactionType, StringComparison.OrdinalIgnoreCase))
        {
            return ["BAR"];
        }

        if (transactionType.Equals(PromotionAllowanceArrearsTransactionType, StringComparison.OrdinalIgnoreCase))
        {
            return ["ALR"];
        }

        if (transactionType.Equals(PromotionEmployeeContributionArrearsTransactionType, StringComparison.OrdinalIgnoreCase))
        {
            return ["ESR"];
        }

        if (transactionType.Equals(BackpayDeductionArrearsTransactionType, StringComparison.OrdinalIgnoreCase))
        {
            return ["DER"];
        }

        return [];
    }

    private static IReadOnlyList<string> PayrollPaymentJournalAliases(string paymentType)
    {
        var aliases = new List<string> { NetPayTransactionType };
        var normalized = TrimOrNull(paymentType);
        if (normalized == null)
        {
            aliases.Add("BAN");
            return aliases;
        }

        aliases.Add(normalized);
        aliases.Add(normalized.Length >= 3 ? normalized[..3].ToUpperInvariant() : normalized.ToUpperInvariant());
        if (normalized.Equals("Bank", StringComparison.OrdinalIgnoreCase))
        {
            aliases.Add("BAN");
        }
        else if (normalized.Equals("Cash", StringComparison.OrdinalIgnoreCase))
        {
            aliases.Add("CAS");
        }

        return aliases;
    }

    private static void AddPayrollJournalLines(
        ICollection<PayrollJournalLine> lines,
        Guid tenantId,
        Guid runId,
        ref int sequence,
        string transactionType,
        string description,
        decimal amount,
        IReadOnlyList<PayrollJournalMapping> mappings,
        string fallbackDebitCredit)
    {
        var roundedAmount = RoundMoney(Math.Abs(amount));
        if (roundedAmount <= 0m)
        {
            return;
        }

        if (mappings.Count == 0)
        {
            AddPayrollJournalLine(
                lines,
                tenantId,
                runId,
                ref sequence,
                transactionType,
                fallbackDebitCredit,
                "UNMAPPED",
                description,
                roundedAmount);
            return;
        }

        foreach (var mapping in mappings)
        {
            AddPayrollJournalLine(
                lines,
                tenantId,
                runId,
                ref sequence,
                TrimOrNull(mapping.TransactionType) ?? transactionType,
                NormalizeDebitCredit(mapping.DebitCredit),
                mapping.AccountCode,
                TrimOrNull(mapping.Description) ?? description,
                roundedAmount);
        }
    }

    private static void AddPayrollJournalLine(
        ICollection<PayrollJournalLine> lines,
        Guid tenantId,
        Guid runId,
        ref int sequence,
        string transactionType,
        string debitCredit,
        string? accountCode,
        string description,
        decimal amount)
    {
        lines.Add(new PayrollJournalLine
        {
            TenantId = tenantId,
            PayrollRunId = runId,
            SequenceNo = sequence++,
            TransactionType = transactionType,
            DebitCredit = NormalizeDebitCredit(debitCredit),
            AccountCode = TrimOrNull(accountCode) ?? "UNMAPPED",
            Description = description,
            Amount = RoundMoney(Math.Abs(amount)),
            Posted = false
        });
    }

    private static void AddNetPayJournalLines(
        Guid tenantId,
        PayrollRun run,
        IReadOnlyList<PayrollTransaction> transactions,
        IReadOnlyList<PayrollJournalMapping> mappings,
        IReadOnlyDictionary<Guid, PayrollEmployeeProfile> profilesByEmployeeId,
        ICollection<PayrollJournalLine> lines,
        ref int sequence)
    {
        var netTransactions = transactions
            .Where(e => e.TransactionType.Equals(NetPayTransactionType, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var netPayByPaymentType = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var transaction in netTransactions)
        {
            var amount = RoundMoney(transaction.Amount);
            if (amount == 0m)
            {
                continue;
            }

            if (profilesByEmployeeId.TryGetValue(transaction.EmployeeId, out var profile))
            {
                var paymentMethods = profile.PaymentMethods
                    .Where(e => e.IsActive && IsEffective(e.StartDate, e.EndDate, run.PayPeriodFrom, run.PayPeriodTo))
                    .OrderBy(e => e.SequenceNo)
                    .ToList();

                if (paymentMethods.Count > 0)
                {
                    foreach (var allocation in BuildPaymentAllocations(paymentMethods, amount, transaction.CurrencyCode))
                    {
                        AddJournalAmount(netPayByPaymentType, NormalizePayrollPaymentType(allocation.Method.PaymentType), allocation.BaseAmount);
                    }

                    continue;
                }
            }

            AddJournalAmount(netPayByPaymentType, "Bank", amount);
        }

        if (netTransactions.Count == 0 && run.NetAmount != 0m)
        {
            AddJournalAmount(netPayByPaymentType, "Bank", run.NetAmount);
        }

        foreach (var paymentGroup in netPayByPaymentType.OrderBy(e => e.Key))
        {
            var matchedMappings = FindPayrollJournalMappings(
                mappings,
                paymentGroup.Key,
                componentCode: null,
                extraAliases: PayrollPaymentJournalAliases(paymentGroup.Key));
            AddPayrollJournalLines(
                lines,
                tenantId,
                run.Id,
                ref sequence,
                paymentGroup.Key,
                $"{paymentGroup.Key} net pay",
                paymentGroup.Value,
                matchedMappings,
                "CR");
        }
    }

    private static string NormalizePayrollPaymentType(string? paymentType)
        => TrimOrNull(paymentType) ?? "Bank";

    private static void AddJournalAmount(IDictionary<string, decimal> amounts, string key, decimal amount)
    {
        var roundedAmount = RoundMoney(amount);
        if (roundedAmount == 0m)
        {
            return;
        }

        amounts.TryGetValue(key, out var currentAmount);
        amounts[key] = RoundMoney(currentAmount + roundedAmount);
    }

    private static void AddEmployerContributionJournalLines(
        Guid tenantId,
        Guid runId,
        IReadOnlyList<PayrollTransaction> transactions,
        IReadOnlyList<PayrollJournalMapping> mappings,
        ICollection<PayrollJournalLine> lines,
        ref int sequence)
    {
        var groups = transactions
            .Select(e => new
            {
                Transaction = e,
                JournalTransactionType = ResolveEmployerContributionJournalTransactionType(e.TransactionType),
                MappingTransactionType = ResolveEmployerContributionJournalMappingType(e.TransactionType),
                Amount = ResolveEmployerContributionJournalAmount(e)
            })
            .Where(e => e.Amount != 0m && e.JournalTransactionType != null && e.MappingTransactionType != null)
            .GroupBy(e => new
            {
                TransactionType = e.JournalTransactionType!,
                MappingTransactionType = e.MappingTransactionType!,
                e.Transaction.ComponentCode
            })
            .OrderBy(e => e.Key.TransactionType)
            .ThenBy(e => e.Key.ComponentCode)
            .ToList();

        foreach (var group in groups)
        {
            var amount = group.Sum(e => e.Amount);
            var description = group
                .Select(e => TrimOrNull(e.Transaction.Description))
                .FirstOrDefault(e => e != null) ?? $"{group.Key.TransactionType} payroll journal";
            var matchedMappings = FindPayrollJournalMappings(mappings, group.Key.MappingTransactionType, group.Key.ComponentCode);
            AddEmployerContributionJournalPair(
                lines,
                tenantId,
                runId,
                ref sequence,
                group.Key.TransactionType,
                $"Employer {description}",
                amount,
                matchedMappings);
        }
    }

    private static string? ResolveEmployerContributionJournalTransactionType(string transactionType)
    {
        if (transactionType.Equals(EmployerPensionTransactionType, StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(PromotionEmployerContributionArrearsTransactionType, StringComparison.OrdinalIgnoreCase))
        {
            return transactionType;
        }

        if (transactionType.Equals(PayrollComponentType.EmployerContribution.ToString(), StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(PayrollComponentType.EmployeeContribution.ToString(), StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(PromotionContributionArrearsTransactionType, StringComparison.OrdinalIgnoreCase))
        {
            return PayrollComponentType.EmployerContribution.ToString();
        }

        return null;
    }

    private static string? ResolveEmployerContributionJournalMappingType(string transactionType)
    {
        if (transactionType.Equals(EmployerPensionTransactionType, StringComparison.OrdinalIgnoreCase))
        {
            return EmployerPensionTransactionType;
        }

        if (transactionType.Equals(PromotionEmployerContributionArrearsTransactionType, StringComparison.OrdinalIgnoreCase))
        {
            return PromotionEmployerContributionArrearsTransactionType;
        }

        if (transactionType.Equals(PromotionContributionArrearsTransactionType, StringComparison.OrdinalIgnoreCase))
        {
            return "ECR";
        }

        if (transactionType.Equals(PayrollComponentType.EmployerContribution.ToString(), StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(PayrollComponentType.EmployeeContribution.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return "ECO";
        }

        return null;
    }

    private static decimal ResolveEmployerContributionJournalAmount(PayrollTransaction transaction)
    {
        if (IsEmployerOnlyPayrollJournalTransaction(transaction.TransactionType))
        {
            return transaction.EmployerAmount ?? transaction.Amount;
        }

        if (transaction.TransactionType.Equals(PayrollComponentType.EmployeeContribution.ToString(), StringComparison.OrdinalIgnoreCase) ||
            transaction.TransactionType.Equals(PromotionContributionArrearsTransactionType, StringComparison.OrdinalIgnoreCase))
        {
            return transaction.EmployerAmount.GetValueOrDefault();
        }

        return 0m;
    }

    private static void AddEmployerContributionJournalPair(
        ICollection<PayrollJournalLine> lines,
        Guid tenantId,
        Guid runId,
        ref int sequence,
        string transactionType,
        string description,
        decimal amount,
        IReadOnlyList<PayrollJournalMapping> mappings)
    {
        var debitMappings = mappings
            .Where(e => NormalizeDebitCredit(e.DebitCredit) == "DR")
            .ToList();
        var creditMappings = mappings
            .Where(e => NormalizeDebitCredit(e.DebitCredit) == "CR")
            .ToList();

        AddPayrollJournalLines(
            lines,
            tenantId,
            runId,
            ref sequence,
            transactionType,
            $"{description} expense",
            amount,
            debitMappings,
            "DR");

        AddPayrollJournalLines(
            lines,
            tenantId,
            runId,
            ref sequence,
            transactionType,
            $"{description} payable",
            amount,
            creditMappings,
            "CR");
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

    private static decimal CalculateComponentAmount(
        ResolvedPayrollComponent componentLine,
        decimal basicSalary,
        decimal? percentageBase = null)
    {
        var component = componentLine.Component;
        var employeeOverride = componentLine.Override;
        var rule = componentLine.Rule;
        var calculationType = employeeOverride?.CalculationTypeOverride ?? rule?.CalculationType ?? component.CalculationType;
        var amount = employeeOverride?.AmountOverride ?? rule?.Amount ?? component.Amount;
        var rate = employeeOverride?.RateOverride ?? rule?.Amount ?? component.Rate;

        return calculationType == PayrollCalculationType.PercentageOfBasic
            ? Math.Round((percentageBase ?? basicSalary) * (rate == 0 ? amount : rate) / 100m, 2)
            : Math.Round(amount, 2);
    }

    private static bool IsComponentTaxable(ResolvedPayrollComponent componentLine)
    {
        if (componentLine.Override?.TaxableOverride is { } overrideValue)
        {
            return overrideValue;
        }

        if (componentLine.Rule is { } rule)
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
        IReadOnlyDictionary<string, IReadOnlyList<PayrollBonusRule>> rulesByBonusCode,
        IReadOnlyDictionary<string, IReadOnlyList<PayrollBonusException>> exceptionsByBonusCode,
        PayrollEmployeeProfile profile,
        decimal basicSalary,
        decimal annualBasicSalary,
        decimal previousYearBonus,
        DateTime payPeriodTo,
        string runCurrencyCode,
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
            rulesByBonusCode.TryGetValue(NormalizeMatchToken(policy.Code), out var policyRules);
            exceptionsByBonusCode.TryGetValue(NormalizeMatchToken(policy.Code), out var policyExceptions);
            var matchingRule = FindMatchingBonusRule(policy, policyRules, profile);
            var matchingException = FindMatchingBonusException(policyExceptions, profile);
            var isException = matchingException != null;

            if (!isException && matchingRule is { Applicable: false })
            {
                continue;
            }

            if (!isException && matchingRule == null && policyRules?.Count > 0)
            {
                continue;
            }

            if (!isException && matchingRule == null && !BonusPolicyAppliesToProfile(policy, profile))
            {
                continue;
            }

            if (matchingException is { Applicable: false })
            {
                continue;
            }

            if (!isException && !BonusEmployeeMeetsMinimumMonths(policy, profile, payPeriodTo))
            {
                continue;
            }

            var calculationType = matchingException?.CalculationType ?? matchingRule?.CalculationType ?? policy.CalculationType;
            var configuredAmount = matchingException?.Amount ?? matchingRule?.Amount ?? policy.Amount;
            if (matchingException != null && calculationType != PayrollCalculationType.PercentageOfBasic)
            {
                configuredAmount = ConvertBonusExceptionAmountToRunCurrency(matchingException, configuredAmount, runCurrencyCode, parameters);
            }

            var amount = CalculateBonusAmount(calculationType, configuredAmount, basicSalary);
            amount = ApplyBonusProration(policy, profile, amount, payPeriodTo);
            if (amount <= 0)
            {
                continue;
            }

            var taxable = matchingException?.Taxable ?? policy.Taxable;
            var split = CalculateBonusTaxSplit(policy, amount, annualBasicSalary, runningPreviousBonus, parameters, taxable);
            var code = NormalizeCode(policy.Code);
            var name = string.IsNullOrWhiteSpace(policy.Name) ? code : policy.Name.Trim();
            var detailDescription = matchingRule == null ? name : BuildBonusRuleDescription(name, matchingRule);

            if (split.NonTaxableAmount > 0)
            {
                lines.Add(new PayrollBonusLine(code, $"{detailDescription} (non-taxable)", split.NonTaxableAmount, false, false));
            }

            if (split.SeparateTaxableAmount > 0)
            {
                lines.Add(new PayrollBonusLine(code, $"{detailDescription} (separate bonus tax)", split.SeparateTaxableAmount, true, true));
            }

            if (split.TaxableBonusAmount > 0)
            {
                var description = policy.SeparateTax || parameters?.SeparateBonusTax == true
                    ? $"{detailDescription} (tax table excess)"
                    : isException
                        ? $"{detailDescription} (employee exception)"
                        : detailDescription;
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

    private static bool BonusEmployeeMeetsMinimumMonths(PayrollBonusPolicy policy, PayrollEmployeeProfile profile, DateTime payPeriodTo)
    {
        var minimumMonths = policy.MinimumMonths.GetValueOrDefault();
        if (minimumMonths <= 0)
        {
            return true;
        }

        if (!profile.Employee.DateEmployed.HasValue)
        {
            return false;
        }

        var employedDate = profile.Employee.DateEmployed.Value.ToDateTime(TimeOnly.MinValue);
        return CompletedMonthsBetween(employedDate, payPeriodTo.Date) >= minimumMonths;
    }

    private static decimal ConvertBonusExceptionAmountToRunCurrency(
        PayrollBonusException exception,
        decimal amount,
        string runCurrencyCode,
        PayrollParameterSet? parameters)
    {
        if (amount == 0m)
        {
            return 0m;
        }

        var sourceCurrency = NormalizeOptionalCurrency(exception.CurrencyCode);
        if (sourceCurrency == null)
        {
            return Math.Round(amount, 2);
        }

        var targetCurrency = NormalizeCurrency(runCurrencyCode);
        if (sourceCurrency.Equals(targetCurrency, StringComparison.OrdinalIgnoreCase))
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

    private static PayrollBonusRule? FindMatchingBonusRule(
        PayrollBonusPolicy policy,
        IReadOnlyList<PayrollBonusRule>? rules,
        PayrollEmployeeProfile profile)
    {
        if (rules == null || rules.Count == 0)
        {
            return null;
        }

        var categoryType = NormalizeBonusCategoryType(policy.Category);
        var tokens = IsTypedBonusCategory(categoryType)
            ? EmployeeCategoryTokens(profile, categoryType).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : BuildBonusProfileTokens(profile);
        return rules.FirstOrDefault(e =>
            !IsAllBonusCategory(e.GroupCode) &&
            tokens.Contains(NormalizeMatchToken(e.GroupCode)))
            ?? rules.FirstOrDefault(e => IsAllBonusCategory(e.GroupCode));
    }

    private static string NormalizeBonusCategoryType(string? category)
    {
        var normalized = NormalizeMatchToken(category).Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase);
        return normalized switch
        {
            "" => "ALL",
            "ALL" or "ALLSTAFF" or "ANY" or "*" => "ALL",
            "POSITION" => "POS",
            "DEPARTMENT" => "DEP",
            "STAFFCATEGORY" => "CAT",
            "RANK" or "RANKS" => "RAN",
            _ => normalized
        };
    }

    private static bool IsTypedBonusCategory(string categoryType)
        => categoryType is "POS" or "DEP" or "CAT" or "JOB" or "RAN" or "GRA" or "SEC" or "LOC";

    private static string BuildBonusRuleDescription(string policyName, PayrollBonusRule rule)
    {
        var groupCode = NormalizeMatchToken(rule.GroupCode);
        return string.IsNullOrWhiteSpace(groupCode) || IsAllBonusCategory(groupCode)
            ? policyName
            : $"{policyName} ({rule.GroupCode.Trim()})";
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
               normalized is "ALL" or "ALLSTAFF" or "ALL STAFF" or "ANY" or "*";
    }

    private static PayrollPromotionArrearsCalculation CalculateSetupBackpayArrears(
        IReadOnlyList<PayrollBackpayPolicy> policies,
        IReadOnlyDictionary<string, IReadOnlyList<PayrollBackpayRule>> rulesByOperation,
        IReadOnlyDictionary<string, IReadOnlyList<PayrollBackpayException>> exceptionsByOperation,
        IReadOnlyList<PayrollRunEmployee> historicalRunEmployees,
        PayrollEmployeeProfile profile,
        IReadOnlyList<PayrollTaxBand> taxBands,
        PayrollPensionScheme? pensionScheme,
        PayrollParameterSet? parameters,
        DateTime runPeriodFrom)
    {
        if (policies.Count == 0 || historicalRunEmployees.Count == 0)
        {
            return PayrollPromotionArrearsCalculation.Empty;
        }

        var basicAmount = 0m;
        var allowanceAmount = 0m;
        var employeeContributionAmount = 0m;
        var employerContributionAmount = 0m;
        var otherContributionAmount = 0m;
        var otherEmployerContributionAmount = 0m;
        var deductionAmount = 0m;
        var taxableIncomeAmount = 0m;
        var taxAmount = 0m;
        var netAmount = 0m;

        foreach (var policy in policies)
        {
            if (!BackpayPolicyServiceApplies(policy, profile, runPeriodFrom))
            {
                continue;
            }

            var operation = NormalizeBackpayOperation(policy.OperationType);
            rulesByOperation.TryGetValue(operation, out var operationRules);
            exceptionsByOperation.TryGetValue(operation, out var operationExceptions);

            var matchingException = FindMatchingBackpayException(operationExceptions, profile);
            if (matchingException is { Applicable: false })
            {
                continue;
            }

            var matchingRule = FindMatchingBackpayRule(policy, operationRules, profile);
            if (matchingRule is { Applicable: false })
            {
                continue;
            }

            var hasRulesForPolicy = operationRules?.Any(e => BackpayRuleBelongsToPolicy(policy, e)) == true;
            if (matchingException == null && matchingRule == null && hasRulesForPolicy)
            {
                continue;
            }

            if (matchingException == null && matchingRule == null && !BackpayPolicyAppliesToProfile(policy, profile))
            {
                continue;
            }

            var calculationType = matchingException?.CalculationType ?? matchingRule?.CalculationType ?? policy.CalculationType;
            var configuredAmount = matchingException?.Amount ?? matchingRule?.Amount ?? policy.Amount;
            if (configuredAmount <= 0)
            {
                continue;
            }

            foreach (var previous in SelectBackpayHistory(policy, historicalRunEmployees, runPeriodFrom))
            {
                var periodBackpayAmount = CalculateBonusAmount(calculationType, configuredAmount, previous.BasicSalary);
                if (periodBackpayAmount <= 0)
                {
                    continue;
                }

                var increaseRate = ResolveBackpayIncreaseRate(calculationType, configuredAmount, previous.BasicSalary);
                var transactionDeltas = operation == "IncreaseSalary"
                    ? CalculateBackpayTransactionDeltas(previous.Transactions, increaseRate)
                    : PayrollBackpayTransactionDeltas.Empty;
                var contribution = policy.ApplySsf
                    ? CalculatePensionDifference(profile, previous.BasicSalary, previous.BasicSalary + periodBackpayAmount, pensionScheme, parameters)
                    : new PayrollPensionCalculation(0m, 0m);
                var taxableIncrement = policy.ApplyTax
                    ? Math.Max(0,
                        periodBackpayAmount +
                        transactionDeltas.TaxableAllowanceAmount +
                        transactionDeltas.TaxableBenefitAmount +
                        transactionDeltas.EmployerTaxableAmount -
                        contribution.EmployeeContribution -
                        transactionDeltas.NonTaxableContributionAmount -
                        transactionDeltas.NonTaxableDeductionAmount)
                    : 0m;
                var previousTax = CalculatePromotionComparableIncomeTax(previous);
                var adjustedTax = policy.ApplyTax && profile.PayTax
                    ? CalculateIncomeTax(taxBands, previous.TaxableIncome + taxableIncrement)
                    : previousTax;
                var periodTaxAmount = Math.Max(0, adjustedTax - previousTax);

                basicAmount += periodBackpayAmount;
                allowanceAmount += transactionDeltas.AllowanceAmount;
                employeeContributionAmount += contribution.EmployeeContribution;
                employerContributionAmount += contribution.EmployerContribution;
                otherContributionAmount += transactionDeltas.ContributionAmount;
                otherEmployerContributionAmount += transactionDeltas.EmployerContributionAmount;
                deductionAmount += transactionDeltas.DeductionAmount;
                taxableIncomeAmount += taxableIncrement;
                taxAmount += periodTaxAmount;
                netAmount += periodBackpayAmount +
                             transactionDeltas.AllowanceAmount -
                             contribution.EmployeeContribution -
                             transactionDeltas.ContributionAmount -
                             transactionDeltas.DeductionAmount -
                             periodTaxAmount;
            }
        }

        return netAmount <= 0
            ? PayrollPromotionArrearsCalculation.Empty
            : new PayrollPromotionArrearsCalculation(
                Math.Round(basicAmount, 2),
                Math.Round(allowanceAmount, 2),
                Math.Round(employeeContributionAmount, 2),
                Math.Round(employerContributionAmount, 2),
                Math.Round(otherContributionAmount, 2),
                Math.Round(otherEmployerContributionAmount, 2),
                Math.Round(deductionAmount, 2),
                Math.Round(taxableIncomeAmount, 2),
                Math.Round(taxAmount, 2),
                Math.Round(netAmount, 2));
    }

    private static decimal ResolveBackpayIncreaseRate(
        PayrollCalculationType calculationType,
        decimal configuredAmount,
        decimal previousBasicSalary)
    {
        if (previousBasicSalary <= 0m || configuredAmount <= 0m)
        {
            return 0m;
        }

        return calculationType == PayrollCalculationType.PercentageOfBasic
            ? configuredAmount
            : Math.Round(configuredAmount * 100m / previousBasicSalary, 6);
    }

    private static PayrollBackpayTransactionDeltas CalculateBackpayTransactionDeltas(
        IEnumerable<PayrollTransaction> transactions,
        decimal salaryIncreaseRate)
    {
        if (salaryIncreaseRate <= 0m)
        {
            return PayrollBackpayTransactionDeltas.Empty;
        }

        var allowanceAmount = 0m;
        var taxableAllowanceAmount = 0m;
        var taxableBenefitAmount = 0m;
        var contributionAmount = 0m;
        var employerContributionAmount = 0m;
        var employerTaxableAmount = 0m;
        var nonTaxableContributionAmount = 0m;
        var deductionAmount = 0m;
        var nonTaxableDeductionAmount = 0m;

        foreach (var transaction in transactions.Where(BackpayTransactionFollowsSalaryIncrease))
        {
            var amount = Math.Round(Math.Abs(transaction.Amount) * salaryIncreaseRate / 100m, 2);
            var employerAmount = Math.Round(Math.Abs(transaction.EmployerAmount ?? transaction.Amount) * salaryIncreaseRate / 100m, 2);
            if (amount <= 0m && employerAmount <= 0m)
            {
                continue;
            }

            if (transaction.TransactionType.Equals(PayrollComponentType.Allowance.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                allowanceAmount += amount;
                if (transaction.Taxable)
                {
                    taxableAllowanceAmount += amount;
                }

                continue;
            }

            if (transaction.TransactionType.Equals(PayrollComponentType.Benefit.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                if (transaction.Taxable)
                {
                    taxableBenefitAmount += amount;
                }

                continue;
            }

            if (transaction.TransactionType.Equals(PayrollComponentType.EmployeeContribution.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                contributionAmount += amount;
                employerContributionAmount += transaction.EmployerAmount.HasValue ? employerAmount : 0m;
                if (!transaction.Taxable)
                {
                    nonTaxableContributionAmount += amount;
                }

                if (transaction.EmployerTaxable)
                {
                    employerTaxableAmount += transaction.EmployerAmount.HasValue ? employerAmount : 0m;
                }

                continue;
            }

            if (transaction.TransactionType.Equals(PayrollComponentType.EmployerContribution.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                employerContributionAmount += employerAmount;
                if (transaction.EmployerTaxable)
                {
                    employerTaxableAmount += employerAmount;
                }

                continue;
            }

            if (transaction.TransactionType.Equals(PayrollComponentType.Deduction.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                deductionAmount += amount;
                if (!transaction.Taxable)
                {
                    nonTaxableDeductionAmount += amount;
                }
            }
        }

        return new PayrollBackpayTransactionDeltas(
            Math.Round(allowanceAmount, 2),
            Math.Round(taxableAllowanceAmount, 2),
            Math.Round(taxableBenefitAmount, 2),
            Math.Round(contributionAmount, 2),
            Math.Round(employerContributionAmount, 2),
            Math.Round(employerTaxableAmount, 2),
            Math.Round(nonTaxableContributionAmount, 2),
            Math.Round(deductionAmount, 2),
            Math.Round(nonTaxableDeductionAmount, 2));
    }

    private static bool BackpayTransactionFollowsSalaryIncrease(PayrollTransaction transaction)
    {
        if (transaction.PayrollComponent == null)
        {
            return true;
        }

        return transaction.PayrollComponent.CalculationType == PayrollCalculationType.PercentageOfBasic;
    }

    private static IReadOnlyList<PayrollRunEmployee> SelectBackpayHistory(
        PayrollBackpayPolicy policy,
        IReadOnlyList<PayrollRunEmployee> historicalRunEmployees,
        DateTime runPeriodFrom)
    {
        var startDate = ResolveBackpayHistoryStart(policy, runPeriodFrom);
        var months = Math.Max(0, policy.NumberOfMonths.GetValueOrDefault());
        var endDate = policy.EffectiveDate.HasValue && months > 0
            ? policy.EffectiveDate.Value.Date.AddMonths(months)
            : runPeriodFrom.Date;
        var selectedHistory = historicalRunEmployees
            .Where(e => e.PayrollRun.PayPeriodFrom.Date >= startDate &&
                        e.PayrollRun.PayPeriodFrom.Date < runPeriodFrom.Date &&
                        e.PayrollRun.PayPeriodFrom.Date < endDate)
            .OrderByDescending(e => e.PayrollRun.PayPeriod)
            .ToList();

        if (!policy.EffectiveDate.HasValue && months > 0)
        {
            selectedHistory = selectedHistory.Take(months).ToList();
        }

        return selectedHistory;
    }

    private static DateTime ResolveBackpayHistoryStart(PayrollBackpayPolicy policy, DateTime runPeriodFrom)
    {
        if (policy.EffectiveDate.HasValue)
        {
            return policy.EffectiveDate.Value.Date;
        }

        var months = Math.Max(1, policy.NumberOfMonths.GetValueOrDefault(1));
        return runPeriodFrom.Date.AddMonths(-months);
    }

    private static bool BackpayPolicyServiceApplies(PayrollBackpayPolicy policy, PayrollEmployeeProfile profile, DateTime runPeriodFrom)
    {
        if (!policy.MinimumServiceDate.HasValue && !policy.MinimumServiceValue.HasValue)
        {
            return true;
        }

        if (!profile.Employee.DateEmployed.HasValue)
        {
            return false;
        }

        var employedDate = profile.Employee.DateEmployed.Value.ToDateTime(TimeOnly.MinValue);
        if (policy.MinimumServiceDate.HasValue && employedDate.Date > policy.MinimumServiceDate.Value.Date)
        {
            return false;
        }

        var serviceValue = policy.MinimumServiceValue.GetValueOrDefault();
        if (serviceValue <= 0)
        {
            return true;
        }

        var completedMonths = CompletedMonthsBetween(employedDate, runPeriodFrom.Date);
        var serviceMode = NormalizeMatchToken(policy.MinimumServiceMode);
        var requiredMonths = serviceMode.Contains("YEAR", StringComparison.OrdinalIgnoreCase)
            ? serviceValue * 12m
            : serviceValue;
        return completedMonths >= requiredMonths;
    }

    private static bool BackpayPolicyAppliesToProfile(PayrollBackpayPolicy policy, PayrollEmployeeProfile profile)
    {
        if (IsAllBonusCategory(policy.CategoryType))
        {
            return true;
        }

        return NormalizeBackpayCategory(policy.CategoryType) == "All Staff" ||
               BuildBonusProfileTokens(profile).Contains(NormalizeMatchToken(policy.CategoryType));
    }

    private static PayrollBackpayRule? FindMatchingBackpayRule(
        PayrollBackpayPolicy policy,
        IReadOnlyList<PayrollBackpayRule>? rules,
        PayrollEmployeeProfile profile)
    {
        if (rules == null || rules.Count == 0)
        {
            return null;
        }

        var tokens = BuildBonusProfileTokens(profile);
        return rules.FirstOrDefault(e =>
        {
            if (!BackpayRuleBelongsToPolicy(policy, e))
            {
                return false;
            }

            var categoryCode = NormalizeMatchToken(e.CategoryCode);
            var categoryName = NormalizeMatchToken(e.CategoryName);
            return !IsAllBonusCategory(categoryCode) &&
                   (tokens.Contains(categoryCode) ||
                    (!string.IsNullOrWhiteSpace(categoryName) && tokens.Contains(categoryName)));
        })
            ?? rules.FirstOrDefault(e =>
                BackpayRuleBelongsToPolicy(policy, e) &&
                IsAllBonusCategory(e.CategoryCode));
    }

    private static bool BackpayRuleBelongsToPolicy(PayrollBackpayPolicy policy, PayrollBackpayRule rule)
        => string.Equals(NormalizeBackpayOperation(policy.OperationType), NormalizeBackpayOperation(rule.OperationType), StringComparison.OrdinalIgnoreCase) &&
           (NormalizeBackpayCategory(policy.CategoryType) == "All Staff" ||
            string.Equals(NormalizeBackpayCategory(policy.CategoryType), NormalizeBackpayCategory(rule.CategoryType), StringComparison.OrdinalIgnoreCase));

    private static PayrollBackpayException? FindMatchingBackpayException(
        IReadOnlyList<PayrollBackpayException>? exceptions,
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

    private static PayrollPensionCalculation CalculatePensionDifference(
        PayrollEmployeeProfile profile,
        decimal previousBasicSalary,
        decimal adjustedBasicSalary,
        PayrollPensionScheme? pensionScheme,
        PayrollParameterSet? parameters)
    {
        var previousContribution = CalculatePension(profile, previousBasicSalary, pensionScheme, parameters);
        var adjustedContribution = CalculatePension(profile, adjustedBasicSalary, pensionScheme, parameters);
        return new PayrollPensionCalculation(
            Math.Max(0, adjustedContribution.EmployeeContribution - previousContribution.EmployeeContribution),
            Math.Max(0, adjustedContribution.EmployerContribution - previousContribution.EmployerContribution));
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

        var normalWorkingHours = overtimePolicy?.NormalWorkingHours > 0 ? overtimePolicy.NormalWorkingHours : 8m;
        var monthDays = parameters?.MonthDays > 0 ? parameters.MonthDays : 30;
        var hourlyRate = normalWorkingHours > 0 && monthDays > 0
            ? originalBasicSalary / (normalWorkingHours * monthDays)
            : 0m;

        var amount = Math.Round(totalUnits * hourlyRate, 2);
        amount = ApplyOvertimeMaximum(amount, totalUnits, hourlyRate, originalBasicSalary, overtimePolicy);
        var taxable = overtimePolicy?.Taxable ?? false;
        return amount <= 0
            ? new PayrollOvertimeCalculation(0, taxable, false, 0, 0, 0)
            : new PayrollOvertimeCalculation(amount, taxable, false, taxable ? amount : 0m, 0, 0);
    }

    private static decimal ApplyOvertimeMaximum(
        decimal amount,
        decimal weightedHours,
        decimal hourlyRate,
        decimal originalBasicSalary,
        PayrollOvertimePolicy? overtimePolicy)
    {
        var maximum = overtimePolicy?.MaxOvertimeAmount ?? 0m;
        if (maximum <= 0m)
        {
            return amount;
        }

        var maximumType = (overtimePolicy?.MaxOvertimeType ?? string.Empty).Trim().ToUpperInvariant();
        if (maximumType == "H")
        {
            return Math.Round(Math.Min(weightedHours, maximum) * hourlyRate, 2);
        }

        var maximumAmount = overtimePolicy?.MaxOvertimeIsPercent == true
            ? Math.Round(originalBasicSalary * maximum / 100m, 2)
            : maximum;

        return maximumAmount > 0m
            ? Math.Round(Math.Min(amount, maximumAmount), 2)
            : amount;
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
        decimal currentIncomeTax,
        PayrollParameterSet? parameters)
    {
        var eligibleHistory = historicalRunEmployees
            .Where(e => e.PayrollRun.PayPeriod < promotionEntry.PayPeriod &&
                        e.PayrollRun.PayPeriodTo.Date >= promotionEntry.EffectiveDate.Date)
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
            var prorationFactor = CalculatePromotionArrearsProrationFactor(promotionEntry, previous.PayrollRun, parameters);
            var previousAllowance = previous.TaxableAllowances + previous.NonTaxableAllowances;
            var previousContributionSplit = BuildContributionArrearsSplit(previous.Transactions);
            var diffBasic = (currentBasicSalary - previous.BasicSalary) * prorationFactor;
            var diffAllowance = (currentAllowanceAmount - previousAllowance) * prorationFactor;
            var diffEmployeeSsf = (currentContributionSplit.EmployeeSsfAmount - previousContributionSplit.EmployeeSsfAmount) * prorationFactor;
            var diffEmployerSsf = (currentContributionSplit.EmployerSsfAmount - previousContributionSplit.EmployerSsfAmount) * prorationFactor;
            var diffOtherContribution = (currentContributionSplit.OtherEmployeeContributionAmount - previousContributionSplit.OtherEmployeeContributionAmount) * prorationFactor;
            var diffOtherEmployerContribution = (currentContributionSplit.OtherEmployerContributionAmount - previousContributionSplit.OtherEmployerContributionAmount) * prorationFactor;
            var diffTaxableIncome = (currentTaxableIncome - previous.TaxableIncome) * prorationFactor;
            var diffTax = (currentIncomeTax - CalculatePromotionComparableIncomeTax(previous)) * prorationFactor;
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
            0m,
            Math.Round(taxableIncomeAmount, 2),
            Math.Round(taxAmount, 2),
            Math.Round(netAmount, 2));
    }

    private static decimal CalculatePromotionArrearsProrationFactor(
        PayrollPromotionArrearsEntry promotionEntry,
        PayrollRun previousRun,
        PayrollParameterSet? parameters)
    {
        var effectiveDate = promotionEntry.EffectiveDate.Date;
        if (effectiveDate <= previousRun.PayPeriodFrom.Date || effectiveDate > previousRun.PayPeriodTo.Date)
        {
            return 1m;
        }

        var monthDays = parameters?.MonthDays > 0 ? parameters.MonthDays : 30;
        if (monthDays <= 0)
        {
            return 1m;
        }

        var workingDays = promotionEntry.WorkingDays.GetValueOrDefault();
        if (workingDays <= 0m)
        {
            workingDays = Math.Max(0, (previousRun.PayPeriodTo.Date - effectiveDate).Days + 1);
        }

        return Math.Clamp(workingDays / monthDays, 0m, 1m);
    }

    private static void ApplyPromotionArrearsTransactions(
        Guid tenantId,
        PayrollRun run,
        PayrollRunEmployee runEmployee,
        PayrollEmployeeProfile profile,
        ICollection<PayrollTransaction> transactions,
        PayrollPromotionArrearsCalculation arrears,
        string descriptionPrefix = "Promotion")
    {
        var prefix = string.IsNullOrWhiteSpace(descriptionPrefix) ? "Promotion" : descriptionPrefix.Trim();
        if (arrears.BasicAmount > 0)
        {
            transactions.Add(CreateTransaction(tenantId, run, runEmployee, profile, PromotionBasicArrearsTransactionType, $"{prefix} basic arrears", arrears.BasicAmount, taxable: true, componentCode: PromotionBasicArrearsTransactionType));
        }

        if (arrears.AllowanceAmount > 0)
        {
            transactions.Add(CreateTransaction(tenantId, run, runEmployee, profile, PromotionAllowanceArrearsTransactionType, $"{prefix} allowance arrears", arrears.AllowanceAmount, taxable: true, componentCode: PromotionAllowanceArrearsTransactionType));
        }

        if (arrears.EmployeeContributionAmount > 0)
        {
            transactions.Add(CreateTransaction(tenantId, run, runEmployee, profile, PromotionEmployeeContributionArrearsTransactionType, $"{prefix} employee contribution arrears", arrears.EmployeeContributionAmount, taxable: false, componentCode: PromotionEmployeeContributionArrearsTransactionType));
        }

        if (arrears.OtherContributionAmount > 0)
        {
            var transaction = CreateTransaction(tenantId, run, runEmployee, profile, PromotionContributionArrearsTransactionType, $"{prefix} contribution arrears", arrears.OtherContributionAmount, taxable: true, componentCode: PromotionContributionArrearsTransactionType);
            transaction.EmployerAmount = arrears.OtherEmployerContributionAmount;
            transactions.Add(transaction);
        }

        if (arrears.DeductionAmount > 0)
        {
            transactions.Add(CreateTransaction(tenantId, run, runEmployee, profile, BackpayDeductionArrearsTransactionType, $"{prefix} deduction arrears", arrears.DeductionAmount, taxable: false, componentCode: BackpayDeductionArrearsTransactionType));
        }

        if (arrears.EmployerContributionAmount > 0)
        {
            transactions.Add(CreateTransaction(tenantId, run, runEmployee, profile, PromotionEmployerContributionArrearsTransactionType, $"{prefix} employer contribution arrears", arrears.EmployerContributionAmount, taxable: false, employerAmount: arrears.EmployerContributionAmount, componentCode: PromotionEmployerContributionArrearsTransactionType));
        }

        if (arrears.TaxAmount > 0)
        {
            transactions.Add(CreateTransaction(tenantId, run, runEmployee, profile, IncomeTaxTransactionType, $"{prefix} arrears tax", arrears.TaxAmount, taxable: false, componentCode: "ARR"));
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
            if (profile.PaymentMethods.Count == 0)
            {
                var defaultMethod = new PayrollPaymentMethod
                {
                    TenantId = tenantId,
                    EmployeeProfile = profile,
                    PaymentType = "Bank",
                    PaymentMode = PaymentModePercentage,
                    PaymentPercent = 100m,
                    Amount = null,
                    CurrencyCode = NormalizeCurrency(profile.CurrencyCode),
                    ExchangeRate = 1m,
                    SequenceNo = 1,
                    IsActive = true
                };
                profile.PaymentMethods.Add(defaultMethod);
                profile.DefaultPaymentMethod = defaultMethod;
            }

            return;
        }

        var keepIds = dto.Where(e => e.Id != Guid.Empty).Select(e => e.Id).ToHashSet();
        foreach (var existing in profile.PaymentMethods.Where(e => !keepIds.Contains(e.Id)).ToList())
        {
            _context.PayrollPaymentMethods.Remove(existing);
        }

        var normalizedRows = dto
            .Select((item, index) => new
            {
                Item = item,
                PaymentMode = NormalizePaymentMode(item.PaymentMode, item.Amount, item.PaymentPercent),
                SequenceNo = item.SequenceNo > 0 ? item.SequenceNo : index + 1
            })
            .ToList();

        foreach (var row in normalizedRows.Where(e => e.Item.IsActive))
        {
            if (row.PaymentMode == PaymentModeFixedAmount && (!row.Item.Amount.HasValue || row.Item.Amount.Value <= 0m))
            {
                throw new InvalidOperationException("Fixed amount payment rows require an amount greater than zero.");
            }

            if (row.PaymentMode == PaymentModePercentage && (!row.Item.PaymentPercent.HasValue || row.Item.PaymentPercent.Value <= 0m))
            {
                throw new InvalidOperationException("Percentage payment rows require a percentage greater than zero.");
            }
        }

        var activePercentageTotal = normalizedRows
            .Where(e => e.Item.IsActive && e.PaymentMode == PaymentModePercentage)
            .Sum(e => e.Item.PaymentPercent ?? 0m);

        if (Math.Round(activePercentageTotal, 4) != 100m)
        {
            throw new InvalidOperationException("Active percentage payment rows must total 100% after fixed amount rows.");
        }

        foreach (var normalized in normalizedRows)
        {
            var item = normalized.Item;
            var method = profile.PaymentMethods.FirstOrDefault(e => e.Id == item.Id)
                ?? new PayrollPaymentMethod { TenantId = tenantId, EmployeeProfile = profile };
            method.PaymentType = string.IsNullOrWhiteSpace(item.PaymentType) ? "Bank" : item.PaymentType.Trim();
            method.PaymentMode = normalized.PaymentMode;
            method.PaymentPercent = normalized.PaymentMode == PaymentModePercentage ? item.PaymentPercent : null;
            method.Amount = normalized.PaymentMode == PaymentModeFixedAmount ? item.Amount : null;
            method.BankCode = TrimOrNull(item.BankCode);
            method.BankBranchCode = TrimOrNull(item.BankBranchCode);
            method.AccountNumber = TrimOrNull(item.AccountNumber);
            method.ChequeNumber = TrimOrNull(item.ChequeNumber);
            method.ChequeBankCode = TrimOrNull(item.ChequeBankCode);
            method.CurrencyCode = NormalizeCurrency(item.CurrencyCode);
            method.ExchangeRate = item.ExchangeRate.HasValue && item.ExchangeRate.Value > 0 ? item.ExchangeRate.Value : 1m;
            method.SequenceNo = normalized.SequenceNo;
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

    private static string NormalizePaymentMode(string? paymentMode, decimal? amount = null, decimal? paymentPercent = null)
    {
        var normalized = paymentMode?.Trim().Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (string.Equals(normalized, PaymentModeFixedAmount, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Amount", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "A", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentModeFixedAmount;
        }

        if (string.Equals(normalized, PaymentModePercentage, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Percent", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "P", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentModePercentage;
        }

        return amount.HasValue && amount.Value > 0m && (!paymentPercent.HasValue || paymentPercent.Value <= 0m)
            ? PaymentModeFixedAmount
            : PaymentModePercentage;
    }

    private static string NormalizeDebitCredit(string? debitCredit)
    {
        var value = debitCredit?.Trim().ToUpperInvariant();
        return value is "CR" or "C" ? "CR" : "DR";
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
            "W" or "WD" or "WITHDRAW" or "WITHDRAWAL" => "Withdrawal",
            "I" or "INT" or "INTEREST" => "Interest",
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
    {
        if (transactionType.Equals(IncomeTaxTransactionType, StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(EmployeePensionTransactionType, StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(EmployerPensionTransactionType, StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(PayrollComponentType.Deduction.ToString(), StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(PayrollComponentType.EmployeeContribution.ToString(), StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(LoanRepaymentTransactionType, StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(LoanInterestTransactionType, StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(SalaryAdvanceTransactionType, StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(AbsenceTransactionType, StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(NetPayTransactionType, StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(PromotionEmployeeContributionArrearsTransactionType, StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(PromotionEmployerContributionArrearsTransactionType, StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(PromotionContributionArrearsTransactionType, StringComparison.OrdinalIgnoreCase) ||
            transactionType.Equals(BackpayDeductionArrearsTransactionType, StringComparison.OrdinalIgnoreCase))
        {
            return "CR";
        }

        return "DR";
    }

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
            SequenceNo = entity.SequenceNo,
            TransactionType = entity.TransactionType,
            ComponentCode = entity.ComponentCode,
            ShortDescription = entity.ShortDescription,
            Description = entity.Description,
            DebitCredit = entity.DebitCredit,
            AccountCode = entity.AccountCode,
            AccountType = entity.AccountType,
            LegacyCompanyCode = entity.LegacyCompanyCode,
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
            PaymentMode = NormalizePaymentMode(entity.PaymentMode, entity.Amount, entity.PaymentPercent),
            PaymentPercent = entity.PaymentPercent,
            Amount = entity.Amount,
            BankCode = entity.BankCode,
            BankBranchCode = entity.BankBranchCode,
            AccountNumber = entity.AccountNumber,
            ChequeNumber = entity.ChequeNumber,
            ChequeBankCode = entity.ChequeBankCode,
            CurrencyCode = entity.CurrencyCode,
            ExchangeRate = entity.ExchangeRate,
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
    private sealed class PayrollBudgetAnalysisAccumulator
    {
        public int OrderField { get; set; }
        public int PayPeriod { get; set; }
        public DateTime PayPeriodFrom { get; set; }
        public DateTime PayPeriodTo { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public string? ActualTransaction { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool Percentage { get; set; }
        public decimal BaseAmount { get; set; }
        public string CompanyCode { get; set; } = "001";
    }
    private sealed record OracleBonusEmployeeRow(
        PayrollRunEmployee Employee,
        decimal Bonus,
        decimal TaxableBonus,
        decimal NonTaxableBonus,
        decimal BonusTax,
        decimal NetBonus);
    private sealed record OracleEmployeePaymentBase(PayrollRunEmployee Employee, decimal Amount);
    private sealed record OracleBankPaymentRow(
        string BankName,
        string? BankCode,
        string? BranchCode,
        string? BranchName,
        string? AccountNumber,
        string CurrencyCode,
        decimal Amount,
        PayrollRunEmployee Employee);
    private sealed record PayrollPaymentAllocation(
        PayrollPaymentMethod Method,
        decimal Amount,
        decimal ExchangeRate,
        decimal BaseAmount);

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

    private sealed record PayrollBackpayTransactionDeltas(
        decimal AllowanceAmount,
        decimal TaxableAllowanceAmount,
        decimal TaxableBenefitAmount,
        decimal ContributionAmount,
        decimal EmployerContributionAmount,
        decimal EmployerTaxableAmount,
        decimal NonTaxableContributionAmount,
        decimal DeductionAmount,
        decimal NonTaxableDeductionAmount)
    {
        public static PayrollBackpayTransactionDeltas Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    private sealed record PayrollPromotionArrearsCalculation(
        decimal BasicAmount,
        decimal AllowanceAmount,
        decimal EmployeeContributionAmount,
        decimal EmployerContributionAmount,
        decimal OtherContributionAmount,
        decimal OtherEmployerContributionAmount,
        decimal DeductionAmount,
        decimal TaxableIncomeAmount,
        decimal TaxAmount,
        decimal NetAmount)
    {
        public static PayrollPromotionArrearsCalculation Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }
}
