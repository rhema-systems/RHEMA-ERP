'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Loader2, ShieldQuestion, Send, Lock, ArrowRightLeft, EyeOff } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Badge } from '@/components/ui/badge';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table';
import { PageHeader } from '@/components/hr/common/PageHeader';
import { EmptyState } from '@/components/hr/common/EmptyState';
import { StatusBadge } from '@/components/hr/common/StatusBadge';
import { EmployeePicker } from '@/components/hr/common/EmployeePicker';
import { useToast } from '@/hooks/use-toast';
import { employeeRelationsConcernService } from '@/services/hr/employee-relations-admin.service';
import {
  CONCERN_CATEGORY_OPTIONS, CONCERN_STATUS_OPTIONS, CONCERN_TRIAGE_STATUS_OPTIONS,
  ER_OPENABLE_CASE_TYPES,
  type ConcernStatus, type EmployeeRelationsConcern, type EmployeeRelationsCaseType,
} from '@/types/hr/employee-relations';

const fmtDateTime = (v?: string | null) => (v ? new Date(v).toLocaleString() : '—');
const catLabel = (v: string) => CONCERN_CATEGORY_OPTIONS.find((c) => c.value === v)?.label ?? v;
const statusLabel = (v: string) => CONCERN_STATUS_OPTIONS.find((s) => s.value === v)?.label ?? v;

type OpenableCaseType = Exclude<EmployeeRelationsCaseType, 'Grievance'>;

/**
 * The anonymous-concern inbox — area 9c slice 6.
 *
 * ⚠ **Nothing on this screen identifies a reporter, because nothing in the payload does.** There is
 * no employee id and no created-by anywhere on a concern; the reporter's posts on the thread carry
 * no author at all. That is the feature, not an omission — a whistleblowing channel that recorded
 * who used it would not be one.
 *
 * ⚠ **The thread is the point.** The commonest outcome of an anonymous report is that HR needs one
 * more detail, and without a reply channel this would be a suggestion box. The reporter reads and
 * answers here using a retrieval code that was shown to them once and cannot be reissued — not even
 * by HR, so there is deliberately no control on this screen to recover one.
 */
