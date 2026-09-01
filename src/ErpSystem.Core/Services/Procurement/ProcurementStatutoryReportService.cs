using System.Globalization;
using System.Text.Json;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Reports;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Procurement;

public sealed class ProcurementStatutoryReportService : IProcurementStatutoryReportService
{
    private const string SourceType = "ProcurementStatutoryReport";
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProcurementAccessControlService _accessControl;
    private readonly ICurrentUserProvider _currentUser;

    public ProcurementStatutoryReportService(
        IUnitOfWork unitOfWork,
        IProcurementAccessControlService accessControl,
        ICurrentUserProvider currentUser)
    {
        _unitOfWork = unitOfWork;
        _accessControl = accessControl;
        _currentUser = currentUser;
    }

    public bool CanHandle(string? reportQuery) => ProcurementStatutoryReportCatalogue.Resolve(reportQuery) is not null;

    public bool OwnsIdentifier(string? reportQuery) =>
        !string.IsNullOrWhiteSpace(reportQuery) &&
        reportQuery.StartsWith(ProcurementStatutoryReportCatalogue.QueryPrefix, StringComparison.OrdinalIgnoreCase);

    public string? ResolveCode(string? reportQuery) =>
        ProcurementStatutoryReportCatalogue.Resolve(reportQuery)?.Code;

