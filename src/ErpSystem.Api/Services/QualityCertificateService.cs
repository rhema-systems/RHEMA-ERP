using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ErpSystem.Api.Services;

public class QualityCertificateService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<QualityCertificateService> _logger;

    public QualityCertificateService(ApplicationDbContext context, ILogger<QualityCertificateService> logger)
    {
        _context = context;
        _logger = logger;

        // Set QuestPDF license (Community license for free use)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> GenerateCertificateAsync(Guid qualityCheckId)
    {
        try
        {
            // Load quality check with all related data
            var qualityCheck = await _context.WorkOrderQualityChecks
                .Include(qc => qc.Checklist)
                .FirstOrDefaultAsync(qc => qc.Id == qualityCheckId) ?? throw new Exception($"Quality check {qualityCheckId} not found");

            // Load work order
            var workOrder = await _context.WorkOrders
                .Include(wo => wo.Asset)
                .Include(wo => wo.WorkOrderType)
                .Include(wo => wo.MaintenanceType)
                .Include(wo => wo.PriorityLevel)
                .Include(wo => wo.JobCard)
                    .ThenInclude(jc => jc.CustomerBusinessPartner)
                .FirstOrDefaultAsync(wo => wo.Id == qualityCheck.WorkOrderId) ?? throw new Exception($"Work order {qualityCheck.WorkOrderId} not found");

            // Parse checklist items and results
            var checklistItems = new List<ChecklistItemData>();
            try
            {
                if (!string.IsNullOrEmpty(qualityCheck.Checklist?.ChecklistItems))
                {
                    var items = System.Text.Json.JsonSerializer.Deserialize<List<ChecklistItemJson>>(qualityCheck.Checklist.ChecklistItems);
                    checklistItems = items?.Select(i => new ChecklistItemData
                    {
                        Item = i.item,
                        Description = i.description,
                        Weight = i.weight
                    }).ToList() ?? new List<ChecklistItemData>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse checklist items");
            }

            var checkResults = new List<CheckResultData>();
            try
            {
                if (!string.IsNullOrEmpty(qualityCheck.CheckResults))
                {
                    var results = System.Text.Json.JsonSerializer.Deserialize<List<CheckResultJson>>(qualityCheck.CheckResults);
                    checkResults = results?.Select(r => new CheckResultData
                    {
                        ItemId = r.ItemId,
                        Result = r.Result,
                        Notes = r.Notes
                    }).ToList() ?? new List<CheckResultData>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse check results");
            }

            // Generate PDF
            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30); // Reduced from 50 to 30
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Black)); // Reduced from 11 to 10

                    page.Header().Element(header => ComposeHeader(header, qualityCheck));
                    page.Content().Element(content => ComposeContent(content, workOrder, qualityCheck, checklistItems, checkResults));
                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
                    });
                });
            }).GeneratePdf();

            return pdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating certificate for quality check {QualityCheckId}", qualityCheckId);
            throw;
        }
    }

    private void ComposeHeader(IContainer container, ErpSystem.Core.Entities.Maintenance.WorkOrderQualityCheck qualityCheck)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("QUALITY INSPECTION CERTIFICATE").FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                column.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(9).FontColor(Colors.Grey.Darken1);
            });

            // Stamp in top-right corner
            row.ConstantItem(100).Height(100).Element(c => ComposeResultBadge(c, qualityCheck));
        });
    }
    private void ComposeContent(IContainer container,
        ErpSystem.Core.Entities.Maintenance.WorkOrder workOrder,
        ErpSystem.Core.Entities.Maintenance.WorkOrderQualityCheck qualityCheck,
        List<ChecklistItemData> checklistItems,
        List<CheckResultData> checkResults)
    {
        container.PaddingTop(5).Column(column =>
        {
            // Work Order Information (stamp moved to header)
            column.Item().Element(c => ComposeSection(c, "Work Order Information", table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(150);
                    columns.RelativeColumn();
                });

                AddRow(table, "Work Order Number:", workOrder.WorkOrderNumber);
                AddRow(table, "Job Card Number:", workOrder.JobCard?.JobCardNumber ?? "N/A");
                AddRow(table, "Customer:", workOrder.JobCard?.CustomerBusinessPartner?.PartnerName ?? "N/A");
                AddRow(table, "Title:", workOrder.Title);
                AddRow(table, "Asset:", workOrder.Asset?.Name ?? "Unknown");
                AddRow(table, "Location:", workOrder.Asset?.Location ?? "N/A");
                AddRow(table, "Work Order Type:", workOrder.WorkOrderType?.Name ?? "Unknown");
                AddRow(table, "Maintenance Type:", workOrder.MaintenanceType?.Name ?? "Unknown");
                AddRow(table, "Priority:", workOrder.PriorityLevel?.Name ?? "Unknown");

                // Prefer the work order's actual completion date, but fall back to inspection date so the certificate never shows N/A
                var completedDate = workOrder.ActualCompletionDate ?? (qualityCheck.InspectionDate == default ? (DateTime?)null : qualityCheck.InspectionDate);
                // Format the completed date the same way as the inspection date (e.g. 18-Nov-25 14:23:45)
                AddRow(table, "Completed Date:", completedDate?.ToString("dd-MMM-yy HH:mm:ss") ?? "N/A");
            }));

            column.Item().PaddingTop(10).Element(c => ComposeSection(c, "Inspection Details", table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(150);
                    columns.RelativeColumn();
                });

                AddRow(table, "Checklist:", qualityCheck.Checklist?.Name ?? "Unknown");
                AddRow(table, "Inspection Date:", qualityCheck.InspectionDate.ToString("dd-MMM-yy HH:mm:ss"));
                AddRow(table, "Overall Result:", qualityCheck.OverallResult);
                AddRow(table, "Score:", $"{qualityCheck.Score}%");
                AddRow(table, "Minimum Passing Score:", $"{qualityCheck.Checklist?.MinimumPassingScore ?? 80}%");
                AddRow(table, "Inspector Notes:", qualityCheck.Notes ?? "None");
            }));

            // Checklist Results
            if (checklistItems.Any() && checkResults.Any())
            {
                column.Item().PaddingTop(10).Element(c => ComposeChecklistResults(c, checklistItems, checkResults));
            }

            // Corrective Actions (if any)
            if (!string.IsNullOrEmpty(qualityCheck.CorrectiveActions))
            {
                column.Item().PaddingTop(10).Element(c => ComposeSection(c, "Corrective Actions", table =>
                {
                    table.ColumnsDefinition(columns => columns.RelativeColumn());
                    table.Cell().Text(qualityCheck.CorrectiveActions);
                }));
            }

            // Signature Section
            column.Item().PaddingTop(15).Element(ComposeSignatureSection);
        });
    }

    private void ComposeResultBadge(IContainer container, ErpSystem.Core.Entities.Maintenance.WorkOrderQualityCheck qualityCheck)
    {
        var imagePath = qualityCheck.OverallResult == "Pass"
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Images", "pass-stamp.png")
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Images", "fail-stamp.png");

        // Check if image file exists
        if (File.Exists(imagePath))
        {
            // Use smaller stamp image to fit on one page
            container.Width(100).Height(100).AlignCenter().Image(imagePath);
        }
        else
        {
            // Fallback to smaller text-based badge
            _logger.LogWarning($"Stamp image not found at {imagePath}. Using text fallback.");

            var bgColor = qualityCheck.OverallResult == "Pass" ? Colors.Green.Lighten3 : Colors.Red.Lighten3;
            var textColor = qualityCheck.OverallResult == "Pass" ? Colors.Green.Darken2 : Colors.Red.Darken2;

            container.Width(100).Height(100).AlignCenter().AlignMiddle().Border(4)
                .BorderColor(qualityCheck.OverallResult == "Pass" ? Colors.Green.Darken1 : Colors.Red.Darken1)
                .Background(bgColor)
                .Padding(10)
                .Column(column =>
                {
                    column.Item().AlignCenter().Text(qualityCheck.OverallResult == "Pass" ? "✓" : "✗")
                        .FontSize(35).Bold().FontColor(textColor);
                    column.Item().AlignCenter().Text(qualityCheck.OverallResult.ToUpper())
                        .FontSize(16).Bold().FontColor(textColor);
                });
        }
    }

    private static void ComposeSection(IContainer container, string title, Action<TableDescriptor> tableContent)
    {
        container.Column(column =>
        {
            column.Item().Text(title).FontSize(13).Bold().FontColor(Colors.Blue.Darken1);
            column.Item().PaddingTop(6).Table(tableContent); // Increased from 3 to 6 for better spacing
        });
    }

    private static void AddRow(TableDescriptor table, string label, string value)
    {
        table.Cell().Text(label).Bold();
        table.Cell().Text(value);
    }

    private static void ComposeChecklistResults(IContainer container, List<ChecklistItemData> items, List<CheckResultData> results)
    {
        container.Column(column =>
        {
            column.Item().Text("Checklist Results").FontSize(14).Bold().FontColor(Colors.Blue.Darken1);
            column.Item().PaddingTop(5).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);
                    columns.RelativeColumn(3);
                    columns.ConstantColumn(60);
                    columns.RelativeColumn(2);
                });

                // Header
                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("#").Bold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Item").Bold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Result").Bold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Notes").Bold();
                });

                // Rows
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    var result = results.FirstOrDefault(r => r.ItemId == $"item-{i}");

                    var bgColor = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                    var resultColor = result?.Result == "Pass" ? Colors.Green.Medium :
                                     result?.Result == "Fail" ? Colors.Red.Medium : Colors.Grey.Medium;

                    table.Cell().Background(bgColor).Padding(5).Text($"{i + 1}");
                    table.Cell().Background(bgColor).Padding(5).Text(item.Item);
                    table.Cell().Background(bgColor).Padding(5).Text(result?.Result ?? "N/A").FontColor(resultColor).Bold();
                    table.Cell().Background(bgColor).Padding(5).Text(result?.Notes ?? "");
                }
            });
        });
    }

    private void ComposeSignatureSection(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Height(50).BorderBottom(1).BorderColor(Colors.Black);
                column.Item().PaddingTop(5).Text("Inspector Signature").FontSize(9);
                column.Item().PaddingTop(3).Text($"Date: {DateTime.Now:dd-MMM-yy HH:mm:ss}").FontSize(9).FontColor(Colors.Grey.Medium);
            });

            row.ConstantItem(50);

            row.RelativeItem().Column(column =>
            {
                column.Item().Height(50).BorderBottom(1).BorderColor(Colors.Black);
                column.Item().PaddingTop(5).Text("Supervisor Signature").FontSize(9);
                column.Item().PaddingTop(3).Text("Date: _____________").FontSize(9).FontColor(Colors.Grey.Medium);
            });
        });
    }

    // Helper classes for JSON deserialization
    private class ChecklistItemJson
    {
        public string item { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public int weight { get; set; }
    }

    private class CheckResultJson
    {
        public string ItemId { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    private class ChecklistItemData
    {
        public string Item { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Weight { get; set; }
    }

    private class CheckResultData
    {
        public string ItemId { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }
}
