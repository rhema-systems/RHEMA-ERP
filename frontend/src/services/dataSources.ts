import { apiService } from './api.service';

export enum DataSourceType {
  SqlServer = 1,
  MySql = 2,
  PostgreSQL = 3,
  Oracle = 4,
  SQLite = 5,
  MongoDB = 6,
  RestApi = 7,
  GraphQL = 8,
  OData = 9,
  Excel = 10,
  Csv = 11,
  Json = 12,
  Xml = 13,
  Redis = 14,
  ElasticSearch = 15,
  Azure = 16,
  AWS = 17,
  GoogleCloud = 18
}

export enum ConnectionStatus {
  Unknown,
  Connected,
  Disconnected,
  Error,
  Testing
}

export interface DataSource {
  id: string;
  name: string;
  description?: string;
  type: DataSourceType;
  typeName: string;
  host?: string;
  port?: number;
  databaseName?: string;
  username?: string;
  additionalSettings?: Record<string, any>;
  isActive: boolean;
  lastConnectionTest?: string;
  lastConnectionSuccess?: boolean;
  lastConnectionError?: string;
  createdBy: string;
  createdAt: string;
  lastUsed?: string;
  usageCount: number;
  status: ConnectionStatus;
}

export interface CreateDataSourceDto {
  name: string;
  description?: string;
  type: DataSourceType;
  host?: string;
  port?: number;
  databaseName?: string;
  username?: string;
  password?: string;
  additionalSettings?: Record<string, any>;
  isActive?: boolean;
}

export interface UpdateDataSourceDto {
  name?: string;
  description?: string;
  host?: string;
  port?: number;
  databaseName?: string;
  username?: string;
  password?: string;
  additionalSettings?: Record<string, any>;
  isActive?: boolean;
}

export interface TestConnectionDto {
  type: DataSourceType;
  host?: string;
  port?: number;
  databaseName?: string;
  username?: string;
  password?: string;
  additionalSettings?: Record<string, any>;
}

export interface ConnectionTestResult {
  isSuccess: boolean;
  errorMessage?: string;
  responseTime: string;
  connectionInfo?: Record<string, any>;
  testedAt: string;
}

export interface ColumnInfo {
  name: string;
  dataType: string;
  isNullable: boolean;
  isPrimaryKey: boolean;
  maxLength?: number;
  precision?: number;
  scale?: number;
}

export interface TableInfo {
  name: string;
  schema?: string;
  columns: ColumnInfo[];
  rowCount: number;
}

export interface ViewInfo {
  name: string;
  schema?: string;
  columns: ColumnInfo[];
}

export interface ParameterInfo {
  name: string;
  dataType: string;
  isOutput: boolean;
  defaultValue?: any;
}

export interface StoredProcedureInfo {
  name: string;
  schema?: string;
  parameters: ParameterInfo[];
}

export interface DataSourceSchema {
  tables: TableInfo[];
  views: ViewInfo[];
  storedProcedures: StoredProcedureInfo[];
}

export interface QueryDataSourceDto {
  query: string;
  parameters?: Record<string, any>;
  maxRows?: number;
}

export interface QueryResult {
  data: Record<string, any>[];
  columns: ColumnInfo[];
  totalRows: number;
  executionTime: string;
  queryUsed?: string;
}

class DataSourcesService {

  async getDataSources(): Promise<DataSource[]> {
    try {
      return await apiService.request<DataSource[]>('/datasources');
    } catch (error) {
      console.error('Error fetching data sources:', error);
      throw error;
    }
  }

  async getActiveDataSources(): Promise<DataSource[]> {
    try {
      return await apiService.request<DataSource[]>('/datasources/active');
    } catch (error) {
      console.error('Error fetching active data sources:', error);
      throw error;
    }
  }

  async getDataSource(dataSourceId: string): Promise<DataSource> {
    try {
      return await apiService.request<DataSource>(`/datasources/${dataSourceId}`);
    } catch (error) {
      console.error('Error fetching data source:', error);
      throw error;
    }
  }

  async createDataSource(createDto: CreateDataSourceDto): Promise<DataSource> {
    try {
      return await apiService.request<DataSource>('/datasources', {
        method: 'POST',
        body: JSON.stringify(createDto),
      });
    } catch (error) {
      console.error('Error creating data source:', error);
      throw error;
    }
  }

