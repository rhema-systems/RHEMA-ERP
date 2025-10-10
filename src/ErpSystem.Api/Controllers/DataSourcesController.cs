using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.DTOs.DataSources;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services;
using ErpSystem.Core.Entities;

namespace ErpSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DataSourcesController : ControllerBase
    {
        private readonly IDataSourceService _dataSourceService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<DataSourcesController> _logger;

        public DataSourcesController(
            IDataSourceService dataSourceService,
            ICurrentUserService currentUserService,
            ILogger<DataSourcesController> logger)
        {
            _dataSourceService = dataSourceService;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        /// <summary>
        /// Debug endpoint to test data source database connection (temporary)
        /// </summary>
        [HttpGet("debug")]
        [AllowAnonymous]
        public async Task<ActionResult> DebugDataSources()
        {
            try
            {
                var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"); // Default tenant
                var dataSources = await _dataSourceService.GetDataSourcesAsync(tenantId);
                return Ok(new { 
                    Success = true, 
                    Count = dataSources.Count, 
                    DataSources = dataSources 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Debug: Error retrieving data sources");
                return Ok(new { 
                    Success = false, 
                    Error = ex.Message,
                    StackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Debug endpoint to create sample data source using the ERP database connection
        /// </summary>
        [HttpPost("debug/create-erp-datasource")]
        [AllowAnonymous]
        public async Task<ActionResult> DebugCreateErpDataSource()
        {
            try
            {
                var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"); // Default tenant
                var userId = Guid.Parse("00000000-0000-0000-0000-000000000001"); // Default admin user
                
                var createDto = new CreateDataSourceDto
                {
                    Name = "ERP System Database (Sample)",
                    Description = "Sample data source using the ERP system's own database for testing ReportBuilder functionality",
                    Type = DataSourceType.SqlServer,
                    Host = "localhost",
                    Port = null,
                    DatabaseName = "RHEMA-ERP",
                    Username = "sa",
                    Password = "sa",
                    IsActive = true
                };
                
                var dataSource = await _dataSourceService.CreateDataSourceAsync(createDto, tenantId, userId);
                return Ok(new { 
                    Success = true, 
                    Message = "ERP database data source created successfully. You can now use this in ReportBuilder to see real tables and schemas.",
                    DataSource = dataSource 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Debug: Error creating ERP data source");
                return Ok(new { 
                    Success = false, 
                    Error = ex.Message,
                    StackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Debug endpoint to test data source creation (temporary)
        /// </summary>
        [HttpPost("debug/create")]
        [AllowAnonymous]
        public async Task<ActionResult> DebugCreateDataSource()
        {
            try
            {
                var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"); // Default tenant
                var userId = Guid.Parse("00000000-0000-0000-0000-000000000001"); // Default admin user
                
                var createDto = new CreateDataSourceDto
                {
                    Name = "Debug Test Source " + DateTime.Now.ToString("HH:mm:ss"),
                    Description = "Created via debug endpoint",
                    Type = DataSourceType.SqlServer,
                    Host = "localhost",
                    Port = 1433,
                    DatabaseName = "DebugDB",
                    Username = "debuguser",
                    Password = "debugpass",
                    IsActive = true
                };
                
                var dataSource = await _dataSourceService.CreateDataSourceAsync(createDto, tenantId, userId);
                return Ok(new { 
                    Success = true, 
                    Message = "Data source created successfully",
                    DataSource = dataSource 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Debug: Error creating data source");
                return Ok(new { 
                    Success = false, 
                    Error = ex.Message,
                    StackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Debug endpoint to test schema retrieval for the first available data source
        /// </summary>
        [HttpGet("debug/test-schema")]
        [AllowAnonymous]
        public async Task<ActionResult> DebugTestSchema()
        {
            try
            {
                var tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"); // Default tenant
                
                // Get the first available data source
                var dataSources = await _dataSourceService.GetActiveDataSourcesAsync(tenantId);
                if (!dataSources.Any())
                {
                    return Ok(new {
                        Success = false,
                        Error = "No data sources available. Use /debug/create-erp-datasource to create one first."
                    });
                }
                
                var firstDataSource = dataSources.First();
                
                // Test schema retrieval
                var schema = await _dataSourceService.GetSchemaAsync(firstDataSource.Id, tenantId);
                
                return Ok(new {
                    Success = true,
                    Message = $"Schema retrieved successfully from data source: {firstDataSource.Name}",
                    DataSourceId = firstDataSource.Id,
                    DataSourceName = firstDataSource.Name,
                    TablesCount = schema.Tables.Count,
                    ViewsCount = schema.Views.Count,
                    StoredProceduresCount = schema.StoredProcedures.Count,
                    SampleTables = schema.Tables.Take(5).Select(t => new {
                        t.Name,
                        t.Schema,
                        ColumnCount = t.Columns.Count,
                        SampleColumns = t.Columns.Take(3).Select(c => new { c.Name, c.DataType }).ToList()
                    }).ToList(),
                    Schema = schema // Full schema for debugging
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Debug: Error testing schema retrieval");
                return Ok(new {
                    Success = false,
                    Error = ex.Message,
                    StackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Simple debug endpoint to test raw database connection
        /// </summary>
        [HttpGet("debug/connection-test")]
        [AllowAnonymous]
        public async Task<ActionResult> DebugConnectionTest()
        {
            try
            {
                var connectionString = "Server=localhost;Database=RHEMA-ERP;User Id=sa;Password=sa;TrustServerCertificate=true;MultipleActiveResultSets=true;";
                _logger.LogInformation("Testing direct database connection to RHEMA-ERP...");
                
                await using var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
                await conn.OpenAsync();
                
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";
                var tableCount = await cmd.ExecuteScalarAsync();
                
                return Ok(new {
                    Success = true,
                    Message = "Database connection successful",
                    TableCount = tableCount,
                    ConnectionString = connectionString.Replace("Password=sa", "Password=***")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Debug connection test failed: {Error}", ex.Message);
                return Ok(new {
                    Success = false,
                    Error = ex.Message,
                    StackTrace = ex.StackTrace
                });
            }
        }
        
        /// <summary>
        /// Get schema from the RHEMA-ERP database
        /// </summary>
        [HttpGet("schema")]
        [AllowAnonymous]
        public async Task<ActionResult<DataSourceSchemaDto>> GetErpSchema()
        {
            try
            {
                _logger.LogInformation("Retrieving schema from RHEMA-ERP database");
                
                // Get the connection string from configuration (includes user secrets)
                var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                
                // Log the database we're connecting to
                if (!string.IsNullOrEmpty(connectionString))
                {
                    var dbName = "Unknown";
                    if (connectionString.Contains("Database="))
                    {
                        var dbPart = connectionString.Split(';').FirstOrDefault(s => s.StartsWith("Database="));
                        if (dbPart != null)
                        {
                            dbName = dbPart.Split('=')[1];
                        }
                    }
                    _logger.LogInformation("Connecting to database: {DatabaseName}", dbName);
                }
                
                if (string.IsNullOrEmpty(connectionString))
                {
                    _logger.LogWarning("No DefaultConnection configured, using sample schema");
                    var fallbackService = _dataSourceService as EnterpriseDataSourceService;
                    if (fallbackService != null)
                    {
                        return Ok(await fallbackService.GetSampleSchemaAsync());
                    }
                    return Ok(GetEmptySchema());
                }
                
                // Use the existing service method to get schema
                var concreteDataSourceService = _dataSourceService as EnterpriseDataSourceService;
                if (concreteDataSourceService != null)
                {
                    _logger.LogInformation("Attempting to retrieve schema from RHEMA-ERP database...");
                    var schema = await concreteDataSourceService.RetrieveDatabaseSchemaDirectAsync(connectionString);
                    _logger.LogInformation("Successfully retrieved schema with {TableCount} tables, {ViewCount} views", schema.Tables?.Count ?? 0, schema.Views?.Count ?? 0);
                    return Ok(schema);
                }
                
                _logger.LogError("Service implementation not available - could not cast to EnterpriseDataSourceService");
                throw new InvalidOperationException("Service implementation not available");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving RHEMA-ERP schema: {ErrorMessage}. Falling back to sample schema", ex.Message);
                
                // Fallback to sample schema
                var concreteService = _dataSourceService as EnterpriseDataSourceService;
                if (concreteService != null)
                {
                    _logger.LogWarning("Using sample schema as fallback due to database connection failure");
                    return Ok(await concreteService.GetSampleSchemaAsync());
                }
                
                // Manual fallback if cast fails
                return Ok(GetEmptySchema());
            }
        }
        
        private static DataSourceSchemaDto GetEmptySchema()
        {
            return new DataSourceSchemaDto
            {
                Tables = new List<TableInfo>(),
                Views = new List<ViewInfo>(),
                StoredProcedures = new List<StoredProcedureInfo>()
            };
        }

        /// <summary>
        /// Get all data sources for the current tenant
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<DataSourceDto>>> GetDataSources()
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var dataSources = await _dataSourceService.GetDataSourcesAsync(tenantId.Value);
                return Ok(dataSources);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving data sources");
                return StatusCode(500, "An error occurred while retrieving data sources");
            }
        }

        /// <summary>
        /// Get active data sources for the current tenant
        /// </summary>
        [HttpGet("active")]
        public async Task<ActionResult<List<DataSourceDto>>> GetActiveDataSources()
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var dataSources = await _dataSourceService.GetActiveDataSourcesAsync(tenantId.Value);
                return Ok(dataSources);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active data sources");
                return StatusCode(500, "An error occurred while retrieving active data sources");
            }
        }

        /// <summary>
        /// Get a specific data source by ID
        /// </summary>
        [HttpGet("{dataSourceId:guid}")]
        public async Task<ActionResult<DataSourceDto>> GetDataSource(Guid dataSourceId)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var dataSource = await _dataSourceService.GetDataSourceAsync(dataSourceId, tenantId.Value);
                if (dataSource == null)
                {
                    return NotFound();
                }

                return Ok(dataSource);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving data source {DataSourceId}", dataSourceId);
                return StatusCode(500, "An error occurred while retrieving the data source");
            }
        }

        /// <summary>
        /// Create a new data source
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<DataSourceDto>> CreateDataSource(CreateDataSourceDto createDto)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var dataSource = await _dataSourceService.CreateDataSourceAsync(createDto, tenantId.Value, userId.Value);
                
                return CreatedAtAction(
                    nameof(GetDataSource), 
                    new { dataSourceId = dataSource.Id }, 
                    dataSource);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating data source");
                return StatusCode(500, "An error occurred while creating the data source");
            }
        }

        /// <summary>
        /// Update an existing data source
        /// </summary>
        [HttpPut("{dataSourceId:guid}")]
        public async Task<ActionResult<DataSourceDto>> UpdateDataSource(Guid dataSourceId, UpdateDataSourceDto updateDto)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var dataSource = await _dataSourceService.UpdateDataSourceAsync(dataSourceId, updateDto, tenantId.Value, userId.Value);
                
                if (dataSource == null)
                {
                    return NotFound();
                }

                return Ok(dataSource);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating data source {DataSourceId}", dataSourceId);
                return StatusCode(500, "An error occurred while updating the data source");
            }
        }

        /// <summary>
        /// Delete a data source
        /// </summary>
        [HttpDelete("{dataSourceId:guid}")]
        public async Task<ActionResult> DeleteDataSource(Guid dataSourceId)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId) ? (Guid?)parsedUserId : null;
                if (!userId.HasValue)
                {
                    return BadRequest("UserId not found in token");
                }

                var success = await _dataSourceService.DeleteDataSourceAsync(dataSourceId, tenantId.Value, userId.Value);
                
                if (!success)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting data source {DataSourceId}", dataSourceId);
                return StatusCode(500, "An error occurred while deleting the data source");
            }
        }

        /// <summary>
        /// Test connection to a data source without saving it
        /// </summary>
        [HttpPost("test-connection")]
        public async Task<ActionResult<ConnectionTestResult>> TestConnection(TestConnectionDto testDto)
        {
            try
            {
                var result = await _dataSourceService.TestConnectionAsync(testDto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing data source connection");
                return StatusCode(500, "An error occurred while testing the connection");
            }
        }

        /// <summary>
        /// Test connection to an existing data source
        /// </summary>
        [HttpPost("{dataSourceId:guid}/test-connection")]
        public async Task<ActionResult<ConnectionTestResult>> TestDataSourceConnection(Guid dataSourceId)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var result = await _dataSourceService.TestDataSourceConnectionAsync(dataSourceId, tenantId.Value);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing data source connection {DataSourceId}", dataSourceId);
                return StatusCode(500, "An error occurred while testing the data source connection");
            }
        }

        /// <summary>
        /// Get schema information for a data source
        /// </summary>
        [HttpGet("{dataSourceId:guid}/schema")]
        public async Task<ActionResult<DataSourceSchemaDto>> GetSchema(Guid dataSourceId)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var schema = await _dataSourceService.GetSchemaAsync(dataSourceId, tenantId.Value);
                return Ok(schema);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving schema for data source {DataSourceId}", dataSourceId);
                return StatusCode(500, "An error occurred while retrieving the schema");
            }
        }

        /// <summary>
        /// Execute a query against a data source
        /// </summary>
        [HttpPost("{dataSourceId:guid}/query")]
        public async Task<ActionResult<QueryResultDto>> ExecuteQuery(Guid dataSourceId, QueryDataSourceDto queryDto)
        {
            try
            {
                var tenantId = _currentUserService.TenantId;
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                // Update usage stats
                await _dataSourceService.UpdateUsageStatsAsync(dataSourceId);

                var result = await _dataSourceService.ExecuteQueryAsync(dataSourceId, queryDto, tenantId.Value);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing query against data source {DataSourceId}", dataSourceId);
                return StatusCode(500, "An error occurred while executing the query");
            }
        }
    }
}
