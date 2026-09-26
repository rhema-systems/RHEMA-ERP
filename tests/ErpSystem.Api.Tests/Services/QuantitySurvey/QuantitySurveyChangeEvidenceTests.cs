using System.Security.Cryptography;
using ErpSystem.Api.Services.QuantitySurvey;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.DocumentManagement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.QuantitySurvey;

public sealed class QuantitySurveyChangeEvidenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Change_evidence_uses_frozen_template_and_real_business_metadata(bool claim)
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tenant = Guid.NewGuid(); var projectId = Guid.NewGuid(); var id = Guid.NewGuid();
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(x => x.TenantId).Returns(tenant);
        user.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
        user.SetupGet(x => x.Roles).Returns(Array.Empty<string>());
        var projects = new Mock<IProjectService>();
        projects.Setup(x => x.HasProjectAccessAsync(projectId)).ReturnsAsync(true);
        var partner = new BusinessPartner { TenantId = tenant, PartnerName = "UAT contractor" };
        var contract = new Contract { TenantId = tenant, BusinessPartnerId = partner.Id, BusinessPartner = partner, ContractType = "Works", Status = "Active" };
        var template = new ErpSystem.Core.Entities.DocumentManagement.CentralDocumentMetadataTemplate { TenantId = tenant, Module = "QuantitySurvey",
            TemplateCode = "QS-VAR-EVD", DocumentType = "QS variation and claim evidence", SourceLabel = "QS",
            AccessProfile = "QS project and audit scope", IsActive = true, PublishedAt = DateTime.UtcNow };
        db.AddRange(partner, contract, template);
        if (claim)
            db.Add(new QuantitySurveyContractClaim { Id = id, TenantId = tenant, ProjectId = projectId, ContractId = contract.Id,
                ContractorBusinessPartnerId = partner.Id, ClaimNumber = "CLM-UAT", EvidenceMetadataTemplateId = template.Id, Status = "Draft", RowVersion = [1] });
        else
            db.Add(new ProjectVariationOrder { Id = id, TenantId = tenant, ProjectId = projectId, ContractId = contract.Id,
                ReferenceNumber = "VO-UAT", EvidenceMetadataTemplateId = template.Id, IsQuantitySurveyGoverned = true, Status = "Draft", RowVersion = [1] });
        await db.SaveChangesAsync();
        var bytes = "synthetic UAT evidence"u8.ToArray();
        var files = new Mock<IControlledFileUploadService>();
        files.Setup(x => x.UploadAsync(It.IsAny<ControlledFileUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ControlledFileUploadResult { Record = new FileUploadRecord { TenantId = tenant, VirusScanStatus = FileVirusScanStatus.Clean },
                PublicUrl = "", ChecksumSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() });
        var documents = new Mock<ICentralDocumentRepositoryFileService>();
        CentralDocumentRepositoryRegistration? captured = null;
        documents.Setup(x => x.RegisterAsync(It.IsAny<CentralDocumentRepositoryRegistration>(), It.IsAny<CancellationToken>()))
            .Callback<CentralDocumentRepositoryRegistration, CancellationToken>((value, _) => captured = value)
            .ThrowsAsync(new InvalidOperationException("DMS boundary inspected"));
        Func<Task> act = claim
            ? () => new QuantitySurveyContractClaimService(db, user.Object, projects.Object, Mock.Of<IWorkflowIntegrationService>(),
                Mock.Of<IWorkflowStatusAdapterRegistry>(), files.Object, documents.Object)
                .UploadEvidenceAsync(id, Guid.NewGuid(), "UAT support", "uat.txt", "text/plain", bytes.Length, () => new MemoryStream(bytes), false, "uat")
            : () => new QuantitySurveyVariationService(db, user.Object, projects.Object, Mock.Of<IWorkflowIntegrationService>(),
                Mock.Of<IWorkflowStatusAdapterRegistry>(), files.Object, documents.Object)
                .UploadEvidenceAsync(id, Guid.NewGuid(), "UAT support", "uat.txt", "text/plain", bytes.Length, () => new MemoryStream(bytes));
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("DMS boundary inspected");
        captured.Should().NotBeNull();
        captured!.DocumentType.Should().Be(template.DocumentType);
        captured.MetadataTemplateCode.Should().Be(template.TemplateCode);
        captured.AccessProfile.Should().Be(template.AccessProfile);
        var metadata = captured.MetadataValues.ToDictionary(x => x.FieldKey, x => x.FieldValue);
        metadata["projectId"].Should().Be(projectId.ToString());
        metadata["contractId"].Should().Be(contract.Id.ToString());
        metadata["recordReference"].Should().Be(claim ? "CLM-UAT" : "VO-UAT");
        DateOnly.Parse(metadata["evidenceDate"]!).Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));
        files.Verify(x => x.DeleteAsync(tenant, It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
