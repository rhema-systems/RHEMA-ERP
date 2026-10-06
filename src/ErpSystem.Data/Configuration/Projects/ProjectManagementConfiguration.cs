using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ErpSystem.Data.Configuration.Projects;

public sealed class ProjectTypeConfiguration : IEntityTypeConfiguration<ProjectType>
{
    public void Configure(EntityTypeBuilder<ProjectType> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Name });
    }
}

public sealed class ProjectPriorityConfiguration : IEntityTypeConfiguration<ProjectPriority>
{
    public void Configure(EntityTypeBuilder<ProjectPriority> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.SortOrder });
    }
}

public sealed class ProjectTemplateConfiguration : IEntityTypeConfiguration<ProjectTemplate>
{
    public void Configure(EntityTypeBuilder<ProjectTemplate> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ProjectTypeId, x.Name });
    }
}

public sealed class ProjectPhaseTemplateConfiguration : IEntityTypeConfiguration<ProjectPhaseTemplate>
{
    public void Configure(EntityTypeBuilder<ProjectPhaseTemplate> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.ProjectTypeId, x.Code });
        builder.HasIndex(x => new { x.TenantId, x.ProjectTypeId, x.SortOrder });
        builder.HasIndex(x => new { x.TenantId, x.ParentPhaseTemplateId, x.SortOrder });

        builder.HasOne(x => x.ProjectType)
            .WithMany()
            .HasForeignKey(x => x.ProjectTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ParentPhaseTemplate)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentPhaseTemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectStageGateRuleConfiguration : IEntityTypeConfiguration<ProjectStageGateRule>
{
    public void Configure(EntityTypeBuilder<ProjectStageGateRule> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.ProjectPhaseTemplateId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ProjectPhaseTemplateId, x.SortOrder });

        builder.HasOne(x => x.ProjectPhaseTemplate)
            .WithMany(x => x.StageGateRules)
            .HasForeignKey(x => x.ProjectPhaseTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ProjectPortfolioConfiguration : IEntityTypeConfiguration<ProjectPortfolio>
{
    public void Configure(EntityTypeBuilder<ProjectPortfolio> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Name });
    }
}

