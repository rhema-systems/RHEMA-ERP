using System.Globalization;
using System.Text.Json;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Interfaces.QuantitySurvey;
using ErpSystem.Core.Services.QuantitySurvey;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.QuantitySurvey;

public sealed class QuantitySurveyStatutoryReportService(
    ApplicationDbContext db,
    ICurrentUserProvider currentUser,
    IProjectService projects,
    IHttpContextAccessor httpContextAccessor,
    IAuthorizationService authorization) : IQuantitySurveyStatutoryReportService
{
    public bool CanHandle(string? reportQuery) => QuantitySurveyStatutoryReportCatalogue.Resolve(reportQuery) is not null;

    public bool OwnsIdentifier(string? reportQuery) =>
        !string.IsNullOrWhiteSpace(reportQuery) &&
        reportQuery.StartsWith(QuantitySurveyStatutoryReportCatalogue.QueryPrefix, StringComparison.OrdinalIgnoreCase);

    public string? ResolveCode(string? reportQuery) => QuantitySurveyStatutoryReportCatalogue.Resolve(reportQuery)?.Code;

    public async Task<bool> CanReadAsync(bool isAdministrator, CancellationToken cancellationToken = default)
    {
        try
        {
            await RequirePermissionAsync(QuantitySurveyStatutoryReportCatalogue.ReadPermission, isAdministrator);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public async Task AuthorizeExportAsync(string reportQuery, bool isAdministrator, CancellationToken cancellationToken = default)
    {
        _ = QuantitySurveyStatutoryReportCatalogue.Resolve(reportQuery)
            ?? throw new InvalidOperationException("The Quantity Survey system report is not registered.");
        await RequirePermissionAsync(QuantitySurveyStatutoryReportCatalogue.ExportPermission, isAdministrator);
    }

    public async Task<ReportResultDto> ExecuteAsync(
        string reportQuery,
        ExecuteReportDto request,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        var definition = QuantitySurveyStatutoryReportCatalogue.Resolve(reportQuery)
            ?? throw new InvalidOperationException("The Quantity Survey system report is not registered.");
        await RequirePermissionAsync(QuantitySurveyStatutoryReportCatalogue.ReadPermission, isAdministrator);
        var filters = ReportFilters.Parse(request);
        var project = await projects.GetProjectByIdAsync(filters.ProjectId)
            ?? throw new KeyNotFoundException("The selected project was not found or is outside your assigned project scope.");
        EnsureTenant();

        var rows = definition.Code switch
        {
            QuantitySurveyStatutoryReportCatalogue.BoqSummaryCode => await BoqSummaryAsync(project, cancellationToken),
            QuantitySurveyStatutoryReportCatalogue.ValuationStatementCode => await ValuationStatementAsync(project, cancellationToken),
            QuantitySurveyStatutoryReportCatalogue.VariationLogCode => await VariationLogAsync(project, cancellationToken),
            QuantitySurveyStatutoryReportCatalogue.FinalAccountCode => await FinalAccountAsync(project, cancellationToken),
            QuantitySurveyStatutoryReportCatalogue.ProjectCostStatusCode => await ProjectCostStatusAsync(project, cancellationToken),
            QuantitySurveyStatutoryReportCatalogue.CertificateRegisterCode => await CertificateRegisterAsync(project, cancellationToken),
            QuantitySurveyStatutoryReportCatalogue.RetentionRegisterCode => await RetentionRegisterAsync(project, cancellationToken),
            QuantitySurveyStatutoryReportCatalogue.CostToCompleteCode => await CostToCompleteAsync(project, cancellationToken),
            QuantitySurveyStatutoryReportCatalogue.ContractBalanceCode => await ContractBalanceAsync(project, cancellationToken),
            QuantitySurveyStatutoryReportCatalogue.AuditTrailCode => await AuditTrailAsync(project, cancellationToken),
            _ => throw new InvalidOperationException("The Quantity Survey system report is not implemented.")
        };

        rows = rows.Where(item => filters.InRange(item.EffectiveAt)).ToList();
        return Page(rows, definition, request, filters);
    }

    private async Task<List<ReportRow>> BoqSummaryAsync(ProjectDetailDto project, CancellationToken token)
    {
        var versions = await db.ProjectBoqVersions.AsNoTracking().Where(value =>
            value.TenantId == currentUser.TenantId && value.ProjectId == project.Id && !value.IsDeleted)
            .OrderByDescending(value => value.VersionNumber).ToListAsync(token);
        var versionIds = versions.Select(value => value.Id).ToList();
        var lines = await db.ProjectBoqVersionLines.AsNoTracking().Where(value =>
            value.TenantId == currentUser.TenantId && value.ProjectId == project.Id &&
            versionIds.Contains(value.ProjectBoqVersionId) && !value.IsDeleted).ToListAsync(token);
        var lineGroups = lines.GroupBy(value => value.ProjectBoqVersionId).ToDictionary(value => value.Key, value => value.ToList());

        return versions.Select(version =>
        {
            var versionLines = lineGroups.GetValueOrDefault(version.Id) ?? [];
            return new ReportRow(version.PublishedAt ?? version.ApprovedAt ?? version.SnapshotAt, Row(
                ("ProjectCode", project.ProjectCode), ("Version", version.VersionNumber),
                ("VersionType", version.VersionType.ToString()), ("Status", version.Status),
                ("LineCount", versionLines.Count), ("Currency", SingleCurrency(versionLines.Select(value => value.Currency))),
                ("BoqValue", versionLines.Sum(value => value.LineAmount ?? 0m)), ("ApprovedAt", version.ApprovedAt),
                ("PublishedAt", version.PublishedAt), ("SnapshotHash", version.SnapshotHash)));
        }).ToList();
    }

    private async Task<List<ReportRow>> ValuationStatementAsync(ProjectDetailDto project, CancellationToken token)
    {
        var valuations = await db.ProjectInterimValuations.AsNoTracking().Where(value =>
            value.TenantId == currentUser.TenantId && value.ProjectId == project.Id && !value.IsDeleted)
            .OrderByDescending(value => value.ValuationDate).ToListAsync(token);
        var ids = valuations.Select(value => value.Id).ToList();
        var worksheets = await db.QuantitySurveyValuationWorksheets.AsNoTracking().Where(value =>
            value.TenantId == currentUser.TenantId && value.ProjectId == project.Id &&
            ids.Contains(value.ProjectInterimValuationId) && !value.IsDeleted).ToDictionaryAsync(value => value.ProjectInterimValuationId, token);
        var contracts = await ContractsAsync(valuations.Where(value => value.ContractId.HasValue).Select(value => value.ContractId!.Value), token);

        return valuations.Select(value =>
        {
            worksheets.TryGetValue(value.Id, out var worksheet);
            return new ReportRow(value.ValuationDate, Row(
                ("ValuationNumber", value.ValuationNumber), ("Title", value.Title),
                ("ContractNumber", ContractNumber(contracts, value.ContractId)), ("ValuationDate", value.ValuationDate),
                ("Status", value.Status), ("WorksheetStatus", worksheet?.Status), ("Currency", value.Currency),
                ("GrossWorkValue", value.GrossWorkValue), ("MaterialsOnSiteValue", value.MaterialsOnSiteValue),
                ("VariationValue", value.VariationValue), ("RetentionAmount", value.RetentionAmount),
                ("PreviousCertifiedAmount", value.PreviousCertifiedAmount), ("NetValuationAmount", value.NetValuationAmount),
                ("CertificateReady", worksheet?.CertificateReady ?? false)));
        }).ToList();
    }

    private async Task<List<ReportRow>> VariationLogAsync(ProjectDetailDto project, CancellationToken token)
    {
        var variations = await db.ProjectVariationOrders.AsNoTracking().Include(value => value.RevisedBoqVersion).Where(value =>
            value.TenantId == currentUser.TenantId && value.ProjectId == project.Id && !value.IsDeleted)
            .OrderByDescending(value => value.RequestedDate).ToListAsync(token);
        var contracts = await ContractsAsync(variations.Where(value => value.ContractId.HasValue).Select(value => value.ContractId!.Value), token);
        return variations.Select(value => new ReportRow(value.RequestedDate, Row(
            ("ReferenceNumber", value.ReferenceNumber), ("Title", value.Title),
            ("ContractNumber", ContractNumber(contracts, value.ContractId)), ("VariationType", value.VariationType),
            ("Status", value.Status), ("RequestedDate", value.RequestedDate), ("ApprovedDate", value.ApprovedDate),
            ("Currency", value.Currency), ("EstimatedAmount", value.EstimatedAmount), ("ApprovedAmount", value.ApprovedAmount),
            ("BudgetImpact", value.BudgetImpactAmount), ("ForecastImpact", value.ForecastImpactAmount),
            ("ScheduleImpactDays", value.ScheduleImpactDays), ("ApplicationStatus", QuantitySurveyVariationService.ResolveApplicationStatus(value))))).ToList();
    }

    private async Task<List<ReportRow>> FinalAccountAsync(ProjectDetailDto project, CancellationToken token)
    {
        var accounts = await db.ProjectFinalAccounts.AsNoTracking().Where(value =>
            value.TenantId == currentUser.TenantId && value.ProjectId == project.Id && !value.IsDeleted)
            .OrderByDescending(value => value.CreatedAt).ToListAsync(token);
        var contracts = await ContractsAsync(accounts.Where(value => value.ContractId.HasValue).Select(value => value.ContractId!.Value), token);
        return accounts.Select(value => new ReportRow(value.ClosedAt ?? value.SettlementDate ?? value.CreatedAt, Row(
            ("ProjectCode", project.ProjectCode), ("ContractNumber", ContractNumber(contracts, value.ContractId)),
            ("Status", value.Status), ("Currency", value.Currency), ("ApprovedBoqValue", value.ApprovedBoqValue),
            ("OriginalContractValue", value.OriginalContractValue), ("ApprovedVariationAmount", value.ApprovedVariationAmount),
            ("ApprovedClaimAmount", value.ApprovedClaimAmount), ("ApprovedEscalationAmount", value.ApprovedEscalationAmount),
            ("CertifiedToDate", value.CertifiedToDate), ("RetentionBalance", value.RetentionHeldAmount - value.RetentionReleasedAmount),
            ("PaidToDate", value.PaidToDateAmount), ("FinalAccountValue", value.FinalAccountValue),
            ("SettlementDate", value.SettlementDate), ("ClosedAt", value.ClosedAt)))).ToList();
    }

    private async Task<List<ReportRow>> ProjectCostStatusAsync(ProjectDetailDto project, CancellationToken token)
    {
        var approvedBoq = await db.ProjectBoqVersions.AsNoTracking().Where(value =>
                value.TenantId == currentUser.TenantId && value.ProjectId == project.Id && !value.IsDeleted &&
                value.Status == ProjectBoqVersionStatuses.Approved)
            .OrderByDescending(value => value.VersionNumber).FirstOrDefaultAsync(token);
        var approvedBoqValue = approvedBoq == null ? 0m : await db.ProjectBoqVersionLines.AsNoTracking().Where(value =>
                value.TenantId == currentUser.TenantId && value.ProjectId == project.Id &&
                value.ProjectBoqVersionId == approvedBoq.Id && !value.IsDeleted)
            .SumAsync(value => value.LineAmount ?? 0m, token);
        var contract = project.ContractId.HasValue
            ? await db.Set<Contract>().AsNoTracking().FirstOrDefaultAsync(value => value.TenantId == currentUser.TenantId &&
                value.Id == project.ContractId.Value && !value.IsDeleted, token)
            : null;
        var approvedVariations = await db.ProjectVariationOrders.AsNoTracking().Where(value =>
                value.TenantId == currentUser.TenantId && value.ProjectId == project.Id && !value.IsDeleted &&
                value.Status == ProjectVariationOrderStatuses.Approved)
            .SumAsync(value => value.ApprovedAmount ?? 0m, token);
        var certified = await db.ProjectPaymentCertificates.AsNoTracking().Where(value =>
                value.TenantId == currentUser.TenantId && value.ProjectId == project.Id && !value.IsDeleted &&
                value.Status == ProjectPaymentCertificateStatuses.Approved)
            .SumAsync(value => value.NetCertifiedAmount, token);
        var forecast = await db.Set<ProjectForecastVersion>().AsNoTracking().Where(value =>
                value.TenantId == currentUser.TenantId && value.ProjectId == project.Id && value.IsActive && !value.IsDeleted)
            .OrderByDescending(value => value.VersionNumber).FirstOrDefaultAsync(token);
        var originalContract = contract?.ContractValue ?? 0m;
        var revisedContract = originalContract + approvedVariations;
        var forecastCost = forecast?.EstimateAtCompletion > 0m ? forecast.EstimateAtCompletion : forecast?.ForecastCost ?? 0m;
        var projectedFinal = forecastCost > 0m ? forecastCost : revisedContract;
        var approvedBudget = project.ApprovedBudget ?? 0m;
        return
        [
            new ReportRow(DateTime.UtcNow, Row(
                ("ProjectCode", project.ProjectCode), ("Project", project.Title), ("Status", project.Status),
                ("Currency", project.BaseCurrencyCode ?? contract?.Currency ?? string.Empty), ("ApprovedBudget", approvedBudget),
                ("ApprovedBoq", approvedBoqValue), ("OriginalContract", originalContract), ("ApprovedVariations", approvedVariations),
                ("RevisedContract", revisedContract), ("CertifiedValue", certified), ("ActualCost", project.ActualCost ?? 0m),
                ("ForecastCost", forecastCost), ("ProjectedFinalCost", projectedFinal), ("BudgetVariance", approvedBudget - projectedFinal)))
        ];
    }

    private async Task<List<ReportRow>> CertificateRegisterAsync(ProjectDetailDto project, CancellationToken token)
    {
        var certificates = await db.ProjectPaymentCertificates.AsNoTracking().Where(value =>
            value.TenantId == currentUser.TenantId && value.ProjectId == project.Id && !value.IsDeleted)
            .OrderByDescending(value => value.IssueDate).ToListAsync(token);
        var contracts = await ContractsAsync(certificates.Where(value => value.ContractId.HasValue).Select(value => value.ContractId!.Value), token);
        return certificates.Select(value => new ReportRow(value.IssueDate, Row(
            ("CertificateNumber", value.CertificateNumber), ("Title", value.Title),
            ("ContractNumber", ContractNumber(contracts, value.ContractId)), ("IssueDate", value.IssueDate),
            ("Status", value.Status), ("ApprovalStatus", value.ApprovalStatus), ("Currency", value.Currency),
            ("GrossCertified", value.GrossCertifiedAmount), ("RetentionHeld", value.RetentionHeldAmount),
            ("RetentionReleased", value.RetentionReleasedAmount), ("AdvanceRecovery", value.AdvanceRecoveryAmount),
            ("OtherDeductions", value.OtherDeductionsAmount + value.MaterialDeductionAmount), ("Tax", value.TaxAmount),
            ("NetCertified", value.NetCertifiedAmount), ("ApHandoffStatus", value.ApHandoffStatus),
            ("PaymentStatus", value.PaymentStatusSnapshot)))).ToList();
    }

    private async Task<List<ReportRow>> RetentionRegisterAsync(ProjectDetailDto project, CancellationToken token)
    {
        var certificates = await db.ProjectPaymentCertificates.AsNoTracking().Where(value =>
            value.TenantId == currentUser.TenantId && value.ProjectId == project.Id && !value.IsDeleted)
            .OrderByDescending(value => value.IssueDate).ToListAsync(token);
        var contracts = await ContractsAsync(certificates.Where(value => value.ContractId.HasValue).Select(value => value.ContractId!.Value), token);
        return certificates.Select(value => new ReportRow(value.IssueDate, Row(
            ("CertificateNumber", value.CertificateNumber), ("ContractNumber", ContractNumber(contracts, value.ContractId)),
            ("IssueDate", value.IssueDate), ("Status", value.Status), ("Currency", value.Currency),
            ("RetentionHeld", value.RetentionHeldAmount), ("RetentionReleased", value.RetentionReleasedAmount),
            ("RetentionBalance", value.RetentionHeldAmount - value.RetentionReleasedAmount),
            ("PaymentStatus", value.PaymentStatusSnapshot)))).ToList();
    }

    private async Task<List<ReportRow>> CostToCompleteAsync(ProjectDetailDto project, CancellationToken token)
    {
        var budget = await db.ProjectBudgetRevisions.AsNoTracking().Where(value =>
                value.TenantId == currentUser.TenantId && value.ProjectId == project.Id &&
                value.Status == "Approved" && !value.IsDeleted)
            .OrderByDescending(value => value.VersionNumber).FirstOrDefaultAsync(token);
        var forecast = await db.Set<ProjectForecastVersion>().AsNoTracking().Where(value =>
                value.TenantId == currentUser.TenantId && value.ProjectId == project.Id && value.IsActive && !value.IsDeleted)
            .OrderByDescending(value => value.VersionNumber).FirstOrDefaultAsync(token);
        var approvedBudget = budget?.ApprovedBudget ?? project.ApprovedBudget ?? 0m;
        var actualCost = project.ActualCost ?? 0m;
        var committed = budget?.CommittedCost ?? 0m;
        var projectedFinal = forecast?.EstimateAtCompletion > 0m
            ? forecast.EstimateAtCompletion
            : forecast?.ForecastCost > 0m ? forecast.ForecastCost : actualCost + committed;
        var costToComplete = Math.Max(0m, projectedFinal - actualCost);
        return
        [
            new ReportRow(DateTime.UtcNow, Row(
                ("ProjectCode", project.ProjectCode), ("Project", project.Title), ("Currency", project.BaseCurrencyCode ?? string.Empty),
                ("ApprovedBudget", approvedBudget), ("ActualCost", actualCost), ("CommittedCost", committed),
                ("CostToComplete", costToComplete), ("ProjectedFinalCost", projectedFinal),
                ("BudgetVariance", approvedBudget - projectedFinal)))
        ];
    }

    private async Task<List<ReportRow>> ContractBalanceAsync(ProjectDetailDto project, CancellationToken token)
    {
        if (!project.ContractId.HasValue) return [];
        var contract = await db.Set<Contract>().AsNoTracking()
            .Include(value => value.BusinessPartner)
            .FirstOrDefaultAsync(value => value.TenantId == currentUser.TenantId &&
                value.Id == project.ContractId.Value && !value.IsDeleted, token);
        if (contract is null) return [];
        var approvedVariations = await db.ProjectVariationOrders.AsNoTracking().Where(value =>
                value.TenantId == currentUser.TenantId && value.ProjectId == project.Id &&
                value.ContractId == contract.Id && value.Status == ProjectVariationOrderStatuses.Approved && !value.IsDeleted)
            .SumAsync(value => value.ApprovedAmount ?? 0m, token);
        var certificates = await db.ProjectPaymentCertificates.AsNoTracking().Where(value =>
                value.TenantId == currentUser.TenantId && value.ProjectId == project.Id &&
                value.ContractId == contract.Id &&
                (value.Status == ProjectPaymentCertificateStatuses.Approved ||
                 value.Status == ProjectPaymentCertificateStatuses.Issued ||
                 value.Status == ProjectPaymentCertificateStatuses.Paid) &&
                !value.IsDeleted)
            .ToListAsync(token);
        var revised = contract.ContractValue + approvedVariations;
        var certified = certificates.Sum(value => value.NetCertifiedAmount);
        var retention = certificates.Sum(value => value.RetentionHeldAmount - value.RetentionReleasedAmount);
        return
        [
            new ReportRow(contract.ActivatedAt ?? contract.StartDate ?? contract.CreatedAt, Row(
                ("ContractNumber", contract.ContractNumber), ("Supplier", contract.BusinessPartner.PartnerName),
                ("Currency", contract.Currency), ("OriginalContract", contract.ContractValue),
                ("ApprovedVariations", approvedVariations), ("RevisedContract", revised),
                ("CertifiedToDate", certified), ("RetentionBalance", retention),
                ("ContractBalance", revised - certified)))
        ];
    }

    private async Task<List<ReportRow>> AuditTrailAsync(ProjectDetailDto project, CancellationToken token)
    {
        var projectToken = project.Id.ToString();
        var logs = await db.Set<AuditLog>().AsNoTracking().Where(value =>
                value.TenantId == currentUser.TenantId && !value.IsDeleted &&
                (value.Resource.StartsWith("QuantitySurvey") || value.Resource.StartsWith("ProjectBoq") ||
                 value.Resource.StartsWith("ProjectInterimValuation") || value.Resource.StartsWith("ProjectPaymentCertificate") ||
                 value.Resource.StartsWith("ProjectVariation") || value.Resource.StartsWith("ProjectFinalAccount")) &&
                ((value.OldValues != null && value.OldValues.Contains(projectToken)) ||
                 (value.NewValues != null && value.NewValues.Contains(projectToken))))
            .OrderByDescending(value => value.Timestamp).ToListAsync(token);
        return logs.Select(value => new ReportRow(value.Timestamp, Row(
            ("Timestamp", value.Timestamp), ("Username", value.Username), ("Action", value.Action),
            ("Resource", value.Resource), ("ResourceId", value.ResourceId), ("IpAddress", value.IpAddress),
            ("Correlation", value.NewValues ?? value.OldValues)))).ToList();
    }

    private async Task<Dictionary<Guid, Contract>> ContractsAsync(IEnumerable<Guid> ids, CancellationToken token)
    {
        var values = ids.Distinct().ToList();
        if (values.Count == 0) return [];
        return await db.Set<Contract>().AsNoTracking().Where(value =>
            value.TenantId == currentUser.TenantId && values.Contains(value.Id) && !value.IsDeleted)
            .ToDictionaryAsync(value => value.Id, token);
    }

    private static string? ContractNumber(IReadOnlyDictionary<Guid, Contract> contracts, Guid? id) =>
        id.HasValue && contracts.TryGetValue(id.Value, out var contract) ? contract.ContractNumber : null;

    private static string SingleCurrency(IEnumerable<string?> values)
    {
        var currencies = values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return currencies.Count switch { 0 => string.Empty, 1 => currencies[0], _ => "Mixed" };
    }

    private async Task RequirePermissionAsync(string permission, bool isAdministrator)
    {
        if (isAdministrator) return;
        var principal = httpContextAccessor.HttpContext?.User
            ?? throw new UnauthorizedAccessException("Authentication is required to access Quantity Survey reports.");
        if (!(await authorization.AuthorizeAsync(principal, permission)).Succeeded)
            throw new UnauthorizedAccessException($"Permission '{permission}' is required.");
    }

    private Guid EnsureTenant()
    {
        if (!currentUser.IsAuthenticated || currentUser.TenantId == Guid.Empty)
            throw new UnauthorizedAccessException("A valid tenant context is required to access Quantity Survey reports.");
        return currentUser.TenantId;
    }

    private static ReportResultDto Page(
        IReadOnlyList<ReportRow> rows,
        QuantitySurveySystemReportDefinition definition,
        ExecuteReportDto request,
        ReportFilters filters)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 1000);
        if (request.MaxRows is > 0) pageSize = Math.Min(pageSize, Math.Clamp(request.MaxRows.Value, 1, 1000));
        var totalPages = rows.Count == 0 ? 0 : (int)Math.Ceiling(rows.Count / (double)pageSize);
        return new ReportResultDto
        {
            TotalRows = rows.Count,
            Columns = definition.Columns.Select((column, index) => new ReportColumnDto
            {
                Name = column.Name, DisplayName = column.DisplayName, DataType = column.DataType,
                Format = column.Format, IsVisible = column.IsVisible, Order = index, AggregationType = column.AggregationType
            }).ToList(),
            Data = rows.Skip((page - 1) * pageSize).Take(pageSize).Select(value => value.Values).ToList(),
            CurrentPage = page, PageSize = pageSize, TotalPages = totalPages,
            HasNextPage = page < totalPages, HasPreviousPage = page > 1 && totalPages > 0,
            Metadata = new ReportMetadataDto
            {
                Parameters = filters.ToMetadata(), Query = definition.Query, DataAsOf = DateTime.UtcNow,
                DataSource = "Authoritative tenant and project-scoped ERP records",
                Statistics = new Dictionary<string, object> { ["systemCode"] = definition.Code, ["totalRows"] = rows.Count }
            }
        };
    }

    private static Dictionary<string, object> Row(params (string Key, object? Value)[] values) =>
        values.ToDictionary(value => value.Key, value => value.Value!);

    private sealed record ReportRow(DateTime EffectiveAt, Dictionary<string, object> Values);

    private sealed record ReportFilters(Guid ProjectId, DateTime? StartUtc, DateTime? EndExclusiveUtc)
    {
        public static ReportFilters Parse(ExecuteReportDto request)
        {
            var projectId = GetGuid(request.Parameters, "projectId");
            if (!projectId.HasValue || projectId.Value == Guid.Empty)
                throw new InvalidOperationException("Select a project before running the Quantity Survey report.");
            var start = request.StartDate ?? GetDate(request.Parameters, "startDate");
            var end = request.EndDate ?? GetDate(request.Parameters, "endDate");
            start = start.HasValue ? DateTime.SpecifyKind(start.Value.Date, DateTimeKind.Utc) : null;
            var endExclusive = end.HasValue ? DateTime.SpecifyKind(end.Value.Date.AddDays(1), DateTimeKind.Utc) : (DateTime?)null;
            if (start.HasValue && endExclusive.HasValue && start.Value >= endExclusive.Value)
                throw new InvalidOperationException("Start date must be on or before end date.");
            return new ReportFilters(projectId.Value, start, endExclusive);
        }

        public bool InRange(DateTime value) => (!StartUtc.HasValue || value >= StartUtc.Value) &&
                                               (!EndExclusiveUtc.HasValue || value < EndExclusiveUtc.Value);

        public Dictionary<string, object> ToMetadata()
        {
            var values = new Dictionary<string, object> { ["projectId"] = ProjectId };
            if (StartUtc.HasValue) values["startDate"] = StartUtc.Value;
            if (EndExclusiveUtc.HasValue) values["endDate"] = EndExclusiveUtc.Value.AddDays(-1);
            return values;
        }

        private static string? GetString(Dictionary<string, object>? values, string key)
        {
            if (values is null || !values.TryGetValue(key, out var value) || value is null) return null;
            var text = value is JsonElement element ? element.ToString() : Convert.ToString(value, CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        private static DateTime? GetDate(Dictionary<string, object>? values, string key) =>
            DateTime.TryParse(GetString(values, key), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value : null;

        private static Guid? GetGuid(Dictionary<string, object>? values, string key) =>
            Guid.TryParse(GetString(values, key), out var value) ? value : null;
    }
}
