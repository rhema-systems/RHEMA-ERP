using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Finance
{
    [Authorize]
    [ApiController]
    [Route("api/finance/ap")]
    public class ApSupplierReturnsController : ControllerBase
    {
        private readonly ISupplierReturnService _returnService;

        public ApSupplierReturnsController(ISupplierReturnService returnService)
        {
            _returnService = returnService;
        }

        [HttpGet("supplier-returns")]
        public async Task<ActionResult<IEnumerable<SupplierReturn>>> GetAll()
        {
            return Ok(await _returnService.GetAllAsync());
        }

        [HttpGet("supplier-returns/{id}")]
        public async Task<ActionResult<SupplierReturn>> GetById(Guid id)
        {
            try
            {
                var ret = await _returnService.GetByIdAsync(id);
                return Ok(ret);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        [HttpPost("supplier-returns")]
        public async Task<ActionResult<SupplierReturn>> Create([FromBody] CreateSupplierReturnDto dto)
        {
            try
            {
                var created = await _returnService.CreateReturnAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("supplier-returns/{id}/approve")]
        public async Task<ActionResult<SupplierReturn>> Approve(Guid id)
        {
            try
            {
                var approved = await _returnService.ApproveReturnAsync(id);
                return Ok(approved);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }
}
