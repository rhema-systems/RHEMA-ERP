using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.HR.Recruitment;

/// <summary>
/// The shortlisting scoring engine, on its own so that everything which claims to run "the same
/// criteria" demonstrably does.
/// </summary>
/// <remarks>
/// <para><b>Round 4, lane B.</b> All of this lived as private members of
/// <c>JobApplicationService</c>, reachable only by an application against a vacancy. Lane B needs
/// the identical rules over a TALENT POOL member, who has no application — and the one thing that
/// must not happen is a second copy of the rubric drifting from the first. So the engine moved out
/// whole, and both callers use it: an application scores through
/// <see cref="ScoringCandidateView.FromSnapshot"/> or <see cref="ScoringCandidateView.FromEntity"/>,
/// a pool member through <see cref="ScoringCandidateView.FromCandidate"/>.</para>
///
/// <para>The move is a move, not a rewrite: every rule, every comment and every round-4 lane A
/// repair is the code that was audited there. What is new is <see cref="Score"/>, which lifts the
/// aggregation loop — evaluate all, a mandatory miss scores zero, nothing measurable scores
/// <c>null</c> — out of the application path so the pool cannot answer those three questions
/// differently.</para>
///
/// <para>⚠ <see cref="EvaluateCriterion"/> is pure and synchronous. The one field that may need a
/// database read, <see cref="ScoringCandidateView.GeoAreaPath"/>, is resolved by the caller before
/// scoring — see the back-fill in <c>JobApplicationService.EvaluateLoadedApplicationScoreAsync</c>
/// and the <c>Include</c> on the pool read.</para>
/// </remarks>
public static class ShortlistingEvaluator
{
    /// <summary>
    /// Scores one candidate against a vacancy's live criteria, applying the three aggregation rules
    /// both callers must share.
    /// </summary>
    /// <returns>
    /// <c>Score</c> is null when nothing could be measured — never 100, and never 0, which would
    /// read as "measured, and failed". <c>TotalWeight</c> counts only the criteria that were
    /// actually evaluated.
    /// </returns>
    /// <remarks>
    /// <para>Every criterion is evaluated even after a mandatory miss, so the stored breakdown is
    /// complete — required for recruiter review and for an algorithmic-decision audit trail.</para>
    ///
    /// <para>⚠ The <c>totalWeight == 0</c> rule is the round 4 lane A repair and the reason this
    /// aggregation is shared rather than copied. Falling through to 100 there handed full marks to
    /// every candidate a criterion could not speak about — which is the ordinary case for a
    /// vacancy screening on an area against people with no address on file.</para>
    ///
    /// <para><paramref name="ladder"/> is the tenant's qualification ladder (round 4, lane Q), which
    /// an "Education level" criterion compares ranks on. Required rather than optional so that no
    /// caller can forget it and quietly score every such criterion as unanswerable.</para>
    /// </remarks>
    public static ShortlistingScoreResult Score(
        IEnumerable<JobShortlistingCriteria> criteria,
        ScoringCandidateView view,
        QualificationLadder ladder)
    {
        var breakdown = new List<CriterionScoreResult>();
        decimal totalWeight = 0m;
        decimal earnedScore = 0m;
        bool allMandatoryPassed = true;

        foreach (var criterion in criteria)
        {
            var result = EvaluateCriterion(criterion, view, ladder);
            breakdown.Add(result);

            if (criterion.IsMandatory && !result.Passed)
                allMandatoryPassed = false;

            // A criterion the engine does not score (Other, an empty one, an unanswerable bound) is
            // left out of the total: it neither lifts nor lowers anybody.
            if (!result.AutoEvaluated) continue;
            totalWeight += criterion.Weight;
            earnedScore += result.WeightedScore;
        }

        if (breakdown.Count == 0)
            return new ShortlistingScoreResult(null, true, 0m, breakdown, HasCriteria: false);

        if (!allMandatoryPassed)
            return new ShortlistingScoreResult(0m, false, totalWeight, breakdown);

        if (totalWeight <= 0)
            return new ShortlistingScoreResult(null, true, 0m, breakdown);

        return new ShortlistingScoreResult(
            Math.Round(earnedScore / totalWeight * 100m, 2), true, totalWeight, breakdown);
    }

