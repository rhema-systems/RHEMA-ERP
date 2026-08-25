'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { Save } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';
import { useToast } from '@/hooks/use-toast';
import {
  canonicalizeProcurementPolicyOptionValue,
  createProcurementPolicyRuleValue,
  procurementDecisionKeyOptions,
  procurementPolicyRuleRegistry,
} from '@/lib/procurement-policy-rule-registry';
import { procurementPolicyService } from '@/services/procurement-policy.service';
import { workflowApiService } from '@/services/workflow-api.service';
import type {
  ProcurementPolicyRule,
  ProcurementPolicyRuleKind,
  ProcurementPolicyRuleValue,
  ProcurementPolicyScopeType,
  SaveProcurementPolicyRuleRequest,
} from '@/types/procurement-policy';
import { WorkflowDefinitionLifecycleStatus } from '@/types/workflow';

type Props = {
  policyId: string;
  scopeType: ProcurementPolicyScopeType;
  kind: ProcurementPolicyRuleKind;
  policyEffectiveFrom: string;
  policyEffectiveTo?: string;
  rule?: ProcurementPolicyRule;
  availableRules?: ProcurementPolicyRule[];
  editable: boolean;
  onChanged: () => void | Promise<void>;
  onCancel?: () => void;
};

const normalizeToken = (value: unknown) =>
  String(value ?? '')
    .replace(/[^a-z0-9]/gi, '')
    .toLowerCase();

const sameOptionalText = (left: unknown, right: unknown) =>
  String(left ?? '')
    .trim()
    .toLowerCase() ===
  String(right ?? '')
    .trim()
    .toLowerCase();

const roleSnapshotField: Record<string, string> = {
  authorityRoleId: 'authorityRole',
  escalationAuthorityRoleId: 'escalationAuthority',
  approverRoleId: 'approverRole',
  initiatorRoleId: 'initiatorRole',
  conflictingRoleId: 'conflictingRole',
};

const errorMessage = (error: unknown) => {
  const candidate = error as {
    message?: string;
    response?: { detail?: string; title?: string };
  };
  return (
    candidate.response?.detail ||
    candidate.response?.title ||
    candidate.message ||
    'The request failed.'
  );
};

const cleanValue = (
  value: ProcurementPolicyRuleValue
): ProcurementPolicyRuleValue =>
  Object.fromEntries(
    Object.entries(value).filter(
      ([, current]) => current !== '' && current !== undefined
    )
  ) as ProcurementPolicyRuleValue;

