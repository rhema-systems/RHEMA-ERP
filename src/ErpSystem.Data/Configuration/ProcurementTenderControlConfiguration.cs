using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class ProcurementTenderControlConfiguration : IEntityTypeConfiguration<ProcurementTenderControl>
{
    public void Configure(EntityTypeBuilder<ProcurementTenderControl> builder)
    {
        builder.ToTable("ProcurementTenderControls", table =>
        {
            table.HasTrigger("TR_ProcurementTenderControls_Lifecycle");
            table.HasTrigger("TR_ProcurementTenderControls_ApprovalPolicy");
        });
        builder.Property(item => item.ApprovalRequired).HasDefaultValue(true);
        builder.HasIndex(item => new { item.TenantId, item.TenderId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.SourcingCaseId });
        builder.HasOne(item => item.Tender).WithMany().HasForeignKey(item => item.TenderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.SourcingCase).WithMany().HasForeignKey(item => item.SourcingCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.MethodRule).WithMany().HasForeignKey(item => item.MethodRuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.AuthorityRoute).WithMany().HasForeignKey(item => item.AuthorityRouteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowDefinition).WithMany().HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowInstance).WithMany().HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementTenderDocumentIssueConfiguration : IEntityTypeConfiguration<ProcurementTenderDocumentIssue>
{
    public void Configure(EntityTypeBuilder<ProcurementTenderDocumentIssue> builder)
    {
        builder.ToTable("ProcurementTenderDocumentIssues", table => table.HasTrigger("TR_ProcurementTenderDocumentIssues_Immutable"));
        builder.HasIndex(item => new { item.TenantId, item.TenderControlId, item.IssueReceiptNumber }).IsUnique();
        builder.HasOne(item => item.TenderControl).WithMany(item => item.DocumentIssues).HasForeignKey(item => item.TenderControlId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.BusinessPartner).WithMany().HasForeignKey(item => item.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementTenderSubmissionReceiptConfiguration : IEntityTypeConfiguration<ProcurementTenderSubmissionReceipt>
{
    public void Configure(EntityTypeBuilder<ProcurementTenderSubmissionReceipt> builder)
    {
        builder.ToTable("ProcurementTenderSubmissionReceipts", table => table.HasTrigger("TR_ProcurementTenderSubmissionReceipts_Immutable"));
        builder.HasIndex(item => new { item.TenantId, item.TenderControlId, item.TenderBidId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.ReceiptNumber }).IsUnique();
        builder.HasOne(item => item.TenderControl).WithMany(item => item.SubmissionReceipts).HasForeignKey(item => item.TenderControlId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.TenderBid).WithMany().HasForeignKey(item => item.TenderBidId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.BusinessPartner).WithMany().HasForeignKey(item => item.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
    }
}
