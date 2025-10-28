import axios from 'axios';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

// Types
export interface TaskTemplate {
  id?: string;
  taskName: string;
  description: string;
  sequence: number;
  estimatedHours: number;
  isRequired: boolean;
  instructions?: string;
  safetyRequirements?: string;
  requiredTools?: string;
  requiredParts?: string;
  isActive: boolean;
}

export interface AssetTaskTemplate extends TaskTemplate {
  assetId: string;
  assetName: string;
  maintenanceTypeId: string;
  maintenanceTypeName: string;
}

export interface AssetTypeTaskTemplate extends TaskTemplate {
  assetTypeId: string;
  assetTypeName: string;
  maintenanceTypeId: string;
  maintenanceTypeName: string;
}

export interface MaintenanceTaskTemplate extends TaskTemplate {
  maintenanceTypeId: string;
  maintenanceTypeName: string;
}

export interface CreateAssetTaskTemplateRequest {
  assetId: string;
  maintenanceTypeId: string;
  taskName: string;
  description: string;
  sequence: number;
  estimatedHours: number;
  isRequired: boolean;
  instructions?: string;
  safetyRequirements?: string;
  requiredTools?: string;
  requiredParts?: string;
  isActive: boolean;
}

export interface CreateAssetTypeTaskTemplateRequest {
  assetTypeId: string;
  maintenanceTypeId: string;
  taskName: string;
  description: string;
  sequence: number;
  estimatedHours: number;
  isRequired: boolean;
  instructions?: string;
  safetyRequirements?: string;
  requiredTools?: string;
  requiredParts?: string;
  isActive: boolean;
}

export interface CreateMaintenanceTaskTemplateRequest {
  maintenanceTypeId: string;
  taskName: string;
  description: string;
  sequence: number;
  estimatedHours: number;
  isRequired: boolean;
  instructions?: string;
  safetyRequirements?: string;
  isActive: boolean;
}

export interface WorkOrderTaskTemplate {
  taskName: string;
  description: string;
  sequence: number;
  estimatedHours: number;
  isRequired: boolean;
  instructions?: string;
  safetyRequirements?: string;
  requiredTools?: string;
  requiredParts?: string;
  source: string; // "Asset", "AssetType", or "MaintenanceType"
}

export interface BulkImportRequest {
  templates: CreateAssetTaskTemplateRequest[] | CreateAssetTypeTaskTemplateRequest[] | CreateMaintenanceTaskTemplateRequest[];
  templateType: 'asset' | 'assetType' | 'maintenanceType';
}

class TaskTemplateService {
  private getAuthHeaders() {
    const token = localStorage.getItem('authToken');
    return {
      'Authorization': token ? `Bearer ${token}` : '',
      'Content-Type': 'application/json'
    };
  }

