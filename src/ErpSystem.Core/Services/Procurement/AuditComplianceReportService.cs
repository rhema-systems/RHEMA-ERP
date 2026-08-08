using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Procurement;

public sealed class AuditComplianceReportService : IAuditComplianceReportService
{
    private const string SourceType = "AuditComplianceReport";
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUser;
    private readonly IProcurementAccessControlService _access;
    private readonly IInventoryDisposalReportSource _disposals;

    public AuditComplianceReportService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUser,
        IProcurementAccessControlService access,
        IInventoryDisposalReportSource disposals)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _access = access;
        _disposals = disposals;
    }

    public bool CanHandle(string? reportQuery) => AuditComplianceReportCatalogue.Resolve(reportQuery) is not null;

    public bool OwnsIdentifier(string? reportQuery) =>
        !string.IsNullOrWhiteSpace(reportQuery) &&
        reportQuery.StartsWith(AuditComplianceReportCatalogue.QueryPrefix, StringComparison.OrdinalIgnoreCase);

    public string? ResolveCode(string? reportQuery) => AuditComplianceReportCatalogue.Resolve(reportQuery)?.Code;

    public async Task<bool> CanReadAsync(bool isAdministrator, CancellationToken cancellationToken = default)
    {
        if (_currentUser.TenantId == Guid.Empty) return false;
        if (isAdministrator) return true;
        try
        {
            return (await _access.CheckCapabilityAsync(
                Capability(AuditComplianceReportCatalogue.ReadPermission, "catalogue"),
                Correlation(), cancellationToken)).Allowed;
        }
        catch (ProcurementAccessValidationException) { return false; }
        catch (ProcurementAccessNotFoundException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    public async Task AuthorizeExportAsync(
        string reportQuery,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantContext();
        var definition = Resolve(reportQuery);
        await EnsureCapabilityAsync(AuditComplianceReportCatalogue.ExportPermission,
            definition.Code, isAdministrator, cancellationToken);
    }

    public async Task<ReportResultDto> ExecuteAsync(
        string reportQuery,
        ExecuteReportDto request,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        EnsureTenantContext();
        var definition = Resolve(reportQuery);
        await EnsureCapabilityAsync(AuditComplianceReportCatalogue.ReadPermission,
            definition.Code, isAdministrator, cancellationToken);
        var filters = ReportFilters.Parse(request);

        return definition.Code switch
        {
            AuditComplianceReportCatalogue.OpeningCode =>
                await ExecuteOpeningAsync(definition, filters, request, cancellationToken),
            AuditComplianceReportCatalogue.CommitteeSignOffCode =>
                await ExecuteCommitteeSignOffAsync(definition, filters, request, cancellationToken),
            AuditComplianceReportCatalogue.DueDiligenceCode =>
                await ExecuteDueDiligenceAsync(definition, filters, request, cancellationToken),
            AuditComplianceReportCatalogue.MatchingExceptionCode =>
                await ExecuteMatchingExceptionsAsync(definition, filters, request, cancellationToken),
            AuditComplianceReportCatalogue.PurchaseOrderPaymentCode =>
                await ExecutePurchaseOrderPaymentsAsync(definition, filters, request, cancellationToken),
            AuditComplianceReportCatalogue.InventoryAdjustmentCode =>
                await ExecuteInventoryAdjustmentsAsync(definition, filters, request, cancellationToken),
            AuditComplianceReportCatalogue.OverrideCode =>
                await ExecuteOverridesAsync(definition, filters, request, cancellationToken),
            AuditComplianceReportCatalogue.DisposalCode =>
                await ExecuteDisposalsAsync(definition, filters, request, cancellationToken),
            _ => throw new InvalidOperationException("The audit and compliance system report is not implemented.")
        };
    }

    private async Task<ReportResultDto> ExecuteOpeningAsync(
        AuditComplianceSystemReportDefinition definition,
        ReportFilters filters,
        ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var formal = Query<ProcurementTenderControl>().Where(item => item.OpenedAtUtc.HasValue);
        if (filters.StartUtc.HasValue) formal = formal.Where(item => item.OpenedAtUtc >= filters.StartUtc.Value);
        if (filters.EndExclusiveUtc.HasValue) formal = formal.Where(item => item.OpenedAtUtc < filters.EndExclusiveUtc.Value);
        if (!string.IsNullOrWhiteSpace(filters.Status) &&
            Enum.TryParse<ProcurementTenderControlStatus>(filters.Status, true, out var tenderStatus))
            formal = formal.Where(item => item.Status == tenderStatus);
        else if (!string.IsNullOrWhiteSpace(filters.Status))
            formal = formal.Where(_ => false);

        var rfq = Query<ProcurementRfqOpeningRegister>();
        if (filters.StartUtc.HasValue) rfq = rfq.Where(item => item.OpenedAtUtc >= filters.StartUtc.Value);
        if (filters.EndExclusiveUtc.HasValue) rfq = rfq.Where(item => item.OpenedAtUtc < filters.EndExclusiveUtc.Value);
        if (!string.IsNullOrWhiteSpace(filters.Status) &&
            !filters.Status.Equals("Closed", StringComparison.OrdinalIgnoreCase))
            rfq = rfq.Where(_ => false);

        var query = formal.Select(item => new OpeningRow
            {
                SourceType = "Tender",
                SourceReference = item.Tender.TenderNumber,
                StatusDomain = 1,
                StatusValue = (int)item.Status,
                OpenedAt = item.OpenedAtUtc!.Value,
                ClosedAt = null,
                EntryCount = item.SubmissionReceipts.Count,
                LateEntryCount = item.SubmissionReceipts.Count(entry =>
                    entry.Disposition == ProcurementTenderSubmissionDisposition.LateRejected),
                ParticipantCount = 0,
                ParticipantSignOffCaptured = item.OpeningSnapshotJson != null &&
                    item.OpeningEvidenceReference != null && item.OpeningIntegrityHash != null,
                EvidenceReference = item.OpeningEvidenceReference,
                IntegrityHash = item.OpeningIntegrityHash ?? item.IntegrityHash
            })
            .Concat(rfq.Select(item => new OpeningRow
            {
                SourceType = "RFQ",
                SourceReference = item.Rfq.RfqNumber,
                StatusDomain = 2,
                StatusValue = 0,
                OpenedAt = item.OpenedAtUtc,
                ClosedAt = item.ClosedAtUtc,
                EntryCount = item.Entries.Count,
                LateEntryCount = item.Entries.Count(entry =>
                    entry.Disposition == ProcurementRfqReceiptDisposition.LateRejected),
                ParticipantCount = item.Participants.Count,
                ParticipantSignOffCaptured = item.Participants.Any() && item.Participants.All(participant =>
                    participant.SignedAtUtc <= item.ClosedAtUtc && participant.SignatureReference != ""),
                EvidenceReference = item.EvidenceReference,
                IntegrityHash = item.IntegrityHash
            }))
            .OrderByDescending(item => item.OpenedAt).ThenBy(item => item.SourceReference);

        return await PageQueryAsync(query, definition, request, filters, item => Row(
            ("SourceType", item.SourceType), ("SourceReference", item.SourceReference),
            ("Status", item.StatusDomain == 1
                ? ((ProcurementTenderControlStatus)item.StatusValue).ToString()
                : "Closed"),
            ("OpenedAt", item.OpenedAt), ("ClosedAt", item.ClosedAt), ("EntryCount", item.EntryCount),
            ("LateEntryCount", item.LateEntryCount), ("ParticipantCount", item.ParticipantCount),
            ("ParticipantSignOffCaptured", item.ParticipantSignOffCaptured),
            ("EvidenceReference", item.EvidenceReference), ("IntegrityHash", item.IntegrityHash)), cancellationToken);
    }

    private async Task<ReportResultDto> ExecuteCommitteeSignOffAsync(
        AuditComplianceSystemReportDefinition definition,
        ReportFilters filters,
        ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var query = Query<ProcurementEvaluationMeeting>();
        if (filters.StartUtc.HasValue) query = query.Where(item => item.ScheduledAtUtc >= filters.StartUtc.Value);
        if (filters.EndExclusiveUtc.HasValue) query = query.Where(item => item.ScheduledAtUtc < filters.EndExclusiveUtc.Value);
        if (!string.IsNullOrWhiteSpace(filters.Status) &&
            Enum.TryParse<ProcurementEvaluationMeetingStatus>(filters.Status, true, out var status))
            query = query.Where(item => item.Status == status);
        else if (!string.IsNullOrWhiteSpace(filters.Status)) query = query.Where(_ => false);

        var projected = query.Select(item => new CommitteeRow
        {
            SourceType = item.CommitteeControl.SourceType,
            SourceReference = item.CommitteeControl.SourceReference,
            CommitteeCode = item.CommitteeControl.CommitteeCode,
            CommitteeName = item.CommitteeControl.CommitteeName,
            Version = item.CommitteeControl.Version,
            Phase = item.Phase,
            MeetingSequence = item.Sequence,
            Status = item.Status,
            ScheduledAt = item.ScheduledAtUtc,
            ClosedAt = item.ClosedAtUtc,
            RequiredQuorum = item.CommitteeControl.RequiredQuorum,
            EligibleVoters = item.EligibleVotingMemberCount,
            SignedAttendance = item.AttendanceRecords.Count(attendance => attendance.SignedAtUtc.HasValue),
            QuorumMet = item.QuorumMet,
            SignedScoreSheets = item.ScoreSheets.Count(score => score.SignatureReference != ""),
            EvidenceReference = item.EvidenceReference,
            RemoteEvidenceReference = item.RemoteMeetingEvidenceReference,
            IntegrityHash = item.QuorumIntegrityHash
        }).OrderByDescending(item => item.ScheduledAt).ThenBy(item => item.SourceReference)
            .ThenBy(item => item.MeetingSequence);

        return await PageQueryAsync(projected, definition, request, filters, item => Row(
            ("SourceType", item.SourceType.ToString()), ("SourceReference", item.SourceReference),
            ("CommitteeCode", item.CommitteeCode), ("CommitteeName", item.CommitteeName), ("Version", item.Version),
            ("Phase", item.Phase.ToString()), ("MeetingSequence", item.MeetingSequence),
            ("Status", item.Status.ToString()), ("ScheduledAt", item.ScheduledAt), ("ClosedAt", item.ClosedAt),
            ("RequiredQuorum", item.RequiredQuorum), ("EligibleVoters", item.EligibleVoters),
            ("SignedAttendance", item.SignedAttendance), ("QuorumMet", item.QuorumMet),
            ("SignedScoreSheets", item.SignedScoreSheets), ("EvidenceReference", item.EvidenceReference),
            ("RemoteEvidenceReference", item.RemoteEvidenceReference), ("IntegrityHash", item.IntegrityHash)), cancellationToken);
    }

    private async Task<ReportResultDto> ExecuteDueDiligenceAsync(
        AuditComplianceSystemReportDefinition definition,
        ReportFilters filters,
        ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var query = Query<ProcurementSupplierDueDiligenceReview>();
        if (filters.StartUtc.HasValue) query = query.Where(item => item.ReviewPeriodEndUtc >= filters.StartUtc.Value);
        if (filters.EndExclusiveUtc.HasValue) query = query.Where(item => item.ReviewPeriodStartUtc < filters.EndExclusiveUtc.Value);
        if (!string.IsNullOrWhiteSpace(filters.Status) &&
            Enum.TryParse<ProcurementSupplierDueDiligenceStatus>(filters.Status, true, out var status))
            query = query.Where(item => item.Status == status);
        else if (!string.IsNullOrWhiteSpace(filters.Status)) query = query.Where(_ => false);

        var projected = query.Select(item => new DueDiligenceRow
        {
            ReviewReference = item.ReviewReference,
            SupplierCode = item.BusinessPartner.PartnerCode,
            SupplierName = item.BusinessPartner.PartnerName,
            ReviewType = item.ReviewType,
            Status = item.Status,
            Outcome = item.Outcome,
            PeriodStart = item.ReviewPeriodStartUtc,
            PeriodEnd = item.ReviewPeriodEndUtc,
            NextReviewDue = item.NextReviewDueAtUtc,
            PolicyProfile = item.PolicyProfileCode,
            PolicyVersion = item.PolicyProfileVersion,
            CheckCount = item.Checks.Count,
            FailedChecks = item.Checks.Count(check => check.Status == ProcurementSupplierDueDiligenceCheckStatus.Adverse),
            PendingChecks = item.Checks.Count(check => check.Status == ProcurementSupplierDueDiligenceCheckStatus.Pending),
            EvidenceCount = item.Checks.SelectMany(check => check.EvidenceLinks).Count(),
            SubmittedAt = item.SubmittedAtUtc,
            ApprovedAt = item.ApprovedAtUtc,
            RejectedAt = item.RejectedAtUtc,
            IntegrityHash = item.IntegrityHash
        }).OrderByDescending(item => item.PeriodEnd).ThenBy(item => item.ReviewReference);

        return await PageQueryAsync(projected, definition, request, filters, item => Row(
            ("ReviewReference", item.ReviewReference), ("SupplierCode", item.SupplierCode),
            ("SupplierName", item.SupplierName), ("ReviewType", item.ReviewType.ToString()),
            ("Status", item.Status.ToString()), ("Outcome", item.Outcome.ToString()),
            ("PeriodStart", item.PeriodStart), ("PeriodEnd", item.PeriodEnd),
            ("NextReviewDue", item.NextReviewDue), ("PolicyProfile", item.PolicyProfile),
            ("PolicyVersion", item.PolicyVersion), ("CheckCount", item.CheckCount),
            ("FailedChecks", item.FailedChecks), ("PendingChecks", item.PendingChecks),
            ("EvidenceCount", item.EvidenceCount), ("SubmittedAt", item.SubmittedAt),
            ("ApprovedAt", item.ApprovedAt), ("RejectedAt", item.RejectedAt),
            ("IntegrityHash", item.IntegrityHash)), cancellationToken);
    }

    private async Task<ReportResultDto> ExecuteMatchingExceptionsAsync(
        AuditComplianceSystemReportDefinition definition,
        ReportFilters filters,
        ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var query = Query<VendorInvoiceMatchException>();
        if (filters.StartUtc.HasValue) query = query.Where(item => item.RequestedAtUtc >= filters.StartUtc.Value);
        if (filters.EndExclusiveUtc.HasValue) query = query.Where(item => item.RequestedAtUtc < filters.EndExclusiveUtc.Value);
        if (!string.IsNullOrWhiteSpace(filters.Status) &&
            Enum.TryParse<VendorInvoiceMatchExceptionStatus>(filters.Status, true, out var status))
            query = query.Where(item => item.Status == status);
        else if (!string.IsNullOrWhiteSpace(filters.Status)) query = query.Where(_ => false);

        var projected = query.Select(item => new MatchExceptionRow
        {
            ExceptionReference = item.VendorInvoice.InvoiceNumber + "-EX-" + item.Sequence,
            InvoiceNumber = item.VendorInvoice.InvoiceNumber,
            SupplierName = item.VendorInvoice.SupplierName,
            PurchaseOrderNumber = item.PurchaseOrder.OrderNumber,
            Status = item.Status,
            VarianceType = item.VarianceType,
            PriceTolerancePercent = item.PriceTolerancePercent,
            QuantityTolerancePercent = item.QuantityTolerancePercent,
            RootCauseCategory = item.RootCauseCategory,
            CorrectiveActionStatus = item.CorrectiveActionStatus,
            CorrectiveActionDue = item.CorrectiveActionDueAtUtc,
            RequestedBy = item.RequestedByName,
            RequestedAt = item.RequestedAtUtc,
            DecisionBy = item.FinalApprovedByName ?? item.RejectedByName,
            DecisionAt = item.FinalApprovedAtUtc ?? item.RejectedAtUtc,
            ExpiresAt = item.ExpiresAtUtc,
            EvidenceCount = item.Evidence.Count,
            ApprovalControlEventId = item.ApprovalControlEventId,
            IntegrityHash = item.IntegrityHash
        }).OrderByDescending(item => item.RequestedAt).ThenBy(item => item.ExceptionReference);

        return await PageQueryAsync(projected, definition, request, filters, item => Row(
            ("ExceptionReference", item.ExceptionReference), ("InvoiceNumber", item.InvoiceNumber),
            ("SupplierName", item.SupplierName), ("PurchaseOrderNumber", item.PurchaseOrderNumber),
            ("Status", item.Status.ToString()), ("VarianceType", item.VarianceType),
            ("PriceTolerancePercent", item.PriceTolerancePercent),
            ("QuantityTolerancePercent", item.QuantityTolerancePercent),
            ("RootCauseCategory", item.RootCauseCategory),
            ("CorrectiveActionStatus", item.CorrectiveActionStatus.ToString()),
            ("CorrectiveActionDue", item.CorrectiveActionDue), ("RequestedBy", item.RequestedBy),
            ("RequestedAt", item.RequestedAt), ("DecisionBy", item.DecisionBy), ("DecisionAt", item.DecisionAt),
            ("ExpiresAt", item.ExpiresAt), ("EvidenceCount", item.EvidenceCount),
            ("ApprovalControlEventId", item.ApprovalControlEventId), ("IntegrityHash", item.IntegrityHash)), cancellationToken);
    }

    private async Task<ReportResultDto> ExecutePurchaseOrderPaymentsAsync(
        AuditComplianceSystemReportDefinition definition,
        ReportFilters filters,
        ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var query = Query<VendorPaymentAllocation>().Where(item => item.VendorInvoice.PurchaseOrderId.HasValue);
        if (filters.StartUtc.HasValue) query = query.Where(item => item.AllocationDate >= filters.StartUtc.Value);
        if (filters.EndExclusiveUtc.HasValue) query = query.Where(item => item.AllocationDate < filters.EndExclusiveUtc.Value);
        if (!string.IsNullOrWhiteSpace(filters.Status) &&
            Enum.TryParse<VendorPaymentStatus>(filters.Status, true, out var status))
            query = query.Where(item => item.VendorPayment.Status == status);
        else if (!string.IsNullOrWhiteSpace(filters.Status)) query = query.Where(_ => false);

        var projected = query.Select(item => new PaymentRow
        {
            PaymentNumber = item.VendorPayment.PaymentNumber,
            PaymentDate = item.VendorPayment.PaymentDate,
            Status = item.VendorPayment.Status,
            SupplierCode = item.VendorPayment.Supplier.SupplierCode,
            SupplierName = item.VendorPayment.Supplier.Name,
            InvoiceNumber = item.VendorInvoice.InvoiceNumber,
            PurchaseOrderNumber = item.VendorInvoice.PurchaseOrder!.OrderNumber,
            Currency = item.VendorPayment.CurrencyCode,
            PaymentAmount = item.VendorPayment.TotalAmount,
            AllocatedAmount = item.AllocatedAmount,
            PaymentMethod = item.VendorPayment.PaymentMethod,
            IsReversal = item.IsReversal,
            ReadinessEvaluatedAt = item.PaymentReadinessEvaluatedAtUtc,
            ReadinessControlEventId = item.PaymentReadinessControlEventId,
            SodControlEventId = item.VendorPayment.InvoicePaymentSodControlEventId,
            AuthorizedAt = item.VendorPayment.AuthorizedDate,
            ClearedAt = item.VendorPayment.ClearedDate,
            JournalEntryId = item.VendorPayment.JournalEntryId,
            TransactionReference = item.VendorPayment.TransactionReference
        }).OrderByDescending(item => item.PaymentDate).ThenBy(item => item.PaymentNumber)
            .ThenBy(item => item.InvoiceNumber);

        return await PageQueryAsync(projected, definition, request, filters, item => Row(
            ("PaymentNumber", item.PaymentNumber), ("PaymentDate", item.PaymentDate),
            ("Status", item.Status.ToString()), ("SupplierCode", item.SupplierCode),
            ("SupplierName", item.SupplierName), ("InvoiceNumber", item.InvoiceNumber),
            ("PurchaseOrderNumber", item.PurchaseOrderNumber), ("Currency", item.Currency),
            ("PaymentAmount", item.PaymentAmount), ("AllocatedAmount", item.AllocatedAmount),
            ("PaymentMethod", item.PaymentMethod.ToString()), ("IsReversal", item.IsReversal),
            ("ReadinessEvaluatedAt", item.ReadinessEvaluatedAt),
            ("ReadinessControlEventId", item.ReadinessControlEventId), ("SodControlEventId", item.SodControlEventId),
            ("AuthorizedAt", item.AuthorizedAt), ("ClearedAt", item.ClearedAt),
            ("JournalEntryId", item.JournalEntryId), ("TransactionReference", item.TransactionReference)), cancellationToken);
    }

    private async Task<ReportResultDto> ExecuteInventoryAdjustmentsAsync(
        AuditComplianceSystemReportDefinition definition,
        ReportFilters filters,
        ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var query = Query<StockAdjustmentItem>();
        if (filters.StartUtc.HasValue) query = query.Where(item => item.Adjustment.AdjustmentDate >= filters.StartUtc.Value);
        if (filters.EndExclusiveUtc.HasValue) query = query.Where(item => item.Adjustment.AdjustmentDate < filters.EndExclusiveUtc.Value);
        if (filters.WarehouseId.HasValue) query = query.Where(item => item.Adjustment.WarehouseId == filters.WarehouseId.Value);
        if (!string.IsNullOrWhiteSpace(filters.Status))
            query = query.Where(item => item.Adjustment.Status == filters.Status);

        var scopes = await query.Select(item => new InventoryScope(item.Adjustment.WarehouseId, item.LocationId))
            .Distinct().ToListAsync(cancellationToken);
        var allowed = await ReadableScopesAsync(scopes, "inventory-adjustment-register", cancellationToken);
        if (scopes.Count > 0 && allowed.Count == 0)
            throw new UnauthorizedAccessException("The current actor has no assigned inventory scope for this report.");
        if (allowed.Count > 0) query = query.Where(BuildAdjustmentScopePredicate(allowed));

        var projected = query.Select(item => new AdjustmentRow
        {
            AdjustmentNumber = item.Adjustment.AdjustmentNumber,
            AdjustmentDate = item.Adjustment.AdjustmentDate,
            WarehouseCode = item.Adjustment.Warehouse == null ? string.Empty : item.Adjustment.Warehouse.Code,
            WarehouseName = item.Adjustment.Warehouse == null ? string.Empty : item.Adjustment.Warehouse.Name,
            Status = item.Adjustment.Status,
            ReasonCode = item.Adjustment.ReasonCode,
            ItemCode = item.InventoryItem.ItemCode,
            ItemName = item.InventoryItem.Name,
            LocationCode = item.Location == null ? null : item.Location.LocationCode,
            SystemQuantity = item.SystemQuantity,
            PhysicalQuantity = item.PhysicalQuantity,
            AdjustmentQuantity = item.AdjustmentQuantity,
            UnitCost = item.UnitCost,
            AdjustmentValue = item.AdjustmentValue,
            SubmittedAt = item.Adjustment.SubmittedAtUtc,
            ApprovedAt = item.Adjustment.ApprovedAt,
            PostedAt = item.Adjustment.PostedAtUtc,
            ReversedAt = item.Adjustment.ReversedAtUtc,
            FinanceJournalEntryId = item.Adjustment.FinanceJournalEntryId,
            EvidenceCount = item.Adjustment.Evidence.Count,
            ActionCount = item.Adjustment.Actions.Count,
            IntegrityHash = item.Adjustment.IntegrityHash
        }).OrderByDescending(item => item.AdjustmentDate).ThenBy(item => item.AdjustmentNumber)
            .ThenBy(item => item.ItemCode);

        return await PageQueryAsync(projected, definition, request, filters, item => Row(
            ("AdjustmentNumber", item.AdjustmentNumber), ("AdjustmentDate", item.AdjustmentDate),
            ("WarehouseCode", item.WarehouseCode), ("WarehouseName", item.WarehouseName),
            ("Status", item.Status), ("ReasonCode", item.ReasonCode), ("ItemCode", item.ItemCode),
            ("ItemName", item.ItemName), ("LocationCode", item.LocationCode),
            ("SystemQuantity", item.SystemQuantity), ("PhysicalQuantity", item.PhysicalQuantity),
            ("AdjustmentQuantity", item.AdjustmentQuantity), ("UnitCost", item.UnitCost),
            ("AdjustmentValue", item.AdjustmentValue), ("SubmittedAt", item.SubmittedAt),
            ("ApprovedAt", item.ApprovedAt), ("PostedAt", item.PostedAt), ("ReversedAt", item.ReversedAt),
            ("FinanceJournalEntryId", item.FinanceJournalEntryId), ("EvidenceCount", item.EvidenceCount),
            ("ActionCount", item.ActionCount), ("IntegrityHash", item.IntegrityHash)), cancellationToken);
    }

    private async Task<ReportResultDto> ExecuteOverridesAsync(
        AuditComplianceSystemReportDefinition definition,
        ReportFilters filters,
        ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var events = Query<ProcurementControlEvent>().Where(item =>
            item.Action.Contains("Override") || item.Action.Contains("Exception") ||
            item.EventType.Contains("Exception") || item.EventType.Contains("Exceptional") ||
            (item.ResultValuesJson != null && item.ResultValuesJson.Contains("OverrideId")));
        if (filters.StartUtc.HasValue) events = events.Where(item => item.OccurredAtUtc >= filters.StartUtc.Value);
        if (filters.EndExclusiveUtc.HasValue) events = events.Where(item => item.OccurredAtUtc < filters.EndExclusiveUtc.Value);

        var overrides = Query<InventoryNegativeStockOverride>();
        if (filters.StartUtc.HasValue) overrides = overrides.Where(item => item.ApprovedAtUtc >= filters.StartUtc.Value);
        if (filters.EndExclusiveUtc.HasValue) overrides = overrides.Where(item => item.ApprovedAtUtc < filters.EndExclusiveUtc.Value);
        if (filters.WarehouseId.HasValue) overrides = overrides.Where(item => item.WarehouseId == filters.WarehouseId.Value);
        if (filters.WarehouseId.HasValue) events = events.Where(_ => false);

        var scopes = await overrides.Select(item => new InventoryScope(item.WarehouseId, item.LocationId))
            .Distinct().ToListAsync(cancellationToken);
        var allowed = await ReadableScopesAsync(scopes, "override-exception-register", cancellationToken);
        if (filters.WarehouseId.HasValue && scopes.Count > 0 && allowed.Count == 0)
            throw new UnauthorizedAccessException("The current actor has no assigned inventory scope for this report.");
        if (allowed.Count > 0) overrides = overrides.Where(BuildOverrideScopePredicate(allowed));
        else if (scopes.Count > 0) overrides = overrides.Where(_ => false);

        var query = events.Select(item => new OverrideRow
            {
                EventKind = "Policy exception",
                Action = item.Action,
                Result = item.Result,
                RuleCode = item.RuleCode,
                SourceType = item.SourceType,
                SourceReference = item.SourceReference,
                ItemCode = null,
                WarehouseCode = null,
                LocationCode = null,
                ActorReference = item.ActorName,
                ActorUserId = item.ActorUserId,
                OccurredAt = item.OccurredAtUtc,
                ApprovedAt = null,
                ExpiresAt = null,
                ConsumedAt = null,
                EvidenceReference = item.EvidenceLinks.Select(link => link.Reference).FirstOrDefault(),
                CorrelationId = item.CorrelationId,
                IntegrityHash = item.IntegrityHash
            })
            .Concat(overrides.Select(item => new OverrideRow
            {
                EventKind = "Inventory emergency override",
                Action = item.ConsumedAtUtc.HasValue ? "Consumed" : "Approved",
                Result = ProcurementControlEventResult.Allowed,
                RuleCode = "DEC-010",
                SourceType = item.ReferenceType,
                SourceReference = item.ReferenceNumber,
                ItemCode = item.InventoryItem.ItemCode,
                WarehouseCode = item.Warehouse.Code,
                LocationCode = item.Location == null ? null : item.Location.LocationCode,
                ActorReference = null,
                ActorUserId = item.ApprovedById,
                OccurredAt = item.ApprovedAtUtc,
                ApprovedAt = item.ApprovedAtUtc,
                ExpiresAt = item.ExpiresAtUtc,
                ConsumedAt = item.ConsumedAtUtc,
                EvidenceReference = item.EvidenceReference,
                CorrelationId = string.Empty,
                IntegrityHash = item.IntegrityHash
            }))
            .OrderByDescending(item => item.OccurredAt).ThenBy(item => item.SourceReference);

        return await PageQueryAsync(query, definition, request, filters, item => Row(
            ("EventKind", item.EventKind), ("Action", item.Action), ("Result", item.Result.ToString()),
            ("RuleCode", item.RuleCode), ("SourceType", item.SourceType),
            ("SourceReference", item.SourceReference), ("ItemCode", item.ItemCode),
            ("WarehouseCode", item.WarehouseCode), ("LocationCode", item.LocationCode),
            ("ActorReference", string.IsNullOrWhiteSpace(item.ActorReference)
                ? item.ActorUserId.ToString() : item.ActorReference),
            ("OccurredAt", item.OccurredAt), ("ApprovedAt", item.ApprovedAt), ("ExpiresAt", item.ExpiresAt),
            ("ConsumedAt", item.ConsumedAt), ("EvidenceReference", item.EvidenceReference),
            ("CorrelationId", item.CorrelationId), ("IntegrityHash", item.IntegrityHash)), cancellationToken);
    }

    private async Task<ReportResultDto> ExecuteDisposalsAsync(
        AuditComplianceSystemReportDefinition definition,
        ReportFilters filters,
        ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        InventoryDisposalStatus? status = null;
        if (!string.IsNullOrWhiteSpace(filters.Status) &&
            Enum.TryParse<InventoryDisposalStatus>(filters.Status, true, out var parsed)) status = parsed;
        else if (!string.IsNullOrWhiteSpace(filters.Status))
            return PageRows([], definition, request, filters, DateTime.UtcNow);

        var source = await _disposals.GetReportSourceAsync(status, filters.WarehouseId, cancellationToken);
        var rows = source.Where(item => !filters.StartUtc.HasValue || item.RequestedAtUtc >= filters.StartUtc.Value)
            .Where(item => !filters.EndExclusiveUtc.HasValue || item.RequestedAtUtc < filters.EndExclusiveUtc.Value)
            .SelectMany(item => item.Lines.Select(line => Row(
                ("DisposalNumber", item.DisposalNumber), ("Status", item.Status.ToString()),
                ("Method", item.Method.ToString()), ("RequestedAt", item.RequestedAtUtc),
                ("WarehouseCode", item.WarehouseCode), ("WarehouseName", item.WarehouseName),
                ("ItemCode", line.ItemCode), ("ItemName", line.ItemName), ("LocationCode", line.LocationCode),
                ("Quantity", line.Quantity), ("UnitCost", line.UnitCost), ("LineValue", line.TotalValue),
                ("AuditVerifiedAt", item.AuditVerifiedAtUtc), ("CommitteeReference", item.CommitteeReference),
                ("ApprovedAt", item.ApprovedAtUtc), ("StockAdjustmentId", item.StockAdjustmentId),
                ("ProceedsAmount", item.ProceedsAmount), ("ExecutionReference", item.ExecutionReference),
                ("CompletedAt", item.CompletedAtUtc), ("EvidenceCount", item.Evidence.Count),
                ("ActionCount", item.Actions.Count))))
            .ToList();
        return PageRows(rows, definition, request, filters, DateTime.UtcNow);
    }

    private IQueryable<T> Query<T>() where T : TenantEntity
    {
        EnsureTenantContext();
        var tenantId = _currentUser.TenantId;
        return _unitOfWork.Repository<T>().GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted)
            .AsNoTracking();
    }

    private async Task<HashSet<InventoryScope>> ReadableScopesAsync(
        IReadOnlyCollection<InventoryScope> scopes,
        string sourceReference,
        CancellationToken cancellationToken)
    {
        var allowed = new HashSet<InventoryScope>();
        foreach (var scope in scopes)
        {
            try
            {
                var decision = await _access.CheckCapabilityAsync(new ProcurementAccessCapabilityRequest
                {
                    PermissionCode = "procurement.inventory.read",
                    WarehouseId = scope.WarehouseId,
                    LocationId = scope.LocationId,
                    RequireLocationScope = true,
                    SourceType = SourceType,
                    SourceReference = $"{sourceReference}:{scope.WarehouseId:N}:{scope.LocationId?.ToString("N") ?? "warehouse"}"
                }, Correlation(), cancellationToken);
                if (decision.Allowed) allowed.Add(scope);
            }
            catch (ProcurementAccessValidationException) { }
            catch (ProcurementAccessNotFoundException) { }
            catch (ProcurementAccessAuthorizationException) { }
        }
        return allowed;
    }

    private static Expression<Func<StockAdjustmentItem, bool>> BuildAdjustmentScopePredicate(
        IEnumerable<InventoryScope> scopes)
    {
        var parameter = Expression.Parameter(typeof(StockAdjustmentItem), "item");
        var adjustment = Expression.Property(parameter, nameof(StockAdjustmentItem.Adjustment));
        Expression body = Expression.Constant(false);
        foreach (var scope in scopes)
        {
            var warehouse = Expression.Equal(Expression.Property(adjustment, nameof(StockAdjustment.WarehouseId)),
                Expression.Constant(scope.WarehouseId));
            var location = Expression.Equal(Expression.Property(parameter, nameof(StockAdjustmentItem.LocationId)),
                Expression.Constant(scope.LocationId, typeof(Guid?)));
            body = Expression.OrElse(body, Expression.AndAlso(warehouse, location));
        }
        return Expression.Lambda<Func<StockAdjustmentItem, bool>>(body, parameter);
    }

    private static Expression<Func<InventoryNegativeStockOverride, bool>> BuildOverrideScopePredicate(
        IEnumerable<InventoryScope> scopes)
    {
        var parameter = Expression.Parameter(typeof(InventoryNegativeStockOverride), "item");
        Expression body = Expression.Constant(false);
        foreach (var scope in scopes)
        {
            var warehouse = Expression.Equal(
                Expression.Property(parameter, nameof(InventoryNegativeStockOverride.WarehouseId)),
                Expression.Constant(scope.WarehouseId));
            var location = Expression.Equal(
                Expression.Property(parameter, nameof(InventoryNegativeStockOverride.LocationId)),
                Expression.Constant(scope.LocationId, typeof(Guid?)));
            body = Expression.OrElse(body, Expression.AndAlso(warehouse, location));
        }
        return Expression.Lambda<Func<InventoryNegativeStockOverride, bool>>(body, parameter);
    }

    private async Task EnsureCapabilityAsync(
        string permission,
        string sourceReference,
        bool isAdministrator,
        CancellationToken cancellationToken)
    {
        if (isAdministrator) return;
        var decision = await _access.EnforceCapabilityAsync(
            Capability(permission, sourceReference), Correlation(), cancellationToken);
        if (!decision.Allowed) throw new UnauthorizedAccessException(decision.Message);
    }

    private static ProcurementAccessCapabilityRequest Capability(string permission, string sourceReference) => new()
    {
        PermissionCode = permission,
        SourceType = SourceType,
        SourceReference = sourceReference
    };

    private void EnsureTenantContext()
    {
        if (_currentUser.TenantId == Guid.Empty)
            throw new UnauthorizedAccessException("A tenant context is required to execute audit and compliance reports.");
    }

    private static AuditComplianceSystemReportDefinition Resolve(string query) =>
        AuditComplianceReportCatalogue.Resolve(query)
        ?? throw new InvalidOperationException("The audit and compliance system report is not registered.");

    private static string Correlation() => $"tdc0704-{Guid.NewGuid():N}";

    private static Dictionary<string, object> Row(params (string Key, object? Value)[] values) =>
        values.ToDictionary(item => item.Key, item => item.Value!);

    private static async Task<ReportResultDto> PageQueryAsync<T>(
        IQueryable<T> query,
        AuditComplianceSystemReportDefinition definition,
        ExecuteReportDto request,
        ReportFilters filters,
        Func<T, Dictionary<string, object>> map,
        CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var (page, pageSize) = Page(request);
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        return Result(definition, filters, DateTime.UtcNow, total, rows.Select(map).ToList(),
            page, pageSize, totalPages);
    }

    private static ReportResultDto PageRows(
        IReadOnlyList<Dictionary<string, object>> rows,
        AuditComplianceSystemReportDefinition definition,
        ExecuteReportDto request,
        ReportFilters filters,
        DateTime dataAsOf)
    {
        var (page, pageSize) = Page(request);
        var totalPages = rows.Count == 0 ? 0 : (int)Math.Ceiling(rows.Count / (double)pageSize);
        return Result(definition, filters, dataAsOf, rows.Count,
            rows.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, totalPages);
    }

    private static ReportResultDto Result(
        AuditComplianceSystemReportDefinition definition,
        ReportFilters filters,
        DateTime dataAsOf,
        int totalRows,
        List<Dictionary<string, object>> rows,
        int page,
        int pageSize,
        int totalPages) => new()
    {
        TotalRows = totalRows,
        Columns = definition.Columns.Select((item, index) => new ReportColumnDto
        {
            Name = item.Name,
            DisplayName = item.DisplayName,
            DataType = item.DataType,
            Format = item.Format,
            IsVisible = item.IsVisible,
            Order = index,
            AggregationType = item.AggregationType
        }).ToList(),
        Data = rows,
        CurrentPage = page,
        PageSize = pageSize,
        TotalPages = totalPages,
        HasNextPage = page < totalPages,
        HasPreviousPage = page > 1 && totalPages > 0,
        Metadata = new ReportMetadataDto
        {
            Parameters = filters.ToMetadata(),
            Query = definition.Query,
            DataAsOf = dataAsOf,
            DataSource = "Tenant and assigned-scope procurement, Finance, audit and inventory owners",
            Statistics = new Dictionary<string, object>
            {
                ["systemCode"] = definition.Code,
                ["page"] = page,
                ["pageSize"] = pageSize,
                ["totalRows"] = totalRows
            }
        }
    };

    private static (int Page, int PageSize) Page(ExecuteReportDto request)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 1000);
        if (request.MaxRows is > 0) pageSize = Math.Min(pageSize, Math.Clamp(request.MaxRows.Value, 1, 1000));
        return (page, pageSize);
    }

    private sealed record InventoryScope(Guid WarehouseId, Guid? LocationId);

    private sealed record ReportFilters(
        DateTime? StartUtc,
        DateTime? EndExclusiveUtc,
        string? Status,
        Guid? WarehouseId)
    {
        public static ReportFilters Parse(ExecuteReportDto request)
        {
            var start = request.StartDate ?? GetDate(request.Parameters, "startDate");
            var end = request.EndDate ?? GetDate(request.Parameters, "endDate");
            start = start.HasValue ? DateTime.SpecifyKind(start.Value.Date, DateTimeKind.Utc) : null;
            DateTime? endExclusive = end.HasValue
                ? DateTime.SpecifyKind(end.Value.Date.AddDays(1), DateTimeKind.Utc)
                : null;
            if (start.HasValue && endExclusive.HasValue && start.Value >= endExclusive.Value)
                throw new InvalidOperationException("Start date must be on or before end date.");
            return new ReportFilters(start, endExclusive, GetString(request.Parameters, "status"),
                GetGuid(request.Parameters, "warehouseId"));
        }

        public Dictionary<string, object> ToMetadata()
        {
            var values = new Dictionary<string, object>();
            if (StartUtc.HasValue) values["startDate"] = StartUtc.Value;
            if (EndExclusiveUtc.HasValue) values["endDate"] = EndExclusiveUtc.Value.AddDays(-1);
            if (!string.IsNullOrWhiteSpace(Status)) values["status"] = Status;
            if (WarehouseId.HasValue) values["warehouseId"] = WarehouseId.Value;
            return values;
        }

        private static string? GetString(Dictionary<string, object>? values, string key)
        {
            if (values is null || !values.TryGetValue(key, out var value) || value is null) return null;
            var text = value is JsonElement element ? element.ToString() : Convert.ToString(value, CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(text) || text.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? null
                : text.Trim();
        }

        private static DateTime? GetDate(Dictionary<string, object>? values, string key) =>
            DateTime.TryParse(GetString(values, key), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var value) ? value : null;

        private static Guid? GetGuid(Dictionary<string, object>? values, string key) =>
            Guid.TryParse(GetString(values, key), out var value) ? value : null;
    }

    private sealed class OpeningRow
    {
        public string SourceType { get; set; } = string.Empty;
        public string SourceReference { get; set; } = string.Empty;
        public int StatusDomain { get; set; }
        public int StatusValue { get; set; }
        public DateTime OpenedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public int EntryCount { get; set; }
        public int LateEntryCount { get; set; }
        public int ParticipantCount { get; set; }
        public bool ParticipantSignOffCaptured { get; set; }
        public string? EvidenceReference { get; set; }
        public string IntegrityHash { get; set; } = string.Empty;
    }

    private sealed class CommitteeRow
    {
        public ProcurementEvaluationSourceType SourceType { get; set; }
        public string SourceReference { get; set; } = string.Empty;
        public string CommitteeCode { get; set; } = string.Empty;
        public string CommitteeName { get; set; } = string.Empty;
        public int Version { get; set; }
        public ProcurementEvaluationPhase Phase { get; set; }
        public int MeetingSequence { get; set; }
        public ProcurementEvaluationMeetingStatus Status { get; set; }
        public DateTime ScheduledAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public int RequiredQuorum { get; set; }
        public int EligibleVoters { get; set; }
        public int SignedAttendance { get; set; }
        public bool QuorumMet { get; set; }
        public int SignedScoreSheets { get; set; }
        public string EvidenceReference { get; set; } = string.Empty;
        public string? RemoteEvidenceReference { get; set; }
        public string IntegrityHash { get; set; } = string.Empty;
    }

    private sealed class DueDiligenceRow
    {
        public string ReviewReference { get; set; } = string.Empty;
        public string SupplierCode { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public ProcurementSupplierDueDiligenceReviewType ReviewType { get; set; }
        public ProcurementSupplierDueDiligenceStatus Status { get; set; }
        public ProcurementSupplierDueDiligenceOutcome Outcome { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public DateTime? NextReviewDue { get; set; }
        public string PolicyProfile { get; set; } = string.Empty;
        public int PolicyVersion { get; set; }
        public int CheckCount { get; set; }
        public int FailedChecks { get; set; }
        public int PendingChecks { get; set; }
        public int EvidenceCount { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? RejectedAt { get; set; }
        public string IntegrityHash { get; set; } = string.Empty;
    }

    private sealed class MatchExceptionRow
    {
        public string ExceptionReference { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string PurchaseOrderNumber { get; set; } = string.Empty;
        public VendorInvoiceMatchExceptionStatus Status { get; set; }
        public string VarianceType { get; set; } = string.Empty;
        public decimal PriceTolerancePercent { get; set; }
        public decimal QuantityTolerancePercent { get; set; }
        public string RootCauseCategory { get; set; } = string.Empty;
        public VendorInvoiceMatchCorrectiveActionStatus CorrectiveActionStatus { get; set; }
        public DateTime CorrectiveActionDue { get; set; }
        public string RequestedBy { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public string? DecisionBy { get; set; }
        public DateTime? DecisionAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int EvidenceCount { get; set; }
        public Guid? ApprovalControlEventId { get; set; }
        public string IntegrityHash { get; set; } = string.Empty;
    }

    private sealed class PaymentRow
    {
        public string PaymentNumber { get; set; } = string.Empty;
        public DateTime PaymentDate { get; set; }
        public VendorPaymentStatus Status { get; set; }
        public string SupplierCode { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public string PurchaseOrderNumber { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public decimal PaymentAmount { get; set; }
        public decimal AllocatedAmount { get; set; }
        public VendorPaymentMethod PaymentMethod { get; set; }
        public bool IsReversal { get; set; }
        public DateTime? ReadinessEvaluatedAt { get; set; }
        public Guid? ReadinessControlEventId { get; set; }
        public Guid? SodControlEventId { get; set; }
        public DateTime? AuthorizedAt { get; set; }
        public DateTime? ClearedAt { get; set; }
        public Guid? JournalEntryId { get; set; }
        public string? TransactionReference { get; set; }
    }

    private sealed class AdjustmentRow
    {
        public string AdjustmentNumber { get; set; } = string.Empty;
        public DateTime AdjustmentDate { get; set; }
        public string WarehouseCode { get; set; } = string.Empty;
        public string WarehouseName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ReasonCode { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string ItemName { get; set; } = string.Empty;
        public string? LocationCode { get; set; }
        public decimal SystemQuantity { get; set; }
        public decimal PhysicalQuantity { get; set; }
        public decimal AdjustmentQuantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal AdjustmentValue { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? PostedAt { get; set; }
        public DateTime? ReversedAt { get; set; }
        public Guid? FinanceJournalEntryId { get; set; }
        public int EvidenceCount { get; set; }
        public int ActionCount { get; set; }
        public string? IntegrityHash { get; set; }
    }

    private sealed class OverrideRow
    {
        public string EventKind { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public ProcurementControlEventResult Result { get; set; }
        public string? RuleCode { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public string SourceReference { get; set; } = string.Empty;
        public string? ItemCode { get; set; }
        public string? WarehouseCode { get; set; }
        public string? LocationCode { get; set; }
        public string? ActorReference { get; set; }
        public Guid ActorUserId { get; set; }
        public DateTime OccurredAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? ConsumedAt { get; set; }
        public string? EvidenceReference { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
        public string IntegrityHash { get; set; } = string.Empty;
    }
}
