'use client';

import { z } from 'zod';
import { useQueryClient } from '@tanstack/react-query';
import { Badge } from '@/components/ui/badge';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceListPanel } from '@/components/hr/common/ResourceListPanel';
import {
  TextField,
  NumberField,
  TextareaField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { alertRuleService } from '@/services/hr/attendance-setup.service';
import { humanizeEnum } from '@/lib/hr/attendance-format';
import {
  ALERT_TRIGGER_TYPE_OPTIONS,
  ALERT_SEVERITY_OPTIONS,
} from '@/types/hr/attendance';
import type { StaffAttendanceAlertRuleSummary } from '@/types/hr/attendance';

/**
 * Rules that raise attendance alerts. Each rule pairs a trigger with a threshold and a
 * notification channel; the alerts themselves are worked in `/hr/attendance/alerts`.
 *
 * The trigger type is fixed after creation — alerts already raised reference it, so the
 * update DTO omits it and the dialog disables it when editing.
 */
const ruleSchema = z.object({
  ruleName: z.string().min(1, 'A name is required').max(150),
  description: z.string().max(500).optional(),
  triggerType: z.enum([
    'ConsecutiveAbsences',
    'ChronicLateness',
    'MissingPunch',
    'OvertimeThresholdReached',
    'ExcessiveEarlyDeparture',
    'UnauthorisedAbsence',
    'LowAttendancePercentage',
  ]),
  severity: z.enum(['Info', 'Warning', 'Critical']),
  thresholdValue: z.coerce.number().min(0).max(10000),
  evaluationWindowDays: z.coerce.number().min(1).max(365).optional(),
  notifyByEmail: z.boolean(),
  notifyInApp: z.boolean(),
  requiresAcknowledgement: z.boolean(),
  isActive: z.boolean(),
});

type RuleForm = z.input<typeof ruleSchema>;

const emptyRule: RuleForm = {
  ruleName: '',
  description: '',
  triggerType: 'ConsecutiveAbsences',
  severity: 'Warning',
  thresholdValue: 3,
  evaluationWindowDays: 30,
  notifyByEmail: true,
  notifyInApp: true,
  requiresAcknowledgement: false,
  isActive: true,
};

/** What the threshold actually counts, so the number isn't a mystery in the dialog. */
const THRESHOLD_UNITS: Record<RuleForm['triggerType'], string> = {
  ConsecutiveAbsences: 'consecutive days absent',
  ChronicLateness: 'late arrivals in the window',
  MissingPunch: 'missing punches in the window',
  OvertimeThresholdReached: 'overtime hours in the window',
  ExcessiveEarlyDeparture: 'early departures in the window',
  UnauthorisedAbsence: 'unauthorised absences in the window',
  LowAttendancePercentage: '% attendance, below which the alert fires',
};

const SEVERITY_VARIANT = {
  Critical: 'destructive',
  Warning: 'secondary',
  Info: 'outline',
} as const;

export default function AlertRulesPage() {
  const queryClient = useQueryClient();

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Attendance Alert Rules"
        description="Thresholds that raise alerts on absence, lateness, missing punches and overtime."
        backHref="/administration/hr/attendance"
      />

      <ResourceListPanel<StaffAttendanceAlertRuleSummary, RuleForm>
        title="alert rules"
        singular="rule"
        queryKey={['hr', 'attendance-alert-rules']}
        dialogHint="Rules are evaluated when a daily attendance record is created or changed."
        list={() => alertRuleService.getAll()}
        create={(values) => {
          const v = ruleSchema.parse(values);
          return alertRuleService.create({
            ...v,
            description: v.description || null,
            evaluationWindowDays: v.evaluationWindowDays ?? null,
            organizationUnitId: null,
            positionId: null,
            employeeId: null,
            notifyRecipientsJson: null,
          });
        }}
        update={(id, values) => {
          const v = ruleSchema.parse(values);
          // triggerType is deliberately absent — the update DTO does not accept it.
          return alertRuleService.update(id, {
            id,
            ruleName: v.ruleName,
            description: v.description || null,
            severity: v.severity,
            thresholdValue: v.thresholdValue,
            evaluationWindowDays: v.evaluationWindowDays ?? null,
            organizationUnitId: null,
            positionId: null,
            employeeId: null,
            notifyByEmail: v.notifyByEmail,
            notifyInApp: v.notifyInApp,
            notifyRecipientsJson: null,
            requiresAcknowledgement: v.requiresAcknowledgement,
            isActive: v.isActive,
          });
        }}
        remove={(id) => alertRuleService.remove(id)}
        getId={(r) => r.id}
        actions={[
          {
            label: (r) => (r.isActive ? 'Deactivate' : 'Activate'),
            run: async (r) => {
              await alertRuleService.toggleActive(r.id);
              await queryClient.invalidateQueries({ queryKey: ['hr', 'attendance-alert-rules'] });
            },
          },
        ]}
        columns={[
          { header: 'Rule', cell: (r) => <span className="font-medium">{r.ruleName}</span> },
          { header: 'Trigger', cell: (r) => humanizeEnum(r.triggerType) },
          {
            header: 'Severity',
            cell: (r) => <Badge variant={SEVERITY_VARIANT[r.severity]}>{r.severity}</Badge>,
          },
          { header: 'Threshold', cell: (r) => r.thresholdValue, className: 'text-right' },
          {
            header: 'Open alerts',
            cell: (r) => r.activeAlertCount,
            className: 'text-right',
          },
          { header: 'Status', cell: (r) => <StatusBadge active={r.isActive} /> },
        ]}
        schema={ruleSchema as any}
        emptyForm={emptyRule}
        toForm={(r) => ({
          ...emptyRule,
          ruleName: r.ruleName,
          triggerType: r.triggerType,
          severity: r.severity,
          thresholdValue: r.thresholdValue,
          isActive: r.isActive,
        })}
        renderFields={(form) => {
          const trigger = form.watch('triggerType') as RuleForm['triggerType'];
          return (
            <>
              <TextField form={form} name="ruleName" label="Rule name" required />
              <TextareaField form={form} name="description" label="Description" rows={2} />
              <FieldRow>
                <SelectField
                  form={form}
                  name="triggerType"
                  label="Trigger"
                  required
                  options={ALERT_TRIGGER_TYPE_OPTIONS}
                />
                <SelectField
                  form={form}
                  name="severity"
                  label="Severity"
                  required
                  options={ALERT_SEVERITY_OPTIONS}
                />
              </FieldRow>
              <FieldRow>
                <NumberField
                  form={form}
                  name="thresholdValue"
                  label="Threshold"
                  step="0.01"
                  required
                />
                <NumberField
                  form={form}
                  name="evaluationWindowDays"
                  label="Window (days)"
                />
              </FieldRow>
              <p className="text-xs text-muted-foreground">
                Fires at {THRESHOLD_UNITS[trigger]}.
              </p>
              <SwitchField form={form} name="notifyInApp" label="Notify in app" />
              <SwitchField form={form} name="notifyByEmail" label="Notify by email" />
              <SwitchField
                form={form}
                name="requiresAcknowledgement"
                label="Requires acknowledgement"
                description="The alert stays open until someone acknowledges it."
              />
              <SwitchField form={form} name="isActive" label="Active" />
            </>
          );
        }}
      />
    </div>
  );
}
