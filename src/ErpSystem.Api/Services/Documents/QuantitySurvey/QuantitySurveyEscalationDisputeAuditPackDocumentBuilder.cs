using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Documents;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.Documents;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Colors = QuestPDF.Helpers.Colors;

namespace ErpSystem.Api.Services.Documents.QuantitySurvey;

public sealed class QuantitySurveyEscalationDisputeAuditPackDocumentBuilder(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IProjectService projectService,
    ICentralDocumentRepositoryFileService centralDocuments) : IDocumentBuilder
{
    private const long MaximumZipEvidenceBytes = 50 * 1024 * 1024;

    public string DocumentType => DocumentTypes.QuantitySurveyEscalationDisputeAuditPack;
    public bool RequiresEntityId => true;
    public bool SupportsFormat(string format) => format is "pdf" or "zip";

    public async Task<RenderedDocumentDto> RenderAsync(DocumentRenderRequestDto request, CancellationToken cancellationToken = default)
    {
        var tenantId = currentUser.TenantId is { } tenant && tenant != Guid.Empty
            ? tenant : throw new UnauthorizedAccessException("A valid tenant context is required.");
        var userId = Guid.TryParse(currentUser.UserId, out var actor) && actor != Guid.Empty
            ? actor : throw new UnauthorizedAccessException("An authenticated user is required.");
        var model = await LoadAsync(request.EntityId, tenantId, cancellationToken);
        if (await projectService.GetProjectByIdAsync(model.Dispute.ProjectId) is null)
            throw new UnauthorizedAccessException("You are not permitted to export this project's dispute evidence.");
        if (model.Dispute.Status != "Resolved" || model.Dispute.Outcome is null || model.Dispute.ResolvedAt is null)
            throw new QuantitySurveyEscalationDisputeConflictException("Resolve the dispute before generating its final audit pack.");

        var generatedAt = DateTime.UtcNow;
        var generatedBy = string.IsNullOrWhiteSpace(currentUser.UserName) ? userId.ToString() : currentUser.UserName.Trim();
        var pdf = BuildPdf(model, generatedAt, generatedBy);
        var format = request.Format.ToLowerInvariant();
        byte[] output;
        string contentType;
        string fileName;
        if (format == "zip")
        {
            output = await BuildZipAsync(model, pdf, generatedAt, generatedBy, cancellationToken);
            contentType = "application/zip";
            fileName = SafeFileName(model.Dispute.DisputeReference) + "-audit-pack.zip";
        }
        else
        {
            output = pdf;
            contentType = "application/pdf";
            fileName = SafeFileName(model.Dispute.DisputeReference) + "-audit-pack.pdf";
        }

        var outputHash = Sha256(output);
        var correlationId = request.Options?.GetValueOrDefault("correlationId")?.Trim();
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId, UserId = userId, Username = generatedBy,
            Action = QuantitySurveyAuditEventMap.ExportEscalationDisputeAuditPack,
            Resource = nameof(QuantitySurveyEscalationDispute), ResourceId = model.Dispute.Id.ToString(),
            NewValues = JsonSerializer.Serialize(new
            {
                correlationId = string.IsNullOrWhiteSpace(correlationId) ? "qs-0304-export" : correlationId,
                model.Dispute.DisputeReference, Format = format, OutputSha256 = outputHash,
                AttachmentIds = model.Dispute.Attachments.Select(value => value.Id), GeneratedAtUtc = generatedAt
            }),
            IpAddress = "api", UserAgent = "QS-0304", Timestamp = generatedAt,
            CreatedAt = generatedAt, CreatedBy = generatedBy, CreatedById = userId
        });
        await db.SaveChangesAsync(cancellationToken);

        return new RenderedDocumentDto
        {
            Content = output, ContentType = contentType, FileName = fileName,
            DocumentType = DocumentType, EntityId = request.EntityId, Format = format
        };
    }

    private async Task<AuditPackModel> LoadAsync(Guid id, Guid tenantId, CancellationToken cancellationToken)
    {
        var dispute = await db.QuantitySurveyEscalationDisputes.AsNoTracking()
            .Include(value => value.CalculationRun).ThenInclude(value => value.Lines)
            .Include(value => value.Project).Include(value => value.Contract)
            .Include(value => value.Attachments)
            .FirstOrDefaultAsync(value => value.TenantId == tenantId && value.Id == id && !value.IsDeleted,
                cancellationToken)
            ?? throw new QuantitySurveyEscalationDisputeNotFoundException("The escalation dispute was not found.");
        var revisions = await db.QuantitySurveyEscalationDisputeRevisions.AsNoTracking()
            .Where(value => value.TenantId == tenantId && value.DisputeId == id && !value.IsDeleted)
            .OrderBy(value => value.CreatedAt).ThenBy(value => value.Id).ToListAsync(cancellationToken);
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(value => value.Id == tenantId && !value.IsDeleted, cancellationToken)
            ?? throw new QuantitySurveyEscalationDisputeConflictException("The tenant profile is unavailable for this audit pack.");
        var actorIds = revisions.Select(value => value.ActorUserId)
            .Append(dispute.OpenedById).Append(dispute.ContractorRespondedById ?? Guid.Empty)
            .Append(dispute.ResolvedById ?? Guid.Empty).Append(dispute.CalculationRun.ReviewedById ?? Guid.Empty)
            .Append(dispute.CalculationRun.ApprovedById ?? Guid.Empty).Where(value => value != Guid.Empty).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(value => actorIds.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, value => value.FullName ?? value.UserName ?? value.Id.ToString(), cancellationToken);
        return new AuditPackModel(tenant, dispute, revisions, users);
    }

    private static byte[] BuildPdf(AuditPackModel model, DateTime generatedAt, string generatedBy)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(24);
            page.DefaultTextStyle(style => style.FontSize(8).FontFamily(Fonts.Arial));
            page.Header().Column(column =>
            {
                column.Item().Text(model.Tenant.Name).Bold().FontSize(13).FontColor(Colors.Blue.Darken3);
                column.Item().Text("QUANTITY SURVEY ESCALATION DISPUTE AUDIT PACK").Bold().FontSize(11);
                column.Item().Text($"{model.Dispute.DisputeReference} · {model.Dispute.Status}").FontSize(9);
                column.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            });
            page.Content().PaddingVertical(10).Column(column =>
            {
                column.Spacing(8);
                column.Item().Element(container => Section(container, "Dispute and controlled lineage", section =>
                {
                    Meta(section, "Project", $"{model.Dispute.Project.ProjectCode} · {model.Dispute.Project.Title}");
                    Meta(section, "Works contract", $"{model.Dispute.Contract.ContractNumber} · {model.Dispute.Contract.ContractTitle}");
                    Meta(section, "Contractor", $"{model.Dispute.ContractorNameSnapshot} ({model.Dispute.ContractorBusinessPartnerId})");
                    Meta(section, "Calculation", $"{model.Dispute.CalculationRun.RunReference} · formula {model.Dispute.CalculationRun.FormulaCodeSnapshot}/v{model.Dispute.CalculationRun.FormulaVersionSnapshot}");
                    Meta(section, "Calculation snapshot SHA-256", model.Dispute.CalculationSnapshotHash);
                    Meta(section, "Impact target", $"{model.Dispute.CalculationRun.ImpactTargetReferenceSnapshot} · {model.Dispute.CalculationRun.ImpactTargetStatusSnapshot}");
                }));
                column.Item().Element(container => Section(container, "Calculation inputs and approved review", section =>
                {
                    Meta(section, "Index period", $"{model.Dispute.CalculationRun.BaseIndexPeriod:yyyy-MM} to {model.Dispute.CalculationRun.CurrentIndexPeriod:yyyy-MM}");
                    Meta(section, "Base / revised rate", $"{model.Dispute.CalculationRun.CurrencyCode} {model.Dispute.CalculationRun.BaseRate:N2} / {model.Dispute.CalculationRun.RevisedRate:N2}");
                    Meta(section, "Adjustment factor", model.Dispute.CalculationRun.AdjustmentFactor.ToString("N12"));
                    Meta(section, "Calculated fluctuation", $"{model.Dispute.CalculationRun.CurrencyCode} {model.Dispute.CalculationRun.CalculatedFluctuationAmount:N2}");
                    Meta(section, "Reviewer adjustment", $"{model.Dispute.CalculationRun.CurrencyCode} {model.Dispute.CalculationRun.ReviewerAdjustmentAmount:N2} · {Text(model.Dispute.CalculationRun.ReviewerAdjustmentReason)}");
                    Meta(section, "Approved impact", $"{model.Dispute.CalculationRun.CurrencyCode} {model.Dispute.CalculationRun.ApprovedImpactAmount:N2}");
                    Meta(section, "Reviewed / approved", $"{User(model, model.Dispute.CalculationRun.ReviewedById)} / {User(model, model.Dispute.CalculationRun.ApprovedById)}");
                }));
                column.Item().Element(container => ComposeIndexTable(container, model.Dispute.CalculationRun.Lines));
                column.Item().Element(container => Section(container, "Dispute, contractor response, and outcome", section =>
                {
                    Meta(section, "Subject", model.Dispute.Subject);
                    Meta(section, "Dispute reason", model.Dispute.DisputeReason);
                    Meta(section, "Opened", $"{User(model, model.Dispute.OpenedById)} · {model.Dispute.OpenedAt:u}");
                    Meta(section, "Contractor response", Text(model.Dispute.ContractorResponse));
                    Meta(section, "Response recorded", $"{User(model, model.Dispute.ContractorRespondedById)} · {Date(model.Dispute.ContractorRespondedAt)}");
                    Meta(section, "Outcome", model.Dispute.Outcome?.ToString() ?? "-");
                    Meta(section, "Resolution notes", Text(model.Dispute.ResolutionNotes));
                    Meta(section, "Resolved", $"{User(model, model.Dispute.ResolvedById)} · {Date(model.Dispute.ResolvedAt)}");
                    Meta(section, "SOD", "Resolver is independent of the opener and contractor-response recorder.");
                }));
                column.Item().Element(container => ComposeAttachmentTable(container, model.Dispute.Attachments));
                column.Item().Element(container => ComposeRevisionTable(container, model.Revisions));
            });
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span($"Generated {generatedAt:u} by {generatedBy} · Page ");
                text.CurrentPageNumber(); text.Span(" of "); text.TotalPages();
            });
        })).GeneratePdf();
    }

    private async Task<byte[]> BuildZipAsync(AuditPackModel model, byte[] pdf, DateTime generatedAt, string generatedBy, CancellationToken cancellationToken)
    {
        await using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            await WriteEntryAsync(archive, "audit-pack.pdf", pdf, cancellationToken);
            var evidence = new List<object>();
            long totalEvidenceBytes = 0;
            var sequence = 0;
            foreach (var attachment in model.Dispute.Attachments.Where(value => !value.IsDeleted).OrderBy(value => value.CreatedAt))
            {
                sequence++;
                await using var content = await centralDocuments.OpenAsync(model.Tenant.Id, attachment.CentralDocumentRecordId,
                    attachment.CentralDocumentVersionId, cancellationToken)
                    ?? throw new QuantitySurveyEscalationDisputeConflictException($"Central-DMS evidence '{attachment.Title}' is unavailable.");
                await using var buffer = new MemoryStream();
                await content.Content.CopyToAsync(buffer, cancellationToken);
                var bytes = buffer.ToArray();
                totalEvidenceBytes += bytes.LongLength;
                if (totalEvidenceBytes > MaximumZipEvidenceBytes)
                    throw new QuantitySurveyEscalationDisputeConflictException("The dispute evidence exceeds the 50 MB audit-pack export limit.");
                var checksum = Sha256(bytes);
                if (!FixedEquals(checksum, attachment.ChecksumSha256) || bytes.LongLength != attachment.FileSize)
                    throw new QuantitySurveyEscalationDisputeConflictException($"Central-DMS evidence '{attachment.Title}' failed its retained checksum or size check.");
                var entryName = $"evidence/{sequence:00}-{SafeFileName(attachment.OriginalFileName)}";
                await WriteEntryAsync(archive, entryName, bytes, cancellationToken);
                evidence.Add(new { attachment.Id, attachment.AttachmentType, attachment.Title, FileName = entryName, attachment.ContentType, attachment.FileSize, Sha256 = checksum, attachment.CentralDocumentRecordId, attachment.CentralDocumentVersionId });
            }
            var manifest = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1, model.Dispute.DisputeReference, model.Dispute.Id,
                calculationRunId = model.Dispute.CalculationRunId,
                calculationSnapshotSha256 = model.Dispute.CalculationSnapshotHash,
                auditPackPdfSha256 = Sha256(pdf), generatedAtUtc = generatedAt, generatedBy, evidence
            }, new JsonSerializerOptions { WriteIndented = true });
            await WriteEntryAsync(archive, "manifest.json", manifest, cancellationToken);
        }
        return output.ToArray();
    }

    private static async Task WriteEntryAsync(ZipArchive archive, string name, byte[] bytes, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await stream.WriteAsync(bytes, cancellationToken);
    }

    private static void ComposeIndexTable(IContainer container, IEnumerable<QuantitySurveyEscalationCalculationLine> lines)
    {
        container.Column(column =>
        {
            column.Item().Text("Formula components and approved indices").Bold().FontSize(9);
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns => { columns.ConstantColumn(30); columns.RelativeColumn(2); columns.RelativeColumn(); columns.RelativeColumn(); columns.RelativeColumn(); });
                table.Header(header =>
                {
                    Header(header, "Seq"); Header(header, "Index family"); Header(header, "Coefficient"); Header(header, "Base index"); Header(header, "Current index");
                });
                foreach (var line in lines.OrderBy(value => value.Sequence))
                {
                    Body(table, line.Sequence.ToString()); Body(table, line.IndexFamilyCodeSnapshot);
                    Body(table, line.Coefficient.ToString("N4")); Body(table, line.BaseIndexValue.ToString("N6")); Body(table, line.CurrentIndexValue.ToString("N6"));
                }
            });
        });
    }

    private static void ComposeAttachmentTable(IContainer container, IEnumerable<QuantitySurveyEscalationDisputeAttachment> attachments)
    {
        container.Column(column =>
        {
            column.Item().Text("Central-DMS evidence inventory").Bold().FontSize(9);
            var values = attachments.Where(value => !value.IsDeleted).OrderBy(value => value.CreatedAt).ToList();
            if (values.Count == 0) { column.Item().Text("No evidence files were attached."); return; }
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns => { columns.RelativeColumn(); columns.RelativeColumn(2); columns.RelativeColumn(2); columns.RelativeColumn(2); });
                table.Header(header => { Header(header, "Type"); Header(header, "Title / file"); Header(header, "DMS version"); Header(header, "SHA-256"); });
                foreach (var value in values)
                {
                    Body(table, value.AttachmentType.ToString()); Body(table, value.Title + "\n" + value.OriginalFileName);
                    Body(table, value.CentralDocumentVersionId.ToString()); Body(table, value.ChecksumSha256);
                }
            });
        });
    }

    private static void ComposeRevisionTable(IContainer container, IReadOnlyList<QuantitySurveyEscalationDisputeRevision> revisions)
    {
        container.Column(column =>
        {
            column.Item().Text("Immutable lifecycle history").Bold().FontSize(9);
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns => { columns.RelativeColumn(); columns.RelativeColumn(2); columns.RelativeColumn(2); columns.RelativeColumn(3); });
                table.Header(header => { Header(header, "Time"); Header(header, "Action"); Header(header, "Actor"); Header(header, "Reason / correlation"); });
                foreach (var value in revisions)
                {
                    Body(table, value.CreatedAt.ToString("u")); Body(table, value.Action); Body(table, value.ActorName);
                    Body(table, Text(value.Reason) + "\n" + value.CorrelationId);
                }
            });
        });
    }

    private static void Section(IContainer container, string title, Action<ColumnDescriptor> content)
        => container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(7).Column(column =>
        {
            column.Item().Text(title).Bold().FontSize(9).FontColor(Colors.Blue.Darken3);
            column.Item().PaddingTop(4).Column(content);
        });
    private static void Meta(ColumnDescriptor column, string label, string value)
        => column.Item().PaddingBottom(2).Text(text => { text.Span(label + ": ").SemiBold(); text.Span(value); });
    private static void Header(TableCellDescriptor table, string value) => table.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text(value).SemiBold().FontSize(7);
    private static void Body(TableDescriptor table, string value) => table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(value).FontSize(6.5f);
    private static string User(AuditPackModel model, Guid? id) => id.HasValue && model.Users.TryGetValue(id.Value, out var value) ? value + " (" + id + ")" : id?.ToString() ?? "-";
    private static string Date(DateTime? value) => value?.ToString("u") ?? "-";
    private static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "document" : safe;
    }
    private static string Sha256(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    private static bool FixedEquals(string left, string right) => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));

    private sealed record AuditPackModel(
        Tenant Tenant,
        QuantitySurveyEscalationDispute Dispute,
        IReadOnlyList<QuantitySurveyEscalationDisputeRevision> Revisions,
        IReadOnlyDictionary<Guid, string> Users);
}
