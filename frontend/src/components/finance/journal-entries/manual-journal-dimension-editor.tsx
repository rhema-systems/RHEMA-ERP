'use client';

import { AlertCircle, Copy, RotateCcw, SlidersHorizontal } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { ScrollArea } from '@/components/ui/scroll-area';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Sheet,
  SheetClose,
  SheetContent,
  SheetDescription,
  SheetFooter,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/sheet';
import {
  getActiveManualDimensionValues,
  getApplicableManualDimensionRules,
  getManualDimensionSummary,
  resolveManualDimensionValues,
} from '@/lib/finance/manual-journal-dimensions';
import type {
  FinanceDimensionAccountRule,
  FinanceDimensionDefinition,
} from '@/types/finance';

interface DimensionContext {
  definitions: FinanceDimensionDefinition[];
  rules: FinanceDimensionAccountRule[];
  effectiveDate: string;
}

interface ManualJournalDimensionDefaultsProps extends Omit<DimensionContext, 'rules'> {
  values: Record<string, string>;
  onChange: (values: Record<string, string>) => void;
  onApplyToAll: () => void;
  disabled?: boolean;
}

const setDimensionValue = (
  values: Record<string, string>,
  code: string,
  value: string
) => {
  const next = { ...values };
  if (value === '__none__') delete next[code];
  else next[code] = value;
  return next;
};

export function ManualJournalDimensionDefaults({
  definitions,
  effectiveDate,
  values,
  onChange,
  onApplyToAll,
  disabled = false,
}: ManualJournalDimensionDefaultsProps) {
  if (definitions.length === 0) return null;

  return (
    <div className="mb-5 rounded-lg border bg-muted/20 p-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h3 className="font-medium">Default coding dimensions</h3>
          <p className="text-sm text-muted-foreground">
            New lines inherit these values. Account-specific fixed and
            prohibited rules still take precedence.
          </p>
        </div>
        <div className="flex gap-2">
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={() => onChange({})}
            disabled={disabled || Object.keys(values).length === 0}
          >
            Clear
          </Button>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={onApplyToAll}
            disabled={disabled}
          >
            Apply to all lines
          </Button>
        </div>
      </div>
      <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {[...definitions]
          .sort((left, right) => left.displayOrder - right.displayOrder)
          .map((dimension) => (
            <div key={dimension.id} className="space-y-1.5">
              <Label className="text-xs">{dimension.name}</Label>
              <Select
                value={values[dimension.code] || '__none__'}
                onValueChange={(value) =>
                  onChange(setDimensionValue(values, dimension.code, value))
                }
                disabled={disabled}
              >
                <SelectTrigger className="h-9 bg-background">
                  <SelectValue placeholder="Not assigned" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__none__">Not assigned</SelectItem>
                  {getActiveManualDimensionValues(dimension, effectiveDate).map(
                    (value) => (
                      <SelectItem key={value.id} value={value.code}>
                        {value.code} — {value.name}
                      </SelectItem>
                    )
                  )}
                </SelectContent>
              </Select>
            </div>
          ))}
      </div>
    </div>
  );
}

interface ManualJournalDimensionCellProps extends DimensionContext {
  lineNumber: number;
  accountId: string;
  accountLabel?: string;
  values: Record<string, string>;
  defaults: Record<string, string>;
  previousValues?: Record<string, string>;
  onChange: (values: Record<string, string>) => void;
  onApplyToAll: (values: Record<string, string>) => void;
  loading?: boolean;
}

