using ErpSystem.Core.Interfaces; // For IGeneralLedgerService
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.Services.Procurement;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.Finance
{
    /// <summary>
    /// Manages journal entries for posting transactions to the General Ledger.
    /// </summary>
    /// <remarks>
    /// This controller is the primary integration endpoint for all ERP modules to post financial transactions.
    /// All modules (Sales, Purchasing, Inventory, Payroll, Fixed Assets) use this controller to create
    /// journal entries that update account balances in the General Ledger.
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/finance/journal-entries")]
    public class JournalEntryController : ControllerBase
    {
        private readonly IJournalEntryService _journalEntryService;
        private readonly IGeneralLedgerService _generalLedgerService;
        private readonly IWorkflowService _workflowService;
        private readonly IWorkflowIntegrationService _workflowIntegration;
        private readonly ICurrentUserService _currentUserService;
        private readonly IFinanceAuditService _financeAuditService;
        private readonly ApplicationDbContext _dbContext;
        private readonly IFinanceBudgetControlService _budgetControl;

        public JournalEntryController(
            IJournalEntryService journalEntryService,
            IGeneralLedgerService generalLedgerService,
            IWorkflowService workflowService,
            ICurrentUserService currentUserService,
            IFinanceAuditService financeAuditService,
            ApplicationDbContext dbContext,
            IFinanceBudgetControlService budgetControl,
            IWorkflowIntegrationService? workflowIntegration = null)
        {
            _journalEntryService = journalEntryService;
            _generalLedgerService = generalLedgerService;
            _workflowService = workflowService;
            _workflowIntegration = workflowIntegration ?? new ErpSystem.Core.Services.Workflow.WorkflowIntegrationService(
                workflowService,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ErpSystem.Core.Services.Workflow.WorkflowIntegrationService>.Instance);
            _currentUserService = currentUserService;
            _financeAuditService = financeAuditService;
            _dbContext = dbContext;
            _budgetControl = budgetControl;
        }

        private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();

        /// <summary>
        /// Gets business audit events recorded for this journal entry.
        /// </summary>
        [HttpGet("{id}/audit-trail")]
        public async Task<ActionResult<IReadOnlyList<FinanceJournalAuditLogDto>>> GetJournalEntryAuditTrail(Guid id)
        {
            try
            {
                if (!await HasAnyPermissionAsync(
                        "Finance.JournalEntries.Create",
                        "Finance.JournalEntries.Edit",
                        "Finance.JournalEntries.Write",
                        "Finance.JournalEntries.SubmitForApproval",
                        "Finance.JournalEntries.Approve",
                        "Finance.JournalEntries.Post",
                        "Finance.JournalEntries.Reverse"))
                    return Forbid();

                var tenantId = TenantId;
                var exists = await _dbContext.JournalEntries
                    .AnyAsync(j => j.Id == id && j.TenantId == tenantId && !j.IsDeleted);

                if (!exists)
                    return NotFound($"Journal entry with ID {id} not found");

                var auditLogs = await _financeAuditService.GetAuditTrailAsync(
                    tenantId,
                    "Finance.JournalEntry",
                    id.ToString(),
                    limit: 100);

                return Ok(auditLogs.Select(MapAuditLogToDto).ToList());
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private static readonly string[] PrivilegedRoles = { "SuperAdmin", "TenantAdmin" };

        private async Task<bool> HasAnyPermissionAsync(params string[] requiredPermissions)
        {
            if (requiredPermissions.Length == 0) return true;
            if (PrivilegedRoles.Any(_currentUserService.IsInRole)) return true;
            if (!Guid.TryParse(_currentUserService.UserId, out var userId)) return false;

            var userPermissions = await _dbContext.UserRoles
                .Where(ur => ur.UserId == userId)
                .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Name))
                .ToListAsync();

            return userPermissions.Any(p => requiredPermissions.Contains(p, StringComparer.OrdinalIgnoreCase));
        }

        private async Task<bool> CanViewJournalEntryAsync(Guid journalEntryId)
        {
            if (await HasAnyPermissionAsync(FinancePermissions.ViewFinance))
                return true;

            if (!await HasAnyPermissionAsync(ProcurementAccessControlRegistry.TenderPaymentVerifyPermission))
                return false;

            var tenantId = TenantId;
            return await _dbContext.TenderPayments
                .AsNoTracking()
                .AnyAsync(payment =>
                    payment.TenantId == tenantId &&
                    !payment.IsDeleted &&
                    payment.JournalEntryId == journalEntryId);
        }

        private async Task<ConflictObjectResult?> GetBatchOwnershipConflictAsync(Guid journalEntryId)
        {
            var ownership = await _dbContext.JournalBatchItems
                .AsNoTracking()
                .Where(x =>
                    x.TenantId == TenantId &&
                    x.JournalEntryId == journalEntryId &&
                    !x.IsDeleted &&
                    !x.JournalBatch.IsDeleted)
                .Select(x => new
                {
                    BatchId = x.JournalBatchId,
                    x.JournalBatch.BatchNumber
                })
                .FirstOrDefaultAsync();

            return ownership == null
                ? null
                : Conflict(new
                {
                    code = "JOURNAL_BATCH_OWNED",
                    message = $"This journal belongs to batch {ownership.BatchNumber}. Edit, approve, post, or reverse it from the journal batch.",
                    ownership.BatchId,
                    ownership.BatchNumber
                });
        }

        private static readonly WorkflowInstanceStatus[] ActiveWorkflowStatuses =
        {
            WorkflowInstanceStatus.Created,
            WorkflowInstanceStatus.InProgress,
            WorkflowInstanceStatus.Waiting,
            WorkflowInstanceStatus.Suspended
        };

        private const string JournalWorkflowEntityType = "JournalEntry";
        private const string DeltaAdjustmentWorkflowEntityType = "DeltaAdjustmentJournal";
        private const string DeltaAdjustmentJournalType = "Delta Adjustment";

        private static string GetWorkflowEntityType(string? journalType) =>
            string.Equals(journalType, DeltaAdjustmentJournalType, StringComparison.OrdinalIgnoreCase)
                ? DeltaAdjustmentWorkflowEntityType
                : JournalWorkflowEntityType;

        private async Task<string> GetWorkflowEntityTypeAsync(Guid journalEntryId)
        {
            var journalType = await _dbContext.JournalEntries.AsNoTracking()
                .Where(item => item.TenantId == TenantId && item.Id == journalEntryId && !item.IsDeleted)
                .Select(item => item.JournalType)
                .SingleAsync();
            return GetWorkflowEntityType(journalType);
        }

        private async Task<bool> HasActiveJournalWorkflowAsync(Guid journalEntryId)
        {
            var tenantId = TenantId;
            var workflowEntityType = await GetWorkflowEntityTypeAsync(journalEntryId);
            return await _dbContext.WorkflowInstances
                .AnyAsync(i =>
                    i.TenantId == tenantId &&
                    i.EntityId == journalEntryId &&
                    ActiveWorkflowStatuses.Contains(i.Status) &&
                    i.EntityType.Code == workflowEntityType);
        }

        private async Task<bool> CanCurrentUserApproveJournalWorkflowAsync(Guid journalEntryId, Guid userId)
        {
            if (!await HasActiveJournalWorkflowAsync(journalEntryId))
                return true;

            return await _workflowService.CanUserApproveAsync(await GetWorkflowEntityTypeAsync(journalEntryId), journalEntryId, userId);
        }

        private async Task<HashSet<Guid>> GetWorkflowAssignedJournalIdsAsync(Guid userId)
        {
            var tenantId = TenantId;
            var userRoles = (_currentUserService.Roles ?? Array.Empty<string>()).ToList();

            var assignedIds = await _dbContext.WorkflowInstances
                .Where(i =>
                    i.TenantId == tenantId &&
                    ActiveWorkflowStatuses.Contains(i.Status) &&
                    (i.EntityType.Code == JournalWorkflowEntityType ||
                     i.EntityType.Code == DeltaAdjustmentWorkflowEntityType) &&
                    i.StepInstances.Any(si =>
                        (si.Status == WorkflowStepInstanceStatus.Pending ||
                         si.Status == WorkflowStepInstanceStatus.InProgress) &&
                        (si.AssignedToId == userId ||
                         si.Approvals.Any(a =>
                             a.Status == WorkflowApprovalStatus.Pending &&
                             (a.ApproverId == userId ||
                              (a.ApproverRole != null && userRoles.Contains(a.ApproverRole)))))))
                .Select(i => i.EntityId)
                .Distinct()
                .ToListAsync();

            return assignedIds.ToHashSet();
        }

        private async Task<HashSet<Guid>> GetJournalIdsWithActiveWorkflowAsync(IEnumerable<Guid> journalEntryIds)
        {
            var ids = journalEntryIds.ToList();
            if (ids.Count == 0)
                return new HashSet<Guid>();

            var tenantId = TenantId;
            var workflowIds = await _dbContext.WorkflowInstances
                .Where(i =>
                    i.TenantId == tenantId &&
                    ids.Contains(i.EntityId) &&
                    ActiveWorkflowStatuses.Contains(i.Status) &&
                    (i.EntityType.Code == JournalWorkflowEntityType ||
                     i.EntityType.Code == DeltaAdjustmentWorkflowEntityType))
                .Select(i => i.EntityId)
                .Distinct()
                .ToListAsync();

            return workflowIds.ToHashSet();
        }

        // ====================================================================
        // CORE JOURNAL ENTRY ENDPOINTS
        // ====================================================================

        /// <summary>
        /// Generates the next sequential journal entry number.
        /// </summary>
        [HttpGet("next-number")]
        public async Task<ActionResult<string>> GetNextJournalNumber()
        {
            try
            {
                var number = await _generalLedgerService.GenerateJournalEntryNumberAsync();
                return Ok(new { number });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves journal entries with optional filtering by status, date range, fiscal period, and source module.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<JournalEntryDto>>> GetJournalEntries(
            [FromQuery] string? status = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] Guid? periodId = null,
            [FromQuery] Guid? fiscalPeriodId = null,
            [FromQuery] string? sourceModule = null)
        {
            try
            {
                var entries = await _journalEntryService.GetJournalEntriesAsync(
                    status,
                    startDate,
                    endDate,
                    fiscalPeriodId ?? periodId,
                    sourceModule);
                return Ok(entries);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves journal entries awaiting approval for the current finance approver.
        /// </summary>
        [HttpGet("pending-approvals")]
        public async Task<ActionResult<List<JournalEntryDto>>> GetPendingApprovals()
        {
            try
            {
                if (!await HasAnyPermissionAsync("Finance.JournalEntries.Approve"))
                    return Forbid();

                Guid.TryParse(_currentUserService.UserId, out var userId);
                var entries = await _journalEntryService.GetJournalEntriesAsync();
                var pendingEntries = entries
                    .Where(e => e.PostingStatus == "Pending Approval")
                    .ToList();
                var assignedWorkflowJournalIds = await GetWorkflowAssignedJournalIdsAsync(userId);
                var activeWorkflowJournalIds = await GetJournalIdsWithActiveWorkflowAsync(pendingEntries.Select(e => e.Id));

                var pending = pendingEntries
                    .Where(e =>
                        assignedWorkflowJournalIds.Contains(e.Id) ||
                        (!activeWorkflowJournalIds.Contains(e.Id) &&
                         (!e.CreatedById.HasValue || e.CreatedById.Value != userId)))
                    .OrderBy(e => e.TransactionDate)
                    .ToList();

                return Ok(pending);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Retrieves a specific journal entry by ID including all transaction lines.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<JournalEntryDto>> GetJournalEntryById(Guid id)
        {
            try
            {
                if (!await CanViewJournalEntryAsync(id))
                    return Forbid();

                var entry = await _journalEntryService.GetJournalEntryByIdAsync(id);
                if (entry == null)
                    return NotFound($"Journal entry with ID {id} not found");

                return Ok(entry);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a new journal entry to post transactions to the General Ledger.
        /// This is the primary integration endpoint for all ERP modules.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<JournalEntryDto>> CreateJournalEntry([FromBody] CreateJournalEntryDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var entry = await _journalEntryService.CreateJournalEntryAsync(dto);
                return CreatedAtAction(nameof(GetJournalEntryById), new { id = entry.Id }, entry);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates a draft journal entry before it is posted.
        /// Can only update entries with status="Draft".
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<JournalEntryDto>> UpdateJournalEntry(Guid id, [FromBody] UpdateJournalEntryDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                if (!await HasAnyPermissionAsync("Finance.JournalEntries.Edit", "Finance.JournalEntries.Write"))
                    return Forbid();

                var ownershipConflict = await GetBatchOwnershipConflictAsync(id);
                if (ownershipConflict != null)
                    return ownershipConflict;

                var entry = await _journalEntryService.UpdateJournalEntryAsync(id, dto);
                return Ok(entry);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes a draft journal entry. Can only delete entries with status="Draft".
        /// For posted entries, use the reverse endpoint instead.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteJournalEntry(Guid id)
        {
            try
            {
                if (!await HasAnyPermissionAsync("Finance.JournalEntries.Delete", "Finance.JournalEntries.Write"))
                    return Forbid();

                var ownershipConflict = await GetBatchOwnershipConflictAsync(id);
                if (ownershipConflict != null)
                    return ownershipConflict;

                await _journalEntryService.DeleteJournalEntryAsync(id);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Posts a draft or approved journal entry to the General Ledger.
        /// Entry must be balanced and fiscal period must be open.
        /// </summary>
        [HttpPost("{id}/post")]
        public async Task<ActionResult> PostJournalEntry(Guid id)
        {
            try
            {
                if (!await HasAnyPermissionAsync("Finance.JournalEntries.Post"))
                    return Forbid();

                var ownershipConflict = await GetBatchOwnershipConflictAsync(id);
                if (ownershipConflict != null)
                    return ownershipConflict;

                await _journalEntryService.PostJournalEntryAsync(id);
                return Ok(new { message = "Journal entry posted successfully" });
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Reverses a posted journal entry by creating an offsetting entry with opposite debits/credits.
        /// Can only reverse posted entries that haven't already been reversed.
        /// </summary>
        [HttpPost("{id}/reverse")]
        public async Task<ActionResult<JournalEntryDto>> ReverseJournalEntry(Guid id, [FromBody] ReverseJournalEntryDto request)
        {
            try
            {
                if (!await HasAnyPermissionAsync("Finance.JournalEntries.Reverse"))
                    return Forbid();

                var ownershipConflict = await GetBatchOwnershipConflictAsync(id);
                if (ownershipConflict != null)
                    return ownershipConflict;

                if (request == null || string.IsNullOrWhiteSpace(request.Reason))
                    return BadRequest("A reversal reason is required.");

                var reversedEntry = await _journalEntryService.ReverseJournalEntryAsync(id, request.Reason, request.ReversalDate);
                return Ok(reversedEntry);
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // ====================================================================
        // APPROVAL WORKFLOW ENDPOINTS
        // ====================================================================

        [HttpGet("{id}/budget-control")]
        public async Task<ActionResult<FinanceBudgetControlEvaluationDto>> GetBudgetControl(Guid id)
        {
            try
            {
                if (!await HasAnyPermissionAsync("Finance.JournalEntries.Create", "Finance.JournalEntries.Write", "Finance.JournalEntries.SubmitForApproval", "Finance.JournalEntries.Approve"))
                    return Forbid();
                return Ok(await _budgetControl.EvaluateManualJournalAsync(id));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{id}/budget-override")]
        public async Task<ActionResult<FinanceBudgetOverrideRequestDto>> RequestBudgetOverride(
            Guid id,
            [FromBody] FinanceBudgetOverrideCommandDto request)
        {
            try
            {
                if (!await HasAnyPermissionAsync("Finance.JournalEntries.SubmitForApproval"))
                    return Forbid();
                return Ok(await _budgetControl.RequestManualJournalOverrideAsync(id, request.Reason));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Submits a draft journal entry for approval. Transitions from "Draft" to "Pending Approval".
        /// </summary>
        [HttpPost("{id}/request-approval")]
        public async Task<ActionResult<JournalEntryDto>> RequestApproval(Guid id)
        {
            try
            {
                if (!await HasAnyPermissionAsync("Finance.JournalEntries.SubmitForApproval", "Finance.JournalEntries.Approve"))
                    return Forbid();

                var ownershipConflict = await GetBatchOwnershipConflictAsync(id);
                if (ownershipConflict != null)
                    return ownershipConflict;

                var entry = await _journalEntryService.GetJournalEntryByIdAsync(id);
                if (entry == null)
                    return NotFound($"Journal entry with ID {id} not found");

                if (entry.PostingStatus != "Draft")
                    return BadRequest($"Only draft journal entries can be submitted for approval. Current status: {entry.PostingStatus}");

                await _journalEntryService.ValidateJournalEntryReadyForSubmissionAsync(id);
                var workflowEntityType = GetWorkflowEntityType(entry.JournalType);

                // Reserve before starting the approval workflow so concurrent journals cannot
                // spend the same adopted budget while they wait in the approval queue.
                await _budgetControl.ReserveManualJournalAsync(id);

                WorkflowExecutionResult workflowResult;
                WorkflowIntegrationResult submission;
                try
                {
                    submission = await _workflowIntegration.SubmitAsync(workflowEntityType, id);
                    workflowResult = submission.ExecutionResult;
                }
                catch
                {
                    await _budgetControl.ReleaseManualJournalAsync(id, "Journal approval workflow failed to start.");
                    throw;
                }
                if (!workflowResult.Success || (!submission.ApprovalRequired &&
                    (submission.Outcome != WorkflowOutcome.Approved || workflowResult.WorkflowInstanceId.HasValue)))
                {
                    await _budgetControl.ReleaseManualJournalAsync(id, workflowResult.Message ?? "Journal workflow did not start.");
                    return BadRequest(!workflowResult.Success
                        ? workflowResult.Message ?? "Unable to start approval workflow."
                        : "The no-approval decision is inconsistent. Refresh and retry submission.");
                }
                if (workflowEntityType == DeltaAdjustmentWorkflowEntityType && !submission.ApprovalRequired)
                {
                    await _budgetControl.ReleaseManualJournalAsync(id, "Delta adjustment approval workflow is required.");
                    return BadRequest("A published DeltaAdjustmentJournal approval workflow is required; Delta adjustments cannot be auto-approved.");
                }

                var currentWorkflowStep = submission.ApprovalRequired
                    ? await _workflowService.GetCurrentWorkflowStepAsync(workflowEntityType, id) : null;
                if (submission.ApprovalRequired && (currentWorkflowStep == null ||
                    string.Equals(currentWorkflowStep.StepName, "Draft", StringComparison.OrdinalIgnoreCase)))
                {
                    await _workflowService.CancelWorkflowAsync(workflowEntityType, id, "Journal workflow did not advance to an approval step.");
                    await _budgetControl.ReleaseManualJournalAsync(id, "Journal workflow did not advance to an approval step.");
                    return BadRequest("Approval workflow did not advance to the approval step. Journal entry was not submitted.");
                }

                // Update the entry status. If this final step fails, cancel the just-started
                // workflow and release its budget commitment instead of leaving split state.
                try
                {
                    await _journalEntryService.UpdateApprovalStatusAsync(id,
                        submission.ApprovalRequired ? "Pending Approval" : "Approved",
                        submission.ApprovalRequired ? "Pending" : "Not Required");
                }
                catch
                {
                    if (submission.ApprovalRequired)
                        await _workflowService.CancelWorkflowAsync(workflowEntityType, id, "Journal submission failed after workflow start.");
                    await _budgetControl.ReleaseManualJournalAsync(id, "Journal submission failed after workflow start.");
                    throw;
                }

                var updated = await _journalEntryService.GetJournalEntryByIdAsync(id);
                return Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Withdraws a pending approval request and returns the journal entry to Draft.
        /// </summary>
        [HttpPost("{id}/withdraw-approval")]
        public async Task<ActionResult<JournalEntryDto>> WithdrawApproval(
            Guid id,
            [FromBody] WithdrawApprovalDto? request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (!await HasAnyPermissionAsync(
                        FinancePermissions.SubmitJournalEntries,
                        FinancePermissions.WorkflowCancel))
                    return Forbid();

                if (!Guid.TryParse(_currentUserService.UserId, out var currentUserId))
                    return Forbid();

                var ownershipConflict = await GetBatchOwnershipConflictAsync(id);
                if (ownershipConflict != null)
                    return ownershipConflict;

                var entry = await _journalEntryService.GetJournalEntryByIdAsync(id);
                if (entry == null)
                    return NotFound($"Journal entry with ID {id} not found");

                if (!string.Equals(entry.PostingStatus, "Pending Approval", StringComparison.OrdinalIgnoreCase))
                    return BadRequest("Only journal entries pending approval can be withdrawn.");

                var reason = request?.Reason?.Trim();
                if (string.IsNullOrWhiteSpace(reason))
                    return BadRequest("A withdrawal reason is required.");

                var canCancelAnyWorkflow = await HasAnyPermissionAsync(FinancePermissions.WorkflowCancel);
                var workflowEntityType = GetWorkflowEntityType(entry.JournalType);

                async Task<WorkflowExecutionResult> CancelWorkflowAndReturnToDraftAsync()
                {
                    var workflowResult = canCancelAnyWorkflow
                        ? await _workflowService.CancelWorkflowAsync(workflowEntityType, id, reason)
                        : await _workflowService.RecallWorkflowAsync(workflowEntityType, id, currentUserId, reason);

                    if (!workflowResult.Success)
                        return workflowResult;

                    await _journalEntryService.WithdrawApprovalAsync(
                        id,
                        currentUserId,
                        reason,
                        cancellationToken);
                    return workflowResult;
                }

                WorkflowExecutionResult result;
                if (!_dbContext.Database.IsRelational() || _dbContext.Database.CurrentTransaction != null)
                {
                    result = await CancelWorkflowAndReturnToDraftAsync();
                }
                else
                {
                    var strategy = _dbContext.Database.CreateExecutionStrategy();
                    result = await strategy.ExecuteAsync(async () =>
                    {
                        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
                        try
                        {
                            var workflowResult = await CancelWorkflowAndReturnToDraftAsync();
                            if (!workflowResult.Success)
                            {
                                await transaction.RollbackAsync(cancellationToken);
                                _dbContext.ChangeTracker.Clear();
                                return workflowResult;
                            }

                            await transaction.CommitAsync(cancellationToken);
                            return workflowResult;
                        }
                        catch
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            _dbContext.ChangeTracker.Clear();
                            throw;
                        }
                    });
                }

                if (!result.Success)
                    return BadRequest(result.Message ?? "Unable to withdraw the active approval workflow.");

                var updated = await _journalEntryService.GetJournalEntryByIdAsync(id);
                return Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Approves a journal entry that is pending approval. Transitions to "Approved".
        /// Entry can then be posted via the POST endpoint.
        /// </summary>
        [HttpPost("{id}/approve")]
        public async Task<ActionResult<JournalEntryDto>> ApproveJournalEntry(Guid id, [FromBody] ApprovalActionDto? request = null)
        {
            try
            {
                if (!await HasAnyPermissionAsync("Finance.JournalEntries.Approve"))
                    return Forbid();

                var ownershipConflict = await GetBatchOwnershipConflictAsync(id);
                if (ownershipConflict != null)
                    return ownershipConflict;

                var entry = await _journalEntryService.GetJournalEntryByIdAsync(id);
                if (entry == null)
                    return NotFound($"Journal entry with ID {id} not found");

                if (entry.PostingStatus != "Pending Approval")
                    return BadRequest($"Only entries pending approval can be approved. Current status: {entry.PostingStatus}");

                Guid.TryParse(_currentUserService.UserId, out var userId);
                var workflowEntityType = GetWorkflowEntityType(entry.JournalType);

                var hasActiveWorkflow = await HasActiveJournalWorkflowAsync(id);
                if (!hasActiveWorkflow)
                    return BadRequest("No active approval workflow was found for this journal entry. Withdraw and resubmit the journal entry for approval.");

                if (!await CanCurrentUserApproveJournalWorkflowAsync(id, userId))
                    return StatusCode(403, "This journal entry is assigned to another workflow approver.");

                // The direct endpoint is retained for compatibility. Revalidate before advancing
                // the workflow so a changed budget position cannot complete the workflow first
                // and only then fail the journal status update.
                await _budgetControl.ValidateManualJournalForPostingAsync(id);

                var workflowResult = await _workflowService.ProcessApprovalStepAsync(workflowEntityType, id, userId, "Approve", request?.Comments);
                if (!workflowResult.Success)
                    return BadRequest(workflowResult.Message ?? "Unable to process workflow approval.");

                if (workflowResult.Status == WorkflowInstanceStatus.Completed)
                {
                    await _journalEntryService.UpdateApprovalStatusAsync(id, "Approved", "Approved", userId);
                }

                var updated = await _journalEntryService.GetJournalEntryByIdAsync(id);
                return Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Rejects a journal entry that is pending approval. A rejection reason is required.
        /// Entry can be edited and resubmitted after correction.
        /// </summary>
        [HttpPost("{id}/reject")]
        public async Task<ActionResult<JournalEntryDto>> RejectJournalEntry(Guid id, [FromBody] ApprovalActionDto request)
        {
            try
            {
                if (!await HasAnyPermissionAsync("Finance.JournalEntries.Approve"))
                    return Forbid();

                var ownershipConflict = await GetBatchOwnershipConflictAsync(id);
                if (ownershipConflict != null)
                    return ownershipConflict;

                if (string.IsNullOrWhiteSpace(request?.Reason))
                    return BadRequest("A rejection reason is required.");

                var entry = await _journalEntryService.GetJournalEntryByIdAsync(id);
                if (entry == null)
                    return NotFound($"Journal entry with ID {id} not found");

                if (entry.PostingStatus != "Pending Approval")
                    return BadRequest($"Only entries pending approval can be rejected. Current status: {entry.PostingStatus}");

                Guid.TryParse(_currentUserService.UserId, out var userId);
                var workflowEntityType = GetWorkflowEntityType(entry.JournalType);

                var hasActiveWorkflow = await HasActiveJournalWorkflowAsync(id);
                if (!hasActiveWorkflow)
                    return BadRequest("No active approval workflow was found for this journal entry. Withdraw and resubmit the journal entry for approval.");

                if (!await CanCurrentUserApproveJournalWorkflowAsync(id, userId))
                    return StatusCode(403, "This journal entry is assigned to another workflow approver.");

                var workflowResult = await _workflowService.ProcessApprovalStepAsync(workflowEntityType, id, userId, "Reject", request.Reason);
                if (!workflowResult.Success)
                    return BadRequest(workflowResult.Message ?? "Unable to process workflow rejection.");

                if (workflowResult.Status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.Failed)
                {
                    await _journalEntryService.UpdateApprovalStatusAsync(id, "Rejected", "Rejected", rejectionReason: request.Reason);
                }

                var updated = await _journalEntryService.GetJournalEntryByIdAsync(id);
                return Ok(updated);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Links an uploaded file to a journal entry.
        /// </summary>
        [HttpPost("{id}/attachments/{fileUploadRecordId}")]
        public async Task<IActionResult> LinkAttachment(Guid id, Guid fileUploadRecordId)
        {
            try
            {
                var ownershipConflict = await GetBatchOwnershipConflictAsync(id);
                if (ownershipConflict != null)
                    return ownershipConflict;

                await _journalEntryService.LinkAttachmentAsync(id, fileUploadRecordId);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Unlinks an attachment from a journal entry.
        /// </summary>
        [HttpDelete("{id}/attachments/{fileUploadRecordId}")]
        public async Task<IActionResult> UnlinkAttachment(Guid id, Guid fileUploadRecordId)
        {
            try
            {
                var ownershipConflict = await GetBatchOwnershipConflictAsync(id);
                if (ownershipConflict != null)
                    return ownershipConflict;

                await _journalEntryService.UnlinkAttachmentAsync(id, fileUploadRecordId);
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets attachment metadata linked to a journal entry.
        /// </summary>
        [HttpGet("{id}/attachments")]
        public async Task<ActionResult<IReadOnlyList<JournalEntryAttachmentDto>>> GetAttachments(Guid id)
        {
            try
            {
                if (!await CanViewJournalEntryAsync(id))
                    return Forbid();

                var attachments = await _journalEntryService.GetAttachmentsAsync(id);
                return Ok(attachments);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private static FinanceJournalAuditLogDto MapAuditLogToDto(ErpSystem.Core.Entities.AuditLog auditLog)
        {
            return new FinanceJournalAuditLogDto
            {
                Id = auditLog.Id,
                Action = auditLog.Action,
                Resource = auditLog.Resource,
                ResourceId = auditLog.ResourceId,
                Username = auditLog.Username,
                UserId = auditLog.UserId,
                Timestamp = auditLog.Timestamp,
                IpAddress = auditLog.IpAddress,
                UserAgent = auditLog.UserAgent,
                OldValues = DeserializeAuditValues(auditLog.OldValues),
                NewValues = DeserializeAuditValues(auditLog.NewValues)
            };
        }

        private static object? DeserializeAuditValues(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonSerializer.Deserialize<object>(json);
            }
            catch
            {
                return json;
            }
        }
    }

    public class FinanceJournalAuditLogDto
    {
        public Guid Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Resource { get; set; } = string.Empty;
        public string? ResourceId { get; set; }
        public string Username { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public DateTime Timestamp { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string? UserAgent { get; set; }
        public object? OldValues { get; set; }
        public object? NewValues { get; set; }
    }
}
