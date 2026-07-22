import { compatibleApiService as apiService } from './compatibleApiService';

export interface InspectionTemplateChecklistItem {
  id: string;
  item: string;
  type: 'checklist' | 'yesno' | 'text' | 'number' | 'measurement';
  required: boolean;
  order: number;
}

export interface InspectionTemplate {
  id: string;
  name: string;
  code: string;
  description: string;
  category: string;
  sheetType: 'InspectionSheet' | 'ServiceSheet' | 'WeeklyChecklist' | 'PreventiveMaintenanceForm' | string;
  templateScope: 'General' | 'Fleet' | string;
  fleetInspectionKind: 'Any' | 'PreTrip' | 'PostTrip' | string;
  assignedAssetCategoryId?: string | null;
  assignedAssetId?: string | null;
  isQrEnabled: boolean;
  mobileOfflineEnabled: boolean;
  qrPayloadVersion: number;
  autoCreateWorkOrderOnFailure: boolean;
  failureWorkOrderTypeId?: string | null;
  failureMaintenanceTypeId?: string | null;
  failurePriorityLevelId?: string | null;
  failureBillingType: 'Default' | 'Repairs' | 'Maintenance' | string;
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
  sheetType: 'InspectionSheet' | 'ServiceSheet' | 'WeeklyChecklist' | 'PreventiveMaintenanceForm' | string;
  templateScope: 'General' | 'Fleet' | string;
  fleetInspectionKind: 'Any' | 'PreTrip' | 'PostTrip' | string;
  assignedAssetCategoryId?: string | null;
  assignedAssetId?: string | null;
  isQrEnabled: boolean;
  mobileOfflineEnabled: boolean;
  qrPayloadVersion: number;
  autoCreateWorkOrderOnFailure: boolean;
  failureWorkOrderTypeId?: string | null;
  failureMaintenanceTypeId?: string | null;
  failurePriorityLevelId?: string | null;
  failureBillingType: 'Default' | 'Repairs' | 'Maintenance' | string;
  frequency: string;
  estimatedDuration: number;
  isActive: boolean;
  requiresSignature: boolean;
  allowPhotos: boolean;
  version: string;
  assetTypes: string[];
  inspectorRoles: string[];
  priority: string;
  checklistItems: Omit<InspectionTemplateChecklistItem, 'id'>[];
}

export interface UpdateInspectionTemplateDto extends CreateInspectionTemplateDto {}

export interface InspectionTemplateQuery {
  activeOnly?: boolean;
  category?: string;
  sheetType?: string;
  frequency?: string;
  searchTerm?: string;
  templateScope?: string;
  fleetInspectionKind?: string;
  assignedAssetCategoryId?: string;
  assignedAssetId?: string;
  isQrEnabled?: boolean;
  mobileOfflineEnabled?: boolean;
}

export interface InspectionTemplateQrPackage {
  schema: string;
  packageId: string;
  templateId: string;
  templateName: string;
  templateCode: string;
  templateVersion: string;
  sheetType: string;
  templateScope: string;
  fleetInspectionKind: string;
  inspectionKind: string;
  assetId?: string | null;
  assetName?: string | null;
  assetNumber?: string | null;
  assetCategoryId?: string | null;
  assetCategoryName?: string | null;
  fleetTripId?: string | null;
  isQrEnabled: boolean;
  mobileOfflineEnabled: boolean;
  allowPhotos: boolean;
  qrPayloadVersion: number;
  generatedAtUtc: string;
  mobileUrl: string;
  payloadHash: string;
  signature: string;
  compactPayloadJson: string;
  compactPayloadBase64Url: string;
  qrValue: string;
  qrPayloadMode: 'Reference' | 'Embedded' | 'UrlOnly' | string;
  canEmbedFullPayload: boolean;
  payloadSizeBytes: number;
  maxQrPayloadBytes: number;
  checklistItems: InspectionTemplateChecklistItem[];
}

export interface InspectionTemplateQrPackageQuery {
  assetId?: string | null;
  assetCategoryId?: string | null;
  fleetTripId?: string | null;
  inspectionKind?: string | null;
  includeEmbeddedPayload?: boolean;
  maxQrPayloadBytes?: number;
}

// Mock data for fallback
const mockTemplates: InspectionTemplate[] = [
  {
    id: '1',
    name: 'Monthly Equipment Safety Inspection',
    code: 'MESI-001',
    description: 'Comprehensive monthly safety check for all production equipment',
    category: 'Safety',
    sheetType: 'InspectionSheet',
    templateScope: 'General',
    fleetInspectionKind: 'Any',
    assignedAssetCategoryId: null,
    assignedAssetId: null,
    isQrEnabled: false,
    mobileOfflineEnabled: false,
    qrPayloadVersion: 1,
    autoCreateWorkOrderOnFailure: true,
    failureWorkOrderTypeId: null,
    failureMaintenanceTypeId: null,
    failurePriorityLevelId: null,
    failureBillingType: 'Repairs',
    frequency: 'Monthly',
    estimatedDuration: 120,
    isActive: true,
    requiresSignature: true,
    allowPhotos: true,
    version: '2.1',
    createdBy: 'John Smith',
    lastUpdated: '2024-03-15',
    checklistItems: [
      { id: '1', item: 'Check emergency stop buttons', type: 'checklist', required: true, order: 1 },
      { id: '2', item: 'Inspect safety guards', type: 'checklist', required: true, order: 2 },
      { id: '3', item: 'Test warning lights', type: 'checklist', required: true, order: 3 },
      { id: '4', item: 'Verify lockout/tagout procedures', type: 'checklist', required: true, order: 4 },
      { id: '5', item: 'Document any issues found', type: 'text', required: false, order: 5 }
    ],
    assetTypes: ['Production Equipment', 'Conveyors'],
    inspectorRoles: ['Safety Inspector', 'Maintenance Supervisor'],
    priority: 'High'
  }
];

