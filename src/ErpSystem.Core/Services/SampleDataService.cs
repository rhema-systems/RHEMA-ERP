using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ErpSystem.Core.Services;

public interface ISampleDataService
{
    Task<Dictionary<string, object>> GenerateSampleDataAsync(string[] fieldNames);
    Dictionary<string, object> GenerateSampleDataForTable(string tableName, string[] fieldNames);
    string GetSampleValueForField(string tableName, string fieldName, string dataType);
}

public class SampleDataService : ISampleDataService
{
    private readonly ILogger<SampleDataService> _logger;
    private readonly Random _random;

    // Sample data pools
    private readonly string[] _firstNames = { "John", "Jane", "Michael", "Sarah", "David", "Emily", "Robert", "Lisa", "William", "Jennifer" };
    private readonly string[] _lastNames = { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez" };
    private readonly string[] _companies = { "Acme Corp", "Global Tech", "Sunrise Industries", "Blue Sky Solutions", "Prime Ventures", "Alpha Systems" };
    private readonly string[] _products = { "Premium Software License", "Consulting Services", "Hardware Package", "Support Contract", "Training Program" };
    private readonly string[] _cities = { "New York", "Los Angeles", "Chicago", "Houston", "Phoenix", "Philadelphia", "San Antonio", "San Diego" };
    private readonly string[] _streets = { "Main Street", "Oak Avenue", "Maple Drive", "Cedar Lane", "Pine Road", "Elm Street", "Park Boulevard" };

    public SampleDataService(ILogger<SampleDataService> logger)
    {
        _logger = logger;
        _random = new Random();
    }

    public async Task<Dictionary<string, object>> GenerateSampleDataAsync(string[] fieldNames)
    {
        try
        {
            var sampleData = new Dictionary<string, object>();

            foreach (var fieldName in fieldNames)
            {
                // Parse field name (format: Table.Field or just Field)
                var parts = fieldName.Split('.');
                var tableName = parts.Length > 1 ? parts[0] : "Unknown";
                var columnName = parts.Length > 1 ? parts[1] : parts[0];

                // Generate sample value based on field name patterns
                var sampleValue = GenerateSampleValueByFieldName(tableName, columnName);
                sampleData[fieldName] = sampleValue;
            }

            return await Task.FromResult(sampleData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating sample data for fields: {FieldNames}", string.Join(", ", fieldNames));
            throw;
        }
    }

    public Dictionary<string, object> GenerateSampleDataForTable(string tableName, string[] fieldNames)
    {
        var sampleData = new Dictionary<string, object>();

        foreach (var fieldName in fieldNames)
        {
            var sampleValue = GetSampleValueForField(tableName, fieldName, "varchar");
            sampleData[$"{tableName}.{fieldName}"] = sampleValue;
        }

        return sampleData;
    }

    public string GetSampleValueForField(string tableName, string fieldName, string dataType)
    {
        // Generate based on field name patterns
        return GenerateSampleValueByFieldName(tableName, fieldName);
    }

    private string GenerateSampleValueByFieldName(string tableName, string fieldName)
    {
        var lowerFieldName = fieldName.ToLower();
        var lowerTableName = tableName.ToLower();

        // Name fields
        if (lowerFieldName.Contains("firstname") || lowerFieldName.Contains("first_name"))
            return _firstNames[_random.Next(_firstNames.Length)];

        if (lowerFieldName.Contains("lastname") || lowerFieldName.Contains("last_name") || lowerFieldName.Contains("surname"))
            return _lastNames[_random.Next(_lastNames.Length)];

        if (lowerFieldName.Contains("fullname") || lowerFieldName.Contains("full_name") || lowerFieldName.Equals("name"))
            return $"{_firstNames[_random.Next(_firstNames.Length)]} {_lastNames[_random.Next(_lastNames.Length)]}";

        // Email fields
        if (lowerFieldName.Contains("email"))
        {
            var firstName = _firstNames[_random.Next(_firstNames.Length)].ToLower();
            var lastName = _lastNames[_random.Next(_lastNames.Length)].ToLower();
            return $"{firstName}.{lastName}@example.com";
        }

        // Phone fields
        if (lowerFieldName.Contains("phone") || lowerFieldName.Contains("mobile") || lowerFieldName.Contains("tel"))
            return $"+1 ({_random.Next(200, 999)}) {_random.Next(100, 999)}-{_random.Next(1000, 9999)}";

        // Address fields
        if (lowerFieldName.Contains("address") || lowerFieldName.Contains("street"))
            return $"{_random.Next(100, 9999)} {_streets[_random.Next(_streets.Length)]}";

        if (lowerFieldName.Contains("city"))
            return _cities[_random.Next(_cities.Length)];

        if (lowerFieldName.Contains("state") || lowerFieldName.Contains("province"))
            return "California";

        if (lowerFieldName.Contains("zip") || lowerFieldName.Contains("postal"))
            return _random.Next(10000, 99999).ToString();

        if (lowerFieldName.Contains("country"))
            return "United States";

        // Company fields
        if (lowerFieldName.Contains("company") || lowerFieldName.Contains("organization"))
            return _companies[_random.Next(_companies.Length)];

        // Product fields
        if (lowerFieldName.Contains("product") || lowerFieldName.Contains("item"))
            return _products[_random.Next(_products.Length)];

        // Amount/Price fields
        if (lowerFieldName.Contains("amount") || lowerFieldName.Contains("price") || lowerFieldName.Contains("total") || lowerFieldName.Contains("cost"))
            return $"${_random.Next(10, 5000):N2}";

        // Quantity fields
        if (lowerFieldName.Contains("quantity") || lowerFieldName.Contains("qty") || lowerFieldName.Contains("count"))
            return _random.Next(1, 100).ToString();

        // Date fields
        if (lowerFieldName.Contains("date") || lowerFieldName.Contains("created") || lowerFieldName.Contains("updated"))
        {
            var randomDate = DateTime.Now.AddDays(-_random.Next(0, 365));
            return randomDate.ToString("yyyy-MM-dd");
        }

        // Status fields
        if (lowerFieldName.Contains("status"))
        {
            var statuses = new[] { "Active", "Pending", "Completed", "In Progress", "Cancelled" };
            return statuses[_random.Next(statuses.Length)];
        }

        // ID fields
        if (lowerFieldName.Contains("id") || lowerFieldName.Equals("id"))
            return _random.Next(1000, 9999).ToString();

        // Boolean fields
        if (lowerFieldName.Contains("active") || lowerFieldName.Contains("enabled") || lowerFieldName.Contains("verified"))
            return _random.Next(2) == 0 ? "false" : "true";

        // Username fields
        if (lowerFieldName.Contains("username") || lowerFieldName.Contains("user_name"))
        {
            var firstName = _firstNames[_random.Next(_firstNames.Length)].ToLower();
            return $"{firstName}{_random.Next(10, 99)}";
        }

        // Description fields
        if (lowerFieldName.Contains("description") || lowerFieldName.Contains("notes") || lowerFieldName.Contains("comment"))
            return "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore.";

        // Title fields
        if (lowerFieldName.Contains("title") || lowerFieldName.Contains("subject"))
            return "Sample Title or Subject";

        // Category fields
        if (lowerFieldName.Contains("category") || lowerFieldName.Contains("type"))
        {
            var categories = new[] { "General", "Important", "Urgent", "Information", "Update" };
            return categories[_random.Next(categories.Length)];
        }

        // Table-specific logic
        switch (lowerTableName)
        {
            case "users":
            case "user":
                if (lowerFieldName.Contains("role"))
                    return "Manager";
                break;

            case "orders":
            case "order":
                if (lowerFieldName.Contains("number"))
                    return $"ORD-{_random.Next(10000, 99999)}";
                break;

            case "invoices":
            case "invoice":
                if (lowerFieldName.Contains("number"))
                    return $"INV-{_random.Next(10000, 99999)}";
                break;

            case "products":
            case "product":
                if (lowerFieldName.Contains("sku"))
                    return $"SKU-{_random.Next(1000, 9999)}";
                break;
        }

        // Default fallback
        return $"Sample {fieldName}";
    }
}