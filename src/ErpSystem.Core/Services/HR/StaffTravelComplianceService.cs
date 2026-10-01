using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Shared;
using ErpSystem.Core.Interfaces.Events;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Application.HR.Extensions;
using ErpSystem.Core.Entities.HR.StaffTravel;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

// ============================================================================
// STAFF TRAVEL — GROUP 7: COMPLIANCE & SAFETY SERVICE
// ============================================================================

#region Staff Travel Compliance Service

public class StaffTravelComplianceService : IStaffTravelComplianceService
{
    private readonly IStaffTravelDocumentRepository _documentRepository;
    private readonly IStaffTravelVisaRequirementRepository _visaRequirementRepository;
    private readonly IStaffTravelVisaApplicationRepository _visaApplicationRepository;
    private readonly IStaffTravelRiskAssessmentRepository _riskAssessmentRepository;
    private readonly IStaffTravelAlertRepository _alertRepository;
    private readonly IStaffTravelAlertNotificationRepository _notificationRepository;
    private readonly IStaffTravelInsurancePolicyRepository _insuranceRepository;
    private readonly IStaffTravelHealthRequirementRepository _healthRequirementRepository;
    private readonly IStaffTravelRequestRepository _requestRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly HrCurrencyBridge _currency;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppEventBus _appEventBus;
    private readonly ILogger<StaffTravelComplianceService> _logger;

