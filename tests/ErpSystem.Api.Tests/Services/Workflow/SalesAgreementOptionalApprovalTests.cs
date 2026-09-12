using ErpSystem.Api.Services.Sales;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Workflow;

public sealed class SalesAgreementOptionalApprovalTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Legacy_activation_uses_central_submission_and_preserves_active_approval(bool required)
    {
        await using var fixture = await Fixture.Create();
        fixture.Workflow.Setup(x => x.SubmitAsync("SalesAgreement", fixture.Agreement.Id)).ReturnsAsync(
            new WorkflowIntegrationResult(new WorkflowExecutionResult {
                Success = true, Status = required ? WorkflowInstanceStatus.InProgress : WorkflowInstanceStatus.Completed,
                WorkflowInstanceId = required ? Guid.NewGuid() : null
            }, required ? WorkflowOutcome.Pending : WorkflowOutcome.Approved, approvalRequired: required));
        var result = await fixture.Service.ActivateAsync(fixture.Agreement.Id);
        result.AgreementStatus.Should().Be(required ? "PendingApproval" : "Active");
        fixture.Agreement.ApprovedById.Should().BeNull();
        fixture.Agreement.ApprovedDate.Should().BeNull();
        fixture.Workflow.Verify(x => x.SubmitAsync("SalesAgreement", fixture.Agreement.Id), Times.Once);
    }

    [Fact]
    public async Task Pending_approval_cannot_be_activated_manually()
    {
        await using var fixture = await Fixture.Create(SalesAgreementStatus.PendingApproval);
        var act = () => fixture.Service.ActivateAsync(fixture.Agreement.Id);
        await act.Should().ThrowAsync<InvalidOperationException>();
        fixture.Agreement.AgreementStatus.Should().Be(SalesAgreementStatus.PendingApproval);
        fixture.Workflow.Verify(x => x.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Workflow_failure_does_not_activate_the_agreement()
    {
        await using var fixture = await Fixture.Create();
        fixture.Workflow.Setup(x => x.SubmitAsync("SalesAgreement", fixture.Agreement.Id)).ReturnsAsync(
            new WorkflowIntegrationResult(new WorkflowExecutionResult { Success = false, Message = "No eligible approver" }, WorkflowOutcome.Pending));
        var act = () => fixture.Service.SubmitForApprovalAsync(fixture.Agreement.Id);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("No eligible approver");
        fixture.Agreement.AgreementStatus.Should().Be(SalesAgreementStatus.Draft);
    }

    [Fact]
    public async Task Tracked_foreign_tenant_record_cannot_be_submitted_or_activated()
    {
        await using var fixture = await Fixture.Create();
        fixture.User.SetupGet(x => x.TenantId).Returns(Guid.NewGuid());
        var submit = () => fixture.Service.SubmitForApprovalAsync(fixture.Agreement.Id);
        var activate = () => fixture.Service.ActivateAsync(fixture.Agreement.Id);
        await submit.Should().ThrowAsync<KeyNotFoundException>();
        await activate.Should().ThrowAsync<KeyNotFoundException>();
        fixture.Workflow.Verify(x => x.SubmitAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Suspended_agreement_with_retained_active_instance_cannot_skip_it()
    {
        await using var fixture = await Fixture.Create(SalesAgreementStatus.Suspended);
        fixture.Workflow.Setup(x => x.HasActiveApprovalInstanceAsync("SalesAgreement", fixture.Agreement.Id)).ReturnsAsync(true);
        var act = () => fixture.Service.ActivateAsync(fixture.Agreement.Id);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*existing agreement approval*");
        fixture.Agreement.AgreementStatus.Should().Be(SalesAgreementStatus.Suspended);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; }
        public SalesAgreement Agreement { get; }
        public Mock<ICurrentUserService> User { get; } = new();
        public Mock<IWorkflowIntegrationService> Workflow { get; } = new();
        public SalesAgreementService Service { get; }

        private Fixture(ApplicationDbContext db, SalesAgreement agreement)
        {
            Db = db;
            Agreement = agreement;
            User.SetupGet(x => x.TenantId).Returns(agreement.TenantId);
            User.SetupGet(x => x.UserId).Returns(Guid.NewGuid().ToString());
            Service = new SalesAgreementService(db, NullLogger<SalesAgreementService>.Instance, User.Object,
                Mock.Of<IDocumentNumberingService>(), Workflow.Object,
                new WorkflowStatusAdapterRegistry([new SalesAgreementWorkflowStatusAdapter()]));
        }

        public static async Task<Fixture> Create(SalesAgreementStatus status = SalesAgreementStatus.Draft)
        {
            var tenant = Guid.NewGuid();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"sales-optional-{Guid.NewGuid()}").Options, tenant);
            var partner = new BusinessPartner { Id = Guid.NewGuid(), TenantId = tenant, PartnerName = "Test customer", PartnerCode = "C-OPTIONAL" };
            var agreement = new SalesAgreement { Id = Guid.NewGuid(), TenantId = tenant,
                BusinessPartnerId = partner.Id, BusinessPartner = partner, CustomerName = partner.PartnerName,
                AgreementTitle = "Optional agreement", DocumentNumber = "SA-OPTIONAL", AgreementStatus = status,
                StartDate = DateTime.UtcNow.Date, CreatedBy = "test", DocumentDate = DateTime.UtcNow.Date };
            db.Add(agreement);
            await db.SaveChangesAsync();
            return new Fixture(db, agreement);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