public sealed class ProjectProgramConfiguration : IEntityTypeConfiguration<ProjectProgram>
{
    public void Configure(EntityTypeBuilder<ProjectProgram> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.PortfolioId, x.Name });

        builder.HasOne(x => x.Portfolio)
            .WithMany(x => x.Programs)
            .HasForeignKey(x => x.PortfolioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectManagementSettingsConfiguration : IEntityTypeConfiguration<ProjectManagementSettings>
{
    public void Configure(EntityTypeBuilder<ProjectManagementSettings> builder)
    {
        builder.HasIndex(x => x.TenantId).IsUnique();
    }
}

public sealed class ProjectCatalogEntryConfiguration : IEntityTypeConfiguration<ProjectCatalogEntry>
{
    public void Configure(EntityTypeBuilder<ProjectCatalogEntry> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.CatalogType, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.CatalogType, x.SortOrder });
        builder.HasIndex(x => new { x.TenantId, x.CatalogType, x.IsActive });
        builder.HasIndex(x => new { x.TenantId, x.CatalogType, x.EffectiveFrom, x.EffectiveTo });
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_ProjectCatalogEntries_EffectivePeriod",
            "[EffectiveTo] IS NULL OR [EffectiveFrom] IS NULL OR [EffectiveTo] >= [EffectiveFrom]"));
    }
}

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.ProjectCode }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.ProjectTypeId });
        builder.HasIndex(x => new { x.TenantId, x.ExternalPortalAccessEnabled, x.BusinessPartnerId });
        builder.Property(x => x.BaseCurrencyCode).HasMaxLength(10);

        builder.HasOne(x => x.ProjectType)
            .WithMany(x => x.Projects)
            .HasForeignKey(x => x.ProjectTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProjectPriority)
            .WithMany(x => x.Projects)
            .HasForeignKey(x => x.ProjectPriorityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Template)
            .WithMany()
            .HasForeignKey(x => x.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Portfolio)
            .WithMany(x => x.Projects)
            .HasForeignKey(x => x.PortfolioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Program)
            .WithMany(x => x.Projects)
            .HasForeignKey(x => x.ProgramId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectDevelopmentProfileConfiguration : IEntityTypeConfiguration<ProjectDevelopmentProfile>
{
    public void Configure(EntityTypeBuilder<ProjectDevelopmentProfile> builder)
    {
        builder.HasIndex(x => x.ProjectId).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.DeliveryStructure });

        builder.HasOne(x => x.Project)
            .WithOne(x => x.DevelopmentProfile)
            .HasForeignKey<ProjectDevelopmentProfile>(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ProjectPhaseConfiguration : IEntityTypeConfiguration<ProjectPhase>
{
    public void Configure(EntityTypeBuilder<ProjectPhase> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.ParentPhaseId, x.SortOrder });
        builder.HasIndex(x => new { x.ProjectId, x.Status });

        builder.HasOne(x => x.Project)
            .WithMany(x => x.Phases)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ParentPhase)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentPhaseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectPackageConfiguration : IEntityTypeConfiguration<ProjectPackage>
{
    public void Configure(EntityTypeBuilder<ProjectPackage> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.SortOrder });
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectPhaseId });
        builder.HasIndex(x => new { x.ProjectId, x.Code });

        builder.HasOne(x => x.Project)
            .WithMany(x => x.Packages)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProjectPhase)
            .WithMany(x => x.Packages)
            .HasForeignKey(x => x.ProjectPhaseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectBoqItemConfiguration : IEntityTypeConfiguration<ProjectBoqItem>
{
    public void Configure(EntityTypeBuilder<ProjectBoqItem> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.ProjectPackageId, x.SortOrder });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectPackageId, x.LineNumber });
        builder.HasIndex(x => new { x.ProjectId, x.ItemType });
        builder.HasIndex(x => new { x.TenantId, x.ProjectWorkItemId });
        builder.HasIndex(x => new { x.TenantId, x.SectionCatalogEntryId });
        builder.HasIndex(x => new { x.TenantId, x.TradeCatalogEntryId });
        builder.HasIndex(x => new { x.TenantId, x.CostCodeCatalogEntryId });
        builder.HasIndex(x => new { x.TenantId, x.MeasurementCodeCatalogEntryId });

        builder.HasOne(x => x.Project)
            .WithMany(x => x.BoqItems)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProjectPackage)
            .WithMany(x => x.BoqItems)
            .HasForeignKey(x => x.ProjectPackageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProjectWorkItem)
            .WithMany()
            .HasForeignKey(x => x.ProjectWorkItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SectionCatalogEntry)
            .WithMany()
            .HasForeignKey(x => x.SectionCatalogEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TradeCatalogEntry)
            .WithMany()
            .HasForeignKey(x => x.TradeCatalogEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CostCodeCatalogEntry)
            .WithMany()
            .HasForeignKey(x => x.CostCodeCatalogEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MeasurementCodeCatalogEntry)
            .WithMany()
            .HasForeignKey(x => x.MeasurementCodeCatalogEntryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectApprovalRegisterItemConfiguration : IEntityTypeConfiguration<ProjectApprovalRegisterItem>
{
    public void Configure(EntityTypeBuilder<ProjectApprovalRegisterItem> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectPhaseId, x.ApprovalType });
        builder.HasIndex(x => new { x.ProjectId, x.TargetDecisionDate });

        builder.HasOne(x => x.Project)
            .WithMany(x => x.ApprovalRegisterItems)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProjectPhase)
            .WithMany(x => x.ApprovalRegisterItems)
            .HasForeignKey(x => x.ProjectPhaseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectDrawingConfiguration : IEntityTypeConfiguration<ProjectDrawing>
{
    public void Configure(EntityTypeBuilder<ProjectDrawing> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.DrawingNumber, x.Revision }).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.ProjectPhaseId, x.Discipline });
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.SupersedesDrawingId }).IsUnique()
            .HasFilter("[SupersedesDrawingId] IS NOT NULL AND [IsDeleted] = 0");

        builder.HasOne(x => x.Project)
            .WithMany(x => x.Drawings)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProjectPhase)
            .WithMany()
            .HasForeignKey(x => x.ProjectPhaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SupersedesDrawing)
            .WithMany()
            .HasForeignKey(x => x.SupersedesDrawingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectSubmittalConfiguration : IEntityTypeConfiguration<ProjectSubmittal>
{
    public void Configure(EntityTypeBuilder<ProjectSubmittal> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.ReferenceNumber });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectPhaseId, x.ProjectPackageId });
        builder.HasIndex(x => new { x.ProjectId, x.Status, x.SubmittalType });

        builder.HasOne(x => x.Project)
            .WithMany(x => x.Submittals)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProjectPhase)
            .WithMany()
            .HasForeignKey(x => x.ProjectPhaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProjectPackage)
            .WithMany()
            .HasForeignKey(x => x.ProjectPackageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectRfiConfiguration : IEntityTypeConfiguration<ProjectRfi>
{
    public void Configure(EntityTypeBuilder<ProjectRfi> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.ReferenceNumber });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectPhaseId, x.ProjectPackageId });
        builder.HasIndex(x => new { x.ProjectId, x.Status, x.Priority });

        builder.HasOne(x => x.Project)
            .WithMany(x => x.Rfis)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProjectPhase)
            .WithMany()
            .HasForeignKey(x => x.ProjectPhaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProjectPackage)
            .WithMany()
            .HasForeignKey(x => x.ProjectPackageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectSiteInstructionConfiguration : IEntityTypeConfiguration<ProjectSiteInstruction>
{
    public void Configure(EntityTypeBuilder<ProjectSiteInstruction> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.ReferenceNumber });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectPhaseId, x.ProjectPackageId });
        builder.HasIndex(x => new { x.ProjectId, x.Status, x.InstructionType });

        builder.HasOne(x => x.Project)
            .WithMany(x => x.SiteInstructions)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProjectPhase)
            .WithMany()
            .HasForeignKey(x => x.ProjectPhaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProjectPackage)
            .WithMany()
            .HasForeignKey(x => x.ProjectPackageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectVariationOrderConfiguration : IEntityTypeConfiguration<ProjectVariationOrder>
{
    public void Configure(EntityTypeBuilder<ProjectVariationOrder> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectPhaseId, x.ProjectPackageId });
        builder.HasIndex(x => new { x.ProjectId, x.ContractId });
        builder.HasIndex(x => new { x.ProjectId, x.RequestedDate });

        builder.HasOne(x => x.Project)
            .WithMany(x => x.VariationOrders)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProjectPhase)
            .WithMany()
            .HasForeignKey(x => x.ProjectPhaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProjectPackage)
            .WithMany()
            .HasForeignKey(x => x.ProjectPackageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Contract)
            .WithMany()
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class ProjectInterimValuationConfiguration : IEntityTypeConfiguration<ProjectInterimValuation>
{
    public void Configure(EntityTypeBuilder<ProjectInterimValuation> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectPhaseId, x.ProjectPackageId });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectMilestoneId });
        builder.HasIndex(x => new { x.ProjectId, x.ContractId });
        builder.HasIndex(x => new { x.ProjectId, x.ValuationDate });

        builder.HasOne(x => x.Project)
            .WithMany(x => x.InterimValuations)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProjectPhase)
            .WithMany()
            .HasForeignKey(x => x.ProjectPhaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProjectPackage)
            .WithMany()
            .HasForeignKey(x => x.ProjectPackageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProjectMilestone)
            .WithMany()
            .HasForeignKey(x => x.ProjectMilestoneId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Contract)
            .WithMany()
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class ProjectInterimValuationPackageCompletionConfiguration : IEntityTypeConfiguration<ProjectInterimValuationPackageCompletion>
{
    public void Configure(EntityTypeBuilder<ProjectInterimValuationPackageCompletion> builder)
    {
        builder.HasIndex(x => x.ProjectInterimValuationId);
        builder.HasIndex(x => x.ProjectPackageId);
        builder.HasIndex(x => new { x.ProjectInterimValuationId, x.ProjectPackageId }).IsUnique();

        builder.HasOne(x => x.ProjectInterimValuation)
            .WithMany(x => x.CompletedProjectPackages)
            .HasForeignKey(x => x.ProjectInterimValuationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProjectPackage)
            .WithMany()
            .HasForeignKey(x => x.ProjectPackageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectPaymentCertificateConfiguration : IEntityTypeConfiguration<ProjectPaymentCertificate>
{
    public void Configure(EntityTypeBuilder<ProjectPaymentCertificate> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectPhaseId, x.ProjectPackageId });
        builder.HasIndex(x => new { x.ProjectId, x.ContractId });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectInterimValuationId });
        builder.HasIndex(x => new { x.ProjectId, x.IssueDate });
        builder.HasIndex(x => new { x.TenantId, x.CertificateNumber }).IsUnique()
            .HasFilter("[CertificateNumber] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(x => new { x.TenantId, x.QuantitySurveyValuationWorksheetId }).IsUnique()
            .HasFilter("[QuantitySurveyValuationWorksheetId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(x => new { x.TenantId, x.QuantitySurveySubcontractValuationId }).IsUnique()
            .HasFilter("[QuantitySurveySubcontractValuationId] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(x => new { x.TenantId, x.ClientRequestId }).IsUnique()
            .HasFilter("[ClientRequestId] <> '00000000-0000-0000-0000-000000000000'");
        builder.HasIndex(x => new { x.TenantId, x.LastMutationClientRequestId }).IsUnique()
            .HasFilter("[LastMutationClientRequestId] IS NOT NULL");
        builder.HasIndex(x => new { x.TenantId, x.VendorInvoiceId }).IsUnique()
            .HasFilter("[VendorInvoiceId] IS NOT NULL");
        builder.Property(x => x.RequestHash).IsUnicode(false);
        builder.Property(x => x.LastMutationRequestHash).IsUnicode(false);
        builder.Property(x => x.PolicyHash).IsUnicode(false);
        builder.Property(x => x.GeneratedDocumentHash).IsUnicode(false);
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.Property(x => x.ApprovalStatus).HasDefaultValue("Draft");
        builder.Property(x => x.ApHandoffStatus).HasDefaultValue(ProjectPaymentCertificateApHandoffStatuses.NotReady);
        builder.Property(x => x.PaymentStatusSnapshot).HasDefaultValue("NotInvoiced");
        builder.Property(x => x.TaxHandling).HasDefaultValue("FinanceCalculated");

        builder.HasOne(x => x.Project)
            .WithMany(x => x.PaymentCertificates)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProjectPhase)
            .WithMany()
            .HasForeignKey(x => x.ProjectPhaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProjectPackage)
            .WithMany()
            .HasForeignKey(x => x.ProjectPackageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Contract)
            .WithMany()
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ProjectInterimValuation)
            .WithMany()
            .HasForeignKey(x => x.ProjectInterimValuationId)
            // Avoid a SQL Server multiple-cascade-path conflict because the valuation already belongs to the same project.
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.QuantitySurveyValuationWorksheet).WithMany()
            .HasForeignKey(x => x.QuantitySurveyValuationWorksheetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.QuantitySurveySubcontractValuation).WithMany()
            .HasForeignKey(x => x.QuantitySurveySubcontractValuationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SubcontractorBusinessPartner).WithMany()
            .HasForeignKey(x => x.SubcontractorBusinessPartnerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PreviousPaymentCertificate).WithMany()
            .HasForeignKey(x => x.PreviousPaymentCertificateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.VendorInvoice).WithMany()
            .HasForeignKey(x => x.VendorInvoiceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ErpSystem.Core.Entities.Finance.Account>().WithMany()
            .HasForeignKey(x => x.ExpenseAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ErpSystem.Core.Entities.Finance.Account>().WithMany()
            .HasForeignKey(x => x.AccountsPayableAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ErpSystem.Core.Entities.Finance.PaymentTerm>().WithMany()
            .HasForeignKey(x => x.PaymentTermId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ErpSystem.Core.Entities.Finance.TaxGroup>().WithMany()
            .HasForeignKey(x => x.TaxGroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ErpSystem.Core.Entities.Finance.Tax>().WithMany()
            .HasForeignKey(x => x.WithholdingTaxId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ErpSystem.Core.Entities.QuantitySurvey.QuantitySurveyConfigurationProfile>().WithMany()
            .HasForeignKey(x => x.ConfigurationProfileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ErpSystem.Core.Entities.QuantitySurvey.QuantitySurveyConfigurationDecision>().WithMany()
            .HasForeignKey(x => x.ValuationDecisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ErpSystem.Core.Entities.Workflow.WorkflowDefinition>().WithMany()
            .HasForeignKey(x => x.ApprovalWorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ErpSystem.Core.Entities.ReportTemplate>().WithMany()
            .HasForeignKey(x => x.CertificateTemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ErpSystem.Core.Entities.DocumentManagement.CentralDocumentMetadataTemplate>().WithMany()
            .HasForeignKey(x => x.CertificateMetadataTemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ErpSystem.Core.Entities.DocumentManagement.CentralDocumentRecord>().WithMany()
            .HasForeignKey(x => x.CentralDocumentRecordId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ErpSystem.Core.Entities.DocumentManagement.CentralDocumentVersion>().WithMany()
            .HasForeignKey(x => x.CentralDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ErpSystem.Core.Entities.ApplicationUser>().WithMany()
            .HasForeignKey(x => x.PreparedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ErpSystem.Core.Entities.ApplicationUser>().WithMany()
            .HasForeignKey(x => x.SubmittedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ErpSystem.Core.Entities.ApplicationUser>().WithMany()
            .HasForeignKey(x => x.ApprovedById).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_ProjectPaymentCertificates_Status", "[Status] IN ('Draft','Issued','Approved','Paid','Cancelled')");
            table.HasCheckConstraint("CK_ProjectPaymentCertificates_Approval", "[ApprovalStatus] IN ('Draft','Pending','Approved','Rejected')");
            table.HasCheckConstraint("CK_ProjectPaymentCertificates_ApHandoff", "[ApHandoffStatus] IN ('NotReady','Ready','Created','Failed')");
            table.HasCheckConstraint("CK_ProjectPaymentCertificates_Amounts", "[CertifiedToDateAmount] >= 0 AND [PreviouslyCertifiedAmount] >= 0 AND [GrossCertifiedAmount] >= 0 AND [RetentionHeldAmount] >= 0 AND [RetentionReleasedAmount] >= 0 AND [OtherDeductionsAmount] >= 0 AND [AdvanceRecoveryAmount] >= 0 AND [MaterialDeductionAmount] >= 0 AND [MaterialOnSiteAmount] >= 0 AND [MaterialOffSiteAmount] >= 0 AND [TaxAmount] >= 0 AND [NetCertifiedAmount] >= 0");
            table.HasCheckConstraint("CK_ProjectPaymentCertificates_QsMaterial", "[QuantitySurveyValuationWorksheetId] IS NULL OR (([QuantitySurveyMaterialReconciliationId] IS NULL AND [MaterialDeductionAmount] = 0 AND [MaterialOnSiteAmount] = 0 AND [MaterialOffSiteAmount] = 0) OR [QuantitySurveyMaterialReconciliationId] IS NOT NULL)");
            table.HasCheckConstraint("CK_ProjectPaymentCertificates_QsLineage", "[QuantitySurveyValuationWorksheetId] IS NULL OR ([ProjectInterimValuationId] IS NOT NULL AND [ConfigurationProfileId] IS NOT NULL AND [ValuationDecisionId] IS NOT NULL AND [ApprovalWorkflowDefinitionId] IS NOT NULL AND [CertificateTemplateId] IS NOT NULL AND [CertificateMetadataTemplateId] IS NOT NULL AND [CertificateMetadataTemplateCodeSnapshot] IS NOT NULL AND [ExpenseAccountId] IS NOT NULL AND [AccountsPayableAccountId] IS NOT NULL AND [PaymentTermId] IS NOT NULL AND [TaxGroupId] IS NOT NULL AND [PreparedById] IS NOT NULL AND [PreparedAt] > '2000-01-01' AND [ClientRequestId] <> '00000000-0000-0000-0000-000000000000' AND [CertificateNumber] IS NOT NULL AND LEN([PolicyHash]) = 64 AND LEN([RequestHash]) = 64)");
            table.HasCheckConstraint("CK_ProjectPaymentCertificates_QsSubcontractLineage", "[QuantitySurveySubcontractValuationId] IS NULL OR ([QuantitySurveyValuationWorksheetId] IS NULL AND [SubcontractorBusinessPartnerId] IS NOT NULL AND [ContractId] IS NOT NULL AND [ConfigurationProfileId] IS NOT NULL AND [ValuationDecisionId] IS NOT NULL AND [ApprovalWorkflowDefinitionId] IS NOT NULL AND [ExpenseAccountId] IS NOT NULL AND [AccountsPayableAccountId] IS NOT NULL AND [PaymentTermId] IS NOT NULL AND [TaxGroupId] IS NOT NULL AND [CertificateNumber] IS NOT NULL AND LEN([PolicyHash]) = 64 AND LEN([RequestHash]) = 64)");
            table.HasCheckConstraint("CK_ProjectPaymentCertificates_GovernedState", "[QuantitySurveyValuationWorksheetId] IS NULL OR (([Status] = 'Draft' AND [ApprovalStatus] = 'Draft' AND [WorkflowInstanceId] IS NULL AND [ApprovedAt] IS NULL) OR ([Status] = 'Issued' AND [ApprovalStatus] = 'Pending' AND [WorkflowInstanceId] IS NOT NULL AND [ApprovedAt] IS NULL) OR ([Status] IN ('Approved','Paid') AND [ApprovalStatus] = 'Approved' AND [WorkflowInstanceId] IS NOT NULL AND [ApprovedById] IS NOT NULL AND [ApprovedAt] IS NOT NULL) OR ([Status] = 'Cancelled' AND [ApprovalStatus] = 'Rejected' AND [WorkflowInstanceId] IS NOT NULL AND [RejectionReason] IS NOT NULL))");
            table.HasCheckConstraint("CK_ProjectPaymentCertificates_ApLink", "([VendorInvoiceId] IS NULL AND [ApHandoffStatus] IN ('NotReady','Ready','Failed')) OR ([VendorInvoiceId] IS NOT NULL AND [ApHandoffStatus] = 'Created')");
        });
    }
}

public sealed class ProjectExtensionOfTimeConfiguration : IEntityTypeConfiguration<ProjectExtensionOfTime>
{
    public void Configure(EntityTypeBuilder<ProjectExtensionOfTime> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectPhaseId, x.ProjectPackageId });
        builder.HasIndex(x => new { x.ProjectId, x.ContractId });
        builder.HasIndex(x => new { x.ProjectId, x.RequestedDate });

        builder.HasOne(x => x.Project)
            .WithMany(x => x.ExtensionOfTimeRequests)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProjectPhase)
            .WithMany()
            .HasForeignKey(x => x.ProjectPhaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProjectPackage)
            .WithMany()
            .HasForeignKey(x => x.ProjectPackageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Contract)
            .WithMany()
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class ProjectFinalAccountConfiguration : IEntityTypeConfiguration<ProjectFinalAccount>
{
    public void Configure(EntityTypeBuilder<ProjectFinalAccount> builder)
    {
        builder.HasIndex(x => x.ProjectId).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.ContractId });
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.ClientRequestId })
            .IsUnique()
            .HasFilter("[ClientRequestId] <> '00000000-0000-0000-0000-000000000000' AND [IsDeleted] = 0");
        builder.HasIndex(x => new { x.TenantId, x.ApprovalWorkflowDefinitionId });
        builder.HasIndex(x => new { x.TenantId, x.ApprovedBoqVersionId });

        builder.HasOne(x => x.Project)
            .WithOne(x => x.FinalAccount)
            .HasForeignKey<ProjectFinalAccount>(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Contract)
            .WithMany()
            .HasForeignKey(x => x.ContractId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ApprovedBoqVersion)
            .WithMany()
            .HasForeignKey(x => x.ApprovedBoqVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_ProjectFinalAccounts_QS0506Amounts",
                "[OriginalContractValue] >= 0 AND [ApprovedBoqValue] >= 0 AND [ApprovedVariationAmount] >= 0 AND [ApprovedClaimAmount] >= 0 AND [ApprovedEscalationAmount] >= 0 AND [CertifiedToDate] >= 0 AND [RetentionHeldAmount] >= 0 AND [RetentionReleasedAmount] >= 0 AND [RetentionReleasedAmount] <= [RetentionHeldAmount] AND [AdvanceRecoveryAmount] >= 0 AND [MaterialDeductionAmount] >= 0 AND [OtherDeductionAmount] >= 0 AND [PaidToDateAmount] >= 0 AND [FinalAccountValue] >= 0");
        });
    }
}

public sealed class ProjectFinalAccountRevisionConfiguration : IEntityTypeConfiguration<ProjectFinalAccountRevision>
{
    public void Configure(EntityTypeBuilder<ProjectFinalAccountRevision> builder)
    {
        builder.HasIndex(x => new { x.ProjectFinalAccountId, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.Action, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.ClientRequestId }).IsUnique();
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_ProjectFinalAccountRevisions_QS0506Request",
                "[ClientRequestId] <> '00000000-0000-0000-0000-000000000000' AND LEN([RequestHash]) = 64");
        });
        builder.HasOne(x => x.ProjectFinalAccount)
            .WithMany(x => x.Revisions)
            .HasForeignKey(x => x.ProjectFinalAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectUnitConfiguration : IEntityTypeConfiguration<ProjectUnit>
{
    public void Configure(EntityTypeBuilder<ProjectUnit> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.SortOrder });
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.ProjectId, x.Code });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectBuildingId });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectFloorId });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectUnitReleaseBatchId });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectUnitTypeTemplateId });
        builder.HasIndex(x => new { x.ProjectId, x.CustomerBusinessPartnerId });
        builder.HasIndex(x => new { x.ProjectId, x.SalesAgreementId });
        builder.HasIndex(x => new { x.ProjectId, x.SalesOrderId });

        builder.HasOne(x => x.Project)
            .WithMany(x => x.Units)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProjectBuilding)
            .WithMany(x => x.Units)
            .HasForeignKey(x => x.ProjectBuildingId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.ProjectFloor)
            .WithMany(x => x.Units)
            .HasForeignKey(x => x.ProjectFloorId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.ProjectUnitReleaseBatch)
            .WithMany(x => x.Units)
            .HasForeignKey(x => x.ProjectUnitReleaseBatchId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.ProjectUnitTypeTemplate)
            .WithMany(x => x.Units)
            .HasForeignKey(x => x.ProjectUnitTypeTemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.SalesAgreement)
            .WithMany()
            .HasForeignKey(x => x.SalesAgreementId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.SalesOrder)
            .WithMany()
            .HasForeignKey(x => x.SalesOrderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class ProjectBuildingConfiguration : IEntityTypeConfiguration<ProjectBuilding>
{
    public void Configure(EntityTypeBuilder<ProjectBuilding> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.SortOrder });
        builder.HasIndex(x => new { x.ProjectId, x.Code });

        builder.HasOne(x => x.Project)
            .WithMany(x => x.Buildings)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ProjectFloorConfiguration : IEntityTypeConfiguration<ProjectFloor>
{
    public void Configure(EntityTypeBuilder<ProjectFloor> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.SortOrder });
        builder.HasIndex(x => new { x.ProjectId, x.Code });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectBuildingId });

        builder.HasOne(x => x.Project)
            .WithMany(x => x.Floors)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProjectBuilding)
            .WithMany(x => x.Floors)
            .HasForeignKey(x => x.ProjectBuildingId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class ProjectUnitReleaseBatchConfiguration : IEntityTypeConfiguration<ProjectUnitReleaseBatch>
{
    public void Configure(EntityTypeBuilder<ProjectUnitReleaseBatch> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.SortOrder });
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.ProjectId, x.Code });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectBuildingId });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectFloorId });

        builder.HasOne(x => x.Project)
            .WithMany(x => x.UnitReleaseBatches)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProjectBuilding)
            .WithMany(x => x.ReleaseBatches)
            .HasForeignKey(x => x.ProjectBuildingId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.ProjectFloor)
            .WithMany(x => x.ReleaseBatches)
            .HasForeignKey(x => x.ProjectFloorId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class ProjectUnitHandoverBatchConfiguration : IEntityTypeConfiguration<ProjectUnitHandoverBatch>
{
    public void Configure(EntityTypeBuilder<ProjectUnitHandoverBatch> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.SortOrder });
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.ProjectId, x.Code });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectBuildingId });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectFloorId });

        builder.HasOne(x => x.Project)
            .WithMany(x => x.UnitHandoverBatches)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProjectBuilding)
            .WithMany(x => x.HandoverBatches)
            .HasForeignKey(x => x.ProjectBuildingId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.ProjectFloor)
            .WithMany(x => x.HandoverBatches)
            .HasForeignKey(x => x.ProjectFloorId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class ProjectUnitTypeTemplateConfiguration : IEntityTypeConfiguration<ProjectUnitTypeTemplate>
{
    public void Configure(EntityTypeBuilder<ProjectUnitTypeTemplate> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Name });
        builder.HasIndex(x => new { x.TenantId, x.IsActive, x.SortOrder });
    }
}

