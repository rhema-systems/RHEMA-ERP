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
    public async Task Auction_readiness_uses_typed_disposal_source_and_does_not_pollute_manual_AR_route()
    {
        var tenant = Guid.NewGuid();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"auction-route-readiness-{Guid.NewGuid():N}").Options);
        var manual = new Invoice { TenantId = tenant, InvoiceNumber = "MANUAL", Status = InvoiceStatus.Draft,
            InvoiceDate = new DateTime(2026, 9, 27), LineItems = [new InvoiceLineItem { TenantId = tenant, Quantity = 1, UnitPrice = 50 }] };
        var auction = new Invoice { TenantId = tenant, InvoiceNumber = "AUCTION", Status = InvoiceStatus.Draft,
            InvoiceDate = new DateTime(2026, 9, 27), LineItems = [new InvoiceLineItem { TenantId = tenant, Quantity = 1, UnitPrice = 70 }] };
        foreach(var line in manual.LineItems) line.InvoiceId = manual.Id;
        foreach(var line in auction.LineItems) line.InvoiceId = auction.Id;
        var sales = new Invoice { TenantId = tenant, InvoiceNumber = "SALES", Status = InvoiceStatus.Draft,
            InvoiceDate = new DateTime(2026, 9, 27), LineItems = [new InvoiceLineItem { TenantId = tenant, Quantity = 1, UnitPrice = 90 }] };
        foreach (var line in sales.LineItems) line.InvoiceId = sales.Id;
        db.Invoices.AddRange(manual, auction, sales);
        db.Set<ErpSystem.Core.Entities.Sales.SalesOrder>().Add(new()
            { TenantId = tenant, BusinessPartnerId = Guid.NewGuid(), InvoiceId = sales.Id });
        db.Set<ErpSystem.Core.Entities.Inventory.InventoryDisposalAuctionInvoice>().Add(new()
            { TenantId = tenant, InventoryDisposalCaseId = Guid.NewGuid(), InvoiceId = auction.Id });
        await db.SaveChangesAsync();
        var manualRoute = FinanceDimensionRouteCatalog.GetRequired(FinanceDimensionRouteId.FinanceArCustomerInvoice);
        var auctionRoute = FinanceDimensionRouteCatalog.GetRequired(FinanceDimensionRouteId.InventoryDisposalAuctionInvoice);
        var manualResult = await new FinanceOwnedSourceDimensionReadinessProvider(db, manualRoute.Id).EvaluateAsync(tenant, manualRoute);
        var auctionResult = await new FinanceOwnedSourceDimensionReadinessProvider(db, auctionRoute.Id).EvaluateAsync(tenant, auctionRoute);
        manualResult.Blockers.Select(value => value.DocumentId).Distinct().Should().Equal(manual.Id);
        auctionResult.Blockers.Select(value => value.DocumentId).Distinct().Should().Equal(auction.Id);
        var salesRoute = FinanceDimensionRouteCatalog.GetRequired(FinanceDimensionRouteId.SalesOrderCustomerInvoice);
        var salesResult = await new FinanceOwnedSourceDimensionReadinessProvider(db, salesRoute.Id).EvaluateAsync(tenant, salesRoute);
        salesResult.Blockers.Select(value => value.DocumentId).Distinct().Should().Equal(sales.Id);
    }

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
        db.BusinessPartners.Add(new BusinessPartner
        {
            Id = supplierId,
            TenantId = tenantId,
            PartnerCode = "SUP-READINESS",
            PartnerName = "Readiness Supplier"
        });
        db.VendorInvoices.Add(new VendorInvoice
        {
            Id = invoiceId,
            TenantId = tenantId,
            BusinessPartnerId = supplierId,
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
    public async Task CapitalProjectReadinessIsTenantScopedAndRequiresTrustedStableLineContext()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"finance-fixed-asset-readiness-{Guid.NewGuid():N}").Options);
        var account = new Account
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountCode = "1500", AccountNumber = "1500",
            AccountName = "Fixed asset", AccountType = AccountType.Asset, Status = AccountStatus.Active
        };
        db.Accounts.Add(account);
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

        var lineId = FinanceSourceLineIdentity.Create(projectId, "SETTLEMENT-ASSET", Guid.NewGuid());
        var capturedLine = Assignment(route, tenantId, projectId, lineId);
        capturedLine.ResolvedAccountId = account.Id;
        var header = Assignment(route, tenantId, projectId, null);
        header.SourceDocumentDate = new DateTime(2026, 9, 1);
        header.ExpectedSourceLineCount = 1;
        header.SourceLineManifestHash = FinanceSourceLineManifest.Compute([(lineId, account.Id)]);
        db.FinanceSourceDimensionAssignments.AddRange(
            header,
            capturedLine);
        await db.SaveChangesAsync();

        (await provider.EvaluateAsync(tenantId, route)).Blockers.Should().BeEmpty();
    }

    [Fact]
    public async Task FixedAssetReadinessDetectsManifestAndFixedRuleDrift()
    {
        var tenantId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var expectedValueId = Guid.NewGuid();
        var capturedValueId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"finance-fixed-asset-rule-drift-{Guid.NewGuid():N}").Options);
        var route = FinanceDimensionRouteCatalog.GetRequired(FinanceDimensionRouteId.FinanceFixedAssetDisposalSaleInvoice);
        db.Accounts.Add(new Account
        {
            Id = accountId, TenantId = tenantId, AccountCode = "4100", AccountNumber = "4100",
            AccountName = "Disposal proceeds", AccountType = AccountType.Revenue, Status = AccountStatus.Active
        });
        var definition = new FinanceDimensionDefinition
        {
            Id = definitionId, TenantId = tenantId, Code = "DEPT", Name = "Department", IsActive = true
        };
        var expected = new FinanceDimensionValue
        {
            Id = expectedValueId, TenantId = tenantId, FinanceDimensionDefinitionId = definitionId,
            Code = "FIN", Name = "Finance", IsActive = true, EffectiveDate = new DateTime(2026, 1, 1)
        };
        var captured = new FinanceDimensionValue
        {
            Id = capturedValueId, TenantId = tenantId, FinanceDimensionDefinitionId = definitionId,
            Code = "OPS", Name = "Operations", IsActive = true, EffectiveDate = new DateTime(2026, 1, 1)
        };
        var set = new FinanceDimensionSet
        {
            Id = Guid.NewGuid(), TenantId = tenantId, CombinationHash = new string('a', 64), DisplayValue = "DEPT=OPS",
            Items = [new FinanceDimensionSetItem
            {
                Id = Guid.NewGuid(), TenantId = tenantId,
                FinanceDimensionDefinitionId = definitionId, FinanceDimensionValueId = capturedValueId,
                DimensionCodeSnapshot = "DEPT", DimensionNameSnapshot = "Department",
                DimensionValueCodeSnapshot = "OPS", DimensionValueNameSnapshot = "Operations"
            }]
        };
        db.FinanceDimensionDefinitions.Add(definition);
        db.FinanceDimensionValues.AddRange(expected, captured);
        db.FinanceDimensionSets.Add(set);
        db.FinanceDimensionAccountRules.Add(new FinanceDimensionAccountRule
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountId = accountId,
            FinanceDimensionDefinitionId = definitionId, RuleType = "Fixed",
            DefaultDimensionValueId = expectedValueId, IsActive = true,
            EffectiveDate = new DateTime(2026, 1, 1), RouteId = route.Id,
            RuleVersion = 1
        });
        var header = Assignment(route, tenantId, documentId, null);
        header.SourceDocumentDate = new DateTime(2026, 9, 1);
        header.ExpectedSourceLineCount = 2;
        header.SourceLineManifestHash = FinanceSourceLineManifest.Compute([(lineId, accountId)]);
        var line = Assignment(route, tenantId, documentId, lineId);
        line.ResolvedAccountId = accountId;
        line.FinanceDimensionSetId = set.Id;
        db.FinanceSourceDimensionAssignments.AddRange(header, line);
        await db.SaveChangesAsync();

        var result = await new FinanceFixedAssetDimensionReadinessProvider(db, route.Id)
            .EvaluateAsync(tenantId, route);

        result.Blockers.Should().Contain(item => item.Code == "FIXED_ASSET_SOURCE_LINE_COUNT_MISMATCH");
        result.Blockers.Should().Contain(item => item.Code == "FIXED_RULE_DRIFT" && item.FixedRuleDrift);
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
