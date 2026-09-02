using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities;
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

public sealed class ProcurementControlEventServiceTests
{
    private static readonly DateTime OccurredAt = new(2026, 7, 21, 8, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RecordCapturesCompleteImmutableDecisionAndSharedEvidenceReference()
    {
        await using var fixture = new Fixture();
        var upload = await fixture.AddUploadAsync(fixture.TenantId, "authority-approval.pdf");

        var recorded = await fixture.Service.RecordAsync(Request("event-complete") with
        {
            RuleCode = "AUTHORITY-MATRIX-001",
            RuleId = Guid.NewGuid(),
            RuleVersion = "3",
            DecisionKeys = new() { "DEC-004", "DEC-002" },
            Before = new { status = "Submitted", amount = 125000m },
            After = new { status = "Approved", amount = 125000m },
            Evidence = new()
            {
                new ProcurementControlEventEvidenceReference
                {
                    ReferenceKind = ProcurementControlEvidenceReferenceKind.FileUploadRecord,
                    ReferenceId = upload.Id,
                    Label = "Authority approval",
                    RequirementKey = "AUTHORITY_APPROVAL"
                }
            }
        });

        recorded.ActorUserId.Should().Be(fixture.UserId);
        recorded.ActorRoles.Should().ContainSingle(ProcurementAccessControlRegistry.InternalAuditRole);
        recorded.DecisionKeys.Should().Equal("DEC-002", "DEC-004");
        recorded.BeforeJson.Should().Contain("Submitted");
        recorded.AfterJson.Should().Contain("Approved");
        recorded.CorrelationId.Should().Be("trace-event-complete");
        recorded.IntegrityHash.Should().MatchRegex("^[0-9A-F]{64}$");
        recorded.IntegrityValid.Should().BeTrue();
        recorded.Evidence.Should().ContainSingle(item => item.ReferenceAvailable &&
            item.FileName == "authority-approval.pdf" && item.RequirementKey == "AUTHORITY_APPROVAL");
        (await fixture.Service.VerifyIntegrityAsync()).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task RepeatedBusinessEventWithLaterRetryTimestampIsIdempotentButDifferentPayloadConflicts()
    {
        await using var fixture = new Fixture();
        var request = Request("event-idempotent");

        var first = await fixture.Service.RecordAsync(request);
        var retryRequest = Request("event-idempotent") with
        {
            OccurredAtUtc = OccurredAt.AddMinutes(5)
        };
        var retry = await fixture.Service.RecordAsync(retryRequest);
        retryRequest.Action = "DifferentAction";
        var conflict = () => fixture.Service.RecordAsync(retryRequest);

        retry.Id.Should().Be(first.Id);
        retry.OccurredAtUtc.Should().Be(OccurredAt);
        (await fixture.Context.ProcurementControlEvents.CountAsync()).Should().Be(1);
        await conflict.Should().ThrowAsync<ProcurementControlEventConflictException>();
    }

    [Fact]
    public async Task ForeignTenantSharedEvidenceCannotBeLinked()
    {
        await using var fixture = new Fixture();
        var upload = await fixture.AddUploadAsync(fixture.ForeignTenantId, "foreign.pdf");
        var request = Request("event-foreign-evidence") with
        {
            Evidence = new()
            {
                new ProcurementControlEventEvidenceReference
                {
                    ReferenceKind = ProcurementControlEvidenceReferenceKind.FileUploadRecord,
                    ReferenceId = upload.Id
                }
            }
        };

        var action = () => fixture.Service.RecordAsync(request);

        await action.Should().ThrowAsync<ProcurementControlEventNotFoundException>();
        (await fixture.Context.ProcurementControlEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SearchAndDetailNeverCrossTenantBoundary()
    {
        await using var fixture = new Fixture();
        var recorded = await fixture.Service.RecordAsync(Request("event-tenant"));

        fixture.SwitchTenant(fixture.ForeignTenantId);
        var page = await fixture.Service.SearchAsync(new ProcurementControlEventSearchRequest());
        var detail = () => fixture.Service.GetAsync(recorded.Id);

        page.TotalCount.Should().Be(0);
        await detail.Should().ThrowAsync<ProcurementControlEventNotFoundException>();
    }

    [Fact]
    public async Task IntegrityVerificationDetectsAChangedStoredPayload()
    {
        await using var fixture = new Fixture();
        var recorded = await fixture.Service.RecordAsync(Request("event-integrity"));
        var entity = await fixture.Context.ProcurementControlEvents.SingleAsync(item => item.Id == recorded.Id);
        entity.ResultValuesJson = "{\"allowed\":false,\"tampered\":true}";
        await fixture.Context.SaveChangesAsync();

        var integrity = await fixture.Service.VerifyIntegrityAsync();

        integrity.IsValid.Should().BeFalse();
        integrity.InvalidCount.Should().Be(1);
        integrity.Issues.Should().ContainSingle(item => item.EventId == recorded.Id && item.ExpectedHash != item.ActualHash);
    }

    [Theory]
    [InlineData("TDC_STORES_OFFICER")]
    [InlineData("TenantAdmin")]
    [InlineData("SuperAdmin")]
    [InlineData(ProcurementAccessControlRegistry.IctAdministratorRole)]
    public async Task AuditReaderPermissionIsRequiredForQueryButNotForInternalWriterContract(string role)
    {
        await using var fixture = new Fixture();
        fixture.SetRoles(role);
        await fixture.Service.RecordAsync(Request("event-writer"));

        var query = () => fixture.Service.GetSummaryAsync();

        await query.Should().ThrowAsync<ProcurementControlEventAuthorizationException>();
    }

    [Fact]
    public async Task RealWriterPersistsEveryRequiredSemanticOperation()
    {
        await using var fixture = new Fixture();
        var actions = new (string Action, AuditOperationKind Operation)[]
        {
            ("Created", AuditOperationKind.Create),
            ("Updated", AuditOperationKind.Update),
            ("InvoiceMatchExceptionApproved", AuditOperationKind.Approve),
            ("InvoiceMatchExceptionRejected", AuditOperationKind.Reject),
            ("OverrideSourcingMethod", AuditOperationKind.Override),
            ("PostStockAdjustment", AuditOperationKind.Post),
            ("ReverseStockAdjustment", AuditOperationKind.Reverse),
            ("Dispatch", AuditOperationKind.Dispatch),
            ("Receive", AuditOperationKind.Receive)
        };

        foreach (var (action, expected) in actions)
        {
            var key = $"semantic-{(int)expected}";
            var request = Request(key);
            request.Action = action;

            var recorded = await fixture.Service.RecordAsync(request);

            recorded.Operation.Should().Be(expected);
        }

        var persisted = await fixture.Context.ProcurementControlEvents
            .OrderBy(item => item.Operation)
            .Select(item => item.Operation)
            .ToListAsync();
        persisted.Should().Equal(actions.Select(item => item.Operation).OrderBy(item => item));
    }

    private static ProcurementControlEventWriteRequest Request(string key) => new()
    {
        EventKey = key,
        EventType = "AuthorityDecision",
        Action = "Approve",
        Result = ProcurementControlEventResult.Allowed,
        RuleCode = "AUTHORITY-MATRIX",
        DecisionKeys = new() { "DEC-002" },
        SourceType = "PurchaseRequisition",
        SourceId = Guid.Parse("3fb2f76d-346c-4c51-ad23-e13d6b7a0066"),
        SourceReference = "PR-2026-0001",
        Reason = "Authority rule satisfied.",
        InputValues = new { amount = 125000m, currency = "GHS" },
        ResultValues = new { allowed = true, authority = "TDC_HEAD_OF_PROCUREMENT" },
        CorrelationId = $"trace-{key}",
        OccurredAtUtc = OccurredAt
    };

    private sealed class Fixture : IAsyncDisposable
    {
        private Guid _activeTenantId;
        private readonly HashSet<string> _roles = new(StringComparer.OrdinalIgnoreCase)
            { ProcurementAccessControlRegistry.InternalAuditRole };
        private readonly Mock<ICurrentUserProvider> _currentUser = new();
        private readonly UnitOfWork _unitOfWork;

        public Fixture()
        {
            TenantId = Guid.NewGuid();
            ForeignTenantId = Guid.NewGuid();
            UserId = Guid.NewGuid();
            _activeTenantId = TenantId;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options;
            Context = new ApplicationDbContext(options);
            Context.Tenants.AddRange(
                new Tenant { Id = TenantId, Code = "TDC", Name = "TDC", Status = TenantStatus.Active },
                new Tenant { Id = ForeignTenantId, Code = "OTHER", Name = "Other", Status = TenantStatus.Active });
            Context.Users.Add(new ApplicationUser
            {
                Id = UserId,
                TenantId = TenantId,
                UserName = "audit@tdc.test",
                NormalizedUserName = "AUDIT@TDC.TEST",
                FirstName = "Control",
                LastName = "Auditor",
                IsActive = true
            });
            Context.SaveChanges();
            _currentUser.SetupGet(item => item.TenantId).Returns(() => _activeTenantId);
            _currentUser.SetupGet(item => item.UserId).Returns(UserId);
            _currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
            _currentUser.SetupGet(item => item.Username).Returns("audit@tdc.test");
            _currentUser.SetupGet(item => item.FullName).Returns("Control Auditor");
            _currentUser.SetupGet(item => item.Roles).Returns(() => _roles);
            _currentUser.Setup(item => item.HasRole(It.IsAny<string>())).Returns((string role) => _roles.Contains(role));
            _unitOfWork = new UnitOfWork(Context);
            Service = new ProcurementControlEventService(_unitOfWork, _currentUser.Object,
                NullLogger<ProcurementControlEventService>.Instance);
        }

        public Guid TenantId { get; }
        public Guid ForeignTenantId { get; }
        public Guid UserId { get; }
        public ApplicationDbContext Context { get; }
        public ProcurementControlEventService Service { get; }
        public void SwitchTenant(Guid tenantId) => _activeTenantId = tenantId;
        public void SetRoles(params string[] roles)
        {
            _roles.Clear();
            foreach (var role in roles) _roles.Add(role);
        }

        public async Task<FileUploadRecord> AddUploadAsync(Guid tenantId, string name)
        {
            var upload = new FileUploadRecord
            {
                TenantId = tenantId,
                Category = "procurement-evidence",
                FilePath = $"/shared/evidence/{name}",
                StoredFileName = $"{Guid.NewGuid():N}.pdf",
                OriginalFileName = name,
                ContentType = "application/pdf",
                FileSize = 128,
                StorageProvider = "Test",
                UploadedByUserId = UserId,
                VirusScanStatus = FileVirusScanStatus.Clean
            };
            Context.FileUploadRecords.Add(upload);
            await Context.SaveChangesAsync();
            return upload;
        }

        public async ValueTask DisposeAsync()
        {
            _unitOfWork.Dispose();
            await Context.DisposeAsync();
        }
    }
}
