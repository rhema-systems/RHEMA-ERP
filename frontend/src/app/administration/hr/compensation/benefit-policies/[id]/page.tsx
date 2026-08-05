'use client';

import { useMemo } from 'react';
import { useParams } from 'next/navigation';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Loader2 } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import { NumberField, SelectField, SwitchField, FieldRow } from '@/components/hr/employee/tabs/fields';
import { benefitPolicyService } from '@/services/hr/benefits.service';
import { salaryGradeService } from '@/services/hr/salary-grade.service';
import { staffLevelService } from '@/services/hr/staff-level.service';
import { formatDate, formatMoney, humanizeEnum } from '@/lib/hr/attendance-format';
import type { BenefitGradeValue } from '@/types/hr/benefits';

function InfoRow({ label, value }: { label: string; value?: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5 py-1.5">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm">{value ?? '—'}</span>
    </div>
  );
}

/**
 * Per-grade value rows. A row is keyed to EITHER a salary grade or a staff level, never both —
 * the two are different ways of banding people and mixing them in one row would make the
 * resolved value ambiguous.
 */
const gradeValueSchema = z
  .object({
    scope: z.enum(['grade', 'level']),
    salaryGradeId: z.string().optional(),
    staffLevelId: z.string().optional(),
    amount: z.coerce.number().min(0).optional(),
    rate: z.coerce.number().min(0).max(100).optional(),
    coverageLimit: z.coerce.number().min(0).optional(),
    isActive: z.boolean(),
  })
  .refine((v) => (v.scope === 'grade' ? !!v.salaryGradeId : !!v.staffLevelId), {
    message: 'Choose the grade or level this row applies to',
    path: ['salaryGradeId'],
  })
  .refine((v) => v.amount != null || v.rate != null || v.coverageLimit != null, {
    message: 'Set at least one of amount, rate or coverage limit',
    path: ['amount'],
  });

type GradeValueForm = z.input<typeof gradeValueSchema>;

const emptyGradeValue: GradeValueForm = {
  scope: 'grade',
  salaryGradeId: '',
  staffLevelId: '',
  amount: undefined,
  rate: undefined,
  coverageLimit: undefined,
  isActive: true,
};

