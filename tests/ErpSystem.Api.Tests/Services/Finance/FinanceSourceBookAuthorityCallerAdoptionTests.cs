using ErpSystem.Core.Entities.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceSourceBookAuthorityCallerAdoptionTests
{
    [Fact]
    public void ArAndCashPostingCallers_UseFrozenExactAuthority_NotIfrsFallbacks()
    {
        var invoice = Read("src/ErpSystem.Api/Services/Finance/AR/InvoiceService.cs");
        var payment = Read("src/ErpSystem.Api/Services/Finance/AR/PaymentService.cs");
        var cash = Read("src/ErpSystem.Api/Services/Finance/Cash/CashTransactionService.cs");

        invoice.Should().NotContain("AccountingBookCode = \"IFRS\"")
            .And.Contain("FreezeInitialPrimaryAsync")
            .And.Contain("FreezeResubmissionAsync")
            .And.Contain("RequireInvoicePostingAuthorityAsync")
            .And.Contain("BindOriginalPostingAsync");
        payment.Should().NotContain("AccountingBookCode = \"IFRS\"")
            .And.Contain("FreezeInheritedAsync")
            .And.Contain("FreezeInitialPrimaryAsync")
            .And.Contain("RetainExistingPostedOriginalAsync")
            .And.Contain("SOURCE_BOOK_AUTHORITY_MIXED_ORIGINS")
            .And.Contain("BindOriginalPostingAsync");
        cash.Should().NotContain("AccountingBookCode = \"IFRS\"")
            .And.Contain("IsolationLevel.Serializable")
            .And.Contain("SourceWorkflowEntityType = CashTransactionWorkflowEntityType")
            .And.Contain("FreezeResubmissionAsync")
            .And.Contain("RequireCashPostingAuthorityAsync")
            .And.Contain("BindOriginalPostingAsync");
    }

    [Fact]
    public void Model_UsesTenantCompositeAuthorityLinks_ForArAndCashCallers()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=SourceBookCallerModel;Trusted_Connection=True")
            .Options);
        AssertAuthorityLink(db, typeof(Invoice));
        AssertAuthorityLink(db, typeof(CustomerPayment));
        AssertAuthorityLink(db, typeof(CashTransaction));
    }

    private static void AssertAuthorityLink(ApplicationDbContext db, Type callerType)
    {
        var entity = db.Model.FindEntityType(callerType);
        entity.Should().NotBeNull();
        entity!.GetIndexes().Should().Contain(index => index.Properties.Select(item => item.Name)
            .SequenceEqual(new[] { "TenantId", "SourceBookAuthorityId" }));
        entity.GetForeignKeys().Should().Contain(foreignKey =>
            foreignKey.Properties.Select(item => item.Name).SequenceEqual(new[] { "TenantId", "SourceBookAuthorityId" })
            && foreignKey.PrincipalEntityType.ClrType == typeof(FinanceSourceBookAuthority)
            && foreignKey.PrincipalKey.Properties.Select(item => item.Name).SequenceEqual(new[] { "TenantId", "Id" }));
    }

    private static string Read(string relative)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        }
        throw new FileNotFoundException(relative);
    }
}