    public static CriterionScoreResult EvaluateCriterion(
        JobShortlistingCriteria criterion,
        ScoringCandidateView    view,
        QualificationLadder     ladder)
    {
        bool passed;
        decimal rawScore;
        string? notes = null;
        bool autoEvaluated = true;

        switch (criterion.Type)
        {
            case JobShortlistingCriteriaType.YearsOfExperience:
            {
                (passed, rawScore, notes, autoEvaluated) = EvaluateNumericCriterion(criterion, view.YearsOfExperience, "year(s) of experience");
                break;
            }

            case JobShortlistingCriteriaType.EducationLevel:
            {
                (passed, rawScore, notes, autoEvaluated) = EvaluateEducationLevelCriterion(criterion, view, ladder);
                break;
            }

            case JobShortlistingCriteriaType.Qualification:
            {
                // ID-first: the legacy single catalogue FK still passes immediately when the candidate holds it.
                if (criterion.RequiredQualificationId.HasValue && view.QualificationIds.Contains(criterion.RequiredQualificationId.Value))
                {
                    passed   = true;
                    rawScore = 1m;
                    notes    = $"Qualification matched by catalogue ID ({criterion.RequiredQualificationId.Value}).";
                    break;
                }
                (passed, rawScore, notes, autoEvaluated) = EvaluateListCriterion(criterion, view.QualificationIds, view.QualificationNames, "qualification");
                notes += $" Candidate qualifications: {string.Join(", ", view.QualificationNames)}.";
                break;
            }

            case JobShortlistingCriteriaType.Skill:
            {
                if (criterion.RequiredSkillId.HasValue && view.SkillIds.Contains(criterion.RequiredSkillId.Value))
                {
                    passed   = true;
                    rawScore = 1m;
                    notes    = $"Skill matched by catalogue ID ({criterion.RequiredSkillId.Value}).";
                    break;
                }
                (passed, rawScore, notes, autoEvaluated) = EvaluateListCriterion(criterion, view.SkillIds, view.SkillNames, "skill");
                break;
            }

            case JobShortlistingCriteriaType.Certification:
            {
                // A candidate's certificate carries a name, not a catalogue id (lane C1 added the
                // number, body and expiry; the id is a follow-on), so a catalogue-picked value
                // matches on the mirrored catalogue name — which is exactly why the label is mirrored.
                (passed, rawScore, notes, autoEvaluated) = EvaluateListCriterion(criterion, new HashSet<Guid>(), view.CertificationNames, "certification");
                break;
            }

            case JobShortlistingCriteriaType.Age:
            {
                // ⚠ Round 4, lane A. DateOfBirth is a NON-NULLABLE DateTime on both the candidate
                // and the snapshot, so a candidate HR typed in without one carries DateTime.MinValue
                // — and this line then computed an age of roughly 2,026 years, which passed every
                // minimum and failed every maximum. An unknown age is not an age: the criterion is
                // left out of the score the way Other is, rather than being answered with a number
                // nobody could act on.
                if (view.DateOfBirth == default || view.DateOfBirth.Year <= 1)
                {
                    passed = true;
                    rawScore = 0m;
                    autoEvaluated = false;
                    notes = "The candidate has no date of birth on file, so their age cannot be "
                          + "computed. This criterion is left out of the score.";
                    break;
                }

                decimal ageYears = (decimal)((DateTime.UtcNow - view.DateOfBirth).TotalDays / 365.25);
                (passed, rawScore, notes, autoEvaluated) = EvaluateNumericCriterion(criterion, ageYears, "years old");
                break;
            }

            case JobShortlistingCriteriaType.Gender:
            {
                // The accepted genders are enum members (or "Any"); the candidate's is compared as a
                // member name, never through the text strategies (R-5 fix 6).
                var accepted = RequiredLabels(criterion);
                var mine = view.Gender.ToString().ToLowerInvariant();
                if (accepted.Count == 0)
                {
                    // Round 4, lane A — the same inflation as the empty list criterion. This used
                    // to award full marks to everybody for a criterion stating no preference, which
                    // raised every percentage without separating anyone.
                    passed = true;
                    rawScore = 0m;
                    autoEvaluated = false;
                    notes = "No gender specified; this criterion measures nothing and is left out "
                          + "of the score entirely.";
                }
                else
                {
                    passed = accepted.Contains("any") || accepted.Contains(mine);
                    rawScore = passed ? 1m : 0m;
                    notes = $"Accepted: {string.Join(", ", accepted)}; candidate: {mine}. Informs the score only — never mandatory (D-7).";
                }
                break;
            }

            case JobShortlistingCriteriaType.Language:
            {
                (passed, rawScore, notes, autoEvaluated) = EvaluateListCriterion(criterion, view.LanguageIds, view.LanguageNames, "language");
                notes += $" Candidate languages: {string.Join(", ", view.LanguageNames)}.";
                break;
            }

            case JobShortlistingCriteriaType.Location:
            {
                (passed, rawScore, notes, autoEvaluated) = EvaluateLocationCriterion(criterion, view);
                break;
            }

            default:
            {
                // Other, or a legacy row with no type. Not auto-evaluated: it passes (a person
                // judges it), contributes NOTHING, and its weight is left out of the total. It used
                // to pass with full marks — a mandatory Other could disqualify nobody (R-5 fix 1;
                // § 3 defect 8).
                passed = true;
                rawScore = 0m;
                autoEvaluated = false;
                notes = "Not auto-evaluated; judged by a person off-system. Contributes nothing to the score.";
                break;
            }
        }

        decimal weightedScore = autoEvaluated ? rawScore * criterion.Weight : 0m;

        return new CriterionScoreResult
        {
            CriteriaId = criterion.Id,
            CriteriaName = criterion.CriteriaName,
            IsMandatory = criterion.IsMandatory,
            Type = criterion.Type,
            Weight = criterion.Weight,
            Passed = passed,
            RawScore = Math.Round(rawScore, 4),
            WeightedScore = Math.Round(weightedScore, 4),
            Notes = notes,
            AutoEvaluated = autoEvaluated,
        };
    }

