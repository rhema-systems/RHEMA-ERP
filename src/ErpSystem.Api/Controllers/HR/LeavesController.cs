using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Models;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR
{
    /// <summary>
    /// Leave management endpoints
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "InternalOnly")]
    public class LeavesController : ControllerBase
    {
        private readonly ILeaveService _leaveService;
        private readonly ILeaveBalanceRecalculationService _recalculationService;
        private readonly IFileStorageService _fileStorageService;
        private readonly IHrControlledDocumentService _hrDocuments;
        private readonly ICentralDocumentRepositoryFileService _centralDocuments;
        private readonly ApplicationDbContext _db;
        private readonly ICurrentUserService _currentUserService;
        private readonly IWorkflowIntegrationService _workflowIntegrationService;
        private readonly IAuthorizationService _authorization;
        private readonly ILeaveYearContext _leaveYear;
        private readonly ILogger<LeavesController> _logger;

        public LeavesController(
            ILeaveService leaveService,
            ILeaveBalanceRecalculationService recalculationService,
            IFileStorageService fileStorageService,
            IHrControlledDocumentService hrDocuments,
            ICentralDocumentRepositoryFileService centralDocuments,
            ApplicationDbContext db,
            ICurrentUserService currentUserService,
            IWorkflowIntegrationService workflowIntegrationService,
            IAuthorizationService authorization,
            ILeaveYearContext leaveYear,
            ILogger<LeavesController> logger)
        {
            _leaveService = leaveService;
            _recalculationService = recalculationService;
            _fileStorageService = fileStorageService;
            _hrDocuments = hrDocuments;
            _centralDocuments = centralDocuments;
            _db = db;
            _currentUserService = currentUserService;
            _workflowIntegrationService = workflowIntegrationService;
            _authorization = authorization;
            _leaveYear = leaveYear;
            _logger = logger;
        }

        /// <summary>
        /// The caller satisfies the given leave policy — evaluated through the policy pipeline,
        /// so database grants and the HR role-fallback both count.
        /// </summary>
        private async Task<bool> HoldsLeavePolicyAsync(string policy)
            => (await _authorization.AuthorizeAsync(User, policy)).Succeeded;

        /// <summary>
        /// Self-or-permission (W3 slice 5): the caller is the employee the record is about, or
        /// holds the given leave policy. Deliberately NOT self-or-manager — a line manager's
        /// part in leave is approval, which reaches them through the workflow engine's own
        /// assignee check, not through read access to the report's file.
        /// </summary>
        private async Task<bool> CanActForEmployeeAsync(Guid employeeId, string policy)
        {
            if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty && me == employeeId)
                return true;
            return await HoldsLeavePolicyAsync(policy);
        }

        /// <summary>Self-or-permission resolved through the leave request's owner.</summary>
        private async Task<bool> CanActOnRequestAsync(Guid leaveRequestId, string policy)
        {
            if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty &&
                _currentUserService.TenantId is Guid tenantId)
            {
                var mine = await _db.Set<Core.Entities.HR.StaffLeave.LeaveRequest>()
                    .AsNoTracking()
                    .AnyAsync(r => r.Id == leaveRequestId && r.TenantId == tenantId && r.EmployeeId == me);
                if (mine) return true;
            }
            return await HoldsLeavePolicyAsync(policy);
        }

        /// <summary>
        /// Self-or-read-tier resolved through the balance's owner (round 5, lane C2): the employee reads
        /// their own balance's accrual statement, the leave read tier anybody's.
        /// </summary>
        private async Task<bool> CanReadBalanceAsync(Guid balanceId)
        {
            if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty &&
                _currentUserService.TenantId is Guid tenantId)
            {
                var mine = await _db.Set<Core.Entities.HR.StaffLeave.LeaveBalance>()
                    .AsNoTracking()
                    .AnyAsync(b => b.Id == balanceId && b.TenantId == tenantId && b.EmployeeId == me);
                if (mine) return true;
            }
            return await HoldsLeavePolicyAsync(HrPermissions.LeaveReadPolicy);
        }

        /// <summary>
        /// Self-or-permission, OR the person the workflow engine is currently asking to decide.
        /// </summary>
        /// <remarks>
        /// <para><b>Why the third arm exists.</b> The read gates here were written as deliberately
        /// NOT self-or-manager, on the reasoning that a line manager's part in leave is approval and
        /// that reaches them through the engine's own assignee check rather than through read access
        /// to a report's file. That was coherent while the Manager stage was optional and HR did the
        /// approving in practice.</para>
        ///
        /// <para>The two-stage ladder made it incoherent: stage 1 routes to the <c>Manager</c> role,
        /// which holds no HR permission at all, so the approver could act on a request they could not
        /// open — and the approvals queue listed it for them, so the screen offered a row that 403'd
        /// on click. Found by the hr-leave suite, which asserted that the person who is supposed to
        /// do the thing can.</para>
        ///
        /// <para><b>It is narrower than granting the role a permission.</b> This opens exactly one
        /// request, to exactly the person being asked to decide it, for exactly as long as it is at
        /// their step — the engine answers false the moment the request moves on. Granting
        /// <c>HR.Leave.Read</c> to <c>Manager</c> would instead open every employee's whole leave
        /// history to every manager, permanently.</para>
        /// </remarks>
        private async Task<bool> CanReadRequestAsync(Guid leaveRequestId)
        {
            if (await CanActOnRequestAsync(leaveRequestId, HrPermissions.LeaveReadPolicy))
                return true;

            // Round 5, lane D: the employee's supervisor or head of department may now recall them
            // and confirm their return, so they must be able to open the leave they act on. The same
            // line authority the Team calendar already reads down, for their own people only.
            if (await IsLineAuthorityForRequestAsync(leaveRequestId))
                return true;

            if (!Guid.TryParse(_currentUserService.UserId, out var userId) || userId == Guid.Empty)
                return false;

            return await _workflowIntegrationService.CanUserApproveAsync(
                "LeaveRequest", leaveRequestId, userId);
        }

        /// <summary>
        /// Whether the caller is the request's employee's supervisor or head of department (round 5,
        /// B1/B3). The rule lives in <c>ILeaveService.IsLineAuthorityAsync</c>.
        /// </summary>
        private async Task<bool> IsLineAuthorityForRequestAsync(Guid leaveRequestId)
        {
            if (_currentUserService.EmployeeId is not Guid me || me == Guid.Empty ||
                _currentUserService.TenantId is not Guid tenantId)
                return false;

            var subject = await _db.Set<Core.Entities.HR.StaffLeave.LeaveRequest>()
                .AsNoTracking()
                .Where(r => r.Id == leaveRequestId && r.TenantId == tenantId)
                .Select(r => (Guid?)r.EmployeeId)
                .FirstOrDefaultAsync();

            return subject is Guid employeeId && await _leaveService.IsLineAuthorityAsync(employeeId, me);
        }

        /// <summary>
        /// Create a new leave request
        /// </summary>
        /// <param name="dto">Leave request data</param>
        /// <returns>Created leave request</returns>
        /// <response code="201">Leave request created successfully</response>
        /// <response code="400">Invalid request data or business rule violation</response>
        [HttpPost]
        [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<LeaveRequestDto>> CreateLeaveRequest([FromBody] CreateLeaveRequestDto dto)
        {
            // W3: an employee files their OWN leave; filing for someone else is the HR desk.
            if (!await CanActForEmployeeAsync(dto.EmployeeId, HrPermissions.LeaveWritePolicy))
                return Forbid();

            try
            {
                var application = await _leaveService.CreateLeaveRequestAsync(dto);
                return CreatedAtAction(nameof(GetLeaveApplicationById), new { id = application.Id }, application);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating leave request");
                return StatusCode(500, "An error occurred while creating leave request");
            }
        }

        /// <summary>
        /// What a request for these dates would cost its leave type, and whether the days beyond
        /// the type's limit could be charged to annual leave (round 5, lane H). Saves nothing.
        /// </summary>
        [HttpGet("excess-preview")]
        [ProducesResponseType(typeof(LeaveExcessPreviewDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveExcessPreviewDto>> PreviewExcess(
            [FromQuery] Guid employeeId,
            [FromQuery] Guid leaveTypeId,
            [FromQuery] DateOnly startDate,
            [FromQuery] DateOnly endDate,
            [FromQuery] Guid? leaveSubTypeId = null)
        {
            // The same door as the balances it reads: one's own, or the leave read tier.
            if (!await CanActForEmployeeAsync(employeeId, HrPermissions.LeaveReadPolicy))
                return Forbid();

            try
            {
                return Ok(await _leaveService.PreviewExcessAsync(
                    employeeId, leaveTypeId, leaveSubTypeId, startDate, endDate));
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Update an existing draft leave request
        /// </summary>
        [HttpPut("{id:guid}/draft")]
        [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveRequestDto>> UpdateDraft(Guid id, [FromBody] CreateLeaveRequestDto dto)
        {
            // W3: the draft's owner, or the HR desk.
            if (!await CanActOnRequestAsync(id, HrPermissions.LeaveWritePolicy))
                return Forbid();

            try
            {
                var result = await _leaveService.UpdateDraftAsync(id, dto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating draft leave request {Id}", id);
                return StatusCode(500, "An error occurred while updating the draft leave request");
            }
        }

        /// <summary>
        /// Get leave application by ID
        /// </summary>
        /// <param name="id">Leave application ID</param>
        /// <returns>Leave application details</returns>
        /// <response code="200">Leave application found</response>
        /// <response code="404">Leave application not found</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveRequestDto>> GetLeaveApplicationById(Guid id)
        {
            // W3: the request's owner, holders of the leave read tier, or whoever the engine is
            // currently asking to decide it — see CanReadRequestAsync.
            if (!await CanReadRequestAsync(id))
                return Forbid();

            try
            {
                var application = await _leaveService.GetLeaveRequestByIdAsync(id);
                // Round 5, lane D: what this viewer may do, decided here because two of the rules turn
                // on who they are to the employee and on today's date.
                application.ViewerActions = await _leaveService.GetViewerActionsAsync(
                    application, await HoldsLeavePolicyAsync(HrPermissions.LeaveWritePolicy));
                return Ok(application);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting leave request");
                return StatusCode(500, "An error occurred while getting leave request");
            }
        }

        /// <summary>
        /// Get leave application by application number (tracking)
        /// </summary>
        /// <param name="requestNumber">Application number</param>
        /// <returns>Leave request details</returns>
        /// <response code="200">Leave request found</response>
        /// <response code="404">Leave request not found</response>
        [HttpGet("by-number/{requestNumber}")]
        [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveRequestDto>> GetLeaveApplicationByNumber(string requestNumber)
        {
            var application = await _leaveService.GetLeaveRequestByNumberAsync(requestNumber);

            if (application == null)
            {
                return NotFound(new { message = $"Leave application '{requestNumber}' not found." });
            }

            // W3: ownership is only knowable after the lookup — the number is not a capability.
            // Same three arms as the by-id read.
            if (!await CanReadRequestAsync(application.Id))
                return Forbid();

            return Ok(application);
        }

        /// <summary>
        /// Get employee leave history for a specific year
        /// </summary>
        /// <param name="employeeId">Employee ID</param>
        /// <param name="year">Year</param>
        /// <param name="pageNumber">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 20)</param>
        /// <returns>Paged list of leave applications</returns>
        /// <response code="200">Leave history retrieved</response>
        [HttpGet("employee/{employeeId}/history")]
        [ProducesResponseType(typeof(PagedResult<LeaveRequestDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResult<LeaveRequestDto>>> GetEmployeeLeaveHistory(
            Guid employeeId,
            [FromQuery] int year = 0,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] LeaveStatus? status = null)
        {
            // W3: own history, or the leave read tier.
            if (!await CanActForEmployeeAsync(employeeId, HrPermissions.LeaveReadPolicy))
                return Forbid();

            if (year == 0)
            {
                year = await _leaveYear.CurrentYearAsync();
            }

            var result = await _leaveService.GetEmployeeLeaveHistoryAsync(employeeId, year, pageNumber, pageSize, status);

            return Ok(result);
        }

        /// <summary>
        /// Get employee leave balances for a specific year
        /// </summary>
        /// <param name="employeeId">Employee ID</param>
        /// <param name="year">Year (default: current year)</param>
        /// <returns>List of leave balances by leave type</returns>
        /// <response code="200">Leave balances retrieved</response>
        [HttpGet("employee/{employeeId}/balances")]
        [ProducesResponseType(typeof(IEnumerable<LeaveBalanceDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<LeaveBalanceDto>>> GetEmployeeLeaveBalances(
            Guid employeeId,
            [FromQuery] int year = 0)
        {
            // W3: own balances, or the leave read tier.
            if (!await CanActForEmployeeAsync(employeeId, HrPermissions.LeaveReadPolicy))
                return Forbid();

            if (year == 0) year = await _leaveYear.CurrentYearAsync();
            var balances = await _leaveService.GetEmployeeLeaveBalancesAsync(employeeId, year);
            return Ok(balances);
        }

        /// <summary>
        /// Get leave balances across all employees with optional filters
        /// </summary>
        [HttpGet("balances")]
        [Authorize(Policy = HrPermissions.LeaveReadPolicy)]
        [ProducesResponseType(typeof(IEnumerable<LeaveBalanceDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<LeaveBalanceDto>>> GetAllLeaveBalances(
            [FromQuery] int year = 0,
            [FromQuery] Guid? employeeId = null,
            [FromQuery] Guid? leaveTypeId = null)
        {
            if (year == 0) year = await _leaveYear.CurrentYearAsync();
            var balances = await _leaveService.GetAllLeaveBalancesAsync(year, employeeId, leaveTypeId);
            return Ok(balances);
        }

        /// <summary>
        /// Mandatory-leave compliance: for leave types flagged as mandatory-to-take, shows each
        /// employee's taken / scheduled / outstanding days for the year.
        /// </summary>
        [HttpGet("mandatory-compliance")]
        [Authorize(Policy = HrPermissions.LeaveReadPolicy)]
        [ProducesResponseType(typeof(IEnumerable<MandatoryLeaveComplianceDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<MandatoryLeaveComplianceDto>>> GetMandatoryCompliance(
            [FromQuery] int year = 0)
        {
            if (year == 0) year = await _leaveYear.CurrentYearAsync();
            var rows = await _leaveService.GetMandatoryLeaveComplianceAsync(year);
            return Ok(rows);
        }

        /// <summary>
        /// Get full audit-trail detail for a single leave balance
        /// </summary>
        [HttpGet("balances/{id}")]
        [Authorize(Policy = HrPermissions.LeaveReadPolicy)]
        [ProducesResponseType(typeof(LeaveBalanceDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveBalanceDetailDto>> GetLeaveBalanceDetail(Guid id)
        {
            var detail = await _leaveService.GetLeaveBalanceDetailAsync(id);
            if (detail == null)
                return NotFound(new { message = "Leave balance not found." });
            return Ok(detail);
        }

        /// <summary>
        /// How a balance's accrual is worked out as at a date: the accrual statement (round 5, lane C2)
        /// </summary>
        /// <remarks>
        /// <para>The rule, the entitlement and where it came from, the rate, and one line per completed
        /// period with a running total. <c>asOf</c> defaults to today; a later date shows what will
        /// have built up by then, and a date past the year end stops at the year end. This is the
        /// answer to "a utility that accrues leave up to a date": accrual is worked out whenever it
        /// is asked for, so there is nothing to run — only something to show.</para>
        ///
        /// <para>Self-or-read-tier, like the employee's balances read: the employee sees their own
        /// statement on the portal, HR anybody's.</para>
        /// </remarks>
        /// <response code="200">The statement</response>
        /// <response code="403">Not the caller's balance, and no leave read tier</response>
        /// <response code="404">No such balance</response>
        [HttpGet("balances/{id:guid}/accrual-statement")]
        [ProducesResponseType(typeof(LeaveAccrualStatementDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveAccrualStatementDto>> GetAccrualStatement(
            Guid id, [FromQuery] DateOnly? asOf = null, CancellationToken ct = default)
        {
            if (!await CanReadBalanceAsync(id))
                return Forbid();

            var statement = await _leaveService.GetAccrualStatementAsync(id, asOf, ct);
            if (statement == null)
                return NotFound(new { message = "Leave balance not found." });
            return Ok(statement);
        }

        /// <summary>
        /// Annual leave owed as at a date, per employee: the "leave owed" report (round 5, lane C6)
        /// </summary>
        /// <remarks>
        /// Days only — built up and not yet taken, for every employee on the books at the date. Finance
        /// puts the money on them. <c>asOf</c> defaults to today. The rules are on
        /// <c>LeaveService.GetLeaveOwedAsync</c>.
        /// </remarks>
        /// <response code="200">The report</response>
        /// <response code="400">The tenant has no annual leave type</response>
        [HttpGet("balances/owed")]
        [Authorize(Policy = HrPermissions.LeaveReadPolicy)]
        [ProducesResponseType(typeof(LeaveOwedReportDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<LeaveOwedReportDto>> GetLeaveOwed(
            [FromQuery] DateOnly? asOf = null, CancellationToken ct = default)
        {
            try
            {
                return Ok(await _leaveService.GetLeaveOwedAsync(asOf, ct));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>The "leave owed" report as a CSV, one row per employee.</summary>
        [HttpGet("balances/owed/export")]
        [Authorize(Policy = HrPermissions.LeaveReadPolicy)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ExportLeaveOwed(
            [FromQuery] DateOnly? asOf = null, CancellationToken ct = default)
        {
            try
            {
                var csv = await _leaveService.ExportLeaveOwedCsvAsync(asOf, ct);
                var date = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
                return File(csv, "text/csv", $"leave-owed-{date:yyyy-MM-dd}.csv");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// The tenant's current leave year (round 5, lane C4)
        /// </summary>
        /// <remarks>
        /// So a screen can open on the leave year we are in rather than on the calendar year. The two
        /// differ from the start of the calendar year to the month the leave year starts, whenever it
        /// does not start in January.
        /// </remarks>
        [HttpGet("leave-year")]
        [ProducesResponseType(typeof(LeaveYearInfoDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<LeaveYearInfoDto>> GetLeaveYear(CancellationToken ct = default)
        {
            var startMonth = await _leaveYear.StartMonthAsync(ct);
            var year = await _leaveYear.CurrentYearAsync(ct);
            return Ok(new LeaveYearInfoDto
            {
                StartMonth = startMonth,
                CurrentYear = year,
                StartDate = Core.Services.HR.LeaveYear.StartOf(year, startMonth),
                EndDate = Core.Services.HR.LeaveYear.EndOf(year, startMonth),
            });
        }

        /// <summary>
        /// Get all adjustments for a leave balance
        /// </summary>
        [HttpGet("balances/{balanceId}/adjustments")]
        [Authorize(Policy = HrPermissions.LeaveReadPolicy)]
        [ProducesResponseType(typeof(IEnumerable<LeaveAdjustmentDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<LeaveAdjustmentDto>>> GetAdjustments(Guid balanceId)
        {
            var adjustments = await _leaveService.GetAdjustmentsAsync(balanceId);
            return Ok(adjustments);
        }

        /// <summary>
        /// Add a manual adjustment to a leave balance
        /// </summary>
        [HttpPost("balances/{balanceId}/adjustments")]
        [Authorize(Policy = HrPermissions.LeaveWritePolicy)]
        [ProducesResponseType(typeof(LeaveAdjustmentDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveAdjustmentDto>> AddAdjustment(Guid balanceId, [FromBody] CreateLeaveAdjustmentDto dto)
        {
            try
            {
                dto.LeaveBalanceId = balanceId;
                var result = await _leaveService.AddAdjustmentAsync(dto);
                return CreatedAtAction(nameof(GetAdjustments), new { balanceId }, result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Delete a leave adjustment by ID (also recalculates the cached AdjustmentDays on the balance)
        /// </summary>
        [HttpDelete("adjustments/{id}")]
        [Authorize(Policy = HrPermissions.LeaveAdminPolicy)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAdjustment(Guid id)
        {
            try
            {
                await _leaveService.DeleteAdjustmentAsync(id);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get all leave adjustments across all employees with optional filters
        /// </summary>
        [HttpGet("adjustments")]
        [Authorize(Policy = HrPermissions.LeaveReadPolicy)]
        [ProducesResponseType(typeof(IEnumerable<LeaveAdjustmentDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<LeaveAdjustmentDto>>> GetAllAdjustments(
            [FromQuery] int year = 0,
            [FromQuery] Guid? employeeId = null,
            [FromQuery] Guid? leaveTypeId = null,
            [FromQuery] string? search = null)
        {
            if (year == 0) year = await _leaveYear.CurrentYearAsync();
            var adjustments = await _leaveService.GetAllAdjustmentsAsync(year, employeeId, leaveTypeId, search);
            return Ok(adjustments);
        }

        /// <summary>
        /// Get a single leave adjustment by ID
        /// </summary>
        [HttpGet("adjustments/{id}")]
        [Authorize(Policy = HrPermissions.LeaveReadPolicy)]
        [ProducesResponseType(typeof(LeaveAdjustmentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveAdjustmentDto>> GetAdjustmentById(Guid id)
        {
            var adjustment = await _leaveService.GetAdjustmentByIdAsync(id);
            if (adjustment == null)
                return NotFound(new { message = "Leave adjustment not found." });
            return Ok(adjustment);
        }

        /// <summary>
        /// Create a standalone leave adjustment (balance looked-up or auto-created; no balance ID required)
        /// </summary>
        [HttpPost("adjustments")]
        [Authorize(Policy = HrPermissions.LeaveWritePolicy)]
        [ProducesResponseType(typeof(LeaveAdjustmentDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveAdjustmentDto>> CreateStandaloneAdjustment([FromBody] CreateLeaveAdjustmentStandaloneDto dto)
        {
            try
            {
                var result = await _leaveService.CreateStandaloneAdjustmentAsync(dto);
                return CreatedAtAction(nameof(GetAdjustmentById), new { id = result.Id }, result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating standalone leave adjustment");
                return StatusCode(500, "An error occurred while creating the adjustment");
            }
        }

        /// <summary>
        /// Update the days and reason of an existing leave adjustment
        /// </summary>
        [HttpPut("adjustments/{id}")]
        [Authorize(Policy = HrPermissions.LeaveWritePolicy)]
        [ProducesResponseType(typeof(LeaveAdjustmentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveAdjustmentDto>> UpdateAdjustment(Guid id, [FromBody] UpdateLeaveAdjustmentDto dto)
        {
            try
            {
                var result = await _leaveService.UpdateAdjustmentAsync(id, dto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating leave adjustment {AdjustmentId}", id);
                return StatusCode(500, "An error occurred while updating the adjustment");
            }
        }

        /// <summary>
        /// Leave requests awaiting YOUR decision
        /// </summary>
        /// <remarks>
        /// <para>The queue the approvals screen reads. It asks the workflow engine which requests
        /// the caller may actually decide, rather than inferring it from the reporting line — the
        /// closure plan's L-10. Under the two-stage ladder that difference matters: after the line
        /// manager approves, the request stays <c>Pending</c> while the engine sits at the HR step,
        /// so a reporting-line query keeps showing it to the manager who already decided and never
        /// shows it to HR, who is nobody's manager.</para>
        ///
        /// <para>No approver parameter, deliberately: the answer is only ever about the caller, so
        /// there is nothing here to point at somebody else's queue.</para>
        /// </remarks>
        /// <response code="200">Requests awaiting your decision</response>
        [HttpGet("my-approvals")]
        [ProducesResponseType(typeof(PagedResult<LeaveRequestDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResult<LeaveRequestDto>>> GetMyApprovals(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default)
            => Ok(await _leaveService.GetMyPendingApprovalsAsync(pageNumber, pageSize, ct));

        /// <summary>
        /// Get pending leave approvals for a manager
        /// </summary>
        /// <remarks>
        /// ⚠ SUPERSEDED by <c>my-approvals</c> above, and kept only because existing callers use
        /// it. It answers "this manager's direct reports' Pending requests", which is not the same
        /// question as "what is waiting on this manager" — see L-10.
        /// </remarks>
        /// <param name="managerId">Manager's employee ID</param>
        /// <param name="pageNumber">Page number (default: 1)</param>
        /// <param name="pageSize">Page size (default: 20)</param>
        /// <returns>Paged list of pending leave applications</returns>
        /// <response code="200">Pending approvals retrieved</response>
        [HttpGet("pending-approvals/{managerId}")]
        [ProducesResponseType(typeof(PagedResult<LeaveRequestDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResult<LeaveRequestDto>>> GetPendingApprovals(
            Guid managerId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            // W3: a manager reads their OWN queue; reading someone else's is the leave read tier.
            if (!await CanActForEmployeeAsync(managerId, HrPermissions.LeaveReadPolicy))
                return Forbid();

            var result = await _leaveService.GetPendingApprovalsAsync(managerId, pageNumber, pageSize);

            return Ok(result);
        }

        /// <summary>
        /// Submit a leave request for approval
        /// </summary>
        /// <param name="id">Leave request ID</param>
        /// <returns>Success indicator</returns>
        /// <response code="200">Leave request submitted for approval</response>
        /// <response code="400">Invalid request or business rule violation</response>
        /// <response code="404">Leave request not found</response>
        [HttpPost("{id}/submit")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SubmitForApproval(Guid id)
        {
            // W3: the request's owner submits; the HR desk may submit on their behalf.
            if (!await CanActOnRequestAsync(id, HrPermissions.LeaveWritePolicy))
                return Forbid();

            try
            {
                await _leaveService.SubmitForApprovalAsync(id);
                return Ok(new { message = "Leave request submitted for approval." });
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting leave request {id} for approval", id);
                return StatusCode(500, new { message = "An error occurred while submitting the leave request." });
            }
        }

        /// <summary>
        /// Approve a leave application
        /// </summary>
        /// <param name="id">Leave application ID</param>
        /// <param name="dto">Approval details</param>
        /// <returns>Updated leave application</returns>
        /// <response code="200">Leave approved successfully</response>
        /// <response code="400">Invalid request or business rule violation</response>
        /// <response code="404">Leave application not found</response>
        // W3: deliberately NOT permission-gated — the approver is whoever the workflow engine
        // assigned, usually a line manager with no HR permission at all, and the service refuses
        // anyone else per request (LeaveService.ApproveLeaveAsync -> CanUserApproveAsync).
        [HttpPut("{id}/approve")]
        [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveRequestDto>> ApproveLeave(Guid id, [FromBody] ApproveLeaveDto dto)
        {
            try
            {
                var application = await _leaveService.ApproveLeaveAsync(id, dto);
                return Ok(application);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving leave request {LeaveRequestId}", id);
                return StatusCode(500, "An error occurred while approving the leave request");
            }
        }

        /// <summary>
        /// Reject a leave application
        /// </summary>
        /// <param name="id">Leave application ID</param>
        /// <param name="dto">Rejection details</param>
        /// <returns>Updated leave application</returns>
        /// <response code="200">Leave rejected successfully</response>
        /// <response code="400">Invalid request or business rule violation</response>
        /// <response code="404">Leave application not found</response>
        // W3: same as approve — the workflow assignee's act, validated per request.
        [HttpPut("{id}/reject")]
        [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveRequestDto>> RejectLeave(Guid id, [FromBody] RejectLeaveDto dto)
        {
            try
            {
                var application = await _leaveService.RejectLeaveAsync(id, dto);
                return Ok(application);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting leave request {LeaveRequestId}", id);
                return StatusCode(500, "An error occurred while rejecting the leave request");
            }
        }

        /// <summary>
        /// Approve several leave requests at once
        /// </summary>
        /// <remarks>
        /// Deliberately NOT permission-gated, exactly like the single approve: the approver is
        /// whoever the workflow engine assigned, and the service refuses anyone else PER REQUEST.
        /// A batch cannot smuggle through a request the caller was never assigned, because each
        /// item runs the same check the single-item endpoint runs (bulk catalogue §4.1).
        /// </remarks>
        /// <response code="200">Per-item results, including why any were skipped</response>
        /// <response code="400">More than 50 at a time</response>
        [HttpPost("bulk-approve")]
        [ProducesResponseType(typeof(HrBulkActionResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<HrBulkActionResultDto>> BulkApprove(
            [FromBody] BulkLeaveDecisionDto dto, CancellationToken ct)
        {
            try
            {
                return Ok(await _leaveService.BulkDecideAsync(dto, approve: true, ct));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Reject several leave requests at once, with one shared reason.</summary>
        [HttpPost("bulk-reject")]
        [ProducesResponseType(typeof(HrBulkActionResultDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<HrBulkActionResultDto>> BulkReject(
            [FromBody] BulkLeaveDecisionDto dto, CancellationToken ct)
        {
            try
            {
                return Ok(await _leaveService.BulkDecideAsync(dto, approve: false, ct));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// The organisation-wide leave register
        /// </summary>
        /// <remarks>
        /// Until this existed the only listing was one employee at a time, so "who is off in
        /// December" or "every rejected request this quarter" could not be asked at all (closure
        /// plan L-6). Org-wide, so it sits on the leave READ tier rather than self-or-permission —
        /// an employee's own history is still served by <c>employee/{id}/history</c>.
        /// </remarks>
        [HttpGet("register")]
        [Authorize(Policy = HrPermissions.LeaveReadPolicy)]
        [ProducesResponseType(typeof(PagedResult<LeaveRequestDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<PagedResult<LeaveRequestDto>>> GetRegister(
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            [FromQuery] LeaveStatus? status,
            [FromQuery] Guid? leaveTypeId,
            [FromQuery] Guid? employeeId,
            [FromQuery] Guid? organizationUnitId,
            [FromQuery] string? search,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 25,
            CancellationToken ct = default)
        {
            var filter = new LeaveRegisterFilterDto
            {
                From = from, To = to, Status = status, LeaveTypeId = leaveTypeId,
                EmployeeId = employeeId, OrganizationUnitId = organizationUnitId, Search = search,
            };
            return Ok(await _leaveService.GetRegisterAsync(filter, pageNumber, pageSize, ct));
        }

        /// <summary>The register as a CSV, with the same filters.</summary>
        [HttpGet("register/export")]
        [Authorize(Policy = HrPermissions.LeaveReadPolicy)]
        public async Task<IActionResult> ExportRegister(
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            [FromQuery] LeaveStatus? status,
            [FromQuery] Guid? leaveTypeId,
            [FromQuery] Guid? employeeId,
            [FromQuery] Guid? organizationUnitId,
            [FromQuery] string? search,
            CancellationToken ct = default)
        {
            var filter = new LeaveRegisterFilterDto
            {
                From = from, To = to, Status = status, LeaveTypeId = leaveTypeId,
                EmployeeId = employeeId, OrganizationUnitId = organizationUnitId, Search = search,
            };
            var csv = await _leaveService.ExportRegisterCsvAsync(filter, ct);
            return File(csv, "text/csv", $"leave-register-{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        /// <summary>The balances register as a CSV.</summary>
        [HttpGet("balances/export")]
        [Authorize(Policy = HrPermissions.LeaveReadPolicy)]
        public async Task<IActionResult> ExportBalances(
            [FromQuery] int year,
            [FromQuery] Guid? employeeId,
            [FromQuery] Guid? leaveTypeId,
            CancellationToken ct = default)
        {
            if (year == 0) year = await _leaveYear.CurrentYearAsync();
            var csv = await _leaveService.ExportBalancesCsvAsync(year, employeeId, leaveTypeId, ct);
            return File(csv, "text/csv", $"leave-balances-{year}.csv");
        }

        /// <summary>The mandatory-leave compliance register as a CSV.</summary>
        [HttpGet("compliance/export")]
        [Authorize(Policy = HrPermissions.LeaveReadPolicy)]
        public async Task<IActionResult> ExportCompliance(
            [FromQuery] int year, CancellationToken ct = default)
        {
            if (year == 0) year = await _leaveYear.CurrentYearAsync();
            var csv = await _leaveService.ExportComplianceCsvAsync(year, ct);
            return File(csv, "text/csv", $"leave-compliance-{year}.csv");
        }

        /// <summary>
        /// Leave drawn as time rather than rows
        /// </summary>
        /// <remarks>
        /// <para>One endpoint, three audiences (decision D-9). The SCOPE decides who is visible and
        /// how the caller is authorized for it — which is the whole reason this is not three
        /// endpoints or one unguarded one:</para>
        /// <list type="bullet">
        /// <item><c>Mine</c> — the caller's own leave. Needs a linked employee record and nothing
        /// else; every member of staff can see their own year.</item>
        /// <item><c>Team</c> — the caller's own direct reports, plus the caller. Needs a linked
        /// employee record. This is the one place leave deliberately DOES read down the reporting
        /// line: a manager arranging cover has to see who is away, and a calendar band carries a
        /// name, a leave type and dates — no reason, no balance, nothing the rest of the module
        /// keeps behind self-or-permission.</item>
        /// <item><c>Organisation</c> — everybody. The leave READ tier, like every other org-wide
        /// leave surface.</item>
        /// </list>
        /// <para>Round 5 lane F: <c>employeeId</c> shows one person's leave. It narrows the scope
        /// and never widens it: in <c>Team</c> it is one of the caller's reports or nobody, so it
        /// is not a way round the scope's own rule. <c>organizationUnitId</c> (Organisation only)
        /// takes the unit and every unit beneath it.</para>
        /// </remarks>
        /// <response code="200">The calendar for the range</response>
        /// <response code="400">The range is backwards or longer than 400 days</response>
        /// <response code="403">Organisation scope without the leave read tier, or no linked employee record</response>
        [HttpGet("calendar")]
        [ProducesResponseType(typeof(LeaveCalendarDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<LeaveCalendarDto>> GetCalendar(
            [FromQuery] DateOnly from,
            [FromQuery] DateOnly to,
            [FromQuery] LeaveCalendarScope scope = LeaveCalendarScope.Mine,
            [FromQuery] Guid? leaveTypeId = null,
            [FromQuery] Guid? organizationUnitId = null,
            [FromQuery] Guid? employeeId = null,
            CancellationToken ct = default)
        {
            try
            {
                Guid? subject = null;

                if (scope == LeaveCalendarScope.Organisation)
                {
                    if (!await HoldsLeavePolicyAsync(HrPermissions.LeaveReadPolicy))
                        return Forbid();
                }
                else
                {
                    // Both personal scopes resolve from the token, never from a query parameter —
                    // otherwise "my team" would take an employee id and become a way to read
                    // anybody's reporting line.
                    if (_currentUserService.EmployeeId is not Guid me || me == Guid.Empty)
                        return Forbid();
                    subject = me;
                }

                return Ok(await _leaveService.GetCalendarAsync(
                    from, to, scope, subject, leaveTypeId, organizationUnitId, employeeId, ct));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building the leave calendar");
                return StatusCode(500, "An error occurred while building the leave calendar");
            }
        }

        /// <summary>
        /// Send a submitted leave request back to the employee with dates of your own
        /// </summary>
        /// <remarks>
        /// The third decision verb beside approve and reject, and TDC's "sending back for
        /// correction with suggested dates". Leave PLANS have had it since the port; the request —
        /// the record that actually books the days — did not.
        /// </remarks>
        /// <response code="200">Sent back; the request is now ChangesSuggested</response>
        /// <response code="400">Not a submitted request, or the dates are the wrong way round</response>
        /// <response code="401">You are not an approver for this request, or it is your own</response>
        /// <response code="404">Leave application not found</response>
        // W3: same as approve and reject — the workflow assignee's act, validated per request.
        [HttpPut("{id}/suggest-changes")]
        [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveRequestDto>> SuggestChanges(
            Guid id, [FromBody] SuggestLeaveRequestChangesDto dto)
        {
            try
            {
                return Ok(await _leaveService.SuggestChangesAsync(id, dto));
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error suggesting changes on leave request {LeaveRequestId}", id);
                return StatusCode(500, "An error occurred while sending the leave request back");
            }
        }

        /// <summary>
        /// Accept the suggested dates, or counter with your own; either way the request re-submits
        /// </summary>
        /// <response code="200">Answered; the request is back in approval</response>
        /// <response code="400">Nothing to respond to, or the dates fail a check</response>
        /// <response code="403">Not your request</response>
        /// <response code="404">Leave application not found</response>
        [HttpPut("{id}/respond-suggestion")]
        [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveRequestDto>> RespondToSuggestion(
            Guid id, [FromBody] RespondToLeaveSuggestionDto dto)
        {
            try
            {
                // Answering a suggestion is the EMPLOYEE's act — it is their leave and their dates.
                // Self-or-leave-write, the same shape every other per-employee write here uses.
                var owner = await _leaveService.GetLeaveRequestByIdAsync(id);
                if (!await CanActForEmployeeAsync(owner.EmployeeId, HrPermissions.LeaveWritePolicy))
                    return Forbid();

                return Ok(await _leaveService.RespondToSuggestionAsync(id, dto));
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error responding to the suggestion on leave request {LeaveRequestId}", id);
                return StatusCode(500, "An error occurred while answering the suggested dates");
            }
        }

        /// <summary>
        /// Move an approved leave request to different dates, keeping its number and its history
        /// </summary>
        /// <remarks>
        /// TDC's "possible shifting of the leave to a different day even after the planning is
        /// done". The alternative was cancel-and-re-key, which loses the number, the approval and
        /// the history. ⚠ Moving the dates RE-OPENS the approval (decision D-5): an approval is an
        /// approval of dates, and carrying it across to different ones would be a lie.
        /// </remarks>
        /// <response code="200">Moved; the request is back in approval on its new dates</response>
        /// <response code="400">Not approved, already closed, no reason given, or the new dates fail a check</response>
        /// <response code="403">Not your request and you do not hold the leave write tier</response>
        /// <response code="404">Leave application not found</response>
        [HttpPut("{id}/reschedule")]
        [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveRequestDto>> Reschedule(
            Guid id, [FromBody] RescheduleLeaveRequestDto dto)
        {
            try
            {
                // Decision D-5: HR may move it, and the employee may move their own. Both then go
                // back through approval, so neither can move a date past an approver.
                var owner = await _leaveService.GetLeaveRequestByIdAsync(id);
                if (!await CanActForEmployeeAsync(owner.EmployeeId, HrPermissions.LeaveWritePolicy))
                    return Forbid();

                return Ok(await _leaveService.RescheduleAsync(id, dto));
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rescheduling leave request {LeaveRequestId}", id);
                return StatusCode(500, "An error occurred while moving the leave request");
            }
        }

        /// <summary>
        /// Recall an employee from leave before their end date
        /// </summary>
        /// <remarks>
        /// Curtailment (residue plan R-14). The leave is <b>truncated</b>: days up to the recall
        /// stand as taken, days after are restored to the balance, and the request keeps its number,
        /// its status and its approval — because it was validly approved and then interrupted.
        ///
        /// <para>⚠ <c>effectiveDate</c> is <b>the first day the employee is back at work</b>, not
        /// the last day of their leave.</para>
        ///
        /// <para>⚠ <b>The employer's act, never self-or-HR</b>, unlike reschedule beside it: an
        /// employee may ask to move their own leave, but may not call themselves back and hand
        /// themselves the days. The service refuses the subject a second time, so this holds even for
        /// an HR user recalling themselves.</para>
        ///
        /// <para>Round 5, decision B1: the employer is the leave write tier <b>or the employee's line
        /// authority</b>, their supervisor or the head of their department. Recall is for urgent
        /// necessity (Labour Act s.25), and the manager who needs them back is who knows it.</para>
        /// </remarks>
        /// <response code="200">The truncated request</response>
        /// <response code="400">Not approved, already closed, no reason, or a date that gives nothing back</response>
        [HttpPut("{id}/recall")]
        [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveRequestDto>> Recall(
            Guid id, [FromBody] RecallLeaveRequestDto dto)
        {
            if (!await HoldsLeavePolicyAsync(HrPermissions.LeaveWritePolicy)
                && !await IsLineAuthorityForRequestAsync(id))
                return Forbid();

            try
            {
                return Ok(await _leaveService.RecallAsync(id, dto));
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recalling leave request {LeaveRequestId}", id);
                return StatusCode(500, "An error occurred while recalling the employee from leave");
            }
        }

        /// <summary>
        /// Point this request at the medical board that ruled on the absence
        /// </summary>
        /// <remarks>
        /// Residue plan G4. Pass a null id to unlink.
        ///
        /// <para>⚠ A board that is only REQUESTED or CONVENED can be linked — a board is usually
        /// asked for before it sits, and the request should be able to say which one it is waiting
        /// on. The evidence gate is what insists on <b>Concluded</b>: linking records intent, the
        /// gate enforces the rule.</para>
        ///
        /// <para>⚠ The board must be about the same employee.</para>
        ///
        /// <para>⚠ <b>An empty body means unlink</b>, and that is not decoration. The frontend's
        /// shared <c>apiService.put</c> drops a <c>null</c> body rather than serialising it, so the
        /// Unlink button sends a PUT with no body at all. Bound strictly that is a <b>400</b> from
        /// model binding — <i>"A non-empty request body is required"</i>, measured against a
        /// deliberately strict sibling endpoint — and the button does nothing. The endpoint would
        /// work from curl and fail in the product, which is precisely the class of defect this slice
        /// was fixing. <c>EmptyBodyBehavior.Allow</c> makes
        /// "no board named" and "no body sent" the same request, which is also what a nullable
        /// parameter ought to mean.</para>
        /// </remarks>
        [HttpPut("{id}/medical-board")]
        [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<LeaveRequestDto>> LinkMedicalBoard(
            Guid id,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] Guid? medicalBoardId)
        {
            try
            {
                // Self-or-desk, like the other things done TO a request: an employee may attach
                // their own evidence, and HR may do it for anybody.
                if (!await CanActOnRequestAsync(id, HrPermissions.LeaveWritePolicy))
                    return Forbid();

                return Ok(await _leaveService.LinkMedicalBoardAsync(id, medicalBoardId));
            }
            catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error linking medical board to leave request {LeaveRequestId}", id);
                return StatusCode(500, "An error occurred while linking the medical board");
            }
        }

        /// <summary>
        /// Record that approved leave is still going ahead
        /// </summary>
        /// <remarks>
        /// The answer to the reminder that asks. It moves no days and changes no status — it
        /// records that somebody was asked and answered, which is what was missing between an
        /// approval and the day the leave starts.
        /// </remarks>
        /// <response code="200">Confirmed</response>
        /// <response code="400">Not approved, or already closed</response>
        /// <response code="403">Not your request and you do not hold the leave write tier</response>
        /// <response code="404">Leave application not found</response>
        [HttpPut("{id}/confirm-observance")]
        [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveRequestDto>> ConfirmObservance(Guid id)
        {
            try
            {
                var owner = await _leaveService.GetLeaveRequestByIdAsync(id);
                if (!await CanActForEmployeeAsync(owner.EmployeeId, HrPermissions.LeaveWritePolicy))
                    return Forbid();

                return Ok(await _leaveService.ConfirmObservanceAsync(id));
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming leave request {LeaveRequestId}", id);
                return StatusCode(500, "An error occurred while confirming the leave");
            }
        }

        /// <summary>
        /// Cancel a leave application
        /// </summary>
        /// <param name="id">Leave application ID</param>
        /// <param name="cancellationReason">Reason for cancellation</param>
        /// <returns>Success status</returns>
        /// <response code="200">Leave cancelled successfully</response>
        /// <response code="400">Invalid request or business rule violation</response>
        /// <response code="404">Leave application not found</response>
        [HttpPut("{id}/cancel")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelLeave(
            Guid id,
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] string? cancellationReason)
        {
            // W3: the request's owner, or the HR desk. What each may cancel is the service's rule
            // (round 5, lane D1): the owner until approval, the desk up to the first day, with a reason.
            if (!await CanActOnRequestAsync(id, HrPermissions.LeaveWritePolicy))
                return Forbid();

            try
            {
                var actingAsDesk = await HoldsLeavePolicyAsync(HrPermissions.LeaveWritePolicy);
                await _leaveService.CancelLeaveRequestAsync(id, cancellationReason, actingAsDesk);
                return Ok(new { message = "Leave application cancelled successfully" });
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling leave request {LeaveRequestId}", id);
                return StatusCode(500, "An error occurred while cancelling the leave request");
            }
        }

        /// <summary>
        /// Confirm the employee's return, which closes the leave
        /// </summary>
        /// <remarks>
        /// Round 5, decision B3. The leave write tier or the employee's line authority (supervisor or
        /// head of department) confirms. An early return, on the employee's own report, cuts the leave
        /// short; a late one records the working days overstayed. The service refuses the employee
        /// confirming their own return.
        /// </remarks>
        /// <response code="200">Leave closed successfully</response>
        /// <response code="400">Invalid request or business rule violation</response>
        /// <response code="404">Leave application not found</response>
        [HttpPut("{id}/close")]
        [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveRequestDto>> CloseLeave(
            Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CloseLeaveDto? dto)
        {
            if (!await HoldsLeavePolicyAsync(HrPermissions.LeaveWritePolicy)
                && !await IsLineAuthorityForRequestAsync(id))
                return Forbid();

            try
            {
                var application = await _leaveService.CloseLeaveRequestAsync(id, dto ?? new CloseLeaveDto());
                return Ok(application);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing leave request {LeaveRequestId}", id);
                return StatusCode(500, "An error occurred while closing the leave request");
            }
        }

        /// <summary>
        /// "I'm back at work": the employee reports the day they returned
        /// </summary>
        /// <remarks>
        /// Round 5, decision B3. The employee's own act, and only theirs; their line authority or HR
        /// confirms it (<c>close</c>). An empty body reports today.
        /// </remarks>
        [HttpPut("{id}/report-resumption")]
        [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveRequestDto>> ReportResumption(
            Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReportResumptionDto? dto)
        {
            // Owner only: CanActOnRequestAsync with a policy nobody is meant to pass on their own.
            if (_currentUserService.EmployeeId is not Guid me || me == Guid.Empty ||
                _currentUserService.TenantId is not Guid tenantId ||
                !await _db.Set<Core.Entities.HR.StaffLeave.LeaveRequest>().AsNoTracking()
                    .AnyAsync(r => r.Id == id && r.TenantId == tenantId && r.EmployeeId == me))
                return Forbid();

            try
            {
                return Ok(await _leaveService.ReportResumptionAsync(id, dto ?? new ReportResumptionDto()));
            }
            catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reporting the return from leave request {LeaveRequestId}", id);
                return StatusCode(500, "An error occurred while reporting the return from leave");
            }
        }

        /// <summary>
        /// Returns from leave waiting for the caller to confirm, as their line authority
        /// </summary>
        /// <remarks>
        /// Round 5, decision B3: the people the caller supervises or heads a department over, whose
        /// return has been reported and not yet confirmed. HR works the register instead.
        /// </remarks>
        [HttpGet("resumptions-to-confirm")]
        [ProducesResponseType(typeof(IReadOnlyList<LeaveRequestDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IReadOnlyList<LeaveRequestDto>>> GetResumptionsToConfirm(
            CancellationToken ct = default)
        {
            if (_currentUserService.EmployeeId is not Guid me || me == Guid.Empty)
                return Ok(Array.Empty<LeaveRequestDto>());

            return Ok(await _leaveService.GetResumptionsToConfirmAsync(me, ct));
        }
        /// <summary>
        /// Recalculate leave balance(s) from source data (admin operation).
        /// If LeaveTypeId is provided only that type is recalculated; otherwise all leave types for the employee are recalculated.
        /// </summary>
        /// <summary>
        /// Recalculate EVERY balance in the tenant for a year (admin operation)
        /// </summary>
        /// <remarks>
        /// <para>⚠ <b>Finding L-19.</b> Recalculation was one employee at a time, which is right for
        /// the ordinary case — it runs after every approval, cancellation and adjustment. But a
        /// policy correction reaches everybody: change an accrual rule, fix an entitlement, or land
        /// the <c>UsedDays</c> correction that G1 shipped, and nine hundred balances are stale with
        /// no route through the UI to put them right.</para>
        ///
        /// <para><b>Safe to run and safe to re-run.</b> It DERIVES the counters from requests and
        /// adjustments that already exist, never invents a figure, and never touches
        /// <c>EntitledDays</c> or <c>CarriedOverDays</c>. That is why it has no dry run, unlike the
        /// year-end jobs — there is nothing to preview when running twice gives the same answer as
        /// running once.</para>
        ///
        /// <para>⚠ <b>Admin tier</b>, a step above the per-employee call beside it: this one walks
        /// the whole tenant and is heavy.</para>
        /// </remarks>
        /// <response code="200">What it did, including any employee it could not finish</response>
        [HttpPost("balances/recalculate-all")]
        [Authorize(Policy = HrPermissions.LeaveAdminPolicy)]
        [ProducesResponseType(typeof(LeaveBulkRecalculationResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<LeaveBulkRecalculationResult>> RecalculateAllBalances(
            [FromQuery] int year, [FromQuery] Guid? leaveTypeId = null, CancellationToken ct = default)
        {
            if (year < 2000)
                return BadRequest(new { message = "A valid year is required." });

            try
            {
                return Ok(await _recalculationService.RecalculateTenantAsync(year, leaveTypeId, ct));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Re-derive stored entitlements from the rulebook, or preview what that would change
        /// </summary>
        /// <remarks>
        /// <para>Entitlement plan A3. <c>EntitledDays</c> is written when a balance row is created
        /// and never refreshed, so an allocation corrected mid-year does not reach the balances that
        /// already exist — and a balance opened by posting an adjustment used to record the leave
        /// type's DEFAULT days rather than the employee's staff-level allocation (A1). Fixing the
        /// creation site helps nobody who is already wrong; this is the pass that does.</para>
        ///
        /// <para>⚠ <b>Unlike the recalculation beside it, this OVERWRITES a stored figure</b> from
        /// configuration that may have moved since. That is why it has a dry run and that one does
        /// not: the preview names every row and both figures, so a person decides.</para>
        ///
        /// <para>⚠ <b>It does not revisit a carry-over already run for the year</b> — that was
        /// computed from the old entitlement. The result counts those rows separately rather than
        /// silently correcting them, because re-running carry-over is a decision of its own.</para>
        ///
        /// <para><b>Admin tier</b>, like the year-end jobs and for the same reason: it changes what
        /// people are owed, in bulk.</para>
        /// </remarks>
        /// <param name="dryRun">⚠ Compute and report, write nothing.</param>
        /// <response code="200">What changed, or what would have</response>
        [HttpPost("balances/repair-entitlements")]
        [Authorize(Policy = HrPermissions.LeaveAdminPolicy)]
        [ProducesResponseType(typeof(LeaveEntitlementRepairResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<LeaveEntitlementRepairResult>> RepairEntitlements(
            [FromQuery] int year,
            [FromQuery] Guid? leaveTypeId = null,
            [FromQuery] Guid? employeeId = null,
            [FromQuery] bool dryRun = false,
            CancellationToken ct = default)
        {
            if (year < 2000)
                return BadRequest(new { message = "A valid year is required." });

            try
            {
                return Ok(await _recalculationService.RepairEntitlementsAsync(
                    year, leaveTypeId, employeeId, dryRun, ct));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("balances/recalculate")]
        [Authorize(Policy = HrPermissions.LeaveWritePolicy)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RecalculateBalance([FromBody] RecalculateLeaveBalanceRequest request)
        {
            if (request.EmployeeId == Guid.Empty)
                return BadRequest(new { message = "EmployeeId is required." });

            try
            {
                if (request.LeaveTypeId.HasValue)
                    await _recalculationService.RecalculateAsync(request.EmployeeId, request.LeaveTypeId.Value, request.Year);
                else
                    await _recalculationService.RecalculateAllAsync(request.EmployeeId, request.Year);

                return Ok(new { message = "Leave balance recalculation completed." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recalculating leave balance for employee {EmployeeId}", request.EmployeeId);
                return StatusCode(500, "An error occurred during leave balance recalculation");
            }
        }

        // ─── Attachments ──────────────────────────────────────────────────────

        [HttpGet("{id:guid}/attachments")]
        [ProducesResponseType(typeof(IEnumerable<LeaveRequestAttachmentDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<LeaveRequestAttachmentDto>>> GetAttachments(Guid id)
        {
            // W3: same audience as the request itself.
            if (!await CanActOnRequestAsync(id, HrPermissions.LeaveReadPolicy))
                return Forbid();

            try
            {
                var attachments = await _leaveService.GetAttachmentsAsync(id);
                return Ok(attachments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching attachments for leave request {LeaveRequestId}", id);
                return StatusCode(500, "An error occurred while fetching attachments");
            }
        }

        [HttpPost("{id:guid}/attachments")]
        [ProducesResponseType(typeof(LeaveRequestAttachmentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadAttachment(
            Guid id, IFormFile file,
            [FromQuery] LeaveEvidenceKind evidenceKind = LeaveEvidenceKind.Other,
            CancellationToken ct = default)
        {
            // W3: evidence goes onto your own request; the HR desk attaches for anyone.
            if (!await CanActOnRequestAsync(id, HrPermissions.LeaveWritePolicy))
                return Forbid();

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file provided" });

            var uploadedById = _currentUserService.EmployeeId ?? Guid.Empty;
            if (uploadedById == Guid.Empty)
                return Unauthorized(new { message = "User employee context not found" });

            if (_currentUserService.TenantId is not Guid tenantId ||
                !Guid.TryParse(_currentUserService.UserId, out var actorUserId))
                return Unauthorized(new { message = "User context could not be resolved" });

            // Confirms the leave request exists and belongs to this tenant before anything is
            // stored, and gives the DMS registration a source record that definitely exists.
            try
            {
                await _leaveService.GetAttachmentsAsync(id);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }

            HrControlledDocument document;
            try
            {
                document = await _hrDocuments.UploadAsync(new HrDocumentUploadRequest
                {
                    TenantId = tenantId,
                    ActorUserId = actorUserId,
                    ActorName = _currentUserService.UserName,
                    Category = ControlledFileUploadCategories.HrLeaveAttachments,
                    File = file,
                    Registration = new HrDocumentDmsRegistration
                    {
                        SourceLabel = "Leave request attachment",
                        SourceEntityType = "LeaveRequest",
                        SourceRecordId = id,
                        Title = Path.GetFileName(file.FileName),
                        DocumentType = "LeaveAttachment",
                        ChangeSummary = "Uploaded through the leave request screen."
                    }
                }, ct);
            }
            catch (ControlledFileUploadException ex)
            {
                return StatusCode(ex.StatusCode, new { code = ex.Code, message = ex.Message });
            }

            try
            {
                var dto = await _leaveService.UploadAttachmentAsync(
                    id, uploadedById, document.OriginalFileName, string.Empty,
                    document.ContentType, document.FileSize,
                    document.FileUploadRecordId, document.DocumentRecordId, document.DocumentVersionId,
                    // ⚠ What the document IS, which is what the R-15a evidence gate reads. Defaults to
                    // Other, so an existing caller that does not send it uploads a plain supporting
                    // document rather than silently satisfying a medical-certificate requirement.
                    evidenceKind);

                return Ok(dto);
            }
            catch (Exception ex)
            {
                // The file is stored and catalogued but nothing references it — take it back out.
                await _hrDocuments.RollbackAsync(document, tenantId, actorUserId, ct);

                if (ex is ArgumentException)
                    return NotFound(new { message = ex.Message });

                _logger.LogError(ex, "Error uploading attachment for leave request {LeaveRequestId}", id);
                return StatusCode(500, "An error occurred while uploading the attachment");
            }
        }

        /// <summary>
        /// Streams a leave attachment to a caller entitled to see it.
        /// </summary>
        /// <remarks>
        /// Attachments are frequently medical certificates, so they are stored privately and
        /// are only reachable here. Neither this endpoint's helper nor the DMS performs the
        /// entitlement check — that is the ownership test below.
        /// </remarks>
        [HttpGet("attachments/{attachmentId:guid}/download")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DownloadAttachment(Guid attachmentId, CancellationToken ct = default)
        {
            if (_currentUserService.TenantId is not Guid tenantId)
                return Unauthorized(new { message = "Tenant context could not be resolved" });

            var attachment = await _db.Set<Core.Entities.HR.StaffLeave.LeaveRequestAttachment>()
                .AsNoTracking()
                .Include(item => item.LeaveRequest)
                .SingleOrDefaultAsync(
                    item => item.Id == attachmentId && item.TenantId == tenantId && !item.IsDeleted,
                    ct);
            if (attachment is null)
                return NotFound(new { message = "Attachment not found" });

            var isOwner = _currentUserService.EmployeeId is Guid employeeId &&
                          attachment.LeaveRequest.EmployeeId == employeeId;
            // W3: the "or HR" side now goes through the leave read tier, so database grants and
            // the role fallback both count — the raw role list this replaced missed TenantAdmin.
            if (!isOwner && !await HoldsLeavePolicyAsync(HrPermissions.LeaveReadPolicy))
                return Forbid();

            return await HrDocumentDownload.ServeAsync(
                this, _centralDocuments, _fileStorageService, _db, tenantId,
                attachment.DocumentRecordId, attachment.DocumentVersionId,
                attachment.FileUploadRecordId, attachment.FilePath,
                attachment.FileName, attachment.ContentType,
                inline: false, ct);
        }

        [HttpDelete("attachments/{attachmentId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteAttachment(Guid attachmentId, CancellationToken ct = default)
        {
            try
            {
                var attachment = await _leaveService.GetAttachmentByIdAsync(attachmentId);
                if (attachment == null)
                    return NotFound(new { message = "Attachment not found" });

                // W3: the request's owner removes their own evidence; otherwise the leave write
                // tier. Previously any authenticated user could delete any leave attachment.
                if (!await CanActOnRequestAsync(attachment.LeaveRequestId, HrPermissions.LeaveWritePolicy))
                    return Forbid();

                // Controlled uploads and their DMS records are removed through the shared
                // boundary, which soft-deletes and schedules the physical delete. Only
                // pre-migration rows still carry a raw storage path to remove directly.
                var stored = await _db.Set<Core.Entities.HR.StaffLeave.LeaveRequestAttachment>()
                    .AsNoTracking()
                    .SingleOrDefaultAsync(item => item.Id == attachmentId, ct);

                if (stored is not null &&
                    _currentUserService.TenantId is Guid tenantId &&
                    Guid.TryParse(_currentUserService.UserId, out var actorUserId) &&
                    stored.FileUploadRecordId is Guid uploadId)
                {
                    await _hrDocuments.RollbackAsync(
                        new HrControlledDocument
                        {
                            FileUploadRecordId = uploadId,
                            FilePath = stored.FilePath,
                            OriginalFileName = stored.FileName,
                            ContentType = stored.ContentType ?? "application/octet-stream",
                            FileSize = stored.FileSizeBytes ?? 0,
                            DocumentRecordId = stored.DocumentRecordId,
                            DocumentVersionId = stored.DocumentVersionId
                        },
                        tenantId, actorUserId, ct);
                }
                else if (!string.IsNullOrWhiteSpace(attachment.FilePath))
                {
                    try { await _fileStorageService.DeleteFileAsync(attachment.FilePath); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Could not delete file {FilePath}", attachment.FilePath); }
                }

                await _leaveService.DeleteAttachmentAsync(attachmentId);
                return Ok(new { message = "Attachment deleted" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting attachment {AttachmentId}", attachmentId);
                return StatusCode(500, "An error occurred while deleting the attachment");
            }
        }
    }
}