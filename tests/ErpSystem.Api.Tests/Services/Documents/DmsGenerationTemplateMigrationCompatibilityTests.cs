using ErpSystem.Data;
using ErpSystem.Data.Migrations;
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

        migrations.Should().ContainKey(SchemaOwnerMigrationId);
        migrations.Should().ContainKey(CompatibilityMigrationId);
        string.CompareOrdinal(SchemaOwnerMigrationId, CompatibilityMigrationId)
            .Should().BeNegative();
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void SchemaOwnerUp_ShouldCreateEveryColumnAndIndexIdempotently()
    {
        var migrationBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestableSchemaOwnerMigration().ApplyUp(migrationBuilder);

        var sql = migrationBuilder.Operations.Should().ContainSingle()
            .Which.Should().BeOfType<SqlOperation>().Which.Sql;

        migrationBuilder.Operations.Should().NotContain(operation =>
            operation is AddColumnOperation or CreateIndexOperation);
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
        var upBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var downBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var migration = new TestableCompatibilityMigration();

        migration.ApplyUp(upBuilder);
        migration.ApplyDown(downBuilder);

        upBuilder.Operations.Should().BeEmpty();
        downBuilder.Operations.Should().BeEmpty();
    }

    private sealed class TestableSchemaOwnerMigration : AddDmsGenerationTemplateWordSource
    {
        public void ApplyUp(MigrationBuilder migrationBuilder) => Up(migrationBuilder);
    }

    private sealed class TestableCompatibilityMigration : AddUploadedDocumentTemplateColumns
    {
        public void ApplyUp(MigrationBuilder migrationBuilder) => Up(migrationBuilder);

        public void ApplyDown(MigrationBuilder migrationBuilder) => Down(migrationBuilder);
    }
}
