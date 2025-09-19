using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.Services;
using ErpSystem.Core.Entities;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TenantController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly ILogger<TenantController> _logger;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUserService;

    public TenantController(
        ITenantService tenantService, 
        ILogger<TenantController> logger,
        IAuditLogService auditLogService,
        ICurrentUserService currentUserService)
    {
        _tenantService = tenantService;
        _logger = logger;
        _auditLogService = auditLogService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get all tenants
    /// </summary>
    /// <returns>List of tenants</returns>
    [HttpGet]
    [AllowAnonymous] // Allow anonymous access for login page tenant selection
    public async Task<ActionResult<IEnumerable<TenantDto>>> GetTenants()
    {
        try
        {
            var tenants = await _tenantService.GetAllTenantsAsync();
            var tenantDtos = tenants.Where(t => t.Status == TenantStatus.Active)
                                  .Select(t => new TenantDto
                                  {
                                      Id = t.Id.ToString(),
                                      Name = t.Name,
                                      Code = t.Code,
                                      Description = t.Description,
                                      IsActive = t.Status == TenantStatus.Active,
                                      CreatedAt = t.CreatedAt,
                                      UpdatedAt = t.UpdatedAt ?? t.CreatedAt,
                                      LogoUrl = t.LogoUrl,
                                      Domain = t.Domain,
                                      ContactEmail = t.ContactEmail,
                                      ContactPhone = t.ContactPhone,
                                      Address = t.Address
                                  }).ToList();

            return Ok(tenantDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tenants");
            return StatusCode(500, "An error occurred while retrieving tenants");
        }
    }

    /// <summary>
    /// Get tenant by ID
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <returns>Tenant details</returns>
    [HttpGet("{id}")]
    public async Task<ActionResult<TenantDto>> GetTenant(Guid id)
    {
        try
        {
            var tenant = await _tenantService.GetTenantByIdAsync(id);
            if (tenant == null)
            {
                return NotFound($"Tenant with ID {id} not found");
            }

            var tenantDto = new TenantDto
            {
                Id = tenant.Id.ToString(),
                Name = tenant.Name,
                Code = tenant.Code,
                Description = tenant.Description,
                IsActive = tenant.Status == TenantStatus.Active,
                CreatedAt = tenant.CreatedAt,
                UpdatedAt = tenant.UpdatedAt ?? tenant.CreatedAt,
                LogoUrl = tenant.LogoUrl,
                Domain = tenant.Domain,
                ContactEmail = tenant.ContactEmail,
                ContactPhone = tenant.ContactPhone,
                Address = tenant.Address
            };

            return Ok(tenantDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tenant {TenantId}", id);
            return StatusCode(500, "An error occurred while retrieving the tenant");
        }
    }

    /// <summary>
    /// Get tenant by code
    /// </summary>
    /// <param name="code">Tenant code</param>
    /// <returns>Tenant details</returns>
    [HttpGet("by-code/{code}")]
    [AllowAnonymous] // Allow anonymous access for login validation
    public async Task<ActionResult<TenantDto>> GetTenantByCode(string code)
    {
        try
        {
            var tenant = await _tenantService.GetTenantByCodeAsync(code);
            if (tenant == null)
            {
                return NotFound($"Tenant with code '{code}' not found");
            }

            var tenantDto = new TenantDto
            {
                Id = tenant.Id.ToString(),
                Name = tenant.Name,
                Code = tenant.Code,
                Description = tenant.Description,
                IsActive = tenant.Status == TenantStatus.Active,
                CreatedAt = tenant.CreatedAt,
                UpdatedAt = tenant.UpdatedAt ?? tenant.CreatedAt,
                LogoUrl = tenant.LogoUrl,
                Domain = tenant.Domain,
                ContactEmail = tenant.ContactEmail,
                ContactPhone = tenant.ContactPhone,
                Address = tenant.Address
            };

            return Ok(tenantDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tenant by code {TenantCode}", code);
            return StatusCode(500, "An error occurred while retrieving the tenant");
        }
    }

    /// <summary>
    /// Create a new tenant
    /// </summary>
    /// <param name="request">Tenant creation request</param>
    /// <returns>Created tenant</returns>
    [HttpPost]
    [Authorize(Roles = Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<TenantDto>> CreateTenant([FromBody] CreateTenantRequest request)
    {
        try
        {
            var tenant = new Tenant
            {
                Name = request.Name,
                Code = request.Code,
                Description = request.Description,
                Status = TenantStatus.Active,
                Domain = request.Domain,
                ContactEmail = request.ContactEmail,
                ContactPhone = request.ContactPhone,
                Address = request.Address
            };

            var createdTenant = await _tenantService.CreateTenantAsync(tenant);

            // Log audit trail for tenant creation
            try
            {
                var currentUserId = _currentUserService.GetUserId();
                var currentUsername = _currentUserService.GetUsername();
                var currentTenantId = _currentUserService.GetTenantId();
                
                _logger.LogInformation("DEBUG: Attempting to log tenant creation. UserId: {UserId}, Username: {Username}, TenantId: {TenantId}, IsAuthenticated: {IsAuth}", 
                    currentUserId, currentUsername, currentTenantId, _currentUserService.IsAuthenticated());
                
                await _auditLogService.LogUserActionAsync(
                    currentUserId ?? Guid.Empty,
                    currentUsername ?? "Unknown",
                    "Create",
                    "Tenant",
                    createdTenant.Id.ToString(),
                    null,
                    new { Name = tenant.Name, Code = tenant.Code, Domain = tenant.Domain },
                    GetClientIpAddress(),
                    GetUserAgent());
                    
                _logger.LogInformation("DEBUG: Tenant creation audit log completed successfully");
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "Failed to log audit trail for tenant creation. Details: {Details}", logEx.Message);
            }

            var tenantDto = new TenantDto
            {
                Id = createdTenant.Id.ToString(),
                Name = createdTenant.Name,
                Code = createdTenant.Code,
                Description = createdTenant.Description,
                IsActive = createdTenant.Status == TenantStatus.Active,
                CreatedAt = createdTenant.CreatedAt,
                UpdatedAt = createdTenant.UpdatedAt ?? createdTenant.CreatedAt,
                LogoUrl = createdTenant.LogoUrl,
                Domain = createdTenant.Domain,
                ContactEmail = createdTenant.ContactEmail,
                ContactPhone = createdTenant.ContactPhone,
                Address = createdTenant.Address
            };

            return CreatedAtAction(nameof(GetTenant), new { id = createdTenant.Id }, tenantDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tenant");
            return StatusCode(500, "An error occurred while creating the tenant");
        }
    }

    /// <summary>
    /// Update an existing tenant
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <param name="request">Tenant update request</param>
    /// <returns>Updated tenant</returns>
    [HttpPut("{id}")]
    [Authorize(Roles = Constants.Roles.SuperAdmin)]
    public async Task<ActionResult<TenantDto>> UpdateTenant(Guid id, [FromBody] UpdateTenantRequest request)
    {
        try
        {
            var existingTenant = await _tenantService.GetTenantByIdAsync(id);
            if (existingTenant == null)
            {
                return NotFound($"Tenant with ID {id} not found");
            }

            // Capture old values for audit logging
            var oldValues = new 
            {
                Name = existingTenant.Name,
                Description = existingTenant.Description,
                Domain = existingTenant.Domain,
                ContactEmail = existingTenant.ContactEmail,
                ContactPhone = existingTenant.ContactPhone,
                Address = existingTenant.Address,
                IsActive = existingTenant.Status == TenantStatus.Active
            };

            // Update the tenant properties
            existingTenant.Name = request.Name;
            existingTenant.Description = request.Description;
            existingTenant.Domain = request.Domain;
            existingTenant.ContactEmail = request.ContactEmail;
            existingTenant.ContactPhone = request.ContactPhone;
            existingTenant.Address = request.Address;
            existingTenant.Status = request.IsActive ? TenantStatus.Active : TenantStatus.Inactive;

            var updatedTenant = await _tenantService.UpdateTenantAsync(existingTenant);

            // Log audit trail for tenant update
            try
            {
                var newValues = new 
                {
                    Name = request.Name,
                    Description = request.Description,
                    Domain = request.Domain,
                    ContactEmail = request.ContactEmail,
                    ContactPhone = request.ContactPhone,
                    Address = request.Address,
                    IsActive = request.IsActive
                };

                await _auditLogService.LogUserActionAsync(
                    _currentUserService.GetUserId() ?? Guid.Empty,
                    _currentUserService.GetUsername() ?? "Unknown",
                    "Update",
                    "Tenant",
                    id.ToString(),
                    oldValues,
                    newValues,
                    GetClientIpAddress(),
                    GetUserAgent());
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to log audit trail for tenant update");
            }

            var tenantDto = new TenantDto
            {
                Id = updatedTenant.Id.ToString(),
                Name = updatedTenant.Name,
                Code = updatedTenant.Code,
                Description = updatedTenant.Description,
                IsActive = updatedTenant.Status == TenantStatus.Active,
                CreatedAt = updatedTenant.CreatedAt,
                UpdatedAt = updatedTenant.UpdatedAt ?? updatedTenant.CreatedAt,
                LogoUrl = updatedTenant.LogoUrl,
                Domain = updatedTenant.Domain,
                ContactEmail = updatedTenant.ContactEmail,
                ContactPhone = updatedTenant.ContactPhone,
                Address = updatedTenant.Address
            };

            return Ok(tenantDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tenant {TenantId}", id);
            return StatusCode(500, "An error occurred while updating the tenant");
        }
    }

    /// <summary>
    /// Delete a tenant
    /// </summary>
    /// <param name="id">Tenant ID</param>
    /// <returns>No content</returns>
    [HttpDelete("{id}")]
    [Authorize(Roles = Constants.Roles.SuperAdmin)]
    public async Task<IActionResult> DeleteTenant(Guid id)
    {
        try
        {
            var existingTenant = await _tenantService.GetTenantByIdAsync(id);
            if (existingTenant == null)
            {
                return NotFound($"Tenant with ID {id} not found");
            }

            await _tenantService.DeleteTenantAsync(id);

            // Log audit trail for tenant deletion
            try
            {
                await _auditLogService.LogUserActionAsync(
                    _currentUserService.GetUserId() ?? Guid.Empty,
                    _currentUserService.GetUsername() ?? "Unknown",
                    "Delete",
                    "Tenant",
                    id.ToString(),
                    new { Name = existingTenant.Name, Code = existingTenant.Code },
                    null,
                    GetClientIpAddress(),
                    GetUserAgent());
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to log audit trail for tenant deletion");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tenant {TenantId}", id);
            return StatusCode(500, "An error occurred while deleting the tenant");
        }
    }

    /// <summary>
    /// Helper method to get client IP address
    /// </summary>
    private string GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    }

    /// <summary>
    /// Helper method to get user agent
    /// </summary>
    private string GetUserAgent()
    {
        return HttpContext.Request.Headers["User-Agent"].ToString();
    }
}

// DTOs
public class TenantDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? LogoUrl { get; set; }
    public string? Domain { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
}

public class CreateTenantRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Domain { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
}

public class UpdateTenantRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Domain { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}
