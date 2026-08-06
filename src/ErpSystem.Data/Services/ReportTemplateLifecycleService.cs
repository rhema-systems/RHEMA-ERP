using System.Text.Json;
using System.Text.RegularExpressions;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Services;

public sealed partial class ReportTemplateLifecycleService : IReportTemplateLifecycleService
{
    private static readonly string[] Audiences = ["PPA/GHANEPS", "Finance", "Audit", "Board"];
    private static readonly string[] Cadences = ["Monthly", "Quarterly", "AdHoc"];
    private static readonly string[] Formats = ["Online", "XLSX", "PDF"];
    private readonly ApplicationDbContext _db;
    private readonly IReportsService _reports;
    private readonly ILogger<ReportTemplateLifecycleService> _logger;

    public ReportTemplateLifecycleService(
        ApplicationDbContext db,
        IReportsService reports,
        ILogger<ReportTemplateLifecycleService> logger)
    {
        _db = db;
        _reports = reports;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ReportTemplateDto>> GetAsync(
        Guid tenantId, Guid userId, bool isAdministrator, string? audience = null,
        string? cadence = null, string? status = null, CancellationToken cancellationToken = default)
    {
        var accessibleReports = await _reports.GetReportsAsync(
            tenantId, userId, bypassRoleFiltering: isAdministrator);
        var accessibleReportIds = accessibleReports.Select(report => report.Id).ToHashSet();

        var query = _db.ReportTemplates.IgnoreQueryFilters().AsNoTracking()
            .Where(template => template.TenantId == tenantId && !template.IsDeleted &&
                               template.ReportId.HasValue && accessibleReportIds.Contains(template.ReportId.Value))
            .Include(template => template.Report)
            .AsQueryable();

        if (!isAdministrator)
            query = query.Where(template => template.Status == "Published");
        else if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(template => template.Status == NormalizeStatus(status));

        if (!string.IsNullOrWhiteSpace(audience))
            query = query.Where(template => template.Audience == NormalizeAudience(audience));
        if (!string.IsNullOrWhiteSpace(cadence))
            query = query.Where(template => template.Cadence == NormalizeCadence(cadence));

        var templates = await query
            .OrderBy(template => template.Audience)
            .ThenBy(template => template.Cadence)
            .ThenBy(template => template.Name)
            .ThenByDescending(template => template.Version)
            .ToListAsync(cancellationToken);
        return templates.Select(Map).ToList();
    }

    public async Task<ReportTemplateDto?> GetByIdAsync(
        Guid templateId, Guid tenantId, Guid userId, bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        var template = await FindAsync(templateId, tenantId, cancellationToken);
        if (template is null || (!isAdministrator && template.Status != "Published"))
            return null;

        await EnsureReportAccessAsync(template, tenantId, userId, isAdministrator);
        return Map(template);
    }

    public async Task<ReportTemplateDto> CreateAsync(
        CreateReportTemplateDto request, Guid tenantId, Guid userId, bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        EnsureAdministrator(isAdministrator);
        var normalized = Validate(request);
        await EnsureReportExistsAsync(request.ReportId, tenantId, userId, cancellationToken);

        var keyExists = await _db.ReportTemplates.IgnoreQueryFilters().AnyAsync(template =>
            template.TenantId == tenantId && template.TemplateKey == normalized.TemplateKey,
            cancellationToken);
        if (keyExists)
            throw new InvalidOperationException("This template key already exists. Clone the existing family to create a new version.");

        var template = new ReportTemplate
        {
            TenantId = tenantId,
            ReportId = request.ReportId,
            TemplateKey = normalized.TemplateKey,
            Version = 1,
            Status = "Draft",
            CreatedBy = userId.ToString(),
            IsCustom = true
        };
        Apply(template, request, normalized);

        _db.ReportTemplates.Add(template);
        AddAudit(template, userId, "Create", null, Snapshot(template));
        await _db.SaveChangesAsync(cancellationToken);
        await _db.Entry(template).Reference(value => value.Report).LoadAsync(cancellationToken);
        return Map(template);
    }

    public async Task<ReportTemplateDto> UpdateAsync(
        Guid templateId, UpdateReportTemplateDto request, Guid tenantId, Guid userId,
        bool isAdministrator, CancellationToken cancellationToken = default)
    {
        EnsureAdministrator(isAdministrator);
        var template = await RequiredAsync(templateId, tenantId, cancellationToken);
        EnsureStatus(template, "Draft", "Only a Draft template can be edited.");
        EnsureConcurrency(template, request.RowVersion);
        if (request.ReportId != template.ReportId || NormalizeKey(request.TemplateKey) != template.TemplateKey)
            throw new InvalidOperationException("The report and template key are immutable. Clone the family for a new version.");

        var normalized = Validate(request);
        var oldValues = Snapshot(template);
        Apply(template, request, normalized);
        template.UpdatedAt = DateTime.UtcNow;
        template.UpdatedBy = userId.ToString();
        AddAudit(template, userId, "Update", oldValues, Snapshot(template));
        await SaveWithConcurrencyAsync(cancellationToken);
        return Map(template);
    }

    public async Task<ReportTemplateDto> PublishAsync(
        Guid templateId, ReportTemplateLifecycleActionDto request, Guid tenantId, Guid userId,
        bool isAdministrator, CancellationToken cancellationToken = default)
    {
        EnsureAdministrator(isAdministrator);
        var template = await RequiredAsync(templateId, tenantId, cancellationToken);
        EnsureStatus(template, "Draft", "Only a Draft template can be published.");
        EnsureConcurrency(template, request.RowVersion);
        ValidateStored(template);

        var now = DateTime.UtcNow;
        var replacements = await _db.ReportTemplates.IgnoreQueryFilters().Where(value =>
            value.TenantId == tenantId && value.TemplateKey == template.TemplateKey &&
            value.Status == "Published" && value.Id != template.Id && !value.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var replacement in replacements)
        {
            replacement.Status = "Archived";
            replacement.UpdatedAt = now;
            replacement.UpdatedBy = userId.ToString();
            AddAudit(replacement, userId, "ArchiveOnReplacement", null, Snapshot(replacement));
        }

        template.Status = "Published";
        template.UpdatedAt = now;
        template.UpdatedBy = userId.ToString();
        AddAudit(template, userId, "Publish", null, Snapshot(template));
        await SaveWithConcurrencyAsync(cancellationToken);
        return Map(template);
    }

    public async Task<ReportTemplateDto> ArchiveAsync(
        Guid templateId, ReportTemplateLifecycleActionDto request, Guid tenantId, Guid userId,
        bool isAdministrator, CancellationToken cancellationToken = default)
    {
        EnsureAdministrator(isAdministrator);
        var template = await RequiredAsync(templateId, tenantId, cancellationToken);
        EnsureStatus(template, "Published", "Only a Published template can be archived.");
        EnsureConcurrency(template, request.RowVersion);
        template.Status = "Archived";
        template.UpdatedAt = DateTime.UtcNow;
        template.UpdatedBy = userId.ToString();
        AddAudit(template, userId, "Archive", null, Snapshot(template));
        await SaveWithConcurrencyAsync(cancellationToken);
        return Map(template);
    }

    public async Task<ReportTemplateDto> CloneAsync(
        Guid templateId, CloneReportTemplateDto request, Guid tenantId, Guid userId,
        bool isAdministrator, CancellationToken cancellationToken = default)
    {
        EnsureAdministrator(isAdministrator);
        var source = await RequiredAsync(templateId, tenantId, cancellationToken);
        if (source.Status == "Draft")
            throw new InvalidOperationException("Publish or delete the existing Draft before cloning this template family.");
        EnsureConcurrency(source, request.RowVersion);

        var hasDraft = await _db.ReportTemplates.IgnoreQueryFilters().AnyAsync(value =>
            value.TenantId == tenantId && value.TemplateKey == source.TemplateKey &&
            value.Status == "Draft" && !value.IsDeleted, cancellationToken);
        if (hasDraft)
            throw new InvalidOperationException("This template family already has a Draft revision.");

        var nextVersion = await _db.ReportTemplates.IgnoreQueryFilters()
            .Where(value => value.TenantId == tenantId && value.TemplateKey == source.TemplateKey)
            .MaxAsync(value => (int?)value.Version, cancellationToken) + 1 ?? 1;
        var clone = new ReportTemplate
        {
            TenantId = tenantId,
            ReportId = source.ReportId,
            TemplateKey = source.TemplateKey,
            Version = nextVersion,
            Name = string.IsNullOrWhiteSpace(request.Name) ? source.Name : request.Name.Trim(),
            Description = source.Description,
            Category = source.Category,
            Type = source.Type,
            Audience = source.Audience,
            Cadence = source.Cadence,
            Status = "Draft",
            DefaultOutputFormat = source.DefaultOutputFormat,
            OutputFormats = source.OutputFormats,
            SavedFilters = source.SavedFilters,
            GenerationMetadata = source.GenerationMetadata,
            ChartType = source.ChartType,
            IsCustom = true,
            Tags = source.Tags,
            PreviewImage = source.PreviewImage,
            Configuration = source.Configuration,
            CreatedBy = userId.ToString()
        };
        _db.ReportTemplates.Add(clone);
        AddAudit(clone, userId, "Clone", null, new { SourceTemplateId = source.Id, clone.TemplateKey, clone.Version });
        await _db.SaveChangesAsync(cancellationToken);
        await _db.Entry(clone).Reference(value => value.Report).LoadAsync(cancellationToken);
        return Map(clone);
    }

    public async Task DeleteAsync(
        Guid templateId, ReportTemplateLifecycleActionDto request, Guid tenantId, Guid userId,
        bool isAdministrator, CancellationToken cancellationToken = default)
    {
        EnsureAdministrator(isAdministrator);
        var template = await RequiredAsync(templateId, tenantId, cancellationToken);
        EnsureStatus(template, "Draft", "Only a Draft template can be deleted.");
        EnsureConcurrency(template, request.RowVersion);
        template.IsDeleted = true;
        template.DeletedAt = DateTime.UtcNow;
        template.DeletedBy = userId.ToString();
        AddAudit(template, userId, "Delete", Snapshot(template), null);
        await SaveWithConcurrencyAsync(cancellationToken);
    }

    public async Task<ReportResultDto> ExecuteAsync(
        Guid templateId, GenerateReportTemplateDto request, Guid tenantId, Guid userId,
        bool isAdministrator, CancellationToken cancellationToken = default)
    {
        var template = await PublishedForGenerationAsync(templateId, tenantId, userId, isAdministrator, cancellationToken);
        var format = NormalizeFormat(request.Format);
        if (format != "Online")
            throw new InvalidOperationException("Use the template export endpoint for XLSX or PDF output.");
        EnsureFormatAllowed(template, format);
        var filters = MergeFilters(template.SavedFilters, request.FilterOverrides);
        var context = GenerationContext(template, format);
        var result = await _reports.ExecuteReportAsync(template.ReportId!.Value, new ExecuteReportDto
        {
            Parameters = filters,
            Page = Math.Max(1, request.Page),
            PageSize = Math.Clamp(request.PageSize, 1, 1000),
            IncludeMetadata = true,
            TemplateContext = context
        }, tenantId, userId, isAdministrator);

        await RecordGenerationAsync(template, userId, format, filters, cancellationToken);
        result.Metadata ??= new ReportMetadataDto();
        result.Metadata.TemplateGeneration = context;
        return result;
    }

    public async Task<ReportExportResultDto> ExportAsync(
        Guid templateId, GenerateReportTemplateDto request, Guid tenantId, Guid userId,
        bool isAdministrator, CancellationToken cancellationToken = default)
    {
        var template = await PublishedForGenerationAsync(templateId, tenantId, userId, isAdministrator, cancellationToken);
        var format = NormalizeFormat(request.Format);
        if (format == "Online")
            throw new InvalidOperationException("Use the template execute endpoint for online output.");
        EnsureFormatAllowed(template, format);
        var filters = MergeFilters(template.SavedFilters, request.FilterOverrides);
        var result = await _reports.ExportReportAsync(template.ReportId!.Value, new ExportReportDto
        {
            Format = format.ToLowerInvariant(),
            Parameters = filters,
            IncludeCharts = request.IncludeCharts,
            IncludeHeaders = request.IncludeHeaders,
            Template = template.TemplateKey,
            TemplateContext = GenerationContext(template, format)
        }, tenantId, userId, isAdministrator);

        await RecordGenerationAsync(template, userId, format, filters, cancellationToken);
        return result;
    }

    private async Task<ReportTemplate> PublishedForGenerationAsync(
        Guid id, Guid tenantId, Guid userId, bool isAdministrator, CancellationToken cancellationToken)
    {
        var template = await RequiredAsync(id, tenantId, cancellationToken);
        EnsureStatus(template, "Published", "Only a Published template can generate a report.");
        await EnsureReportAccessAsync(template, tenantId, userId, isAdministrator);
        return template;
    }

    private async Task EnsureReportExistsAsync(Guid reportId, Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (await _reports.GetReportAsync(reportId, tenantId, userId, true) is null)
            throw new KeyNotFoundException("The selected report definition was not found in this tenant.");
    }

    private async Task EnsureReportAccessAsync(
        ReportTemplate template, Guid tenantId, Guid userId, bool isAdministrator)
    {
        if (!template.ReportId.HasValue ||
            await _reports.GetReportAsync(template.ReportId.Value, tenantId, userId, isAdministrator) is null)
            throw new UnauthorizedAccessException("The selected report is not available to this user.");
    }

    private async Task<ReportTemplate?> FindAsync(Guid id, Guid tenantId, CancellationToken cancellationToken) =>
        await _db.ReportTemplates.IgnoreQueryFilters()
            .Include(value => value.Report)
            .SingleOrDefaultAsync(value => value.Id == id && value.TenantId == tenantId && !value.IsDeleted,
                cancellationToken);

    private async Task<ReportTemplate> RequiredAsync(Guid id, Guid tenantId, CancellationToken cancellationToken) =>
        await FindAsync(id, tenantId, cancellationToken) ??
        throw new KeyNotFoundException("Report template not found.");

    private async Task RecordGenerationAsync(
        ReportTemplate template, Guid userId, string format, Dictionary<string, object>? filters,
        CancellationToken cancellationToken)
    {
        template.UsageCount++;
        template.LastUsed = DateTime.UtcNow;
        template.LastGeneratedAt = template.LastUsed;
        template.LastGeneratedBy = userId;
        template.LastGenerationFormat = format;
        template.UpdatedAt = template.LastUsed;
        template.UpdatedBy = userId.ToString();
        AddAudit(template, userId, "Generate", null, new
        {
            template.TemplateKey,
            template.Version,
            Format = format,
            Filters = filters,
            template.GenerationMetadata
        });
        await SaveWithConcurrencyAsync(cancellationToken);
    }

    private static void Apply(ReportTemplate template, CreateReportTemplateDto request, Normalized normalized)
    {
        template.Name = request.Name.Trim();
        template.Description = request.Description?.Trim() ?? string.Empty;
        template.Category = request.Category.Trim();
        template.Type = request.Type.Trim();
        template.Audience = normalized.Audience;
        template.Cadence = normalized.Cadence;
        template.DefaultOutputFormat = normalized.DefaultFormat;
        template.OutputFormats = JsonSerializer.Serialize(normalized.OutputFormats);
        template.SavedFilters = Serialize(request.SavedFilters);
        template.GenerationMetadata = Serialize(request.GenerationMetadata);
        template.ChartType = NullIfEmpty(request.ChartType);
        template.Tags = request.Tags is null ? null : JsonSerializer.Serialize(request.Tags);
        template.PreviewImage = NullIfEmpty(request.PreviewImage);
        template.Configuration = Serialize(request.Configuration);
    }

    private static Normalized Validate(CreateReportTemplateDto request)
    {
        if (request.ReportId == Guid.Empty) throw new InvalidOperationException("A report definition is required.");
        if (string.IsNullOrWhiteSpace(request.Name)) throw new InvalidOperationException("Template name is required.");
        if (string.IsNullOrWhiteSpace(request.Category)) throw new InvalidOperationException("Category is required.");
        if (string.IsNullOrWhiteSpace(request.Type)) throw new InvalidOperationException("Template type is required.");
        var key = NormalizeKey(request.TemplateKey);
        if (!TemplateKeyPattern().IsMatch(key))
            throw new InvalidOperationException("Template key may contain only A-Z, 0-9, dot, underscore and hyphen.");
        var audience = NormalizeAudience(request.Audience);
        var cadence = NormalizeCadence(request.Cadence);
        var outputFormats = (request.OutputFormats ?? []).Select(NormalizeFormat).Distinct().ToList();
        if (outputFormats.Count == 0) throw new InvalidOperationException("Select at least one output format.");
        var defaultFormat = NormalizeFormat(request.DefaultOutputFormat);
        if (!outputFormats.Contains(defaultFormat))
            throw new InvalidOperationException("The default output format must be one of the selected output formats.");
        return new Normalized(key, audience, cadence, defaultFormat, outputFormats);
    }

    private static void ValidateStored(ReportTemplate template)
    {
        var formats = DeserializeList(template.OutputFormats);
        if (string.IsNullOrWhiteSpace(template.TemplateKey) || !TemplateKeyPattern().IsMatch(template.TemplateKey) ||
            !Audiences.Contains(template.Audience) || !Cadences.Contains(template.Cadence) ||
            formats.Count == 0 || !formats.Contains(template.DefaultOutputFormat) || !template.ReportId.HasValue)
            throw new InvalidOperationException("The Draft template is incomplete and cannot be published.");
    }

    private static void EnsureFormatAllowed(ReportTemplate template, string format)
    {
        if (!DeserializeList(template.OutputFormats).Contains(format))
            throw new InvalidOperationException($"{format} is not enabled for this template.");
    }

    private static Dictionary<string, object>? MergeFilters(
        string? savedJson, Dictionary<string, object>? overrides)
    {
        var merged = DeserializeDictionary(savedJson) ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (overrides is not null)
            foreach (var (key, value) in overrides)
                merged[key] = value;
        return merged.Count == 0 ? null : merged;
    }

    private void AddAudit(ReportTemplate template, Guid userId, string action, object? oldValues, object? newValues) =>
        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = template.TenantId,
            UserId = userId,
            Username = userId.ToString(),
            Action = $"ReportTemplate.{action}",
            Resource = nameof(ReportTemplate),
            ResourceId = template.Id.ToString(),
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            IpAddress = "api",
            UserAgent = "TDC-0705",
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId.ToString()
        });

    private async Task SaveWithConcurrencyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            _logger.LogWarning(exception, "Concurrent report-template mutation was rejected.");
            throw new InvalidOperationException("The report template changed after it was loaded. Refresh and try again.", exception);
        }
    }

    private static void EnsureConcurrency(ReportTemplate template, string supplied)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(supplied ?? string.Empty); }
        catch (FormatException exception) { throw new InvalidOperationException("The row version is invalid.", exception); }
        if (!template.RowVersion.SequenceEqual(expected))
            throw new InvalidOperationException("The report template changed after it was loaded. Refresh and try again.");
        _ = template;
    }

    private static void EnsureAdministrator(bool value)
    {
        if (!value) throw new UnauthorizedAccessException("Report-template administration is restricted to tenant administrators.");
    }

    private static void EnsureStatus(ReportTemplate template, string expected, string message)
    {
        if (!string.Equals(template.Status, expected, StringComparison.Ordinal))
            throw new InvalidOperationException(message);
    }

    private static ReportTemplateGenerationContextDto GenerationContext(ReportTemplate template, string format) => new()
    {
        TemplateId = template.Id,
        TemplateKey = template.TemplateKey,
        TemplateName = template.Name,
        Version = template.Version,
        Audience = template.Audience,
        Cadence = template.Cadence,
        OutputFormat = format,
        GenerationMetadata = DeserializeDictionary(template.GenerationMetadata)
    };

    private static ReportTemplateDto Map(ReportTemplate template) => new()
    {
        Id = template.Id,
        ReportId = template.ReportId,
        ReportName = template.Report?.Name,
        TemplateKey = template.TemplateKey,
        Version = template.Version,
        Name = template.Name,
        Description = template.Description,
        Category = template.Category,
        Type = template.Type,
        Audience = template.Audience,
        Cadence = template.Cadence,
        Status = template.Status,
        DefaultOutputFormat = template.DefaultOutputFormat,
        OutputFormats = DeserializeList(template.OutputFormats),
        SavedFilters = DeserializeDictionary(template.SavedFilters),
        GenerationMetadata = DeserializeDictionary(template.GenerationMetadata),
        ChartType = template.ChartType,
        IsCustom = template.IsCustom,
        CreatedBy = template.CreatedBy ?? string.Empty,
        CreatedAt = template.CreatedAt,
        LastUsed = template.LastUsed,
        UsageCount = template.UsageCount,
        Tags = DeserializeList(template.Tags),
        PreviewImage = template.PreviewImage,
        Configuration = DeserializeDictionary(template.Configuration),
        LastGeneratedAt = template.LastGeneratedAt,
        LastGeneratedBy = template.LastGeneratedBy,
        LastGenerationFormat = template.LastGenerationFormat,
        RowVersion = Convert.ToBase64String(template.RowVersion)
    };

    private static object Snapshot(ReportTemplate template) => new
    {
        template.ReportId, template.TemplateKey, template.Version, template.Name, template.Category,
        template.Type, template.Audience, template.Cadence, template.Status, template.DefaultOutputFormat,
        template.OutputFormats, template.SavedFilters, template.GenerationMetadata
    };

    private static string NormalizeKey(string value) => (value ?? string.Empty).Trim().ToUpperInvariant();
    private static string NormalizeAudience(string value) => Normalize(value, Audiences, "audience");
    private static string NormalizeCadence(string value) => Normalize(value, Cadences, "cadence");
    private static string NormalizeFormat(string value) => Normalize(value, Formats, "output format");
    private static string NormalizeStatus(string value) => Normalize(value, ["Draft", "Published", "Archived"], "status");
    private static string Normalize(string value, IReadOnlyList<string> allowed, string label) =>
        allowed.FirstOrDefault(item => string.Equals(item, value?.Trim(), StringComparison.OrdinalIgnoreCase)) ??
        throw new InvalidOperationException($"Unsupported {label}.");
    private static string? Serialize(Dictionary<string, object>? value) => value is null ? null : JsonSerializer.Serialize(value);
    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static List<string> DeserializeList(string? value) => string.IsNullOrWhiteSpace(value)
        ? [] : JsonSerializer.Deserialize<List<string>>(value) ?? [];
    private static Dictionary<string, object>? DeserializeDictionary(string? value) => string.IsNullOrWhiteSpace(value)
        ? null : JsonSerializer.Deserialize<Dictionary<string, object>>(value);

    [GeneratedRegex("^[A-Z0-9][A-Z0-9._-]{1,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex TemplateKeyPattern();

    private sealed record Normalized(
        string TemplateKey, string Audience, string Cadence, string DefaultFormat, List<string> OutputFormats);
}
