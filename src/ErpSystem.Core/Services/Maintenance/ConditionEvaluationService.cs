using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Maintenance;

/// <summary>
/// Service for evaluating condition-based maintenance triggers
/// </summary>
public class ConditionEvaluationService : IConditionEvaluationService
{
    private readonly ILogger<ConditionEvaluationService> _logger;

    public ConditionEvaluationService(ILogger<ConditionEvaluationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Evaluates condition criteria JSON against current values
    /// </summary>
    public async Task<bool> EvaluateConditionsAsync(string? conditionCriteriaJson, Dictionary<string, object>? currentValues = null)
    {
        try
        {
            if (string.IsNullOrEmpty(conditionCriteriaJson))
            {
                _logger.LogWarning("Condition criteria JSON is null or empty");
                return false;
            }

            var conditions = JsonSerializer.Deserialize<List<ConditionCriteria>>(conditionCriteriaJson);
            if (conditions == null || !conditions.Any())
            {
                _logger.LogWarning("No conditions found in criteria JSON");
                return false;
            }

            // If no current values provided, we cannot evaluate (would need to fetch from data sources)
            if (currentValues == null || !currentValues.Any())
            {
                _logger.LogWarning("No current values provided for condition evaluation");
                return false;
            }

            // Evaluate each condition
            var results = new List<bool>();
            foreach (var condition in conditions)
            {
                if (!currentValues.ContainsKey(condition.Parameter))
                {
                    _logger.LogWarning("Parameter {Parameter} not found in current values", condition.Parameter);
                    results.Add(false);
                    continue;
                }

                var currentValue = currentValues[condition.Parameter];
                var conditionMet = EvaluateCondition(currentValue, condition.Operator, condition.Value);
                results.Add(conditionMet);

                _logger.LogDebug("Condition evaluation: {Parameter} {Operator} {Value} = {Result}", 
                    condition.Parameter, condition.Operator, condition.Value, conditionMet);
            }

            // All conditions must be met (AND logic within the criteria)
            var finalResult = results.All(r => r);
            
            _logger.LogInformation("Condition evaluation complete. {Met}/{Total} conditions met. Result: {Result}", 
                results.Count(r => r), results.Count, finalResult);

            return finalResult;
        }
        catch (JsonException jsonEx)
        {
            _logger.LogError(jsonEx, "Error deserializing condition criteria JSON");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating conditions");
            return false;
        }
    }

    /// <summary>
    /// Evaluates a single condition
    /// </summary>
    private bool EvaluateCondition(object currentValue, string operatorStr, object expectedValue)
    {
        try
        {
            // Convert values to comparable format
            var currentDecimal = Convert.ToDecimal(currentValue);
            var expectedDecimal = Convert.ToDecimal(expectedValue);

            return operatorStr switch
            {
                ">" => currentDecimal > expectedDecimal,
                ">=" => currentDecimal >= expectedDecimal,
                "<" => currentDecimal < expectedDecimal,
                "<=" => currentDecimal <= expectedDecimal,
                "==" or "=" => currentDecimal == expectedDecimal,
                "!=" => currentDecimal != expectedDecimal,
                _ => throw new ArgumentException($"Unsupported operator: {operatorStr}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating condition: {CurrentValue} {Operator} {ExpectedValue}", 
                currentValue, operatorStr, expectedValue);
            return false;
        }
    }

    /// <summary>
    /// Fetches current values from data sources defined in the schedule
    /// </summary>
    public async Task<Dictionary<string, object>?> FetchConditionDataAsync(string? conditionDataSourcesJson)
    {
        try
        {
            if (string.IsNullOrEmpty(conditionDataSourcesJson))
            {
                return null;
            }

            var dataSources = JsonSerializer.Deserialize<List<ConditionDataSource>>(conditionDataSourcesJson);
            if (dataSources == null || !dataSources.Any())
            {
                return null;
            }

            var data = new Dictionary<string, object>();

            // This is a placeholder implementation
            // In a real implementation, you would:
            // 1. Connect to sensors/APIs based on source type
            // 2. Fetch actual data
            // 3. Map to parameter names
            
            _logger.LogInformation("Fetching data from {Count} data sources", dataSources.Count);

            foreach (var source in dataSources)
            {
                // Placeholder: Return mock data
                // TODO: Implement actual data source connections
                _logger.LogWarning("Data source fetching not yet implemented for source: {Source}", source.Source);
            }

            return data.Any() ? data : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching condition data from sources");
            return null;
        }
    }
}

/// <summary>
/// Interface for condition evaluation service
/// </summary>
public interface IConditionEvaluationService
{
    Task<bool> EvaluateConditionsAsync(string? conditionCriteriaJson, Dictionary<string, object>? currentValues = null);
    Task<Dictionary<string, object>?> FetchConditionDataAsync(string? conditionDataSourcesJson);
}

/// <summary>
/// Represents a single condition criteria
/// </summary>
public class ConditionCriteria
{
    public string Parameter { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public object Value { get; set; } = 0;
}

/// <summary>
/// Represents a data source for condition monitoring
/// </summary>
public class ConditionDataSource
{
    public string Source { get; set; } = string.Empty; // "sensor", "api", "database"
    public string Id { get; set; } = string.Empty;
    public string? Endpoint { get; set; }
    public Dictionary<string, string>? Configuration { get; set; }
}
