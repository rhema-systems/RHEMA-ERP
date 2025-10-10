using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Moq;
using FluentAssertions;
using AutoFixture;
using AutoFixture.Xunit2;
using Xunit;
using ErpSystem.Core.Services.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Enums;
using ErpSystem.Data;

namespace ErpSystem.Tests.Services;

/// <summary>
/// Integration tests for the complete maintenance workflow lifecycle:
/// Creation → Assignment → Parts Allocation → Execution → Completion with Quality Control
/// </summary>
public class MaintenanceWorkflowIntegrationTests : IDisposable
{
    private readonly Mock<IWorkOrderRepository> _mockWorkOrderRepository;
    private readonly Mock<IMaintenanceAssetRepository> _mockAssetRepository;
    // Note: Focusing on core workflow services that are already enabled
    // private readonly Mock<IMaintenanceInventoryRepository> _mockInventoryRepository;
    // private readonly Mock<IInventoryItemRepository> _mockItemRepository;
    private readonly Mock<IMaintenanceTypeRepository> _mockMaintenanceTypeRepository;
    private readonly Mock<IPriorityLevelRepository> _mockPriorityRepository;
    private readonly Mock<ErpSystem.Core.Interfaces.HR.IEmployeeRepository> _mockEmployeeRepository;
    private readonly Mock<ILogger<WorkOrderService>> _mockWorkOrderLogger;
    private readonly Mock<ILogger<MaintenanceInventoryService>> _mockInventoryLogger;
    private readonly Mock<ILogger<QualityControlService>> _mockQualityLogger;
    
    private readonly WorkOrderService _workOrderService;
    // private readonly MaintenanceInventoryService _inventoryService; // Not available in Phase 1.4
    private readonly QualityControlService _qualityControlService;
    
    private readonly Fixture _fixture;

