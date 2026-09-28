using ErpSystem.Core.Services.Legal;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Legal;

public sealed class LegalProcedureCatalogServiceTests
{
    [Theory]
    [InlineData("LegalTransfer")]
    [InlineData("LegalAssignmentSubleaseVesting")]
    [InlineData("LegalTerminationRecognition")]
    [InlineData("LegalMortgage")]
    [InlineData("LegalCourtProcess")]
    public void MatterIntake_CanLinkPropertyAndApplicant(string entityType)
    {
        var workspace = new LegalProcedureCatalogService().GetProcedureWorkspace(entityType);

        workspace.Should().NotBeNull();
        workspace!.IntakeFields.Select(field => field.Key)
            .Should().Contain("estateManagedAssetId")
            .And.Contain("customerBusinessPartnerId");
    }

    [Fact]
    public void Workspaces_ExposeSopControlRegisters()
    {
        var service = new LegalProcedureCatalogService();

        foreach (var procedure in service.GetProcedures())
        {
            var workspace = service.GetProcedureWorkspace(procedure.EntityType);
            var fieldKeys = workspace!.IntakeFields.Select(field => field.Key).ToList();

            fieldKeys.Should().Contain("legalFileRegisterStatus");
            fieldKeys.Should().Contain("generatedDocumentStatus");
            fieldKeys.Should().Contain("signatureStatus");
            fieldKeys.Should().Contain("sealRegisterNumber");
            fieldKeys.Should().Contain("dispatchStatus");
            fieldKeys.Should().Contain("financeVerificationStatus");
            fieldKeys.Should().Contain("estateRecordsAmendmentStatus");
            fieldKeys.Should().Contain("landsCommissionHandoff");
        }
    }

    [Fact]
    public void CourtWorkspaces_ExposeCalendarAndLitigationControls()
    {
        var service = new LegalProcedureCatalogService();

        foreach (var entityType in new[] { "LegalCourtProcess", "LegalOtherCourtProcess" })
        {
            var workspace = service.GetProcedureWorkspace(entityType);
            var fieldKeys = workspace!.IntakeFields.Select(field => field.Key).ToList();

            fieldKeys.Should().Contain("courtName");
            fieldKeys.Should().Contain("caseNumber");
            fieldKeys.Should().Contain("responseDeadline");
            fieldKeys.Should().Contain("nextHearingDate");
            fieldKeys.Should().Contain("courtJacketMovementStatus");
            fieldKeys.Should().Contain("litigationExposureAmount");
        }
    }
}
