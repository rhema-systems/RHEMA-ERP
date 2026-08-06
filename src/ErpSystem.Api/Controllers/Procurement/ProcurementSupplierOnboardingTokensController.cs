using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.Procurement;

[ApiController]
[Route("api/procurement/supplier-onboarding-tokens")]
[Authorize]
public sealed class ProcurementSupplierOnboardingTokensController : ControllerBase
{
    private readonly IProcurementSupplierOnboardingTokenService _service;
    private readonly IProcurementSupplierApplicantAccessService _applicantAccess;

    public ProcurementSupplierOnboardingTokensController(
        IProcurementSupplierOnboardingTokenService service,
        IProcurementSupplierApplicantAccessService applicantAccess)
    {
        _service = service;
        _applicantAccess = applicantAccess;
    }

    [HttpGet("summary")]
    public Task<IActionResult> Summary(CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetSummaryAsync(cancellationToken)));

    [HttpGet]
    public Task<IActionResult> Search(
        [FromQuery] ProcurementSupplierOnboardingTokenSearchRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.SearchAsync(request, cancellationToken)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetAsync(id, cancellationToken)));

    [HttpGet("registrations/{registrationId:guid}")]
    public Task<IActionResult> GetForRegistration(
        Guid registrationId,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await _service.GetForRegistrationAsync(registrationId, cancellationToken);
            return Ok(value);
        });

    [HttpGet("{id:guid}/payment-methods")]
    public Task<IActionResult> PaymentMethods(Guid id, CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.GetPaymentMethodsAsync(id, cancellationToken)));

    [HttpPost]
    public Task<IActionResult> Issue(
        [FromBody] IssueProcurementSupplierOnboardingTokenRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await _service.IssueAsync(request, CorrelationId, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = value.Token.Id }, value);
        });

    [HttpPost("{id:guid}/reissue")]
    public Task<IActionResult> Reissue(
        Guid id,
        [FromBody] ReissueProcurementSupplierOnboardingTokenRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await _service.ReissueAsync(
                id, request, CorrelationId, cancellationToken);
            return Ok(await DeliverAndSanitizeAsync(
                value, CorrelationId, cancellationToken));
        });

    [HttpPost("{id:guid}/payments")]
    public Task<IActionResult> RecordPayment(
        Guid id,
        [FromBody] RecordProcurementSupplierOnboardingPaymentRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.RecordPaymentAsync(
            id, request, CorrelationId, cancellationToken)));

    [HttpPost("{tokenId:guid}/payments/{paymentId:guid}/reconcile")]
    public Task<IActionResult> Reconcile(
        Guid tokenId,
        Guid paymentId,
        [FromBody] ReconcileProcurementSupplierOnboardingPaymentRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await _service.ReconcilePaymentAsync(
                tokenId, paymentId, request, CorrelationId, cancellationToken);
            await DeliverAndSanitizeAsync(value, CorrelationId, cancellationToken);
            return Ok(value.Token);
        });

    [HttpPost("{id:guid}/exemptions")]
    public Task<IActionResult> RequestExemption(
        Guid id,
        [FromBody] RequestProcurementSupplierOnboardingExemptionRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () => Ok(await _service.RequestExemptionAsync(
            id, request, CorrelationId, cancellationToken)));

    [HttpPost("{tokenId:guid}/exemptions/{exemptionId:guid}/decision")]
    public Task<IActionResult> DecideExemption(
        Guid tokenId,
        Guid exemptionId,
        [FromBody] DecideProcurementSupplierOnboardingExemptionRequest request,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            var value = await _service.DecideExemptionAsync(
                tokenId, exemptionId, request, CorrelationId, cancellationToken);
            await DeliverAndSanitizeAsync(value, CorrelationId, cancellationToken);
            return Ok(value.Token);
        });

    private async Task<ProcurementSupplierOnboardingTokenIssueResultDto>
        DeliverAndSanitizeAsync(
            ProcurementSupplierOnboardingTokenIssueResultDto value,
            string correlationId,
            CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value.PlaintextToken))
            return value;

        var delivery = await _applicantAccess.DeliverApplicationTokenAsync(
            value.Token.Id,
            value.PlaintextToken,
            correlationId,
            cancellationToken);
        if (!delivery.ApplicantAccessFound)
            return value;
        if (!delivery.Delivered)
            throw new ProcurementSupplierOnboardingTokenConflictException(
                "SUPPLIER_ONBOARDING_TOKEN_DELIVERY_FAILED",
                delivery.FailureMessage ??
                "Application-token delivery failed. Reissue the token to retry delivery.");

        return new ProcurementSupplierOnboardingTokenIssueResultDto
        {
            Token = value.Token,
            PlaintextToken = null
        };
    }

    private string CorrelationId
    {
        get
        {
            var supplied = Request.Headers["X-Correlation-ID"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(supplied)
                ? string.IsNullOrWhiteSpace(HttpContext.TraceIdentifier)
                    ? Guid.NewGuid().ToString("N")
                    : HttpContext.TraceIdentifier
                : supplied;
        }
    }

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ProcurementSupplierOnboardingTokenNotFoundException exception)
        {
            return NotFound(Problem(404, exception.Code,
                "Supplier-onboarding token not found", exception.Message));
        }
        catch (ProcurementSupplierOnboardingTokenAuthorizationException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Problem(403, "SUPPLIER_ONBOARDING_TOKEN_ACCESS_FORBIDDEN",
                    "Supplier-onboarding token access forbidden", exception.Message));
        }
        catch (ProcurementSupplierOnboardingTokenConflictException exception)
        {
            return Conflict(Problem(409, exception.Code,
                "Supplier-onboarding token conflict", exception.Message));
        }
        catch (ProcurementSupplierOnboardingTokenValidationException exception)
        {
            var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [exception.Code] = [exception.Message]
            })
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Supplier-onboarding token validation failed",
                Detail = exception.Message,
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["code"] = exception.Code;
            problem.Extensions["correlationId"] = CorrelationId;
            return UnprocessableEntity(problem);
        }
    }

    private ProblemDetails Problem(int status, string code, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
        Instance = HttpContext.Request.Path,
        Extensions =
        {
            ["code"] = code,
            ["correlationId"] = CorrelationId
        }
    };
}
