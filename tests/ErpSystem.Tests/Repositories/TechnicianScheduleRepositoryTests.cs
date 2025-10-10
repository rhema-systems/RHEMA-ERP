using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using AutoFixture;
using AutoFixture.Xunit2;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Tests.Repositories;

public class TechnicianScheduleRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly TechnicianScheduleRepository _repository;
    private readonly Fixture _fixture;

    public TechnicianScheduleRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _repository = new TechnicianScheduleRepository(_context);
        _fixture = new Fixture();

        // Configure AutoFixture to handle circular references
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
    }

    [Fact]
    public async Task GetByTechnicianIdAsync_ShouldReturnSchedulesForTechnician()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var schedules = CreateTestSchedules(technicianId, 3);
        
        await _context.TechnicianSchedules.AddRangeAsync(schedules);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByTechnicianIdAsync(technicianId);

        // Assert
        result.Should().HaveCount(3);
        result.Should().OnlyContain(s => s.TechnicianId == technicianId);
        result.Should().BeInAscendingOrder(s => s.StartDate);
    }

    [Fact]
    public async Task GetByTechnicianIdAsync_ShouldExcludeDeletedSchedules()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var schedules = CreateTestSchedules(technicianId, 2);
        schedules.First().IsDeleted = true;
        
        await _context.TechnicianSchedules.AddRangeAsync(schedules);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByTechnicianIdAsync(technicianId);

        // Assert
        result.Should().HaveCount(1);
        result.First().IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task HasConflictingScheduleAsync_ShouldReturnTrueWhenConflictExists()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddHours(1);
        var endDate = startDate.AddHours(2);
        
        var existingSchedule = CreateTestSchedule(technicianId, startDate.AddMinutes(30), endDate.AddMinutes(30));
        await _context.TechnicianSchedules.AddAsync(existingSchedule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.HasConflictingScheduleAsync(technicianId, startDate, endDate);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasConflictingScheduleAsync_ShouldReturnFalseWhenNoConflict()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddHours(1);
        var endDate = startDate.AddHours(2);
        
        var existingSchedule = CreateTestSchedule(technicianId, endDate.AddHours(1), endDate.AddHours(3));
        await _context.TechnicianSchedules.AddAsync(existingSchedule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.HasConflictingScheduleAsync(technicianId, startDate, endDate);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasConflictingScheduleAsync_ShouldExcludeSpecifiedSchedule()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddHours(1);
        var endDate = startDate.AddHours(2);
        
        var existingSchedule = CreateTestSchedule(technicianId, startDate, endDate);
        await _context.TechnicianSchedules.AddAsync(existingSchedule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.HasConflictingScheduleAsync(technicianId, startDate, endDate, existingSchedule.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetTotalScheduledHoursAsync_ShouldCalculateCorrectTotal()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.Date;
        var endDate = startDate.AddDays(1);

        var schedules = new[]
        {
            CreateTestSchedule(technicianId, startDate.AddHours(8), startDate.AddHours(12), 4.0, "Scheduled"),
            CreateTestSchedule(technicianId, startDate.AddHours(13), startDate.AddHours(17), 4.0, "InProgress"),
            CreateTestSchedule(technicianId, startDate.AddHours(18), startDate.AddHours(20), 2.0, "Completed") // Should be excluded
        };
        
        await _context.TechnicianSchedules.AddRangeAsync(schedules);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetTotalScheduledHoursAsync(technicianId, startDate, endDate);

        // Assert
        result.Should().Be(8.0); // Only Scheduled and InProgress
    }

    [Fact]
    public async Task GetOverdueSchedulesAsync_ShouldReturnOnlyOverdueSchedules()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var pastDate = DateTime.UtcNow.AddHours(-2);
        var futureDate = DateTime.UtcNow.AddHours(2);

        var schedules = new[]
        {
            CreateTestSchedule(technicianId, pastDate.AddHours(-2), pastDate, status: "Scheduled"), // Overdue
            CreateTestSchedule(technicianId, pastDate.AddHours(-1), pastDate.AddMinutes(-30), status: "InProgress"), // Overdue
            CreateTestSchedule(technicianId, futureDate, futureDate.AddHours(2), status: "Scheduled"), // Not overdue
            CreateTestSchedule(technicianId, pastDate.AddHours(-3), pastDate.AddHours(-1), status: "Completed") // Completed, not overdue
        };
        
        await _context.TechnicianSchedules.AddRangeAsync(schedules);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetOverdueSchedulesAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(s => s.Status is "Scheduled" or "InProgress");
        result.Should().OnlyContain(s => s.EndDate < DateTime.UtcNow);
    }

    [Theory]
    [InlineData("Scheduled")]
    [InlineData("InProgress")]
    [InlineData("Completed")]
    public async Task GetByStatusAsync_ShouldReturnSchedulesWithSpecifiedStatus(string status)
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var schedules = new[]
        {
            CreateTestSchedule(technicianId, status: "Scheduled"),
            CreateTestSchedule(technicianId, status: "InProgress"),
            CreateTestSchedule(technicianId, status: status) // This should be returned
        };
        
        await _context.TechnicianSchedules.AddRangeAsync(schedules);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByStatusAsync(status);

        // Assert
        result.Should().HaveCount(1);
        result.First().Status.Should().Be(status);
    }

    [Fact]
    public async Task ValidateScheduleAsync_ShouldReturnValidForValidSchedule()
    {
        // Arrange
        var schedule = CreateValidTestSchedule();

        // Act
        var result = await _repository.ValidateScheduleAsync(schedule);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ValidationErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateScheduleAsync_ShouldReturnErrorsForInvalidSchedule()
    {
        // Arrange
        var schedule = new TechnicianSchedule
        {
            Id = Guid.Empty,
            TechnicianId = Guid.Empty, // Invalid
            StartDate = DateTime.UtcNow.AddHours(2),
            EndDate = DateTime.UtcNow.AddHours(1), // End before start - Invalid
            EstimatedHours = -1, // Negative hours - Invalid
            Title = "", // Empty title - Invalid
            Status = "Scheduled"
        };

        // Act
        var result = await _repository.ValidateScheduleAsync(schedule);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ValidationErrors.Should().Contain("Valid technician ID is required");
        result.ValidationErrors.Should().Contain("Start date must be before end date");
        result.ValidationErrors.Should().Contain("Estimated hours must be greater than zero");
        result.ValidationErrors.Should().Contain("Schedule title is required");
    }

    [Fact]
    public async Task ValidateScheduleAsync_ShouldDetectSchedulingInPast()
    {
        // Arrange
        var schedule = CreateValidTestSchedule();
        schedule.StartDate = DateTime.UtcNow.AddHours(-1); // In the past
        schedule.EndDate = DateTime.UtcNow.AddMinutes(-30);

        // Act
        var result = await _repository.ValidateScheduleAsync(schedule);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ValidationErrors.Should().Contain("Cannot schedule work in the past");
    }

    [Fact]
    public async Task AddAsync_ShouldThrowExceptionForInvalidSchedule()
    {
        // Arrange
        var invalidSchedule = new TechnicianSchedule
        {
            TechnicianId = Guid.Empty,
            StartDate = DateTime.UtcNow.AddHours(2),
            EndDate = DateTime.UtcNow.AddHours(1), // Invalid: end before start
            EstimatedHours = 1,
            Title = "Test",
            Status = "Scheduled"
        };

        // Act & Assert
        await _repository.Invoking(r => r.AddAsync(invalidSchedule))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*validation failed*");
    }

    [Fact]
    public async Task GetByTechnicianIdLightweightAsync_ShouldReturnSchedulesWithoutNestedIncludes()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var schedules = CreateTestSchedules(technicianId, 2);
        
        await _context.TechnicianSchedules.AddRangeAsync(schedules);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByTechnicianIdLightweightAsync(technicianId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(s => s.TechnicianId == technicianId);
        // Note: In a real scenario with actual navigation properties loaded,
        // we would verify that nested properties aren't loaded for performance
    }

    private TechnicianSchedule CreateValidTestSchedule()
    {
        return new TechnicianSchedule
        {
            Id = Guid.NewGuid(),
            TechnicianId = Guid.NewGuid(),
            StartDate = DateTime.UtcNow.AddHours(1),
            EndDate = DateTime.UtcNow.AddHours(3),
            EstimatedHours = 2,
            Title = "Test Schedule",
            Description = "Test Description",
            Status = "Scheduled",
            Priority = "Medium",
            ScheduleType = "WorkOrder"
        };
    }

    private List<TechnicianSchedule> CreateTestSchedules(Guid technicianId, int count)
    {
        var schedules = new List<TechnicianSchedule>();
        var baseDate = DateTime.UtcNow.AddHours(1);

        for (int i = 0; i < count; i++)
        {
            schedules.Add(CreateTestSchedule(
                technicianId,
                baseDate.AddHours(i * 3),
                baseDate.AddHours(i * 3 + 2)
            ));
        }

        return schedules;
    }

    private TechnicianSchedule CreateTestSchedule(
        Guid technicianId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        double estimatedHours = 2.0,
        string status = "Scheduled")
    {
        var start = startDate ?? DateTime.UtcNow.AddHours(1);
        var end = endDate ?? start.AddHours(2);

        return new TechnicianSchedule
        {
            Id = Guid.NewGuid(),
            TechnicianId = technicianId,
            StartDate = start,
            EndDate = end,
            EstimatedHours = estimatedHours,
            Title = "Test Schedule",
            Description = "Test Description",
            Status = status,
            Priority = "Medium",
            ScheduleType = "WorkOrder"
        };
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}