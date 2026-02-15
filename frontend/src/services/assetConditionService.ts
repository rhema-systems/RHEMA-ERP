import { compatibleApiService as apiService } from './compatibleApiService';

// Template DTOs
export interface AssetConditionChecklistItemDto {
  id: string;
  templateId: string;
  itemName: string;
  description?: string;
  category: string;
  itemType: 'Boolean' | 'Text' | 'Numeric' | 'Choice';
  isRequired: boolean;
  sortOrder: number;
  choiceOptions?: string[];
  unit?: string;
  minValue?: number;
  maxValue?: number;
  defaultValue?: string;
  helpText?: string;
  requiresPhoto: boolean;
  // Repair/Replacement options
  allowRepairReplacement: boolean;
  defaultRepairReplacementAction?: 'None' | 'Repair' | 'Replace';
  estimatedRepairHours: number;
  estimatedReplacementHours: number;
}

export interface AssetConditionChecklistTemplateDto {
  id: string;
  name: string;
  description?: string;
  category: string;
  assetCategoryId: string;
  assetCategoryName: string;
  isActive: boolean;
  isDefault: boolean;
  sortOrder: number;
  version: number;
  versionNotes?: string;
  itemCount: number;
  createdAt: string;
  checklistItems: AssetConditionChecklistItemDto[];
}

export interface CreateAssetConditionItemDto {
  itemName: string;
  description?: string;
  category: string;
  itemType: string;
  isRequired: boolean;
  sortOrder: number;
  choiceOptions?: string[];
  unit?: string;
  minValue?: number;
  maxValue?: number;
  defaultValue?: string;
  helpText?: string;
  requiresPhoto: boolean;
  // Repair/Replacement options
  allowRepairReplacement: boolean;
  defaultRepairReplacementAction?: 'None' | 'Repair' | 'Replace';
  estimatedRepairHours: number;
  estimatedReplacementHours: number;
}

export interface CreateAssetConditionTemplateDto {
  name: string;
  description?: string;
  category: string;
  assetCategoryId: string;
  isActive: boolean;
  isDefault: boolean;
  sortOrder: number;
  checklistItems: CreateAssetConditionItemDto[];
}

export interface UpdateAssetConditionTemplateDto extends CreateAssetConditionTemplateDto {
  versionNotes?: string;
}

// Record DTOs
export interface AssetConditionItemResultDto {
  id: string;
  conditionRecordId: string;
  checklistItemId: string;
  itemName: string;
  category: string;
  itemType: string;
  isRequired: boolean;
  isPresent?: boolean;
  textValue?: string;
  numericValue?: number;
  selectedOption?: string;
  comment?: string;
  photoPaths?: string[];
  inspectedAt?: string;
  // Repair/Replacement tracking
  repairReplacementAction?: 'None' | 'Repair' | 'Replace';
  taskCreated: boolean;
  createdTaskId?: string;
  // From checklist item
  allowRepairReplacement: boolean;
}

export interface AssetConditionRecordDto {
  id: string;
  inspectionNumber: string;
  assetId: string;
  assetName: string;
  assetNumber: string;
  templateId: string;
  templateName: string;
  inspectorId: string;
  inspectorName: string;
  inspectionDate: string;
  inspectionType: 'Admission' | 'Discharge';
  status: 'InProgress' | 'Completed' | 'Cancelled';
  generalNotes?: string;
  admissionId?: string;
  dischargeId?: string;
  photoPaths?: string[];
  createdAt: string;
  itemResults: AssetConditionItemResultDto[];
  totalItems: number;
  completedItems: number;
}

export interface AssetConditionRecordSummaryDto {
  id: string;
  inspectionNumber: string;
  assetName: string;
  assetNumber: string;
  templateName: string;
  inspectorName: string;
  inspectionDate: string;
  inspectionType: string;
  status: string;
  totalItems: number;
  completedItems: number;
  hasAdmission: boolean;
  hasDischarge: boolean;
}

export interface CreateAssetConditionRecordDto {
  assetId: string;
  templateId: string;
  inspectionType: 'Admission' | 'Discharge';
  admissionId?: string;
  inspectorId?: string;
  generalNotes?: string;
}

