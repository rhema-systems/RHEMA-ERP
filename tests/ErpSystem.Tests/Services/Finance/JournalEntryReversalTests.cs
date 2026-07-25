using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using MockQueryable.Moq;
using Moq;
using AutoMapper;
using Xunit;

namespace ErpSystem.Tests.Services.Finance
{
    public class JournalEntryReversalTests
    {
        private readonly Mock<IFinancialRepository> _mockRepo;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<IGeneralLedgerService> _mockGlService;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IGLSegmentSecurityService> _mockSegmentSecurityService;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly Mock<IDocumentSplittingService> _mockDocSplittingService;
        private readonly Mock<IBookValidationService> _mockBookValidationService;

        private readonly JournalEntryService _service;
        private readonly Guid _tenantId = Guid.NewGuid();

        public JournalEntryReversalTests()
        {
            _mockRepo = new Mock<IFinancialRepository>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockGlService = new Mock<IGeneralLedgerService>();
            _mockMapper = new Mock<IMapper>();
            _mockSegmentSecurityService = new Mock<IGLSegmentSecurityService>();
            var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
            var mockDocumentSplittingService = new Mock<IDocumentSplittingService>();
            var mockBookValidationService = new Mock<IBookValidationService>();
            var mockNotificationService = new Mock<INotificationService>();

            _mockCurrentUserService.Setup(s => s.TenantId).Returns(_tenantId);
            _mockCurrentUserService.Setup(s => s.UserName).Returns("test-user");

            _mockGlService.Setup(s => s.GenerateJournalEntryNumberAsync(It.IsAny<CancellationToken>()))
                          .ReturnsAsync("JE-REV-001");

            _service = new JournalEntryService(
                _mockRepo.Object,
                _mockCurrentUserService.Object,
                _mockGlService.Object,
                _mockMapper.Object,
                _mockSegmentSecurityService.Object,
                mockConfig.Object,
                mockDocumentSplittingService.Object,
                mockBookValidationService.Object,
                mockNotificationService.Object
            );
        }

        private FiscalPeriod SetupFiscalPeriod(string status)
        {
            var period = new FiscalPeriod
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                PeriodName = "Current Period",
                PeriodStatus = status,
                StartDate = DateTime.UtcNow.AddDays(-10),
                EndDate = DateTime.UtcNow.AddDays(10)
            };

            var mockQueryable = new List<FiscalPeriod> { period }.AsQueryable().BuildMock();
            _mockRepo.Setup(r => r.Query<FiscalPeriod>()).Returns(mockQueryable);

            return period;
        }

