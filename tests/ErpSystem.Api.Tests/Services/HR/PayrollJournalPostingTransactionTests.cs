using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.HR;

public sealed class PayrollJournalPostingTransactionTests
{
    [Fact]
    [Trait("Category", "PayrollPosting")]
    public void Posting_ShouldFlushRemovedPreviewLinesBeforeFinancePersistsRegeneratedLines()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Api", "Services", "HR", "PayrollService.cs"));
        var methodStart = source.IndexOf(
            "public async Task<PayrollJournalPostingDto> PostPayrollJournalAsync(",
            StringComparison.Ordinal);
        var methodEnd = source.IndexOf(
            "private async Task<PayrollParameterSet?> GetActivePayrollParametersAsync(",
            methodStart,
            StringComparison.Ordinal);

        methodStart.Should().BeGreaterThanOrEqualTo(0);
        methodEnd.Should().BeGreaterThan(methodStart);

        var method = source[methodStart..methodEnd];
        var transactionStart = method.IndexOf(
            "BeginTransactionAsync(cancellationToken)", StringComparison.Ordinal);
        var removeExisting = method.IndexOf(
            "_context.PayrollJournalLines.RemoveRange(journalLines)", StringComparison.Ordinal);
        var flushRemoval = method.IndexOf(
            "await _context.SaveChangesAsync(cancellationToken)",
            removeExisting,
            StringComparison.Ordinal);
        var addRegenerated = method.IndexOf(
            "_context.PayrollJournalLines.AddRange(journalLines)",
            removeExisting,
            StringComparison.Ordinal);
        var financePost = method.IndexOf(
            "_financePostingEngine.PostAsync", StringComparison.Ordinal);

        transactionStart.Should().BeGreaterThanOrEqualTo(0);
        transactionStart.Should().BeLessThan(removeExisting,
            "the preview replacement must remain inside the retryable SQL transaction");
        removeExisting.Should().BeLessThan(flushRemoval,
            "existing unique sequence keys must be deleted in SQL before regenerated rows are tracked");
        flushRemoval.Should().BeLessThan(addRegenerated);
        addRegenerated.Should().BeLessThan(financePost,
            "Finance persists the shared DbContext and must only see conflict-free payroll rows");
    }

    [Fact]
    [Trait("Category", "PayrollPosting")]
    public void Posting_ShouldUseTheSystemPostingEngineWithStablePayrollIdentity()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Api", "Services", "HR", "PayrollService.cs"));
        var methodStart = source.IndexOf(
            "public async Task<PayrollJournalPostingDto> PostPayrollJournalAsync(",
            StringComparison.Ordinal);
        var methodEnd = source.IndexOf(
            "private async Task<PayrollParameterSet?> GetActivePayrollParametersAsync(",
            methodStart,
            StringComparison.Ordinal);

        methodStart.Should().BeGreaterThanOrEqualTo(0);
        methodEnd.Should().BeGreaterThan(methodStart);

        var method = source[methodStart..methodEnd];
        method.Should().Contain("SourceDocumentType = \"PayrollRun\"");
        method.Should().Contain("SourceDocumentId = run.Id");
        method.Should().Contain("SourceDocumentTenantId = tenantId");
        method.Should().Contain("IdempotencyKey = $");
        method.Should().Contain("FinanceExternalProducerContractId.HrPayrollJournal");
        method.Should().Contain("_financePostingEngine.PostAsync(postingRequest, payrollProducer, cancellationToken)");
        method.Should().NotContain("_journalEntryService.CreateJournalEntryAsync");
        method.Should().NotContain("_journalEntryService.PostJournalEntryAsync");
    }

    [Fact]
    [Trait("Category", "PayrollPosting")]
    public void PayrollJournalLineSequenceIndex_ShouldIgnoreSoftDeletedPreviewRows()
    {
        var root = FindRepositoryRoot();
        var model = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Data", "ApplicationDbContext.cs"));
        var migration = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ErpSystem.Data",
            "LegacyMigrationsArchive",
            "20260903235000_FilterPayrollJournalLineSequenceIndex.cs"));

        model.Should().Contain(
            "entity.HasIndex(e => new { e.TenantId, e.PayrollRunId, e.SequenceNo })");
        model.Should().Contain(".HasFilter(\"[IsDeleted] = 0\")");
        migration.Should().Contain(
            "\"IX_PayrollJournalLines_TenantId_PayrollRunId_SequenceNo\"");
        migration.Should().Contain("name: IndexName");
        migration.Should().Contain("filter: \"[IsDeleted] = 0\"");
    }

    [Fact]
    [Trait("Category", "PayrollPosting")]
    public void PayrollController_ShouldReturnCodedProblemDetailsAndPersistHandledExceptionContext()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root, "src", "ErpSystem.Api", "Controllers", "HR", "PayrollController.cs"));

        source.Should().Contain("PAYROLL_INVALID_OPERATION");
        source.Should().Contain("PAYROLL_UNEXPECTED");
        source.Should().Contain("SystemExceptionResultLoggingFilter.HandledExceptionItemKey] = ex");
        source.Should().Contain("problem.Extensions[\"code\"] = code");
        source.Should().Contain("problem.Extensions[\"correlationId\"] = HttpContext.TraceIdentifier");
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
}
