using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// What a trip's compliance records say about it (travel final closure, lane 7, slice 7b) — read by submission, which
/// warns, and by ticketing, which refuses. One place, so the two cannot disagree.
/// </summary>
/// <remarks>
/// <para><b>The visa register (E4, D-39).</b> When the traveller's primary passport is on file and the register has an
/// entry for its country to the destination, the register answers whether the trip needs a visa: an E-Visa or an embassy
/// visa is applied for before travel; visa-free and visa-on-arrival are not; and a <i>Prohibited</i> entry means the
/// passport is refused entry. Without a passport or an entry, the requester's own answer stands.</para>
///
/// <para><b>Ticketing</b> waits for three things: a visa when one is needed (lane 5, T-24); cover across an international
/// trip's dates (O-16); and, on a Critical trip, the traveller's acknowledgement of the current risk assessment (D-37).</para>
/// </remarks>
public static class StaffTravelComplianceRules
{
    /// <summary>A passport within this many months of expiry at the return is warned about (O-16; TDC to confirm).</summary>
    public const int PassportValidityMonths = 6;

    public sealed record VisaVerdict(bool Known, bool Needs, bool Prohibited, string? PassportCountry, string? DestinationCountry, VisaRequirementType? Type);

    private static readonly VisaVerdict Unknown = new(false, false, false, null, null, null);

    /// <summary>The traveller's primary passport's country, if one is on file.</summary>
    public static Task<Guid?> PassportCountryAsync(IUnitOfWork unitOfWork, Guid tenantId, Guid employeeId, CancellationToken cancellationToken)
        => unitOfWork.Repository<StaffTravelDocument>()
            .GetQueryable(d => d.TenantId == tenantId && d.EmployeeId == employeeId && !d.IsDeleted
                            && d.DocumentType == TravelDocumentType.Passport && d.IsPrimary)
            .OrderByDescending(d => d.ExpiryDate)
            .Select(d => (Guid?)d.IssuingCountryId)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>What the visa register says for the traveller's passport to the destination (D-39).</summary>
    public static async Task<VisaVerdict> VisaVerdictAsync(
        IUnitOfWork unitOfWork, Guid tenantId, Guid employeeId, Guid destinationCountryId, CancellationToken cancellationToken)
    {
        var passportCountryId = await PassportCountryAsync(unitOfWork, tenantId, employeeId, cancellationToken);
        if (passportCountryId is not Guid passport) return Unknown;
        var entry = await unitOfWork.Repository<StaffTravelVisaRequirement>()
            .GetQueryable(r => r.TenantId == tenantId && !r.IsDeleted && r.PassportCountryId == passport
                            && r.DestinationCountryId == destinationCountryId)
            .Select(r => new { r.VisaRequirementType, Passport = r.PassportCountry.Name, Destination = r.DestinationCountry.Name })
            .FirstOrDefaultAsync(cancellationToken);
        if (entry is null) return Unknown;
        return new VisaVerdict(true,
            entry.VisaRequirementType is VisaRequirementType.EVisa or VisaRequirementType.EmbassyVisa,
            entry.VisaRequirementType == VisaRequirementType.Prohibited,
            entry.Passport, entry.Destination, entry.VisaRequirementType);
    }

    /// <summary>Whether a trip needs a visa now: its own flag, or — no override recorded — the register (a passport may
    /// have been recorded after the trip was raised).</summary>
    public static async Task<bool> NeedsVisaAsync(IUnitOfWork unitOfWork, StaffTravelRequest request, CancellationToken cancellationToken)
    {
        if (request.RequiresVisa) return true;
        if (!string.IsNullOrWhiteSpace(request.VisaOverrideReason)) return false;
        return (await VisaVerdictAsync(unitOfWork, request.TenantId, request.EmployeeId, request.DestinationCountryId, cancellationToken)).Needs;
    }

    /// <summary>O-16: an insurance policy on the trip covers every day of it.</summary>
    public static Task<bool> InsuranceCoversAsync(IUnitOfWork unitOfWork, StaffTravelRequest request, CancellationToken cancellationToken)
        => unitOfWork.Repository<StaffTravelInsurancePolicy>()
            .GetQueryable(p => p.TenantId == request.TenantId && p.StaffTravelRequestId == request.Id && !p.IsDeleted
                            && p.CoverageStart <= request.TravelStartDate && p.CoverageEnd >= request.TravelEndDate)
            .AnyAsync(cancellationToken);

    /// <summary>
    /// D-37: a trip is Critical when its own level is, or the latest risk assessment still valid at departure says so — and
    /// then that assessment must exist and be acknowledged by the traveller. Null when the trip may be ticketed; otherwise
    /// why not.
    /// </summary>
    public static async Task<string?> CriticalUnacknowledgedAsync(IUnitOfWork unitOfWork, StaffTravelRequest request, CancellationToken cancellationToken)
    {
        var latest = await unitOfWork.Repository<StaffTravelRiskAssessment>()
            .GetQueryable(a => a.TenantId == request.TenantId && a.StaffTravelRequestId == request.Id && !a.IsDeleted
                            && (a.ValidUntil == null || a.ValidUntil >= request.TravelStartDate))
            .OrderByDescending(a => a.AssessedAt ?? a.CreatedAt)
            .Select(a => new { a.RiskLevel, a.EmployeeAcknowledged })
            .FirstOrDefaultAsync(cancellationToken);
        var critical = request.RiskLevel == TravelRiskLevel.Critical || latest?.RiskLevel == TravelRiskLevel.Critical;
        if (!critical) return null;
        if (latest is null)
            return $"Travel request {request.RequestNumber} is rated Critical and has no risk assessment — the flight is ticketed " +
                   "once one is recorded and the traveller has acknowledged it.";
        return latest.EmployeeAcknowledged
            ? null
            : $"Travel request {request.RequestNumber} is rated Critical, and the traveller has not acknowledged its risk " +
              "assessment — the flight is ticketed once they have (on their portal).";
    }

    /// <summary>O-16's warnings at submission — the ticket is refused later, not the submission now.</summary>
    public static async Task<List<string>> SubmissionWarningsAsync(IUnitOfWork unitOfWork, StaffTravelRequest request, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        if (request.IsInternational && !await InsuranceCoversAsync(unitOfWork, request, cancellationToken))
            warnings.Add("No travel insurance on the trip covers every day of it yet — record the cover on its Compliance tab; " +
                         "the flight is not ticketed until it is.");
        var expiry = await unitOfWork.Repository<StaffTravelDocument>()
            .GetQueryable(d => d.TenantId == request.TenantId && d.EmployeeId == request.EmployeeId && !d.IsDeleted
                            && d.DocumentType == TravelDocumentType.Passport && d.IsPrimary)
            .OrderByDescending(d => d.ExpiryDate)
            .Select(d => d.ExpiryDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (request.IsInternational && expiry is DateOnly expires && expires < request.TravelEndDate.AddMonths(PassportValidityMonths))
            warnings.Add($"The traveller's passport expires on {expires:dd MMM yyyy}, less than {PassportValidityMonths} months " +
                         $"after the return ({request.TravelEndDate:dd MMM yyyy}) — many countries refuse entry on it.");
        return warnings;
    }
}
