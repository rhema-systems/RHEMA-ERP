using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Common;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class BusinessPartnerRegistrationConversionTests
{
    [Fact]
    public async Task TokenApplicantAuditSubjectIsNotUsedAsBusinessPartnerUserForeignKey()
    {
        var tenantId = Guid.NewGuid();
        var registrationId = Guid.NewGuid();
        var applicantSessionSubjectId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var registration = new BusinessPartnerRegistration
        {
            Id = registrationId,
            TenantId = tenantId,
            RegistrationNumber = "APP-001",
            ApplicantName = "Supplier Limited",
            ApplicantEmail = "supplier@example.test",
            PartnerType = "Supplier",
            Status = "UnderReview",
            CreatedById = applicantSessionSubjectId,
            RegistrationDataJson = """
                {
                  "PartnerType": "Supplier",
                  "CompanyName": "Supplier Limited",
                  "Email": "supplier@example.test"
                }
                """
        };
        var registrations = new Mock<IBusinessPartnerRegistrationRepository>();
        registrations.Setup(item => item.GetByIdAsync(registrationId))
            .ReturnsAsync(registration);
        var partners = new Mock<IBusinessPartnerRepository>();
        partners.Setup(item => item.GeneratePartnerCodeAsync("Supplier"))
            .ReturnsAsync("SUP-001");
        BusinessPartner? createdPartner = null;
        partners.Setup(item => item.CreateAsync(It.IsAny<BusinessPartner>()))
            .Callback<BusinessPartner>(value => createdPartner = value)
            .ReturnsAsync((BusinessPartner value) => value);
        var documents = new Mock<IBusinessPartnerRegistrationDocumentRepository>();
        documents.Setup(item => item.GetDocumentsByRegistrationAsync(registrationId))
            .ReturnsAsync(Array.Empty<BusinessPartnerRegistrationDocument>());

        var service = new BusinessPartnerRegistrationService(
            registrations.Object,
            documents.Object,
            Mock.Of<IBusinessPartnerRegistrationStatusHistoryRepository>(),
            partners.Object,
            Mock.Of<IBusinessPartnerContactRepository>(),
            Mock.Of<IBusinessPartnerFinancialRepository>(),
            Mock.Of<IBusinessPartnerDocumentRepository>(),
            Mock.Of<IBusinessPartnerLicenseRepository>(),
            Mock.Of<IUnitOfWork>(),
            Mock.Of<ICurrentUserProvider>(),
            Mock.Of<IAppEventBus>(),
            Mock.Of<IProcurementAccessControlService>(),
            NullLogger<BusinessPartnerRegistrationService>.Instance);

        await service.ConvertToBusinessPartnerAsync(registrationId, approverId);

        createdPartner.Should().NotBeNull();
        createdPartner!.UserId.Should().BeNull(
            "an applicant-session audit subject is not an ApplicationUser");
        createdPartner.CreatedById.Should().Be(approverId);
        createdPartner.ApprovedById.Should().Be(approverId);
    }
}