  async updateDataSource(dataSourceId: string, updateDto: UpdateDataSourceDto): Promise<DataSource> {
    try {
      return await apiService.request<DataSource>(`/datasources/${dataSourceId}`, {
        method: 'PUT',
        body: JSON.stringify(updateDto),
      });
    } catch (error) {
      console.error('Error updating data source:', error);
      throw error;
    }
  }

  async deleteDataSource(dataSourceId: string): Promise<void> {
    try {
      await apiService.request<void>(`/datasources/${dataSourceId}`, {
        method: 'DELETE',
      });
    } catch (error) {
      console.error('Error deleting data source:', error);
      throw error;
    }
  }

  async testConnection(testDto: TestConnectionDto): Promise<ConnectionTestResult> {
    try {
      return await apiService.request<ConnectionTestResult>('/datasources/test-connection', {
        method: 'POST',
        body: JSON.stringify(testDto),
      });
    } catch (error) {
      console.error('Error testing connection:', error);
      throw error;
    }
  }

  async testDataSourceConnection(dataSourceId: string): Promise<ConnectionTestResult> {
    try {
      return await apiService.request<ConnectionTestResult>(`/datasources/${dataSourceId}/test-connection`, {
        method: 'POST',
        body: JSON.stringify({}),
      });
    } catch (error) {
      console.error('Error testing data source connection:', error);
      throw error;
    }
  }

  async getSchema(dataSourceId: string): Promise<DataSourceSchema> {
    try {
      return await apiService.request<DataSourceSchema>(`/datasources/${dataSourceId}/schema`);
    } catch (error) {
      console.error('Error fetching data source schema:', error);
      throw error;
    }
  }

  async executeQuery(dataSourceId: string, queryDto: QueryDataSourceDto): Promise<QueryResult> {
    try {
      return await apiService.request<QueryResult>(`/datasources/${dataSourceId}/query`, {
        method: 'POST',
        body: JSON.stringify(queryDto),
      });
    } catch (error) {
      console.error('Error executing query:', error);
      throw error;
    }
  }

  getDataSourceTypeOptions() {
    return [
      { value: DataSourceType.SqlServer, label: 'SQL Server', icon: '🗄️' },
      { value: DataSourceType.MySql, label: 'MySQL', icon: '🐬' },
      { value: DataSourceType.PostgreSQL, label: 'PostgreSQL', icon: '🐘' },
      { value: DataSourceType.Oracle, label: 'Oracle', icon: '🔶' },
      { value: DataSourceType.SQLite, label: 'SQLite', icon: '💾' },
      { value: DataSourceType.MongoDB, label: 'MongoDB', icon: '🍃' },
      { value: DataSourceType.RestApi, label: 'REST API', icon: '🌐' },
      { value: DataSourceType.GraphQL, label: 'GraphQL', icon: '📊' },
      { value: DataSourceType.OData, label: 'OData', icon: '🔗' },
      { value: DataSourceType.Excel, label: 'Excel Files', icon: '📊' },
      { value: DataSourceType.Csv, label: 'CSV Files', icon: '📄' },
      { value: DataSourceType.Json, label: 'JSON Files', icon: '{ }' },
      { value: DataSourceType.Xml, label: 'XML Files', icon: '📝' },
      { value: DataSourceType.Redis, label: 'Redis', icon: '🔴' },
      { value: DataSourceType.ElasticSearch, label: 'Elasticsearch', icon: '🔍' },
      { value: DataSourceType.Azure, label: 'Azure Services', icon: '☁️' },
      { value: DataSourceType.AWS, label: 'AWS Services', icon: '🟠' },
      { value: DataSourceType.GoogleCloud, label: 'Google Cloud', icon: '🌥️' }
    ];
  }

  getConnectionStatusColor(status: ConnectionStatus): string {
    switch (status) {
      case ConnectionStatus.Connected:
        return 'text-green-600';
      case ConnectionStatus.Error:
        return 'text-red-600';
      case ConnectionStatus.Testing:
        return 'text-blue-600';
      case ConnectionStatus.Disconnected:
        return 'text-gray-600';
      default:
        return 'text-gray-400';
    }
  }

  getConnectionStatusLabel(status: ConnectionStatus): string {
    switch (status) {
      case ConnectionStatus.Connected:
        return 'Connected';
      case ConnectionStatus.Error:
        return 'Error';
      case ConnectionStatus.Testing:
        return 'Testing...';
      case ConnectionStatus.Disconnected:
        return 'Disconnected';
      default:
        return 'Unknown';
    }
  }
}

export const dataSourcesService = new DataSourcesService();