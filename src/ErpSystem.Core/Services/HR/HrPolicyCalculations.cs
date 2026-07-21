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
    public static int? Age(DateOnly? dateOfBirth)
    {
        if (dateOfBirth is null)
            return null;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - dateOfBirth.Value.Year;
        if (dateOfBirth.Value > today.AddYears(-age))
            age--;

        return age;
    }
}
