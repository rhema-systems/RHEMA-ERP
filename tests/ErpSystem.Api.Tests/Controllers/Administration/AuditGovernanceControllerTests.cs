using System.Reflection;
using ErpSystem.Api.Controllers.Administration;
using ErpSystem.Core.DTOs.Compliance;
using ErpSystem.Core.Interfaces.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Administration;

public sealed class AuditGovernanceControllerTests
{
    [Fact]
    public void Controller_is_internal_only_and_uses_shared_admin_route()
    {
        var controller = typeof(AuditGovernanceController);

        Assert.Equal(
            "InternalOnly",
            controller.GetCustomAttributes<AuthorizeAttribute>(true).Single().Policy);
        Assert.Equal(
            "api/admin/audit-governance",
            controller.GetCustomAttribute<RouteAttribute>()?.Template);
    }

    [Theory]
    [InlineData(nameof(AuditGovernanceController.GetCoverage), "AuditGovernanceRead")]
    [InlineData(nameof(AuditGovernanceController.Get), "AuditGovernanceRead")]
    [InlineData(nameof(AuditGovernanceController.PlaceLegalHold), "AuditGovernanceManage")]
    [InlineData(nameof(AuditGovernanceController.ReleaseLegalHold), "AuditGovernanceManage")]
    [InlineData(nameof(AuditGovernanceController.Archive), "AuditGovernanceManage")]
    [InlineData(nameof(AuditGovernanceController.Restore), "AuditGovernanceManage")]
    public void Endpoints_require_explicit_shared_governance_policies(
        string action,
        string expectedPolicy)
    {
        var policies = typeof(AuditGovernanceController).GetMethods()
            .Where(value => value.Name == action)
            .SelectMany(value => value.GetCustomAttributes<AuthorizeAttribute>(true))
            .Select(value => value.Policy)
            .ToList();

        Assert.Contains(expectedPolicy, policies);
    }

    [Fact]
    public async Task Read_delegates_store_and_record_to_tenant_safe_service()
    {
        var recordId = Guid.NewGuid();
        var expected = new AuditRecordGovernanceDto
        {
            StoreKey = "platform-audit-log",
            RecordId = recordId,
            RetentionDays = 2555,
            IsImmutable = true
        };
        var governance = new Mock<IAuditGovernanceService>(MockBehavior.Strict);
        governance.Setup(value => value.GetAsync("platform-audit-log", recordId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = Controller(governance);

        var result = await controller.Get("platform-audit-log", recordId, default);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, ok.Value);
        governance.VerifyAll();
    }

    [Fact]
    public async Task Missing_cross_tenant_record_is_not_disclosed()
    {
        var governance = new Mock<IAuditGovernanceService>();
        governance.Setup(value => value.GetAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AuditGovernanceNotFoundException("Not found in current tenant."));
        var controller = Controller(governance);

        var result = await controller.Get("platform-audit-log", Guid.NewGuid(), default);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Archive_command_is_delegated_without_mutating_the_source_record()
    {
        var recordId = Guid.NewGuid();
        var command = new AuditLifecycleCommandDto
        {
            Reason = "Move to statutory archive",
            RequestKey = "archive-contract-test"
        };
        var expected = new AuditRecordGovernanceDto
        {
            StoreKey = "procurement-inventory-control-event",
            RecordId = recordId,
            IsImmutable = true,
            IsArchived = true
        };
        var governance = new Mock<IAuditGovernanceService>(MockBehavior.Strict);
        governance.Setup(value => value.ArchiveAsync(
                "procurement-inventory-control-event", recordId, command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = Controller(governance);

        var result = await controller.Archive(
            "procurement-inventory-control-event", recordId, command, default);

        Assert.Same(expected, Assert.IsType<OkObjectResult>(result).Value);
        governance.VerifyAll();
    }

    private static AuditGovernanceController Controller(Mock<IAuditGovernanceService> governance)
    {
        var coverage = new Mock<IAuditEventCoverageService>();
        coverage.Setup(value => value.GetReport()).Returns(new AuditEventCoverageReportDto());
        return new AuditGovernanceController(governance.Object, coverage.Object);
    }
}
