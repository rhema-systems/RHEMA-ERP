using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.UnitAccounting;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Tests.Services.Finance
{
    /// <summary>
    /// Unit tests for UnitAccountService.
    /// Tests CRUD operations and business logic for Unit Accounts.
    /// </summary>
    public class UnitAccountServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<UnitAccountService>> _mockLogger;
        private readonly Mock<IGenericRepository<UnitAccount>> _mockAccountRepository;
        private readonly Mock<IGenericRepository<UnitType>> _mockUnitTypeRepository;
        private readonly Mock<IGenericRepository<UnitAccountBalance>> _mockBalanceRepository;
        private readonly Mock<IGenericRepository<UnitJournalEntryLine>> _mockJournalLineRepository;
        private readonly UnitAccountService _service;
        private readonly Guid _tenantId = Guid.NewGuid();
        private readonly string _userName = "test-user";

        public UnitAccountServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<UnitAccountService>>();
            _mockAccountRepository = new Mock<IGenericRepository<UnitAccount>>();
            _mockUnitTypeRepository = new Mock<IGenericRepository<UnitType>>();
            _mockBalanceRepository = new Mock<IGenericRepository<UnitAccountBalance>>();
            _mockJournalLineRepository = new Mock<IGenericRepository<UnitJournalEntryLine>>();

            _mockCurrentUserService.Setup(s => s.TenantId).Returns(_tenantId);
            _mockCurrentUserService.Setup(s => s.UserName).Returns(_userName);

            _mockUnitOfWork.Setup(u => u.Repository<UnitAccount>()).Returns(_mockAccountRepository.Object);
            _mockUnitOfWork.Setup(u => u.Repository<UnitType>()).Returns(_mockUnitTypeRepository.Object);
            _mockUnitOfWork.Setup(u => u.Repository<UnitAccountBalance>()).Returns(_mockBalanceRepository.Object);
            _mockUnitOfWork.Setup(u => u.Repository<UnitJournalEntryLine>()).Returns(_mockJournalLineRepository.Object);
            SetupJournalLineQueryable(new List<UnitJournalEntryLine>());

            _service = new UnitAccountService(
                _mockUnitOfWork.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object);
        }

        #region GetAllAsync Tests

        [Fact]
        public async Task GetAllAsync_ReturnsAllAccounts_ForTenant()
        {
            // Arrange
            var accounts = new List<UnitAccount>
            {
                CreateAccount("U-1000", "Department A"),
                CreateAccount("U-2000", "Department B"),
            };
            SetupAccountQueryable(accounts);
            SetupBalanceQueryable(new List<UnitAccountBalance>());

            // Act
            var result = await _service.GetAllAsync();

            // Assert
            result.Should().HaveCount(2);
        }

        #endregion

        #region GetByIdAsync Tests

        [Fact]
        public async Task GetByIdAsync_ReturnsAccount_WhenExists()
        {
            // Arrange
            var account = CreateAccount("U-1000", "Test Account");
            SetupAccountQueryable(new List<UnitAccount> { account });
            SetupBalanceQueryable(new List<UnitAccountBalance>());

            // Act
            var result = await _service.GetByIdAsync(account.Id);

            // Assert
            result.Should().NotBeNull();
            result!.AccountNumber.Should().Be("U-1000");
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
        {
            // Arrange
            SetupAccountQueryable(new List<UnitAccount>());

            // Act
            var result = await _service.GetByIdAsync(Guid.NewGuid());

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetBalancesAsync_ShouldRejectAccountOwnedByAnotherTenant()
        {
            var otherTenantAccount = CreateAccount("U-OTHER", "Other tenant");
            otherTenantAccount.TenantId = Guid.NewGuid();
            SetupAccountQueryable(new List<UnitAccount> { otherTenantAccount });
            SetupBalanceQueryable(new List<UnitAccountBalance>());

            var action = () => _service.GetBalancesAsync(otherTenantAccount.Id);

            await action.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*not found*");
        }

        #endregion

        #region CreateAsync Tests

        [Fact]
        public async Task CreateAsync_CreatesAccount_WithValidDto()
        {
            // Arrange
            var unitTypeId = Guid.NewGuid();
            var unitType = new UnitType { Id = unitTypeId, TenantId = _tenantId, Code = "EMP", Name = "Employees" };
            var dto = new CreateUnitAccountDto
            {
                AccountNumber = "U-3000",
                Name = "New Department",
                UnitTypeId = unitTypeId
            };

            var accounts = new List<UnitAccount>();
            SetupAccountQueryable(accounts);
            _mockUnitTypeRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitType, bool>>>()))
                .ReturnsAsync(unitType);
            _mockAccountRepository
                .Setup(r => r.AddAsync(It.IsAny<UnitAccount>()))
                .ReturnsAsync((UnitAccount entity) =>
                {
                    accounts.Add(entity);
                    return entity;
                });
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            SetupBalanceQueryable(new List<UnitAccountBalance>());

            // Act
            var result = await _service.CreateAsync(dto);

            // Assert
            result.Should().NotBeNull();
            result.AccountNumber.Should().Be("U-3000");
            result.Name.Should().Be("New Department");
            _mockAccountRepository.Verify(r => r.AddAsync(It.IsAny<UnitAccount>()), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_ThrowsException_WhenAccountNumberExists()
        {
            // Arrange
            var existingAccount = CreateAccount("U-1000", "Existing");
            var dto = new CreateUnitAccountDto
            {
                AccountNumber = "U-1000",
                Name = "New Account",
                UnitTypeId = Guid.NewGuid()
            };

            SetupAccountQueryable(new List<UnitAccount> { existingAccount });

            // Act
            Func<Task> act = async () => await _service.CreateAsync(dto);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*already exists*");
        }

        [Fact]
        public async Task CreateAsync_ThrowsException_WhenUnitTypeNotFound()
        {
            // Arrange
            var dto = new CreateUnitAccountDto
            {
                AccountNumber = "U-3000",
                Name = "New Account",
                UnitTypeId = Guid.NewGuid()
            };

            SetupAccountQueryable(new List<UnitAccount>());
            _mockUnitTypeRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitType, bool>>>()))
                .ReturnsAsync((UnitType?)null);

            // Act
            Func<Task> act = async () => await _service.CreateAsync(dto);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Unit type*not found*");
        }

        #endregion

        #region UpdateAsync Tests

        [Fact]
        public async Task UpdateAsync_UpdatesAccount_WithValidDto()
        {
            // Arrange
            var account = CreateAccount("U-1000", "Original Name");
            var dto = new UpdateUnitAccountDto { Name = "Updated Name", Description = "New Description" };

            SetupAccountQueryable(new List<UnitAccount> { account });
            _mockAccountRepository.Setup(r => r.UpdateAsync(It.IsAny<UnitAccount>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            SetupBalanceQueryable(new List<UnitAccountBalance>());

            // Act
            var result = await _service.UpdateAsync(account.Id, dto);

            // Assert
            result.Name.Should().Be("Updated Name");
            _mockAccountRepository.Verify(r => r.UpdateAsync(It.IsAny<UnitAccount>()), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ThrowsException_WhenNotFound()
        {
            // Arrange
            SetupAccountQueryable(new List<UnitAccount>());

            // Act
            Func<Task> act = async () => await _service.UpdateAsync(Guid.NewGuid(), new UpdateUnitAccountDto());

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*not found*");
        }

        #endregion

        #region DeleteAsync Tests

        [Fact]
        public async Task DeleteAsync_SoftDeletesAccount_WhenNoChildren()
        {
            // Arrange
            var account = CreateAccount("U-1000", "Test Account");
            _mockAccountRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitAccount, bool>>>()))
                .ReturnsAsync(account);
            SetupAccountQueryable(new List<UnitAccount>()); // No children
            SetupBalanceQueryable(new List<UnitAccountBalance>());
            _mockAccountRepository.Setup(r => r.UpdateAsync(It.IsAny<UnitAccount>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            await _service.DeleteAsync(account.Id);

            // Assert
            account.IsDeleted.Should().BeTrue();
        }

        [Fact]
        public async Task DeleteAsync_ThrowsException_WhenHasChildAccounts()
        {
            // Arrange
            var account = CreateAccount("U-1000", "Parent");
            var childAccount = CreateAccount("U-1001", "Child");
            childAccount.ParentAccountId = account.Id;

            _mockAccountRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitAccount, bool>>>()))
                .ReturnsAsync(account);
            SetupAccountQueryable(new List<UnitAccount> { childAccount });

            // Act
            Func<Task> act = async () => await _service.DeleteAsync(account.Id);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*child accounts*");
        }

        #endregion

        #region ActivateAsync / DeactivateAsync Tests

        [Fact]
        public async Task ActivateAsync_ActivatesAccount_WhenExists()
        {
            // Arrange
            var account = CreateAccount("U-1000", "Test", isActive: false);
            _mockAccountRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitAccount, bool>>>()))
                .ReturnsAsync(account);
            _mockAccountRepository.Setup(r => r.UpdateAsync(It.IsAny<UnitAccount>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            await _service.ActivateAsync(account.Id);

            // Assert
            account.IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task DeactivateAsync_DeactivatesAccount_WhenExists()
        {
            // Arrange
            var account = CreateAccount("U-1000", "Test", isActive: true);
            _mockAccountRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitAccount, bool>>>()))
                .ReturnsAsync(account);
            _mockAccountRepository.Setup(r => r.UpdateAsync(It.IsAny<UnitAccount>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            await _service.DeactivateAsync(account.Id);

            // Assert
            account.IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task RecalculateBalancesAsync_ShouldAggregatePostingDescendants_ForSummaryAccount()
        {
            var parent = CreateAccount("U-1000", "Summary");
            parent.IsPostingAccount = false;
            var child = CreateAccount("U-1001", "Posting child");
            child.ParentAccountId = parent.Id;
            var entry = new UnitJournalEntry
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                Status = UnitJournalEntryStatus.Posted
            };
            var line = new UnitJournalEntryLine
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                UnitAccountId = child.Id,
                UnitJournalEntryId = entry.Id,
                UnitJournalEntry = entry,
                Quantity = 25m
            };

            _mockAccountRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitAccount, bool>>>() ))
                .ReturnsAsync(parent);
            SetupAccountQueryable(new List<UnitAccount> { parent, child });
            SetupJournalLineQueryable(new List<UnitJournalEntryLine> { line });
            _mockAccountRepository.Setup(r => r.UpdateAsync(It.IsAny<UnitAccount>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            await _service.RecalculateBalancesAsync(parent.Id);

            parent.CurrentBalance.Should().Be(25m);
        }

        #endregion

        #region Helper Methods

        private UnitAccount CreateAccount(string accountNumber, string name, bool isActive = true)
        {
            return new UnitAccount
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                AccountNumber = accountNumber,
                Name = name,
                UnitTypeId = Guid.NewGuid(),
                IsActive = isActive,
                IsPostingAccount = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _userName
            };
        }

        private void SetupAccountQueryable(List<UnitAccount> accounts)
        {
            _mockAccountRepository
                .Setup(r => r.GetQueryable(It.IsAny<Expression<Func<UnitAccount, bool>>>()))
                .Returns((Expression<Func<UnitAccount, bool>> predicate) =>
                    accounts.Where(predicate.Compile()).AsAsyncQueryable());
        }

        private void SetupBalanceQueryable(List<UnitAccountBalance> balances)
        {
            _mockBalanceRepository
                .Setup(r => r.GetQueryable(It.IsAny<Expression<Func<UnitAccountBalance, bool>>>()))
                .Returns((Expression<Func<UnitAccountBalance, bool>> predicate) =>
                    balances.Where(predicate.Compile()).AsAsyncQueryable());
        }

        private void SetupJournalLineQueryable(List<UnitJournalEntryLine> lines)
        {
            _mockJournalLineRepository
                .Setup(r => r.GetQueryable(It.IsAny<Expression<Func<UnitJournalEntryLine, bool>>>()))
                .Returns((Expression<Func<UnitJournalEntryLine, bool>> predicate) =>
                    lines.Where(predicate.Compile()).AsAsyncQueryable());
        }

        #endregion
    }
}
