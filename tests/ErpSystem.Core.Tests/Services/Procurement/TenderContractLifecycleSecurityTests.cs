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

    [Theory]
    [InlineData(0, null)]
    [InlineData(5, " Retain 5 percent until inspection acceptance and the agreed release approval. ")]
    [InlineData(10, "Retain 10 percent until completion under the approved terms.")]
    public async Task ContractCreationRepairsExactAwardTenderLineageWithContractPermission(decimal retention, string? clause)
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
            RetentionPercentage = retention,
            RetentionClause = clause,
            Currency = "GHS"
        });

        result.ContractNumber.Should().Be("CON-2026-TEST");
        created!.RetentionPercentage.Should().Be(retention);
        created.RetentionClause.Should().Be(clause?.Trim());
        result.RetentionClause.Should().Be(clause?.Trim());
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

    [Theory]
    [InlineData(5, null)]
    [InlineData(5, "")]
    [InlineData(10, "   ")]
    [InlineData(-1, "Terms")]
    [InlineData(101, "Terms")]
    public async Task InvalidRetentionIsRejectedBeforeCreationOrSourceMutation(decimal retention, string? clause)
    {
        var fixture = new Fixture();
        var tender = new Tender { Id = Guid.NewGuid(), TenantId = fixture.TenantId };
        var award = new TenderAward { Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            TenderId = tender.Id, Status = "Awarded", AwardedAmount = 100m, Currency = "GHS" };
        fixture.Awards.Setup(r => r.GetByIdAsync(award.Id)).ReturnsAsync(award);
        fixture.Tenders.Setup(r => r.GetByIdAsync(tender.Id)).ReturnsAsync(tender);

        await fixture.Service.Invoking(s => s.CreateFromAwardAsync(new CreateContractDto {
            TenderAwardId = award.Id, ContractTitle = "Supply contract", ContractValue = 100m,
            Currency = "GHS", RetentionPercentage = retention, RetentionClause = clause
        })).Should().ThrowAsync<InvalidOperationException>().WithMessage("*retention*");
        fixture.Contracts.Verify(r => r.CreateAsync(It.IsAny<Contract>()), Times.Never);
        fixture.SourcingCases.VerifyNoOtherCalls();
        fixture.SupplierValidation.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(5, "", false)]
    [InlineData(5, "   ", false)]
    [InlineData(5, null, true)]
    [InlineData(10, " Updated release conditions ", true)]
    [InlineData(0, "", true)]
    [InlineData(101, "Terms", false)]
    public async Task RetentionEditsValidateEffectiveTermsBeforeMutation(decimal retention, string? clause, bool valid)
    {
        var fixture = new Fixture();
        var contract = new Contract { Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            ContractType = "Supply", Status = "Draft", RetentionPercentage = 5m,
            RetentionClause = "Original release conditions", ContractTitle = "Original" };
        fixture.Contracts.Setup(r => r.GetByIdAsync(contract.Id)).ReturnsAsync(contract);
        fixture.Contracts.Setup(r => r.GetByIdWithDetailsAsync(contract.Id)).ReturnsAsync(contract);
        var request = new UpdateContractDto { ContractTitle = "Updated", RetentionPercentage = retention, RetentionClause = clause };
        if (valid)
        {
            var result = await fixture.Service.UpdateAsync(contract.Id, request);
            result.RetentionPercentage.Should().Be(retention);
            result.RetentionClause.Should().Be(clause?.Trim() ?? "Original release conditions");
        }
        else
        {
            await fixture.Service.Invoking(s => s.UpdateAsync(contract.Id, request))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage("*retention*");
            contract.RetentionPercentage.Should().Be(5m);
            contract.RetentionClause.Should().Be("Original release conditions");
            contract.ContractTitle.Should().Be("Original");
            fixture.Contracts.Verify(r => r.UpdateAsync(It.IsAny<Contract>()), Times.Never);
        }
    }

    [Fact]
    public async Task GovernedWorksRetentionClauseCannotBeChangedThroughGenericEdit()
    {
        var fixture = new Fixture();
        var contract = new Contract { Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            ContractType = "Works", Status = "Draft", RetentionPercentage = 5m,
            RetentionClause = "Governed terms", CommercialTermsPolicyHash = new string('a', 64) };
        fixture.Contracts.Setup(r => r.GetByIdAsync(contract.Id)).ReturnsAsync(contract);
        await fixture.Service.Invoking(s => s.UpdateAsync(contract.Id,
                new UpdateContractDto { RetentionClause = "Different terms" }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*Quantity Survey*");
        contract.RetentionClause.Should().Be("Governed terms");
        fixture.Contracts.Verify(r => r.UpdateAsync(It.IsAny<Contract>()), Times.Never);
    }

    [Theory]
    [InlineData("PendingSignature")]
    [InlineData("Active")]
    [InlineData("Completed")]
    [InlineData("Suspended")]
    public async Task RetentionCannotChangeOutsideDraft(string status)
    {
        var fixture = new Fixture();
        var contract = new Contract { Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            ContractType = "Supply", Status = status, RetentionPercentage = 5m, RetentionClause = "Approved terms" };
        fixture.Contracts.Setup(r => r.GetByIdAsync(contract.Id)).ReturnsAsync(contract);
        await fixture.Service.Invoking(s => s.UpdateAsync(contract.Id,
                new UpdateContractDto { RetentionClause = "Different terms" }))
            .Should().ThrowAsync<InvalidOperationException>();
        contract.RetentionClause.Should().Be("Approved terms");
        fixture.Contracts.Verify(r => r.UpdateAsync(It.IsAny<Contract>()), Times.Never);
    }

    [Fact]
    public async Task ClauseCapacityIsValidatedBeforeUpdate()
    {
        var fixture = new Fixture();
        var contract = new Contract { Id = Guid.NewGuid(), Status = "Draft" };
        fixture.Contracts.Setup(r => r.GetByIdAsync(contract.Id)).ReturnsAsync(contract);
        await fixture.Service.Invoking(s => s.UpdateAsync(contract.Id,
                new UpdateContractDto { RetentionClause = new string('x', 2001) }))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*2000*");
        fixture.Contracts.Verify(r => r.UpdateAsync(It.IsAny<Contract>()), Times.Never);
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