    /// <summary>
    /// The accepted items of a list criterion, ids first and labels second (round 3, lane K; register
    /// row R-8). The value rows are the truth; the legacy comma-separated text is read for rows
    /// written before the lane. A row with a catalogue id matches the candidate's catalogue link
    /// exactly, or its mirrored label under the strategy — so a candidate who typed the same name
    /// still matches. Duplicates collapse.
    /// </summary>
    /// <summary>
    /// The Location criterion — round 4, lane A. Areas from the shared geography tree, matched by
    /// containment, with the pre-tree free-text path kept for candidates who have no area.
    /// </summary>
    /// <remarks>
    /// <para><b>What this replaced, and why each part was wrong.</b> The old arm compared the
    /// criterion's typed labels against the candidate's typed city with a raw
    /// <c>string.Contains</c>:</para>
    /// <list type="number">
    ///   <item><description><b>Only two operators were honoured.</b> Anything that was not
    ///   <c>Equals</c> fell through to substring — so <c>NotEquals</c> <i>inverted nothing</i> and a
    ///   "not Accra" criterion passed Accra candidates, and <c>In</c> behaved as Contains.</description></item>
    ///   <item><description><b>Matching was one-directional</b>, unlike <c>MatchesValue</c> which
    ///   every other list criterion uses. "Accra" matched "Greater Accra"; "Greater Accra" did not
    ///   match "Accra". Which way round it worked was an accident of who typed what.</description></item>
    ///   <item><description><b><c>MatchMode</c> and <c>MatchStrategy</c> were ignored.</b> A
    ///   Location criterion set to "all required" behaved as "any", silently.</description></item>
    ///   <item><description><b>The score was binary.</b> Every other list criterion gives partial
    ///   credit for matching some of several values; this one gave all or nothing.</description></item>
    ///   <item><description><b>An empty criterion scored FULL MARKS</b> — "defaulting to pass" with
    ///   <c>rawScore = 1</c>, lifting every candidate's percentage for a criterion that measured
    ///   nothing. It is now treated the way <c>Other</c> is: excluded from the total entirely, so it
    ///   neither lifts nor lowers anybody.</description></item>
    /// </list>
    ///
    /// <para><b>Containment, not string comparison.</b> <c>GeoArea.Path</c> is
    /// <c>/root/child/leaf</c> and includes the area's own id as its last segment, so a candidate is
    /// inside an accepted area when that area's id appears anywhere in their path. That is what
    /// makes "Greater Accra" match somebody recorded in Tema — the thing the free-text version could
    /// never do.</para>
    ///
    /// <para><b>The text fallback is not legacy debt.</b> Most countries have no geography scheme
    /// loaded, and the anonymous apply form collects a typed city by design. A candidate with no
    /// area is matched on their city, through the same bidirectional <c>MatchesValue</c> the rest of
    /// the engine uses, against the criterion's mirrored labels.</para>
    /// </remarks>
    private static (bool passed, decimal rawScore, string? notes, bool autoEvaluated)
        EvaluateLocationCriterion(JobShortlistingCriteria criterion, ScoringCandidateView view)
    {
        var values = criterion.Values.Where(v => !v.IsDeleted).OrderBy(v => v.SortOrder).ToList();
        var areaIds = values
            .Where(v => v.Kind == ShortlistingValueKind.GeoArea && v.ReferenceId.HasValue)
            .Select(v => v.ReferenceId!.Value)
            .Distinct()
            .ToList();
        var labels = RequiredLabels(criterion);

        if (areaIds.Count == 0 && labels.Count == 0)
            return (true, 0m, "No location specified; this criterion measures nothing and is left "
                            + "out of the score entirely.", false);

        var op = criterion.ComparisonOperator ?? ShortlistingComparisonOperator.In;
        var negated = op == ShortlistingComparisonOperator.NotEquals;

        int matched;
        int considered;
        string basis;

        if (areaIds.Count > 0 && !string.IsNullOrWhiteSpace(view.GeoAreaPath))
        {
            // Exact means "this precise tier"; every other operator means "in, or anywhere under".
            var exactTierOnly = op == ShortlistingComparisonOperator.Equals;
            var path = view.GeoAreaPath!;
            matched = areaIds.Count(id => exactTierOnly
                ? view.GeoAreaId == id
                : PathContainsArea(path, id));
            considered = areaIds.Count;
            basis = exactTierOnly
                ? $"candidate's own area against {considered} listed area(s)"
                : $"candidate's area and its ancestors against {considered} listed area(s)";
        }
        else if (labels.Count > 0)
        {
            // No area on either side — fall back to the city, bidirectionally, honouring the
            // criterion's own match strategy like every other list criterion does.
            matched = labels.Count(l => MatchesValue(view.City, l, criterion.MatchStrategy));
            considered = labels.Count;
            basis = $"candidate's typed city '{view.City}' against {considered} listed name(s) "
                  + $"[{criterion.MatchStrategy}]";
        }
        else
        {
            // Nothing to compare on either side: no usable area, and no label either.
            //
            // ⚠ This is NOT the "candidate has no area" case, which is the common one and is
            // handled by the text branch above — every GeoArea value carries the area's name
            // mirrored onto it at save, so a candidate with only a typed city is measured against
            // that name and can genuinely miss. Scoring such a miss as zero is deliberate:
            // excluding it would let a candidate with no address outrank one who demonstrably does
            // not match, which is the "rewards missing data" fault G-13.2 removed from the talent
            // pool. The round 4 harness asserts both halves — the miss AND a typed city that hits.
            //
            // So this branch is reached only by a row the write path will not produce: values that
            // carry neither a reference nor a label. Kept for rows written before the write path
            // enforced that, and for a corrupt one. Excluded rather than failed, because a record
            // that says nothing should not be read as a candidate saying no.
            return (true, 0m,
                "This location criterion carries no usable area or name, so it could not be "
                + "evaluated. It is left out of the score.", false);
        }

        var allRequired = criterion.MatchMode == MandatoryMatchMode.AllRequired;
        var positive = allRequired ? matched == considered : matched > 0;
        var passed = negated ? matched == 0 : positive;

        // Partial credit, on the same terms as every other list criterion. A negated criterion is
        // binary by nature: "not in these places" is satisfied or it is not.
        var rawScore = negated
            ? (passed ? 1m : 0m)
            : (considered > 0 ? (decimal)matched / considered : 0m);

        var mode = negated ? "none may match" : allRequired ? "all required" : "any sufficient";
        var notes = $"{matched}/{considered} matched — {basis} [{mode}].";

        return (passed, rawScore, notes, true);
    }

