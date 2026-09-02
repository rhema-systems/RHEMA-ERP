'use client';

import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import type { DepreciationConvention, DepreciationMethod } from '@/types/fixed-assets';

const descriptions: Record<DepreciationConvention, string> = {
  FullMonth: 'A full month is charged in the placed-in-service month and no charge is taken in the disposal month.',
  MidMonth: 'Half a month is charged in both the placed-in-service and disposal months.',
  HalfYear: 'One half of a fiscal year is charged in the first and disposal fiscal years, using the tenant fiscal calendar.',
  ActualDays: 'The annual charge is prorated by eligible calendar days over the actual days in the tenant fiscal year.',
};

interface Props {
  value: DepreciationConvention;
  method: DepreciationMethod;
  onChange: (value: DepreciationConvention) => void;
  disabled?: boolean;
}

export function DepreciationConventionField({ value, method, onChange, disabled = false }: Props) {
  const productionBased = method === 'UnitsOfProduction';
  return (
    <div className="space-y-2">
      <Label htmlFor="convention">Depreciation Convention</Label>
      <Select value={value} onValueChange={(next) => onChange(next as DepreciationConvention)} disabled={disabled || productionBased}>
        <SelectTrigger id="convention"><SelectValue /></SelectTrigger>
        <SelectContent>
          <SelectItem value="FullMonth">Full Month</SelectItem>
          <SelectItem value="MidMonth">Mid Month</SelectItem>
          <SelectItem value="HalfYear">Half Year</SelectItem>
          <SelectItem value="ActualDays">Actual Days</SelectItem>
        </SelectContent>
      </Select>
      <p className="text-xs text-muted-foreground">
        {productionBased
          ? 'Informational for units of production: verified usage determines the charge, with no time-based proration.'
          : descriptions[value]}
      </p>
      <p className="text-xs text-muted-foreground">
        The method determines the depreciation amount pattern; the convention determines timing at service and disposal boundaries.
      </p>
    </div>
  );
}
