using System.Data;
using System.Globalization;
using System.Text.Json;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Reporting;

/// <summary>
/// Production FR-RP-012 implementation. Builder definitions are persisted beside
/// the shared Report record, while execution compiles only server-catalogued SQL
/// expressions and parameterises every user-supplied value.
/// </summary>
public sealed class FinanceAdHocReportService : IFinanceAdHocReportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthorizationService _authorization;
    private readonly IHttpContextAccessor _httpContext;
    private readonly IConfiguration _configuration;
    private readonly IFinanceAuditService _audit;

    public FinanceAdHocReportService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        IAuthorizationService authorization,
        IHttpContextAccessor httpContext,
        IConfiguration configuration,
        IFinanceAuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _authorization = authorization;
        _httpContext = httpContext;
        _configuration = configuration;
        _audit = audit;
    }

    public async Task<FinanceAdHocReportWorkspaceDto> GetWorkspaceAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId();
        var userId = UserId();
        var definitions = await _db.FinanceAdHocReportDefinitions.AsNoTracking()
            .Include(item => item.Report)
            .Include(item => item.OwnerUser)
            .Where(item => item.TenantId == tenantId && !item.IsDeleted
                && !item.Report.IsDeleted
                && (item.OwnerUserId == userId
                    || item.Visibility == FinanceAdHocReportValues.FinanceVisibility))
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .ToListAsync(cancellationToken);

        return new FinanceAdHocReportWorkspaceDto
        {
            Datasets = FinanceAdHocReportCatalog.All.Select(FinanceAdHocReportCatalog.Map).ToList(),
            Definitions = definitions.Select(Map).ToList(),
            PrivateDefinitions = definitions.Count(item =>
                item.Visibility == FinanceAdHocReportValues.PrivateVisibility),
            SharedDefinitions = definitions.Count(item =>
                item.Visibility == FinanceAdHocReportValues.FinanceVisibility)
        };
    }

    public async Task<FinanceAdHocReportDefinitionDto> CreateAsync(
        CreateFinanceAdHocReportDto request, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId();
        var userId = UserId();
        var normalized = await ValidateAndNormalizeAsync(request, cancellationToken);
        var definitionId = Guid.NewGuid();
        var report = new Report
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Type = "table",
            Status = "published",
            Query = FinanceAdHocReportValues.QueryPrefix + definitionId,
            Columns = JsonSerializer.Serialize(ToReportColumns(normalized), JsonOptions),
            Tags = JsonSerializer.Serialize(new[] { "finance", "ad-hoc", normalized.DatasetCode }, JsonOptions),
            CreatedBy = userId.ToString()
        };
        var definition = new FinanceAdHocReportDefinition
        {
            Id = definitionId,
            TenantId = tenantId,
            ReportId = report.Id,
            DatasetCode = normalized.DatasetCode,
            DefinitionJson = JsonSerializer.Serialize(normalized, JsonOptions),
            OwnerUserId = userId,
            Visibility = CanonicalVisibility(request.Visibility),
            MaximumRows = request.MaximumRows,
            CreatedBy = _currentUser.UserName ?? userId.ToString(),
            CreatedById = userId
        };

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        _db.Reports.Add(report);
        _db.FinanceAdHocReportDefinitions.Add(definition);
        await _db.SaveChangesAsync(cancellationToken);
        // Keep governance evidence in the same transaction as the definition. If audit persistence
        // fails, callers must not receive an error while an unaudited definition remains committed.
        await AuditAsync(FinanceAuditEvents.AdHocReportCreated, definition, null, request, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await RequiredDtoAsync(definition.Id, tenantId, cancellationToken);
    }

    public async Task<FinanceAdHocReportDefinitionDto> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var definition = await RequiredAsync(id, TenantId(), cancellationToken);
        await EnsureCanReadDefinitionAsync(definition, cancellationToken);
        return Map(definition);
    }

    public async Task<FinanceAdHocReportDefinitionDto> UpdateAsync(
        Guid id, UpdateFinanceAdHocReportDto request, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId();
        var definition = await RequiredAsync(id, tenantId, cancellationToken);
        await EnsureCanMaintainAsync(definition, cancellationToken);
        SetConcurrencyToken(definition, request.RowVersion);
        var before = Map(definition);
        var normalized = await ValidateAndNormalizeAsync(request, cancellationToken);

        definition.DatasetCode = normalized.DatasetCode;
        definition.DefinitionJson = JsonSerializer.Serialize(normalized, JsonOptions);
        definition.Visibility = CanonicalVisibility(request.Visibility);
        definition.MaximumRows = request.MaximumRows;
        definition.UpdatedAt = DateTime.UtcNow;
        definition.UpdatedBy = _currentUser.UserName;
        definition.LastModifiedById = UserId();
        definition.Report.Name = request.Name.Trim();
        definition.Report.Description = request.Description.Trim();
        definition.Report.Columns = JsonSerializer.Serialize(ToReportColumns(normalized), JsonOptions);
        definition.Report.Tags = JsonSerializer.Serialize(
            new[] { "finance", "ad-hoc", normalized.DatasetCode }, JsonOptions);
        definition.Report.UpdatedAt = DateTime.UtcNow;
        definition.Report.UpdatedBy = _currentUser.UserName;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await SaveWithConcurrencyMessageAsync(cancellationToken);
        await AuditAsync(FinanceAuditEvents.AdHocReportUpdated, definition, before, request, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await RequiredDtoAsync(id, tenantId, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, string rowVersion, CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId();
        var definition = await RequiredAsync(id, tenantId, cancellationToken);
        await EnsureCanMaintainAsync(definition, cancellationToken);
        SetConcurrencyToken(definition, rowVersion);
        var before = Map(definition);
        var now = DateTime.UtcNow;
        definition.IsDeleted = true;
        definition.DeletedAt = now;
        definition.DeletedBy = _currentUser.UserName;
        definition.Report.IsDeleted = true;
        definition.Report.DeletedAt = now;
        definition.Report.DeletedBy = _currentUser.UserName;
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await SaveWithConcurrencyMessageAsync(cancellationToken);
        await AuditAsync(FinanceAuditEvents.AdHocReportDeleted, definition, before,
            new { Reason = "Deleted through the governed Finance builder." }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public bool CanHandle(string? reportQuery) => TryDefinitionId(reportQuery, out _);

    // Finance definitions can be private to their owner or visible to all authorized Finance
    // users, so the shared catalogue must authorize each persisted definition independently.
    public bool RequiresRecordLevelReadAuthorization => true;

    public bool OwnsIdentifier(string? reportQuery) => !string.IsNullOrWhiteSpace(reportQuery)
        && reportQuery.StartsWith(FinanceAdHocReportValues.QueryPrefix, StringComparison.OrdinalIgnoreCase);

    public string? ResolveCode(string? reportQuery) =>
        TryDefinitionId(reportQuery, out var id) ? id.ToString() : null;

    public async Task<bool> CanReadAsync(bool isAdministrator, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.TenantId is null) return false;
        return isAdministrator || await HasPermissionAsync(FinancePermissions.RunFinanceReports, cancellationToken);
    }

    public async Task<bool> CanReadReportAsync(
        string reportQuery, bool isAdministrator, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAsync(isAdministrator, cancellationToken)
            || !TryDefinitionId(reportQuery, out var definitionId)) return false;

        var tenantId = _currentUser.TenantId;
        var userId = Guid.TryParse(_currentUser.UserId, out var parsedUserId) ? parsedUserId : Guid.Empty;
        if (tenantId is null || userId == Guid.Empty) return false;

        // Evaluate ownership in SQL. This method is also used while constructing the shared Reports
        // menu, where returning false is preferable to throwing and revealing a private record.
        return await _db.FinanceAdHocReportDefinitions.AsNoTracking().AnyAsync(item =>
            item.Id == definitionId && item.TenantId == tenantId.Value && !item.IsDeleted
            && !item.Report.IsDeleted
            // Administrative report authority may bypass the broad run permission, but it does
            // not redefine a user's explicitly Private definition as shared.
            && (item.OwnerUserId == userId
                || item.Visibility == FinanceAdHocReportValues.FinanceVisibility), cancellationToken);
    }

    public async Task AuthorizeExportAsync(
        string reportQuery, bool isAdministrator, CancellationToken cancellationToken = default)
    {
        var definition = await RequiredForExecutionAsync(reportQuery, cancellationToken);
        await EnsureCanReadDefinitionAsync(definition, cancellationToken);
        if (!isAdministrator && !await HasPermissionAsync(FinancePermissions.ExportFinanceReports, cancellationToken))
            throw new UnauthorizedAccessException("Finance report export permission is required.");
    }

    public async Task<ReportResultDto> ExecuteAsync(
        string reportQuery,
        ExecuteReportDto request,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        var definition = await RequiredForExecutionAsync(reportQuery, cancellationToken);
        await EnsureCanReadDefinitionAsync(definition, cancellationToken);
        var requiredPermission = RequiredExecutionPermission(request);
        if (!isAdministrator && !await HasPermissionAsync(requiredPermission, cancellationToken))
            throw new UnauthorizedAccessException(request.IsExportExecution
                ? "Finance report export permission is required."
                : "Finance report run permission is required.");

        var stored = DeserializeStored(definition.DefinitionJson);
        var dataset = ValidateStored(stored);
        // The shared exporter pages in 1,000-row chunks. For Finance exports, its generic MaxRows
        // value is a page transport default rather than an authority ceiling; the saved definition
        // remains the governing limit (up to 5,000 rows). Interactive callers retain their optional
        // stricter MaxRows override.
        var governedRequest = GovernExecutionRequest(definition.MaximumRows, request);
        var compiled = FinanceAdHocSqlCompiler.Compile(
            dataset, stored, TenantId(), definition.MaximumRows, governedRequest);
        var connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("The Finance reporting database connection is not configured.");
        var startedAt = DateTime.UtcNow;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var totalRows = await ExecuteCountAsync(connection, compiled, cancellationToken);
        var cappedRows = Math.Min(totalRows, compiled.MaximumRows);
        var rows = await ExecuteRowsAsync(connection, compiled, cancellationToken);

        return new ReportResultDto
        {
            ReportId = definition.ReportId,
            ReportName = definition.Report.Name,
            ExecutedAt = DateTime.UtcNow,
            ExecutionTime = DateTime.UtcNow - startedAt,
            TotalRows = cappedRows,
            Columns = compiled.Columns,
            Data = rows,
            CurrentPage = compiled.Page,
            PageSize = compiled.PageSize,
            TotalPages = cappedRows == 0 ? 0 : (int)Math.Ceiling(cappedRows / (double)compiled.PageSize),
            HasPreviousPage = compiled.Page > 1,
            HasNextPage = compiled.Page * compiled.PageSize < cappedRows,
            Metadata = new ReportMetadataDto
            {
                DataAsOf = DateTime.UtcNow,
                DataSource = dataset.Name,
                Parameters = request.Parameters,
                Statistics = new Dictionary<string, object>
                {
                    ["MaximumRows"] = compiled.MaximumRows,
                    ["ResultWasCapped"] = totalRows > compiled.MaximumRows,
                    ["Visibility"] = definition.Visibility
                }
            }
        };
    }

    internal static string RequiredExecutionPermission(ExecuteReportDto request) =>
        request.IsExportExecution
            ? FinancePermissions.ExportFinanceReports
            : FinancePermissions.RunFinanceReports;

    internal static ExecuteReportDto GovernExecutionRequest(
        int definitionMaximumRows,
        ExecuteReportDto request) =>
        !request.IsExportExecution
            ? request
            : new ExecuteReportDto
            {
                Parameters = request.Parameters,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                IncludeMetadata = request.IncludeMetadata,
                Page = request.Page,
                PageSize = request.PageSize,
                // Export pages may be transported 1,000 rows at a time, but the persisted Finance
                // definition—not that generic page default—governs the complete export ceiling.
                MaxRows = definitionMaximumRows,
                IsExportExecution = true,
                TemplateContext = request.TemplateContext
            };

    private async Task<FinanceAdHocReportDefinition> RequiredForExecutionAsync(
        string query, CancellationToken cancellationToken)
    {
        if (!TryDefinitionId(query, out var definitionId))
            throw new InvalidOperationException("The Finance ad hoc report identifier is invalid.");
        return await RequiredAsync(definitionId, TenantId(), cancellationToken);
    }

    private async Task<FinanceAdHocStoredDefinition> ValidateAndNormalizeAsync(
        CreateFinanceAdHocReportDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("A report name is required.");
        var dataset = FinanceAdHocReportCatalog.Required(request.DatasetCode);
        var stored = new FinanceAdHocStoredDefinition
        {
            DatasetCode = dataset.Code,
            Columns = request.Columns.Select(item => new FinanceAdHocColumnDto
            {
                Field = item.Field.Trim(),
                Aggregation = CanonicalAggregation(item.Aggregation)
            }).ToList(),
            Filters = request.Filters.Select(item => new FinanceAdHocFilterDto
            {
                Field = item.Field.Trim(),
                Operator = CanonicalOperator(item.Operator),
                Value = item.Value?.Trim(),
                ValueTo = item.ValueTo?.Trim()
            }).ToList(),
            Sorts = request.Sorts.Select(item => new FinanceAdHocSortDto
            {
                Field = item.Field.Trim(),
                Descending = item.Descending
            }).ToList()
        };
        ValidateStored(stored);
        if (CanonicalVisibility(request.Visibility) == FinanceAdHocReportValues.FinanceVisibility
            && !await HasPermissionAsync(FinancePermissions.ShareAdHocReports, cancellationToken))
            throw new UnauthorizedAccessException("Sharing an ad hoc definition with Finance requires the share permission.");
        return stored;
    }

    internal static FinanceAdHocDataset ValidateStored(FinanceAdHocStoredDefinition stored)
    {
        var dataset = FinanceAdHocReportCatalog.Required(stored.DatasetCode);
        if (stored.Columns.Count == 0 || stored.Columns.Count > 20)
            throw new InvalidOperationException("Select between 1 and 20 report columns.");
        if (stored.Columns.Select(item => item.Field).Distinct(StringComparer.OrdinalIgnoreCase).Count()
            != stored.Columns.Count)
            throw new InvalidOperationException("A report field can be selected only once.");
        foreach (var column in stored.Columns)
        {
            if (!dataset.Fields.TryGetValue(column.Field, out var field))
                throw new InvalidOperationException($"Field '{column.Field}' is not in the selected Finance dataset.");
            if (!FinanceAdHocReportValues.Aggregations.Contains(column.Aggregation))
                throw new InvalidOperationException($"Aggregation '{column.Aggregation}' is not supported.");
            if (!column.Aggregation.Equals("None", StringComparison.OrdinalIgnoreCase) && !field.CanAggregate
                && !column.Aggregation.Equals("Count", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Field '{field.Label}' cannot use {column.Aggregation}.");
        }
        if (stored.Filters.Count > 12) throw new InvalidOperationException("A report supports up to 12 filters.");
        foreach (var filter in stored.Filters)
        {
            if (!dataset.Fields.ContainsKey(filter.Field))
                throw new InvalidOperationException($"Filter field '{filter.Field}' is not available.");
            if (!FinanceAdHocReportValues.FilterOperators.Contains(filter.Operator))
                throw new InvalidOperationException($"Filter operator '{filter.Operator}' is not supported.");
            if (!filter.Operator.Equals("IsBlank", StringComparison.OrdinalIgnoreCase)
                && !filter.Operator.Equals("IsNotBlank", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(filter.Value))
                throw new InvalidOperationException($"Filter '{filter.Field}' requires a value.");
            if (filter.Operator.Equals("Between", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(filter.ValueTo))
                throw new InvalidOperationException($"Between filter '{filter.Field}' requires two values.");
        }
        if (stored.Sorts.Count > 3) throw new InvalidOperationException("A report supports up to 3 sort fields.");
        var selected = stored.Columns.Select(item => item.Field).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (stored.Sorts.Any(item => !selected.Contains(item.Field)))
            throw new InvalidOperationException("Sort fields must also be selected report columns.");
        return dataset;
    }

    private Task EnsureCanReadDefinitionAsync(
        FinanceAdHocReportDefinition definition, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (definition.OwnerUserId == UserId()
            || definition.Visibility == FinanceAdHocReportValues.FinanceVisibility)
            return Task.CompletedTask;
        throw new UnauthorizedAccessException("This private Finance report definition belongs to another user.");
    }

    private Task EnsureCanMaintainAsync(
        FinanceAdHocReportDefinition definition, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (definition.OwnerUserId == UserId()) return Task.CompletedTask;

        // Share authority permits an analyst to publish their own definition to Finance; it does
        // not transfer ownership or permit a supervisor to mutate another analyst's work by ID.
        // This keeps shared definitions reusable while preserving clear authorship and audit evidence.
        throw new UnauthorizedAccessException("Only the owner can change or delete this Finance report definition.");
    }

    private async Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken)
    {
        var principal = _httpContext.HttpContext?.User;
        return principal != null && (await _authorization.AuthorizeAsync(principal, null, permission)).Succeeded;
    }

    private async Task<FinanceAdHocReportDefinition> RequiredAsync(
        Guid id, Guid tenantId, CancellationToken cancellationToken) =>
        await _db.FinanceAdHocReportDefinitions
            .Include(item => item.Report)
            .Include(item => item.OwnerUser)
            .SingleOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId
                && !item.IsDeleted && !item.Report.IsDeleted, cancellationToken)
        ?? throw new KeyNotFoundException("Finance ad hoc report definition not found.");

    private async Task<FinanceAdHocReportDefinitionDto> RequiredDtoAsync(
        Guid id, Guid tenantId, CancellationToken cancellationToken) =>
        Map(await RequiredAsync(id, tenantId, cancellationToken));

    private FinanceAdHocReportDefinitionDto Map(FinanceAdHocReportDefinition item)
    {
        var stored = DeserializeStored(item.DefinitionJson);
        var dataset = FinanceAdHocReportCatalog.Required(item.DatasetCode);
        return new FinanceAdHocReportDefinitionDto
        {
            Id = item.Id,
            ReportId = item.ReportId,
            Name = item.Report.Name,
            Description = item.Report.Description,
            DatasetCode = item.DatasetCode,
            DatasetName = dataset.Name,
            Columns = stored.Columns,
            Filters = stored.Filters,
            Sorts = stored.Sorts,
            Visibility = item.Visibility,
            MaximumRows = item.MaximumRows,
            OwnerUserId = item.OwnerUserId,
            OwnerName = (item.OwnerUser.FirstName + " " + item.OwnerUser.LastName).Trim(),
            CanMaintain = item.OwnerUserId == UserId(),
            CreatedAt = item.CreatedAt,
            LastRun = item.Report.LastRun,
            RowVersion = Convert.ToBase64String(item.RowVersion)
        };
    }

    private void SetConcurrencyToken(FinanceAdHocReportDefinition item, string encoded)
    {
        try { _db.Entry(item).Property(value => value.RowVersion).OriginalValue = Convert.FromBase64String(encoded); }
        catch (FormatException) { throw new InvalidOperationException("The definition row version is invalid. Refresh and try again."); }
    }

    private async Task SaveWithConcurrencyMessageAsync(CancellationToken cancellationToken)
    {
        try { await _db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("This report definition changed after it was opened. Refresh and try again.");
        }
    }

    private async Task AuditAsync(string eventType, FinanceAdHocReportDefinition item,
        object? before, object after, CancellationToken cancellationToken) =>
        await _audit.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = item.TenantId,
            Resource = nameof(FinanceAdHocReportDefinition),
            ResourceId = item.Id.ToString(),
            BeforeValues = before,
            AfterValues = after
        }, cancellationToken);

    private Guid TenantId() => _currentUser.GetRequiredFinanceTenantId();
    private Guid UserId() => Guid.TryParse(_currentUser.UserId, out var id) && id != Guid.Empty
        ? id : throw new InvalidOperationException("Finance ad hoc reporting requires an authenticated user.");

    private static string CanonicalVisibility(string value) =>
        FinanceAdHocReportValues.Visibilities.Single(item => item.Equals(value, StringComparison.OrdinalIgnoreCase));
    private static string CanonicalAggregation(string value) =>
        FinanceAdHocReportValues.Aggregations.Single(item => item.Equals(value, StringComparison.OrdinalIgnoreCase));
    private static string CanonicalOperator(string value) =>
        FinanceAdHocReportValues.FilterOperators.Single(item => item.Equals(value, StringComparison.OrdinalIgnoreCase));
    private static bool TryDefinitionId(string? query, out Guid id) => Guid.TryParse(
        query?.StartsWith(FinanceAdHocReportValues.QueryPrefix, StringComparison.OrdinalIgnoreCase) == true
            ? query[FinanceAdHocReportValues.QueryPrefix.Length..] : null, out id);
    private static FinanceAdHocStoredDefinition DeserializeStored(string json) =>
        JsonSerializer.Deserialize<FinanceAdHocStoredDefinition>(json, JsonOptions)
        ?? throw new InvalidOperationException("The stored Finance report definition is invalid.");
    private static List<ReportColumnDto> ToReportColumns(FinanceAdHocStoredDefinition stored)
    {
        var dataset = FinanceAdHocReportCatalog.Required(stored.DatasetCode);
        return stored.Columns.Select((item, index) => new ReportColumnDto
        {
            Name = item.Field,
            DisplayName = dataset.Fields[item.Field].Label,
            DataType = item.Aggregation.Equals("Count", StringComparison.OrdinalIgnoreCase)
                ? "Integer" : dataset.Fields[item.Field].DataType,
            IsVisible = true,
            Order = index,
            AggregationType = item.Aggregation == "None" ? null : item.Aggregation
        }).ToList();
    }

    private static async Task<int> ExecuteCountAsync(
        SqlConnection connection, CompiledFinanceAdHocQuery compiled, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(compiled.CountSql, connection) { CommandTimeout = 30 };
        AddParameters(command, compiled.Parameters);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return (int)Math.Min(Convert.ToInt64(value, CultureInfo.InvariantCulture), int.MaxValue);
    }

    private static async Task<List<Dictionary<string, object>>> ExecuteRowsAsync(
        SqlConnection connection, CompiledFinanceAdHocQuery compiled, CancellationToken cancellationToken)
    {
        // A page requested beyond the definition's governed row ceiling is intentionally empty.
        // Avoid sending FETCH NEXT 0 to SQL Server, which is invalid syntax.
        if (compiled.Offset >= compiled.MaximumRows) return [];
        await using var command = new SqlCommand(compiled.PageSql, connection) { CommandTimeout = 30 };
        AddParameters(command, compiled.Parameters);
        command.Parameters.Add(new SqlParameter("@offset", SqlDbType.Int) { Value = compiled.Offset });
        command.Parameters.Add(new SqlParameter("@pageSize", SqlDbType.Int) { Value = compiled.PageSize });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<Dictionary<string, object>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index++)
                row[reader.GetName(index)] = reader.IsDBNull(index) ? null! : reader.GetValue(index);
            rows.Add(row);
        }
        return rows;
    }

    private static void AddParameters(SqlCommand command, IReadOnlyList<SqlParameterValue> parameters)
    {
        foreach (var parameter in parameters)
            command.Parameters.Add(new SqlParameter(parameter.Name, parameter.Type) { Value = parameter.Value ?? DBNull.Value });
    }
}

