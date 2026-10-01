using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Estate;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Estate;

public sealed class EstatePropertyPortalSourceTests
{
    [Theory]
    [InlineData(EstateManagedAssetSourceType.Manual, false, true)]
    [InlineData(EstateManagedAssetSourceType.Imported, false, true)]
    [InlineData(EstateManagedAssetSourceType.ProjectUnit, true, true)]
    [InlineData(EstateManagedAssetSourceType.ProjectUnit, false, false)]
    [InlineData(EstateManagedAssetSourceType.LandAcquisition, false, false)]
    public void OnlyRegisteredImportedOrPublishedProjectPropertyCanEnterPortal(
        EstateManagedAssetSourceType sourceType,
        bool isPublishedFromProject,
        bool expected)
    {
        Assert.Equal(expected,
            EstateManagedAssetService.IsPropertyPortalSourceAllowed(sourceType, isPublishedFromProject));
    }
}
