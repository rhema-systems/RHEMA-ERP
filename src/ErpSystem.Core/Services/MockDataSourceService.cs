using ErpSystem.Core.DTOs.DataSources;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Services
{
    public class MockDataSourceService : IDataSourceService
    {
        private static readonly List<DataSourceDto> _mockDataSources = new()
        {
            new DataSourceDto
            {
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
            },
            new DataSourceDto
            {
                Id = Guid.NewGuid(),
                Name = "Analytics MySQL",
                Description = "MySQL database for analytics and reporting",
                Type = DataSourceType.MySql,
                TypeName = "MySQL",
                Host = "analytics.company.com",
                Port = 3306,
                DatabaseName = "analytics_db",
                Username = "reporting_user",
                IsActive = true,
                LastConnectionTest = DateTime.Now.AddMinutes(-15),
                LastConnectionSuccess = true,
                CreatedBy = "Data Team",
                CreatedAt = DateTime.Now.AddDays(-15),
                LastUsed = DateTime.Now.AddHours(-4),
                UsageCount = 67,
                Status = ConnectionStatus.Connected
            },
            new DataSourceDto
            {
                Id = Guid.NewGuid(),
                Name = "Customer API",
                Description = "REST API for customer data integration",
                Type = DataSourceType.RestApi,
                TypeName = "REST API",
                Host = "api.crm.company.com",
                Port = 443,
                Username = "api_user",
                IsActive = true,
                LastConnectionTest = DateTime.Now.AddHours(-1),
                LastConnectionSuccess = true,
                CreatedBy = "Integration Team",
                CreatedAt = DateTime.Now.AddDays(-10),
                UsageCount = 23,
                Status = ConnectionStatus.Connected,
                AdditionalSettings = new Dictionary<string, object>
                {
                    ["ApiKey"] = "****-****-****-****",
                    ["BaseUrl"] = "https://api.crm.company.com/v1",
                    ["Timeout"] = 30
                }
            },
            new DataSourceDto
            {
                Id = Guid.NewGuid(),
                Name = "Sales PostgreSQL",
                Description = "PostgreSQL database for sales data",
                Type = DataSourceType.PostgreSQL,
                TypeName = "PostgreSQL",
                Host = "sales-db.company.com",
                Port = 5432,
                DatabaseName = "sales_analytics",
                Username = "readonly_user",
                IsActive = false,
                LastConnectionTest = DateTime.Now.AddDays(-2),
                LastConnectionSuccess = false,
                LastConnectionError = "Connection timeout",
                CreatedBy = "Sales Team",
                CreatedAt = DateTime.Now.AddDays(-5),
                UsageCount = 8,
                Status = ConnectionStatus.Error
            },
            new DataSourceDto
            {
                Id = Guid.NewGuid(),
                Name = "Excel Reports",
                Description = "Excel files for monthly reports",
                Type = DataSourceType.Excel,
                TypeName = "Excel Files",
                IsActive = true,
                CreatedBy = "Finance Team",
                CreatedAt = DateTime.Now.AddDays(-20),
                UsageCount = 34,
                Status = ConnectionStatus.Unknown,
                AdditionalSettings = new Dictionary<string, object>
                {
                    ["FilePath"] = "\\\\server\\shared\\reports\\",
                    ["FilePattern"] = "*.xlsx"
                }
            }
        };

        public async Task<List<DataSourceDto>> GetDataSourcesAsync(Guid tenantId)
        {
            await Task.Delay(100);
            return _mockDataSources.ToList();
        }

        public async Task<DataSourceDto?> GetDataSourceAsync(Guid dataSourceId, Guid tenantId)
        {
            await Task.Delay(50);
            return _mockDataSources.FirstOrDefault(ds => ds.Id == dataSourceId);
        }

        public async Task<DataSourceDto> CreateDataSourceAsync(CreateDataSourceDto createDto, Guid tenantId, Guid userId)
        {
            await Task.Delay(200);

            var newDataSource = new DataSourceDto
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

            _mockDataSources.Add(newDataSource);
            return newDataSource;
        }

        public async Task<DataSourceDto?> UpdateDataSourceAsync(Guid dataSourceId, UpdateDataSourceDto updateDto, Guid tenantId, Guid userId)
        {
            await Task.Delay(150);

            var dataSource = _mockDataSources.FirstOrDefault(ds => ds.Id == dataSourceId);
            if (dataSource == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(updateDto.Name))
            {
                dataSource.Name = updateDto.Name;
            }

            if (updateDto.Description != null)
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

            if (updateDto.AdditionalSettings != null)
            {
                dataSource.AdditionalSettings = updateDto.AdditionalSettings;
            }

            if (updateDto.IsActive.HasValue)
            {
                dataSource.IsActive = updateDto.IsActive.Value;
            }

            return dataSource;
        }

        public async Task<bool> DeleteDataSourceAsync(Guid dataSourceId, Guid tenantId, Guid userId)
        {
            await Task.Delay(100);
            var dataSource = _mockDataSources.FirstOrDefault(ds => ds.Id == dataSourceId);
            if (dataSource == null)
            {
                return false;
            }

            _mockDataSources.Remove(dataSource);
            return true;
        }

        public async Task<ConnectionTestResult> TestConnectionAsync(TestConnectionDto testDto)
        {
            await Task.Delay(2000); // Simulate connection test time

            var random = new Random();
            var isSuccess = random.NextDouble() > 0.2; // 80% success rate

            return new ConnectionTestResult
            {
                IsSuccess = isSuccess,
                ErrorMessage = isSuccess ? null : "Failed to connect to the data source. Please check your connection settings.",
                ResponseTime = TimeSpan.FromMilliseconds(random.Next(100, 3000)),
                TestedAt = DateTime.Now,
                ConnectionInfo = isSuccess ? new Dictionary<string, object>
                {
                    ["ServerVersion"] = GetMockServerVersion(testDto.Type),
                    ["DatabaseName"] = testDto.DatabaseName ?? "N/A",
                    ["Connected"] = true
                } : null
            };
        }

        public async Task<ConnectionTestResult> TestDataSourceConnectionAsync(Guid dataSourceId, Guid tenantId)
        {
            await Task.Delay(1500);

            var dataSource = _mockDataSources.FirstOrDefault(ds => ds.Id == dataSourceId);
            if (dataSource == null)
            {
                return new ConnectionTestResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Data source not found",
                    ResponseTime = TimeSpan.Zero,
                    TestedAt = DateTime.Now
                };
            }

            var random = new Random();
            var isSuccess = random.NextDouble() > 0.15; // 85% success rate for existing sources

            var result = new ConnectionTestResult
            {
                IsSuccess = isSuccess,
                ErrorMessage = isSuccess ? null : "Connection failed. Please check the data source configuration.",
                ResponseTime = TimeSpan.FromMilliseconds(random.Next(100, 2500)),
                TestedAt = DateTime.Now,
                ConnectionInfo = isSuccess ? new Dictionary<string, object>
                {
                    ["ServerVersion"] = GetMockServerVersion(dataSource.Type),
                    ["DatabaseName"] = dataSource.DatabaseName ?? "N/A",
                    ["Host"] = dataSource.Host ?? "N/A"
                } : null
            };

            // Update the data source status
            dataSource.LastConnectionTest = result.TestedAt;
            dataSource.LastConnectionSuccess = result.IsSuccess;
            dataSource.LastConnectionError = result.ErrorMessage;
            dataSource.Status = result.IsSuccess ? ConnectionStatus.Connected : ConnectionStatus.Error;

            return result;
        }

        public async Task<DataSourceSchemaDto> GetSchemaAsync(Guid dataSourceId, Guid tenantId)
        {
            await Task.Delay(1000);

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
                            new ColumnInfo { Name = "Id", DataType = "uniqueidentifier", IsPrimaryKey = true },
                            new ColumnInfo { Name = "FirstName", DataType = "nvarchar", MaxLength = 50 },
                            new ColumnInfo { Name = "LastName", DataType = "nvarchar", MaxLength = 50 },
                            new ColumnInfo { Name = "Email", DataType = "nvarchar", MaxLength = 256 },
                            new ColumnInfo { Name = "CreatedAt", DataType = "datetime2" }
                        }
                    },
                    new TableInfo
                    {
                        Name = "Orders",
                        Schema = "dbo",
                        RowCount = 5680,
                        Columns = new List<ColumnInfo>
                        {
                            new ColumnInfo { Name = "Id", DataType = "uniqueidentifier", IsPrimaryKey = true },
                            new ColumnInfo { Name = "UserId", DataType = "uniqueidentifier" },
                            new ColumnInfo { Name = "OrderDate", DataType = "datetime2" },
                            new ColumnInfo { Name = "TotalAmount", DataType = "decimal", Precision = 18, Scale = 2 },
                            new ColumnInfo { Name = "Status", DataType = "nvarchar", MaxLength = 20 }
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
                            new ColumnInfo { Name = "UserId", DataType = "uniqueidentifier" },
                            new ColumnInfo { Name = "UserName", DataType = "nvarchar", MaxLength = 100 },
                            new ColumnInfo { Name = "OrderCount", DataType = "int" },
                            new ColumnInfo { Name = "TotalSpent", DataType = "decimal", Precision = 18, Scale = 2 }
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
                            new ParameterInfo { Name = "@UserId", DataType = "uniqueidentifier" },
                            new ParameterInfo { Name = "@StartDate", DataType = "datetime2", DefaultValue = null },
                            new ParameterInfo { Name = "@EndDate", DataType = "datetime2", DefaultValue = null }
                        }
                    }
                }
            };
        }

        public async Task<QueryResultDto> ExecuteQueryAsync(Guid dataSourceId, QueryDataSourceDto queryDto, Guid tenantId)
        {
            await Task.Delay(1500); // Simulate query execution time

            // Mock data based on the query
            var mockData = new List<Dictionary<string, object>>();
            var columns = new List<ColumnInfo>();

            if (queryDto.Query.Contains("users", StringComparison.CurrentCultureIgnoreCase))
            {
                columns = new List<ColumnInfo>
                {
                    new ColumnInfo { Name = "Id", DataType = "uniqueidentifier" },
                    new ColumnInfo { Name = "FirstName", DataType = "nvarchar" },
                    new ColumnInfo { Name = "LastName", DataType = "nvarchar" },
                    new ColumnInfo { Name = "Email", DataType = "nvarchar" },
                    new ColumnInfo { Name = "CreatedAt", DataType = "datetime2" }
                };

                for (int i = 1; i <= Math.Min(queryDto.MaxRows, 10); i++)
                {
                    mockData.Add(new Dictionary<string, object>
                    {
                        ["Id"] = Guid.NewGuid(),
                        ["FirstName"] = $"User{i}",
                        ["LastName"] = $"LastName{i}",
                        ["Email"] = $"user{i}@example.com",
                        ["CreatedAt"] = DateTime.Now.AddDays(-i * 10)
                    });
                }
            }
            else if (queryDto.Query.Contains("orders", StringComparison.CurrentCultureIgnoreCase))
            {
                columns = new List<ColumnInfo>
                {
                    new ColumnInfo { Name = "Id", DataType = "uniqueidentifier" },
                    new ColumnInfo { Name = "OrderDate", DataType = "datetime2" },
                    new ColumnInfo { Name = "TotalAmount", DataType = "decimal" },
                    new ColumnInfo { Name = "Status", DataType = "nvarchar" }
                };

                var statuses = new[] { "Pending", "Completed", "Cancelled" };
                var random = new Random();

                for (int i = 1; i <= Math.Min(queryDto.MaxRows, 10); i++)
                {
                    mockData.Add(new Dictionary<string, object>
                    {
                        ["Id"] = Guid.NewGuid(),
                        ["OrderDate"] = DateTime.Now.AddDays(-random.Next(1, 30)),
                        ["TotalAmount"] = Math.Round(random.NextDouble() * 1000, 2),
                        ["Status"] = statuses[random.Next(statuses.Length)]
                    });
                }
            }
            else
            {
                // Generic result for unknown queries
                columns = new List<ColumnInfo>
                {
                    new ColumnInfo { Name = "Column1", DataType = "nvarchar" },
                    new ColumnInfo { Name = "Column2", DataType = "int" },
                    new ColumnInfo { Name = "Column3", DataType = "datetime2" }
                };

                for (int i = 1; i <= Math.Min(queryDto.MaxRows, 5); i++)
                {
                    mockData.Add(new Dictionary<string, object>
                    {
                        ["Column1"] = $"Value{i}",
                        ["Column2"] = i * 10,
                        ["Column3"] = DateTime.Now.AddDays(-i)
                    });
                }
            }

            return new QueryResultDto
            {
                Data = mockData,
                Columns = columns,
                TotalRows = mockData.Count,
                ExecutionTime = TimeSpan.FromMilliseconds(new Random().Next(100, 2000)),
                QueryUsed = queryDto.Query
            };
        }

        public async Task<List<DataSourceDto>> GetActiveDataSourcesAsync(Guid tenantId)
        {
            await Task.Delay(50);
            return _mockDataSources.Where(ds => ds.IsActive).ToList();
        }

        public async Task UpdateUsageStatsAsync(Guid dataSourceId)
        {
            await Task.Delay(10);
            var dataSource = _mockDataSources.FirstOrDefault(ds => ds.Id == dataSourceId);
            if (dataSource != null)
            {
                dataSource.UsageCount++;
                dataSource.LastUsed = DateTime.Now;
            }
        }

        private static string GetTypeName(DataSourceType type)
        {
            return type switch
            {
                DataSourceType.SqlServer => "SQL Server",
                DataSourceType.MySql => "MySQL",
                DataSourceType.PostgreSQL => "PostgreSQL",
                DataSourceType.Oracle => "Oracle",
                DataSourceType.SQLite => "SQLite",
                DataSourceType.MongoDB => "MongoDB",
                DataSourceType.RestApi => "REST API",
                DataSourceType.GraphQL => "GraphQL",
                DataSourceType.OData => "OData",
                DataSourceType.Excel => "Excel Files",
                DataSourceType.Csv => "CSV Files",
                DataSourceType.Json => "JSON Files",
                DataSourceType.Xml => "XML Files",
                DataSourceType.Redis => "Redis",
                DataSourceType.ElasticSearch => "Elasticsearch",
                DataSourceType.Azure => "Azure Services",
                DataSourceType.AWS => "AWS Services",
                DataSourceType.GoogleCloud => "Google Cloud",
                _ => type.ToString()
            };
        }

        private static string GetMockServerVersion(DataSourceType type)
        {
            return type switch
            {
                DataSourceType.SqlServer => "Microsoft SQL Server 2022",
                DataSourceType.MySql => "MySQL 8.0.32",
                DataSourceType.PostgreSQL => "PostgreSQL 15.2",
                DataSourceType.Oracle => "Oracle Database 19c",
                DataSourceType.MongoDB => "MongoDB 6.0.4",
                DataSourceType.Redis => "Redis 7.0.8",
                DataSourceType.ElasticSearch => "Elasticsearch 8.6.2",
                _ => "Unknown Version"
            };
        }
    }
}