        private JournalEntry SetupJournalEntry(string postingStatus = "Posted", bool isReversed = false)
        {
            var entryId = Guid.NewGuid();
            var txn1Id = Guid.NewGuid();
            var txn2Id = Guid.NewGuid();

            var entry = new JournalEntry
            {
                Id = entryId,
                TenantId = _tenantId,
                PostingStatus = postingStatus,
                IsReversed = isReversed,
                ReferenceNumber = "INV-001",
                SourceModule = "AP",
                TotalDebitAmount = 500,
                TotalCreditAmount = 500,
                BookClassification = "IFRS",
                Transactions = new List<AccountTransaction>
                {
                    new AccountTransaction 
                    { 
                        Id = txn1Id, AccountId = Guid.NewGuid(), DebitAmount = 500, CreditAmount = 0,
                        SourceModule = "AP", SourceDocumentId = Guid.NewGuid(), SegmentString = "01-FIN"
                    },
                    new AccountTransaction 
                    { 
                        Id = txn2Id, AccountId = Guid.NewGuid(), DebitAmount = 0, CreditAmount = 500,
                        SourceModule = "AP", SourceDocumentId = Guid.NewGuid(), SegmentString = "01-FIN"
                    }
                }
            };

            var mockQueryable = new List<JournalEntry> { entry }.AsQueryable().BuildMock();
            _mockRepo.Setup(r => r.Query<JournalEntry>()).Returns(mockQueryable);
            _mockRepo.Setup(r => r.GetByIdAsync<Account>(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new Account { Balance = 1000 });

            // Setup mapper mock for returning the created reversal
            _mockMapper.Setup(m => m.Map<JournalEntryDto>(It.IsAny<JournalEntry>()))
                       .Returns((JournalEntry je) => new JournalEntryDto { Id = je.Id, PostingStatus = je.PostingStatus });

            return entry;
        }

        [Fact]
        public async Task ReverseJournalEntryAsync_SuccessfullyReverses_WhenPeriodIsOpen()
        {
            SetupFiscalPeriod("Open");
            var original = SetupJournalEntry("Posted");

            // Capture the inserted reversal entry
            JournalEntry savedReversal = null;
            _mockRepo.Setup(r => r.Add(It.IsAny<JournalEntry>())).Callback<JournalEntry>(je => savedReversal = je);

            var result = await _service.ReverseJournalEntryAsync(original.Id, "Correction needed");

            result.Should().NotBeNull();
            savedReversal.Should().NotBeNull();
            
            // Check original is marked reversed
            original.IsReversed.Should().BeTrue();
            original.ReversalReason.Should().Be("Correction needed");
            original.ReversalJournalEntryId.Should().Be(savedReversal.Id);

            // Check reversal entry correctly maps
            savedReversal.OriginalJournalEntryId.Should().Be(original.Id);
            savedReversal.TotalDebitAmount.Should().Be(original.TotalCreditAmount);
            savedReversal.TotalCreditAmount.Should().Be(original.TotalDebitAmount);

            // Check subledger context & lines preservation
            savedReversal.Transactions.Should().HaveCount(2);
            var revTxn1 = savedReversal.Transactions.ElementAt(0);
            var origTxn1 = original.Transactions.ElementAt(0);
            
            revTxn1.OriginalTransactionId.Should().Be(origTxn1.Id);
            origTxn1.ReversalTransactionId.Should().Be(revTxn1.Id);
            
            revTxn1.DebitAmount.Should().Be(origTxn1.CreditAmount);
            revTxn1.CreditAmount.Should().Be(origTxn1.DebitAmount);
            revTxn1.SegmentString.Should().Be(origTxn1.SegmentString);
            revTxn1.SourceModule.Should().Be(origTxn1.SourceModule);
            revTxn1.SourceDocumentId.Should().Be(origTxn1.SourceDocumentId);
        }

        [Fact]
        public async Task ReverseJournalEntryAsync_ThrowsException_WhenReasonIsEmpty()
        {
            var original = SetupJournalEntry();

            Func<Task> act = async () => await _service.ReverseJournalEntryAsync(original.Id, "");

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("A reversal reason must be provided.");
        }

        [Fact]
        public async Task ReverseJournalEntryAsync_ThrowsException_WhenPeriodIsNotOpen()
        {
            SetupFiscalPeriod("Closed");
            var original = SetupJournalEntry();

            Func<Task> act = async () => await _service.ReverseJournalEntryAsync(original.Id, "Reason");

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*Reversals can only be posted into an Open fiscal period*");
        }

        [Fact]
        public async Task ReverseJournalEntryAsync_ThrowsException_WhenAlreadyReversed()
        {
            SetupFiscalPeriod("Open");
            var original = SetupJournalEntry(isReversed: true);

            Func<Task> act = async () => await _service.ReverseJournalEntryAsync(original.Id, "Reason");

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Journal entry is already reversed.");
        }

        [Fact]
        public async Task ReverseJournalEntryAsync_ThrowsException_WhenExistingReversalFoundInDb()
        {
            SetupFiscalPeriod("Open");
            var original = SetupJournalEntry(isReversed: false); // IsReversed flag was somehow false

            // Mock DB returning a reversal journal
            var mockQueryable = new List<JournalEntry> 
            { 
                original, 
                new JournalEntry { OriginalJournalEntryId = original.Id } 
            }.AsQueryable().BuildMock();
            
            _mockRepo.Setup(r => r.Query<JournalEntry>()).Returns(mockQueryable);

            Func<Task> act = async () => await _service.ReverseJournalEntryAsync(original.Id, "Reason");

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("A reversal journal already exists for this journal entry.");
        }

        [Fact]
        public async Task ReverseJournalEntryAsync_ThrowsException_WhenEntryNotPosted()
        {
            var original = SetupJournalEntry("Draft");

            Func<Task> act = async () => await _service.ReverseJournalEntryAsync(original.Id, "Reason");

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("Only posted entries can be reversed.");
        }
    }
}
