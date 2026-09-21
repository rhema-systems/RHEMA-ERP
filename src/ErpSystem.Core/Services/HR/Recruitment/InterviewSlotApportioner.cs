namespace ErpSystem.Core.Services.HR.Recruitment;

/// <summary>
/// Spreads a day's candidates across an interview window — round 4, lane C.
/// </summary>
/// <remarks>
/// <para><b>Pure and I/O-free on purpose.</b> Everything here is arithmetic over times, so the rule
/// that decides whether a panel can see eleven people before five o'clock is readable in one place
/// and assertable to the minute. The service above it does the loading and the saving.</para>
///
/// <para><b>What was here before: nothing.</b> <c>JobInterviewee.SlotStartTime</c> and
/// <c>SlotEndTime</c> have existed since the port and <c>CreateJobInterviewDto</c> has accepted
/// per-candidate slots all along — but no code ever computed one, validated one, or noticed when a
/// reschedule left them behind. The columns were a promise the product never kept.</para>
///
/// <para><b>The feasibility answer is the point, not a by-product.</b> A recruiter booking nine
/// people into a two-hour window needs to be told <i>before</i> the invitations go out that four of
/// them will not fit, and when the rest could be seen instead. An apportioner that silently
/// produced four slots and dropped five would be worse than no apportioner.</para>
/// </remarks>
public static class InterviewSlotApportioner
{
    /// <summary>A rest period inside the interview window that no candidate may be booked into.</summary>
    public sealed record Break(TimeSpan Start, TimeSpan End, string? Label);

    /// <summary>One candidate's place in the day.</summary>
    public sealed record Slot(Guid ApplicationId, TimeSpan Start, TimeSpan End, int Ordinal);

    /// <summary>What the day looks like once everyone has been placed, or found not to fit.</summary>
    public sealed record Plan(
        IReadOnlyList<Slot> Slots,
        IReadOnlyList<Guid> Unplaced,
        IReadOnlyList<Break> Breaks,
        bool AllFit,
        TimeSpan? FirstFreeAfterWindow,
        string Summary);

    // The bounds a caller's numbers are held to, so a typo cannot produce 4,000 slots.
    //
    // ⚠ Single-sourced from the DTO's InterviewSlotLimits rather than restated here. The DTO needs
    // them as compile-time constants for its [Range] attributes, and a second copy in this file
    // would be two numbers that must agree and no compiler to make them — which is how a payload
    // comes to pass validation and then be refused by the engine, with two different messages.
    private const int MinSlotMinutes = ErpSystem.Core.DTOs.HR.InterviewSlotLimits.MinSlotMinutes;
    private const int MaxSlotMinutes = ErpSystem.Core.DTOs.HR.InterviewSlotLimits.MaxSlotMinutes;
    private const int MaxBufferMinutes = ErpSystem.Core.DTOs.HR.InterviewSlotLimits.MaxBufferMinutes;

    /// <summary>
    /// Places <paramref name="applicationIds"/> in order across the window, stepping over breaks.
    /// </summary>
    /// <param name="slotMinutes">How long each candidate gets.</param>
    /// <param name="bufferMinutes">
    /// Turnaround between candidates — notes written, the next person fetched. Applied BETWEEN
    /// slots and never after the last one, so a buffer cannot by itself push the day over its end.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">The window or the numbers are unusable.</exception>
    public static Plan Apportion(
        TimeSpan windowStart,
        TimeSpan windowEnd,
        int slotMinutes,
        int bufferMinutes,
        IEnumerable<Break> breaks,
        IReadOnlyList<Guid> applicationIds)
    {
        if (windowEnd <= windowStart)
            throw new ArgumentOutOfRangeException(nameof(windowEnd),
                "The interview must end after it starts.");
        if (slotMinutes is < MinSlotMinutes or > MaxSlotMinutes)
            throw new ArgumentOutOfRangeException(nameof(slotMinutes),
                $"A slot must be between {MinSlotMinutes} and {MaxSlotMinutes} minutes.");
        if (bufferMinutes is < 0 or > MaxBufferMinutes)
            throw new ArgumentOutOfRangeException(nameof(bufferMinutes),
                $"The gap between candidates must be between 0 and {MaxBufferMinutes} minutes.");

        var slotLength = TimeSpan.FromMinutes(slotMinutes);
        var buffer = TimeSpan.FromMinutes(bufferMinutes);
        var ordered = NormaliseBreaks(breaks, windowStart, windowEnd);

        var placed = new List<Slot>();
        var unplaced = new List<Guid>();
        var cursor = windowStart;
        var ordinal = 1;

        foreach (var applicationId in applicationIds ?? Array.Empty<Guid>())
        {
            // Step over any break the slot would run into. Looped rather than checked once,
            // because clearing one break can land the cursor inside the next.
            cursor = SkipBreaks(cursor, slotLength, ordered);

            var end = cursor + slotLength;
            if (end > windowEnd)
            {
                // The window is full. Everyone from here on is unplaced — and the loop CONTINUES
                // rather than breaking, so the caller is told how many did not fit, not merely
                // that somebody did not.
                unplaced.Add(applicationId);
                continue;
            }

            placed.Add(new Slot(applicationId, cursor, end, ordinal++));
            cursor = end + buffer;
        }

        // Where the day ran out, say when it could resume: the first moment after the window that
        // is not inside a break. The date is the caller's to choose; this is the time of day.
        TimeSpan? continuation = unplaced.Count > 0
            ? SkipBreaks(windowEnd, slotLength, ordered)
            : null;

        return new Plan(
            placed,
            unplaced,
            ordered,
            AllFit: unplaced.Count == 0,
            FirstFreeAfterWindow: continuation,
            Summary: Describe(placed.Count, unplaced.Count, slotMinutes, ordered.Count));
    }

