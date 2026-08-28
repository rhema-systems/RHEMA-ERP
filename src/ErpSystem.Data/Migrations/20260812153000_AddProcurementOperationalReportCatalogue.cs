using System.Text.Json;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Services.Procurement;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260812153000_AddProcurementOperationalReportCatalogue")]
public sealed class AddProcurementOperationalReportCatalogue : Migration
{
    private static readonly string[] Codes =
    {
        ProcurementStatutoryReportCatalogue.RequisitionStatusCode,
        ProcurementStatutoryReportCatalogue.PurchaseOrderRegisterCode,
        ProcurementStatutoryReportCatalogue.CommitmentRegisterCode,
        ProcurementStatutoryReportCatalogue.CertificateTrackingCode
    };

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var parameters = Sql(JsonSerializer.Serialize(ProcurementStatutoryReportCatalogue.BuildParameters()));
        var visualization = Sql(JsonSerializer.Serialize(new ReportVisualizationDto { Type = "Table" }));
        foreach (var definition in ProcurementStatutoryReportCatalogue.Definitions.Where(item => Codes.Contains(item.Code)))
        {
            var columns = Sql(JsonSerializer.Serialize(definition.Columns.Select((column, index) => new ReportColumnDto
            {
                Name = column.Name,
                DisplayName = column.DisplayName,
                DataType = column.DataType,
                Format = column.Format,
                IsVisible = column.IsVisible,
                Order = index,
                AggregationType = column.AggregationType
            })));
            var tags = Sql(JsonSerializer.Serialize(definition.Tags));
            migrationBuilder.Sql($$"""
                INSERT INTO [dbo].[Reports]
                    ([Id], [Name], [Description], [Type], [Status], [Query], [Parameters], [Columns],
                     [Visualization], [Tags], [IsScheduled], [ModuleId], [CreatedAt], [CreatedBy],
                     [UpdatedAt], [UpdatedBy], [IsDeleted], [TenantId])
                SELECT NEWID(), N'{{Sql(definition.Name)}}', N'{{Sql(definition.Description)}}',
                       N'{{ProcurementStatutoryReportCatalogue.ReportType}}', N'published', N'{{Sql(definition.Query)}}',
                       N'{{parameters}}', N'{{columns}}', N'{{visualization}}', N'{{tags}}', 0,
                       (SELECT TOP (1) [tm].[Id] FROM [dbo].[TenantModules] [tm]
                        WHERE [tm].[TenantId] = [tenant].[Id] AND [tm].[ModuleName] = N'Procurement'
                          AND [tm].[IsDeleted] = 0 ORDER BY [tm].[CreatedAt]),
                       SYSUTCDATETIME(), N'System', SYSUTCDATETIME(), N'System', 0, [tenant].[Id]
                FROM [dbo].[Tenants] [tenant]
                WHERE [tenant].[IsDeleted] = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM [dbo].[Reports] [report]
                      WHERE [report].[TenantId] = [tenant].[Id]
                        AND [report].[Query] = N'{{Sql(definition.Query)}}');
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var code in Codes)
        {
            migrationBuilder.Sql($$"""
                DELETE FROM [dbo].[Reports]
                WHERE [Query] = N'{{ProcurementStatutoryReportCatalogue.QueryPrefix}}{{Sql(code)}}';
                """);
        }
    }

    private static string Sql(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