public sealed class FinanceAdHocStoredDefinition
{
    public string DatasetCode { get; set; } = string.Empty;
    public List<FinanceAdHocColumnDto> Columns { get; set; } = [];
    public List<FinanceAdHocFilterDto> Filters { get; set; } = [];
    public List<FinanceAdHocSortDto> Sorts { get; set; } = [];
}

internal static class FinanceAdHocSqlCompiler
{
    public static CompiledFinanceAdHocQuery Compile(
        FinanceAdHocDataset dataset,
        FinanceAdHocStoredDefinition stored,
        Guid tenantId,
        int definitionMaximumRows,
        ExecuteReportDto request)
    {
        var hasAggregation = stored.Columns.Any(item => !item.Aggregation.Equals("None", StringComparison.OrdinalIgnoreCase));
        var select = stored.Columns.Select(item => Select(item, dataset.Fields[item.Field])).ToList();
        var groups = hasAggregation
            ? stored.Columns.Where(item => item.Aggregation.Equals("None", StringComparison.OrdinalIgnoreCase))
                .Select(item => dataset.Fields[item.Field].SqlExpression).ToList()
            : [];
        var parameters = new List<SqlParameterValue>
        {
            new("@tenantId", SqlDbType.UniqueIdentifier, tenantId)
        };
        var predicates = new List<string> { dataset.BaselinePredicate, $"{dataset.TenantSql} = @tenantId" };
        for (var index = 0; index < stored.Filters.Count; index++)
            predicates.Add(Filter(stored.Filters[index], dataset.Fields[stored.Filters[index].Field], index, parameters));

        // Compute the governed ceiling before building the count. The count needs only one row
        // beyond that ceiling to determine whether the result was capped; counting an entire large
        // grouped ledger would add database load without changing any response metadata.
        var requestedMaximumRows = Math.Clamp(request.MaxRows ?? 5000, 1, 5000);
        var maximumRows = Math.Min(Math.Clamp(definitionMaximumRows, 1, 5000), requestedMaximumRows);
        var baseSql = $"SELECT {string.Join(", ", select)} FROM {dataset.FromSql} " +
            $"WHERE {string.Join(" AND ", predicates.Select(item => $"({item})"))}" +
            (groups.Count == 0 ? string.Empty : $" GROUP BY {string.Join(", ", groups)}");
        var countSql = $"SELECT COUNT_BIG(1) FROM (SELECT TOP ({maximumRows + 1}) 1 AS [RowPresent] " +
            $"FROM ({baseSql}) AS [GovernedRows]) AS [AdHocCount]";
        var order = stored.Sorts.Count > 0
            ? stored.Sorts.Select(item => $"[{item.Field}] {(item.Descending ? "DESC" : "ASC")}")
            : new[] { $"[{stored.Columns[0].Field}] ASC" };
        // ExecuteReportDto is shared with legacy providers and therefore has no range annotations.
        // Clamp its optional override here so a crafted zero/negative value cannot produce invalid
        // FETCH syntax and an oversized value can never relax the definition's saved ceiling.
        var page = Math.Max(1, request.Page);
        var requestedPageSize = Math.Min(Math.Max(1, request.PageSize), Math.Min(200, maximumRows));
        // Calculate in Int64 because Page is client input; Int32 multiplication could wrap a very
        // large requested page back into the governed result window.
        var offset = (int)Math.Min((long)(page - 1) * requestedPageSize, maximumRows);
        // Never let the final page cross the saved maximum-row governance boundary.
        var pageSize = offset >= maximumRows
            ? requestedPageSize
            : Math.Min(requestedPageSize, maximumRows - offset);
        var pageSql = $"{baseSql} ORDER BY {string.Join(", ", order)} OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";
        var columns = stored.Columns.Select((item, index) => new ReportColumnDto
        {
            Name = item.Field,
            DisplayName = dataset.Fields[item.Field].Label,
            DataType = item.Aggregation.Equals("Count", StringComparison.OrdinalIgnoreCase)
                ? "Integer" : dataset.Fields[item.Field].DataType,
            IsVisible = true,
            Order = index,
            AggregationType = item.Aggregation == "None" ? null : item.Aggregation
        }).ToList();
        return new CompiledFinanceAdHocQuery(countSql, pageSql, parameters, columns,
            page, pageSize, offset, maximumRows);
    }

