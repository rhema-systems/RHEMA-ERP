using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementUnpublishedScheduleMigrationTests
{
    [Fact]
    public void MigrationAddsOnlyNullableOpeningHistoryAndKeepsOriginalRegisterUntouched()
    {
        var type = typeof(AllowUnpublishedTenderScheduleReschedule);
        type.GetCustomAttribute<DbContextAttribute>().Should().NotBeNull();
        type.GetCustomAttribute<MigrationAttribute>()!.Id.Should().Be("20260905193000_AllowUnpublishedTenderScheduleReschedule");
        var operations = Operations("Up");
        operations.OfType<AddColumnOperation>().Should().HaveCount(2)
            .And.OnlyContain(item => item.Table == "ProcurementTenderDocumentChanges" && item.IsNullable);
        operations.OfType<AddColumnOperation>().Select(item => item.Name).Should().BeEquivalentTo(
            ["PreviousOpeningScheduledAtUtc", "NewOpeningScheduledAtUtc"]);
        operations.OfType<DropColumnOperation>().Should().BeEmpty();
        operations.OfType<UpdateDataOperation>().Should().BeEmpty();
        operations.OfType<DeleteDataOperation>().Should().BeEmpty();
    }

    [Fact]
    public void ConstraintsKeepRescheduleDistinctAndOpeningAfterSubmission()
    {
        var checks = Operations("Up").OfType<AddCheckConstraintOperation>().ToList();
        checks.Should().HaveCount(2);
        var kind = checks.Single(item => item.Name.EndsWith("_Kind")).Sql;
        kind.Should().Contain("[ChangeType] = 3")
            .And.Contain("[NewOpeningScheduledAtUtc] > [NewValueUtc]")
            .And.Contain("[NewValueUtc] > [PreviousValueUtc]")
            .And.Contain("[RequiresAcknowledgement] = 0")
            .And.Contain("[PreviousOpeningScheduledAtUtc] IS NULL AND [NewOpeningScheduledAtUtc] IS NULL");
        checks.Single(item => item.Name.EndsWith("_State")).Sql.Should().Contain("[ChangeType] BETWEEN 0 AND 3");
    }

    [Fact]
    public void TriggerUpgradePreservesImmutablePayloadAndRequiresUnpublishedUntouchedSource()
    {
        var sql = string.Join("\n", Operations("Up").OfType<SqlOperation>().Select(item => item.Sql));
        sql.Should().Contain("recognized governed source variant")
            .And.Contain("d.PreviousOpeningScheduledAtUtc, d.NewOpeningScheduledAtUtc")
            .And.Contain("i.PreviousOpeningScheduledAtUtc, i.NewOpeningScheduledAtUtc")
            .And.Contain("t.Status <> ''Approved''")
            .And.Contain("t.PublishDate IS NOT NULL OR t.PublishedById IS NOT NULL")
            .And.Contain("t.RequiresPrequalification = 1 OR t.UseQCBSEvaluation = 1")
            .And.Contain("FROM TenderBids")
            .And.Contain("FROM ProcurementTenderControls")
            .And.Contain("FROM ProcurementTenderDocumentIssuances")
            .And.Contain("c.ChangeType IN (1, 3)")
            .And.Contain("t.Status = ''Published'' AND t.PublishDate IS NOT NULL")
            .And.Contain("r.Method = 0")
            .And.Contain("i.NewValueUtc <= COALESCE(i.DecidedAtUtc, i.RequestedAtUtc)")
            .And.NotContain("DISABLE TRIGGER")
            .And.NotContain("DELETE FROM");
    }

    [Fact]
    public void PublicationGuardRejectsStalePublisherWithoutBlockingLaterUnchangedPublicationStamps()
    {
        var sql = Operations("Up").OfType<SqlOperation>()
            .Single(item => item.Sql.Contains("CREATE OR ALTER TRIGGER [dbo].[TR_Tenders_ControlledDocumentPublicationGuard]")).Sql;
        sql.Should().Contain("AFTER UPDATE")
            .And.Contain("WHERE i.Status = 'Published'")
            .And.Contain("d.Status <> 'Published'")
            .And.Contain("ISNULL(i.PublishDate, '19000101') <> ISNULL(d.PublishDate, '19000101')")
            .And.Contain("ISNULL(i.PublishedById")
            .And.Contain("c.ChangeType IN (1, 3)")
            .And.Contain("c.NewOpeningScheduledAtUtc")
            .And.Contain("pending.Status = 0")
            .And.Contain("i.SubmissionDeadline <> COALESCE(dl.NewValueUtc, r.OriginalSubmissionDeadlineUtc)")
            .And.Contain("ISNULL(i.OpeningDate, '9999-12-31') <>")
            .And.NotContain("UPDATE Tenders");
    }

    [Fact]
    public void TriggerRewriteRecognizesSqlServersWhitespaceNormalizedCreateHeader()
    {
        var sql = string.Join("\n", Operations("Up").OfType<SqlOperation>().Select(item => item.Sql));
        sql.Should().Contain("@triggerPosition")
            .And.Contain("CHARINDEX(N'trigger', LOWER(@definition))")
            .And.Contain("@headerToken")
            .And.Contain("createoralter")
            .And.Contain("N'ALTER ' + SUBSTRING(@definition, @triggerPosition");
    }

    [Theory]
    [InlineData("CREATE TRIGGER ")]
    [InlineData("CREATE   TRIGGER ")]
    [InlineData("CREATE OR ALTER TRIGGER ")]
    [InlineData("ALTER TRIGGER ")]
    [InlineData("\r\n\t CREATE\t\r\nTRIGGER ")]
    [InlineData("\tCREATE  OR\t ALTER\r\nTRIGGER ")]
    public void SupportedTriggerHeadersNormalizeWithoutChangingGuardBody(string header)
    {
        const string suffix = "[TR_Test]\r\nON [Records] AFTER UPDATE AS BEGIN\r\nTHROW 51212, 'Protected history', 1;\r\nEND";
        NormalizeTriggerHeader(header + suffix).Should().Be("ALTER TRIGGER " + suffix);
    }

    [Theory]
    [InlineData("DROP TRIGGER [TR_Test]")]
    [InlineData("CREATE PROCEDURE [TR_Test] AS SELECT 'trigger'")]
    [InlineData("-- unrecognized prefix\r\nCREATE TRIGGER [TR_Test]")]
    public void UnrecognizedTriggerHeaderFailsClosed(string definition)
    {
        var action = () => NormalizeTriggerHeader(definition);
        action.Should().Throw<InvalidOperationException>();
    }

    // Mirrors the guarded SQL prefix normalization: SQL Server stores CREATE OR ALTER
    // triggers as CREATE   TRIGGER. Only the statement prefix may change, never the body.
    private static string NormalizeTriggerHeader(string definition)
    {
        var executable = definition.TrimStart(' ', '\t', '\r', '\n');
        var position = executable.IndexOf("trigger", StringComparison.OrdinalIgnoreCase);
        if (position < 0) throw new InvalidOperationException("Unsupported statement header.");
        var token = executable[..position].ToLowerInvariant()
            .Replace(" ", "").Replace("\t", "").Replace("\r", "").Replace("\n", "");
        if (token is not ("create" or "createoralter" or "alter"))
            throw new InvalidOperationException("Unsupported statement header.");
        return "ALTER " + executable[position..];
    }

    [Fact]
    public void RollbackDoesNotDeleteOrReinterpretEffectiveScheduleHistory()
    {
        var sql = Operations("Down").OfType<SqlOperation>().Should().ContainSingle().Which.Sql;
        sql.Should().Contain("THROW").And.Contain("forward-only").And.NotContain("DELETE");
    }

    private static IReadOnlyList<MigrationOperation> Operations(string direction)
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        typeof(AllowUnpublishedTenderScheduleReschedule).GetMethod(direction, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(new AllowUnpublishedTenderScheduleReschedule(), [builder]);
        return builder.Operations;
    }
}
