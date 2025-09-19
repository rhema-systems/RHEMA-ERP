# Database Optimization Configuration

## 🎯 **Current Optimizations in Place**

Your ERP system already includes several database optimizations:

### **Connection Pooling & Resilience** ✅
```csharp
// Already configured in ServiceCollectionExtensions.cs
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.MigrationsAssembly("ErpSystem.Data");
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    }));
```

### **Health Monitoring** ✅
- Database connectivity health checks
- Memory usage monitoring
- Disk space monitoring

## 🚀 **Enhanced Production Optimizations**

### **1. Advanced Connection Pool Settings**

Update your production `appsettings.Production.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=sqlserver,1433;Database=ErpSystemProd;User Id=sa;Password=${SQL_SA_PASSWORD};MultipleActiveResultSets=true;TrustServerCertificate=true;Max Pool Size=200;Min Pool Size=10;Connection Timeout=30;Command Timeout=120;Pooling=true;Connect Retry Count=3;Connect Retry Interval=10"
  }
}
```

### **2. EF Core Performance Configuration**

Create `DatabaseOptimizationExtensions.cs`:

```csharp
public static class DatabaseOptimizationExtensions
{
    public static IServiceCollection AddOptimizedDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly("ErpSystem.Data");
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
                
                // Performance optimizations
                sqlOptions.CommandTimeout(120);
                
                // Enable batch operations for bulk inserts/updates
                if (configuration.GetValue<bool>("Database:EnableBatching", true))
                {
                    sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
                }
            });

            // EF Core performance settings
            options.EnableSensitiveDataLogging(false); // Disable in production
            options.EnableServiceProviderCaching(true);
            options.EnableThreadSafetyChecks(false); // Only in production
            
            // Query optimization
            options.ConfigureWarnings(warnings =>
            {
                warnings.Ignore(CoreEventId.RowLimitingOperationWithoutOrderByWarning);
                warnings.Throw(RelationalEventId.QueryPossibleUnintendedUseOfEqualsWarning);
            });
        });

        // Add compiled models for faster startup (EF Core 6+)
        services.AddDbContextFactory<ApplicationDbContext>();

        return services;
    }
}
```

### **3. Database Index Optimization**

Add these indexes to your migrations:

```sql
-- High-performance indexes for common queries
CREATE NONCLUSTERED INDEX [IX_User_TenantId_Email] ON [dbo].[AspNetUsers] ([TenantId], [Email]);
CREATE NONCLUSTERED INDEX [IX_User_IsActive_TenantId] ON [dbo].[AspNetUsers] ([IsActive], [TenantId]);
CREATE NONCLUSTERED INDEX [IX_AuditLog_TenantId_CreatedAt] ON [dbo].[AuditLogs] ([TenantId], [CreatedAt] DESC);
CREATE NONCLUSTERED INDEX [IX_SecurityLog_TenantId_CreatedAt] ON [dbo].[SecurityLogs] ([TenantId], [CreatedAt] DESC);

-- Covering indexes for frequently accessed data
CREATE NONCLUSTERED INDEX [IX_Tenant_IsActive_Cover] 
ON [dbo].[Tenants] ([IsActive]) 
INCLUDE ([Name], [DisplayName], [ConnectionString]);

-- Filtered indexes for soft-deleted entities
CREATE NONCLUSTERED INDEX [IX_User_Active_Filtered] 
ON [dbo].[AspNetUsers] ([TenantId], [Email]) 
WHERE [IsDeleted] = 0;
```

### **4. Read Replica Configuration**

For high-traffic scenarios, configure read replicas:

```csharp
public static class ReadReplicaExtensions
{
    public static IServiceCollection AddReadReplicas(this IServiceCollection services, IConfiguration configuration)
    {
        var readConnectionString = configuration.GetConnectionString("ReadReplica");
        
        if (!string.IsNullOrEmpty(readConnectionString))
        {
            // Add read-only context for reporting queries
            services.AddDbContext<ReadOnlyDbContext>(options =>
                options.UseSqlServer(readConnectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
                    sqlOptions.CommandTimeout(60);
                }));

            // Register read service
            services.AddScoped<IReadOnlyService, ReadOnlyService>();
        }

        return services;
    }
}
```

### **5. Query Performance Monitoring**

Add performance logging:

```csharp
public static class QueryPerformanceExtensions
{
    public static IServiceCollection AddQueryPerformance(this IServiceCollection services)
    {
        services.AddScoped<IInterceptor, QueryPerformanceInterceptor>();
        return services;
    }
}

public class QueryPerformanceInterceptor : DbCommandInterceptor
{
    private readonly ILogger<QueryPerformanceInterceptor> _logger;
    
    public QueryPerformanceInterceptor(ILogger<QueryPerformanceInterceptor> logger)
    {
        _logger = logger;
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        var duration = eventData.Duration;
        
        if (duration.TotalMilliseconds > 1000) // Log slow queries (>1 second)
        {
            _logger.LogWarning("Slow query detected: {Duration}ms - {CommandText}", 
                duration.TotalMilliseconds, command.CommandText);
        }

        return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }
}
```

## 📊 **Performance Monitoring**

### **Key Metrics to Track**

1. **Connection Pool Metrics**
   ```csharp
   // Add to health checks
   services.AddHealthChecks()
       .AddCheck("database_connection_pool", () =>
       {
           // Monitor active connections, pool exhaustion
           return HealthCheckResult.Healthy($"Pool: {activeConnections}/{maxPoolSize}");
       });
   ```

2. **Query Performance**
   - Average query execution time
   - Slow query count (>1 second)
   - Connection timeout errors
   - Deadlock detection

