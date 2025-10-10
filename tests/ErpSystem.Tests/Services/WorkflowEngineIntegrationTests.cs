using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Moq;
using FluentAssertions;
using AutoFixture;
using AutoFixture.Xunit2;
using Xunit;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Workflow;

namespace ErpSystem.Tests.Services;

/// <summary>
/// Comprehensive integration tests for the workflow engine foundation:
/// Tests workflow definition creation, instance execution, approval flows, and conditional logic
/// </summary>
public class WorkflowEngineIntegrationTests : IDisposable
{
    private readonly Mock<IWorkflowDefinitionRepository> _mockDefinitionRepository;
    private readonly Mock<IWorkflowInstanceRepository> _mockInstanceRepository;
    private readonly Mock<IWorkflowStepRepository> _mockStepRepository;
    private readonly Mock<IWorkflowApprovalRepository> _mockApprovalRepository;
    private readonly Mock<IWorkflowActivityLogRepository> _mockActivityLogRepository;
    private readonly Mock<ILogger<WorkflowService>> _mockWorkflowLogger;
    private readonly Mock<ILogger<WorkflowEngineService>> _mockEngineLogger;
    private readonly Mock<ILogger<WorkflowExecutionService>> _mockExecutionLogger;
    private readonly Mock<ILogger<WorkflowApprovalService>> _mockApprovalLogger;
    private readonly Mock<ILogger<WorkflowConditionService>> _mockConditionLogger;

    private readonly WorkflowService _workflowService;
    private readonly WorkflowEngineService _engineService;
    private readonly WorkflowExecutionService _executionService;
    private readonly WorkflowApprovalService _approvalService;
    private readonly WorkflowConditionService _conditionService;

    private readonly Fixture _fixture;
    private readonly string _testTenantId = Guid.NewGuid().ToString();

