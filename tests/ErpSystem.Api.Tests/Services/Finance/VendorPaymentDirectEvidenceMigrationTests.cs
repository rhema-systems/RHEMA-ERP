using System.Text.RegularExpressions;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

/// <summary>Checks emitted operations, including the exact SQL text used for guarded replacement.</summary>
public sealed class VendorPaymentDirectEvidenceMigrationTests
{
    private static string Sql(Migration migration, string marker, bool down = false) =>
        (down ? migration.DownOperations : migration.UpOperations).OfType<SqlOperation>()
        .Select(operation => operation.Sql.Replace("\r", string.Empty)).Single(sql => sql.Contains(marker, StringComparison.Ordinal));

    private static string SqlLiteral(string sql, string variable)
    {
        var match = Regex.Match(sql, $@"DECLARE @{variable} nvarchar\(max\)=N'((?:''|[^'])*)';", RegexOptions.Singleline);
        match.Success.Should().BeTrue($"the emitted @{variable} literal is the reviewed replacement boundary");
        return match.Groups[1].Value.Replace("''", "'");
    }

    [Fact]
    public void Patch_ExactlyMatchesEmittedPriorGuard_AfterSqlLiteralUnescaping()
    {
        var original = Sql(new VendorPaymentOptionalApproval(), "CREATE OR ALTER TRIGGER dbo.TR_VendorPayment_OptionalApproval");
        var patch = Sql(new VendorPaymentDirectEvidence(), "DECLARE @definition");
        var before = SqlLiteral(patch, "before");
        var after = SqlLiteral(patch, "after");

        before.Should().Contain("\n" + new string(' ', 20) + "WHERE NULLIF");
        original.Split(before, StringSplitOptions.None).Should().HaveCount(2, "only the exact blanket evidence prohibition is replaced");
        patch.Should().Contain("<>1").And.Contain("THROW 51905").And.Contain("ALTER ");
        var changed = original.Replace(before, after, StringComparison.Ordinal);
        changed.Should().NotContain(before).And.Contain("TR_VendorPayment_DirectEvidence");
        changed.Should().Contain("$.requiresManagingDirectorApproval").And.Contain("$.signaturePolicy.isRequired");
        changed.Should().Contain("dbo.WorkflowApprovalRequiredAtSubmission").And.Contain("The payment no-approval source and policy snapshot are immutable");

        var downPatch = Sql(new VendorPaymentDirectEvidence(), "DECLARE @definition", down: true);
        SqlLiteral(downPatch, "before").Should().Be(after);
        SqlLiteral(downPatch, "after").Should().Be(before);
        changed.Replace(SqlLiteral(downPatch, "before"), SqlLiteral(downPatch, "after"), StringComparison.Ordinal)
            .Should().Be(original, "the narrow text replacement is reversible when no evidence history exists");
    }

    [Fact]
    public void NewTable_HasOnlyPaymentOwnedEvidenceAndNoHumanVerificationFields()
    {
        var table = new VendorPaymentDirectEvidence().UpOperations.OfType<CreateTableOperation>().Single();
        table.Name.Should().Be("VendorPaymentEvidenceLinks");
        table.Columns.Should().HaveCount(23);
        table.Columns.Select(column => column.Name).Should().BeEquivalentTo(new[]
        {
            "Id", "TenantId", "VendorPaymentId", "RequirementKey", "ClientRequestId", "RequestHash",
            "FileUploadRecordId", "CentralDocumentRecordId", "CentralDocumentVersionId", "FileName",
            "ContentType", "FileSize", "ChecksumSha256", "ExpiryDate", "CreatedAt", "UpdatedAt",
            "CreatedBy", "UpdatedBy", "CreatedById", "LastModifiedById", "IsDeleted", "DeletedAt", "DeletedBy"
        });
        table.Columns.Should().NotContain(column => column.Name.Contains("Verified", StringComparison.OrdinalIgnoreCase) ||
            column.Name.Contains("Approved", StringComparison.OrdinalIgnoreCase) || column.Name.Contains("WorkflowInstance", StringComparison.OrdinalIgnoreCase));
        table.Columns.Single(column => column.Name == "ExpiryDate").IsNullable.Should().BeTrue();
        foreach (var name in new[] { "RequestHash", "ChecksumSha256" })
        {
            var hash = table.Columns.Single(column => column.Name == name);
            hash.IsNullable.Should().BeFalse();
            hash.MaxLength.Should().Be(64);
        }
    }

