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
  FinanceSourceDocumentDimension,
} from '@/types/finance';

export interface SourceDimensionEditableLine {
  id: string;
  accountId?: string;
  additionalAccountIds?: string[];
  requiredDimensionCodes?: string[];
  accountLabel?: string;
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
                <Badge variant="secondary">Server-resolved</Badge>
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
                The economic account and Fixed dimensions are resolved by the
                server. Document defaults are applied to eligible dimensions.
              </p>
            )}
          </div>
        ))}
      </div>
      {certificationState === 'CaptureOptional' && (
        <p className="flex items-start gap-2 text-xs text-amber-700">
          <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
          Missing Required dimensions are readiness warnings during the
          CaptureOptional rollout. Supplied, Fixed and Prohibited values are
          still validated by Finance.
        </p>
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