    public StaffTravelComplianceService(
        IStaffTravelDocumentRepository documentRepository,
        IStaffTravelVisaRequirementRepository visaRequirementRepository,
        IStaffTravelVisaApplicationRepository visaApplicationRepository,
        IStaffTravelRiskAssessmentRepository riskAssessmentRepository,
        IStaffTravelAlertRepository alertRepository,
        IStaffTravelAlertNotificationRepository notificationRepository,
        IStaffTravelInsurancePolicyRepository insuranceRepository,
        IStaffTravelHealthRequirementRepository healthRequirementRepository,
        IStaffTravelRequestRepository requestRepository,
        IEmployeeRepository employeeRepository,
        HrCurrencyBridge currency,
        ICurrentUserProvider currentUserProvider,
        IUnitOfWork unitOfWork,
        IAppEventBus appEventBus,
        ILogger<StaffTravelComplianceService> logger)
    {
        _documentRepository = documentRepository;
        _visaRequirementRepository = visaRequirementRepository;
        _visaApplicationRepository = visaApplicationRepository;
        _riskAssessmentRepository = riskAssessmentRepository;
        _alertRepository = alertRepository;
        _notificationRepository = notificationRepository;
        _insuranceRepository = insuranceRepository;
        _healthRequirementRepository = healthRequirementRepository;
        _requestRepository = requestRepository;
        _employeeRepository = employeeRepository;
        _currency = currency;
        _currentUserProvider = currentUserProvider;
        _unitOfWork = unitOfWork;
        _appEventBus = appEventBus;
        _logger = logger;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant query-filter and
    // TenantId auto-stamp are inert. Following the RHEMA convention, this service scopes every read and
    // mutation to the authenticated tenant explicitly and passes it into the repository predicate.
    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    private Guid RequireCurrentTenant(Guid tenantId)
    {
        var current = GetTenantId();
        if (tenantId != Guid.Empty && tenantId != current)
            throw new UnauthorizedAccessException("The supplied tenant does not match the authenticated tenant.");
        return current;
    }

    private async Task<StaffTravelDocument> GetOwnedDocumentAsync(Guid id)
    {
        var entity = await _documentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Travel document with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelVisaRequirement> GetOwnedVisaRequirementAsync(Guid id)
    {
        var entity = await _visaRequirementRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Visa requirement with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelVisaApplication> GetOwnedVisaApplicationAsync(Guid id)
    {
        var entity = await _visaApplicationRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Visa application with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelRiskAssessment> GetOwnedRiskAssessmentAsync(Guid id)
    {
        var entity = await _riskAssessmentRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Risk assessment with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelAlert> GetOwnedAlertAsync(Guid id)
    {
        var entity = await _alertRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Travel alert with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelAlertNotification> GetOwnedNotificationAsync(Guid id)
    {
        var entity = await _notificationRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Alert notification with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelInsurancePolicy> GetOwnedInsuranceAsync(Guid id)
    {
        var entity = await _insuranceRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Insurance policy with ID '{id}' not found.");
        return entity;
    }

    private async Task<StaffTravelHealthRequirement> GetOwnedHealthRequirementAsync(Guid id)
    {
        var entity = await _healthRequirementRepository.GetByIdAsync(id);
        if (entity == null || entity.TenantId != GetTenantId())
            throw new ArgumentException($"Health requirement with ID '{id}' not found.");
        return entity;
    }

    // ---- Travel documents --------------------------------------------------

    public async Task<StaffTravelDocumentDto> GetDocumentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _documentRepository.GetWithDetailsAsync(GetTenantId(), id)
            ?? throw new ArgumentException($"Document with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelDocumentDto>> GetAllDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _documentRepository.GetAllWithDetailsAsync())
            .Where(d => d.TenantId == tenantId)
            .Select(d => d.ToDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelDocumentDto>> GetDocumentsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _documentRepository.GetByEmployeeIdAsync(employeeId))
            .Where(d => d.TenantId == tenantId)
            .Select(d => d.ToDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelDocumentDto>> GetExpiringDocumentsAsync(int daysAhead = 90, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _documentRepository.GetExpiringDocumentsAsync(daysAhead))
            .Where(d => d.TenantId == tenantId)
            .Select(d => d.ToDto())
            .ToList();
    }

    public async Task<StaffTravelDocumentDto> CreateDocumentAsync(CreateStaffTravelDocumentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await RequireOwnedEmployeeAsync(createDto.EmployeeId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _documentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _documentRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<StaffTravelDocumentDto> UpdateDocumentAsync(UpdateStaffTravelDocumentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _documentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _documentRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<bool> VerifyDocumentAsync(VerifyStaffTravelDocumentDto verifyDto, Guid verifierEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(verifyDto.DocumentId);

        entity.IsVerified = true;
        entity.VerifiedById = verifierEmployeeId;   // the caller, not a payload value
        entity.VerifiedAt = DateTime.UtcNow;        // ...and the clock, not one either
        // UpdatedBy is an audit field and takes the USER id; VerifiedById above is the Employee
        // FK. Two identifiers, two different things — the same split slice 1 had to untangle.
        entity.UpdatedBy = _currentUserProvider.UserId.ToString();
        entity.UpdatedAt = DateTime.UtcNow;

        await _documentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteDocumentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(id);
        await _documentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Visa requirements -------------------------------------------------

    public async Task<StaffTravelVisaRequirementDto?> GetVisaRequirementAsync(Guid passportCountryId, Guid destinationCountryId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _visaRequirementRepository.GetRequirementAsync(passportCountryId, destinationCountryId);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelVisaRequirementDto>> GetVisaRequirementsByDestinationAsync(Guid destinationCountryId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _visaRequirementRepository.GetByDestinationAsync(destinationCountryId))
            .Where(r => r.TenantId == tenantId)
            .Select(r => r.ToDto())
            .ToList();
    }

    /// <summary>
    /// Re-reads a saved requirement through the include-carrying lookup, so the write response
    /// names its two countries.
    /// </summary>
    /// <remarks>
    /// ⚠ <c>ToDto</c> reads <c>entity.PassportCountry?.Name</c>, and a just-constructed entity has
    /// no navigation loaded — so create and update both answered with two null country names while
    /// the lookup beside them answered "Ghana" and "United Kingdom". Measured 2026-09-01. A screen
    /// that renders the write response instead of refetching shows a row naming nobody, which is
    /// the shape area 13's DevelopmentPanel shipped as "undefined — undefined".
    /// </remarks>
    private async Task<StaffTravelVisaRequirementDto> ReadBackVisaRequirementAsync(StaffTravelVisaRequirement entity)
    {
        var saved = await _visaRequirementRepository.GetRequirementAsync(
            entity.PassportCountryId, entity.DestinationCountryId);
        return (saved ?? entity).ToDto();
    }

    /// <summary>
    /// Refuses a second requirement for a country pair that already has one.
    /// </summary>
    /// <remarks>
    /// <para>⚠ Nothing stopped it. Probed 2026-09-01: Ghana → United Kingdom was saved twice, once
    /// as <c>EmbassyVisa</c> and once as <c>VisaFree</c>, and both were accepted. This table exists
    /// to answer exactly one question — *what does this passport need for this destination* — and
    /// <c>GetRequirementAsync</c> answers it with <c>FirstOrDefaultAsync</c>, so with two rows it
    /// returns whichever the database hands back first. A traveller could be told a visa is not
    /// required by a register that also says an embassy visa is.</para>
    ///
    /// <para>Enforced here rather than by a unique index, deliberately: the delete is a SOFT delete,
    /// and this module has met "a soft delete does not release a unique index" nine times. Querying
    /// the repository excludes tombstones by construction, so a pair can be retired and entered
    /// again — which a filtered index would also allow, but only if the filter were written
    /// correctly, and only after a migration.</para>
    /// </remarks>
    private async Task RequireUnclaimedCountryPairAsync(Guid passportCountryId, Guid destinationCountryId)
    {
        var existing = await _visaRequirementRepository.GetRequirementAsync(passportCountryId, destinationCountryId);
        if (existing == null || existing.TenantId != GetTenantId()) return;

        throw new InvalidOperationException(
            $"A visa requirement for {existing.PassportCountry?.Name ?? "that passport"} travelling to "
            + $"{existing.DestinationCountry?.Name ?? "that destination"} already exists, and there can only be one — "
            + "two would give the same traveller two different answers. Edit the existing one instead.");
    }

    public async Task<StaffTravelVisaRequirementDto> CreateVisaRequirementAsync(CreateStaffTravelVisaRequirementDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await RequireUnclaimedCountryPairAsync(createDto.PassportCountryId, createDto.DestinationCountryId);

        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _visaRequirementRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadBackVisaRequirementAsync(entity);
    }

    public async Task<StaffTravelVisaRequirementDto> UpdateVisaRequirementAsync(UpdateStaffTravelVisaRequirementDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedVisaRequirementAsync(updateDto.Id);
        // ⚠ The update DTO carries no country fields, so an edit cannot move a requirement onto
        // another pair and the uniqueness check above does not need repeating here.
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _visaRequirementRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await ReadBackVisaRequirementAsync(entity);
    }

    public async Task<bool> DeleteVisaRequirementAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedVisaRequirementAsync(id);
        await _visaRequirementRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Visa applications --------------------------------------------------

    public async Task<StaffTravelVisaApplicationDto> GetVisaApplicationByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _visaApplicationRepository.GetWithDetailsAsync(GetTenantId(), id)
            ?? throw new ArgumentException($"VisaApplication with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelVisaApplicationSummaryDto>> GetAllVisaApplicationsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _visaApplicationRepository.GetAllWithDetailsAsync())
            .Where(v => v.TenantId == tenantId)
            .Select(v => v.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelVisaApplicationSummaryDto>> GetVisaApplicationsByStatusAsync(VisaApplicationStatus status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _visaApplicationRepository.GetByStatusAsync(status))
            .Where(v => v.TenantId == tenantId)
            .Select(v => v.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelVisaApplicationSummaryDto>> GetVisaApplicationsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _visaApplicationRepository.GetByRequestIdAsync(requestId))
            .Where(v => v.TenantId == tenantId)
            .Select(v => v.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelVisaApplicationSummaryDto>> GetVisaApplicationsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _visaApplicationRepository.GetByEmployeeIdAsync(employeeId))
            .Where(v => v.TenantId == tenantId)
            .Select(v => v.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelVisaApplicationSummaryDto>> GetExpiringVisasAsync(int daysAhead = 90, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _visaApplicationRepository.GetExpiringVisasAsync(daysAhead))
            .Where(v => v.TenantId == tenantId)
            .Select(v => v.ToSummaryDto())
            .ToList();
    }

    public async Task<StaffTravelVisaApplicationDto> CreateVisaApplicationAsync(CreateStaffTravelVisaApplicationDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        // A visa application may record no fee, so the currency is optional here — but if one
        // is given it still has to be a currency Finance holds.
        await _currency.RequireKnownCurrencyAsync(createDto.CurrencyCode, cancellationToken, optional: true);
        await RequireOwnedRequestAsync(createDto.StaffTravelRequestId);
        await RequireOwnedEmployeeAsync(createDto.EmployeeId);
        // The status is an input since lane 0 of the travel final closure. A JSON name the enum does
        // not have is refused at binding; a bare number is not, so it is checked here.
        if (!Enum.IsDefined(createDto.Status))
            throw new InvalidOperationException($"{(int)createDto.Status} is not a visa application status.");
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _visaApplicationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _visaApplicationRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<StaffTravelVisaApplicationDto> UpdateVisaApplicationAsync(UpdateStaffTravelVisaApplicationDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedVisaApplicationAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _visaApplicationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _visaApplicationRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<bool> DeleteVisaApplicationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedVisaApplicationAsync(id);
        await _visaApplicationRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Risk assessments --------------------------------------------------

    public async Task<StaffTravelRiskAssessmentDto> GetRiskAssessmentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _riskAssessmentRepository.GetWithDetailsAsync(GetTenantId(), id)
            ?? throw new ArgumentException($"RiskAssessment with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelRiskAssessmentDto>> GetRiskAssessmentsByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _riskAssessmentRepository.GetByRequestIdAsync(requestId))
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.ToDto())
            .ToList();
    }

    public async Task<StaffTravelRiskAssessmentDto?> GetCurrentRiskAssessmentAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _riskAssessmentRepository.GetCurrentForRequestAsync(requestId);
        if (entity == null || entity.TenantId != tenantId)
            return null;
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelRiskAssessmentDto>> GetAssessmentsRequiringAcknowledgementAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _riskAssessmentRepository.GetRequiringAcknowledgementAsync())
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.ToDto())
            .ToList();
    }

    /// <summary>Records a risk assessment for a trip's destination.</summary>
    /// <remarks>
    /// ⚠ <b><c>AssessedById</c> on the payload is ignored.</b> It is an Employee FK naming who
    /// judged the destination safe or unsafe, and it was a client input — so a security assessment
    /// could be attributed to a colleague who never made it. The assessor is the caller, falling
    /// back to null for an account with no employee link rather than refusing the write, since the
    /// assessment itself is still worth recording.
    /// </remarks>
    public async Task<StaffTravelRiskAssessmentDto> CreateRiskAssessmentAsync(CreateStaffTravelRiskAssessmentDto createDto, Guid tenantId, Guid createdByUserId, Guid? assessorEmployeeId = null, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await RequireOwnedRequestAsync(createDto.StaffTravelRequestId);
        createDto.AssessedById = assessorEmployeeId;
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _riskAssessmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _riskAssessmentRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<StaffTravelRiskAssessmentDto> UpdateRiskAssessmentAsync(UpdateStaffTravelRiskAssessmentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRiskAssessmentAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _riskAssessmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _riskAssessmentRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    /// <summary>
    /// Records that the traveller has read and accepted the risk assessment for their trip.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>Only the traveller can acknowledge.</b> The endpoint took no actor at all and is
    /// <c>HR.Travel.Write</c>-gated, so a travel clerk could set <c>EmployeeAcknowledged</c> on the
    /// employee's behalf — making the record assert something that had not happened, about a safety
    /// briefing, for a trip the assessment exists to warn someone about. The entity has no
    /// <c>AcknowledgedById</c> column, so rather than add one the caller is required to *be* the
    /// traveller; that is what the flag has always claimed.</para>
    ///
    /// <para>This is the area-9 acknowledge-with-no-actor shape. There it was a disciplinary
    /// notice; here it is a security warning about a destination.</para>
    /// </remarks>
    public async Task<bool> AcknowledgeRiskAssessmentAsync(AcknowledgeStaffTravelRiskAssessmentDto acknowledgeDto, Guid acknowledgerEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRiskAssessmentAsync(acknowledgeDto.RiskAssessmentId);

        var request = await _requestRepository.GetByIdAsync(entity.StaffTravelRequestId);
        if (request is null || request.TenantId != GetTenantId())
            throw new ArgumentException("The travel request for this risk assessment was not found.");

        if (request.EmployeeId != acknowledgerEmployeeId)
            throw new UnauthorizedAccessException(
                "Only the traveller can acknowledge their own travel risk assessment.");

        entity.EmployeeAcknowledged = true;
        entity.AcknowledgedAt = DateTime.UtcNow;   // the clock, not a payload value
        entity.UpdatedAt = DateTime.UtcNow;

        await _riskAssessmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteRiskAssessmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRiskAssessmentAsync(id);
        await _riskAssessmentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Alerts ------------------------------------------------------------

    public async Task<StaffTravelAlertDto> GetAlertByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _alertRepository.GetWithNotificationsAsync(id);
        if (entity == null || entity.TenantId != tenantId)
            throw new ArgumentException($"Travel alert with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelAlertSummaryDto>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _alertRepository.GetActiveAlertsAsync())
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.ToSummaryDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelAlertSummaryDto>> GetAlertsByCountryAsync(Guid countryId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _alertRepository.GetByCountryAsync(countryId))
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.ToSummaryDto())
            .ToList();
    }

    /// <summary>The alerts in force for a destination right now.</summary>
    /// <remarks>
    /// ⚠ <b>This returned <c>StaffTravelAlertSummaryDto</c>, which has no <c>Body</c></b> — and the
    /// body IS the alert. The travel request's compliance panel renders the title, the severity and
    /// then <c>{a.body &amp;&amp; …}</c>, so since it shipped a traveller has been shown
    /// "Civil unrest · High" and never a word about what or where. TypeScript did not catch it
    /// because the client typed this read as the full DTO.
    ///
    /// <para>The D-09 rule still holds and this is not an exception to it: a cross-record read
    /// feeds a list, and <c>alerts/active</c> and <c>alerts/country/{}</c> remain summaries for
    /// exactly that reason. This read is not a list — its whole purpose is to carry an alert's
    /// content to the person travelling there.</para>
    /// </remarks>
    public async Task<IEnumerable<StaffTravelAlertDto>> GetCurrentAlertsForCountryAsync(Guid countryId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _alertRepository.GetCurrentAlertsForCountryAsync(countryId))
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.ToDto())
            .ToList();
    }

    public async Task<StaffTravelAlertDto> CreateAlertAsync(CreateStaffTravelAlertDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _alertRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _alertRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<StaffTravelAlertDto> UpdateAlertAsync(UpdateStaffTravelAlertDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAlertAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _alertRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _alertRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<bool> DeleteAlertAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedAlertAsync(id);
        await _alertRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Alert notifications -----------------------------------------------

    /// <summary>
    /// Confirms the travel request exists in the caller's tenant, and returns it — the compliance
    /// creates all took StaffTravelRequestId from the payload without checking it.
    /// </summary>
    private async Task<StaffTravelRequest> RequireOwnedRequestAsync(Guid requestId)
    {
        var request = await _requestRepository.GetByIdAsync(requestId);
        if (request == null || request.TenantId != GetTenantId())
            throw new ArgumentException($"Staff travel request with ID '{requestId}' not found.");
        return request;
    }

    /// <summary>
    /// Confirms the employee exists in the caller's tenant, and returns it.
    /// </summary>
    private async Task<Employee> RequireOwnedEmployeeAsync(Guid employeeId)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null || employee.TenantId != GetTenantId())
            throw new ArgumentException($"Employee with ID '{employeeId}' not found.");
        return employee;
    }

    /// <summary>
    /// Publishes the alert to the travel desk and the traveller, then records that it was sent.
    /// </summary>
    /// <remarks>
    /// The stamp is written only after <c>PublishAsync</c> returns. If publishing throws, the row
    /// keeps a null <c>NotificationSentAt</c> and reads as undelivered — which is the truth, and is
    /// the whole point of the change. An unsent alert that admits it is unsent can be retried; one
    /// that claims delivery cannot.
    /// </remarks>
    private async Task SendAlertAsync(
        StaffTravelAlertNotification notification,
        StaffTravelAlert alert,
        StaffTravelRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureAlertTopicAsync(notification.TenantId, cancellationToken);

        var employee = await _employeeRepository.GetByIdAsync(notification.EmployeeId);
        var country = await _alertRepository.GetQueryable()
            .Where(a => a.Id == alert.Id)
            .Select(a => a.Country.Name)
            .FirstOrDefaultAsync(cancellationToken);

        await _appEventBus.PublishAsync(new EntityActivityEvent
        {
            TenantId = notification.TenantId,
            EntityType = AlertTopicEntityType,
            Activity = "Issued",
            Audience = AlertTopicAudience,
            EntityId = alert.Id,
            TriggeredByUserId = _currentUserProvider.UserId,
            Data = new Dictionary<string, object>
            {
                ["AlertTitle"] = alert.Title ?? string.Empty,
                ["Severity"] = alert.Severity.ToString(),
                ["Country"] = country ?? string.Empty,
                ["Reference"] = request.RequestNumber ?? string.Empty,
                ["Route"] = $"{request.OriginCity} to {request.DestinationCity}",
                ["Dates"] = $"{request.TravelStartDate:yyyy-MM-dd} to {request.TravelEndDate:yyyy-MM-dd}",
                ["TravellerEmail"] = employee?.EmailAddress ?? string.Empty,
                // Area 25 slice 7: /hr/travel/requests/{id} never existed — the desk detail is /hr/travel/{id}.
                ["ActionPath"] = $"/hr/travel/{request.Id}",
            },
        }, cancellationToken);

        notification.NotificationSentAt = DateTime.UtcNow;
        await _notificationRepository.UpdateAsync(notification);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ---- Travel alerts that actually reach somebody ---------------------------

    private const string AlertTopicEntityType = "StaffTravelAlert";
    private const string AlertTopicAudience = "Internal";
    private const string AlertTopicKey = "StaffTravelAlert.Issued.Internal";

    /// <summary>
    /// Creates the travel-alert notification topic for a tenant if it does not exist.
    /// </summary>
    /// <remarks>
    /// Two recipients, deliberately. <b>Role HR</b> is the travel desk, who may have to act — move
    /// a booking, cancel a leg. <b>EmailFromData</b> reaches the traveller directly, because an
    /// alert about the country you are flying to next week is useless if it only ever lands in
    /// somebody's queue. The platform has no "employee" recipient kind, so the traveller's address
    /// travels in the event data and the rule points at that key.
    /// </remarks>
    private async Task EnsureAlertTopicAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var topicRepo = _unitOfWork.Repository<NotificationTopic>();
        var existing = await topicRepo
            .GetQueryable(t => t.TenantId == tenantId && !t.IsDeleted && t.Key == AlertTopicKey)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing != null) return;

        var topic = new NotificationTopic
        {
            TenantId = tenantId,
            Key = AlertTopicKey,
            Name = "Travel: Destination alert",
            Description = "System-seeded — a security, health or disruption alert affects a trip already booked.",
            EntityType = AlertTopicEntityType,
            IsSystem = true,
            IsActive = true,
            EnableInApp = true,
            EnableEmail = true,
            EnableSms = false,
            InAppTitleTemplate = "{{Severity}} travel alert: {{Country}}",
            InAppBodyTemplate = "{{AlertTitle}} — affects {{Reference}} ({{Route}}, {{Dates}}).",
            ActionUrlTemplate = "{{ActionPath}}",
            CreatedBy = "System",
        };
        await topicRepo.AddAsync(topic);

        var recipientRepo = _unitOfWork.Repository<NotificationTopicRecipient>();
        await recipientRepo.AddAsync(new NotificationTopicRecipient
        {
            TenantId = tenantId, TopicId = topic.Id,
            RecipientKind = "Role", RecipientValue = Constants.Roles.Hr,
            IsSystem = true, SendInApp = true, CreatedBy = "System",
        });
        await recipientRepo.AddAsync(new NotificationTopicRecipient
        {
            TenantId = tenantId, TopicId = topic.Id,
            RecipientKind = "EmailFromData", RecipientValue = "TravellerEmail",
            IsSystem = true, SendEmail = true, CreatedBy = "System",
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<StaffTravelAlertNotificationDto> CreateAlertNotificationAsync(CreateStaffTravelAlertNotificationDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);

        // Both parents are real records, and neither was checked.
        var request = await RequireOwnedRequestAsync(createDto.StaffTravelRequestId);
        var alert = await _alertRepository.GetByIdAsync(createDto.TravelAlertId);
        if (alert == null || alert.TenantId != tenantId)
            throw new ArgumentException($"Travel alert with ID '{createDto.TravelAlertId}' not found.");

        // ⚠ There was a THIRD parent, and the comment above did not count it. EmployeeId came from
        // the body, was validated by nothing, and went straight to the foreign key — so an unknown
        // employee died as the generic 500 naming neither the field nor the constraint, and a valid
        // one belonging to a different trip produced a coherent-looking row telling somebody they
        // were travelling where they were not. The request already names its traveller; take it from
        // there and there is nothing left to forge.
        var entity = createDto.ToEntity(tenantId, createdByUserId, request.EmployeeId);

        // NotificationSentAt used to be stamped from the payload — the CALLER asserted delivery and
        // nothing ever sent anything, so a destination-security alert reached nobody while the table
        // said it had been delivered. Stamp it only after the event is actually published.
        entity.NotificationSentAt = null;

        await _notificationRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await SendAlertAsync(entity, alert, request, cancellationToken);

        // GetByIdAsync loads no navigations, so this re-read was returning the same blanks it was
        // added to avoid: alertTitle, requestNumber and employeeName all empty on the create.
        var reloaded = await _notificationRepository.GetByIdWithDetailsAsync(entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<IEnumerable<StaffTravelAlertNotificationDto>> GetNotificationsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _notificationRepository.GetByEmployeeIdAsync(employeeId))
            .Where(n => n.TenantId == tenantId)
            .Select(n => n.ToDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelAlertNotificationDto>> GetUnacknowledgedNotificationsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _notificationRepository.GetUnacknowledgedAsync(employeeId))
            .Where(n => n.TenantId == tenantId)
            .Select(n => n.ToDto())
            .ToList();
    }

    /// <summary>
    /// Records that the employee an alert was sent to has seen it.
    /// </summary>
    /// <remarks>
    /// ⚠ Only that employee may acknowledge — the notification carries its own <c>EmployeeId</c>, so
    /// unlike the risk assessment there is no lookup needed to know whose it is. Same reasoning:
    /// an acknowledgement anyone can record on your behalf records nothing.
    /// </remarks>
    public async Task<bool> AcknowledgeNotificationAsync(AcknowledgeStaffTravelAlertNotificationDto acknowledgeDto, Guid acknowledgerEmployeeId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedNotificationAsync(acknowledgeDto.NotificationId);

        if (entity.EmployeeId != acknowledgerEmployeeId)
            throw new UnauthorizedAccessException(
                "Only the employee an alert was sent to can acknowledge it.");

        entity.IsAcknowledged = true;
        entity.AcknowledgedAt = DateTime.UtcNow;   // the clock, not a payload value
        entity.UpdatedAt = DateTime.UtcNow;

        await _notificationRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Insurance policies ------------------------------------------------

    public async Task<StaffTravelInsurancePolicyDto> GetInsuranceByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _insuranceRepository.GetWithDetailsAsync(GetTenantId(), id)
            ?? throw new ArgumentException($"Insurance with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelInsurancePolicyDto>> GetInsuranceByRequestAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _insuranceRepository.GetByRequestIdAsync(requestId))
            .Where(i => i.TenantId == tenantId)
            .Select(i => i.ToDto())
            .ToList();
    }

    public async Task<StaffTravelInsurancePolicyDto> CreateInsuranceAsync(CreateStaffTravelInsurancePolicyDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await _currency.RequireKnownCurrencyAsync(createDto.CurrencyCode, cancellationToken);
        await RequireOwnedRequestAsync(createDto.StaffTravelRequestId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _insuranceRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _insuranceRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<StaffTravelInsurancePolicyDto> UpdateInsuranceAsync(UpdateStaffTravelInsurancePolicyDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInsuranceAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _insuranceRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _insuranceRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<bool> DeleteInsuranceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedInsuranceAsync(id);
        await _insuranceRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Health requirements -----------------------------------------------

    public async Task<StaffTravelHealthRequirementDto> GetHealthRequirementByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _healthRequirementRepository.GetWithDetailsAsync(GetTenantId(), id)
            ?? throw new ArgumentException($"HealthRequirement with ID '{id}' not found.");
        return entity.ToDto();
    }

    public async Task<IEnumerable<StaffTravelHealthRequirementDto>> GetHealthRequirementsByCountryAsync(Guid countryId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _healthRequirementRepository.GetByCountryAsync(countryId))
            .Where(h => h.TenantId == tenantId)
            .Select(h => h.ToDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelHealthRequirementDto>> GetMandatoryHealthRequirementsAsync(Guid countryId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _healthRequirementRepository.GetMandatoryByCountryAsync(countryId))
            .Where(h => h.TenantId == tenantId)
            .Select(h => h.ToDto())
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelHealthRequirementDto>> GetActiveHealthRequirementsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _healthRequirementRepository.GetActiveAsync())
            .Where(h => h.TenantId == tenantId)
            .Select(h => h.ToDto())
            .ToList();
    }

    public async Task<StaffTravelHealthRequirementDto> CreateHealthRequirementAsync(CreateStaffTravelHealthRequirementDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _healthRequirementRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _healthRequirementRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<StaffTravelHealthRequirementDto> UpdateHealthRequirementAsync(UpdateStaffTravelHealthRequirementDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHealthRequirementAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        await _healthRequirementRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _healthRequirementRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<bool> DeleteHealthRequirementAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedHealthRequirementAsync(id);
        await _healthRequirementRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}

#endregion
