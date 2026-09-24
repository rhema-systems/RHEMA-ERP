using System.Collections;
using System.Reflection;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Api.Services.Finance.AP;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.AspNetCore.Authorization;
using ErpSystem.Shared;
using ErpSystem.Web.Services;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class SupplierDebitNoteFoundationTests
{
    private const string MigrationId = "20260818103000_AddSupplierDebitNoteLifecycleAndApplications";
    private const string PrecisionMigrationId = "20260818123000_WidenSupplierDebitNoteExchangeRatePrecision";
    private const string HardeningMigrationId = "20260818130000_HardenSupplierDebitNoteLineageIdentityAndSettlement";

    [Fact]
    [Trait("Category", "AccountsPayable")]
    public void DebitNoteLifecyclePreservesLegacyValuesAndAddsMakerCheckerStates()
    {
        ((int)SupplierDebitNoteStatus.Draft).Should().Be(1);
        ((int)SupplierDebitNoteStatus.Posted).Should().Be(2);
        ((int)SupplierDebitNoteStatus.Cancelled).Should().Be(3);
        ((int)SupplierDebitNoteStatus.PendingApproval).Should().Be(4);
        ((int)SupplierDebitNoteStatus.Approved).Should().Be(5);
        ((int)SupplierDebitNoteStatus.Rejected).Should().Be(6);
        ((int)SupplierDebitNoteStatus.Reversed).Should().Be(7);
    }

    [Fact]
    [Trait("Category", "AccountsPayable")]
    public void ApplicationHistoryUsesImmutableCompensatingRows()
    {
        var first = Application(200m);
        var second = Application(50m);
        var reversal = Application(-200m, isReversal: true, originalApplicationId: first.Id);

        var effective = VendorPaymentService.GetEffectiveSupplierDebitNoteApplications(
            new[] { first, second, reversal });

        effective.Should().ContainSingle().Which.Id.Should().Be(second.Id);
        effective.Sum(item => item.ApplicationAmount).Should().Be(50m);
    }

    [Fact]
    [Trait("Category", "AccountsPayable")]
    public void SupplierCreditApplicationDoesNotConsumePaymentCashTwice()
    {
        var payment = new VendorPayment
        {
            TotalAmount = 800m,
            AllocatedAmount = 800m
        };
        payment.SupplierDebitNoteApplications.Add(Application(200m));

        payment.UnallocatedAmount.Should().Be(0m);
        (payment.AllocatedAmount + payment.SupplierDebitNoteApplications.Sum(item => item.ApplicationAmount))
            .Should().Be(1_000m, "GHS 800 cash plus GHS 200 posted supplier credit settles a GHS 1,000 invoice");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void ModelMapsTenantScopedApplicationLedgerWithRestrictiveRelationships()
    {
        using var context = CreateContext();
        // Check constraints and some relational metadata are intentionally omitted from
        // EF Core's read-optimized runtime model, so inspect the design-time model here.
        var model = context.GetService<IDesignTimeModel>().Model;
        var entity = model.FindEntityType(typeof(SupplierDebitNoteApplication));

        entity.Should().NotBeNull();
        entity!.GetTableName().Should().Be("SupplierDebitNoteApplications");
        entity.GetForeignKeys().Should().OnlyContain(key => key.DeleteBehavior == DeleteBehavior.Restrict);
        entity.GetIndexes().Should().Contain(index =>
            index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(new[] { nameof(SupplierDebitNoteApplication.TenantId), nameof(SupplierDebitNoteApplication.OriginalApplicationId) }));
        entity.GetCheckConstraints().Should().Contain(constraint =>
            constraint.Name == "CK_SupplierDebitNoteApplications_Amount" &&
            constraint.Sql!.Contains("ApplicationAmount", StringComparison.Ordinal));

        var note = model.FindEntityType(typeof(SupplierDebitNote));
        note!.FindProperty(nameof(SupplierDebitNote.RowVersion))!.IsConcurrencyToken.Should().BeTrue();
        note.FindProperty(nameof(SupplierDebitNote.ExchangeRate))!.GetColumnType().Should().Be("decimal(18,6)");
        note.FindProperty(nameof(SupplierDebitNote.BusinessPartnerRoleId)).Should().NotBeNull();
        note.FindProperty(nameof(SupplierDebitNote.BusinessPartnerApProfileVersionId)).Should().NotBeNull();
        note.FindProperty(nameof(SupplierDebitNote.BusinessPartnerCode)).Should().NotBeNull();

        var line = model.FindEntityType(typeof(SupplierDebitNoteLineItem));
        line!.FindProperty(nameof(SupplierDebitNoteLineItem.OriginalAccountTransactionId)).Should().NotBeNull();
        line.FindProperty(nameof(SupplierDebitNoteLineItem.ResolvedCreditAccountId)).Should().NotBeNull();
        line.FindProperty(nameof(SupplierDebitNoteLineItem.LineItemType))!.GetMaxLength().Should().Be(20);
        line.GetForeignKeys().Where(key =>
                key.Properties.Any(property => property.Name is
                    nameof(SupplierDebitNoteLineItem.OriginalAccountTransactionId) or
                    nameof(SupplierDebitNoteLineItem.OriginalVendorInvoiceLineItemId) or
                    nameof(SupplierDebitNoteLineItem.OriginalFinancePurchaseOrderItemId) or
                    nameof(SupplierDebitNoteLineItem.ResolvedCreditAccountId)))
            .Should().OnlyContain(key => key.DeleteBehavior == DeleteBehavior.Restrict);

        var taxComponent = model.FindEntityType(typeof(SupplierDebitNoteTaxComponent));
        taxComponent.Should().NotBeNull();
        taxComponent!.GetForeignKeys().Should().OnlyContain(key => key.DeleteBehavior == DeleteBehavior.Restrict);
        taxComponent.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(SupplierDebitNoteTaxComponent.TenantId),
                nameof(SupplierDebitNoteTaxComponent.SupplierDebitNoteLineItemId),
                nameof(SupplierDebitNoteTaxComponent.CalculationOrder),
                nameof(SupplierDebitNoteTaxComponent.TaxId)
            }));

        var identity = model.FindEntityType(typeof(ApSupplierIdentityLink));
        identity.Should().NotBeNull();
        identity!.GetIndexes().Count(index => index.IsUnique).Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void MigrationRepairsHistoricalTablesAndCreatesApplicationConstraints()
    {
        using var context = CreateContext();
        context.GetService<IMigrationsAssembly>().Migrations.Keys.Should()
            .Equal("20260916132000_DisposableDevelopmentCurrentModelBaseline");
        var sql = ArchivedMigrationSource.Read("20260818103000_AddSupplierDebitNoteLifecycleAndApplications.cs");

        sql.Should().Contain("OBJECT_ID(N'[dbo].[SupplierDebitNotes]', N'U') IS NULL");
        sql.Should().Contain("COL_LENGTH(N'dbo.SupplierDebitNotes', N'RowVersion')");
        sql.Should().Contain("EXEC(N'CREATE UNIQUE INDEX [UX_SupplierDebitNotes_Tenant_Vendor_SupplierReference]",
            "SQL Server must compile the filtered index after any legacy SupplierCreditNoteReference column repair");
        sql.Should().Contain("CREATE TABLE [dbo].[SupplierDebitNoteApplications]");
        sql.Should().Contain("CK_SupplierDebitNoteApplications_Amount");
        sql.Should().Contain("UX_SupplierDebitNoteApplication_Tenant_Original_Reversal");
        sql.Should().Contain("FOREIGN KEY ([VendorPaymentId]) REFERENCES [dbo].[VendorPayment] ([Id])");
        sql.Should().Contain("ALTER TABLE [dbo].[SupplierDebitNotes] ALTER COLUMN [ExchangeRate] decimal(18,6) NOT NULL");
        sql.Should().Contain("OBJECT_ID(N'[dbo].[SupplierReturns]', N'U') IS NOT NULL");
        sql.Should().Contain("FIN-INT-012/013 remain Planned");

        var precisionSql = ArchivedMigrationSource.Read("20260818123000_WidenSupplierDebitNoteExchangeRatePrecision.cs");
        precisionSql.Should().Contain("c.[scale] < 6");
        precisionSql.Should().Contain("ALTER TABLE [dbo].[SupplierDebitNotes] ALTER COLUMN [ExchangeRate] decimal(18,6) NOT NULL");

        var hardeningSql = ArchivedMigrationSource.Read("20260818130000_HardenSupplierDebitNoteLineageIdentityAndSettlement.cs");
        var hardeningBlocks = ArchivedMigrationSource.SqlBlocks("20260818130000_HardenSupplierDebitNoteLineageIdentityAndSettlement.cs");
        hardeningBlocks.Should().HaveCount(2);
        var schemaSql = hardeningBlocks[0];
        var controlSql = hardeningBlocks[1];
        schemaSql.Should().Contain("ADD [LineItemType] nvarchar(20) NULL");
        schemaSql.Should().NotContain("SET [LineItemType] = N'Expense'",
            "a historical AP line cannot be guessed to be an expense instead of inventory");
        controlSql.Should().Contain("WHERE [LineItemType] IS NULL");
        controlSql.Should().Contain("ALTER COLUMN [LineItemType] nvarchar(20) NOT NULL");
        hardeningSql.Should().Contain("SupplierDebitNoteTaxComponents");
        hardeningSql.Should().Contain("ApSupplierIdentityLinks");
        hardeningSql.Should().Contain("[PartnerType] IN (N'Supplier', N'Contractor', N'Both')");
        hardeningSql.Should().Contain("[IsActive] = 1");
        hardeningSql.Should().Contain("FK_SupplierDebitNotes_BusinessPartners_VendorId");
        hardeningSql.Should().Contain("[delete_referential_action] <> 0");
        hardeningSql.Should().Contain("FK_SupplierDebitNoteLineItems_VendorInvoiceLineItem_OriginalVendorInvoiceLineItemId");
        hardeningSql.Should().Contain("FK_SupplierDebitNoteLineItems_FinancePurchaseOrderItems_OriginalFinancePurchaseOrderItemId");
        hardeningSql.Should().Contain("IX_AccountTransactions_TenantId_SourceDocumentId_SourceDocumentLineId");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void AccountingEvidenceMigrationsRejectDestructiveDowngrades()
    {
        foreach (var file in new[] { "20260818103000_AddSupplierDebitNoteLifecycleAndApplications.cs",
            "20260818123000_WidenSupplierDebitNoteExchangeRatePrecision.cs",
            "20260818130000_HardenSupplierDebitNoteLineageIdentityAndSettlement.cs" })
            ArchivedMigrationSource.Read(file).Should().Contain("throw new NotSupportedException");
    }

    [Fact]
    [Trait("Category", "AccountsPayable")]
    public void LinkedLineageTaxIdentityAndSettlementControlsFailClosedInSource()
    {
        var root = FindRepositoryRoot();
        var debitNoteService = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Api", "Services", "Finance", "AP", "SupplierDebitNoteService.cs"));
        debitNoteService.Should().Contain("IsolationLevel.Serializable");
        debitNoteService.Should().Contain("ApSettlementLockKeys.Invoice");
        debitNoteService.Should().Contain("AP_DEBIT_NOTE_SOURCE_LINEAGE_UNAVAILABLE");
        debitNoteService.Should().Contain("SourceDocumentLineId == sourceLine.Id");
        debitNoteService.Should().Contain("_taxEngine.CalculateTaxesAsync");
        debitNoteService.Should().Contain("return invoice.ExchangeRate;");
        debitNoteService.Should().Contain("note.ExchangeRate <= 0m");
        debitNoteService.Should().Contain("note.TotalAmount * note.ExchangeRate");
        debitNoteService.Should().NotContain("NormalizeExchangeRate");
        debitNoteService.Should().Contain("RequireLineItemType");
        debitNoteService.Should().Contain("ValidateLineClassificationEvidence(note);");
        debitNoteService.Should().Contain("AP_DEBIT_NOTE_LINE_CLASSIFICATION_REQUIRED");
        debitNoteService.Should().NotContain("NormalizeLineItemType",
            "legacy classifications must fail closed instead of silently becoming expenses");
        debitNoteService.Should().Contain("SupplierReturnId stays as scalar historical");
        debitNoteService.Should().NotContain(".Include(item => item.SupplierReturn)",
            "standalone Finance reads must not depend on the quarantined optional return table");

        var paymentService = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Api", "Services", "Finance", "AP", "VendorPaymentService.cs"));
        paymentService.Should().Contain("ApSettlementLockKeys.Payment");
        paymentService.Should().Contain("application.SupplierDebitNote.ExchangeRate <= 0m");
        paymentService.Should().Contain("FunctionalAmount = RoundMoney(requestedAmount * note.ExchangeRate)");
        paymentService.Should().NotContain("NormalizeExchangeRate(note.ExchangeRate)");
        paymentService.Should().NotContain("s.Name == partner.PartnerName");
        paymentService.Should().NotContain("Auto-created from business partner");

        var identityService = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Api", "Services", "Finance", "AP", "ApSupplierIdentityService.cs"));
        identityService.Should().Contain("LookupAsync");
        identityService.Should().Contain("item.IsActive");
        identityService.Should().NotContain("PartnerName ==");

        var invoiceService = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Api", "Services", "Finance", "AP", "VendorInvoiceService.cs"));
        invoiceService.Should().Contain("Cannot void an invoice");
        invoiceService.Should().Contain("Repository<SupplierDebitNoteApplication>");
    }

    [Fact]
    [Trait("Category", "FrontendContract")]
    public void LinkedDebitNoteFormUsesFrozenInvoiceEvidenceAndReadOnlyAmounts()
    {
        var root = FindRepositoryRoot();
        var form = File.ReadAllText(Path.Combine(
            root, "frontend", "src", "components", "finance", "ap", "SupplierDebitNoteForm.tsx"));
        form.Should().MatchRegex(
            @"linkedInvoice\?\.exchangeRate\s*\?\?\s*\(await resolveApprovedRate\(\)\)");
        form.Should().MatchRegex(@"return\s+linkedDraftLine\(\s*source,");
        form.Should().Contain("disabled={isLinkedNote}");
        form.Should().Contain("Frozen source-invoice rate");
        form.Should().Contain("one unambiguous AP supplier identity");
        form.Should().NotContain("const approvedRate = await resolveApprovedRate();");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void ApSupplierEntryUsesOneGovernedFinanceIdentityBoundary()
    {
        var root = FindRepositoryRoot();
        var identityService = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Api", "Services", "Finance", "AP", "ApSupplierIdentityService.cs"));
        identityService.Should().Contain("BusinessPartnerLifecyclePolicy.IsOperationallyApproved");
        identityService.Should().Contain("BuildFinanceSupplierProjection(partner)");
        identityService.Should().Contain("BusinessPartnerProjection");
        identityService.Should().NotContain("partner.PartnerName ==");

        var invoiceService = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Api", "Services", "Finance", "AP", "VendorInvoiceService.cs"));
        invoiceService.Should().Contain("_apSupplierIdentityService.ResolveAsync");
        invoiceService.Should().NotContain("s.Name == partner.PartnerName");
        invoiceService.Should().NotContain("Auto-created from business partner");

        var reportsService = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Api", "Services", "Finance", "AP", "ApReportsService.cs"));
        reportsService.Should().Contain("Repository<ApSupplierIdentityLink>");
        reportsService.Should().NotContain("string.Equals(s.Name, partner.PartnerName");
        reportsService.Should().NotContain("string.Equals(p.PrimaryEmail, supplier.Email");

        var paymentPage = File.ReadAllText(Path.Combine(
            root, "frontend", "src", "app", "finance", "ap", "payments", "create", "page.tsx"));
        paymentPage.Should().Contain("getInvoiceSupplierEntryOptions");
        paymentPage.Should().NotContain("businessPartnerService.getPartners");

        var supplierRegister = File.ReadAllText(Path.Combine(
            root, "frontend", "src", "app", "finance", "ap", "suppliers", "page.tsx"));
        supplierRegister.Should().Contain("Linked on first use");
        supplierRegister.Should().Contain("Legacy AP supplier");

        var sidebar = File.ReadAllText(Path.Combine(
            root, "frontend", "src", "components", "layout", "sidebar.tsx"));
        sidebar.Should().Contain("href: '/finance/ap/suppliers'");
    }

    [Fact]
    [Trait("Category", "FinanceSecurity")]
    public void LifecycleMutationsUsePurposeSpecificPermissions()
    {
        var expected = new Dictionary<string, string>
        {
            [nameof(SupplierDebitNotesController.Create)] = FinancePermissions.ManageApSupplierDebitNotes,
            [nameof(SupplierDebitNotesController.Update)] = FinancePermissions.ManageApSupplierDebitNotes,
            [nameof(SupplierDebitNotesController.Submit)] = FinancePermissions.SubmitApSupplierDebitNotes,
            [nameof(SupplierDebitNotesController.ProcessApproval)] = FinancePermissions.ApproveApSupplierDebitNotes,
            [nameof(SupplierDebitNotesController.Post)] = FinancePermissions.PostApSupplierDebitNotes,
            [nameof(SupplierDebitNotesController.Cancel)] = FinancePermissions.ManageApSupplierDebitNotes,
            [nameof(SupplierDebitNotesController.Reverse)] = FinancePermissions.ReverseApSupplierDebitNotes
        };

        foreach (var (action, permission) in expected)
        {
            var method = typeof(SupplierDebitNotesController).GetMethod(action);
            method.Should().NotBeNull();
            method!.GetCustomAttributes<AuthorizeAttribute>()
                .Select(attribute => attribute.Policy)
                .Should().Contain(permission);
            FinancePermissions.AllNames.Should().Contain(permission);
        }
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void WorkflowSeedAndGenericControllerGuardUseCanonicalDebitNoteEntity()
    {
        var seedMethod = typeof(DatabaseSeedingService).GetMethod(
            "GetFinanceWorkflowSeedSpecs",
            BindingFlags.Static | BindingFlags.NonPublic);
        seedMethod.Should().NotBeNull();
        var entityCodes = ((IEnumerable)seedMethod!.Invoke(null, null)!)
            .Cast<object>()
            .Select(spec => spec.GetType().GetProperty("EntityCode")!.GetValue(spec)?.ToString())
            .ToList();
        entityCodes.Should().Contain("SupplierDebitNote");
        entityCodes.Should().NotContain("SupplierReturn",
            "FIN-INT-012/013 remain planned and cannot be implied by a Finance workflow seed");

        var root = FindRepositoryRoot();
        var workflowController = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Api", "Controllers", "WorkflowController.cs"));
        workflowController.Should().Contain("SupplierDebitNote");
        workflowController.Should().Contain(
            "route = $\"/api/ap/supplier-debit-notes/{workflowMetadata.EntityId}/approval\"",
            "the generic workflow endpoint must redirect to the concrete Finance-owned document approval route");

        var seeder = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Api", "Services", "DatabaseSeedingService.cs"));
        seeder.Should().Contain("RetireQuarantinedSupplierReturnWorkflowDefinitionsAsync(tenantId)");
        seeder.Should().Contain("historical SupplierReturn definitions must not authorize a Procurement/");
        seeder.Should().Contain("FIN-INT-012/013 remain");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void FinanceWorkflowSeedRetiresEveryActiveSupplierReturnDefinitionIdempotently()
    {
        var tenantId = Guid.NewGuid();
        var retiredAt = new DateTime(2026, 8, 18, 14, 30, 0, DateTimeKind.Utc);
        var returnEntityType = new WorkflowEntityType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "Supplier_Return",
            Name = "Supplier Return",
            EntityClassName = typeof(SupplierReturn).FullName
        };
        var debitNoteEntityType = new WorkflowEntityType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = "SupplierDebitNote",
            Name = "Supplier Debit Note",
            EntityClassName = typeof(SupplierDebitNote).FullName
        };
        var activeReturn = new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityTypeId = returnEntityType.Id,
            EntityType = returnEntityType,
            Name = "Supplier Return Approval",
            IsActive = true,
            LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
            PublishedAt = retiredAt.AddDays(-1),
            RetiredById = Guid.NewGuid(),
            LastModifiedById = Guid.NewGuid()
        };
        var inactiveReturn = new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityTypeId = returnEntityType.Id,
            EntityType = returnEntityType,
            Name = "Already Inactive Supplier Return Approval",
            IsActive = false,
            LifecycleStatus = WorkflowDefinitionLifecycleStatus.Retired,
            RetiredAt = retiredAt.AddDays(-2),
            UpdatedBy = "Prior Actor"
        };
        var activeDebitNote = new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EntityTypeId = debitNoteEntityType.Id,
            EntityType = debitNoteEntityType,
            Name = "Supplier Debit Note Approval",
            IsActive = true,
            LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published
        };

        var retireMethod = typeof(DatabaseSeedingService).GetMethod(
            "RetireQuarantinedSupplierReturnWorkflowDefinitions",
            BindingFlags.Static | BindingFlags.NonPublic);
        retireMethod.Should().NotBeNull();

        var definitions = new[] { activeReturn, inactiveReturn, activeDebitNote };
        var retiredCount = (int)retireMethod!.Invoke(null, new object[] { definitions, retiredAt })!;

        retiredCount.Should().Be(1);
        activeReturn.IsActive.Should().BeFalse();
        activeReturn.LifecycleStatus.Should().Be(WorkflowDefinitionLifecycleStatus.Retired);
        activeReturn.RetiredAt.Should().Be(retiredAt);
        activeReturn.RetiredById.Should().BeNull("startup retirement is a named System action, not an impersonated user");
        activeReturn.UpdatedAt.Should().Be(retiredAt);
        activeReturn.UpdatedBy.Should().Be("System");
        activeReturn.LastModifiedById.Should().BeNull();
        activeReturn.PublishedAt.Should().Be(retiredAt.AddDays(-1),
            "retirement must retain the historical publication evidence");

        inactiveReturn.RetiredAt.Should().Be(retiredAt.AddDays(-2));
        inactiveReturn.UpdatedBy.Should().Be("Prior Actor",
            "a second seed pass must leave an already inactive definition unchanged");
        activeDebitNote.IsActive.Should().BeTrue();
        activeDebitNote.LifecycleStatus.Should().Be(WorkflowDefinitionLifecycleStatus.Published);
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void ForeignCurrencyAndSnapshotControlsFailClosed()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Api", "Services", "Finance", "AP", "SupplierDebitNoteService.cs"));
        service.Should().Contain("ResolveApprovedExchangeRateAsync");
        service.Should().Contain("ValidatePersistedExchangeRateEvidenceAsync");
        service.Should().Contain("No active approved {side} Daily exchange rate exists");
        service.Should().Contain("(!allowControlAccount && account.IsControlAccount)");
        service.Should().Contain("(requireDirectPosting && !account.AllowDirectPosting)");
        service.Should().Contain("item.Applicability == TaxApplicability.Purchases");
        service.Should().Contain("item.Applicability == TaxApplicability.Both");
        service.Should().Contain("component.Tax.Applicability != TaxApplicability.Purchases");
        var uniquenessStart = service.IndexOf("private async Task ValidateSupplierReferenceUniqueAsync", StringComparison.Ordinal);
        var rateStart = service.IndexOf("private async Task<decimal> ResolveApprovedExchangeRateAsync", uniquenessStart, StringComparison.Ordinal);
        uniquenessStart.Should().BeGreaterThanOrEqualTo(0);
        rateStart.Should().BeGreaterThan(uniquenessStart);
        service[uniquenessStart..rateStart].Should().NotContain("SupplierDebitNoteStatus.Cancelled",
            "cancelled documents retain their external supplier reference for audit and the database index enforces immutable uniqueness");

        var snapshot = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Data", "Migrations", "ApplicationDbContextModelSnapshot.cs"));
        snapshot.Should().Contain("SupplierDebitNoteApplication");
        snapshot.Should().Contain("UX_SupplierDebitNoteApplication_Tenant_Original_Reversal");
    }

    private static SupplierDebitNoteApplication Application(
        decimal amount,
        bool isReversal = false,
        Guid? originalApplicationId = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        SupplierDebitNoteId = Guid.NewGuid(),
        VendorPaymentId = Guid.NewGuid(),
        VendorInvoiceId = Guid.NewGuid(),
        ApplicationAmount = amount,
        FunctionalAmount = amount,
        CurrencyCode = "GHS",
        ExchangeRate = 1m,
        ApplicationDate = DateTime.UtcNow,
        IsReversal = isReversal,
        OriginalApplicationId = originalApplicationId
    };

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=SupplierDebitNoteModel;Trusted_Connection=True")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("RHEMA_REPOSITORY_ROOT");
        if (!string.IsNullOrWhiteSpace(configuredRoot) &&
            Directory.Exists(Path.Combine(configuredRoot, "src")))
            return Path.GetFullPath(configuredRoot);

        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
                directory = directory.Parent;
            if (directory != null)
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

}
