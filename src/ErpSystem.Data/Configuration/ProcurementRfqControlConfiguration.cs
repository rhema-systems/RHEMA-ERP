using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class ProcurementRfqReceiptConfiguration : IEntityTypeConfiguration<ProcurementRfqReceipt>
{
    public void Configure(EntityTypeBuilder<ProcurementRfqReceipt> builder)
    {
        builder.ToTable("ProcurementRfqReceipts", table => table.HasTrigger("TR_ProcurementRfqReceipts_Immutable"));
        builder.HasIndex(item => new { item.TenantId, item.RfqId, item.ReceiptSequence }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.RfqId, item.QuoteId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.ReceiptNumber }).IsUnique();
        builder.HasOne(item => item.Rfq).WithMany(item => item.Receipts).HasForeignKey(item => item.RfqId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Quote).WithMany().HasForeignKey(item => item.QuoteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.BusinessPartner).WithMany().HasForeignKey(item => item.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.OpeningRegister).WithMany(item => item.Receipts).HasForeignKey(item => item.OpeningRegisterId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementRfqOpeningRegisterConfiguration : IEntityTypeConfiguration<ProcurementRfqOpeningRegister>
{
    public void Configure(EntityTypeBuilder<ProcurementRfqOpeningRegister> builder)
    {
        builder.ToTable("ProcurementRfqOpeningRegisters", table => table.HasTrigger("TR_ProcurementRfqOpeningRegisters_Immutable"));
        builder.HasIndex(item => new { item.TenantId, item.RfqId }).IsUnique();
        builder.HasOne(item => item.Rfq).WithOne(item => item.OpeningRegister).HasForeignKey<ProcurementRfqOpeningRegister>(item => item.RfqId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementRfqOpeningParticipantConfiguration : IEntityTypeConfiguration<ProcurementRfqOpeningParticipant>
{
    public void Configure(EntityTypeBuilder<ProcurementRfqOpeningParticipant> builder)
    {
        builder.ToTable("ProcurementRfqOpeningParticipants", table => table.HasTrigger("TR_ProcurementRfqOpeningParticipants_Immutable"));
        builder.HasIndex(item => new { item.TenantId, item.OpeningRegisterId, item.ParticipantName, item.RoleName }).IsUnique();
        builder.HasOne(item => item.OpeningRegister).WithMany(item => item.Participants).HasForeignKey(item => item.OpeningRegisterId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementRfqOpeningEntryConfiguration : IEntityTypeConfiguration<ProcurementRfqOpeningEntry>
{
    public void Configure(EntityTypeBuilder<ProcurementRfqOpeningEntry> builder)
    {
        builder.ToTable("ProcurementRfqOpeningEntries", table => table.HasTrigger("TR_ProcurementRfqOpeningEntries_Immutable"));
        builder.HasIndex(item => new { item.TenantId, item.OpeningRegisterId, item.ReceiptId }).IsUnique();
        builder.HasOne(item => item.OpeningRegister).WithMany(item => item.Entries).HasForeignKey(item => item.OpeningRegisterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Receipt).WithMany().HasForeignKey(item => item.ReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Quote).WithMany().HasForeignKey(item => item.QuoteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.BusinessPartner).WithMany().HasForeignKey(item => item.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementRfqEvaluationConfiguration : IEntityTypeConfiguration<ProcurementRfqEvaluation>
{
    public void Configure(EntityTypeBuilder<ProcurementRfqEvaluation> builder)
    {
        builder.ToTable("ProcurementRfqEvaluations", table =>
        {
            table.HasTrigger("TR_ProcurementRfqEvaluations_Lifecycle");
            table.HasTrigger("TR_ProcurementRfqEvaluations_ApprovalPolicy");
        });
        builder.Property(item => item.ApprovalRequired).HasDefaultValue(true);
        builder.HasIndex(item => new { item.TenantId, item.RfqId }).IsUnique();
        builder.HasOne(item => item.Rfq).WithOne(item => item.Evaluation).HasForeignKey<ProcurementRfqEvaluation>(item => item.RfqId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.OpeningRegister).WithMany().HasForeignKey(item => item.OpeningRegisterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.MethodRule).WithMany().HasForeignKey(item => item.MethodRuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowInstance).WithMany().HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementRfqEvaluationLineConfiguration : IEntityTypeConfiguration<ProcurementRfqEvaluationLine>
{
    public void Configure(EntityTypeBuilder<ProcurementRfqEvaluationLine> builder)
    {
        builder.ToTable("ProcurementRfqEvaluationLines", table => table.HasTrigger("TR_ProcurementRfqEvaluationLines_Lifecycle"));
        builder.HasIndex(item => new { item.TenantId, item.EvaluationId, item.RfqItemId }).IsUnique();
        builder.HasOne(item => item.Evaluation).WithMany(item => item.Lines).HasForeignKey(item => item.EvaluationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.RfqItem).WithMany().HasForeignKey(item => item.RfqItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Quote).WithMany().HasForeignKey(item => item.QuoteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.BusinessPartner).WithMany().HasForeignKey(item => item.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
    }
}
