using ErpSystem.Api.Controllers.Ehc;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Ehc;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Ehc;

public sealed class EhcInternalTicketsAssignmentTests
{
    [Fact]
    public async Task Route_to_department_derives_the_assigning_owner_from_the_authenticated_user()
    {
        var ticketId = Guid.NewGuid();
        var organizationUnitId = Guid.NewGuid();
        var authenticatedUserId = Guid.NewGuid();
        var tickets = new Mock<IEhcTicketService>();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(value => value.UserId).Returns(authenticatedUserId.ToString());
        tickets.Setup(value => value.AssignTicketAsync(
                ticketId,
                authenticatedUserId,
                organizationUnitId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var controller = Controller(tickets.Object, currentUser.Object);

        var response = await controller.RouteToDepartment(ticketId, organizationUnitId, CancellationToken.None);

        response.Should().BeOfType<OkObjectResult>();
        tickets.VerifyAll();
    }

    [Fact]
    public async Task Route_to_department_rejects_a_missing_authenticated_user_context()
    {
        var tickets = new Mock<IEhcTicketService>(MockBehavior.Strict);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(value => value.UserId).Returns((string?)null);
        var controller = Controller(tickets.Object, currentUser.Object);

        var response = await controller.RouteToDepartment(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        response.Should().BeOfType<UnauthorizedObjectResult>();
        tickets.VerifyNoOtherCalls();
    }

    private static EhcInternalTicketsController Controller(
        IEhcTicketService tickets,
        ICurrentUserService currentUser)
        => new(
            tickets,
            Mock.Of<IEhcProblemService>(),
            currentUser,
            Mock.Of<ILogger<EhcInternalTicketsController>>());
}
