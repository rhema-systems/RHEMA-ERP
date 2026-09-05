using ErpSystem.Api.Services.HR;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Medical;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.DocumentManagement;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Lets an employee file and follow their own medical expense claims, and read their own
/// insurance coverage and clinic appointments.
/// </summary>
/// <remarks>
/// <para>A reimbursement claim is the employee's own transaction, not an HR record about them, so
/// it is filed here rather than through <see cref="MedicalExpenseClaimsController"/> — which is the
/// HR caseload surface and requires a medical permission an employee does not hold. Filing through
/// HR would also mean the same function both raises and adjudicates a claim, which defeats the
/// approver recorded on it.</para>
///
/// <para>The coverage and appointment reads (area 25 slice 8) follow the same law: the HR
/// registers on <see cref="MedicalInsuranceController"/> and <see cref="MedicalClinicalController"/>
/// stay behind the medical policies, and the self arms live here — read-only, because a policy is
/// employer-administered and an appointment is booked by the clinic; corrections go through HR.</para>
///
/// <para>The scoping rule is the one <see cref="EmployeeHealthSelfServiceController"/> already
/// states, and it has no exceptions: every claim is resolved through the employee id on the token,
/// and <b>no route or query parameter on this controller may ever carry an employee id</b>. A claim
/// id that belongs to somebody else is a lookup miss — 404, not 403 — so this surface cannot be
/// used to discover which claims exist.</para>
///
/// <para><b>Deliberately absent.</b> Approval, payment, flagging and claim notes are HR actions and
/// have no route here; notes in particular carry adjudicator commentary marked internal, so this
/// controller never calls the note service at all rather than relying on a flag.</para>
///
/// <para>Receipts are uploaded as multipart content through the same controlled gate the HR endpoint
/// uses — scanned, registered in the DMS, and stored outside the web root. Ownership is checked
/// before the file is accepted, so an unowned claim id cannot even cause a file to be scanned.</para>
/// </remarks>
[ApiController]
[Route("api/medical/me")]
[Authorize(Policy = "InternalOnly")]
public class MedicalSelfServiceController : MedicalControllerBase
{
    private readonly IMedicalExpenseClaimService _claims;
    private readonly IMedicalInsuranceService _insurance;
    private readonly IMedicalClinicalService _clinical;
    private readonly IHrControlledDocumentService _hrDocuments;
    private readonly ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorage;
    private readonly ApplicationDbContext _db;

    public MedicalSelfServiceController(
        IMedicalExpenseClaimService claims,
        IMedicalInsuranceService insurance,
        IMedicalClinicalService clinical,
        IHrControlledDocumentService hrDocuments,
        ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorage,
        ApplicationDbContext db,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _claims = claims;
        _insurance = insurance;
        _clinical = clinical;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorage = fileStorage;
        _db = db;
    }

