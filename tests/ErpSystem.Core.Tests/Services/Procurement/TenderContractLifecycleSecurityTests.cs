using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class TenderContractLifecycleSecurityTests
{
    [Fact]
    public async Task ContractCreationRequiresFinalizedAwardLineage()
    {
        var fixture = new Fixture();
        var award = new TenderAward
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            TenderId = Guid.NewGuid(),
            TenderBidId = Guid.NewGuid(),
            BusinessPartnerId = Guid.NewGuid(),
            AwardedAmount = 100m,
            Currency = "GHS",
            Status = "Draft"
        };
        fixture.Awards.Setup(repository => repository.GetByIdAsync(award.Id))
            .ReturnsAsync(award);

        await fixture.Service.Invoking(service => service.CreateFromAwardAsync(
                new CreateContractDto
                {
                    TenderAwardId = award.Id,
                    ContractTitle = "Invalid draft award contract",
                    ContractType = "Goods",
                    ContractValue = 100m,
                    Currency = "GHS"
                }))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*finalized award*");
        fixture.Contracts.Verify(
            repository => repository.CreateAsync(It.IsAny<Contract>()), Times.Never);
        fixture.SupplierValidation.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AmendmentRequesterCannotApproveOwnAmendment()
    {
        var fixture = new Fixture();
        var amendment = new ContractAmendment
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            ContractId = Guid.NewGuid(),
            AmendmentNumber = "AMD-001",
            AmendmentType = "ScopeChange",
            Status = "PendingApproval",
            RequestedById = fixture.UserId
        };
        fixture.Amendments.Setup(repository => repository.GetByIdAsync(amendment.Id))
            .ReturnsAsync(amendment);

        await fixture.Service.Invoking(service => service.ProcessAmendmentAsync(
                amendment.Id, new ProcessAmendmentDto { Approved = true }))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*requester cannot approve*");
        fixture.Amendments.Verify(
            repository => repository.UpdateAsync(It.IsAny<ContractAmendment>()), Times.Never);
    }

    [Fact]
    public async Task TimeExtensionAliasCreatesCanonicalTimelineExtension()
    {
        var fixture = new Fixture();
        var contract = new Contract
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            ContractNumber = "CON-001",
            ContractTitle = "Active works contract",
            ContractType = "Works",
            ContractValue = 100m,
            Currency = "GHS",
            Status = "Active",
            EndDate = DateTime.UtcNow.Date.AddDays(10)
        };
        fixture.Contracts.Setup(repository => repository.GetByIdAsync(contract.Id))
            .ReturnsAsync(contract);
        fixture.Amendments.Setup(repository => repository.GetByContractIdAsync(contract.Id))
            .ReturnsAsync(Array.Empty<ContractAmendment>());
        fixture.Amendments.Setup(repository => repository.GenerateAmendmentNumberAsync(contract.Id))
            .ReturnsAsync("AMD-001");
        fixture.Amendments.Setup(repository => repository.CreateAsync(It.IsAny<ContractAmendment>()))
            .ReturnsAsync((ContractAmendment value) => value);

        var result = await fixture.Service.CreateAmendmentAsync(
            contract.Id,
            new CreateContractAmendmentDto
            {
                AmendmentType = "TimeExtension",
                Reason = "Approved delivery delay",
                Description = "Extend the delivery period.",
                NewEndDate = contract.EndDate.Value.AddDays(5)
            });

        result.AmendmentType.Should().Be("TimelineExtension");
        result.NewEndDate.Should().Be(contract.EndDate.Value.AddDays(5));
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            CurrentUser.SetupGet(provider => provider.TenantId).Returns(TenantId);
            CurrentUser.SetupGet(provider => provider.UserId).Returns(UserId);
            Service = new ContractService(
                Contracts.Object,
                Mock.Of<IContractMilestoneRepository>(),
                Amendments.Object,
                Mock.Of<IContractDocumentRepository>(),
                Awards.Object,
                Mock.Of<ITenderRepository>(),
                Mock.Of<ITenderBidRepository>(),
                UnitOfWork.Object,
                CurrentUser.Object,
                SupplierValidation.Object,
                NullLogger<ContractService>.Instance);
        }

        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid UserId { get; } = Guid.NewGuid();
        public Mock<IContractRepository> Contracts { get; } = new();
        public Mock<IContractAmendmentRepository> Amendments { get; } = new();
        public Mock<ITenderAwardRepository> Awards { get; } = new();
        public Mock<ICurrentUserProvider> CurrentUser { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<ISupplierValidationService> SupplierValidation { get; } = new();
        public ContractService Service { get; }
    }
}
