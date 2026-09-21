using System.Security.Cryptography;
using System.Reflection;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Api.Services.QuantitySurvey;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Documents;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyCertificateDocumentTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Certificate_handoff_uses_Finance_identity_and_rejects_a_different_partner(bool matchingPartner)
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tenant=Guid.NewGuid();
        var user=new Mock<ICurrentUserService>();
        user.SetupGet(x=>x.TenantId).Returns(tenant);
        var contract=new Contract { TenantId=tenant, BusinessPartnerId=Guid.NewGuid(), ContractType="Works", Status="Active" };
        db.Add(contract);
        await db.SaveChangesAsync();
        var identity=new Mock<IApSupplierIdentityService>();
        var supplierId=Guid.NewGuid();
        identity.Setup(x=>x.ResolveByBusinessPartnerAsync(contract.BusinessPartnerId,It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApSupplierIdentityDto { BusinessPartnerId=matchingPartner ? contract.BusinessPartnerId : Guid.NewGuid(), SupplierId=supplierId });
        var invoices=new Mock<IVendorInvoiceService>();
        VendorInvoiceCreateDto? captured=null;
        invoices.Setup(x=>x.CreateAsync(It.IsAny<VendorInvoiceCreateDto>(),It.IsAny<CancellationToken>()))
            .Callback<VendorInvoiceCreateDto,CancellationToken>((value,_)=>captured=value)
            .ThrowsAsync(new InvalidOperationException("invoice boundary reached"));
        var service=new QuantitySurveyPaymentCertificateService(db,user.Object,Mock.Of<IProjectService>(),
            Mock.Of<ICivilEngineeringIpcEndorsementService>(),Mock.Of<IWorkflowIntegrationService>(),Mock.Of<IWorkflowStatusAdapterRegistry>(),
            invoices.Object,Mock.Of<ITaxCalculationEngine>(),Mock.Of<IDocumentOutputService>(),Mock.Of<IControlledFileUploadService>(),
            Mock.Of<ICentralDocumentRepositoryFileService>(),Mock.Of<IProcurementBudgetCommitmentLifecycleService>(),identity.Object);
        var certificate=new ProjectPaymentCertificate { TenantId=tenant,ContractId=contract.Id,NetCertifiedAmount=12000,
            TaxAmount=2000,CertificateNumber="IPC-UAT-001",Status="Approved",ApprovalStatus="Approved" };
        var method=typeof(QuantitySurveyPaymentCertificateService).GetMethod("CreateApInvoiceAsync",BindingFlags.Instance|BindingFlags.NonPublic)!;
        Func<Task> act=()=> (Task)method.Invoke(service,[certificate,"uat-handoff",CancellationToken.None])!;
        if (matchingPartner)
        {
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("invoice boundary reached");
            captured!.SupplierId.Should().Be(supplierId);
            captured.AcceptedSupplySourceId.Should().Be(certificate.Id);
            captured.IsTrustedAcceptedSupplyHandoff.Should().BeTrue();
            captured.LineItems.Single().UnitPrice.Should().Be(10000);
        }
        else
        {
            await act.Should().ThrowAsync<Exception>().WithMessage("*does not match*");
            captured.Should().BeNull();
        }
        identity.Verify(x=>x.ResolveByBusinessPartnerAsync(contract.BusinessPartnerId,It.IsAny<CancellationToken>()),Times.Once);
    }

    [Fact]
    public async Task Approved_certificate_uses_frozen_DMS_type_and_business_metadata_and_reuses_registration()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tenant = Guid.NewGuid();
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.TenantId).Returns(tenant);
        user.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        user.SetupGet(x => x.Roles).Returns(Array.Empty<string>());
        var projects = new Mock<IProjectService>();
        projects.Setup(x => x.HasProjectAccessAsync(It.IsAny<Guid>())).ReturnsAsync(true);
        var template = new ErpSystem.Core.Entities.DocumentManagement.CentralDocumentMetadataTemplate { TenantId=tenant, Module="QuantitySurvey",
            DocumentType="QS payment certificate evidence", TemplateCode="QS-CERT-EVD", SourceLabel="QS",
            AccessProfile="QS project and audit scope", IsActive=true, PublishedAt=DateTime.UtcNow };
        var certificate = new ProjectPaymentCertificate { TenantId=tenant, ProjectId=Guid.NewGuid(), ContractId=Guid.NewGuid(),
            QuantitySurveyValuationWorksheetId=Guid.NewGuid(), CertificateMetadataTemplateId=template.Id,
            CertificateMetadataTemplateCodeSnapshot=template.TemplateCode, CertificateNumber="IPC-UAT-001",
            Status=ProjectPaymentCertificateStatuses.Approved, ApprovalStatus="Approved", IssueDate=new DateTime(2026,9,20),
            Title="UAT certificate", RowVersion=[1] };
        db.AddRange(template,certificate);
        await db.SaveChangesAsync();
        var output = new Mock<IDocumentOutputService>();
        var bytes = "synthetic certificate"u8.ToArray();
        output.Setup(x => x.RenderAsync(It.IsAny<DocumentRenderRequestDto>(),It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RenderedDocumentDto { Content=bytes,FileName="uat.pdf",ContentType="application/pdf" });
        var files = new Mock<IControlledFileUploadService>();
        var upload = new FileUploadRecord { TenantId=tenant,VirusScanStatus=FileVirusScanStatus.Clean };
        files.Setup(x => x.UploadAsync(It.IsAny<ControlledFileUploadRequest>(),It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ControlledFileUploadResult { Record=upload,
                ChecksumSha256=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), PublicUrl="" });
        var documents = new Mock<ICentralDocumentRepositoryFileService>();
        CentralDocumentRepositoryRegistration? captured=null;
        documents.Setup(x => x.RegisterAsync(It.IsAny<CentralDocumentRepositoryRegistration>(),It.IsAny<CancellationToken>()))
            .Callback<CentralDocumentRepositoryRegistration,CancellationToken>((registration,_) => captured=registration)
            .ReturnsAsync(new CentralDocumentRepositoryLink { DocumentRecordId=Guid.NewGuid(),DocumentVersionId=Guid.NewGuid(),FileUploadRecordId=upload.Id,
                DocumentReference="UAT-DMS-1",VersionNumber="1" });
        var service = new QuantitySurveyPaymentCertificateService(db,user.Object,projects.Object,
            Mock.Of<ICivilEngineeringIpcEndorsementService>(),Mock.Of<IWorkflowIntegrationService>(),Mock.Of<IWorkflowStatusAdapterRegistry>(),
            Mock.Of<IVendorInvoiceService>(),Mock.Of<ITaxCalculationEngine>(),output.Object,files.Object,documents.Object,
            Mock.Of<IProcurementBudgetCommitmentLifecycleService>(),Mock.Of<IApSupplierIdentityService>());
        await service.RenderAsync(certificate.Id,"uat-render");
        captured.Should().NotBeNull();
        captured!.DocumentType.Should().Be(template.DocumentType);
        captured.AccessProfile.Should().Be(template.AccessProfile);
        var metadata = captured.MetadataValues.ToDictionary(x=>x.FieldKey,x=>x.FieldValue);
        metadata["contractId"].Should().Be(certificate.ContractId.ToString());
        metadata["recordReference"].Should().Be("IPC-UAT-001");
        metadata["evidenceDate"].Should().Be("2026-09-20");
        await service.RenderAsync(certificate.Id,"uat-render-retry");
        documents.Verify(x=>x.RegisterAsync(It.IsAny<CentralDocumentRepositoryRegistration>(),It.IsAny<CancellationToken>()),Times.Once);
        files.Verify(x=>x.UploadAsync(It.IsAny<ControlledFileUploadRequest>(),It.IsAny<CancellationToken>()),Times.Once);
    }
}
