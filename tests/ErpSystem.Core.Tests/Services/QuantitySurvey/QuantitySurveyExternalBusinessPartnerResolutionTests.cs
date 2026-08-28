using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Procurement;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyExternalBusinessPartnerResolutionTests
{
    [Fact]
    public async Task Active_multi_user_link_resolves_partner_within_authenticated_tenant()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var context = CreateContext();
        var expected = NewPartner(tenantId, "QS-CONTRACTOR");
        var otherTenant = NewPartner(otherTenantId, "OTHER-CONTRACTOR");
        context.BusinessPartners.AddRange(expected, otherTenant);
        context.BusinessPartnerUsers.AddRange(
            NewLink(expected, userId, isActive: true),
            NewLink(otherTenant, userId, isActive: true));
        await context.SaveChangesAsync();
        var repository = CreateRepository(context, tenantId, userId);

        var resolved = await repository.GetByUserIdAsync(userId);

        resolved.Should().NotBeNull();
        resolved!.Id.Should().Be(expected.Id);
        (await repository.GetByIdAsync(expected.Id)).Should().NotBeNull();
        (await repository.GetByIdAsync(otherTenant.Id)).Should().BeNull();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Inactive_or_deleted_multi_user_link_does_not_grant_partner_access(
        bool isActive,
        bool isDeleted)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var context = CreateContext();
        var partner = NewPartner(tenantId, "BLOCKED-CONTRACTOR");
        context.BusinessPartners.Add(partner);
        var link = NewLink(partner, userId, isActive);
        link.IsDeleted = isDeleted;
        context.BusinessPartnerUsers.Add(link);
        await context.SaveChangesAsync();
        var repository = CreateRepository(context, tenantId, userId);

        (await repository.GetByUserIdAsync(userId)).Should().BeNull();
        (await repository.GetByIdAsync(partner.Id)).Should().BeNull();
    }

    private static ApplicationDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static BusinessPartnerRepository CreateRepository(
        ApplicationDbContext context,
        Guid tenantId,
        Guid userId)
    {
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(value => value.TenantId).Returns(tenantId);
        currentUser.SetupGet(value => value.UserId).Returns(userId);
        currentUser.SetupGet(value => value.IsExternalUser).Returns(true);
        return new BusinessPartnerRepository(
            context,
            currentUser.Object,
            NullLogger<BusinessPartnerRepository>.Instance);
    }

    private static BusinessPartner NewPartner(Guid tenantId, string code)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerCode = code,
            PartnerName = code,
            PartnerType = "Contractor",
            IsActive = true
        };

    private static BusinessPartnerUser NewLink(
        BusinessPartner partner,
        Guid userId,
        bool isActive)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = partner.TenantId,
            BusinessPartnerId = partner.Id,
            UserId = userId,
            Role = "User",
            IsActive = isActive
        };
}
