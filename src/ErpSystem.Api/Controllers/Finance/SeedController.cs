using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Data;
using ErpSystem.Core.Entities.Finance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Finance
{
    [ApiController]
    [Route("api/[controller]")]
    public class SeedController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SeedController> _logger;

        public SeedController(ApplicationDbContext context, ILogger<SeedController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost("department-lookup")]
        [AllowAnonymous] // For testing only - remove in production
        public async Task<IActionResult> SeedDepartmentLookup()
        {
            try
            {
                // Get DEFAULT tenant
                var tenant = await _context.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT");
                if (tenant == null)
                {
                    return BadRequest(new { error = "DEFAULT tenant not found" });
                }

                // Get the first segment structure (should be Department)
                var segmentStructure = await _context.AccountSegmentStructures
                    .Where(s => s.TenantId == tenant.Id)
                    .OrderBy(s => s.SegmentPosition)
                    .FirstOrDefaultAsync();

                if (segmentStructure == null)
                {
                    return BadRequest(new { error = "No segment structure found for DEFAULT tenant" });
                }

                // Delete existing lookup values for this segment
                var existing = await _context.SegmentLookupValues
                    .Where(v => v.SegmentStructureId == segmentStructure.Id)
                    .ToListAsync();
                
                if (existing.Any())
                {
                    _context.SegmentLookupValues.RemoveRange(existing);
                    await _context.SaveChangesAsync();
                }

                // Create department lookup values (3-digit codes)
                var departments = new[]
                {
                    new { Code = "100", Name = "General Administration" },
                    new { Code = "200", Name = "Finance Department" },
                    new { Code = "300", Name = "Human Resources" },
                    new { Code = "400", Name = "Information Technology" },
                    new { Code = "500", Name = "Sales Department" },
                    new { Code = "600", Name = "Marketing Department" },
                    new { Code = "700", Name = "Operations" },
                    new { Code = "800", Name = "Customer Service" },
                    new { Code = "900", Name = "Research & Development" }
                };

                var lookupValues = new List<SegmentLookupValue>();
                int displayOrder = 1;

                foreach (var dept in departments)
                {
                    lookupValues.Add(new SegmentLookupValue
                    {
                        Id = Guid.NewGuid(),
                        SegmentStructureId = segmentStructure.Id,
                        SegmentValue = dept.Code,
                        Description = dept.Name,
                        EffectiveDate = DateTime.UtcNow,
                        IsActive = true,
                        DisplayOrder = displayOrder++,
                        TenantId = tenant.Id,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = "00000000-0000-0000-0000-000000000001" // Admin user ID as string
                    });
                }

                await _context.SegmentLookupValues.AddRangeAsync(lookupValues);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = $"Successfully seeded {lookupValues.Count} department lookup values",
                    segmentStructure = new
                    {
                        segmentStructure.Id,
                        segmentStructure.SegmentName,
                        segmentStructure.SegmentLength,
                        segmentStructure.LookupTableRequired
                    },
                    values = lookupValues.Select(v => new
                    {
                        v.SegmentValue,
                        v.Description,
                        v.IsActive
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding department lookup values");
                return StatusCode(500, new { error = "An error occurred while seeding department lookup values", details = ex.Message });
            }
        }
    }
}
