const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || '/api';

export interface EvaluationCriterion {
  id: string;
  criterionName: string;
  criterionCode: string;
  category: string;
  /** Specifies whether this criterion is used for Technical or Financial evaluation in QCBS */
  evaluationType: 'Technical' | 'Financial';
  description?: string;
  maxScore: number;
  weight: number;
  isActive: boolean;
  displayOrder: number;
  createdAt: string;
  createdByName?: string;
}

export interface CreateEvaluationCriterionDto {
  criterionName: string;
  criterionCode: string;
  category: string;
  /** Specifies whether this criterion is used for Technical or Financial evaluation in QCBS */
  evaluationType: 'Technical' | 'Financial';
  description?: string;
  maxScore: number;
  weight: number;
  isActive: boolean;
  displayOrder: number;
}

export interface UpdateEvaluationCriterionDto {
  criterionName: string;
  /** Specifies whether this criterion is used for Technical or Financial evaluation in QCBS */
  evaluationType: 'Technical' | 'Financial';
  description?: string;
  maxScore: number;
  weight: number;
  isActive: boolean;
  displayOrder: number;
}

const getAuthHeaders = () => {
  const token = localStorage.getItem('token') || localStorage.getItem('authToken');
  return {
    'Content-Type': 'application/json',
    ...(token && { 'Authorization': `Bearer ${token}` })
  };
};

export const evaluationCriteriaService = {
  async getAll(): Promise<EvaluationCriterion[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/EvaluationCriteria`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      throw new Error('Failed to fetch evaluation criteria');
    }
    return response.json();
  },

  async getActive(): Promise<EvaluationCriterion[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/EvaluationCriteria/active`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      throw new Error('Failed to fetch active evaluation criteria');
    }
    return response.json();
  },

  async getById(id: string): Promise<EvaluationCriterion> {
    const response = await fetch(`${API_BASE_URL}/procurement/EvaluationCriteria/${id}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      throw new Error('Failed to fetch evaluation criterion');
    }
    return response.json();
  },

  async getByCategory(category: string): Promise<EvaluationCriterion[]> {
    const response = await fetch(`${API_BASE_URL}/procurement/EvaluationCriteria/category/${category}`, {
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      throw new Error('Failed to fetch evaluation criteria by category');
    }
    return response.json();
  },

  async create(data: CreateEvaluationCriterionDto): Promise<EvaluationCriterion> {
    const response = await fetch(`${API_BASE_URL}/procurement/EvaluationCriteria`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.message || 'Failed to create evaluation criterion');
    }
    return response.json();
  },

  async update(id: string, data: UpdateEvaluationCriterionDto): Promise<EvaluationCriterion> {
    const response = await fetch(`${API_BASE_URL}/procurement/EvaluationCriteria/${id}`, {
      method: 'PUT',
      headers: getAuthHeaders(),
      body: JSON.stringify(data),
    });
    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.message || 'Failed to update evaluation criterion');
    }
    return response.json();
  },

  async delete(id: string): Promise<void> {
    const response = await fetch(`${API_BASE_URL}/procurement/EvaluationCriteria/${id}`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });
    if (!response.ok) {
      const error = await response.json();
      throw new Error(error.message || 'Failed to delete evaluation criterion');
    }
  },
};
