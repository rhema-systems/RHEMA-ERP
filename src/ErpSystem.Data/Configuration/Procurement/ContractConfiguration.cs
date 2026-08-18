using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Procurement;

public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("Contracts", table =>
        {
            table.HasTrigger("TR_Contracts_TDC0407ActivationGuard");
            table.HasTrigger("TR_Contracts_TDC0409CloseoutGuard");
            table.HasTrigger("TR_Contracts_QS0520CommercialTerms");
            table.HasCheckConstraint("CK_Contracts_QS0520_Amounts",
                "[ProvisionalSumAmount] >= 0 AND [ContingencyAmount] >= 0 AND [ProvisionalSumAmount] + [ContingencyAmount] <= [ContractValue] AND [RetentionPercentage] >= 0 AND [RetentionPercentage] <= 100 AND ([DefectsLiabilityDays] IS NULL OR [DefectsLiabilityDays] BETWEEN 0 AND 3650) AND ([ClaimNoticePeriodDays] IS NULL OR [ClaimNoticePeriodDays] BETWEEN 0 AND 3650)");
            table.HasCheckConstraint("CK_Contracts_QS0520_Lineage",
                "([CommercialTermsPolicyHash] IS NULL AND [CommercialTermsConfigurationProfileId] IS NULL AND [ContractControlsDecisionId] IS NULL AND [RetentionDecisionId] IS NULL AND [CommercialTermsClientRequestId] IS NULL AND [CommercialTermsRequestHash] IS NULL AND [CommercialTermsConfiguredAt] IS NULL AND [CommercialTermsConfiguredById] IS NULL) OR ([CommercialTermsPolicyHash] IS NOT NULL AND LEN([CommercialTermsPolicyHash]) = 64 AND [CommercialTermsConfigurationProfileId] IS NOT NULL AND [ContractControlsDecisionId] IS NOT NULL AND [RetentionDecisionId] IS NOT NULL AND [CommercialTermsClientRequestId] IS NOT NULL AND LEN([CommercialTermsRequestHash]) = 64 AND [CommercialTermsConfiguredAt] IS NOT NULL AND [CommercialTermsConfiguredById] IS NOT NULL AND [PaymentTermId] IS NOT NULL)");
            table.HasCheckConstraint("CK_Contracts_QS0520_Clauses",
                "([RetentionPercentage] = 0 OR NULLIF(LTRIM(RTRIM([RetentionClause])), '') IS NOT NULL) AND ([AllowSectionalTakeover] = 0 OR NULLIF(LTRIM(RTRIM([SectionalTakeoverClause])), '') IS NOT NULL) AND ([AllowSubcontracting] = 0 OR ([SubcontractPaymentTermId] IS NOT NULL AND NULLIF(LTRIM(RTRIM([SubcontractTerms])), '') IS NOT NULL))");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ContractNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.ContractTitle)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.ContractType)
            .HasMaxLength(50);

        builder.Property(c => c.Status)
            .HasMaxLength(50);

        builder.Property(c => c.RowVersion)
            .IsRowVersion();

        builder.Property(c => c.ContractValue)
            .HasPrecision(18, 2);

        builder.Property(c => c.Currency)
            .HasMaxLength(3)
            .HasDefaultValue("USD");

        builder.Property(c => c.PaymentTerms)
            .HasMaxLength(200);

        builder.Property(c => c.RetentionPercentage)
            .HasPrecision(5, 2);

        builder.Property(c => c.ProvisionalSumAmount)
            .HasPrecision(18, 2);

        builder.Property(c => c.ContingencyAmount)
            .HasPrecision(18, 2);

        builder.Property(c => c.RetentionClause)
            .HasMaxLength(2000);

        builder.Property(c => c.SectionalTakeoverClause)
            .HasMaxLength(2000);

        builder.Property(c => c.SubcontractTerms)
            .HasMaxLength(2000);

        builder.Property(c => c.ClaimClause)
            .HasMaxLength(2000);

        builder.Property(c => c.CommercialTermsRequestHash)
            .HasMaxLength(64);

        builder.Property(c => c.CommercialTermsPolicyHash)
            .HasMaxLength(64);

        builder.Property(c => c.ScopeOfWork);

        builder.Property(c => c.Deliverables);

        builder.Property(c => c.SpecialConditions);

        builder.Property(c => c.PenaltyClause);

        builder.Property(c => c.SignedByName)
            .HasMaxLength(200);

        builder.Property(c => c.ContractorSignatoryName)
            .HasMaxLength(200);

        builder.Property(c => c.ContractDocumentPath)
            .HasMaxLength(500);

        builder.Property(c => c.Notes);

        builder.Property(c => c.TerminationReason)
            .HasMaxLength(500);

        // Indexes
        builder.HasIndex(c => c.ContractNumber);
        builder.HasIndex(c => c.TenantId);
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.BusinessPartnerId);
        builder.HasIndex(c => c.TenderAwardId);
        builder.HasIndex(c => c.PaymentTermId);
        builder.HasIndex(c => c.SubcontractPaymentTermId);
        builder.HasIndex(c => c.CommercialTermsContractDocumentId);
        builder.HasIndex(c => c.CommercialTermsConfigurationProfileId);
        builder.HasIndex(c => c.ContractControlsDecisionId);
        builder.HasIndex(c => c.RetentionDecisionId);
        builder.HasIndex(c => new { c.TenantId, c.CommercialTermsClientRequestId })
            .IsUnique()
            .HasFilter("[CommercialTermsClientRequestId] IS NOT NULL AND [IsDeleted] = 0");

        // Relationships
        builder.HasOne(c => c.TenderAward)
            .WithMany()
            .HasForeignKey(c => c.TenderAwardId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.BusinessPartner)
            .WithMany()
            .HasForeignKey(c => c.BusinessPartnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Tender)
            .WithMany()
            .HasForeignKey(c => c.TenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.PaymentTerm)
            .WithMany()
            .HasForeignKey(c => c.PaymentTermId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.SubcontractPaymentTerm)
            .WithMany()
            .HasForeignKey(c => c.SubcontractPaymentTermId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.CommercialTermsContractDocument)
            .WithMany()
            .HasForeignKey(c => c.CommercialTermsContractDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.CommercialTermsConfigurationProfile)
            .WithMany()
            .HasForeignKey(c => c.CommercialTermsConfigurationProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ContractControlsDecision)
            .WithMany()
            .HasForeignKey(c => c.ContractControlsDecisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.RetentionDecision)
            .WithMany()
            .HasForeignKey(c => c.RetentionDecisionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.CommercialTermsConfiguredBy)
            .WithMany()
            .HasForeignKey(c => c.CommercialTermsConfiguredById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Milestones)
            .WithOne(m => m.Contract)
            .HasForeignKey(m => m.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Amendments)
            .WithOne(a => a.Contract)
            .HasForeignKey(a => a.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Documents)
            .WithOne(d => d.Contract)
            .HasForeignKey(d => d.ContractId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ContractMilestoneConfiguration : IEntityTypeConfiguration<ContractMilestone>
{
    public void Configure(EntityTypeBuilder<ContractMilestone> builder)
    {
        builder.ToTable("ContractMilestones");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.MilestoneName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.Description);

        builder.Property(m => m.PaymentPercentage)
            .HasPrecision(5, 2);

        builder.Property(m => m.PaymentAmount)
            .HasPrecision(18, 2);

        builder.Property(m => m.Status)
            .HasMaxLength(50);

        builder.Property(m => m.InvoiceNumber)
            .HasMaxLength(50);

        builder.Property(m => m.Notes);

        // Indexes
        builder.HasIndex(m => m.ContractId);
        builder.HasIndex(m => m.TenantId);
        builder.HasIndex(m => m.Status);
        builder.HasIndex(m => m.PlannedDate);
    }
}

