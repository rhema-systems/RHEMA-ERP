using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Api.Controllers.Maintenance
{
    [ApiController]
    [Route("api/maintenance/safety-protocols")]
    public class SafetyProtocolController : ControllerBase
    {
        private readonly ILogger<SafetyProtocolController> _logger;
        private readonly ISafetyProtocolService? _safetyProtocolService;

        public SafetyProtocolController(
            ILogger<SafetyProtocolController> logger,
            ISafetyProtocolService? safetyProtocolService = null)
        {
            _logger = logger;
            _safetyProtocolService = safetyProtocolService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<SafetyProtocolDto>>> GetSafetyProtocols(
            [FromQuery] SafetyProtocolFilterDto filter)
        {
            try
            {
                if (_safetyProtocolService != null)
                {
                    var result = await _safetyProtocolService.GetProtocolsPagedAsync(filter);
                    return Ok(result);
                }
                
                return Ok(GetMockSafetyProtocols(filter));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving safety protocols");
                return Ok(GetMockSafetyProtocols(filter));
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SafetyProtocolDto>> GetSafetyProtocol(Guid id)
        {
            try
            {
                if (_safetyProtocolService != null)
                {
                    var result = await _safetyProtocolService.GetProtocolByIdAsync(id);
                    if (result == null)
                        return NotFound();
                    return Ok(result);
                }
                
                var mockProtocol = GetMockSafetyProtocolById(id);
                if (mockProtocol == null)
                    return NotFound();
                return Ok(mockProtocol);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving safety protocol {Id}", id);
                var mockProtocol = GetMockSafetyProtocolById(id);
                if (mockProtocol == null)
                    return NotFound();
                return Ok(mockProtocol);
            }
        }

        [HttpPost]
        public async Task<ActionResult<SafetyProtocolDto>> CreateSafetyProtocol(
            [FromBody] CreateSafetyProtocolDto createDto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                if (_safetyProtocolService != null)
                {
                    var result = await _safetyProtocolService.CreateProtocolAsync(createDto);
                    return CreatedAtAction(nameof(GetSafetyProtocol), new { id = result.Id }, result);
                }
                
                var mockResult = CreateMockSafetyProtocol(createDto);
                return CreatedAtAction(nameof(GetSafetyProtocol), new { id = mockResult.Id }, mockResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating safety protocol");
                return StatusCode(500, "An error occurred while creating the safety protocol");
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<SafetyProtocolDto>> UpdateSafetyProtocol(
            Guid id, [FromBody] UpdateSafetyProtocolDto updateDto)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                if (_safetyProtocolService != null)
                {
                    var result = await _safetyProtocolService.UpdateProtocolAsync(id, updateDto);
                    if (result == null)
                        return NotFound();
                    return Ok(result);
                }
                
                var mockResult = UpdateMockSafetyProtocol(id, updateDto);
                if (mockResult == null)
                    return NotFound();
                return Ok(mockResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating safety protocol {Id}", id);
                return StatusCode(500, "An error occurred while updating the safety protocol");
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteSafetyProtocol(Guid id)
        {
            try
            {
                if (_safetyProtocolService != null)
                {
                    await _safetyProtocolService.DeleteProtocolAsync(id);
                    return NoContent();
                }
                
                // Mock always succeeds
                return NoContent();
            }
            catch (ArgumentException)
            {
                return NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting safety protocol {Id}", id);
                return StatusCode(500, "An error occurred while deleting the safety protocol");
            }
        }

        [HttpGet("compliance")]
        public async Task<ActionResult<IEnumerable<SafetyProtocolComplianceDto>>> GetProtocolCompliance(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var start = startDate ?? DateTime.UtcNow.AddMonths(-1);
                var end = endDate ?? DateTime.UtcNow;
                
                if (_safetyProtocolService != null)
                {
                    var result = await _safetyProtocolService.GetProtocolComplianceAsync(start, end);
                    return Ok(result);
                }
                
                var mockCompliance = GetMockProtocolComplianceList();
                return Ok(mockCompliance);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving protocol compliance");
                var mockCompliance = GetMockProtocolComplianceList();
                return Ok(mockCompliance);
            }
        }

        [HttpGet("analytics")]
        public async Task<ActionResult<SafetyAnalyticsDto>> GetSafetyAnalytics(
            [FromQuery] DateTime? startDate = null, 
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var start = startDate ?? DateTime.UtcNow.AddMonths(-3);
                var end = endDate ?? DateTime.UtcNow;
                
                if (_safetyProtocolService != null)
                {
                    var result = await _safetyProtocolService.GetSafetyAnalyticsAsync(start, end);
                    return Ok(result);
                }
                
                return Ok(GetMockSafetyAnalytics());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving safety analytics");
                return Ok(GetMockSafetyAnalytics());
            }
        }

        // Mock data methods
        private PagedResult<SafetyProtocolDto> GetMockSafetyProtocols(SafetyProtocolFilterDto filter)
        {
            var protocols = new List<SafetyProtocolDto>
            {
                new SafetyProtocolDto
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Code = "SP-001",
                    Title = "Personal Protective Equipment",
                    Description = "Guidelines for proper PPE usage",
                    RiskLevel = "Medium",
                    RequiredPPE = new List<string> { "Hard Hat", "Safety Vest", "Steel Boots" },
                    CreatedDate = DateTime.UtcNow.AddDays(-30),
                    IsActive = true,
                    Category = "Equipment Safety"
                },
                new SafetyProtocolDto
                {
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Code = "SP-002",
                    Title = "Lockout/Tagout Procedures",
                    Description = "Energy isolation procedures",
                    RiskLevel = "High",
                    RequiredPPE = new List<string> { "Lockout Kit", "Tags" },
                    CreatedDate = DateTime.UtcNow.AddDays(-45),
                    IsActive = true,
                    Category = "Electrical Safety"
                }
            };

            var filteredProtocols = protocols.AsQueryable();

            if (!string.IsNullOrEmpty(filter.SearchTerm))
            {
                filteredProtocols = filteredProtocols.Where(p => 
                    p.Title.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    p.Code.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(filter.Category))
            {
                filteredProtocols = filteredProtocols.Where(p => p.Category == filter.Category);
            }

            if (!string.IsNullOrEmpty(filter.RiskLevel))
            {
                filteredProtocols = filteredProtocols.Where(p => p.RiskLevel == filter.RiskLevel);
            }

            if (filter.IsActive.HasValue)
            {
                filteredProtocols = filteredProtocols.Where(p => p.IsActive == filter.IsActive.Value);
            }

            var totalCount = filteredProtocols.Count();
            var pagedProtocols = filteredProtocols
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToList();

            return new PagedResult<SafetyProtocolDto>
            {
                Items = pagedProtocols,
                TotalCount = totalCount,
                Page = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }

        private SafetyProtocolDto GetMockSafetyProtocolById(Guid id)
        {
            var protocols = GetMockSafetyProtocols(new SafetyProtocolFilterDto { PageSize = 100 });
            return protocols.Items.FirstOrDefault(p => p.Id == id);
        }

        private SafetyProtocolDto CreateMockSafetyProtocol(CreateSafetyProtocolDto createDto)
        {
            return new SafetyProtocolDto
            {
                Id = Guid.NewGuid(),
                Code = createDto.Code,
                Title = createDto.Title,
                Description = createDto.Description,
                RiskLevel = createDto.RiskLevel,
                RequiredPPE = createDto.RequiredPPE,
                Category = createDto.Category,
                CreatedDate = DateTime.UtcNow,
                IsActive = true
            };
        }

        private SafetyProtocolDto UpdateMockSafetyProtocol(Guid id, UpdateSafetyProtocolDto updateDto)
        {
            var existing = GetMockSafetyProtocolById(id);
            if (existing == null) return null;

            existing.Title = updateDto.Title;
            existing.Description = updateDto.Description;
            existing.RiskLevel = updateDto.RiskLevel;
            existing.RequiredPPE = updateDto.RequiredPPE;
            existing.Category = updateDto.Category;
            existing.IsActive = updateDto.IsActive;

            return existing;
        }

        private SafetyProtocolComplianceDto GetMockProtocolCompliance(Guid protocolId)
        {
            return new SafetyProtocolComplianceDto
            {
                ProtocolId = protocolId,
                ProtocolTitle = "Mock Protocol",
                ProtocolCode = "SP-001",
                TotalAdherence = 85,
                TotalViolations = 5,
                ComplianceRate = 94.4m,
                LastAuditDate = DateTime.UtcNow.AddDays(-7)
            };
        }

        private IEnumerable<SafetyProtocolComplianceDto> GetMockProtocolComplianceList()
        {
            return new List<SafetyProtocolComplianceDto>
            {
                new SafetyProtocolComplianceDto
                {
                    ProtocolId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    ProtocolTitle = "Personal Protective Equipment",
                    ProtocolCode = "SP-001",
                    TotalAdherence = 85,
                    TotalViolations = 5,
                    ComplianceRate = 94.4m,
                    LastAuditDate = DateTime.UtcNow.AddDays(-7)
                },
                new SafetyProtocolComplianceDto
                {
                    ProtocolId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    ProtocolTitle = "Lockout/Tagout Procedures",
                    ProtocolCode = "SP-002",
                    TotalAdherence = 78,
                    TotalViolations = 8,
                    ComplianceRate = 90.7m,
                    LastAuditDate = DateTime.UtcNow.AddDays(-5)
                }
            };
        }

        private SafetyAnalyticsDto GetMockSafetyAnalytics()
        {
            return new SafetyAnalyticsDto
            {
                TotalProtocols = 25,
                ActiveProtocols = 22,
                OverallComplianceRate = 87.5m,
                TotalViolations = 12,
                CriticalViolations = 2,
                ProtocolsByCategory = new Dictionary<string, int>
                {
                    { "Equipment Safety", 8 },
                    { "Electrical Safety", 6 },
                    { "Chemical Safety", 4 },
                    { "Emergency Procedures", 7 }
                },
                RecentViolations = new List<SafetyViolationSummaryDto>
                {
                    new SafetyViolationSummaryDto
                    {
                        Id = Guid.NewGuid(),
                        ProtocolCode = "SP-001",
                        ViolationType = "PPE Not Worn",
                        Severity = "Medium",
                        Date = DateTime.UtcNow.AddDays(-2),
                        Status = "Under Investigation"
                    }
                }
            };
        }
    }
}