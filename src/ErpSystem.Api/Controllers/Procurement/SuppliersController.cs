using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

/// <summary>
/// API controller for managing suppliers
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SuppliersController : ControllerBase
{
    private readonly ISupplierRepository _supplierRepository;
    private readonly ISupplierContactRepository _supplierContactRepository;
    private readonly ISupplierItemCatalogRepository _supplierItemCatalogRepository;
    private readonly ILogger<SuppliersController> _logger;
    private readonly IProcurementMasterDataChangeService? _masterDataChanges;

    public SuppliersController(
        ISupplierRepository supplierRepository,
        ISupplierContactRepository supplierContactRepository,
        ISupplierItemCatalogRepository supplierItemCatalogRepository,
        ILogger<SuppliersController> logger,
        IProcurementMasterDataChangeService? masterDataChanges = null)
    {
        _supplierRepository = supplierRepository;
        _supplierContactRepository = supplierContactRepository;
        _supplierItemCatalogRepository = supplierItemCatalogRepository;
        _logger = logger;
        _masterDataChanges = masterDataChanges;
    }

    /// <summary>
    /// Gets all suppliers with pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<SupplierDto>>> GetSuppliers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? supplierType = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] bool? isPreferred = null)
    {
        try
        {
            var suppliers = await _supplierRepository.GetSuppliersAsync(
                page, pageSize, search, status, supplierType, isActive, isPreferred);

            var supplierDtos = suppliers.Items.Select(s => new SupplierDto
            {
                Id = s.Id,
                SupplierCode = s.SupplierCode,
                Name = s.Name,
                Description = s.Description,
                SupplierType = s.SupplierType,
                Phone = s.Phone,
                Email = s.Email,
                Website = s.Website,
                PrimaryContactName = s.PrimaryContactName,
                PaymentTerms = s.PaymentTerms,
                LeadTimeDays = s.LeadTimeDays,
                IsActive = s.IsActive,
                IsPreferred = s.IsPreferred,
                Status = s.Status,
                Rating = s.Rating,
                LastOrderDate = s.LastOrderDate
            }).ToList();

            var result = new PagedResult<SupplierDto>
            {
                Items = supplierDtos,
                TotalCount = suppliers.TotalCount,
                Page = suppliers.Page,
                PageSize = suppliers.PageSize
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving suppliers");
            return StatusCode(500, "An error occurred while retrieving suppliers");
        }
    }

    /// <summary>
    /// Gets a supplier by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<SupplierDetailDto>> GetSupplier(Guid id)
    {
        try
        {
            var supplier = await _supplierRepository.GetSupplierByIdAsync(id);
            if (supplier == null)
            {
                return NotFound($"Supplier with ID {id} not found");
            }

            var contacts = await _supplierContactRepository.GetContactsBySupplierId(supplier.Id);
            var catalog = await _supplierItemCatalogRepository.GetCatalogBySupplierId(supplier.Id);

            var supplierDto = new SupplierDetailDto
            {
                Id = supplier.Id,
                SupplierCode = supplier.SupplierCode,
                Name = supplier.Name,
                Description = supplier.Description,
                SupplierType = supplier.SupplierType,
                Address = supplier.Address,
                City = supplier.City,
                State = supplier.State,
                ZipCode = supplier.ZipCode,
                Country = supplier.Country,
                Phone = supplier.Phone,
                Email = supplier.Email,
                Website = supplier.Website,
                PrimaryContactName = supplier.PrimaryContactName,
                PrimaryContactTitle = supplier.PrimaryContactTitle,
                PrimaryContactPhone = supplier.PrimaryContactPhone,
                PrimaryContactEmail = supplier.PrimaryContactEmail,
                TaxId = supplier.TaxId,
                PaymentTerms = supplier.PaymentTerms,
                ShippingTerms = supplier.ShippingTerms,
                CreditLimit = supplier.CreditLimit,
                LeadTimeDays = supplier.LeadTimeDays,
                IsActive = supplier.IsActive,
                IsPreferred = supplier.IsPreferred,
                Status = supplier.Status,
                Rating = supplier.Rating,
                Notes = supplier.Notes,
                ContractStartDate = supplier.ContractStartDate,
                ContractEndDate = supplier.ContractEndDate,
                LastOrderDate = supplier.LastOrderDate,
                Contacts = contacts.Select(c => new SupplierContactDto
                {
                    Id = c.Id,
                    SupplierId = c.SupplierId,
                    Name = c.Name,
                    Title = c.Title,
                    Department = c.Department,
                    Phone = c.Phone,
                    Email = c.Email,
                    IsPrimary = c.IsPrimary,
                    ContactType = c.ContactType,
                    Notes = c.Notes
                }).ToList(),
                ItemCatalog = catalog.Select(ci => new SupplierItemCatalogDto
                {
                    Id = ci.Id,
                    SupplierId = ci.SupplierId,
                    InventoryItemId = ci.InventoryItemId,
                    SupplierItemCode = ci.SupplierItemCode,
                    SupplierItemName = ci.SupplierItemName,
                    Description = ci.Description,
                    UnitPrice = ci.UnitPrice,
                    UnitOfMeasure = ci.UnitOfMeasure,
                    MinimumOrderQuantity = ci.MinimumOrderQuantity,
                    LeadTimeDays = ci.LeadTimeDays,
                    IsPreferred = ci.IsPreferred,
                    IsActive = ci.IsActive,
                    EffectiveDate = ci.EffectiveDate,
                    ExpiryDate = ci.ExpiryDate,
                    Notes = ci.Notes,
                    SupplierName = ci.Supplier?.Name ?? "",
                    ItemCode = ci.InventoryItem?.ItemCode ?? "",
                    ItemName = ci.InventoryItem?.Name ?? ""
                }).ToList()
            };

            return Ok(supplierDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving supplier {SupplierId}", id);
            return StatusCode(500, "An error occurred while retrieving the supplier");
        }
    }

    /// <summary>
    /// Creates a new supplier
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SupplierDetailDto>> CreateSupplier([FromBody] CreateSupplierDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Check if supplier code already exists
            var existingSupplier = await _supplierRepository.GetSupplierByCodeAsync(createDto.SupplierCode);
            if (existingSupplier != null)
            {
                return BadRequest($"Supplier with code '{createDto.SupplierCode}' already exists");
            }

            var supplier = new Supplier
            {
                Id = Guid.NewGuid(),
                SupplierCode = createDto.SupplierCode,
                Name = createDto.Name,
                Description = createDto.Description,
                SupplierType = createDto.SupplierType,
                Address = createDto.Address,
                City = createDto.City,
                State = createDto.State,
                ZipCode = createDto.ZipCode,
                Country = createDto.Country,
                Phone = createDto.Phone,
                Email = createDto.Email,
                Website = createDto.Website,
                PrimaryContactName = createDto.PrimaryContactName,
                PrimaryContactTitle = createDto.PrimaryContactTitle,
                PrimaryContactPhone = createDto.PrimaryContactPhone,
                PrimaryContactEmail = createDto.PrimaryContactEmail,
                TaxId = createDto.TaxId,
                PaymentTerms = createDto.PaymentTerms,
                ShippingTerms = createDto.ShippingTerms,
                CreditLimit = createDto.CreditLimit,
                LeadTimeDays = createDto.LeadTimeDays,
                IsActive = true,
                IsPreferred = createDto.IsPreferred,
                Status = "Active",
                Rating = createDto.Rating,
                Notes = createDto.Notes,
                ContractStartDate = createDto.ContractStartDate,
                ContractEndDate = createDto.ContractEndDate,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _supplierRepository.CreateSupplierAsync(supplier);

            // Return the created supplier as detailed DTO
            return CreatedAtAction(nameof(GetSupplier), new { id = supplier.Id },
                await GetSupplierDetailDto(supplier.Id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating supplier");
            return StatusCode(500, "An error occurred while creating the supplier");
        }
    }

    /// <summary>
    /// Updates an existing supplier
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSupplier(Guid id, [FromBody] CreateSupplierDto updateDto)
    {
        try
        {
            var protection = await GuardDirectMutationAsync(id, "LegacySupplier.Update");
            if (protection is not null) return protection;
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var supplier = await _supplierRepository.GetSupplierByIdAsync(id);
            if (supplier == null)
            {
                return NotFound($"Supplier with ID {id} not found");
            }

            // Check if supplier code already exists for different supplier
            var existingSupplier = await _supplierRepository.GetSupplierByCodeAsync(updateDto.SupplierCode);
            if (existingSupplier != null && existingSupplier.Id != id)
            {
                return BadRequest($"Supplier with code '{updateDto.SupplierCode}' already exists");
            }

            // Update supplier properties
            supplier.SupplierCode = updateDto.SupplierCode;
            supplier.Name = updateDto.Name;
            supplier.Description = updateDto.Description;
            supplier.SupplierType = updateDto.SupplierType;
            supplier.Address = updateDto.Address;
            supplier.City = updateDto.City;
            supplier.State = updateDto.State;
            supplier.ZipCode = updateDto.ZipCode;
            supplier.Country = updateDto.Country;
            supplier.Phone = updateDto.Phone;
            supplier.Email = updateDto.Email;
            supplier.Website = updateDto.Website;
            supplier.PrimaryContactName = updateDto.PrimaryContactName;
            supplier.PrimaryContactTitle = updateDto.PrimaryContactTitle;
            supplier.PrimaryContactPhone = updateDto.PrimaryContactPhone;
            supplier.PrimaryContactEmail = updateDto.PrimaryContactEmail;
            supplier.TaxId = updateDto.TaxId;
            supplier.PaymentTerms = updateDto.PaymentTerms;
            supplier.ShippingTerms = updateDto.ShippingTerms;
            supplier.CreditLimit = updateDto.CreditLimit;
            supplier.LeadTimeDays = updateDto.LeadTimeDays;
            supplier.IsPreferred = updateDto.IsPreferred;
            supplier.Rating = updateDto.Rating;
            supplier.Notes = updateDto.Notes;
            supplier.ContractStartDate = updateDto.ContractStartDate;
            supplier.ContractEndDate = updateDto.ContractEndDate;
            supplier.UpdatedAt = DateTime.UtcNow;

            await _supplierRepository.UpdateSupplierAsync(supplier);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating supplier {SupplierId}", id);
            return StatusCode(500, "An error occurred while updating the supplier");
        }
    }

    /// <summary>
    /// Deletes a supplier
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSupplier(Guid id)
    {
        try
        {
            var protection = await GuardDirectMutationAsync(id, "LegacySupplier.Delete");
            if (protection is not null) return protection;
            var supplier = await _supplierRepository.GetSupplierByIdAsync(id);
            if (supplier == null)
            {
                return NotFound($"Supplier with ID {id} not found");
            }

            // Check if supplier has active purchase orders
            var hasActivePOs = await _supplierRepository.HasActivePurchaseOrdersAsync(id);
            if (hasActivePOs)
            {
                return BadRequest("Cannot delete supplier with active purchase orders");
            }

            await _supplierRepository.DeleteSupplierAsync(id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting supplier {SupplierId}", id);
            return StatusCode(500, "An error occurred while deleting the supplier");
        }
    }

    /// <summary>
    /// Updates supplier status
    /// </summary>
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateSupplierStatus(Guid id, [FromBody] UpdateStatusDto statusDto)
    {
        try
        {
            var protection = await GuardDirectMutationAsync(id, "LegacySupplier.Status");
            if (protection is not null) return protection;
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var supplier = await _supplierRepository.GetSupplierByIdAsync(id);
            if (supplier == null)
            {
                return NotFound($"Supplier with ID {id} not found");
            }

            await _supplierRepository.UpdateSupplierStatusAsync(id, statusDto.Status);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating supplier status {SupplierId}", id);
            return StatusCode(500, "An error occurred while updating supplier status");
        }
    }

    /// <summary>
    /// Gets suppliers by type
    /// </summary>
    [HttpGet("by-type/{supplierType}")]
    public async Task<ActionResult<List<SupplierDto>>> GetSuppliersByType(string supplierType)
    {
        try
        {
            var suppliers = await _supplierRepository.GetSuppliersByTypeAsync(supplierType);

            var supplierDtos = suppliers.Select(s => new SupplierDto
            {
                Id = s.Id,
                SupplierCode = s.SupplierCode,
                Name = s.Name,
                Description = s.Description,
                SupplierType = s.SupplierType,
                Phone = s.Phone,
                Email = s.Email,
                Website = s.Website,
                PrimaryContactName = s.PrimaryContactName,
                PaymentTerms = s.PaymentTerms,
                LeadTimeDays = s.LeadTimeDays,
                IsActive = s.IsActive,
                IsPreferred = s.IsPreferred,
                Status = s.Status,
                Rating = s.Rating,
                LastOrderDate = s.LastOrderDate
            }).ToList();

            return Ok(supplierDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving suppliers by type {SupplierType}", supplierType);
            return StatusCode(500, "An error occurred while retrieving suppliers");
        }
    }

    /// <summary>
    /// Gets preferred suppliers
    /// </summary>
    [HttpGet("preferred")]
    public async Task<ActionResult<List<SupplierDto>>> GetPreferredSuppliers()
    {
        try
        {
            var suppliers = await _supplierRepository.GetPreferredSuppliersAsync();

            var supplierDtos = suppliers.Select(s => new SupplierDto
            {
                Id = s.Id,
                SupplierCode = s.SupplierCode,
                Name = s.Name,
                Description = s.Description,
                SupplierType = s.SupplierType,
                Phone = s.Phone,
                Email = s.Email,
                Website = s.Website,
                PrimaryContactName = s.PrimaryContactName,
                PaymentTerms = s.PaymentTerms,
                LeadTimeDays = s.LeadTimeDays,
                IsActive = s.IsActive,
                IsPreferred = s.IsPreferred,
                Status = s.Status,
                Rating = s.Rating,
                LastOrderDate = s.LastOrderDate
            }).ToList();

            return Ok(supplierDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving preferred suppliers");
            return StatusCode(500, "An error occurred while retrieving preferred suppliers");
        }
    }

    #region Supplier Contacts

    /// <summary>
    /// Gets contacts for a supplier
    /// </summary>
    [HttpGet("{supplierId}/contacts")]
    public async Task<ActionResult<List<SupplierContactDto>>> GetSupplierContacts(Guid supplierId)
    {
        try
        {
            var supplier = await _supplierRepository.GetSupplierByIdAsync(supplierId);
            if (supplier == null)
            {
                return NotFound($"Supplier with ID {supplierId} not found");
            }

            var contacts = await _supplierContactRepository.GetContactsBySupplierId(supplierId);

            var contactDtos = contacts.Select(c => new SupplierContactDto
            {
                Id = c.Id,
                SupplierId = c.SupplierId,
                Name = c.Name,
                Title = c.Title,
                Department = c.Department,
                Phone = c.Phone,
                Email = c.Email,
                IsPrimary = c.IsPrimary,
                ContactType = c.ContactType,
                Notes = c.Notes
            }).ToList();

            return Ok(contactDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving contacts for supplier {SupplierId}", supplierId);
            return StatusCode(500, "An error occurred while retrieving contacts");
        }
    }

    /// <summary>
    /// Creates a new supplier contact
    /// </summary>
    [HttpPost("{supplierId}/contacts")]
    public async Task<ActionResult<SupplierContactDto>> CreateSupplierContact(
        Guid supplierId,
        [FromBody] CreateSupplierContactDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var supplier = await _supplierRepository.GetSupplierByIdAsync(supplierId);
            if (supplier == null)
            {
                return NotFound($"Supplier with ID {supplierId} not found");
            }

            var contact = new SupplierContact
            {
                Id = Guid.NewGuid(),
                SupplierId = supplierId,
                Name = createDto.Name,
                Title = createDto.Title,
                Department = createDto.Department,
                Phone = createDto.Phone,
                Email = createDto.Email,
                IsPrimary = createDto.IsPrimary,
                ContactType = createDto.ContactType,
                Notes = createDto.Notes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _supplierContactRepository.CreateContactAsync(contact);

            var contactDto = new SupplierContactDto
            {
                Id = contact.Id,
                SupplierId = contact.SupplierId,
                Name = contact.Name,
                Title = contact.Title,
                Department = contact.Department,
                Phone = contact.Phone,
                Email = contact.Email,
                IsPrimary = contact.IsPrimary,
                ContactType = contact.ContactType,
                Notes = contact.Notes
            };

            return CreatedAtAction(nameof(GetSupplierContacts),
                new { supplierId = supplierId }, contactDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating contact for supplier {SupplierId}", supplierId);
            return StatusCode(500, "An error occurred while creating the contact");
        }
    }

    #endregion

    #region Private Helper Methods

    private async Task<SupplierDetailDto> GetSupplierDetailDto(Guid supplierId)
    {
        var supplier = await _supplierRepository.GetSupplierByIdAsync(supplierId);
        if (supplier == null)
        {
            return null!;
        }

        var contacts = await _supplierContactRepository.GetContactsBySupplierId(supplier.Id);
        var catalog = await _supplierItemCatalogRepository.GetCatalogBySupplierId(supplier.Id);

        return new SupplierDetailDto
        {
            Id = supplier.Id,
            SupplierCode = supplier.SupplierCode,
            Name = supplier.Name,
            Description = supplier.Description,
            SupplierType = supplier.SupplierType,
            Address = supplier.Address,
            City = supplier.City,
            State = supplier.State,
            ZipCode = supplier.ZipCode,
            Country = supplier.Country,
            Phone = supplier.Phone,
            Email = supplier.Email,
            Website = supplier.Website,
            PrimaryContactName = supplier.PrimaryContactName,
            PrimaryContactTitle = supplier.PrimaryContactTitle,
            PrimaryContactPhone = supplier.PrimaryContactPhone,
            PrimaryContactEmail = supplier.PrimaryContactEmail,
            TaxId = supplier.TaxId,
            PaymentTerms = supplier.PaymentTerms,
            ShippingTerms = supplier.ShippingTerms,
            CreditLimit = supplier.CreditLimit,
            LeadTimeDays = supplier.LeadTimeDays,
            IsActive = supplier.IsActive,
            IsPreferred = supplier.IsPreferred,
            Status = supplier.Status,
            Rating = supplier.Rating,
            Notes = supplier.Notes,
            ContractStartDate = supplier.ContractStartDate,
            ContractEndDate = supplier.ContractEndDate,
            LastOrderDate = supplier.LastOrderDate,
            Contacts = contacts.Select(c => new SupplierContactDto
            {
                Id = c.Id,
                SupplierId = c.SupplierId,
                Name = c.Name,
                Title = c.Title,
                Department = c.Department,
                Phone = c.Phone,
                Email = c.Email,
                IsPrimary = c.IsPrimary,
                ContactType = c.ContactType,
                Notes = c.Notes
            }).ToList(),
            ItemCatalog = catalog.Select(ci => new SupplierItemCatalogDto
            {
                Id = ci.Id,
                SupplierId = ci.SupplierId,
                InventoryItemId = ci.InventoryItemId,
                SupplierItemCode = ci.SupplierItemCode,
                SupplierItemName = ci.SupplierItemName,
                Description = ci.Description,
                UnitPrice = ci.UnitPrice,
                UnitOfMeasure = ci.UnitOfMeasure,
                MinimumOrderQuantity = ci.MinimumOrderQuantity,
                LeadTimeDays = ci.LeadTimeDays,
                IsPreferred = ci.IsPreferred,
                IsActive = ci.IsActive,
                EffectiveDate = ci.EffectiveDate,
                ExpiryDate = ci.ExpiryDate,
                Notes = ci.Notes,
                SupplierName = ci.Supplier?.Name ?? "",
                ItemCode = ci.InventoryItem?.ItemCode ?? "",
                ItemName = ci.InventoryItem?.Name ?? ""
            }).ToList()
        };
    }

    #endregion
    private async Task<ObjectResult?> GuardDirectMutationAsync(Guid id, string action)
    {
        if (_masterDataChanges is null) return null;
        var decision = await _masterDataChanges.CheckDirectMutationAsync(
            new[] { ProcurementMasterDataResourceType.SupplierProfile, ProcurementMasterDataResourceType.SupplierTaxDetails },
            id, action, HttpContext.TraceIdentifier, HttpContext.RequestAborted);
        return decision.Allowed ? null : Conflict(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Staged supplier change required",
            Detail = decision.Message,
            Instance = HttpContext.Request.Path,
            Extensions = { ["code"] = decision.Code, ["correlationId"] = decision.CorrelationId, ["policyId"] = decision.PolicyId }
        });
    }
}
