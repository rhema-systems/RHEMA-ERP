using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Api.Services.Finance.FixedAssets;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceOwnedSourceDimensionReadinessProviderTests
{
    [Fact]
    public async Task BankDepositRouteRequiresStableSourceAndAllocationEvidence()
    {
        var tenantId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"finance-bank-deposit-readiness-{Guid.NewGuid():N}").Options);
        var bankGl = new Account
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountCode = "1100", AccountNumber = "1100",
            AccountName = "Bank", AccountType = AccountType.Asset, Status = AccountStatus.Active
        };
        var holdingGl = new Account
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountCode = "1110", AccountNumber = "1110",
            AccountName = "Undeposited Cash", AccountType = AccountType.Asset, Status = AccountStatus.Active
        };
        var bank = new BankAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountNumber = "BANK-01", AccountName = "Operating Bank",
            BankName = "Test Bank", Currency = "GHS", AccountType = BankAccountType.Checking,
            GLAccountId = bankGl.Id, IsActive = true, OpeningDate = new DateTime(2026, 9, 1)
        };
        var holding = new LiquidityAccount
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Code = "UNDEPOSITED", Name = "Undeposited Cash",
            AccountType = LiquidityAccountType.UndepositedCash, Currency = "GHS",
            GLAccountId = holdingGl.Id, IsActive = true
        };
        var entry = new LiquidityAccountEntry
        {
            Id = Guid.NewGuid(), TenantId = tenantId, LiquidityAccountId = holding.Id,
            EntryNumber = "LQE-READY-001", EntryDate = new DateTime(2026, 9, 1),
            EntryType = LiquidityEntryType.CustomerReceipt, Direction = LiquidityEntryDirection.Increase,
            Amount = 100m, Currency = "GHS", SourceDocumentType = "CustomerPayment",
            SourceDocumentId = Guid.NewGuid()
        };
        var deposit = new BankDepositBatch
        {
            Id = Guid.NewGuid(), TenantId = tenantId, DepositNumber = "DEP-READY-001",
            BankAccountId = bank.Id, DepositDate = new DateTime(2026, 9, 1),
            DepositReference = "SLIP-READY", Currency = "GHS", Status = BankDepositStatus.Draft,
            TotalReceipts = 100m, NetAmount = 100m
        };
        var allocation = new BankDepositAllocation
        {
            Id = Guid.NewGuid(), TenantId = tenantId, BankDepositBatchId = deposit.Id,
            LiquidityAccountEntryId = entry.Id, AllocationType = BankDepositAllocationType.Receipt,
            Amount = 100m
        };
        db.Accounts.AddRange(bankGl, holdingGl);
        db.BankAccounts.Add(bank);
        db.LiquidityAccounts.Add(holding);
        db.LiquidityAccountEntries.Add(entry);
        db.BankDepositBatches.Add(deposit);
        db.BankDepositAllocations.Add(allocation);
        await db.SaveChangesAsync();

        var route = FinanceDimensionRouteCatalog.GetRequired(FinanceDimensionRouteId.FinanceBankDeposit);
        var provider = new FinanceOwnedSourceDimensionReadinessProvider(db, route.Id);
        (await provider.EvaluateAsync(tenantId, route)).Blockers.Should().ContainSingle(item =>
            item.Code == "UNCERTIFIED_LEGACY_DOCUMENT" && item.DocumentId == deposit.Id);

        db.FinanceSourceDimensionAssignments.AddRange(
            Assignment(route, tenantId, deposit.Id, null),
            Assignment(route, tenantId, deposit.Id, deposit.Id),
            Assignment(route, tenantId, deposit.Id, allocation.Id));
        await db.SaveChangesAsync();
        (await provider.EvaluateAsync(tenantId, route)).Blockers.Should().ContainSingle(item =>
            item.Code == "SETTLEMENT_EVIDENCE_MISSING" && item.DocumentId == deposit.Id);

        db.FinanceSettlementDimensionComponents.Add(new FinanceSettlementDimensionComponent
        {
            Id = Guid.NewGuid(), TenantId = tenantId, RouteId = route.Id,
            ProducerModule = route.ProducerModule, SourceRoute = route.SourceRoute,
            SourceDocumentType = route.DocumentType, ContractVersion = route.ContractVersion,
            SourceDocumentId = deposit.Id, SettlementSourceLineId = allocation.Id,
            SettlementAllocationId = allocation.Id, ComponentType = FinanceSettlementComponentType.Principal,
            TransactionCurrencyCode = "GHS", TransactionAmount = 100m, FunctionalAmount = 100m,
            ExchangeRate = 1m, EvidenceHash = new string('a', 64)
        });
        await db.SaveChangesAsync();

        (await provider.EvaluateAsync(tenantId, route)).Blockers.Should().BeEmpty();
    }

    [Fact]
    public async Task ManualVendorRouteReportsLegacyAndMissingLineEvidenceWithFinanceLink()
    {
        var tenantId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"finance-dimension-readiness-{Guid.NewGuid():N}").Options);
        var supplierId = Guid.NewGuid();
        db.Suppliers.Add(new Supplier
        {
            Id = supplierId,
            TenantId = tenantId,
            SupplierCode = "SUP-READINESS",
            Name = "Readiness Supplier"
        });
        db.VendorInvoices.Add(new VendorInvoice
        {
            Id = invoiceId,
            TenantId = tenantId,
            SupplierId = supplierId,
            InvoiceNumber = "VI-READINESS-001",
            SupplierName = "Readiness Supplier",
            InvoiceDate = new DateTime(2026, 8, 30),
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            Status = VendorInvoiceStatus.Draft,
            ApprovalStatus = "Draft",
            LineItems =
            [
                new VendorInvoiceLineItem
                {
                    Id = lineId,
                    TenantId = tenantId,
                    VendorInvoiceId = invoiceId,
                    LineItemType = "Expense",
                    GLAccountId = accountId,
                    Description = "Readiness expense",
                    Quantity = 1m,
                    UnitPrice = 100m
                }
            ]
        });
        await db.SaveChangesAsync();

        var route = FinanceDimensionRouteCatalog.GetRequired(
            FinanceDimensionRouteId.FinanceApVendorInvoice);
        var provider = new FinanceOwnedSourceDimensionReadinessProvider(db, route.Id);
        var legacy = await provider.EvaluateAsync(tenantId, route);

        legacy.Blockers.Should().ContainSingle(blocker =>
            blocker.Code == "UNCERTIFIED_LEGACY_DOCUMENT"
            && blocker.DocumentLink == $"/finance/ap/invoices/{invoiceId}");

        db.FinanceSourceDimensionAssignments.Add(new FinanceSourceDimensionAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RouteId = route.Id,
            ProducerModule = route.ProducerModule,
            SourceRoute = route.SourceRoute,
            SourceDocumentType = route.DocumentType,
            ContractVersion = route.ContractVersion,
            SourceDocumentId = invoiceId
        });
        await db.SaveChangesAsync();

        var adaptedHeader = await provider.EvaluateAsync(tenantId, route);
        adaptedHeader.Blockers.Should().ContainSingle(blocker =>
            blocker.Code == "SOURCE_LINE_NOT_ADAPTED" && blocker.DocumentId == invoiceId);
    }

    [Fact]
    public async Task CapitalProjectReadinessIsTenantScopedAndRequiresFrozenStableLines()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"finance-fixed-asset-readiness-{Guid.NewGuid():N}").Options);
        db.CapitalProjects.AddRange(
            new CapitalProject
            {
                Id = projectId, TenantId = tenantId, ProjectCode = "CIP-READY-001", Name = "Ready project",
                StartDate = new DateTime(2026, 9, 1), TotalBudgetAmount = 100m,
                TotalAccumulatedCost = 100m, Status = ProjectStatus.InProgress, RowVersion = [1]
            },
            new CapitalProject
            {
                Id = Guid.NewGuid(), TenantId = otherTenantId, ProjectCode = "CIP-OTHER", Name = "Other tenant",
                StartDate = new DateTime(2026, 9, 1), TotalBudgetAmount = 50m,
                TotalAccumulatedCost = 50m, Status = ProjectStatus.InProgress, RowVersion = [1]
            });
        await db.SaveChangesAsync();

        var route = FinanceDimensionRouteCatalog.GetRequired(
            FinanceDimensionRouteId.FinanceCapitalProjectSettlement);
        var provider = new FinanceFixedAssetDimensionReadinessProvider(db, route.Id);
        var legacy = await provider.EvaluateAsync(tenantId, route);
        legacy.Blockers.Should().ContainSingle(item =>
            item.Code == "UNCERTIFIED_FIXED_ASSET_DOCUMENT"
            && item.DocumentId == projectId
            && item.DocumentReference == "CIP-READY-001");

        var frozenLine = Assignment(route, tenantId, projectId,
            FinanceSourceLineIdentity.Create(projectId, "SETTLEMENT-ASSET", Guid.NewGuid()));
        frozenLine.EvidenceFrozenAt = DateTime.UtcNow;
        db.FinanceSourceDimensionAssignments.AddRange(
            Assignment(route, tenantId, projectId, null),
            frozenLine);
        await db.SaveChangesAsync();

        (await provider.EvaluateAsync(tenantId, route)).Blockers.Should().BeEmpty();
    }

    private static FinanceSourceDimensionAssignment Assignment(
        FinanceDimensionRouteDefinition route,
        Guid tenantId,
        Guid documentId,
        Guid? lineId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        RouteId = route.Id,
        ProducerModule = route.ProducerModule,
        SourceRoute = route.SourceRoute,
        SourceDocumentType = route.DocumentType,
        ContractVersion = route.ContractVersion,
        SourceDocumentId = documentId,
        SourceLineId = lineId
    };
}
