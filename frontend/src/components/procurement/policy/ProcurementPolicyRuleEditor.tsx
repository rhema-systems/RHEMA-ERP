'use client';

import { useMemo, useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { Save } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import {
  createProcurementPolicyRuleValue,
  procurementDecisionKeyOptions,
  procurementPolicyRuleRegistry,
} from '@/lib/procurement-policy-rule-registry';
import { procurementPolicyService } from '@/services/procurement-policy.service';
import type {
  ProcurementPolicyRule,
  ProcurementPolicyRuleKind,
  ProcurementPolicyRuleValue,
  ProcurementPolicyScopeType,
  SaveProcurementPolicyRuleRequest,
} from '@/types/procurement-policy';

type Props = {
  policyId: string;
  scopeType: ProcurementPolicyScopeType;
  kind: ProcurementPolicyRuleKind;
  policyEffectiveFrom: string;
  policyEffectiveTo?: string;
  rule?: ProcurementPolicyRule;
  editable: boolean;
  onChanged: () => void | Promise<void>;
  onCancel?: () => void;
};

const errorMessage = (error: unknown) => {
  const candidate = error as { message?: string; response?: { detail?: string; title?: string } };
  return candidate.response?.detail || candidate.response?.title || candidate.message || 'The request failed.';
};

const cleanValue = (value: ProcurementPolicyRuleValue): ProcurementPolicyRuleValue => Object.fromEntries(
  Object.entries(value).filter(([, current]) => current !== '' && current !== undefined),
) as ProcurementPolicyRuleValue;

export function ProcurementPolicyRuleEditor({
  policyId,
  scopeType,
  kind,
  policyEffectiveFrom,
  policyEffectiveTo,
  rule,
  editable,
  onChanged,
  onCancel,
}: Props) {
  const { toast } = useToast();
  const definition = procurementPolicyRuleRegistry[kind];
  const [value, setValue] = useState<ProcurementPolicyRuleValue>(() => ({
    ...createProcurementPolicyRuleValue(kind, policyEffectiveFrom.slice(0, 10), policyEffectiveTo?.slice(0, 10)),
    ...(rule?.value ?? {}),
    effectiveFrom: (rule?.value.effectiveFrom ?? policyEffectiveFrom).slice(0, 10),
    effectiveTo: (rule?.value.effectiveTo ?? policyEffectiveTo)?.slice(0, 10),
  }));
  const [reason, setReason] = useState('');

  const missingRequired = useMemo(() => definition.fields.filter(field => {
    if (!field.required) return false;
    const current = value[field.key];
    return current === undefined || current === null || current === '';
  }), [definition.fields, value]);

  const save = useMutation({
    mutationFn: () => {
      const normalized = cleanValue(value);
      const request = {
        kind,
        [definition.payloadKey]: normalized,
        rowVersion: rule?.rowVersion,
        reason: reason.trim() || undefined,
      } as SaveProcurementPolicyRuleRequest;
      return rule
        ? procurementPolicyService.updateRule(policyId, rule.id, request)
        : procurementPolicyService.createRule(policyId, request);
    },
    onSuccess: async () => {
      toast({ title: rule ? 'Rule updated' : 'Rule created', description: `${definition.label} rule saved with an audit revision.`, variant: 'success' });
      await onChanged();
    },
    onError: error => toast({ title: 'Unable to save policy rule', description: errorMessage(error), variant: 'destructive' }),
  });

  const setField = (key: string, next: unknown) => setValue(current => ({ ...current, [key]: next }));

  return (
    <div className="space-y-5">
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <Label>Rule code *</Label>
          <Input disabled={!editable} value={String(value.ruleCode ?? '')} onChange={event => setField('ruleCode', event.target.value.toUpperCase())} placeholder="Unique within this policy" />
        </div>
        <div className="space-y-2">
          <Label>Source decision *</Label>
          <Select disabled={!editable} value={String(value.sourceDecisionKey ?? definition.defaultDecisionKey)} onValueChange={next => setField('sourceDecisionKey', next)}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>{procurementDecisionKeyOptions.map(key => <SelectItem key={key} value={key}>{key}</SelectItem>)}</SelectContent>
          </Select>
        </div>
        <div className="space-y-2">
          <Label>Effective from *</Label>
          <Input disabled={!editable} type="date" value={String(value.effectiveFrom ?? '')} onChange={event => setField('effectiveFrom', event.target.value)} />
        </div>
        <div className="space-y-2">
          <Label>Effective to</Label>
          <Input disabled={!editable} type="date" value={String(value.effectiveTo ?? '')} onChange={event => setField('effectiveTo', event.target.value || undefined)} />
        </div>
        <div className="space-y-2">
          <Label>Priority</Label>
          <Input disabled={!editable} type="number" value={String(value.priority ?? 0)} onChange={event => setField('priority', Number(event.target.value || 0))} />
        </div>
        <div className="space-y-2">
          <Label>Override action</Label>
          <Select disabled={!editable} value={String(value.overrideAction ?? 'Add')} onValueChange={next => setField('overrideAction', next)}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="Add">Add</SelectItem>
              {scopeType === 'TenantOverride' && <SelectItem value="Replace">Replace base rule</SelectItem>}
              {scopeType === 'TenantOverride' && <SelectItem value="Disable">Disable base rule</SelectItem>}
            </SelectContent>
          </Select>
        </div>
        {scopeType === 'TenantOverride' && value.overrideAction !== 'Add' && (
          <div className="space-y-2 sm:col-span-2">
            <Label>Base rule ID *</Label>
            <Input disabled={!editable} value={String(value.sourceRuleId ?? '')} onChange={event => setField('sourceRuleId', event.target.value)} placeholder="Immutable rule ID from the selected base policy" />
          </div>
        )}
        <div className="flex h-10 items-center gap-3 rounded-md border px-3 sm:col-span-2">
          <Switch disabled={!editable} checked={Boolean(value.isEnabled)} onCheckedChange={next => setField('isEnabled', next)} />
          <span className="text-sm">{value.isEnabled ? 'Enabled rule' : 'Disabled rule'}</span>
        </div>
      </div>

      <div className="border-t pt-5">
        <p className="mb-4 text-sm text-muted-foreground">{definition.description}</p>
        <div className="grid gap-4 sm:grid-cols-2">
          {definition.fields.map(field => {
            const current = value[field.key];
            return (
              <div key={field.key} className={field.wide ? 'space-y-2 sm:col-span-2' : 'space-y-2'}>
                <Label>{field.label}{field.required ? ' *' : ''}</Label>
                {field.type === 'select' ? (
                  <Select disabled={!editable} value={String(current ?? '')} onValueChange={next => setField(field.key, next)}>
                    <SelectTrigger><SelectValue placeholder="Select an option" /></SelectTrigger>
                    <SelectContent>{field.options?.map(option => <SelectItem key={option.value} value={option.value}>{option.label}</SelectItem>)}</SelectContent>
                  </Select>
                ) : field.type === 'boolean' ? (
                  <div className="flex h-10 items-center gap-3 rounded-md border px-3">
                    <Switch disabled={!editable} checked={Boolean(current)} onCheckedChange={next => setField(field.key, next)} />
                    <span className="text-sm text-muted-foreground">{current ? 'Enabled' : 'Disabled'}</span>
                  </div>
                ) : field.type === 'textarea' ? (
                  <Textarea disabled={!editable} value={String(current ?? '')} onChange={event => setField(field.key, event.target.value)} placeholder={field.placeholder} />
                ) : (
                  <Input disabled={!editable} type={field.type} min={field.min} step={field.step} value={String(current ?? '')} onChange={event => setField(field.key, field.type === 'number' ? (event.target.value === '' ? undefined : Number(event.target.value)) : event.target.value)} placeholder={field.placeholder} />
                )}
              </div>
            );
          })}
        </div>
      </div>

      {editable && (
        <div className="border-t pt-5">
          <div className="space-y-2"><Label>Change reason</Label><Input value={reason} onChange={event => setReason(event.target.value)} placeholder="Captured in immutable policy history" /></div>
          <div className="mt-4 flex items-center justify-between gap-3">
            <p className="text-xs text-muted-foreground">{missingRequired.length ? `${missingRequired.length} required field(s) still need values.` : 'Client checks passed; server validation remains authoritative.'}</p>
            <div className="flex gap-2">
              {onCancel && <Button variant="outline" onClick={onCancel}>Cancel</Button>}
              <Button onClick={() => save.mutate()} disabled={save.isPending || missingRequired.length > 0 || !String(value.ruleCode ?? '').trim()}>
                <Save className="mr-2 h-4 w-4" />{save.isPending ? 'Saving…' : 'Save rule'}
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
