using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.UnitAccounting;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Tests.Services.Finance
{
    /// <summary>
    /// Unit tests for UnitJournalEntryService.
    /// Tests CRUD operations and workflow (approval, posting, reversal).
    /// </summary>
    public class UnitJournalEntryServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<ILogger<UnitJournalEntryService>> _mockLogger;
        private readonly Mock<IGenericRepository<UnitJournalEntry>> _mockEntryRepository;
        private readonly Mock<IGenericRepository<UnitJournalEntryLine>> _mockLineRepository;
        private readonly Mock<IGenericRepository<UnitAccount>> _mockAccountRepository;
        private readonly Mock<IGenericRepository<UnitAccountBalance>> _mockBalanceRepository;
        private readonly Mock<IGenericRepository<FiscalPeriod>> _mockPeriodRepository;
        private readonly Mock<IDocumentNumberingService> _mockDocumentNumberingService;
        private readonly Mock<IWorkflowService> _mockWorkflowService;
        private readonly UnitJournalEntryService _service;
        private readonly Guid _tenantId = Guid.NewGuid();
        private readonly Guid _userId = Guid.NewGuid();
        private readonly string _userName = "test-user";

        public UnitJournalEntryServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockLogger = new Mock<ILogger<UnitJournalEntryService>>();
            _mockEntryRepository = new Mock<IGenericRepository<UnitJournalEntry>>();
            _mockLineRepository = new Mock<IGenericRepository<UnitJournalEntryLine>>();
            _mockAccountRepository = new Mock<IGenericRepository<UnitAccount>>();
            _mockBalanceRepository = new Mock<IGenericRepository<UnitAccountBalance>>();
            _mockPeriodRepository = new Mock<IGenericRepository<FiscalPeriod>>();
            _mockDocumentNumberingService = new Mock<IDocumentNumberingService>();
            _mockWorkflowService = new Mock<IWorkflowService>();

            _mockCurrentUserService.Setup(s => s.TenantId).Returns(_tenantId);
            _mockCurrentUserService.Setup(s => s.UserName).Returns(_userName);
            _mockCurrentUserService.Setup(s => s.UserId).Returns(_userId.ToString());

            _mockUnitOfWork.Setup(u => u.Repository<UnitJournalEntry>()).Returns(_mockEntryRepository.Object);
            _mockUnitOfWork.Setup(u => u.Repository<UnitJournalEntryLine>()).Returns(_mockLineRepository.Object);
            _mockUnitOfWork.Setup(u => u.Repository<UnitAccount>()).Returns(_mockAccountRepository.Object);
            _mockUnitOfWork.Setup(u => u.Repository<UnitAccountBalance>()).Returns(_mockBalanceRepository.Object);
            _mockUnitOfWork.Setup(u => u.Repository<FiscalPeriod>()).Returns(_mockPeriodRepository.Object);
            _mockUnitOfWork
                .Setup(u => u.ExecuteInStrategyAsync(
                    It.IsAny<Func<Task<UnitJournalEntryDto>>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((Func<Task<UnitJournalEntryDto>> operation, CancellationToken _) => operation());
            _mockUnitOfWork
                .Setup(u => u.BeginTransactionAsync(
                    It.IsAny<System.Data.IsolationLevel>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockUnitOfWork
                .Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            _mockUnitOfWork
                .Setup(u => u.RollbackAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            SetupBalanceRepository(new List<UnitAccountBalance>());
            _mockDocumentNumberingService
                .Setup(s => s.GenerateAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<string?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync($"UJE-{DateTime.UtcNow.Year}-0001");
            _mockWorkflowService
                .Setup(s => s.StartApprovalWorkflowAsync("UnitJournalEntry", It.IsAny<Guid>()))
                .ReturnsAsync(new WorkflowExecutionResult
                {
                    Success = true,
                    Status = WorkflowInstanceStatus.InProgress
                });
            _mockWorkflowService
                .Setup(s => s.CanUserApproveAsync("UnitJournalEntry", It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(true);
            _mockWorkflowService
                .Setup(s => s.ProcessApprovalStepAsync(
                    "UnitJournalEntry",
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>()))
                .ReturnsAsync((string _, Guid _, Guid _, string action, string? _) =>
                    new WorkflowExecutionResult
                    {
                        Success = true,
                        Status = action == "Approve"
                            ? WorkflowInstanceStatus.Completed
                            : WorkflowInstanceStatus.Cancelled
                    });

            _service = new UnitJournalEntryService(
                _mockUnitOfWork.Object,
                _mockCurrentUserService.Object,
                _mockLogger.Object,
                _mockDocumentNumberingService.Object,
                _mockWorkflowService.Object);
        }

        #region GetByIdAsync Tests

        [Fact]
        public async Task GetByIdAsync_ReturnsEntry_WhenExists()
        {
            // Arrange
            var entry = CreateEntry(UnitJournalEntryStatus.Draft);
            SetupEntryQueryableWithIncludes(new List<UnitJournalEntry> { entry });

            // Act
            var result = await _service.GetByIdAsync(entry.Id);

            // Assert
            result.Should().NotBeNull();
            result!.EntryNumber.Should().Be(entry.EntryNumber);
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
        {
            // Arrange
            SetupEntryQueryableWithIncludes(new List<UnitJournalEntry>());

            // Act
            var result = await _service.GetByIdAsync(Guid.NewGuid());

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region CreateAsync Tests

        [Fact]
        public async Task CreateAsync_CreatesEntry_WithValidDto()
        {
            // Arrange
            var fiscalPeriod = CreateFiscalPeriod();
            var accountId = Guid.NewGuid();
            var dto = new CreateUnitJournalEntryDto
            {
                EntryDate = DateTime.UtcNow,
                Description = "Test Entry",
                FiscalPeriodId = fiscalPeriod.Id,
                Lines = new List<CreateUnitJournalEntryLineDto>
                {
                    new() { UnitAccountId = accountId, Quantity = 10, Description = "Line 1" }
                }
            };

            SetupEntryQueryable(new List<UnitJournalEntry>());
            SetupPeriodQueryable(new List<FiscalPeriod> { fiscalPeriod });
            SetupAccountQueryable(new List<UnitAccount> { CreateAccount(accountId) });
            _mockEntryRepository
                .Setup(r => r.AddAsync(It.IsAny<UnitJournalEntry>()))
                .ReturnsAsync((UnitJournalEntry entity) => entity);
            _mockLineRepository
                .Setup(r => r.AddAsync(It.IsAny<UnitJournalEntryLine>()))
                .ReturnsAsync((UnitJournalEntryLine entity) => entity);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            var result = await _service.CreateAsync(dto);

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("Draft");
            _mockEntryRepository.Verify(r => r.AddAsync(It.IsAny<UnitJournalEntry>()), Times.Once);
            _mockLineRepository.Verify(r => r.AddAsync(It.IsAny<UnitJournalEntryLine>()), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_ThrowsException_WhenFiscalPeriodNotFound()
        {
            // Arrange
            var dto = new CreateUnitJournalEntryDto
            {
                EntryDate = DateTime.UtcNow,
                Description = "Test",
                FiscalPeriodId = Guid.NewGuid(),
                Lines = new List<CreateUnitJournalEntryLineDto>()
            };

            SetupEntryQueryable(new List<UnitJournalEntry>());
            SetupPeriodQueryable(new List<FiscalPeriod>());

            // Act
            Func<Task> act = async () => await _service.CreateAsync(dto);

            // Assert
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Fiscal period*not found*");
        }

        #endregion

        #region UpdateAsync Tests

        [Fact]
        public async Task UpdateAsync_UpdatesEntry_WhenDraft()
        {
            // Arrange
            var entry = CreateEntry(UnitJournalEntryStatus.Draft);
            var dto = new UpdateUnitJournalEntryDto { Description = "Updated Description" };

            SetupEntryQueryableWithIncludes(new List<UnitJournalEntry> { entry });
            _mockEntryRepository.Setup(r => r.UpdateAsync(It.IsAny<UnitJournalEntry>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            var result = await _service.UpdateAsync(entry.Id, dto);

            // Assert
            result.Should().NotBeNull();
            _mockEntryRepository.Verify(r => r.UpdateAsync(It.IsAny<UnitJournalEntry>()), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ThrowsException_WhenNotDraft()
        {
            // Arrange
            var entry = CreateEntry(UnitJournalEntryStatus.Posted);
            SetupEntryQueryableWithIncludes(new List<UnitJournalEntry> { entry });

            // Act
            Func<Task> act = async () => await _service.UpdateAsync(entry.Id, new UpdateUnitJournalEntryDto());

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Only draft or rejected entries*");
        }

        #endregion

        #region DeleteAsync Tests

        [Fact]
        public async Task DeleteAsync_SoftDeletesEntry_WhenDraft()
        {
            // Arrange
            var entry = CreateEntry(UnitJournalEntryStatus.Draft);
            _mockEntryRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitJournalEntry, bool>>>()))
                .ReturnsAsync(entry);
            _mockEntryRepository.Setup(r => r.UpdateAsync(It.IsAny<UnitJournalEntry>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            await _service.DeleteAsync(entry.Id);

            // Assert
            entry.IsDeleted.Should().BeTrue();
        }

        [Fact]
        public async Task DeleteAsync_ThrowsException_WhenNotDraft()
        {
            // Arrange
            var entry = CreateEntry(UnitJournalEntryStatus.Posted);
            _mockEntryRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitJournalEntry, bool>>>()))
                .ReturnsAsync(entry);

            // Act
            Func<Task> act = async () => await _service.DeleteAsync(entry.Id);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Only draft or rejected entries*");
        }

        #endregion

        #region Workflow: SubmitForApprovalAsync Tests

        [Fact]
        public async Task SubmitForApprovalAsync_ChangesStatusToPending_WhenDraft()
        {
            // Arrange
            var entry = CreateEntry(UnitJournalEntryStatus.Draft);
            entry.Lines.Add(CreateLine(entry.Id)); // Add a line
            SetupEntryQueryableWithIncludes(new List<UnitJournalEntry> { entry });
            SetupAccountQueryable(new List<UnitAccount> { CreateAccount(entry.Lines.Single().UnitAccountId) });
            SetupPeriodQueryable(new List<FiscalPeriod> { CreateFiscalPeriod(entry.FiscalPeriodId, entry.FiscalYearId) });
            _mockEntryRepository.Setup(r => r.UpdateAsync(It.IsAny<UnitJournalEntry>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            var result = await _service.SubmitForApprovalAsync(entry.Id);

            // Assert
            result.Status.Should().Be("PendingApproval");
        }

        [Fact]
        public async Task SubmitForApprovalAsync_ThrowsException_WhenNoLines()
        {
            // Arrange
            var entry = CreateEntry(UnitJournalEntryStatus.Draft);
            // No lines added
            SetupEntryQueryableWithIncludes(new List<UnitJournalEntry> { entry });

            // Act
            Func<Task> act = async () => await _service.SubmitForApprovalAsync(entry.Id);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*no lines*");
        }

        #endregion

        #region Workflow: ApproveAsync Tests

        [Fact]
        public async Task ApproveAsync_ChangesStatusToApproved_WhenPending()
        {
            // Arrange
            var entry = CreateEntry(UnitJournalEntryStatus.PendingApproval);
            SetupEntryQueryableWithIncludes(new List<UnitJournalEntry> { entry });
            _mockEntryRepository.Setup(r => r.UpdateAsync(It.IsAny<UnitJournalEntry>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            var result = await _service.ApproveAsync(entry.Id);

            // Assert
            result.Status.Should().Be("Approved");
            entry.ApprovedAt.Should().NotBeNull();
            entry.ApprovedBy.Should().Be(_userId);
        }

        [Fact]
        public async Task ApproveAsync_ThrowsException_WhenNotPending()
        {
            // Arrange
            var entry = CreateEntry(UnitJournalEntryStatus.Draft);
            SetupEntryQueryableWithIncludes(new List<UnitJournalEntry> { entry });

            // Act
            Func<Task> act = async () => await _service.ApproveAsync(entry.Id);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Only pending entries*");
        }

        #endregion

        #region Workflow: RejectAsync Tests

        [Fact]
        public async Task RejectAsync_ChangesStatusToRejected_WhenPending()
        {
            // Arrange
            var entry = CreateEntry(UnitJournalEntryStatus.PendingApproval);
            var reason = "Invalid data";
            SetupEntryQueryableWithIncludes(new List<UnitJournalEntry> { entry });
            _mockEntryRepository.Setup(r => r.UpdateAsync(It.IsAny<UnitJournalEntry>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            var result = await _service.RejectAsync(entry.Id, reason);

            // Assert
            result.Status.Should().Be("Rejected");
            entry.RejectionReason.Should().Be(reason);
        }

        [Fact]
        public async Task RejectAsync_ThrowsException_WhenNotPending()
        {
            // Arrange
            var entry = CreateEntry(UnitJournalEntryStatus.Draft);
            SetupEntryQueryableWithIncludes(new List<UnitJournalEntry> { entry });

            // Act
            Func<Task> act = async () => await _service.RejectAsync(entry.Id, "reason");

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Only pending entries*");
        }

        #endregion

        #region Workflow: PostAsync Tests

        [Fact]
        public async Task PostAsync_ChangesStatusToPosted_WhenApproved()
        {
            // Arrange
            var entry = CreateEntry(UnitJournalEntryStatus.Approved);
            var line = CreateLine(entry.Id);
            entry.Lines.Add(line);

            var account = CreateAccount(line.UnitAccountId, currentBalance: 100);

            SetupEntryQueryableWithIncludes(new List<UnitJournalEntry> { entry });
            SetupAccountQueryable(new List<UnitAccount> { account });
            SetupPeriodQueryable(new List<FiscalPeriod> { CreateFiscalPeriod(entry.FiscalPeriodId, entry.FiscalYearId) });
            _mockAccountRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitAccount, bool>>>()))
                .ReturnsAsync(account);
            _mockAccountRepository.Setup(r => r.UpdateAsync(It.IsAny<UnitAccount>())).Returns(Task.CompletedTask);
            _mockEntryRepository.Setup(r => r.UpdateAsync(It.IsAny<UnitJournalEntry>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            var result = await _service.PostAsync(entry.Id);

            // Assert
            result.Status.Should().Be("Posted");
            entry.PostedAt.Should().NotBeNull();
            account.CurrentBalance.Should().Be(110); // 100 + 10 (line quantity)
        }

        [Fact]
        public async Task PostAsync_ThrowsException_WhenNotApproved()
        {
            // Arrange
            var entry = CreateEntry(UnitJournalEntryStatus.Draft);
            SetupEntryQueryableWithIncludes(new List<UnitJournalEntry> { entry });

            // Act
            Func<Task> act = async () => await _service.PostAsync(entry.Id);

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Only approved entries*");
        }

        #endregion

        #region Workflow: ReverseAsync Tests

        [Fact]
        public async Task ReverseAsync_CreatesReversalEntry_WhenPosted()
        {
            // Arrange
            var entry = CreateEntry(UnitJournalEntryStatus.Posted);
            var line = CreateLine(entry.Id);
            entry.Lines.Add(line);

            var account = CreateAccount(line.UnitAccountId, currentBalance: 110);

            SetupEntryQueryableWithIncludes(new List<UnitJournalEntry> { entry });
            SetupAccountQueryable(new List<UnitAccount> { account });
            SetupPeriodQueryable(new List<FiscalPeriod>
            {
                CreateFiscalPeriod(entry.FiscalPeriodId, entry.FiscalYearId)
            });
            _mockAccountRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitAccount, bool>>>()))
                .ReturnsAsync(account);
            _mockAccountRepository.Setup(r => r.UpdateAsync(It.IsAny<UnitAccount>())).Returns(Task.CompletedTask);
            _mockEntryRepository
                .Setup(r => r.AddAsync(It.IsAny<UnitJournalEntry>()))
                .ReturnsAsync((UnitJournalEntry entity) => entity);
            _mockEntryRepository.Setup(r => r.UpdateAsync(It.IsAny<UnitJournalEntry>())).Returns(Task.CompletedTask);
            _mockLineRepository
                .Setup(r => r.AddAsync(It.IsAny<UnitJournalEntryLine>()))
                .ReturnsAsync((UnitJournalEntryLine entity) => entity);
            _mockUnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

            // Act
            var result = await _service.ReverseAsync(entry.Id, "Error correction");

            // Assert
            result.Should().NotBeNull();
            entry.Status.Should().Be(UnitJournalEntryStatus.Reversed);
            account.CurrentBalance.Should().Be(100); // 110 - 10 (reversed)
            _mockEntryRepository.Verify(r => r.AddAsync(It.IsAny<UnitJournalEntry>()), Times.Once);
        }

        [Fact]
        public async Task ReverseAsync_ThrowsException_WhenNotPosted()
        {
            // Arrange
            var entry = CreateEntry(UnitJournalEntryStatus.Approved);
            SetupEntryQueryableWithIncludes(new List<UnitJournalEntry> { entry });

            // Act
            Func<Task> act = async () => await _service.ReverseAsync(entry.Id, "reason");

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Only posted entries*");
        }

        #endregion

        #region Helper Methods

        private UnitJournalEntry CreateEntry(UnitJournalEntryStatus status)
        {
            return new UnitJournalEntry
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                EntryNumber = $"UJE-{DateTime.UtcNow.Year}-0001",
                EntryDate = DateTime.UtcNow,
                Description = "Test Entry",
                FiscalPeriodId = Guid.NewGuid(),
                FiscalYearId = Guid.NewGuid(),
                Status = status,
                Lines = new List<UnitJournalEntryLine>(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _userName
            };
        }

        private UnitJournalEntryLine CreateLine(Guid entryId)
        {
            return new UnitJournalEntryLine
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                UnitJournalEntryId = entryId,
                LineNumber = 1,
                UnitAccountId = Guid.NewGuid(),
                Quantity = 10,
                Description = "Test Line",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _userName
            };
        }

        private UnitAccount CreateAccount(Guid id, decimal currentBalance = 0m)
        {
            return new UnitAccount
            {
                Id = id,
                TenantId = _tenantId,
                AccountNumber = $"UNIT-{id:N}",
                Name = "Test Unit Account",
                IsActive = true,
                IsPostingAccount = true,
                CurrentBalance = currentBalance,
                ChildAccounts = new List<UnitAccount>()
            };
        }

        private FiscalPeriod CreateFiscalPeriod(Guid? id = null, Guid? fiscalYearId = null)
        {
            return new FiscalPeriod
            {
                Id = id ?? Guid.NewGuid(),
                TenantId = _tenantId,
                FiscalYearId = fiscalYearId ?? Guid.NewGuid(),
                PeriodName = "Test Period",
                StartDate = DateTime.UtcNow.Date.AddDays(-15),
                EndDate = DateTime.UtcNow.Date.AddDays(15),
                IsOpen = true,
                IsClosed = false,
                IsLocked = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _userName
            };
        }

        private void SetupEntryQueryable(List<UnitJournalEntry> entries)
        {
            _mockEntryRepository
                .Setup(r => r.GetQueryable(It.IsAny<Expression<Func<UnitJournalEntry, bool>>>()))
                .Returns((Expression<Func<UnitJournalEntry, bool>> predicate) =>
                    entries.Where(predicate.Compile()).AsAsyncQueryable());
        }

        private void SetupEntryQueryableWithIncludes(List<UnitJournalEntry> entries)
        {
            _mockEntryRepository
                .Setup(r => r.GetQueryable(It.IsAny<Expression<Func<UnitJournalEntry, bool>>>()))
                .Returns((Expression<Func<UnitJournalEntry, bool>> predicate) =>
                    entries.Where(predicate.Compile()).AsAsyncQueryable());
        }

        private void SetupAccountQueryable(List<UnitAccount> accounts)
        {
            _mockAccountRepository
                .Setup(r => r.GetQueryable(It.IsAny<Expression<Func<UnitAccount, bool>>>()))
                .Returns((Expression<Func<UnitAccount, bool>> predicate) =>
                    accounts.Where(predicate.Compile()).AsAsyncQueryable());
        }

        private void SetupPeriodQueryable(List<FiscalPeriod> periods)
        {
            _mockPeriodRepository
                .Setup(r => r.GetQueryable(It.IsAny<Expression<Func<FiscalPeriod, bool>>>()))
                .Returns((Expression<Func<FiscalPeriod, bool>> predicate) =>
                    periods.Where(predicate.Compile()).AsAsyncQueryable());
        }

        private void SetupBalanceRepository(List<UnitAccountBalance> balances)
        {
            _mockBalanceRepository
                .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UnitAccountBalance, bool>>>()))
                .ReturnsAsync((Expression<Func<UnitAccountBalance, bool>> predicate) =>
                    balances.FirstOrDefault(predicate.Compile()));
            _mockBalanceRepository
                .Setup(r => r.GetQueryable(It.IsAny<Expression<Func<UnitAccountBalance, bool>>>()))
                .Returns((Expression<Func<UnitAccountBalance, bool>> predicate) =>
                    balances.Where(predicate.Compile()).AsAsyncQueryable());
            _mockBalanceRepository
                .Setup(r => r.AddAsync(It.IsAny<UnitAccountBalance>()))
                .ReturnsAsync((UnitAccountBalance balance) =>
                {
                    balances.Add(balance);
                    return balance;
                });
            _mockBalanceRepository
                .Setup(r => r.UpdateAsync(It.IsAny<UnitAccountBalance>()))
                .Returns(Task.CompletedTask);
        }

        #endregion
    }
}
