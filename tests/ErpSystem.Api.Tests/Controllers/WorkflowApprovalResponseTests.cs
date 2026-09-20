using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using ErpSystem.Api.Controllers;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Procedures;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers;

public sealed class WorkflowApprovalResponseTests
{
    [Theory]
    [InlineData(WorkflowApprovalAction.Approve, WorkflowApprovalStatus.Approved)]
    [InlineData(WorkflowApprovalAction.Reject, WorkflowApprovalStatus.Rejected)]
    public async Task ProcessedApprovalSerializesAsContractScalarsDespiteLoadedGraphCycles(
        WorkflowApprovalAction action, WorkflowApprovalStatus expectedStatus)
    {
        await using var db = CreateDb();
        var actorId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = actorId, UserName = "not-in-approval-response",
            PasswordHash = "never-serialize-user-security-data",
            SecurityStamp = "never-serialize-security-stamp",
            AuthenticatorKey = "never-serialize-authenticator"
        };
        var definition = new WorkflowDefinition { Id = Guid.NewGuid(), Name = "Shared approval" };
        var definitionStep = new WorkflowStep
        {
            Id = Guid.NewGuid(), WorkflowDefinition = definition,
            WorkflowDefinitionId = definition.Id, Name = "Approval", StepType = WorkflowStepType.Approval
        };
        definition.Steps.Add(definitionStep);
        var instance = new WorkflowInstance { WorkflowDefinition = definition };
        var step = new WorkflowStepInstance { WorkflowInstance = instance, WorkflowStep = definitionStep };
        var approval = new WorkflowApproval
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), StepInstanceId = step.Id,
            StepInstance = step, ApproverId = actorId, Approver = user, ProcessedBy = user,
            Status = WorkflowApprovalStatus.Pending,
            RequestedDate = new DateTime(2026, 9, 5, 18, 0, 0, DateTimeKind.Utc),
            DueDate = new DateTime(2026, 9, 6, 18, 0, 0, DateTimeKind.Utc)
        };
        step.Approvals.Add(approval);
        var options = MvcJsonOptions();
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(approval, options));

        var repository = new Mock<IWorkflowApprovalRepository>();
        repository.Setup(value => value.GetByIdAsync(approval.Id)).ReturnsAsync(approval);
        var engine = new Mock<IWorkflowEngine>();
        engine.Setup(value => value.ProcessStepAsync(step.Id, actorId,
                It.IsAny<WorkflowStepAction>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .Callback<Guid, Guid, WorkflowStepAction, object?, string?>((_, _, _, _, comments) =>
            {
                approval.Status = expectedStatus;
                approval.ProcessedDate = new DateTime(2026, 9, 5, 18, 41, 25, DateTimeKind.Utc);
                approval.Comments = comments;
            })
            .ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed });
        var controller = CreateController(db, actorId, repository.Object, engine.Object);

        var result = await controller.ProcessApproval(approval.Id,
            new ProcessApprovalRequest { Action = action, Comments = "Reviewed \"dates\"\nfor publication." });

        var response = Assert.IsType<OkObjectResult>(result.Result);
        var json = JsonSerializer.Serialize(response.Value, options);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean());
        var data = root.GetProperty("data");
        Assert.Equal(new[]
        {
            "approverId", "approverRole", "comments", "dueDate", "id", "processedDate",
            "requestedDate", "status", "stepInstanceId", "tenantId"
        }, data.EnumerateObject().Select(property => property.Name).OrderBy(name => name).ToArray());
        Assert.Equal(approval.Id, data.GetProperty("id").GetGuid());
        Assert.Equal(step.Id, data.GetProperty("stepInstanceId").GetGuid());
        Assert.Equal(actorId, data.GetProperty("approverId").GetGuid());
        Assert.Equal(approval.TenantId, data.GetProperty("tenantId").GetGuid());
        Assert.Equal(expectedStatus.ToString(), data.GetProperty("status").GetString());
        Assert.Equal(approval.RequestedDate, data.GetProperty("requestedDate").GetDateTime());
        Assert.Equal(approval.ProcessedDate, data.GetProperty("processedDate").GetDateTime());
        Assert.Equal(approval.DueDate, data.GetProperty("dueDate").GetDateTime());
        Assert.Equal(approval.Comments, data.GetProperty("comments").GetString());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("approverRole").ValueKind);
        Assert.DoesNotContain("never-serialize", json);
        Assert.DoesNotContain("not-in-approval-response", json);
        Assert.DoesNotContain("workflowDefinition", json);
        engine.Verify(value => value.ProcessStepAsync(step.Id, actorId,
            action == WorkflowApprovalAction.Approve ? WorkflowStepAction.Complete : WorkflowStepAction.Reject,
            It.IsAny<object?>(), approval.Comments), Times.Once);
    }

    [Fact]
    public void RoleAssignedApprovalKeepsOptionalContractFieldsWithoutNavigationProperties()
    {
        var response = new WorkflowApprovalResponse
        {
            Id = Guid.NewGuid(), StepInstanceId = Guid.NewGuid(), TenantId = Guid.NewGuid(),
            ApproverRole = "TDC_HEAD_OF_PROCUREMENT", Status = WorkflowApprovalStatus.Pending
        };
        var json = JsonSerializer.SerializeToElement(response, MvcJsonOptions());
        Assert.Equal("TDC_HEAD_OF_PROCUREMENT", json.GetProperty("approverRole").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("approverId").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("processedDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("dueDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.GetProperty("comments").ValueKind);
    }

    private static JsonSerializerOptions MvcJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase($"workflow-approval-response-{Guid.NewGuid()}").Options);

    private static WorkflowController CreateController(ApplicationDbContext db, Guid actorId,
        IWorkflowApprovalRepository repository, IWorkflowEngine engine)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(value => value.Roles).Returns(Array.Empty<string>());
        return new WorkflowController(
            engine, Mock.Of<IWorkflowService>(), Mock.Of<IWorkflowDefinitionService>(),
            Mock.Of<IWorkflowDefinitionRepository>(), Mock.Of<IWorkflowInstanceService>(),
            Mock.Of<IWorkflowStepService>(), Mock.Of<IWorkflowApprovalService>(),
            Mock.Of<IWorkflowConditionEvaluator>(), Mock.Of<IWorkflowNotificationService>(),
            Mock.Of<IWorkflowInstanceRepository>(), Mock.Of<IWorkflowStepInstanceRepository>(), repository,
            Mock.Of<IWorkflowEntityTypeRepository>(), Mock.Of<IWorkflowEntityTypeCatalogService>(), db,
            Mock.Of<IWorkflowStatusAdapterRegistry>(), Mock.Of<IAppEventBus>(), Mock.Of<IFileStorageService>(),
            currentUser.Object, Mock.Of<IProcurementRequisitionBudgetControlService>(),
            Mock.Of<IProcurementAccessControlService>(), Mock.Of<IProcedureCaseService>(),
            NullLogger<WorkflowController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, actorId.ToString())], "Test"))
                }
            }
        };
    }
}
