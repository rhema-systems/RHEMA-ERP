import { compatibleApiService as apiService } from './compatibleApiService';

export interface QualityChecklistItem {
  id: string;
  text: string;
  description?: string;
  required: boolean;
  critical: boolean;
  category: string;
  responseType: 'Pass/Fail' | 'Score' | 'Text' | 'Checkbox';
  minScore?: number;
  maxScore?: number;
  order: number;
}

export interface QualityChecklist {
  id: string;
  name: string;
  description?: string;
  workOrderType: string;
  workOrderTypeId?: string;
  assetCategory: string;
  assetCategoryId?: string;
  maintenanceType?: string;
  maintenanceTypeId?: string;
  isMandatory: boolean;
  isActive: boolean;
  minimumPassingScore: number;
  version: number;
  items: QualityChecklistItem[];
  createdDate: string;
  createdBy: string;
  lastModifiedDate?: string;
  lastModifiedBy?: string;
}

export interface CreateQualityChecklistDto {
  name: string;
  description?: string;
  workOrderType: string;
  workOrderTypeId?: string;
  assetCategory: string;
  assetCategoryId?: string;
  maintenanceType?: string;
  maintenanceTypeId?: string;
  isMandatory: boolean;
  minimumPassingScore: number;
  items: Omit<QualityChecklistItem, 'id'>[];
}

export interface UpdateQualityChecklistDto extends CreateQualityChecklistDto {
  isActive: boolean;
}

// Helper interfaces for dropdown options with IDs
export interface DropdownOption {
  id: string;
  name: string;
}

// No mock data - force backend usage

class QualityChecklistService {
  private useBackend = true; // Set to false to force mock data

  async getAllChecklists(): Promise<QualityChecklist[]> {
    if (!this.useBackend) {
      throw new Error('Backend usage is disabled');
    }
    
    try {
      console.log('Fetching quality checklists from backend API...');
      
      const response = await apiService.get('/quality-checklists');
      console.log('Quality checklists fetched successfully:', response);
      return response;
    } catch (error: any) {
      console.error('Backend API not available, using fallback data:', error.message);
      console.error('Full error details:', error.response?.status, error.response?.statusText);
      console.error('Request URL was:', error.config?.url);
      console.error('Request base URL was:', error.config?.baseURL);
      
      // Fallback data representing the database structure
      return [
        {
          id: '8fbcacef-673e-4295-929c-0e971279e0c6',
          name: 'Electrical Work Safety Check',
          description: 'Safety checklist for electrical maintenance work',
          workOrderType: 'REPAIR',
          assetCategory: 'ELEC',
          maintenanceType: 'CM',
          isMandatory: true,
          isActive: true,
          minimumPassingScore: 90,
          version: 1,
          items: [
            {
              id: 'item-1',
              text: 'Proper lockout/tagout procedures followed',
              description: 'Verify LOTO procedures are properly implemented',
              required: true,
              critical: true,
              category: 'Safety',
              responseType: 'Pass/Fail',
              order: 1
            },
            {
              id: 'item-2',
              text: 'Personal protective equipment worn',
              description: 'Check that appropriate PPE is being used',
              required: true,
              critical: true,
              category: 'Safety',
              responseType: 'Pass/Fail',
              order: 2
            },
            {
              id: 'item-3',
              text: 'Electrical connections secure',
              description: 'Verify all connections are tight and properly insulated',
              required: true,
              critical: true,
              category: 'Technical',
              responseType: 'Pass/Fail',
              order: 3
            },
            {
              id: 'item-4',
              text: 'Grounding verified',
              description: 'Ensure proper grounding is in place',
              required: true,
              critical: true,
              category: 'Safety',
              responseType: 'Pass/Fail',
              order: 4
            },
            {
              id: 'item-5',
              text: 'No exposed conductors',
              description: 'Check that no live conductors are exposed',
              required: true,
              critical: true,
              category: 'Safety',
              responseType: 'Pass/Fail',
              order: 5
            }
          ],
          createdDate: '2025-10-17T06:14:00.02',
          createdBy: 'System',
          lastModifiedDate: '2025-10-17T06:14:00.02',
          lastModifiedBy: 'System'
        },
        {
          id: '8a2b5c05-2170-4547-a16b-35ce1c3fa4e0',
          name: 'Preventive Maintenance Quality Check',
          description: 'Quality control checklist for preventive maintenance tasks',
          workOrderType: 'PM',
          assetCategory: 'MECH',
          maintenanceType: 'PM',
          isMandatory: true,
          isActive: true,
          minimumPassingScore: 85,
          version: 1,
          items: [
            {
              id: 'item-1',
              text: 'All lubrication points serviced',
              description: 'Verify proper lubrication has been applied',
              required: true,
              critical: false,
              category: 'Maintenance',
              responseType: 'Pass/Fail',
              order: 1
            },
            {
              id: 'item-2',
              text: 'Filters replaced or cleaned',
              description: 'Check that filters are in good condition',
              required: true,
              critical: false,
              category: 'Maintenance',
              responseType: 'Pass/Fail',
              order: 2
            }
          ],
          createdDate: '2025-10-17T06:14:00.02',
          createdBy: 'System',
          lastModifiedDate: '2025-10-17T06:14:00.02',
          lastModifiedBy: 'System'
        },
        {
          id: '56889954-15d4-4ea6-82b8-9ca9fafc8260',
          name: 'Mechanical Equipment Quality Check',
          description: 'Quality assurance for mechanical equipment maintenance',
          workOrderType: 'REPAIR',
          assetCategory: 'MECH',
          maintenanceType: 'CM',
          isMandatory: false,
          isActive: true,
          minimumPassingScore: 80,
          version: 1,
          items: [
            {
              id: 'item-1',
              text: 'Equipment function test performed',
              description: 'Test equipment operation after maintenance',
              required: true,
              critical: true,
              category: 'Testing',
              responseType: 'Pass/Fail',
              order: 1
            }
          ],
          createdDate: '2025-10-17T06:14:00.02',
          createdBy: 'System',
          lastModifiedDate: '2025-10-17T06:14:00.02',
          lastModifiedBy: 'System'
        },
        {
          id: '490aad1f-ffd0-4881-9672-e36890609d03',
          name: 'HVAC Repair Quality Check',
          description: 'Quality checklist for HVAC repair and maintenance work',
          workOrderType: 'REPAIR',
          assetCategory: 'HVAC',
          maintenanceType: 'CM',
          isMandatory: true,
          isActive: true,
          minimumPassingScore: 90,
          version: 1,
          items: [
            {
              id: 'item-1',
              text: 'System pressure checked',
              description: 'Verify system operates at correct pressure',
              required: true,
              critical: true,
              category: 'Performance',
              responseType: 'Pass/Fail',
              order: 1
            }
          ],
          createdDate: '2025-10-17T06:14:00.02',
          createdBy: 'System',
          lastModifiedDate: '2025-10-17T06:14:00.02',
          lastModifiedBy: 'System'
        },
        {
          id: 'eb93f532-d576-4f0e-883f-f4c117940ff5',
          name: 'General Maintenance Quality Check',
          description: 'Generic quality checklist for general maintenance activities',
          workOrderType: 'PM',
          assetCategory: 'GEN',
          maintenanceType: 'PM',
          isMandatory: false,
          isActive: true,
          minimumPassingScore: 75,
          version: 1,
          items: [
            {
              id: 'item-1',
              text: 'Work area cleaned up',
              description: 'Ensure work area is clean and organized',
              required: true,
              critical: false,
              category: 'General',
              responseType: 'Pass/Fail',
              order: 1
            }
          ],
          createdDate: '2025-10-17T06:14:00.02',
          createdBy: 'System',
          lastModifiedDate: '2025-10-17T06:14:00.02',
          lastModifiedBy: 'System'
        }
      ];
    }
  }

