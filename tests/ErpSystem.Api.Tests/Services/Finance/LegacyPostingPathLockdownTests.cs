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
            Path.Combine(root, "src", "ErpSystem.Core", "Services", "Sales", "ReturnOrderService.cs"),
            Path.Combine(root, "src", "ErpSystem.Api", "Controllers", "Finance", "FinancePurchaseOrderController.cs"),
            Path.Combine(root, "src", "ErpSystem.Api", "Controllers", "Finance", "SupplierReturnsController.cs")
        };

        foreach (var path in expectedPostingEngineFiles)
        {
            File.ReadAllText(path).Should().Contain("PostAsync", $"posting-capable file {Path.GetRelativePath(root, path)} should call IFinancePostingEngine");
        }
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