export function ProcurementPolicyRuleEditor({
  policyId,
  scopeType,
  kind,
  policyEffectiveFrom,
  policyEffectiveTo,
  rule,
  availableRules = [],
  editable,
  onChanged,
  onCancel,
}: Props) {
  const { toast } = useToast();
  const definition = procurementPolicyRuleRegistry[kind];
  const [value, setValue] = useState<ProcurementPolicyRuleValue>(() => {
    const initial = {
      ...createProcurementPolicyRuleValue(
        kind,
        policyEffectiveFrom.slice(0, 10),
        policyEffectiveTo?.slice(0, 10)
      ),
      ...(rule?.value ?? {}),
      effectiveFrom: (rule?.value.effectiveFrom ?? policyEffectiveFrom).slice(
        0,
        10
      ),
      effectiveTo: (rule?.value.effectiveTo ?? policyEffectiveTo)?.slice(0, 10),
    };
    definition.fields.forEach((field) => {
      if (field.type === 'select')
        initial[field.key] = canonicalizeProcurementPolicyOptionValue(
          initial[field.key],
          field.options
        );
    });
    return initial;
  });
  const [reason, setReason] = useState('');
  const hasWorkflowReference = definition.fields.some(
    (field) => field.key === 'workflowDefinitionId'
  );
  const hasRoleField = definition.fields.some((field) => field.type === 'role');
  const selectedWorkflowDefinitionId = String(
    value.workflowDefinitionId ?? ''
  ).trim();
  const roles = useQuery({
    queryKey: [
      'procurement-policy-roles',
      selectedWorkflowDefinitionId || 'tenant',
    ],
    enabled: hasRoleField,
    queryFn: () =>
      procurementPolicyService.getRoleOptions(
        selectedWorkflowDefinitionId || undefined
      ),
  });
  const availableRoles = roles.data ?? [];
  const workflows = useQuery({
    queryKey: ['procurement-policy-workflows', 'published'],
    enabled: hasWorkflowReference,
    queryFn: async () => {
      const result = await workflowApiService.getWorkflowDefinitions({
        page: 1,
        pageSize: 250,
        isActive: true,
        sortBy: 'name',
        sortDescending: false,
      });
      return result.data.filter(
        (item) =>
          item.lifecycleStatus ===
            WorkflowDefinitionLifecycleStatus.Published && item.isActive
      );
    },
  });
  const publishedWorkflows = workflows.data ?? [];
  const selectableWorkflows = publishedWorkflows.filter((workflow) => {
    if (kind === 'Method')
      return (
        normalizeToken(workflow.entityType) ===
        normalizeToken('TENDER_EVALUATION')
      );
    if (kind === 'Authority')
      return (
        normalizeToken(workflow.entityType) ===
        normalizeToken('PurchaseRequisition')
      );
    return true;
  });

  const enabledCategoryRules = availableRules.filter(
    (candidate) => candidate.kind === 'Category' && candidate.isEnabled
  );
  const selectedCategoryRule = enabledCategoryRules.find(
    (candidate) =>
      normalizeToken(candidate.value.category) ===
        normalizeToken(value.category) &&
      sameOptionalText(candidate.value.serviceClass, value.serviceClass)
  );
  const matchingMethodRules = availableRules.filter(
    (candidate) =>
      candidate.kind === 'Method' &&
      candidate.isEnabled &&
      normalizeToken(candidate.value.category) ===
        normalizeToken(value.category) &&
      sameOptionalText(candidate.value.serviceClass, value.serviceClass)
  );
  const selectedMethodRule = matchingMethodRules.find(
    (candidate) =>
      normalizeToken(candidate.value.method) === normalizeToken(value.method)
  );

  const missingRequired = useMemo(
    () =>
      definition.fields.filter((field) => {
        if (!field.required) return false;
        const current = value[field.key];
        return current === undefined || current === null || current === '';
      }),
    [definition.fields, value]
  );

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
      toast({
        title: rule ? 'Rule updated' : 'Rule created',
        description: `${definition.label} rule saved with an audit revision.`,
        variant: 'success',
      });
      await onChanged();
    },
    onError: (error) =>
      toast({
        title: 'Unable to save policy rule',
        description: errorMessage(error),
        variant: 'destructive',
      }),
  });

  const setField = (key: string, next: unknown) =>
    setValue((current) => ({ ...current, [key]: next }));

  return (
    <div className="space-y-5">
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2">
          <Label>Rule code *</Label>
          <Input
            disabled={!editable}
            value={String(value.ruleCode ?? '')}
            onChange={(event) =>
              setField('ruleCode', event.target.value.toUpperCase())
            }
            placeholder="Unique within this policy"
          />
        </div>
        <div className="space-y-2">
          <Label>Source decision *</Label>
          <Select
            disabled={!editable}
            value={String(
              value.sourceDecisionKey ?? definition.defaultDecisionKey
            )}
            onValueChange={(next) => setField('sourceDecisionKey', next)}
          >
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {procurementDecisionKeyOptions.map((key) => (
                <SelectItem key={key} value={key}>
                  {key}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-2">
          <Label>Effective from *</Label>
          <Input
            disabled={!editable}
            type="date"
            value={String(value.effectiveFrom ?? '')}
            onChange={(event) => setField('effectiveFrom', event.target.value)}
          />
        </div>
        <div className="space-y-2">
          <Label>Effective to</Label>
          <Input
            disabled={!editable}
            type="date"
            value={String(value.effectiveTo ?? '')}
            onChange={(event) =>
              setField('effectiveTo', event.target.value || undefined)
            }
          />
        </div>
        <div className="space-y-2">
          <Label>Priority</Label>
          <Input
            disabled={!editable}
            type="number"
            value={String(value.priority ?? 0)}
            onChange={(event) =>
              setField('priority', Number(event.target.value || 0))
            }
          />
        </div>
        <div className="space-y-2">
          <Label>Override action</Label>
          <Select
            disabled={!editable}
            value={String(value.overrideAction ?? 'Add')}
            onValueChange={(next) => setField('overrideAction', next)}
          >
            <SelectTrigger>
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="Add">Add</SelectItem>
              {scopeType === 'TenantOverride' && (
                <SelectItem value="Replace">Replace base rule</SelectItem>
              )}
              {scopeType === 'TenantOverride' && (
                <SelectItem value="Disable">Disable base rule</SelectItem>
              )}
            </SelectContent>
          </Select>
        </div>
        {scopeType === 'TenantOverride' && value.overrideAction !== 'Add' && (
          <div className="space-y-2 sm:col-span-2">
            <Label>Base rule ID *</Label>
            <Input
              disabled={!editable}
              value={String(value.sourceRuleId ?? '')}
              onChange={(event) => setField('sourceRuleId', event.target.value)}
              placeholder="Immutable rule ID from the selected base policy"
            />
          </div>
        )}
        <div className="flex h-10 items-center gap-3 rounded-md border px-3 sm:col-span-2">
          <Switch
            disabled={!editable}
            checked={Boolean(value.isEnabled)}
            onCheckedChange={(next) => setField('isEnabled', next)}
          />
          <span className="text-sm">
            {value.isEnabled ? 'Enabled rule' : 'Disabled rule'}
          </span>
        </div>
      </div>

      <div className="border-t pt-5">
        <p className="mb-4 text-sm text-muted-foreground">
          {definition.description}
        </p>
        {kind === 'Evidence' && (
          <div className="mb-4 rounded-md border bg-muted/30 p-3 text-sm">
            <p className="font-medium">Evidence requirement identifier</p>
            <p className="mt-1 text-muted-foreground">
              Enter the evidence name and stage. The system generates the stable
              internal requirement key when the rule is saved; users provide the
              actual document or evidence during the applicable procurement
              workflow stage.
            </p>
            <p className="mt-2 text-xs text-muted-foreground">
              System key:{' '}
              <span className="font-mono">
                {String(
                  value.sharedRequirementKey ||
                    value.ruleCode ||
                    'generated when saved'
                )}
              </span>
            </p>
          </div>
        )}
        <div className="grid gap-4 sm:grid-cols-2">
          {definition.fields.map((field) => {
            const current = value[field.key];
            return (
              <div
                key={field.key}
                className={field.wide ? 'space-y-2 sm:col-span-2' : 'space-y-2'}
              >
                <Label>
                  {field.label}
                  {field.required ? ' *' : ''}
                </Label>
                {field.key === 'workflowDefinitionId' ? (
                  <div className="space-y-1.5">
                    <Select
                      disabled={!editable || workflows.isLoading}
                      value={String(current ?? '') || undefined}
                      onValueChange={(next) => {
                        setValue((currentValue) => {
                          const nextValue = {
                            ...currentValue,
                            [field.key]: next,
                          };
                          definition.fields
                            .filter((candidate) => candidate.type === 'role')
                            .forEach((candidate) => {
                              nextValue[candidate.key] = null;
                            });
                          return nextValue;
                        });
                      }}
                    >
                      <SelectTrigger>
                        <SelectValue
                          placeholder={
                            workflows.isLoading
                              ? 'Loading published workflows…'
                              : kind === 'Method'
                                ? 'Select a published Tender Evaluation workflow'
                                : kind === 'Authority'
                                  ? 'Select a published Purchase Requisition workflow'
                                  : 'Select a published workflow'
                          }
                        />
                      </SelectTrigger>
                      <SelectContent>
                        {Boolean(current) &&
                          !selectableWorkflows.some(
                            (workflow) => workflow.id === current
                          ) && (
                            <SelectItem value={String(current)}>
                              Saved workflow reference (not currently published)
                            </SelectItem>
                          )}
                        {selectableWorkflows.map((workflow) => (
                          <SelectItem key={workflow.id} value={workflow.id}>
                            {workflow.name} · {workflow.entityType} · v
                            {workflow.version}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    {workflows.isError && (
                      <p className="text-xs text-destructive">
                        Published workflows could not be loaded. Refresh the
                        page before saving this rule.
                      </p>
                    )}
                    {!workflows.isLoading &&
                      !workflows.isError &&
                      selectableWorkflows.length === 0 && (
                        <p className="text-xs text-amber-700">
                          {kind === 'Method'
                            ? 'Publish an active TENDER_EVALUATION workflow before configuring this RFQ rule.'
                            : kind === 'Authority'
                              ? 'Publish an active Purchase Requisition workflow before configuring this authority rule.'
                              : 'Publish an active workflow before configuring this rule.'}
                        </p>
                      )}
                  </div>
                ) : kind === 'Threshold' && field.key === 'category' ? (
                  <div className="space-y-1.5">
                    <Select
                      disabled={!editable}
                      value={selectedCategoryRule?.id}
                      onValueChange={(next) => {
                        const selected = enabledCategoryRules.find(
                          (candidate) => candidate.id === next
                        );
                        if (!selected) return;
                        setValue((currentValue) => ({
                          ...currentValue,
                          category: canonicalizeProcurementPolicyOptionValue(
                            selected.value.category,
                            field.options
                          ),
                          serviceClass: selected.value.serviceClass ?? '',
                          method: '',
                        }));
                      }}
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="Select an enabled category rule" />
                      </SelectTrigger>
                      <SelectContent>
                        {enabledCategoryRules.map((candidate) => (
                          <SelectItem key={candidate.id} value={candidate.id}>
                            {candidate.name} ·{' '}
                            {String(candidate.value.category)}
                            {candidate.value.serviceClass
                              ? ` · ${String(candidate.value.serviceClass)}`
                              : ''}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    {!selectedCategoryRule && Boolean(value.category) && (
                      <p className="text-xs text-amber-700">
                        The saved category/service class no longer matches an
                        enabled Category rule. Select the intended rule again.
                      </p>
                    )}
                  </div>
                ) : kind === 'Threshold' && field.key === 'method' ? (
                  <div className="space-y-1.5">
                    <Select
                      disabled={!editable || !selectedCategoryRule}
                      value={selectedMethodRule?.id}
                      onValueChange={(next) => {
                        const selected = matchingMethodRules.find(
                          (candidate) => candidate.id === next
                        );
                        if (!selected) return;
                        setValue((currentValue) => ({
                          ...currentValue,
                          category: canonicalizeProcurementPolicyOptionValue(
                            selected.value.category,
                            procurementPolicyRuleRegistry.Threshold.fields.find(
                              (candidateField) =>
                                candidateField.key === 'category'
                            )?.options
                          ),
                          serviceClass: selected.value.serviceClass ?? '',
                          method: canonicalizeProcurementPolicyOptionValue(
                            selected.value.method,
                            field.options
                          ),
                        }));
                      }}
                    >
                      <SelectTrigger>
                        <SelectValue placeholder="Select an enabled method rule" />
                      </SelectTrigger>
                      <SelectContent>
                        {matchingMethodRules.map((candidate) => (
                          <SelectItem key={candidate.id} value={candidate.id}>
                            {candidate.name} · {String(candidate.value.method)}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    {selectedCategoryRule &&
                      matchingMethodRules.length === 0 && (
                        <p className="text-xs text-amber-700">
                          No enabled Method rule matches this category and
                          service class. Create or enable the method rule first.
                        </p>
                      )}
                  </div>
                ) : field.type === 'role' ? (
                  <div className="space-y-1.5">
                    <Select
                      disabled={!editable || roles.isLoading}
                      value={String(current ?? '') || undefined}
                      onValueChange={(next) => {
                        const roleId = next === '__none__' ? null : next;
                        const snapshotKey = roleSnapshotField[field.key];
                        const selectedRole = availableRoles.find(
                          (role) => role.id === roleId
                        );
                        setValue((currentValue) => ({
                          ...currentValue,
                          [field.key]: roleId,
                          ...(snapshotKey
                            ? { [snapshotKey]: selectedRole?.name ?? null }
                            : {}),
                        }));
                      }}
                    >
                      <SelectTrigger>
                        <SelectValue
                          placeholder={
                            roles.isLoading
                              ? 'Loading configured roles…'
                              : 'Select a configured role'
                          }
                        />
                      </SelectTrigger>
                      <SelectContent>
                        {!field.required && (
                          <SelectItem value="__none__">
                            No escalation role
                          </SelectItem>
                        )}
                        {Boolean(current) &&
                          !availableRoles.some(
                            (role) =>
                              role.id.toLowerCase() ===
                              String(current).toLowerCase()
                          ) && (
                            <SelectItem value={String(current)}>
                              {String(
                                value[roleSnapshotField[field.key]] ?? current
                              )}{' '}
                              (saved role is unavailable)
                            </SelectItem>
                          )}
                        {availableRoles.map((role) => (
                          <SelectItem key={role.id} value={role.id}>
                            {role.name}
                            {role.description ? ` · ${role.description}` : ''}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    {roles.isError && (
                      <p className="text-xs text-destructive">
                        Configured roles could not be loaded. Refresh the page
                        before saving this rule.
                      </p>
                    )}
                    {!roles.isLoading &&
                      !roles.isError &&
                      availableRoles.length === 0 && (
                        <p className="text-xs text-amber-700">
                          Create the required role in Identity Management before
                          configuring this rule.
                        </p>
                      )}
                  </div>
                ) : field.type === 'select' ? (
                  <Select
                    disabled={!editable}
                    value={canonicalizeProcurementPolicyOptionValue(
                      current,
                      field.options
                    )}
                    onValueChange={(next) => setField(field.key, next)}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select an option" />
                    </SelectTrigger>
                    <SelectContent>
                      {field.options?.map((option) => (
                        <SelectItem key={option.value} value={option.value}>
                          {option.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                ) : field.type === 'boolean' ? (
                  <div className="flex h-10 items-center gap-3 rounded-md border px-3">
                    <Switch
                      disabled={!editable}
                      checked={Boolean(current)}
                      onCheckedChange={(next) => setField(field.key, next)}
                    />
                    <span className="text-sm text-muted-foreground">
                      {current ? 'Enabled' : 'Disabled'}
                    </span>
                  </div>
                ) : field.type === 'textarea' ? (
                  <Textarea
                    disabled={!editable}
                    value={String(current ?? '')}
                    onChange={(event) =>
                      setField(field.key, event.target.value)
                    }
                    placeholder={field.placeholder}
                  />
                ) : (
                  <Input
                    disabled={!editable}
                    type={field.type}
                    min={field.min}
                    step={field.step}
                    value={String(current ?? '')}
                    onChange={(event) =>
                      setField(
                        field.key,
                        field.type === 'number'
                          ? event.target.value === ''
                            ? undefined
                            : Number(event.target.value)
                          : event.target.value
                      )
                    }
                    placeholder={field.placeholder}
                  />
                )}
              </div>
            );
          })}
        </div>
      </div>

      {editable && (
        <div className="border-t pt-5">
          <div className="space-y-2">
            <Label>Change reason</Label>
            <Input
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              placeholder="Captured in immutable policy history"
            />
          </div>
          <div className="mt-4 flex items-center justify-between gap-3">
            <p className="text-xs text-muted-foreground">
              {missingRequired.length
                ? `${missingRequired.length} required field(s) still need values.`
                : 'Client checks passed; server validation remains authoritative.'}
            </p>
            <div className="flex gap-2">
              {onCancel && (
                <Button variant="outline" onClick={onCancel}>
                  Cancel
                </Button>
              )}
              <Button
                onClick={() => save.mutate()}
                disabled={
                  save.isPending ||
                  missingRequired.length > 0 ||
                  !String(value.ruleCode ?? '').trim()
                }
              >
                <Save className="mr-2 h-4 w-4" />
                {save.isPending ? 'Saving…' : 'Save rule'}
              </Button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
