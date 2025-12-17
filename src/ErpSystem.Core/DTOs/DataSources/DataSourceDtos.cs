using ErpSystem.Core.Entities;

namespace ErpSystem.Core.DTOs.DataSources
{
    public class DataSourceDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DataSourceType Type { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public string? Host { get; set; }
        public int? Port { get; set; }
        public string? DatabaseName { get; set; }
        public string? Username { get; set; }
        public Dictionary<string, object>? AdditionalSettings { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastConnectionTest { get; set; }
        public bool? LastConnectionSuccess { get; set; }
        public string? LastConnectionError { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUsed { get; set; }
        public int UsageCount { get; set; }
        public ConnectionStatus Status { get; set; }
    }

    public class CreateDataSourceDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DataSourceType Type { get; set; }
        public string? Host { get; set; }
        public int? Port { get; set; }
        public string? DatabaseName { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public Dictionary<string, object>? AdditionalSettings { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateDataSourceDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Host { get; set; }
        public int? Port { get; set; }
        public string? DatabaseName { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public Dictionary<string, object>? AdditionalSettings { get; set; }
        public bool? IsActive { get; set; }
    }

    public class TestConnectionDto
    {
        public DataSourceType Type { get; set; }
        public string? Host { get; set; }
        public int? Port { get; set; }
        public string? DatabaseName { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public Dictionary<string, object>? AdditionalSettings { get; set; }
    }

    public class ConnectionTestResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan ResponseTime { get; set; }
        public Dictionary<string, object>? ConnectionInfo { get; set; }
        public DateTime TestedAt { get; set; }
    }

    public class DataSourceSchemaDto
    {
        public List<TableInfo> Tables { get; set; } = new();
        public List<ViewInfo> Views { get; set; } = new();
        public List<StoredProcedureInfo> StoredProcedures { get; set; } = new();
    }

    public class TableInfo
    {
        public string Name { get; set; } = string.Empty;
        public string? Schema { get; set; }
        public List<ColumnInfo> Columns { get; set; } = new();
        public long RowCount { get; set; }
    }

    public class ViewInfo
    {
        public string Name { get; set; } = string.Empty;
        public string? Schema { get; set; }
        public List<ColumnInfo> Columns { get; set; } = new();
    }

    public class StoredProcedureInfo
    {
        public string Name { get; set; } = string.Empty;
        public string? Schema { get; set; }
        public List<ParameterInfo> Parameters { get; set; } = new();
    }

    public class ColumnInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        public int? MaxLength { get; set; }
        public int? Precision { get; set; }
        public int? Scale { get; set; }
    }

    public class ParameterInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsOutput { get; set; }
        public object? DefaultValue { get; set; }
    }

    public class QueryDataSourceDto
    {
        public string Query { get; set; } = string.Empty;
        public Dictionary<string, object>? Parameters { get; set; }
        public int MaxRows { get; set; } = 1000;
    }

    public class QueryResultDto
    {
        public List<Dictionary<string, object>> Data { get; set; } = new();
        public List<ColumnInfo> Columns { get; set; } = new();
        public int TotalRows { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public string? QueryUsed { get; set; }
    }

    public enum ConnectionStatus
    {
        Unknown,
        Connected,
        Disconnected,
        Error,
        Testing
    }
}
