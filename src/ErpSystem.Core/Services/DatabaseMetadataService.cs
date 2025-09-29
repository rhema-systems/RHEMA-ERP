using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services;

public interface IDatabaseMetadataService
{
    Task<List<DatabaseTable>> GetTablesAsync();
    Task<List<DatabaseColumn>> GetTableColumnsAsync(string tableName);
    Task<List<string>> GetAvailableModulesAsync();
}

public class DatabaseTable
{
    public string Name { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class DatabaseColumn
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public string? Description { get; set; }
    public string? DefaultValue { get; set; }
}

public class DatabaseMetadataService : IDatabaseMetadataService
{
    private readonly IConfiguration _configuration;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DatabaseMetadataService> _logger;

    public DatabaseMetadataService(
        IConfiguration configuration,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        ILogger<DatabaseMetadataService> logger)
    {
        _configuration = configuration;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<List<DatabaseTable>> GetTablesAsync()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        var tables = new List<DatabaseTable>();

        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    t.TABLE_SCHEMA,
                    t.TABLE_NAME,
                    CASE WHEN ep.value IS NOT NULL THEN CAST(ep.value AS NVARCHAR(MAX)) ELSE NULL END AS TABLE_COMMENT
                FROM INFORMATION_SCHEMA.TABLES t
                LEFT JOIN sys.tables st ON st.name = t.TABLE_NAME
                LEFT JOIN sys.extended_properties ep ON ep.major_id = st.object_id AND ep.minor_id = 0 AND ep.name = 'MS_Description'
                WHERE t.TABLE_TYPE = 'BASE TABLE' 
                    AND t.TABLE_SCHEMA NOT IN ('sys', 'INFORMATION_SCHEMA')
                    AND t.TABLE_NAME NOT LIKE '__EF%'
                ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME";

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                tables.Add(new DatabaseTable
                {
                    Schema = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2)
                });
            }

            _logger.LogInformation("Retrieved {Count} database tables", tables.Count);
            return tables;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving database tables");
            throw;
        }
    }

    public async Task<List<DatabaseColumn>> GetTableColumnsAsync(string tableName)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        var columns = new List<DatabaseColumn>();

        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT 
                    c.COLUMN_NAME,
                    c.DATA_TYPE,
                    c.IS_NULLABLE,
                    c.COLUMN_DEFAULT,
                    CASE WHEN tc.CONSTRAINT_TYPE = 'PRIMARY KEY' THEN 1 ELSE 0 END AS IS_PRIMARY_KEY,
                    CASE WHEN ep.value IS NOT NULL THEN CAST(ep.value AS NVARCHAR(MAX)) ELSE NULL END AS COLUMN_COMMENT
                FROM INFORMATION_SCHEMA.COLUMNS c
                LEFT JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu 
                    ON c.TABLE_NAME = kcu.TABLE_NAME AND c.COLUMN_NAME = kcu.COLUMN_NAME
                LEFT JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc 
                    ON kcu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                LEFT JOIN sys.columns sc ON sc.object_id = OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME) 
                    AND sc.name = c.COLUMN_NAME
                LEFT JOIN sys.extended_properties ep ON ep.major_id = sc.object_id 
                    AND ep.minor_id = sc.column_id AND ep.name = 'MS_Description'
                WHERE c.TABLE_NAME = @tableName
                ORDER BY c.ORDINAL_POSITION";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@tableName", tableName);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                columns.Add(new DatabaseColumn
                {
                    Name = reader.GetString(0),
                    DataType = reader.GetString(1),
                    IsNullable = reader.GetString(2) == "YES",
                    IsPrimaryKey = reader.GetInt32(4) == 1,
                    DefaultValue = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Description = reader.IsDBNull(5) ? null : reader.GetString(5)
                });
            }

            _logger.LogInformation("Retrieved {Count} columns for table {TableName}", columns.Count, tableName);
            return columns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving columns for table {TableName}", tableName);
            throw;
        }
    }

    public async Task<List<string>> GetAvailableModulesAsync()
    {
        try
        {
            var tenantId = _currentUserService.GetTenantId();
            if (!tenantId.HasValue)
            {
                throw new InvalidOperationException("No tenant context available");
            }

            var tenantModules = await _unitOfWork.Repository<Core.Entities.TenantModule>()
                .FindAsync(tm => tm.TenantId == tenantId.Value && tm.Status == Shared.ModuleStatus.Enabled);

            var moduleNames = tenantModules.Select(tm => tm.ModuleName).OrderBy(x => x).ToList();
            
            _logger.LogInformation("Retrieved {Count} enabled modules for tenant {TenantId}", moduleNames.Count, tenantId);
            return moduleNames;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available modules");
            throw;
        }
    }
}