export interface SubmitAssetConditionItemDto {
  checklistItemId: string;
  isPresent?: boolean;
  textValue?: string;
  numericValue?: number;
  selectedOption?: string;
  comment?: string;
  repairReplacementAction?: 'None' | 'Repair' | 'Replace';
  photoPaths?: string[];
}

export interface CompleteAssetConditionRecordDto {
  generalNotes?: string;
}

const BASE_URL = '/maintenance/asset-conditions';

class AssetConditionService {
  // Template endpoints
  async getAllTemplates(includeInactive = false): Promise<AssetConditionChecklistTemplateDto[]> {
    try {
      const response = await apiService.get(`${BASE_URL}/templates?includeInactive=${includeInactive}`);
      return response;
    } catch (error) {
      console.error('Error fetching asset condition templates:', error);
      throw error;
    }
  }

  async getTemplateById(id: string): Promise<AssetConditionChecklistTemplateDto> {
    try {
      const response = await apiService.get(`${BASE_URL}/templates/${id}`);
      return response;
    } catch (error) {
      console.error(`Error fetching template ${id}:`, error);
      throw error;
    }
  }

  async getTemplatesByAssetCategory(assetCategoryId: string): Promise<AssetConditionChecklistTemplateDto[]> {
    try {
      const response = await apiService.get(`${BASE_URL}/templates/by-asset-category/${assetCategoryId}`);
      return response;
    } catch (error) {
      console.error(`Error fetching templates for asset category ${assetCategoryId}:`, error);
      throw error;
    }
  }

  async getDefaultTemplateForAssetCategory(assetCategoryId: string): Promise<AssetConditionChecklistTemplateDto | null> {
    try {
      const response = await apiService.get(`${BASE_URL}/templates/default/${assetCategoryId}`);
      return response;
    } catch (error) {
      console.error(`Error fetching default template for asset category ${assetCategoryId}:`, error);
      return null;
    }
  }

  async createTemplate(dto: CreateAssetConditionTemplateDto): Promise<AssetConditionChecklistTemplateDto> {
    try {
      const response = await apiService.post(`${BASE_URL}/templates`, dto);
      return response;
    } catch (error) {
      console.error('Error creating template:', error);
      throw error;
    }
  }

  async updateTemplate(id: string, dto: UpdateAssetConditionTemplateDto): Promise<AssetConditionChecklistTemplateDto> {
    try {
      const response = await apiService.put(`${BASE_URL}/templates/${id}`, dto);
      return response;
    } catch (error) {
      console.error(`Error updating template ${id}:`, error);
      throw error;
    }
  }

  async deleteTemplate(id: string): Promise<void> {
    try {
      await apiService.delete(`${BASE_URL}/templates/${id}`);
    } catch (error) {
      console.error(`Error deleting template ${id}:`, error);
      throw error;
    }
  }

  // Record endpoints
  async getAllRecords(page = 1, pageSize = 20): Promise<AssetConditionRecordSummaryDto[]> {
    try {
      const response = await apiService.get(`${BASE_URL}/records?page=${page}&pageSize=${pageSize}`);
      return response;
    } catch (error) {
      console.error('Error fetching condition records:', error);
      throw error;
    }
  }

  async getRecordById(id: string): Promise<AssetConditionRecordDto> {
    try {
      const response = await apiService.get(`${BASE_URL}/records/${id}`);
      return response;
    } catch (error) {
      console.error(`Error fetching record ${id}:`, error);
      throw error;
    }
  }

  async getRecordsByAsset(assetId: string): Promise<AssetConditionRecordSummaryDto[]> {
    try {
      const response = await apiService.get(`${BASE_URL}/records/by-asset/${assetId}`);
      return response;
    } catch (error) {
      console.error(`Error fetching records for asset ${assetId}:`, error);
      throw error;
    }
  }

  async getAdmissionRecordForAdmission(admissionId: string): Promise<AssetConditionRecordDto | null> {
    try {
      // Use silentGet to avoid console errors for expected 404s
      const response = await apiService.silentGet<AssetConditionRecordDto>(`${BASE_URL}/records/by-admission/${admissionId}`);
      return response;
    } catch (error: any) {
      // 404 is expected when no admission record exists yet
      if (error?.status !== 404) {
        console.error(`Error fetching admission record for admission ${admissionId}:`, error);
      }
      return null;
    }
  }

