using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class AwardVerificationDocumentMigrationTests
{
    [Fact]
    public void MigrationOnlyAddsTheIndependentRuleAndDoesNotRewriteReviewHistory()
    {
        var migration = new SeparateAwardVerificationDocumentRequirement();
        var column = migration.UpOperations.Should().ContainSingle().Subject.Should().BeOfType<AddColumnOperation>().Subject;
        column.Table.Should().Be("AwardVerificationChecklistItems");
        column.Name.Should().Be("RequiresDocument");
        column.ClrType.Should().Be(typeof(bool));
        column.DefaultValue.Should().Be(false);
        column.IsNullable.Should().BeFalse();
        migration.DownOperations.Should().ContainSingle().Which.Should().BeOfType<DropColumnOperation>();
    }
}
