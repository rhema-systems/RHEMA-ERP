// ⚠ `Entities.HR.Assets`, not `Entities.HR`. With the shorter one imported, `Assets` resolves to
// `ErpSystem.Core.Entities.HR.Assets` from inside this namespace and `Assets.AssetAssignment`
// compiles while the bare name does not — a using that looks right and half works.
using ErpSystem.Core.Entities.HR.Assets;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.HR.Assets;

/// <summary>
/// One thing an employee still holds, as the exit clearance needs to see it — area 16, slice 10.
/// </summary>
/// <remarks>
/// Flattened on purpose. The clearance form <b>snapshots</b> what it is told (area 9b's rule: a
/// form is evidence about a particular exit, and editing the register afterwards must not rewrite
/// what somebody signed), so what crosses this seam is a set of values and not a live entity that
/// the separation service could accidentally write through.
/// </remarks>
public sealed record AssetCustodyLine
{
    public required Guid AssignmentId { get; init; }

    public required string AssignmentNumber { get; init; }

    public required Guid AssetId { get; init; }

    public required string AssetNumber { get; init; }

    public required string AssetName { get; init; }

    public string? AssetTypeName { get; init; }

    public required DateOnly AssignmentDate { get; init; }

    public DateOnly? ExpectedReturnDate { get; init; }

    public required AssignmentStatus Status { get; init; }

    /// <summary>
    /// The employee physically has it — <c>Active</c> or <c>Overdue</c>.
    /// </summary>
    /// <remarks>
    /// The distinction the gate turns on. A <c>Lost</c> or <c>Damaged</c> custody is equally
    /// unreturned, but no return will ever close it: telling somebody to "record the return" of a
    /// stolen laptop is an instruction they cannot follow. Both are outstanding; only one of them
    /// can be answered by handing the thing back.
    /// </remarks>
    public required bool StillHeld { get; init; }

    /// <summary>
    /// A charge against this custody that has been <b>decided</b> and not yet fully collected.
    /// </summary>
    /// <remarks>
    /// Approved or Recovering with <c>AmountRecovered &lt; AssessedAmount</c> — the module's own
    /// definition of outstanding (<c>AssetSurchargeRepository.GetOutstandingAsync</c>), reused
    /// rather than restated so the exit statement and the surcharge register cannot disagree about
    /// what is owed.
    /// </remarks>
    public decimal? OutstandingSurcharge { get; init; }

    public string? SurchargeCurrencyCode { get; init; }

    public Guid? SurchargeId { get; init; }

    public string? SurchargeNumber { get; init; }

    /// <summary>
    /// A charge exists against this custody and its fate is not yet settled.
    /// </summary>
    /// <remarks>
    /// Wider than <see cref="OutstandingSurcharge"/> on purpose, and it exists for the gate rather
    /// than for the money. A charge still in Draft, with the employee, or awaiting approval is not
    /// a debt — nobody has decided it, and deducting it from a final settlement would take money no
    /// approver has authorised. But it is equally not <i>nothing</i>: a line answered "Cleared"
    /// while a charge against that employee is still being decided says the organisation has no
    /// further claim, at the exact moment it is deciding one.
    /// </remarks>
    public required bool HasUndecidedCharge { get; init; }

    /// <summary>How the line reads on a clearance form.</summary>
    public string Label => $"{AssetName} ({AssetNumber})";

    /// <summary>
    /// The custody has not ended - nothing was handed back and no transfer closed it.
    /// </summary>
    /// <remarks>
    /// ⚠ <c>Returned</c> and <c>Transferred</c> are the only two endings that release the employee.
    /// Slice 4 added <c>Transferred</c> precisely so that passing an asset to a colleague through
    /// an approved transfer would not have to be recorded as a return; it ends the custody just as
    /// completely. Slice 7's incident route deliberately leaves <c>ReturnDate</c> null on a loss -
    /// the comment there names this query - so <b>never</b> ask this question with
    /// <c>ReturnDate == null</c>, which is true of an Active custody too and false of nothing that
    /// matters.
    /// </remarks>
    public bool Outstanding => IsOutstanding(Status);

    /// <summary>The predicate behind <see cref="Outstanding"/>, usable before a line is built.</summary>
    public static bool IsOutstanding(AssignmentStatus status)
        => status is not (AssignmentStatus.Returned or AssignmentStatus.Transferred);
}

/// <summary>
/// HR Assets' read-only window onto what an employee still holds, for the exit clearance —
/// FR-HR-183, area 16 slice 10, closing area 9b's decision D4.
/// </summary>
/// <remarks>
/// <para><b>Why a bridge and not a query.</b> <c>SeparationService</c> already reaches across HR
/// to read travel advances directly, and that was fine because "an advance the employee still
/// owes" is a single obvious predicate. Custody is not: what counts as unreturned spans four
/// assignment statuses, two of which exist only because slice 7 added them, and what counts as
/// money owed is a surcharge state machine with eight members and a right of reply. Restating
/// either of those inside the separation service is how two modules come to disagree about whether
/// a leaver owes for a laptop. Area 16 answers the question; area 9b asks it.</para>
///
/// <para><b>Read-only, and it writes nothing back.</b> Clearance does not return assets, close
/// custodies or settle charges — it <i>reports</i> them and refuses to be signed off while they
/// stand. Returning the asset happens in HR Assets, on the route that already does it, with the
/// condition check and the rent closure that come with it. A clearance form that could quietly
/// close a custody would be a second door onto custody with none of the first one's rules.</para>
///
/// <para>Not an interface of its own, following <c>StaffTravelCurrencyBridge</c>: it is a thin,
/// single-purpose read that one service needs, and a bespoke abstraction would only obscure where
/// the data comes from.</para>
/// </remarks>
public sealed class AssetCustodyClearanceBridge
{
    private readonly IUnitOfWork _unitOfWork;

