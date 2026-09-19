using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using ErpSystem.Api.Tests.Services.Finance;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Documents;

public sealed class DmsGenerationTemplateMigrationCompatibilityTests
{
    private const string SchemaOwnerMigrationId =
        "20260820120000_AddDmsGenerationTemplateWordSource";
    private const string CompatibilityMigrationId =
        "20260822130000_AddUploadedDocumentTemplateColumns";

    private static readonly string[] Columns =
    {
        "TemplateFileUploadRecordId",
        "TemplateRepositoryPath",
        "TemplateFileName",
        "TemplateContentType",
        "TemplateFileSize"
    };

    [Fact]
    [Trait("Category", "Architecture")]
    public void BothHistoricalMigrations_ShouldRemainDiscoverableInOrder()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=DmsTemplateMigrationDiscovery;Trusted_Connection=True")
            .Options;

        using var context = new ApplicationDbContext(options);
        var migrations = context.GetService<IMigrationsAssembly>().Migrations;

        migrations.Keys.Should().Equal("20260916132000_DisposableDevelopmentCurrentModelBaseline");
        ArchivedMigrationSource.Read("20260820120000_AddDmsGenerationTemplateWordSource.cs")
            .Should().Contain("class AddDmsGenerationTemplateWordSource");
        ArchivedMigrationSource.Read("20260822130000_AddUploadedDocumentTemplateColumns.cs")
            .Should().Contain(CompatibilityMigrationId);
        string.CompareOrdinal(SchemaOwnerMigrationId, CompatibilityMigrationId)
            .Should().BeNegative();
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void SchemaOwnerUp_ShouldCreateEveryColumnAndIndexIdempotently()
    {
        var sql = ArchivedMigrationSource.Read("20260820120000_AddDmsGenerationTemplateWordSource.cs");
        sql.Should().NotContain("migrationBuilder.AddColumn")
            .And.NotContain("migrationBuilder.CreateIndex");
        foreach (var column in Columns)
        {
            sql.Should().Contain(
                $"COL_LENGTH(N'dbo.CentralDocumentGenerationTemplates', N'{column}') IS NULL");
        }

        sql.Should().Contain(
            "IX_CentralDocumentGenerationTemplates_TenantId_TemplateFileUploadRecordId");
        sql.Should().Contain("IF NOT EXISTS");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void LaterDuplicateMigration_ShouldBeACompatibilityNoOp()
    {
        var source = ArchivedMigrationSource.Read("20260822130000_AddUploadedDocumentTemplateColumns.cs");
        source.Should().Contain("Compatibility marker only")
            .And.Contain("The earlier schema-owning migration removes these columns")
            .And.NotContain("migrationBuilder.AddColumn")
            .And.NotContain("migrationBuilder.DropColumn");
    }
}