    public async Task<bool> CanReadAsync(bool isAdministrator, CancellationToken cancellationToken = default)
    {
        if (isAdministrator) return true;
        try
        {
            var decision = await _accessControl.CheckCapabilityAsync(
                Capability(ProcurementStatutoryReportCatalogue.ReadPermission, "catalogue"),
                NewCorrelation(), cancellationToken);
            return decision.Allowed;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public async Task AuthorizeExportAsync(
        string reportQuery,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        var definition = ProcurementStatutoryReportCatalogue.Resolve(reportQuery)
            ?? throw new InvalidOperationException("The procurement system report is not registered.");
        await EnsureCapabilityAsync(
            ProcurementStatutoryReportCatalogue.ExportPermission,
            definition.Code,
            isAdministrator,
            cancellationToken);
    }

    public async Task<ReportResultDto> ExecuteAsync(
        string reportQuery,
        ExecuteReportDto request,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        var definition = ProcurementStatutoryReportCatalogue.Resolve(reportQuery)
            ?? throw new InvalidOperationException("The procurement system report is not registered.");
        await EnsureCapabilityAsync(
            ProcurementStatutoryReportCatalogue.ReadPermission,
            definition.Code,
            isAdministrator,
            cancellationToken);
        var filters = ReportFilters.Parse(request);

        return definition.Code switch
        {
            ProcurementStatutoryReportCatalogue.AppVsActualCode =>
                await ExecuteAppVsActualAsync(definition, filters, request, cancellationToken),
            ProcurementStatutoryReportCatalogue.TenderRegisterCode =>
                await ExecuteTenderRegisterAsync(definition, filters, request, cancellationToken),
            ProcurementStatutoryReportCatalogue.ContractRegisterCode =>
                await ExecuteContractRegisterAsync(definition, filters, request, cancellationToken),
            ProcurementStatutoryReportCatalogue.SupplierPerformanceCode =>
                await ExecuteSupplierPerformanceAsync(definition, filters, request, cancellationToken),
            ProcurementStatutoryReportCatalogue.AwardNotificationCode =>
                await ExecuteAwardNotificationAsync(definition, filters, request, cancellationToken),
            ProcurementStatutoryReportCatalogue.SavingsCode =>
                await ExecuteSavingsAsync(definition, filters, request, cancellationToken),
            ProcurementStatutoryReportCatalogue.EtcMinutesCode =>
                await ExecuteEtcMinutesAsync(definition, filters, request, cancellationToken),
            ProcurementStatutoryReportCatalogue.RequisitionStatusCode =>
                await ExecuteRequisitionStatusAsync(definition, filters, request, cancellationToken),
            ProcurementStatutoryReportCatalogue.PurchaseOrderRegisterCode =>
                await ExecutePurchaseOrderRegisterAsync(definition, filters, request, cancellationToken),
            ProcurementStatutoryReportCatalogue.CommitmentRegisterCode =>
                await ExecuteCommitmentRegisterAsync(definition, filters, request, cancellationToken),
            ProcurementStatutoryReportCatalogue.CertificateTrackingCode =>
                await ExecuteCertificateTrackingAsync(definition, filters, request, cancellationToken),
            ProcurementStatutoryReportCatalogue.ExceptionRegisterCode =>
                await ExecuteExceptionRegisterAsync(definition, filters, request, cancellationToken),
            ProcurementStatutoryReportCatalogue.ProcurementToPaymentCode =>
                await ExecuteProcurementToPaymentAsync(definition, filters, request, cancellationToken),
            _ => throw new InvalidOperationException("The procurement system report is not implemented.")
        };
    }

    private async Task<ReportResultDto> ExecuteAppVsActualAsync(
        ProcurementSystemReportDefinition definition,
        ReportFilters filters,
        ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var plans = Query<ProcurementPlan>();
        var items = Query<ProcurementPlanItem>();
        var purchaseOrders = Query<PurchaseOrder>();
        var awards = Query<TenderAward>();
        var departments = Query<Department>();
        var submissions = Query<ProcurementAppSubmission>();
        var query =
            from plan in plans
            join department in departments on plan.DepartmentId equals department.Id into departmentJoin
            from department in departmentJoin.DefaultIfEmpty()
            select new AppActualRow
            {
                PlanNumber = plan.PlanNumber,
                FiscalYear = plan.FiscalYear,
                Department = department == null ? "Unassigned" : department.Name,
                Status = plan.Status,
                Currency = plan.Currency,
                PlanStartDate = plan.PlanStartDate,
                PlanEndDate = plan.PlanEndDate,
                PlannedItemCount = items.Count(item => item.ProcurementPlanId == plan.Id),
                ProcuredItemCount = items.Count(item => item.ProcurementPlanId == plan.Id &&
                    (item.PurchaseOrderId.HasValue || item.TenderId.HasValue)),
                ApprovedBudget = plan.ApprovedBudget,
                PlannedValue = items.Where(item => item.ProcurementPlanId == plan.Id)
                    .Sum(item => (decimal?)item.EstimatedTotalCost) ?? 0m,
                PurchaseOrderValue = purchaseOrders.Where(order =>
                        items.Where(item => item.ProcurementPlanId == plan.Id && item.PurchaseOrderId.HasValue)
                            .Select(item => item.PurchaseOrderId!.Value).Contains(order.Id))
                    .Sum(order => (decimal?)order.TotalAmount) ?? 0m,
                UnconvertedAwardValue = awards.Where(award => !award.PurchaseOrderId.HasValue &&
                        items.Where(item => item.ProcurementPlanId == plan.Id && item.TenderId.HasValue)
                            .Select(item => item.TenderId!.Value).Contains(award.TenderId) &&
                        award.Status != "Cancelled")
                    .Sum(award => (decimal?)award.AwardedAmount) ?? 0m,
                SubmissionStatus = submissions.Where(item => item.ProcurementPlanId == plan.Id)
                    .OrderByDescending(item => item.AttemptNumber)
                    .Select(item => (ProcurementAppSubmissionStatus?)item.Status).FirstOrDefault(),
                ExternalReference = submissions.Where(item => item.ProcurementPlanId == plan.Id)
                    .OrderByDescending(item => item.AttemptNumber)
                    .Select(item => item.ExternalSubmissionReference).FirstOrDefault()
            };
        if (filters.FiscalYear.HasValue) query = query.Where(item => item.FiscalYear == filters.FiscalYear.Value);
        if (!string.IsNullOrWhiteSpace(filters.Status)) query = query.Where(item => item.Status == filters.Status);
        if (filters.StartUtc.HasValue) query = query.Where(item => item.PlanEndDate >= filters.StartUtc.Value);
        if (filters.EndExclusiveUtc.HasValue) query = query.Where(item => item.PlanStartDate < filters.EndExclusiveUtc.Value);
        query = query.OrderByDescending(item => item.FiscalYear).ThenBy(item => item.PlanNumber);
        return await PageAsync(query, definition, request, filters, item => Row(
            ("PlanNumber", item.PlanNumber), ("FiscalYear", item.FiscalYear), ("Department", item.Department),
            ("Status", item.Status), ("Currency", item.Currency), ("PlannedItemCount", item.PlannedItemCount),
            ("ProcuredItemCount", item.ProcuredItemCount), ("ApprovedBudget", item.ApprovedBudget),
            ("PlannedValue", item.PlannedValue), ("ActualValue", item.ActualValue),
            ("Variance", item.PlannedValue - item.ActualValue),
            ("SubmissionStatus", item.SubmissionStatus?.ToString()), ("ExternalReference", item.ExternalReference)), cancellationToken);
    }

    private async Task<ReportResultDto> ExecuteTenderRegisterAsync(
        ProcurementSystemReportDefinition definition, ReportFilters filters, ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var tenders = Query<Tender>();
        var bids = Query<TenderBid>();
        var awards = Query<TenderAward>();
        var contracts = Query<Contract>();
        var query = tenders.Select(tender => new TenderRegisterRow
        {
            TenderNumber = tender.TenderNumber,
            Title = tender.Title,
            TenderType = tender.TenderType,
            Status = tender.Status,
            EffectiveDate = tender.PublishDate ?? tender.CreatedAt,
            PublishDate = tender.PublishDate,
            SubmissionDeadline = tender.SubmissionDeadline,
            OpeningDate = tender.OpeningDate,
            AwardDate = tender.AwardDate,
            Currency = tender.Currency ?? string.Empty,
            EstimatedValue = tender.EstimatedValue,
            BidCount = bids.Count(item => item.TenderId == tender.Id),
            AwardCount = awards.Count(item => item.TenderId == tender.Id && item.Status != "Cancelled"),
            AwardedValue = awards.Where(item => item.TenderId == tender.Id && item.Status != "Cancelled")
                .Sum(item => (decimal?)item.AwardedAmount) ?? 0m,
            ContractCount = contracts.Count(item => item.TenderId == tender.Id)
        });
        query = ApplyDate(query, filters, item => item.EffectiveDate);
        if (!string.IsNullOrWhiteSpace(filters.Status)) query = query.Where(item => item.Status == filters.Status);
        query = query.OrderByDescending(item => item.EffectiveDate).ThenBy(item => item.TenderNumber);
        return await PageAsync(query, definition, request, filters, item => Row(
            ("TenderNumber", item.TenderNumber), ("Title", item.Title), ("TenderType", item.TenderType),
            ("Status", item.Status), ("PublishDate", item.PublishDate), ("SubmissionDeadline", item.SubmissionDeadline),
            ("OpeningDate", item.OpeningDate), ("AwardDate", item.AwardDate), ("Currency", item.Currency),
            ("EstimatedValue", item.EstimatedValue), ("BidCount", item.BidCount), ("AwardCount", item.AwardCount),
            ("AwardedValue", item.AwardedValue), ("ContractCount", item.ContractCount)), cancellationToken);
    }

    private async Task<ReportResultDto> ExecuteContractRegisterAsync(
        ProcurementSystemReportDefinition definition, ReportFilters filters, ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var contracts = Query<Contract>();
        var partners = Query<BusinessPartner>();
        var tenders = Query<Tender>();
        var amendments = Query<ContractAmendment>();
        var milestones = Query<ContractMilestone>();
        var purchaseOrders = Query<PurchaseOrder>();
        var invoices = Query<VendorInvoice>();
        var certificates = Query<ProjectPaymentCertificate>();
        var requisitions = Query<PurchaseRequisition>();
        var projects = Query<Project>();
        var query =
            from contract in contracts
            join partner in partners on contract.BusinessPartnerId equals partner.Id
            join tender in tenders on contract.TenderId equals tender.Id
            select new ContractRegisterRow
            {
                BusinessPartnerId = contract.BusinessPartnerId,
                ContractNumber = contract.ContractNumber,
                ContractTitle = contract.ContractTitle,
                ContractType = contract.ContractType,
                Status = contract.Status,
                SupplierCode = partner.PartnerCode,
                SupplierName = partner.PartnerName,
                TenderNumber = tender.TenderNumber,
                ProjectCode = projects.Where(project => requisitions.Any(requisition =>
                        requisition.Id == tender.SourcePurchaseRequisitionId && requisition.ProjectId == project.Id))
                    .Select(project => project.ProjectCode).FirstOrDefault(),
                ProjectName = projects.Where(project => requisitions.Any(requisition =>
                        requisition.Id == tender.SourcePurchaseRequisitionId && requisition.ProjectId == project.Id))
                    .Select(project => project.Title).FirstOrDefault(),
                Currency = contract.Currency,
                ContractValue = contract.ContractValue,
                ApprovalDate = contract.ActivatedAt ?? contract.SignedDate,
                RetentionPercentage = contract.RetentionPercentage,
                ApprovedVariationCount = amendments.Count(item => item.ContractId == contract.Id && item.Status == "Approved"),
                ApprovedAmendmentValue = amendments.Where(item => item.ContractId == contract.Id && item.Status == "Approved")
                    .Sum(item => (decimal?)item.ValueChange) ?? 0m,
                CertificateCount = certificates.Count(item => item.ContractId == contract.Id &&
                    item.Status != ProjectPaymentCertificateStatuses.Draft &&
                    item.Status != ProjectPaymentCertificateStatuses.Cancelled),
                CertifiedAmount = certificates.Where(item => item.ContractId == contract.Id &&
                        item.Status != ProjectPaymentCertificateStatuses.Draft &&
                        item.Status != ProjectPaymentCertificateStatuses.Cancelled)
                    .Sum(item => (decimal?)item.NetCertifiedAmount) ?? 0m,
                InvoiceCount = invoices.Count(invoice => invoice.PurchaseOrderId.HasValue &&
                    purchaseOrders.Any(order => order.Id == invoice.PurchaseOrderId.Value && order.ContractId == contract.Id) &&
                    invoice.Status != VendorInvoiceStatus.Voided && invoice.Status != VendorInvoiceStatus.Rejected),
                InvoicedAmount = invoices.Where(invoice => invoice.PurchaseOrderId.HasValue &&
                        purchaseOrders.Any(order => order.Id == invoice.PurchaseOrderId.Value && order.ContractId == contract.Id) &&
                        invoice.Status != VendorInvoiceStatus.Voided && invoice.Status != VendorInvoiceStatus.Rejected)
                    .Sum(invoice => (decimal?)invoice.TotalAmount) ?? 0m,
                PaidAmount = invoices.Where(invoice => invoice.PurchaseOrderId.HasValue &&
                        purchaseOrders.Any(order => order.Id == invoice.PurchaseOrderId.Value && order.ContractId == contract.Id) &&
                        invoice.Status != VendorInvoiceStatus.Voided && invoice.Status != VendorInvoiceStatus.Rejected)
                    .Sum(invoice => (decimal?)invoice.PaidAmount) ?? 0m,
                EffectiveDate = contract.StartDate ?? contract.CreatedAt,
                StartDate = contract.StartDate,
                EndDate = contract.EndDate,
                SignedDate = contract.SignedDate,
                CompletedMilestones = milestones.Count(item => item.ContractId == contract.Id &&
                    (item.Status == "Completed" || item.Status == "Invoiced" || item.Status == "Paid")),
                TotalMilestones = milestones.Count(item => item.ContractId == contract.Id)
            };
        query = ApplyDate(query, filters, item => item.EffectiveDate);
        if (!string.IsNullOrWhiteSpace(filters.Status)) query = query.Where(item => item.Status == filters.Status);
        if (filters.SupplierId.HasValue) query = query.Where(item => item.BusinessPartnerId == filters.SupplierId.Value);
        query = query.OrderByDescending(item => item.EffectiveDate).ThenBy(item => item.ContractNumber);
        return await PageAsync(query, definition, request, filters, item => Row(
            ("ContractNumber", item.ContractNumber), ("ContractTitle", item.ContractTitle), ("ContractType", item.ContractType),
            ("Status", item.Status), ("SupplierCode", item.SupplierCode), ("SupplierName", item.SupplierName),
            ("TenderNumber", item.TenderNumber), ("ProjectCode", item.ProjectCode), ("ProjectName", item.ProjectName),
            ("Currency", item.Currency), ("ContractValue", item.ContractValue), ("ApprovalDate", item.ApprovalDate),
            ("RetentionPercentage", item.RetentionPercentage), ("ApprovedVariationCount", item.ApprovedVariationCount),
            ("ApprovedAmendmentValue", item.ApprovedAmendmentValue), ("StartDate", item.StartDate),
            ("CertificateCount", item.CertificateCount), ("CertifiedAmount", item.CertifiedAmount),
            ("InvoiceCount", item.InvoiceCount), ("InvoicedAmount", item.InvoicedAmount),
            ("PaidAmount", item.PaidAmount), ("Balance", item.Balance),
            ("EndDate", item.EndDate), ("SignedDate", item.SignedDate),
            ("CompletedMilestones", item.CompletedMilestones), ("TotalMilestones", item.TotalMilestones)), cancellationToken);
    }

    private async Task<ReportResultDto> ExecuteSupplierPerformanceAsync(
        ProcurementSystemReportDefinition definition, ReportFilters filters, ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var scorecards = Query<ProcurementSupplierPerformanceScorecard>();
        var partners = Query<BusinessPartner>();
        var query =
            from scorecard in scorecards
            join partner in partners on scorecard.BusinessPartnerId equals partner.Id
            select new SupplierPerformanceRow
            {
                BusinessPartnerId = scorecard.BusinessPartnerId,
                ScorecardReference = scorecard.ScorecardReference,
                SupplierCode = partner.PartnerCode,
                SupplierName = partner.PartnerName,
                PeriodStart = scorecard.PeriodStartUtc,
                PeriodEnd = scorecard.PeriodEndUtc,
                CalculatedAt = scorecard.CalculatedAtUtc,
                DataStatus = scorecard.DataStatus,
                CoveragePercent = scorecard.DataCoveragePercent,
                OverallScore = scorecard.OverallScore,
                PerformanceBand = scorecard.PerformanceBand,
                MinimumScore = scorecard.MinimumScore,
                MinimumScoreBreached = scorecard.MinimumScoreBreached,
                PurchaseOrders = scorecard.PurchaseOrderCount,
                Receipts = scorecard.ReceiptCount,
                Contracts = scorecard.ContractCount,
                NextReviewDue = scorecard.NextReviewDueAtUtc
            };
        query = ApplyDate(query, filters, item => item.CalculatedAt);
        if (filters.SupplierId.HasValue) query = query.Where(item => item.BusinessPartnerId == filters.SupplierId.Value);
        if (!string.IsNullOrWhiteSpace(filters.Status) &&
            Enum.TryParse<ProcurementSupplierPerformanceDataStatus>(filters.Status, true, out var dataStatus))
            query = query.Where(item => item.DataStatus == dataStatus);
        query = query.OrderByDescending(item => item.CalculatedAt).ThenBy(item => item.SupplierCode);
        return await PageAsync(query, definition, request, filters, item => Row(
            ("ScorecardReference", item.ScorecardReference), ("SupplierCode", item.SupplierCode),
            ("SupplierName", item.SupplierName), ("PeriodStart", item.PeriodStart), ("PeriodEnd", item.PeriodEnd),
            ("CalculatedAt", item.CalculatedAt), ("DataStatus", item.DataStatus.ToString()),
            ("CoveragePercent", item.CoveragePercent), ("OverallScore", item.OverallScore),
            ("PerformanceBand", item.PerformanceBand), ("MinimumScore", item.MinimumScore),
            ("MinimumScoreBreached", item.MinimumScoreBreached), ("PurchaseOrders", item.PurchaseOrders),
            ("Receipts", item.Receipts), ("Contracts", item.Contracts), ("NextReviewDue", item.NextReviewDue)), cancellationToken);
    }

    private async Task<ReportResultDto> ExecuteAwardNotificationAsync(
        ProcurementSystemReportDefinition definition, ReportFilters filters, ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var registers = Query<ProcurementBidderCommunicationRegister>();
        var recipients = Query<ProcurementBidderCommunicationRecipient>();
        var query =
            from recipient in recipients
            join register in registers on recipient.RegisterId equals register.Id
            select new AwardNotificationRow
            {
                BusinessPartnerId = recipient.BusinessPartnerId,
                AwardReference = register.AwardReference,
                SourceReference = register.SourceReference,
                AwardFamily = register.AwardFamily,
                AwardedAt = register.AwardedAtUtc,
                SupplierCode = recipient.PartnerCode,
                SupplierName = recipient.PartnerName,
                Outcome = recipient.Outcome,
                ApprovedLetterVersion = recipient.LetterVersions.Select(item => (int?)item.Version).Max(),
                DispatchCount = recipient.LetterVersions.SelectMany(item => item.Dispatches).Count(),
                LatestDispatchAt = recipient.LetterVersions.SelectMany(item => item.Dispatches)
                    .OrderByDescending(item => item.DispatchedAtUtc).Select(item => (DateTime?)item.DispatchedAtUtc).FirstOrDefault(),
                LatestDeliveryOutcome = recipient.LetterVersions.SelectMany(item => item.Dispatches)
                    .SelectMany(item => item.Deliveries).OrderByDescending(item => item.OccurredAtUtc)
                    .Select(item => (ProcurementBidderCommunicationDeliveryOutcome?)item.Outcome).FirstOrDefault(),
                Acknowledged = recipient.LetterVersions.SelectMany(item => item.Dispatches)
                    .SelectMany(item => item.Acknowledgements).Any(),
                AppealCount = recipient.Appeals.Count,
                StandstillEndsAt = register.StandstillEndsAtUtc,
                AppealWindowEndsAt = register.AppealWindowEndsAtUtc
            };
        query = ApplyDate(query, filters, item => item.AwardedAt);
        if (filters.SupplierId.HasValue) query = query.Where(item => item.BusinessPartnerId == filters.SupplierId.Value);
        if (!string.IsNullOrWhiteSpace(filters.Status) &&
            Enum.TryParse<ProcurementBidderCommunicationRecipientOutcome>(filters.Status, true, out var outcome))
            query = query.Where(item => item.Outcome == outcome);
        query = query.OrderByDescending(item => item.AwardedAt).ThenBy(item => item.AwardReference).ThenBy(item => item.SupplierCode);
        return await PageAsync(query, definition, request, filters, item => Row(
            ("AwardReference", item.AwardReference), ("SourceReference", item.SourceReference),
            ("AwardFamily", item.AwardFamily.ToString()), ("AwardedAt", item.AwardedAt),
            ("SupplierCode", item.SupplierCode), ("SupplierName", item.SupplierName), ("Outcome", item.Outcome.ToString()),
            ("ApprovedLetterVersion", item.ApprovedLetterVersion), ("DispatchCount", item.DispatchCount),
            ("LatestDispatchAt", item.LatestDispatchAt), ("LatestDeliveryOutcome", item.LatestDeliveryOutcome?.ToString()),
            ("Acknowledged", item.Acknowledged), ("AppealCount", item.AppealCount),
            ("StandstillEndsAt", item.StandstillEndsAt), ("AppealWindowEndsAt", item.AppealWindowEndsAt)), cancellationToken);
    }

    private async Task<ReportResultDto> ExecuteSavingsAsync(
        ProcurementSystemReportDefinition definition, ReportFilters filters, ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var tenders = Query<Tender>();
        var awards = Query<TenderAward>();
        var query = tenders.Where(tender => awards.Any(award => award.TenderId == tender.Id && award.Status != "Cancelled"))
            .Select(tender => new SavingsRow
            {
                TenderNumber = tender.TenderNumber,
                Title = tender.Title,
                TenderType = tender.TenderType,
                Status = tender.Status,
                Currency = tender.Currency ?? string.Empty,
                EstimatedValue = tender.EstimatedValue ?? 0m,
                AwardedValue = awards.Where(award => award.TenderId == tender.Id && award.Status != "Cancelled")
                    .Sum(award => (decimal?)award.AwardedAmount) ?? 0m,
                AwardCount = awards.Count(award => award.TenderId == tender.Id && award.Status != "Cancelled"),
                SupplierCount = awards.Where(award => award.TenderId == tender.Id && award.Status != "Cancelled")
                    .Select(award => award.BusinessPartnerId).Distinct().Count(),
                AwardDate = awards.Where(award => award.TenderId == tender.Id && award.Status != "Cancelled")
                    .Max(award => (DateTime?)award.AwardDate) ?? tender.AwardDate ?? tender.CreatedAt
            });
        query = ApplyDate(query, filters, item => item.AwardDate);
        if (!string.IsNullOrWhiteSpace(filters.Status)) query = query.Where(item => item.Status == filters.Status);
        query = query.OrderByDescending(item => item.AwardDate).ThenBy(item => item.TenderNumber);
        return await PageAsync(query, definition, request, filters, item =>
        {
            var savings = item.EstimatedValue - item.AwardedValue;
            var percent = item.EstimatedValue == 0m ? 0m : Math.Round(savings / item.EstimatedValue * 100m, 2);
            return Row(("TenderNumber", item.TenderNumber), ("Title", item.Title), ("TenderType", item.TenderType),
                ("Status", item.Status), ("Currency", item.Currency), ("EstimatedValue", item.EstimatedValue),
                ("AwardedValue", item.AwardedValue), ("SavingsValue", savings), ("SavingsPercent", percent),
                ("AwardCount", item.AwardCount), ("SupplierCount", item.SupplierCount), ("AwardDate", item.AwardDate));
        }, cancellationToken);
    }

    private async Task<ReportResultDto> ExecuteEtcMinutesAsync(
        ProcurementSystemReportDefinition definition, ReportFilters filters, ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var meetings = Query<ProcurementEvaluationMeeting>();
        var controls = Query<ProcurementEvaluationCommitteeControl>();
        var query =
            from meeting in meetings
            join control in controls on meeting.CommitteeControlId equals control.Id
            select new EtcMinutesRow
            {
                SourceReference = control.SourceReference,
                CommitteeCode = control.CommitteeCode,
                CommitteeName = control.CommitteeName,
                MeetingSequence = meeting.Sequence,
                Phase = meeting.Phase,
                Status = meeting.Status,
                MeetingMode = meeting.MeetingMode,
                MeetingChannel = meeting.MeetingChannel,
                ScheduledAt = meeting.ScheduledAtUtc,
                StartedAt = meeting.StartedAtUtc,
                ClosedAt = meeting.ClosedAtUtc,
                EligibleVoters = meeting.EligibleVotingMemberCount,
                SignedAttendance = meeting.SignedVotingAttendanceCount,
                QuorumMet = meeting.QuorumMet,
                ChairPresent = meeting.ChairPresent,
                SecretaryPresent = meeting.SecretaryPresent,
                EvidenceReference = meeting.EvidenceReference
            };
        query = ApplyDate(query, filters, item => item.ScheduledAt);
        if (!string.IsNullOrWhiteSpace(filters.Status) &&
            Enum.TryParse<ProcurementEvaluationMeetingStatus>(filters.Status, true, out var status))
            query = query.Where(item => item.Status == status);
        query = query.OrderByDescending(item => item.ScheduledAt).ThenBy(item => item.SourceReference).ThenBy(item => item.MeetingSequence);
        return await PageAsync(query, definition, request, filters, item => Row(
            ("SourceReference", item.SourceReference), ("CommitteeCode", item.CommitteeCode),
            ("CommitteeName", item.CommitteeName), ("MeetingSequence", item.MeetingSequence),
            ("Phase", item.Phase.ToString()), ("Status", item.Status.ToString()), ("MeetingMode", item.MeetingMode),
            ("MeetingChannel", item.MeetingChannel), ("ScheduledAt", item.ScheduledAt), ("StartedAt", item.StartedAt),
            ("ClosedAt", item.ClosedAt), ("EligibleVoters", item.EligibleVoters),
            ("SignedAttendance", item.SignedAttendance), ("QuorumMet", item.QuorumMet),
            ("ChairPresent", item.ChairPresent), ("SecretaryPresent", item.SecretaryPresent),
            ("EvidenceReference", item.EvidenceReference)), cancellationToken);
    }

    private async Task<ReportResultDto> ExecuteRequisitionStatusAsync(
        ProcurementSystemReportDefinition definition, ReportFilters filters, ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var tenantId = RequiredTenantId();
        var requisitions = Query<PurchaseRequisition>();
        var items = Query<PurchaseRequisitionItem>();
        var purchaseOrders = Query<PurchaseOrder>();
        var query = requisitions.Select(requisition => new RequisitionStatusRow
        {
            RequisitionNumber = requisition.RequisitionNumber,
            RequisitionDate = requisition.RequisitionDate,
            RequiredDate = requisition.RequiredDate,
            RequestedBy = requisition.RequestedBy != null && requisition.RequestedBy.TenantId == tenantId
                ? requisition.RequestedBy.FirstName + " " + requisition.RequestedBy.LastName
                : "Unavailable",
            Department = requisition.Department,
            CostCenter = requisition.CostCenter,
            Category = requisition.ProcurementCategory,
            Priority = requisition.Priority,
            Status = requisition.Status,
            Currency = requisition.Currency,
            TotalAmount = requisition.TotalAmount,
            LineCount = items.Count(item => item.RequisitionId == requisition.Id),
            OrderedLineCount = items.Count(item => item.RequisitionId == requisition.Id && item.PurchaseOrderId.HasValue),
            PurchaseOrderCount = purchaseOrders.Count(item => item.SourceRequisitionId == requisition.Id),
            ApprovalLevel = requisition.ApprovalLevel,
            RequiredApprovalLevel = requisition.RequiredApprovalLevel,
            ApprovedAt = requisition.ApprovedAt,
            SourcePlanNumber = requisition.SourcePlanNumber,
            ProjectCode = requisition.ProjectCode,
            BudgetCode = requisition.BudgetCode,
            ExceptionRuleCode = requisition.ApprovedExceptionRuleCode
        });
        query = ApplyDate(query, filters, item => item.RequisitionDate);
        if (filters.FiscalYear.HasValue)
            query = query.Where(item => item.RequisitionDate.Year == filters.FiscalYear.Value);
        if (!string.IsNullOrWhiteSpace(filters.Status))
            query = query.Where(item => item.Status == filters.Status);
        query = query.OrderByDescending(item => item.RequisitionDate).ThenBy(item => item.RequisitionNumber);
        return await PageAsync(query, definition, request, filters, item => Row(
            ("RequisitionNumber", item.RequisitionNumber), ("RequisitionDate", item.RequisitionDate),
            ("RequiredDate", item.RequiredDate), ("RequestedBy", item.RequestedBy),
            ("Department", item.Department), ("CostCenter", item.CostCenter),
            ("Category", item.Category?.ToString()), ("Priority", item.Priority), ("Status", item.Status),
            ("Currency", item.Currency), ("TotalAmount", item.TotalAmount), ("LineCount", item.LineCount),
            ("OrderedLineCount", item.OrderedLineCount), ("PurchaseOrderCount", item.PurchaseOrderCount),
            ("ApprovalLevel", item.ApprovalLevel), ("RequiredApprovalLevel", item.RequiredApprovalLevel),
            ("ApprovedAt", item.ApprovedAt), ("SourcePlanNumber", item.SourcePlanNumber),
            ("ProjectCode", item.ProjectCode), ("BudgetCode", item.BudgetCode),
            ("ExceptionRuleCode", item.ExceptionRuleCode)), cancellationToken);
    }

    private async Task<ReportResultDto> ExecutePurchaseOrderRegisterAsync(
        ProcurementSystemReportDefinition definition, ReportFilters filters, ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var purchaseOrders = Query<PurchaseOrder>();
        var partners = Query<BusinessPartner>();
        var items = Query<PurchaseOrderItem>();
        var receipts = Query<PurchaseOrderReceipt>();
        var query =
            from order in purchaseOrders
            join partner in partners on order.BusinessPartnerId equals partner.Id
            select new PurchaseOrderRegisterRow
            {
                BusinessPartnerId = order.BusinessPartnerId,
                OrderNumber = order.OrderNumber,
                OrderDate = order.OrderDate,
                SupplierCode = partner.PartnerCode,
                SupplierName = partner.PartnerName,
                Status = order.Status,
                Category = order.ProcurementCategory,
                OrderType = order.OrderType,
                Currency = order.Currency,
                TotalAmount = order.TotalAmount,
                SourceRequisitionNumber = order.SourceRequisitionNumber,
                SourceType = order.ProcurementSourceType,
                SourceReference = order.ProcurementSourceReference,
                TenderNumber = order.TenderNumber,
                ContractNumber = order.ContractNumber,
                RevisionNumber = order.RevisionNumber,
                RequiredDate = order.RequiredDate,
                PromisedDate = order.PromisedDate,
                ReceivedDate = order.ReceivedDate,
                ApprovedAt = order.ApprovedAt,
                LineCount = items.Count(item => item.PurchaseOrderId == order.Id),
                ReceiptCount = receipts.Count(item => item.PurchaseOrderId == order.Id),
                LatestReceiptAt = receipts.Where(item => item.PurchaseOrderId == order.Id)
                    .Max(item => (DateTime?)item.ReceiptDate)
            };
        query = ApplyDate(query, filters, item => item.OrderDate);
        if (!string.IsNullOrWhiteSpace(filters.Status)) query = query.Where(item => item.Status == filters.Status);
        if (filters.SupplierId.HasValue) query = query.Where(item => item.BusinessPartnerId == filters.SupplierId.Value);
        query = query.OrderByDescending(item => item.OrderDate).ThenBy(item => item.OrderNumber);
        return await PageAsync(query, definition, request, filters, item => Row(
            ("OrderNumber", item.OrderNumber), ("OrderDate", item.OrderDate),
            ("SupplierCode", item.SupplierCode), ("SupplierName", item.SupplierName), ("Status", item.Status),
            ("Category", item.Category?.ToString()), ("OrderType", item.OrderType), ("Currency", item.Currency),
            ("TotalAmount", item.TotalAmount), ("SourceRequisitionNumber", item.SourceRequisitionNumber),
            ("SourceType", item.SourceType?.ToString()), ("SourceReference", item.SourceReference),
            ("TenderNumber", item.TenderNumber), ("ContractNumber", item.ContractNumber),
            ("RevisionNumber", item.RevisionNumber), ("RequiredDate", item.RequiredDate),
            ("PromisedDate", item.PromisedDate), ("ReceivedDate", item.ReceivedDate),
            ("ApprovedAt", item.ApprovedAt), ("LineCount", item.LineCount),
            ("ReceiptCount", item.ReceiptCount), ("LatestReceiptAt", item.LatestReceiptAt)), cancellationToken);
    }

    private async Task<ReportResultDto> ExecuteCommitmentRegisterAsync(
        ProcurementSystemReportDefinition definition, ReportFilters filters, ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var commitments = Query<ProcurementBudgetCommitment>();
        var requisitions = Query<PurchaseRequisition>();
        var budgets = Query<ProcurementBudget>();
        var adjustments = Query<ProcurementPurchaseOrderCommitmentAdjustment>();
        var query =
            from commitment in commitments
            join requisition in requisitions on commitment.PurchaseRequisitionId equals requisition.Id
            join budget in budgets on commitment.ProcurementBudgetId equals budget.Id
            select new CommitmentRegisterRow
            {
                ReservationReference = commitment.ReservationReference,
                RequisitionNumber = requisition.RequisitionNumber,
                BudgetCode = budget.BudgetCode,
                FiscalYear = budget.FiscalYear,
                Status = commitment.Status,
                Currency = commitment.Currency,
                ReservedAmount = commitment.ReservedAmount,
                FormallyCommittedAmount = commitment.FormallyCommittedAmount,
                UtilizedAmount = commitment.UtilizedAmount,
                BudgetAvailableBefore = commitment.BudgetAvailableBefore,
                BudgetAvailableAfter = commitment.BudgetAvailableAfter,
                ReservedAt = commitment.ReservedAtUtc,
                ReservedBy = commitment.ReservedByName,
                ConsumedAt = commitment.ConsumedAtUtc,
                ReleasedAt = commitment.ReleasedAtUtc,
                ReleaseReason = commitment.ReleaseReason,
                IsOverride = commitment.IsOverride,
                OverrideRuleCode = commitment.OverrideRuleCode,
                OverrideApprovalReference = commitment.OverrideApprovalReference,
                AdjustmentCount = adjustments.Count(item => item.BudgetCommitmentId == commitment.Id),
                AdjustmentDelta = adjustments.Where(item => item.BudgetCommitmentId == commitment.Id)
                    .Sum(item => (decimal?)item.DeltaAmount) ?? 0m
            };
        query = ApplyDate(query, filters, item => item.ReservedAt);
        if (filters.FiscalYear.HasValue) query = query.Where(item => item.FiscalYear == filters.FiscalYear.Value);
        if (!string.IsNullOrWhiteSpace(filters.Status) &&
            Enum.TryParse<ProcurementBudgetCommitmentStatus>(filters.Status, true, out var status))
            query = query.Where(item => item.Status == status);
        query = query.OrderByDescending(item => item.ReservedAt).ThenBy(item => item.ReservationReference);
        return await PageAsync(query, definition, request, filters, item => Row(
            ("ReservationReference", item.ReservationReference), ("RequisitionNumber", item.RequisitionNumber),
            ("BudgetCode", item.BudgetCode), ("FiscalYear", item.FiscalYear), ("Status", item.Status.ToString()),
            ("Currency", item.Currency), ("ReservedAmount", item.ReservedAmount),
            ("OutstandingReservedAmount", item.OutstandingReservedAmount),
            ("FormallyCommittedAmount", item.FormallyCommittedAmount), ("UtilizedAmount", item.UtilizedAmount),
            ("BudgetAvailableBefore", item.BudgetAvailableBefore), ("BudgetAvailableAfter", item.BudgetAvailableAfter),
            ("ReservedAt", item.ReservedAt), ("ReservedBy", item.ReservedBy), ("ConsumedAt", item.ConsumedAt),
            ("ReleasedAt", item.ReleasedAt), ("ReleaseReason", item.ReleaseReason), ("IsOverride", item.IsOverride),
            ("OverrideRuleCode", item.OverrideRuleCode), ("OverrideApprovalReference", item.OverrideApprovalReference),
            ("AdjustmentCount", item.AdjustmentCount), ("AdjustmentDelta", item.AdjustmentDelta)), cancellationToken);
    }

    private async Task<ReportResultDto> ExecuteCertificateTrackingAsync(
        ProcurementSystemReportDefinition definition, ReportFilters filters, ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var certificates = Query<ProjectPaymentCertificate>();
        var projects = Query<Project>();
        var contracts = Query<Contract>();
        var partners = Query<BusinessPartner>();
        var certificateContracts =
            from certificate in certificates
            join project in projects on certificate.ProjectId equals project.Id
            join contract in contracts on certificate.ContractId equals (Guid?)contract.Id into contractJoin
            from contract in contractJoin.DefaultIfEmpty()
            select new { certificate, project, contract };
        var query =
            from source in certificateContracts
            join partner in partners
                on (source.certificate.SubcontractorBusinessPartnerId ??
                    (source.contract == null ? (Guid?)null : source.contract.BusinessPartnerId)) equals (Guid?)partner.Id
                into partnerJoin
            from partner in partnerJoin.DefaultIfEmpty()
            select new CertificateTrackingRow
            {
                BusinessPartnerId = source.certificate.SubcontractorBusinessPartnerId ??
                    (source.contract == null ? null : source.contract.BusinessPartnerId),
                CertificateNumber = source.certificate.CertificateNumber,
                Title = source.certificate.Title,
                IssueDate = source.certificate.IssueDate,
                ProjectCode = source.project.ProjectCode,
                ProjectTitle = source.project.Title,
                ContractNumber = source.contract == null ? null : source.contract.ContractNumber,
                SupplierCode = partner == null ? null : partner.PartnerCode,
                SupplierName = partner == null ? null : partner.PartnerName,
                Status = source.certificate.Status,
                ApprovalStatus = source.certificate.ApprovalStatus,
                Currency = source.certificate.Currency,
                GrossCertifiedAmount = source.certificate.GrossCertifiedAmount,
                RetentionHeldAmount = source.certificate.RetentionHeldAmount,
                DeductionsAmount = source.certificate.OtherDeductionsAmount + source.certificate.AdvanceRecoveryAmount +
                    source.certificate.MaterialDeductionAmount,
                TaxAmount = source.certificate.TaxAmount,
                NetCertifiedAmount = source.certificate.NetCertifiedAmount,
                ApprovedAt = source.certificate.ApprovedAt,
                ApHandoffStatus = source.certificate.ApHandoffStatus,
                ApHandoffAt = source.certificate.ApHandoffAt,
                PaymentStatus = source.certificate.PaymentStatusSnapshot,
                PaymentDueDate = source.certificate.PaymentDueDate,
                VendorInvoiceLinked = source.certificate.VendorInvoiceId.HasValue,
                DocumentGenerated = source.certificate.CentralDocumentVersionId.HasValue &&
                    source.certificate.GeneratedAt.HasValue && source.certificate.GeneratedDocumentHash != null
            };
        query = ApplyDate(query, filters, item => item.IssueDate);
        if (!string.IsNullOrWhiteSpace(filters.Status))
            query = query.Where(item => item.Status == filters.Status || item.ApprovalStatus == filters.Status);
        if (filters.SupplierId.HasValue) query = query.Where(item => item.BusinessPartnerId == filters.SupplierId.Value);
        query = query.OrderByDescending(item => item.IssueDate).ThenBy(item => item.CertificateNumber);
        return await PageAsync(query, definition, request, filters, item => Row(
            ("CertificateNumber", item.CertificateNumber), ("Title", item.Title), ("IssueDate", item.IssueDate),
            ("ProjectCode", item.ProjectCode), ("ProjectTitle", item.ProjectTitle),
            ("ContractNumber", item.ContractNumber), ("SupplierCode", item.SupplierCode),
            ("SupplierName", item.SupplierName), ("Status", item.Status), ("ApprovalStatus", item.ApprovalStatus),
            ("Currency", item.Currency), ("GrossCertifiedAmount", item.GrossCertifiedAmount),
            ("RetentionHeldAmount", item.RetentionHeldAmount), ("DeductionsAmount", item.DeductionsAmount),
            ("TaxAmount", item.TaxAmount), ("NetCertifiedAmount", item.NetCertifiedAmount),
            ("ApprovedAt", item.ApprovedAt), ("ApHandoffStatus", item.ApHandoffStatus),
            ("ApHandoffAt", item.ApHandoffAt), ("PaymentStatus", item.PaymentStatus),
            ("PaymentDueDate", item.PaymentDueDate), ("VendorInvoiceLinked", item.VendorInvoiceLinked),
            ("DocumentGenerated", item.DocumentGenerated)), cancellationToken);
    }

    private async Task<ReportResultDto> ExecuteExceptionRegisterAsync(
        ProcurementSystemReportDefinition definition, ReportFilters filters, ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var controls = Query<ProcurementExceptionalSourcingControl>();
        var tenders = Query<Tender>();
        var sourcingCases = Query<ProcurementSourcingCase>();
        var query =
            from control in controls
            join tender in tenders on control.TenderId equals tender.Id
            join sourcingCase in sourcingCases on control.SourcingCaseId equals sourcingCase.Id
            select new ExceptionRegisterRow
            {
                TenderNumber = tender.TenderNumber,
                SourcingReference = sourcingCase.CaseNumber,
                Method = control.Method,
                Status = control.Status,
                Justification = control.Justification,
                ExceptionRuleCode = control.ExceptionRuleCode,
                AuthorityRouteReference = control.AuthorityRouteReference,
                JustificationEvidenceReference = control.JustificationEvidenceReference,
                SupplierSelectionEvidenceReference = control.SupplierSelectionEvidenceReference,
                PreparedAt = control.PreparedAtUtc,
                SubmittedAt = control.SubmittedForApprovalAtUtc,
                ApprovedAt = control.ApprovedAtUtc,
                ManagingDirectorApprovalRequired = control.ManagingDirectorApprovalRequired,
                ManagingDirectorApprovalReference = control.ManagingDirectorApprovalReference,
                PpaApprovalRequired = control.PpaApprovalRequired,
                PpaApprovalReference = control.PpaApprovalReference,
                AwardReference = control.AwardReference,
                ContractReference = control.ContractReference,
                ExceptionReportReference = control.ExceptionReportReference,
                PostAwardFilingReference = control.PostAwardFilingReference,
                FiledAt = control.FiledAtUtc
            };
        query = ApplyDate(query, filters, item => item.PreparedAt);
        if (!string.IsNullOrWhiteSpace(filters.Status) &&
            Enum.TryParse<ProcurementExceptionalSourcingControlStatus>(filters.Status, true, out var status))
            query = query.Where(item => item.Status == status);
        query = query.OrderByDescending(item => item.PreparedAt).ThenBy(item => item.TenderNumber);
        return await PageAsync(query, definition, request, filters, item => Row(
            ("TenderNumber", item.TenderNumber), ("SourcingReference", item.SourcingReference),
            ("Method", item.Method.ToString()), ("Status", item.Status.ToString()),
            ("Justification", item.Justification), ("ExceptionRuleCode", item.ExceptionRuleCode),
            ("AuthorityRouteReference", item.AuthorityRouteReference),
            ("JustificationEvidenceReference", item.JustificationEvidenceReference),
            ("SupplierSelectionEvidenceReference", item.SupplierSelectionEvidenceReference),
            ("PreparedAt", item.PreparedAt), ("SubmittedAt", item.SubmittedAt), ("ApprovedAt", item.ApprovedAt),
            ("ManagingDirectorApprovalRequired", item.ManagingDirectorApprovalRequired),
            ("ManagingDirectorApprovalReference", item.ManagingDirectorApprovalReference),
            ("PpaApprovalRequired", item.PpaApprovalRequired), ("PpaApprovalReference", item.PpaApprovalReference),
            ("AwardReference", item.AwardReference), ("ContractReference", item.ContractReference),
            ("ExceptionReportReference", item.ExceptionReportReference),
            ("PostAwardFilingReference", item.PostAwardFilingReference), ("FiledAt", item.FiledAt)), cancellationToken);
    }

    private async Task<ReportResultDto> ExecuteProcurementToPaymentAsync(
        ProcurementSystemReportDefinition definition, ReportFilters filters, ExecuteReportDto request,
        CancellationToken cancellationToken)
    {
        var purchaseOrders = Query<PurchaseOrder>();
        var partners = Query<BusinessPartner>();
        var requisitions = Query<PurchaseRequisition>();
        var receipts = Query<PurchaseOrderReceipt>();
        var receiptItems = Query<PurchaseOrderReceiptItem>();
        var invoices = Query<VendorInvoice>();
        var allocations = Query<VendorPaymentAllocation>();
        var payments = Query<VendorPayment>();
        var query =
            from order in purchaseOrders
            join partner in partners on order.BusinessPartnerId equals partner.Id
            join requisition in requisitions on order.SourceRequisitionId equals (Guid?)requisition.Id into requisitionJoin
            from requisition in requisitionJoin.DefaultIfEmpty()
            select new ProcurementToPaymentRow
            {
                BusinessPartnerId = order.BusinessPartnerId,
                RequisitionNumber = requisition == null ? order.SourceRequisitionNumber : requisition.RequisitionNumber,
                RequisitionStatus = requisition == null ? null : requisition.Status,
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                OrderDate = order.OrderDate,
                OrderStatus = order.Status,
                SupplierName = partner.PartnerName,
                Currency = order.Currency,
                OrderAmount = order.TotalAmount,
                ReceiptCount = receipts.Count(receipt => receipt.PurchaseOrderId == order.Id),
                AcceptedQuantity = receiptItems.Where(receiptItem => receipts.Any(receipt =>
                        receipt.Id == receiptItem.ReceiptId && receipt.PurchaseOrderId == order.Id))
                    .Sum(receiptItem => (decimal?)receiptItem.AcceptedQuantity) ?? 0m,
                InvoiceCount = invoices.Count(invoice => invoice.PurchaseOrderId == order.Id &&
                    invoice.Status != VendorInvoiceStatus.Voided && invoice.Status != VendorInvoiceStatus.Rejected),
                InvoiceAmount = invoices.Where(invoice => invoice.PurchaseOrderId == order.Id &&
                        invoice.Status != VendorInvoiceStatus.Voided && invoice.Status != VendorInvoiceStatus.Rejected)
                    .Sum(invoice => (decimal?)invoice.TotalAmount) ?? 0m,
                MatchedInvoiceCount = invoices.Count(invoice => invoice.PurchaseOrderId == order.Id &&
                    invoice.Status != VendorInvoiceStatus.Voided && invoice.Status != VendorInvoiceStatus.Rejected &&
                    invoice.MatchingType == InvoiceMatchingType.ThreeWay &&
                    invoice.MatchingStatus == InvoiceMatchingStatus.ThreeWayMatched),
                PaidAmount = allocations.Where(allocation => !allocation.IsReversal &&
                        invoices.Any(invoice => invoice.Id == allocation.VendorInvoiceId && invoice.PurchaseOrderId == order.Id &&
                            invoice.Status != VendorInvoiceStatus.Voided && invoice.Status != VendorInvoiceStatus.Rejected) &&
                        payments.Any(payment => payment.Id == allocation.VendorPaymentId &&
                            (payment.Status == VendorPaymentStatus.Processed ||
                             payment.Status == VendorPaymentStatus.Cleared ||
                             payment.Status == VendorPaymentStatus.Reconciled)))
                    .Sum(allocation => (decimal?)allocation.AllocatedAmount) ?? 0m,
                LatestPaymentAt = allocations.Where(allocation => !allocation.IsReversal &&
                        invoices.Any(invoice => invoice.Id == allocation.VendorInvoiceId && invoice.PurchaseOrderId == order.Id &&
                            invoice.Status != VendorInvoiceStatus.Voided && invoice.Status != VendorInvoiceStatus.Rejected) &&
                        payments.Any(payment => payment.Id == allocation.VendorPaymentId &&
                            (payment.Status == VendorPaymentStatus.Processed ||
                             payment.Status == VendorPaymentStatus.Cleared ||
                             payment.Status == VendorPaymentStatus.Reconciled)))
                    .Max(allocation => (DateTime?)allocation.AllocationDate)
            };
        query = ApplyDate(query, filters, item => item.OrderDate);
        if (!string.IsNullOrWhiteSpace(filters.Status)) query = query.Where(item => item.OrderStatus == filters.Status);
        if (filters.SupplierId.HasValue) query = query.Where(item => item.BusinessPartnerId == filters.SupplierId.Value);
        query = query.OrderByDescending(item => item.OrderDate).ThenBy(item => item.OrderNumber);
        return await PageAsync(query, definition, request, filters, item => Row(
            ("RequisitionNumber", item.RequisitionNumber), ("RequisitionStatus", item.RequisitionStatus),
            ("OrderNumber", item.OrderNumber), ("OrderDate", item.OrderDate), ("OrderStatus", item.OrderStatus),
            ("SupplierName", item.SupplierName), ("Currency", item.Currency), ("OrderAmount", item.OrderAmount),
            ("ReceiptCount", item.ReceiptCount), ("AcceptedQuantity", item.AcceptedQuantity),
            ("InvoiceCount", item.InvoiceCount), ("InvoiceAmount", item.InvoiceAmount),
            ("MatchedInvoiceCount", item.MatchedInvoiceCount), ("PaidAmount", item.PaidAmount),
            ("OutstandingAmount", item.OutstandingAmount), ("LatestPaymentAt", item.LatestPaymentAt)), cancellationToken);
    }

    private IQueryable<T> Query<T>() where T : TenantEntity
    {
        var tenantId = RequiredTenantId();

        // Do not rely only on the optional DbContext global tenant filter. Reports are an
        // aggregation surface, so every source query carries an explicit tenant predicate.
        return _unitOfWork.Repository<T>()
            .GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted)
            .AsNoTracking();
    }

    private Guid RequiredTenantId()
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("A tenant context is required to execute procurement reports.");
        return tenantId;
    }