public sealed class ProjectUnitTypeTemplateAmenityConfiguration : IEntityTypeConfiguration<ProjectUnitTypeTemplateAmenity>
{
    public void Configure(EntityTypeBuilder<ProjectUnitTypeTemplateAmenity> builder)
    {
        builder.HasIndex(x => x.ProjectUnitTypeTemplateId);
        builder.HasIndex(x => x.InventoryItemId);
        builder.HasIndex(x => new { x.ProjectUnitTypeTemplateId, x.InventoryItemId, x.SortOrder });

        builder.HasOne(x => x.ProjectUnitTypeTemplate)
            .WithMany(x => x.Amenities)
            .HasForeignKey(x => x.ProjectUnitTypeTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.InventoryItem)
            .WithMany()
            .HasForeignKey(x => x.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectUnitAmenityConfiguration : IEntityTypeConfiguration<ProjectUnitAmenity>
{
    public void Configure(EntityTypeBuilder<ProjectUnitAmenity> builder)
    {
        builder.HasIndex(x => x.ProjectUnitId);
        builder.HasIndex(x => x.InventoryItemId);
        builder.HasIndex(x => new { x.ProjectUnitId, x.SortOrder });

        builder.HasOne(x => x.ProjectUnit)
            .WithMany(x => x.Amenities)
            .HasForeignKey(x => x.ProjectUnitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.InventoryItem)
            .WithMany()
            .HasForeignKey(x => x.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectCustomerVariationConfiguration : IEntityTypeConfiguration<ProjectCustomerVariation>
{
    public void Configure(EntityTypeBuilder<ProjectCustomerVariation> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectUnitId });
        builder.HasIndex(x => new { x.ProjectId, x.CustomerBusinessPartnerId });
        builder.HasIndex(x => new { x.ProjectId, x.RequestDate });
        builder.HasIndex(x => new { x.ProjectId, x.SalesAgreementId });
        builder.HasIndex(x => new { x.ProjectId, x.SalesOrderId });
        builder.HasIndex(x => new { x.ProjectId, x.JobCardId });
        builder.HasIndex(x => new { x.ProjectId, x.WorkOrderId });

        builder.HasOne(x => x.Project)
            .WithMany(x => x.CustomerVariations)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProjectUnit)
            .WithMany(x => x.CustomerVariations)
            .HasForeignKey(x => x.ProjectUnitId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.SalesAgreement)
            .WithMany()
            .HasForeignKey(x => x.SalesAgreementId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.SalesOrder)
            .WithMany()
            .HasForeignKey(x => x.SalesOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.JobCard)
            .WithMany()
            .HasForeignKey(x => x.JobCardId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.WorkOrder)
            .WithMany()
            .HasForeignKey(x => x.WorkOrderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class ProjectCommissioningItemConfiguration : IEntityTypeConfiguration<ProjectCommissioningItem>
{
    public void Configure(EntityTypeBuilder<ProjectCommissioningItem> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.SortOrder });
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectUnitId });

        builder.HasOne(x => x.Project)
            .WithMany(x => x.CommissioningItems)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProjectUnit)
            .WithMany(x => x.CommissioningItems)
            .HasForeignKey(x => x.ProjectUnitId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class ProjectHandoverItemConfiguration : IEntityTypeConfiguration<ProjectHandoverItem>
{
    public void Configure(EntityTypeBuilder<ProjectHandoverItem> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.SortOrder });
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.ProjectId, x.HandoverType });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectUnitId });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectUnitHandoverBatchId });

        builder.HasOne(x => x.Project)
            .WithMany(x => x.HandoverItems)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProjectUnit)
            .WithMany(x => x.HandoverItems)
            .HasForeignKey(x => x.ProjectUnitId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.ProjectUnitHandoverBatch)
            .WithMany(x => x.HandoverItems)
            .HasForeignKey(x => x.ProjectUnitHandoverBatchId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class ProjectSnagItemConfiguration : IEntityTypeConfiguration<ProjectSnagItem>
{
    public void Configure(EntityTypeBuilder<ProjectSnagItem> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.ProjectId, x.Severity });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectUnitId });
        builder.HasIndex(x => new { x.ProjectId, x.TargetClosureDate });

        builder.HasOne(x => x.Project)
            .WithMany(x => x.SnagItems)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProjectUnit)
            .WithMany(x => x.SnagItems)
            .HasForeignKey(x => x.ProjectUnitId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class ProjectDefectLiabilityCaseConfiguration : IEntityTypeConfiguration<ProjectDefectLiabilityCase>
{
    public void Configure(EntityTypeBuilder<ProjectDefectLiabilityCase> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.ProjectId, x.ProjectUnitId });
        builder.HasIndex(x => new { x.ProjectId, x.CustomerBusinessPartnerId });
        builder.HasIndex(x => new { x.ProjectId, x.TargetResolutionDate });
        builder.HasIndex(x => new { x.ProjectId, x.WarrantyExpiryDate });
        builder.HasIndex(x => new { x.ProjectId, x.FirstResponseDate });
        builder.HasIndex(x => new { x.ProjectId, x.JobCardId });
        builder.HasIndex(x => new { x.ProjectId, x.WorkOrderId });

        builder.HasOne(x => x.Project)
            .WithMany(x => x.DefectLiabilityCases)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProjectUnit)
            .WithMany(x => x.DefectLiabilityCases)
            .HasForeignKey(x => x.ProjectUnitId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.JobCard)
            .WithMany()
            .HasForeignKey(x => x.JobCardId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.WorkOrder)
            .WithMany()
            .HasForeignKey(x => x.WorkOrderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class ProjectInitiationVersionConfiguration : IEntityTypeConfiguration<ProjectInitiationVersion>
{
    public void Configure(EntityTypeBuilder<ProjectInitiationVersion> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.VersionNumber }).IsUnique();
    }
}

