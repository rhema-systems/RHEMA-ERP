using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using Moq;
using MockQueryable.Moq;
using Xunit;
using AutoMapper;

namespace ErpSystem.Tests.Services.Finance.GL
{
    public class JournalEntryBudgetTests
    {
        private readonly Mock<IFinancialRepository> _mockRepo;
        private readonly Mock<ICurrentUserService> _mockUserService;
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly JournalEntryService _service;
        private readonly Guid _tenantId = Guid.NewGuid();
        private readonly Guid _userId = Guid.NewGuid();

        public JournalEntryBudgetTests()
        {
            _mockRepo = new Mock<IFinancialRepository>();
            _mockUserService = new Mock<ICurrentUserService>();
            _mockNotificationService = new Mock<INotificationService>();

            _mockUserService.Setup(u => u.TenantId).Returns(_tenantId);
            _mockUserService.Setup(u => u.UserId).Returns(_userId.ToString());

            var mockGeneralLedgerService = new Mock<IGeneralLedgerService>();
            var mockMapper = new Mock<IMapper>();
            var mockSegmentSecurityService = new Mock<IGLSegmentSecurityService>();
            var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            var mockDocumentSplittingService = new Mock<IDocumentSplittingService>();
            var mockBookValidationService = new Mock<IBookValidationService>();

            _service = new JournalEntryService(
                _mockRepo.Object,
                _mockUserService.Object,
                mockGeneralLedgerService.Object,
                mockMapper.Object,
                mockSegmentSecurityService.Object,
                mockConfig.Object,
                mockDocumentSplittingService.Object,
                mockBookValidationService.Object,
                _mockNotificationService.Object
            );
        }

        [Fact]
        public async Task JournalPosting_ShouldCreateBudgetExceptionNotification()
        {
            // Arrange
            var entryId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var creditAccountId = Guid.NewGuid();
            var periodId = Guid.NewGuid();

            var account = new Account
            {
                Id = accountId,
                TenantId = _tenantId,
                AccountNumber = "6000",
                AccountName = "Operating Expenses",
                AccountType = AccountType.Expense,
                Balance = 500, // existing balance
                BudgetTrackingEnabled = true
            };

            var entry = new JournalEntry
            {
                Id = entryId,
                TenantId = _tenantId,
                JournalEntryNumber = "JE-001",
                FiscalPeriodId = periodId,
                Transactions = new List<AccountTransaction>
                {
                    new AccountTransaction
                    {
                        AccountId = accountId,
                        DebitAmount = 1000, // brings total to 1500
                        CreditAmount = 0
                    },
                    new AccountTransaction
                    {
                        AccountId = creditAccountId,
                        DebitAmount = 0,
                        CreditAmount = 1000
                    }
                }
            };

            var budgetEntries = new List<BudgetEntry>
            {
                new BudgetEntry
                {
                    AccountId = accountId,
                    FiscalPeriodId = periodId,
                    AmountBase = 1200, // Budget is 1200
                    BudgetReturn = new BudgetReturn
                    {
                        Status = "Approved",
                        BudgetScenario = new BudgetScenario
                        {
                            IsActive = true
                        }
                    }
                }
            };

            _mockRepo.Setup(r => r.GetByIdAsync<JournalEntry>(entryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entry);
            _mockRepo.Setup(r => r.Query<JournalEntry>())
                .Returns(new List<JournalEntry> { entry }.AsQueryable().BuildMock());
            _mockRepo.Setup(r => r.GetByIdAsync<Account>(accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);
            _mockRepo.Setup(r => r.Query<BudgetEntry>())
                .Returns(budgetEntries.AsQueryable().BuildMock());

            var creditAccount = new Account
            {
                Id = creditAccountId,
                TenantId = _tenantId,
                AccountNumber = "1000",
                AccountName = "Cash",
                AccountType = AccountType.Asset
            };
            
            _mockRepo.Setup(r => r.GetByIdAsync<Account>(creditAccountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(creditAccount);

            // Add setup for ValidateManualPostingAllowedAsync
            _mockRepo.Setup(r => r.Query<Account>())
                .Returns(new List<Account> { account, creditAccount }.AsQueryable().BuildMock());
            
            // Act
            await _service.PostJournalEntryAsync(entryId);

            // Assert
            _mockNotificationService.Verify(n => n.CreateInAppNotificationAsync(
                _userId,
                "Budget Exception Alert",
                It.Is<string>(msg => msg.Contains("Budget exceeded after posting journal entry JE-001")),
                "BudgetException",
                It.IsAny<Dictionary<string, object>>(),
                _tenantId), Times.Once);
        }

        [Fact]
        public async Task JournalPosting_ShouldNotCreateBudgetExceptionNotification_WhenUnderBudget()
        {
            // Arrange
            var entryId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var creditAccountId = Guid.NewGuid();
            var periodId = Guid.NewGuid();

            var account = new Account
            {
                Id = accountId,
                TenantId = _tenantId,
                AccountNumber = "6000",
                AccountName = "Operating Expenses",
                AccountType = AccountType.Expense,
                Balance = 500, // existing balance
                BudgetTrackingEnabled = true
            };

            var entry = new JournalEntry
            {
                Id = entryId,
                TenantId = _tenantId,
                JournalEntryNumber = "JE-001",
                FiscalPeriodId = periodId,
                Transactions = new List<AccountTransaction>
                {
                    new AccountTransaction
                    {
                        AccountId = accountId,
                        DebitAmount = 200, // brings total to 700
                        CreditAmount = 0
                    },
                    new AccountTransaction
                    {
                        AccountId = creditAccountId,
                        DebitAmount = 0,
                        CreditAmount = 200
                    }
                }
            };

            var budgetEntries = new List<BudgetEntry>
            {
                new BudgetEntry
                {
                    AccountId = accountId,
                    FiscalPeriodId = periodId,
                    AmountBase = 1200, // Budget is 1200
                    BudgetReturn = new BudgetReturn
                    {
                        Status = "Approved",
                        BudgetScenario = new BudgetScenario
                        {
                            IsActive = true
                        }
                    }
                }
            };

            _mockRepo.Setup(r => r.GetByIdAsync<JournalEntry>(entryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entry);
            _mockRepo.Setup(r => r.Query<JournalEntry>())
                .Returns(new List<JournalEntry> { entry }.AsQueryable().BuildMock());
            _mockRepo.Setup(r => r.GetByIdAsync<Account>(accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(account);
            _mockRepo.Setup(r => r.Query<BudgetEntry>())
                .Returns(budgetEntries.AsQueryable().BuildMock());

            var creditAccount = new Account
            {
                Id = creditAccountId,
                TenantId = _tenantId,
                AccountNumber = "1000",
                AccountName = "Cash",
                AccountType = AccountType.Asset
            };
            
            _mockRepo.Setup(r => r.GetByIdAsync<Account>(creditAccountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(creditAccount);

            // Add setup for ValidateManualPostingAllowedAsync
            _mockRepo.Setup(r => r.Query<Account>())
                .Returns(new List<Account> { account, creditAccount }.AsQueryable().BuildMock());

            // Act
            await _service.PostJournalEntryAsync(entryId);

            // Assert
            _mockNotificationService.Verify(n => n.CreateInAppNotificationAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<Guid>()), Times.Never);
        }
    }
}
