using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.DTOs.Common;

namespace ErpSystem.Api.Controllers.HR
{
    /// <summary>
    /// Leave management endpoints
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LeavesController : ControllerBase
    {
        private readonly ILeaveService _leaveService;
        private readonly ILogger<LeavesController> _logger;

        public LeavesController(ILeaveService leaveService, ILogger<LeavesController> logger)
        {
            _leaveService = leaveService;
            _logger = logger;
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
            try
            {
                var application = await _leaveService.GetLeaveRequestByIdAsync(id);
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
            [FromQuery] int pageSize = 20)
        {
            if (year == 0)
            {
                year = DateTime.Today.Year;
            }

            var result = await _leaveService.GetEmployeeLeaveHistoryAsync(employeeId, year, pageNumber, pageSize);

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
            if (year == 0)
            {
                year = DateTime.Today.Year;
            }

            var balances = await _leaveService.GetEmployeeLeaveBalancesAsync(employeeId, year);

            return Ok(balances);
        }

        /// <summary>
        /// Get pending leave approvals for a manager
        /// </summary>
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
            var result = await _leaveService.GetPendingApprovalsAsync(managerId, pageNumber, pageSize);

            return Ok(result);
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
        public async Task<IActionResult> CancelLeave(Guid id, [FromBody] string cancellationReason)
        {
            try
            {
                await _leaveService.CancelLeaveRequestAsync(id, cancellationReason);
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
        /// Close a completed leave application
        /// </summary>
        /// <param name="id">Leave application ID</param>
        /// <param name="dto">Closure details</param>
        /// <returns>Updated leave application</returns>
        /// <response code="200">Leave closed successfully</response>
        /// <response code="400">Invalid request or business rule violation</response>
        /// <response code="404">Leave application not found</response>
        [HttpPut("{id}/close")]
        [ProducesResponseType(typeof(LeaveRequestDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<LeaveRequestDto>> CloseLeave(Guid id, [FromBody] CloseLeaveDto dto)
        {
            try
            {
                var application = await _leaveService.CloseLeaveRequestAsync(id, dto);
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
    }
}