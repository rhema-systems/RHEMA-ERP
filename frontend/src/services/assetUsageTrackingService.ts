import axios from 'axios';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

export interface AssetUsageTracking {
  id: string;
  assetId: string;
  assetName?: string;
  assetNumber?: string;
  recordedAt: string;
  mileage?: number;
  mileageUnit?: string;
  operatingHours?: number;
  cycles?: number;
  fuelConsumed?: number;
  fuelUnit?: string;
  dataSource?: string;
  externalReferenceId?: string;
  additionalMetrics?: string;
  notes?: string;
  recordedById?: string;
  recordedByName?: string;
  triggeredMaintenance: boolean;
  isValidated: boolean;
  createdAt: string;
}

export interface CreateAssetUsageTrackingRequest {
  assetId: string;
  recordedAt: string;
  mileage?: number;
  mileageUnit?: string;
  operatingHours?: number;
  cycles?: number;
  fuelConsumed?: number;
  fuelUnit?: string;
  dataSource?: string;
  externalReferenceId?: string;
  additionalMetrics?: string;
  notes?: string;
  isValidated: boolean;
}

export interface BulkUsageImportRequest {
  usageRecords: CreateAssetUsageTrackingRequest[];
  dataSource: string;
  validateAll: boolean;
}

export interface AssetUsageSummary {
  assetId: string;
  assetName: string;
  assetNumber: string;
  currentMileage?: number;
  currentOperatingHours?: number;
  currentCycles?: number;
  averageDailyMileage?: number;
  averageDailyOperatingHours?: number;
  totalFuelConsumed?: number;
  lastRecordedAt?: string;
  totalRecords: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

class AssetUsageTrackingService {
  private getAuthHeaders() {
    const token = localStorage.getItem('authToken');
    return {
      'Content-Type': 'application/json',
      'Authorization': token ? `Bearer ${token}` : ''
    };
  }

  // Create single usage record
  async createUsageRecord(data: CreateAssetUsageTrackingRequest): Promise<AssetUsageTracking> {
    const response = await axios.post(
      `${API_URL}/maintenance/usage-tracking`,
      data,
      { headers: this.getAuthHeaders() }
    );
    return response.data;
  }

  // Bulk create usage records
  async bulkCreateUsageRecords(data: BulkUsageImportRequest): Promise<AssetUsageTracking[]> {
    const response = await axios.post(
      `${API_URL}/maintenance/usage-tracking/bulk`,
      data,
      { headers: this.getAuthHeaders() }
    );
    return response.data;
  }

  // Get usage record by ID
  async getUsageRecordById(id: string): Promise<AssetUsageTracking> {
    const response = await axios.get(
      `${API_URL}/maintenance/usage-tracking/${id}`,
      { headers: this.getAuthHeaders() }
    );
    return response.data;
  }

  // Get usage records for an asset
  async getUsageRecords(
    assetId: string,
    startDate?: string,
    endDate?: string
  ): Promise<AssetUsageTracking[]> {
    const params: any = {};
    if (startDate) params.startDate = startDate;
    if (endDate) params.endDate = endDate;

    const response = await axios.get(
      `${API_URL}/maintenance/usage-tracking/asset/${assetId}`,
      { 
        params,
        headers: this.getAuthHeaders() 
      }
    );
    return response.data;
  }

  // Get paged usage records for an asset
  async getUsageRecordsPaged(
    assetId: string,
    page: number = 1,
    pageSize: number = 20,
    startDate?: string,
    endDate?: string
  ): Promise<PagedResult<AssetUsageTracking>> {
    const params: any = { page, pageSize };
    if (startDate) params.startDate = startDate;
    if (endDate) params.endDate = endDate;

    const response = await axios.get(
      `${API_URL}/maintenance/usage-tracking/asset/${assetId}/paged`,
      { 
        params,
        headers: this.getAuthHeaders() 
      }
    );
    return response.data;
  }

  // Delete usage record
  async deleteUsageRecord(id: string): Promise<void> {
    await axios.delete(
      `${API_URL}/maintenance/usage-tracking/${id}`,
      { headers: this.getAuthHeaders() }
    );
  }

  // Get usage summary for an asset
  async getAssetUsageSummary(assetId: string): Promise<AssetUsageSummary> {
    const response = await axios.get(
      `${API_URL}/maintenance/usage-tracking/asset/${assetId}/summary`,
      { headers: this.getAuthHeaders() }
    );
    return response.data;
  }

  // Get usage summaries for all assets
  async getAllAssetUsageSummaries(): Promise<AssetUsageSummary[]> {
    const response = await axios.get(
      `${API_URL}/maintenance/usage-tracking/summaries`,
      { headers: this.getAuthHeaders() }
    );
    return response.data;
  }

  // Get current mileage
  async getCurrentMileage(assetId: string): Promise<number | null> {
    const response = await axios.get(
      `${API_URL}/maintenance/usage-tracking/asset/${assetId}/mileage`,
      { headers: this.getAuthHeaders() }
    );
    return response.data;
  }

  // Get current operating hours
  async getCurrentOperatingHours(assetId: string): Promise<number | null> {
    const response = await axios.get(
      `${API_URL}/maintenance/usage-tracking/asset/${assetId}/hours`,
      { headers: this.getAuthHeaders() }
    );
    return response.data;
  }

  // Get current cycles
  async getCurrentCycles(assetId: string): Promise<number | null> {
    const response = await axios.get(
      `${API_URL}/maintenance/usage-tracking/asset/${assetId}/cycles`,
      { headers: this.getAuthHeaders() }
    );
    return response.data;
  }

  // Get average daily mileage
  async getAverageDailyMileage(assetId: string, days: number = 30): Promise<number | null> {
    const response = await axios.get(
      `${API_URL}/maintenance/usage-tracking/asset/${assetId}/average-mileage`,
      { 
        params: { days },
        headers: this.getAuthHeaders() 
      }
    );
    return response.data;
  }

  // Get average daily operating hours
  async getAverageDailyHours(assetId: string, days: number = 30): Promise<number | null> {
    const response = await axios.get(
      `${API_URL}/maintenance/usage-tracking/asset/${assetId}/average-hours`,
      { 
        params: { days },
        headers: this.getAuthHeaders() 
      }
    );
    return response.data;
  }

  // Import usage data from external source
  async importUsageData(dataSource: string, externalData: string): Promise<boolean> {
    const response = await axios.post(
      `${API_URL}/maintenance/usage-tracking/import`,
      { dataSource, externalData },
      { headers: this.getAuthHeaders() }
    );
    return response.data;
  }

  // Get unvalidated records
  async getUnvalidatedRecords(): Promise<AssetUsageTracking[]> {
    const response = await axios.get(
      `${API_URL}/maintenance/usage-tracking/unvalidated`,
      { headers: this.getAuthHeaders() }
    );
    return response.data;
  }

  // Validate usage record
  async validateUsageRecord(id: string): Promise<void> {
    await axios.post(
      `${API_URL}/maintenance/usage-tracking/${id}/validate`,
      {},
      { headers: this.getAuthHeaders() }
    );
  }
}

export const assetUsageTrackingService = new AssetUsageTrackingService();