public sealed class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.UserId, x.Role }).IsUnique();
    }
}

public sealed class ProjectWorkItemConfiguration : IEntityTypeConfiguration<ProjectWorkItem>
{
    public void Configure(EntityTypeBuilder<ProjectWorkItem> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.ParentId, x.SortOrder });
        builder.HasIndex(x => x.ProjectPackageId);
        builder.HasIndex(x => new { x.ProjectId, x.ProjectPackageId });

        builder.HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProjectPackage)
            .WithMany()
            .HasForeignKey(x => x.ProjectPackageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectMilestoneConfiguration : IEntityTypeConfiguration<ProjectMilestone>
{
    public void Configure(EntityTypeBuilder<ProjectMilestone> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.TargetDate });
    }
}

public sealed class ProjectMilestonePhaseConfiguration : IEntityTypeConfiguration<ProjectMilestonePhase>
{
    public void Configure(EntityTypeBuilder<ProjectMilestonePhase> builder)
    {
        builder.HasIndex(x => new { x.ProjectMilestoneId, x.ProjectPhaseId }).IsUnique();
        builder.HasIndex(x => x.ProjectPhaseId);

        builder.HasOne(x => x.ProjectMilestone)
            .WithMany(x => x.PhaseSelections)
            .HasForeignKey(x => x.ProjectMilestoneId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ProjectPhase)
            .WithMany(x => x.MilestoneSelections)
            .HasForeignKey(x => x.ProjectPhaseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectResourceAllocationConfiguration : IEntityTypeConfiguration<ProjectResourceAllocation>
{
    public void Configure(EntityTypeBuilder<ProjectResourceAllocation> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.UserId, x.StartDate, x.EndDate });
        builder.HasIndex(x => new { x.UserId, x.Status });

        builder.HasOne(x => x.WorkItem)
            .WithMany()
            .HasForeignKey(x => x.WorkItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectRiskConfiguration : IEntityTypeConfiguration<ProjectRisk>
{
    public void Configure(EntityTypeBuilder<ProjectRisk> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Status });
    }
}

public sealed class ProjectIssueConfiguration : IEntityTypeConfiguration<ProjectIssue>
{
    public void Configure(EntityTypeBuilder<ProjectIssue> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Status });
    }
}

