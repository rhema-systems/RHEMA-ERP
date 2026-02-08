/**
 * useFieldLabels Hook
 * React hook for fetching and using configurable field labels
 */

import { useState, useEffect, useCallback } from 'react';
import { fieldLabelService, DEFAULT_INVENTORY_ITEM_LABELS } from '@/services/fieldLabelService';

export interface UseFieldLabelsResult {
  labels: Record<string, string>;
  loading: boolean;
  error: string | null;
  getLabel: (fieldName: string) => string;
  refreshLabels: () => Promise<void>;
}

/**
 * Hook to fetch and use field labels for a specific module
 * @param module - The module name (e.g., 'InventoryItem')
 * @returns Object containing labels, loading state, error, and helper functions
 */
export function useFieldLabels(module: string): UseFieldLabelsResult {
  const [labels, setLabels] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchLabels = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const fetchedLabels = await fieldLabelService.getFieldLabels(module);
      setLabels(fetchedLabels);
    } catch (err) {
      console.error(`Error fetching field labels for ${module}:`, err);
      setError(`Failed to load field labels for ${module}`);
      // Set defaults for InventoryItem module
      if (module === 'InventoryItem') {
        setLabels(DEFAULT_INVENTORY_ITEM_LABELS);
      }
    } finally {
      setLoading(false);
    }
  }, [module]);

  useEffect(() => {
    fetchLabels();
  }, [fetchLabels]);

  const getLabel = useCallback((fieldName: string): string => {
    return labels[fieldName] || fieldName;
  }, [labels]);

  const refreshLabels = useCallback(async () => {
    fieldLabelService.clearCache(module);
    await fetchLabels();
  }, [module, fetchLabels]);

  return {
    labels,
    loading,
    error,
    getLabel,
    refreshLabels
  };
}

/**
 * Hook specifically for inventory item field labels
 * @returns Object containing labels, loading state, error, and helper functions
 */
export function useInventoryItemLabels(): UseFieldLabelsResult {
  return useFieldLabels('InventoryItem');
}

export default useFieldLabels;
