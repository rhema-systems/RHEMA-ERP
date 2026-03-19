using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ErpSystem.Tests.Controllers.Finance
{
    /// <summary>
    /// Integration tests for UnitTypeController.
    /// Tests HTTP responses and controller action behavior.
    /// </summary>
    public class UnitTypeControllerTests
    {
        private readonly Mock<IUnitTypeService> _mockService;
        private readonly UnitTypeController _controller;
        private readonly Guid _testId = Guid.NewGuid();

        public UnitTypeControllerTests()
        {
            _mockService = new Mock<IUnitTypeService>();
            _controller = new UnitTypeController(_mockService.Object);
        }

        #region GetUnitTypes Tests

        [Fact]
        public async Task GetUnitTypes_ReturnsOk_WithListOfUnitTypes()
        {
            // Arrange
            var unitTypes = new List<UnitTypeDto>
            {
                CreateDto("EMP", "Employees"),
                CreateDto("SQFT", "Square Feet")
            };
            _mockService.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(unitTypes);

            // Act
            var result = await _controller.GetUnitTypes();

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            (okResult!.Value as List<UnitTypeDto>).Should().HaveCount(2);
        }

        [Fact]
        public async Task GetUnitTypes_ReturnsOk_WithEmptyList_WhenNoData()
        {
            // Arrange
            _mockService.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<UnitTypeDto>());

            // Act
            var result = await _controller.GetUnitTypes();

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
        }

        #endregion

        #region GetActiveUnitTypes Tests

        [Fact]
        public async Task GetActiveUnitTypes_ReturnsOk_WithActiveUnitTypesOnly()
        {
            // Arrange
            var activeTypes = new List<UnitTypeDto> { CreateDto("EMP", "Employees") };
            _mockService.Setup(s => s.GetActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(activeTypes);

            // Act
            var result = await _controller.GetActiveUnitTypes();

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            (okResult!.Value as List<UnitTypeDto>).Should().HaveCount(1);
        }

        #endregion

        #region GetUnitTypeById Tests

        [Fact]
        public async Task GetUnitTypeById_ReturnsOk_WhenFound()
        {
            // Arrange
            var dto = CreateDto("EMP", "Employees");
            _mockService.Setup(s => s.GetByIdAsync(_testId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            // Act
            var result = await _controller.GetUnitTypeById(_testId);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            (okResult!.Value as UnitTypeDto)!.Code.Should().Be("EMP");
        }

        [Fact]
        public async Task GetUnitTypeById_ReturnsNotFound_WhenNotExists()
        {
            // Arrange
            _mockService.Setup(s => s.GetByIdAsync(_testId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((UnitTypeDto?)null);

            // Act
            var result = await _controller.GetUnitTypeById(_testId);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        #endregion

        #region GetUnitTypeByCode Tests

        [Fact]
        public async Task GetUnitTypeByCode_ReturnsOk_WhenFound()
        {
            // Arrange
            var dto = CreateDto("EMP", "Employees");
            _mockService.Setup(s => s.GetByCodeAsync("EMP", It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            // Act
            var result = await _controller.GetUnitTypeByCode("EMP");

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task GetUnitTypeByCode_ReturnsNotFound_WhenNotExists()
        {
            // Arrange
            _mockService.Setup(s => s.GetByCodeAsync("XYZ", It.IsAny<CancellationToken>()))
                .ReturnsAsync((UnitTypeDto?)null);

            // Act
            var result = await _controller.GetUnitTypeByCode("XYZ");

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        #endregion

        #region CreateUnitType Tests

        [Fact]
        public async Task CreateUnitType_ReturnsCreated_WithValidDto()
        {
            // Arrange
            var createDto = new CreateUnitTypeDto { Code = "HRS", Name = "Hours" };
            var createdDto = CreateDto("HRS", "Hours");
            _mockService.Setup(s => s.CreateAsync(createDto, It.IsAny<CancellationToken>()))
                .ReturnsAsync(createdDto);

            // Act
            var result = await _controller.CreateUnitType(createDto);

            // Assert
            result.Result.Should().BeOfType<CreatedAtActionResult>();
            var createdResult = result.Result as CreatedAtActionResult;
            (createdResult!.Value as UnitTypeDto)!.Code.Should().Be("HRS");
        }

        [Fact]
        public async Task CreateUnitType_ReturnsBadRequest_WhenDuplicateCode()
        {
            // Arrange
            var createDto = new CreateUnitTypeDto { Code = "EMP", Name = "Employees" };
            _mockService.Setup(s => s.CreateAsync(createDto, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Unit type with code 'EMP' already exists."));

            // Act
            var result = await _controller.CreateUnitType(createDto);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        #endregion

        #region UpdateUnitType Tests

        [Fact]
        public async Task UpdateUnitType_ReturnsOk_WhenUpdated()
        {
            // Arrange
            var updateDto = new UpdateUnitTypeDto { Name = "Updated Name" };
            var updatedDto = CreateDto("EMP", "Updated Name");
            _mockService.Setup(s => s.UpdateAsync(_testId, updateDto, It.IsAny<CancellationToken>()))
                .ReturnsAsync(updatedDto);

            // Act
            var result = await _controller.UpdateUnitType(_testId, updateDto);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            (okResult!.Value as UnitTypeDto)!.Name.Should().Be("Updated Name");
        }

        [Fact]
        public async Task UpdateUnitType_ReturnsNotFound_WhenNotExists()
        {
            // Arrange
            var updateDto = new UpdateUnitTypeDto { Name = "New Name" };
            _mockService.Setup(s => s.UpdateAsync(_testId, updateDto, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Unit type not found."));

            // Act
            var result = await _controller.UpdateUnitType(_testId, updateDto);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        #endregion

        #region DeleteUnitType Tests

        [Fact]
        public async Task DeleteUnitType_ReturnsNoContent_WhenDeleted()
        {
            // Arrange
            _mockService.Setup(s => s.DeleteAsync(_testId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.DeleteUnitType(_testId);

            // Assert
            result.Should().BeOfType<NoContentResult>();
        }

        [Fact]
        public async Task DeleteUnitType_ReturnsNotFound_WhenNotExists()
        {
            // Arrange
            _mockService.Setup(s => s.DeleteAsync(_testId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Unit type not found."));

            // Act
            var result = await _controller.DeleteUnitType(_testId);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task DeleteUnitType_ReturnsBadRequest_WhenHasAssociatedAccounts()
        {
            // Arrange
            _mockService.Setup(s => s.DeleteAsync(_testId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Cannot delete - has associated accounts."));

            // Act
            var result = await _controller.DeleteUnitType(_testId);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        #endregion

        #region ActivateUnitType Tests

        [Fact]
        public async Task ActivateUnitType_ReturnsNoContent_WhenActivated()
        {
            // Arrange
            _mockService.Setup(s => s.ActivateAsync(_testId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.ActivateUnitType(_testId);

            // Assert
            result.Should().BeOfType<NoContentResult>();
        }

        [Fact]
        public async Task ActivateUnitType_ReturnsNotFound_WhenNotExists()
        {
            // Arrange
            _mockService.Setup(s => s.ActivateAsync(_testId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Not found."));

            // Act
            var result = await _controller.ActivateUnitType(_testId);

            // Assert
            result.Should().BeOfType<NotFoundObjectResult>();
        }

        #endregion

        #region DeactivateUnitType Tests

        [Fact]
        public async Task DeactivateUnitType_ReturnsNoContent_WhenDeactivated()
        {
            // Arrange
            _mockService.Setup(s => s.DeactivateAsync(_testId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.DeactivateUnitType(_testId);

            // Assert
            result.Should().BeOfType<NoContentResult>();
        }

        [Fact]
        public async Task DeactivateUnitType_ReturnsBadRequest_WhenHasAssociatedAccounts()
        {
            // Arrange
            _mockService.Setup(s => s.DeactivateAsync(_testId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Cannot deactivate - has associated accounts."));

            // Act
            var result = await _controller.DeactivateUnitType(_testId);

            // Assert
            result.Should().BeOfType<BadRequestObjectResult>();
        }

        #endregion

        #region Helper Methods

        private UnitTypeDto CreateDto(string code, string name)
        {
            return new UnitTypeDto
            {
                Id = _testId,
                Code = code,
                Name = name,
                Description = $"Description for {name}",
                DecimalPlaces = 0,
                IsActive = true,
                AccountCount = 0,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test-user"
            };
        }

        #endregion
    }
}
