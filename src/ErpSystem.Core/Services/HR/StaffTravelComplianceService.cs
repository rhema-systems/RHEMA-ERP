using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Shared;
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
    // Lane 8 (E6, D-4): an alert and a risk briefing reach the traveller in the app and by email.
    private readonly StaffTravelNotices _notices;
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
        StaffTravelNotices notices,
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
        _notices = notices;
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

    /// <summary>Lane 7 (O-7): a list read shows a document's number to its last four only.</summary>
    private static StaffTravelDocumentDto Masked(StaffTravelDocument entity)
    {
        var dto = entity.ToDto();
        dto.DocumentNumber = StaffTravelMappingExtensions.MaskAllButLastFour(dto.DocumentNumber) ?? string.Empty;
        dto.NumberMasked = true;
        return dto;
    }

    public async Task<IEnumerable<StaffTravelDocumentDto>> GetAllDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _documentRepository.GetAllWithDetailsAsync())
            .Where(d => d.TenantId == tenantId)
            .Select(Masked)
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelDocumentDto>> GetDocumentsByEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _documentRepository.GetByEmployeeIdAsync(employeeId))
            .Where(d => d.TenantId == tenantId)
            .Select(Masked)
            .ToList();
    }

    public async Task<IEnumerable<StaffTravelDocumentDto>> GetExpiringDocumentsAsync(int daysAhead = 90, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        return (await _documentRepository.GetExpiringDocumentsAsync(daysAhead))
            .Where(d => d.TenantId == tenantId)
            .Select(Masked)
            .ToList();
    }

    /// <summary>
    /// Lane 7 (E2): one primary document per type per employee — the one the visa lookup and the passport checks read.
    /// A new primary stands the old one down; the caller saves.
    /// </summary>
    private async Task StandDownOtherPrimariesAsync(StaffTravelDocument primary, CancellationToken cancellationToken)
    {
        if (!primary.IsPrimary) return;
        var others = await _unitOfWork.Repository<StaffTravelDocument>()
            .GetQueryable(d => d.TenantId == primary.TenantId && d.EmployeeId == primary.EmployeeId && !d.IsDeleted
                            && d.DocumentType == primary.DocumentType && d.IsPrimary && d.Id != primary.Id)
            .ToListAsync(cancellationToken);
        foreach (var other in others)
        {
            other.IsPrimary = false;
            other.UpdatedAt = DateTime.UtcNow;
            other.UpdatedBy = _currentUserProvider.UserId.ToString();
        }
    }

    public async Task<StaffTravelDocumentDto> CreateDocumentAsync(CreateStaffTravelDocumentDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        tenantId = RequireCurrentTenant(tenantId);
        await RequireOwnedEmployeeAsync(createDto.EmployeeId);
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        RequireDocumentDates(entity);
        await StandDownOtherPrimariesAsync(entity, cancellationToken);
        await _documentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _documentRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    /// <summary>
    /// Lane 7 (E2): an edit un-verifies the document — the verification was of what it said before; another officer
    /// verifies it again. Saved by change tracking.
    /// </summary>
    public async Task<StaffTravelDocumentDto> UpdateDocumentAsync(UpdateStaffTravelDocumentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedDocumentAsync(updateDto.Id);
        entity.UpdateEntity(updateDto, updatedByUserId);
        RequireDocumentDates(entity);
        entity.IsVerified = false;
        entity.VerifiedById = null;
        entity.VerifiedAt = null;
        await StandDownOtherPrimariesAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var reloaded = await _documentRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    private static void RequireDocumentDates(StaffTravelDocument document)
    {
        if (document.IssueDate is DateOnly issued && document.ExpiryDate is DateOnly expires && expires <= issued)
            throw new InvalidOperationException("A document expires after it is issued.");
        if (string.IsNullOrWhiteSpace(document.DocumentNumber))
            throw new InvalidOperationException("Give the document's number.");
        document.DocumentNumber = document.DocumentNumber.Trim();
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
        // Lane 7 (O-15): a verified document is the record that someone checked it — corrected by an edit (which
        // un-verifies it) or replaced by a new one, not deleted.
        if (entity.IsVerified)
            throw new InvalidOperationException(
                "A verified document is not deleted — edit it (which takes the verification off) or record its replacement.");
        await _documentRepository.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- Visa requirements -------------------------------------------------

    public async Task<StaffTravelVisaRequirementDto?> GetVisaRequirementAsync(Guid passportCountryId, Guid destinationCountryId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var entity = await _visaRequirementRepository.GetRequirementAsync(tenantId, passportCountryId, destinationCountryId);
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
        var saved = await _visaRequirementRepository.GetRequirementAsync(entity.TenantId,
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
        var existing = await _visaRequirementRepository.GetRequirementAsync(GetTenantId(), passportCountryId, destinationCountryId);
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

    /// <remarks>Lane 7 (E3): the edit had no caller; now that it does, it takes the create's checks — a present currency
    /// is one Finance holds, the status one the enum has.</remarks>
    public async Task<StaffTravelVisaApplicationDto> UpdateVisaApplicationAsync(UpdateStaffTravelVisaApplicationDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedVisaApplicationAsync(updateDto.Id);
        await _currency.RequireKnownCurrencyAsync(updateDto.CurrencyCode, cancellationToken, optional: true);
        if (!Enum.IsDefined(updateDto.Status))
            throw new InvalidOperationException($"{(int)updateDto.Status} is not a visa application status.");
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
        var request = await RequireOwnedRequestAsync(createDto.StaffTravelRequestId);
        createDto.AssessedById = assessorEmployeeId;
        var entity = createDto.ToEntity(tenantId, createdByUserId);
        await _riskAssessmentRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await AskForAcknowledgementAsync(request, entity, assessorEmployeeId, cancellationToken);
        var reloaded = await _riskAssessmentRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    /// <remarks>Lane 7 (E5): the assessor stays who made it (the mapper no longer takes it from the payload), and an
    /// acknowledgement is of the risk as it stood — a higher level asks the traveller to acknowledge again.</remarks>
    public async Task<StaffTravelRiskAssessmentDto> UpdateRiskAssessmentAsync(UpdateStaffTravelRiskAssessmentDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = await GetOwnedRiskAssessmentAsync(updateDto.Id);
        var before = entity.RiskLevel;
        entity.UpdateEntity(updateDto, updatedByUserId);
        if (entity.RiskLevel > before && entity.EmployeeAcknowledged)
        {
            entity.EmployeeAcknowledged = false;
            entity.AcknowledgedAt = null;
        }
        await _riskAssessmentRepository.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (entity.RiskLevel > before)
            await AskForAcknowledgementAsync(
                await RequireOwnedRequestAsync(entity.StaffTravelRequestId), entity, actorEmployeeId: null, cancellationToken);
        var reloaded = await _riskAssessmentRepository.GetWithDetailsAsync(entity.TenantId, entity.Id);
        return (reloaded ?? entity).ToDto();
    }

    /// <summary>
    /// Lane 8 (D-4): the traveller is asked to read and acknowledge a risk assessment — a new one, or one whose level rose
    /// (which cleared any acknowledgement, E5) — while the trip is still to happen or under way. D-37 holds a Critical
    /// trip's ticket until they do; 8b's sweep chases one still unacknowledged near departure.
    /// </summary>
    private Task AskForAcknowledgementAsync(
        StaffTravelRequest request, StaffTravelRiskAssessment assessment, Guid? actorEmployeeId, CancellationToken cancellationToken)
    {
        if (request.Status is StaffTravelRequestStatus.Cancelled or StaffTravelRequestStatus.Rejected
            or StaffTravelRequestStatus.Completed or StaffTravelRequestStatus.Closed)
            return Task.CompletedTask;
        return _notices.TellTravellerAsync(request, StaffTravelNotices.BriefingToAcknowledge,
            StaffTravelNotices.TravellerTrip(request.Id, "before"), _currentUserProvider.UserId, actorEmployeeId,
            new Dictionary<string, object> { ["RiskLevel"] = assessment.RiskLevel.ToString() }, cancellationToken);
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

        // Lane 7 (7c1): a second click keeps the first acknowledgement's time — that is when they read it.
        if (entity.EmployeeAcknowledged) return true;

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
        // Lane 7 (O-15): an acknowledged assessment is the duty-of-care record that the traveller was told.
        if (entity.EmployeeAcknowledged)
            throw new InvalidOperationException(
                "The traveller has acknowledged this risk assessment, so it is kept — record a new assessment instead.");
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

        // Lane 7 (E6, T-44): an alert reaches the people already going — the traveller of every approved or under-way
        // trip to the country (to the alert's city, when it names one) whose dates meet the alert's window — through the
        // same path as the desk's Send, so each is recorded and reaches them in the app and by email.
        if (entity.IsActive)
        {
            var from = DateOnly.FromDateTime(entity.EffectiveFrom);
            var to = entity.EffectiveTo is DateTime until ? DateOnly.FromDateTime(until) : DateOnly.MaxValue;
            var trips = await _unitOfWork.Repository<StaffTravelRequest>()
                .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted && r.DestinationCountryId == entity.CountryId
                                && (r.Status == StaffTravelRequestStatus.Approved || r.Status == StaffTravelRequestStatus.InProgress)
                                && r.TravelStartDate <= to && r.TravelEndDate >= from)
                .ToListAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(entity.City))
                trips = trips.Where(r => string.Equals(r.DestinationCity?.Trim(), entity.City.Trim(), StringComparison.OrdinalIgnoreCase))
                             .ToList();
            foreach (var trip in trips)
            {
                var notification = new StaffTravelAlertNotification
                {
                    TenantId = tenantId,
                    TravelAlertId = entity.Id,
                    StaffTravelRequestId = trip.Id,
                    EmployeeId = trip.EmployeeId,
                    NotificationSentAt = null,   // stamped by SendAlertAsync once published
                    CreatedBy = createdByUserId.ToString(),
                };
                await _notificationRepository.AddAsync(notification);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await SendAlertAsync(notification, entity, trip, cancellationToken);
            }
            _logger.LogInformation("Travel alert {AlertId} sent to {Count} trip(s) already approved or under way", entity.Id, trips.Count);
        }

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
        // Lane 7 (O-15): once sent, the alert is part of the trips it reached — deactivate it instead.
        var sent = await _unitOfWork.Repository<StaffTravelAlertNotification>()
            .GetQueryable(n => n.TenantId == entity.TenantId && n.TravelAlertId == entity.Id && !n.IsDeleted)
            .CountAsync(cancellationToken);
        if (sent > 0)
            throw new InvalidOperationException(
                $"The alert '{entity.Title}' has reached {sent} trip(s), so it is not deleted — deactivate it instead (untick Active).");
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
    /// Sends the alert to the trip's traveller and to the travel desk, then records that it was sent.
    /// </summary>
    /// <remarks>
    /// <para>The stamp is written only after the notices are published. A notice that cannot be published is logged by
    /// <see cref="StaffTravelNotices"/>, never thrown — and the platform's bus swallows its handlers' failures anyway (lane
    /// 8, U2) — so <c>NotificationSentAt</c> records that the send was made; that it was delivered is read from the
    /// notifications written.</para>
    ///
    /// <para><b>Lane 8 (E6).</b> The traveller was told by email only (an <c>EmailFromData</c> rule on the employee's
    /// address) and the desk in the app. Now the traveller is told in the app and by email, on the trip's Before you go
    /// tab — or by email alone with no login, or through the desk with neither — and the desk in the app, less whoever
    /// sent it. The old <c>StaffTravelAlert.Issued.Internal</c> topic is switched off.</para>
    /// </remarks>
    private async Task SendAlertAsync(
        StaffTravelAlertNotification notification,
        StaffTravelAlert alert,
        StaffTravelRequest request,
        CancellationToken cancellationToken)
    {
        var country = await _alertRepository.GetQueryable()
            .Where(a => a.Id == alert.Id)
            .Select(a => a.Country.Name)
            .FirstOrDefaultAsync(cancellationToken);
        var data = new Dictionary<string, object>
        {
            ["AlertTitle"] = alert.Title ?? string.Empty,
            ["Severity"] = alert.Severity.ToString(),
            ["Country"] = country ?? string.Empty,
        };

        await _notices.TellTravellerAsync(request, StaffTravelNotices.AlertIssued,
            StaffTravelNotices.TravellerTrip(request.Id, "before"), _currentUserProvider.UserId, actorEmployeeId: null,
            data, cancellationToken);
        await _notices.TellDeskAsync(request, StaffTravelNotices.AlertIssued,
            StaffTravelNotices.DeskTrip(request.Id, "compliance"), _currentUserProvider.UserId, actorEmployeeId: null,
            data, onlyWhenActorOutsideDesk: false, cancellationToken);

        notification.NotificationSentAt = DateTime.UtcNow;
        await _notificationRepository.UpdateAsync(notification);
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

    // ---- Health requirements cleared per trip (lane 7, D-36, T-25) -----------------------------------------------

    /// <summary>The trip and the destination's health requirements in force over its dates, mandatory first.</summary>
    private async Task<(StaffTravelRequest Request, List<StaffTravelHealthRequirement> Applicable)> TripHealthAsync(
        Guid requestId, CancellationToken cancellationToken)
    {
        var request = await RequireOwnedRequestAsync(requestId);
        var applicable = await _unitOfWork.Repository<StaffTravelHealthRequirement>()
            .GetQueryable(h => h.TenantId == request.TenantId && !h.IsDeleted && h.IsActive && h.CountryId == request.DestinationCountryId
                            && h.EffectiveFrom <= request.TravelEndDate && (h.EffectiveTo == null || h.EffectiveTo >= request.TravelStartDate))
            .OrderByDescending(h => h.IsMandatory)
            .ThenBy(h => h.RequirementName)
            .ToListAsync(cancellationToken);
        return (request, applicable);
    }

    public async Task<IReadOnlyList<StaffTravelTripHealthRequirementDto>> GetTripHealthRequirementsAsync(
        Guid requestId, CancellationToken cancellationToken = default)
    {
        var (request, applicable) = await TripHealthAsync(requestId, cancellationToken);
        var ids = applicable.Select(h => h.Id).ToList();
        var cleared = await _unitOfWork.Repository<StaffTravelHealthClearance>()
            .GetQueryable(c => c.TenantId == request.TenantId && c.StaffTravelRequestId == request.Id && !c.IsDeleted
                            && ids.Contains(c.HealthRequirementId))
            .Select(c => new
            {
                c.Id, c.HealthRequirementId, c.ClearedAt, c.ClearedById, c.Note,
                Name = (c.ClearedBy.FirstName + " " + c.ClearedBy.LastName).Trim(),
            })
            .ToListAsync(cancellationToken);
        return applicable.Select(h =>
        {
            var c = cleared.FirstOrDefault(x => x.HealthRequirementId == h.Id);
            return new StaffTravelTripHealthRequirementDto
            {
                HealthRequirementId = h.Id, RequirementName = h.RequirementName, RequirementType = h.RequirementType,
                IsMandatory = h.IsMandatory, ValidityDays = h.ValidityDays, Notes = h.Notes,
                Cleared = c is not null, ClearanceId = c?.Id, ClearedAt = c?.ClearedAt, ClearedById = c?.ClearedById,
                ClearedByName = c?.Name, ClearanceNote = c?.Note,
            };
        }).ToList();
    }

    /// <summary>Ticks a requirement as checked for the trip's traveller — by the caller, now, with what was seen.</summary>
    public async Task<StaffTravelTripHealthRequirementDto> ClearHealthRequirementAsync(
        Guid requestId, Guid healthRequirementId, string? note, Guid clearedByEmployeeId, CancellationToken cancellationToken = default)
    {
        var (request, applicable) = await TripHealthAsync(requestId, cancellationToken);
        if (request.Status is StaffTravelRequestStatus.Cancelled or StaffTravelRequestStatus.Rejected or StaffTravelRequestStatus.Closed)
            throw new InvalidOperationException(
                $"Travel request {request.RequestNumber} is {request.Status}; its health requirements are not cleared now.");
        var requirement = applicable.FirstOrDefault(h => h.Id == healthRequirementId)
                          ?? throw new ArgumentException(
                              $"Health requirement '{healthRequirementId}' does not apply to travel request {request.RequestNumber}.");
        var existing = await _unitOfWork.Repository<StaffTravelHealthClearance>()
            .GetQueryable(c => c.TenantId == request.TenantId && c.StaffTravelRequestId == request.Id && !c.IsDeleted
                            && c.HealthRequirementId == requirement.Id)
            .Select(c => new { c.ClearedAt, Name = (c.ClearedBy.FirstName + " " + c.ClearedBy.LastName).Trim() })
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException(
                $"{requirement.RequirementName} was cleared for this trip by {existing.Name} on {existing.ClearedAt:dd MMM yyyy}.");

        var text = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        await _unitOfWork.Repository<StaffTravelHealthClearance>().AddAsync(new StaffTravelHealthClearance
        {
            TenantId = request.TenantId,
            StaffTravelRequestId = request.Id,
            HealthRequirementId = requirement.Id,
            ClearedById = clearedByEmployeeId,   // the caller, not a payload value
            ClearedAt = DateTime.UtcNow,         // ...and the clock
            Note = text is { Length: > 1000 } ? text[..1000] : text,
            CreatedBy = _currentUserProvider.UserId.ToString(),
        });
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (await GetTripHealthRequirementsAsync(requestId, cancellationToken)).First(r => r.HealthRequirementId == requirement.Id);
    }

    /// <summary>Takes a tick off (a soft delete) — the requirement can be cleared again.</summary>
    public async Task<bool> UnclearHealthRequirementAsync(Guid requestId, Guid healthRequirementId, CancellationToken cancellationToken = default)
    {
        var request = await RequireOwnedRequestAsync(requestId);
        if (request.Status == StaffTravelRequestStatus.Closed)
            throw new InvalidOperationException($"Travel request {request.RequestNumber} is closed; its record stands.");
        var live = await _unitOfWork.Repository<StaffTravelHealthClearance>()
            .GetQueryable(c => c.TenantId == request.TenantId && c.StaffTravelRequestId == request.Id && !c.IsDeleted
                            && c.HealthRequirementId == healthRequirementId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ArgumentException($"Health requirement '{healthRequirementId}' is not cleared on travel request {request.RequestNumber}.");
        live.IsDeleted = true;
        live.DeletedAt = DateTime.UtcNow;
        live.DeletedBy = _currentUserProvider.UserId.ToString();
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
