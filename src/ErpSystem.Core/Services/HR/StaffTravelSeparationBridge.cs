using System.Globalization;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.StaffTravel;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Staff travel's side of an employee's separation (travel final closure, lane 9, slice 9c — O-14, D-56, D-57, D-58):
/// what the leaver still has open in travel, whether a trip falls after they have gone, and the advance their final
/// settlement recovered. Travel owns what counts as open and how an advance settles; separation owns the form, the
/// approval and the settlement — as <see cref="AssetCustodyClearanceBridge"/> keeps HR Assets' answer in HR Assets.
/// </summary>
/// <remarks>Tenant-explicit throughout: every read takes the tenant, so it answers alike inside any caller.</remarks>
public class StaffTravelSeparationBridge
{
    private readonly IUnitOfWork _unitOfWork;

    public StaffTravelSeparationBridge(IUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    /// <summary>A separation from its approval on — the leaver's day is settled and their trips are answered for.</summary>
    public static readonly SeparationStatus[] Decided =
    {
        SeparationStatus.Approved, SeparationStatus.ClearanceInProgress, SeparationStatus.ClearanceCompleted,
        SeparationStatus.SettlementPending, SeparationStatus.SettlementUnderReview, SeparationStatus.SettlementApproved,
        SeparationStatus.Completed,
    };

    /// <summary>Trips the separation's approval cancels (D-57): nothing booked or paid hangs off them yet.</summary>
    public static readonly StaffTravelRequestStatus[] CancelledOnApproval =
    {
        StaffTravelRequestStatus.Draft, StaffTravelRequestStatus.Submitted, StaffTravelRequestStatus.ReturnedForRevision,
    };

    private static string Day(DateOnly d) => d.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    // ---- D-56: the clearance's Travel block ----------------------------------------------------------------------

    /// <summary>
    /// The leaver's staff travel, read live: every trip not cancelled, rejected or closed, with its live bookings; every
    /// advance not settled, written off, rejected or cancelled; every claim not paid or rejected. Advisory — it is no
    /// clearance line and blocks nothing; the money settles through the final settlement.
    /// </summary>
    /// <param name="separationDecided">Whether the separation has been approved — so a trip its approval should have
    /// cancelled is named as not cancelled.</param>
    public async Task<SeparationTravelDto> ReadAsync(
        Guid tenantId, Guid employeeId, bool separationDecided, CancellationToken cancellationToken = default)
    {
        var trips = await _unitOfWork.Repository<StaffTravelRequest>()
            .GetQueryable(t => t.TenantId == tenantId && t.EmployeeId == employeeId
                            && t.Status != StaffTravelRequestStatus.Cancelled && t.Status != StaffTravelRequestStatus.Rejected
                            && t.Status != StaffTravelRequestStatus.Closed)
            .OrderBy(t => t.TravelStartDate)
            .Select(t => new { t.Id, t.RequestNumber, t.Status, t.TravelStartDate, t.TravelEndDate, t.DestinationCity })
            .ToListAsync(cancellationToken);
        var tripIds = trips.Select(t => t.Id).ToList();

        var live = new Dictionary<Guid, int>();
        if (tripIds.Count > 0)
        {
            void Count(IEnumerable<(Guid Trip, TravelBookingStatus Status)> rows)
            {
                foreach (var (trip, status) in rows.Where(r => StaffTravelBookingRules.IsLive(r.Status)))
                    live[trip] = live.GetValueOrDefault(trip) + 1;
            }
            Count((await _unitOfWork.Repository<StaffTravelFlightBooking>()
                .GetQueryable(b => b.TenantId == tenantId && tripIds.Contains(b.StaffTravelRequestId))
                .Select(b => new { b.StaffTravelRequestId, b.Status }).ToListAsync(cancellationToken))
                .Select(b => (b.StaffTravelRequestId, b.Status)));
            Count((await _unitOfWork.Repository<StaffTravelHotelBooking>()
                .GetQueryable(b => b.TenantId == tenantId && tripIds.Contains(b.StaffTravelRequestId))
                .Select(b => new { b.StaffTravelRequestId, b.Status }).ToListAsync(cancellationToken))
                .Select(b => (b.StaffTravelRequestId, b.Status)));
            Count((await _unitOfWork.Repository<StaffTravelGroundTransport>()
                .GetQueryable(b => b.TenantId == tenantId && tripIds.Contains(b.StaffTravelRequestId))
                .Select(b => new { b.StaffTravelRequestId, b.Status }).ToListAsync(cancellationToken))
                .Select(b => (b.StaffTravelRequestId, b.Status)));
            Count((await _unitOfWork.Repository<StaffTravelCarRentalBooking>()
                .GetQueryable(b => b.TenantId == tenantId && tripIds.Contains(b.StaffTravelRequestId))
                .Select(b => new { b.StaffTravelRequestId, b.Status }).ToListAsync(cancellationToken))
                .Select(b => (b.StaffTravelRequestId, b.Status)));
        }

        var advances = await _unitOfWork.Repository<StaffTravelAdvance>()
            .GetQueryable(a => a.TenantId == tenantId && a.EmployeeId == employeeId
                            && a.Status != TravelAdvanceStatus.FullySettled && a.Status != TravelAdvanceStatus.WrittenOff
                            && a.Status != TravelAdvanceStatus.Rejected && a.Status != TravelAdvanceStatus.Cancelled)
            .OrderBy(a => a.AdvanceNumber)
            .Select(a => new
            {
                a.Id, a.StaffTravelRequestId, a.AdvanceNumber, a.Status, a.CurrencyCode, a.RequestedAmount, a.ApprovedAmount,
                a.UnsettledAmount,
            })
            .ToListAsync(cancellationToken);
        var claims = await _unitOfWork.Repository<StaffTravelExpenseClaim>()
            .GetQueryable(c => c.TenantId == tenantId && c.EmployeeId == employeeId
                            && c.Status != TravelClaimStatus.Paid && c.Status != TravelClaimStatus.Rejected)
            .OrderBy(c => c.ClaimNumber)
            .Select(c => new { c.Id, c.StaffTravelRequestId, c.ClaimNumber, c.Status, c.CurrencyCode, c.TotalClaimed })
            .ToListAsync(cancellationToken);
        var numbers = trips.ToDictionary(t => t.Id, t => t.RequestNumber);

        return new SeparationTravelDto
        {
            Trips = trips.Select(t => new SeparationTravelTripDto
            {
                Id = t.Id,
                RequestNumber = t.RequestNumber,
                Status = t.Status.ToString(),
                TravelStartDate = t.TravelStartDate,
                TravelEndDate = t.TravelEndDate,
                Destination = t.DestinationCity,
                LiveBookings = live.GetValueOrDefault(t.Id),
                Note = CancelledOnApproval.Contains(t.Status)
                    ? separationDecided
                        ? "Not cancelled by the separation's approval — cancel it on its page."
                        : "Cancelled when the separation is approved."
                    : t.Status is StaffTravelRequestStatus.Approved or StaffTravelRequestStatus.InProgress
                        ? "Goes ahead unless the travel desk cancels it — bookings and money may hang off it."
                        : "Completed — its claims and advances settle as usual, and the trip closes once they have.",
            }).ToList(),
            Advances = advances.Select(a => new SeparationTravelAdvanceDto
            {
                Id = a.Id,
                TripId = a.StaffTravelRequestId,
                TripNumber = numbers.GetValueOrDefault(a.StaffTravelRequestId),
                AdvanceNumber = a.AdvanceNumber,
                Status = a.Status.ToString(),
                CurrencyCode = a.CurrencyCode,
                Amount = a.ApprovedAmount ?? a.RequestedAmount,
                Unsettled = a.UnsettledAmount,
                Note = StaffTravelAdvanceRules.IsUndisbursed(a.Status)
                    ? "Not paid out — withdrawn with its trip if the trip is cancelled."
                    : "Cash out — deducted from the final settlement, and settled in travel when the settlement is released.",
            }).ToList(),
            Claims = claims.Select(c => new SeparationTravelClaimDto
            {
                Id = c.Id,
                TripId = c.StaffTravelRequestId,
                TripNumber = numbers.GetValueOrDefault(c.StaffTravelRequestId),
                ClaimNumber = c.ClaimNumber,
                Status = c.Status.ToString(),
                CurrencyCode = c.CurrencyCode,
                Amount = c.TotalClaimed,
            }).ToList(),
        };
    }

    // ---- D-57: a trip after the leaver has gone ------------------------------------------------------------------

    /// <summary>
    /// The day an employee leaves under an approved separation, when <paramref name="tripStart"/> falls after it — or null
    /// when it does not, or they have no approved separation. The day is the separation's effective date, else its last
    /// working day, as the settlement reads it.
    /// </summary>
    public static async Task<DateOnly?> LeavesBeforeAsync(
        IUnitOfWork unitOfWork, Guid tenantId, Guid employeeId, DateOnly tripStart, CancellationToken cancellationToken)
    {
        var days = await unitOfWork.Repository<EmployeeSeparation>()
            .GetQueryable(s => s.TenantId == tenantId && s.EmployeeId == employeeId && Decided.Contains(s.Status))
            .Select(s => s.EffectiveDate ?? s.LastWorkingDay)
            .ToListAsync(cancellationToken);
        var gone = days.Where(d => d is not null).Select(d => d!.Value).OrderBy(d => d).FirstOrDefault();
        return gone != default && tripStart > gone ? gone : null;
    }

    // ---- D-58: the advance the final settlement recovered --------------------------------------------------------

    /// <summary>
    /// Settles in travel each advance a released settlement recovered (a <see cref="SettlementLineCategory.TravelAdvanceRecovery"/>
    /// line naming it): what it deducted, up to what is still out, goes to the advance's settled amount, its status follows,
    /// and its trip carries an internal note. <b>No travel posting</b> — the settlement's own journal credits the staff
    /// advances receivable; a refund through travel would credit it twice. Tracked; the caller saves, inside the
    /// settlement's posting transaction, so travel's change commits or rolls back with Finance's journal.
    /// </summary>
    /// <returns>How many advances it settled.</returns>
    public static async Task<int> ApplyFinalSettlementRecoveryAsync(
        IUnitOfWork unitOfWork, Guid tenantId, IEnumerable<SeparationSettlementLine> lines, string separationNumber,
        Guid? actorEmployeeId, ILogger logger, CancellationToken cancellationToken)
    {
        var recovered = lines
            .Where(l => l.Category == SettlementLineCategory.TravelAdvanceRecovery && l.SourceTravelAdvanceId is not null
                     && l.Amount is > 0m)
            .GroupBy(l => l.SourceTravelAdvanceId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Amount!.Value));
        if (recovered.Count == 0) return 0;

        var ids = recovered.Keys.ToList();
        // ⚠ Idempotent by construction: the posting runs this inside an execution strategy that may run it again after a
        // transient failure, and a rollback does not undo values EF holds on tracked rows. So the amounts are read as the
        // DATABASE has them (no tracking), set absolutely, and the row marked for update — never incremented in place.
        var asStored = await unitOfWork.Repository<StaffTravelAdvance>()
            .GetQueryable(a => a.TenantId == tenantId && ids.Contains(a.Id))
            .AsNoTracking()
            .Select(a => new { a.Id, a.SettledAmount, a.UnsettledAmount })
            .ToDictionaryAsync(a => a.Id, cancellationToken);
        var advances = await unitOfWork.Repository<StaffTravelAdvance>()
            .GetQueryable(a => a.TenantId == tenantId && ids.Contains(a.Id))
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var settled = 0;
        foreach (var advance in advances)
        {
            var stored = asStored[advance.Id];
            var deducted = recovered[advance.Id];
            var applied = Math.Min(deducted, stored.UnsettledAmount);
            if (applied <= 0m)
            {
                logger.LogWarning(
                    "Separation {Separation}: its settlement deducted {Currency} {Deducted} for travel advance {Advance}, which has nothing outstanding any more",
                    separationNumber, advance.CurrencyCode, deducted, advance.AdvanceNumber);
                continue;
            }

            advance.SettledAmount = stored.SettledAmount + applied;
            advance.UnsettledAmount = (advance.ApprovedAmount ?? 0m) - advance.SettledAmount;
            advance.Status = StaffTravelAdvanceRules.SettlementStatus(advance, DateOnly.FromDateTime(now));
            advance.UpdatedAt = now;
            advance.UpdatedBy = $"separation {separationNumber}";
            await unitOfWork.Repository<StaffTravelAdvance>().UpdateAsync(advance);
            settled++;

            if (actorEmployeeId is Guid author)
                await unitOfWork.Repository<StaffTravelRequestComment>().AddAsync(new StaffTravelRequestComment
                {
                    TenantId = tenantId,
                    StaffTravelRequestId = advance.StaffTravelRequestId,
                    AuthorId = author,
                    CommentType = TravelRequestCommentType.InternalNote,
                    IsVisibleToTraveller = false,
                    Body = $"Advance {advance.AdvanceNumber}: {advance.CurrencyCode} {applied:N2} recovered from the final settlement of " +
                           $"separation {separationNumber}, released on {Day(DateOnly.FromDateTime(now))}" +
                           (applied < deducted ? $" (it deducted {advance.CurrencyCode} {deducted:N2}; only {applied:N2} was still out)" : string.Empty) +
                           ". Settled in travel without a travel posting — the settlement's journal is the posting.",
                    CreatedBy = $"separation {separationNumber}",
                });
            else
                logger.LogWarning(
                    "Separation {Separation}: advance {Advance} settled from the final settlement with no note — the releasing login has no employee record",
                    separationNumber, advance.AdvanceNumber);
        }
        return settled;
    }
}
