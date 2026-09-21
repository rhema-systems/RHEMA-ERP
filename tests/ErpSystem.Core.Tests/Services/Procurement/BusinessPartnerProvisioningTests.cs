using System.Linq.Expressions;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public class BusinessPartnerProvisioningTests
{
    [Fact]
    public async Task LicenceCreatesTenantScopedRecordAndPreservesPartnerApproval()
    {
        var f = new Fixture();
        var result = await f.Licences.AddLicenseAsync(f.Partner.Id, f.Request());
        var saved = Assert.Single(f.LicenceRows);
        Assert.Equal(f.Tenant, saved.TenantId);
        Assert.Equal(f.Partner.Id, saved.BusinessPartnerId);
        Assert.Equal("Active", result.Status);
        Assert.Equal("Pending", f.Partner.ApprovalStatus);
        Assert.Equal(f.Actor, saved.CreatedById);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task LicenceRejectsNonAdminAndExternalActor(bool admin, bool external)
    {
        var f = new Fixture(); f.Admin = admin; f.External = external;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Licences.AddLicenseAsync(f.Partner.Id, f.Request()));
        Assert.Empty(f.LicenceRows);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LicenceRejectsCrossTenantPartnerOrType(bool partner)
    {
        var f = new Fixture();
        if (partner) f.Partner.TenantId = Guid.NewGuid(); else f.Type.TenantId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Licences.AddLicenseAsync(f.Partner.Id, f.Request()));
        Assert.Empty(f.LicenceRows);
    }

    [Fact]
    public async Task LicenceRejectsDuplicateAndReversedDates()
    {
        var f = new Fixture(); var request = f.Request();
        request.ExpiryDate = request.IssueDate.AddDays(-1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Licences.AddLicenseAsync(f.Partner.Id, request));
        await f.Licences.AddLicenseAsync(f.Partner.Id, f.Request());
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Licences.AddLicenseAsync(f.Partner.Id, f.Request()));
        Assert.Single(f.LicenceRows);
    }

    [Fact]
    public async Task ExpiredLicenceDoesNotBecomeCurrent()
    {
        var f = new Fixture(); var request = f.Request();
        request.IssueDate = DateTime.UtcNow.Date.AddDays(-10); request.ExpiryDate = DateTime.UtcNow.Date.AddDays(-1);
        Assert.Equal("Expired", (await f.Licences.AddLicenseAsync(f.Partner.Id, request)).Status);
    }

    [Fact]
    public async Task ExistingPortalLinkIsScopedAndRepeatDoesNotDuplicateOrChangeCredentials()
    {
        var f = new Fixture();
        var first = await f.Access.LinkExistingExternalUserAsync(f.Partner.Id, f.User.Id);
        var second = await f.Access.LinkExistingExternalUserAsync(f.Partner.Id, f.User.Id);
        Assert.Equal(first.Id, second.Id);
        var link = Assert.Single(f.Links);
        Assert.Equal("User", link.Role); Assert.Equal(f.Partner.Id, link.BusinessPartnerId);
        f.Manager.Verify(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        f.Manager.Verify(m => m.ResetPasswordAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LinkRequiresTenantAdministrator()
    {
        var f = new Fixture(); f.Admin = false;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => f.Access.LinkExistingExternalUserAsync(f.Partner.Id, f.User.Id));
        Assert.Empty(f.Links);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("inactive")]
    [InlineData("staff")]
    [InlineData("other-partner")]
    public async Task LinkRejectsIneligibleAccount(string reason)
    {
        var f = new Fixture();
        if (reason == "tenant") f.User.TenantId = Guid.NewGuid();
        if (reason == "inactive") f.User.IsActive = false;
        if (reason == "staff") f.Roles.Add("Employee");
        if (reason == "other-partner") f.Links.Add(new BusinessPartnerUser { Id = Guid.NewGuid(), TenantId = f.Tenant, UserId = f.User.Id, BusinessPartnerId = Guid.NewGuid(), IsActive = true });
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Access.LinkExistingExternalUserAsync(f.Partner.Id, f.User.Id));
        Assert.DoesNotContain(f.Links, l => l.BusinessPartnerId == f.Partner.Id);
    }

    private sealed class Fixture
    {
        public Guid Tenant = Guid.NewGuid(), Actor = Guid.NewGuid();
        public bool Admin = true, External;
        public BusinessPartner Partner;
        public LicenseType Type;
        public ApplicationUser User;
        public List<string> Roles = new() { "ExternalUser" };
        public List<BusinessPartnerLicense> LicenceRows = new();
        public List<BusinessPartnerUser> Links = new();
        public Mock<UserManager<ApplicationUser>> Manager;
        public BusinessPartnerService Licences;
        public BusinessPartnerUserService Access;
        public Fixture()
        {
            Partner = new() { Id = Guid.NewGuid(), TenantId = Tenant, ApprovalStatus = "Pending" };
            Type = new() { Id = Guid.NewGuid(), TenantId = Tenant, IsActive = true, LicenseName = "Test" };
            User = new() { Id = Guid.NewGuid(), TenantId = Tenant, IsActive = true, UserName = "external.test" };
            var unit = new Mock<IUnitOfWork>();
            unit.Setup(u => u.Repository<BusinessPartner>()).Returns(Repo(new List<BusinessPartner> { Partner }).Object);
            unit.Setup(u => u.Repository<LicenseType>()).Returns(Repo(new List<LicenseType> { Type }).Object);
            unit.Setup(u => u.Repository<BusinessPartnerLicense>()).Returns(Repo(LicenceRows).Object);
            unit.Setup(u => u.Repository<BusinessPartnerUser>()).Returns(Repo(Links).Object);
            unit.Setup(u => u.ExecuteInStrategyAsync(It.IsAny<Func<Task<BusinessPartnerLicenseDto>>>(), default))
                .Returns((Func<Task<BusinessPartnerLicenseDto>> run, CancellationToken _) => run());
            unit.Setup(u => u.ExecuteInStrategyAsync(It.IsAny<Func<Task<BusinessPartnerUserDto>>>(), default))
                .Returns((Func<Task<BusinessPartnerUserDto>> run, CancellationToken _) => run());
            var provider = new Mock<ICurrentUserProvider>();
            provider.SetupGet(p => p.IsAuthenticated).Returns(true); provider.SetupGet(p => p.TenantId).Returns(Tenant);
            provider.SetupGet(p => p.UserId).Returns(Actor); provider.SetupGet(p => p.IsExternalUser).Returns(() => External);
            provider.Setup(p => p.HasRole(It.IsAny<string>())).Returns(() => Admin);
            Licences = new(new Mock<IBusinessPartnerRepository>().Object, new Mock<IBusinessPartnerContactRepository>().Object,
                provider.Object, new Mock<IWorkflowIntegrationService>().Object, new Mock<IWorkflowStatusAdapterRegistry>().Object,
                new Mock<IPaymentTermRepository>().Object, NullLogger<BusinessPartnerService>.Instance, unit.Object);
            Manager = new(new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);
            Manager.Setup(m => m.FindByIdAsync(User.Id.ToString())).ReturnsAsync(User);
            Manager.Setup(m => m.GetRolesAsync(User)).ReturnsAsync(Roles);
            var tenant = new Mock<ITenantContext>(); tenant.Setup(t => t.GetCurrentTenantId()).Returns(Tenant);
            var current = new Mock<ICurrentUserService>(); current.SetupGet(c => c.IsAuthenticated).Returns(true);
            current.SetupGet(c => c.TenantId).Returns(Tenant); current.SetupGet(c => c.UserId).Returns(Actor.ToString());
            current.Setup(c => c.IsInRole(It.IsAny<string>())).Returns(() => Admin);
            var repository = new Mock<IBusinessPartnerUserRepository>();
            repository.Setup(r => r.CreateAsync(It.IsAny<BusinessPartnerUser>())).Returns((BusinessPartnerUser link) =>
            { link.User = User; link.BusinessPartner = Partner; Links.Add(link); return Task.FromResult(link); });
            repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Guid id) => Links.SingleOrDefault(l => l.Id == id));
            Access = new(repository.Object, new Mock<IUserService>().Object, Manager.Object, unit.Object, tenant.Object, current.Object, NullLogger<BusinessPartnerUserService>.Instance);
        }
        public CreateBusinessPartnerLicenseDto Request() => new() { LicenseTypeId = Type.Id, LicenseNumber = "UAT-NOT-REAL", IssuingAuthority = "Local UAT", IssueDate = DateTime.UtcNow.Date, ExpiryDate = DateTime.UtcNow.Date.AddDays(30) };
        private static Mock<IGenericRepository<T>> Repo<T>(List<T> rows) where T : BaseEntity
        {
            var mock = new Mock<IGenericRepository<T>>();
            mock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<T, bool>>>())).ReturnsAsync((Expression<Func<T, bool>> p) => rows.FirstOrDefault(p.Compile()));
            mock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<T, bool>>>())).ReturnsAsync((Expression<Func<T, bool>> p) => rows.Where(p.Compile()).ToList());
            mock.Setup(r => r.ExistsAsync(It.IsAny<Expression<Func<T, bool>>>())).ReturnsAsync((Expression<Func<T, bool>> p) => rows.Any(p.Compile()));
            mock.Setup(r => r.AddAsync(It.IsAny<T>())).Returns((T row) => { rows.Add(row); return Task.FromResult(row); });
            return mock;
        }
    }
}
