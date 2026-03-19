using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Sales;

namespace ErpSystem.Core.Interfaces.Sales
{
    public interface IForecastService
    {
        Task<(IEnumerable<ForecastSummaryDto> Items, int TotalCount)> GetForecastsAsync(int page, int pageSize, string? search = null, string? status = null);
        Task<ForecastDetailDto?> GetForecastByIdAsync(Guid id);
        Task<ForecastDetailDto> CreateForecastAsync(CreateForecastDto dto);
        Task<ForecastDetailDto> SubmitForecastAsync(Guid id);
        Task<ForecastDetailDto> ApproveForecastAsync(Guid id);
        Task<ForecastDetailDto> LockForecastAsync(Guid id);
        Task<ForecastDetailDto> RefreshActualsAsync(Guid id);
    }
}