3. **Database Resource Usage**
   - CPU utilization
   - Memory usage
   - Disk I/O
   - Connection count

### **Prometheus Metrics Configuration**

```yaml
# monitoring/prometheus.yml
scrape_configs:
  - job_name: 'erp-database'
    static_configs:
      - targets: ['sqlserver:1433']
    metrics_path: /metrics
    scrape_interval: 30s
    
  - job_name: 'erp-api-db-metrics'
    static_configs:
      - targets: ['api-primary:5000', 'api-secondary:5000']
    metrics_path: /metrics
    scrape_interval: 15s
```

## 🔧 **Production Database Configuration**

### **SQL Server Optimization Settings**

```sql
-- Memory configuration (adjust based on available RAM)
EXEC sp_configure 'max server memory', 8192; -- 8GB for SQL Server
RECONFIGURE;

-- Parallelism settings
EXEC sp_configure 'max degree of parallelism', 4;
EXEC sp_configure 'cost threshold for parallelism', 50;
RECONFIGURE;

-- Database-specific settings
ALTER DATABASE ErpSystemProd SET AUTO_CREATE_STATISTICS ON;
ALTER DATABASE ErpSystemProd SET AUTO_UPDATE_STATISTICS ON;
ALTER DATABASE ErpSystemProd SET AUTO_UPDATE_STATISTICS_ASYNC ON;
ALTER DATABASE ErpSystemProd SET PARAMETERIZATION FORCED;

-- Enable query optimization
ALTER DATABASE ErpSystemProd SET LEGACY_CARDINALITY_ESTIMATION OFF;
ALTER DATABASE ErpSystemProd SET QUERY_OPTIMIZER_HOTFIXES ON;
```

### **Backup and Maintenance Strategy**

```sql
-- Create maintenance plan
BACKUP DATABASE ErpSystemProd 
TO DISK = '/var/opt/mssql/backup/ErpSystemProd_Full.bak'
WITH COMPRESSION, CHECKSUM, STATS = 10;

-- Index maintenance (weekly)
DECLARE @sql NVARCHAR(MAX) = '';
SELECT @sql = @sql + 'ALTER INDEX ' + QUOTENAME(i.name) + ' ON ' + QUOTENAME(s.name) + '.' + QUOTENAME(t.name) + ' REBUILD;' + CHAR(13)
FROM sys.indexes i
JOIN sys.tables t ON i.object_id = t.object_id
JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE i.index_id > 0 AND i.is_disabled = 0;

EXEC sp_executesql @sql;
```

## 🚀 **Docker Compose Database Optimizations**

Update your `docker-compose.monolith.yml`:

```yaml
sqlserver:
  image: mcr.microsoft.com/mssql/server:2022-latest
  container_name: erp-sqlserver
  environment:
    - ACCEPT_EULA=Y
    - SA_PASSWORD=${SQL_SA_PASSWORD}
    - MSSQL_PID=Express
    # Performance optimizations
    - MSSQL_MEMORY_LIMIT_MB=6144  # 6GB memory limit
    - MSSQL_CPU_COUNT=4           # 4 CPU cores
  ports:
    - "1433:1433"
  volumes:
    - sqlserver_data:/var/opt/mssql/data
    - sqlserver_log:/var/opt/mssql/log
    - sqlserver_backup:/var/opt/mssql/backup
    - ./sql-scripts:/var/opt/mssql/scripts  # Custom SQL scripts
  command: >
    sh -c "
    /opt/mssql/bin/sqlservr &
    sleep 30 &&
    /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P ${SQL_SA_PASSWORD} -i /var/opt/mssql/scripts/optimization.sql &&
    wait
    "
  restart: unless-stopped
  networks:
    - erp-network
  healthcheck:
    test: ["CMD-SHELL", "/opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P ${SQL_SA_PASSWORD} -Q 'SELECT 1' || exit 1"]
    interval: 30s
    timeout: 10s
    retries: 5
    start_period: 60s
  deploy:
    resources:
      limits:
        memory: 6G
        cpus: '4.0'
      reservations:
        memory: 4G
        cpus: '2.0'
```

## 📈 **Performance Testing**

### **Load Testing Script**

```bash
#!/bin/bash
# test-database-performance.sh

echo "Testing database performance..."

# Connection pool stress test
for i in {1..100}; do
  curl -f "http://localhost:5000/api/health/database" &
done
wait

# Query performance test
curl -X POST "http://localhost:5000/api/user" \
  -H "Content-Type: application/json" \
  -d '{"username":"test'$RANDOM'","email":"test'$RANDOM'@example.com","tenantId":"'$TENANT_ID'"}'

# Monitor metrics
curl -s "http://localhost:5000/metrics" | grep -E "(database|connection|query)"
```

## ✅ **Implementation Checklist**

- [x] **Connection Pooling**: Already configured with retry logic
- [x] **Health Monitoring**: Database, memory, and disk checks in place  
- [x] **Distributed Caching**: Redis integration for session state
- [ ] **Enhanced Connection Pool Settings**: Update production connection string
- [ ] **Database Index Optimization**: Add performance indexes via migration
- [ ] **Query Performance Monitoring**: Add slow query logging interceptor
- [ ] **Read Replica Configuration**: Optional for high-traffic scenarios
- [ ] **SQL Server Optimization**: Apply production database settings
- [ ] **Performance Metrics**: Add Prometheus database metrics
- [ ] **Load Testing**: Validate under production load

Your modular monolith is already well-optimized for production use. These enhancements will provide enterprise-level database performance and monitoring capabilities.