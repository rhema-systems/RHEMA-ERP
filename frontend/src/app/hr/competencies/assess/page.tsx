'use client';

import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ClipboardCheck, Loader2, Search, TriangleAlert } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { useToast } from '@/hooks/use-toast';
import { jobArchitectureService } from '@/services/hr/job-architecture.service';
import { employeeService } from '@/services/hr/employee.service';
import type { BatchAssessmentResult } from '@/types/hr/job-architecture';

/**
 * Records where one employee stands against the competency catalogue, several at a time.
 *
 * ⚠ **Nothing could assess anybody.** `/hr/competencies` reports who is "not assessed" — as a column
 * kept deliberately separate from "below requirement", because an unknown is not a shortfall — and
 * there was no screen anywhere that could turn an unknown into a level. `assessEmployee`,
 * `reassessEmployee` and `batch-assess` all existed and none had a caller, so the gaps report named
 * work the product could not do.
 *
 * ⚠ **One call, one row per competency, and every skipped row is shown.** The endpoint returns
 * `succeeded`, `failed` and an `errors` list rather than throwing, so a partial result is the normal
 * case. Reporting "saved" over a batch where three rows failed would be the same defect lane 2 found
 * in bulk training completion.
 *
 * ⚠ The assessor is the signed-in user's employee record. An administrator whose account is not
 * linked to one is refused with a 400 — which the page says plainly rather than showing as a
 * generic error, because it is an account problem and not something the form can fix.
 */
