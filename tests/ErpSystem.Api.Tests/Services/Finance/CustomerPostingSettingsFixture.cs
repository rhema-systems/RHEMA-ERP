using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Repositories;
using ErpSystem.Data.Repositories.Procurement;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ErpSystem.Api.Tests.Services.Finance;

internal static class CustomerPostingSettingsFixture
{
    // Exercise the same validation, persistence and DTO projection used by the account screen.
    // This fixture never opens an external database; callers supply their isolated test context.
    public static async Task<BusinessPartnerReceivablesDefaultsDto> SaveAndReloadAsync(
        ApplicationDbContext db, Guid tenantId, Guid partnerId,
        BusinessPartnerReceivablesDefaultsDto defaults)
    {
        var current = new Mock<ICurrentUserProvider>();
        current.SetupGet(x => x.TenantId).Returns(tenantId);
        current.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        current.SetupGet(x => x.IsAuthenticated).Returns(true);
        var access = new Mock<IProcurementAccessControlService>();
        access.Setup(x => x.EnforceCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true });
        var repository = new BusinessPartnerRepository(db, current.Object,
            NullLogger<BusinessPartnerRepository>.Instance);
        var service = new BusinessPartnerService(repository, Mock.Of<IBusinessPartnerContactRepository>(),
            current.Object, Mock.Of<IWorkflowIntegrationService>(), Mock.Of<IWorkflowStatusAdapterRegistry>(),
            Mock.Of<IPaymentTermRepository>(), NullLogger<BusinessPartnerService>.Instance,
            new UnitOfWork(db), access.Object);
        var partner = await db.Set<BusinessPartner>().SingleAsync(x => x.Id == partnerId);
        var saved = await service.UpdateAsync(partnerId, new UpdateBusinessPartnerDto
        {
            PartnerName = partner.PartnerName,
            ReceivablesDefaults = defaults
        });

        db.ChangeTracker.Clear();
        var reloaded = await db.Set<BusinessPartner>().SingleAsync(x => x.Id == partnerId);
        var readBack = BusinessPartnerReceivablesDefaults.FromPartner(reloaded);
        readBack.Should().BeEquivalentTo(saved.ReceivablesDefaults);
        return readBack;
    }
}