    [Fact]
    public void Patch_NormalizesWindowsScriptLiteralLineEndingsAsWellAsStoredDefinition()
    {
        var original = Sql(new VendorPaymentOptionalApproval(), "CREATE OR ALTER TRIGGER dbo.TR_VendorPayment_OptionalApproval");
        var patch = Sql(new VendorPaymentDirectEvidence(), "DECLARE @definition");
        var windowsLiteral = SqlLiteral(patch.Replace("\n", "\r\n"), "before");
        original.Should().NotContain(windowsLiteral);
        original.Should().Contain(windowsLiteral.Replace("\r", string.Empty));
        foreach (var reverse in new[] { false, true })
        {
            var operation = Sql(new VendorPaymentDirectEvidence(), "DECLARE @definition", down: reverse);
            operation.Should().Contain("SET @before=REPLACE(@before,CHAR(13),N'')")
                .And.Contain("SET @after=REPLACE(@after,CHAR(13),N'')");
        }
    }

    [Fact]
    public void NewTable_HasFiveRestrictiveForeignKeysAndBothIdempotencyIndexes()
    {
        var operations = new VendorPaymentDirectEvidence().UpOperations;
        var table = operations.OfType<CreateTableOperation>().Single();
        table.ForeignKeys.Should().HaveCount(5);
        table.ForeignKeys.Should().OnlyContain(key => key.OnDelete == ReferentialAction.Restrict);
        table.ForeignKeys.Select(key => key.PrincipalTable).Should().BeEquivalentTo(new[]
        { "Tenants", "VendorPayment", "FileUploadRecords", "CentralDocumentRecords", "CentralDocumentVersions" });
        var indexes = operations.OfType<CreateIndexOperation>().Where(index => index.IsUnique).ToList();
        indexes.Should().HaveCount(2);
        indexes.Single(index => index.Name == "UX_VendorPaymentEvidenceLinks_Tenant_Request").Columns
            .Should().Equal("TenantId", "ClientRequestId");
        indexes.Single(index => index.Name == "UX_VendorPaymentEvidenceLinks_Tenant_Payment_Requirement_Hash").Columns
            .Should().Equal("TenantId", "VendorPaymentId", "RequirementKey", "ChecksumSha256");
    }

    [Fact]
    public void SourceLink_IsAppendOnlyAndBoundToCurrentNoWorkflowDirectDraft()
    {
        var sql = Sql(new VendorPaymentDirectEvidence(), "CREATE OR ALTER TRIGGER dbo.TR_VendorPaymentEvidenceLinks_SourceGuard");
        sql.Should().Contain("AFTER INSERT, UPDATE, DELETE").And.Contain("IF EXISTS (SELECT 1 FROM deleted)");
        sql.Should().Contain("p.TenantId=e.TenantId").And.Contain("p.Status=1").And.Contain("p.PaymentBatchId IS NULL");
        sql.Should().Contain("p.WorkflowInstanceId IS NULL").And.Contain("p.JournalEntryId IS NULL");
        sql.Should().Contain("dbo.WorkflowApprovalRequiredAtSubmission(p.TenantId,N'VendorPayment',p.Id)=0");
        sql.Should().Contain("LEN(e.RequestHash)<>64").And.Contain("LEN(e.ChecksumSha256)<>64");
        sql.Should().Contain("LIKE '%[^0-9A-Fa-f]%'").And.Contain("e.ExpiryDate<=SYSUTCDATETIME()");
    }