export default function ConcernInboxPage() {
  const queryClient = useQueryClient();
  const { toast } = useToast();

  const [status, setStatus] = useState<ConcernStatus | 'all'>('all');
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const [triageOpen, setTriageOpen] = useState(false);
  const [triageNotes, setTriageNotes] = useState('');
  const [triageStatus, setTriageStatus] = useState<'UnderTriage' | 'UnderReview' | 'Closed'>('UnderReview');

  const [replyBody, setReplyBody] = useState('');

  const [closeOpen, setCloseOpen] = useState(false);
  const [closeReason, setCloseReason] = useState('');

  const [convertOpen, setConvertOpen] = useState(false);
  const [convertType, setConvertType] = useState<OpenableCaseType>('ConflictMediation');
  const [convertEmployee, setConvertEmployee] = useState<string | null>(null);

  const { data: concerns, isLoading } = useQuery({
    queryKey: ['hr', 'employee-relations', 'concerns', status],
    queryFn: () => employeeRelationsConcernService.getAll(status === 'all' ? undefined : status),
  });

  /**
   * ⚠ The selected concern is READ BACK BY ID, not held as a snapshot out of the list.
   *
   * The list row and the detail are the same shape here, so keeping the clicked object was
   * tempting — and wrong: the moment anything else refetches the list, the pane goes on showing a
   * thread that has moved on. A reply the reporter posted while HR had the concern open would
   * simply not appear. One id, one authoritative read.
   */
  const { data: selected } = useQuery({
    queryKey: ['hr', 'employee-relations', 'concerns', 'detail', selectedId],
    queryFn: () => employeeRelationsConcernService.getById(selectedId as string),
    enabled: !!selectedId,
  });

  const afterWrite = (updated: EmployeeRelationsConcern) => {
    queryClient.setQueryData(['hr', 'employee-relations', 'concerns', 'detail', updated.id], updated);
    queryClient.invalidateQueries({ queryKey: ['hr', 'employee-relations', 'concerns'] });
  };

  const triage = useMutation({
    mutationFn: (v: { id: string }) =>
      employeeRelationsConcernService.triage(v.id, { notes: triageNotes.trim(), status: triageStatus }),
    onSuccess: (u) => { afterWrite(u); setTriageOpen(false); setTriageNotes(''); },
    onError: (e: any) => toast({ title: 'Not triaged', description: e?.message, variant: 'destructive' }),
  });

  const reply = useMutation({
    mutationFn: (v: { id: string }) =>
      employeeRelationsConcernService.reply(v.id, { body: replyBody.trim() }),
    onSuccess: (u) => { afterWrite(u); setReplyBody(''); },
    onError: (e: any) => toast({ title: 'Not sent', description: e?.message, variant: 'destructive' }),
  });

  const closeConcern = useMutation({
    mutationFn: (v: { id: string }) =>
      employeeRelationsConcernService.close(v.id, { reason: closeReason.trim() }),
    onSuccess: (u) => { afterWrite(u); setCloseOpen(false); setCloseReason(''); },
    onError: (e: any) => toast({ title: 'Not closed', description: e?.message, variant: 'destructive' }),
  });

  const convert = useMutation({
    mutationFn: (v: { id: string; employeeId: string }) =>
      employeeRelationsConcernService.convert(v.id, { caseType: convertType, employeeId: v.employeeId }),
    onSuccess: (u) => { afterWrite(u); setConvertOpen(false); setConvertEmployee(null); },
    onError: (e: any) => toast({ title: 'Not converted', description: e?.message, variant: 'destructive' }),
  });

  const rows = concerns ?? [];
  const untriaged = rows.filter((c) => c.status === 'New').length;
  const isFinished = (c: EmployeeRelationsConcern) => c.status === 'Closed' || c.status === 'ConvertedToCase';

  return (
    <div className="space-y-6 p-6">
      <PageHeader
        title="Concerns reported anonymously"
        description="Reports made without a name, and the thread HR uses to ask for more. Nothing here records who reported — that is the guarantee, not an oversight."
        backHref="/hr/employee-relations"
      />

      <div className="flex flex-wrap items-end gap-2">
        <div>
          <Label className="text-xs text-muted-foreground">Status</Label>
          <Select value={status} onValueChange={(v) => setStatus(v as ConcernStatus | 'all')}>
            <SelectTrigger className="w-56"><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All statuses</SelectItem>
              {CONCERN_STATUS_OPTIONS.map((o) => (
                <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        {untriaged > 0 && (
          <Badge variant="outline" className="mb-2">
            {untriaged} nobody has looked at yet
          </Badge>
        )}
      </div>

      <div className="grid gap-4 lg:grid-cols-5">
        <Card className="lg:col-span-2">
          <CardContent className="p-0">
            {isLoading ? (
              <div className="flex items-center justify-center p-10">
                <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
              </div>
            ) : rows.length === 0 ? (
              <EmptyState title="Nothing reported" description="No concern has been raised through the anonymous channel." />
            ) : (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Reference</TableHead>
                    <TableHead>Category</TableHead>
                    <TableHead>Reported</TableHead>
                    <TableHead>Status</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {rows.map((c) => (
                    <TableRow
                      key={c.id}
                      className={`cursor-pointer ${selectedId === c.id ? 'bg-muted/50' : ''}`}
                      onClick={() => setSelectedId(c.id)}
                    >
                      <TableCell className="font-medium">{c.concernNumber}</TableCell>
                      <TableCell>{catLabel(c.category)}</TableCell>
                      <TableCell className="text-xs">{fmtDateTime(c.reportedAt)}</TableCell>
                      <TableCell><StatusBadge status={statusLabel(c.status)} /></TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </CardContent>
        </Card>

        <div className="space-y-4 lg:col-span-3">
          {!selected ? (
            <Card>
              <CardContent className="p-0">
                <EmptyState
                  title="Pick a concern"
                  description="Its statement and the thread with the reporter appear here."
                />
              </CardContent>
            </Card>
          ) : (
            <>
              <Card>
                <CardHeader className="flex flex-row items-start justify-between">
                  <div>
                    <CardTitle className="text-base">
                      {selected.concernNumber} · {selected.subject}
                    </CardTitle>
                    <p className="mt-1 text-xs text-muted-foreground">
                      {catLabel(selected.category)} · reported {fmtDateTime(selected.reportedAt)}
                    </p>
                  </div>
                  <StatusBadge status={statusLabel(selected.status)} />
                </CardHeader>
                <CardContent className="space-y-4">
                  <p className="whitespace-pre-wrap text-sm">{selected.statement}</p>

                  {/* Said plainly, because a reader will otherwise look for the name. */}
                  <div className="flex items-start gap-2 rounded-md border border-dashed p-3 text-xs text-muted-foreground">
                    <EyeOff className="mt-0.5 h-3 w-3 shrink-0" />
                    <span>
                      Nobody is recorded against this report. It cannot be traced to a person from
                      here or from the database, and the reporter&apos;s retrieval code cannot be
                      recovered or reissued — not even by HR.
                    </span>
                  </div>

                  {selected.triagedAt && (
                    <div className="rounded-md border p-3 text-sm">
                      <div className="text-xs uppercase tracking-wide text-muted-foreground">
                        Triage · {selected.triagedByName} · {fmtDateTime(selected.triagedAt)}
                      </div>
                      <p className="mt-1 whitespace-pre-wrap">{selected.triageNotes}</p>
                    </div>
                  )}

                  {selected.closedAt && (
                    <div className="rounded-md border border-dashed p-3 text-sm">
                      <span className="font-medium">Closed {fmtDateTime(selected.closedAt)}</span>
                      {selected.closureReason ? ` — ${selected.closureReason}` : ''}
                      {selected.closedByName ? ` (${selected.closedByName})` : ''}
                    </div>
                  )}

                  {selected.convertedCaseId && (
                    <div className="rounded-md border p-3 text-sm">
                      Became case{' '}
                      <Link
                        href={`/hr/employee-relations/${selected.convertedCaseId}`}
                        className="font-medium hover:underline"
                      >
                        {selected.convertedCaseNumber}
                      </Link>
                      <p className="mt-1 text-xs text-muted-foreground">
                        The case carries this concern&apos;s subject and statement only — never the
                        thread, which may hold things the reporter said precisely because they were
                        anonymous, and which the case&apos;s primary party can read.
                      </p>
                    </div>
                  )}

                  {!isFinished(selected) && (
                    <div className="flex flex-wrap gap-2">
                      <Button variant="outline" size="sm" onClick={() => {
                        setTriageNotes(selected.triageNotes ?? ''); setTriageOpen(true);
                      }}>
                        <ShieldQuestion className="mr-2 h-4 w-4" /> Triage
                      </Button>
                      <Button variant="outline" size="sm" onClick={() => setConvertOpen(true)}>
                        <ArrowRightLeft className="mr-2 h-4 w-4" /> Open a case from it
                      </Button>
                      <Button variant="outline" size="sm" onClick={() => setCloseOpen(true)}>
                        <Lock className="mr-2 h-4 w-4" /> Close it
                      </Button>
                    </div>
                  )}
                </CardContent>
              </Card>

              <Card>
                <CardHeader>
                  <CardTitle className="text-base">Thread with the reporter</CardTitle>
                  <p className="mt-1 text-xs text-muted-foreground">
                    They read this using their retrieval code. Your replies are attributed to you;
                    theirs are attributed to nobody.
                  </p>
                </CardHeader>
                <CardContent className="space-y-3">
                  {selected.updates.length === 0 ? (
                    <p className="text-sm text-muted-foreground">Nothing has been said yet.</p>
                  ) : (
                    selected.updates.map((u) => (
                      <div
                        key={u.id}
                        className={`rounded-md border p-3 text-sm ${u.isFromReporter ? 'bg-muted/40' : ''}`}
                      >
                        <div className="text-xs text-muted-foreground">
                          {/* authorName is null for the reporter, by design — say so rather than
                              rendering an empty byline. */}
                          {u.isFromReporter ? 'The reporter (anonymous)' : u.authorName}
                          {' · '}{fmtDateTime(u.postedAt)}
                        </div>
                        <p className="mt-1 whitespace-pre-wrap">{u.body}</p>
                      </div>
                    ))
                  )}

                  {!isFinished(selected) && (
                    <div className="space-y-2 pt-2">
                      <Label htmlFor="reply">Reply</Label>
                      <Textarea
                        id="reply" rows={3} value={replyBody}
                        onChange={(e) => setReplyBody(e.target.value)}
                        placeholder="Ask for the detail you need. They can answer without giving a name."
                      />
                      <Button
                        size="sm"
                        disabled={replyBody.trim().length < 5 || reply.isPending}
                        onClick={() => reply.mutate({ id: selected.id })}
                      >
                        {reply.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                        <Send className="mr-2 h-4 w-4" /> Send
                      </Button>
                    </div>
                  )}
                </CardContent>
              </Card>
            </>
          )}
        </div>
      </div>

      <Dialog open={triageOpen} onOpenChange={setTriageOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Triage this concern</DialogTitle>
            <DialogDescription>
              What you made of it, and where that leaves it.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1">
              <Label htmlFor="tri-notes">Notes</Label>
              <Textarea id="tri-notes" rows={4} value={triageNotes} onChange={(e) => setTriageNotes(e.target.value)} />
            </div>
            <div className="space-y-1">
              <Label>Where it stands</Label>
              <Select value={triageStatus} onValueChange={(v) => setTriageStatus(v as typeof triageStatus)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {CONCERN_TRIAGE_STATUS_OPTIONS.map((o) => (
                    <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {/*
                "New" and "Converted to a case" are absent on purpose and the server refuses both:
                moving back to New would erase that anybody looked, and claiming Converted here
                would name a case that does not exist.
              */}
              <p className="text-xs text-muted-foreground">
                A concern cannot be put back to &ldquo;New&rdquo; — that would erase the fact that
                somebody looked at it.
              </p>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setTriageOpen(false)}>Cancel</Button>
            <Button
              disabled={!selected || triageNotes.trim().length < 10 || triage.isPending}
              onClick={() => { if (selected) triage.mutate({ id: selected.id }); }}
            >Record</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={closeOpen} onOpenChange={setCloseOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Close this concern</DialogTitle>
            <DialogDescription>
              The reporter can still read the reason with their code, so write it for them.
            </DialogDescription>
          </DialogHeader>
          <Textarea rows={4} value={closeReason} onChange={(e) => setCloseReason(e.target.value)} />
          <DialogFooter>
            <Button variant="outline" onClick={() => setCloseOpen(false)}>Cancel</Button>
            <Button
              disabled={!selected || closeReason.trim().length < 10 || closeConcern.isPending}
              onClick={() => { if (selected) closeConcern.mutate({ id: selected.id }); }}
            >Close it</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={convertOpen} onOpenChange={setConvertOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Open a case from this concern</DialogTitle>
            <DialogDescription>
              The new case carries this concern&apos;s subject and statement — and nothing from the
              thread, which the case&apos;s primary party would be able to read.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div className="space-y-1">
              <Label>Case type</Label>
              <Select value={convertType} onValueChange={(v) => setConvertType(v as OpenableCaseType)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  {ER_OPENABLE_CASE_TYPES.map((o) => (
                    <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {/* Not offered, and the server refuses it: it would be a grievance raised on
                  somebody's behalf by a longer route. */}
              <p className="text-xs text-muted-foreground">
                Never a grievance — only the employee themselves can raise one of those.
              </p>
            </div>
            <div className="space-y-1">
              <Label>Primary party</Label>
              <EmployeePicker
                value={convertEmployee}
                onChange={(v) => setConvertEmployee(v)}
                placeholder="Whom is the new case about?"
              />
              <p className="text-xs text-muted-foreground">
                Naming somebody here does not name the reporter — the two are unrelated, and the
                reporter stays unknown.
              </p>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConvertOpen(false)}>Cancel</Button>
            <Button
              disabled={!selected || !convertEmployee || convert.isPending}
              onClick={() => {
                if (selected && convertEmployee) convert.mutate({ id: selected.id, employeeId: convertEmployee });
              }}
            >Open the case</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
