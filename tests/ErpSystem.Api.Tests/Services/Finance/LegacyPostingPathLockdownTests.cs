using FluentAssertions;
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
            "// Check for seed command",
            migrationStart,
            StringComparison.Ordinal);
        migrationStart.Should().BeGreaterThanOrEqualTo(0);
        seedStart.Should().BeGreaterThan(migrationStart);

        var migrationCommand = program[migrationStart..seedStart];
        migrationCommand.Should().Contain("WebApplication.CreateBuilder(args)");
        migrationCommand.Should().Contain("tempBuilder.Services.AddHttpContextAccessor()")
            .And.Contain("tempBuilder.Services.AddErpSystemDatabase(tempBuilder.Configuration)");
        migrationCommand.IndexOf("AddHttpContextAccessor", StringComparison.Ordinal).Should().BeLessThan(
            migrationCommand.IndexOf("AddErpSystemDatabase", StringComparison.Ordinal),
            "the audited DbContext dependency must be registered before host validation");
        migrationCommand.Should().NotContain("CreateSeedBuilder(args)");
        migrationCommand.Should().NotContain("Environment.SetEnvironmentVariable");
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
    public void BankOpeningBalancePosting_ShouldRemainDisabledUntilPostingEngineMigration()
    {
        var root = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(root, "src", "ErpSystem.Api", "Services", "Finance", "Cash", "BankAccountService.cs"));

        service.Should().Contain("FIN-LIM-0006 opening-balance migration batch");
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
