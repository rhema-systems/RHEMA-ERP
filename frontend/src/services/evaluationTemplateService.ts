const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

export interface EvaluationTemplateCriterion {
  id: string;
  evaluationTemplateId: string;
  evaluationCriterionId: string;
  criterionName: string;
  criterionCode: string;
  category: string;
  criterionDescription?: string;
  weight: number;
  maxScore: number;
  isMandatory: boolean;
  minimumScore?: number;
  displayOrder: number;
}

export interface EvaluationTemplate {
  id: string;
  templateName: string;
  templateCode: string;
  description?: string;
  category: string;
  tenderType: string;
  isDefault: boolean;
  isActive: boolean;
  passingScore: number;
  scoringMethod: string;
  displayOrder: number;
  // QCBS Configuration (used when scoringMethod = "QCBS")
  technicalWeight: number;
  financialWeight: number;
  minimumTechnicalScore: number;
  createdAt: string;
  createdByName?: string;
  criteriaCount: number;
  totalWeight: number;
  criteria: EvaluationTemplateCriterion[];
}

export interface EvaluationTemplateListItem {
  id: string;
  templateName: string;
  templateCode: string;
  category: string;
  tenderType: string;
  isDefault: boolean;
  criteriaCount: number;
}

export interface CreateEvaluationTemplateCriterionDto {
  evaluationCriterionId: string;
  weight: number;
  maxScore: number;
  isMandatory: boolean;
  minimumScore?: number;
  displayOrder: number;
}

export interface CreateEvaluationTemplateDto {
  templateName: string;
  templateCode: string;
  description?: string;
  category: string;
  tenderType: string;
  isDefault: boolean;
  isActive: boolean;
  passingScore: number;
  scoringMethod: string;
  displayOrder: number;
  // QCBS Configuration (used when scoringMethod = "QCBS")
  technicalWeight: number;
  financialWeight: number;
  minimumTechnicalScore: number;
  criteria: CreateEvaluationTemplateCriterionDto[];
}

export interface UpdateEvaluationTemplateDto {
  templateName: string;
  description?: string;
  category: string;
  tenderType: string;
  isDefault: boolean;
  isActive: boolean;
  passingScore: number;
  scoringMethod: string;
  displayOrder: number;
  // QCBS Configuration (used when scoringMethod = "QCBS")
  technicalWeight: number;
  financialWeight: number;
  minimumTechnicalScore: number;
  criteria: CreateEvaluationTemplateCriterionDto[];
}

const getAuthHeaders = () => {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token && { 'Authorization': `Bearer ${token}` })
  };
};

export const evaluationTemplateService = {
  async getAll(): Promise<EvaluationTemplate[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/EvaluationTemplates`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      throw new Error('Failed to fetch evaluation templates');
    }
    return response.json();
  },

  async getActive(): Promise<EvaluationTemplate[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/EvaluationTemplates/active`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      throw new Error('Failed to fetch active evaluation templates');
    }
    return response.json();
  },

  async getForDropdown(): Promise<EvaluationTemplateListItem[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/EvaluationTemplates/dropdown`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      throw new Error('Failed to fetch evaluation templates for dropdown');
    }
    return response.json();
  },

  async getById(id: string): Promise<EvaluationTemplate> {
    const response = await fetch(`${API_BASE_URL}/procurement/EvaluationTemplates/${id}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      throw new Error('Failed to fetch evaluation template');
    }
    return response.json();
  },

  async getByCategory(category: string): Promise<EvaluationTemplate[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/EvaluationTemplates/category/${category}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      throw new Error('Failed to fetch evaluation templates by category');
    }
    return response.json();
  },

  async getByTenderType(tenderType: string): Promise<EvaluationTemplate[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/EvaluationTemplates/tender-type/${tenderType}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      throw new Error('Failed to fetch evaluation templates by tender type');
    }
    return response.json();
  },

  async getDefault(category: string, tenderType: string): Promise<EvaluationTemplate | null> {
    const response = await fetch(
      `${API_BASE_URL}/procurement/EvaluationTemplates/default?category=${encodeURIComponent(category)}&tenderType=${encodeURIComponent(tenderType)}`,
      { headers: getAuthHeaders() }
    );
    if (response.status === 404) {
      return null;
    }
    if (!response.ok) {
      throw new Error('Failed to fetch default evaluation template');
    }
    return response.json();
  },

  async create(data: CreateEvaluationTemplateDto): Promise<EvaluationTemplate> {
    const response = await fetch(`${API_BASE_URL}/procurement/EvaluationTemplates`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to create evaluation template');
    }
    return response.json();
  },

  async update(id: string, data: UpdateEvaluationTemplateDto): Promise<EvaluationTemplate> {
    const response = await fetch(`${API_BASE_URL}/procurement/EvaluationTemplates/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to update evaluation template');
    }
    return response.json();
  },

  async delete(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/EvaluationTemplates/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      const error = await response.text();
      throw new Error(error || 'Failed to delete evaluation template');
    }
  },

  async validateWeights(id: string): Promise<{ isValid: boolean; message: string }> {
    const response = await fetch(`${API_BASE_URL}/procurement/EvaluationTemplates/${id}/validate-weights`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      throw new Error('Failed to validate evaluation template weights');
    }
    return response.json();
  },
};

