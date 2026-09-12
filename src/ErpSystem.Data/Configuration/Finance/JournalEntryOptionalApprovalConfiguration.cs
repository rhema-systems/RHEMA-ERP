using ErpSystem.Core.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Finance;

public sealed class JournalEntryOptionalApprovalConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder) =>
        builder.ToTable("JournalEntries", table => table.HasTrigger("TR_JournalEntries_OptionalApprovalSubmission"));
}
