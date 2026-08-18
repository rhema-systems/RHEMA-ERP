'use client';

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { FileCheck2, Loader2, Paperclip, ScrollText, ShieldCheck, Upload } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { oathOfSecrecyService } from '@/services/hr/probation.service';
import type { OathOutstandingEmployee } from '@/types/hr/probation';
import { toast } from 'sonner';

const fmtDate = (v?: string | null) => (v ? new Date(v).toLocaleDateString() : '—');

/**
 * Oaths of secrecy (FR-HR-030).
 *
 * ⚠ **HR cannot affirm on anyone's behalf, and this screen offers no way to try.** The only thing
 * HR can do here is record an oath that was sworn on paper, which needs a named witness. The
 * employee's own affirmation lives on their tab and takes no employee id at all — the shape of the
 * API is the guarantee, and the screen should not imply otherwise by offering a person picker next
 * to an "affirm" button.
 */
export default function OathsOfSecrecyPage() {
  const qc = useQueryClient();
  const [recordFor, setRecordFor] = useState<OathOutstandingEmployee | null>(null);

  const { data: outstanding, isLoading } = useQuery({
    queryKey: ['oaths-outstanding'],
    queryFn: () => oathOfSecrecyService.getOutstanding(),
  });

  const { data: mine } = useQuery({
    queryKey: ['oaths-mine'],
    queryFn: () => oathOfSecrecyService.getMine(),
  });

  const refresh = () => {
    void qc.invalidateQueries({ queryKey: ['oaths-outstanding'] });
    void qc.invalidateQueries({ queryKey: ['oaths-mine'] });
  };

  const affirm = useMutation({
    mutationFn: () => oathOfSecrecyService.affirm({}),
    onSuccess: () => {
      toast.success('Oath affirmed');
      refresh();
    },
    onError: (e: unknown) => toast.error(e instanceof Error ? e.message : 'Refused'),
  });

  const myOath = mine?.[0];

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Oaths of secrecy"
        description="Who has sworn one, who has not, and the signed copies."
        backHref="/hr/probation"
      />

      <Tabs defaultValue="outstanding">
        <TabsList>
          <TabsTrigger value="outstanding">Outstanding ({outstanding?.length ?? 0})</TabsTrigger>
          <TabsTrigger value="mine">Mine</TabsTrigger>
        </TabsList>

        <TabsContent value="outstanding">
          <Card>
            <CardContent className="p-0">
              {isLoading ? (
                <div className="flex justify-center p-12">
                  <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
                </div>
              ) : !outstanding || outstanding.length === 0 ? (
                <EmptyState
                  icon={ShieldCheck}
                  title="Everyone has sworn one"
                  description="No active employee is missing an oath of secrecy."
                />
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Employee</TableHead>
                      <TableHead>Joined</TableHead>
                      <TableHead>On probation</TableHead>
                      <TableHead className="text-right">Action</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {outstanding.map((e) => (
                      <TableRow key={e.employeeId}>
                        <TableCell>
                          <div className="font-medium">{e.employeeName}</div>
                          <div className="text-xs text-muted-foreground">{e.employeeNumber}</div>
                        </TableCell>
                        <TableCell>{fmtDate(e.dateEmployed)}</TableCell>
                        <TableCell>
                          {/* The cohort onboarding is actually about — worth marking, because a
                              new hire without an oath is more urgent than a ten-year veteran. */}
                          {e.isOnProbation ? <Badge variant="secondary">Yes</Badge> : '—'}
                        </TableCell>
                        <TableCell className="text-right">
                          <Button size="sm" variant="outline" onClick={() => setRecordFor(e)}>
                            <FileCheck2 className="mr-2 h-4 w-4" />
                            Record paper oath
                          </Button>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="mine">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Your oath</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              {myOath ? (
                <>
                  <div className="flex flex-wrap items-center gap-3">
                    <Badge variant="secondary">{myOath.methodName}</Badge>
                    <span className="text-sm text-muted-foreground">
                      Sworn {fmtDate(myOath.swornOn)}
                      {myOath.witnessedByName ? ` before ${myOath.witnessedByName}` : ''}
                    </span>
                    {myOath.hasSignature && (
                      <Badge variant="outline">
                        <ShieldCheck className="mr-1 h-3 w-3" />
                        Signed in the system
                      </Badge>
                    )}
                  </div>
                  <p className="whitespace-pre-wrap rounded border bg-muted/40 p-3 text-sm">
                    {myOath.oathText}
                  </p>
                </>
              ) : (
                <>
                  <Alert>
                    <ScrollText className="h-4 w-4" />
                    <AlertDescription>
                      You have not sworn an oath of secrecy. Read the wording below and affirm it if
                      you agree; the date and your identity are recorded by the system.
                    </AlertDescription>
                  </Alert>
                  <Button disabled={affirm.isPending} onClick={() => affirm.mutate()}>
                    {affirm.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                    Affirm my oath
                  </Button>
                </>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

      <RecordPaperOathDialog
        employee={recordFor}
        onClose={() => setRecordFor(null)}
        onDone={refresh}
      />
    </div>
  );
}

function RecordPaperOathDialog({
  employee,
  onClose,
  onDone,
}: {
  employee: OathOutstandingEmployee | null;
  onClose: () => void;
  onDone: () => void;
}) {
  const [witnessId, setWitnessId] = useState('');
  const [swornOn, setSwornOn] = useState(() => new Date().toISOString().slice(0, 10));
  const [notes, setNotes] = useState('');
  const [file, setFile] = useState<File | null>(null);

  const record = useMutation({
    mutationFn: async (target: OathOutstandingEmployee) => {
      const oath = await oathOfSecrecyService.recordAdministered({
        employeeId: target.employeeId,
        witnessedById: witnessId,
        swornOn,
        notes: notes.trim() || undefined,
      });
      // ⚠ Two calls, and they cannot be merged. The JSON write carries no file — not a path and not
      // an upload id — because the only trustworthy route for a document is the multipart endpoint,
      // which runs the virus-scan gate and registers it centrally.
      if (file) await oathOfSecrecyService.attachScan(oath.id, file, 'Signed oath of secrecy');
      return oath;
    },
    onSuccess: () => {
      toast.success('Oath recorded');
      onDone();
      onClose();
    },
    onError: (e: unknown) => toast.error(e instanceof Error ? e.message : 'Refused'),
  });

  if (!employee) return null;

  return (
    <Dialog open onOpenChange={onClose}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Record a paper oath — {employee.employeeName}</DialogTitle>
        </DialogHeader>

        <div className="space-y-4">
          <Alert>
            <FileCheck2 className="h-4 w-4" />
            <AlertDescription>
              This records an oath sworn on paper. It is kept apart from an oath affirmed in the
              system: the attestation here is the witness and the signed copy.
            </AlertDescription>
          </Alert>

          <div className="space-y-2">
            <Label>Witnessed by (required)</Label>
            <EmployeePicker value={witnessId} onChange={(id) => setWitnessId(id ?? '')} />
          </div>

          <div className="space-y-2">
            <Label htmlFor="sworn">Date sworn</Label>
            {/* The date it was SWORN, not the date it is being keyed in — the API refuses a future
                date and one before the employee joined. */}
            <Input
              id="sworn"
              type="date"
              value={swornOn}
              onChange={(e) => setSwornOn(e.target.value)}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="scan">Signed copy (optional)</Label>
            <div className="flex items-center gap-2">
              <Input
                id="scan"
                type="file"
                onChange={(e) => setFile(e.target.files?.[0] ?? null)}
              />
              {file && <Paperclip className="h-4 w-4 text-muted-foreground" />}
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="oath-notes">Notes</Label>
            <Textarea
              id="oath-notes"
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              rows={2}
            />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button disabled={!witnessId || !swornOn || record.isPending} onClick={() => record.mutate(employee)}>
            {record.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Upload className="mr-2 h-4 w-4" />
            )}
            Record oath
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
