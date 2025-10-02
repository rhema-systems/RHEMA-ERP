using ErpSystem.Core.DTOs.DataSources;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ErpSystem.Core.Services
{
    public class EnterpriseDataSourceService : IDataSourceService
    {
        private readonly IConnectionPoolManager _connectionPoolManager;
        private readonly IQueryCacheService _queryCacheService;
        private readonly IQueryExecutionService _queryExecutionService;
        private readonly IRateLimitingService _rateLimitingService;
        private readonly ILogger<EnterpriseDataSourceService> _logger;
        private readonly PerformanceSettings _performanceSettings;

        public EnterpriseDataSourceService(
            IConnectionPoolManager connectionPoolManager,
            IQueryCacheService queryCacheService,
            IQueryExecutionService queryExecutionService,
            IRateLimitingService rateLimitingService,
            ILogger<EnterpriseDataSourceService> logger,
            IOptions<PerformanceSettings> performanceSettings)
        {
            _connectionPoolManager = connectionPoolManager;
            _queryCacheService = queryCacheService;
            _queryExecutionService = queryExecutionService;
            _rateLimitingService = rateLimitingService;
            _logger = logger;
            _performanceSettings = performanceSettings.Value;
        }

        public async Task<List<DataSourceDto>> GetDataSourcesAsync(Guid tenantId)
        {
            var cacheKey = $"datasources:tenant:{tenantId}";
            var cached = await _queryCacheService.GetCachedResultAsync(cacheKey);
            
            if (cached != null)
            {
                _logger.LogDebug("Returning cached data sources for tenant {TenantId}", tenantId);
                return ConvertFromCachedResult<List<DataSourceDto>>(cached);
            }

            // Simulate database call - replace with actual implementation
            await Task.Delay(100);
            
            var dataSources = GetMockDataSources();
            
            // Cache the results
            var resultToCache = CreateCacheableResult(dataSources);
            await _queryCacheService.SetCachedResultAsync(
                cacheKey, 
                resultToCache, 
                TimeSpan.FromMinutes(10)
            );

            return dataSources;
        }

        public async Task<DataSourceDto?> GetDataSourceAsync(Guid dataSourceId, Guid tenantId)
        {
            var cacheKey = $"datasource:{dataSourceId}:tenant:{tenantId}";
            var cached = await _queryCacheService.GetCachedResultAsync(cacheKey);
            
            if (cached != null)
            {
                return ConvertFromCachedResult<DataSourceDto>(cached);
            }

            // Simulate database call
            await Task.Delay(50);
            
            var dataSource = GetMockDataSources().FirstOrDefault(ds => ds.Id == dataSourceId);
            
            if (dataSource != null)
            {
                var resultToCache = CreateCacheableResult(dataSource);
                await _queryCacheService.SetCachedResultAsync(
                    cacheKey, 
                    resultToCache, 
                    TimeSpan.FromMinutes(30)
                );
            }

            return dataSource;
        }

        public async Task<QueryResultDto> ExecuteQueryAsync(Guid dataSourceId, QueryDataSourceDto queryDto, Guid tenantId)
        {
            var userId = Guid.NewGuid(); // Get from context in real implementation
            
            // Check rate limits first
            var rateLimitCheck = await _rateLimitingService.CheckRateLimitAsync(new RateLimitRequest
            {
                UserId = userId,
                TenantId = tenantId,
                DataSourceId = dataSourceId,
                Type = RateLimitType.QueryExecution,
                RequestedWeight = CalculateQueryWeight(queryDto)
            });

            if (!rateLimitCheck.IsAllowed)
            {
                _logger.LogWarning("Query execution rate limited for user {UserId}: {Reason}", 
                    userId, rateLimitCheck.ReasonMessage);
                throw new InvalidOperationException($"Rate limit exceeded: {rateLimitCheck.ReasonMessage}");
            }

            // Check cache first
            var cacheKey = _queryCacheService.GenerateCacheKey(dataSourceId, queryDto.Query, queryDto.Parameters);
            var cachedResult = await _queryCacheService.GetCachedResultAsync(cacheKey);
            
            if (cachedResult != null)
            {
                _logger.LogDebug("Returning cached query result for key {CacheKey}", cacheKey);
                return ConvertFromCachedResult<QueryResultDto>(cachedResult);
            }

            // For long-running queries, use background processing
            if (ShouldUseBackgroundProcessing(queryDto))
            {
                var executionId = await _queryExecutionService.SubmitQueryAsync(new QueryExecutionRequest
                {
                    DataSourceId = dataSourceId,
                    Query = queryDto.Query,
                    Parameters = queryDto.Parameters,
                    MaxRows = queryDto.MaxRows,
                    UserId = userId,
                    TenantId = tenantId,
                    Priority = DetermineQueryPriority(userId),
                    CacheResult = true
                });

                // For this demo, we'll simulate immediate execution
                // In production, you'd return the execution ID and have the client poll for results
                await Task.Delay(2000);
                
                var executionResult = await _queryExecutionService.GetQueryResultAsync(executionId);
                return executionResult.Result ?? throw new InvalidOperationException("Query execution failed");
            }

            // Execute directly for simple queries
            var result = await ExecuteQueryDirectly(dataSourceId, queryDto);

            // Cache the result if it's not too large
            if (ShouldCacheResult(result))
            {
                var cacheExpiration = CalculateCacheExpiration(queryDto);
                var resultToCache = CreateCacheableResult(result);
                await _queryCacheService.SetCachedResultAsync(cacheKey, resultToCache, cacheExpiration);
            }

            return result;
        }

        public async Task<DataSourceDto> CreateDataSourceAsync(CreateDataSourceDto createDto, Guid tenantId, Guid userId)
        {
            // Invalidate tenant cache
            await _queryCacheService.InvalidateCachePatternAsync($"datasources:tenant:{tenantId}*");
            
            // Simulate creation
            await Task.Delay(200);
            
            var dataSource = new DataSourceDto
            {
                Id = Guid.NewGuid(),
                Name = createDto.Name,
                Description = createDto.Description,
                Type = createDto.Type,
                TypeName = GetTypeName(createDto.Type),
                Host = createDto.Host,
                Port = createDto.Port,
                DatabaseName = createDto.DatabaseName,
                Username = createDto.Username,
                AdditionalSettings = createDto.AdditionalSettings,
                IsActive = createDto.IsActive,
                CreatedBy = "Current User",
                CreatedAt = DateTime.Now,
                UsageCount = 0,
                Status = ConnectionStatus.Unknown
            };

            // Warm up connection pool for new data source
            if (_performanceSettings.ConnectionPool.EnableWarmup)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _connectionPoolManager.WarmupPoolAsync(
                            dataSource.Id, 
                            _performanceSettings.ConnectionPool.WarmupConnectionCount
                        );
                        _logger.LogInformation("Connection pool warmed up for data source {DataSourceId}", dataSource.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to warm up connection pool for data source {DataSourceId}", dataSource.Id);
                    }
                });
            }

            return dataSource;
        }

        public async Task<DataSourceDto?> UpdateDataSourceAsync(Guid dataSourceId, UpdateDataSourceDto updateDto, Guid tenantId, Guid userId)
        {
            // Invalidate caches
            await _queryCacheService.InvalidateCacheAsync(dataSourceId);
            await _queryCacheService.InvalidateCachePatternAsync($"datasources:tenant:{tenantId}*");

            // Clear connection pool to pick up new settings
            await _connectionPoolManager.ClearPoolAsync(dataSourceId);

            // Simulate update
            await Task.Delay(150);
            return await GetDataSourceAsync(dataSourceId, tenantId);
        }

        public async Task<bool> DeleteDataSourceAsync(Guid dataSourceId, Guid tenantId, Guid userId)
        {
            // Clear all related caches and resources
            await _queryCacheService.InvalidateCacheAsync(dataSourceId);
            await _queryCacheService.InvalidateCachePatternAsync($"datasources:tenant:{tenantId}*");
            await _connectionPoolManager.ClearPoolAsync(dataSourceId);

            await Task.Delay(100);
            return true;
        }

        public async Task<ConnectionTestResult> TestConnectionAsync(TestConnectionDto testDto)
        {
            // Rate limit connection tests
            var userId = Guid.NewGuid(); // Get from context
            var rateLimitCheck = await _rateLimitingService.CheckRateLimitAsync(new RateLimitRequest
            {
                UserId = userId,
                TenantId = Guid.NewGuid(), // Get from context
                DataSourceId = Guid.NewGuid(),
                Type = RateLimitType.ConnectionTest
            });

            if (!rateLimitCheck.IsAllowed)
            {
                return new ConnectionTestResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Rate limit exceeded for connection tests",
                    ResponseTime = TimeSpan.Zero,
                    TestedAt = DateTime.Now
                };
            }

            await Task.Delay(2000); // Simulate connection test
            return CreateMockConnectionResult();
        }

        public async Task<ConnectionTestResult> TestDataSourceConnectionAsync(Guid dataSourceId, Guid tenantId)
        {
            await Task.Delay(1500);
            return CreateMockConnectionResult();
        }

        public async Task<DataSourceSchemaDto> GetSchemaAsync(Guid dataSourceId, Guid tenantId)
        {
            var cacheKey = $"schema:{dataSourceId}";
            var cached = await _queryCacheService.GetCachedResultAsync(cacheKey);
            
            if (cached != null)
            {
                return ConvertFromCachedResult<DataSourceSchemaDto>(cached);
            }

            await Task.Delay(1000);
            var schema = CreateMockSchema();
            
            // Cache schema for longer periods since it changes infrequently
            var resultToCache = CreateCacheableResult(schema);
            await _queryCacheService.SetCachedResultAsync(
                cacheKey, 
                resultToCache, 
                TimeSpan.FromHours(4)
            );

            return schema;
        }

        public async Task<List<DataSourceDto>> GetActiveDataSourcesAsync(Guid tenantId)
        {
            var allDataSources = await GetDataSourcesAsync(tenantId);
            return allDataSources.Where(ds => ds.IsActive).ToList();
        }

        public async Task UpdateUsageStatsAsync(Guid dataSourceId)
        {
            // Invalidate cached data source to update usage stats
            await _queryCacheService.InvalidateCachePatternAsync($"datasource:{dataSourceId}*");
            await Task.Delay(10);
        }

        // Private helper methods
        private int CalculateQueryWeight(QueryDataSourceDto queryDto)
        {
            var weight = 1;
            
            // Increase weight for complex queries
            if (queryDto.Query.ToLower().Contains("join")) weight += 2;
            if (queryDto.Query.ToLower().Contains("group by")) weight += 1;
            if (queryDto.Query.ToLower().Contains("order by")) weight += 1;
            if (queryDto.MaxRows > 10_000) weight += 2;

            return weight;
        }

        private bool ShouldUseBackgroundProcessing(QueryDataSourceDto queryDto)
        {
            return _performanceSettings.QueryExecution.EnableBackgroundProcessing &&
                   (queryDto.MaxRows > 10_000 || 
                    queryDto.Query.Length > 1000 ||
                    queryDto.Query.ToLower().Contains("join"));
        }

        private QueryPriority DetermineQueryPriority(Guid userId)
        {
            // In real implementation, check user role/permissions
            return QueryPriority.Normal;
        }

        private bool ShouldCacheResult(QueryResultDto result)
        {
            return result.TotalRows <= _performanceSettings.QueryCache.MaxResultRowsToCache;
        }

        private TimeSpan CalculateCacheExpiration(QueryDataSourceDto queryDto)
        {
            // Shorter cache for large result sets, longer for small ones
            if (queryDto.MaxRows > 5000)
                return TimeSpan.FromMinutes(5);
            else if (queryDto.MaxRows > 1000)
                return TimeSpan.FromMinutes(15);
            else
                return _performanceSettings.QueryCache.DefaultExpiration;
        }

        private async Task<QueryResultDto> ExecuteQueryDirectly(Guid dataSourceId, QueryDataSourceDto queryDto)
        {
            // Use connection pool
            using var connection = await _connectionPoolManager.GetConnectionAsync(dataSourceId);
            
            // Simulate query execution
            await Task.Delay(1500);
            
            return CreateMockQueryResult(queryDto.Query);
        }

        // Mock data methods (replace with actual implementations)
        private List<DataSourceDto> GetMockDataSources() => MockDataProvider.GetMockDataSourcesList();
        private ConnectionTestResult CreateMockConnectionResult() => new()
        {
            IsSuccess = true,
            ResponseTime = TimeSpan.FromMilliseconds(new Random().Next(100, 2000)),
            TestedAt = DateTime.Now,
            ConnectionInfo = new Dictionary<string, object> { ["Status"] = "Connected" }
        };
        
        private DataSourceSchemaDto CreateMockSchema() => new()
        {
            Tables = new List<TableInfo>
            {
                new() { Name = "Users", Schema = "dbo", RowCount = 1000, Columns = new List<ColumnInfo>() }
            }
        };

        private QueryResultDto CreateMockQueryResult(string query) => new()
        {
            Data = new List<Dictionary<string, object>>(),
            Columns = new List<ColumnInfo>(),
            TotalRows = 0,
            ExecutionTime = TimeSpan.FromMilliseconds(1500),
            QueryUsed = query
        };

        private string GetTypeName(DataSourceType type) => type.ToString();

        // Cache conversion helpers
        private T ConvertFromCachedResult<T>(QueryResultDto cached) => 
            System.Text.Json.JsonSerializer.Deserialize<T>(cached.Data.First()["data"].ToString() ?? "{}") ?? default!;
        
        private QueryResultDto CreateCacheableResult<T>(T data) => new()
        {
            Data = new List<Dictionary<string, object>>
            {
                new() { ["data"] = System.Text.Json.JsonSerializer.Serialize(data) }
            },
            Columns = new List<ColumnInfo>(),
            TotalRows = 1,
            ExecutionTime = TimeSpan.Zero
        };
    }

    // Helper class to provide mock data
    public static class MockDataProvider
    {
        public static List<DataSourceDto> GetMockDataSourcesList() => new()
        {
            new() {
                Id = Guid.NewGuid(),
                Name = "Main SQL Server",
                Description = "Primary application database",
                Type = DataSourceType.SqlServer,
                TypeName = "SQL Server",
                Host = "localhost",
                Port = 1433,
                DatabaseName = "ErpSystemDb",
                Username = "sa",
                IsActive = true,
                LastConnectionTest = DateTime.Now.AddMinutes(-5),
                LastConnectionSuccess = true,
                CreatedBy = "System Admin",
                CreatedAt = DateTime.Now.AddDays(-30),
                LastUsed = DateTime.Now.AddHours(-2),
                UsageCount = 145,
                Status = ConnectionStatus.Connected
            }
        };
    }
}