    public AssetCustodyClearanceBridge(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    private static bool IsStillHeld(AssignmentStatus status)
        => status is AssignmentStatus.Active or AssignmentStatus.Overdue;

    /// <summary>Everything the employee has not given back, most recently issued first.</summary>
    public async Task<IReadOnlyList<AssetCustodyLine>> GetOutstandingCustodyAsync(
        Guid tenantId, Guid employeeId, CancellationToken cancellationToken = default)
    {
        var assignments = await ScopedAssignments(tenantId)
            .Where(a => a.EmployeeId == employeeId)
            .ToListAsync(cancellationToken);

        var outstanding = assignments.Where(a => AssetCustodyLine.IsOutstanding(a.Status)).ToList();
        if (outstanding.Count == 0) return Array.Empty<AssetCustodyLine>();

        var ids = outstanding.Select(a => a.Id).ToList();
        var charges = await _unitOfWork.Repository<AssetSurcharge>().GetQueryable()
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && ids.Contains(s.AssignmentId))
            .ToListAsync(cancellationToken);

        return outstanding
            .OrderByDescending(a => a.AssignmentDate)
            .ThenBy(a => a.Asset.AssetName)
            .Select(a => ToLine(a, charges.Where(c => c.AssignmentId == a.Id).ToList()))
            .ToList();
    }

    /// <summary>
    /// Named custodies as they stand now, whether or not they are still outstanding. Assignments
    /// that are not this tenant's, or that have been deleted out from under a clearance line, are
    /// simply absent from the result.
    /// </summary>
    /// <remarks>
    /// <para>Asked at the moment a clearance form is read or a line is answered, rather than read
    /// off the snapshot: the form is generated when the separation is approved, and the whole
    /// notice period sits between that and somebody signing the line. An asset returned on the
    /// last working day must clear, and one issued after the form was drawn must not be missed
    /// because it was.</para>
    ///
    /// <para>Batched rather than one call per line because a clearance form is read as a whole,
    /// and a per-item lookup behind a list read is how a seven-line form becomes fifteen queries.</para>
    /// </remarks>
    public async Task<IReadOnlyDictionary<Guid, AssetCustodyLine>> GetCustodiesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> assignmentIds, CancellationToken cancellationToken = default)
    {
        if (assignmentIds is null || assignmentIds.Count == 0)
            return new Dictionary<Guid, AssetCustodyLine>();

        var ids = assignmentIds.Distinct().ToList();

        var assignments = await ScopedAssignments(tenantId)
            .Where(a => ids.Contains(a.Id))
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0) return new Dictionary<Guid, AssetCustodyLine>();

        var found = assignments.Select(a => a.Id).ToList();
        var charges = await _unitOfWork.Repository<AssetSurcharge>().GetQueryable()
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted && found.Contains(s.AssignmentId))
            .ToListAsync(cancellationToken);

        return assignments.ToDictionary(
            a => a.Id,
            a => ToLine(a, charges.Where(c => c.AssignmentId == a.Id).ToList()));
    }

    /// <summary>One custody as it stands now. Null when it cannot be found. See <see cref="GetCustodiesAsync"/>.</summary>
    public async Task<AssetCustodyLine?> GetCustodyAsync(
        Guid tenantId, Guid assignmentId, CancellationToken cancellationToken = default)
        => (await GetCustodiesAsync(tenantId, new[] { assignmentId }, cancellationToken))
            .TryGetValue(assignmentId, out var line) ? line : null;

    /// <summary>
    /// ⚠ The <c>.Include</c> is load-bearing, not decoration. Every field a clearance line shows —
    /// the asset's name, its number, its type — comes through it, and this area has produced a
    /// blank column six separate times from a mapping written without the include that feeds it
    /// (D-n, D-o, D-o(b), D-t, D-w and the requisition number in slice 3).
    /// </summary>
    private IQueryable<AssetAssignment> ScopedAssignments(Guid tenantId)
        => _unitOfWork.Repository<AssetAssignment>().GetQueryable()
            .Include(a => a.Asset).ThenInclude(x => x.AssetType)
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && !a.IsDeleted);

    private static AssetCustodyLine ToLine(AssetAssignment a, List<AssetSurcharge> charges)
    {
        // The module's own definition of outstanding, from AssetSurchargeRepository.
        var decided = charges
            .Where(c => c.Status is AssetSurchargeStatus.Approved or AssetSurchargeStatus.Recovering
                        && c.AmountRecovered < c.AssessedAmount)
            .OrderByDescending(c => c.AssessedAmount - c.AmountRecovered)
            .FirstOrDefault();

        var undecided = charges.Any(c => c.Status is AssetSurchargeStatus.Draft
                                                  or AssetSurchargeStatus.WithEmployee
                                                  or AssetSurchargeStatus.Submitted);

        return new AssetCustodyLine
        {
            AssignmentId = a.Id,
            AssignmentNumber = a.AssignmentNumber,
            AssetId = a.AssetId,
            AssetNumber = a.Asset.AssetNumber,
            AssetName = a.Asset.AssetName,
            AssetTypeName = a.Asset.AssetType?.Name,
            AssignmentDate = a.AssignmentDate,
            ExpectedReturnDate = a.ExpectedReturnDate,
            Status = a.Status,
            StillHeld = IsStillHeld(a.Status),
            OutstandingSurcharge = decided is null ? null : decided.AssessedAmount - decided.AmountRecovered,
            SurchargeCurrencyCode = decided?.CurrencyCode,
            SurchargeId = decided?.Id,
            SurchargeNumber = decided?.SurchargeNumber,
            HasUndecidedCharge = undecided,
        };
    }
}
