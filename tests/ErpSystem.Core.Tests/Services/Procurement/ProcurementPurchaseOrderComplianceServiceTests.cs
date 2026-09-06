using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementPurchaseOrderComplianceServiceTests
{
    [Fact]
    public async Task BlockedEnforcementReturnsTenChecksAndRecordsImmutableAudit()
    {
        await using var fixture = new Fixture();

        var action = () => fixture.Service.EnforceAsync(
            fixture.PurchaseOrder,
            "Submit",
            "tdc0404-blocked");

        var exception = await action.Should()
            .ThrowAsync<ProcurementPurchaseOrderComplianceBlockedException>();
        exception.Which.Code.Should().Be("PO_COMPLIANCE_BLOCKED");
        exception.Which.Readiness.Checks.Should().HaveCount(10);
        exception.Which.Readiness.Checks.Select(item => item.Key).Should().Equal(
            "source",
            "supplier",
            "budget",
            "commitment",
            "evaluation",
            "award",
            "sod",
            "ghaneps",
            "contract",
            "signature");
        exception.Which.Readiness.DecisionKeys.Should().HaveCount(14)
            .And.StartWith("DEC-001")
            .And.EndWith("DEC-014");

        fixture.ControlEvents.Should().ContainSingle();
        var controlEvent = fixture.ControlEvents.Single();
        controlEvent.RuleCode.Should().Be("PO-003");
        controlEvent.RuleVersion.Should().Be("TDC-0404");
        controlEvent.Result.Should().Be(ProcurementControlEventResult.Denied);
        controlEvent.DecisionKeys.Should().HaveCount(14);
        controlEvent.SourceId.Should().Be(fixture.PurchaseOrder.Id);
        fixture.Notifications.Should().ContainSingle(item =>
            item.TopicKey == "procurement.purchase-order.compliance-blocked" &&
            item.EntityId == fixture.PurchaseOrder.Id);
    }

    [Fact]
    public async Task ForeignTenantPurchaseOrderIsRejectedBeforeAudit()
    {
        await using var fixture = new Fixture();
        fixture.PurchaseOrder.TenantId = Guid.NewGuid();

        var action = () => fixture.Service.EnforceAsync(
            fixture.PurchaseOrder,
            "Approve",
            "tdc0404-foreign");

        await action.Should()
            .ThrowAsync<ProcurementPurchaseOrderComplianceAuthorizationException>();
        fixture.ControlEvents.Should().BeEmpty();
        fixture.Notifications.Should().BeEmpty();
    }

    [Fact]
    public void BuildEvidenceCollapsesDuplicateResolvedReferences()
    {
        var referenceId = Guid.NewGuid();
        var checks = new[]
        {
            new ProcurementPurchaseOrderComplianceCheckDto
            {
                Key = "evaluation",
                Label = "Approved evaluation",
                Reference = " EVAL-001 "
            },
            new ProcurementPurchaseOrderComplianceCheckDto
            {
                Key = "award",
                Label = "Approved award",
                Reference = "eval-001"
            },
            new ProcurementPurchaseOrderComplianceCheckDto
            {
                Key = "source",
                Label = "Approved source",
                ReferenceId = referenceId
            },
            new ProcurementPurchaseOrderComplianceCheckDto
            {
                Key = "empty",
                Label = "No evidence"
            }
        };

        var evidence = ProcurementPurchaseOrderComplianceService
            .BuildEvidence(checks);

        evidence.Should().HaveCount(2);
        evidence.Select(item => item.Reference).Should().BeEquivalentTo(
            "EVAL-001",
            referenceId.ToString("D"));
        evidence.Should().OnlyHaveUniqueItems(item => new
        {
            item.ReferenceKind,
            Reference = item.Reference!.ToUpperInvariant()
        });
    }

    [Theory]
    [InlineData(ProcurementCategoryClass.Goods, "GOODS")]
    [InlineData(ProcurementCategoryClass.Works, "WORKS")]
    [InlineData(ProcurementCategoryClass.TechnicalServices, "SERVICES")]
    [InlineData(ProcurementCategoryClass.ConsultancyServices, "SERVICES")]
    [InlineData(ProcurementCategoryClass.GeneralServices, "SERVICES")]
    public void SupplierCategoryCodeUsesTheCanonicalOnboardingClassification(
        ProcurementCategoryClass category,
        string expectedCode)
    {
        ProcurementPurchaseOrderComplianceService.SupplierCategoryCode(category)
            .Should().Be(expectedCode);
    }

    [Fact]
    public void ReleaseOnlyRfqAwardIsRecognizedWithoutFabricatedAdvancedLineage()
    {
        var requisitionId = Guid.NewGuid();
        var releaseId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var integrityHash = new string('a', 64);
        var purchaseOrder = new PurchaseOrder
        {
            ProcurementSourceType = ProcurementPurchaseOrderSourceType.RfqAward,
            SourceRequisitionId = requisitionId,
            SourcingReleaseId = releaseId,
            SourcingCaseId = null,
            AwardReadinessDecisionId = null,
            BusinessPartnerId = supplierId,
            SourceIntegrityHash = integrityHash
        };
        var source = new ProcurementPurchaseOrderSourceResolution
        {
            SourceType = ProcurementPurchaseOrderSourceType.RfqAward,
            SourceId = Guid.NewGuid(),
            PurchaseRequisitionId = requisitionId,
            SourcingReleaseId = releaseId,
            SourcingCaseId = Guid.Empty,
            AwardReadinessDecisionId = Guid.Empty,
            BusinessPartnerId = supplierId,
            SourceIntegrityHash = integrityHash
        };

        ProcurementPurchaseOrderComplianceService.IsReleaseOnlyRfqAward(
                purchaseOrder,
                source)
            .Should().BeTrue();
    }

    [Fact]
    public async Task DraftPoPassesCommitmentCheckWhenActualExposureIsBudgetAvailable()
    {
        await using var fixture = new Fixture();
        fixture.PurchaseOrder.SourceRequisitionId = Guid.NewGuid();
        fixture.BudgetControl.Setup(item => item.GetDownstreamReadinessAsync(
                fixture.PurchaseOrder.SourceRequisitionId.Value,
                It.IsAny<decimal>(),
                fixture.PurchaseOrder.Currency,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PurchaseRequisitionBudgetReadinessDto
            {
                RequisitionId = fixture.PurchaseOrder.SourceRequisitionId.Value,
                BudgetId = Guid.NewGuid(),
                BudgetCode = "PB-TEST-001",
                BudgetStatus = "Approved",
                Currency = "GHS",
                RequestedAmount = fixture.PurchaseOrder.TotalAmount,
                AvailableAmount = 500m,
                IsCompliant = true,
                CanReserve = true,
                DecisionCode = "PR_BUDGET_AVAILABLE"
            });

        var action = () => fixture.Service.EnforceAsync(
            fixture.PurchaseOrder,
            "Submit",
            "trace-draft-budget");
        var exception = await action.Should()
            .ThrowAsync<ProcurementPurchaseOrderComplianceBlockedException>();
        var readiness = exception.Which.Readiness;

        var commitment = readiness.Checks.Single(item => item.Key == "commitment");
        commitment.Passed.Should().BeTrue();
        commitment.Required.Should().BeFalse();
        commitment.Message.Should().Contain("final PO approval");
    }

    [Fact]
    public async Task ApprovedPoRequiresTheFinalApprovalCommitment()
    {
        await using var fixture = new Fixture();
        fixture.PurchaseOrder.SourceRequisitionId = Guid.NewGuid();
        fixture.PurchaseOrder.Status = "Approved";
        fixture.BudgetControl.Setup(item => item.GetDownstreamReadinessAsync(
                fixture.PurchaseOrder.SourceRequisitionId.Value,
                It.IsAny<decimal>(),
                fixture.PurchaseOrder.Currency,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PurchaseRequisitionBudgetReadinessDto
            {
                RequisitionId = fixture.PurchaseOrder.SourceRequisitionId.Value,
                BudgetId = Guid.NewGuid(),
                BudgetCode = "PB-TEST-001",
                BudgetStatus = "Approved",
                Currency = "GHS",
                RequestedAmount = fixture.PurchaseOrder.TotalAmount,
                AvailableAmount = 500m,
                IsCompliant = true,
                CanReserve = true,
                DecisionCode = "PR_BUDGET_AVAILABLE"
            });

        var action = () => fixture.Service.EnforceAsync(
            fixture.PurchaseOrder,
            "Approve",
            "trace-approved-budget");
        var exception = await action.Should()
            .ThrowAsync<ProcurementPurchaseOrderComplianceBlockedException>();
        var readiness = exception.Which.Readiness;

        var commitment = readiness.Checks.Single(item => item.Key == "commitment");
        commitment.Passed.Should().BeFalse();
        commitment.Required.Should().BeTrue();
        commitment.Message.Should().Contain("no active reservation");
    }

    [Theory]
    [InlineData("Preview")]
    [InlineData("Submit")]
    [InlineData("Approve")]
    public async Task EnabledContractSettingBlocksAwardWithoutContract(string action)
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync(fixture.Setting(true), fixture.Award());

        var readiness = await fixture.ReadinessAsync(action);

        var contract = readiness.Checks.Single(item => item.Key == "contract");
        contract.Required.Should().BeTrue();
        contract.Passed.Should().BeFalse();
        contract.Message.Should().Contain("Require contract for PO");
        readiness.Checks.Single(item => item.Key == "signature").Passed.Should().BeFalse();
        fixture.ControlEvents.Should().BeEmpty("preview must not create audit or configuration records");

        if (action != "Preview")
        {
            var enforce = () => fixture.Service.EnforceAsync(fixture.PurchaseOrder, action, "contract-setting");
            var error = await enforce.Should().ThrowAsync<ProcurementPurchaseOrderComplianceBlockedException>();
            error.Which.Readiness.BlockedReasons.Should().Contain(item => item.Contains("Require contract for PO"));
        }
    }

    [Fact]
    public async Task DisabledContractSettingExplainsPolicyRatherThanClaimingAwardWaiver()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync(fixture.Setting(false), fixture.Award());

        var check = (await fixture.ReadinessAsync()).Checks.Single(item => item.Key == "contract");

        check.Passed.Should().BeTrue();
        check.Required.Should().BeFalse();
        check.Message.Should().Contain("Require contract for PO").And.Contain("off");
        check.Message.Should().NotContain("governed award does not require");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContractSettingUsesOnlyCurrentTenant(bool required)
    {
        await using var fixture = new Fixture();
        var foreign = fixture.Setting(!required);
        foreign.TenantId = Guid.NewGuid();
        await fixture.SeedAsync(foreign, fixture.Setting(required), fixture.Award());

        var check = (await fixture.ReadinessAsync()).Checks.Single(item => item.Key == "contract");

        check.Required.Should().Be(required);
        check.Passed.Should().Be(!required);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("foreign")]
    [InlineData("deleted")]
    public async Task AbsentTenantSettingRetainsDefaultWithoutWritingConfiguration(string scope)
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync(fixture.Award());
        if (scope != "missing")
        {
            var setting = fixture.Setting(true);
            setting.IsDeleted = scope == "deleted";
            if (scope == "foreign") setting.TenantId = Guid.NewGuid();
            await fixture.SeedAsync(setting);
        }
        var count = await fixture.Context.Set<ProcurementSettings>().CountAsync();

        var check = (await fixture.ReadinessAsync()).Checks.Single(item => item.Key == "contract");

        check.Required.Should().BeFalse();
        check.Message.Should().Contain("No tenant").And.NotContain("is off");
        (await fixture.Context.Set<ProcurementSettings>().CountAsync()).Should().Be(count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActiveSignedAwardContractPassesForEitherSetting(bool required)
    {
        await using var fixture = new Fixture();
        var award = fixture.Award();
        await fixture.SeedAsync(fixture.Setting(required), award, fixture.Contract(award.Id));

        var checks = (await fixture.ReadinessAsync()).Checks;

        checks.Single(item => item.Key == "contract").Passed.Should().BeTrue();
        checks.Single(item => item.Key == "contract").Required.Should().BeTrue();
        checks.Single(item => item.Key == "signature").Passed.Should().BeTrue();
    }

    [Theory]
    [InlineData("Draft", true, false, true)]
    [InlineData("Active", false, true, false)]
    [InlineData("Suspended", true, false, true)]
    public async Task DisabledSettingDoesNotBypassExistingContractControls(
        string status, bool signed, bool contractPasses, bool signaturePasses)
    {
        await using var fixture = new Fixture();
        var award = fixture.Award();
        var contract = fixture.Contract(award.Id);
        contract.Status = status;
        if (!signed) contract.ContractorSignedDate = null;
        await fixture.SeedAsync(fixture.Setting(false), award, contract);

        var checks = (await fixture.ReadinessAsync()).Checks;

        checks.Single(item => item.Key == "contract").Required.Should().BeTrue();
        checks.Single(item => item.Key == "contract").Passed.Should().Be(contractPasses);
        checks.Single(item => item.Key == "signature").Passed.Should().Be(signaturePasses);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("foreign")]
    [InlineData("deleted")]
    [InlineData("supplier")]
    [InlineData("award")]
    public async Task ExactLinkedContractCannotBeReplacedByAnotherValidAwardContract(string invalid)
    {
        await using var fixture = new Fixture();
        var award = fixture.Award();
        var exact = fixture.Contract(award.Id);
        fixture.PurchaseOrder.ContractId = exact.Id;
        if (invalid == "foreign") exact.TenantId = Guid.NewGuid();
        if (invalid == "deleted") exact.IsDeleted = true;
        if (invalid == "supplier") exact.BusinessPartnerId = Guid.NewGuid();
        if (invalid == "award") exact.TenderAwardId = Guid.NewGuid();
        await fixture.SeedAsync(fixture.Setting(false), award, fixture.Contract(award.Id));
        if (invalid != "missing") await fixture.SeedAsync(exact);

        var check = (await fixture.ReadinessAsync()).Checks.Single(item => item.Key == "contract");

        check.Required.Should().BeTrue();
        check.Passed.Should().BeFalse();
    }

    [Theory]
    [InlineData(ProcurementPurchaseOrderSourceType.Contract, false)]
    [InlineData(ProcurementPurchaseOrderSourceType.RfqAward, true)]
    [InlineData(ProcurementPurchaseOrderSourceType.ApprovedException, true)]
    public async Task ContractRequirementAlsoAppliesOutsideTenderAward(
        ProcurementPurchaseOrderSourceType sourceType, bool settingRequired)
    {
        await using var fixture = new Fixture();
        fixture.PurchaseOrder.ProcurementSourceType = sourceType;
        fixture.PurchaseOrder.ProcurementSourceId = Guid.NewGuid();
        await fixture.SeedAsync(fixture.Setting(settingRequired));

        var check = (await fixture.ReadinessAsync()).Checks.Single(item => item.Key == "contract");

        check.Required.Should().BeTrue();
        check.Passed.Should().BeFalse();
    }

    [Theory]
    [InlineData(ProcurementPurchaseOrderSourceType.Contract)]
    [InlineData(ProcurementPurchaseOrderSourceType.TenderAward)]
    [InlineData(ProcurementPurchaseOrderSourceType.RfqAward)]
    public async Task ExactActiveSignedContractSatisfiesConfiguredRequirement(
        ProcurementPurchaseOrderSourceType sourceType)
    {
        await using var fixture = new Fixture();
        var award = fixture.Award();
        var contract = fixture.Contract(award.Id);
        fixture.PurchaseOrder.ContractId = contract.Id;
        fixture.PurchaseOrder.ProcurementSourceType = sourceType;
        if (sourceType == ProcurementPurchaseOrderSourceType.Contract)
            fixture.PurchaseOrder.ProcurementSourceId = contract.Id;
        await fixture.SeedAsync(fixture.Setting(true), award, contract);

        var checks = (await fixture.ReadinessAsync()).Checks;

        checks.Single(item => item.Key == "contract").ReferenceId.Should().Be(contract.Id);
        checks.Single(item => item.Key == "contract").Passed.Should().BeTrue();
        checks.Single(item => item.Key == "signature").Passed.Should().BeTrue();
    }

    [Fact]
    public async Task ContractSourceWithoutReferenceCannotBecomeOptional()
    {
        await using var fixture = new Fixture();
        fixture.PurchaseOrder.ProcurementSourceType = ProcurementPurchaseOrderSourceType.Contract;
        await fixture.SeedAsync(fixture.Setting(false));

        var check = (await fixture.ReadinessAsync()).Checks.Single(item => item.Key == "contract");

        check.Required.Should().BeTrue();
        check.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task ContractSignedAwardStillRequiresContractWhenSettingIsOff()
    {
        await using var fixture = new Fixture();
        var award = fixture.Award();
        award.Status = "ContractSigned";
        await fixture.SeedAsync(fixture.Setting(false), award);

        var check = (await fixture.ReadinessAsync()).Checks.Single(item => item.Key == "contract");

        check.Required.Should().BeTrue();
        check.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task ContractSettingChangeIsReReadWithoutMutatingApprovedPurchaseOrder()
    {
        await using var fixture = new Fixture();
        var setting = fixture.Setting(false);
        fixture.PurchaseOrder.Status = "Approved";
        await fixture.SeedAsync(setting, fixture.Award());
        (await fixture.ReadinessAsync()).Checks.Single(item => item.Key == "contract").Required.Should().BeFalse();
        setting.RequireContractForPO = true;
        await fixture.Context.SaveChangesAsync();

        (await fixture.ReadinessAsync()).Checks.Single(item => item.Key == "contract").Passed.Should().BeFalse();
        fixture.PurchaseOrder.Status.Should().Be("Approved");
        fixture.PurchaseOrder.TotalAmount.Should().Be(100m);
        fixture.ControlEvents.Should().BeEmpty();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly UnitOfWork _unitOfWork;

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            UserId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            _context = new ApplicationDbContext(options);
            _unitOfWork = new UnitOfWork(_context);

            PurchaseOrder = new PurchaseOrder
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                OrderNumber = "PO-TDC0404-001",
                BusinessPartnerId = Guid.NewGuid(),
                Status = "Draft",
                Currency = "GHS",
                TotalAmount = 100m
            };

            var current = new Mock<ICurrentUserProvider>();
            current.SetupGet(item => item.IsAuthenticated).Returns(true);
            current.SetupGet(item => item.IsExternalUser).Returns(false);
            current.SetupGet(item => item.TenantId).Returns(TenantId);
            current.SetupGet(item => item.UserId).Returns(UserId);
            current.SetupGet(item => item.Username).Returns("officer@tdc.test");
            current.SetupGet(item => item.FullName).Returns("TDC Officer");
            current.SetupGet(item => item.Roles)
                .Returns(["TDC_PROCUREMENT_OFFICER"]);

            var access = new Mock<IProcurementAccessControlService>();
            access.Setup(item => item.EnforceCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
                {
                    Allowed = true,
                    Message = "Allowed"
                });

            var sources = new Mock<IProcurementPurchaseOrderSourceService>();
            sources.Setup(item => item.EvaluateCurrentAsync(
                    It.IsAny<PurchaseOrder>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ProcurementPurchaseOrderSourceValidationException(
                    "PO_SOURCE_REQUIRED",
                    "Approved source lineage is required."));

            BudgetControl = new Mock<IProcurementRequisitionBudgetControlService>();
            var suppliers = new Mock<ISupplierValidationService>();
            suppliers.Setup(item => item.EvaluateEligibilityAsync(
                    It.IsAny<SupplierEligibilityEvaluationRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SupplierValidationResult
                {
                    IsValid = false,
                    ValidationCode = "SUPPLIER_INELIGIBLE",
                    Errors = ["Supplier evidence is incomplete."],
                    BusinessPartnerId = PurchaseOrder.BusinessPartnerId,
                    TenantId = TenantId,
                    PartnerCode = "SUP-001",
                    PartnerName = "Controlled Supplier"
                });

            var ghaneps = new Mock<IProcurementGhanepsExchangeService>();
            var controlEvents = new Mock<IProcurementControlEventService>();
            controlEvents.Setup(item => item.RecordAsync(
                    It.IsAny<ProcurementControlEventWriteRequest>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ProcurementControlEventWriteRequest, CancellationToken>(
                    (request, _) => ControlEvents.Add(request))
                .ReturnsAsync(new ProcurementControlEventDto());

            var notifications = new Mock<INotificationTopicPublisher>();
            notifications.Setup(item => item.PublishAsync(
                    It.IsAny<NotificationTopicEvent>(),
                    It.IsAny<CancellationToken>()))
                .Callback<NotificationTopicEvent, CancellationToken>(
                    (request, _) => Notifications.Add(request))
                .Returns(Task.CompletedTask);

            Service = new ProcurementPurchaseOrderComplianceService(
                _unitOfWork,
                current.Object,
                access.Object,
                sources.Object,
                BudgetControl.Object,
                suppliers.Object,
                ghaneps.Object,
                controlEvents.Object,
                notifications.Object,
                NullLogger<ProcurementPurchaseOrderComplianceService>.Instance);
        }

        public Guid TenantId { get; }
        public Guid UserId { get; }
        public PurchaseOrder PurchaseOrder { get; }
        public Mock<IProcurementRequisitionBudgetControlService> BudgetControl { get; }
        public ProcurementPurchaseOrderComplianceService Service { get; }
        public List<ProcurementControlEventWriteRequest> ControlEvents { get; } = [];
        public List<NotificationTopicEvent> Notifications { get; } = [];
        public ApplicationDbContext Context => _context;

        public ProcurementSettings Setting(bool required) => new()
        {
            Id = Guid.NewGuid(), TenantId = TenantId, RequireContractForPO = required
        };

        public TenderAward Award()
        {
            var award = new TenderAward
            {
                Id = Guid.NewGuid(), TenantId = TenantId, Status = "Awarded",
                BusinessPartnerId = PurchaseOrder.BusinessPartnerId
            };
            PurchaseOrder.ProcurementSourceType = ProcurementPurchaseOrderSourceType.TenderAward;
            PurchaseOrder.ProcurementSourceId = award.Id;
            PurchaseOrder.TenderAwardId = award.Id;
            return award;
        }

        public Contract Contract(Guid awardId) => new()
        {
            Id = Guid.NewGuid(), TenantId = TenantId, TenderAwardId = awardId,
            BusinessPartnerId = PurchaseOrder.BusinessPartnerId, Status = "Active",
            ContractNumber = "CON-TEST-001", ContractTitle = "Test contract",
            SignedDate = DateTime.UtcNow, SignedById = UserId, SignedByName = "Organization signer",
            ContractorSignedDate = DateTime.UtcNow, ContractorSignatoryName = "Supplier signer",
            ContractDocumentPath = "test-only-signed-copy.pdf"
        };

        public async Task SeedAsync(params object[] entities)
        {
            _context.AddRange(entities);
            await _context.SaveChangesAsync();
        }

        public async Task<ProcurementPurchaseOrderComplianceDto> ReadinessAsync(string action = "Preview")
        {
            if (_context.Entry(PurchaseOrder).State == EntityState.Detached)
                _context.Add(PurchaseOrder);
            await _context.SaveChangesAsync();
            return await Service.GetReadinessAsync(PurchaseOrder.Id, action, "contract-setting-test");
        }

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
            _unitOfWork.Dispose();
        }
    }
}
