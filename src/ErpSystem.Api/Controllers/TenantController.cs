using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using ErpSystem.Data.Seeders;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    private readonly ILdapAuthenticationService _ldapAuthenticationService;
    private readonly PaymentTermBaselineSeeder _paymentTermBaselineSeeder;
    private readonly FinanceCloseTemplateBaselineSeeder _financeCloseTemplateBaselineSeeder;
    private readonly ProcurementConfigurationProfileSeeder? _procurementConfigurationProfileSeeder;
    private readonly ProcurementAccessControlSeeder? _procurementAccessControlSeeder;
    private readonly ProcurementStatutoryReportSeeder? _procurementStatutoryReportSeeder;
    private readonly InventoryStatutoryReportSeeder? _inventoryStatutoryReportSeeder;
    private readonly AuditComplianceReportSeeder? _auditComplianceReportSeeder;

    public TenantController(
        ITenantService tenantService,
        ILogger<TenantController> logger,
        IAuditLogService auditLogService,
        ICurrentUserService currentUserService,
        ILdapAuthenticationService ldapAuthenticationService,
        PaymentTermBaselineSeeder paymentTermBaselineSeeder,
        FinanceCloseTemplateBaselineSeeder financeCloseTemplateBaselineSeeder,
        ProcurementConfigurationProfileSeeder? procurementConfigurationProfileSeeder = null,
        ProcurementAccessControlSeeder? procurementAccessControlSeeder = null,
        ProcurementStatutoryReportSeeder? procurementStatutoryReportSeeder = null,
        InventoryStatutoryReportSeeder? inventoryStatutoryReportSeeder = null,
        AuditComplianceReportSeeder? auditComplianceReportSeeder = null)
    {
        _tenantService = tenantService;
        _logger = logger;
        _auditLogService = auditLogService;
        _currentUserService = currentUserService;
        _ldapAuthenticationService = ldapAuthenticationService;
        _paymentTermBaselineSeeder = paymentTermBaselineSeeder;
        _financeCloseTemplateBaselineSeeder = financeCloseTemplateBaselineSeeder;
        _procurementConfigurationProfileSeeder = procurementConfigurationProfileSeeder;
        _procurementAccessControlSeeder = procurementAccessControlSeeder;
        _procurementStatutoryReportSeeder = procurementStatutoryReportSeeder;
        _inventoryStatutoryReportSeeder = inventoryStatutoryReportSeeder;
        _auditComplianceReportSeeder = auditComplianceReportSeeder;
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
                                  .Select(MapToTenantDto)
                                  .ToList();

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

            var tenantDto = MapToTenantDto(tenant);

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

            var tenantDto = MapToTenantDto(tenant);

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
            // Parse status from request or default to Active
            var status = TenantStatus.Active;
            if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<TenantStatus>(request.Status, out var parsedStatus))
            {
                status = parsedStatus;
            }

            var tenant = new Tenant
            {
                // Core fields
                Name = request.Name,
                Code = request.Code,
                Description = request.Description,
                Status = status,

                // Contact Information
                Domain = request.Domain,
                ContactEmail = request.ContactEmail,
                ContactPhone = request.ContactPhone,
                Address = request.Address,

                // Branding
                LogoUrl = request.LogoUrl,
                PrimaryColor = request.PrimaryColor,
                SecondaryColor = request.SecondaryColor,
                FaviconUrl = request.FaviconUrl,
                CoverImageUrl = request.CoverImageUrl,

                // Subscription
                SubscriptionStartDate = request.SubscriptionStartDate,
                SubscriptionEndDate = request.SubscriptionEndDate,

                // LDAP Configuration
                LdapServer = request.LdapServer,
                LdapPort = request.LdapPort,
                LdapBaseDn = request.LdapBaseDn,
                LdapBindDn = request.LdapBindDn,
                LdapBindPassword = request.LdapBindPassword,
                LdapEnabled = request.LdapEnabled,

                // Default tenant settings
                IsDefaultForPublicUsers = request.IsDefaultForPublicUsers,
                IsDefaultForInternalUsers = request.IsDefaultForInternalUsers,

                // Feature flags
                AllowSelfRegistration = request.AllowSelfRegistration,
                PublicRegistrationDomains = request.PublicRegistrationDomains,
                RequireEmailVerification = request.RequireEmailVerification,
                UserAudience = request.UserAudience,
                WelcomeMessage = request.WelcomeMessage,
                DefaultPriority = request.DefaultPriority,
                EnableAutoSelection = request.EnableAutoSelection,

                // Finance/company settings
                BaseCurrency = NormalizeCurrencyCode(request.BaseCurrency),
                BaseCurrencyName = request.BaseCurrencyName,
                CurrencySymbol = request.CurrencySymbol,
                CurrencyDecimalPlaces = NormalizeCurrencyDecimalPlaces(request.CurrencyDecimalPlaces)
            };

            var createdTenant = await _tenantService.CreateTenantAsync(tenant);
            if (_procurementConfigurationProfileSeeder is not null)
            {
                var initializerActorId = Guid.TryParse(_currentUserService.UserId, out var parsedInitializerActorId)
                    ? parsedInitializerActorId
                    : (Guid?)null;
                await _procurementConfigurationProfileSeeder.SeedTenantAsync(createdTenant.Id, initializerActorId);
            }
            if (_procurementAccessControlSeeder is not null)
            {
                var initializerActorId = Guid.TryParse(_currentUserService.UserId, out var parsedInitializerActorId)
                    ? parsedInitializerActorId
                    : (Guid?)null;
                await _procurementAccessControlSeeder.SeedTenantAsync(createdTenant.Id, initializerActorId);
            }
            if (_procurementStatutoryReportSeeder is not null)
            {
                await _procurementStatutoryReportSeeder.SeedTenantAsync(createdTenant.Id);
            }
            if (_inventoryStatutoryReportSeeder is not null)
            {
                await _inventoryStatutoryReportSeeder.SeedTenantAsync(createdTenant.Id);
            }
            if (_auditComplianceReportSeeder is not null)
            {
                await _auditComplianceReportSeeder.SeedTenantAsync(createdTenant.Id);
            }

            // Tenant provisioning owns baseline installation; startup reconciliation is only the safety net.
            await _paymentTermBaselineSeeder.SeedTenantAsync(createdTenant.Id);
            await _financeCloseTemplateBaselineSeeder.SeedTenantAsync(createdTenant.Id);

            // Log audit trail for tenant creation
            try
            {
                var currentUserId = Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : (Guid?)null;
                var currentUsername = _currentUserService.UserName;
                var currentTenantId = _currentUserService.TenantId;

                _logger.LogInformation("DEBUG: Attempting to log tenant creation. UserId: {UserId}, Username: {Username}, TenantId: {TenantId}, IsAuthenticated: {IsAuth}",
                    currentUserId, currentUsername, currentTenantId, _currentUserService.IsAuthenticated);

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

            var tenantDto = MapToTenantDto(createdTenant);

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
                // Core fields
                Name = existingTenant.Name,
                Description = existingTenant.Description,
                Status = existingTenant.Status.ToString(),
                IsActive = existingTenant.Status == TenantStatus.Active,

                // Contact Information
                Domain = existingTenant.Domain,
                ContactEmail = existingTenant.ContactEmail,
                ContactPhone = existingTenant.ContactPhone,
                Address = existingTenant.Address,

                // Branding
                LogoUrl = existingTenant.LogoUrl,
                PrimaryColor = existingTenant.PrimaryColor,
                SecondaryColor = existingTenant.SecondaryColor,
                FaviconUrl = existingTenant.FaviconUrl,
                CoverImageUrl = existingTenant.CoverImageUrl,

                // Subscription
                SubscriptionStartDate = existingTenant.SubscriptionStartDate,
                SubscriptionEndDate = existingTenant.SubscriptionEndDate,

                // LDAP Configuration
                LdapServer = existingTenant.LdapServer,
                LdapPort = existingTenant.LdapPort,
                LdapBaseDn = existingTenant.LdapBaseDn,
                LdapBindDn = existingTenant.LdapBindDn,
                LdapEnabled = existingTenant.LdapEnabled,

                // Default tenant settings
                IsDefaultForPublicUsers = existingTenant.IsDefaultForPublicUsers,
                IsDefaultForInternalUsers = existingTenant.IsDefaultForInternalUsers,

                // Feature flags
                AllowSelfRegistration = existingTenant.AllowSelfRegistration,
                PublicRegistrationDomains = existingTenant.PublicRegistrationDomains,
                RequireEmailVerification = existingTenant.RequireEmailVerification,
                UserAudience = existingTenant.UserAudience,
                WelcomeMessage = existingTenant.WelcomeMessage,
                DefaultPriority = existingTenant.DefaultPriority,
                EnableAutoSelection = existingTenant.EnableAutoSelection,
                BaseCurrency = existingTenant.BaseCurrency,
                BaseCurrencyName = existingTenant.BaseCurrencyName,
                CurrencySymbol = existingTenant.CurrencySymbol,
                CurrencyDecimalPlaces = existingTenant.CurrencyDecimalPlaces
            };

            // Parse status from request
            var status = request.IsActive ? TenantStatus.Active : TenantStatus.Inactive;
            if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<TenantStatus>(request.Status, out var parsedStatus))
            {
                status = parsedStatus;
            }

            // Update the tenant properties
            // Core fields
            existingTenant.Name = request.Name;
            existingTenant.Description = request.Description;
            existingTenant.Status = status;

            // Contact Information
            existingTenant.Domain = request.Domain;
            existingTenant.ContactEmail = request.ContactEmail;
            existingTenant.ContactPhone = request.ContactPhone;
            existingTenant.Address = request.Address;

            // Branding
            existingTenant.LogoUrl = request.LogoUrl;
            existingTenant.PrimaryColor = request.PrimaryColor;
            existingTenant.SecondaryColor = request.SecondaryColor;
            existingTenant.FaviconUrl = request.FaviconUrl;
            existingTenant.CoverImageUrl = request.CoverImageUrl;

            // Subscription
            existingTenant.SubscriptionStartDate = request.SubscriptionStartDate;
            existingTenant.SubscriptionEndDate = request.SubscriptionEndDate;

            // LDAP Configuration
            existingTenant.LdapServer = request.LdapServer;
            existingTenant.LdapPort = request.LdapPort;
            existingTenant.LdapBaseDn = request.LdapBaseDn;
            existingTenant.LdapBindDn = request.LdapBindDn;
            existingTenant.LdapBindPassword = request.LdapBindPassword;
            existingTenant.LdapEnabled = request.LdapEnabled;

            // Default tenant settings
            existingTenant.IsDefaultForPublicUsers = request.IsDefaultForPublicUsers;
            existingTenant.IsDefaultForInternalUsers = request.IsDefaultForInternalUsers;

            // Feature flags
            existingTenant.AllowSelfRegistration = request.AllowSelfRegistration;
            existingTenant.PublicRegistrationDomains = request.PublicRegistrationDomains;
            existingTenant.RequireEmailVerification = request.RequireEmailVerification;
            existingTenant.UserAudience = request.UserAudience;
            existingTenant.WelcomeMessage = request.WelcomeMessage;
            existingTenant.DefaultPriority = request.DefaultPriority;
            existingTenant.EnableAutoSelection = request.EnableAutoSelection;
            existingTenant.BaseCurrency = NormalizeCurrencyCode(request.BaseCurrency);
            existingTenant.BaseCurrencyName = request.BaseCurrencyName;
            existingTenant.CurrencySymbol = request.CurrencySymbol;
            existingTenant.CurrencyDecimalPlaces = NormalizeCurrencyDecimalPlaces(request.CurrencyDecimalPlaces);

            var updatedTenant = await _tenantService.UpdateTenantAsync(existingTenant);

            // Log audit trail for tenant update
            try
            {
                var newValues = new
                {
                    // Core fields
                    Name = request.Name,
                    Description = request.Description,
                    Status = request.Status,
                    IsActive = request.IsActive,

                    // Contact Information
                    Domain = request.Domain,
                    ContactEmail = request.ContactEmail,
                    ContactPhone = request.ContactPhone,
                    Address = request.Address,

                    // Branding
                    LogoUrl = request.LogoUrl,
                    PrimaryColor = request.PrimaryColor,
                    SecondaryColor = request.SecondaryColor,
                    FaviconUrl = request.FaviconUrl,
                    CoverImageUrl = request.CoverImageUrl,

                    // Subscription
                    SubscriptionStartDate = request.SubscriptionStartDate,
                    SubscriptionEndDate = request.SubscriptionEndDate,

                    // LDAP Configuration
                    LdapServer = request.LdapServer,
                    LdapPort = request.LdapPort,
                    LdapBaseDn = request.LdapBaseDn,
                    LdapBindDn = request.LdapBindDn,
                    LdapEnabled = request.LdapEnabled,

                    // Default tenant settings
                    IsDefaultForPublicUsers = request.IsDefaultForPublicUsers,
                    IsDefaultForInternalUsers = request.IsDefaultForInternalUsers,

                    // Feature flags
                    AllowSelfRegistration = request.AllowSelfRegistration,
                    PublicRegistrationDomains = request.PublicRegistrationDomains,
                    RequireEmailVerification = request.RequireEmailVerification,
                    UserAudience = request.UserAudience,
                    WelcomeMessage = request.WelcomeMessage,
                    DefaultPriority = request.DefaultPriority,
                    EnableAutoSelection = request.EnableAutoSelection,
                    BaseCurrency = NormalizeCurrencyCode(request.BaseCurrency),
                    BaseCurrencyName = request.BaseCurrencyName,
                    CurrencySymbol = request.CurrencySymbol,
                    CurrencyDecimalPlaces = NormalizeCurrencyDecimalPlaces(request.CurrencyDecimalPlaces)
                };

                await _auditLogService.LogUserActionAsync(
                    Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : (Guid?)null ?? Guid.Empty,
                    _currentUserService.UserName ?? "Unknown",
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

            var tenantDto = MapToTenantDto(updatedTenant);

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
                    Guid.TryParse(_currentUserService.UserId, out var userId) ? userId : (Guid?)null ?? Guid.Empty,
                    _currentUserService.UserName ?? "Unknown",
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
    /// Test LDAP connection settings
    /// </summary>
    /// <param name="request">LDAP test request</param>
    /// <returns>Test result</returns>
    [HttpPost("ldap/test")]
    [Authorize(Roles = Constants.Roles.SuperAdmin + "," + Constants.Roles.TenantAdmin)]
    public async Task<ActionResult<TestLdapResultDto>> TestLdapConnection([FromBody] TestLdapRequest request)
    {
        try
        {
            // Validate required fields
            if (string.IsNullOrEmpty(request.LdapServer))
            {
                return Ok(new TestLdapResultDto
                {
                    Success = false,
                    Message = "LDAP server is required"
                });
            }

            if (!request.LdapPort.HasValue || request.LdapPort <= 0)
            {
                return Ok(new TestLdapResultDto
                {
                    Success = false,
                    Message = "Valid LDAP port is required"
                });
            }

            // Create a temporary tenant object with the test settings
            var testTenant = new Tenant
            {
                LdapEnabled = true, // Enable for testing
                LdapServer = request.LdapServer,
                LdapPort = request.LdapPort,
                LdapBaseDn = request.LdapBaseDn,
                LdapBindDn = request.LdapBindDn,
                LdapBindPassword = request.LdapBindPassword
            };

            // Use the LDAP authentication service to test the connection
            var result = await _ldapAuthenticationService.TestConnectionAsync(testTenant);

            return Ok(new TestLdapResultDto
            {
                Success = result.Success,
                Message = result.ErrorMessage ?? "LDAP connection test completed"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing LDAP connection");
            return Ok(new TestLdapResultDto
            {
                Success = false,
                Message = $"Error testing LDAP connection: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// List/search LDAP users (Active Directory) using the provided connection settings.
    /// </summary>
    [HttpPost("ldap/users")]
    [Authorize(Roles = Constants.Roles.SuperAdmin + "," + Constants.Roles.TenantAdmin)]
    public async Task<ActionResult<ListLdapUsersResultDto>> ListLdapUsers([FromBody] ListLdapUsersRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.LdapServer))
            {
                return Ok(new ListLdapUsersResultDto
                {
                    Success = false,
                    Message = "LDAP server is required",
                    Users = new List<LdapDirectoryUserDto>()
                });
            }

            if (!request.LdapPort.HasValue || request.LdapPort <= 0)
            {
                return Ok(new ListLdapUsersResultDto
                {
                    Success = false,
                    Message = "Valid LDAP port is required",
                    Users = new List<LdapDirectoryUserDto>()
                });
            }

            if (string.IsNullOrWhiteSpace(request.LdapBaseDn))
            {
                return Ok(new ListLdapUsersResultDto
                {
                    Success = false,
                    Message = "LDAP Base DN is required",
                    Users = new List<LdapDirectoryUserDto>()
                });
            }

            var testTenant = new Tenant
            {
                LdapEnabled = true,
                LdapServer = request.LdapServer,
                LdapPort = request.LdapPort,
                LdapBaseDn = request.LdapBaseDn,
                LdapBindDn = request.LdapBindDn,
                LdapBindPassword = request.LdapBindPassword
            };

            var limit = request.Limit.GetValueOrDefault(200);
            var users = await _ldapAuthenticationService.SearchUsersAsync(request.Query, limit, testTenant);

            return Ok(new ListLdapUsersResultDto
            {
                Success = true,
                Message = $"Found {users.Count} user(s)",
                Users = users.Select(u => new LdapDirectoryUserDto
                {
                    Username = u.Username,
                    UserPrincipalName = u.UserPrincipalName,
                    DistinguishedName = u.DistinguishedName,
                    DisplayName = u.DisplayName,
                    Email = u.Email,
                    FirstName = u.FirstName,
                    LastName = u.LastName
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing LDAP users");
            return Ok(new ListLdapUsersResultDto
            {
                Success = false,
                Message = $"Error listing LDAP users: {ex.Message}",
                Users = new List<LdapDirectoryUserDto>()
            });
        }
    }

    /// <summary>
    /// Get modules for the current tenant
    /// </summary>
    /// <returns>List of tenant modules</returns>
    [HttpGet("modules")]
    public async Task<ActionResult<IEnumerable<TenantModuleDto>>> GetTenantModules()
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            if (!tenantId.HasValue)
            {
                return BadRequest("TenantId not found in token");
            }

            var modules = await _tenantService.GetTenantModulesAsync(tenantId.Value);
            var moduleDtos = modules.Select(MapToTenantModuleDto).ToList();

            return Ok(moduleDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tenant modules");
            return StatusCode(500, "An error occurred while retrieving tenant modules");
        }
    }

    /// <summary>
    /// Helper method to map TenantModule entity to TenantModuleDto
    /// </summary>
    private TenantModuleDto MapToTenantModuleDto(TenantModule module)
    {
        return new TenantModuleDto
        {
            Id = module.Id.ToString(),
            ModuleName = module.ModuleName,
            Description = module.Description,
            Status = module.Status.ToString(),
            EnabledDate = module.EnabledDate,
            DisabledDate = module.DisabledDate
        };
    }

    /// <summary>
    /// Helper method to map Tenant entity to TenantDto
    /// </summary>
    private TenantDto MapToTenantDto(Tenant tenant)
    {
        return new TenantDto
        {
            Id = tenant.Id.ToString(),
            Name = tenant.Name,
            Code = tenant.Code,
            Description = tenant.Description,
            IsActive = tenant.Status == TenantStatus.Active,
            Status = tenant.Status.ToString(),
            CreatedAt = tenant.CreatedAt,
            UpdatedAt = tenant.UpdatedAt ?? tenant.CreatedAt,

            // Contact Information
            Domain = tenant.Domain,
            ContactEmail = tenant.ContactEmail,
            ContactPhone = tenant.ContactPhone,
            Address = tenant.Address,

            // Branding
            LogoUrl = tenant.LogoUrl,
            PrimaryColor = tenant.PrimaryColor,
            SecondaryColor = tenant.SecondaryColor,
            FaviconUrl = tenant.FaviconUrl,
            CoverImageUrl = tenant.CoverImageUrl,

            // Subscription
            SubscriptionStartDate = tenant.SubscriptionStartDate,
            SubscriptionEndDate = tenant.SubscriptionEndDate,

            // LDAP Configuration
            LdapServer = tenant.LdapServer,
            LdapPort = tenant.LdapPort,
            LdapBaseDn = tenant.LdapBaseDn,
            LdapBindDn = tenant.LdapBindDn,
            LdapBindPassword = tenant.LdapBindPassword,
            LdapEnabled = tenant.LdapEnabled,

            // Default tenant settings
            IsDefaultForPublicUsers = tenant.IsDefaultForPublicUsers,
            IsDefaultForInternalUsers = tenant.IsDefaultForInternalUsers,

            // Feature flags
            AllowSelfRegistration = tenant.AllowSelfRegistration,
            PublicRegistrationDomains = tenant.PublicRegistrationDomains,
            RequireEmailVerification = tenant.RequireEmailVerification,
            UserAudience = tenant.UserAudience,
            WelcomeMessage = tenant.WelcomeMessage,
            DefaultPriority = tenant.DefaultPriority,
            EnableAutoSelection = tenant.EnableAutoSelection,

            // Finance/company settings
            BaseCurrency = tenant.BaseCurrency,
            BaseCurrencyName = tenant.BaseCurrencyName,
            CurrencySymbol = tenant.CurrencySymbol,
            CurrencyDecimalPlaces = tenant.CurrencyDecimalPlaces
        };
    }

    private static string NormalizeCurrencyCode(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "GHS" : value.Trim().ToUpperInvariant();
        return normalized.Length > 3 ? normalized[..3] : normalized;
    }

    private static int NormalizeCurrencyDecimalPlaces(int? value)
    {
        if (!value.HasValue || value.Value < 0)
        {
            return 2;
        }

        return Math.Min(value.Value, 6);
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
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Contact Information
    public string? Domain { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }

    // Branding
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? FaviconUrl { get; set; }
    public string? CoverImageUrl { get; set; }

    // Subscription
    public DateTime? SubscriptionStartDate { get; set; }
    public DateTime? SubscriptionEndDate { get; set; }

    // LDAP Configuration
    public string? LdapServer { get; set; }
    public int? LdapPort { get; set; }
    public string? LdapBaseDn { get; set; }
    public string? LdapBindDn { get; set; }
    public string? LdapBindPassword { get; set; }
    public bool LdapEnabled { get; set; } = false;

    // Default tenant settings
    public bool IsDefaultForPublicUsers { get; set; } = false;
    public bool IsDefaultForInternalUsers { get; set; } = false;

    // Feature flags
    public bool AllowSelfRegistration { get; set; } = false;
    public string? PublicRegistrationDomains { get; set; }
    public bool RequireEmailVerification { get; set; } = false;
    public int UserAudience { get; set; } = 2; // Internal (1), External (2), Both (3)
    public string? WelcomeMessage { get; set; }
    public int DefaultPriority { get; set; } = 10;
    public bool EnableAutoSelection { get; set; } = false;

    // Finance/company settings
    public string BaseCurrency { get; set; } = "GHS";
    public string? BaseCurrencyName { get; set; }
    public string? CurrencySymbol { get; set; }
    public int CurrencyDecimalPlaces { get; set; } = 2;
}

public class CreateTenantRequest
{
    // Required fields
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "Active";

    // Contact Information
    public string? Domain { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }

    // Branding
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? FaviconUrl { get; set; }
    public string? CoverImageUrl { get; set; }

    // Subscription
    public DateTime? SubscriptionStartDate { get; set; }
    public DateTime? SubscriptionEndDate { get; set; }

    // LDAP Configuration
    public string? LdapServer { get; set; }
    public int? LdapPort { get; set; }
    public string? LdapBaseDn { get; set; }
    public string? LdapBindDn { get; set; }
    public string? LdapBindPassword { get; set; }
    public bool LdapEnabled { get; set; } = false;

    // Default tenant settings
    public bool IsDefaultForPublicUsers { get; set; } = false;
    public bool IsDefaultForInternalUsers { get; set; } = false;

    // Feature flags
    public bool AllowSelfRegistration { get; set; } = false;
    public string? PublicRegistrationDomains { get; set; }
    public bool RequireEmailVerification { get; set; } = false;
    public int UserAudience { get; set; } = 2; // Internal (1), External (2), Both (3)
    public string? WelcomeMessage { get; set; }
    public int DefaultPriority { get; set; } = 10;
    public bool EnableAutoSelection { get; set; } = false;

    // Finance/company settings
    public string? BaseCurrency { get; set; }
    public string? BaseCurrencyName { get; set; }
    public string? CurrencySymbol { get; set; }
    public int? CurrencyDecimalPlaces { get; set; }
}

public class TestLdapRequest
{
    public string? LdapServer { get; set; }
    public int? LdapPort { get; set; }
    public string? LdapBaseDn { get; set; }
    public string? LdapBindDn { get; set; }
    public string? LdapBindPassword { get; set; }
}

public class TestLdapResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ListLdapUsersRequest
{
    public string? LdapServer { get; set; }
    public int? LdapPort { get; set; }
    public string? LdapBaseDn { get; set; }
    public string? LdapBindDn { get; set; }
    public string? LdapBindPassword { get; set; }
    public string? Query { get; set; }
    public int? Limit { get; set; }
}

public class LdapDirectoryUserDto
{
    public string Username { get; set; } = string.Empty;
    public string UserPrincipalName { get; set; } = string.Empty;
    public string DistinguishedName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

public class ListLdapUsersResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<LdapDirectoryUserDto> Users { get; set; } = new();
}

public class UpdateTenantRequest
{
    // Core fields
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public string Status { get; set; } = "Active";

    // Contact Information
    public string? Domain { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }

    // Branding
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? FaviconUrl { get; set; }
    public string? CoverImageUrl { get; set; }

    // Subscription
    public DateTime? SubscriptionStartDate { get; set; }
    public DateTime? SubscriptionEndDate { get; set; }

    // LDAP Configuration
    public string? LdapServer { get; set; }
    public int? LdapPort { get; set; }
    public string? LdapBaseDn { get; set; }
    public string? LdapBindDn { get; set; }
    public string? LdapBindPassword { get; set; }
    public bool LdapEnabled { get; set; } = false;

    // Default tenant settings
    public bool IsDefaultForPublicUsers { get; set; } = false;
    public bool IsDefaultForInternalUsers { get; set; } = false;

    // Feature flags
    public bool AllowSelfRegistration { get; set; } = false;
    public string? PublicRegistrationDomains { get; set; }
    public bool RequireEmailVerification { get; set; } = false;
    public int UserAudience { get; set; } = 2; // Internal (1), External (2), Both (3)
    public string? WelcomeMessage { get; set; }
    public int DefaultPriority { get; set; } = 10;
    public bool EnableAutoSelection { get; set; } = false;

    // Finance/company settings
    public string? BaseCurrency { get; set; }
    public string? BaseCurrencyName { get; set; }
    public string? CurrencySymbol { get; set; }
    public int? CurrencyDecimalPlaces { get; set; }
}

public class TenantModuleDto
{
    public string Id { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? EnabledDate { get; set; }
    public DateTime? DisabledDate { get; set; }
}