export default function AssessCompetenciesPage() {
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const [search, setSearch] = useState('');
  const [employeeId, setEmployeeId] = useState('');
  const [method, setMethod] = useState('Manager assessment');
  const [levels, setLevels] = useState<Record<string, string>>({});
  const [notes, setNotes] = useState<Record<string, string>>({});
  const [result, setResult] = useState<BatchAssessmentResult | null>(null);

  const { data: employees } = useQuery({
    queryKey: ['hr', 'employees', 'competency-assess', search],
    queryFn: () => employeeService.searchPaged({ searchTerm: search || undefined }, 1, 20),
  });

  const { data: catalogue } = useQuery({
    queryKey: ['competencies', 'catalogue'],
    queryFn: () => jobArchitectureService.getCompetencies(),
  });

  const { data: held } = useQuery({
    queryKey: ['competencies', 'employee', employeeId],
    queryFn: () => jobArchitectureService.getEmployeeCompetencies(employeeId),
    enabled: !!employeeId,
  });

  const chosen = (employees?.items ?? []).find((e) => e.id === employeeId) ?? null;

  /**
   * One row per catalogue competency, carrying the employee's existing assessment when there is
   * one. ⚠ The existing row's id is what turns a first assessment into a RE-assessment — sending
   * `competencyId` for a competency they already hold would try to create a second record.
   */
  const rows = useMemo(() => {
    const heldByCompetency = new Map((held ?? []).map((h) => [h.competencyId, h]));
    return (catalogue ?? []).map((c) => ({
      competency: c,
      existing: heldByCompetency.get(c.id) ?? null,
    }));
  }, [catalogue, held]);

  const staged = rows.filter((r) => {
    const v = levels[r.competency.id];
    return v !== undefined && v !== '' && Number(v) !== (r.existing?.currentProficiencyLevel ?? -1);
  });

  const submit = useMutation({
    mutationFn: () =>
      jobArchitectureService.batchAssess(
        employeeId,
        staged.map((r) => ({
          employeeCompetencyId: r.existing?.id ?? null,
          competencyId: r.existing ? null : r.competency.id,
          newLevel: Number(levels[r.competency.id]),
          assessmentMethod: method.trim() || 'Manager assessment',
          evidenceNotes: notes[r.competency.id]?.trim() || null,
          changeReason: r.existing ? 'Re-assessed' : null,
        })),
      ),
    onSuccess: (r) => {
      setResult(r);
      setLevels({});
      setNotes({});
      queryClient.invalidateQueries({ queryKey: ['competencies', 'employee', employeeId] });
      queryClient.invalidateQueries({ queryKey: ['competencies', 'organisation-gaps'] });
      // ⚠ Reports what actually landed. `failed > 0` is not an error state — the successful rows
      // are saved and the failures are listed below rather than swallowed.
      toast({
        title: r.failed === 0
          ? `${r.succeeded} assessment${r.succeeded === 1 ? '' : 's'} recorded`
          : `${r.succeeded} recorded, ${r.failed} could not be`,
        description: r.failed === 0 ? undefined : 'The rows that failed are listed with their reasons.',
        variant: r.failed === 0 ? undefined : 'destructive',
      });
    },
    onError: (e: any) =>
      toast({
        variant: 'destructive',
        title: 'Nothing was recorded',
        description: e?.body?.detail ?? e?.body?.message ?? e?.message,
      }),
  });

  return (
    <div className="space-y-6">
      <PageHeader
        title="Assess competencies"
        description="Record where somebody stands against the catalogue. Several at once; each is kept with its own history."
        backHref="/hr/competencies"
      />

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Who</CardTitle>
          <CardDescription>
            The assessment is recorded against you as the assessor.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="relative">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              className="pl-9"
              placeholder="Search by name or staff number"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
          <div className="flex flex-wrap gap-2">
            {(employees?.items ?? []).slice(0, 12).map((e) => (
              <Button
                key={e.id}
                size="sm"
                variant={e.id === employeeId ? 'default' : 'outline'}
                onClick={() => { setEmployeeId(e.id); setResult(null); setLevels({}); }}
              >
                {e.firstName} {e.lastName}
                {e.employeeNumber && <span className="ml-2 opacity-70">{e.employeeNumber}</span>}
              </Button>
            ))}
          </div>
          {!employeeId && (
            <p className="text-xs text-muted-foreground">Choose somebody to begin.</p>
          )}
        </CardContent>
      </Card>

      {employeeId && (
        <>
          <Card>
            <CardHeader>
              <CardTitle className="text-base">How it was assessed</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="max-w-md space-y-2">
                <Label htmlFor="method">Method</Label>
                <Input
                  id="method"
                  value={method}
                  onChange={(e) => setMethod(e.target.value)}
                  placeholder="Manager assessment, test, observed…"
                />
                <p className="text-xs text-muted-foreground">
                  Recorded on every row in this batch. Required by the server.
                </p>
              </div>
            </CardContent>
          </Card>

          {result && result.failed > 0 && (
            <Alert variant="destructive">
              <TriangleAlert className="h-4 w-4" />
              <AlertTitle>{result.succeeded} recorded, {result.failed} not</AlertTitle>
              <AlertDescription>
                <ul className="mt-2 list-disc space-y-1 pl-4 text-sm">
                  {result.errors.map((err, i) => (
                    <li key={`${err.competencyId ?? err.employeeCompetencyId ?? i}`}>
                      {(catalogue ?? []).find((c) => c.id === err.competencyId)?.name
                        ?? err.competencyId
                        ?? 'A row'}
                      {' — '}{err.errorMessage}
                    </li>
                  ))}
                </ul>
              </AlertDescription>
            </Alert>
          )}

          <Card>
            <CardHeader>
              <CardTitle className="text-base">
                {chosen ? `${chosen.firstName} ${chosen.lastName}` : 'Levels'}
              </CardTitle>
              <CardDescription>
                Leave a level blank to say nothing about it. A level that matches what is already
                recorded is not sent — an assessment that changes nothing is not an assessment.
              </CardDescription>
            </CardHeader>
            <CardContent className="p-0">
              {rows.length === 0 ? (
                <EmptyState
                  icon={ClipboardCheck}
                  title="The catalogue is empty"
                  description="Competencies are defined under Administration → HR → Competencies."
                />
              ) : (
                <div className="overflow-x-auto">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>Competency</TableHead>
                        <TableHead>Category</TableHead>
                        <TableHead className="w-[130px]">Held</TableHead>
                        <TableHead className="w-[130px]">New level</TableHead>
                        <TableHead>Evidence</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {rows.map(({ competency, existing }) => (
                        <TableRow key={competency.id}>
                          <TableCell className="font-medium">
                            {competency.name}
                            <div className="text-xs text-muted-foreground">{competency.code}</div>
                          </TableCell>
                          <TableCell className="text-muted-foreground">
                            {competency.competencyCategory}
                          </TableCell>
                          <TableCell>
                            {existing ? (
                              <Badge variant="secondary">
                                {existing.currentProficiencyLevel} / {existing.proficiencyScaleMax}
                              </Badge>
                            ) : (
                              <span className="text-xs text-muted-foreground">Not assessed</span>
                            )}
                          </TableCell>
                          <TableCell>
                            <Input
                              type="number"
                              min={1}
                              max={existing?.proficiencyScaleMax ?? competency.proficiencyScaleMax ?? 10}
                              value={levels[competency.id] ?? ''}
                              onChange={(e) =>
                                setLevels((l) => ({ ...l, [competency.id]: e.target.value }))
                              }
                            />
                          </TableCell>
                          <TableCell>
                            <Input
                              placeholder="Optional"
                              value={notes[competency.id] ?? ''}
                              onChange={(e) =>
                                setNotes((n) => ({ ...n, [competency.id]: e.target.value }))
                              }
                            />
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              )}
            </CardContent>
          </Card>

          <div className="flex items-center justify-between gap-4">
            <p className="text-sm text-muted-foreground">
              {staged.length === 0
                ? 'Nothing to record yet.'
                : `${staged.length} to record.`}
            </p>
            <Button
              onClick={() => submit.mutate()}
              disabled={staged.length === 0 || submit.isPending}
            >
              {submit.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Record {staged.length > 0 ? staged.length : ''} assessment{staged.length === 1 ? '' : 's'}
            </Button>
          </div>
        </>
      )}
    </div>
  );
}
