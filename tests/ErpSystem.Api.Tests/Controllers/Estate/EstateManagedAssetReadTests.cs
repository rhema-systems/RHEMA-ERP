using System.Linq.Expressions;
using System.Text.Json;
using ErpSystem.Api.Controllers.Estate;
using ErpSystem.Api.Services.Estate;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Estate;
using ErpSystem.Core.Interfaces.Procedures;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Estate;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Estate;

public sealed class EstateManagedAssetReadTests
{
    [Fact]
    public async Task SingleReadFiltersByIdBeforePagingAndRetainsTenantAndDeletedVisibility()
    {
        var tenantId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenantId);
        var first = new EstateManagedAsset { Id = Guid.NewGuid(), TenantId = tenantId, AssetCode = "AAA", Name = "First" };
        var target = new EstateManagedAsset { Id = Guid.NewGuid(), TenantId = tenantId, AssetCode = "ZZZ", Name = "Requested" };
        var foreign = new EstateManagedAsset { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), AssetCode = "FOREIGN", Name = "Other tenant" };
        var deleted = new EstateManagedAsset { Id = Guid.NewGuid(), TenantId = tenantId, AssetCode = "DELETED", Name = "Deleted", IsDeleted = true };
        db.EstateManagedAssets.AddRange(first, target, foreign, deleted);
        await db.SaveChangesAsync();

        var unitOfWork = new Mock<IUnitOfWork>();
        // Remove context filters here to prove the owner read predicate itself enforces visibility.
        unitOfWork.Setup(item => item.Repository<EstateManagedAsset>()).Returns(Repository<EstateManagedAsset>(db));
        unitOfWork.Setup(item => item.Repository<EstateLandDemarcation>()).Returns(Repository<EstateLandDemarcation>(db));
        var provider = new Mock<ICurrentUserProvider>();
        provider.SetupGet(item => item.TenantId).Returns(tenantId);
        var owner = new EstateManagedAssetService(unitOfWork.Object, provider.Object);
        var controller = Controller(owner, db, tenantId);

        var result = Assert.IsType<OkObjectResult>(await controller.GetManagedAsset(target.Id));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        Assert.Equal(target.Id, json.RootElement.GetProperty("data").GetProperty("Id").GetGuid());
        Assert.IsType<NotFoundObjectResult>(await controller.GetManagedAsset(foreign.Id));
        Assert.IsType<NotFoundObjectResult>(await controller.GetManagedAsset(deleted.Id));
        Assert.IsType<NotFoundObjectResult>(await controller.GetManagedAsset(Guid.NewGuid()));
    }

    [Fact]
    public async Task MissingTenantIsDeniedBeforeOwnerRead()
    {
        var owner = new Mock<IEstateManagedAssetService>(MockBehavior.Strict);
        var controller = Controller(owner.Object, null!, null);
        Assert.IsType<ForbidResult>(await controller.GetManagedAsset(Guid.NewGuid()));
        owner.VerifyNoOtherCalls();
    }

    [Fact]
    public void SingleReadRequiresTheSameAuthenticationAsTheRegister()
    {
        Assert.NotEmpty(typeof(EstateManagedAssetsController).GetCustomAttributes(typeof(AuthorizeAttribute), true));
        var method = typeof(EstateManagedAssetsController).GetMethod(nameof(EstateManagedAssetsController.GetManagedAsset))!;
        Assert.Empty(method.GetCustomAttributes(typeof(AllowAnonymousAttribute), true));
    }

    private static IGenericRepository<T> Repository<T>(ApplicationDbContext db) where T : BaseEntity
    {
        var repository = new Mock<IGenericRepository<T>>();
        repository.Setup(item => item.GetQueryable(It.IsAny<Expression<Func<T, bool>>>()))
            .Returns((Expression<Func<T, bool>> predicate) => db.Set<T>().IgnoreQueryFilters().Where(predicate));
        return repository.Object;
    }

    private static EstateManagedAssetsController Controller(IEstateManagedAssetService owner, ApplicationDbContext db, Guid? tenantId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        return new EstateManagedAssetsController(owner, Mock.Of<IProjectService>(), Mock.Of<IProcedureCaseService>(),
            Mock.Of<IFileStorageService>(), db, currentUser.Object, Mock.Of<IEstateSalesListingApplicationHandoffService>());
    }
}