class InspectionTemplateService {
  private useBackend = true;

  async getAllTemplates(query?: InspectionTemplateQuery): Promise<InspectionTemplate[]> {
    try {
      if (this.useBackend) {
        console.log('Fetching inspection templates from backend API...');
        const params = new URLSearchParams();
        if (query?.activeOnly !== undefined) params.set('activeOnly', String(query.activeOnly));
        if (query?.category) params.set('category', query.category);
        if (query?.sheetType) params.set('sheetType', query.sheetType);
        if (query?.frequency) params.set('frequency', query.frequency);
        if (query?.searchTerm) params.set('searchTerm', query.searchTerm);
        if (query?.templateScope) params.set('templateScope', query.templateScope);
        if (query?.fleetInspectionKind) params.set('fleetInspectionKind', query.fleetInspectionKind);
        if (query?.assignedAssetCategoryId) params.set('assignedAssetCategoryId', query.assignedAssetCategoryId);
        if (query?.assignedAssetId) params.set('assignedAssetId', query.assignedAssetId);
        if (query?.isQrEnabled !== undefined) params.set('isQrEnabled', String(query.isQrEnabled));
        if (query?.mobileOfflineEnabled !== undefined) params.set('mobileOfflineEnabled', String(query.mobileOfflineEnabled));
        const suffix = params.toString() ? `?${params.toString()}` : '';
        const response = await apiService.get(`/inspection-templates${suffix}`);
        console.log('Inspection templates fetched successfully:', response);
        return response;
      }
    } catch (error) {
      console.error('Backend error, using mock data:', error);
    }

    return mockTemplates;
  }

  async getOfflineCatalog(): Promise<InspectionTemplate[]> {
    const params = new URLSearchParams({
      activeOnly: 'true',
      isQrEnabled: 'true',
      mobileOfflineEnabled: 'true',
    });
    return apiService.get(`/inspection-templates?${params.toString()}`);
  }

  async getFleetOfflineCatalog(): Promise<InspectionTemplate[]> {
    return this.getOfflineCatalog();
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

  async getQrPackage(id: string, query?: InspectionTemplateQrPackageQuery): Promise<InspectionTemplateQrPackage> {
    const params = new URLSearchParams();
    if (query?.assetId) params.set('assetId', query.assetId);
    if (query?.assetCategoryId) params.set('assetCategoryId', query.assetCategoryId);
    if (query?.fleetTripId) params.set('fleetTripId', query.fleetTripId);
    if (query?.inspectionKind) params.set('inspectionKind', query.inspectionKind);
    if (query?.includeEmbeddedPayload !== undefined) params.set('includeEmbeddedPayload', String(query.includeEmbeddedPayload));
    if (query?.maxQrPayloadBytes) params.set('maxQrPayloadBytes', String(query.maxQrPayloadBytes));
    const suffix = params.toString() ? `?${params.toString()}` : '';

    return apiService.get(`/inspection-templates/${id}/qr-package${suffix}`);
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
        id: `${id}-${index + 1}`
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
      templateScope: original.templateScope || 'General',
      sheetType: original.sheetType || 'InspectionSheet',
      fleetInspectionKind: original.fleetInspectionKind || 'Any',
      assignedAssetCategoryId: original.assignedAssetCategoryId || null,
      assignedAssetId: original.assignedAssetId || null,
      isQrEnabled: original.isQrEnabled,
      mobileOfflineEnabled: original.mobileOfflineEnabled,
      qrPayloadVersion: original.qrPayloadVersion || 1,
      autoCreateWorkOrderOnFailure: original.autoCreateWorkOrderOnFailure !== false,
      failureWorkOrderTypeId: original.failureWorkOrderTypeId || null,
      failureMaintenanceTypeId: original.failureMaintenanceTypeId || null,
      failurePriorityLevelId: original.failurePriorityLevelId || null,
      failureBillingType: original.failureBillingType || 'Repairs',
      frequency: original.frequency,
      estimatedDuration: original.estimatedDuration,
      isActive: original.isActive,
      requiresSignature: original.requiresSignature,
      allowPhotos: original.allowPhotos,
      version: '1.0',
      assetTypes: [...original.assetTypes],
      inspectorRoles: [...original.inspectorRoles],
      priority: original.priority,
      checklistItems: original.checklistItems.map(item => ({
        item: item.item,
        type: item.type,
        required: item.required,
        order: item.order ?? 0
      }))
    };

    return this.createTemplate(duplicateData);
  }
}

export const inspectionTemplateService = new InspectionTemplateService();
