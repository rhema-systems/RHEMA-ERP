using ErpSystem.Core.Services.HR.Extensions;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
// ProbationPeriod lives under Recruitment — the hire path opened it, so that is where it was filed.
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Application service for the Employee aggregate. Orchestrates repositories, enforces rules, and manages transactions.
/// </summary>
public class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IOrganizationUnitRepository _organizationUnitRepository;
    private readonly IEmployeePositionRepository _positionRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPositionNamedSetService _namedSets;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<EmployeeService> _logger;
    private readonly IDisabilityTypeService _disabilityTypes; // round 3, lane P2

    public EmployeeService(
        IEmployeeRepository employeeRepository,
        IOrganizationUnitRepository organizationUnitRepository,
        IEmployeePositionRepository positionRepository,
        ILocationRepository locationRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        HrCurrencyBridge currencies,
        IStaffNumberService staffNumbers,
        IPayrollMembershipService payrollMembership,
        IGeographyService geography,
        INumberSequenceService numberSequence,
        ICompanyHrPolicySettingsService policySettings,
        IPositionNamedSetService namedSets,
        ILogger<EmployeeService> logger,
        IDisabilityTypeService disabilityTypes)
    {
        _currencies = currencies;
        _staffNumbers = staffNumbers;
        _payrollMembership = payrollMembership;
        _geography = geography;
        _numberSequence = numberSequence;
        _policySettings = policySettings;
        _namedSets = namedSets;
        _employeeRepository = employeeRepository;
        _organizationUnitRepository = organizationUnitRepository;
        _positionRepository = positionRepository;
        _locationRepository = locationRepository;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
        _disabilityTypes = disabilityTypes;
    }

    // The ApplicationDbContext is registered without a tenant, so its global tenant
    // query-filter and TenantId auto-stamp are inert. Following the RHEMA convention,
    // this service scopes reads/writes to the current tenant explicitly.
    // Finance owns what a currency IS; HR says which one it uses. A bare three-letter code nothing
    // validates is how travel became able to file a claim in "XYZ" and total it.
    private readonly HrCurrencyBridge _currencies;

    // Which staff number a new employee gets is the REGISTER's decision, not this service's, and
    // not a compiled-in format. See StaffNumberService.
    private readonly IStaffNumberService _staffNumbers;

    // Whether the person is paid through the payroll run is HR's statement; whether payroll runs
    // them is payroll's. This is the bridge, and the only place HR reaches into payroll.
    private readonly IPayrollMembershipService _payrollMembership;

    // Shared reference data, not HR's: the administrative-geography tree that Estate, Sales and
    // Procurement will read too. HR only asks it to resolve an area into the region/town names the
    // snapshot columns carry, so the two can never disagree.
    private readonly IGeographyService _geography;

    // Contract numbers. The hire-from-offer path had compiled in `CTR-{employeeNumber}`, which is a
    // number an employee can only ever have ONE of — fine while nothing but a hire wrote a contract,
    // wrong the moment a second set of terms supersedes the first.
    private readonly INumberSequenceService _numberSequence;

    // Where the probation term falls back to when the post is silent about it.
    private readonly ICompanyHrPolicySettingsService _policySettings;

    /// <summary>The sequence key contract numbers count on: <c>CTR-2026-001</c>.</summary>
    private const string ContractSequenceKey = "CTR";

    /// <summary>
    /// Rewrites <c>State</c> and <c>City</c> from the employee's area, so the free-text snapshot
    /// always agrees with the structured link.
    /// </summary>
    /// <remarks>
    /// <para>⚠ The tree wins. If a caller sends both a <c>GeoAreaId</c> and its own spelling of
    /// State, the tree's spelling replaces it — otherwise the two drift and nobody can tell which
    /// is right, which is the state the whole geography module exists to end.</para>
    ///
    /// <para>A <c>null</c> area leaves both columns exactly as they were. Most of the register
    /// predates the tree and has only the free text; blanking it here would destroy the only
    /// address those rows have.</para>
    /// </remarks>
    /// <remarks>
    /// ⚠ The employee's region column is called <c>State</c>, which is the whole reason the shared
    /// helper takes setters rather than reading property names. The rule itself has lived in
    /// <see cref="GeoAddressSnapshot"/> since round 2 lane D2, where four more tables gained the
    /// same column and the four private copies of this method had already drifted apart.
    /// </remarks>
    private async Task ApplyGeoAreaSnapshotAsync(Employee employee, CancellationToken cancellationToken)
    {
        employee.CountryId = await GeoAddressSnapshot.ReconcileCountryAsync(
            _geography, employee.GeoAreaId, employee.CountryId, "employee", cancellationToken);

        await GeoAddressSnapshot.ApplyAsync(
            _geography, _logger, employee.GeoAreaId,
            r => employee.State = r, c => employee.City = c,
            "employee", employee.Id, cancellationToken);
    }

    /// <summary>
    /// The same two rules for a sub-record of an employee — an address, a next of kin, a guarantor,
    /// a previous employer.
    /// </summary>
    /// <remarks>
    /// ⚠ Called AFTER the DTO has been applied to the entity, never before: the tree wins over
    /// whatever spelling the caller sent, and running it first would let the payload overwrite the
    /// snapshot it is supposed to lose to.
    /// </remarks>
    private async Task ApplySubRecordGeoAsync(
        Guid? geoAreaId,
        Func<Guid?> readCountry,
        Action<Guid?> writeCountry,
        Action<string> setRegion,
        Action<string> setCity,
        string subject,
        Guid recordId,
        CancellationToken cancellationToken)
    {
        writeCountry(await GeoAddressSnapshot.ReconcileCountryAsync(
            _geography, geoAreaId, readCountry(), subject, cancellationToken));

        await GeoAddressSnapshot.ApplyAsync(
            _geography, _logger, geoAreaId, setRegion, setCity, subject, recordId, cancellationToken);
    }

    /// <summary>
    /// Resolves a relationship-catalogue id for a screen, and mirrors its name into the record's
    /// free-text column.
    /// </summary>
    /// <remarks>
    /// <para>Returns the words to store. A null id leaves <paramref name="typedRelationship"/>
    /// standing — a tie nobody has catalogued may still be typed, and every row written before the
    /// catalogue keeps its wording.</para>
    ///
    /// <para><b>⚠ The category set is the point.</b> A next-of-kin screen that offered "Former
    /// manager" and a referee screen that offered "Nephew" would be the same defect as the free
    /// text this catalogue replaces. Each caller states what it accepts; a value outside that set
    /// is refused with a sentence naming both, not silently dropped.</para>
    /// </remarks>
    private async Task<string> ResolveRelationshipAsync(
        Guid? relationshipTypeId,
        string typedRelationship,
        RelationshipCategory[] allowed,
        string screen,
        CancellationToken cancellationToken)
    {
        if (relationshipTypeId is not Guid id) return typedRelationship;

        var tenantId = GetTenantId();
        var type = await _unitOfWork.Repository<RelationshipType>().GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && !t.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Relationship type '{id}' was not found.");

        if (!type.IsActive)
            throw new InvalidOperationException(
                $"Relationship type '{type.Name}' is retired and cannot be chosen for a new record.");

        if (!allowed.Contains(type.Category))
            throw new InvalidOperationException(
                $"'{type.Name}' is a {type.Category.ToString().ToLowerInvariant()} relationship, and "
                + $"{screen} accepts {DescribeCategories(allowed)}.");

        return type.Name;
    }

    private static string DescribeCategories(RelationshipCategory[] allowed)
    {
        var words = allowed.Select(c => c.ToString().ToLowerInvariant()).ToArray();
        return words.Length switch
        {
            1 => $"only {words[0]} ones",
            _ => string.Join(" and ", new[] { string.Join(", ", words[..^1]), words[^1] }) + " ones",
        };
    }

    // What each screen offers, per plan § 6.9. Stated here rather than at the call sites so the
    // four doors cannot drift apart the way the four free-text columns did.
    private static readonly RelationshipCategory[] FamilialOrOther =
        { RelationshipCategory.Familial, RelationshipCategory.Other };

    private static readonly RelationshipCategory[] ProfessionalOrOther =
        { RelationshipCategory.Professional, RelationshipCategory.Other };

    private static readonly RelationshipCategory[] AnyRelationship =
        { RelationshipCategory.Familial, RelationshipCategory.Professional, RelationshipCategory.Other };

    /// <summary>
    /// A referee's acceptable categories, which depend on what KIND of referee they are: a personal
    /// referee may be a relative or a family friend, a professional or academic one may not.
    /// </summary>
    private static RelationshipCategory[] RefereeCategories(RefereeType type)
        => type == RefereeType.Personal ? FamilialOrOther : ProfessionalOrOther;

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("No tenant is associated with the current user.");
        return tenantId;
    }

    // ── Probation, and the contract that carries it ───────────────────────────

    /// <summary>
    /// Settles an employee's probation term and says where it came from.
    /// </summary>
    /// <remarks>
    /// <para>The rule the demo feedback asked for in E-2b, and the one
    /// <c>ProbationService.BuildPolicy</c> was already applying to READ a policy: <b>the position is
    /// the source</b>, the company policy default is the fallback. What did not exist was anything
    /// WRITING it — the create form had a free "Probation (days)" box defaulted to 90, and 90 is not
    /// TDC's number for anybody (junior posts run three months, senior and management six, and 123
    /// of 146 positions carry the term). So every create overwrote a maintained figure with a form
    /// default.</para>
    ///
    /// <para>⚠ A supplied value that contradicts the post is REFUSED, not quietly dropped. Silently
    /// ignoring input is how a field comes to look editable while doing nothing — the very failure
    /// this lane is fixing on the confirmation date. The refusal names the post and both numbers, so
    /// the caller can either change the post or stop sending the field.</para>
    ///
    /// <para>Days, not months, because <c>Employee.ProbationPeriodDays</c> is the stored unit and
    /// the hire path's months × 30 is the established conversion.</para>
    /// </remarks>
    private async Task<(int Days, ProbationSource Source)> ResolveProbationAsync(
        EmployeePosition? position, int? suppliedDays, CancellationToken cancellationToken)
    {
        var positionMonths = position?.ProbationPeriodMonths;

        if (positionMonths is > 0)
        {
            var fromPosition = positionMonths.Value * 30;
            if (suppliedDays.HasValue && suppliedDays.Value != fromPosition)
                throw new InvalidOperationException(
                    $"The position '{position!.Title}' sets probation at {positionMonths.Value} month(s), "
                    + $"which is {fromPosition} days, so {suppliedDays.Value} days cannot be recorded against it. "
                    + "Change the probation period on the position, or leave the field to the post.");

            return (fromPosition, ProbationSource.Position);
        }

        var settings = await _policySettings.GetAsync(cancellationToken);
        var fromPolicy = Math.Max(settings.DefaultProbationMonths, 0) * 30;

        // The position is silent, so a supplied length is this person's own — the only case in which
        // an override is a real answer rather than a contradiction.
        if (suppliedDays.HasValue && suppliedDays.Value != fromPolicy)
            return (suppliedDays.Value, ProbationSource.Override);

        return (fromPolicy, ProbationSource.PolicyDefault);
    }

    /// <summary>
    /// Resolves a contract kind from the tenant's vocabulary, refusing an unknown or retired one.
    /// </summary>
    private async Task<EmployeeContractType?> ResolveContractTypeAsync(
        Guid? contractTypeId, CancellationToken cancellationToken)
    {
        if (contractTypeId is not { } id || id == Guid.Empty) return null;

        var type = await _unitOfWork.Repository<EmployeeContractType>().GetByIdAsync(id);
        if (type == null || type.TenantId != GetTenantId() || type.IsDeleted)
            throw new ArgumentException("Contract type not found.");

        if (!type.IsActive)
            throw new InvalidOperationException(
                $"The contract type '{type.Name}' has been retired, so it cannot be put on a contract. "
                + "Choose a current one, or reinstate it under People Reference Data.");

        return type;
    }

    /// <summary>
    /// Files a contract row and, when it is the terms now in force, closes whatever it supersedes.
    /// </summary>
    /// <remarks>
    /// <para>⚠ This is defect X-4, in one place so every door goes through it.
    /// <c>AddContractAsync</c> inserted <c>EffectiveDate = 0001-01-01</c> (it never set the field),
    /// left <c>IsCurrent</c> at the entity default of <c>true</c>, and closed nothing — so an
    /// employee accumulated contracts that all claimed to be current, and
    /// <c>Employee.CurrentTerms</c>, which reads that flag, returned whichever one EF materialised
    /// first.</para>
    ///
    /// <para>Superseding closes the previous row completely — not current, not active, status
    /// <c>Expired</c> if it was Active, and an <c>EndDate</c> of the day before its successor takes
    /// effect, so the two never both claim the same day. A row the caller marks inactive or expired
    /// is filed as history and supersedes nothing; that is how a backfilled old contract is recorded
    /// without disturbing the terms in force.</para>
    ///
    /// <para>A new current row dated BEFORE the open one is refused rather than back-dated over it —
    /// the same rule lane B1 settled for organisation-unit history, for the same reason: the closing
    /// date is derived from the successor, so an out-of-order insert would close a row on a date
    /// before it opened.</para>
    /// </remarks>
    private async Task<EmployeeContractDetail> FileContractAsync(
        EmployeeContractDetail contract, CancellationToken cancellationToken)
    {
        var repo = _unitOfWork.Repository<EmployeeContractDetail>();

        if (contract.IsCurrent)
        {
            var superseded = await repo.GetQueryable()
                .Where(c => c.TenantId == contract.TenantId
                         && c.EmployeeId == contract.EmployeeId
                         && c.IsCurrent
                         && !c.IsDeleted)
                .OrderByDescending(c => c.EffectiveDate)
                .ToListAsync(cancellationToken);

            foreach (var open in superseded)
            {
                if (open.EffectiveDate > contract.EffectiveDate)
                    throw new InvalidOperationException(
                        $"The terms in force ({open.ContractNumber}) took effect on {open.EffectiveDate:yyyy-MM-dd}, "
                        + $"which is after {contract.EffectiveDate:yyyy-MM-dd}. Correct that contract's effective "
                        + "date first, or date this one after it.");

                open.IsCurrent = false;
                open.IsActive = false;
                if (open.ContractStatus == ContractStatus.Active)
                    open.ContractStatus = ContractStatus.Expired;

                // DateOnly.MinValue.AddDays(-1) throws, and every row written through the manual tab
                // before this lane carries exactly that effective date — so an X-4 row is closed on
                // its own date rather than taking the save down with it.
                if (open.EndDate == null)
                    open.EndDate = contract.EffectiveDate > DateOnly.MinValue
                        ? contract.EffectiveDate.AddDays(-1)
                        : contract.EffectiveDate;

                await repo.UpdateAsync(open);
            }
        }

        await repo.AddAsync(contract);
        return contract;
    }

    /// <summary>The employee's terms in force, tracked, or null.</summary>
    private async Task<EmployeeContractDetail?> GetCurrentContractEntityAsync(
        Guid employeeId, CancellationToken cancellationToken)
        => await _unitOfWork.Repository<EmployeeContractDetail>().GetQueryable()
            .Where(c => c.EmployeeId == employeeId && c.IsCurrent && !c.IsDeleted)
            .OrderByDescending(c => c.EffectiveDate)
            .FirstOrDefaultAsync(cancellationToken);

    #region 1) Core Employee Lifecycle

    public async Task<EmployeeDetailDto> CreateEmployeeAsync(CreateEmployeeDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // ⚠ There is no longer a "blank means generate" rule. Whether a number is issued or typed
        // is the REGISTER's decision, held in StaffNumberFormat — previously it was decided per
        // request by whether this field happened to be filled, which is behaviour masquerading as
        // configuration. A register with no rule is manual and REFUSES a blank number rather than
        // inventing one in a format nobody chose.
        var employeeNumber = await _staffNumbers.ResolveForCreateAsync(
            dto.EmploymentType, dto.EmployeeNumber, cancellationToken);

        return await CreateWithNumberAsync(dto, employeeNumber, isImport: false, cancellationToken);
    }

    public async Task<EmployeeDetailDto> ImportEmployeeAsync(CreateEmployeeDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var supplied = dto.EmployeeNumber?.Trim();
        if (string.IsNullOrWhiteSpace(supplied))
            throw new ArgumentException(
                "An imported employee must carry the staff number they already have. Use the normal create "
                + "for a new hire, which issues one from the register's own numbering rule.");

        // Everything else about an import is an ordinary create — the same validation, the same
        // position history. The ONE difference is where the number comes from, so this shares the
        // body rather than reimplementing it: a second copy of employee validation would drift.
        var created = await CreateWithNumberAsync(dto, supplied, isImport: true, cancellationToken);

        // ⚠ Deliberately AFTER the row is committed. The counter is a watermark over numbers that
        // are actually in the register, and advancing it for an import that then failed validation
        // would burn numbers nobody holds. The other order is only recoverable by reconciling.
        await _staffNumbers.AcceptImportedAsync(dto.EmploymentType, supplied, cancellationToken);

        _logger.LogInformation(
            "Employee imported with their existing number: {EmployeeNumber} ({EmployeeId})", supplied, created.Id);

        return created;
    }

    /// <summary>
    /// Everything a create does once the staff number has been settled, whichever way it was settled.
    /// </summary>
    /// <param name="isImport">
    /// Whether this came through the import door, i.e. an employee who already exists rather than a
    /// new hire. ⚠ It decides ONE thing: whether a <c>ConfirmationDate</c> may be supplied. Somebody
    /// whose probation ended before this system existed arrives already confirmed and has no
    /// probation record to confirm them through; a new hire has not passed a probation that has not
    /// started. See <see cref="Entities.HR.Employee.ConfirmationDate"/>.
    /// </param>
    private async Task<EmployeeDetailDto> CreateWithNumberAsync(
        CreateEmployeeDto dto, string employeeNumber, bool isImport, CancellationToken cancellationToken)
    {
        // Null when blank: email is optional, and the unique index is filtered on NOT NULL.
        var email = NormalizeEmail(dto.EmailAddress);

        if (!await IsEmployeeNumberUniqueAsync(employeeNumber, null, cancellationToken))
            throw new InvalidOperationException($"Employee number '{employeeNumber}' already exists.");

        if (email != null && !await IsEmailUniqueAsync(email, null, cancellationToken))
            throw new InvalidOperationException($"Email address '{email}' already exists.");

        if (!string.IsNullOrWhiteSpace(dto.BadgeNumber) &&
            !await IsBadgeNumberUniqueAsync(dto.BadgeNumber.Trim(), null, cancellationToken))
            throw new InvalidOperationException($"Badge number '{dto.BadgeNumber}' already exists.");

        if (!string.IsNullOrWhiteSpace(dto.TaxNumber) &&
            !await IsTaxNumberUniqueAsync(dto.TaxNumber.Trim(), null, cancellationToken))
            throw new InvalidOperationException($"Tax number '{dto.TaxNumber}' already exists.");

        if (!string.IsNullOrWhiteSpace(dto.SocialSecurityNumber) &&
            !await IsSocialSecurityNumberUniqueAsync(dto.SocialSecurityNumber.Trim(), null, cancellationToken))
            throw new InvalidOperationException("Social security number already exists.");

        if (!string.IsNullOrWhiteSpace(dto.TINNumber) &&
            !await IsTinNumberUniqueAsync(dto.TINNumber.Trim(), null, cancellationToken))
            throw new InvalidOperationException("TIN number already exists.");

        // Validate deprecated Department field only if a real value is provided (backward compatibility)
        if (dto.DepartmentId != Guid.Empty)
        {
            var departmentRepo = _unitOfWork.Repository<Department>();
            var department = await departmentRepo.GetByIdAsync(dto.DepartmentId);
            if (department == null)
                throw new ArgumentException("Department not found.");
            if (!department.IsActive)
                throw new ArgumentException("Department is not active.");
        }

        // Validate deprecated Section field only if a real value is provided (backward compatibility)
        if (dto.SectionId.HasValue && dto.SectionId.Value != Guid.Empty)
        {
            var sectionRepo = _unitOfWork.Repository<Section>();
            var section = await sectionRepo.GetByIdAsync(dto.SectionId.Value);
            if (section == null)
                throw new ArgumentException("Section not found.");
            if (!section.IsActive)
                throw new ArgumentException("Section is not active.");
            
            // Validate section belongs to department if both provided
            if (dto.DepartmentId != Guid.Empty && section.DepartmentId != dto.DepartmentId)
                throw new InvalidOperationException("Selected section does not belong to the specified department.");
        }

        if (dto.OrganizationUnitId == Guid.Empty)
            throw new ArgumentException("OrganizationUnitId is required.");

        var orgUnit = await _organizationUnitRepository.GetWithDetailsAsync(dto.OrganizationUnitId);
        if (orgUnit == null || !orgUnit.IsActive)
            throw new ArgumentException("Organization unit not found or inactive.");

        var position = await _positionRepository.GetByIdAsync(dto.PositionId);
        if (position == null)
            throw new ArgumentException("Position not found.");

        if (position.OrganizationUnitId != dto.OrganizationUnitId)
            throw new InvalidOperationException("Selected position does not belong to the specified organization unit.");

        if (!dto.LocationId.HasValue || dto.LocationId.Value == Guid.Empty)
            throw new ArgumentException("LocationId is required for employee assignment.");

        var location = await _locationRepository.GetWithDetailsAsync(dto.LocationId.Value);
        if (location == null || !location.IsActive)
            throw new ArgumentException("Location not found or inactive.");

        await ValidatePayrollFlagsAsync(dto.PayTax, dto.SSFund, dto.GrossUp, dto.Tier2Only, dto.Overtime, cancellationToken);
        ValidatePayrollMembership(
            dto.IsOnPayroll, dto.OffPayrollReason, dto.Salary,
            dto.PayTax || dto.SSFund || dto.GrossUp || dto.Tier2Only || dto.Overtime);

        if (dto.ManagerId.HasValue)
        {
            await ValidateManagerAssignmentAsync(Guid.Empty, dto.ManagerId.Value, cancellationToken);
        }

        // The probation term belongs to the POST. Resolved before the entity is built, so a row is
        // never persisted carrying a length its own position contradicts.
        var (probationDays, probationSource) =
            await ResolveProbationAsync(position, dto.ProbationPeriodDays, cancellationToken);

        // ⚠ Confirmation is an OUTCOME, not a term. The probation confirm action writes it, against
        // an authority and a letter. The one exception is somebody who arrives already confirmed,
        // which is precisely what the import door means.
        if (dto.ConfirmationDate.HasValue && !isImport)
            throw new InvalidOperationException(
                "A confirmation date cannot be set when hiring: it records that probation was passed, and "
                + "the probation record is what passes it. To record an employee who was confirmed before "
                + "this system, use the import door — tick that they already have a staff number.");

        await _disabilityTypes.EnsureUsableAsync(dto.DisabilityTypeId, dto.HasDisability, cancellationToken); // round 3, lane P2
        var employeeEntity = dto.ToEntity(employeeNumber, orgUnit.OrganizationLevelId, location.LocationLevelId);
        employeeEntity.EmailAddress = email;
        employeeEntity.TenantId = GetTenantId();
        employeeEntity.ProbationPeriodDays = probationDays;
        employeeEntity.ProbationSource = probationSource;

        // Before the insert: State and City are written from the tree so the row is never
        // persisted with a snapshot that disagrees with its own GeoAreaId, not even briefly.
        await ApplyGeoAreaSnapshotAsync(employeeEntity, cancellationToken);

        // Persist the employee and its initial position-history row atomically:
        // both commit together, or neither does. Prevents an orphaned employee
        // (saved) with no position history (failed) if the child insert throws.
        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _employeeRepository.AddAsync(employeeEntity);
            await _unitOfWork.SaveChangesAsync(ct);
            await EnsureInitialPositionHistoryExistsAsync(employeeEntity, ct);

            // ⚠ In the SAME transaction as the employee, deliberately. An employee whose terms of
            // employment do not exist is a record nothing can quote — no salary history, no notice
            // period, nothing for a probation to hang off (ProbationPeriod.ContractDetailId is NOT
            // NULL). Measured 2026-08: 4 contract rows for 2,351 employees, because the hire-from-
            // offer path was the only writer and almost nobody arrives that way.
            await OpenInitialContractAsync(employeeEntity, dto.ContractTypeId, ct);
        }, cancellationToken);

        _logger.LogInformation("Employee created: {EmployeeNumber} ({EmployeeId})", employeeEntity.EmployeeNumber, employeeEntity.Id);

        // After the commit, on purpose: the employee exists whether or not payroll can take them
        // today, and a failed enrolment is reported by the reconciliation read, not by failing
        // the hire. Create-only — see PayrollMembershipService.
        await _payrollMembership.EnsurePayrollProfileAsync(employeeEntity, cancellationToken);

        var created = await _employeeRepository.GetByIdWithDetailsAsync(employeeEntity.Id);
        if (created == null) throw new InvalidOperationException("Employee created but could not be reloaded.");
        return created.ToDetailDto();
    }

    /// <summary>
    /// Opens the employee's first contract — their terms of employment — and, for a permanent
    /// appointment, the probation period that runs against it.
    /// </summary>
    /// <remarks>
    /// <para>Mirrors what the hire-from-offer path has always done
    /// (<c>JobOfferHireService</c>), which was the only door that did it. The header supplies the
    /// terms: employment type, start date, salary, the probation term just resolved from the post.
    /// A contract kind, if one is named, supplies the scheduled end date from its duration.</para>
    ///
    /// <para>The probation row is keyed to <c>Permanent</c> because the enforcement is
    /// (<c>ProbationService.BuildPolicy</c> sets <c>IsEnforced</c> that way): a contract or
    /// temporary appointment is governed by its own contract terms. It is also skipped for anybody
    /// arriving already confirmed — an imported employee who passed probation years ago does not
    /// need one opened for them today.</para>
    /// </remarks>
    private async Task OpenInitialContractAsync(
        Employee employee, Guid? contractTypeId, CancellationToken cancellationToken)
    {
        var start = employee.DateEmployed ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var type = await ResolveContractTypeAsync(contractTypeId, cancellationToken);

        var contract = new EmployeeContractDetail
        {
            TenantId = employee.TenantId,
            CreatedById = employee.CreatedById,
            EmployeeId = employee.Id,
            ContractNumber = await _numberSequence.GenerateAsync(ContractSequenceKey, cancellationToken),
            ContractTypeId = type?.Id,
            EmploymentType = employee.EmploymentType,
            StartDate = start,
            EffectiveDate = start,
            // Zero duration means open-ended: a permanent appointment has no scheduled end.
            ContractEndDate = type is { Duration: > 0 } ? start.AddMonths(type.Duration) : null,
            Salary = employee.Salary ?? 0m,
            ProbationPeriodDays = employee.ProbationPeriodDays,
            WorkSchedule = employee.EmploymentType == EmploymentType.PartTime
                ? WorkArrangementType.PartTime
                : WorkArrangementType.FullTime,
            IsCurrent = true,
            IsActive = true,
            ContractStatus = ContractStatus.Active,
            Notes = "Opened with the employee record.",
        };

        await FileContractAsync(contract, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (employee.EmploymentType != EmploymentType.Permanent
            || employee.ProbationPeriodDays <= 0
            || employee.ConfirmationDate != null)
            return;

        // Back to months, the unit ProbationPeriod counts in. At least one: a term of a few days is
        // still a probation, and a zero-month row would end the day it began.
        var months = Math.Max(1, (int)Math.Round(employee.ProbationPeriodDays / 30.0));
        var probationEnd = start.AddMonths(months);

        await _unitOfWork.Repository<ProbationPeriod>().AddAsync(new ProbationPeriod
        {
            TenantId = employee.TenantId,
            CreatedById = employee.CreatedById,
            EmployeeId = employee.Id,
            ContractDetailId = contract.Id,
            StartDate = start,
            OriginalEndDate = probationEnd,
            CurrentEndDate = probationEnd,
            DurationMonths = months,
            Status = ProbationStatus.Active,
        });

        // ⚠ Only from Active, never over a status the caller chose. Employee.IsOnProbation is
        // computed from StaffStatus, and leaving a person with a live probation row flagged Active
        // would tell the benefit rules they are past it. The hire path sets this the same way.
        if (employee.StaffStatus == StaffStatus.Active)
        {
            employee.StaffStatus = StaffStatus.Probation;
            await _employeeRepository.UpdateAsync(employee);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureInitialPositionHistoryExistsAsync(Employee employee, CancellationToken cancellationToken)
    {
        var repo = _unitOfWork.Repository<EmployeePositionHistory>();

        var anyExisting = await repo.GetQueryable()
            .AnyAsync(x => x.EmployeeId == employee.Id, cancellationToken);

        if (anyExisting)
        {
            return;
        }

        // OrganizationLevelId and PositionId are required on the history row. An employee may not have
        // them yet, and coalescing to Guid.Empty writes an FK that matches no row — the insert then
        // fails and takes the whole read down with it. Skip the back-fill instead; it runs again once
        // the employee has an org level.
        if (!employee.OrganizationLevelId.HasValue || employee.PositionId == Guid.Empty)
        {
            _logger.LogDebug(
                "Skipping initial position history for {EmployeeId}: no organization level or position yet.",
                employee.Id);
            return;
        }

        var startDate = employee.DateEmployed.HasValue
            ? employee.DateEmployed.Value.ToDateTime(TimeOnly.MinValue)
            : DateTime.UtcNow;

        var initial = new EmployeePositionHistory
        {
            EmployeeId = employee.Id,
            TenantId = employee.TenantId,
            // Nullable on both sides — leave it null rather than inventing an empty FK.
            LocationLevelId = employee.LocationLevelId,
            LocationId = employee.LocationId,
            OrganizationLevelId = employee.OrganizationLevelId.Value,
            OrganizationUnitId = employee.OrganizationUnitId,
            PositionId = employee.PositionId,
            StartDate = startDate,
            EndDate = null,
            ChangeReason = PositionChangeReason.InitialAssignment,
            Notes = "Initial position assignment"
        };

        await repo.AddAsync(initial);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<EmployeeDetailDto> UpdateEmployeeAsync(Guid employeeId, UpdateEmployeeDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null) throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        if (!string.IsNullOrWhiteSpace(dto.EmployeeNumber) &&
            !await IsEmployeeNumberUniqueAsync(dto.EmployeeNumber.Trim(), employeeId, cancellationToken))
            throw new InvalidOperationException($"Employee number '{dto.EmployeeNumber}' already exists.");

        if (!string.IsNullOrWhiteSpace(dto.EmailAddress) &&
            !await IsEmailUniqueAsync(dto.EmailAddress, employeeId, cancellationToken))
            throw new InvalidOperationException($"Email address '{dto.EmailAddress}' already exists.");

        if (!string.IsNullOrWhiteSpace(dto.BadgeNumber) &&
            !await IsBadgeNumberUniqueAsync(dto.BadgeNumber.Trim(), employeeId, cancellationToken))
            throw new InvalidOperationException($"Badge number '{dto.BadgeNumber}' already exists.");

        if (!string.IsNullOrWhiteSpace(dto.TaxNumber) &&
            !await IsTaxNumberUniqueAsync(dto.TaxNumber.Trim(), employeeId, cancellationToken))
            throw new InvalidOperationException($"Tax number '{dto.TaxNumber}' already exists.");

        if (!string.IsNullOrWhiteSpace(dto.SocialSecurityNumber) &&
            !await IsSocialSecurityNumberUniqueAsync(dto.SocialSecurityNumber.Trim(), employeeId, cancellationToken))
            throw new InvalidOperationException("Social security number already exists.");

        if (!string.IsNullOrWhiteSpace(dto.TINNumber) &&
            !await IsTinNumberUniqueAsync(dto.TINNumber.Trim(), employeeId, cancellationToken))
            throw new InvalidOperationException("TIN number already exists.");

        // Validate org unit / position changes
        Guid? newOrgLevelId = null;
        if (dto.OrganizationUnitId.HasValue)
        {
            var orgUnit = await _organizationUnitRepository.GetWithDetailsAsync(dto.OrganizationUnitId.Value);
            if (orgUnit == null || !orgUnit.IsActive)
                throw new ArgumentException("Organization unit not found or inactive.");

            newOrgLevelId = orgUnit.OrganizationLevelId;

            if (dto.PositionId.HasValue)
            {
                var position = await _positionRepository.GetByIdAsync(dto.PositionId.Value);
                if (position == null)
                    throw new ArgumentException("Position not found.");
                if (position.OrganizationUnitId != orgUnit.Id)
                    throw new InvalidOperationException("Selected position does not belong to the specified organization unit.");
            }
            else
            {
                // If org unit changed but position didn't, ensure current position still belongs.
                var currentPosition = await _positionRepository.GetByIdAsync(employee.PositionId);
                if (currentPosition != null && currentPosition.OrganizationUnitId != orgUnit.Id)
                    throw new InvalidOperationException("Current position does not belong to the specified organization unit.");
            }
        }

        Guid? newLocationLevelId = null;
        if (dto.LocationId.HasValue)
        {
            var location = await _locationRepository.GetWithDetailsAsync(dto.LocationId.Value);
            if (location == null || !location.IsActive)
                throw new ArgumentException("Location not found or inactive.");
            newLocationLevelId = location.LocationLevelId;
        }

        // Manager change validation
        if (dto.ManagerId.HasValue)
            await ValidateManagerAssignmentAsync(employeeId, dto.ManagerId.Value, cancellationToken);

        // Payroll flags validation (only validate if any flag is being changed or was supplied)
        var payTax = dto.PayTax ?? employee.PayTax;
        var ssFund = dto.SSFund ?? employee.SSFund;
        var grossUp = dto.GrossUp ?? employee.GrossUp;
        var tier2Only = dto.Tier2Only ?? employee.Tier2Only;
        var overtime = dto.Overtime ?? employee.Overtime;
        await ValidatePayrollFlagsAsync(payTax, ssFund, grossUp, tier2Only, overtime, cancellationToken);

        // Payroll membership. The rule is checked against what the record will look like AFTER
        // this update, so an off-payroll employee cannot be handed a salary by a payload that
        // simply omits the flag.
        var wasOnPayroll = employee.IsOnPayroll;
        var willBeOnPayroll = dto.IsOnPayroll ?? employee.IsOnPayroll;
        var reasonAfter = dto.OffPayrollReason ?? employee.OffPayrollReason;
        ValidatePayrollMembership(
            willBeOnPayroll, reasonAfter,
            dto.Salary ?? (willBeOnPayroll ? employee.Salary : null),
            (dto.PayTax ?? false) || (dto.SSFund ?? false) || (dto.GrossUp ?? false)
                || (dto.Tier2Only ?? false) || (dto.Overtime ?? false));

        // Round 3, lane S. The form stopped sending the figure in round 2 (E1); the server never
        // refused it. Under the approval policy a CHANGED figure on the header is refused too. A
        // first figure — the record had none, typically somebody just put (back) on payroll — is
        // not a change and passes; a request cannot be raised for someone off payroll, so refusing
        // it here would leave no door at all.
        if (dto.Salary.HasValue && employee.Salary.HasValue && dto.Salary.Value != employee.Salary.Value)
            await RequireDirectChangeAllowedAsync(SalaryChangeAuthority.Direct, cancellationToken);

        // ⚠ The hole this lane closes. `EmployeeMappingExtensions.Apply` wrote
        // `e.ConfirmationDate = dto.ConfirmationDate` with NO guard of any kind, so anybody who
        // could edit an employee could confirm them — or un-confirm them, or move the date —
        // bypassing the confirming authority, the probation record and the letter. The contract's
        // own copy of the date had been guarded since lane 3d, and that guard READS this column, so
        // the protected copy was the one nothing wrote and the authoritative one was open.
        //
        // Echoing the stored value back is fine: a form that round-trips the field is not asking to
        // change it. Anything else is refused, whoever is asking.
        if (dto.ConfirmationDate.HasValue && dto.ConfirmationDate != employee.ConfirmationDate)
            throw new InvalidOperationException(
                employee.ConfirmationDate is { } already
                    ? $"This employee was confirmed on {already:yyyy-MM-dd}. The date is recorded by the "
                      + "probation record that issued the letter, and is corrected there, not by editing "
                      + "the employee."
                    : "A confirmation date is recorded by confirming the employee's probation, not by "
                      + "editing the employee. Open their probation record and confirm it there.");

        // The post is the source of the probation term here too, so a position change carries the
        // new post's term with it rather than leaving the old one behind.
        var positionForProbation = dto.PositionId.HasValue
            ? await _positionRepository.GetByIdAsync(dto.PositionId.Value)
            : await _positionRepository.GetByIdAsync(employee.PositionId);

        var reresolveProbation = dto.PositionId.HasValue || dto.ProbationPeriodDays.HasValue;
        var (resolvedProbationDays, resolvedProbationSource) = reresolveProbation
            ? await ResolveProbationAsync(positionForProbation, dto.ProbationPeriodDays, cancellationToken)
            : (employee.ProbationPeriodDays, employee.ProbationSource ?? ProbationSource.PolicyDefault);

        // Track position changes for history
        var oldPositionId = employee.PositionId;
        var isPositionChanging = dto.PositionId.HasValue && dto.PositionId.Value != oldPositionId;
        var previousEmploymentType = employee.EmploymentType;
        var previousProbationDays = employee.ProbationPeriodDays;

        await _disabilityTypes.EnsureUsableAsync(dto.DisabilityTypeId, dto.HasDisability, cancellationToken); // round 3, lane P2
        dto.Apply(employee, newOrgLevelId, newLocationLevelId);

        // ⚠ AFTER Apply, which has just written whatever probation length the caller sent. The
        // resolved value wins, for the same reason the geo snapshot below overwrites the caller's
        // city: two sources for one fact, and only one of them is maintained.
        if (reresolveProbation)
        {
            employee.ProbationPeriodDays = resolvedProbationDays;
            employee.ProbationSource = resolvedProbationSource;
        }
        // null = not supplied; "" = clear. Before 2026-09-03 an email could never be removed.
        if (dto.EmailAddress != null)
            employee.EmailAddress = NormalizeEmail(dto.EmailAddress);

        // ⚠ AFTER Apply, on purpose. Apply has just written whatever State/City the caller sent;
        // this overwrites them from the area so the snapshot cannot be left disagreeing with the
        // link. Running it before Apply would let a stale form field win.
        await ApplyGeoAreaSnapshotAsync(employee, cancellationToken);

        await ApplyPayrollMembershipAsync(employee, dto, wasOnPayroll, willBeOnPayroll, cancellationToken);

        await _employeeRepository.UpdateAsync(employee);

        await WriteThroughToCurrentContractAsync(
            employee, previousEmploymentType, previousProbationDays, cancellationToken);

        // Handle position history if position changed
        if (isPositionChanging && dto.PositionId.HasValue)
        {
            var posHistoryRepo = _unitOfWork.Repository<EmployeePositionHistory>();
            var today = DateTime.Today;

            // Close the current position history entry
            var currentHistory = await posHistoryRepo
                .FirstOrDefaultAsync(ph => ph.EmployeeId == employeeId &&
                                     (ph.EndDate == null || ph.EndDate > today));
            
            if (currentHistory != null)
            {
                currentHistory.EndDate = today;
                currentHistory.ChangeReason = PositionChangeReason.Transfer; // or determine based on context
                await posHistoryRepo.UpdateAsync(currentHistory);
            }

            // Create new position history entry
            var newPosition = await _positionRepository.GetByIdAsync(dto.PositionId.Value);
            if (newPosition != null)
            {
                // ⚠ TenantId was missing here until 2026-09-03: the create path stamps it (the
                // DbContext auto-stamp is inert), this path did not, and every position change made
                // through an update failed on FK_EmployeePositionHistories_Tenants_TenantId. Found by
                // the employee-import update harness; the edit form had the same hole.
                var newHistory = new EmployeePositionHistory
                {
                    EmployeeId = employeeId,
                    TenantId = employee.TenantId,
                    PositionId = dto.PositionId.Value,
                    // Nullable on both sides — leave it null rather than inventing an empty FK (as on create).
                    LocationLevelId = newLocationLevelId ?? employee.LocationLevelId,
                    LocationId = dto.LocationId ?? employee.LocationId,
                    OrganizationLevelId = (newOrgLevelId ?? employee.OrganizationLevelId) ?? Guid.Empty,
                    OrganizationUnitId = dto.OrganizationUnitId ?? employee.OrganizationUnitId,
                    StartDate = today,
                    EndDate = null,
                    ChangeReason = PositionChangeReason.Transfer, // or determine based on context
                    Notes = "Position changed via employee update"
                };
                await posHistoryRepo.AddAsync(newHistory);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee updated: {EmployeeId}", employeeId);

        // Only when the caller SAID on-payroll (the form always does; an API caller that omits the
        // flag is editing something else and should not create payroll rows as a side effect).
        if (dto.IsOnPayroll == true && employee.IsOnPayroll)
            await _payrollMembership.EnsurePayrollProfileAsync(employee, cancellationToken);

        var updated = await _employeeRepository.GetByIdWithDetailsAsync(employeeId);
        if (updated == null) throw new InvalidOperationException("Employee updated but could not be reloaded.");
        return updated.ToDetailDto();
    }

    public async Task<EmployeeDetailDto> ActivateEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null) throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        if (employee.StaffStatus == StaffStatus.Terminated)
            throw new InvalidOperationException("Cannot activate a terminated employee. Use reinstate.");

        employee.IsActive = true;
        employee.StaffStatus = StaffStatus.Active;

        await _employeeRepository.UpdateAsync(employee);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _employeeRepository.GetByIdWithDetailsAsync(employeeId);
        if (updated == null) throw new InvalidOperationException("Employee updated but could not be reloaded.");
        return updated.ToDetailDto();
    }

    public async Task<EmployeeDetailDto> DeactivateEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null) throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        if (employee.StaffStatus == StaffStatus.Terminated)
            throw new InvalidOperationException("Cannot deactivate a terminated employee.");

        employee.IsActive = false;
        employee.StaffStatus = StaffStatus.Inactive;

        await _employeeRepository.UpdateAsync(employee);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _employeeRepository.GetByIdWithDetailsAsync(employeeId);
        if (updated == null) throw new InvalidOperationException("Employee updated but could not be reloaded.");
        return updated.ToDetailDto();
    }

    public async Task<EmployeeDetailDto> TerminateEmployeeAsync(Guid employeeId, TerminateEmployeeDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null) throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        if (employee.StaffStatus == StaffStatus.Terminated)
            throw new InvalidOperationException("Employee is already terminated.");

        // Basic termination eligibility
        if (!await CanTerminateEmployeeAsync(employeeId, cancellationToken))
            throw new InvalidOperationException("Employee cannot be terminated due to active dependencies/constraints.");

        // ⚠ This is the DIRECT path, and area 9b made it the exception rather than the rule. Where a
        // separation is in flight for this employee, terminating them here would walk straight past
        // the FR-HR-091 clearance gate, the FR-HR-092 signature and the FR-HR-185 settlement review
        // — every control the exit process exists to apply. Refused, with the way in named.
        var openSeparation = await _unitOfWork.Repository<EmployeeSeparation>().GetQueryable()
            .Where(s => s.EmployeeId == employeeId && s.TenantId == employee.TenantId && !s.IsDeleted
                        && s.Status != SeparationStatus.Cancelled
                        && s.Status != SeparationStatus.Rejected
                        && s.Status != SeparationStatus.Completed)
            .Select(s => new { s.SeparationNumber, s.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (openSeparation is not null)
            throw new InvalidOperationException(
                $"Separation {openSeparation.SeparationNumber} is in progress for this employee "
                + $"({openSeparation.Status}). Complete it through the separation process — terminating "
                + "the employee directly would bypass clearance, approval and the settlement review.");

        employee.StaffStatus = StaffStatus.Terminated;
        employee.IsActive = false;
        employee.TerminationDate = dto.TerminationDate;
        employee.TerminationNotes = dto.TerminationNotes;

        // ⚠ Was: TryParse ? value : null — an unknown or misspelled reason wrote NULL and the caller
        // still got a 200. Knowing why somebody left is the whole point of FR-HR-090, and a silently
        // discarded reason is unrecoverable once the person has gone.
        if (!string.IsNullOrWhiteSpace(dto.TerminationReason))
        {
            if (!Enum.TryParse<TerminationReason>(dto.TerminationReason, ignoreCase: true, out var parsedReason))
                throw new InvalidOperationException(
                    $"'{dto.TerminationReason}' is not a termination reason. Use one of: "
                    + string.Join(", ", Enum.GetNames<TerminationReason>()) + ".");

            employee.TerminationReason = parsedReason;
        }
        else
        {
            employee.TerminationReason = null;
        }

        // Terminate active contracts
        var contractRepo = _unitOfWork.Repository<EmployeeContractDetail>();
        var activeContracts = await contractRepo.FindAsync(c => c.EmployeeId == employeeId && c.IsActive && !c.IsDeleted);
        foreach (var c in activeContracts)
        {
            c.IsActive = false;
            c.IsCurrent = false;
            c.ContractStatus = ContractStatus.Terminated;
            c.TerminationDate = DateOnly.FromDateTime(dto.TerminationDate);
            c.TerminationReason = dto.TerminationReason;
            c.EndDate ??= DateOnly.FromDateTime(dto.TerminationDate);
            await contractRepo.UpdateAsync(c);
        }

        // Close current position history
        var posHistoryRepo = _unitOfWork.Repository<EmployeePositionHistory>();
        var currentHistory = await posHistoryRepo
            .FirstOrDefaultAsync(ph => ph.EmployeeId == employeeId && (ph.EndDate == null || ph.EndDate > dto.TerminationDate));

        if (currentHistory != null)
        {
            currentHistory.EndDate = dto.TerminationDate;
            currentHistory.ChangeReason = PositionChangeReason.Termination;
            await posHistoryRepo.UpdateAsync(currentHistory);
        }

        // G-3.2: log the seat this departure has emptied. Before 2026-09-15 nothing did — the
        // vacancy register was only as current as the last manual Reconcile, which the HR role
        // could not even run. Committed by the SaveChangesAsync below, in the same transaction as
        // the termination, so a failed termination cannot leave a vacancy behind.
        await PositionVacancyLog.LogDepartureAsync(
            _unitOfWork,
            employee.TenantId,
            employee.Id,
            employee.PositionId,
            VacancyReasonFor(employee.TerminationReason),
            dto.TerminationDate,
            _currentUserProvider.UserId,
            note: employee.TerminationNotes,
            cancellationToken: cancellationToken);

        await _employeeRepository.UpdateAsync(employee);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var terminated = await _employeeRepository.GetByIdWithDetailsAsync(employeeId);
        if (terminated == null) throw new InvalidOperationException("Employee terminated but could not be reloaded.");
        return terminated.ToDetailDto();
    }

    /// <summary>
    /// Translates why employment ended into why the seat is empty (G-3.4).
    /// </summary>
    /// <remarks>
    /// Two vocabularies for adjacent facts: <see cref="TerminationReason"/> is about the person's
    /// employment, <see cref="VacancyReason"/> is about the post. They overlap but are not the same
    /// list — there is no "Redundancy" vacancy reason because a redundant post is a restructure
    /// from the establishment's point of view, and no "Dismissal" because the register cares that
    /// the seat emptied by termination, not how badly. Anything with no counterpart maps to
    /// <c>Other</c>, which is what every reconcile-created row said before this existed.
    /// </remarks>
    private static VacancyReason VacancyReasonFor(TerminationReason? reason) => reason switch
    {
        TerminationReason.Resignation     => VacancyReason.Resignation,
        TerminationReason.Retirement      => VacancyReason.Retirement,
        TerminationReason.Death           => VacancyReason.Death,
        TerminationReason.Redundancy      => VacancyReason.Restructure,
        TerminationReason.Dismissal       => VacancyReason.Termination,
        TerminationReason.ContractExpiry  => VacancyReason.Termination,
        TerminationReason.MutualAgreement => VacancyReason.Termination,
        TerminationReason.EndOfInternship => VacancyReason.Termination,
        _                                 => VacancyReason.Other,
    };

    public async Task<EmployeeDetailDto> ReinstateEmployeeAsync(Guid employeeId, string? notes = null, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null) throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        if (employee.StaffStatus != StaffStatus.Terminated)
            throw new InvalidOperationException("Only terminated employees can be reinstated.");

        // ⚠ Was: all three termination fields nulled and the notes overwritten, so the fact that
        // somebody had been terminated and reinstated became unrecoverable. Reinstatement is an
        // event in an employment history, not an eraser. The dates and reason are cleared because
        // the person is employed again — but what they were is written into the notes first.
        var priorTermination = employee.TerminationDate is { } was
            ? $"[Reinstated {DateTime.UtcNow:yyyy-MM-dd}] Previously terminated {was:yyyy-MM-dd}"
              + (employee.TerminationReason is { } reason ? $" ({reason})" : string.Empty)
              + (string.IsNullOrWhiteSpace(employee.TerminationNotes) ? "." : $": {employee.TerminationNotes}")
            : null;

        var reinstatementNote = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        employee.StaffStatus = StaffStatus.Active;
        employee.IsActive = true;
        employee.TerminationDate = null;
        employee.TerminationReason = null;
        employee.TerminationNotes = string.Join(
            "\n\n",
            new[] { priorTermination, reinstatementNote }.Where(s => !string.IsNullOrWhiteSpace(s)));

        if (string.IsNullOrWhiteSpace(employee.TerminationNotes))
            employee.TerminationNotes = null;

        // ⚠ Contracts and position history closed by the termination are NOT reopened, deliberately.
        // A reinstated employee needs a new contract with its own start date — silently reviving a
        // contract that was terminated would make the record say they were employed throughout a
        // period when they were not. Stated here because the asymmetry looks like an oversight.

        await _employeeRepository.UpdateAsync(employee);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _employeeRepository.GetByIdWithDetailsAsync(employeeId);
        if (updated == null) throw new InvalidOperationException("Employee updated but could not be reloaded.");
        return updated.ToDetailDto();
    }

    public async Task<bool> DeleteEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        await _employeeRepository.DeleteAsync(employeeId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Employee soft-deleted: {EmployeeId}", employeeId);
        return true;
    }

    #endregion

    #region 2) Employee Retrieval (intent-based)

    public async Task<EmployeeDto?> GetEmployeeSummaryByIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId,
            e => e.Position,
            e => e.Department!,
            e => e.Section!,
            e => e.OrganizationUnit!,
            e => e.OrganizationLevel!,
            e => e.Location!,
            e => e.LocationLevel!);

        return employee?.ToSummaryDto();
    }

    public async Task<EmployeeDetailDto?> GetEmployeeDetailsByIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdWithDetailsAsync(employeeId);
        return employee?.ToDetailDto();
    }

    public async Task<EmployeeFullProfileDto?> GetEmployeeFullProfileByIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdWithFullProfileAsync(employeeId);
        return employee?.ToFullProfileDto();
    }

    public async Task<EmployeeDto?> GetEmployeeSummaryByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employeeNumber)) return null;
        var employee = await _employeeRepository.GetByEmployeeNumberAsync(employeeNumber.Trim());
        return employee?.ToSummaryDto();
    }

    public async Task<EmployeeDetailDto?> GetEmployeeDetailsByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employeeNumber)) return null;
        var employee = await _employeeRepository.GetByEmployeeNumberWithDetailsAsync(employeeNumber.Trim());
        return employee?.ToDetailDto();
    }

    public async Task<EmployeeFullProfileDto?> GetEmployeeFullProfileByEmployeeNumberAsync(string employeeNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employeeNumber)) return null;
        var employee = await _employeeRepository.GetByEmployeeNumberWithFullProfileAsync(employeeNumber.Trim());
        return employee?.ToFullProfileDto();
    }

    public async Task<EmployeeDto?> GetEmployeeSummaryByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var employee = await _employeeRepository.GetByEmailAsync(NormalizeEmail(email));
        return employee?.ToSummaryDto();
    }

    public async Task<PagedResult<EmployeeDto>> GetEmployeesPagedAsync(EmployeeSearchDto searchCriteria, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(searchCriteria);

        if (page <= 0) page = 1;
        if (pageSize <= 0) pageSize = 20;
        if (pageSize > 200) pageSize = 200;

        var tenantId = GetTenantId();
        IQueryable<Employee> q = _employeeRepository.GetQueryable().Where(e => e.TenantId == tenantId);

        q = q
            .Include(e => e.Position)
            .Include(e => e.Department)
            .Include(e => e.Section)
            .Include(e => e.OrganizationUnit)
            .Include(e => e.OrganizationLevel)
            .Include(e => e.Location)
            .Include(e => e.LocationLevel);

        if (!string.IsNullOrWhiteSpace(searchCriteria.SearchTerm))
        {
            var term = searchCriteria.SearchTerm.Trim();
            var like = $"%{term}%";
            q = q.Where(e =>
                EF.Functions.Like(e.FirstName, like) ||
                EF.Functions.Like(e.LastName, like) ||
                EF.Functions.Like(e.EmployeeNumber, like) ||
                EF.Functions.Like(e.EmailAddress, like) ||
                EF.Functions.Like((e.FirstName + " " + e.LastName), like));
        }

        if (searchCriteria.DepartmentId.HasValue) q = q.Where(e => e.DepartmentId == searchCriteria.DepartmentId);
        if (searchCriteria.SectionId.HasValue) q = q.Where(e => e.SectionId == searchCriteria.SectionId);
        if (searchCriteria.PositionId.HasValue) q = q.Where(e => e.PositionId == searchCriteria.PositionId);
        if (searchCriteria.StaffStatus.HasValue) q = q.Where(e => e.StaffStatus == searchCriteria.StaffStatus);
        if (searchCriteria.EmploymentType.HasValue) q = q.Where(e => e.EmploymentType == searchCriteria.EmploymentType);
        if (searchCriteria.IsActive.HasValue) q = q.Where(e => e.IsActive == searchCriteria.IsActive);
        if (searchCriteria.IsFullTime.HasValue) q = q.Where(e => e.IsFullTime == searchCriteria.IsFullTime);
        if (searchCriteria.MaintenanceTechniciansOnly == true) q = q.Where(e => e.CanBeAssignedToMaintenance);
        if (searchCriteria.IsOnPayroll.HasValue) q = q.Where(e => e.IsOnPayroll == searchCriteria.IsOnPayroll);
        if (searchCriteria.HiredAfter.HasValue) q = q.Where(e => e.DateEmployed >= searchCriteria.HiredAfter);
        if (searchCriteria.HiredBefore.HasValue) q = q.Where(e => e.DateEmployed <= searchCriteria.HiredBefore);

        var totalCount = await q.CountAsync(cancellationToken);

        var employees = await q.OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<EmployeeDto>
        {
            Items = employees.Select(e => e.ToSummaryDto()).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<EmployeeDto>> GetEmployeesByOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
        => (await _employeeRepository.GetByOrganizationUnitAsync(organizationUnitId)).Select(e => e.ToSummaryDto());

    public async Task<IEnumerable<EmployeeDto>> GetEmployeesByOrganizationLevelAsync(Guid organizationLevelId, CancellationToken cancellationToken = default)
        => (await _employeeRepository.GetByOrganizationLevelAsync(organizationLevelId)).Select(e => e.ToSummaryDto());

    public async Task<IEnumerable<EmployeeDto>> GetEmployeesByPositionAsync(Guid positionId, CancellationToken cancellationToken = default)
        => (await _employeeRepository.GetByPositionAsync(positionId)).Select(e => e.ToSummaryDto());

    public async Task<IEnumerable<EmployeeDto>> GetEmployeesByLocationAsync(Guid locationId, CancellationToken cancellationToken = default)
        => (await _employeeRepository.FindAsync(e => e.LocationId == locationId,
            e => e.Position,
            e => e.OrganizationUnit!,
            e => e.OrganizationLevel!,
            e => e.Location!,
            e => e.LocationLevel!)).Select(e => e.ToSummaryDto());

    public async Task<IEnumerable<EmployeeDto>> GetEmployeesByManagerAsync(Guid managerId, CancellationToken cancellationToken = default)
        => (await _employeeRepository.GetByManagerAsync(managerId)).Select(e => e.ToSummaryDto());

    public async Task<IEnumerable<EmployeeDto>> GetEmployeesByStatusAsync(StaffStatus status, CancellationToken cancellationToken = default)
        => (await _employeeRepository.GetByStatusAsync(status)).Select(e => e.ToSummaryDto());

    #endregion

    #region 3) Relationship Management (subresources)

        public async Task<IEnumerable<EmployeeContactDto>> GetContactsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeContact>();
        var items = await repo.FindAsync(e => e.EmployeeId == employeeId);
        return items.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.ContactType.ToString()).Select(x => x.ToDto());
    }

    public async Task<EmployeeContactDto> AddContactAsync(CreateEmployeeContactDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);

        var repo = _unitOfWork.Repository<EmployeeContact>();
        var entity = dto.ToEntity();
        // ToEntity() does not stamp the tenant, and the DbContext auto-stamp is inert, so an
        // unstamped row inserts TenantId = Guid.Empty and trips the Tenants FK.
        entity.TenantId = GetTenantId();

        await ApplySubRecordGeoAsync(
            entity.GeoAreaId, () => entity.CountryId, c => entity.CountryId = c,
            r => entity.Region = r, c => entity.City = c,
            "address", entity.Id, cancellationToken);

        if (entity.IsPrimary)
        {
            var existing = await repo.FindAsync(x => x.EmployeeId == dto.EmployeeId && x.IsPrimary);
            foreach (var c in existing)
            {
                c.IsPrimary = false;
                await repo.UpdateAsync(c);
            }
        }

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<EmployeeContactDto> UpdateContactAsync(UpdateEmployeeContactDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var repo = _unitOfWork.Repository<EmployeeContact>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException($"Contact '{dto.Id}' not found.");

        dto.Apply(entity);

        // ⚠ AFTER Apply — the tree wins over whatever spelling the payload sent.
        await ApplySubRecordGeoAsync(
            entity.GeoAreaId, () => entity.CountryId, c => entity.CountryId = c,
            r => entity.Region = r, c => entity.City = c,
            "address", entity.Id, cancellationToken);

        if (entity.IsPrimary)
        {
            var others = await repo.FindAsync(x => x.EmployeeId == entity.EmployeeId && x.Id != entity.Id && x.IsPrimary);
            foreach (var c in others)
            {
                c.IsPrimary = false;
                await repo.UpdateAsync(c);
            }
        }

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> RemoveContactAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeContact>();
        var entity = await repo.GetByIdAsync(contactId);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<EmployeeContactDto> SetPrimaryContactAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeContact>();
        var entity = await repo.GetByIdAsync(contactId);
        if (entity == null) throw new ArgumentException($"Contact '{contactId}' not found.");

        var existing = await repo.FindAsync(x => x.EmployeeId == entity.EmployeeId && x.IsPrimary && x.Id != entity.Id);
        foreach (var c in existing)
        {
            c.IsPrimary = false;
            await repo.UpdateAsync(c);
        }

        entity.IsPrimary = true;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<EmployeeContactDto?> GetContactByIdAsync(Guid contactId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeContact>();
        var entity = await repo.GetByIdAsync(contactId);
        return entity?.ToDto();
    }

    public async Task<IEnumerable<EmployeeEmergencyContactDto>> GetEmergencyContactsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeEmergencyContact>();
        var items = await repo.FindAsync(e => e.EmployeeId == employeeId);
        return items.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.LastName).Select(x => x.ToDto());
    }

    public async Task<EmployeeEmergencyContactDto> AddEmergencyContactAsync(CreateEmployeeEmergencyContactDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);

        var repo = _unitOfWork.Repository<EmployeeEmergencyContact>();
        var entity = dto.ToEntity();
        // ToEntity() does not stamp the tenant, and the DbContext auto-stamp is inert, so an
        // unstamped row inserts TenantId = Guid.Empty and trips the Tenants FK.
        entity.TenantId = GetTenantId();

        // ⚠ Familial and other only: a next of kin is not a former manager.
        entity.Relationship = await ResolveRelationshipAsync(
            entity.RelationshipTypeId, entity.Relationship, FamilialOrOther,
            "a next of kin", cancellationToken);

        await ApplySubRecordGeoAsync(
            entity.GeoAreaId, () => entity.CountryId, c => entity.CountryId = c,
            r => entity.Region = r, c => entity.City = c,
            "emergency contact", entity.Id, cancellationToken);

        if (entity.IsPrimary)
        {
            // Ensure single primary per employee
            var existing = await repo.FindAsync(x => x.EmployeeId == dto.EmployeeId && x.IsPrimary);
            foreach (var c in existing)
            {
                c.IsPrimary = false;
                await repo.UpdateAsync(c);
            }
        }

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<EmployeeEmergencyContactDto> UpdateEmergencyContactAsync(UpdateEmployeeEmergencyContactDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var repo = _unitOfWork.Repository<EmployeeEmergencyContact>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException($"Emergency contact '{dto.Id}' not found.");

        dto.Apply(entity);

        entity.Relationship = await ResolveRelationshipAsync(
            entity.RelationshipTypeId, entity.Relationship, FamilialOrOther,
            "a next of kin", cancellationToken);

        // ⚠ AFTER Apply — the tree wins over whatever spelling the payload sent.
        await ApplySubRecordGeoAsync(
            entity.GeoAreaId, () => entity.CountryId, c => entity.CountryId = c,
            r => entity.Region = r, c => entity.City = c,
            "emergency contact", entity.Id, cancellationToken);

        if (entity.IsPrimary)
        {
            var others = await repo.FindAsync(x => x.EmployeeId == entity.EmployeeId && x.Id != entity.Id && x.IsPrimary);
            foreach (var c in others)
            {
                c.IsPrimary = false;
                await repo.UpdateAsync(c);
            }
        }

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> RemoveEmergencyContactAsync(Guid emergencyContactId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeEmergencyContact>();
        var entity = await repo.GetByIdAsync(emergencyContactId);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<EmployeeEmergencyContactDto> SetPrimaryEmergencyContactAsync(Guid emergencyContactId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeEmergencyContact>();
        var entity = await repo.GetByIdAsync(emergencyContactId);
        if (entity == null) throw new ArgumentException($"Emergency contact '{emergencyContactId}' not found.");

        var existing = await repo.FindAsync(x => x.EmployeeId == entity.EmployeeId && x.IsPrimary && x.Id != entity.Id);
        foreach (var c in existing)
        {
            c.IsPrimary = false;
            await repo.UpdateAsync(c);
        }

        entity.IsPrimary = true;
        entity.IsActive = true;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<EmployeeEmergencyContactDto> ActivateEmergencyContactAsync(Guid emergencyContactId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeEmergencyContact>();
        var entity = await repo.GetByIdAsync(emergencyContactId);
        if (entity == null) throw new ArgumentException($"Emergency contact '{emergencyContactId}' not found.");

        entity.IsActive = true;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<EmployeeEmergencyContactDto> DeactivateEmergencyContactAsync(Guid emergencyContactId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeEmergencyContact>();
        var entity = await repo.GetByIdAsync(emergencyContactId);
        if (entity == null) throw new ArgumentException($"Emergency contact '{emergencyContactId}' not found.");

        if (entity.IsPrimary)
            throw new InvalidOperationException("Primary emergency contact cannot be deactivated. Set another primary first.");

        entity.IsActive = false;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }


    public async Task<IEnumerable<EmployeeDependentReadDto>> GetDependentsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeDependent>();
        // Round 3, lane P2: the type's name travels on the read.
        var items = await repo.GetQueryable().AsNoTracking().Include(d => d.DisabilityType)
            .Where(d => d.EmployeeId == employeeId && !d.IsDeleted).ToListAsync(cancellationToken);
        return items.OrderBy(d => d.LastName).ThenBy(d => d.FirstName).Select(d => d.ToReadDto());
    }

    public async Task<EmployeeDependentReadDto> AddDependentAsync(EmployeeDependentCreateDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);

        var repo = _unitOfWork.Repository<EmployeeDependent>();
        await _disabilityTypes.EnsureUsableAsync(dto.DisabilityTypeId, dto.HasDisability, cancellationToken); // round 3, lane P2
        var entity = dto.ToEntity();
        // ToEntity() does not stamp the tenant, and the DbContext auto-stamp is inert, so an
        // unstamped row inserts TenantId = Guid.Empty and trips the Tenants FK.
        entity.TenantId = GetTenantId();

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await LoadDependentAsync(repo, entity.Id, cancellationToken)).ToReadDto();
    }

    public async Task<EmployeeDependentReadDto> UpdateDependentAsync(EmployeeDependentUpdateDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var repo = _unitOfWork.Repository<EmployeeDependent>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException($"Dependent '{dto.Id}' not found.");

        await _disabilityTypes.EnsureUsableAsync(dto.DisabilityTypeId, dto.HasDisability ?? entity.HasDisability, cancellationToken); // round 3, lane P2
        dto.Apply(entity);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await LoadDependentAsync(repo, entity.Id, cancellationToken)).ToReadDto();
    }

    /// <summary>The dependant with its disability type loaded — the write paths answer through this (round 3, lane P2).</summary>
    private static async Task<EmployeeDependent> LoadDependentAsync(IGenericRepository<EmployeeDependent> repo, Guid id, CancellationToken cancellationToken)
        => await repo.GetQueryable().AsNoTracking().Include(d => d.DisabilityType).FirstAsync(d => d.Id == id, cancellationToken);

    public async Task<bool> RemoveDependentAsync(Guid employeeId, Guid dependentId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeDependent>();
        var entity = await repo.GetByIdAsync(dependentId);
        if (entity == null) return false;

        if (entity.EmployeeId != employeeId)
            throw new ArgumentException("Dependent does not belong to the specified employee.");

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<IEnumerable<EmployeeDependentBenefitDto>> GetDependentBenefitsAsync(Guid employeeDependentId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeDependentBenefit>();
        var q = repo.GetQueryable()
            .Include(b => b.BenefitPolicy)
            .Where(b => b.EmployeeDependentId == employeeDependentId);

        var items = await q.ToListAsync(cancellationToken);
        return items.Select(b => b.ToDto());
    }

    public async Task<EmployeeDependentBenefitDto> AddDependentBenefitAsync(CreateEmployeeDependentBenefitDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // Ensure dependent exists and is eligible for benefits
        var dependentRepo = _unitOfWork.Repository<EmployeeDependent>();
        var dependent = await dependentRepo.GetByIdAsync(dto.EmployeeDependentId);
        if (dependent == null) throw new ArgumentException("Dependent not found.");
        if (!dependent.IsEligibleForBenefits) throw new InvalidOperationException("Dependent is not eligible for benefits.");

        var repo = _unitOfWork.Repository<EmployeeDependentBenefit>();
        var entity = dto.ToEntity();
        // ToEntity() does not stamp the tenant, and the DbContext auto-stamp is inert, so an
        // unstamped row inserts TenantId = Guid.Empty and trips the Tenants FK.
        entity.TenantId = GetTenantId();

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // reload with policy for display
        var reloaded = await repo.GetQueryable().Include(b => b.BenefitPolicy).FirstOrDefaultAsync(b => b.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<EmployeeDependentBenefitDto> UpdateDependentBenefitAsync(UpdateEmployeeDependentBenefitDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var repo = _unitOfWork.Repository<EmployeeDependentBenefit>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException("Dependent benefit not found.");

        dto.Apply(entity);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(b => b.BenefitPolicy).FirstOrDefaultAsync(b => b.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<bool> RemoveDependentBenefitAsync(Guid dependentBenefitId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeDependentBenefit>();
        var entity = await repo.GetByIdAsync(dependentBenefitId);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<EmployeeDependentBenefitDto> ActivateDependentBenefitAsync(Guid dependentBenefitId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeDependentBenefit>();
        var entity = await repo.GetByIdAsync(dependentBenefitId);
        if (entity == null) throw new ArgumentException("Dependent benefit not found.");

        entity.IsActive = true;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(b => b.BenefitPolicy).FirstOrDefaultAsync(b => b.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<EmployeeDependentBenefitDto> DeactivateDependentBenefitAsync(Guid dependentBenefitId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeDependentBenefit>();
        var entity = await repo.GetByIdAsync(dependentBenefitId);
        if (entity == null) throw new ArgumentException("Dependent benefit not found.");

        entity.IsActive = false;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(b => b.BenefitPolicy).FirstOrDefaultAsync(b => b.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<IEnumerable<EmployeeQualificationDto>> GetQualificationsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeQualification>();
        var q = repo.GetQueryable()
            .Include(x => x.Qualification)
            .Include(x => x.Country)
            .Where(x => x.EmployeeId == employeeId);

        var items = await q.ToListAsync(cancellationToken);
        return items.Select(x => x.ToDto());
    }

    public async Task<EmployeeQualificationDto> AddQualificationAsync(CreateEmployeeQualificationDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);

        // Validate that either QualificationId or CustomQualificationName is provided
        if ((!dto.QualificationId.HasValue || dto.QualificationId.Value == Guid.Empty) && 
            string.IsNullOrWhiteSpace(dto.CustomQualificationName))
            throw new ArgumentException("Either QualificationId or CustomQualificationName is required.");

        var repo = _unitOfWork.Repository<EmployeeQualification>();
        var entity = dto.ToEntity();
        // ToEntity() does not stamp the tenant, and the DbContext auto-stamp is inert, so an
        // unstamped row inserts TenantId = Guid.Empty and trips the Tenants FK.
        entity.TenantId = GetTenantId();

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable()
            .Include(x => x.Qualification)
            .Include(x => x.Country)
            .FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);

        return (reloaded ?? entity).ToDto();
    }

    public async Task<EmployeeQualificationDto> UpdateQualificationAsync(UpdateEmployeeQualificationDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var repo = _unitOfWork.Repository<EmployeeQualification>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException("Qualification not found.");

        dto.Apply(entity);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable()
            .Include(x => x.Qualification)
            .Include(x => x.Country)
            .FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);

        return (reloaded ?? entity).ToDto();
    }

    public async Task<bool> RemoveQualificationAsync(Guid qualificationId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeQualification>();
        var entity = await repo.GetByIdAsync(qualificationId);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<EmployeeQualificationDto> VerifyQualificationAsync(Guid qualificationId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeQualification>();
        var entity = await repo.GetByIdAsync(qualificationId);
        if (entity == null) throw new ArgumentException("Qualification not found.");

        entity.IsVerified = true;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(x => x.Qualification).Include(x => x.Country).FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<EmployeeQualificationDto> UnverifyQualificationAsync(Guid qualificationId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeQualification>();
        var entity = await repo.GetByIdAsync(qualificationId);
        if (entity == null) throw new ArgumentException("Qualification not found.");

        entity.IsVerified = false;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(x => x.Qualification).Include(x => x.Country).FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDto();
    }

    /// <summary>
    /// What the employee's post asks of them, against what they hold (round 2, lane C3b).
    /// </summary>
    /// <remarks>
    /// Reads the position's EFFECTIVE skills - its attached skill sets unioned with its individual
    /// rows - and NOT PositionSkillRequirement directly. A skill the post requires through a set is
    /// as required as one listed individually, and reading the table would have told the employee
    /// they were complete while a whole set went unmentioned.
    /// </remarks>
    public async Task<EmployeeSkillRequirementsDto> GetSkillRequirementsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await _unitOfWork.Repository<Employee>().GetQueryable()
                           .AsNoTracking()
                           .Include(e => e.Position)
                           .Where(e => e.Id == employeeId && !e.IsDeleted)
                           .Select(e => new
                           {
                               e.Id,
                               Name = (e.FirstName + " " + e.LastName).Trim(),
                               e.PositionId,
                               PositionTitle = e.Position != null ? e.Position.Title : null,
                           })
                           .FirstOrDefaultAsync(cancellationToken)
                       ?? throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        var result = new EmployeeSkillRequirementsDto
        {
            EmployeeId = employee.Id,
            EmployeeName = employee.Name,
            PositionId = employee.PositionId == Guid.Empty ? null : employee.PositionId,
            PositionTitle = employee.PositionTitle,
            IsCompliant = true,
        };

        if (employee.PositionId == Guid.Empty) return result;

        var required = await _namedSets.GetEffectiveSkillsAsync(employee.PositionId, null, cancellationToken);
        // The held rows go through the SAME mapper the skills list uses, so the credential rule is
        // stated once rather than reimplemented here.
        var held = (await GetSkillsAsync(employeeId, cancellationToken)).ToList();

        foreach (var r in required)
        {
            var mine = held.FirstOrDefault(h => h.SkillId == r.SkillId);
            var meets = mine is not null && (int)mine.SkillLevel >= (int)r.RequiredLevel;

            result.Lines.Add(new EmployeeSkillRequirementLineDto
            {
                SkillId = r.SkillId,
                SkillName = r.SkillName,
                SkillCategory = r.SkillCategory,
                RequiredLevel = r.RequiredLevel,
                IsRequired = r.IsRequired,
                Priority = r.Priority,
                Sources = r.Sources,
                Held = mine is not null,
                EmployeeSkillId = mine?.Id,
                HeldLevel = mine?.SkillLevel,
                IsVerified = mine?.IsVerified ?? false,
                MeetsLevel = meets,
                RequiresCertification = r.RequiresCertification,
                CredentialSatisfied = mine?.IsCompliant ?? false,
                Status = mine is null ? "Missing" : meets ? "Held" : "BelowLevel",
            });
        }

        var mandatory = result.Lines.Where(l => l.IsRequired).ToList();
        result.RequiredCount = mandatory.Count;
        result.RequiredHeldCount = mandatory.Count(l => l.Held);
        result.RequiredAtLevelCount = mandatory.Count(l => l.MeetsLevel);
        result.IsCompliant = result.RequiredAtLevelCount == result.RequiredCount;
        return result;
    }

    public async Task<IEnumerable<EmployeeSkillDto>> GetSkillsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeSkill>();
        var items = await repo.GetQueryable()
            .Include(s => s.Skill)
            // ⚠ The DTO reports the catalogued certifier's NAME, so every read that builds one has to
            // load it — including the re-reads after a write, or the response contradicts the list.
            .Include(s => s.CertifyingBodyRef)
            .Include(s => s.EmployeeCertification).ThenInclude(c => c!.Certification)
            .Where(s => s.EmployeeId == employeeId)
            .OrderByDescending(s => s.IsVerified)
            .ThenByDescending(s => s.SkillLevel)
            .ToListAsync(cancellationToken);

        return items.Select(s => s.ToDto());
    }

    public async Task<EmployeeSkillDto> AddSkillAsync(CreateEmployeeSkillDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);

        var repo = _unitOfWork.Repository<EmployeeSkill>();

        var exists = await repo.ExistsAsync(s => s.EmployeeId == dto.EmployeeId && s.SkillId == dto.SkillId);
        if (exists) throw new InvalidOperationException("Employee already has this skill assigned.");

        await RequireOwnCredentialAsync(dto.EmployeeId, dto.EmployeeCertificationId, cancellationToken);

        var entity = dto.ToEntity();
        // ToEntity() does not stamp the tenant, and the DbContext auto-stamp is inert, so an
        // unstamped row inserts TenantId = Guid.Empty and trips the Tenants FK.
        entity.TenantId = GetTenantId();

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(s => s.Skill).Include(s => s.CertifyingBodyRef)
            .Include(s => s.EmployeeCertification).ThenInclude(c => c!.Certification)
            .FirstOrDefaultAsync(s => s.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDto();
    }

    /// <summary>
    /// A credential named on a skill row must be one the SAME employee holds (round 2, lane C2).
    /// Recording a skill without one is allowed — the row is flagged, not refused.
    /// </summary>
    private async Task RequireOwnCredentialAsync(Guid employeeId, Guid? employeeCertificationId, CancellationToken cancellationToken)
    {
        if (!employeeCertificationId.HasValue) return;
        var owned = await _unitOfWork.Repository<EmployeeCertification>().GetQueryable()
            .AnyAsync(c => c.Id == employeeCertificationId.Value && c.EmployeeId == employeeId && !c.IsDeleted, cancellationToken);
        if (!owned)
            throw new ArgumentException("The credential named as evidence is not one this employee holds.");
    }

    public async Task<EmployeeSkillDto> UpdateSkillAsync(UpdateEmployeeSkillDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var repo = _unitOfWork.Repository<EmployeeSkill>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException("Employee skill not found.");

        await RequireOwnCredentialAsync(entity.EmployeeId, dto.EmployeeCertificationId, cancellationToken);

        dto.Apply(entity);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(s => s.Skill).Include(s => s.CertifyingBodyRef)
            .Include(s => s.EmployeeCertification).ThenInclude(c => c!.Certification)
            .FirstOrDefaultAsync(s => s.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<bool> RemoveSkillAsync(Guid employeeSkillId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeSkill>();
        var entity = await repo.GetByIdAsync(employeeSkillId);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<EmployeeSkillDto> VerifySkillAsync(Guid employeeSkillId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeSkill>();
        var entity = await repo.GetByIdAsync(employeeSkillId);
        if (entity == null) throw new ArgumentException("Employee skill not found.");

        entity.IsVerified = true;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(s => s.Skill).Include(s => s.CertifyingBodyRef).FirstOrDefaultAsync(s => s.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<EmployeeSkillDto> UnverifySkillAsync(Guid employeeSkillId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeSkill>();
        var entity = await repo.GetByIdAsync(employeeSkillId);
        if (entity == null) throw new ArgumentException("Employee skill not found.");

        entity.IsVerified = false;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(s => s.Skill).Include(s => s.CertifyingBodyRef).FirstOrDefaultAsync(s => s.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDto();
    }

    public async Task<IEnumerable<EmployeeIdentificationCardListDto>> GetIdentificationCardsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeIdentificationCard>();
        var items = await repo.GetQueryable()
            .Include(x => x.IdentificationType)
                .ThenInclude(it => it.IssuingCountry)
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.ExpiryDate)
            .ToListAsync(cancellationToken);

        return items.Select(x => x.ToListDto());
    }

    public async Task<EmployeeIdentificationCardDetailDto?> GetIdentificationCardByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeIdentificationCard>();
        var entity = await repo.GetQueryable()
            .Include(x => x.IdentificationType)
                .ThenInclude(it => it.IssuingCountry)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity?.ToDetailDto();
    }

    public async Task<EmployeeIdentificationCardDetailDto> AddIdentificationCardAsync(CreateEmployeeIdentificationCardDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);

        var repo = _unitOfWork.Repository<EmployeeIdentificationCard>();
        var entity = dto.ToEntity();
        // ToEntity() does not stamp the tenant, and the DbContext auto-stamp is inert, so an
        // unstamped row inserts TenantId = Guid.Empty and trips the Tenants FK.
        entity.TenantId = GetTenantId();

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable()
            .Include(x => x.IdentificationType)
                .ThenInclude(it => it.IssuingCountry)
            .FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDetailDto();
    }

    public async Task<EmployeeIdentificationCardDetailDto> UpdateIdentificationCardAsync(UpdateEmployeeIdentificationCardDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var repo = _unitOfWork.Repository<EmployeeIdentificationCard>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException("Identification card not found.");

        dto.Apply(entity);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable()
            .Include(x => x.IdentificationType)
                .ThenInclude(it => it.IssuingCountry)
            .FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDetailDto();
    }

    public async Task<bool> RemoveIdentificationCardAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeIdentificationCard>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<EmployeeIdentificationCardDetailDto> VerifyIdentificationCardAsync(Guid id, DateTime verifiedDate, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeIdentificationCard>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Identification card not found.");

        entity.IsVerified = true;
        entity.VerifiedDate = verifiedDate;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(x => x.IdentificationType).FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDetailDto();
    }

    public async Task<EmployeeIdentificationCardDetailDto> UnverifyIdentificationCardAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeIdentificationCard>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Identification card not found.");

        entity.IsVerified = false;
        entity.VerifiedDate = null;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(x => x.IdentificationType).FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDetailDto();
    }

    public async Task<IEnumerable<EmployeeWorkHistoryListDto>> GetWorkHistoriesAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeWorkHistory>();
        var items = await repo.FindAsync(x => x.EmployeeId == employeeId);
        return items.OrderByDescending(x => x.StartDate).Select(x => x.ToListDto());
    }

    public async Task<EmployeeWorkHistoryDetailDto?> GetWorkHistoryByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeWorkHistory>();
        var entity = await repo.GetByIdAsync(id);
        return entity?.ToDetailDto();
    }

    public async Task<EmployeeWorkHistoryDetailDto> AddWorkHistoryAsync(CreateEmployeeWorkHistoryDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);

        var repo = _unitOfWork.Repository<EmployeeWorkHistory>();
        var entity = dto.ToEntity();
        // ToEntity() does not stamp the tenant, and the DbContext auto-stamp is inert, so an
        // unstamped row inserts TenantId = Guid.Empty and trips the Tenants FK.
        entity.TenantId = GetTenantId();

        await ApplySubRecordGeoAsync(
            entity.GeoAreaId, () => entity.CountryId, c => entity.CountryId = c,
            r => entity.Region = r, c => entity.City = c,
            "previous employer", entity.Id, cancellationToken);

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDetailDto();
    }

    public async Task<EmployeeWorkHistoryDetailDto> UpdateWorkHistoryAsync(UpdateEmployeeWorkHistoryDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var repo = _unitOfWork.Repository<EmployeeWorkHistory>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException("Work history not found.");

        dto.Apply(entity);

        // ⚠ AFTER Apply — the tree wins over whatever spelling the payload sent.
        await ApplySubRecordGeoAsync(
            entity.GeoAreaId, () => entity.CountryId, c => entity.CountryId = c,
            r => entity.Region = r, c => entity.City = c,
            "previous employer", entity.Id, cancellationToken);

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDetailDto();
    }

    public async Task<bool> RemoveWorkHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeWorkHistory>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// Carries a change of employment type or probation term from the employee header onto the
    /// contract in force.
    /// </summary>
    /// <remarks>
    /// <para>E-7b. Both facts live in two places — <c>Employee.EmploymentType</c> /
    /// <c>ProbationPeriodDays</c> and the current <c>EmployeeContractDetail</c>'s own copies — and
    /// nothing kept them in step, so a person converted from contract to permanent on the header
    /// went on holding a contract that said otherwise.</para>
    ///
    /// <para><b>One direction only.</b> The header is the source; the contract's copies are a
    /// snapshot of it. Writing back the other way would mean two editable copies of one fact and no
    /// answer to which is right — which is the state this is fixing.</para>
    ///
    /// <para>⚠ The probation term is NOT copied once the employee is confirmed. It is then a record
    /// of something that happened, with a letter issued against it — the same rule
    /// <see cref="RequireUnconfirmedProbationAsync"/> enforces on the contract's own edit path, and
    /// it would be pointless to guard the direct door and leave this one open.</para>
    /// </remarks>
    private async Task WriteThroughToCurrentContractAsync(
        Employee employee,
        EmploymentType previousEmploymentType,
        int previousProbationDays,
        CancellationToken cancellationToken)
    {
        var employmentTypeChanged = employee.EmploymentType != previousEmploymentType;
        var probationChanged = employee.ProbationPeriodDays != previousProbationDays
                            && employee.ConfirmationDate == null;

        if (!employmentTypeChanged && !probationChanged) return;

        var current = await GetCurrentContractEntityAsync(employee.Id, cancellationToken);
        if (current == null) return;

        if (employmentTypeChanged) current.EmploymentType = employee.EmploymentType;
        if (probationChanged) current.ProbationPeriodDays = employee.ProbationPeriodDays;

        await _unitOfWork.Repository<EmployeeContractDetail>().UpdateAsync(current);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Contract {ContractNumber} updated from the employee header for {EmployeeNumber}.",
            current.ContractNumber, employee.EmployeeNumber);
    }

    public async Task<IEnumerable<EmployeeContractDetailDto>> GetContractsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        // Include the kind so the list can print its name rather than a bare id. Ordered on the
        // effective date, which is what supersession runs on — StartDate can equal it on every row
        // when a series of amendments all quote the original engagement.
        var items = await _unitOfWork.Repository<EmployeeContractDetail>().GetQueryable()
            .Include(x => x.ContractType)
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.EffectiveDate)
            .ThenByDescending(x => x.StartDate)
            .ToListAsync(cancellationToken);

        return items.Select(x => x.ToDto());
    }

    /// <remarks>
    /// ⚠ The terms in FORCE, which is <c>IsCurrent</c> — not the first row that happens to be
    /// <c>IsActive</c>, which is what this returned before lane D1. With nothing maintaining either
    /// flag, an employee with three contracts had three actives and the answer was whichever one EF
    /// materialised first.
    /// </remarks>
    public async Task<EmployeeContractDetailDto?> GetActiveContractAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var entity = await _unitOfWork.Repository<EmployeeContractDetail>().GetQueryable()
            .Include(x => x.ContractType)
            .Where(x => x.EmployeeId == employeeId && x.IsCurrent && !x.IsDeleted)
            .OrderByDescending(x => x.EffectiveDate)
            .FirstOrDefaultAsync(cancellationToken);

        return entity?.ToDto();
    }

    /// <remarks>
    /// <para>⚠ <b>Defect X-4.</b> This used to refuse a second contract outright — "Employee already
    /// has an active contract" — which is the wrong answer to the commonest thing that happens to
    /// terms of employment: they get replaced. A promotion, a conversion from contract to permanent,
    /// a renewal all produce NEW terms, and the old ones become history rather than an obstacle. So
    /// the refusal is gone and the previous current row is closed instead, by
    /// <see cref="FileContractAsync"/>.</para>
    ///
    /// <para>Three fields the writer never set are now set: <c>EffectiveDate</c> (it inserted
    /// <c>0001-01-01</c> on every row), <c>ContractEndDate</c> and <c>AnnualLeaveEntitlementDays</c>
    /// — the last two because the DTO did not carry them at all.</para>
    /// </remarks>
    /// <inheritdoc />
    public async Task<EmployeeContractDetailDto?> SupersedeCurrentContractAsync(
        Guid employeeId,
        DateOnly effectiveDate,
        decimal? newSalary,
        EmploymentType? newEmploymentType,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var current = await GetCurrentContractEntityAsync(employeeId, cancellationToken);
        if (current == null)
        {
            // Not an error. Most of the register predates contracts being written on create, and
            // inventing a start date for somebody hired in 2014 would be worse than saying nothing.
            _logger.LogInformation(
                "Employee {EmployeeId} has no contract in force, so none was superseded.", employeeId);
            return null;
        }

        var salaryChanged = newSalary is > 0 && newSalary.Value != current.Salary;
        var typeChanged = newEmploymentType.HasValue && newEmploymentType.Value != current.EmploymentType;
        if (!salaryChanged && !typeChanged) return null;

        var successor = new EmployeeContractDetail
        {
            TenantId = current.TenantId,
            EmployeeId = employeeId,
            ContractNumber = await _numberSequence.GenerateAsync(ContractSequenceKey, cancellationToken),
            ContractTypeId = current.ContractTypeId,
            EmploymentType = newEmploymentType ?? current.EmploymentType,
            StartDate = effectiveDate,
            EffectiveDate = effectiveDate,
            ContractEndDate = current.ContractEndDate,
            Salary = salaryChanged ? newSalary!.Value : current.Salary,
            CurrencyCode = current.CurrencyCode,
            PayFrequency = current.PayFrequency,
            TaxTreatmentType = current.TaxTreatmentType,
            WithholdingTaxRate = current.WithholdingTaxRate,
            IsPensionApplicable = current.IsPensionApplicable,
            IsTaxExempt = current.IsTaxExempt,
            WorkingHoursPerWeek = current.WorkingHoursPerWeek,
            ProbationPeriodDays = current.ProbationPeriodDays,
            // ⚠ ConfirmationDate is deliberately NOT carried across. It is a hand-typed copy of a
            // fact that lives on the employee, and copying it onto every successor would spread a
            // duplicate of the one field this lane is trying to keep in a single place.
            WorkSchedule = current.WorkSchedule,
            Terms = current.Terms,
            SpecialConditions = current.SpecialConditions,
            IsCurrent = true,
            IsActive = true,
            ContractStatus = ContractStatus.Active,
            Notes = reason,
        };

        await FileContractAsync(successor, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Contract {ContractNumber} opened for employee {EmployeeId}, superseding {Previous}.",
            successor.ContractNumber, employeeId, current.ContractNumber);

        return successor.ToDto();
    }

    public async Task<EmployeeContractDetailDto> AddContractAsync(CreateEmployeeContractDetailDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);

        ValidateContractTaxRules(dto.TaxTreatmentType, dto.WithholdingTaxRate);

        // ⚠ The same gate the guarantor amount goes through. A salary currency is money's unit;
        // an unvalidated one lets "XYZ" onto a contract and every figure derived from it. 3a
        // established the pattern against Finance's master — not a second HR-side list.
        await _currencies.RequireKnownCurrencyAsync(dto.CurrencyCode, cancellationToken, optional: true);

        var type = await ResolveContractTypeAsync(dto.ContractTypeId, cancellationToken);

        // Silence about the effective date means "when it starts", which is what every caller
        // writing a single contract meant all along.
        var effectiveDate = dto.EffectiveDate ?? dto.StartDate;

        // A row filed as inactive or already expired is history being recorded, not the terms coming
        // into force — so it supersedes nothing. That is the door a backfill goes through.
        var isCurrent = dto.IsActive && dto.ContractStatus == ContractStatus.Active;

        var entity = new EmployeeContractDetail
        {
            EmployeeId = dto.EmployeeId,
            TenantId = GetTenantId(),
            ContractNumber = dto.ContractNumber.Trim(),
            ContractTypeId = type?.Id,
            EmploymentType = dto.EmploymentType,
            StartDate = dto.StartDate,
            EffectiveDate = effectiveDate,
            EndDate = dto.EndDate,
            // The kind's duration is the default, not an override: a date the caller gave stands.
            ContractEndDate = dto.ContractEndDate
                              ?? (type is { Duration: > 0 } ? dto.StartDate.AddMonths(type.Duration) : null),
            Salary = dto.Salary,
            PayFrequency = dto.PayFrequency,
            TaxTreatmentType = dto.TaxTreatmentType,
            WithholdingTaxRate = dto.WithholdingTaxRate,
            IsPensionApplicable = dto.IsPensionApplicable,
            IsTaxExempt = dto.IsTaxExempt,
            WorkingHoursPerWeek = dto.WorkingHoursPerWeek,
            ProbationPeriodDays = dto.ProbationPeriodDays,
            ConfirmationDate = dto.ConfirmationDate,
            CurrencyCode = dto.CurrencyCode,
            WorkSchedule = dto.WorkSchedule,
            SpecialConditions = dto.SpecialConditions,
            Notes = dto.Notes,
            Terms = dto.Terms,
            IsCurrent = isCurrent,
            IsActive = dto.IsActive,
            ContractPath = dto.ContractPath,
            ContractStatus = dto.ContractStatus,
            TerminationDate = dto.TerminationDate,
            TerminationReason = dto.TerminationReason
        };

        await FileContractAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        entity.ContractType = type;
        return entity.ToDto();
    }

    /// <summary>
    /// Refuses a change to a contract's probation terms once the employee has been confirmed.
    /// </summary>
    /// <remarks>
    /// <para>Only the two probation fields are gated. Salary, hours and leave are ordinary terms
    /// that change over an employment; the probation term and the date it was passed are a record
    /// of something that already happened, and a probation letter has been issued against them.</para>
    ///
    /// <para>⚠ In the service, not on the screen — the D-03 lesson. A client-side check protects
    /// one form; an import, another screen or a direct call would still rewrite it.</para>
    /// </remarks>
    private async Task RequireUnconfirmedProbationAsync(EmployeeContractDetail contract, CancellationToken cancellationToken)
    {
        var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(contract.EmployeeId);
        if (employee == null || employee.ConfirmationDate == null) return;

        throw new InvalidOperationException(
            $"This employee was confirmed on {employee.ConfirmationDate:yyyy-MM-dd}, so the probation term "
            + "on their contract can no longer be changed. Correct the confirmation through the probation "
            + "record, which is what issued the letter.");
    }

    public async Task<EmployeeContractDetailDto> UpdateContractAsync(UpdateEmployeeContractDetailDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var repo = _unitOfWork.Repository<EmployeeContractDetail>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException("Contract not found.");

        if (!string.IsNullOrWhiteSpace(dto.ContractNumber)) entity.ContractNumber = dto.ContractNumber.Trim();
        if (dto.EmploymentType.HasValue) entity.EmploymentType = dto.EmploymentType.Value;
        if (dto.ContractTypeId.HasValue)
        {
            var type = await ResolveContractTypeAsync(dto.ContractTypeId, cancellationToken);
            entity.ContractTypeId = type?.Id;
        }
        if (dto.StartDate.HasValue) entity.StartDate = dto.StartDate.Value;
        if (dto.EffectiveDate.HasValue) entity.EffectiveDate = dto.EffectiveDate.Value;
        if (dto.EndDate.HasValue) entity.EndDate = dto.EndDate;
        if (dto.ContractEndDate.HasValue) entity.ContractEndDate = dto.ContractEndDate;
        if (dto.Salary.HasValue) entity.Salary = dto.Salary.Value;
        if (dto.PayFrequency.HasValue) entity.PayFrequency = dto.PayFrequency.Value;
        if (dto.TaxTreatmentType.HasValue) entity.TaxTreatmentType = dto.TaxTreatmentType.Value;
        if (dto.WithholdingTaxRate.HasValue) entity.WithholdingTaxRate = dto.WithholdingTaxRate;
        if (dto.IsPensionApplicable.HasValue) entity.IsPensionApplicable = dto.IsPensionApplicable.Value;
        if (dto.IsTaxExempt.HasValue) entity.IsTaxExempt = dto.IsTaxExempt.Value;
        if (dto.WorkingHoursPerWeek.HasValue) entity.WorkingHoursPerWeek = dto.WorkingHoursPerWeek.Value;
        // ⚠ Ledger lane 3d. Probation terms stayed editable after the employee was confirmed:
        // the term could be stretched, or the confirmation date moved, on a contract whose probation
        // had already been decided and a letter issued against it. The same shape as D-03, where an
        // APPROVED job description's content could still be rewritten.
        //
        // The rule keys off the EMPLOYEE, not the contract. Confirmation is recorded by
        // ProbationService.MarkEmployeeConfirmed, which sets Employee.ConfirmationDate and moves
        // StaffStatus off Probation; EmployeeContractDetail.ConfirmationDate is a separate,
        // hand-typed copy that nothing else writes. Guarding the contract's own copy against
        // itself would let the real confirmation be contradicted.
        if (dto.ProbationPeriodDays.HasValue || dto.ConfirmationDate.HasValue)
            await RequireUnconfirmedProbationAsync(entity, cancellationToken);

        if (dto.ProbationPeriodDays.HasValue) entity.ProbationPeriodDays = dto.ProbationPeriodDays;
        if (dto.ConfirmationDate.HasValue) entity.ConfirmationDate = dto.ConfirmationDate;
        if (dto.CurrencyCode != null)
        {
            await _currencies.RequireKnownCurrencyAsync(dto.CurrencyCode, cancellationToken, optional: true);
            entity.CurrencyCode = dto.CurrencyCode;
        }
        if (dto.WorkSchedule.HasValue) entity.WorkSchedule = dto.WorkSchedule.Value;
        if (dto.SpecialConditions != null) entity.SpecialConditions = dto.SpecialConditions;
        if (dto.Notes != null) entity.Notes = dto.Notes;
        if (dto.Terms != null) entity.Terms = dto.Terms;
        if (dto.IsActive.HasValue) entity.IsActive = dto.IsActive.Value;
        if (dto.ContractPath != null) entity.ContractPath = dto.ContractPath;
        if (dto.ContractStatus.HasValue) entity.ContractStatus = dto.ContractStatus.Value;
        if (dto.TerminationDate.HasValue) entity.TerminationDate = dto.TerminationDate;
        if (dto.TerminationReason != null) entity.TerminationReason = dto.TerminationReason;

        ValidateContractTaxRules(entity.TaxTreatmentType, entity.WithholdingTaxRate);

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<bool> RemoveContractAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeContractDetail>();
        var entity = await repo.GetByIdAsync(contractId);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<EmployeeContractDetailDto> ActivateContractAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeContractDetail>();
        var entity = await repo.GetByIdAsync(contractId);
        if (entity == null) throw new ArgumentException("Contract not found.");

        // Reinstating one row while another is in force would leave two claiming to be the terms.
        // The way to replace terms is to add a contract, which supersedes cleanly.
        var current = await GetCurrentContractEntityAsync(entity.EmployeeId, cancellationToken);
        if (current != null && current.Id != entity.Id)
            throw new InvalidOperationException(
                $"{current.ContractNumber} is the contract in force since {current.EffectiveDate:yyyy-MM-dd}. "
                + "Add a new contract to replace it — that closes this one — rather than reactivating an old one.");

        entity.IsActive = true;
        entity.IsCurrent = true;
        entity.ContractStatus = ContractStatus.Active;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<EmployeeContractDetailDto> DeactivateContractAsync(Guid contractId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeContractDetail>();
        var entity = await repo.GetByIdAsync(contractId);
        if (entity == null) throw new ArgumentException("Contract not found.");

        entity.IsActive = false;
        // ⚠ Cleared together, always. Leaving IsCurrent set on a deactivated row is how a terminated
        // contract went on being returned as the employee's current terms.
        entity.IsCurrent = false;
        entity.ContractStatus = entity.ContractStatus == ContractStatus.Active ? ContractStatus.Expired : entity.ContractStatus;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<EmployeeContractDetailDto> TerminateContractAsync(Guid contractId, DateOnly terminationDate, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Termination reason is required.");

        var repo = _unitOfWork.Repository<EmployeeContractDetail>();
        var entity = await repo.GetByIdAsync(contractId);
        if (entity == null) throw new ArgumentException("Contract not found.");

        entity.IsActive = false;
        entity.IsCurrent = false;
        entity.ContractStatus = ContractStatus.Terminated;
        entity.TerminationDate = terminationDate;
        entity.TerminationReason = reason.Trim();
        entity.EndDate ??= terminationDate;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDto();
    }

    public async Task<IEnumerable<ExpatriateAssignmentListDto>> GetExpatriateAssignmentsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<ExpatriateAssignment>();
        var items = await repo.GetQueryable()
            .Include(x => x.Country)
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.StartDate)
            .ToListAsync(cancellationToken);
        return items.Select(x => x.ToListDto());
    }

    public async Task<ExpatriateAssignmentDetailDto?> GetExpatriateAssignmentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<ExpatriateAssignment>();
        var entity = await repo.GetQueryable()
            .Include(x => x.Country)
            // ⚠ Without this the detail read returns an EMPTY family for an assignment that has
            // one, and the panel says "nobody accompanied them". A detail read that drops the very
            // collection its screen exists to show is the D-09 shape, met eight times in this module.
            .Include(x => x.FamilyMembers)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity?.ToDetailDto();
    }

    public async Task<ExpatriateAssignmentDetailDto> AddExpatriateAssignmentAsync(CreateExpatriateAssignmentDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);
        if (!await IsEligibleForExpatriateAssignmentAsync(dto.EmployeeId, cancellationToken))
            throw new InvalidOperationException("Employee is not eligible for expatriate assignment.");

        var repo = _unitOfWork.Repository<ExpatriateAssignment>();
        var entity = dto.ToEntity();
        // ToEntity() does not stamp the tenant, and the DbContext auto-stamp is inert, so an
        // unstamped row inserts TenantId = Guid.Empty and trips the Tenants FK.
        entity.TenantId = GetTenantId();

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable()
            .Include(x => x.Country)
            .Include(x => x.FamilyMembers)
            .FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDetailDto();
    }

    public async Task<ExpatriateAssignmentDetailDto> UpdateExpatriateAssignmentAsync(UpdateExpatriateAssignmentDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var repo = _unitOfWork.Repository<ExpatriateAssignment>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException("Expatriate assignment not found.");

        dto.Apply(entity);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // ⚠ FamilyMembers included here too, or the UPDATE response reports an empty family while
        // the read beside it resolves one — the stale-navigation-on-a-write-response shape, which
        // has been found five times in this module and never once by reading the code.
        var reloaded = await repo.GetQueryable()
            .Include(x => x.Country)
            .Include(x => x.FamilyMembers)
            .FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDetailDto();
    }

    // ── Expatriate family members ────────────────────────────────────────────
    //
    // ⚠ `FamilyAccompanying` was a bare bool: the record could assert a family had come and never
    // say who. Each accompanying person needs their own residence permit on their own clock.

    public async Task<IEnumerable<ExpatriateFamilyMemberDto>> GetExpatriateFamilyMembersAsync(
        Guid assignmentId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var items = await _unitOfWork.Repository<ExpatriateFamilyMember>().GetQueryable()
            .Where(m => m.ExpatriateAssignmentId == assignmentId && m.TenantId == tenantId)
            .OrderBy(m => m.FullName)
            .ToListAsync(cancellationToken);
        return items.Select(m => m.ToDto()).ToList();
    }

    public async Task<ExpatriateFamilyMemberDto> AddExpatriateFamilyMemberAsync(
        CreateExpatriateFamilyMemberDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();

        var assignment = await _unitOfWork.Repository<ExpatriateAssignment>().GetQueryable()
            .FirstOrDefaultAsync(a => a.Id == dto.ExpatriateAssignmentId && a.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException("Expatriate assignment not found.");

        var entity = dto.ToEntity();
        // ToEntity() does not stamp the tenant and the DbContext auto-stamp is inert.
        entity.TenantId = tenantId;

        await _unitOfWork.Repository<ExpatriateFamilyMember>().AddAsync(entity);

        // Recording a family member IS the statement that family accompanied them, so the flag
        // follows the facts rather than waiting for somebody to tick it separately and disagree.
        if (!assignment.FamilyAccompanying)
        {
            assignment.FamilyAccompanying = true;
            await _unitOfWork.Repository<ExpatriateAssignment>().UpdateAsync(assignment);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<ExpatriateFamilyMemberDto> UpdateExpatriateFamilyMemberAsync(
        UpdateExpatriateFamilyMemberDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var tenantId = GetTenantId();
        var repo = _unitOfWork.Repository<ExpatriateFamilyMember>();
        var entity = await repo.GetQueryable()
            .FirstOrDefaultAsync(m => m.Id == dto.Id && m.TenantId == tenantId, cancellationToken)
            ?? throw new ArgumentException("Family member not found.");

        dto.Apply(entity);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<bool> RemoveExpatriateFamilyMemberAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var repo = _unitOfWork.Repository<ExpatriateFamilyMember>();
        var entity = await repo.GetQueryable()
            .FirstOrDefaultAsync(m => m.Id == id && m.TenantId == tenantId, cancellationToken);
        if (entity == null) return false;

        // ⚠ The FamilyAccompanying flag is NOT cleared when the last member is removed. Somebody
        // whose family went home still travelled with one, and the assignment record should keep
        // saying so; the members list is what answers "who is here now".
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveExpatriateAssignmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<ExpatriateAssignment>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<EmployeePositionHistoryListDto>> GetPositionHistoryAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeePositionHistory>();
        var items = await repo.GetQueryable()
            .Include(x => x.Position)
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.StartDate)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            var employee = await _employeeRepository.GetByIdAsync(employeeId);
            if (employee != null)
            {
                await EnsureInitialPositionHistoryExistsAsync(employee, cancellationToken);

                items = await repo.GetQueryable()
                    .Include(x => x.Position)
                    .Where(x => x.EmployeeId == employeeId)
                    .OrderByDescending(x => x.StartDate)
                    .ToListAsync(cancellationToken);
            }
        }

        return items.Select(x => x.ToListDto());
    }

    public async Task<EmployeePositionHistoryDetailDto?> GetPositionHistoryByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeePositionHistory>();
        var entity = await repo.GetQueryable().Include(x => x.Position).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity?.ToDetailDto();
    }

    public async Task<EmployeePositionHistoryDetailDto> AddPositionHistoryAsync(CreateEmployeePositionHistoryDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);

        var repo = _unitOfWork.Repository<EmployeePositionHistory>();
        var entity = dto.ToEntity();
        // ToEntity() does not stamp the tenant, and the DbContext auto-stamp is inert, so an
        // unstamped row inserts TenantId = Guid.Empty and trips the Tenants FK.
        entity.TenantId = GetTenantId();

        // close current history if overlapping
        var current = await repo.FirstOrDefaultAsync(x => x.EmployeeId == dto.EmployeeId && x.EndDate == null);
        if (current != null && current.StartDate <= entity.StartDate)
        {
            current.EndDate = entity.StartDate.AddSeconds(-1);
            await repo.UpdateAsync(current);
        }

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(x => x.Position).FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDetailDto();
    }

    public async Task<EmployeePositionHistoryDetailDto> UpdatePositionHistoryAsync(UpdateEmployeePositionHistoryDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var repo = _unitOfWork.Repository<EmployeePositionHistory>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException("Position history not found.");

        dto.Apply(entity);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(x => x.Position).FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDetailDto();
    }

    public async Task<bool> RemovePositionHistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeePositionHistory>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<EmployeeSalaryAssignmentListDto>> GetSalaryAssignmentsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeSalaryAssignment>();
        var items = await repo.GetQueryable()
            .Include(x => x.Grade)
            .Include(x => x.Level)
            .Include(x => x.Notch)
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.EffectiveDate)
            .ToListAsync(cancellationToken);
        return items.Select(x => x.ToListDto());
    }

    public async Task<EmployeeSalaryAssignmentDetailDto?> GetSalaryAssignmentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeSalaryAssignment>();
        var entity = await repo.GetQueryable()
            .Include(x => x.Grade)
            .Include(x => x.Level)
            .Include(x => x.Notch)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity?.ToDetailDto();
    }

    public async Task<EmployeeSalaryAssignmentDetailDto> AssignSalaryAsync(CreateEmployeeSalaryAssignmentDto dto, CancellationToken cancellationToken = default, SalaryChangeAuthority authority = SalaryChangeAuthority.Direct)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);
        await RequireDirectChangeAllowedAsync(authority, cancellationToken);
        await RequireOnPayrollAsync(dto.EmployeeId, cancellationToken);
        await RequireOnScaleAsync(dto.EmployeeId, cancellationToken);

        if (dto.EffectiveDate == default) throw new ArgumentException("EffectiveDate is required.");

        dto.LevelId = await ResolvePlacementLevelAsync(dto.GradeId, dto.LevelId, dto.NotchId, cancellationToken);

        var repo = _unitOfWork.Repository<EmployeeSalaryAssignment>();
        var entity = dto.ToEntity();
        // ToEntity() does not stamp the tenant, and the DbContext auto-stamp is inert, so an
        // unstamped row inserts TenantId = Guid.Empty and trips the Tenants FK.
        entity.TenantId = GetTenantId();

        // Prevent overlaps: end any placement the new one takes over from.
        var current = await repo.FirstOrDefaultAsync(a =>
            a.EmployeeId == dto.EmployeeId &&
            a.WithdrawnAt == null &&
            a.EffectiveDate <= dto.EffectiveDate &&
            (a.EffectiveTo == null || a.EffectiveTo >= dto.EffectiveDate));

        if (current != null)
        {
            // ⚠ The two placements can share an effective date — re-placing somebody on the same
            // day to correct a mistake is the ordinary way that happens. Ending the old one "the
            // day before the new one" would then close it before it began. It never took effect,
            // so it is withdrawn rather than closed, for the same reason as above.
            if (current.EffectiveDate >= dto.EffectiveDate)
            {
                current.WithdrawnAt = DateTime.UtcNow;
                current.WithdrawnReason = $"Replaced by a placement effective {dto.EffectiveDate:yyyy-MM-dd}.";
            }
            else
            {
                current.EffectiveTo = dto.EffectiveDate.AddDays(-1);
            }
            await repo.UpdateAsync(current);
        }

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(x => x.Grade).Include(x => x.Level).Include(x => x.Notch).FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDetailDto();
    }

    public async Task<EmployeeSalaryAssignmentDetailDto> UpdateSalaryAssignmentAsync(UpdateEmployeeSalaryAssignmentDto dto, CancellationToken cancellationToken = default, SalaryChangeAuthority authority = SalaryChangeAuthority.Direct)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var repo = _unitOfWork.Repository<EmployeeSalaryAssignment>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException("Salary assignment not found.");
        await RequireDirectChangeAllowedAsync(authority, cancellationToken);
        await RequireOnPayrollAsync(entity.EmployeeId, cancellationToken);
        await RequireOnScaleAsync(entity.EmployeeId, cancellationToken);

        dto.LevelId = await ResolvePlacementLevelAsync(
            dto.GradeId ?? entity.GradeId, dto.LevelId, dto.NotchId, cancellationToken);

        dto.Apply(entity);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await repo.GetQueryable().Include(x => x.Grade).Include(x => x.Level).Include(x => x.Notch).FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        return (reloaded ?? entity).ToDetailDto();
    }

    public async Task<bool> RemoveSalaryAssignmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeSalaryAssignment>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IEnumerable<EmployeeRefereeListDto>> GetRefereesAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeReferee>();
        var items = await repo.FindAsync(x => x.EmployeeId == employeeId);
        return items.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.FullName).Select(x => x.ToListDto());
    }

    public async Task<EmployeeRefereeDetailDto?> GetRefereeByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeReferee>();
        var entity = await repo.GetByIdAsync(id);
        return entity?.ToDetailDto();
    }

    public async Task<EmployeeRefereeDetailDto> AddRefereeAsync(CreateEmployeeRefereeDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);

        var repo = _unitOfWork.Repository<EmployeeReferee>();
        var entity = dto.ToEntity();
        // ToEntity() does not stamp the tenant, and the DbContext auto-stamp is inert, so an
        // unstamped row inserts TenantId = Guid.Empty and trips the Tenants FK.
        entity.TenantId = GetTenantId();

        // ⚠ What is accepted depends on the KIND of referee. A personal referee may be a relative
        // or a family friend; a professional or academic one may not.
        entity.Relationship = await ResolveRelationshipAsync(
            entity.RelationshipTypeId, entity.Relationship, RefereeCategories(entity.RefereeType),
            $"a {entity.RefereeType.ToString().ToLowerInvariant()} referee", cancellationToken);

        if (entity.IsPrimary)
        {
            var others = await repo.FindAsync(x => x.EmployeeId == dto.EmployeeId && x.IsPrimary);
            foreach (var r in others)
            {
                r.IsPrimary = false;
                await repo.UpdateAsync(r);
            }
        }

        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDetailDto();
    }

    public async Task<EmployeeRefereeDetailDto> UpdateRefereeAsync(UpdateEmployeeRefereeDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var repo = _unitOfWork.Repository<EmployeeReferee>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException("Referee not found.");

        dto.Apply(entity);

        // ⚠ Read from the ENTITY, not the DTO: an edit that changes only the referee type has to
        // re-check the relationship already on the row against the new kind, and one that changes
        // only the relationship has to check it against the kind already stored.
        entity.Relationship = await ResolveRelationshipAsync(
            entity.RelationshipTypeId, entity.Relationship, RefereeCategories(entity.RefereeType),
            $"a {entity.RefereeType.ToString().ToLowerInvariant()} referee", cancellationToken);

        if (entity.IsPrimary)
        {
            var others = await repo.FindAsync(x => x.EmployeeId == entity.EmployeeId && x.Id != entity.Id && x.IsPrimary);
            foreach (var r in others)
            {
                r.IsPrimary = false;
                await repo.UpdateAsync(r);
            }
        }
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDetailDto();
    }

    public async Task<bool> RemoveRefereeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeReferee>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<EmployeeRefereeDetailDto> SetPrimaryRefereeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeReferee>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Referee not found.");

        var others = await repo.FindAsync(x => x.EmployeeId == entity.EmployeeId && x.Id != entity.Id && x.IsPrimary);
        foreach (var r in others)
        {
            r.IsPrimary = false;
            await repo.UpdateAsync(r);
        }
        entity.IsPrimary = true;
        entity.IsActive = true;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDetailDto();
    }

    public async Task<EmployeeRefereeDetailDto> ActivateRefereeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeReferee>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Referee not found.");
        entity.IsActive = true;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDetailDto();
    }

    public async Task<EmployeeRefereeDetailDto> DeactivateRefereeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeReferee>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Referee not found.");
        if (entity.IsPrimary) throw new InvalidOperationException("Primary referee cannot be deactivated.");
        entity.IsActive = false;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDetailDto();
    }

    // ⚠ Both guarantor reads INCLUDE the navigations the list DTO now renders — the national-ID
    // kind and the document count. A repository FindAsync leaves them unloaded and every row would
    // read "no type, no documents" while the detail beside it says otherwise.
    private IQueryable<EmployeeGuarantor> QueryGuarantors() =>
        _unitOfWork.Repository<EmployeeGuarantor>().GetQueryable()
            .Include(g => g.NationalIdTypeRef)
            .Include(g => g.Documents);

    public async Task<IEnumerable<EmployeeGuarantorListDto>> GetGuarantorsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var items = await QueryGuarantors()
            .AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        return items.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.LastName).Select(x => x.ToListDto());
    }

    public async Task<EmployeeGuarantorDetailDto?> GetGuarantorByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await QueryGuarantors()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        return entity?.ToDetailDto();
    }

    /// <summary>
    /// The national-ID kind must be one of the tenant's identification types, and active.
    /// </summary>
    /// <remarks>
    /// Checked here rather than left to the FK: SQL 547 surfaces as an unexplained 500 where
    /// "that identification type was not found" is the answer the screen can show.
    /// </remarks>
    private async Task RequireIdentificationTypeAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id is not Guid typeId) return;
        var tenantId = GetTenantId();
        var type = await _unitOfWork.Repository<IdentificationType>().GetQueryable()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == typeId && t.TenantId == tenantId && !t.IsDeleted, cancellationToken)
            ?? throw new ArgumentException($"Identification type '{typeId}' was not found.");
        if (!type.IsActive)
            throw new InvalidOperationException($"Identification type '{type.Name}' is inactive and cannot be chosen for a new record.");
    }

    public async Task<EmployeeGuarantorDetailDto> AddGuarantorAsync(CreateEmployeeGuarantorDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);
        // optional: a guarantor may be recorded with no amount, and then no currency either.
        await _currencies.RequireKnownCurrencyAsync(
            dto.AmountGuaranteedCurrencyCode, cancellationToken, optional: true);
        await RequireIdentificationTypeAsync(dto.NationalIdTypeId, cancellationToken);

        var repo = _unitOfWork.Repository<EmployeeGuarantor>();
        var entity = dto.ToEntity();
        // ToEntity() does not stamp the tenant, and the DbContext auto-stamp is inert, so an
        // unstamped row inserts TenantId = Guid.Empty and trips the Tenants FK.
        entity.TenantId = GetTenantId();

        // ⚠ All three categories: an employer, a brother and a landlord can each stand surety.
        entity.Relationship = await ResolveRelationshipAsync(
            entity.RelationshipTypeId, entity.Relationship, AnyRelationship,
            "a guarantor", cancellationToken);

        await ApplySubRecordGeoAsync(
            entity.GeoAreaId, () => entity.CountryId, c => entity.CountryId = c,
            r => entity.Region = r, c => entity.City = c,
            "guarantor", entity.Id, cancellationToken);

        if (entity.IsPrimary)
        {
            var others = await repo.FindAsync(x => x.EmployeeId == dto.EmployeeId && x.IsPrimary);
            foreach (var g in others)
            {
                g.IsPrimary = false;
                await repo.UpdateAsync(g);
            }
        }
        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Re-read rather than mapping the tracked instance: a just-added row has no navigation
        // loaded, so the create response would carry a blank type name beside a list that has it.
        return (await GetGuarantorByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<EmployeeGuarantorDetailDto> UpdateGuarantorAsync(UpdateEmployeeGuarantorDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await _currencies.RequireKnownCurrencyAsync(
            dto.AmountGuaranteedCurrencyCode, cancellationToken, optional: true);
        await RequireIdentificationTypeAsync(dto.NationalIdTypeId, cancellationToken);
        var repo = _unitOfWork.Repository<EmployeeGuarantor>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException("Guarantor not found.");

        dto.Apply(entity);

        entity.Relationship = await ResolveRelationshipAsync(
            entity.RelationshipTypeId, entity.Relationship, AnyRelationship,
            "a guarantor", cancellationToken);

        // ⚠ AFTER Apply — the tree wins over whatever spelling the payload sent.
        await ApplySubRecordGeoAsync(
            entity.GeoAreaId, () => entity.CountryId, c => entity.CountryId = c,
            r => entity.Region = r, c => entity.City = c,
            "guarantor", entity.Id, cancellationToken);

        if (entity.IsPrimary)
        {
            var others = await repo.FindAsync(x => x.EmployeeId == entity.EmployeeId && x.Id != entity.Id && x.IsPrimary);
            foreach (var g in others)
            {
                g.IsPrimary = false;
                await repo.UpdateAsync(g);
            }
        }
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return (await GetGuarantorByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<bool> RemoveGuarantorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeGuarantor>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) return false;

        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<EmployeeGuarantorDetailDto> SetPrimaryGuarantorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeGuarantor>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Guarantor not found.");

        var others = await repo.FindAsync(x => x.EmployeeId == entity.EmployeeId && x.Id != entity.Id && x.IsPrimary);
        foreach (var g in others)
        {
            g.IsPrimary = false;
            await repo.UpdateAsync(g);
        }
        entity.IsPrimary = true;
        entity.IsActive = true;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDetailDto();
    }

    public async Task<EmployeeGuarantorDetailDto> VerifyGuarantorAsync(Guid id, Guid verifiedByEmployeeId, DateTime verifiedDate, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeGuarantor>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Guarantor not found.");

        // Ensure verifier exists
        await EnsureEmployeeExistsAsync(verifiedByEmployeeId);

        entity.IsVerified = true;
        entity.VerificationDate = verifiedDate;
        entity.VerifiedByEmployeeId = verifiedByEmployeeId;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDetailDto();
    }

    public async Task<EmployeeGuarantorDetailDto> UnverifyGuarantorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeGuarantor>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Guarantor not found.");

        entity.IsVerified = false;
        entity.VerificationDate = null;
        entity.VerifiedByEmployeeId = null;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return entity.ToDetailDto();
    }

    public async Task<EmployeeGuarantorDetailDto> ActivateGuarantorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeGuarantor>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Guarantor not found.");
        entity.IsActive = true;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDetailDto();
    }

    public async Task<EmployeeGuarantorDetailDto> DeactivateGuarantorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeGuarantor>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Guarantor not found.");
        if (entity.IsPrimary) throw new InvalidOperationException("Primary guarantor cannot be deactivated.");
        entity.IsActive = false;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDetailDto();
    }

    // ── Bank Details ─────────────────────────────────────────────────────────

    // ⚠ Both bank reads INCLUDE the bank and branch. The mapper reads `e.Bank?.Name ?? e.BankName`,
    // and with the navigations unloaded it fell through to the free-text column on every row —
    // which is why linking a catalogue bank had no visible effect before round 2.
    private IQueryable<EmployeeBankDetail> QueryBankDetails() =>
        _unitOfWork.Repository<EmployeeBankDetail>().GetQueryable()
            .Include(b => b.Bank)
            .Include(b => b.Branch);

    public async Task<IEnumerable<EmployeeBankDetailDto>> GetBankDetailsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var items = await QueryBankDetails()
            .AsNoTracking()
            .Where(x => x.EmployeeId == employeeId && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        return items.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.Bank != null ? x.Bank.Name : x.BankName).Select(x => x.ToDto());
    }

    public async Task<EmployeeBankDetailDto?> GetBankDetailByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await QueryBankDetails()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        return entity?.ToDto();
    }

    /// <summary>
    /// Resolves the catalogue bank and branch a bank-account row names, refusing a branch that is
    /// not the bank's, and mirrors their names into the free-text columns.
    /// </summary>
    /// <remarks>
    /// <para>⚠ The branch-in-bank check is the one that matters (demo feedback round 2, E-14): the
    /// columns were being copied straight through, so a caller could pair GCB with an Ecobank
    /// branch and the row would say both. Mirroring the names means any read that forgets to
    /// Include still prints the catalogue name rather than whatever was typed.</para>
    /// <para>Either id may be absent — the free-text fallback stays for banks not in the list.
    /// A branch without its bank is refused, since it cannot be checked against anything.</para>
    /// </remarks>
    private async Task ResolveBankLinksAsync(EmployeeBankDetail entity, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();

        if (entity.BranchId.HasValue && !entity.BankId.HasValue)
            throw new ArgumentException("A branch was given without its bank. Choose the bank first.");

        if (entity.BankId is Guid bankId)
        {
            var bank = await _unitOfWork.Repository<EmployeeBank>().GetQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == bankId && b.TenantId == tenantId && !b.IsDeleted, cancellationToken)
                ?? throw new ArgumentException($"Bank '{bankId}' was not found.");
            if (!bank.IsActive)
                throw new InvalidOperationException($"Bank '{bank.Name}' is inactive and cannot be chosen for a new account.");
            entity.BankName = bank.Name;
        }

        if (entity.BranchId is Guid branchId)
        {
            var branch = await _unitOfWork.Repository<EmployeeBankBranch>().GetQueryable()
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == branchId && b.TenantId == tenantId && !b.IsDeleted, cancellationToken)
                ?? throw new ArgumentException($"Bank branch '{branchId}' was not found.");
            if (branch.BankId != entity.BankId)
                throw new InvalidOperationException($"Branch '{branch.Name}' does not belong to the chosen bank.");
            if (!branch.IsActive)
                throw new InvalidOperationException($"Branch '{branch.Name}' is inactive and cannot be chosen for a new account.");
            entity.BranchName = branch.Name;
        }

        if (string.IsNullOrWhiteSpace(entity.BankName))
            throw new ArgumentException("Choose a bank from the list, or type its name.");
    }

    public async Task<EmployeeBankDetailDto> AddBankDetailAsync(CreateEmployeeBankDetailDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureEmployeeExistsAsync(dto.EmployeeId);

        var repo = _unitOfWork.Repository<EmployeeBankDetail>();

        // Enforce single primary: if new record is primary, clear others
        if (dto.IsPrimary)
        {
            var existing = await repo.FindAsync(x => x.EmployeeId == dto.EmployeeId && x.IsPrimary);
            foreach (var e in existing)
            {
                e.IsPrimary = false;
                await repo.UpdateAsync(e);
            }
        }

        var entity = dto.ToEntity();
        // ToEntity() does not stamp the tenant, and the DbContext auto-stamp is inert, so an
        // unstamped row inserts TenantId = Guid.Empty and trips the Tenants FK.
        entity.TenantId = GetTenantId();
        await ResolveBankLinksAsync(entity, cancellationToken);
        await repo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (await GetBankDetailByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<EmployeeBankDetailDto> UpdateBankDetailAsync(UpdateEmployeeBankDetailDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var repo = _unitOfWork.Repository<EmployeeBankDetail>();
        var entity = await repo.GetByIdAsync(dto.Id);
        if (entity == null) throw new ArgumentException("Bank detail not found.");

        // Enforce single primary: if patching to primary, clear others first
        if (dto.IsPrimary == true && !entity.IsPrimary)
        {
            var others = await repo.FindAsync(x => x.EmployeeId == entity.EmployeeId && x.IsPrimary && x.Id != entity.Id);
            foreach (var o in others)
            {
                o.IsPrimary = false;
                await repo.UpdateAsync(o);
            }
        }

        dto.Apply(entity);
        // A bank change without a branch change would leave the old branch under the new bank;
        // resolving after Apply catches that pairing the same way it catches it on create.
        await ResolveBankLinksAsync(entity, cancellationToken);
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return (await GetBankDetailByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<bool> RemoveBankDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeBankDetail>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) return false;
        if (entity.IsPrimary) throw new InvalidOperationException("Cannot remove the primary bank account. Set another account as primary first.");
        await repo.DeleteAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<EmployeeBankDetailDto> SetPrimaryBankDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeBankDetail>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Bank detail not found.");

        var others = await repo.FindAsync(x => x.EmployeeId == entity.EmployeeId && x.Id != entity.Id && x.IsPrimary);
        foreach (var o in others)
        {
            o.IsPrimary = false;
            await repo.UpdateAsync(o);
        }
        entity.IsPrimary = true;
        entity.IsActive = true;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<EmployeeBankDetailDto> VerifyBankDetailAsync(Guid id, Guid verifiedByEmployeeId, DateTime verifiedDate, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeBankDetail>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Bank detail not found.");

        await EnsureEmployeeExistsAsync(verifiedByEmployeeId);

        entity.IsVerified  = true;
        entity.VerifiedDate = verifiedDate;
        entity.VerifiedById = verifiedByEmployeeId;

        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<EmployeeBankDetailDto> UnverifyBankDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeBankDetail>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Bank detail not found.");
        entity.IsVerified   = false;
        entity.VerifiedDate = null;
        entity.VerifiedById = null;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<EmployeeBankDetailDto> ActivateBankDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeBankDetail>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Bank detail not found.");
        entity.IsActive = true;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    public async Task<EmployeeBankDetailDto> DeactivateBankDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeBankDetail>();
        var entity = await repo.GetByIdAsync(id);
        if (entity == null) throw new ArgumentException("Bank detail not found.");
        if (entity.IsPrimary) throw new InvalidOperationException("Primary bank account cannot be deactivated.");
        entity.IsActive = false;
        await repo.UpdateAsync(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    #endregion

    #region 4) Manager & Hierarchy

    public async Task<EmployeeDetailDto> AssignManagerAsync(Guid employeeId, Guid managerId, CancellationToken cancellationToken = default)
    {
        await ValidateManagerAssignmentAsync(employeeId, managerId, cancellationToken);

        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null) throw new ArgumentException($"Employee '{employeeId}' not found.");

        employee.ManagerId = managerId;

        await _employeeRepository.UpdateAsync(employee);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _employeeRepository.GetByIdWithDetailsAsync(employeeId);
        if (updated == null) throw new InvalidOperationException("Employee updated but could not be reloaded.");
        return updated.ToDetailDto();
    }

    public async Task<EmployeeDetailDto> RemoveManagerAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null) throw new ArgumentException($"Employee '{employeeId}' not found.");

        employee.ManagerId = null;

        await _employeeRepository.UpdateAsync(employee);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _employeeRepository.GetByIdWithDetailsAsync(employeeId);
        if (updated == null) throw new InvalidOperationException("Employee updated but could not be reloaded.");
        return updated.ToDetailDto();
    }

    public async Task<IEnumerable<EmployeeDto>> GetDirectReportsAsync(Guid managerId, CancellationToken cancellationToken = default)
        => (await _employeeRepository.GetByManagerAsync(managerId)).Select(e => e.ToSummaryDto());

    public async Task<IEnumerable<EmployeeDto>> GetManagementChainAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var chain = new List<Employee>();
        var visited = new HashSet<Guid>();

        var current = await _employeeRepository.GetByIdAsync(employeeId);
        if (current == null) throw new ArgumentException($"Employee '{employeeId}' not found.");

        var managerId = current.ManagerId;
        while (managerId.HasValue)
        {
            if (!visited.Add(managerId.Value))
                throw new InvalidOperationException("Circular reporting detected while building management chain.");

            var manager = await _employeeRepository.GetByIdAsync(managerId.Value, e => e.Position, e => e.OrganizationUnit!, e => e.OrganizationLevel!);
            if (manager == null) break;
            chain.Add(manager);
            managerId = manager.ManagerId;
        }

        return chain.Select(e => e.ToSummaryDto());
    }

    public async Task<bool> WouldCreateCircularReportingAsync(Guid employeeId, Guid proposedManagerId, CancellationToken cancellationToken = default)
    {
        if (employeeId == proposedManagerId) return true;

        var visited = new HashSet<Guid> { employeeId };
        var current = proposedManagerId;

        while (true)
        {
            if (!visited.Add(current)) return true;
            var manager = await _employeeRepository.GetByIdAsync(current);
            if (manager == null || !manager.ManagerId.HasValue) return false;
            current = manager.ManagerId.Value;
        }
    }

    #endregion

    #region 5) Payroll, Tax & Contract Logic

    // ── Payroll membership ─────────────────────────────────────────────────────────────────
    //
    // "On payroll" gates the salary, the five payroll switches and the grade/notch assignment. The
    // gate is refusal, not silent dropping: a caller that says "off payroll" and sends a salary is
    // told so, because dropping the figure would let the form and the record disagree without
    // anyone noticing. The mirror rule — on payroll REQUIRES a salary — is deliberately NOT
    // enforced here: the hire path, imports and every fixture create employees before their pay
    // basis is known, and the run-time consequence (a run skips a zero basis silently) is what the
    // reconciliation read exists to catch as NoPayBasis.

    private static void ValidatePayrollMembership(
        bool isOnPayroll, OffPayrollReason? reason, decimal? salary, bool anySwitchOn)
    {
        if (isOnPayroll) return;

        if (reason == null)
            throw new InvalidOperationException(
                "An employee who is not on payroll needs a reason (paid by invoice, allowance, parent organisation, unpaid, board or committee, other).");
        if (!Enum.IsDefined(reason.Value))
            throw new ArgumentException("That is not a recognised off-payroll reason.");
        if (salary is > 0)
            throw new InvalidOperationException(
                "This employee is not on payroll, so a salary cannot be recorded. Put them on payroll first, or leave the salary blank.");
        if (anySwitchOn)
            throw new InvalidOperationException(
                "This employee is not on payroll, so the payroll switches (pay tax, SS fund, gross up, tier 2 only, overtime) do not apply.");
    }

    /// <summary>
    /// Applies the membership fields the mapping deliberately leaves alone, and the consequences of
    /// a flip. Off: the pay figures are cleared and any open grade/notch assignment is closed as of
    /// today (an "active" placement on a grade for somebody the run does not pay is a contradiction
    /// the salary tab would otherwise display). The figures survive in position history, movement
    /// history and the contract line, so nothing is lost that a letter or a settlement needs. On:
    /// the reason is cleared; enrolment in payroll happens after the save.
    /// </summary>
    private async Task ApplyPayrollMembershipAsync(
        Employee employee, UpdateEmployeeDto dto, bool wasOnPayroll, bool willBeOnPayroll, CancellationToken cancellationToken)
    {
        employee.IsOnPayroll = willBeOnPayroll;

        if (willBeOnPayroll)
        {
            employee.OffPayrollReason = null;
            employee.OffPayrollNote = null;
            return;
        }

        if (dto.OffPayrollReason.HasValue) employee.OffPayrollReason = dto.OffPayrollReason;
        if (dto.OffPayrollNote != null)
            employee.OffPayrollNote = string.IsNullOrWhiteSpace(dto.OffPayrollNote) ? null : dto.OffPayrollNote.Trim();

        employee.Salary = null;
        employee.PayTax = false;
        employee.SSFund = false;
        employee.GrossUp = false;
        employee.Tier2Only = false;
        employee.Overtime = false;

        if (!wasOnPayroll) return;

        await CloseOpenSalaryAssignmentsAsync(employee, "taken off payroll", cancellationToken);
    }

    /// <summary>
    /// Withdraws every grade placement that is still standing, because the notch has stopped being
    /// this person's pay. Withdraw and close, never delete: the row is the record.
    /// </summary>
    /// <remarks>
    /// <para>Shared by the off-payroll flip and the switch to negotiated pay — one rule for "the
    /// notch is no longer this person's pay", whichever fact changed.</para>
    ///
    /// <para><b>⚠ The end date is written ONLY for a placement that had actually taken effect.</b>
    /// This used to clamp <c>EffectiveTo</c> to the row's own start date when yesterday would have
    /// been earlier, to keep the window from going negative — which quietly turned a withdrawn
    /// FUTURE placement into a one-day window on its start date. A promotion booked for 1 October
    /// and withdrawn on 15 September came back to life on 1 October and was read as basic pay for
    /// the day. The window now says only what it means — when these terms were in force — and
    /// withdrawal is recorded as the separate act it is.</para>
    /// </remarks>
    private async Task CloseOpenSalaryAssignmentsAsync(Employee employee, string because, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);
        var repo = _unitOfWork.Repository<EmployeeSalaryAssignment>();

        var standing = await repo.GetQueryable()
            .Where(a => a.EmployeeId == employee.Id && !a.IsDeleted
                     && a.WithdrawnAt == null
                     && (a.EffectiveTo == null || a.EffectiveTo >= today))
            .ToListAsync(cancellationToken);

        foreach (var assignment in standing)
        {
            assignment.WithdrawnAt = DateTime.UtcNow;
            assignment.WithdrawnReason = because;

            // Took effect at some point before today, so it genuinely ran until yesterday. One that
            // starts today or later never ran at all and gets no window — WithdrawnAt is what keeps
            // it out of every as-of read.
            if (assignment.EffectiveDate <= yesterday)
                assignment.EffectiveTo = yesterday;

            await repo.UpdateAsync(assignment);
        }

        if (standing.Count > 0)
            _logger.LogInformation(
                "Withdrew {Count} salary placement(s) for {EmployeeId}: {Because}.", standing.Count, employee.Id, because);
    }

    /// <inheritdoc />
    public async Task<EmployeeDetailDto> SetPayBasisAsync(Guid employeeId, SetEmployeePayBasisDto dto, CancellationToken cancellationToken = default, SalaryChangeAuthority authority = SalaryChangeAuthority.Direct)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var employee = await _employeeRepository.GetByIdAsync(employeeId)
            ?? throw new ArgumentException($"Employee with ID '{employeeId}' not found.");
        if (employee.PayBasis != dto.PayBasis)
            await RequireDirectChangeAllowedAsync(authority, cancellationToken);

        var note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        if (dto.PayBasis == PayBasis.Negotiated && note == null)
            throw new InvalidOperationException(
                "A negotiated salary needs a note saying who agreed what, and when — otherwise the figure "
                + "has nothing behind it when it is questioned.");

        var wasOnScale = employee.PayBasis == PayBasis.SalaryScale;

        employee.PayBasis = dto.PayBasis;
        // The note is the negotiation's; back on the scale it would describe a basis that no
        // longer applies, so it goes with the basis it explains.
        employee.PayBasisNote = dto.PayBasis == PayBasis.Negotiated ? note : null;

        if (wasOnScale && dto.PayBasis == PayBasis.Negotiated)
            await CloseOpenSalaryAssignmentsAsync(employee, "pay basis changed to negotiated", cancellationToken);

        await _employeeRepository.UpdateAsync(employee);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var reloaded = await _employeeRepository.GetByIdWithDetailsAsync(employeeId);
        return (reloaded ?? employee).ToDetailDto();
    }

    /// <summary>
    /// Settles the level a placement sits on, so a caller never has to know whether the level tier
    /// is real or implicit.
    /// </summary>
    /// <remarks>
    /// <para>Lane G. A notch names its level, so a notch always wins and a level that disagrees with
    /// it is a contradiction, not a choice. With no notch: in a two-tier structure the grade's one
    /// implicit level is filled in, because the caller cannot see it and should not have to; in
    /// three-tier a supplied level is checked against the grade and an omitted one stays null — a
    /// placement on the grade alone is allowed, as it always was.</para>
    /// <para>Round 3, lane H: public, and on <see cref="IEmployeeService"/>. The staff-movement
    /// service calls it when a movement is RAISED (so a notch of another grade is refused before
    /// three approvals, not after) and then writes the placement through
    /// <see cref="AssignSalaryAsync"/> at implementation; the hire-from-offer path calls it to fill
    /// the level it used to leave as the offer set it. Every placement in the system now resolves
    /// its level here.</para>
    /// </remarks>
    public async Task<Guid?> ResolvePlacementLevelAsync(
        Guid gradeId, Guid? levelId, Guid? notchId, CancellationToken cancellationToken = default)
    {
        if (notchId is { } nId)
        {
            var notch = await _unitOfWork.Repository<SalaryNotch>().GetQueryable()
                .Include(n => n.Level)
                .FirstOrDefaultAsync(n => n.Id == nId && !n.IsDeleted, cancellationToken)
                ?? throw new ArgumentException("Salary notch not found.");

            if (notch.Level.SalaryGradeId != gradeId)
                throw new InvalidOperationException(
                    $"Notch {notch.NotchNumber} belongs to a different grade from the one chosen. Pick a notch of that grade.");

            if (levelId.HasValue && levelId.Value != notch.SalaryLevelId)
                throw new InvalidOperationException(
                    $"Notch {notch.NotchNumber} sits on level {notch.Level.Code}, not on the level chosen. A notch names its level; pick one or the other.");

            return notch.SalaryLevelId;
        }

        var levels = await _unitOfWork.Repository<SalaryLevel>().GetQueryable()
            .Where(l => l.SalaryGradeId == gradeId && !l.IsDeleted && l.IsActive)
            .Select(l => new { l.Id, l.Code })
            .ToListAsync(cancellationToken);

        if (levelId is { } lId)
        {
            if (levels.All(l => l.Id != lId))
                throw new InvalidOperationException("The level chosen does not belong to the grade chosen.");
            return lId;
        }

        var settings = await _policySettings.GetAsync(cancellationToken);
        if (settings.SalaryStructureTiers == SalaryStructureTiers.GradeAndNotch && levels.Count == 1)
            return levels[0].Id;

        return null;
    }

    /// <summary>
    /// Round 3, lane S. When the tenant requires approval for pay changes, a caller at the door is
    /// refused and pointed at the salary change request; a record the engine has already approved
    /// (the request itself when applied, a staff movement) passes <see cref="SalaryChangeAuthority.Approved"/>.
    /// One implementation, several callers — the rule cannot drift between the doors.
    /// </summary>
    private async Task RequireDirectChangeAllowedAsync(SalaryChangeAuthority authority, CancellationToken cancellationToken)
    {
        if (authority == SalaryChangeAuthority.Approved) return;
        var settings = await _policySettings.GetAsync(cancellationToken);
        if (settings.SalaryChangeRequiresApproval)
            throw new InvalidOperationException(
                "On this tenant a change of pay goes through an approved salary change request. Raise one from the Salary tab; it is applied when approved.");
    }

    /// <summary>The scale gate: a placement on the grade structure contradicts a negotiated salary.</summary>
    /// <remarks>
    /// ⚠ 400, not the 409 the plan wrote (§ 6.5.2), and on purpose: the off-payroll refusal on the
    /// SAME endpoint — the same shape of rule, "cannot be placed on a grade because X" — is a 400
    /// through <c>ToClientError</c>. Two refusals of one kind at two codes on one door would be a
    /// worse outcome than the plan's number. Recorded in the plan's lane E1 block.
    /// </remarks>
    private async Task RequireOnScaleAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId)
            ?? throw new ArgumentException($"Employee with ID '{employeeId}' not found.");
        if (employee.PayBasis == PayBasis.Negotiated)
            throw new InvalidOperationException(
                $"{employee.EmployeeNumber} is paid a negotiated amount, so they cannot be placed on a salary grade. "
                + "Change their pay basis to the salary scale first.");
    }

    /// <summary>The grade/notch gate: a placement on the pay structure is meaningless for someone the run does not pay.</summary>
    private async Task RequireOnPayrollAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId)
            ?? throw new ArgumentException($"Employee with ID '{employeeId}' not found.");
        if (!employee.IsOnPayroll)
            throw new InvalidOperationException(
                $"{employee.EmployeeNumber} is not on payroll, so they cannot be placed on a salary grade. Put them on payroll first.");
    }

    public Task ValidatePayrollFlagsAsync(bool payTax, bool ssFund, bool grossUp, bool tier2Only, bool overtime, CancellationToken cancellationToken = default)
    {
        // Coherence rules (policy-level; can be expanded safely without changing persistence):
        // - Gross-up only makes sense if PAYE is enabled (otherwise nothing to gross-up)
        // - Tier2Only implies SSFund (pension) is enabled
        if (!payTax && grossUp)
            throw new InvalidOperationException("GrossUp requires PayTax to be enabled.");

        if (tier2Only && !ssFund)
            throw new InvalidOperationException("Tier2Only requires SSFund to be enabled.");

        _ = overtime; // reserved for future validations / payroll calendar checks
        return Task.CompletedTask;
    }

    #endregion

    #region 6) Validation & Business Rules

    public async Task<bool> IsEmployeeNumberUniqueAsync(string employeeNumber, Guid? excludeEmployeeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(employeeNumber)) return false;
        var num = employeeNumber.Trim();

        return excludeEmployeeId.HasValue
            ? !await _employeeRepository.EmployeeNumberExistsAsync(num, excludeEmployeeId.Value)
            : !await _employeeRepository.EmployeeNumberExistsAsync(num);
    }

    public async Task<bool> IsEmailUniqueAsync(string? email, Guid? excludeEmployeeId = null, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeEmail(email);
        // No email is not a duplicate of anything (was `false`, which refused every emailless create).
        if (normalized == null) return true;

        return excludeEmployeeId.HasValue
            ? !await _employeeRepository.EmailExistsAsync(normalized, excludeEmployeeId.Value)
            : !await _employeeRepository.EmailExistsAsync(normalized);
    }

    public async Task<bool> IsBadgeNumberUniqueAsync(string badgeNumber, Guid? excludeEmployeeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(badgeNumber)) return true;
        var value = badgeNumber.Trim();
        return excludeEmployeeId.HasValue
            ? !await _employeeRepository.BadgeNumberExistsAsync(value, excludeEmployeeId.Value)
            : !await _employeeRepository.BadgeNumberExistsAsync(value);
    }

    public async Task<bool> IsTaxNumberUniqueAsync(string taxNumber, Guid? excludeEmployeeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taxNumber)) return true;
        var value = taxNumber.Trim();
        return excludeEmployeeId.HasValue
            ? !await _employeeRepository.TaxNumberExistsAsync(value, excludeEmployeeId.Value)
            : !await _employeeRepository.TaxNumberExistsAsync(value);
    }

    public async Task<bool> IsSocialSecurityNumberUniqueAsync(string socialSecurityNumber, Guid? excludeEmployeeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(socialSecurityNumber)) return true;
        var value = socialSecurityNumber.Trim();
        return excludeEmployeeId.HasValue
            ? !await _employeeRepository.SocialSecurityNumberExistsAsync(value, excludeEmployeeId.Value)
            : !await _employeeRepository.SocialSecurityNumberExistsAsync(value);
    }

    public async Task<bool> IsTinNumberUniqueAsync(string tinNumber, Guid? excludeEmployeeId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tinNumber)) return true;
        var value = tinNumber.Trim();
        return excludeEmployeeId.HasValue
            ? !await _employeeRepository.TinNumberExistsAsync(value, excludeEmployeeId.Value)
            : !await _employeeRepository.TinNumberExistsAsync(value);
    }

    public async Task<bool> HasActiveContractAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeContractDetail>();
        return await repo.ExistsAsync(x => x.EmployeeId == employeeId && x.IsActive);
    }

    public async Task<bool> HasActiveDependentsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeDependent>();
        return await repo.ExistsAsync(x => x.EmployeeId == employeeId);
    }

    public async Task<bool> HasActiveGuarantorsAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var repo = _unitOfWork.Repository<EmployeeGuarantor>();
        return await repo.ExistsAsync(x => x.EmployeeId == employeeId && x.IsActive);
    }

    public async Task<bool> IsEligibleForExpatriateAssignmentAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null) return false;

        // Simple baseline eligibility: active employee, not terminated
        return employee.IsActive && employee.StaffStatus != StaffStatus.Terminated;
    }

    /// <summary>
    /// Whether this employee could be terminated at all — not whether a particular caller may do it.
    /// </summary>
    /// <remarks>
    /// ⚠ This used to return <c>true</c> after re-checking only "already terminated", under a
    /// comment reading <i>"no active guarantor verification pending? (simplified)"</i> — a check
    /// that had never been written. It now answers the question it is named for. The gate that
    /// matters most, FR-HR-091's clearance, lives in <c>TerminateEmployeeAsync</c> as a refusal
    /// naming the separation in flight, because a bare false here could not say why.
    /// </remarks>
    public async Task<bool> CanTerminateEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null) return false;
        if (employee.StaffStatus == StaffStatus.Terminated) return false;
        if (employee.IsDeleted) return false;

        return true;
    }

    /// <summary>
    /// Applies a completed separation to the employee's master record — the step that was missing
    /// entirely before area 9b.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>This exists because of a measured defect.</b> On 2026-08-20 the live tenant held
    /// <b>29 disciplinary terminations whose employees were all still <c>StaffStatus = Active</c></b>:
    /// the outcome was recorded somewhere nobody read, so dismissed people stayed in headcount, in
    /// establishment counts and on every roster. Recording an exit and applying it are two different
    /// acts, and only one of them had ever been built.</para>
    ///
    /// <para>Separate from <see cref="TerminateEmployeeAsync"/> on purpose. That path now refuses
    /// when a separation is in flight, because using it would bypass clearance, approval and the
    /// settlement review. This one <i>is</i> the separation completing, so it carries the
    /// separation's id and skips that check — the one legitimate way through.</para>
    /// </remarks>
    public async Task<EmployeeDetailDto> ApplySeparationOutcomeAsync(
        Guid employeeId,
        Guid separationId,
        DateTime effectiveDate,
        TerminationReason? reason,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId)
            ?? throw new ArgumentException($"Employee with ID '{employeeId}' not found.");

        if (employee.StaffStatus == StaffStatus.Terminated)
            throw new InvalidOperationException(
                $"{employee.FirstName} {employee.LastName} is already recorded as terminated.");

        employee.StaffStatus = StaffStatus.Terminated;
        employee.IsActive = false;
        employee.TerminationDate = effectiveDate;
        employee.TerminationReason = reason;
        employee.TerminationNotes = notes;

        // Close every active contract, as the direct path does.
        var contractRepo = _unitOfWork.Repository<EmployeeContractDetail>();
        var activeContracts = await contractRepo.FindAsync(
            c => c.EmployeeId == employeeId && c.IsActive && !c.IsDeleted);

        foreach (var contract in activeContracts)
        {
            contract.IsActive = false;
            contract.IsCurrent = false;
            contract.ContractStatus = ContractStatus.Terminated;
            contract.TerminationDate = DateOnly.FromDateTime(effectiveDate);
            contract.TerminationReason = reason?.ToString();
            contract.EndDate ??= DateOnly.FromDateTime(effectiveDate);
            await contractRepo.UpdateAsync(contract);
        }

        // Close the open position-history row, so the establishment counts area 17/18 made
        // load-bearing stop counting somebody who has left.
        var posHistoryRepo = _unitOfWork.Repository<EmployeePositionHistory>();
        var current = await posHistoryRepo.FirstOrDefaultAsync(
            ph => ph.EmployeeId == employeeId && (ph.EndDate == null || ph.EndDate > effectiveDate));

        if (current != null)
        {
            current.EndDate = effectiveDate;
            current.ChangeReason = PositionChangeReason.Termination;
            await posHistoryRepo.UpdateAsync(current);
        }

        // G-3.2, the same log as the direct path. This is the route most real exits take — a
        // separation completing — so without it the register would have stayed empty even after
        // the direct path was wired.
        await PositionVacancyLog.LogDepartureAsync(
            _unitOfWork,
            employee.TenantId,
            employee.Id,
            employee.PositionId,
            VacancyReasonFor(reason),
            effectiveDate,
            _currentUserProvider.UserId,
            note: notes,
            cancellationToken: cancellationToken);

        await _employeeRepository.UpdateAsync(employee);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Separation {SeparationId} applied to employee {EmployeeId}: terminated {Date:yyyy-MM-dd}",
            separationId, employeeId, effectiveDate);

        var updated = await _employeeRepository.GetByIdWithDetailsAsync(employeeId)
            ?? throw new InvalidOperationException("Employee updated but could not be reloaded.");
        return updated.ToDetailDto();
    }

    #endregion

    #region Legacy / Backward compatibility

    public Task<EmployeeDto?> GetByIdAsync(Guid id) => GetEmployeeSummaryByIdAsync(id);
    public Task<EmployeeDetailDto?> GetDetailsByIdAsync(Guid id) => GetEmployeeDetailsByIdAsync(id);
    public Task<EmployeeDto?> GetByEmployeeNumberAsync(string employeeNumber) => GetEmployeeSummaryByEmployeeNumberAsync(employeeNumber);

    public async Task<IEnumerable<EmployeeDto>> GetAllAsync()
    {
        var employees = await _employeeRepository.GetAllAsync(e => e.Position, e => e.OrganizationUnit!, e => e.OrganizationLevel!);
        return employees.Select(e => e.ToSummaryDto());
    }

    public async Task<IEnumerable<EmployeeDto>> GetActiveEmployeesAsync()
        => (await _employeeRepository.GetActiveEmployeesAsync()).Select(e => e.ToSummaryDto());

    public async Task<IEnumerable<EmployeeDto>> SearchEmployeesAsync(EmployeeSearchDto searchCriteria)
        => (await GetEmployeesPagedAsync(searchCriteria, 1, 200)).Items;

    public async Task<EmployeeDto> CreateEmployeeAsync(CreateEmployeeDto createDto)
    {
        var detail = await CreateEmployeeAsync(createDto, CancellationToken.None);
        var summary = await GetEmployeeSummaryByIdAsync(detail.Id, CancellationToken.None);
        return summary ?? ToSummaryFromDetail(detail);
    }

    public async Task<EmployeeDto> UpdateEmployeeAsync(Guid id, UpdateEmployeeDto updateDto)
    {
        var detail = await UpdateEmployeeAsync(id, updateDto, CancellationToken.None);
        var summary = await GetEmployeeSummaryByIdAsync(detail.Id, CancellationToken.None);
        return summary ?? ToSummaryFromDetail(detail);
    }

    public Task<bool> DeleteEmployeeAsync(Guid id) => DeleteEmployeeAsync(id, CancellationToken.None);

    public async Task<bool> DeactivateEmployeeAsync(Guid id)
    {
        await DeactivateEmployeeAsync(id, CancellationToken.None);
        return true;
    }

    public async Task<bool> ActivateEmployeeAsync(Guid id)
    {
        await ActivateEmployeeAsync(id, CancellationToken.None);
        return true;
    }

    public async Task<bool> TerminateEmployeeAsync(Guid id, TerminateEmployeeDto dto)
    {
        await TerminateEmployeeAsync(id, dto, CancellationToken.None);
        return true;
    }

    public async Task<IEnumerable<EmployeeDto>> GetByDepartmentAsync(Guid departmentId)
        => (await _employeeRepository.GetByDepartmentAsync(departmentId)).Select(e => e.ToSummaryDto());

    public async Task<IEnumerable<EmployeeDto>> GetBySectionAsync(Guid sectionId)
        => (await _employeeRepository.GetBySectionAsync(sectionId)).Select(e => e.ToSummaryDto());

    public async Task<IEnumerable<EmployeeDto>> GetByPositionAsync(Guid positionId)
        => (await _employeeRepository.GetByPositionAsync(positionId)).Select(e => e.ToSummaryDto());

    public async Task<IEnumerable<EmployeeDto>> GetByStatusAsync(StaffStatus status)
        => (await _employeeRepository.GetByStatusAsync(status)).Select(e => e.ToSummaryDto());

    public async Task<IEnumerable<EmployeeDto>> GetByEmploymentTypeAsync(EmploymentType employmentType)
        => (await _employeeRepository.GetByEmploymentTypeAsync(employmentType)).Select(e => e.ToSummaryDto());

    // Maintenance integration left as existing (no aggregate writes)
    public async Task<IEnumerable<MaintenanceTechnicianDto>> GetMaintenanceTechniciansAsync()
    {
        var technicians = await _employeeRepository.GetMaintenanceTechniciansAsync();
        return technicians.Select(MapToMaintenanceTechnicianDto);
    }

    public async Task<IEnumerable<MaintenanceTechnicianDto>> GetAvailableTechniciansAsync()
    {
        var technicians = await _employeeRepository.GetAvailableTechniciansAsync();
        return technicians.Select(MapToMaintenanceTechnicianDto);
    }

    public async Task<MaintenanceTechnicianDto?> GetTechnicianByIdAsync(Guid employeeId)
    {
        var employee = await _employeeRepository.GetByIdWithDetailsAsync(employeeId);
        return employee == null || !employee.CanBeAssignedToMaintenance ? null : MapToMaintenanceTechnicianDto(employee);
    }

    public async Task<TechnicianAvailabilityDto?> GetTechnicianAvailabilityAsync(Guid employeeId)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null || !employee.CanBeAssignedToMaintenance) return null;

        return new TechnicianAvailabilityDto
        {
            EmployeeId = employee.Id,
            EmployeeName = employee.FullName,
            IsAvailable = employee.IsActive && employee.StaffStatus == StaffStatus.Active,
            AvailableFrom = employee.StaffStatus == StaffStatus.Active ? DateTime.UtcNow : null,
            UnavailabilityReason = employee.StaffStatus != StaffStatus.Active ? employee.StaffStatus.ToString() : null,
            CurrentWorkOrders = 0,
            WorkloadPercentage = 0
        };
    }

    public async Task<IEnumerable<MaintenanceTechnicianDto>> GetTechniciansWithSkillAsync(Guid skillId, SkillLevel? minLevel = null)
    {
        var employees = await _employeeRepository.GetEmployeesBySkillAsync(skillId, minLevel);
        return employees.Where(e => e.CanBeAssignedToMaintenance).Select(MapToMaintenanceTechnicianDto);
    }

    public Task<bool> EmployeeNumberExistsAsync(string employeeNumber) => _employeeRepository.EmployeeNumberExistsAsync(employeeNumber);
    public Task<bool> EmailExistsAsync(string email) => _employeeRepository.EmailExistsAsync(NormalizeEmail(email));
    public Task<string> GenerateEmployeeNumberAsync() => _employeeRepository.GenerateEmployeeNumberAsync();

    // ── Headline counts ─────────────────────────────────────────────────────────
    //
    // These four feed the `/hr` landing page and nothing else. All four were wrong in the same
    // way and one of them was wrong twice.
    //
    // ⚠ TENANT. Every one of them read `CountAsync()` / `GetAllAsync()`, which scope on
    // `!IsDeleted` and nothing else. The comment on `GetTenantId()` at the top of this file says
    // why that is not enough — the DbContext is registered without a tenant, so its global tenant
    // filter is INERT — and `SearchAsync` twenty lines up already scopes explicitly for exactly
    // that reason. Measured before this fix, with one employee planted under a second tenant:
    // `stats/total` answered 6591 where the caller's own tenant held 6590. A landing page was
    // publishing another company's headcount. Every query below now names the tenant.
    //
    // ⚠ MEANING. `GetActiveEmployeeCountAsync` counted `IsActive` — the record-enabled flag —
    // while its three neighbours group `StaffStatus`, the employment status. The home renders the
    // four side by side under one heading, so "Active 6474" and "On probation 53" read as
    // disjoint when the 53 were inside the 6474, and the two numbers were answers to two
    // different questions. It now counts the status its label names, which also makes the tiles
    // a genuine partition of the total.
    //
    // ⚠ COST. The two dictionaries materialised every employee row — 6,588 of them, and the
    // department one dragged its navigation along — to produce a handful of integers: 274 ms and
    // 387 ms against 21–32 ms for the two that counted in SQL. Both now group in SQL.

    public async Task<int> GetTotalEmployeeCountAsync()
    {
        var tenantId = GetTenantId();
        return await _employeeRepository.GetQueryable()
            .Where(e => e.TenantId == tenantId)
            .CountAsync();
    }

    public async Task<int> GetActiveEmployeeCountAsync()
    {
        var tenantId = GetTenantId();
        return await _employeeRepository.GetQueryable()
            .Where(e => e.TenantId == tenantId && e.StaffStatus == StaffStatus.Active)
            .CountAsync();
    }

    public async Task<Dictionary<StaffStatus, int>> GetEmployeeCountByStatusAsync()
    {
        var tenantId = GetTenantId();
        var rows = await _employeeRepository.GetQueryable()
            .Where(e => e.TenantId == tenantId)
            .GroupBy(e => e.StaffStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        return rows.ToDictionary(r => r.Status, r => r.Count);
    }

    // ⚠ `GetEmployeeCountByDepartmentAsync` was removed in slice 10, along with its endpoint and
    // its uncalled frontend method. It grouped the workforce on `Department`, which the create
    // path a few hundred lines above already calls deprecated: `OrganizationUnitId` is REQUIRED on
    // an employee and `DepartmentId` is not, and the live data says the same — 42 organisation
    // units against 7 departments, and 6,566 of 6,590 employees carrying a unit against 6,076
    // carrying a department, so 514 people filed under "Unassigned".
    //
    // It had no caller anywhere in the solution. Re-pointing it at `OrganizationUnitId` was the
    // obvious repair and is the wrong one: `GET api/Organogram/units` already returns per-unit
    // `EmployeeCount` **and** a subtree rollup, so a second by-unit count here would be the same
    // rule stated twice with no rollup and nothing reading it. Headcount by organisation unit has
    // one home, and it is the organogram.

    #endregion

    #region Private helpers

    private async Task EnsureEmployeeExistsAsync(Guid employeeId)
    {
        var employee = await _employeeRepository.GetByIdAsync(employeeId);
        if (employee == null) throw new ArgumentException($"Employee '{employeeId}' not found.");
    }

    /// <summary>Trimmed and lower-cased, or <c>null</c> for blank — never an empty string.</summary>
    private static string? NormalizeEmail(string? email)
        => string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    private async Task ValidateManagerAssignmentAsync(Guid employeeId, Guid managerId, CancellationToken cancellationToken)
    {
        if (employeeId == managerId)
            throw new InvalidOperationException("Employee cannot be their own manager.");

        var manager = await _employeeRepository.GetByIdAsync(managerId);
        if (manager == null) throw new ArgumentException("Manager not found.");
        if (!manager.IsActive || manager.StaffStatus == StaffStatus.Terminated)
            throw new InvalidOperationException("Manager must be active and not terminated.");

        if (employeeId != Guid.Empty && await WouldCreateCircularReportingAsync(employeeId, managerId, cancellationToken))
            throw new InvalidOperationException("Manager assignment would create circular reporting.");
    }

    private static EmployeeDto ToSummaryFromDetail(EmployeeDetailDto d)
        => new()
        {
            Id = d.Id,
            EmployeeNumber = d.EmployeeNumber,
            FirstName = d.FirstName,
            MiddleName = d.MiddleName,
            LastName = d.LastName,
            FullName = d.FullName,
            DisplayName = d.DisplayName,
            Title = d.Title,
            Gender = d.Gender,
            EmailAddress = d.EmailAddress,
            MobileNumber = d.MobileNumber,
            DepartmentName = d.DepartmentName,
            SectionName = d.SectionName,
            PositionTitle = d.PositionTitle,
            OrganizationLevelName = d.OrganizationLevelName,
            OrganizationUnitName = d.OrganizationUnitName,
            LocationLevelName = d.LocationLevelName,
            LocationName = d.LocationName,
            StaffStatus = d.StaffStatus,
            EmploymentType = d.EmploymentType,
            IsActive = d.IsActive,
            IsFullTime = d.IsFullTime,
            IsExpatriate = d.IsExpatriate,
            DateEmployed = d.DateEmployed,
            YearsOfService = d.YearsOfService,
            CanBeAssignedToMaintenance = d.CanBeAssignedToMaintenance,
            PicturePath = d.PicturePath
        };

    private static void ValidateContractTaxRules(TaxTreatmentType taxTreatmentType, decimal? withholdingTaxRate)
    {
        if (taxTreatmentType == TaxTreatmentType.WithholdingTax && !withholdingTaxRate.HasValue)
            throw new InvalidOperationException("Withholding tax contracts require a WithholdingTaxRate.");

        if (withholdingTaxRate.HasValue && (withholdingTaxRate < 0 || withholdingTaxRate > 100))
            throw new InvalidOperationException("WithholdingTaxRate must be between 0 and 100.");
    }

    private static MaintenanceTechnicianDto MapToMaintenanceTechnicianDto(Employee employee)
    {
        return new MaintenanceTechnicianDto
        {
            Id = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            FullName = employee.FullName,
            DisplayName = employee.DisplayName,
            EmailAddress = employee.EmailAddress,
            MobileNumber = employee.MobileNumber,
            PositionTitle = employee.Position?.Title ?? string.Empty,
            IsActive = employee.IsActive,
            IsAvailable = employee.IsActive && employee.StaffStatus == StaffStatus.Active,
            Skills = employee.Skills?.Select(es => new ErpSystem.Core.DTOs.Maintenance.UserTechnicianSkillDto
            {
                SkillName = es.Skill.Name,
                Level = (int)es.SkillLevel,
                IsCertified = es.CertificationDate.HasValue
            }).ToList() ?? new List<ErpSystem.Core.DTOs.Maintenance.UserTechnicianSkillDto>(),
            CurrentWorkOrders = 0,
            WorkloadScore = 0,
            BadgeNumber = employee.BadgeNumber,
            ShiftName = null
        };
    }

    #endregion
}
