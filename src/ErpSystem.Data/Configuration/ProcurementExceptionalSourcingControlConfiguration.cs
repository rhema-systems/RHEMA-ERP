using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration;

public sealed class ProcurementExceptionalSourcingControlConfiguration : IEntityTypeConfiguration<ProcurementExceptionalSourcingControl>
{
    public void Configure(EntityTypeBuilder<ProcurementExceptionalSourcingControl> builder)
    {
        builder.ToTable("ProcurementExceptionalSourcingControls",
            table => table.HasTrigger("TR_ProcurementExceptionalSourcingControls_Lifecycle"));
        builder.HasIndex(item => new { item.TenantId, item.TenderId }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.SourcingCaseId });
        builder.HasIndex(item => new { item.TenantId, item.ExceptionRuleId });
        builder.HasIndex(item => new { item.TenantId, item.WorkflowInstanceId });
        builder.HasIndex(item => new { item.TenantId, item.NegotiationId });
        builder.HasOne(item => item.Tender).WithMany().HasForeignKey(item => item.TenderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.SourcingCase).WithMany().HasForeignKey(item => item.SourcingCaseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.MethodRule).WithMany().HasForeignKey(item => item.MethodRuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ExceptionRule).WithMany().HasForeignKey(item => item.ExceptionRuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.AuthorityRoute).WithMany().HasForeignKey(item => item.AuthorityRouteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowDefinition).WithMany().HasForeignKey(item => item.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.WorkflowInstance).WithMany().HasForeignKey(item => item.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Negotiation).WithMany().HasForeignKey(item => item.NegotiationId).OnDelete(DeleteBehavior.Restrict);
    }
}
