using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Data;
using ErpSystem.Core.Entities.Finance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.Finance
{
    /// <summary>
    /// Provides seed data endpoints for populating the finance module with initial reference data during development and testing.
    /// </summary>
    /// <remarks>
    /// **WARNING: This controller is intended for development and testing environments ONLY.
    /// The [AllowAnonymous] attribute bypasses all authentication. Do NOT deploy to production without
    /// removing or securing these endpoints behind administrative authorization.**
    ///
    /// **Domain Responsibility:**
    /// - Seeds account segment lookup values (e.g., department codes) into the Chart of Accounts segment structure
    /// - Establishes foundational reference data required by the multi-segment account coding system
    ///
    /// **Integration Pattern:**
    /// - Operates directly on <see cref="ApplicationDbContext"/> to insert seed records
    /// - Populates <see cref="SegmentLookupValue"/> entities tied to <see cref="AccountSegmentStructure"/>
    /// - Seeded data is consumed by the Chart of Accounts, Journal Entry, and General Ledger modules
    ///   when users select segment values during transaction entry
    ///
    /// **Lifecycle:**
    /// - Designed to be called once per environment setup; re-calling will replace existing lookup values
    /// - Targets the DEFAULT tenant only
    /// </remarks>
    [ApiController]
    [Route("api/[controller]")]
    public class SeedController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SeedController> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="SeedController"/> with required dependencies.
        /// </summary>
        /// <param name="context">The application database context for direct entity manipulation.</param>
        /// <param name="logger">Logger instance for capturing seed operation diagnostics and errors.</param>
        public SeedController(ApplicationDbContext context, ILogger<SeedController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Seeds the department segment lookup values for the DEFAULT tenant's first account segment structure.
        /// </summary>
        /// <remarks>
        /// **WARNING: Development/testing endpoint only. Marked [AllowAnonymous] -- must be secured or removed before production deployment.**
        ///
        /// **Common Use Cases:**
        /// - Initial environment setup when provisioning a new development or QA database
        /// - Resetting department lookup values to a known baseline during testing
        /// - Bootstrapping the Chart of Accounts segment structure with standard department codes
        ///
        /// **Integration Pattern:**
        /// - Reads the DEFAULT tenant and its first <see cref="AccountSegmentStructure"/> (ordered by SegmentPosition)
        /// - Deletes all existing <see cref="SegmentLookupValue"/> records for that segment, then inserts 9 standard departments
        /// - Downstream consumers (Journal Entry, GL Reporting, Budget) rely on these lookup values for segment validation
        ///
        /// **Business Rules:**
        /// - Requires a tenant with Code "DEFAULT" to exist in the Tenants table
        /// - Requires at least one AccountSegmentStructure record for the DEFAULT tenant
        /// - Existing lookup values for the target segment are fully replaced (delete-then-insert)
        /// - Department codes follow a 3-digit convention: 100 (General Admin) through 900 (R&amp;D)
        /// - All seeded values are marked IsActive = true with EffectiveDate set to the current UTC time
        /// - CreatedBy is set to a hardcoded admin user ID string
        ///
        /// **Authorization:** None (AllowAnonymous) -- development/testing only
        /// </remarks>
        /// <returns>
        /// A JSON object containing a success message, the target segment structure details
        /// (Id, SegmentName, SegmentLength, LookupTableRequired), and the list of seeded
        /// lookup values (SegmentValue, Description, IsActive).
        /// </returns>
        /// <response code="200">Department lookup values seeded successfully; returns segment structure info and seeded values</response>
        /// <response code="400">DEFAULT tenant not found, or no segment structure exists for the DEFAULT tenant</response>
        /// <response code="500">Internal server error during seed operation (e.g., database connectivity or constraint violation)</response>
        [HttpPost("department-lookup")]
        // [AllowAnonymous] Removed for production security - requires authorization
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
