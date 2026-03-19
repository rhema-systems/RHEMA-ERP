using System.Drawing;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Colors = QuestPDF.Helpers.Colors;

namespace ErpSystem.Api.Services.Finance.FixedAssets;

public class FixedAssetReportsService : IFixedAssetReportsService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public FixedAssetReportsService(
        ApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;

        // Set QuestPDF license
        QuestPDF.Settings.License = LicenseType.Community;
        // EPPlus license
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    private Guid TenantId => _currentUser.TenantId ?? Guid.Empty;

    public async Task<FixedAssetRegisterDto> GetAssetRegisterAsync(FixedAssetReportQueryDto query)
    {
        var dbQuery = _context.FixedAssets
            .Include(a => a.Category)
            .Include(a => a.MaintenanceAsset)
            .Where(a => a.TenantId == TenantId);

        if (query.CategoryId.HasValue)
            dbQuery = dbQuery.Where(a => a.FixedAssetCategoryId == query.CategoryId.Value);

        if (query.Status.HasValue)
            dbQuery = dbQuery.Where(a => a.Status == query.Status.Value);

        if (!string.IsNullOrEmpty(query.SearchTerm))
            dbQuery = dbQuery.Where(a => a.Name.Contains(query.SearchTerm) || a.AssetCode.Contains(query.SearchTerm));

        var assets = await dbQuery.ToListAsync();

        var items = assets.Select(a => new FixedAssetRegisterItemDto
        {
            AssetCode = a.AssetCode,
            Name = a.Name,
            CategoryName = a.Category?.Name ?? "Unknown",
            AcquisitionDate = a.PurchaseDate,
            Cost = a.AcquisitionCost,
            AccumulatedDepreciation = a.AcquisitionCost - a.NetBookValue,
            NetBookValue = a.NetBookValue,
            Status = a.Status,
            SerialNumber = a.SerialNumber,
            Location = a.MaintenanceAsset?.Location
        }).ToList();

        return new FixedAssetRegisterDto
        {
            Items = items,
            TotalCost = items.Sum(i => i.Cost),
            TotalAccumulatedDepreciation = items.Sum(i => i.AccumulatedDepreciation),
            TotalNetBookValue = items.Sum(i => i.NetBookValue)
        };
    }

    public async Task<List<AssetDisposalReportDto>> GetDisposalReportAsync(FixedAssetReportQueryDto query)
    {
        var dbQuery = _context.AssetDisposals
            .Include(d => d.FixedAsset)
            .Where(d => d.TenantId == TenantId && d.Status == AssetDisposalStatus.Completed);

        if (query.FromDate.HasValue)
            dbQuery = dbQuery.Where(d => d.DisposalDate >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            dbQuery = dbQuery.Where(d => d.DisposalDate <= query.ToDate.Value);

        var disposals = await dbQuery.ToListAsync();

        return disposals.Select(d => new AssetDisposalReportDto
        {
            AssetCode = d.FixedAsset?.AssetCode ?? "N/A",
            Name = d.FixedAsset?.Name ?? "Unknown",
            DisposalDate = d.DisposalDate,
            DisposalType = d.DisposalType,
            SaleProceeds = d.SaleProceeds,
            DisposalCost = d.DisposalCost,
            NetBookValue = d.NetBookValueAtDisposal,
            GainLoss = d.GainOrLoss,
            BuyerName = d.BuyerName
        }).ToList();
    }

    public async Task<List<AssetTransferReportDto>> GetTransferReportAsync(FixedAssetReportQueryDto query)
    {
        var dbQuery = _context.AssetTransfers
            .Include(t => t.FixedAsset)
            .Where(t => t.TenantId == TenantId && t.Status == AssetTransferStatus.Completed);

        if (query.FromDate.HasValue)
            dbQuery = dbQuery.Where(t => t.TransferDate >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            dbQuery = dbQuery.Where(t => t.TransferDate <= query.ToDate.Value);

        var transfers = await dbQuery.ToListAsync();

        return transfers.Select(t => new AssetTransferReportDto
        {
            AssetCode = t.FixedAsset?.AssetCode ?? "N/A",
            Name = t.FixedAsset?.Name ?? "Unknown",
            TransferDate = t.TransferDate,
            FromLocation = t.FromLocation,
            ToLocation = t.ToLocation,
            FromDepartment = null,
            ToDepartment = null,
            Reason = t.Reason
        }).ToList();
    }

    public async Task<VerificationSummaryDto> GetVerificationSummaryAsync(Guid sessionId)
    {
        var session = await _context.AssetVerificationSessions
            .Include(s => s.Items)
            .ThenInclude(i => i.FixedAsset)
            .FirstOrDefaultAsync(s => s.TenantId == TenantId && s.Id == sessionId)
            ?? throw new KeyNotFoundException("Session not found.");

        var conditionSummary = session.Items
            .Where(i => i.IsVerified)
            .GroupBy(i => i.Condition)
            .Select(g => new AssetConditionSummaryDto
            {
                Condition = g.Key,
                Count = g.Count(),
                Percentage = session.Items.Count > 0 ? (decimal)g.Count() / session.Items.Count * 100 : 0
            }).ToList();

        return new VerificationSummaryDto
        {
            SessionId = session.Id,
            SessionName = session.SessionName,
            StartDate = session.ScheduledDate,
            EndDate = session.CompletionDate,
            TotalItems = session.Items.Count,
            VerifiedItems = session.Items.Count(i => i.IsVerified),
            MissingItems = session.Items.Count(i => i.Condition == AssetCondition.Missing),
            ConditionSummary = conditionSummary,
            DetailedItems = session.Items.Select(i => new AssetVerificationItemDto
            {
                Id = i.Id,
                FixedAssetId = i.FixedAssetId,
                AssetCode = i.FixedAsset?.AssetCode,
                FixedAssetName = i.FixedAsset?.Name,
                IsVerified = i.IsVerified,
                Condition = i.Condition,
                VerificationDate = i.VerificationDate,
                CurrentLocation = i.CurrentLocation,
                Notes = i.Notes
            }).ToList()
        };
    }

    public async Task<byte[]> ExportToExcelAsync(string reportType, FixedAssetReportQueryDto query)
    {
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add(reportType);

        if (reportType == "AssetRegister")
        {
            var data = await GetAssetRegisterAsync(query);
            worksheet.Cells[1, 1].Value = "Asset Code";
            worksheet.Cells[1, 2].Value = "Name";
            worksheet.Cells[1, 3].Value = "Category";
            worksheet.Cells[1, 4].Value = "Acquisition Date";
            worksheet.Cells[1, 5].Value = "Cost";
            worksheet.Cells[1, 6].Value = "Acc. Depreciation";
            worksheet.Cells[1, 7].Value = "NBV";
            worksheet.Cells[1, 8].Value = "Status";

            using (var range = worksheet.Cells[1, 1, 1, 8])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            }

            for (int i = 0; i < data.Items.Count; i++)
            {
                var item = data.Items[i];
                worksheet.Cells[i + 2, 1].Value = item.AssetCode;
                worksheet.Cells[i + 2, 2].Value = item.Name;
                worksheet.Cells[i + 2, 3].Value = item.CategoryName;
                worksheet.Cells[i + 2, 4].Value = item.AcquisitionDate.ToString("yyyy-MM-dd");
                worksheet.Cells[i + 2, 5].Value = item.Cost;
                worksheet.Cells[i + 2, 6].Value = item.AccumulatedDepreciation;
                worksheet.Cells[i + 2, 7].Value = item.NetBookValue;
                worksheet.Cells[i + 2, 8].Value = item.Status.ToString();
            }
            worksheet.Cells.AutoFitColumns();
        }
        // ... Other reports can be added here

        return await package.GetAsByteArrayAsync();
    }

    public async Task<byte[]> ExportToPdfAsync(string reportType, FixedAssetReportQueryDto query)
    {
        if (reportType == "AssetRegister")
        {
            var data = await GetAssetRegisterAsync(query);
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.Header().Text("FIXED ASSET REGISTER").FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                    
                    page.Content().PaddingVertical(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(80);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.ConstantColumn(80);
                            columns.ConstantColumn(80);
                            columns.ConstantColumn(80);
                            columns.ConstantColumn(80);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Code");
                            header.Cell().Element(CellStyle).Text("Name");
                            header.Cell().Element(CellStyle).Text("Category");
                            header.Cell().Element(CellStyle).Text("Cost");
                            header.Cell().Element(CellStyle).Text("Depr.");
                            header.Cell().Element(CellStyle).Text("NBV");
                            header.Cell().Element(CellStyle).Text("Status");

                            static IContainer CellStyle(IContainer container) => container.DefaultTextStyle(x => x.Bold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
                        });

                        foreach (var item in data.Items)
                        {
                            table.Cell().Element(ValueStyle).Text(item.AssetCode);
                            table.Cell().Element(ValueStyle).Text(item.Name);
                            table.Cell().Element(ValueStyle).Text(item.CategoryName);
                            table.Cell().Element(ValueStyle).Text(item.Cost.ToString("N2"));
                            table.Cell().Element(ValueStyle).Text(item.AccumulatedDepreciation.ToString("N2"));
                            table.Cell().Element(ValueStyle).Text(item.NetBookValue.ToString("N2"));
                            table.Cell().Element(ValueStyle).Text(item.Status.ToString());

                            static IContainer ValueStyle(IContainer container) => container.PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
                        }
                    });

                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Page ");
                        t.CurrentPageNumber();
                    });
                });
            }).GeneratePdf();
        }
        
        return Array.Empty<byte>();
    }
}