public sealed class ProjectQualityCheckpointConfiguration : IEntityTypeConfiguration<ProjectQualityCheckpoint>
{
    public void Configure(EntityTypeBuilder<ProjectQualityCheckpoint> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.ProjectId, x.DueDate });

        builder.HasOne(x => x.WorkItem)
            .WithMany()
            .HasForeignKey(x => x.WorkItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Deliverable)
            .WithMany()
            .HasForeignKey(x => x.DeliverableId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectNonConformanceConfiguration : IEntityTypeConfiguration<ProjectNonConformance>
{
    public void Configure(EntityTypeBuilder<ProjectNonConformance> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.ProjectId, x.Severity });

        builder.HasOne(x => x.QualityCheckpoint)
            .WithMany()
            .HasForeignKey(x => x.QualityCheckpointId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Deliverable)
            .WithMany()
            .HasForeignKey(x => x.DeliverableId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectChangeRequestConfiguration : IEntityTypeConfiguration<ProjectChangeRequest>
{
    public void Configure(EntityTypeBuilder<ProjectChangeRequest> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Status });
    }
}

public sealed class ProjectBillingScheduleConfiguration : IEntityTypeConfiguration<ProjectBillingSchedule>
{
    public void Configure(EntityTypeBuilder<ProjectBillingSchedule> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.BillingDate });
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.ContractId, x.ContractMilestoneId });

        builder.HasOne(x => x.Milestone)
            .WithMany()
            .HasForeignKey(x => x.MilestoneId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectInvoiceRequestConfiguration : IEntityTypeConfiguration<ProjectInvoiceRequest>
{
    public void Configure(EntityTypeBuilder<ProjectInvoiceRequest> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.RequestNumber }).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.Status });

        builder.HasOne(x => x.BillingSchedule)
            .WithMany()
            .HasForeignKey(x => x.BillingScheduleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectDocumentConfiguration : IEntityTypeConfiguration<ProjectDocument>
{
    public void Configure(EntityTypeBuilder<ProjectDocument> builder)
    {
        builder.Property(x => x.ArtifactType)
            .HasMaxLength(30);

        builder.HasIndex(x => new { x.ProjectId, x.Category });
        builder.HasIndex(x => new { x.ProjectId, x.IsExternalVisible });
        builder.HasIndex(x => new { x.ProjectId, x.ArtifactType, x.ArtifactId });
    }
}

public sealed class ProjectCommentConfiguration : IEntityTypeConfiguration<ProjectComment>
{
    public void Configure(EntityTypeBuilder<ProjectComment> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.WorkItemId });
    }
}

