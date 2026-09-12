using System.Reflection;
using System.Text.Json;
using ErpSystem.Api.Controllers;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers;

public sealed class WorkflowEvidenceControllerTests
{
    [Fact]
    public void VerifyUsesAuthenticatedWorkflowAuthorityNotBusinessRoleAllowlist()
    {
        Assert.NotNull(typeof(WorkflowEvidenceController).GetCustomAttribute<AuthorizeAttribute>());
        Assert.Empty(typeof(WorkflowEvidenceController).GetMethod(nameof(WorkflowEvidenceController.Verify))!
            .GetCustomAttributes<AuthorizeAttribute>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AssignedTdcReviewerCanVerifyBeforeOrAfterTheirApproval(bool completed)
    {
        await using var fixture = await Fixture.Create(completed: completed);
        var result = await fixture.Controller.Verify(fixture.Evidence.Id, new() { Accepted = true }, default);
        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(WorkflowEvidenceVerificationStatus.Verified, fixture.Evidence.VerificationStatus);
        Assert.Equal(fixture.ActorId, fixture.Evidence.VerifiedById);
        Assert.Single(await fixture.Db.WorkflowActivityLogs.ToListAsync());
        var firstTime = fixture.Evidence.VerifiedAt;
        Assert.IsType<OkObjectResult>(await fixture.Controller.Verify(fixture.Evidence.Id, new() { Accepted = true }, default));
        Assert.Equal(firstTime, fixture.Evidence.VerifiedAt);
        Assert.Single(await fixture.Db.WorkflowActivityLogs.ToListAsync());
    }

    [Fact]
    public async Task ManagerAndStaleTdcClaimsDoNotGrantReview()
    {
        await using var fixture = await Fixture.Create(roleName: "Manager");
        fixture.CurrentUser.SetupGet(value => value.Roles).Returns(["Manager", "TDC_HEAD_OF_PROCUREMENT", "TenantAdmin"]);
        AssertProblem(await fixture.Controller.Verify(fixture.Evidence.Id, new() { Accepted = true }, default), 403,
            "WORKFLOW_EVIDENCE_REVIEW_FORBIDDEN");
        Assert.Equal(WorkflowEvidenceVerificationStatus.Pending, fixture.Evidence.VerificationStatus);
        Assert.Empty(await fixture.Db.WorkflowActivityLogs.ToListAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UploaderCannotReviewOwnFileEvenAsAdministrator(bool accepted)
    {
        await using var fixture = await Fixture.Create(roleName: "TenantAdmin");
        fixture.Evidence.UploadedById = fixture.ActorId;
        await fixture.Db.SaveChangesAsync();
        AssertProblem(await fixture.Controller.Verify(fixture.Evidence.Id, new() { Accepted = accepted, Notes = "Reason" }, default),
            409, "WORKFLOW_EVIDENCE_INDEPENDENT_REVIEW_REQUIRED");
        var read = Assert.IsType<OkObjectResult>(await fixture.Controller.GetStepEvidence(fixture.Evidence.StepInstanceId, default));
        var row = JsonSerializer.SerializeToElement(read.Value).GetProperty("data")[0];
        Assert.False(row.GetProperty("CanVerify").GetBoolean());
        Assert.Contains("different", row.GetProperty("VerificationBlockedReason").GetString());
    }

    [Fact]
    public async Task ForeignTenantEvidenceIsNotFound()
    {
        await using var fixture = await Fixture.Create();
        fixture.Evidence.TenantId = Guid.NewGuid();
        await fixture.Db.SaveChangesAsync();
        Assert.IsType<NotFoundResult>(await fixture.Controller.Verify(fixture.Evidence.Id, new() { Accepted = true }, default));
        Assert.Empty(await fixture.Db.WorkflowActivityLogs.ToListAsync());
    }

    [Fact]
    public async Task InactiveUserCannotUseEvenAdministrativeRole()
    {
        await using var fixture = await Fixture.Create(roleName: "TenantAdmin");
        (await fixture.Db.Users.SingleAsync()).IsActive = false;
        await fixture.Db.SaveChangesAsync();
        AssertProblem(await fixture.Controller.Verify(fixture.Evidence.Id, new() { Accepted = true }, default), 403,
            "WORKFLOW_EVIDENCE_REVIEW_FORBIDDEN");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SecondaryTenantMembershipHonorsExpiry(bool expired)
    {
        await using var fixture = await Fixture.Create();
        (await fixture.Db.Users.SingleAsync()).TenantId = Guid.NewGuid();
        fixture.Db.UserTenants.Add(new UserTenant
        {
            Id = Guid.NewGuid(), UserId = fixture.ActorId, TenantId = fixture.Evidence.TenantId,
            Status = UserTenantStatus.Active, GrantedAt = DateTime.UtcNow.AddDays(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(expired ? -1 : 1)
        });
        await fixture.Db.SaveChangesAsync();
        var result = await fixture.Controller.Verify(fixture.Evidence.Id, new() { Accepted = true }, default);
        if (expired) AssertProblem(result, 403, "WORKFLOW_EVIDENCE_REVIEW_FORBIDDEN");
        else Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ExactWorkflowLookupIsFilteredBeforeRecentPageLimitAndRemainsTenantScoped()
    {
        await using var fixture = await Fixture.Create();
        var exact = await fixture.Db.WorkflowInstances.SingleAsync();
        fixture.Db.WorkflowEntityTypes.Add(new WorkflowEntityType
        {
            Id = exact.EntityTypeId, TenantId = exact.TenantId, Code = "TEMPLATE", Name = "Template"
        });
        fixture.Db.WorkflowDefinitions.Add(new WorkflowDefinition
        {
            Id = exact.WorkflowDefinitionId, TenantId = exact.TenantId,
            EntityTypeId = exact.EntityTypeId, Name = "Template approval"
        });
        foreach (var step in await fixture.Db.WorkflowStepInstances.ToListAsync())
            fixture.Db.WorkflowSteps.Add(new WorkflowStep
            {
                Id = step.WorkflowStepId, TenantId = exact.TenantId,
                WorkflowDefinitionId = exact.WorkflowDefinitionId, Name = "Review"
            });
        for (var i = 0; i < 15; i++)
            fixture.Db.WorkflowInstances.Add(new WorkflowInstance
            {
                Id = Guid.NewGuid(), TenantId = exact.TenantId, EntityId = Guid.NewGuid(),
                WorkflowDefinitionId = exact.WorkflowDefinitionId, EntityTypeId = exact.EntityTypeId,
                InitiatedById = fixture.ActorId, CreatedDate = DateTime.UtcNow.AddDays(1)
            });
        await fixture.Db.SaveChangesAsync();
        var result = Assert.IsType<OkObjectResult>(await fixture.Controller.ReviewInstances(null, null, null,
            pageSize: 10, workflowInstanceId: exact.Id));
        var data = JsonSerializer.SerializeToElement(result.Value).GetProperty("data");
        Assert.Equal(1, data.GetArrayLength());
        Assert.Equal(exact.Id, data[0].GetProperty("id").GetGuid());
        exact.TenantId = Guid.NewGuid();
        await fixture.Db.SaveChangesAsync();
        var foreign = Assert.IsType<OkObjectResult>(await fixture.Controller.ReviewInstances(null, null, null,
            pageSize: 10, workflowInstanceId: exact.Id));
        Assert.Equal(0, JsonSerializer.SerializeToElement(foreign.Value).GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task ExplicitRevokedPrimaryTenantMappingOverridesLegacyPrimaryTenant()
    {
        await using var fixture = await Fixture.Create(roleName: "TenantAdmin");
        fixture.Db.UserTenants.Add(new UserTenant
        {
            Id = Guid.NewGuid(), UserId = fixture.ActorId, TenantId = fixture.Evidence.TenantId,
            Status = UserTenantStatus.Suspended, GrantedAt = DateTime.UtcNow.AddDays(-1)
        });
        await fixture.Db.SaveChangesAsync();
        AssertProblem(await fixture.Controller.Verify(fixture.Evidence.Id, new() { Accepted = true }, default), 403,
            "WORKFLOW_EVIDENCE_REVIEW_FORBIDDEN");
        var membership = await fixture.Db.UserTenants.IgnoreQueryFilters().SingleAsync();
        membership.Status = UserTenantStatus.Active;
        membership.IsDeleted = true;
        await fixture.Db.SaveChangesAsync();
        AssertProblem(await fixture.Controller.Verify(fixture.Evidence.Id, new() { Accepted = true }, default), 403,
            "WORKFLOW_EVIDENCE_REVIEW_FORBIDDEN");
    }

    [Fact]
    public async Task SupersededEvidenceCannotBeReviewed()
    {
        await using var fixture = await Fixture.Create();
        fixture.Evidence.IsCurrent = false;
        await fixture.Db.SaveChangesAsync();
        AssertProblem(await fixture.Controller.Verify(fixture.Evidence.Id, new() { Accepted = true }, default), 409,
            "WORKFLOW_EVIDENCE_VERSION_SUPERSEDED");
    }

    [Fact]
    public async Task RejectionRequiresReasonAndRetainsPendingStateOnFailure()
    {
        await using var fixture = await Fixture.Create();
        AssertProblem(await fixture.Controller.Verify(fixture.Evidence.Id, new() { Accepted = false }, default), 400,
            "WORKFLOW_EVIDENCE_REJECTION_REASON_REQUIRED");
        Assert.Equal(WorkflowEvidenceVerificationStatus.Pending, fixture.Evidence.VerificationStatus);
        Assert.IsType<OkObjectResult>(await fixture.Controller.Verify(fixture.Evidence.Id,
            new() { Accepted = false, Notes = "Incorrect content" }, default));
        Assert.Equal(WorkflowEvidenceVerificationStatus.Rejected, fixture.Evidence.VerificationStatus);
        Assert.Equal("Incorrect content", fixture.Evidence.VerificationNotes);
    }

    private static void AssertProblem(IActionResult result, int status, string code)
    {
        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, Assert.IsType<ProblemDetails>(response.Value).Extensions["code"]);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"workflow-evidence-{Guid.NewGuid()}").Options);
        public Guid ActorId { get; } = Guid.NewGuid();
        public WorkflowEvidenceDocument Evidence { get; private set; } = null!;
        public Mock<ICurrentUserService> CurrentUser { get; } = new();
        public WorkflowEvidenceController Controller { get; private set; } = null!;

        public static async Task<Fixture> Create(string roleName = "TDC_HEAD_OF_PROCUREMENT", bool completed = false)
        {
            var value = new Fixture();
            var tenantId = Guid.NewGuid();
            var instance = new WorkflowInstance
            {
                Id = Guid.NewGuid(), TenantId = tenantId, WorkflowDefinitionId = Guid.NewGuid(),
                EntityId = Guid.NewGuid(), EntityTypeId = Guid.NewGuid(), InitiatedById = Guid.NewGuid(),
                Status = completed ? WorkflowInstanceStatus.Completed : WorkflowInstanceStatus.InProgress
            };
            var submitted = new WorkflowStepInstance
            {
                Id = Guid.NewGuid(), TenantId = tenantId, WorkflowInstanceId = instance.Id,
                WorkflowStepId = Guid.NewGuid(), Status = WorkflowStepInstanceStatus.Completed
            };
            var review = new WorkflowStepInstance
            {
                Id = Guid.NewGuid(), TenantId = tenantId, WorkflowInstanceId = instance.Id,
                WorkflowStepId = Guid.NewGuid(),
                Status = completed ? WorkflowStepInstanceStatus.Completed : WorkflowStepInstanceStatus.InProgress
            };
            instance.CurrentStepId = completed ? null : review.WorkflowStepId;
            value.Evidence = new WorkflowEvidenceDocument
            {
                Id = Guid.NewGuid(), TenantId = tenantId, StepInstanceId = submitted.Id,
                UploadedById = instance.InitiatedById, AttachmentId = "uat-file", FileName = "test.pdf",
                FilePath = "protected/test.pdf", Sha256 = new string('A', 64), IsCurrent = true,
                MalwareScanStatus = WorkflowMalwareScanStatus.Clean
            };
            var role = new ApplicationRole(roleName) { Id = Guid.NewGuid(), NormalizedName = roleName.ToUpperInvariant() };
            value.Db.Users.Add(new ApplicationUser
            {
                Id = value.ActorId, TenantId = tenantId, IsActive = true, UserName = "reviewer",
                FirstName = "Test", LastName = "Reviewer", SecurityStamp = Guid.NewGuid().ToString()
            });
            value.Db.Roles.Add(role);
            value.Db.UserRoles.Add(new ApplicationUserRole { UserId = value.ActorId, RoleId = role.Id });
            value.Db.WorkflowInstances.Add(instance);
            value.Db.WorkflowStepInstances.AddRange(submitted, review);
            value.Db.WorkflowApprovals.Add(new WorkflowApproval
            {
                Id = Guid.NewGuid(), TenantId = tenantId, StepInstanceId = review.Id,
                ApproverRole = "TDC_HEAD_OF_PROCUREMENT",
                Status = completed ? WorkflowApprovalStatus.Approved : WorkflowApprovalStatus.Pending,
                ProcessedById = completed ? value.ActorId : null
            });
            value.Db.WorkflowEvidenceDocuments.Add(value.Evidence);
            await value.Db.SaveChangesAsync();
            value.CurrentUser.SetupGet(user => user.UserId).Returns(value.ActorId.ToString());
            value.CurrentUser.SetupGet(user => user.TenantId).Returns(tenantId);
            value.Controller = new WorkflowEvidenceController(value.Db, value.CurrentUser.Object, Mock.Of<IFileStorageService>())
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            return value;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
