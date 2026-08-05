'use client';

import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { Wallet, Loader2 } from 'lucide-react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { ResourceCollectionTab } from '@/components/hr/common/ResourceCollectionTab';
import {
  NumberField,
  DateField,
  SelectField,
  SwitchField,
  FieldRow,
} from '@/components/hr/employee/tabs/fields';
import { payComponentService, emolumentService } from '@/services/hr/compensation.service';
import { formatDate, formatMoney, humanizeEnum, today } from '@/lib/hr/attendance-format';
import type { EmployeePayComponent } from '@/types/hr/compensation';

/**
 * An employee's emolument package: the resolved roll-up, and the employee-level rows that
 * produced it.
 *
 * The two tabs answer different questions. "Package" is what the employee actually gets as of a
 * date — position defaults with employee rows applied on top and percentages already turned into
 * money — and each line names where it came from. "Overrides" is only the employee-level rows,
 * which either replace a position default for the same component or add one the position does
 * not carry.
 */
const overrideSchema = z
  .object({
    payComponentId: z.string().min(1, 'Select a component'),
    amount: z.coerce.number().min(0),
    effectiveFrom: z.string().min(1, 'Required'),
    effectiveTo: z.string().optional(),
    isActive: z.boolean(),
  })
  .refine((v) => !v.effectiveTo || v.effectiveTo >= v.effectiveFrom, {
    message: 'The effective-to date cannot be before the effective-from date',
    path: ['effectiveTo'],
  });

type OverrideForm = z.input<typeof overrideSchema>;

const toDateInput = (v?: string | null) => (v ? v.slice(0, 10) : '');

function Money({ label, value, tone }: { label: string; value: number; tone?: 'up' | 'down' }) {
  return (
    <Card>
      <CardContent className="p-4">
        <p className="text-xs text-muted-foreground">{label}</p>
        <p
          className={`mt-1 text-2xl font-semibold ${
            tone === 'down' ? 'text-red-600' : tone === 'up' ? 'text-emerald-600' : ''
          }`}
        >
          {formatMoney(value)}
        </p>
      </CardContent>
    </Card>
  );
}

