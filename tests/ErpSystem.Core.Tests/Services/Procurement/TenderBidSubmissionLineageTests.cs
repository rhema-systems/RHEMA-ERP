using System.Linq.Expressions;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class TenderBidSubmissionLineageTests
{
    [Fact]
    public void FreeTenderWithoutDeclarationIsReadyAfterAssignment()
    {
        var tender = new Tender { Id = Guid.NewGuid(), RequiresAcceptanceDeclaration = false };
        var partnerId = Guid.NewGuid();
        var status = TenderBidService.BuildInitiationStatus(
            tender,
            partnerId,
            new TenderAssignment
            {
                TenderId = tender.Id,
                BusinessPartnerId = partnerId,
                AssignmentType = "Self"
            },
            null,
            [],
            []);

        status.DeclarationSatisfied.Should().BeTrue();
        status.PaymentRequired.Should().BeFalse();
        status.PaymentSatisfied.Should().BeTrue();
        status.CanProceed.Should().BeTrue();
    }

    [Fact]
    public void RequiredDeclarationAndUnresolvedMandatoryFeeAllowSealedSubmission()
    {
        var tender = new Tender { Id = Guid.NewGuid(), RequiresAcceptanceDeclaration = true };
        var partnerId = Guid.NewGuid();
        var fee = new TenderFee
        {
            Id = Guid.NewGuid(),
            TenderId = tender.Id,
            FeeType = "DocumentFee",
            Amount = 100m,
            Currency = "GHS",
            PaymentMethod = "BankTransfer",
            IsMandatory = true
        };
        var bid = new TenderBid
        {
            Id = Guid.NewGuid(),
            TenderId = tender.Id,
            BusinessPartnerId = partnerId,
            Status = "Draft",
            AcceptedDeclaration = true
        };
        var status = TenderBidService.BuildInitiationStatus(
            tender,
            partnerId,
            new TenderAssignment
            {
                TenderId = tender.Id,
                BusinessPartnerId = partnerId,
                AssignmentType = "Self"
            },
            bid,
            [fee],
            [new TenderPayment
            {
                Id = Guid.NewGuid(),
                TenderFeeId = fee.Id,
                BusinessPartnerId = partnerId,
                Status = "Pending",
                PaymentReference = "PAY-PENDING",
                Amount = fee.Amount,
                Currency = fee.Currency,
                PaymentMethod = fee.PaymentMethod
            }]);

        status.DeclarationSatisfied.Should().BeTrue();
        status.PaymentRequired.Should().BeTrue();
        status.PaymentSatisfied.Should().BeFalse();
        status.PaymentEvidenceAccepted.Should().BeTrue();
        status.PaymentPendingVerification.Should().BeFalse();
        status.CanProceed.Should().BeTrue();
        status.Fees.Should().ContainSingle(item => item.Status == "Pending");
    }

    [Fact]
    public void UnpaidMandatoryFeeReturnsAmountAndRetainsSealedFilingPolicy()
    {
        var tenantId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var tender = new Tender
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RequiresAcceptanceDeclaration = false
        };
        var fee = new TenderFee
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderId = tender.Id,
            FeeType = "TenderFee",
            Amount = 500m,
            Currency = "GHS",
            PaymentMethod = "MobileMoney",
            IsMandatory = true
        };

        var status = TenderBidService.BuildInitiationStatus(
            tender,
            partnerId,
            ExactAssignment(tender, partnerId),
            null,
            [fee],
            []);

        status.PaymentRequired.Should().BeTrue();
        status.HasPayment.Should().BeFalse();
        status.PaymentSatisfied.Should().BeFalse();
        status.PaymentEvidenceAccepted.Should().BeTrue();
        status.CanProceed.Should().BeTrue();
        status.Fees.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new
            {
                TenderFeeId = fee.Id,
                FeeType = "TenderFee",
                Amount = 500m,
                Currency = "GHS",
                IsMandatory = true,
                Status = "NotPaid"
            });
    }

    [Fact]
    public void VerifiedMandatoryFeeForExactSupplierIsSatisfied()
    {
        var tenantId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var tender = new Tender { Id = Guid.NewGuid(), TenantId = tenantId };
        var fee = new TenderFee
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderId = tender.Id,
            FeeType = "TenderFee",
            Amount = 500m,
            Currency = "GHS",
            IsMandatory = true
        };
        var payment = new TenderPayment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderFeeId = fee.Id,
            BusinessPartnerId = partnerId,
            Status = "Verified",
            Amount = fee.Amount,
            Currency = fee.Currency
        };

        var status = TenderBidService.BuildInitiationStatus(
            tender,
            partnerId,
            ExactAssignment(tender, partnerId),
            null,
            [fee],
            [payment]);

        status.PaymentRequired.Should().BeTrue();
        status.HasPayment.Should().BeTrue();
        status.PaymentSatisfied.Should().BeTrue();
        status.CanProceed.Should().BeTrue();
        status.Fees.Should().ContainSingle(item =>
            item.Status == "Verified" && item.PaymentId == payment.Id);
    }

    [Fact]
    public void InitiationIgnoresWrongTenderAssignmentsAndAnotherSuppliersPayment()
    {
        var tenantId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var tender = new Tender { Id = Guid.NewGuid(), TenantId = tenantId };
        var fee = new TenderFee
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderId = tender.Id,
            Amount = 500m,
            Currency = "GHS",
            IsMandatory = true
        };
        var otherSupplierPayment = new TenderPayment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenderFeeId = fee.Id,
            BusinessPartnerId = Guid.NewGuid(),
            Status = "Verified",
            Amount = fee.Amount,
            Currency = fee.Currency
        };
        var wrongTenderAssignment = ExactAssignment(tender, partnerId);
        wrongTenderAssignment.TenderId = Guid.NewGuid();

        var status = TenderBidService.BuildInitiationStatus(
            tender,
            partnerId,
            wrongTenderAssignment,
            null,
            [fee],
            [otherSupplierPayment]);

        status.HasAssignment.Should().BeFalse();
        status.AssignmentType.Should().BeNull();
        status.PaymentRequired.Should().BeTrue();
        status.HasPayment.Should().BeFalse();
        status.PaymentSatisfied.Should().BeFalse();
        status.CanProceed.Should().BeFalse();
        status.Fees.Should().ContainSingle(item =>
            item.Status == "NotPaid" && item.PaymentId == null);
    }

    [Fact]
    public void InitiationIgnoresAssignmentForAnotherBusinessPartner()
    {
        var tenantId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var tender = new Tender { Id = Guid.NewGuid(), TenantId = tenantId };
        var otherSupplierAssignment = ExactAssignment(tender, Guid.NewGuid());

        var status = TenderBidService.BuildInitiationStatus(
            tender,
            partnerId,
            otherSupplierAssignment,
            null,
            [],
            []);

        status.HasAssignment.Should().BeFalse();
        status.PaymentRequired.Should().BeFalse();
        status.PaymentSatisfied.Should().BeTrue();
        status.CanProceed.Should().BeFalse();
    }

    [Fact]
    public async Task ReleaseOnlyTenderBidSubmitsWithoutAdvancedDocumentGuard()
    {
        var fixture = new Fixture(advancedSourcingCase: false);

        var result = await fixture.Service.SubmitBidAsync(
            fixture.Bid.Id,
            new SubmitTenderBidDto());

        result.Status.Should().Be("Submitted");
        fixture.Bid.Status.Should().Be("Submitted");
        fixture.DocumentControl.Verify(service => service.EnsureSubmissionReadyAsync(
                It.IsAny<ProcurementTenderDocumentSourceType>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        fixture.TenderControl.Verify(service => service.RecordSubmissionAsync(
                fixture.Bid,
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.Bids.Verify(repository => repository.UpdateAsync(fixture.Bid), Times.Once);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdvancedTenderBidStillRequiresControlledDocumentSubmissionReadiness()
    {
        var fixture = new Fixture(advancedSourcingCase: true);
        fixture.DocumentControl.Setup(service => service.EnsureSubmissionReadyAsync(
                ProcurementTenderDocumentSourceType.Tender,
                fixture.Tender.Id,
                fixture.Bid.BusinessPartnerId,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementTenderDocumentControlValidationException(
                "TENDER_DOCUMENT_SOURCING_CASE_REQUIRED",
                "The procurement source must be linked to its locked sourcing case."));

        var action = () => fixture.Service.SubmitBidAsync(
            fixture.Bid.Id,
            new SubmitTenderBidDto());

        var error = await action.Should()
            .ThrowAsync<ProcurementTenderDocumentControlValidationException>();
        error.Which.Code.Should().Be("TENDER_DOCUMENT_SOURCING_CASE_REQUIRED");
        fixture.Bid.Status.Should().Be("Draft");
        fixture.DocumentControl.Verify(service => service.EnsureSubmissionReadyAsync(
                ProcurementTenderDocumentSourceType.Tender,
                fixture.Tender.Id,
                fixture.Bid.BusinessPartnerId,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.TenderControl.Verify(service => service.RecordSubmissionAsync(
                It.IsAny<TenderBid>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        fixture.Bids.Verify(repository => repository.UpdateAsync(It.IsAny<TenderBid>()), Times.Never);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TenderWithoutAdvancedCaseOrReleaseLineageCannotSubmit()
    {
        var fixture = new Fixture(advancedSourcingCase: false);
        fixture.Tender.SourcingReleaseId = null;

        var action = () => fixture.Service.SubmitBidAsync(
            fixture.Bid.Id,
            new SubmitTenderBidDto());

        var error = await action.Should()
            .ThrowAsync<ProcurementRequisitionSourcingValidationException>();
        error.Which.Code.Should().Be("TENDER_SOURCE_LINEAGE_REQUIRED");
        fixture.Bid.Status.Should().Be("Draft");
        fixture.DocumentControl.Verify(service => service.EnsureSubmissionReadyAsync(
                It.IsAny<ProcurementTenderDocumentSourceType>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        fixture.TenderControl.Verify(service => service.RecordSubmissionAsync(
                It.IsAny<TenderBid>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        fixture.Bids.Verify(repository => repository.UpdateAsync(It.IsAny<TenderBid>()), Times.Never);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequiredDeclarationBlocksSubmissionUntilAccepted()
    {
        var fixture = new Fixture(advancedSourcingCase: false);
        fixture.Tender.RequiresAcceptanceDeclaration = true;

        var action = () => fixture.Service.SubmitBidAsync(fixture.Bid.Id, new SubmitTenderBidDto());

        var error = await action.Should().ThrowAsync<TenderBidInitiationValidationException>();
        error.Which.Code.Should().Be("TENDER_BID_DECLARATION_REQUIRED");
        fixture.Bid.Status.Should().Be("Draft");
        fixture.Bids.Verify(repository => repository.UpdateAsync(It.IsAny<TenderBid>()), Times.Never);
    }

    [Fact]
    public async Task PositiveMandatoryFeeWithoutPaymentAllowsSealedSubmission()
    {
        var fixture = new Fixture(advancedSourcingCase: false);
        var fee = fixture.AddMandatoryFee(100m);
        var decision = TenderBidPaymentRules.Assess(fixture.Bid, [fee], []);

        var result = await fixture.Service.SubmitBidAsync(fixture.Bid.Id, new SubmitTenderBidDto());

        result.Status.Should().Be("Submitted");
        decision.CanSubmitSealed.Should().BeTrue();
        decision.CanOpenOrEvaluate.Should().BeFalse();
        decision.Code.Should().Be("TENDER_BID_PAYMENT_EVIDENCE_REQUIRED");
    }

    [Fact]
    public async Task ZeroMandatoryFeeDoesNotCreateAPaymentGate()
    {
        var fixture = new Fixture(advancedSourcingCase: false);
        fixture.AddMandatoryFee(0m);

        var result = await fixture.Service.SubmitBidAsync(fixture.Bid.Id, new SubmitTenderBidDto());

        result.Status.Should().Be("Submitted");
    }

    [Fact]
    public async Task ManualPaymentEvidenceAllowsSealedSubmissionWhileVerificationRemainsPending()
    {
        var fixture = new Fixture(advancedSourcingCase: false);
        var fee = fixture.AddMandatoryFee(100m);
        fixture.Payments.Setup(repository => repository.GetByBusinessPartnerIdAsync(fixture.Bid.BusinessPartnerId))
            .ReturnsAsync([new TenderPayment
            {
                Id = Guid.NewGuid(), TenantId = fixture.Bid.TenantId,
                TenderFeeId = fee.Id, BusinessPartnerId = fixture.Bid.BusinessPartnerId,
                Status = "Pending", PaymentReference = "PAY-MANUAL-PENDING",
                TransactionId = "BANK-RECEIPT-001", PaymentProof = "BANK-RECEIPT-001",
                Amount = fee.Amount, Currency = fee.Currency, PaymentMethod = fee.PaymentMethod
            }]);

        var result = await fixture.Service.SubmitBidAsync(fixture.Bid.Id, new SubmitTenderBidDto());
        var decision = TenderBidPaymentRules.Assess(
            fixture.Bid,
            [fee],
            await fixture.Payments.Object.GetByBusinessPartnerIdAsync(fixture.Bid.BusinessPartnerId));

        result.Status.Should().Be("Submitted");
        decision.CanSubmitSealed.Should().BeTrue();
        decision.CanOpenOrEvaluate.Should().BeFalse();
        decision.Code.Should().Be("TENDER_BID_PAYMENT_VERIFICATION_PENDING");
    }

    [Fact]
    public async Task OnlinePaymentPendingProviderConfirmationAllowsSealedSubmission()
    {
        var fixture = new Fixture(advancedSourcingCase: false);
        var fee = fixture.AddMandatoryFee(100m);
        fee.PaymentMethod = "Online";
        fixture.Payments.Setup(repository => repository.GetByBusinessPartnerIdAsync(fixture.Bid.BusinessPartnerId))
            .ReturnsAsync([new TenderPayment
            {
                Id = Guid.NewGuid(), TenantId = fixture.Bid.TenantId,
                TenderFeeId = fee.Id, BusinessPartnerId = fixture.Bid.BusinessPartnerId,
                Status = "Pending", PaymentReference = "PROVIDER-PENDING",
                TransactionId = "PROVIDER-001", Amount = fee.Amount,
                Currency = fee.Currency, PaymentMethod = fee.PaymentMethod
            }]);

        var result = await fixture.Service.SubmitBidAsync(fixture.Bid.Id, new SubmitTenderBidDto());
        var decision = TenderBidPaymentRules.Assess(
            fixture.Bid,
            [fee],
            await fixture.Payments.Object.GetByBusinessPartnerIdAsync(fixture.Bid.BusinessPartnerId));

        result.Status.Should().Be("Submitted");
        decision.CanSubmitSealed.Should().BeTrue();
        decision.CanOpenOrEvaluate.Should().BeFalse();
        decision.Code.Should().Be("TENDER_BID_PAYMENT_PROVIDER_PENDING");
    }

    [Fact]
    public async Task RejectedMandatoryPaymentAllowsSealedSubmissionButRemainsUnresolved()
    {
        var fixture = new Fixture(advancedSourcingCase: false);
        var fee = fixture.AddMandatoryFee(100m);
        fixture.Payments.Setup(repository => repository.GetByBusinessPartnerIdAsync(fixture.Bid.BusinessPartnerId))
            .ReturnsAsync([new TenderPayment
            {
                Id = Guid.NewGuid(), TenantId = fixture.Bid.TenantId,
                TenderFeeId = fee.Id, BusinessPartnerId = fixture.Bid.BusinessPartnerId,
                Status = "Rejected", PaymentReference = "PAY-REJECTED",
                TransactionId = "BANK-RECEIPT-REJECTED", Amount = fee.Amount,
                Currency = fee.Currency, PaymentMethod = fee.PaymentMethod
            }]);

        var decision = TenderBidPaymentRules.Assess(
            fixture.Bid,
            [fee],
            await fixture.Payments.Object.GetByBusinessPartnerIdAsync(fixture.Bid.BusinessPartnerId));
        var result = await fixture.Service.SubmitBidAsync(fixture.Bid.Id, new SubmitTenderBidDto());

        result.Status.Should().Be("Submitted");
        decision.CanSubmitSealed.Should().BeTrue();
        decision.CanOpenOrEvaluate.Should().BeFalse();
        decision.PendingVerification.Should().BeFalse();
        decision.Code.Should().Be("TENDER_BID_PAYMENT_REJECTED");
    }

    [Fact]
    public async Task PaymentVerificationRejectsRouteFromAnotherBidSupplier()
    {
        var fixture = new Fixture(advancedSourcingCase: false);
        var fee = fixture.AddMandatoryFee(100m);
        var payment = new TenderPayment
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.Bid.TenantId,
            TenderFeeId = fee.Id,
            BusinessPartnerId = Guid.NewGuid(),
            Status = "Pending",
            PaymentReference = "PAY-WRONG-SUPPLIER"
        };
        fixture.Payments.Setup(repository => repository.GetByIdAsync(payment.Id))
            .ReturnsAsync(payment);

        var action = () => fixture.Service.VerifyPaymentAsync(
            fixture.Bid.Id, payment.Id, new VerifyPaymentDto { IsApproved = true });

        await action.Should().ThrowAsync<TenderBidInitiationValidationException>()
            .Where(exception => exception.Code == "TENDER_BID_PAYMENT_ROUTE_MISMATCH");
        fixture.Payments.Verify(
            repository => repository.UpdateAsync(It.IsAny<TenderPayment>()), Times.Never);
    }

    [Fact]
    public async Task PaymentVerificationIsScopedAndSameDecisionRetryIsIdempotent()
    {
        var fixture = new Fixture(advancedSourcingCase: false);
        var fee = fixture.AddMandatoryFee(100m);
        var payment = new TenderPayment
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.Bid.TenantId,
            TenderFeeId = fee.Id,
            BusinessPartnerId = fixture.Bid.BusinessPartnerId,
            Status = "Pending",
            PaymentReference = "PAY-PENDING",
            Amount = fee.Amount,
            Currency = fee.Currency,
            PaymentMethod = fee.PaymentMethod
        };
        fixture.Payments.Setup(repository => repository.GetByIdAsync(payment.Id))
            .ReturnsAsync(payment);
        using var finance = fixture.ConfigurePaymentPosting(fee, payment);

        var first = await fixture.Service.VerifyPaymentAsync(
            fixture.Bid.Id, payment.Id, new VerifyPaymentDto { IsApproved = true });
        var retry = await fixture.Service.VerifyPaymentAsync(
            fixture.Bid.Id, payment.Id, new VerifyPaymentDto { IsApproved = true });

        first.Status.Should().Be("Verified");
        retry.Status.Should().Be("Verified");
        fixture.Payments.Verify(repository => repository.UpdateAsync(payment), Times.Once);
        payment.PostingEventId.Should().NotBeNull();
        payment.JournalEntryId.Should().NotBeNull();
        payment.PostedAtUtc.Should().NotBeNull();
        fixture.FinancePosting.Verify(engine => engine.PostAsync(
            It.IsAny<FinancePostingRequestV2Dto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void LegacyOpeningRequiresClosedTenderAndElapsedSchedule()
    {
        var now = DateTime.UtcNow;
        var tender = new Tender
        {
            Status = "Published",
            SubmissionDeadline = now.AddMinutes(-1)
        };

        Action beforeClose = () => TenderBidService.EnsureLegacyOpeningReady(tender, now);
        beforeClose.Should().Throw<InvalidOperationException>().WithMessage("*closed*");

        tender.Status = "Closed";
        tender.OpeningDate = now.AddMinutes(1);
        Action beforeSchedule = () => TenderBidService.EnsureLegacyOpeningReady(tender, now);
        beforeSchedule.Should().Throw<InvalidOperationException>().WithMessage("*scheduled opening*");

        tender.OpeningDate = now;
        TenderBidService.EnsureLegacyOpeningReady(tender, now);
    }

    [Fact]
    public async Task BidSummaryUsesVerifiedPaymentFromExactTenderAndTenant()
    {
        var fixture = new Fixture(advancedSourcingCase: false);
        fixture.Bid.OpenedDate = DateTime.UtcNow;
        var fee = fixture.AddMandatoryFee(100m);
        fixture.Bids.Setup(repository => repository.GetBidsAsync(
                1, 10, null, null, fixture.Tender.Id))
            .ReturnsAsync(new ErpSystem.Core.DTOs.Common.PagedResult<TenderBid>
            {
                Items = [fixture.Bid], TotalCount = 1, Page = 1, PageSize = 10
            });
        fixture.Payments.Setup(repository => repository.GetByBusinessPartnerIdAsync(
                fixture.Bid.BusinessPartnerId))
            .ReturnsAsync(
            [
                new TenderPayment
                {
                    Id = Guid.NewGuid(),
                    TenantId = fixture.Bid.TenantId,
                    TenderFeeId = Guid.NewGuid(),
                    BusinessPartnerId = fixture.Bid.BusinessPartnerId,
                    Status = "Completed",
                    PaymentReference = "OTHER-TENDER"
                },
                new TenderPayment
                {
                    Id = Guid.NewGuid(),
                    TenantId = fixture.Bid.TenantId,
                    TenderFeeId = fee.Id,
                    BusinessPartnerId = fixture.Bid.BusinessPartnerId,
                    Status = "Verified",
                    PaymentReference = "THIS-TENDER",
                    VerifiedDate = DateTime.UtcNow
                }
            ]);

        var result = await fixture.Service.GetBidsAsync(
            1, 10, null, null, fixture.Tender.Id);

        var summary = result.Items.Should().ContainSingle().Subject;
        summary.HasPaidFees.Should().BeTrue();
        summary.PaymentStatus.Should().Be("Verified");
        fixture.Bids.Verify(repository => repository.GetBidsAsync(
            1, 10, null, null, fixture.Tender.Id), Times.Once);
    }

    private static TenderAssignment ExactAssignment(Tender tender, Guid businessPartnerId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tender.TenantId,
        TenderId = tender.Id,
        BusinessPartnerId = businessPartnerId,
        AssignmentType = "AllUsers"
    };

    [Theory]
    [InlineData("SUPPLIER_BLACKLISTED")]
    [InlineData("SUPPLIER_NOT_APPROVED")]
    [InlineData("SUPPLIER_EVIDENCE_NOT_READY")]
    [InlineData("TENDER_BID_SOURCE_LINEAGE_INVALID")]
    public async Task SubmissionRechecksEligibilityBeforeWritingOrAdmittingSealedBid(string code)
    {
        var fixture = new Fixture(advancedSourcingCase: true);
        fixture.SupplierValidation.Setup(service => service.ValidateForTenderBidAsync(
                fixture.Bid.BusinessPartnerId, fixture.Tender.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SupplierValidationResult.Fail("Current eligibility was revoked.", code));

        var submit = () => fixture.Service.SubmitBidAsync(fixture.Bid.Id, new SubmitTenderBidDto());

        await submit.Should().ThrowAsync<TenderBidInitiationValidationException>()
            .Where(exception => exception.Code == code);
        fixture.Bid.Status.Should().Be("Draft");
        fixture.DocumentControl.Verify(service => service.EnsureSubmissionReadyAsync(
            It.IsAny<ProcurementTenderDocumentSourceType>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Bids.Verify(repository => repository.UpdateAsync(It.IsAny<TenderBid>()), Times.Never);
        fixture.UnitOfWork.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Fixture
    {
        public Mock<ITenderBidRepository> Bids { get; } = new();
        public Mock<ITenderPaymentRepository> Payments { get; } = new();
        public Mock<ITenderFeeRepository> Fees { get; } = new();
        public Mock<ITenderAssignmentRepository> Assignments { get; } = new();
        public Mock<IProcurementTenderControlService> TenderControl { get; } = new();
        public Mock<IProcurementTenderDocumentControlService> DocumentControl { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<ISupplierValidationService> SupplierValidation { get; } = new();
        public Mock<IFinancePostingEngine> FinancePosting { get; } = new();
        public Tender Tender { get; }
        public TenderBid Bid { get; }
        public TenderBidService Service { get; }

        public ApplicationDbContext ConfigurePaymentPosting(TenderFee fee, TenderPayment payment)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"tender-submission-payment-{Guid.NewGuid():N}")
                .Options;
            var db = new ApplicationDbContext(options, Bid.TenantId);
            var receivingAccount = new Account
            {
                Id = Guid.NewGuid(),
                TenantId = Bid.TenantId,
                AccountCode = "100-1001-0000",
                AccountNumber = "100-1001-0000",
                AccountName = "Tender fee receipts",
                AccountType = AccountType.Asset,
                Status = AccountStatus.Active,
                CurrencyCode = "GHS",
                AllowDirectPosting = true
            };
            var revenueAccount = new Account
            {
                Id = Guid.NewGuid(),
                TenantId = Bid.TenantId,
                AccountCode = "000-4300-0000",
                AccountNumber = "000-4300-0000",
                AccountName = "Tender fee income",
                AccountType = AccountType.Revenue,
                Status = AccountStatus.Active,
                CurrencyCode = "GHS",
                AllowDirectPosting = true
            };
            db.Accounts.AddRange(receivingAccount, revenueAccount);
            db.FinanceSettings.Add(new FinanceSettings
            {
                Id = Guid.NewGuid(),
                TenantId = Bid.TenantId,
                BaseCurrency = "GHS"
            });
            db.SaveChanges();
            fee.ReceivingAccountId = receivingAccount.Id;
            fee.RevenueAccountId = revenueAccount.Id;

            var accounts = new Mock<IGenericRepository<Account>>();
            accounts.Setup(repository => repository.GetQueryable(
                    It.IsAny<Expression<Func<Account, bool>>>()))
                .Returns((Expression<Func<Account, bool>> predicate) => db.Accounts.Where(predicate));
            var settings = new Mock<IGenericRepository<FinanceSettings>>();
            settings.Setup(repository => repository.GetQueryable(
                    It.IsAny<Expression<Func<FinanceSettings, bool>>>()))
                .Returns((Expression<Func<FinanceSettings, bool>> predicate) => db.FinanceSettings.Where(predicate));
            UnitOfWork.Setup(unit => unit.Repository<Account>()).Returns(accounts.Object);
            UnitOfWork.Setup(unit => unit.Repository<FinanceSettings>()).Returns(settings.Object);
            FinancePosting.Setup(engine => engine.PostAsync(
                    It.IsAny<FinancePostingRequestV2Dto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FinancePostingResultDto
                {
                    PostingEventId = Guid.NewGuid(),
                    JournalEntryId = Guid.NewGuid(),
                    JournalEntryNumber = "JE-SUBMISSION-TEST",
                    PostingStatus = "Posted",
                    FunctionalCurrencyCode = "GHS",
                    TotalDebitAmount = payment.Amount,
                    TotalCreditAmount = payment.Amount,
                    SourceModule = "Procurement",
                    OriginModuleCode = "PROC",
                    SourceDocumentType = "TenderFeePayment",
                    SourceDocumentId = payment.Id,
                    PostingAction = "Post"
                });
            return db;
        }

        public TenderFee AddMandatoryFee(decimal amount)
        {
            var fee = new TenderFee
            {
                Id = Guid.NewGuid(),
                TenantId = Tender.TenantId,
                TenderId = Tender.Id,
                FeeType = "DocumentFee",
                Amount = amount,
                Currency = "GHS",
                PaymentMethod = "BankTransfer",
                IsMandatory = true
            };
            Fees.Setup(repository => repository.GetByTenderIdAsync(Tender.Id))
                .ReturnsAsync([fee]);
            Fees.Setup(repository => repository.GetByIdAsync(fee.Id))
                .ReturnsAsync(fee);
            return fee;
        }

        public Fixture(bool advancedSourcingCase)
        {
            var tenantId = Guid.NewGuid();
            var tenderId = Guid.NewGuid();
            var partnerId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            Tender = new Tender
            {
                Id = tenderId,
                TenantId = tenantId,
                TenderNumber = "TND-SUBMISSION-TEST",
                Title = "Submission lineage test",
                Status = "Published",
                Currency = "GHS",
                SubmissionDeadline = DateTime.UtcNow.AddDays(1),
                SourcePurchaseRequisitionId = Guid.NewGuid(),
                SourcingReleaseId = Guid.NewGuid(),
                SourcingCaseId = advancedSourcingCase ? Guid.NewGuid() : null
            };
            Bid = new TenderBid
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TenderId = tenderId,
                BusinessPartnerId = partnerId,
                BidNumber = "BID-SUBMISSION-TEST",
                Status = "Draft",
                Currency = "GHS",
                TotalBidAmount = 100m,
                CreatedAt = DateTime.UtcNow
            };
            var bidItem = new TenderBidItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TenderBidId = Bid.Id,
                TenderItemId = itemId,
                OfferedQuantity = 1m,
                UnitPrice = 100m,
                TotalPrice = 100m,
                CreatedAt = DateTime.UtcNow
            };

            var tenders = new Mock<ITenderRepository>();
            var bidItems = new Mock<ITenderBidItemRepository>();
            var bidDocuments = new Mock<ITenderBidDocumentRepository>();
            var notification = new Mock<ITenderNotificationService>();
            var currentUser = new Mock<ICurrentUserProvider>();
            var events = new Mock<IAppEventBus>();
            var exceptionalSourcing = new Mock<IProcurementExceptionalSourcingControlService>();
            var quantitySurvey = new Mock<IQuantitySurveyTenderBoqSubmissionService>();
            var assignment = new TenderAssignment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TenderId = Tender.Id,
                BusinessPartnerId = Bid.BusinessPartnerId,
                AssignmentType = "Self"
            };

            Bids.Setup(repository => repository.GetByIdAsync(Bid.Id)).ReturnsAsync(Bid);
            Bids.Setup(repository => repository.UpdateAsync(It.IsAny<TenderBid>()))
                .ReturnsAsync((TenderBid entity) => entity);
            tenders.Setup(repository => repository.GetByIdAsync(Tender.Id)).ReturnsAsync(Tender);
            bidItems.Setup(repository => repository.GetByBidIdAsync(Bid.Id))
                .ReturnsAsync([bidItem]);
            bidDocuments.Setup(repository => repository.GetByBidIdAsync(Bid.Id))
                .ReturnsAsync([]);
            Assignments.Setup(repository => repository.GetByTenderAndBusinessPartnerAsync(
                    Tender.Id, Bid.BusinessPartnerId))
                .ReturnsAsync([assignment]);
            Fees.Setup(repository => repository.GetByTenderIdAsync(Tender.Id))
                .ReturnsAsync([]);
            Payments.Setup(repository => repository.GetByBusinessPartnerIdAsync(Bid.BusinessPartnerId))
                .ReturnsAsync([]);
            TenderControl.Setup(service => service.IsControlledTenderMethodAsync(
                    Tender.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            TenderControl.Setup(service => service.RecordSubmissionAsync(
                    Bid, It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcurementTenderSubmissionDisposition?)null);
            exceptionalSourcing.Setup(service => service.EnsureBidSupplierAllowedAsync(
                    Tender.Id, Bid.BusinessPartnerId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            quantitySurvey.Setup(service => service.EnsureReadyForTenderSubmissionAsync(
                    Bid.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            notification.Setup(service => service.SendBidSubmittedNotificationAsync(Bid.Id))
                .Returns(Task.CompletedTask);
            events.Setup(bus => bus.PublishAsync(
                    It.IsAny<EntityActivityEvent>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            UnitOfWork.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(1);
            UnitOfWork.Setup(unit => unit.ExecuteInStrategyAsync(
                    It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
                .Returns((Func<Task> operation, CancellationToken _) => operation());
            UnitOfWork.Setup(unit => unit.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            UnitOfWork.Setup(unit => unit.AcquireTransactionLockAsync(
                    It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            UnitOfWork.Setup(unit => unit.CommitAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            UnitOfWork.Setup(unit => unit.RollbackAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            currentUser.SetupGet(provider => provider.TenantId).Returns(tenantId);
            currentUser.SetupGet(provider => provider.UserId).Returns(Guid.NewGuid());
            SupplierValidation.Setup(service => service.ValidateForTenderBidAsync(
                    Bid.BusinessPartnerId, Tender.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SupplierValidationResult { IsValid = true });

            Service = new TenderBidService(
                Bids.Object,
                tenders.Object,
                bidItems.Object,
                bidDocuments.Object,
                Payments.Object,
                Fees.Object,
                Mock.Of<ITenderInterviewRepository>(),
                Assignments.Object,
                Mock.Of<ITenderBidLotRepository>(),
                notification.Object,
                Mock.Of<IBusinessPartnerRepository>(),
                Mock.Of<IBusinessPartnerUserRepository>(),
                SupplierValidation.Object,
                UnitOfWork.Object,
                currentUser.Object,
                events.Object,
                TenderControl.Object,
                DocumentControl.Object,
                exceptionalSourcing.Object,
                quantitySurvey.Object,
                Mock.Of<ILogger<TenderBidService>>(),
                FinancePosting.Object);
        }
    }
}
