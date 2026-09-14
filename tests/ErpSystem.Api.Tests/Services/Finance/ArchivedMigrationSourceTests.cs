using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class ArchivedMigrationSourceTests
{
    [Fact]
    public void Read_RequiresAFileNameAndCannotTraverseOutsideTheArchive()
    {
        var action = () => ArchivedMigrationSource.Read("../ApplicationDbContext.cs");

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SqlBlocks_ExtractsRawAndVerbatimLiteralsWithoutCompilingArchivedTypes()
    {
        ArchivedMigrationSource.SqlBlocks(
                "20260818130000_HardenSupplierDebitNoteLineageIdentityAndSettlement.cs")
            .Should().HaveCount(2)
            .And.OnlyContain(sql => !string.IsNullOrWhiteSpace(sql));

        ArchivedMigrationSource.SqlContaining(
                "20260905182403_AddBookAwareBalanceFoundation.cs",
                "C2_BOOK_AUTHORITY_PREFLIGHT")
            .Should().Contain("THROW 51000");
    }

    [Fact]
    public void Read_RejectsAnAbsentArchiveFile()
    {
        var action = () => ArchivedMigrationSource.Read("20990101000000_DoesNotExist.cs");

        action.Should().Throw<FileNotFoundException>();
    }
}
