using System.Linq.Expressions;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class TenderPaymentFinancePostingTests
{
    [Fact]
    public async Task ApprovingPendingPaymentPostsBalancedTenderFeeReceiptAndRetainsFinanceLineage()
    {
        using var fixture = new Fixture();
        FinancePostingRequestDto? request = null;
        fixture.FinancePosting
            .Setup(engine => engine.PostAsync(
                It.IsAny<FinancePostingRequestDto>(),
                It.IsAny<CancellationToken>()))
            .Callback<FinancePostingRequestDto, CancellationToken>((value, _) => request = value)
            .ReturnsAsync(fixture.PostingResult);

        var result = await fixture.Service.VerifyPaymentAsync(
            fixture.Bid.Id,
            fixture.Payment.Id,
            new VerifyPaymentDto { IsApproved = true, Notes = "Bank receipt matched." });

        result.Status.Should().Be("Verified");
        fixture.Payment.PostingEventId.Should().Be(fixture.PostingResult.PostingEventId);
        fixture.Payment.JournalEntryId.Should().Be(fixture.PostingResult.JournalEntryId);
        fixture.Payment.PostedAtUtc.Should().NotBeNull();
        request.Should().NotBeNull();
        request!.SourceModule.Should().Be("Procurement");
        request.OriginModuleCode.Should().Be("PROC");
        request.SourceDocumentType.Should().Be("TenderFeePayment");
        request.SourceDocumentId.Should().Be(fixture.Payment.Id);
        request.SourceDocumentTenantId.Should().Be(fixture.TenantId);
        request.SourceDocumentReference.Should().Be(fixture.Payment.PaymentReference);
        request.FunctionalCurrencyCode.Should().Be("GHS");
        request.IdempotencyKey.Should().Be(
            $"PROCUREMENT|TENDER-FEE|{fixture.Payment.Id:N}|POST");
        request.ReturnExistingOnDuplicate.Should().BeTrue();
        request.Lines.Should().HaveCount(2);
        request.Lines.Should().ContainSingle(line =>
            line.AccountId == fixture.ReceivingAccount.Id &&
            line.DebitAmount == fixture.Payment.Amount &&
            line.CreditAmount == 0m);
        request.Lines.Should().ContainSingle(line =>
            line.AccountId == fixture.RevenueAccount.Id &&
            line.DebitAmount == 0m &&
            line.CreditAmount == fixture.Payment.Amount);
        request.Lines.Sum(line => line.DebitAmount)
            .Should().Be(request.Lines.Sum(line => line.CreditAmount));
        fixture.Payments.Verify(repository => repository.UpdateAsync(fixture.Payment), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetryingVerifiedPaymentWithFinanceLineageReturnsExistingWithoutDuplicatePosting()
    {
        using var fixture = new Fixture();
        fixture.Payment.Status = "Verified";
        fixture.Payment.PostingEventId = fixture.PostingResult.PostingEventId;
        fixture.Payment.JournalEntryId = fixture.PostingResult.JournalEntryId;
        fixture.Payment.PostedAtUtc = DateTime.UtcNow.AddMinutes(-1);

        var result = await fixture.Service.VerifyPaymentAsync(
            fixture.Bid.Id,
            fixture.Payment.Id,
            new VerifyPaymentDto { IsApproved = true });

        result.Status.Should().Be("Verified");
        fixture.FinancePosting.Verify(engine => engine.PostAsync(
            It.IsAny<FinancePostingRequestDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
        fixture.Payments.Verify(repository => repository.UpdateAsync(It.IsAny<TenderPayment>()), Times.Never);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LegacyVerifiedPaymentWithoutFinanceLineagePostsOnceAndBackfillsLineage()
    {
        using var fixture = new Fixture();
        fixture.Payment.Status = "Verified";
        fixture.Payment.VerifiedDate = DateTime.UtcNow.AddDays(-1);
        fixture.FinancePosting
            .Setup(engine => engine.PostAsync(
                It.IsAny<FinancePostingRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(fixture.PostingResult);

        var result = await fixture.Service.VerifyPaymentAsync(
            fixture.Bid.Id,
            fixture.Payment.Id,
            new VerifyPaymentDto { IsApproved = true });

        result.Status.Should().Be("Verified");
        fixture.Payment.PostingEventId.Should().Be(fixture.PostingResult.PostingEventId);
        fixture.Payment.JournalEntryId.Should().Be(fixture.PostingResult.JournalEntryId);
        fixture.FinancePosting.Verify(engine => engine.PostAsync(
            It.Is<FinancePostingRequestDto>(request =>
                request.IdempotencyKey ==
                $"PROCUREMENT|TENDER-FEE|{fixture.Payment.Id:N}|POST" &&
                request.ReturnExistingOnDuplicate),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Payments.Verify(repository => repository.UpdateAsync(fixture.Payment), Times.Once);
    }

    [Fact]
    public async Task RejectingPaymentDoesNotPostToFinance()
    {
        using var fixture = new Fixture();

        var result = await fixture.Service.VerifyPaymentAsync(
            fixture.Bid.Id,
            fixture.Payment.Id,
            new VerifyPaymentDto { IsApproved = false, Notes = "Receipt could not be confirmed." });

        result.Status.Should().Be("Rejected");
        fixture.Payment.PostingEventId.Should().BeNull();
        fixture.Payment.JournalEntryId.Should().BeNull();
        fixture.Payment.PostedAtUtc.Should().BeNull();
        fixture.FinancePosting.Verify(engine => engine.PostAsync(
            It.IsAny<FinancePostingRequestDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
        fixture.Payments.Verify(repository => repository.UpdateAsync(fixture.Payment), Times.Once);
    }

    [Fact]
    public async Task ApprovingPaymentRejectsAControlAccountBeforeFinancePosting()
    {
        using var fixture = new Fixture();
        fixture.MarkReceivingAccountAsControl();

        var action = () => fixture.Service.VerifyPaymentAsync(
            fixture.Bid.Id,
            fixture.Payment.Id,
            new VerifyPaymentDto { IsApproved = true });

        await action.Should()
            .ThrowAsync<TenderBidInitiationValidationException>()
            .WithMessage("*non-control asset account*");
        fixture.Payment.Status.Should().Be("Pending");
        fixture.FinancePosting.Verify(engine => engine.PostAsync(
            It.IsAny<FinancePostingRequestDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
        fixture.UnitOfWork.Verify(unit => unit.RollbackAsync(
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RejectingFinancePostedPaymentRequiresControlledReversal()
    {
        using var fixture = new Fixture();
        fixture.Payment.PostingEventId = fixture.PostingResult.PostingEventId;
        fixture.Payment.JournalEntryId = fixture.PostingResult.JournalEntryId;
        fixture.Payment.PostedAtUtc = DateTime.UtcNow.AddMinutes(-1);

        var action = () => fixture.Service.VerifyPaymentAsync(
            fixture.Bid.Id,
            fixture.Payment.Id,
            new VerifyPaymentDto { IsApproved = false, Notes = "Reject" });

        await action.Should()
            .ThrowAsync<TenderBidInitiationValidationException>()
            .WithMessage("*controlled Finance reversal*");
        fixture.Payment.Status.Should().Be("Pending");
        fixture.FinancePosting.Verify(engine => engine.PostAsync(
            It.IsAny<FinancePostingRequestDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FinancePostingFailureLeavesPaymentPendingAndRollsBackDecision()
    {
        using var fixture = new Fixture();
        fixture.FinancePosting
            .Setup(engine => engine.PostAsync(
                It.IsAny<FinancePostingRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Finance posting unavailable."));

        var action = () => fixture.Service.VerifyPaymentAsync(
            fixture.Bid.Id,
            fixture.Payment.Id,
            new VerifyPaymentDto { IsApproved = true });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Finance posting unavailable.");
        fixture.Payment.Status.Should().Be("Pending");
        fixture.Payment.VerifiedDate.Should().BeNull();
        fixture.Payment.VerifiedById.Should().BeNull();
        fixture.Payment.PostingEventId.Should().BeNull();
        fixture.Payment.JournalEntryId.Should().BeNull();
        fixture.Payment.PostedAtUtc.Should().BeNull();
        fixture.Payments.Verify(repository => repository.UpdateAsync(It.IsAny<TenderPayment>()), Times.Never);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        fixture.UnitOfWork.Verify(unit => unit.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.ClearTrackedChanges(), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void MigrationIsDiscoverableAndContainsThePostingLineageSchema()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=TenderFeePostingMigrationDiscovery;Trusted_Connection=True")
            .Options;
        using var context = new ApplicationDbContext(options);

        context.GetService<IMigrationsAssembly>().Migrations.Should()
            .ContainKey("20260831203000_AddTenderFeeFinancePostingLineage");

        var migration = new AddTenderFeeFinancePostingLineage();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType()
            .GetMethod("Up", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { builder });

        builder.Operations.OfType<AddColumnOperation>()
            .Select(operation => $"{operation.Table}.{operation.Name}")
            .Should().BeEquivalentTo(
                "TenderFees.ReceivingAccountId",
                "TenderFees.RevenueAccountId",
                "TenderPayments.PostingEventId",
                "TenderPayments.JournalEntryId",
                "TenderPayments.PostedAtUtc");
        builder.Operations.OfType<CreateIndexOperation>()
            .Where(operation => operation.Table == "TenderPayments")
            .Should().OnlyContain(operation =>
                operation.IsUnique && operation.Filter != null);
        builder.Operations.OfType<AddForeignKeyOperation>()
            .Select(operation => operation.PrincipalTable)
            .Should().BeEquivalentTo(
                "Accounts", "Accounts", "FinancePostingEvents", "JournalEntries");
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ApplicationDbContext _db;

        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid UserId { get; } = Guid.NewGuid();
        public TenderBid Bid { get; }
        public TenderFee Fee { get; }
        public TenderPayment Payment { get; }
        public Account ReceivingAccount { get; }
        public Account RevenueAccount { get; }
        public FinancePostingResultDto PostingResult { get; }
        public Mock<ITenderPaymentRepository> Payments { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IFinancePostingEngine> FinancePosting { get; } = new();
        public TenderBidService Service { get; }

        public Fixture()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"tender-payment-finance-{Guid.NewGuid():N}")
                .Options;
            _db = new ApplicationDbContext(options, TenantId);

            ReceivingAccount = Account(
                Guid.NewGuid(), "100-1001-0000", "Tender fee receipts", AccountType.Asset);
            RevenueAccount = Account(
                Guid.NewGuid(), "000-4300-0000", "Tender fee income", AccountType.Revenue);
            _db.Accounts.AddRange(ReceivingAccount, RevenueAccount);
            _db.FinanceSettings.Add(new FinanceSettings
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BaseCurrency = "GHS",
                CreatedAt = DateTime.UtcNow
            });
            _db.SaveChanges();

            var tenderId = Guid.NewGuid();
            var partnerId = Guid.NewGuid();
            Bid = new TenderBid
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderId = tenderId,
                BusinessPartnerId = partnerId,
                BidNumber = "BID-FIN-001",
                Status = "Submitted",
                Currency = "GHS",
                CreatedAt = DateTime.UtcNow
            };
            Fee = new TenderFee
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderId = tenderId,
                FeeType = "TenderFee",
                Amount = 500m,
                Currency = "GHS",
                PaymentMethod = "BankTransfer",
                ReceivingAccountId = ReceivingAccount.Id,
                RevenueAccountId = RevenueAccount.Id,
                CreatedAt = DateTime.UtcNow
            };
            Payment = new TenderPayment
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                TenderFeeId = Fee.Id,
                BusinessPartnerId = partnerId,
                PaymentReference = "PAY-2026-0001",
                TransactionId = "BANK-REF-001",
                Amount = Fee.Amount,
                Currency = Fee.Currency,
                PaymentMethod = Fee.PaymentMethod,
                Status = "Pending",
                PaymentDate = DateTime.UtcNow.AddHours(-1),
                CreatedAt = DateTime.UtcNow.AddHours(-1)
            };
            PostingResult = new FinancePostingResultDto
            {
                PostingEventId = Guid.NewGuid(),
                JournalEntryId = Guid.NewGuid(),
                JournalEntryNumber = "JE-2026-0001",
                PostingStatus = "Posted",
                FunctionalCurrencyCode = "GHS",
                TotalDebitAmount = Payment.Amount,
                TotalCreditAmount = Payment.Amount,
                SourceModule = "Procurement",
                OriginModuleCode = "PROC",
                SourceDocumentType = "TenderFeePayment",
                SourceDocumentId = Payment.Id,
                PostingAction = "Post"
            };

            var bids = new Mock<ITenderBidRepository>();
            var fees = new Mock<ITenderFeeRepository>();
            var currentUser = new Mock<ICurrentUserProvider>();
            var events = new Mock<IAppEventBus>();
            var accountRepository = new Mock<IGenericRepository<Account>>();
            var settingsRepository = new Mock<IGenericRepository<FinanceSettings>>();

            bids.Setup(repository => repository.GetByIdAsync(Bid.Id)).ReturnsAsync(Bid);
            fees.Setup(repository => repository.GetByIdAsync(Fee.Id)).ReturnsAsync(Fee);
            Payments.Setup(repository => repository.GetByIdAsync(Payment.Id)).ReturnsAsync(Payment);
            Payments.Setup(repository => repository.UpdateAsync(It.IsAny<TenderPayment>()))
                .ReturnsAsync((TenderPayment value) => value);
            currentUser.SetupGet(provider => provider.TenantId).Returns(TenantId);
            currentUser.SetupGet(provider => provider.UserId).Returns(UserId);
            events.Setup(bus => bus.PublishAsync(
                    It.IsAny<EntityActivityEvent>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            accountRepository
                .Setup(repository => repository.GetQueryable(
                    It.IsAny<Expression<Func<Account, bool>>>()))
                .Returns((Expression<Func<Account, bool>> predicate) =>
                    _db.Accounts.Where(predicate));
            settingsRepository
                .Setup(repository => repository.GetQueryable(
                    It.IsAny<Expression<Func<FinanceSettings, bool>>>()))
                .Returns((Expression<Func<FinanceSettings, bool>> predicate) =>
                    _db.FinanceSettings.Where(predicate));
            UnitOfWork.Setup(unit => unit.Repository<Account>()).Returns(accountRepository.Object);
            UnitOfWork.Setup(unit => unit.Repository<FinanceSettings>()).Returns(settingsRepository.Object);
            UnitOfWork.Setup(unit => unit.ExecuteInStrategyAsync(
                    It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
                .Returns((Func<Task> operation, CancellationToken _) => operation());
            UnitOfWork.Setup(unit => unit.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            UnitOfWork.Setup(unit => unit.AcquireTransactionLockAsync(
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            UnitOfWork.Setup(unit => unit.CommitAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            UnitOfWork.Setup(unit => unit.RollbackAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            UnitOfWork.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);

            Service = new TenderBidService(
                bids.Object,
                Mock.Of<ITenderRepository>(),
                Mock.Of<ITenderBidItemRepository>(),
                Mock.Of<ITenderBidDocumentRepository>(),
                Payments.Object,
                fees.Object,
                Mock.Of<ITenderInterviewRepository>(),
                Mock.Of<ITenderAssignmentRepository>(),
                Mock.Of<ITenderBidLotRepository>(),
                Mock.Of<ITenderNotificationService>(),
                Mock.Of<IBusinessPartnerRepository>(),
                Mock.Of<IBusinessPartnerUserRepository>(),
                Mock.Of<ISupplierValidationService>(),
                UnitOfWork.Object,
                currentUser.Object,
                events.Object,
                Mock.Of<IProcurementTenderControlService>(),
                Mock.Of<IProcurementTenderDocumentControlService>(),
                Mock.Of<IProcurementExceptionalSourcingControlService>(),
                Mock.Of<IQuantitySurveyTenderBoqSubmissionService>(),
                Mock.Of<ILogger<TenderBidService>>(),
                FinancePosting.Object);
        }

        public void Dispose() => _db.Dispose();

        public void MarkReceivingAccountAsControl()
        {
            ReceivingAccount.IsControlAccount = true;
            _db.SaveChanges();
        }

        private Account Account(Guid id, string number, string name, AccountType type) => new()
        {
            Id = id,
            TenantId = TenantId,
            AccountCode = number,
            AccountNumber = number,
            AccountName = name,
            AccountType = type,
            Status = AccountStatus.Active,
            CurrencyCode = "GHS",
            AllowDirectPosting = true,
            CreatedAt = DateTime.UtcNow
        };
    }
}
