'use client';

import { AlertCircle, FileCheck2, Loader2 } from 'lucide-react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import type {
  ProcurementCategoryClass,
  PurchaseRequisitionLinkageOptionDto,
  PurchaseRequisitionLinkageOptionsDto,
  PurchaseRequisitionType,
  SavePurchaseRequisitionLinkageRequest,
} from '@/services/purchasingService';
import { deriveRequisitionLinkageFromPlanItem } from '@/lib/procurement-requisition-linkage';

const NONE = '__none__';

interface Props {
  value: SavePurchaseRequisitionLinkageRequest;
  onChange: (value: SavePurchaseRequisitionLinkageRequest) => void;
  options?: PurchaseRequisitionLinkageOptionsDto;
  loading?: boolean;
  disabled?: boolean;
}

function optionLabel(option: PurchaseRequisitionLinkageOptionDto) {
  const suffix = [option.parentReference, option.status].filter(Boolean).join(' · ');
  return `${option.code} — ${option.name}${suffix ? ` (${suffix})` : ''}`;
}

export function PurchaseRequisitionLinkageFields({ value, onChange, options, loading, disabled }: Props) {
  const setValue = <K extends keyof SavePurchaseRequisitionLinkageRequest>(
    key: K,
    next: SavePurchaseRequisitionLinkageRequest[K]
  ) => onChange({ ...value, [key]: next });

  const select = <K extends keyof SavePurchaseRequisitionLinkageRequest>(key: K, next: string) =>
    setValue(key, (next === NONE ? undefined : next) as SavePurchaseRequisitionLinkageRequest[K]);

  const clearException = (next: string) => {
    if (next === NONE) {
      onChange({
        ...value,
        approvedExceptionRuleId: undefined,
        exceptionWorkflowInstanceId: undefined,
        exceptionApprovalReference: undefined,
        exceptionEvidenceReference: undefined,
      });
      return;
    }
    setValue('approvedExceptionRuleId', next);
  };

  const selectedBudget = options?.budgets.find((item) => item.id === value.budgetId);
  const selectPlanItem = (next: string) => {
    if (next === NONE) {
      onChange({ ...value, sourcePlanItemId: undefined, budgetId: undefined });
      return;
    }
    onChange(deriveRequisitionLinkageFromPlanItem(value, next, options));
  };

  return (
    <Card data-testid="purchase-requisition-linkage-fields">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <FileCheck2 className="h-5 w-5" />
          Planning and Governance Linkage
        </CardTitle>
        <CardDescription>
          Link the requisition to its approved planning source. The linked budget and category are derived automatically when available.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        {loading && (
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" /> Loading tenant reference options…
          </div>
        )}

        <div className="grid grid-cols-1 gap-5 md:grid-cols-2 xl:grid-cols-3">
          <div className="space-y-2 xl:col-span-2">
            <Label>Procurement plan item</Label>
            <Select
              value={value.sourcePlanItemId || NONE}
              onValueChange={selectPlanItem}
              disabled={disabled || loading}
            >
              <SelectTrigger aria-label="Procurement plan item"><SelectValue placeholder="No plan item linked" /></SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE}>No plan item linked</SelectItem>
                {(options?.planItems || []).map((option) => (
                  <SelectItem key={option.id} value={option.id}>{optionLabel(option)}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label>Request type</Label>
            <Select
              value={value.requisitionType}
              onValueChange={(next) => setValue('requisitionType', next as PurchaseRequisitionType)}
              disabled={disabled || loading}
            >
              <SelectTrigger aria-label="Request type"><SelectValue /></SelectTrigger>
              <SelectContent>
                {(options?.requestTypes || []).map((option) => (
                  <SelectItem key={option.name} value={option.name}>{option.label}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label>Procurement budget</Label>
            {value.sourcePlanItemId ? (
              <div className="rounded-md border bg-muted/30 px-3 py-2 text-sm" data-testid="derived-procurement-budget">
                {selectedBudget
                  ? `${optionLabel(selectedBudget)}${selectedBudget.currency ? ` · ${selectedBudget.currency}` : ''}`
                  : 'The budget assigned to this plan item will be applied automatically when the draft is saved.'}
              </div>
            ) : (
              <Select value={value.budgetId || NONE} onValueChange={(next) => select('budgetId', next)} disabled={disabled || loading}>
                <SelectTrigger aria-label="Procurement budget"><SelectValue placeholder="No budget linked" /></SelectTrigger>
                <SelectContent>
                  <SelectItem value={NONE}>No budget linked</SelectItem>
                  {(options?.budgets || []).map((option) => (
                    <SelectItem key={option.id} value={option.id}>
                      {optionLabel(option)}{option.currency ? ` · ${option.currency}` : ''}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          </div>

          <div className="space-y-2">
            <Label>Procurement category</Label>
            <Select
              value={value.procurementCategory || NONE}
              onValueChange={(next) => setValue('procurementCategory', next === NONE ? undefined : next as ProcurementCategoryClass)}
              disabled={disabled || loading}
            >
              <SelectTrigger aria-label="Procurement category"><SelectValue placeholder="No category selected" /></SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE}>No category selected</SelectItem>
                {(options?.categories || []).map((option) => (
                  <SelectItem key={option.name} value={option.name}>{option.label}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2">
            <Label>Project</Label>
            <Select value={value.projectId || NONE} onValueChange={(next) => select('projectId', next)} disabled={disabled || loading}>
              <SelectTrigger aria-label="Project"><SelectValue placeholder="No project linked" /></SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE}>No project linked</SelectItem>
                {(options?.projects || []).map((option) => (
                  <SelectItem key={option.id} value={option.id}>{optionLabel(option)}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2 xl:col-span-2">
            <Label>Specification / TOR template</Label>
            <Select
              value={value.specificationTemplateId || NONE}
              onValueChange={(next) => select('specificationTemplateId', next)}
              disabled={disabled || loading}
            >
              <SelectTrigger aria-label="Specification template"><SelectValue placeholder="No template linked" /></SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE}>No template linked</SelectItem>
                {(options?.specificationTemplates || []).map((option) => (
                  <SelectItem key={option.id} value={option.id}>{optionLabel(option)}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-2 xl:col-span-3">
            <Label>Approved exception rule</Label>
            <Select value={value.approvedExceptionRuleId || NONE} onValueChange={clearException} disabled={disabled || loading}>
              <SelectTrigger aria-label="Approved exception rule"><SelectValue placeholder="No approved exception" /></SelectTrigger>
              <SelectContent>
                <SelectItem value={NONE}>No approved exception</SelectItem>
                {(options?.approvedExceptionRules || []).map((option) => (
                  <SelectItem key={option.id} value={option.id}>{optionLabel(option)}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </div>

        {value.approvedExceptionRuleId && (
          <div className="space-y-4 rounded-lg border border-amber-200 bg-amber-50/50 p-4">
            <div className="flex items-start gap-2 text-sm text-amber-900">
              <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
              The rule is only a policy definition. Link the completed shared Procurement Exception workflow that proves approval.
            </div>
            <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
              <div className="space-y-2 md:col-span-2">
                <Label>Completed exception workflow *</Label>
                <Select
                  value={value.exceptionWorkflowInstanceId || NONE}
                  onValueChange={(next) => select('exceptionWorkflowInstanceId', next)}
                  disabled={disabled || loading}
                >
                  <SelectTrigger aria-label="Completed exception workflow"><SelectValue placeholder="Select approved workflow" /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value={NONE}>Select approved workflow</SelectItem>
                    {(options?.approvedExceptionWorkflows || []).map((option) => (
                      <SelectItem key={option.id} value={option.id}>{optionLabel(option)}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="space-y-2">
                <Label htmlFor="pr-exception-reference">Approval reference</Label>
                <Input
                  id="pr-exception-reference"
                  value={value.exceptionApprovalReference || ''}
                  onChange={(event) => setValue('exceptionApprovalReference', event.target.value)}
                  placeholder="Committee minute or external reference"
                  disabled={disabled}
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="pr-exception-evidence">Evidence reference</Label>
                <Input
                  id="pr-exception-evidence"
                  value={value.exceptionEvidenceReference || ''}
                  onChange={(event) => setValue('exceptionEvidenceReference', event.target.value)}
                  placeholder="Shared evidence or external reference"
                  disabled={disabled}
                />
              </div>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
