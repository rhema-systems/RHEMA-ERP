using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyEscalationDisputeGuardTests
{
    [Fact]
    public void Migration_is_narrow_discoverable_and_enforces_governed_lineage()
    {
        var root = FindRepositoryRoot();
        const string migrationId = "20260810005756_AddQuantitySurveyEscalationDisputes";
        var migration = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "LegacyMigrationsArchive", migrationId + ".cs"));
        var preflight = File.ReadAllText(Path.Combine(root, "scripts", "vps", "Invoke-RhemaVpsRemote.ps1"));

        migration.Should().Contain($"[Migration(\"{migrationId}\")]");
        Count(migration, "migrationBuilder.CreateTable(").Should().Be(3);
        Count(migration, "migrationBuilder.DropTable(").Should().Be(3);
        migration.Should().Contain("TR_QsEscalationDisputes_Guard");
        migration.Should().Contain("TR_QsEscalationDisputeAttachments_Guard");
        migration.Should().Contain("TR_QsEscalationDisputeRevisions_Guard");
        migration.Should().Contain("approved calculation, Works contract, contractor or separation-of-duties lineage is invalid");
        migration.Should().Contain("central-DMS lineage is invalid");
        migration.Should().Contain("revision history is append-only");
        migration.Should().Contain("i.[ResolvedById] = i.[OpenedById]");
        migration.Should().Contain("i.[ResolvedById] = i.[ContractorRespondedById]");
        migration.Should().Contain("quantity-survey-escalation-dispute-evidence");
        migration.Should().NotContain("EstateManagedAssets");
        migration.Should().NotContain("FinanceSettings");
        preflight.Should().Contain($"GUARD_COVERAGE|{migrationId}");
    }

    [Fact]
    public void Service_reuses_central_controls_and_enforces_retry_concurrency_and_sod()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "QuantitySurvey", "QuantitySurveyEscalationDisputeService.cs"));

        service.Should().Contain("IControlledFileUploadService");
        service.Should().Contain("ICentralDocumentRepositoryFileService");
        service.Should().Contain("IsolationLevel.Serializable");
        service.Should().Contain("value.ClientRequestId == request.ClientRequestId");
        service.Should().Contain("FixedEquals(existing.RequestHash, requestHash)");
        service.Should().Contain("ApplyRowVersion(entity, request.RowVersion)");
        service.Should().Contain("UserId == entity.OpenedById || UserId == entity.ContractorRespondedById");
        service.Should().Contain("MaximumAttachmentBytes = 10 * 1024 * 1024");
        service.Should().Contain("MaximumAttachments = 10");
        service.Should().Contain("FileVirusScanStatus.Clean");
        service.Should().Contain("QuantitySurveyEscalationDisputeEvidence");
        service.Should().NotContain("new Contract");
        service.Should().NotContain("new BusinessPartner");
    }

    [Fact]
    public void Audit_pack_uses_shared_document_output_and_retained_DMS_content()
    {
        var builder = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "ErpSystem.Api", "Services", "Documents", "QuantitySurvey", "QuantitySurveyEscalationDisputeAuditPackDocumentBuilder.cs"));

        builder.Should().Contain(": IDocumentBuilder");
        builder.Should().Contain("ICentralDocumentRepositoryFileService");
        builder.Should().Contain("application/pdf");
        builder.Should().Contain("application/zip");
        builder.Should().Contain("manifest.json");
        builder.Should().Contain("FixedEquals(checksum, attachment.ChecksumSha256)");
        builder.Should().Contain("50 MB audit-pack export limit");
    }

    [Fact]
    public void Frontend_reuses_command_ids_for_unchanged_retries()
    {
        var frontend = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "frontend", "src", "components", "quantity-survey", "QuantitySurveyEscalationDisputePanel.tsx"));

        frontend.Should().Contain("clientRequestId: openRequestId");
        frontend.Should().Contain("clientRequestId: responseRequestId");
        frontend.Should().Contain("clientRequestId: resolutionRequestId");
        frontend.Should().Contain("clientRequestId: attachmentRequestId");
        frontend.Should().Contain("setOpenRequestId(newId())");
        frontend.Should().Contain("setResponseRequestId(newId())");
        frontend.Should().Contain("setResolutionRequestId(newId())");
        frontend.Should().Contain("setAttachmentRequestId(newId())");
    }

    private static int Count(string value, string token) =>
        value.Split(token, StringSplitOptions.None).Length - 1;

    private static string FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        for (var directory = new FileInfo(sourceFile).Directory;
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ErpSystem.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Repository root containing ErpSystem.sln was not found.");
    }
}
