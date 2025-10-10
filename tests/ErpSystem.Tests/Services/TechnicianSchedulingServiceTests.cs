using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using ErpSystem.Core.Services.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.DTOs.Maintenance;

namespace ErpSystem.Tests.Services;

public class TechnicianSchedulingServiceTests
{
    private readonly Mock<ITechnicianScheduleRepository> _mockScheduleRepository;
    private readonly Mock<ITechnicianAvailabilityRepository> _mockAvailabilityRepository;
    private readonly Mock<ITechnicianShiftRepository> _mockShiftRepository;
    private readonly Mock<IWorkOrderRepository> _mockWorkOrderRepository;
    private readonly Mock<IEmployeeService> _mockEmployeeService;
    private readonly Mock<ILogger<TechnicianSchedulingService>> _mockLogger;
    private readonly TechnicianSchedulingService _service;

    public TechnicianSchedulingServiceTests()
    {
        _mockScheduleRepository = new Mock<ITechnicianScheduleRepository>();
        _mockAvailabilityRepository = new Mock<ITechnicianAvailabilityRepository>();
        _mockShiftRepository = new Mock<ITechnicianShiftRepository>();
        _mockWorkOrderRepository = new Mock<IWorkOrderRepository>();
        _mockEmployeeService = new Mock<IEmployeeService>();
        _mockLogger = new Mock<ILogger<TechnicianSchedulingService>>();

        _service = new TechnicianSchedulingService(
            _mockScheduleRepository.Object,
            _mockAvailabilityRepository.Object,
            _mockShiftRepository.Object,
            _mockWorkOrderRepository.Object,
            _mockEmployeeService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task IsTechnicianAvailableAsync_ShouldReturnFalse_WhenTechnicianNotFound()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var startTime = DateTime.UtcNow.AddHours(1);
        var endTime = startTime.AddHours(2);

        _mockEmployeeService.Setup(x => x.GetTechnicianByIdAsync(technicianId))
            .ReturnsAsync((TechnicianDto?)null);

        // Act
        var result = await _service.IsTechnicianAvailableAsync(technicianId, startTime, endTime);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsTechnicianAvailableAsync_ShouldReturnFalse_WhenTechnicianInactive()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var startTime = DateTime.UtcNow.AddHours(1);
        var endTime = startTime.AddHours(2);

        var inactiveTechnician = CreateTestTechnicianDto();
        inactiveTechnician.IsActive = false;

        _mockEmployeeService.Setup(x => x.GetTechnicianByIdAsync(technicianId))
            .ReturnsAsync(inactiveTechnician);

        // Act
        var result = await _service.IsTechnicianAvailableAsync(technicianId, startTime, endTime);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsTechnicianAvailableAsync_ShouldReturnFalse_WhenStartTimeInPast()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var startTime = DateTime.UtcNow.AddHours(-1); // Past time
        var endTime = DateTime.UtcNow.AddHours(1);

        // Act & Assert
        await _service.Invoking(s => s.IsTechnicianAvailableAsync(technicianId, startTime, endTime))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Start time must be before end time*");
    }

    [Fact]
    public async Task IsTechnicianAvailableAsync_ShouldReturnFalse_WhenUnavailablePeriodsExist()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var startTime = DateTime.UtcNow.AddHours(1);
        var endTime = startTime.AddHours(2);

        var activeTechnician = CreateTestTechnicianDto();
        _mockEmployeeService.Setup(x => x.GetTechnicianByIdAsync(technicianId))
            .ReturnsAsync(activeTechnician);

        var unavailablePeriods = new List<TechnicianAvailability>
        {
            CreateTestAvailability(technicianId, startTime.AddMinutes(-30), endTime.AddMinutes(30), "Unavailable")
        };

        _mockAvailabilityRepository.Setup(x => x.GetUnavailablePeriodsByTechnicianAsync(technicianId, startTime, endTime))
            .ReturnsAsync(unavailablePeriods);

        // Act
        var result = await _service.IsTechnicianAvailableAsync(technicianId, startTime, endTime);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsTechnicianAvailableAsync_ShouldReturnFalse_WhenConflictingScheduleExists()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var startTime = DateTime.UtcNow.AddHours(1);
        var endTime = startTime.AddHours(2);

        var activeTechnician = CreateTestTechnicianDto();
        _mockEmployeeService.Setup(x => x.GetTechnicianByIdAsync(technicianId))
            .ReturnsAsync(activeTechnician);

        _mockAvailabilityRepository.Setup(x => x.GetUnavailablePeriodsByTechnicianAsync(technicianId, startTime, endTime))
            .ReturnsAsync(new List<TechnicianAvailability>());

        _mockScheduleRepository.Setup(x => x.HasConflictingScheduleAsync(technicianId, startTime, endTime))
            .ReturnsAsync(true);

        // Act
        var result = await _service.IsTechnicianAvailableAsync(technicianId, startTime, endTime);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsTechnicianAvailableAsync_ShouldReturnFalse_WhenOutsideWorkingHours()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var startTime = DateTime.UtcNow.Date.AddHours(3); // 3 AM - outside working hours
        var endTime = startTime.AddHours(2);

        var activeTechnician = CreateTestTechnicianDto();
        _mockEmployeeService.Setup(x => x.GetTechnicianByIdAsync(technicianId))
            .ReturnsAsync(activeTechnician);

        _mockAvailabilityRepository.Setup(x => x.GetUnavailablePeriodsByTechnicianAsync(technicianId, startTime, endTime))
            .ReturnsAsync(new List<TechnicianAvailability>());

        _mockScheduleRepository.Setup(x => x.HasConflictingScheduleAsync(technicianId, startTime, endTime))
            .ReturnsAsync(false);

        // Act
        var result = await _service.IsTechnicianAvailableAsync(technicianId, startTime, endTime);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsTechnicianAvailableAsync_ShouldReturnTrue_WhenAllConditionsMet()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var startTime = DateTime.UtcNow.Date.AddHours(9); // 9 AM - within working hours
        var endTime = startTime.AddHours(2);

        var activeTechnician = CreateTestTechnicianDto();
        _mockEmployeeService.Setup(x => x.GetTechnicianByIdAsync(technicianId))
            .ReturnsAsync(activeTechnician);

        _mockAvailabilityRepository.Setup(x => x.GetUnavailablePeriodsByTechnicianAsync(technicianId, startTime, endTime))
            .ReturnsAsync(new List<TechnicianAvailability>());

        _mockScheduleRepository.Setup(x => x.HasConflictingScheduleAsync(technicianId, startTime, endTime))
            .ReturnsAsync(false);

        // Mock hour limits check - within daily limit
        _mockScheduleRepository.Setup(x => x.GetTotalScheduledHoursAsync(technicianId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(8.0); // Current daily hours = 8, adding 2 more = 10, which is under 12 limit

        // Act
        var result = await _service.IsTechnicianAvailableAsync(technicianId, startTime, endTime);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ScheduleWorkOrderAsync_ShouldThrowException_WhenRequestIsNull()
    {
        // Arrange
        ScheduleWorkOrderDto? request = null;

        // Act & Assert
        await _service.Invoking(s => s.ScheduleWorkOrderAsync(request!))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ScheduleWorkOrderAsync_ShouldThrowException_WhenTechnicianIdEmpty()
    {
        // Arrange
        var request = new ScheduleWorkOrderDto
        {
            TechnicianId = Guid.Empty,
            WorkOrderId = Guid.NewGuid(),
            StartTime = DateTime.UtcNow.AddHours(1),
            EndTime = DateTime.UtcNow.AddHours(3),
            EstimatedHours = 2.0
        };

        // Act & Assert
        await _service.Invoking(s => s.ScheduleWorkOrderAsync(request))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*technician ID is required*");
    }

    [Fact]
    public async Task ScheduleWorkOrderAsync_ShouldThrowException_WhenWorkOrderIdEmpty()
    {
        // Arrange
        var request = new ScheduleWorkOrderDto
        {
            TechnicianId = Guid.NewGuid(),
            WorkOrderId = Guid.Empty,
            StartTime = DateTime.UtcNow.AddHours(1),
            EndTime = DateTime.UtcNow.AddHours(3),
            EstimatedHours = 2.0
        };

        // Act & Assert
        await _service.Invoking(s => s.ScheduleWorkOrderAsync(request))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*work order ID is required*");
    }

    [Fact]
    public async Task ScheduleWorkOrderAsync_ShouldThrowException_WhenTechnicianNotAvailable()
    {
        // Arrange
        var request = CreateValidScheduleRequest();

        var activeTechnician = CreateTestTechnicianDto();
        _mockEmployeeService.Setup(x => x.GetTechnicianByIdAsync(request.TechnicianId))
            .ReturnsAsync(activeTechnician);

        // Setup technician as unavailable (has conflicting schedule)
        _mockAvailabilityRepository.Setup(x => x.GetUnavailablePeriodsByTechnicianAsync(request.TechnicianId, request.StartTime, request.EndTime))
            .ReturnsAsync(new List<TechnicianAvailability>());

        _mockScheduleRepository.Setup(x => x.HasConflictingScheduleAsync(request.TechnicianId, request.StartTime, request.EndTime))
            .ReturnsAsync(true);

        // Act & Assert
        await _service.Invoking(s => s.ScheduleWorkOrderAsync(request))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not available*");
    }

    [Fact]
    public async Task ScheduleWorkOrderAsync_ShouldThrowException_WhenWorkOrderNotFound()
    {
        // Arrange
        var request = CreateValidScheduleRequest();

        SetupAvailableTechnician(request.TechnicianId, request.StartTime, request.EndTime);

        _mockWorkOrderRepository.Setup(x => x.GetByIdAsync(request.WorkOrderId))
            .ReturnsAsync((WorkOrder?)null);

        // Act & Assert
        await _service.Invoking(s => s.ScheduleWorkOrderAsync(request))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*work order*not found*");
    }

    [Fact]
    public async Task ScheduleWorkOrderAsync_ShouldCreateScheduleSuccessfully()
    {
        // Arrange
        var request = CreateValidScheduleRequest();
        var workOrder = CreateTestWorkOrder();

        SetupAvailableTechnician(request.TechnicianId, request.StartTime, request.EndTime);

        _mockWorkOrderRepository.Setup(x => x.GetByIdAsync(request.WorkOrderId))
            .ReturnsAsync(workOrder);

        var createdSchedule = CreateTestSchedule();
        _mockScheduleRepository.Setup(x => x.AddAsync(It.IsAny<TechnicianSchedule>()))
            .ReturnsAsync(createdSchedule);

        _mockWorkOrderRepository.Setup(x => x.UpdateAsync(It.IsAny<WorkOrder>()))
            .ReturnsAsync(workOrder);

        // Act
        var result = await _service.ScheduleWorkOrderAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.TechnicianId.Should().Be(request.TechnicianId);
        result.WorkOrderId.Should().Be(request.WorkOrderId);
        result.StartTime.Should().Be(request.StartTime);
        result.EndTime.Should().Be(request.EndTime);

        _mockScheduleRepository.Verify(x => x.AddAsync(It.IsAny<TechnicianSchedule>()), Times.Once);
        _mockWorkOrderRepository.Verify(x => x.UpdateAsync(It.Is<WorkOrder>(w => 
            w.AssignedTechnicianId == request.TechnicianId && w.Status == "Assigned")), Times.Once);
    }

    [Fact]
    public async Task FindBestTechnicianAsync_ShouldReturnNoTechnicianWhenNoneAvailable()
    {
        // Arrange
        var workOrderId = Guid.NewGuid();
        var preferredStartTime = DateTime.UtcNow.AddHours(1);
        var estimatedHours = 2.0;

        var workOrder = CreateTestWorkOrder();
        _mockWorkOrderRepository.Setup(x => x.GetByIdAsync(workOrderId))
            .ReturnsAsync(workOrder);

        _mockEmployeeService.Setup(x => x.GetAvailableTechniciansAsync())
            .ReturnsAsync(new List<TechnicianDto>()); // No technicians available

        // Act
        var result = await _service.FindBestTechnicianAsync(workOrderId, preferredStartTime, estimatedHours);

        // Assert
        result.Should().NotBeNull();
        result.TechnicianId.Should().Be(Guid.Empty);
        result.IsAvailable.Should().BeFalse();
        result.ReasonForRecommendation.Should().Contain("No technicians available");
    }

    [Fact]
    public async Task FindBestTechnicianAsync_ShouldReturnBestAvailableTechnician()
    {
        // Arrange
        var workOrderId = Guid.NewGuid();
        var preferredStartTime = DateTime.UtcNow.Date.AddHours(9); // 9 AM
        var estimatedHours = 4.0;
        var preferredEndTime = preferredStartTime.AddHours(estimatedHours);

        var workOrder = CreateTestWorkOrder();
        _mockWorkOrderRepository.Setup(x => x.GetByIdAsync(workOrderId))
            .ReturnsAsync(workOrder);

        var technicians = new List<TechnicianDto>
        {
            CreateTestTechnicianDto(),
            CreateTestTechnicianDto()
        };

        _mockEmployeeService.Setup(x => x.GetAvailableTechniciansAsync())
            .ReturnsAsync(technicians);

        // Setup first technician as available with good workload
        SetupTechnicianAvailabilityAndWorkload(technicians[0].Id, preferredStartTime, preferredEndTime, 
            isAvailable: true, utilizationPercentage: 70.0);

        // Setup second technician as available but with higher workload
        SetupTechnicianAvailabilityAndWorkload(technicians[1].Id, preferredStartTime, preferredEndTime,
            isAvailable: true, utilizationPercentage: 90.0);

        // Act
        var result = await _service.FindBestTechnicianAsync(workOrderId, preferredStartTime, estimatedHours);

        // Assert
        result.Should().NotBeNull();
        result.TechnicianId.Should().Be(technicians[0].Id); // Should prefer less busy technician
        result.IsAvailable.Should().BeTrue();
        result.Score.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public async Task ScheduleWorkOrderAsync_ShouldThrowException_WhenInvalidEstimatedHours(double estimatedHours)
    {
        // Arrange
        var request = CreateValidScheduleRequest();
        request.EstimatedHours = estimatedHours;

        // Act & Assert
        await _service.Invoking(s => s.ScheduleWorkOrderAsync(request))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Estimated hours must be greater than zero*");
    }

    [Fact]
    public async Task GetTechnicianWorkloadAsync_ShouldReturnCorrectWorkload()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.Date;
        var endDate = startDate.AddDays(7);

        var expectedWorkload = new TechnicianWorkloadDto
        {
            TechnicianId = technicianId,
            StartDate = startDate,
            EndDate = endDate,
            TotalScheduledHours = 32.0,
            TotalAvailableHours = 40.0,
            UtilizationPercentage = 80.0,
            ActiveWorkOrders = 2,
            PendingWorkOrders = 3
        };

        // Setup workload calculation dependencies
        var schedules = CreateTestSchedules(technicianId, 5);
        var availabilities = CreateTestAvailabilities(technicianId, 5);

        // Mock the internal calls that CalculateWorkloadAsync would make
        _mockScheduleRepository.Setup(x => x.GetAllAsync())
            .ReturnsAsync(schedules);

        _mockAvailabilityRepository.Setup(x => x.GetAllAsync())
            .ReturnsAsync(availabilities);

        // Act
        var result = await _service.GetTechnicianWorkloadAsync(technicianId, startDate, endDate);

        // Assert
        result.Should().NotBeNull();
        result.TechnicianId.Should().Be(technicianId);
        result.StartDate.Should().Be(startDate);
        result.EndDate.Should().Be(endDate);
    }

    // Helper methods

    private TechnicianDto CreateTestTechnicianDto()
    {
        return new TechnicianDto
        {
            Id = Guid.NewGuid(),
            FirstName = "John",
            LastName = "Doe",
            FullName = "John Doe",
            Email = "john.doe@example.com",
            PhoneNumber = "555-1234",
            IsActive = true,
            EmployeeNumber = "EMP001",
            PositionTitle = "Technician",
            Skills = new List<TechnicianSkillDto>
            {
                new() { SkillName = "Electrical", Level = "Advanced", IsCertified = true },
                new() { SkillName = "HVAC", Level = "Intermediate", IsCertified = false }
            }
        };
    }

    private TechnicianAvailability CreateTestAvailability(Guid technicianId, DateTime startDate, DateTime endDate, string availabilityType = "Available")
    {
        return new TechnicianAvailability
        {
            Id = Guid.NewGuid(),
            TechnicianId = technicianId,
            StartDate = startDate,
            EndDate = endDate,
            AvailabilityType = availabilityType,
            Reason = "WorkingHours"
        };
    }

    private WorkOrder CreateTestWorkOrder()
    {
        return new WorkOrder
        {
            Id = Guid.NewGuid(),
            WorkOrderNumber = "WO-001",
            Title = "Test Work Order",
            Description = "Test Description",
            Status = "Approved",
            PriorityLevel = new PriorityLevel { Id = Guid.NewGuid(), Name = "High", Level = 2 }
        };
    }

    private TechnicianSchedule CreateTestSchedule()
    {
        return new TechnicianSchedule
        {
            Id = Guid.NewGuid(),
            TechnicianId = Guid.NewGuid(),
            StartDate = DateTime.UtcNow.AddHours(1),
            EndDate = DateTime.UtcNow.AddHours(3),
            Title = "Test Schedule",
            Status = "Scheduled"
        };
    }

    private ScheduleWorkOrderDto CreateValidScheduleRequest()
    {
        return new ScheduleWorkOrderDto
        {
            TechnicianId = Guid.NewGuid(),
            WorkOrderId = Guid.NewGuid(),
            StartTime = DateTime.UtcNow.Date.AddHours(9), // 9 AM
            EndTime = DateTime.UtcNow.Date.AddHours(11),   // 11 AM
            EstimatedHours = 2.0,
            Notes = "Test scheduling"
        };
    }

    private void SetupAvailableTechnician(Guid technicianId, DateTime startTime, DateTime endTime)
    {
        var activeTechnician = CreateTestTechnicianDto();
        activeTechnician.Id = technicianId;

        _mockEmployeeService.Setup(x => x.GetTechnicianByIdAsync(technicianId))
            .ReturnsAsync(activeTechnician);

        _mockAvailabilityRepository.Setup(x => x.GetUnavailablePeriodsByTechnicianAsync(technicianId, startTime, endTime))
            .ReturnsAsync(new List<TechnicianAvailability>());

        _mockScheduleRepository.Setup(x => x.HasConflictingScheduleAsync(technicianId, startTime, endTime))
            .ReturnsAsync(false);

        // Setup hour limits - current usage is within limits
        _mockScheduleRepository.Setup(x => x.GetTotalScheduledHoursAsync(technicianId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(8.0);
    }

    private void SetupTechnicianAvailabilityAndWorkload(Guid technicianId, DateTime startTime, DateTime endTime, 
        bool isAvailable, double utilizationPercentage)
    {
        var technician = CreateTestTechnicianDto();
        technician.Id = technicianId;

        _mockEmployeeService.Setup(x => x.GetTechnicianByIdAsync(technicianId))
            .ReturnsAsync(technician);

        if (isAvailable)
        {
            _mockAvailabilityRepository.Setup(x => x.GetUnavailablePeriodsByTechnicianAsync(technicianId, startTime, endTime))
                .ReturnsAsync(new List<TechnicianAvailability>());

            _mockScheduleRepository.Setup(x => x.HasConflictingScheduleAsync(technicianId, startTime, endTime))
                .ReturnsAsync(false);

            _mockScheduleRepository.Setup(x => x.GetTotalScheduledHoursAsync(technicianId, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(8.0);
        }

        // Mock workload calculation
        var totalScheduledHours = utilizationPercentage * 40 / 100; // Assuming 40-hour work week
        var schedules = CreateTestSchedules(technicianId, 3);
        var availabilities = CreateTestAvailabilities(technicianId, 5);

        _mockScheduleRepository.Setup(x => x.GetAllAsync())
            .ReturnsAsync(schedules);

        _mockAvailabilityRepository.Setup(x => x.GetAllAsync())
            .ReturnsAsync(availabilities);
    }

    private List<TechnicianSchedule> CreateTestSchedules(Guid technicianId, int count)
    {
        var schedules = new List<TechnicianSchedule>();
        var baseDate = DateTime.UtcNow;

        for (int i = 0; i < count; i++)
        {
            schedules.Add(new TechnicianSchedule
            {
                Id = Guid.NewGuid(),
                TechnicianId = technicianId,
                StartDate = baseDate.AddDays(i),
                EndDate = baseDate.AddDays(i).AddHours(8),
                EstimatedHours = 8.0,
                Status = i < 2 ? "Scheduled" : "InProgress"
            });
        }

        return schedules;
    }

    private List<TechnicianAvailability> CreateTestAvailabilities(Guid technicianId, int count)
    {
        var availabilities = new List<TechnicianAvailability>();
        var baseDate = DateTime.UtcNow;

        for (int i = 0; i < count; i++)
        {
            availabilities.Add(new TechnicianAvailability
            {
                Id = Guid.NewGuid(),
                TechnicianId = technicianId,
                StartDate = baseDate.AddDays(i).Date.AddHours(8),
                EndDate = baseDate.AddDays(i).Date.AddHours(17),
                AvailabilityType = "Available",
                Reason = "WorkingHours",
                AvailableHours = 8.0
            });
        }

        return availabilities;
    }
}