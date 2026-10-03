using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Core.Interfaces.Inventory;

namespace ErpSystem.Core.Services.Sales
{
    public class ForecastService : IForecastService
    {
        private readonly IGenericRepository<SalesForecast> _forecastRepo;
        private readonly IGenericRepository<SalesForecastLine> _lineRepo;
        private readonly IGenericRepository<SalesOrder> _salesOrderRepo;
        private readonly ICommercialQuantityPolicyValidator? _commercialQuantityValidator;

        public ForecastService(
            IGenericRepository<SalesForecast> forecastRepo,
            IGenericRepository<SalesForecastLine> lineRepo,
            IGenericRepository<SalesOrder> salesOrderRepo,
            ICommercialQuantityPolicyValidator? commercialQuantityValidator = null)
        {
            _forecastRepo = forecastRepo;
            _lineRepo = lineRepo;
            _salesOrderRepo = salesOrderRepo;
            _commercialQuantityValidator = commercialQuantityValidator;
        }

        public async Task<(IEnumerable<ForecastSummaryDto> Items, int TotalCount)> GetForecastsAsync(int page, int pageSize, string? search = null, string? status = null)
        {
            var query = _forecastRepo.GetQueryable().Include(f => f.Lines).Where(f => !f.IsDeleted);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(f => f.Name.Contains(search));
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SalesForecastStatus>(status, out var st))
                query = query.Where(f => f.Status == st);

            var totalCount = await query.CountAsync();
            var items = await query.OrderByDescending(f => f.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(f => new ForecastSummaryDto
                {
                    Id = f.Id, Name = f.Name, Method = f.Method.ToString(), Status = f.Status.ToString(),
                    PeriodStart = f.PeriodStart, PeriodEnd = f.PeriodEnd,
                    TotalForecastAmount = f.TotalForecastAmount, TotalActualAmount = f.TotalActualAmount,
                    Variance = f.TotalActualAmount - f.TotalForecastAmount,
                    VariancePercentage = f.TotalForecastAmount != 0 ? Math.Round((f.TotalActualAmount - f.TotalForecastAmount) / f.TotalForecastAmount * 100, 2) : 0,
                    OwnerName = f.OwnerName, LineCount = f.Lines.Count, CreatedAt = f.CreatedAt
                }).ToListAsync();

            return (items, totalCount);
        }

        public async Task<ForecastDetailDto?> GetForecastByIdAsync(Guid id)
        {
            var f = await _forecastRepo.GetQueryable().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);
            if (f == null) return null;
            return MapDetail(f);
        }

        public async Task<ForecastDetailDto> CreateForecastAsync(CreateForecastDto dto)
        {
            var forecast = new SalesForecast
            {
                Name = dto.Name, Description = dto.Description,
                Method = Enum.Parse<SalesForecastMethod>(dto.Method),
                PeriodStart = dto.PeriodStart, PeriodEnd = dto.PeriodEnd,
                OwnerId = dto.OwnerId, Notes = dto.Notes
            };

            foreach (var line in dto.Lines)
            {
                var forecastLine = new SalesForecastLine
                {
                    SalesForecastId = forecast.Id,
                    Category = line.Category, ProductName = line.ProductName,
                    ProductId = line.ProductId, SalesRepName = line.SalesRepName,
                    SalesRepId = line.SalesRepId,
                    ForecastQuantity = line.ForecastQuantity, ForecastAmount = line.ForecastAmount,
                    Unit = line.Unit, UnitOfMeasureId = line.UnitOfMeasureId,
                    Notes = line.Notes
                };
                await ValidateLineQuantityAsync(forecastLine, "Forecast create");
                forecast.Lines.Add(forecastLine);
            }

            forecast.TotalForecastAmount = forecast.Lines.Sum(l => l.ForecastAmount);
            await _forecastRepo.AddAsync(forecast);
            await _forecastRepo.SaveChangesAsync();
            return (await GetForecastByIdAsync(forecast.Id))!;
        }

        public async Task<ForecastDetailDto> SubmitForecastAsync(Guid id)
        {
            var f = await GetForecast(id);
            if (f.Status != SalesForecastStatus.Draft)
                throw new InvalidOperationException("Only draft forecasts can be submitted");
            await ValidateForecastQuantitiesAsync(f, "Forecast submit");
            f.Status = SalesForecastStatus.Submitted; f.SubmittedDate = DateTime.UtcNow; f.UpdatedAt = DateTime.UtcNow;
            await _forecastRepo.UpdateAsync(f); await _forecastRepo.SaveChangesAsync();
            return MapDetail(f);
        }

