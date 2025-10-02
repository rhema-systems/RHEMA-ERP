using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Core.DTOs.DataSources;
using ErpSystem.Core.Services;

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
        /// Get all data sources for the current tenant
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<DataSourceDto>>> GetDataSources()
        {
            try
            {
                var tenantId = _currentUserService.GetTenantId();
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
                var tenantId = _currentUserService.GetTenantId();
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
                var tenantId = _currentUserService.GetTenantId();
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
                var tenantId = _currentUserService.GetTenantId();
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = _currentUserService.GetUserId();
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
                var tenantId = _currentUserService.GetTenantId();
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = _currentUserService.GetUserId();
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
                var tenantId = _currentUserService.GetTenantId();
                if (!tenantId.HasValue)
                {
                    return BadRequest("TenantId not found in token");
                }

                var userId = _currentUserService.GetUserId();
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
                var tenantId = _currentUserService.GetTenantId();
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
                var tenantId = _currentUserService.GetTenantId();
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
                var tenantId = _currentUserService.GetTenantId();
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