    [Theory]
    [InlineData("r.SourceModule=N'Finance'")]
    [InlineData("r.SourceEntityType=N'VendorPayment'")]
    [InlineData("r.IsDeleted=0 AND v.IsDeleted=0 AND f.IsDeleted=0")]
    [InlineData("f.StorageDeletedAtUtc IS NULL")]
    [InlineData("r.LifecycleStatus=N'Active'")]
    [InlineData("r.VersionStatus=N'Submitted' AND v.Status=N'Submitted'")]
    [InlineData("r.CurrentVersion=v.VersionNumber")]
    [InlineData("r.ExpiryDate>SYSUTCDATETIME()")]
    [InlineData("f.Category=N'document-management'")]
    [InlineData("v.RepositoryPath=f.FilePath")]
    [InlineData("f.VirusScanStatus=2 AND f.ScannedAtUtc IS NOT NULL")]
    [InlineData("f.UploadedByUserId=e.CreatedById AND v.CreatedByUserId=e.CreatedById")]
    [InlineData("f.FileSize=e.FileSize AND v.FileSize=e.FileSize")]
    [InlineData("f.ContentType=e.ContentType AND v.ContentType=e.ContentType")]
    [InlineData("f.OriginalFileName=e.FileName AND v.FileName=e.FileName")]
    public void InitialCaptureAndFirstPosting_BothCheckExactCleanCurrentOwnedDocument(string predicate)
    {
        var migration = new VendorPaymentDirectEvidence();
        Sql(migration, "CREATE OR ALTER TRIGGER dbo.TR_VendorPaymentEvidenceLinks_SourceGuard").Should().Contain(predicate);
        Sql(migration, "CREATE OR ALTER TRIGGER dbo.TR_VendorPayment_DirectEvidence").Should().Contain(predicate);
    }

    [Fact]
    public void FirstPostingGate_ShieldsNonDirectJsonAndRetainsMinimumDistinctDocumentCounts()
    {
        var sql = Sql(new VendorPaymentDirectEvidence(), "CREATE OR ALTER TRIGGER dbo.TR_VendorPayment_DirectEvidence");
        sql.Should().Contain("OPENJSON(CASE WHEN p.ApprovalRequired=0 AND ISJSON(p.ApprovalControlSnapshotJson)=1");
        sql.Should().Contain("THEN p.ApprovalControlSnapshotJson ELSE N'{}' END,'$.evidenceRequirements')");
        sql.Should().Contain("old.Id IS NULL OR old.ApprovalRequired=1 OR (old.JournalEntryId IS NULL AND p.JournalEntryId IS NOT NULL)");
        sql.Should().Contain("COUNT(DISTINCT e.ChecksumSha256)").And.Contain("e.VendorPaymentId=p.Id AND e.TenantId=p.TenantId");
        sql.Should().Contain("$.minimumDocuments").And.Contain("ELSE 1 END");
        sql.Should().NotContain("VerifiedBy").And.NotContain("VerifiedAt");
    }

    [Fact]
    public void Down_RefusesToDeleteRetainedEvidenceBeforeDroppingTableOrGuards()
    {
        var operations = new VendorPaymentDirectEvidence().DownOperations.ToList();
        var first = operations.OfType<SqlOperation>().First().Sql;
        first.Should().Contain("IF EXISTS (SELECT 1 FROM dbo.VendorPaymentEvidenceLinks)").And.Contain("THROW 51904");
        first.IndexOf("THROW 51904", StringComparison.Ordinal).Should().BeLessThan(first.IndexOf("DROP TRIGGER", StringComparison.Ordinal));
        operations.Last().Should().BeOfType<DropTableOperation>().Which.Name.Should().Be("VendorPaymentEvidenceLinks");
    }
}