export function ManualJournalDimensionCell({
  definitions,
  rules,
  effectiveDate,
  lineNumber,
  accountId,
  accountLabel,
  values,
  defaults,
  previousValues,
  onChange,
  onApplyToAll,
  loading = false,
}: ManualJournalDimensionCellProps) {
  if (loading)
    return <span className="text-xs text-muted-foreground">Loading…</span>;
  if (definitions.length === 0)
    return (
      <span className="text-xs text-muted-foreground">None configured</span>
    );
  if (!accountId)
    return (
      <span className="text-xs text-muted-foreground">
        Select account first
      </span>
    );

  const applicableRules = getApplicableManualDimensionRules(
    rules,
    accountId,
    effectiveDate
  );
  const visibleDefinitions = [...definitions]
    .sort((left, right) => left.displayOrder - right.displayOrder)
    .filter(
      (dimension) =>
        applicableRules.find(
          (rule) => rule.financeDimensionDefinitionId === dimension.id
        )?.ruleType !== 'Prohibited'
    );
  const governedDefinitions = visibleDefinitions.filter((dimension) => {
    const type = applicableRules.find(
      (rule) => rule.financeDimensionDefinitionId === dimension.id
    )?.ruleType;
    return type === 'Required' || type === 'Fixed';
  });
  const optionalDefinitions = visibleDefinitions.filter(
    (dimension) => !governedDefinitions.includes(dimension)
  );
  const missingRequired = applicableRules.filter(
    (rule) =>
      rule.ruleType === 'Required' &&
      !rule.defaultValueCode &&
      !values[rule.dimensionCode]
  );

  const renderDimension = (dimension: FinanceDimensionDefinition) => {
    const rule = applicableRules.find(
      (item) => item.financeDimensionDefinitionId === dimension.id
    );
    const isFixed = rule?.ruleType === 'Fixed';
    return (
      <div key={dimension.id} className="space-y-1.5">
        <div className="flex items-center justify-between gap-2">
          <Label>
            {dimension.name}
            {rule?.ruleType === 'Required' ? ' *' : ''}
          </Label>
          {isFixed && <Badge variant="secondary">Fixed</Badge>}
        </div>
        <Select
          value={values[dimension.code] || '__none__'}
          onValueChange={(value) =>
            onChange(setDimensionValue(values, dimension.code, value))
          }
          disabled={isFixed}
        >
          <SelectTrigger>
            <SelectValue placeholder="Not assigned" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="__none__">Not assigned</SelectItem>
            {getActiveManualDimensionValues(dimension, effectiveDate).map(
              (value) => (
                <SelectItem key={value.id} value={value.code}>
                  {value.code} — {value.name}
                </SelectItem>
              )
            )}
          </SelectContent>
        </Select>
      </div>
    );
  };

  return (
    <Sheet>
      <SheetTrigger asChild>
        <Button
          type="button"
          variant="outline"
          size="sm"
          className={`h-auto min-h-9 w-full justify-start px-2.5 py-2 font-normal ${missingRequired.length > 0 ? 'border-destructive text-destructive' : ''}`}
        >
          {missingRequired.length > 0 ? (
            <AlertCircle className="mr-2 h-4 w-4 shrink-0" />
          ) : (
            <SlidersHorizontal className="mr-2 h-4 w-4 shrink-0" />
          )}
          <span className="truncate">
            {getManualDimensionSummary(definitions, values)}
          </span>
          {missingRequired.length > 0 && (
            <Badge variant="destructive" className="ml-auto">
              {missingRequired.length}
            </Badge>
          )}
        </Button>
      </SheetTrigger>
      <SheetContent side="right" className="flex w-full flex-col sm:max-w-lg">
        <SheetHeader>
          <SheetTitle>Line {lineNumber} coding dimensions</SheetTitle>
          <SheetDescription>
            {accountLabel || 'Journal line'}. Required and fixed dimensions are
            shown first.
          </SheetDescription>
        </SheetHeader>

        <ScrollArea className="-mx-2 mt-4 flex-1 px-2">
          <div className="space-y-6 pb-6">
            {missingRequired.length > 0 && (
              <div className="rounded-md border border-destructive/40 bg-destructive/5 p-3 text-sm text-destructive">
                Complete{' '}
                {missingRequired.map((rule) => rule.dimensionName).join(', ')}{' '}
                before saving.
              </div>
            )}

            {governedDefinitions.length > 0 && (
              <section className="space-y-4">
                <div>
                  <h3 className="font-medium">Required and fixed</h3>
                  <p className="text-xs text-muted-foreground">
                    These values are governed by the selected account.
                  </p>
                </div>
                {governedDefinitions.map(renderDimension)}
              </section>
            )}

            {optionalDefinitions.length > 0 && (
              <details
                className="group"
                open={governedDefinitions.length === 0}
              >
                <summary className="cursor-pointer select-none font-medium">
                  Optional dimensions ({optionalDefinitions.length})
                </summary>
                <div className="mt-4 space-y-4">
                  {optionalDefinitions.map(renderDimension)}
                </div>
              </details>
            )}
          </div>
        </ScrollArea>

        <div className="border-t pt-4">
          <div className="grid gap-2 sm:grid-cols-2">
            <Button
              type="button"
              variant="outline"
              onClick={() =>
                onChange(
                  resolveManualDimensionValues(
                    definitions,
                    rules,
                    accountId,
                    effectiveDate,
                    defaults
                  )
                )
              }
            >
              <RotateCcw className="mr-2 h-4 w-4" /> Use defaults
            </Button>
            <Button
              type="button"
              variant="outline"
              disabled={!previousValues}
              onClick={() =>
                previousValues &&
                onChange(
                  resolveManualDimensionValues(
                    definitions,
                    rules,
                    accountId,
                    effectiveDate,
                    previousValues
                  )
                )
              }
            >
              <Copy className="mr-2 h-4 w-4" /> Copy previous line
            </Button>
            <Button
              type="button"
              variant="outline"
              className="sm:col-span-2"
              onClick={() => onApplyToAll(values)}
            >
              Apply this coding to all lines
            </Button>
          </div>
          <SheetFooter className="mt-3">
            <SheetClose asChild>
              <Button type="button">Done</Button>
            </SheetClose>
          </SheetFooter>
        </div>
      </SheetContent>
    </Sheet>
  );
}
