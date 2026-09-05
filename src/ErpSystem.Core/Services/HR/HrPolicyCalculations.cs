using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.HR;

/// <summary>
/// Pure, DB-free calculations driven by <see cref="CompanyHrPolicySettings"/>.
/// Load the settings once (via <c>ICompanyHrPolicyProvider</c>), then call these
/// per employee — no side effects, fully unit-testable.
/// </summary>
public static class HrPolicyCalculations
{
    /// <summary>
    /// The retirement age that applies to an employee of the given gender, honouring
    /// gender-specific overrides when enabled. Falls back to the compulsory age.
    /// </summary>
    public static int EffectiveRetirementAge(CompanyHrPolicySettings settings, Gender? gender)
    {
        if (settings.UseGenderSpecificRetirementAge)
        {
            return gender switch
            {
                Gender.Male   => settings.MaleRetirementAge   ?? settings.CompulsoryRetirementAge,
                Gender.Female => settings.FemaleRetirementAge ?? settings.CompulsoryRetirementAge,
                _             => settings.CompulsoryRetirementAge
            };
        }

        return settings.CompulsoryRetirementAge;
    }

    /// <summary>
    /// Effective retirement date for an employee: the explicit
    /// <see cref="Employee.RetirementDate"/> when set, otherwise
    /// date-of-birth + effective retirement age. Null when neither is known.
    /// </summary>
    public static DateOnly? RetirementDate(CompanyHrPolicySettings settings, Employee employee)
    {
        if (employee.RetirementDate.HasValue)
            return employee.RetirementDate.Value;

        if (employee.DateOfBirth.HasValue)
            return employee.DateOfBirth.Value.AddYears(EffectiveRetirementAge(settings, employee.Gender));

        return null;
    }

    /// <summary>
    /// Whole years of active service remaining before retirement (floored, never negative).
    /// Null when the retirement date cannot be determined.
    /// </summary>
    public static int? ServiceYearsLeft(CompanyHrPolicySettings settings, Employee employee)
    {
        var retire = RetirementDate(settings, employee);
        if (retire is null)
            return null;

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (retire.Value <= today)
            return 0;

        var months = (retire.Value.Year - today.Year) * 12 + retire.Value.Month - today.Month;
        if (retire.Value.Day < today.Day)
            months--;

        return Math.Max(0, months / 12);
    }

    /// <summary>Current age in whole years from a date of birth. Null when unknown.</summary>
    public static int? Age(DateOnly? dateOfBirth) => CompletedYears(dateOfBirth);

    /// <summary>
    /// Whole years elapsed since <paramref name="from"/>, counting only anniversaries that have
    /// actually come round.
    /// </summary>
    /// <remarks>
    /// <para><b>The single home for this arithmetic.</b> Area 14 slice 3b found three
    /// implementations of it in the HR module and only one of them correct. <see cref="Age"/> did
    /// the anniversary check properly; <c>Employee.YearsOfService</c> subtracted calendar years
    /// (<c>Today.Year - DateEmployed.Year</c>), which reports a completed year on 1 January for
    /// someone whose anniversary is in December; and area 14's eligibility evaluator had grown a
    /// private fourth copy while fixing the second. All of them now call this.</para>
    ///
    /// <para><b>Why it matters beyond tidiness.</b> The overstatement is at most one year, but it
    /// falls on exactly the rules that turn on a threshold: length-of-service eligibility for an
    /// award, a long-service milestone, a minimum-age gate. A rule reading "ten years" admitted
    /// people with nine years and one month.</para>
    ///
    /// <param name="asOf">
    /// The date to measure to. Defaults to today. Supplied explicitly where a rule must be
    /// evaluated as it stood at some other moment — award eligibility does this so that an
    /// effective-dated exclusion can be re-checked against the day it was in force.
    /// </param>
    /// </remarks>
    public static int? CompletedYears(DateOnly? from, DateOnly? asOf = null)
    {
        if (from is null)
            return null;

        var to = asOf ?? DateOnly.FromDateTime(DateTime.Today);
        var years = to.Year - from.Value.Year;

        // Undo the year if the anniversary has not been reached yet.
        if (from.Value > to.AddYears(-years))
            years--;

        return Math.Max(0, years);
    }
}