public sealed class ProjectDeliverableConfiguration : IEntityTypeConfiguration<ProjectDeliverable>
{
    public void Configure(EntityTypeBuilder<ProjectDeliverable> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.ProjectId, x.IsExternalVisible, x.ExternalSubmissionAllowed });
    }
}

public sealed class ProjectDeliverableExternalReviewConfiguration : IEntityTypeConfiguration<ProjectDeliverableExternalReview>
{
    public void Configure(EntityTypeBuilder<ProjectDeliverableExternalReview> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.DeliverableId, x.ReviewDate });
        builder.HasIndex(x => new { x.ProjectId, x.Decision, x.StatusSnapshot });

        builder.HasOne(x => x.Deliverable)
            .WithMany(x => x.ExternalReviews)
            .HasForeignKey(x => x.DeliverableId)
            .OnDelete(DeleteBehavior.Cascade);

        // Avoid a SQL Server multiple-cascade-path conflict because Deliverable already rolls up to Project.
        builder.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SubmittedDocument)
            .WithMany()
            .HasForeignKey(x => x.SubmittedDocumentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectTaskDependencyConfiguration : IEntityTypeConfiguration<ProjectTaskDependency>
{
    public void Configure(EntityTypeBuilder<ProjectTaskDependency> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.PredecessorWorkItemId, x.SuccessorWorkItemId }).IsUnique();
    }
}

