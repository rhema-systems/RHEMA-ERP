'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useMutation, useQuery } from '@tanstack/react-query';
import { AlertTriangle, Info, Loader2, Save, UserCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { probationService } from '@/services/hr/probation.service';
import { employeeService } from '@/services/hr/employee.service';
import { toast } from 'sonner';

/**
 * Open a probation period.
 *
 * ⚠ **The duration is not a field the user fills in.** FR-HR-031 sets it by staff category — senior
 * 6 months, junior 3 — and the API refuses a value that contradicts the category for permanent
 * staff. So the form reads the policy for the chosen employee and shows the resolved length,
 * offering an override only where the API would accept one.
 *
 * ⚠ **The confirming authority is shown before anything is saved.** If none is configured, that is
 * surfaced here rather than a month later when the reminder has nobody to route to.
 */
export default function NewProbationPage() {
  const router = useRouter();
  const [employeeId, setEmployeeId] = useState<string>('');
  const [contractDetailId, setContractDetailId] = useState<string>('');
  const [startDate, setStartDate] = useState<string>(() => new Date().toISOString().slice(0, 10));
  const [overrideMonths, setOverrideMonths] = useState<string>('');
  const [notes, setNotes] = useState<string>('');

  const { data: policy, isFetching: policyLoading } = useQuery({
    queryKey: ['probation-policy', employeeId],
    queryFn: () => probationService.getPolicyForEmployee(employeeId),
    enabled: !!employeeId,
  });

  // ⚠ ProbationPeriod.ContractDetailId is required by the API, and most employees have no contract
  // row: only 4 of 2,351 on the reference tenant. The picker therefore has to show what is
  // available and say plainly when there is nothing to choose, rather than silently submitting an
  // empty id and surfacing a foreign-key error.
  const { data: contracts, isFetching: contractsLoading } = useQuery({
    queryKey: ['employee-contracts', employeeId],
    queryFn: () => employeeService.getContracts(employeeId),
    enabled: !!employeeId,
  });

  const create = useMutation({
    mutationFn: () =>
      probationService.create({
        employeeId,
        contractDetailId,
        startDate,
        // Omitted unless the user deliberately overrode it — omitting is what applies the category
        // length, and sending the same number back is merely a slower way of agreeing.
        durationMonths: overrideMonths ? Number(overrideMonths) : undefined,
        outcomeNotes: notes.trim() || undefined,
      }),
    onSuccess: (created) => {
      toast.success('Probation opened');
      router.push(`/hr/probation/${created.id}`);
    },
    onError: (e: unknown) => {
      toast.error(e instanceof Error ? e.message : 'Could not open the probation');
    },
  });

  const contractList = contracts ?? [];
  const canSubmit = !!employeeId && !!contractDetailId && !!startDate && !create.isPending;

  const expected = policy?.expectedDurationMonths;
  const effectiveMonths = overrideMonths ? Number(overrideMonths) : expected;
  const endDate =
    startDate && effectiveMonths
      ? new Date(new Date(startDate).setMonth(new Date(startDate).getMonth() + effectiveMonths))
          .toISOString()
          .slice(0, 10)
      : null;

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Open a probation period"
        description="The length comes from the employee's staff category; you should rarely need to change it."
        backHref="/hr/probation"
      />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Who</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label>Employee</Label>
            <EmployeePicker
              value={employeeId}
              onChange={(id) => {
                setEmployeeId(id ?? '');
                setContractDetailId('');
                setOverrideMonths('');
              }}
            />
          </div>

          {employeeId && (
            <div className="space-y-2">
              <Label>Contract</Label>
              {contractsLoading ? (
                <p className="text-sm text-muted-foreground">Looking for contracts…</p>
              ) : contractList.length === 0 ? (
                <Alert variant="destructive">
                  <AlertTriangle className="h-4 w-4" />
                  <AlertDescription>
                    This employee has no contract on record, and a probation must reference one. Add
                    a contract on their employee record first.
                  </AlertDescription>
                </Alert>
              ) : (
                <div className="space-y-2">
                  {contractList.map((c) => (
                    <label
                      key={c.id}
                      className="flex cursor-pointer items-center gap-3 rounded-md border p-3 text-sm"
                    >
                      <input
                        type="radio"
                        name="contract"
                        checked={contractDetailId === c.id}
                        onChange={() => setContractDetailId(c.id)}
                      />
                      <span className="font-medium">{c.contractNumber}</span>
                      <span className="text-muted-foreground">
                        {c.employmentType} · from {c.startDate}
                      </span>
                    </label>
                  ))}
                </div>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {employeeId && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">How long, and who confirms it</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            {policyLoading ? (
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" /> Resolving the policy…
              </div>
            ) : policy ? (
              <>
                <Alert>
                  <Info className="h-4 w-4" />
                  <AlertDescription>
                    <strong>{policy.expectedDurationMonths} months</strong>
                    {policy.staffLevelName ? ` for ${policy.staffLevelName}` : ''} —{' '}
                    {policy.source === 'Position'
                      ? 'from the position record'
                      : 'from the tenant default, because the position carries no probation length'}
                    .{' '}
                    {policy.isEnforced
                      ? 'This is enforced for permanent staff: a different length will be refused.'
                      : `${policy.employmentTypeName} staff are governed by their contract, so a different length is accepted.`}
                  </AlertDescription>
                </Alert>

                {!policy.isEnforced && (
                  <div className="space-y-2">
                    <Label htmlFor="months">Length in months (optional override)</Label>
                    <Input
                      id="months"
                      type="number"
                      min={1}
                      max={24}
                      value={overrideMonths}
                      onChange={(e) => setOverrideMonths(e.target.value)}
                      placeholder={String(policy.expectedDurationMonths)}
                    />
                  </div>
                )}

                {policy.confirmingAuthorityName ? (
                  <p className="flex items-center gap-2 text-sm text-muted-foreground">
                    <UserCheck className="h-4 w-4" />
                    <span>
                      <strong>{policy.confirmingAuthorityName}</strong> will confirm this probation
                      {policy.confirmingAuthorityScope ? ` (${policy.confirmingAuthorityScope})` : ''}.
                    </span>
                  </p>
                ) : (
                  // ⚠ Shown, not hidden. An unconfigured tenant is visible here rather than a month
                  // later when the month-5 form has nobody to go to.
                  <Alert variant="destructive">
                    <AlertTriangle className="h-4 w-4" />
                    <AlertDescription>
                      No confirming authority covers this employee, so the confirmation form will
                      have nobody to route to. Set one under Administration → HR → Probation.
                    </AlertDescription>
                  </Alert>
                )}
              </>
            ) : null}

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="start">Start date</Label>
                <Input
                  id="start"
                  type="date"
                  value={startDate}
                  onChange={(e) => setStartDate(e.target.value)}
                />
              </div>
              <div className="space-y-2">
                <Label>Ends</Label>
                <Input value={endDate ?? '—'} readOnly disabled />
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="notes">Notes (optional)</Label>
              <Textarea
                id="notes"
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                rows={3}
                placeholder="Anything that should sit with the record."
              />
            </div>
          </CardContent>
        </Card>
      )}

      <div className="flex justify-end gap-2">
        <Button variant="outline" onClick={() => router.push('/hr/probation')}>
          Cancel
        </Button>
        <Button disabled={!canSubmit} onClick={() => create.mutate()}>
          {create.isPending ? (
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
          ) : (
            <Save className="mr-2 h-4 w-4" />
          )}
          Open probation
        </Button>
      </div>
    </div>
  );
}