        public async Task<ForecastDetailDto> ApproveForecastAsync(Guid id)
        {
            var f = await GetForecast(id);
            if (f.Status != SalesForecastStatus.Submitted)
                throw new InvalidOperationException("Only submitted forecasts can be approved");
            await ValidateForecastQuantitiesAsync(f, "Forecast approve");
            f.Status = SalesForecastStatus.Approved; f.ApprovedDate = DateTime.UtcNow; f.UpdatedAt = DateTime.UtcNow;
            await _forecastRepo.UpdateAsync(f); await _forecastRepo.SaveChangesAsync();
            return MapDetail(f);
        }

        public async Task<ForecastDetailDto> LockForecastAsync(Guid id)
        {
            var f = await GetForecast(id);
            if (f.Status != SalesForecastStatus.Approved)
                throw new InvalidOperationException("Only approved forecasts can be locked");
            await ValidateForecastQuantitiesAsync(f, "Forecast lock");
            f.Status = SalesForecastStatus.Locked; f.UpdatedAt = DateTime.UtcNow;
            await _forecastRepo.UpdateAsync(f); await _forecastRepo.SaveChangesAsync();
            return MapDetail(f);
        }

        public async Task<ForecastDetailDto> RefreshActualsAsync(Guid id)
        {
            var f = await GetForecast(id);
            var orders = await _salesOrderRepo.GetQueryable()
                .Where(o => !o.IsDeleted && o.DocumentDate >= f.PeriodStart && o.DocumentDate <= f.PeriodEnd
                    && o.OrderStatus == SalesOrderStatus.Closed)
                .ToListAsync();

            decimal totalActual = orders.Sum(o => o.TotalAmount);
            f.TotalActualAmount = totalActual;

            foreach (var line in f.Lines)
            {
                if (f.TotalForecastAmount > 0)
                    line.ActualAmount = totalActual * (line.ForecastAmount / f.TotalForecastAmount);
            }

            f.UpdatedAt = DateTime.UtcNow;
            await _forecastRepo.UpdateAsync(f); await _forecastRepo.SaveChangesAsync();
            return MapDetail(f);
        }

        private async Task<SalesForecast> GetForecast(Guid id)
        {
            return await _forecastRepo.GetQueryable().Include(x => x.Lines)
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted)
                ?? throw new InvalidOperationException("Forecast not found");
        }

        private async Task ValidateForecastQuantitiesAsync(SalesForecast forecast, string boundary)
        {
            foreach (var line in forecast.Lines.Where(line => !line.IsDeleted))
                await ValidateLineQuantityAsync(line, boundary);
        }

        private Task ValidateLineQuantityAsync(SalesForecastLine line, string boundary)
        {
            var validator = _commercialQuantityValidator
                ?? throw new InvalidOperationException("Commercial quantity policy validation is not configured for Sales forecasts.");
            return SalesCommercialQuantityEvidence.ValidateAndFreezeAsync(
                validator, line, line.Unit, line.ForecastQuantity, $"{boundary} line {line.Id}");
        }

        private ForecastDetailDto MapDetail(SalesForecast f)
        {
            return new ForecastDetailDto
            {
                Id = f.Id, Name = f.Name, Description = f.Description,
                Method = f.Method.ToString(), Status = f.Status.ToString(),
                PeriodStart = f.PeriodStart, PeriodEnd = f.PeriodEnd,
                TotalForecastAmount = f.TotalForecastAmount, TotalActualAmount = f.TotalActualAmount,
                Variance = f.TotalActualAmount - f.TotalForecastAmount,
                VariancePercentage = f.TotalForecastAmount != 0 ? Math.Round((f.TotalActualAmount - f.TotalForecastAmount) / f.TotalForecastAmount * 100, 2) : 0,
                OwnerName = f.OwnerName, OwnerId = f.OwnerId,
                SubmittedDate = f.SubmittedDate, ApprovedDate = f.ApprovedDate, ApprovedBy = f.ApprovedBy,
                Notes = f.Notes, CreatedAt = f.CreatedAt, LineCount = f.Lines.Count,
                Lines = f.Lines.Select(l => new ForecastLineDto
                {
                    Id = l.Id, Category = l.Category, ProductName = l.ProductName,
                    SalesRepName = l.SalesRepName,
                    ForecastQuantity = l.ForecastQuantity, ForecastAmount = l.ForecastAmount,
                    Unit = l.Unit, UnitOfMeasureId = l.UnitOfMeasureId,
                    UnitOfMeasureCodeSnapshot = l.UnitOfMeasureCodeSnapshot,
                    UnitOfMeasureDecimalPlacesSnapshot = l.UnitOfMeasureDecimalPlacesSnapshot,
                    UnitOfMeasureRoundingIncrementSnapshot = l.UnitOfMeasureRoundingIncrementSnapshot,
                    ActualQuantity = l.ActualQuantity, ActualAmount = l.ActualAmount,
                    Variance = l.ActualAmount - l.ForecastAmount, Notes = l.Notes
                }).ToList()
            };
        }
    }
}
