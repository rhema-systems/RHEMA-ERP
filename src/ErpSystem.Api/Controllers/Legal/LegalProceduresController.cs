using ErpSystem.Core.Interfaces.Legal;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Legal;

[ApiController]
[Route("api/legal/procedures")]
[Authorize]
public sealed class LegalProceduresController : ControllerBase
{
    private static readonly string[] AllowedRoles =
    [
        "admin", "Admin", "SystemAdmin", "SuperAdmin", "TenantAdmin",
        "Legal", "Legal Officer", "Legal Manager", "Head of Legal",
        "Legal Admin Assistant", "Secretary", "Registry", "Legal Registry"
    ];

    private readonly ILegalProcedureCatalogService _procedureCatalog;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public LegalProceduresController(
        ILegalProcedureCatalogService procedureCatalog,
        ApplicationDbContext db,
        ICurrentUserService currentUser)
    {
        _procedureCatalog = procedureCatalog;
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        if (!CanReadLegal())
        {
            return Forbid();
        }

        var tenantId = _currentUser.TenantId;
        if (!tenantId.HasValue || tenantId == Guid.Empty)
        {
            return Unauthorized(new { success = false, message = "A tenant context is required." });
        }

        var cases = await _db.ProcedureCases
            .AsNoTracking()
            .Include(item => item.Fields.Where(field => !field.IsDeleted))
            .Where(item => item.TenantId == tenantId.Value
                && item.Module == "Legal"
                && !item.IsDeleted)
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Take(1000)
            .ToListAsync(cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var inThirtyDays = today.AddDays(30);
        var courtCases = cases.Where(item =>
            item.EntityType == "LegalCourtProcess"
            || item.EntityType == "LegalOtherCourtProcess").ToList();
        string? Value(ErpSystem.Core.Entities.Procedures.ProcedureCase item, string key)
            => item.Fields.FirstOrDefault(field =>
                string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;
        bool IsOpen(ErpSystem.Core.Entities.Procedures.ProcedureCase item)
            => !string.Equals(item.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(item.Status, "Closed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(item.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(item.Status, "Canceled", StringComparison.OrdinalIgnoreCase);
        bool IsOutcome(ErpSystem.Core.Entities.Procedures.ProcedureCase item, string outcome)
            => string.Equals(Value(item, "courtOutcome"), outcome, StringComparison.OrdinalIgnoreCase);
        bool DateInRange(string? value, DateOnly start, DateOnly end)
            => DateOnly.TryParse(value, out var date) && date >= start && date <= end;
        bool DateBefore(string? value, DateOnly date)
            => DateOnly.TryParse(value, out var parsed) && parsed < date;
        LegalDashboardQueueItem QueueItem(
            ErpSystem.Core.Entities.Procedures.ProcedureCase item,
            string statusDetail,
            string? dueDate = null)
            => new(
                item.Id,
                item.EntityType,
                item.Title,
                item.ReferenceNumber,
                item.ApplicantName,
                item.CurrentStageName,
                Value(item, "assignedLegalOfficer") ?? item.CurrentAssignedRole,
                statusDetail,
                dueDate,
                Value(item, "litigationRisk") ?? Value(item, "legalRiskLevel"));
        string? FirstValue(ErpSystem.Core.Entities.Procedures.ProcedureCase item, params string[] keys)
            => keys.Select(key => Value(item, key)).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        bool ValueIn(ErpSystem.Core.Entities.Procedures.ProcedureCase item, string key, params string[] values)
            => values.Any(value => string.Equals(Value(item, key), value, StringComparison.OrdinalIgnoreCase));
        bool PendingOrBlank(ErpSystem.Core.Entities.Procedures.ProcedureCase item, string key, params string[] completedValues)
        {
            var value = Value(item, key);
            return string.IsNullOrWhiteSpace(value)
                || !completedValues.Any(completed => string.Equals(value, completed, StringComparison.OrdinalIgnoreCase));
        }
        var procedures = _procedureCatalog.GetProcedures()
            .ToDictionary(item => item.EntityType, item => item.Title, StringComparer.OrdinalIgnoreCase);
        LegalSopControlItem SopItem(
            string category,
            ErpSystem.Core.Entities.Procedures.ProcedureCase item,
            string statusDetail,
            string? dueDate = null,
            string? reference = null)
            => new(
                item.Id,
                item.EntityType,
                procedures.TryGetValue(item.EntityType, out var procedureTitle) ? procedureTitle : item.EntityType,
                category,
                item.Title,
                item.ReferenceNumber,
                item.ApplicantName,
                item.CurrentStageName,
                Value(item, "assignedLegalOfficer") ?? item.CurrentAssignedRole,
                statusDetail,
                dueDate,
                reference,
                Value(item, "litigationRisk") ?? Value(item, "legalRiskLevel"),
                item.UpdatedAt ?? item.CreatedAt);
        var sopControlItems = new List<LegalSopControlItem>();

        foreach (var item in cases.Where(IsOpen))
        {
            if (PendingOrBlank(item, "legalFileRegisterStatus", "Registered", "Closed")
                || PendingOrBlank(item, "loOwnershipStatus", "Completed"))
            {
                sopControlItems.Add(SopItem(
                    "File movement",
                    item,
                    FirstValue(item, "legalFileRegisterStatus", "loOwnershipStatus") ?? "File movement pending",
                    FirstValue(item, "holAssignmentDate", "secretaryRoutingDate", "registryReceiptDate"),
                    FirstValue(item, "legalFileNumber", "registryReceiptReference", "courtJacketReference")));
            }

            if (PendingOrBlank(item, "generatedDocumentStatus", "Final generated", "Not required")
                || ValueIn(item, "draftVersionStatus", "Returned for correction")
                || ValueIn(item, "vettingCommentStatus", "Open comments"))
            {
                sopControlItems.Add(SopItem(
                    "Templates and drafting",
                    item,
                    FirstValue(item, "generatedDocumentStatus", "draftVersionStatus", "vettingCommentStatus") ?? "Draft/template pending",
                    null,
                    FirstValue(item, "templateCode", "draftDocumentReference")));
            }

            if (PendingOrBlank(item, "signatureStatus", "Fully signed")
                || ValueIn(item, "sealStatus", "Pending")
                || PendingOrBlank(item, "dispatchStatus", "Picked up", "Dispatched", "Filed"))
            {
                sopControlItems.Add(SopItem(
                    "Signature / sealing / dispatch",
                    item,
                    FirstValue(item, "signatureStatus", "sealStatus", "dispatchStatus") ?? "Execution pending",
                    FirstValue(item, "collectionDate", "sealedDate"),
                    FirstValue(item, "sealRegisterNumber", "dispatchReference")));
            }

            if (ValueIn(item, "paymentStatus", "Pending")
                || ValueIn(item, "financeVerificationStatus", "Pending Finance verification", "Rejected")
                || DateBefore(Value(item, "paymentDueDate"), today))
            {
                sopControlItems.Add(SopItem(
                    "Finance payment controls",
                    item,
                    FirstValue(item, "financeVerificationStatus", "paymentStatus") ?? "Payment control pending",
                    Value(item, "paymentDueDate"),
                    FirstValue(item, "financeVerificationReference", "paymentReceiptReference", "transferFeeInvoiceReference")));
            }

            if (ValueIn(item, "estateReturnStatus", "Pending return", "Returned for correction")
                || ValueIn(item, "estateRecordsAmendmentStatus", "Pending", "Returned for correction")
                || ValueIn(item, "landsCommissionHandoff", "Pending", "Sent", "Returned")
                || ValueIn(item, "revenueUpdateStatus", "Pending", "Returned for correction"))
            {
                sopControlItems.Add(SopItem(
                    "Estate / registration close-out",
                    item,
                    FirstValue(item, "landsCommissionHandoff", "estateRecordsAmendmentStatus", "estateReturnStatus", "revenueUpdateStatus") ?? "Estate close-out pending",
                    FirstValue(item, "landsCommissionReturnDate", "estateFileReturnDate", "estateRecordsAmendmentDate"),
                    FirstValue(item, "landsCommissionRegistrationNumber", "sourceRecordReference", "propertyFileReference")));
            }

            if (item.EntityType == "LegalCourtProcess" || item.EntityType == "LegalOtherCourtProcess")
            {
                sopControlItems.Add(SopItem(
                    "Court calendar and litigation",
                    item,
                    FirstValue(item, "courtMatterStatus", "courtOutcome", "appealStatus") ?? "Court action pending",
                    FirstValue(item, "responseDeadline", "appearanceDeadline", "nextHearingDate"),
                    FirstValue(item, "caseNumber", "courtJacketReference", "filingReference")));
            }

            if (item.EntityType == "LegalExternalCounsel")
            {
                sopControlItems.Add(SopItem(
                    "External counsel",
                    item,
                    FirstValue(item, "counselMatterStatus", "performanceRating") ?? "Counsel matter active",
                    Value(item, "counselDeliverableDueDate"),
                    FirstValue(item, "counselName", "instructionReference", "invoiceReference")));
            }

            if (item.EntityType == "LegalOpinionAdvisory")
            {
                sopControlItems.Add(SopItem(
                    "Legal opinions",
                    item,
                    FirstValue(item, "recommendationStatus", "confidentialityLevel") ?? "Opinion/advice active",
                    Value(item, "opinionDueDate"),
                    FirstValue(item, "advisorySubject", "adviceRecipient")));
            }
        }
        var sopCategoryCounts = sopControlItems
            .GroupBy(item => item.Category)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        return Ok(new
        {
            success = true,
            data = new
            {
                totalMatters = cases.Count,
                openMatters = cases.Count(IsOpen),
                completedMatters = cases.Count(item => !IsOpen(item)),
                propertyLinkedMatters = cases.Count(item => !string.IsNullOrWhiteSpace(Value(item, "sourceProcedureCaseId"))),
                activeCourtCases = courtCases.Count(IsOpen),
                courtPending = courtCases.Count(item => IsOpen(item) && (string.IsNullOrWhiteSpace(Value(item, "courtOutcome")) || IsOutcome(item, "Pending"))),
                courtWon = courtCases.Count(item => IsOutcome(item, "Won")),
                courtLost = courtCases.Count(item => IsOutcome(item, "Lost")),
                courtSettled = courtCases.Count(item => IsOutcome(item, "Settled")),
                courtWithdrawn = courtCases.Count(item => IsOutcome(item, "Withdrawn") || IsOutcome(item, "Struck out")),
                hearingsNext30Days = courtCases.Count(item => IsOpen(item) && DateInRange(Value(item, "nextHearingDate"), today, inThirtyDays)),
                overdueResponseDeadlines = courtCases.Count(item => IsOpen(item) && DateBefore(Value(item, "responseDeadline"), today)),
                pendingSignatures = cases.Count(item => IsOpen(item) && !string.Equals(Value(item, "signatureStatus"), "Fully signed", StringComparison.OrdinalIgnoreCase)),
                pendingPayments = cases.Count(item => IsOpen(item) && string.Equals(Value(item, "paymentStatus"), "Pending", StringComparison.OrdinalIgnoreCase)),
                awaitingEstateReturn = cases.Count(item => IsOpen(item) && string.Equals(Value(item, "estateReturnStatus"), "Pending return", StringComparison.OrdinalIgnoreCase)),
                templateDraftingControls = sopCategoryCounts.GetValueOrDefault("Templates and drafting"),
                fileMovementControls = sopCategoryCounts.GetValueOrDefault("File movement"),
                signatureDispatchControls = sopCategoryCounts.GetValueOrDefault("Signature / sealing / dispatch"),
                financePaymentControls = sopCategoryCounts.GetValueOrDefault("Finance payment controls"),
                estateRegistrationControls = sopCategoryCounts.GetValueOrDefault("Estate / registration close-out"),
                courtCalendarControls = sopCategoryCounts.GetValueOrDefault("Court calendar and litigation"),
                externalCounselControls = sopCategoryCounts.GetValueOrDefault("External counsel"),
                legalOpinionControls = sopCategoryCounts.GetValueOrDefault("Legal opinions"),
                pendingSignatureMatters = cases
                    .Where(item => IsOpen(item) && !string.Equals(Value(item, "signatureStatus"), "Fully signed", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
                    .Select(item => QueueItem(item, Value(item, "signatureStatus") ?? "Signature not complete"))
                    .Take(8)
                    .ToList(),
                pendingPaymentMatters = cases
                    .Where(item => IsOpen(item) && string.Equals(Value(item, "paymentStatus"), "Pending", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
                    .Select(item => QueueItem(item, Value(item, "paymentStatus") ?? "Payment pending", Value(item, "paymentDueDate")))
                    .Take(8)
                    .ToList(),
                awaitingEstateReturnMatters = cases
                    .Where(item => IsOpen(item) && string.Equals(Value(item, "estateReturnStatus"), "Pending return", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
                    .Select(item => QueueItem(item, Value(item, "estateReturnStatus") ?? "Pending Estate return"))
                    .Take(8)
                    .ToList(),
                overdueCourtDeadlines = courtCases
                    .Where(item => IsOpen(item) && DateBefore(Value(item, "responseDeadline"), today))
                    .OrderBy(item => Value(item, "responseDeadline"))
                    .Select(item => QueueItem(item, "Response deadline overdue", Value(item, "responseDeadline")))
                    .Take(8)
                    .ToList(),
                upcomingCourtEvents = courtCases
                    .Where(item => IsOpen(item) && (DateOnly.TryParse(Value(item, "nextHearingDate"), out _) || DateOnly.TryParse(Value(item, "responseDeadline"), out _)))
                    .Select(item => new
                    {
                        item.Id,
                        item.Title,
                        item.ReferenceNumber,
                        CourtName = Value(item, "courtName"),
                        CaseNumber = Value(item, "caseNumber"),
                        ResponseDeadline = Value(item, "responseDeadline"),
                        NextHearingDate = Value(item, "nextHearingDate"),
                        Risk = Value(item, "litigationRisk"),
                        item.CurrentStageName
                    })
                    .OrderBy(item => item.NextHearingDate ?? item.ResponseDeadline)
                    .Take(10)
                    .ToList(),
                sopControlItems = sopControlItems
                    .OrderBy(item => DateOnly.TryParse(item.DueDate, out var dueDate) ? dueDate : DateOnly.MaxValue)
                    .ThenByDescending(item => item.UpdatedAt)
                    .Take(120)
                    .ToList()
            }
        });
    }

    [HttpGet("matter-register")]
    public async Task<IActionResult> GetMatterRegister(
        [FromQuery] string? status,
        [FromQuery] string? entityType,
        [FromQuery] string? search,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (!CanReadLegal())
        {
            return Forbid();
        }

        var tenantId = _currentUser.TenantId;
        if (!tenantId.HasValue || tenantId == Guid.Empty)
        {
            return Unauthorized(new { success = false, message = "A tenant context is required." });
        }

        var procedures = _procedureCatalog.GetProcedures()
            .ToDictionary(item => item.EntityType, item => item.Title, StringComparer.OrdinalIgnoreCase);
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? "open" : status.Trim();
        var normalizedSearch = search?.Trim();
        var normalizedEntityType = entityType?.Trim();
        var take = Math.Clamp(pageSize, 1, 200);

        var cases = await _db.ProcedureCases
            .AsNoTracking()
            .Include(item => item.Fields.Where(field => !field.IsDeleted))
            .Where(item => item.TenantId == tenantId.Value
                && item.Module == "Legal"
                && !item.IsDeleted)
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Take(5000)
            .ToListAsync(cancellationToken);

        var rows = cases.Select(item =>
        {
            string? Value(string key) => FieldValue(item, key);
            var procedureTitle = procedures.TryGetValue(item.EntityType, out var title)
                ? title
                : item.EntityType;

            return new LegalMatterRegisterRow(
                item.Id,
                item.EntityType,
                procedureTitle,
                item.Title,
                item.ReferenceNumber,
                item.ApplicantName,
                item.Status,
                item.CurrentStageName,
                item.CurrentAssignedRole,
                item.CreatedAt,
                item.UpdatedAt,
                Value("referenceNumber"),
                Value("propertyFileReference"),
                Value("propertyNumber"),
                Value("assignedLegalOfficer"),
                Value("sourceDepartment"),
                Value("paymentStatus"),
                Value("signatureStatus"),
                Value("estateReturnStatus"),
                Value("litigationRisk") ?? Value("legalRiskLevel"),
                Value("responseDeadline"),
                Value("nextHearingDate"),
                Value("courtName"),
                Value("caseNumber"));
        }).ToList();

        rows = rows.Where(item =>
        {
            if (!string.IsNullOrWhiteSpace(normalizedEntityType)
                && !string.Equals(normalizedEntityType, "all", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(item.EntityType, normalizedEntityType, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(normalizedStatus, "open", StringComparison.OrdinalIgnoreCase) && !IsOpen(item.Status))
            {
                return false;
            }

            if (string.Equals(normalizedStatus, "completed", StringComparison.OrdinalIgnoreCase) && IsOpen(item.Status))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(normalizedSearch))
            {
                return true;
            }

            return item.SearchValues.Any(value =>
                value?.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) == true);
        }).ToList();

        return Ok(new
        {
            success = true,
            data = new
            {
                totalCount = rows.Count,
                items = rows.Take(take).ToList()
            }
        });
    }

    [HttpGet]
    public IActionResult GetProcedures()
    {
        return Ok(new
        {
            success = true,
            data = _procedureCatalog.GetProcedures()
        });
    }

    [HttpGet("search")]
    public IActionResult SearchProcedures([FromQuery] string? search, [FromQuery] int take = 5)
    {
        var term = search?.Trim() ?? string.Empty;
        var procedures = _procedureCatalog.GetProcedures()
            .Where(item => term.Length >= 2 && term.Length <= 100
                && (item.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || item.EntityType.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Take(Math.Clamp(take, 1, 10));
        return Ok(new { success = true, data = procedures });
    }

    [HttpGet("{entityType}")]
    public IActionResult GetProcedureWorkspace(string entityType)
    {
        var workspace = _procedureCatalog.GetProcedureWorkspace(entityType);

        if (workspace is null)
        {
            return NotFound(new
            {
                success = false,
                message = $"Legal procedure workspace '{entityType}' was not found."
            });
        }

        return Ok(new
        {
            success = true,
            data = workspace
        });
    }

    private bool CanReadLegal() =>
        _currentUser.Roles.Any(role => AllowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase));

    private static bool IsOpen(string status)
    {
        return !string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(status, "Canceled", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FieldValue(ErpSystem.Core.Entities.Procedures.ProcedureCase item, string key)
        => item.Fields.FirstOrDefault(field =>
            string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase))?.Value;

    private sealed record LegalMatterRegisterRow(
        Guid Id,
        string EntityType,
        string ProcedureTitle,
        string Title,
        string? ReferenceNumber,
        string? ApplicantName,
        string Status,
        string CurrentStageName,
        string? CurrentAssignedRole,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        string? MatterNumber,
        string? PropertyFileReference,
        string? PropertyNumber,
        string? AssignedLegalOfficer,
        string? SourceDepartment,
        string? PaymentStatus,
        string? SignatureStatus,
        string? EstateReturnStatus,
        string? Risk,
        string? ResponseDeadline,
        string? NextHearingDate,
        string? CourtName,
        string? CaseNumber)
    {
        [System.Text.Json.Serialization.JsonIgnore]
        public IReadOnlyList<string?> SearchValues =>
        [
            Title,
            ReferenceNumber,
            MatterNumber,
            ApplicantName,
            ProcedureTitle,
            CurrentStageName,
            CurrentAssignedRole,
            PropertyFileReference,
            PropertyNumber,
            AssignedLegalOfficer,
            SourceDepartment,
            PaymentStatus,
            SignatureStatus,
            EstateReturnStatus,
            Risk,
            CourtName,
            CaseNumber
        ];
    }

    private sealed record LegalDashboardQueueItem(
        Guid Id,
        string EntityType,
        string Title,
        string? ReferenceNumber,
        string? ApplicantName,
        string CurrentStageName,
        string? Owner,
        string StatusDetail,
        string? DueDate,
        string? Risk);

    private sealed record LegalSopControlItem(
        Guid Id,
        string EntityType,
        string ProcedureTitle,
        string Category,
        string Title,
        string? ReferenceNumber,
        string? ApplicantName,
        string CurrentStageName,
        string? Owner,
        string StatusDetail,
        string? DueDate,
        string? Reference,
        string? Risk,
        DateTime UpdatedAt);
}