public class ContractAmendmentConfiguration : IEntityTypeConfiguration<ContractAmendment>
{
    public void Configure(EntityTypeBuilder<ContractAmendment> builder)
    {
        builder.ToTable("ContractAmendments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.AmendmentNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.AmendmentType)
            .HasMaxLength(100);

        builder.Property(a => a.Reason)
            .HasMaxLength(500);

        builder.Property(a => a.Description);

        builder.Property(a => a.PreviousValue)
            .HasPrecision(18, 2);

        builder.Property(a => a.NewValue)
            .HasPrecision(18, 2);

        builder.Property(a => a.ValueChange)
            .HasPrecision(18, 2);

        builder.Property(a => a.ScopeChanges);

        builder.Property(a => a.Status)
            .HasMaxLength(50);

        builder.Property(a => a.ApprovalNotes)
            .HasMaxLength(500);

        builder.Property(a => a.DocumentPath)
            .HasMaxLength(500);

        builder.Property(a => a.Notes);

        // Indexes
        builder.HasIndex(a => a.ContractId);
        builder.HasIndex(a => a.TenantId);
        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.AmendmentNumber);
    }
}

public class ContractDocumentConfiguration : IEntityTypeConfiguration<ContractDocument>
{
    public void Configure(EntityTypeBuilder<ContractDocument> builder)
    {
        builder.ToTable("ContractDocuments", table =>
            table.HasTrigger("TR_ContractDocuments_TDC0407DmsRequired"));

        builder.HasKey(d => d.Id);

        builder.Property(d => d.DocumentType)
            .HasMaxLength(100);

        builder.Property(d => d.FileName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.FilePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.ContentType)
            .HasMaxLength(50);

        builder.Property(d => d.Description);

        // Indexes
        builder.HasIndex(d => d.ContractId);
        builder.HasIndex(d => d.TenantId);
        builder.HasIndex(d => d.DocumentType);
        builder.HasIndex(d => new { d.TenantId, d.CentralDocumentRecordId });

        builder.HasOne(d => d.FileUploadRecord)
            .WithMany()
            .HasForeignKey(d => d.FileUploadRecordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.CentralDocumentRecord)
            .WithMany()
            .HasForeignKey(d => d.CentralDocumentRecordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.CentralDocumentVersion)
            .WithMany()
            .HasForeignKey(d => d.CentralDocumentVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