    /// <summary>Files a claim for the authenticated employee.</summary>
    [HttpPost("expense-claims")]
    public async Task<IActionResult> FileOwnClaim(
        [FromBody] CreateMedicalExpenseClaimDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out var tenantId, out var userId, out var employeeId,
                "Filing your own medical claim") is { } error) return error;

        // The subject is the token's employee, always. A supplied EmployeeId is ignored rather
        // than rejected, because the field is shared with the HR endpoint where it is required.
        dto.EmployeeId = employeeId;

        var created = await _claims.CreateClaimAsync(dto, employeeId, tenantId, userId, ct);
        return CreatedAtAction(nameof(GetOwnClaim), new { id = created.Id }, Project(created));
    }

    /// <summary>Lists the authenticated employee's own claims.</summary>
    [HttpGet("expense-claims")]
    public async Task<IActionResult> GetOwnClaims(CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Listing your own medical claims") is { } error) return error;

        var claims = await _claims.GetClaimsByEmployeeAsync(employeeId, ct);
        // IsFlaggedForReview is on the summary DTO and is deliberately NOT projected: telling a
        // claimant their claim has been flagged for review would defeat the point of flagging it.
        return Ok(claims.Select(claim => new
        {
            claim.Id,
            claim.ClaimNumber,
            claim.ClaimDate,
            claim.ServiceDate,
            claim.ExpenseType,
            claim.FacilityName,
            claim.Status,
            claim.AmountRequested,
            claim.AmountApproved,
        }));
    }

    /// <summary>Returns one of the authenticated employee's own claims.</summary>
    [HttpGet("expense-claims/{id:guid}")]
    public async Task<IActionResult> GetOwnClaim(Guid id, CancellationToken ct)
    {
        var owned = await LoadOwnClaimAsync(id, ct);
        if (owned.Error != null) return owned.Error;

        return Ok(Project(owned.Claim!));
    }

    /// <summary>Returns the lines on one of the authenticated employee's own claims.</summary>
    [HttpGet("expense-claims/{id:guid}/items")]
    public async Task<IActionResult> GetOwnClaimItems(Guid id, CancellationToken ct)
    {
        var owned = await LoadOwnClaimAsync(id, ct);
        if (owned.Error != null) return owned.Error;

        var items = await _claims.GetItemsAsync(id, ct);
        return Ok(items.Select(item => new
        {
            item.Id,
            item.Description,
            item.ItemType,
            item.Quantity,
            item.UnitCost,
            item.Remarks,
        }));
    }

    /// <summary>Adds a line to one of the authenticated employee's own claims.</summary>
    [HttpPost("expense-claims/{id:guid}/items")]
    public async Task<IActionResult> AddOwnClaimItem(
        Guid id,
        [FromBody] CreateMedicalExpenseItemDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetWriteContext(out var tenantId, out var userId) is { } contextError) return contextError;

        // Ownership is re-checked here rather than trusted from the body: this is an id-addressed
        // child, and a child that only validates its own id is how a self-service surface leaks.
        var owned = await LoadOwnClaimAsync(id, ct);
        if (owned.Error != null) return owned.Error;

        dto.ClaimId = id;

        var created = await _claims.AddItemAsync(dto, tenantId, userId, ct);
        return Ok(new { created.Id, created.Description, created.ItemType, created.Quantity, created.UnitCost });
    }

    /// <summary>Lists documents attached to one of the authenticated employee's own claims.</summary>
    /// <remarks>Read-only — see the note on this controller about receipt upload.</remarks>
    [HttpGet("expense-claims/{id:guid}/documents")]
    public async Task<IActionResult> GetOwnClaimDocuments(Guid id, CancellationToken ct)
    {
        var owned = await LoadOwnClaimAsync(id, ct);
        if (owned.Error != null) return owned.Error;

        var documents = await _claims.GetDocumentsAsync(id, ct);
        // FilePath is deliberately not projected — it is a server path and of no use to a claimant.
        return Ok(documents.Select(document => new
        {
            document.Id,
            document.FileName,
            document.Type,
            document.Description,
            document.UploadDate,
        }));
    }

    /// <summary>Attaches a receipt to one of the authenticated employee's own claims.</summary>
    [HttpPost("expense-claims/{id:guid}/documents")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> AddOwnClaimDocument(
        Guid id,
        IFormFile file,
        [FromForm] MedicalDocumentType type,
        [FromForm] string? description,
        CancellationToken ct)
    {
        if (TryGetWriteContext(out var tenantId, out var userId) is { } contextError) return contextError;

        // Ownership before storage: an unowned claim id must not even cause a file to be scanned
        // and registered, let alone attached.
        var owned = await LoadOwnClaimAsync(id, ct);
        if (owned.Error != null) return owned.Error;

        return await MedicalClaimDocumentUpload.ExecuteAsync(
            this, _hrDocuments, _claims, id, file, type, description,
            tenantId, userId, CurrentUser.UserName, ct);
    }

    /// <summary>Streams a document attached to one of the authenticated employee's own claims.</summary>
    [HttpGet("expense-claims/{id:guid}/documents/{documentId:guid}/download")]
    public async Task<IActionResult> DownloadOwnClaimDocument(
        Guid id,
        Guid documentId,
        CancellationToken ct)
    {
        if (TryGetWriteContext(out var tenantId, out _) is { } contextError) return contextError;

        var owned = await LoadOwnClaimAsync(id, ct);
        if (owned.Error != null) return owned.Error;

        // The document is resolved through the owned claim, so a document id belonging to somebody
        // else's claim is a lookup miss rather than a disclosure. The claim id in the route is not
        // decoration — it is what makes that join possible.
        var document = await _db.Set<MedicalExpenseDocument>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == documentId &&
                        item.ClaimId == id &&
                        item.TenantId == tenantId &&
                        !item.IsDeleted, ct);
        if (document is null) return NotFound();

        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            document.DocumentRecordId, document.DocumentVersionId,
            document.FileUploadRecordId, document.FilePath,
            document.FileName, fallbackContentType: null,
            inline: false, ct);
    }

    // ── My coverage (area 25 slice 8) ────────────────────────────────────────────────────────

    /// <summary>Lists the authenticated employee's own insurance policies.</summary>
    [HttpGet("insurance-policies")]
    public async Task<IActionResult> GetOwnPolicies(CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Reading your own medical coverage") is { } error) return error;

        var policies = await _insurance.GetPoliciesByEmployeeAsync(employeeId, ct);
        return Ok(policies.Select(policy => new
        {
            policy.Id,
            policy.ProviderName,
            policy.PlanName,
            policy.PolicyNumber,
            policy.StartDate,
            policy.EndDate,
            policy.RemainingLimit,
            policy.Status,
            policy.StatusName,
            policy.IsActive,
        }));
    }

    /// <summary>Returns the authenticated employee's currently active policy, or 404 when none.</summary>
    [HttpGet("insurance-policies/active")]
    public async Task<IActionResult> GetOwnActivePolicy(CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Reading your own medical coverage") is { } error) return error;

        var policy = await _insurance.GetActivePolicyForEmployeeAsync(employeeId, ct);
        if (policy is null) return NotFound();

        return Ok(ProjectPolicy(policy));
    }

    /// <summary>Returns one of the authenticated employee's own policies.</summary>
    [HttpGet("insurance-policies/{id:guid}")]
    public async Task<IActionResult> GetOwnPolicy(Guid id, CancellationToken ct)
    {
        var owned = await LoadOwnPolicyAsync(id, ct);
        if (owned.Error != null) return owned.Error;

        return Ok(ProjectPolicy(owned.Policy!));
    }

    /// <summary>Lists the dependants covered on one of the authenticated employee's own policies.</summary>
    /// <remarks>Reached only through an owned policy id — the dependant DTO carries no employee id
    /// of its own, so ownership of the parent is the only scoping there is.</remarks>
    [HttpGet("insurance-policies/{id:guid}/dependents")]
    public async Task<IActionResult> GetOwnPolicyDependents(Guid id, CancellationToken ct)
    {
        var owned = await LoadOwnPolicyAsync(id, ct);
        if (owned.Error != null) return owned.Error;

        var dependents = await _insurance.GetPolicyDependentsAsync(id, ct);
        return Ok(dependents.Select(dependent => new
        {
            dependent.Id,
            dependent.DependentName,
            dependent.Relationship,
            dependent.MembershipNumber,
            dependent.CoverageStartDate,
            dependent.CoverageEndDate,
            dependent.AnnualLimit,
            dependent.UtilizedAmount,
            dependent.RemainingLimit,
            dependent.IsActive,
        }));
    }

    // ── My appointments (area 25 slice 8) ────────────────────────────────────────────────────

    /// <summary>Lists the authenticated employee's own clinic appointments.</summary>
    [HttpGet("appointments")]
    public async Task<IActionResult> GetOwnAppointments(CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Reading your own medical appointments") is { } error) return error;

        var appointments = await _clinical.GetAppointmentsByEmployeeAsync(employeeId, ct);
        return Ok(appointments.Select(appointment => new
        {
            appointment.Id,
            appointment.AppointmentNumber,
            appointment.FacilityName,
            appointment.AppointmentDateTime,
            appointment.Status,
            appointment.StatusName,
        }));
    }

    /// <summary>Returns one of the authenticated employee's own appointments.</summary>
    [HttpGet("appointments/{id:guid}")]
    public async Task<IActionResult> GetOwnAppointment(Guid id, CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Reading your own medical appointments") is { } error) return error;

        MedicalAppointmentDto appointment;
        try
        {
            appointment = await _clinical.GetAppointmentByIdAsync(id, ct);
        }
        catch (MedicalWorkflowException)
        {
            return NotFound();
        }

        if (appointment.EmployeeId != employeeId) return NotFound();

        // Notes carry desk/clinician commentary and are deliberately not projected; the outcome
        // summary is the part written for the patient.
        return Ok(new
        {
            appointment.Id,
            appointment.AppointmentNumber,
            appointment.IsForDependent,
            appointment.DependentName,
            appointment.FacilityName,
            appointment.PhysicianName,
            appointment.AppointmentDateTime,
            appointment.DurationMinutes,
            appointment.ServiceType,
            appointment.ServiceTypeName,
            appointment.Purpose,
            appointment.Status,
            appointment.StatusName,
            appointment.CheckInTime,
            appointment.CheckOutTime,
            appointment.OutcomeSummary,
            appointment.CancellationReason,
        });
    }

    /// <summary>
    /// Resolves a policy and confirms it belongs to the authenticated employee — the same single
    /// choke point <see cref="LoadOwnClaimAsync"/> is for claims. Somebody else's policy id is a
    /// 404 lookup miss, never a 403.
    /// </summary>
    private async Task<(EmployeeMedicalInsurancePolicyDto? Policy, IActionResult? Error)> LoadOwnPolicyAsync(
        Guid id, CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Reading your own medical coverage") is { } error)
            return (null, error);

        EmployeeMedicalInsurancePolicyDto policy;
        try
        {
            policy = await _insurance.GetPolicyByIdAsync(id, ct);
        }
        catch (MedicalWorkflowException)
        {
            return (null, NotFound());
        }

        if (policy.EmployeeId != employeeId)
            return (null, NotFound());

        return (policy, null);
    }

    /// <summary>
    /// The policy fields the covered employee may see. Free-text cancellation reasons and notes are
    /// HR commentary and stay desk-side; the utilisation figures are the point of the screen.
    /// </summary>
    private static object ProjectPolicy(EmployeeMedicalInsurancePolicyDto policy) => new
    {
        policy.Id,
        policy.ProviderName,
        policy.PlanName,
        policy.BenefitTierName,
        policy.PolicyNumber,
        policy.MembershipNumber,
        policy.StartDate,
        policy.EndDate,
        policy.AnnualLimit,
        policy.UtilizedAmount,
        policy.RemainingLimit,
        policy.UtilizationPercentage,
        policy.CoversDependents,
        policy.Status,
        policy.StatusName,
        policy.IsActive,
        policy.CancellationDate,
    };

    /// <summary>
    /// Resolves a claim and confirms it belongs to the authenticated employee. Every id-addressed
    /// claim operation on this controller goes through here — it is the single point where "own" is
    /// defined, so a route added later cannot forget to ask.
    /// </summary>
    /// <remarks>
    /// A claim that exists but belongs to somebody else returns the same 404 as one that does not
    /// exist. Distinguishing them would turn this surface into a way of discovering claim ids.
    /// </remarks>
    private async Task<(MedicalExpenseClaimDto? Claim, IActionResult? Error)> LoadOwnClaimAsync(
        Guid id, CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Reading your own medical claims") is { } error)
            return (null, error);

        MedicalExpenseClaimDto claim;
        try
        {
            claim = await _claims.GetClaimByIdAsync(id, ct);
        }
        catch (MedicalWorkflowException)
        {
            return (null, NotFound());
        }

        if (claim.EmployeeId != employeeId)
            return (null, NotFound());

        return (claim, null);
    }

    /// <summary>
    /// The claim fields a claimant may see. Projected explicitly rather than returning the DTO, so
    /// a field added to the HR contract later does not silently appear on the employee's copy.
    /// </summary>
    private static object Project(MedicalExpenseClaimDto claim) => new
    {
        claim.Id,
        claim.ClaimNumber,
        claim.ServiceDate,
        claim.ServiceEndDate,
        claim.ExpenseType,
        claim.Description,
        claim.FacilityId,
        claim.FacilityName,
        claim.PhysicianName,
        claim.IsEmergency,
        claim.RequiredHospitalization,
        claim.TotalAmount,
        claim.AmountRequested,
        claim.AmountApproved,
        claim.Status,
        claim.ClaimDate,
    };
}
