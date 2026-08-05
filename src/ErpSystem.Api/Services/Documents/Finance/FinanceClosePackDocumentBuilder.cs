using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Documents;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Colors = QuestPDF.Helpers.Colors;

namespace ErpSystem.Api.Services.Documents.Finance;

/// <summary>
/// Produces the printable, electronically signed evidence pack for one numbered Finance close
/// cycle. The builder deliberately reads the durable close workspace instead of recalculating
/// live balances: an auditor must see the exact evidence that the maker and checker approved,
/// even after the period is reopened and a later cycle is completed.
/// </summary>
public sealed class FinanceClosePackDocumentBuilder : IDocumentBuilder
{
    private const string FinanceModule = "FINANCE";

    // These integrity controls are the implemented resolution for FIN-LIM-0034. Repeating the
    // list in the evidence pack makes the policy visible to reviewers without creating a second
    // waiver policy: the close service remains the only enforcement boundary.
    private static readonly IReadOnlySet<string> NonWaivableControlCodes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "POSTING_INTEGRITY",
            "TRIAL_BALANCE",
            "AP_CONTROL_RECONCILIATION",
            "AR_CONTROL_RECONCILIATION",
            "RECURRING_JOURNAL_EXCEPTIONS",
            "FIXED_ASSET_DEPRECIATION"
        };

    private static readonly JsonSerializerOptions DigestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IFinanceAuditService? _financeAuditService;

    public FinanceClosePackDocumentBuilder(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IFinanceAuditService? financeAuditService = null)
    {
        _context = context;
        _currentUser = currentUser;
        _financeAuditService = financeAuditService;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public string DocumentType => DocumentTypes.FinanceClosePack;

    public bool RequiresEntityId => true;

    public bool SupportsFormat(string format)
        => string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase);

    public async Task<RenderedDocumentDto> RenderAsync(
        DocumentRenderRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!SupportsFormat(request.Format))
        {
            throw new NotSupportedException("Finance close packs currently support PDF output only.");
        }

        var tenantId = _currentUser.TenantId
            ?? throw new UnauthorizedAccessException("Invalid tenant context.");
        var generatedAtUtc = DateTime.UtcNow;
        var copyType = NormalizeCopyType(request.CopyType);
        var model = await LoadModelAsync(request.EntityId, tenantId, cancellationToken);

        ValidateFinalCertificate(model);

        var documentNumber = BuildDocumentNumber(model.Period.PeriodCode, model.Cycle.CycleNumber);
        var evidenceDigest = BuildEvidenceDigest(model);
        var pdfBytes = BuildPdf(
            model,
            documentNumber,
            evidenceDigest,
            copyType,
            generatedAtUtc,
            _currentUser.UserName);
        var pdfSha256 = Convert.ToHexString(SHA256.HashData(pdfBytes)).ToLowerInvariant();

        // Generating a close pack exposes a sensitive signed accounting record. Retaining both
        // the evidence digest printed in the pack and the hash of the emitted bytes lets TDC
        // distinguish the reproducible source evidence from the particular rendered copy.
        if (_financeAuditService != null)
        {
            await _financeAuditService.RecordAsync(new FinanceAuditEventDto
            {
                EventType = FinanceAuditEvents.AccountingPeriodClosePackGenerated,
                TenantId = tenantId,
                SourceModule = FinanceModule,
                SourceDocumentType = "FinanceCloseCycle",
                SourceDocumentId = model.Cycle.Id,
                AfterValues = new
                {
                    DocumentNumber = documentNumber,
                    model.Cycle.FiscalPeriodId,
                    model.Cycle.CycleNumber,
                    model.Cycle.Status,
                    CopyType = copyType,
                    EvidenceDigest = evidenceDigest,
                    PdfSha256 = pdfSha256,
                    GeneratedAtUtc = generatedAtUtc
                },
                Resource = "Finance.ClosePack",
                ResourceId = model.Cycle.Id.ToString(),
                Comment = $"{copyType} Finance period-close pack generated."
            }, cancellationToken);
        }

        return new RenderedDocumentDto
        {
            Content = pdfBytes,
            ContentType = "application/pdf",
            FileName = $"{SafeFileName(documentNumber)}-{copyType.ToLowerInvariant()}.pdf",
            DocumentType = DocumentType,
            EntityId = request.EntityId,
            Format = "pdf"
        };
    }

    private async Task<ClosePackModel> LoadModelAsync(
        Guid cycleId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        // Each query carries the tenant predicate even though global filters also apply. Signed
        // close evidence warrants defence in depth, and explicit predicates remain testable if a
        // future administrative query ever disables the normal tenant filter.
        var cycle = await _context.FinanceCloseCycles
            .AsNoTracking()
            .Include(item => item.FiscalPeriod)
                .ThenInclude(period => period.FiscalYear)
            .Include(item => item.FinanceCloseTemplate)
            .FirstOrDefaultAsync(item =>
                item.Id == cycleId && item.TenantId == tenantId && !item.IsDeleted,
                cancellationToken)
            ?? throw new KeyNotFoundException($"Finance close cycle '{cycleId}' was not found.");

        var certification = await _context.FinanceCloseCertifications
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.FinanceCloseCycleId == cycleId &&
                !item.IsDeleted)
            .OrderByDescending(item => item.ApprovedAt)
            .ThenByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var tasks = await _context.FinanceCloseTasks
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.FinanceCloseCycleId == cycleId &&
                !item.IsDeleted)
            .OrderBy(item => item.Sequence)
            .ThenBy(item => item.TaskCode)
            .ToListAsync(cancellationToken);

        var evaluationNumber = cycle.EvaluationCount;
        if (evaluationNumber <= 0)
        {
            evaluationNumber = await _context.FinanceCloseCheckSnapshots
                .AsNoTracking()
                .Where(item =>
                    item.TenantId == tenantId &&
                    item.FinanceCloseCycleId == cycleId &&
                    !item.IsDeleted)
                .Select(item => (int?)item.EvaluationNumber)
                .MaxAsync(cancellationToken) ?? 0;
        }

        var checks = await _context.FinanceCloseCheckSnapshots
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.FinanceCloseCycleId == cycleId &&
                item.EvaluationNumber == evaluationNumber &&
                !item.IsDeleted)
            .OrderBy(item => item.Category)
            .ThenBy(item => item.CheckCode)
            .ToListAsync(cancellationToken);

        var evidence = await _context.FinanceCloseEvidenceAttachments
            .AsNoTracking()
            .Include(item => item.FileUploadRecord)
            .Where(item =>
                item.TenantId == tenantId &&
                item.FinanceCloseCycleId == cycleId &&
                !item.IsDeleted &&
                !item.FileUploadRecord.IsDeleted)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        var waivers = await _context.FinanceCloseExceptionWaivers
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.FinanceCloseCycleId == cycleId &&
                !item.IsDeleted)
            .OrderBy(item => item.RequestedAt)
            .ToListAsync(cancellationToken);

        var alertDeliveries = await _context.FinanceCloseAlertDeliveries
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.FinanceCloseCycleId == cycleId &&
                !item.IsDeleted)
            .OrderBy(item => item.DueAtUtc)
            .ThenBy(item => item.AlertType)
            .ToListAsync(cancellationToken);

        var history = await _context.FinanceCloseCycles
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.FiscalPeriodId == cycle.FiscalPeriodId &&
                !item.IsDeleted)
            .OrderBy(item => item.CycleNumber)
            .ToListAsync(cancellationToken);

        var tenant = await _context.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == tenantId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("The tenant profile for this Finance close pack was not found.");

        return new ClosePackModel(
            tenant,
            cycle,
            cycle.FiscalPeriod,
            cycle.FiscalPeriod.FiscalYear,
            certification,
            tasks,
            checks,
            evidence,
            waivers,
            alertDeliveries,
            history,
            evaluationNumber);
    }

    private static void ValidateFinalCertificate(ClosePackModel model)
    {
        if (model.Cycle.Status != FinanceCloseStatuses.Closed &&
            model.Cycle.Status != FinanceCloseStatuses.Reopened)
        {
            throw new InvalidOperationException(
                "A Finance close pack can be generated only after the numbered cycle has been approved and closed.");
        }

        if (model.Certification?.PreparedAt == null ||
            model.Certification.PreparedByUserId == null ||
            model.Certification.ApprovedAt == null ||
            model.Certification.ApprovedByUserId == null)
        {
            throw new InvalidOperationException(
                "The Finance close cycle does not contain a complete maker-checker certificate.");
        }

        if (model.Certification.PreparedByUserId == model.Certification.ApprovedByUserId)
        {
            throw new InvalidOperationException(
                "The Finance close certificate violates maker-checker separation and cannot be printed.");
        }

        if (model.EvaluationNumber <= 0 || model.Checks.Count == 0)
        {
            throw new InvalidOperationException(
                "The Finance close cycle does not contain a final immutable control evaluation.");
        }

        if (model.Checks.Any(item =>
                item.Severity == FinanceCloseCheckSeverities.Mandatory &&
                item.Status == FinanceCloseCheckStatuses.Failed))
        {
            throw new InvalidOperationException(
                "The retained final evaluation contains a mandatory failed control and cannot support a signed close pack.");
        }
    }

    private static byte[] BuildPdf(
        ClosePackModel model,
        string documentNumber,
        string evidenceDigest,
        string copyType,
        DateTime generatedAtUtc,
        string? generatedBy)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(26);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Darken4));

                // Decoration is the QuestPDF construct that repeats its Before/After sections
                // around every page of paged content. Using it here keeps the complete TDC header
                // and export footer on table-continuation pages; ordinary page-region children can
                // otherwise consume their static text on page one while only dynamic page numbers
                // remain visible on later pages.
                page.Content().Decoration(decoration =>
                {
                    decoration.Before().Element(container => ComposeHeader(
                        container,
                        model,
                        documentNumber,
                        copyType));
                    decoration.Content().Element(container => ComposeContent(
                        container,
                        model,
                        documentNumber,
                        evidenceDigest));
                    decoration.After().Element(container => ComposeFooter(
                        container,
                        generatedAtUtc,
                        generatedBy));
                });
            });
        }).GeneratePdf();
    }

    private static void ComposeHeader(
        IContainer container,
        ClosePackModel model,
        string documentNumber,
        string copyType)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(model.Tenant.Name).Bold().FontSize(13).FontColor(Colors.Blue.Darken3);
                    if (!string.IsNullOrWhiteSpace(model.Tenant.Address))
                    {
                        left.Item().Text(model.Tenant.Address).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                    }

                    var contact = string.Join(" | ", new[]
                    {
                        model.Tenant.ContactPhone,
                        model.Tenant.ContactEmail
                    }.Where(value => !string.IsNullOrWhiteSpace(value)));
                    if (!string.IsNullOrWhiteSpace(contact))
                    {
                        left.Item().Text(contact).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                    }
                });

                row.ConstantItem(240).AlignRight().Column(right =>
                {
                    right.Item().Text("FINANCE PERIOD CLOSE PACK")
                        .Bold().FontSize(16).FontColor(Colors.Blue.Darken3);
                    right.Item().Text(documentNumber).SemiBold().FontSize(10);
                    right.Item().Text(copyType.ToUpperInvariant())
                        .FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                });
            });

            column.Item().PaddingTop(7).LineHorizontal(1).LineColor(Colors.Blue.Darken3);
        });
    }

    private static void ComposeContent(
        IContainer container,
        ClosePackModel model,
        string documentNumber,
        string evidenceDigest)
    {
        container.PaddingTop(10).Column(column =>
        {
            column.Item().Element(item => ComposeStatusBanner(item, model));
            column.Item().PaddingTop(9).Element(item => ComposeCloseSummary(item, model, documentNumber));
            column.Item().Element(item => SectionTitle(item, "1. Electronically Signed Close Certificate"));
            column.Item().Element(item => ComposeCertificate(item, model));

            column.Item().Element(item => SectionTitle(item, "2. Final Control Evaluation"));
            column.Item().Element(item => ComposeCheckTable(item, model.Checks, model.Tenant.BaseCurrency));

            column.Item().Element(item => SectionTitle(item, "3. Reconciliation Summary"));
            column.Item().Element(item => ComposeReconciliationSummary(item, model));

            column.Item().Element(item => SectionTitle(item, "4. Close Checklist"));
            column.Item().Element(item => ComposeTaskTable(item, model));

            column.Item().Element(item => SectionTitle(item, "5. Exception and Waiver Register"));
            column.Item().Element(item => ComposeExceptionRegister(item, model));

            column.Item().Element(item => SectionTitle(item, "6. Evidence Register"));
            column.Item().Element(item => ComposeEvidenceRegister(item, model));

            column.Item().Element(item => SectionTitle(item, "7. Aging and Escalation Audit"));
            column.Item().Element(item => ComposeAlertRegister(item, model));

            column.Item().Element(item => SectionTitle(item, "8. Reopen and Re-close History"));
            column.Item().Element(item => ComposeHistory(item, model));

            column.Item().Element(item => SectionTitle(item, "9. Evidence Integrity and Control Notes"));
            column.Item().Element(item => ComposeIntegrityNotes(item, model, evidenceDigest));
        });
    }

    private static void ComposeStatusBanner(IContainer container, ClosePackModel model)
    {
        var reopened = model.Cycle.Status == FinanceCloseStatuses.Reopened;
        var background = reopened ? Colors.Orange.Lighten5 : Colors.Green.Lighten5;
        var border = reopened ? Colors.Orange.Darken2 : Colors.Green.Darken2;
        var title = reopened
            ? "HISTORICAL CERTIFICATE - CYCLE LATER SUPERSEDED BY REOPEN"
            : "CLOSED - MAKER-CHECKER CERTIFICATE COMPLETE";

        container.Background(background).Border(1).BorderColor(border).Padding(8).Column(column =>
        {
            column.Item().Text(title).Bold().FontSize(9).FontColor(border);
            if (reopened)
            {
                column.Item().PaddingTop(2).Text(
                    $"Reopened {DateTimeText(model.Cycle.ReopenedAt)}. Reason: {TextOrDash(model.Cycle.ReopenReason)}")
                    .FontSize(7.5f);
            }
        });
    }

    private static void ComposeCloseSummary(
        IContainer container,
        ClosePackModel model,
        string documentNumber)
    {
        var warningCount = model.Checks.Count(item =>
            item.Status is FinanceCloseCheckStatuses.Warning or FinanceCloseCheckStatuses.Waived);
        var completedTasks = model.Tasks.Count(item => item.Status == FinanceCloseTaskStatuses.Completed);

        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(9).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Element(item => MetaCell(item, "Document", documentNumber));
                row.RelativeItem().Element(item => MetaCell(item, "Period", $"{model.Period.PeriodName} ({model.Period.PeriodCode})"));
                row.RelativeItem().Element(item => MetaCell(item, "Fiscal year", model.FiscalYear?.FiscalYearCode ?? "-"));
            });
            column.Item().PaddingTop(7).Row(row =>
            {
                row.RelativeItem().Element(item => MetaCell(item, "Cycle", model.Cycle.CycleNumber.ToString()));
                row.RelativeItem().Element(item => MetaCell(item, "Template", $"{model.TemplateName} v{model.Cycle.TemplateVersion}"));
                row.RelativeItem().Element(item => MetaCell(item, "Final evaluation", model.EvaluationNumber.ToString()));
            });
            column.Item().PaddingTop(7).Row(row =>
            {
                row.RelativeItem().Element(item => MetaCell(item, "Closed", DateTimeText(model.Cycle.ClosedAt)));
                row.RelativeItem().Element(item => MetaCell(item, "Checklist", $"{completedTasks}/{model.Tasks.Count} completed"));
                row.RelativeItem().Element(item => MetaCell(item, "Warnings / waivers", warningCount.ToString()));
            });
        });
    }

    private static void ComposeCertificate(IContainer container, ClosePackModel model)
    {
        var certificate = model.Certification!;

        container.Column(column =>
        {
            column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(9).Text(
                "This certificate records system-authenticated electronic attestations. It is not a scanned handwritten signature or a third-party digital certificate. The retained user IDs, names, declarations and UTC timestamps are the authoritative approval evidence.")
                .FontSize(7.5f).FontColor(Colors.Grey.Darken1);

            column.Item().PaddingTop(7).Row(row =>
            {
                row.RelativeItem().Element(item => SignatureBox(
                    item,
                    "PREPARED BY (MAKER)",
                    certificate.PreparedByUserName,
                    certificate.PreparedByUserId,
                    certificate.PreparedAt,
                    certificate.PreparerDeclaration));
                row.ConstantItem(8);
                row.RelativeItem().Element(item => SignatureBox(
                    item,
                    "REVIEWED AND APPROVED BY (CHECKER)",
                    certificate.ApprovedByUserName ?? certificate.ReviewedByUserName,
                    certificate.ApprovedByUserId ?? certificate.ReviewedByUserId,
                    certificate.ApprovedAt ?? certificate.ReviewedAt,
                    certificate.ReviewerDeclaration));
            });

            column.Item().PaddingTop(7)
                .Background(Colors.Blue.Lighten5)
                .Border(1)
                .BorderColor(Colors.Blue.Lighten2)
                .Padding(7)
                .Text("Separation of duties: PASSED - the preparer and approver are different authenticated users.")
                .SemiBold().FontSize(7.5f).FontColor(Colors.Blue.Darken3);
        });
    }

    private static void SignatureBox(
        IContainer container,
        string role,
        string? userName,
        Guid? userId,
        DateTime? signedAt,
        string? declaration)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(9).Column(column =>
        {
            column.Item().Text(role).Bold().FontSize(8).FontColor(Colors.Blue.Darken3);
            column.Item().PaddingTop(5).Text(TextOrDash(userName)).SemiBold().FontSize(9);
            column.Item().Text($"User ID: {userId?.ToString() ?? "-"}")
                .FontSize(6.5f).FontColor(Colors.Grey.Darken1);
            column.Item().Text($"Signed: {DateTimeText(signedAt)}")
                .FontSize(7).FontColor(Colors.Grey.Darken1);
            column.Item().PaddingTop(6).Text("Declaration").SemiBold().FontSize(7);
            column.Item().PaddingTop(2).Text(TextOrDash(declaration)).FontSize(7.5f);
        });
    }

    private static void ComposeCheckTable(
        IContainer container,
        IReadOnlyList<FinanceCloseCheckSnapshot> checks,
        string currency)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2.2f);
                columns.RelativeColumn(1.2f);
                columns.ConstantColumn(52);
                columns.ConstantColumn(48);
                columns.ConstantColumn(64);
                columns.RelativeColumn(2.5f);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Control");
                header.Cell().Element(HeaderCell).Text("Category");
                header.Cell().Element(HeaderCell).Text("Severity");
                header.Cell().Element(HeaderCell).Text("Result");
                header.Cell().Element(HeaderCell).Text("Exceptions");
                header.Cell().Element(HeaderCell).Text("Final evidence summary");
            });

            foreach (var check in checks)
            {
                table.Cell().Element(BodyCell).Column(column =>
                {
                    column.Item().Text(check.Title).SemiBold().FontSize(7.2f);
                    column.Item().Text(check.CheckCode).FontSize(6.2f).FontColor(Colors.Grey.Darken1);
                });
                table.Cell().Element(BodyCell).Text(check.Category).FontSize(6.8f);
                table.Cell().Element(BodyCell).Text(check.Severity).FontSize(6.8f);
                table.Cell().Element(BodyCell).Text(check.Status).SemiBold().FontSize(6.8f)
                    .FontColor(StatusColor(check.Status));
                table.Cell().Element(BodyCell).Text(FormatExceptionMeasurement(check, currency)).FontSize(6.8f);
                table.Cell().Element(BodyCell).Text(check.ResultSummary).FontSize(6.8f);
            }
        });
    }

    private static void ComposeReconciliationSummary(IContainer container, ClosePackModel model)
    {
        var reconciliationChecks = model.Checks
            .Where(item =>
                item.CheckCode.Contains("RECONCILIATION", StringComparison.OrdinalIgnoreCase) ||
                item.CheckCode.Contains("TRIAL_BALANCE", StringComparison.OrdinalIgnoreCase) ||
                item.CheckCode.Contains("POSTING_INTEGRITY", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (reconciliationChecks.Count == 0)
        {
            container.Element(item => EmptyRegister(item, "No reconciliation controls were present in this template."));
            return;
        }

        ComposeCheckTable(container, reconciliationChecks, model.Tenant.BaseCurrency);
    }

    private static void ComposeTaskTable(IContainer container, ClosePackModel model)
    {
        var evidenceCounts = model.Evidence
            .GroupBy(item => item.FinanceCloseTaskId)
            .ToDictionary(group => group.Key, group => group.Count());

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(24);
                columns.RelativeColumn(2.5f);
                columns.RelativeColumn(1.2f);
                columns.ConstantColumn(48);
                columns.ConstantColumn(50);
                columns.RelativeColumn(1.6f);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Seq");
                header.Cell().Element(HeaderCell).Text("Task");
                header.Cell().Element(HeaderCell).Text("Category");
                header.Cell().Element(HeaderCell).Text("Type");
                header.Cell().Element(HeaderCell).Text("Status");
                header.Cell().Element(HeaderCell).Text("Owner / evidence");
            });

            foreach (var task in model.Tasks)
            {
                evidenceCounts.TryGetValue(task.Id, out var evidenceCount);
                table.Cell().Element(BodyCell).Text(task.Sequence.ToString()).FontSize(6.8f);
                table.Cell().Element(BodyCell).Column(column =>
                {
                    column.Item().Text(task.Title).SemiBold().FontSize(7.1f);
                    column.Item().Text(task.TaskCode).FontSize(6.1f).FontColor(Colors.Grey.Darken1);
                    if (!string.IsNullOrWhiteSpace(task.EvidenceSummary))
                    {
                        column.Item().PaddingTop(2).Text(task.EvidenceSummary).FontSize(6.4f);
                    }
                });
                table.Cell().Element(BodyCell).Text(task.Category).FontSize(6.7f);
                table.Cell().Element(BodyCell).Text(task.IsAutomated ? "Automated" : "Manual").FontSize(6.7f);
                table.Cell().Element(BodyCell).Text(task.Status).SemiBold().FontSize(6.7f)
                    .FontColor(StatusColor(task.Status));
                table.Cell().Element(BodyCell).Column(column =>
                {
                    column.Item().Text(TextOrDash(task.CompletedByUserName ?? task.AssignedToUserName)).FontSize(6.7f);
                    column.Item().Text($"Completed: {DateTimeText(task.CompletedAt)}").FontSize(6.1f).FontColor(Colors.Grey.Darken1);
                    column.Item().Text($"Files: {evidenceCount}").FontSize(6.1f).FontColor(Colors.Grey.Darken1);
                });
            }
        });
    }

    private static void ComposeExceptionRegister(IContainer container, ClosePackModel model)
    {
        var exceptions = model.Checks
            .Where(item => item.Status != FinanceCloseCheckStatuses.Passed &&
                item.Status != FinanceCloseCheckStatuses.NotApplicable)
            .ToList();

        container.Column(column =>
        {
            column.Item().Text("Final evaluation exceptions").SemiBold().FontSize(8);
            if (exceptions.Count == 0)
            {
                column.Item().PaddingTop(4).Element(item => EmptyRegister(
                    item,
                    "No warning, failed or waived results remained in the final evaluation."));
            }
            else
            {
                column.Item().PaddingTop(4).Element(item => ComposeCheckTable(
                    item,
                    exceptions,
                    model.Tenant.BaseCurrency));
            }

            column.Item().PaddingTop(8).Text("Waiver decision history").SemiBold().FontSize(8);
            if (model.Waivers.Count == 0)
            {
                column.Item().PaddingTop(4).Element(item => EmptyRegister(item, "No exception waivers were requested for this cycle."));
            }
            else
            {
                column.Item().PaddingTop(4).Element(item => ComposeWaiverTable(item, model));
            }
        });
    }

    private static void ComposeWaiverTable(IContainer container, ClosePackModel model)
    {
        var evidenceById = model.Evidence.ToDictionary(item => item.Id);

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.3f);
                columns.ConstantColumn(52);
                columns.RelativeColumn(2.3f);
                columns.RelativeColumn(1.4f);
                columns.RelativeColumn(1.8f);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Control");
                header.Cell().Element(HeaderCell).Text("Decision");
                header.Cell().Element(HeaderCell).Text("Justification");
                header.Cell().Element(HeaderCell).Text("Requested");
                header.Cell().Element(HeaderCell).Text("Review / evidence");
            });

            foreach (var waiver in model.Waivers)
            {
                evidenceById.TryGetValue(waiver.FinanceCloseEvidenceAttachmentId, out var evidence);
                table.Cell().Element(BodyCell).Column(column =>
                {
                    column.Item().Text(waiver.CheckCode).SemiBold().FontSize(6.8f);
                    column.Item().Text($"Fingerprint: {AbbreviateHash(waiver.EvidenceFingerprint)}")
                        .FontSize(5.8f).FontColor(Colors.Grey.Darken1);
                });
                table.Cell().Element(BodyCell).Text(waiver.Status).SemiBold().FontSize(6.7f)
                    .FontColor(StatusColor(waiver.Status));
                table.Cell().Element(BodyCell).Text(waiver.Justification).FontSize(6.5f);
                table.Cell().Element(BodyCell).Column(column =>
                {
                    column.Item().Text(waiver.RequestedByUserName).FontSize(6.5f);
                    column.Item().Text(DateTimeText(waiver.RequestedAt)).FontSize(5.9f).FontColor(Colors.Grey.Darken1);
                });
                table.Cell().Element(BodyCell).Column(column =>
                {
                    column.Item().Text(TextOrDash(waiver.ReviewedByUserName)).FontSize(6.5f);
                    column.Item().Text(DateTimeText(waiver.ReviewedAt)).FontSize(5.9f).FontColor(Colors.Grey.Darken1);
                    column.Item().Text(TextOrDash(waiver.ReviewComment)).FontSize(6.2f);
                    column.Item().Text($"File: {evidence?.FileUploadRecord.OriginalFileName ?? "-"}")
                        .FontSize(5.9f).FontColor(Colors.Grey.Darken1);
                });
            }
        });
    }

    private static void ComposeEvidenceRegister(IContainer container, ClosePackModel model)
    {
        if (model.Evidence.Count == 0)
        {
            container.Element(item => EmptyRegister(item, "No controlled file attachments were linked to this close cycle."));
            return;
        }

        var tasksById = model.Tasks.ToDictionary(item => item.Id);
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(2.2f);
                columns.ConstantColumn(70);
                columns.ConstantColumn(54);
                columns.RelativeColumn(1.5f);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Task");
                header.Cell().Element(HeaderCell).Text("Controlled file");
                header.Cell().Element(HeaderCell).Text("Evidence type");
                header.Cell().Element(HeaderCell).Text("Size / scan");
                header.Cell().Element(HeaderCell).Text("Retained metadata");
            });

            foreach (var evidence in model.Evidence)
            {
                tasksById.TryGetValue(evidence.FinanceCloseTaskId, out var task);
                var file = evidence.FileUploadRecord;
                table.Cell().Element(BodyCell).Column(column =>
                {
                    column.Item().Text(task?.Title ?? "Unknown task").FontSize(6.6f);
                    column.Item().Text(task?.TaskCode ?? "-").FontSize(5.9f).FontColor(Colors.Grey.Darken1);
                });
                table.Cell().Element(BodyCell).Column(column =>
                {
                    column.Item().Text(file.OriginalFileName).SemiBold().FontSize(6.8f);
                    column.Item().Text(TextOrDash(evidence.Description)).FontSize(6.1f);
                    column.Item().Text($"File ID: {file.Id}").FontSize(5.5f).FontColor(Colors.Grey.Darken1);
                });
                table.Cell().Element(BodyCell).Text(evidence.EvidenceType).FontSize(6.4f);
                table.Cell().Element(BodyCell).Column(column =>
                {
                    column.Item().Text(FileSize(file.FileSize)).FontSize(6.4f);
                    column.Item().Text(file.VirusScanStatus.ToString()).FontSize(6.1f);
                });
                table.Cell().Element(BodyCell).Column(column =>
                {
                    column.Item().Text($"Uploaded: {DateTimeText(file.CreatedAt)}").FontSize(6.1f);
                    column.Item().Text($"User ID: {file.UploadedByUserId}").FontSize(5.5f).FontColor(Colors.Grey.Darken1);
                });
            }
        });
    }

    private static void ComposeAlertRegister(IContainer container, ClosePackModel model)
    {
        if (model.AlertDeliveries.Count == 0)
        {
            container.Element(item => EmptyRegister(item, "No aging or escalation alerts were due for this close cycle."));
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(1.5f);
                columns.ConstantColumn(54);
                columns.RelativeColumn(1.5f);
                columns.RelativeColumn(1.7f);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Alert");
                header.Cell().Element(HeaderCell).Text("Recipient");
                header.Cell().Element(HeaderCell).Text("Status");
                header.Cell().Element(HeaderCell).Text("Control deadline");
                header.Cell().Element(HeaderCell).Text("Delivery audit");
            });

            foreach (var alert in model.AlertDeliveries)
            {
                table.Cell().Element(BodyCell).Text(alert.AlertType).SemiBold().FontSize(6.6f);
                table.Cell().Element(BodyCell).Column(column =>
                {
                    column.Item().Text(alert.RecipientUserName).FontSize(6.5f);
                    column.Item().Text(alert.RecipientUserId.ToString()).FontSize(5.5f).FontColor(Colors.Grey.Darken1);
                });
                table.Cell().Element(BodyCell).Text(alert.Status).SemiBold().FontSize(6.5f)
                    .FontColor(StatusColor(alert.Status));
                table.Cell().Element(BodyCell).Text(DateTimeText(alert.DueAtUtc)).FontSize(6.2f);
                table.Cell().Element(BodyCell).Column(column =>
                {
                    column.Item().Text($"Delivered: {DateTimeText(alert.DeliveredAtUtc)}").FontSize(6.1f);
                    column.Item().Text($"Attempts: {alert.AttemptCount}").FontSize(6.1f);
                    if (!string.IsNullOrWhiteSpace(alert.LastError))
                    {
                        column.Item().Text($"Last error: {alert.LastError}").FontSize(5.9f).FontColor(Colors.Red.Darken2);
                    }
                });
            }
        });
    }

    private static void ComposeHistory(IContainer container, ClosePackModel model)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(38);
                columns.ConstantColumn(58);
                columns.RelativeColumn(1.3f);
                columns.RelativeColumn(1.3f);
                columns.RelativeColumn(1.3f);
                columns.RelativeColumn(2.2f);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Cycle");
                header.Cell().Element(HeaderCell).Text("Status");
                header.Cell().Element(HeaderCell).Text("Started");
                header.Cell().Element(HeaderCell).Text("Closed");
                header.Cell().Element(HeaderCell).Text("Reopened");
                header.Cell().Element(HeaderCell).Text("Reopen reason");
            });

            foreach (var cycle in model.History)
            {
                table.Cell().Element(BodyCell).Text(cycle.CycleNumber.ToString()).SemiBold().FontSize(6.7f);
                table.Cell().Element(BodyCell).Text(cycle.Status).SemiBold().FontSize(6.6f)
                    .FontColor(StatusColor(cycle.Status));
                table.Cell().Element(BodyCell).Text(DateTimeText(cycle.StartedAt)).FontSize(6.1f);
                table.Cell().Element(BodyCell).Text(DateTimeText(cycle.ClosedAt)).FontSize(6.1f);
                table.Cell().Element(BodyCell).Text(DateTimeText(cycle.ReopenedAt)).FontSize(6.1f);
                table.Cell().Element(BodyCell).Text(TextOrDash(cycle.ReopenReason)).FontSize(6.3f);
            }
        });
    }

    private static void ComposeIntegrityNotes(
        IContainer container,
        ClosePackModel model,
        string evidenceDigest)
    {
        var nonWaivableResults = model.Checks
            .Where(item => NonWaivableControlCodes.Contains(item.CheckCode))
            .OrderBy(item => item.CheckCode)
            .Select(item => $"{item.CheckCode}: {item.Status}")
            .ToList();

        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(9).Column(column =>
        {
            column.Item().Text("Evidence digest (SHA-256)").SemiBold().FontSize(7.5f);
            column.Item().PaddingTop(2).Text(evidenceDigest).FontFamily("Courier New").FontSize(6.8f);
            column.Item().PaddingTop(5).Text(
                "The digest is calculated from canonical, sorted close evidence and excludes the generation timestamp and copy label. Original and reprint copies of unchanged retained evidence therefore show the same digest.")
                .FontSize(6.8f).FontColor(Colors.Grey.Darken1);

            column.Item().PaddingTop(7).Text("FIN-LIM-0034 non-waivable integrity controls").SemiBold().FontSize(7.5f);
            column.Item().PaddingTop(2).Text(nonWaivableResults.Count == 0
                    ? "The selected template contained no mapped non-waivable control results."
                    : string.Join(" | ", nonWaivableResults))
                .FontSize(6.8f);
            column.Item().PaddingTop(3).Text(
                "Posting integrity, trial balance, AP and AR control reconciliation, recurring-journal exceptions and fixed-asset depreciation cannot be accepted through the generic close-waiver workflow. Any applicable mandatory failure blocks certification and pack generation.")
                .FontSize(6.8f).FontColor(Colors.Grey.Darken1);
        });
    }

    private static void ComposeFooter(
        IContainer container,
        DateTime generatedAtUtc,
        string? generatedBy)
    {
        container.BorderTop(1).BorderColor(Colors.Grey.Lighten3).PaddingTop(5).Row(row =>
        {
            row.RelativeItem()
                .DefaultTextStyle(style => style.FontSize(6.5f).FontColor(Colors.Grey.Darken1))
                .Text(text =>
            {
                text.Span($"Generated {generatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
                if (!string.IsNullOrWhiteSpace(generatedBy))
                {
                    text.Span($" by {generatedBy.Trim()}");
                }
            });

            row.ConstantItem(100)
                .AlignRight()
                .DefaultTextStyle(style => style.FontSize(6.5f).FontColor(Colors.Grey.Darken1))
                .Text(text =>
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        });
    }

    private static string BuildEvidenceDigest(ClosePackModel model)
    {
        // Every collection is explicitly ordered and every value is JSON-escaped before it is
        // appended. This compact canonical stream avoids delimiter ambiguity while keeping the
        // compiler and runtime work predictable for large close packs. It deliberately excludes
        // render time, PDF metadata and the Original/Reprint label.
        var canonical = new StringBuilder(8192);
        AppendCanonical(canonical, "tenant.id", model.Tenant.Id);

        AppendCanonical(canonical, "cycle.id", model.Cycle.Id);
        AppendCanonical(canonical, "cycle.periodId", model.Cycle.FiscalPeriodId);
        AppendCanonical(canonical, "cycle.number", model.Cycle.CycleNumber);
        AppendCanonical(canonical, "cycle.templateCode", model.Cycle.TemplateCode);
        AppendCanonical(canonical, "cycle.templateVersion", model.Cycle.TemplateVersion);
        AppendCanonical(canonical, "cycle.closeType", model.Cycle.CloseType);
        AppendCanonical(canonical, "cycle.status", model.Cycle.Status);
        AppendCanonical(canonical, "cycle.evaluationCount", model.Cycle.EvaluationCount);
        AppendCanonical(canonical, "cycle.startedAt", model.Cycle.StartedAt);
        AppendCanonical(canonical, "cycle.preparedAt", model.Cycle.PreparedAt);
        AppendCanonical(canonical, "cycle.closedAt", model.Cycle.ClosedAt);
        AppendCanonical(canonical, "cycle.reopenedAt", model.Cycle.ReopenedAt);
        AppendCanonical(canonical, "cycle.reopenedBy", model.Cycle.ReopenedByUserId);
        AppendCanonical(canonical, "cycle.reopenReason", model.Cycle.ReopenReason);

        AppendCanonical(canonical, "period.id", model.Period.Id);
        AppendCanonical(canonical, "period.code", model.Period.PeriodCode);
        AppendCanonical(canonical, "period.name", model.Period.PeriodName);
        AppendCanonical(canonical, "period.start", model.Period.StartDate);
        AppendCanonical(canonical, "period.end", model.Period.EndDate);
        AppendCanonical(canonical, "period.notes", model.Period.ClosingNotes);
        AppendCanonical(canonical, "period.debits", model.Period.TotalDebits);
        AppendCanonical(canonical, "period.credits", model.Period.TotalCredits);
        AppendCanonical(canonical, "period.difference", model.Period.BalanceDifference);

        if (model.Certification != null)
        {
            var certificate = model.Certification;
            AppendCanonical(canonical, "certificate.id", certificate.Id);
            AppendCanonical(canonical, "certificate.preparedBy", certificate.PreparedByUserId);
            AppendCanonical(canonical, "certificate.preparedName", certificate.PreparedByUserName);
            AppendCanonical(canonical, "certificate.preparedAt", certificate.PreparedAt);
            AppendCanonical(canonical, "certificate.preparerDeclaration", certificate.PreparerDeclaration);
            AppendCanonical(canonical, "certificate.reviewedBy", certificate.ReviewedByUserId);
            AppendCanonical(canonical, "certificate.reviewedName", certificate.ReviewedByUserName);
            AppendCanonical(canonical, "certificate.reviewedAt", certificate.ReviewedAt);
            AppendCanonical(canonical, "certificate.reviewerDeclaration", certificate.ReviewerDeclaration);
            AppendCanonical(canonical, "certificate.approvedBy", certificate.ApprovedByUserId);
            AppendCanonical(canonical, "certificate.approvedName", certificate.ApprovedByUserName);
            AppendCanonical(canonical, "certificate.approvedAt", certificate.ApprovedAt);
            AppendCanonical(canonical, "certificate.superseded", certificate.IsSuperseded);
            AppendCanonical(canonical, "certificate.supersededAt", certificate.SupersededAt);
            AppendCanonical(canonical, "certificate.supersededReason", certificate.SupersededReason);
        }

        foreach (var task in model.Tasks.OrderBy(item => item.Sequence).ThenBy(item => item.TaskCode))
        {
            var prefix = $"task.{task.Id}.";
            AppendCanonical(canonical, prefix + "code", task.TaskCode);
            AppendCanonical(canonical, prefix + "title", task.Title);
            AppendCanonical(canonical, prefix + "category", task.Category);
            AppendCanonical(canonical, prefix + "dependency", task.DependsOnTaskCode);
            AppendCanonical(canonical, prefix + "check", task.CheckCode);
            AppendCanonical(canonical, prefix + "sequence", task.Sequence);
            AppendCanonical(canonical, prefix + "mandatory", task.IsMandatory);
            AppendCanonical(canonical, prefix + "automated", task.IsAutomated);
            AppendCanonical(canonical, prefix + "status", task.Status);
            AppendCanonical(canonical, prefix + "assignedTo", task.AssignedToUserId);
            AppendCanonical(canonical, prefix + "dueAt", task.DueAt);
            AppendCanonical(canonical, prefix + "completedAt", task.CompletedAt);
            AppendCanonical(canonical, prefix + "completedBy", task.CompletedByUserId);
            AppendCanonical(canonical, prefix + "evidenceSummary", task.EvidenceSummary);
        }

        foreach (var check in model.Checks.OrderBy(item => item.CheckCode))
        {
            var prefix = $"check.{check.Id}.";
            AppendCanonical(canonical, prefix + "evaluation", check.EvaluationNumber);
            AppendCanonical(canonical, prefix + "code", check.CheckCode);
            AppendCanonical(canonical, prefix + "title", check.Title);
            AppendCanonical(canonical, prefix + "category", check.Category);
            AppendCanonical(canonical, prefix + "severity", check.Severity);
            AppendCanonical(canonical, prefix + "status", check.Status);
            AppendCanonical(canonical, prefix + "summary", check.ResultSummary);
            AppendCanonical(canonical, prefix + "exceptionCount", check.ExceptionCount);
            AppendCanonical(canonical, prefix + "exceptionAmount", check.ExceptionAmount);
            AppendCanonical(canonical, prefix + "evidence", check.EvidenceJson);
            AppendCanonical(canonical, prefix + "fingerprint", check.EvidenceFingerprint);
            AppendCanonical(canonical, prefix + "waiver", check.AppliedWaiverId);
            AppendCanonical(canonical, prefix + "evaluatedAt", check.EvaluatedAt);
            AppendCanonical(canonical, prefix + "evaluatedBy", check.EvaluatedByUserId);
        }

        foreach (var evidence in model.Evidence.OrderBy(item => item.Id))
        {
            var prefix = $"evidence.{evidence.Id}.";
            var file = evidence.FileUploadRecord;
            AppendCanonical(canonical, prefix + "task", evidence.FinanceCloseTaskId);
            AppendCanonical(canonical, prefix + "fileId", evidence.FileUploadRecordId);
            AppendCanonical(canonical, prefix + "type", evidence.EvidenceType);
            AppendCanonical(canonical, prefix + "description", evidence.Description);
            AppendCanonical(canonical, prefix + "linkedAt", evidence.CreatedAt);
            AppendCanonical(canonical, prefix + "fileName", file.OriginalFileName);
            AppendCanonical(canonical, prefix + "fileSize", file.FileSize);
            AppendCanonical(canonical, prefix + "contentType", file.ContentType);
            AppendCanonical(canonical, prefix + "uploadedBy", file.UploadedByUserId);
            AppendCanonical(canonical, prefix + "scanStatus", file.VirusScanStatus);
            AppendCanonical(canonical, prefix + "scannedAt", file.ScannedAtUtc);
            AppendCanonical(canonical, prefix + "uploadedAt", file.CreatedAt);
        }

        foreach (var waiver in model.Waivers.OrderBy(item => item.Id))
        {
            var prefix = $"waiver.{waiver.Id}.";
            AppendCanonical(canonical, prefix + "task", waiver.FinanceCloseTaskId);
            AppendCanonical(canonical, prefix + "snapshot", waiver.FinanceCloseCheckSnapshotId);
            AppendCanonical(canonical, prefix + "evidence", waiver.FinanceCloseEvidenceAttachmentId);
            AppendCanonical(canonical, prefix + "code", waiver.CheckCode);
            AppendCanonical(canonical, prefix + "fingerprint", waiver.EvidenceFingerprint);
            AppendCanonical(canonical, prefix + "status", waiver.Status);
            AppendCanonical(canonical, prefix + "justification", waiver.Justification);
            AppendCanonical(canonical, prefix + "requestedBy", waiver.RequestedByUserId);
            AppendCanonical(canonical, prefix + "requestedAt", waiver.RequestedAt);
            AppendCanonical(canonical, prefix + "reviewedBy", waiver.ReviewedByUserId);
            AppendCanonical(canonical, prefix + "reviewedAt", waiver.ReviewedAt);
            AppendCanonical(canonical, prefix + "reviewComment", waiver.ReviewComment);
        }

        foreach (var alert in model.AlertDeliveries.OrderBy(item => item.Id))
        {
            var prefix = $"alert.{alert.Id}.";
            AppendCanonical(canonical, prefix + "task", alert.FinanceCloseTaskId);
            AppendCanonical(canonical, prefix + "waiver", alert.FinanceCloseExceptionWaiverId);
            AppendCanonical(canonical, prefix + "type", alert.AlertType);
            AppendCanonical(canonical, prefix + "dedupe", alert.DedupeKey);
            AppendCanonical(canonical, prefix + "recipient", alert.RecipientUserId);
            AppendCanonical(canonical, prefix + "status", alert.Status);
            AppendCanonical(canonical, prefix + "dueAt", alert.DueAtUtc);
            AppendCanonical(canonical, prefix + "deliveredAt", alert.DeliveredAtUtc);
            AppendCanonical(canonical, prefix + "attempts", alert.AttemptCount);
            AppendCanonical(canonical, prefix + "notification", alert.NotificationId);
            AppendCanonical(canonical, prefix + "lastError", alert.LastError);
        }

        foreach (var history in model.History.OrderBy(item => item.CycleNumber))
        {
            var prefix = $"history.{history.Id}.";
            AppendCanonical(canonical, prefix + "cycle", history.CycleNumber);
            AppendCanonical(canonical, prefix + "status", history.Status);
            AppendCanonical(canonical, prefix + "startedAt", history.StartedAt);
            AppendCanonical(canonical, prefix + "preparedAt", history.PreparedAt);
            AppendCanonical(canonical, prefix + "closedAt", history.ClosedAt);
            AppendCanonical(canonical, prefix + "reopenedAt", history.ReopenedAt);
            AppendCanonical(canonical, prefix + "reopenedBy", history.ReopenedByUserId);
            AppendCanonical(canonical, prefix + "reason", history.ReopenReason);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private static void AppendCanonical(StringBuilder canonical, string key, object? value)
    {
        canonical.Append(key)
            .Append('=')
            .Append(JsonSerializer.Serialize(value, DigestJsonOptions))
            .Append('\n');
    }

    private static void SectionTitle(IContainer container, string title)
    {
        container.PaddingTop(13).PaddingBottom(4).Column(column =>
        {
            column.Item().Text(title).Bold().FontSize(10).FontColor(Colors.Blue.Darken3);
            column.Item().PaddingTop(2).LineHorizontal(0.6f).LineColor(Colors.Blue.Lighten2);
        });
    }

    private static void MetaCell(IContainer container, string label, string value)
    {
        container.Column(column =>
        {
            column.Item().Text(label).FontSize(6.5f).FontColor(Colors.Grey.Darken1);
            column.Item().Text(value).SemiBold().FontSize(7.6f);
        });
    }

    private static void EmptyRegister(IContainer container, string message)
    {
        container.Background(Colors.Grey.Lighten5)
            .Border(1)
            .BorderColor(Colors.Grey.Lighten3)
            .Padding(7)
            .Text(message)
            .FontSize(7)
            .FontColor(Colors.Grey.Darken1);
    }

    private static IContainer HeaderCell(IContainer container)
        => container.DefaultTextStyle(style => style.SemiBold().FontSize(6.8f))
            .Background(Colors.Grey.Lighten3)
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(4);

    private static IContainer BodyCell(IContainer container)
        => container.BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten3)
            .Padding(4);

    private static string BuildDocumentNumber(string? periodCode, int cycleNumber)
        => $"FCP-{SafeFileName(periodCode, "PERIOD")}-C{cycleNumber:00}".ToUpperInvariant();

    private static string NormalizeCopyType(string? copyType)
    {
        var value = copyType?.Trim();
        return string.Equals(value, "Reprint", StringComparison.OrdinalIgnoreCase)
            ? "Reprint"
            : "Original";
    }

    private static string FormatExceptionMeasurement(FinanceCloseCheckSnapshot check, string currency)
    {
        if (check.ExceptionAmount.HasValue)
        {
            return $"{check.ExceptionCount:N0} | {currency} {check.ExceptionAmount.Value:N2}";
        }

        return check.ExceptionCount.ToString("N0");
    }

    private static string DateTimeText(DateTime? value)
        => value.HasValue ? $"{value.Value:yyyy-MM-dd HH:mm:ss} UTC" : "-";

    private static string TextOrDash(string? value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private static string AbbreviateHash(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.Length <= 16
                ? value
                : $"{value[..8]}...{value[^8..]}";

    private static string FileSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        if (bytes < 1024 * 1024)
        {
            return $"{bytes / 1024d:N1} KB";
        }

        return $"{bytes / (1024d * 1024d):N1} MB";
    }

    private static string SafeFileName(string? value, string fallback = "finance-close-pack")
    {
        var fileName = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidCharacter, '-');
        }

        return fileName;
    }

    private static string StatusColor(string status)
        => status switch
        {
            FinanceCloseCheckStatuses.Passed => Colors.Green.Darken2,
            FinanceCloseCheckStatuses.NotApplicable => Colors.Grey.Darken1,
            FinanceCloseCheckStatuses.Warning => Colors.Orange.Darken2,
            FinanceCloseCheckStatuses.Waived => Colors.Blue.Darken2,
            FinanceCloseCheckStatuses.Failed => Colors.Red.Darken2,
            FinanceCloseTaskStatuses.Completed => Colors.Green.Darken2,
            FinanceCloseTaskStatuses.Blocked => Colors.Red.Darken2,
            FinanceCloseWaiverStatuses.Approved => Colors.Blue.Darken2,
            FinanceCloseWaiverStatuses.Rejected => Colors.Red.Darken2,
            FinanceCloseAlertDeliveryStatuses.Delivered => Colors.Green.Darken2,
            FinanceCloseStatuses.Closed => Colors.Green.Darken2,
            FinanceCloseStatuses.Reopened => Colors.Orange.Darken2,
            _ => Colors.Grey.Darken2
        };

    private sealed record ClosePackModel(
        Tenant Tenant,
        FinanceCloseCycle Cycle,
        FiscalPeriod Period,
        FiscalYear? FiscalYear,
        FinanceCloseCertification? Certification,
        IReadOnlyList<FinanceCloseTask> Tasks,
        IReadOnlyList<FinanceCloseCheckSnapshot> Checks,
        IReadOnlyList<FinanceCloseEvidenceAttachment> Evidence,
        IReadOnlyList<FinanceCloseExceptionWaiver> Waivers,
        IReadOnlyList<FinanceCloseAlertDelivery> AlertDeliveries,
        IReadOnlyList<FinanceCloseCycle> History,
        int EvaluationNumber)
    {
        public string TemplateName => Cycle.FinanceCloseTemplate?.Name ?? Cycle.TemplateCode;
    }
}
