using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using MockQueryable.Moq;
using Moq;
using AutoMapper;
using Xunit;

namespace ErpSystem.Tests.Services.Finance
{
    public class JournalEntryServicePeriodTests
    {
        private readonly Mock<IFinancialRepository> _mockRepo;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<IGeneralLedgerService> _mockGlService;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IGLSegmentSecurityService> _mockSegmentSecurityService;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly Mock<IDocumentSplittingService> _mockDocSplittingService;
        private readonly Mock<IBookValidationService> _mockBookValidationService;
        private readonly Mock<INotificationService> _mockNotificationService;

        private readonly JournalEntryService _service;
        private readonly Guid _tenantId = Guid.NewGuid();

        public JournalEntryServicePeriodTests()
        {
            _mockRepo = new Mock<IFinancialRepository>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockGlService = new Mock<IGeneralLedgerService>();
            _mockMapper = new Mock<IMapper>();
            _mockSegmentSecurityService = new Mock<IGLSegmentSecurityService>();
            _mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            _mockDocSplittingService = new Mock<IDocumentSplittingService>();
            _mockBookValidationService = new Mock<IBookValidationService>();
            _mockNotificationService = new Mock<INotificationService>();

            _mockCurrentUserService.Setup(s => s.TenantId).Returns(_tenantId);

            _service = new JournalEntryService(
                _mockRepo.Object,
                _mockCurrentUserService.Object,
                _mockGlService.Object,
                _mockMapper.Object,
                _mockSegmentSecurityService.Object,
                _mockConfig.Object,
                _mockDocSplittingService.Object,
                _mockBookValidationService.Object,
                _mockNotificationService.Object
            );
        }

        private FiscalPeriod SetupFiscalPeriod(string status)
        {
            var period = new FiscalPeriod
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                PeriodName = "Test Period",
                PeriodStatus = status,
                StartDate = DateTime.UtcNow.AddDays(-10),
                EndDate = DateTime.UtcNow.AddDays(10)
            };

            _mockRepo.Setup(r => r.GetByIdAsync<FiscalPeriod>(period.Id, It.IsAny<CancellationToken>()))
                     .ReturnsAsync(period);

            return period;
        }

        [Fact]
        public async Task CreateJournalEntryAsync_ThrowsException_WhenPeriodIsClosed()
        {
            var period = SetupFiscalPeriod("Closed");
            var dto = new CreateJournalEntryDto
            {
                FiscalPeriodId = period.Id,
                TransactionDate = DateTime.UtcNow,
                Transactions = new List<CreateAccountTransactionDto>
                {
                    new CreateAccountTransactionDto { AccountId = Guid.NewGuid(), TransactionType = "Debit", Amount = 100 },
                    new CreateAccountTransactionDto { AccountId = Guid.NewGuid(), TransactionType = "Credit", Amount = 100 }
                }
            };

            var accounts = dto.Transactions.Select(t => new Account { Id = t.AccountId, AccountCode = "ACC" }).AsQueryable().BuildMock();
            _mockRepo.Setup(r => r.Query<Account>()).Returns(accounts);

            _mockDocSplittingService.Setup(s => s.ApplyZeroBalanceClearingAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<CreateAccountTransactionDto>>(), It.IsAny<CancellationToken>()))
                                    .ReturnsAsync(dto.Transactions.ToList());

            Func<Task> act = async () => await _service.CreateJournalEntryAsync(dto);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage($"*Fiscal period '{period.PeriodName}' is Closed*");
        }

        [Fact]
        public async Task UpdateJournalEntryAsync_ThrowsException_WhenPeriodIsLocked()
        {
            var period = SetupFiscalPeriod("Locked");
            var entryId = Guid.NewGuid();
            var entry = new JournalEntry
            {
                Id = entryId,
                TenantId = _tenantId,
                FiscalPeriodId = period.Id,
                PostingStatus = "Draft",
                Transactions = new List<AccountTransaction>()
            };

            var mockQueryable = new List<JournalEntry> { entry }.AsQueryable().BuildMock();
            _mockRepo.Setup(r => r.Query<JournalEntry>()).Returns(mockQueryable);

            Func<Task> act = async () => await _service.UpdateJournalEntryAsync(entryId, new UpdateJournalEntryDto());

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage($"*Fiscal period '{period.PeriodName}' is Locked*");
        }

        [Fact]
        public async Task PostJournalEntryAsync_ThrowsException_WhenPeriodIsClosed()
        {
            var period = SetupFiscalPeriod("Closed");
            var entryId = Guid.NewGuid();
            var entry = new JournalEntry
            {
                Id = entryId,
                TenantId = _tenantId,
                FiscalPeriodId = period.Id,
                PostingStatus = "Draft",
                Transactions = new List<AccountTransaction>()
            };

            var mockQueryable = new List<JournalEntry> { entry }.AsQueryable().BuildMock();
            _mockRepo.Setup(r => r.Query<JournalEntry>()).Returns(mockQueryable);

            Func<Task> act = async () => await _service.PostJournalEntryAsync(entryId);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage($"*Fiscal period '{period.PeriodName}' is Closed*");
        }

        [Fact]
        public async Task DeleteJournalEntryAsync_ThrowsException_WhenPeriodIsLocked()
        {
            var period = SetupFiscalPeriod("Locked");
            var entryId = Guid.NewGuid();
            var entry = new JournalEntry
            {
                Id = entryId,
                TenantId = _tenantId,
                FiscalPeriodId = period.Id,
                PostingStatus = "Draft",
                Transactions = new List<AccountTransaction>()
            };

            _mockRepo.Setup(r => r.GetByIdAsync<JournalEntry>(entryId, It.IsAny<CancellationToken>())).ReturnsAsync(entry);

            Func<Task> act = async () => await _service.DeleteJournalEntryAsync(entryId);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage($"*Fiscal period '{period.PeriodName}' is Locked*");
        }
    }
}
