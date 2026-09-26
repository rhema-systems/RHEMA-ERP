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
}
