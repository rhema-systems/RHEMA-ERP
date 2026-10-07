using ErpSystem.Api.Authorization;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Api.Services.Finance.MultiCurrency;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceExchangeRateOverrideTests
{
    [Fact]
    public async Task Request_ShouldRequireMeaningfulReasonBeforeReadingTransaction()
    {
        await using var db = CreateDb();
        var service = CreateService(db, Guid.NewGuid(), Guid.NewGuid());

        var action = () => service.RequestAsync(new FinanceExchangeRateOverrideCommandDto
        {
            SourceDocumentType = "VendorInvoice",
            SourceDocumentId = Guid.NewGuid(),
            TransactionCurrencyCode = "USD",
            GovernedExchangeRateId = Guid.NewGuid(),
            RequestedRate = 12.345678m,
            Reason = "too short"
        });

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*at least 10 characters*");
    }

    [Fact]
    public async Task WorkflowOutcome_ShouldRejectMakerAsChecker()
    {
        var tenantId = Guid.NewGuid();
        var makerId = Guid.NewGuid();
        await using var db = CreateDb();
        var request = new FinanceExchangeRateOverrideRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceDocumentType = "VendorInvoice",
            SourceDocumentId = Guid.NewGuid(),
            TransactionCurrencyCode = "USD",
            FunctionalCurrencyCode = "GHS",
            GovernedExchangeRateId = Guid.NewGuid(),
            GovernedRate = 12m,
            GovernedRateSource = "Daily rate",
            GovernedRateEffectiveDate = DateTime.UtcNow.Date,
            GovernedRateType = "Spot",
            GovernedQuoteSide = "Mid",
            RequestedRate = 12.5m,
            Reason = "Documented treasury exception",
            SourceSnapshotHash = new string('A', 64),
            Status = FinanceExchangeRateOverrideStatuses.PendingApproval,
            WorkflowInstanceId = Guid.NewGuid(),
            RequestedByUserId = makerId,
            RequestedAtUtc = DateTime.UtcNow
        };
        db.FinanceExchangeRateOverrideRequests.Add(request);
        await db.SaveChangesAsync();
        var service = CreateService(db, tenantId, makerId);

        var action = () => service.ApplyWorkflowOutcomeAsync(request.Id, true, makerId, null);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot approve or reject their own request*");
    }

    [Fact]
    public async Task Posting_ShouldRejectCallerSuppliedOverrideAuthorityForSupportedTransaction()
    {
        await using var db = CreateDb();
        var service = CreateService(db, Guid.NewGuid(), Guid.NewGuid());
        var posting = new FinancePostingRequestV2Dto
        {
            SourceDocumentType = "VendorInvoice",
            SourceDocumentId = Guid.NewGuid(),
            ExchangeRateOverrideRequestId = Guid.NewGuid()
        };

        var action = () => service.ApplyApprovedOverridesForPostingAsync(Guid.NewGuid(), posting);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be supplied by the caller*");
    }

    [Fact]
    public async Task Posting_ShouldLeaveUnrelatedHistoricalCorrectionEvidenceAlone()
    {
        await using var db = CreateDb();
        var service = CreateService(db, Guid.NewGuid(), Guid.NewGuid());
        var posting = new FinancePostingRequestV2Dto
        {
            SourceDocumentType = "SupplierDebitNote",
            SourceDocumentId = Guid.NewGuid(),
            ExchangeRateOverrideApprovedByUserId = Guid.NewGuid(),
            ExchangeRateOverrideApprovedAt = DateTime.UtcNow
        };

        var result = await service.ApplyApprovedOverridesForPostingAsync(Guid.NewGuid(), posting);

        result.Should().BeEmpty();
    }

    [Fact]
    public void CreateEndpoint_ShouldRequireDedicatedRequesterPermission()
    {
        var method = typeof(FinanceExchangeRateOverridesController)
            .GetMethod(nameof(FinanceExchangeRateOverridesController.Create));

        method.Should().NotBeNull();
        method!.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .Select(attribute => attribute.Policy)
            .Should().Contain(FinancePermissions.RequestTransactionExchangeRateOverride);

        FinancePermissionPolicyMap.GetRequiredPolicies(
                "FinanceExchangeRateOverrides", "Create", ["POST"], ["contract"])
            .Should().Equal(FinancePermissions.RequestTransactionExchangeRateOverride);
        FinancePermissions.RequestTransactionExchangeRateOverride
            .Should().NotBe(FinancePermissions.ApproveTransactionExchangeRateOverride);
    }

    private static ApplicationDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static FinanceExchangeRateOverrideService CreateService(
        ApplicationDbContext db, Guid tenantId, Guid userId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.TenantId).Returns(tenantId);
        currentUser.SetupGet(x => x.UserId).Returns(userId.ToString());
        currentUser.SetupGet(x => x.Claims).Returns(new Dictionary<string, string>());
        return new FinanceExchangeRateOverrideService(
            db, currentUser.Object, Mock.Of<IWorkflowService>());
    }
}