    public WorkflowEngineIntegrationTests()
    {
        // Initialize mocks
        _mockDefinitionRepository = new Mock<IWorkflowDefinitionRepository>();
        _mockInstanceRepository = new Mock<IWorkflowInstanceRepository>();
        _mockStepRepository = new Mock<IWorkflowStepRepository>();
        _mockApprovalRepository = new Mock<IWorkflowApprovalRepository>();
        _mockActivityLogRepository = new Mock<IWorkflowActivityLogRepository>();
        _mockWorkflowLogger = new Mock<ILogger<WorkflowService>>();
        _mockEngineLogger = new Mock<ILogger<WorkflowEngineService>>();
        _mockExecutionLogger = new Mock<ILogger<WorkflowExecutionService>>();
        _mockApprovalLogger = new Mock<ILogger<WorkflowApprovalService>>();
        _mockConditionLogger = new Mock<ILogger<WorkflowConditionService>>();

        // Initialize services
        _conditionService = new WorkflowConditionService(
            _mockConditionLogger.Object);

        _approvalService = new WorkflowApprovalService(
            _mockApprovalRepository.Object,
            _mockInstanceRepository.Object,
            _mockActivityLogRepository.Object,
            _mockApprovalLogger.Object);

        _executionService = new WorkflowExecutionService(
            _mockInstanceRepository.Object,
            _mockStepRepository.Object,
            _mockActivityLogRepository.Object,
            _conditionService,
            _mockExecutionLogger.Object);

        _engineService = new WorkflowEngineService(
            _mockDefinitionRepository.Object,
            _mockInstanceRepository.Object,
            _executionService,
            _approvalService,
            _conditionService,
            _mockEngineLogger.Object);

        _workflowService = new WorkflowService(
            _mockDefinitionRepository.Object,
            _engineService,
            _mockWorkflowLogger.Object);

        // Initialize AutoFixture
        _fixture = new Fixture();
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    #region Complete Workflow Engine Integration Tests

    [Fact]
    public async Task WorkflowEngine_CompleteLifecycle_ShouldExecuteSuccessfully()
    {
        // Arrange - Create a complete workflow with multiple steps and approvals
        var testData = await SetupCompleteWorkflowTestData();
        var workflowDefinition = await CreateWorkflowDefinitionStep(testData);
        
        // Act & Assert - Execute complete workflow lifecycle
        
        // Step 1: Start workflow instance
        var workflowInstance = await StartWorkflowInstanceStep(testData, workflowDefinition);
        workflowInstance.Should().NotBeNull();
        workflowInstance.Status.Should().Be(WorkflowInstanceStatus.InProgress);
        
        // Step 2: Execute first step (automatic step)
        await ExecuteAutomaticStep(testData, workflowInstance);
        
        // Step 3: Execute approval step
        await ExecuteApprovalStep(testData, workflowInstance);
        
        // Step 4: Execute conditional step
        await ExecuteConditionalStep(testData, workflowInstance);
        
        // Step 5: Complete workflow
        await CompleteWorkflowStep(testData, workflowInstance);
        
        // Verify all interactions occurred as expected
        VerifyCompleteWorkflowExecution(testData);
    }

    [Fact]
    public async Task WorkflowEngine_WithConditionalLogic_ShouldFollowCorrectPath()
    {
        // Arrange - Setup workflow with conditional transitions
        var testData = await SetupConditionalWorkflowTestData();
        var workflowDefinition = await CreateConditionalWorkflowDefinition(testData);
        var workflowInstance = await StartWorkflowInstanceStep(testData, workflowDefinition);
        
        // Act - Execute workflow with condition that should branch to specific path
        testData.WorkflowData["priority"] = "high";
        testData.WorkflowData["amount"] = 15000m;
        
        var result = await _executionService.ExecuteNextStepAsync(
            workflowInstance.Id, testData.WorkflowData, testData.UserId);
        
        // Assert - Should follow high-priority path
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Status.Should().Be(WorkflowInstanceStatus.InProgress);
        
        // Verify the correct step was activated based on conditions
        var currentStep = await _mockStepRepository.Object.GetCurrentStepAsync(workflowInstance.Id);
        currentStep!.Name.Should().Be("Senior Manager Approval");
        
        // Verify activity log was created
        _mockActivityLogRepository.Verify(x => x.AddAsync(
            It.Is<WorkflowActivityLog>(log => 
                log.ActivityType == "StepTransition" && 
                log.Description.Contains("Condition evaluated: true"))), 
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task WorkflowEngine_WithApprovalRejection_ShouldHandleCorrectly()
    {
        // Arrange - Setup workflow with approval step
        var testData = await SetupApprovalWorkflowTestData();
        var workflowDefinition = await CreateApprovalWorkflowDefinition(testData);
        var workflowInstance = await StartWorkflowInstanceStep(testData, workflowDefinition);
        
        // Move to approval step
        await _executionService.ExecuteNextStepAsync(
            workflowInstance.Id, testData.WorkflowData, testData.UserId);
        
        // Act - Submit rejection
        var rejectionResult = await _approvalService.SubmitApprovalAsync(new SubmitApprovalDto
        {
            WorkflowInstanceId = workflowInstance.Id,
            StepId = testData.ApprovalStepId,
            Decision = "Rejected",
            Comment = "Insufficient documentation",
            Data = new Dictionary<string, object> { ["rejectionReason"] = "missing_docs" }
        }, testData.ApproverId);
        
        // Assert - Workflow should handle rejection appropriately
        rejectionResult.Should().NotBeNull();
        rejectionResult.Success.Should().BeTrue();
        rejectionResult.Status.Should().Be(WorkflowInstanceStatus.Rejected);
        
        // Verify rejection was logged
        _mockActivityLogRepository.Verify(x => x.AddAsync(
            It.Is<WorkflowActivityLog>(log => 
                log.ActivityType == "ApprovalSubmitted" && 
                log.Description.Contains("Rejected"))), 
            Times.Once);
        
        // Verify workflow instance status was updated
        _mockInstanceRepository.Verify(x => x.UpdateAsync(
            It.Is<WorkflowInstance>(wi => wi.Status == WorkflowInstanceStatus.Rejected)), 
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task WorkflowEngine_WithMultipleApprovals_ShouldRequireAllApprovers()
    {
        // Arrange - Setup workflow requiring multiple approvals
        var testData = await SetupMultiApprovalWorkflowTestData();
        var workflowDefinition = await CreateMultiApprovalWorkflowDefinition(testData);
        var workflowInstance = await StartWorkflowInstanceStep(testData, workflowDefinition);
        
        // Move to multi-approval step
        await _executionService.ExecuteNextStepAsync(
            workflowInstance.Id, testData.WorkflowData, testData.UserId);
        
        // Act - Submit first approval
        var firstApproval = await _approvalService.SubmitApprovalAsync(new SubmitApprovalDto
        {
            WorkflowInstanceId = workflowInstance.Id,
            StepId = testData.ApprovalStepId,
            Decision = "Approved",
            Comment = "First approver consent"
        }, testData.FirstApproverId);
        
        // Assert - Workflow should still be pending second approval
        firstApproval.Success.Should().BeTrue();
        firstApproval.Status.Should().Be(WorkflowInstanceStatus.InProgress);
        
        // Act - Submit second approval
        var secondApproval = await _approvalService.SubmitApprovalAsync(new SubmitApprovalDto
        {
            WorkflowInstanceId = workflowInstance.Id,
            StepId = testData.ApprovalStepId,
            Decision = "Approved",
            Comment = "Second approver consent"
        }, testData.SecondApproverId);
        
        // Assert - Workflow should now proceed to next step
        secondApproval.Success.Should().BeTrue();
        
        // Verify both approvals were recorded
        _mockApprovalRepository.Verify(x => x.AddAsync(It.IsAny<WorkflowApproval>()), Times.Exactly(2));
    }

    [Fact]
    public async Task WorkflowEngine_WithComplexConditions_ShouldEvaluateCorrectly()
    {
        // Arrange - Setup workflow with complex conditional logic
        var testData = await SetupComplexConditionalWorkflowTestData();
        var workflowDefinition = await CreateComplexConditionalWorkflowDefinition(testData);
        var workflowInstance = await StartWorkflowInstanceStep(testData, workflowDefinition);
        
        // Set up complex condition data
        testData.WorkflowData["amount"] = 25000m;
        testData.WorkflowData["department"] = "Engineering";
        testData.WorkflowData["urgency"] = "high";
        testData.WorkflowData["hasApprovals"] = true;
        
        // Act - Execute step with complex conditions
        var result = await _executionService.ExecuteNextStepAsync(
            workflowInstance.Id, testData.WorkflowData, testData.UserId);
        
        // Assert - Should evaluate complex AND/OR conditions correctly
        result.Success.Should().BeTrue();
        
        // Verify condition evaluation
        var conditionEvaluated = await _conditionService.EvaluateConditionAsync(
            testData.ComplexCondition, testData.WorkflowData);
        
        conditionEvaluated.Should().BeTrue();
        
        // Verify correct path was taken
        var currentStep = await _mockStepRepository.Object.GetCurrentStepAsync(workflowInstance.Id);
        currentStep!.Name.Should().Be("Executive Approval");
    }

    [Fact]
    public async Task WorkflowEngine_WithTimeoutEscalation_ShouldEscalateCorrectly()
    {
        // Arrange - Setup workflow with timeout and escalation
        var testData = await SetupEscalationWorkflowTestData();
        var workflowDefinition = await CreateEscalationWorkflowDefinition(testData);
        var workflowInstance = await StartWorkflowInstanceStep(testData, workflowDefinition);
        
        // Move to step with timeout
        await _executionService.ExecuteNextStepAsync(
            workflowInstance.Id, testData.WorkflowData, testData.UserId);
        
        // Simulate timeout by setting step start time in the past
        var currentStep = CreateTestWorkflowInstanceStep();
        currentStep.StartedAt = DateTime.UtcNow.AddHours(-25); // 25 hours ago
        currentStep.Status = WorkflowStepInstanceStatus.InProgress;
        
        _mockStepRepository.Setup(x => x.GetCurrentStepAsync(workflowInstance.Id))
            .ReturnsAsync(currentStep);
        
        // Act - Check for escalation
        var escalationResult = await _executionService.CheckAndHandleEscalationAsync(workflowInstance.Id);
        
        // Assert - Should trigger escalation
        escalationResult.Should().NotBeNull();
        escalationResult.EscalationTriggered.Should().BeTrue();
        escalationResult.EscalationActions.Should().NotBeEmpty();
        
        // Verify escalation was logged
        _mockActivityLogRepository.Verify(x => x.AddAsync(
            It.Is<WorkflowActivityLog>(log => 
                log.ActivityType == "Escalation" && 
                log.Description.Contains("timeout"))), 
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task WorkflowEngine_WithValidationErrors_ShouldPreventExecution()
    {
        // Arrange - Setup workflow with validation issues
        var testData = await SetupValidationErrorWorkflowTestData();
        var workflowDefinition = CreateInvalidWorkflowDefinition(testData);
        
        // Act - Attempt to start workflow with validation errors
        var exception = await Record.ExceptionAsync(async () =>
            await _engineService.StartWorkflowAsync(workflowDefinition.WorkflowKey, 
                testData.EntityId, testData.WorkflowData, testData.UserId, _testTenantId));
        
        // Assert - Should throw validation exception
        exception.Should().BeOfType<InvalidOperationException>();
        exception!.Message.Should().Contain("validation");
        
        // Verify no workflow instance was created
        _mockInstanceRepository.Verify(x => x.AddAsync(It.IsAny<WorkflowInstance>()), Times.Never);
    }

    #endregion

    #region Workflow Setup Methods

    private async Task<WorkflowEngineTestData> SetupCompleteWorkflowTestData()
    {
        var testData = new WorkflowEngineTestData
        {
            WorkflowKey = "complete-test-workflow",
            WorkflowName = "Complete Test Workflow",
            EntityType = "PurchaseOrder",
            EntityId = Guid.NewGuid().ToString(),
            UserId = Guid.NewGuid().ToString(),
            ApproverId = Guid.NewGuid().ToString(),
            WorkflowData = new Dictionary<string, object>
            {
                ["amount"] = 5000m,
                ["department"] = "Finance",
                ["priority"] = "normal"
            }
        };

        return testData;
    }

    private async Task<WorkflowEngineTestData> SetupConditionalWorkflowTestData()
    {
        var testData = await SetupCompleteWorkflowTestData();
        testData.WorkflowKey = "conditional-test-workflow";
        testData.WorkflowName = "Conditional Test Workflow";
        
        // Setup conditional step data
        testData.ConditionalStepId = Guid.NewGuid();
        testData.HighPriorityCondition = new WorkflowConditionDto
        {
            ConditionType = WorkflowConditionType.Expression,
            Expression = "priority == 'high' AND amount > 10000",
            LogicalOperator = WorkflowLogicalOperator.And
        };
        
        return testData;
    }

    private async Task<WorkflowEngineTestData> SetupApprovalWorkflowTestData()
    {
        var testData = await SetupCompleteWorkflowTestData();
        testData.WorkflowKey = "approval-test-workflow";
        testData.WorkflowName = "Approval Test Workflow";
        testData.ApprovalStepId = Guid.NewGuid();
        
        return testData;
    }

    private async Task<WorkflowEngineTestData> SetupMultiApprovalWorkflowTestData()
    {
        var testData = await SetupApprovalWorkflowTestData();
        testData.WorkflowKey = "multi-approval-workflow";
        testData.WorkflowName = "Multi Approval Workflow";
        testData.FirstApproverId = Guid.NewGuid().ToString();
        testData.SecondApproverId = Guid.NewGuid().ToString();
        
        return testData;
    }

    private async Task<WorkflowEngineTestData> SetupComplexConditionalWorkflowTestData()
    {
        var testData = await SetupCompleteWorkflowTestData();
        testData.WorkflowKey = "complex-conditional-workflow";
        testData.WorkflowName = "Complex Conditional Workflow";
        
        testData.ComplexCondition = new WorkflowConditionDto
        {
            ConditionType = WorkflowConditionType.Expression,
            LogicalOperator = WorkflowLogicalOperator.And,
            ChildConditions = new List<WorkflowConditionDto>
            {
                new()
                {
                    ConditionType = WorkflowConditionType.Expression,
                    Expression = "amount > 20000",
                    LogicalOperator = WorkflowLogicalOperator.Or
                },
                new()
                {
                    ConditionType = WorkflowConditionType.Expression,
                    Expression = "department == 'Engineering' AND urgency == 'high'",
                    LogicalOperator = WorkflowLogicalOperator.And
                }
            }
        };
        
        return testData;
    }

    private async Task<WorkflowEngineTestData> SetupEscalationWorkflowTestData()
    {
        var testData = await SetupCompleteWorkflowTestData();
        testData.WorkflowKey = "escalation-workflow";
        testData.WorkflowName = "Escalation Test Workflow";
        testData.TimeoutHours = 24;
        testData.EscalationTargetId = Guid.NewGuid().ToString();
        
        return testData;
    }

    private async Task<WorkflowEngineTestData> SetupValidationErrorWorkflowTestData()
    {
        var testData = await SetupCompleteWorkflowTestData();
        testData.WorkflowKey = "invalid-workflow";
        testData.WorkflowName = ""; // Invalid - empty name
        
        return testData;
    }

    private async Task<WorkflowDefinition> CreateWorkflowDefinitionStep(WorkflowEngineTestData testData)
    {
        var definition = CreateTestWorkflowDefinition(testData);
        
        _mockDefinitionRepository.Setup(x => x.GetByKeyAsync(testData.WorkflowKey, _testTenantId))
            .ReturnsAsync(definition);
        _mockDefinitionRepository.Setup(x => x.AddAsync(It.IsAny<WorkflowDefinition>()))
            .ReturnsAsync((WorkflowDefinition def) => { def.Id = 1; return def; });
        
        return definition;
    }

    private async Task<WorkflowDefinition> CreateConditionalWorkflowDefinition(WorkflowEngineTestData testData)
    {
        var definition = CreateTestWorkflowDefinition(testData);
        
        // Add conditional step
        var conditionalStep = CreateTestWorkflowStep();
        conditionalStep.Name = "Conditional Step";
        conditionalStep.StepType = "Conditional";
        conditionalStep.Order = 2;
        
        definition.Steps.Add(conditionalStep);
        
        _mockDefinitionRepository.Setup(x => x.GetByKeyAsync(testData.WorkflowKey, _testTenantId))
            .ReturnsAsync(definition);
        
        return definition;
    }

    private async Task<WorkflowDefinition> CreateApprovalWorkflowDefinition(WorkflowEngineTestData testData)
    {
        var definition = CreateTestWorkflowDefinition(testData);
        
        // Add approval step
        var approvalStep = CreateTestWorkflowStep();
        approvalStep.Name = "Manager Approval";
        approvalStep.StepType = "Approval";
        approvalStep.Order = 2;
        approvalStep.RequiresApproval = true;
        approvalStep.ApprovalRoles.Add(new WorkflowStepApprovalRole { RoleName = "Manager" });
        
        definition.Steps.Add(approvalStep);
        testData.ApprovalStepId = approvalStep.Id;
        
        _mockDefinitionRepository.Setup(x => x.GetByKeyAsync(testData.WorkflowKey, _testTenantId))
            .ReturnsAsync(definition);
        
        return definition;
    }

    private async Task<WorkflowDefinition> CreateMultiApprovalWorkflowDefinition(WorkflowEngineTestData testData)
    {
        var definition = await CreateApprovalWorkflowDefinition(testData);
        
        // Update approval step to require multiple approvals
        var approvalStep = definition.Steps.First(s => s.StepType == "Approval");
        approvalStep.ApprovalRoles.Add(new WorkflowStepApprovalRole { RoleName = "Senior Manager" });
        
        return definition;
    }

    private async Task<WorkflowDefinition> CreateComplexConditionalWorkflowDefinition(WorkflowEngineTestData testData)
    {
        var definition = CreateTestWorkflowDefinition(testData);
        
        // Add complex conditional step
        var conditionalStep = CreateTestWorkflowStep();
        conditionalStep.Name = "Complex Conditional Step";
        conditionalStep.StepType = "Conditional";
        conditionalStep.Order = 2;
        conditionalStep.Conditions = System.Text.Json.JsonSerializer.Serialize(testData.ComplexCondition);
        
        // Add executive approval step
        var execStep = CreateTestWorkflowStep();
        execStep.Name = "Executive Approval";
        execStep.StepType = "Approval";
        execStep.Order = 3;
        
        definition.Steps.AddRange(new[] { conditionalStep, execStep });
        
        _mockDefinitionRepository.Setup(x => x.GetByKeyAsync(testData.WorkflowKey, _testTenantId))
            .ReturnsAsync(definition);
        
        return definition;
    }

    private async Task<WorkflowDefinition> CreateEscalationWorkflowDefinition(WorkflowEngineTestData testData)
    {
        var definition = CreateTestWorkflowDefinition(testData);
        
        // Add step with timeout
        var timeoutStep = CreateTestWorkflowStep();
        timeoutStep.Name = "Timeout Step";
        timeoutStep.StepType = "Manual";
        timeoutStep.Order = 2;
        timeoutStep.TimeoutMinutes = testData.TimeoutHours * 60;
        
        definition.Steps.Add(timeoutStep);
        
        _mockDefinitionRepository.Setup(x => x.GetByKeyAsync(testData.WorkflowKey, _testTenantId))
            .ReturnsAsync(definition);
        
        return definition;
    }

    private WorkflowDefinition CreateInvalidWorkflowDefinition(WorkflowEngineTestData testData)
    {
        var definition = CreateTestWorkflowDefinition(testData);
        definition.Name = ""; // Invalid - empty name
        definition.Steps.Clear(); // Invalid - no steps
        
        return definition;
    }

    private async Task<WorkflowInstance> StartWorkflowInstanceStep(WorkflowEngineTestData testData, WorkflowDefinition definition)
    {
        var instance = CreateTestWorkflowInstance();
        instance.WorkflowDefinitionId = definition.Id;
        instance.EntityId = testData.EntityId;
        instance.InitiatedBy = testData.UserId;
        instance.TenantId = _testTenantId;
        
        _mockInstanceRepository.Setup(x => x.AddAsync(It.IsAny<WorkflowInstance>()))
            .ReturnsAsync((WorkflowInstance inst) => { inst.Id = 1; return inst; });
        _mockInstanceRepository.Setup(x => x.GetByIdAsync(instance.Id))
            .ReturnsAsync(instance);
        
        return instance;
    }

    #endregion

    #region Workflow Execution Methods

    private async Task ExecuteAutomaticStep(WorkflowEngineTestData testData, WorkflowInstance instance)
    {
        // Setup first step as automatic
        var firstStep = CreateTestWorkflowInstanceStep();
        firstStep.Status = WorkflowStepInstanceStatus.Completed;
        
        _mockStepRepository.Setup(x => x.GetCurrentStepAsync(instance.Id))
            .ReturnsAsync(firstStep);
        
        _mockActivityLogRepository.Setup(x => x.AddAsync(It.IsAny<WorkflowActivityLog>()))
            .ReturnsAsync((WorkflowActivityLog log) => { log.Id = 1; return log; });
    }

    private async Task ExecuteApprovalStep(WorkflowEngineTestData testData, WorkflowInstance instance)
    {
        var approvalStep = CreateTestWorkflowInstanceStep();
        approvalStep.Status = WorkflowStepInstanceStatus.WaitingForApproval;
        
        _mockStepRepository.Setup(x => x.GetCurrentStepAsync(instance.Id))
            .ReturnsAsync(approvalStep);
        
        var approval = CreateTestWorkflowApproval();
        approval.WorkflowInstanceId = instance.Id;
        approval.StepId = approvalStep.Id;
        approval.ApproverId = testData.ApproverId;
        
        _mockApprovalRepository.Setup(x => x.AddAsync(It.IsAny<WorkflowApproval>()))
            .ReturnsAsync((WorkflowApproval app) => { app.Id = 1; return app; });
    }

    private async Task ExecuteConditionalStep(WorkflowEngineTestData testData, WorkflowInstance instance)
    {
        var conditionalStep = CreateTestWorkflowInstanceStep();
        conditionalStep.Status = WorkflowStepInstanceStatus.InProgress;
        
        _mockStepRepository.Setup(x => x.GetCurrentStepAsync(instance.Id))
            .ReturnsAsync(conditionalStep);
    }

    private async Task CompleteWorkflowStep(WorkflowEngineTestData testData, WorkflowInstance instance)
    {
        instance.Status = WorkflowInstanceStatus.Completed;
        instance.CompletedAt = DateTime.UtcNow;
        
        _mockInstanceRepository.Setup(x => x.UpdateAsync(It.IsAny<WorkflowInstance>()))
            .ReturnsAsync((WorkflowInstance inst) => inst);
    }

    #endregion

    #region Entity Creation Helpers

    private WorkflowDefinition CreateTestWorkflowDefinition(WorkflowEngineTestData testData)
    {
        var definition = new WorkflowDefinition
        {
            Id = 1,
            Name = testData.WorkflowName,
            WorkflowKey = testData.WorkflowKey,
            EntityType = testData.EntityType,
            TriggerType = "Manual",
            IsActive = true,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            TenantId = _testTenantId,
            Steps = new List<WorkflowStep>()
        };

        // Add default initial step
        var initialStep = CreateTestWorkflowStep();
        initialStep.Name = "Start";
        initialStep.StepType = "Start";
        initialStep.Order = 1;
        definition.Steps.Add(initialStep);

        return definition;
    }

    private WorkflowStep CreateTestWorkflowStep()
    {
        return new WorkflowStep
        {
            Id = Guid.NewGuid(),
            Name = "Test Step",
            Description = "Test workflow step",
            StepType = "Manual",
            Order = 1,
            IsRequired = true,
            RequiresApproval = false,
            ApprovalRoles = new List<WorkflowStepApprovalRole>()
        };
    }

    private WorkflowInstance CreateTestWorkflowInstance()
    {
        return new WorkflowInstance
        {
            Id = 1,
            WorkflowDefinitionId = 1,
            EntityType = "TestEntity",
            EntityId = Guid.NewGuid().ToString(),
            Status = WorkflowInstanceStatus.InProgress,
            InitiatedBy = Guid.NewGuid().ToString(),
            CreatedAt = DateTime.UtcNow,
            TenantId = _testTenantId,
            Steps = new List<WorkflowInstanceStep>(),
            Approvals = new List<WorkflowApproval>()
        };
    }

    private WorkflowInstanceStep CreateTestWorkflowInstanceStep()
    {
        return new WorkflowInstanceStep
        {
            Id = 1,
            WorkflowInstanceId = 1,
            StepDefinitionId = Guid.NewGuid(),
            Name = "Test Step Instance",
            Status = WorkflowStepInstanceStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    private WorkflowApproval CreateTestWorkflowApproval()
    {
        return new WorkflowApproval
        {
            Id = 1,
            WorkflowInstanceId = 1,
            StepId = 1,
            ApproverId = Guid.NewGuid().ToString(),
            Status = WorkflowApprovalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    #endregion

    #region Verification Methods

    private void VerifyCompleteWorkflowExecution(WorkflowEngineTestData testData)
    {
        // Verify workflow instance was created and updated
        _mockInstanceRepository.Verify(x => x.AddAsync(It.IsAny<WorkflowInstance>()), Times.Once);
        _mockInstanceRepository.Verify(x => x.UpdateAsync(It.IsAny<WorkflowInstance>()), Times.AtLeastOnce);

        // Verify activity logs were created
        _mockActivityLogRepository.Verify(x => x.AddAsync(It.IsAny<WorkflowActivityLog>()), Times.AtLeastOnce);

        // Verify workflow definition was accessed
        _mockDefinitionRepository.Verify(x => x.GetByKeyAsync(testData.WorkflowKey, _testTenantId), Times.AtLeastOnce);
    }

    #endregion

    #region Test Data Classes

    private class WorkflowEngineTestData
    {
        public string WorkflowKey { get; set; } = string.Empty;
        public string WorkflowName { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string ApproverId { get; set; } = string.Empty;
        public string FirstApproverId { get; set; } = string.Empty;
        public string SecondApproverId { get; set; } = string.Empty;
        public string EscalationTargetId { get; set; } = string.Empty;
        public Dictionary<string, object> WorkflowData { get; set; } = new();
        public Guid ConditionalStepId { get; set; }
        public Guid ApprovalStepId { get; set; }
        public int TimeoutHours { get; set; }
        public WorkflowConditionDto? HighPriorityCondition { get; set; }
        public WorkflowConditionDto? ComplexCondition { get; set; }
    }

    #endregion

    public void Dispose()
    {
        // Cleanup resources if needed
    }
}