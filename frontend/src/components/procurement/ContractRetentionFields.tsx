'use client';

import React from 'react';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';

export const DEFAULT_CONTRACT_RETENTION_PERCENTAGE = 0;

export function validateContractRetention(percentage: number, clause?: string | null): string | null {
  if (!Number.isFinite(percentage) || percentage < 0 || percentage > 100) {
    return 'Retention percentage must be between 0 and 100.';
  }
  if ((clause?.length ?? 0) > 2000) return 'Retention clause must not exceed 2000 characters.';
  if (percentage > 0 && !clause?.trim()) {
    return 'Enter the retention clause, including deduction and release conditions.';
  }
  return null;
}

export function ContractRetentionFields({
  percentage, clause = '', onPercentageChange, onClauseChange,
}: {
  percentage: number;
  clause?: string | null;
  onPercentageChange: (value: number) => void;
  onClauseChange: (value: string) => void;
}) {
  const clauseText = clause ?? '';
  return (
    <div className="space-y-3">
      <div className="space-y-1">
        <Label htmlFor="contract-retention-percentage">Retention %</Label>
        <Input id="contract-retention-percentage" type="number" min="0" max="100" step="0.01"
          value={percentage} aria-describedby="contract-retention-help"
          onChange={(event) => onPercentageChange(Number(event.target.value))} />
        <p id="contract-retention-help" className="text-xs text-muted-foreground">
          Use the approved contract terms. Enter 0 when retention does not apply.
        </p>
      </div>
      {(percentage > 0 || clauseText.length > 0) && (
        <div className="space-y-1">
          <Label htmlFor="contract-retention-clause">Retention clause and release conditions{percentage > 0 ? ' *' : ''}</Label>
          <Textarea id="contract-retention-clause" rows={3} maxLength={2000} required={percentage > 0}
            value={clauseText} onChange={(event) => onClauseChange(event.target.value)}
            placeholder="State how retention is deducted and the conditions for its release." />
        </div>
      )}
    </div>
  );
}