    private static string Select(FinanceAdHocColumnDto selected, FinanceAdHocField field)
    {
        var expression = selected.Aggregation.ToUpperInvariant() switch
        {
            "NONE" => field.SqlExpression,
            "COUNT" => $"COUNT_BIG({field.SqlExpression})",
            "SUM" => $"SUM({field.SqlExpression})",
            "AVERAGE" => $"AVG({field.SqlExpression})",
            "MINIMUM" => $"MIN({field.SqlExpression})",
            "MAXIMUM" => $"MAX({field.SqlExpression})",
            _ => throw new InvalidOperationException("Unsupported Finance report aggregation.")
        };
        return $"{expression} AS [{selected.Field}]";
    }

    private static string Filter(FinanceAdHocFilterDto filter, FinanceAdHocField field, int index,
        ICollection<SqlParameterValue> parameters)
    {
        var name = $"@filter{index}";
        if (filter.Operator.Equals("IsBlank", StringComparison.OrdinalIgnoreCase))
            return $"({field.SqlExpression} IS NULL OR CONVERT(nvarchar(max), {field.SqlExpression}) = '')";
        if (filter.Operator.Equals("IsNotBlank", StringComparison.OrdinalIgnoreCase))
            return $"({field.SqlExpression} IS NOT NULL AND CONVERT(nvarchar(max), {field.SqlExpression}) <> '')";
        var parsed = Parse(filter.Value!, field.DataType);
        var type = SqlType(field.DataType);
        parameters.Add(new SqlParameterValue(name, type, parsed));
        if (filter.Operator.Equals("Between", StringComparison.OrdinalIgnoreCase))
        {
            var second = name + "To";
            parameters.Add(new SqlParameterValue(second, type, Parse(filter.ValueTo!, field.DataType)));
            return $"{field.SqlExpression} BETWEEN {name} AND {second}";
        }
        if (filter.Operator.Equals("Contains", StringComparison.OrdinalIgnoreCase)
            || filter.Operator.Equals("StartsWith", StringComparison.OrdinalIgnoreCase))
        {
            var escaped = EscapeLike(Convert.ToString(parsed, CultureInfo.InvariantCulture) ?? string.Empty);
            parameters.Remove(parameters.Last());
            parameters.Add(new SqlParameterValue(name, SqlDbType.NVarChar,
                filter.Operator.Equals("Contains", StringComparison.OrdinalIgnoreCase) ? $"%{escaped}%" : $"{escaped}%"));
            return $"CONVERT(nvarchar(max), {field.SqlExpression}) LIKE {name} ESCAPE '\\'";
        }
        var sqlOperator = filter.Operator switch
        {
            "Equals" => "=",
            "NotEquals" => "<>",
            "GreaterThan" => ">",
            "GreaterThanOrEqual" => ">=",
            "LessThan" => "<",
            "LessThanOrEqual" => "<=",
            _ => throw new InvalidOperationException("Unsupported Finance report filter operator.")
        };
        return $"{field.SqlExpression} {sqlOperator} {name}";
    }

    private static object Parse(string value, string dataType) => dataType switch
    {
        "Decimal" => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            ? number : throw new InvalidOperationException($"'{value}' is not a valid decimal filter value."),
        "DateTime" => DateTime.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal, out var date) ? date : throw new InvalidOperationException($"'{value}' is not a valid date filter value."),
        "Boolean" => bool.TryParse(value, out var flag) ? flag
            : throw new InvalidOperationException($"'{value}' is not a valid true/false filter value."),
        _ => value
    };
    private static SqlDbType SqlType(string dataType) => dataType switch
    {
        "Decimal" => SqlDbType.Decimal,
        "DateTime" => SqlDbType.DateTime2,
        "Boolean" => SqlDbType.Bit,
        _ => SqlDbType.NVarChar
    };
    private static string EscapeLike(string value) => value.Replace("\\", "\\\\")
        .Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}

internal sealed record SqlParameterValue(string Name, SqlDbType Type, object? Value);
internal sealed record CompiledFinanceAdHocQuery(
    string CountSql,
    string PageSql,
    IReadOnlyList<SqlParameterValue> Parameters,
    List<ReportColumnDto> Columns,
    int Page,
    int PageSize,
    int Offset,
    int MaximumRows);
