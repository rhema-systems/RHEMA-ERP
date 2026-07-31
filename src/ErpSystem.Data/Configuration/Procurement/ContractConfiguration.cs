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