export default function EmployeeEmolumentsPage() {
  const [employeeId, setEmployeeId] = useState<string | null>(null);
  const [employeeLabel, setEmployeeLabel] = useState<string | null>(null);
  const [asOf, setAsOf] = useState(today());

  const { data: components } = useQuery({
    queryKey: ['hr', 'pay-components', true],
    queryFn: () => payComponentService.getAll(true),
  });

  const { data: summary, isLoading: loadingSummary } = useQuery({
    queryKey: ['hr', 'emolument-summary', employeeId, asOf],
    queryFn: () => emolumentService.getEmployeeSummary(employeeId ?? '', asOf),
    enabled: !!employeeId,
  });

  const componentOptions = useMemo(
    () =>
      (components ?? []).map((c) => ({
        value: c.id,
        label: `${c.code} · ${c.name} (${humanizeEnum(c.componentType)})`,
      })),
    [components],
  );

  const emptyOverride: OverrideForm = {
    payComponentId: '',
    amount: 0,
    effectiveFrom: asOf,
    effectiveTo: '',
    isActive: true,
  };

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Employee Emoluments"
        description="The resolved pay package — basic, allowances and deductions — and the employee-level overrides behind it."
        backHref="/hr"
      />

      <Card>
        <CardHeader>
          <CardTitle>Employee</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid gap-4 md:grid-cols-2">
            <div className="space-y-2">
              <Label>Employee</Label>
              <EmployeePicker
                value={employeeId}
                initialLabel={employeeLabel}
                onChange={(id, label) => {
                  setEmployeeId(id);
                  setEmployeeLabel(label);
                }}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="asOf">As of</Label>
              <Input
                id="asOf"
                type="date"
                value={asOf}
                onChange={(e) => setAsOf(e.target.value)}
              />
              <p className="text-xs text-muted-foreground">
                Only components effective on this date are counted.
              </p>
            </div>
          </div>
        </CardContent>
      </Card>

      {!employeeId ? (
        <Card>
          <CardContent className="p-0">
            <EmptyState
              icon={Wallet}
              title="Choose an employee"
              description="Emoluments are resolved per employee, as of a date."
            />
          </CardContent>
        </Card>
      ) : (
        <Tabs defaultValue="package">
          <TabsList>
            <TabsTrigger value="package">Package</TabsTrigger>
            <TabsTrigger value="overrides">Employee overrides</TabsTrigger>
          </TabsList>

          <TabsContent value="package" className="space-y-4 pt-4">
            {loadingSummary ? (
              <div className="flex items-center justify-center py-16">
                <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
              </div>
            ) : !summary ? (
              <EmptyState
                title="No package resolved"
                description="This employee has no salary assignment or emolument components on that date."
              />
            ) : (
              <>
                <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
                  <Money label="Monthly basic" value={summary.monthlyBasicPay} />
                  <Money label="Allowances" value={summary.totalAllowances} tone="up" />
                  <Money label="Deductions" value={summary.totalDeductions} tone="down" />
                  <Money label="Gross monthly" value={summary.grossMonthly} />
                  <Money label="Net monthly" value={summary.netMonthly} />
                </div>

                <Card>
                  <CardHeader>
                    <CardTitle className="text-base">
                      Components as of {formatDate(summary.asOfDate)}
                      {summary.positionTitle ? ` · ${summary.positionTitle}` : ''}
                    </CardTitle>
                  </CardHeader>
                  <CardContent className="p-0">
                    {summary.components.length === 0 ? (
                      <EmptyState
                        title="No components"
                        description="Only basic pay applies on this date."
                      />
                    ) : (
                      <Table>
                        <TableHeader>
                          <TableRow>
                            <TableHead>Code</TableHead>
                            <TableHead>Component</TableHead>
                            <TableHead>Type</TableHead>
                            <TableHead>Basis</TableHead>
                            <TableHead className="text-right">Amount</TableHead>
                            <TableHead>Taxable</TableHead>
                            <TableHead>Source</TableHead>
                          </TableRow>
                        </TableHeader>
                        <TableBody>
                          {summary.components.map((c) => (
                            <TableRow key={c.payComponentId}>
                              <TableCell className="font-medium">{c.code}</TableCell>
                              <TableCell>{c.name}</TableCell>
                              <TableCell>{humanizeEnum(c.componentType)}</TableCell>
                              <TableCell className="text-muted-foreground">
                                {humanizeEnum(c.calculationBasis)}
                              </TableCell>
                              <TableCell
                                className={`text-right ${
                                  c.componentType === 'Deduction' ? 'text-red-600' : ''
                                }`}
                              >
                                {c.componentType === 'Deduction' ? '−' : ''}
                                {formatMoney(c.amount)}
                              </TableCell>
                              <TableCell>{c.isTaxable ? 'Yes' : 'No'}</TableCell>
                              <TableCell>
                                {/* Employee rows beat position defaults; naming the source is the
                                    only way to tell an override from an inherited line. */}
                                <Badge
                                  variant={c.source === 'Employee' ? 'default' : 'outline'}
                                >
                                  {c.source}
                                </Badge>
                              </TableCell>
                            </TableRow>
                          ))}
                        </TableBody>
                      </Table>
                    )}
                  </CardContent>
                </Card>
              </>
            )}
          </TabsContent>

          <TabsContent value="overrides" className="pt-4">
            <ResourceCollectionTab<EmployeePayComponent, OverrideForm>
              parentId={employeeId}
              title="overrides"
              singular="override"
              queryKey={['hr', 'employee-emoluments', employeeId]}
              invalidateKeys={[['hr', 'emolument-summary', employeeId, asOf]]}
              dialogHint="Replaces the position default for the same component, or adds one the position does not carry."
              emptyDescription="This employee inherits their position's components unchanged."
              list={(id) => emolumentService.getEmployeeComponents(id)}
              create={(id, values) => {
                const v = overrideSchema.parse(values);
                return emolumentService.assignEmployeeComponent({
                  employeeId: id,
                  payComponentId: v.payComponentId,
                  amount: v.amount,
                  effectiveFrom: v.effectiveFrom,
                  effectiveTo: v.effectiveTo || null,
                });
              }}
              update={(id, rowId, values) => {
                const v = overrideSchema.parse(values);
                return emolumentService.updateEmployeeComponent(rowId, {
                  id: rowId,
                  employeeId: id,
                  payComponentId: v.payComponentId,
                  amount: v.amount,
                  effectiveFrom: v.effectiveFrom,
                  effectiveTo: v.effectiveTo || null,
                  isActive: v.isActive,
                });
              }}
              remove={(_id, rowId) => emolumentService.removeEmployeeComponent(rowId)}
              getId={(r) => r.id}
              columns={[
                {
                  header: 'Component',
                  cell: (r) => <span className="font-medium">{r.payComponentName}</span>,
                },
                { header: 'Type', cell: (r) => humanizeEnum(r.componentType) },
                {
                  header: 'Amount',
                  cell: (r) => formatMoney(r.amount),
                  className: 'text-right',
                },
                { header: 'From', cell: (r) => formatDate(r.effectiveFrom) },
                { header: 'To', cell: (r) => formatDate(r.effectiveTo) },
                { header: 'Status', cell: (r) => <StatusBadge active={r.isActive} /> },
              ]}
              schema={overrideSchema as any}
              emptyForm={emptyOverride}
              toForm={(r) => ({
                payComponentId: r.payComponentId,
                amount: r.amount,
                effectiveFrom: toDateInput(r.effectiveFrom),
                effectiveTo: toDateInput(r.effectiveTo),
                isActive: r.isActive,
              })}
              renderFields={(form) => (
                <>
                  <SelectField
                    form={form}
                    name="payComponentId"
                    label="Pay component"
                    required
                    options={componentOptions}
                    placeholder={
                      componentOptions.length ? 'Select a component…' : 'No active pay components'
                    }
                  />
                  <NumberField form={form} name="amount" label="Amount" step="0.01" required />
                  <FieldRow>
                    <DateField form={form} name="effectiveFrom" label="Effective from" required />
                    <DateField form={form} name="effectiveTo" label="Effective to" />
                  </FieldRow>
                  <SwitchField form={form} name="isActive" label="Active" />
                </>
              )}
            />
          </TabsContent>
        </Tabs>
      )}
    </div>
  );
}