export default function BenefitPolicyDetailPage() {
  const params = useParams();
  const id = (params?.id as string) ?? '';

  const { data: policy, isLoading, isError } = useQuery({
    queryKey: ['hr', 'benefit-policies', id],
    queryFn: () => benefitPolicyService.getById(id),
    enabled: !!id,
  });

  const { data: grades } = useQuery({
    queryKey: ['hr', 'salary-grades', 'all'],
    queryFn: () => salaryGradeService.getAll(),
  });

  const { data: levels } = useQuery({
    queryKey: ['hr', 'staff-levels'],
    queryFn: () => staffLevelService.getAll(),
  });

  const gradeOptions = useMemo(
    () => (grades ?? []).map((g) => ({ value: g.id, label: `${g.code} · ${g.name}` })),
    [grades],
  );
  const levelOptions = useMemo(
    () => (levels ?? []).map((l) => ({ value: l.id, label: l.name })),
    [levels],
  );

  const gradeName = (gid?: string | null) =>
    gradeOptions.find((o) => o.value === gid)?.label ?? null;
  const levelName = (lid?: string | null) =>
    levelOptions.find((o) => o.value === lid)?.label ?? null;

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-24">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (isError || !policy) {
    return (
      <div className="p-6">
        <EmptyState title="Benefit policy not found" description="It may have been removed." />
      </div>
    );
  }

  const currency = policy.definition?.currency ?? 'GHS';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title={policy.policyName}
        description={`${humanizeEnum(policy.policyType)} · ${humanizeEnum(policy.recipient)}${policy.policyCode ? ` · ${policy.policyCode}` : ''}`}
        backHref="/administration/hr/compensation/benefit-policies"
        actions={
          <div className="flex items-center gap-2">
            {policy.isMandatory && <Badge variant="secondary">Mandatory</Badge>}
            <StatusBadge active={policy.isActive} />
          </div>
        }
      />

      <Tabs defaultValue="grade-values">
        <TabsList>
          <TabsTrigger value="grade-values">
            Grade values ({policy.gradeValues?.length ?? 0})
          </TabsTrigger>
          <TabsTrigger value="relations">
            Dependant rules ({policy.relations?.length ?? 0})
          </TabsTrigger>
          <TabsTrigger value="terms">Terms</TabsTrigger>
        </TabsList>

        <TabsContent value="grade-values" className="pt-4">
          <p className="mb-4 text-sm text-muted-foreground">
            Overrides the policy&apos;s headline value for a specific salary grade or staff level.
            Without any rows, everyone gets the policy&apos;s own coverage limit and valuation.
          </p>

          <ResourceCollectionTab<BenefitGradeValue, GradeValueForm>
            parentId={id}
            title="grade values"
            singular="grade value"
            queryKey={['hr', 'benefit-policies', id, 'grade-values']}
            invalidateKeys={[['hr', 'benefit-policies', id]]}
            emptyDescription="Everyone on this policy gets its headline value."
            list={(policyId) => benefitPolicyService.getGradeValues(policyId)}
            create={(policyId, values) => {
              const v = gradeValueSchema.parse(values);
              return benefitPolicyService.addGradeValue(policyId, {
                salaryGradeId: v.scope === 'grade' ? (v.salaryGradeId || null) : null,
                staffLevelId: v.scope === 'level' ? (v.staffLevelId || null) : null,
                amount: v.amount ?? null,
                rate: v.rate ?? null,
                coverageLimit: v.coverageLimit ?? null,
                isActive: v.isActive,
              });
            }}
            update={(policyId, rowId, values) => {
              const v = gradeValueSchema.parse(values);
              return benefitPolicyService.updateGradeValue(policyId, rowId, {
                id: rowId,
                salaryGradeId: v.scope === 'grade' ? (v.salaryGradeId || null) : null,
                staffLevelId: v.scope === 'level' ? (v.staffLevelId || null) : null,
                amount: v.amount ?? null,
                rate: v.rate ?? null,
                coverageLimit: v.coverageLimit ?? null,
                isActive: v.isActive,
              });
            }}
            remove={(policyId, rowId) => benefitPolicyService.removeGradeValue(policyId, rowId)}
            getId={(r) => r.id}
            columns={[
              {
                header: 'Applies to',
                cell: (r) => (
                  <span className="font-medium">
                    {gradeName(r.salaryGradeId) ?? levelName(r.staffLevelId) ?? '—'}
                  </span>
                ),
              },
              {
                header: 'Scope',
                cell: (r) => (r.salaryGradeId ? 'Salary grade' : 'Staff level'),
              },
              {
                header: 'Amount',
                cell: (r) => (r.amount == null ? '—' : formatMoney(r.amount, currency)),
                className: 'text-right',
              },
              {
                header: 'Rate',
                cell: (r) => (r.rate == null ? '—' : `${r.rate}%`),
                className: 'text-right',
              },
              {
                header: 'Coverage limit',
                cell: (r) =>
                  r.coverageLimit == null ? '—' : formatMoney(r.coverageLimit, currency),
                className: 'text-right',
              },
              { header: 'Status', cell: (r) => <StatusBadge active={r.isActive} /> },
            ]}
            schema={gradeValueSchema as any}
            emptyForm={emptyGradeValue}
            toForm={(r) => ({
              scope: r.salaryGradeId ? 'grade' : 'level',
              salaryGradeId: r.salaryGradeId ?? '',
              staffLevelId: r.staffLevelId ?? '',
              amount: r.amount ?? undefined,
              rate: r.rate ?? undefined,
              coverageLimit: r.coverageLimit ?? undefined,
              isActive: r.isActive,
            })}
            renderFields={(form) => {
              const scope = form.watch('scope');
              return (
                <>
                  <SelectField
                    form={form}
                    name="scope"
                    label="Band by"
                    required
                    options={[
                      { value: 'grade', label: 'Salary grade' },
                      { value: 'level', label: 'Staff level' },
                    ]}
                  />
                  {scope === 'grade' ? (
                    <SelectField
                      form={form}
                      name="salaryGradeId"
                      label="Salary grade"
                      required
                      options={gradeOptions}
                      placeholder={gradeOptions.length ? 'Select a grade…' : 'No salary grades'}
                    />
                  ) : (
                    <SelectField
                      form={form}
                      name="staffLevelId"
                      label="Staff level"
                      required
                      options={levelOptions}
                      placeholder={levelOptions.length ? 'Select a level…' : 'No staff levels'}
                    />
                  )}
                  <FieldRow>
                    <NumberField form={form} name="amount" label="Amount" step="0.01" />
                    <NumberField form={form} name="rate" label="Rate (%)" step="0.01" />
                  </FieldRow>
                  <NumberField
                    form={form}
                    name="coverageLimit"
                    label="Coverage limit"
                    step="0.01"
                  />
                  <SwitchField form={form} name="isActive" label="Active" />
                </>
              );
            }}
          />
        </TabsContent>

        <TabsContent value="relations" className="pt-4">
          <Card>
            <CardContent className="p-0">
              {(policy.relations?.length ?? 0) === 0 ? (
                <EmptyState
                  title="No dependant rules"
                  description="Which relations may be covered, and how many, is set on the policy itself."
                />
              ) : (
                <div className="divide-y">
                  {policy.relations.map((r) => (
                    <div key={r.id} className="flex items-center justify-between p-4 text-sm">
                      <span className="font-medium">{humanizeEnum(r.relationType)}</span>
                      <span className="text-muted-foreground">
                        {r.maxCount != null ? `max ${r.maxCount}` : 'no count limit'}
                        {r.maxAge != null ? ` · up to age ${r.maxAge}` : ''}
                      </span>
                      <StatusBadge active={r.isActive} />
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="terms" className="space-y-4 pt-4">
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Coverage</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
              <InfoRow
                label="Coverage limit"
                value={`${formatMoney(policy.coverageLimit, currency)} / ${humanizeEnum(policy.limitPeriod)}`}
              />
              <InfoRow label="Max dependants" value={policy.maxDependents ?? '—'} />
              <InfoRow label="Effective from" value={formatDate(policy.effectiveFrom)} />
              <InfoRow label="Effective to" value={formatDate(policy.effectiveTo)} />
              <InfoRow label="Mandatory" value={policy.isMandatory ? 'Yes' : 'No'} />
              <InfoRow label="Currency" value={currency} />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Valuation &amp; tax</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
              <InfoRow label="Delivery" value={humanizeEnum(policy.definition?.deliveryType)} />
              <InfoRow label="Frequency" value={humanizeEnum(policy.definition?.frequency)} />
              <InfoRow
                label="Calculation basis"
                value={humanizeEnum(policy.definition?.calculationBasis)}
              />
              <InfoRow
                label="Valuation method"
                value={humanizeEnum(policy.definition?.valuationMethod)}
              />
              <InfoRow
                label="Flat value"
                value={
                  policy.definition?.flatValue == null
                    ? '—'
                    : formatMoney(policy.definition.flatValue, currency)
                }
              />
              <InfoRow
                label="Valuation rate"
                value={
                  policy.definition?.valuationRate == null
                    ? '—'
                    : `${policy.definition.valuationRate}%`
                }
              />
              <InfoRow label="Taxable" value={policy.definition?.isTaxable ? 'Yes' : 'No'} />
              <InfoRow
                label="Tax treatment"
                value={humanizeEnum(policy.definition?.taxTreatment)}
              />
              <InfoRow
                label="Pensionable"
                value={policy.definition?.isPensionable ? 'Yes' : 'No'}
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-base">Contribution &amp; eligibility</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-2 gap-x-6 md:grid-cols-3">
              <InfoRow
                label="Who contributes"
                value={humanizeEnum(policy.definition?.contributionResponsibility)}
              />
              <InfoRow
                label="Employer rate"
                value={
                  policy.definition?.employerContributionRate == null
                    ? '—'
                    : `${policy.definition.employerContributionRate}%`
                }
              />
              <InfoRow
                label="Employee rate"
                value={
                  policy.definition?.employeeContributionRate == null
                    ? '—'
                    : `${policy.definition.employeeContributionRate}%`
                }
              />
              <InfoRow
                label="Min service"
                value={
                  policy.definition?.minServiceMonths == null
                    ? '—'
                    : `${policy.definition.minServiceMonths} months`
                }
              />
              <InfoRow
                label="During probation"
                value={policy.definition?.availableDuringProbation ? 'Available' : 'Not available'}
              />
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
