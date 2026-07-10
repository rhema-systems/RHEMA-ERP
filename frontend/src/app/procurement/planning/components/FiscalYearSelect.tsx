'use client';

import { useEffect, useMemo, useState } from 'react';

import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { financeDataService } from '@/services/finance/finance-data.service';
import type { FiscalYear } from '@/types/finance';

interface FiscalYearSelectProps {
  value: number;
  onValueChange: (year: number) => void;
  disabled?: boolean;
  triggerClassName?: string;
  placeholder?: string;
  autoSelectFirstAvailable?: boolean;
}

const manualValuePrefix = 'manual-year-';

const isWithinFiscalYear = (fiscalYear: FiscalYear, timestamp: number) => {
  const start = Date.parse(fiscalYear.startDate);
  const end = Date.parse(fiscalYear.endDate);

  return !Number.isNaN(start) && !Number.isNaN(end) && start <= timestamp && timestamp <= end;
};

const formatDate = (value?: string) => {
  if (!value) return '';

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return '';

  return parsed.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
};

const formatFiscalYearLabel = (fiscalYear: FiscalYear) => {
  const code = fiscalYear.fiscalYearCode?.trim();
  const name = fiscalYear.fiscalYearName?.trim();

  if (code && name && code !== name) return `${code} - ${name}`;
  return code || name || fiscalYear.year.toString();
};

export function FiscalYearSelect({
  value,
  onValueChange,
  disabled = false,
  triggerClassName,
  placeholder = 'Select fiscal year',
  autoSelectFirstAvailable = false,
}: FiscalYearSelectProps) {
  const [fiscalYears, setFiscalYears] = useState<FiscalYear[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadFailed, setLoadFailed] = useState(false);

  useEffect(() => {
    let isMounted = true;

    const loadFiscalYears = async () => {
      try {
        setLoading(true);
        setLoadFailed(false);
        const data = await financeDataService.getFiscalYears();
        if (isMounted) setFiscalYears(data);
      } catch (error) {
        console.error('Error loading fiscal years:', error);
        if (isMounted) {
          setLoadFailed(true);
          setFiscalYears([]);
        }
      } finally {
        if (isMounted) setLoading(false);
      }
    };

    loadFiscalYears();

    return () => {
      isMounted = false;
    };
  }, []);

  const sortedFiscalYears = useMemo(
    () => [...fiscalYears].sort((a, b) => b.year - a.year || a.fiscalYearName.localeCompare(b.fiscalYearName)),
    [fiscalYears]
  );

  const selectedFiscalYear = useMemo(
    () => sortedFiscalYears.find((fiscalYear) => fiscalYear.year === value),
    [sortedFiscalYears, value]
  );

  const preferredFiscalYear = useMemo(() => {
    const now = Date.now();

    return (
      sortedFiscalYears.find((fiscalYear) => !fiscalYear.isClosed && !fiscalYear.isLocked && isWithinFiscalYear(fiscalYear, now)) ??
      sortedFiscalYears.find((fiscalYear) => !fiscalYear.isClosed && !fiscalYear.isLocked) ??
      sortedFiscalYears[0]
    );
  }, [sortedFiscalYears]);

  useEffect(() => {
    if (autoSelectFirstAvailable && preferredFiscalYear && !selectedFiscalYear) {
      onValueChange(preferredFiscalYear.year);
    }
  }, [autoSelectFirstAvailable, onValueChange, preferredFiscalYear, selectedFiscalYear]);

  const manualValue = value ? `${manualValuePrefix}${value}` : undefined;
  const selectValue = selectedFiscalYear?.id ?? manualValue;
  const isDisabled = disabled || loading || (sortedFiscalYears.length === 0 && !manualValue);
  let placeholderText = placeholder;
  if (loading) {
    placeholderText = 'Loading fiscal years...';
  } else if (loadFailed) {
    placeholderText = 'Fiscal years unavailable';
  }

  const handleValueChange = (nextValue: string) => {
    if (nextValue.startsWith(manualValuePrefix)) return;

    const fiscalYear = sortedFiscalYears.find((item) => item.id === nextValue);
    if (fiscalYear) onValueChange(fiscalYear.year);
  };

  return (
    <Select value={selectValue} onValueChange={handleValueChange} disabled={isDisabled}>
      <SelectTrigger className={triggerClassName}>
        <SelectValue placeholder={placeholderText} />
      </SelectTrigger>
      <SelectContent>
        {manualValue && !selectedFiscalYear ? (
          <SelectItem value={manualValue}>
            {value} (not configured in Finance)
          </SelectItem>
        ) : null}
        {sortedFiscalYears.map((fiscalYear) => {
          const range = [formatDate(fiscalYear.startDate), formatDate(fiscalYear.endDate)].filter(Boolean).join(' - ');

          return (
            <SelectItem key={fiscalYear.id} value={fiscalYear.id}>
              <div className="flex flex-col">
                <span>{formatFiscalYearLabel(fiscalYear)}</span>
                {range ? <span className="text-xs text-muted-foreground">{range}</span> : null}
              </div>
            </SelectItem>
          );
        })}
      </SelectContent>
    </Select>
  );
}
