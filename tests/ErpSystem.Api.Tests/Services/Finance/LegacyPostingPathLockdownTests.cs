using ErpSystem.Api.Configuration;
using ErpSystem.Api.Extensions;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class LegacyPostingPathLockdownTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-LegacyPostingLockdown")]
    [Trait("Category", "Architecture")]
    public void NormalRuntime_ShouldNotRegisterLegacySubledgerPostingService()
    {
        var root = FindRepositoryRoot();
        var serviceCollection = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Extensions", "ServiceCollectionExtensions.cs"));
        var program = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Program.cs"));

        serviceCollection.Should().NotContain("ISubledgerPostingService, ErpSystem.Api.Services.Finance.GL.SubledgerPostingService");
        program.Should().NotContain("GetRequiredService<ErpSystem.Core.Interfaces.Finance.ISubledgerPostingService>");
        program.Should().Contain("legacy post-finance-grv maintenance command is disabled");
    }

    [Fact]
    [Trait("Category", "Deployment")]
    public void MigrationOnlyCommand_ShouldPreserveAspNetCoreProductionDefault()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Program.cs"));
        var migrationStart = program.IndexOf(
            "if (args.Length > 0 && args[0] == \"apply-migrations\")",
            StringComparison.Ordinal);
        var seedStart = program.IndexOf(
            "if (args.Length > 0",
            migrationStart + 1,
            StringComparison.Ordinal);
        migrationStart.Should().BeGreaterThanOrEqualTo(0);
        seedStart.Should().BeGreaterThan(migrationStart);

        var migrationCommand = program[migrationStart..seedStart];
        migrationCommand.Should().Contain("WebApplication.CreateBuilder(args)");
        migrationCommand.Should().Contain("tempBuilder.Services.AddHttpContextAccessor()")
            .And.Contain("tempBuilder.Services.AddErpSystemCliDatabase(");
        migrationCommand.IndexOf("AddHttpContextAccessor", StringComparison.Ordinal).Should().BeLessThan(
            migrationCommand.IndexOf("AddErpSystemCliDatabase", StringComparison.Ordinal),
            "the audited DbContext dependency must be registered before host validation");
        migrationCommand.Should().NotContain("CreateSeedBuilder(args)");
        migrationCommand.Should().NotContain("Environment.SetEnvironmentVariable");
    }

    [Fact]
    [Trait("Category", "Deployment")]
    public void MigrationOnlyCommand_ShouldApplyBoundedCliTimeoutBeforeMigration()
    {
        var parsed = MigrationCommandOptions.Parse(
            ["apply-migrations", MigrationCommandOptions.TimeoutArgument, "600"]);
        using var services = BuildDatabaseServices(cli: true, parsed.CommandTimeoutSeconds);
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        parsed.ApplyAndAssertTo(context.Database);

        parsed.CommandTimeoutSeconds.Should().Be(600);
        context.Database.GetCommandTimeout().Should().Be(600);
        context.Database.CreateExecutionStrategy().RetriesOnFailure.Should().BeFalse();

        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Program.cs"));
        var migrationStart = program.IndexOf(
            "if (args.Length > 0 && args[0] == \"apply-migrations\")",
            StringComparison.Ordinal);
        migrationStart.Should().BeGreaterThanOrEqualTo(0);
        var seedStart = program.IndexOf("if (args.Length > 0", migrationStart + 1, StringComparison.Ordinal);
        seedStart.Should().BeGreaterThan(migrationStart);
        var migrationCommand = program[migrationStart..seedStart];
        migrationCommand.IndexOf("migrationCommandOptions.ApplyAndAssertTo(db.Database)", StringComparison.Ordinal)
            .Should().BeLessThan(migrationCommand.IndexOf("db.Database.MigrateAsync()", StringComparison.Ordinal));
        migrationCommand.IndexOf("RHEMA_MIGRATION_COMMAND_TIMEOUT_SECONDS", StringComparison.Ordinal)
            .Should().BeLessThan(migrationCommand.IndexOf("db.Database.MigrateAsync()", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Deployment")]
    public void MigrationOnlyCommand_ShouldKeepOrdinaryProviderTimeoutAndRefuseUnsafeOverrides()
    {
        var defaultOptions = MigrationCommandOptions.Parse(["apply-migrations"]);
        defaultOptions.CommandTimeoutSeconds.Should().Be(600);
        MigrationCommandOptions.Parse(
            ["seed-db", MigrationCommandOptions.TimeoutArgument, "600"]).CommandTimeoutSeconds.Should().Be(600);
        foreach (var command in new[] { "seed-deployment-uat", "seed-operational-uat" })
        {
            MigrationCommandOptions.Parse([command]).CommandTimeoutSeconds.Should().Be(600);
            MigrationCommandOptions.Parse([command, MigrationCommandOptions.TimeoutArgument, "300"])
                .CommandTimeoutSeconds.Should().Be(300);
            var unsafeTimeout = () => MigrationCommandOptions.Parse([command, MigrationCommandOptions.TimeoutArgument, "0"]);
            unsafeTimeout.Should().Throw<InvalidOperationException>();
            var unsupportedOption = () => MigrationCommandOptions.Parse([command, "--unknown", "600"]);
            unsupportedOption.Should().Throw<InvalidOperationException>();
        }

        using var services = BuildDatabaseServices(cli: false, commandTimeoutSeconds: 600);
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Database.GetCommandTimeout().Should().Be(30);
        context.Database.CreateExecutionStrategy().RetriesOnFailure.Should().BeTrue();
        var unsafeProfile = () => defaultOptions.ApplyAndAssertTo(context.Database);
        unsafeProfile.Should().Throw<InvalidOperationException>()
            .WithMessage("*forbid automatic execution-strategy retries*");

        MigrationCommandOptions.Parse(
            ["apply-migrations", MigrationCommandOptions.TimeoutArgument, "30"]).CommandTimeoutSeconds.Should().Be(30);
        MigrationCommandOptions.Parse(
            ["apply-migrations", MigrationCommandOptions.TimeoutArgument, "900"]).CommandTimeoutSeconds.Should().Be(900);

        var invalidArguments = new[]
        {
            new[] { "apply-migrations", MigrationCommandOptions.TimeoutArgument },
            new[] { "apply-migrations", MigrationCommandOptions.TimeoutArgument, "0" },
            new[] { "apply-migrations", MigrationCommandOptions.TimeoutArgument, "29" },
            new[] { "apply-migrations", MigrationCommandOptions.TimeoutArgument, "901" },
            new[] { "apply-migrations", MigrationCommandOptions.TimeoutArgument, "infinite" },
            new[] { "apply-migrations", MigrationCommandOptions.TimeoutArgument, "600", MigrationCommandOptions.TimeoutArgument, "600" },
            new[] { "apply-migrations", "--unknown", "600" },
            new[] { "seed", MigrationCommandOptions.TimeoutArgument, "600" }
        };

        foreach (var invalid in invalidArguments)
        {
            var action = () => MigrationCommandOptions.Parse(invalid);
            action.Should().Throw<InvalidOperationException>();
        }

        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Program.cs"));
        program.Should().Contain("AddErpSystemCliDatabase")
            .And.Contain("migrationCommandOptions.ApplyAndAssertTo(db.Database)");
        var seedStart = program.IndexOf("if (args.Length > 0 && args[0] is \"seed-db\" or \"seed-deployment-uat\")", StringComparison.Ordinal);
        seedStart.Should().BeGreaterThanOrEqualTo(0);
        var seedEnd = program.IndexOf("// Check for HR module seeding command", seedStart, StringComparison.Ordinal);
        seedEnd.Should().BeGreaterThan(seedStart);
        var seedCommand = program[seedStart..seedEnd];
        seedCommand.IndexOf("migrationCommandOptions.ApplyAndAssertTo(db.Database)", StringComparison.Ordinal)
            .Should().BeLessThan(seedCommand.IndexOf("db.Database.MigrateAsync()", StringComparison.Ordinal));
        seedCommand.Should().Contain("AddErpSystemCliDatabase")
            .And.Contain("RHEMA_MIGRATION_COMMAND_TIMEOUT_SECONDS");

        foreach (var migrationSource in new[]
        {
            "20260916132000_DisposableDevelopmentCurrentModelBaseline.cs",
            "ArchivedGovernanceBaselineSql.cs",
            "FinanceC1C8BaselineAuthoritySql.cs"
        })
        {
            File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Data", "Migrations", migrationSource))
                .Should().NotContain("suppressTransaction: true",
                    "the sole baseline and all 493-trigger/108-patch helpers must roll back with migration history on failure");
        }
    }

    private static ServiceProvider BuildDatabaseServices(bool cli, int commandTimeoutSeconds)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "SqlServer",
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=localhost;Database=TimeoutPolicyOnly;Integrated Security=true;TrustServerCertificate=true",
                ["Audit:Enabled"] = "false"
            })
            .Build();
        var services = new ServiceCollection();
        if (cli)
        {
            services.AddErpSystemCliDatabase(configuration, commandTimeoutSeconds);
        }
        else
        {
            services.AddErpSystemDatabase(configuration);
        }
        return services.BuildServiceProvider();
    }

    [Fact]
    [Trait("Category", "Deployment")]
    public void GlCutoverHarness_ShouldGuardCloneAndSanitizeDurableEvidence()
    {
        var root = FindRepositoryRoot();
        var harness = File.ReadAllText(Path.Combine(
            root, "scripts", "finance", "Invoke-GlCutoverRehearsal.ps1"));

        harness.Should().Contain("'RehearseClone'")
            .And.Contain("exact configured RhemaERP catalog")
            .And.Contain("same SQL Server instance")
            .And.Contain("Evidence directory must be new or empty")
            .And.Contain("WITH COPY_ONLY, CHECKSUM")
            .And.Contain("RESTORE VERIFYONLY")
            .And.Contain("DBCC CHECKDB")
            .And.Contain("source-fingerprint-before.txt")
            .And.Contain("source-fingerprint-after.txt")
            .And.Contain("targetDerivedBackupExists = $false")
            .And.Contain("<REDACTED>")
            .And.Contain("<REPOSITORY>");
        harness.Should().NotContain("dotnet ef database update");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-LegacyPostingLockdown")]
    [Trait("Category", "Architecture")]
    public void NormalRuntime_ShouldNotDependOnLegacySubledgerPostingService()
    {
        var root = FindRepositoryRoot();
        var allowedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Normalize(Path.Combine(root, "src", "ErpSystem.Core", "Interfaces", "Finance", "ISubledgerPostingService.cs")),
            Normalize(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "GL", "SubledgerPostingService.cs"))
        };

        var offenders = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !allowedFiles.Contains(Normalize(path)))
            .Where(path =>
            {
                var text = File.ReadAllText(path);
                return text.Contains("ISubledgerPostingService", StringComparison.Ordinal)
                    || text.Contains("PostApInvoiceAsync", StringComparison.Ordinal)
                    || text.Contains("PostApPaymentAsync", StringComparison.Ordinal)
                    || text.Contains("PostArInvoiceAsync", StringComparison.Ordinal)
                    || text.Contains("PostArPaymentAsync", StringComparison.Ordinal)
                    || text.Contains("PostSalesCreditNoteAsync", StringComparison.Ordinal)
                    || text.Contains("PostFinancePurchaseOrderReceiptAsync", StringComparison.Ordinal)
                    || text.Contains("PostSupplierDebitNoteAsync", StringComparison.Ordinal);
            })
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();

        offenders.Should().BeEmpty("normal runtime Finance code must not reference legacy subledger posting methods");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-LegacyPostingLockdown")]
    [Trait("Category", "Architecture")]
    public void GenericFinanceControllerPostingEndpoint_ShouldRejectBypassInsteadOfCallingGeneralLedgerDirectPost()
    {
        var root = FindRepositoryRoot();
        var controller = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Controllers", "Finance", "FinanceController.cs"));

        controller.Should().Contain("PostingEngineBypassRejected");
        controller.Should().Contain("Legacy generic GL post-to-ledger endpoint is disabled");
        controller.Should().NotContain("_glService.PostJournalEntryAsync");
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void VendorInvoiceApproval_ShouldNotCreateInventoryReceipts()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Api", "Services", "Finance", "AP",
            "VendorInvoiceService.cs"));
        var approvals = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Api", "Controllers", "Finance",
            "FinanceApprovalsController.cs"));

        service.Should().NotContain("ProcessReceiptAsync(");
        approvals.Should().NotContain("ProcessReceiptAsync(");
        service.Should().Contain(
            "Inventory is posted only by the governed purchase-receipt/inspection");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-LegacyPostingLockdown")]
    [Trait("Category", "Architecture")]
    public void CurrentFinancePostingServices_ShouldRouteThroughPostingEngine()
    {
        var root = FindRepositoryRoot();
        var expectedPostingEngineFiles = new[]
        {
            Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "AP", "VendorInvoiceService.cs"),
            Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "AP", "VendorPaymentService.cs"),
            Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "AR", "InvoiceService.cs"),
            Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "AR", "PaymentService.cs"),
            Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "Cash", "CashTransactionService.cs"),
            Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "SubledgerAdjustmentJournalService.cs"),
            Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "MultiCurrency", "CurrencyRevaluationService.cs"),
            Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "FixedAssets", "FixedAssetService.cs"),
            Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "FixedAssets", "FixedAssetDepreciationService.cs"),
            Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "FixedAssets", "AssetValuationService.cs"),
            Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "FixedAssets", "AssetDisposalService.cs"),
            Path.Combine(root, "src", "ErpSystem.Api", "Controllers", "Finance", "FinancePurchaseOrderController.cs")
        };

        foreach (var path in expectedPostingEngineFiles)
        {
            File.ReadAllText(path).Should().Contain("PostAsync", $"posting-capable file {Path.GetRelativePath(root, path)} should call IFinancePostingEngine");
        }

        // This inherited controller is a quarantined compatibility surface, not a posting producer.
        File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Controllers", "Finance", "SupplierReturnsController.cs"))
            .Should().NotContain("ISubledgerPostingService");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-LegacyPostingLockdown")]
    [Trait("Category", "Architecture")]
    public void ReturnOrderService_ShouldUseOnlyGovernedProducerContracts()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Services", "Sales", "ReturnOrderService.cs"));

        service.Should().Contain("IFinanceProducerIntentService") // C7 preparation
            .And.Contain("IFinanceProducerApprovedExecutionService") // C11/C12 execution
            .And.Contain("IFinanceProducerReversalPreparationService") // C13 reversal
            .And.Contain("IFinanceProducerReplayVerificationService") // C15 replay
            .And.Contain("PrepareAsync(")
            .And.Contain("GetAsync(prepared.Id")
            .And.Contain("decision.ProducerDecisionStatus != ProducerIntentDecisionStatuses.Approved")
            .And.Contain("ExecuteInAmbientTransactionAsync(")
            .And.Contain("PrepareReversalAsync(")
            .And.Contain("VerifyPostedAsync(");

        service.Should().NotContain("IFinancePostingEngine")
            .And.NotContain("ISubledgerPostingService")
            .And.NotContain("AccountingBook")
            .And.NotContain("BookCode")
            .And.NotContain("GetActiveBooks")
            .And.NotContain("ALL_ACTIVE_BOOKS")
            .And.NotContain("DecideProducerAccountingIntentDto")
            .And.NotContain("ProducerDecisionStatuses.Approved =")
            .And.NotContain(".PostAsync(");

        var governedPost = service[service.IndexOf("PostCreditNoteAsync", StringComparison.Ordinal)
            ..service.IndexOf("ApplyCreditNoteAsync", StringComparison.Ordinal)];
        governedPost.Should().NotContain("ApprovePreparedAsync")
            .And.NotContain(".ApproveAsync(")
            .And.NotContain("ProducerDecisionStatus = ProducerIntentDecisionStatuses.Approved");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-LegacyPostingLockdown")]
    [Trait("Category", "Architecture")]
    public void BankMasterCreation_ShouldNotExposeLegacyOpeningBalanceInputs()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "Cash", "BankAccountService.cs"));
        var dto = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "DTOs", "Finance", "BankAccountDtos.cs"));
        var createDto = dto[dto.IndexOf("public class CreateBankAccountDto", StringComparison.Ordinal)
            ..dto.IndexOf("public class UpdateBankAccountDto", StringComparison.Ordinal)];

        createDto.Should().NotContain("OpeningBalance")
            .And.NotContain("OpeningBalanceExchangeRate")
            .And.NotContain("OpeningDate");
        service.Should().Contain("OpeningBalance = 0m")
            .And.Contain("CurrentBalance = 0m")
            .And.Contain("AvailableBalance = 0m");
        service.Should().NotContain("_journalEntryService");
        service.Should().NotContain("CreateJournalEntryAsync");
        service.Should().NotContain("PostJournalEntryAsync");
        service.Should().NotContain("CreateOpeningBalancePostingAsync");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-OpeningBalances")]
    [Trait("Category", "Architecture")]
    public void LegacyOpeningBalanceEntryPoints_ShouldRemainRetired()
    {
        var root = FindRepositoryRoot();
        var subledgerService = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "SubledgerAdjustmentJournalService.cs"));
        var journalService = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "GL", "JournalEntryService.cs"));
        var entity = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Entities", "Finance", "SubledgerAdjustmentJournal.cs"));
        var dto = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "DTOs", "Finance", "SubledgerAdjustmentJournalDtos.cs"));
        var subledgerPage = File.ReadAllText(Path.Combine(root, "frontend", "src", "app", "finance", "subledger-adjustments", "new", "page.tsx"));
        var journalPage = File.ReadAllText(Path.Combine(root, "frontend", "src", "app", "finance", "journal-entries", "new", "page.tsx"));

        // Historical values remain readable and reversible, but no active creation surface accepts them.
        entity.Should().Contain("public static class SubledgerAdjustmentPurposes");
        entity.Should().Contain("public string Purpose");
        dto.Should().Contain("public string? Purpose");
        dto.Should().Contain("public string Purpose");

        subledgerService.Should().Contain("allowRetiredOpeningBalance: originalAdjustmentId.HasValue");
        subledgerService.Should().Contain("Subledger opening-balance adjustments are retired");
        subledgerService.Should().Contain("Purpose = original.Purpose");
        journalService.Should().Contain("EnsureLegacyOpeningBalanceIsRetired(dto.JournalType)");
        journalService.Should().Contain("Manual opening-balance journals are retired");

        subledgerPage.Should().NotContain("<SelectItem value=\"OpeningBalance\"");
        subledgerPage.Should().NotContain("financeDataService.getFinanceSettings()");
        journalPage.Should().NotContain("<SelectItem value=\"Opening Balance\"");
        journalPage.Should().NotContain("ALL_ACTIVE_BOOKS_CODE");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CanonicalBusinessPartner")]
    [Trait("Category", "Architecture")]
    public void SubledgerAdjustments_ShouldUseGovernedCanonicalBusinessPartnerIdentity()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "SubledgerAdjustmentJournalService.cs"));
        var entity = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Entities", "Finance", "SubledgerAdjustmentJournal.cs"));
        var dto = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "DTOs", "Finance", "SubledgerAdjustmentJournalDtos.cs"));
        var page = File.ReadAllText(Path.Combine(root, "frontend", "src", "app", "finance", "subledger-adjustments", "new", "page.tsx"));
        var migrationPath = Directory.GetFiles(
                Path.Combine(root, "src", "ErpSystem.Data", "Migrations"),
                "*CanonicalSubledgerAdjustmentBusinessPartnerIdentity.cs")
            .Single(path => !path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase));
        var migration = File.ReadAllText(migrationPath).Replace("\r\n", "\n", StringComparison.Ordinal);

        entity.Should().Contain("public Guid BusinessPartnerId")
            .And.Contain("public Guid BusinessPartnerRoleId")
            .And.Contain("public Guid? BusinessPartnerApProfileVersionId")
            .And.Contain("public Guid? BusinessPartnerArProfileVersionId")
            .And.Contain("public string BusinessPartnerCode")
            .And.Contain("public string BusinessPartnerName");
        entity.Should().NotContain("public Guid? CustomerId")
            .And.NotContain("public Guid? SupplierId");

        dto.Should().Contain("public Guid BusinessPartnerId")
            .And.Contain("public Guid? BusinessPartnerRoleId");
        dto.Should().NotContain("public Guid? CustomerId")
            .And.NotContain("public Guid? SupplierId");

        service.Should().Contain("BusinessPartnerFinanceProfilePolicy.ResolveAr")
            .And.Contain("BusinessPartnerFinanceProfilePolicy.ResolveAp")
            .And.Contain("BusinessPartnerCode = counterparty.Partner.PartnerCode")
            .And.Contain("BusinessPartnerName = counterparty.Partner.PartnerName");
        service.Should().NotContain("_context.Set<Supplier>()")
            .And.NotContain("new Supplier")
            .And.NotContain("dto.CustomerId")
            .And.NotContain("dto.SupplierId");

        page.Should().Contain("businessPartnerId: data.businessPartnerId")
            .And.Contain("accountsPayableService.getInvoiceSupplierEntryOptions()")
            .And.NotContain("customerId: data")
            .And.NotContain("supplierId: data");

        migration.Should().Contain("IF EXISTS (SELECT 1 FROM [dbo].[SubledgerAdjustmentJournals])")
            .And.Contain("requires the approved Finance transaction reset")
            .And.Contain("DropColumn(\n                name: \"CustomerId\"")
            .And.Contain("DropColumn(\n                name: \"SupplierId\"")
            .And.NotContain("RenameColumn(\n                name: \"CustomerId\"")
            .And.NotContain("RenameColumn(\n                name: \"SupplierId\"");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CanonicalBusinessPartner")]
    [Trait("Category", "Architecture")]
    public void CustomerReceipts_ShouldUseGovernedCanonicalBusinessPartnerIdentity()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "AR", "PaymentService.cs"));
        var entity = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Entities", "Finance", "CustomerPayment.cs"));
        var dto = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "DTOs", "AR", "PaymentCrudDtos.cs"));
        var createDto = dto[dto.IndexOf("public class PaymentCreateDto", StringComparison.Ordinal)..dto.IndexOf("public class PaymentUpdateDto", StringComparison.Ordinal)];
        var migrationPath = Directory.GetFiles(
                Path.Combine(root, "src", "ErpSystem.Data", "Migrations"),
                "*CanonicalCustomerPaymentBusinessPartnerIdentity.cs")
            .Single(path => !path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase));
        var migration = File.ReadAllText(migrationPath).Replace("\r\n", "\n", StringComparison.Ordinal);

        entity.Should().Contain("public Guid BusinessPartnerId")
            .And.Contain("public Guid BusinessPartnerRoleId")
            .And.Contain("public Guid BusinessPartnerArProfileVersionId")
            .And.Contain("public string BusinessPartnerCode")
            .And.Contain("public string BusinessPartnerName")
            .And.NotContain("public Guid CustomerId");
        createDto.Should().Contain("public Guid BusinessPartnerId")
            .And.Contain("public Guid? BusinessPartnerRoleId")
            .And.NotContain("public Guid CustomerId");
        service.Should().Contain("BusinessPartnerFinanceProfilePolicy.ResolveAr")
            .And.Contain("BusinessPartnerArProfileVersionId = counterparty.Profile.Id")
            .And.Contain("BusinessPartnerCode = customer.PartnerCode")
            .And.NotContain("dto.CustomerId")
            .And.NotContain("payment.CustomerId");
        migration.Should().Contain("IF EXISTS (SELECT 1 FROM [CustomerPayment])")
            .And.Contain("requires a fresh Finance transactional database")
            .And.Contain("DropColumn(\n                name: \"CustomerId\"")
            .And.NotContain("RenameColumn(\n                name: \"CustomerId\"");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CanonicalBusinessPartner")]
    [Trait("Category", "Architecture")]
    public void CustomerInvoices_ShouldUseGovernedCanonicalBusinessPartnerIdentity()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "AR", "InvoiceService.cs"));
        var entity = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "Entities", "Finance", "Invoice.cs"));
        var dto = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "DTOs", "AR", "PaymentCrudDtos.cs"));
        var createDto = dto[dto.IndexOf("public class InvoiceCreateDto", StringComparison.Ordinal)..dto.IndexOf("public class InvoiceLineItemCreateDto", StringComparison.Ordinal)];
        var migrationPath = Directory.GetFiles(
                Path.Combine(root, "src", "ErpSystem.Data", "Migrations"),
                "*CanonicalInvoiceBusinessPartnerEvidence.cs")
            .Single(path => !path.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase));
        var migration = File.ReadAllText(migrationPath);

        entity.Should().Contain("public Guid BusinessPartnerId")
            .And.Contain("public Guid BusinessPartnerRoleId")
            .And.Contain("public Guid BusinessPartnerArProfileVersionId")
            .And.Contain("public string BusinessPartnerCode")
            .And.NotContain("public Guid CustomerId");
        createDto.Should().Contain("public Guid BusinessPartnerId")
            .And.Contain("public Guid? BusinessPartnerRoleId")
            .And.NotContain("public Guid CustomerId");
        service.Should().Contain("BusinessPartnerFinanceProfilePolicy.ResolveAr")
            .And.Contain("BusinessPartnerArProfileVersionId = counterparty.Profile.Id")
            .And.Contain("BusinessPartnerCode = customer.PartnerCode")
            .And.Contain("var arAccountId = settings.ControlAccountArId")
            .And.NotContain("dto.CustomerId")
            .And.NotContain("invoice.CustomerId");
        migration.Should().Contain("IF EXISTS (SELECT 1 FROM [Invoices])")
            .And.Contain("requires a fresh Finance transactional database")
            .And.Contain("BusinessPartnerArProfileVersionId")
            .And.Contain("BusinessPartnerRoleId")
            .And.NotContain("RenameColumn(\n                name: \"CustomerId\"");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CanonicalBusinessPartner")]
    [Trait("Category", "Architecture")]
    public void TaxCalculation_ShouldUseOneCanonicalCounterpartyAndExplicitRole()
    {
        var root = FindRepositoryRoot();
        var dto = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "DTOs", "Finance", "TaxDtos.cs"));
        var request = dto[dto.IndexOf("public class TaxCalculationRequestDto", StringComparison.Ordinal)..dto.IndexOf("public class TaxCalculationResultDto", StringComparison.Ordinal)];
        var engine = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "Taxation", "TaxCalculationEngine.cs"));

        request.Should().Contain("public Guid? BusinessPartnerId")
            .And.Contain("public BusinessPartnerRoleType? BusinessPartnerRole")
            .And.NotContain("CustomerId")
            .And.NotContain("SupplierId");
        engine.Should().Contain("request.BusinessPartnerId")
            .And.Contain("request.BusinessPartnerRole")
            .And.NotContain("request.CustomerId")
            .And.NotContain("request.SupplierId");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CanonicalBusinessPartner")]
    [Trait("Category", "Architecture")]
    public void TaxReporting_ShouldUseOneCanonicalCounterpartyFilterAndNoLegacySupplierLookup()
    {
        var root = FindRepositoryRoot();
        var dto = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "DTOs", "Finance", "TaxReportDtos.cs"));
        var request = dto[dto.IndexOf("public sealed class TaxReportRequestDto", StringComparison.Ordinal)..dto.IndexOf("public sealed class GhanaTaxSnapshotReportDto", StringComparison.Ordinal)];
        var service = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "Taxation", "TaxReportingService.cs"));

        request.Should().Contain("public Guid? BusinessPartnerId")
            .And.Contain("public BusinessPartnerRoleType? BusinessPartnerRole")
            .And.NotContain("CustomerId")
            .And.NotContain("SupplierId");
        service.Should().Contain("request.BusinessPartnerId")
            .And.Contain("request.BusinessPartnerRole")
            .And.Contain("docInfo.BusinessPartnerId")
            .And.NotContain("_context.Set<Supplier>()")
            .And.NotContain("request.CustomerId")
            .And.NotContain("request.SupplierId")
            .And.NotContain("docInfo.CustomerId")
            .And.NotContain("docInfo.SupplierId");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CanonicalBusinessPartner")]
    [Trait("Category", "Architecture")]
    public void ApReporting_ShouldUseCanonicalBusinessPartnerIdentityWithoutSupplierFallbacks()
    {
        var root = FindRepositoryRoot();
        var dto = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "DTOs", "Finance", "AccountsPayableDtos.cs"));
        var reportDtos = dto[dto.IndexOf("#region AP Reports", StringComparison.Ordinal)..];
        var service = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "AP", "ApReportsService.cs"));
        var controller = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Controllers", "Finance", "ApControllersConsolidated.cs"));

        reportDtos.Should().Contain("public Guid BusinessPartnerId")
            .And.NotContain("public Guid SupplierId")
            .And.NotContain("public Guid? SupplierId");
        service.Should().Contain("Repository<BusinessPartner>()")
            .And.Contain("selection.BusinessPartnerId")
            .And.NotContain("Repository<Supplier>()")
            .And.NotContain("BusinessPartnerId ??")
            .And.NotContain("selection.SupplierId");
        controller.Should().Contain("businessPartnerIds")
            .And.Contain("businessPartnerId")
            .And.NotContain("supplier-detailed-ledger?supplierIds");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CanonicalBusinessPartner")]
    [Trait("Category", "Architecture")]
    public void ArReporting_ShouldUseCanonicalBusinessPartnerIdentityAndGovernedCustomerRoles()
    {
        var root = FindRepositoryRoot();
        var dto = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "DTOs", "AR", "ReportDtos.cs"));
        var service = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "AR", "ArReportsService.cs"));
        var controller = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Controllers", "Finance", "ArControllersConsolidated.cs"));

        dto.Should().Contain("public Guid BusinessPartnerId")
            .And.NotContain("public Guid CustomerId")
            .And.NotContain("public Guid? CustomerId");
        service.Should().Contain("Repository<BusinessPartnerRole>()")
            .And.Contain("BusinessPartnerRoleType.Customer")
            .And.Contain("customer.BusinessPartnerId")
            .And.NotContain("PartnerType == \"Customer\"")
            .And.NotContain("customer.CustomerId")
            .And.NotContain("query.CustomerId");
        controller.Should().Contain("businessPartnerIds")
            .And.Contain("customer-statement/{businessPartnerId}");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CanonicalBusinessPartner")]
    [Trait("Category", "Architecture")]
    public void AuxiliaryFinanceContracts_ShouldExposeCanonicalBusinessPartnerIdentityOnly()
    {
        var root = FindRepositoryRoot();
        var landedCost = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "DTOs", "Finance", "LandedCostInvoiceDtos.cs"));
        var purchaseOrder = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "DTOs", "Finance", "FinancePurchaseOrderDtos.cs"));
        var banking = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "DTOs", "Finance", "BankingSettlementDtos.cs"));

        landedCost.Should().Contain("BusinessPartnerId").And.NotContain("SupplierId");
        purchaseOrder.Should().Contain("BusinessPartnerId")
            .And.NotContain("public Guid VendorId")
            .And.NotContain("public Guid SupplierId");
        banking[banking.IndexOf("public class ReturnedChequeCaseDto", StringComparison.Ordinal)..]
            .Should().Contain("BusinessPartnerId").And.NotContain("public Guid CustomerId");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-CanonicalBusinessPartner")]
    [Trait("Category", "Architecture")]
    public void ArCustomerRegister_ShouldBeAReadOnlyCanonicalBusinessPartnerView()
    {
        var root = FindRepositoryRoot();
        var controller = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Controllers", "Finance", "CustomerController.cs"));
        var service = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "AR", "CustomerService.cs"));
        var searchDtos = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Core", "DTOs", "Finance", "FinanceSearchDtos.cs"));
        var listPage = File.ReadAllText(Path.Combine(root, "frontend", "src", "app", "finance", "ar", "customers", "page.tsx"));

        controller.Should().NotContain("[HttpPost]")
            .And.NotContain("[HttpPut(")
            .And.NotContain("[HttpDelete(");
        service.Should().Contain("p.Roles.Any")
            .And.Contain("BusinessPartnerRoleType.Customer")
            .And.NotContain("PartnerType == \"Customer\"")
            .And.NotContain("Task<CustomerDto> CreateAsync")
            .And.NotContain("Task<CustomerDto> UpdateAsync")
            .And.NotContain("Task DeleteAsync");
        searchDtos.Should().Contain("public Guid? BusinessPartnerId")
            .And.NotContain("public Guid? CustomerId");
        listPage.Should().Contain("/procurement/business-partners/new")
            .And.Contain("/procurement/business-partners/${customer.id}/edit")
            .And.NotContain("/finance/ar/customers/new");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be located.");
    }

    private static string Normalize(string path)
        => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
