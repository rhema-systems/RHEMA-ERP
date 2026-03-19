using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Sales;

namespace ErpSystem.Core.Interfaces.Sales
{
    public interface ICompetitorService
    {
        Task<(IEnumerable<CompetitorSummaryDto> Items, int TotalCount)> GetCompetitorsAsync(int page, int pageSize, string? search = null, string? threatLevel = null);
        Task<CompetitorDetailDto?> GetCompetitorByIdAsync(Guid id);
        Task<CompetitorDetailDto> CreateCompetitorAsync(CreateCompetitorDto dto);
        Task<CompetitorDetailDto> UpdateCompetitorAsync(Guid id, CreateCompetitorDto dto);
        Task<CompetitorDealDto> TrackDealAsync(CreateCompetitorDealDto dto);
        Task<CompetitorDealDto> UpdateDealOutcomeAsync(Guid dealId, string outcome, string? lessonsLearned = null);
    }

    public interface ISalesReportingService
    {
        Task<SalesReportSummaryDto> GetSalesSummaryAsync(DateTime? from = null, DateTime? to = null);
    }

    public interface ISalesJournalTemplateService
    {
        Task<(IEnumerable<SalesJournalTemplateSummaryDto> Items, int TotalCount)> GetTemplatesAsync(int page, int pageSize, string? search = null, bool? isActive = null);
        Task<SalesJournalTemplateDetailDto?> GetTemplateByIdAsync(Guid id);
        Task<SalesJournalTemplateDetailDto> CreateTemplateAsync(CreateSalesJournalTemplateDto dto);
        Task<SalesJournalTemplateDetailDto> UpdateTemplateAsync(Guid id, CreateSalesJournalTemplateDto dto);
        Task<bool> ActivateTemplateAsync(Guid id);
        Task<bool> DeactivateTemplateAsync(Guid id);
    }
}
