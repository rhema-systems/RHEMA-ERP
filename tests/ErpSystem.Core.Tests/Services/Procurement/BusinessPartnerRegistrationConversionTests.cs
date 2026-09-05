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
    public async Task ConversionPersistsSubmittedContactsWithExactlyOneTenantScopedPrimary()
    {
        var tenantId = Guid.NewGuid();
        var registrationId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var registration = new BusinessPartnerRegistration
        {
            Id = registrationId,
            TenantId = tenantId,
            RegistrationNumber = "APP-004",
            ApplicantName = "Contact Supplier Limited",
            ApplicantEmail = "applicant@example.test",
            PartnerType = "Supplier",
            Status = "UnderReview",
            RegistrationDataJson = """
                {
                  "companyName": "Contact Supplier Limited",
                  "email": "applicant@example.test",
                  "contacts": [
                    {
                      "contactName": "Operations Lead",
                      "contactTitle": "Operations Manager",
                      "department": "Operations",
                      "email": "operations@example.test",
                      "phone": "+233 20 000 0001",
                      "isPrimary": true
                    },
                    {
                      "contactName": "Finance Lead",
                      "title": "Finance Manager",
                      "department": "Finance",
                      "email": "finance@example.test",
                      "mobile": "+233 20 000 0002",
                      "isPrimary": true
                    }
                  ]
                }
                """
        };
        var registrations = new Mock<IBusinessPartnerRegistrationRepository>();
        registrations.Setup(item => item.GetByIdAsync(registrationId))
            .ReturnsAsync(registration);
        var partners = new Mock<IBusinessPartnerRepository>();
        partners.Setup(item => item.GeneratePartnerCodeAsync("Supplier"))
            .ReturnsAsync("SUP-004");
        BusinessPartner? createdPartner = null;
        partners.Setup(item => item.CreateAsync(It.IsAny<BusinessPartner>()))
            .Callback<BusinessPartner>(value => createdPartner = value)
            .ReturnsAsync((BusinessPartner value) => value);
        var documents = new Mock<IBusinessPartnerRegistrationDocumentRepository>();
        documents.Setup(item => item.GetDocumentsByRegistrationAsync(registrationId))
            .ReturnsAsync(Array.Empty<BusinessPartnerRegistrationDocument>());
        var contacts = new Mock<IBusinessPartnerContactRepository>();
        contacts.Setup(item => item.GetContactsByPartnerAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<BusinessPartnerContact>());
        var createdContacts = new List<BusinessPartnerContact>();
        contacts.Setup(item => item.CreateAsync(It.IsAny<BusinessPartnerContact>()))
            .Callback<BusinessPartnerContact>(createdContacts.Add)
            .ReturnsAsync((BusinessPartnerContact value) => value);

        var service = new BusinessPartnerRegistrationService(
            registrations.Object,
            documents.Object,
            Mock.Of<IBusinessPartnerRegistrationStatusHistoryRepository>(),
            partners.Object,
            contacts.Object,
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
        createdContacts.Should().HaveCount(2);
        createdContacts.Should().OnlyContain(contact =>
            contact.TenantId == tenantId &&
            contact.BusinessPartnerId == createdPartner!.Id);
        createdContacts.Should().ContainSingle(contact => contact.IsPrimary)
            .Which.ContactName.Should().Be("Operations Lead");
        createdContacts.Single(contact => contact.ContactName == "Finance Lead")
            .ContactTitle.Should().Be("Finance Manager");
    }

    [Fact]
    public async Task ConversionPersistsSubmittedBankAccountsWithExactlyOneTenantScopedPrimary()
    {
        var tenantId = Guid.NewGuid();
        var registrationId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var registration = new BusinessPartnerRegistration
        {
            Id = registrationId,
            TenantId = tenantId,
            RegistrationNumber = "APP-006",
            ApplicantName = "Multi Bank Supplier Limited",
            ApplicantEmail = "applicant@example.test",
            PartnerType = "Supplier",
            Status = "UnderReview",
            RegistrationDataJson = """
                {
                  "companyName": "Multi Bank Supplier Limited",
                  "email": "applicant@example.test",
                  "bankAccounts": [
                    {
                      "bankName": "Primary Bank",
                      "branchName": "Main Branch",
                      "accountName": "Multi Bank Supplier Limited",
                      "accountNumber": "001-001",
                      "swiftCode": "PRIMARY",
                      "currency": "GHS",
                      "isPrimary": true
                    },
                    {
                      "bankName": "Reserve Bank",
                      "bankBranchCode": "002",
                      "accountName": "Multi Bank Supplier Limited",
                      "accountNumber": "002-002",
                      "iban": "GH00RESERVE",
                      "currency": "USD",
                      "isPrimary": true
                    }
                  ]
                }
                """
        };
        var registrations = new Mock<IBusinessPartnerRegistrationRepository>();
        registrations.Setup(item => item.GetByIdAsync(registrationId))
            .ReturnsAsync(registration);
        var partners = new Mock<IBusinessPartnerRepository>();
        partners.Setup(item => item.GeneratePartnerCodeAsync("Supplier"))
            .ReturnsAsync("SUP-006");
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
        createdPartner!.BankAccounts.Should().HaveCount(2);
        createdPartner.BankAccounts.Should().OnlyContain(account =>
            account.TenantId == tenantId &&
            account.BusinessPartnerId == createdPartner.Id &&
            account.IsActive);
        createdPartner.BankAccounts.Should().ContainSingle(account => account.IsPrimary)
            .Which.BankName.Should().Be("Primary Bank");
        createdPartner.BankName.Should().Be("Primary Bank");
        createdPartner.BankAccountNumber.Should().Be("001-001");
        createdPartner.BankBranch.Should().Be("Main Branch");
    }

    [Fact]
    public async Task RepeatedApprovalBackfillsMissingBankAccountWithoutDuplicates()
    {
        var tenantId = Guid.NewGuid();
        var registrationId = Guid.NewGuid();
        var businessPartnerId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var registration = new BusinessPartnerRegistration
        {
            Id = registrationId,
            TenantId = tenantId,
            RegistrationNumber = "APP-007",
            ApplicantName = "Legacy Bank Supplier",
            ApplicantEmail = "legacy@example.test",
            PartnerType = "Supplier",
            Status = "Approved",
            BusinessPartnerId = businessPartnerId,
            RegistrationDataJson = """
                {
                  "companyName": "Legacy Bank Supplier",
                  "bankAccounts": [
                    {
                      "bankName": "Primary Bank",
                      "accountName": "Legacy Bank Supplier",
                      "accountNumber": "001 001",
                      "isPrimary": true
                    },
                    {
                      "bankName": "Reserve Bank",
                      "accountName": "Legacy Bank Supplier",
                      "accountNumber": "002 002",
                      "isPrimary": false
                    }
                  ]
                }
                """
        };
        var existingAccount = new BusinessPartnerBankAccount
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = businessPartnerId,
            BankName = "PRIMARY BANK",
            AccountName = "Legacy Bank Supplier",
            AccountNumber = "001-001",
            IsPrimary = false,
            IsActive = true
        };
        var partner = new BusinessPartner
        {
            Id = businessPartnerId,
            TenantId = tenantId,
            PartnerName = "Legacy Bank Supplier"
        };
        partner.BankAccounts.Add(existingAccount);
        var registrations = new Mock<IBusinessPartnerRegistrationRepository>();
        registrations.Setup(item => item.GetByIdAsync(registrationId))
            .ReturnsAsync(registration);
        var partners = new Mock<IBusinessPartnerRepository>();
        partners.Setup(item => item.GetWithBankAccountsAsync(businessPartnerId))
            .ReturnsAsync(partner);
        var contacts = new Mock<IBusinessPartnerContactRepository>();
        contacts.Setup(item => item.GetContactsByPartnerAsync(businessPartnerId))
            .ReturnsAsync(Array.Empty<BusinessPartnerContact>());
        var unitOfWork = CreateApprovalUnitOfWork();
        var currentUser = CreateApprover(tenantId, approverId);

        var service = new BusinessPartnerRegistrationService(
            registrations.Object,
            Mock.Of<IBusinessPartnerRegistrationDocumentRepository>(),
            Mock.Of<IBusinessPartnerRegistrationStatusHistoryRepository>(),
            partners.Object,
            contacts.Object,
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

        partner.BankAccounts.Should().HaveCount(2);
        partner.BankAccounts.Should().ContainSingle(account => account.IsPrimary)
            .Which.Id.Should().Be(existingAccount.Id);
        partner.BankName.Should().Be("PRIMARY BANK");
        partner.BankAccountNumber.Should().Be("001-001");
        partners.Verify(item => item.CreateAsync(It.IsAny<BusinessPartner>()), Times.Never);
    }

    [Fact]
    public async Task RepeatedApprovalPromotesEquivalentContactWithoutCreatingADuplicate()
    {
        var tenantId = Guid.NewGuid();
        var registrationId = Guid.NewGuid();
        var businessPartnerId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var registration = new BusinessPartnerRegistration
        {
            Id = registrationId,
            TenantId = tenantId,
            RegistrationNumber = "APP-005",
            ApplicantName = "Legacy Contact Supplier",
            ApplicantEmail = "legacy@example.test",
            PartnerType = "Supplier",
            Status = "Approved",
            BusinessPartnerId = businessPartnerId,
            RegistrationDataJson = """
                {
                  "companyName": "Legacy Contact Supplier",
                  "contactPersonName": "Legacy Contact",
                  "contactPersonEmail": "legacy.contact@example.test"
                }
                """
        };
        var registrations = new Mock<IBusinessPartnerRegistrationRepository>();
        registrations.Setup(item => item.GetByIdAsync(registrationId))
            .ReturnsAsync(registration);
        var existingContact = new BusinessPartnerContact
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BusinessPartnerId = businessPartnerId,
            ContactName = "Legacy Contact",
            Email = "LEGACY.CONTACT@example.test",
            IsPrimary = false
        };
        var contacts = new Mock<IBusinessPartnerContactRepository>();
        contacts.Setup(item => item.GetContactsByPartnerAsync(businessPartnerId))
            .ReturnsAsync(() => new[] { existingContact });
        contacts.Setup(item => item.SetPrimaryContactAsync(businessPartnerId, existingContact.Id))
            .Callback(() => existingContact.IsPrimary = true)
            .Returns(Task.CompletedTask);
        var unitOfWork = CreateApprovalUnitOfWork();
        var currentUser = CreateApprover(tenantId, approverId);
        var partners = new Mock<IBusinessPartnerRepository>();

        var service = new BusinessPartnerRegistrationService(
            registrations.Object,
            Mock.Of<IBusinessPartnerRegistrationDocumentRepository>(),
            Mock.Of<IBusinessPartnerRegistrationStatusHistoryRepository>(),
            partners.Object,
            contacts.Object,
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

        existingContact.IsPrimary.Should().BeTrue();
        contacts.Verify(item => item.CreateAsync(It.IsAny<BusinessPartnerContact>()), Times.Never);
        contacts.Verify(
            item => item.SetPrimaryContactAsync(businessPartnerId, existingContact.Id),
            Times.Once);
        partners.Verify(item => item.CreateAsync(It.IsAny<BusinessPartner>()), Times.Never);
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

    private static Mock<IUnitOfWork> CreateApprovalUnitOfWork()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
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
        return unitOfWork;
    }

    private static Mock<ICurrentUserProvider> CreateApprover(Guid tenantId, Guid approverId)
    {
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
        currentUser.SetupGet(item => item.IsExternalUser).Returns(false);
        currentUser.SetupGet(item => item.UserId).Returns(approverId);
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
        currentUser.Setup(item => item.HasRole(It.IsAny<string>())).Returns(true);
        return currentUser;
    }
}
