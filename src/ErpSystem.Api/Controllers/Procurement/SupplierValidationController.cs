using ErpSystem.Core.Services.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/supplier-validation")]
[Authorize]
public class SupplierValidationController : ControllerBase
{
    private readonly ISupplierValidationService _validationService;
    private readonly ILogger<SupplierValidationController> _logger;

    public SupplierValidationController(
        ISupplierValidationService validationService,
        ILogger<SupplierValidationController> logger)
    {
        _validationService = validationService;
        _logger = logger;
    }

    /// <summary>
    /// Validates a supplier for purchase order creation
    /// </summary>
    [HttpPost("purchase-order/{businessPartnerId:guid}")]
    public async Task<ActionResult<SupplierValidationResult>> ValidateForPurchaseOrder(Guid businessPartnerId)
    {
        try
        {
            var result = await _validationService.ValidateForPurchaseOrderAsync(businessPartnerId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating supplier {PartnerId} for purchase order", businessPartnerId);
            return StatusCode(500, "An error occurred while validating the supplier");
        }
    }

    /// <summary>
    /// Validates a supplier for RFQ participation
    /// </summary>
    [HttpPost("rfq/{businessPartnerId:guid}")]
    public async Task<ActionResult<SupplierValidationResult>> ValidateForRfq(
        Guid businessPartnerId,
        [FromBody] RfqValidationRequest? request = null)
    {
        try
        {
            var result = await _validationService.ValidateForRfqAsync(
                businessPartnerId,
                request?.CategoryIds,
                request?.MinimumPerformanceRating);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating supplier {PartnerId} for RFQ", businessPartnerId);
            return StatusCode(500, "An error occurred while validating the supplier");
        }
    }

    /// <summary>
    /// Validates a supplier for contract creation
    /// </summary>
    [HttpPost("contract/{businessPartnerId:guid}")]
    public async Task<ActionResult<SupplierValidationResult>> ValidateForContract(
        Guid businessPartnerId,
        [FromQuery] bool requiresLicenses = false)
    {
        try
        {
            var result = await _validationService.ValidateForContractAsync(businessPartnerId, requiresLicenses);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating supplier {PartnerId} for contract", businessPartnerId);
            return StatusCode(500, "An error occurred while validating the supplier");
        }
    }

    /// <summary>
    /// Validates supplier financial health
    /// </summary>
    [HttpPost("financial-health/{businessPartnerId:guid}")]
    public async Task<ActionResult<SupplierValidationResult>> ValidateFinancialHealth(
        Guid businessPartnerId,
        [FromQuery] decimal? minimumCreditRatingScore = null)
    {
        try
        {
            var result = await _validationService.ValidateFinancialHealthAsync(businessPartnerId, minimumCreditRatingScore);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating financial health for supplier {PartnerId}", businessPartnerId);
            return StatusCode(500, "An error occurred while validating financial health");
        }
    }

    /// <summary>
    /// Validates a supplier comprehensively for all procurement activities
    /// </summary>
    [HttpPost("comprehensive/{businessPartnerId:guid}")]
    public async Task<ActionResult<ComprehensiveValidationResult>> ValidateComprehensive(Guid businessPartnerId)
    {
        try
        {
            var poValidation = await _validationService.ValidateForPurchaseOrderAsync(businessPartnerId);
            var rfqValidation = await _validationService.ValidateForRfqAsync(businessPartnerId);
            var contractValidation = await _validationService.ValidateForContractAsync(businessPartnerId, true);
            var financialValidation = await _validationService.ValidateFinancialHealthAsync(businessPartnerId);

            var result = new ComprehensiveValidationResult
            {
                PurchaseOrderValidation = poValidation,
                RfqValidation = rfqValidation,
                ContractValidation = contractValidation,
                FinancialHealthValidation = financialValidation,
                OverallValid = poValidation.IsValid && rfqValidation.IsValid && contractValidation.IsValid
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing comprehensive validation for supplier {PartnerId}", businessPartnerId);
            return StatusCode(500, "An error occurred while performing comprehensive validation");
        }
    }
}

// Request models
public record RfqValidationRequest(List<Guid>? CategoryIds, decimal? MinimumPerformanceRating);

public class ComprehensiveValidationResult
{
    public SupplierValidationResult PurchaseOrderValidation { get; set; } = null!;
    public SupplierValidationResult RfqValidation { get; set; } = null!;
    public SupplierValidationResult ContractValidation { get; set; } = null!;
    public SupplierValidationResult FinancialHealthValidation { get; set; } = null!;
    public bool OverallValid { get; set; }
}