  async getChecklistById(id: string): Promise<QualityChecklist | null> {
    if (!this.useBackend) {
      throw new Error('Backend usage is disabled');
    }
    
    try {
      console.log(`Fetching quality checklist ${id} from backend API...`);
      const response = await apiService.get(`/quality-checklists/${id}`);
      console.log('Quality checklist fetched successfully:', response);
      return response;
    } catch (error: any) {
      console.error('Backend API not available, using fallback data:', error.message);
      
      // Get all checklists and find the one with matching ID
      const allChecklists = await this.getAllChecklists();
      return allChecklists.find(checklist => checklist.id === id) || null;
    }
  }

  async getChecklistsForWorkOrder(workOrderType: string, assetCategory: string, maintenanceType?: string): Promise<QualityChecklist[]> {
    if (!this.useBackend) {
      throw new Error('Backend usage is disabled');
    }
    
    const params = new URLSearchParams({
      workOrderType,
      assetCategory,
      ...(maintenanceType && { maintenanceType })
    });
    console.log(`Fetching quality checklists for work order: ${workOrderType}, category: ${assetCategory}`);
    const response = await apiService.get(`/quality-checklists?${params}`);
    console.log('Quality checklists for work order fetched successfully:', response);
    return response;
  }

  async createChecklist(data: CreateQualityChecklistDto): Promise<QualityChecklist> {
    if (!this.useBackend) {
      throw new Error('Backend usage is disabled');
    }
    
    console.log('Creating quality checklist:', data);
    const response = await apiService.post('/quality-checklists', data);
    console.log('Quality checklist created successfully:', response);
    return response;
  }

