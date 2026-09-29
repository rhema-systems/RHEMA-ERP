'use client';

import React, { useEffect, useId, useState } from 'react';
import axios from 'axios';
import { X } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { inventoryManagementService, type PhysicalCountCounterOptionDto } from '@/services/inventoryManagementService';

export const counterErrorMessage = (error: unknown, fallback: string) => {
  const data = axios.isAxiosError(error) ? error.response?.data : undefined;
  const message = (typeof data === 'string' && data.trim()) || data?.detail || data?.message || data?.title || (error instanceof Error ? error.message : fallback);
  const code = data?.code || data?.extensions?.code;
  return code ? `${message} (${code})` : message;
};

type Props = {
  warehouseId: string;
  locationId?: string;
  selected: PhysicalCountCounterOptionDto[];
  onChange: (employees: PhysicalCountCounterOptionDto[]) => void;
  disabled?: boolean;
};

export function PhysicalCountCounterSelector({ warehouseId, locationId, selected, onChange, disabled = false }: Props) {
  const searchId = useId();
  const [search, setSearch] = useState('');
  const [options, setOptions] = useState<PhysicalCountCounterOptionDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reload, setReload] = useState(0);

  useEffect(() => {
    let cancelled = false;
    setOptions([]);
    setError(null);
    if (!warehouseId || disabled) { setLoading(false); return; }
    setLoading(true);
    const timer = window.setTimeout(() => {
      inventoryManagementService.getPhysicalCountCounterOptions(warehouseId, search, locationId)
        .then(rows => { if (!cancelled) setOptions(rows); })
        .catch(reason => { if (!cancelled) setError(counterErrorMessage(reason, 'Could not load eligible employees.')); })
        .finally(() => { if (!cancelled) setLoading(false); });
    }, 250);
    return () => { cancelled = true; window.clearTimeout(timer); };
  }, [warehouseId, locationId, search, disabled, reload]);

  const remove = (employeeId: string) => onChange(selected.filter(employee => employee.employeeId !== employeeId));
  return <div className="space-y-2">
    <div className="flex items-center justify-between"><Label htmlFor={searchId}>Counters / counting committee</Label>
      <span className="text-xs text-muted-foreground">{selected.length} / 100 selected</span></div>
    {selected.length > 0 && <ul aria-label="Selected counters" className="flex flex-wrap gap-2">
      {selected.map(employee => <li key={employee.employeeId}><Badge variant="secondary" className="gap-1 py-1">
        {employee.employeeName} <span className="font-normal">({employee.employeeNumber})</span>
        <button type="button" disabled={disabled} aria-label={`Remove ${employee.employeeName}`} className="rounded p-0.5 hover:bg-muted disabled:opacity-50"
          onClick={() => remove(employee.employeeId)}><X className="h-3.5 w-3.5" /></button>
      </Badge></li>)}
    </ul>}
    <Input id={searchId} placeholder="Search employee name or number" value={search} maxLength={100}
      disabled={disabled || !warehouseId} onChange={event => setSearch(event.target.value)} />
    {selected.length >= 100 && <p className="text-xs text-muted-foreground">The committee can contain up to 100 employees.</p>}
    {!warehouseId ? <p className="text-sm text-muted-foreground">Select a warehouse to find counters.</p>
      : disabled ? null
      : error ? <div role="alert" className="flex items-center gap-2 text-sm text-red-600"><span>{error}</span>
        <Button type="button" size="sm" variant="outline" onClick={() => setReload(value => value + 1)}>Retry employee search</Button></div>
      : loading ? <p role="status" className="text-sm text-muted-foreground">Loading employees…</p>
      : <div className="max-h-44 overflow-y-auto rounded-md border">
        {options.length === 0 ? <p className="p-3 text-sm text-muted-foreground">No employees found.</p> :
          options.map(employee => <label key={employee.employeeId} className="flex cursor-pointer items-start gap-2 border-b p-2 last:border-0">
            <Checkbox className="mt-0.5" aria-label={`Select ${employee.employeeName} (${employee.employeeNumber})`}
              disabled={disabled || !employee.canAssign || (selected.length >= 100 && !selected.some(value => value.employeeId === employee.employeeId))} checked={selected.some(value => value.employeeId === employee.employeeId)}
              onCheckedChange={checked => {
                if (checked === true && employee.canAssign && selected.length < 100 && !selected.some(value => value.employeeId === employee.employeeId))
                  onChange([...selected, employee]);
                else if (checked === false) remove(employee.employeeId);
              }} />
            <span className="min-w-0 text-sm"><span className="font-medium">{employee.employeeName}</span> · {employee.employeeNumber}
              {!employee.canAssign && <span className="block text-xs text-muted-foreground">{employee.ineligibilityReason || 'This employee cannot be assigned.'}</span>}
            </span>
          </label>)}
      </div>}
  </div>;
}
