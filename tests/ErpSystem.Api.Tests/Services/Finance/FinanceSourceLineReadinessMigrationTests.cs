using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceSourceLineReadinessMigrationTests
{
    [Fact]
    [Trait("Category", "Architecture")]
    public void Up_adds_only_the_generic_trusted_source_context_schema()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestableMigration().ApplyUp(builder);

        builder.Operations.OfType<AddColumnOperation>().Select(item => item.Name).Should().BeEquivalentTo(
            "ExpectedSourceLineCount", "ResolvedAccountId", "SourceDocumentDate", "SourceLineManifestHash");
        builder.Operations.OfType<CreateIndexOperation>().Should().ContainSingle(index =>
            index.Name == "IX_FinanceSourceDimensionAssignments_TenantId_ResolvedAccountId"
            && index.Columns.SequenceEqual(new[] { "TenantId", "ResolvedAccountId" }));
        builder.Operations.OfType<AddForeignKeyOperation>().Should().ContainSingle(foreignKey =>
            foreignKey.Name == "FK_FinanceSourceDimensionAssignments_Accounts_ResolvedAccountId"
            && foreignKey.PrincipalTable == "Accounts"
            && foreignKey.OnDelete == ReferentialAction.Restrict);
        builder.Operations.Should().HaveCount(6);
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void Down_removes_the_source_context_schema_in_dependency_order()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestableMigration().ApplyDown(builder);

        builder.Operations[0].Should().BeOfType<DropForeignKeyOperation>();
        builder.Operations[1].Should().BeOfType<DropIndexOperation>();
        builder.Operations.OfType<DropColumnOperation>().Select(item => item.Name).Should().BeEquivalentTo(
            "ExpectedSourceLineCount", "ResolvedAccountId", "SourceDocumentDate", "SourceLineManifestHash");
        builder.Operations.Should().HaveCount(6);
    }

    private sealed class TestableMigration : AddFinanceSourceLineReadinessEvidence
    {
        public void ApplyUp(MigrationBuilder builder) => Up(builder);
        public void ApplyDown(MigrationBuilder builder) => Down(builder);
    }
}
