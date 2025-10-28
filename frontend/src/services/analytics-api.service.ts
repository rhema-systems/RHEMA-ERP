import { apiService } from './api.service';

export interface AnalyticsAsset {
  id: string;
  name: string;
  type: string;
  location: string;
  department: string;
}

export interface PerformanceDataPoint {
  date: string;
  oee: number;
  availability: number;
  performance: number;
  quality: number;
}

export interface ReliabilityDataPoint {
  assetName: string;
  mtbf: number;
  mttr: number;
  availability: number;
}

export interface CostDataPoint {
  category: string;
  cost: number;
  percentage: number;
}

export interface EnergyDataPoint {
  date: string;
  consumption: number;
  efficiency: number;
  cost: number;
}

export interface AnalyticsData {
  assets: AnalyticsAsset[];
  performanceData: PerformanceDataPoint[];
  reliabilityData: ReliabilityDataPoint[];
  costData: CostDataPoint[];
  energyData: EnergyDataPoint[];
}

class AnalyticsApiService {
  async getAnalyticsData(): Promise<AnalyticsData> {
    try {
      const response = await apiService.request<AnalyticsData>('/maintenance/analytics/data');
      console.log('✅ Analytics API service received data:', response);
      return response;
    } catch (error) {
      console.error('❌ Analytics API service error:', error);
      throw error;
    }
  }

  // Mock data fallback (same as in the analytics page)
  getMockAnalyticsData(): AnalyticsData {
    return {
      assets: [
        {
          id: '1',
          name: 'HVAC Unit 1',
          type: 'HVAC',
          location: 'Main Building',
          department: 'Facilities'
        },
        {
          id: '2',
          name: 'Elevator 1',
          type: 'Elevator',
          location: 'Building A',
          department: 'Facilities'
        },
        {
          id: '3',
          name: 'Emergency Generator',
          type: 'Generator',
          location: 'Basement',
          department: 'Maintenance'
        }
      ],
      performanceData: [
        {
          date: '2024-10-01',
          oee: 85.2,
          availability: 92.1,
          performance: 95.8,
          quality: 96.5
        },
        {
          date: '2024-10-02',
          oee: 88.7,
          availability: 94.3,
          performance: 96.2,
          quality: 97.8
        },
        {
          date: '2024-10-03',
          oee: 82.4,
          availability: 89.7,
          performance: 93.1,
          quality: 95.2
        },
        {
          date: '2024-10-04',
          oee: 90.1,
          availability: 96.2,
          performance: 97.1,
          quality: 96.4
        },
        {
          date: '2024-10-05',
          oee: 87.3,
          availability: 93.8,
          performance: 95.7,
          quality: 97.1
        }
      ],
      reliabilityData: [
        {
          assetName: 'HVAC Unit 1',
          mtbf: 2160, // Mean Time Between Failures (hours)
          mttr: 4.2,  // Mean Time To Repair (hours)
          availability: 94.2
        },
        {
          assetName: 'Elevator 1',
          mtbf: 4320,
          mttr: 2.8,
          availability: 96.8
        },
        {
          assetName: 'Emergency Generator',
          mtbf: 8760,
          mttr: 6.5,
          availability: 99.1
        }
      ],
      costData: [
        {
          category: 'Labor',
          cost: 45000,
          percentage: 45
        },
        {
          category: 'Parts & Materials',
          cost: 32000,
          percentage: 32
        },
        {
          category: 'External Services',
          cost: 15000,
          percentage: 15
        },
        {
          category: 'Equipment',
          cost: 8000,
          percentage: 8
        }
      ],
      energyData: [
        {
          date: '2024-10-01',
          consumption: 1250,
          efficiency: 87.3,
          cost: 312.50
        },
        {
          date: '2024-10-02',
          consumption: 1180,
          efficiency: 89.1,
          cost: 295.00
        },
        {
          date: '2024-10-03',
          consumption: 1320,
          efficiency: 85.7,
          cost: 330.00
        },
        {
          date: '2024-10-04',
          consumption: 1095,
          efficiency: 91.2,
          cost: 273.75
        },
        {
          date: '2024-10-05',
          consumption: 1210,
          efficiency: 88.5,
          cost: 302.50
        }
      ]
    };
  }
}

export const analyticsApiService = new AnalyticsApiService();
export default analyticsApiService;