public sealed class ProjectInterdependencyConfiguration : IEntityTypeConfiguration<ProjectInterdependency>
{
    public void Configure(EntityTypeBuilder<ProjectInterdependency> builder)
    {
        builder.HasIndex(x => new { x.TenantId, x.SourceProjectId, x.TargetProjectId, x.DependencyType }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Status, x.ImpactLevel });

        builder.HasOne(x => x.SourceProject)
            .WithMany()
            .HasForeignKey(x => x.SourceProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TargetProject)
            .WithMany()
            .HasForeignKey(x => x.TargetProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectBaselineConfiguration : IEntityTypeConfiguration<ProjectBaseline>
{
    public void Configure(EntityTypeBuilder<ProjectBaseline> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Name });
    }
}

public sealed class ProjectTimesheetEntryConfiguration : IEntityTypeConfiguration<ProjectTimesheetEntry>
{
    public void Configure(EntityTypeBuilder<ProjectTimesheetEntry> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.UserId, x.EntryDate });
        builder.HasIndex(x => new { x.ProjectId, x.Status });
    }
}

public sealed class ProjectExpenseConfiguration : IEntityTypeConfiguration<ProjectExpense>
{
    public void Configure(EntityTypeBuilder<ProjectExpense> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.UserId, x.ExpenseDate });
        builder.HasIndex(x => new { x.ProjectId, x.Status });
    }
}

