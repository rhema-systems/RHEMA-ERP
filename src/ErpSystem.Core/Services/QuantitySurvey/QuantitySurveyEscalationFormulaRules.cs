using ErpSystem.Core.DTOs.QuantitySurvey;
using ErpSystem.Core.Entities.QuantitySurvey;

namespace ErpSystem.Core.Services.QuantitySurvey;

public static class QuantitySurveyEscalationFormulaRules
{
    public static string? ValidatePolicyComponents(
        IEnumerable<(QuantitySurveyEscalationComponentType Component, decimal Coefficient)> values,
        QsEscalationValue policy)
    {
        var components = values.ToList();
        var required = Enum.GetValues<QuantitySurveyEscalationComponentType>();
        if (components.Count != required.Length ||
            components.Select(value => value.Component).Distinct().Count() != required.Length ||
            required.Except(components.Select(value => value.Component)).Any())
        {
            return "Provide each Material, Labour, Plant, and Other component exactly once.";
        }

        var expected = new Dictionary<QuantitySurveyEscalationComponentType, decimal>
        {
            [QuantitySurveyEscalationComponentType.Material] = policy.MaterialCoefficient,
            [QuantitySurveyEscalationComponentType.Labour] = policy.LabourCoefficient,
            [QuantitySurveyEscalationComponentType.Plant] = policy.PlantCoefficient,
            [QuantitySurveyEscalationComponentType.Other] = policy.OtherCoefficient
        };

        foreach (var component in components)
        {
            if (component.Coefficient != expected[component.Component])
            {
                return $"{component.Component} coefficient must equal the approved QS-DEC-006 value of {expected[component.Component]:0.####}%.";
            }
        }

        return components.Sum(value => value.Coefficient) == 100m
            ? null
            : "Escalation coefficients must total exactly 100 percent.";
    }
}
