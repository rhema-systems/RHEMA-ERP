using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementContractDocumentCatalogTests
{
    [Fact]
    public void ControlledContractUploadsSupportTheRequiredSignedCopyWithoutLosingOtherTypes()
    {
        var family = ProcurementDocumentManagementCatalog.Find(ProcurementDocumentFamily.Contract)!;
        family.Classifications.Should().BeEquivalentTo(new[]
        {
            "Contract", "SignedCopy", "Amendment", "Addendum", "Specification",
            "Certificate", "Invoice", "Receipt", "Correspondence", "Other"
        });
        family.UploadPermissions.Should().ContainSingle().Which.Should().Be("procurement.contract.manage");
    }
}
