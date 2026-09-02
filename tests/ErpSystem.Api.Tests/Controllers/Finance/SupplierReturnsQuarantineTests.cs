using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Finance;

public sealed class SupplierReturnsQuarantineTests
{
    [Fact]
    public void Create_ShouldReturnPlannedBoundaryConflictWithoutTrackingAMutation()
    {
        using var db = Context();
        var controller = new SupplierReturnsController(db, Mock.Of<ICurrentUserService>());

        var result = controller.Create(new CreateSupplierReturnDto(), CancellationToken.None);

        AssertPlannedConflict(result);
        db.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public void Approve_ShouldReturnPlannedBoundaryConflictWithoutTrackingAMutation()
    {
        using var db = Context();
        var controller = new SupplierReturnsController(db, Mock.Of<ICurrentUserService>());

        var result = controller.Approve(Guid.NewGuid(), CancellationToken.None);

        AssertPlannedConflict(result);
        db.ChangeTracker.Entries().Should().BeEmpty();
    }

    private static void AssertPlannedConflict(ActionResult<SupplierReturnDto> action)
    {
        var objectResult = action.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Post-acceptance supplier return is not available");
        problem.Extensions["code"].Should().Be("FIN-INT-012-013-PLANNED");
        problem.Detail.Should().Contain("No return, inventory movement, supplier debit note, tax adjustment, AP application, or journal was created.");
    }

    private static ApplicationDbContext Context()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"supplier-return-quarantine-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
