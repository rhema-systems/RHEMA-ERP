using ErpSystem.Core.Entities.Ehc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Ehc;

public sealed class EhcPropertyEnquiryProspectConfiguration : IEntityTypeConfiguration<EhcPropertyEnquiryProspect>
{
    public void Configure(EntityTypeBuilder<EhcPropertyEnquiryProspect> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.TicketId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.LeadId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.OpportunityId }).IsUnique().HasFilter("[OpportunityId] IS NOT NULL");
        builder.HasOne(x => x.Ticket).WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Lead).WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Opportunity).WithMany().HasForeignKey(x => x.OpportunityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SalesAllocation).WithMany().HasForeignKey(x => x.SalesAllocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BusinessPartner).WithMany().HasForeignKey(x => x.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("CK_EhcPropertyEnquiryProspects_DepositRequirement", "[DepositRequirementType] IN ('Fixed','Percentage','Full')"));
    }
}

public sealed class EhcPropertyProspectDepositPolicyConfiguration : IEntityTypeConfiguration<EhcPropertyProspectDepositPolicy>
{
    public void Configure(EntityTypeBuilder<EhcPropertyProspectDepositPolicy> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.SalesSaleableSourceId }).IsUnique();
        builder.HasOne(x => x.SalesSaleableSource).WithMany().HasForeignKey(x => x.SalesSaleableSourceId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("CK_EhcPropertyProspectDepositPolicies_Requirement", "[RequirementType] IN ('Fixed','Percentage','Full')"));
        builder.ToTable(t => t.HasCheckConstraint("CK_EhcPropertyProspectDepositPolicies_Value", "([RequirementType] = 'Fixed' AND [FixedAmount] > 0) OR ([RequirementType] = 'Percentage' AND [Percentage] > 0 AND [Percentage] <= 100) OR [RequirementType] = 'Full'"));
    }
}

public sealed class ProspectDepositReceiptConfiguration : IEntityTypeConfiguration<ProspectDepositReceipt>
{
    public void Configure(EntityTypeBuilder<ProspectDepositReceipt> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.ReceiptNumber }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.TransactionReference }).IsUnique().HasFilter("[TransactionReference] IS NOT NULL");
        builder.HasIndex(x => new { x.TenantId, x.ProspectId, x.Status });
        builder.HasOne(x => x.Prospect).WithMany(x => x.DepositReceipts).HasForeignKey(x => x.ProspectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EhcTicket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ErpSystem.Core.Entities.Sales.Lead>().WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ErpSystem.Core.Entities.Sales.Opportunity>().WithMany().HasForeignKey(x => x.OpportunityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ErpSystem.Core.Entities.Procurement.BusinessPartner>().WithMany().HasForeignKey(x => x.BusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("CK_EhcProspectDepositReceipts_Amount", "[Amount] > 0"));
        builder.ToTable(t => t.HasCheckConstraint("CK_EhcProspectDepositReceipts_Status", "[Status] IN ('Pending','Cleared','Reversed')"));
        builder.ToTable(t => t.HasCheckConstraint("CK_EhcProspectDepositReceipts_CashSource", "([BankAccountId] IS NOT NULL AND [LiquidityAccountId] IS NULL) OR ([BankAccountId] IS NULL AND [LiquidityAccountId] IS NOT NULL)"));
    }
}

public sealed class EhcPropertyEnquiryEmailAttemptConfiguration : IEntityTypeConfiguration<EhcPropertyEnquiryEmailAttempt>
{
    public void Configure(EntityTypeBuilder<EhcPropertyEnquiryEmailAttempt> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.TicketId, x.AttemptedAt });
        builder.HasOne(x => x.Ticket).WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Restrict);
    }
}