  // Asset Task Templates
  async getAssetTaskTemplates(assetId?: string, maintenanceTypeId?: string): Promise<AssetTaskTemplate[]> {
    const params = new URLSearchParams();
    if (assetId) params.append('assetId', assetId);
    if (maintenanceTypeId) params.append('maintenanceTypeId', maintenanceTypeId);

    const response = await axios.get(`${API_URL}/maintenance/task-templates/asset?${params}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async createAssetTaskTemplate(request: CreateAssetTaskTemplateRequest): Promise<AssetTaskTemplate> {
    const response = await axios.post(`${API_URL}/maintenance/task-templates/asset`, request, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async updateAssetTaskTemplate(id: string, request: Partial<CreateAssetTaskTemplateRequest>): Promise<AssetTaskTemplate> {
    const response = await axios.put(`${API_URL}/maintenance/task-templates/asset/${id}`, request, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async deleteAssetTaskTemplate(id: string): Promise<void> {
    await axios.delete(`${API_URL}/maintenance/task-templates/asset/${id}`, {
      headers: this.getAuthHeaders()
    });
  }

  // Asset Type Task Templates
  async getAssetTypeTaskTemplates(assetTypeId?: string, maintenanceTypeId?: string): Promise<AssetTypeTaskTemplate[]> {
    const params = new URLSearchParams();
    if (assetTypeId) params.append('assetTypeId', assetTypeId);
    if (maintenanceTypeId) params.append('maintenanceTypeId', maintenanceTypeId);

    const response = await axios.get(`${API_URL}/maintenance/task-templates/asset-type?${params}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async createAssetTypeTaskTemplate(request: CreateAssetTypeTaskTemplateRequest): Promise<AssetTypeTaskTemplate> {
    const response = await axios.post(`${API_URL}/maintenance/task-templates/asset-type`, request, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async updateAssetTypeTaskTemplate(id: string, request: Partial<CreateAssetTypeTaskTemplateRequest>): Promise<AssetTypeTaskTemplate> {
    const response = await axios.put(`${API_URL}/maintenance/task-templates/asset-type/${id}`, request, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async deleteAssetTypeTaskTemplate(id: string): Promise<void> {
    await axios.delete(`${API_URL}/maintenance/task-templates/asset-type/${id}`, {
      headers: this.getAuthHeaders()
    });
  }

  // Maintenance Type Task Templates (Generic)
  async getMaintenanceTaskTemplates(maintenanceTypeId?: string): Promise<MaintenanceTaskTemplate[]> {
    const params = new URLSearchParams();
    if (maintenanceTypeId) params.append('maintenanceTypeId', maintenanceTypeId);

    const response = await axios.get(`${API_URL}/maintenance/task-templates/maintenance-type?${params}`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async createMaintenanceTaskTemplate(request: CreateMaintenanceTaskTemplateRequest): Promise<MaintenanceTaskTemplate> {
    const response = await axios.post(`${API_URL}/maintenance/task-templates/maintenance-type`, request, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async updateMaintenanceTaskTemplate(id: string, request: Partial<CreateMaintenanceTaskTemplateRequest>): Promise<MaintenanceTaskTemplate> {
    const response = await axios.put(`${API_URL}/maintenance/task-templates/maintenance-type/${id}`, request, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async deleteMaintenanceTaskTemplate(id: string): Promise<void> {
    await axios.delete(`${API_URL}/maintenance/task-templates/maintenance-type/${id}`, {
      headers: this.getAuthHeaders()
    });
  }

  // Work Order Task Generation
  async getTaskTemplatesForWorkOrder(assetId: string, maintenanceTypeId: string): Promise<WorkOrderTaskTemplate[]> {
    const response = await axios.get(`${API_URL}/maintenance/task-templates/work-order`, {
      params: { assetId, maintenanceTypeId },
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Template Management Utilities
  async duplicateAssetTaskTemplate(id: string, targetAssetId?: string): Promise<AssetTaskTemplate> {
    const response = await axios.post(`${API_URL}/maintenance/task-templates/asset/${id}/duplicate`, {
      targetAssetId
    }, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async copyAssetTypeToAsset(assetTypeId: string, assetId: string, maintenanceTypeId: string): Promise<AssetTaskTemplate[]> {
    const response = await axios.post(`${API_URL}/maintenance/task-templates/copy-type-to-asset`, {
      assetTypeId,
      assetId,
      maintenanceTypeId
    }, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  async reorderTemplates(
    templateType: 'asset' | 'assetType' | 'maintenanceType',
    templateIds: string[]
  ): Promise<void> {
    await axios.post(`${API_URL}/maintenance/task-templates/${templateType}/reorder`, {
      templateIds
    }, {
      headers: this.getAuthHeaders()
    });
  }

  // Bulk Operations
  async bulkImportTemplates(request: BulkImportRequest): Promise<number> {
    const response = await axios.post(`${API_URL}/maintenance/task-templates/bulk-import`, request, {
      headers: this.getAuthHeaders()
    });
    return response.data.importedCount;
  }

  async exportTemplates(
    templateType: 'asset' | 'assetType' | 'maintenanceType',
    filters?: {
      assetId?: string;
      assetTypeId?: string;
      maintenanceTypeId?: string;
    }
  ): Promise<Blob> {
    const response = await axios.get(`${API_URL}/maintenance/task-templates/export`, {
      params: { templateType, ...filters },
      headers: {
        ...this.getAuthHeaders(),
        'Accept': 'application/json'
      },
      responseType: 'blob'
    });
    return response.data;
  }

  // Validation
  async validateTemplateStructure(templates: any[]): Promise<{
    valid: boolean;
    errors: string[];
    warnings: string[];
  }> {
    const response = await axios.post(`${API_URL}/maintenance/task-templates/validate`, {
      templates
    }, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Template Statistics
  async getTemplateStatistics(): Promise<{
    assetTemplateCount: number;
    assetTypeTemplateCount: number;
    maintenanceTypeTemplateCount: number;
    mostUsedTemplates: {
      templateId: string;
      templateName: string;
      usageCount: number;
    }[];
    assetsWithoutTemplates: {
      assetId: string;
      assetName: string;
      assetType: string;
    }[];
  }> {
    const response = await axios.get(`${API_URL}/maintenance/task-templates/statistics`, {
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // Template Suggestions
  async suggestTasksForAsset(assetId: string, maintenanceTypeId: string): Promise<{
    suggestedTasks: WorkOrderTaskTemplate[];
    source: string;
    confidence: number;
  }> {
    const response = await axios.get(`${API_URL}/maintenance/task-templates/suggestions`, {
      params: { assetId, maintenanceTypeId },
      headers: this.getAuthHeaders()
    });
    return response.data;
  }

  // File Operations
  async uploadTemplateFile(file: File, templateType: 'asset' | 'assetType' | 'maintenanceType'): Promise<{
    success: boolean;
    importedCount: number;
    errors: string[];
  }> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('templateType', templateType);

    const response = await axios.post(`${API_URL}/maintenance/task-templates/upload`, formData, {
      headers: {
        'Authorization': localStorage.getItem('authToken') ? `Bearer ${localStorage.getItem('authToken')}` : '',
        'Content-Type': 'multipart/form-data'
      }
    });
    return response.data;
  }

  async downloadTemplateTemplate(): Promise<Blob> {
    const response = await axios.get(`${API_URL}/maintenance/task-templates/download-template`, {
      headers: this.getAuthHeaders(),
      responseType: 'blob'
    });
    return response.data;
  }
}

const taskTemplateService = new TaskTemplateService();
export default taskTemplateService;