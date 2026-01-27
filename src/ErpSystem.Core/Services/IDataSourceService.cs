using ErpSystem.Core.DTOs.DataSources;

namespace ErpSystem.Core.Services
{
    public interface IDataSourceService
    {
        Task<List<DataSourceDto>> GetDataSourcesAsync(Guid tenantId);
        Task<DataSourceDto?> GetDataSourceAsync(Guid dataSourceId, Guid tenantId);
        Task<DataSourceDto> CreateDataSourceAsync(CreateDataSourceDto createDto, Guid tenantId, Guid userId);
        Task<DataSourceDto?> UpdateDataSourceAsync(Guid dataSourceId, UpdateDataSourceDto updateDto, Guid tenantId, Guid userId);
        Task<bool> DeleteDataSourceAsync(Guid dataSourceId, Guid tenantId, Guid userId);

        Task<ConnectionTestResult> TestConnectionAsync(TestConnectionDto testDto);
        Task<ConnectionTestResult> TestDataSourceConnectionAsync(Guid dataSourceId, Guid tenantId);

        Task<DataSourceSchemaDto> GetSchemaAsync(Guid dataSourceId, Guid tenantId);
        Task<QueryResultDto> ExecuteQueryAsync(Guid dataSourceId, QueryDataSourceDto queryDto, Guid tenantId);

        Task<List<DataSourceDto>> GetActiveDataSourcesAsync(Guid tenantId);
        Task UpdateUsageStatsAsync(Guid dataSourceId);
    }
}
