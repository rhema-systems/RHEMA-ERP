using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Sales;

namespace ErpSystem.Core.Interfaces.Sales
{
    public interface ICommissionService
    {
        // Rules
        Task<(IEnumerable<CommissionRuleSummaryDto> Items, int TotalCount)> GetRulesAsync(int page, int pageSize, string? search = null, bool? isActive = null);
        Task<CommissionRuleDetailDto?> GetRuleByIdAsync(Guid id);
        Task<CommissionRuleDetailDto> CreateRuleAsync(CreateCommissionRuleDto dto);
        Task<CommissionRuleDetailDto> UpdateRuleAsync(Guid id, CreateCommissionRuleDto dto);
        Task<bool> ActivateRuleAsync(Guid id);
        Task<bool> DeactivateRuleAsync(Guid id);

        // Statements
        Task<(IEnumerable<CommissionStatementSummaryDto> Items, int TotalCount)> GetStatementsAsync(int page, int pageSize, string? search = null, string? status = null);
        Task<CommissionStatementDetailDto?> GetStatementByIdAsync(Guid id);
        Task<CommissionStatementDetailDto> GenerateStatementAsync(GenerateStatementDto dto);
        Task<CommissionStatementDetailDto> ApproveStatementAsync(Guid id);
        Task<CommissionStatementDetailDto> MarkAsPaidAsync(Guid id, string? paymentReference = null);
        Task<CommissionStatementDetailDto> DisputeStatementAsync(Guid id, string? reason = null);
    }
}
