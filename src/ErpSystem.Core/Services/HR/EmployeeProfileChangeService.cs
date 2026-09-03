using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.ProfileChanges;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Decision D6 — the employee's profile, and the approval path for the parts of it that carry
/// identity or payment consequences. See <see cref="IEmployeeProfileChangeService"/>.
/// </summary>
public class EmployeeProfileChangeService : IEmployeeProfileChangeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<EmployeeProfileChangeService> _logger;

    public EmployeeProfileChangeService(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<EmployeeProfileChangeService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    /// <summary>Human labels, so no screen has to keep its own copy of this map.</summary>
    private static readonly IReadOnlyDictionary<EmployeeProfileField, string> FieldLabels =
        new Dictionary<EmployeeProfileField, string>
        {
            [EmployeeProfileField.FirstName] = "First name",
            [EmployeeProfileField.MiddleName] = "Middle name",
            [EmployeeProfileField.LastName] = "Last name",
            [EmployeeProfileField.Title] = "Title",
            [EmployeeProfileField.DateOfBirth] = "Date of birth",
            [EmployeeProfileField.Gender] = "Gender",
            [EmployeeProfileField.MaritalStatus] = "Marital status",
            [EmployeeProfileField.EmailAddress] = "Email address",
            [EmployeeProfileField.Address] = "Address",
            [EmployeeProfileField.City] = "City",
            [EmployeeProfileField.State] = "Region / state",
            [EmployeeProfileField.PostalCode] = "Postal code",
            [EmployeeProfileField.DigitalAddress] = "Digital address",
            [EmployeeProfileField.CountryId] = "Country",
            [EmployeeProfileField.SocialSecurityNumber] = "SSNIT number",
            [EmployeeProfileField.TINNumber] = "TIN",
            [EmployeeProfileField.TaxNumber] = "Tax number",
            [EmployeeProfileField.BankName] = "Bank",
            [EmployeeProfileField.BankBranchName] = "Bank branch",
            [EmployeeProfileField.BankAccountNumber] = "Account number",
            [EmployeeProfileField.BankAccountName] = "Account name",
            [EmployeeProfileField.BankAccountType] = "Account type",
            [EmployeeProfileField.MobileMoneyNumber] = "Mobile money number",
        };

    /// <summary>The fields that live on an <see cref="EmployeeBankDetail"/> row, not the employee.</summary>
    private static readonly HashSet<EmployeeProfileField> BankFields =
    [
        EmployeeProfileField.BankName,
        EmployeeProfileField.BankBranchName,
        EmployeeProfileField.BankAccountNumber,
        EmployeeProfileField.BankAccountName,
        EmployeeProfileField.BankAccountType,
        EmployeeProfileField.MobileMoneyNumber,
    ];

    private Guid GetTenantId()
    {
        var tenantId = _currentUserProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("No tenant is associated with the current user.");
        return tenantId;
    }

    private IQueryable<EmployeeProfileChangeRequest> Scoped(Guid tenantId) =>
        Bare(tenantId)
            .Include(r => r.Employee)
            .Include(r => r.ReviewedBy)
            .Include(r => r.BankDetail)
            .Include(r => r.Items);

    /// <summary>
    /// The same scoping without the navigation loads — for queries that project to something
    /// other than the entity, where EF discards the <c>Include</c>s anyway.
    /// </summary>
    private IQueryable<EmployeeProfileChangeRequest> Bare(Guid tenantId) =>
        _unitOfWork.Repository<EmployeeProfileChangeRequest>()
            .GetQueryable()
            .Where(r => r.TenantId == tenantId && !r.IsDeleted);

    /// <summary>The fields this employee already has waiting on HR.</summary>
    private Task<List<EmployeeProfileField>> PendingFieldsAsync(
        Guid tenantId, Guid employeeId, CancellationToken ct) =>
        Bare(tenantId)
            .Where(r => r.EmployeeId == employeeId && r.Status == ProfileChangeRequestStatus.Pending)
            .SelectMany(r => r.Items.Select(i => i.Field))
            .Distinct()
            .ToListAsync(ct);

    // ── Profile ───────────────────────────────────────────────────────────────

    public async Task<MyProfileDto> GetMyProfileAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var employee = await LoadEmployeeAsync(tenantId, employeeId, cancellationToken);

        var pendingFields = await PendingFieldsAsync(tenantId, employeeId, cancellationToken);
        return MapProfile(employee, pendingFields);
    }

    public async Task<MyProfileDto> UpdateMyContactDetailsAsync(
        Guid employeeId, UpdateMyContactDetailsDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var employee = await LoadEmployeeAsync(tenantId, employeeId, cancellationToken);

        // Null means "leave alone"; an empty string clears the field — a person genuinely may
        // no longer have a desk extension, and refusing to record that would be wrong.
        if (dto.MobileNumber is not null) employee.MobileNumber = Blank(dto.MobileNumber);
        if (dto.TelephoneNumber is not null) employee.TelephoneNumber = Blank(dto.TelephoneNumber);
        if (dto.BusinessNumber is not null) employee.BusinessNumber = Blank(dto.BusinessNumber);
        if (dto.Extension is not null) employee.Extension = Blank(dto.Extension);
        if (dto.Religion is not null) employee.Religion = Blank(dto.Religion);
        if (dto.MaritalStatus is not null) employee.MaritalStatus = dto.MaritalStatus;

        await _unitOfWork.Repository<Employee>().UpdateAsync(employee);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Employee {EmployeeId} updated their own contact details.", employeeId);

        return await GetMyProfileAsync(employeeId, cancellationToken);
    }

    // ── Requests ──────────────────────────────────────────────────────────────

    public async Task<ProfileChangeRequestDto> CreateRequestAsync(
        Guid employeeId, CreateProfileChangeRequestDto dto, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var employee = await LoadEmployeeAsync(tenantId, employeeId, cancellationToken);

        if (dto.Items is null || dto.Items.Count == 0)
            throw new InvalidOperationException("A change request must name at least one field.");

        var duplicated = dto.Items.GroupBy(i => i.Field).FirstOrDefault(g => g.Count() > 1);
        if (duplicated is not null)
            throw new InvalidOperationException(
                $"'{Label(duplicated.Key)}' is listed twice in the same request.");

        var touchesBank = dto.Items.Any(i => BankFields.Contains(i.Field));
        EmployeeBankDetail? bank = null;
        if (touchesBank)
        {
            if (dto.BankDetailId is not { } bankId)
                throw new InvalidOperationException(
                    "Say which of your bank accounts this change is for.");

            // The account must be one of the caller's OWN — this is the whole point of the guard.
            bank = employee.BankDetails.FirstOrDefault(b => b.Id == bankId && !b.IsDeleted)
                ?? throw new InvalidOperationException("That bank account is not one of yours.");
        }

        // A second pending request for the same field would race the first, and whichever HR
        // approved last would silently win.
        var alreadyPending = await PendingFieldsAsync(tenantId, employeeId, cancellationToken);
        var clash = dto.Items.FirstOrDefault(i => alreadyPending.Contains(i.Field));
        if (clash is not null)
            throw new InvalidOperationException(
                $"A change to '{Label(clash.Field)}' is already waiting for HR. Withdraw that request first.");

        var request = new EmployeeProfileChangeRequest
        {
            TenantId = tenantId,
            RequestNumber = await GenerateNumberAsync(tenantId, cancellationToken),
            EmployeeId = employeeId,
            Status = ProfileChangeRequestStatus.Pending,
            SubmittedAt = DateTime.UtcNow,
            Reason = dto.Reason.Trim(),
            BankDetailId = bank?.Id,
        };

        foreach (var item in dto.Items)
        {
            var newValue = (item.NewValue ?? string.Empty).Trim();
            if (newValue.Length == 0)
                throw new InvalidOperationException($"Give a new value for '{Label(item.Field)}'.");

            // Parse now, not at approval: a value that cannot be applied must be refused while
            // the employee is still on the screen to correct it.
            ValidateValue(item.Field, newValue);

            var current = ReadCurrent(employee, bank, item.Field);
            if (string.Equals(current ?? string.Empty, newValue, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"'{Label(item.Field)}' already has that value.");

            request.Items.Add(new EmployeeProfileChangeItem
            {
                TenantId = tenantId,
                Field = item.Field,
                OldValue = current,
                NewValue = newValue,
            });
        }

        await _unitOfWork.Repository<EmployeeProfileChangeRequest>().AddAsync(request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Profile change request {Number} filed by employee {EmployeeId} over {Count} field(s).",
            request.RequestNumber, employeeId, request.Items.Count);

        return await RequireDtoAsync(request.Id, tenantId, cancellationToken);
    }

    public async Task<IEnumerable<ProfileChangeRequestDto>> GetMineAsync(
        Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var rows = await Scoped(tenantId)
            .Where(r => r.EmployeeId == employeeId)
            .OrderByDescending(r => r.SubmittedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<ProfileChangeRequestDto?> GetByIdAsync(
        Guid id, Guid requestingEmployeeId, bool isHrDesk, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var request = await Scoped(tenantId).FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (request is null) return null;

        // Somebody else's request is a lookup MISS, not a refusal: a 403 would confirm that a
        // request with that id exists.
        if (!isHrDesk && request.EmployeeId != requestingEmployeeId) return null;

        return Map(request);
    }

    public async Task<ProfileChangeRequestDto> CancelAsync(
        Guid id, Guid employeeId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // Scoped to the caller IN THE QUERY, so a request that is not theirs is a lookup miss
        // and not a refusal — matching the read arm. A 403 here would make cancel an
        // enumeration oracle: "forbidden" would confirm the id exists, "not found" that it does
        // not, which is exactly what the 404-on-foreign-id law elsewhere in this module prevents.
        var request = await Scoped(tenantId)
                .FirstOrDefaultAsync(r => r.Id == id && r.EmployeeId == employeeId, cancellationToken)
            ?? throw new KeyNotFoundException("Change request not found.");

        if (request.Status != ProfileChangeRequestStatus.Pending)
            throw new InvalidOperationException(
                $"This request has already been {request.Status.ToString().ToLowerInvariant()}.");

        request.Status = ProfileChangeRequestStatus.Cancelled;
        request.CancelledAt = DateTime.UtcNow;
        await _unitOfWork.Repository<EmployeeProfileChangeRequest>().UpdateAsync(request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(request);
    }

    // ── HR desk ───────────────────────────────────────────────────────────────

    public async Task<IEnumerable<ProfileChangeRequestDto>> GetQueueAsync(
        ProfileChangeRequestStatus? status, CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var query = Scoped(tenantId);
        if (status is { } wanted) query = query.Where(r => r.Status == wanted);

        var rows = await query
            // Pending first — the queue's job is what still needs answering.
            .OrderBy(r => r.Status == ProfileChangeRequestStatus.Pending ? 0 : 1)
            .ThenByDescending(r => r.SubmittedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<ProfileChangeRequestDto> ApproveAsync(
        Guid id, Guid reviewerEmployeeId, ReviewProfileChangeRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var request = await Scoped(tenantId).FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Change request not found.");

        RequirePending(request);

        var employee = await LoadEmployeeAsync(tenantId, request.EmployeeId, cancellationToken);
        EmployeeBankDetail? bank = null;
        if (request.BankDetailId is { } bankId)
        {
            bank = employee.BankDetails.FirstOrDefault(b => b.Id == bankId && !b.IsDeleted)
                ?? throw new InvalidOperationException(
                    "The bank account this request targets no longer exists on the employee's record.");
        }

        // The desk update path enforces these; approving here must not become a way around them.
        await EnsureUniqueAsync(tenantId, employee.Id, request, cancellationToken);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            foreach (var item in request.Items)
            {
                ApplyValue(employee, bank, item.Field, item.NewValue);
                item.AppliedValue = item.NewValue;
            }

            // A bank account that HR has just re-approved is verified by definition — and
            // leaving a stale "verified" flag on a changed account number would be a lie.
            if (bank is not null)
            {
                bank.IsVerified = true;
                bank.VerifiedById = reviewerEmployeeId;
                bank.VerifiedDate = DateTime.UtcNow;
                await _unitOfWork.Repository<EmployeeBankDetail>().UpdateAsync(bank);
            }

            request.Status = ProfileChangeRequestStatus.Approved;
            request.ReviewedById = reviewerEmployeeId;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewComments = string.IsNullOrWhiteSpace(dto.Comments) ? null : dto.Comments.Trim();
            request.AppliedAt = DateTime.UtcNow;

            await _unitOfWork.Repository<Employee>().UpdateAsync(employee);
            await _unitOfWork.Repository<EmployeeProfileChangeRequest>().UpdateAsync(request);
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        _logger.LogInformation(
            "Profile change request {Number} approved and applied by {ReviewerId}.",
            request.RequestNumber, reviewerEmployeeId);

        return await RequireDtoAsync(request.Id, tenantId, cancellationToken);
    }

    public async Task<ProfileChangeRequestDto> RejectAsync(
        Guid id, Guid reviewerEmployeeId, ReviewProfileChangeRequestDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var request = await Scoped(tenantId).FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("Change request not found.");

        RequirePending(request);

        // "No" without a reason is a dead end for the employee, who cannot then correct it.
        if (string.IsNullOrWhiteSpace(dto.Comments))
            throw new InvalidOperationException("Say why the request is being refused.");

        request.Status = ProfileChangeRequestStatus.Rejected;
        request.ReviewedById = reviewerEmployeeId;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewComments = dto.Comments.Trim();

        await _unitOfWork.Repository<EmployeeProfileChangeRequest>().UpdateAsync(request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(request);
    }

    public async Task AttachEvidenceAsync(
        Guid id, Guid employeeId, Guid fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        string filePath, string fileName, string contentType, long fileSize,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();

        // Caller-scoped in the query, for the same reason as CancelAsync.
        var request = await Scoped(tenantId)
                .FirstOrDefaultAsync(r => r.Id == id && r.EmployeeId == employeeId, cancellationToken)
            ?? throw new KeyNotFoundException("Change request not found.");

        RequirePending(request);

        request.EvidenceFileUploadRecordId = fileUploadRecordId;
        request.EvidenceDocumentRecordId = documentRecordId;
        request.EvidenceDocumentVersionId = documentVersionId;
        request.EvidenceFilePath = filePath;
        request.EvidenceFileName = fileName;
        request.EvidenceContentType = contentType;
        request.EvidenceFileSize = fileSize;

        await _unitOfWork.Repository<EmployeeProfileChangeRequest>().UpdateAsync(request);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private static void RequirePending(EmployeeProfileChangeRequest request)
    {
        if (request.Status != ProfileChangeRequestStatus.Pending)
            throw new InvalidOperationException(
                $"This request has already been {request.Status.ToString().ToLowerInvariant()}.");
    }

    private static string Label(EmployeeProfileField field)
        => FieldLabels.TryGetValue(field, out var label) ? label : field.ToString();

    private static string? Blank(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<Employee> LoadEmployeeAsync(Guid tenantId, Guid employeeId, CancellationToken ct)
        => await _unitOfWork.Repository<Employee>()
               .GetQueryable()
               .Include(e => e.EmergencyContacts)
               .Include(e => e.Dependents)
               // Bank + Branch are loaded because the canonical ToDto() resolves the catalogue
               // names and codes through them; without these it silently falls back to the
               // free-text columns and the codes come back null.
               .Include(e => e.BankDetails).ThenInclude(b => b.Bank)
               .Include(e => e.BankDetails).ThenInclude(b => b.Branch)
               .Include(e => e.Position)
               .Include(e => e.Department)
               .Include(e => e.Section)
               .Include(e => e.OrganizationUnit)
               .Include(e => e.Location)
               .Include(e => e.Manager)
               .Include(e => e.Country)
               .FirstOrDefaultAsync(e => e.Id == employeeId && e.TenantId == tenantId && !e.IsDeleted, ct)
           ?? throw new KeyNotFoundException("Employee record not found.");

    /// <remarks>
    /// Numbers come from the highest suffix already issued, over rows INCLUDING soft-deleted
    /// ones — counting live rows re-issues a number the moment anything is deleted (the shape
    /// fixed across the SHE, movement, grievance and disciplinary generators).
    /// </remarks>
    private async Task<string> GenerateNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var prefix = $"PCR-{DateTime.UtcNow.Year}-";

        var issued = await _unitOfWork.Repository<EmployeeProfileChangeRequest>()
            .GetQueryableIncludingDeleted(r => r.TenantId == tenantId && r.RequestNumber.StartsWith(prefix))
            .Select(r => r.RequestNumber)
            .ToListAsync(cancellationToken);

        var highest = issued
            .Select(n => int.TryParse(n[prefix.Length..], out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"{prefix}{(highest + 1):D5}";
    }

    /// <summary>
    /// Re-runs the tenant-uniqueness rules <c>EmployeeService.UpdateEmployeeAsync</c> enforces,
    /// so an approval cannot introduce a duplicate the desk path would have refused.
    /// </summary>
    private async Task EnsureUniqueAsync(
        Guid tenantId, Guid employeeId, EmployeeProfileChangeRequest request, CancellationToken ct)
    {
        var employees = _unitOfWork.Repository<Employee>().GetQueryable()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.Id != employeeId);

        foreach (var item in request.Items)
        {
            var value = item.NewValue;
            var taken = item.Field switch
            {
                EmployeeProfileField.EmailAddress =>
                    // Blank clears the address (optional since 2026-09-03) and collides with nothing.
                    !string.IsNullOrWhiteSpace(value)
                    && await employees.AnyAsync(e => e.EmailAddress == value, ct),
                EmployeeProfileField.SocialSecurityNumber =>
                    await employees.AnyAsync(e => e.SocialSecurityNumber == value, ct),
                EmployeeProfileField.TINNumber =>
                    await employees.AnyAsync(e => e.TINNumber == value, ct),
                EmployeeProfileField.TaxNumber =>
                    await employees.AnyAsync(e => e.TaxNumber == value, ct),
                _ => false,
            };

            if (taken)
                throw new InvalidOperationException(
                    $"Another employee already has that {Label(item.Field).ToLowerInvariant()}.");
        }
    }

    /// <summary>Refuses a value that could not be applied, while the employee can still fix it.</summary>
    private static void ValidateValue(EmployeeProfileField field, string value)
    {
        switch (field)
        {
            case EmployeeProfileField.DateOfBirth:
                if (!DateOnly.TryParse(value, out var dob))
                    throw new InvalidOperationException("Give the date of birth as a date, for example 1990-04-23.");
                if (dob > DateOnly.FromDateTime(DateTime.UtcNow))
                    throw new InvalidOperationException("A date of birth cannot be in the future.");
                break;

            case EmployeeProfileField.Gender:
                if (!Enum.TryParse<Gender>(value, ignoreCase: true, out _))
                    throw new InvalidOperationException($"'{value}' is not a gender this system recognises.");
                break;

            case EmployeeProfileField.MaritalStatus:
                if (!Enum.TryParse<MaritalStatus>(value, ignoreCase: true, out _))
                    throw new InvalidOperationException($"'{value}' is not a marital status this system recognises.");
                break;

            case EmployeeProfileField.BankAccountType:
                if (!Enum.TryParse<EmployeeBankAccountType>(value, ignoreCase: true, out _))
                    throw new InvalidOperationException($"'{value}' is not an account type this system recognises.");
                break;

            case EmployeeProfileField.CountryId:
                if (!Guid.TryParse(value, out _))
                    throw new InvalidOperationException("Pick a country from the list.");
                break;

            case EmployeeProfileField.EmailAddress:
                if (!value.Contains('@') || value.StartsWith('@') || value.EndsWith('@'))
                    throw new InvalidOperationException("That does not look like an email address.");
                break;
        }
    }

    private static string? ReadCurrent(Employee e, EmployeeBankDetail? bank, EmployeeProfileField field)
        => field switch
        {
            EmployeeProfileField.FirstName => e.FirstName,
            EmployeeProfileField.MiddleName => e.MiddleName,
            EmployeeProfileField.LastName => e.LastName,
            EmployeeProfileField.Title => e.Title,
            EmployeeProfileField.DateOfBirth => e.DateOfBirth?.ToString("yyyy-MM-dd"),
            EmployeeProfileField.Gender => e.Gender?.ToString(),
            EmployeeProfileField.MaritalStatus => e.MaritalStatus?.ToString(),
            EmployeeProfileField.EmailAddress => e.EmailAddress,
            EmployeeProfileField.Address => e.Address,
            EmployeeProfileField.City => e.City,
            EmployeeProfileField.State => e.State,
            EmployeeProfileField.PostalCode => e.PostalCode,
            EmployeeProfileField.DigitalAddress => e.DigitalAddress,
            EmployeeProfileField.CountryId => e.CountryId?.ToString(),
            EmployeeProfileField.SocialSecurityNumber => e.SocialSecurityNumber,
            EmployeeProfileField.TINNumber => e.TINNumber,
            EmployeeProfileField.TaxNumber => e.TaxNumber,
            EmployeeProfileField.BankName => bank?.BankName,
            EmployeeProfileField.BankBranchName => bank?.BranchName,
            EmployeeProfileField.BankAccountNumber => bank?.AccountNumber,
            EmployeeProfileField.BankAccountName => bank?.AccountName,
            EmployeeProfileField.BankAccountType => bank?.AccountType.ToString(),
            EmployeeProfileField.MobileMoneyNumber => bank?.MobileMoneyNumber,
            _ => null,
        };

    private static void ApplyValue(Employee e, EmployeeBankDetail? bank, EmployeeProfileField field, string value)
    {
        switch (field)
        {
            case EmployeeProfileField.FirstName: e.FirstName = value; break;
            case EmployeeProfileField.MiddleName: e.MiddleName = value; break;
            case EmployeeProfileField.LastName: e.LastName = value; break;
            case EmployeeProfileField.Title: e.Title = value; break;
            case EmployeeProfileField.DateOfBirth: e.DateOfBirth = DateOnly.Parse(value); break;
            case EmployeeProfileField.Gender: e.Gender = Enum.Parse<Gender>(value, ignoreCase: true); break;
            case EmployeeProfileField.MaritalStatus:
                e.MaritalStatus = Enum.Parse<MaritalStatus>(value, ignoreCase: true); break;
            case EmployeeProfileField.EmailAddress:
                // Never store "" — the unique index is filtered on NOT NULL, not on non-empty.
                e.EmailAddress = string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
                break;
            case EmployeeProfileField.Address: e.Address = value; break;
            case EmployeeProfileField.City: e.City = value; break;
            case EmployeeProfileField.State: e.State = value; break;
            case EmployeeProfileField.PostalCode: e.PostalCode = value; break;
            case EmployeeProfileField.DigitalAddress: e.DigitalAddress = value; break;
            case EmployeeProfileField.CountryId: e.CountryId = Guid.Parse(value); break;
            case EmployeeProfileField.SocialSecurityNumber: e.SocialSecurityNumber = value; break;
            case EmployeeProfileField.TINNumber: e.TINNumber = value; break;
            case EmployeeProfileField.TaxNumber: e.TaxNumber = value; break;

            case EmployeeProfileField.BankName:
                RequireBank(bank).BankName = value; break;
            case EmployeeProfileField.BankBranchName:
                RequireBank(bank).BranchName = value; break;
            case EmployeeProfileField.BankAccountNumber:
                RequireBank(bank).AccountNumber = value; break;
            case EmployeeProfileField.BankAccountName:
                RequireBank(bank).AccountName = value; break;
            case EmployeeProfileField.BankAccountType:
                RequireBank(bank).AccountType = Enum.Parse<EmployeeBankAccountType>(value, ignoreCase: true); break;
            case EmployeeProfileField.MobileMoneyNumber:
                RequireBank(bank).MobileMoneyNumber = value; break;

            default:
                throw new InvalidOperationException($"'{field}' cannot be changed through a profile request.");
        }
    }

    private static EmployeeBankDetail RequireBank(EmployeeBankDetail? bank)
        => bank ?? throw new InvalidOperationException(
            "This request changes bank details but names no account.");

    private async Task<ProfileChangeRequestDto> RequireDtoAsync(Guid id, Guid tenantId, CancellationToken ct)
    {
        var row = await Scoped(tenantId).AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException("Change request not found.");
        return Map(row);
    }

    private static string? MaskAccount(string? accountNumber)
        => string.IsNullOrWhiteSpace(accountNumber)
            ? null
            : accountNumber.Length <= 4
                ? new string('•', accountNumber.Length)
                : $"{new string('•', accountNumber.Length - 4)}{accountNumber[^4..]}";

    private static ProfileChangeRequestDto Map(EmployeeProfileChangeRequest r) => new()
    {
        Id = r.Id,
        RequestNumber = r.RequestNumber,
        EmployeeId = r.EmployeeId,
        EmployeeName = r.Employee?.FullName ?? string.Empty,
        EmployeeNumber = r.Employee?.EmployeeNumber ?? string.Empty,
        Status = r.Status,
        SubmittedAt = r.SubmittedAt,
        Reason = r.Reason,
        BankDetailId = r.BankDetailId,
        BankAccountMasked = MaskAccount(r.BankDetail?.AccountNumber),
        ReviewedById = r.ReviewedById,
        ReviewedByName = r.ReviewedBy?.FullName,
        ReviewedAt = r.ReviewedAt,
        ReviewComments = r.ReviewComments,
        AppliedAt = r.AppliedAt,
        CancelledAt = r.CancelledAt,
        HasEvidence = r.EvidenceFileUploadRecordId.HasValue,
        EvidenceFileName = r.EvidenceFileName,
        Items = r.Items
            .OrderBy(i => i.Field)
            .Select(i => new ProfileChangeItemDto
            {
                Id = i.Id,
                Field = i.Field,
                FieldLabel = Label(i.Field),
                OldValue = i.OldValue,
                NewValue = i.NewValue,
                AppliedValue = i.AppliedValue,
            })
            .ToList(),
    };

    private static MyProfileDto MapProfile(Employee e, IEnumerable<EmployeeProfileField> pendingFields) => new()
    {
        EmployeeId = e.Id,
        EmployeeNumber = e.EmployeeNumber,

        FirstName = e.FirstName,
        MiddleName = e.MiddleName,
        LastName = e.LastName,
        FullName = e.FullName,
        Title = e.Title,
        DateOfBirth = e.DateOfBirth,
        Gender = e.Gender,
        MaritalStatus = e.MaritalStatus,
        Religion = e.Religion,
        BloodType = e.BloodType,
        PicturePath = e.PicturePath,

        EmailAddress = e.EmailAddress,
        MobileNumber = e.MobileNumber,
        TelephoneNumber = e.TelephoneNumber,
        BusinessNumber = e.BusinessNumber,
        Extension = e.Extension,
        Address = e.Address,
        City = e.City,
        State = e.State,
        PostalCode = e.PostalCode,
        DigitalAddress = e.DigitalAddress,
        CountryId = e.CountryId,
        CountryName = e.Country?.Name,

        SocialSecurityNumber = e.SocialSecurityNumber,
        TINNumber = e.TINNumber,
        TaxNumber = e.TaxNumber,

        PositionTitle = e.Position?.Title,
        DepartmentName = e.Department?.Name,
        SectionName = e.Section?.Name,
        OrganizationUnitName = e.OrganizationUnit?.Name,
        LocationName = e.Location?.Name,
        ManagerName = e.Manager?.FullName,
        EmploymentType = e.EmploymentType,
        StaffStatus = e.StaffStatus,
        DateEmployed = e.DateEmployed,
        ConfirmationDate = e.ConfirmationDate,
        YearsOfService = e.YearsOfService,

        // ⚠ These three use the module's OWN mappers rather than hand-rolled projections.
        // The first draft hand-rolled them and lost two things the canonical ones do:
        // `EmployeeBankDetail.ToDto()` MASKS the account number to its last four digits and
        // resolves bank/branch names (and codes) from the catalogue navigations. A DTO whose
        // desk read is redacted must not arrive unredacted through the portal — that is the
        // field-level-redaction trap, and the portal is the more exposed of the two surfaces.
        EmergencyContacts = e.EmergencyContacts.Where(c => !c.IsDeleted).Select(c => c.ToDto()).ToList(),
        Dependents = e.Dependents.Where(d => !d.IsDeleted).Select(d => d.ToLegacyDto()).ToList(),
        BankDetails = e.BankDetails.Where(b => !b.IsDeleted).Select(b => b.ToDto()).ToList(),

        FieldsWithPendingRequests = pendingFields.ToList(),
    };
}
