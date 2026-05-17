using ErpSystem.Core.DTOs.HR.Payroll;
using ErpSystem.Core.Entities.HR.Payroll;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/hr/payroll")]
[Authorize]
public class PayrollController : ControllerBase
{
    private readonly IPayrollService _payrollService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<PayrollController> _logger;

    public PayrollController(
        IPayrollService payrollService,
        ICurrentUserService currentUserService,
        ILogger<PayrollController> logger)
    {
        _payrollService = payrollService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [HttpGet("legacy-menu")]
    public async Task<ActionResult<IReadOnlyList<PayrollLegacyMenuItemDto>>> GetLegacyMenu(CancellationToken cancellationToken)
        => Ok(await _payrollService.GetLegacyMenuAsync(cancellationToken));

    [HttpGet("setup-summary")]
    public async Task<ActionResult<PayrollSetupSummaryDto>> GetSetupSummary(CancellationToken cancellationToken)
        => Ok(await _payrollService.GetSetupSummaryAsync(GetTenantId(), cancellationToken));

    [HttpPost("setup/budget-analysis")]
    public Task<ActionResult<PayrollBudgetAnalysisDto>> BuildBudgetAnalysis([FromBody] PayrollBudgetAnalysisRequestDto? dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.BuildBudgetAnalysisAsync(GetTenantId(), dto ?? new PayrollBudgetAnalysisRequestDto(), cancellationToken));

    [HttpPost("setup/budget-analysis/save")]
    public Task<ActionResult<PayrollBudgetAnalysisDto>> SaveBudgetAnalysis([FromBody] PayrollBudgetAnalysisRequestDto? dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.SaveBudgetAnalysisAsync(GetTenantId(), dto ?? new PayrollBudgetAnalysisRequestDto(), cancellationToken));

    [HttpGet("setup/codes")]
    public async Task<ActionResult<PayrollCodeSetupDto>> GetCodes([FromQuery] string? codeType, CancellationToken cancellationToken)
        => Ok(await _payrollService.GetCodeSetupAsync(GetTenantId(), codeType, cancellationToken));

    [HttpPost("setup/code-types")]
    public Task<ActionResult<PayrollCodeTypeDto>> UpsertCodeType([FromBody] UpsertPayrollCodeTypeDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertCodeTypeAsync(GetTenantId(), dto, cancellationToken));

    [HttpPost("setup/code-values")]
    public Task<ActionResult<PayrollCodeValueDto>> UpsertCodeValue([FromBody] UpsertPayrollCodeValueDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertCodeValueAsync(GetTenantId(), dto, cancellationToken));

    [HttpGet("setup/holidays")]
    public async Task<ActionResult<PayrollHolidaySetupDto>> GetHolidays(CancellationToken cancellationToken)
        => Ok(await _payrollService.GetHolidaySetupAsync(GetTenantId(), cancellationToken));

    [HttpPost("setup/holidays")]
    public Task<ActionResult<PayrollHolidayDto>> UpsertHoliday([FromBody] UpsertPayrollHolidayDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertHolidayAsync(GetTenantId(), dto, cancellationToken));

    [HttpPost("setup/non-working-days")]
    public Task<ActionResult<PayrollNonWorkingDayDto>> UpsertNonWorkingDay([FromBody] UpsertPayrollNonWorkingDayDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertNonWorkingDayAsync(GetTenantId(), dto, cancellationToken));

    [HttpGet("setup/exchange-rates")]
    public async Task<ActionResult<IReadOnlyList<PayrollExchangeRateDto>>> GetExchangeRates(CancellationToken cancellationToken)
        => Ok(await _payrollService.GetExchangeRatesAsync(GetTenantId(), cancellationToken));

    [HttpPost("setup/exchange-rates")]
    public Task<ActionResult<PayrollExchangeRateDto>> UpsertExchangeRate([FromBody] UpsertPayrollExchangeRateDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertExchangeRateAsync(GetTenantId(), dto, cancellationToken));

    [HttpGet("setup/bank-branches")]
    public async Task<ActionResult<IReadOnlyList<PayrollBankBranchDto>>> GetBankBranches(CancellationToken cancellationToken)
        => Ok(await _payrollService.GetBankBranchesAsync(GetTenantId(), cancellationToken));

    [HttpPost("setup/bank-branches")]
    public Task<ActionResult<PayrollBankBranchDto>> UpsertBankBranch([FromBody] UpsertPayrollBankBranchDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertBankBranchAsync(GetTenantId(), dto, cancellationToken));

    [HttpGet("setup/leave")]
    public async Task<ActionResult<PayrollLeaveSetupCollectionDto>> GetLeaveSetup(CancellationToken cancellationToken)
        => Ok(await _payrollService.GetLeaveSetupAsync(GetTenantId(), cancellationToken));

    [HttpPost("setup/leave")]
    public Task<ActionResult<PayrollLeaveSetupDto>> UpsertLeaveSetup([FromBody] UpsertPayrollLeaveSetupDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertLeaveSetupAsync(GetTenantId(), dto, cancellationToken));

    [HttpPost("setup/leave-details")]
    public Task<ActionResult<PayrollLeaveSetupDetailDto>> UpsertLeaveSetupDetail([FromBody] UpsertPayrollLeaveSetupDetailDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertLeaveSetupDetailAsync(GetTenantId(), dto, cancellationToken));

    [HttpGet("setup/overtime")]
    public async Task<ActionResult<PayrollOvertimeSetupDto>> GetOvertimeSetup(CancellationToken cancellationToken)
        => Ok(await _payrollService.GetOvertimeSetupAsync(GetTenantId(), cancellationToken));

    [HttpPost("setup/overtime-ranges")]
    public Task<ActionResult<PayrollOvertimeRangeDto>> UpsertOvertimeRange([FromBody] UpsertPayrollOvertimeRangeDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertOvertimeRangeAsync(GetTenantId(), dto, cancellationToken));

    [HttpGet("setup/menu-users")]
    public async Task<ActionResult<IReadOnlyList<PayrollLegacyMenuUserDto>>> GetLegacyMenuUsers(CancellationToken cancellationToken)
        => Ok(await _payrollService.GetLegacyMenuUsersAsync(GetTenantId(), cancellationToken));

    [HttpPost("setup/menu-users")]
    public Task<ActionResult<PayrollLegacyMenuUserDto>> UpsertLegacyMenuUser([FromBody] UpsertPayrollLegacyMenuUserDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertLegacyMenuUserAsync(GetTenantId(), dto, cancellationToken));

    [HttpGet("setup/menu-security")]
    public async Task<ActionResult<IReadOnlyList<PayrollLegacyMenuSecurityDto>>> GetMenuSecurity(CancellationToken cancellationToken)
        => Ok(await _payrollService.GetMenuSecurityAsync(GetTenantId(), cancellationToken));

    [HttpPost("setup/menu-security")]
    public Task<ActionResult<PayrollLegacyMenuSecurityDto>> UpsertMenuSecurity([FromBody] UpsertPayrollLegacyMenuSecurityDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertMenuSecurityAsync(GetTenantId(), dto, cancellationToken));

    [HttpPost("setup/menu-users/change-password")]
    public Task<ActionResult<PayrollLegacyMenuUserDto>> ChangeLegacyMenuUserPassword([FromBody] PayrollLegacyPasswordChangeDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.ChangeLegacyMenuUserPasswordAsync(GetTenantId(), dto, cancellationToken));

    [HttpGet("setup/company")]
    public async Task<ActionResult<PayrollCompanySetupDto>> GetCompanySetup(CancellationToken cancellationToken)
        => Ok(await _payrollService.GetCompanySetupAsync(GetTenantId(), cancellationToken));

    [HttpPost("setup/company-profiles")]
    public Task<ActionResult<PayrollCompanyProfileDto>> UpsertCompanyProfile([FromBody] UpsertPayrollCompanyProfileDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertCompanyProfileAsync(GetTenantId(), dto, cancellationToken));

    [HttpPost("setup/business-units")]
    public Task<ActionResult<PayrollBusinessUnitDto>> UpsertBusinessUnit([FromBody] UpsertPayrollBusinessUnitDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertBusinessUnitAsync(GetTenantId(), dto, cancellationToken));

    [HttpPost("setup/company-bankers")]
    public Task<ActionResult<PayrollCompanyBankerDto>> UpsertCompanyBanker([FromBody] UpsertPayrollCompanyBankerDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertCompanyBankerAsync(GetTenantId(), dto, cancellationToken));

    [HttpGet("setup/grades")]
    public async Task<ActionResult<PayrollGradeSetupDto>> GetGradeSetup(CancellationToken cancellationToken)
        => Ok(await _payrollService.GetGradeSetupAsync(GetTenantId(), cancellationToken));

    [HttpPost("setup/grades")]
    public Task<ActionResult<PayrollGradeDto>> UpsertGrade([FromBody] UpsertPayrollGradeDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertGradeAsync(GetTenantId(), dto, cancellationToken));

    [HttpPost("setup/grade-notches")]
    public Task<ActionResult<PayrollGradeNotchDto>> UpsertGradeNotch([FromBody] UpsertPayrollGradeNotchDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertGradeNotchAsync(GetTenantId(), dto, cancellationToken));

    [HttpGet("setup/tax-table")]
    public async Task<ActionResult<PayrollTaxTableDto>> GetTaxTable(CancellationToken cancellationToken)
        => Ok(await _payrollService.GetTaxTableAsync(GetTenantId(), cancellationToken));

    [HttpPost("setup/parameters")]
    public Task<ActionResult<PayrollParameterSetDto>> UpsertParameters([FromBody] UpsertPayrollParameterSetDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertParameterSetAsync(GetTenantId(), dto, cancellationToken));

    [HttpPost("setup/components")]
    public Task<ActionResult<PayrollComponentDto>> UpsertComponent([FromBody] UpsertPayrollComponentDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertComponentAsync(GetTenantId(), dto, cancellationToken));

    [HttpPost("setup/component-rules")]
    public Task<ActionResult<PayrollComponentRuleDto>> UpsertComponentRule([FromBody] UpsertPayrollComponentRuleDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertComponentRuleAsync(GetTenantId(), dto, cancellationToken));

    [HttpPost("setup/tax-bands")]
    public Task<ActionResult<PayrollTaxBandDto>> UpsertTaxBand([FromBody] UpsertPayrollTaxBandDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertTaxBandAsync(GetTenantId(), dto, cancellationToken));

    [HttpPost("setup/tax-reliefs")]
    public Task<ActionResult<PayrollTaxReliefDto>> UpsertTaxRelief([FromBody] UpsertPayrollTaxReliefDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertTaxReliefAsync(GetTenantId(), dto, cancellationToken));

    [HttpPost("setup/pension-schemes")]
    public Task<ActionResult<PayrollPensionSchemeDto>> UpsertPensionScheme([FromBody] UpsertPayrollPensionSchemeDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertPensionSchemeAsync(GetTenantId(), dto, cancellationToken));

    [HttpPost("setup/overtime-policies")]
    public Task<ActionResult<PayrollOvertimePolicyDto>> UpsertOvertimePolicy([FromBody] UpsertPayrollOvertimePolicyDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertOvertimePolicyAsync(GetTenantId(), dto, cancellationToken));

    [HttpPost("setup/loan-policies")]
    public Task<ActionResult<PayrollLoanPolicyDto>> UpsertLoanPolicy([FromBody] UpsertPayrollLoanPolicyDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertLoanPolicyAsync(GetTenantId(), dto, cancellationToken));

    [HttpPost("setup/bonus-policies")]
    public Task<ActionResult<PayrollBonusPolicyDto>> UpsertBonusPolicy([FromBody] UpsertPayrollBonusPolicyDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertBonusPolicyAsync(GetTenantId(), dto, cancellationToken));

    [HttpPost("setup/bonus-rules")]
    public Task<ActionResult<PayrollBonusRuleDto>> UpsertBonusRule([FromBody] UpsertPayrollBonusRuleDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertBonusRuleAsync(GetTenantId(), dto, cancellationToken));

    [HttpPost("setup/bonus-rules/bulk")]
    public Task<ActionResult<IReadOnlyList<PayrollBonusRuleDto>>> SaveBonusRules(
        [FromBody] PayrollBonusRuleBulkSaveDto dto,
        CancellationToken cancellationToken)
        => Handle(() => _payrollService.SaveBonusRulesAsync(GetTenantId(), dto, cancellationToken));

    [HttpGet("setup/bonus-exceptions")]
    public async Task<ActionResult<IReadOnlyList<PayrollBonusExceptionDto>>> GetBonusExceptions(
        [FromQuery] string? bonusCode,
        CancellationToken cancellationToken)
        => Ok(await _payrollService.GetBonusExceptionsAsync(GetTenantId(), bonusCode, cancellationToken));

    [HttpPost("setup/bonus-exceptions/bulk")]
    public Task<ActionResult<IReadOnlyList<PayrollBonusExceptionDto>>> SaveBonusExceptions(
        [FromBody] PayrollBonusExceptionBulkSaveDto dto,
        CancellationToken cancellationToken)
        => Handle(() => _payrollService.SaveBonusExceptionsAsync(GetTenantId(), dto, cancellationToken));

    [HttpPost("setup/backpay-policies")]
    public Task<ActionResult<PayrollBackpayPolicyDto>> UpsertBackpayPolicy([FromBody] UpsertPayrollBackpayPolicyDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertBackpayPolicyAsync(GetTenantId(), dto, cancellationToken));

    [HttpPost("setup/backpay-rules")]
    public Task<ActionResult<PayrollBackpayRuleDto>> UpsertBackpayRule([FromBody] UpsertPayrollBackpayRuleDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertBackpayRuleAsync(GetTenantId(), dto, cancellationToken));

    [HttpPost("setup/backpay-exceptions")]
    public Task<ActionResult<PayrollBackpayExceptionDto>> UpsertBackpayException([FromBody] UpsertPayrollBackpayExceptionDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertBackpayExceptionAsync(GetTenantId(), dto, cancellationToken));

    [HttpPost("setup/journal-mappings")]
    public Task<ActionResult<PayrollJournalMappingDto>> UpsertJournalMapping([FromBody] UpsertPayrollJournalMappingDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertJournalMappingAsync(GetTenantId(), dto, cancellationToken));

    [HttpGet("employee-profiles")]
    public async Task<ActionResult<IReadOnlyList<PayrollEmployeeProfileDto>>> GetEmployeeProfiles([FromQuery] string? searchTerm, CancellationToken cancellationToken)
        => Ok(await _payrollService.GetEmployeeProfilesAsync(GetTenantId(), searchTerm, cancellationToken));

    [HttpPost("employee-profiles")]
    public Task<ActionResult<PayrollEmployeeProfileDto>> UpsertEmployeeProfile([FromBody] UpsertPayrollEmployeeProfileDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertEmployeeProfileAsync(GetTenantId(), dto, cancellationToken));

    [HttpGet("loans")]
    public async Task<ActionResult<IReadOnlyList<PayrollLoanDto>>> GetLoans(
        [FromQuery] string? employeeNumber,
        [FromQuery] bool includeInactive,
        CancellationToken cancellationToken)
        => Ok(await _payrollService.GetLoansAsync(GetTenantId(), employeeNumber, includeInactive, cancellationToken));

    [HttpPost("loans")]
    public Task<ActionResult<PayrollLoanDto>> UpsertLoan([FromBody] UpsertPayrollLoanDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertLoanAsync(GetTenantId(), dto, cancellationToken));

    [HttpPost("loans/repayments")]
    public Task<ActionResult<PayrollLoanRepaymentDto>> PostLoanRepayment([FromBody] PayrollLoanRepaymentRequestDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.PostLoanRepaymentAsync(GetTenantId(), dto, cancellationToken));

    [HttpGet("salary-advances")]
    public async Task<ActionResult<IReadOnlyList<PayrollSalaryAdvanceDto>>> GetSalaryAdvances(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? employeeNumber,
        CancellationToken cancellationToken)
        => Ok(await _payrollService.GetSalaryAdvancesAsync(GetTenantId(), fromDate, toDate, employeeNumber, cancellationToken));

    [HttpPost("salary-advances")]
    public Task<ActionResult<PayrollSalaryAdvanceDto>> UpsertSalaryAdvance([FromBody] UpsertPayrollSalaryAdvanceDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertSalaryAdvanceAsync(GetTenantId(), dto, cancellationToken));

    [HttpGet("tax-reliefs")]
    public async Task<ActionResult<IReadOnlyList<PayrollEmployeeTaxReliefDto>>> GetEmployeeTaxReliefs(
        [FromQuery] string? reliefCode,
        CancellationToken cancellationToken)
        => Ok(await _payrollService.GetEmployeeTaxReliefsAsync(GetTenantId(), reliefCode, cancellationToken));

    [HttpPost("tax-reliefs/bulk")]
    public Task<ActionResult<IReadOnlyList<PayrollEmployeeTaxReliefDto>>> SaveEmployeeTaxReliefs(
        [FromBody] PayrollEmployeeTaxReliefBulkSaveDto dto,
        CancellationToken cancellationToken)
        => Handle(() => _payrollService.SaveEmployeeTaxReliefsAsync(GetTenantId(), dto, cancellationToken));

    [HttpGet("component-exceptions")]
    public async Task<ActionResult<IReadOnlyList<PayrollEmployeeComponentExceptionDto>>> GetEmployeeComponentExceptions(
        [FromQuery] Guid? payrollComponentId,
        [FromQuery] string? componentCode,
        [FromQuery] PayrollComponentType? componentType,
        CancellationToken cancellationToken)
        => Ok(await _payrollService.GetEmployeeComponentExceptionsAsync(GetTenantId(), payrollComponentId, componentCode, componentType, cancellationToken));

    [HttpPost("component-exceptions/bulk")]
    public Task<ActionResult<IReadOnlyList<PayrollEmployeeComponentExceptionDto>>> SaveEmployeeComponentExceptions(
        [FromBody] PayrollEmployeeComponentExceptionBulkSaveDto dto,
        CancellationToken cancellationToken)
        => Handle(() => _payrollService.SaveEmployeeComponentExceptionsAsync(GetTenantId(), dto, cancellationToken));

    [HttpGet("promotion-arrears")]
    public async Task<ActionResult<IReadOnlyList<PayrollPromotionArrearsEntryDto>>> GetPromotionArrears(
        [FromQuery] string? employeeNumber,
        [FromQuery] bool includeInactive,
        CancellationToken cancellationToken)
        => Ok(await _payrollService.GetPromotionArrearsAsync(GetTenantId(), employeeNumber, includeInactive, cancellationToken));

    [HttpPost("promotion-arrears/bulk")]
    public Task<ActionResult<IReadOnlyList<PayrollPromotionArrearsEntryDto>>> SavePromotionArrears(
        [FromBody] PayrollPromotionArrearsBulkSaveDto dto,
        CancellationToken cancellationToken)
        => Handle(() => _payrollService.SavePromotionArrearsAsync(GetTenantId(), dto, cancellationToken));

    [HttpGet("overtime-summaries")]
    public async Task<ActionResult<IReadOnlyList<PayrollOvertimeSummaryEntryDto>>> GetOvertimeSummaries(
        [FromQuery] string? employeeNumber,
        [FromQuery] bool includeInactive,
        CancellationToken cancellationToken)
        => Ok(await _payrollService.GetOvertimeSummariesAsync(GetTenantId(), employeeNumber, includeInactive, cancellationToken));

    [HttpPost("overtime-summaries/bulk")]
    public Task<ActionResult<IReadOnlyList<PayrollOvertimeSummaryEntryDto>>> SaveOvertimeSummaries(
        [FromBody] PayrollOvertimeSummaryBulkSaveDto dto,
        CancellationToken cancellationToken)
        => Handle(() => _payrollService.SaveOvertimeSummariesAsync(GetTenantId(), dto, cancellationToken));

    [HttpGet("contribution-opening-balances")]
    public async Task<ActionResult<IReadOnlyList<PayrollContributionOpeningBalanceDto>>> GetContributionOpeningBalances(
        [FromQuery] string? contributionCode,
        [FromQuery] string? contributionCodeType,
        [FromQuery] bool includeInactive,
        CancellationToken cancellationToken)
        => Ok(await _payrollService.GetContributionOpeningBalancesAsync(GetTenantId(), contributionCode, contributionCodeType, includeInactive, cancellationToken));

    [HttpPost("contribution-opening-balances/bulk")]
    public Task<ActionResult<IReadOnlyList<PayrollContributionOpeningBalanceDto>>> SaveContributionOpeningBalances(
        [FromBody] PayrollContributionOpeningBalanceBulkSaveDto dto,
        CancellationToken cancellationToken)
        => Handle(() => _payrollService.SaveContributionOpeningBalancesAsync(GetTenantId(), dto, cancellationToken));

    [HttpGet("contribution-transactions")]
    public async Task<ActionResult<IReadOnlyList<PayrollContributionTransactionDto>>> GetContributionTransactions(
        [FromQuery] string? employeeNumber,
        [FromQuery] string? contributionCode,
        [FromQuery] string? contributionCodeType,
        [FromQuery] bool includeInactive,
        CancellationToken cancellationToken)
        => Ok(await _payrollService.GetContributionTransactionsAsync(GetTenantId(), employeeNumber, contributionCode, contributionCodeType, includeInactive, cancellationToken));

    [HttpPost("contribution-transactions")]
    public Task<ActionResult<PayrollContributionTransactionDto>> UpsertContributionTransaction(
        [FromBody] UpsertPayrollContributionTransactionDto dto,
        CancellationToken cancellationToken)
        => Handle(() => _payrollService.UpsertContributionTransactionAsync(GetTenantId(), dto, cancellationToken));

    [HttpPost("imports/employee-reconciliation")]
    public Task<ActionResult<PayrollImportBatchDto>> ImportEmployeeReconciliation([FromBody] PayrollReconciliationImportDto dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.ImportEmployeeReconciliationAsync(GetTenantId(), GetUserId(), dto, cancellationToken));

    [HttpGet("imports/{batchId:guid}")]
    public async Task<ActionResult<PayrollImportBatchDto>> GetImportBatch(Guid batchId, CancellationToken cancellationToken)
    {
        var result = await _payrollService.GetImportBatchAsync(GetTenantId(), batchId, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("runs")]
    public async Task<ActionResult<IReadOnlyList<PayrollRunDto>>> GetRuns(CancellationToken cancellationToken)
        => Ok(await _payrollService.GetRunsAsync(GetTenantId(), cancellationToken));

    [HttpGet("runs/{runId:guid}")]
    public async Task<ActionResult<PayrollRunDto>> GetRun(Guid runId, CancellationToken cancellationToken)
    {
        var result = await _payrollService.GetRunAsync(GetTenantId(), runId, cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("runs")]
    public Task<ActionResult<PayrollRunDto>> CreateRun([FromBody] CreatePayrollRunDto dto, CancellationToken cancellationToken)
        => HandleCreated(() => _payrollService.CreateRunAsync(GetTenantId(), dto, cancellationToken));

    [HttpPost("runs/{runId:guid}/calculate")]
    public Task<ActionResult<PayrollRunDto>> CalculateRun(Guid runId, CancellationToken cancellationToken)
        => Handle(() => _payrollService.CalculateRunAsync(GetTenantId(), runId, GetUserId(), cancellationToken));

    [HttpPost("runs/{runId:guid}/submit-review")]
    public Task<ActionResult<PayrollRunDto>> SubmitRunForReview(Guid runId, [FromBody] PayrollRunActionDto? dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.SubmitRunForReviewAsync(GetTenantId(), runId, GetUserId(), dto ?? new PayrollRunActionDto(), cancellationToken));

    [HttpPost("runs/{runId:guid}/approve")]
    public Task<ActionResult<PayrollRunDto>> ApproveRun(Guid runId, [FromBody] PayrollRunActionDto? dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.ApproveRunAsync(GetTenantId(), runId, GetUserId(), dto ?? new PayrollRunActionDto(), cancellationToken));

    [HttpPost("runs/{runId:guid}/reject")]
    public Task<ActionResult<PayrollRunDto>> RejectRun(Guid runId, [FromBody] PayrollRunActionDto? dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.RejectRunAsync(GetTenantId(), runId, GetUserId(), dto ?? new PayrollRunActionDto(), cancellationToken));

    [HttpPost("runs/{runId:guid}/close")]
    public Task<ActionResult<PayrollRunDto>> CloseRun(Guid runId, [FromBody] PayrollRunActionDto? dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.CloseRunAsync(GetTenantId(), runId, GetUserId(), dto ?? new PayrollRunActionDto(), cancellationToken));

    [HttpGet("runs/{runId:guid}/summary")]
    public Task<ActionResult<PayrollSummaryReportDto>> GetRunSummary(Guid runId, [FromQuery] bool createSnapshot, CancellationToken cancellationToken)
        => Handle(() => _payrollService.GetRunSummaryReportAsync(GetTenantId(), runId, GetUserId(), createSnapshot, cancellationToken));

    [HttpPost("runs/{runId:guid}/oracle-report")]
    public Task<ActionResult<PayrollOracleReportDto>> GetOracleRunReport(Guid runId, [FromBody] PayrollOracleReportRequestDto? dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.GetOracleRunReportAsync(GetTenantId(), runId, dto ?? new PayrollOracleReportRequestDto(), cancellationToken));

    [HttpGet("runs/{runId:guid}/payslips")]
    public Task<ActionResult<IReadOnlyList<PayrollPayslipDto>>> GetPayslips(Guid runId, [FromQuery] Guid? employeeId, [FromQuery] string? categoryType, [FromQuery] string? categoryValue, CancellationToken cancellationToken)
        => Handle(() => _payrollService.GetPayslipsAsync(GetTenantId(), runId, employeeId, categoryType, categoryValue, cancellationToken));

    [HttpPost("runs/{runId:guid}/payslips/snapshots")]
    public Task<ActionResult<IReadOnlyList<PayrollPayslipSnapshotDto>>> GeneratePayslipSnapshots(Guid runId, CancellationToken cancellationToken)
        => Handle(() => _payrollService.GeneratePayslipSnapshotsAsync(GetTenantId(), runId, GetUserId(), cancellationToken));

    [HttpPost("runs/{runId:guid}/payslips/email")]
    public Task<ActionResult<PayrollPayslipEmailResultDto>> EmailPayslips(Guid runId, [FromBody] PayrollPayslipEmailRequestDto? dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.EmailPayslipsAsync(GetTenantId(), runId, GetUserId(), dto ?? new PayrollPayslipEmailRequestDto(), cancellationToken));

    [HttpPost("runs/{runId:guid}/post-journal")]
    public Task<ActionResult<PayrollJournalPostingDto>> PostPayrollJournal(Guid runId, [FromBody] PayrollRunActionDto? dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.PostPayrollJournalAsync(GetTenantId(), runId, GetUserId(), dto ?? new PayrollRunActionDto(), cancellationToken));

    [HttpPost("runs/{runId:guid}/rollback")]
    public Task<ActionResult<PayrollRunDto>> RollbackRun(Guid runId, [FromBody] PayrollRunActionDto? dto, CancellationToken cancellationToken)
        => Handle(() => _payrollService.RollbackRunAsync(GetTenantId(), runId, GetUserId(), dto ?? new PayrollRunActionDto(), cancellationToken));

    private Guid GetTenantId()
        => _currentUserService.TenantId ?? throw new UnauthorizedAccessException("Invalid tenant context.");

    private Guid? GetUserId()
        => Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : null;

    private async Task<ActionResult<T>> Handle<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payroll API request failed");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Payroll request failed." });
        }
    }

    private async Task<ActionResult<T>> HandleCreated<T>(Func<Task<T>> action)
    {
        var result = await Handle(action);
        return result.Result is OkObjectResult ok ? StatusCode(StatusCodes.Status201Created, ok.Value) : result;
    }
}
