'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { AxiosError } from 'axios';
import { RefreshCw } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import {
  filterInventoryTrackingExceptions,
  inventoryTrackingExceptionLabel,
  type InventoryTrackingExceptionContext,
} from '@/lib/inventory-tracking-exception-options';
import {
  inventoryTrackingControlService,
  type InventoryTrackingException,
} from '@/services/inventoryTrackingControlService';

type ProblemDetails = {
  detail?: string;
  message?: string;
  title?: string;
  code?: string;
  extensions?: { code?: string };
};

const NONE = '__none__';

const problemMessage = (error: unknown) => {
  const problem = (error as AxiosError<ProblemDetails>)?.response?.data;
  const message = problem?.detail || problem?.message || problem?.title ||
    (error instanceof Error ? error.message : 'Unable to load approved tracking exceptions.');
  const code = problem?.code || problem?.extensions?.code;
  return code ? `${message} (${code})` : message;
};

export function useAvailableInventoryTrackingExceptions(enabled: boolean) {
  const [exceptions, setExceptions] = useState<InventoryTrackingException[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>();

  const refresh = useCallback(async () => {
    if (!enabled) {
      setExceptions([]);
      setError(undefined);
      return;
    }
    setLoading(true);
    setError(undefined);
    try {
      setExceptions(await inventoryTrackingControlService.getExceptions(500));
    } catch (loadError) {
      setError(problemMessage(loadError));
    } finally {
      setLoading(false);
    }
  }, [enabled]);

  useEffect(() => { void refresh(); }, [refresh]);

  return { exceptions, loading, error, refresh };
}

type InventoryTrackingExceptionSelectProps = {
  value?: string;
  onValueChange: (value?: string) => void;
  exceptions: InventoryTrackingException[];
  context: InventoryTrackingExceptionContext;
  loading?: boolean;
  error?: string;
  onRetry?: () => void;
  disabled?: boolean;
};

export function InventoryTrackingExceptionSelect({
  value,
  onValueChange,
  exceptions,
  context,
  loading = false,
  error,
  onRetry,
  disabled = false,
}: InventoryTrackingExceptionSelectProps) {
  const options = useMemo(
    () => filterInventoryTrackingExceptions(exceptions, context, value),
    [context, exceptions, value]
  );

  return <div className="space-y-1">
    <Select
      value={value || NONE}
      onValueChange={(next) => onValueChange(next === NONE ? undefined : next)}
      disabled={disabled || loading}
    >
      <SelectTrigger>
        <SelectValue placeholder={loading ? 'Loading approved exceptions…' : 'No exception'} />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value={NONE}>No approved exception</SelectItem>
        {options.map((option) => <SelectItem key={option.id} value={option.id}>
          {inventoryTrackingExceptionLabel(option)}{!option.isAvailable ? ' (no longer available)' : ''}
        </SelectItem>)}
      </SelectContent>
    </Select>
    {error && <div className="flex items-start justify-between gap-2 text-xs text-destructive">
      <span>{error}</span>
      {onRetry && <Button type="button" variant="ghost" size="sm" className="h-6 px-2" onClick={onRetry}>
        <RefreshCw className="mr-1 h-3 w-3" />Retry
      </Button>}
    </div>}
    {!loading && !error && options.length === 0 &&
      <p className="text-xs text-muted-foreground">No available approved exception matches this transaction.</p>}
  </div>;
}