    /// <summary>
    /// Moves <paramref name="cursor"/> forward until a slot of <paramref name="slotLength"/> starting
    /// there would not overlap a break.
    /// </summary>
    /// <remarks>
    /// ⚠ A slot must not merely START outside a break — it must not RUN INTO one. Testing only the
    /// start is the obvious version of this and it books somebody's interview straight through
    /// lunch. The loop is bounded by the break count, which is finite and normalised.
    /// </remarks>
    private static TimeSpan SkipBreaks(TimeSpan cursor, TimeSpan slotLength, IReadOnlyList<Break> breaks)
    {
        var moved = true;
        while (moved)
        {
            moved = false;
            foreach (var b in breaks)
            {
                // Overlap, not containment: [cursor, cursor+slot) against [b.Start, b.End).
                if (cursor < b.End && b.Start < cursor + slotLength)
                {
                    cursor = b.End;
                    moved = true;
                    break;
                }
            }
        }
        return cursor;
    }

    /// <summary>
    /// Clips breaks to the window, drops the ones that fall outside it, and merges any that touch
    /// or overlap — so two adjacent "breaks" cannot make the skip loop oscillate between them.
    /// </summary>
    public static IReadOnlyList<Break> NormaliseBreaks(
        IEnumerable<Break> breaks, TimeSpan windowStart, TimeSpan windowEnd)
    {
        var clipped = (breaks ?? Array.Empty<Break>())
            .Where(b => b is not null && b.End > b.Start)
            .Select(b => new Break(
                b.Start < windowStart ? windowStart : b.Start,
                b.End > windowEnd ? windowEnd : b.End,
                b.Label))
            .Where(b => b.End > b.Start)
            .OrderBy(b => b.Start)
            .ToList();

        var merged = new List<Break>();
        foreach (var b in clipped)
        {
            var last = merged.Count > 0 ? merged[^1] : null;
            if (last is not null && b.Start <= last.End)
            {
                // Keep the earlier label: it is the one the user typed first, and a merged rest
                // period is better described by its opening than by whatever ran into it.
                merged[^1] = new Break(last.Start, b.End > last.End ? b.End : last.End, last.Label);
                continue;
            }
            merged.Add(b);
        }
        return merged;
    }

    private static string Describe(int placed, int unplaced, int slotMinutes, int breakCount)
    {
        var breaks = breakCount switch
        {
            0 => string.Empty,
            1 => " around 1 break",
            _ => $" around {breakCount} breaks",
        };

        if (unplaced == 0)
            return placed == 0
                ? "No candidates to place."
                : $"All {placed} candidate{(placed == 1 ? "" : "s")} fit, "
                  + $"{slotMinutes} minutes each{breaks}.";

        return $"{placed} of {placed + unplaced} candidates fit in this window at {slotMinutes} "
             + $"minutes each{breaks}. The remaining {unplaced} need another session — "
             + "lengthen the window, shorten the slots, or schedule a second day.";
    }
}
