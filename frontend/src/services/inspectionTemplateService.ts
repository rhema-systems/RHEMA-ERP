import { compatibleApiService as apiService } from './compatibleApiService';

export interface InspectionTemplateChecklistItem {
  id: string;
  item: string;
  type: 'checklist' | 'text' | 'number' | 'measurement';
  required: boolean;
}

export interface InspectionTemplate {
  id: string;
  name: string;
  code: string;
  description: string;
  category: string;
  frequency: string;
  estimatedDuration: number;
  isActive: boolean;
  requiresSignature: boolean;
  allowPhotos: boolean;
  version: string;
  assetTypes: string[];
  inspectorRoles: string[];
  priority: string;
  checklistItems: InspectionTemplateChecklistItem[];
  createdBy: string;
  lastUpdated: string;
}

export interface CreateInspectionTemplateDto {
  name: string;
  code: string;
  description: string;
  category: string;
  frequency: string;
  estimatedDuration: number;
  requiresSignature: boolean;
  allowPhotos: boolean;
  version: string;
  assetTypes: string[];
  inspectorRoles: string[];
  priority: string;
  checklistItems: Omit<InspectionTemplateChecklistItem, 'id'>[];
}

export interface UpdateInspectionTemplateDto extends CreateInspectionTemplateDto {
  isActive: boolean;
}

// Mock data for fallback
const mockTemplates: InspectionTemplate[] = [
  {
    id: '1',
    name: 'Monthly Equipment Safety Inspection',
    code: 'MESI-001',
    description: 'Comprehensive monthly safety check for all production equipment',
    category: 'Safety',
    frequency: 'Monthly',
    estimatedDuration: 120,
    isActive: true,
    requiresSignature: true,
    allowPhotos: true,
    version: '2.1',
    createdBy: 'John Smith',
    lastUpdated: '2024-03-15',
    checklistItems: [
      { id: '1', item: 'Check emergency stop buttons', type: 'checklist', required: true },
      { id: '2', item: 'Inspect safety guards', type: 'checklist', required: true },
      { id: '3', item: 'Test warning lights', type: 'checklist', required: true },
      { id: '4', item: 'Verify lockout/tagout procedures', type: 'checklist', required: true },
      { id: '5', item: 'Document any issues found', type: 'text', required: false }
    ],
    assetTypes: ['Production Equipment', 'Conveyors'],
    inspectorRoles: ['Safety Inspector', 'Maintenance Supervisor'],
    priority: 'High'
  }
];

class InspectionTemplateService {
  private useBackend = true;

  async getAllTemplates(): Promise<InspectionTemplate[]> {
    try {
      if (this.useBackend) {
        console.log('Fetching inspection templates from backend API...');
        const response = await apiService.get('/inspection-templates');
        console.log('Inspection templates fetched successfully:', response);
        return response;
      }
    } catch (error) {
      console.error('Backend error, using mock data:', error);
    }

    return mockTemplates;
  }

  async getTemplateById(id: string): Promise<InspectionTemplate | null> {
    try {
      if (this.useBackend) {
        console.log(`Fetching inspection template ${id} from backend API...`);
        const response = await apiService.get(`/inspection-templates/${id}`);
        console.log('Inspection template fetched successfully:', response);
        return response;
      }
    } catch (error) {
      console.error('Backend error, using mock data:', error);
    }

    return mockTemplates.find(template => template.id === id) || null;
  }

  async createTemplate(data: CreateInspectionTemplateDto): Promise<InspectionTemplate> {
    try {
      if (this.useBackend) {
        console.log('Creating inspection template:', data);
        const response = await apiService.post('/inspection-templates', data);
        console.log('Inspection template created successfully:', response);
        return response;
      }
    } catch (error) {
      console.error('Backend error creating template, using mock data:', error);
    }

    // Mock creation
    const newTemplate: InspectionTemplate = {
      id: (mockTemplates.length + 1).toString(),
      ...data,
      isActive: true,
      createdBy: 'Current User',
      lastUpdated: new Date().toISOString().split('T')[0],
      checklistItems: data.checklistItems.map((item, index) => ({
        ...item,
        id: `${mockTemplates.length + 1}-${index + 1}`
      }))
    };

    mockTemplates.push(newTemplate);
    return newTemplate;
  }

  async updateTemplate(id: string, data: UpdateInspectionTemplateDto): Promise<InspectionTemplate> {
    try {
      if (this.useBackend) {
        console.log(`Updating inspection template ${id}:`, data);
        const response = await apiService.put(`/inspection-templates/${id}`, data);
        console.log('Inspection template updated successfully:', response);
        return response;
      }
    } catch (error) {
      console.error('Backend error updating template, using mock data:', error);
    }

    // Mock update
    const existingIndex = mockTemplates.findIndex(t => t.id === id);
    if (existingIndex === -1) {
      throw new Error('Template not found');
    }

    const existing = mockTemplates[existingIndex];
    const updatedTemplate: InspectionTemplate = {
      ...existing,
      ...data,
      lastUpdated: new Date().toISOString().split('T')[0],
      checklistItems: data.checklistItems.map((item, index) => ({
        ...item,
        id: item.id || `${id}-${index + 1}`
      }))
    };

    mockTemplates[existingIndex] = updatedTemplate;
    return updatedTemplate;
  }

  async deleteTemplate(id: string): Promise<void> {
    try {
      if (this.useBackend) {
        console.log(`Deleting inspection template ${id}`);
        await apiService.delete(`/inspection-templates/${id}`);
        console.log('Inspection template deleted successfully');
        return;
      }
    } catch (error) {
      console.error('Backend error deleting template, using mock data:', error);
    }

    // Mock deletion
    const index = mockTemplates.findIndex(t => t.id === id);
    if (index > -1) {
      mockTemplates.splice(index, 1);
    }
  }

  async duplicateTemplate(id: string, newName: string, newCode: string): Promise<InspectionTemplate> {
    const original = await this.getTemplateById(id);
    if (!original) {
      throw new Error('Original template not found');
    }

    const duplicateData: CreateInspectionTemplateDto = {
      name: newName,
      code: newCode,
      description: `Copy of ${original.description}`,
      category: original.category,
      frequency: original.frequency,
      estimatedDuration: original.estimatedDuration,
      requiresSignature: original.requiresSignature,
      allowPhotos: original.allowPhotos,
      version: '1.0',
      assetTypes: [...original.assetTypes],
      inspectorRoles: [...original.inspectorRoles],
      priority: original.priority,
      checklistItems: original.checklistItems.map(item => ({
        item: item.item,
        type: item.type,
        required: item.required
      }))
    };

    return this.createTemplate(duplicateData);
  }
}

export const inspectionTemplateService = new InspectionTemplateService();