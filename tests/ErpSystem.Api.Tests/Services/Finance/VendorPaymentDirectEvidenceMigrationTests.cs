using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

/// <summary>Checks emitted operations, including the exact SQL text used for guarded replacement.</summary>
public sealed class VendorPaymentDirectEvidenceMigrationTests
{
    private const string OptionalMigration = "20260912190000_VendorPaymentOptionalApproval.cs";
    private const string EvidenceMigration = "20260912220000_VendorPaymentDirectEvidence.cs";
    private static string EvidenceSource => ArchivedMigrationSource.Read(EvidenceMigration);
    private static string Sql(string file, string marker, bool down = false)
    {
        var matches = ArchivedMigrationSource.SqlBlocks(file)
            .Select(sql => sql.Replace("\r", string.Empty))
            .Where(sql => sql.Contains(marker, StringComparison.Ordinal)).ToArray();
        matches.Should().NotBeEmpty();
        return down ? matches[^1] : matches[0];
    }

    private static string SqlLiteral(string sql, string variable)
    {
        var match = Regex.Match(sql, $@"DECLARE @{variable} nvarchar\(max\)=N'((?:''|[^'])*)';", RegexOptions.Singleline);
        match.Success.Should().BeTrue($"the emitted @{variable} literal is the reviewed replacement boundary");
        return match.Groups[1].Value.Replace("''", "'");
    }

    [Fact]
    public void Patch_ExactlyMatchesEmittedPriorGuard_AfterSqlLiteralUnescaping()
    {
        var original = Sql(OptionalMigration, "CREATE OR ALTER TRIGGER dbo.TR_VendorPayment_OptionalApproval");
        original.Should().Contain("WHERE NULLIF").And.Contain("dbo.WorkflowApprovalRequiredAtSubmission");
        EvidenceSource.Should().Contain("var before = remove ? clause : marker")
            .And.Contain("var after = remove ? marker : clause")
            .And.Contain("PatchBlanketEvidenceGuard(migrationBuilder, remove: true)")
            .And.Contain("PatchBlanketEvidenceGuard(migrationBuilder, remove: false)")
            .And.Contain("<>1").And.Contain("THROW 51905").And.Contain("N'ALTER '")
            .And.Contain("TR_VendorPayment_DirectEvidence");
        original.Should().Contain("$.requiresManagingDirectorApproval")
            .And.Contain("$.signaturePolicy.isRequired")
            .And.Contain("The payment no-approval source and policy snapshot are immutable");
    }

    [Fact]
    public void NewTable_HasOnlyPaymentOwnedEvidenceAndNoHumanVerificationFields()
    {
        var expectedColumns = new[]
        {
            "Id", "TenantId", "VendorPaymentId", "RequirementKey", "ClientRequestId", "RequestHash",
            "FileUploadRecordId", "CentralDocumentRecordId", "CentralDocumentVersionId", "FileName",
            "ContentType", "FileSize", "ChecksumSha256", "ExpiryDate", "CreatedAt", "UpdatedAt",
            "CreatedBy", "UpdatedBy", "CreatedById", "LastModifiedById", "IsDeleted", "DeletedAt", "DeletedBy"
        };
        EvidenceSource.Should().Contain("name: \"VendorPaymentEvidenceLinks\"");
        foreach (var column in expectedColumns) EvidenceSource.Should().Contain(column);
        EvidenceSource.Should().NotContain("VerifiedBy = table.Column").And.NotContain("VerifiedAt = table.Column")
            .And.NotContain("WorkflowInstanceId = table.Column").And.NotContain("ApprovedById = table.Column");
        EvidenceSource.Should().Contain("ExpiryDate = table.Column<DateTime>(type: \"datetime2\", nullable: true)")
            .And.Contain("RequestHash = table.Column<string>(type: \"nvarchar(64)\", maxLength: 64, nullable: false)")
            .And.Contain("ChecksumSha256 = table.Column<string>(type: \"nvarchar(64)\", maxLength: 64, nullable: false)");
    }

    [Fact]
    public void Patch_NormalizesWindowsScriptLiteralLineEndingsAsWellAsStoredDefinition()
    {
        EvidenceSource.Should().Contain("OBJECT_DEFINITION(OBJECT_ID(N'dbo.TR_VendorPayment_OptionalApproval')),CHAR(13),N'')")
            .And.Contain("SET @before=REPLACE(@before,CHAR(13),N'')")
            .And.Contain("SET @after=REPLACE(@after,CHAR(13),N'')");
    }

    [Fact]
    public void NewTable_HasFiveRestrictiveForeignKeysAndBothIdempotencyIndexes()
    {
        foreach (var principal in new[] { "Tenants", "VendorPayment", "FileUploadRecords", "CentralDocumentRecords", "CentralDocumentVersions" })
            EvidenceSource.Should().Contain($"\"{principal}\", \"Id\", onDelete: ReferentialAction.Restrict");
        EvidenceSource.Should().Contain("CreateIndex(\"UX_VendorPaymentEvidenceLinks_Tenant_Request\"")
            .And.Contain("new[] { \"TenantId\", \"ClientRequestId\" }")
            .And.Contain("CreateIndex(\"UX_VendorPaymentEvidenceLinks_Tenant_Payment_Requirement_Hash\"")
            .And.Contain("new[] { \"TenantId\", \"VendorPaymentId\", \"RequirementKey\", \"ChecksumSha256\" }");
    }

    [Fact]
    public void SourceLink_IsAppendOnlyAndBoundToCurrentNoWorkflowDirectDraft()
    {
        var sql = Sql(EvidenceMigration, "CREATE OR ALTER TRIGGER dbo.TR_VendorPaymentEvidenceLinks_SourceGuard");
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
        Sql(EvidenceMigration, "CREATE OR ALTER TRIGGER dbo.TR_VendorPaymentEvidenceLinks_SourceGuard").Should().Contain(predicate);
        Sql(EvidenceMigration, "CREATE OR ALTER TRIGGER dbo.TR_VendorPayment_DirectEvidence").Should().Contain(predicate);
    }

    [Fact]
    public void FirstPostingGate_ShieldsNonDirectJsonAndRetainsMinimumDistinctDocumentCounts()
    {
        var sql = Sql(EvidenceMigration, "CREATE OR ALTER TRIGGER dbo.TR_VendorPayment_DirectEvidence");
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
        var down = Sql(EvidenceMigration, "THROW 51904");
        down.Should().Contain("IF EXISTS (SELECT 1 FROM dbo.VendorPaymentEvidenceLinks)").And.Contain("DROP TRIGGER");
        down.IndexOf("THROW 51904", StringComparison.Ordinal).Should().BeLessThan(down.IndexOf("DROP TRIGGER", StringComparison.Ordinal));
        EvidenceSource.LastIndexOf("migrationBuilder.DropTable(\"VendorPaymentEvidenceLinks\")", StringComparison.Ordinal)
            .Should().BeGreaterThan(EvidenceSource.IndexOf("protected override void Down", StringComparison.Ordinal));
    }
}