public sealed class ProjectMaterialCostEntryConfiguration : IEntityTypeConfiguration<ProjectMaterialCostEntry>
{
    public void Configure(EntityTypeBuilder<ProjectMaterialCostEntry> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.EntryDate });
        builder.HasIndex(x => new { x.ProjectId, x.EntryType, x.PostingState });
        builder.HasIndex(x => new { x.TenantId, x.SourceTransactionType, x.SourceTransactionId }).IsUnique();
    }
}

public sealed class ProjectRevenueRecognitionConfiguration : IEntityTypeConfiguration<ProjectRevenueRecognition>
{
    public void Configure(EntityTypeBuilder<ProjectRevenueRecognition> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.RecognitionPeriod });
    }
}

public sealed class ProjectBudgetRevisionConfiguration : IEntityTypeConfiguration<ProjectBudgetRevision>
{
    public void Configure(EntityTypeBuilder<ProjectBudgetRevision> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.Status });
        builder.HasIndex(x => new { x.ProjectId, x.EffectiveDate });
    }
}

public sealed class ProjectForecastVersionConfiguration : IEntityTypeConfiguration<ProjectForecastVersion>
{
    public void Configure(EntityTypeBuilder<ProjectForecastVersion> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.IsActive });
        builder.HasIndex(x => new { x.ProjectId, x.AsOfDate });
    }
}

public sealed class ProjectAssetLinkConfiguration : IEntityTypeConfiguration<ProjectAssetLink>
{
    public void Configure(EntityTypeBuilder<ProjectAssetLink> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.LinkType, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.ReconciliationKey })
            .IsUnique()
            .HasFilter("[ReconciliationKey] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasOne<FixedAsset>()
            .WithMany()
            .HasForeignKey(x => x.FixedAssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectExternalAccessPolicyConfiguration : IEntityTypeConfiguration<ProjectExternalAccessPolicy>
{
    public void Configure(EntityTypeBuilder<ProjectExternalAccessPolicy> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.BusinessPartnerId, x.ArtifactType, x.ArtifactId }).IsUnique();
    }
}

public sealed class ProjectDecisionConfiguration : IEntityTypeConfiguration<ProjectDecision>
{
    public void Configure(EntityTypeBuilder<ProjectDecision> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.DecisionDate });
        builder.HasIndex(x => new { x.ProjectId, x.Status });
    }
}

public sealed class ProjectMeetingMinuteConfiguration : IEntityTypeConfiguration<ProjectMeetingMinute>
{
    public void Configure(EntityTypeBuilder<ProjectMeetingMinute> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.MeetingDate });
    }
}

public sealed class ProjectActionItemConfiguration : IEntityTypeConfiguration<ProjectActionItem>
{
    public void Configure(EntityTypeBuilder<ProjectActionItem> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Status, x.DueDate });

        builder.HasOne(x => x.Project)
            .WithMany(x => x.ActionItems)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MeetingMinute)
            .WithMany(x => x.ActionItems)
            .HasForeignKey(x => x.MeetingMinuteId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.WorkItem)
            .WithMany()
            .HasForeignKey(x => x.WorkItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProjectLessonLearnedConfiguration : IEntityTypeConfiguration<ProjectLessonLearned>
{
    public void Configure(EntityTypeBuilder<ProjectLessonLearned> builder)
    {
        builder.HasIndex(x => new { x.ProjectId, x.Category });
    }
}

public sealed class ProjectClosureConfiguration : IEntityTypeConfiguration<ProjectClosure>
{
    public void Configure(EntityTypeBuilder<ProjectClosure> builder)
    {
        builder.HasIndex(x => x.ProjectId).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.Status });
    }
}