    /// <summary>
    /// Whether <paramref name="path"/> — a <c>GeoArea.Path</c> of the form <c>/a/b/c</c> — passes
    /// through <paramref name="areaId"/> at any tier, including as its own leaf.
    /// </summary>
    /// <remarks>
    /// The trailing slash is appended to both sides so the last segment is delimited like every
    /// other one. Without it, a path ending in the area's id would not match, and a leaf candidate
    /// would fail a criterion naming their own area.
    /// </remarks>
    private static bool PathContainsArea(string path, Guid areaId)
        => (path.EndsWith('/') ? path : path + '/')
            .Contains($"/{areaId}/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// "Education level, at least …": passes when any of the candidate's qualifications sits on the
    /// required rung of the ladder or higher (round 4, lane Q; decisions Q-D3 and Q-D4).
    /// </summary>
    /// <remarks>
    /// <para>It compares RANKS, never names. That is the whole repair: the seeded "A relevant first
    /// degree" criterion matched the word "degree", which no Bachelor of Science contains
    /// (recruitment guide R4-5.2). Ties pass, because a rank shared on the ladder means
    /// equivalence: TDC ranks the HND level with the Bachelor's degree on purpose.</para>
    ///
    /// <para>Binary, not partial (Q-D3): a diploma is not 80% of a degree, so a lower rung scores 0
    /// however close it is — unlike years of experience, where a near miss is a near miss.</para>
    ///
    /// <para>⚠ No levelled qualification at all is a MISS (Q-D4), scored 0 rather than left out.
    /// The Location criterion's rule for a candidate with no area follows the same reasoning:
    /// leaving them out would let a candidate who has shown nothing outrank one who demonstrably
    /// falls short.</para>
    ///
    /// <para>The one case that IS left out is the vacancy's own fault: a criterion that names no rung,
    /// or a rung no longer on the ladder. That measures nothing about the candidate, so it neither
    /// lifts nor lowers anybody — the same repair lane A gave an empty list criterion.</para>
    /// </remarks>
    private static (bool passed, decimal rawScore, string notes, bool autoEvaluated) EvaluateEducationLevelCriterion(
        JobShortlistingCriteria criterion, ScoringCandidateView view, QualificationLadder ladder)
    {
        var required = criterion.Values
            .Where(v => !v.IsDeleted && v.Kind == ShortlistingValueKind.QualificationLevel && v.ReferenceId.HasValue)
            .OrderBy(v => v.SortOrder)
            .FirstOrDefault();

        if (required is null)
            return (true, 0m,
                "This criterion names no level on the qualification ladder, so it measures nothing and is left out of the score. "
                + "Edit it and pick the minimum level.", false);

        if (!ladder.TryGet(required.ReferenceId!.Value, out var need))
            return (true, 0m,
                $"The level this criterion asks for ('{required.Label}') is no longer on the qualification ladder, "
                + "so it could not be evaluated and is left out of the score.", false);

        var held = view.QualificationLevelIds
            .Select(id => ladder.TryGet(id, out var rung) ? rung : (QualificationRung?)null)
            .Where(r => r.HasValue)
            .Select(r => r!.Value)
            .ToList();

        if (held.Count == 0)
            return (false, 0m,
                $"No qualification with a level on file; the criterion asks for at least {need.Name}. "
                + "A qualification counts once its level is set on the candidate's record.", true);

        var best = held.MaxBy(r => r.Rank);
        var passed = best.Rank >= need.Rank;
        var notes = passed
            ? $"Highest qualification level: {best.Name} (rank {best.Rank}); at least {need.Name} (rank {need.Rank}) is required."
            : $"Highest qualification level: {best.Name} (rank {best.Rank}), below the {need.Name} (rank {need.Rank}) required.";
        return (passed, passed ? 1m : 0m, notes, true);
    }

    /// <remarks>
    /// ⚠ Returns <c>autoEvaluated</c> as of round 4, lane A. An EMPTY criterion used to return
    /// <c>(true, 1m, "defaulting to pass")</c> — full marks, for a criterion that measures nothing,
    /// lifting every candidate's percentage and flattening the ranking the score exists to produce.
    /// It is now treated exactly as <c>Other</c> is: excluded from both the earned score and the
    /// total weight, so it neither lifts nor lowers anybody. This is the same repair the vacancy's
    /// "no criteria at all" branch already had, one level down.
    /// </remarks>
    private static (bool passed, decimal rawScore, string notes, bool autoEvaluated) EvaluateListCriterion(
        JobShortlistingCriteria criterion, HashSet<Guid> candidateIds, HashSet<string> candidateNames, string noun)
    {
        var items = new List<(Guid? Id, string Label)>();
        foreach (var v in criterion.Values.Where(v => !v.IsDeleted).OrderBy(v => v.SortOrder))
        {
            var label = v.Label.Trim().ToLowerInvariant();
            if (items.Any(i => (v.ReferenceId.HasValue && i.Id == v.ReferenceId) || (label.Length > 0 && i.Label == label))) continue;
            items.Add((v.ReferenceId, label));
        }
        foreach (var label in SplitValues(criterion.RequiredValue))
        {
            if (items.Any(i => i.Label == label)) continue;
            items.Add((null, label));
        }

        if (items.Count == 0)
            return (true, 0m,
                $"No {noun} specified; this criterion measures nothing and is left out of the score "
                + "entirely.", false);

        int matched = items.Count(i =>
            (i.Id is Guid id && candidateIds.Contains(id))
            || (i.Label.Length > 0 && candidateNames.Any(c => MatchesValue(c, i.Label, criterion.MatchStrategy))));
        bool passed = criterion.MatchMode == MandatoryMatchMode.AllRequired
            ? matched == items.Count
            : matched > 0;
        decimal rawScore = (decimal)matched / items.Count;
        string notes = $"{matched}/{items.Count} required {noun}(s) matched "
                     + $"[{(criterion.MatchMode == MandatoryMatchMode.AllRequired ? "all required" : "any sufficient")}, {criterion.MatchStrategy}; ids first, names second].";
        return (passed, rawScore, notes, true);
    }

    /// <summary>The accepted labels of a criterion, lower-cased: value rows first, legacy text second.</summary>
    private static List<string> RequiredLabels(JobShortlistingCriteria criterion)
    {
        var labels = criterion.Values.Where(v => !v.IsDeleted).OrderBy(v => v.SortOrder)
            .Select(v => v.Label.Trim().ToLowerInvariant()).Where(l => l.Length > 0).ToList();
        foreach (var label in SplitValues(criterion.RequiredValue))
            if (!labels.Contains(label)) labels.Add(label);
        return labels;
    }

    /// <remarks>
    /// ⚠ Returns <c>autoEvaluated</c> as of round 4, lane A, for the same reason the list evaluator
    /// does: a criterion the engine cannot answer must be excluded from the total, not scored zero.
    /// Scoring it zero would make an unanswerable criterion *lower* the candidate's percentage,
    /// which is the mirror image of the bug being fixed.
    /// </remarks>
    private static (bool passed, decimal rawScore, string notes, bool autoEvaluated) EvaluateNumericCriterion(
        JobShortlistingCriteria criterion,
        decimal candidateValue,
        string unit)
    {
        decimal min = criterion.MinValue ?? 0;
        decimal max = criterion.MaxValue ?? decimal.MaxValue;
        var op = criterion.ComparisonOperator ?? ShortlistingComparisonOperator.Between;

        // ⚠ Round 4, lane A. "Less than" with no ceiling compared against decimal.MaxValue and so
        // PASSED EVERY CANDIDATE — a criterion that reads as a restriction and restricted nobody.
        // The write path refuses a numeric criterion with neither bound, but not one carrying only
        // the bound the chosen operator does not use, so the hole was reachable from the form.
        // Treat it the way an empty criterion is treated: measured nothing, so scores nothing.
        var ceilingNeeded = op is ShortlistingComparisonOperator.LessThan
                                or ShortlistingComparisonOperator.LessThanOrEqual;
        if (ceilingNeeded && criterion.MaxValue is null)
            return (true, 0m,
                $"Candidate: {candidateValue:F1} {unit}; the criterion asks for less than a maximum "
                + "it does not state, so it could not be evaluated and is left out of the score.",
                false);

        bool passed = op switch
        {
            ShortlistingComparisonOperator.GreaterThan => candidateValue > min,
            ShortlistingComparisonOperator.GreaterThanOrEqual => candidateValue >= min,
            ShortlistingComparisonOperator.LessThan => candidateValue < max,
            ShortlistingComparisonOperator.LessThanOrEqual => candidateValue <= max,
            ShortlistingComparisonOperator.Equals => candidateValue == min,
            _ => candidateValue >= min && (criterion.MaxValue == null || candidateValue <= max),
        };

        // Partial credit for a near miss, on EITHER side (round 3, lane K; R-5 fix 7). It used to be
        // asymmetric: too little experience scored a fraction, too much scored nothing at all.
        decimal rawScore;
        if (passed)
        {
            rawScore = 1m;
        }
        else if (op is ShortlistingComparisonOperator.Equals)
        {
            rawScore = 0m;
        }
        else if (candidateValue < min)
        {
            rawScore = min > 0 ? Math.Min(candidateValue / min, 0.8m) : 0m;
        }
        else
        {
            // Above the ceiling: the closer to it, the more of the 0.8 cap.
            rawScore = criterion.MaxValue.HasValue && candidateValue > 0 ? Math.Min(max / candidateValue, 0.8m) : 0m;
        }

        string label = op switch
        {
            ShortlistingComparisonOperator.GreaterThan => $"> {min}",
            ShortlistingComparisonOperator.GreaterThanOrEqual => $">= {min}",
            ShortlistingComparisonOperator.LessThan => $"< {max}",
            ShortlistingComparisonOperator.LessThanOrEqual => $"<= {max}",
            ShortlistingComparisonOperator.Equals => $"= {min}",
            _ => criterion.MaxValue.HasValue ? $"{min}–{max}" : $">= {min}",
        };
        string notes = $"Candidate: {candidateValue:F1} {unit}; required {label}.";
        return (passed, rawScore, notes, true);
    }

    private static List<string> SplitValues(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? new List<string>()
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Select(v => v.ToLowerInvariant())
                 .ToList();

    // ── Value match strategy helpers ──────────────────────────────────────────

    /// <summary>
    /// Returns true when <paramref name="candidateValue"/> satisfies the match for
    /// <paramref name="requiredTerm"/> under the given <paramref name="strategy"/>.
    /// Both inputs are expected to be already lower-cased.
    /// </summary>
    private static bool MatchesValue(string candidateValue, string requiredTerm, ValueMatchStrategy strategy)
        => strategy switch
        {
            ValueMatchStrategy.Contains => candidateValue.Contains(requiredTerm, StringComparison.Ordinal)
                                        || requiredTerm.Contains(candidateValue, StringComparison.Ordinal),
            ValueMatchStrategy.Fuzzy    => FuzzyMatches(candidateValue, requiredTerm),
            _                           => candidateValue == requiredTerm,  // Exact (default)
        };

    /// <summary>
    /// Fuzzy match: passes when token overlap score ≥ 0.5, or when Levenshtein
    /// edit-distance ≤ 2 for short strings (≤ 20 chars each).
    /// </summary>
    private static bool FuzzyMatches(string a, string b)
    {
        var tokensA = TokeniseForFuzzy(a);
        var tokensB = TokeniseForFuzzy(b);
        if (tokensA.Count > 0 && tokensB.Count > 0)
        {
            int shared = tokensA.Count(t => tokensB.Contains(t));
            double overlapScore = (double)shared / Math.Max(tokensA.Count, tokensB.Count);
            if (overlapScore >= 0.5) return true;
        }
        // Levenshtein fallback for short strings
        if (a.Length <= 20 && b.Length <= 20 && EditDistance(a, b) <= 2) return true;
        return false;
    }

    private static HashSet<string> TokeniseForFuzzy(string s)
        => s.Split([' ', '-', '_', '/', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Iterative Levenshtein distance, O(min(n,m)) space.</summary>
    private static int EditDistance(string s, string t)
    {
        int n = s.Length, m = t.Length;
        if (n == 0) return m;
        if (m == 0) return n;
        var prev = new int[m + 1];
        var curr = new int[m + 1];
        for (int j = 0; j <= m; j++) prev[j] = j;
        for (int i = 1; i <= n; i++)
        {
            curr[0] = i;
            for (int j = 1; j <= m; j++)
                curr[j] = s[i - 1] == t[j - 1]
                    ? prev[j - 1]
                    : 1 + Math.Min(prev[j - 1], Math.Min(prev[j], curr[j - 1]));
            (prev, curr) = (curr, prev);
        }
        return prev[m];
    }
}

/// <summary>
/// Thin adapter that presents candidate data to <see cref="ShortlistingEvaluator.EvaluateCriterion"/> in a
/// uniform shape regardless of whether the data came from a live <see cref="JobCandidate"/>
/// entity or a deserialized <see cref="ApplicationCandidateSnapshot"/>.
///
/// All string collections are pre-normalised to lower-case so criterion comparisons
/// are O(1) hash-set lookups rather than repeated ToLowerInvariant() allocations.
/// </summary>
public sealed class ScoringCandidateView
{
    // ── Scalars ───────────────────────────────────────────────────────────
    public decimal  YearsOfExperience    { get; init; }
    public DateTime DateOfBirth          { get; init; }
    public Gender   Gender               { get; init; }
    public string   City                 { get; init; } = string.Empty;

    /// <summary>The candidate's administrative area, when they have one on file.</summary>
    public Guid?    GeoAreaId            { get; init; }

    /// <summary>
    /// The materialised ancestor path of <see cref="GeoAreaId"/>, in the form
    /// <c>/root/child/leaf</c> and INCLUDING the area's own id as the last segment.
    /// </summary>
    /// <remarks>
    /// ⚠ Settable after construction, unlike everything else here, because the path is the one
    /// field that may need a database read: a snapshot written before round 4 carries the id
    /// but not the path, and the orchestrator back-fills it from the live tree before scoring
    /// rather than making <c>EvaluateCriterion</c> async. Null means "no area, or the area
    /// could not be read" — both fall through to the free-text city.
    /// </remarks>
    public string?  GeoAreaPath          { get; set; }

    // ── Pre-normalised sets for O(1) lookups ──────────────────────────────
    public HashSet<string> SkillNames          { get; init; } = new();
    public HashSet<Guid>   SkillIds            { get; init; } = new();
    public HashSet<string> CertificationNames  { get; init; } = new();
    public HashSet<string> QualificationNames  { get; init; } = new();
    public HashSet<Guid>   QualificationIds    { get; init; } = new();

    /// <summary>
    /// The ladder rungs the candidate's qualifications sit on: each one's own level, or else its
    /// catalogue entry's (round 4, lane Q). Ranks are read from the ladder at scoring time.
    /// </summary>
    /// <remarks>
    /// A set the orchestrator may ADD to after construction: a snapshot written before lane Q
    /// carries no levels, and they are back-filled from the candidate's live profile, as
    /// <see cref="GeoAreaPath"/> is from the live tree.
    /// </remarks>
    public HashSet<Guid>   QualificationLevelIds { get; init; } = new();

    public HashSet<string> LanguageNames       { get; init; } = new();
    public HashSet<Guid>   LanguageIds         { get; init; } = new();

    // ── Factory: from live entity ─────────────────────────────────────────
    public static ScoringCandidateView FromEntity(JobCandidate c, JobApplication app) =>
        new()
        {
            YearsOfExperience   = app.YearsOfExperience ?? c.TotalYearsExperience ?? 0,
            DateOfBirth         = c.DateOfBirth,
            Gender              = c.Gender,
            City                = c.City?.ToLowerInvariant() ?? string.Empty,
            GeoAreaId           = c.GeoAreaId,
            // Only set when the caller Included the navigation; the orchestrator back-fills it
            // from the live tree otherwise.
            GeoAreaPath         = c.GeoArea?.Path,
            SkillNames          = c.Skills
                                    .Select(s => s.SkillName.ToLowerInvariant())
                                    .ToHashSet(),
            SkillIds            = c.Skills
                                    .Where(s => s.SkillId.HasValue)
                                    .Select(s => s.SkillId!.Value)
                                    .ToHashSet(),
            CertificationNames  = c.Skills
                                    .Where(s => s.IsCertified && !string.IsNullOrEmpty(s.CertificationName))
                                    .Select(s => s.CertificationName!.ToLowerInvariant())
                                    .ToHashSet(),
            QualificationNames  = c.Qualifications
                                    .Select(q => (q.Qualification?.Name ?? q.QualificationFreeText ?? string.Empty)
                                                 .ToLowerInvariant())
                                    .Where(n => n.Length > 0)
                                    .ToHashSet(),
            QualificationIds    = c.Qualifications
                                    .Where(q => q.QualificationId.HasValue)
                                    .Select(q => q.QualificationId!.Value)
                                    .ToHashSet(),
            QualificationLevelIds = LevelsOf(c.Qualifications),
            LanguageNames       = c.Languages
                                    .Select(l => l.LanguageName.ToLowerInvariant())
                                    .ToHashSet(),
            LanguageIds         = c.Languages
                                    .Where(l => l.LanguageId.HasValue)
                                    .Select(l => l.LanguageId!.Value)
                                    .ToHashSet(),
        };

    /// <summary>
    /// The effective rung of each live qualification: its own level, or else its catalogue entry's.
    /// </summary>
    /// <remarks>
    /// ⚠ The catalogue fallback needs <c>Qualification</c> loaded, which every scoring read already
    /// includes for the name. Deleted rows are filtered here rather than trusted to the read.
    /// </remarks>
    public static HashSet<Guid> LevelsOf(IEnumerable<JobCandidateQualification> qualifications) =>
        qualifications
            .Where(q => !q.IsDeleted)
            .Select(q => q.QualificationLevelId ?? q.Qualification?.QualificationLevelId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();

    // ── Factory: from snapshot ────────────────────────────────────────────
    public static ScoringCandidateView FromSnapshot(ApplicationCandidateSnapshot snap) =>
        new()
        {
            YearsOfExperience   = snap.YearsOfExperience ?? snap.TotalYearsExperience ?? 0,
            DateOfBirth         = snap.DateOfBirth,
            Gender              = snap.Gender,
            City                = snap.City?.ToLowerInvariant() ?? string.Empty,
            GeoAreaId           = snap.GeoAreaId,
            // Null on every snapshot written before round 4, and on the anonymous apply path
            // when the candidate row had no area loaded. Back-filled by the orchestrator.
            GeoAreaPath         = snap.GeoAreaPath,
            SkillNames          = snap.Skills
                                    .Select(s => s.SkillName.ToLowerInvariant())
                                    .ToHashSet(),
            SkillIds            = snap.Skills
                                    .Where(s => s.SkillId.HasValue)
                                    .Select(s => s.SkillId!.Value)
                                    .ToHashSet(),
            CertificationNames  = snap.Skills
                                    .Where(s => s.IsCertified && !string.IsNullOrEmpty(s.CertificationName))
                                    .Select(s => s.CertificationName!.ToLowerInvariant())
                                    .ToHashSet(),
            QualificationNames  = snap.Qualifications
                                    .Select(q => q.NormalisedName)
                                    .Where(n => n.Length > 0)
                                    .ToHashSet(),
            QualificationIds    = snap.Qualifications
                                    .Where(q => q.QualificationId.HasValue)
                                    .Select(q => q.QualificationId!.Value)
                                    .ToHashSet(),
            // Empty on a snapshot written before lane Q (QualificationLevelsRecorded is false); the
            // orchestrator back-fills it from the live profile before scoring.
            QualificationLevelIds = snap.Qualifications
                                    .Where(q => q.QualificationLevelId.HasValue)
                                    .Select(q => q.QualificationLevelId!.Value)
                                    .ToHashSet(),
            LanguageNames       = snap.Languages
                                    .Select(l => l.NormalisedName)
                                    .ToHashSet(),
            LanguageIds         = snap.Languages
                                    .Where(l => l.LanguageId.HasValue)
                                    .Select(l => l.LanguageId!.Value)
                                    .ToHashSet(),
        };

    // ── Factory: from a pool member, who has no application ───────────────
    /// <summary>
    /// Round 4, lane B. A talent-pool member is a <see cref="JobCandidate"/> and nothing else:
    /// there is no application, so no application-level override of the years of experience and no
    /// frozen snapshot to prefer. Everything else is read exactly as <see cref="FromEntity"/> reads
    /// it, which is the point — the pool is screened by the same engine, not by a second rubric
    /// that happens to agree today.
    /// </summary>
    /// <remarks>
    /// ⚠ The caller owns the <c>Include</c>s. <c>Skills</c>, <c>Qualifications</c> and
    /// <c>Languages</c> are read straight off the entity, so a read that did not load them produces
    /// a view with empty sets — a candidate who silently matches nothing rather than an error. They
    /// must also be filtered on <c>IsDeleted</c> at the read, as every other candidate read is:
    /// a retired skill scoring a criterion is worse than a missing one.
    /// </remarks>
    public static ScoringCandidateView FromCandidate(JobCandidate c) =>
        new()
        {
            YearsOfExperience   = c.TotalYearsExperience ?? 0,
            DateOfBirth         = c.DateOfBirth,
            Gender              = c.Gender,
            City                = c.City?.ToLowerInvariant() ?? string.Empty,
            GeoAreaId           = c.GeoAreaId,
            GeoAreaPath         = c.GeoArea?.Path,
            SkillNames          = c.Skills
                                    .Select(s => s.SkillName.ToLowerInvariant())
                                    .ToHashSet(),
            SkillIds            = c.Skills
                                    .Where(s => s.SkillId.HasValue)
                                    .Select(s => s.SkillId!.Value)
                                    .ToHashSet(),
            CertificationNames  = c.Skills
                                    .Where(s => s.IsCertified && !string.IsNullOrEmpty(s.CertificationName))
                                    .Select(s => s.CertificationName!.ToLowerInvariant())
                                    .ToHashSet(),
            QualificationNames  = c.Qualifications
                                    .Select(q => (q.Qualification?.Name ?? q.QualificationFreeText ?? string.Empty)
                                                 .ToLowerInvariant())
                                    .Where(n => n.Length > 0)
                                    .ToHashSet(),
            QualificationIds    = c.Qualifications
                                    .Where(q => q.QualificationId.HasValue)
                                    .Select(q => q.QualificationId!.Value)
                                    .ToHashSet(),
            QualificationLevelIds = LevelsOf(c.Qualifications),
            LanguageNames       = c.Languages
                                    .Select(l => l.LanguageName.ToLowerInvariant())
                                    .ToHashSet(),
            LanguageIds         = c.Languages
                                    .Where(l => l.LanguageId.HasValue)
                                    .Select(l => l.LanguageId!.Value)
                                    .ToHashSet(),
        };
}


/// <summary>
/// What <see cref="ShortlistingEvaluator.Score"/> answers: the normalised 0–100 score, whether the
/// mandatory criteria were all met, the weight actually measured, and the per-criterion breakdown.
/// </summary>
/// <param name="Score">
/// Null means nothing could be measured — the vacancy stated no criteria, or every criterion it
/// stated turned out to be unevaluable against this candidate. A null score is never
/// auto-shortlisted, which is the point of distinguishing it from 0.
/// </param>
public readonly record struct ShortlistingScoreResult(
    decimal? Score,
    bool AllMandatoryPassed,
    decimal TotalWeight,
    List<CriterionScoreResult> Breakdown,
    bool HasCriteria = true);
