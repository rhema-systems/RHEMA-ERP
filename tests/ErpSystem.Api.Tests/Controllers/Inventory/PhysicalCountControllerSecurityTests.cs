using System.Reflection;
using ErpSystem.Api.Controllers.Inventory;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Inventory;

public sealed class PhysicalCountControllerSecurityTests
{
    [Fact]
    public void Controller_requires_internal_tenant_authentication()
    {
        var authorization = typeof(PhysicalCountsController).GetCustomAttributes<AuthorizeAttribute>(true).Single();
        authorization.Policy.Should().Be("InternalOnly");
        typeof(PhysicalCountsController).GetCustomAttribute<AllowAnonymousAttribute>(true).Should().BeNull();
    }

    [Theory]
    [InlineData(nameof(PhysicalCountsController.RecordRecount), "{id}/recount")]
    [InlineData(nameof(PhysicalCountsController.Review), "{id:guid}/review")]
    [InlineData(nameof(PhysicalCountsController.SubmitReviewed), "{id:guid}/submit")]
    [InlineData(nameof(PhysicalCountsController.DecideStores), "{id}/stores-decision")]
    [InlineData(nameof(PhysicalCountsController.DecideFinance), "{id}/finance-decision")]
    [InlineData(nameof(PhysicalCountsController.AttestAudit), "{id}/audit-attestation")]
    [InlineData(nameof(PhysicalCountsController.ControlledPost), "{id}/controlled-post")]
    [InlineData(nameof(PhysicalCountsController.GenerateCycleCounts), "cycle-schedules/generate")]
    public void Controlled_mutations_are_post_only_without_anonymous_override(string methodName, string route)
    {
        var method = typeof(PhysicalCountsController).GetMethod(methodName)!;
        method.GetCustomAttribute<HttpPostAttribute>()!.Template.Should().Be(route);
        method.GetCustomAttribute<AllowAnonymousAttribute>(true).Should().BeNull();
    }

    [Fact]
    public void Schedule_reads_and_writes_use_dedicated_tenant_safe_routes()
    {
        typeof(PhysicalCountsController).GetMethod(nameof(PhysicalCountsController.GetCycleSchedules))!
            .GetCustomAttribute<HttpGetAttribute>()!.Template.Should().Be("cycle-schedules");
        typeof(PhysicalCountsController).GetMethod(nameof(PhysicalCountsController.CreateCycleSchedule))!
            .GetCustomAttribute<HttpPostAttribute>()!.Template.Should().Be("cycle-schedules");
        typeof(PhysicalCountsController).GetMethod(nameof(PhysicalCountsController.UpdateCycleSchedule))!
            .GetCustomAttribute<HttpPutAttribute>()!.Template.Should().Be("cycle-schedules/{scheduleId:guid}");
    }

    [Fact]
    public void Stock_taking_evidence_uses_source_scoped_internal_multipart_routes()
    {
        var upload = typeof(PhysicalCountsController).GetMethod(nameof(PhysicalCountsController.UploadEvidence))!;
        upload.GetCustomAttribute<HttpPostAttribute>()!.Template.Should().Be("{id:guid}/evidence");
        upload.GetCustomAttribute<ConsumesAttribute>()!.ContentTypes.Should().ContainSingle("multipart/form-data");
        upload.GetCustomAttribute<AllowAnonymousAttribute>(true).Should().BeNull();

        var read = typeof(PhysicalCountsController).GetMethod(nameof(PhysicalCountsController.GetEvidence))!;
        read.GetCustomAttribute<HttpGetAttribute>()!.Template.Should().Be("{id:guid}/evidence");
        read.GetCustomAttribute<AllowAnonymousAttribute>(true).Should().BeNull();
    }
}
