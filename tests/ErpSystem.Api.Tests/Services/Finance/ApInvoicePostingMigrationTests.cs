using System.Globalization;
using System.Reflection;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

#pragma warning disable CS0618 // Regression tests intentionally assert that obsolete legacy posting paths are not used.

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class ApInvoicePostingMigrationTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-APBudget")]
    [Trait("Category", "AccountsPayable")]
    public void BudgetEvidenceMigration_ShouldAddOnlyTheNullableApLineReferenceAndReverseCleanly()
    {
        var migration = new AddApVendorInvoiceBudgetEvidence();
        var up = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { up });

        up.Operations.OfType<AddColumnOperation>().Should().ContainSingle(column =>
            column.Table == "VendorInvoiceLineItem" &&
            column.Name == "BudgetEntryId" &&
            column.IsNullable);
        up.Operations.OfType<CreateIndexOperation>().Select(index => index.Name).Should().BeEquivalentTo(
            "IX_VendorInvoiceLineItem_BudgetEntryId",
            "IX_VendorInvoiceLineItem_TenantId_BudgetEntryId");
        up.Operations.OfType<AddForeignKeyOperation>().Should().ContainSingle(foreignKey =>
            foreignKey.PrincipalTable == "BudgetEntries" &&
            foreignKey.OnDelete == ReferentialAction.Restrict);

        var down = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Down", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { down });
        down.Operations.OfType<DropColumnOperation>().Should().ContainSingle(column =>
            column.Table == "VendorInvoiceLineItem" && column.Name == "BudgetEntryId");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task ApprovedApInvoice_ShouldPostThroughFinancePostingEngineAndCreateAuditEvent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId);
        var (service, subledgerPostingMock) = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.Invoice.Id);

        result.JournalEntryId.Should().NotBeNull();
        subledgerPostingMock.Verify(x => x.PostApInvoiceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        var postingEvent = await db.FinancePostingEvents.SingleAsync(e =>
            e.TenantId == tenantId &&
            e.SourceModule == "AP" &&
            e.SourceDocumentType == "VendorInvoice" &&
            e.SourceDocumentId == fixture.Invoice.Id &&
            e.PostingAction == "Post");
        postingEvent.JournalEntryId.Should().Be(result.JournalEntryId);

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == result.JournalEntryId);
        journal.PostingStatus.Should().Be("Posted");
        journal.SourceModule.Should().Be("AP");
        journal.SourceDocumentType.Should().Be("VendorInvoice");
        journal.Transactions.Should().HaveCount(2);
        journal.Transactions.Single(t => t.AccountId == fixture.ExpenseAccount.Id).DebitAmount.Should().Be(100m);
        journal.Transactions.Single(t => t.AccountId == fixture.ApAccount.Id).CreditAmount.Should().Be(100m);

        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.ApInvoicePosted && a.TenantId == tenantId)).Should().Be(1);
        // The posting engine keeps Account.Balance as a read-side snapshot for legacy balance APIs.
        fixture.ExpenseAccount.Balance.Should().Be(100m);
        fixture.ApAccount.Balance.Should().Be(100m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task LineTradeDiscounts_ShouldReduceExpenseAndRetainSourceDimensionCombinations()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, invoice =>
        {
            var original = invoice.LineItems.Single();
            original.DiscountPercentage = 10m;
            original.DiscountAmount = 10m;
            invoice.LineItems.Add(new VendorInvoiceLineItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                VendorInvoiceId = invoice.Id,
                LineItemType = "Expense",
                GLAccountId = original.GLAccountId,
                Description = "Implementation services",
                Quantity = 1m,
                UnitPrice = 50m,
                DiscountPercentage = 10m,
                DiscountAmount = 5m,
                CreatedAt = DateTime.UtcNow.AddSeconds(1),
                CreatedBy = "seed"
            });
            invoice.SubTotal = 150m;
            invoice.DiscountAmount = 15m;
            invoice.TotalAmount = 135m;
            invoice.BaseCurrencyAmount = 135m;
        });
        var lines = fixture.Invoice.LineItems.OrderBy(line => line.CreatedAt).ToList();
        var firstLine = lines[0];
        var secondLine = lines[1];

        var sourceDimensions = new Mock<IFinanceSourceDimensionService>();
        sourceDimensions.Setup(service => service.GetPostingDimensionsAsync(
                It.IsAny<FinancePostingProducerContext>(),
                fixture.Invoice.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<FinancePostingDimensionValueDto>>
            {
                [firstLine.Id] = new[] { new FinancePostingDimensionValueDto { DimensionCode = "DEPARTMENT", ValueCode = "FIN" } },
                [secondLine.Id] = new[] { new FinancePostingDimensionValueDto { DimensionCode = "DEPARTMENT", ValueCode = "OPS" } }
            });
        var (service, _) = CreateService(db, tenantId, sourceDimensions: sourceDimensions.Object);
        var build = typeof(VendorInvoiceService).GetMethod(
            "BuildApInvoicePostingRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        var request = await (Task<FinancePostingRequestDto>)build.Invoke(service, new object[]
        {
            fixture.Invoice,
            Array.Empty<Guid>(),
            new FinancePostingProducerContext(FinanceDimensionRouteId.FinanceApVendorInvoice),
            CancellationToken.None
        })!;

        request.Lines.Should().NotContain(line => line.TransactionTag == "AP-Discount");
        var expenses = request.Lines.Where(line => line.TransactionTag == "AP-Expense").ToList();
        expenses.Should().HaveCount(2);
        expenses.Single(line => line.SourceDocumentLineId == firstLine.Id)
            .TransactionDebitAmount.Should().Be(90m);
        expenses.Single(line => line.SourceDocumentLineId == firstLine.Id)
            .Dimensions.Should().ContainSingle(value => value.DimensionCode == "DEPARTMENT" && value.ValueCode == "FIN");
        expenses.Single(line => line.SourceDocumentLineId == secondLine.Id)
            .TransactionDebitAmount.Should().Be(45m);
        expenses.Single(line => line.SourceDocumentLineId == secondLine.Id)
            .Dimensions.Should().ContainSingle(value => value.DimensionCode == "DEPARTMENT" && value.ValueCode == "OPS");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task InvoiceTradeDiscount_ShouldPostWithoutDiscountReceivedAccount()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, invoice =>
        {
            var line = invoice.LineItems.Single();
            line.DiscountPercentage = 10m;
            line.DiscountAmount = 10m;
            invoice.SubTotal = 90m;
            invoice.DiscountAmount = 10m;
            invoice.TotalAmount = 90m;
            invoice.BaseCurrencyAmount = 90m;
        });
        var settings = await db.FinanceSettings.SingleAsync(item => item.TenantId == tenantId);
        settings.DiscountReceivedAccountId = null;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.Invoice.Id);

        var journal = await db.JournalEntries.Include(item => item.Transactions)
            .SingleAsync(item => item.Id == result.JournalEntryId);
        journal.Transactions.Should().HaveCount(2);
        journal.Transactions.Single(item => item.AccountId == fixture.ApAccount.Id)
            .CreditAmount.Should().Be(90m);
        journal.Transactions.Single(item => item.AccountId == fixture.ExpenseAccount.Id)
            .DebitAmount.Should().Be(90m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task CreateInvoice_ShouldApplyLineTradeDiscountBeforeTax()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId);
        TaxCalculationRequestDto? capturedTaxRequest = null;
        var taxEngine = new Mock<ITaxCalculationEngine>();
        taxEngine.Setup(engine => engine.CalculateTaxesAsync(
                It.IsAny<TaxCalculationRequestDto>(),
                It.IsAny<CancellationToken>()))
            .Callback<TaxCalculationRequestDto, CancellationToken>((request, _) => capturedTaxRequest = request)
            .ReturnsAsync((TaxCalculationRequestDto request, CancellationToken _) => new TaxCalculationResultDto
            {
                TotalTaxAmount = decimal.Round(request.BaseAmount * 0.15m, 2, MidpointRounding.AwayFromZero)
            });
        var (service, _) = CreateService(db, tenantId, taxEngine: taxEngine.Object);

        var created = await service.CreateAsync(new VendorInvoiceCreateDto
        {
            SupplierId = fixture.Supplier.Id,
            SupplierInvoiceNumber = "SUP-DISCOUNT-001",
            InvoiceDate = new DateTime(2026, 7, 6),
            DueDate = new DateTime(2026, 8, 5),
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            LineItems = new List<VendorInvoiceLineItemCreateDto>
            {
                new()
                {
                    LineItemType = "Expense",
                    GLAccountId = fixture.ExpenseAccount.Id,
                    Description = "Discounted service",
                    Quantity = 1m,
                    UnitPrice = 100m,
                    DiscountPercentage = 10m,
                    TaxGroupId = Guid.NewGuid(),
                    TaxTreatment = TaxTreatment.Standard
                }
            }
        });

        capturedTaxRequest.Should().NotBeNull();
        capturedTaxRequest!.BaseAmount.Should().Be(90m);
        created.SubTotal.Should().Be(90m);
        created.DiscountAmount.Should().Be(10m);
        created.TaxAmount.Should().Be(13.5m);
        created.TotalAmount.Should().Be(103.5m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APBudget")]
    [Trait("Category", "AccountsPayable")]
    public async Task DirectBudgetControlledExpense_ShouldReserveBeforeApprovalWorkflowStarts()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, invoice =>
        {
            invoice.Status = VendorInvoiceStatus.Draft;
            invoice.ApprovalStatus = "Draft";
            invoice.ApprovedById = null;
            invoice.ApprovedDate = null;
        });
        var budgetEntry = await SeedBudgetEntryAsync(db, fixture);
        var reservationId = Guid.NewGuid();
        FinanceBudgetCommitmentRequestDto? capturedRequest = null;
        var budgetCommitments = new Mock<IFinanceBudgetCommitmentService>();
        budgetCommitments
            .Setup(service => service.ReserveAsync(
                It.IsAny<FinanceBudgetCommitmentRequestDto>(),
                It.IsAny<CancellationToken>()))
            .Callback<FinanceBudgetCommitmentRequestDto, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new FinanceBudgetCommitmentResultDto
            {
                Reservations = new[]
                {
                    new FinanceBudgetReservationDto
                    {
                        Id = reservationId,
                        BudgetEntryId = budgetEntry.Id,
                        SourceDocumentType = "VendorInvoice",
                        SourceDocumentId = fixture.Invoice.Id,
                        Status = "Reserved",
                        Version = 1
                    }
                }
            });
        var workflow = new Mock<IWorkflowService>();
        workflow
            .Setup(service => service.StartApprovalWorkflowAsync("VendorInvoice", fixture.Invoice.Id))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress,
                WorkflowInstanceId = Guid.NewGuid()
            });
        var (service, _) = CreateService(db, tenantId, workflow.Object, budgetCommitments.Object);

        var result = await service.SubmitForApprovalAsync(fixture.Invoice.Id);

        result.Status.Should().Be(VendorInvoiceStatus.PendingApproval);
        capturedRequest.Should().NotBeNull();
        capturedRequest!.SourceDocumentType.Should().Be("VendorInvoice");
        capturedRequest.SourceDocumentId.Should().Be(fixture.Invoice.Id);
        capturedRequest.BudgetDate.Should().Be(fixture.Invoice.InvoiceDate.Date);
        capturedRequest.Lines.Should().ContainSingle();
        capturedRequest.Lines.Single().BudgetEntryId.Should().Be(budgetEntry.Id);
        capturedRequest.Lines.Single().AccountId.Should().Be(fixture.ExpenseAccount.Id);
        capturedRequest.Lines.Single().TransactionAmount.Should().Be(100m);
        capturedRequest.Lines.Single().DimensionAssignments.Should().ContainSingle();
        workflow.Verify(
            service => service.StartApprovalWorkflowAsync("VendorInvoice", fixture.Invoice.Id),
            Times.Once);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APBudget")]
    [Trait("Category", "AccountsPayable")]
    public async Task OpeningInvoice_ShouldNotCreateAnExpenseBudgetReservation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, invoice =>
        {
            invoice.IsOpeningBalance = true;
            invoice.Status = VendorInvoiceStatus.Draft;
            invoice.ApprovalStatus = "Draft";
            invoice.ApprovedById = null;
            invoice.ApprovedDate = null;
        });
        var budgetCommitments = new Mock<IFinanceBudgetCommitmentService>(MockBehavior.Strict);
        var workflow = new Mock<IWorkflowService>();
        workflow
            .Setup(service => service.StartApprovalWorkflowAsync("VendorInvoice", fixture.Invoice.Id))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress,
                WorkflowInstanceId = Guid.NewGuid()
            });
        var (service, _) = CreateService(db, tenantId, workflow.Object, budgetCommitments.Object);

        var result = await service.SubmitForApprovalAsync(fixture.Invoice.Id);

        result.Status.Should().Be(VendorInvoiceStatus.PendingApproval);
        budgetCommitments.VerifyNoOtherCalls();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APBudget")]
    [Trait("Category", "AccountsPayable")]
    public async Task RejectedDirectExpenseInvoice_ShouldReleaseItsBudgetReservation()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, invoice =>
        {
            invoice.Status = VendorInvoiceStatus.PendingApproval;
            invoice.ApprovalStatus = "PendingApproval";
            invoice.ApprovedById = null;
            invoice.ApprovedDate = null;
        });
        var reservation = new FinanceBudgetReservation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceDocumentType = "VendorInvoice",
            SourceDocumentId = fixture.Invoice.Id,
            Status = "Reserved",
            ReservationVersion = 3,
            EvaluationHash = new string('A', 64),
            CurrencyCode = "GHS",
            TransactionCurrencyCode = "GHS",
            ReservedByUserId = Guid.NewGuid(),
            ReservedAt = DateTime.UtcNow
        };
        db.FinanceBudgetReservations.Add(reservation);
        await db.SaveChangesAsync();
        ReleaseFinanceBudgetReservationDto? capturedRelease = null;
        var budgetCommitments = new Mock<IFinanceBudgetCommitmentService>();
        budgetCommitments
            .Setup(service => service.ReleaseAsync(
                reservation.Id,
                It.IsAny<ReleaseFinanceBudgetReservationDto>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, ReleaseFinanceBudgetReservationDto, CancellationToken>((_, request, _) => capturedRelease = request)
            .ReturnsAsync(new FinanceBudgetReservationDto
            {
                Id = reservation.Id,
                Status = "Released",
                Version = 4
            });
        var workflow = new Mock<IWorkflowService>();
        workflow
            .Setup(service => service.CanUserApproveAsync("VendorInvoice", fixture.Invoice.Id, It.IsAny<Guid>()))
            .ReturnsAsync(true);
        workflow
            .Setup(service => service.ProcessApprovalStepAsync(
                "VendorInvoice",
                fixture.Invoice.Id,
                It.IsAny<Guid>(),
                "Reject",
                "Budget no longer approved"))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.Cancelled
            });
        var (service, _) = CreateService(db, tenantId, workflow.Object, budgetCommitments.Object);

        var result = await service.RejectAsync(fixture.Invoice.Id, "Budget no longer approved");

        result.Status.Should().Be(VendorInvoiceStatus.Rejected);
        capturedRelease.Should().NotBeNull();
        capturedRelease!.ExpectedVersion.Should().Be(3);
        capturedRelease.Reason.Should().Be("Vendor invoice was rejected.");
        capturedRelease.IdempotencyKey.Should().Contain(fixture.Invoice.Id.ToString("N"));
        budgetCommitments.VerifyAll();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APBudget")]
    [Trait("Category", "AccountsPayable")]
    public async Task RejectedInvoiceRetry_ShouldRepairAStrandedBudgetReservationWithoutReplayingWorkflow()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, invoice =>
        {
            invoice.Status = VendorInvoiceStatus.Rejected;
            invoice.ApprovalStatus = "Rejected";
            invoice.ApprovalComments = "Original rejection";
            invoice.ApprovedById = null;
            invoice.ApprovedDate = null;
        });
        var reservation = new FinanceBudgetReservation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceDocumentType = "VendorInvoice",
            SourceDocumentId = fixture.Invoice.Id,
            Status = "Reserved",
            ReservationVersion = 1,
            EvaluationHash = new string('B', 64),
            CurrencyCode = "GHS",
            TransactionCurrencyCode = "GHS",
            ReservedByUserId = Guid.NewGuid(),
            ReservedAt = DateTime.UtcNow
        };
        db.FinanceBudgetReservations.Add(reservation);
        await db.SaveChangesAsync();

        var budgetCommitments = new Mock<IFinanceBudgetCommitmentService>(MockBehavior.Strict);
        budgetCommitments
            .Setup(service => service.ReleaseAsync(
                reservation.Id,
                It.Is<ReleaseFinanceBudgetReservationDto>(request =>
                    request.ExpectedVersion == 1 &&
                    request.Reason == "Vendor invoice was rejected."),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanceBudgetReservationDto
            {
                Id = reservation.Id,
                Status = "Released",
                Version = 2
            });
        var workflow = new Mock<IWorkflowService>(MockBehavior.Strict);
        var (service, _) = CreateService(db, tenantId, workflow.Object, budgetCommitments.Object);

        var result = await service.RejectAsync(fixture.Invoice.Id, "Retry after lost response");

        result.Status.Should().Be(VendorInvoiceStatus.Rejected);
        (await db.VendorInvoices.SingleAsync(item => item.Id == fixture.Invoice.Id))
            .ApprovalComments.Should().Be("Original rejection");
        budgetCommitments.VerifyAll();
        workflow.VerifyNoOtherCalls();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task OpeningBalanceApInvoice_ShouldPostControlAgainstMigrationClearing()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, invoice =>
        {
            invoice.IsOpeningBalance = true;
            var line = invoice.LineItems.Single();
            line.LineItemType = "FixedAsset";
            line.GLAccountId = Guid.NewGuid();
        });
        var clearingAccount = SeedAccount(db, tenantId, "3999", AccountType.Equity);
        var settings = await db.FinanceSettings.SingleAsync(s => s.TenantId == tenantId);
        settings.MigrationClearingAccountId = clearingAccount.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.Invoice.Id);

        result.JournalEntryId.Should().NotBeNull();

        var journal = await db.JournalEntries
            .Include(j => j.Transactions)
            .SingleAsync(j => j.Id == result.JournalEntryId);
        journal.JournalType.Should().Be("AP Opening Balance");
        journal.SourceModule.Should().Be("AP");
        journal.SourceDocumentType.Should().Be("VendorInvoice");
        journal.Transactions.Should().HaveCount(2);
        journal.Transactions.Single(t => t.AccountId == clearingAccount.Id).DebitAmount.Should().Be(100m);
        journal.Transactions.Single(t => t.AccountId == fixture.ApAccount.Id).CreditAmount.Should().Be(100m);
        journal.Transactions.Should().NotContain(t => t.AccountId == fixture.ExpenseAccount.Id);

        (await db.FinancePostingEvents.CountAsync(e =>
            e.TenantId == tenantId &&
            e.SourceModule == "AP" &&
            e.SourceDocumentType == "VendorInvoice" &&
            e.SourceDocumentId == fixture.Invoice.Id)).Should().Be(1);
        (await db.Set<TaxCalculation>().CountAsync()).Should().Be(0);
        fixture.ExpenseAccount.Balance.Should().Be(0m);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task ForeignOpeningBalanceApInvoice_ShouldRetainApprovedRateAndKeepClearingFunctional()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, invoice =>
        {
            invoice.IsOpeningBalance = true;
            invoice.CurrencyCode = "USD";
            invoice.ExchangeRate = 12.5m;
            invoice.BaseCurrencyAmount = 1250m;
        });
        var rate = SeedApprovedDailyRate(db, tenantId, "USD", 12.5m, ExchangeRateQuoteSide.Selling);
        fixture.Invoice.ExchangeRateId = rate.Id;
        EnableCurrencyForAccounts(db, tenantId, "USD", fixture.ApAccount);
        var clearingAccount = SeedAccount(db, tenantId, "3999", AccountType.Equity);
        var settings = await db.FinanceSettings.SingleAsync(s => s.TenantId == tenantId);
        settings.MigrationClearingAccountId = clearingAccount.Id;
        settings.DirectionalExchangeRatePolicyEnabled = true;
        settings.ApInvoiceQuoteSide = ExchangeRateQuoteSide.Selling;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var result = await service.PostAsync(fixture.Invoice.Id);

        var journal = await db.JournalEntries.Include(item => item.Transactions)
            .SingleAsync(item => item.Id == result.JournalEntryId);
        var clearing = journal.Transactions.Single(item => item.AccountId == clearingAccount.Id);
        clearing.DebitAmount.Should().Be(1250m);
        clearing.TransactionCurrency.Should().Be("GHS");
        clearing.TransactionDebitAmount.Should().Be(1250m);
        clearing.ExchangeRateId.Should().BeNull();

        var control = journal.Transactions.Single(item => item.AccountId == fixture.ApAccount.Id);
        control.CreditAmount.Should().Be(1250m);
        control.TransactionCurrency.Should().Be("USD");
        control.TransactionCreditAmount.Should().Be(100m);
        control.ExchangeRate.Should().Be(12.5m);
        control.ExchangeRateId.Should().Be(rate.Id);
        control.ExchangeRateSource.Should().Be("Regression approved rate");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task ForeignOpeningBalanceApInvoice_ShouldRejectRateDriftBeforePosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, invoice =>
        {
            invoice.IsOpeningBalance = true;
            invoice.CurrencyCode = "USD";
            invoice.ExchangeRate = 14m;
        });
        var rate = SeedApprovedDailyRate(db, tenantId, "USD", 15m);
        fixture.Invoice.ExchangeRateId = rate.Id;
        var settings = await db.FinanceSettings.SingleAsync(s => s.TenantId == tenantId);
        settings.MigrationClearingAccountId = SeedAccount(db, tenantId, "3999", AccountType.Equity).Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var action = () => service.PostAsync(fixture.Invoice.Id);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not match the approved rate record*");
        (await db.FinancePostingEvents.AnyAsync()).Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Batch", "FinanceGoLive-APPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task ForeignApInvoice_ShouldRejectCreateWithoutRateEvidence(bool isOpeningBalance)
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);

        var action = () => service.CreateAsync(new VendorInvoiceCreateDto
        {
            SupplierId = fixture.Supplier.Id,
            InvoiceDate = new DateTime(2026, 7, 5),
            DueDate = new DateTime(2026, 8, 4),
            CurrencyCode = "USD",
            ExchangeRate = 15m,
            IsOpeningBalance = isOpeningBalance,
            LineItems = new List<VendorInvoiceLineItemCreateDto>
            {
                new()
                {
                    LineItemType = "Expense",
                    GLAccountId = fixture.ExpenseAccount.Id,
                    Description = "Opening supplier balance",
                    Quantity = 1m,
                    UnitPrice = 100m,
                    TaxTreatment = TaxTreatment.OutOfScope
                }
            }
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*require an approved exchange-rate record*");
        (await db.VendorInvoices.CountAsync()).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task UnapprovedApInvoice_ShouldNotPost()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, invoice =>
        {
            invoice.Status = VendorInvoiceStatus.PendingApproval;
            invoice.ApprovalStatus = "PendingApproval";
        });
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Only approved AP invoices can be posted.");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
        (await db.JournalEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task UnbalancedApInvoice_ShouldFailBeforeLedgerPosting()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, invoice =>
        {
            invoice.TotalAmount = 125m;
            invoice.BaseCurrencyAmount = 125m;
        });
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AP invoice amount does not match posting line totals.");
        (await db.FinancePostingEvents.CountAsync()).Should().Be(0);
        (await db.JournalEntries.CountAsync()).Should().Be(0);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.ApInvoicePostingFailed)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task CrossTenantSupplier_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherSupplier = SeedSupplier(db, otherTenantId, fixture.ApAccount.Id, fixture.ExpenseAccount.Id);
        fixture.Invoice.SupplierId = otherSupplier.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AP invoice supplier was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task CrossTenantLineAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherAccount = SeedAccount(db, otherTenantId, "6010", AccountType.Expense);
        fixture.Invoice.LineItems.Single().GLAccountId = otherAccount.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AP posting expense account was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task CrossTenantApControlAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId);
        SeedTenant(db, otherTenantId, "OTH");
        var otherApAccount = SeedAccount(db, otherTenantId, "2100", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        fixture.Invoice.ApAccountId = otherApAccount.Id;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AP posting AP control account was not found for this tenant.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task NonPostableExpenseAccount_ShouldBeRejected()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId);
        fixture.ExpenseAccount.AllowDirectPosting = false;
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("AP posting expense account account '6000' does not allow direct posting.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task ClosedPeriod_ShouldBeRejectedByPostingEngine()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, periodIsOpen: false, periodIsClosed: true);
        var (service, _) = CreateService(db, tenantId);

        var act = () => service.PostAsync(fixture.Invoice.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posting period is not open.");
        fixture.Invoice.JournalEntryId.Should().BeNull();
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task DuplicateApInvoicePosting_ShouldReturnExistingPostingAndAuditDuplicate()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);

        var first = await service.PostAsync(fixture.Invoice.Id);
        var second = await service.PostAsync(fixture.Invoice.Id);

        second.JournalEntryId.Should().Be(first.JournalEntryId);
        (await db.JournalEntries.CountAsync()).Should().Be(1);
        (await db.FinancePostingEvents.CountAsync()).Should().Be(1);
        (await db.AuditLogs.CountAsync(a => a.Action == FinanceAuditEvents.ApInvoiceDuplicatePostingAttempt)).Should().Be(1);
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APBudget")]
    [Trait("Category", "AccountsPayable")]
    public async Task BudgetControlledPostingRetry_ShouldUseDurableConsumedEvidenceBeforeReserving()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId);
        var budgetEntry = await SeedBudgetEntryAsync(db, fixture);
        var reservationId = Guid.NewGuid();
        var firstCommitments = new Mock<IFinanceBudgetCommitmentService>();
        firstCommitments
            .Setup(service => service.ReserveAsync(
                It.IsAny<FinanceBudgetCommitmentRequestDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanceBudgetCommitmentResultDto
            {
                Reservations = new[]
                {
                    new FinanceBudgetReservationDto
                    {
                        Id = reservationId,
                        BudgetEntryId = budgetEntry.Id,
                        SourceDocumentType = "VendorInvoice",
                        SourceDocumentId = fixture.Invoice.Id,
                        Status = "Reserved",
                        Version = 1
                    }
                }
            });
        firstCommitments
            .Setup(service => service.ConsumeForPostingAsync(
                tenantId,
                "VendorInvoice",
                fixture.Invoice.Id,
                It.Is<IReadOnlyList<Guid>>(ids => ids.SequenceEqual(new[] { reservationId })),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var (firstService, _) = CreateService(db, tenantId, budgetCommitments: firstCommitments.Object);

        var first = await firstService.PostAsync(fixture.Invoice.Id);
        var postingEvent = await db.FinancePostingEvents.SingleAsync(posting =>
            posting.SourceDocumentType == "VendorInvoice" &&
            posting.SourceDocumentId == fixture.Invoice.Id &&
            posting.PostingAction == "Post");
        db.FinanceBudgetReservations.Add(new FinanceBudgetReservation
        {
            Id = reservationId,
            TenantId = tenantId,
            BudgetEntryId = budgetEntry.Id,
            SourceDocumentType = "VendorInvoice",
            SourceDocumentId = fixture.Invoice.Id,
            Status = "Consumed",
            ReservationVersion = 2,
            EvaluationHash = new string('C', 64),
            CurrencyCode = "GHS",
            TransactionCurrencyCode = "GHS",
            ReservedByUserId = Guid.NewGuid(),
            ReservedAt = DateTime.UtcNow,
            ConsumedAt = DateTime.UtcNow,
            JournalEntryId = first.JournalEntryId,
            PostingEventId = postingEvent.Id
        });
        fixture.Invoice.JournalEntryId = null;
        await db.SaveChangesAsync();
        var retryCommitments = new Mock<IFinanceBudgetCommitmentService>(MockBehavior.Strict);
        var (retryService, _) = CreateService(db, tenantId, budgetCommitments: retryCommitments.Object);

        var retried = await retryService.PostAsync(fixture.Invoice.Id);

        retried.JournalEntryId.Should().Be(first.JournalEntryId);
        (await db.FinanceBudgetReservations.CountAsync()).Should().Be(1);
        retryCommitments.VerifyNoOtherCalls();
    }

    [Fact]
    [Trait("Batch", "FinanceSourceDimensions")]
    [Trait("Category", "AccountsPayable")]
    public async Task DraftEdit_ShouldPreserveExistingAndClientAllocatedDimensionLineIds()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, invoice =>
        {
            invoice.Status = VendorInvoiceStatus.Draft;
            invoice.ApprovalStatus = "Draft";
            invoice.ApprovedById = null;
            invoice.ApprovedDate = null;
        });
        var existingLine = fixture.Invoice.LineItems.Single();
        var newLineId = Guid.NewGuid();
        IReadOnlyList<FinanceSourceDocumentLineContext>? synchronizedLines = null;
        FinanceSourceDocumentDimensionInputDto? synchronizedInput = null;
        var sourceDimensions = new Mock<IFinanceSourceDimensionService>();
        sourceDimensions.Setup(service => service.SynchronizeDraftAsync(
                It.IsAny<FinancePostingProducerContext>(),
                fixture.Invoice.Id,
                fixture.Invoice.InvoiceDate,
                It.IsAny<IReadOnlyList<FinanceSourceDocumentLineContext>>(),
                It.IsAny<FinanceSourceDocumentDimensionInputDto>(),
                true,
                It.IsAny<string?>(),
                "Vendor invoice draft changed.",
                It.IsAny<CancellationToken>()))
            .Callback<FinancePostingProducerContext, Guid, DateTime,
                IReadOnlyList<FinanceSourceDocumentLineContext>, FinanceSourceDocumentDimensionInputDto?,
                bool, string?, string, CancellationToken>((_, _, _, lines, input, _, _, _, _) =>
                {
                    synchronizedLines = lines;
                    synchronizedInput = input;
                })
            .ReturnsAsync(new FinanceSourceDocumentDimensionDto
            {
                RouteId = FinanceDimensionRouteId.FinanceApVendorInvoice,
                SourceDocumentId = fixture.Invoice.Id
            });
        sourceDimensions.Setup(service => service.GetAsync(
                It.IsAny<FinancePostingProducerContext>(),
                fixture.Invoice.Id,
                fixture.Invoice.InvoiceDate,
                It.IsAny<IReadOnlyList<FinanceSourceDocumentLineContext>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinanceSourceDocumentDimensionDto
            {
                RouteId = FinanceDimensionRouteId.FinanceApVendorInvoice,
                SourceDocumentId = fixture.Invoice.Id
            });
        var (service, _) = CreateService(db, tenantId, sourceDimensions: sourceDimensions.Object);
        var dimensionInput = new FinanceSourceDocumentDimensionInputDto
        {
            Lines = new[]
            {
                new FinanceSourceLineDimensionInputDto
                {
                    SourceLineId = existingLine.Id,
                    AccountId = fixture.ExpenseAccount.Id,
                    Dimensions = Array.Empty<FinancePostingDimensionValueDto>()
                },
                new FinanceSourceLineDimensionInputDto
                {
                    SourceLineId = newLineId,
                    AccountId = fixture.ExpenseAccount.Id,
                    Dimensions = Array.Empty<FinancePostingDimensionValueDto>()
                }
            }
        };

        await service.UpdateAsync(new VendorInvoiceUpdateDto
        {
            Id = fixture.Invoice.Id,
            SupplierInvoiceNumber = fixture.Invoice.SupplierInvoiceNumber,
            InvoiceDate = fixture.Invoice.InvoiceDate,
            ReceivedDate = fixture.Invoice.ReceivedDate,
            DueDate = fixture.Invoice.DueDate,
            CurrencyCode = fixture.Invoice.CurrencyCode,
            ExchangeRate = fixture.Invoice.ExchangeRate,
            PaymentTermsDays = fixture.Invoice.PaymentTermsDays,
            ApAccountId = fixture.ApAccount.Id,
            LineItems = new List<VendorInvoiceLineItemCreateDto>
            {
                new()
                {
                    Id = existingLine.Id,
                    LineItemType = "Expense",
                    GLAccountId = fixture.ExpenseAccount.Id,
                    Description = "Updated professional services",
                    Quantity = 1m,
                    UnitPrice = 100m
                },
                new()
                {
                    Id = newLineId,
                    LineItemType = "Expense",
                    GLAccountId = fixture.ExpenseAccount.Id,
                    Description = "Additional professional services",
                    Quantity = 1m,
                    UnitPrice = 50m
                }
            },
            FinanceDimensions = dimensionInput
        }, new FinancePostingProducerContext(FinanceDimensionRouteId.FinanceApVendorInvoice));

        var persistedIds = await db.Set<VendorInvoiceLineItem>()
            .Where(line => line.VendorInvoiceId == fixture.Invoice.Id && !line.IsDeleted)
            .Select(line => line.Id)
            .ToListAsync();
        persistedIds.Should().BeEquivalentTo(new[] { existingLine.Id, newLineId });
        synchronizedLines!.Select(line => line.SourceLineId)
            .Should().BeEquivalentTo(new[] { existingLine.Id, newLineId });
        synchronizedInput.Should().BeSameAs(dimensionInput);
    }

    [Fact]
    [Trait("Batch", "FinanceSourceDimensions")]
    [Trait("Category", "AccountsPayable")]
    public async Task DraftEdit_ShouldRejectALineIdentityAlreadyPersistedByAnotherDocument()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId, invoice =>
        {
            invoice.Status = VendorInvoiceStatus.Draft;
            invoice.ApprovalStatus = "Draft";
            invoice.ApprovedById = null;
            invoice.ApprovedDate = null;
        });
        var conflictingLineId = Guid.NewGuid();
        db.Set<VendorInvoiceLineItem>().Add(new VendorInvoiceLineItem
        {
            Id = conflictingLineId,
            TenantId = tenantId,
            VendorInvoiceId = Guid.NewGuid(),
            LineItemType = "Expense",
            GLAccountId = fixture.ExpenseAccount.Id,
            Description = "Other document line",
            Quantity = 1m,
            UnitPrice = 1m,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });
        await db.SaveChangesAsync();
        var (service, _) = CreateService(db, tenantId);

        var update = () => service.UpdateAsync(new VendorInvoiceUpdateDto
        {
            Id = fixture.Invoice.Id,
            InvoiceDate = fixture.Invoice.InvoiceDate,
            ReceivedDate = fixture.Invoice.ReceivedDate,
            DueDate = fixture.Invoice.DueDate,
            CurrencyCode = fixture.Invoice.CurrencyCode,
            ExchangeRate = fixture.Invoice.ExchangeRate,
            ApAccountId = fixture.ApAccount.Id,
            LineItems = new List<VendorInvoiceLineItemCreateDto>
            {
                new()
                {
                    Id = conflictingLineId,
                    LineItemType = "Expense",
                    GLAccountId = fixture.ExpenseAccount.Id,
                    Description = "Attempted identity reuse",
                    Quantity = 1m,
                    UnitPrice = 100m
                }
            }
        });

        await update.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("A vendor invoice line identity already belongs to a persisted document.");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-APPosting")]
    [Trait("Category", "AccountsPayable")]
    public async Task PostedApInvoice_ShouldNotBeEditedOrDeleted()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId);
        var (service, _) = CreateService(db, tenantId);
        await service.PostAsync(fixture.Invoice.Id);

        var update = () => service.UpdateAsync(new VendorInvoiceUpdateDto
        {
            Id = fixture.Invoice.Id,
            InvoiceDate = fixture.Invoice.InvoiceDate,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            LineItems = new List<VendorInvoiceLineItemCreateDto>()
        });
        var delete = () => service.DeleteAsync(fixture.Invoice.Id);

        await update.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posted vendor invoices cannot be updated. Use a reversal, credit note, or adjustment.");
        await delete.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Posted vendor invoices cannot be deleted. Use a reversal, credit note, or adjustment.");
    }

    [Fact]
    [Trait("Batch", "TDC-0508")]
    [Trait("Category", "AccountsPayable")]
    public async Task PostedApInvoice_ShouldVoidWithOneBalancedIdempotentReversal()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var fixture = await SeedApprovedApInvoiceAsync(db, tenantId);
        var reversalDate = DateTime.UtcNow.Date;
        if (reversalDate.Year != fixture.Invoice.InvoiceDate.Year ||
            reversalDate.Month != fixture.Invoice.InvoiceDate.Month)
        {
            SeedOpenPeriod(db, tenantId, reversalDate);
            await db.SaveChangesAsync();
        }
        var (service, _) = CreateService(db, tenantId);
        var posted = await service.PostAsync(fixture.Invoice.Id);

        var first = await service.VoidAsync(fixture.Invoice.Id, "incorrect supplier invoice");
        var second = await service.VoidAsync(fixture.Invoice.Id, "idempotent retry");

        first.Status.Should().Be(VendorInvoiceStatus.Voided);
        second.Status.Should().Be(VendorInvoiceStatus.Voided);
        var original = await db.JournalEntries
            .Include(item => item.ReversalJournalEntry)
                .ThenInclude(item => item!.Transactions)
            .SingleAsync(item => item.Id == posted.JournalEntryId);
        original.IsReversed.Should().BeTrue();
        original.ReversalJournalEntryId.Should().NotBeNull();
        original.ReversalJournalEntry!.IsBalanced.Should().BeTrue();
        original.ReversalJournalEntry.TotalDebitAmount.Should().Be(original.TotalCreditAmount);
        original.ReversalJournalEntry.TotalCreditAmount.Should().Be(original.TotalDebitAmount);
        original.ReversalJournalEntry.Transactions.Sum(item => item.DebitAmount)
            .Should().Be(original.Transactions.Sum(item => item.CreditAmount));
        original.ReversalJournalEntry.Transactions.Sum(item => item.CreditAmount)
            .Should().Be(original.Transactions.Sum(item => item.DebitAmount));
        (await db.FinancePostingEvents.CountAsync(item =>
            item.SourceDocumentType == "VendorInvoice" && item.PostingAction == "Reverse")).Should().Be(1);
        (await db.AuditLogs.CountAsync(item => item.Action == FinanceAuditEvents.ApInvoiceReversed)).Should().Be(1);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ap-invoice-posting-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static (VendorInvoiceService Service, Mock<ISubledgerPostingService> SubledgerPostingMock) CreateService(
        ApplicationDbContext db,
        Guid tenantId,
        IWorkflowService? workflowService = null,
        IFinanceBudgetCommitmentService? budgetCommitments = null,
        IFinanceSourceDimensionService? sourceDimensions = null,
        ITaxCalculationEngine? taxEngine = null)
    {
        var currentUser = CreateCurrentUser(tenantId);
        var auditService = new FinanceAuditService(
            db,
            currentUser.Object,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-ap-posting" }
            });
        var postingEngine = new FinancePostingEngine(
            db,
            currentUser.Object,
            Mock.Of<ILogger<FinancePostingEngine>>(),
            auditService,
            budgetCommitments: budgetCommitments);
        var subledgerPostingMock = new Mock<ISubledgerPostingService>();
        var numbering = new Mock<IDocumentNumberingService>();
        numbering.Setup(service => service.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("VI-2026-TEST");

        var service = new VendorInvoiceService(
            new UnitOfWork(db),
            currentUser.Object,
            Mock.Of<IInventoryValuationService>(),
            Mock.Of<ILogger<VendorInvoiceService>>(),
            numbering.Object,
            workflowService ?? Mock.Of<IWorkflowService>(),
            postingEngine,
            auditService,
            taxEngine: taxEngine,
            budgetCommitments: budgetCommitments,
            sourceDimensions: sourceDimensions);

        return (service, subledgerPostingMock);
    }

    private static Mock<ICurrentUserService> CreateCurrentUser(Guid tenantId)
    {
        var userId = Guid.NewGuid().ToString();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        currentUser.SetupGet(x => x.UserId).Returns(userId);
        currentUser.SetupGet(x => x.UserName).Returns("ap.poster");
        currentUser.SetupGet(x => x.IpAddress).Returns("127.0.0.1");
        currentUser.SetupGet(x => x.UserAgent).Returns("ap-posting-tests");
        currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        currentUser.SetupGet(x => x.Roles).Returns(Array.Empty<string>());
        return currentUser;
    }

    private static async Task<ApInvoiceFixture> SeedApprovedApInvoiceAsync(
        ApplicationDbContext db,
        Guid tenantId,
        Action<VendorInvoice>? configureInvoice = null,
        bool periodIsOpen = true,
        bool periodIsClosed = false)
    {
        SeedTenant(db, tenantId);
        SeedOpenPeriod(db, tenantId, periodIsOpen, periodIsClosed);
        var expenseAccount = SeedAccount(db, tenantId, "6000", AccountType.Expense);
        var apAccount = SeedAccount(db, tenantId, "2000", AccountType.Liability, isControlAccount: true, allowDirectPosting: false);
        var taxAccount = SeedAccount(db, tenantId, "1400", AccountType.Asset, isControlAccount: true, allowDirectPosting: false);
        var discountAccount = SeedAccount(db, tenantId, "4900", AccountType.Revenue);
        var supplier = SeedSupplier(db, tenantId, apAccount.Id, expenseAccount.Id);

        db.Set<FinanceSettings>().Add(new FinanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrency = "GHS",
            ControlAccountApId = apAccount.Id,
            ControlAccountTaxId = taxAccount.Id,
            DiscountReceivedAccountId = discountAccount.Id
        });

        var invoice = new VendorInvoice
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceNumber = "VI-2026-00001",
            SupplierInvoiceNumber = "SUP-001",
            SupplierId = supplier.Id,
            SupplierName = supplier.Name,
            InvoiceDate = new DateTime(2026, 7, 5),
            ReceivedDate = new DateTime(2026, 7, 5),
            DueDate = new DateTime(2026, 8, 4),
            SubTotal = 100m,
            TaxAmount = 0m,
            DiscountAmount = 0m,
            TotalAmount = 100m,
            PaidAmount = 0m,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            BaseCurrencyAmount = 100m,
            Status = VendorInvoiceStatus.Approved,
            ApprovalStatus = "Approved",
            ApprovedById = Guid.NewGuid(),
            ApprovedDate = DateTime.UtcNow,
            ApAccountId = apAccount.Id,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        invoice.LineItems.Add(new VendorInvoiceLineItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VendorInvoiceId = invoice.Id,
            LineItemType = "Expense",
            GLAccountId = expenseAccount.Id,
            Description = "Professional services",
            Quantity = 1m,
            UnitPrice = 100m,
            TaxRate = 0m,
            TaxAmount = 0m,
            DiscountPercentage = 0m,
            DiscountAmount = 0m,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        });

        configureInvoice?.Invoke(invoice);
        db.VendorInvoices.Add(invoice);
        await db.SaveChangesAsync();

        return new ApInvoiceFixture(invoice, supplier, expenseAccount, apAccount, taxAccount);
    }

    private static async Task<BudgetEntry> SeedBudgetEntryAsync(
        ApplicationDbContext db,
        ApInvoiceFixture fixture)
    {
        fixture.ExpenseAccount.BudgetTrackingEnabled = true;
        var line = fixture.Invoice.LineItems.Single();
        var period = await db.FiscalPeriods.SingleAsync(period =>
            period.TenantId == fixture.Invoice.TenantId &&
            period.StartDate <= fixture.Invoice.InvoiceDate &&
            period.EndDate >= fixture.Invoice.InvoiceDate);
        var definition = new FinanceDimensionDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.Invoice.TenantId,
            Code = "DEPARTMENT",
            Name = "Department",
            Classification = "Analytical",
            ValueSourceType = "Lookup",
            IsActive = true
        };
        var value = new FinanceDimensionValue
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.Invoice.TenantId,
            FinanceDimensionDefinitionId = definition.Id,
            FinanceDimensionDefinition = definition,
            Code = "FIN",
            Name = "Finance",
            EffectiveDate = new DateTime(2025, 1, 1),
            IsActive = true
        };
        var dimensionSet = new FinanceDimensionSet
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.Invoice.TenantId,
            CombinationHash = new string('B', 64),
            DisplayValue = "DEPARTMENT=FIN"
        };
        dimensionSet.Items.Add(new FinanceDimensionSetItem
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.Invoice.TenantId,
            FinanceDimensionSetId = dimensionSet.Id,
            FinanceDimensionSet = dimensionSet,
            FinanceDimensionDefinitionId = definition.Id,
            FinanceDimensionDefinition = definition,
            FinanceDimensionValueId = value.Id,
            FinanceDimensionValue = value,
            DimensionCodeSnapshot = definition.Code,
            DimensionValueCodeSnapshot = value.Code,
            DimensionValueNameSnapshot = value.Name
        });
        var budgetReturn = new BudgetReturn
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.Invoice.TenantId,
            BudgetScenarioId = Guid.NewGuid(),
            Status = "Approved"
        };
        var budgetEntry = new BudgetEntry
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.Invoice.TenantId,
            BudgetReturnId = budgetReturn.Id,
            BudgetReturn = budgetReturn,
            AccountId = fixture.ExpenseAccount.Id,
            FiscalPeriodId = period.Id,
            FinanceDimensionSetId = dimensionSet.Id,
            FinanceDimensionSet = dimensionSet,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            Amount = 1_000m,
            AmountBase = 1_000m
        };
        line.BudgetEntryId = budgetEntry.Id;
        db.FinanceDimensionDefinitions.Add(definition);
        db.FinanceDimensionValues.Add(value);
        db.FinanceDimensionSets.Add(dimensionSet);
        db.BudgetReturns.Add(budgetReturn);
        db.BudgetEntries.Add(budgetEntry);
        await db.SaveChangesAsync();
        return budgetEntry;
    }

    private static void SeedTenant(ApplicationDbContext db, Guid tenantId, string code = "TEN")
    {
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = $"Tenant {code}",
            Code = code,
            Status = TenantStatus.Active,
            BaseCurrency = "GHS"
        });
    }

    private static FiscalPeriod SeedOpenPeriod(
        ApplicationDbContext db,
        Guid tenantId,
        bool isOpen = true,
        bool isClosed = false)
        => SeedOpenPeriod(db, tenantId, new DateTime(2026, 7, 1), isOpen, isClosed);

    private static FiscalPeriod SeedOpenPeriod(
        ApplicationDbContext db,
        Guid tenantId,
        DateTime periodDate,
        bool isOpen = true,
        bool isClosed = false)
    {
        var startDate = new DateTime(periodDate.Year, periodDate.Month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = Guid.NewGuid(),
            PeriodName = startDate.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            PeriodCode = startDate.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            PeriodNumber = startDate.Month,
            PeriodType = PeriodType.Monthly,
            StartDate = startDate,
            EndDate = endDate,
            PeriodDays = (endDate - startDate).Days + 1,
            PeriodStatus = isClosed ? "Closed" : isOpen ? "Open" : "Future",
            IsOpen = isOpen,
            IsClosed = isClosed,
            IsLocked = false
        };

        db.FiscalPeriods.Add(period);
        return period;
    }

    private static Account SeedAccount(
        ApplicationDbContext db,
        Guid tenantId,
        string accountNumber,
        AccountType accountType,
        AccountStatus status = AccountStatus.Active,
        bool isControlAccount = false,
        bool allowDirectPosting = true)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AccountCode = accountNumber,
            AccountNumber = accountNumber,
            AccountName = $"Account {accountNumber}",
            AccountType = accountType,
            Status = status,
            CurrencyCode = "GHS",
            IsControlAccount = isControlAccount,
            AllowDirectPosting = allowDirectPosting
        };

        db.Accounts.Add(account);
        return account;
    }

    private static ExchangeRate SeedApprovedDailyRate(
        ApplicationDbContext db,
        Guid tenantId,
        string targetCurrency,
        decimal rate,
        ExchangeRateQuoteSide quoteSide = ExchangeRateQuoteSide.Mid)
    {
        var exchangeRate = new ExchangeRate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BaseCurrencyCode = "GHS",
            TargetCurrencyCode = targetCurrency,
            Rate = rate,
            InverseRate = decimal.Round(1m / rate, 6),
            EffectiveDate = new DateTime(2026, 7, 5),
            RateType = ExchangeRateType.Daily,
            QuoteSide = quoteSide,
            RateSource = "Regression approved rate",
            IsActive = true,
            ApprovalStatus = RateApprovalStatus.Approved,
            CreatedByUserId = Guid.NewGuid(),
            CreatedDate = DateTime.UtcNow
        };
        db.ExchangeRates.Add(exchangeRate);
        return exchangeRate;
    }

    private static void EnableCurrencyForAccounts(
        ApplicationDbContext db,
        Guid tenantId,
        string currencyCode,
        params Account[] accounts)
    {
        foreach (var account in accounts)
        {
            account.IsMultiCurrency = true;
            db.AccountCurrencyLinks.Add(new AccountCurrencyLink
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountId = account.Id,
                LinkedCurrencyCode = currencyCode,
                TransactionRateType = "Daily",
                IsActive = true,
                EffectiveDate = new DateTime(2026, 1, 1),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "seed"
            });
        }
    }

    private static Supplier SeedSupplier(
        ApplicationDbContext db,
        Guid tenantId,
        Guid apAccountId,
        Guid expenseAccountId)
    {
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SupplierCode = $"SUP-{tenantId.ToString("N")[..6]}",
            Name = "Test Supplier",
            SupplierType = "Vendor",
            IsActive = true,
            Status = "Active",
            DefaultApAccountId = apAccountId,
            DefaultExpenseAccountId = expenseAccountId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        db.Suppliers.Add(supplier);
        return supplier;
    }

    private sealed record ApInvoiceFixture(
        VendorInvoice Invoice,
        Supplier Supplier,
        Account ExpenseAccount,
        Account ApAccount,
        Account TaxAccount);
}