  async updateChecklist(id: string, data: UpdateQualityChecklistDto): Promise<QualityChecklist> {
    if (!this.useBackend) {
      throw new Error('Backend usage is disabled');
    }
    
    try {
      console.log(`Updating quality checklist ${id}:`, data);
      console.log('PUT URL will be:', `/quality-checklists/${id}`);
      console.log('Full URL will be:', `/api/quality-checklists/${id}`);
      
      const response = await apiService.put(`/quality-checklists/${id}`, data);
      console.log('Quality checklist updated successfully:', response);
      return response;
    } catch (error: any) {
      console.error('Update checklist failed:');
      console.error('- Error message:', error.message);
      console.error('- Status code:', error.response?.status);
      console.error('- Status text:', error.response?.statusText);
      console.error('- Request URL:', error.config?.url);
      console.error('- Base URL:', error.config?.baseURL);
      console.error('- Full request config:', error.config);
      throw error;
    }
  }

  async deleteChecklist(id: string): Promise<void> {
    if (!this.useBackend) {
      throw new Error('Backend usage is disabled');
    }
    
    console.log(`Deleting quality checklist ${id}`);
    await apiService.delete(`/quality-checklists/${id}`);
    console.log('Quality checklist deleted successfully');
  }

  async duplicateChecklist(id: string, newName: string): Promise<QualityChecklist> {
    const original = await this.getChecklistById(id);
    if (!original) {
      throw new Error('Original checklist not found');
    }

    const duplicateData: CreateQualityChecklistDto = {
      name: newName,
      description: `Copy of ${original.description || original.name}`,
      workOrderType: original.workOrderType,
      assetCategory: original.assetCategory,
      maintenanceType: original.maintenanceType,
      isMandatory: original.isMandatory,
      minimumPassingScore: original.minimumPassingScore,
      items: original.items.map(item => ({
        text: item.text,
        description: item.description,
        required: item.required,
        critical: item.critical,
        category: item.category,
        responseType: item.responseType,
        minScore: item.minScore,
        maxScore: item.maxScore,
        order: item.order
      }))
    };

    return this.createChecklist(duplicateData);
  }

  async getChecklistCategories(): Promise<string[]> {
    const checklists = await this.getAllChecklists();
    const categories = new Set<string>();
    
    checklists.forEach(checklist => {
      checklist.items.forEach(item => {
        categories.add(item.category);
      });
    });
    
    return Array.from(categories).sort();
  }

  async getWorkOrderTypes(): Promise<string[]> {
    // This could also call backend for dynamic work order types
    return ['Preventive', 'Corrective', 'Emergency', 'Safety', 'Regulatory', 'Installation'];
  }

  async getAssetCategories(): Promise<string[]> {
    try {
      console.log('Fetching asset categories from backend API...');
      const response = await apiService.get('/maintenance/asset-categories');
      console.log('Asset categories fetched successfully:', response);
      // Extract names from MaintenanceAssetCategoryDto objects
      return response.map((category: any) => category.name);
    } catch (error: any) {
      console.error('Backend API not available for asset categories, using fallback data:', error.message);
      return ['Vehicle', 'HVAC', 'Electrical', 'Fire Safety', 'Elevator', 'Plumbing', 'General Equipment'];
    }
  }

  async getMaintenanceTypes(): Promise<string[]> {
    try {
      console.log('Fetching maintenance types from database...');
      const response = await apiService.get('/maintenance/maintenance-types/active');
      console.log('Maintenance types fetched successfully:', response);
      // Extract names from MaintenanceTypeDto objects
      return response.map((type: any) => type.name);
    } catch (error: any) {
      console.error('Backend API not available for maintenance types, using fallback data:', error.message);
      return ['Scheduled', 'Inspection', 'Regulatory', 'Preventive', 'Reactive'];
    }
  }

  // Methods returning full objects with IDs for proper foreign key matching
  async getMaintenanceTypesWithIds(): Promise<DropdownOption[]> {
    try {
      console.log('Fetching maintenance types with IDs from database...');
      const response = await apiService.get('/maintenance/maintenance-types/active');
      console.log('Maintenance types fetched successfully:', response);
      return response.map((type: any) => ({ id: type.id, name: type.name }));
    } catch (error: any) {
      console.error('Backend API not available for maintenance types:', error.message);
      return [];
    }
  }

  async getWorkOrderTypesWithIds(): Promise<DropdownOption[]> {
    try {
      console.log('Fetching work order types with IDs from database...');
      const response = await apiService.get('/maintenance/work-order-types/active');
      console.log('Work order types fetched successfully:', response);
      return response.map((type: any) => ({ id: type.id, name: type.name }));
    } catch (error: any) {
      console.error('Backend API not available for work order types:', error.message);
      return [];
    }
  }

  async getAssetCategoriesWithIds(): Promise<DropdownOption[]> {
    try {
      console.log('Fetching asset categories with IDs from database...');
      const response = await apiService.get('/maintenance/asset-categories');
      console.log('Asset categories fetched successfully:', response);
      return response.map((cat: any) => ({ id: cat.id, name: cat.name }));
    } catch (error: any) {
      console.error('Backend API not available for asset categories:', error.message);
      return [];
    }
  }
}

export const qualityChecklistService = new QualityChecklistService();
