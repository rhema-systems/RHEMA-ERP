import { apiService as api } from './api';

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
  assetCategory: string;
  maintenanceType?: string;
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
  assetCategory: string;
  maintenanceType?: string;
  isMandatory: boolean;
  minimumPassingScore: number;
  items: Omit<QualityChecklistItem, 'id'>[];
}

export interface UpdateQualityChecklistDto extends CreateQualityChecklistDto {
  isActive: boolean;
}

// Mock data for fallback
const mockChecklists: QualityChecklist[] = [
  {
    id: '1',
    name: 'Vehicle Preventive Maintenance Checklist',
    description: 'Standard checklist for vehicle preventive maintenance',
    workOrderType: 'Preventive',
    assetCategory: 'Vehicle',
    maintenanceType: 'Scheduled',
    isMandatory: true,
    isActive: true,
    minimumPassingScore: 85,
    version: 1,
    items: [
      {
        id: '1-1',
        text: 'Engine oil changed',
        description: 'Verify engine oil has been changed with correct grade',
        required: true,
        critical: true,
        category: 'Engine',
        responseType: 'Pass/Fail',
        order: 1
      },
      {
        id: '1-2',
        text: 'Oil filter replaced',
        description: 'Confirm oil filter has been replaced with OEM or equivalent',
        required: true,
        critical: true,
        category: 'Engine',
        responseType: 'Pass/Fail',
        order: 2
      },
      {
        id: '1-3',
        text: 'Brake fluid level checked',
        description: 'Verify brake fluid is at proper level and condition',
        required: true,
        critical: true,
        category: 'Safety',
        responseType: 'Pass/Fail',
        order: 3
      },
      {
        id: '1-4',
        text: 'Tire pressure verified',
        description: 'Check all tires are at manufacturer specified pressure',
        required: true,
        critical: false,
        category: 'Safety',
        responseType: 'Pass/Fail',
        order: 4
      },
      {
        id: '1-5',
        text: 'All lights functional',
        description: 'Test headlights, taillights, turn signals, and hazards',
        required: true,
        critical: true,
        category: 'Safety',
        responseType: 'Pass/Fail',
        order: 5
      },
      {
        id: '1-6',
        text: 'Service documentation complete',
        description: 'Ensure all service records are properly documented',
        required: true,
        critical: false,
        category: 'Documentation',
        responseType: 'Pass/Fail',
        order: 6
      }
    ],
    createdDate: '2024-01-01',
    createdBy: 'System Administrator'
  },
  {
    id: '2',
    name: 'HVAC Safety Inspection Checklist',
    description: 'Safety inspection checklist for HVAC systems',
    workOrderType: 'Safety',
    assetCategory: 'HVAC',
    maintenanceType: 'Inspection',
    isMandatory: true,
    isActive: true,
    minimumPassingScore: 90,
    version: 1,
    items: [
      {
        id: '2-1',
        text: 'Electrical connections secure',
        description: 'Verify all electrical connections are tight and properly insulated',
        required: true,
        critical: true,
        category: 'Safety',
        responseType: 'Pass/Fail',
        order: 1
      },
      {
        id: '2-2',
        text: 'Refrigerant levels within range',
        description: 'Check refrigerant pressure and levels are within specifications',
        required: true,
        critical: true,
        category: 'Performance',
        responseType: 'Score',
        minScore: 1,
        maxScore: 10,
        order: 2
      },
      {
        id: '2-3',
        text: 'Air filter condition',
        description: 'Inspect air filters for cleanliness and proper installation',
        required: true,
        critical: false,
        category: 'Maintenance',
        responseType: 'Pass/Fail',
        order: 3
      },
      {
        id: '2-4',
        text: 'Thermostat calibration',
        description: 'Verify thermostat is reading and controlling temperature accurately',
        required: true,
        critical: false,
        category: 'Performance',
        responseType: 'Score',
        minScore: 1,
        maxScore: 10,
        order: 4
      },
      {
        id: '2-5',
        text: 'Emergency shutoff accessible',
        description: 'Ensure emergency shutoff switches are clearly marked and accessible',
        required: true,
        critical: true,
        category: 'Safety',
        responseType: 'Pass/Fail',
        order: 5
      }
    ],
    createdDate: '2024-01-01',
    createdBy: 'Safety Administrator'
  },
  {
    id: '3',
    name: 'Fire Safety System Inspection',
    description: 'Comprehensive fire safety system inspection checklist',
    workOrderType: 'Safety',
    assetCategory: 'Fire Safety',
    maintenanceType: 'Regulatory',
    isMandatory: true,
    isActive: true,
    minimumPassingScore: 95,
    version: 1,
    items: [
      {
        id: '3-1',
        text: 'Fire alarm system functional test',
        description: 'Test fire alarm system including all zones and notification devices',
        required: true,
        critical: true,
        category: 'Safety',
        responseType: 'Pass/Fail',
        order: 1
      },
      {
        id: '3-2',
        text: 'Sprinkler system pressure test',
        description: 'Verify sprinkler system water pressure meets code requirements',
        required: true,
        critical: true,
        category: 'Safety',
        responseType: 'Pass/Fail',
        order: 2
      },
      {
        id: '3-3',
        text: 'Fire extinguisher inspection',
        description: 'Check all fire extinguishers for proper charge, seals, and accessibility',
        required: true,
        critical: true,
        category: 'Safety',
        responseType: 'Pass/Fail',
        order: 3
      },
      {
        id: '3-4',
        text: 'Emergency lighting test',
        description: 'Test emergency lighting systems and battery backup',
        required: true,
        critical: true,
        category: 'Safety',
        responseType: 'Pass/Fail',
        order: 4
      },
      {
        id: '3-5',
        text: 'Exit signs illuminated',
        description: 'Verify all exit signs are properly illuminated and visible',
        required: true,
        critical: true,
        category: 'Safety',
        responseType: 'Pass/Fail',
        order: 5
      },
      {
        id: '3-6',
        text: 'Fire doors operational',
        description: 'Test fire doors for proper closure and no obstructions',
        required: true,
        critical: true,
        category: 'Safety',
        responseType: 'Pass/Fail',
        order: 6
      }
    ],
    createdDate: '2024-01-01',
    createdBy: 'Fire Safety Inspector'
  }
];

