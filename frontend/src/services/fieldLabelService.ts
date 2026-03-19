/**
 * Field Label Service
 * API service for configurable field labels
 */

import axios from 'axios';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

export interface FieldLabelsDto {
  module: string;
  labels: Record<string, string>;
}

export interface FieldLabelDto {
  module: string;
  fieldName: string;
  label: string;
}

export interface UpdateFieldLabelsRequest {
  labels: Record<string, string>;
}

export interface UpdateFieldLabelRequest {
  label: string;
}

// Default field labels for inventory items
export const DEFAULT_INVENTORY_ITEM_LABELS: Record<string, string> = {
  Brand: 'Brand',
  Manufacturer: 'Manufacturer',
  Style: 'Style',
  Feature: 'Feature',
};

const getAuthHeaders = () => {
  const token = typeof window !== 'undefined'
    ? (localStorage.getItem('token') || localStorage.getItem('authToken'))
    : null;
  return token ? { Authorization: `Bearer ${token}` } : {};
};

// Cache for field labels
const labelCache: Map<string, Record<string, string>> = new Map();

export const fieldLabelService = {
  /**
   * Get all field labels for a specific module
   */
  async getFieldLabels(module: string): Promise<Record<string, string>> {
    // Check cache first
    if (labelCache.has(module)) {
      return labelCache.get(module) ?? {};
    }

    try {
      const response = await axios.get<FieldLabelsDto>(`${API_URL}/settings/field-labels/${module}`, { headers: getAuthHeaders() });
      const labels = response.data.labels;
      labelCache.set(module, labels);
      return labels;
    } catch (error) {
      console.error(`Error fetching field labels for ${module}:`, error);
      // Return defaults for InventoryItem module
      if (module === 'InventoryItem') {
        return DEFAULT_INVENTORY_ITEM_LABELS;
      }
      return {};
    }
  },

  /**
   * Get a single field label
   */
  async getFieldLabel(module: string, fieldName: string): Promise<string> {
    try {
      const labels = await this.getFieldLabels(module);
      return labels[fieldName] || fieldName;
    } catch (error) {
      console.error(`Error fetching field label ${module}:${fieldName}:`, error);
      return fieldName;
    }
  },

  /**
   * Update all field labels for a module
   */
  async updateFieldLabels(module: string, labels: Record<string, string>): Promise<Record<string, string>> {
    try {
      const response = await axios.put<FieldLabelsDto>(`${API_URL}/settings/field-labels/${module}`, { labels }, { headers: getAuthHeaders() });
      const updatedLabels = response.data.labels;
      labelCache.set(module, updatedLabels);
      return updatedLabels;
    } catch (error) {
      console.error(`Error updating field labels for ${module}:`, error);
      throw error;
    }
  },

  /**
   * Update a single field label
   */
  async updateFieldLabel(module: string, fieldName: string, label: string): Promise<string> {
    try {
      const response = await axios.put<FieldLabelDto>(`${API_URL}/settings/field-labels/${module}/${fieldName}`, { label }, { headers: getAuthHeaders() });
      // Invalidate cache
      labelCache.delete(module);
      return response.data.label;
    } catch (error) {
      console.error(`Error updating field label ${module}:${fieldName}:`, error);
      throw error;
    }
  },

  /**
   * Clear the cache for a specific module or all modules
   */
  clearCache(module?: string): void {
    if (module) {
      labelCache.delete(module);
    } else {
      labelCache.clear();
    }
  },

  /**
   * Get inventory item field labels (convenience method)
   */
  async getInventoryItemLabels(): Promise<Record<string, string>> {
    return this.getFieldLabels('InventoryItem');
  },

  /**
   * Update inventory item field labels (convenience method)
   */
  async updateInventoryItemLabels(labels: Record<string, string>): Promise<Record<string, string>> {
    return this.updateFieldLabels('InventoryItem', labels);
  }
};

export default fieldLabelService;
