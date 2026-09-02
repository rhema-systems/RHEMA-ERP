using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
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
    public async Task ContractCreationRepairsExactAwardTenderLineageWithContractPermission()
    {
        var fixture = new Fixture();
        var requisitionId = Guid.NewGuid();
        var releaseId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var tender = new Tender
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            TenderNumber = "TND-CONTRACT-LINEAGE",
            Title = "Award contract lineage",
            Status = "Awarded",
            SourcePurchaseRequisitionId = requisitionId,
            SourcingReleaseId = releaseId
        };
        var award = new TenderAward
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            TenderId = tender.Id,
            TenderBidId = Guid.NewGuid(),
            BusinessPartnerId = Guid.NewGuid(),
            AwardedAmount = 1_000m,
            Currency = "GHS",
            Status = "Awarded"
        };
        Contract? created = null;
        fixture.Awards.Setup(repository => repository.GetByIdAsync(award.Id))
            .ReturnsAsync(award);
        fixture.Tenders.Setup(repository => repository.GetByIdAsync(tender.Id))
            .ReturnsAsync(tender);
        fixture.Contracts.Setup(repository => repository.GetByAwardIdAsync(award.Id))
            .ReturnsAsync((Contract?)null);
        fixture.Contracts.Setup(repository => repository.GenerateContractNumberAsync())
            .ReturnsAsync("CON-2026-TEST");
        fixture.Contracts.Setup(repository => repository.CreateAsync(It.IsAny<Contract>()))
            .Callback((Contract value) => created = value)
            .ReturnsAsync((Contract value) => value);
        fixture.Contracts.Setup(repository => repository.GetByIdWithDetailsAsync(It.IsAny<Guid>()))
            .ReturnsAsync(() => created);
        fixture.SupplierValidation.Setup(service => service.EnforceEligibilityAsync(
                It.IsAny<SupplierEligibilityEvaluationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SupplierValidationResult
            {
                IsValid = true,
                TenantId = fixture.TenantId,
                BusinessPartnerId = award.BusinessPartnerId,
                Boundary = SupplierEligibilityBoundary.Contract
            });
        fixture.SourcingCases.Setup(service => service.RecoverTenderSourceEntryAsync(
                requisitionId,
                releaseId,
                tender.Id,
                tender.TenderNumber,
                $"contract-award-{award.Id:N}",
                ProcurementTenderSourceRecoveryBoundary.ContractCreation,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSourcingCaseEntryGateDto
            {
                SourcingReleaseId = releaseId,
                SourcingCaseId = caseId,
                SelectedMethod = ProcurementMethodType.NationalCompetitiveTendering
            });

        var result = await fixture.Service.CreateFromAwardAsync(new CreateContractDto
        {
            TenderAwardId = award.Id,
            ContractTitle = "Awarded goods contract",
            ContractType = "Goods",
            ContractValue = award.AwardedAmount,
            Currency = "GHS"
        });

        result.ContractNumber.Should().Be("CON-2026-TEST");
        tender.SourcingCaseId.Should().Be(caseId);
        fixture.Tenders.Verify(repository => repository.UpdateAsync(tender), Times.Once);
        fixture.SourcingCases.VerifyAll();
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
                Tenders.Object,
                Mock.Of<ITenderBidRepository>(),
                UnitOfWork.Object,
                CurrentUser.Object,
                SupplierValidation.Object,
                SourcingCases.Object,
                NullLogger<ContractService>.Instance);
        }

        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid UserId { get; } = Guid.NewGuid();
        public Mock<IContractRepository> Contracts { get; } = new();
        public Mock<IContractAmendmentRepository> Amendments { get; } = new();
        public Mock<ITenderAwardRepository> Awards { get; } = new();
        public Mock<ITenderRepository> Tenders { get; } = new();
        public Mock<ICurrentUserProvider> CurrentUser { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<ISupplierValidationService> SupplierValidation { get; } = new();
        public Mock<IProcurementSourcingCaseService> SourcingCases { get; } = new();
        public ContractService Service { get; }
    }
}
