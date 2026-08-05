using System.Text.Json;
using ErpSystem.Api.Controllers;
using ErpSystem.Api.Services.Documents.Finance;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceClosePackDocumentBuilderTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClosePack")]
    public async Task Render_ShouldProduceAuditedDeterministicSignedPackFromRetainedCycleEvidence()
    {
        var tenantId = Guid.NewGuid();
        var generatedByUserId = Guid.NewGuid();
        await using var db = CreateContext();
        var cycleId = SeedCompleteCloseCycle(db, tenantId);
        await db.SaveChangesAsync();

        var currentUser = CurrentUser(tenantId, generatedByUserId, "abena.owusu");
        var audit = new CapturingFinanceAuditService();
        var builder = new FinanceClosePackDocumentBuilder(db, currentUser.Object, audit);
        var request = new DocumentRenderRequestDto
        {
            DocumentType = DocumentTypes.FinanceClosePack,
            EntityId = cycleId,
            Format = "pdf",
            CopyType = "Original"
        };

        var first = await builder.RenderAsync(request);
        var second = await builder.RenderAsync(request);

        first.Content.Should().StartWith(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF
        first.FileName.Should().Be("FCP-2026-07-C02-original.pdf");
        first.DocumentType.Should().Be(DocumentTypes.FinanceClosePack);
        audit.Events.Should().HaveCount(2);
        audit.Events.Should().OnlyContain(item =>
            item.EventType == FinanceAuditEvents.AccountingPeriodClosePackGenerated &&
            item.TenantId == tenantId &&
            item.Resource == "Finance.ClosePack" &&
            item.ResourceId == cycleId.ToString());

        // The PDF footer contains its generation time, but the printed evidence digest must be
        // identical on repeat generation while the retained accounting evidence is unchanged.
        EvidenceDigest(audit.Events[0]).Should().Be(EvidenceDigest(audit.Events[1]));

        var qaOutputPath = Environment.GetEnvironmentVariable("TDC_CLOSE_PACK_QA_OUTPUT");
        if (!string.IsNullOrWhiteSpace(qaOutputPath))
        {
            // This opt-in output supports the repository's render-and-inspect QA workflow without
            // making normal unit-test runs write artifacts into a developer's working tree.
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(qaOutputPath))!);
            await File.WriteAllBytesAsync(qaOutputPath, first.Content);
        }
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClosePack")]
    public async Task Render_ShouldRejectCrossTenantCycleEvenWhenIdentifierIsKnown()
    {
        var ownerTenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var cycleId = SeedCompleteCloseCycle(db, ownerTenantId);
        await db.SaveChangesAsync();
        var currentUser = CurrentUser(Guid.NewGuid(), Guid.NewGuid(), "cross.tenant");
        var builder = new FinanceClosePackDocumentBuilder(db, currentUser.Object);

        var action = () => builder.RenderAsync(new DocumentRenderRequestDto
        {
            DocumentType = DocumentTypes.FinanceClosePack,
            EntityId = cycleId,
            Format = "pdf"
        });

        await action.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*was not found*");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClosePack")]
    public async Task Render_ShouldPreserveSupersededCertificateForHistoricalReopenedCycle()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateContext();
        var cycleId = SeedCompleteCloseCycle(db, tenantId);
        await db.SaveChangesAsync();
        var cycle = await db.FinanceCloseCycles.SingleAsync(item => item.Id == cycleId);
        var certificate = await db.FinanceCloseCertifications.SingleAsync(item =>
            item.FinanceCloseCycleId == cycleId);
        cycle.Status = FinanceCloseStatuses.Reopened;
        cycle.ReopenedAt = new DateTime(2026, 8, 4, 9, 0, 0, DateTimeKind.Utc);
        cycle.ReopenReason = "Post-close audit adjustment requires a separately certified cycle three.";
        certificate.IsSuperseded = true;
        certificate.SupersededAt = cycle.ReopenedAt;
        certificate.SupersededReason = $"Period reopened: {cycle.ReopenReason}";
        await db.SaveChangesAsync();

        var builder = new FinanceClosePackDocumentBuilder(
            db,
            CurrentUser(tenantId, Guid.NewGuid(), "historical.reviewer").Object);

        var result = await builder.RenderAsync(new DocumentRenderRequestDto
        {
            DocumentType = DocumentTypes.FinanceClosePack,
            EntityId = cycleId,
            Format = "pdf",
            CopyType = "Reprint"
        });

        result.Content.Should().StartWith(new byte[] { 0x25, 0x50, 0x44, 0x46 });
        result.FileName.Should().Be("FCP-2026-07-C02-reprint.pdf");
    }

    [Fact]
    [Trait("Batch", "FinanceGoLive-PeriodClosePack")]
    public void ClosePackDocumentType_ShouldUseFinanceReportExportPolicy()
    {
        DocumentsController.FinanceDocumentPolicies[DocumentTypes.FinanceClosePack]
            .Should().Be(FinancePermissions.ExportFinanceReports);
    }

    private static Guid SeedCompleteCloseCycle(ApplicationDbContext db, Guid tenantId)
    {
        var preparerId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var fiscalYear = new FiscalYear
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearName = "Fiscal Year 2026",
            FiscalYearCode = "FY2026",
            Year = 2026,
            FiscalYearType = "Calendar",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            TotalDays = 365,
            NumberOfPeriods = 12,
            Status = "Open"
        };
        var period = new FiscalPeriod
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalYearId = fiscalYear.Id,
            FiscalYear = fiscalYear,
            PeriodName = "July 2026",
            PeriodCode = "2026-07",
            PeriodNumber = 7,
            PeriodType = PeriodType.Monthly,
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2026, 7, 31),
            PeriodDays = 31,
            PeriodStatus = "Closed",
            IsOpen = false,
            IsClosed = true,
            ClosedDate = new DateTime(2026, 8, 3, 8, 30, 0, DateTimeKind.Utc),
            ClosingNotes = "July close approved after all mandatory controls passed.",
            TotalDebits = 15_250_000m,
            TotalCredits = 15_250_000m,
            BalanceDifference = 0m
        };
        var template = new FinanceCloseTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TemplateCode = "TDC-MONTH-END",
            Name = "TDC Month-End Close",
            CloseType = FinanceCloseTemplateTypes.MonthEnd,
            Version = 4,
            Status = FinanceCloseTemplateStatuses.Approved,
            IsActive = true,
            ApprovedByUserId = Guid.NewGuid(),
            ApprovedByUserName = "Finance Director",
            ApprovedAt = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc),
            ApprovalDeclaration = "Approved for TDC monthly financial close operations."
        };
        var priorCycle = new FinanceCloseCycle
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalPeriodId = period.Id,
            CycleNumber = 1,
            TemplateCode = template.TemplateCode,
            TemplateVersion = 3,
            CloseType = FinanceCloseTemplateTypes.MonthEnd,
            Status = FinanceCloseStatuses.Reopened,
            EvaluationCount = 3,
            StartedAt = new DateTime(2026, 8, 1, 8, 0, 0, DateTimeKind.Utc),
            PreparedAt = new DateTime(2026, 8, 1, 15, 0, 0, DateTimeKind.Utc),
            ClosedAt = new DateTime(2026, 8, 1, 16, 0, 0, DateTimeKind.Utc),
            ReopenedAt = new DateTime(2026, 8, 2, 9, 0, 0, DateTimeKind.Utc),
            ReopenedByUserId = approverId,
            ReopenReason = "Material audit adjustment required before management reporting."
        };
        var cycle = new FinanceCloseCycle
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FiscalPeriodId = period.Id,
            FiscalPeriod = period,
            FinanceCloseTemplateId = template.Id,
            FinanceCloseTemplate = template,
            CycleNumber = 2,
            TemplateCode = template.TemplateCode,
            TemplateVersion = template.Version,
            CloseType = FinanceCloseTemplateTypes.MonthEnd,
            Status = FinanceCloseStatuses.Closed,
            EvaluationCount = 4,
            StartedAt = new DateTime(2026, 8, 2, 9, 15, 0, DateTimeKind.Utc),
            PreparedAt = new DateTime(2026, 8, 3, 8, 0, 0, DateTimeKind.Utc),
            ClosedAt = period.ClosedDate
        };

        var postingTask = CompletedTask(
            tenantId,
            cycle.Id,
            10,
            "POSTING_INTEGRITY",
            "Finance posting integrity",
            "Ledger Integrity",
            preparerId,
            automated: true);
        var apTask = CompletedTask(
            tenantId,
            cycle.Id,
            20,
            "AP_CONTROL_RECONCILIATION",
            "Accounts payable control reconciliation",
            "Accounts Payable",
            preparerId,
            automated: true);
        var fxTask = CompletedTask(
            tenantId,
            cycle.Id,
            30,
            "FX_REVALUATION",
            "Foreign-currency revaluation review",
            "Foreign Exchange",
            preparerId,
            automated: true);
        var manualTask = CompletedTask(
            tenantId,
            cycle.Id,
            40,
            "MANAGEMENT_REVIEW",
            "Management analytical review",
            "Certification",
            preparerId,
            automated: false);

        var postingCheck = PassedCheck(
            tenantId,
            cycle.Id,
            postingTask.CheckCode!,
            "Finance posting integrity",
            "Ledger Integrity",
            "All posted Finance source documents have a valid posting event and balanced journal.");
        var apCheck = PassedCheck(
            tenantId,
            cycle.Id,
            apTask.CheckCode!,
            "Accounts payable control reconciliation",
            "Accounts Payable",
            "AP supplier balances reconcile to the general ledger control account at GHS 4,825,300.00.");
        var fxCheck = new FinanceCloseCheckSnapshot
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FinanceCloseCycleId = cycle.Id,
            EvaluationNumber = 4,
            CheckCode = fxTask.CheckCode!,
            Title = "Foreign-currency revaluation review",
            Category = "Foreign Exchange",
            Severity = FinanceCloseCheckSeverities.Warning,
            Status = FinanceCloseCheckStatuses.Waived,
            ResultSummary = "One immaterial dormant USD account was supported by management evidence.",
            ExceptionCount = 1,
            ExceptionAmount = 125.50m,
            EvidenceJson = "{\"account\":\"USD-DORMANT-01\",\"amount\":125.50}",
            EvidenceFingerprint = new string('f', 64),
            EvaluatedAt = new DateTime(2026, 8, 3, 8, 25, 0, DateTimeKind.Utc),
            EvaluatedByUserId = approverId,
            EvaluatedByUserName = "Kwame Boateng"
        };

        var file = new FileUploadRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Category = "finance-close-evidence",
            FilePath = "tenant-controlled/finance/evidence.pdf",
            StoredFileName = "evidence-immutable.pdf",
            OriginalFileName = "FX dormant account reconciliation.pdf",
            ContentType = "application/pdf",
            FileSize = 245_760,
            StorageProvider = "Local",
            UploadedByUserId = preparerId,
            VirusScanStatus = FileVirusScanStatus.Clean,
            ScannedAtUtc = new DateTime(2026, 8, 3, 7, 30, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 8, 3, 7, 29, 0, DateTimeKind.Utc)
        };
        var evidence = new FinanceCloseEvidenceAttachment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FinanceCloseCycleId = cycle.Id,
            FinanceCloseTaskId = fxTask.Id,
            FileUploadRecordId = file.Id,
            FileUploadRecord = file,
            EvidenceType = FinanceCloseEvidenceTypes.Reconciliation,
            Description = "Management-supported reconciliation for the dormant foreign-currency account."
        };
        var waiver = new FinanceCloseExceptionWaiver
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FinanceCloseCycleId = cycle.Id,
            FinanceCloseTaskId = fxTask.Id,
            FinanceCloseCheckSnapshotId = fxCheck.Id,
            FinanceCloseEvidenceAttachmentId = evidence.Id,
            CheckCode = fxCheck.CheckCode,
            EvidenceFingerprint = fxCheck.EvidenceFingerprint,
            Status = FinanceCloseWaiverStatuses.Approved,
            Justification = "The balance is immaterial, dormant, and fully supported by retained reconciliation evidence.",
            RequestedByUserId = preparerId,
            RequestedByUserName = "Ama Mensah",
            RequestedAt = new DateTime(2026, 8, 3, 7, 35, 0, DateTimeKind.Utc),
            ReviewedByUserId = approverId,
            ReviewedByUserName = "Kwame Boateng",
            ReviewedAt = new DateTime(2026, 8, 3, 7, 50, 0, DateTimeKind.Utc),
            ReviewComment = "Approved as an immaterial warning exception with adequate evidence."
        };
        fxCheck.AppliedWaiverId = waiver.Id;

        var certification = new FinanceCloseCertification
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FinanceCloseCycleId = cycle.Id,
            PreparedByUserId = preparerId,
            PreparedByUserName = "Ama Mensah",
            PreparedAt = cycle.PreparedAt,
            PreparerDeclaration = "I confirm the July checklist and retained evidence are complete and accurate.",
            ReviewedByUserId = approverId,
            ReviewedByUserName = "Kwame Boateng",
            ReviewedAt = cycle.ClosedAt,
            ReviewerDeclaration = "I independently reviewed the final controls and approve closure of the July period.",
            ApprovedByUserId = approverId,
            ApprovedByUserName = "Kwame Boateng",
            ApprovedAt = cycle.ClosedAt
        };
        var alert = new FinanceCloseAlertDelivery
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FinanceCloseCycleId = cycle.Id,
            FinanceCloseTaskId = manualTask.Id,
            AlertType = FinanceCloseAlertTypes.TaskDueSoon,
            DedupeKey = $"task-due-soon:{manualTask.Id}:{preparerId}",
            RecipientUserId = preparerId,
            RecipientUserName = "Ama Mensah",
            Status = FinanceCloseAlertDeliveryStatuses.Delivered,
            DueAtUtc = new DateTime(2026, 8, 3, 8, 0, 0, DateTimeKind.Utc),
            LastAttemptAtUtc = new DateTime(2026, 8, 3, 7, 0, 0, DateTimeKind.Utc),
            DeliveredAtUtc = new DateTime(2026, 8, 3, 7, 0, 1, DateTimeKind.Utc),
            AttemptCount = 1,
            NotificationId = Guid.NewGuid()
        };

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Tema Development Corporation",
            Code = "TDC",
            Status = TenantStatus.Active,
            Address = "Tema, Greater Accra Region, Ghana",
            ContactEmail = "finance@tdc.gov.gh",
            ContactPhone = "+233 30 320 0000",
            BaseCurrency = "GHS",
            BaseCurrencyName = "Ghana Cedi",
            CurrencySymbol = "GHS"
        });
        db.FiscalYears.Add(fiscalYear);
        db.FiscalPeriods.Add(period);
        db.FinanceCloseTemplates.Add(template);
        db.FinanceCloseCycles.AddRange(priorCycle, cycle);
        db.FinanceCloseTasks.AddRange(postingTask, apTask, fxTask, manualTask);
        db.FinanceCloseCheckSnapshots.AddRange(postingCheck, apCheck, fxCheck);
        db.FileUploadRecords.Add(file);
        db.FinanceCloseEvidenceAttachments.Add(evidence);
        db.FinanceCloseExceptionWaivers.Add(waiver);
        db.FinanceCloseCertifications.Add(certification);
        db.FinanceCloseAlertDeliveries.Add(alert);
        return cycle.Id;
    }

    private static FinanceCloseTask CompletedTask(
        Guid tenantId,
        Guid cycleId,
        int sequence,
        string checkCode,
        string title,
        string category,
        Guid completedBy,
        bool automated)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FinanceCloseCycleId = cycleId,
            TaskCode = checkCode,
            CheckCode = automated ? checkCode : null,
            Title = title,
            Category = category,
            Sequence = sequence,
            IsMandatory = true,
            IsAutomated = automated,
            Status = FinanceCloseTaskStatuses.Completed,
            AssignedToUserId = completedBy,
            AssignedToUserName = "Ama Mensah",
            DueAt = new DateTime(2026, 8, 3, 8, 0, 0, DateTimeKind.Utc),
            CompletedAt = new DateTime(2026, 8, 3, 7, 45, 0, DateTimeKind.Utc),
            CompletedByUserId = completedBy,
            CompletedByUserName = "Ama Mensah",
            EvidenceSummary = automated
                ? "Completion is supported by the immutable final automated control snapshot."
                : "Management analytical review signed and retained in the close workspace."
        };

    private static FinanceCloseCheckSnapshot PassedCheck(
        Guid tenantId,
        Guid cycleId,
        string checkCode,
        string title,
        string category,
        string summary)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FinanceCloseCycleId = cycleId,
            EvaluationNumber = 4,
            CheckCode = checkCode,
            Title = title,
            Category = category,
            Severity = FinanceCloseCheckSeverities.Mandatory,
            Status = FinanceCloseCheckStatuses.Passed,
            ResultSummary = summary,
            ExceptionCount = 0,
            EvidenceJson = "{\"passed\":true}",
            EvidenceFingerprint = Convert.ToHexString(Guid.NewGuid().ToByteArray()).PadRight(64, '0')[..64].ToLowerInvariant(),
            EvaluatedAt = new DateTime(2026, 8, 3, 8, 25, 0, DateTimeKind.Utc)
        };

    private static Mock<ICurrentUserService> CurrentUser(Guid tenantId, Guid userId, string userName)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.UserId).Returns(userId.ToString());
        currentUser.SetupGet(service => service.UserName).Returns(userName);
        return currentUser;
    }

    private static string? EvidenceDigest(FinanceAuditEventDto auditEvent)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(auditEvent.AfterValues));
        return json.RootElement.GetProperty("EvidenceDigest").GetString();
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"finance-close-pack-{Guid.NewGuid()}")
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed class CapturingFinanceAuditService : IFinanceAuditService
    {
        public List<FinanceAuditEventDto> Events { get; } = new();

        public Task<AuditLog> RecordAsync(
            FinanceAuditEventDto auditEvent,
            CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.FromResult(new AuditLog { Id = Guid.NewGuid() });
        }

        public Task<IReadOnlyList<AuditLog>> GetAuditTrailAsync(
            Guid tenantId,
            string resource,
            string resourceId,
            int limit = 100,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditLog>>(Array.Empty<AuditLog>());
    }
}