    private async Task<ReportResultDto> PageAsync<T>(
        IQueryable<T> query,
        ProcurementSystemReportDefinition definition,
        ExecuteReportDto request,
        ReportFilters filters,
        Func<T, Dictionary<string, object>> map,
        CancellationToken cancellationToken)
    {
        var totalRows = await query.CountAsync(cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 1000);
        if (request.MaxRows is > 0) pageSize = Math.Min(pageSize, Math.Clamp(request.MaxRows.Value, 1, 1000));
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var totalPages = totalRows == 0 ? 0 : (int)Math.Ceiling(totalRows / (double)pageSize);
        return new ReportResultDto
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
            Data = rows.Select(map).ToList(),
            CurrentPage = page,
            PageSize = pageSize,
            TotalPages = totalPages,
            HasNextPage = page < totalPages,
            HasPreviousPage = page > 1 && totalPages > 0,
            Metadata = new ReportMetadataDto
            {
                Parameters = filters.ToMetadata(),
                Query = definition.Query,
                DataAsOf = DateTime.UtcNow,
                DataSource = "Tenant-scoped ERP procurement transaction owners",
                Statistics = new Dictionary<string, object>
                {
                    ["systemCode"] = definition.Code,
                    ["page"] = page,
                    ["pageSize"] = pageSize,
                    ["totalRows"] = totalRows
                }
            }
        };
    }

    private async Task EnsureCapabilityAsync(
        string permission,
        string sourceReference,
        bool isAdministrator,
        CancellationToken cancellationToken)
    {
        if (isAdministrator) return;
        var decision = await _accessControl.EnforceCapabilityAsync(
            Capability(permission, sourceReference), NewCorrelation(), cancellationToken);
        if (!decision.Allowed) throw new UnauthorizedAccessException(decision.Message);
    }

    private static ProcurementAccessCapabilityRequest Capability(string permission, string sourceReference) => new()
    {
        PermissionCode = permission,
        SourceType = SourceType,
        SourceReference = sourceReference
    };

    private static string NewCorrelation() => $"tdc0701-{Guid.NewGuid():N}";

    private static IQueryable<T> ApplyDate<T>(IQueryable<T> query, ReportFilters filters, System.Linq.Expressions.Expression<Func<T, DateTime>> selector)
    {
        if (filters.StartUtc.HasValue)
        {
            var value = filters.StartUtc.Value;
            query = query.Where(BuildDatePredicate(selector, value, false));
        }
        if (filters.EndExclusiveUtc.HasValue)
        {
            var value = filters.EndExclusiveUtc.Value;
            query = query.Where(BuildDatePredicate(selector, value, true));
        }
        return query;
    }

    private static System.Linq.Expressions.Expression<Func<T, bool>> BuildDatePredicate<T>(
        System.Linq.Expressions.Expression<Func<T, DateTime>> selector,
        DateTime value,
        bool upperBound)
    {
        var comparison = upperBound
            ? System.Linq.Expressions.Expression.LessThan(selector.Body, System.Linq.Expressions.Expression.Constant(value))
            : System.Linq.Expressions.Expression.GreaterThanOrEqual(selector.Body, System.Linq.Expressions.Expression.Constant(value));
        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(comparison, selector.Parameters);
    }

    private static Dictionary<string, object> Row(params (string Key, object? Value)[] values) =>
        values.ToDictionary(item => item.Key, item => item.Value!);

    private sealed record ReportFilters(DateTime? StartUtc, DateTime? EndExclusiveUtc, int? FiscalYear, string? Status, Guid? SupplierId)
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
            return new ReportFilters(
                start,
                endExclusive,
                GetInt(request.Parameters, "fiscalYear"),
                GetString(request.Parameters, "status"),
                GetGuid(request.Parameters, "supplierId"));
        }

        public Dictionary<string, object> ToMetadata()
        {
            var values = new Dictionary<string, object>();
            if (StartUtc.HasValue) values["startDate"] = StartUtc.Value;
            if (EndExclusiveUtc.HasValue) values["endDate"] = EndExclusiveUtc.Value.AddDays(-1);
            if (FiscalYear.HasValue) values["fiscalYear"] = FiscalYear.Value;
            if (!string.IsNullOrWhiteSpace(Status)) values["status"] = Status;
            if (SupplierId.HasValue) values["supplierId"] = SupplierId.Value;
            return values;
        }

        private static string? GetString(Dictionary<string, object>? values, string key)
        {
            if (values is null || !values.TryGetValue(key, out var value) || value is null) return null;
            var text = value is JsonElement element ? element.ToString() : Convert.ToString(value, CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(text) || text.Equals("all", StringComparison.OrdinalIgnoreCase) ? null : text.Trim();
        }

        private static DateTime? GetDate(Dictionary<string, object>? values, string key) =>
            DateTime.TryParse(GetString(values, key), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value)
                ? value
                : null;

        private static int? GetInt(Dictionary<string, object>? values, string key) =>
            int.TryParse(GetString(values, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;

        private static Guid? GetGuid(Dictionary<string, object>? values, string key) =>
            Guid.TryParse(GetString(values, key), out var value) ? value : null;
    }

    private sealed class AppActualRow
    {
        public string PlanNumber { get; set; } = string.Empty;
        public int FiscalYear { get; set; }
        public string Department { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public DateTime PlanStartDate { get; set; }
        public DateTime PlanEndDate { get; set; }
        public int PlannedItemCount { get; set; }
        public int ProcuredItemCount { get; set; }
        public decimal ApprovedBudget { get; set; }
        public decimal PlannedValue { get; set; }
        public decimal PurchaseOrderValue { get; set; }
        public decimal UnconvertedAwardValue { get; set; }
        public decimal ActualValue => PurchaseOrderValue + UnconvertedAwardValue;
        public ProcurementAppSubmissionStatus? SubmissionStatus { get; set; }
        public string? ExternalReference { get; set; }
    }

    private sealed class TenderRegisterRow
    {
        public string TenderNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string TenderType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime EffectiveDate { get; set; }
        public DateTime? PublishDate { get; set; }
        public DateTime? SubmissionDeadline { get; set; }
        public DateTime? OpeningDate { get; set; }
        public DateTime? AwardDate { get; set; }
        public string Currency { get; set; } = string.Empty;
        public decimal? EstimatedValue { get; set; }
        public int BidCount { get; set; }
        public int AwardCount { get; set; }
        public decimal AwardedValue { get; set; }
        public int ContractCount { get; set; }
    }

    private sealed class ContractRegisterRow
    {
        public Guid BusinessPartnerId { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public string ContractTitle { get; set; } = string.Empty;
        public string ContractType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string SupplierCode { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string TenderNumber { get; set; } = string.Empty;
        public string? ProjectCode { get; set; }
        public string? ProjectName { get; set; }
        public string Currency { get; set; } = string.Empty;
        public decimal ContractValue { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public decimal RetentionPercentage { get; set; }
        public int ApprovedVariationCount { get; set; }
        public decimal ApprovedAmendmentValue { get; set; }
        public int CertificateCount { get; set; }
        public decimal CertifiedAmount { get; set; }
        public int InvoiceCount { get; set; }
        public decimal InvoicedAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal Balance => Math.Max(0m, ContractValue + ApprovedAmendmentValue - PaidAmount);
        public DateTime EffectiveDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? SignedDate { get; set; }
        public int CompletedMilestones { get; set; }
        public int TotalMilestones { get; set; }
    }

    private sealed class SupplierPerformanceRow
    {
        public Guid BusinessPartnerId { get; set; }
        public string ScorecardReference { get; set; } = string.Empty;
        public string SupplierCode { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public DateTime CalculatedAt { get; set; }
        public ProcurementSupplierPerformanceDataStatus DataStatus { get; set; }
        public decimal CoveragePercent { get; set; }
        public decimal? OverallScore { get; set; }
        public string? PerformanceBand { get; set; }
        public decimal MinimumScore { get; set; }
        public bool MinimumScoreBreached { get; set; }
        public int PurchaseOrders { get; set; }
        public int Receipts { get; set; }
        public int Contracts { get; set; }
        public DateTime NextReviewDue { get; set; }
    }

    private sealed class AwardNotificationRow
    {
        public Guid BusinessPartnerId { get; set; }
        public string AwardReference { get; set; } = string.Empty;
        public string SourceReference { get; set; } = string.Empty;
        public ProcurementBidderCommunicationAwardFamily AwardFamily { get; set; }
        public DateTime AwardedAt { get; set; }
        public string SupplierCode { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public ProcurementBidderCommunicationRecipientOutcome Outcome { get; set; }
        public int? ApprovedLetterVersion { get; set; }
        public int DispatchCount { get; set; }
        public DateTime? LatestDispatchAt { get; set; }
        public ProcurementBidderCommunicationDeliveryOutcome? LatestDeliveryOutcome { get; set; }
        public bool Acknowledged { get; set; }
        public int AppealCount { get; set; }
        public DateTime StandstillEndsAt { get; set; }
        public DateTime AppealWindowEndsAt { get; set; }
    }

    private sealed class SavingsRow
    {
        public string TenderNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string TenderType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public decimal EstimatedValue { get; set; }
        public decimal AwardedValue { get; set; }
        public int AwardCount { get; set; }
        public int SupplierCount { get; set; }
        public DateTime AwardDate { get; set; }
    }

    private sealed class EtcMinutesRow
    {
        public string SourceReference { get; set; } = string.Empty;
        public string CommitteeCode { get; set; } = string.Empty;
        public string CommitteeName { get; set; } = string.Empty;
        public int MeetingSequence { get; set; }
        public ProcurementEvaluationPhase Phase { get; set; }
        public ProcurementEvaluationMeetingStatus Status { get; set; }
        public string MeetingMode { get; set; } = string.Empty;
        public string MeetingChannel { get; set; } = string.Empty;
        public DateTime ScheduledAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public int EligibleVoters { get; set; }
        public int SignedAttendance { get; set; }
        public bool QuorumMet { get; set; }
        public bool ChairPresent { get; set; }
        public bool SecretaryPresent { get; set; }
        public string EvidenceReference { get; set; } = string.Empty;
    }

    private sealed class RequisitionStatusRow
    {
        public string RequisitionNumber { get; set; } = string.Empty;
        public DateTime RequisitionDate { get; set; }
        public DateTime? RequiredDate { get; set; }
        public string RequestedBy { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string? CostCenter { get; set; }
        public ProcurementCategoryClass? Category { get; set; }
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int LineCount { get; set; }
        public int OrderedLineCount { get; set; }
        public int PurchaseOrderCount { get; set; }
        public int ApprovalLevel { get; set; }
        public int RequiredApprovalLevel { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? SourcePlanNumber { get; set; }
        public string? ProjectCode { get; set; }
        public string? BudgetCode { get; set; }
        public string? ExceptionRuleCode { get; set; }
    }

    private sealed class PurchaseOrderRegisterRow
    {
        public Guid BusinessPartnerId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public string SupplierCode { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public ProcurementCategoryClass? Category { get; set; }
        public string OrderType { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string? SourceRequisitionNumber { get; set; }
        public ProcurementPurchaseOrderSourceType? SourceType { get; set; }
        public string? SourceReference { get; set; }
        public string? TenderNumber { get; set; }
        public string? ContractNumber { get; set; }
        public int RevisionNumber { get; set; }
        public DateTime? RequiredDate { get; set; }
        public DateTime? PromisedDate { get; set; }
        public DateTime? ReceivedDate { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public int LineCount { get; set; }
        public int ReceiptCount { get; set; }
        public DateTime? LatestReceiptAt { get; set; }
    }

    private sealed class CommitmentRegisterRow
    {
        public string ReservationReference { get; set; } = string.Empty;
        public string RequisitionNumber { get; set; } = string.Empty;
        public string BudgetCode { get; set; } = string.Empty;
        public int FiscalYear { get; set; }
        public ProcurementBudgetCommitmentStatus Status { get; set; }
        public string Currency { get; set; } = string.Empty;
        public decimal ReservedAmount { get; set; }
        public decimal OutstandingReservedAmount => Math.Max(0m, ReservedAmount - FormallyCommittedAmount);
        public decimal FormallyCommittedAmount { get; set; }
        public decimal UtilizedAmount { get; set; }
        public decimal BudgetAvailableBefore { get; set; }
        public decimal BudgetAvailableAfter { get; set; }
        public DateTime ReservedAt { get; set; }
        public string ReservedBy { get; set; } = string.Empty;
        public DateTime? ConsumedAt { get; set; }
        public DateTime? ReleasedAt { get; set; }
        public string? ReleaseReason { get; set; }
        public bool IsOverride { get; set; }
        public string? OverrideRuleCode { get; set; }
        public string? OverrideApprovalReference { get; set; }
        public int AdjustmentCount { get; set; }
        public decimal AdjustmentDelta { get; set; }
    }

    private sealed class CertificateTrackingRow
    {
        public Guid? BusinessPartnerId { get; set; }
        public string? CertificateNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime IssueDate { get; set; }
        public string ProjectCode { get; set; } = string.Empty;
        public string ProjectTitle { get; set; } = string.Empty;
        public string? ContractNumber { get; set; }
        public string? SupplierCode { get; set; }
        public string? SupplierName { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ApprovalStatus { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public decimal GrossCertifiedAmount { get; set; }
        public decimal RetentionHeldAmount { get; set; }
        public decimal DeductionsAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal NetCertifiedAmount { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string ApHandoffStatus { get; set; } = string.Empty;
        public DateTime? ApHandoffAt { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime? PaymentDueDate { get; set; }
        public bool VendorInvoiceLinked { get; set; }
        public bool DocumentGenerated { get; set; }
    }

    private sealed class ExceptionRegisterRow
    {
        public string TenderNumber { get; set; } = string.Empty;
        public string SourcingReference { get; set; } = string.Empty;
        public ProcurementMethodType Method { get; set; }
        public ProcurementExceptionalSourcingControlStatus Status { get; set; }
        public string Justification { get; set; } = string.Empty;
        public string ExceptionRuleCode { get; set; } = string.Empty;
        public string AuthorityRouteReference { get; set; } = string.Empty;
        public string JustificationEvidenceReference { get; set; } = string.Empty;
        public string SupplierSelectionEvidenceReference { get; set; } = string.Empty;
        public DateTime PreparedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public bool ManagingDirectorApprovalRequired { get; set; }
        public string? ManagingDirectorApprovalReference { get; set; }
        public bool PpaApprovalRequired { get; set; }
        public string? PpaApprovalReference { get; set; }
        public string? AwardReference { get; set; }
        public string? ContractReference { get; set; }
        public string? ExceptionReportReference { get; set; }
        public string? PostAwardFilingReference { get; set; }
        public DateTime? FiledAt { get; set; }
    }

    private sealed class ProcurementToPaymentRow
    {
        public Guid BusinessPartnerId { get; set; }
        public string? RequisitionNumber { get; set; }
        public string? RequisitionStatus { get; set; }
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public string OrderStatus { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public decimal OrderAmount { get; set; }
        public int ReceiptCount { get; set; }
        public decimal AcceptedQuantity { get; set; }
        public int InvoiceCount { get; set; }
        public decimal InvoiceAmount { get; set; }
        public int MatchedInvoiceCount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal OutstandingAmount => Math.Max(0m, InvoiceAmount - PaidAmount);
        public DateTime? LatestPaymentAt { get; set; }
    }
}
