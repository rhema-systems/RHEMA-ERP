'use client';

import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, LockKeyhole } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { financeDataService } from '@/services/finance/finance-data.service';
import {
  resolveSourceDimensionValues,
  type FinanceDimensionRuleContext,
} from '@/lib/finance/source-document-dimensions';
import {
  TransactionDimensionDefaults,
  TransactionDimensionLineEditor,
} from './transaction-dimension-editor';
import type {
  FinanceDimensionCertificationState,
  FinanceSettlementDimensionComponent,
  FinanceSourceDocumentDimension,
} from '@/types/finance';

export interface SourceDimensionEditableLine {
  id: string;
  accountId?: string;
  additionalAccountIds?: string[];
  requiredDimensionCodes?: string[];
  accountLabel?: string;
  accountResolution?: 'UserSelection' | 'SourceDocument';
}

export interface SourceDocumentDimensionPanelProps {
  context: FinanceDimensionRuleContext;
  effectiveDate: string;
  lines: SourceDimensionEditableLine[];
  defaultValues: Record<string, string>;
  lineValues: Record<string, Record<string, string>>;
  onDefaultValuesChange: (values: Record<string, string>) => void;
  onLineValuesChange: (values: Record<string, Record<string, string>>) => void;
  onApplyDefaultToAll?: () => void;
  certificationState?: FinanceDimensionCertificationState;
  disabled?: boolean;
}

export interface SourceDocumentDimensionDefaultsPanelProps {
  effectiveDate: string;
  values: Record<string, string>;
  onChange: (values: Record<string, string>) => void;
  disabled?: boolean;
}

/**
 * Capture surface for server-derived economic lines whose stable IDs/accounts are not available
 * until the Finance action builds its posting proposal. The server applies this clearable default
 * only to eligible lines, resolves Fixed values, and remains authoritative for every line.
 */
export function SourceDocumentDimensionDefaultsPanel({
  effectiveDate,
  values,
  onChange,
  disabled = false,
}: SourceDocumentDimensionDefaultsPanelProps) {
  const { data: definitions = [], isLoading } = useQuery({
    queryKey: ['finance-dimensions', 'source-document-defaults'],
    queryFn: () => financeDataService.getFinanceDimensions(),
  });

  if (isLoading)
    return <div className="rounded-lg border p-4 text-sm text-muted-foreground">Loading Finance coding dimensions…</div>;
  if (definitions.length === 0) return null;

  return (
    <div className="space-y-2 rounded-lg border p-4">
      <TransactionDimensionDefaults
        definitions={definitions}
        effectiveDate={effectiveDate}
        values={values}
        onChange={onChange}
        onApplyToAll={() => undefined}
        showApplyToAll={false}
        disabled={disabled}
      />
      <p className="flex items-start gap-2 text-xs text-muted-foreground">
        <LockKeyhole className="mt-0.5 h-3.5 w-3.5 shrink-0" />
        Finance applies this default to eligible economic lines. Fixed values are resolved and locked
        by the server; prohibited values are never copied. Each resulting line remains authoritative.
      </p>
    </div>
  );
}

export function SourceDocumentDimensionPanel({
  context,
  effectiveDate,
  lines,
  defaultValues,
  lineValues,
  onDefaultValuesChange,
  onLineValuesChange,
  onApplyDefaultToAll,
  certificationState = 'CaptureOptional',
  disabled = false,
}: SourceDocumentDimensionPanelProps) {
  const { data: definitions = [], isLoading: definitionsLoading } = useQuery({
    queryKey: ['finance-dimensions', 'source-document-editor'],
    queryFn: () => financeDataService.getFinanceDimensions(),
  });
  const { data: rules = [], isLoading: rulesLoading } = useQuery({
    queryKey: ['finance-dimension-rules', 'source-document-editor'],
    queryFn: () => financeDataService.getFinanceDimensionRules(),
  });

  const applyToAll = () => {
    const next = { ...lineValues };
    for (const line of lines) {
      if (!line.accountId) continue;
      next[line.id] = resolveSourceDimensionValues(
        definitions,
        rules,
        line.accountId,
        effectiveDate,
        context,
        defaultValues,
        line.additionalAccountIds
      );
    }
    onLineValuesChange(next);
    onApplyDefaultToAll?.();
  };

  if (definitionsLoading || rulesLoading)
    return (
      <div className="rounded-lg border p-4 text-sm text-muted-foreground">
        Loading Finance coding dimensions…
      </div>
    );
  if (definitions.length === 0) return null;

  return (
    <div className="space-y-4">
      <TransactionDimensionDefaults
        definitions={definitions}
        effectiveDate={effectiveDate}
        values={defaultValues}
        onChange={onDefaultValuesChange}
        onApplyToAll={applyToAll}
        disabled={disabled}
      />
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
        {lines.map((line, index) => (
          <div key={line.id} className="rounded-lg border p-3">
            <div className="mb-2 flex items-center justify-between gap-2">
              <span className="truncate text-sm font-medium">
                Line {index + 1}
                {line.accountLabel ? ` · ${line.accountLabel}` : ''}
              </span>
              {!line.accountId && (
                <Badge variant="secondary">
                  {line.accountResolution === 'UserSelection'
                    ? 'Select account first'
                    : 'Source-controlled'}
                </Badge>
              )}
            </div>
            {line.accountId ? (
              <TransactionDimensionLineEditor
                definitions={definitions}
                rules={rules}
                context={context}
                effectiveDate={effectiveDate}
                lineNumber={index + 1}
                accountId={line.accountId}
                additionalAccountIds={line.additionalAccountIds}
                requiredDimensionCodes={line.requiredDimensionCodes}
                accountLabel={line.accountLabel}
                values={lineValues[line.id] || {}}
                defaults={defaultValues}
                certificationState={certificationState}
                onChange={(values) =>
                  onLineValuesChange({ ...lineValues, [line.id]: values })
                }
                disabled={disabled}
              />
            ) : (
              <p className="flex items-start gap-2 text-xs text-muted-foreground">
                <LockKeyhole className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                {line.accountResolution === 'UserSelection'
                  ? 'Select this line’s GL account above to make its coding dimensions available.'
                  : 'Finance derives the posting account from the approved source document, then applies these defaults only where they are allowed.'}
              </p>
            )}
          </div>
        ))}
      </div>
      {certificationState === 'CaptureOptional' && (
        <div className="flex items-start gap-2 rounded-md border border-amber-200 bg-amber-50/60 p-3 text-xs text-amber-800">
          <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
          <div>
            <p className="font-medium">Dimension validation is in pilot mode</p>
            <p className="mt-0.5">
              Missing required coding is currently reported as a warning and will not block this invoice.
              Any coding entered must still comply with Finance’s fixed and prohibited-value rules.
            </p>
          </div>
        </div>
      )}
    </div>
  );
}

