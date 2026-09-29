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

namespace ErpSystem.Api.Tests.Services.Procurement;

public sealed class BusinessPartnerLifecycleGovernanceTests
{
    [Fact]
    public async Task OrdinaryUpdate_CannotActivatePendingPartner()
    {
        var tenantId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"business-partner-lifecycle-{Guid.NewGuid():N}")
                .Options);
        var partner = new BusinessPartner
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerCode = "SUP-GOV-001",
            PartnerName = "Governed Supplier",
            PartnerType = "Supplier",
            RegistrationStatus = "PendingApproval",
            ApprovalStatus = "Pending",
            IsActive = false
        };
        db.BusinessPartners.Add(partner);
        await db.SaveChangesAsync();

        var current = new Mock<ICurrentUserProvider>();
        current.SetupGet(value => value.TenantId).Returns(tenantId);
        current.SetupGet(value => value.UserId).Returns(Guid.NewGuid());
        current.SetupGet(value => value.IsAuthenticated).Returns(true);
        var repository = new BusinessPartnerRepository(
            db, current.Object, NullLogger<BusinessPartnerRepository>.Instance);
        var service = new BusinessPartnerService(
            repository,
            Mock.Of<IBusinessPartnerContactRepository>(),
            current.Object,
            Mock.Of<IWorkflowIntegrationService>(),
            Mock.Of<IWorkflowStatusAdapterRegistry>(),
            Mock.Of<IPaymentTermRepository>(),
            NullLogger<BusinessPartnerService>.Instance,
            new UnitOfWork(db));

        var action = () => service.UpdateAsync(partner.Id, new UpdateBusinessPartnerDto
        {
            PartnerName = partner.PartnerName,
            Status = "Active"
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*lifecycle status cannot be changed through ordinary editing*");
        (await db.BusinessPartners.SingleAsync(value => value.Id == partner.Id))
            .RegistrationStatus.Should().Be("PendingApproval");
    }
}
