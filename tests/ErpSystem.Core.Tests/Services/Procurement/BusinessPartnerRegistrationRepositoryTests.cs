using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Procurement;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class BusinessPartnerRegistrationRepositoryTests
{
    [Fact]
    public async Task MyRegistrationsIncludeOwnedAndActivePartnerLinkedRowsOnlyInCurrentTenant()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var applicantActorId = Guid.NewGuid();
        var linkedPartnerId = Guid.NewGuid();
        var inactivePartnerId = Guid.NewGuid();
        var databaseName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var owned = Registration(tenantId, "APP-OWNED", createdById: userId);
        var linked = Registration(
            tenantId,
            "APP-LINKED",
            createdById: applicantActorId,
            businessPartnerId: linkedPartnerId);
        var inactive = Registration(
            tenantId,
            "APP-INACTIVE",
            createdById: applicantActorId,
            businessPartnerId: inactivePartnerId);
        var crossTenant = Registration(
            otherTenantId,
            "APP-OTHER-TENANT",
            createdById: userId,
            businessPartnerId: Guid.NewGuid());

        await using (var seed = new ApplicationDbContext(options))
        {
            seed.BusinessPartnerRegistrations.AddRange(
                owned,
                linked,
                inactive,
                crossTenant);
            seed.BusinessPartnerUsers.AddRange(
                Link(tenantId, linkedPartnerId, userId, isActive: true),
                Link(tenantId, inactivePartnerId, userId, isActive: false),
                Link(otherTenantId, crossTenant.BusinessPartnerId!.Value, userId,
                    isActive: true));
            await seed.SaveChangesAsync();
        }

        await using var scoped = new ApplicationDbContext(options, tenantId);
        var repository = new BusinessPartnerRegistrationRepository(
            scoped,
            NullLogger<BusinessPartnerRegistrationRepository>.Instance);

        var results = (await repository.GetRegistrationsByUserAsync(userId))
            .ToList();

        results.Select(item => item.RegistrationNumber)
            .Should()
            .BeEquivalentTo(["APP-OWNED", "APP-LINKED"]);
        results.Should().OnlyContain(item => item.TenantId == tenantId);
    }

    private static BusinessPartnerRegistration Registration(
        Guid tenantId,
        string number,
        Guid createdById,
        Guid? businessPartnerId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RegistrationNumber = number,
            ApplicantName = number,
            PartnerType = "Supplier",
            Status = businessPartnerId.HasValue ? "Approved" : "Draft",
            BusinessPartnerId = businessPartnerId,
            CreatedById = createdById,
            CreatedAt = DateTime.UtcNow
        };

    private static BusinessPartnerUser Link(
        Guid tenantId,
        Guid businessPartnerId,
        Guid userId,
        bool isActive) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = businessPartnerId,
            UserId = userId,
            Role = "Admin",
            IsActive = isActive,
            GrantedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
}