export function SourceDocumentDimensionEvidence({
  evidence,
}: {
  evidence?: FinanceSourceDocumentDimension;
}) {
  if (!evidence) return null;
  return (
    <div className="space-y-3 rounded-lg border p-4">
      <div className="flex flex-wrap items-center gap-2">
        <h3 className="font-medium">Finance coding dimensions</h3>
        <Badge variant="outline">{evidence.certificationState}</Badge>
        {evidence.budgetEvidenceStatus !== 'NotApplicable' && (
          <Badge
            variant={
              evidence.budgetEvidenceStatus === 'Current'
                ? 'secondary'
                : 'destructive'
            }
          >
            Budget {evidence.budgetEvidenceStatus}
          </Badge>
        )}
      </div>
      {evidence.lines.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          No line coding captured.
        </p>
      ) : (
        <div className="space-y-2">
          {evidence.lines.map((line, index) => (
            <div key={line.sourceLineId} className="rounded-md bg-muted/40 p-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <span className="text-sm font-medium">Line {index + 1}</span>
                {line.isFrozen && (
                  <Badge variant="secondary">Frozen evidence</Badge>
                )}
              </div>
              <p className="mt-1 text-sm">
                {line.values.length
                  ? line.values
                      .map(
                        (value) =>
                          `${value.dimensionCode}: ${value.valueCode} — ${value.valueName}`
                      )
                      .join(' · ')
                  : 'No dimensions assigned'}
              </p>
              {line.readinessWarnings.map((warning) => (
                <p key={warning} className="mt-1 text-xs text-amber-700">
                  {warning}
                </p>
              ))}
            </div>
          ))}
        </div>
      )}
      {evidence.readinessWarnings.map((warning) => (
        <p key={warning} className="text-xs text-amber-700">
          {warning}
        </p>
      ))}
    </div>
  );
}

export function SettlementDimensionEvidence({
  evidence = [],
}: {
  evidence?: FinanceSettlementDimensionComponent[];
}) {
  if (evidence.length === 0) return null;
  return (
    <div className="space-y-3 rounded-lg border p-4">
      <div className="flex flex-wrap items-center gap-2">
        <h3 className="font-medium">Settlement dimension evidence</h3>
        <Badge variant="outline">{evidence.length} component lines</Badge>
      </div>
      <div className="space-y-2">
        {evidence.map((item) => (
          <div key={item.id} className="rounded-md bg-muted/40 p-3 text-sm">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className="font-medium">{item.componentType}</span>
              {item.evidenceFrozenAt && <Badge variant="secondary">Frozen evidence</Badge>}
            </div>
            <p className="mt-1 text-muted-foreground">
              {item.transactionCurrencyCode} {item.transactionAmount.toFixed(2)} · Functional{' '}
              {item.functionalAmount.toFixed(2)}
              {item.isFinalResidualRecipient ? ' · final residual line' : ''}
            </p>
            <p className="mt-1">
              {item.dimensionValues.length
                ? item.dimensionValues
                    .map(
                      (value) =>
                        `${value.dimensionCode} (${value.dimensionName}): ${value.valueCode} — ${value.valueName}`,
                    )
                    .join(' · ')
                : 'No dimensions assigned'}
            </p>
          </div>
        ))}
      </div>
    </div>
  );
}
