using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance
{
    /// <summary>
    /// Manages cost allocation rules and executes overhead distribution across departments, cost centres, and projects.
    /// </summary>
    /// <remarks>
    /// **Common Use Cases:**
    /// - Define rules for distributing indirect costs (e.g., rent, utilities, IT overhead) to cost centres or departments
    /// - Execute periodic cost allocations at month-end or quarter-end as part of the financial close process
    /// - Maintain and version allocation bases such as headcount, square footage, or revenue proportions
    ///
    /// **Integration Pattern:**
    /// - Works alongside the General Ledger module to post allocation journal entries automatically
    /// - References cost centres and departments defined in the Chart of Accounts and organisational structure
    /// - Allocation results feed into management reporting, budgeting, and variance analysis modules
    ///
    /// **Business Rules:**
    /// - Each allocation rule must have a unique code to prevent duplicate overhead distribution configurations
    /// - Only active rules can be executed; inactive rules are retained for audit history but are skipped during runs
    /// - Allocation percentages or basis weights defined in a rule must sum correctly to ensure balanced distribution
    ///
    /// **Authorization:** Requires authenticated access; all endpoints are protected by the [Authorize] attribute
    /// </remarks>
    [Authorize]
    [ApiController]
    [Route("api/finance/allocations")]
    public class AllocationController : ControllerBase
    {
        private readonly IAllocationService _allocationService;

        public AllocationController(IAllocationService allocationService)
        {
            _allocationService = allocationService;
        }

        /// <summary>
        /// Retrieves every allocation rule in the system, including both active and inactive rules.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Populate an administration screen listing all configured cost allocation rules
        /// - Export the full rule set for audit review or external compliance documentation
        /// - Provide a master list for bulk status updates or rule clean-up exercises
        ///
        /// **Integration Pattern:**
        /// - Typically called by the Allocation Rules management page in the Finance module UI
        /// - Results can be filtered client-side or used to seed dropdown selectors in other finance screens
        ///
        /// **Business Rules:**
        /// - Returns all rules regardless of their active/inactive status
        /// - Rules are returned as read-only DTOs; modifications require the dedicated update endpoint
        ///
        /// **Authorization:** Requires authenticated access
        /// </remarks>
        /// <returns>A list of all allocation rule DTOs, including active and inactive rules.</returns>
        /// <response code="200">Successfully retrieved the complete list of allocation rules</response>
        /// <response code="401">Not authenticated - valid credentials are required</response>
        /// <response code="500">Internal server error during rule retrieval</response>
        [HttpGet("rules")]
        public async Task<ActionResult<IReadOnlyList<AllocationRuleDto>>> GetAllRules()
        {
            var rules = await _allocationService.GetAllRulesAsync();
            return Ok(rules);
        }

        /// <summary>
        /// Retrieves only the allocation rules that are currently marked as active.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Populate a run-allocation dialog with only the rules eligible for execution
        /// - Display the current active overhead distribution configuration for finance managers
        /// - Feed automated month-end allocation batch jobs with the set of rules to process
        ///
        /// **Integration Pattern:**
        /// - Called before executing allocations to determine which rules are eligible
        /// - Used by scheduled jobs and the period-end close workflow to identify runnable rules
        ///
        /// **Business Rules:**
        /// - Only rules with an active status are included; deactivated or draft rules are excluded
        /// - Active rules are expected to have valid allocation bases and target account mappings
        ///
        /// **Authorization:** Requires authenticated access
        /// </remarks>
        /// <returns>A filtered list containing only active allocation rule DTOs.</returns>
        /// <response code="200">Successfully retrieved the list of active allocation rules</response>
        /// <response code="401">Not authenticated - valid credentials are required</response>
        /// <response code="500">Internal server error during active rule retrieval</response>
        [HttpGet("rules/active")]
        public async Task<ActionResult<IReadOnlyList<AllocationRuleDto>>> GetActiveRules()
        {
            var rules = await _allocationService.GetActiveRulesAsync();
            return Ok(rules);
        }

        /// <summary>
        /// Retrieves a single allocation rule by its unique identifier.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Load full rule details into an edit form for modifying allocation percentages or target accounts
        /// - Display a detailed read-only view of a specific allocation rule for review or approval
        /// - Fetch rule metadata before executing a targeted allocation run
        ///
        /// **Integration Pattern:**
        /// - Called from the rule detail/edit page after selecting a rule from the master list
        /// - Used by the run-allocation endpoint pre-check to validate rule existence
        ///
        /// **Business Rules:**
        /// - Returns 404 if no rule exists with the specified ID
        /// - The returned DTO includes all line items, basis weights, and target account mappings
        ///
        /// **Authorization:** Requires authenticated access
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the allocation rule to retrieve.</param>
        /// <returns>The allocation rule DTO matching the specified ID.</returns>
        /// <response code="200">Successfully retrieved the allocation rule</response>
        /// <response code="401">Not authenticated - valid credentials are required</response>
        /// <response code="404">No allocation rule found with the specified ID</response>
        /// <response code="500">Internal server error during rule lookup</response>
        [HttpGet("rules/{id}")]
        public async Task<ActionResult<AllocationRuleDto>> GetRuleById(Guid id)
        {
            var rule = await _allocationService.GetRuleByIdAsync(id);
            if (rule == null)
                return NotFound(new { error = $"Allocation rule with ID '{id}' not found" });
            return Ok(rule);
        }

        /// <summary>
        /// Retrieves a single allocation rule by its unique business code.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Look up a rule using a human-readable code (e.g., "RENT-ALLOC-HQ") instead of a system GUID
        /// - Integrate with external systems or spreadsheets that reference allocation rules by code
        /// - Validate that a specific rule code exists before importing allocation configurations
        ///
        /// **Integration Pattern:**
        /// - Commonly used by import utilities and external integrations that identify rules by code
        /// - Supports cross-referencing allocation rules with budget templates that use rule codes
        ///
        /// **Business Rules:**
        /// - Rule codes are unique across the system; each code maps to exactly one rule
        /// - Returns 404 if no rule exists with the specified code
        ///
        /// **Authorization:** Requires authenticated access
        /// </remarks>
        /// <param name="code">The unique business code of the allocation rule (e.g., "RENT-ALLOC-HQ").</param>
        /// <returns>The allocation rule DTO matching the specified code.</returns>
        /// <response code="200">Successfully retrieved the allocation rule</response>
        /// <response code="401">Not authenticated - valid credentials are required</response>
        /// <response code="404">No allocation rule found with the specified code</response>
        /// <response code="500">Internal server error during rule lookup</response>
        [HttpGet("rules/by-code/{code}")]
        public async Task<ActionResult<AllocationRuleDto>> GetRuleByCode(string code)
        {
            var rule = await _allocationService.GetRuleByCodeAsync(code);
            if (rule == null)
                return NotFound(new { error = $"Allocation rule with code '{code}' not found" });
            return Ok(rule);
        }

        /// <summary>
        /// Creates a new cost allocation rule defining how indirect costs are distributed to target accounts.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Set up a new overhead allocation rule when a new cost centre or department is established
        /// - Define a rule for distributing shared service costs (e.g., IT, HR) based on headcount or revenue
        /// - Configure project-based allocation rules for grant-funded or client-billable overhead recovery
        ///
        /// **Integration Pattern:**
        /// - The created rule becomes available for execution via the run-allocation endpoint
        /// - New rules are created in an active or inactive state as specified in the DTO
        /// - Returns a Location header pointing to the newly created rule for RESTful resource discovery
        ///
        /// **Business Rules:**
        /// - Rule code must be unique; duplicate codes are rejected with a 400 error
        /// - Allocation basis weights or percentages must be valid and internally consistent
        /// - Source and target account references must point to existing Chart of Accounts entries
        ///
        /// **Authorization:** Requires authenticated access
        /// </remarks>
        /// <param name="dto">The allocation rule creation payload containing code, name, basis type, source/target accounts, and distribution weights.</param>
        /// <returns>The newly created allocation rule DTO with its assigned ID.</returns>
        /// <response code="201">Allocation rule created successfully; Location header contains the resource URI</response>
        /// <response code="400">Validation failure - duplicate code, invalid percentages, or missing required fields</response>
        /// <response code="401">Not authenticated - valid credentials are required</response>
        /// <response code="500">Internal server error during rule creation</response>
        [HttpPost("rules")]
        public async Task<ActionResult<AllocationRuleDto>> CreateRule([FromBody] CreateAllocationRuleDto dto)
        {
            try
            {
                var rule = await _allocationService.CreateRuleAsync(dto);
                return CreatedAtAction(nameof(GetRuleById), new { id = rule.Id }, rule);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Updates an existing allocation rule with revised distribution parameters or metadata.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Adjust allocation percentages when departmental headcount or square footage changes
        /// - Update target accounts after a Chart of Accounts restructuring
        /// - Rename or re-describe a rule for improved clarity in financial reports
        ///
        /// **Integration Pattern:**
        /// - The updated rule takes effect on the next allocation run; previously posted entries are not reversed
        /// - Pairs with the GET by ID endpoint for a load-edit-save workflow on the rule detail page
        ///
        /// **Business Rules:**
        /// - The rule must exist; a 404 is returned if the specified ID does not match any rule
        /// - Modifying an active rule does not retroactively affect prior allocation journal entries
        /// - Allocation basis weights or percentages must remain valid after the update
        ///
        /// **Authorization:** Requires authenticated access
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the allocation rule to update.</param>
        /// <param name="dto">The update payload containing the revised rule properties.</param>
        /// <returns>The updated allocation rule DTO reflecting the applied changes.</returns>
        /// <response code="200">Allocation rule updated successfully</response>
        /// <response code="400">Validation failure - invalid field values or business rule violation</response>
        /// <response code="401">Not authenticated - valid credentials are required</response>
        /// <response code="404">No allocation rule found with the specified ID</response>
        /// <response code="500">Internal server error during rule update</response>
        [HttpPut("rules/{id}")]
        public async Task<ActionResult<AllocationRuleDto>> UpdateRule(Guid id, [FromBody] UpdateAllocationRuleDto dto)
        {
            try
            {
                var rule = await _allocationService.UpdateRuleAsync(id, dto);
                return Ok(rule);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves the target account distribution for an allocation rule.
        /// </summary>
        /// <response code="200">Returns the rule's allocation targets</response>
        /// <response code="404">No allocation rule found with the specified ID</response>
        [HttpGet("rules/{id}/targets")]
        public async Task<ActionResult<IReadOnlyList<AllocationTargetDto>>> GetRuleTargets(Guid id)
        {
            var rule = await _allocationService.GetRuleByIdAsync(id);
            if (rule == null)
                return NotFound(new { error = $"Allocation rule with ID '{id}' not found" });
            return Ok(rule.Targets);
        }

        /// <summary>
        /// Replaces the target account distribution of an allocation rule.
        /// </summary>
        /// <remarks>
        /// Targets are part of the rule contract, so this composes the existing rule update
        /// (and its percentage/driver validation) with the submitted target list only.
        /// </remarks>
        /// <response code="200">Returns the updated allocation targets</response>
        /// <response code="400">Invalid targets - percentages or account references failed validation</response>
        /// <response code="404">No allocation rule found with the specified ID</response>
        [HttpPut("rules/{id}/targets")]
        public async Task<ActionResult<IReadOnlyList<AllocationTargetDto>>> UpdateRuleTargets(
            Guid id,
            [FromBody] List<CreateAllocationTargetDto> targets)
        {
            var rule = await _allocationService.GetRuleByIdAsync(id);
            if (rule == null)
                return NotFound(new { error = $"Allocation rule with ID '{id}' not found" });

            try
            {
                var updated = await _allocationService.UpdateRuleAsync(id, new UpdateAllocationRuleDto(
                    rule.Name,
                    rule.Description,
                    rule.SourceAccountId,
                    rule.AllocationType,
                    rule.DriverUnitAccountId,
                    rule.IsActive,
                    rule.AutoReverse,
                    targets));
                return Ok(updated.Targets);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves controlled allocation run batches.
        /// </summary>
        [HttpGet("runs")]
        public async Task<ActionResult<IReadOnlyList<AllocationRunBatchDto>>> GetRunBatches([FromQuery] string? status = null)
        {
            try
            {
                return Ok(await _allocationService.GetRunBatchesAsync(status));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves one controlled allocation run batch with its approved preview lines.
        /// </summary>
        [HttpGet("runs/{id}")]
        public async Task<ActionResult<AllocationRunBatchDto>> GetRunBatch(Guid id)
        {
            var batch = await _allocationService.GetRunBatchByIdAsync(id);
            if (batch == null)
                return NotFound(new { error = $"Allocation run batch with ID '{id}' not found" });

            return Ok(batch);
        }

        /// <summary>
        /// Calculates a draft allocation run batch for review and workflow approval.
        /// </summary>
        [HttpPost("rules/{id}/runs")]
        public async Task<ActionResult<AllocationRunBatchDto>> CreateRunBatch(Guid id, [FromBody] CreateAllocationRunBatchDto dto)
        {
            try
            {
                if (id != dto.AllocationRuleId)
                {
                    dto = dto with { AllocationRuleId = id };
                }

                var batch = await _allocationService.CreateRunBatchAsync(dto);
                return CreatedAtAction(nameof(GetRunBatch), new { id = batch.Id }, batch);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Submits a draft allocation run batch into approval workflow.
        /// </summary>
        [HttpPost("runs/{id}/submit")]
        public async Task<ActionResult<AllocationRunBatchDto>> SubmitRunBatch(Guid id, [FromBody] SubmitAllocationRunBatchDto? dto)
        {
            try
            {
                return Ok(await _allocationService.SubmitRunBatchAsync(id, dto?.Comment));
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Approves the current workflow step for an allocation run batch.
        /// </summary>
        [HttpPost("runs/{id}/approve")]
        public async Task<ActionResult<AllocationRunBatchDto>> ApproveRunBatch(Guid id, [FromBody] SubmitAllocationRunBatchDto? dto)
        {
            try
            {
                return Ok(await _allocationService.ApproveRunBatchAsync(id, dto?.Comment));
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Rejects the current workflow step for an allocation run batch.
        /// </summary>
        [HttpPost("runs/{id}/reject")]
        public async Task<ActionResult<AllocationRunBatchDto>> RejectRunBatch(Guid id, [FromBody] RejectAllocationRunBatchDto dto)
        {
            try
            {
                return Ok(await _allocationService.RejectRunBatchAsync(id, dto.Reason));
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Posts an approved allocation run batch to GL using its approved snapshot lines.
        /// </summary>
        [HttpPost("runs/{id}/post")]
        public async Task<ActionResult<AllocationRunBatchDto>> PostRunBatch(Guid id)
        {
            try
            {
                return Ok(await _allocationService.PostRunBatchAsync(id));
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Permanently deletes an allocation rule from the system.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Remove obsolete rules that are no longer needed after an organisational restructure
        /// - Clean up draft or test rules created during initial allocation configuration
        /// - Delete duplicate rules that were created in error
        ///
        /// **Integration Pattern:**
        /// - Deletion is permanent; consider deactivating instead if audit trail retention is required
        /// - Previously posted allocation journal entries from this rule are not reversed upon deletion
        ///
        /// **Business Rules:**
        /// - The rule is permanently removed; this action cannot be undone
        /// - Deleting a rule does not reverse or void any allocation entries already posted to the General Ledger
        /// - Consider using the deactivate endpoint instead if the rule may be needed for historical reference
        ///
        /// **Authorization:** Requires authenticated access
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the allocation rule to delete.</param>
        /// <returns>No content on successful deletion.</returns>
        /// <response code="204">Allocation rule deleted successfully</response>
        /// <response code="401">Not authenticated - valid credentials are required</response>
        /// <response code="404">No allocation rule found with the specified ID</response>
        /// <response code="500">Internal server error during rule deletion</response>
        [HttpDelete("rules/{id}")]
        public async Task<ActionResult> DeleteRule(Guid id)
        {
            await _allocationService.DeleteRuleAsync(id);
            return NoContent();
        }

        /// <summary>
        /// Activates an allocation rule, making it eligible for execution during allocation runs.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Enable a newly configured rule after it has been reviewed and approved by a finance manager
        /// - Re-enable a previously deactivated rule when it becomes relevant again (e.g., seasonal cost centres)
        /// - Activate rules as part of a period-end preparation checklist
        ///
        /// **Integration Pattern:**
        /// - Once activated, the rule appears in the active rules list and can be selected for allocation runs
        /// - Works in tandem with the deactivate endpoint to toggle rule availability without deleting configuration
        ///
        /// **Business Rules:**
        /// - Only existing rules can be activated; a 404 is returned if the rule ID is not found
        /// - Activating an already-active rule is idempotent and returns the current rule state
        /// - The rule must have valid configuration (source accounts, targets, weights) to be meaningful when active
        ///
        /// **Authorization:** Requires authenticated access
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the allocation rule to activate.</param>
        /// <returns>The updated allocation rule DTO with its status set to active.</returns>
        /// <response code="200">Allocation rule activated successfully</response>
        /// <response code="401">Not authenticated - valid credentials are required</response>
        /// <response code="404">No allocation rule found with the specified ID</response>
        /// <response code="500">Internal server error during rule activation</response>
        [HttpPatch("rules/{id}/activate")]
        public async Task<ActionResult<AllocationRuleDto>> ActivateRule(Guid id)
        {
            try
            {
                var rule = await _allocationService.ActivateRuleAsync(id);
                return Ok(rule);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Deactivates an allocation rule, preventing it from being executed in future allocation runs.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Temporarily suspend an allocation rule during organisational changes without losing its configuration
        /// - Disable a seasonal rule (e.g., holiday-period overhead) outside its applicable period
        /// - Retire a rule at fiscal year-end while preserving it for historical audit reference
        ///
        /// **Integration Pattern:**
        /// - Deactivated rules are excluded from the active rules list and cannot be selected for allocation runs
        /// - Pairs with the activate endpoint to provide a reversible enable/disable workflow
        ///
        /// **Business Rules:**
        /// - Only existing rules can be deactivated; a 404 is returned if the rule ID is not found
        /// - Deactivating an already-inactive rule is idempotent and returns the current rule state
        /// - Previously posted allocation entries from this rule remain unaffected in the General Ledger
        ///
        /// **Authorization:** Requires authenticated access
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the allocation rule to deactivate.</param>
        /// <returns>The updated allocation rule DTO with its status set to inactive.</returns>
        /// <response code="200">Allocation rule deactivated successfully</response>
        /// <response code="401">Not authenticated - valid credentials are required</response>
        /// <response code="404">No allocation rule found with the specified ID</response>
        /// <response code="500">Internal server error during rule deactivation</response>
        [HttpPatch("rules/{id}/deactivate")]
        public async Task<ActionResult<AllocationRuleDto>> DeactivateRule(Guid id)
        {
            try
            {
                var rule = await _allocationService.DeactivateRuleAsync(id);
                return Ok(rule);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Executes a cost allocation run for a specified rule, distributing costs from source accounts to target accounts.
        /// </summary>
        /// <remarks>
        /// **Common Use Cases:**
        /// - Run month-end overhead allocation to distribute shared costs (rent, utilities, admin) across departments
        /// - Execute a one-off allocation for a specific cost pool as part of ad-hoc management reporting
        /// - Process period-end cost distribution as a step in the financial close workflow
        ///
        /// **Integration Pattern:**
        /// - Posts allocation journal entries to the General Ledger based on the rule's defined distribution weights
        /// - The allocation result contains a summary of amounts distributed, target accounts debited/credited, and any warnings
        /// - The route ID is authoritative; if the request body contains a different rule ID, it is overridden to match the route
        /// - Feeds downstream into management reports, cost centre profitability analysis, and budget variance calculations
        ///
        /// **Business Rules:**
        /// - The allocation rule must exist and be in an active state; inactive or missing rules result in an error
        /// - The allocation period and parameters specified in the DTO must be valid and not overlap with previously completed runs (if enforced)
        /// - The route parameter ID takes precedence over the AllocationRuleId in the request body to ensure consistency
        /// - Allocation amounts are calculated based on the rule's defined basis (percentage, headcount, revenue, etc.)
        ///
        /// **Authorization:** Requires authenticated access
        /// </remarks>
        /// <param name="id">The unique identifier (GUID) of the allocation rule to execute. Overrides the AllocationRuleId in the request body if they differ.</param>
        /// <param name="dto">The allocation run parameters including the period, effective date, and any run-specific overrides.</param>
        /// <returns>An allocation result DTO containing the distribution summary, posted journal entry references, and any warnings.</returns>
        /// <response code="200">Allocation executed successfully with distribution results returned</response>
        /// <response code="400">Validation failure - rule is inactive, invalid period, or business rule violation</response>
        /// <response code="401">Not authenticated - valid credentials are required</response>
        /// <response code="404">No allocation rule found with the specified ID</response>
        /// <response code="500">Internal server error during allocation execution</response>
        [HttpPost("rules/{id}/run")]
        public async Task<ActionResult<AllocationResultDto>> RunAllocation(Guid id, [FromBody] RunAllocationDto dto)
        {
            try
            {
                // Ensure the ID in the route matches the DTO
                if (id != dto.AllocationRuleId)
                {
                    dto = dto with { AllocationRuleId = id };
                }

                var result = await _allocationService.RunAllocationAsync(dto);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
