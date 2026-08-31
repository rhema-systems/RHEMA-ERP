'use client';

import { AlertCircle, SlidersHorizontal } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
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
  getActiveDimensionValues,
  getApplicableDimensionRules,
  getDimensionSummary,
  getMissingRequiredDimensions,
  resolveSourceDimensionValues,
  type FinanceDimensionRuleContext,
} from '@/lib/finance/source-document-dimensions';
import type {
  FinanceDimensionAccountRule,
  FinanceDimensionDefinition,
} from '@/types/finance';

const setValue = (
  values: Record<string, string>,
  code: string,
  value: string
) => {
  const next = { ...values };
  if (value === '__none__') delete next[code];
  else next[code] = value;
  return next;
};

export interface TransactionDimensionDefaultsProps {
  definitions: FinanceDimensionDefinition[];
  effectiveDate: string;
  values: Record<string, string>;
  onChange: (values: Record<string, string>) => void;
  onApplyToAll: () => void;
  disabled?: boolean;
}

export function TransactionDimensionDefaults({
  definitions,
  effectiveDate,
  values,
  onChange,
  onApplyToAll,
  disabled = false,
}: TransactionDimensionDefaultsProps) {
  if (definitions.length === 0) return null;
  return (
    <section className="rounded-lg border bg-muted/20 p-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h3 className="font-medium">Default coding dimensions</h3>
          <p className="text-sm text-muted-foreground">
            New lines inherit these defaults. Existing lines change only when
            you apply them explicitly.
          </p>
        </div>
        <div className="flex gap-2">
          <Button
            type="button"
            variant="ghost"
            size="sm"
            disabled={disabled || Object.keys(values).length === 0}
            onClick={() => onChange({})}
          >
            Clear
          </Button>
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={disabled}
            onClick={onApplyToAll}
          >
            Apply to all eligible lines
          </Button>
        </div>
      </div>
      <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {[...definitions]
          .sort((left, right) => left.displayOrder - right.displayOrder)
          .filter((definition) => definition.classification !== 'Derived')
          .map((definition) => (
            <div key={definition.id} className="space-y-1.5">
              <Label className="text-xs">{definition.name}</Label>
              <Select
                value={values[definition.code] || '__none__'}
                disabled={disabled}
                onValueChange={(value) =>
                  onChange(setValue(values, definition.code, value))
                }
              >
                <SelectTrigger className="h-9 bg-background">
                  <SelectValue placeholder="Not assigned" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__none__">Not assigned</SelectItem>
                  {getActiveDimensionValues(definition, effectiveDate).map(
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
    </section>
  );
}

export interface TransactionDimensionLineEditorProps {
  definitions: FinanceDimensionDefinition[];
  rules: FinanceDimensionAccountRule[];
  context: FinanceDimensionRuleContext;
  effectiveDate: string;
  lineNumber: number;
  accountId: string;
  accountLabel?: string;
  values: Record<string, string>;
  defaults: Record<string, string>;
  certificationState?: 'LegacyReadOnly' | 'CaptureOptional' | 'Enforced';
  onChange: (values: Record<string, string>) => void;
  disabled?: boolean;
}

export function TransactionDimensionLineEditor({
  definitions,
  rules,
  context,
  effectiveDate,
  lineNumber,
  accountId,
  accountLabel,
  values,
  defaults,
  certificationState = 'CaptureOptional',
  onChange,
  disabled = false,
}: TransactionDimensionLineEditorProps) {
  if (!accountId)
    return (
      <span className="text-xs text-muted-foreground">
        Select account first
      </span>
    );
  const applicable = getApplicableDimensionRules(
    rules,
    accountId,
    effectiveDate,
    context
  );
  const visible = [...definitions]
    .sort((left, right) => left.displayOrder - right.displayOrder)
    .filter(
      (definition) =>
        definition.classification !== 'Derived' &&
        applicable.find(
          (rule) => rule.financeDimensionDefinitionId === definition.id
        )?.ruleType !== 'Prohibited'
    );
  const missing = getMissingRequiredDimensions(
    rules,
    accountId,
    effectiveDate,
    context,
    values
  );
  const isBlocking = certificationState === 'Enforced' && missing.length > 0;

  return (
    <Sheet>
      <SheetTrigger asChild>
        <Button
          type="button"
          variant="outline"
          size="sm"
          className={`h-auto min-h-9 w-full justify-start px-2.5 py-2 font-normal ${isBlocking ? 'border-destructive text-destructive' : ''}`}
        >
          {isBlocking ? (
            <AlertCircle className="mr-2 h-4 w-4" />
          ) : (
            <SlidersHorizontal className="mr-2 h-4 w-4" />
          )}
          <span className="truncate">
            {getDimensionSummary(definitions, values)}
          </span>
          {missing.length > 0 && (
            <Badge
              variant={isBlocking ? 'destructive' : 'secondary'}
              className="ml-auto"
            >
              {missing.length} {isBlocking ? 'required' : 'warning'}
            </Badge>
          )}
        </Button>
      </SheetTrigger>
      <SheetContent
        side="right"
        className="flex w-full flex-col overflow-y-auto sm:max-w-lg"
      >
        <SheetHeader>
          <SheetTitle>Line {lineNumber} coding dimensions</SheetTitle>
          <SheetDescription>
            {accountLabel || 'Source line'} ·{' '}
            {context.sourceRoute || context.sourceDocumentType}
          </SheetDescription>
        </SheetHeader>
        {missing.length > 0 && (
          <div className="mt-4 rounded-md border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900">
            Missing {missing.map((rule) => rule.dimensionName).join(', ')}.
            {certificationState === 'CaptureOptional'
              ? ' This is a readiness warning until the route is promoted.'
              : ''}
          </div>
        )}
        <div className="mt-5 flex-1 space-y-4">
          {visible.map((definition) => {
            const rule = applicable.find(
              (item) => item.financeDimensionDefinitionId === definition.id
            );
            const fixed = rule?.ruleType === 'Fixed';
            return (
              <div key={definition.id} className="space-y-1.5">
                <div className="flex items-center justify-between">
                  <Label>
                    {definition.name}
                    {rule?.ruleType === 'Required' ? ' *' : ''}
                  </Label>
                  {fixed && <Badge variant="secondary">Fixed</Badge>}
                </div>
                <Select
                  value={values[definition.code] || '__none__'}
                  disabled={disabled || fixed}
                  onValueChange={(value) =>
                    onChange(setValue(values, definition.code, value))
                  }
                >
                  <SelectTrigger>
                    <SelectValue placeholder="Not assigned" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="__none__">Not assigned</SelectItem>
                    {getActiveDimensionValues(definition, effectiveDate).map(
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
          })}
        </div>
        <SheetFooter className="mt-5 border-t pt-4">
          <Button
            type="button"
            variant="outline"
            disabled={disabled}
            onClick={() =>
              onChange(
                resolveSourceDimensionValues(
                  definitions,
                  rules,
                  accountId,
                  effectiveDate,
                  context,
                  defaults
                )
              )
            }
          >
            Use document defaults
          </Button>
          <SheetClose asChild>
            <Button type="button">Done</Button>
          </SheetClose>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}
