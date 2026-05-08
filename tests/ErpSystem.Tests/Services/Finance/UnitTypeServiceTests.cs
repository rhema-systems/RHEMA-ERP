using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Tests.Services.Finance
{
    /// <summary>
    /// Unit tests for UnitTypeService.
    /// Tests all CRUD operations and business logic.
    /// </summary>
    public class UnitTypeServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<UnitTypeService>> _mockLogger;
        private readonly Mock<IRepository<UnitType>> _mockUnitTypeRepository;
        private readonly Mock<IRepository<UnitAccount>> _mockUnitAccountRepository;
        private readonly UnitTypeService _service;
        private readonly Guid _tenantId = Guid.NewGuid();
        private readonly string _userName = "test-user";

        public UnitTypeServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<UnitTypeService>>();
            _mockUnitTypeRepository = new Mock<IRepository<UnitType>>();
            _mockUnitAccountRepository = new Mock<IRepository<UnitAccount>>();

            // Setup current user service
            _mockCurrentUserService.Setup(s => s.TenantId).Returns(_tenantId);
            _mockCurrentUserService.Setup(s => s.UserName).Returns(_userName);

            // Setup unit of work to return repositories
            _mockUnitOfWork.Setup(u => u.Repository<UnitType>()).Returns(_mockUnitTypeRepository.Object);
            _mockUnitOfWork.Setup(u => u.Repository<UnitAccount>()).Returns(_mockUnitAccountRepository.Object);

            _service = new UnitTypeService(
                _mockUnitOfWork.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object);
        }

        #region GetAllAsync Tests

        [Fact]
        public async Task GetAllAsync_ReturnsAllUnitTypes_ForTenant()
        {
            // Arrange
            var unitTypes = new List<UnitType>
            {
                CreateUnitType("EMP", "Employees"),
                CreateUnitType("SQFT", "Square Feet"),
            };

            SetupQueryable(unitTypes);

            // Act
            var result = await _service.GetAllAsync();

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain(ut => ut.Code == "EMP");
            result.Should().Contain(ut => ut.Code == "SQFT");
        }

        [Fact]
        public async Task GetAllAsync_ReturnsEmpty_WhenNoUnitTypesExist()
        {
            // Arrange
            SetupQueryable(new List<UnitType>());

            // Act
            var result = await _service.GetAllAsync();

            // Assert
            result.Should().BeEmpty();
        }

        #endregion

        #region GetActiveAsync Tests

        [Fact]
        public async Task GetActiveAsync_ReturnsOnlyActiveUnitTypes()
        {
            // Arrange
            var unitTypes = new List<UnitType>
            {
                CreateUnitType("EMP", "Employees", isActive: true),
                CreateUnitType("SQFT", "Square Feet", isActive: false),
                CreateUnitType("HRS", "Hours", isActive: true),
            };

            SetupQueryable(unitTypes);

            // Act
            var result = await _service.GetActiveAsync();

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain(ut => ut.Code == "EMP");
            result.Should().Contain(ut => ut.Code == "HRS");
        }

        #endregion

        #region GetByIdAsync Tests

        [Fact]
        public async Task GetByIdAsync_ReturnsUnitType_WhenExists()
        {
            // Arrange
            var unitType = CreateUnitType("EMP", "Employees");
            _mockUnitTypeRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitType, bool>>>()))
                .ReturnsAsync(unitType);

            // Act
            var result = await _service.GetByIdAsync(unitType.Id);

            // Assert
            result.Should().NotBeNull();
            result!.Code.Should().Be("EMP");
            result.Name.Should().Be("Employees");
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
        {
            // Arrange
            _mockUnitTypeRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitType, bool>>>()))
                .ReturnsAsync((UnitType?)null);

            // Act
            var result = await _service.GetByIdAsync(Guid.NewGuid());

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region GetByCodeAsync Tests

        [Fact]
        public async Task GetByCodeAsync_ReturnsUnitType_WhenCodeExists()
        {
            // Arrange
            var unitType = CreateUnitType("EMP", "Employees");
            _mockUnitTypeRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitType, bool>>>()))
                .ReturnsAsync(unitType);

            // Act
            var result = await _service.GetByCodeAsync("EMP");

            // Assert
            result.Should().NotBeNull();
            result!.Code.Should().Be("EMP");
        }

        [Fact]
        public async Task GetByCodeAsync_ReturnsNull_WhenCodeNotFound()
        {
            // Arrange
            _mockUnitTypeRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitType, bool>>>()))
                .ReturnsAsync((UnitType?)null);

            // Act
            var result = await _service.GetByCodeAsync("NOTEXIST");

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region CreateAsync Tests

        [Fact]
        public async Task CreateAsync_CreatesUnitType_WithValidDto()
        {
            // Arrange
            var dto = new CreateUnitTypeDto
            {
                Code = "hrs",
                Name = "Hours",
                Description = "Work hours tracking",
                DecimalPlaces = 2
            };

            SetupQueryable(new List<UnitType>()); // No existing types
            _mockUnitTypeRepository.Setup(r => r.AddAsync(It.IsAny<UnitType>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            var result = await _service.CreateAsync(dto);

            // Assert
            result.Should().NotBeNull();
            result.Code.Should().Be("HRS"); // Uppercased
            result.Name.Should().Be("Hours");
            result.DecimalPlaces.Should().Be(2);
            result.IsActive.Should().BeTrue();

            _mockUnitTypeRepository.Verify(r => r.AddAsync(It.IsAny<UnitType>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_ThrowsException_WhenCodeAlreadyExists()
        {
            // Arrange
            var dto = new CreateUnitTypeDto { Code = "EMP", Name = "Employees" };
            SetupQueryable(new List<UnitType> { CreateUnitType("EMP", "Existing") });

            // Act
            Func<Task> act = async () => await _service.CreateAsync(dto);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*already exists*");
        }

        #endregion

        #region UpdateAsync Tests

        [Fact]
        public async Task UpdateAsync_UpdatesUnitType_WithValidDto()
        {
            // Arrange
            var unitType = CreateUnitType("EMP", "Employees");
            var dto = new UpdateUnitTypeDto { Name = "Full-Time Employees", DecimalPlaces = 0 };

            _mockUnitTypeRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitType, bool>>>()))
                .ReturnsAsync(unitType);
            _mockUnitTypeRepository.Setup(r => r.UpdateAsync(It.IsAny<UnitType>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            var result = await _service.UpdateAsync(unitType.Id, dto);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("Full-Time Employees");
            result.DecimalPlaces.Should().Be(0);

            _mockUnitTypeRepository.Verify(r => r.UpdateAsync(It.IsAny<UnitType>()), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ThrowsException_WhenNotFound()
        {
            // Arrange
            _mockUnitTypeRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitType, bool>>>()))
                .ReturnsAsync((UnitType?)null);

            // Act
            Func<Task> act = async () => await _service.UpdateAsync(Guid.NewGuid(), new UpdateUnitTypeDto());

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*not found*");
        }

        #endregion

        #region ActivateAsync Tests

        [Fact]
        public async Task ActivateAsync_ActivatesUnitType_WhenExists()
        {
            // Arrange
            var unitType = CreateUnitType("EMP", "Employees", isActive: false);
            _mockUnitTypeRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitType, bool>>>()))
                .ReturnsAsync(unitType);
            _mockUnitTypeRepository.Setup(r => r.UpdateAsync(It.IsAny<UnitType>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            await _service.ActivateAsync(unitType.Id);

            // Assert
            unitType.IsActive.Should().BeTrue();
            _mockUnitTypeRepository.Verify(r => r.UpdateAsync(It.Is<UnitType>(ut => ut.IsActive)), Times.Once);
        }

        [Fact]
        public async Task ActivateAsync_ThrowsException_WhenNotFound()
        {
            // Arrange
            _mockUnitTypeRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitType, bool>>>()))
                .ReturnsAsync((UnitType?)null);

            // Act
            Func<Task> act = async () => await _service.ActivateAsync(Guid.NewGuid());

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*not found*");
        }

        #endregion

        #region DeactivateAsync Tests

        [Fact]
        public async Task DeactivateAsync_DeactivatesUnitType_WhenNoAccounts()
        {
            // Arrange
            var unitType = CreateUnitType("EMP", "Employees", isActive: true);
            _mockUnitTypeRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitType, bool>>>()))
                .ReturnsAsync(unitType);
            SetupAccountQueryable(new List<UnitAccount>()); // No accounts
            _mockUnitTypeRepository.Setup(r => r.UpdateAsync(It.IsAny<UnitType>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            await _service.DeactivateAsync(unitType.Id);

            // Assert
            unitType.IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task DeactivateAsync_ThrowsException_WhenHasAssociatedAccounts()
        {
            // Arrange
            var unitType = CreateUnitType("EMP", "Employees");
            var account = new UnitAccount { Id = Guid.NewGuid(), UnitTypeId = unitType.Id, TenantId = _tenantId };

            _mockUnitTypeRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitType, bool>>>()))
                .ReturnsAsync(unitType);
            SetupAccountQueryable(new List<UnitAccount> { account });

            // Act
            Func<Task> act = async () => await _service.DeactivateAsync(unitType.Id);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*associated unit accounts*");
        }

        #endregion

        #region DeleteAsync Tests

        [Fact]
        public async Task DeleteAsync_SoftDeletesUnitType_WhenNoAccounts()
        {
            // Arrange
            var unitType = CreateUnitType("EMP", "Employees");
            _mockUnitTypeRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitType, bool>>>()))
                .ReturnsAsync(unitType);
            SetupAccountQueryable(new List<UnitAccount>()); // No accounts
            _mockUnitTypeRepository.Setup(r => r.UpdateAsync(It.IsAny<UnitType>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            await _service.DeleteAsync(unitType.Id);

            // Assert
            unitType.IsDeleted.Should().BeTrue();
            unitType.DeletedAt.Should().NotBeNull();
            unitType.DeletedBy.Should().Be(_userName);
        }

        [Fact]
        public async Task DeleteAsync_ThrowsException_WhenHasAssociatedAccounts()
        {
            // Arrange
            var unitType = CreateUnitType("EMP", "Employees");
            var account = new UnitAccount { Id = Guid.NewGuid(), UnitTypeId = unitType.Id, TenantId = _tenantId };

            _mockUnitTypeRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitType, bool>>>()))
                .ReturnsAsync(unitType);
            SetupAccountQueryable(new List<UnitAccount> { account });

            // Act
            Func<Task> act = async () => await _service.DeleteAsync(unitType.Id);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*associated unit accounts*");
        }

        [Fact]
        public async Task DeleteAsync_DoesNothing_WhenNotFound()
        {
            // Arrange
            _mockUnitTypeRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitType, bool>>>()))
                .ReturnsAsync((UnitType?)null);

            // Act - Should not throw
            await _service.DeleteAsync(Guid.NewGuid());

            // Assert - UpdateAsync should not be called
            _mockUnitTypeRepository.Verify(r => r.UpdateAsync(It.IsAny<UnitType>()), Times.Never);
        }

        #endregion

        #region Helper Methods

        private UnitType CreateUnitType(string code, string name, bool isActive = true, bool isDeleted = false)
        {
            return new UnitType
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                Code = code,
                Name = name,
                Description = $"Description for {name}",
                DecimalPlaces = 0,
                IsActive = isActive,
                IsDeleted = isDeleted,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _userName
            };
        }

        private void SetupQueryable(List<UnitType> unitTypes)
        {
            _mockUnitTypeRepository
                .Setup(r => r.GetQueryable(It.IsAny<Expression<Func<UnitType, bool>>>()))
                .Returns((Expression<Func<UnitType, bool>> predicate) =>
                    unitTypes.AsQueryable().Where(predicate));

            _mockUnitAccountRepository
                .Setup(r => r.GetQueryable(It.IsAny<Expression<Func<UnitAccount, bool>>>()))
                .Returns(new List<UnitAccount>().AsQueryable());
        }

        private void SetupAccountQueryable(List<UnitAccount> accounts)
        {
            _mockUnitAccountRepository
                .Setup(r => r.GetQueryable(It.IsAny<Expression<Func<UnitAccount, bool>>>()))
                .Returns((Expression<Func<UnitAccount, bool>> predicate) =>
                    accounts.AsQueryable().Where(predicate));
        }

        #endregion
    }
}