    public MaintenanceWorkflowIntegrationTests()
    {
        // Initialize mocks
        _mockWorkOrderRepository = new Mock<IWorkOrderRepository>();
        _mockAssetRepository = new Mock<IMaintenanceAssetRepository>();
        _mockInventoryRepository = new Mock<IMaintenanceInventoryRepository>();
        _mockItemRepository = new Mock<IInventoryItemRepository>();
        _mockMaintenanceTypeRepository = new Mock<IMaintenanceTypeRepository>();
        _mockPriorityRepository = new Mock<IPriorityLevelRepository>();
        _mockEmployeeRepository = new Mock<ErpSystem.Core.Interfaces.HR.IEmployeeRepository>();
        _mockWorkOrderLogger = new Mock<ILogger<WorkOrderService>>();
        _mockInventoryLogger = new Mock<ILogger<MaintenanceInventoryService>>();
        _mockQualityLogger = new Mock<ILogger<QualityControlService>>();

        // Initialize services
        _workOrderService = new WorkOrderService(
            _mockWorkOrderRepository.Object,
            _mockAssetRepository.Object,
            _mockMaintenanceTypeRepository.Object,
            _mockPriorityRepository.Object,
            _mockEmployeeRepository.Object,
            _mockWorkOrderLogger.Object
        );

        _inventoryService = new MaintenanceInventoryService(
            _mockInventoryRepository.Object,
            _mockItemRepository.Object,
            _mockWorkOrderRepository.Object,
            _mockInventoryLogger.Object
        );

        _qualityControlService = new QualityControlService(
            _mockWorkOrderRepository.Object,
            _mockAssetRepository.Object,
            _mockEmployeeRepository.Object,
            _mockQualityLogger.Object
        );

        // Initialize AutoFixture
        _fixture = new Fixture();
        _fixture.Customize<DateOnly>(composer => composer.FromFactory<DateTime>(DateOnly.FromDateTime));
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    #region Complete Workflow Integration Tests

    [Fact]
    public async Task MaintenanceWorkflow_CompleteLifecycle_ShouldExecuteSuccessfully()
    {
        // Arrange - Setup complete maintenance workflow scenario
        var testData = await SetupCompleteWorkflowTestData();
        
        // Act & Assert - Execute complete workflow step by step
        
        // Step 1: Create Work Order
        var workOrder = await CreateWorkOrderStep(testData);
        workOrder.Should().NotBeNull();
        workOrder.Status.Should().Be("Created");
        
        // Step 2: Assign Technician
        await AssignTechnicianStep(testData, workOrder);
        workOrder.Status.Should().Be("Assigned");
        workOrder.AssignedTechnicianId.Should().Be(testData.TechnicianId);
        
        // Step 3: Allocate Parts
        await AllocatePartsStep(testData, workOrder);
        
        // Step 4: Start Work Order Execution
        await StartWorkOrderStep(testData, workOrder);
        workOrder.Status.Should().Be("InProgress");
        
        // Step 5: Complete Work Order with Quality Validation
        await CompleteWorkOrderWithQualityStep(testData, workOrder);
        
        // Verify all interactions occurred as expected
        VerifyWorkflowCompletionInteractions(testData);
    }

    [Fact]
    public async Task MaintenanceWorkflow_WithQualityControlFailure_ShouldBlockCompletion()
    {
        // Arrange - Setup scenario where quality control blocks completion
        var testData = await SetupQualityControlFailureTestData();
        var workOrder = await SetupInProgressWorkOrder(testData);

        // Act - Attempt to complete work order with quality issues
        var exception = await Record.ExceptionAsync(async () =>
            await _workOrderService.CompleteWorkOrderAsync(new CompleteWorkOrderDto
            {
                WorkOrderId = workOrder.Id,
                CompletionNotes = "Work completed but with quality issues",
                CompletedById = testData.TechnicianId,
                ActualHours = 4.5,
                PartsUsed = new List<WorkOrderPartUsageDto>()
            }));

        // Assert - Should throw exception due to quality control failure
        exception.Should().BeOfType<InvalidOperationException>();
        exception!.Message.Should().Contain("quality control");
        
        // Verify work order status was updated to pending quality approval
        _mockWorkOrderRepository.Verify(x => x.UpdateAsync(
            It.Is<WorkOrder>(w => w.Status == "PendingQualityApproval")), Times.Once);
    }

    [Fact]
    public async Task MaintenanceWorkflow_WithPartsShortage_ShouldHandleGracefully()
    {
        // Arrange - Setup scenario with insufficient parts
        var testData = await SetupPartsShortageTestData();
        var workOrder = await SetupAssignedWorkOrder(testData);

        // Act - Attempt to allocate more parts than available
        var exception = await Record.ExceptionAsync(async () =>
            await _inventoryService.AllocatePartsAsync(new AllocatePartsDto
            {
                WorkOrderId = workOrder.Id,
                Parts = testData.RequestedParts.Select(p => new AllocatePartDto
                {
                    ItemId = p.ItemId,
                    Quantity = p.Quantity + 100 // Request more than available
                }).ToList()
            }));

        // Assert - Should handle shortage appropriately
        exception.Should().BeOfType<InvalidOperationException>();
        exception!.Message.Should().Contain("insufficient quantity");
    }

    [Fact]
    public async Task MaintenanceWorkflow_WithEmergencyPriority_ShouldExpediteProcess()
    {
        // Arrange - Setup emergency priority work order
        var testData = await SetupEmergencyWorkOrderTestData();
        
        // Act - Create emergency work order
        var workOrder = await CreateEmergencyWorkOrderStep(testData);
        
        // Assert - Should be created with emergency status and priority
        workOrder.Should().NotBeNull();
        workOrder.PriorityLevel!.Name.Should().Be("Emergency");
        workOrder.PriorityLevel.Level.Should().Be(1);
        
        // Verify emergency escalation occurred
        _mockWorkOrderRepository.Verify(x => x.AddAsync(
            It.Is<WorkOrder>(w => 
                w.PriorityLevel!.Name == "Emergency" && 
                w.RequiresImmediateAttention == true)), Times.Once);
    }

    [Fact]
    public async Task MaintenanceWorkflow_WithSafetyRequirements_ShouldEnforceQualityControl()
    {
        // Arrange - Setup safety-critical work order
        var testData = await SetupSafetyWorkOrderTestData();
        var workOrder = await SetupInProgressWorkOrder(testData);

        // Act - Complete safety-critical work order
        var completionResult = await _workOrderService.CompleteWorkOrderAsync(new CompleteWorkOrderDto
        {
            WorkOrderId = workOrder.Id,
            CompletionNotes = "Safety work completed",
            CompletedById = testData.TechnicianId,
            ActualHours = 6.0,
            PartsUsed = new List<WorkOrderPartUsageDto>()
        });

        // Assert - Should require inspection officer approval
        completionResult.Should().NotBeNull();
        completionResult.QualityValidation.Should().NotBeNull();
        completionResult.QualityValidation!.RequiresInspectionOfficerApproval.Should().BeTrue();
        completionResult.QualityValidation.ValidationMessages.Should().Contain(m => 
            m.Contains("Safety") || m.Contains("inspection officer"));
    }

    #endregion

    #region Workflow Step Methods

    private async Task<WorkOrder> CreateWorkOrderStep(WorkflowTestData testData)
    {
        var createDto = new CreateWorkOrderDto
        {
            Title = testData.WorkOrderTitle,
            Description = testData.WorkOrderDescription,
            AssetId = testData.AssetId,
            MaintenanceTypeId = testData.MaintenanceTypeId,
            PriorityId = testData.PriorityId,
            EstimatedHours = testData.EstimatedHours,
            RequesterEmployeeId = testData.RequesterId
        };

        var workOrder = await _workOrderService.CreateWorkOrderAsync(createDto);
        
        // Verify creation interactions
        _mockWorkOrderRepository.Verify(x => x.AddAsync(It.IsAny<WorkOrder>()), Times.Once);
        _mockAssetRepository.Verify(x => x.GetByIdAsync(testData.AssetId), Times.Once);
        
        return workOrder;
    }

    private async Task AssignTechnicianStep(WorkflowTestData testData, WorkOrder workOrder)
    {
        await _workOrderService.AssignWorkOrderAsync(new AssignWorkOrderDto
        {
            WorkOrderId = workOrder.Id,
            TechnicianId = testData.TechnicianId,
            AssignedById = testData.AssignedById,
            EstimatedStartDate = testData.EstimatedStartDate,
            Notes = "Assigned based on availability and skills"
        });

        // Verify assignment interactions
        _mockEmployeeRepository.Verify(x => x.GetByIdAsync(testData.TechnicianId), Times.AtLeastOnce);
        _mockWorkOrderRepository.Verify(x => x.UpdateAsync(
            It.Is<WorkOrder>(w => w.AssignedTechnicianId == testData.TechnicianId)), Times.Once);
    }

    private async Task AllocatePartsStep(WorkflowTestData testData, WorkOrder workOrder)
    {
        var allocation = await _inventoryService.AllocatePartsAsync(new AllocatePartsDto
        {
            WorkOrderId = workOrder.Id,
            Parts = testData.RequestedParts.Select(p => new AllocatePartDto
            {
                ItemId = p.ItemId,
                Quantity = p.Quantity
            }).ToList()
        });

        allocation.Should().NotBeNull();
        allocation.AllocatedParts.Should().HaveCount(testData.RequestedParts.Count);

        // Verify allocation interactions
        _mockInventoryRepository.Verify(x => x.AddAsync(It.IsAny<MaintenanceInventoryTransaction>()), 
            Times.Exactly(testData.RequestedParts.Count));
    }

    private async Task StartWorkOrderStep(WorkflowTestData testData, WorkOrder workOrder)
    {
        await _workOrderService.StartWorkOrderAsync(new StartWorkOrderDto
        {
            WorkOrderId = workOrder.Id,
            StartedById = testData.TechnicianId,
            StartNotes = "Beginning maintenance work"
        });

        // Verify start interactions
        _mockWorkOrderRepository.Verify(x => x.UpdateAsync(
            It.Is<WorkOrder>(w => w.Status == "InProgress")), Times.Once);
    }

    private async Task CompleteWorkOrderWithQualityStep(WorkflowTestData testData, WorkOrder workOrder)
    {
        var completionResult = await _workOrderService.CompleteWorkOrderAsync(new CompleteWorkOrderDto
        {
            WorkOrderId = workOrder.Id,
            CompletionNotes = "Work completed successfully",
            CompletedById = testData.TechnicianId,
            ActualHours = testData.ActualHours,
            PartsUsed = testData.PartsUsed
        });

        // Verify completion and quality control
        completionResult.Should().NotBeNull();
        completionResult.QualityValidation.Should().NotBeNull();
        
        if (testData.RequiresQualityApproval)
        {
            completionResult.QualityValidation!.RequiresInspectionOfficerApproval.Should().BeTrue();
        }
        else
        {
            completionResult.Success.Should().BeTrue();
            workOrder.Status.Should().Be("Completed");
        }
    }

    private async Task<WorkOrder> CreateEmergencyWorkOrderStep(WorkflowTestData testData)
    {
        var createDto = new CreateWorkOrderDto
        {
            Title = "EMERGENCY: " + testData.WorkOrderTitle,
            Description = testData.WorkOrderDescription,
            AssetId = testData.AssetId,
            MaintenanceTypeId = testData.MaintenanceTypeId,
            PriorityId = testData.EmergencyPriorityId,
            EstimatedHours = testData.EstimatedHours,
            RequesterEmployeeId = testData.RequesterId,
            IsEmergency = true
        };

        return await _workOrderService.CreateWorkOrderAsync(createDto);
    }

    #endregion

    #region Setup Methods

    private async Task<WorkflowTestData> SetupCompleteWorkflowTestData()
    {
        var testData = new WorkflowTestData
        {
            WorkOrderTitle = "Preventive Maintenance - HVAC System",
            WorkOrderDescription = "Annual maintenance of HVAC system including filter replacement and calibration",
            AssetId = Guid.NewGuid(),
            MaintenanceTypeId = Guid.NewGuid(),
            PriorityId = Guid.NewGuid(),
            TechnicianId = Guid.NewGuid(),
            RequesterId = Guid.NewGuid(),
            AssignedById = Guid.NewGuid(),
            EstimatedHours = 4.0,
            ActualHours = 4.5,
            EstimatedStartDate = DateTime.UtcNow.AddHours(2),
            RequiresQualityApproval = false
        };

        // Setup required entities and mocks
        await SetupBasicEntitiesForWorkflow(testData);
        SetupSuccessfulQualityControl(testData);
        
        return testData;
    }

    private async Task<WorkflowTestData> SetupQualityControlFailureTestData()
    {
        var testData = await SetupCompleteWorkflowTestData();
        testData.RequiresQualityApproval = true;
        
        // Setup asset in critical condition to trigger quality control failure
        var asset = CreateTestAsset(testData.AssetId);
        asset.Status = AssetStatus.OutOfService;
        _mockAssetRepository.Setup(x => x.GetByIdAsync(testData.AssetId))
            .ReturnsAsync(asset);
        
        return testData;
    }

    private async Task<WorkflowTestData> SetupPartsShortageTestData()
    {
        var testData = await SetupCompleteWorkflowTestData();
        
        // Setup inventory items with limited quantities
        testData.RequestedParts = new List<RequestedPart>
        {
            new() { ItemId = Guid.NewGuid(), Quantity = 10 },
            new() { ItemId = Guid.NewGuid(), Quantity = 5 }
        };

        // Mock insufficient inventory
        foreach (var part in testData.RequestedParts)
        {
            var inventoryItem = CreateTestInventoryItem(part.ItemId);
            inventoryItem.CurrentStock = part.Quantity - 1; // Less than requested
            
            _mockItemRepository.Setup(x => x.GetByIdAsync(part.ItemId))
                .ReturnsAsync(inventoryItem);
        }
        
        return testData;
    }

    private async Task<WorkflowTestData> SetupEmergencyWorkOrderTestData()
    {
        var testData = await SetupCompleteWorkflowTestData();
        testData.EmergencyPriorityId = Guid.NewGuid();
        
        // Setup emergency priority level
        var emergencyPriority = CreateTestPriorityLevel(testData.EmergencyPriorityId);
        emergencyPriority.Name = "Emergency";
        emergencyPriority.Level = 1;
        
        _mockPriorityRepository.Setup(x => x.GetByIdAsync(testData.EmergencyPriorityId))
            .ReturnsAsync(emergencyPriority);
            
        return testData;
    }

    private async Task<WorkflowTestData> SetupSafetyWorkOrderTestData()
    {
        var testData = await SetupCompleteWorkflowTestData();
        testData.RequiresQualityApproval = true;
        
        // Setup safety maintenance type
        var maintenanceType = CreateTestMaintenanceType(testData.MaintenanceTypeId);
        maintenanceType.Name = "Safety";
        maintenanceType.RequiresApproval = true;
        
        _mockMaintenanceTypeRepository.Setup(x => x.GetByIdAsync(testData.MaintenanceTypeId))
            .ReturnsAsync(maintenanceType);
            
        return testData;
    }

    private async Task SetupBasicEntitiesForWorkflow(WorkflowTestData testData)
    {
        // Setup Asset
        var asset = CreateTestAsset(testData.AssetId);
        _mockAssetRepository.Setup(x => x.GetByIdAsync(testData.AssetId))
            .ReturnsAsync(asset);

        // Setup Maintenance Type
        var maintenanceType = CreateTestMaintenanceType(testData.MaintenanceTypeId);
        _mockMaintenanceTypeRepository.Setup(x => x.GetByIdAsync(testData.MaintenanceTypeId))
            .ReturnsAsync(maintenanceType);

        // Setup Priority Level
        var priority = CreateTestPriorityLevel(testData.PriorityId);
        _mockPriorityRepository.Setup(x => x.GetByIdAsync(testData.PriorityId))
            .ReturnsAsync(priority);

        // Setup Employees
        var technician = CreateTestEmployee(testData.TechnicianId);
        var requester = CreateTestEmployee(testData.RequesterId);
        var assigner = CreateTestEmployee(testData.AssignedById);

        _mockEmployeeRepository.Setup(x => x.GetByIdAsync(testData.TechnicianId))
            .ReturnsAsync(technician);
        _mockEmployeeRepository.Setup(x => x.GetByIdAsync(testData.RequesterId))
            .ReturnsAsync(requester);
        _mockEmployeeRepository.Setup(x => x.GetByIdAsync(testData.AssignedById))
            .ReturnsAsync(assigner);

        // Setup Parts
        testData.RequestedParts = new List<RequestedPart>
        {
            new() { ItemId = Guid.NewGuid(), Quantity = 2 },
            new() { ItemId = Guid.NewGuid(), Quantity = 1 }
        };

        testData.PartsUsed = testData.RequestedParts.Select(p => new WorkOrderPartUsageDto
        {
            ItemId = p.ItemId,
            QuantityUsed = p.Quantity,
            UnitCost = 25.00m
        }).ToList();

        foreach (var part in testData.RequestedParts)
        {
            var inventoryItem = CreateTestInventoryItem(part.ItemId);
            _mockItemRepository.Setup(x => x.GetByIdAsync(part.ItemId))
                .ReturnsAsync(inventoryItem);
        }

        // Setup Work Order Repository responses
        _mockWorkOrderRepository.Setup(x => x.AddAsync(It.IsAny<WorkOrder>()))
            .ReturnsAsync((WorkOrder wo) => { wo.Id = Guid.NewGuid(); return wo; });

        _mockWorkOrderRepository.Setup(x => x.UpdateAsync(It.IsAny<WorkOrder>()))
            .ReturnsAsync((WorkOrder wo) => wo);
    }

    private void SetupSuccessfulQualityControl(WorkflowTestData testData)
    {
        var workOrder = CreateTestWorkOrder();
        workOrder.AssetId = testData.AssetId;
        workOrder.AssignedTechnicianId = testData.TechnicianId;

        _mockWorkOrderRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(workOrder);
    }

    private async Task<WorkOrder> SetupAssignedWorkOrder(WorkflowTestData testData)
    {
        var workOrder = CreateTestWorkOrder();
        workOrder.AssetId = testData.AssetId;
        workOrder.AssignedTechnicianId = testData.TechnicianId;
        workOrder.Status = "Assigned";
        
        _mockWorkOrderRepository.Setup(x => x.GetByIdAsync(workOrder.Id))
            .ReturnsAsync(workOrder);
            
        return workOrder;
    }

    private async Task<WorkOrder> SetupInProgressWorkOrder(WorkflowTestData testData)
    {
        var workOrder = await SetupAssignedWorkOrder(testData);
        workOrder.Status = "InProgress";
        workOrder.ActualStartDate = DateTime.UtcNow.AddHours(-2);
        
        return workOrder;
    }

    #endregion

    #region Entity Creation Helpers

    private Asset CreateTestAsset(Guid assetId)
    {
        return new Asset
        {
            Id = assetId,
            Name = "HVAC System Unit 1",
            Description = "Main HVAC system for building",
            AssetNumber = "HVAC-001",
            Status = AssetStatus.Active,
            Criticality = AssetCriticality.High,
            Location = "Building A - Roof",
            InstallationDate = DateTime.UtcNow.AddYears(-5),
            TenantId = Guid.NewGuid()
        };
    }

    private MaintenanceType CreateTestMaintenanceType(Guid typeId)
    {
        return new MaintenanceType
        {
            Id = typeId,
            Name = "Preventive",
            Description = "Preventive maintenance",
            RequiresApproval = false,
            EstimatedHours = 4.0,
            TenantId = Guid.NewGuid()
        };
    }

    private PriorityLevel CreateTestPriorityLevel(Guid priorityId)
    {
        return new PriorityLevel
        {
            Id = priorityId,
            Name = "High",
            Level = 2,
            Description = "High priority maintenance",
            TenantId = Guid.NewGuid()
        };
    }

    private Employee CreateTestEmployee(Guid employeeId)
    {
        return new Employee
        {
            Id = employeeId,
            FirstName = "John",
            LastName = "Doe",
            Email = $"employee{employeeId:N}@company.com",
            EmployeeNumber = $"EMP{employeeId:N}".Substring(0, 10),
            IsActive = true,
            HireDate = DateTime.UtcNow.AddYears(-2),
            TenantId = Guid.NewGuid()
        };
    }

    private InventoryItem CreateTestInventoryItem(Guid itemId)
    {
        return _fixture.Build<InventoryItem>()
            .With(i => i.Id, itemId)
            .With(i => i.ItemNumber, $"PART{itemId:N}".Substring(0, 10))
            .With(i => i.Name, "Test Part")
            .With(i => i.CurrentStock, 50)
            .With(i => i.MinimumStock, 10)
            .With(i => i.UnitPrice, 25.00m)
            .With(i => i.IsActive, true)
            .Create();
    }

    private WorkOrder CreateTestWorkOrder()
    {
        return new WorkOrder
        {
            Id = Guid.NewGuid(),
            WorkOrderNumber = $"WO-{DateTime.UtcNow:yyyyMMdd}-001",
            Title = "Test Work Order",
            Description = "Test work order for integration testing",
            Status = "Created",
            CreatedDate = DateTime.UtcNow,
            TenantId = Guid.NewGuid()
        };
    }

    #endregion

    #region Verification Methods

    private void VerifyWorkflowCompletionInteractions(WorkflowTestData testData)
    {
        // Verify work order lifecycle transitions
        _mockWorkOrderRepository.Verify(x => x.AddAsync(It.IsAny<WorkOrder>()), Times.Once);
        _mockWorkOrderRepository.Verify(x => x.UpdateAsync(It.IsAny<WorkOrder>()), Times.AtLeast(3));

        // Verify technician assignment
        _mockEmployeeRepository.Verify(x => x.GetByIdAsync(testData.TechnicianId), Times.AtLeastOnce);

        // Verify parts allocation
        if (testData.RequestedParts?.Any() == true)
        {
            _mockInventoryRepository.Verify(x => x.AddAsync(It.IsAny<MaintenanceInventoryTransaction>()), 
                Times.AtLeast(testData.RequestedParts.Count));
        }

        // Verify asset and maintenance type lookups
        _mockAssetRepository.Verify(x => x.GetByIdAsync(testData.AssetId), Times.AtLeastOnce);
        _mockMaintenanceTypeRepository.Verify(x => x.GetByIdAsync(testData.MaintenanceTypeId), Times.AtLeastOnce);
    }

    #endregion

    #region Test Data Classes

    private class WorkflowTestData
    {
        public string WorkOrderTitle { get; set; } = string.Empty;
        public string WorkOrderDescription { get; set; } = string.Empty;
        public Guid AssetId { get; set; }
        public Guid MaintenanceTypeId { get; set; }
        public Guid PriorityId { get; set; }
        public Guid EmergencyPriorityId { get; set; }
        public Guid TechnicianId { get; set; }
        public Guid RequesterId { get; set; }
        public Guid AssignedById { get; set; }
        public double EstimatedHours { get; set; }
        public double ActualHours { get; set; }
        public DateTime EstimatedStartDate { get; set; }
        public bool RequiresQualityApproval { get; set; }
        public List<RequestedPart>? RequestedParts { get; set; }
        public List<WorkOrderPartUsageDto>? PartsUsed { get; set; }
    }

    private class RequestedPart
    {
        public Guid ItemId { get; set; }
        public int Quantity { get; set; }
    }

    #endregion

    public void Dispose()
    {
        // Cleanup resources if needed
    }
}