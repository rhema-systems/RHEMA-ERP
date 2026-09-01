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

    [Fact]
    public async Task ConversionAssignsTheSelectedRegistrationCategoryToTheSupplier()
    {
        var tenantId = Guid.NewGuid();
        var registrationId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var registration = new BusinessPartnerRegistration
        {
            Id = registrationId,
            TenantId = tenantId,
            RegistrationNumber = "APP-002",
            ApplicantName = "Goods Supplier Limited",
            ApplicantEmail = "goods@example.test",
            PartnerType = "Supplier",
            RegistrationCategory = ErpSystem.Core.Enums.ProcurementSupplierRegistrationCategory.Goods,
            Status = "UnderReview",
            RegistrationDataJson = """
                {
                  "PartnerType": "Supplier",
                  "CompanyName": "Goods Supplier Limited",
                  "Email": "goods@example.test"
                }
                """
        };
        var registrations = new Mock<IBusinessPartnerRegistrationRepository>();
        registrations.Setup(item => item.GetByIdAsync(registrationId))
            .ReturnsAsync(registration);
        var partners = new Mock<IBusinessPartnerRepository>();
        partners.Setup(item => item.GeneratePartnerCodeAsync("Supplier"))
            .ReturnsAsync("SUP-002");
        BusinessPartner? createdPartner = null;
        partners.Setup(item => item.CreateAsync(It.IsAny<BusinessPartner>()))
            .Callback<BusinessPartner>(value => createdPartner = value)
            .ReturnsAsync((BusinessPartner value) => value);
        var documents = new Mock<IBusinessPartnerRegistrationDocumentRepository>();
        documents.Setup(item => item.GetDocumentsByRegistrationAsync(registrationId))
            .ReturnsAsync(Array.Empty<BusinessPartnerRegistrationDocument>());
        var categoryRepository = new Mock<IGenericRepository<PartnerCategory>>();
        categoryRepository.Setup(item => item.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<PartnerCategory, bool>>>() ))
            .ReturnsAsync(new PartnerCategory
            {
                Id = categoryId,
                TenantId = tenantId,
                CategoryCode = "GOODS",
                CategoryName = "Goods",
                CategoryType = "Supplier",
                IsActive = true
            });
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(item => item.Repository<PartnerCategory>())
            .Returns(categoryRepository.Object);

        var service = new BusinessPartnerRegistrationService(
            registrations.Object,
            documents.Object,
            Mock.Of<IBusinessPartnerRegistrationStatusHistoryRepository>(),
            partners.Object,
            Mock.Of<IBusinessPartnerContactRepository>(),
            Mock.Of<IBusinessPartnerFinancialRepository>(),
            Mock.Of<IBusinessPartnerDocumentRepository>(),
            Mock.Of<IBusinessPartnerLicenseRepository>(),
            unitOfWork.Object,
            Mock.Of<ICurrentUserProvider>(),
            Mock.Of<IAppEventBus>(),
            Mock.Of<IProcurementAccessControlService>(),
            NullLogger<BusinessPartnerRegistrationService>.Instance);

        await service.ConvertToBusinessPartnerAsync(registrationId, approverId);

        createdPartner.Should().NotBeNull();
        createdPartner!.Categories.Should().ContainSingle();
        createdPartner.Categories.Single().CategoryId.Should().Be(categoryId);
        createdPartner.Categories.Single().IsPrimary.Should().BeTrue();
        createdPartner.Categories.Single().BusinessPartner.Should().BeSameAs(createdPartner,
            "the category join must be part of the new supplier graph before its first flush");
    }

    [Fact]
    public async Task ApprovalUsesOneTransactionAndRepeatedApprovalDoesNotCreateAnotherSupplier()
    {
        var tenantId = Guid.NewGuid();
        var registrationId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var registration = new BusinessPartnerRegistration
        {
            Id = registrationId,
            TenantId = tenantId,
            RegistrationNumber = "APP-003",
            ApplicantName = "Atomic Supplier Limited",
            ApplicantEmail = "atomic@example.test",
            PartnerType = "Supplier",
            Status = "Submitted",
            RegistrationCategory = ErpSystem.Core.Enums.ProcurementSupplierRegistrationCategory.Goods,
            RegistrationDataJson = """
                {
                  "PartnerType": "Supplier",
                  "CompanyName": "Atomic Supplier Limited",
                  "Email": "atomic@example.test"
                }
                """
        };
        var registrations = new Mock<IBusinessPartnerRegistrationRepository>();
        registrations.Setup(item => item.GetByIdAsync(registrationId))
            .ReturnsAsync(registration);
        var documents = new Mock<IBusinessPartnerRegistrationDocumentRepository>();
        documents.Setup(item => item.GetByRegistrationIdAsync(registrationId))
            .ReturnsAsync(Array.Empty<BusinessPartnerRegistrationDocument>());
        documents.Setup(item => item.GetDocumentsByRegistrationAsync(registrationId))
            .ReturnsAsync(Array.Empty<BusinessPartnerRegistrationDocument>());
        var partners = new Mock<IBusinessPartnerRepository>();
        partners.Setup(item => item.GeneratePartnerCodeAsync("Supplier"))
            .ReturnsAsync("SUP-003");
        partners.Setup(item => item.CreateAsync(It.IsAny<BusinessPartner>()))
            .ReturnsAsync((BusinessPartner value) => value);
        var history = new Mock<IBusinessPartnerRegistrationStatusHistoryRepository>();
        history.Setup(item => item.CreateAsync(It.IsAny<BusinessPartnerRegistrationStatusHistory>()))
            .ReturnsAsync((BusinessPartnerRegistrationStatusHistory value) => value);
        var categoryRepository = new Mock<IGenericRepository<PartnerCategory>>();
        categoryRepository.Setup(item => item.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<PartnerCategory, bool>>>() ))
            .ReturnsAsync(new PartnerCategory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CategoryCode = "GOODS",
                CategoryName = "Goods",
                CategoryType = "Supplier",
                IsActive = true
            });
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(item => item.Repository<PartnerCategory>())
            .Returns(categoryRepository.Object);
        unitOfWork.Setup(item => item.ExecuteInStrategyAsync(
                It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> operation, CancellationToken _) => operation());
        unitOfWork.Setup(item => item.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        unitOfWork.Setup(item => item.AcquireTransactionLockAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        unitOfWork.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        unitOfWork.Setup(item => item.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
        currentUser.SetupGet(item => item.IsExternalUser).Returns(false);
        currentUser.SetupGet(item => item.UserId).Returns(approverId);
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.Setup(item => item.HasRole(It.IsAny<string>())).Returns(true);

        var service = new BusinessPartnerRegistrationService(
            registrations.Object,
            documents.Object,
            history.Object,
            partners.Object,
            Mock.Of<IBusinessPartnerContactRepository>(),
            Mock.Of<IBusinessPartnerFinancialRepository>(),
            Mock.Of<IBusinessPartnerDocumentRepository>(),
            Mock.Of<IBusinessPartnerLicenseRepository>(),
            unitOfWork.Object,
            currentUser.Object,
            Mock.Of<IAppEventBus>(),
            Mock.Of<IProcurementAccessControlService>(),
            NullLogger<BusinessPartnerRegistrationService>.Instance);

        await service.ApproveRegistrationAsync(registrationId, approverId);
        await service.ApproveRegistrationAsync(registrationId, approverId);

        registration.Status.Should().Be("Approved");
        registration.BusinessPartnerId.Should().NotBeNull();
        partners.Verify(item => item.CreateAsync(It.IsAny<BusinessPartner>()), Times.Once);
        registrations.Verify(item => item.UpdateAsync(It.IsAny<BusinessPartnerRegistration>()), Times.Never);
        unitOfWork.Verify(item => item.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        unitOfWork.Verify(item => item.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