class QualityChecklistService {
  private useBackend = true; // Set to false to force mock data

  async getAllChecklists(): Promise<QualityChecklist[]> {
    try {
      if (this.useBackend) {
        const response = await api.get('/api/quality-control/checklists');
        return response.data;
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }
    
    // Return mock data as fallback
    return mockChecklists;
  }

  async getChecklistById(id: string): Promise<QualityChecklist | null> {
    try {
      if (this.useBackend) {
        const response = await api.get(`/api/quality-control/checklists/${id}`);
        return response.data;
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }
    
    // Return mock data as fallback
    return mockChecklists.find(checklist => checklist.id === id) || null;
  }

  async getChecklistsForWorkOrder(workOrderType: string, assetCategory: string, maintenanceType?: string): Promise<QualityChecklist[]> {
    try {
      if (this.useBackend) {
        const params = new URLSearchParams({
          workOrderType,
          assetCategory,
          ...(maintenanceType && { maintenanceType })
        });
        const response = await api.get(`/api/quality-control/checklists/for-work-order?${params}`);
        return response.data;
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }
    
    // Return mock data as fallback
    return mockChecklists.filter(checklist => 
      checklist.workOrderType === workOrderType && 
      checklist.assetCategory === assetCategory &&
      checklist.isActive &&
      (!maintenanceType || checklist.maintenanceType === maintenanceType)
    );
  }

  async createChecklist(data: CreateQualityChecklistDto): Promise<QualityChecklist> {
    try {
      if (this.useBackend) {
        const response = await api.post('/api/quality-control/checklists', data);
        return response.data;
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }
    
    // Return mock data as fallback
    const newId = (mockChecklists.length + 1).toString();
    const newChecklist: QualityChecklist = {
      id: newId,
      ...data,
      version: 1,
      isActive: true,
      createdDate: new Date().toISOString(),
      createdBy: 'Current User',
      items: data.items.map((item, index) => ({
        ...item,
        id: `${newId}-${index + 1}`
      }))
    };
    
    mockChecklists.push(newChecklist);
    return newChecklist;
  }

  async updateChecklist(id: string, data: UpdateQualityChecklistDto): Promise<QualityChecklist> {
    try {
      if (this.useBackend) {
        const response = await api.put(`/api/quality-control/checklists/${id}`, data);
        return response.data;
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }
    
    // Return mock data as fallback
    const existingIndex = mockChecklists.findIndex(c => c.id === id);
    if (existingIndex === -1) {
      throw new Error('Checklist not found');
    }
    
    const existing = mockChecklists[existingIndex];
    const updatedChecklist: QualityChecklist = {
      ...existing,
      ...data,
      version: existing.version + 1,
      lastModifiedDate: new Date().toISOString(),
      lastModifiedBy: 'Current User',
      items: data.items.map((item, index) => ({
        ...item,
        id: item.id || `${id}-${index + 1}`
      }))
    };
    
    mockChecklists[existingIndex] = updatedChecklist;
    return updatedChecklist;
  }

  async deleteChecklist(id: string): Promise<void> {
    try {
      if (this.useBackend) {
        await api.delete(`/api/quality-control/checklists/${id}`);
        return;
      }
    } catch (error) {
      console.warn('Backend unavailable, using mock data:', error);
    }
    
    // Remove from mock data as fallback
    const index = mockChecklists.findIndex(c => c.id === id);
    if (index > -1) {
      mockChecklists.splice(index, 1);
    }
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
    // This could also call backend for dynamic asset categories
    return ['Vehicle', 'HVAC', 'Electrical', 'Fire Safety', 'Elevator', 'Plumbing', 'General Equipment'];
  }

  async getMaintenanceTypes(): Promise<string[]> {
    return ['Scheduled', 'Inspection', 'Regulatory', 'Preventive', 'Reactive'];
  }
}

export const qualityChecklistService = new QualityChecklistService();