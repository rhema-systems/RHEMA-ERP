using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
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
    private readonly IProcurementAccessControlService _accessControl;
    private readonly ICurrentUserProvider _currentUser;
    private readonly ILogger<SupplierValidationController> _logger;

    public SupplierValidationController(
        ISupplierValidationService validationService,
        IProcurementAccessControlService accessControl,
        ICurrentUserProvider currentUser,
        ILogger<SupplierValidationController> logger)
    {
        _validationService = validationService;
        _accessControl = accessControl;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>
    /// Returns one tenant-safe, explainable supplier eligibility decision without mutating procurement state.
    /// </summary>
    [HttpPost("eligibility")]
    public async Task<IActionResult> EvaluateEligibility(
        [FromBody] SupplierEligibilityApiRequest request,
        CancellationToken cancellationToken)
    {
        var correlationId = CorrelationId;
        try
        {
            await EnsureReaderAsync(request.BusinessPartnerId.ToString("N"), correlationId, cancellationToken);
            var result = await _validationService.EvaluateEligibilityAsync(
                new SupplierEligibilityEvaluationRequest
                {
                    BusinessPartnerId = request.BusinessPartnerId,
                    Boundary = request.Boundary,
                    CategoryIds = request.CategoryIds ?? new List<Guid>(),
                    RequiresPrequalification = request.RequiresPrequalification,
                    RequiresLicenses = request.RequiresLicenses,
                    IncludeFinancialWarnings = request.IncludeFinancialWarnings,
                    MinimumPerformanceRating = request.MinimumPerformanceRating,
                    RecordAudit = false
                }, cancellationToken);
            return Ok(result);
        }
        catch (SupplierEligibilityAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Problem(exception.Message, statusCode: StatusCodes.Status403Forbidden,
                    title: "SUPPLIER_ELIGIBILITY_FORBIDDEN"));
        }
        catch (ProcurementAccessAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Problem(exception.Message, statusCode: StatusCodes.Status403Forbidden,
                    title: "SUPPLIER_ELIGIBILITY_FORBIDDEN"));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Error evaluating supplier eligibility with correlation {CorrelationId}", correlationId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                Problem("The supplier eligibility decision could not be evaluated.",
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "SUPPLIER_ELIGIBILITY_FAILED"));
        }
    }

    [HttpGet("boundaries")]
    public ActionResult<IReadOnlyList<object>> GetBoundaries() =>
        Ok(Enum.GetValues<SupplierEligibilityBoundary>()
            .Select(value => new { value = value.ToString(), label = SplitName(value.ToString()) })
            .ToList());

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

    private async Task EnsureReaderAsync(
        string sourceReference,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.TenantId == Guid.Empty ||
            _currentUser.UserId == Guid.Empty || _currentUser.IsExternalUser)
            throw new SupplierEligibilityAuthorizationException(
                "Internal supplier-management access is required.");
        var review = await _accessControl.CheckCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = "procurement.supplier.review",
            SourceType = "SupplierEligibility",
            SourceReference = sourceReference
        }, correlationId, cancellationToken);
        if (review.Allowed) return;

        var manage = await _accessControl.CheckCapabilityAsync(new ProcurementAccessCapabilityRequest
        {
            PermissionCode = "procurement.supplier.manage",
            SourceType = "SupplierEligibility",
            SourceReference = sourceReference
        }, correlationId, cancellationToken);
        if (!manage.Allowed)
            throw new ProcurementAccessAuthorizationException(manage.Message);
    }

    private string CorrelationId =>
        Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

    private static string SplitName(string value) =>
        string.Concat(value.Select((character, index) =>
            index > 0 && char.IsUpper(character) ? $" {character}" : character.ToString()));
}

// Request models
public record RfqValidationRequest(List<Guid>? CategoryIds, decimal? MinimumPerformanceRating);

public sealed class SupplierEligibilityApiRequest
{
    public Guid BusinessPartnerId { get; set; }
    public SupplierEligibilityBoundary Boundary { get; set; } = SupplierEligibilityBoundary.StatusReview;
    public List<Guid>? CategoryIds { get; set; }
    public bool RequiresPrequalification { get; set; }
    public bool RequiresLicenses { get; set; }
    public bool IncludeFinancialWarnings { get; set; } = true;
    public decimal? MinimumPerformanceRating { get; set; }
}

public class ComprehensiveValidationResult
{
    public SupplierValidationResult PurchaseOrderValidation { get; set; } = null!;
    public SupplierValidationResult RfqValidation { get; set; } = null!;
    public SupplierValidationResult ContractValidation { get; set; } = null!;
    public SupplierValidationResult FinancialHealthValidation { get; set; } = null!;
    public bool OverallValid { get; set; }
}
