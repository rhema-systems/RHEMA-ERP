using ErpSystem.Core.DTOs.DataSources;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services
{
    public class EnterpriseDataSourceService : IDataSourceService
    {
        private readonly IDataSourceRepository _dataSourceRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<EnterpriseDataSourceService> _logger;

        public EnterpriseDataSourceService(
            IDataSourceRepository dataSourceRepository,
            IUnitOfWork unitOfWork,
            ILogger<EnterpriseDataSourceService> logger)
        {
            _dataSourceRepository = dataSourceRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<List<DataSourceDto>> GetDataSourcesAsync(Guid tenantId)
        {
            try
            {
                var dataSources = await _dataSourceRepository.GetDataSourcesByTenantAsync(tenantId);
                var result = dataSources.Select(MapToDto).ToList();

                _logger.LogDebug("Retrieved {Count} data sources for tenant {TenantId}", result.Count, tenantId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving data sources for tenant {TenantId}", tenantId);
                throw;
            }
        }

        public async Task<DataSourceDto?> GetDataSourceAsync(Guid dataSourceId, Guid tenantId)
        {
            try
            {
                var dataSource = await _dataSourceRepository.GetDataSourceWithUsageAsync(dataSourceId, tenantId);
                if (dataSource == null)
                {
                    _logger.LogWarning("Data source {DataSourceId} not found for tenant {TenantId}", dataSourceId, tenantId);
                    return null;
                }

                return MapToDto(dataSource);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving data source {DataSourceId} for tenant {TenantId}", dataSourceId, tenantId);
                throw;
            }
        }

        public async Task<QueryResultDto> ExecuteQueryAsync(Guid dataSourceId, QueryDataSourceDto queryDto, Guid tenantId)
        {
            try
            {
                // Get the data source first
                var dataSource = await _dataSourceRepository.GetByIdAsync(dataSourceId);
                if (dataSource == null || dataSource.TenantId != tenantId)
                {
                    throw new InvalidOperationException("Data source not found or access denied");
                }

                // Update usage statistics
                await _dataSourceRepository.UpdateUsageCountAsync(dataSourceId);

                // For now, return a simple mock result
                // TODO: Implement actual query execution based on data source type
                return new QueryResultDto
                {
                    Data = new List<Dictionary<string, object>>(),
                    Columns = new List<ColumnInfo>(),
                    TotalRows = 0,
                    ExecutionTime = TimeSpan.FromMilliseconds(100),
                    QueryUsed = queryDto.Query
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing query on data source {DataSourceId}", dataSourceId);
                throw;
            }
        }

        public async Task<DataSourceDto> CreateDataSourceAsync(CreateDataSourceDto createDto, Guid tenantId, Guid userId)
        {
            try
            {
                // Check if name already exists
                var existingByName = await _dataSourceRepository.GetByNameAsync(createDto.Name, tenantId);
                if (existingByName != null)
                {
                    throw new InvalidOperationException($"A data source with the name '{createDto.Name}' already exists");
                }

                var dataSource = new DataSource
                {
                    Id = Guid.NewGuid(),
                    Name = createDto.Name,
                    Description = createDto.Description,
                    Type = (int)createDto.Type,
                    Host = createDto.Host,
                    Port = createDto.Port,
                    DatabaseName = createDto.DatabaseName,
                    Username = createDto.Username,
                    EncryptedPassword = EncryptPassword(createDto.Password),
                    AdditionalSettings = System.Text.Json.JsonSerializer.Serialize(createDto.AdditionalSettings),
                    IsActive = createDto.IsActive,
                    TenantId = tenantId,
                    CreatedByUserId = userId,
                    CreatedAt = DateTime.UtcNow
                };

                await _dataSourceRepository.AddAsync(dataSource);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Created data source {DataSourceId} for tenant {TenantId}", dataSource.Id, tenantId);
                return MapToDto(dataSource);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating data source for tenant {TenantId}", tenantId);
                throw;
            }
        }

        public async Task<DataSourceDto?> UpdateDataSourceAsync(Guid dataSourceId, UpdateDataSourceDto updateDto, Guid tenantId, Guid userId)
        {
            try
            {
                var dataSource = await _dataSourceRepository.GetByIdAsync(dataSourceId);
                if (dataSource == null || dataSource.TenantId != tenantId)
                {
                    return null;
                }

                // Check if name change conflicts with existing
                if (!string.IsNullOrEmpty(updateDto.Name) && updateDto.Name != dataSource.Name)
                {
                    var nameExists = await _dataSourceRepository.IsDataSourceNameExistsAsync(updateDto.Name, tenantId, dataSourceId);
                    if (nameExists)
                    {
                        throw new InvalidOperationException($"A data source with the name '{updateDto.Name}' already exists");
                    }
                    dataSource.Name = updateDto.Name;
                }

                // Update properties
                if (!string.IsNullOrEmpty(updateDto.Description))
                {
                    dataSource.Description = updateDto.Description;
                }

                if (!string.IsNullOrEmpty(updateDto.Host))
                {
                    dataSource.Host = updateDto.Host;
                }

                if (updateDto.Port.HasValue)
                {
                    dataSource.Port = updateDto.Port;
                }

                if (!string.IsNullOrEmpty(updateDto.DatabaseName))
                {
                    dataSource.DatabaseName = updateDto.DatabaseName;
                }

                if (!string.IsNullOrEmpty(updateDto.Username))
                {
                    dataSource.Username = updateDto.Username;
                }

                if (!string.IsNullOrEmpty(updateDto.Password))
                {
                    dataSource.EncryptedPassword = EncryptPassword(updateDto.Password);
                }

                if (updateDto.AdditionalSettings != null)
                {
                    dataSource.AdditionalSettings = System.Text.Json.JsonSerializer.Serialize(updateDto.AdditionalSettings);
                }

                if (updateDto.IsActive.HasValue)
                {
                    dataSource.IsActive = updateDto.IsActive.Value;
                }

                dataSource.UpdatedAt = DateTime.UtcNow;

                await _dataSourceRepository.UpdateAsync(dataSource);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Updated data source {DataSourceId} for tenant {TenantId}", dataSourceId, tenantId);
                return MapToDto(dataSource);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating data source {DataSourceId}", dataSourceId);
                throw;
            }
        }

        public async Task<bool> DeleteDataSourceAsync(Guid dataSourceId, Guid tenantId, Guid userId)
        {
            try
            {
                var dataSource = await _dataSourceRepository.GetByIdAsync(dataSourceId);
                if (dataSource == null || dataSource.TenantId != tenantId)
                {
                    return false;
                }

                // Soft delete
                dataSource.IsDeleted = true;
                dataSource.DeletedAt = DateTime.UtcNow;
                await _dataSourceRepository.UpdateAsync(dataSource);
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Deleted data source {DataSourceId} for tenant {TenantId}", dataSourceId, tenantId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting data source {DataSourceId}", dataSourceId);
                throw;
            }
        }

        public async Task<ConnectionTestResult> TestConnectionAsync(TestConnectionDto testDto)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                _logger.LogInformation("Testing connection to {Host}:{Port} database {Database} as {Username}",
                    testDto.Host, testDto.Port, testDto.DatabaseName, testDto.Username);

                var connectionString = BuildConnectionString(testDto);
                var connectionInfo = await TestDatabaseConnectionAsync(connectionString, testDto.Type);

                var responseTime = DateTime.UtcNow - startTime;

                return new ConnectionTestResult
                {
                    IsSuccess = true,
                    ResponseTime = responseTime,
                    TestedAt = DateTime.UtcNow,
                    ConnectionInfo = connectionInfo
                };
            }
            catch (Exception ex)
            {
                var responseTime = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Error testing connection to {Host}:{Port}", testDto.Host, testDto.Port);

                return new ConnectionTestResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    ResponseTime = responseTime,
                    TestedAt = DateTime.UtcNow
                };
            }
        }

        public async Task<ConnectionTestResult> TestDataSourceConnectionAsync(Guid dataSourceId, Guid tenantId)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                var dataSource = await _dataSourceRepository.GetByIdAsync(dataSourceId);
                if (dataSource == null || dataSource.TenantId != tenantId)
                {
                    return new ConnectionTestResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Data source not found",
                        ResponseTime = TimeSpan.Zero,
                        TestedAt = DateTime.UtcNow
                    };
                }

                _logger.LogInformation("Testing connection for data source {DataSourceId} ({Name})", dataSourceId, dataSource.Name);

                // Build connection string from data source
                var testDto = new TestConnectionDto
                {
                    Type = (DataSourceType)dataSource.Type,
                    Host = dataSource.Host,
                    Port = dataSource.Port,
                    DatabaseName = dataSource.DatabaseName,
                    Username = dataSource.Username,
                    Password = DecryptPassword(dataSource.EncryptedPassword)
                };

                var connectionString = BuildConnectionString(testDto);
                var connectionInfo = await TestDatabaseConnectionAsync(connectionString, testDto.Type);

                var responseTime = DateTime.UtcNow - startTime;

                // Update connection test status in database
                await _dataSourceRepository.UpdateConnectionStatusAsync(dataSourceId, true);
                await _unitOfWork.SaveChangesAsync();

                return new ConnectionTestResult
                {
                    IsSuccess = true,
                    ResponseTime = responseTime,
                    TestedAt = DateTime.UtcNow,
                    ConnectionInfo = connectionInfo
                };
            }
            catch (Exception ex)
            {
                var responseTime = DateTime.UtcNow - startTime;
                _logger.LogError(ex, "Error testing connection for data source {DataSourceId}", dataSourceId);

                // Update connection status to failed
                try
                {
                    await _dataSourceRepository.UpdateConnectionStatusAsync(dataSourceId, false, ex.Message);
                    await _unitOfWork.SaveChangesAsync();
                }
                catch (Exception updateEx)
                {
                    _logger.LogError(updateEx, "Failed to update connection status for data source {DataSourceId}", dataSourceId);
                }

                return new ConnectionTestResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    ResponseTime = responseTime,
                    TestedAt = DateTime.UtcNow
                };
            }
        }

        public async Task<DataSourceSchemaDto> GetSchemaAsync(Guid dataSourceId, Guid tenantId)
        {
            try
            {
                var dataSource = await _dataSourceRepository.GetByIdAsync(dataSourceId);
                if (dataSource == null || dataSource.TenantId != tenantId)
                {
                    throw new InvalidOperationException("Data source not found or access denied");
                }

                _logger.LogInformation("Retrieving schema for data source {DataSourceId} ({Name})", dataSourceId, dataSource.Name);

                // Validate data source configuration
                if (string.IsNullOrEmpty(dataSource.Host) || string.IsNullOrEmpty(dataSource.DatabaseName))
                {
                    _logger.LogWarning("Data source {DataSourceId} has incomplete connection configuration, returning sample schema", dataSourceId);
                    return GetSampleSchema();
                }

                // Build connection string
                var testDto = new TestConnectionDto
                {
                    Type = (DataSourceType)dataSource.Type,
                    Host = dataSource.Host,
                    Port = dataSource.Port,
                    DatabaseName = dataSource.DatabaseName,
                    Username = dataSource.Username,
                    Password = DecryptPassword(dataSource.EncryptedPassword)
                };

                var connectionString = BuildConnectionString(testDto);
                return await RetrieveDatabaseSchemaAsync(connectionString, testDto.Type);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving schema for data source {DataSourceId}, returning sample schema", dataSourceId);
                return GetSampleSchema();
            }
        }

        public async Task<List<DataSourceDto>> GetActiveDataSourcesAsync(Guid tenantId)
        {
            try
            {
                var dataSources = await _dataSourceRepository.GetDataSourcesByTenantAsync(tenantId, activeOnly: true);
                return dataSources.Select(MapToDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active data sources for tenant {TenantId}", tenantId);
                throw;
            }
        }

        private static string BuildConnectionString(TestConnectionDto dto)
        {
            switch (dto.Type)
            {
                case DataSourceType.SqlServer:
                    var portPart = dto.Port.HasValue ? $",{dto.Port.Value}" : string.Empty;
                    return $"Server={dto.Host}{portPart};Database={dto.DatabaseName};User Id={dto.Username};Password={dto.Password};TrustServerCertificate=true;";
                // TODO: Add other providers
                default:
                    throw new NotSupportedException($"Connection test for {dto.Type} is not yet supported.");
            }
        }

        private static async Task<Dictionary<string, object>> TestDatabaseConnectionAsync(string connectionString, DataSourceType type)
        {
            switch (type)
            {
                case DataSourceType.SqlServer:
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    await using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
                    {
                        await conn.OpenAsync();
                        var serverVersion = conn.ServerVersion;
                        await using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT 1";
                            await cmd.ExecuteScalarAsync();
                        }
                        sw.Stop();
                        return new Dictionary<string, object>
                        {
                            ["Status"] = "Connected",
                            ["ServerVersion"] = serverVersion,
                            ["ElapsedMs"] = sw.ElapsedMilliseconds
                        };
                    }
                default:
                    throw new NotSupportedException($"Connection test for {type} is not yet supported.");
            }
        }

        private async Task<DataSourceSchemaDto> RetrieveDatabaseSchemaAsync(string connectionString, DataSourceType type)
        {
            switch (type)
            {
                case DataSourceType.SqlServer:
                    return await RetrieveSqlServerSchemaAsync(connectionString);
                default:
                    throw new NotSupportedException($"Schema retrieval for {type} is not yet supported.");
            }
        }

        private async Task<DataSourceSchemaDto> RetrieveSqlServerSchemaAsync(string connectionString)
        {
            var tables = new List<TableInfo>();
            var views = new List<ViewInfo>();
            var storedProcedures = new List<StoredProcedureInfo>();

            await using var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
            await conn.OpenAsync();

            // Get Tables - First collect table info, then get columns separately
            var tableInfoList = new List<(string SchemaName, string TableName, int RowCount)>();

            const string tablesQuery = @"
                SELECT 
                    TABLE_SCHEMA as SchemaName,
                    TABLE_NAME as TableName,
                    0 as [RowCount]
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE'
                ORDER BY TABLE_SCHEMA, TABLE_NAME";

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = tablesQuery;
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var schemaName = reader.GetString(0);
                    var tableName = reader.GetString(1);
                    var rowCount = reader.GetInt32(2); // Now it's always 0

                    tableInfoList.Add((schemaName, tableName, rowCount));
                }
            }

            // Now get columns for each table (after the reader is closed)
            foreach (var (schemaName, tableName, rowCount) in tableInfoList)
            {
                tables.Add(new TableInfo
                {
                    Name = tableName,
                    Schema = schemaName,
                    RowCount = rowCount,
                    Columns = await GetTableColumnsAsync(conn, schemaName, tableName)
                });
            }

            // Get Views - First collect view info, then get columns separately
            var viewInfoList = new List<(string SchemaName, string ViewName)>();

            const string viewsQuery = @"
                SELECT 
                    TABLE_SCHEMA as SchemaName,
                    TABLE_NAME as ViewName
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'VIEW'
                ORDER BY TABLE_SCHEMA, TABLE_NAME";

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = viewsQuery;
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var schemaName = reader.GetString(0);
                    var viewName = reader.GetString(1);

                    viewInfoList.Add((schemaName, viewName));
                }
            }

            // Now get columns for each view (after the reader is closed)
            foreach (var (schemaName, viewName) in viewInfoList)
            {
                views.Add(new ViewInfo
                {
                    Name = viewName,
                    Schema = schemaName,
                    Columns = await GetViewColumnsAsync(conn, schemaName, viewName)
                });
            }

            // Get Stored Procedures - First collect procedure info, then get parameters separately
            var procedureInfoList = new List<(string SchemaName, string ProcedureName)>();

            const string proceduresQuery = @"
                SELECT 
                    ROUTINE_SCHEMA as SchemaName,
                    ROUTINE_NAME as ProcedureName
                FROM INFORMATION_SCHEMA.ROUTINES
                WHERE ROUTINE_TYPE = 'PROCEDURE'
                ORDER BY ROUTINE_SCHEMA, ROUTINE_NAME";

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = proceduresQuery;
                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    var schemaName = reader.GetString(0);
                    var procedureName = reader.GetString(1);

                    procedureInfoList.Add((schemaName, procedureName));
                }
            }

            // Now get parameters for each stored procedure (after the reader is closed)
            foreach (var (schemaName, procedureName) in procedureInfoList)
            {
                storedProcedures.Add(new StoredProcedureInfo
                {
                    Name = procedureName,
                    Schema = schemaName,
                    Parameters = await GetProcedureParametersAsync(conn, schemaName, procedureName)
                });
            }

            return new DataSourceSchemaDto
            {
                Tables = tables,
                Views = views,
                StoredProcedures = storedProcedures
            };
        }

        private static async Task<List<ColumnInfo>> GetTableColumnsAsync(Microsoft.Data.SqlClient.SqlConnection conn, string schemaName, string tableName)
        {
            var columns = new List<ColumnInfo>();

            const string columnsQuery = @"
                SELECT 
                    c.COLUMN_NAME,
                    c.DATA_TYPE,
                    c.IS_NULLABLE,
                    c.CHARACTER_MAXIMUM_LENGTH,
                    c.NUMERIC_PRECISION,
                    c.NUMERIC_SCALE,
                    CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END as IS_PRIMARY_KEY
                FROM INFORMATION_SCHEMA.COLUMNS c
                LEFT JOIN (
                    SELECT ku.COLUMN_NAME
                    FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc ON ku.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                    WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY' 
                        AND ku.TABLE_SCHEMA = @SchemaName 
                        AND ku.TABLE_NAME = @TableName
                ) pk ON c.COLUMN_NAME = pk.COLUMN_NAME
                WHERE c.TABLE_SCHEMA = @SchemaName AND c.TABLE_NAME = @TableName
                ORDER BY c.ORDINAL_POSITION";

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = columnsQuery;
            cmd.Parameters.AddWithValue("@SchemaName", schemaName);
            cmd.Parameters.AddWithValue("@TableName", tableName);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(new ColumnInfo
                {
                    Name = reader.GetString(0),
                    DataType = reader.GetString(1),
                    IsNullable = reader.GetString(2) == "YES",
                    IsPrimaryKey = reader.GetInt32(6) == 1,
                    MaxLength = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    Precision = reader.IsDBNull(4) ? null : reader.GetByte(4),
                    Scale = reader.IsDBNull(5) ? null : reader.GetInt32(5)
                });
            }

            return columns;
        }

        private static async Task<List<ColumnInfo>> GetViewColumnsAsync(Microsoft.Data.SqlClient.SqlConnection conn, string schemaName, string viewName)
        {
            var columns = new List<ColumnInfo>();

            const string columnsQuery = @"
                SELECT 
                    COLUMN_NAME,
                    DATA_TYPE,
                    IS_NULLABLE,
                    CHARACTER_MAXIMUM_LENGTH,
                    NUMERIC_PRECISION,
                    NUMERIC_SCALE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = @SchemaName AND TABLE_NAME = @ViewName
                ORDER BY ORDINAL_POSITION";

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = columnsQuery;
            cmd.Parameters.AddWithValue("@SchemaName", schemaName);
            cmd.Parameters.AddWithValue("@ViewName", viewName);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(new ColumnInfo
                {
                    Name = reader.GetString(0),
                    DataType = reader.GetString(1),
                    IsNullable = reader.GetString(2) == "YES",
                    IsPrimaryKey = false, // Views don't have primary keys
                    MaxLength = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    Precision = reader.IsDBNull(4) ? null : reader.GetByte(4),
                    Scale = reader.IsDBNull(5) ? null : reader.GetInt32(5)
                });
            }

            return columns;
        }

        private static async Task<List<ParameterInfo>> GetProcedureParametersAsync(Microsoft.Data.SqlClient.SqlConnection conn, string schemaName, string procedureName)
        {
            var parameters = new List<ParameterInfo>();

            const string parametersQuery = @"
                SELECT 
                    PARAMETER_NAME,
                    DATA_TYPE,
                    PARAMETER_MODE
                FROM INFORMATION_SCHEMA.PARAMETERS
                WHERE SPECIFIC_SCHEMA = @SchemaName AND SPECIFIC_NAME = @ProcedureName
                ORDER BY ORDINAL_POSITION";

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = parametersQuery;
            cmd.Parameters.AddWithValue("@SchemaName", schemaName);
            cmd.Parameters.AddWithValue("@ProcedureName", procedureName);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                parameters.Add(new ParameterInfo
                {
                    Name = reader.GetString(0),
                    DataType = reader.GetString(1),
                    IsOutput = reader.GetString(2).Contains("OUT")
                });
            }

            return parameters;
        }

        public async Task UpdateUsageStatsAsync(Guid dataSourceId)
        {
            try
            {
                await _dataSourceRepository.UpdateUsageCountAsync(dataSourceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating usage stats for data source {DataSourceId}", dataSourceId);
                // Don't throw - this is not critical
            }
        }

        // Helper methods
        private DataSourceDto MapToDto(DataSource dataSource)
        {
            return new DataSourceDto
            {
                Id = dataSource.Id,
                Name = dataSource.Name,
                Description = dataSource.Description,
                Type = (DataSourceType)dataSource.Type,
                TypeName = ((DataSourceType)dataSource.Type).ToString(),
                Host = dataSource.Host,
                Port = dataSource.Port,
                DatabaseName = dataSource.DatabaseName,
                Username = dataSource.Username,
                AdditionalSettings = ParseAdditionalSettings(dataSource.AdditionalSettings),
                IsActive = dataSource.IsActive,
                LastConnectionTest = dataSource.LastConnectionTest,
                LastConnectionSuccess = dataSource.LastConnectionSuccess,
                LastConnectionError = dataSource.LastConnectionError,
                CreatedBy = "User", // TODO: Get from CreatedByUser navigation property
                CreatedAt = dataSource.CreatedAt,
                LastUsed = dataSource.LastUsed,
                UsageCount = dataSource.UsageCount,
                Status = DetermineConnectionStatus(dataSource)
            };
        }

        private static Dictionary<string, object>? ParseAdditionalSettings(string? settingsJson)
        {
            if (string.IsNullOrEmpty(settingsJson))
            {
                return null;
            }

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(settingsJson);
            }
            catch
            {
                return null;
            }
        }

        private static ConnectionStatus DetermineConnectionStatus(DataSource dataSource)
        {
            if (!dataSource.LastConnectionTest.HasValue)
            {
                return ConnectionStatus.Unknown;
            }

            if (dataSource.LastConnectionSuccess == true)
            {
                return ConnectionStatus.Connected;
            }

            if (dataSource.LastConnectionSuccess == false)
            {
                return ConnectionStatus.Error;
            }

            return ConnectionStatus.Unknown;
        }

        private static string? EncryptPassword(string? password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return null;
            }

            // TODO: Implement proper encryption
            // For now, just encode as base64 (NOT SECURE - implement proper encryption)
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password));
        }

        private string? DecryptPassword(string? encryptedPassword)
        {
            if (string.IsNullOrEmpty(encryptedPassword))
            {
                return null;
            }

            try
            {
                // TODO: Implement proper decryption
                // For now, just decode from base64 (matches the EncryptPassword method above)
                return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encryptedPassword));
            }
            catch
            {
                _logger.LogWarning("Failed to decrypt password, returning null");
                return null;
            }
        }

        public async Task<DataSourceSchemaDto> RetrieveDatabaseSchemaDirectAsync(string connectionString)
        {
            return await RetrieveDatabaseSchemaAsync(connectionString, DataSourceType.SqlServer);
        }

        public async Task<DataSourceSchemaDto> GetSampleSchemaAsync()
        {
            return await Task.FromResult(GetSampleSchema());
        }

        private static DataSourceSchemaDto GetSampleSchema()
        {
            // Return a comprehensive sample schema for demonstration
            return new DataSourceSchemaDto
            {
                Tables = new List<TableInfo>
                {
                    new TableInfo
                    {
                        Name = "Users",
                        Schema = "dbo",
                        RowCount = 1250,
                        Columns = new List<ColumnInfo>
                        {
                            new ColumnInfo { Name = "Id", DataType = "uniqueidentifier", IsPrimaryKey = true, IsNullable = false },
                            new ColumnInfo { Name = "FirstName", DataType = "nvarchar", MaxLength = 50, IsNullable = false },
                            new ColumnInfo { Name = "LastName", DataType = "nvarchar", MaxLength = 50, IsNullable = false },
                            new ColumnInfo { Name = "Email", DataType = "nvarchar", MaxLength = 256, IsNullable = false },
                            new ColumnInfo { Name = "PhoneNumber", DataType = "nvarchar", MaxLength = 20, IsNullable = true },
                            new ColumnInfo { Name = "DateOfBirth", DataType = "date", IsNullable = true },
                            new ColumnInfo { Name = "IsActive", DataType = "bit", IsNullable = false },
                            new ColumnInfo { Name = "CreatedAt", DataType = "datetime2", IsNullable = false },
                            new ColumnInfo { Name = "UpdatedAt", DataType = "datetime2", IsNullable = true }
                        }
                    },
                    new TableInfo
                    {
                        Name = "Orders",
                        Schema = "dbo",
                        RowCount = 5680,
                        Columns = new List<ColumnInfo>
                        {
                            new ColumnInfo { Name = "Id", DataType = "uniqueidentifier", IsPrimaryKey = true, IsNullable = false },
                            new ColumnInfo { Name = "UserId", DataType = "uniqueidentifier", IsNullable = false },
                            new ColumnInfo { Name = "OrderNumber", DataType = "nvarchar", MaxLength = 50, IsNullable = false },
                            new ColumnInfo { Name = "OrderDate", DataType = "datetime2", IsNullable = false },
                            new ColumnInfo { Name = "TotalAmount", DataType = "decimal", Precision = 18, Scale = 2, IsNullable = false },
                            new ColumnInfo { Name = "TaxAmount", DataType = "decimal", Precision = 18, Scale = 2, IsNullable = false },
                            new ColumnInfo { Name = "DiscountAmount", DataType = "decimal", Precision = 18, Scale = 2, IsNullable = true },
                            new ColumnInfo { Name = "Status", DataType = "nvarchar", MaxLength = 20, IsNullable = false },
                            new ColumnInfo { Name = "ShippingAddress", DataType = "nvarchar", MaxLength = 500, IsNullable = true }
                        }
                    },
                    new TableInfo
                    {
                        Name = "Products",
                        Schema = "dbo",
                        RowCount = 2340,
                        Columns = new List<ColumnInfo>
                        {
                            new ColumnInfo { Name = "Id", DataType = "uniqueidentifier", IsPrimaryKey = true, IsNullable = false },
                            new ColumnInfo { Name = "Name", DataType = "nvarchar", MaxLength = 200, IsNullable = false },
                            new ColumnInfo { Name = "Description", DataType = "nvarchar", MaxLength = 1000, IsNullable = true },
                            new ColumnInfo { Name = "SKU", DataType = "nvarchar", MaxLength = 50, IsNullable = false },
                            new ColumnInfo { Name = "Price", DataType = "decimal", Precision = 18, Scale = 2, IsNullable = false },
                            new ColumnInfo { Name = "Cost", DataType = "decimal", Precision = 18, Scale = 2, IsNullable = true },
                            new ColumnInfo { Name = "StockQuantity", DataType = "int", IsNullable = false },
                            new ColumnInfo { Name = "CategoryId", DataType = "uniqueidentifier", IsNullable = true },
                            new ColumnInfo { Name = "IsDiscontinued", DataType = "bit", IsNullable = false }
                        }
                    },
                    new TableInfo
                    {
                        Name = "Categories",
                        Schema = "dbo",
                        RowCount = 45,
                        Columns = new List<ColumnInfo>
                        {
                            new ColumnInfo { Name = "Id", DataType = "uniqueidentifier", IsPrimaryKey = true, IsNullable = false },
                            new ColumnInfo { Name = "Name", DataType = "nvarchar", MaxLength = 100, IsNullable = false },
                            new ColumnInfo { Name = "Description", DataType = "nvarchar", MaxLength = 500, IsNullable = true },
                            new ColumnInfo { Name = "ParentCategoryId", DataType = "uniqueidentifier", IsNullable = true },
                            new ColumnInfo { Name = "SortOrder", DataType = "int", IsNullable = false }
                        }
                    }
                },
                Views = new List<ViewInfo>
                {
                    new ViewInfo
                    {
                        Name = "UserOrderSummary",
                        Schema = "dbo",
                        Columns = new List<ColumnInfo>
                        {
                            new ColumnInfo { Name = "UserId", DataType = "uniqueidentifier", IsNullable = false },
                            new ColumnInfo { Name = "UserName", DataType = "nvarchar", MaxLength = 101, IsNullable = false },
                            new ColumnInfo { Name = "Email", DataType = "nvarchar", MaxLength = 256, IsNullable = false },
                            new ColumnInfo { Name = "OrderCount", DataType = "int", IsNullable = false },
                            new ColumnInfo { Name = "TotalSpent", DataType = "decimal", Precision = 18, Scale = 2, IsNullable = false },
                            new ColumnInfo { Name = "LastOrderDate", DataType = "datetime2", IsNullable = true }
                        }
                    },
                    new ViewInfo
                    {
                        Name = "ProductSalesReport",
                        Schema = "dbo",
                        Columns = new List<ColumnInfo>
                        {
                            new ColumnInfo { Name = "ProductId", DataType = "uniqueidentifier", IsNullable = false },
                            new ColumnInfo { Name = "ProductName", DataType = "nvarchar", MaxLength = 200, IsNullable = false },
                            new ColumnInfo { Name = "CategoryName", DataType = "nvarchar", MaxLength = 100, IsNullable = true },
                            new ColumnInfo { Name = "UnitsSold", DataType = "int", IsNullable = false },
                            new ColumnInfo { Name = "TotalRevenue", DataType = "decimal", Precision = 18, Scale = 2, IsNullable = false },
                            new ColumnInfo { Name = "AveragePrice", DataType = "decimal", Precision = 18, Scale = 2, IsNullable = false }
                        }
                    }
                },
                StoredProcedures = new List<StoredProcedureInfo>
                {
                    new StoredProcedureInfo
                    {
                        Name = "GetUserOrders",
                        Schema = "dbo",
                        Parameters = new List<ParameterInfo>
                        {
                            new ParameterInfo { Name = "@UserId", DataType = "uniqueidentifier", IsOutput = false },
                            new ParameterInfo { Name = "@StartDate", DataType = "datetime2", IsOutput = false, DefaultValue = null },
                            new ParameterInfo { Name = "@EndDate", DataType = "datetime2", IsOutput = false, DefaultValue = null }
                        }
                    },
                    new StoredProcedureInfo
                    {
                        Name = "CalculateMonthlyRevenue",
                        Schema = "dbo",
                        Parameters = new List<ParameterInfo>
                        {
                            new ParameterInfo { Name = "@Year", DataType = "int", IsOutput = false },
                            new ParameterInfo { Name = "@Month", DataType = "int", IsOutput = false },
                            new ParameterInfo { Name = "@TotalRevenue", DataType = "decimal", IsOutput = true }
                        }
                    }
                }
            };
        }
    }
}
