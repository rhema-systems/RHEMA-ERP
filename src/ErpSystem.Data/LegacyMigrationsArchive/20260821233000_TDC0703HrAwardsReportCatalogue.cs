using System.Text.Json;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Services.HR;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Publishes the HR awards system reports to every existing tenant (FR-HR-113, area 14 slice 10).
/// </summary>
/// <remarks>
/// <para><b>Why a migration as well as a seeder.</b> <c>HrAwardsReportSeeder</c> runs under
/// <c>seed-db</c> and when a tenant is provisioned — neither of which happens to a tenant that
/// already exists. Without this, the report would be defined in code, registered in DI, executable
/// by the engine, and <b>invisible to every tenant already running</b>: the "declared, implemented,
/// called by nothing" shape this module has produced twice already. Its procurement and inventory
/// siblings each carry the same migration for the same reason.</para>
///
/// <para><b>It changes no schema.</b> There is nothing here for the model snapshot, which is why it
/// is hand-written and carries its own <c>[DbContext]</c> and <c>[Migration]</c> attributes rather
/// than being scaffolded and listed in <c>FastBuildMigrationMetadata</c> — matching TDC0701 and
/// TDC0702.</para>
///
/// <para><b>Idempotent by <c>NOT EXISTS</c> on the query string</b>, so a tenant that already has
/// the row is left alone and re-running costs nothing. The seeder is still the authority on the
/// row's <i>contents</i>: it repairs a drifted definition, whereas this only ever inserts a missing
/// one.</para>
/// </remarks>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260821233000_TDC0703HrAwardsReportCatalogue")]
public sealed class TDC0703HrAwardsReportCatalogue : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var parameters = Sql(JsonSerializer.Serialize(HrAwardsReportCatalogue.BuildParameters()));
        var visualization = Sql(JsonSerializer.Serialize(new ReportVisualizationDto { Type = "Table" }));

        foreach (var definition in HrAwardsReportCatalogue.Definitions)
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
                       N'{{HrAwardsReportCatalogue.ReportType}}', N'published', N'{{Sql(definition.Query)}}',
                       N'{{parameters}}', N'{{columns}}', N'{{visualization}}', N'{{tags}}', 0,
                       (SELECT TOP (1) [tm].[Id] FROM [dbo].[TenantModules] [tm]
                        WHERE [tm].[TenantId] = [tenant].[Id] AND [tm].[ModuleName] = N'HR'
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
        migrationBuilder.Sql($$"""
            DELETE FROM [dbo].[Reports]
            WHERE [Query] LIKE N'{{HrAwardsReportCatalogue.QueryPrefix}}%';
            """);
    }

    private static string Sql(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