  async getDischargeRecordForAdmission(admissionId: string): Promise<AssetConditionRecordDto | null> {
    try {
      // Use silentGet to avoid console errors for expected 404s
      const response = await apiService.silentGet<AssetConditionRecordDto>(`${BASE_URL}/records/discharge-by-admission/${admissionId}`);
      return response;
    } catch (error: any) {
      // 404 is expected when no discharge record exists yet
      if (error?.status !== 404) {
        console.error(`Error fetching discharge record for admission ${admissionId}:`, error);
      }
      return null;
    }
  }

  async getAdmissionRecordForJobCard(jobCardId: string): Promise<AssetConditionRecordDto | null> {
    try {
      const response = await apiService.silentGet<AssetConditionRecordDto>(`${BASE_URL}/records/by-job-card/${jobCardId}`);
      return response;
    } catch (error: any) {
      if (error?.status !== 404) {
        console.error(`Error fetching admission record for job card ${jobCardId}:`, error);
      }
      return null;
    }
  }

  async getDischargeRecordForJobCard(jobCardId: string): Promise<AssetConditionRecordDto | null> {
    try {
      const response = await apiService.silentGet<AssetConditionRecordDto>(`${BASE_URL}/records/discharge-by-job-card/${jobCardId}`);
      return response;
    } catch (error: any) {
      if (error?.status !== 404) {
        console.error(`Error fetching discharge record for job card ${jobCardId}:`, error);
      }
      return null;
    }
  }

  async startConditionInspection(dto: CreateAssetConditionRecordDto): Promise<AssetConditionRecordDto> {
    try {
      const response = await apiService.post(`${BASE_URL}/records/start`, dto);
      return response;
    } catch (error) {
      console.error('Error starting condition inspection:', error);
      throw error;
    }
  }

  async submitItemResult(recordId: string, dto: SubmitAssetConditionItemDto): Promise<AssetConditionItemResultDto> {
    try {
      const response = await apiService.post(`${BASE_URL}/records/${recordId}/items`, dto);
      return response;
    } catch (error) {
      console.error(`Error submitting item result for record ${recordId}:`, error);
      throw error;
    }
  }

  async completeInspection(recordId: string, dto: CompleteAssetConditionRecordDto): Promise<AssetConditionRecordDto> {
    try {
      const response = await apiService.post(`${BASE_URL}/records/${recordId}/complete`, dto);
      return response;
    } catch (error) {
      console.error(`Error completing inspection ${recordId}:`, error);
      throw error;
    }
  }

  async cancelInspection(recordId: string): Promise<void> {
    try {
      await apiService.post(`${BASE_URL}/records/${recordId}/cancel`, {});
    } catch (error) {
      console.error(`Error cancelling inspection ${recordId}:`, error);
      throw error;
    }
  }

  async linkToAdmission(recordId: string, admissionId: string): Promise<void> {
    try {
      await apiService.post(`${BASE_URL}/records/${recordId}/link-admission/${admissionId}`, {});
    } catch (error) {
      console.error(`Error linking record ${recordId} to admission ${admissionId}:`, error);
      throw error;
    }
  }

  async linkToDischarge(recordId: string, dischargeId: string): Promise<void> {
    try {
      await apiService.post(`${BASE_URL}/records/${recordId}/link-discharge/${dischargeId}`, {});
    } catch (error) {
      console.error(`Error linking record ${recordId} to discharge ${dischargeId}:`, error);
      throw error;
    }
  }

  async uploadItemPhoto(recordId: string, itemResultId: string, photoPath: string): Promise<AssetConditionRecordDto> {
    try {
      const response = await apiService.post(`${BASE_URL}/records/${recordId}/items/${itemResultId}/photos`, { photoPath });
      return response;
    } catch (error) {
      console.error(`Error uploading photo for item ${itemResultId}:`, error);
      throw error;
    }
  }
}

export const assetConditionService = new AssetConditionService();
export default assetConditionService;
