using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Maintenance;
using ErpSystem.Core.Entities.Maintenance;

namespace ErpSystem.Tests.Repositories;

public class TechnicianAvailabilityRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly TechnicianAvailabilityRepository _repository;

    public TechnicianAvailabilityRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _repository = new TechnicianAvailabilityRepository(_context);
    }

    [Fact]
    public async Task GetByTechnicianIdAsync_ShouldReturnAvailabilityForTechnician()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var availabilities = CreateTestAvailabilities(technicianId, 3);
        
        await _context.TechnicianAvailabilities.AddRangeAsync(availabilities);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByTechnicianIdAsync(technicianId);

        // Assert
        result.Should().HaveCount(3);
        result.Should().OnlyContain(a => a.TechnicianId == technicianId);
        result.Should().BeInAscendingOrder(a => a.StartDate);
    }

    [Fact]
    public async Task GetByTechnicianAndDateRangeAsync_ShouldReturnOverlappingAvailabilities()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var searchStart = DateTime.UtcNow.Date;
        var searchEnd = searchStart.AddDays(7);

        var availabilities = new[]
        {
            CreateTestAvailability(technicianId, searchStart.AddDays(-1), searchStart.AddDays(1)), // Overlaps at start
            CreateTestAvailability(technicianId, searchStart.AddDays(2), searchStart.AddDays(5)), // Fully within range
            CreateTestAvailability(technicianId, searchStart.AddDays(6), searchStart.AddDays(8)), // Overlaps at end
            CreateTestAvailability(technicianId, searchStart.AddDays(10), searchStart.AddDays(12)) // Outside range
        };
        
        await _context.TechnicianAvailabilities.AddRangeAsync(availabilities);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByTechnicianAndDateRangeAsync(technicianId, searchStart, searchEnd);

        // Assert
        result.Should().HaveCount(3); // First three should overlap
    }

    [Fact]
    public async Task GetByAvailabilityTypeAsync_ShouldReturnMatchingTypes()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var availabilities = new[]
        {
            CreateTestAvailability(technicianId, availabilityType: "Available"),
            CreateTestAvailability(technicianId, availabilityType: "Unavailable"),
            CreateTestAvailability(technicianId, availabilityType: "Available")
        };
        
        await _context.TechnicianAvailabilities.AddRangeAsync(availabilities);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByAvailabilityTypeAsync("Available");

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(a => a.AvailabilityType == "Available");
    }

    [Fact]
    public async Task GetUnavailablePeriodsByTechnicianAsync_ShouldReturnOnlyUnavailablePeriods()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.Date;
        var endDate = startDate.AddDays(7);

        var availabilities = new[]
        {
            CreateTestAvailability(technicianId, startDate.AddDays(1), startDate.AddDays(2), "Unavailable", "Sick Leave"),
            CreateTestAvailability(technicianId, startDate.AddDays(3), startDate.AddDays(4), "Available", "WorkingHours"),
            CreateTestAvailability(technicianId, startDate.AddDays(5), startDate.AddDays(6), "Unavailable", "Vacation")
        };
        
        await _context.TechnicianAvailabilities.AddRangeAsync(availabilities);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetUnavailablePeriodsByTechnicianAsync(technicianId, startDate, endDate);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(a => a.AvailabilityType == "Unavailable");
    }

    [Fact]
    public async Task IsTechnicianAvailableAsync_ShouldReturnFalseWhenUnavailable()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddHours(1);
        var endDate = startDate.AddHours(2);

        var unavailablePeriod = CreateTestAvailability(
            technicianId, 
            startDate.AddMinutes(-30), 
            endDate.AddMinutes(30),
            "Unavailable",
            "Meeting"
        );
        
        await _context.TechnicianAvailabilities.AddAsync(unavailablePeriod);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.IsTechnicianAvailableAsync(technicianId, startDate, endDate);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsTechnicianAvailableAsync_ShouldReturnTrueWhenNoUnavailablePeriods()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddHours(1);
        var endDate = startDate.AddHours(2);

        // No unavailable periods added

        // Act
        var result = await _repository.IsTechnicianAvailableAsync(technicianId, startDate, endDate);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetAvailableHoursAsync_ShouldCalculateCorrectTotal()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.Date;
        var endDate = startDate.AddDays(1);

        var availabilities = new[]
        {
            CreateTestAvailability(technicianId, startDate.AddHours(8), startDate.AddHours(12), 
                "Available", "WorkingHours", availableHours: 4.0),
            CreateTestAvailability(technicianId, startDate.AddHours(13), startDate.AddHours(17), 
                "Available", "WorkingHours", availableHours: 4.0),
            CreateTestAvailability(technicianId, startDate.AddHours(18), startDate.AddHours(20), 
                "Unavailable", "Personal"), // Should not be counted
            CreateTestAvailability(technicianId, startDate.AddHours(21), startDate.AddHours(22), 
                "Available", "Overtime", availableHours: null) // No hours specified, should not be counted
        };
        
        await _context.TechnicianAvailabilities.AddRangeAsync(availabilities);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAvailableHoursAsync(technicianId, startDate, endDate);

        // Assert
        result.Should().Be(8.0); // Only the two "Available" periods with specified hours
    }

    [Fact]
    public async Task GetRecurringAvailabilityAsync_ShouldReturnOnlyRecurringEntries()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var availabilities = new[]
        {
            CreateTestAvailability(technicianId, isRecurring: true, recurrencePattern: "FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR"),
            CreateTestAvailability(technicianId, isRecurring: false),
            CreateTestAvailability(technicianId, isRecurring: true, recurrencePattern: "FREQ=DAILY")
        };
        
        await _context.TechnicianAvailabilities.AddRangeAsync(availabilities);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetRecurringAvailabilityAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(a => a.IsRecurring);
    }

    [Fact]
    public async Task ValidateAvailabilityAsync_ShouldReturnValidForValidAvailability()
    {
        // Arrange
        var availability = CreateValidTestAvailability();

        // Act
        var result = await _repository.ValidateAvailabilityAsync(availability);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ValidationErrors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAvailabilityAsync_ShouldReturnErrorsForInvalidAvailability()
    {
        // Arrange
        var availability = new TechnicianAvailability
        {
            TechnicianId = Guid.Empty, // Invalid
            StartDate = DateTime.UtcNow.AddHours(2),
            EndDate = DateTime.UtcNow.AddHours(1), // End before start - Invalid
            AvailabilityType = "", // Empty - Invalid
            Reason = "", // Empty - Invalid
            CapacityPercentage = 150 // Invalid percentage
        };

        // Act
        var result = await _repository.ValidateAvailabilityAsync(availability);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ValidationErrors.Should().Contain("Valid technician ID is required");
        result.ValidationErrors.Should().Contain("Start date must be before end date");
        result.ValidationErrors.Should().Contain("Availability type is required");
        result.ValidationErrors.Should().Contain("Reason for availability change is required");
        result.ValidationErrors.Should().Contain("Capacity percentage must be between 0 and 100");
    }

    [Fact]
    public async Task ValidateAvailabilityAsync_ShouldDetectOverlappingPeriods()
    {
        // Arrange
        var technicianId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddHours(1);
        var endDate = startDate.AddHours(2);

        // Add existing availability
        var existingAvailability = CreateTestAvailability(technicianId, startDate, endDate);
        await _context.TechnicianAvailabilities.AddAsync(existingAvailability);
        await _context.SaveChangesAsync();

        // Create overlapping availability
        var newAvailability = CreateTestAvailability(technicianId, startDate.AddMinutes(30), endDate.AddMinutes(30));

        // Act
        var result = await _repository.ValidateAvailabilityAsync(newAvailability);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ValidationErrors.Should().Contain("Availability period overlaps with existing availability records");
    }

    [Theory]
    [InlineData("InvalidType")]
    [InlineData("")]
    [InlineData(null)]
    public async Task ValidateAvailabilityAsync_ShouldRejectInvalidAvailabilityTypes(string availabilityType)
    {
        // Arrange
        var availability = CreateValidTestAvailability();
        availability.AvailabilityType = availabilityType;

        // Act
        var result = await _repository.ValidateAvailabilityAsync(availability);

        // Assert
        result.IsValid.Should().BeFalse();
        if (string.IsNullOrEmpty(availabilityType))
        {
            result.ValidationErrors.Should().Contain("Availability type is required");
        }
        else
        {
            result.ValidationErrors.Should().Contain("Invalid availability type. Must be one of: Available, Unavailable, PartiallyAvailable");
        }
    }

    [Fact]
    public async Task AddAsync_ShouldThrowExceptionForInvalidAvailability()
    {
        // Arrange
        var invalidAvailability = new TechnicianAvailability
        {
            TechnicianId = Guid.Empty,
            StartDate = DateTime.UtcNow.AddHours(2),
            EndDate = DateTime.UtcNow.AddHours(1), // Invalid: end before start
            AvailabilityType = "Available",
            Reason = "Test"
        };

        // Act & Assert
        await _repository.Invoking(r => r.AddAsync(invalidAvailability))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*validation failed*");
    }

    private TechnicianAvailability CreateValidTestAvailability()
    {
        return new TechnicianAvailability
        {
            Id = Guid.NewGuid(),
            TechnicianId = Guid.NewGuid(),
            StartDate = DateTime.UtcNow.AddHours(1),
            EndDate = DateTime.UtcNow.AddHours(9), // 8-hour day
            AvailabilityType = "Available",
            Reason = "WorkingHours",
            Notes = "Standard working hours",
            IsRecurring = true,
            RecurrencePattern = "FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR",
            AvailableHours = 8.0,
            CapacityPercentage = 100.0
        };
    }

    private List<TechnicianAvailability> CreateTestAvailabilities(Guid technicianId, int count)
    {
        var availabilities = new List<TechnicianAvailability>();
        var baseDate = DateTime.UtcNow.AddHours(1);

        for (int i = 0; i < count; i++)
        {
            availabilities.Add(CreateTestAvailability(
                technicianId,
                baseDate.AddDays(i),
                baseDate.AddDays(i).AddHours(8)
            ));
        }

        return availabilities;
    }

    private TechnicianAvailability CreateTestAvailability(
        Guid technicianId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string availabilityType = "Available",
        string reason = "WorkingHours",
        bool isRecurring = false,
        string? recurrencePattern = null,
        double? availableHours = null)
    {
        var start = startDate ?? DateTime.UtcNow.AddHours(1);
        var end = endDate ?? start.AddHours(8);

        return new TechnicianAvailability
        {
            Id = Guid.NewGuid(),
            TechnicianId = technicianId,
            StartDate = start,
            EndDate = end,
            AvailabilityType = availabilityType,
            Reason = reason,
            IsRecurring = isRecurring,
            RecurrencePattern = recurrencePattern,
            AvailableHours = availableHours,
            CapacityPercentage = 100.0
        };
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}