using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public class BusinessPartnerServiceContactTests
{
    [Fact]
    public async Task GetByIdAsync_ShouldReturnAssignedBusinessCategories()
    {
        var fixture = new BusinessPartnerContactFixture();
        fixture.AddCategory("SUP-GOODS", "Goods", isPrimary: true);
        var service = fixture.CreateService();

        var result = await service.GetByIdAsync(fixture.Partner.Id);

        result.Should().NotBeNull();
        result!.Categories.Should().Equal("Goods");
    }

    [Fact]
    public async Task AddContactAsync_ShouldCreatePrimaryContactAndSyncPartnerSummary()
    {
        var fixture = new BusinessPartnerContactFixture();
        var service = fixture.CreateService();

        var created = await service.AddContactAsync(fixture.Partner.Id, new CreateBusinessPartnerContactDto
        {
            ContactName = "Irene Mensah",
            Title = "Commercial Director",
            Department = "Commercial",
            Email = "irene@atlas.test",
            Phone = "+1 555 0100",
            Mobile = "+1 555 0101",
            IsPrimary = false
        });

        created.IsPrimary.Should().BeTrue();
        fixture.Partner.PrimaryContactName.Should().Be("Irene Mensah");
        fixture.Partner.PrimaryContactTitle.Should().Be("Commercial Director");
        fixture.Partner.PrimaryEmail.Should().Be("irene@atlas.test");
        fixture.Partner.PrimaryPhone.Should().Be("+1 555 0100");
        fixture.Contacts.Should().ContainSingle(x => x.Id == created.Id && x.IsPrimary);
    }

    [Fact]
    public async Task UpdateContactAsync_ShouldPromoteReplacementWhenPrimaryIsUnset()
    {
        var fixture = new BusinessPartnerContactFixture();
        fixture.AddContact("Primary Contact", isPrimary: true, email: "primary@atlas.test");
        var secondary = fixture.AddContact("Secondary Contact", email: "secondary@atlas.test");
        var service = fixture.CreateService();

        var updated = await service.UpdateContactAsync(fixture.Partner.Id, fixture.Contacts[0].Id, new CreateBusinessPartnerContactDto
        {
            ContactName = "Primary Contact",
            Title = "Director",
            Email = "primary@atlas.test",
            Phone = "+1 555 0100",
            IsPrimary = false
        });

        updated.IsPrimary.Should().BeFalse();
        fixture.Contacts.Single(x => x.Id == secondary.Id).IsPrimary.Should().BeTrue();
        fixture.Partner.PrimaryContactName.Should().Be("Secondary Contact");
        fixture.Partner.PrimaryEmail.Should().Be("secondary@atlas.test");
    }

    [Fact]
    public async Task DeleteContactAsync_ShouldPromoteRemainingPrimaryAndSyncPartnerSummary()
    {
        var fixture = new BusinessPartnerContactFixture();
        var primary = fixture.AddContact("Kojo Asare", isPrimary: true, phone: "+1 555 1000");
        var replacement = fixture.AddContact("Ama Boateng", email: "ama@atlas.test");
        var service = fixture.CreateService();

        await service.DeleteContactAsync(fixture.Partner.Id, primary.Id);

        fixture.Contacts.Single(x => x.Id == primary.Id).IsDeleted.Should().BeTrue();
        fixture.Contacts.Single(x => x.Id == replacement.Id).IsPrimary.Should().BeTrue();
        fixture.Partner.PrimaryContactName.Should().Be("Ama Boateng");
        fixture.Partner.PrimaryEmail.Should().Be("ama@atlas.test");
    }

    private sealed class BusinessPartnerContactFixture
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid UserId { get; } = Guid.NewGuid();
        public BusinessPartner Partner { get; }
        public List<BusinessPartnerContact> Contacts { get; } = new();

        private readonly Mock<IBusinessPartnerRepository> _partnerRepository = new();
        private readonly Mock<IBusinessPartnerContactRepository> _contactRepository = new();
        private readonly Mock<ICurrentUserProvider> _currentUserProvider = new();
        private readonly Mock<IWorkflowIntegrationService> _workflowIntegrationService = new();
        private readonly Mock<IWorkflowStatusAdapterRegistry> _workflowStatusAdapterRegistry = new();
        private readonly Mock<IPaymentTermRepository> _paymentTermRepository = new();

        public BusinessPartnerContactFixture()
        {
            Partner = new BusinessPartner
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PartnerCode = "CUST-CRM-001",
                PartnerName = "Atlas Infrastructure",
                PartnerType = "Customer",
                RegistrationStatus = "Approved"
            };

            _partnerRepository
                .Setup(x => x.GetByIdAsync(Partner.Id))
                .ReturnsAsync(Partner);
            _partnerRepository
                .Setup(x => x.GetWithAllRelatedDataAsync(Partner.Id))
                .ReturnsAsync(Partner);
            _partnerRepository
                .Setup(x => x.UpdateAsync(It.IsAny<BusinessPartner>()))
                .ReturnsAsync((BusinessPartner partner) => partner);

            _contactRepository
                .Setup(x => x.GetContactsByPartnerAsync(Partner.Id))
                .ReturnsAsync(() => Contacts
                    .Where(x => x.BusinessPartnerId == Partner.Id && !x.IsDeleted)
                    .OrderByDescending(x => x.IsPrimary)
                    .ThenBy(x => x.ContactName)
                    .ToList());
            _contactRepository
                .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Guid id) => Contacts.FirstOrDefault(x => x.Id == id && !x.IsDeleted));
            _contactRepository
                .Setup(x => x.CreateAsync(It.IsAny<BusinessPartnerContact>()))
                .ReturnsAsync((BusinessPartnerContact contact) =>
                {
                    Contacts.Add(contact);
                    return contact;
                });
            _contactRepository
                .Setup(x => x.UpdateAsync(It.IsAny<BusinessPartnerContact>()))
                .ReturnsAsync((BusinessPartnerContact contact) => contact);
            _contactRepository
                .Setup(x => x.DeleteAsync(It.IsAny<Guid>()))
                .Returns((Guid id) =>
                {
                    var contact = Contacts.First(x => x.Id == id);
                    contact.IsDeleted = true;
                    return Task.CompletedTask;
                });
            _contactRepository
                .Setup(x => x.SetPrimaryContactAsync(Partner.Id, It.IsAny<Guid>()))
                .Returns((Guid _, Guid contactId) =>
                {
                    foreach (var contact in Contacts.Where(x => x.BusinessPartnerId == Partner.Id && !x.IsDeleted))
                    {
                        contact.IsPrimary = contact.Id == contactId;
                    }

                    return Task.CompletedTask;
                });
            _contactRepository
                .Setup(x => x.SaveChangesAsync())
                .ReturnsAsync(1);

            _currentUserProvider.SetupGet(x => x.TenantId).Returns(TenantId);
            _currentUserProvider.SetupGet(x => x.UserId).Returns(UserId);
        }

        public BusinessPartnerService CreateService()
            => new(
                _partnerRepository.Object,
                _contactRepository.Object,
                _currentUserProvider.Object,
                _workflowIntegrationService.Object,
                _workflowStatusAdapterRegistry.Object,
                _paymentTermRepository.Object,
                NullLogger<BusinessPartnerService>.Instance);

        public BusinessPartnerContact AddContact(
            string name,
            bool isPrimary = false,
            string? email = null,
            string? phone = null)
        {
            var contact = new BusinessPartnerContact
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BusinessPartnerId = Partner.Id,
                ContactName = name,
                Email = email,
                Phone = phone,
                IsPrimary = isPrimary
            };

            Contacts.Add(contact);
            if (isPrimary)
            {
                Partner.PrimaryContactName = contact.ContactName;
                Partner.PrimaryEmail = contact.Email;
                Partner.PrimaryPhone = contact.Phone;
            }

            return contact;
        }

        public void AddCategory(string code, string name, bool isPrimary = false)
        {
            var category = new PartnerCategory
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                CategoryCode = code,
                CategoryName = name,
                CategoryType = "Supplier",
                IsActive = true
            };

            Partner.Categories.Add(new BusinessPartnerCategory
            {
                Id = Guid.NewGuid(),
                BusinessPartnerId = Partner.Id,
                CategoryId = category.Id,
                Category = category,
                BusinessPartner = Partner,
                IsPrimary = isPrimary
            });
        }
    }
}
